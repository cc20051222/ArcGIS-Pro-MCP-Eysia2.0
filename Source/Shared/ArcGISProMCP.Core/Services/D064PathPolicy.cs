namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-064 · 中转/快照目录解析策略（**G-138 硬约束**：禁 C 盘、禁 %TEMP%）。
/// <para><b>D-063 教训前置固化</b>：D-063 阶段二暴露的真缺陷 ① —— 依赖 <c>AppContext.BaseDirectory</c>
/// 推导中转目录，实装后落在 <c>C:\Program Files\ArcGIS\Pro\bin</c>（C 盘、只读，且直接违反 G-138），
/// 回图一律失败。本策略因此**永不**使用 <c>AppContext.BaseDirectory</c>，改为显式候选链 +
/// **可写性探针**（真实创建并删除一个哨兵文件）——全部候选不可用则**明确报错**，绝不静默回落 C 盘/系统临时目录。</para>
/// 候选链（依序探测）：
/// <list type="number">
/// <item>显式 <c>explicitRoot</c>（调用方给定，须在 D 盘且可写）；</item>
/// <item>环境变量 <c>ARCGIS_PRO_MCP_TRANSIENT_ROOT</c>（LIVE 期指向 run 目录）；</item>
/// <item>环境变量 <c>ARCGIS_PRO_MCP_GP_AUDIT_DIR</c> 的父目录下的 <c>transient</c> 子目录；</item>
/// <item>D 盘固定根 <c>&lt;首个存在的 D 盘&gt;\ArcGIS-Pro-MCP-transient</c>。</item>
/// </list>
/// </summary>
public static class D064PathPolicy
{
    /// <summary>中转根环境变量名。</summary>
    public const string TransientRootVariable = "ARCGIS_PRO_MCP_TRANSIENT_ROOT";

    /// <summary>D 盘固定根目录名（兜底候选）。</summary>
    public const string DDriveRootName = "ArcGIS-Pro-MCP-transient";

    /// <summary>可写性探针文件名前缀。</summary>
    private const string ProbeFileName = ".write-probe";

    /// <summary>候选列表（未做可写性判定；供测试与诊断展示）。</summary>
    public static IReadOnlyList<string> Candidates(string? explicitRoot = null)
    {
        var list = new List<string>();
        void Add(string? p)
        {
            if (!string.IsNullOrWhiteSpace(p))
            {
                list.Add(p!);
            }
        }

        Add(explicitRoot);
        Add(Environment.GetEnvironmentVariable(TransientRootVariable));

        var auditDir = Environment.GetEnvironmentVariable(GpAuditLog.AuditDirVariable);
        if (!string.IsNullOrWhiteSpace(auditDir))
        {
            var parent = SafeGetDirectoryName(auditDir!);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Add(Path.Combine(parent!, "transient"));
            }
        }

        foreach (var drive in EnumerateDrives())
        {
            Add(Path.Combine(drive, DDriveRootName));
            break;   // 仅取首个 D 盘
        }

        return list;
    }

    /// <summary>
    /// 解析可写中转根：返回 <c>(path, null)</c> 或 <c>(null, 原因)</c>。
    /// 判定要求：① 路径为**绝对**且在 **D 盘**（G-138）；② **不在** <c>%TEMP%</c> 之下；
    /// ③ 可创建目录并**真实写入哨兵文件**后删除成功。
    /// </summary>
    public static (string? Path, string? Error) ResolveWritableRoot(string? explicitRoot = null)
    {
        var tried = new List<string>();
        foreach (var candidate in Candidates(explicitRoot))
        {
            var full = SafeFullPath(candidate);
            if (full is null)
            {
                tried.Add(candidate + " (unresolvable)");
                continue;
            }

            if (!IsAbsolute(full))
            {
                tried.Add(full + " (not-absolute)");
                continue;
            }

            if (!IsOnDDrive(full))
            {
                tried.Add(full + " (not-D-drive; G-138)");
                continue;
            }

            if (IsUnderTemp(full))
            {
                tried.Add(full + " (under %TEMP%; G-138)");
                continue;
            }

            if (TryProbeWrite(full, out var why))
            {
                return (full, null);
            }

            tried.Add(full + " (" + why + ")");
        }

        return (null,
            "no writable transient root on D: (G-138 forbids C:/%TEMP%). Tried: "
            + string.Join(" | ", tried));
    }

    /// <summary>路径是否位于 D 盘（<c>D:\...</c>）。</summary>
    public static bool IsOnDDrive(string? path)
        => !string.IsNullOrWhiteSpace(path)
           && path.Length >= 2
           && char.ToUpperInvariant(path[0]) == 'D'
           && path[1] == ':'
           && (path.Length == 2 || path[2] == '\\' || path[2] == '/');

    /// <summary>
    /// 系统级临时根集合（**多根**，G-138 禁区判定用）。
    /// <para><b>D-064 阶段二真缺陷 ⑧ 修复</b>：原实现只比对 <c>Path.GetTempPath()</c> 单一根 —— LIVE 实测
    /// 宿主进程的 TEMP/TMP 可能被**重定向**（本机 Harness 为遵守 G-138 把 TEMP 指到 D 盘），
    /// 于是写入 <c><user-home>\AppData\Local\Temp\…</c> 时判定为"不在 %TEMP% 下" ⇒ **C 盘禁区被穿透**。
    /// 现改为枚举全部已知临时根（进程 TEMP/TMP、GetTempPath、<c>%LOCALAPPDATA%\Temp</c>、
    /// <c>%SystemRoot%\Temp</c>、<c>C:\Windows\Temp</c>、<c><user-root>\Public\Temp</c>）。</para>
    /// </summary>
    public static IReadOnlyList<string> TempRoots()
    {
        var roots = new List<string?>();
        try
        {
            roots.Add(Path.GetTempPath());
        }
        catch
        {
            // 忽略：单一来源失败不影响其余根。
        }

        roots.Add(Environment.GetEnvironmentVariable("TEMP"));
        roots.Add(Environment.GetEnvironmentVariable("TMP"));

        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            roots.Add(Path.Combine(localAppData!, "Temp"));
        }

        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (!string.IsNullOrWhiteSpace(systemRoot))
        {
            roots.Add(Path.Combine(systemRoot!, "Temp"));
        }

        roots.Add(@"C:\Windows\Temp");
        roots.Add(@"C:\Users\Public\Temp");

        var resolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in roots)
        {
            if (string.IsNullOrWhiteSpace(r))
            {
                continue;
            }

            var trimmed = r!.Trim().TrimEnd('\\', '/');
            if (trimmed.Length > 0 && seen.Add(trimmed))
            {
                resolved.Add(trimmed);
            }
        }

        return resolved;
    }

    /// <summary>
    /// 路径是否位于**任一**系统临时目录之下（G-138 禁区）。
    /// <para><b>D-064 阶段二真缺陷 ④ 修复</b>：原实现只做朴素前缀比较，**可被 8.3 短名绕过** ——
    /// LIVE 实测把快照写到 <c><user-home-short>\AppData\Local\Temp\d064snap</c>（短名形态）时，
    /// 与 <c>Path.GetTempPath()</c> 的长名形态前缀不同 ⇒ 判定为"不在 %TEMP% 下" ⇒ **G-138 禁区被穿透**。
    /// 现改为**多重规范化**判据：原串 / <c>GetFullPath</c> / **Windows 长名（<c>GetLongPathNameW</c>，含逐级上溯）**
    /// / **短名（<c>GetShortPathNameW</c>）**，任一路径形态与任一临时根的任一形态命中即判真。</para>
    /// </summary>
    public static bool IsUnderTemp(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var forms = CanonicalForms(path).ToArray();
        foreach (var tempRoot in TempRoots())
        {
            foreach (var temp in CanonicalForms(tempRoot))
            {
                foreach (var candidate in forms)
                {
                    if (IsDescendantOrSame(candidate, temp))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>路径的多种规范化形态（去重、去空）：原串 / 全路径 / 长名 / **短名**（各含全路径变体）。</summary>
    private static IEnumerable<string> CanonicalForms(string? path)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var longName = TryGetLongPath(path);
        var shortName = TryGetShortPath(path);
        foreach (var raw in new[]
                 {
                     path, SafeFullPath(path),
                     longName, SafeFullPath(longName),
                     shortName, SafeFullPath(shortName)
                 })
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw!.TrimEnd('\\', '/');
            if (seen.Add(trimmed))
            {
                yield return trimmed;
            }
        }
    }

    private static bool IsDescendantOrSame(string candidate, string root)
    {
        if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(root + '/', StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Windows 8.3 短名 → 长名（失败/非 Windows ⇒ null；不抛出）。
    /// <para><b>D-064 阶段二真缺陷 ④ 加固（第二轮）</b>：<c>GetLongPathNameW</c> 对**尚不存在**的路径会失败
    /// （LIVE 实测：把快照写到 <c>…\Temp\d064snap-short</c>（新目录，短名形态）时转换返回 0 ⇒ 退回原串 ⇒
    /// 仍与长名 temp 根前缀不同 ⇒ **G-138 禁区被穿透，C 盘被写入**）。现改为**逐级上溯**：对不存在的末段，
    /// 先转换其**存在的父级**，再拼回剩余段；这样"未创建的短名目标"也能得到长名形态。</para>
    /// </summary>
    internal static string? TryGetLongPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !OperatingSystem.IsWindows())
        {
            return null;
        }

        var direct = TryGetLongPathExact(path);
        if (direct is not null)
        {
            return direct;
        }

        // 逐级上溯：父级可转换 ⇒ 父长名 + 剩余（原样保留）段
        try
        {
            var full = SafeFullPath(path);
            if (full is null)
            {
                return null;
            }

            var trimmed = full.TrimEnd('\\', '/');
            var leaf = Path.GetFileName(trimmed);
            var parent = Path.GetDirectoryName(trimmed);
            if (string.IsNullOrWhiteSpace(leaf) || string.IsNullOrWhiteSpace(parent))
            {
                return null;
            }

            var parentLong = TryGetLongPath(parent);
            return parentLong is null ? null : Path.Combine(parentLong, leaf);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetLongPathExact(string path)
    {
        try
        {
            var buffer = new char[4096];
            var written = GetLongPathNameW(path, buffer, (uint)buffer.Length);
            return written == 0 || written > buffer.Length ? null : new string(buffer, 0, (int)written);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>长名 → Windows 8.3 短名（失败/非 Windows/卷禁短名 ⇒ null；不抛出）。</summary>
    internal static string? TryGetShortPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var buffer = new char[4096];
            var written = GetShortPathNameW(path!, buffer, (uint)buffer.Length);
            return written == 0 || written > buffer.Length ? null : new string(buffer, 0, (int)written);
        }
        catch
        {
            return null;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathNameW(string lpszShortPath, [System.Runtime.InteropServices.Out] char[] lpszLongPath, uint cchBuffer);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathNameW(string lpszLongPath, [System.Runtime.InteropServices.Out] char[] lpszShortPath, uint cchBuffer);

    /// <summary>可写性探针：创建目录 → 写哨兵 → 删除哨兵（真实 IO，非权限位推断）。</summary>
    public static bool TryProbeWrite(string directory, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(directory))
        {
            reason = "empty-path";
            return false;
        }

        var probe = Path.Combine(directory, ProbeFileName + "." + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex)
        {
            reason = "probe-failed: " + ex.GetType().Name;
            TryDelete(probe);
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 探针清理失败不影响判定结论（已在 reason 中披露原因）。
        }
    }

    private static IEnumerable<string> EnumerateDrives()
    {
        string[] drives;
        try
        {
            drives = Directory.GetLogicalDrives();
        }
        catch
        {
            yield break;
        }

        foreach (var d in drives)
        {
            if (!string.IsNullOrWhiteSpace(d)
                && d.Length >= 2
                && char.ToUpperInvariant(d[0]) == 'D')
            {
                yield return d.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            }
        }
    }

    private static bool IsAbsolute(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path);
        }
        catch
        {
            return false;
        }
    }

    private static string? SafeFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path!);
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeGetDirectoryName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetDirectoryName(path);
        }
        catch
        {
            return null;
        }
    }
}
