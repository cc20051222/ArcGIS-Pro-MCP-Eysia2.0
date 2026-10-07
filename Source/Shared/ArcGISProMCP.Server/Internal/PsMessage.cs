using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Server.Internal;

/// <summary>
/// D-083 组 B——PS 通道消息（ADR D-105 消息最小字段集）解析结果。
/// <para>
/// <b>fail-closed</b>：11 个最小字段**必须全部存在且可解析**；任一缺失/类型不符/不可解析即整条拒绝。
/// <c>typedArgs</c> 内**未知字段一律拒绝**（不静默忽略），且不接受任何表达式/脚本/任意 URL 语义
/// （B-05：本类型不含任何"执行"入口，只做类型化取值）。
/// </para>
/// </summary>
public sealed class PsMessage
{
    private PsMessage(
        string requestId,
        string? jobId,
        string? documentId,
        string? specRevision,
        string? stepId,
        string op,
        System.Text.Json.JsonElement typedArgs,
        System.Text.Json.JsonElement artifactRefs,
        DateTimeOffset deadline,
        string? approvalDigest,
        int fencingGeneration)
    {
        RequestId = requestId;
        JobId = jobId;
        DocumentId = documentId;
        SpecRevision = specRevision;
        StepId = stepId;
        Op = op;
        TypedArgs = typedArgs;
        ArtifactRefs = artifactRefs;
        Deadline = deadline;
        ApprovalDigest = approvalDigest;
        FencingGeneration = fencingGeneration;
    }

    /// <summary>GUID 字符串（必填、格式合法；一次性，重放拒绝）。</summary>
    public string RequestId { get; }

    /// <summary>作业身份（M06 JobStore 口径；骨架阶段允许空串，字段必须存在）。</summary>
    public string? JobId { get; }

    /// <summary>宿主文档身份（必须为拥有或明确选定文档；骨架阶段不认领任何文档）。</summary>
    public string? DocumentId { get; }

    /// <summary>DesignSpec 不可变 revision。</summary>
    public string? SpecRevision { get; }

    /// <summary>步骤身份（幂等键）。</summary>
    public string? StepId { get; }

    /// <summary>有限操作名（白名单枚举）。</summary>
    public string Op { get; }

    /// <summary>类型化参数对象（逐字段类型/范围校验；未知字段拒绝）。</summary>
    public System.Text.Json.JsonElement TypedArgs { get; }

    /// <summary>输入/输出产物引用数组（路径须通过拥有关系校验）。</summary>
    public System.Text.Json.JsonElement ArtifactRefs { get; }

    /// <summary>该步骤截止时刻（超期不新开工；超期不等于失败）。</summary>
    public DateTimeOffset Deadline { get; }

    /// <summary>批准记录摘要（ADR：客户端 <c>confirm=true</c> 不构成批准）。骨架阶段为空串。</summary>
    public string? ApprovalDigest { get; }

    /// <summary>执行权代际（必填、单调；旧代际迟到结果一律丢弃）。</summary>
    public int FencingGeneration { get; }

    /// <summary>解析结果（成功携带消息；失败携带注册错误码与原因）。</summary>
    public readonly record struct ParseResult(PsMessage? Message, string? ErrorCode, string? Reason, string? Field)
    {
        public bool Success => Message is not null;
    }

    public static ParseResult Parse(string? body, PsChannelPolicy policy, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new ParseResult(null, ErrorCodes.InvalidArgument, "empty-body", null);
        }

        System.Text.Json.JsonDocument document;
        try
        {
            document = System.Text.Json.JsonDocument.Parse(body);
        }
        catch (System.Text.Json.JsonException)
        {
            return new ParseResult(null, ErrorCodes.InvalidArgument, "not-json", null);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "not-object", null);
            }

            var allowed = new HashSet<string>(InternalPsRoutes.MessageFields, StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!allowed.Contains(property.Name))
                {
                    // ADR D-105「拒绝未知字段（不静默忽略）」
                    return new ParseResult(null, ErrorCodes.InvalidArgument, "unknown-field", property.Name);
                }
            }

            foreach (var field in InternalPsRoutes.MessageFields)
            {
                if (!root.TryGetProperty(field, out _))
                {
                    return new ParseResult(null, ErrorCodes.InvalidArgument, "missing-field", field);
                }
            }

            if (!TryGetString(root, "requestId", out var requestId))
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "field-not-string", "requestId");
            }

            if (!Guid.TryParse(requestId, out _))
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "requestId-not-guid", "requestId");
            }

            if (!TryGetString(root, "jobId", out var jobId)
                || !TryGetString(root, "documentId", out var documentId)
                || !TryGetString(root, "specRevision", out var specRevision)
                || !TryGetString(root, "stepId", out var stepId)
                || !TryGetString(root, "approvalDigest", out var approvalDigest))
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "field-not-string", "text-field");
            }

            if (!TryGetString(root, "op", out var op) || string.IsNullOrWhiteSpace(op))
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "op-empty", "op");
            }

            // op 白名单：白名单外即拒绝（不接受表达式/脚本）——本批已实现集在 handler 侧再判一次。
            if (!InternalPsRoutes.ImplementedOps.Contains(op, StringComparer.Ordinal))
            {
                return new ParseResult(null, ErrorCodes.NotFound, "op-not-allowlisted", "op");
            }

            var typedArgs = root.GetProperty("typedArgs");
            if (typedArgs.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "typedArgs-not-object", "typedArgs");
            }

            var typedArgsJson = typedArgs.GetRawText();
            if (System.Text.Encoding.UTF8.GetByteCount(typedArgsJson) > policy.MaxTypedArgsBytes)
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "typedArgs-too-large", "typedArgs");
            }

            var artifactRefs = root.GetProperty("artifactRefs");
            if (artifactRefs.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "artifactRefs-not-array", "artifactRefs");
            }

            if (artifactRefs.GetArrayLength() > policy.MaxArtifactRefs)
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "artifactRefs-too-many", "artifactRefs");
            }

            // 路径拥有关系校验：artifactRefs 中任一 path 字段必须落在拥有根下（ADR D-106 面 4）
            foreach (var artifact in artifactRefs.EnumerateArray())
            {
                if (artifact.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    return new ParseResult(null, ErrorCodes.InvalidArgument, "artifactRef-not-object", "artifactRefs");
                }

                foreach (var property in artifact.EnumerateObject())
                {
                    if (!string.Equals(property.Name, "path", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (property.Value.ValueKind != System.Text.Json.JsonValueKind.String)
                    {
                        return new ParseResult(null, ErrorCodes.InvalidArgument, "artifact-path-not-string", "artifactRefs");
                    }

                    if (!policy.IsPathOwned(property.Value.GetString(), out var pathReason))
                    {
                        return new ParseResult(null, ErrorCodes.PathEscapeRejected, pathReason, "artifactRefs.path");
                    }
                }
            }

            if (!TryGetDeadline(root, out var deadline))
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "deadline-unparsable", "deadline");
            }

            if (!root.TryGetProperty("fencingGeneration", out var fencingElement)
                || fencingElement.ValueKind != System.Text.Json.JsonValueKind.Number
                || !fencingElement.TryGetInt32(out var fencing)
                || fencing < 0)
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "fencingGeneration-invalid", "fencingGeneration");
            }

            // deadline 窗口：已过期不新开工（超期拒绝，不等于失败 ⇒ 调用方只记账不执行）
            if (deadline <= now)
            {
                return new ParseResult(null, ErrorCodes.RequestTimeout, "deadline-expired", "deadline");
            }

            if (deadline > now.AddSeconds(policy.MaxDeadlineHorizonSeconds))
            {
                return new ParseResult(null, ErrorCodes.InvalidArgument, "deadline-beyond-horizon", "deadline");
            }

            return new ParseResult(
                new PsMessage(requestId, jobId, documentId, specRevision, stepId, op,
                    typedArgs, artifactRefs, deadline, approvalDigest, fencing),
                null,
                null,
                null);
        }
    }

    private static bool TryGetString(System.Text.Json.JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var element)
            || element.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return value is not null;
    }

    private static bool TryGetDeadline(System.Text.Json.JsonElement root, out DateTimeOffset deadline)
    {
        deadline = default;
        if (!root.TryGetProperty("deadline", out var element))
        {
            return false;
        }

        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.String:
                return DateTimeOffset.TryParse(
                    element.GetString(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out deadline);
            case System.Text.Json.JsonValueKind.Number:
                if (element.TryGetInt64(out var epochMs))
                {
                    deadline = DateTimeOffset.FromUnixTimeMilliseconds(epochMs);
                    return true;
                }

                return false;
            default:
                return false;
        }
    }
}
