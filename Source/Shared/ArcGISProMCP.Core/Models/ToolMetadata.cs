namespace ArcGISProMCP.Core.Models;

/// <summary>
/// 工具统一元数据（Tool Metadata / Contract）。纯数据，用于 tools/list 与 AI 可理解性。
/// </summary>
public sealed class ToolMetadata
{
    /// <summary>MCP 工具名（snake_case，唯一）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>面向人类的显示名（可中文）。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>面向 AI 的描述（输入/输出/场景/参数/前置条件）。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>工具分类，见 ToolCategories。</summary>
    public string Category { get; set; } = "General";

    /// <summary>工具版本。</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>执行类型：Native / Geoprocessing / Python / Bridge(PS/UXP)（见 ExecutionTypes）。</summary>
    public string ExecutionType { get; set; } = ExecutionTypes.Native;

    /// <summary>是否需要 ArcGIS（True=需 ArcGIS Host）。</summary>
    public bool RequiresArcGIS { get; set; }

    /// <summary>是否需要活动地图。</summary>
    public bool RequiresActiveMap { get; set; }

    /// <summary>是否支持取消。</summary>
    public bool SupportsCancellation { get; set; } = true;

    /// <summary>预期超时（秒），0=默认。</summary>
    public int TimeoutSeconds { get; set; }
}
