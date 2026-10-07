using System.Collections.Concurrent;
using System.Text.Json;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Core.Jobs;
/// <summary>任务项状态（分片执行的逐项留证）。</summary>
public sealed class FolderJobItem
{
    public int Index { get; set; }
    public string Tool { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    /// <summary>ok | skipped | failed | pending</summary>
    public string State { get; set; } = "pending";
    public string? Reason { get; set; }
    public string? ErrorCode { get; set; }
    public string? Artifact { get; set; }
    public string? Sha256 { get; set; }
    public long DurationMs { get; set; }
}

/// <summary>任务状态（会话内内存 + D 盘断点）。</summary>
public sealed class FolderJobState
{
    public string JobId { get; set; } = string.Empty;
    public string Kind { get; set; } = "apply_processing_plan";
    public int Total { get; set; }
    public int Done { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int Pending { get; set; }
    public int CurrentShard { get; set; }
    public int ShardCount { get; set; }
    public int ShardSeconds { get; set; } = 25;
    public int MaxItemsPerShard { get; set; } = 10;
    public string? ResumeToken { get; set; }
    public string? LastError { get; set; }
    public int ResumeCount { get; set; }
    public int LastResumedSkips { get; set; }
    public bool CancelRequested { get; set; }
    public bool Cancelled { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>D-077：所有者令牌（进程 + 宿主实例），用于并发同 jobId 冲突拒写。</summary>
    public string OwnerToken { get; set; } = string.Empty;

    /// <summary>D-087 fencing token. Old JSON/JSONL records omit this and resume at generation 1.</summary>
    public long FencingGeneration { get; set; }

    /// <summary>D-077：该 job 是否正在执行（并发同 jobId ⇒ 拒绝，不静默覆盖）。</summary>
    public bool Running { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
    public string? ExpectedRevision { get; set; }
    public string? ActualRevision { get; set; }
    public bool ReconcileRequired { get; set; }
    public List<FolderJobItem> Items { get; set; } = new();

    /// <summary>
    /// D-079 · B 组（O-D077-01）：断点载体 = true ⇒ 增量 JSONL 日志；false ⇒ D-075/D-077 期单文件全量快照。
    /// 载体在 job 创建（或断点读回）时确定并**粘住**，故旧 job 续跑仍写旧格式、不产生双文件歧义。
    /// 不参与序列化 ⇒ 快照文件形状与 D-077 逐字节同构（向后兼容）。
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Journal { get; set; }
}

/// <summary>任务存储：会话内内存 + 断点清单落 D 盘（G-138：禁 %TEMP%；可续跑）。</summary>
public static class WorkflowJobStore
{
    /// <summary>G-138：临时目录判定（宿主 %TEMP% ∪ 规范化 AppData\Local\Temp ∪ Windows\Temp）。</summary>
    public static bool IsTempLikePath(string full)
    {
        var f = Path.GetFullPath(full).Replace('/', '\\');
        if (f.StartsWith(Path.GetFullPath(Path.GetTempPath()).Replace('/', '\\'), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var lower = f.ToLowerInvariant();
        return lower.Contains(@"\appdata\local\temp\") || lower.Contains(@"\windows\temp\");
    }

    private static readonly ConcurrentDictionary<string, FolderJobState> Jobs = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> CancellationSources = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, FileStream> LeaseHandles = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, long> LeaseGenerations = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    /// <summary>D-079 · B 组：JSONL 记录用紧凑序列化（一行一条，无缩进）。</summary>
    private static readonly JsonSerializerOptions Line = new() { WriteIndented = false };
    private static readonly JsonSerializerOptions SnapshotLine = new() { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const long JournalCompactionThresholdBytes = 1024 * 1024;

    /// <summary>
    /// D-079 · B 组：每个 job 已写出的逐项记录指纹（⇒ 每次 Save 只追加**发生变化**的项，消除全量重写）。
    /// 仅内存态；进程重启后由日志重放重建（见 <see cref="Find"/>）。
    /// </summary>
    private static readonly ConcurrentDictionary<string, Dictionary<int, string>> WrittenItems = new(StringComparer.Ordinal);

    /// <summary>断点目录解析：显式 jobs 目录 → 显式审计目录同级 jobs；不回退到应用目录或系统临时目录。</summary>
    public static string ResolveDir()
        => ResolveDir(
            Environment.GetEnvironmentVariable("ARCGIS_PRO_MCP_JOBS_DIR"),
            Environment.GetEnvironmentVariable(GpAuditLog.AuditDirVariable));

    /// <summary>Pure resolver overload for validation and tests; both accepted roots are explicit.</summary>
    public static string ResolveDir(string? explicitDir, string? auditDir)
    {
        string dir;
        if (!string.IsNullOrWhiteSpace(explicitDir))
        {
            dir = explicitDir!;
        }
        else
        {
            dir = !string.IsNullOrWhiteSpace(auditDir)
                ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(auditDir!)) ?? Path.GetFullPath(auditDir!), "jobs")
                : throw new InvalidOperationException("A jobs directory or audit directory must be explicitly configured.");
        }

        dir = Path.GetFullPath(dir);
        if (!string.Equals(Path.GetPathRoot(dir), @"D:\", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("D-087: job checkpoint directory must resolve under the configured D: drive root.");
        }

        if (IsTempLikePath(dir))
        {
            throw new InvalidOperationException("G-197: job checkpoint directory must not live under a temporary directory.");
        }

        return dir;
    }

    /// <summary>D-077：本宿主实例的所有者令牌（进程 + 随机实例标识）。</summary>
    public static readonly string OwnerToken =
        Environment.ProcessId + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);

    /// <summary>D-077：竞争结果（ConcurrentDictionary 原子占位）。</summary>
    public enum ClaimResult { Created, Resumed, Busy, ForeignOwner }

    /// <summary>D-077：声明（创建/续跑/拒写）。同一 jobId 被其它所有者或正在执行 ⇒ 拒（不静默覆盖）。</summary>
    public static (ClaimResult Result, FolderJobState? State) Claim(
        string jobId, string kind, int total, int shardSeconds, int maxItemsPerShard)
    {
        if (!IsSafeJobId(jobId))
        {
            return (ClaimResult.ForeignOwner, null);
        }

        var state = Jobs.TryGetValue(jobId, out var current) ? current : Find(jobId);
        var createdFromDisk = state is null;
        state ??= Jobs.GetOrAdd(jobId, _ => new FolderJobState
        {
            JobId = jobId,
            Kind = kind,
            Total = total,
            Pending = total,
            ShardSeconds = shardSeconds,
            MaxItemsPerShard = maxItemsPerShard,
            OwnerToken = OwnerToken,
            CreatedUtc = DateTime.UtcNow.ToString("o"),
            UpdatedUtc = DateTime.UtcNow.ToString("o"),

            // D-079 · B 组：新建 job ⇒ 断点载体 = 增量 JSONL（粘性，见 FolderJobState.Journal）。
            Journal = true,
        });

        if (!TryAcquireLease(jobId))
        {
            return (ClaimResult.Busy, state);
        }

        lock (state)
        {
            if (state.Running && LeaseHandles.TryGetValue(jobId, out var heldByThisProcess)
                && heldByThisProcess is not null && LeaseGenerations.ContainsKey(jobId))
            {
                ReleaseLease(jobId);
                return (ClaimResult.Busy, state);
            }

            var created = createdFromDisk && state.Items.Count == 0;
            state.OwnerToken = OwnerToken;
            state.FencingGeneration = Math.Max(1, state.FencingGeneration + 1);
            LeaseGenerations[jobId] = state.FencingGeneration;
            state.Running = true;
            if (!created)
            {
                state.ResumeCount++;
                state.LastResumedSkips = 0;
            }
            state.CancelRequested = false;
            state.Cancelled = false;
            state.CancelReason = null;
            if (created && state.Journal)
            {
                // 头部记录 = job 级元数据（一次写入；后续每次 Save 只追加变化项）
                WrittenItems[state.JobId] = new Dictionary<int, string>();
                try
                {
                    AppendRecords(state.JobId, new[]
                    {
                        Serialize(HeaderRecord("header", state)),
                        Serialize(ManifestRecord(state)),
                    });
                }
                catch
                {
                    state.Running = false;
                    ReleaseLease(jobId);
                    throw;
                }
            }
            else if (state.Journal)
            {
                try
                {
                    // Persist each newly issued fencing generation before any resumed work can dispatch.
                    AppendRecords(state.JobId, new[] { Serialize(ManifestRecord(state)) });
                }
                catch
                {
                    state.Running = false;
                    ReleaseLease(jobId);
                    throw;
                }
            }
            else
            {
                // Legacy single-file jobs keep their original carrier while persisting the new fence.
                SaveCore(state);
            }

            return (created ? ClaimResult.Created : ClaimResult.Resumed, state);
        }
    }

    private static bool TryAcquireLease(string jobId)
    {
        if (LeaseHandles.ContainsKey(jobId))
        {
            return false;
        }

        try
        {
            var directory = ResolveDir();
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "." + jobId + ".lease");
            var handle = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (LeaseHandles.TryAdd(jobId, handle))
            {
                return true;
            }

            handle.Dispose();
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void ReleaseLease(string jobId)
    {
        if (LeaseHandles.TryRemove(jobId, out var handle))
        {
            try { handle.Dispose(); } catch { }
        }

        LeaseGenerations.TryRemove(jobId, out _);
    }

    public static bool IsSafeJobId(string? jobId)
        => !string.IsNullOrWhiteSpace(jobId)
           && jobId!.Length <= 128
           && jobId is not ("." or "..")
           && string.Equals(Path.GetFileName(jobId), jobId, StringComparison.Ordinal)
           && jobId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
           && !jobId.Contains('/') && !jobId.Contains('\\');

    public static JobManifest? CreateManifest(string jobId)
    {
        var state = Find(jobId);
        return state is null ? null : CreateManifest(state);
    }

    public static JobReceipt? CreateReceipt(string jobId)
    {
        var state = Find(jobId);
        return state is null ? null : CreateReceipt(state);
    }

    private static JobManifest CreateManifest(FolderJobState state)
        => new(
            "manifest",
            JobManifest.SchemaName,
            state.JobId,
            state.Kind,
            state.CreatedUtc,
            ParseRevision(state.ExpectedRevision),
            state.FencingGeneration,
            BuildArtifactReferences(state));

    private static JobReceipt CreateReceipt(FolderJobState state)
    {
        var outcome = state.ReconcileRequired
            ? "UNKNOWN"
            : state.Cancelled
                ? "CANCELLED"
                : state.Pending > 0
                    ? "PARTIAL"
                    : state.Failed > 0
                        ? "FAILED"
                        : "SUCCESS";
        return new JobReceipt(
            "receipt",
            JobReceipt.SchemaName,
            state.JobId,
            outcome,
            state.UpdatedUtc,
            ParseRevision(state.ExpectedRevision),
            ParseRevision(state.ActualRevision),
            state.ReconcileRequired,
            state.FencingGeneration,
            BuildArtifactReferences(state));
    }

    private static IReadOnlyList<JobArtifactReference> BuildArtifactReferences(FolderJobState state)
        => state.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Artifact))
            .Select(item => new JobArtifactReference(
                item.Artifact!,
                ArtifactHash.TryParse(item.Sha256, out var hash) ? hash : null,
                null,
                ParseRevision(state.ExpectedRevision)))
            .ToArray();

    private static JobRevision? ParseRevision(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new JobRevision(value);

    private static Dictionary<string, object?> ManifestRecord(FolderJobState state)
    {
        var manifest = CreateManifest(state);
        return new Dictionary<string, object?>
        {
            ["t"] = manifest.T,
            ["schema"] = manifest.Schema,
            ["jobId"] = manifest.JobId,
            ["kind"] = manifest.Kind,
            ["createdUtc"] = manifest.CreatedUtc,
            ["expectedRevision"] = manifest.ExpectedRevision?.Value,
            ["fencingGeneration"] = manifest.FencingGeneration,
            ["artifacts"] = manifest.Artifacts,
        };
    }

    private static Dictionary<string, object?> ReceiptRecord(FolderJobState state)
    {
        var receipt = CreateReceipt(state);
        return new Dictionary<string, object?>
        {
            ["t"] = receipt.T,
            ["schema"] = receipt.Schema,
            ["jobId"] = receipt.JobId,
            ["outcome"] = receipt.Outcome,
            ["createdUtc"] = receipt.CreatedUtc,
            ["expectedRevision"] = receipt.ExpectedRevision?.Value,
            ["actualRevision"] = receipt.ActualRevision?.Value,
            ["reconcileRequired"] = receipt.ReconcileRequired,
            ["fencingGeneration"] = receipt.FencingGeneration,
            ["artifacts"] = receipt.Artifacts,
        };
    }

    public static bool WasResumed(FolderJobState state) => state.LastResumedSkips > 0;

    /// <summary>Stable public job state name shared by storage and tool response layers.</summary>
    public static string JobStateName(FolderJobState state)
    {
        if (state.Cancelled) return "cancelled";
        if (state.Running) return "running";
        if (state.Pending == state.Total && state.Done == 0 && state.Failed == 0 && state.Skipped == 0) return "queued";
        if (state.Pending > 0 && state.Done == 0 && state.Failed == 0 && state.Skipped == 0) return "awaiting";
        if (state.Pending > 0 || state.Skipped > 0 || (state.Done > 0 && state.Failed > 0)) return "partial";
        if (state.Failed > 0) return "failed";
        return "done";
    }

    public static IReadOnlyList<FolderJobState> ListAll()
    {
        try
        {
            var dir = ResolveDir();
            if (Directory.Exists(dir))
            {
                var ids = Directory.EnumerateFiles(dir, "*.jsonl")
                    .Concat(Directory.EnumerateFiles(dir, "*.json"))
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(id => IsSafeJobId(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                foreach (var id in ids) _ = Find(id!);
            }
        }
        catch { }

        return Jobs.Values.OrderByDescending(s => s.UpdatedUtc, StringComparer.Ordinal)
            .ThenBy(s => s.JobId, StringComparer.Ordinal).ToList();
    }

    public static CancellationTokenSource BeginExecution(FolderJobState state, CancellationToken outerToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
        if (!CancellationSources.TryAdd(state.JobId, source))
        {
            source.Dispose();
            throw new InvalidOperationException("A cancellation source is already registered for this job.");
        }
        if (state.CancelRequested) source.Cancel();
        return source;
    }

    public static void EndExecution(FolderJobState state, CancellationTokenSource source)
    {
        CancellationSources.TryRemove(state.JobId, out _);
        source.Dispose();
    }

    public static async Task<OperationResult<IReadOnlyDictionary<string, object?>>> RequestCancelAsync(
        string jobId, string? reason, int waitMs, CancellationToken ct)
    {
        if (!IsSafeJobId(jobId) || waitMs is < 0 or > 60_000)
            return OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.InvalidArgument, "jobId is invalid or waitMs is outside 0..60000.");
        var state = Find(jobId);
        if (state is null)
            return OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotFound, $"No job found for jobId '{jobId}'.");
        if (!string.Equals(state.Kind, "apply_processing_plan", StringComparison.Ordinal))
            return OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.InvalidArgument, "This job kind does not expose cooperative cancellation.");

        lock (state)
        {
            if (!state.Running)
            {
                return OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.InvalidState,
                    state.Cancelled ? "The job is already cancelled." : "The job is not running; no cancellation was requested.");
            }
            state.CancelRequested = true;
            state.CancelReason = string.IsNullOrWhiteSpace(reason) ? "cancel requested" : reason!.Trim();
            Save(state);
        }

        if (CancellationSources.TryGetValue(jobId, out var source))
        {
            try { source.Cancel(); } catch (ObjectDisposedException) { }
        }

        var until = DateTime.UtcNow.AddMilliseconds(waitMs);
        while (waitMs > 0 && DateTime.UtcNow < until)
        {
            ct.ThrowIfCancellationRequested();
            lock (state) if (!state.Running) break;
            await Task.Delay(Math.Min(25, Math.Max(1, (int)(until - DateTime.UtcNow).TotalMilliseconds)), ct).ConfigureAwait(false);
        }

        bool running;
        bool cancelled;
        int pending;
        lock (state) { running = state.Running; cancelled = state.Cancelled; pending = state.Pending; }
        var responseState = cancelled ? "cancelled" : running ? "awaiting" : JobStateName(state);
        IReadOnlyDictionary<string, object?> response = new Dictionary<string, object?>
        {
            ["jobId"] = state.JobId,
            ["accepted"] = true,
            ["state"] = responseState,
            ["cancelled"] = cancelled,
            ["pending"] = pending,
            ["reason"] = state.CancelReason,
            ["waitedMs"] = waitMs,
        };
        return OperationResult<IReadOnlyDictionary<string, object?>>.Ok(response);
    }

    /// <summary>D-077：释放执行占用（正常结束/异常均须调用）。</summary>
    public static void Release(FolderJobState state)
    {
        lock (state)
        {
            if (!HasCurrentLease(state))
            {
                throw new InvalidOperationException("D-087 fencing generation rejected a stale job release.");
            }

            state.Running = false;
            SaveCore(state);
            if (state.Journal)
            {
                AppendRecords(state.JobId, new[] { Serialize(ReceiptRecord(state)) });
            }
        }

        ReleaseLease(state.JobId);
    }

    /// <summary>
    /// 读回断点：内存 ⇒ 增量 JSONL（D-079 新格式）⇒ 单文件快照（D-075/D-077 旧格式，向后兼容）。
    /// </summary>
    public static FolderJobState? Find(string jobId)
    {
        if (!IsSafeJobId(jobId)) return null;
        if (Jobs.TryGetValue(jobId, out var s))
        {
            return s;
        }

        var loaded = ReadPersisted(jobId);
        if (loaded is not null)
        {
            Jobs[jobId] = loaded;
        }

        return loaded;
    }

    /// <summary>Reads the durable snapshot/journal even when an in-memory copy exists.</summary>
    public static FolderJobState? ReadPersisted(string jobId)
    {
        if (!IsSafeJobId(jobId)) return null;

        // A direct disk read supports reconcile and independent compatibility checks.
        try
        {
            var dir = ResolveDir();
            var journal = Path.Combine(dir, jobId + ".jsonl");
            if (File.Exists(journal))
            {
                var replayed = LoadJournal(journal);
                return replayed;
            }

            var path = Path.Combine(dir, jobId + ".json");
            if (!File.Exists(path))
            {
                return null;
            }

            var loaded = JsonSerializer.Deserialize<FolderJobState>(File.ReadAllText(path), Json);
            return loaded;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 写断点。D-079 · B 组：日志载体 = **只追加变化项**（每次 Save 的体积与总项数无关 ⇒ 消除 O(n²) 写放大）；
    /// 旧载体 = 原子替换整文件（<c>.tmp</c> + <c>File.Move</c>，D-077 语义逐字节保持）。
    /// </summary>
    public static void Save(FolderJobState s)
    {
        lock (s)
        {
            if (!HasCurrentLease(s))
            {
                throw new InvalidOperationException("D-087 fencing generation rejected a stale job writer.");
            }

            SaveCore(s);
        }
    }

    private static bool HasCurrentLease(FolderJobState state)
        => state.FencingGeneration > 0
           && string.Equals(state.OwnerToken, OwnerToken, StringComparison.Ordinal)
           && LeaseHandles.ContainsKey(state.JobId)
           && LeaseGenerations.TryGetValue(state.JobId, out var activeGeneration)
           && activeGeneration == state.FencingGeneration;

    private static void SaveCore(FolderJobState s)
    {
        s.UpdatedUtc = DateTime.UtcNow.ToString("o");
        s.Done = s.Items.Count(i => i.State == "ok");
        s.Failed = s.Items.Count(i => i.State == "failed");
        s.Skipped = s.Items.Count(i => i.State == "skipped");
        s.Pending = s.Items.Count(i => i.State == "pending");
        if (s.Journal)
        {
            SaveJournal(s);
            MaybeCompactJournal(s);
            return;
        }

        try
        {
            var dir = ResolveDir();
            Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, s.JobId + ".json");
            var tmp = target + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(s, Json));
            File.Move(tmp, target, overwrite: true);   // D-077：原子替换，防撕裂/半写
        }
        catch
        {
            // 断点落盘失败不阻断结果（如实由 get_job_status 的 checkpointDir 字段披露）
        }
    }

    /// <summary>D-079 · B 组：追加"变化项 + 一条 job 级刻度"⇒ 单次写入、无临时文件、无全量重写。</summary>
    private static void SaveJournal(FolderJobState s)
    {
        try
        {
            var known = WrittenItems.GetOrAdd(s.JobId, _ => new Dictionary<int, string>());
            var records = new List<string>();
            foreach (var item in s.Items)
            {
                var fingerprint = ItemFingerprint(item);
                if (!known.TryGetValue(item.Index, out var previous)
                    || !string.Equals(previous, fingerprint, StringComparison.Ordinal))
                {
                    known[item.Index] = fingerprint;
                    records.Add(Serialize(ItemRecord(item)));
                }
            }

            records.Add(Serialize(TickRecord(s)));
            AppendRecords(s.JobId, records);
        }
        catch
        {
            // 与旧载体同口径：落盘失败不阻断结果，由 checkpoint 字段与披露面如实反映
        }
    }

    public static bool CompactJournal(string jobId, int tailRecordCount = 128)
    {
        if (!IsSafeJobId(jobId) || tailRecordCount < 0)
        {
            return false;
        }

        var state = Find(jobId);
        if (state is null || !state.Journal)
        {
            return false;
        }

        lock (state)
        {
            if (!HasCurrentLease(state))
            {
                throw new InvalidOperationException("D-087 fencing generation rejected a stale journal compaction.");
            }

            var journalPath = Path.Combine(ResolveDir(), state.JobId + ".jsonl");
            if (!File.Exists(journalPath))
            {
                return false;
            }

            var lines = File.ReadAllLines(journalPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            var eventLines = new List<string>();
            foreach (var line in lines)
            {
                using var document = JsonDocument.Parse(line);
                var type = ReadString(document.RootElement, "t");
                if (type is not ("header" or "manifest" or "snapshot"))
                {
                    eventLines.Add(line);
                }
            }

            var tail = eventLines.TakeLast(tailRecordCount).ToArray();
            var snapshotState = JsonSerializer.Serialize(state, SnapshotLine);
            var snapshotRecord = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["t"] = "snapshot",
                ["state"] = JsonDocument.Parse(snapshotState).RootElement.Clone(),
            }, Line);
            var evidencePath = Path.Combine(ResolveDir(), state.JobId + ".compaction-evidence");
            _ = new OwnedJsonlCompactor().Compact(journalPath, snapshotRecord, tail, evidencePath);
            return true;
        }
    }

    private static void MaybeCompactJournal(FolderJobState state)
    {
        try
        {
            var journalPath = Path.Combine(ResolveDir(), state.JobId + ".jsonl");
            if (File.Exists(journalPath) && new FileInfo(journalPath).Length > JournalCompactionThresholdBytes)
            {
                _ = CompactJournal(state.JobId);
            }
        }
        catch
        {
            // Preserve the append-only journal if an optional compaction cannot complete.
        }
    }

    private static void AppendRecords(string jobId, IReadOnlyList<string> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        var dir = ResolveDir();
        Directory.CreateDirectory(dir);
        var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", records) + "\n");
        using var fs = new FileStream(Path.Combine(dir, jobId + ".jsonl"),
            new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write, Share = FileShare.ReadWrite });
        fs.Write(bytes, 0, bytes.Length);
        fs.Flush(flushToDisk: true);   // 整批记录一次落盘 ⇒ 不产生"半写即可读"的中间态
    }

    /// <summary>
    /// 日志重放：header 建骨架 → item 按 index 覆盖 → tick 取最后一次。
    /// 末行撕裂（进程被切断）⇒ 停在该一致点：未完成的那一条按 pending 重跑，绝不把已完成项伪装成丢失。
    /// </summary>
    private static FolderJobState? LoadJournal(string path)
    {
        FolderJobState? state = null;
        var items = new Dictionary<int, FolderJobItem>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonElement rec;
            try
            {
                rec = JsonDocument.Parse(line).RootElement;
            }
            catch (JsonException)
            {
                break;   // 撕裂/非完整记录 ⇒ 一致点到此为止
            }

            var t = ReadString(rec, "t");
            if (t == "header")
            {
                state = new FolderJobState
                {
                    Journal = true,
                    JobId = ReadString(rec, "jobId") ?? string.Empty,
                    Kind = ReadString(rec, "kind") ?? "apply_processing_plan",
                    Total = ReadInt(rec, "total"),
                    ShardSeconds = ReadInt(rec, "shardSeconds"),
                    MaxItemsPerShard = ReadInt(rec, "maxItemsPerShard"),
                    OwnerToken = ReadString(rec, "ownerToken") ?? string.Empty,
                    FencingGeneration = ReadLong(rec, "fencingGeneration"),
                    ExpectedRevision = ReadString(rec, "expectedRevision"),
                    CreatedUtc = ReadString(rec, "createdUtc") ?? string.Empty,
                    UpdatedUtc = ReadString(rec, "updatedUtc") ?? string.Empty,
                    ResumeCount = ReadInt(rec, "resumeCount"),
                };
            }
            else if (t == "snapshot" && rec.TryGetProperty("state", out var stateElement))
            {
                state = JsonSerializer.Deserialize<FolderJobState>(stateElement.GetRawText(), Json);
                items.Clear();
                if (state is not null)
                {
                    state.Journal = true;
                    // A running lease is process-local and cannot be restored from a disk snapshot.
                    state.Running = false;
                    foreach (var item in state.Items)
                    {
                        items[item.Index] = item;
                    }
                }
            }
            else if (t == "manifest" && state is not null)
            {
                state.ExpectedRevision = ReadString(rec, "expectedRevision") ?? state.ExpectedRevision;
                state.FencingGeneration = ReadLong(rec, "fencingGeneration");
            }
            else if (t == "item" && state is not null)
            {
                var index = ReadInt(rec, "index");
                var item = items.TryGetValue(index, out var existing) ? existing : new FolderJobItem { Index = index };
                item.Tool = ReadString(rec, "tool") ?? item.Tool;
                item.Target = ReadString(rec, "target") ?? item.Target;
                item.State = ReadString(rec, "state") ?? "pending";
                item.Reason = ReadString(rec, "reason");
                item.ErrorCode = ReadString(rec, "errorCode");
                item.Artifact = ReadString(rec, "artifact");
                item.Sha256 = ReadString(rec, "sha256");
                item.DurationMs = ReadLong(rec, "durationMs");
                items[index] = item;
            }
            else if (t == "tick" && state is not null)
            {
                state.CurrentShard = ReadInt(rec, "currentShard");
                state.ShardCount = ReadInt(rec, "shardCount");
                state.ResumeToken = ReadString(rec, "resumeToken");
                state.LastError = ReadString(rec, "lastError");
                state.LastResumedSkips = ReadInt(rec, "lastResumedSkips");
                state.CancelRequested = ReadBool(rec, "cancelRequested");
                state.Cancelled = ReadBool(rec, "cancelled");
                state.CancelReason = ReadString(rec, "cancelReason");
                state.ResumeCount = ReadInt(rec, "resumeCount");
                state.UpdatedUtc = ReadString(rec, "updatedUtc") ?? state.UpdatedUtc;
                state.FencingGeneration = ReadLong(rec, "fencingGeneration");
                state.ActualRevision = ReadString(rec, "actualRevision") ?? state.ActualRevision;
                state.ReconcileRequired = ReadBool(rec, "reconcileRequired");
            }
            else if (t == "receipt" && state is not null)
            {
                state.ExpectedRevision = ReadString(rec, "expectedRevision") ?? state.ExpectedRevision;
                state.ActualRevision = ReadString(rec, "actualRevision") ?? state.ActualRevision;
                state.ReconcileRequired = ReadBool(rec, "reconcileRequired");
                state.FencingGeneration = ReadLong(rec, "fencingGeneration");
            }
        }

        if (state is null)
        {
            return null;
        }

        state.Items = items.Count == 0
            ? new List<FolderJobItem>()
            : items.Keys.OrderBy(k => k).Select(k => items[k]).ToList();

        // 未写出的项 = 待办；已写出但状态仍为 pending（如分片预算耗尽）也计入待办 ⇒ 与 Save() 的口径一致。
        state.Done = state.Items.Count(i => i.State == "ok");
        state.Failed = state.Items.Count(i => i.State == "failed");
        state.Skipped = state.Items.Count(i => i.State == "skipped");
        state.Pending = Math.Max(0, state.Total - state.Items.Count) + state.Items.Count(i => i.State == "pending");

        var known = new Dictionary<int, string>();
        foreach (var i in state.Items)
        {
            known[i.Index] = ItemFingerprint(i);
        }

        WrittenItems[state.JobId] = known;
        return state;
    }

    private static string ItemFingerprint(FolderJobItem i) => string.Join('\u001f',
        i.State, i.Reason, i.ErrorCode, i.Artifact, i.Sha256, i.DurationMs.ToString(
            System.Globalization.CultureInfo.InvariantCulture));

    private static string Serialize(Dictionary<string, object?> record) => JsonSerializer.Serialize(record, Line);

    private static Dictionary<string, object?> HeaderRecord(string type, FolderJobState s) => new()
    {
        ["t"] = type,
        ["jobId"] = s.JobId,
        ["kind"] = s.Kind,
        ["total"] = s.Total,
        ["shardSeconds"] = s.ShardSeconds,
        ["maxItemsPerShard"] = s.MaxItemsPerShard,
        ["ownerToken"] = s.OwnerToken,
        ["fencingGeneration"] = s.FencingGeneration,
        ["expectedRevision"] = s.ExpectedRevision,
        ["createdUtc"] = s.CreatedUtc,
        ["updatedUtc"] = s.UpdatedUtc,
        ["actualRevision"] = s.ActualRevision,
        ["reconcileRequired"] = s.ReconcileRequired,
        ["resumeCount"] = s.ResumeCount,
    };

    private static Dictionary<string, object?> ItemRecord(FolderJobItem i) => new()
    {
        ["t"] = "item",
        ["index"] = i.Index,
        ["tool"] = i.Tool,
        ["target"] = i.Target,
        ["state"] = i.State,
        ["reason"] = i.Reason,
        ["errorCode"] = i.ErrorCode,
        ["artifact"] = i.Artifact,
        ["sha256"] = i.Sha256,
        ["durationMs"] = i.DurationMs,
    };

    private static Dictionary<string, object?> TickRecord(FolderJobState s) => new()
    {
        ["t"] = "tick",
        ["done"] = s.Done,
        ["failed"] = s.Failed,
        ["skipped"] = s.Skipped,
        ["pending"] = s.Pending,
        ["currentShard"] = s.CurrentShard,
        ["shardCount"] = s.ShardCount,
        ["resumeToken"] = s.ResumeToken,
        ["lastError"] = s.LastError,
        ["lastResumedSkips"] = s.LastResumedSkips,
        ["cancelRequested"] = s.CancelRequested,
        ["cancelled"] = s.Cancelled,
        ["cancelReason"] = s.CancelReason,
        ["resumeCount"] = s.ResumeCount,
        ["updatedUtc"] = s.UpdatedUtc,
        ["fencingGeneration"] = s.FencingGeneration,
        ["expectedRevision"] = s.ExpectedRevision,
        ["actualRevision"] = s.ActualRevision,
        ["reconcileRequired"] = s.ReconcileRequired,
    };

    private static string? ReadString(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int ReadInt(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;

    private static bool ReadBool(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False && v.GetBoolean();

    private static long ReadLong(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    /// <summary>断点载体路径：日志优先（D-079 新 job），其次单文件快照（旧 job）。</summary>
    public static string? TryCheckpointPath(string jobId)
    {
        try
        {
            var dir = ResolveDir();
            var journal = Path.Combine(dir, jobId + ".jsonl");
            var snapshot = Path.Combine(dir, jobId + ".json");
            if (File.Exists(journal))
            {
                return journal;
            }

            if (File.Exists(snapshot))
            {
                return snapshot;
            }

            return Jobs.TryGetValue(jobId, out var s) && !s.Journal ? snapshot : journal;
        }
        catch
        {
            return null;
        }
    }
}

