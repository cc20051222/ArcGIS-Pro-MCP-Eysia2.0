using System.Text.Json;

namespace ArcGISProMCP.Core.Models;

/// <summary>
/// D-062 · A1 受控 GP 白名单条目（Config/gp-whitelist.json 单条）。
/// 白名单由 Keeper 工单定义：起步 ~50 件，禁入 delete/rename/覆写类 GP、workspace 管理类、环境破坏类；
/// 本批不扩充。白名单外工具一律 <see cref="ErrorCodes.InvalidArgument"/>（消息明示「不在受控白名单」）。
/// </summary>
public sealed class GpWhitelistEntry
{
    /// <summary>GP 工具名（点串别名形态，如 analysis.Buffer）。</summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>破坏性标记：true ⇒ 调用必须显式 confirm=true（缺省拒）。</summary>
    public bool Destructive { get; set; }

    /// <summary>一句话用途（A2 枚举披露用）。</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>参数清单（按 GP 形参序排列；A3 describe 与 A1 参数映射双用）。</summary>
    public List<GpWhitelistParameter> Parameters { get; set; } = new();
}

/// <summary>白名单条目的参数定义（方向用于守卫分流：output → 输出守卫；input → 输入守卫）。</summary>
public sealed class GpWhitelistParameter
{
    public string Name { get; set; } = string.Empty;

    /// <summary>input | output（守卫判定依据；派生形态（如 filter/extent）不判界，如实披露）。</summary>
    public string Direction { get; set; } = "input";

    public bool Required { get; set; }

    /// <summary>GP 参数类型（describe 披露用；不参与强校验）。</summary>
    public string Type { get; set; } = "string";

    /// <summary>默认值（存在则必填可豁免；执行时以显式传入为准）。</summary>
    public string? Default { get; set; }
}

/// <summary>
/// 受控 GP 白名单（整册）。加载/解析见 <see cref="GpWhitelistDocument"/>；运行时经
/// <c>IGeoprocessingService.GetWhitelistAsync</c> 提供（宿主负责路径解析与嵌入资源兜底）。
/// </summary>
public sealed class GpWhitelist
{
    public IReadOnlyList<GpWhitelistEntry> Entries { get; init; } = Array.Empty<GpWhitelistEntry>();

    /// <summary>加载来源披露（env / file / embedded / test）。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>按名查条目（大小写不敏感；GP 名大小写习惯不一）。</summary>
    public GpWhitelistEntry? Find(string toolName)
    {
        foreach (var e in Entries)
        {
            if (string.Equals(e.Tool, toolName, StringComparison.OrdinalIgnoreCase))
            {
                return e;
            }
        }

        return null;
    }
}

/// <summary>白名单 JSON 解析与校验（纯函数，Core 可单测）。</summary>
public static class GpWhitelistParser
{
    /// <summary>解析整册白名单；结构非法 → 异常（调用方转为 INVALID_STATE 保守拒绝）。</summary>
    public static GpWhitelist Parse(string json, string source)
    {
        var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("gp-whitelist.json root must be an array.");
        }

        var entries = new List<GpWhitelistEntry>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var entry = new GpWhitelistEntry
            {
                Tool = item.GetProperty("tool").GetString() ?? string.Empty,
                Destructive = item.TryGetProperty("destructive", out var d) && d.ValueKind == JsonValueKind.True,
                Notes = item.TryGetProperty("notes", out var n) ? n.GetString() ?? string.Empty : string.Empty,
            };

            if (item.TryGetProperty("parameters", out var ps) && ps.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in ps.EnumerateArray())
                {
                    entry.Parameters.Add(new GpWhitelistParameter
                    {
                        Name = p.GetProperty("name").GetString() ?? string.Empty,
                        Direction = p.TryGetProperty("direction", out var dir) ? dir.GetString() ?? "input" : "input",
                        Required = p.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.True,
                        Type = p.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string",
                        Default = p.TryGetProperty("default", out var def) && def.ValueKind == JsonValueKind.String
                            ? def.GetString()
                            : null,
                    });
                }
            }

            if (entry.Tool.Length > 0)
            {
                entries.Add(entry);
            }
        }

        return new GpWhitelist { Entries = entries, Source = source };
    }
}

/// <summary>D-062 · A1 受控 GP 运行请求。</summary>
public sealed class GpRunRequest
{
    /// <summary>GP 工具名（点串，如 analysis.Buffer）—— 必须在白名单内。</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>命名参数形态（键 = 白名单参数名）。与 <see cref="PositionalValues"/> 二选一。</summary>
    public IReadOnlyDictionary<string, object?>? Parameters { get; set; }

    /// <summary>位置参数形态（按白名单参数序）。与 <see cref="Parameters"/> 二选一（审计披露所选形态）。</summary>
    public IReadOnlyList<string>? PositionalValues { get; set; }

    /// <summary>破坏性工具（destructive=true）必须显式 true，缺省拒。</summary>
    public bool Confirm { get; set; }

    /// <summary>调用方附注（写入审计行，可追溯调用意图）。</summary>
    public string? AuditNote { get; set; }

    /// <summary>审计文件目录覆盖（缺省走环境变量 / 程序集旁默认；G-138：禁 %TEMP%）。</summary>
    public string? AuditPath { get; set; }
}

/// <summary>D-062 · A1 受控 GP 运行结果。</summary>
public sealed class GpRunResult
{
    public string ToolName { get; set; } = string.Empty;

    /// <summary>GP 返回值字符串（无则空串）。</summary>
    public string Result { get; set; } = string.Empty;

    public IReadOnlyList<string> Messages { get; set; } = Array.Empty<string>();

    /// <summary>输出状态证明 JSON（沿用 D-016 三态语义；输出位可判定时才有）。</summary>
    public string? StateProof { get; set; }

    public long DurationMs { get; set; }

    /// <summary>本调用审计行落点（jsonl 全路径）。</summary>
    public string AuditPath { get; set; } = string.Empty;

    /// <summary>审计行号（1 起；追加失败时为 0 并披露原因）。</summary>
    public long AuditEntryIndex { get; set; }

    public bool Destructive { get; set; }

    public bool ConfirmRequired { get; set; }

    /// <summary>传参形态披露（named / positional）。</summary>
    public string ParameterForm { get; set; } = string.Empty;
}

/// <summary>D-062 · A4 最近一次 GP 执行的消息快照。</summary>
public sealed class GpMessagesInfo
{
    /// <summary>最近一次调用的时间（UTC ISO-8601）；从未执行过 → null。</summary>
    public string? LastCallAtUtc { get; set; }

    public string? ToolName { get; set; }

    public bool? Success { get; set; }

    /// <summary>常规消息（severity=info）。</summary>
    public IReadOnlyList<string> Messages { get; set; } = Array.Empty<string>();

    /// <summary>错误消息（severity=error）。</summary>
    public IReadOnlyList<string> ErrorMessages { get; set; } = Array.Empty<string>();

    /// <summary> severity 推导披露：Executor 仅留双列表，error = ErrorMessages 非空。</summary>
    public string SeverityBasis { get; set; } = "executor dual-list (info=Messages, error=ErrorMessages)";
}

/// <summary>D-062 · A5 扩展许可检查结果。</summary>
public sealed class ExtensionCheckInfo
{
    /// <summary>SDK 许可码（如 SpatialAnalyst）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>可用许可数 > 0。</summary>
    public bool Available { get; set; }

    /// <summary>是否尝试签出。</summary>
    public bool CheckoutAttempted { get; set; }

    /// <summary>签出是否成功（未尝试 → null）。</summary>
    public bool? CheckoutSucceeded { get; set; }
}

/// <summary>D-062 · C1 字段统计（数值字段；空值跳过并披露计数）。</summary>
public sealed class FieldStatisticsInfo
{
    public string MapName { get; set; } = string.Empty;
    public string LayerName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? WhereClause { get; set; }

    public long Count { get; set; }

    /// <summary>空值/非数值被跳过的行数（如实披露）。</summary>
    public long Skipped { get; set; }

    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Mean { get; set; }
    public double? Median { get; set; }
    public double? Sum { get; set; }

    /// <summary>样本标准差（n-1；n&lt;2 → null，披露口径）。</summary>
    public double? StdDev { get; set; }

    /// <summary>扫描行数上限（超出截断并披露）。</summary>
    public long? TruncatedAt { get; set; }
}

/// <summary>D-062 · C2 分组聚合行。</summary>
public sealed class GroupSummaryRow
{
    /// <summary>分组键（多字段以 " | " 连接，顺序同请求）。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>各分组字段原始值（顺序同请求；null 保留为 null）。</summary>
    public IReadOnlyList<string?> GroupValues { get; set; } = Array.Empty<string?>();

    public long Count { get; set; }

    /// <summary>sum/min/max/mean/first/last —— 未请求的聚合为 null（不臆测）。</summary>
    public double? Sum { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Mean { get; set; }

    /// <summary>first/last 取遍历序首/末的非空值（披露口径）。</summary>
    public string? First { get; set; }
    public string? Last { get; set; }
}

/// <summary>D-062 · C2 分组聚合结果。</summary>
public sealed class GroupSummaryInfo
{
    public string MapName { get; set; } = string.Empty;
    public string LayerName { get; set; } = string.Empty;
    public IReadOnlyList<string> GroupByFields { get; set; } = Array.Empty<string>();
    public string? AggField { get; set; }
    public IReadOnlyList<string> Aggregations { get; set; } = Array.Empty<string>();
    public string? WhereClause { get; set; }
    public IReadOnlyList<GroupSummaryRow> Rows { get; set; } = Array.Empty<GroupSummaryRow>();

    /// <summary>topN 截断披露：实际分组数 > 返回行数时非空。</summary>
    public long? TotalGroups { get; set; }

    public long? TruncatedAt { get; set; }
}

/// <summary>D-062 · B 段编辑会话状态（B6；count 为会话跟踪，HasEdits 为权威）。</summary>
public sealed class EditSessionState
{
    /// <summary>工程存在未提交编辑（SDK 权威）。</summary>
    public bool HasEdits { get; set; }

    /// <summary>会话跟踪的未提交变更行数（本会话内 B1–B3 累计；save/discard 归零）。</summary>
    public long PendingChangeCount { get; set; }

    /// <summary>本会话内被编辑过的图层（跟踪口径）。</summary>
    public IReadOnlyList<string> AffectedLayers { get; set; } = Array.Empty<string>();

    /// <summary>跟踪口径披露。</summary>
    public string TrackingNote { get; set; } =
        "PendingChangeCount is session-tracked (this MCP session); Project.HasEdits is the authoritative ground truth.";
}

/// <summary>D-062 · B 段单次编辑操作结果。</summary>
public sealed class EditOpResult
{
    public string Action { get; set; } = string.Empty;

    public string MapName { get; set; } = string.Empty;

    public string LayerName { get; set; } = string.Empty;

    /// <summary>EditOperation.Execute() 是否成功。</summary>
    public bool Executed { get; set; }

    /// <summary>本操作影响的行数（插入数/更新数/删除数）。</summary>
    public long RowsAffected { get; set; }

    /// <summary>操作后未提交变更累计（会话跟踪）。</summary>
    public long PendingChangeCount { get; set; }

    /// <summary>提交语义披露：默认不自动提交，需 save_edits。</summary>
    public string TransactionNote { get; set; } =
        "Auto-commit is disabled by design: changes enter the EditOperation undo stack; call save_edits to commit or discard_edits to roll back.";
}
