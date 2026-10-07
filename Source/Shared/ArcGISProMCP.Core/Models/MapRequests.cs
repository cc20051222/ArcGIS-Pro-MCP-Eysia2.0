namespace ArcGISProMCP.Core.Models;

/// <summary>属性查询请求。</summary>
public sealed class AttributeQueryRequest
{
    public string MapName { get; set; } = string.Empty;
    public string LayerName { get; set; } = string.Empty;
    public string? WhereClause { get; set; }
    public IReadOnlyList<string>? FieldNames { get; set; }
    public int MaxFeatures { get; set; } = 1000;
}

/// <summary>地理处理请求。</summary>
public sealed class GeoprocessingRequest
{
    public string ToolName { get; set; } = string.Empty;

    /// <summary>按工具参数顺序排列的值（字符串形式，GP values）。</summary>
    public IReadOnlyList<string>? Values { get; set; }
}
