using System.Text.RegularExpressions;

namespace ArcGISProMCP.Server.Internal;

/// <summary>
/// D-083 组 B——PS 通道校验策略（ADR D-106 校验矩阵的**配置单一来源**）。
/// <para>
/// 所有上限/口径集中在此类型，禁止散落到处理器分支里；测试与本策略取同一常量。
/// 策略对象**不持有任何监听器/端口**：内部路由挂在现役监听上（B-04）。
/// </para>
/// </summary>
public sealed class PsChannelPolicy
{
    /// <summary>缺省请求体硬上限（UTF-8 字节）。超限**拒绝而非截断**（ADR D-106「大小校验」）。</summary>
    public const int DefaultMaxRequestBodyBytes = 256 * 1024;

    /// <summary>缺省 <c>typedArgs</c> 序列化后硬上限（UTF-8 字节）。</summary>
    public const int DefaultMaxTypedArgsBytes = 64 * 1024;

    /// <summary>缺省 <c>artifactRefs</c> 元素数上限。</summary>
    public const int DefaultMaxArtifactRefs = 32;

    /// <summary>缺省重放缓存容量上限（超出即拒绝新请求，fail-closed）。</summary>
    public const int DefaultMaxReplayEntries = 4096;

    /// <summary>缺省 <c>deadline</c> 允许的未来窗口（防止远期时间戳绕过重放窗口）。</summary>
    public const int DefaultMaxDeadlineHorizonSeconds = 300;

    /// <summary>缺省配对码 TTL（分钟级；ADR D-104 ≤10 min 拟定值，F04-A 定标）。</summary>
    public const int DefaultPairingCodeTtlSeconds = 10 * 60;

    /// <summary>缺省持久令牌 TTL（长期；可由工作台撤销）。</summary>
    public const int DefaultPersistentTokenTtlSeconds = 30 * 24 * 60 * 60;

    /// <summary>缺省会话令牌 TTL（会话级短 TTL）。</summary>
    public const int DefaultSessionTokenTtlSeconds = 30 * 60;

    /// <summary>缺省配对码错误次数上限（达到即废，需重新配对）。</summary>
    public const int DefaultMaxPairingCodeAttempts = 5;

    /// <summary>
    /// <b>[VERIFY] V7-12</b>：UXP WebView 下 <c>Origin</c> 的实际取值**未实测**
    /// （ADR §5 V7-12、D-102 [VERIFY]）。此处按 Adobe 公开的 UXP 宿主来源形态
    /// <c>uxp://&lt;host&gt;</c> 立<strong>保守允许式</strong>：不放通 <c>*</c>、不放通 <c>null</c>、
    /// 不放通任意 http(s) 远端域；仅放通 UXP 来源与非环回 http(s) 均拒绝。
    /// F04-A 抓到真实 Origin 前不得以此断定为"已确认"。
    /// </summary>
    public const string UxpOriginPattern = "^uxp://[A-Za-z0-9._-]+$";

    private static readonly string[] DefaultAllowedHosts = { "127.0.0.1", "localhost" };

    private readonly Regex[] _originPatterns;
    private readonly string[] _allowedHosts;

    public PsChannelPolicy(
        string routePrefix = InternalPsRoutes.Prefix,
        IEnumerable<string>? allowedHosts = null,
        IEnumerable<string>? originPatterns = null,
        int maxRequestBodyBytes = DefaultMaxRequestBodyBytes,
        int maxTypedArgsBytes = DefaultMaxTypedArgsBytes,
        int maxArtifactRefs = DefaultMaxArtifactRefs,
        int maxReplayEntries = DefaultMaxReplayEntries,
        int maxDeadlineHorizonSeconds = DefaultMaxDeadlineHorizonSeconds,
        string? ownedArtifactRoot = null)
    {
        RoutePrefix = string.IsNullOrWhiteSpace(routePrefix) ? InternalPsRoutes.Prefix : routePrefix.TrimEnd('/');
        _allowedHosts = (allowedHosts ?? DefaultAllowedHosts)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (_allowedHosts.Length == 0)
        {
            _allowedHosts = DefaultAllowedHosts;
        }

        var patterns = (originPatterns ?? new[] { UxpOriginPattern }).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        _originPatterns = patterns.Length == 0
            ? new[] { new Regex(UxpOriginPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant) }
            : patterns.Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.CultureInvariant)).ToArray();

        MaxRequestBodyBytes = maxRequestBodyBytes > 0 ? maxRequestBodyBytes : DefaultMaxRequestBodyBytes;
        MaxTypedArgsBytes = maxTypedArgsBytes > 0 ? maxTypedArgsBytes : DefaultMaxTypedArgsBytes;
        MaxArtifactRefs = maxArtifactRefs > 0 ? maxArtifactRefs : DefaultMaxArtifactRefs;
        MaxReplayEntries = maxReplayEntries > 0 ? maxReplayEntries : DefaultMaxReplayEntries;
        MaxDeadlineHorizonSeconds = maxDeadlineHorizonSeconds > 0
            ? maxDeadlineHorizonSeconds
            : DefaultMaxDeadlineHorizonSeconds;
        OwnedArtifactRoot = string.IsNullOrWhiteSpace(ownedArtifactRoot) ? null : ownedArtifactRoot!.Trim();
    }

    /// <summary>路由前缀（默认 <c>/internal/ps/v1</c>）。</summary>
    public string RoutePrefix { get; }

    /// <summary>允许的 Host 主机名集合（回环口径；端口在比较时剥离）。</summary>
    public IReadOnlyList<string> AllowedHosts => _allowedHosts;

    /// <summary>允许的 Origin 正则集合（缺省仅 UXP 来源；绝不含 <c>*</c>）。</summary>
    public IReadOnlyList<string> OriginPatterns => _originPatterns.Select(p => p.ToString()).ToArray();

    public int MaxRequestBodyBytes { get; }

    public int MaxTypedArgsBytes { get; }

    public int MaxArtifactRefs { get; }

    public int MaxReplayEntries { get; }

    public int MaxDeadlineHorizonSeconds { get; }

    /// <summary>
    /// 拥有的产物根（**必须**是已探针的 D 盘拥有根；见 ADR D-106「路径拥有关系」）。
    /// <c>null</c> ⇒ 路径校验 fail-closed（任何产物路径拒绝），绝不静默放行。
    /// </summary>
    public string? OwnedArtifactRoot { get; }

    /// <summary>
    /// Host 校验（ADR D-106 面 1）：仅接受本机回环 Host；剥离端口与 IPv6 方括号后再比对。
    /// 无公网/局域网地址可放通。
    /// </summary>
    public bool IsHostAllowed(string? hostHeader, out string reason)
    {
        if (string.IsNullOrWhiteSpace(hostHeader))
        {
            reason = "host-missing";
            return false;
        }

        var host = hostHeader.Trim();

        // IPv6 形如 [::1]:6520 ⇒ 去括号取主机
        if (host.StartsWith('['))
        {
            var close = host.IndexOf(']');
            host = close > 1 ? host[1..close] : host.Trim('[', ']');
        }
        else
        {
            var colon = host.LastIndexOf(':');
            if (colon > 0 && host.IndexOf(':') == colon)
            {
                host = host[..colon];
            }
        }

        foreach (var allowed in _allowedHosts)
        {
            if (string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase))
            {
                reason = "host-allowed:" + allowed;
                return true;
            }
        }

        // 回环家族的其余写法（::1）在缺省集合下视为回环，但仍需显式在允许集合内才放通 ⇒ 不放通。
        reason = "host-not-loopback:" + host;
        return false;
    }

    /// <summary>
    /// Origin 校验（ADR D-106 面 2）：只接受 UXP 面板来源；<c>null</c>/未知/通配一律拒绝；不设 <c>*</c>。
    /// </summary>
    public bool IsOriginAllowed(string? originHeader, out string reason)
    {
        if (string.IsNullOrWhiteSpace(originHeader))
        {
            reason = "origin-missing";
            return false;
        }

        var origin = originHeader.Trim();
        if (origin == "*" || origin.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            reason = "origin-forbidden:" + origin;
            return false;
        }

        foreach (var pattern in _originPatterns)
        {
            if (pattern.IsMatch(origin))
            {
                reason = "origin-allowed";
                return true;
            }
        }

        reason = "origin-not-allowlisted:" + origin;
        return false;
    }

    /// <summary>
    /// 路径拥有关系校验（ADR D-106 面 4）：产物路径必须落在**拥有根**下，
    /// 且不得命中现役受保护根（复用 <c>ProtectedOutputPathGuard</c> 同一语义）。
    /// </summary>
    public bool IsPathOwned(string? path, out string reason)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "path-empty";
            return false;
        }

        if (OwnedArtifactRoot is null)
        {
            reason = "owned-root-unresolved";
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            reason = "path-not-normalizable";
            return false;
        }

        // 归一化必须构成闭合（\..\ 消解后仍原样 ⇒ 无绕行）
        if (!string.Equals(full, path, StringComparison.OrdinalIgnoreCase)
            && path.IndexOf("..", StringComparison.Ordinal) >= 0)
        {
            reason = "path-traversal";
            return false;
        }

        var root = OwnedArtifactRoot;
        if (!(full.Equals(root, StringComparison.OrdinalIgnoreCase)
              || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
              || full.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
        {
            reason = "path-outside-owned-root";
            return false;
        }

        // D-083：Server 不引用 Tools 程序集（分层 Server → Core）⇒ 以**本地等价实现**复现现役守护语义
        // （受保护根 / 重解析点），避免新增项目依赖或安全旁路。
        var protectedHit = LocalProtectedPathHit(full);
        if (protectedHit is not null)
        {
            reason = "path-protected:" + protectedHit;
            return false;
        }

        reason = "path-owned";
        return true;
    }

    /// <summary>D-083：本地受保护根 / 重解析点判定（等价于现役守护的最小面；Server 不引用 Tools 程序集）。</summary>
    public static string? LocalProtectedPathHit(string full)
    {
        var f = Path.GetFullPath(full);
        var parts = f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var segment in parts)
        {
            if (string.Equals(segment, "TestFixtures", StringComparison.OrdinalIgnoreCase))
            {
                return "protected-root:TestFixtures";
            }

            if (string.Equals(segment, "ArcGIS-Pro-MCP", StringComparison.OrdinalIgnoreCase))
            {
                return "protected-root:legacy-repo";
            }
        }

        try
        {
            var current = new DirectoryInfo(Path.GetDirectoryName(f) ?? f);
            var guard = 0;
            while (current is not null && guard++ < 64)
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return "reparse-point:" + current.Name;
                }

                current = current.Parent;
            }
        }
        catch
        {
            return "reparse-probe-failed";
        }

        return null;
    }
}
