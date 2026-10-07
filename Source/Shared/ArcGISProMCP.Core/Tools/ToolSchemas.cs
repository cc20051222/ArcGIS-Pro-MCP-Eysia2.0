namespace ArcGISProMCP.Core.Tools;

/// <summary>
/// 工具 InputSchema 约定为 JSON Schema 对象（type/properties/required）。
/// </summary>
public static class ToolSchemas
{
    /// <summary>空对象 JSON Schema：{ "type": "object", "properties": {} }。</summary>
    public static IReadOnlyDictionary<string, object?> Object()
        => new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>()
        };
}
