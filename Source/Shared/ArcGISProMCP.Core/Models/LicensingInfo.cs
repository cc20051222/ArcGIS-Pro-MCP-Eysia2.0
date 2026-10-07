namespace ArcGISProMCP.Core.Models;

/// <summary>许可信息（纯数据）。</summary>
public sealed class LicensingInfo
{
    public string LicenseLevel { get; set; } = string.Empty;
    public IReadOnlyList<string> ExtensionsAvailable { get; set; } = Array.Empty<string>();
}
