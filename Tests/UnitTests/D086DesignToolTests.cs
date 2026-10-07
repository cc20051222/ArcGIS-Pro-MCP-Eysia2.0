using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>D-086 target direct tests: frozen schemas, D-drive write gates, manifests and read-only tools.</summary>
public sealed class D086DesignToolTests
{
    private static ToolExecutionContext Context(
        Dictionary<string, object?>? args = null,
        FakeArcGISHost? host = null,
        MCPToolRegistry? registry = null,
        bool readOnly = false)
    {
        var mode = new ReadOnlyModeService();
        if (readOnly) mode.Set(true);
        return new ToolExecutionContext
        {
            Host = host ?? new FakeArcGISHost(),
            Arguments = args ?? new Dictionary<string, object?>(),
            Registry = registry,
            ReadOnly = mode,
        };
    }

    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] pairs)
        => pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static Dictionary<string, object?> Data(OperationResult<object?> result)
        => Assert.IsType<Dictionary<string, object?>>(result.Data);

    private static string? Code(OperationResult<object?> result)
        => result.Success ? null : result.Errors.FirstOrDefault()?.Code;

    private static string NewRunRoot()
    {
        var configured = Environment.GetEnvironmentVariable("ARCGIS_PRO_MCP_TEST_TEMP_ROOT");
        var basePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "d086-test-runs")
            : configured!;
        var root = Path.GetFullPath(Path.Combine(basePath, "d086-" + Guid.NewGuid().ToString("N")));
        Assert.Equal(@"D:\", Path.GetPathRoot(root));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void RegistryAndSchemasExposeExactlySixFrozenTools()
    {
        var registry = ProductionCompositionAccess.BuildRegistry();
        Assert.Equal(239, registry.List().Count);
        var expected = new[]
        {
            "export_design_bundle", "validate_design_bundle", "refresh_design_bundle",
            "validate_delivery_package", "suggest_workflow", "get_performance_stats",
        };
        foreach (var name in expected)
            Assert.NotNull(registry.Get(name));

        AssertSchema(new ExportDesignBundleTool(), new[] { "layout", "outputPath", "dpi", "transparentBackground", "includeVectorFiles", "designSpecRevision", "overwrite" }, "layout", "outputPath");
        AssertSchema(new ValidateDesignBundleTool(), new[] { "bundlePath", "manifestPath", "strict" }, "bundlePath");
        AssertSchema(new RefreshDesignBundleTool(), new[] { "bundlePath", "dataUpdates", "outputPath", "rerenderAll", "overwrite" }, "bundlePath");
        AssertSchema(new ValidateDeliveryPackageTool(), new[] { "packagePath", "manifestPath", "requireAll" }, "packagePath");
        AssertSchema(new SuggestWorkflowTool(), new[] { "goal", "inputs", "maxSuggestions" }, "goal");
        AssertSchema(new GetPerformanceStatsTool(), new[] { "scope", "toolName", "sinceHours", "maxItems100" });
        Assert.Equal(33, typeof(ErrorCodes).GetFields().Count(field => field.IsLiteral && field.IsPublic));
    }

    [Fact]
    public async Task ExportUsesOwnedDStageManifestAndDefaultNoOverwrite()
    {
        var root = NewRunRoot();
        var parent = Path.Combine(root, "job-output");
        Directory.CreateDirectory(parent);
        var bundle = Path.Combine(parent, "bundle");
        var host = LayoutHost(out var recorder);
        var args = Args(("layout", "TestLayout"), ("outputPath", bundle));
        var exported = await new ExportDesignBundleTool().ExecuteAsync(Context(args, host));
        Assert.True(exported.Success, string.Join("; ", exported.Errors.Select(error => error.ToString())));
        var data = Data(exported);
        Assert.Equal(3, data["artifactCount"]);
        Assert.True(Directory.Exists(bundle));
        Assert.True(File.Exists(Path.Combine(bundle, "ArtifactManifest.json")));
        Assert.Equal(3, recorder.Calls);
        Assert.Equal(true, recorder.Transparency);

        var validated = await new ValidateDesignBundleTool().ExecuteAsync(Context(Args(("bundlePath", bundle))));
        Assert.True(validated.Success);
        var report = Data(validated);
        Assert.Equal(true, report["valid"]);
        Assert.Equal("not_verified", ((Dictionary<string, object?>)report["checks"]!)["fonts"] is Dictionary<string, object?> fontCheck
            ? fontCheck["status"] : null);

        var before = File.ReadAllBytes(Path.Combine(bundle, "ArtifactManifest.json"));
        var refused = await new ExportDesignBundleTool().ExecuteAsync(Context(args, host));
        Assert.Equal(ErrorCodes.OutputExists, Code(refused));
        Assert.Equal(3, recorder.Calls);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(bundle, "ArtifactManifest.json")));

        var overwrite = await new ExportDesignBundleTool().ExecuteAsync(Context(Args(("layout", "TestLayout"), ("outputPath", bundle), ("overwrite", true)), host));
        Assert.True(overwrite.Success);
        Assert.Equal(6, recorder.Calls);
        Assert.True(File.Exists(Path.Combine(bundle, "preview.png")));
        Assert.DoesNotContain(Directory.EnumerateDirectories(parent), path => Path.GetFileName(path).Contains("previous", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshWritesSiblingRevisionAndLeavesSourceBytesUntouched()
    {
        var root = NewRunRoot();
        var parent = Path.Combine(root, "refresh");
        Directory.CreateDirectory(parent);
        var source = Path.Combine(parent, "source-bundle");
        var host = LayoutHost(out _);
        var exported = await new ExportDesignBundleTool().ExecuteAsync(Context(
            Args(("layout", "TestLayout"), ("outputPath", source)), host));
        Assert.True(exported.Success);
        var manifestBefore = File.ReadAllBytes(Path.Combine(source, "ArtifactManifest.json"));
        var pngBefore = File.ReadAllBytes(Path.Combine(source, "preview.png"));

        var updates = new object?[]
        {
            new Dictionary<string, object?> { ["sourcePath"] = "D:\\inputs\\old.gdb", ["newPath"] = "D:\\inputs\\new.gdb" },
        };
        var refreshed = await new RefreshDesignBundleTool().ExecuteAsync(Context(
            Args(("bundlePath", source), ("dataUpdates", updates)), host));
        Assert.True(refreshed.Success, string.Join("; ", refreshed.Errors.Select(error => error.ToString())));
        var result = Data(refreshed);
        var revisionPath = Assert.IsType<string>(result["outputPath"]);
        Assert.NotEqual(source, revisionPath);
        Assert.True(Directory.Exists(revisionPath));
        Assert.True(Assert.IsType<bool>(result["sourceBundleUntouched"]));
        Assert.Equal(manifestBefore, File.ReadAllBytes(Path.Combine(source, "ArtifactManifest.json")));
        Assert.Equal(pngBefore, File.ReadAllBytes(Path.Combine(source, "preview.png")));
        Assert.Equal(true, Data(await new ValidateDesignBundleTool().ExecuteAsync(Context(Args(("bundlePath", revisionPath)))))["valid"]);

        var noPartialRefresh = await new RefreshDesignBundleTool().ExecuteAsync(Context(
            Args(("bundlePath", source), ("rerenderAll", false)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(noPartialRefresh));
    }

    [Fact]
    public async Task WriteToolsRejectNonDAndOverlappingSourceOutputBeforeWriting()
    {
        var notD = await new ExportDesignBundleTool().ExecuteAsync(Context(
            Args(("layout", "TestLayout"), ("outputPath", @"C:\d086-forbidden-output"))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(notD));

        var root = NewRunRoot();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "ArtifactManifest.json"),
            "{\"schemaVersion\":\"artifact-manifest-v1\",\"layout\":\"TestLayout\",\"artifacts\":[]}");
        var overlap = await new RefreshDesignBundleTool().ExecuteAsync(Context(
            Args(("bundlePath", source), ("outputPath", source))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(overlap));
        Assert.True(File.Exists(Path.Combine(source, "ArtifactManifest.json")));
    }

    [Fact]
    public async Task DeliveryValidatorReadsZipWithoutExtractionAndRejectsTraversal()
    {
        var root = NewRunRoot();
        var package = Path.Combine(root, "delivery.zip");
        var bytes = Encoding.UTF8.GetBytes("verified delivery file");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            using (var stream = archive.CreateEntry("files/map.txt").Open())
                stream.Write(bytes);
            var manifest = JsonSerializer.Serialize(new
            {
                artifacts = new[] { new { path = "files/map.txt", required = true, bytes = bytes.Length, sha256 = hash } },
            });
            using var writer = new StreamWriter(archive.CreateEntry("DeliveryManifest.json").Open(), Encoding.UTF8);
            writer.Write(manifest);
        }

        var checkedPackage = await new ValidateDeliveryPackageTool().ExecuteAsync(Context(Args(("packagePath", package))));
        Assert.True(checkedPackage.Success);
        Assert.Equal(true, Data(checkedPackage)["valid"]);
        Assert.False(Directory.Exists(Path.Combine(root, "files")));

        using (var archive = ZipFile.Open(package, ZipArchiveMode.Update))
        {
            using var writer = new StreamWriter(archive.CreateEntry("../outside.txt").Open(), Encoding.UTF8);
            writer.Write("no extraction");
        }
        var traversal = await new ValidateDeliveryPackageTool().ExecuteAsync(Context(Args(("packagePath", package))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(traversal));
    }

    [Fact]
    public async Task WorkflowSuggestionsAndPerformanceStatsStayDeterministicAndReadOnly()
    {
        var suggested = await new SuggestWorkflowTool().ExecuteAsync(Context(Args(
            ("goal", "规划区位现状图"), ("inputs", new Dictionary<string, object?> { ["studyAreaGeometry"] = "declared" }))));
        Assert.True(suggested.Success);
        var suggestion = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(Data(suggested)["suggestions"]).ToList();
        Assert.Equal("S19", suggestion[0]["scenarioId"]);
        var missing = Assert.IsType<string[]>(suggestion[0]["missingPrerequisites"]);
        Assert.DoesNotContain("studyAreaGeometry", missing);
        Assert.Equal(false, Data(suggested)["sideEffects"]);

        var registry = new MCPToolRegistry();
        registry.Register(new SuggestWorkflowTool());
        var missingTool = await new GetPerformanceStatsTool().ExecuteAsync(Context(
            Args(("scope", "tool"), ("toolName", "not_registered")), registry: registry));
        Assert.Equal(ErrorCodes.NotFound, Code(missingTool));
        var invalidScope = await new GetPerformanceStatsTool().ExecuteAsync(Context(Args(("scope", "filesystem"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(invalidScope));
        var stats = await new GetPerformanceStatsTool().ExecuteAsync(Context(Args(("scope", "all"), ("maxItems100", 2))));
        Assert.True(stats.Success);
        Assert.Equal("D-073 in-memory/job ledger item records only", Data(stats)["source"]);
        Assert.Equal(false, Data(stats)["newPersistenceCreated"]);
        Assert.True(Assert.IsType<int>(Data(stats)["returnedCount"]) <= 2);
    }

    private sealed class ExportRecorder
    {
        public int Calls { get; set; }
        public bool? Transparency { get; set; }
    }

    private static FakeArcGISHost LayoutHost(out ExportRecorder recorder)
    {
        var captured = new ExportRecorder();
        recorder = captured;
        var host = new FakeArcGISHost();
        var layout = Assert.IsType<NotImplementedService>(host.Layout);
        layout.GetLayoutInfoHook = name => OperationResult<LayoutDetailInfo>.Ok(new LayoutDetailInfo
        {
            Name = name,
            PageWidth = 8.5,
            PageHeight = 11,
            PageUnits = "Inches",
            ElementCount = 1,
        });
        layout.ListLayoutElementsHook = _ => OperationResult<IReadOnlyList<LayoutElementInfo>>.Ok(new[]
        {
            new LayoutElementInfo { Name = "MapFrame", ElementType = "MapFrame", IsVisible = true, X = 0.5, Y = 0.5 },
        });
        OperationResult<LayoutExportInfo> WriteExport(string name, string path, string format, double? dpi, bool overwrite, bool? transparent)
        {
            captured.Calls++;
            captured.Transparency = transparent;
            byte[] bytes = format switch
            {
                "PDF" => Encoding.ASCII.GetBytes("%PDF-1.7"),
                "SVG" => Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
                "PNG" => PngHeader(2550, 3300),
                _ => throw new InvalidOperationException("Unexpected format."),
            };
            File.WriteAllBytes(path, bytes);
            return OperationResult<LayoutExportInfo>.Ok(new LayoutExportInfo
            {
                LayoutName = name,
                OutputPath = path,
                Format = format,
                Resolution = dpi,
                FileSizeBytes = bytes.LongLength,
                Overwritten = overwrite,
                MagicBytesHex = Convert.ToHexString(bytes.Take(8).ToArray()),
            });
        }
        layout.ExportLayoutHook = (name, path, format, dpi, overwrite) => WriteExport(name, path, format, dpi, overwrite, null);
        layout.ExportLayoutWithOptionsHook = WriteExport;
        return host;
    }

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

    private static void AssertSchema(IMCPTool tool, string[] propertyNames, params string[] required)
    {
        var properties = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(tool.InputSchema["properties"]);
        Assert.Equal(propertyNames, properties.Keys);
        Assert.Equal(required, Assert.IsAssignableFrom<IEnumerable<string>>(tool.InputSchema["required"]));
        Assert.Equal(false, tool.InputSchema["additionalProperties"]);
    }
}
