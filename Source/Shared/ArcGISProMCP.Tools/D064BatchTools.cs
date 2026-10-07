using System.Diagnostics;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

// ══════════════════════════════════════════════════════════════════════════════
// D-064 · D 段：批处理与系列输出（2 件；run_batch 为本批**安全重点**）
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// D-064 · <b>安全重点</b>：一次往返顺序执行多条**既有工具**调用。
/// 安全架构（工单强制，逐条落地）：
/// <list type="number">
/// <item><b>白名单准入（执行前整体校验）</b>：任一项未注册 / 属批内禁用名录（<c>run_batch</c> 自身、<c>set_readonly_mode</c>）
/// 或超出 <c>allowedTools</c> 收窄集 ⇒ **整批拒绝**（<c>rejectedTools</c> 列出），**零副作用**；</item>
/// <item><b>顺序执行</b>（绝不并发 —— 保证"每项状态可见"与审计可读）；</item>
/// <item><b>不豁免</b>：每项经**同一** required 校验 + **只读闸门** + 工具自身守卫/confirm；批处理不提供绕过通道；</item>
/// <item><b>单项失败</b>按 <c>continueOnError</c>（缺省 false ⇒ 中止后续并如实标记 skipped 原因）；</item>
/// <item><b>总耗时 ≤30 s 硬约束</b>：预算耗尽 ⇒ 截断（后续项 <c>budget-exceeded</c>）并报告已完成项。</item>
/// </list>
/// </summary>
public sealed class RunBatchTool : McpToolBase
{
    public override string Name => "run_batch";

    public override string Description =>
        "顺序执行多条**已注册工具**调用（一次往返）。参数：items（数组，元素 = {tool, arguments?, continueOnError?}）、" +
        "continueOnError（批级缺省 false）、budgetMs（缺省 30000，**上限 30000**，只允许更严）、allowedTools（可选：进一步收窄可调用工具集）。" +
        "契约（安全重点）：① 仅限**已注册**工具，且**禁** run_batch 自身（禁嵌套）与 set_readonly_mode（禁批内翻转只读闸门）——任一项不合规 ⇒ **整批拒绝、零副作用**；" +
        "② 单次最多 50 项；③ **顺序执行**，每项经同一 required 校验 / 只读闸门 / 工具守卫与 confirm（**不豁免**）；" +
        "④ 单项失败按 continueOnError 决定继续或中止；⑤ **总耗时 ≤30 s**，超限即截断并报告已完成项；" +
        "⑥ 每项写一条审计（auditEntryIndex=0 表示审计未落盘，如实披露）。错误码零新增。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["items"] = new Dictionary<string, object?>
            {
                ["type"] = "array",
                ["description"] = "Ordered items: {tool, arguments?, continueOnError?}.",
                ["items"] = new Dictionary<string, object?> { ["type"] = "object" },
            },
            ["continueOnError"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Continue after a failing item (default false)." },
            ["budgetMs"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Total time budget in ms (default 30000, hard max 30000)." },
            ["allowedTools"] = new Dictionary<string, object?>
            {
                ["type"] = "array",
                ["description"] = "Optional narrower allow-list of tool names (can only narrow, never widen).",
                ["items"] = new Dictionary<string, object?> { ["type"] = "string" },
            },
        },
        ["required"] = new[] { "items" }
    };

    protected override string CategoryName => ToolCategories.General;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var registry = context.Registry;
        if (registry is null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidState, "run_batch requires a tool registry in the execution context (none was supplied).");
        }

        var rawItems = ToolArgs.GetObjectList(context, "items");
        if (rawItems.Count == 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "items must contain at least one {tool, arguments} entry.");
        }

        if (BatchPolicy.ExceedsItemLimit(rawItems.Count))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                $"items contains {rawItems.Count} entries; the maximum per batch is {BatchPolicy.MaxItems}.");
        }

        var requested = new List<BatchItemRequest>(rawItems.Count);
        for (var i = 0; i < rawItems.Count; i++)
        {
            var item = rawItems[i];
            if (item is null)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"items[{i}] must be an object.");
            }

            var name = ToolArgs.ReadString(item, "tool");
            var argsObj = ToolArgs.Read(item, "arguments");
            var args = new Dictionary<string, object?>(StringComparer.Ordinal);
            switch (argsObj)
            {
                case IReadOnlyDictionary<string, object?> ro:
                    foreach (var kv in ro)
                    {
                        args[kv.Key] = kv.Value;
                    }

                    break;
                case IDictionary<string, object?> d:
                    foreach (var kv in d)
                    {
                        args[kv.Key] = kv.Value;
                    }

                    break;
                case null:
                    break;
                default:
                    return OperationResult<object?>.Fail(
                        ErrorCodes.InvalidArgument, $"items[{i}].arguments must be an object.");
            }

            bool? perItemContinue = null;
            if (ToolArgs.Read(item, "continueOnError") is { } co)
            {
                perItemContinue = co switch
                {
                    bool b => b,
                    string s when bool.TryParse(s, out var sb) => sb,
                    _ => null,
                };
            }

            requested.Add(new BatchItemRequest { Tool = name ?? string.Empty, Arguments = args, ContinueOnError = perItemContinue });
        }

        // ── ① 白名单准入（执行前**整体**校验；任一不合规 ⇒ 整批拒绝，零副作用）──
        var allowed = ToolArgs.GetStringList(context, "allowedTools");
        var rejected = BatchPolicy.Validate(requested, registry.Contains, allowed.Count > 0 ? allowed : null);
        if (rejected.Count > 0)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                $"batch refused before execution (zero side effects): {rejected.Count} item(s) are not admissible — "
                + string.Join(", ", rejected.Distinct())
                + ". Allowed = registered tools minus [" + string.Join(", ", BatchPolicy.DeniedInBatch) + "]"
                + (allowed.Count > 0 ? ", further narrowed by allowedTools" : string.Empty) + ".");
        }

        var budgetMs = BatchPolicy.ClampBudget(ToolArgs.GetInt(context, "budgetMs"));
        var batchContinue = ToolArgs.GetBool(context, "continueOnError");
        var readOnly = context.ReadOnly;
        var auditPath = TryResolveAuditPath();

        var sw = Stopwatch.StartNew();
        var results = new List<BatchItemResult>(requested.Count);
        var truncated = false;
        var aborted = false;

        for (var i = 0; i < requested.Count; i++)
        {
            var req = requested[i];
            var write = ToolWriteClassification.RefusedInReadOnly(req.Tool);

            if (aborted)
            {
                results.Add(Skipped(i, req.Tool, "aborted-after-failure", write));
                continue;
            }

            if (BatchPolicy.BudgetExhausted(sw.ElapsedMilliseconds, budgetMs))
            {
                truncated = true;
                results.Add(Skipped(i, req.Tool, "budget-exceeded", write));
                continue;
            }

            if (readOnly is { IsReadOnly: true } && write)
            {
                results.Add(Skipped(i, req.Tool, "read-only", write));
                continue;
            }

            var tool = registry.Get(req.Tool);
            if (tool is null)
            {
                results.Add(Skipped(i, req.Tool, "not-registered", write));
                continue;
            }

            // ② 与路由层**同一实现**的 required 校验（批内不豁免）。
            var itemInvoker = context.Invoker ?? ToolInvoker.Default;
            var validationContext = new ToolExecutionContext
            {
                RequestId = context.RequestId,
                CancellationToken = context.CancellationToken,
                Logger = context.Logger,
                Host = context.Host,
                Python = context.Python,
                Settings = context.Settings,
                Arguments = req.Arguments,
                Registry = registry,
                ReadOnly = readOnly,
                Invoker = itemInvoker,
            };
            var validation = itemInvoker.Validate(tool, validationContext);
            if (validation is not null)
            {
                results.Add(new BatchItemResult
                {
                    Index = i,
                    Tool = req.Tool,
                    Executed = false,
                    Success = false,
                    ResultCode = validation.Code,
                    Message = validation.Message,
                    WriteOperation = write,
                    SkippedReason = "invalid-arguments",
                });
                AppendAudit(auditPath, req.Tool, req.Arguments, false, validation.Code, 0, "batch item invalid-arguments");
                if (!(req.ContinueOnError ?? batchContinue))
                {
                    aborted = true;
                }

                continue;
            }

            // ③ 顺序执行（子上下文：同 Host/Logger/Settings/Registry/ReadOnly；参数换成该项参数）。
            var itemStopwatch = Stopwatch.StartNew();
            OperationResult<object?> itemResult;
            try
            {
                var subContext = new ToolExecutionContext
                {
                    RequestId = context.RequestId,
                    CancellationToken = context.CancellationToken,
                    Logger = context.Logger,
                    Host = context.Host,
                    Python = context.Python,
                    Settings = context.Settings,
                    Arguments = req.Arguments,
                    Registry = registry,
                    ReadOnly = readOnly,
                    Invoker = context.Invoker,
                };
                itemResult = await (subContext.Invoker ?? ToolInvoker.Default)
                    .InvokeAsync(tool, subContext).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                itemStopwatch.Stop();
                results.Add(new BatchItemResult
                {
                    Index = i,
                    Tool = req.Tool,
                    Executed = true,
                    Success = false,
                    ResultCode = ErrorCodes.Cancelled,
                    Message = "item cancelled.",
                    DurationMs = itemStopwatch.ElapsedMilliseconds,
                    WriteOperation = write,
                });
                AppendAudit(auditPath, req.Tool, req.Arguments, false, ErrorCodes.Cancelled, itemStopwatch.ElapsedMilliseconds, "batch item cancelled");
                throw;
            }
            catch (Exception ex)
            {
                itemResult = OperationResult<object?>.Fail(ErrorCodes.InternalError, $"item threw: {ex.Message}");
            }

            itemStopwatch.Stop();
            var code = itemResult.Success
                ? "OK"
                : (itemResult.Errors.Count > 0 ? itemResult.Errors[0].Code : ErrorCodes.ExecutionFailed);

            results.Add(new BatchItemResult
            {
                Index = i,
                Tool = req.Tool,
                Executed = true,
                Success = itemResult.Success,
                ResultCode = code,
                Message = itemResult.Success
                    ? itemResult.Message
                    : (itemResult.Errors.Count > 0 ? itemResult.Errors[0].Message : itemResult.Message),
                DurationMs = itemStopwatch.ElapsedMilliseconds,
                Data = itemResult.Data,
                WriteOperation = write,
            });

            AppendAudit(auditPath, req.Tool, req.Arguments,
                itemResult.Success && string.Equals(D064BulkConfirmation.BoolRepr(req.Arguments, "confirm"), "true", StringComparison.Ordinal),
                code, itemStopwatch.ElapsedMilliseconds, "batch item");

            if (!itemResult.Success && !(req.ContinueOnError ?? batchContinue))
            {
                aborted = true;
            }
        }

        sw.Stop();

        var executed = results.Count(r => r.Executed);
        var succeeded = results.Count(r => r.Success);
        // 语义：Failed = 已执行但失败 **或** 校验拒绝（invalid-arguments，属失败而非"未尝试"）；
        //      Skipped = 未尝试（read-only / budget-exceeded / aborted-after-failure / not-registered）。
        var failed = results.Count(r => !r.Success && (r.Executed || r.SkippedReason == "invalid-arguments"));
        var skipped = results.Count(r => !r.Executed && r.SkippedReason != "invalid-arguments");

        var auditNote = "batch summary: " + executed + " executed / " + succeeded + " ok / " + failed + " failed / "
                        + skipped + " skipped; budgetMs=" + budgetMs + "; elapsedMs=" + sw.ElapsedMilliseconds
                        + "; truncated=" + truncated + (readOnly is null ? string.Empty : "; readOnly=" + readOnly.IsReadOnly);

        var batchIndex = AppendAudit(auditPath, "run_batch",
            new Dictionary<string, object?> { ["items"] = requested.Count, ["durationMs"] = sw.ElapsedMilliseconds },
            false, failed == 0 && !truncated ? "OK" : "PARTIAL", sw.ElapsedMilliseconds, auditNote);

        var payload = new RunBatchResult
        {
            TotalRequested = requested.Count,
            Executed = executed,
            Succeeded = succeeded,
            Failed = failed,
            Skipped = skipped,
            Truncated = truncated,
            BudgetMs = budgetMs,
            ElapsedMs = sw.ElapsedMilliseconds,
            ReadOnlyMode = readOnly?.IsReadOnly ?? false,
            Items = results,
            AuditNote = auditNote,
            AuditEntryIndex = batchIndex,
            AuditPath = auditPath,
        };

        return OperationResult<object?>.Ok(payload, auditNote);
    }

    private static BatchItemResult Skipped(int index, string tool, string reason, bool write)
        => new()
        {
            Index = index,
            Tool = tool,
            Executed = false,
            Success = false,
            ResultCode = reason switch
            {
                "read-only" => ErrorCodes.PermissionDenied,
                "budget-exceeded" => ErrorCodes.Timeout,
                _ => ErrorCodes.InvalidState,
            },
            SkippedReason = reason,
            WriteOperation = write,
        };

    /// <summary>审计目录解析（G-138：解析失败**不阻断**批处理，但如实返回 0 条号）。</summary>
    private static string? TryResolveAuditPath()
    {
        try
        {
            return GpAuditLog.ResolvePath();
        }
        catch
        {
            return null;
        }
    }

    private static long AppendAudit(
        string? auditPath, string tool, IReadOnlyDictionary<string, object?>? args,
        bool destructiveConfirmed, string resultCode, long durationMs, string note)
    {
        if (string.IsNullOrWhiteSpace(auditPath))
        {
            return 0;
        }

        var digest = args is null
            ? string.Empty
            : string.Join("; ", args.Select(kv => kv.Key + "=" + Truncate(D064BulkConfirmation.BoolRepr(kv.Value), 60)));

        var entry = new GpAuditEntry
        {
            TimestampUtc = DateTime.UtcNow.ToString("o"),
            Tool = "run_batch:" + tool,
            ParameterDigest = Truncate(digest, 400),
            ParameterForm = "batch",
            Destructive = ToolWriteClassification.TierOf(tool) == ToolWriteTier.Write,
            Confirm = destructiveConfirmed,
            Success = string.Equals(resultCode, "OK", StringComparison.Ordinal),
            ResultCode = resultCode,
            DurationMs = durationMs,
            AuditNote = note,
            Pid = Environment.ProcessId,
        };

        return GpAuditLog.TryAppend(auditPath!, entry, out var index, out _) ? index : 0;
    }

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s!.Length <= max ? s : s.Substring(0, max) + "…");
}

/// <summary>D-064 · 布局 map series 多页 PDF 导出（输出守卫 + 页数上限披露）。</summary>
public sealed class ExportMapSeriesTool : McpToolBase
{
    public override string Name => "export_map_series";

    public override string Description =>
        "把布局的 **map series（系列）** 导出为多页 PDF。参数：layoutName、outputPath（**必须以 .pdf 结尾**）、" +
        "maxPages（缺省 50，上限 500）、resolution（DPI，可省略 ⇒ SDK 默认 96）、overwrite（缺省 false）。" +
        "契约：布局未启用 map series ⇒ INVALID_ARGUMENT（**不静默退化为单页导出**）；页数 > maxPages ⇒ INVALID_ARGUMENT（**不静默截断文件**）；" +
        "扩展名非 .pdf → INVALID_ARGUMENT；输出已存在且未显式 overwrite=true → OUTPUT_EXISTS（零状态变更）；" +
        "受保护输出根 → PATH_ESCAPE_REJECTED；失败时宿主须清理半成品产物；返回 pageCount 与 PDF magic 校验。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["layoutName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layout carrying the map series." },
            ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output .pdf path." },
            ["maxPages"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Page cap (default 50, max 500); exceeding it refuses the run." },
            ["resolution"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Export DPI (optional; SDK default 96)." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "layoutName", "outputPath" }
    };

    protected override string CategoryName => ToolCategories.Layout;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    /// <summary>页数上限天花板（工具层钳制）。</summary>
    public const int MaxPageCeiling = 500;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var layout = ToolArgs.GetString(context, "layoutName");
        var output = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(layout) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layoutName and outputPath are required.");
        }

        if (!output!.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "outputPath must end with .pdf.");
        }

        var maxPages = ToolArgs.GetInt(context, "maxPages") ?? 50;
        if (maxPages <= 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxPages must be > 0.");
        }

        if (maxPages > MaxPageCeiling)
        {
            maxPages = MaxPageCeiling;
        }

        var resolution = ToolArgs.GetDouble(context, "resolution");
        if (resolution is not null && resolution.Value <= 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "resolution must be > 0 when provided.");
        }

        var hit = ProtectedOutputPathGuard.Match(output);
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"output '{output}' is inside a protected root ('{hit}'); export refused (no artefacts).");
        }

        var gate = await GpOverwriteGuard.EvaluateAsync(context, output!, context.CancellationToken).ConfigureAwait(false);
        if (gate.Refusal is not null)
        {
            return gate.Refusal;
        }

        var r = await context.Host.Layout.ExportMapSeriesAsync(
            layout!, output!, maxPages, resolution, ToolArgs.GetBool(context, "overwrite"), context.CancellationToken)
            .ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>D-064 · 批处理审计的取值呈现助手（纯函数）。</summary>
internal static class D064BulkConfirmation
{
    /// <summary>把任意参数值渲染成可审计的短串（bool 小写化，便于断言）。</summary>
    public static string BoolRepr(object? value)
        => value switch
        {
            null => string.Empty,
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? string.Empty,
        };

    /// <summary>从参数字典读取 confirm（缺省 false）。</summary>
    public static string BoolRepr(IReadOnlyDictionary<string, object?>? args, string key)
    {
        if (args is null || !args.TryGetValue(key, out var v))
        {
            return "false";
        }

        return v switch
        {
            bool b => b ? "true" : "false",
            string s when bool.TryParse(s, out var sb) => sb ? "true" : "false",
            _ => "false",
        };
    }
}
