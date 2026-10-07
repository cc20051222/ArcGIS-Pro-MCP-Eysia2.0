using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-064 单测（其二）：D 段 run_batch（安全重点 14）+ G-166 差异化四件（diagnose / snapshot / restore / readonly）
/// ＋ 写类分层覆盖性与路由闸门集成。
/// </summary>
public class D064G166Tests
{
    // ══════════════════════════ 共用夹具 ══════════════════════════

    private sealed class OkSchemaService : ISchemaService
    {
        public readonly List<string> Calls = new();

        public Task<OperationResult<SchemaInfo>> GetSchemaInfoAsync(string path, CancellationToken ct = default)
            => Task.FromResult(OperationResult<SchemaInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<IReadOnlyList<DomainInfo>>> GetDomainsAsync(string workspace, int maxItems, CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<DomainInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<SubtypeInfo>> GetSubtypesAsync(string path, CancellationToken ct = default)
            => Task.FromResult(OperationResult<SubtypeInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<IReadOnlyList<IndexInfo>>> GetIndexesAsync(string path, int maxItems, CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<IndexInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<TruncateTableResult>> TruncateTableAsync(string path, CancellationToken ct = default)
        {
            Calls.Add("truncate:" + path);
            return Task.FromResult(OperationResult<TruncateTableResult>.Ok(
                new TruncateTableResult { Path = path, RowsBefore = 5, RowsAfter = 0, SchemaPreserved = true }));
        }

        public Task<OperationResult<DeleteFieldResult>> DeleteFieldAsync(string path, string fieldName, CancellationToken ct = default)
        {
            Calls.Add("delfield:" + path + ":" + fieldName);
            return Task.FromResult(OperationResult<DeleteFieldResult>.Ok(new DeleteFieldResult { Path = path, FieldName = fieldName, Deleted = true }));
        }
    }

    private sealed class OkMapService : IMapService
    {
        public readonly List<string> Calls = new();

        public Task<OperationResult<MapInfo?>> GetCurrentMapAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapInfo?>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<IReadOnlyList<MapInfo>>> GetMapsAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<MapInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<MapExtentInfo>> GetMapExtentAsync(string? mapName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapExtentInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<MapExtentSetInfo>> SetMapExtentAsync(
            string? mapName, double xMin, double yMin, double xMax, double yMax, string? spatialReference, CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapExtentSetInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<ActivateMapResult>> ActivateMapAsync(string mapName, CancellationToken ct = default)
        {
            Calls.Add("activate:" + mapName);
            return Task.FromResult(OperationResult<ActivateMapResult>.Ok(
                new ActivateMapResult { MapName = mapName, ActiveMap = mapName, Activated = true, ViewOpened = true }));
        }
    }

    private sealed class OkGpService : IGeoprocessingService
    {
        public readonly List<string> Calls = new();

        public Task<OperationResult<GeoprocessingResult>> RunToolAsync(GeoprocessingRequest request, CancellationToken ct = default)
            => Task.FromResult(OperationResult<GeoprocessingResult>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<OutputExistence>> CheckOutputExistsAsync(string outputPath, CancellationToken ct = default)
            => Task.FromResult(OperationResult<OutputExistence>.Ok(OutputExistence.NotExists));

        public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByAttributeAsync(
            string? mapName, string layerName, string mode, IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByLocationAsync(
            string? mapName, string layerName, string? selectingLayerName, string overlapType,
            double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<GpEnvironmentInfo>> GetEnvironmentAsync(CancellationToken ct = default)
        {
            Calls.Add("getenv");
            return Task.FromResult(OperationResult<GpEnvironmentInfo>.Ok(new GpEnvironmentInfo
            {
                Workspace = @"D:\d064unit\ws.gdb",
                Unset = new[] { "mask" },
            }));
        }

        public Task<OperationResult<SetEnvironmentResult>> SetEnvironmentAsync(GpEnvironmentInfo? desired, bool reset, CancellationToken ct = default)
        {
            Calls.Add($"setenv:{reset}:{(desired is null ? "null" : desired.Workspace)}");
            return Task.FromResult(OperationResult<SetEnvironmentResult>.Ok(new SetEnvironmentResult { Reset = reset }));
        }
    }

    private sealed class ScriptedDiagnostics : IDiagnosticsService
    {
        public DiagnosticsReport? Report { get; set; }
        public int Calls { get; private set; }

        public Task<OperationResult<DiagnosticsReport>> DiagnoseAsync(CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(OperationResult<DiagnosticsReport>.Ok(
                Report ?? new DiagnosticsReport { OverallStatus = DiagnosticsComposer.StatusOk }));
        }
    }

    private sealed class ScriptedSnapshot : ISnapshotService
    {
        public OperationResult<SnapshotResult>? CreateResult { get; set; }
        public OperationResult<RestoreSnapshotResult>? RestoreResult { get; set; }
        public readonly List<string> Calls = new();

        public Task<OperationResult<SnapshotResult>> CreateAsync(string? snapshotDir, string? snapshotId, CancellationToken ct = default)
        {
            Calls.Add($"create:{snapshotDir}:{snapshotId}");
            return Task.FromResult(CreateResult ?? OperationResult<SnapshotResult>.Ok(new SnapshotResult { Directory = snapshotDir ?? "d" }));
        }

        public Task<OperationResult<RestoreSnapshotResult>> RestoreAsync(string snapshotDir, bool confirm, CancellationToken ct = default)
        {
            Calls.Add($"restore:{snapshotDir}:{confirm}");
            return Task.FromResult(RestoreResult ?? OperationResult<RestoreSnapshotResult>.Ok(new RestoreSnapshotResult { Directory = snapshotDir, Confirm = true }));
        }
    }

    private sealed class Fixture
    {
        public OkSchemaService Schema { get; } = new();
        public OkGpService Gp { get; } = new();
        public OkMapService Maps { get; } = new();
        public ScriptedDiagnostics Diagnostics { get; } = new();
        public ScriptedSnapshot Snapshots { get; } = new();
        public FakeArcGISHost Host { get; }
        public MCPToolRegistry Registry { get; } = new();
        public ReadOnlyModeService ReadOnly { get; } = new();

        public Fixture(bool withDiagnostics = true, bool withSnapshots = true)
        {
            Host = new FakeArcGISHost(
                schema: Schema,
                geoprocessing: Gp,
                maps: Maps,
                diagnostics: withDiagnostics ? Diagnostics : null,
                snapshots: withSnapshots ? Snapshots : null);
        }

        public ToolExecutionContext Ctx(Dictionary<string, object?>? args = null, bool withRegistry = true)
            => new()
            {
                Host = Host,
                Arguments = args ?? new Dictionary<string, object?>(StringComparer.Ordinal),
                Settings = new MCPSettings { Port = 6520 },
                Registry = withRegistry ? Registry : null,
                ReadOnly = ReadOnly,
            };
    }

    private const string Fc = @"D:\d064unit\work.gdb\roads";

    /// <summary>D 盘临时目录（G-138：snapshotDir 不得落在 %TEMP% 之下，故不能复用 TestWorkspace）。</summary>
    private sealed class DDriveTemp : IDisposable
    {
        public DDriveTemp()
        {
            Root = Path.Combine(@"D:\ArcGIS-Pro-MCP 2.0\.runtime\unit-tmp-d064", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string Dir(string name)
        {
            var p = Path.Combine(Root, name);
            Directory.CreateDirectory(p);
            return p;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // 清理失败不影响判据（目录位于 .runtime 受管区）。
            }
        }
    }

    private static Dictionary<string, object?> A(params (string Key, object? Value)[] pairs)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (k, v) in pairs)
        {
            d[k] = v;
        }

        return d;
    }

    private static Dictionary<string, object?> BatchItem(string tool, Dictionary<string, object?>? args = null, bool? continueOnError = null)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal) { ["tool"] = tool };
        if (args is not null)
        {
            d["arguments"] = args;
        }

        if (continueOnError is not null)
        {
            d["continueOnError"] = continueOnError;
        }

        return d;
    }

    private static string Code<T>(OperationResult<T> r) => r.Errors.Count > 0 ? r.Errors[0].Code : "OK";

    // ══════════════════════════ run_batch（16）══════════════════════════

    [Fact]
    public async Task RunBatch_MissingItems_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new RunBatchTool().ExecuteAsync(f.Ctx())));
    }

    [Fact]
    public async Task RunBatch_EmptyItems_IsInvalid()
    {
        var f = new Fixture();
        var r = await new RunBatchTool().ExecuteAsync(f.Ctx(A(("items", new List<object?>()))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task RunBatch_TooManyItems_IsInvalid()
    {
        var f = new Fixture();
        var items = Enumerable.Range(0, BatchPolicy.MaxItems + 1).Select(_ => (object?)BatchItem("ping")).ToList();
        var r = await new RunBatchTool().ExecuteAsync(f.Ctx(A(("items", items))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Contains("maximum per batch", r.Errors[0].Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunBatch_UnregisteredTool_RefusesWholeBatch_ZeroSideEffects()
    {
        var f = new Fixture();
        f.Registry.Register(new TruncateTableTool());
        var items = new List<object?> { BatchItem("truncate_table", A(("path", Fc), ("confirm", true))), BatchItem("no_such_tool") };
        var r = await new RunBatchTool().ExecuteAsync(f.Ctx(A(("items", items))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Schema.Calls);   // 整批拒绝 ⇒ 第一项也未执行
    }

    [Fact]
    public async Task RunBatch_SelfNesting_Refused()
    {
        var f = new Fixture();
        f.Registry.Register(new RunBatchTool());
        var r = await new RunBatchTool().ExecuteAsync(f.Ctx(A(("items", new List<object?> { BatchItem("run_batch") }))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task RunBatch_ReadonlyToggleInsideBatch_Refused()
    {
        var f = new Fixture();
        f.Registry.Register(new SetReadOnlyModeTool());
        var r = await new RunBatchTool().ExecuteAsync(
            f.Ctx(A(("items", new List<object?> { BatchItem("set_readonly_mode", A(("enabled", true))) }))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.False(f.ReadOnly.IsReadOnly);
    }

    [Fact]
    public async Task RunBatch_AllowedTools_NarrowsAndRefusesOthers()
    {
        var f = new Fixture();
        f.Registry.Register(new GetEnvironmentTool());
        f.Registry.Register(new TruncateTableTool());
        var items = new List<object?> { BatchItem("truncate_table", A(("path", Fc), ("confirm", true))) };
        var r = await new RunBatchTool().ExecuteAsync(
            f.Ctx(A(("items", items), ("allowedTools", new List<object?> { "get_environment" }))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task RunBatch_TwoToolCombo_SucceedsInOrder()
    {
        var f = new Fixture();
        f.Registry.Register(new GetEnvironmentTool());
        f.Registry.Register(new TruncateTableTool());
        var items = new List<object?>
        {
            BatchItem("get_environment"),
            BatchItem("truncate_table", A(("path", Fc), ("confirm", true))),
        };
        var r = await new RunBatchTool().ExecuteAsync(f.Ctx(A(("items", items))));
        var payload = Assert.IsType<RunBatchResult>(r.Data);
        Assert.Equal(2, payload.TotalRequested);
        Assert.Equal(2, payload.Executed);
        Assert.Equal(2, payload.Succeeded);
        Assert.Equal(0, payload.Failed);
        Assert.False(payload.Truncated);
        Assert.Equal(30000, payload.BudgetMs);
        Assert.Single(f.Schema.Calls);
    }

    [Fact]
    public async Task RunBatch_RequiredArgMissing_ItemFailsWithInvalidArgument()
    {
        var f = new Fixture();
        f.Registry.Register(new TruncateTableTool());
        var r = await new RunBatchTool().ExecuteAsync(
            f.Ctx(A(("items", new List<object?> { BatchItem("truncate_table", A(("path", Fc))) }))));
        var payload = Assert.IsType<RunBatchResult>(r.Data);
        Assert.Equal(1, payload.Failed);
        Assert.Equal(ErrorCodes.InvalidArgument, payload.Items[0].ResultCode);
        Assert.False(payload.Items[0].Executed);
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task RunBatch_ContinueOnErrorFalse_AbortsRest()
    {
        var f = new Fixture();
        f.Registry.Register(new TruncateTableTool());
        f.Registry.Register(new GetEnvironmentTool());
        var items = new List<object?>
        {
            BatchItem("truncate_table", A(("path", Fc))),   // 缺 confirm ⇒ INVALID_ARGUMENT
            BatchItem("get_environment"),
        };
        var r = await new RunBatchTool().ExecuteAsync(f.Ctx(A(("items", items))));
        var payload = Assert.IsType<RunBatchResult>(r.Data);
        Assert.Equal(0, payload.Executed);
        Assert.Equal(1, payload.Failed);    // 项 0 = 校验失败（invalid-arguments）⇒ 计 Failed
        Assert.Equal(1, payload.Skipped);   // 项 1 = 未尝试（aborted-after-failure）
        Assert.Equal("aborted-after-failure", payload.Items[1].SkippedReason);
    }

    [Fact]
    public async Task RunBatch_ContinueOnErrorTrue_Continues()
    {
        var f = new Fixture();
        f.Registry.Register(new TruncateTableTool());
        f.Registry.Register(new GetEnvironmentTool());
        var items = new List<object?>
        {
            BatchItem("truncate_table", A(("path", Fc))),
            BatchItem("get_environment"),
        };
        var r = await new RunBatchTool().ExecuteAsync(f.Ctx(A(("items", items), ("continueOnError", true))));
        var payload = Assert.IsType<RunBatchResult>(r.Data);
        Assert.Equal(1, payload.Executed);
        Assert.Equal(1, payload.Succeeded);
        Assert.Equal(1, payload.Failed);
        Assert.Equal(0, payload.Skipped);
    }

    [Fact]
    public async Task RunBatch_ReadOnlyMode_SkipsWriteItems_KeepsReadItems()
    {
        var f = new Fixture();
        f.Registry.Register(new TruncateTableTool());
        f.Registry.Register(new GetEnvironmentTool());
        f.ReadOnly.Set(true);
        var items = new List<object?>
        {
            BatchItem("truncate_table", A(("path", Fc), ("confirm", true))),
            BatchItem("get_environment"),
        };
        var r = await new RunBatchTool().ExecuteAsync(f.Ctx(A(("items", items), ("continueOnError", true))));
        var payload = Assert.IsType<RunBatchResult>(r.Data);
        Assert.True(payload.ReadOnlyMode);
        Assert.Equal("read-only", payload.Items[0].SkippedReason);
        Assert.Equal(ErrorCodes.PermissionDenied, payload.Items[0].ResultCode);
        Assert.True(payload.Items[1].Success);
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task RunBatch_BudgetClampedToHardMax()
    {
        var f = new Fixture();
        f.Registry.Register(new GetEnvironmentTool());
        var r = await new RunBatchTool().ExecuteAsync(
            f.Ctx(A(("items", new List<object?> { BatchItem("get_environment") }), ("budgetMs", 999999))));
        var payload = Assert.IsType<RunBatchResult>(r.Data);
        Assert.Equal(BatchPolicy.MaxBudgetMs, payload.BudgetMs);
        Assert.Equal(30000, payload.BudgetMs);
    }

    [Fact]
    public async Task RunBatch_BudgetExhausted_TruncatesAndReportsCompleted()
    {
        var f = new Fixture();
        f.Registry.Register(new GetEnvironmentTool());
        var items = Enumerable.Range(0, BatchPolicy.MaxItems).Select(_ => (object?)BatchItem("get_environment")).ToList();
        // budgetMs=1 ⇒ 首项之后预算即耗尽（ClampBudget 允许下调；取满额项数以消除计时抖动）。
        var r = await new RunBatchTool().ExecuteAsync(f.Ctx(A(("items", items), ("budgetMs", 1))));
        var payload = Assert.IsType<RunBatchResult>(r.Data);
        Assert.True(payload.Truncated);
        Assert.Contains(payload.Items, i => i.SkippedReason == "budget-exceeded");
        Assert.Equal(ErrorCodes.Timeout, payload.Items.First(i => i.SkippedReason == "budget-exceeded").ResultCode);
    }

    [Fact]
    public async Task RunBatch_AuditNoteAndElapsedReported()
    {
        var f = new Fixture();
        f.Registry.Register(new GetEnvironmentTool());
        var r = await new RunBatchTool().ExecuteAsync(
            f.Ctx(A(("items", new List<object?> { BatchItem("get_environment") }))));
        var payload = Assert.IsType<RunBatchResult>(r.Data);
        Assert.NotNull(payload.AuditNote);
        Assert.Contains("batch summary", payload.AuditNote!, StringComparison.Ordinal);
        Assert.True(payload.ElapsedMs >= 0);
    }

    [Fact]
    public async Task RunBatch_NoRegistry_IsInvalidState()
    {
        var f = new Fixture();
        var r = await new RunBatchTool().ExecuteAsync(f.Ctx(A(("items", new List<object?> { BatchItem("ping") })), withRegistry: false));
        Assert.Equal(ErrorCodes.InvalidState, Code(r));
    }

    [Fact]
    public async Task RunBatch_ItemArgumentsMustBeObject()
    {
        var f = new Fixture();
        f.Registry.Register(new GetEnvironmentTool());
        var bad = new Dictionary<string, object?>(StringComparer.Ordinal) { ["tool"] = "get_environment", ["arguments"] = "nope" };
        var r = await new RunBatchTool().ExecuteAsync(f.Ctx(A(("items", new List<object?> { bad }))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public void RunBatch_DeniedListIsExactlyRunBatchAndReadonly()
    {
        Assert.Contains("run_batch", BatchPolicy.DeniedInBatch);
        Assert.Contains("set_readonly_mode", BatchPolicy.DeniedInBatch);
        Assert.Equal(2, BatchPolicy.DeniedInBatch.Count);
    }

    // ══════════════════════════ diagnose（11）══════════════════════════

    [Fact]
    public async Task Diagnose_UsesHostServiceWhenPresent()
    {
        var f = new Fixture();
        var r = await new DiagnoseTool().ExecuteAsync(f.Ctx());
        Assert.True(r.Success);
        Assert.Equal(1, f.Diagnostics.Calls);
    }

    [Fact]
    public async Task Diagnose_FallsBackToLocalFacts_WhenHostServiceMissing()
    {
        var f = new Fixture(withDiagnostics: false);
        var r = await new DiagnoseTool().ExecuteAsync(f.Ctx());
        Assert.True(r.Success);
        var payload = Assert.IsType<DiagnosticsReport>(r.Data);
        Assert.Equal(0, f.Diagnostics.Calls);
        Assert.Equal(8, payload.Checks.Count);
    }

    [Fact]
    public async Task Diagnose_FallbackReportsClassificationCoverageOk()
    {
        var f = new Fixture(withDiagnostics: false);
        f.Registry.Register(new DiagnoseTool());
        var r = await new DiagnoseTool().ExecuteAsync(f.Ctx());
        var payload = Assert.IsType<DiagnosticsReport>(r.Data);
        var ro = payload.Checks.Single(c => c.Id == "readonly-mode");
        Assert.Equal(DiagnosticsComposer.StatusFail, ro.Status);   // registry=1 ≠ 149 ⇒ 覆盖性核对失败（如实）
    }

    [Fact]
    public async Task Diagnose_FallbackIncludesReadOnlyModeFlag()
    {
        var f = new Fixture(withDiagnostics: false);
        f.ReadOnly.Set(true);
        var r = await new DiagnoseTool().ExecuteAsync(f.Ctx());
        Assert.True(Assert.IsType<DiagnosticsReport>(r.Data).ReadOnlyMode);
    }

    [Fact]
    public void Diagnose_MetadataIsSystemAndNoArcGIS()
    {  // O-D066-04：原 async 无 await（CS1998）⇒ 改同步。

        var tool = new DiagnoseTool();
        Assert.Equal("diagnose", tool.Name);
        Assert.Equal(ToolCategories.System, tool.Metadata.Category);
        Assert.False(tool.Metadata.RequiresArcGIS);
    }

    [Fact]
    public void Diagnose_ComposeAllOk_WhenFactsHealthy()
    {
        var facts = new DiagnosticsFacts
        {
            Port = 6520, ServerListening = true, HostContextLoaded = true,
            LoadedAssemblySha256 = "AA", InstalledPayloadSha256 = "AA",
            GdbLockCount = 0, TransientRootWritable = true, ProtectedRootsConfigured = true,
            ArcGISProVersion = "3.5", ProjectPath = @"D:\p\p.aprx", DefaultGdb = @"D:\p\p.gdb",
            RegisteredToolCount = ToolWriteClassification.Total,
        };
        var report = DiagnosticsComposer.Compose(facts, DateTimeOffset.UtcNow);
        Assert.Equal(DiagnosticsComposer.StatusOk, report.OverallStatus);
    }

    [Fact]
    public void Diagnose_ComposeDetectsPortState()
    {
        var facts = new DiagnosticsFacts { Port = 6520, ServerListening = false, RegisteredToolCount = ToolWriteClassification.Total };
        var report = DiagnosticsComposer.Compose(facts, DateTimeOffset.UtcNow);
        var server = report.Checks.Single(c => c.Id == "server");
        Assert.Equal(DiagnosticsComposer.StatusFail, server.Status);
        Assert.NotNull(server.RecommendedAction);
        Assert.Equal(DiagnosticsComposer.StatusFail, report.OverallStatus);
    }

    [Fact]
    public void Diagnose_ComposeDetectsAssemblyCacheStale()
    {
        var facts = new DiagnosticsFacts
        {
            LoadedAssemblySha256 = "AAAA", InstalledPayloadSha256 = "BBBB",
            RegisteredToolCount = ToolWriteClassification.Total,
        };
        var report = DiagnosticsComposer.Compose(facts, DateTimeOffset.UtcNow);
        var check = report.Checks.Single(c => c.Id == "assembly-identity");
        Assert.Equal(DiagnosticsComposer.StatusFail, check.Status);
        Assert.Contains("STALE", check.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnose_ComposeUnknownNotDisguisedAsOk()
    {
        var facts = new DiagnosticsFacts { RegisteredToolCount = ToolWriteClassification.Total, ProjectPath = @"D:\p\p.aprx" };
        var report = DiagnosticsComposer.Compose(facts, DateTimeOffset.UtcNow);
        Assert.Equal(DiagnosticsComposer.StatusUnknown, report.Checks.Single(c => c.Id == "server").Status);
        Assert.Equal(DiagnosticsComposer.StatusUnknown, report.OverallStatus);
    }

    [Fact]
    public void Diagnose_ComposeWarnsOnGdbLocks()
    {
        var facts = new DiagnosticsFacts
        {
            ServerListening = true, HostContextLoaded = true,
            LoadedAssemblySha256 = "AA", InstalledPayloadSha256 = "AA",
            GdbLockCount = 2, TransientRootWritable = true, ProtectedRootsConfigured = true,
            ArcGISProVersion = "3.5", ProjectPath = "p", DefaultGdb = "g",
            RegisteredToolCount = ToolWriteClassification.Total,
        };
        var report = DiagnosticsComposer.Compose(facts, DateTimeOffset.UtcNow);
        Assert.Equal(DiagnosticsComposer.StatusWarn, report.Checks.Single(c => c.Id == "gdb-locks").Status);
        Assert.Equal(DiagnosticsComposer.StatusWarn, report.OverallStatus);
    }

    [Fact]
    public void Diagnose_ComposeFailsWhenTransientRootUnwritable()
    {
        var facts = new DiagnosticsFacts { TransientRootWritable = false, RegisteredToolCount = ToolWriteClassification.Total };
        var report = DiagnosticsComposer.Compose(facts, DateTimeOffset.UtcNow);
        Assert.Equal(DiagnosticsComposer.StatusFail, report.Checks.Single(c => c.Id == "environment-gate").Status);
    }

    // ══════════════════════════ snapshot_project（12）══════════════════════════

    [Fact]
    public async Task Snapshot_NoHostService_NotImplemented()
    {
        var f = new Fixture(withSnapshots: false);
        Assert.Equal(ErrorCodes.NotImplemented, Code(await new SnapshotProjectTool().ExecuteAsync(f.Ctx())));
    }

    [Fact]
    public async Task Snapshot_RelativeDir_IsInvalid()
    {
        var f = new Fixture();
        var r = await new SnapshotProjectTool().ExecuteAsync(f.Ctx(A(("snapshotDir", @"rel\snap"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Snapshots.Calls);
    }

    [Fact]
    public async Task Snapshot_TempDir_RejectedByG138()
    {
        var f = new Fixture();
        var temp = Path.Combine(Path.GetTempPath(), "snaptest");
        var r = await new SnapshotProjectTool().ExecuteAsync(f.Ctx(A(("snapshotDir", temp))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        Assert.Contains("G-138", r.Errors[0].Message!, StringComparison.Ordinal);
        Assert.Empty(f.Snapshots.Calls);
    }

    [Fact]
    public async Task Snapshot_ProtectedRoot_Rejected()
    {
        var f = new Fixture();
        var r = await new SnapshotProjectTool().ExecuteAsync(f.Ctx(A(("snapshotDir", @"D:\r\TestFixtures\snap"))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        Assert.Empty(f.Snapshots.Calls);
    }

    [Fact]
    public async Task Snapshot_DefaultDir_Delegates()
    {
        var f = new Fixture();
        await new SnapshotProjectTool().ExecuteAsync(f.Ctx());
        Assert.Single(f.Snapshots.Calls);
        Assert.StartsWith("create::", f.Snapshots.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Snapshot_ExplicitDirAndId_Forwarded()
    {
        var f = new Fixture();
        using var tmp = new DDriveTemp();
        var dir = tmp.Dir("snapdir");
        await new SnapshotProjectTool().ExecuteAsync(f.Ctx(A(("snapshotDir", dir), ("snapshotId", "snap-001"))));
        Assert.Single(f.Snapshots.Calls);
        Assert.Contains("snap-001", f.Snapshots.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Snapshot_RealCoreService_CreatesManifestAndIsVerifiable()
    {
        using var ws = TestWorkspace.Create();
        var aprx = ws.CreateOwnedFile("proj.aprx", "APRX-CONTENT-V1");
        var project = new StubProject(aprx);
        var svc = new ProjectSnapshotService(project);
        var dir = ws.CreateOwnedDirectory("snap1");

        var r = await svc.CreateAsync(dir, "s1");
        Assert.True(r.Success, Code(r));
        var payload = r.Data!;
        Assert.True(File.Exists(payload.ManifestPath));
        Assert.False(string.IsNullOrWhiteSpace(payload.AprxSha256));
        Assert.True(payload.TotalBytes > 0);

        var manifest = SnapshotComposer.ReadManifest(dir, out var err);
        Assert.Null(err);
        Assert.True(SnapshotComposer.VerifyIntegrity(dir, manifest).Verified);
    }

    [Fact]
    public async Task Snapshot_RealCoreService_NoProjectFile_InvalidState()
    {
        using var ws = TestWorkspace.Create();
        var project = new StubProject(Path.Combine(ws.RootPath, "missing.aprx"));
        var svc = new ProjectSnapshotService(project);
        var r = await svc.CreateAsync(ws.CreateOwnedDirectory("snap2"), null);
        Assert.Equal(ErrorCodes.InvalidState, Code(r));
    }

    [Fact]
    public async Task Snapshot_RealCoreService_RelativeDir_IsInvalid()
    {
        using var ws = TestWorkspace.Create();
        var aprx = ws.CreateOwnedFile("p2.aprx", "x");
        var svc = new ProjectSnapshotService(new StubProject(aprx));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await svc.CreateAsync(@"rel\dir", null)));
    }

    [Fact]
    public async Task Snapshot_RealCoreService_HostFailure()
    {
        var f = new Fixture();
        f.Snapshots.CreateResult = OperationResult<SnapshotResult>.Fail(ErrorCodes.ExecutionFailed, "boom");
        Assert.Equal(ErrorCodes.ExecutionFailed, Code(await new SnapshotProjectTool().ExecuteAsync(f.Ctx())));
    }

    [Fact]
    public void Snapshot_MetadataProject()
    {
        var tool = new SnapshotProjectTool();
        Assert.Equal("snapshot_project", tool.Name);
        Assert.Equal(ToolCategories.Project, tool.Metadata.Category);
    }

    [Fact]
    public void Snapshot_IsWriteTier()
    {
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("snapshot_project"));
    }

    // ══════════════════════════ restore_snapshot（13）══════════════════════════

    [Fact]
    public async Task Restore_NoHostService_NotImplemented()
    {
        var f = new Fixture(withSnapshots: false);
        var r = await new RestoreSnapshotTool().ExecuteAsync(f.Ctx(A(("snapshotDir", @"D:\d"), ("confirm", true))));
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Restore_MissingDir_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new RestoreSnapshotTool().ExecuteAsync(f.Ctx(A(("confirm", true))))));
    }

    [Fact]
    public async Task Restore_ConfirmOmitted_RefusedNoHost()
    {
        var f = new Fixture();
        var r = await new RestoreSnapshotTool().ExecuteAsync(f.Ctx(A(("snapshotDir", @"D:\d064unit\s"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Snapshots.Calls);
    }

    [Fact]
    public async Task Restore_ConfirmFalse_RefusedNoHost()
    {
        var f = new Fixture();
        var r = await new RestoreSnapshotTool().ExecuteAsync(f.Ctx(A(("snapshotDir", @"D:\d064unit\s"), ("confirm", false))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Snapshots.Calls);
    }

    [Fact]
    public async Task Restore_RelativeDir_IsInvalid()
    {
        var f = new Fixture();
        var r = await new RestoreSnapshotTool().ExecuteAsync(f.Ctx(A(("snapshotDir", @"rel\s"), ("confirm", true))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task Restore_TempDir_RejectedByG138()
    {
        var f = new Fixture();
        var temp = Path.Combine(Path.GetTempPath(), "snaptest");
        var r = await new RestoreSnapshotTool().ExecuteAsync(f.Ctx(A(("snapshotDir", temp), ("confirm", true))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        Assert.Empty(f.Snapshots.Calls);
    }

    [Fact]
    public async Task Restore_ConfirmTrue_DelegatesWithConfirm()
    {
        var f = new Fixture();
        using var tmp = new DDriveTemp();
        var dir = tmp.Dir("restore1");
        await new RestoreSnapshotTool().ExecuteAsync(f.Ctx(A(("snapshotDir", dir), ("confirm", true))));
        Assert.Single(f.Snapshots.Calls);
        Assert.Contains(":True", f.Snapshots.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_CoreService_MissingManifest_Refuses()
    {
        using var ws = TestWorkspace.Create();
        var svc = new ProjectSnapshotService(new StubProject(ws.CreateOwnedFile("a.aprx", "x")));
        var dir = ws.CreateOwnedDirectory("emptysnap");
        var r = await svc.RestoreAsync(dir, confirm: true);
        Assert.Equal(ErrorCodes.InvalidState, Code(r));
        Assert.Contains("manifest", r.Errors[0].Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_CoreService_TamperedSnapshot_Refuses()
    {
        using var ws = TestWorkspace.Create();
        var aprx = ws.CreateOwnedFile("p.aprx", "ORIGINAL");
        var svc = new ProjectSnapshotService(new StubProject(aprx));
        var dir = ws.CreateOwnedDirectory("snap-t");
        Assert.True((await svc.CreateAsync(dir, "t1")).Success);

        // 篡改快照内 APRX 一个字节 ⇒ 哈希不符 ⇒ 必须拒绝恢复。
        var packaged = Path.Combine(dir, SnapshotComposer.AprxRelativePath);
        File.WriteAllText(packaged, "TAMPERED!");

        var r = await svc.RestoreAsync(dir, confirm: true);
        Assert.Equal(ErrorCodes.InvalidState, Code(r));
        Assert.Contains("integrity", r.Errors[0].Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ORIGINAL", File.ReadAllText(aprx));   // 目标未被改动
    }

    [Fact]
    public async Task Restore_CoreService_UnlistedFile_Refuses()
    {
        using var ws = TestWorkspace.Create();
        var aprx = ws.CreateOwnedFile("p.aprx", "V1");
        var svc = new ProjectSnapshotService(new StubProject(aprx));
        var dir = ws.CreateOwnedDirectory("snap-p");
        Assert.True((await svc.CreateAsync(dir, "p1")).Success);
        File.WriteAllText(Path.Combine(dir, "intruder.txt"), "x");
        Assert.Equal(ErrorCodes.InvalidState, Code(await svc.RestoreAsync(dir, confirm: true)));
    }

    [Fact]
    public async Task Restore_CoreService_RoundTrip_ByteIdentical()
    {
        using var ws = TestWorkspace.Create();
        var aprx = ws.CreateOwnedFile("p.aprx", "VERSION-1");
        var svc = new ProjectSnapshotService(new StubProject(aprx));
        var dir = ws.CreateOwnedDirectory("snap-rt");
        var snap = await svc.CreateAsync(dir, "rt");
        Assert.True(snap.Success);

        // 改工程 → 恢复 → 逐字节比对。
        File.WriteAllText(aprx, "VERSION-2-CHANGED");
        var r = await svc.RestoreAsync(dir, confirm: true);
        var payload = Assert.IsType<RestoreSnapshotResult>(r.Data);
        Assert.True(payload.IntegrityVerified);
        Assert.True(payload.ByteIdentical);
        Assert.Equal("VERSION-1", File.ReadAllText(aprx));
        Assert.Equal("VERSION-2-CHANGED", File.ReadAllText(payload.BackupOfPreviousAprx!));
        Assert.Equal(snap.Data!.AprxSha256, payload.AprxSha256Restored);
    }

    [Fact]
    public async Task Restore_CoreService_ConfirmFalse_RefusedAtServiceLayer()
    {
        using var ws = TestWorkspace.Create();
        var svc = new ProjectSnapshotService(new StubProject(ws.CreateOwnedFile("p.aprx", "x")));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await svc.RestoreAsync(ws.CreateOwnedDirectory("s"), confirm: false)));
    }

    [Fact]
    public void Restore_IsWriteTierAndRequiresConfirm()
    {
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("restore_snapshot"));
        var required = (IEnumerable<object?>)new RestoreSnapshotTool().InputSchema["required"]!;
        Assert.Contains("confirm", required);
    }

    private sealed class StubProject : IProjectService
    {
        private readonly string _path;

        public StubProject(string path) => _path = path;

        public Task<OperationResult<ProjectInfo>> GetProjectInfoAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<ProjectInfo>.Ok(new ProjectInfo { Path = _path }));

        public Task<OperationResult<IReadOnlyList<LayoutInfo>>> ListLayoutsAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<LayoutInfo>>.Ok(Array.Empty<LayoutInfo>()));

        public Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListDatabasesAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<DatasetInfo>>.Ok(Array.Empty<DatasetInfo>()));

        public Task<OperationResult<ProjectSaveInfo>> SaveProjectAsync(string? saveAsPath, CancellationToken ct = default)
            => Task.FromResult(OperationResult<ProjectSaveInfo>.Ok(new ProjectSaveInfo
            {
                Path = _path, FileExists = File.Exists(_path), FileSizeBytes = File.Exists(_path) ? new FileInfo(_path).Length : 0,
            }));
    }

    // ══════════════════════════ set_readonly_mode（11）══════════════════════════

    [Fact]
    public async Task ReadOnly_QueryOnly_DoesNotChange()
    {
        var f = new Fixture();
        var r = await new SetReadOnlyModeTool().ExecuteAsync(f.Ctx());
        var payload = Assert.IsType<ReadOnlyModeResult>(r.Data);
        Assert.False(payload.ReadOnlyMode);
        Assert.False(payload.Changed);
        Assert.False(f.ReadOnly.IsReadOnly);
    }

    [Fact]
    public async Task ReadOnly_EnableAndDisable()
    {
        var f = new Fixture();
        var on = await new SetReadOnlyModeTool().ExecuteAsync(f.Ctx(A(("enabled", true))));
        Assert.True(Assert.IsType<ReadOnlyModeResult>(on.Data).ReadOnlyMode);
        Assert.True(f.ReadOnly.IsReadOnly);

        var off = await new SetReadOnlyModeTool().ExecuteAsync(f.Ctx(A(("enabled", false))));
        Assert.False(Assert.IsType<ReadOnlyModeResult>(off.Data).ReadOnlyMode);
        Assert.False(f.ReadOnly.IsReadOnly);
    }

    [Fact]
    public async Task ReadOnly_StringTrueAccepted()
    {
        var f = new Fixture();
        var r = await new SetReadOnlyModeTool().ExecuteAsync(f.Ctx(A(("enabled", "true"))));
        Assert.True(Assert.IsType<ReadOnlyModeResult>(r.Data).ReadOnlyMode);
    }

    [Fact]
    public async Task ReadOnly_InvalidType_IsInvalid()
    {
        var f = new Fixture();
        var r = await new SetReadOnlyModeTool().ExecuteAsync(f.Ctx(A(("enabled", 123))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.False(f.ReadOnly.IsReadOnly);
    }

    [Fact]
    public async Task ReadOnly_UnchangedFlagWhenSameValue()
    {
        var f = new Fixture();
        await new SetReadOnlyModeTool().ExecuteAsync(f.Ctx(A(("enabled", true))));
        var again = await new SetReadOnlyModeTool().ExecuteAsync(f.Ctx(A(("enabled", true))));
        var payload = Assert.IsType<ReadOnlyModeResult>(again.Data);
        Assert.False(payload.Changed);
        Assert.True(payload.Previous);
        Assert.True(payload.ReadOnlyMode);
    }

    [Fact]
    public async Task ReadOnly_PayloadCountsAndScope()
    {
        var f = new Fixture();
        var r = await new SetReadOnlyModeTool().ExecuteAsync(f.Ctx());
        var payload = Assert.IsType<ReadOnlyModeResult>(r.Data);
        // D-086 has 170 production tools while the complete frozen classification ledger remains 285.
        Assert.Equal(158, payload.WriteToolCount);
        Assert.Equal(94, payload.ReadToolCount);
        Assert.Equal(285, payload.WriteToolCount + payload.ReadToolCount + ToolWriteClassification.SessionTools.Count);
        Assert.Contains("session", payload.Scope!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadOnly_NoService_IsInvalidState()
    {
        var f = new Fixture();
        var ctx = new ToolExecutionContext { Host = f.Host, Arguments = new Dictionary<string, object?>() };
        var r = await new SetReadOnlyModeTool().ExecuteAsync(ctx);
        Assert.Equal(ErrorCodes.InvalidState, Code(r));
    }

    [Fact]
    public async Task ReadOnly_RouterRefusesWriteTool_AndAllowsReadTool()
    {
        var f = new Fixture();
        f.Registry.Register(new TruncateTableTool());
        f.Registry.Register(new GetEnvironmentTool());
        var router = new MCPToolRouter(f.Registry, f.Host, new MCPSettings { Port = 6520 }, NullLogger.Instance, null, f.ReadOnly);
        f.ReadOnly.Set(true);

        var write = await router.ExecuteAsync(new MCPToolCall
        {
            Name = "truncate_table",
            Arguments = A(("path", Fc), ("confirm", true)),
        });
        Assert.False(write.Success);
        Assert.Equal(ErrorCodes.PermissionDenied, write.Errors[0].Code);
        Assert.Contains("(read-only)", write.Errors[0].Message!, StringComparison.Ordinal);
        Assert.Empty(f.Schema.Calls);   // 执行前拒绝 ⇒ 宿主未被调用

        var read = await router.ExecuteAsync(new MCPToolCall { Name = "get_environment" });
        Assert.True(read.Success);
    }

    [Fact]
    public async Task ReadOnly_RouterAllowsSessionTierTool()
    {
        var f = new Fixture();
        f.Registry.Register(new ActivateMapTool());
        var router = new MCPToolRouter(f.Registry, f.Host, new MCPSettings { Port = 6520 }, NullLogger.Instance, null, f.ReadOnly);
        f.ReadOnly.Set(true);
        var r = await router.ExecuteAsync(new MCPToolCall { Name = "activate_map", Arguments = A(("mapName", "M")) });
        Assert.True(r.Success);
    }

    [Fact]
    public async Task ReadOnly_RouterWithoutService_NoGate()
    {
        var f = new Fixture();
        f.Registry.Register(new TruncateTableTool());
        var router = new MCPToolRouter(f.Registry, f.Host, new MCPSettings { Port = 6520 }, NullLogger.Instance);
        var r = await router.ExecuteAsync(new MCPToolCall
        {
            Name = "truncate_table",
            Arguments = A(("path", Fc), ("confirm", true)),
        });
        Assert.True(r.Success);
    }

    [Fact]
    public void ReadOnly_RefusalUsesExistingErrorCode()
    {
        var err = ToolWriteClassification.ReadOnlyRefusal("truncate_table");
        Assert.Equal(ErrorCodes.PermissionDenied, err.Code);
        Assert.Contains("(read-only)", err.Message, StringComparison.Ordinal);
    }

    // ══════════════════ 写类分层覆盖性（3）+ 分类抽查（6）══════════════════

    [Fact]
    public void Classification_TotalIs154Plus131PreRegistration()
    {
        // D-086：实际注册 170；分类名册维持 285（十六件此前已预登记，尚余 96 个未来候选）。
        // 未知名仍默认 Write（fail-closed，见 UnknownToolFailsClosed）。
        Assert.Equal(285, ToolWriteClassification.Total);
        Assert.Equal(94, ToolWriteClassification.ReadTools.Count);
        Assert.Equal(33, ToolWriteClassification.SessionTools.Count);
        Assert.Equal(158, ToolWriteClassification.WriteTools.Count);

        var snapshot = ProductionToolContractSnapshot.Tools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var classified = ToolWriteClassification.ReadTools
            .Concat(ToolWriteClassification.SessionTools)
            .Concat(ToolWriteClassification.WriteTools)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(239, snapshot.Count);
        Assert.True(snapshot.IsSubsetOf(classified));
        Assert.Equal(46, classified.Count - snapshot.Count);
    }

    [Fact]
    public void Classification_HasNoDuplicatesAcrossTiers()
    {
        var all = ToolWriteClassification.ReadTools
            .Concat(ToolWriteClassification.SessionTools)
            .Concat(ToolWriteClassification.WriteTools)
            .ToList();
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Classification_MatchesContractSnapshot()
    {
        var snapshot = ProductionToolContractSnapshot.Tools.Select(t => t.Name).ToList();
        Assert.Equal(239, snapshot.Count);
        var classified = ToolWriteClassification.ReadTools
            .Concat(ToolWriteClassification.SessionTools)
            .Concat(ToolWriteClassification.WriteTools)
            .ToHashSet(StringComparer.Ordinal);
        var missing = snapshot.Where(n => !classified.Contains(n)).ToList();
        var extra = classified.Where(n => !snapshot.Contains(n)).ToList();
        Assert.Empty(missing);
        // D-102 + D-104：77 → 61；D-118：+15 件注册后未实现候选 61 → 46；名册总量 285 不变。
        Assert.Equal(46, extra.Count);
    }

    [Fact]
    public void Classification_D083PreRegisteredSpotCheck()
    {
        // D-083 A 组：F03 冻结净生效 131 件的**具名抽检**（按 `prereg-131-derivation.csv` 的 rw 轴落档；预登记 ≠ 实现）
        foreach (var n in new[] { "check_topology_rules", "compare_datasets", "describe_tool_catalog",
                                  "find_identical", "assess_remote_sensing_quality", "validate_plan",
                                  "generate_quality_report", "list_jobs" })
        {
            Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf(n));
        }

        foreach (var n in new[] { "export_design_bundle", "solve_routes" })
        {
            Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf(n));
        }

        foreach (var n in new[] { "cancel_job" })
        {
            Assert.Equal(ToolWriteTier.SessionState, ToolWriteClassification.TierOf(n));
        }
    }

    [Fact]
    public void Classification_UnknownToolFailsClosed()
    {
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("totally_unknown_tool"));
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf(null));
        Assert.True(ToolWriteClassification.RefusedInReadOnly("brand_new_write_tool"));
    }

    [Fact]
    public void Classification_D064NewToolsTiered()
    {
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("create_feature_class"));
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("truncate_table"));
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("run_batch"));
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("add_folder_connection"));
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("set_environment"));
        Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf("diagnose"));
        Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf("set_readonly_mode"));
        Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf("list_folder"));
        Assert.Equal(ToolWriteTier.SessionState, ToolWriteClassification.TierOf("activate_map"));
    }

    [Fact]
    public void Classification_ExistingWriteToolsStillWrite()
    {
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("save_project"));
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("delete_dataset"));
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("export_layout_pdf"));
        Assert.Equal(ToolWriteTier.SessionState, ToolWriteClassification.TierOf("select_layer"));
        Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf("get_layers"));
    }

    [Fact]
    public void D064PathPolicy_TempAndCDriveRejected()
    {
        Assert.True(D064PathPolicy.IsUnderTemp(Path.Combine(Path.GetTempPath(), "x")));
        Assert.False(D064PathPolicy.IsOnDDrive(@"C:\x"));
        Assert.True(D064PathPolicy.IsOnDDrive(@"D:\x"));
        Assert.False(D064PathPolicy.IsOnDDrive(@"d"));
    }

    [Fact]
    public void D064PathPolicy_WritableProbeUsesRealIo()
    {
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("probe");
        Assert.True(D064PathPolicy.TryProbeWrite(dir, out var why));
        Assert.Empty(why);
    }
}
