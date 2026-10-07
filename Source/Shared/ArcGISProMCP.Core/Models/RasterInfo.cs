namespace ArcGISProMCP.Core.Models;

/// <summary>栅格信息（纯数据）。</summary>
public sealed class RasterInfo
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string PixelType { get; set; } = string.Empty;
    public int Bands { get; set; }
}
