using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// D-062 · A 段：受控 GP 通用调用 + 元工具 + 审计查询（6 件＝本段工具数，非白名单件数）。
/// <b>安全架构（工单强制）</b>：run_geoprocessing 只执行受控白名单内的工具；白名单唯一权威源＝
/// <c>Config/gp-whitelist.json</c>（embedded fallback），条目数随该文件浮动，**本注释与任何文案均不得硬编码件数**
/// （D-125 O-D116-01：件数一律由权威源现算，守卫测试钉死本块不得出现白名单件数字面量）；
/// 白名单外 → INVALID_ARGUMENT（消息明示「不在受控白名单」）；**禁止任何绕过形态**。
/// 破坏性工具（destructive=true）必须 confirm=true（缺省拒）。每次调用审计落盘（jsonl，G-138 禁 %TEMP%）。
/// </summary>
public static class GpControlNotes
{
    public const string WhitelistNote =
        "Whitelist source of truth: Config/gp-whitelist.json (embedded fallback). " +
        "Forbidden categories are never listed: delete/rename/overwrite-class GP, workspace management, environment tampering. " +
        "Whitelist expansion is NOT part of this batch (Keeper approval required).";
}

/// <summary>A1 · run_geoprocessing（本批核心安全敏感件）。</summary>
public sealed class RunGeoprocessingTool : McpToolBase
{
    public override string Name => "run_geoprocessing";

    public override string Description =>
        "**受控 GP 通用调用（白名单版，本批旗舰）**。参数：tool（GP 工具点串，如 `analysis.Buffer`，**必须在白名单内**）、" +
        "parameters（命名参数字典，键 = 白名单参数名）或 positionalValues（按参数序的字符串数组，二选一）、" +
        "confirm（destructive=true 的工具**必须显式 true，缺省拒**）、auditNote（可选调用说明，写入审计）、auditDir（可选审计目录覆盖）。" +
        "执行纪律：① 白名单外 → `INVALID_ARGUMENT`（消息明示「不在受控白名单」），**禁止任何绕过形态**；" +
        "② 破坏性类（如 management.CalculateField / management.RepairGeometry / analysis.Near —— 就地改输入）confirm 缺省拒；" +
        "③ 输出/输入守卫全接入：任一参数值命中受保护根（TestFixtures／旧仓库／ARCGIS_PRO_MCP_PROTECTED_ROOTS）→ " +
        "`PATH_ESCAPE_REJECTED`（零变更；multivalue 按分隔符逐项判界）；" +
        "④ 输出存在性闸门：输出已存在 → `OUTPUT_EXISTS`（v1 **不提供覆写语义**，如实披露）；" +
        "⑤ 每次调用**审计落盘**（gp-audit.jsonl：时间/工具/参数摘要/形态/时长/结果码/auditNote；目录默认程序集旁 gp-audit/，" +
        "可用 ARCGIS_PRO_MCP_GP_AUDIT_DIR 或 auditDir 覆盖；**禁 %TEMP%（G-138）**；写失败不阻断结果但以 auditEntryIndex=0 明示）；" +
        "⑥ 参数序以白名单元数据为准（A/B 实测在 LIVE 复核，见交付报告）。" +
        "GP 失败 → `GEOPROCESSING_ERROR`（消息透传 GP 报文）；错误码 33 零新增。" +
        "白名单枚举与参数签名见 list_geoprocessing_tools / describe_geoprocessing_tool。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["tool"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "GP tool name in alias form (e.g. analysis.Buffer); must be whitelisted." },
            ["parameters"] = new Dictionary<string, object?> { ["type"] = "object", ["description"] = "Named parameters keyed by whitelist parameter name (preferred form)." },
            ["positionalValues"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "string" }, ["description"] = "Positional values in whitelist parameter order (alternative to parameters)." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Must be explicitly true for destructive tools; default-refuse otherwise." },
            ["auditNote"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional caller note recorded in the audit log." },
            ["auditDir"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional audit directory override (never %TEMP%, G-138)." },
        },
        ["required"] = new[] { "tool" },
    };

    protected override string CategoryName => ToolCategories.Geoprocessing;

    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var tool = ToolArgs.GetString(context, "tool");
        if (string.IsNullOrWhiteSpace(tool))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "tool is required (e.g. analysis.Buffer).");
        }

        var request = new GpRunRequest
        {
            ToolName = tool.Trim(),
            Confirm = ToolArgs.GetBool(context, "confirm"),
            AuditNote = ToolArgs.GetString(context, "auditNote"),
            AuditPath = ToolArgs.GetString(context, "auditDir"),
        };

        if (context.Arguments is not null && context.Arguments.TryGetValue("parameters", out var namedRaw))
        {
            var dict = JsonHelpers.ToNamedDictionary(namedRaw);
            if (dict is null)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "parameters must be an object (name → value).");
            }

            request.Parameters = dict;
        }

        if (context.Arguments is not null && context.Arguments.TryGetValue("positionalValues", out var posRaw))
        {
            var list = JsonHelpers.ToStringList(posRaw);
            if (list is null)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "positionalValues must be an array of strings.");
            }

            request.PositionalValues = list;
        }

        var result = await context.Host.Geoprocessing.RunWhitelistedAsync(request, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>A2 · list_geoprocessing_tools（白名单内枚举；白名单外永不暴露）。</summary>
public sealed class ListGeoprocessingToolsTool : McpToolBase
{
    public override string Name => "list_geoprocessing_tools";

    public override string Description =>
        "**枚举受控白名单内的 GP 工具**（run_geoprocessing 可执行集）。参数：filter（对工具名/用途做子串过滤，可选）、" +
        "category（工具箱前缀过滤：analysis / management / conversion / sa，可选）。" +
        "返回：tool / destructive（破坏性标记）/ notes（一句话用途）/ parameterCount，以及白名单来源与总数。" +
        "**白名单外工具永不出现**（安全架构：不暴露可执行面之外的任何 GP）；白名单扩充不在本批内（需 Keeper + 用户批准）。" +
        "只读；零 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["filter"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional case-insensitive substring filter over tool name and notes." },
            ["category"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional toolbox-prefix filter (analysis/management/conversion/sa)." },
        },
    };

    protected override string CategoryName => ToolCategories.Geoprocessing;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var wl = await context.Host.Geoprocessing.GetWhitelistAsync(context.CancellationToken).ConfigureAwait(false);
        if (!wl.Success || wl.Data is null)
        {
            return ToolResult.From(wl);
        }

        var filter = ToolArgs.GetString(context, "filter");
        var category = ToolArgs.GetString(context, "category");

        var rows = wl.Data.Entries
            .Where(e => category is null || string.IsNullOrWhiteSpace(category)
                ? true
                : e.Tool.StartsWith(category.Trim() + ".", StringComparison.OrdinalIgnoreCase))
            .Where(e => filter is null || string.IsNullOrWhiteSpace(filter)
                ? true
                : e.Tool.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)
                  || e.Notes.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(e => new Dictionary<string, object?>
            {
                ["tool"] = e.Tool,
                ["destructive"] = e.Destructive,
                ["notes"] = e.Notes,
                ["parameterCount"] = e.Parameters.Count,
            })
            .ToList();

        return OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["count"] = rows.Count,
            ["totalWhitelisted"] = wl.Data.Entries.Count,
            ["whitelistSource"] = wl.Data.Source,
            ["securityNote"] = GpControlNotes.WhitelistNote,
            ["tools"] = rows,
        });
    }
}

/// <summary>A3 · describe_geoprocessing_tool（白名单内参数签名）。</summary>
public sealed class DescribeGeoprocessingToolTool : McpToolBase
{
    public override string Name => "describe_geoprocessing_tool";

    public override string Description =>
        "**返回白名单内 GP 工具的参数签名**（供 run_geoprocessing 组参）。参数：tool（必填，白名单内点串）。" +
        "返回：tool / destructive / notes / parameters[]（name / direction(input|output) / required / type / default）。" +
        "direction=output 的参数受输出守卫与 OUTPUT_EXISTS 闸门约束；白名单外 → INVALID_ARGUMENT（不泄露存在性以外信息：消息明示不在白名单）。" +
        "签名来自白名单元数据（Pro 3.5 arcpy 形参序；A/B 实测在 LIVE 复核）。只读；零 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["tool"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Whitelisted GP tool name (e.g. analysis.Buffer)." },
        },
        ["required"] = new[] { "tool" },
    };

    protected override string CategoryName => ToolCategories.Geoprocessing;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var tool = ToolArgs.GetString(context, "tool");
        if (string.IsNullOrWhiteSpace(tool))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "tool is required.");
        }

        var wl = await context.Host.Geoprocessing.GetWhitelistAsync(context.CancellationToken).ConfigureAwait(false);
        if (!wl.Success || wl.Data is null)
        {
            return ToolResult.From(wl);
        }

        var entry = wl.Data.Find(tool);
        if (entry is null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                $"'{tool}' is not in the controlled GP whitelist (受控白名单外). Use list_geoprocessing_tools for the allowed set.");
        }

        return OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["tool"] = entry.Tool,
            ["destructive"] = entry.Destructive,
            ["notes"] = entry.Notes,
            ["whitelistSource"] = wl.Data.Source,
            ["parameters"] = entry.Parameters.Select(p => new Dictionary<string, object?>
            {
                ["name"] = p.Name,
                ["direction"] = p.Direction,
                ["required"] = p.Required,
                ["type"] = p.Type,
                ["default"] = p.Default,
            }).ToList(),
        });
    }
}

/// <summary>A4 · get_messages（最近一次 GP 执行消息）。</summary>
public sealed class GetMessagesTool : McpToolBase
{
    public override string Name => "get_messages";

    public override string Description =>
        "**返回最近一次 GP 执行的消息**（含 run_geoprocessing 与既有命名 GP 工具的最后一次调用）。" +
        "无参数。返回：lastCallAtUtc / toolName / success / messages[]（severity=info）/ errorMessages[]（severity=error）" +
        "/ severityBasis（口径披露：Executor 双列表，error = ErrorMessages 非空）。" +
        "从未执行过 → Ok + 全空（不臆造）。只读；零 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>(),
    };

    protected override string CategoryName => ToolCategories.Geoprocessing;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var result = await context.Host.Geoprocessing.GetLastMessagesAsync(context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>A5 · check_extension（扩展许可检查/签出）。</summary>
public sealed class CheckExtensionTool : McpToolBase
{
    public override string Name => "check_extension";

    public override string Description =>
        "**扩展许可检查**（Spatial Analyst 等）。参数：extensionCode（必填，SDK 许可码如 `SpatialAnalyst`）、" +
        "checkout（可选，true = 尝试签出；缺省只查可用性）。" +
        "返回：code / available（可用许可数 > 0）/ checkoutAttempted / checkoutSucceeded（未尝试 → null）。" +
        "签出失败**不抛错**（Ok + checkoutSucceeded=false + 原因），由调用方判定；未知许可码 → INVALID_ARGUMENT。只读 GP；错误码零新增。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["extensionCode"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "SDK extension code (e.g. SpatialAnalyst)." },
            ["checkout"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "true = attempt checkout; default false (availability only)." },
        },
        ["required"] = new[] { "extensionCode" },
    };

    protected override string CategoryName => ToolCategories.System;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var code = ToolArgs.GetString(context, "extensionCode");
        if (string.IsNullOrWhiteSpace(code))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "extensionCode is required.");
        }

        var result = await context.Host.License.CheckExtensionDetailedAsync(
            code.Trim(), ToolArgs.GetBool(context, "checkout"), context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>get_audit_log（G-166 支柱一·信任层：审计查询，只读、不可篡改）。</summary>
public sealed class GetAuditLogTool : McpToolBase
{
    private const int MaxLimit = 500;

    public override string Name => "get_audit_log";

    public override string Description =>
        "**查询受控 GP 调用审计**（gp-audit.jsonl；G-166 支柱一·信任层，竞品零覆盖）。参数：tool（按工具名过滤，可选）、" +
        "success（结果过滤 true/false，可选）、fromUtc / toUtc（时间窗 ISO-8601，可选）、limit（条数上限，默认 50、上限 500）、" +
        "auditDir（可选审计目录覆盖）。返回：结构化条目（timestampUtc/tool/parameterDigest/parameterForm/destructive/confirm/" +
        "success/resultCode/durationMs/auditNote/pid）+ totalEntries/matchedEntries/corruptLines（损坏行跳过计数，不抛错）+ truncated。" +
        "审计文件不存在 → **优雅空结果**（fileExists=false，不抛错）。**只读**：不提供删除/改写审计能力（不可篡改性）。" +
        "审计目录禁 %TEMP%（G-138）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["tool"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional tool-name filter (exact, case-insensitive)." },
            ["success"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Optional result filter (true=successful calls only)." },
            ["fromUtc"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional window start (ISO-8601 UTC)." },
            ["toUtc"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional window end (ISO-8601 UTC)." },
            ["limit"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Max rows returned (default 50, cap 500)." },
            ["auditDir"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional audit directory override (never %TEMP%)." },
        },
    };

    protected override string CategoryName => ToolCategories.System;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        string path;
        try
        {
            path = GpAuditLog.ResolvePath(ToolArgs.GetString(context, "auditDir"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, ex.Message));
        }

        var toolFilter = ToolArgs.GetString(context, "tool");
        bool? successFilter = context.Arguments is not null && context.Arguments.TryGetValue("success", out var sRaw)
            ? sRaw switch
            {
                bool b => b,
                JsonElement je when je.ValueKind == JsonValueKind.True => true,
                JsonElement je when je.ValueKind == JsonValueKind.False => false,
                _ => null,
            }
            : null;

        var limit = ToolArgs.GetInt(context, "limit") ?? 50;
        limit = Math.Clamp(limit, 0, MaxLimit);

        DateTime? fromUtc = ParseUtc(ToolArgs.GetString(context, "fromUtc"), "fromUtc", out var fromError);
        if (fromError is not null)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, fromError));
        }

        DateTime? toUtc = ParseUtc(ToolArgs.GetString(context, "toUtc"), "toUtc", out var toError);
        if (toError is not null)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, toError));
        }

        var query = GpAuditLog.Read(path, toolFilter, successFilter, limit: limit);
        if (query.CorruptLines < 0)
        {
            return Task.FromResult(OperationResult<object?>.Fail(
                ErrorCodes.InvalidState, "Audit log could not be read: " + (query.Error ?? "unknown")));
        }

        var entries = query.Entries
            .Where(e => fromUtc is null || ParseLoose(e.TimestampUtc) >= fromUtc)
            .Where(e => toUtc is null || ParseLoose(e.TimestampUtc) <= toUtc)
            .ToList();

        return Task.FromResult(OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["path"] = query.Path,
            ["fileExists"] = query.FileExists,
            ["totalEntries"] = query.TotalEntries,
            ["matchedEntries"] = query.MatchedEntries,
            ["returnedEntries"] = entries.Count,
            ["corruptLines"] = query.CorruptLines,
            ["truncated"] = query.Truncated,
            ["immutabilityNote"] = "Read-only query; no API is provided to delete or alter audit entries (non-repudiation).",
            ["entries"] = entries,
        }));
    }

    private static DateTime? ParseUtc(string? text, string field, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var value))
        {
            return value.ToUniversalTime();
        }

        error = $"'{field}' is not a valid ISO-8601 datetime: '{text}'.";
        return null;
    }

    private static DateTime ParseLoose(string text)
        => DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var value)
            ? value.ToUniversalTime()
            : DateTime.MinValue;
}
