using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-064（功能完善第四批）· B 段：Project 增强 —— <c>MapService</c> 分部实现。
/// <para>SDK 实证（`bin/Extensions/Mapping/ArcGIS.Desktop.Mapping.XML` + Core/Framework XML，T:/M: 逐条核对）：
/// <c>Project.RemoveItem(IProjectItem)</c> · <c>Map.SetName(string)</c> · <c>Map.SetSpatialReference(SpatialReference)</c> ·
/// <c>Map.Name</c>/<c>Map.SpatialReference</c> · <c>MapProjectItem.GetMap()</c> ·
/// <c>FrameworkApplication.Panes.CreateMapPaneAsync(Map, MapViewingMode?, TimeRange)</c>（Core 扩展方法）。</para>
/// <para><b>UI 依赖的诚实披露</b>：<c>activate_map</c> 属 UI 联动——本实现优先复用已打开的地图窗格并激活；
/// 无窗格时尝试以 <c>CreateMapPaneAsync</c> 新建。若宿主环境不允许（无 UI 线程/自动化环境），
/// 返回 <c>activated=false</c> + <c>note</c> 如实说明，**绝不伪报成功**。</para>
/// </summary>
public sealed partial class MapService
{
    public Task<OperationResult<RemoveMapResult>> RemoveMapAsync(string mapName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<RemoveMapResult>>(
            () =>
            {
                if (string.IsNullOrWhiteSpace(mapName))
                {
                    return OperationResult<RemoveMapResult>.Fail(ErrorCodes.InvalidArgument, "mapName is required.");
                }

                var items = Project.Current?.GetItems<MapProjectItem>().ToList() ?? new List<MapProjectItem>();
                var before = items.Select(i => i.Name ?? string.Empty).ToList();
                var matches = items
                    .Where(i => string.Equals(i.Name, mapName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                {
                    return OperationResult<RemoveMapResult>.Fail(
                        ErrorCodes.MapNotFound, $"map '{mapName}' was not found in the current project.");
                }

                if (matches.Count > 1)
                {
                    return OperationResult<RemoveMapResult>.Fail(
                        ErrorCodes.AmbiguousMapName,
                        $"map name '{mapName}' is ambiguous: {matches.Count} maps share that name; rename one first.");
                }

                try
                {
                    Project.Current!.RemoveItem(matches[0]);
                }
                catch (Exception ex)
                {
                    return OperationResult<RemoveMapResult>.Fail(
                        ErrorCodes.ExecutionFailed, $"failed to remove map '{mapName}': {ex.Message}");
                }

                var after = Project.Current!.GetItems<MapProjectItem>()
                    .Select(i => i.Name ?? string.Empty)
                    .ToList();

                return OperationResult<RemoveMapResult>.Ok(new RemoveMapResult
                {
                    MapName = mapName,
                    Confirm = true,
                    Removed = !after.Any(n => string.Equals(n, mapName, StringComparison.OrdinalIgnoreCase)),
                    MapsBefore = before,
                    MapsAfter = after,
                });
            },
            TaskCreationOptions.None);

    public async Task<OperationResult<ActivateMapResult>> ActivateMapAsync(string mapName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return OperationResult<ActivateMapResult>.Fail(ErrorCodes.InvalidArgument, "mapName is required.");
        }

        // ① 目标地图解析（工程态；不依赖活动视图）。
        var map = await QueuedTask.Run(() =>
        {
            var item = Project.Current?.GetItems<MapProjectItem>()
                .FirstOrDefault(i => string.Equals(i.Name, mapName, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                return (Map?)null;
            }

            return item.GetMap();
        }, TaskCreationOptions.None).ConfigureAwait(false);

        if (map is null)
        {
            return OperationResult<ActivateMapResult>.Fail(
                ErrorCodes.MapNotFound, $"map '{mapName}' was not found in the current project.");
        }

        // ② 已打开窗格 → 激活；否则尝试新建窗格。
        var viewOpened = false;
        try
        {
            // 已打开的地图视图会被 Pro 复用/置前；无视图时新建。
            // 歧义消解：显式转换两个可空参数（构建期 CS0121 证伪了裸 null 的过载推断）。
            var pane = await FrameworkApplication.Panes
                .CreateMapPaneAsync(map, (MapViewingMode?)null, (TimeRange?)null)
                .ConfigureAwait(false);
            viewOpened = pane is not null;
        }
        catch (Exception ex)
        {
            return OperationResult<ActivateMapResult>.Ok(new ActivateMapResult
            {
                MapName = mapName,
                Activated = false,
                ViewOpened = false,
                Note = "map resolved but view activation failed in this host environment: " + ex.Message
                       + " (in an interactive ArcGIS Pro session, open the map view manually; the UI-automation path uses SelectionItem.select + Enter).",
            });
        }

        var active = await QueuedTask.Run(() => MapView.Active?.Map?.Name, TaskCreationOptions.None).ConfigureAwait(false);

        return OperationResult<ActivateMapResult>.Ok(new ActivateMapResult
        {
            MapName = mapName,
            ActiveMap = active,
            Activated = string.Equals(active, mapName, StringComparison.OrdinalIgnoreCase),
            ViewOpened = viewOpened,
            Note = string.Equals(active, mapName, StringComparison.OrdinalIgnoreCase)
                ? null
                : "view was opened/activated but the active view read-back does not match yet (UI may need focus); "
                  + "downstream view-dependent tools (export_map_view / bookmarks) require an active view.",
        });
    }

    public Task<OperationResult<SetMapPropertiesResult>> SetMapPropertiesAsync(
        string mapName, string? newName, string? spatialReference, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<SetMapPropertiesResult>>(
            () =>
            {
                if (string.IsNullOrWhiteSpace(mapName))
                {
                    return OperationResult<SetMapPropertiesResult>.Fail(ErrorCodes.InvalidArgument, "mapName is required.");
                }

                if (string.IsNullOrWhiteSpace(newName) && string.IsNullOrWhiteSpace(spatialReference))
                {
                    return OperationResult<SetMapPropertiesResult>.Fail(
                        ErrorCodes.InvalidArgument, "at least one of newName or spatialReference must be provided.");
                }

                var item = Project.Current?.GetItems<MapProjectItem>()
                    .FirstOrDefault(i => string.Equals(i.Name, mapName, StringComparison.OrdinalIgnoreCase));
                if (item is null)
                {
                    return OperationResult<SetMapPropertiesResult>.Fail(
                        ErrorCodes.MapNotFound, $"map '{mapName}' was not found in the current project.");
                }

                // 改名重名前置拒绝（避免 SDK 静默产生歧义名）。
                if (!string.IsNullOrWhiteSpace(newName)
                    && !string.Equals(newName, mapName, StringComparison.OrdinalIgnoreCase)
                    && (Project.Current!.GetItems<MapProjectItem>()
                        .Any(i => string.Equals(i.Name, newName, StringComparison.OrdinalIgnoreCase))))
                {
                    return OperationResult<SetMapPropertiesResult>.Fail(
                        ErrorCodes.AmbiguousMapName,
                        $"a map named '{newName}' already exists; rename refused (no change made).");
                }

                Map map;
                try
                {
                    map = item.GetMap();
                }
                catch (Exception ex)
                {
                    return OperationResult<SetMapPropertiesResult>.Fail(
                        ErrorCodes.ExecutionFailed, $"failed to load map '{mapName}': {ex.Message}");
                }

                if (map is null)
                {
                    return OperationResult<SetMapPropertiesResult>.Fail(
                        ErrorCodes.MapNotFound, $"map '{mapName}' could not be loaded.");
                }

                var nameBefore = map.Name ?? string.Empty;
                var srBefore = map.SpatialReference?.Name;

                var renamed = false;
                if (!string.IsNullOrWhiteSpace(newName)
                    && !string.Equals(newName, nameBefore, StringComparison.Ordinal))
                {
                    try
                    {
                        map.SetName(newName!);
                        renamed = true;
                    }
                    catch (Exception ex)
                    {
                        return OperationResult<SetMapPropertiesResult>.Fail(
                            ErrorCodes.ExecutionFailed, $"failed to rename map: {ex.Message}");
                    }
                }

                var srChanged = false;
                if (!string.IsNullOrWhiteSpace(spatialReference))
                {
                    SpatialReference? sr = null;
                    // ★ D-064 阶段二真缺陷 ② 修复：短串（如 "4326"）**先按 WKID 解释**，
                    //   否则会被 SpatialReferenceBuilder 当作"名称/WKT"解析而失败（LIVE 实测 INVALID_ARGUMENT）。
                    var srText = spatialReference!.Trim();
                    try
                    {
                        if (int.TryParse(srText, out var wkid) && wkid > 0)
                        {
                            sr = SpatialReferenceBuilder.CreateSpatialReference(wkid);
                        }
                        else
                        {
                            sr = SpatialReferenceBuilder.CreateSpatialReference(srText);
                        }
                    }
                    catch (Exception)
                    {
                        sr = null;
                    }

                    if (sr is null)
                    {
                        return OperationResult<SetMapPropertiesResult>.Fail(
                            ErrorCodes.InvalidArgument,
                            $"spatialReference '{spatialReference}' could not be resolved (WKID number, SR name or WKT expected).");
                    }

                    try
                    {
                        map.SetSpatialReference(sr);
                        srChanged = true;
                    }
                    catch (Exception ex)
                    {
                        return OperationResult<SetMapPropertiesResult>.Fail(
                            ErrorCodes.ExecutionFailed, $"failed to set map spatial reference: {ex.Message}");
                    }
                }

                var names = Project.Current!.GetItems<MapProjectItem>()
                    .Select(i => i.Name ?? string.Empty)
                    .ToList();

                return OperationResult<SetMapPropertiesResult>.Ok(new SetMapPropertiesResult
                {
                    MapName = mapName,
                    Name = map.Name ?? string.Empty,
                    NameBefore = nameBefore,
                    Renamed = renamed,
                    SpatialReference = map.SpatialReference?.Name,
                    SpatialReferenceBefore = srBefore,
                    SpatialReferenceChanged = srChanged,
                    MapNames = names,
                });
            },
            TaskCreationOptions.None);
}
