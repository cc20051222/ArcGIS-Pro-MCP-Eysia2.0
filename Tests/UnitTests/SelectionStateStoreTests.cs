using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Selection;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 8.3 (D-013)：选择账本/快照存储状态机单测（纯 Core，无 SDK）。
/// 覆盖：首次放行/核对相符/不符+diff/revision 单调/LRU 淘汰/惰性过期语义。
/// </summary>
public sealed class SelectionStateStoreTests
{
    private static SelectionContent Content(string mapUri, params (string Uri, long[] Oids)[] layers)
    {
        var c = new SelectionContent { MapName = "M", MapUri = mapUri };
        foreach (var (uri, oids) in layers)
        {
            c.Layers.Add(new SelectionLayerContent
            {
                LayerName = uri,
                LayerUri = uri,
                Oids = oids.OrderBy(o => o).ToList(),
                SelectedCount = oids.Length,
            });
        }

        return c;
    }

    [Fact]
    public void FirstOperationOnMap_IsAllowedAndInitializesAtRevisionOne()
    {
        var store = new SelectionStateStore();
        var actual = Content("map://a", ("l://p", new long[] { 1, 2 }));

        var check = store.CheckWritePrecondition("map://a", actual);
        Assert.True(check.Matched);
        Assert.False(check.LedgerExists);

        Assert.Equal(1, store.InitializeLedger("map://a", actual, "op"));
        var again = store.CheckWritePrecondition("map://a", actual);
        Assert.True(again.Matched);
        Assert.True(again.LedgerExists);
        Assert.Equal(1, again.Revision);
    }

    [Fact]
    public void UserModificationBetweenOperations_IsRefusedWithDiffSummary()
    {
        var store = new SelectionStateStore();
        var baseline = Content("map://a", ("l://p", new long[] { 1, 2, 3 }));
        store.InitializeLedger("map://a", baseline, "op");

        var modified = Content("map://a", ("l://p", new long[] { 7 }));
        var check = store.CheckWritePrecondition("map://a", modified);

        Assert.False(check.Matched);
        Assert.NotNull(check.DiffSummary);
        Assert.Contains("expected=3", check.DiffSummary);
        Assert.Contains("actual=1", check.DiffSummary);
    }

    [Fact]
    public void CommitWrite_MonotonicallyIncreasesRevision()
    {
        var store = new SelectionStateStore();
        var c0 = Content("map://a", ("l://p", new long[] { 1 }));
        store.InitializeLedger("map://a", c0, "init");

        var r1 = store.CommitWrite("map://a", Content("map://a", ("l://p", new long[] { 1, 2 })), "select");
        var r2 = store.CommitWrite("map://a", Content("map://a", ("l://p", new long[] { 2 })), "clear");

        Assert.Equal(2, r1);
        Assert.Equal(3, r2);
    }

    [Fact]
    public void ContentEquals_IgnoresOidOrderButNotMembership()
    {
        var a = Content("m", ("l://p", new long[] { 1, 2, 3 }));
        var b = Content("m", ("l://p", new long[] { 3, 1, 2 }));
        var c = Content("m", ("l://p", new long[] { 1, 2, 4 }));

        Assert.True(SelectionStateStore.ContentEquals(a, b));
        Assert.False(SelectionStateStore.ContentEquals(a, c));
    }

    [Fact]
    public void ResyncLedger_ResetsExpectedContent_KeepsRevision()
    {
        var store = new SelectionStateStore();
        var c0 = Content("map://a", ("l://p", new long[] { 1 }));
        store.InitializeLedger("map://a", c0, "init");
        store.CommitWrite("map://a", Content("map://a", ("l://p", new long[] { 2 })), "select");

        // 用户 UI 改动 → 实测 ≠ 账本 → 拒绝。
        var userState = Content("map://a", ("l://p", new long[] { 9 }));
        Assert.False(store.CheckWritePrecondition("map://a", userState).Matched);

        // 显式重新快照 = 重同步（revision 不变，expected=实测）。
        var rev = store.ResyncLedger("map://a", userState, "create_selection_snapshot");
        Assert.Equal(2, rev);
        Assert.True(store.CheckWritePrecondition("map://a", userState).Matched);
    }

    [Fact]
    public void SnapshotLru_EvictsOldestBeyondCapacity()
    {
        var store = new SelectionStateStore(2);
        var content = Content("map://a");
        var s1 = store.AddSnapshot("map://a", "M", content, 1);
        var s2 = store.AddSnapshot("map://a", "M", content, 1);
        var s3 = store.AddSnapshot("map://a", "M", content, 1);

        Assert.Null(store.TryGetSnapshot(s1.SnapshotId));
        Assert.NotNull(store.TryGetSnapshot(s2.SnapshotId));
        Assert.NotNull(store.TryGetSnapshot(s3.SnapshotId));
    }

    [Fact]
    public void TryGetSnapshot_TouchesLruOrder()
    {
        var store = new SelectionStateStore(2);
        var content = Content("map://a");
        var s1 = store.AddSnapshot("map://a", "M", content, 1);
        var s2 = store.AddSnapshot("map://a", "M", content, 1);

        // 触碰 s1 使其变新 → 下一次插入应淘汰 s2。
        Assert.NotNull(store.TryGetSnapshot(s1.SnapshotId));
        var s3 = store.AddSnapshot("map://a", "M", content, 1);

        Assert.NotNull(store.TryGetSnapshot(s1.SnapshotId));
        Assert.Null(store.TryGetSnapshot(s2.SnapshotId));
        Assert.NotNull(store.TryGetSnapshot(s3.SnapshotId));
    }

    [Fact]
    public void RemoveAllForMap_RemovesOnlyThatMap()
    {
        var store = new SelectionStateStore();
        var s1 = store.AddSnapshot("map://a", "A", Content("map://a"), 1);
        var s2 = store.AddSnapshot("map://b", "B", Content("map://b"), 1);

        store.RemoveAllForMap("map://a");

        Assert.Null(store.TryGetSnapshot(s1.SnapshotId));
        Assert.NotNull(store.TryGetSnapshot(s2.SnapshotId));
    }

    [Fact]
    public void IsLedgerMatch_MissingLedgerOrMatchingContent_ReturnsTrue()
    {
        var store = new SelectionStateStore();
        var c = Content("map://a", ("l://p", new long[] { 1 }));

        Assert.True(store.IsLedgerMatch("map://a", c));
        store.InitializeLedger("map://a", c, "op");
        Assert.True(store.IsLedgerMatch("map://a", c));
        Assert.False(store.IsLedgerMatch("map://a", Content("map://a", ("l://p", new long[] { 9 }))));
    }
}
