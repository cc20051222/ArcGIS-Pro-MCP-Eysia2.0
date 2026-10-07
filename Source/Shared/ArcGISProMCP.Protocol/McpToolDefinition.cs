namespace ArcGISProMCP.Protocol;

/// <summary>
/// MCP 工具定义（tools/list 返回项）。
/// name/description/inputSchema 为 MCP 标准字段；displayName/category/executionType 为可选扩展字段。
/// </summary>
public sealed class McpToolDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, object?> InputSchema { get; set; } = new Dictionary<string, object?>();

    public string DisplayName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string ExecutionType { get; set; } = string.Empty;
}
