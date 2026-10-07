using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>地理处理服务。</summary>
public interface IGeoprocessingService
{
    Task<OperationResult<GeoprocessingResult>> RunToolAsync(GeoprocessingRequest request, CancellationToken ct = default);

    // Phase 8.5.6 (D-021 / F6)：写工具进入 GP 之前的输出存在性探测（覆写前置判定用）。
    /// <summary>
    /// 判定输出是否已存在：文件型 → 文件系统语义；<c>.gdb</c> 容器内 → SDK Geodatabase 定义枚举。
    /// 返回 Ok + <see cref="OutputExistence"/>（判定不可得 → <see cref="OutputExistence.Unknown"/>，Message 携带原因）；
    /// 探测本身异常 → Fail。调用方（工具）据 <see cref="OverwritePolicy"/> 保守决策。
    /// </summary>
    Task<OperationResult<OutputExistence>> CheckOutputExistsAsync(string outputPath, CancellationToken ct = default);

    // Phase 8.3 (D-014 方案 A)：选择写入走 Pro 进程内 SDK GP（SelectLayerByAttribute/Location）。
    /// <summary>按属性/OID 选择（oidList 与 where 二选一由调用方保证；mode=replace|add|remove|switch）。</summary>
    Task<OperationResult<JsonElement?>> SelectLayerByAttributeAsync(
        string? mapName, string layerName, string mode,
        IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default);

    /// <summary>按空间关系选择（17 overlap 白名单由调用方保证；mode=replace|add|remove）。</summary>
    Task<OperationResult<JsonElement?>> SelectLayerByLocationAsync(
        string? mapName, string layerName, string? selectingLayerName,
        string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default);

    // ── D-062 · 受控 GP 通用调用（旗舰；新成员一律默认实现，保既有实现零改动编译）──

    /// <summary>
    /// D-062 · A1：受控白名单 GP 通用入口。宿主职责：白名单校验（白名单外 INVALID_ARGUMENT）、
    /// 破坏性 confirm 缺省拒、输出/输入守卫、输出存在性闸门、执行、审计落盘、最近消息记录。
    /// </summary>
    Task<OperationResult<GpRunResult>> RunWhitelistedAsync(GpRunRequest request, CancellationToken ct = default)
        => Task.FromResult(OperationResult<GpRunResult>.Fail(
            ErrorCodes.NotImplemented, "RunWhitelistedAsync is not implemented by this host."));

    /// <summary>D-062 · A2/A3：当前受控白名单（宿主负责 env → 程序集旁 → 嵌入资源 解析链）。</summary>
    Task<OperationResult<GpWhitelist>> GetWhitelistAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<GpWhitelist>.Fail(
            ErrorCodes.NotImplemented, "GetWhitelistAsync is not implemented by this host."));

    /// <summary>D-062 · A4：最近一次 GP 执行的消息快照（从未执行 → Ok + 全 null/空）。</summary>
    Task<OperationResult<GpMessagesInfo>> GetLastMessagesAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<GpMessagesInfo>.Ok(new GpMessagesInfo()));

    // ── D-064 · B 段：GP 环境设置（get_environment / set_environment；会话级 + 审计）──

    /// <summary>D-064：读取当前 GP 环境设置（workspace / 输出 CS / extent / mask / cellSize / overwriteOutput / parallel…）。</summary>
    /// <remarks>默认实现返回 NOT_IMPLEMENTED（保既有宿主实现零改动编译）。</remarks>
    Task<OperationResult<GpEnvironmentInfo>> GetEnvironmentAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<GpEnvironmentInfo>.Fail(
            ErrorCodes.NotImplemented, "GP environment read is not implemented by this host."));

    /// <summary>
    /// D-064：设置 GP 环境（**会话级**：影响后续 GP / 受控调用，不落盘、进程结束失效）。
    /// <paramref name="reset"/> = true ⇒ 还原到**会话初始快照**（首见快照）。
    /// 未提供的键保持不动；返回前后照。
    /// </summary>
    /// <remarks>默认实现返回 NOT_IMPLEMENTED（保既有宿主实现零改动编译）。</remarks>
    Task<OperationResult<SetEnvironmentResult>> SetEnvironmentAsync(
        GpEnvironmentInfo? desired, bool reset, CancellationToken ct = default)
        => Task.FromResult(OperationResult<SetEnvironmentResult>.Fail(
            ErrorCodes.NotImplemented, "GP environment write is not implemented by this host."));
}
