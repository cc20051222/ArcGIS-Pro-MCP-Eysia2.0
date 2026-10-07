using ArcGISProMCP.Core.Services;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-009：MapNameMatcher 纯名称匹配逻辑的单测（Shared/Core，无 SDK 依赖）。
/// 覆盖派工单 §3 要求：重名、不存在、空串、大小写不敏感。
/// Resolve 的 SDK 路径由 D-008/D-009 复验实机覆盖。
/// </summary>
public sealed class MapNameMatcherTests
{
    private static readonly KeyValuePair<string, string>[] FixtureItems =
    {
        new("MD_Active", "CIMPATH=MD_Active/MD_Active.json"),
        new("MD_Inactive", "CIMPATH=MD_Inactive/MD_Inactive.json"),
        new("MD_Scene", "CIMPATH=MD_Scene/MD_Scene.json"),
        new("MD_Empty", "CIMPATH=MD_Empty/MD_Empty.json"),
        new("MD_Active", "CIMPATH=MD_Active/MD_Active2.json")
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_Name_Returns_Ok_With_Null_Match(string? blank)
    {
        // 空白语义由 SDK 侧 Resolve 处理为"活动地图"（G-22）；纯匹配层表现为 Ok + null。
        var (status, id, candidates) = MapNameMatcher.MatchByName(blank, FixtureItems);
        Assert.Equal(MapNameMatchStatus.Ok, status);
        Assert.Null(id);
        Assert.Empty(candidates);
    }

    [Fact]
    public void Duplicate_Name_Returns_Ambiguous_With_Distinct_Candidates()
    {
        var (status, id, candidates) = MapNameMatcher.MatchByName("MD_Active", FixtureItems);
        Assert.Equal(MapNameMatchStatus.Ambiguous, status);
        Assert.Null(id);
        Assert.Equal(2, candidates.Count);
        Assert.Contains("CIMPATH=MD_Active/MD_Active.json", candidates);
        Assert.Contains("CIMPATH=MD_Active/MD_Active2.json", candidates);
    }

    [Fact]
    public void Missing_Name_Returns_NotFound()
    {
        var (status, id, candidates) = MapNameMatcher.MatchByName("MD_NotExist", FixtureItems);
        Assert.Equal(MapNameMatchStatus.NotFound, status);
        Assert.Null(id);
        Assert.Empty(candidates);
    }

    [Fact]
    public void Name_Match_Is_Case_Insensitive()
    {
        var (status, id, _) = MapNameMatcher.MatchByName("md_inactive", FixtureItems);
        Assert.Equal(MapNameMatchStatus.Ok, status);
        Assert.Equal("CIMPATH=MD_Inactive/MD_Inactive.json", id);
    }

    [Fact]
    public void Duplicate_Match_Is_Case_Insensitive_Too()
    {
        var (status, _, candidates) = MapNameMatcher.MatchByName("md_ACTIVE", FixtureItems);
        Assert.Equal(MapNameMatchStatus.Ambiguous, status);
        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public void Empty_Item_Collection_Returns_NotFound_For_NonBlank_Name()
    {
        var (status, id, candidates) = MapNameMatcher.MatchByName("Any", Array.Empty<KeyValuePair<string, string>>());
        Assert.Equal(MapNameMatchStatus.NotFound, status);
        Assert.Null(id);
        Assert.Empty(candidates);
    }
}
