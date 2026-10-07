namespace ArcGISProMCP.Core.Models;

/// <summary>工具执行类型常量。</summary>
public static class ExecutionTypes
{
    /// <summary>C# Native（ArcGIS Pro SDK 直接完成）。</summary>
    public const string Native = "Native";

    /// <summary>Geoprocessing（统一 GP Executor）。</summary>
    public const string Geoprocessing = "Geoprocessing";

    /// <summary>Python / ArcPy（Phase 5 再实现，Phase 4 仅保留扩展能力）。</summary>
    public const string Python = "Python";

    /// <summary>Photoshop / UXP bridge channel.</summary>
    public const string Bridge = "Bridge(PS/UXP)";
}
