using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>D-084: tool contract paths and fail-closed cases; SDK mutations remain separate LIVE evidence.</summary>
public sealed class D084ToolTests
{
    private static ToolExecutionContext Ctx(
        Dictionary<string, object?>? args = null, IArcGISHost? host = null,
        MCPToolRegistry? registry = null, bool readOnly = false)
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

    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] values)
        => values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

    private static Dictionary<string, object?> Data(OperationResult<object?> result)
        => Assert.IsType<Dictionary<string, object?>>(result.Data);

    private static string? Code(OperationResult<object?> result)
        => result.Success ? null : result.Errors.FirstOrDefault()?.Code;

    [Fact]
    public async Task ListJobs_EnumeratesWithFilterAndRejectsUnknownState()
    {
        var listed = await new ListJobsTool().ExecuteAsync(Ctx(Args(("maxItems50", 1))));
        Assert.True(listed.Success);
        var data = Data(listed);
        Assert.True((int)data["returnedCount"]! <= 1);
        Assert.True(data.ContainsKey("totalMatched"));
        Assert.True(data.ContainsKey("truncated"));

        var invalid = await new ListJobsTool().ExecuteAsync(Ctx(Args(("state", "sleeping"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(invalid));
    }

    [Fact]
    public async Task CancelJob_UnknownAndUnsafeIdentifiersAreRejected()
    {
        var missing = await new CancelJobTool().ExecuteAsync(Ctx(Args(("jobId", "d084-missing-" + Guid.NewGuid().ToString("N")))));
        Assert.Equal(ErrorCodes.NotFound, Code(missing));
        var unsafeId = await new CancelJobTool().ExecuteAsync(Ctx(Args(("jobId", "..\\outside"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(unsafeId));
    }

    [Fact]
    public async Task ValidatePlan_ChecksRegisteredRequiredArgumentsAndRejectsMalformedPlan()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new GetGeometryInfoTool());
        var goodPlan = new Dictionary<string, object?>
        {
            ["steps"] = new object?[]
            {
                new Dictionary<string, object?>
                {
                    ["tool"] = "get_geometry_info",
                    ["args"] = Args(("layer", "Roads")),
                }
            }
        };
        var good = await new ValidatePlanTool().ExecuteAsync(Ctx(Args(("plan", goodPlan)), registry: registry));
        Assert.True(good.Success);
        Assert.Equal(true, Data(good)["valid"]);
        Assert.Equal(false, Data(good)["sideEffects"]);

        var badPlan = new Dictionary<string, object?> { ["steps"] = Array.Empty<object?>() };
        var bad = await new ValidatePlanTool().ExecuteAsync(Ctx(Args(("plan", badPlan)), registry: registry));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task DescribeToolCatalog_MapsKnownScenariosAndDisclosesUnmappedOnes()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new GetGeometryInfoTool());
        registry.Register(new FindIdenticalTool());
        registry.Register(new GenerateQualityReportTool());
        var mapped = await new DescribeToolCatalogTool().ExecuteAsync(Ctx(Args(("scenarioId", "S03")), registry: registry));
        Assert.True(mapped.Success);
        Assert.Equal("mapped", Data(mapped)["scenarioMappingStatus"]);
        Assert.Equal(3, Data(mapped)["returnedCount"]);

        var validUnmapped = await new DescribeToolCatalogTool().ExecuteAsync(Ctx(Args(("scenarioId", "S60")), registry: registry));
        Assert.True(validUnmapped.Success);
        Assert.Equal("unmapped", Data(validUnmapped)["scenarioMappingStatus"]);
        Assert.Equal(0, Data(validUnmapped)["returnedCount"]);
        var invalid = await new DescribeToolCatalogTool().ExecuteAsync(Ctx(Args(("scenarioId", "S61")), registry: registry));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(invalid));
    }

    [Fact]
    public async Task GetGeometryInfo_DelegatesValidInputsAndRejectsMissingLayer()
    {
        var analysis = new AnalysisFake();
        var host = new FakeArcGISHost(d084Analysis: analysis);
        var good = await new GetGeometryInfoTool().ExecuteAsync(Ctx(Args(("layer", "Roads")), host));
        Assert.True(good.Success);
        Assert.Equal("Roads", analysis.LastDataset);
        var bad = await new GetGeometryInfoTool().ExecuteAsync(Ctx(Args(("maxFeatures", 0)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
        Assert.Equal(1, analysis.GeometryCalls);
    }

    [Fact]
    public async Task FindIdentical_DelegatesSupportedModeAndRejectsUnknownMode()
    {
        var analysis = new AnalysisFake();
        var host = new FakeArcGISHost(d084Analysis: analysis);
        var good = await new FindIdenticalTool().ExecuteAsync(Ctx(Args(("layer", "Roads"), ("mode", "attributes")), host));
        Assert.True(good.Success);
        Assert.Equal("attributes", analysis.LastMode);
        var bad = await new FindIdenticalTool().ExecuteAsync(Ctx(Args(("layer", "Roads"), ("mode", "nearby")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
        Assert.Equal(1, analysis.IdenticalCalls);
    }

    [Fact]
    public async Task QualityReport_WritesHashedManifestAndRefusesOverwrite()
    {
        var analysis = new AnalysisFake();
        var host = new FakeArcGISHost(d084Analysis: analysis);
        var evidenceRoot = Environment.GetEnvironmentVariable("ARCGIS_PRO_MCP_TEST_TEMP_ROOT") ?? AppContext.BaseDirectory;
        var folder = Path.Combine(evidenceRoot, "d084-report-artifacts");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "quality-" + Guid.NewGuid().ToString("N") + ".json");
        Assert.Equal("D:\\", Path.GetPathRoot(path), ignoreCase: true);

        var written = await new GenerateQualityReportTool().ExecuteAsync(Ctx(Args(("dataset", "Roads"), ("reportPath", path)), host));
        Assert.True(written.Success);
        Assert.True(File.Exists(path));
        var data = Data(written);
        var manifest = Assert.IsType<Dictionary<string, object?>>(data["artifactManifest"]);
        var artifacts = Assert.IsType<Dictionary<string, object?>[]>(manifest["artifacts"]);
        Assert.Equal(path, artifacts[0]["path"]);
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        Assert.Equal(expectedHash, artifacts[0]["sha256"]);

        var again = await new GenerateQualityReportTool().ExecuteAsync(Ctx(Args(("dataset", "Roads"), ("reportPath", path)), host));
        Assert.Equal(ErrorCodes.OutputExists, Code(again));
    }

    [Fact]
    public async Task QualityReport_ReadOnlyModeRefusesPathBeforeAnalysisOrWrite()
    {
        var analysis = new AnalysisFake();
        var host = new FakeArcGISHost(d084Analysis: analysis);
        var path = Path.Combine(AppContext.BaseDirectory, "must-not-be-written.json");
        var result = await new GenerateQualityReportTool().ExecuteAsync(
            Ctx(Args(("dataset", "Roads"), ("reportPath", path)), host, readOnly: true));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(result));
        Assert.Equal(0, analysis.QualityCalls);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task SetLayoutElementProperties_ForwardsAllowlistedChangeAndRejectsUnknownKey()
    {
        var host = new FakeArcGISHost();
        var service = Assert.IsType<NotImplementedService>(host.Layout);
        var calls = 0;
        service.SetElementPropertiesHook = (layout, element, properties, units) =>
        {
            calls++;
            Assert.Equal("Main", layout);
            Assert.Equal("Title", element);
            Assert.Equal("page", units);
            return OperationResult<object?>.Ok(new Dictionary<string, object?> { ["updated"] = true });
        };
        var good = await new SetLayoutElementPropertiesTool().ExecuteAsync(Ctx(Args(
            ("layout", "Main"), ("elementId", "Title"), ("properties", Args(("x", 2d)))), host));
        Assert.True(good.Success);
        var bad = await new SetLayoutElementPropertiesTool().ExecuteAsync(Ctx(Args(
            ("layout", "Main"), ("elementId", "Title"), ("properties", Args(("script", "x")))), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task SetLabelProperties_ForwardsSafeFieldAndRejectsExpressionScript()
    {
        var host = new FakeArcGISHost();
        var layerService = Assert.IsType<FakeLayerService>(host.Layers);
        var calls = 0;
        layerService.SetLabelPropertiesHandler = (map, layer, expression, font, size, placement, visible) =>
        {
            calls++;
            Assert.Null(map);
            Assert.Equal("Roads", layer);
            Assert.Equal("$feature.Name", expression);
            return OperationResult<object?>.Ok(new Dictionary<string, object?> { ["updatedLabelClasses"] = 1 });
        };
        var good = await new SetLabelPropertiesTool().ExecuteAsync(Ctx(Args(("layer", "Roads"), ("expression", "$feature.Name")), host));
        Assert.True(good.Success);
        var bad = await new SetLabelPropertiesTool().ExecuteAsync(Ctx(Args(("layer", "Roads"), ("expression", "return System();")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ConfigureMapSeries_ForwardsValidFieldsAndRejectsUnknownExtentSource()
    {
        var host = new FakeArcGISHost();
        var service = Assert.IsType<NotImplementedService>(host.Layout);
        var extents = new List<string>();
        service.ConfigureMapSeriesHook = (map, index, sort, extent, name) =>
        {
            Assert.Equal("Planning", map);
            Assert.Equal("PageNo", index);
            extents.Add(extent);
            return OperationResult<object?>.Ok(new Dictionary<string, object?> { ["updated"] = true });
        };
        var good = await new ConfigureMapSeriesTool().ExecuteAsync(Ctx(Args(
            ("map", "Planning"), ("indexField", "PageNo"), ("extentSource", "layer")), host));
        Assert.True(good.Success);
        var fixedScale = await new ConfigureMapSeriesTool().ExecuteAsync(Ctx(Args(
            ("map", "Planning"), ("indexField", "PageNo"), ("extentSource", "fixed")), host));
        Assert.True(fixedScale.Success);
        var bad = await new ConfigureMapSeriesTool().ExecuteAsync(Ctx(Args(
            ("map", "Planning"), ("indexField", "PageNo"), ("extentSource", "other")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
        Assert.Equal(new[] { "layer", "fixed" }, extents);
    }

    private sealed class AnalysisFake : ID084AnalysisService
    {
        public int GeometryCalls { get; private set; }
        public int IdenticalCalls { get; private set; }
        public int QualityCalls { get; private set; }
        public string? LastDataset { get; private set; }
        public string? LastMode { get; private set; }

        private static OperationResult<IReadOnlyDictionary<string, object?>> Result(string key, object? value)
            => OperationResult<IReadOnlyDictionary<string, object?>>.Ok(new Dictionary<string, object?> { [key] = value });

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> GetGeometryInfoAsync(
            string layerOrPath, string geometryUnit, int maxFeatures, CancellationToken ct = default)
        {
            GeometryCalls++;
            LastDataset = layerOrPath;
            return Task.FromResult(Result("geometryUnit", geometryUnit));
        }

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> FindIdenticalAsync(
            string layerOrPath, string mode, IReadOnlyList<string> fields, double tolerance, int maxFeatures, CancellationToken ct = default)
        {
            IdenticalCalls++;
            LastMode = mode;
            return Task.FromResult(Result("mode", mode));
        }

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> GenerateQualityReportAsync(
            string dataset, IReadOnlyList<string> rules, string severityFloor, int maxFeatures, CancellationToken ct = default)
        {
            QualityCalls++;
            LastDataset = dataset;
            return Task.FromResult(Result("dataset", dataset));
        }
    }
}
