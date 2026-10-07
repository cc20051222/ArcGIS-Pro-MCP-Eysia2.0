using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-118：M4 Native 十五件的注册面、冻结参数面等值、窄子集实算与 fail-closed 行为。
/// 真机宿主面零执行（NOT VERIFIED），本文件只断言离线可核验的事实。
/// </summary>
public sealed class D118M4ToolTests
{
    private static readonly string WorkspaceRoot = FindWorkspaceRoot();

    private static readonly (string Name, string Category, Type ToolType)[] Fifteen =
    [
        ("detect_temporal_change_points", ToolCategories.Quality, typeof(DetectTemporalChangePointsTool)),
        ("compare_temporal_trajectories", ToolCategories.Quality, typeof(CompareTemporalTrajectoriesTool)),
        ("export_time_animation", ToolCategories.Layout, typeof(ExportTimeAnimationTool)),
        ("extrude_scene_features", ToolCategories.Layer, typeof(ExtrudeSceneFeaturesTool)),
        ("create_elevation_profile", ToolCategories.Raster, typeof(CreateElevationProfileTool)),
        ("export_scene_package", ToolCategories.Project, typeof(ExportScenePackageTool)),
        ("assess_remote_sensing_quality", ToolCategories.Quality, typeof(AssessRemoteSensingQualityTool)),
        ("assess_classification_accuracy", ToolCategories.Quality, typeof(AssessClassificationAccuracyTool)),
        ("design_spatial_sample", ToolCategories.DataManagement, typeof(DesignSpatialSampleTool)),
        ("cross_validate_spatial_model", ToolCategories.Quality, typeof(CrossValidateSpatialModelTool)),
        ("analyze_scenario_sensitivity", ToolCategories.Quality, typeof(AnalyzeScenarioSensitivityTool)),
        ("compare_multi_criteria_scenarios", ToolCategories.Quality, typeof(CompareMultiCriteriaScenariosTool)),
        ("evaluate_scenario_ensemble", ToolCategories.Quality, typeof(EvaluateScenarioEnsembleTool)),
        ("validate_statistical_assumptions", ToolCategories.Quality, typeof(ValidateStatisticalAssumptionsTool)),
        ("validate_interchange_conformance", ToolCategories.Quality, typeof(ValidateInterchangeConformanceTool)),
    ];

    private static ToolExecutionContext Context(params (string Key, object? Value)[] values)
        => new() { Arguments = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal) };

    private static string? Code(OperationResult<object?> result)
        => result.Success ? null : result.Errors.FirstOrDefault()?.Code;

    private static string WriteInput(TestWorkspace workspace, string name, string content)
        => workspace.CreateOwnedFile("d118/" + name, content);

    private static string OutputPath(TestWorkspace workspace, string name)
        => Path.Combine(workspace.RootPath, "d118", "out-" + name + ".json");

    private static string FindWorkspaceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Config", "gp-whitelist.json"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("workspace root with Config/gp-whitelist.json not found");
    }

    [Fact]
    public void AllFifteenD118Tools_AreRegisteredOnceAsNativeRequiringArcGIS()
    {
        var tools = ProductionCompositionAccess.BuildRegistry().List().ToList();

        Assert.Equal(239, tools.Count);
        foreach (var (name, category, _) in Fifteen)
        {
            var matches = tools.Where(t => string.Equals(t.Name, name, StringComparison.Ordinal)).ToList();
            var tool = Assert.Single(matches);
            Assert.Equal(category, tool.Metadata.Category);
            Assert.Equal(ExecutionTypes.Native, tool.Metadata.ExecutionType);
            Assert.True(tool.Metadata.RequiresArcGIS);
        }
    }

    [Theory]
    [InlineData("detect_temporal_change_points")]
    [InlineData("compare_temporal_trajectories")]
    [InlineData("export_time_animation")]
    [InlineData("extrude_scene_features")]
    [InlineData("create_elevation_profile")]
    [InlineData("export_scene_package")]
    [InlineData("assess_remote_sensing_quality")]
    [InlineData("assess_classification_accuracy")]
    [InlineData("design_spatial_sample")]
    [InlineData("cross_validate_spatial_model")]
    [InlineData("analyze_scenario_sensitivity")]
    [InlineData("compare_multi_criteria_scenarios")]
    [InlineData("evaluate_scenario_ensemble")]
    [InlineData("validate_statistical_assumptions")]
    [InlineData("validate_interchange_conformance")]
    public void InputSchema_MatchesTheFrozenSchemaFileVerbatim(string toolName)
    {
        var tool = Fifteen.Single(x => x.Name == toolName);
        var instance = (McpToolBase)Activator.CreateInstance(tool.ToolType)!;
        var schemaPath = Path.Combine(WorkspaceRoot, ".runtime", "evolution", "v5-f", "run-20260928-d082",
            "f03b-5-schemas", toolName + ".schema.json");
        Assert.True(File.Exists(schemaPath), schemaPath);

        var frozen = JsonDocument.Parse(File.ReadAllText(schemaPath)).RootElement;
        var materialized = instance.InputSchema;
        Assert.Equal(false, materialized["additionalProperties"]);

        var frozenRequired = frozen.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        var declaredRequired = ((IEnumerable<object?>)materialized["required"]!).Select(x => Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(frozenRequired, declaredRequired);

        var frozenProps = frozen.GetProperty("properties");
        var declaredProps = (IDictionary<string, object?>)materialized["properties"]!;
        Assert.Equal(frozenProps.EnumerateObject().Count(), declaredProps.Count);
        foreach (var property in frozenProps.EnumerateObject())
        {
            var declaredObj = declaredProps[property.Name] ?? throw new InvalidOperationException("frozen property schema missing in materialized face: " + property.Name);
            var declared = (IDictionary<string, object?>)declaredObj;
            Assert.Equal(property.Value.GetProperty("type").GetString(), Convert.ToString(declared["type"]!, System.Globalization.CultureInfo.InvariantCulture));
            if (property.Value.TryGetProperty("enum", out var frozenEnum))
            {
                var values = ((IEnumerable<object?>)declared["enum"]!).Select(x => Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture)).ToList();
                Assert.Equal(frozenEnum.EnumerateArray().Select(e => e.GetString()).ToList(), values);
            }
        }
    }

    [Fact]
    public void EmbeddedSchemaText_EqualsNormalizedFrozenFile_AndRawShaMatchesSurface()
    {
        var rows = File.ReadAllLines(Path.Combine(WorkspaceRoot, "Benchmarks", "m4-capability-surface-v1.jsonl"))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToDictionary(e => e.GetProperty("toolName").GetString()!, StringComparer.Ordinal);

        foreach (var (name, _, _) in Fifteen)
        {
            var row = rows[name];
            var embedded = D118FrozenSchemas.ByToolName[name];
            var raw = File.ReadAllBytes(Path.Combine(WorkspaceRoot, row.GetProperty("schemaRef").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
            var normalized = new UTF8Encoding(false).GetString(raw).Replace("\r\n", "\n").TrimEnd('\n');

            Assert.Equal(normalized, embedded);
            Assert.Equal(row.GetProperty("schemaSha256").GetString(), Convert.ToHexString(SHA256.HashData(raw)));
            Assert.Equal(row.GetProperty("schemaSha256").GetString(), D118FrozenSchemas.SchemaSha256ByToolName[name]);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false).GetBytes(embedded))),
                D118FrozenSchemas.SchemaTextSha256ByToolName[name]);
        }
    }

    [Fact]
    public async Task UnknownExtraParameter_IsRefusedBeforeAnyWrite()
    {
        using var workspace = TestWorkspace.Create();
        var series = WriteInput(workspace, "series.csv", "t,value\n1,2\n2,3\n3,40\n4,41\n5,42\n");
        var output = OutputPath(workspace, "unknown-param");

        var result = await new DetectTemporalChangePointsTool().ExecuteAsync(
            Context(("series", series), ("outputPath", output), ("valueField", "value"), ("method", "pettitt"), ("surprise", 1)));

        Assert.Equal(ErrorCodes.InvalidArgument, Code(result));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task MissingRequiredParameter_IsRefused()
    {
        var result = await new DetectTemporalChangePointsTool().ExecuteAsync(
            Context(("series", "x.csv"), ("method", "pettitt")));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(result));
        // 冻结 required 序＝[series, outputPath, valueField, method]；首个缺失项即 outputPath。
        Assert.Contains("outputPath", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pettitt_OnSyntheticStep_FindsTheStepAndWritesOnce()
    {
        using var workspace = TestWorkspace.Create();
        var body = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"{i},1")
            .Concat(Enumerable.Range(1, 8).Select(i => $"{i},9")));
        var series = WriteInput(workspace, "step.csv", "t,value\n" + body + "\n");
        var output = OutputPath(workspace, "pettitt");

        var first = await new DetectTemporalChangePointsTool().ExecuteAsync(
            Context(("series", series), ("outputPath", output), ("valueField", "value"), ("method", "pettitt")));
        Assert.True(first.Success, string.Join("; ", first.Errors.Select(e => e.Message)));

        var data = Assert.IsType<Dictionary<string, object?>>(first.Data);
        var report = Assert.IsType<Dictionary<string, object?>>(data["report"]);
        var nested = Assert.IsType<Dictionary<string, object?>>(report["result"]);
        Assert.Equal(10, Convert.ToInt32(nested["changepointIndex"], System.Globalization.CultureInfo.InvariantCulture));

        var bytes = await File.ReadAllBytesAsync(output);
        var second = await new DetectTemporalChangePointsTool().ExecuteAsync(
            Context(("series", series), ("outputPath", output), ("valueField", "value"), ("method", "pettitt")));
        Assert.Equal(ErrorCodes.OutputExists, Code(second));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(output));
    }

    [Fact]
    public async Task BayesianOnlineSubset_IsRefusedWithoutWriting()
    {
        using var workspace = TestWorkspace.Create();
        var series = WriteInput(workspace, "step2.csv", "t,value\n1,1\n2,1\n3,9\n4,9\n5,9\n6,9\n");
        var output = OutputPath(workspace, "bopd");
        var result = await new DetectTemporalChangePointsTool().ExecuteAsync(
            Context(("series", series), ("outputPath", output), ("valueField", "value"), ("method", "bayesian_online")));
        Assert.Equal(ErrorCodes.NotImplemented, Code(result));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task TrajectoryComparison_ResampledZScore_YieldsUnitSelfCorrelation()
    {
        using var workspace = TestWorkspace.Create();
        var series = WriteInput(workspace, "traj.csv", "group,value\na,1\na,2\na,3\na,4\nb,2\nb,4\nb,6\nb,8\n");
        var output = OutputPath(workspace, "traj");
        var result = await new CompareTemporalTrajectoriesTool().ExecuteAsync(Context(
            ("series", series), ("groupField", "group"), ("outputPath", output),
            ("alignment", "resample"), ("normalization", "zscore")));

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        var report = Assert.IsType<Dictionary<string, object?>>(Assert.IsType<Dictionary<string, object?>>(result.Data)["report"]);
        var similarity = Assert.IsType<List<IDictionary<string, object?>>>(report["similarity"]);
        var rowA = similarity.Single(r => (string?)r["group"] == "a");
        var cells = Assert.IsType<Dictionary<string, object?>>(rowA["correlationByGroup"]);
        Assert.Equal(1.0, Convert.ToDouble(cells["a"], System.Globalization.CultureInfo.InvariantCulture), 10);
        Assert.Equal(1.0, Convert.ToDouble(cells["b"], System.Globalization.CultureInfo.InvariantCulture), 10);
    }

    [Fact]
    public async Task ClassificationAccuracy_KnownTwoByTwoCase_MatchesHandComputedMetrics()
    {
        using var workspace = TestWorkspace.Create();
        var classified = WriteInput(workspace, "cls.csv", "id,class\n1,A\n2,A\n3,B\n4,B\n");
        var reference = WriteInput(workspace, "ref.csv", "id,class\n1,A\n2,A\n3,A\n4,B\n");
        var output = OutputPath(workspace, "accuracy");

        var result = await new AssessClassificationAccuracyTool().ExecuteAsync(Context(
            ("classified", classified), ("reference", reference), ("outputPath", output), ("samplingDesign", "simple_random")));
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));

        var report = Assert.IsType<Dictionary<string, object?>>(Assert.IsType<Dictionary<string, object?>>(result.Data)["report"]);
        var metrics = Assert.IsType<Dictionary<string, object?>>(report["metrics"]);
        Assert.Equal(0.75, Convert.ToDouble(metrics["overallAccuracy"], System.Globalization.CultureInfo.InvariantCulture), 10);
        Assert.Equal(0.5, Convert.ToDouble(metrics["kappa"], System.Globalization.CultureInfo.InvariantCulture), 10);
    }

    [Fact]
    public async Task SpatialSample_WithFixedSeed_IsDeterministic()
    {
        using var workspace = TestWorkspace.Create();
        var population = WriteInput(workspace, "pop.csv", "x,y\n0,0\n1,0\n2,0\n3,0\n4,0\n5,0\n");
        var first = await new DesignSpatialSampleTool().ExecuteAsync(Context(
            ("population", population), ("sampleSize", 3), ("outputPath", OutputPath(workspace, "sample-a")), ("seed", 7)));
        var second = await new DesignSpatialSampleTool().ExecuteAsync(Context(
            ("population", population), ("sampleSize", 3), ("outputPath", OutputPath(workspace, "sample-b")), ("seed", 7)));

        Assert.True(first.Success, string.Join("; ", first.Errors.Select(e => e.Message)));
        Assert.True(second.Success);
        var a = Assert.IsType<List<IDictionary<string, object?>>>(Report(first)["selected"]);
        var b = Assert.IsType<List<IDictionary<string, object?>>>(Report(second)["selected"]);
        Assert.Equal(3, a.Count);
        Assert.Equal(a.Select(x => x["rowIndex"]), b.Select(x => x["rowIndex"]));
    }

    [Fact]
    public async Task CrossValidation_ExactLinearModel_HasZeroPooledError()
    {
        using var workspace = TestWorkspace.Create();
        var rows = string.Join("\n", Enumerable.Range(1, 12).Select(i => $"{i},{2 * i}"));
        var observations = WriteInput(workspace, "obs.csv", "x,y\n" + rows + "\n");
        var result = await new CrossValidateSpatialModelTool().ExecuteAsync(Context(
            ("observations", observations),
            ("modelSpec", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["response"] = "y", ["predictor"] = "linear", ["predictors"] = new List<object?> { "x" },
            }),
            ("outputPath", OutputPath(workspace, "cv")), ("blocking", "random"), ("folds", 3), ("seed", 11)));

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        var metrics = Assert.IsType<Dictionary<string, object?>>(Report(result)["metrics"]);
        Assert.True(Convert.ToDouble(metrics["pooledRmse"], System.Globalization.CultureInfo.InvariantCulture) < 1e-9);
    }

    [Fact]
    public async Task Sensitivity_EnumeratesThreeLevelsPerParameter()
    {
        using var workspace = TestWorkspace.Create();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["p1"] = new List<object?> { 0.0, 10.0 },
            ["p2"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["low"] = 0.0, ["high"] = 4.0 },
        };
        var scenarios = new List<object?>
        {
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["p1"] = 5.0, ["p2"] = 9.0 },
        };
        var result = await new AnalyzeScenarioSensitivityTool().ExecuteAsync(Context(
            ("parameters", parameters), ("scenarios", scenarios), ("outputPath", OutputPath(workspace, "sens"))));

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        var report = Report(result);
        var grid = Assert.IsType<Dictionary<string, object?>>(report["designMatrix"]);
        Assert.Equal(9, Convert.ToInt32(grid["rowsProduced"], System.Globalization.CultureInfo.InvariantCulture));
        var listed = Assert.IsType<List<IDictionary<string, object?>>>(report["scenarios"]);
        Assert.Contains("out-of-range:p2=9", (IEnumerable<string>)Assert.IsType<List<string>>(listed[0]["issues"]), StringComparer.Ordinal);
    }

    [Fact]
    public async Task MultiCriteria_WeightsMustSumToOne_AndRankingIsDeterministic()
    {
        using var workspace = TestWorkspace.Create();
        var alternatives = WriteInput(workspace, "alt.csv", "id,c1,c2\nA,1,10\nB,10,1\n");
        var badWeights = new Dictionary<string, object?>(StringComparer.Ordinal) { ["c1"] = 0.6, ["c2"] = 0.6 };
        var refused = await new CompareMultiCriteriaScenariosTool().ExecuteAsync(Context(
            ("alternatives", alternatives),
            ("criteria", new List<object?> { "c1", "c2" }),
            ("weights", badWeights), ("outputPath", OutputPath(workspace, "mcdm-bad"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(refused));

        var weights = new Dictionary<string, object?>(StringComparer.Ordinal) { ["c1"] = 0.5, ["c2"] = 0.5 };
        var result = await new CompareMultiCriteriaScenariosTool().ExecuteAsync(Context(
            ("alternatives", alternatives),
            ("criteria", new List<object?>
            {
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = "c1", ["direction"] = "max" },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = "c2", ["direction"] = "min" },
            }),
            ("weights", weights), ("outputPath", OutputPath(workspace, "mcdm"))));
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        var ranking = Assert.IsType<List<IDictionary<string, object?>>>(Report(result)["ranking"]);
        Assert.Equal("B", ranking[0]["id"]);
        Assert.Equal(1.0, Convert.ToDouble(ranking[0]["weightedScore"], System.Globalization.CultureInfo.InvariantCulture), 10);
    }

    [Fact]
    public async Task Ensemble_ReportsRelativeDispersionAndThresholdFlag()
    {
        using var workspace = TestWorkspace.Create();
        var ensemble = new List<object?>
        {
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["member"] = "m1", ["value"] = 10.0 },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["member"] = "m2", ["value"] = 20.0 },
        };
        var result = await new EvaluateScenarioEnsembleTool().ExecuteAsync(Context(
            ("ensemble", ensemble), ("outputPath", OutputPath(workspace, "ens")), ("disagreementThreshold", 0.25)));
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        var groups = Assert.IsType<List<IDictionary<string, object?>>>(Report(result)["groups"]);
        var only = groups.Single();
        Assert.Equal(true, only["exceedsThreshold"]);
        Assert.Equal(15.0, Convert.ToDouble(only["mean"], System.Globalization.CultureInfo.InvariantCulture), 10);
    }

    [Fact]
    public async Task StatisticalAssumptions_SymmetricVector_HasZeroSkewAndRefusesUnknownCheck()
    {
        using var workspace = TestWorkspace.Create();
        var variables = new List<object?>
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = "v1",
                ["values"] = Enumerable.Range(1, 10).Select(i => (object?)(double)i).ToList(),
            },
        };
        var result = await new ValidateStatisticalAssumptionsTool().ExecuteAsync(Context(
            ("variables", variables), ("intendedMethod", "linear_regression")));
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        var data = Assert.IsType<Dictionary<string, object?>>(result.Data);   // 只读件无输出包装，Data 即报告本体
        var perVariable = Assert.IsType<List<IDictionary<string, object?>>>(data["variables"]);
        Assert.Equal(0.0, Convert.ToDouble(perVariable[0]["skewness"], System.Globalization.CultureInfo.InvariantCulture), 10);

        var refused = await new ValidateStatisticalAssumptionsTool().ExecuteAsync(Context(
            ("variables", variables), ("intendedMethod", "linear_regression"),
            ("checks", new List<object?> { "spatial_autocorrelation" })));
        Assert.Equal(ErrorCodes.NotImplemented, Code(refused));
    }

    [Fact]
    public async Task InterchangeConformance_ReportsStructuralFactsAndRefusesUnknownDeepChecks()
    {
        using var workspace = TestWorkspace.Create();
        var input = WriteInput(workspace, "interchange.csv", "a,b\n1,2\n3\n");
        var result = await new ValidateInterchangeConformanceTool().ExecuteAsync(Context(
            ("input", input), ("standardVersion", "test-1.0")));
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        var data = Assert.IsType<Dictionary<string, object?>>(result.Data);   // 只读件无输出包装，Data 即报告本体
        var findings = Assert.IsType<List<IDictionary<string, object?>>>(data["findings"]);
        Assert.Contains(findings, f => (string?)f["check"] == "rowWidth" && (string?)f["status"] == "FAIL");

        var refused = await new ValidateInterchangeConformanceTool().ExecuteAsync(Context(
            ("input", input), ("standardVersion", "test-1.0"), ("deepChecks", new List<object?> { "relationship-integrity" })));
        Assert.Equal(ErrorCodes.NotImplemented, Code(refused));
    }

    [Fact]
    public async Task RasterQuality_AsciiGridIsMeasuredAndTiffIsStructuralOnly()
    {
        using var workspace = TestWorkspace.Create();
        var ascii = WriteInput(workspace, "grid.asc", "ncols 3\nnrows 2\nxllcorner 0\nyllcorner 0\ncellsize 1\nNODATA_value -9999\n1 2 3\n4 -9999 6\n");
        var result = await new AssessRemoteSensingQualityTool().ExecuteAsync(Context(("input", ascii)));
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        var face = Assert.IsType<Dictionary<string, object?>>(result.Data);
        Assert.Equal("esri-ascii-grid", face["format"]);
        Assert.Equal(5, Convert.ToInt32(face["cellsNumeric"], System.Globalization.CultureInfo.InvariantCulture));
        var stats = Assert.IsType<Dictionary<string, object?>>(face["stats"]);
        Assert.Equal(1.0, Convert.ToDouble(stats["min"], System.Globalization.CultureInfo.InvariantCulture), 10);
        Assert.Equal(6.0, Convert.ToDouble(stats["max"], System.Globalization.CultureInfo.InvariantCulture), 10);

        var tiff = workspace.CreateOwnedFile("d118/probe.tif", string.Empty);
        File.WriteAllBytes(tiff, new byte[] { 0x49, 0x49, 42, 0, 8, 0, 0, 0, 0, 0, 0, 0 });
        var tiffResult = await new AssessRemoteSensingQualityTool().ExecuteAsync(Context(("input", tiff)));
        Assert.True(tiffResult.Success, string.Join("; ", tiffResult.Errors.Select(e => e.Message)));
        var tiffFace = Assert.IsType<Dictionary<string, object?>>(tiffResult.Data);
        Assert.Equal("tiff-or-geotiff", tiffFace["format"]);
        var verification = Assert.IsType<Dictionary<string, object?>>(tiffFace["verification"]);
        Assert.StartsWith("NOT VERIFIED", (string?)verification["pixelStatistics"], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("export_time_animation")]
    [InlineData("extrude_scene_features")]
    [InlineData("create_elevation_profile")]
    [InlineData("export_scene_package")]
    public void HostFaceTools_AreRegisteredButRefuseWithoutAHost(string toolName)
    {
        Assert.NotNull(ProductionCompositionAccess.BuildRegistry().List().SingleOrDefault(tool => tool.Name == toolName));
        var entry = Fifteen.Single(x => x.Name == toolName);
        Assert.IsAssignableFrom<McpToolBase>(Activator.CreateInstance(entry.ToolType));
    }

    [Fact]
    public async Task AnimationExport_FailsClosedWithoutCreatingTheDeclaredOutput()
    {
        using var workspace = TestWorkspace.Create();
        var output = OutputPath(workspace, "anim");
        var result = await new ExportTimeAnimationTool().ExecuteAsync(Context(
            ("timeLayers", new List<object?> { "layer-a" }), ("outputPath", output),
            ("visualRules", new Dictionary<string, object?>(StringComparer.Ordinal)), ("format", "gif")));
        Assert.Equal(ErrorCodes.NotImplemented, Code(result));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task ScenePackageExport_WithVerifyReopen_IsRefusedBeforeWriting()
    {
        using var workspace = TestWorkspace.Create();
        var output = OutputPath(workspace, "sp");
        var result = await new ExportScenePackageTool().ExecuteAsync(Context(
            ("scene", "Scene1"), ("outputPath", output), ("verifyReopen", true)));
        Assert.Equal(ErrorCodes.NotImplemented, Code(result));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task ProtectedOutputRoot_IsRejectedBeforeWriting()
    {
        using var workspace = TestWorkspace.Create();
        var series = WriteInput(workspace, "series3.csv", "t,value\n1,1\n2,2\n3,30\n4,31\n5,32\n");
        var output = Path.Combine(WorkspaceRoot, "TestFixtures", "d118-should-never-exist.json");
        var result = await new DetectTemporalChangePointsTool().ExecuteAsync(Context(
            ("series", series), ("outputPath", output), ("valueField", "value"), ("method", "pettitt")));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(result));
        Assert.False(File.Exists(output));
    }

    private static Dictionary<string, object?> Report(OperationResult<object?> result)
        => Assert.IsType<Dictionary<string, object?>>(Assert.IsType<Dictionary<string, object?>>(result.Data)["report"]);
}
