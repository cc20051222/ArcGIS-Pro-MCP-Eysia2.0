namespace ArcGISProMCP.Core.Models;

/// <summary>
/// Phase 9 第四批（D-038）：字段值域画像（只读，get_field_values）。
/// 语义：TotalCount=表内总记录数；NullCount/NonNullCount 为扫描所得；DistinctValues 为**字符串化**后的
/// 去重值（上限 maxDistinct，超出 → Truncated=true，不返回无界数据）；Min/Max 为非空值中同类型可比较的
/// 最小/最大（格式化字符串，InvariantCulture）。空表（TotalCount=0）与全空字段（NonNullCount=0）可区分。
/// </summary>
public sealed class FieldValuesInfo
{
    public string MapName { get; set; } = string.Empty;

    public string LayerName { get; set; } = string.Empty;

    public string FieldName { get; set; } = string.Empty;

    public long TotalCount { get; set; }

    public long NullCount { get; set; }

    public long NonNullCount { get; set; }

    public int DistinctCount { get; set; }

    public bool Truncated { get; set; }

    public List<string> DistinctValues { get; set; } = new();

    public string? MinValue { get; set; }

    public string? MaxValue { get; set; }

    /// <summary>D-061（get_unique_values）：调用方请求的 topN（未指定时 null）。</summary>
    public int? RequestedMaxDistinct { get; set; }

    /// <summary>D-061（get_unique_values）：实际施加的上限值（被夹取时与 RequestedMaxDistinct 不同）。</summary>
    public int AppliedMaxDistinct { get; set; }

    /// <summary>D-061（get_unique_values）：请求值超过上限被夹取（true ⇒ Truncated 语义之一，需显式披露）。</summary>
    public bool Clamped { get; set; }
}
