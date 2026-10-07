namespace ArcGISProMCP.Core.Models;

/// <summary>D-043：定义查询写入结果（set_definition_query）。
/// 与 <see cref="DefinitionQueryInfo"/> 三态口径严格对齐（G-82-C）：不支持 = null + supports=false；
/// 已设 = 查询原文；清除 = 空串。<see cref="Applied"/> 为写入后**读回**值（可校对，非自述）。</summary>
public sealed class DefinitionQuerySetInfo
{
    public string LayerName { get; set; } = string.Empty;

    /// <summary>true = 已写入并读回成功。</summary>
    public bool Applied { get; set; }

    /// <summary>true = 图层类型支持定义查询（BasicFeatureLayer）。</summary>
    public bool SupportsDefinitionQuery { get; set; }

    /// <summary>写入后**读回**的查询原文；不支持层 → null；清除 → 空串。</summary>
    public string? DefinitionQuery { get; set; }

    /// <summary>写入面披露（"BasicFeatureLayer.SetDefinitionQuery"，就地修改、不可回退）。</summary>
    public string WriteTarget { get; set; } = "BasicFeatureLayer.SetDefinitionQuery";
}

/// <summary>D-043：图层顺序调整结果（move_layer）。
/// <see cref="RootLayerOrder"/> = 写入后读回的根容器图层顺序，供调用方与 <c>get_layers</c> 前后对照。</summary>
public sealed class LayerOrderInfo
{
    public string MapName { get; set; } = string.Empty;
    public string LayerName { get; set; } = string.Empty;

    /// <summary>生效的位置语义（TOP/BOTTOM/BEFORE/AFTER）。</summary>
    public string Position { get; set; } = string.Empty;

    /// <summary>目标索引（根容器内，0 基）。</summary>
    public int TargetIndex { get; set; }

    /// <summary>写入后读回的根容器图层名顺序（自上而下）。</summary>
    public IReadOnlyList<string> RootLayerOrder { get; set; } = Array.Empty<string>();

    /// <summary>写入面披露（"Map.MoveLayer(Layer,int)"，根容器语义）。</summary>
    public string WriteTarget { get; set; } = "Map.MoveLayer";
}

/// <summary>D-043：地图范围写入结果（set_map_extent）。
/// ★ 同源披露（O-D042-01 延伸）：写入面 = <see cref="WriteTarget"/>（Map.SetCustomFullExtent，custom full extent）；
/// <see cref="ReadBack"/>* = 写入后即时读回的 <c>Map.GetDefaultExtent</c>（即 <c>get_map_extent</c> 口径）；
/// <see cref="SameSource"/> = 读回值与写入值一致则为 true，否则 false（**如实披露，不伪造**）。</summary>
public sealed class MapExtentSetInfo
{
    public string MapName { get; set; } = string.Empty;

    /// <summary>写入的四至（调用方请求值）。</summary>
    public double XMin { get; set; }
    public double YMin { get; set; }
    public double XMax { get; set; }
    public double YMax { get; set; }

    /// <summary>写入所用的空间参考（名称；缺省为地图自身 SR）。</summary>
    public string? SpatialReferenceName { get; set; }

    /// <summary>写入面披露。</summary>
    public string WriteTarget { get; set; } = "Map.SetCustomFullExtent";

    // ---- 读回（= get_map_extent 同源口径 Map.GetDefaultExtent） ----
    public double? ReadBackXMin { get; set; }
    public double? ReadBackYMin { get; set; }
    public double? ReadBackXMax { get; set; }
    public double? ReadBackYMax { get; set; }
    public string? ReadBackSpatialReferenceName { get; set; }

    /// <summary>读回口径字段（固定 "default-extent"，与 get_map_extent 同）。</summary>
    public string ReadBackSource { get; set; } = "default-extent";

    /// <summary>true = 读回四至与写入四至一致（同源成立）；false = 不同源（口径错位，按工单 §1.3 处置）。</summary>
    public bool SameSource { get; set; }
}
