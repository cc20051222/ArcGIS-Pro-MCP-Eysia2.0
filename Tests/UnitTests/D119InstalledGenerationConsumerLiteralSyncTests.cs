using System.Text.RegularExpressions;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-119（G-278 派 · G-291 开工序列）装机代际消费者字面量同源守卫：闸门与诊断文案 ≡ 现役契约工具数。
/// </summary>
/// <remarks>
/// 代际教训沿用 D-113/D-115：字面量停在上一个代际（154／78／224）会让仓库件对现役载荷自拒，
/// 或使真机诊断文案与实数不符。224→239 推进后，本守卫把两处消费者件的 239 字面量钉在契约快照与
/// one-click 自身 canonicalToolCount 变量上，并禁止已淘汰代际字面量回流。零新增工具、零注册变更。
/// </remarks>
public sealed class D119InstalledGenerationConsumerLiteralSyncTests
{
    private static string RepoRoot => TestRepo.Root;

    private static string ReadScript(string relativePath)
    {
        var path = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, System.Text.Encoding.UTF8);
    }

    private static string StripCommentLines(string text) => string.Join(
        "\n",
        text.Replace("\r\n", "\n").Split('\n').Where(line => !line.TrimStart().StartsWith("#", StringComparison.Ordinal)));

    private static int SingleMatch(string text, string pattern)
    {
        var matches = Regex.Matches(text, pattern);
        Assert.Single(matches);
        return int.Parse(matches[0].Groups[1].Value);
    }

    [Theory]
    [InlineData("scripts/one-click-setup.ps1")]
    [InlineData("scripts/build-share-package.ps1")]
    public void ConsumerScripts_DoNotCarrySupersededGenerationToolCountLiteral(string relativeScript)
    {
        var code = StripCommentLines(ReadScript(relativeScript));
        Assert.Empty(Regex.Matches(code, @"\b224\b"));
        Assert.Empty(Regex.Matches(code, @"\b154\b"));
        Assert.Empty(Regex.Matches(code, @"\b78\b"));
    }

    [Theory]
    [InlineData("scripts/one-click-setup.ps1")]
    [InlineData("scripts/build-share-package.ps1")]
    public void ConsumerScripts_CanonicalToolCountLiteral_MatchesCurrentContractSnapshot(string relativeScript)
    {
        var expected = ProductionToolContractSnapshot.Tools.Count;
        Assert.Equal(239, expected);
        Assert.Equal(expected, SingleMatch(ReadScript(relativeScript), @"canonicalProductionToolCount = (\d+);"));
    }

    [Fact]
    public void OneClickSetup_PlanLiteralAndCanonicalNamesVariable_AreSameSource()
    {
        var code = StripCommentLines(ReadScript("scripts/one-click-setup.ps1"));
        Assert.Equal(SingleMatch(code, @"\$script:canonicalToolCount = (\d+)"),
                     SingleMatch(code, @"canonicalProductionToolCount = (\d+);"));
    }

    [Fact]
    public void OneClickSetup_HttpDiagnosisProse_CarriesCurrentGenerationCount()
    {
        var code = StripCommentLines(ReadScript("scripts/one-click-setup.ps1"));
        Assert.Equal(2, Regex.Matches(code, @"canonical 239").Count);
        Assert.Single(Regex.Matches(code, @"tools/list=239"));
    }

    // ---------- D-121 f3-consumer 扩位：向导副标（第 6 消费者）＋全产品树代际面归零 ----------
    private static readonly string[] GenerationRoots = { "Source", "Tests", "Config", "scripts", "tools" };
    private static readonly string[] GenerationExtensions = { ".cs", ".ps1", ".py", ".json", ".md", ".cmd", ".bat", ".xaml", ".daml" };
    private static readonly string[] SkipDirs = { "bin", "obj", ".vs", "TestResults", "node_modules" };

    private static IEnumerable<string> GenerationFaceFiles()
    {
        foreach (var root in GenerationRoots)
        {
            var baseDir = Path.Combine(RepoRoot, root);
            if (!Directory.Exists(baseDir)) continue;
            foreach (var path in Directory.EnumerateFiles(baseDir, "*.*", SearchOption.AllDirectories))
            {
                var directoryName = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;
                if (SkipDirs.Contains(directoryName, StringComparer.OrdinalIgnoreCase)) continue;
                if (GenerationExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())) yield return path;
            }
        }
    }

    [Fact]
    public void OneClickSetup_WizardSubtitleCanonicalToolCount_MatchesCurrentContractSnapshot()
    {
        var code = StripCommentLines(ReadScript("scripts/one-click-setup.ps1"));
        Assert.Single(Regex.Matches(code, @"canonical tools=(\d+)"));
        Assert.Equal(ProductionToolContractSnapshot.Tools.Count,
                     int.Parse(Regex.Match(code, @"canonical tools=(\d+)").Groups[1].Value));
    }

    private static string StripCodeComments(string text) => string.Join(
        "\n",
        text.Replace("\r\n", "\n").Split('\n')
            .Where(line => !(line.TrimStart().StartsWith("#", StringComparison.Ordinal)
                             || line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                             || line.TrimStart().StartsWith("<!--", StringComparison.Ordinal))));

    [Fact]
    public void ProductTrees_ToolCountFace_CarriesNoSupersededGenerationLiterals()
    {
        var expected = ProductionToolContractSnapshot.Tools.Count;
        Assert.Equal(239, expected);
        var assignments = 0;
        var wizardSubtitles = 0;
        foreach (var path in GenerationFaceFiles())
        {
            var text = File.ReadAllText(path, System.Text.Encoding.UTF8);
            var code = StripCodeComments(text);
            foreach (Match match in Regex.Matches(code, @"canonical(?:Production)?ToolCount\s*(?:=|-ne|:""?)\s*""?(\d+)"))
            {
                assignments++;
                Assert.Equal(expected, int.Parse(match.Groups[1].Value));
            }
            foreach (Match match in Regex.Matches(code, @"canonical tools=(\d+)"))
            {
                wizardSubtitles++;
                Assert.Equal(expected, int.Parse(match.Groups[1].Value));
            }
            Assert.Empty(Regex.Matches(code, @"\b224\b"));
        }
        Assert.True(assignments >= 6, $"expected the six tool-count-bearing scripts to be scanned, found {assignments} assignments");
        Assert.Equal(1, wizardSubtitles);
    }

    // ---------- D-120 f1-catalog（G-295 R-3① · 清偿 O-D121-01）：Config 机读面的工具计数键 ≡ 现值 ----------
    [Fact]
    public void ConfigMachineFaces_ToolCountKeys_MatchCurrentContractSnapshot()
    {
        var expected = ProductionToolContractSnapshot.Tools.Count;
        Assert.Equal(239, expected);
        var configDir = Path.Combine(RepoRoot, "Config");
        var keys = 0;
        foreach (var path in Directory.EnumerateFiles(configDir, "*.json", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path, System.Text.Encoding.UTF8);
            foreach (Match match in Regex.Matches(text, @"""(?:canonical)?[Pp]roductionToolCount""\s*:\s*(\d+)"))
            {
                keys++;
                Assert.Equal(expected, int.Parse(match.Groups[1].Value));
            }
        }
        Assert.True(keys >= 2, $"expected both client-catalog tool-count keys to be pinned, found {keys}");
    }
}
