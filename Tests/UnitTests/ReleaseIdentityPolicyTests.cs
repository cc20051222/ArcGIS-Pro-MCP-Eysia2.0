using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace ArcGISProMCP.UnitTests;

public sealed class ReleaseIdentityPolicyTests
{
    [Fact]
    public void CentralVersionPolicyMatchesConfigDaml()
    {
        var root = FindRepositoryRoot();
        var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        var propertyGroup = props.Root!.Elements().Single(e => e.Name.LocalName == "PropertyGroup");
        var config = XDocument.Load(Path.Combine(root, "Source", "ArcGISProMCP.Compatibility", "Config.daml"));
        var addIn = config.Root!.Elements().Single(e => e.Name.LocalName == "AddInInfo");

        Assert.Equal("1.0.2", propertyGroup.Element("Version")!.Value);
        Assert.Equal("1.0.0.0", propertyGroup.Element("AssemblyVersion")!.Value);
        Assert.Equal("1.0.2.0", propertyGroup.Element("FileVersion")!.Value);
        Assert.Equal("1.0.2", propertyGroup.Element("InformationalVersion")!.Value);
        Assert.Equal("1.0.2", (string?)addIn.Attribute("version"));
        Assert.Equal("3.0", (string?)addIn.Attribute("desktopVersion"));
    }

    [Fact]
    public void PackageScriptExposesExplicitPackageOnlyMode()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "package-addin.ps1"));

        Assert.Contains("[string]$ProjectDir = ''", script, StringComparison.Ordinal);
        Assert.Contains("Join-Path $repoRoot 'Source\\ArcGISProMCP.Compatibility'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("D:\\ArcGIS-Pro-MCP\\Source\\ArcGISProMCP.Compatibility", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[switch]$SkipRegistration", script, StringComparison.Ordinal);
        Assert.Contains("if ($SkipRegistration)", script, StringComparison.Ordinal);
        Assert.Contains("Registration: SKIPPED (-SkipRegistration)", script, StringComparison.Ordinal);
        Assert.Contains("arcgis-pro-mcp-release-manifest-v1", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageOnlyRunProducesManifestWithoutRegistering()
    {
        var root = FindRepositoryRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), "ArcGISProMCP.ReleaseIdentityTests", Guid.NewGuid().ToString("N"));
        try
        {
            var projectDir = Path.Combine(tempRoot, "Compatibility");
            var outputDir = Path.Combine(projectDir, "bin", "x64", "Debug", "net6.0-windows");
            Directory.CreateDirectory(outputDir);
            CopyDirectory(Path.Combine(root, "Source", "ArcGISProMCP.Compatibility", "Images"), Path.Combine(projectDir, "Images"));
            File.Copy(Path.Combine(root, "Source", "ArcGISProMCP.Compatibility", "Config.daml"), Path.Combine(projectDir, "Config.daml"));
            CopyBuildOutput(Path.Combine(root, "Source", "ArcGISProMCP.Compatibility", "bin", "x64", "Debug", "net6.0-windows"), outputDir);

            var result = RunPackageScript(root, projectDir, skipRegistration: true);

            Assert.True(result.ExitCode == 0, result.Output);
            Assert.Contains("Registration: SKIPPED (-SkipRegistration)", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("RegisterAddIn exit code", result.Output, StringComparison.Ordinal);

            var packagePath = Path.Combine(outputDir, "ArcGISProMCP.Compatibility.esriAddInX");
            var manifestPath = Path.Combine(outputDir, "ArcGISProMCP.Compatibility.release-manifest.json");
            Assert.True(File.Exists(packagePath));
            Assert.True(File.Exists(manifestPath));
            using var json = JsonDocument.Parse(File.ReadAllText(manifestPath));
            Assert.Equal("arcgis-pro-mcp-release-manifest-v1", json.RootElement.GetProperty("schema").GetString());
            Assert.Equal("1.0.2", json.RootElement.GetProperty("product").GetProperty("releaseVersion").GetString());
            Assert.Equal("3.0", json.RootElement.GetProperty("target").GetProperty("arcgisProDesktopVersion").GetString());
            var assemblies = json.RootElement.GetProperty("firstPartyAssemblies");
            Assert.True(assemblies.GetArrayLength() > 0);
            foreach (var assembly in assemblies.EnumerateArray())
                Assert.Equal("1.0.0.0", assembly.GetProperty("assemblyVersion").GetString());
            var runtimeArtifacts = json.RootElement.GetProperty("runtimeArtifacts");
            var bridgeArtifact = Assert.Single(runtimeArtifacts.EnumerateArray());
            Assert.Equal(
                "Install/PythonBridge/bridge_runner.py",
                bridgeArtifact.GetProperty("logicalPath").GetString());
            Assert.Equal(
                "python-bridge-script",
                bridgeArtifact.GetProperty("kind").GetString());
            using (var zip = ZipFile.OpenRead(packagePath))
            {
                var bridgeEntries = zip.Entries
                    .Where(entry => entry.FullName.Replace('\\', '/') ==
                                    "Install/PythonBridge/bridge_runner.py")
                    .ToArray();
                var bridgeEntry = Assert.Single(bridgeEntries);
                Assert.Equal(
                    bridgeEntry.Length,
                    bridgeArtifact.GetProperty("sizeBytes").GetInt64());
                using var stream = bridgeEntry.Open();
                Assert.Equal(
                    bridgeArtifact.GetProperty("sha256").GetString(),
                    Convert.ToHexString(SHA256.HashData(stream)));
            }
            Assert.Equal(0, json.RootElement.GetProperty("packageContentSummary").GetProperty("nestedAddInCount").GetInt32());
            Assert.DoesNotContain(root, File.ReadAllText(manifestPath), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void VersionMismatchFailsClosedBeforeRegistration()
    {
        var root = FindRepositoryRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), "ArcGISProMCP.ReleaseIdentityTests", Guid.NewGuid().ToString("N"));
        try
        {
            var projectDir = Path.Combine(tempRoot, "Compatibility");
            var outputDir = Path.Combine(projectDir, "bin", "x64", "Debug", "net6.0-windows");
            Directory.CreateDirectory(outputDir);
            CopyDirectory(Path.Combine(root, "Source", "ArcGISProMCP.Compatibility", "Images"), Path.Combine(projectDir, "Images"));
            var config = File.ReadAllText(Path.Combine(root, "Source", "ArcGISProMCP.Compatibility", "Config.daml"))
                .Replace("version=\"1.0.2\"", "version=\"9.9.9\"", StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(projectDir, "Config.daml"), config);
            CopyBuildOutput(Path.Combine(root, "Source", "ArcGISProMCP.Compatibility", "bin", "x64", "Debug", "net6.0-windows"), outputDir);

            var result = RunPackageScript(root, projectDir, skipRegistration: true);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("RELEASE_IDENTITY_MISMATCH", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("RegisterAddIn exit code", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static (int ExitCode, string Output) RunPackageScript(string root, string projectDir, bool skipRegistration)
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
        start.ArgumentList.Add(Path.Combine(root, "scripts", "package-addin.ps1"));
        start.ArgumentList.Add("-ProjectDir");
        start.ArgumentList.Add(projectDir);
        if (skipRegistration) start.ArgumentList.Add("-SkipRegistration");

        using var process = Process.Start(start)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);
        var output = stdoutTask.Result + Environment.NewLine + stderrTask.Result;
        return (process.ExitCode, output);
    }

    private static void CopyBuildOutput(string source, string destination)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".esriAddInX", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("release-manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, Path.Combine(destination, name));
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                File.Exists(Path.Combine(directory.FullName, "scripts", "package-addin.ps1")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
