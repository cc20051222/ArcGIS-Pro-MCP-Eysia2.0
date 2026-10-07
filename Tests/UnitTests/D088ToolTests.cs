using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>D-088: M2 P1/P2 quality and lineage tool contract paths and fail-closed cases.</summary>
public sealed class D088ToolTests
{
    private static ToolExecutionContext Ctx(
        Dictionary<string, object?>? args = null, IArcGISHost? host = null,
        bool readOnly = false, IPythonBridgeService? python = null)
    {
        var mode = new ReadOnlyModeService();
        if (readOnly) mode.Set(true);
        return new ToolExecutionContext
        {
            Host = host ?? new FakeArcGISHost(),
            Arguments = args ?? new Dictionary<string, object?>(),
            ReadOnly = mode,
            Python = python,
        };
    }

    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] values)
        => values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

    private static string? Code(OperationResult<object?> result)
        => result.Success ? null : result.Errors.FirstOrDefault()?.Code;

    private static IArcGISHost HostWithQuality(QualityFake? fake = null)
        => new FakeArcGISHost(d088Quality: fake ?? new QualityFake());

    // ═══════════════════ validate_geometries ═══════════════════

    [Fact]
    public async Task ValidateGeometries_DelegatesValidInputAndRejectsMissingDataset()
    {
        var fake = new QualityFake();
        var host = HostWithQuality(fake);
        var good = await new ValidateGeometriesTool().ExecuteAsync(Ctx(Args(("dataset", "Roads")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.ValidateGeometriesCalls);

        var bad = await new ValidateGeometriesTool().ExecuteAsync(Ctx(Args(), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
        Assert.Equal(1, fake.ValidateGeometriesCalls);
    }

    [Fact]
    public async Task ValidateGeometries_RejectsUnsupportedCheck()
    {
        var host = HostWithQuality();
        var bad = await new ValidateGeometriesTool().ExecuteAsync(
            Ctx(Args(("dataset", "Roads"), ("checks", new[] { "invalid_check" })), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task ValidateGeometries_RejectsInvalidMaxFeatures()
    {
        var host = HostWithQuality();
        var bad = await new ValidateGeometriesTool().ExecuteAsync(
            Ctx(Args(("dataset", "Roads"), ("maxFeatures", 0)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task ValidateGeometries_ReadOnlyRefusesReportPath()
    {
        var host = HostWithQuality();
        var result = await new ValidateGeometriesTool().ExecuteAsync(
            Ctx(Args(("dataset", "Roads"), ("reportPath", @"D:\temp\report.json")), host, readOnly: true));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(result));
    }

    [Fact]
    public async Task ValidateGeometries_WritesReportWithManifestAndRefusesOverwrite()
    {
        var host = HostWithQuality();
        var evidenceRoot = Environment.GetEnvironmentVariable("ARCGIS_PRO_MCP_TEST_TEMP_ROOT") ?? AppContext.BaseDirectory;
        var folder = Path.Combine(evidenceRoot, "d088-report-artifacts");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "geom-" + Guid.NewGuid().ToString("N") + ".json");

        var written = await new ValidateGeometriesTool().ExecuteAsync(
            Ctx(Args(("dataset", "Roads"), ("reportPath", path)), host));
        Assert.True(written.Success);
        Assert.True(File.Exists(path));
        var data = Assert.IsType<Dictionary<string, object?>>(written.Data);
        Assert.True(data.ContainsKey("artifactManifest"));

        var again = await new ValidateGeometriesTool().ExecuteAsync(
            Ctx(Args(("dataset", "Roads"), ("reportPath", path)), host));
        Assert.Equal(ErrorCodes.OutputExists, Code(again));
    }

    [Fact]
    public async Task ValidateGeometries_RefusesNonDDrivePath()
    {
        var host = HostWithQuality();
        var result = await new ValidateGeometriesTool().ExecuteAsync(
            Ctx(Args(("dataset", "Roads"), ("reportPath", @"C:\temp\report.json")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(result));
    }

    [Fact]
    public async Task ValidateGeometries_NoServiceReturnsNotImplemented()
    {
        var host = new FakeArcGISHost(); // no D088Quality
        var result = await new ValidateGeometriesTool().ExecuteAsync(Ctx(Args(("dataset", "Roads")), host));
        Assert.Equal(ErrorCodes.NotImplemented, Code(result));
    }

    // ═══════════════════ check_topology_rules ═══════════════════

    [Fact]
    public async Task CheckTopologyRules_DelegatesValidInputAndRejectsEmptyRules()
    {
        var fake = new QualityFake();
        var host = HostWithQuality(fake);
        var good = await new CheckTopologyRulesTool().ExecuteAsync(
            Ctx(Args(("dataset", "Parcels"), ("rules", new[] { "must_not_overlap" })), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.CheckTopologyCalls);

        var bad = await new CheckTopologyRulesTool().ExecuteAsync(
            Ctx(Args(("dataset", "Parcels"), ("rules", Array.Empty<string>())), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
        Assert.Equal(1, fake.CheckTopologyCalls);
    }

    [Fact]
    public async Task CheckTopologyRules_RejectsNegativeClusterTolerance()
    {
        var host = HostWithQuality();
        var bad = await new CheckTopologyRulesTool().ExecuteAsync(
            Ctx(Args(("dataset", "P"), ("rules", new[] { "r" }), ("clusterTolerance", -1.0)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ compare_datasets ═══════════════════

    [Fact]
    public async Task CompareDatasets_DelegatesValidInputAndRejectsMissingPaths()
    {
        var fake = new QualityFake();
        var host = HostWithQuality(fake);
        var good = await new CompareDatasetsTool().ExecuteAsync(
            Ctx(Args(("left", "A.gdb\\Roads"), ("right", "B.gdb\\Roads")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.CompareDatasetsCalls);

        var bad = await new CompareDatasetsTool().ExecuteAsync(Ctx(Args(("left", "A")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task CompareDatasets_RejectsNegativeTolerance()
    {
        var host = HostWithQuality();
        var bad = await new CompareDatasetsTool().ExecuteAsync(
            Ctx(Args(("left", "A"), ("right", "B"), ("tolerance", -0.5)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ compare_schemas ═══════════════════

    [Fact]
    public async Task CompareSchemas_DelegatesValidInputAndRejectsMissingPaths()
    {
        var fake = new QualityFake();
        var host = HostWithQuality(fake);
        var good = await new CompareSchemasTool().ExecuteAsync(
            Ctx(Args(("left", "A.gdb\\T"), ("right", "B.gdb\\T")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.CompareSchemasCalls);

        var bad = await new CompareSchemasTool().ExecuteAsync(Ctx(Args(("left", "A")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ validate_field_constraints ═══════════════════

    [Fact]
    public async Task ValidateFieldConstraints_DelegatesValidInputAndRejectsEmptyConstraints()
    {
        var fake = new QualityFake();
        var host = HostWithQuality(fake);
        var constraints = new object?[] { Args(("field", "NAME"), ("kind", "null")) };
        var good = await new ValidateFieldConstraintsTool().ExecuteAsync(
            Ctx(Args(("dataset", "T"), ("constraints", constraints)), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.ValidateFieldConstraintsCalls);

        var bad = await new ValidateFieldConstraintsTool().ExecuteAsync(
            Ctx(Args(("dataset", "T"), ("constraints", Array.Empty<object?>())), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task ValidateFieldConstraints_RejectsUnsupportedKind()
    {
        var host = HostWithQuality();
        var constraints = new object?[] { Args(("field", "X"), ("kind", "regex")) };
        var bad = await new ValidateFieldConstraintsTool().ExecuteAsync(
            Ctx(Args(("dataset", "T"), ("constraints", constraints)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task ValidateFieldConstraints_RejectsMissingField()
    {
        var host = HostWithQuality();
        var constraints = new object?[] { Args(("kind", "null")) };
        var bad = await new ValidateFieldConstraintsTool().ExecuteAsync(
            Ctx(Args(("dataset", "T"), ("constraints", constraints)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ inspect_raster_alignment ═══════════════════

    [Fact]
    public async Task InspectRasterAlignment_RejectsWithoutPythonBridge()
    {
        var host = HostWithQuality();
        var result = await new InspectRasterAlignmentTool().ExecuteAsync(
            Ctx(Args(("rasters", new[] { "a.tif" })), host, python: null));
        Assert.Equal(ErrorCodes.PythonBridgeUnavailable, Code(result));
    }

    [Fact]
    public async Task InspectRasterAlignment_DelegatesWithBridgeAndRejectsEmptyRasters()
    {
        var fake = new QualityFake();
        var host = HostWithQuality(fake);
        var bridge = new FakePythonBridge();
        var good = await new InspectRasterAlignmentTool().ExecuteAsync(
            Ctx(Args(("rasters", new[] { "a.tif", "b.tif" })), host, python: bridge));
        Assert.True(good.Success);
        Assert.Equal(1, fake.InspectRasterAlignmentCalls);

        var bad = await new InspectRasterAlignmentTool().ExecuteAsync(
            Ctx(Args(("rasters", Array.Empty<string>())), host, python: bridge));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task InspectRasterAlignment_RejectsNegativeSnapTolerance()
    {
        var host = HostWithQuality();
        var bridge = new FakePythonBridge();
        var bad = await new InspectRasterAlignmentTool().ExecuteAsync(
            Ctx(Args(("rasters", new[] { "a.tif" }), ("snapTolerance", -1.0)), host, python: bridge));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ list_geographic_transformations ═══════════════════

    [Fact]
    public async Task ListGeographicTransformations_DelegatesValidInputAndRejectsMissingCrs()
    {
        var fake = new QualityFake();
        var host = HostWithQuality(fake);
        var good = await new ListGeographicTransformationsTool().ExecuteAsync(
            Ctx(Args(("sourceCrs", "4326"), ("targetCrs", "3857")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.ListGeoTransformsCalls);

        var bad = await new ListGeographicTransformationsTool().ExecuteAsync(
            Ctx(Args(("sourceCrs", "4326")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task ListGeographicTransformations_RejectsInvalidMaxItems()
    {
        var host = HostWithQuality();
        var bad = await new ListGeographicTransformationsTool().ExecuteAsync(
            Ctx(Args(("sourceCrs", "4326"), ("targetCrs", "3857"), ("maxItems", 0)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
        var bad2 = await new ListGeographicTransformationsTool().ExecuteAsync(
            Ctx(Args(("sourceCrs", "4326"), ("targetCrs", "3857"), ("maxItems", 1001)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad2));
    }

    // ═══════════════════ trace_dataset_dependencies ═══════════════════

    [Fact]
    public async Task TraceDatasetDependencies_DelegatesValidInputAndRejectsMissingRoot()
    {
        var fake = new QualityFake();
        var host = HostWithQuality(fake);
        var good = await new TraceDatasetDependenciesTool().ExecuteAsync(
            Ctx(Args(("root", "C:\\data\\project.aprx")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.TraceDependenciesCalls);

        var bad = await new TraceDatasetDependenciesTool().ExecuteAsync(Ctx(Args(), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task TraceDatasetDependencies_RejectsInvalidMaxDepth()
    {
        var host = HostWithQuality();
        var bad = await new TraceDatasetDependenciesTool().ExecuteAsync(
            Ctx(Args(("root", "x"), ("maxDepth", 7)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ get_dataset_lineage ═══════════════════

    [Fact]
    public async Task GetDatasetLineage_DelegatesValidInputAndRejectsMissingDataset()
    {
        var fake = new QualityFake();
        var host = HostWithQuality(fake);
        var good = await new GetDatasetLineageTool().ExecuteAsync(
            Ctx(Args(("dataset", "C:\\data\\out.gdb\\Roads")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.GetLineageCalls);

        var bad = await new GetDatasetLineageTool().ExecuteAsync(Ctx(Args(), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task GetDatasetLineage_RejectsInvalidMaxDepth()
    {
        var host = HostWithQuality();
        var bad = await new GetDatasetLineageTool().ExecuteAsync(
            Ctx(Args(("dataset", "x"), ("maxDepth", 11)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ Schema contract verification ═══════════════════

    [Fact]
    public void AllNineTools_HaveCorrectMetadata()
    {
        var tools = new McpToolBase[]
        {
            new ValidateGeometriesTool(), new CheckTopologyRulesTool(), new CompareDatasetsTool(),
            new CompareSchemasTool(), new ValidateFieldConstraintsTool(), new InspectRasterAlignmentTool(),
            new ListGeographicTransformationsTool(), new TraceDatasetDependenciesTool(), new GetDatasetLineageTool(),
        };
        Assert.Equal(9, tools.Length);
        foreach (var t in tools)
        {
            Assert.Equal(ExecutionTypes.Native, t.Metadata.ExecutionType);
            Assert.True(t.Metadata.RequiresArcGIS);
            Assert.NotNull(t.InputSchema);
            Assert.Equal("object", t.InputSchema["type"]);
            Assert.Equal(false, t.InputSchema["additionalProperties"]);
        }
    }

    [Fact]
    public void AllNineTools_AreInContractSnapshotWith224Total()
    {
        var snapshot = ProductionToolContractSnapshot.Tools;
        Assert.Equal(239, snapshot.Count);
        var names = new[]
        {
            "validate_geometries", "check_topology_rules", "compare_datasets", "compare_schemas",
            "validate_field_constraints", "inspect_raster_alignment", "list_geographic_transformations",
            "trace_dataset_dependencies", "get_dataset_lineage",
        };
        foreach (var name in names)
        {
            Assert.Contains(snapshot, t => t.Name == name);
        }
        // No duplicates
        Assert.Equal(snapshot.Count, snapshot.Select(t => t.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AllNineTools_ClassifiedAsReadInRoster()
    {
        var names = new[]
        {
            "validate_geometries", "check_topology_rules", "compare_datasets", "compare_schemas",
            "validate_field_constraints", "inspect_raster_alignment", "list_geographic_transformations",
            "trace_dataset_dependencies", "get_dataset_lineage",
        };
        foreach (var name in names)
        {
            Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf(name));
        }
    }

    // ═══════════════════ Fakes ═══════════════════

    private sealed class QualityFake : ID088QualityService
    {
        public int ValidateGeometriesCalls { get; private set; }
        public int CheckTopologyCalls { get; private set; }
        public int CompareDatasetsCalls { get; private set; }
        public int CompareSchemasCalls { get; private set; }
        public int ValidateFieldConstraintsCalls { get; private set; }
        public int InspectRasterAlignmentCalls { get; private set; }
        public int ListGeoTransformsCalls { get; private set; }
        public int TraceDependenciesCalls { get; private set; }
        public int GetLineageCalls { get; private set; }

        private static OperationResult<IReadOnlyDictionary<string, object?>> Ok(string key = "status", object? val = null)
            => OperationResult<IReadOnlyDictionary<string, object?>>.Ok(new Dictionary<string, object?> { [key] = val ?? "ok" });

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ValidateGeometriesAsync(
            string dataset, IReadOnlyList<string> checks, double minimumSegmentLength, int maxFeatures, CancellationToken ct = default)
        { ValidateGeometriesCalls++; return Task.FromResult(Ok()); }

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CheckTopologyRulesAsync(
            string dataset, IReadOnlyList<string> rules, double clusterTolerance, CancellationToken ct = default)
        { CheckTopologyCalls++; return Task.FromResult(Ok()); }

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CompareDatasetsAsync(
            string left, string right, IReadOnlyList<string> keyFields, bool compareGeometry, double tolerance, int maxFeatures, CancellationToken ct = default)
        { CompareDatasetsCalls++; return Task.FromResult(Ok()); }

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CompareSchemasAsync(
            string left, string right, bool ignoreOrder, CancellationToken ct = default)
        { CompareSchemasCalls++; return Task.FromResult(Ok()); }

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ValidateFieldConstraintsAsync(
            string dataset, IReadOnlyList<IReadOnlyDictionary<string, object?>> constraints, int maxFeatures, CancellationToken ct = default)
        { ValidateFieldConstraintsCalls++; return Task.FromResult(Ok()); }

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> InspectRasterAlignmentAsync(
            IReadOnlyList<string> rasters, string? reference, double snapTolerance, CancellationToken ct = default)
        { InspectRasterAlignmentCalls++; return Task.FromResult(Ok()); }

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ListGeographicTransformationsAsync(
            string sourceCrs, string targetCrs, IReadOnlyDictionary<string, object?>? extent, int maxItems, CancellationToken ct = default)
        { ListGeoTransformsCalls++; return Task.FromResult(Ok()); }

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> TraceDatasetDependenciesAsync(
            string root, int maxDepth, bool includeBroken, CancellationToken ct = default)
        { TraceDependenciesCalls++; return Task.FromResult(Ok()); }

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> GetDatasetLineageAsync(
            string dataset, int maxDepth, bool includeArtifacts, CancellationToken ct = default)
        { GetLineageCalls++; return Task.FromResult(Ok()); }
    }

    private sealed class FakePythonBridge : IPythonBridgeService
    {
        public Task<OperationResult<string>> PingAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<string>.Ok("pong"));
        public Task<OperationResult<System.Text.Json.JsonElement?>> GetRuntimeInfoAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Ok(null));
        public Task<OperationResult<bool>> ArcpyExistsAsync(string path, CancellationToken ct = default)
            => Task.FromResult(OperationResult<bool>.Ok(true));
        public Task<OperationResult<System.Text.Json.JsonElement?>> DescribeAsync(string path, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Ok(null));
        public Task<OperationResult<System.Text.Json.JsonElement?>> DatasetSummaryAsync(string path, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Ok(null));
        public Task<OperationResult<System.Text.Json.JsonElement?>> ListFieldsAsync(string datasetPath, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Ok(null));
        public Task<OperationResult<System.Text.Json.JsonElement?>> ListWorkspaceDatasetsAsync(string workspacePath, bool recursive = false, int maxDepth = 3, int maxItems = 500, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Ok(null));
        public Task<OperationResult<System.Text.Json.JsonElement?>> DatasetInfoAsync(string path, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Ok(null));
        public Task<OperationResult<System.Text.Json.JsonElement?>> RasterInfoAsync(string datasetPath, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Ok(null));
        public Task<OperationResult<System.Text.Json.JsonElement?>> SelectByAttributeAsync(string? mapName, string layerName, string mode, IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Ok(null));
        public Task<OperationResult<System.Text.Json.JsonElement?>> SelectByLocationAsync(string? mapName, string layerName, string? selectingLayerName, string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Ok(null));
    }
}
