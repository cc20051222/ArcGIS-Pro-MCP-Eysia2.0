namespace ArcGISProMCP.Core.Models;

/// <summary>数据集信息（纯数据）。</summary>
public sealed class DatasetInfo
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    /// <summary>D-061：空间参考名称（可判定则填；不可判定为 null，**不臆测**）。</summary>
    public string? SpatialReference { get; set; }

    /// <summary>D-061：像元大小 X（仅栅格；非栅格为 null）。</summary>
    public double? CellSizeX { get; set; }

    /// <summary>D-061：像元大小 Y（仅栅格；非栅格为 null）。</summary>
    public double? CellSizeY { get; set; }

    /// <summary>D-061：波段数（仅栅格；不可判定为 null）。</summary>
    public int? BandCount { get; set; }
}
