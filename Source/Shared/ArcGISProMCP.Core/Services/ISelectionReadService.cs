using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// 选择集读取/快照/恢复服务（Phase 8.3，Native 侧；10 ADR：读与快照归 Native）。
/// 空白 mapName 语义循 G-22（活动地图）；无活动视图 → NO_ACTIVE_VIEW。
/// </summary>
public interface ISelectionReadService
{
    /// <summary>读取地图当前选择内容（OID 级，逐图层）。OID 读取上限 10,000/图层（截断+SelectedCount 真实值）。</summary>
    Task<OperationResult<SelectionContent>> GetSelectionContentAsync(string? mapName, CancellationToken ct = default);

    /// <summary>按 mapUri 读取（重名地图场景专用，D-014：快照恢复的账本核对走此路径）。</summary>
    Task<OperationResult<SelectionContent>> GetSelectionContentByUriAsync(string mapUri, CancellationToken ct = default);

    /// <summary>创建选择快照（读 + 存入账本存储）。</summary>
    Task<OperationResult<SelectionSnapshotRecord>> CreateSnapshotAsync(string? mapName, CancellationToken ct = default);

    /// <summary>把快照内容应用回地图（SetSelection）。调用方负责账本核对；恢复后调用方须再读并 CommitWrite。</summary>
    Task<OperationResult<SnapshotRestoreResult>> RestoreSnapshotAsync(string mapName, SelectionContent content, CancellationToken ct = default);
}
