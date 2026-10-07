using System.Diagnostics;
using System.Text;
using ArcGISProMCP.TestSupport;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-058（Phase 14 收官批 · A 项 14.2）安装向导 <c>Distribution/CONFIGURE-CLIENT.cmd</c>。
/// </summary>
/// <remarks>
/// 判据（工单 A）：**逐客户端独立事务**（Plan → Apply → Validate）；**无任何批量应用形态**；单测 ≥4。
/// <list type="number">
/// <item>静态契约：四个客户端分支 + 直达参数 + 无批量路径 + 无 <c>ApplyAll</c> 字样；</item>
/// <item>真实执行（一层 spawn，L1 可用）：codex / cursor / deepseek-harness 各走完整事务（Apply 生效）；</item>
/// <item>claude-desktop：Plan + Validate，**跳过 Apply**（该客户端不支持）⇒ ConfigRoot 无写入；</item>
/// <item>参数直达（<c>[client] [configRoot]</c>）避免交互，且 ConfigRoot 指向 run 目录（不触碰真实用户配置）。</item>
/// </list>
/// </remarks>
public sealed class D058ClientWizardTests
{
    private static string Root => FindRepositoryRoot();

    private static string Wizard => Path.Combine(Root, "Distribution", "CONFIGURE-CLIENT.cmd");

    // ---------- 1) 静态契约：四客户端分支 + 直达 + 无批量形态 ----------
    [Fact]
    public void Wizard_ExposesPerClientIndependentPaths()
    {
        Assert.True(File.Exists(Wizard), "向导缺失: " + Wizard);
        var text = ReadWizardText();

        foreach (var label in new[] { ":PICK_CODEX", ":PICK_CURSOR", ":PICK_DEEPSEEK", ":PICK_CLAUDE" })
        {
            Assert.Contains(label, text, StringComparison.Ordinal);
        }

        // 四个客户端各有一个直达分支（goto 标签分发，避免 `if ... set & goto` 的 & 优先级陷阱）
        foreach (var client in new[] { "codex", "cursor", "deepseek-harness", "claude-desktop" })
        {
            Assert.Contains(client, text, StringComparison.Ordinal);
        }

        // 直达参数（使非交互测试/自动化可用）
        Assert.Contains("DIRECT", text, StringComparison.Ordinal);
        Assert.Contains("CFGROOT", text, StringComparison.Ordinal);

        // 无批量应用形态
        Assert.DoesNotContain("ApplyAll", text, StringComparison.Ordinal);
        Assert.DoesNotContain("all-clients", text, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- 2) codex：完整事务（Plan → Apply → Validate） ----------
    [Fact]
    public void Wizard_Codex_PlanApplyValidateSucceed()
    {
        var root = NewConfigRoot("codex");
        var (code, output) = RunWizard("codex", root);

        Assert.Equal(0, code);
        Assert.Contains("Plan", output, StringComparison.Ordinal);
        Assert.Contains("Apply", output, StringComparison.Ordinal);
        Assert.Contains("Validate", output, StringComparison.Ordinal);
        Assert.NotEmpty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));   // Apply 生效
    }

    // ---------- 3) cursor：完整事务 ----------
    [Fact]
    public void Wizard_Cursor_PlanApplyValidateSucceed()
    {
        var root = NewConfigRoot("cursor");
        var (code, _) = RunWizard("cursor", root);
        Assert.Equal(0, code);
        Assert.NotEmpty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    // ---------- 4) deepseek-harness：完整事务 ----------
    [Fact]
    public void Wizard_DeepSeek_PlanApplyValidateSucceed()
    {
        var root = NewConfigRoot("deepseek");
        var (code, _) = RunWizard("deepseek-harness", root);
        Assert.Equal(0, code);
        Assert.NotEmpty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    // ---------- 5) claude-desktop：Plan + Validate，跳过 Apply ----------
    [Fact]
    public void Wizard_ClaudeDesktop_SkipsApplyAndWritesNothing()
    {
        var root = NewConfigRoot("claude");
        var (code, _) = RunWizard("claude-desktop", root);
        Assert.Equal(0, code);
        // Apply 被跳过 ⇒ ConfigRoot 下不应产生配置文件（该客户端在本版本不支持 Apply）
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    // ---------- 6) 无批量应用路径（每次仅一个客户端） ----------
    [Fact]
    public void Wizard_HasNoBatchApplicationPath()
    {
        var text = ReadWizardText();

        // 单次运行只出现一个 -Client 值绑定：CLIENT 只由单客户端分支或直达参数设置
        var sets = text.Split('\n').Count(l => l.Contains("set \"CLIENT=", StringComparison.Ordinal));
        Assert.True(sets <= 6, "CLIENT 赋值点异常（应为 1 直达 + 4 分支，实际 " + sets + "）");

        // 不存在对多个客户端的循环/批量调用
        Assert.DoesNotContain("for %", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-All ", text, StringComparison.OrdinalIgnoreCase);
    }

    // ============================ helpers ============================

    private static string ReadWizardText()
    {
        // 向导为 GBK 编码（cmd.exe 默认代码页）⇒ 用 936 读取，避免中文/符号解析异常
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return File.ReadAllText(Wizard, Encoding.GetEncoding(936));
    }

    private static string NewConfigRoot(string tag)
    {
        var dir = Path.Combine(Root, ".runtime", "evolution", "phase14", "run-20260918-d058-stage1",
            "wizard", tag + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static (int Code, string Output) RunWizard(string client, string configRoot)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.GetEncoding(936),
            StandardErrorEncoding = Encoding.GetEncoding(936),
            CreateNoWindow = true,
            // 关键：`cmd /c` 对含空格路径的引号处理特殊（会把未加引号的整串在空格处截断）⇒
            // 以 Distribution 为工作目录、用**无空格的相对脚本名**调用（脚本内 %~dp0 仍解析为绝对路径）。
            WorkingDirectory = Path.GetDirectoryName(Wizard)!,
        };
        process.StartInfo.ArgumentList.Add("/c");
        process.StartInfo.ArgumentList.Add(Path.GetFileName(Wizard));
        process.StartInfo.ArgumentList.Add(client);
        process.StartInfo.ArgumentList.Add(configRoot);

        process.Start();
        process.StandardInput.Close();   // 立即 EOF：即使走到 pause 也立即返回（无交互环境）
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout + stderr);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ArcGIS-Pro-MCP.sln"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("repository root not found (ArcGIS-Pro-MCP.sln)");
    }
}
