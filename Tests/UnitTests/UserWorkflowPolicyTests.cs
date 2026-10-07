using System.Diagnostics;
using System.Text.Json;

namespace ArcGISProMCP.UnitTests;

public sealed class UserWorkflowPolicyTests
{
    [Fact]
    public void DefaultPlanIsReadOnlyAndDoesNotCreateFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandbox = CreateSandbox(repositoryRoot, includeNoOpClientScript: true);
        try
        {
            var before = SnapshotTree(sandbox);
            var result = RunWorkflow(sandbox, "-Action", "Plan");
            Assert.Equal(0, result.ExitCode);

            using var json = JsonDocument.Parse(result.Output);
            Assert.Equal("arcgis-pro-mcp-user-workflow-ledger-v1", json.RootElement.GetProperty("schema").GetString());
            Assert.Equal("Plan", json.RootElement.GetProperty("action").GetString());
            Assert.True(json.RootElement.GetProperty("readOnly").GetBoolean());
            Assert.False(json.RootElement.GetProperty("mutationAttempted").GetBoolean());
            Assert.Equal("PASS", json.RootElement.GetProperty("status").GetString());
            Assert.Equal(before, SnapshotTree(sandbox));
        }
        finally
        {
            DeleteOwnedDirectory(sandbox);
        }
    }

    [Fact]
    public void ValidateWithExplicitClientIsReadOnly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandbox = CreateSandbox(repositoryRoot, includeNoOpClientScript: true);
        try
        {
            var configRoot = Path.Combine(sandbox, "owned-config-that-does-not-exist");
            var templateRoot = Path.Combine(sandbox, "owned-template-that-does-not-exist");
            var before = SnapshotTree(sandbox);
            var result = RunWorkflow(
                sandbox,
                "-Action", "Validate",
                "-Client", "codex",
                "-ConfigRoot", configRoot,
                "-TemplateRoot", templateRoot);

            Assert.Equal(0, result.ExitCode);
            using var json = JsonDocument.Parse(result.Output);
            Assert.True(json.RootElement.GetProperty("readOnly").GetBoolean());
            Assert.Equal("PASS", json.RootElement.GetProperty("status").GetString());
            Assert.Equal(before, SnapshotTree(sandbox));
            Assert.False(Directory.Exists(configRoot));
            Assert.False(Directory.Exists(templateRoot));
        }
        finally
        {
            DeleteOwnedDirectory(sandbox);
        }
    }

    [Fact]
    public void ClientMutationRequiresOneExplicitCatalogClientWithoutStartingChild()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandbox = CreateSandbox(repositoryRoot, includeNoOpClientScript: true);
        try
        {
            var before = SnapshotTree(sandbox);
            var result = RunWorkflow(sandbox, "-Action", "ClientApply");

            Assert.NotEqual(0, result.ExitCode);
            using var json = JsonDocument.Parse(result.Output);
            Assert.Equal("FAILED", json.RootElement.GetProperty("status").GetString());
            Assert.Equal(
                "WORKFLOW_CLIENT_REQUIRED",
                json.RootElement.GetProperty("failed")[0].GetProperty("errorCode").GetString());
            Assert.Equal(before, SnapshotTree(sandbox));
            Assert.False(File.Exists(Path.Combine(sandbox, "scripts", "delegation.log")));
        }
        finally
        {
            DeleteOwnedDirectory(sandbox);
        }
    }

    [Fact]
    public void WrapperDelegatesPackageReleaseAndSingleClientActionsWithSafeArguments()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandbox = CreateSandbox(repositoryRoot, includeNoOpClientScript: false);
        try
        {
            var packageResult = RunWorkflow(
                sandbox,
                "-Action", "Package",
                "-Configuration", "Debug",
                "-ProjectDir", Path.Combine(sandbox, "owned-project"),
                "-ManifestPath", Path.Combine(sandbox, "owned", "manifest.json"));
            Assert.Equal(0, packageResult.ExitCode);

            var releaseResult = RunWorkflow(
                sandbox,
                "-Action", "Install",
                "-DryRun",
                "-PackagePath", Path.Combine(sandbox, "owned", "package.esriAddInX"),
                "-ManifestPath", Path.Combine(sandbox, "owned", "manifest.json"),
                "-InstallRoot", Path.Combine(sandbox, "owned", "install"),
                "-TransactionRoot", Path.Combine(sandbox, "owned", "transaction"),
                "-LedgerPath", Path.Combine(sandbox, "owned", "transaction", "ledger.json"));
            Assert.Equal(0, releaseResult.ExitCode);
            using (var releaseJson = JsonDocument.Parse(releaseResult.Output))
            {
                Assert.True(releaseJson.RootElement.GetProperty("readOnly").GetBoolean());
                Assert.False(releaseJson.RootElement.GetProperty("mutationAttempted").GetBoolean());
                Assert.Contains(
                    releaseJson.RootElement.GetProperty("completed").EnumerateArray(),
                    item => item.GetProperty("step").GetString() == "release:dry-run");
                Assert.DoesNotContain("WORKFLOW_LEDGER_INCOMPLETE", releaseResult.Output, StringComparison.Ordinal);
            }

            var clientResult = RunWorkflow(
                sandbox,
                "-Action", "ClientApply",
                "-Client", "codex",
                "-ConfigRoot", Path.Combine(sandbox, "owned", "config"),
                "-TemplateRoot", Path.Combine(sandbox, "owned", "templates"));
            Assert.Equal(0, clientResult.ExitCode);

            var entries = File.ReadAllLines(Path.Combine(sandbox, "scripts", "delegation.log"))
                .Select(line => JsonDocument.Parse(line))
                .Select(document =>
                {
                    using (document)
                    {
                        var script = document.RootElement.GetProperty("script").GetString()!;
                        var args = document.RootElement.GetProperty("args")
                            .EnumerateArray()
                            .Select(item => item.GetString() ?? string.Empty)
                            .ToArray();
                        return (script, args);
                    }
                })
                .ToArray();

            var package = Assert.Single(entries.Where(entry => entry.script.Equals("package-addin.ps1", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains("-SkipRegistration", package.args, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("-Configuration", package.args, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("-ProjectDir", package.args, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("-ManifestPath", package.args, StringComparer.OrdinalIgnoreCase);

            var release = Assert.Single(entries.Where(entry => entry.script.Equals("release-transaction.ps1", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains("-Action", release.args, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Install", release.args, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("-DryRun", release.args, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("-TestMode", release.args, StringComparer.OrdinalIgnoreCase);

            var client = Assert.Single(entries.Where(entry => entry.script.Equals("client-config.ps1", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains("-Action", client.args, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Apply", client.args, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("-Client", client.args, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("codex", client.args, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("-Json", client.args, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteOwnedDirectory(sandbox);
        }
    }

    [Fact]
    public void ChildFailurePropagatesNonzeroAndHidesRawChildOutput()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandbox = CreateSandbox(repositoryRoot, includeNoOpClientScript: false);
        try
        {
            var result = RunWorkflow(
                sandbox,
                new Dictionary<string, string> { ["ARCGIS_MCP_TEST_FAKE_FAIL"] = "1" },
                "-Action", "Package");

            Assert.Equal(23, result.ExitCode);
            Assert.DoesNotContain("PRIVATE_CHILD_OUTPUT_FOR_TEST", result.Output, StringComparison.Ordinal);
            using var json = JsonDocument.Parse(result.Output);
            Assert.Equal("FAILED", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("WORKFLOW_CHILD_FAILED", json.RootElement.GetProperty("failed")[0].GetProperty("errorCode").GetString());
        }
        finally
        {
            DeleteOwnedDirectory(sandbox);
        }
    }

    [Fact]
    public void ReleaseFailureReportsVerifiedAutomaticRollbackAndNotStartedSteps()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandbox = CreateSandbox(repositoryRoot, includeNoOpClientScript: false);
        try
        {
            var result = RunWorkflow(
                sandbox,
                new Dictionary<string, string> { ["ARCGIS_MCP_TEST_LEDGER_MODE"] = "rollback-success" },
                "-Action", "Install",
                "-PackagePath", Path.Combine(sandbox, "owned", "package.esriAddInX"),
                "-ManifestPath", Path.Combine(sandbox, "owned", "manifest.json"),
                "-InstallRoot", Path.Combine(sandbox, "owned", "install"),
                "-TransactionRoot", Path.Combine(sandbox, "owned", "transaction"),
                "-LedgerPath", Path.Combine(sandbox, "owned", "transaction", "ledger.json"));

            Assert.Equal(23, result.ExitCode);
            using var json = JsonDocument.Parse(result.Output);
            Assert.False(json.RootElement.GetProperty("readOnly").GetBoolean());
            Assert.True(json.RootElement.GetProperty("mutationAttempted").GetBoolean());
            Assert.Equal("FAILED", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("AUTOMATIC_ROLLBACK_VERIFIED", json.RootElement.GetProperty("recovery").GetProperty("state").GetString());
            Assert.Equal("NONE", json.RootElement.GetProperty("recovery").GetProperty("action").GetString());
            Assert.Equal("FAIL", json.RootElement.GetProperty("transaction").GetProperty("status").GetString());
            Assert.Contains(
                json.RootElement.GetProperty("completed").EnumerateArray(),
                item => item.GetProperty("step").GetString() == "release:rollback");
            Assert.Contains(
                json.RootElement.GetProperty("failed").EnumerateArray(),
                item => item.GetProperty("step").GetString() == "release:install");
            Assert.Contains(
                json.RootElement.GetProperty("notStarted").EnumerateArray(),
                item => item.GetProperty("step").GetString() == "release:install-verify");
            Assert.DoesNotContain("PRIVATE_CHILD_OUTPUT_FOR_TEST", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteOwnedDirectory(sandbox);
        }
    }

    [Fact]
    public void ReleaseFailureReportsRollbackRequiredWhenAutomaticRollbackFails()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandbox = CreateSandbox(repositoryRoot, includeNoOpClientScript: false);
        try
        {
            var result = RunWorkflow(
                sandbox,
                new Dictionary<string, string> { ["ARCGIS_MCP_TEST_LEDGER_MODE"] = "rollback-failure" },
                "-Action", "Install",
                "-PackagePath", Path.Combine(sandbox, "owned", "package.esriAddInX"),
                "-ManifestPath", Path.Combine(sandbox, "owned", "manifest.json"),
                "-InstallRoot", Path.Combine(sandbox, "owned", "install"),
                "-TransactionRoot", Path.Combine(sandbox, "owned", "transaction"),
                "-LedgerPath", Path.Combine(sandbox, "owned", "transaction", "ledger.json"));

            Assert.Equal(23, result.ExitCode);
            using var json = JsonDocument.Parse(result.Output);
            Assert.Equal("ROLLBACK_REQUIRED", json.RootElement.GetProperty("recovery").GetProperty("state").GetString());
            Assert.Equal("Rollback", json.RootElement.GetProperty("recovery").GetProperty("action").GetString());
            Assert.Contains(
                json.RootElement.GetProperty("failed").EnumerateArray(),
                item => item.GetProperty("step").GetString() == "release:automatic-rollback");
            Assert.DoesNotContain("PRIVATE_CHILD_OUTPUT_FOR_TEST", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteOwnedDirectory(sandbox);
        }
    }

    [Fact]
    public void ReleaseFailureWithoutLedgerIsNotPresentedAsRollbackSuccess()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandbox = CreateSandbox(repositoryRoot, includeNoOpClientScript: false);
        try
        {
            var result = RunWorkflow(
                sandbox,
                new Dictionary<string, string> { ["ARCGIS_MCP_TEST_LEDGER_MODE"] = "missing" },
                "-Action", "Install",
                "-PackagePath", Path.Combine(sandbox, "owned", "package.esriAddInX"),
                "-ManifestPath", Path.Combine(sandbox, "owned", "manifest.json"),
                "-InstallRoot", Path.Combine(sandbox, "owned", "install"),
                "-TransactionRoot", Path.Combine(sandbox, "owned", "transaction"),
                "-LedgerPath", Path.Combine(sandbox, "owned", "transaction", "ledger.json"));

            Assert.Equal(23, result.ExitCode);
            using var json = JsonDocument.Parse(result.Output);
            Assert.Equal("TRANSACTION_LEDGER_NOT_VERIFIED", json.RootElement.GetProperty("recovery").GetProperty("state").GetString());
            Assert.Equal("Preflight", json.RootElement.GetProperty("recovery").GetProperty("action").GetString());
            Assert.Equal("NOT_VERIFIED", json.RootElement.GetProperty("transaction").GetProperty("availability").GetString());
            Assert.DoesNotContain("AUTOMATIC_ROLLBACK_VERIFIED", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteOwnedDirectory(sandbox);
        }
    }

    [Fact]
    public void ReleaseLedgerActionMismatchFailsClosedWithoutAcceptingOutcome()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandbox = CreateSandbox(repositoryRoot, includeNoOpClientScript: false);
        try
        {
            var result = RunWorkflow(
                sandbox,
                new Dictionary<string, string> { ["ARCGIS_MCP_TEST_LEDGER_MODE"] = "action-mismatch" },
                "-Action", "Install",
                "-PackagePath", Path.Combine(sandbox, "owned", "package.esriAddInX"),
                "-ManifestPath", Path.Combine(sandbox, "owned", "manifest.json"),
                "-InstallRoot", Path.Combine(sandbox, "owned", "install"),
                "-TransactionRoot", Path.Combine(sandbox, "owned", "transaction"),
                "-LedgerPath", Path.Combine(sandbox, "owned", "transaction", "ledger.json"));

            Assert.Equal(1, result.ExitCode);
            using var json = JsonDocument.Parse(result.Output);
            Assert.False(json.RootElement.GetProperty("readOnly").GetBoolean());
            Assert.True(json.RootElement.GetProperty("mutationAttempted").GetBoolean());
            Assert.Equal("FAILED", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("TRANSACTION_LEDGER_NOT_VERIFIED", json.RootElement.GetProperty("recovery").GetProperty("state").GetString());
            Assert.Equal("NOT_VERIFIED", json.RootElement.GetProperty("transaction").GetProperty("availability").GetString());
            Assert.DoesNotContain("release:install", json.RootElement.GetProperty("completed").EnumerateArray().Select(item => item.GetProperty("step").GetString()));
            Assert.DoesNotContain("PRIVATE_CHILD_OUTPUT_FOR_TEST", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteOwnedDirectory(sandbox);
        }
    }

    [Fact]
    public void ReleaseLedgerDryRunMismatchFailsClosedWithoutAcceptingOutcome()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandbox = CreateSandbox(repositoryRoot, includeNoOpClientScript: false);
        try
        {
            var result = RunWorkflow(
                sandbox,
                new Dictionary<string, string> { ["ARCGIS_MCP_TEST_LEDGER_MODE"] = "dryrun-mismatch" },
                "-Action", "Install",
                "-DryRun",
                "-PackagePath", Path.Combine(sandbox, "owned", "package.esriAddInX"),
                "-ManifestPath", Path.Combine(sandbox, "owned", "manifest.json"),
                "-InstallRoot", Path.Combine(sandbox, "owned", "install"),
                "-TransactionRoot", Path.Combine(sandbox, "owned", "transaction"),
                "-LedgerPath", Path.Combine(sandbox, "owned", "transaction", "ledger.json"));

            Assert.Equal(1, result.ExitCode);
            using var json = JsonDocument.Parse(result.Output);
            Assert.True(json.RootElement.GetProperty("readOnly").GetBoolean());
            Assert.False(json.RootElement.GetProperty("mutationAttempted").GetBoolean());
            Assert.Equal("FAILED", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("TRANSACTION_LEDGER_NOT_VERIFIED", json.RootElement.GetProperty("recovery").GetProperty("state").GetString());
            Assert.Equal("NOT_VERIFIED", json.RootElement.GetProperty("transaction").GetProperty("availability").GetString());
            Assert.DoesNotContain("AUTOMATIC_ROLLBACK_VERIFIED", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE_CHILD_OUTPUT_FOR_TEST", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteOwnedDirectory(sandbox);
        }
    }

    [Fact]
    public void DryRunIgnoresUnexpectedPriorMutationEvents()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sandbox = CreateSandbox(repositoryRoot, includeNoOpClientScript: false);
        try
        {
            var result = RunWorkflow(
                sandbox,
                new Dictionary<string, string> { ["ARCGIS_MCP_TEST_LEDGER_MODE"] = "dryrun-stale-events" },
                "-Action", "Install",
                "-DryRun",
                "-PackagePath", Path.Combine(sandbox, "owned", "package.esriAddInX"),
                "-ManifestPath", Path.Combine(sandbox, "owned", "manifest.json"),
                "-InstallRoot", Path.Combine(sandbox, "owned", "install"),
                "-TransactionRoot", Path.Combine(sandbox, "owned", "transaction"),
                "-LedgerPath", Path.Combine(sandbox, "owned", "transaction", "ledger.json"));

            Assert.Equal(0, result.ExitCode);
            using var json = JsonDocument.Parse(result.Output);
            Assert.True(json.RootElement.GetProperty("readOnly").GetBoolean());
            Assert.False(json.RootElement.GetProperty("mutationAttempted").GetBoolean());
            Assert.Contains(
                json.RootElement.GetProperty("completed").EnumerateArray(),
                item => item.GetProperty("step").GetString() == "release:dry-run");
            Assert.DoesNotContain(
                json.RootElement.GetProperty("completed").EnumerateArray(),
                item => item.GetProperty("step").GetString() == "release:install");
        }
        finally
        {
            DeleteOwnedDirectory(sandbox);
        }
    }

    [Fact]
    public void WrapperDoesNotContainDirectMutationOrApplyAllLogic()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "user-workflow.ps1"));

        foreach (var forbidden in new[]
                 {
                     "Set-Content", "WriteAllText", "WriteAllBytes", "Copy-Item", "Remove-Item",
                     "Move-Item", "New-Item", "AtomicWrite", "ApplyAll"
                 })
        {
            Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("-SkipRegistration", script, StringComparison.Ordinal);
        Assert.Contains("release-transaction.ps1", script, StringComparison.Ordinal);
        Assert.Contains("client-config.ps1", script, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "-TestMode", "-InjectFailureAfterInstall", "-CompatibilityFactsPath", "-ForceRestore" })
            Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateSandbox(string repositoryRoot, bool includeNoOpClientScript)
    {
        var sandbox = Path.Combine(
            Path.GetTempPath(),
            "ArcGISProMCP.UserWorkflowTests",
            Guid.NewGuid().ToString("N"));
        var scripts = Path.Combine(sandbox, "scripts");
        var config = Path.Combine(sandbox, "Config");
        Directory.CreateDirectory(scripts);
        Directory.CreateDirectory(config);
        File.Copy(Path.Combine(repositoryRoot, "scripts", "user-workflow.ps1"), Path.Combine(scripts, "user-workflow.ps1"));
        File.Copy(Path.Combine(repositoryRoot, "Config", "client-catalog.json"), Path.Combine(config, "client-catalog.json"));

        var childScript = includeNoOpClientScript ? "exit 0" : FakeChildScript;
        File.WriteAllText(Path.Combine(scripts, "client-config.ps1"), childScript);
        File.WriteAllText(Path.Combine(scripts, "package-addin.ps1"), includeNoOpClientScript ? "exit 0" : FakeChildScript);
        File.WriteAllText(Path.Combine(scripts, "release-transaction.ps1"), includeNoOpClientScript ? "exit 0" : FakeChildScript);
        return sandbox;
    }

    private static ProcessResult RunWorkflow(string sandbox, params string[] arguments)
        => RunWorkflow(sandbox, null, arguments);

    private static ProcessResult RunWorkflow(
        string sandbox,
        IDictionary<string, string>? environment,
        params string[] arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = sandbox,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(Path.Combine(sandbox, "scripts", "user-workflow.ps1"));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (environment is not null)
        {
            foreach (var pair in environment)
                start.Environment[pair.Key] = pair.Value;
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdout, stderr);
        return new ProcessResult(process.ExitCode, stdout.Result + Environment.NewLine + stderr.Result);
    }

    private static string[] SnapshotTree(string root)
        => Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void DeleteOwnedDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ArcGIS-Pro-MCP.sln")) &&
                File.Exists(Path.Combine(directory.FullName, "scripts", "user-workflow.ps1")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private readonly record struct ProcessResult(int ExitCode, string Output);

    private const string FakeChildScript = """
$log = Join-Path $PSScriptRoot 'delegation.log'
$payload = [ordered]@{ script = [IO.Path]::GetFileName($PSCommandPath); args = @($args) } | ConvertTo-Json -Compress
[IO.File]::AppendAllText($log, $payload + [Environment]::NewLine)
$argList = @($args)
$ledgerPath = $null
for ($i = 0; $i -lt $argList.Count - 1; $i++) {
    if ([string]$argList[$i] -eq '-LedgerPath') { $ledgerPath = [string]$argList[$i + 1] }
}
    $scriptName = [IO.Path]::GetFileName($PSCommandPath)
    $mode = [string]$env:ARCGIS_MCP_TEST_LEDGER_MODE
    if ($scriptName -eq 'release-transaction.ps1' -and $ledgerPath -and $mode -ne 'missing') {
    $dryRun = $argList -contains '-DryRun'
    $ledgerAction = 'Install'
    $ledgerDryRun = $dryRun
    $events = @(
        [ordered]@{ name = 'preflight'; state = 'PASS' },
        [ordered]@{ name = 'compatibility-preflight'; state = 'PASS' },
        [ordered]@{ name = 'snapshot'; state = 'PASS' }
    )
    $status = 'PASS'
    if ($dryRun) {
        $events = @(
            [ordered]@{ name = 'preflight'; state = 'PASS' },
            [ordered]@{ name = 'compatibility-preflight'; state = 'PASS' },
            [ordered]@{ name = 'dry-run'; state = 'PASS' }
        )
        if ($mode -eq 'dryrun-stale-events') {
            $events += [ordered]@{ name = 'install'; state = 'PASS'; data = @{ stale = $true } }
        }
    } elseif ($mode -eq 'rollback-success') {
        $events += [ordered]@{ name = 'install'; state = 'FAIL'; data = @{ error = 'PRIVATE_CHILD_ERROR' } }
        $events += [ordered]@{ name = 'rollback'; state = 'PASS'; data = @{ restored = $true } }
        $status = 'FAIL'
    } elseif ($mode -eq 'rollback-failure') {
        $events += [ordered]@{ name = 'install'; state = 'FAIL'; data = @{ error = 'PRIVATE_CHILD_ERROR' } }
        $events += [ordered]@{ name = 'automatic-rollback'; state = 'FAIL'; data = @{ error = 'PRIVATE_ROLLBACK_ERROR' } }
        $status = 'FAIL'
    } else {
        $events += [ordered]@{ name = 'install'; state = 'PASS' }
        $events += [ordered]@{ name = 'install-verify'; state = 'PASS' }
    }
    $ledgerDirectory = Split-Path -Parent $ledgerPath
    New-Item -ItemType Directory -Force -Path $ledgerDirectory | Out-Null
    $transactionLedger = [ordered]@{
        schema = 'arcgis-pro-mcp-release-transaction-v1'
        action = if ($mode -eq 'action-mismatch') { 'Uninstall' } else { $ledgerAction }
        dryRun = if ($mode -eq 'dryrun-mismatch') { $false } else { $ledgerDryRun }
        status = $status
        recoveryAttempted = ($mode -in @('rollback-success', 'rollback-failure'))
        events = @($events)
    }
    [IO.File]::WriteAllText($ledgerPath, ($transactionLedger | ConvertTo-Json -Depth 8))
}
if ($env:ARCGIS_MCP_TEST_FAKE_FAIL -eq '1' -or $mode -in @('rollback-success', 'rollback-failure', 'missing')) {
    Write-Output 'PRIVATE_CHILD_OUTPUT_FOR_TEST'
    exit 23
}
exit 0
""";
}
