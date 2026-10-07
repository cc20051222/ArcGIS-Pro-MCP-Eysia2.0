namespace ArcGISProMCP.Core.Models;

/// <summary>布局信息（纯数据）。</summary>
public sealed class LayoutInfo
{
    public string Name { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
}
