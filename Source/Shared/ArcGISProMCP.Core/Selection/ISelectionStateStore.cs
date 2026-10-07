using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Selection;

/// <summary>选择账本/快照存储抽象（Phase 8.3；无 SDK 依赖）。</summary>
public interface ISelectionStateStore
{
    /// <summary>写前核对：账本存在且 expectedContent ≠ actual → Mismatch（拒绝）；账本不存在 → FirstSeen（初始化后放行）。</summary>
    SelectionLedgerCheckResult CheckWritePrecondition(string mapUri, SelectionContent actual);

    /// <summary>初始化账本（首次操作该地图），返回 revision=1。</summary>
    long InitializeLedger(string mapUri, SelectionContent actual, string op);

    /// <summary>写后提交：revision 单调递增，expectedContent=写后实测值。</summary>
    long CommitWrite(string mapUri, SelectionContent after, string op);

    /// <summary>重同步基线（设计 §9：用户 UI 改动被拒后，显式重新快照即以实测内容重置 expectedContent；revision 不变——未发生写入）。</summary>
    long ResyncLedger(string mapUri, SelectionContent actual, string op);

    /// <summary>只读场景标记：账本缺失或相符 → true（不拒绝读取）。</summary>
    bool IsLedgerMatch(string mapUri, SelectionContent actual);

    /// <summary>创建快照（LRU 淘汰最旧）。</summary>
    SelectionSnapshotRecord AddSnapshot(string mapUri, string mapName, SelectionContent content, long revisionAtCreation);

    /// <summary>按 id 取快照（不存在/被 LRU 淘汰 → null；命中会触碰 LRU）。</summary>
    SelectionSnapshotRecord? TryGetSnapshot(string snapshotId);

    /// <summary>读取账本 revision（不存在 → false，revision=0）。快照创建时记录诊断用。</summary>
    bool TryGetLedgerRevision(string mapUri, out long revision);

    /// <summary>清除某地图的全部快照（会话清理用）。</summary>
    void RemoveAllForMap(string mapUri);

    /// <summary>当前快照总数（测试/诊断）。</summary>
    int SnapshotCount { get; }
}
