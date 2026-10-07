using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.Protocol;
using ArcGISProMCP.Server.JsonRpc;
using ArcGISProMCP.Server.Mcp;
using ArcGISProMCP.Server.Transport;

namespace ArcGISProMCP.Server;

/// <summary>
/// MCP Server：编排 Transport → JSON-RPC → MCP method → Tool Router。
/// 不直接实现 GIS 操作。
/// </summary>
public sealed class McpServer
{
    private readonly IMcpTransport _transport;
    private readonly McpProtocolHandler _protocol;
    private readonly MCPSettings _settings;
    private readonly MCPToolRegistry _registry;   // D-064：按工具执行类型选择请求预算
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _concurrency;

    /// <summary>D-056 C1：当前**等待准入**的请求数（已准入者即出队）。</summary>
    private int _queuedRequests;

    /// <summary>D-056 C1：等待队列深度上限；0 = 不设上限（历史行为）。</summary>
    private readonly int _maxQueuedRequests;

    /// <summary>Phase 8.4（D-016）：活动请求注册表（notifications/cancelled 取消）。</summary>
    private readonly ActiveRequestRegistry _activeRequests = new();

    /// <summary>被取消通知命中的 requestId（响应码用 CANCELLED 而非 REQUEST_TIMEOUT）。</summary>
    private readonly ConcurrentDictionary<string, bool> _cancelledByNotification = new();

    public McpServer(
        IMcpTransport transport,
        MCPToolRegistry registry,
        MCPToolRouter router,
        MCPSettings settings,
        ILogger logger,
        McpServerInfo? serverInfo = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _protocol = new McpProtocolHandler(
            registry ?? throw new ArgumentNullException(nameof(registry)),
            router ?? throw new ArgumentNullException(nameof(router)),
            serverInfo ?? new McpServerInfo { Name = McpConstants.ServerName, Version = McpConstants.ServerVersion })
        {
            CancelNotification = rid =>
            {
                if (_activeRequests.Cancel(rid))
                {
                    _cancelledByNotification[rid] = true;
                }
            },
        };
        _concurrency = new SemaphoreSlim(Math.Max(1, settings.MaxConcurrentRequests));
        _maxQueuedRequests = Math.Max(0, settings.MaxQueuedRequests);
    }

    public bool IsRunning => _transport.IsRunning;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _transport.RequestHandler = HandleRequestAsync;
        await _transport.StartAsync(cancellationToken).ConfigureAwait(false);
        _logger.Info("MCP server started.");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _transport.StopAsync(cancellationToken).ConfigureAwait(false);
        _logger.Info("MCP server stopped.");
    }

    /// <summary>
    /// 请求预算（毫秒）选择：**交互类**（含 tools/list、只读查询等）用 <c>RequestTimeoutMs</c>；
    /// **GP / Python 执行类**（以及 30 s 自限的 <c>run_batch</c>）用 <c>GeoprocessingRequestTimeoutMs</c>。
    /// <para>D-064 阶段二真缺陷 ⑨：GP 的固有耗时（数据落盘/建 GDB/大数据量分析）与交互查询不是一个量级，
    /// 用 30 s 交互预算会**在 GP 执行中被取消**并留下半成品数据集。</para>
    /// </summary>
    private int ResolveRequestBudgetMs(JsonRpcRequest request)
    {
        var baseMs = _settings.RequestTimeoutMs;
        var extendedMs = _settings.GeoprocessingRequestTimeoutMs;
        if (baseMs <= 0 || extendedMs <= baseMs || request.Method != "tools/call")
        {
            return baseMs;
        }

        try
        {
            if (request.Params is { } p && p.ValueKind == JsonValueKind.Object
                && p.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            {
                var name = nameEl.GetString() ?? string.Empty;
                var executionType = _registry.Get(name)?.Metadata.ExecutionType;
                if (string.Equals(executionType, ExecutionTypes.Geoprocessing, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(executionType, ExecutionTypes.Python, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "run_batch", StringComparison.Ordinal))
                {
                    return extendedMs;
                }
            }
        }
        catch
        {
            // 预算解析失败 ⇒ 退回交互预算（不因解析异常改变安全语义）。
        }

        return baseMs;
    }

    /// <summary>
    /// 处理一条原始请求文本，返回 JSON-RPC 响应文本；通知返回 null。
    /// </summary>
    public async Task<string?> HandleRequestAsync(string requestJson, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (!JsonRpcCodec.TryParseRequest(requestJson, out var request, out var parseError))
        {
            _logger.Log(new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = LogLevel.Warning,
                Category = "mcp",
                Message = "JSON-RPC parse error",
                Error = $"{parseError!.Code}: {parseError.Message}",
                ExecutionTime = sw.Elapsed
            });
            return JsonRpcCodec.BuildError(null, parseError.Code, parseError.Message);
        }

        var method = request!.Method;
        var requestId = request.Id?.ToString();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var budgetMs = ResolveRequestBudgetMs(request!);
        if (budgetMs > 0)
        {
            timeoutCts.CancelAfter(budgetMs);
        }

        // Phase 8.4（D-016）：注册活动请求供 notifications/cancelled 取消（弱引用，finally 移除）。
        _activeRequests.Register(requestId, timeoutCts);

        // D-056 C1（兑现 G-147 登记的「等待队列无深度上限」，本批经 G-149 批准执行）：
        // 队列深度上限 —— 超出即**不等待**直接拒绝，避免极端并发下等待者无界堆积。
        // 复用注册错误码 REQUEST_TIMEOUT（错误码注册表保持 33 项零新增）；以 `(queue-full)` 限定词与
        // 既有「等待超时」`(queued)` 区分；前导 token 仍是注册码，符合 `CODE: message` 契约形态。
        var queueDepth = Interlocked.Increment(ref _queuedRequests);
        if (_maxQueuedRequests > 0 && queueDepth > _maxQueuedRequests)
        {
            Interlocked.Decrement(ref _queuedRequests);
            _activeRequests.Remove(requestId);
            var overflow = $"REQUEST_TIMEOUT (queue-full): concurrency queue depth limit ({_maxQueuedRequests}) exceeded; rejected without waiting.";
            _logger.Log(new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = LogLevel.Warning,
                Category = "mcp",
                RequestId = requestId,
                MessageCode = "MCP_REQUEST_FAILED",
                ErrorCode = ErrorCodes.RequestTimeout,
            });
            if (method == "tools/call")
            {
                return JsonRpcCodec.BuildResponse(request.Id, new McpToolCallResult
                {
                    IsError = true,
                    Content = new[] { new McpTextContent { Type = "text", Text = overflow } }
                });
            }

            return JsonRpcCodec.BuildError(request.Id, JsonRpcErrorCodes.InternalError, overflow);
        }

        try
        {
            await _concurrency.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 未能准入 ⇒ 出队（D-056 C1）
            Interlocked.Decrement(ref _queuedRequests);
            // Phase 8.4（D-016，G-37）：排队超时/被取消分码。
            if (_cancelledByNotification.TryRemove(requestId ?? string.Empty, out _))
            {
                return JsonRpcCodec.BuildError(request.Id, JsonRpcErrorCodes.InternalError, "CANCELLED: request cancelled while queued");
            }

            if (method == "tools/call")
            {
                return JsonRpcCodec.BuildResponse(request.Id, new McpToolCallResult
                {
                    IsError = true,
                    Content = new[] { new McpTextContent { Type = "text", Text = "REQUEST_TIMEOUT (queued): concurrency limit; timed out while waiting to execute." } }
                });
            }

            return JsonRpcCodec.BuildError(request.Id, JsonRpcErrorCodes.InternalError, "REQUEST_TIMEOUT (queued): request timed out while waiting to execute");
        }

        // 已准入 ⇒ 出队（D-056 C1）
        Interlocked.Decrement(ref _queuedRequests);

        try
        {
            // 通知：JSON-RPC 2.0 规定不回复（含错误）
            if (request.IsNotification)
            {
                try
                {
                    await _protocol.DispatchAsync(request, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Log(new LogEntry
                    {
                        Timestamp = DateTimeOffset.Now,
                        Level = LogLevel.Warning,
                        Category = "mcp",
                        RequestId = requestId,
                        Tool = method,
                        Message = "notification handling failed",
                        Error = ex.Message,
                        ExecutionTime = sw.Elapsed
                    });
                }

                return null;
            }

            object? result;
            try
            {
                result = await _protocol.DispatchAsync(request, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (JsonRpcMethodNotFoundException)
            {
                return BuildAndLogError(request.Id, method, requestId, JsonRpcErrorCodes.MethodNotFound, "Method not found", sw);
            }
            catch (JsonRpcInvalidParamsException ex)
            {
                return BuildAndLogError(request.Id, method, requestId, JsonRpcErrorCodes.InvalidParams, ex.Message, sw);
            }
            catch (OperationCanceledException)
            {
                // Phase 8.4（D-016，G-37）：区分 用户取消（CANCELLED）与 服务器超时（REQUEST_TIMEOUT executing）。
                if (_cancelledByNotification.TryRemove(requestId ?? string.Empty, out _))
                {
                    return BuildAndLogError(request.Id, method, requestId, JsonRpcErrorCodes.InternalError, "CANCELLED: request cancelled by notification", sw);
                }

                if (!cancellationToken.IsCancellationRequested && _settings.RequestTimeoutMs > 0)
                {
                    return BuildAndLogError(request.Id, method, requestId, JsonRpcErrorCodes.InternalError, "REQUEST_TIMEOUT (executing): request timed out during execution", sw);
                }

                return BuildAndLogError(request.Id, method, requestId, JsonRpcErrorCodes.InternalError, "Request cancelled", sw);
            }
            catch (Exception ex)
            {
                _logger.Error($"MCP request failed.", requestId: requestId, exception: ex);
                return BuildAndLogError(request.Id, method, requestId, JsonRpcErrorCodes.InternalError, "Internal error", sw);
            }

            if (result is null)
            {
                return null;
            }

            // Phase 8.4（D-016）：执行中服务器超时——工具返回 CANCELLED（ct 已贯穿），改写为 REQUEST_TIMEOUT(executing)。
            // 客户端显式取消 / 客户端断开不在此列。
            if (result is McpToolCallResult { IsError: true } toolResult
                && timeoutCts.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested
                && !_cancelledByNotification.TryRemove(requestId ?? string.Empty, out _)
                && _settings.RequestTimeoutMs > 0
                && toolResult.Content.FirstOrDefault() is McpTextContent { Text: { } } firstText
                && firstText.Text.StartsWith("CANCELLED:", StringComparison.Ordinal))
            {
                // D-063：Content 元素放宽为 object（承载 image content）⇒ 此处按文本块类型判别。
                firstText.Text = "REQUEST_TIMEOUT (executing): " + firstText.Text["CANCELLED:".Length..].TrimStart();
            }

            _logger.Log(new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = LogLevel.Information,
                Category = "mcp",
                RequestId = requestId,
                Tool = method,
                Message = "request succeeded",
                Result = "SUCCESS",
                ExecutionTime = sw.Elapsed
            });

            return JsonRpcCodec.BuildResponse(request.Id, result);
        }
        finally
        {
            _activeRequests.Remove(requestId);
            _concurrency.Release();
        }
    }

    private string BuildAndLogError(JsonElement? id, string method, string? requestId, int code, string message, Stopwatch sw)
    {
        _logger.Log(new LogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = LogLevel.Warning,
            Category = "mcp",
            RequestId = requestId,
            Tool = method,
            Message = "request failed",
            Error = $"{code}: {message}",
            ExecutionTime = sw.Elapsed
        });
        return JsonRpcCodec.BuildError(id, code, message);
    }
}
