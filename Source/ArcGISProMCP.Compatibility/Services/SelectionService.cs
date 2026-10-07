using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Selection;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>选择集服务实现（真实访问 ArcGIS Pro 选择）。</summary>
/// <remarks>D-009：地图解析收敛至 <see cref="MapResolver"/>（去 CreateMapFromItem 依赖）。
/// D-013（G-30/G-32）：clear_selection 接入选择账本——写前核对基线（不符 → SELECTION_BASELINE_MISMATCH 拒绝）、
/// 自动写前快照（强制，不可关）、写后提交 revision。select_layer 语义不变（TOC 图层高亮，不触碰要素选择集）。</remarks>
public sealed class SelectionService : ISelectionService
{
    private readonly ISelectionStateStore _store;

    public SelectionService(ISelectionStateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<OperationResult<SelectionInfo>> GetSelectionAsync(string? mapName = null, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<SelectionInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<SelectionInfo>(resolved, mapName) is { } resolveFail) return resolveFail;
                var map = resolved.Map!;

                // 返回当前地图的上下文信息；具体被选要素的读取依赖活动视图选择 API（受限）。
                return OperationResult<SelectionInfo>.Ok(new SelectionInfo
                {
                    MapName = map.Name ?? string.Empty,
                    LayerName = string.Empty,
                    Count = 0
                });
            },
            TaskCreationOptions.None);

    public Task<OperationResult<bool>> ClearSelectionAsync(string? mapName = null, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<bool>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<bool>(resolved, mapName) is { } resolveFail) return resolveFail;
                var map = resolved.Map!;

                // Phase 8.3（D-013）：账本写前核对——UI 并发改动 → 拒绝（绝不覆盖用户选择）。
                var before = SelectionReadService.ReadContent(map);
                var check = _store.CheckWritePrecondition(map.URI, before);
                if (!check.Matched)
                {
                    return OperationResult<bool>.Fail(
                        ErrorCodes.SelectionBaselineMismatch,
                        check.DiffSummary ?? "selection baseline mismatch.");
                }

                // 自动写前快照（G-30 强制）。
                _store.AddSnapshot(map.URI, map.Name ?? string.Empty, before, check.Revision);

                map.ClearSelection();

                var after = SelectionReadService.ReadContent(map);
                _store.CommitWrite(map.URI, after, "clear_selection");
                return OperationResult<bool>.Ok(true);
            },
            TaskCreationOptions.None);

    public Task<OperationResult<SelectionInfo>> SelectLayerAsync(string mapName, string layerName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<SelectionInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<SelectionInfo>(resolved, mapName) is { } resolveFail) return resolveFail;
                var map = resolved.Map!;

                // F1（D-022 站点 4）：重名 → AMBIGUOUS_LAYER_NAME，禁止静默高亮第一层。
                var resolvedLayer = LayerResolver.Resolve(map, layerName, flatten: true);
                if (LayerResolver.FailIfNotOk<SelectionInfo>(resolvedLayer, layerName) is { } layerFail)
                {
                    return layerFail;
                }

                var layer = resolvedLayer.Layer!;

                var view = MapView.Active;
                if (view is null || !ReferenceEquals(view.Map, map))
                {
                    return OperationResult<SelectionInfo>.Fail(ErrorCodes.InvalidState, "No active map view for the map.");
                }

                view.SelectLayers(new[] { layer });
                return OperationResult<SelectionInfo>.Ok(new SelectionInfo { MapName = map.Name ?? string.Empty, LayerName = layer.Name ?? string.Empty, Count = 0 });
            },
            TaskCreationOptions.None);

}
