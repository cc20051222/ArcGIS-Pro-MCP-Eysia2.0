using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>地图服务实现。所有 SDK 访问通过 QueuedTask.Run（Rule 4 MCT）。</summary>
/// <remarks>
/// Phase 8.1 修复：
/// C-06 —— <see cref="GetCurrentMapAsync"/> 无活动视图时返回 <c>NO_ACTIVE_VIEW</c>，
///         **不再回退工程首个地图**（历史上曾以"保证自测稳定返回非空"掩盖失败）。
/// C-12 —— 种类由真实 <c>MapType</c> 映射，禁止硬编码 <c>Map</c>。
/// C-13 —— <see cref="GetMapsAsync"/> 改用 <c>Project.Current.GetItems&lt;MapProjectItem&gt;()</c>
///         直接读取 <c>MapType</c>/<c>Path</c>/<c>Name</c>，**既不创建也不加载**地图对象，
///         消除原 <c>MapFactory.CreateMapFromItem</c> 在"只读取"路径上的副作用。
/// 依据 ADR：<c>Docs/phases/PHASE_08/06_SDK_SPIKE_ADR.md</c>（S1 / S2 / S3）。
/// </remarks>
public sealed partial class MapService : IMapService
{
    public Task<OperationResult<MapInfo?>> GetCurrentMapAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<MapInfo?>>(
            () =>
            {
                var map = MapView.Active?.Map;
                if (map is null)
                {
                    // C-06：不得回退到工程首个地图来"保证返回非空"。
                    return OperationResult<MapInfo?>.Fail(
                        ErrorCodes.NoActiveView,
                        "No active map view is available.");
                }

                var items = SafeGetMapProjectItems();
                return OperationResult<MapInfo?>.Ok(ToInfo(map, items, isActive: true));
            },
            TaskCreationOptions.None);

    public Task<OperationResult<IReadOnlyList<MapInfo>>> GetMapsAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<MapInfo>>>(
            () =>
            {
                var items = SafeGetMapProjectItems();

                // S3：活动地图以 MapView.Active 为准；与项目项按 ADR 方案 A（URI ↔ Path）关联。
                // 该相等性无官方文档保证（ADR S3 标注 NOT VERIFIED）。匹配不到时全部 IsActive=false，
                // 由 get_current_map 作为权威来源，**绝不用"列表首项"冒充**。
                var activeUri = MapView.Active?.Map?.URI?.ToString();

                var infos = items
                    .Select(item =>
                    {
                        var path = item.Path ?? string.Empty;
                        var mapTypeText = item.MapType.ToString() ?? string.Empty;
                        return new MapInfo
                        {
                            Name = item.Name ?? string.Empty,
                            Uri = path,
                            Kind = KindFrom(mapTypeText),
                            MapType = mapTypeText,
                            Id = path,
                            IsActive = !string.IsNullOrEmpty(activeUri)
                                       && string.Equals(activeUri, path, StringComparison.OrdinalIgnoreCase)
                        };
                    })
                    .ToList();

                return OperationResult<IReadOnlyList<MapInfo>>.Ok(infos);
            },
            TaskCreationOptions.None);

    private static List<MapProjectItem> SafeGetMapProjectItems()
    {
        var items = Project.Current?.GetItems<MapProjectItem>();
        return items is null ? new List<MapProjectItem>() : items.ToList();
    }

    private static MapInfo ToInfo(Map map, IReadOnlyList<MapProjectItem> items, bool isActive)
    {
        var uri = map.URI?.ToString() ?? string.Empty;

        MapProjectItem? matched = null;
        if (!string.IsNullOrEmpty(uri))
        {
            matched = items.FirstOrDefault(
                i => string.Equals(i.Path ?? string.Empty, uri, StringComparison.OrdinalIgnoreCase));
        }

        var mapTypeText = matched is not null
            ? matched.MapType.ToString() ?? string.Empty
            : map.MapType.ToString() ?? string.Empty;

        return new MapInfo
        {
            Name = map.Name ?? string.Empty,
            Uri = uri,
            Kind = KindFrom(mapTypeText),
            MapType = mapTypeText,
            // 关联成功用 Path，否则退回 URI（可能为空）；ADR S3 标注 NOT VERIFIED。
            Id = matched?.Path ?? uri,
            IsActive = isActive
        };
    }

    /// <summary>
    /// 由真实 MapType 文本映射种类：取值含 <c>Map</c> / <c>Scene</c> / <c>LinkChart</c>（Pro 3.3+）。
    /// 未知或空一律 <c>Unknown</c>，**禁止**硬编码 <c>Map</c>（C-12）。
    /// </summary>
    private static string KindFrom(string? mapTypeText)
        => string.IsNullOrWhiteSpace(mapTypeText) ? "Unknown" : mapTypeText;
    /// <summary>D-042：地图范围（口径 = Map.GetDefaultExtent 工程态，不依赖活动视图——SDK spike 确证在场）。</summary>
    public Task<OperationResult<MapExtentInfo>> GetMapExtentAsync(string? mapName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<MapExtentInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<MapExtentInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }
                var map = resolved.Map!;
                var env = map.GetDefaultExtent();
                var info = new MapExtentInfo
                {
                    MapName = map.Name ?? string.Empty,
                    SpatialReferenceName = map.SpatialReference?.Name,
                    ExtentSource = "default-extent",
                };
                if (env is not null)
                {
                    info.XMin = env.XMin;
                    info.YMin = env.YMin;
                    info.XMax = env.XMax;
                    info.YMax = env.YMax;
                }
                return OperationResult<MapExtentInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    public Task<OperationResult<MapExtentSetInfo>> SetMapExtentAsync(
        string? mapName, double xMin, double yMin, double xMax, double yMax, string? spatialReference,
        CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<MapExtentSetInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<MapExtentSetInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var map = resolved.Map!;
                if (!(xMin < xMax) || !(yMin < yMax) ||
                    double.IsNaN(xMin) || double.IsNaN(yMin) || double.IsNaN(xMax) || double.IsNaN(yMax))
                {
                    return OperationResult<MapExtentSetInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        "Extent is invalid: finite numbers with xMin < xMax and yMin < yMax are required.");
                }

                ArcGIS.Core.Geometry.SpatialReference? sr = null;
                if (!string.IsNullOrWhiteSpace(spatialReference))
                {
                    try
                    {
                        sr = int.TryParse(
                                spatialReference,
                                System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var wkid)
                            ? new ArcGIS.Core.Geometry.SpatialReferenceBuilder(wkid).ToSpatialReference()
                            : new ArcGIS.Core.Geometry.SpatialReferenceBuilder(spatialReference).ToSpatialReference();
                    }
                    catch (Exception ex)
                    {
                        return OperationResult<MapExtentSetInfo>.Fail(
                            ErrorCodes.InvalidArgument,
                            "spatialReference is not resolvable (WKID integer or recognized name/WKT expected): " + ex.Message);
                    }
                }

                sr ??= map.SpatialReference;
                var envelope = new ArcGIS.Core.Geometry.EnvelopeBuilderEx(xMin, yMin, xMax, yMax, sr).ToGeometry();

                // 唯一公开的工程态写入面（SDK 无 SetDefaultExtent；不写活动视图 Camera，规避 MG3′）。
                map.SetCustomFullExtent(envelope);

                var info = new MapExtentSetInfo
                {
                    MapName = map.Name ?? string.Empty,
                    XMin = xMin,
                    YMin = yMin,
                    XMax = xMax,
                    YMax = yMax,
                    SpatialReferenceName = sr?.Name,
                };

                // 读回 = get_map_extent 同源口径（Map.GetDefaultExtent）——如实披露，不伪造。
                var read = map.GetDefaultExtent();
                if (read is not null)
                {
                    info.ReadBackXMin = read.XMin;
                    info.ReadBackYMin = read.YMin;
                    info.ReadBackXMax = read.XMax;
                    info.ReadBackYMax = read.YMax;
                    info.SameSource = Nearly(read.XMin, xMin) && Nearly(read.YMin, yMin)
                                      && Nearly(read.XMax, xMax) && Nearly(read.YMax, yMax);
                }

                return OperationResult<MapExtentSetInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    /// <summary>
    /// D-061：在工程内新建 **2D 地图**（<c>MapType.Map</c>）并返回其信息。
    /// </summary>
    /// <remarks>
    /// **basemap 暂不支持（如实披露）**：本批宿主未实现底图解析（不注入、不静默忽略）——
    /// 调用方传入非空 <paramref name="basemap"/> 时**显式返回 NOT_IMPLEMENTED**，
    /// 而不是"接受参数却什么也不做"。空间参考解析与 <see cref="SetMapExtentAsync"/> 同源
    /// （WKID 整数 / 名称 / WKT），解析失败 ⇒ INVALID_ARGUMENT。
    /// 新建地图**不自动成为活动地图**：IsActive 一律 false（活动地图以 <c>MapView.Active</c> 为准）。
    /// </remarks>
    public Task<OperationResult<MapInfo>> CreateMapAsync(
        string name, string? basemap, string? spatialReference, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<MapInfo>>(
            () =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return OperationResult<MapInfo>.Fail(ErrorCodes.InvalidArgument, "name is required.");
                }

                // basemap：本批不实现 —— 显式 NOT_IMPLEMENTED，绝不静默忽略参数。
                if (!string.IsNullOrWhiteSpace(basemap))
                {
                    return OperationResult<MapInfo>.Fail(
                        ErrorCodes.NotImplemented,
                        "basemap is not supported by this host yet; omit basemap to create a map without one.");
                }

                ArcGIS.Core.Geometry.SpatialReference? sr = null;
                if (!string.IsNullOrWhiteSpace(spatialReference))
                {
                    try
                    {
                        sr = int.TryParse(
                                spatialReference,
                                System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var wkid)
                            ? new ArcGIS.Core.Geometry.SpatialReferenceBuilder(wkid).ToSpatialReference()
                            : new ArcGIS.Core.Geometry.SpatialReferenceBuilder(spatialReference).ToSpatialReference();
                    }
                    catch (Exception ex)
                    {
                        return OperationResult<MapInfo>.Fail(
                            ErrorCodes.InvalidArgument,
                            "spatialReference is not resolvable (WKID integer or recognized name/WKT expected): " + ex.Message);
                    }
                }

                try
                {
                    // D-061 编译期求证：CreateMap 接受 (name, MapType, MapViewingMode)，
                    // 且**创建时不支持传入空间参考** ⇒ SR 创建后由 Map.SetSpatialReference 写入。
                    var map = MapFactory.Instance.CreateMap(
                        name.Trim(),
                        ArcGIS.Core.CIM.MapType.Map,
                        ArcGIS.Core.CIM.MapViewingMode.Map);

                    if (sr is not null)
                    {
                        map.SetSpatialReference(sr);
                    }

                    var mapTypeText = map.MapType.ToString() ?? string.Empty;
                    return OperationResult<MapInfo>.Ok(new MapInfo
                    {
                        Name = map.Name ?? name.Trim(),
                        Uri = map.URI?.ToString() ?? string.Empty,
                        Kind = string.IsNullOrWhiteSpace(mapTypeText) ? "Map" : mapTypeText,
                        MapType = mapTypeText,
                        Id = map.URI?.ToString() ?? string.Empty,
                        IsActive = false
                    });
                }
                catch (Exception ex)
                {
                    return OperationResult<MapInfo>.Fail(
                        ErrorCodes.InternalError, "Creating the map failed: " + ex.Message);
                }
            },
            TaskCreationOptions.None);

    private static bool Nearly(double? a, double b)
        => a is not null && Math.Abs(a.Value - b) <= 1e-6 * Math.Max(1.0, Math.Abs(b));
}
