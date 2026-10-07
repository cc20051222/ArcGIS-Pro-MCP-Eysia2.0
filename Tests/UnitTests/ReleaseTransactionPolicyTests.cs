using System.Diagnostics;

namespace ArcGISProMCP.UnitTests;

public sealed class ReleaseTransactionPolicyTests
{
    [Fact]
    public void PreflightUsesExactAddInIdentityWithoutMutation()
    {
        var root = FindRepositoryRoot();
        var result = RunScript(root, "-Action", "Preflight", "-DryRun");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("{BAA5628C-3C08-4AD5-A6C0-915ADA475709}", result.Output, StringComparison.Ordinal);
        Assert.Contains("officialInstallMechanism", result.Output, StringComparison.Ordinal);
        Assert.Contains("uninstallMechanism", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterAddIn exit code", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRunIsRepeatableAndDoesNotMutateTestRoot()
    {
        var root = FindRepositoryRoot();
        var tempRoot = CreateTempRoot("DryRun");
        try
        {
            var installRoot = Path.Combine(tempRoot, "install");
            var transactionRoot = Path.Combine(tempRoot, "transaction");
            Directory.CreateDirectory(installRoot);
            var first = RunScript(root, "-Action", "Install", "-DryRun", "-TestMode", "-InstallRoot", installRoot, "-TransactionRoot", transactionRoot);
            var second = RunScript(root, "-Action", "Install", "-DryRun", "-TestMode", "-InstallRoot", installRoot, "-TransactionRoot", transactionRoot);

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(0, second.ExitCode);
            Assert.Contains("DryRun: PASS", first.Output, StringComparison.Ordinal);
            Assert.Contains("DryRun: PASS", second.Output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(installRoot, "{BAA5628C-3C08-4AD5-A6C0-915ADA475709}")));
        }
        finally { DeleteTempRoot(tempRoot); }
    }

    [Fact]
    public void RunningArcGisProGuardRefusesWithoutMutation()
    {
        var root = FindRepositoryRoot();
        var tempRoot = CreateTempRoot("RunningGuard");
        try
        {
            var installRoot = Path.Combine(tempRoot, "install");
            Directory.CreateDirectory(installRoot);
            var result = RunScript(root, "-Action", "Install", "-DryRun", "-TestMode", "-InstallRoot", installRoot, "-ArcGISProProcessName", "powershell");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("ARCGIS_PRO_RUNNING", result.Output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(installRoot, "{BAA5628C-3C08-4AD5-A6C0-915ADA475709}")));
        }
        finally { DeleteTempRoot(tempRoot); }
    }

    [Fact]
    public void ManifestMismatchFailsClosedBeforeMutation()
    {
        var root = FindRepositoryRoot();
        var tempRoot = CreateTempRoot("ManifestMismatch");
        try
        {
            var installRoot = Path.Combine(tempRoot, "install");
            Directory.CreateDirectory(installRoot);
            var manifestPath = Path.Combine(tempRoot, "tampered.release-manifest.json");
            var sourceManifest = File.ReadAllText(Path.Combine(root, "Source", "ArcGISProMCP.Compatibility", "bin", "x64", "Debug", "net6.0-windows", "ArcGISProMCP.Compatibility.release-manifest.json"));
            File.WriteAllText(manifestPath, sourceManifest.Replace("{BAA5628C-3C08-4AD5-A6C0-915ADA475709}", "{00000000-0000-0000-0000-000000000000}", StringComparison.Ordinal));

            var result = RunScript(root, "-Action", "Install", "-DryRun", "-TestMode", "-InstallRoot", installRoot, "-ManifestPath", manifestPath);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("MANIFEST_ID_MISMATCH", result.Output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(installRoot, "{BAA5628C-3C08-4AD5-A6C0-915ADA475709}")));
        }
        finally { DeleteTempRoot(tempRoot); }
    }

    [Fact]
    public void RuntimeArtifactManifestMismatchFailsClosedBeforeMutation()
    {
        var root = FindRepositoryRoot();
        var tempRoot = CreateTempRoot("RuntimeArtifactMismatch");
        try
        {
            var installRoot = Path.Combine(tempRoot, "install");
            Directory.CreateDirectory(installRoot);
            var sourcePackage = Path.Combine(
                root,
                "Source",
                "ArcGISProMCP.Compatibility",
                "bin",
                "x64",
                "Debug",
                "net6.0-windows",
                "ArcGISProMCP.Compatibility.esriAddInX");
            var packagePath = Path.Combine(tempRoot, Path.GetFileName(sourcePackage));
            File.Copy(sourcePackage, packagePath);
            var sourceManifest = Path.ChangeExtension(sourcePackage, ".release-manifest.json");
            var manifestPath = Path.Combine(tempRoot, "tampered.release-manifest.json");
            var manifestText = File.ReadAllText(sourceManifest);
            var manifest = System.Text.Json.JsonDocument.Parse(manifestText);
            var originalHash = manifest.RootElement
                .GetProperty("runtimeArtifacts")[0]
                .GetProperty("sha256")
                .GetString()!;
            File.WriteAllText(
                manifestPath,
                manifestText.Replace(originalHash, new string('0', originalHash.Length), StringComparison.Ordinal));

            var result = RunScript(
                root,
                "-Action",
                "Install",
                "-DryRun",
                "-TestMode",
                "-InstallRoot",
                installRoot,
                "-PackagePath",
                packagePath,
                "-ManifestPath",
                manifestPath);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("MANIFEST_RUNTIME_ARTIFACT_MISMATCH", result.Output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(installRoot, "{BAA5628C-3C08-4AD5-A6C0-915ADA475709}")));
        }
        finally { DeleteTempRoot(tempRoot); }
    }

    [Fact]
    public void TestAcceptanceAndInjectedFailureRestoreExactEmptyBaseline()
    {
        var root = FindRepositoryRoot();
        var successRoot = CreateTempRoot("Acceptance");
        var failureRoot = CreateTempRoot("InjectedFailure");
        try
        {
            var successInstall = Path.Combine(successRoot, "install");
            Directory.CreateDirectory(successInstall);
            var success = RunScript(root, "-Action", "Acceptance", "-TestMode", "-InstallRoot", successInstall, "-TransactionRoot", Path.Combine(successRoot, "transaction"));
            Assert.True(success.ExitCode == 0, success.Output);
            Assert.Contains("Acceptance : PASS", success.Output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(successInstall, "{BAA5628C-3C08-4AD5-A6C0-915ADA475709}")));

            var failureInstall = Path.Combine(failureRoot, "install");
            Directory.CreateDirectory(failureInstall);
            var failure = RunScript(root, "-Action", "Acceptance", "-TestMode", "-InjectFailureAfterInstall", "-InstallRoot", failureInstall, "-TransactionRoot", Path.Combine(failureRoot, "transaction"));
            Assert.NotEqual(0, failure.ExitCode);
            Assert.Contains("TEST_INJECTED_FAILURE_AFTER_INSTALL", failure.Output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(failureInstall, "{BAA5628C-3C08-4AD5-A6C0-915ADA475709}")));
        }
        finally
        {
            DeleteTempRoot(successRoot);
            DeleteTempRoot(failureRoot);
        }
    }

    [Fact]
    public void ExplicitInstallUninstallRollbackActionsAreIdempotentInTestMode()
    {
        var root = FindRepositoryRoot();
        var tempRoot = CreateTempRoot("ExplicitActions");
        try
        {
            var installRoot = Path.Combine(tempRoot, "install");
            var transactionRoot = Path.Combine(tempRoot, "transaction");
            Directory.CreateDirectory(installRoot);
            var baselineTarget = Path.Combine(installRoot, "{BAA5628C-3C08-4AD5-A6C0-915ADA475709}");
            Directory.CreateDirectory(baselineTarget);
            File.Copy(Path.Combine(root, "Source", "ArcGISProMCP.Compatibility", "bin", "x64", "Debug", "net6.0-windows", "ArcGISProMCP.Compatibility.esriAddInX"), Path.Combine(baselineTarget, "ArcGISProMCP.Compatibility.esriAddInX"));

            var install1 = RunScript(root, "-Action", "Install", "-TestMode", "-InstallRoot", installRoot, "-TransactionRoot", transactionRoot);
            var install2 = RunScript(root, "-Action", "Install", "-TestMode", "-InstallRoot", installRoot, "-TransactionRoot", transactionRoot);
            var uninstall = RunScript(root, "-Action", "Uninstall", "-TestMode", "-InstallRoot", installRoot, "-TransactionRoot", transactionRoot);
            var uninstallAgain = RunScript(root, "-Action", "Uninstall", "-TestMode", "-InstallRoot", installRoot, "-TransactionRoot", transactionRoot);
            var rollback = RunScript(root, "-Action", "Rollback", "-TestMode", "-InstallRoot", installRoot, "-TransactionRoot", transactionRoot);
            var rollbackAgain = RunScript(root, "-Action", "Rollback", "-TestMode", "-InstallRoot", installRoot, "-TransactionRoot", transactionRoot);

            Assert.True(install1.ExitCode == 0, install1.Output);
            Assert.True(install2.ExitCode == 0, install2.Output);
            Assert.True(uninstall.ExitCode == 0, uninstall.Output);
            Assert.True(uninstallAgain.ExitCode == 0, uninstallAgain.Output);
            Assert.True(rollback.ExitCode == 0, rollback.Output);
            Assert.True(rollbackAgain.ExitCode == 0, rollbackAgain.Output);
            Assert.True(File.Exists(Path.Combine(installRoot, "{BAA5628C-3C08-4AD5-A6C0-915ADA475709}", "ArcGISProMCP.Compatibility.esriAddInX")));
        }
        finally { DeleteTempRoot(tempRoot); }
    }

    private static (int ExitCode, string Output) RunScript(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(Path.Combine(root, "scripts", "release-transaction.ps1"));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdout, stderr);
        return (process.ExitCode, stdout.Result + Environment.NewLine + stderr.Result);
    }

    private static string CreateTempRoot(string suffix)
    {
        var path = Path.Combine(Path.GetTempPath(), "ArcGISProMCP.Phase72.Tests", suffix + "." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempRoot(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                File.Exists(Path.Combine(directory.FullName, "scripts", "release-transaction.ps1")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
