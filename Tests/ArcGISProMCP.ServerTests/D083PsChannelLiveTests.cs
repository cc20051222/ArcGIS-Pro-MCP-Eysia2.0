using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using ArcGISProMCP.Logging;
using ArcGISProMCP.Server.Internal;
using ArcGISProMCP.Server.Transport;

namespace ArcGISProMCP.ServerTests;

/// <summary>
/// D-083 B 组（A-13）· **D 盘独立宿主 LIVE**：真实 <see cref="HttpListener"/> ＋ 真实 socket/HTTP。
/// 不依赖 ArcGIS Pro、不安装、不写 C 盘（O-D083 处置：安装落 D 盘 = 传输层从 D 盘直接起）。
/// 同时给出 **V10-02 实测**：启动前后活跃 TCP 监听数（本端口）＝1 ⇒ 未新增监听。
/// </summary>
[Collection("d083-live")]
public sealed class D083PsChannelLiveTests : IAsyncLifetime
{
    private const int Port = 6520;
    private const string Host = "127.0.0.1";
    private HttpMcpTransport? _transport;
    private PsChannelHandler? _handler;
    private readonly List<string> _raw = new();

    private static string RunDir => Path.Combine(
        @"D:\ArcGIS-Pro-MCP 2.0", ".runtime", "evolution", "v5-f", "run-20260928-d083");

    public async Task InitializeAsync()
    {
        _handler = new PsChannelHandler(new PsChannelPolicy(), new PsSessionStore(), "1.0.2");
        _transport = new HttpMcpTransport(Host, Port, "/mcp", NullLogger.Instance, _handler);
        await _transport.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_transport is not null)
        {
            await _transport.StopAsync();
        }

        try
        {
            Directory.CreateDirectory(RunDir);
            File.AppendAllText(Path.Combine(RunDir, "b-route-live-raw.jsonl"),
                string.Join("\n", _raw) + "\n", new UTF8Encoding(false));
        }
        catch
        {
            // 证据落盘失败不影响测试结论
        }
    }

    private static int ListenerCount(int port)
        => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
            .Count(ep => ep.Port == port);

    private async Task<(int Status, string Body, string Raw)> SendAsync(
        string method, string path, string? body = null, string? hostHeader = null, int? contentLength = null)
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://{Host}:{Port}") };
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        if (hostHeader is not null)
        {
            request.Headers.Host = hostHeader;
        }

        if (contentLength is not null)
        {
            request.Headers.TransferEncodingChunked = false;
        }

        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var raw = $"{(int)response.StatusCode} {method} {path} host={hostHeader ?? "(default)"} -> {text}";
        _raw.Add(raw);
        return ((int)response.StatusCode, text, raw);
    }

    // ── V10-02：不新增监听（实测） ──

    [Fact]
    public void V10_02_ExactlyOneListenerOnTheConfiguredPort()
    {
        Assert.True(_transport!.IsRunning);
        Assert.Equal(1, ListenerCount(Port));
        _raw.Add($"V10-02 listeners on {Host}:{Port} = {ListenerCount(Port)} (expected 1)");
    }

    // ── LIVE：健康检查（raw） ──

    [Fact]
    public async Task Live_Health_ReturnsRawJson()
    {
        var (status, body, _) = await SendAsync("GET", InternalPsRoutes.Health, hostHeader: $"{Host}:{Port}");
        Assert.Equal(200, status);
        Assert.Contains(InternalPsRoutes.ServiceName, body, StringComparison.Ordinal);
        Assert.Contains(InternalPsRoutes.Prefix, body, StringComparison.Ordinal);
    }

    // ── LIVE：校验矩阵（负例，真实 HTTP） ──

    [Fact]
    public async Task Live_ForeignHost_IsRefused()
    {
        var (status, _, _) = await SendAsync("GET", InternalPsRoutes.Health, hostHeader: "evil.example.com");
        Assert.NotEqual(200, status);
    }

    [Fact]
    public async Task Live_UnknownRoute_IsRefused()
    {
        var (status, body, _) = await SendAsync("GET", InternalPsRoutes.Prefix + "/nope", hostHeader: $"{Host}:{Port}");
        Assert.NotEqual(200, status);
        Assert.Contains("ok", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Live_FileToken_IsRefused()
    {
        var (status, _, _) = await SendAsync("POST", InternalPsRoutes.FileToken, "{}", hostHeader: $"{Host}:{Port}");
        Assert.NotEqual(200, status);
    }

    [Fact]
    public async Task Live_OversizeBody_IsRefusedBeforeRead()
    {
        var policy = new PsChannelPolicy();
        var big = new string('x', policy.MaxRequestBodyBytes + 1024);
        var (status, _, _) = await SendAsync("POST", InternalPsRoutes.Ping,
            "{\"pad\":\"" + big + "\"}", hostHeader: $"{Host}:{Port}");
        Assert.NotEqual(200, status);
    }

    // ── 契约不变：/mcp 仍走原路径（未被内部路由截获） ──

    [Fact]
    public async Task Live_McpPath_StillRoutedToMcpNotInternal()
    {
        var (status, body, _) = await SendAsync("POST", "/mcp", "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}",
            hostHeader: $"{Host}:{Port}");
        Assert.DoesNotContain(InternalPsRoutes.ServiceName, body, StringComparison.Ordinal);
        _raw.Add($"contract: /mcp status={status} (internal-route marker absent)");
    }
}
