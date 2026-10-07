using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Jobs;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

public sealed class D087InfrastructureTests
{
    [Fact]
    public void ValidatorPipeline_RefusesWriteBeforeRequiredArgumentValidation()
    {
        var readOnly = new ReadOnlyModeService();
        readOnly.Set(true);
        var pathValidator = new CountingPathValidator();
        var tool = new TestTool("export_features", RequiredOutputSchema());
        var context = new ToolExecutionContext { ReadOnly = readOnly };

        var error = new ToolValidatorPipeline(pathValidator).Validate(tool, context);

        Assert.Equal(ErrorCodes.PermissionDenied, error?.Code);
        Assert.Equal(0, pathValidator.Calls);
        Assert.Equal(0, tool.Calls);
    }

    [Fact]
    public void ValidatorPipeline_RequiredArgumentsPrecedeSharedPathGuard()
    {
        var pathValidator = new CountingPathValidator();
        var tool = new TestTool("sample", RequiredOutputSchema());

        var error = new ToolValidatorPipeline(pathValidator).Validate(tool, new ToolExecutionContext());

        Assert.Equal(ErrorCodes.InvalidArgument, error?.Code);
        Assert.Equal(0, pathValidator.Calls);
    }

    [Fact]
    public void ProtectedOutputValidator_RejectsProtectedRootAndAllowsOrdinaryDPath()
    {
        var validator = new ProtectedOutputPathArgumentValidator();
        var tool = new TestTool("sample", OutputPathSchema());

        var rejected = validator.Validate(tool, new Dictionary<string, object?>
        {
            ["outputPath"] = @"D:\owned\TestFixtures\protected.gdb"
        });
        var accepted = validator.Validate(tool, new Dictionary<string, object?>
        {
            ["outputPath"] = @"D:\owned\ordinary.gdb"
        });

        Assert.Equal(ErrorCodes.PathEscapeRejected, rejected?.Code);
        Assert.Null(accepted);
    }

    [Fact]
    public async Task ToolInvoker_RecordsIntentBeforeReceiptAndUsesUniqueInvocationIds()
    {
        var journal = new RecordingJournal();
        var invoker = new ToolInvoker(journal: journal);
        var tool = new TestTool("sample", new Dictionary<string, object?>());
        var firstContext = new ToolExecutionContext { RequestId = "same-request" };
        var secondContext = new ToolExecutionContext { RequestId = "same-request" };

        var first = await invoker.InvokeAsync(tool, firstContext);
        var second = await invoker.InvokeAsync(tool, secondContext);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(new[] { "intent", "receipt", "intent", "receipt" }, journal.Order);
        Assert.NotEqual(journal.Manifests[0].InvocationId, journal.Manifests[1].InvocationId);
        Assert.Equal("same-request", firstContext.LastInvocationReceipt?.RequestId);
        Assert.Equal("SUCCESS", firstContext.LastInvocationReceipt?.Outcome);
    }

    [Fact]
    public async Task ToolInvoker_ExceptionAfterDispatchRequiresReconciliationWithoutRetry()
    {
        var journal = new RecordingJournal();
        var tool = new TestTool("throws", new Dictionary<string, object?>(), shouldThrow: true);
        var result = await new ToolInvoker(journal: journal).InvokeAsync(tool, new ToolExecutionContext());

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InternalError, result.Errors.Single().Code);
        Assert.Equal(1, tool.Calls);
        Assert.True(journal.Receipts.Single().ReconcileRequired);
        Assert.Equal("UNKNOWN", journal.Receipts.Single().Outcome);
    }

    [Fact]
    public async Task ToolInvoker_CancellationAfterDispatchRequiresReconciliation()
    {
        var journal = new RecordingJournal();
        var tool = new TestTool("cancels", new Dictionary<string, object?>(), shouldCancel: true);
        var result = await new ToolInvoker(journal: journal).InvokeAsync(tool, new ToolExecutionContext());

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.Cancelled, result.Errors.Single().Code);
        Assert.True(journal.Receipts.Single().ReconcileRequired);
        Assert.Equal("UNKNOWN", journal.Receipts.Single().Outcome);
    }

    [Fact]
    public async Task McpRouter_UsesTheInjectedSharedInvoker()
    {
        var registry = new MCPToolRegistry();
        var tool = new TestTool("sample", new Dictionary<string, object?>());
        registry.Register(tool);
        var journal = new RecordingJournal();
        var router = new MCPToolRouter(
            registry,
            new FakeArcGISHost(),
            new MCPSettings(),
            NullLogger.Instance,
            invoker: new ToolInvoker(journal: journal));

        var result = await router.ExecuteAsync(new MCPToolCall { Name = "sample", RequestId = "router-request" });

        Assert.True(result.Success);
        Assert.Equal(1, tool.Calls);
        Assert.Equal(new[] { "intent", "receipt" }, journal.Order);
        Assert.Equal("router-request", journal.Manifests.Single().RequestId);
    }

    [Fact]
    public void JobStoreRootResolver_RequiresExplicitDDriveRootAndRejectsTempOrFallback()
    {
        var explicitRoot = Path.Combine(WorkflowJobStore.ResolveDir(), "explicit-root");
        var auditRoot = Path.Combine(WorkflowJobStore.ResolveDir(), "audit", "gp-audit.jsonl");

        Assert.Equal(Path.GetFullPath(explicitRoot), WorkflowJobStore.ResolveDir(explicitRoot, null));
        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(auditRoot)!, "jobs")),
            WorkflowJobStore.ResolveDir(null, auditRoot));
        Assert.Throws<InvalidOperationException>(() => WorkflowJobStore.ResolveDir(null, null));
        Assert.Throws<InvalidOperationException>(() => WorkflowJobStore.ResolveDir(@"C:\jobs", null));
        Assert.Throws<InvalidOperationException>(() => WorkflowJobStore.ResolveDir(Path.GetTempPath(), null));
    }

    [Fact]
    public void TypedManifestAndReceipt_EncodeHashAndRevisionAsStrings()
    {
        var hashText = new string('a', 64).ToUpperInvariant();
        var manifest = new JobManifest(
            "manifest", JobManifest.SchemaName, "job-typed", "sample", "2026-09-29T00:00:00Z",
            new JobRevision("rev-12"), 4,
            new[] { new JobArtifactReference(@"D:\output.gdb", new ArtifactHash(hashText), 12, new JobRevision("rev-12")) });
        var receipt = new JobReceipt(
            "receipt", JobReceipt.SchemaName, "job-typed", "SUCCESS", "2026-09-29T00:00:01Z",
            new JobRevision("rev-12"), new JobRevision("rev-13"), false, 4, manifest.Artifacts);

        using var manifestJson = JsonDocument.Parse(JsonSerializer.Serialize(manifest));
        using var receiptJson = JsonDocument.Parse(JsonSerializer.Serialize(receipt));

        Assert.Equal(hashText, manifestJson.RootElement.GetProperty("artifacts")[0].GetProperty("sha256").GetString());
        Assert.Equal("rev-12", manifestJson.RootElement.GetProperty("expectedRevision").GetString());
        Assert.Equal("rev-13", receiptJson.RootElement.GetProperty("actualRevision").GetString());
        Assert.IsType<ArtifactHash>(JsonSerializer.Deserialize<ArtifactHash>(JsonSerializer.Serialize(new ArtifactHash(hashText))));
    }

    [Fact]
    public async Task JobStore_ReadsListsCancelsAndResumesLegacySnapshotAndD079JournalGenerations()
    {
        var root = WorkflowJobStore.ResolveDir();
        var legacyId = "d087-legacy-" + Guid.NewGuid().ToString("N");
        var journalId = "d087-d079-" + Guid.NewGuid().ToString("N");
        var legacyPath = Path.Combine(root, legacyId + ".json");
        var journalPath = Path.Combine(root, journalId + ".jsonl");
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(legacyPath,
                "{\"JobId\":\"" + legacyId + "\",\"Kind\":\"legacy\",\"Total\":1,\"Done\":1,\"Pending\":0," +
                "\"Items\":[{\"Index\":0,\"Tool\":\"sample\",\"Target\":\"one\",\"State\":\"ok\"}]}" );
            var legacy = WorkflowJobStore.ReadPersisted(legacyId);
            Assert.NotNull(legacy);
            Assert.Equal("legacy", legacy!.Kind);
            Assert.Equal("ok", legacy.Items.Single().State);
            Assert.Equal(0, legacy.FencingGeneration);

            File.WriteAllLines(journalPath, new[]
            {
                "{\"t\":\"header\",\"jobId\":\"" + journalId + "\",\"kind\":\"apply_processing_plan\",\"total\":1,\"shardSeconds\":25,\"maxItemsPerShard\":10,\"ownerToken\":\"old-owner\",\"createdUtc\":\"2026-09-28T00:00:00Z\",\"updatedUtc\":\"2026-09-28T00:00:00Z\"}",
                "{\"t\":\"item\",\"index\":0,\"tool\":\"sample\",\"target\":\"two\",\"state\":\"ok\",\"artifact\":\"D:\\\\two.gdb\",\"sha256\":\"" + new string('B', 64) + "\"}",
                "{\"t\":\"tick\",\"done\":1,\"failed\":0,\"skipped\":0,\"pending\":0,\"currentShard\":1,\"shardCount\":1,\"updatedUtc\":\"2026-09-28T00:00:01Z\"}"
            });
            var journal = WorkflowJobStore.ReadPersisted(journalId);
            Assert.NotNull(journal);
            Assert.Equal("apply_processing_plan", journal!.Kind);
            Assert.Equal("ok", journal.Items.Single().State);
            Assert.Equal(new string('B', 64), journal.Items.Single().Sha256);
            Assert.False(journal.Running);

            var listed = await new ListJobsTool().ExecuteAsync(new ToolExecutionContext
            {
                Arguments = new Dictionary<string, object?> { ["kind"] = "apply_processing_plan" }
            });
            Assert.True(listed.Success);
            var listData = Assert.IsType<Dictionary<string, object?>>(listed.Data);
            var summaries = Assert.IsType<List<Dictionary<string, object?>>>(listData["jobs"]);
            Assert.Contains(summaries, item => string.Equals(item["jobId"]?.ToString(), journalId, StringComparison.Ordinal));

            var resumed = WorkflowJobStore.Claim(journalId, "apply_processing_plan", 1, 25, 10);
            Assert.Equal(WorkflowJobStore.ClaimResult.Resumed, resumed.Result);
            Assert.True(resumed.State!.FencingGeneration > 0);
            var cancel = await new CancelJobTool().ExecuteAsync(new ToolExecutionContext
            {
                Arguments = new Dictionary<string, object?> { ["jobId"] = journalId, ["waitMs"] = 0 }
            });
            Assert.True(cancel.Success);
            Assert.True(resumed.State.CancelRequested);
            WorkflowJobStore.Release(resumed.State);

            var recovered = WorkflowJobStore.ReadPersisted(journalId);
            Assert.NotNull(recovered);
            Assert.True(recovered!.CancelRequested);
            Assert.False(recovered.Running);
        }
        finally
        {
            DeleteIfExists(legacyPath);
            DeleteIfExists(journalPath);
        }
    }

    [Fact]
    public void JobStore_FencingGenerationRejectsStaleWriter()
    {
        var jobId = "d087-fence-" + Guid.NewGuid().ToString("N");
        var first = WorkflowJobStore.Claim(jobId, "sample", 1, 25, 10);
        Assert.Equal(WorkflowJobStore.ClaimResult.Created, first.Result);
        var active = Assert.IsType<FolderJobState>(first.State);
        var stale = JsonSerializer.Deserialize<FolderJobState>(JsonSerializer.Serialize(active))!;
        var firstGeneration = active.FencingGeneration;
        WorkflowJobStore.Release(active);

        try
        {
            var second = WorkflowJobStore.Claim(jobId, "sample", 1, 25, 10);
            Assert.Equal(WorkflowJobStore.ClaimResult.Resumed, second.Result);
            Assert.True(second.State!.FencingGeneration > firstGeneration);
            Assert.Throws<InvalidOperationException>(() => WorkflowJobStore.Save(stale));
            WorkflowJobStore.Save(second.State);
            WorkflowJobStore.Release(second.State);
        }
        finally
        {
            DeleteJobFiles(jobId);
        }
    }

    [Fact]
    public void JobStore_CompactionPreservesSnapshotTailReceiptAndEvidence()
    {
        var jobId = "d087-compact-" + Guid.NewGuid().ToString("N");
        var claimed = WorkflowJobStore.Claim(jobId, "sample", 1, 25, 10);
        Assert.Equal(WorkflowJobStore.ClaimResult.Created, claimed.Result);
        var state = Assert.IsType<FolderJobState>(claimed.State);
        state.ExpectedRevision = "expected-1";
        state.Items.Add(new FolderJobItem
        {
            Index = 0, Tool = "sample", Target = "one", State = "ok",
            Artifact = @"D:\run\output.gdb", Sha256 = new string('C', 64)
        });

        try
        {
            WorkflowJobStore.Save(state);
            Assert.True(WorkflowJobStore.CompactJournal(jobId, tailRecordCount: 1));
            var journalPath = WorkflowJobStore.TryCheckpointPath(jobId)!;
            var firstRecord = JsonDocument.Parse(File.ReadLines(journalPath).First());
            using (firstRecord)
            {
                Assert.Equal("snapshot", firstRecord.RootElement.GetProperty("t").GetString());
            }

            var persisted = WorkflowJobStore.ReadPersisted(jobId);
            Assert.NotNull(persisted);
            Assert.False(persisted!.Running);
            Assert.Equal("expected-1", persisted.ExpectedRevision);
            Assert.Equal("ok", persisted.Items.Single().State);
            Assert.Equal(new string('C', 64), persisted.Items.Single().Sha256);
            Assert.True(File.Exists(Path.Combine(WorkflowJobStore.ResolveDir(), jobId + ".compaction-evidence")));

            WorkflowJobStore.Release(state);
            var receipt = WorkflowJobStore.CreateReceipt(jobId);
            Assert.Equal("SUCCESS", receipt?.Outcome);
            Assert.Equal("expected-1", receipt?.ExpectedRevision?.Value);
        }
        finally
        {
            DeleteJobFiles(jobId);
        }
    }

    [Fact]
    public void OwnedJsonlCompactor_RejectsMalformedInputWithoutMutatingOwnedJournal()
    {
        var root = Path.Combine(WorkflowJobStore.ResolveDir(), "d087-compactor-tests");
        Directory.CreateDirectory(root);
        var journal = Path.Combine(root, Guid.NewGuid().ToString("N") + ".jsonl");
        var evidence = Path.ChangeExtension(journal, ".evidence");
        const string original = "{\"t\":\"header\"}\n";
        File.WriteAllText(journal, original);

        try
        {
            Assert.ThrowsAny<JsonException>(() => new OwnedJsonlCompactor().Compact(
                journal, "{\"t\":\"snapshot\"}", new[] { "{bad-json}" }, evidence));
            Assert.Equal(original, File.ReadAllText(journal));
            Assert.False(File.Exists(evidence));

            var receipt = new OwnedJsonlCompactor().Compact(
                journal, "{\"t\":\"snapshot\"}", new[] { "{\"t\":\"tail\"}" }, evidence);
            Assert.Equal(1, receipt.OriginalRecordCount);
            Assert.Equal(1, receipt.TailRecordCount);
            Assert.True(File.Exists(evidence));
        }
        finally
        {
            DeleteIfExists(journal);
            DeleteIfExists(evidence);
            DeleteIfExists(evidence + ".pending");
            DeleteIfExists(journal + ".compact.tmp");
        }
    }

    [Fact]
    public void OwnedJsonlCompactor_PreservesPreexistingTemporaryAndJournal()
    {
        var root = Path.Combine(WorkflowJobStore.ResolveDir(), "d087-compactor-tests");
        Directory.CreateDirectory(root);
        var journal = Path.Combine(root, Guid.NewGuid().ToString("N") + ".jsonl");
        var evidence = Path.ChangeExtension(journal, ".evidence");
        var temporary = journal + ".compact.tmp";
        const string original = "{\"t\":\"header\"}\n";
        const string preexisting = "owned-by-an-earlier-interrupted-run";
        File.WriteAllText(journal, original);
        File.WriteAllText(temporary, preexisting);

        try
        {
            Assert.Throws<InvalidOperationException>(() => new OwnedJsonlCompactor().Compact(
                journal, "{\"t\":\"snapshot\"}", Array.Empty<string>(), evidence));
            Assert.Equal(original, File.ReadAllText(journal));
            Assert.Equal(preexisting, File.ReadAllText(temporary));
            Assert.False(File.Exists(evidence));
        }
        finally
        {
            DeleteIfExists(journal);
            DeleteIfExists(evidence);
            DeleteIfExists(evidence + ".pending");
            DeleteIfExists(temporary);
        }
    }

    private static IReadOnlyDictionary<string, object?> RequiredOutputSchema()
        => new Dictionary<string, object?>
        {
            ["properties"] = new Dictionary<string, object?>
            {
                ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string" }
            },
            ["required"] = new[] { "outputPath" }
        };

    private static IReadOnlyDictionary<string, object?> OutputPathSchema()
        => new Dictionary<string, object?>
        {
            ["properties"] = new Dictionary<string, object?>
            {
                ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string" }
            }
        };

    private static void DeleteJobFiles(string jobId)
    {
        var root = WorkflowJobStore.ResolveDir();
        DeleteIfExists(Path.Combine(root, jobId + ".json"));
        DeleteIfExists(Path.Combine(root, jobId + ".jsonl"));
        DeleteIfExists(Path.Combine(root, jobId + ".compaction-evidence"));
        DeleteIfExists(Path.Combine(root, "." + jobId + ".lease"));
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private sealed class CountingPathValidator : IToolPathArgumentValidator
    {
        public int Calls { get; private set; }

        public OperationError? Validate(IMCPTool tool, IReadOnlyDictionary<string, object?>? arguments)
        {
            Calls++;
            return null;
        }
    }

    private sealed class RecordingJournal : IToolInvocationJournal
    {
        public List<string> Order { get; } = new();
        public List<ToolInvocationManifest> Manifests { get; } = new();
        public List<ToolInvocationReceipt> Receipts { get; } = new();

        public void RecordIntent(ToolInvocationManifest manifest, ILogger logger)
        {
            Order.Add("intent");
            Manifests.Add(manifest);
        }

        public void RecordReceipt(ToolInvocationReceipt receipt, ILogger logger)
        {
            Order.Add("receipt");
            Receipts.Add(receipt);
        }
    }

    private sealed class TestTool : IMCPTool
    {
        private readonly bool _shouldThrow;
        private readonly bool _shouldCancel;

        public TestTool(string name, IReadOnlyDictionary<string, object?> schema, bool shouldThrow = false, bool shouldCancel = false)
        {
            Name = name;
            InputSchema = schema;
            _shouldThrow = shouldThrow;
            _shouldCancel = shouldCancel;
        }

        public string Name { get; }
        public string Description => "D-087 infrastructure test tool";
        public IReadOnlyDictionary<string, object?> InputSchema { get; }
        public int Calls { get; private set; }

        public Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        {
            Calls++;
            if (_shouldThrow) throw new InvalidOperationException("simulated post-dispatch failure");
            if (_shouldCancel) throw new OperationCanceledException("simulated uncertain cancellation");
            return Task.FromResult(OperationResult<object?>.Ok("ok"));
        }
    }
}
