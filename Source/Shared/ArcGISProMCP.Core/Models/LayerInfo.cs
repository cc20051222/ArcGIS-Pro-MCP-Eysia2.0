namespace ArcGISProMCP.Core.Models;

/// <summary>图层（纯数据）。</summary>
public sealed class LayerInfo
{
    public string Name { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string LayerType { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
    public string MapName { get; set; } = string.Empty;
}
