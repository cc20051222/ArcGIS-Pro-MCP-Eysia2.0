using System.Globalization;
using System.Text.Json;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Server.Transport;

namespace ArcGISProMCP.Server.Internal;

/// <summary>
/// D-083 组 B——`/internal/ps/v1/*` 最小验证版处理器（PS 通道 ADR `f03b-7` D-101…D-107 落地）。
/// <para>
/// <b>本批实现的 op 集（零 `ps_*` 工具）</b>：
/// <c>GET health</c>（只读、无令牌、仍走 Host/Origin 校验面）、
/// <c>POST pair/request</c>（一次性挑战）、<c>POST pair/confirm</c>（配对码 ⇒ 持久令牌）、
/// <c>POST session/open</c>（持久令牌 ⇒ 会话令牌）、<c>POST ping</c>（会话域回声，ADR D-105 全字段强校验）；
/// <c>POST file-token</c> 为**明确拒绝桩**（见 <see cref="InternalPsRoutes.FileTokenRefusal"/>）。
/// </para>
/// <para>
/// <b>B-05</b>：不接受任意 JS / actionJSON / 任意 URL；<c>op</c> 只取白名单枚举，
/// <c>typedArgs</c> 只做类型化取值，本处理器**没有任何执行入口**（不调用 GIS、不写文件、不起进程）。
/// </para>
/// </summary>
public sealed class PsChannelHandler : IInternalRouteHandler
{
    /// <summary>健康检查允许缺失 Origin（仅只读、无 secret、无状态变更）；其余 op 一律要求 Origin。</summary>
    private const string HealthOpName = "health";

    private readonly PsChannelPolicy _policy;
    private readonly PsSessionStore _store;
    private readonly PsReplayGuard _replay;
    private readonly string _serverVersion;

    public PsChannelHandler(
        PsChannelPolicy policy,
        PsSessionStore store,
        string serverVersion,
        DateTimeOffset? fixedNow = null)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _serverVersion = string.IsNullOrWhiteSpace(serverVersion) ? "0.0.0" : serverVersion;
        FixedNow = fixedNow;
        _replay = new PsReplayGuard(policy.MaxReplayEntries);
    }

    /// <summary>测试用的冻结时钟（null ⇒ 取 <see cref="DateTimeOffset.UtcNow"/>）。</summary>
    public DateTimeOffset? FixedNow { get; set; }

    /// <summary>已发出的挑战（测试/诊断）。</summary>
    public PsSessionStore Store => _store;

    public string RoutePrefix => _policy.RoutePrefix;

    public int MaxRequestBodyBytes => _policy.MaxRequestBodyBytes;

    private DateTimeOffset Now => FixedNow ?? DateTimeOffset.UtcNow;

    public async Task<InternalRouteResponse> HandleAsync(
        InternalRouteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Error(ErrorCodes.InvalidArgument, "request-null", null);
        }

        await Task.CompletedTask.ConfigureAwait(false);

        // ---- 面 1/2：Host 与 Origin 校验（在读取任何请求体之前；拒绝零副作用）----
        var host = request.GetHeader("Host");
        if (!_policy.IsHostAllowed(host, out var hostReason))
        {
            return Error(ErrorCodes.PermissionDenied, hostReason, "Host");
        }

        var origin = request.GetHeader("Origin");
        var isHealth = PathEquals(request.Path, InternalPsRoutes.Health);
        if (!_policy.IsOriginAllowed(origin, out var originReason))
        {
            var missing = string.IsNullOrWhiteSpace(origin);
            if (!(isHealth && missing))
            {
                return Error(ErrorCodes.PermissionDenied, originReason, "Origin");
            }
        }

        var path = request.Path;

        // ---- 文件令牌下载：明确拒绝桩（未指定文件权限 ⇒ 标 [VERIFY]，不猜测实现）----
        if (PathEquals(path, InternalPsRoutes.FileToken))
        {
            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                return MethodNotAllowed(request.Method, path);
            }

            return Error(ErrorCodes.NotImplemented, InternalPsRoutes.FileTokenRefusal, "file-token");
        }

        // ---- 健康检查（GET）----
        if (PathEquals(path, InternalPsRoutes.Health))
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return MethodNotAllowed(request.Method, path);
            }

            // 面 3：GET 请求体同样受大小上限约束（超限在传输层拒绝；此处防御式复检）
            var healthBody = request.ReadBody();
            if (healthBody is null)
            {
                return Error(ErrorCodes.InvalidArgument, "body-too-large", "body");
            }

            if (!string.IsNullOrWhiteSpace(healthBody))
            {
                return Error(ErrorCodes.InvalidArgument, "health-takes-no-body", "body");
            }

            return Json(HealthJson(originMissing: string.IsNullOrWhiteSpace(origin)));
        }

        // ---- 其余 op 一律 POST ----
        if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return MethodNotAllowed(request.Method, path);
        }

        var body = request.ReadBody();
        if (body is null)
        {
            return Error(ErrorCodes.InvalidArgument, "body-too-large", "body");
        }

        if (PathEquals(path, InternalPsRoutes.PairRequest))
        {
            return HandlePairRequest(body);
        }

        if (PathEquals(path, InternalPsRoutes.PairConfirm))
        {
            return HandlePairConfirm(body);
        }

        if (PathEquals(path, InternalPsRoutes.SessionOpen))
        {
            return HandleSessionOpen(body);
        }

        if (PathEquals(path, InternalPsRoutes.Ping))
        {
            return HandlePing(body);
        }

        // 前缀内未知路径 ⇒ NOT_FOUND（现役注册码；不新增码）
        return Error(ErrorCodes.NotFound, "route-unknown", path);
    }

    // ================= pair/request =================

    /// <summary>
    /// 握手 op 的**信封**（session 域之外的三种握手消息共同形态）。
    /// 与 ADR D-105 的 session 域最小字段集**分离登记**：握手时尚无 job/document/step/批准，
    /// 故 ADR 十一字段为兼取式（<c>minimalFieldSetScope</c> 在健康检查中原样回显两个作用域）。
    /// </summary>
    private InternalRouteResponse HandlePairRequest(string body)
    {
        if (!TryOpenHandshakeEnvelope(body, out var root, out var requestId, out var error))
        {
            return error!;
        }

        if (!TryGetRequiredString(root, "instanceId", out var instanceId, out error))
        {
            return error!;
        }

        if (!_replay.TryRegister(requestId!, Now))
        {
            return Error(ErrorCodes.RequestTimeout, "replay-request-id", "requestId");
        }

        var outcome = _store.CreateChallenge(instanceId!, Now);
        if (!outcome.Ok)
        {
            return Error(ErrorCodes.PermissionDenied, outcome.Reason ?? "pair-request-refused", "instanceId");
        }

        // 配对码**绝不**出现在 HTTP 响应里（只在 Pro 工作台显示）
        var payload = new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["challenge"] = outcome.Challenge,
            ["challengeTtlSeconds"] = PsChannelPolicy.DefaultPairingCodeTtlSeconds,
            ["pairingCodeDelivery"] = "pro-workbench-only",
            ["requestId"] = requestId,
            ["schemaVersion"] = InternalPsRoutes.SchemaVersion,
        };
        return Json(Serialize(payload));
    }

    // ================= pair/confirm =================

    private InternalRouteResponse HandlePairConfirm(string body)
    {
        if (!TryOpenHandshakeEnvelope(body, out var root, out var requestId, out var error))
        {
            return error!;
        }

        if (!TryGetRequiredString(root, "challenge", out var challenge, out error))
        {
            return error!;
        }

        if (!root.TryGetProperty("pairingCode", out var codeElement)
            || codeElement.ValueKind != JsonValueKind.String)
        {
            return Error(ErrorCodes.InvalidArgument, "missing-field", "pairingCode");
        }

        var pairingCode = codeElement.GetString();

        if (!_replay.TryRegister(requestId!, Now))
        {
            return Error(ErrorCodes.RequestTimeout, "replay-request-id", "requestId");
        }

        var outcome = _store.ConfirmWithPairingCode(challenge, pairingCode, Now);
        if (!outcome.Ok)
        {
            // 凭据失败一律 PERMISSION_DENIED；不区分"挑战不存在"与"配对码错"以外的细节，
            // 且**绝不回显**任何凭据原文（含用户提交的错误配对码）。
            return Error(ErrorCodes.PermissionDenied, outcome.Reason ?? "pair-confirm-refused", "pairingCode");
        }

        var payload = new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["persistentToken"] = outcome.PersistentToken,
            ["instanceId"] = outcome.InstanceId,
            ["authLevel"] = "persistent",
            ["requestId"] = requestId,
        };
        return Json(Serialize(payload));
    }

    // ================= session/open =================

    private InternalRouteResponse HandleSessionOpen(string body)
    {
        if (!TryOpenHandshakeEnvelope(body, out var root, out var requestId, out var error))
        {
            return error!;
        }

        if (!TryGetRequiredString(root, "persistentToken", out var persistentToken, out error))
        {
            return error!;
        }

        var instanceId = root.TryGetProperty("instanceId", out var instanceElement)
                        && instanceElement.ValueKind == JsonValueKind.String
            ? instanceElement.GetString()
            : null;

        if (!_replay.TryRegister(requestId!, Now))
        {
            return Error(ErrorCodes.RequestTimeout, "replay-request-id", "requestId");
        }

        var outcome = _store.OpenSession(persistentToken, instanceId, Now);
        if (!outcome.Ok)
        {
            return Error(ErrorCodes.PermissionDenied, outcome.Reason ?? "session-open-refused", "persistentToken");
        }

        var payload = new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["sessionToken"] = outcome.SessionToken,
            ["instanceId"] = outcome.InstanceId,
            ["authLevel"] = "session",
            ["expiresInSeconds"] = outcome.ExpiresInSeconds,
            ["requestId"] = requestId,
        };
        return Json(Serialize(payload));
    }

    // ================= ping（session 域，ADR D-105 全字段）=================

    private InternalRouteResponse HandlePing(string body)
    {
        var sessionToken = TryReadSessionToken(body);
        var validation = _store.ValidateSession(sessionToken, Now);
        if (!validation.Ok)
        {
            return Error(ErrorCodes.RequestTimeout, validation.Reason ?? "session-token-unknown", "sessionToken");
        }

        var parsed = PsMessage.Parse(body, _policy, Now);
        if (!parsed.Success)
        {
            return Error(parsed.ErrorCode!, parsed.Reason ?? "message-rejected", parsed.Field);
        }

        var message = parsed.Message!;

        // 重放：同 requestId 第二次出现即拒绝（拒绝路径零副作用）
        if (!_replay.TryRegister(message.RequestId, Now))
        {
            return Error(ErrorCodes.RequestTimeout, "replay-request-id", "requestId");
        }

        // 代际单调：递减即拒（旧代际迟到结果一律丢弃）
        if (!_store.TryAcceptFencing(sessionToken, message.FencingGeneration, out var fencingReason))
        {
            return Error(ErrorCodes.PermissionDenied, fencingReason, "fencingGeneration");
        }

        // 拥有关系：[VERIFY] 本批**不认领任何宿主文档**（无 PS 实机）⇒ 非空 documentId 一律拒绝，
        // 不静默接受，也不抢 activeDocument（ADR D-103 第 4 条）。
        if (!string.IsNullOrWhiteSpace(message.DocumentId))
        {
            return Error(ErrorCodes.PermissionDenied, "document-not-owned", "documentId");
        }

        var fields = new Dictionary<string, object?>();
        var note = new Dictionary<string, object?>
        {
            ["jobId"] = "skeleton-not-bound",
            ["documentId"] = "no-owned-document",
            ["specRevision"] = "skeleton-not-bound",
            ["stepId"] = "echo-only",
            ["approvalDigest"] = "not-enforced-in-skeleton",
            ["artifactRefs"] = "path-ownership-validated",
            ["cancellation"] = "three-state-not-implemented",
            ["queue"] = "single-writer-not-implemented",
        };
        fields["requestId"] = message.RequestId;
        fields["op"] = message.Op;
        fields["deadline"] = message.Deadline.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        fields["fencingGeneration"] = message.FencingGeneration;
        fields["typedArgs"] = JsonDocument.Parse(message.TypedArgs.GetRawText()).RootElement.Clone();
        fields["artifactRefs"] = JsonDocument.Parse(message.ArtifactRefs.GetRawText()).RootElement.Clone();

        var payload = new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["ealEcho"] = "skeleton",
            ["requestId"] = message.RequestId,
            ["instanceId"] = validation.InstanceId,
            ["messageFieldSet"] = InternalPsRoutes.MessageFields,
            ["fields"] = fields,
            ["notes"] = note,
        };
        return Json(Serialize(payload));
    }

    // ================= 健康检查 =================

    private string HealthJson(bool originMissing)
    {
        var payload = new Dictionary<string, object?>
        {
            ["status"] = "ok",
            ["service"] = InternalPsRoutes.ServiceName,
            ["version"] = _serverVersion,
            ["route"] = InternalPsRoutes.Health,
            ["routePrefix"] = InternalPsRoutes.Prefix,
            ["schemaVersion"] = InternalPsRoutes.SchemaVersion,
            ["operationCount"] = InternalPsRoutes.ImplementedOps.Length,
            ["ops"] = InternalPsRoutes.ImplementedOps,
            ["messageFieldsEcho"] = InternalPsRoutes.MessageFields,
            ["messageFieldsProposedExt"] = InternalPsRoutes.ProposedExtFields,
            ["minimalFieldSetScope"] = new Dictionary<string, object?>
            {
                ["sessionDomain"] = InternalPsRoutes.MessageFields,
                ["handshake"] = HandshakeEnvelopeFields,
            },
            ["fileToken"] = InternalPsRoutes.FileTokenRefusal,
            ["newListener"] = false,
            ["boundToExistingListener"] = true,
            ["auth"] = "none-for-health-only",
            ["originHeader"] = originMissing ? "absent-allowed-for-health" : "present",
        };
        return Serialize(payload);
    }

    // ================= 握手信封 =================

    private static readonly string[] HandshakeEnvelopeFields =
    {
        "schemaVersion", "requestId", "op", "deadline",
    };

    /// <summary>握手信封允许的 op 名。</summary>
    private static readonly string[] HandshakeOps =
    {
        InternalPsRoutes.OpPairRequest, InternalPsRoutes.OpPairConfirm, InternalPsRoutes.OpSessionOpen,
    };

    /// <summary>握手信封允许的字段全集（未知字段一律拒绝 —— 与 ADR D-105「不静默忽略」同纪律）。</summary>
    private static readonly string[] HandshakeEnvelopeAllowedFields =
    {
        "schemaVersion", "requestId", "op", "deadline",
        "instanceId", "uxpVersion", "hostVersion", "capabilitiesHash",
        "challenge", "pairingCode", "persistentToken",
    };

    private bool TryOpenHandshakeEnvelope(
        string body,
        out JsonElement root,
        out string? requestId,
        out InternalRouteResponse? error)
    {
        root = default;
        requestId = null;
        error = null;

        if (string.IsNullOrWhiteSpace(body))
        {
            error = Error(ErrorCodes.InvalidArgument, "empty-body", null);
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            error = Error(ErrorCodes.InvalidArgument, "not-json", null);
            return false;
        }

        // 注意：JsonDocument 生命周期由 root.Clone() 延续，避免 using 释放后取属性。
        root = document.RootElement.Clone();
        document.Dispose();

        if (root.ValueKind != JsonValueKind.Object)
        {
            error = Error(ErrorCodes.InvalidArgument, "not-object", null);
            return false;
        }

        var allowedHandshake = new HashSet<string>(HandshakeEnvelopeAllowedFields, StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!allowedHandshake.Contains(property.Name))
            {
                error = Error(ErrorCodes.InvalidArgument, "unknown-field", property.Name);
                return false;
            }
        }

        foreach (var field in HandshakeEnvelopeFields)
        {
            if (!root.TryGetProperty(field, out _))
            {
                error = Error(ErrorCodes.InvalidArgument, "missing-field", field);
                return false;
            }
        }

        if (!TryGetRequiredString(root, "schemaVersion", out var schemaVersion, out _))
        {
            error = Error(ErrorCodes.InvalidArgument, "field-not-string", "schemaVersion");
            return false;
        }

        if (!string.Equals(schemaVersion, InternalPsRoutes.SchemaVersion, StringComparison.Ordinal))
        {
            // 版本前缀兼容：不匹配即明确拒绝，不降级为静默失败（ADR D-107）
            error = Error(ErrorCodes.InvalidArgument, "schema-version-mismatch", "schemaVersion");
            return false;
        }

        if (!TryGetRequiredString(root, "requestId", out requestId, out _) || !Guid.TryParse(requestId, out _))
        {
            error = Error(ErrorCodes.InvalidArgument, "requestId-not-guid", "requestId");
            return false;
        }

        if (!TryGetRequiredString(root, "op", out var op, out _))
        {
            error = Error(ErrorCodes.InvalidArgument, "op-empty", "op");
            return false;
        }

        if (op is null || !HandshakeOps.Contains(op, StringComparer.Ordinal))
        {
            // 握手信封的 op 只允许 pair/request · pair/confirm · session/open（白名单外一律拒绝）
            error = Error(ErrorCodes.NotFound, "op-not-allowlisted", "op");
            return false;
        }

        if (!TryGetDeadline(root, out var deadline))
        {
            error = Error(ErrorCodes.InvalidArgument, "deadline-unparsable", "deadline");
            return false;
        }

        if (deadline <= Now)
        {
            error = Error(ErrorCodes.RequestTimeout, "deadline-expired", "deadline");
            return false;
        }

        if (deadline > Now.AddSeconds(_policy.MaxDeadlineHorizonSeconds))
        {
            error = Error(ErrorCodes.InvalidArgument, "deadline-beyond-horizon", "deadline");
            return false;
        }

        return true;
    }

    private static bool TryGetRequiredString(
        JsonElement root, string name, out string? value, out InternalRouteResponse? error)
    {
        value = null;
        error = null;
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            error = Error(ErrorCodes.InvalidArgument, "field-not-string", name);
            return false;
        }

        value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            error = Error(ErrorCodes.InvalidArgument, "field-empty", name);
            return false;
        }

        return true;
    }

    private static bool TryGetDeadline(JsonElement root, out DateTimeOffset deadline)
    {
        deadline = default;
        if (!root.TryGetProperty("deadline", out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(
                element.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out deadline);
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var epochMs))
        {
            deadline = DateTimeOffset.FromUnixTimeMilliseconds(epochMs);
            return true;
        }

        return false;
    }

    private static string? TryReadSessionToken(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return document.RootElement.TryGetProperty("sessionToken", out var element)
                   && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ================= 响应构造 =================

    private static bool PathEquals(string? left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static InternalRouteResponse MethodNotAllowed(string method, string path)
        => Error(ErrorCodes.NotFound, "method-not-allowed:" + method, path);

    private static InternalRouteResponse Json(string json) => InternalRouteResponse.Json(json);

    private static InternalRouteResponse Error(string code, string detail, string? field)
    {
        var payload = new Dictionary<string, object?>
        {
            ["ok"] = false,
            ["code"] = code,
            ["message"] = code + ": " + detail,
            ["details"] = new Dictionary<string, object?>
            {
                ["detail"] = detail,
                ["field"] = field,
            },
        };
        return InternalRouteResponse.Error(Serialize(payload));
    }

    private static string Serialize(object payload)
        => JsonSerializer.Serialize(payload, SerializerOptions);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
