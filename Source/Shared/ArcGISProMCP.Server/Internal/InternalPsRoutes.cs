namespace ArcGISProMCP.Server.Internal;

/// <summary>
/// D-083 组 B（PS 通道 ADR <c>f03b-7</c> D-101）——内部路由表常量。
/// <para>
/// <b>B-04 恒定（不可协商）</b>：这些路由是**现役监听上的新路径**（同一 <c>HttpListener</c>、
/// 同一 <c>127.0.0.1:6520</c>、同一 <c>ArcGISProMCP.Configuration</c> 解析链），
/// **绝不新增 listener / 绝不新增端口 / 绝不放通 0.0.0.0**；现役 <c>/mcp</c> 契约语义零变化。
/// </para>
/// <para>
/// <b>B-05 恒定</b>：路由只服务**有限 op 集**；不提供"执行任意 JS / 任意 actionJSON / 任意 URL"入口
/// （ADR D-101 末段、FINAL 路线图 §3 M13）。任何 <c>op</c> 白名单外取值一律整条拒绝。
/// </para>
/// </summary>
public static class InternalPsRoutes
{
    /// <summary>内部 PS 通道路由前缀（版本化）。</summary>
    public const string Prefix = "/internal/ps/v1";

    /// <summary>服务标识（健康检查回显；非工具名）。</summary>
    public const string ServiceName = "arcgis-pro-mcp-internal-ps-channel";

    /// <summary>通道协议 schema 版本（PROPOSED-EXT：ADR D-105 建议冻结项）。</summary>
    public const string SchemaVersion = "ps-channel/1";

    // ---- 路由 ----

    /// <summary>GET 只读健康检查（不需配对令牌；仍受 Host/Origin 校验面约束）。</summary>
    public const string Health = Prefix + "/health";

    /// <summary>POST 配对挑战请求（一次性挑战，短 TTL）。</summary>
    public const string PairRequest = Prefix + "/pair/request";

    /// <summary>POST 配对确认（用户在 Pro 工作台读到的配对码 =&gt; 持久令牌）。</summary>
    public const string PairConfirm = Prefix + "/pair/confirm";

    /// <summary>POST 会话开启（持久令牌 =&gt; 短 TTL 会话令牌）。</summary>
    public const string SessionOpen = Prefix + "/session/open";

    /// <summary>POST 会话域回声/心跳（需会话令牌；ADR 消息最小字段集强校验）。</summary>
    public const string Ping = Prefix + "/ping";

    /// <summary>
    /// POST 文件访问令牌下载 —— **本批拒绝桩**（见 <see cref="FileTokenRefusal"/>）。
    /// ADR D-104 的文件令牌由宿主/UXP 管理，其获取/续期/失效信号为 <c>[VERIFY]</c>（V7-08）。
    /// </summary>
    public const string FileToken = Prefix + "/file-token";

    /// <summary>本批已实现（探测可达且进入校验）的 op 名——内网只暴露枚举，不暴露表达式。</summary>
    public const string OpHealth = "health";
    public const string OpPairRequest = "pair/request";
    public const string OpPairConfirm = "pair/confirm";
    public const string OpSessionOpen = "session/open";
    public const string OpPing = "ping";
    public const string OpFileToken = "file-token";

    /// <summary>已实现 op 白名单（op 枚举，不接受任何表达式/脚本/URL）。</summary>
    public static readonly string[] ImplementedOps =
    {
        OpHealth, OpPairRequest, OpPairConfirm, OpSessionOpen, OpPing,
    };

    /// <summary>ADR D-105 消息最小字段集（顺序与 ADR 表一致，供健康检查回显与解析器共用）。</summary>
    public static readonly string[] MessageFields =
    {
        "requestId", "jobId", "documentId", "specRevision", "stepId", "op",
        "typedArgs", "artifactRefs", "deadline", "approvalDigest", "fencingGeneration",
    };

    /// <summary>PROPOSED-EXT 附加字段（ADR D-105 末段；未升格为冻结项）。</summary>
    public static readonly string[] ProposedExtFields =
    {
        "schemaVersion", "authLevel", "leaseId",
    };

    /// <summary>
    /// 文件令牌下载的**拒绝口径**（原样出现在错误 details 与健康检查响应中）：
    /// 该流程依赖未证实的宿主文件权限语义（ADR V7-08 / F04-A 文件权限族），
    /// 依派工单「未指定文件权限即标 [VERIFY] 并留明确桩/拒绝」处置 —— 本批不实现、不猜测。
    /// </summary>
    public const string FileTokenRefusal = "file-token-refused-pending-verify-v7-08";

    /// <summary>路径是否落在内部路由前缀下（大小写不敏感；段边界严格，<c>/internal/ps/v1x</c> 不算）。</summary>
    public static bool IsUnderPrefix(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (path.Equals(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith(Prefix + "/", StringComparison.OrdinalIgnoreCase);
    }
}
