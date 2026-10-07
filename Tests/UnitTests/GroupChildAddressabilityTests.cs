using ArcGISProMCP.Core.Services;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-024 / F4：组子层按名可寻址性统一。
/// 契约（Keeper 已定）：凡 <c>get_layers(flatten=true)</c> 可见的图层（含组内子层与嵌套下钻），
/// 均应按名可寻址；同名命中顶层与子层（或多个子层）→ <c>AMBIGUOUS_LAYER_NAME</c>+候选，不得静默取第一。
/// </summary>
/// <remarks>
/// 本类覆盖两层：
/// ① **纯逻辑**（<see cref="LayerNameMatcher"/>，Rule 5）：以"展平候选集（顶层+组子层+嵌套）"
///    模拟 <c>LayerResolver.Resolve(flatten:true)</c> 的输入，验证唯一命中 / 歧义 / 未找到三分支；
/// ② **结构性断言**（D-020 D1 纪律：先断言样本条数）：5 个站点的按名解析全部经
///    <c>LayerResolver</c> 且**统一 flatten:true**，无残留 <c>flatten:false</c> 解析调用。
/// SDK 侧 <c>Map.GetLayersAsFlattenedList()</c> 的实际枚举属运行态行为，装后运行态复验归
/// <c>BLOCKED_BY_INSTALL</c>（本单不安装）。
/// </remarks>
public sealed class GroupChildAddressabilityTests
{
    private const string CompatibilityServices = "Source/ArcGISProMCP.Compatibility/Services";

    /// <summary>模拟 flatten=true 展平候选集：顶层 + 组内子层 + 嵌套组子层（fixture MD_Active 同构）。</summary>
    private static List<KeyValuePair<string, string>> FlattenedFixture()
        => new()
        {
            new("L_Lines", "CIMPATH=MD_Active/FC_Lines2.json"),   // 顶层（与组子层同名场景的另一实例不同名）
            new("L_Hidden", "CIMPATH=MD_Active/FC_Polygons2.json"),
            new("L_Group", "CIMPATH=MD_Active/L_Group.json"),     // 组图层本身
            new("L_Sub_A", "CIMPATH=MD_Active/L_Sub_A.json"),     // 组子层
            new("L_Sub_B", "CIMPATH=MD_Active/L_Sub_B.json"),     // 组子层
            new("L_Points", "CIMPATH=MD_Active/FC_Points.json"),
            new("L_Lines", "CIMPATH=MD_Active/FC_Lines.json"),    // 另一顶层同名（既有重名场景）
            new("L_Polygons", "CIMPATH=MD_Active/FC_Polygons.json"),
            new("L_Nested_Deep", "CIMPATH=MD_Active/L_Nested.json"), // 嵌套组内子层（下钻可达）
        };

    // -------------------------------------------------------------- 纯逻辑：组子层唯一命中

    [Fact]
    public void Match_GroupChild_UniqueHit_ReturnsOk()
    {
        var (status, matched, candidates) = LayerNameMatcher.MatchByName("L_Sub_A", FlattenedFixture());
        Assert.Equal(LayerNameMatchStatus.Ok, status);
        Assert.Equal("CIMPATH=MD_Active/L_Sub_A.json", matched);
        Assert.Empty(candidates);
    }

    [Fact]
    public void Match_NestedGroupChild_UniqueHit_ReturnsOk()
    {
        // 嵌套下钻：子层的子层也在展平集合中 → 按名可达。
        var (status, matched, _) = LayerNameMatcher.MatchByName("L_Nested_Deep", FlattenedFixture());
        Assert.Equal(LayerNameMatchStatus.Ok, status);
        Assert.Equal("CIMPATH=MD_Active/L_Nested.json", matched);
    }

    [Fact]
    public void Match_GroupLayerItself_StillAddressable()
    {
        var (status, matched, _) = LayerNameMatcher.MatchByName("L_Group", FlattenedFixture());
        Assert.Equal(LayerNameMatchStatus.Ok, status);
        Assert.Equal("CIMPATH=MD_Active/L_Group.json", matched);
    }

    // -------------------------------------------------------------- 纯逻辑：同名歧义（顶层 vs 子层 / 多子层）

    [Fact]
    public void Match_TopLevelAndChild_SameName_ReturnsAmbiguous_WithBothCandidates()
    {
        var pairs = new List<KeyValuePair<string, string>>
        {
            new("L_River", "CIMPATH=MD_Active/L_River.json"),        // 顶层
            new("L_Group", "CIMPATH=MD_Active/L_Group.json"),
            new("L_River", "CIMPATH=MD_Active/L_River_Child.json"),  // 组内子层同名
        };
        var (status, matched, candidates) = LayerNameMatcher.MatchByName("L_River", pairs);
        Assert.Equal(LayerNameMatchStatus.Ambiguous, status);
        Assert.Null(matched);
        Assert.Equal(2, candidates.Count);
        Assert.Contains("CIMPATH=MD_Active/L_River.json", candidates);
        Assert.Contains("CIMPATH=MD_Active/L_River_Child.json", candidates);
    }

    [Fact]
    public void Match_TwoGroupChildren_SameName_ReturnsAmbiguous()
    {
        var pairs = new List<KeyValuePair<string, string>>
        {
            new("L_Group", "CIMPATH=MD_Active/L_Group.json"),
            new("L_Dup", "CIMPATH=MD_Active/L_GroupA/L_Dup1.json"),
            new("L_Dup", "CIMPATH=MD_Active/L_GroupB/L_Dup2.json"),
        };
        var (status, _, candidates) = LayerNameMatcher.MatchByName("L_Dup", pairs);
        Assert.Equal(LayerNameMatchStatus.Ambiguous, status);
        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public void Match_CaseInsensitive_Preserved_ForGroupChild()
    {
        var (status, matched, _) = LayerNameMatcher.MatchByName("l_sub_a", FlattenedFixture());
        Assert.Equal(LayerNameMatchStatus.Ok, status);
        Assert.Equal("CIMPATH=MD_Active/L_Sub_A.json", matched);
    }

    [Fact]
    public void Match_UnknownName_InFlattenedSet_ReturnsNotFound()
    {
        var (status, matched, candidates) = LayerNameMatcher.MatchByName("NoSuchLayer", FlattenedFixture());
        Assert.Equal(LayerNameMatchStatus.NotFound, status);
        Assert.Null(matched);
        Assert.Empty(candidates);
    }

    // -------------------------------------------------------------- 结构性断言（先断言样本条数）

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ArcGIS-Pro-MCP.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Repository root could not be located from test base directory: {AppContext.BaseDirectory}");
    }

    private static string ReadSource(string relativePath)
    {
        var full = Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(full), $"Source file is missing: {relativePath}");
        return File.ReadAllText(full);
    }

    private static int CountOccurrences(string content, string needle)
        => content.Split(needle, StringSplitOptions.None).Length - 1;

    [Fact]
    public void Structure_AllFiveSites_ResolveViaLayerResolver_WithFlattenTrue()
    {
        // 样本集自证（D-020 D1）：先断言站点条数，再逐站点断言。
        var sites = new[]
        {
            (File: "AttributeService.cs", MinResolveCalls: 1),      // query_attributes / get_field_info / get_feature_count
            (File: "LayerService.cs", MinResolveCalls: 3),          // get_layer_info / set_layer_visibility / remove_layer
            (File: "SelectionService.cs", MinResolveCalls: 1),      // select_layer
            (File: "GeoprocessingService.cs", MinResolveCalls: 1),  // select_by_attribute / select_by_location（第 5 站点）
        };
        Assert.Equal(4, sites.Length);

        var total = 0;
        foreach (var (file, minResolveCalls) in sites)
        {
            var src = ReadSource($"{CompatibilityServices}/{file}");
            var viaResolver = CountOccurrences(src, "LayerResolver.Resolve(map, layerName, flatten: true)")
                              + CountOccurrences(src, "LayerResolver.ResolveExact(map, layerName, out error, flatten: true)");
            Assert.True(viaResolver >= minResolveCalls,
                $"{file}: expected >= {minResolveCalls} flatten:true resolver calls, found {viaResolver}.");
            total += viaResolver;
        }

        Assert.True(total >= 6, $"Expected >= 6 flatten:true resolve call sites across 4 service files, found {total}.");
    }

    [Fact]
    public void Structure_NoResidual_FlattenFalse_ResolveCalls()
    {
        // D-024 收敛后不得残留 flatten:false 的按名解析调用（排除 LayerResolver.cs 自身定义与注释）。
        var files = new[]
        {
            "AttributeService.cs",
            "LayerService.cs",
            "SelectionService.cs",
            "GeoprocessingService.cs",
        };
        Assert.Equal(4, files.Length);

        foreach (var file in files)
        {
            var src = ReadSource($"{CompatibilityServices}/{file}");
            Assert.DoesNotContain("flatten: false", src);
        }
    }
}
