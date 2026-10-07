using System.Net;
using ArcGISProMCP.Server.Internal;
using ArcGISProMCP.Server.Transport;

namespace ArcGISProMCP.ServerTests;

/// <summary>
/// D-083 B 组（A-13）· PS 内部路由最小验证版单测。
/// 处理器按设计**不持有监听器**（请求/响应为纯数据）⇒ 无需端口即可测；
/// B-04 恒定：路由是**现役监听上的新路径**（同 host:port），不新增监听/不放通 0.0.0.0。
/// </summary>
public sealed class D083PsInternalRouteTests
{
    private static PsChannelHandler Handler()
        => new(new PsChannelPolicy(), new PsSessionStore(), "1.0.2", DateTimeOffset.UnixEpoch.AddYears(56));

    private static InternalRouteRequest Get(string path, params (string Name, string Value)[] headers)
    {
        var dict = headers.ToDictionary(h => h.Name, h => h.Value, StringComparer.OrdinalIgnoreCase);
        return new InternalRouteRequest
        {
            Method = "GET",
            Path = path,
            GetHeader = n => dict.TryGetValue(n, out var v) ? v : null,
            RemoteEndpoint = "127.0.0.1:55555",
            ReadBody = () => string.Empty,
        };
    }

    private static InternalRouteRequest Post(string path, string body, params (string Name, string Value)[] headers)
    {
        var dict = headers.ToDictionary(h => h.Name, h => h.Value, StringComparer.OrdinalIgnoreCase);
        return new InternalRouteRequest
        {
            Method = "POST",
            Path = path,
            GetHeader = n => dict.TryGetValue(n, out var v) ? v : null,
            RemoteEndpoint = "127.0.0.1:55555",
            ReadBody = () => body,
        };
    }

    // ── B-04：路由为现役监听上的新路径（不是新监听/新端口） ──

    [Fact]
    public void RoutePrefix_IsSubPathOfTheExistingEndpoint()
    {
        var h = Handler();
        Assert.StartsWith("/internal/ps/v1", h.RoutePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", h.RoutePrefix, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0.0.0.0", h.RoutePrefix, StringComparison.Ordinal);
        Assert.True(h.MaxRequestBodyBytes > 0);
    }

    // ── 健康检查（只读）──

    [Fact]
    public async Task Health_Get_ReturnsOkWithServiceIdentity()
    {
        // 校验矩阵的 Host/Origin 面生效：带合法本地 Host 才通过
        var r = await Handler().HandleAsync(Get(InternalPsRoutes.Health, ("Host", "127.0.0.1:6520")));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains(InternalPsRoutes.ServiceName, r.Body, StringComparison.Ordinal);
        Assert.Contains(InternalPsRoutes.SchemaVersion, r.Body, StringComparison.Ordinal);
        Assert.Contains(InternalPsRoutes.Prefix, r.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_WrongMethod_IsRefused()
    {
        var r = await Handler().HandleAsync(Post(InternalPsRoutes.Health, "{}"));
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Health_ForeignHost_IsRefused()
    {
        // 校验矩阵（Host/Origin）：非本地 Host ⇒ 拒绝
        var r = await Handler().HandleAsync(Get(InternalPsRoutes.Health, ("Host", "evil.example.com")));
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Health_NoHostHeader_IsRefused()
    {
        var r = await Handler().HandleAsync(Get(InternalPsRoutes.Health));
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    // ── 未知 op / 未认领路径 ⇒ 拒绝（fail-closed，不静默） ──

    [Fact]
    public async Task UnknownRoute_IsRefused()
    {
        var r = await Handler().HandleAsync(Get(InternalPsRoutes.Prefix + "/nope", ("Host", "127.0.0.1:6520")));
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("\"ok\":false", r.Body, StringComparison.Ordinal);   // 拒止且机器可读（非裸 500）
    }

    [Fact]
    public async Task UnknownOp_IsRefused()
    {
        var r = await Handler().HandleAsync(Post(InternalPsRoutes.Prefix + "/execute", """{"op":"runArbitraryJs"}"""));
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        // B-05：不存在「执行任意 JS / 任意 actionJSON / 任意 URL」入口
        Assert.DoesNotContain("runArbitraryJs", r.Body, StringComparison.Ordinal);
    }

    // ── 文件令牌：ADR [VERIFY] 未证 ⇒ 明确拒止（不伪装实现） ──

    [Fact]
    public async Task FileToken_IsRefusedPendingVerify()
    {
        var r = await Handler().HandleAsync(Post(InternalPsRoutes.FileToken, "{}", ("Host", "127.0.0.1:6520")));
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("\"ok\":false", r.Body, StringComparison.Ordinal);   // [VERIFY] 未证 ⇒ 明确拒止、不伪装实现
    }

    // ── 配对握手骨架：错误配对码必须拒绝（不得放过） ──

    [Fact]
    public async Task PairConfirm_WithWrongCode_IsRefused()
    {
        var h = Handler();
        var r = await h.HandleAsync(Post(InternalPsRoutes.PairConfirm,
            """{"requestId":"r1","challenge":"nope","pairingCode":"000000","deadline":"2099-01-01T00:00:00Z"}"""));
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    // ── 本地守护（受保护根）——Server 不引用 Tools，故为本地等价实现 ──

    [Theory]
    [InlineData(@"D:\ArcGIS-Pro-MCP 2.0\TestFixtures\a.bin", true)]
    [InlineData(@"D:\ArcGIS-Pro-MCP\old\a.bin", true)]
    [InlineData(@"D:\ArcGIS-Pro-MCP 2.0\.runtime\evolution\x.bin", false)]
    public void LocalProtectedPathHit_MatchesGuardSemantics(string path, bool expectHit)
    {
        var hit = PsChannelPolicy.LocalProtectedPathHit(path);
        Assert.Equal(expectHit, hit is not null);
    }

    // ── 策略默认值自洽（上限集中单一来源） ──

    [Fact]
    public void Policy_Defaults_AreSingleSourcedAndPositive()
    {
        var p = new PsChannelPolicy();
        Assert.True(p.MaxRequestBodyBytes > 0);
        Assert.Equal(PsChannelPolicy.DefaultMaxRequestBodyBytes, p.MaxRequestBodyBytes);
        Assert.Equal(InternalPsRoutes.Prefix, p.RoutePrefix);
    }
}
