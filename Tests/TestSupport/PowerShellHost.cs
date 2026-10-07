namespace ArcGISProMCP.TestSupport;

/// <summary>
/// 解析可用的 PowerShell 宿主可执行文件（测试基建）。
/// </summary>
/// <remarks>
/// **设计动机（D-054 A1 spike，2026-09-17）**：客户端配置策略 / 兼容性策略测试原先硬编码
/// <c>pwsh.exe</c>（PowerShell 7）。在未安装 PS7 的机器上（仅 Windows PowerShell 5.1），
/// <c>Process.Start</c> 直接抛 <c>Win32Exception</c>（"系统找不到指定的文件"）⇒ 整族测试恒失败，
/// 属**环境不可达**而非产品缺口。
///
/// 解析顺序（确定性、只读）：
/// <list type="number">
/// <item>PATH 上的 <c>pwsh.exe</c>（PS7，若已安装则优先；语义与历史一致）；</item>
/// <item>标准安装位 <c>%ProgramFiles%\PowerShell\7(-preview)\pwsh.exe</c>；</item>
/// <item>Windows 内置 <c>System32\WindowsPowerShell\v1.0\powershell.exe</c>（5.1 兜底，所有 Windows 均有）；</item>
/// <item>PATH 上的 <c>powershell.exe</c>；</item>
/// <item>全部不可得 ⇒ 抛异常（不静默跳过，保持可诊断）。</item>
/// </list>
/// 说明：被调用脚本 <c>scripts/client-config.ps1</c> 等不含 PS7 专属语法（已核：无 <c>??</c> /
/// 三元 / <c>-Parallel</c>），5.1 可承载 ⇒ 兜底不降低断言强度。
/// </remarks>
public static class PowerShellHost
{
    /// <summary>解析得到的 PowerShell 宿主可执行文件绝对路径。</summary>
    public static string Executable { get; } = Resolve();

    /// <summary>当前宿主是否为 PowerShell 7+（<c>pwsh</c>）。false 表示走 5.1 兜底。</summary>
    public static bool IsPowerShell7 =>
        string.Equals(Path.GetFileNameWithoutExtension(Executable), "pwsh", StringComparison.OrdinalIgnoreCase);

    private static string Resolve()
    {
        var fromPath = FindOnPath("pwsh.exe");
        if (fromPath is not null)
        {
            return fromPath;
        }

        foreach (var candidate in new[]
                 {
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\PowerShell\7\pwsh.exe"),
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\PowerShell\7-preview\pwsh.exe"),
                 })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var builtIn = Path.Combine(systemDir, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(builtIn))
        {
            return builtIn;
        }

        var legacy = FindOnPath("powershell.exe");
        if (legacy is not null)
        {
            return legacy;
        }

        throw new InvalidOperationException(
            "No PowerShell host could be located (tried pwsh.exe on PATH, %ProgramFiles%\\PowerShell\\7, " +
            "System32\\WindowsPowerShell\\v1.0\\powershell.exe).");
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var full = Path.Combine(dir, fileName);
                if (File.Exists(full))
                {
                    return full;
                }
            }
            catch (ArgumentException)
            {
                // 非法 PATH 片段 → 跳过（不影响其余候选）
            }
        }

        return null;
    }
}
