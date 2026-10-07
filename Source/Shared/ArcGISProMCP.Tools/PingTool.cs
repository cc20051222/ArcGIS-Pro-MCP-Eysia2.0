using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>内置 Ping 工具（ArcGIS 无关，用于链路自检）。</summary>
public sealed class PingTool : IMCPTool
{
    public string Name => "ping";

    public string Description => "返回 pong，用于验证 MCP 调用链。";

    public IReadOnlyDictionary<string, object?> InputSchema => ToolSchemas.Object();

    public Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        return Task.FromResult(OperationResult<object?>.Ok("pong"));
    }
}
