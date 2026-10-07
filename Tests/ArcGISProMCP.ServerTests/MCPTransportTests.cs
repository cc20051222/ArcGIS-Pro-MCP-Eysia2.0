using System.Net;
using System.Text;
using ArcGISProMCP.Server.Transport;

namespace ArcGISProMCP.ServerTests;

/// <summary>HTTP MCP 传输测试（真实绑定 127.0.0.1:16521）。</summary>
public class MCPTransportTests
{
    private const int TestPort = 16521;

    private static HttpMcpTransport CreateTransport() => TestServerFactory.CreateHttpTransport(TestPort);

    [Fact]
    public async Task Start_Then_IsRunning_True()
    {
        var transport = CreateTransport();
        try
        {
            await transport.StartAsync();
            Assert.True(transport.IsRunning);
        }
        finally
        {
            await transport.StopAsync();
        }
    }

    [Fact]
    public async Task Stop_Then_IsRunning_False()
    {
        var transport = CreateTransport();
        await transport.StartAsync();
        await transport.StopAsync();

        Assert.False(transport.IsRunning);
    }

    [Fact]
    public async Task DoubleStart_Is_Idempotent()
    {
        var transport = CreateTransport();
        try
        {
            await transport.StartAsync();
            await transport.StartAsync();
            Assert.True(transport.IsRunning);
        }
        finally
        {
            await transport.StopAsync();
        }
    }

    [Fact]
    public async Task DoubleStop_Is_Idempotent()
    {
        var transport = CreateTransport();
        await transport.StartAsync();
        await transport.StopAsync();
        await transport.StopAsync();

        Assert.False(transport.IsRunning);
    }

    [Fact]
    public async Task POST_Returns_Response()
    {
        var transport = CreateTransport();
        transport.RequestHandler = (_, _) => Task.FromResult<string?>("""{"echo":true}""");

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{TestPort}/") };
        try
        {
            await transport.StartAsync();

            using var content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"initialize"}""", Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/mcp", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("echo", body);
        }
        finally
        {
            await transport.StopAsync();
        }
    }

    [Fact]
    public async Task InvalidContentType_Returns_415()
    {
        var transport = CreateTransport();
        transport.RequestHandler = (_, _) => Task.FromResult<string?>("""{"echo":true}""");

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{TestPort}/") };
        try
        {
            await transport.StartAsync();

            using var content = new StringContent("hello", Encoding.UTF8, "text/plain");
            var response = await client.PostAsync("/mcp", content);

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }
        finally
        {
            await transport.StopAsync();
        }
    }

    [Fact]
    public async Task MalformedJSON_Returns_ParseError()
    {
        var server = TestServerFactory.Create();
        var transport = CreateTransport();
        transport.RequestHandler = server.HandleRequestAsync;

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{TestPort}/") };
        try
        {
            await transport.StartAsync();

            using var content = new StringContent("{ not valid json", Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/mcp", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("-32700", body);
        }
        finally
        {
            await transport.StopAsync();
        }
    }
}
