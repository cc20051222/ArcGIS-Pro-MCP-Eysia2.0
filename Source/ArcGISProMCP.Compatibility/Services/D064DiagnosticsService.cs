using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-064 · G-166 支柱一：<c>diagnose</c> 的宿主侧**事实采集**（只读）。
/// <para>采集面（全部只读，零写面）：服务监听态（TCP 探针，禁用代理）、插件/工程加载态、Pro 版本、
/// AssemblyCache（运行中程序集）↔ 安装位（<c>Documents\ArcGIS\AddIns\ArcGISPro\{GUID}\…esriAddInX</c>）哈希比对、
/// GDB 锁统计、环境门（G-138 中转根可写性 + 受保护根配置）、只读模式与写类分层覆盖。
/// 组合与判据全部在 Core 的 <see cref="DiagnosticsComposer"/>（纯函数）内，本类只负责"取事实"。</para>
/// </summary>
public sealed class D064DiagnosticsService : IDiagnosticsService
{
    private readonly MCPSettings _settings;
    private readonly IArcGISVersionService _version;
    private readonly IProjectService _project;
    private readonly IReadOnlyModeService? _readOnly;
    private readonly Func<int>? _registryCount;

    public D064DiagnosticsService(
        MCPSettings settings,
        IArcGISVersionService version,
        IProjectService project,
        IReadOnlyModeService? readOnly = null,
        Func<int>? registryCount = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _version = version ?? throw new ArgumentNullException(nameof(version));
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _readOnly = readOnly;
        _registryCount = registryCount;
    }

    public async Task<OperationResult<DiagnosticsReport>> DiagnoseAsync(CancellationToken ct = default)
    {
        var port = _settings.Port;
        var listening = DiagnosticsComposer.ProbeListening("127.0.0.1", port);

        string? proVersion = null;
        try
        {
            var v = await _version.GetVersionAsync(ct).ConfigureAwait(false);
            if (v.Success && v.Data is not null)
            {
                proVersion = v.Data.ProductVersion;
            }
        }
        catch
        {
            proVersion = null;
        }

        string? projectPath = null;
        string? defaultGdb = null;
        bool? hostLoaded = null;
        try
        {
            var p = await _project.GetProjectInfoAsync(ct).ConfigureAwait(false);
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

        var loadedAssembly = typeof(D064DiagnosticsService).Assembly.Location;
        var installed = InstalledPayloadPath();

        // ★ D-064 阶段二真缺陷 ① 修复：**同物同口径**比对 —— 加载侧 = 运行中 DLL，安装侧 = 包内
        //   `Install/ArcGISProMCP.Compatibility.dll` 条目（而非包 ZIP 自身）。阶段一原实现拿 ZIP 哈希比 DLL 哈希
        //   ⇒ 恒报 STALE 假警报（LIVE 实测暴露）。包 ZIP 哈希另存 `InstalledPackageSha256` 仅作追溯。
        var installedDllSha = ReadPackageEntrySha(installed, "Install/ArcGISProMCP.Compatibility.dll");
        var installedZipSha = installed is not null && File.Exists(installed) ? SnapshotComposer.HashFile(installed) : null;

        var facts = new DiagnosticsFacts
        {
            Host = "127.0.0.1",
            Port = port,
            ServerListening = listening,
            ServerListenerDetail = DiagnosticsComposer.ProbeListeningDetail,
            HostContextLoaded = hostLoaded,
            ProductVersion = ManagedCompatibilityFacts.Current.AddInVersion,
            ArcGISProVersion = proVersion,
            SupportedProVersionRange = "3.0–3.5",
            ProjectPath = projectPath,
            DefaultGdb = defaultGdb,
            LoadedAssemblyPath = loadedAssembly,
            LoadedAssemblySha256 = SnapshotComposer.HashFile(loadedAssembly),
            InstalledPayloadPath = installed,
            InstalledPayloadSha256 = installedDllSha,
            InstalledPackageSha256 = installedZipSha,
            GdbLockCount = CountLocks(defaultGdb, out var lockDetail),
            GdbLockDetail = lockDetail,
            TransientRootWritable = transientWritable,
            TransientRootPath = transientRoot,
            TransientRootDetail = transientError,
            ProtectedRootsConfigured = !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(ProtectedOutputPathGuard.EnvironmentVariable)),
            ReadOnlyMode = _readOnly?.IsReadOnly ?? false,
            RegisteredToolCount = _registryCount?.Invoke() ?? 0,
        };

        return OperationResult<DiagnosticsReport>.Ok(DiagnosticsComposer.Compose(facts, DateTimeOffset.UtcNow));
    }

    // 环境门事实（G-138 可写性探针）。
    private static readonly string? transientRoot;
    private static readonly bool? transientWritable;
    private static readonly string? transientError;

    static D064DiagnosticsService()
    {
        var (root, error) = D064PathPolicy.ResolveWritableRoot();
        transientRoot = root;
        transientWritable = root is not null;
        transientError = error;
    }

    private static int? CountLocks(string? gdb, out string? detail)
    {
        detail = null;
        if (string.IsNullOrWhiteSpace(gdb))
        {
            detail = "no default geodatabase in the current project";
            return null;
        }

        if (!Directory.Exists(gdb))
        {
            detail = "default geodatabase is not on disk";
            return null;
        }

        try
        {
            return Directory.EnumerateFiles(gdb!, "*.lock", SearchOption.TopDirectoryOnly).Count();
        }
        catch (Exception ex)
        {
            detail = "enumeration failed: " + ex.GetType().Name;
            return null;
        }
    }

    /// <summary>读取安装包（.esriAddInX = ZIP）内某条目的 SHA256；包/条目缺失 ⇒ null（判定侧落 Unknown，不伪造）。</summary>
    private static string? ReadPackageEntrySha(string? packagePath, string entryName)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            return null;
        }

        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(packagePath!);
            var entry = zip.GetEntry(entryName);
            if (entry is null)
            {
                return null;
            }

            using var stream = entry.Open();
            using var sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }
        catch
        {
            return null;
        }
    }

    private static string? InstalledPayloadPath()
    {
        try
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs))
            {
                return null;
            }

            return Path.Combine(docs, "ArcGIS", "AddIns", "ArcGISPro",
                "{BAA5628C-3C08-4AD5-A6C0-915ADA475709}", "ArcGISProMCP.Compatibility.esriAddInX");
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// D-064 · G-166 支柱二：项目快照/恢复的宿主实现 = Core <see cref="ProjectSnapshotService"/> +
/// **恢复后工程重载**（SDK：<c>Project.OpenAsync</c>）覆写。
/// </summary>
public sealed class D064SnapshotService : ProjectSnapshotService
{
    private readonly Func<string, CancellationToken, Task<OperationResult<object?>>>? _prepare;
    private readonly Func<string, CancellationToken, Task<OperationResult<object?>>>? _reload;

    public D064SnapshotService(
        IProjectService project,
        ILayerService? layers = null,
        Func<string, CancellationToken, Task<OperationResult<object?>>>? prepare = null,
        Func<string, CancellationToken, Task<OperationResult<object?>>>? reload = null)
        : base(project, layers)
    {
        _prepare = prepare;
        _reload = reload;
    }

    /// <summary>恢复前"让路"：关闭当前工程以释放 APRX 独占句柄（LIVE 实测恢复写失败根因）。</summary>
    protected override Task<OperationResult<object?>> PrepareForRestoreAsync(string aprxPath, CancellationToken ct)
        => _prepare is null ? base.PrepareForRestoreAsync(aprxPath, ct) : _prepare(aprxPath, ct);

    /// <summary>恢复后重开工程（使 UI 立即反映恢复态）。</summary>
    protected override Task<OperationResult<object?>> ReloadProjectAsync(string aprxPath, CancellationToken ct)
        => _reload is null ? base.ReloadProjectAsync(aprxPath, ct) : _reload(aprxPath, ct);
}
