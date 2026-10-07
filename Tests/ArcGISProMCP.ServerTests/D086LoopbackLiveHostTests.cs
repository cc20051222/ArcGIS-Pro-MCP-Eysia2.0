using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.Server;
using ArcGISProMCP.Server.Transport;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.ServerTests;

/// <summary>
/// D-086 D-drive loopback LIVE: six registered tools cross a real local HTTP/MCP host.
/// The ArcGIS host is a fixture; this does not claim a real ArcGIS Pro runtime.
/// </summary>
[Collection("d083-live")]
public sealed class D086LoopbackLiveHostTests : IAsyncLifetime
{
    private const string Host = "127.0.0.1";
    private const int Port = 6520;
    private static readonly string RunRoot = ResolveRunRoot();

    private readonly string _caseRoot = Path.Combine(
        RunRoot,
        "case-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _raw = new();
    private HttpClient? _client;
    private McpServer? _server;
    private string _bundlePath = string.Empty;
    private string _revisionPath = string.Empty;
    private string _packagePath = string.Empty;

    private static string ResolveRunRoot()
    {
        var configured = Environment.GetEnvironmentVariable("ARCGIS_PRO_MCP_LIVE_TEST_ROOT");
        return string.IsNullOrWhiteSpace(configured)
            ? @"D:\ArcGIS-Pro-MCP 2.0\.runtime\evolution\v5-f\run-20260929-d086\live-host"
            : Path.GetFullPath(configured);
    }

    public async Task InitializeAsync()
    {
        Assert.Equal(@"D:\", Path.GetPathRoot(_caseRoot));
        Directory.CreateDirectory(_caseRoot);
        _bundlePath = Path.Combine(_caseRoot, "design-bundle");
        _revisionPath = Path.Combine(_caseRoot, "design-bundle-revision-1");
        _packagePath = Path.Combine(_caseRoot, "delivery-package");

        var layout = new NotImplementedService
        {
            GetLayoutInfoHook = name => OperationResult<LayoutDetailInfo>.Ok(new LayoutDetailInfo
            {
                Name = name,
                PageWidth = 8.5,
                PageHeight = 11,
                PageUnits = "Inches",
                ElementCount = 1,
            }),
            ListLayoutElementsHook = _ => OperationResult<IReadOnlyList<LayoutElementInfo>>.Ok(new[]
            {
                new LayoutElementInfo
                {
                    Name = "MapFrame",
                    ElementType = "MapFrame",
                    IsVisible = true,
                    X = 0.5,
                    Y = 0.5,
                },
            }),
        };

        OperationResult<LayoutExportInfo> WriteExport(
            string name, string output, string format, double? resolution, bool overwrite, bool? transparent)
        {
            Assert.Equal(@"D:\", Path.GetPathRoot(Path.GetFullPath(output)));
            byte[] bytes = format.ToUpperInvariant() switch
            {
                "PNG" => PngHeader(2550, 3300),
                "PDF" => Encoding.ASCII.GetBytes("%PDF-1.7"),
                "SVG" => Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
                _ => throw new InvalidOperationException("Unexpected D-086 export format: " + format),
            };
            File.WriteAllBytes(output, bytes);
            return OperationResult<LayoutExportInfo>.Ok(new LayoutExportInfo
            {
                LayoutName = name,
                OutputPath = output,
                Format = format,
                Resolution = resolution,
                FileSizeBytes = bytes.LongLength,
                Overwritten = overwrite,
                MagicBytesHex = Convert.ToHexString(bytes.Take(8).ToArray()),
            });
        }

        layout.ExportLayoutHook = (name, output, format, resolution, overwrite)
            => WriteExport(name, output, format, resolution, overwrite, null);
        layout.ExportLayoutWithOptionsHook = WriteExport;

        var registry = new MCPToolRegistry();
        registry.Register(new ExportDesignBundleTool());
        registry.Register(new ValidateDesignBundleTool());
        registry.Register(new RefreshDesignBundleTool());
        registry.Register(new ValidateDeliveryPackageTool());
        registry.Register(new SuggestWorkflowTool());
        registry.Register(new GetPerformanceStatsTool());

        var settings = new MCPSettings();
        // Match the production Compatibility composition: every HTTP tool call uses the shared
        // invocation pipeline and the centralized protected-output path validator.
        var invoker = new ToolInvoker(new ToolValidatorPipeline(new ProtectedOutputPathArgumentValidator()));
        var router = new MCPToolRouter(
            registry,
            new FakeArcGISHost(layout: layout),
            settings,
            NullLogger.Instance,
            invoker: invoker);
        var transport = new HttpMcpTransport(Host, Port, "/mcp", NullLogger.Instance);
        _server = new McpServer(transport, registry, router, settings, NullLogger.Instance);
        await _server.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri($"http://{Host}:{Port}") };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_server is not null)
            await _server.StopAsync();

        Directory.CreateDirectory(_caseRoot);
        await File.WriteAllLinesAsync(
            Path.Combine(_caseRoot, "d086-loopback-live-raw.jsonl"),
            _raw,
            new UTF8Encoding(false));
    }

    [Fact]
    public async Task AllSixD086Tools_RunThroughLoopbackMcpHost()
    {
        using (var initialized = await PostAsync("initialize", new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "initialize",
            ["params"] = new Dictionary<string, object?>(),
        }))
        {
            Assert.Equal("2.0", initialized.RootElement.GetProperty("jsonrpc").GetString());
            Assert.True(initialized.RootElement.GetProperty("result").TryGetProperty("serverInfo", out _));
        }

        var expected = new[]
        {
            "export_design_bundle",
            "validate_design_bundle",
            "refresh_design_bundle",
            "validate_delivery_package",
            "suggest_workflow",
            "get_performance_stats",
        };
        using (var listing = await PostAsync("tools/list", new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 2,
            ["method"] = "tools/list",
        }))
        {
            var tools = listing.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
            var names = tools.Select(tool => tool.GetProperty("name").GetString()).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(6, tools.Length);
            Assert.True(expected.All(names.Contains));
        }

        using (var exported = await CallAsync(3, "export_design_bundle", new Dictionary<string, object?>
        {
            ["layout"] = "D086LiveLayout",
            ["outputPath"] = _bundlePath,
            ["dpi"] = 300,
            ["transparentBackground"] = true,
            ["includeVectorFiles"] = true,
        }))
        {
            Assert.False(IsError(exported), Text(exported));
            using var data = Data(exported);
            Assert.Equal(3, data.RootElement.GetProperty("artifactCount").GetInt32());
            Assert.True(data.RootElement.GetProperty("sourceBundleUntouched").GetBoolean());
            Assert.True(File.Exists(Path.Combine(_bundlePath, "ArtifactManifest.json")));
        }

        using (var validated = await CallAsync(4, "validate_design_bundle", new Dictionary<string, object?>
        {
            ["bundlePath"] = _bundlePath,
            ["strict"] = true,
        }))
        {
            Assert.False(IsError(validated), Text(validated));
            using var data = Data(validated);
            Assert.True(data.RootElement.GetProperty("valid").GetBoolean());
            Assert.False(data.RootElement.GetProperty("sideEffects").GetBoolean());
        }

        var sourceBefore = BundleFingerprint(_bundlePath);
        using (var refreshed = await CallAsync(5, "refresh_design_bundle", new Dictionary<string, object?>
        {
            ["bundlePath"] = _bundlePath,
            ["outputPath"] = _revisionPath,
            ["dataUpdates"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["sourcePath"] = "source.gdb",
                    ["newPath"] = "source-v2.gdb",
                },
            },
            ["rerenderAll"] = true,
            ["overwrite"] = false,
        }))
        {
            Assert.False(IsError(refreshed), Text(refreshed));
            using var data = Data(refreshed);
            Assert.True(data.RootElement.GetProperty("sourceBundleUntouched").GetBoolean());
            Assert.Equal(_revisionPath, data.RootElement.GetProperty("outputPath").GetString());
        }
        Assert.True(Directory.Exists(_revisionPath));
        Assert.Equal(sourceBefore, BundleFingerprint(_bundlePath));

        var packageBytes = Encoding.UTF8.GetBytes("D086 loopback delivery fixture");
        Directory.CreateDirectory(_packagePath);
        File.WriteAllBytes(Path.Combine(_packagePath, "delivery.bin"), packageBytes);
        var deliveryManifest = new
        {
            artifacts = new[]
            {
                new
                {
                    path = "delivery.bin",
                    required = true,
                    bytes = packageBytes.LongLength,
                    sha256 = Convert.ToHexString(SHA256.HashData(packageBytes)),
                },
            },
        };
        File.WriteAllText(
            Path.Combine(_packagePath, "DeliveryManifest.json"),
            JsonSerializer.Serialize(deliveryManifest),
            new UTF8Encoding(false));

        using (var delivery = await CallAsync(6, "validate_delivery_package", new Dictionary<string, object?>
        {
            ["packagePath"] = _packagePath,
            ["requireAll"] = true,
        }))
        {
            Assert.False(IsError(delivery), Text(delivery));
            using var data = Data(delivery);
            Assert.True(data.RootElement.GetProperty("valid").GetBoolean());
            Assert.True(data.RootElement.GetProperty("complete").GetBoolean());
        }

        using (var suggestion = await CallAsync(7, "suggest_workflow", new Dictionary<string, object?>
        {
            ["goal"] = "planning location study area map",
            ["inputs"] = new Dictionary<string, object?>
            {
                ["studyAreaGeometry"] = "D086 live fixture",
                ["CRS"] = "EPSG:4326",
            },
            ["maxSuggestions"] = 3,
        }))
        {
            Assert.False(IsError(suggestion), Text(suggestion));
            using var data = Data(suggestion);
            Assert.Equal("S19", data.RootElement.GetProperty("suggestions")[0].GetProperty("scenarioId").GetString());
            Assert.False(data.RootElement.GetProperty("sideEffects").GetBoolean());
        }

        using (var stats = await CallAsync(8, "get_performance_stats", new Dictionary<string, object?>
        {
            ["scope"] = "host",
            ["sinceHours"] = 24,
            ["maxItems100"] = 10,
        }))
        {
            Assert.False(IsError(stats), Text(stats));
            using var data = Data(stats);
            Assert.Equal("host", data.RootElement.GetProperty("scope").GetString());
            Assert.False(data.RootElement.GetProperty("sensitiveArgumentsIncluded").GetBoolean());
            Assert.False(data.RootElement.GetProperty("newPersistenceCreated").GetBoolean());
        }

        _raw.Add(JsonSerializer.Serialize(new
        {
            eventName = "D086_LOOPBACK_ASSERTIONS",
            status = "completed",
            toolCount = expected.Length,
            runRoot = _caseRoot,
            sourceBundleUntouched = sourceBefore == BundleFingerprint(_bundlePath),
        }));
    }

    private async Task<JsonDocument> CallAsync(int id, string name, Dictionary<string, object?> arguments)
        => await PostAsync("tools/call " + name, new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = "tools/call",
            ["params"] = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["arguments"] = arguments,
            },
        });

    private async Task<JsonDocument> PostAsync(string label, Dictionary<string, object?> request)
    {
        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client!.PostAsync("/mcp", content);
        var body = await response.Content.ReadAsStringAsync();
        _raw.Add(JsonSerializer.Serialize(new
        {
            label,
            statusCode = (int)response.StatusCode,
            request = json,
            response = body,
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(body);
    }

    private static bool IsError(JsonDocument document)
        => document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean();

    private static string Text(JsonDocument document)
        => document.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;

    private static JsonDocument Data(JsonDocument document)
        => JsonDocument.Parse(Text(document));

    private static string BundleFingerprint(string path)
        => string.Join(
            "|",
            Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .OrderBy(file => Path.GetRelativePath(path, file), StringComparer.Ordinal)
                .Select(file => Path.GetRelativePath(path, file) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))));

    private static byte[] PngHeader(int width, int height)
    {
        var bytes = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), 13);
        Encoding.ASCII.GetBytes("IHDR").CopyTo(bytes, 12);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }
}
