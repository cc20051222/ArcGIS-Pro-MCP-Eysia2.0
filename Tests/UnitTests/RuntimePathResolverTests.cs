using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Runtime;

namespace ArcGISProMCP.UnitTests;

public sealed class RuntimePathResolverTests
{
    [Fact]
    public void ResolvesPackagedBridgeActiveProPythonAndLazyPerUserRuntimeWithoutCreatingDirectories()
    {
        using var fixture = TestFixture.Create();

        var resolution = RuntimePathResolver.Resolve(fixture.Inputs);

        Assert.True(resolution.Succeeded, resolution.ErrorCode);
        var paths = resolution.Paths!;
        Assert.Equal(
            Path.Combine(fixture.AssemblyDirectory, "PythonBridge", "bridge_runner.py"),
            paths.PythonBridgeScript);
        Assert.Equal(Path.GetDirectoryName(paths.PythonBridgeScript), paths.PythonWorkingDirectory);
        Assert.Equal(
            Path.Combine(fixture.ProBin, "Python", "envs", "arcgispro-py3", "python.exe"),
            paths.PythonExecutable);
        Assert.Equal(
            Path.Combine(fixture.LocalApplicationData, "ArcGISProMCP", "1.0.2", "runtime"),
            paths.RuntimeRoot);
        var productRoot = Path.Combine(fixture.LocalApplicationData, "ArcGISProMCP");
        var releaseRoot = Path.Combine(productRoot, "1.0.2");
        Assert.Equal("runtime", Path.GetRelativePath(releaseRoot, paths.RuntimeRoot));
        Assert.DoesNotContain(
            "..",
            Path.GetRelativePath(productRoot, paths.RuntimeRoot),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "..",
            Path.GetRelativePath(releaseRoot, paths.RuntimeRoot),
            StringComparison.Ordinal);
        Assert.Equal(Path.Combine(paths.RuntimeRoot, "managed-logs"), paths.ManagedLogDirectory);
        Assert.False(Directory.Exists(paths.RuntimeRoot));
        Assert.False(Directory.Exists(paths.ManagedLogDirectory));
        Assert.StartsWith(
            Path.GetFullPath(fixture.LocalApplicationData).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            Path.GetFullPath(paths.RuntimeRoot) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingBridgeFailsClosedWithFixedCode()
    {
        using var fixture = TestFixture.Create();
        File.Delete(fixture.BridgeScript);

        var resolution = RuntimePathResolver.Resolve(fixture.Inputs);

        Assert.False(resolution.Succeeded);
        Assert.Equal(RuntimePathErrorCodes.BridgeScriptMissing, resolution.ErrorCode);
        Assert.Null(resolution.Paths);
    }

    [Fact]
    public void MissingActiveProPythonFailsClosedWithFixedCode()
    {
        using var fixture = TestFixture.Create();
        File.Delete(fixture.PythonExecutable);

        var resolution = RuntimePathResolver.Resolve(fixture.Inputs);

        Assert.False(resolution.Succeeded);
        Assert.Equal(RuntimePathErrorCodes.PythonExecutableMissing, resolution.ErrorCode);
    }

    [Fact]
    public void NonArcGISProActiveProcessFailsClosedWithoutSearchingAnotherLocation()
    {
        using var fixture = TestFixture.Create();
        var otherProcess = Path.Combine(fixture.ProBin, "OtherHost.exe");
        File.WriteAllText(otherProcess, "not ArcGIS Pro");
        var inputs = fixture.Inputs with { ActiveArcGISProExecutablePath = otherProcess };

        var resolution = RuntimePathResolver.Resolve(inputs);

        Assert.False(resolution.Succeeded);
        Assert.Equal(RuntimePathErrorCodes.ArcGISProExecutableInvalid, resolution.ErrorCode);
    }

    [DirectoryReparseFact]
    public void ReparseBridgeDirectoryFailsClosedAndCannotEscapePackageRoot()
    {
        using var fixture = TestFixture.Create();
        var outside = Path.Combine(fixture.Root, "outside-bridge");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "bridge_runner.py"), "outside");
        Directory.Delete(Path.Combine(fixture.AssemblyDirectory, "PythonBridge"), recursive: true);
        Directory.CreateSymbolicLink(
            Path.Combine(fixture.AssemblyDirectory, "PythonBridge"),
            outside);

        var resolution = RuntimePathResolver.Resolve(fixture.Inputs);

        Assert.False(resolution.Succeeded);
        Assert.Equal(RuntimePathErrorCodes.BridgeScriptInvalid, resolution.ErrorCode);
    }

    [Fact]
    public void InvalidLocalApplicationDataFailsClosedWithoutFallbackToRepositoryOrEnvironment()
    {
        using var fixture = TestFixture.Create();
        var inputs = fixture.Inputs with
        {
            LocalApplicationDataDirectory = Path.Combine(fixture.Root, "missing-local-appdata")
        };

        var resolution = RuntimePathResolver.Resolve(inputs);

        Assert.False(resolution.Succeeded);
        Assert.Equal(RuntimePathErrorCodes.LocalApplicationDataInvalid, resolution.ErrorCode);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("1..2")]
    [InlineData(".1.2")]
    [InlineData("1.2.")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1/2/3")]
    [InlineData("1\\2\\3")]
    [InlineData("+1.2.3")]
    [InlineData(" 1.2.3")]
    [InlineData("1.2.3 ")]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.03")]
    [InlineData("9999999999.0.0")]
    [InlineData("1.9999999999.0")]
    [InlineData("1.2.9999999999")]
    public void AdversarialReleaseTokensFailClosedBeforePathConstruction(string releaseVersion)
    {
        using var fixture = TestFixture.Create();
        var resolution = RuntimePathResolver.Resolve(fixture.Inputs with { ReleaseVersion = releaseVersion });

        Assert.False(resolution.Succeeded);
        Assert.Equal(RuntimePathErrorCodes.ReleaseVersionInvalid, resolution.ErrorCode);
        Assert.Null(resolution.Paths);
        Assert.False(Directory.Exists(Path.Combine(fixture.LocalApplicationData, "ArcGISProMCP")));
    }

    [Fact]
    public void ProductionCompositionContainsNoDeveloperRepositoryOrFixedPythonPath()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(
            Path.Combine(root, "Source", "ArcGISProMCP.Compatibility", "Composition.cs"));

        Assert.DoesNotContain(@"D:\ArcGIS-Pro-MCP", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Program Files\ArcGIS", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\".runtime\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RuntimePathResolver", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultSharedSettingsDoNotCarryMachineSpecificRuntimePaths()
    {
        var settings = new MCPSettings();

        Assert.Empty(settings.PythonExecutable);
        Assert.Empty(settings.PythonBridgeScript);
        Assert.Empty(settings.RuntimeRoot);
        Assert.Empty(settings.ManagedLogDirectory);
        Assert.Null(settings.RuntimePathErrorCode);
    }

    [Fact]
    public void RuntimePathErrorCodesAreFixedSafeTokens()
    {
        Assert.True(RuntimePathErrorCodes.IsSafe(RuntimePathErrorCodes.BridgeScriptMissing));
        Assert.True(RuntimePathErrorCodes.IsSafe(RuntimePathErrorCodes.PythonExecutableMissing));
        Assert.False(RuntimePathErrorCodes.IsSafe(@"C:\Users\Alice\private.txt"));
    }

    private static string FindRepositoryRoot()
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

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class TestFixture : IDisposable
    {
        private TestFixture(string root)
        {
            Root = root;
            AssemblyDirectory = Path.Combine(root, "owned-package", "Install");
            BridgeScript = Path.Combine(AssemblyDirectory, "PythonBridge", "bridge_runner.py");
            ProBin = Path.Combine(root, "ArcGISPro", "bin");
            PythonExecutable = Path.Combine(ProBin, "Python", "envs", "arcgispro-py3", "python.exe");
            LocalApplicationData = Path.Combine(root, "LocalApplicationData");

            Directory.CreateDirectory(Path.GetDirectoryName(BridgeScript)!);
            Directory.CreateDirectory(Path.GetDirectoryName(PythonExecutable)!);
            Directory.CreateDirectory(ProBin);
            Directory.CreateDirectory(LocalApplicationData);
            File.WriteAllText(BridgeScript, "owned bridge");
            File.WriteAllText(Path.Combine(AssemblyDirectory, "ArcGISProMCP.Compatibility.dll"), "assembly");
            File.WriteAllText(Path.Combine(ProBin, "ArcGISPro.exe"), "ArcGIS Pro");
            File.WriteAllText(PythonExecutable, "ArcGIS Python");

            Inputs = new RuntimePathInputs
            {
                CompatibilityAssemblyDirectory = AssemblyDirectory,
                ActiveArcGISProExecutablePath = Path.Combine(ProBin, "ArcGISPro.exe"),
                LocalApplicationDataDirectory = LocalApplicationData,
                ReleaseVersion = "1.0.2"
            };
        }

        public string Root { get; }

        public string AssemblyDirectory { get; }

        public string BridgeScript { get; }

        public string ProBin { get; }

        public string PythonExecutable { get; }

        public string LocalApplicationData { get; }

        public RuntimePathInputs Inputs { get; }

        public static TestFixture Create()
            => new(Directory.CreateTempSubdirectory("arcgispro-mcp-runtime-paths-").FullName);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Test-owned temporary data only.
            }
        }
    }

    private sealed class DirectoryReparseFactAttribute : FactAttribute
    {
        public DirectoryReparseFactAttribute()
        {
            if (!CanCreateDirectoryReparsePoint(out var reason))
            {
                Skip = reason;
            }
        }

        private static bool CanCreateDirectoryReparsePoint(out string reason)
        {
            var root = Directory.CreateTempSubdirectory("arcgispro-mcp-runtime-reparse-capability-");
            var target = Directory.CreateDirectory(Path.Combine(root.FullName, "target"));
            var link = Path.Combine(root.FullName, "link");
            try
            {
                Directory.CreateSymbolicLink(link, target.FullName);
                reason = string.Empty;
                return (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or PlatformNotSupportedException
                                       or NotSupportedException
                                       or ArgumentException)
            {
                reason = $"Directory reparse-point test skipped: {ex.GetType().Name}";
                return false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(link))
                    {
                        Directory.Delete(link, recursive: false);
                    }
                }
                catch
                {
                }

                try
                {
                    root.Delete(recursive: true);
                }
                catch
                {
                }
            }
        }
    }
}
