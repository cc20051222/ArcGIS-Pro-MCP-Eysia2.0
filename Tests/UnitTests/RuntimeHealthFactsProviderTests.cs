using System.Reflection;
using System.Runtime.Loader;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.PythonBridge;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.UnitTests;

public sealed class RuntimeHealthFactsProviderTests
{
    [Fact]
    public void ProviderReadsManagedDelegatesWithoutCreatingRuntimeObjects()
    {
        var serverReads = 0;
        var hostReads = 0;
        var bridgeReads = 0;
        var loggerReads = 0;

        var provider = CreateProvider(
            new MCPSettings(),
            () =>
            {
                serverReads++;
                return true;
            },
            () =>
            {
                hostReads++;
                return true;
            },
            () =>
            {
                bridgeReads++;
                return null;
            },
            () =>
            {
                loggerReads++;
                return null;
            });

        var facts = provider.GetFacts();

        Assert.Equal(1, serverReads);
        Assert.Equal(1, hostReads);
        Assert.Equal(1, bridgeReads);
        Assert.Equal(1, loggerReads);
        Assert.Equal(TransportHealthStatus.Running, facts.Server.Status);
        Assert.Equal(HealthComponentStatus.Pass, facts.ArcGISHost.Status);
        Assert.Equal(HealthComponentStatus.DeclaredOnly, facts.Compatibility.Status);
        Assert.Equal(HealthComponentStatus.NotChecked, facts.Bridge.Status);
        Assert.Equal(HealthComponentStatus.NotChecked, facts.Logging.Status);
    }

    [Fact]
    public void ProviderConvertsDelegateFailuresToSafeUnverifiedFacts()
    {
        var provider = CreateProvider(
            new MCPSettings(),
            () => throw new InvalidOperationException("C:\\private\\payload"),
            () => throw new InvalidOperationException("host secret"),
            () => throw new InvalidOperationException("bridge secret"),
            () => throw new InvalidOperationException("logger secret"));

        var facts = provider.GetFacts();
        var snapshot = new HealthService().CreateSnapshot(facts, DateTimeOffset.UtcNow);
        var serialized = System.Text.Json.JsonSerializer.Serialize(snapshot);

        Assert.Equal(TransportHealthStatus.Unknown, facts.Server.Status);
        Assert.Equal(HealthComponentStatus.NotChecked, facts.ArcGISHost.Status);
        Assert.Equal(HealthStatus.Unavailable, snapshot.OverallStatus);
        Assert.DoesNotContain("private", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static IHealthFactsProvider CreateProvider(
        MCPSettings settings,
        Func<bool?> serverRunning,
        Func<bool?> hostContextLoaded,
        Func<PythonBridgeProcessManager?> bridgeAccessor,
        Func<ILogger?> loggerAccessor)
    {
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(FindCompatibilityAssemblyPath());
        var providerType = assembly.GetType(
            "ArcGISProMCP.Compatibility.Health.RuntimeHealthFactsProvider",
            throwOnError: true)!;

        return (IHealthFactsProvider)Activator.CreateInstance(
            providerType,
            new object?[]
            {
                settings,
                serverRunning,
                hostContextLoaded,
                bridgeAccessor,
                loggerAccessor,
                ManagedCompatibilityFacts.Current
            })!;
    }

    private static string FindCompatibilityAssemblyPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        var binRoot = Path.Combine(repositoryRoot, "Source", "ArcGISProMCP.Compatibility", "bin");
        return Directory
            .EnumerateFiles(binRoot, "ArcGISProMCP.Compatibility.dll", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
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
}
