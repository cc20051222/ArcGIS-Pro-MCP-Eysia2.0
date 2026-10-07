using System.Text;

namespace ArcGISProMCP.Core.Models;

/// <summary>
/// 某一地图当前选择集的规范化快照内容（Phase 8.3，D-013）。
/// 以 layerUri 为键规避重名歧义与改名漂移；Oid 升序。
/// </summary>
public sealed class SelectionContent
{
    public string MapName { get; set; } = string.Empty;

    public string MapUri { get; set; } = string.Empty;

    public List<SelectionLayerContent> Layers { get; set; } = new();

    /// <summary>全部图层被选要素总数（不截断的真实值）。</summary>
    public long TotalSelected => Layers.Sum(l => l.SelectedCount);

    /// <summary>layerUri → OID 有序集（供账本比对）。</summary>
    public Dictionary<string, HashSet<long>> ToUriSetMap()
    {
        var map = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
        foreach (var layer in Layers)
        {
            map[layer.LayerUri] = new HashSet<long>(layer.Oids);
        }

        return map;
    }
}

/// <summary>单图层的选择内容。</summary>
public sealed class SelectionLayerContent
{
    public string LayerName { get; set; } = string.Empty;

    public string LayerUri { get; set; } = string.Empty;

    /// <summary>升序 OID（读取侧可能截断，见 Truncated）。</summary>
    public List<long> Oids { get; set; } = new();

    /// <summary>截断前的真实选中数（账本/响应以此为准）。</summary>
    public long SelectedCount { get; set; }

    /// <summary>Oid 是否被读取侧截断（真实 SelectedCount > Oids.Count）。</summary>
    public bool Truncated { get; set; }
}

/// <summary>选择快照记录（服务端内存，会话级，LRU 上限由存储层管理）。</summary>
public sealed class SelectionSnapshotRecord
{
    public string SnapshotId { get; set; } = string.Empty;

    public string MapName { get; set; } = string.Empty;

    public string MapUri { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public SelectionContent Content { get; set; } = new();

    /// <summary>快照时的账本 revision（诊断用）。</summary>
    public long RevisionAtCreation { get; set; }
}

/// <summary>恢复结果（设计 §7.3：恢复成员 ≠ 恢复原层序）。</summary>
public sealed class SnapshotRestoreResult
{
    public string SnapshotId { get; set; } = string.Empty;

    public List<SelectionLayerContent> RestoredLayers { get; set; } = new();

    public long RestoredCount { get; set; }

    /// <summary>固定 true：仅恢复选择成员，不恢复层序/可见性/视图。</summary>
    public bool RestoresSelectionOnly => true;
}

/// <summary>账本比对结果。</summary>
public sealed class SelectionLedgerCheckResult
{
    public bool Matched { get; init; }

    /// <summary>账本是否存在（false = 本会话首次操作该地图，初始化后放行）。</summary>
    public bool LedgerExists { get; init; }

    /// <summary>不符时的 diff 摘要（错误消息用，已截断到安全长度）。</summary>
    public string? DiffSummary { get; init; }

    public long Revision { get; init; }

    public static SelectionLedgerCheckResult FirstSeen() => new() { Matched = true, LedgerExists = false };

    public static SelectionLedgerCheckResult Match(long revision) => new() { Matched = true, LedgerExists = true, Revision = revision };

    public static SelectionLedgerCheckResult Mismatch(string diffSummary, long revision) =>
        new() { Matched = false, LedgerExists = true, DiffSummary = diffSummary, Revision = revision };

    /// <summary>生成两份内容的 diff 摘要（安全长度，供错误消息）。</summary>
    public static string BuildDiffSummary(SelectionContent expected, SelectionContent actual)
    {
        var sb = new StringBuilder();
        var exp = expected.ToUriSetMap();
        var act = actual.ToUriSetMap();
        foreach (var uri in exp.Keys.Union(act.Keys).OrderBy(u => u, StringComparer.Ordinal))
        {
            exp.TryGetValue(uri, out var e);
            act.TryGetValue(uri, out var a);
            var eCount = e?.Count ?? 0;
            var aCount = a?.Count ?? 0;
            if (eCount != aCount || (e is not null && a is not null && !e.SetEquals(a)))
            {
                if (sb.Length > 0)
                {
                    sb.Append("; ");
                }

                sb.Append(uri).Append(": expected=").Append(eCount).Append(" actual=").Append(aCount);
                if (sb.Length > 280)
                {
                    sb.Append("; …");
                    break;
                }
            }
        }

        return sb.Length == 0 ? "selection differs from ledger expectation." : "ledger baseline mismatch — " + sb;
    }
}
