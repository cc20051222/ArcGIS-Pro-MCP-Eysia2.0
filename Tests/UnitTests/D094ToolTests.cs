using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>D-094: M2 P5 eight analysis tool contract paths and fail-closed cases.</summary>
public sealed class D094ToolTests
{
    private static ToolExecutionContext Ctx(
        Dictionary<string, object?>? args = null, IArcGISHost? host = null, bool readOnly = false)
    {
        var mode = new ReadOnlyModeService();
        if (readOnly) mode.Set(true);
        return new ToolExecutionContext
        {
            Host = host ?? new FakeArcGISHost(),
            Arguments = args ?? new Dictionary<string, object?>(),
            ReadOnly = mode,
        };
    }

    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] values)
        => values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

    private static string? Code(OperationResult<object?> result)
        => result.Success ? null : result.Errors.FirstOrDefault()?.Code;

    private static IArcGISHost HostWithAnalysis(AnalysisFake? fake = null)
        => new FakeArcGISHost(d094Analysis: fake ?? new AnalysisFake());

    // ═══════════════════ simplify_features ═══════════════════

    [Fact]
    public async Task SimplifyFeatures_DelegatesValidInputAndRejectsMissingInput()
    {
        var fake = new AnalysisFake();
        var host = HostWithAnalysis(fake);
        var good = await new SimplifyFeaturesTool().ExecuteAsync(
            Ctx(Args(("input", "roads"), ("outputPath", "D:/out.gdb/simplified"), ("algorithm", "POINT_REMOVE"), ("tolerance", 1.0)), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.SimplifyCalls);

        var bad = await new SimplifyFeaturesTool().ExecuteAsync(
            Ctx(Args(("outputPath", "D:/out"), ("algorithm", "POINT_REMOVE"), ("tolerance", 1.0)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task SimplifyFeatures_RejectsInvalidAlgorithm()
    {
        var host = HostWithAnalysis();
        var bad = await new SimplifyFeaturesTool().ExecuteAsync(
            Ctx(Args(("input", "r"), ("outputPath", "D:/o"), ("algorithm", "INVALID"), ("tolerance", 1.0)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task SimplifyFeatures_ReadOnlyRefuses()
    {
        var host = HostWithAnalysis();
        var result = await new SimplifyFeaturesTool().ExecuteAsync(
            Ctx(Args(("input", "r"), ("outputPath", "D:/o"), ("algorithm", "POINT_REMOVE"), ("tolerance", 1.0)), host, readOnly: true));
        Assert.Equal(ErrorCodes.PermissionDenied, Code(result));
    }

    // ═══════════════════ smooth_features ═══════════════════

    [Fact]
    public async Task SmoothFeatures_DelegatesValidInputAndRejectsMissingTolerance()
    {
        var fake = new AnalysisFake();
        var host = HostWithAnalysis(fake);
        var good = await new SmoothFeaturesTool().ExecuteAsync(
            Ctx(Args(("input", "roads"), ("outputPath", "D:/out.gdb/smoothed"), ("algorithm", "PAEK"), ("tolerance", 2.0)), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.SmoothCalls);

        var bad = await new SmoothFeaturesTool().ExecuteAsync(
            Ctx(Args(("input", "r"), ("outputPath", "D:/o"), ("algorithm", "PAEK")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task SmoothFeatures_RejectsInvalidAlgorithm()
    {
        var host = HostWithAnalysis();
        var bad = await new SmoothFeaturesTool().ExecuteAsync(
            Ctx(Args(("input", "r"), ("outputPath", "D:/o"), ("algorithm", "INVALID"), ("tolerance", 1.0)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ polygon_neighbors ═══════════════════

    [Fact]
    public async Task PolygonNeighbors_DelegatesValidInputAndRejectsMissingOutputPath()
    {
        var fake = new AnalysisFake();
        var host = HostWithAnalysis(fake);
        var good = await new PolygonNeighborsTool().ExecuteAsync(
            Ctx(Args(("input", "parcels"), ("outputPath", "D:/out.gdb/neighbors")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.PolygonNeighborsCalls);

        var bad = await new PolygonNeighborsTool().ExecuteAsync(
            Ctx(Args(("input", "parcels")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task PolygonNeighbors_ReadOnlyRefuses()
    {
        var host = HostWithAnalysis();
        var result = await new PolygonNeighborsTool().ExecuteAsync(
            Ctx(Args(("input", "p"), ("outputPath", "D:/o")), host, readOnly: true));
        Assert.Equal(ErrorCodes.PermissionDenied, Code(result));
    }

    // ═══════════════════ generate_tessellation ═══════════════════

    [Fact]
    public async Task GenerateTessellation_DelegatesValidInputAndRejectsMissingExtent()
    {
        var fake = new AnalysisFake();
        var host = HostWithAnalysis(fake);
        var extent = Args(("xmin", 0.0), ("ymin", 0.0), ("xmax", 100.0), ("ymax", 100.0), ("sr", 4326));
        var good = await new GenerateTessellationTool().ExecuteAsync(
            Ctx(Args(("extent", extent), ("outputPath", "D:/out.gdb/tess"), ("shapeType", "HEXAGON"), ("size", 10.0)), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.TessellationCalls);

        var bad = await new GenerateTessellationTool().ExecuteAsync(
            Ctx(Args(("outputPath", "D:/o"), ("shapeType", "SQUARE"), ("size", 10.0)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task GenerateTessellation_RejectsInvalidShapeType()
    {
        var host = HostWithAnalysis();
        var extent = Args(("xmin", 0.0), ("ymin", 0.0), ("xmax", 1.0), ("ymax", 1.0));
        var bad = await new GenerateTessellationTool().ExecuteAsync(
            Ctx(Args(("extent", extent), ("outputPath", "D:/o"), ("shapeType", "INVALID"), ("size", 1.0)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ calculate_service_areas ═══════════════════

    [Fact]
    public async Task CalculateServiceAreas_DelegatesValidInputAndRejectsMissingImpedance()
    {
        var fake = new AnalysisFake();
        var host = HostWithAnalysis(fake);
        var breaks = new object?[] { 5.0, 10.0, 15.0 };
        var good = await new CalculateServiceAreasTool().ExecuteAsync(
            Ctx(Args(("network", "streets"), ("facilities", "fire_stations"), ("outputPath", "D:/out.gdb/sa"), ("breaks", breaks), ("impedance", "TravelTime")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.ServiceAreaCalls);

        var bad = await new CalculateServiceAreasTool().ExecuteAsync(
            Ctx(Args(("network", "n"), ("facilities", "f"), ("outputPath", "D:/o"), ("breaks", breaks)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task CalculateServiceAreas_RejectsEmptyBreaks()
    {
        var host = HostWithAnalysis();
        var bad = await new CalculateServiceAreasTool().ExecuteAsync(
            Ctx(Args(("network", "n"), ("facilities", "f"), ("outputPath", "D:/o"), ("breaks", Array.Empty<object?>()), ("impedance", "T")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task CalculateServiceAreas_RejectsInvalidTravelDirection()
    {
        var host = HostWithAnalysis();
        var breaks = new object?[] { 5.0 };
        var bad = await new CalculateServiceAreasTool().ExecuteAsync(
            Ctx(Args(("network", "n"), ("facilities", "f"), ("outputPath", "D:/o"), ("breaks", breaks), ("impedance", "T"), ("travelDirection", "invalid")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ solve_routes ═══════════════════

    [Fact]
    public async Task SolveRoutes_DelegatesValidInputAndRejectsMissingStops()
    {
        var fake = new AnalysisFake();
        var host = HostWithAnalysis(fake);
        var good = await new SolveRoutesTool().ExecuteAsync(
            Ctx(Args(("network", "streets"), ("stops", "depots"), ("outputPath", "D:/out.gdb/routes"), ("impedance", "TravelTime")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.SolveRoutesCalls);

        var bad = await new SolveRoutesTool().ExecuteAsync(
            Ctx(Args(("network", "n"), ("outputPath", "D:/o"), ("impedance", "T")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task SolveRoutes_ReadOnlyRefuses()
    {
        var host = HostWithAnalysis();
        var result = await new SolveRoutesTool().ExecuteAsync(
            Ctx(Args(("network", "n"), ("stops", "s"), ("outputPath", "D:/o"), ("impedance", "T")), host, readOnly: true));
        Assert.Equal(ErrorCodes.PermissionDenied, Code(result));
    }

    // ═══════════════════ spatial_autocorrelation ═══════════════════

    [Fact]
    public async Task SpatialAutocorrelation_DelegatesValidInputAndRejectsMissingField()
    {
        var fake = new AnalysisFake();
        var host = HostWithAnalysis(fake);
        var good = await new SpatialAutocorrelationTool().ExecuteAsync(
            Ctx(Args(("input", "census"), ("outputPath", "D:/out.gdb/moran"), ("field", "POPULATION")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.AutocorrelationCalls);

        var bad = await new SpatialAutocorrelationTool().ExecuteAsync(
            Ctx(Args(("input", "c"), ("outputPath", "D:/o")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task SpatialAutocorrelation_RejectsInvalidConceptualization()
    {
        var host = HostWithAnalysis();
        var bad = await new SpatialAutocorrelationTool().ExecuteAsync(
            Ctx(Args(("input", "c"), ("outputPath", "D:/o"), ("field", "F"), ("conceptualization", "INVALID")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task SpatialAutocorrelation_RejectsInvalidStandardization()
    {
        var host = HostWithAnalysis();
        var bad = await new SpatialAutocorrelationTool().ExecuteAsync(
            Ctx(Args(("input", "c"), ("outputPath", "D:/o"), ("field", "F"), ("standardization", "INVALID")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ hotspot_analysis ═══════════════════

    [Fact]
    public async Task HotspotAnalysis_DelegatesValidInputAndRejectsMissingInput()
    {
        var fake = new AnalysisFake();
        var host = HostWithAnalysis(fake);
        var good = await new HotspotAnalysisTool().ExecuteAsync(
            Ctx(Args(("input", "crime"), ("outputPath", "D:/out.gdb/hotspot"), ("field", "INCIDENTS")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.HotspotCalls);

        var bad = await new HotspotAnalysisTool().ExecuteAsync(
            Ctx(Args(("outputPath", "D:/o"), ("field", "F")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task HotspotAnalysis_ReadOnlyRefuses()
    {
        var host = HostWithAnalysis();
        var result = await new HotspotAnalysisTool().ExecuteAsync(
            Ctx(Args(("input", "i"), ("outputPath", "D:/o"), ("field", "F")), host, readOnly: true));
        Assert.Equal(ErrorCodes.PermissionDenied, Code(result));
    }

    // ═══════════════════ Cross-cutting ═══════════════════

    [Fact]
    public async Task AllWriteTools_NoServiceReturnsNotImplemented()
    {
        var host = new FakeArcGISHost(); // no D094Analysis
        var tools = new (McpToolBase tool, Dictionary<string, object?> args)[]
        {
            (new SimplifyFeaturesTool(), Args(("input", "r"), ("outputPath", "D:/o"), ("algorithm", "POINT_REMOVE"), ("tolerance", 1.0))),
            (new SmoothFeaturesTool(), Args(("input", "r"), ("outputPath", "D:/o"), ("algorithm", "PAEK"), ("tolerance", 1.0))),
            (new PolygonNeighborsTool(), Args(("input", "p"), ("outputPath", "D:/o"))),
            (new GenerateTessellationTool(), Args(("extent", Args(("xmin", 0.0))), ("outputPath", "D:/o"), ("shapeType", "SQUARE"), ("size", 1.0))),
            (new CalculateServiceAreasTool(), Args(("network", "n"), ("facilities", "f"), ("outputPath", "D:/o"), ("breaks", new object?[] { 1.0 }), ("impedance", "T"))),
            (new SolveRoutesTool(), Args(("network", "n"), ("stops", "s"), ("outputPath", "D:/o"), ("impedance", "T"))),
            (new SpatialAutocorrelationTool(), Args(("input", "c"), ("outputPath", "D:/o"), ("field", "F"))),
            (new HotspotAnalysisTool(), Args(("input", "i"), ("outputPath", "D:/o"), ("field", "F"))),
        };
        foreach (var (tool, args) in tools)
        {
            var result = await tool.ExecuteAsync(Ctx(args, host));
            Assert.Equal(ErrorCodes.NotImplemented, Code(result));
        }
    }

    [Fact]
    public void AllEightTools_HaveCorrectMetadata()
    {
        var tools = new McpToolBase[]
        {
            new SimplifyFeaturesTool(), new SmoothFeaturesTool(), new PolygonNeighborsTool(),
            new GenerateTessellationTool(), new CalculateServiceAreasTool(), new SolveRoutesTool(),
            new SpatialAutocorrelationTool(), new HotspotAnalysisTool(),
        };
        Assert.Equal(8, tools.Length);
        foreach (var t in tools)
        {
            Assert.Equal(ToolCategories.Analysis, t.Metadata.Category);
            Assert.True(t.Metadata.RequiresArcGIS);
            Assert.NotNull(t.InputSchema);
            Assert.Equal("object", t.InputSchema["type"]);
            Assert.Equal(false, t.InputSchema["additionalProperties"]);
        }
        // 7 GP + 1 Native (polygon_neighbors)
        Assert.Equal(7, tools.Count(t => t.Metadata.ExecutionType == ExecutionTypes.Geoprocessing));
        Assert.Equal(1, tools.Count(t => t.Metadata.ExecutionType == ExecutionTypes.Native));
    }

    [Fact]
    public void AllEightTools_InContractSnapshotWith224Total()
    {
        var snapshot = ProductionToolContractSnapshot.Tools;
        Assert.Equal(239, snapshot.Count);
        var names = new[]
        {
            "simplify_features", "smooth_features", "polygon_neighbors", "generate_tessellation",
            "calculate_service_areas", "solve_routes", "spatial_autocorrelation", "hotspot_analysis",
        };
        foreach (var name in names)
            Assert.Contains(snapshot, t => t.Name == name);
        Assert.Equal(snapshot.Count, snapshot.Select(t => t.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AllEightTools_ClassifiedCorrectlyInRoster()
    {
        var writeTools = new[]
        {
            "simplify_features", "smooth_features", "polygon_neighbors", "generate_tessellation",
            "calculate_service_areas", "solve_routes", "spatial_autocorrelation", "hotspot_analysis",
        };
        foreach (var name in writeTools)
            Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf(name));
    }

    // ═══════════════════ Fake ═══════════════════

    private sealed class AnalysisFake : ID094AnalysisService
    {
        public int SimplifyCalls { get; private set; }
        public int SmoothCalls { get; private set; }
        public int PolygonNeighborsCalls { get; private set; }
        public int TessellationCalls { get; private set; }
        public int ServiceAreaCalls { get; private set; }
        public int SolveRoutesCalls { get; private set; }
        public int AutocorrelationCalls { get; private set; }
        public int HotspotCalls { get; private set; }

        private static OperationResult<IReadOnlyDictionary<string, object?>> Ok()
            => OperationResult<IReadOnlyDictionary<string, object?>>.Ok(new Dictionary<string, object?> { ["status"] = "ok" });

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> SimplifyFeaturesAsync(string i, string o, string a, double t, bool pt, bool ow, CancellationToken ct = default)
        { SimplifyCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> SmoothFeaturesAsync(string i, string o, string a, double t, bool pe, bool ow, CancellationToken ct = default)
        { SmoothCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> PolygonNeighborsAsync(string i, string o, bool iel, bool ipt, bool ow, CancellationToken ct = default)
        { PolygonNeighborsCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> GenerateTessellationAsync(IReadOnlyDictionary<string, object?> e, string o, string st, double s, bool ow, CancellationToken ct = default)
        { TessellationCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CalculateServiceAreasAsync(string n, string f, string o, IReadOnlyList<object?> b, string imp, string td, bool ow, CancellationToken ct = default)
        { ServiceAreaCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> SolveRoutesAsync(string n, string s, string o, string imp, bool fbo, bool pfl, bool ow, CancellationToken ct = default)
        { SolveRoutesCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> SpatialAutocorrelationAsync(string i, string o, string f, string c, string s, bool l, bool ow, CancellationToken ct = default)
        { AutocorrelationCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> HotspotAnalysisAsync(string i, string o, string f, string c, bool fdr, bool l, bool ow, CancellationToken ct = default)
        { HotspotCalls++; return Task.FromResult(Ok()); }
    }
}
