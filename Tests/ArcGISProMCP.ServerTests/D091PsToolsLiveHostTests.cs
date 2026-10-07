using System.Net;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.Server;
using ArcGISProMCP.Server.Internal;
using ArcGISProMCP.Server.Transport;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.ServerTests;

/// <summary>D-091 D-drive loopback LIVE: registered PS tools cross the existing MCP listener; no Adobe client is started.</summary>
[Collection("d083-live")]
public sealed class D091PsToolsLiveHostTests : IAsyncLifetime
{
    private const string Host = "127.0.0.1";
    private const int Port = 6520;
    private readonly List<string> _raw = new();
    private readonly string _runRoot = ResolveRunRoot();
    private HttpClient? _client;
    private McpServer? _server;

    private static string ResolveRunRoot()
    {
        var configured = Environment.GetEnvironmentVariable("ARCGIS_PRO_MCP_LIVE_TEST_ROOT");
        return string.IsNullOrWhiteSpace(configured)
            ? @"D:\ArcGIS-Pro-MCP 2.0\.runtime\evolution\v5-f\run-20260929-d091\phase-b\live-host"
            : Path.GetFullPath(configured);
    }

    public async Task InitializeAsync()
    {
        Assert.Equal(@"D:\", Path.GetPathRoot(_runRoot));
        Directory.CreateDirectory(_runRoot);
        var registry = new MCPToolRegistry();
        registry.Register(new PsGetCapabilitiesTool());
        registry.Register(new PsApplyDesignRecipeTool());
        registry.Register(new PsImportDesignBundleTool());
        registry.Register(new PsRefreshDesignBundleTool());
        registry.Register(new PsExportDeliverablesTool());

        var settings = new MCPSettings { Host = Host, Port = Port };
        var router = new MCPToolRouter(
            registry,
            new FakeArcGISHost(),
            settings,
            NullLogger.Instance,
            invoker: new ToolInvoker(new ToolValidatorPipeline()));
        var psRoute = new PsChannelHandler(new PsChannelPolicy(), new PsSessionStore(), "1.0.2");
        var transport = new HttpMcpTransport(Host, Port, "/mcp", NullLogger.Instance, psRoute);
        _server = new McpServer(transport, registry, router, settings, NullLogger.Instance);
        await _server.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri($"http://{Host}:{Port}") };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_server is not null)
            await _server.StopAsync();
        Directory.CreateDirectory(_runRoot);
        await File.WriteAllLinesAsync(
            Path.Combine(_runRoot, "d091-loopback-live-raw.jsonl"),
            _raw,
            new UTF8Encoding(false));
    }

    [Fact]
    public async Task D091Tools_AreListedAndFailClosedThroughExistingLoopbackMcpHost()
    {
        using (var listing = await PostAsync(1, "tools/list", new Dictionary<string, object?>()))
        {
            var tools = listing.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
            var names = tools.Select(tool => tool.GetProperty("name").GetString()).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(5, tools.Length);
            Assert.True(new[]
            {
                "ps_get_capabilities", "ps_apply_design_recipe", "ps_import_design_bundle",
                "ps_refresh_design_bundle", "ps_export_deliverables",
            }.All(names.Contains));
        }

        using (var capabilities = await CallAsync(2, "ps_get_capabilities", new Dictionary<string, object?>
        {
            ["probeDeep"] = true,
        }))
        {
            Assert.False(IsError(capabilities), Text(capabilities));
            using var data = JsonDocument.Parse(Text(capabilities));
            Assert.Equal("NOT_VERIFIED", data.RootElement.GetProperty("status").GetString());
            Assert.True(data.RootElement.GetProperty("probeDeepRequested").GetBoolean());
            Assert.False(data.RootElement.GetProperty("deepProbePerformed").GetBoolean());
            Assert.False(data.RootElement.GetProperty("psRequestSent").GetBoolean());
            Assert.Equal("HEALTHY", data.RootElement.GetProperty("routeHealth").GetProperty("status").GetString());
            Assert.True(data.RootElement.GetProperty("routeHealth").GetProperty("serviceIdentityMatched").GetBoolean());
        }

        var actions = new (string Name, Dictionary<string, object?> Args)[]
        {
            ("ps_apply_design_recipe", new Dictionary<string, object?>
            {
                ["documentId"] = "doc-live",
                ["apsReference"] = new Dictionary<string, object?>(),
                ["recipeId"] = "recipe-live",
            }),
            ("ps_import_design_bundle", new Dictionary<string, object?>
            {
                ["bundlePath"] = "D:/owned/live-bundle",
                ["documentId"] = "doc-live",
                ["apsReference"] = new Dictionary<string, object?>(),
            }),
            ("ps_refresh_design_bundle", new Dictionary<string, object?>
            {
                ["documentId"] = "doc-live",
                ["bundlePath"] = "D:/owned/live-bundle",
                ["apsReference"] = new Dictionary<string, object?>(),
            }),
            ("ps_export_deliverables", new Dictionary<string, object?>
            {
                ["documentId"] = "doc-live",
                ["outputDir"] = "D:/owned/live-output",
                ["formats"] = new[] { "pdf" },
            }),
        };

        var requestId = 3;
        foreach (var action in actions)
        {
            using var response = await CallAsync(requestId++, action.Name, action.Args);
            Assert.True(IsError(response), Text(response));
            Assert.Contains(ErrorCodes.NotImplemented, Text(response), StringComparison.Ordinal);
            Assert.Contains("NOT_VERIFIED", Text(response), StringComparison.Ordinal);
            Assert.Contains("psRequestSent", Text(response), StringComparison.Ordinal);
            Assert.Contains("false", Text(response), StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<JsonDocument> CallAsync(int id, string name, Dictionary<string, object?> arguments)
        => await PostAsync(id, "tools/call " + name, new Dictionary<string, object?>
        {
            ["name"] = name,
            ["arguments"] = arguments,
        }, "tools/call");

    private async Task<JsonDocument> PostAsync(
        int id,
        string label,
        Dictionary<string, object?> parameters,
        string method = "tools/list")
    {
        var request = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };
        if (method == "tools/call")
            request["params"] = parameters;
        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client!.PostAsync("/mcp", content);
        var body = await response.Content.ReadAsStringAsync();
        _raw.Add(JsonSerializer.Serialize(new { label, statusCode = (int)response.StatusCode, request = json, response = body }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(body);
    }

    private static bool IsError(JsonDocument document)
        => document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean();

    private static string Text(JsonDocument document)
        => document.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
}
