namespace ArcGISProMCP.Core.Models;

/// <summary>D-047：布局导出结果（export_layout_pdf / export_layout_png）。
/// 导出 = **新建文件**（可复原 = 删除产物）；本模型为**导出后读回**的产物事实。</summary>
public sealed class LayoutExportInfo
{
    public string LayoutName { get; set; } = string.Empty;

    /// <summary>产物绝对路径。</summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>导出格式：PDF / PNG。</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>导出分辨率（DPI）；未指定时 null（SDK 默认 96）。</summary>
    public double? Resolution { get; set; }

    /// <summary>产物字节数（导出后实测）。</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>是否覆盖了既有文件（false = 新建）。</summary>
    public bool Overwritten { get; set; }

    /// <summary>产物文件头（十六进制，最多 8 字节）——供调用方校验 magic bytes。</summary>
    public string MagicBytesHex { get; set; } = string.Empty;

    /// <summary>形态披露。</summary>
    public string Notes { get; set; } =
        "export creates a new file (revert = delete the artifact); " +
        "width/height are not exposed: SDK documents them as map-view-only (ignored for layouts); " +
        "map-level export is not available in this batch (requires active MapView)";
}
