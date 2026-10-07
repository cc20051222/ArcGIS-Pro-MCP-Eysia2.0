using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>地图服务。</summary>
public interface IMapService
{
    Task<OperationResult<MapInfo?>> GetCurrentMapAsync(CancellationToken ct = default);

    Task<OperationResult<IReadOnlyList<MapInfo>>> GetMapsAsync(CancellationToken ct = default);

    /// <summary>D-042：地图范围（口径 = Map.GetDefaultExtent 工程态，不依赖活动视图）。</summary>
    Task<OperationResult<MapExtentInfo>> GetMapExtentAsync(string? mapName, CancellationToken ct = default);

    /// <summary>
    /// D-043：地图范围**写入**（就地修改、调用方自行备份）。
    /// 写入面 = <c>Map.SetCustomFullExtent</c>（SDK 无 SetDefaultExtent，见 sdk-spike.md）；
    /// 返回 <see cref="MapExtentSetInfo"/> 含写入后立即以 <c>Map.GetDefaultExtent</c>（= get_map_extent
    /// 同源口径）读回的四至与 <c>SameSource</c> 判定——**如实披露，不伪造 stateProof**。
    /// <paramref name="spatialReference"/> 可为 WKID（整数）或名称/WKT 字符串；省略 = 沿用地图自身 SR。
    /// </summary>
    Task<OperationResult<MapExtentSetInfo>> SetMapExtentAsync(
        string? mapName, double xMin, double yMin, double xMax, double yMax, string? spatialReference,
        CancellationToken ct = default);

    /// <summary>
    /// D-061：在工程内新建地图（2D Map），返回其信息。<paramref name="basemap"/> / <paramref name="spatialReference"/>
    /// 可为 null（省略 ⇒ SDK 默认；basemap 未提供时地图无底图）。
    /// </summary>
    /// <remarks>
    /// 以**默认接口实现**提供，既有宿主实现（含测试替身）无需改动即可编译；未覆写时返回 NOT_IMPLEMENTED。
    /// 注：本宿主方法负责把新地图加入当前工程；重名判定由工具层前置（见 CreateMapTool）。
    /// </remarks>
    Task<OperationResult<MapInfo>> CreateMapAsync(
        string name, string? basemap, string? spatialReference, CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapInfo>.Fail(
            ErrorCodes.NotImplemented, "Map creation is not implemented by this host."));

    // ══════════════════════════ D-063 · C 段：视图与书签 ＋ 回图 ══════════════════════════

    /// <summary>D-063：读取地图视图相机态（依赖活动视图；无活动视图 ⇒ HasActiveView=false + 明确说明）。</summary>
    Task<OperationResult<MapViewInfo>> GetMapViewAsync(string? mapName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapViewInfo>.Fail(
            ErrorCodes.NotImplemented, "Map view read is not implemented by this host."));

    /// <summary>D-063：移动/设置视图（extent 或 center+scale 或 rotation，三选一）。写后读回。</summary>
    Task<OperationResult<MapViewSetInfo>> SetMapViewAsync(
        string? mapName, double? xMin, double? yMin, double? xMax, double? yMax,
        double? centerX, double? centerY, double? scale, double? rotation,
        CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapViewSetInfo>.Fail(
            ErrorCodes.NotImplemented, "Map view write is not implemented by this host."));

    /// <summary>
    /// D-063 ★ 回图：渲染地图视图为 PNG 并回传（不落盘模式 = 零文件副作用）。
    /// 约束：96 DPI、长边 ≤<paramref name="maxEdge"/> px、PNG ≤1 MB（降采样重试，仍超限 ⇒ 明确报错）。
    /// </summary>
    Task<OperationResult<MapViewImageResult>> ExportMapViewAsync(
        string? mapName, int? width, int? height, int? resolutionDpi, int? maxEdge,
        string? outputPath, CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapViewImageResult>.Fail(
            ErrorCodes.NotImplemented, "Map view export is not implemented by this host."));

    /// <summary>D-063：书签清单（只读）。</summary>
    Task<OperationResult<BookmarksInfo>> ListBookmarksAsync(string? mapName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<BookmarksInfo>.Fail(
            ErrorCodes.NotImplemented, "Bookmark listing is not implemented by this host."));

    /// <summary>D-063：以当前视图创建书签。</summary>
    Task<OperationResult<BookmarkOpInfo>> CreateBookmarkAsync(
        string? mapName, string name, CancellationToken ct = default)
        => Task.FromResult(OperationResult<BookmarkOpInfo>.Fail(
            ErrorCodes.NotImplemented, "Bookmark creation is not implemented by this host."));

    /// <summary>D-063：应用书签（视图跳转到书签相机）。</summary>
    Task<OperationResult<BookmarkOpInfo>> ApplyBookmarkAsync(
        string? mapName, string name, CancellationToken ct = default)
        => Task.FromResult(OperationResult<BookmarkOpInfo>.Fail(
            ErrorCodes.NotImplemented, "Bookmark application is not implemented by this host."));

    /// <summary>D-063：删除书签（**仅视图资产**，无数据破坏 ⇒ 不入 destructive 名录，但 Description 披露）。</summary>
    Task<OperationResult<BookmarkOpInfo>> DeleteBookmarkAsync(
        string? mapName, string name, CancellationToken ct = default)
        => Task.FromResult(OperationResult<BookmarkOpInfo>.Fail(
            ErrorCodes.NotImplemented, "Bookmark deletion is not implemented by this host."));

    // ══════════════════════════ D-064 · B 段：工程/地图增强 ══════════════════════════

    /// <summary>
    /// D-064：删除工程内地图（**破坏性** ⇒ 工具层 `confirm` 缺省拒；本方法只做执行，不做 confirm 判定）。
    /// 返回删除前后地图名清单（供工具层对照证明）。
    /// </summary>
    /// <remarks>默认实现返回 NOT_IMPLEMENTED（保既有宿主实现零改动编译）。</remarks>
    Task<OperationResult<RemoveMapResult>> RemoveMapAsync(string mapName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<RemoveMapResult>.Fail(
            ErrorCodes.NotImplemented, "Map removal is not implemented by this host."));

    /// <summary>
    /// D-064：激活/打开地图视图（UI 联动）。无活动视图时创建并激活；已存在则仅激活并置前。
    /// </summary>
    /// <remarks>默认实现返回 NOT_IMPLEMENTED（保既有宿主实现零改动编译）。</remarks>
    Task<OperationResult<ActivateMapResult>> ActivateMapAsync(string mapName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<ActivateMapResult>.Fail(
            ErrorCodes.NotImplemented, "Map activation is not implemented by this host."));

    /// <summary>
    /// D-064：地图属性（改名 / 换空间参考）。二者至少提供一项；写后读回。
    /// </summary>
    /// <remarks>默认实现返回 NOT_IMPLEMENTED（保既有宿主实现零改动编译）。</remarks>
    Task<OperationResult<SetMapPropertiesResult>> SetMapPropertiesAsync(
        string mapName, string? newName, string? spatialReference, CancellationToken ct = default)
        => Task.FromResult(OperationResult<SetMapPropertiesResult>.Fail(
            ErrorCodes.NotImplemented, "Map property update is not implemented by this host."));
}
