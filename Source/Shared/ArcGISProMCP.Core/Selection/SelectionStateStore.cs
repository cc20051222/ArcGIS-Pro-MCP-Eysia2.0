using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Selection;

/// <summary>
/// 选择账本与快照存储（Phase 8.3，D-013；纯 .NET，无 SDK 依赖，Rule 5）。
/// 设计 §7.2：per-map Ledger{revision, expectedContent, lastOp}；快照 LRU 8 / 会话级 / 惰性判定。
/// 线程安全：全部操作持锁（MCP 工具执行为串行单飞，锁竞争可忽略）。
/// </summary>
public sealed class SelectionStateStore : ISelectionStateStore
{
    public const int DefaultMaxSnapshots = 8;

    private readonly object _gate = new();
    private readonly int _maxSnapshots;
    private readonly Dictionary<string, LedgerEntry> _ledgers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SelectionSnapshotRecord> _snapshots = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _snapshotLru = new();
    private long _snapshotSeq;

    public SelectionStateStore(int maxSnapshots = DefaultMaxSnapshots)
    {
        if (maxSnapshots < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSnapshots));
        }

        _maxSnapshots = maxSnapshots;
    }

    private sealed class LedgerEntry
    {
        public long Revision;
        public SelectionContent Content = new();
        public string LastOp = string.Empty;
    }

    /// <inheritdoc />
    public SelectionLedgerCheckResult CheckWritePrecondition(string mapUri, SelectionContent actual)
    {
        if (string.IsNullOrWhiteSpace(mapUri))
        {
            throw new ArgumentException("mapUri is required.", nameof(mapUri));
        }

        ArgumentNullException.ThrowIfNull(actual);

        lock (_gate)
        {
            if (!_ledgers.TryGetValue(mapUri, out var entry))
            {
                return SelectionLedgerCheckResult.FirstSeen();
            }

            if (ContentEquals(entry.Content, actual))
            {
                return SelectionLedgerCheckResult.Match(entry.Revision);
            }

            return SelectionLedgerCheckResult.Mismatch(
                SelectionLedgerCheckResult.BuildDiffSummary(entry.Content, actual),
                entry.Revision);
        }
    }

    /// <inheritdoc />
    public long InitializeLedger(string mapUri, SelectionContent actual, string op)
    {
        lock (_gate)
        {
            _ledgers[mapUri] = new LedgerEntry
            {
                Revision = 1,
                Content = actual,
                LastOp = op,
            };
            return 1;
        }
    }

    /// <inheritdoc />
    public long CommitWrite(string mapUri, SelectionContent after, string op)
    {
        lock (_gate)
        {
            var revision = _ledgers.TryGetValue(mapUri, out var entry) ? entry.Revision + 1 : 1;
            _ledgers[mapUri] = new LedgerEntry
            {
                Revision = revision,
                Content = after,
                LastOp = op,
            };
            return revision;
        }
    }

    /// <inheritdoc />
    public long ResyncLedger(string mapUri, SelectionContent actual, string op)
    {
        lock (_gate)
        {
            var revision = _ledgers.TryGetValue(mapUri, out var entry) ? entry.Revision : 1;
            _ledgers[mapUri] = new LedgerEntry
            {
                Revision = revision,
                Content = actual,
                LastOp = op,
            };
            return revision;
        }
    }

    /// <inheritdoc />
    public bool IsLedgerMatch(string mapUri, SelectionContent actual)
    {
        lock (_gate)
        {
            return !_ledgers.TryGetValue(mapUri, out var entry) || ContentEquals(entry.Content, actual);
        }
    }

    /// <inheritdoc />
    public SelectionSnapshotRecord AddSnapshot(string mapUri, string mapName, SelectionContent content, long revisionAtCreation)
    {
        lock (_gate)
        {
            var id = "sel-snap-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture)
                     + "-" + System.Threading.Interlocked.Increment(ref _snapshotSeq).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var record = new SelectionSnapshotRecord
            {
                SnapshotId = id,
                MapName = mapName,
                MapUri = mapUri,
                CreatedAtUtc = DateTime.UtcNow,
                Content = content,
                RevisionAtCreation = revisionAtCreation,
            };
            _snapshots[id] = record;
            _snapshotLru.AddFirst(id);

            while (_snapshotLru.Count > _maxSnapshots)
            {
                var oldest = _snapshotLru.Last!.Value;
                _snapshotLru.RemoveLast();
                _snapshots.Remove(oldest);
            }

            return record;
        }
    }

    /// <inheritdoc />
    public SelectionSnapshotRecord? TryGetSnapshot(string snapshotId)
    {
        lock (_gate)
        {
            if (!_snapshots.TryGetValue(snapshotId, out var record))
            {
                return null;
            }

            // 惰性 LRU 触碰。
            _snapshotLru.Remove(snapshotId);
            _snapshotLru.AddFirst(snapshotId);
            return record;
        }
    }

    /// <inheritdoc />
    public int SnapshotCount
    {
        get
        {
            lock (_gate)
            {
                return _snapshots.Count;
            }
        }
    }

    /// <inheritdoc />
    public bool TryGetLedgerRevision(string mapUri, out long revision)
    {
        lock (_gate)
        {
            if (_ledgers.TryGetValue(mapUri, out var entry))
            {
                revision = entry.Revision;
                return true;
            }

            revision = 0;
            return false;
        }
    }

    /// <inheritdoc />
    public void RemoveAllForMap(string mapUri)
    {
        lock (_gate)
        {
            var doomed = _snapshots.Values.Where(s => string.Equals(s.MapUri, mapUri, StringComparison.Ordinal)).Select(s => s.SnapshotId).ToList();
            foreach (var id in doomed)
            {
                _snapshots.Remove(id);
                _snapshotLru.Remove(id);
            }
        }
    }

    /// <summary>集合级相等：同键集合、同 OID 集（忽略顺序）。</summary>
    public static bool ContentEquals(SelectionContent a, SelectionContent b)
    {
        var ma = a.ToUriSetMap();
        var mb = b.ToUriSetMap();
        if (ma.Count != mb.Count)
        {
            return false;
        }

        foreach (var (uri, setA) in ma)
        {
            if (!mb.TryGetValue(uri, out var setB) || !setA.SetEquals(setB))
            {
                return false;
            }
        }

        return true;
    }
}
