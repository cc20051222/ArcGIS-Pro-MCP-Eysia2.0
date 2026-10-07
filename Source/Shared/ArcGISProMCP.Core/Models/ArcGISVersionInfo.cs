namespace ArcGISProMCP.Core.Models;

/// <summary>ArcGIS Pro 版本信息（纯数据）。</summary>
public sealed class ArcGISVersionInfo
{
    public string ProductVersion { get; set; } = string.Empty;
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Build { get; set; }
    public string TargetFramework { get; set; } = string.Empty;
}
