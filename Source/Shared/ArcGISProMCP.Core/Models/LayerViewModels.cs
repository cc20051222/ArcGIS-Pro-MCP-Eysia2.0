using System.Collections.Generic;

namespace ArcGISProMCP.Core.Models;

// ════════════════════════════════════════════════════════════════
// D-063 功能完善第三批：图层管理 ＋ 渲染进阶 ＋ 视图书签 ＋ 回图
// 纯数据契约（Rule 5：Core 不引用 ArcGIS Pro SDK）。
// ════════════════════════════════════════════════════════════════

/// <summary>图层外观（透明度 / 显示比例范围）—— CIM 写回后的读回态。</summary>
public sealed class LayerAppearanceInfo
{
    public string LayerName { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;

    /// <summary>透明度百分比 0–100（SDK <c>Layer.SetTransparency</c> 同口径）。</summary>
    public double Transparency { get; set; }

    /// <summary>最小显示比例分母；0 或 null = 不限（ShowLayerAtAllScales）。</summary>
    public double? MinScale { get; set; }

    public double? MaxScale { get; set; }

    public bool ShowLayerAtAllScales { get; set; }

    /// <summary>写入面披露（CIM 写回 / 就地修改 / 可前照复原）。</summary>
    public string WriteSurface { get; set; } =
        "in-place on the live layer (CIM write-back); caller should snapshot prior values to restore";
}

/// <summary>组图层创建结果。</summary>
public sealed class GroupLayerInfo
{
    public string GroupName { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public int Position { get; set; }

    /// <summary>建组后移入的既有图层数（0 = 建空组）。</summary>
    public int MovedLayerCount { get; set; }

    public IReadOnlyList<string> MovedLayers { get; set; } = new List<string>();
}

/// <summary>底图设置结果（状态类；离线明确报错不挂起）。</summary>
public sealed class BasemapInfo
{
    public string MapName { get; set; } = string.Empty;

    /// <summary>请求底图名。</summary>
    public string RequestedBasemap { get; set; } = string.Empty;

    /// <summary>是否成功应用。</summary>
    public bool Applied { get; set; }

    /// <summary>可用底图清单（离线时可能为空）。</summary>
    public IReadOnlyList<string> AvailableBasemaps { get; set; } = new List<string>();

    /// <summary>离线/失败原因（Applied=false 时必填）。</summary>
    public string? Reason { get; set; }

    /// <summary>★ 超时保护披露：底图枚举受网络影响，本工具带有限时保护，超时 ⇒ 明确报错而非挂起。</summary>
    public string OfflineBehavior { get; set; } =
        "basemap enumeration requires portal/network access; offline ⇒ explicit error (no hang, timeout-guarded)";
}

/// <summary>断源图层条目。</summary>
public sealed class BrokenLayerInfo
{
    public string LayerName { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public string LayerType { get; set; } = string.Empty;

    /// <summary>数据源路径（不可得时 null）。</summary>
    public string? DataSourcePath { get; set; }

    /// <summary>SDK 断源判定（ConnectionStatus / IsBroken 同源）。</summary>
    public bool IsBroken { get; set; }
}

public sealed class BrokenLayersInfo
{
    public IReadOnlyList<BrokenLayerInfo> Items { get; set; } = new List<BrokenLayerInfo>();
    public int TotalCount { get; set; }

    /// <summary>扫描范围（当前工程全部地图）。</summary>
    public string ScopeNote { get; set; } = "scans all maps in the current project";
}

/// <summary>重指数据源结果（前后照：旧路径 → 新路径）。</summary>
public sealed class RepairLayerInfo
{
    public string LayerName { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public string? OldPath { get; set; }
    public string? NewPath { get; set; }
    public bool Repaired { get; set; }

    /// <summary>修复方式（workspacePath 替换 / dataset 重指）。</summary>
    public string? Method { get; set; }

    public string WriteSurface { get; set; } =
        "in-place data-source rebinding (no data modified); caller should snapshot prior path to restore";
}

/// <summary>连接（join）状态与操作结果 —— ★ SDK 可达面披露。</summary>
public sealed class JoinInfo
{
    public string LayerName { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;

    /// <summary>操作后是否处于连接态。</summary>
    public bool Joined { get; set; }

    /// <summary>操作类型：add / remove / none。</summary>
    public string Operation { get; set; } = "none";

    /// <summary>关系类名（add 时的入参）。</summary>
    public string? RelationshipClass { get; set; }

    /// <summary>keepAll 语义披露（外连接保留全部目标行）。</summary>
    public bool? KeepAll { get; set; }

    /// <summary>★ 可达性披露（实证：SDK JoinDescription 仅 RelationshipClass 构造）。</summary>
    public string ReachabilityNote { get; set; } =
        "ArcGIS Pro SDK exposes joins via RelationshipClass-based JoinDescription only; " +
        "attribute (common-field) joins are NOT reachable through the managed SDK (GP AddJoin is out of the v1 whitelist).";
}

/// <summary>
/// D-066：图层连接状态回读（连接计数/字段前后照的<b>证据通道</b>）。
/// 证据形态＝字段命名探针（GetFieldDescriptions）：GP AddJoin 就地生效后图层字段以
/// 「表名.字段名」形式现身（D-066 spike 实测）；CIM join 内省在 SDK 引用面未暴露，
/// 故以 <see cref="JoinedDetected"/>（点名字段存在）作为连接态代理指标并如实披露证据口径。
/// </summary>
public sealed class JoinStateInfo
{
    public string LayerName { get; set; } = string.Empty;

    public string MapName { get; set; } = string.Empty;

    /// <summary>图层当前字段总数（含连接字段）。</summary>
    public int FieldCount { get; set; }

    /// <summary>字段名清单（截断防护：至多 400 条）。</summary>
    public IReadOnlyList<string> Fields { get; set; } = Array.Empty<string>();

    /// <summary>含「.」的字段名（连接现身形态；至多 200 条）。</summary>
    public IReadOnlyList<string> DottedFields { get; set; } = Array.Empty<string>();

    /// <summary>连接态代理指标：点名字段存在 ⇒ 判定处于连接态（证据口径见类型说明）。</summary>
    public bool JoinedDetected { get; set; }

    /// <summary>证据口径披露（不伪造内省语义）。</summary>
    public string Evidence { get; set; } =
        "field-naming probe via GetFieldDescriptions (dotted names = joined fields); " +
        "CIM join introspection is not exposed in the SDK reference surface.";
}

/// <summary>TOC 改名结果。</summary>
public sealed class RenameLayerInfo
{
    public string OldName { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
}

/// <summary>图层复制结果。</summary>
public sealed class DuplicateLayerInfo
{
    public string SourceLayerName { get; set; } = string.Empty;
    public string NewLayerName { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
}

/// <summary>渲染器设置结果（写后读回）。</summary>
public sealed class LayerRendererInfo
{
    public string LayerName { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;

    /// <summary>模式：single / unique / graduated。</summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>读回的渲染器运行时类型（如 CIMUniqueValueRenderer）。</summary>
    public string? RendererType { get; set; }

    /// <summary>分类字段（unique/graduated 模式）。</summary>
    public string? Field { get; set; }

    /// <summary>分级数（graduated 模式）。</summary>
    public int? ClassCount { get; set; }

    /// <summary>色带名（graduated 模式）。</summary>
    public string? ColorRamp { get; set; }

    public bool Applied { get; set; }

    public string WriteSurface { get; set; } =
        "in-place renderer replacement (CIM write-back); prior renderer is not auto-saved — snapshot via save_layer_file if restore is needed";
}

/// <summary>色带条目。</summary>
public sealed class ColorRampInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public sealed class ColorRampsInfo
{
    public IReadOnlyList<ColorRampInfo> Items { get; set; } = new List<ColorRampInfo>();
    public int TotalCount { get; set; }

    /// <summary>来源样式（工程样式项）。</summary>
    public string? StyleName { get; set; }
}

/// <summary>.lyrx 保存 / 应用结果（写文件 ⇒ 输出路径守卫）。</summary>
public sealed class LayerFileInfo
{
    public string LayerName { get; set; } = string.Empty;

    /// <summary>文件路径（绝对路径）。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>操作：save / apply。</summary>
    public string Operation { get; set; } = string.Empty;

    public long? Bytes { get; set; }

    public bool Ok { get; set; }

    public string? Reason { get; set; }
}

/// <summary>地图视图（相机）快照。</summary>
public sealed class MapViewInfo
{
    public string MapName { get; set; } = string.Empty;

    /// <summary>相机中心 X（地图坐标）。</summary>
    public double? X { get; set; }

    public double? Y { get; set; }

    /// <summary>比例尺分母。</summary>
    public double? Scale { get; set; }

    public double? Heading { get; set; }
    public double? Pitch { get; set; }
    public double? Roll { get; set; }

    /// <summary>当前可见范围四至（活动视图）。</summary>
    public double? XMin { get; set; }
    public double? YMin { get; set; }
    public double? XMax { get; set; }
    public double? YMax { get; set; }

    /// <summary>无活动视图时为 false（口径披露：相机依赖活动视图，非工程态）。</summary>
    public bool HasActiveView { get; set; }

    public string ViewSourceNote { get; set; } =
        "camera/extent reflect the ACTIVE map view (not project state); opening the map changes availability";
}

/// <summary>视图移动结果（写后读回）。</summary>
public sealed class MapViewSetInfo
{
    public string MapName { get; set; } = string.Empty;
    public bool Applied { get; set; }

    /// <summary>请求摘要（extent / center+scale / rotation）。</summary>
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>写后读回的相机态。</summary>
    public MapViewInfo? CameraAfter { get; set; }

    /// <summary>视图移动属视图态，不改数据 ⇒ 可经前照相机复原。</summary>
    public string WriteSurface { get; set; } =
        "view/navigation state only (no data change); prior camera can be restored via set_map_view";
}

/// <summary>
/// ★ 回图结果（D-063 新能力面）：地图视图渲染为 PNG 并回传 base64（MCP image content）。
/// </summary>
public sealed class MapViewImageResult
{
    public string MapName { get; set; } = string.Empty;

    /// <summary>MIME（固定 <c>image/png</c>）。</summary>
    public string MimeType { get; set; } = "image/png";

    /// <summary>PNG 字节的 base64（标准 base64，无 data: 前缀）。</summary>
    public string DataBase64 { get; set; } = string.Empty;

    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>编码前字节数（自检 ≤1 MB）。</summary>
    public int Bytes { get; set; }

    public int ResolutionDpi { get; set; } = 96;

    /// <summary>落盘路径；null = **不落盘模式**（零文件副作用）。</summary>
    public string? OutputPath { get; set; }

    /// <summary>是否经受控临时文件中转（实现披露，绝不静默）。</summary>
    public bool UsedTransientFile { get; set; }

    /// <summary>中转文件是否已删除（不落盘模式应为 true）。</summary>
    public bool TransientFileDeleted { get; set; }

    public string ConstraintsNote { get; set; } =
        "96 DPI default; long edge capped at 1200 px; PNG capped at 1 MB (downscale retry, else explicit error)";
}

/// <summary>书签条目。</summary>
public sealed class BookmarkInfo
{
    public string Name { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
}

public sealed class BookmarksInfo
{
    public IReadOnlyList<BookmarkInfo> Items { get; set; } = new List<BookmarkInfo>();
    public int TotalCount { get; set; }
}

/// <summary>书签操作结果（create / apply / delete）。</summary>
public sealed class BookmarkOpInfo
{
    public string Name { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;

    /// <summary>create / apply / delete。</summary>
    public string Operation { get; set; } = string.Empty;

    public bool Ok { get; set; }

    /// <summary>操作后书签总数（供往返断言）。</summary>
    public int BookmarkCountAfter { get; set; }

    /// <summary>delete 语义披露：仅视图资产，无数据破坏 ⇒ 不入 destructive 名录。</summary>
    public string DeleteScopeNote { get; set; } =
        "bookmarks are view assets only; deleting removes no data (not in the destructive catalogue, disclosed in description)";
}
