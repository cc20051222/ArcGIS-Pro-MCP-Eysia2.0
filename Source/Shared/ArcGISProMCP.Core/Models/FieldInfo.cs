namespace ArcGISProMCP.Core.Models;

/// <summary>字段（纯数据）。</summary>
public sealed class FieldInfo
{
    public string Name { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public int Length { get; set; }
}
