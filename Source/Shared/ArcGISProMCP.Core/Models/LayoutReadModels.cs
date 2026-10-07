namespace ArcGISProMCP.Core.Models;

/// <summary>D-042：布局详情（get_layout_info）。</summary>
public sealed class LayoutDetailInfo
{
    public string Name { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    /// <summary>页面宽（CIMPage.Width，单位随 Units）。</summary>
    public double? PageWidth { get; set; }
    /// <summary>页面高（CIMPage.Height）。</summary>
    public double? PageHeight { get; set; }
    /// <summary>页面单位（CIMPage.Units 枚举名，如 Inches / Centimeters）。</summary>
    public string? PageUnits { get; set; }
    /// <summary>元素计数（GetElementsAsFlattenedList 展平数）。</summary>
    public int ElementCount { get; set; }
}

/// <summary>D-042：布局元素（list_layout_elements，展平清单）。</summary>
public sealed class LayoutElementInfo
{
    public string Name { get; set; } = string.Empty;
    /// <summary>元素运行时类型名（TextElement / MapFrame / GroupElement …）。</summary>
    public string ElementType { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
}

/// <summary>D-042：地图范围（get_map_extent；口径 = Map.GetDefaultExtent 工程态，不依赖活动视图）。</summary>
public sealed class MapExtentInfo
{
    public string MapName { get; set; } = string.Empty;
    public double? XMin { get; set; }
    public double? YMin { get; set; }
    public double? XMax { get; set; }
    public double? YMax { get; set; }
    public string? SpatialReferenceName { get; set; }
    /// <summary>口径披露："default-extent"（Map.GetDefaultExtent 工程态口径）。</summary>
    public string ExtentSource { get; set; } = "default-extent";
}

/// <summary>D-042：图层定义查询（get_definition_query）。null = 该图层类型不支持定义查询（如 GroupLayer）；
/// 空串 = 支持 但未设置；非空 = 查询原文。三者可判别（G-82-C 口径）。</summary>
public sealed class DefinitionQueryInfo
{
    public string LayerName { get; set; } = string.Empty;
    /// <summary>true = 图层类型支持定义查询（DefinitionQuery 属性存在）；false = 不支持（如 GroupLayer）。</summary>
    public bool SupportsDefinitionQuery { get; set; }
    /// <summary>查询原文；不支持 → null；支持但未设置 → 空串。</summary>
    public string? DefinitionQuery { get; set; }
}
