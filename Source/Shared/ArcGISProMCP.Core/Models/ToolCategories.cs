namespace ArcGISProMCP.Core.Models;

/// <summary>工具分类常量。（仅约束，不强校验）</summary>
public static class ToolCategories
{
    public const string General = "General";
    public const string Map = "Map";
    public const string Layer = "Layer";
    public const string Attribute = "Attribute";
    public const string Selection = "Selection";
    public const string Analysis = "Analysis";
    public const string Quality = "Quality";
    public const string Geoprocessing = "Geoprocessing";
    public const string DataManagement = "DataManagement";
    public const string Raster = "Raster";
    public const string Layout = "Layout";
    public const string Project = "Project";
    public const string System = "System";

    /// <summary>Python / ArcPy 扩展通道（Phase 5）。</summary>
    public const string Python = "Python";

    /// <summary>Photoshop / UXP bridge channel; peer behavior is separately verified.</summary>
    public const string Photoshop = "Photoshop";
}
