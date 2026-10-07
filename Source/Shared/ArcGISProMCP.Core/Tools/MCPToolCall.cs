namespace ArcGISProMCP.Core.Tools;

/// <summary>一次工具调用（由未来的 MCP Server 构造，本阶段用于前置测试）。</summary>
public sealed class MCPToolCall
{
    public string Name { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, object?>? Arguments { get; init; }

    public string? RequestId { get; init; }

    public CancellationToken CancellationToken { get; init; }
}
