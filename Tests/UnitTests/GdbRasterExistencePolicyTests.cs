using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-051（P1 维修单，O-D050-08）GDB 容器输出「存在性判定」与「取消清理」策略测试。
/// 反证锚点 = <see cref="GdbDefinitionMatcher.AnyMatches"/> 的**栅格分支**（回退 → 本族 ≥1 例变红）。
/// 另有源码级防回归：`GeoprocessingService` 必须同时枚举三条定义、且清理走 <see cref="ResidualCleanupPolicy"/>。
/// </summary>
public sealed class GdbRasterExistencePolicyTests
{
    private static string RepoRoot => TestRepo.Root;

    // ---------------- A：三条定义命中判定 ----------------

    [Fact]
    public void RasterOnlyInGdbIsDetected()
    {
        // ★ 本用例即缺陷回归：# 仅在栅格数据集里存在也必须判 Exists=true
        Assert.True(GdbDefinitionMatcher.AnyMatches(
            "S_R1",
            featureClasses: Array.Empty<string>(),
            tables: Array.Empty<string>(),
            rasterDatasets: new[] { "S_R1" }));
    }

    [Fact]
    public void FeatureClassOnlyIsDetected()
        => Assert.True(GdbDefinitionMatcher.AnyMatches("FC_Points", new[] { "FC_Points" }, Array.Empty<string>(), Array.Empty<string>()));

    [Fact]
    public void TableOnlyIsDetected()
        => Assert.True(GdbDefinitionMatcher.AnyMatches("T_Stats", Array.Empty<string>(), new[] { "T_Stats" }, Array.Empty<string>()));

    [Fact]
    public void MixedDefinitionsAreDetected()
    {
        var fcs = new[] { "A", "B" };
        var tables = new[] { "C" };
        var rasters = new[] { "MOS", "S_R1" };
        Assert.True(GdbDefinitionMatcher.AnyMatches("S_R1", fcs, tables, rasters));
        Assert.True(GdbDefinitionMatcher.AnyMatches("MOS", fcs, tables, rasters));
    }

    [Fact]
    public void UnknownNameIsNotDetected()
    {
        Assert.False(GdbDefinitionMatcher.AnyMatches(
            "NOT_THERE",
            new[] { "FC_Points" },
            new[] { "T_Stats" },
            new[] { "S_R1" }));
    }

    [Fact]
    public void MatchIsCaseInsensitive()
    {
        Assert.True(GdbDefinitionMatcher.AnyMatches("s_r1", Array.Empty<string>(), Array.Empty<string>(), new[] { "S_R1" }));
    }

    [Fact]
    public void EmptyOrNullNameIsNotDetected()
    {
        Assert.False(GdbDefinitionMatcher.AnyMatches(null, new[] { "X" }, new[] { "X" }, new[] { "X" }));
        Assert.False(GdbDefinitionMatcher.AnyMatches("   ", new[] { "X" }, new[] { "X" }, new[] { "X" }));
        Assert.False(GdbDefinitionMatcher.AnyMatches("X", null, null, null));
    }

    [Fact]
    public void ReliablePrecheckDetailOnlyForSdkEnumeration()
    {
        Assert.True(GdbDefinitionMatcher.IsReliablePrecheckDetail("sdk-geodatabase"));
        Assert.False(GdbDefinitionMatcher.IsReliablePrecheckDetail("sdk-error: boom"));
        Assert.False(GdbDefinitionMatcher.IsReliablePrecheckDetail("path-is-gdb-root"));   // D-037 防御
        Assert.False(GdbDefinitionMatcher.IsReliablePrecheckDetail("no-gdb-segment"));
        Assert.False(GdbDefinitionMatcher.IsReliablePrecheckDetail(null));
    }

    // ---------------- B：取消清理决策（加固） ----------------

    [Fact]
    public void PreExistingGdbDatasetIsNeverDeleted()
        => Assert.Equal(ResidualCleanupDecision.KeepPreExisting,
            ResidualCleanupPolicy.Decide(isGdbContainer: true, preExists: true, precheckDetail: "sdk-geodatabase", afterExists: false));

    [Fact]
    public void UnknownPreExistenceSkipsCleanup()
        => Assert.Equal(ResidualCleanupDecision.SkipPreExistenceUnknown,
            ResidualCleanupPolicy.Decide(true, null, "sdk-error: timeout", true));

    [Fact]
    public void UnreliablePrecheckDetailSkipsCleanup()
    {
        // ★ D-051 新增加固：GDB 容器下"先验不存在"必须来自可靠 SDK 枚举；其它 detail 一律不删。
        Assert.Equal(ResidualCleanupDecision.SkipPrecheckUnreliable, ResidualCleanupPolicy.Decide(true, false, "sdk-error: boom", true));
        Assert.Equal(ResidualCleanupDecision.SkipPrecheckUnreliable, ResidualCleanupPolicy.Decide(true, false, "path-is-gdb-root", true));
        Assert.Equal(ResidualCleanupDecision.SkipPrecheckUnreliable, ResidualCleanupPolicy.Decide(true, false, "no-gdb-segment", true));
    }

    [Fact]
    public void ReliableNotExistsAllowsCleanup()
        => Assert.Equal(ResidualCleanupDecision.DeleteResidual,
            ResidualCleanupPolicy.Decide(true, false, "sdk-geodatabase", true));

    [Fact]
    public void FileOutputPreExistingKept()
        => Assert.Equal(ResidualCleanupDecision.KeepPreExisting, ResidualCleanupPolicy.Decide(false, true, null, true));

    [Fact]
    public void FileOutputMissingAfterIsNothingToDelete()
        => Assert.Equal(ResidualCleanupDecision.NothingToDelete, ResidualCleanupPolicy.Decide(false, false, null, false));

    [Fact]
    public void FileOutputResidualIsDeleted()
        => Assert.Equal(ResidualCleanupDecision.DeleteResidual, ResidualCleanupPolicy.Decide(false, false, null, true));

    [Fact]
    public void SkipNotesCarryPrecheckDetailAndOthersHaveNoNote()
    {
        Assert.Contains("precheck [sdk-error: boom]",
            ResidualCleanupPolicy.Describe(ResidualCleanupDecision.SkipPrecheckUnreliable, "sdk-error: boom"));
        Assert.Contains("pre-existence unknown",
            ResidualCleanupPolicy.Describe(ResidualCleanupDecision.SkipPreExistenceUnknown, "sdk-error: x"));
        Assert.Null(ResidualCleanupPolicy.Describe(ResidualCleanupDecision.DeleteResidual, "sdk-geodatabase"));
        Assert.Null(ResidualCleanupPolicy.Describe(ResidualCleanupDecision.KeepPreExisting, "sdk-geodatabase"));
        Assert.Null(ResidualCleanupPolicy.Describe(ResidualCleanupDecision.NothingToDelete, null));
    }

    // ---------------- 源码级防回归（静态核对） ----------------

    [Fact]
    public void ServiceEnumeratesAllThreeDefinitionKinds()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot, "Source", "ArcGISProMCP.Compatibility", "Services", "GeoprocessingService.cs"));
        Assert.Contains("GetDefinitions<FeatureClassDefinition>()", src);
        Assert.Contains("GetDefinitions<TableDefinition>()", src);
        Assert.Contains("GetDefinitions<RasterDatasetDefinition>()", src);   // ★ 本单修复点
        Assert.Contains("GdbDefinitionMatcher.AnyMatches", src);
        Assert.Contains("GdbDefinitionMatcher.SdkDetail", src);
    }

    [Fact]
    public void CleanupGoesThroughSharedPolicyAndKeepsD037Defence()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot, "Source", "ArcGISProMCP.Compatibility", "Services", "GeoprocessingService.cs"));
        Assert.Contains("ResidualCleanupPolicy.Decide", src);
        Assert.Contains("ResidualCleanupDecision.DeleteResidual", src);
        Assert.Contains("path-is-gdb-root", src);   // D-037 纵深防御不得回退
    }

    [Fact]
    public void NoNewErrorCodeWasIntroduced()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot, "Source", "Shared", "ArcGISProMCP.Core", "Results", "ErrorCodes.cs"));
        Assert.Equal(33, System.Text.RegularExpressions.Regex.Matches(src, @"public const string").Count);
    }
}

/// <summary>测试用仓库根定位（与既有策略测试同法：向上找到含 Source/Tests 的目录）。</summary>
internal static class TestRepo
{
    private static string? _root;

    public static string Root => _root ??= Find();

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Source")) && Directory.Exists(Path.Combine(dir.FullName, "Tests")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found");
    }
}
