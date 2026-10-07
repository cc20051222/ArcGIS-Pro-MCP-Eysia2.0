using System.Text.RegularExpressions;

namespace ArcGISProMCP.UnitTests;

public sealed class DocumentationPolicyTests
{
    [Fact]
    public void CurrentDocumentationLinksResolveToRepositoryFiles()
    {
        var root = FindRepositoryRoot();
        var documents = new[]
        {
            "README.md",
            "Docs/USER_GUIDE.md",
            "Docs/RELEASE_WORKFLOW.md",
            "Docs/CLIENT_CONFIGURATION_GUIDE.md",
            "Docs/SHARING_AND_SIMPLE_INSTALL.md",
            "Docs/PROJECT_STATE.md",
            "Docs/CURRENT_TASK.md",
            "Docs/VERIFICATION.md",
            "Docs/CONTEXT_HANDOFF.md",
            "Docs/phases/PHASE_07.md",
            "Docs/phases/PHASE_07_IMPLEMENTATION_PLAN.md",
            "Docs/phases/PHASE_07_6_1_RELEASE_PORTABILITY_FOUNDATION.md",
            "Docs/phases/PHASE_07_6_2_USER_DOCUMENTATION_ONE_COMMAND.md",
        };

        foreach (var relative in documents)
        {
            var source = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(source), $"Missing documentation source: {relative}");
            var sourceDirectory = Path.GetDirectoryName(source)!;
            foreach (Match match in Regex.Matches(File.ReadAllText(source), @"\[[^\]]*\]\(([^)]+)\)"))
            {
                var link = match.Groups[1].Value.Trim().Trim('<', '>');
                var fragmentIndex = link.IndexOf('#');
                if (fragmentIndex >= 0) link = link[..fragmentIndex];
                if (string.IsNullOrWhiteSpace(link) ||
                    link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    link.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    link.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                    link.StartsWith("codex:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var target = Path.GetFullPath(Path.Combine(sourceDirectory, link.Replace('/', Path.DirectorySeparatorChar)));
                Assert.True(
                    File.Exists(target) || Directory.Exists(target),
                    $"Broken local link in {relative}: {link}");
            }
        }
    }

    [Fact]
    public void DocumentationMatchesCatalogAndWorkflowContract()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var userGuide = File.ReadAllText(Path.Combine(root, "Docs", "USER_GUIDE.md"));
        var releaseWorkflow = File.ReadAllText(Path.Combine(root, "Docs", "RELEASE_WORKFLOW.md"));
        var wrapper = File.ReadAllText(Path.Combine(root, "scripts", "user-workflow.ps1"));
        var catalog = File.ReadAllText(Path.Combine(root, "Config", "client-catalog.json"));

        foreach (var document in new[] { readme, userGuide, releaseWorkflow })
        {
            Assert.Contains("http://127.0.0.1:6520/mcp", document, StringComparison.Ordinal);
            Assert.Contains("arcgis-pro-mcp", document, StringComparison.Ordinal);
            Assert.Contains("30", document, StringComparison.Ordinal);
            Assert.Contains("NOT VERIFIED", document, StringComparison.Ordinal);
            Assert.Contains("ApplyAll", document, StringComparison.Ordinal);
        }

        Assert.Contains("\"canonicalProductionToolCount\": 239", catalog, StringComparison.Ordinal);
        foreach (var expectedClient in new[] { "codex", "cursor", "deepseek-harness", "claude-desktop" })
            Assert.Contains($"\"id\": \"{expectedClient}\"", catalog, StringComparison.Ordinal);

        foreach (var expectedAction in new[]
                 {
                     "Plan", "Validate", "Package", "Install", "Uninstall", "Rollback",
                     "ClientApply", "ClientRestore"
                 })
        {
            Assert.Contains(expectedAction, wrapper, StringComparison.Ordinal);
            Assert.Contains(expectedAction, releaseWorkflow, StringComparison.Ordinal);
        }

        Assert.Contains("-SkipRegistration", wrapper, StringComparison.Ordinal);
        Assert.Contains("-DryRun", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("LedgerPath", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("-PackagePath", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("-ManifestPath", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("-InstallRoot", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("-TransactionRoot", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("-LedgerPath", releaseWorkflow, StringComparison.Ordinal);

        foreach (var forbidden in new[] { "-TestMode", "-InjectFailureAfterInstall", "-CompatibilityFactsPath", "-ForceRestore" })
        {
            Assert.DoesNotContain(forbidden, wrapper, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbidden, readme, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbidden, userGuide, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbidden, releaseWorkflow, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CurrentStatusAndUnsupportedClaimsAreEvidenceAccurate()
    {
        var root = FindRepositoryRoot();
        var currentDocuments = new[]
        {
            File.ReadAllText(Path.Combine(root, "README.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "USER_GUIDE.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "RELEASE_WORKFLOW.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "PROJECT_STATE.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "CURRENT_TASK.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "VERIFICATION.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "CONTEXT_HANDOFF.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "phases", "PHASE_07.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "phases", "PHASE_07_IMPLEMENTATION_PLAN.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "phases", "PHASE_07_6_2_USER_DOCUMENTATION_ONE_COMMAND.md")),
        };
        var currentText = string.Join(Environment.NewLine, currentDocuments);

        Assert.Contains("7.6.1", currentText, StringComparison.Ordinal);
        Assert.Contains("FORMALLY ACCEPTED / PASS", currentText, StringComparison.Ordinal);
        Assert.Contains("7.6.2", currentText, StringComparison.Ordinal);
        Assert.Contains("PASS CANDIDATE", currentText, StringComparison.Ordinal);
        Assert.Contains("7.7", currentText, StringComparison.Ordinal);
        Assert.Contains("NOT STARTED", currentText, StringComparison.Ordinal);
        Assert.Contains("NOT VERIFIED", currentText, StringComparison.Ordinal);

        foreach (var forbidden in new[]
                 {
                     "clean-machine support = PASS",
                     "installed runtime = PASS",
                     "fresh client acceptance = PASS",
                     "Phase 7.7 = PASS"
                 })
        {
            Assert.DoesNotContain(forbidden, currentText, StringComparison.OrdinalIgnoreCase);
        }

        var phaseReport = File.ReadAllText(Path.Combine(
            root, "Docs", "phases", "PHASE_07_6_1_RELEASE_PORTABILITY_FOUNDATION.md"));
        Assert.Contains("PASS CANDIDATE", phaseReport, StringComparison.Ordinal);
        Assert.Contains("Independent Gate Keeper Formal Acceptance", phaseReport, StringComparison.Ordinal);
        Assert.Contains("FORMALLY ACCEPT — PHASE 7.6.1 PASS", phaseReport, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentUserDocumentationUsesPortableRepositoryRootInstructions()
    {
        var root = FindRepositoryRoot();
        var currentUserDocuments = new[]
        {
            File.ReadAllText(Path.Combine(root, "README.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "USER_GUIDE.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "RELEASE_WORKFLOW.md")),
            File.ReadAllText(Path.Combine(root, "Docs", "CLIENT_CONFIGURATION_GUIDE.md")),
        };

        foreach (var document in currentUserDocuments)
            Assert.DoesNotContain(@"D:\ArcGIS-Pro-MCP", document, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShareBundleEntryPointsPreserveTransactionalAndSingleClientBoundaries()
    {
        var root = FindRepositoryRoot();
        var launcher = File.ReadAllText(Path.Combine(root, "scripts", "share-setup.ps1"));
        var builder = File.ReadAllText(Path.Combine(root, "scripts", "build-share-package.ps1"));
        var guide = File.ReadAllText(Path.Combine(root, "Docs", "SHARING_AND_SIMPLE_INSTALL.md"));

        foreach (var relative in new[]
                 {
                     "Distribution/START-HERE.cmd",
                     "Distribution/INSTALL-PLUGIN.cmd",
                     "Distribution/CONFIGURE-CODEX.cmd",
                     "Distribution/UNINSTALL-PLUGIN.cmd",
                     "Distribution/README-START-HERE.md",
                     "scripts/share-setup.ps1",
                     "scripts/build-share-package.ps1"
                 })
            Assert.True(File.Exists(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))), relative);

        Assert.Contains("user-workflow.ps1", launcher, StringComparison.Ordinal);
        Assert.Contains("latest-transaction.json", launcher, StringComparison.Ordinal);
        Assert.Contains("SAFE_TRANSACTION_STATE_NOT_FOUND", launcher, StringComparison.Ordinal);
        Assert.Contains("STALE_BACKUP_REFUSED", guide, StringComparison.Ordinal);
        Assert.Contains("NOT VERIFIED", guide, StringComparison.Ordinal);
        Assert.Contains("ApplyAll", guide, StringComparison.Ordinal);
        Assert.Contains("registration = 'SKIPPED'", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("ClientApplyAll", launcher, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MyProject1.aprx", launcher, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Phase4Test.gdb", launcher, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ArcGIS-Pro-MCP.sln")) &&
                File.Exists(Path.Combine(directory.FullName, "README.md")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
