using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>图层服务。</summary>
public interface ILayerService
{
    /// <summary>
    /// 获取图层列表。
    /// </summary>
    /// <param name="mapName">地图名；为空表示当前活动地图。</param>
    /// <param name="flatten">
    /// 是否展开组合图层。默认 <c>true</c>：<c>Map.GetLayersAsFlattenedList()</c>，
    /// 嵌套子图层可见；<c>false</c>：<c>Map.Layers</c>，仅返回顶层并保留层级结构。
    /// 默认值由 Gate Keeper 决定 G-06 批准（属既有契约变更）。
    /// </param>
    /// <param name="ct">取消令牌。</param>
    Task<OperationResult<IReadOnlyList<LayerInfo>>> GetLayersAsync(
        string? mapName = null,
        bool flatten = true,
        CancellationToken ct = default);

    /// <summary>内部实现用：读取地图中当前 raster layer 数据源，供资格闸门在写入前复核。</summary>
    Task<OperationResult<IReadOnlyList<string>>> GetRasterSourcePathsAsync(
        string? mapName = null,
        CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<string>>.Ok(Array.Empty<string>()));

    Task<OperationResult<LayerInfo?>> FindLayerAsync(string mapName, string layerName, CancellationToken ct = default);

    Task<OperationResult<LayerInfo?>> GetLayerInfoAsync(string mapName, string layerName, CancellationToken ct = default);

    Task<OperationResult<bool>> SetLayerVisibilityAsync(string mapName, string layerName, bool visible, CancellationToken ct = default);

    Task<OperationResult<LayerInfo?>> AddLayerAsync(string mapName, string layerPathOrUri, CancellationToken ct = default);

    Task<OperationResult<bool>> RemoveLayerAsync(string mapName, string layerName, CancellationToken ct = default);

    /// <summary>D-042：定义查询读取。null = 图层类型不支持（如 GroupLayer）；空串 = 支持但未设置（G-82-C 可判别）。</summary>
    Task<OperationResult<DefinitionQueryInfo>> GetDefinitionQueryAsync(string? mapName, string layerName, CancellationToken ct = default);

    /// <summary>
    /// D-043：定义查询**写入**（就地修改、调用方自行备份，O-3 同类强制披露）。
    /// <paramref name="definitionQuery"/> 为空 = 清除；非 BasicFeatureLayer → INVALID_ARGUMENT
    /// （与 get 的 <c>supportsDefinitionQuery=false</c> 语义一致）；返回值为写入后读回原文。
    /// </summary>
    Task<OperationResult<DefinitionQuerySetInfo>> SetDefinitionQueryAsync(
        string? mapName, string layerName, string? definitionQuery, CancellationToken ct = default);

    /// <summary>
    /// D-043：调整图层在**根容器**（TOC 顶层）中的顺序；<paramref name="position"/> =
    /// TOP/BOTTOM/BEFORE/AFTER（大小写不敏感）；BEFORE/AFTER 必须有 <paramref name="referenceLayer"/>，
    /// 否则 INVALID_ARGUMENT；嵌套（组内）referenceLayer → INVALID_ARGUMENT（本批不做跨层级语义）。
    /// 重名 → AMBIGUOUS_LAYER_NAME（D-022/D-042 口径）；不存在 → LAYER_NOT_FOUND。
    /// </summary>
    Task<OperationResult<LayerOrderInfo>> MoveLayerAsync(
        string? mapName, string layerName, string? referenceLayer, string position, CancellationToken ct = default);

    /// <summary>
    /// D-046：读取图层符号（渲染器）形态。非要素图层 → <c>SupportsSymbology=false</c>；
    /// 复杂渲染器 → <c>IsSimpleRenderer=false</c> 且符号字段 null（**不静默降级**，G-82-C 可判别）。
    /// 色值形态 <c>#RRGGBB</c>（spike 确证 CIMRGBColor R/G/B）；点符号填充色经内层字符标记符号递归取
    /// （不可达 → null + 披露）。图层不存在 → LAYER_NOT_FOUND；重名 → AMBIGUOUS_LAYER_NAME。
    /// </summary>
    Task<OperationResult<LayerSymbologyInfo>> GetLayerSymbologyAsync(
        string? mapName, string layerName, CancellationToken ct = default);

    /// <summary>
    /// D-046：设置**简单符号**属性（填充色/描边色/点大小/线宽，至少一项）。
    /// 仅受理 <c>CIMSimpleRenderer</c>；复杂渲染器 → INVALID_ARGUMENT（不静默降级）。
    /// 色值入参接受 <c>#RRGGBB</c> 或 <c>RRGGBB</c>；点大小/线宽 &gt; 0。写后读回逐字。
    /// </summary>
    Task<OperationResult<SymbologySetInfo>> SetSimpleSymbologyAsync(
        string? mapName, string layerName, string? fillColor, string? outlineColor,
        double? pointSize, double? lineWidth, CancellationToken ct = default);

    /// <summary>
    /// D-046：读取图层标注配置（启用态 / labelClass 数 / 表达式 / 主要字体）。
    /// 标注未启用 → <c>Enabled=false</c> + labelClass 数如实（**非错误**）；非要素图层 → <c>SupportsLabels=false</c>。
    /// </summary>
    Task<OperationResult<LabelInfo>> GetLabelInfoAsync(
        string? mapName, string layerName, CancellationToken ct = default);

    /// <summary>
    /// D-046：标注开关。<c>enabled=true</c> 且图层**无 labelClass** → INVALID_ARGUMENT（如实拒绝，**不代建**）；
    /// <c>enabled=false</c> 恒受理。返回值为写后读回态。
    /// </summary>
    Task<OperationResult<LabelVisibilityInfo>> SetLabelVisibilityAsync(
        string? mapName, string layerName, bool enabled, CancellationToken ct = default);

    /// <summary>D-084 Session：设置受限标注表达式/字体/字号/放置/可见性。</summary>
    Task<OperationResult<object?>> SetLabelPropertiesAsync(
        string? mapName, string layerName, string? expression, string? fontFamily, double? fontSize,
        string? placement, bool? visible, CancellationToken ct = default)
        => Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "Label properties are not implemented by this host."));

    // ══════════════════════════ D-063 · A 段：图层管理 ══════════════════════════
    // 约定：新成员以**默认接口实现**提供（既有宿主与测试替身无需改动即可编译）；
    // 未覆写时返回 NOT_IMPLEMENTED —— 与本批"如实拒绝、不静默降级"一致。

    /// <summary>D-063：图层外观写入（透明度 0–100 / 最小最大显示比例，null = 不限）。写后读回。</summary>
    Task<OperationResult<LayerAppearanceInfo>> SetLayerAppearanceAsync(
        string? mapName, string layerName, double? transparency,
        double? minScale, double? maxScale, CancellationToken ct = default)
        => Task.FromResult(OperationResult<LayerAppearanceInfo>.Fail(
            ErrorCodes.NotImplemented, "Layer appearance is not implemented by this host."));

    /// <summary>D-063：创建组图层；<paramref name="layerNames"/> 非空时把指定图层移入组内。</summary>
    Task<OperationResult<GroupLayerInfo>> CreateGroupLayerAsync(
        string? mapName, string groupName, IReadOnlyList<string>? layerNames, CancellationToken ct = default)
        => Task.FromResult(OperationResult<GroupLayerInfo>.Fail(
            ErrorCodes.NotImplemented, "Group layer creation is not implemented by this host."));

    /// <summary>D-063：底图设置（**需网络**；离线 ⇒ 明确报错不挂起，带超时保护）。</summary>
    Task<OperationResult<BasemapInfo>> SetBasemapAsync(
        string? mapName, string basemap, CancellationToken ct = default)
        => Task.FromResult(OperationResult<BasemapInfo>.Fail(
            ErrorCodes.NotImplemented, "Basemap assignment is not implemented by this host."));

    /// <summary>D-063：全工程断源图层扫描（只读）。</summary>
    Task<OperationResult<BrokenLayersInfo>> GetBrokenLayersAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<BrokenLayersInfo>.Fail(
            ErrorCodes.NotImplemented, "Broken layer scan is not implemented by this host."));

    /// <summary>D-063：重指数据源（<paramref name="newWorkspacePath"/> 与 <paramref name="newDatasetName"/> 至少一项）。</summary>
    Task<OperationResult<RepairLayerInfo>> RepairLayerSourceAsync(
        string? mapName, string layerName, string? newWorkspacePath, string? newDatasetName,
        CancellationToken ct = default)
        => Task.FromResult(OperationResult<RepairLayerInfo>.Fail(
            ErrorCodes.NotImplemented, "Layer source repair is not implemented by this host."));

    // O-D066-05（D-066 阶段二）：AddJoinAsync/RemoveJoinAsync（D-063 关系类通道占位）已删除——
    // 两件工具升级为受控 GP 代理（IGeoprocessingService.RunWhitelistedAsync）后零调用方；
    // 连接证据通道由 GetJoinStateAsync（D-066）承担。
    /// <summary>
    /// D-066：连接状态回读（连接计数/字段前后照的证据通道）。
    /// 证据口径＝字段命名探针（点名字段 ⇒ 连接现身）；缺省体 = NotImplemented（宿主可覆写）。
    /// </summary>
    Task<OperationResult<JoinStateInfo>> GetJoinStateAsync(
        string? mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<JoinStateInfo>.Fail(
            ErrorCodes.NotImplemented, "Join state probe is not implemented by this host."));

    /// <summary>D-063：TOC 改名（写后读回新名）。</summary>
    Task<OperationResult<RenameLayerInfo>> RenameLayerAsync(
        string? mapName, string layerName, string newName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<RenameLayerInfo>.Fail(
            ErrorCodes.NotImplemented, "Layer rename is not implemented by this host."));

    /// <summary>D-063：复制图层（同图内副本）。</summary>
    Task<OperationResult<DuplicateLayerInfo>> DuplicateLayerAsync(
        string? mapName, string layerName, string? newName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<DuplicateLayerInfo>.Fail(
            ErrorCodes.NotImplemented, "Layer duplication is not implemented by this host."));

    // ══════════════════════════ D-063 · B 段：渲染进阶 ══════════════════════════

    /// <summary>D-063：设置渲染器（single/unique/graduated 三模式）。<paramref name="field"/> 非法 ⇒ INVALID_ARGUMENT。</summary>
    Task<OperationResult<LayerRendererInfo>> SetLayerRendererAsync(
        string? mapName, string layerName, string mode, string? field,
        int? classCount, string? colorRamp, CancellationToken ct = default)
        => Task.FromResult(OperationResult<LayerRendererInfo>.Fail(
            ErrorCodes.NotImplemented, "Renderer assignment is not implemented by this host."));

    /// <summary>D-063：工程可用色带清单（只读）。</summary>
    Task<OperationResult<ColorRampsInfo>> ListColorRampsAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<ColorRampsInfo>.Fail(
            ErrorCodes.NotImplemented, "Color ramp enumeration is not implemented by this host."));

    /// <summary>D-063：从 .lyrx 应用符号到目标图层（**只替换 renderer**，不替换数据连接）。</summary>
    Task<OperationResult<LayerFileInfo>> ApplySymbologyFromLayerAsync(
        string? mapName, string layerName, string layerFilePath, CancellationToken ct = default)
        => Task.FromResult(OperationResult<LayerFileInfo>.Fail(
            ErrorCodes.NotImplemented, "Symbology application is not implemented by this host."));

    /// <summary>D-063：保存图层为 .lyrx（**写文件 ⇒ 输出路径守卫**）。</summary>
    Task<OperationResult<LayerFileInfo>> SaveLayerFileAsync(
        string? mapName, string layerName, string outputPath, CancellationToken ct = default)
        => Task.FromResult(OperationResult<LayerFileInfo>.Fail(
            ErrorCodes.NotImplemented, "Layer file saving is not implemented by this host."));
}
