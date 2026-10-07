using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

// ══════════════════════════════════════════════════════════════════════════════
// D-064 · G-166 差异化增补（4 件：diagnose / snapshot_project / restore_snapshot / set_readonly_mode）
//   支柱一（自诊断 + 只读模式）／支柱二（项目快照与恢复）—— 竞品零覆盖。「五件套」同走：单测/守卫/LIVE/反证/审计。
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>D-064 · <b>一键体检</b>（只读）：结构化 JSON 报告 + 逐项处置建议。</summary>
public sealed class DiagnoseTool : McpToolBase
{
    public override string Name => "diagnose";

    public override string Description =>
        "一键体检（**只读**，零状态变更）：输出结构化报告 —— 服务监听态（默认 127.0.0.1:6520）/ 插件加载态 / " +
        "**AssemblyCache ↔ 安装位哈希比对**（IN_SYNC 判定）/ GDB 锁统计 / 环境门（G-138 中转根可写性 + 受保护根）/ " +
        "ArcGIS Pro 版本 / 工程与默认地理数据库 / 只读模式与写类分层覆盖。每项含 status（Ok/Warn/Fail/**Unknown**）、detail、" +
        "recommendedAction、evidence。**不可判定项一律 Unknown，绝不伪装成 Ok**；overallStatus = 各项最差态。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>(),
    };

    protected override string CategoryName => ToolCategories.System;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    protected override bool? RequiresArcGISOverride => false;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        // 优先用宿主侧采集（可拿到 Pro/插件/SDK 事实）；宿主未提供 ⇒ 退化为 Core 侧可自证事实集（如实标注 Unknown）。
        if (context.Host?.Diagnostics is { } svc)
        {
            var r = await svc.DiagnoseAsync(context.CancellationToken).ConfigureAwait(false);
            return ToolResult.From(r);
        }

        var facts = await D064DiagnosticsLocal.BuildAsync(context).ConfigureAwait(false);
        return OperationResult<object?>.Ok(DiagnosticsComposer.Compose(facts, DateTimeOffset.UtcNow));
    }
}

/// <summary>D-064 · 项目快照（APRX + 图层引用清单 + 断点记录 → 哈希清单包）。</summary>
public sealed class SnapshotProjectTool : McpToolBase
{
    public override string Name => "snapshot_project";

    public override string Description =>
        "为当前工程创建**快照**：APRX（先保存以保证最新）+ **图层引用清单** + **断点记录** 打包到目标目录，并写入 SHA256 清单（snapshot.manifest.json）。" +
        "参数：snapshotDir（可选；**绝对路径**。省略 ⇒ 自动解析 **D 盘受控根**（G-138）+ 可写性探针）、snapshotId（可选，缺省 = 时间戳）。" +
        "契约：**快照目录落在 %TEMP% 之下 → PATH_ESCAPE_REJECTED**（G-138）；受保护根 → PATH_ESCAPE_REJECTED；" +
        "D 盘候选链全不可用且未显式给定目录 → INVALID_ARGUMENT（**绝不静默回落 C 盘/%TEMP%**）；" +
        "工程未保存/文件不在位 → INVALID_STATE（不伪造快照）；返回 aprxSha256/aprxBytes/layerRefCount/breakPointCount 等实测事实。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["snapshotDir"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Absolute snapshot directory; omit to use the D-drive controlled root (G-138)." },
            ["snapshotId"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Snapshot id (default: timestamp)." },
        },
    };

    protected override string CategoryName => ToolCategories.Project;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var svc = context.Host?.Snapshots;
        if (svc is null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.NotImplemented, "snapshot_project is not implemented by this host.");
        }

        var dir = ToolArgs.GetString(context, "snapshotDir");
        if (!string.IsNullOrWhiteSpace(dir))
        {
            // 绝对路径判定必须**先于** GetFullPath（否则相对路径会被 CWD 解析成绝对路径，判定形同虚设 —— 单测反证）。
            if (!Path.IsPathFullyQualified(dir!))
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "snapshotDir must be an absolute path.");
            }

            string full;
            try
            {
                full = Path.GetFullPath(dir!);
            }
            catch (Exception ex)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"snapshotDir is not a valid path: {ex.Message}");
            }

            // G-138 硬约束：快照目录**禁** %TEMP%。
            if (D064PathPolicy.IsUnderTemp(full))
            {
                return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                    $"snapshotDir '{full}' is under %TEMP%, which G-138 forbids; refused (no artefacts)."
                    + " checkedTempRoots=" + string.Join(" | ", D064PathPolicy.TempRoots()));
            }

            var hit = ProtectedOutputPathGuard.Match(full);
            if (hit is not null)
            {
                return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                    $"snapshotDir '{full}' is inside a protected root ('{hit}'); refused (no artefacts).");
            }

            dir = full;
        }

        var r = await svc.CreateAsync(dir, ToolArgs.GetString(context, "snapshotId"), context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · 从快照恢复（破坏性 ⇒ confirm 缺省拒 + 完整性校验）。</summary>
public sealed class RestoreSnapshotTool : McpToolBase
{
    public override string Name => "restore_snapshot";

    public override string Description =>
        "从快照恢复工程（**破坏性**：覆盖当前 APRX ⇒ **confirm 必须显式 true**，缺省或 false → INVALID_ARGUMENT 且零变更）。" +
        "参数：snapshotDir（**绝对路径**）、confirm。契约：恢复**前**先做**清单完整性校验**（存在性/字节数/SHA256 逐项 + 未登记文件检测）——" +
        "校验失败（**快照损坏或被篡改**）→ INVALID_STATE 且**拒绝恢复**（绝不\"尽力恢复\"）；快照目录不得位于 %TEMP%（G-138）；" +
        "恢复前自动备份当前 APRX 到快照目录**之外**的兄弟文件（backupOfPreviousAprx，供回退）；" +
        "返回 aprxSha256Snapshot / aprxSha256Restored / **byteIdentical** 作为逐字节一致的证明；工程重载情况如实披露。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["snapshotDir"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Absolute snapshot directory to restore from." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Must be explicitly true; otherwise refused (INVALID_ARGUMENT)." },
        },
        ["required"] = new[] { "snapshotDir", "confirm" }
    };

    protected override string CategoryName => ToolCategories.Project;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var svc = context.Host?.Snapshots;
        if (svc is null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.NotImplemented, "restore_snapshot is not implemented by this host.");
        }

        var dir = ToolArgs.GetString(context, "snapshotDir");
        if (string.IsNullOrWhiteSpace(dir))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "snapshotDir is required.");
        }

        if (!ToolArgs.GetBool(context, "confirm"))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "restore_snapshot is destructive: confirm=true is required (default-refuse); no change was made.");
        }

        if (!Path.IsPathFullyQualified(dir!))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "snapshotDir must be an absolute path.");
        }

        string full;
        try
        {
            full = Path.GetFullPath(dir!);
        }
        catch (Exception ex)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"snapshotDir is not a valid path: {ex.Message}");
        }

        if (D064PathPolicy.IsUnderTemp(full))
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"snapshotDir '{full}' is under %TEMP%, which G-138 forbids; refused (no change made)."
                + " checkedTempRoots=" + string.Join(" | ", D064PathPolicy.TempRoots()));
        }

        var r = await svc.RestoreAsync(full, true, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · 会话级只读模式开关（写类/破坏类工具自动拒绝；错误码零新增）。</summary>
public sealed class SetReadOnlyModeTool : McpToolBase
{
    public override string Name => "set_readonly_mode";

    public override string Description =>
        "开关**会话级只读模式**：开启后所有**写类/破坏类**工具（84 件）在**执行前**被拒绝，返回 PERMISSION_DENIED 并带 `(read-only)` 限定词；" +
        "只读类（56 件）与**会话/视图态**类（如选择、相机、书签跳转，9 件，不落盘）**不受影响**。参数：enabled（boolean；**省略 = 仅查询当前状态**，不改变任何东西）。" +
        "契约：**不落盘**（进程结束/重启即失效）；状态可由本工具与 `diagnose` 读取；**错误码零新增**（复用 PERMISSION_DENIED）；" +
        "返回 readOnlyMode/previous/changed + writeToolCount/readToolCount（拒绝清单规模）。本工具自身可**在只读模式下调用**（否则无法解除只读）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["enabled"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "true = enter read-only mode; false = leave it; omit = query only." },
        },
    };

    protected override string CategoryName => ToolCategories.System;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    protected override bool? RequiresArcGISOverride => false;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var svc = context.ReadOnly;
        if (svc is null)
        {
            return Task.FromResult(OperationResult<object?>.Fail(
                ErrorCodes.InvalidState, "read-only mode service is not available in this execution context."));
        }

        var raw = ToolArgs.GetValue(context, "enabled");
        var previous = svc.IsReadOnly;
        var changed = false;

        if (raw is not null)
        {
            bool enabled;
            switch (raw)
            {
                case bool b:
                    enabled = b;
                    break;
                case string s when bool.TryParse(s, out var sb):
                    enabled = sb;
                    break;
                default:
                    return Task.FromResult(OperationResult<object?>.Fail(
                        ErrorCodes.InvalidArgument, "enabled must be a boolean (true/false) when provided."));
            }

            var before = svc.Set(enabled);
            changed = before != enabled;
            previous = before;
        }

        var payload = new ReadOnlyModeResult
        {
            ReadOnlyMode = svc.IsReadOnly,
            Previous = previous,
            Changed = changed,
            WriteToolCount = svc.WriteToolNames.Count,
            ReadToolCount = svc.ReadToolNames.Count,
            Scope = "session-scoped, in-memory only (not persisted; resets when the process/ArcGIS Pro restarts)",
            Note = ToolWriteClassification.Total + " tool(s) classified: read=" + svc.ReadToolNames.Count
                   + " / session=" + svc.SessionToolNames.Count + " / write=" + svc.WriteToolNames.Count
                   + "; write-tier tools are refused before execution while read-only mode is on.",
        };

        return Task.FromResult(OperationResult<object?>.Ok(payload));
    }
}

/// <summary>D-064 · <c>diagnose</c> 的 Core 侧事实采集（宿主未提供 Diagnostics 服务时的降级路径）。</summary>
internal static class D064DiagnosticsLocal
{
    public static async Task<DiagnosticsFacts> BuildAsync(ToolExecutionContext context)
    {
        var settings = context.Settings;
        var port = settings.Port;
        var listening = DiagnosticsComposer.ProbeListening("127.0.0.1", port);

        string? proVersion = null;
        string? projectPath = null;
        string? defaultGdb = null;
        bool? hostLoaded = null;

        if (context.Host is not null)
        {
            try
            {
                var v = await context.Host.Version.GetVersionAsync(context.CancellationToken).ConfigureAwait(false);
                if (v.Success && v.Data is not null)
                {
                    proVersion = v.Data.ProductVersion;
                }

                var p = await context.Host.Project.GetProjectInfoAsync(context.CancellationToken).ConfigureAwait(false);
                if (p.Success && p.Data is not null)
                {
                    projectPath = string.IsNullOrWhiteSpace(p.Data.Path) ? null : p.Data.Path;
                    defaultGdb = string.IsNullOrWhiteSpace(p.Data.DefaultGeodatabase) ? null : p.Data.DefaultGeodatabase;
                    hostLoaded = true;
                }
                else
                {
                    hostLoaded = false;
                }
            }
            catch
            {
                hostLoaded = null;
            }
        }

        var (root, error) = D064PathPolicy.ResolveWritableRoot();
        var loadedAssembly = typeof(D064DiagnosticsLocal).Assembly.Location;
        var installed = D064DiagnosticsLocalPaths.InstalledPayloadPath();

        return new DiagnosticsFacts
        {
            Host = "127.0.0.1",
            Port = port,
            ServerListening = listening,
            ServerListenerDetail = "tcp-probe 500ms",
            HostContextLoaded = hostLoaded,
            ProductVersion = ManagedCompatibilityFacts.Current.AddInVersion,
            ArcGISProVersion = proVersion,
            ProjectPath = projectPath,
            DefaultGdb = defaultGdb,
            LoadedAssemblyPath = loadedAssembly,
            LoadedAssemblySha256 = SnapshotComposer.HashFile(loadedAssembly),
            InstalledPayloadPath = installed,
            InstalledPayloadSha256 = SnapshotComposer.HashFile(installed),
            GdbLockCount = CountGdbLocks(defaultGdb, out var lockDetail),
            GdbLockDetail = lockDetail,
            TransientRootWritable = root is not null,
            TransientRootPath = root,
            TransientRootDetail = error,
            ProtectedRootsConfigured = !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(ProtectedOutputPathGuard.EnvironmentVariable)),
            ReadOnlyMode = context.ReadOnly?.IsReadOnly ?? false,
            RegisteredToolCount = context.Registry?.Count ?? 0,
        };
    }

    private static int? CountGdbLocks(string? defaultGdb, out string? detail)
    {
        detail = null;
        if (string.IsNullOrWhiteSpace(defaultGdb) || !Directory.Exists(defaultGdb))
        {
            detail = defaultGdb is null ? "no default geodatabase" : "geodatabase not on disk";
            return null;
        }

        try
        {
            return Directory.EnumerateFiles(defaultGdb!, "*.lock", SearchOption.TopDirectoryOnly).Count();
        }
        catch (Exception ex)
        {
            detail = "enumeration failed: " + ex.GetType().Name;
            return null;
        }
    }
}

/// <summary>D-064 · 诊断用路径常量（安装位；纯常量，无 IO）。</summary>
internal static class D064DiagnosticsLocalPaths
{
    /// <summary>ArcGIS Pro Add-in 安装位目录（产品 GUID 固定，随安装账本一致）。</summary>
    public const string AddInFolderName = "{BAA5628C-3C08-4AD5-A6C0-915ADA475709}";

    /// <summary>安装载荷文件名。</summary>
    public const string PayloadFileName = "ArcGISProMCP.Compatibility.esriAddInX";

    /// <summary>解析安装载荷全路径（Documents\ArcGIS\AddIns\ArcGISPro\{GUID}\...esriAddInX）。</summary>
    public static string? InstalledPayloadPath()
    {
        try
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs))
            {
                return null;
            }

            return Path.Combine(docs, "ArcGIS", "AddIns", "ArcGISPro", AddInFolderName, PayloadFileName);
        }
        catch
        {
            return null;
        }
    }
}
