using System.Net.Sockets;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-064 · <c>diagnose</c>（G-166 差异化支柱一）的**事实包**：全部由调用方（宿主/测试）注入，
/// 组合层不做任何 IO —— 因此"健康态全绿 / 人为制态检出"两类判据均可在单测中穷举。
/// </summary>
public sealed class DiagnosticsFacts
{
    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; }

    /// <summary>监听态三态：true=监听中；false=未监听；null=不可判定（不得伪装为 false/true）。</summary>
    public bool? ServerListening { get; init; }

    public string? ServerListenerDetail { get; init; }

    /// <summary>插件（Add-in）加载态：true=已加载 Host 上下文；null=不可判定。</summary>
    public bool? HostContextLoaded { get; init; }

    public string? ProductVersion { get; init; }

    public string? ArcGISProVersion { get; init; }

    /// <summary>支持区间（声明式；兼容性判定用）。</summary>
    public string? SupportedProVersionRange { get; init; } = "3.0–3.5";

    public string? ProjectPath { get; init; }

    public string? DefaultGdb { get; init; }

    /// <summary>运行中的插件程序集路径（AssemblyCache 侧）。</summary>
    public string? LoadedAssemblyPath { get; init; }

    public string? LoadedAssemblySha256 { get; init; }

    /// <summary>
    /// 已安装 Add-in 载荷路径（安装位侧）。
    /// <b>恒等比对口径（D-064 阶段二真缺陷 ① 修复）</b>：<see cref="InstalledPayloadSha256"/> 是**包内
    /// <c>Install/ArcGISProMCP.Compatibility.dll</c> 条目**的哈希（与 <see cref="LoadedAssemblySha256"/>
    /// **同物同口径**）——包 ZIP 自身的哈希不可与之直接比较（阶段一原实现拿 ZIP 哈希比 DLL 哈希 ⇒ 恒报 STALE 假警报）。
    /// </summary>
    public string? InstalledPayloadPath { get; init; }

    /// <summary>包内 `Install/…Compatibility.dll` 的哈希（与加载侧同物同口径）。</summary>
    public string? InstalledPayloadSha256 { get; init; }

    /// <summary>安装包（.esriAddInX ZIP）自身哈希（**仅作追溯披露**，不参与恒等判定）。</summary>
    public string? InstalledPackageSha256 { get; init; }

    /// <summary>GDB 锁统计：*.lock 文件数（null = 无法枚举）。</summary>
    public int? GdbLockCount { get; init; }

    public string? GdbLockDetail { get; init; }

    /// <summary>环境门：中转根可写（G-138；null = 不可判定）。</summary>
    public bool? TransientRootWritable { get; init; }

    public string? TransientRootPath { get; init; }

    public string? TransientRootDetail { get; init; }

    /// <summary>环境门：受保护根环境变量是否已配置。</summary>
    public bool? ProtectedRootsConfigured { get; init; }

    public bool ReadOnlyMode { get; init; }

    /// <summary>已注册工具总数（与写类分层名册核对，防漏项）。</summary>
    public int RegisteredToolCount { get; init; }

    public string? RegisteredToolDetail { get; init; }
}

/// <summary>
/// D-064 · <c>diagnose</c> 的组合层（**纯函数**）：把 <see cref="DiagnosticsFacts"/> 折算为
/// 结构化体检报告（各项状态 + 处置建议 + 证据）。只读，不产生任何写面。
/// </summary>
public static class DiagnosticsComposer
{
    public const string StatusOk = "Ok";
    public const string StatusWarn = "Warn";
    public const string StatusFail = "Fail";
    public const string StatusUnknown = "Unknown";

    /// <summary>组合体检报告。</summary>
    public static DiagnosticsReport Compose(DiagnosticsFacts facts, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var checks = new List<DiagnosticCheck>
        {
            Server(facts),
            Plugin(facts),
            AssemblyIdentity(facts),
            GeodatabaseLocks(facts),
            EnvironmentGate(facts),
            ArcGisVersion(facts),
            Project(facts),
            ReadOnlyMode(facts),
        };

        return new DiagnosticsReport
        {
            GeneratedAtUtc = generatedAt.UtcDateTime.ToString("o"),
            OverallStatus = Worst(checks),
            ProductVersion = facts.ProductVersion,
            ArcGISProVersion = facts.ArcGISProVersion,
            ProjectPath = facts.ProjectPath,
            ReadOnlyMode = facts.ReadOnlyMode,
            Checks = checks,
        };
    }

    /// <summary>整体态 = 各项最差（Unknown 单独归类，不与 Fail 混同）。</summary>
    public static string Worst(IReadOnlyList<DiagnosticCheck> checks)
    {
        if (checks.Count == 0)
        {
            return StatusUnknown;
        }

        if (checks.Any(c => c.Status == StatusFail))
        {
            return StatusFail;
        }

        if (checks.Any(c => c.Status == StatusWarn))
        {
            return StatusWarn;
        }

        return checks.Any(c => c.Status == StatusUnknown) ? StatusUnknown : StatusOk;
    }

    private static DiagnosticCheck Server(DiagnosticsFacts f)
    {
        var status = f.ServerListening switch
        {
            true => StatusOk,
            false => StatusFail,
            _ => StatusUnknown,
        };

        return new DiagnosticCheck
        {
            Id = "server",
            Name = "MCP 服务监听态",
            Status = status,
            Detail = f.ServerListening switch
            {
                true => $"listening on {f.Host}:{f.Port}",
                false => $"NOT listening on {f.Host}:{f.Port}" + (f.ServerListenerDetail is null ? string.Empty : $" ({f.ServerListenerDetail})"),
                _ => "listener state not determinable" + (f.ServerListenerDetail is null ? string.Empty : $" ({f.ServerListenerDetail})"),
            },
            RecommendedAction = status == StatusOk
                ? null
                : $"确认端口 {f.Port} 未被占用且插件已加载；必要时释放端口或改用其它端口后重启 Pro。",
            Evidence = new Dictionary<string, object?>
            {
                ["host"] = f.Host,
                ["port"] = f.Port,
                ["listening"] = f.ServerListening,
            },
        };
    }

    private static DiagnosticCheck Plugin(DiagnosticsFacts f)
        => new()
        {
            Id = "plugin",
            Name = "插件加载态",
            Status = f.HostContextLoaded switch
            {
                true => StatusOk,
                false => StatusFail,
                _ => StatusUnknown,
            },
            Detail = f.HostContextLoaded switch
            {
                true => "ArcGIS Pro host context loaded (add-in active)",
                false => "ArcGIS Pro host context NOT loaded (add-in inactive)",
                _ => "host context state not determinable",
            },
            RecommendedAction = f.HostContextLoaded == true
                ? null
                : "在 ArcGIS Pro 中确认插件已加载（加载项管理器），必要时重装后重启 Pro。",
            Evidence = new Dictionary<string, object?>
            {
                ["hostContextLoaded"] = f.HostContextLoaded,
                ["productVersion"] = f.ProductVersion,
            },
        };

    private static DiagnosticCheck AssemblyIdentity(DiagnosticsFacts f)
    {
        var bothKnown = !string.IsNullOrWhiteSpace(f.LoadedAssemblySha256)
                        && !string.IsNullOrWhiteSpace(f.InstalledPayloadSha256);
        var same = bothKnown
                   && string.Equals(f.LoadedAssemblySha256, f.InstalledPayloadSha256, StringComparison.OrdinalIgnoreCase);

        string status;
        string detail;
        if (!bothKnown)
        {
            status = StatusUnknown;
            detail = "assembly identity not determinable (hash missing on one or both sides)";
        }
        else if (same)
        {
            status = StatusOk;
            detail = "AssemblyCache DLL == package Install/…Compatibility.dll (IN_SYNC; like-for-like comparison)";
        }
        else
        {
            status = StatusFail;
            detail = "AssemblyCache DLL != package Install/…Compatibility.dll (STALE: 运行中插件与安装位不一致)";
        }

        return new DiagnosticCheck
        {
            Id = "assembly-identity",
            Name = "AssemblyCache ↔ 安装位哈希比对",
            Status = status,
            Detail = detail,
            RecommendedAction = status == StatusOk
                ? null
                : "重启 ArcGIS Pro 使 AssemblyCache 与安装位同步；若仍不一致，重装 Add-in。",
            Evidence = new Dictionary<string, object?>
            {
                ["loadedAssembly"] = f.LoadedAssemblyPath,
                ["loadedSha256"] = f.LoadedAssemblySha256,
                ["installedPayload"] = f.InstalledPayloadPath,
                ["installedPayloadDllSha256"] = f.InstalledPayloadSha256,
                ["installedPackageZipSha256"] = f.InstalledPackageSha256,
                ["comparedArtifacts"] = "AssemblyCache/…Compatibility.dll ↔ <package>/Install/…Compatibility.dll (同物同口径)",
                ["inSync"] = bothKnown ? same : null,
            },
        };
    }

    private static DiagnosticCheck GeodatabaseLocks(DiagnosticsFacts f)
    {
        var status = f.GdbLockCount switch
        {
            null => StatusUnknown,
            0 => StatusOk,
            _ => StatusWarn,
        };

        return new DiagnosticCheck
        {
            Id = "gdb-locks",
            Name = "GDB 锁统计",
            Status = status,
            Detail = f.GdbLockCount is null
                ? "lock enumeration not available" + (f.GdbLockDetail is null ? string.Empty : $" ({f.GdbLockDetail})")
                : $"{f.GdbLockCount} lock file(s) under {f.DefaultGdb}".TrimEnd(),
            RecommendedAction = status == StatusWarn
                ? "存在锁文件：确认无其它进程占用该 GDB；必要时关闭残留进程后重试。"
                : null,
            Evidence = new Dictionary<string, object?>
            {
                ["gdb"] = f.DefaultGdb,
                ["lockCount"] = f.GdbLockCount,
            },
        };
    }

    private static DiagnosticCheck EnvironmentGate(DiagnosticsFacts f)
    {
        var writable = f.TransientRootWritable;
        var protectedConfigured = f.ProtectedRootsConfigured;

        string status;
        if (writable == false)
        {
            status = StatusFail;
        }
        else if (writable is null || protectedConfigured is null)
        {
            status = StatusUnknown;
        }
        else if (protectedConfigured == false)
        {
            status = StatusWarn;
        }
        else
        {
            status = StatusOk;
        }

        return new DiagnosticCheck
        {
            Id = "environment-gate",
            Name = "环境门（G-138 全 D 盘 / 受保护根）",
            Status = status,
            Detail = $"transientRoot={f.TransientRootPath ?? "unresolved"}; writable={writable}; protectedRootsConfigured={protectedConfigured}"
                     + (f.TransientRootDetail is null ? string.Empty : $"; {f.TransientRootDetail}"),
            RecommendedAction = status switch
            {
                StatusFail => "设置 ARCGIS_PRO_MCP_TRANSIENT_ROOT 指向可写的 D 盘目录（禁 C 盘/%TEMP%）。",
                StatusWarn => "可选：设置 ARCGIS_PRO_MCP_PROTECTED_ROOTS 追加受保护根以收紧输出面。",
                StatusUnknown => "确认 D 盘可用且中转根可写（可写性探针失败时不得静默回落 C 盘）。",
                _ => null,
            },
            Evidence = new Dictionary<string, object?>
            {
                ["transientRoot"] = f.TransientRootPath,
                ["transientRootWritable"] = writable,
                ["protectedRootsConfigured"] = protectedConfigured,
                ["tempRoots"] = D064PathPolicy.TempRoots(),   // G-138：判定所用临时根（多根）
            },
        };
    }

    private static DiagnosticCheck ArcGisVersion(DiagnosticsFacts f)
        => new()
        {
            Id = "arcgis-version",
            Name = "ArcGIS Pro 版本",
            Status = string.IsNullOrWhiteSpace(f.ArcGISProVersion) ? StatusUnknown : StatusOk,
            Detail = string.IsNullOrWhiteSpace(f.ArcGISProVersion)
                ? "version not determinable"
                : $"Pro {f.ArcGISProVersion} (supported: {f.SupportedProVersionRange})",
            RecommendedAction = string.IsNullOrWhiteSpace(f.ArcGISProVersion)
                ? "确认 ArcGIS Pro 已启动且插件可读版本信息。"
                : null,
            Evidence = new Dictionary<string, object?>
            {
                ["arcgisProVersion"] = f.ArcGISProVersion,
                ["supportedRange"] = f.SupportedProVersionRange,
            },
        };

    private static DiagnosticCheck Project(DiagnosticsFacts f)
        => new()
        {
            Id = "project",
            Name = "工程与默认地理数据库",
            Status = string.IsNullOrWhiteSpace(f.ProjectPath) ? StatusWarn : StatusOk,
            Detail = string.IsNullOrWhiteSpace(f.ProjectPath)
                ? "no project open"
                : $"project={f.ProjectPath}; defaultGdb={f.DefaultGdb}",
            RecommendedAction = string.IsNullOrWhiteSpace(f.ProjectPath)
                ? "打开一个工程（.aprx）后再调用需要工程上下文的工具。"
                : null,
            Evidence = new Dictionary<string, object?>
            {
                ["projectPath"] = f.ProjectPath,
                ["defaultGdb"] = f.DefaultGdb,
            },
        };

    private static DiagnosticCheck ReadOnlyMode(DiagnosticsFacts f)
    {
        var registryOk = f.RegisteredToolCount == ToolWriteClassification.Total;

        return new DiagnosticCheck
        {
            Id = "readonly-mode",
            Name = "只读模式与写类分层覆盖",
            Status = registryOk ? StatusOk : StatusFail,
            Detail = $"readOnlyMode={f.ReadOnlyMode}; classification={ToolWriteClassification.Total} "
                     + $"(read={ToolWriteClassification.ReadTools.Count}/session={ToolWriteClassification.SessionTools.Count}/write={ToolWriteClassification.WriteTools.Count}); "
                     + $"registered={f.RegisteredToolCount}",
            RecommendedAction = registryOk
                ? null
                : "写类分层名册与注册表不一致（有工具未被分类）；补齐 ToolWriteClassification 后重跑。",
            Evidence = new Dictionary<string, object?>
            {
                ["readOnlyMode"] = f.ReadOnlyMode,
                ["writeToolCount"] = ToolWriteClassification.WriteTools.Count,
                ["readToolCount"] = ToolWriteClassification.ReadTools.Count,
                ["sessionToolCount"] = ToolWriteClassification.SessionTools.Count,
                ["registeredToolCount"] = f.RegisteredToolCount,
            },
        };
    }

    /// <summary>端口监听探针（只读、限时 500 ms；不做任何写入）。</summary>
    public static bool? ProbeListening(string host, int port, int timeoutMs = 500)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0)
        {
            return null;
        }

        // ★ D-064 阶段二真缺陷 ⑬：单次 500 ms 探针在宿主繁忙（UI 线程阻塞/GC）时会**假阴性** ——
        //   LIVE 实测同一会话内 diagnose 报 NOT listening，而其前后的工具调用全部正常响应。
        //   改为 3 次尝试（任一成功即 true；全部明确拒绝才 false；其余 Unknown），并如实标注口径。
        bool? verdict = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var r = ProbeListeningOnce(host, port, timeoutMs);
            if (r is true)
            {
                return true;
            }

            if (r is false)
            {
                verdict = false;
            }

            try
            {
                Thread.Sleep(250);
            }
            catch
            {
                break;
            }
        }

        return verdict;
    }

    /// <summary>探针口径说明（随 evidence 披露）。</summary>
    public static string ProbeListeningDetail => "tcp-probe 500ms x3 (read-only; no proxy; any-success=true)";

    private static bool? ProbeListeningOnce(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host, port);
            if (!task.Wait(timeoutMs))
            {
                return false;
            }

            return client.Connected;
        }
        catch (AggregateException ex) when (ex.InnerException is SocketException se)
        {
            return se.SocketErrorCode == SocketError.ConnectionRefused ? false : null;
        }
        catch (SocketException se)
        {
            return se.SocketErrorCode == SocketError.ConnectionRefused ? false : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// D-064 · <c>diagnose</c> 宿主服务契约（Compatibility 侧实现：采集事实 → 交
/// <see cref="DiagnosticsComposer"/> 组合）。只读；不提供任何写面。
/// </summary>
public interface IDiagnosticsService
{
    Task<OperationResult<DiagnosticsReport>> DiagnoseAsync(CancellationToken ct = default);
}
