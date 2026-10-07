using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArcGISProMCP.TestSupport;

namespace ArcGISProMCP.UnitTests;

public sealed class CompatibilityPolicyTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static string Checker => Path.Combine(RepoRoot, "scripts", "check-compatibility.ps1");
    private static string Policy => Path.Combine(RepoRoot, "Config", "compatibility-policy.json");
    private static string Manifest => Path.Combine(RepoRoot, "Source", "ArcGISProMCP.Compatibility", "bin", "x64", "Debug", "net6.0-windows", "ArcGISProMCP.Compatibility.release-manifest.json");
    private static string Config => Path.Combine(RepoRoot, "Source", "ArcGISProMCP.Compatibility", "Config.daml");

    [Fact]
    public void CurrentHostReadOnlyProbePasses()
    {
        var result = RunScript(Checker, "-PolicyPath", Policy, "-ManifestPath", Manifest, "-ConfigPath", Config, "-Json");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("PASS", JsonDocument.Parse(result.StdOut).RootElement.GetProperty("overallStatus").GetString());
    }

    [Fact]
    public void OutOfRangeArcGisProSeriesFailsClosed()
    {
        var result = RunFacts(Facts(proVersion: "2.9.0.50000"));
        Assert.Equal(10, result.ExitCode);
        Assert.Equal("UNSUPPORTED", Report(result).GetProperty("overallStatus").GetString());
    }

    [Fact]
    public void MissingArcGisProIsNotInstalled()
    {
        var result = RunFacts(Facts(proInstalled: false));
        Assert.Equal(11, result.ExitCode);
        Assert.Contains("NOT_INSTALLED", result.StdOut);
    }

    [Fact]
    public void WrongArchitectureFailsClosed()
    {
        var result = RunFacts(Facts(architecture: "x86"));
        Assert.Equal(10, result.ExitCode);
        Assert.Equal("UNSUPPORTED", Report(result).GetProperty("overallStatus").GetString());
    }

    [Fact]
    public void WrongArcGisProPeMachineFailsClosed()
    {
        var result = RunFacts(Facts(proArchitecture: "I386"));
        Assert.Equal(10, result.ExitCode);
        Assert.Equal("UNSUPPORTED", Report(result).GetProperty("overallStatus").GetString());
    }

    [Fact]
    public void MissingArcGisProPeMachineIsNotVerified()
    {
        var result = RunFacts(Facts(proArchitecture: null));
        Assert.Equal(12, result.ExitCode);
        Assert.Equal("NOT_VERIFIED", Report(result).GetProperty("overallStatus").GetString());
    }

    [Fact]
    public void OutOfRangeSdkVersionFailsClosed()
    {
        var result = RunFacts(Facts(sdkVersion: "12.9.0.57366"));
        Assert.Equal(10, result.ExitCode);
        Assert.Equal("UNSUPPORTED", Report(result).GetProperty("overallStatus").GetString());
    }

    [Fact]
    public void MissingSdkIsNotInstalled()
    {
        var result = RunFacts(Facts(sdkInstalled: false));
        Assert.Equal(11, result.ExitCode);
        Assert.Contains("arcgis-pro-sdk", result.StdOut);
    }

    [Fact]
    public void MissingDotNetRuntimeIsNotInstalled()
    {
        var result = RunFacts(Facts(dotnetPresent: false));
        Assert.Equal(11, result.ExitCode);
        Assert.Contains("dotnet-runtime", result.StdOut);
    }

    [Fact]
    public void BrokenArcPyIsNotVerified()
    {
        var result = RunFacts(Facts(arcpyError: "import failed"));
        Assert.Equal(12, result.ExitCode);
        Assert.Equal("NOT_VERIFIED", Report(result).GetProperty("overallStatus").GetString());
    }

    [Fact]
    public void PolicyManifestMismatchReturnsError()
    {
        // D-066：篡改锚点改为「值级」匹配（生成器为 json.dumps 单空格形态；旧两空格锚点会空转导致假阴性）。
        var badManifest = WriteTemp(File.ReadAllText(Manifest).Replace("\"releaseVersion\": \"1.0.2\"", "\"releaseVersion\": \"9.9.9\"", StringComparison.Ordinal));
        var facts = WriteTemp(Facts());
        try
        {
            var result = RunScript(Checker, "-PolicyPath", Policy, "-ManifestPath", badManifest, "-ConfigPath", Config, "-FactsPath", facts, "-Json");
            Assert.Equal(1, result.ExitCode);
            Assert.Equal("ERROR", Report(result).GetProperty("overallStatus").GetString());
        }
        finally { File.Delete(badManifest); File.Delete(facts); }
    }

    [Fact]
    public void InstallRefusesIncompatibleFactsBeforeMutation()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-compat-install-");
        var transaction = Directory.CreateTempSubdirectory("arcgispro-mcp-compat-tx-");
        var facts = WriteTemp(Facts(proVersion: "2.9.0.50000"));
        try
        {
            var result = RunScript(
                Path.Combine(RepoRoot, "scripts", "release-transaction.ps1"),
                "-Action", "Install", "-TestMode", "-InstallRoot", root.FullName,
                "-TransactionRoot", transaction.FullName, "-CompatibilityFactsPath", facts);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("COMPATIBILITY_GATE_REFUSED", result.StdOut + result.StdErr);
            Assert.Empty(Directory.EnumerateFileSystemEntries(root.FullName));

            var acceptanceRoot = Directory.CreateTempSubdirectory("arcgispro-mcp-compat-accept-");
            try
            {
                var acceptance = RunScript(
                    Path.Combine(RepoRoot, "scripts", "release-transaction.ps1"),
                    "-Action", "Acceptance", "-TestMode", "-InstallRoot", root.FullName,
                    "-TransactionRoot", acceptanceRoot.FullName, "-CompatibilityFactsPath", facts);
                Assert.NotEqual(0, acceptance.ExitCode);
                Assert.Contains("COMPATIBILITY_GATE_REFUSED", acceptance.StdOut + acceptance.StdErr);
            }
            finally { acceptanceRoot.Delete(true); }
        }
        finally
        {
            root.Delete(true);
            transaction.Delete(true);
            File.Delete(facts);
        }
    }

    // ───────── D-060 · 支持范围三段口径（G-160）检测矩阵单测 ─────────

    [Fact]
    public void VerifiedSeriesHostPassesAndIsMarkedVerified()
    {
        var result = RunFacts(Facts(proVersion: "3.5.0.57366"));
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("PASS", Report(result).GetProperty("overallStatus").GetString());
        var check = VersionCheck(result);
        Assert.Equal("PASS", check.GetProperty("status").GetString());
        Assert.Contains("已实测支持", check.GetProperty("evidence").GetString());
        Assert.DoesNotContain("未验证", check.GetProperty("evidence").GetString());
    }

    [Fact]
    public void NotVerifiedSeriesHostPassesWithWarning_UpperNotVerified_3_4()
    {
        var result = RunFacts(Facts(proVersion: "3.4.0.55405"));
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("PASS", Report(result).GetProperty("overallStatus").GetString());
        var check = VersionCheck(result);
        Assert.Equal("PASS", check.GetProperty("status").GetString());
        Assert.Contains("运行期未验证", check.GetProperty("evidence").GetString());
    }

    [Fact]
    public void NotVerifiedSeriesHostPassesWithWarning_LowerBoundary_3_0()
    {
        var result = RunFacts(Facts(proVersion: "3.0.0.36056"));
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("运行期未验证", VersionCheck(result).GetProperty("evidence").GetString());
    }

    [Fact]
    public void BelowSupportedRangeFailsClosed()
    {
        var result = RunFacts(Facts(proVersion: "2.9.0.50000"));
        Assert.Equal(10, result.ExitCode);
        Assert.Equal("UNSUPPORTED", VersionCheck(result).GetProperty("status").GetString());
    }

    [Fact]
    public void AboveSupportedRangeNotMeasuredIsNotVerified_3_6()
    {
        var result = RunFacts(Facts(proVersion: "3.6.0.59527"));
        Assert.Equal(12, result.ExitCode);
        Assert.Equal("NOT_VERIFIED", VersionCheck(result).GetProperty("status").GetString());
    }

    [Fact]
    public void UnknownHostVersionIsNotVerified()
    {
        var result = RunFacts(Facts(proVersion: ""));
        Assert.Equal(12, result.ExitCode);
        Assert.Equal("NOT_VERIFIED", VersionCheck(result).GetProperty("status").GetString());
    }

    [Fact]
    public void SdkVersionWithinSupportedPrefixesPasses()
    {
        var result = RunFacts(Facts(sdkVersion: "13.2.0.49743"));
        Assert.Equal(0, result.ExitCode);
        var sdkCheck = Check(result, "arcgis-pro-sdk");
        Assert.Equal("PASS", sdkCheck.GetProperty("status").GetString());
    }

    // ───────── D-069 · 双代 TFM 变体判定（net6/net8 双 manifest 过门）─────────

    private static string Net8Manifest => Path.Combine(RepoRoot, "Source", "ArcGISProMCP.Compatibility", "bin", "x64", "Debug", "net8.0-windows", "ArcGISProMCP.Compatibility.release-manifest.json");

    // D-072：net8 变体 Config.daml 夹具（desktopVersion=3.5.0，打包期双代化后的形态）
    private static string WriteNet8Config()
    {
        var text = File.ReadAllText(Config).Replace("desktopVersion=\"3.0\"", "desktopVersion=\"3.5.0\"", StringComparison.Ordinal);
        return WriteTemp(text);
    }

    [Fact]
    public void Net8ManifestPassesDualVariantGate()
    {
        var facts = WriteTemp(Facts());
        var net8Config = WriteNet8Config();
        try
        {
            var result = RunScript(Checker, "-PolicyPath", Policy, "-ManifestPath", Net8Manifest, "-ConfigPath", net8Config, "-FactsPath", facts, "-Json");
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("PASS", Report(result).GetProperty("overallStatus").GetString());
            Assert.Equal("PASS", Check(result, "release-identity-consistency").GetProperty("status").GetString());
        }
        finally { File.Delete(facts); File.Delete(net8Config); }
    }

    [Fact]
    public void Net8ManifestWithNet6ConfigFailsClosed()
    {
        // O-D069-01 负例：net8 包 Config.daml desktopVersion=3.0（≠ 变体 3.5.0）→ 规则 7 逐变体精确匹配 → ERROR
        var facts = WriteTemp(Facts());
        try
        {
            var result = RunScript(Checker, "-PolicyPath", Policy, "-ManifestPath", Net8Manifest, "-ConfigPath", Config, "-FactsPath", facts, "-Json");
            Assert.Equal(1, result.ExitCode);
            Assert.Equal("ERROR", Check(result, "release-identity-consistency").GetProperty("status").GetString());
        }
        finally { File.Delete(facts); }
    }

    [Fact]
    public void Net8ManifestHost35IsVerifiedAfterLive()
    {
        // net8 变体：3.5 已实测（D-068 阶段二＋D-070 LIVE，policy 1.3 事实升格）→ PASS＋已实测支持
        var facts = WriteTemp(Facts(proVersion: "3.5.0.57366"));
        var net8Config = WriteNet8Config();
        try
        {
            var result = RunScript(Checker, "-PolicyPath", Policy, "-ManifestPath", Net8Manifest, "-ConfigPath", net8Config, "-FactsPath", facts, "-Json");
            Assert.Equal(0, result.ExitCode);
            var vc = Check(result, "arcgis-pro-version");
            Assert.Equal("PASS", vc.GetProperty("status").GetString());
            Assert.Contains("已实测支持", vc.GetProperty("evidence").GetString());
        }
        finally { File.Delete(facts); File.Delete(net8Config); }
    }

    [Fact]
    public void Net6ManifestStillPassesDualVariantGate()
    {
        // net6 现役 manifest 经双代判据仍过门（回归保护）
        var facts = WriteTemp(Facts());
        try
        {
            var result = RunScript(Checker, "-PolicyPath", Policy, "-ManifestPath", Manifest, "-ConfigPath", Config, "-FactsPath", facts, "-Json");
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("PASS", Check(result, "release-identity-consistency").GetProperty("status").GetString());
        }
        finally { File.Delete(facts); }
    }

    [Fact]
    public void Net8ManifestWrongDesktopVersionFailsClosed()
    {
        // 变体判据负例：net8 manifest 的 desktopVersion 与 net8 变体 requiredDesktopVersion 不符 → ERROR
        // （manifest 为 PowerShell ConvertTo-Json 双空格格式，用 regex 稳健替换）
        var bad = WriteTemp(Regex.Replace(File.ReadAllText(Net8Manifest), "\"arcgisProDesktopVersion\"\\s*:\\s*\"3\\.5\\.0\"", "\"arcgisProDesktopVersion\": \"3.0\""));
        var facts = WriteTemp(Facts());
        try
        {
            var result = RunScript(Checker, "-PolicyPath", Policy, "-ManifestPath", bad, "-ConfigPath", Config, "-FactsPath", facts, "-Json");
            Assert.Equal(1, result.ExitCode);
            Assert.Equal("ERROR", Check(result, "release-identity-consistency").GetProperty("status").GetString());
        }
        finally { File.Delete(bad); File.Delete(facts); }
    }

    private static JsonElement VersionCheck(RunResult result) => Check(result, "arcgis-pro-version");

    private static JsonElement Check(RunResult result, string name)
    {
        foreach (var item in Report(result).GetProperty("checks").EnumerateArray())
        {
            if (string.Equals(item.GetProperty("name").GetString(), name, StringComparison.Ordinal))
            {
                return item;
            }
        }
        throw new InvalidOperationException("check not found: " + name);
    }

    private static JsonElement Report(RunResult result) => JsonDocument.Parse(result.StdOut).RootElement;

    private static RunResult RunFacts(string facts, params string[] extra)
    {
        var factsPath = WriteTemp(facts);
        try
        {
            var args = new List<string> { "-PolicyPath", Policy, "-ManifestPath", Manifest, "-ConfigPath", Config, "-FactsPath", factsPath, "-Json" };
            args.AddRange(extra);
            return RunScript(Checker, args.ToArray());
        }
        finally { File.Delete(factsPath); }
    }

    private static string Facts(
        string proVersion = "3.5.0.57366",
        bool proInstalled = true,
        string architecture = "x64",
        bool dotnetPresent = true,
        string? arcpyError = null,
        string? proArchitecture = "AMD64",
        string sdkVersion = "13.5.0.57366",
        bool sdkInstalled = true)
    {
        object python;
        if (arcpyError is null)
            python = new { path = "C:\\ArcGIS\\python.exe", version = "3.11.11", arcpyImported = true, arcpyVersion = "3.5", arcpyProduct = "ArcGISPro" };
        else
            python = new { path = "C:\\ArcGIS\\python.exe", error = arcpyError };
        return JsonSerializer.Serialize(new
        {
            os = new { platform = "Windows", architecture },
            arcgisPro = new { installed = proInstalled, path = "C:\\ArcGIS\\Pro\\bin\\ArcGISPro.exe", fileVersion = proVersion, productVersion = proVersion, architecture = proArchitecture },
            sdk = new { installed = sdkInstalled, assemblies = new[] { new { productVersion = sdkVersion }, new { productVersion = sdkVersion } } },
            dotnet = new { runtimePresent = dotnetPresent, runtimeMajor = dotnetPresent ? 8 : 0, evidence = "injected" },
            python
        });
    }

    private static string WriteTemp(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "arcgispro-mcp-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// 调用脚本。**D-054 A2 同因修复**：宿主由硬编码 <c>pwsh.exe</c> 改为
    /// <see cref="PowerShellHost"/> 解析（PS7 优先，Windows 内置 5.1 兜底）。
    /// 原实现在未安装 PS7 的机器上以 Win32Exception 整族失败（环境不可达，非产品缺口）。
    /// </summary>
    private static RunResult RunScript(string script, params string[] args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = PowerShellHost.Executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(script);
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new RunResult(process.ExitCode, stdout, stderr);
    }

    private sealed record RunResult(int ExitCode, string StdOut, string StdErr);
}
