namespace ArcGISProMCP.Core.Models;

/// <summary>D-046：图层符号（渲染器）读取结果（get_layer_symbology）。
/// 非要素图层（如 GroupLayer）→ <see cref="SupportsSymbology"/>=false + 其余字段 null（G-82-C 可判别形态）。
/// 复杂渲染器（唯一值/分级/热力图…）→ <see cref="IsSimpleRenderer"/>=false，符号字段 null（不静默降级）。</summary>
public sealed class LayerSymbologyInfo
{
    public string LayerName { get; set; } = string.Empty;

    /// <summary>是否支持符号读取（要素图层 true；组图层等 false）。</summary>
    public bool SupportsSymbology { get; set; }

    /// <summary>渲染器运行时类型名（如 SimpleRenderer / UniqueValueRenderer / ClassBreaksRenderer）；不支持时 null。</summary>
    public string? RendererType { get; set; }

    /// <summary>是否 SimpleRenderer（本批写面仅受理此形态）。</summary>
    public bool IsSimpleRenderer { get; set; }

    /// <summary>符号大类：Point / Line / Polygon（不可判别时 null）。</summary>
    public string? SymbolKind { get; set; }

    /// <summary>填充色 #RRGGBB（点/面）；不可达或缺失时 null。</summary>
    public string? FillColor { get; set; }

    /// <summary>描边色 #RRGGBB（线/面/点外框）；不可达或缺失时 null。</summary>
    public string? OutlineColor { get; set; }

    /// <summary>点大小（点符号）；非点符号 null。</summary>
    public double? PointSize { get; set; }

    /// <summary>线宽（线/面描边）；不可达时 null。</summary>
    public double? LineWidth { get; set; }

    /// <summary>形态披露（色值格式 / 收窄项）。</summary>
    public string Notes { get; set; } =
        "colors as #RRGGBB (alpha not included; CIMRGBColor R/G/B 0-255); " +
        "point fill color resolved via inner character-marker symbol (null if unreachable)";

    // ══════════════ D-063 增强：复杂渲染器读出（唯一值 / 分级 / 标注摘要）══════════════
    // 既有语义保持不变（G-82-C 三态可判别）；本段把"复杂渲染器 ⇒ 符号字段 null"升级为
    // **可读出的结构化摘要**（不静默降级仍然成立：不可读出的项保持 null）。

    /// <summary>唯一值渲染器的分类字段集合（非唯一值渲染器 ⇒ null）。</summary>
    public IReadOnlyList<string>? UniqueValueFields { get; set; }

    /// <summary>唯一值类目数（非唯一值渲染器 ⇒ null）。</summary>
    public int? UniqueValueClassCount { get; set; }

    /// <summary>唯一值类目摘要（值 + 色值 #RRGGBB）。</summary>
    public IReadOnlyList<UniqueValueClassSummary>? UniqueValueClasses { get; set; }

    /// <summary>分级渲染器的分级字段（非分级 ⇒ null）。</summary>
    public string? ClassBreakField { get; set; }

    /// <summary>分级断点数（非分级 ⇒ null）。</summary>
    public int? ClassBreakCount { get; set; }

    /// <summary>分级断点摘要（上下界 + 色值）。</summary>
    public IReadOnlyList<ClassBreakSummary>? ClassBreaks { get; set; }

    /// <summary>色带名（分级渲染器；不可得 ⇒ null）。</summary>
    public string? ColorRampName { get; set; }

    /// <summary>标注摘要（启用态 / 类数 / 首个表达式）—— 与 get_label_info 同源，此处随符号一并读出。</summary>
    public LabelDigest? Labels { get; set; }
}

/// <summary>D-063：唯一值类目摘要（值 + 色值）。</summary>
public sealed class UniqueValueClassSummary
{
    /// <summary>类目显示串（多字段组合以 " | " 连接）。</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>该类目符号色 #RRGGBB（不可达 ⇒ null）。</summary>
    public string? Color { get; set; }

    public string? Label { get; set; }
}

/// <summary>D-063：分级断点摘要。</summary>
public sealed class ClassBreakSummary
{
    public double? Lower { get; set; }
    public double? Upper { get; set; }

    /// <summary>该级符号色 #RRGGBB（不可达 ⇒ null）。</summary>
    public string? Color { get; set; }

    public string? Label { get; set; }
}

/// <summary>D-063：标注摘要（随 get_layer_symbology 一并读出，避免二次调用）。</summary>
public sealed class LabelDigest
{
    public bool Enabled { get; set; }
    public int LabelClassCount { get; set; }

    /// <summary>首个标注类的表达式（无 ⇒ null）。</summary>
    public string? FirstExpression { get; set; }
}

/// <summary>D-046：简单符号写入结果（set_simple_symbology）。字段为**写后读回**值（逐字对照用）。</summary>
public sealed class SymbologySetInfo
{
    public string LayerName { get; set; } = string.Empty;

    public string RendererType { get; set; } = "SimpleRenderer";

    public string? FillColor { get; set; }

    public string? OutlineColor { get; set; }

    public double? PointSize { get; set; }

    public double? LineWidth { get; set; }

    /// <summary>写入面披露（就地改渲染器；可经原值复原）。</summary>
    public string WriteTarget { get; set; } = "FeatureLayer.SetRenderer(CIMRenderer)";
}

/// <summary>D-046：图层标注配置（get_label_info）。
/// 标注不可用（图层类型不支持）→ <see cref="SupportsLabels"/>=false；
/// 支持但未启用 → <see cref="Enabled"/>=false + <see cref="LabelClassCount"/> 如实（**非错误**，G-82-C）。</summary>
public sealed class LabelInfo
{
    public string LayerName { get; set; } = string.Empty;

    /// <summary>是否支持标注读取（要素图层 true）。</summary>
    public bool SupportsLabels { get; set; }

    /// <summary>标注是否正在绘制（FeatureLayer.IsLabelVisible）。</summary>
    public bool Enabled { get; set; }

    /// <summary>labelClass 数量（只读集合计数）。</summary>
    public int LabelClassCount { get; set; }

    /// <summary>首个 labelClass 的标注表达式（无 labelClass 时 null）。</summary>
    public string? Expression { get; set; }

    /// <summary>表达式引擎名（如 Arcade / Python / SQL）；不可判别时 null。</summary>
    public string? ExpressionEngine { get; set; }

    /// <summary>主要字体族（首个 labelClass 的 TextSymbol.FontFamilyName）；不可达时 null。</summary>
    public string? FontFamily { get; set; }

    /// <summary>字号（TextSymbol.Height，点）；不可达时 null。</summary>
    public double? FontSize { get; set; }

    /// <summary>形态披露。</summary>
    public string Notes { get; set; } =
        "expression/font read from the first label class; labels-not-enabled is not an error " +
        "(Enabled=false with LabelClassCount reported as-is)";
}

/// <summary>D-046：标注开关写入结果（set_label_visibility）。<see cref="Enabled"/> 为**写后读回**值。</summary>
public sealed class LabelVisibilityInfo
{
    public string LayerName { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    /// <summary>写入时所依据的 labelClass 数（0 且请求开启 → 已按工单拒绝，不代建）。</summary>
    public int LabelClassCount { get; set; }

    /// <summary>写入面披露。</summary>
    public string WriteTarget { get; set; } = "FeatureLayer.SetLabelVisibility(bool)";
}
