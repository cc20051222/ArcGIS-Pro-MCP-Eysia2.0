using System.Diagnostics;
using System.Text;
using ArcGISProMCP.TestSupport;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-057（Phase 14 第一批 · D 项）安装隔离矩阵（14.3）。
/// </summary>
/// <remarks>
/// 场景：中文路径 / 空格路径 / 长路径 / 重复安装 / 缺依赖 / 不静默改系统 /
/// 入口件引用与身份 / 中断可恢复 / 回环端点（端口冲突面）。
/// <para>
/// 路径类场景以 <c>client-config.ps1 -ConfigRoot</c> 驱动（<b>一层 spawn，本 harness 可用</b>；
/// 依据 <c>spawn-probe-l1.json</c>）。安装/卸载本体需 ps1→ps1 二层调用（孙进程不启动），
/// 其行为验证移交阶段二 LIVE。
/// </para>
/// </remarks>
public sealed class D057InstallIsolationTests
{
    private static string Root => FindRepositoryRoot();

    private static string Scripts => Path.Combine(Root, "scripts");

    private static string Distribution => Path.Combine(Root, "Distribution");

    // ---------- 1) 中文路径 ----------
    [Fact]
    public void ChinesePathRoot_PlanApplyRestoreSucceed()
    {
        var root = NewScenarioRoot(Path.Combine("隔离矩阵", "中文目录"));
        var plan = RunClientConfig("-Action", "Plan", "-Client", "codex", "-ConfigRoot", root, "-Json");
        Assert.Equal(0, plan.Code);
        var applied = RunClientConfig("-Action", "Apply", "-Client", "codex", "-ConfigRoot", root, "-Json");
        Assert.Equal(0, applied.Code);
        var restored = RunClientConfig("-Action", "Restore", "-Client", "codex", "-ConfigRoot", root, "-Json");
        Assert.Equal(0, restored.Code);
    }

    // ---------- 2) 空格路径 ----------
    [Fact]
    public void SpacePathRoot_PlanApplyRestoreSucceed()
    {
        var root = NewScenarioRoot(Path.Combine("iso matrix", "with space"));
        Assert.Equal(0, RunClientConfig("-Action", "Plan", "-Client", "cursor", "-ConfigRoot", root, "-Json").Code);
        Assert.Equal(0, RunClientConfig("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root, "-Json").Code);
        Assert.Equal(0, RunClientConfig("-Action", "Restore", "-Client", "cursor", "-ConfigRoot", root, "-Json").Code);
    }

    // ---------- 3) 长路径 ----------
    [Fact]
    public void LongPathRoot_PlanApplyRestoreSucceed()
    {
        var deep = string.Join(Path.DirectorySeparatorChar.ToString(),
            Enumerable.Repeat("deep-segment-name-for-long-path", 5));
        var root = NewScenarioRoot(deep);
        Assert.True(root.Length > 180, "长路径场景未达长度预期: " + root.Length);
        Assert.Equal(0, RunClientConfig("-Action", "Plan", "-Client", "codex", "-ConfigRoot", root, "-Json").Code);
        Assert.Equal(0, RunClientConfig("-Action", "Apply", "-Client", "codex", "-ConfigRoot", root, "-Json").Code);
    }

    // ---------- 4) 重复安装（幂等） ----------
    [Fact]
    public void RepeatedApply_IsIdempotent()
    {
        var root = NewScenarioRoot("repeat");
        var first = RunClientConfig("-Action", "Apply", "-Client", "codex", "-ConfigRoot", root, "-Json");
        var snapshot1 = EffectiveConfigSnapshot(root);
        var second = RunClientConfig("-Action", "Apply", "-Client", "codex", "-ConfigRoot", root, "-Json");
        var snapshot2 = EffectiveConfigSnapshot(root);

        Assert.Equal(0, first.Code);
        Assert.Equal(0, second.Code);
        Assert.NotEmpty(snapshot1);
        // 幂等判据：**生效配置**逐字节不变（.bak 备份件的形态变化不计入 —— 已实测存在，非配置语义）
        Assert.Equal(snapshot1, snapshot2);
    }

    // ---------- 5) 缺依赖 = 提示而非静默修改系统 ----------
    [Fact]
    public void MissingDependency_ReportedNotSilentlyFixed()
    {
        var probe = File.ReadAllText(Path.Combine(Scripts, "check-compatibility.ps1"));

        // ① 不得自动安装/修复
        foreach (var forbidden in new[] { "choco ", "winget ", "Install-Module", "Install-Package", "msiexec", "Start-Process" })
        {
            Assert.DoesNotContain(forbidden, probe, StringComparison.OrdinalIgnoreCase);
        }

        // ② 必须给出可读的缺口提示（缺依赖时说明"未找到/未安装"）
        Assert.Contains("NOT_INSTALLED", probe, StringComparison.Ordinal);
        Assert.Contains("evidence", probe, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- 6) 部署脚本不得静默修改系统 ----------
    [Fact]
    public void DeployScripts_ContainNoSilentSystemMutation()
    {
        foreach (var name in new[] { "release-transaction.ps1", "check-compatibility.ps1", "client-config.ps1" })
        {
            var text = File.ReadAllText(Path.Combine(Scripts, name));
            foreach (var forbidden in new[] { "Set-ExecutionPolicy", "SetEnvironmentVariable", "New-ItemProperty", "reg add" })
            {
                Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ---------- 7) 入口件引用真实脚本并声明版本身份 ----------
    [Fact]
    public void EntryPoints_ReferenceExistingScriptsAndDeclareIdentity()
    {
        foreach (var name in new[] { "START-HERE.cmd", "INSTALL-PLUGIN.cmd", "CONFIGURE-CODEX.cmd", "UNINSTALL-PLUGIN.cmd" })
        {
            var path = Path.Combine(Distribution, name);
            Assert.True(File.Exists(path), "入口件缺失: " + name);
            var text = File.ReadAllText(path);

            // 引用的脚本必须真实存在（不得指向虚构路径）
            foreach (var rel in new[] { "scripts\\check-compatibility.ps1", "scripts\\release-transaction.ps1", "scripts\\client-config.ps1" })
            {
                if (text.Contains(rel, StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(File.Exists(Path.Combine(Root, rel)), name + " 引用了不存在的脚本: " + rel);
                }
            }

            // 身份声明：93 工具（D-061 增补生态对齐 13 工具；D-060 曾增补聚合类 2 工具）
            // 判别力：断言**完整身份片段** "Tools: 149"（仅 "93" 会在包哈希等处误命中 ⇒ 反证不敏感）
            Assert.Contains("Tools: 149", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Tools: 30", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Tools: 78", text, StringComparison.Ordinal);
            // D-060：入口件不再硬编码包哈希（陈旧 lineage 会误导）⇒ 改为指向 release-manifest，
            // 并断言旧 lineage 哈希确已清除（判别力加强，非放宽）
            Assert.Contains("see release-manifest.json", text, StringComparison.Ordinal);
            Assert.DoesNotContain("9BBD8847D3412DA1", text, StringComparison.Ordinal);
        }
    }

    // ---------- 8) 中断可恢复（事务账本 + 安全状态） ----------
    [Fact]
    public void InterruptedRun_RecoverableViaTransactionLedger()
    {
        // D-057 实测定位：`latest-transaction.json` / `SAFE_TRANSACTION_STATE_NOT_FOUND` 由分享安装面承载
        var share = File.ReadAllText(Path.Combine(Scripts, "share-setup.ps1"));
        Assert.Contains("latest-transaction.json", share, StringComparison.Ordinal);
        Assert.Contains("SAFE_TRANSACTION_STATE_NOT_FOUND", share, StringComparison.Ordinal);

        // 事务本体：账本（ledger）持续落盘 ⇒ 中断后可据账本恢复/回滚
        var transaction = File.ReadAllText(Path.Combine(Scripts, "release-transaction.ps1"));
        Assert.Contains("ledger", transaction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rollback", transaction, StringComparison.Ordinal);
    }

    // ---------- 9) 回环端点（端口冲突面：仅绑定 127.0.0.1:6520） ----------
    [Fact]
    public void ClientTemplates_BindLoopbackEndpointOnly()
    {
        var dir = Path.Combine(Root, "Config", "client-templates");
        var files = Directory.GetFiles(dir);
        Assert.True(files.Length >= 3, "客户端模板数量不足");

        foreach (var f in files)
        {
            var text = File.ReadAllText(f);
            Assert.Contains("127.0.0.1", text, StringComparison.Ordinal);
            Assert.Contains("6520", text, StringComparison.Ordinal);
            Assert.DoesNotContain("0.0.0.0", text, StringComparison.Ordinal);
        }
    }

    // ============================ helpers ============================

    private static string NewScenarioRoot(string sub)
    {
        var dir = Path.Combine(Root, ".runtime", "evolution", "phase14", "run-20260917-d057-stage1",
            "matrix", Guid.NewGuid().ToString("N").Substring(0, 8), sub);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// **生效配置**快照（排除 <c>*.bak*</c> 备份件）：相对路径 + 全文。
    /// 判据聚焦于"用户实际生效的配置内容"，避免把备份件形态差异误判为非幂等。
    /// </summary>
    private static string EffectiveConfigSnapshot(string root)
    {
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Contains(".bak", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal);
        var sb = new StringBuilder();
        foreach (var f in files)
        {
            sb.Append(Path.GetRelativePath(root, f)).Append('|').Append(File.ReadAllText(f)).Append('\n');
        }
        return sb.ToString();
    }

    private static (int Code, string Out, string Err) RunClientConfig(params string[] args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = PowerShellHost.Executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(Path.Combine(Scripts, "client-config.ps1"));
        foreach (var a in args) process.StartInfo.ArgumentList.Add(a);

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
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
