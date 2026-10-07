using System.Text.RegularExpressions;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-113（G-246 派 · R-G245-P1＋R-G244-P1/P2 清偿）分发闸门与包内 Config.daml 同源守卫。
/// </summary>
/// <remarks>
/// 闸门数值曾停留在 r21 代际（154），导致仓库件校验现役 r22/r23 包被自家判为 BUNDLE_IDENTITY_INVALID；
/// 兼容门入口则曾固定读外层 net6 元数据 Config.daml（desktopVersion 3.0），对 net8 载荷自拒（D-111 已修）。
/// 本守卫把两代回归钉死在现役契约数值与"同源指针"上，零新增工具、零注册变更。
/// </remarks>
public sealed class D113DistributionGateAndConfigDamlSameSourcePolicyTests
{
    private static string RepoRoot => TestRepo.Root;

    private static string ReadScript(string relativePath)
    {
        var path = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, System.Text.Encoding.UTF8);
    }

    /// <summary>Comment-only lines must not drive the same-source assertions.</summary>
    private static string StripCommentLines(string text) => string.Join(
        "\n",
        text.Replace("\r\n", "\n").Split('\n').Where(line => !line.TrimStart().StartsWith("#", StringComparison.Ordinal)));

    private static int SingleMatch(string text, string pattern)
    {
        var matches = Regex.Matches(text, pattern);
        Assert.Single(matches);
        return int.Parse(matches[0].Groups[1].Value);
    }

    // ---------- 1) verify 件闸门 ≡ 现役契约工具数 ----------
    [Fact]
    public void VerifyScript_ToolCountGate_MatchesCurrentContractSnapshot()
    {
        var expected = ProductionToolContractSnapshot.Tools.Count;
        Assert.Equal(239, expected);
        var text = ReadScript("scripts/verify-one-click-package.ps1");
        Assert.Equal(expected, SingleMatch(text, @"canonicalProductionToolCount -ne (\d+)"));
        Assert.Equal(expected, SingleMatch(text, @"canonicalProductionToolCount = (\d+);"));
    }

    // ---------- 2) verify 件可接受修订含现役 r22/r23 ----------
    [Fact]
    public void VerifyScript_AcceptedDeploymentVersions_IncludeCurrentRevisions()
    {
        var text = ReadScript("scripts/verify-one-click-package.ps1");
        var revisions = Regex.Matches(text, @"one-click-1\.0\.2-r(\d+)").Select(m => int.Parse(m.Groups[1].Value)).ToList();
        var total = revisions.Count;
        var distinct = revisions.Distinct().Count();
        var highest = revisions.Max();
        Assert.True(total >= 23, $"expected at least r1..r23, found {total} entries");
        Assert.Contains(22, revisions);
        Assert.Contains(23, revisions);
        Assert.Equal(total, distinct);
        Assert.Equal(Enumerable.Range(1, highest), revisions.OrderBy(x => x));
    }

    // ---------- 3) build 件与 verify 件口径一致 ----------
    [Fact]
    public void BuildScript_ToolCountLiteral_MatchesVerifyScriptGate()
    {
        var verifyGate = SingleMatch(ReadScript("scripts/verify-one-click-package.ps1"),
                                     @"canonicalProductionToolCount -ne (\d+)");
        Assert.Equal(verifyGate, SingleMatch(ReadScript("scripts/build-one-click-package.ps1"),
                                             @"canonicalProductionToolCount = (\d+);"));
    }

    // ---------- 4) 闸门数值不得再以 78/154 代际残留出现（D-114 五件；D-115 纳入两处 catalog 消费者闸面）----------
    [Theory]
    [InlineData("scripts/verify-one-click-package.ps1")]
    [InlineData("scripts/build-one-click-package.ps1")]
    [InlineData("scripts/one-click-setup.ps1")]
    [InlineData("scripts/build-share-package.ps1")]
    [InlineData("scripts/check-compatibility.ps1")]
    [InlineData("scripts/user-workflow.ps1")]
    [InlineData("scripts/client-config.ps1")]
    public void GateScripts_DoNotCarrySupersededGenerationToolCountLiteral(string relativeScript)
    {
        var code = StripCommentLines(ReadScript(relativeScript));
        Assert.Empty(Regex.Matches(code, @"\b154\b"));
        Assert.Empty(Regex.Matches(code, @"\b78\b"));
    }

    // ---------- 4d) catalog ≡ 两处消费者闸面 ≡ 文档断言，四方同值（D-115 / G-249 R-2）----------
    [Fact]
    public void Catalog_ConsumersAndDocumentationAssertion_ShareOneCurrentToolCount()
    {
        var expected = ProductionToolContractSnapshot.Tools.Count;   // 现役契约数；注册表面由既有三向一致测试钉住
        Assert.Equal(239, expected);

        var catalog = SingleMatch(ReadScript("Config/client-catalog.json"), @"""canonicalProductionToolCount"": (\d+)");
        var workflowGate = SingleMatch(ReadScript("scripts/user-workflow.ps1"), @"canonicalProductionToolCount -ne (\d+)");
        var clientGate = SingleMatch(ReadScript("scripts/client-config.ps1"), @"canonicalProductionToolCount -ne (\d+)");
        var docAssertion = SingleMatch(ReadScript("Tests/UnitTests/DocumentationPolicyTests.cs"),
                                       @"""canonicalProductionToolCount\\"": (\d+)");

        Assert.Equal(expected, catalog);
        Assert.Equal(catalog, workflowGate);      // 单向改动即 WORKFLOW_CATALOG_INVALID
        Assert.Equal(catalog, clientGate);       // 单向改动即 CATALOG_TOOL_NORMALIZATION_INVALID
        Assert.Equal(catalog, docAssertion);     // 单向改动即文档政策断言红
    }

    // ---------- 4b) one-click-setup 名单面 ≡ 真实 Composition 注册表（D-114 契约预检）----------
    [Fact]
    public void OneClickSetup_CanonicalToolNames_MatchTheProductionRegistryExactly()
    {
        var text = ReadScript("scripts/one-click-setup.ps1");
        var block = Regex.Match(text, @"\$script:canonicalToolNames = @\((?<body>.*?)^\)",
                                RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(block.Success, "canonicalToolNames block not found");
        var listed = Regex.Matches(block.Groups["body"].Value, "'([^']+)'").Select(m => m.Groups[1].Value).ToList();
        var registry = ProductionCompositionAccess.BuildRegistry().List().Select(t => t.Name).ToList();

        Assert.Equal(registry.Count, listed.Count);                                  // 数量逐一相等（现役 239）
        Assert.Equal(239, listed.Count);
        Assert.Equal(listed.Count, listed.Distinct(StringComparer.Ordinal).Count());  // 无重名
        Assert.True(listed.OrderBy(x => x, StringComparer.Ordinal)
                          .SequenceEqual(registry.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal),
                          "one-click-setup canonical list must equal the registered production tool names");
        var countGate = int.Parse(Regex.Match(text, @"\$script:canonicalToolCount = (\d+)").Groups[1].Value);
        Assert.Equal(registry.Count, countGate);
    }

    // ---------- 4c) builder 双载荷结构（D-114 范围④；运行期由分发批自证，本批禁制包）----------
    [Fact]
    public void BuildScript_PayloadLayout_IsDualLayerNet6AndNet8()
    {
        var code = StripCommentLines(ReadScript("scripts/build-one-click-package.ps1"));
        foreach (var tf in new[] { "net6.0-windows", "net8.0-windows" })
        {
            Assert.Contains(tf, code, StringComparison.Ordinal);
        }
        Assert.Contains("'payload\\' + $layer.Tf", code, StringComparison.Ordinal);
        Assert.Contains("$layer.PackagePath", code, StringComparison.Ordinal);
        Assert.Contains("$layer.ManifestPath", code, StringComparison.Ordinal);
        Assert.Contains("payloads = $payloadRecords", code, StringComparison.Ordinal);
        var recorded = Regex.Matches(code, @"'payload/' \+ \$\w+\.Tf \+ '/' \+ (\$packageFileName|\$manifestFileName)")
                             .Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.Equal(2, recorded.Count);
        // 单层时代的两个残留形态不得复现：直接拷 $packagePath，以及只落 payload\ 根级 esriAddInX。
        Assert.DoesNotContain("-LiteralPath $packagePath", code, StringComparison.Ordinal);
        Assert.DoesNotContain("'payload\\ArcGISProMCP.Compatibility.esriAddInX'", code, StringComparison.Ordinal);
        // 单层时代只校验一个 net6 产物；双载荷必须逐层以各自 release manifest 作身份锚。
        Assert.Contains("RELEASE_MANIFEST_IDENTITY_INVALID:' + $layer.Tf", code, StringComparison.Ordinal);
        Assert.Contains("arcgisProDesktopVersion -cne $layer.DesktopVersion", code, StringComparison.Ordinal);
    }

    // ---------- 5) 兼容门 Config.daml 同源指针（D-111 回归守卫）----------
    [Theory]
    [InlineData("scripts/one-click-setup.ps1", "Get-PayloadConfigDaml")]
    [InlineData("scripts/share-setup.ps1", "Get-PayloadConfigDaml")]
    public void CompatibilityEntryPoints_ReadConfigDamlFromPayload(string relativeScript, string helperName)
    {
        var code = StripCommentLines(ReadScript(relativeScript));
        Assert.Contains("function " + helperName, code);
        Assert.Contains("'-ConfigPath', $extracted.Path", code, StringComparison.Ordinal);
        Assert.DoesNotContain("$configDamlPath = Join-Path", code, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"Source[/\\]ArcGISProMCP\.Compatibility[/\\]Config\.daml", code);
    }

    // ---------- 6) 同源提取的可判读元数据在场 ----------
    [Fact]
    public void PayloadConfigDamlHelper_ReportsSameSourceOriginAndDesktopVersion()
    {
        foreach (var relative in new[] { "scripts/one-click-setup.ps1", "scripts/share-setup.ps1" })
        {
            var code = ReadScript(relative);
            Assert.Contains("configDamlOrigin = 'payload entry Config.daml (same-source)'", code, StringComparison.Ordinal);
            Assert.Contains("COMPATIBILITY_PAYLOAD_CONFIG_DAML_MISSING", code, StringComparison.Ordinal);
            Assert.Contains("COMPATIBILITY_PAYLOAD_NOT_FOUND", code, StringComparison.Ordinal);
        }
    }
}
