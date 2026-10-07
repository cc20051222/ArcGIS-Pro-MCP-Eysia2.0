using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-062 · B 段编辑栈服务（EditOperation 撤销栈）。
/// <b>事务铁律（工单强制）</b>：B1–B3 默认<b>不自动提交</b> —— 编辑进 EditOperation 撤销栈，
/// 由调用方经 <see cref="SaveEditsAsync"/> / <see cref="DiscardEditsAsync"/> 显式收束；跨调用状态保持。
/// 目标守卫：目标图层的底层数据集命中受保护根 → PATH_ESCAPE_REJECTED（零变更）。
/// </summary>
public interface IEditService
{
    /// <summary>
    /// B1：向图层/表插入行。行字典键 = 字段名（值为字符串/数值/布尔；geometry 以 WKT 字符串给）。
    /// 默认不提交。
    /// </summary>
    Task<OperationResult<EditOpResult>> InsertFeaturesAsync(
        string? mapName, string layerName,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        CancellationToken ct = default)
        => Task.FromResult(OperationResult<EditOpResult>.Fail(
            ErrorCodes.NotImplemented, "InsertFeaturesAsync is not implemented by this host."));

    /// <summary>
    /// B2：按 where（或 OID 集）更新属性/几何（geometryWKT 提供时一并改形）。
    /// ⚠️ 破坏性：调用方必须显式 confirm=true（缺省拒，工单强制）。默认不提交。
    /// </summary>
    Task<OperationResult<EditOpResult>> UpdateFeaturesAsync(
        string? mapName, string layerName,
        string? where, IReadOnlyList<long>? oidList,
        IReadOnlyDictionary<string, object?>? attributes, string? geometryWkt,
        bool confirm, CancellationToken ct = default)
        => Task.FromResult(OperationResult<EditOpResult>.Fail(
            ErrorCodes.NotImplemented, "UpdateFeaturesAsync is not implemented by this host."));

    /// <summary>
    /// B3：按 where（或 OID 集）删除行。
    /// ⚠️ 破坏性：调用方必须显式 confirm=true（缺省拒，工单强制）。默认不提交。
    /// </summary>
    Task<OperationResult<EditOpResult>> DeleteFeaturesAsync(
        string? mapName, string layerName,
        string? where, IReadOnlyList<long>? oidList,
        bool confirm, CancellationToken ct = default)
        => Task.FromResult(OperationResult<EditOpResult>.Fail(
            ErrorCodes.NotImplemented, "DeleteFeaturesAsync is not implemented by this host."));

    /// <summary>B4：提交当前编辑会话（就地写 ⇒ 输入守卫在会话建立时已判）。</summary>
    Task<OperationResult<EditSessionState>> SaveEditsAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<EditSessionState>.Fail(
            ErrorCodes.NotImplemented, "SaveEditsAsync is not implemented by this host."));

    /// <summary>B5：丢弃当前编辑会话（撤销栈回退；非磁盘写）。</summary>
    Task<OperationResult<EditSessionState>> DiscardEditsAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<EditSessionState>.Fail(
            ErrorCodes.NotImplemented, "DiscardEditsAsync is not implemented by this host."));

    /// <summary>B6：查询当前未提交编辑状态（计数/涉及图层）—— 只读；超 Knight60 能力件。</summary>
    Task<OperationResult<EditSessionState>> GetEditSessionAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<EditSessionState>.Fail(
            ErrorCodes.NotImplemented, "GetEditSessionAsync is not implemented by this host."));
}
