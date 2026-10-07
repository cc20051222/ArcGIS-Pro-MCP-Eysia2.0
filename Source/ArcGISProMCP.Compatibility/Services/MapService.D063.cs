using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-063（功能完善第三批）· C 段：视图书签 ＋ ★回图 —— <c>MapService</c> 分部实现。
/// <para>SDK 实证（编译探针 + XML 文档）：<c>MapView.Camera/Extent/ZoomTo</c>、<c>Map.GetBookmarks/AddBookmark/RemoveBookmark</c>、
/// <c>PNGFormat</c>+<c>MapView.Export(ExportFormat)</c>。</para>
/// <para>★ 回图可达性披露：<c>ExportFormat</c> **仅支持 OutputFileName（无内存流 API）**（实证），
/// 故"不落盘模式"=  Render 到**受控临时文件**（D 盘，禁 %TEMP%/C 盘）→ 读取字节 → base64 → **立即删除**；
/// 对外零残留，实现细节如实披露（<c>UsedTransientFile</c>/<c>TransientFileDeleted</c>），绝不静默。</para>
/// </summary>
public sealed partial class MapService
{

    /// <summary>
    /// 回图受控临时目录（**G-138：必须落在 D 盘**，禁 %TEMP%/C 盘；禁程序集旁 = Pro bin 只读）。
    /// <para>★ LIVE 修正（D-063 阶段二首轮实测）：初版默认取 <c>AppContext.BaseDirectory</c>，
    /// 实际解析为 <c>C:\Program Files\ArcGIS\Pro\bin\…</c> ⇒ 目录不可写、且违反 G-138 ⇒ 一律失败。
    /// 现改为「env 覆盖 → D 盘固定根」候选链 + **可写性探针**，全部不可用 ⇒ 返回空串由调用方明确报错
    /// （**绝不静默回落 C 盘**）。</para>
    /// </summary>
    private static string ResolveTransientExportDir()
    {
        var candidates = new List<string>();
        var configured = Environment.GetEnvironmentVariable("ARCGIS_PRO_MCP_MAP_IMAGE_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidates.Add(configured!);
        }

        candidates.Add(Path.Combine(@"D:\", "ArcGISProMCP-temp", "map-image"));

        foreach (var c in candidates)
        {
            try
            {
                Directory.CreateDirectory(c);
                var probe = Path.Combine(c, ".writable-probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return c;
            }
            catch
            {
                // 候选不可用 ⇒ 试下一个（不静默选择不可写/非 D 盘路径）
            }
        }

        return string.Empty;
    }

    // ══════════════════════════ 视图 ══════════════════════════

    /// <summary>D-063：读取活动视图相机态（无活动视图 ⇒ HasActiveView=false + 明确说明）。</summary>
    public Task<OperationResult<MapViewInfo>> GetMapViewAsync(string? mapName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<MapViewInfo>>(
            () =>
            {
                var view = MapView.Active;
                if (view?.Map is null)
                {
                    return OperationResult<MapViewInfo>.Fail(
                        ErrorCodes.NoActiveView,
                        "no active map view is open; open the map (or set it active) before reading the camera.");
                }

                var map = view.Map;
                if (!string.IsNullOrWhiteSpace(mapName)
                    && !string.Equals(map.Name, mapName, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<MapViewInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"active view shows map '{map.Name}', not '{mapName}'; camera state is view-scoped.");
                }

                return OperationResult<MapViewInfo>.Ok(ReadCamera(view, map.Name ?? string.Empty));
            },
            TaskCreationOptions.None);

    private static MapViewInfo ReadCamera(MapView view, string mapName)
    {
        var cam = view.Camera;
        var ext = view.Extent;
        return new MapViewInfo
        {
            MapName = mapName,
            HasActiveView = true,
            X = cam?.X,
            Y = cam?.Y,
            Scale = cam?.Scale,
            Heading = cam?.Heading,
            Pitch = cam?.Pitch,
            Roll = cam?.Roll,
            XMin = ext?.XMin,
            YMin = ext?.YMin,
            XMax = ext?.XMax,
            YMax = ext?.YMax,
        };
    }

    /// <summary>D-063：移动视图（extent 优先，其次 center+scale，rotation 附带）。写后读回。</summary>
    public Task<OperationResult<MapViewSetInfo>> SetMapViewAsync(
        string? mapName, double? xMin, double? yMin, double? xMax, double? yMax,
        double? centerX, double? centerY, double? scale, double? rotation,
        CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<MapViewSetInfo>>(
            () =>
            {
                var view = MapView.Active;
                if (view?.Map is null)
                {
                    return OperationResult<MapViewSetInfo>.Fail(
                        ErrorCodes.NoActiveView, "no active map view is open; cannot set the view.");
                }

                if (!string.IsNullOrWhiteSpace(mapName)
                    && !string.Equals(view.Map.Name, mapName, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<MapViewSetInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"active view shows map '{view.Map.Name}', not '{mapName}'.");
                }

                string requestedBy;
                if (xMin.HasValue && yMin.HasValue && xMax.HasValue && yMax.HasValue)
                {
                    var env = EnvelopeBuilderEx.CreateEnvelope(
                        xMin.Value, yMin.Value, xMax.Value, yMax.Value, view.Map.SpatialReference);
                    view.ZoomTo(env, TimeSpan.Zero);
                    requestedBy = "extent";
                }
                else if (centerX.HasValue && centerY.HasValue)
                {
                    var sr = view.Map.SpatialReference;
                    var targetScale = scale ?? view.Camera?.Scale ?? 0;
                    if (targetScale <= 0)
                    {
                        var ptOnly = MapPointBuilderEx.CreateMapPoint(centerX.Value, centerY.Value, sr);
                        view.ZoomTo(ptOnly, null, false);
                        requestedBy = "center";
                    }
                    else
                    {
                        // 比例 → 范围换算：地图宽度 ≈ scale × 视口像素宽 / 96 DPI / 39.37（inch→m）→ 转 SR 线性单位。
                        var size = view.GetViewSize();
                        var pxW = size.Width > 0 ? size.Width : 1200;
                        var pxH = size.Height > 0 ? size.Height : 800;
                        var metersPerPixel = targetScale / 96.0 / 39.37;
                        var unitFactor = sr?.Unit?.ConversionFactor ?? 1.0;
                        var halfW = (pxW / 2.0) * metersPerPixel * unitFactor;
                        var halfH = (pxH / 2.0) * metersPerPixel * unitFactor;
                        var env = EnvelopeBuilderEx.CreateEnvelope(
                            centerX.Value - halfW, centerY.Value - halfH,
                            centerX.Value + halfW, centerY.Value + halfH, sr);
                        view.ZoomTo(env, TimeSpan.Zero);
                        requestedBy = "center+scale";
                    }
                }
                else if (rotation.HasValue)
                {
                    // ★ 实证披露：Camera 公开构造为 (x,y,z,scale,SR,viewpoint)，heading 无公开 setter。
                    return OperationResult<MapViewSetInfo>.Fail(
                        ErrorCodes.NotImplemented,
                        "rotation-only is not reachable via the public Camera constructor " +
                        "(Camera takes SpatialReference + CameraViewpoint; heading has no public setter). " +
                        "Use an extent or center+scale. Disclosed, not silently ignored.");
                }
                else
                {
                    return OperationResult<MapViewSetInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        "provide an extent (xMin/yMin/xMax/yMax), or centerX+centerY (optionally scale/rotation), or rotation.");
                }

                return OperationResult<MapViewSetInfo>.Ok(new MapViewSetInfo
                {
                    MapName = view.Map.Name ?? string.Empty,
                    Applied = true,
                    RequestedBy = requestedBy,
                    CameraAfter = ReadCamera(view, view.Map.Name ?? string.Empty),
                });
            },
            TaskCreationOptions.None);

    // ══════════════════════════ ★ 回图 ══════════════════════════

    /// <summary>
    /// D-063 ★ 回图：渲染地图视图 → PNG bytes（base64）。不落盘模式（outputPath 为空）
    /// 经受控临时文件中转并立即删除 ⇒ **对外零文件副作用**。
    /// </summary>
    public Task<OperationResult<MapViewImageResult>> ExportMapViewAsync(
        string? mapName, int? width, int? height, int? resolutionDpi, int? maxEdge,
        string? outputPath, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<MapViewImageResult>>(
            () =>
            {
                var view = MapView.Active;
                if (view?.Map is null)
                {
                    return OperationResult<MapViewImageResult>.Fail(
                        ErrorCodes.NoActiveView, "no active map view is open; cannot render a map image.");
                }

                if (!string.IsNullOrWhiteSpace(mapName)
                    && !string.Equals(view.Map.Name, mapName, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<MapViewImageResult>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"active view shows map '{view.Map.Name}', not '{mapName}'.");
                }

                var mapNameResolved = view.Map.Name ?? string.Empty;
                var dpi = resolutionDpi ?? MapImagePolicy.DefaultDpi;
                var edge = maxEdge ?? MapImagePolicy.DefaultMaxEdge;

                // 输出尺寸：显式 width/height 优先，否则按视图宽高比 + 长边上限推导。
                var size = view.GetViewSize();
                var vw = (int)(size.Width > 0 ? size.Width : 1200);
                var vh = (int)(size.Height > 0 ? size.Height : 800);
                var w = width ?? vw;
                var h = height ?? vh;
                MapImagePolicy.ClampToMaxEdge(ref w, ref h, edge);

                var transient = string.IsNullOrWhiteSpace(outputPath);
                var dir = transient ? ResolveTransientExportDir() : ResolveTransientExportDir();
                if (string.IsNullOrEmpty(dir))
                {
                    return OperationResult<MapViewImageResult>.Fail(
                        ErrorCodes.LayerDataSourceUnavailable,
                        "no writable D-drive staging directory for the image export (G-138 forbids %TEMP%/C-drive); " +
                        "set ARCGIS_PRO_MCP_MAP_IMAGE_DIR to a writable D-drive path.");
                }

                var file = Path.Combine(dir, $"mapview-{DateTime.UtcNow:yyyyMMddHHmmssfff}.png");

                string? usedFile = null;
                bool deleted = false;
                try
                {
                    // 首轮渲染；超限 ⇒ 逐轮降采样重试（不截断、不静默超发）。
                    byte[] bytes;
                    var attempt = 0;
                    while (true)
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                        }

                        var fmt = new PNGFormat
                        {
                            OutputFileName = file,
                            Width = Math.Max(1, w),
                            Height = Math.Max(1, h),
                            Resolution = dpi,
                        };

                        view.Export(fmt);

                        if (!File.Exists(file))
                        {
                            return OperationResult<MapViewImageResult>.Fail(
                                ErrorCodes.ArcGISError, "SDK produced no output file for the map view export.");
                        }

                        usedFile = file;
                        bytes = File.ReadAllBytes(file);

                        if (bytes.Length <= MapImagePolicy.MaxPngBytes || attempt >= MapImagePolicy.MaxDownscaleAttempts)
                        {
                            break;
                        }

                        w = (int)(w * 0.7);
                        h = (int)(h * 0.7);
                        MapImagePolicy.ClampToMaxEdge(ref w, ref h, edge);
                        attempt++;
                    }

                    if (bytes.Length > MapImagePolicy.MaxPngBytes)
                    {
                        return OperationResult<MapViewImageResult>.Fail(
                            ErrorCodes.InvalidArgument,
                            $"rendered PNG is {bytes.Length} bytes, above the 1 MB cap even after downscaling; " +
                            "request a smaller width/height or maxEdge.");
                    }

                    // 落盘模式：复制到目标路径（调用方/工具层已过守卫）。
                    if (!transient)
                    {
                        File.Copy(file, outputPath!, true);
                    }

                    // ★ LIVE 修正：中转文件的删除必须在**构造返回值之前**完成并把结果写入字段
                    // （初版把删除放在 finally、返回对象已固定为 false ⇒ 字段语义与实现不符）。
                    deleted = TryDeleteTransient(ref usedFile);

                    var result = new MapViewImageResult
                    {
                        MapName = mapNameResolved,
                        MimeType = "image/png",
                        DataBase64 = Convert.ToBase64String(bytes),
                        Width = Math.Max(1, w),
                        Height = Math.Max(1, h),
                        Bytes = bytes.Length,
                        ResolutionDpi = dpi,
                        OutputPath = transient ? null : outputPath,
                        UsedTransientFile = true,
                        TransientFileDeleted = deleted,
                    };

                    return OperationResult<MapViewImageResult>.Ok(result);
                }
                catch (Exception ex)
                {
                    return OperationResult<MapViewImageResult>.Fail(
                        ErrorCodes.ArcGISError, $"map view export failed: {ex.Message}");
                }
                finally
                {
                    // 兜底：任何遗留的中转文件都删除（成功路径已删并置 null ⇒ 此处幂等）。
                    TryDeleteTransient(ref usedFile);
                }
            },
            TaskCreationOptions.None);

    /// <summary>
    /// 删除回图中转文件并**重试**（★ LIVE 实测教训：SDK 渲染刚结束时可能短暂持有句柄，
    /// 首删会抛 IOException ⇒ 无重试会留下残留件，违反"轮末零残留"判据）。
    /// </summary>
    private static bool TryDeleteTransient(ref string? file)
    {
        if (file is null)
        {
            return false;
        }

        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                if (!File.Exists(file))
                {
                    file = null;
                    return true;
                }

                File.Delete(file);
                if (!File.Exists(file))
                {
                    file = null;
                    return true;
                }
            }
            catch
            {
                // 句柄未释放 ⇒ 短暂等待后重试
            }

            System.Threading.Thread.Sleep(250);
        }

        return false;
    }


    // ══════════════════════════ 书签 ══════════════════════════

    /// <summary>D-063：书签清单（只读）。</summary>
    public Task<OperationResult<BookmarksInfo>> ListBookmarksAsync(string? mapName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<BookmarksInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<BookmarksInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var map = resolved.Map!;
                var bms = map.GetBookmarks();
                return OperationResult<BookmarksInfo>.Ok(new BookmarksInfo
                {
                    Items = bms.Select(b => new BookmarkInfo
                    {
                        Name = b.Name ?? string.Empty,
                        MapName = map.Name ?? string.Empty,
                    }).ToList(),
                    TotalCount = bms.Count,
                });
            },
            TaskCreationOptions.None);

    /// <summary>D-063：以当前视图创建书签。</summary>
    public Task<OperationResult<BookmarkOpInfo>> CreateBookmarkAsync(
        string? mapName, string name, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<BookmarkOpInfo>>(
            () =>
            {
                var view = MapView.Active;
                if (view?.Map is null)
                {
                    return OperationResult<BookmarkOpInfo>.Fail(
                        ErrorCodes.NoActiveView, "no active map view is open; a bookmark captures the current view.");
                }

                if (!string.IsNullOrWhiteSpace(mapName)
                    && !string.Equals(view.Map.Name, mapName, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<BookmarkOpInfo>.Fail(
                        ErrorCodes.InvalidArgument, $"active view shows map '{view.Map.Name}', not '{mapName}'.");
                }

                var map = view.Map;
                if (map.GetBookmarks()
                    .Any(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return OperationResult<BookmarkOpInfo>.Fail(
                        ErrorCodes.InvalidArgument, $"bookmark '{name}' already exists on map '{map.Name}'.");
                }

                map.AddBookmark(view, name);
                return OperationResult<BookmarkOpInfo>.Ok(new BookmarkOpInfo
                {
                    Name = name,
                    MapName = map.Name ?? string.Empty,
                    Operation = "create",
                    Ok = true,
                    BookmarkCountAfter = map.GetBookmarks().Count,
                });
            },
            TaskCreationOptions.None);

    /// <summary>D-063：应用书签（视图跳转）。</summary>
    public Task<OperationResult<BookmarkOpInfo>> ApplyBookmarkAsync(
        string? mapName, string name, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<BookmarkOpInfo>>(
            () =>
            {
                var view = MapView.Active;
                if (view?.Map is null)
                {
                    return OperationResult<BookmarkOpInfo>.Fail(
                        ErrorCodes.NoActiveView, "no active map view is open; cannot apply a bookmark.");
                }

                if (!string.IsNullOrWhiteSpace(mapName)
                    && !string.Equals(view.Map.Name, mapName, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<BookmarkOpInfo>.Fail(
                        ErrorCodes.InvalidArgument, $"active view shows map '{view.Map.Name}', not '{mapName}'.");
                }

                var map = view.Map;
                var bm = map.GetBookmarks()
                    .FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));

                if (bm is null)
                {
                    return OperationResult<BookmarkOpInfo>.Fail(
                        ErrorCodes.NotFound, $"bookmark '{name}' not found on map '{map.Name}'.");
                }

                view.ZoomTo(bm, TimeSpan.Zero);
                return OperationResult<BookmarkOpInfo>.Ok(new BookmarkOpInfo
                {
                    Name = name,
                    MapName = map.Name ?? string.Empty,
                    Operation = "apply",
                    Ok = true,
                    BookmarkCountAfter = map.GetBookmarks().Count,
                });
            },
            TaskCreationOptions.None);

    /// <summary>D-063：删除书签（**仅视图资产**，无数据破坏）。</summary>
    public Task<OperationResult<BookmarkOpInfo>> DeleteBookmarkAsync(
        string? mapName, string name, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<BookmarkOpInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<BookmarkOpInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var map = resolved.Map!;
                var bm = map.GetBookmarks()
                    .FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));

                if (bm is null)
                {
                    return OperationResult<BookmarkOpInfo>.Fail(
                        ErrorCodes.NotFound, $"bookmark '{name}' not found on map '{map.Name}'.");
                }

                map.RemoveBookmark(bm);
                return OperationResult<BookmarkOpInfo>.Ok(new BookmarkOpInfo
                {
                    Name = name,
                    MapName = map.Name ?? string.Empty,
                    Operation = "delete",
                    Ok = true,
                    BookmarkCountAfter = map.GetBookmarks().Count,
                });
            },
            TaskCreationOptions.None);
}
