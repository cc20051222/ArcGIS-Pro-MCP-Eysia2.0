using System.Net;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.Server;
using ArcGISProMCP.Server.Transport;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.ServerTests;

/// <summary>
/// D-084 B1 independent-host LIVE: real loopback HttpListener and MCP JSON-RPC,
/// using FakeArcGISHost so no ArcGIS Pro SDK or user GIS project is required.
/// </summary>
[Collection("d083-live")]
public sealed class D084B1LiveHostTests : IAsyncLifetime
{
    private const string Host = "127.0.0.1";
    private const int Port = 6520;
    private const string RunDir = @"D:\ArcGIS-Pro-MCP 2.0\.runtime\evolution\v5-f\run-20260928-d084";

    private readonly List<string> _raw = new();
    private HttpClient? _client;
    private McpServer? _server;

    public async Task InitializeAsync()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new PingTool());
        registry.Register(new ListJobsTool());
        registry.Register(new CancelJobTool());
        registry.Register(new ValidatePlanTool());
        registry.Register(new DescribeToolCatalogTool());

        var settings = new MCPSettings();
        var router = new MCPToolRouter(registry, new FakeArcGISHost(), settings, NullLogger.Instance);
        var transport = new HttpMcpTransport(Host, Port, "/mcp", NullLogger.Instance);
        _server = new McpServer(transport, registry, router, settings, NullLogger.Instance);
        await _server.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri($"http://{Host}:{Port}") };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_server is not null)
        {
            await _server.StopAsync();
        }

        Directory.CreateDirectory(RunDir);
        await File.AppendAllLinesAsync(
            Path.Combine(RunDir, "d084-b1-live-raw.jsonl"),
            _raw,
            new UTF8Encoding(false));
    }

    [Fact]
    public async Task B1Tools_RunThroughLoopbackMcpHost()
    {
        using (var initialized = await PostAsync("initialize", """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}"""))
        {
            Assert.Equal("2.0", initialized.RootElement.GetProperty("jsonrpc").GetString());
            Assert.True(initialized.RootElement.GetProperty("result").TryGetProperty("serverInfo", out _));
        }

        using (var listing = await PostAsync("tools/list", """{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""))
        {
            var tools = listing.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
            var expected = new[] { "list_jobs", "cancel_job", "validate_plan", "describe_tool_catalog" };
            foreach (var name in expected)
            {
                Assert.Single(tools.Where(t => t.GetProperty("name").GetString() == name));
            }
            _raw.Add($"tools/list -> count={tools.Length}; D084={string.Join(',', expected)}");
        }

        using (var jobs = await CallAsync(3, "list_jobs", """{"maxItems50":10,"responseFormat":"summary"}"""))
        {
            Assert.False(IsError(jobs));
            using var data = Data(jobs);
            Assert.True(data.RootElement.GetProperty("jobs").ValueKind == JsonValueKind.Array);
            _raw.Add("tools/call list_jobs -> " + jobs.RootElement.GetProperty("result").GetRawText());
        }

        using (var cancel = await CallAsync(4, "cancel_job", """{"jobId":"d084-live-missing-job","waitMs":0}"""))
        {
            Assert.True(IsError(cancel));
            Assert.Contains("NOT_FOUND", Text(cancel), StringComparison.OrdinalIgnoreCase);
            _raw.Add("tools/call cancel_job unknown-id -> " + cancel.RootElement.GetProperty("result").GetRawText());
        }

        using (var validation = await CallAsync(5, "validate_plan", """{"plan":{"steps":[{"tool":"ping","args":{}}]},"strict":true}"""))
        {
            Assert.False(IsError(validation));
            using var data = Data(validation);
            Assert.True(data.RootElement.GetProperty("valid").GetBoolean());
            Assert.False(data.RootElement.GetProperty("sideEffects").GetBoolean());
            _raw.Add("tools/call validate_plan -> " + data.RootElement.GetRawText());
        }

        using (var catalog = await CallAsync(6, "describe_tool_catalog", """{"query":"list_jobs","includeSchema":false,"maxItems50":10}"""))
        {
            Assert.False(IsError(catalog));
            using var data = Data(catalog);
            Assert.Equal(1, data.RootElement.GetProperty("returnedCount").GetInt32());
            Assert.Equal(5, data.RootElement.GetProperty("registryCount").GetInt32());
            _raw.Add("tools/call describe_tool_catalog -> " + data.RootElement.GetRawText());
        }
    }

    private async Task<JsonDocument> CallAsync(int id, string name, string arguments)
        => await PostAsync("tools/call " + name,
            $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"tools/call\",\"params\":{{\"name\":{JsonSerializer.Serialize(name)},\"arguments\":{arguments}}}}}");

    private async Task<JsonDocument> PostAsync(string label, string request)
    {
        using var content = new StringContent(request, Encoding.UTF8, "application/json");
        using var response = await _client!.PostAsync("/mcp", content);
        var body = await response.Content.ReadAsStringAsync();
        _raw.Add($"{(int)response.StatusCode} {label} request={request} response={body}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(body);
    }

    private static bool IsError(JsonDocument document)
        => document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean();

    private static string Text(JsonDocument document)
        => document.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;

    private static JsonDocument Data(JsonDocument document)
        => JsonDocument.Parse(Text(document));
}
