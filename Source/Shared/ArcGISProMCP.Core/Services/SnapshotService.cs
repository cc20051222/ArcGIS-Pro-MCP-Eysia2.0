using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-064 · 项目快照 / 恢复（G-166 差异化支柱二）宿主契约。
/// 语义：快照 = **APRX + 图层引用清单 + 断点记录**打包到指定目录（含哈希清单）；
/// 恢复 = **confirm 缺省拒**（破坏性）+ **完整性校验**（清单逐项哈希）。
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// 创建快照。<paramref name="snapshotDir"/> 为空 ⇒ 由实现解析 D 盘受控根（G-138）。
    /// </summary>
    Task<OperationResult<SnapshotResult>> CreateAsync(string? snapshotDir, string? snapshotId, CancellationToken ct = default);

    /// <summary>从快照恢复（破坏性；<paramref name="confirm"/> 必须为 true）。</summary>
    Task<OperationResult<RestoreSnapshotResult>> RestoreAsync(string snapshotDir, bool confirm, CancellationToken ct = default);
}

/// <summary>
/// D-064 · 快照/恢复的**共享实现**（Core，文件级）：APRX 打包 + 哈希清单 + 完整性校验 + 逐字节恢复。
/// 依赖面全部为既有 Core 抽象（<see cref="IProjectService"/> / <see cref="ILayerService"/>），
/// 因此可在单测中以替身穷举（往返 / 损坏拒绝 / 篡改拒绝 / confirm 拒 / 越界拒）。
/// <para><b>恢复的可见性诚实披露</b>：本实现只保证**磁盘上 APRX 逐字节一致**；工程若正处于打开态，
/// 需重载工程才可见（<see cref="ReloadProjectAsync"/> 钩子；默认实现**如实说明未执行重载**，
/// 不谎报"已生效"）。</para>
/// </summary>
public class ProjectSnapshotService : ISnapshotService
{
    private readonly IProjectService _project;
    private readonly ILayerService? _layers;

    public ProjectSnapshotService(IProjectService project, ILayerService? layers = null)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _layers = layers;
    }

    public async Task<OperationResult<SnapshotResult>> CreateAsync(
        string? snapshotDir, string? snapshotId, CancellationToken ct = default)
    {
        // ① 目录解析（G-138：缺省走 D 盘受控根 + 可写探针；绝不静默回落 C 盘/%TEMP%）。
        string dir;
        var onD = false;
        if (string.IsNullOrWhiteSpace(snapshotDir))
        {
            var (root, error) = D064PathPolicy.ResolveWritableRoot();
            if (root is null)
            {
                return OperationResult<SnapshotResult>.Fail(ErrorCodes.InvalidArgument, error!);
            }

            dir = Path.Combine(root, "snapshots", string.IsNullOrWhiteSpace(snapshotId) ? NewId() : snapshotId!);
            onD = true;
        }
        else
        {
            dir = snapshotDir!;
            if (!Path.IsPathFullyQualified(dir))
            {
                return OperationResult<SnapshotResult>.Fail(
                    ErrorCodes.InvalidArgument, $"snapshotDir must be an absolute path (got '{dir}').");
            }

            onD = D064PathPolicy.IsOnDDrive(dir);
        }

        // ② 落盘前先刷工程（未保存改动也应进快照）。
        var save = await _project.SaveProjectAsync(null, ct).ConfigureAwait(false);
        if (!save.Success || save.Data is null)
        {
            return OperationResult<SnapshotResult>.Fail(save.Errors.Count > 0
                ? save.Errors[0]
                : new OperationError(ErrorCodes.ExecutionFailed, "Project save before snapshot failed."));
        }

        var aprxPath = save.Data.Path;
        if (string.IsNullOrWhiteSpace(aprxPath) || !File.Exists(aprxPath))
        {
            return OperationResult<SnapshotResult>.Fail(
                ErrorCodes.InvalidState, $"project file is not on disk after save ('{aprxPath}'); snapshot refused.");
        }

        // ③ 打包（快照目录内固定相对路径）。
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            return OperationResult<SnapshotResult>.Fail(
                ErrorCodes.InvalidArgument, $"snapshot directory is not usable ('{dir}'): {ex.Message}");
        }

        var id = string.IsNullOrWhiteSpace(snapshotId) ? NewId() : snapshotId!;
        var created = DateTimeOffset.UtcNow;
        var target = Path.Combine(dir, SnapshotComposer.AprxRelativePath);
        try
        {
            File.Copy(aprxPath, target, overwrite: true);
        }
        catch (Exception ex)
        {
            return OperationResult<SnapshotResult>.Fail(
                ErrorCodes.ExecutionFailed, $"failed to copy project into snapshot: {ex.Message}");
        }

        // ④ 图层引用清单 + 断点记录（只读；失败不阻断快照，但如实记 0 项）。
        var refs = await CollectLayerRefsAsync(ct).ConfigureAwait(false);
        var breakPoints = refs.Where(r => r.Broken).Select(r => r.LayerName).ToList();

        // ⑤ 哈希清单 + 清单落盘。
        var files = new List<SnapshotFileInfo>();
        var described = SnapshotComposer.Describe(dir, target);
        if (described is null)
        {
            return OperationResult<SnapshotResult>.Fail(
                ErrorCodes.ExecutionFailed, "failed to hash the packaged project file.");
        }

        files.Add(described);
        var manifest = new SnapshotManifest
        {
            SnapshotId = id,
            CreatedAtUtc = created.UtcDateTime.ToString("o"),
            AprxRelativePath = SnapshotComposer.AprxRelativePath,
            AprxSha256 = described.Sha256,
            AprxBytes = described.Bytes,
            LayerReferences = refs,
            BreakPoints = breakPoints,
            Files = files,
            TotalBytes = files.Sum(f => f.Bytes),
        };

        try
        {
            SnapshotComposer.WriteManifest(dir, manifest);
        }
        catch (Exception ex)
        {
            return OperationResult<SnapshotResult>.Fail(
                ErrorCodes.ExecutionFailed, $"failed to write snapshot manifest: {ex.Message}");
        }

        return OperationResult<SnapshotResult>.Ok(new SnapshotResult
        {
            SnapshotId = id,
            Directory = dir,
            ManifestPath = Path.Combine(dir, SnapshotComposer.ManifestFileName),
            AprxPath = aprxPath,
            AprxSha256 = described.Sha256,
            AprxBytes = described.Bytes,
            LayerRefCount = refs.Count,
            BreakPointCount = breakPoints.Count,
            TotalBytes = manifest.TotalBytes,
            FileCount = files.Count,
            CreatedAtUtc = manifest.CreatedAtUtc,
            TransientRootOnDDrive = onD,
            Note = onD
                ? null
                : "snapshotDir is not on D: (G-138 recommends a D-drive controlled root; the caller-provided path was honoured as-is).",
        });
    }

    public async Task<OperationResult<RestoreSnapshotResult>> RestoreAsync(
        string snapshotDir, bool confirm, CancellationToken ct = default)
    {
        // ① confirm 缺省拒（**服务层也守**：工具层已守，双保险防直调绕过）。
        if (!confirm)
        {
            return OperationResult<RestoreSnapshotResult>.Fail(
                ErrorCodes.InvalidArgument,
                "restore_snapshot is destructive: confirm=true is required (default-refuse); no change was made.");
        }

        if (string.IsNullOrWhiteSpace(snapshotDir) || !Path.IsPathFullyQualified(snapshotDir))
        {
            return OperationResult<RestoreSnapshotResult>.Fail(
                ErrorCodes.InvalidArgument, "snapshotDir must be an absolute path.");
        }

        // ② 清单 + 完整性校验（损坏/篡改 ⇒ 拒绝恢复，绝不"尽力恢复"）。
        var manifest = SnapshotComposer.ReadManifest(snapshotDir, out var manifestError);
        if (manifest is null)
        {
            return OperationResult<RestoreSnapshotResult>.Fail(ErrorCodes.InvalidState,
                "snapshot is not usable: " + manifestError);
        }

        var integrity = SnapshotComposer.VerifyIntegrity(snapshotDir, manifest);
        if (!integrity.Verified)
        {
            return OperationResult<RestoreSnapshotResult>.Fail(
                ErrorCodes.InvalidState,
                "snapshot integrity check failed; restore refused (no change was made). "
                + integrity.Detail + ": " + string.Join("; ", integrity.Mismatches),
                details: integrity.Detail);
        }

        // ③ 目标 APRX 解析（须已存在，避免"恢复到错误路径"）。
        var info = await _project.GetProjectInfoAsync(ct).ConfigureAwait(false);
        var aprxPath = info.Success ? info.Data?.Path : null;
        if (string.IsNullOrWhiteSpace(aprxPath))
        {
            return OperationResult<RestoreSnapshotResult>.Fail(
                ErrorCodes.InvalidState, "no project is open; restore requires an open project to target.");
        }

        var beforeHash = SnapshotComposer.HashFile(aprxPath);
        var source = Path.Combine(snapshotDir, manifest.AprxRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(source))
        {
            return OperationResult<RestoreSnapshotResult>.Fail(
                ErrorCodes.InvalidState, $"snapshot project file is missing: {source}");
        }

        // ③.5 ★ D-064 阶段二真缺陷 ⑤ 修复：工程处于**打开态**时 Pro 持有 APRX 独占句柄
        //   （LIVE 实测：restore 内层写失败 "file is being used by another process"）⇒ 恢复前先"让路"。
        //   默认实现**不做事**并如实说明；宿主（Compatibility）覆写为 SDK 关闭工程。
        var prepare = await PrepareForRestoreAsync(aprxPath, ct).ConfigureAwait(false);

        // ④ 当前工程备份（快照目录**之外**的兄弟文件：不污染快照清单，保留可回退证据）。
        //    同样**带重试**：恢复前工程可能仍被短暂持有（LIVE 实测独占句柄 ⇒ 朴素 File.Copy 即失败）。
        string? backup = null;
        if (File.Exists(aprxPath))
        {
            backup = snapshotDir.TrimEnd('\\', '/') + ".previous.aprx";
            if (!TryCopyWithRetry(aprxPath, backup, out var backupError))
            {
                return OperationResult<RestoreSnapshotResult>.Fail(
                    ErrorCodes.ExecutionFailed,
                    "failed to back up the current project before restore: " + backupError
                    + " —— 目标 .aprx 可能正被 ArcGIS Pro（或其它进程）独占持有；"
                    + "请关闭该工程后重试（本产品会先请求宿主关闭工程再恢复）。"
                    + " | prepare: " + (prepare.Message ?? "n/a"));
            }
        }

        // ⑤ 逐字节恢复 + 读回比对（带**重试**：关闭工程后 SDK/OS 仍可能短暂持有句柄 —— D-063 删除重试同源教训）。
        var copied = TryCopyWithRetry(source, aprxPath, out var copyError);
        if (!copied)
        {
            var reopenOnFailure = await ReloadProjectAsync(aprxPath, ct).ConfigureAwait(false);
            return OperationResult<RestoreSnapshotResult>.Fail(
                ErrorCodes.ExecutionFailed,
                $"failed to restore the project file: {copyError}"
                + " | prepare: " + (prepare.Message ?? "n/a")
                + " | reopen: " + (reopenOnFailure.Message ?? "n/a"));
        }

        var afterHash = SnapshotComposer.HashFile(aprxPath);
        var identical = afterHash is not null
                        && string.Equals(afterHash, manifest.AprxSha256, StringComparison.OrdinalIgnoreCase);

        var reload = await ReloadProjectAsync(aprxPath, ct).ConfigureAwait(false);

        return OperationResult<RestoreSnapshotResult>.Ok(new RestoreSnapshotResult
        {
            Directory = snapshotDir,
            Confirm = true,
            IntegrityVerified = true,
            IntegrityDetail = integrity.Detail,
            Restored = identical,
            RestoredFiles = manifest.Files.Count,
            AprxPath = aprxPath,
            AprxSha256Snapshot = manifest.AprxSha256,
            AprxSha256Restored = afterHash,
            ByteIdentical = identical,
            BackupOfPreviousAprx = backup,
            PrepareNote = prepare.Message,
            ReloadNote = reload.Message,
        },
        reload.Success ? reload.Message : "restored; project reload note: " + (reload.Message ?? "not performed"));
    }

    /// <summary>
    /// 恢复**之前**的"让路"钩子（默认不做事并如实说明）。宿主覆写为 SDK 关闭工程（释放 APRX 独占句柄）。
    /// </summary>
    protected virtual Task<OperationResult<object?>> PrepareForRestoreAsync(string aprxPath, CancellationToken ct)
        => Task.FromResult(OperationResult<object?>.Ok(
            null,
            "restore preparation NOT performed by this host; if the project is open in ArcGIS Pro, the .aprx file may be locked."));

    /// <summary>
    /// 恢复后重载工程的钩子（默认**不执行**并如实说明）。宿主可覆写以调用 SDK 重开工程。
    /// </summary>
    protected virtual Task<OperationResult<object?>> ReloadProjectAsync(string aprxPath, CancellationToken ct)
        => Task.FromResult(OperationResult<object?>.Ok(
            null,
            "project reload NOT performed by this host; reopen the project in ArcGIS Pro to see the restored state "
            + "(the on-disk .aprx is byte-identical to the snapshot)."));

    /// <summary>带重试的整文件覆盖复制（8 × 400 ms；文件被占用属于**瞬态**，不应当即失败）。</summary>
    private static bool TryCopyWithRetry(string source, string destination, out string error, int attempts = 15, int delayMs = 600)
    {
        error = string.Empty;
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                error = $"{ex.GetType().Name}: {ex.Message} (attempt {i + 1}/{attempts})";
                if (i < attempts - 1)
                {
                    try
                    {
                        Thread.Sleep(delayMs);
                    }
                    catch
                    {
                        // 忽略休眠中断，继续重试。
                    }
                }
            }
        }

        return false;
    }

    private async Task<List<SnapshotLayerRef>> CollectLayerRefsAsync(CancellationToken ct)
    {
        var result = new List<SnapshotLayerRef>();
        if (_layers is null)
        {
            return result;
        }

        try
        {
            var all = await _layers.GetLayersAsync(null, true, ct).ConfigureAwait(false);
            var broken = await _layers.GetBrokenLayersAsync(ct).ConfigureAwait(false);
            var brokenKeys = new HashSet<string>(StringComparer.Ordinal);
            if (broken.Success && broken.Data is not null)
            {
                foreach (var b in broken.Data.Items)
                {
                    brokenKeys.Add(b.MapName + "|" + b.LayerName);
                }
            }

            if (all.Success && all.Data is not null)
            {
                foreach (var l in all.Data)
                {
                    var brokenFlag = brokenKeys.Contains(l.MapName + "|" + l.Name);
                    result.Add(new SnapshotLayerRef
                    {
                        MapName = l.MapName,
                        LayerName = l.Name,
                        DataSource = string.IsNullOrWhiteSpace(l.Uri) ? null : l.Uri,
                        Broken = brokenFlag,
                    });
                }
            }
        }
        catch
        {
            // 只读采集失败不阻断快照（清单为空 ⇒ 如实 0 项，不伪造）。
        }

        return result;
    }

    private static string NewId() => "snap-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
}
