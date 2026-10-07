using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-064（功能完善第四批）· B 段：GP 环境设置 —— <c>GeoprocessingService</c> 分部实现。
/// 会话环境本体见 <see cref="D064GpEnvironment"/>（每次 GP 调用下发为环境数组 ⇒ 真正影响后续运行）。
/// <c>set_environment</c> 每次写一条**审计附注**（复用 D-062 的 <see cref="GpAuditLog"/>，jsonl，append-only）。
/// </summary>
public sealed partial class GeoprocessingService
{
    public Task<OperationResult<GpEnvironmentInfo>> GetEnvironmentAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<GpEnvironmentInfo>.Ok(D064GpEnvironment.Snapshot()));

    public Task<OperationResult<SetEnvironmentResult>> SetEnvironmentAsync(
        GpEnvironmentInfo? desired, bool reset, CancellationToken ct = default)
    {
        var result = D064GpEnvironment.Apply(desired, reset);

        // 审计附注（G-138：审计目录解析失败 ⇒ 不阻断，但 index=0 如实披露）。
        try
        {
            var path = GpAuditLog.ResolvePath();
            result.AuditPath = path;
            var entry = new GpAuditEntry
            {
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                Tool = "set_environment",
                ParameterDigest = "applied=[" + string.Join(",", result.Applied) + "]; reset=" + reset,
                ParameterForm = "named",
                Destructive = false,
                Confirm = false,
                Success = true,
                ResultCode = "OK",
                DurationMs = 0,
                AuditNote = "GP environment (session-scoped): " + result.Scope,
                Pid = Environment.ProcessId,
            };

            result.AuditEntryIndex = GpAuditLog.TryAppend(path, entry, out var index, out _) ? index : 0;
        }
        catch (Exception ex)
        {
            result.AuditNote = "audit not written: " + ex.Message;
        }

        return Task.FromResult(OperationResult<SetEnvironmentResult>.Ok(result));
    }
}
