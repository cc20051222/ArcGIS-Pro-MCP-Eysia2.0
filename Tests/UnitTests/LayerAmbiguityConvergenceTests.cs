using System.Text.RegularExpressions;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-022 / F1 全站点收敛的结构性断言（Keeper K2/K2b）。
/// 目的：证明"静默取第一个匹配"这一**模式化缺陷**在 4 个站点（≥6 工具）已全部消失，
/// 且错误码映射正确（图层歧义 ≠ 地图歧义），而不是只修了被复现的那一处。
/// </summary>
/// <remarks>
/// 纪律（D-020 D1）：所有取证/汇总必须**显式断言用例数**，禁止以"过滤后为空"判定全通过。
/// 本类对每个扫描集合都先断言样本条数，再做断言。
/// </remarks>
public sealed class LayerAmbiguityConvergenceTests
{
    private const string CompatibilityServices = "Source/ArcGISProMCP.Compatibility/Services";

    /// <summary>Keeper 圈定的 4 个站点（≥6 工具）+ 第 5 站点（GP，本单一并收敛）。</summary>
    private static readonly string[] ExpectedSites =
    {
        "AttributeService.cs",   // query_attributes / get_field_info / get_feature_count
        "LayerService.cs",       // set_layer_visibility / remove_layer
        "SelectionService.cs",   // select_layer
        "GeoprocessingService.cs"// select_by_attribute / select_by_location（第 5 站点）
    };

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

    // ------------------------------------------------------------------ 0. 样本集自证

    [Fact]
    public void Site_Sample_Set_Is_Explicitly_Enumerated()
    {
        Assert.Equal(4, ExpectedSites.Length);
        foreach (var site in ExpectedSites)
        {
            Assert.False(string.IsNullOrWhiteSpace(ReadSource($"{CompatibilityServices}/{site}")));
        }
    }

    // ------------------------------------------------------------------ 1. F1：静默取第一的模式已清零

    [Theory]
    [InlineData("AttributeService.cs")]
    [InlineData("LayerService.cs")]
    [InlineData("SelectionService.cs")]
    [InlineData("GeoprocessingService.cs")]
    public void Site_No_Longer_Uses_Silent_First_Match(string site)
    {
        var content = ReadSource($"{CompatibilityServices}/{site}");

        Assert.DoesNotContain("FindLayer(layerName", content);
        Assert.DoesNotContain("map.Layers.FirstOrDefault(", content);
        Assert.DoesNotContain("FirstOrDefault(l => string.Equals(l.Name, layerName", content);
    }

    [Fact]
    public void Silent_First_Match_Pattern_Is_Zero_Repo_Wide()
    {
        // K2 纪律：修模式化缺陷须全仓扫描，而非只修已复现站点。
        var root = Path.Combine(RepoRoot(), "Source");
        var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        // 样本集自证：Source 下 .cs 文件数必须显著大于 0（防止路径解析失败 → 空集真空真）。
        Assert.True(files.Count > 50, $"Unexpectedly small source file set: {files.Count}");

        var offenders = files
            .Where(f => File.ReadAllText(f).Contains("FindLayer(layerName"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("AttributeService.cs")]
    [InlineData("LayerService.cs")]
    [InlineData("SelectionService.cs")]
    public void Site_Routes_Through_Shared_LayerResolver(string site)
    {
        var content = ReadSource($"{CompatibilityServices}/{site}");

        Assert.Contains("LayerResolver.Resolve", content);
    }

    [Fact]
    public void Fifth_Site_Geoprocessing_Delegates_To_Shared_LayerResolver()
    {
        var content = ReadSource($"{CompatibilityServices}/GeoprocessingService.cs");

        Assert.Contains("LayerResolver.ResolveExact", content);
        // D-024（F4 组子层可寻址性统一）：第 5 站点枚举语义由 flatten:false 改为 flatten:true——
        // 凡 get_layers(flatten=true) 可见的图层（含组内子层）均按名可寻址；重名仍 AMBIGUOUS_LAYER_NAME。
        // （原 D-022 断言 flatten:false 已随契约演进废止，见 GroupChildAddressabilityTests。）
        Assert.Contains("flatten: true", content);
        Assert.DoesNotContain("flatten: false", content);
    }

    [Fact]
    public void Shared_LayerResolver_Exists_And_Uses_Pure_Matcher()
    {
        var content = ReadSource($"{CompatibilityServices}/LayerResolver.cs");

        Assert.Contains("LayerNameMatcher.MatchByName", content);
        Assert.Equal(1, CountOccurrences(content, "ErrorCodes.AmbiguousLayerName"));
        Assert.Equal(0, CountOccurrences(content, "ErrorCodes.AmbiguousMapName"));
        Assert.Contains("ErrorCodes.LayerNotFound", content);
    }

    // ------------------------------------------------------------------ 2. K2b：错误码映射不再错配

    [Fact]
    public void AttributeService_Maps_Layer_Ambiguity_To_AmbiguousLayerName()
    {
        var content = ReadSource($"{CompatibilityServices}/AttributeService.cs");

        // 地图歧义仍为 AMBIGUOUS_MAP_NAME、图层歧义仍为 AMBIGUOUS_LAYER_NAME，且二者不得混用（K2b 核心）。
        // D-040 B 组：ResolveTable 改为返回 (Table?, ErrorCode?, ErrorMessage?) 元组——两歧义码各以
        // "return (null, ErrorCodes.AmbiguousXxx, ...)" 形式出现**恰 1 处**（语义与旧"赋值形态"等价）。
        Assert.Equal(1, CountOccurrences(content, "return (null, ErrorCodes.AmbiguousMapName,"));
        Assert.Equal(1, CountOccurrences(content, "return (null, ErrorCodes.AmbiguousLayerName,"));

        // 历史缺陷：调用方把 resolveError 硬编码为地图级错误码 → 必须消失。
        Assert.DoesNotContain("resolveError", content);
        Assert.DoesNotContain("Fail(ErrorCodes.AmbiguousMapName, resolveError)", content);
    }

    [Fact]
    public void SelectionService_And_LayerService_Use_Resolver_Error_Codes()
    {
        foreach (var site in new[] { "LayerService.cs", "SelectionService.cs" })
        {
            var content = ReadSource($"{CompatibilityServices}/{site}");
            Assert.Contains("LayerResolver.FailIfNotOk", content);
        }
    }

    // ------------------------------------------------------------------ 3. F2：OID 投影已接入

    [Fact]
    public void AttributeService_Projects_Oid_Through_Pure_Helper()
    {
        var content = ReadSource($"{CompatibilityServices}/AttributeService.cs");

        Assert.Contains("OidProjection.Build", content);
        Assert.Contains("OidProjection.UnavailableMessage", content);
        // 历史缺陷：直接把客户端字段原样投递。
        Assert.DoesNotContain("string.Join(\",\", request.FieldNames)", content);
    }

    [Fact]
    public void Pure_Helpers_Live_In_Core_Without_Sdk_Dependency()
    {
        // Rule 5：Shared 不得引用 ArcGIS Pro SDK。
        var matcher = ReadSource("Source/Shared/ArcGISProMCP.Core/Services/LayerNameMatcher.cs");
        var projection = ReadSource("Source/Shared/ArcGISProMCP.Core/Services/OidProjection.cs");

        foreach (var content in new[] { matcher, projection })
        {
            Assert.DoesNotContain("ArcGIS.Desktop", content);
            Assert.DoesNotContain("ArcGIS.Core", content);
        }
    }

    // ------------------------------------------------------------------ 4. 契约文本同步

    [Theory]
    // 期望条数：文件内**全部** AMBIGUOUS_LAYER_NAME 披露点（含 8.1 已披露的 get_layer_info）。
    [InlineData("Source/Shared/ArcGISProMCP.Tools/AttributeTools.cs", 3)]
    [InlineData("Source/Shared/ArcGISProMCP.Tools/LayerTools.cs", 3)]
    [InlineData("Source/Shared/ArcGISProMCP.Tools/SelectionTools.cs", 1)]
    public void Tool_Descriptions_Disclose_Ambiguity_Contract(string file, int expectedMentions)
    {
        var content = ReadSource(file);

        Assert.Equal(expectedMentions, CountOccurrences(content, "AMBIGUOUS_LAYER_NAME"));
    }

    [Fact]
    public void QueryAttributes_Description_Discloses_Oid_Semantics()
    {
        var content = ReadSource("Source/Shared/ArcGISProMCP.Tools/AttributeTools.cs");

        Assert.Contains("oid=-1", content);
        Assert.Contains("OID", content);
    }
}
