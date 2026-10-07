using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Core.Tools;

/// <summary>
/// MCP 工具路由：接收 ToolCall，找到工具，校验参数，创建执行上下文，调用工具，
/// 捕获异常并转换为统一 OperationResult。路由只负责调度，不实现 GIS 业务。
/// </summary>
public sealed class MCPToolRouter
{
    private readonly MCPToolRegistry _registry;
    private readonly IArcGISHost _host;
    private readonly MCPSettings _settings;
    private readonly ILogger _logger;
    private readonly IPythonBridgeService? _pythonBridge;
    private readonly IToolInvoker _invoker;

    /// <summary>D-064：会话级只读模式服务（可空 ⇒ 不启用闸门，保既有构造点/测试零改动）。</summary>
    private readonly IReadOnlyModeService? _readOnly;

    public MCPToolRouter(
        MCPToolRegistry registry,
        IArcGISHost host,
        MCPSettings settings,
        ILogger logger,
        IPythonBridgeService? pythonBridge = null,
        IReadOnlyModeService? readOnly = null,
        IToolInvoker? invoker = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pythonBridge = pythonBridge;
        _readOnly = readOnly;
        _invoker = invoker ?? ToolInvoker.Default;
    }

    public async Task<OperationResult<object?>> ExecuteAsync(MCPToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (call.CancellationToken.IsCancellationRequested)
        {
            return Cancelled(call);
        }

        var tool = _registry.Get(call.Name);
        if (tool is null)
        {
            var r = OperationResult<object?>.Fail(ErrorCodes.ToolNotFound, $"Tool '{call.Name}' was not found.");
            return Finalize(r, TimeSpan.Zero, call);
        }

        var context = new ToolExecutionContext
        {
            RequestId = call.RequestId,
            CancellationToken = call.CancellationToken,
            Logger = _logger,
            Host = _host,
            Python = _pythonBridge,
            Settings = _settings,
            Arguments = call.Arguments,
            Registry = _registry,
            ReadOnly = _readOnly,
            Invoker = _invoker
        };
        return await _invoker.InvokeAsync(tool, context).ConfigureAwait(false);
    }

    private OperationResult<object?> Cancelled(MCPToolCall call, TimeSpan? elapsed = null)
    {
        var r = OperationResult<object?>.Fail(ErrorCodes.Cancelled, $"Tool '{call.Name}' was cancelled.");
        if (elapsed.HasValue)
        {
            r.ExecutionTime = elapsed.Value;
        }

        r.RequestId = call.RequestId;
        return r;
    }

    private OperationResult<object?> Finalize(OperationResult<object?> result, TimeSpan elapsed, MCPToolCall call)
    {
        result.ExecutionTime = elapsed;
        result.RequestId = call.RequestId;
        _logger.Log(new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = result.Success ? LogLevel.Information : LogLevel.Warning,
            Category = "router",
            RequestId = call.RequestId,
            Tool = call.Name,
            Message = result.Success ? "tool call succeeded" : "tool call failed",
            ExecutionTime = elapsed,
            Result = result.Success ? "SUCCESS" : string.Join(";", result.Errors.Select(e => e.Code))
        });
        return result;
    }
}
