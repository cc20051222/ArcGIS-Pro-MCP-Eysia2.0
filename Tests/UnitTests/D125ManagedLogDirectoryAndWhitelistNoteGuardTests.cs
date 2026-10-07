using System.Text.RegularExpressions;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Runtime;
using ArcGISProMCP.Tools;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-125（G-301 R-4 派）挂账清偿守卫：①受管日志目录开关（O-D100-01＋O-D120-01）——
/// 未配置时派生路径逐字节不变、配置时只替换日志叶节点、非法值 fail-closed；
/// ②<see cref="GpControlNotes"/> 权威源文案（O-D116-01）——白名单件数不再硬编码。
/// 本件只新增断言，未放宽任何既有断言；零真机、零注册变更、零 payload 字节变更。
/// </summary>
public sealed class D125ManagedLogDirectoryAndWhitelistNoteGuardTests
{
    private const string LogDirectorySwitchToken = "ARCGIS_PRO_MCP_LOG_DIRECTORY";

    private static string RepoRoot => TestRepo.Root;

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)),
                            System.Text.Encoding.UTF8);

    // ───────── 完成门②：缺省行为与现值逐字节等值 ─────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ManagedLogDirectory_WhenSwitchUnset_KeepsDerivedDefaultByteForByte(string? overrideValue)
    {
        using var fixture = TestFixture.Create();
        var baseline = RuntimePathResolver.Resolve(fixture.Inputs);
        Assert.True(baseline.Succeeded, baseline.ErrorCode);

        var withOverrideFieldAbsent = RuntimePathResolver.Resolve(fixture.Inputs with { ManagedLogDirectoryOverride = overrideValue });

        Assert.True(withOverrideFieldAbsent.Succeeded, withOverrideFieldAbsent.ErrorCode);
        Assert.NotNull(withOverrideFieldAbsent.Paths);
        Assert.Equal(baseline.Paths!.ManagedLogDirectory, withOverrideFieldAbsent.Paths!.ManagedLogDirectory);
        Assert.Equal(baseline.Paths.RuntimeRoot, withOverrideFieldAbsent.Paths.RuntimeRoot);
        Assert.Equal(Path.Combine(baseline.Paths.RuntimeRoot, "managed-logs"),
                     withOverrideFieldAbsent.Paths.ManagedLogDirectory);
        Assert.False(Directory.Exists(withOverrideFieldAbsent.Paths.ManagedLogDirectory));
    }

    // ───────── 完成门③：开关置位时日志目录随之改变（临时根・零真机） ─────────

    [Fact]
    public void ManagedLogDirectory_WhenSwitchSet_ReroutesOnlyTheLogLeafAndKeepsRuntimeRoot()
    {
        using var fixture = TestFixture.Create();
        var explicitLogDirectory = Path.Combine(fixture.Root, "d-drive-logs", "managed");

        var resolution = RuntimePathResolver.Resolve(
            fixture.Inputs with { ManagedLogDirectoryOverride = explicitLogDirectory });

        Assert.True(resolution.Succeeded, resolution.ErrorCode);
        var paths = resolution.Paths!;
        Assert.Equal(Path.GetFullPath(explicitLogDirectory), paths.ManagedLogDirectory);
        Assert.Equal(Path.Combine(fixture.LocalApplicationData, "ArcGISProMCP", "1.0.2", "runtime"),
                     paths.RuntimeRoot);
        Assert.NotEqual(Path.Combine(paths.RuntimeRoot, "managed-logs"), paths.ManagedLogDirectory);
        // 无副作用：解析器不创建目录
        Assert.False(Directory.Exists(paths.ManagedLogDirectory));
        Assert.False(Directory.Exists(paths.RuntimeRoot));
    }

    [Fact]
    public void ManagedLogDirectory_WhenSwitchSetWithTrailingSeparator_NormalisesToSameDirectory()
    {
        using var fixture = TestFixture.Create();
        var withSeparator = Path.Combine(fixture.Root, "logs-nested") + Path.DirectorySeparatorChar;

        var resolution = RuntimePathResolver.Resolve(
            fixture.Inputs with { ManagedLogDirectoryOverride = withSeparator });
        var plain = RuntimePathResolver.Resolve(
            fixture.Inputs with { ManagedLogDirectoryOverride = Path.Combine(fixture.Root, "logs-nested") });

        Assert.True(resolution.Succeeded, resolution.ErrorCode);
        Assert.True(plain.Succeeded, plain.ErrorCode);
        Assert.Equal(plain.Paths!.ManagedLogDirectory, resolution.Paths!.ManagedLogDirectory);
    }

    // ───────── 完成门①／⑤：非法开关值 fail-closed・固定 safe code ─────────

    [Theory]
    [InlineData("logs/relative-only")]
    [InlineData(@"\\unc-host\share\logs")]
    [InlineData("C:\\logs\\..\\sibling")]
    [InlineData("bad|pipe|name")]
    [InlineData("://not-a-path")]
    public void ManagedLogDirectory_WhenSwitchInvalid_FailsClosedWithFixedSafeCode(string invalid)
    {
        using var fixture = TestFixture.Create();

        var resolution = RuntimePathResolver.Resolve(fixture.Inputs with { ManagedLogDirectoryOverride = invalid });

        Assert.False(resolution.Succeeded);
        Assert.Equal(RuntimePathErrorCodes.ManagedLogDirectoryInvalid, resolution.ErrorCode);
        Assert.Null(resolution.Paths);
        Assert.True(RuntimePathErrorCodes.IsSafe(resolution.ErrorCode));
    }

    [Fact]
    public void ManagedLogDirectory_WhenSwitchIsAFileSystemRoot_FailsClosed()
    {
        using var fixture = TestFixture.Create();
        var root = Path.GetPathRoot(fixture.LocalApplicationData)!;
        Assert.False(string.IsNullOrEmpty(root));

        var resolution = RuntimePathResolver.Resolve(fixture.Inputs with { ManagedLogDirectoryOverride = root });

        Assert.False(resolution.Succeeded);
        Assert.Equal(RuntimePathErrorCodes.ManagedLogDirectoryInvalid, resolution.ErrorCode);
        Assert.True(RuntimePathErrorCodes.IsSafe(resolution.ErrorCode));
    }

    [Fact]
    public void ManagedLogDirectoryInvalidCode_IsFixedSafeTokenWithoutMachinePath()
    {
        Assert.Equal("RUNTIME_MANAGED_LOG_DIRECTORY_INVALID", RuntimePathErrorCodes.ManagedLogDirectoryInvalid);
        Assert.True(RuntimePathErrorCodes.IsSafe(RuntimePathErrorCodes.ManagedLogDirectoryInvalid));
        Assert.DoesNotContain(Path.DirectorySeparatorChar, RuntimePathErrorCodes.ManagedLogDirectoryInvalid);
        Assert.DoesNotContain('/', RuntimePathErrorCodes.ManagedLogDirectoryInvalid);
    }

    // ───────── 配置面：开关在册且缺省不携带机器路径（沿用既有机制・不新造格式） ─────────

    [Fact]
    public void McpSettings_ExposesLogDirectorySwitch_WithMachineAgnosticDefault()
    {
        Assert.Equal(LogDirectorySwitchToken, MCPSettings.LogDirectoryEnvironmentVariable);
        var defaults = new MCPSettings();
        Assert.Equal(string.Empty, defaults.ManagedLogDirectoryOverride);
        Assert.Equal(string.Empty, defaults.ManagedLogDirectory);
        Assert.True(string.IsNullOrWhiteSpace(defaults.ManagedLogDirectoryOverride));
    }

    [Fact]
    public void EnvironmentVariableIsReadByCompatibilityRoot_NotByTheResolver()
    {
        var resolverSource = ReadRepoFile("Source/Shared/ArcGISProMCP.Core/Runtime/RuntimePathResolver.cs");
        Assert.Empty(Regex.Matches(resolverSource, "GetEnvironmentVariable"));

        var compositionSource = ReadRepoFile("Source/ArcGISProMCP.Compatibility/Composition.cs");
        Assert.Single(Regex.Matches(compositionSource,
            Regex.Escape("Environment.GetEnvironmentVariable(MCPSettings.LogDirectoryEnvironmentVariable)")));
        Assert.Single(Regex.Matches(compositionSource,
            Regex.Escape("ManagedLogDirectoryOverride = ManagedLogDirectoryOverrideValue,")));
        Assert.Single(Regex.Matches(compositionSource,
            Regex.Escape("ManagedLogDirectoryOverride = ManagedLogDirectoryOverrideValue ?? string.Empty")));
        // 唯一生产 sink 的注入面不变（缺省行为零变更的源码侧佐证）
        Assert.Single(Regex.Matches(compositionSource,
            Regex.Escape("new ManagedFileLogger(paths.ManagedLogDirectory)")));
    }

    [Fact]
    public void OverrideIsCarriedByThePureInputsRecord_WithoutNewConfigurationFormat()
    {
        var inputsSource = ReadRepoFile("Source/Shared/ArcGISProMCP.Core/Runtime/RuntimePathResolver.cs");
        Assert.Single(Regex.Matches(inputsSource,
            Regex.Escape("public string? ManagedLogDirectoryOverride { get; init; }")));
        Assert.Contains("RUNTIME_MANAGED_LOG_DIRECTORY_INVALID", inputsSource, StringComparison.Ordinal);
        // 不新造 JSON 配置面：Source 内不引入 settings.json / mcpsettings.json
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(RepoRoot, "Source"), "settings.json",
            SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(RepoRoot, "Source"), "mcpsettings.json",
            SearchOption.AllDirectories));
    }

    // ───────── 完成门④：GpControlNotes 零硬编码白名单件数・指向权威源 ─────────

    private static string NotesSummaryBlock()
    {
        var source = ReadRepoFile("Source/Shared/ArcGISProMCP.Tools/GpControlTools.cs")
            .Replace("\r\n", "\n");
        var start = source.IndexOf("/// <summary>", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = source.IndexOf("public static class GpControlNotes", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return source[start..end];
    }

    [Fact]
    public void GpControlNotesComment_ReferencesAuthoritativeWhitelistSource_AndCarriesNoWhitelistCount()
    {
        var block = NotesSummaryBlock();
        Assert.Contains("Config/gp-whitelist.json", block, StringComparison.Ordinal);

        var counted = Regex.Matches(block, @"(\d+)\s*件").Select(m => int.Parse(m.Groups[1].Value)).ToList();
        // 唯一允许的数量字面量＝本段工具数 6（D-062 A 段），白名单件数一律不得出现
        Assert.Equal(new[] { 6 }, counted);
        Assert.DoesNotContain("51", block, StringComparison.Ordinal);
        Assert.DoesNotContain("53", block, StringComparison.Ordinal);
        Assert.DoesNotContain("起步", block, StringComparison.Ordinal);
    }

    [Fact]
    public void WhitelistNoteConstant_StillDeclaresAuthoritativeSource_AndIsTheSecurityNoteSurface()
    {
        Assert.StartsWith("Whitelist source of truth: Config/gp-whitelist.json",
                          GpControlNotes.WhitelistNote, StringComparison.Ordinal);
        var source = ReadRepoFile("Source/Shared/ArcGISProMCP.Tools/GpControlTools.cs").Replace("\r\n", "\n");
        Assert.Single(Regex.Matches(source, Regex.Escape("[\"securityNote\"] = GpControlNotes.WhitelistNote")));
        Assert.DoesNotContain("件", GpControlNotes.WhitelistNote, StringComparison.Ordinal);
    }

    [Fact]
    public void WhitelistEntryCountComesFromTheAuthoritativeFile_NotFromProse()
    {
        var json = ReadRepoFile("Config/gp-whitelist.json");
        var entries = System.Text.Json.JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();
        Assert.True(entries.Count > 0);
        // 文案不再硬编码 ⇒ 任何件数事实只能来自权威源本身
        Assert.DoesNotMatch(@"\d+\s*件", GpControlNotes.WhitelistNote);
        var block = NotesSummaryBlock();
        Assert.True(Regex.Matches(block, @"(\d{2,3})\s*件").Count == 0);
    }

    private sealed class TestFixture : IDisposable
    {
        private TestFixture(string root)
        {
            Root = root;
            AssemblyDirectory = Path.Combine(root, "owned-package", "Install");
            BridgeScript = Path.Combine(AssemblyDirectory, "PythonBridge", "bridge_runner.py");
            ProBin = Path.Combine(root, "ArcGISPro", "bin");
            PythonExecutable = Path.Combine(ProBin, "Python", "envs", "arcgispro-py3", "python.exe");
            LocalApplicationData = Path.Combine(root, "LocalApplicationData");

            Directory.CreateDirectory(Path.GetDirectoryName(BridgeScript)!);
            Directory.CreateDirectory(Path.GetDirectoryName(PythonExecutable)!);
            Directory.CreateDirectory(ProBin);
            Directory.CreateDirectory(LocalApplicationData);
            File.WriteAllText(BridgeScript, "owned bridge");
            File.WriteAllText(Path.Combine(AssemblyDirectory, "ArcGISProMCP.Compatibility.dll"), "assembly");
            File.WriteAllText(Path.Combine(ProBin, "ArcGISPro.exe"), "ArcGIS Pro");
            File.WriteAllText(PythonExecutable, "ArcGIS Python");

            Inputs = new RuntimePathInputs
            {
                CompatibilityAssemblyDirectory = AssemblyDirectory,
                ActiveArcGISProExecutablePath = Path.Combine(ProBin, "ArcGISPro.exe"),
                LocalApplicationDataDirectory = LocalApplicationData,
                ReleaseVersion = "1.0.2"
            };
        }

        public string Root { get; }

        public string AssemblyDirectory { get; }

        public string BridgeScript { get; }

        public string ProBin { get; }

        public string PythonExecutable { get; }

        public string LocalApplicationData { get; }

        public RuntimePathInputs Inputs { get; }

        public static TestFixture Create()
            => new(Directory.CreateTempSubdirectory("arcgispro-mcp-d125-logpaths-").FullName);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // Test-owned temporary data only.
            }
        }
    }
}
