namespace ArcGISProMCP.Core.Models;

/// <summary>当前选择集信息（纯数据）。</summary>
public sealed class SelectionInfo
{
    public string MapName { get; set; } = string.Empty;
    public string LayerName { get; set; } = string.Empty;
    public long Count { get; set; }
}
