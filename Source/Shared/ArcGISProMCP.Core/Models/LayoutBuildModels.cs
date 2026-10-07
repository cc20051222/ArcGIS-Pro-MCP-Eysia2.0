namespace ArcGISProMCP.Core.Models;

/// <summary>D-045：新建布局结果（create_layout）。页面参数为创建后**读回**值。</summary>
public sealed class LayoutCreateInfo
{
    public string Name { get; set; } = string.Empty;

    /// <summary>创建后读回的页面宽（页面单位）。</summary>
    public double PageWidth { get; set; }

    /// <summary>创建后读回的页面高（页面单位）。</summary>
    public double PageHeight { get; set; }

    /// <summary>页面单位（Inches / Centimeters / Millimeters / Points）。</summary>
    public string PageUnits { get; set; } = string.Empty;

    /// <summary>创建后布局内的元素计数。</summary>
    public int ElementCount { get; set; }

    /// <summary>已绑定的地图框名（未绑定 mapName 时为 null）。</summary>
    public string? MapFrameName { get; set; }

    /// <summary>写入面披露（就地新建、无元素级回滚）。</summary>
    public string WriteTarget { get; set; } = "LayoutFactory.CreateLayout";

    /// <summary>页面单位与 DPI 披露（spike 结论：SDK 无 DPI 可达面）。</summary>
    public string Notes { get; set; } = "page units as requested; DPI not settable via SDK (no reachable member)";
}

/// <summary>D-045：向既有布局添加元素的结果（add_layout_text / add_legend / add_north_arrow / add_scale_bar）。
/// <see cref="ElementName"/> 为写入后**读回**的元素名，供 <c>list_layout_elements</c> 逐字对照。</summary>
public sealed class LayoutElementAddInfo
{
    public string LayoutName { get; set; } = string.Empty;

    public string ElementName { get; set; } = string.Empty;

    /// <summary>元素运行时类型名（TextElement / Legend / NorthArrow / ScaleBar / MapFrame …）。</summary>
    public string ElementType { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>锚定地图框名（图例/指北针/比例尺；文本为 null）。</summary>
    public string? AnchorMapFrame { get; set; }

    /// <summary>添加后布局内的元素计数（读回）。</summary>
    public int ElementCount { get; set; }

    /// <summary>写入面披露（就地修改既有布局；元素级删除/重命名不在本批参数面，Phase 12）。</summary>
    public string WriteTarget { get; set; } = "ElementFactory.Create*";
}
