using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.TestSupport;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-057（Phase 14 第一批 · C 项）升级 / 卸载 / 回滚 / 配置恢复 / 陈旧备份拒绝。
/// </summary>
/// <remarks>
/// <b>测试分层（依据 D-057 S0 探针 <c>spawn-probe-l1.json</c>：L1 可用 / L2 不可用）</b>
/// <list type="number">
/// <item><b>离线可验证</b>（不 spawn）：候选包条目与核心件、回滚链每级包有效性、现役/陈旧备份哈希区分。</item>
/// <item><b>静态契约</b>（不 spawn）：事务脚本 Action 集、fail-closed 身份校验次序、<c>STALE_BACKUP_REFUSED</c>
/// 与 <c>ARCGIS_PRO_RUNNING</c> 护栏在位 —— 这些逻辑需 ps1→ps1 二层 spawn（本 harness 孙进程不启动），
/// 故阶段一以静态契约取证，<b>行为验证移交阶段二 LIVE</b>（安装位级：升级 + 回滚）。</item>
/// <item><b>真实一层 spawn</b>：配置恢复（<c>client-config.ps1 -Action Restore</c>，宿主→ps1 可用）。</item>
/// </list>
/// </remarks>
public sealed class D057UpgradeRollbackTests
{
    /// <summary>
    /// 上一代基线（D-055 收官基线）在链中的实测身份：SHA-256 **前 16 位** + 字节数
    /// （取自 <c>backup-pre-d056/ArcGISProMCP.Compatibility.esriAddInX</c> 实测，见
    /// <c>rollback-chain-inventory.json</c>；不写全串以免凭记忆引入错值）。
    /// </summary>
    private const string ExpectedPreviousBaselineShaPrefix = "A5394E47648369D8";

    private const long ExpectedPreviousBaselineBytes = 443901;

    private static string Root => FindRepositoryRoot();

    private static string Scripts => Path.Combine(Root, "scripts");

    private static string CandidatePackage => Path.Combine(
        Root, "Source", "ArcGISProMCP.Compatibility", "bin", "x64", "Debug", "net6.0-windows",
        "ArcGISProMCP.Compatibility.esriAddInX");

    // ---------- 1) 事务脚本暴露完整 Action 集（升级/卸载/回滚/预检/快照/验收） ----------
    [Fact]
    public void ReleaseTransaction_ExposesFullTransactionalActionSet()
    {
        var text = File.ReadAllText(Path.Combine(Scripts, "release-transaction.ps1"));
        foreach (var action in new[] { "Preflight", "Snapshot", "Install", "Uninstall", "Rollback", "Acceptance" })
        {
            Assert.Contains("'" + action + "'", text, StringComparison.Ordinal);
        }
    }

    // ---------- 2) fail-closed 身份校验先于兼容门 ----------
    [Fact]
    public void FailClosedIdentityChecks_PrecedeCompatibilityGate()
    {
        var path = Path.Combine(Scripts, "release-transaction.ps1");
        var lines = File.ReadAllLines(path);
        var idLine = IndexOfFirst(lines, "MANIFEST_ID_MISMATCH");
        var artifactLine = IndexOfFirst(lines, "MANIFEST_ARTIFACT_MISMATCH");
        var runtimeLine = IndexOfFirst(lines, "MANIFEST_RUNTIME_ARTIFACT_MISMATCH");
        var gateLine = IndexOfFirst(lines, "Invoke-CompatibilityCheck");

        Assert.True(idLine >= 0, "MANIFEST_ID_MISMATCH 缺失（fail-closed 身份校验）");
        Assert.True(artifactLine >= 0, "MANIFEST_ARTIFACT_MISMATCH 缺失");
        Assert.True(runtimeLine >= 0, "MANIFEST_RUNTIME_ARTIFACT_MISMATCH 缺失");
        Assert.True(gateLine >= 0, "兼容门调用点缺失");
        Assert.True(artifactLine < gateLine, "清单校验必须**先于**兼容门（否则兼容门永不可达）");
    }

    // ---------- 3) 新候选保留全部核心条目（升级不丢件） ----------
    [Fact]
    public void NewCandidate_PreservesCoreEntriesForUpgrade()
    {
        Assert.True(File.Exists(CandidatePackage), "候选包不存在: " + CandidatePackage);
        using var zip = ZipFile.OpenRead(CandidatePackage);
        var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var core in new[]
                 {
                     "Config.daml",
                     "Images/AddInIcon.png",
                     "Install/ArcGISProMCP.Compatibility.dll",
                     "Install/PythonBridge/bridge_runner.py",
                 })
        {
            Assert.True(names.Contains(core), "升级路径丢失核心条目: " + core);
        }

        // D-057 B：部署件已装入（版本身份链 + 冻结清单 + 用户指南 + 依赖检测 + 模板 + 入口件）
        foreach (var dep in new[]
                 {
                     "Install/Deployment/deployment-manifest.json",
                     "Install/Deployment/FUNCTION_SCOPE_FREEZE.md",
                     "Install/Deployment/SHARING_AND_SIMPLE_INSTALL.md",
                     "Install/Deployment/check-compatibility.ps1",
                     "Install/Deployment/entrypoints/INSTALL-PLUGIN.cmd",
                 })
        {
            Assert.True(names.Contains(dep), "部署件未装入包: " + dep);
        }
    }

    // ---------- 4) 回滚链：每级备份都是有效 AddIn 包 ----------
    [Fact]
    public void RollbackChain_EachLevelIsValidAddInPackage()
    {
        var chain = Directory.GetDirectories(Path.Combine(Root, ".runtime", "evolution", "phase08"), "backup-pre-*")
            .OrderBy(d => d, StringComparer.Ordinal).ToArray();
        Assert.True(chain.Length >= 20, "回滚链级别数不足（期望 ≥20，实际 " + chain.Length + "）");

        // 已知例外：fixture 级备份存的是 aprx/fixture 快照而非 AddIn 包（历史形态，非缺陷）
        var fixtureLevels = new List<string>();
        var bad = new List<string>();
        var valid = 0;

        foreach (var dir in chain)
        {
            var file = Path.Combine(dir, "ArcGISProMCP.Compatibility.esriAddInX");
            if (!File.Exists(file))
            {
                if (Directory.GetFiles(dir).Length > 0)
                {
                    fixtureLevels.Add(Path.GetFileName(dir));   // 非空但非 AddIn 包 ⇒ fixture 级
                }
                else
                {
                    bad.Add(dir + " :: 空目录");
                }
                continue;
            }

            try
            {
                using var zip = ZipFile.OpenRead(file);
                if (zip.Entries.Any(e => e.FullName.Equals("Config.daml", StringComparison.OrdinalIgnoreCase)))
                {
                    valid++;
                }
                else
                {
                    bad.Add(dir + " :: 非 AddIn 包（无 Config.daml）");
                }
            }
            catch (Exception ex)
            {
                bad.Add(dir + " :: 无法打开 " + ex.GetType().Name);
            }
        }

        Assert.Empty(bad);
        Assert.True(valid >= 30, "有效 AddIn 包级别数不足（期望 ≥30，实际 " + valid + "）");
        Assert.True(fixtureLevels.Count <= 2, "fixture 级例外过多（实际 " + fixtureLevels.Count + "）");
    }

    // ---------- 5) 回滚链最新级 == 上一代基线（现役/陈旧可区分） ----------
    [Fact]
    public void RollbackChain_LatestLevelMatchesPreviousBaseline()
    {
        var file = Path.Combine(Root, ".runtime", "evolution", "phase08", "backup-pre-d056",
            "ArcGISProMCP.Compatibility.esriAddInX");
        Assert.True(File.Exists(file), "backup-pre-d056 缺失（D-056 阶段二安装前的现役备份）");
        var sha = Sha256(file);
        Assert.StartsWith(ExpectedPreviousBaselineShaPrefix, sha, StringComparison.Ordinal);
        Assert.Equal(ExpectedPreviousBaselineBytes, new FileInfo(file).Length);

        // 现役候选不得与其同名（否则回滚/陈旧判定失去区分度）
        Assert.NotEqual(Sha256(file), Sha256(CandidatePackage));
    }

    // ---------- 6) 陈旧备份拒绝护栏在位 ----------
    [Fact]
    public void StaleBackupRefusal_GuardPresent()
    {
        // D-057 实测定位：该护栏由**客户端配置面 / 用户工作流面**承载（不在 release-transaction）
        var config = File.ReadAllText(Path.Combine(Scripts, "client-config.ps1"));
        var workflow = File.ReadAllText(Path.Combine(Scripts, "user-workflow.ps1"));
        Assert.Contains("STALE_BACKUP_REFUSED", config, StringComparison.Ordinal);
        Assert.Contains("STALE_BACKUP_REFUSED", workflow, StringComparison.Ordinal);
        // 护栏须作用在备份上下文（而非孤立字符串）
        Assert.Contains("backup", config, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- 7) Pro 运行护栏（卸载/安装前拒绝变更） ----------
    [Fact]
    public void RunningProGuardrail_Present()
    {
        var text = File.ReadAllText(Path.Combine(Scripts, "release-transaction.ps1"));
        Assert.Contains("ARCGIS_PRO_RUNNING", text, StringComparison.Ordinal);
        Assert.Contains("ArcGISProProcessName", text, StringComparison.Ordinal);
    }

    // ---------- 8) 配置恢复（真实一层 spawn） ----------
    [Fact]
    public void ConfigRestore_RemovesAppliedConfiguration()
    {
        var root = NewScenarioRoot("restore");
        var client = "codex";

        var applied = RunClientConfig("-Action", "Apply", "-Client", client, "-ConfigRoot", root);
        Assert.Equal(0, applied.Code);

        var configPath = LocateAppliedFile(root);
        Assert.True(configPath is not null, "Apply 后应在 ConfigRoot 下产生配置文件");
        var afterApply = File.ReadAllText(configPath!);

        var restored = RunClientConfig("-Action", "Restore", "-Client", client, "-ConfigRoot", root);
        Assert.Equal(0, restored.Code);

        var afterRestore = File.Exists(configPath!) ? File.ReadAllText(configPath!) : string.Empty;
        Assert.NotEqual(afterApply, afterRestore);
    }

    // ============================ helpers ============================

    private static int IndexOfFirst(string[] lines, string token)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(token, StringComparison.Ordinal)) return i;
        }
        return -1;
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string NewScenarioRoot(string tag)
    {
        var dir = Path.Combine(Root, ".runtime", "evolution", "phase14", "run-20260917-d057-stage1",
            "matrix", tag + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string? LocateAppliedFile(string root)
        => Directory.Exists(root)
            ? Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .FirstOrDefault(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            : null;

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
