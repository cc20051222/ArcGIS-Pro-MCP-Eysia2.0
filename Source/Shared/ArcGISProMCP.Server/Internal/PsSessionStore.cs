using System.Security.Cryptography;

namespace ArcGISProMCP.Server.Internal;

/// <summary>
/// D-083 组 B——配对与会话状态（ADR D-104 凭据分离）。
/// <para>
/// <b>凭据分离是真实边界，不是命名差异</b>：
/// <list type="number">
/// <item><b>配对挑战</b>（<c>pair/request</c> 一次性挑战）——仅内存，使用即废/超时即废；</item>
/// <item><b>配对码</b>（用户在 Pro 工作台读到）——仅内存，<b>不经 HTTP 下发、不回显、不写日志</b>，
/// 使用即废、超时即废、错误次数上限即废；</item>
/// <item><b>持久令牌</b>（<c>pair/confirm</c> 产出，绑定插件实例，可撤销）——与前者不同值、不同生命周期；</item>
/// <item><b>会话令牌</b>（<c>session/open</c> 产出，短 TTL，心跳续期）——与持久令牌不同值、不同 TTL。</item>
/// </list>
/// 四者前缀不同、生成源不同、TTL 不同；测试逐一断言互不相等。
/// </para>
/// <para>
/// <b>[VERIFY]</b>：UXP 侧受限存储能力（V7-07）、宿主文件访问令牌（V7-08）、
/// 配对首次启用操作次数（V7-09）均**未实测**，本类型只实现 Pro 侧内存侧。
/// </para>
/// </summary>
public sealed class PsSessionStore
{
    private const string ChallengePrefix = "psc1.";
    private const string PersistentPrefix = "psp1.";
    private const string SessionPrefix = "pss1.";
    private const string PairingCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly object _gate = new();
    private readonly Dictionary<string, Challenge> _challenges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PersistentCredential> _persistent = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SessionCredential> _sessions = new(StringComparer.Ordinal);

    private int _challengeTtlSeconds = PsChannelPolicy.DefaultPairingCodeTtlSeconds;
    private int _persistentTtlSeconds = PsChannelPolicy.DefaultPersistentTokenTtlSeconds;
    private int _sessionTtlSeconds = PsChannelPolicy.DefaultSessionTokenTtlSeconds;
    private int _maxPairingCodeAttempts = PsChannelPolicy.DefaultMaxPairingCodeAttempts;

    /// <summary>读出 TTL 配置（供测试收紧窗口；生产用缺省值）。</summary>
    public void Configure(int? challengeTtlSeconds = null, int? persistentTtlSeconds = null,
        int? sessionTtlSeconds = null, int? maxPairingCodeAttempts = null)
    {
        lock (_gate)
        {
            if (challengeTtlSeconds is > 0)
            {
                _challengeTtlSeconds = challengeTtlSeconds.Value;
            }

            if (persistentTtlSeconds is > 0)
            {
                _persistentTtlSeconds = persistentTtlSeconds.Value;
            }

            if (sessionTtlSeconds is > 0)
            {
                _sessionTtlSeconds = sessionTtlSeconds.Value;
            }

            if (maxPairingCodeAttempts is > 0)
            {
                _maxPairingCodeAttempts = maxPairingCodeAttempts.Value;
            }
        }
    }

    /// <summary>挑战会话数（测试/诊断）。</summary>
    public int ChallengeCount { get { lock (_gate) { return _challenges.Count; } } }

    /// <summary>已配对插件实例数（测试/诊断）。</summary>
    public int PairedInstanceCount { get { lock (_gate) { return _persistent.Count; } } }

    /// <summary>活动会话数（测试/诊断）。</summary>
    public int SessionCount { get { lock (_gate) { return _sessions.Count; } } }

    /// <summary>
    /// <c>pair/request</c>：登记插件实例并签发一次性挑战。
    /// </summary>
    public PairRequestOutcome CreateChallenge(string instanceId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return PairRequestOutcome.Refuse("instance-id-empty");
        }

        lock (_gate)
        {
            Prune(now);

            var challenge = ChallengePrefix + NewSecret(24);
            var pairingCode = NewPairingCode();

            _challenges[challenge] = new Challenge
            {
                InstanceId = instanceId,
                PairingCode = pairingCode,
                ExpiresAt = now.AddSeconds(_challengeTtlSeconds),
                Attempts = 0,
            };

            // 配对码**只**经此方法回给 Pro 工作台显示；HTTP 响应永不含它。
            return PairRequestOutcome.Accepted(challenge, pairingCode);
        }
    }

    /// <summary>工作台读出当前最短 TTL 挑战的配对码（仅本机 UI 面；假会话挑战不参与）。</summary>
    public string? GetActivePairingCodeForWorkbench()
    {
        lock (_gate)
        {
            Challenge? best = null;
            foreach (var challenge in _challenges.Values)
            {
                if (best is null || challenge.ExpiresAt < best.ExpiresAt)
                {
                    best = challenge;
                }
            }

            return best?.PairingCode;
        }
    }

    /// <summary>
    /// <c>pair/confirm</c>：一次性挑战 + 配对码 ⇒ 持久令牌。
    /// 挑战一次性（成功即废）、配对码一次性（成功即废）、错误次数上限即废。
    /// </summary>
    public PairConfirmOutcome ConfirmWithPairingCode(string? challenge, string? pairingCode, DateTimeOffset now)
    {
        lock (_gate)
        {
            Prune(now);

            if (string.IsNullOrWhiteSpace(challenge) || !_challenges.TryGetValue(challenge, out var entry))
            {
                return PairConfirmOutcome.Refuse("challenge-unknown");
            }

            if (entry.ExpiresAt <= now)
            {
                _challenges.Remove(challenge);
                return PairConfirmOutcome.Refuse("challenge-expired");
            }

            if (string.IsNullOrWhiteSpace(pairingCode)
                || !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(pairingCode.Trim().ToUpperInvariant()),
                    System.Text.Encoding.UTF8.GetBytes(entry.PairingCode)))
            {
                entry.Attempts++;
                if (entry.Attempts >= _maxPairingCodeAttempts)
                {
                    _challenges.Remove(challenge);
                    return PairConfirmOutcome.Refuse("pairing-code-attempts-exceeded");
                }

                return PairConfirmOutcome.Refuse("pairing-code-mismatch");
            }

            // 成功 ⇒ 挑战与配对码立即作废（一次性）
            _challenges.Remove(challenge);

            var persistent = PersistentPrefix + NewSecret(32);
            _persistent[persistent] = new PersistentCredential
            {
                InstanceId = entry.InstanceId,
                ExpiresAt = now.AddSeconds(_persistentTtlSeconds),
                Revoked = false,
            };

            return PairConfirmOutcome.Accepted(persistent, entry.InstanceId);
        }
    }

    /// <summary><c>session/open</c>：持久令牌 ⇒ 短 TTL 会话令牌（与持久令牌不同值）。</summary>
    public SessionOpenOutcome OpenSession(string? persistentToken, string? instanceId, DateTimeOffset now)
    {
        lock (_gate)
        {
            Prune(now);

            if (string.IsNullOrWhiteSpace(persistentToken)
                || !_persistent.TryGetValue(persistentToken, out var credential))
            {
                return SessionOpenOutcome.Refuse("persistent-token-unknown");
            }

            if (credential.Revoked)
            {
                return SessionOpenOutcome.Refuse("persistent-token-revoked");
            }

            if (credential.ExpiresAt <= now)
            {
                _persistent.Remove(persistentToken);
                return SessionOpenOutcome.Refuse("persistent-token-expired");
            }

            if (!string.IsNullOrWhiteSpace(instanceId)
                && !string.Equals(instanceId, credential.InstanceId, StringComparison.Ordinal))
            {
                return SessionOpenOutcome.Refuse("instance-mismatch");
            }

            var session = SessionPrefix + NewSecret(32);
            _sessions[session] = new SessionCredential
            {
                InstanceId = credential.InstanceId,
                ExpiresAt = now.AddSeconds(_sessionTtlSeconds),
                FencingGeneration = 0,
            };

            return SessionOpenOutcome.Accepted(session, credential.InstanceId, _sessionTtlSeconds);
        }
    }

    /// <summary>会话令牌校验（含心跳续期）；失败原因机器可判。</summary>
    public SessionValidation ValidateSession(string? sessionToken, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(sessionToken) || !_sessions.TryGetValue(sessionToken, out var session))
            {
                return new SessionValidation(false, "session-token-unknown", null);
            }

            if (session.ExpiresAt <= now)
            {
                _sessions.Remove(sessionToken);
                return new SessionValidation(false, "session-token-expired", null);
            }

            // 心跳续期（短 TTL + 心跳；失联即过期）
            session.ExpiresAt = now.AddSeconds(_sessionTtlSeconds);
            return new SessionValidation(true, null, session.InstanceId);
        }
    }

    /// <summary>
    /// <c>fencingGeneration</c> 单调性检查：**递减即拒**；等于上一代际允许（幂等重发同一步骤）。
    /// </summary>
    public bool TryAcceptFencing(string? sessionToken, int generation, out string reason)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(sessionToken) || !_sessions.TryGetValue(sessionToken, out var session))
            {
                reason = "session-token-unknown";
                return false;
            }

            if (generation < session.FencingGeneration)
            {
                reason = "fencing-generation-regressed";
                return false;
            }

            session.FencingGeneration = generation;
            reason = "fencing-accepted";
            return true;
        }
    }

    /// <summary>工作台撤销配对（用户面动作；模型与 UXP 都不能自行撤销或延长授权）。</summary>
    public int RevokeAllPairings(DateTimeOffset now)
    {
        lock (_gate)
        {
            var revoked = 0;
            foreach (var credential in _persistent.Values)
            {
                if (!credential.Revoked)
                {
                    credential.Revoked = true;
                    revoked++;
                }
            }

            _sessions.Clear();
            _challenges.Clear();
            Prune(now);
            return revoked;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        List<string>? dead = null;
        foreach (var pair in _challenges)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                (dead ??= new List<string>()).Add(pair.Key);
            }
        }

        if (dead is not null)
        {
            foreach (var key in dead)
            {
                _challenges.Remove(key);
            }
        }

        dead = null;
        foreach (var pair in _persistent)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                (dead ??= new List<string>()).Add(pair.Key);
            }
        }

        if (dead is not null)
        {
            foreach (var key in dead)
            {
                _persistent.Remove(key);
            }
        }

        dead = null;
        foreach (var pair in _sessions)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                (dead ??= new List<string>()).Add(pair.Key);
            }
        }

        if (dead is not null)
        {
            foreach (var key in dead)
            {
                _sessions.Remove(key);
            }
        }
    }

    private static string NewSecret(int bytes)
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();

    private static string NewPairingCode()
    {
        var buffer = new char[8];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = PairingCodeAlphabet[RandomNumberGenerator.GetInt32(PairingCodeAlphabet.Length)];
        }

        return new string(buffer);
    }

    private sealed class Challenge
    {
        public string InstanceId { get; init; } = string.Empty;
        public string PairingCode { get; init; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public int Attempts { get; set; }
    }

    private sealed class PersistentCredential
    {
        public string InstanceId { get; init; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public bool Revoked { get; set; }
    }

    private sealed class SessionCredential
    {
        public string InstanceId { get; init; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public int FencingGeneration { get; set; }
    }
}

/// <summary>会话令牌校验结果。</summary>
public readonly record struct SessionValidation(bool Ok, string? Reason, string? InstanceId);

/// <summary><c>pair/request</c> 结果。<see cref="PairingCode"/> 只供 Pro 工作台显示，绝不出现在 HTTP 响应。</summary>
public readonly record struct PairRequestOutcome(
    bool Ok, string? Challenge, string? PairingCode, string? Reason)
{
    public static PairRequestOutcome Refuse(string reason) => new(false, null, null, reason);
    public static PairRequestOutcome Accepted(string challenge, string pairingCode)
        => new(true, challenge, pairingCode, null);
}

/// <summary><c>pair/confirm</c> 结果。</summary>
public readonly record struct PairConfirmOutcome(
    bool Ok, string? PersistentToken, string? InstanceId, string? Reason)
{
    public static PairConfirmOutcome Refuse(string reason) => new(false, null, null, reason);

    /// <summary>D-083：确认成功（持久令牌 ＋ 插件实例）。</summary>
    public static PairConfirmOutcome Accepted(string persistentToken, string instanceId)
        => new(true, persistentToken, instanceId, null);
}

/// <summary><c>session/open</c> 结果。</summary>
public readonly record struct SessionOpenOutcome(
    bool Ok, string? SessionToken, string? InstanceId, int ExpiresInSeconds, string? Reason)
{
    public static SessionOpenOutcome Refuse(string reason) => new(false, null, null, 0, reason);
    public static SessionOpenOutcome Accepted(string sessionToken, string instanceId, int expiresInSeconds)
        => new(true, sessionToken, instanceId, expiresInSeconds, null);
}
