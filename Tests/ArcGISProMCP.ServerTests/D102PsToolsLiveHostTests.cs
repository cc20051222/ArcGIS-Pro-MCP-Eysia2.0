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

/// <summary>D-102 loopback LIVE through the existing MCP listener; no ArcGIS Pro or Adobe client is started.</summary>
[Collection("d083-live")]
public sealed class D102PsToolsLiveHostTests : IAsyncLifetime
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
            ? @"D:\ArcGIS-Pro-MCP 2.0\.runtime\evolution\v5-f\run-20260930-d102\phase-b\live-host"
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
        registry.Register(new PsCompareDocumentVersionsTool());
        registry.Register(new PsGetDocumentInfoTool());
        registry.Register(new PsGetLayerInfoTool());
        registry.Register(new PsListDocumentsTool());
        registry.Register(new PsListLayersTool());
        registry.Register(new PsValidateDocumentTool());
        registry.Register(new PsCreateAdjustmentLayerTool());
        registry.Register(new PsManageArtboardsTool());
        registry.Register(new PsPlaceDesignAssetTool());
        registry.Register(new PsRestoreDocumentSnapshotTool());
        registry.Register(new PsSetLayerMaskTool());
        registry.Register(new PsSetLayerPropertiesTool());
        registry.Register(new PsSetTextPropertiesTool());
        registry.Register(new PsCreateDocumentSnapshotTool());
        registry.Register(new PsPreviewDocumentTool());

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
            Path.Combine(_runRoot, "d102-loopback-live-raw.jsonl"),
            _raw,
            new UTF8Encoding(false));
    }

    [Fact]
    public async Task D102Tools_AreListedAndAllFailClosedThroughTheExistingLoopbackMcpHost()
    {
        using (var listing = await PostAsync(1, "tools/list", new Dictionary<string, object?>()))
        {
            var tools = listing.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
            var names = tools.Select(tool => tool.GetProperty("name").GetString()).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(20, tools.Length);
            Assert.True(ExpectedD102Names.All(names.Contains));
        }

        var requestId = 2;
        foreach (var (name, arguments) in ValidActions())
        {
            using var response = await CallAsync(requestId++, name, arguments);
            Assert.True(IsError(response), Text(response));
            Assert.Contains(ErrorCodes.NotImplemented, Text(response), StringComparison.Ordinal);
            Assert.Contains("NOT_VERIFIED", Text(response), StringComparison.Ordinal);
            Assert.Contains("psRequestSent", Text(response), StringComparison.Ordinal);
            Assert.Contains("sideEffects", Text(response), StringComparison.Ordinal);
            Assert.Contains("false", Text(response), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static readonly string[] ExpectedD102Names =
    [
        "ps_compare_document_versions", "ps_get_document_info", "ps_get_layer_info",
        "ps_list_documents", "ps_list_layers", "ps_validate_document",
        "ps_create_adjustment_layer", "ps_manage_artboards", "ps_place_design_asset",
        "ps_restore_document_snapshot", "ps_set_layer_mask", "ps_set_layer_properties",
        "ps_set_text_properties", "ps_create_document_snapshot", "ps_preview_document",
    ];

    private static IEnumerable<(string Name, Dictionary<string, object?> Arguments)> ValidActions()
    {
        yield return ("ps_compare_document_versions", Args(("leftRef", "snapshot-left"), ("rightRef", "snapshot-right")));
        yield return ("ps_get_document_info", Args(("documentId", "doc-live")));
        yield return ("ps_get_layer_info", Args(("documentId", "doc-live"), ("layerId", "layer-live")));
        yield return ("ps_list_documents", Args());
        yield return ("ps_list_layers", Args(("documentId", "doc-live")));
        yield return ("ps_validate_document", Args(("documentId", "doc-live")));
        yield return ("ps_create_adjustment_layer", Args(("documentId", "doc-live"), ("kind", "levels"), ("apsReference", new Dictionary<string, object?>())));
        yield return ("ps_manage_artboards", Args(("documentId", "doc-live"), ("action", "list"), ("apsReference", new Dictionary<string, object?>())));
        yield return ("ps_place_design_asset", Args(("documentId", "doc-live"), ("assetPath", "D:/owned/asset.png"), ("apsReference", new Dictionary<string, object?>())));
        yield return ("ps_restore_document_snapshot", Args(("documentId", "doc-live"), ("snapshotId", "snapshot-live"), ("apsReference", new Dictionary<string, object?>())));
        yield return ("ps_set_layer_mask", Args(("documentId", "doc-live"), ("layerId", "layer-live"), ("maskSource", new Dictionary<string, object?>()), ("apsReference", new Dictionary<string, object?>())));
        yield return ("ps_set_layer_properties", Args(("documentId", "doc-live"), ("layerId", "layer-live"), ("properties", new Dictionary<string, object?>()), ("apsReference", new Dictionary<string, object?>())));
        yield return ("ps_set_text_properties", Args(("documentId", "doc-live"), ("layerId", "text-live"), ("properties", new Dictionary<string, object?>()), ("apsReference", new Dictionary<string, object?>())));
        yield return ("ps_create_document_snapshot", Args(("documentId", "doc-live"), ("outputPath", "D:/owned/snapshot.psd")));
        yield return ("ps_preview_document", Args(("documentId", "doc-live"), ("outputPath", "D:/owned/preview.png")));
    }

    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] pairs)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in pairs)
            arguments.Add(pair.Key, pair.Value);
        return arguments;
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
