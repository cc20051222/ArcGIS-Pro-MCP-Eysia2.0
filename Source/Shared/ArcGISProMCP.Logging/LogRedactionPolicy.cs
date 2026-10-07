using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ArcGISProMCP.Logging;

/// <summary>
/// Allowlist-first sanitizer for legacy and structured log fields.
/// Disallowed fields are omitted and never returned as a fallback string.
/// </summary>
public sealed class LogRedactionPolicy
{
    public const string PolicyVersion = "arcgis-pro-mcp-log-redaction-v1";

    private static readonly Regex AbsolutePath = new(
        "(?:[A-Za-z]:[\\\\/]|\\\\\\\\|(?:^|[\\s(])/[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SensitiveIdentifier = new(
        "(?:^|[_ .:-])(?:credential|password|passwd|token|secret|authorization|bearer|header|provider|model|login|payload|api[_-]?key)(?:$|[_ .:-])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedMessageCodes = new(StringComparer.Ordinal)
    {
        "REQUEST_COMPLETED",
        "REQUEST_SUCCEEDED",
        "REQUEST_FAILED",
        "TOOL_CALL_SUCCEEDED",
        "TOOL_CALL_FAILED",
        "MCP_SERVER_STARTED",
        "MCP_SERVER_STOPPED",
        "PYTHON_BRIDGE_STARTED",
        "PYTHON_BRIDGE_STOPPED",
        "JSON_RPC_PARSE_ERROR",
        "NOTIFICATION_FAILED",
        "MCP_REQUEST_FAILED",
        "HTTP_TRANSPORT_ERROR",
        "PRODUCTION_EVENT",
        "SELFTEST_TOOL_COMPLETED",
        "SELFTEST_RUN_COMPLETED",
        "SELFTEST_RUN_FAILED",
        "DIAGNOSTIC_EXPORT_COMPLETED",
        "DIAGNOSTIC_EXPORT_DEGRADED"
    };

    public LogRedactionPolicy(int maxMessageLength = 512)
    {
        if (maxMessageLength < 32)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMessageLength), "The message limit must be at least 32 characters.");
        }

        MaxMessageLength = maxMessageLength;
    }

    public int MaxMessageLength { get; }

    public static bool IsAllowedMessageCode(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && AllowedMessageCodes.Contains(value.Trim());

    public static bool IsAllowedOutcome(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && AllowedOutcomes.Contains(value.Trim());

    /// <summary>
    /// D-055 C2（13.2 日志脱敏）：把 Windows 用户主目录段掩码为 <c>%USERPROFILE%</c> 形态，
    /// 供**需要保留诊断价值**的日志/回显面在落盘或返回前统一调用。
    /// </summary>
    /// <remarks>
    /// 语义与边界（如实披露）：
    /// <list type="bullet">
    /// <item>仅替换 <c>&lt;盘符&gt;:\Users\&lt;账户段&gt;\</c> 与 <c>/Users/&lt;账户段&gt;/</c> 中的**账户段**，
    /// 形如 <c>C:\Users\alice\proj\x.gdb</c> → <c>C:\Users\%USERPROFILE%\proj\x.gdb</c>；</item>
    /// <item>不触碰其余路径、不做大小写归一、不修改任何错误码或消息结构；输入为 null/空白 → 原样返回；</item>
    /// <item><b>接线状态（D-056 C2 已接线）</b>：本函数已接入结构化日志通道 <see cref="Sanitize"/> 的**标识符字段**判定 ——
    /// 命中绝对路径形态且**掩码确实生效**（账户段被替换）时，字段以**掩码形态保留**并计入
    /// <c>RedactionMaskedCount</c>（<c>Flags</c> 追加 <c>REDACTION_MASKED</c>），**不再计为丢弃**；
    /// <b>契约面语义变化</b>：此类字段不再触发 <c>RedactionFailed</c>（该标志现仅表示「有字段被拒绝/丢弃」）。
    /// <b>未放宽</b>：不含用户主目录段的绝对路径、敏感标识符、超长值与非法字符仍一律丢弃。</item>
    /// </list>
    /// </remarks>
    public static string? MaskUserProfilePaths(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return UserProfileSegment.Replace(
            text,
            m => m.Groups["prefix"].Value + "%USERPROFILE%" + m.Groups["sep"].Value);
    }

    /// <summary>匹配 <c>&lt;盘符&gt;:\Users\&lt;账户段&gt;\</c> 或 <c>/Users/&lt;账户段&gt;/</c>（账户段必需，避免凭空造段；Windows 路径大小写不敏感）。</summary>
    private static readonly Regex UserProfileSegment = new(
        @"(?<prefix>(?:[A-Za-z]:[\\/]|[\\/])users[\\/])(?<account>[^\\/\r\n]+)(?<sep>[\\/])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// 探测**掩码后仍残留的原始账户段**（<c>%USERPROFILE%</c> 占位符本身不算）。
    /// 用于掩码保留前的兜底断言：掩码形态 <c>C:\Users\%USERPROFILE%\x</c> 不应被此规则命中。
    /// </summary>
    private static readonly Regex UnmaskedUserProfileSegment = new(
        @"(?:[A-Za-z]:[\\/]|[\\/])users[\\/](?!%USERPROFILE%[\\/])[^\\/\r\n]+[\\/]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public LogRedactionResult Sanitize(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var failures = 0;
        var masked = 0;
        var messageCode = SanitizeMessageCode(entry.MessageCode, ref failures);
        var outcome = SanitizeOutcome(entry.Outcome, ref failures);
        if (!string.IsNullOrWhiteSpace(entry.Result))
        {
            // Result may be a payload, GIS value, path alias or arbitrary legacy text.
            failures++;
        }

        if (!string.IsNullOrWhiteSpace(entry.Message))
        {
            // Legacy free-text is never persisted by the structured sink, even when it looks harmless.
            failures++;
        }

        if (!string.IsNullOrWhiteSpace(entry.Error))
        {
            // Exception.ToString(), traceback and raw error text are never copied.
            failures++;
        }

        var durationMs = NormalizeDuration(entry, ref failures);
        var record = new SanitizedLogRecord
        {
            TimestampUtc = (entry.Timestamp == default ? DateTimeOffset.UtcNow : entry.Timestamp).ToUniversalTime(),
            Level = entry.Level.ToString(),
            Category = SanitizeIdentifier(entry.Category, ref failures, ref masked),
            RequestId = SanitizeIdentifier(entry.RequestId, ref failures, ref masked),
            CorrelationId = SanitizeIdentifier(entry.CorrelationId, ref failures, ref masked),
            Client = SanitizeIdentifier(entry.Client, ref failures, ref masked),
            Tool = SanitizeIdentifier(entry.Tool, ref failures, ref masked),
            Component = SanitizeIdentifier(entry.Component, ref failures, ref masked),
            Operation = SanitizeIdentifier(entry.Operation, ref failures, ref masked),
            ErrorCode = SanitizeIdentifier(entry.ErrorCode, ref failures, ref masked),
            Outcome = outcome,
            DurationMs = durationMs,
            MessageCode = messageCode,
            RedactionFailed = failures > 0,
            RedactionFailureCount = failures,
            RedactionMaskedCount = masked,
            Flags = Array.Empty<string>()
        };

        // The structured record itself is allowlist-only. If a later field check
        // failed, retain only the marker and count, never the rejected value.
        // D-056 C2：掩码保留（非丢弃）单独标记，便于审计区分「已掩码」与「已丢弃」。
        var flags = new List<string>();
        if (failures > 0)
        {
            flags.Add("REDACTION_FAILED");
        }

        if (masked > 0)
        {
            flags.Add("REDACTION_MASKED");
        }

        if (flags.Count > 0)
        {
            record = record with { Flags = flags };
        }

        return new LogRedactionResult(record, failures, masked);
    }

    private static string? SanitizeMessageCode(string? value, ref int failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (!AllowedMessageCodes.Contains(normalized))
        {
            failures++;
            return null;
        }

        return normalized;
    }

    private static string? SanitizeOutcome(string? value, ref int failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > 64
            || !AllowedOutcomes.Contains(normalized))
        {
            failures++;
            return null;
        }

        return normalized;
    }

    private static string? SanitizeIdentifier(string? value, ref int failures, ref int masked)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        // D-056 C2（13.2 挂账兑现；G-147 登记 → G-149 批准）：**丢弃 → 掩码保留**。
        // 命中绝对路径形态时，先做用户主目录段掩码；**仅当掩码确实生效**（账户段被替换）才保留，
        // 且保留值须满足：长度上限 / 不含敏感标识符 / 掩码后不再含任何用户主目录段。
        // 未含主目录段的绝对路径**仍按既有策略丢弃**（不放宽、不新增可持久化的路径面）。
        if (AbsolutePath.IsMatch(normalized))
        {
            var maskedValue = MaskUserProfilePaths(normalized) ?? normalized;
            var maskingApplied = !string.Equals(maskedValue, normalized, StringComparison.Ordinal);
            if (maskingApplied
                && maskedValue.Length <= MaxIdentifierLength
                && !UnmaskedUserProfileSegment.IsMatch(maskedValue)
                && !SensitiveIdentifier.IsMatch(maskedValue))
            {
                masked++;
                return maskedValue;
            }

            failures++;
            return null;
        }

        if (normalized.Length > MaxIdentifierLength
            || SensitiveIdentifier.IsMatch(normalized)
            || normalized.Any(character => !IsAllowedIdentifierCharacter(character)))
        {
            failures++;
            return null;
        }

        return normalized;
    }

    /// <summary>标识符字段长度上限（既有实现内联的 128）。</summary>
    private const int MaxIdentifierLength = 128;

    private static long? NormalizeDuration(LogEntry entry, ref int failures)
    {
        var duration = entry.DurationMs;
        if (!duration.HasValue && entry.ExecutionTime.HasValue)
        {
            duration = (long)Math.Round(entry.ExecutionTime.Value.TotalMilliseconds);
        }

        if (!duration.HasValue)
        {
            return null;
        }

        if (duration < 0 || duration > TimeSpan.FromDays(7).TotalMilliseconds)
        {
            failures++;
            return null;
        }

        return duration;
    }

    private static readonly HashSet<string> AllowedOutcomes = new(StringComparer.Ordinal)
    {
        "OK",
        "SUCCESS",
        "FAILURE",
        "CANCELLED",
        "TIMEOUT",
        "STARTED",
        "STOPPED",
        "RUNNING",
        "UNAVAILABLE",
        "INCOMPATIBLE",
        "MALFORMED_CONFIGURATION",
        "BRIDGE_FAILURE",
        "NOT_CHECKED",
        "DEGRADED"
    };

    private static bool IsAllowedIdentifierCharacter(char character)
        => char.IsLetterOrDigit(character)
           || character is '_' or '-' or '.' or ':' or ' ';
}

public sealed record LogRedactionResult(SanitizedLogRecord Record, int FailureCount, int MaskedCount = 0);

/// <summary>脱敏后的 allowlist 记录；不包含 legacy Result/Error 或任意 payload。</summary>
public sealed record SanitizedLogRecord
{
    [JsonPropertyName("schema")]
    public string Schema { get; init; } = "arcgis-pro-mcp-log-record-v1";

    [JsonPropertyName("timestampUtc")]
    public DateTimeOffset TimestampUtc { get; init; }

    [JsonPropertyName("level")]
    public string Level { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; init; }

    [JsonPropertyName("requestId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestId { get; init; }

    [JsonPropertyName("correlationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("client")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Client { get; init; }

    [JsonPropertyName("tool")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tool { get; init; }

    [JsonPropertyName("component")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Component { get; init; }

    [JsonPropertyName("operation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Operation { get; init; }

    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("outcome")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Outcome { get; init; }

    [JsonPropertyName("durationMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? DurationMs { get; init; }

    [JsonPropertyName("messageCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MessageCode { get; init; }

    [JsonPropertyName("redactionFailed")]
    public bool RedactionFailed { get; init; }

    [JsonPropertyName("redactionFailureCount")]
    public int RedactionFailureCount { get; init; }

    /// <summary>D-056 C2：被**掩码保留**（而非丢弃）的字段数（用户主目录段 → %USERPROFILE%）。</summary>
    [JsonPropertyName("redactionMaskedCount")]
    public int RedactionMaskedCount { get; init; }

    [JsonPropertyName("flags")]
    public IReadOnlyList<string> Flags { get; init; } = Array.Empty<string>();
}
