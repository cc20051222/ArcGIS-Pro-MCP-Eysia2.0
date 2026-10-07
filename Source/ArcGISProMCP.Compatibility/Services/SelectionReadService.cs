using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Data;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Selection;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// 选择集读取/快照/恢复服务实现（Phase 8.3，D-013；10 ADR：读与快照归 Native）。
/// 读取路径：FeatureLayer/StandaloneTable.GetSelection() + Selection.GetObjectIDs()（D-060 跨代公共 API 面）。
/// 恢复路径：SelectionSet.FromDictionary + Map.SetSelection（两代同名同形，已在 13.0/13.5 双向核验）。
/// </summary>
public sealed class SelectionReadService : ISelectionReadService
{
    /// <summary>读取侧单图层 OID 返回上限（截断告警，8.2 模式；SelectedCount 始终为真实值）。</summary>
    public const int MaxOidsPerLayer = 10_000;

    private readonly ISelectionStateStore _store;

    public SelectionReadService(ISelectionStateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public Task<OperationResult<SelectionContent>> GetSelectionContentAsync(string? mapName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<SelectionContent>>(() =>
        {
            var resolved = MapResolver.Resolve(mapName);
            if (MapResolver.FailIfNotOk<SelectionContent>(resolved, mapName) is { } resolveFail)
            {
                return resolveFail;
            }

            return OperationResult<SelectionContent>.Ok(ReadContent(resolved.Map!));
        });

    /// <inheritdoc />
    public Task<OperationResult<SelectionContent>> GetSelectionContentByUriAsync(string mapUri, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<SelectionContent>>(() =>
        {
            foreach (var item in Project.Current.GetItems<MapProjectItem>())
            {
                var m = item.GetMap();
                if (m is not null && string.Equals(m.URI, mapUri, StringComparison.Ordinal))
                {
                    return OperationResult<SelectionContent>.Ok(ReadContent(m));
                }
            }

            return OperationResult<SelectionContent>.Fail(
                ErrorCodes.SelectionSnapshotExpired,
                $"map not found in current project (mapUri={mapUri}).");
        });

    /// <inheritdoc />
    public Task<OperationResult<SelectionSnapshotRecord>> CreateSnapshotAsync(string? mapName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<SelectionSnapshotRecord>>(() =>
        {
            var resolved = MapResolver.Resolve(mapName);
            if (MapResolver.FailIfNotOk<SelectionSnapshotRecord>(resolved, mapName) is { } resolveFail)
            {
                return resolveFail;
            }

            var map = resolved.Map!;
            var content = ReadContent(map);
            _store.TryGetLedgerRevision(map.URI, out var revision);
            // 设计 §9：显式重新快照 = 基线重同步点（用户 UI 改动被拒后由此恢复可写）。
            revision = _store.ResyncLedger(map.URI, content, "create_selection_snapshot");
            var record = _store.AddSnapshot(map.URI, map.Name ?? string.Empty, content, revision);
            return OperationResult<SelectionSnapshotRecord>.Ok(record);
        });

    /// <inheritdoc />
    public Task<OperationResult<SnapshotRestoreResult>> RestoreSnapshotAsync(string mapName, SelectionContent content, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<SnapshotRestoreResult>>(() =>
        {
            if (content is null)
            {
                return OperationResult<SnapshotRestoreResult>.Fail(ErrorCodes.InvalidArgument, "content is required.");
            }

            // 按 URI 定位快照目标地图（地图名可重名，8.1 AMBIGUOUS 循例；URI 唯一）。
            ArcGIS.Desktop.Mapping.Map? map = null;
            foreach (var item in Project.Current.GetItems<MapProjectItem>())
            {
                var m = item.GetMap();
                if (m is not null && string.Equals(m.URI, content.MapUri, StringComparison.Ordinal))
                {
                    map = m;
                    break;
                }
            }

            if (map is null)
            {
                return OperationResult<SnapshotRestoreResult>.Fail(
                    ErrorCodes.SelectionSnapshotExpired,
                    $"snapshot map not found in current project (mapUri={content.MapUri}).");
            }

            // F10（D-028）：索引必须**展平**（含组内子层），否则组子层选择会出现
            // "能选（LayerResolver flatten）/能拍快照（ReadContent）/不能恢复" 的不一致（restoredCount 静默为 0）。
            // 复用 LayerResolver.EnumerateLayers(flatten:true)（= Map.GetLayersAsFlattenedList()），不新造解析。
            var byUri = new Dictionary<string, MapMember>(StringComparer.Ordinal);
            foreach (var member in LayerResolver.EnumerateLayers(map, flatten: true).OfType<MapMember>())
            {
                byUri[member.URI] = member;
            }

            // 快照内图层在当前地图不可解析 → **显式报错**（不再静默跳过，避免 restoredCount 与快照不符的静默失败）。
            // 判定逻辑下放到 Core（纯逻辑，单测覆盖）；此处只提供**展平**的 URI 集合。
            var unresolvable = SelectionRestoreResolver.UnresolvableLayerNames(byUri.Keys, content.Layers);
            if (unresolvable.Count > 0)
            {
                return OperationResult<SnapshotRestoreResult>.Fail(
                    ErrorCodes.SelectionSnapshotExpired,
                    $"snapshot layer(s) not resolvable in the current map: {string.Join(", ", unresolvable)}. " +
                    "No selection was changed; re-take the snapshot or restore the missing layer(s).");
            }

            var dict = new Dictionary<MapMember, List<long>>();
            var restored = new List<SelectionLayerContent>();
            foreach (var layerContent in content.Layers)
            {
                if (!byUri.TryGetValue(layerContent.LayerUri, out var member))
                {
                    // 不可达：上面已整体拒绝，此处仅作防御（不得静默跳过）。
                    return OperationResult<SnapshotRestoreResult>.Fail(
                        ErrorCodes.SelectionSnapshotExpired,
                        $"snapshot layer '{layerContent.LayerName}' not resolvable in the current map.");
                }

                dict[member] = layerContent.Oids.ToList();
                restored.Add(layerContent);
            }

            SelectionSet selectionSet = dict.Count == 0
                ? null!
                : SelectionSet.FromDictionary(dict);

            map.SetSelection(selectionSet); // null → 清空（官方文档语义）

            var result = new SnapshotRestoreResult
            {
                SnapshotId = string.Empty,
                RestoredLayers = restored,
                RestoredCount = restored.Sum(l => l.SelectedCount),
            };
            return OperationResult<SnapshotRestoreResult>.Ok(result);
        });

    /// <summary>读取地图选择内容（QueuedTask 内调用）。</summary>
    /// <remarks>
    /// <para>D-060 跨代兼容改造：<b>不再使用 <c>Map.GetSelection()</c> / <c>SelectionSet.ToDictionary()</c></b>。</para>
    /// <para>原因（元数据实测）：该 API 在两代的<b>返回类型不同</b> —— 13.0 返回 <c>SelectionSet</c>（实例方法 <c>ToDictionary</c>），
    /// 13.5 返回 <c>MapMemberIDSet</c>（该类型在 13.0 不存在）。同一份源码在两代编译出的<b>调用目标不同</b>，
    /// 故 net6 单包无法同时兼容（13.0 编译 ⇒ 在 3.5 上调用 <c>SelectionSet::ToDictionary</c> 失败）。</para>
    /// <para>改用两代**共同具备**的 API：<c>Map.GetLayersAsFlattenedList</c> / <c>GetStandaloneTablesAsFlattenedList</c>
    /// ＋ <c>BasicFeatureLayer.GetSelection()</c> / <c>StandaloneTable.GetSelection()</c>（返回 <c>ArcGIS.Core.Data.Selection</c>）
    /// ＋ <c>Selection.GetObjectIDs()</c> —— 已在 13.0 / 13.1 / 13.2 / 13.5 引用集与 Pro 3.5 运行期程序集上逐成员核验存在。</para>
    /// <para>语义等价性：原实现按「地图级选择集字典」逐成员输出；新实现按「图层/独立表的选择对象」逐成员输出，
    /// 均为「有选择集的成员各一条」，空选择集不产出条目 —— 与既有快照/账本语义一致。</para>
    /// </remarks>
    internal static SelectionContent ReadContent(Map map)
    {
        var content = new SelectionContent
        {
            MapName = map.Name ?? string.Empty,
            MapUri = map.URI ?? string.Empty,
        };

        foreach (var layer in map.GetLayersAsFlattenedList().OfType<BasicFeatureLayer>())
        {
            AppendSelectedMember(content, layer, layer.GetSelection());
        }

        foreach (var table in map.GetStandaloneTablesAsFlattenedList().OfType<StandaloneTable>())
        {
            AppendSelectedMember(content, table, table.GetSelection());
        }

        return content;
    }

    /// <summary>把单个成员的选中 OID 集追加到内容（无选择集时跳过）。</summary>
    private static void AppendSelectedMember(SelectionContent content, MapMember member, Selection? selection)
    {
        if (selection is null)
        {
            return;
        }

        var oids = selection.GetObjectIDs().OrderBy(o => o).ToList();
        var truncated = oids.Count > MaxOidsPerLayer;
        content.Layers.Add(new SelectionLayerContent
        {
            LayerName = member.Name ?? string.Empty,
            LayerUri = member.URI ?? string.Empty,
            Oids = truncated ? oids.Take(MaxOidsPerLayer).ToList() : oids,
            SelectedCount = oids.Count, // 真实值（截断前）
            Truncated = truncated,
        });
    }
}
