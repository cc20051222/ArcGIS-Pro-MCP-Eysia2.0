namespace ArcGISProMCP.Core.Models;

/// <summary>工程信息（纯数据）。</summary>
public sealed class ProjectInfo
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDirty { get; set; }
    public string? DefaultGeodatabase { get; set; }
    public string? HomeFolder { get; set; }
}
