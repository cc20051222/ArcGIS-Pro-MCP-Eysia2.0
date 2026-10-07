using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>D-073 · 数据文件夹工作流 5 件单测（正例 + 负例；≥8/件）。</summary>
public sealed class FolderWorkflowTests : IDisposable
{
    private readonly string _root;
    private readonly string _src;
    private readonly string _out;
    private readonly MCPToolRegistry _registry = new();

    public FolderWorkflowTests()
    {
        _root = Path.Combine(AppContext.BaseDirectory, "d073-tests-" + Guid.NewGuid().ToString("N"));
        _src = Path.Combine(_root, "src");
        _out = Path.Combine(_root, "out");
        Directory.CreateDirectory(_src);
        Directory.CreateDirectory(_out);
        _registry.Register(new StubTool("stub_ok"));
        _registry.Register(new StubTool("stub_fail", ok: false));
        _registry.Register(new StubTool("add_folder_connection"));
        _registry.Register(new SlowStubTool());
        File.WriteAllText(Path.Combine(_src, "sample.csv"), "code,label\n1,a\n2,b\n");
        File.WriteAllText(Path.Combine(_src, "notes.txt"), "ignored");
        Directory.CreateDirectory(Path.Combine(_src, "sub"));
        File.WriteAllText(Path.Combine(_src, "sub", "deep.csv"), "x\n1\n");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch
        {
            // 测试夹具清理失败不影响结论
        }
    }

    private ToolExecutionContext Ctx(Dictionary<string, object?>? args = null, MCPToolRegistry? registry = null,
        bool readOnly = false, IArcGISHost? host = null)
    {
        var ro = new ReadOnlyModeService();
        if (readOnly)
        {
            ro.Set(true);
        }

        return new ToolExecutionContext
        {
            Host = host ?? new FakeArcGISHost(),
            Arguments = args ?? new Dictionary<string, object?>(),
            Settings = new MCPSettings { Port = 6520 },
            Registry = registry ?? _registry,
            ReadOnly = ro,
        };
    }

    private static string? Code(OperationResult<object?> r) => r.Success ? null : (r.Errors.Count > 0 ? r.Errors[0].Code : null);

    private static Dictionary<string, object?> Data(OperationResult<object?> r)
        => (Dictionary<string, object?>)r.Data!;

    private static List<Dictionary<string, object?>> Items(OperationResult<object?> r)
        => (List<Dictionary<string, object?>>)((Dictionary<string, object?>)r.Data!)["items"]!;

    private sealed class SlowStubTool : IMCPTool
    {
        public string Name => "stub_slow";
        public string Description => "slow stub";
        public IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>();
        public ToolMetadata Metadata => new() { Name = Name, Description = Description, Category = ToolCategories.General };
        public async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        {
            await Task.Delay(400, context.CancellationToken).ConfigureAwait(false);
            return OperationResult<object?>.Ok(new Dictionary<string, object?> { ["outputPath"] = null });
        }
    }

    private sealed class CancelAwareStubTool : IMCPTool
    {
        private readonly TaskCompletionSource<bool> _started;
        public CancelAwareStubTool(TaskCompletionSource<bool> started) => _started = started;
        public string Name => "stub_cancel_aware";
        public string Description => "waits until its execution token is cancelled";
        public IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>();
        public ToolMetadata Metadata => new() { Name = Name, Description = Description, Category = ToolCategories.General };
        public async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        {
            _started.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, context.CancellationToken).ConfigureAwait(false);
            return OperationResult<object?>.Ok(new Dictionary<string, object?>());
        }
    }

    private sealed class StubTool : IMCPTool
    {
        private readonly bool _ok;
        public StubTool(string name, bool ok = true)
        {
            Name = name;
            _ok = ok;
        }

        public string Name { get; }
        public string Description => "stub";
        public IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>();
        public ToolMetadata Metadata => new() { Name = Name, Description = Description, Category = ToolCategories.General };
        public Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
            => Task.FromResult(_ok
                ? OperationResult<object?>.Ok(new Dictionary<string, object?> { ["outputPath"] = null })
                : OperationResult<object?>.Fail(ErrorCodes.GeoprocessingError, "stub failure"));
    }

    // ───────────────────────── scan_data_folder（只读；10 项）─────────────────────────

    [Fact]
    public async Task Scan_MissingFolderPath_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ScanDataFolderTool().ExecuteAsync(Ctx())));

    [Fact]
    public async Task Scan_NonexistentFolder_IsNotFound()
        => Assert.Equal(ErrorCodes.NotFound, Code(await new ScanDataFolderTool().ExecuteAsync(
            Ctx(new() { ["folderPath"] = Path.Combine(_root, "nope") }))));

    [Fact]
    public async Task Scan_ProtectedRoot_IsPathEscapeRejected()
    {
        var probe = Path.Combine(AppContext.BaseDirectory, "TestFixtures");
        Directory.CreateDirectory(probe);
        try
        {
            Assert.Equal(ErrorCodes.PathEscapeRejected, Code(await new ScanDataFolderTool().ExecuteAsync(
                Ctx(new() { ["folderPath"] = probe }))));
        }
        finally
        {
            try { Directory.Delete(probe); } catch { }
        }
    }

    [Fact]
    public async Task Scan_ListsCsvWithRowCount()
    {
        var r = await new ScanDataFolderTool().ExecuteAsync(Ctx(new() { ["folderPath"] = _src }));
        Assert.True(r.Success);
        var items = Items(r);
        var csv = items.First(i => (string)i["name"]! == "sample.csv");
        Assert.Equal("table", csv["kind"]);
        Assert.Equal(2L, csv["featureOrRowCount"]);
        Assert.Equal(true, csv["available"]);
        Assert.Null(csv["skipReason"]);
    }

    [Fact]
    public async Task Scan_SkipsNonDataFiles()
    {
        var r = await new ScanDataFolderTool().ExecuteAsync(Ctx(new() { ["folderPath"] = _src }));
        var items = Items(r);
        Assert.DoesNotContain(items, i => (string)i["name"]! == "notes.txt");
    }

    [Fact]
    public async Task Scan_RespectsMaxDepth()
    {
        var r = await new ScanDataFolderTool().ExecuteAsync(Ctx(new() { ["folderPath"] = _src, ["maxDepth"] = 0 }));
        var items = Items(r);
        Assert.DoesNotContain(items, i => (string)i["name"]! == "deep.csv");
    }

    [Fact]
    public async Task Scan_DescendsWhenDepthAllows()
    {
        var r = await new ScanDataFolderTool().ExecuteAsync(Ctx(new() { ["folderPath"] = _src, ["maxDepth"] = 2 }));
        var items = Items(r);
        Assert.Contains(items, i => (string)i["name"]! == "deep.csv");
    }

    [Fact]
    public async Task Scan_TruncatesAtMaxItems()
    {
        var r = await new ScanDataFolderTool().ExecuteAsync(Ctx(new() { ["folderPath"] = _src, ["maxItems"] = 1 }));
        var data = Data(r);
        Assert.Equal(true, data["truncated"]);
        Assert.Single((List<Dictionary<string, object?>>)data["items"]!);
    }

    [Fact]
    public async Task Scan_IsReadOnlyAndLeavesSourceUntouched()
    {
        var before = File.ReadAllText(Path.Combine(_src, "sample.csv"));
        var r = await new ScanDataFolderTool().ExecuteAsync(Ctx(new() { ["folderPath"] = _src }));
        var data = Data(r);
        Assert.Equal(true, data["readOnly"]);
        Assert.Equal(true, data["sourceUntouched"]);
        Assert.Equal(before, File.ReadAllText(Path.Combine(_src, "sample.csv")));
    }

    [Fact]
    public async Task Scan_UnavailableHostItemsAreListedWithReason()
    {
        // 无 Host ⇒ 栅格项应如实列 skipReason，而非静默丢弃
        File.WriteAllBytes(Path.Combine(_src, "r.tif"), new byte[] { 0x49, 0x49, 0x2A, 0x00 });
        var ctx = new ToolExecutionContext
        {
            Host = null,
            Arguments = new Dictionary<string, object?> { ["folderPath"] = _src },
            Settings = new MCPSettings { Port = 6520 },
            Registry = new MCPToolRegistry(),
            ReadOnly = new ReadOnlyModeService(),
        };
        var r = await new ScanDataFolderTool().ExecuteAsync(ctx);
        var items = Items(r);
        var tif = items.First(i => (string)i["name"]! == "r.tif");
        Assert.Equal(false, tif["available"]);
        Assert.NotNull(tif["skipReason"]);
    }

    // ───────────────────────── load_folder_data（写；11 项）─────────────────────────

    [Fact]
    public async Task Load_MissingArgs_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new LoadFolderDataTool().ExecuteAsync(Ctx())));

    [Fact]
    public async Task Load_BadOutputMode_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new LoadFolderDataTool().ExecuteAsync(
            Ctx(new() { ["folderPath"] = _src, ["outputMode"] = "nope" }))));

    [Fact]
    public async Task Load_CopyToGdbWithoutTarget_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new LoadFolderDataTool().ExecuteAsync(
            Ctx(new() { ["folderPath"] = _src, ["outputMode"] = "copy_to_gdb" }))));

    [Fact]
    public async Task Load_NonexistentFolder_IsNotFound()
        => Assert.Equal(ErrorCodes.NotFound, Code(await new LoadFolderDataTool().ExecuteAsync(
            Ctx(new() { ["folderPath"] = Path.Combine(_root, "nope"), ["outputMode"] = "register_only" }))));

    [Fact]
    public async Task Load_ProtectedFolder_IsPathEscapeRejected()
    {
        var probe = Path.Combine(AppContext.BaseDirectory, "TestFixtures");
        Directory.CreateDirectory(probe);
        try
        {
            Assert.Equal(ErrorCodes.PathEscapeRejected, Code(await new LoadFolderDataTool().ExecuteAsync(
                Ctx(new() { ["folderPath"] = probe, ["outputMode"] = "register_only" }))));
        }
        finally
        {
            try { Directory.Delete(probe); } catch { }
        }
    }

    [Fact]
    public async Task Load_TempTargetGdb_IsRefused()
        => Assert.Equal(ErrorCodes.PathEscapeRejected, Code(await new LoadFolderDataTool().ExecuteAsync(
            Ctx(new()
            {
                ["folderPath"] = _src,
                ["outputMode"] = "copy_to_gdb",
                ["targetGdb"] = Path.Combine(Path.GetTempPath(), "bad.gdb"),
            }))));

    [Fact]
    public async Task Load_DefaultDryRun_ReturnsPlanWithoutExecuting()
    {
        var r = await new LoadFolderDataTool().ExecuteAsync(Ctx(new()
        {
            ["folderPath"] = _src,
            ["outputMode"] = "register_only",
        }));
        Assert.True(r.Success);
        var data = Data(r);
        Assert.Equal(true, data["dryRun"]);
        Assert.Equal(false, data["executed"]);
        Assert.NotNull(data["plan"]);
    }

    [Fact]
    public async Task Load_DryRunFalseWithoutConfirm_IsPermissionDenied()
        => Assert.Equal(ErrorCodes.PermissionDenied, Code(await new LoadFolderDataTool().ExecuteAsync(
            Ctx(new()
            {
                ["folderPath"] = _src,
                ["outputMode"] = "register_only",
                ["dryRun"] = false,
            }))));

    [Fact]
    public async Task Load_ReadOnlyMode_IsPermissionDenied()
        => Assert.Equal(ErrorCodes.PermissionDenied, Code(await new LoadFolderDataTool().ExecuteAsync(
            Ctx(new()
            {
                ["folderPath"] = _src,
                ["outputMode"] = "register_only",
                ["dryRun"] = false,
                ["confirm"] = true,
            }, readOnly: true))));

    [Fact]
    public async Task Load_NoMatchingItems_IsNotFound()
        => Assert.Equal(ErrorCodes.NotFound, Code(await new LoadFolderDataTool().ExecuteAsync(
            Ctx(new()
            {
                ["folderPath"] = _src,
                ["outputMode"] = "register_only",
                ["items"] = new List<object?> { "ghost.shp" },
            }))));

    [Fact]
    public async Task Load_RegisterOnly_ExecutesViaRegistryAndReportsPerItem()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new StubTool("add_folder_connection"));
        var r = await new LoadFolderDataTool().ExecuteAsync(Ctx(new()
        {
            ["folderPath"] = _src,
            ["outputMode"] = "register_only",
            ["items"] = new List<object?> { "sample.csv" },
            ["dryRun"] = false,
            ["confirm"] = true,
        }, registry));
        Assert.True(r.Success);
        var data = Data(r);
        Assert.Equal(false, data["dryRun"]);
        Assert.Equal(true, data["executed"]);
        Assert.Equal(1, data["ok"]);
        Assert.Equal(true, data["sourceUntouched"]);
    }

    // ───────────────────────── apply_processing_plan（写/分片；12 项）─────────────────────────

    private Dictionary<string, object?> PlanArgs(string outPath, string tool = "stub_ok", bool dryRun = true,
        bool confirm = false, bool continueOnError = false, MCPToolRegistry? registry = null, bool readOnly = false,
        string? onConflict = null, string? jobId = null)
    {
        var reg = registry ?? _registry;
        var args = new Dictionary<string, object?>
        {
            ["plan"] = new Dictionary<string, object?>
            {
                ["steps"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["tool"] = tool, ["args"] = new Dictionary<string, object?>() },
                },
            },
            ["targets"] = new List<object?> { Path.Combine(_src, "sample.csv") },
            ["outputLocation"] = outPath,
            ["dryRun"] = dryRun,
            ["confirm"] = confirm,
            ["continueOnError"] = continueOnError,
        };
        if (onConflict is not null)
        {
            args["onConflict"] = onConflict;
        }

        if (jobId is not null)
        {
            args["jobId"] = jobId;
        }

        return args;
    }

    [Fact]
    public async Task Plan_MissingArgs_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ApplyProcessingPlanTool().ExecuteAsync(Ctx())));

    [Fact]
    public async Task Plan_EmptySteps_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(new()
            {
                ["plan"] = new Dictionary<string, object?> { ["steps"] = new List<object?>() },
                ["targets"] = new List<object?> { "x" },
                ["outputLocation"] = _out,
            }))));

    [Fact]
    public async Task Plan_DeniedNestedRunBatch_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, tool: "run_batch")))));

    [Fact]
    public async Task Plan_DeniedSelfReference_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, tool: "apply_processing_plan")))));

    [Fact]
    public async Task Plan_UnregisteredTool_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, tool: "not_registered_tool")))));

    [Fact]
    public async Task Plan_ProtectedOutputLocation_IsPathEscapeRejected()
        => Assert.Equal(ErrorCodes.PathEscapeRejected, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(Path.Combine(AppContext.BaseDirectory, "TestFixtures"))))));

    [Fact]
    public async Task Plan_TempOutputLocation_IsRefused()
        => Assert.Equal(ErrorCodes.PathEscapeRejected, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(Path.GetTempPath())))));

    [Fact]
    public async Task Plan_DefaultDryRun_ReturnsStructuredPlan()
    {
        var r = await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(PlanArgs(_out)));
        Assert.True(r.Success);
        var data = Data(r);
        Assert.Equal(true, data["dryRun"]);
        Assert.Equal(false, data["executed"]);
        var plan = (Dictionary<string, object?>)data["plan"]!;
        Assert.Equal(1, plan["workItemCount"]);
        Assert.Equal(_out, plan["outputLocation"]);
    }

    [Fact]
    public async Task Plan_DryRunFalseWithoutConfirm_IsPermissionDenied()
        => Assert.Equal(ErrorCodes.PermissionDenied, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false)))));

    [Fact]
    public async Task Plan_ReadOnlyMode_IsPermissionDenied()
        => Assert.Equal(ErrorCodes.PermissionDenied, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, confirm: true), readOnly: true))));

    [Fact]
    public async Task Plan_ExecutesRegisteredStepsAndReportsOk()
    {
        var r = await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(PlanArgs(_out, dryRun: false, confirm: true)));
        Assert.True(r.Success);
        var data = Data(r);
        Assert.Equal(true, data["executed"]);
        Assert.Equal(1, data["total"]);
        Assert.Equal(1, data["ok"]);
        Assert.Equal(0, data["failed"]);
        Assert.NotNull(data["jobId"]);
        Assert.NotNull(data["checkpoint"]);
    }

    [Fact]
    public async Task Plan_FailedStepIsRecordedWithErrorCode()
    {
        var r = await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(PlanArgs(_out, tool: "stub_fail", dryRun: false, confirm: true)));
        Assert.True(r.Success);
        var data = Data(r);
        Assert.Equal(1, data["failed"]);
        var items = (List<Dictionary<string, object?>>)data["items"]!;
        Assert.Equal(ErrorCodes.GeoprocessingError, items[0]["errorCode"]);
        Assert.Equal("failed", items[0]["state"]);
    }

    // ─────────── D-077 并发/确定性（≥4）───────────

    [Fact]
    public async Task Plan_ConcurrentSameJobId_SecondCallIsRefused()
    {
        var first = new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, tool: "stub_slow", dryRun: false, confirm: true, jobId: "race-1")));
        await Task.Delay(120);
        var second = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, tool: "stub_slow", dryRun: false, confirm: true, jobId: "race-1")));
        Assert.Equal(ErrorCodes.InvalidState, Code(second));
        await first;
    }

    [Fact]
    public async Task Plan_DifferentJobIds_AreIsolated()
    {
        var a = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: "iso-a")));
        var b = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: "iso-b")));
        Assert.True(a.Success && b.Success);
        Assert.Equal(1, Data(a)["ok"]);
        Assert.Equal(1, Data(b)["ok"]);
        Assert.Equal(0, Data(b)["resumedSkips"]);   // 不同 jobId ⇒ 不共享状态
    }

    [Fact]
    public async Task Plan_Resume_DoesNotRegressCompletedItems()
    {
        await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: "regress-1")));
        var again = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: "regress-1")));
        Assert.True(again.Success);
        Assert.Equal(1, Data(again)["ok"]);
        Assert.Equal(1, Data(again)["resumedSkips"]);
        Assert.Equal(0, Data(again)["failed"]);
    }

    [Fact]
    public async Task Plan_CheckpointWrite_LeavesNoTmpResidue()
    {
        var r = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: "atomic-1")));
        var ck = (string?)Data(r)["checkpoint"];
        Assert.NotNull(ck);
        Assert.True(File.Exists(ck));
        Assert.False(File.Exists(ck + ".tmp"));   // 原子替换后无 .tmp 残留
    }

    [Fact]
    public async Task Plan_Resume_SkipsAlreadyCompletedItems()
    {
        // D-075：按断点续跑 ⇒ 已完成项不重跑（resumedSkips > 0）
        var first = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: "resume-1")));
        Assert.True(first.Success);
        Assert.Equal(1, Data(first)["ok"]);
        var second = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: "resume-1")));
        Assert.True(second.Success);
        var d = Data(second);
        Assert.Equal(1, d["total"]);
        Assert.Equal(1, d["ok"]);
        Assert.Equal(1, d["resumedSkips"]);
    }

    [Fact]
    public async Task D084_CancelJobCooperativelyStopsRunningPlanAndReportsCancelled()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = _registry;
        registry.Register(new CancelAwareStubTool(started));
        var jobId = "d084-cancel-" + Guid.NewGuid().ToString("N");
        var running = new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, tool: "stub_cancel_aware", dryRun: false, confirm: true, jobId: jobId), registry));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var cancelled = await new CancelJobTool().ExecuteAsync(Ctx(new()
        {
            ["jobId"] = jobId,
            ["reason"] = "unit cancellation path",
            ["waitMs"] = 5000,
        }, registry));
        Assert.True(cancelled.Success);
        var cancelData = Data(cancelled);
        Assert.Equal(true, cancelData["accepted"]);
        Assert.Equal("cancelled", cancelData["state"]);

        var completed = await running;
        Assert.True(completed.Success);
        var job = Data(completed);
        Assert.Equal(true, job["cancelled"]);
        Assert.Equal(1, job["pending"]);
        Assert.Equal(jobId, job["resumeToken"]);
    }

    [Fact]
    public async Task Plan_ContinueOnErrorTrue_ContinuesAfterFailure()
    {
        var reg = _registry;
        var args = PlanArgs(_out, tool: "stub_fail", dryRun: false, confirm: true, continueOnError: true, jobId: "coe");
        var r = await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(args, reg));
        Assert.True(r.Success);
        var d = Data(r);
        Assert.Equal(1, d["failed"]);
        Assert.Equal(0, d["skipped"]);   // continueOnError=true ⇒ 其余项未被 abort 跳过
    }

    [Fact]
    public async Task Plan_InvalidOnConflict_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, onConflict: "banana")))));

    [Fact]
    public async Task Plan_OnConflictOverwriteWithoutConfirm_IsPermissionDenied()
        => Assert.Equal(ErrorCodes.PermissionDenied, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, onConflict: "overwrite")))));

    [Fact]
    public async Task Plan_OnConflictOverwriteWithConfirm_Executes()
    {
        var r = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, confirm: true, onConflict: "overwrite")));
        Assert.True(r.Success);
        Assert.Equal(true, Data(r)["executed"]);
    }

    [Fact]
    public async Task Load_InPlaceRequested_IsRefused()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new LoadFolderDataTool().ExecuteAsync(
            Ctx(new()
            {
                ["folderPath"] = _src,
                ["outputMode"] = "into_map",
                ["inPlace"] = true,
                ["dryRun"] = false,
                ["confirm"] = true,
            }))));

    [Fact]
    public async Task Plan_InPlaceRequested_IsRefused()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(new()
            {
                ["plan"] = new Dictionary<string, object?>
                {
                    ["steps"] = new List<object?>
                    {
                        new Dictionary<string, object?> { ["tool"] = "stub_ok", ["args"] = new Dictionary<string, object?>() },
                    },
                },
                ["targets"] = new List<object?> { Path.Combine(_src, "sample.csv") },
                ["outputLocation"] = _out,
                ["inPlace"] = true,
            }))));

    [Fact]
    public async Task Plan_WindowsTempOutput_IsRefusedByG138()
        => Assert.Equal(ErrorCodes.PathEscapeRejected, Code(await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(@"C:\Windows\Temp\d073")))));

    [Fact]
    public async Task Load_AppDataLocalTempTargetGdb_IsRefusedByG138()
        => Assert.Equal(ErrorCodes.PathEscapeRejected, Code(await new LoadFolderDataTool().ExecuteAsync(
            Ctx(new()
            {
                ["folderPath"] = _src,
                ["outputMode"] = "copy_to_gdb",
                ["targetGdb"] = @"C:\Users\<user>\AppData\Local\Temp\d073.gdb",
            }))));

    // ───────────────────────── get_job_status / get_job_report（只读；8 项）─────────────────────────

    [Fact]
    public async Task Status_MissingJobId_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new GetJobStatusTool().ExecuteAsync(Ctx())));

    [Fact]
    public async Task Status_UnknownJobId_IsNotFound()
        => Assert.Equal(ErrorCodes.NotFound, Code(await new GetJobStatusTool().ExecuteAsync(
            Ctx(new() { ["jobId"] = "no-such-job" }))));

    [Fact]
    public async Task Status_AfterPlanRun_ReportsProgressAndCheckpoint()
    {
        await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: "st")));
        var r = await new GetJobStatusTool().ExecuteAsync(Ctx(new() { ["jobId"] = "st" }));
        Assert.True(r.Success);
        var data = Data(r);
        Assert.Equal("st", data["jobId"]);
        Assert.Equal(1, data["total"]);
        Assert.Equal(1, data["done"]);
        Assert.Equal(0, data["remaining"]);
        Assert.NotNull(data["checkpoint"]);
    }

    [Fact]
    public void Status_RequiresNoArcGISHost()
    {
        Assert.False(new GetJobStatusTool().Metadata.RequiresArcGIS);
    }

    [Fact]
    public async Task Report_MissingJobId_IsInvalid()
        => Assert.Equal(ErrorCodes.InvalidArgument, Code(await new GetJobReportTool().ExecuteAsync(Ctx())));

    [Fact]
    public async Task Report_UnknownJobId_IsNotFound()
        => Assert.Equal(ErrorCodes.NotFound, Code(await new GetJobReportTool().ExecuteAsync(
            Ctx(new() { ["jobId"] = "no-such-job" }))));

    [Fact]
    public async Task Report_AfterPlanRun_ReportsProcessedSkippedFailedAndAuditLink()
    {
        await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: "rp")));
        var r = await new GetJobReportTool().ExecuteAsync(Ctx(new() { ["jobId"] = "rp" }));
        Assert.True(r.Success);
        var data = Data(r);
        Assert.Equal("rp", data["auditJobId"]);
        Assert.Single((List<Dictionary<string, object?>>)data["processed"]!);
        Assert.NotNull(data["summary"]);
        Assert.NotNull(data["unverified"]);
    }

    [Fact]
    public async Task Report_FailedItemsAreNotSilenced()
    {
        await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(PlanArgs(_out, tool: "stub_fail", dryRun: false, confirm: true, jobId: "rpf")));
        var r = await new GetJobReportTool().ExecuteAsync(Ctx(new() { ["jobId"] = "rpf" }));
        var data = Data(r);
        var failed = (List<Dictionary<string, object?>>)data["failed"]!;
        Assert.Single(failed);
        Assert.NotNull(failed[0]["reason"]);
        Assert.NotNull(failed[0]["errorCode"]);
    }

    // ───────────────────────── 契约面（分类/快照；4 项）─────────────────────────

    [Theory]
    [InlineData("scan_data_folder", ToolCategories.DataManagement)]
    [InlineData("load_folder_data", ToolCategories.Project)]
    [InlineData("apply_processing_plan", ToolCategories.General)]
    [InlineData("get_job_status", ToolCategories.System)]
    [InlineData("get_job_report", ToolCategories.System)]
    public void NewTools_AreDeclaredInContractSnapshotWithCategory(string name, string category)
    {
        var contract = ProductionToolContractSnapshot.Tools.Single(t => t.Name == name);
        Assert.Equal(category, contract.Category);
        Assert.Equal(ExecutionTypes.Native, contract.ExecutionType);
    }

    [Theory]
    [InlineData("scan_data_folder", ToolWriteTier.Read)]
    [InlineData("get_job_status", ToolWriteTier.Read)]
    [InlineData("get_job_report", ToolWriteTier.Read)]
    [InlineData("load_folder_data", ToolWriteTier.Write)]
    [InlineData("apply_processing_plan", ToolWriteTier.Write)]
    public void NewTools_AreClassifiedForReadOnlyMode(string name, ToolWriteTier tier)
    {
        Assert.Equal(tier, ToolWriteClassification.TierOf(name));
    }

    [Fact]
    public void NewReadOnlyTools_DoNotRequireArcGIS()
    {
        Assert.False(new GetJobStatusTool().Metadata.RequiresArcGIS);
        Assert.False(new GetJobReportTool().Metadata.RequiresArcGIS);
    }

    // ═══════════════ D-079 · A 组（计划回显完备化／GDB 行数／members 摘要）═══════════════

    private static OperationResult<IReadOnlyList<DatasetMemberInfo>> Members(
        params (string Name, string Type, long? Rows)[] listed)
        => OperationResult<IReadOnlyList<DatasetMemberInfo>>.Ok(listed.Select(m => new DatasetMemberInfo
        {
            Name = m.Name,
            Path = "container\\" + m.Name,
            Type = m.Type,
            RowCount = m.Rows,
            CountUnavailableReason = m.Rows.HasValue ? null : "not-row-addressable:" + m.Type,
        }).ToList());

    private static Dictionary<string, object?>? PlanOf(OperationResult<object?> r)
        => (Dictionary<string, object?>?)Data(r)["plan"];

    private static List<Dictionary<string, object?>> AmbiguitiesOf(Dictionary<string, object?>? plan)
        => (List<Dictionary<string, object?>>)plan!["ambiguities"]!;

    private static Dictionary<string, object?> DefaultsOf(Dictionary<string, object?>? plan)
        => (Dictionary<string, object?>)plan!["defaults"]!;

    private static Dictionary<string, object?> DefaultEntry(Dictionary<string, object?> defaults, string key)
        => (Dictionary<string, object?>)defaults[key]!;

    [Fact]
    public async Task Scan_GdbContainer_SumsTopLevelRowCounts()
    {
        Directory.CreateDirectory(Path.Combine(_src, "mixed.gdb"));
        var host = new FakeArcGISHost
        {
            MembersHandler = (_, _) => Members(("fc_mixed", "FeatureClass", 4), ("tbl_mixed", "Table", 2)),
        };

        var r = await new ScanDataFolderTool().ExecuteAsync(Ctx(new() { ["folderPath"] = _src }, host: host));

        Assert.True(r.Success);
        var gdb = Items(r).Single(i => (string?)i["kind"] == "dataset");
        Assert.Equal(6L, gdb["featureOrRowCount"]);
        Assert.Null(gdb["countUnavailableReason"]);
        Assert.Equal("sum-of-top-level-featureclasses-and-tables", gdb["countScope"]);
        Assert.Equal(2, gdb["memberCount"]);
        Assert.Equal(false, gdb["membersTruncated"]);
        var members = (List<Dictionary<string, object?>>)gdb["members"]!;
        Assert.Equal(new[] { "fc_mixed", "tbl_mixed" }, members.Select(m => (string?)m["name"]));
        Assert.Equal(4L, members[0]["featureOrRowCount"]);
    }

    [Fact]
    public async Task Scan_GdbContainer_CapsMembersAtFiftyAndRefusesToReportPartialSum()
    {
        Directory.CreateDirectory(Path.Combine(_src, "big.gdb"));
        var requestedCap = 0;
        var host = new FakeArcGISHost
        {
            MembersHandler = (_, cap) =>
            {
                requestedCap = cap;
                return Members(Enumerable.Range(0, cap).Select(i => ($"fc{i:00}", "FeatureClass", (long?)i)).ToArray());
            },
        };

        var r = await new ScanDataFolderTool().ExecuteAsync(Ctx(new() { ["folderPath"] = _src }, host: host));

        var gdb = Items(r).Single(i => (string?)i["kind"] == "dataset");
        Assert.Equal(51, requestedCap);                       // 上限 50 ＋ 1 条用于判定"是否还有更多"
        Assert.Equal(50, gdb["memberCount"]);                 // 摘要本身截断在 50
        Assert.Equal(true, gdb["membersTruncated"]);
        Assert.Equal(50, ((List<Dictionary<string, object?>>)gdb["members"]!).Count);
        Assert.Null(gdb["featureOrRowCount"]);                // 截断 ⇒ 聚合必低估 ⇒ 宁给原因，不给半个真相
        Assert.Contains("truncated:more-than-50-members", (string?)gdb["countUnavailableReason"]);
    }

    [Fact]
    public async Task Scan_GdbContainer_WithoutEnumerationCapability_GivesReasonNotBareNull()
    {
        Directory.CreateDirectory(Path.Combine(_src, "plain.gdb"));

        var r = await new ScanDataFolderTool().ExecuteAsync(Ctx(new() { ["folderPath"] = _src }));   // 默认宿主 ⇒ NOT_IMPLEMENTED

        var gdb = Items(r).Single(i => (string?)i["kind"] == "dataset");
        Assert.Null(gdb["featureOrRowCount"]);
        Assert.Contains(ErrorCodes.NotImplemented, (string?)gdb["countUnavailableReason"]);
        Assert.Equal(true, gdb["available"]);                 // 计数不可得 ≠ 不可处理
        Assert.Null(gdb["skipReason"]);
    }

    [Fact]
    public async Task Scan_GdbContainer_ContainerOnlyMembersAreNotCountedAsRows()
    {
        Directory.CreateDirectory(Path.Combine(_src, "fds.gdb"));
        var host = new FakeArcGISHost
        {
            MembersHandler = (_, _) => Members(("fd_roads", "FeatureDataset", null), ("img", "RasterDataset", null)),
        };

        var r = await new ScanDataFolderTool().ExecuteAsync(Ctx(new() { ["folderPath"] = _src }, host: host));

        var gdb = Items(r).Single(i => (string?)i["kind"] == "dataset");
        Assert.Null(gdb["featureOrRowCount"]);
        Assert.Contains("no-row-addressable-member", (string?)gdb["countUnavailableReason"]);
        Assert.Equal(2, gdb["memberCount"]);                  // 子项摘要仍在（限深 1 层，不递归其内容）
    }

    [Fact]
    public async Task Scan_EveryCountlessItem_CarriesAReasonAndEveryCountedItemACarriesABasis()
    {
        File.WriteAllText(Path.Combine(_src, "orphan.shp"), "shapeless");   // 无同名 .dbf ⇒ 行数不可得

        var r = await new ScanDataFolderTool().ExecuteAsync(Ctx(new() { ["folderPath"] = _src }));

        var items = Items(r);
        Assert.NotEmpty(items);
        foreach (var item in items)
        {
            if (item["featureOrRowCount"] is null)
            {
                Assert.False(string.IsNullOrWhiteSpace(item["countUnavailableReason"] as string),
                    $"{item["name"]}: 裸 null 无原因（O-D073-02 违例）");
            }
            else
            {
                Assert.Null(item["countUnavailableReason"]);
                Assert.False(string.IsNullOrWhiteSpace(item["countBasis"] as string), $"{item["name"]}: 有计数但无口径");
            }
        }

        Assert.Contains(items, i => (string?)i["name"] == "orphan.shp" && (string?)i["countUnavailableReason"] == "dbf-missing");
    }

    [Fact]
    public async Task Plan_DryRun_EchoesThreeAmbiguityKindsAndDefaults_WithoutExecuting()
    {
        var r = await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(PlanArgs(_out)));

        Assert.True(r.Success);
        Assert.Equal(false, Data(r)["executed"]);             // 「未落计划不得执行」不回退
        var plan = PlanOf(r);
        var kinds = AmbiguitiesOf(plan).Select(a => (string?)a["kind"]).ToList();
        Assert.Contains("targetLocation", kinds);
        Assert.Contains("outputNaming", kinds);
        Assert.Contains("overwritePolicy", kinds);
        Assert.All(AmbiguitiesOf(plan), a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a["detail"] as string));
            Assert.False(string.IsNullOrWhiteSpace(a["resolution"] as string));
        });

        var defaults = DefaultsOf(plan);
        Assert.Equal("fail", DefaultEntry(defaults, "onConflict")["value"]);
        Assert.Equal("default", DefaultEntry(defaults, "onConflict")["source"]);
        Assert.Equal("user", DefaultEntry(defaults, "dryRun")["source"]);      // PlanArgs 显式给了 dryRun
        Assert.Equal("default", DefaultEntry(defaults, "jobId")["source"]);
        Assert.Equal("(原样透传)", DefaultEntry(defaults, "plan.steps[].args")["value"]);
    }

    [Fact]
    public async Task Plan_DryRun_WhenOverwritePolicyIsGiven_DefaultsSayUser()
    {
        var r = await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(PlanArgs(_out, onConflict: "skip")));

        var defaults = DefaultsOf(PlanOf(r));
        Assert.Equal("skip", DefaultEntry(defaults, "onConflict")["value"]);
        Assert.Equal("user", DefaultEntry(defaults, "onConflict")["source"]);
        var overwrite = AmbiguitiesOf(PlanOf(r)).Single(a => (string?)a["kind"] == "overwritePolicy");
        Assert.Contains("已显式给定", (string?)overwrite["detail"]);
    }

    [Theory]
    [InlineData("stub_needs_out")]      // required 为 List<object?>（Native 工具写法）
    [InlineData("stub_needs_out_arr")]  // required 为 string[]（GP 工具写法，如 clip）
    public async Task Plan_DryRun_DisclosesMissingRequiredOutputArgument(string toolName)
    {
        _registry.Register(new StubRequiresOutputTool(arrayForm: false));
        _registry.Register(new StubRequiresOutputTool(arrayForm: true));
        var args = new Dictionary<string, object?>
        {
            ["plan"] = new Dictionary<string, object?>
            {
                ["steps"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["tool"] = toolName, ["args"] = new Dictionary<string, object?>() },
                },
            },
            ["targets"] = new List<object?> { Path.Combine(_src, "sample.csv") },
            ["outputLocation"] = _out,
            ["dryRun"] = true,
        };

        var r = await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(args));

        var target = AmbiguitiesOf(PlanOf(r)).Single(a => (string?)a["kind"] == "targetLocation");
        Assert.Contains("out_feature_class", (string?)target["detail"]);
        Assert.Contains("不猜测落点", (string?)target["resolution"]);
    }

    [Fact]
    public async Task Plan_DryRun_DisclosesTargetBaseNameCollisions()
    {
        Directory.CreateDirectory(Path.Combine(_src, "dup"));
        File.WriteAllText(Path.Combine(_src, "dup", "sample.csv"), "code\n1\n");   // 与 _src\sample.csv 同基名
        var args = new Dictionary<string, object?>
        {
            ["plan"] = new Dictionary<string, object?>
            {
                ["steps"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["tool"] = "stub_ok", ["args"] = new Dictionary<string, object?>() },
                },
            },
            ["targets"] = new List<object?> { Path.Combine(_src, "sample.csv"), Path.Combine(_src, "dup", "sample.csv") },
            ["outputLocation"] = _out,
            ["dryRun"] = true,
        };

        var r = await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(args));

        var naming = AmbiguitiesOf(PlanOf(r)).Single(a => (string?)a["kind"] == "outputNaming");
        Assert.Contains("sample", (string?)naming["detail"]);
        Assert.Equal(2, PlanOf(r)!["workItemCount"]);
    }

    [Fact]
    public async Task Load_DryRun_EchoesAmbiguitiesAndDefaults()
    {
        var r = await new LoadFolderDataTool().ExecuteAsync(
            Ctx(new() { ["folderPath"] = _src, ["outputMode"] = "into_map", ["dryRun"] = true }));

        Assert.True(r.Success);
        Assert.Equal(false, Data(r)["executed"]);
        var plan = PlanOf(r);
        var amb = AmbiguitiesOf(plan);
        Assert.Equal(3, amb.Count);                           // 三类歧义恒常逐条明示
        Assert.Contains(amb, a => (string?)a["kind"] == "targetLocation" && ((string?)a["detail"])!.Contains("mapName"));
        Assert.Contains(amb, a => (string?)a["kind"] == "overwritePolicy" && ((string?)a["resolution"])!.Contains("绝不覆盖"));

        var defaults = DefaultsOf(plan);
        Assert.Equal("(current map)", DefaultEntry(defaults, "mapName")["value"]);
        Assert.Equal("default", DefaultEntry(defaults, "mapName")["source"]);
        Assert.Equal("(全部可处理项)", DefaultEntry(defaults, "items")["value"]);
        Assert.Equal(false, DefaultEntry(defaults, "confirm")["value"]);
        Assert.Equal("default", DefaultEntry(defaults, "confirm")["source"]);   // 缺省拒：未给 confirm ⇒ source=default
    }

    // ═══════════════════ D-079 · B 组（O-D077-01 断点写放大修复）═══════════════════

    [Fact]
    public async Task Plan_Journal_IsTheLiveCarrier_AndGetJobStatusSemanticsUnchanged()
    {
        var r = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: "d079-journal-1")));

        Assert.True(r.Success);
        var ck = (string?)Data(r)["checkpoint"];
        Assert.NotNull(ck);
        Assert.EndsWith(".jsonl", ck!);
        Assert.True(File.Exists(ck));

        var s = await new GetJobStatusTool().ExecuteAsync(Ctx(new() { ["jobId"] = "d079-journal-1" }));
        var d = Data(s);
        Assert.Equal(1, d["total"]);
        Assert.Equal(1, d["done"]);
        Assert.Equal(0, d["failed"]);
        Assert.Equal(0, d["pending"]);
        Assert.Equal(ck, d["checkpoint"]);
    }

    [Fact]
    public async Task Plan_Journal_AppendsOnlyWhatChanged_NoFullRewrite()
    {
        const int N = 40;
        var dir = Path.Combine(_root, "append-drill");
        Directory.CreateDirectory(dir);
        var targets = new List<object?>();
        for (var i = 0; i < N; i++)
        {
            var p = Path.Combine(dir, $"t{i:00}.csv");
            File.WriteAllText(p, "code\n1\n");
            targets.Add(p);
        }

        var jobId = "d079-append-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var args = new Dictionary<string, object?>
        {
            ["plan"] = new Dictionary<string, object?>
            {
                ["steps"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["tool"] = "stub_ok", ["args"] = new Dictionary<string, object?>() },
                },
            },
            ["targets"] = targets,
            ["outputLocation"] = _out,
            ["dryRun"] = false,
            ["confirm"] = true,
            ["jobId"] = jobId,
            ["maxItemsPerShard"] = N,
        };

        var r = await new ApplyProcessingPlanTool().ExecuteAsync(Ctx(args));
        Assert.True(r.Success);

        var lines = File.ReadLines((string)Data(r)["checkpoint"]!).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        Assert.StartsWith("{\"t\":\"header\"", lines[0]);
        var recordTypes = lines.Select(line =>
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.GetProperty("t").GetString();
        }).ToList();
        Assert.Equal(1, recordTypes.Count(type => type == "manifest"));   // 新共享 JobStore 元数据
        Assert.Equal(1, recordTypes.Count(type => type == "receipt"));    // 释放时的共享作业回执
        Assert.Equal(2 * N + 1, recordTypes.Count(type => type is "item" or "tick")); // 变化项＋刻度＋释放刻度
        Assert.True(lines.Max(l => l.Length) < 600,                      // 单条记录不含全量状态 ⇒ 每次写 O(1)
            "存在超长记录，疑似全量重写：" + lines.Max(l => l.Length));
        Assert.DoesNotContain(lines, l => l.Contains("\"Items\":["));     // 旧格式的全量数组不再出现
    }

    [Fact]
    public async Task Plan_Journal_KeepsSingleCarrierAndLeavesNoTmpResidue()
    {
        var jobId = "d079-single-carrier-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var r = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: jobId)));
        var ck = (string)Data(r)["checkpoint"]!;
        var dir = Path.GetDirectoryName(ck)!;

        Assert.True(File.Exists(ck));
        Assert.False(File.Exists(Path.Combine(dir, jobId + ".json")));      // 一个 job 只有一份真相（不双写）
        Assert.False(File.Exists(ck + ".tmp"));
        Assert.DoesNotContain(Directory.GetFiles(dir), f => f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Plan_LegacySnapshotFormat_IsStillReadableAndReportable()
    {
        var dir = await CheckpointDirAsync();
        var jobId = "d079-legacy-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        File.WriteAllText(Path.Combine(dir, jobId + ".json"), """
            {
              "JobId": "__ID__",
              "Kind": "apply_processing_plan",
              "Total": 3,
              "Done": 2,
              "Failed": 1,
              "Skipped": 0,
              "Pending": 0,
              "CurrentShard": 1,
              "ShardCount": 1,
              "ShardSeconds": 25,
              "MaxItemsPerShard": 10,
              "ResumeToken": null,
              "LastError": "stub_fail: stub failure",
              "OwnerToken": "24476-ac661935",
              "Running": false,
              "CreatedUtc": "2026-09-24T04:19:28.5720033Z",
              "UpdatedUtc": "2026-09-24T04:19:28.6970521Z",
              "Items": [
                { "Index": 0, "Tool": "stub_ok", "Target": "a.csv", "State": "ok", "Reason": null, "ErrorCode": null, "Artifact": null, "Sha256": null, "DurationMs": 1 },
                { "Index": 1, "Tool": "stub_ok", "Target": "b.csv", "State": "ok", "Reason": null, "ErrorCode": null, "Artifact": null, "Sha256": null, "DurationMs": 2 },
                { "Index": 2, "Tool": "stub_fail", "Target": "c.csv", "State": "failed", "Reason": "stub failure", "ErrorCode": "GEOPROCESSING_ERROR", "Artifact": null, "Sha256": null, "DurationMs": 3 }
              ]
            }
            """.Replace("__ID__", jobId));

        var s = await new GetJobStatusTool().ExecuteAsync(Ctx(new() { ["jobId"] = jobId }));
        Assert.True(s.Success);
        var d = Data(s);
        Assert.Equal(3, d["total"]);
        Assert.Equal(2, d["done"]);
        Assert.Equal(1, d["failed"]);
        Assert.EndsWith(".json", (string?)d["checkpoint"]);

        var rep = await new GetJobReportTool().ExecuteAsync(Ctx(new() { ["jobId"] = jobId }));
        var rd = Data(rep);
        Assert.Equal(2, ((List<Dictionary<string, object?>>)rd["processed"]!).Count);
        var failed = (List<Dictionary<string, object?>>)rd["failed"]!;
        Assert.Single(failed);
        Assert.Equal("c.csv", failed[0]["target"]);           // 失败不静默（语义与 D-073 期一致）
    }

    [Fact]
    public async Task Plan_Journal_TornTrailingLineStopsAtTheConsistentPoint_AndKeepsCompletedItems()
    {
        var dir = await CheckpointDirAsync();
        var jobId = "d079-torn-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var header = """{"t":"header","jobId":"__ID__","kind":"apply_processing_plan","total":3,"shardSeconds":25,"maxItemsPerShard":10,"ownerToken":"1-abc","createdUtc":"2026-09-27T00:00:00.0000000Z","updatedUtc":"2026-09-27T00:00:00.0000000Z"}"""
            .Replace("__ID__", jobId);
        var item0 = """{"t":"item","index":0,"tool":"stub_ok","target":"a.csv","state":"ok","reason":null,"errorCode":null,"artifact":null,"sha256":null,"durationMs":1}""";
        var item1 = """{"t":"item","index":1,"tool":"stub_ok","target":"b.csv","state":"ok","reason":null,"errorCode":null,"artifact":null,"sha256":null,"durationMs":2}""";
        var tick = """{"t":"tick","done":2,"failed":0,"skipped":0,"pending":1,"currentShard":1,"shardCount":1,"resumeToken":"__ID__","lastError":null,"updatedUtc":"2026-09-27T00:00:01.0000000Z"}"""
            .Replace("__ID__", jobId);
        File.WriteAllText(Path.Combine(dir, jobId + ".jsonl"),
            header + "\n" + item0 + "\n" + item1 + "\n" + tick + "\n" + """{"t":"item","index":2,"tool":"stub_ok","ta""");   // 末行撕裂

        var s = await new GetJobStatusTool().ExecuteAsync(Ctx(new() { ["jobId"] = jobId }));
        Assert.True(s.Success);
        var d = Data(s);
        Assert.Equal(3, d["total"]);
        Assert.Equal(2, d["done"]);                            // 已完成项不因撕裂丢失
        Assert.Equal(1, d["pending"]);                         // 撕裂那条按待办处理 ⇒ 绝不伪装完成
        Assert.Equal(true, d["resumable"]);
    }

    [Fact]
    public async Task Plan_Journal_ReplayCountsBothUnwrittenAndDeferredItemsAsRemaining()
    {
        var dir = await CheckpointDirAsync();
        var jobId = "d079-mixed-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var header = $$"""{"t":"header","jobId":"{{jobId}}","kind":"apply_processing_plan","total":5,"shardSeconds":25,"maxItemsPerShard":10,"ownerToken":"1-abc","createdUtc":"2026-09-27T00:00:00.0000000Z","updatedUtc":"2026-09-27T00:00:00.0000000Z"}""";
        var written = $$"""{"t":"item","index":0,"tool":"stub_ok","target":"a.csv","state":"ok","reason":null,"errorCode":null,"artifact":null,"sha256":null,"durationMs":1}"""          ;
        var deferred = $$"""{"t":"item","index":1,"tool":"stub_ok","target":"b.csv","state":"pending","reason":"shard-budget-exhausted (resume with the same jobId)","errorCode":null,"artifact":null,"sha256":null,"durationMs":0}""";
        var tick = $$"""{"t":"tick","done":1,"failed":0,"skipped":0,"pending":4,"currentShard":1,"shardCount":1,"resumeToken":"{{jobId}}","lastError":null,"updatedUtc":"2026-09-27T00:00:01.0000000Z"}""";
        File.WriteAllText(Path.Combine(dir, jobId + ".jsonl"), header + "\n" + written + "\n" + deferred + "\n" + tick + "\n");

        var d = Data(await new GetJobStatusTool().ExecuteAsync(Ctx(new() { ["jobId"] = jobId })));

        Assert.Equal(5, d["total"]);
        Assert.Equal(1, d["done"]);
        Assert.Equal(4, d["pending"]);   // 3 条未写出 ＋ 1 条已写出但状态仍 pending
        Assert.Equal(0, d["failed"]);
        Assert.Equal(true, d["resumable"]);
    }

    [Fact]
    public async Task Plan_ConcurrentSameJobId_IsStillRefused_WithJournalCarrier()
    {
        var first = new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, tool: "stub_slow", dryRun: false, confirm: true, jobId: "d079-race-1")));
        await Task.Delay(120);

        var second = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, tool: "stub_slow", dryRun: false, confirm: true, jobId: "d079-race-1")));

        Assert.Equal(ErrorCodes.InvalidState, Code(second));   // D-077 并发拒写语义不因载体改变
        await first;
        var status = Data(await new GetJobStatusTool().ExecuteAsync(Ctx(new() { ["jobId"] = "d079-race-1" })));
        Assert.Equal(1, status["total"]);
        Assert.Equal(1, status["done"]);                       // 断点未被并发改写（未混写）
    }

    private async Task<string> CheckpointDirAsync()    {
        var probe = await new ApplyProcessingPlanTool().ExecuteAsync(
            Ctx(PlanArgs(_out, dryRun: false, confirm: true, jobId: "d079-dirprobe-" + Guid.NewGuid().ToString("N").Substring(0, 8))));
        return Path.GetDirectoryName((string)Data(probe)["checkpoint"]!)!;
    }

    /// <summary>D-079 · A1：带**必填产物落点参数**的桩工具（用于验证缺参歧义披露）。</summary>
    private sealed class StubRequiresOutputTool : IMCPTool
    {
        private readonly bool _arrayForm;

        public StubRequiresOutputTool(bool arrayForm) => _arrayForm = arrayForm;

        public string Name => _arrayForm ? "stub_needs_out_arr" : "stub_needs_out";
        public string Description => "stub requiring an output argument";

        public IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?> { ["out_feature_class"] = new Dictionary<string, object?> { ["type"] = "string" } },

            // 产品内两种真实写法都要能读：GP 工具用 string[]，Native 工具用 List<object?>。
            ["required"] = _arrayForm ? new[] { "out_feature_class" } : new List<object?> { "out_feature_class" },
        };

        public ToolMetadata Metadata => new() { Name = Name, Description = Description, Category = ToolCategories.General };

        public Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
            => Task.FromResult(OperationResult<object?>.Ok(new Dictionary<string, object?> { ["outputPath"] = null }));
    }
}
