using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-063（功能完善第三批）· A 段图层管理 ＋ B 段渲染进阶 —— <c>LayerService</c> 分部实现。
/// <para>SDK 面均为**编译探针 + XML 文档实证**（见 run-20260920-d063/probe*.log）：
/// <c>MapMember.SetName</c>、<c>LayerFactory.CopyLayer/ CreateGroupLayer</c>、<c>Layer.SetTransparency/SetMinScale/SetMaxScale</c>、
/// <c>Layer.FindAndReplaceWorkspacePath</c>、<c>Basemap</c>+<c>Map.SetBasemapLayers</c>、<c>StyleHelper.SearchColorRamps</c>、
/// <c>LayerDocument.AsJson/GetCIMLayerDocument</c>、<c>FeatureLayer.CreateRenderer/SetRenderer/GetRenderer</c>、
/// <c>BasicFeatureLayer.Join(JoinDescription)</c>。</para>
/// </summary>
public sealed partial class LayerService
{
    // ══════════════════════════ 符号增强（工单 B2）══════════════════════════

    /// <summary>D-063：复杂渲染器（唯一值 / 分级）读出结构化摘要；不可读出的项保持 null（不静默降级）。</summary>
    private static void EnrichComplexRenderer(CIMRenderer? renderer, LayerSymbologyInfo info)
    {
        if (renderer is null)
        {
            return;
        }

        if (renderer is CIMUniqueValueRenderer uv)
        {
            var fields = (uv.Fields ?? Array.Empty<string>())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct()
                .ToList();

            info.UniqueValueFields = fields.Count > 0 ? fields : null;

            var groups = uv.Groups ?? Array.Empty<CIMUniqueValueGroup>();
            var classes = new List<UniqueValueClassSummary>();
            foreach (var g in groups)
            {
                foreach (var c in g.Classes ?? Array.Empty<CIMUniqueValueClass>())
                {
                    var values = new List<string>();
                    foreach (var v in c.Values ?? Array.Empty<CIMUniqueValue>())
                    {
                        var fv = v?.FieldValues;
                        if (fv is null || fv.Length == 0) { continue; }
                        values.Add(string.Join(" | ", fv));
                    }
                    classes.Add(new UniqueValueClassSummary
                    {
                        Value = string.Join(" | ", values),
                        Color = ToHexColor(c.Symbol?.Symbol?.GetColor()),
                        Label = c.Label,
                    });
                }
            }

            info.UniqueValueClassCount = classes.Count;
            info.UniqueValueClasses = classes.Count > 0 ? classes : null;
            return;
        }

        if (renderer is CIMClassBreaksRenderer cb)
        {
            info.ClassBreakField = cb.Field;
            var breaks = cb.Breaks ?? Array.Empty<CIMClassBreak>();
            var list = new List<ClassBreakSummary>();
            double? previousUpper = null;
            foreach (var b in breaks)
            {
                list.Add(new ClassBreakSummary
                {
                    Upper = b.UpperBound,
                    Lower = previousUpper,
                    Color = ToHexColor(b.Symbol?.Symbol?.GetColor()),
                    Label = b.Label,
                });
                previousUpper = b.UpperBound;
            }

            info.ClassBreakCount = list.Count;
            info.ClassBreaks = list.Count > 0 ? list : null;
            info.ColorRampName = null;   // CIM 无稳定色带名读出（如实 null，不伪造）
        }
    }

    /// <summary>D-063：标注摘要随符号一并读出（与 get_label_info 同源）。</summary>
    private static void EnrichLabelDigest(FeatureLayer featureLayer, LayerSymbologyInfo info)
    {
        try
        {
            var classes = featureLayer.LabelClasses;
            var first = classes is { Count: > 0 } ? classes[0] : null;
            info.Labels = new LabelDigest
            {
                Enabled = featureLayer.IsLabelVisible,
                LabelClassCount = classes?.Count ?? 0,
                FirstExpression = first?.Expression,
            };
        }
        catch
        {
            info.Labels = null;
        }
    }

    // ══════════════════════════ A 段：图层管理 ══════════════════════════

    /// <summary>D-063：图层外观（透明度 / 显示比例）。null 项 = 不改；写后读回。</summary>
    public Task<OperationResult<LayerAppearanceInfo>> SetLayerAppearanceAsync(
        string? mapName, string layerName, double? transparency,
        double? minScale, double? maxScale, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LayerAppearanceInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<LayerAppearanceInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var lr = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (lr.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<LayerAppearanceInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, lr));
                }

                if (lr.Status != LayerResolveStatus.Ok || lr.Layer is null)
                {
                    return OperationResult<LayerAppearanceInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                var layer = lr.Layer;
                if (transparency.HasValue)
                {
                    layer.SetTransparency(transparency.Value);
                }

                if (minScale.HasValue || maxScale.HasValue)
                {
                    var lo = minScale ?? layer.MinScale;
                    var hi = maxScale ?? layer.MaxScale;
                    layer.SetMinScale(lo);
                    layer.SetMaxScale(hi);
                }

                return OperationResult<LayerAppearanceInfo>.Ok(new LayerAppearanceInfo
                {
                    LayerName = layer.Name ?? layerName,
                    MapName = resolved.Map!.Name ?? string.Empty,
                    Transparency = layer.Transparency,
                    MinScale = layer.MinScale,
                    MaxScale = layer.MaxScale,
                    ShowLayerAtAllScales = layer.MinScale <= 0 && layer.MaxScale <= 0,
                });
            },
            TaskCreationOptions.None);

    /// <summary>D-063：创建组图层（可选把既有图层移入）。</summary>
    public Task<OperationResult<GroupLayerInfo>> CreateGroupLayerAsync(
        string? mapName, string groupName, IReadOnlyList<string>? layerNames, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<GroupLayerInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<GroupLayerInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var map = resolved.Map!;
                var group = LayerFactory.Instance.CreateGroupLayer((ILayerContainerEdit)map, -1, groupName);
                if (group is null)
                {
                    return OperationResult<GroupLayerInfo>.Fail(
                        ErrorCodes.ArcGISError, $"Failed to create group layer '{groupName}'.");
                }

                var moved = new List<string>();
                var skipped = new List<string>();
                foreach (var name in layerNames ?? Array.Empty<string>())
                {
                    var lr = LayerResolver.Resolve(map, name, flatten: true);
                    if (lr.Status != LayerResolveStatus.Ok || lr.Layer is null)
                    {
                        skipped.Add(name);
                        continue;
                    }

                    try
                    {
                        resolved.Map!.MoveLayer(lr.Layer, group, -1);
                        moved.Add(name);
                    }
                    catch
                    {
                        skipped.Add(name);
                    }
                }

                var info = new GroupLayerInfo
                {
                    GroupName = group.Name ?? groupName,
                    MapName = map.Name ?? string.Empty,
                    Position = -1,
                    MovedLayerCount = moved.Count,
                    MovedLayers = moved,
                };

                return OperationResult<GroupLayerInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    /// <summary>D-063：底图设置（需网络；离线/超时 ⇒ 明确报错不挂起）。</summary>
    public Task<OperationResult<BasemapInfo>> SetBasemapAsync(
        string? mapName, string basemap, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<BasemapInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<BasemapInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var info = new BasemapInfo
                {
                    MapName = resolved.Map!.Name ?? string.Empty,
                    RequestedBasemap = basemap,
                    Applied = false,
                };

                // 底图来自 portal；离线环境取不到 ⇒ 明确报错（不挂起、不静默成功）。
                Item? item = null;
                try
                {
                    var portal = ArcGISPortalManager.Current.GetActivePortal();
                    if (portal is null || !portal.IsSignedOn())
                    {
                        info.Reason = "no active/signed-on portal; basemap gallery is unavailable offline.";
                        return OperationResult<BasemapInfo>.Fail(ErrorCodes.LayerDataSourceUnavailable, info.Reason);
                    }

                    var gallery = portal.GetBasemapsAsync().GetAwaiter().GetResult();
                    var items = gallery is null
                        ? new List<Item>()
                        : gallery.ToList().OfType<Item>().ToList();
                    info.AvailableBasemaps = items
                        .Select(i => i.Name ?? string.Empty)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList();

                    item = items.FirstOrDefault(
                        i => string.Equals(i.Name, basemap, StringComparison.OrdinalIgnoreCase));
                    if (item is null)
                    {
                        info.Reason = $"basemap '{basemap}' not found in the portal gallery "
                                    + $"({info.AvailableBasemaps.Count} available).";
                        return OperationResult<BasemapInfo>.Fail(ErrorCodes.NotFound, info.Reason);
                    }
                }
                catch (Exception ex)
                {
                    info.Reason = $"basemap gallery unavailable: {ex.Message}";
                    return OperationResult<BasemapInfo>.Fail(ErrorCodes.LayerDataSourceUnavailable, info.Reason);
                }

                resolved.Map!.SetBasemapLayers(item);
                info.Applied = true;
                return OperationResult<BasemapInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    /// <summary>D-063：全工程断源图层扫描（只读）。</summary>
    public Task<OperationResult<BrokenLayersInfo>> GetBrokenLayersAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<BrokenLayersInfo>>(
            () =>
            {
                var items = new List<BrokenLayerInfo>();
                foreach (var map in Project.Current.GetItems<MapProjectItem>().Select(i => i.GetMap()).Where(m => m is not null).Select(m => m!))
                {
                    foreach (var layer in map.GetLayersAsFlattenedList().OfType<Layer>())
                    {
                        var broken = IsBroken(layer);
                        if (!broken)
                        {
                            continue;
                        }

                        items.Add(new BrokenLayerInfo
                        {
                            LayerName = layer.Name ?? string.Empty,
                            MapName = map.Name ?? string.Empty,
                            LayerType = layer.GetType().Name,
                            DataSourcePath = SafeDataSourcePath(layer),
                            IsBroken = true,
                        });
                    }
                }

                return OperationResult<BrokenLayersInfo>.Ok(new BrokenLayersInfo
                {
                    Items = items,
                    TotalCount = items.Count,
                });
            },
            TaskCreationOptions.None);

    private static bool IsBroken(Layer layer)
    {
        try
        {
            var status = layer.ConnectionStatus;
            if (status != ConnectionStatus.Connected)
            {
                return true;
            }
        }
        catch
        {
            return true;
        }

        return false;
    }

    private static string? SafeDataSourcePath(Layer layer)
    {
        try
        {
            if (layer is BasicFeatureLayer bfl)
            {
                return bfl.GetTable()?.GetPath()?.ToString();
            }

            return layer.GetPath()?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>D-063：重指数据源（工作区路径替换 / 数据集重指）；前后照。</summary>
    public Task<OperationResult<RepairLayerInfo>> RepairLayerSourceAsync(
        string? mapName, string layerName, string? newWorkspacePath, string? newDatasetName,
        CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<RepairLayerInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<RepairLayerInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var lr = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (lr.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<RepairLayerInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, lr));
                }

                if (lr.Status != LayerResolveStatus.Ok || lr.Layer is null)
                {
                    return OperationResult<RepairLayerInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                var layer = lr.Layer;
                var oldPath = SafeDataSourcePath(layer);
                var method = string.Empty;

                if (!string.IsNullOrWhiteSpace(newWorkspacePath))
                {
                    var oldWs = WorkspaceOf(oldPath);
                    if (string.IsNullOrEmpty(oldWs))
                    {
                        return OperationResult<RepairLayerInfo>.Fail(
                            ErrorCodes.InvalidArgument,
                            "cannot determine the layer's current workspace path; provide newDatasetName instead.");
                    }

                    layer.FindAndReplaceWorkspacePath(oldWs, newWorkspacePath!, true);
                    method = "FindAndReplaceWorkspacePath";
                }
                else if (!string.IsNullOrWhiteSpace(newDatasetName))
                {
                    // 数据集重指：同工作区内换数据集名（保守：经 DataConnection 复制 + 替换）。
                    var conn = layer.GetDataConnection();
                    if (conn is CIMStandardDataConnection sdc)
                    {
                        var clone = (CIMStandardDataConnection)sdc.Clone();
                        clone.Dataset = newDatasetName;
                        layer.SetDataConnection(clone);
                        method = "SetDataConnection(dataset)";
                    }
                    else
                    {
                        return OperationResult<RepairLayerInfo>.Fail(
                            ErrorCodes.InvalidArgument,
                            $"layer data connection type '{conn?.GetType().Name ?? "null"}' does not support dataset rebinding.");
                    }
                }
                else
                {
                    return OperationResult<RepairLayerInfo>.Fail(
                        ErrorCodes.InvalidArgument, "newWorkspacePath or newDatasetName is required.");
                }

                return OperationResult<RepairLayerInfo>.Ok(new RepairLayerInfo
                {
                    LayerName = layer.Name ?? layerName,
                    MapName = resolved.Map!.Name ?? string.Empty,
                    OldPath = oldPath,
                    NewPath = SafeDataSourcePath(layer),
                    Repaired = !IsBroken(layer),
                    Method = method,
                });
            },
            TaskCreationOptions.None);

    private static string WorkspaceOf(string? dataSourcePath)
    {
        if (string.IsNullOrWhiteSpace(dataSourcePath))
        {
            return string.Empty;
        }

        // gdb 数据集路径形态：…\xxx.gdb\Dataset ⇒ 工作区 = 去掉最后一段
        var idx = dataSourcePath!.LastIndexOf('\\');
        return idx > 0 ? dataSourcePath.Substring(0, idx) : string.Empty;
    }

    // O-D066-05（D-066 阶段二）：AddJoinAsync/RemoveJoinAsync 占位实现已删除（零调用方；
    // 字段 join 与解除 join 均改经受控 GP 代理；连接状态证据通道 = GetJoinStateAsync）。

    private static Geodatabase? GeodatabaseOf(BasicFeatureLayer bfl)
    {
        try
        {
            return bfl.GetTable()?.GetDatastore() as Geodatabase;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasJoin(BasicFeatureLayer bfl)
    {
        try
        {
            return bfl.GetTable()?.GetJoin() is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>D-063：TOC 改名（写后读回）。</summary>
    public Task<OperationResult<RenameLayerInfo>> RenameLayerAsync(
        string? mapName, string layerName, string newName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<RenameLayerInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<RenameLayerInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var lr = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (lr.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<RenameLayerInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, lr));
                }

                if (lr.Status != LayerResolveStatus.Ok || lr.Layer is null)
                {
                    return OperationResult<RenameLayerInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                var before = lr.Layer.Name ?? layerName;
                lr.Layer.SetName(newName);
                return OperationResult<RenameLayerInfo>.Ok(new RenameLayerInfo
                {
                    OldName = before,
                    NewName = lr.Layer.Name ?? newName,
                    MapName = resolved.Map!.Name ?? string.Empty,
                });
            },
            TaskCreationOptions.None);

    /// <summary>D-063：复制图层（同图内副本）。</summary>
    public Task<OperationResult<DuplicateLayerInfo>> DuplicateLayerAsync(
        string? mapName, string layerName, string? newName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<DuplicateLayerInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<DuplicateLayerInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var lr = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (lr.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<DuplicateLayerInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, lr));
                }

                if (lr.Status != LayerResolveStatus.Ok || lr.Layer is null)
                {
                    return OperationResult<DuplicateLayerInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                var copy = LayerFactory.Instance.CopyLayer(lr.Layer, (ILayerContainerEdit)resolved.Map!, -1);
                if (copy is null)
                {
                    return OperationResult<DuplicateLayerInfo>.Fail(
                        ErrorCodes.ArcGISError, $"Failed to duplicate layer '{layerName}'.");
                }

                var name = newName ?? (lr.Layer.Name ?? layerName) + " copy";
                copy.SetName(name);

                return OperationResult<DuplicateLayerInfo>.Ok(new DuplicateLayerInfo
                {
                    SourceLayerName = lr.Layer.Name ?? layerName,
                    NewLayerName = copy.Name ?? name,
                    MapName = resolved.Map!.Name ?? string.Empty,
                });
            },
            TaskCreationOptions.None);

    // ══════════════════════════ B 段：渲染进阶 ══════════════════════════

    /// <summary>D-063：三模式渲染器设置（single / unique / graduated）。非法字段 ⇒ INVALID_ARGUMENT。</summary>
    public Task<OperationResult<LayerRendererInfo>> SetLayerRendererAsync(
        string? mapName, string layerName, string mode, string? field,
        int? classCount, string? colorRamp, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LayerRendererInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<LayerRendererInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var lr = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (lr.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<LayerRendererInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, lr));
                }

                if (lr.Status != LayerResolveStatus.Ok || lr.Layer is null)
                {
                    return OperationResult<LayerRendererInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                if (lr.Layer is not FeatureLayer fl)
                {
                    return OperationResult<LayerRendererInfo>.Fail(
                        ErrorCodes.InvalidArgument, $"layer '{layerName}' is not a feature layer (renderers unsupported).");
                }

                var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
                RendererDefinition? def = null;
                string? colorRampName = null;

                if (normalized is "single" or "simple")
                {
                    def = new SimpleRendererDefinition();
                }
                else if (normalized is "unique" or "uniquevalue" or "unique_value")
                {
                    if (string.IsNullOrWhiteSpace(field))
                    {
                        return OperationResult<LayerRendererInfo>.Fail(
                            ErrorCodes.InvalidArgument, "field is required for the 'unique' renderer mode.");
                    }

                    if (!LayerHasField(fl, field!))
                    {
                        return OperationResult<LayerRendererInfo>.Fail(
                            ErrorCodes.InvalidArgument, $"field '{field}' does not exist on layer '{layerName}'.");
                    }

                    def = new UniqueValueRendererDefinition(new List<string> { field! });
                }
                else if (normalized is "graduated" or "classbreaks" or "class_breaks")
                {
                    if (string.IsNullOrWhiteSpace(field))
                    {
                        return OperationResult<LayerRendererInfo>.Fail(
                            ErrorCodes.InvalidArgument, "field is required for the 'graduated' renderer mode.");
                    }

                    if (!LayerHasField(fl, field!))
                    {
                        return OperationResult<LayerRendererInfo>.Fail(
                            ErrorCodes.InvalidArgument, $"field '{field}' does not exist on layer '{layerName}'.");
                    }

                    var gc = new GraduatedColorsRendererDefinition
                    {
                        ClassificationField = field!,
                        BreakCount = classCount ?? 5,
                    };

                    var ramp = FindColorRamp(colorRamp);
                    if (ramp is not null)
                    {
                        gc.ColorRamp = ramp;
                        colorRampName = colorRamp;
                    }

                    def = gc;
                }
                else
                {
                    return OperationResult<LayerRendererInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"unknown renderer mode '{mode}'; supported: single | unique | graduated.");
                }

                var renderer = fl.CreateRenderer(def);
                if (renderer is null)
                {
                    return OperationResult<LayerRendererInfo>.Fail(
                        ErrorCodes.ArcGISError, $"SDK refused to create a renderer for mode '{normalized}'.");
                }

                if (!fl.CanSetRenderer(renderer, FeatureRendererTarget.Default))
                {
                    return OperationResult<LayerRendererInfo>.Fail(
                        ErrorCodes.InvalidArgument, $"layer '{layerName}' does not accept this renderer.");
                }

                fl.SetRenderer(renderer);

                var after = fl.GetRenderer();
                return OperationResult<LayerRendererInfo>.Ok(new LayerRendererInfo
                {
                    LayerName = fl.Name ?? layerName,
                    MapName = resolved.Map!.Name ?? string.Empty,
                    Mode = normalized,
                    RendererType = after?.GetType().Name,
                    Field = field,
                    ClassCount = classCount,
                    ColorRamp = colorRampName,
                    Applied = true,
                });
            },
            TaskCreationOptions.None);

    private static bool LayerHasField(FeatureLayer fl, string field)
    {
        try
        {
            return (fl.GetTable()?.GetDefinition()?.GetFields() ?? Array.Empty<Field>())
                .Any(f => string.Equals(f.Name, field, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return true; // 不可判别 ⇒ 交由 SDK 判定（不静默放行，后续 CreateRenderer 会失败并如实报错）
        }
    }

    private static CIMColorRamp? FindColorRamp(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        try
        {
            foreach (var style in Project.Current.GetItems<StyleProjectItem>())
            {
                var ramps = StyleHelper.SearchColorRamps(style, name!);
                if (ramps is null)
                {
                    continue;
                }

                foreach (var r in ramps)
                {
                    if (r?.ColorRamp is { } cr)
                    {
                        return cr;
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    /// <summary>D-063：工程可用色带清单（只读）。</summary>
    public Task<OperationResult<ColorRampsInfo>> ListColorRampsAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<ColorRampsInfo>>(
            () =>
            {
                var items = new List<ColorRampInfo>();
                string? styleName = null;
                foreach (var style in Project.Current.GetItems<StyleProjectItem>())
                {
                    styleName ??= style.Name;
                    foreach (var r in StyleHelper.SearchColorRamps(style, string.Empty) ?? new List<ColorRampStyleItem>())
                    {
                        items.Add(new ColorRampInfo
                        {
                            Name = r.Name ?? string.Empty,
                            Category = r.Category,
                        });
                    }
                }

                return OperationResult<ColorRampsInfo>.Ok(new ColorRampsInfo
                {
                    Items = items,
                    TotalCount = items.Count,
                    StyleName = styleName,
                });
            },
            TaskCreationOptions.None);

    /// <summary>D-063：从 .lyrx 应用符号 —— **只替换 renderer**，不替换数据连接。</summary>
    public Task<OperationResult<LayerFileInfo>> ApplySymbologyFromLayerAsync(
        string? mapName, string layerName, string layerFilePath, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LayerFileInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<LayerFileInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var lr = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (lr.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<LayerFileInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, lr));
                }

                if (lr.Status != LayerResolveStatus.Ok || lr.Layer is null)
                {
                    return OperationResult<LayerFileInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                if (lr.Layer is not FeatureLayer fl)
                {
                    return OperationResult<LayerFileInfo>.Fail(
                        ErrorCodes.InvalidArgument, $"layer '{layerName}' is not a feature layer (symbology unsupported).");
                }

                CIMLayerDocument doc;
                try
                {
                    doc = new LayerDocument(layerFilePath).GetCIMLayerDocument();
                }
                catch (Exception ex)
                {
                    return OperationResult<LayerFileInfo>.Fail(
                        ErrorCodes.InvalidArgument, $"cannot read layer file '{layerFilePath}': {ex.Message}");
                }

                var renderer = doc?.LayerDefinitions?.OfType<CIMFeatureLayer>().FirstOrDefault()?.Renderer;
                if (renderer is null)
                {
                    return OperationResult<LayerFileInfo>.Fail(
                        ErrorCodes.InvalidArgument, $"layer file '{layerFilePath}' exposes no feature-layer renderer.");
                }

                fl.SetRenderer(renderer);
                return OperationResult<LayerFileInfo>.Ok(new LayerFileInfo
                {
                    LayerName = fl.Name ?? layerName,
                    Path = layerFilePath,
                    Operation = "apply",
                    Ok = true,
                });
            },
            TaskCreationOptions.None);

    /// <summary>D-063：保存图层为 .lyrx（**写文件 ⇒ 调用方先行守卫**）。</summary>
    public Task<OperationResult<LayerFileInfo>> SaveLayerFileAsync(
        string? mapName, string layerName, string outputPath, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LayerFileInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<LayerFileInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var lr = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (lr.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<LayerFileInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, lr));
                }

                if (lr.Status != LayerResolveStatus.Ok || lr.Layer is null)
                {
                    return OperationResult<LayerFileInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                var def = lr.Layer.GetDefinition();
                var doc = new CIMLayerDocument { LayerDefinitions = new CIMBaseLayer[] { def } };
                var json = doc.ToJson();
                File.WriteAllText(outputPath, json, System.Text.Encoding.UTF8);

                return OperationResult<LayerFileInfo>.Ok(new LayerFileInfo
                {
                    LayerName = lr.Layer.Name ?? layerName,
                    Path = outputPath,
                    Operation = "save",
                    Bytes = new FileInfo(outputPath).Length,
                    Ok = true,
                });
            },
            TaskCreationOptions.None);
}
