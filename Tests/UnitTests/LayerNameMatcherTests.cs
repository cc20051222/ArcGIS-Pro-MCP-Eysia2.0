using ArcGISProMCP.Core.Services;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-022 / F1：LayerNameMatcher 纯图层名匹配逻辑单测（Shared/Core，无 SDK 依赖）。
/// 覆盖三分支（0 / 1 / &gt;1）+ 大小写不敏感 + 空白语义 + 候选 id 去重但计数不去重。
/// SDK 侧的枚举与错误码映射由 <see cref="LayerAmbiguityConvergenceTests"/> 以源码结构断言覆盖。
/// </summary>
public sealed class LayerNameMatcherTests
{
    /// <summary>fixture 映射：MD_Active 内有两个同名 <c>L_Lines</c>（与真实 fixture 的重名场景同构）。</summary>
    private static readonly KeyValuePair<string, string>[] FixtureLayers =
    {
        new("L_Points", "CIMPATH=map/l_points.json"),
        new("L_Lines", "CIMPATH=map/l_lines_a.json"),
        new("L_Polygons", "CIMPATH=map/l_polygons.json"),
        new("L_Lines", "CIMPATH=map/l_lines_b.json")
    };

    private const int ExpectedLayerCount = 4;

    [Fact]
    public void Fixture_Shape_Is_Explicitly_Asserted()
    {
        // 纪律（D-020 D1）：禁止以"过滤后为空"判定全通过——先断言样本集本身。
        Assert.Equal(ExpectedLayerCount, FixtureLayers.Length);
        Assert.Equal(2, FixtureLayers.Count(l => l.Key == "L_Lines"));
    }

    [Fact]
    public void Duplicate_Layer_Name_Returns_Ambiguous_With_Candidates()
    {
        var (status, id, candidates) = LayerNameMatcher.MatchByName("L_Lines", FixtureLayers);

        Assert.Equal(LayerNameMatchStatus.Ambiguous, status);
        Assert.Null(id);
        Assert.Equal(2, candidates.Count);
        Assert.Contains("CIMPATH=map/l_lines_a.json", candidates);
        Assert.Contains("CIMPATH=map/l_lines_b.json", candidates);
    }

    [Fact]
    public void Unique_Layer_Name_Returns_Ok_With_MatchedId()
    {
        var (status, id, candidates) = LayerNameMatcher.MatchByName("L_Points", FixtureLayers);

        Assert.Equal(LayerNameMatchStatus.Ok, status);
        Assert.Equal("CIMPATH=map/l_points.json", id);
        Assert.Empty(candidates);
    }

    [Fact]
    public void Missing_Layer_Name_Returns_NotFound()
    {
        var (status, id, candidates) = LayerNameMatcher.MatchByName("L_NotExist", FixtureLayers);

        Assert.Equal(LayerNameMatchStatus.NotFound, status);
        Assert.Null(id);
        Assert.Empty(candidates);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_Layer_Name_Returns_NotFound(string? blank)
    {
        // 图层无"活动图层"语义；空白 layerName 由工具层以 INVALID_ARGUMENT 先行拒绝。
        var (status, id, _) = LayerNameMatcher.MatchByName(blank, FixtureLayers);

        Assert.Equal(LayerNameMatchStatus.NotFound, status);
        Assert.Null(id);
    }

    [Fact]
    public void Layer_Name_Match_Is_Case_Insensitive_For_Unique_And_Ambiguous()
    {
        var unique = LayerNameMatcher.MatchByName("l_polygons", FixtureLayers);
        Assert.Equal(LayerNameMatchStatus.Ok, unique.Status);
        Assert.Equal("CIMPATH=map/l_polygons.json", unique.MatchedId);

        var ambiguous = LayerNameMatcher.MatchByName("l_LINES", FixtureLayers);
        Assert.Equal(LayerNameMatchStatus.Ambiguous, ambiguous.Status);
        Assert.Equal(2, ambiguous.Candidates.Count);
    }

    [Fact]
    public void Empty_Layer_Collection_Returns_NotFound()
    {
        var (status, id, candidates) = LayerNameMatcher.MatchByName(
            "L_Lines",
            Array.Empty<KeyValuePair<string, string>>());

        Assert.Equal(LayerNameMatchStatus.NotFound, status);
        Assert.Null(id);
        Assert.Empty(candidates);
    }

    [Fact]
    public void Duplicate_Count_Is_Not_Collapsed_By_Identical_Ids()
    {
        // 计数按"匹配到的图层数"而非"去重后的 id 数"：id 重复不得把重名降为唯一。
        var layers = new[]
        {
            new KeyValuePair<string, string>("L_Lines", "#1"),
            new KeyValuePair<string, string>("L_Lines", "#1")
        };

        var (status, _, candidates) = LayerNameMatcher.MatchByName("L_Lines", layers);

        Assert.Equal(LayerNameMatchStatus.Ambiguous, status);
        Assert.Single(candidates);
    }
}
