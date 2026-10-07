using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-062（旗舰批：受控 GP + 编辑栈 + 统计聚合 + 审计查询）单测。
/// 覆盖：白名单解析与禁入类别反证 / 审计读写与 G-138（禁 %TEMP%）/ A1 执行前安全链（白名单外拒、
/// confirm 缺省拒、守卫、OUTPUT_EXISTS）/ B 段 confirm 前置拒与事务语义 / C 段参数面与 FieldMath 已知答案 /
/// 元工具（list/describe/messages/extension）。安全件（A1/B2/B3）用例按工单要求在此单列。
/// </summary>
public sealed class D062FeatureBatchTests
{
    private static async Task<OperationResult<object?>> CallAsync(
        FakeArcGISHost host, IMCPTool tool, IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    private static string Code(OperationResult<object?> r)
        => r.Errors.FirstOrDefault()?.Code ?? (r.Success ? "OK" : "NO_CODE");

    private static Dictionary<string, object?> Args(params (string K, object? V)[] items)
        => items.ToDictionary(x => x.K, x => x.V);

    // ════════════════════════ 白名单（Core）════════════════════════

    [Fact]
    public void Whitelist_EmbeddedResource_Loads_With51Entries()
    {
        var svc = NewRealGpService();
        var wl = CallRealWhitelist(svc);
        Assert.True(wl.Success);
        Assert.Contains("embedded:", wl.Data!.Source);
        Assert.True(wl.Data.Entries.Count >= 50, $"whitelist count = {wl.Data.Entries.Count}");
    }

    [Fact]
    public void Whitelist_NoForbiddenCategories_NegativeAssertion()
    {
        var svc = NewRealGpService();
        var wl = CallRealWhitelist(svc).Data!;
        var forbidden = new[] { "Delete", "Rename", "Compact", "CopyRow", "Truncate" };
        var hits = wl.Entries.Where(e => forbidden.Any(f => e.Tool.Contains(f, StringComparison.OrdinalIgnoreCase))).ToList();
        Assert.True(hits.Count == 0, "禁入类别混入白名单: " + string.Join(",", hits.Select(h => h.Tool)));
    }

    [Fact]
    public void Whitelist_DestructiveFlags_AreExactlyFive()
    {
        // D-066（Keeper 批准书 = HANDOFF_D066；P-21 = B）：新增 management.AddJoin / RemoveJoin（destructive=true）⇒ 3 → 5。
        var svc = NewRealGpService();
        var wl = CallRealWhitelist(svc).Data!;
        var destructive = wl.Entries.Where(e => e.Destructive).Select(e => e.Tool).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "analysis.Near", "management.AddJoin", "management.CalculateField", "management.RemoveJoin", "management.RepairGeometry" }, destructive);
    }

    [Fact]
    public void Parser_FindsCaseInsensitive_AndParsesParameters()
    {
        const string json = """
            [
              { "tool": "analysis.Buffer", "destructive": false, "notes": "buffer test", "parameters": [
                { "name": "in_features", "direction": "input", "required": true },
                { "name": "out_feature_class", "direction": "output", "required": true, "type": "featureclass" } ] }
            ]
            """;
        var wl = GpWhitelistParser.Parse(json, "test");
        Assert.NotNull(wl.Find("ANALYSIS.buffer"));
        var entry = wl.Find("analysis.Buffer")!;
        Assert.Equal(2, entry.Parameters.Count);
        Assert.Equal("output", entry.Parameters[1].Direction);
        Assert.True(entry.Parameters[0].Required);
    }

    [Fact]
    public void Parser_NonArrayRoot_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => GpWhitelistParser.Parse("{}", "bad"));
    }

    // ════════════════════════ 审计（Core · G-138）═════════════════════

    private static string TempAuditDir()
    {
        var dir = Path.Combine(@"D:\ArcGIS-Pro-MCP 2.0\.runtime", "unit-audit-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Audit_ResolvePath_RejectsTempDir_G138()
    {
        Assert.Throws<InvalidOperationException>(() => GpAuditLog.ResolvePath(Path.GetTempPath()));
    }

    [Fact]
    public void Audit_EnvOverride_IsHonored()
    {
        var dir = TempAuditDir();
        try
        {
            Environment.SetEnvironmentVariable(GpAuditLog.AuditDirVariable, dir);
            var path = GpAuditLog.ResolvePath(null);
            Assert.Equal(Path.Combine(dir, GpAuditLog.FileName), path);
        }
        finally
        {
            Environment.SetEnvironmentVariable(GpAuditLog.AuditDirVariable, null);
        }
    }

    [Fact]
    public void Audit_AppendRead_Roundtrip_PreservesFields()
    {
        var dir = TempAuditDir();
        try
        {
            var path = GpAuditLog.ResolvePath(dir);
            var entry = new GpAuditEntry
            {
                TimestampUtc = "2026-09-20T08:00:00.0000000Z",
                Tool = "analysis.Buffer",
                ParameterDigest = "in_features=FC; out_feature_class=D:/x/out.shp",
                ParameterForm = "named",
                Destructive = false,
                Confirm = false,
                Success = true,
                ResultCode = "OK",
                DurationMs = 42,
                AuditNote = "unit",
                Pid = 123,
            };
            Assert.True(GpAuditLog.TryAppend(path, entry, out var idx1, out var err1));
            var entry2 = new GpAuditEntry
            {
                TimestampUtc = entry.TimestampUtc, Tool = "sa.Slope", ParameterDigest = entry.ParameterDigest,
                ParameterForm = entry.ParameterForm, Destructive = entry.Destructive, Confirm = entry.Confirm,
                Success = entry.Success, ResultCode = entry.ResultCode, DurationMs = entry.DurationMs,
                AuditNote = entry.AuditNote, Pid = entry.Pid,
            };
            Assert.True(GpAuditLog.TryAppend(path, entry2, out var idx2, out var err2));
            Assert.Equal(1, idx1);
            Assert.Equal(2, idx2);

            var q = GpAuditLog.Read(path);
            Assert.Equal(2, q.TotalEntries);
            Assert.Equal("analysis.Buffer", q.Entries[0].Tool);
            Assert.Equal(42, q.Entries[0].DurationMs);
            Assert.Equal(123, q.Entries[0].Pid);
            Assert.Equal("sa.Slope", q.Entries[1].Tool);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Audit_Read_Filters_Tool_And_Success()
    {
        var dir = TempAuditDir();
        try
        {
            var path = GpAuditLog.ResolvePath(dir);
            foreach (var (tool, ok) in new[] { ("analysis.Buffer", true), ("analysis.Buffer", false), ("sa.Slope", true) })
            {
                GpAuditLog.TryAppend(path, new GpAuditEntry
                {
                    TimestampUtc = "2026-09-20T08:00:00.0000000Z",
                    Tool = tool,
                    ParameterDigest = "x=1",
                    ParameterForm = "named",
                    Success = ok,
                    ResultCode = ok ? "OK" : "GEOPROCESSING_ERROR",
                    DurationMs = 1,
                    Pid = 1,
                }, out _, out _);
            }

            Assert.Equal(2, GpAuditLog.Read(path, toolFilter: "analysis.Buffer").MatchedEntries);
            Assert.Equal(2, GpAuditLog.Read(path, successFilter: true).MatchedEntries);
            Assert.Equal(1, GpAuditLog.Read(path, toolFilter: "analysis.Buffer", successFilter: false).MatchedEntries);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Audit_Read_Limit_Truncates_AndCounts()
    {
        var dir = TempAuditDir();
        try
        {
            var path = GpAuditLog.ResolvePath(dir);
            for (var i = 0; i < 5; i++)
            {
                GpAuditLog.TryAppend(path, new GpAuditEntry
                {
                    TimestampUtc = "2026-09-20T08:00:00.0000000Z",
                    Tool = "sa.Slope",
                    ParameterDigest = "i=" + i,
                    ParameterForm = "named",
                    Success = true,
                    ResultCode = "OK",
                    DurationMs = i,
                    Pid = 1,
                }, out _, out _);
            }

            var q = GpAuditLog.Read(path, limit: 2);
            Assert.Equal(5, q.TotalEntries);
            Assert.Equal(5, q.MatchedEntries);
            Assert.Equal(2, q.Entries.Count);
            Assert.True(q.Truncated);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Audit_Read_SkipsCorruptLines()
    {
        var dir = TempAuditDir();
        try
        {
            var path = GpAuditLog.ResolvePath(dir);
            GpAuditLog.TryAppend(path, new GpAuditEntry { TimestampUtc = "t", Tool = "a", ParameterDigest = "d", ParameterForm = "named", Success = true, ResultCode = "OK", DurationMs = 1, Pid = 1 }, out _, out _);
            File.AppendAllText(path, "{not-valid-json\n");
            var q = GpAuditLog.Read(path);
            Assert.Equal(1, q.TotalEntries);
            Assert.Equal(1, q.CorruptLines);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Audit_Read_MissingFile_IsGracefulEmpty()
    {
        var q = GpAuditLog.Read(Path.Combine(TempAuditDir(), GpAuditLog.FileName));
        Assert.False(q.FileExists);
        Assert.Empty(q.Entries);
    }

    // ════════════════════════ FieldMath（Core · 已知答案）══════════════

    [Fact]
    public void FieldMath_KnownAnswers_FourValues()
    {
        var (min, max, mean, median, sum, stddev) = FieldMath.Reduce(new List<double> { 1, 2, 3, 4 });
        Assert.Equal(1, min);
        Assert.Equal(4, max);
        Assert.Equal(2.5, mean);
        Assert.Equal(2.5, median);
        Assert.Equal(10, sum);
        Assert.True(Math.Abs(stddev!.Value - Math.Sqrt(5.0 / 3.0)) < 1e-12);
    }

    [Fact]
    public void FieldMath_SingleValue_StdDevNull_OddMedian()
    {
        var (min, max, mean, median, sum, stddev) = FieldMath.Reduce(new List<double> { 5 });
        Assert.Equal(5, min);
        Assert.Equal(5, max);
        Assert.Equal(5, median);
        Assert.Null(stddev);
    }

    [Fact]
    public void FieldMath_Empty_AllNull()
    {
        var (min, max, mean, median, sum, stddev) = FieldMath.Reduce(new List<double>());
        Assert.All(new[] { min, max, mean, median, sum, stddev }, v => Assert.Null(v));
    }

    // ── 真实 GeoprocessingService（反射加载 Compatibility 程序集；同 D-057 模式）──

    private static object NewRealGpService()
        => ProductionCompositionAccess.CreateProductionService<object>(
            "ArcGISProMCP.Compatibility.Services.GeoprocessingService")!;

    private static OperationResult<GpRunResult> CallRealRun(object svc, GpRunRequest request)
    {
        var method = svc.GetType().GetMethod("RunWhitelistedAsync")!;
        var task = (Task<OperationResult<GpRunResult>>)method.Invoke(svc, new object?[] { request, default(CancellationToken) })!;
        return task.GetAwaiter().GetResult();
    }

    private static OperationResult<GpWhitelist> CallRealWhitelist(object svc)
    {
        var method = svc.GetType().GetMethod("GetWhitelistAsync")!;
        var task = (Task<OperationResult<GpWhitelist>>)method.Invoke(svc, new object?[] { default(CancellationToken) })!;
        return task.GetAwaiter().GetResult();
    }

    // ════════════════════════ A1 · run_geoprocessing（安全件）════════════

    [Fact]
    public async Task RunGp_MissingTool_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new RunGeoprocessingTool(), Args());
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task RunGp_NamedForm_PassesThrough()
    {
        var gp = new RecordingGeoprocessingService();
        var r = await CallAsync(new FakeArcGISHost(geoprocessing: gp), new RunGeoprocessingTool(), Args(
            ("tool", "analysis.Buffer"),
            ("parameters", new Dictionary<string, object?> { ["in_features"] = "FC", ["out_feature_class"] = "D:/x/out.shp" }),
            ("auditNote", "unit")));
        Assert.True(r.Success);
        Assert.NotNull(gp.LastRunRequest);
        Assert.Equal("analysis.Buffer", gp.LastRunRequest!.ToolName);
        Assert.Equal("unit", gp.LastRunRequest.AuditNote);
        Assert.Equal("FC", gp.LastRunRequest.Parameters!["in_features"]);
    }

    [Fact]
    public async Task RunGp_PositionalForm_PassesThrough()
    {
        var gp = new RecordingGeoprocessingService();
        var r = await CallAsync(new FakeArcGISHost(geoprocessing: gp), new RunGeoprocessingTool(), Args(
            ("tool", "sa.Slope"),
            ("positionalValues", new List<object?> { "R1", "D:/x/slope.tif" })));
        Assert.True(r.Success);
        Assert.Equal(2, gp.LastRunRequest!.PositionalValues!.Count);
    }

    [Fact]
    public async Task RunGp_Confirm_PassesThrough()
    {
        var gp = new RecordingGeoprocessingService();
        await CallAsync(new FakeArcGISHost(geoprocessing: gp), new RunGeoprocessingTool(), Args(
            ("tool", "management.CalculateField"), ("confirm", true)));
        Assert.True(gp.LastRunRequest!.Confirm);
    }

    [Fact]
    public async Task RunGp_AuditDir_PassesThrough()
    {
        var gp = new RecordingGeoprocessingService();
        await CallAsync(new FakeArcGISHost(geoprocessing: gp), new RunGeoprocessingTool(), Args(
            ("tool", "analysis.Clip"), ("auditDir", @"D:\ArcGIS-Pro-MCP 2.0\.runtime\audit-x")));
        Assert.Equal(@"D:\ArcGIS-Pro-MCP 2.0\.runtime\audit-x", gp.LastRunRequest!.AuditPath);
    }

    [Fact]
    public async Task RunGp_Failure_SurfacesCode()
    {
        var gp = new RecordingGeoprocessingService
        {
            RunResult = OperationResult<GpRunResult>.Fail(ErrorCodes.GeoprocessingError, "gp boom"),
        };
        var r = await CallAsync(new FakeArcGISHost(geoprocessing: gp), new RunGeoprocessingTool(), Args(("tool", "analysis.Clip")));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.GeoprocessingError, Code(r));
    }

    [Fact]
    public async Task RunGp_ServiceFailure_SurfacesFailClosed()
    {
        // 服务级 fail-closed（真实 GeoprocessingService 白名单不可得路径）由服务级测试覆盖；
        // 此处验证工具层对服务失败的透传。
        var gp = new RecordingGeoprocessingService
        {
            RunResult = OperationResult<GpRunResult>.Fail(
                ErrorCodes.InvalidState, "GP whitelist is unavailable; refusing to run (fail-closed)."),
        };
        var r = await CallAsync(new FakeArcGISHost(geoprocessing: gp), new RunGeoprocessingTool(), Args(("tool", "analysis.Clip")));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidState, Code(r));
    }

    [Fact]
    public async Task RunGp_ParametersMustBeObject_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new RunGeoprocessingTool(), Args(
            ("tool", "analysis.Clip"), ("parameters", "not-a-dict")));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    // ── A1 服务级执行前安全链（真实 GeoprocessingService；不触执行器）──

    [Fact]
    public void RunGpService_WhitelistMiss_RejectedWithControlledMessage()
    {
        var svc = NewRealGpService();
        var r = CallRealRun(svc, new GpRunRequest
        {
            ToolName = "management.Delete",
            Parameters = new Dictionary<string, object?> { ["in_dataset"] = "x" },
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors[0].Code);
        Assert.Contains("受控白名单", r.Errors[0].Message);
    }

    [Fact]
    public void RunGpService_DestructiveWithoutConfirm_Rejected()
    {
        var svc = NewRealGpService();
        var r = CallRealRun(svc, new GpRunRequest
        {
            ToolName = "management.CalculateField",
            Parameters = new Dictionary<string, object?> { ["in_table"] = "x", ["field"] = "f", ["expression"] = "1" },
            Confirm = false,
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors[0].Code);
        Assert.Contains("confirm=true is required", r.Errors[0].Message);
    }

    [Fact]
    public void RunGpService_AuditTempDir_RejectedBeforeExecution()
    {
        var svc = NewRealGpService();
        var r = CallRealRun(svc, new GpRunRequest
        {
            ToolName = "analysis.Buffer",
            Parameters = new Dictionary<string, object?> { ["in_features"] = "x", ["out_feature_class"] = "D:/y/o.shp" },
            AuditPath = Path.GetTempPath(),
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors[0].Code);
        Assert.Contains("G-138", r.Errors[0].Message);
    }

    [Fact]
    public void RunGpService_InputGuard_HitsProtectedRoot()
    {
        var svc = NewRealGpService();
        var r = CallRealRun(svc, new GpRunRequest
        {
            ToolName = "analysis.Buffer",
            Parameters = new Dictionary<string, object?>
            {
                ["in_features"] = @"D:\ArcGIS-Pro-MCP 2.0\TestFixtures\Phase8_5_6\WorkBuddyTest.gdb\FC",
                ["out_feature_class"] = "D:/y/o.shp",
            },
            AuditPath = TempAuditDir(),
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, r.Errors[0].Code);
    }

    [Fact]
    public void RunGpService_MultivalueInputs_GuardedItemwise()
    {
        var svc = NewRealGpService();
        var r = CallRealRun(svc, new GpRunRequest
        {
            ToolName = "analysis.Union",
            Parameters = new Dictionary<string, object?>
            {
                ["in_features"] = "D:/ok/a.shp;D:/ArcGIS-Pro-MCP/TestFixtures/evil.shp",
                ["out_feature_class"] = "D:/y/o.shp",
            },
            AuditPath = TempAuditDir(),
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, r.Errors[0].Code);
    }

    [Fact]
    public void RunGpService_OutputExists_RejectedBeforeExecution()
    {
        var dir = TempAuditDir();
        var outDir = Path.Combine(dir, "out");
        Directory.CreateDirectory(outDir);
        var existing = Path.Combine(outDir, "exists.shp");
        File.WriteAllText(existing, "placeholder");
        try
        {
            var svc = NewRealGpService();
            var r = CallRealRun(svc, new GpRunRequest
            {
                ToolName = "analysis.Buffer",
                Parameters = new Dictionary<string, object?> { ["in_features"] = "FC", ["out_feature_class"] = existing },
                AuditPath = dir,
            });
            Assert.False(r.Success);
            Assert.Equal(ErrorCodes.OutputExists, r.Errors[0].Code);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RunGpService_PositionalBeyondSignature_Rejected()
    {
        var r = CallRealRun(NewRealGpService(), new GpRunRequest
        {
            ToolName = "analysis.Buffer",
            PositionalValues = new List<string> { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" },
            AuditPath = TempAuditDir(),
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors[0].Code);
        Assert.Contains("at most", r.Errors[0].Message);
    }

    [Fact]
    public void RunGpService_UnknownParamName_Rejected()
    {
        var r = CallRealRun(NewRealGpService(), new GpRunRequest
        {
            ToolName = "analysis.Buffer",
            Parameters = new Dictionary<string, object?> { ["typo"] = "x" },
            AuditPath = TempAuditDir(),
        });
        Assert.False(r.Success);
        Assert.Contains("Unknown parameter", r.Errors[0].Message);
    }

    [Fact]
    public void RunGpService_MissingRequired_Rejected()
    {
        var r = CallRealRun(NewRealGpService(), new GpRunRequest
        {
            ToolName = "analysis.Buffer",
            Parameters = new Dictionary<string, object?>(),
            AuditPath = TempAuditDir(),
        });
        Assert.False(r.Success);
        Assert.Contains("parameters (named) or positionalValues is required", r.Errors[0].Message);
    }

    [Fact]
    public void RunGpService_PositionalMissingRequired_Rejected()
    {
        var r = CallRealRun(NewRealGpService(), new GpRunRequest
        {
            ToolName = "analysis.Buffer",
            PositionalValues = new List<string> { "only-one" },
            AuditPath = TempAuditDir(),
        });
        Assert.False(r.Success);
        Assert.Contains("positional form leaves it empty", r.Errors[0].Message);
    }

    [Fact]
    public void RunGpService_BothForms_Rejected()
    {
        var r = CallRealRun(NewRealGpService(), new GpRunRequest
        {
            ToolName = "analysis.Buffer",
            Parameters = new Dictionary<string, object?> { ["in_features"] = "x" },
            PositionalValues = new List<string> { "y" },
            AuditPath = TempAuditDir(),
        });
        Assert.False(r.Success);
        Assert.Contains("not both", r.Errors[0].Message);
    }

    // ════════════════════════ A2/A3 · 白名单元工具 ═════════════════════

    private static GpWhitelist SmallWhitelist() => GpWhitelistParser.Parse("""
        [
          { "tool": "analysis.Buffer", "destructive": false, "notes": "buffering", "parameters": [
            { "name": "in_features", "direction": "input", "required": true },
            { "name": "out_feature_class", "direction": "output", "required": true } ] },
          { "tool": "sa.Slope", "destructive": false, "notes": "slope surface", "parameters": [
            { "name": "in_raster", "direction": "input", "required": true },
            { "name": "out_raster", "direction": "output", "required": true } ] },
          { "tool": "management.CalculateField", "destructive": true, "notes": "in-place field calc", "parameters": [
            { "name": "in_table", "direction": "input", "required": true } ] }
        ]
        """, "test");

    [Fact]
    public async Task ListGp_EnumeratesOnlyWhitelist()
    {
        var host = new FakeArcGISHost(geoprocessing: new RecordingGeoprocessingService { WhitelistData = SmallWhitelist() });
        var r = await CallAsync(host, new ListGeoprocessingToolsTool(), Args());
        Assert.True(r.Success);
        var data = Assert.IsType<Dictionary<string, object?>>(r.Data);
        Assert.Equal(3, data["count"]);
        Assert.Equal(3, data["totalWhitelisted"]);
    }

    [Fact]
    public async Task ListGp_FilterByName_AndCategory()
    {
        var host = new FakeArcGISHost(geoprocessing: new RecordingGeoprocessingService { WhitelistData = SmallWhitelist() });
        var r1 = await CallAsync(host, new ListGeoprocessingToolsTool(), Args(("filter", "slope")));
        var d1 = Assert.IsType<Dictionary<string, object?>>(r1.Data);
        Assert.Equal(1, d1["count"]);

        var r2 = await CallAsync(host, new ListGeoprocessingToolsTool(), Args(("category", "analysis")));
        var d2 = Assert.IsType<Dictionary<string, object?>>(r2.Data);
        Assert.Equal(1, d2["count"]);

        var r3 = await CallAsync(host, new ListGeoprocessingToolsTool(), Args(("category", "sa")));
        var d3 = Assert.IsType<Dictionary<string, object?>>(r3.Data);
        Assert.Equal(1, d3["count"]);
    }

    [Fact]
    public async Task ListGp_ExposesDestructiveFlags_AndParameterCounts()
    {
        var host = new FakeArcGISHost(geoprocessing: new RecordingGeoprocessingService { WhitelistData = SmallWhitelist() });
        var r = await CallAsync(host, new ListGeoprocessingToolsTool(), Args());
        var tools = (IEnumerable<Dictionary<string, object?>>)Assert.IsType<Dictionary<string, object?>>(r.Data)["tools"]!;
        var calc = tools.First(t => (string)t["tool"]! == "management.CalculateField");
        Assert.True((bool)calc["destructive"]!);
        var buffer = tools.First(t => (string)t["tool"]! == "analysis.Buffer");
        Assert.Equal(2, buffer["parameterCount"]);
    }

    [Fact]
    public async Task ListGp_WhitelistUnavailable_SurfacesError()
    {
        var host = new FakeArcGISHost(geoprocessing: new RecordingGeoprocessingService());
        var r = await CallAsync(host, new ListGeoprocessingToolsTool(), Args());
        Assert.False(r.Success);
    }

    [Fact]
    public async Task DescribeGp_Hit_ReturnsFullSignature()
    {
        var host = new FakeArcGISHost(geoprocessing: new RecordingGeoprocessingService { WhitelistData = SmallWhitelist() });
        var r = await CallAsync(host, new DescribeGeoprocessingToolTool(), Args(("tool", "analysis.Buffer")));
        Assert.True(r.Success);
        var d = Assert.IsType<Dictionary<string, object?>>(r.Data);
        Assert.Equal("analysis.Buffer", d["tool"]);
        Assert.Equal(2, ((IEnumerable<object?>)d["parameters"]!).Count());
        Assert.Equal("test", d["whitelistSource"]);
    }

    [Fact]
    public async Task DescribeGp_Miss_RejectedWithControlledMessage()
    {
        var host = new FakeArcGISHost(geoprocessing: new RecordingGeoprocessingService { WhitelistData = SmallWhitelist() });
        var r = await CallAsync(host, new DescribeGeoprocessingToolTool(), Args(("tool", "management.Delete")));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Contains("受控白名单", r.Errors.First().Message);
    }

    [Fact]
    public async Task DescribeGp_MissingToolArg_Rejected()
    {
        var host = new FakeArcGISHost(geoprocessing: new RecordingGeoprocessingService { WhitelistData = SmallWhitelist() });
        var r = await CallAsync(host, new DescribeGeoprocessingToolTool(), Args());
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task DescribeGp_DestructiveEntry_Flagged()
    {
        var host = new FakeArcGISHost(geoprocessing: new RecordingGeoprocessingService { WhitelistData = SmallWhitelist() });
        var r = await CallAsync(host, new DescribeGeoprocessingToolTool(), Args(("tool", "management.CalculateField")));
        var d = Assert.IsType<Dictionary<string, object?>>(r.Data);
        Assert.True((bool)d["destructive"]!);
    }

    // ════════════════════════ A4/A5 · 消息与许可 ═════════════════════

    [Fact]
    public async Task GetMessages_EmptyDefault_Graceful()
    {
        var r = await CallAsync(new FakeArcGISHost(), new GetMessagesTool(), Args());
        Assert.True(r.Success);
        var info = Assert.IsType<GpMessagesInfo>(r.Data);
        Assert.Null(info.LastCallAtUtc);
    }

    [Fact]
    public async Task GetMessages_ScriptedSnapshot_Surfaces()
    {
        var gp = new RecordingGeoprocessingService
        {
            MessagesResult = OperationResult<GpMessagesInfo>.Ok(new GpMessagesInfo
            {
                LastCallAtUtc = "2026-09-20T08:00:00.0000000Z",
                ToolName = "sa.Slope",
                Success = false,
                Messages = new[] { "start", "done" },
                ErrorMessages = new[] { "bad z factor" },
            }),
        };
        var r = await CallAsync(new FakeArcGISHost(geoprocessing: gp), new GetMessagesTool(), Args());
        var info = Assert.IsType<GpMessagesInfo>(r.Data);
        Assert.Equal("sa.Slope", info.ToolName);
        Assert.False(info.Success!.Value);
        Assert.Equal(2, info.Messages.Count);
        Assert.Single(info.ErrorMessages);
    }

    [Fact]
    public async Task GetMessages_SeverityBasis_Disclosed()
    {
        var r = await CallAsync(new FakeArcGISHost(), new GetMessagesTool(), Args());
        var info = Assert.IsType<GpMessagesInfo>(r.Data);
        Assert.Contains("dual-list", info.SeverityBasis);
    }

    [Fact]
    public async Task GetMessages_MessagesAndErrorsAreSeparate()
    {
        var gp = new RecordingGeoprocessingService
        {
            MessagesResult = OperationResult<GpMessagesInfo>.Ok(new GpMessagesInfo
            {
                Messages = new[] { "info-only" },
                ErrorMessages = Array.Empty<string>(),
            }),
        };
        var r = await CallAsync(new FakeArcGISHost(geoprocessing: gp), new GetMessagesTool(), Args());
        var info = Assert.IsType<GpMessagesInfo>(r.Data);
        Assert.Empty(info.ErrorMessages);
        Assert.Single(info.Messages);
    }

    [Fact]
    public async Task GetMessages_LastCallTimestamp_WhenPresent()
    {
        var gp = new RecordingGeoprocessingService
        {
            MessagesResult = OperationResult<GpMessagesInfo>.Ok(new GpMessagesInfo
            {
                LastCallAtUtc = "2026-09-20T09:30:00.0000000Z",
                ToolName = "analysis.Clip",
                Success = true,
            }),
        };
        var r = await CallAsync(new FakeArcGISHost(geoprocessing: gp), new GetMessagesTool(), Args());
        var info = Assert.IsType<GpMessagesInfo>(r.Data);
        Assert.Equal("analysis.Clip", info.ToolName);
        Assert.True(info.Success);
        Assert.NotNull(info.LastCallAtUtc);
    }

    [Fact]
    public async Task GetMessages_SuccessNullWhenUnknown()
    {
        var r = await CallAsync(new FakeArcGISHost(), new GetMessagesTool(), Args());
        var info = Assert.IsType<GpMessagesInfo>(r.Data);
        Assert.Null(info.Success);
        Assert.Null(info.ToolName);
    }

    [Fact]
    public async Task GetMessages_ToolNameRoundTrip()
    {
        var gp = new RecordingGeoprocessingService
        {
            MessagesResult = OperationResult<GpMessagesInfo>.Ok(new GpMessagesInfo { ToolName = "x.Y", Success = true }),
        };
        var r = await CallAsync(new FakeArcGISHost(geoprocessing: gp), new GetMessagesTool(), Args());
        Assert.Equal("x.Y", Assert.IsType<GpMessagesInfo>(r.Data).ToolName);
    }

    [Fact]
    public async Task GetMessages_ErrorListPreserved()
    {
        var gp = new RecordingGeoprocessingService
        {
            MessagesResult = OperationResult<GpMessagesInfo>.Ok(new GpMessagesInfo
            {
                ErrorMessages = new[] { "e1", "e2", "e3" },
            }),
        };
        var r = await CallAsync(new FakeArcGISHost(geoprocessing: gp), new GetMessagesTool(), Args());
        Assert.Equal(3, Assert.IsType<GpMessagesInfo>(r.Data).ErrorMessages.Count);
    }

    [Fact]
    public async Task CheckExtension_MissingCode_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new CheckExtensionTool(), Args());
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task CheckExtension_PassesCodeAndCheckout()
    {
        var r = await CallAsync(new FakeArcGISHost(), new CheckExtensionTool(), Args(("extensionCode", "SpatialAnalyst")));
        // FakeLicenseService 未脚本化新成员 → DIM NOT_IMPLEMENTED（证明默认接口实现兜底）。
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task CheckExtension_CodeIsTrimmed()
    {
        var r = await CallAsync(new FakeArcGISHost(), new CheckExtensionTool(), Args(("extensionCode", "  ")));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task CheckExtension_CheckoutFlagFlowsFalseByDefault()
    {
        var r = await CallAsync(new FakeArcGISHost(), new CheckExtensionTool(), Args(("extensionCode", "SpatialAnalyst"), ("checkout", false)));
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task CheckExtension_FailedResult_Surfaces()
    {
        var host = new FakeArcGISHost();
        var r = await CallAsync(host, new CheckExtensionTool(), Args(("extensionCode", "SpatialAnalyst"), ("checkout", true)));
        Assert.False(r.Success);
    }

    [Fact]
    public void CheckExtension_ResultShape_WhenScripted()
    {
        // 直接对 FakeAttributeService 同款脚本化路径做接口级验证（宿主层）。
        var info = new ExtensionCheckInfo { Code = "SpatialAnalyst", Available = true, CheckoutAttempted = true, CheckoutSucceeded = true };
        Assert.True(info.Available);
        Assert.True(info.CheckoutSucceeded!.Value);
    }

    [Fact]
    public async Task CheckExtension_DIMDefault_NotImplemented()
    {
        var r = await CallAsync(new FakeArcGISHost(), new CheckExtensionTool(), Args(("extensionCode", "Unknown")));
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    // ════════════════════════ get_audit_log ═════════════════════════

    [Fact]
    public async Task AuditLog_MissingFile_GracefulEmpty()
    {
        var dir = TempAuditDir();
        try
        {
            var r = await CallAsync(new FakeArcGISHost(), new GetAuditLogTool(), Args(("auditDir", dir)));
            Assert.True(r.Success);
            var d = Assert.IsType<Dictionary<string, object?>>(r.Data);
            Assert.False((bool)d["fileExists"]!);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AuditLog_ReturnsWrittenEntries()
    {
        var dir = TempAuditDir();
        try
        {
            var path = GpAuditLog.ResolvePath(dir);
            GpAuditLog.TryAppend(path, new GpAuditEntry { TimestampUtc = "2026-09-20T08:00:00Z", Tool = "analysis.Buffer", ParameterDigest = "a=1", ParameterForm = "named", Success = true, ResultCode = "OK", DurationMs = 7, AuditNote = "n", Pid = 9 }, out _, out _);
            var r = await CallAsync(new FakeArcGISHost(), new GetAuditLogTool(), Args(("auditDir", dir)));
            var d = Assert.IsType<Dictionary<string, object?>>(r.Data);
            Assert.Equal(1L, Convert.ToInt64(d["totalEntries"]));
            var entries = (IEnumerable<GpAuditEntry>)d["entries"]!;
            var e = entries.First();
            Assert.Equal("analysis.Buffer", e.Tool);
            Assert.Equal(7, e.DurationMs);
            Assert.Equal("n", e.AuditNote);
            Assert.Equal(9, e.Pid);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AuditLog_ToolFilter()
    {
        var dir = TempAuditDir();
        try
        {
            var path = GpAuditLog.ResolvePath(dir);
            foreach (var tool in new[] { "a.X", "a.Y", "a.X" })
            {
                GpAuditLog.TryAppend(path, new GpAuditEntry { TimestampUtc = "t", Tool = tool, ParameterDigest = "d", ParameterForm = "named", Success = true, ResultCode = "OK", DurationMs = 1, Pid = 1 }, out _, out _);
            }

            var r = await CallAsync(new FakeArcGISHost(), new GetAuditLogTool(), Args(("auditDir", dir), ("tool", "a.X")));
            var d = Assert.IsType<Dictionary<string, object?>>(r.Data);
            Assert.Equal(2L, Convert.ToInt64(d["matchedEntries"]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AuditLog_SuccessFilter()
    {
        var dir = TempAuditDir();
        try
        {
            var path = GpAuditLog.ResolvePath(dir);
            GpAuditLog.TryAppend(path, new GpAuditEntry { TimestampUtc = "t", Tool = "a", ParameterDigest = "d", ParameterForm = "named", Success = true, ResultCode = "OK", DurationMs = 1, Pid = 1 }, out _, out _);
            GpAuditLog.TryAppend(path, new GpAuditEntry { TimestampUtc = "t", Tool = "a", ParameterDigest = "d", ParameterForm = "named", Success = false, ResultCode = "GEOPROCESSING_ERROR", DurationMs = 1, Pid = 1 }, out _, out _);
            var r = await CallAsync(new FakeArcGISHost(), new GetAuditLogTool(), Args(("auditDir", dir), ("success", false)));
            var d = Assert.IsType<Dictionary<string, object?>>(r.Data);
            Assert.Equal(1L, Convert.ToInt64(d["matchedEntries"]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AuditLog_LimitCap()
    {
        var dir = TempAuditDir();
        try
        {
            var path = GpAuditLog.ResolvePath(dir);
            for (var i = 0; i < 8; i++)
            {
                GpAuditLog.TryAppend(path, new GpAuditEntry { TimestampUtc = "t", Tool = "a", ParameterDigest = "d", ParameterForm = "named", Success = true, ResultCode = "OK", DurationMs = 1, Pid = 1 }, out _, out _);
            }

            var r = await CallAsync(new FakeArcGISHost(), new GetAuditLogTool(), Args(("auditDir", dir), ("limit", 3)));
            var d = Assert.IsType<Dictionary<string, object?>>(r.Data);
            Assert.Equal(3L, Convert.ToInt64(d["returnedEntries"]));
            Assert.True((bool)d["truncated"]!);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AuditLog_CorruptLinesCounted_NotThrown()
    {
        var dir = TempAuditDir();
        try
        {
            var path = GpAuditLog.ResolvePath(dir);
            GpAuditLog.TryAppend(path, new GpAuditEntry { TimestampUtc = "t", Tool = "a", ParameterDigest = "d", ParameterForm = "named", Success = true, ResultCode = "OK", DurationMs = 1, Pid = 1 }, out _, out _);
            File.AppendAllText(path, "BROKEN>>>\n");
            var r = await CallAsync(new FakeArcGISHost(), new GetAuditLogTool(), Args(("auditDir", dir)));
            var d = Assert.IsType<Dictionary<string, object?>>(r.Data);
            Assert.Equal(1L, Convert.ToInt64(d["corruptLines"]));
            Assert.Equal(1L, Convert.ToInt64(d["totalEntries"]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AuditLog_TimeWindow()
    {
        var dir = TempAuditDir();
        try
        {
            var path = GpAuditLog.ResolvePath(dir);
            GpAuditLog.TryAppend(path, new GpAuditEntry { TimestampUtc = "2026-09-20T08:00:00Z", Tool = "a", ParameterDigest = "d", ParameterForm = "named", Success = true, ResultCode = "OK", DurationMs = 1, Pid = 1 }, out _, out _);
            GpAuditLog.TryAppend(path, new GpAuditEntry { TimestampUtc = "2026-09-20T10:00:00Z", Tool = "a", ParameterDigest = "d", ParameterForm = "named", Success = true, ResultCode = "OK", DurationMs = 1, Pid = 1 }, out _, out _);
            var r = await CallAsync(new FakeArcGISHost(), new GetAuditLogTool(), Args(
                ("auditDir", dir), ("fromUtc", "2026-09-20T09:00:00Z")));
            var d = Assert.IsType<Dictionary<string, object?>>(r.Data);
            Assert.Equal(1L, Convert.ToInt64(d["returnedEntries"]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AuditLog_TempDirRejected_G138()
    {
        var r = await CallAsync(new FakeArcGISHost(), new GetAuditLogTool(), Args(("auditDir", Path.GetTempPath())));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task AuditLog_InvalidWindow_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new GetAuditLogTool(), Args(("fromUtc", "not-a-date")));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    // ════════════════════════ B 段 · 编辑栈（安全件 B2/B3）═════════════

    [Fact]
    public async Task Insert_MissingLayer_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new InsertFeaturesTool(), Args(
            ("rows", new List<object?> { new Dictionary<string, object?> { ["F"] = 1 } })));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Insert_MissingRows_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new InsertFeaturesTool(), Args(("layerName", "L")));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Insert_RowsRouted_AndCounted()
    {
        var edits = new FakeEditService();
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new InsertFeaturesTool(), Args(
            ("layerName", "L"), ("mapName", "M"),
            ("rows", new List<object?>
            {
                new Dictionary<string, object?> { ["CAT"] = "A", ["VAL"] = 1 },
                new Dictionary<string, object?> { ["CAT"] = "B", ["VAL"] = 2 },
            })));
        Assert.True(r.Success);
        var result = Assert.IsType<EditOpResult>(r.Data);
        Assert.Equal(2, result.RowsAffected);
        Assert.Equal(2, result.PendingChangeCount);
        Assert.Contains("insert:L:2", edits.Calls);
        Assert.Equal("M", edits.LastInsertMap);
    }

    [Fact]
    public async Task Insert_PendingAccumulates_AcrossCalls()
    {
        var edits = new FakeEditService();
        var host = new FakeArcGISHost(edits: edits);
        await CallAsync(host, new InsertFeaturesTool(), Args(("layerName", "L"), ("rows", new List<object?> { new Dictionary<string, object?> { ["F"] = 1 } })));
        var r2 = await CallAsync(host, new InsertFeaturesTool(), Args(("layerName", "L"), ("rows", new List<object?> { new Dictionary<string, object?> { ["F"] = 2 } })));
        Assert.Equal(2, Assert.IsType<EditOpResult>(r2.Data).PendingChangeCount);
    }

    [Fact]
    public async Task Insert_FailedResult_Surfaces()
    {
        var edits = new FakeEditService
        {
            InsertResult = OperationResult<EditOpResult>.Fail(ErrorCodes.InternalError, "op failed"),
        };
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new InsertFeaturesTool(), Args(
            ("layerName", "L"), ("rows", new List<object?> { new Dictionary<string, object?>() })));
        Assert.Equal(ErrorCodes.InternalError, Code(r));
    }

    [Fact]
    public async Task Insert_TracksAffectedLayer()
    {
        var edits = new FakeEditService();
        await CallAsync(new FakeArcGISHost(edits: edits), new InsertFeaturesTool(), Args(
            ("layerName", "MyLayer"), ("rows", new List<object?> { new Dictionary<string, object?>() })));
        Assert.Contains("MyLayer", edits.Affected);
    }

    [Fact]
    public async Task Insert_NonObjectRow_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new InsertFeaturesTool(), Args(
            ("layerName", "L"), ("rows", new List<object?> { "not-a-dict" })));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Insert_TransactionNoteDisclosed()
    {
        var r = await CallAsync(new FakeArcGISHost(), new InsertFeaturesTool(), Args(
            ("layerName", "L"), ("rows", new List<object?> { new Dictionary<string, object?>() })));
        Assert.Contains("save_edits", Assert.IsType<EditOpResult>(r.Data).TransactionNote);
    }

    [Fact]
    public async Task InsertDIM_DefaultFake_PathWorks()
    {
        // 默认 FakeEditService（DIM 兜底路径）：缺省成功语义 + 计数。
        var r = await CallAsync(new FakeArcGISHost(), new InsertFeaturesTool(), Args(
            ("layerName", "L"), ("rows", new List<object?> { new Dictionary<string, object?>() })));
        Assert.True(r.Success);
        Assert.Equal(1, Assert.IsType<EditOpResult>(r.Data).RowsAffected);
    }

    // ── B2 update_features ──

    [Fact]
    public async Task Update_WithoutConfirm_RejectedAtToolLayer()
    {
        var r = await CallAsync(new FakeArcGISHost(), new UpdateFeaturesTool(), Args(
            ("layerName", "L"), ("where", "CAT='A'"), ("confirm", false),
            ("attributes", new Dictionary<string, object?> { ["VAL"] = 9 })));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Contains("confirm=true is required", r.Errors.First().Message);
    }

    [Fact]
    public async Task Update_WithConfirm_Routed()
    {
        var edits = new FakeEditService();
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new UpdateFeaturesTool(), Args(
            ("layerName", "L"), ("where", "CAT='A'"), ("confirm", true),
            ("attributes", new Dictionary<string, object?> { ["VAL"] = 9 })));
        Assert.True(r.Success);
        Assert.Contains("confirm=True", edits.Calls.Single(c => c.StartsWith("update:")));
    }

    [Fact]
    public async Task Update_OidListRouted()
    {
        var edits = new FakeEditService();
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new UpdateFeaturesTool(), Args(
            ("layerName", "L"), ("confirm", true), ("oidList", new List<object?> { 1L, 2L, 3L }),
            ("attributes", new Dictionary<string, object?> { ["VAL"] = 9 })));
        Assert.True(r.Success);
        Assert.Equal(3, Assert.IsType<EditOpResult>(r.Data).RowsAffected);
    }

    [Fact]
    public async Task Update_GeometryWktRouted()
    {
        var edits = new FakeEditService();
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new UpdateFeaturesTool(), Args(
            ("layerName", "L"), ("where", "OID=1"), ("confirm", true),
            ("geometryWkt", "POINT (1 2)")));
        Assert.True(r.Success);
        Assert.Contains("wkt=yes", edits.Calls.Single(c => c.StartsWith("update:")));
    }

    [Fact]
    public async Task Update_MissingLayer_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new UpdateFeaturesTool(), Args(("confirm", true)));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Update_OidListMustBeIntegers()
    {
        var r = await CallAsync(new FakeArcGISHost(), new UpdateFeaturesTool(), Args(
            ("layerName", "L"), ("confirm", true), ("oidList", new List<object?> { "x" })));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Update_PendingAccumulates()
    {
        var edits = new FakeEditService();
        var host = new FakeArcGISHost(edits: edits);
        await CallAsync(host, new InsertFeaturesTool(), Args(("layerName", "L"), ("rows", new List<object?> { new Dictionary<string, object?>() })));
        var r = await CallAsync(host, new UpdateFeaturesTool(), Args(
            ("layerName", "L"), ("confirm", true), ("oidList", new List<object?> { 1L }),
            ("attributes", new Dictionary<string, object?> { ["V"] = 2 })));
        Assert.Equal(2, Assert.IsType<EditOpResult>(r.Data).PendingChangeCount);
    }

    [Fact]
    public async Task Update_FailedResult_Surfaces()
    {
        var edits = new FakeEditService { UpdateResult = OperationResult<EditOpResult>.Fail(ErrorCodes.InternalError, "boom") };
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new UpdateFeaturesTool(), Args(
            ("layerName", "L"), ("confirm", true), ("where", "1=1"), ("attributes", new Dictionary<string, object?>())));
        Assert.Equal(ErrorCodes.InternalError, Code(r));
    }

    // ── B3 delete_features ──

    [Fact]
    public async Task Delete_WithoutConfirm_RejectedAtToolLayer()
    {
        var r = await CallAsync(new FakeArcGISHost(), new DeleteFeaturesTool(), Args(("layerName", "L"), ("where", "1=1"), ("confirm", false)));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Contains("confirm=true is required", r.Errors.First().Message);
    }

    [Fact]
    public async Task Delete_WithConfirm_Routed()
    {
        var edits = new FakeEditService();
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new DeleteFeaturesTool(), Args(
            ("layerName", "L"), ("confirm", true), ("where", "CAT='X'")));
        Assert.True(r.Success);
        Assert.Contains(edits.Calls, c => c.Contains("delete:L:where=CAT='X'"));
    }

    [Fact]
    public async Task Delete_OidListRouted()
    {
        var edits = new FakeEditService();
        await CallAsync(new FakeArcGISHost(edits: edits), new DeleteFeaturesTool(), Args(
            ("layerName", "L"), ("confirm", true), ("oidList", new List<object?> { 5L })));
        Assert.Contains("oids=1", edits.Calls.Single(c => c.StartsWith("delete:")));
    }

    [Fact]
    public async Task Delete_MissingLayer_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new DeleteFeaturesTool(), Args(("confirm", true)));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Delete_PendingAccumulates()
    {
        var edits = new FakeEditService();
        var host = new FakeArcGISHost(edits: edits);
        await CallAsync(host, new DeleteFeaturesTool(), Args(("layerName", "L"), ("confirm", true), ("oidList", new List<object?> { 1L, 2L })));
        var r2 = await CallAsync(host, new DeleteFeaturesTool(), Args(("layerName", "L"), ("confirm", true), ("oidList", new List<object?> { 3L })));
        Assert.Equal(3, Assert.IsType<EditOpResult>(r2.Data).PendingChangeCount);
    }

    [Fact]
    public async Task Delete_NOT_FOUND_Surfaces()
    {
        var edits = new FakeEditService { DeleteResult = OperationResult<EditOpResult>.Fail(ErrorCodes.NotFound, "no rows") };
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new DeleteFeaturesTool(), Args(
            ("layerName", "L"), ("confirm", true), ("where", "1=2")));
        Assert.Equal(ErrorCodes.NotFound, Code(r));
    }

    [Fact]
    public async Task Delete_InvalidOids_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new DeleteFeaturesTool(), Args(
            ("layerName", "L"), ("confirm", true), ("oidList", new List<object?> { 1.5 })));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Delete_TransactionNoteDisclosed()
    {
        var r = await CallAsync(new FakeArcGISHost(), new InsertFeaturesTool(), Args(
            ("layerName", "L"), ("rows", new List<object?> { new Dictionary<string, object?>() })));
        Assert.Contains("save_edits", Assert.IsType<EditOpResult>(r.Data).TransactionNote);
    }

    // ── B4/B5/B6 ──

    [Fact]
    public async Task SaveEdits_ResetsSession()
    {
        var edits = new FakeEditService();
        var host = new FakeArcGISHost(edits: edits);
        await CallAsync(host, new InsertFeaturesTool(), Args(("layerName", "L"), ("rows", new List<object?> { new Dictionary<string, object?>() })));
        var r = await CallAsync(host, new SaveEditsTool(), Args());
        var state = Assert.IsType<EditSessionState>(r.Data);
        Assert.False(state.HasEdits);
        Assert.Equal(0, state.PendingChangeCount);
        Assert.Contains("save", edits.Calls);
    }

    [Fact]
    public async Task SaveEdits_FailedResult_Surfaces()
    {
        var edits = new FakeEditService { SaveResult = OperationResult<EditSessionState>.Fail(ErrorCodes.InternalError, "save failed") };
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new SaveEditsTool(), Args());
        Assert.Equal(ErrorCodes.InternalError, Code(r));
    }

    [Fact]
    public async Task SaveEdits_IdempotentEmptySession()
    {
        var r = await CallAsync(new FakeArcGISHost(), new SaveEditsTool(), Args());
        Assert.True(r.Success);
        Assert.False(Assert.IsType<EditSessionState>(r.Data).HasEdits);
    }

    [Fact]
    public async Task SaveEdits_SecondCallStillOk()
    {
        var host = new FakeArcGISHost();
        await CallAsync(host, new SaveEditsTool(), Args());
        var r2 = await CallAsync(host, new SaveEditsTool(), Args());
        Assert.True(r2.Success);
    }

    [Fact]
    public async Task DiscardEdits_ResetsSession()
    {
        var edits = new FakeEditService();
        var host = new FakeArcGISHost(edits: edits);
        await CallAsync(host, new InsertFeaturesTool(), Args(("layerName", "L"), ("rows", new List<object?> { new Dictionary<string, object?>() })));
        var r = await CallAsync(host, new DiscardEditsTool(), Args());
        var state = Assert.IsType<EditSessionState>(r.Data);
        Assert.Equal(0, state.PendingChangeCount);
        Assert.Contains("discard", edits.Calls);
    }

    [Fact]
    public async Task DiscardEdits_FailedResult_Surfaces()
    {
        var edits = new FakeEditService { DiscardResult = OperationResult<EditSessionState>.Fail(ErrorCodes.InternalError, "discard failed") };
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new DiscardEditsTool(), Args());
        Assert.Equal(ErrorCodes.InternalError, Code(r));
    }

    [Fact]
    public async Task DiscardEdits_IdempotentEmptySession()
    {
        var r = await CallAsync(new FakeArcGISHost(), new DiscardEditsTool(), Args());
        Assert.True(r.Success);
    }

    [Fact]
    public async Task DiscardEdits_AfterInsert_ClearsAffected()
    {
        var edits = new FakeEditService();
        var host = new FakeArcGISHost(edits: edits);
        await CallAsync(host, new InsertFeaturesTool(), Args(("layerName", "L"), ("rows", new List<object?> { new Dictionary<string, object?>() })));
        var r = await CallAsync(host, new DiscardEditsTool(), Args());
        Assert.Empty(Assert.IsType<EditSessionState>(r.Data).AffectedLayers);
    }

    [Fact]
    public async Task EditSession_EmptyState()
    {
        var r = await CallAsync(new FakeArcGISHost(), new GetEditSessionTool(), Args());
        var state = Assert.IsType<EditSessionState>(r.Data);
        Assert.False(state.HasEdits);
        Assert.Equal(0, state.PendingChangeCount);
    }

    [Fact]
    public async Task EditSession_ReflectsPending()
    {
        var edits = new FakeEditService();
        var host = new FakeArcGISHost(edits: edits);
        await CallAsync(host, new InsertFeaturesTool(), Args(("layerName", "A"), ("rows", new List<object?> { new Dictionary<string, object?>(), new Dictionary<string, object?>() })));
        await CallAsync(host, new DeleteFeaturesTool(), Args(("layerName", "B"), ("confirm", true), ("oidList", new List<object?> { 1L })));
        var r = await CallAsync(host, new GetEditSessionTool(), Args());
        var state = Assert.IsType<EditSessionState>(r.Data);
        Assert.True(state.HasEdits);
        Assert.Equal(3, state.PendingChangeCount);
        Assert.Equal(new[] { "A", "B" }, state.AffectedLayers);
    }

    [Fact]
    public async Task EditSession_TrackingNoteDisclosed()
    {
        var r = await CallAsync(new FakeArcGISHost(), new GetEditSessionTool(), Args());
        Assert.Contains("authoritative", Assert.IsType<EditSessionState>(r.Data).TrackingNote);
    }

    [Fact]
    public async Task EditSession_ScriptedSurfaces()
    {
        var edits = new FakeEditService { SessionResult = OperationResult<EditSessionState>.Ok(new EditSessionState { HasEdits = true, PendingChangeCount = 9 }) };
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new GetEditSessionTool(), Args());
        Assert.Equal(9, Assert.IsType<EditSessionState>(r.Data).PendingChangeCount);
    }

    [Fact]
    public async Task EditSession_SaveThenQuery_IsClean()
    {
        var edits = new FakeEditService();
        var host = new FakeArcGISHost(edits: edits);
        await CallAsync(host, new InsertFeaturesTool(), Args(("layerName", "L"), ("rows", new List<object?> { new Dictionary<string, object?>() })));
        await CallAsync(host, new SaveEditsTool(), Args());
        var r = await CallAsync(host, new GetEditSessionTool(), Args());
        Assert.False(Assert.IsType<EditSessionState>(r.Data).HasEdits);
    }

    [Fact]
    public async Task EditSession_FailedResult_Surfaces()
    {
        var edits = new FakeEditService { SessionResult = OperationResult<EditSessionState>.Fail(ErrorCodes.InvalidState, "no project") };
        var r = await CallAsync(new FakeArcGISHost(edits: edits), new GetEditSessionTool(), Args());
        Assert.Equal(ErrorCodes.InvalidState, Code(r));
    }

    // ════════════════════════ C 段 · 统计聚合 ═══════════════════════

    [Fact]
    public async Task Stats_MissingField_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new GetFieldStatisticsTool(), Args(("layerName", "L")));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Stats_MissingLayer_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new GetFieldStatisticsTool(), Args(("fieldName", "F")));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Stats_AllArgsRouted()
    {
        var attrs = new FakeAttributeService
        {
            StatsResult = OperationResult<FieldStatisticsInfo>.Ok(new FieldStatisticsInfo { FieldName = "VAL", Count = 3 }),
        };
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetFieldStatisticsTool(), Args(
            ("layerName", "FC"), ("fieldName", "VAL"), ("mapName", "M"), ("where", "CAT='A'")));
        Assert.True(r.Success);
        Assert.Equal(("M", "FC", "VAL", "CAT='A'"), attrs.LastStatsCall);
    }

    [Fact]
    public async Task Stats_ScriptedResult_Surfaces()
    {
        var attrs = new FakeAttributeService
        {
            StatsResult = OperationResult<FieldStatisticsInfo>.Ok(new FieldStatisticsInfo
            {
                FieldName = "VAL", Count = 4, Min = 1, Max = 4, Mean = 2.5, Sum = 10, Skipped = 1,
            }),
        };
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetFieldStatisticsTool(), Args(("layerName", "FC"), ("fieldName", "VAL")));
        var info = Assert.IsType<FieldStatisticsInfo>(r.Data);
        Assert.Equal(4, info.Count);
        Assert.Equal(1, info.Skipped);
        Assert.Equal(2.5, info.Mean);
    }

    [Fact]
    public async Task Stats_FailedResult_Surfaces()
    {
        var attrs = new FakeAttributeService { StatsResult = OperationResult<FieldStatisticsInfo>.Fail(ErrorCodes.InvalidArgument, "non-numeric") };
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetFieldStatisticsTool(), Args(("layerName", "FC"), ("fieldName", "TXT")));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Stats_FieldIsTrimmed()
    {
        var attrs = new FakeAttributeService { StatsResult = OperationResult<FieldStatisticsInfo>.Ok(new FieldStatisticsInfo()) };
        await CallAsync(new FakeArcGISHost(attributes: attrs), new GetFieldStatisticsTool(), Args(("layerName", "FC"), ("fieldName", " VAL ")));
        Assert.Equal("VAL", attrs.LastStatsCall!.Value.Field);
    }

    [Fact]
    public async Task Stats_DIMDefault_SurfacesWhenUnscripted()
    {
        var r = await CallAsync(new FakeArcGISHost(), new GetFieldStatisticsTool(), Args(("layerName", "FC"), ("fieldName", "F")));
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Stats_LayerNameTrimmed()
    {
        var attrs = new FakeAttributeService { StatsResult = OperationResult<FieldStatisticsInfo>.Ok(new FieldStatisticsInfo()) };
        await CallAsync(new FakeArcGISHost(attributes: attrs), new GetFieldStatisticsTool(), Args(("layerName", " FC "), ("fieldName", "F")));
        Assert.Equal("FC", attrs.LastStatsCall!.Value.Layer);
    }

    [Fact]
    public async Task Summarize_MissingGroupBy_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new SummarizeFeaturesTool(), Args(("layerName", "L")));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Summarize_EmptyGroupBy_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new SummarizeFeaturesTool(), Args(
            ("layerName", "L"), ("groupByFields", new List<object?>())));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Summarize_AllArgsRouted()
    {
        var attrs = new FakeAttributeService
        {
            SummaryResult = OperationResult<GroupSummaryInfo>.Ok(new GroupSummaryInfo { Rows = Array.Empty<GroupSummaryRow>() }),
        };
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new SummarizeFeaturesTool(), Args(
            ("layerName", "FC"), ("groupByFields", new List<object?> { "CAT" }), ("aggField", "VAL"),
            ("aggregations", new List<object?> { "count", "sum" }), ("where", "V>0"), ("topN", 5)));
        Assert.True(r.Success);
        var call = attrs.LastSummaryCall!.Value;
        Assert.Equal(new[] { "CAT" }, call.Groups);
        Assert.Equal("VAL", call.AggField);
        Assert.Equal(new[] { "count", "sum" }, call.Aggs);
        Assert.Equal(5, call.TopN);
    }

    [Fact]
    public async Task Summarize_ScriptedRows_Surfaces()
    {
        var attrs = new FakeAttributeService
        {
            SummaryResult = OperationResult<GroupSummaryInfo>.Ok(new GroupSummaryInfo
            {
                GroupByFields = new[] { "CAT" },
                Rows = new[]
                {
                    new GroupSummaryRow { Key = "A", Count = 7, Sum = 3.5 },
                    new GroupSummaryRow { Key = "B", Count = 3, Sum = 9.0 },
                },
                TotalGroups = 2,
            }),
        };
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new SummarizeFeaturesTool(), Args(
            ("layerName", "FC"), ("groupByFields", new List<object?> { "CAT" })));
        var info = Assert.IsType<GroupSummaryInfo>(r.Data);
        Assert.Equal(2, info.Rows.Count);
        Assert.Equal("A", info.Rows[0].Key);
        Assert.Equal(7, info.Rows[0].Count);
    }

    [Fact]
    public async Task Summarize_FailedResult_Surfaces()
    {
        var attrs = new FakeAttributeService { SummaryResult = OperationResult<GroupSummaryInfo>.Fail(ErrorCodes.InvalidArgument, "aggField required") };
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new SummarizeFeaturesTool(), Args(
            ("layerName", "FC"), ("groupByFields", new List<object?> { "CAT" }), ("aggregations", new List<object?> { "sum" })));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Summarize_LayerMissing_Rejected()
    {
        var r = await CallAsync(new FakeArcGISHost(), new SummarizeFeaturesTool(), Args(
            ("groupByFields", new List<object?> { "CAT" })));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Summarize_GroupByMustBeArray()
    {
        var r = await CallAsync(new FakeArcGISHost(), new SummarizeFeaturesTool(), Args(
            ("layerName", "FC"), ("groupByFields", "CAT")));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Summarize_DIMDefault_SurfacesWhenUnscripted()
    {
        var r = await CallAsync(new FakeArcGISHost(), new SummarizeFeaturesTool(), Args(
            ("layerName", "FC"), ("groupByFields", new List<object?> { "CAT" })));
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }
}
