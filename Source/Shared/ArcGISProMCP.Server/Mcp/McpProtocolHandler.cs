using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Protocol;
using ArcGISProMCP.Server.JsonRpc;

namespace ArcGISProMCP.Server.Mcp;

/// <summary>
/// MCP 协议处理器：把 MCP method（initialize / initialized / tools/list / tools/call）
/// 映射到工具注册表与工具路由。不含传输、不含 GIS 业务。
/// </summary>
public sealed class McpProtocolHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MCPToolRegistry _registry;
    private readonly MCPToolRouter _router;
    private readonly McpServerInfo _serverInfo;

    /// <summary>Phase 8.4（D-016）：notifications/cancelled 回调（由 McpServer 注入活动请求注册表）。</summary>
    public Action<string>? CancelNotification { get; set; }

    public McpProtocolHandler(MCPToolRegistry registry, MCPToolRouter router, McpServerInfo serverInfo)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
    }

    /// <summary>
    /// 分发 MCP method。返回 null 表示通知（无响应）。
    /// </summary>
    public async Task<object?> DispatchAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        switch (request.Method)
        {
            case "initialize":
                return Initialize(request);
            case "notifications/initialized":
                return null;
            case "notifications/cancelled":
                // Phase 8.4（D-016，G-37）：主动取消。幂等：未知/已完成/重复 → 静默忽略。
                if (request.Params is { } cp && cp.TryGetProperty("requestId", out var ridEl))
                {
                    var rid = ridEl.ToString();
                    if (!string.IsNullOrEmpty(rid))
                    {
                        CancelNotification?.Invoke(rid);
                    }
                }

                return null;
            case "tools/list":
                return ListTools();
            case "tools/call":
                return await CallToolAsync(request, cancellationToken).ConfigureAwait(false);
            default:
                throw new JsonRpcMethodNotFoundException(request.Method);
        }
    }

    private McpInitializeResult Initialize(JsonRpcRequest request)
    {
        if (request.Params is { } p && p.ValueKind != JsonValueKind.Object)
        {
            throw new JsonRpcInvalidParamsException("initialize params must be an object.");
        }

        return new McpInitializeResult
        {
            ProtocolVersion = McpConstants.ProtocolVersion,
            Capabilities = new McpServerCapabilities { Tools = new McpToolsCapability { ListChanged = false } },
            ServerInfo = _serverInfo
        };
    }

    private McpToolsListResult ListTools()
    {
        var tools = _registry.List()
            .Select(t =>
            {
                var md = t.Metadata;
                return new McpToolDefinition
                {
                    Name = t.Name,
                    Description = md.Description,
                    InputSchema = t.InputSchema,
                    DisplayName = md.DisplayName,
                    Category = md.Category,
                    ExecutionType = md.ExecutionType
                };
            })
            .ToList();

        return new McpToolsListResult { Tools = tools };
    }

    private async Task<McpToolCallResult> CallToolAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (request.Params is not { } p || p.ValueKind != JsonValueKind.Object)
        {
            throw new JsonRpcInvalidParamsException("tools/call params must be an object.");
        }

        string? name = null;
        IReadOnlyDictionary<string, object?>? arguments = null;

        if (p.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
        {
            name = nameEl.GetString();
        }

        if (p.TryGetProperty("arguments", out var argsEl) && argsEl.ValueKind == JsonValueKind.Object)
        {
            arguments = JsonValueConverter.Convert(argsEl) as IReadOnlyDictionary<string, object?>;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new JsonRpcInvalidParamsException("tools/call requires a 'name'.");
        }

        var call = new MCPToolCall
        {
            Name = name!,
            Arguments = arguments,
            RequestId = request.Id?.ToString(),
            CancellationToken = cancellationToken
        };

        var result = await _router.ExecuteAsync(call).ConfigureAwait(false);

        if (result.Success)
        {
            // D-063 ★ 回图：工具返回地图图像 ⇒ 产出 image content（附文字摘要在前，兼容端先读文再读图）。
            if (result.Data is MapViewImageResult img)
            {
                var summary = JsonSerializer.Serialize(new
                {
                    mapName = img.MapName,
                    width = img.Width,
                    height = img.Height,
                    bytes = img.Bytes,
                    resolutionDpi = img.ResolutionDpi,
                    outputPath = img.OutputPath,
                    usedTransientFile = img.UsedTransientFile,
                    transientFileDeleted = img.TransientFileDeleted,
                    constraints = img.ConstraintsNote,
                }, JsonOptions);

                return new McpToolCallResult
                {
                    IsError = false,
                    Content = new object[]
                    {
                        new McpTextContent { Type = "text", Text = summary },
                        new McpImageContent { Type = "image", Data = img.DataBase64, MimeType = img.MimeType },
                    }
                };
            }

            var text = result.Data is string s ? s : JsonSerializer.Serialize(result.Data, JsonOptions);
            return new McpToolCallResult
            {
                IsError = false,
                Content = new object[] { new McpTextContent { Type = "text", Text = text } }
            };
        }

        var errorText = string.Join("; ", result.Errors.Select(e => e.ToString()));
        return new McpToolCallResult
        {
            IsError = true,
            Content = new[] { new McpTextContent { Type = "text", Text = errorText } }
        };
    }
}
