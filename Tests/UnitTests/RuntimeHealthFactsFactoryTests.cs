using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.PythonBridge;
using ArcGISProMCP.Core.Runtime;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.UnitTests;

public sealed class RuntimeHealthFactsFactoryTests
{
    [Fact]
    public void ManagedRunningFactsKeepExternalClientsNotCheckedAndNonBlocking()
    {
        var facts = RuntimeHealthFactsFactory.Create(
            new MCPSettings(),
            new RuntimeHealthInputs
            {
                ServerRunning = true,
                HostContextLoaded = true,
                BridgeState = null,
                LoggingHealth = null
            });
        var snapshot = new HealthService().CreateSnapshot(facts, DateTimeOffset.UtcNow);

        Assert.Equal(TransportHealthStatus.Running, facts.Server.Status);
        Assert.Equal(HealthComponentStatus.Pass, facts.Configuration.Status);
        Assert.Equal(HealthComponentStatus.DeclaredOnly, facts.Compatibility.Status);
        Assert.Equal(HealthComponentStatus.Pass, facts.ArcGISHost.Status);
        Assert.Equal(HealthComponentStatus.NotChecked, facts.Bridge.Status);
        Assert.Equal(HealthComponentStatus.NotChecked, facts.Logging.Status);
        Assert.Equal(HealthStatus.Running, snapshot.OverallStatus);
        Assert.Equal(4, snapshot.Clients.Count);
        Assert.All(snapshot.Clients, client => Assert.Equal(HealthComponentStatus.NotChecked, client.Status));
        Assert.Contains("Declared target: Pro 3.0", facts.Compatibility.Summary);
        Assert.Contains("live compatibility not checked", facts.Compatibility.Summary);
        Assert.Contains("live map/project state is not probed", facts.ArcGISHost.Summary);
    }

    [Fact]
    public void InvalidManagedSettingsBecomeMalformedWithoutEchoingRawValues()
    {
        var settings = new MCPSettings
        {
            Host = @"C:\Users\Alice\private.json",
            Port = 6500,
            Endpoint = "/private-payload"
        };
        var snapshot = new HealthService().CreateSnapshot(
            RuntimeHealthFactsFactory.Create(
                settings,
                new RuntimeHealthInputs { ServerRunning = false }),
            DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(snapshot);

        Assert.Equal(HealthStatus.MalformedConfiguration, snapshot.OverallStatus);
        Assert.Equal(HealthComponentStatus.MalformedConfiguration, snapshot.Configuration.Status);
        Assert.Null(snapshot.Server.Host);
        Assert.Null(snapshot.Server.Endpoint);
        Assert.DoesNotContain("Alice", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private.json", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-payload", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimePathFailureIsReportedAsAFixedSafeBridgeCodeWithoutStartingTheBridge()
    {
        var snapshot = new HealthService().CreateSnapshot(
            RuntimeHealthFactsFactory.Create(
                new MCPSettings
                {
                    RuntimePathErrorCode = RuntimePathErrorCodes.BridgeScriptMissing
                },
                new RuntimeHealthInputs
                {
                    ServerRunning = true,
                    BridgeState = PythonBridgeState.Stopped,
                    BridgeErrorCode = RuntimePathErrorCodes.BridgeScriptMissing
                }),
            DateTimeOffset.UtcNow);

        Assert.Equal(HealthComponentStatus.Failure, snapshot.Bridge.Status);
        Assert.Equal(RuntimePathErrorCodes.BridgeScriptMissing, snapshot.Bridge.ErrorCode);
        Assert.Equal(HealthStatus.BridgeFailure, snapshot.OverallStatus);
    }

    [Fact]
    public void IncompleteManagedCompatibilityFactsRemainNotCheckedWithoutBlockingRunning()
    {
        var snapshot = new HealthService().CreateSnapshot(
            RuntimeHealthFactsFactory.Create(
                new MCPSettings(),
                new RuntimeHealthInputs
                {
                    ServerRunning = true,
                    CompatibilityFacts = new ManagedCompatibilityFacts(null, "3.5.0", "net6.0")
                }),
            DateTimeOffset.UtcNow);

        Assert.Equal(HealthComponentStatus.NotChecked, snapshot.Compatibility.Status);
        Assert.Equal(HealthStatus.Running, snapshot.OverallStatus);
        Assert.Contains("not checked", snapshot.Compatibility.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LiveManagedCompatibilityMatchIsTheOnlyPathToCompatibilityPass()
    {
        var snapshot = new HealthService().CreateSnapshot(
            RuntimeHealthFactsFactory.Create(
                new MCPSettings(),
                new RuntimeHealthInputs
                {
                    ServerRunning = true,
                    CompatibilityFacts = new ManagedCompatibilityFacts(
                        "1.0.2",
                        "3.5.0",
                        "net6.0",
                        isLiveVerified: true,
                        liveCompatibilityMatches: true)
                }),
            DateTimeOffset.UtcNow);

        Assert.Equal(HealthComponentStatus.Pass, snapshot.Compatibility.Status);
        Assert.Equal(HealthStatus.Running, snapshot.OverallStatus);
    }

    [Fact]
    public void LiveManagedCompatibilityMismatchIsExplicitlyIncompatible()
    {
        var snapshot = new HealthService().CreateSnapshot(
            RuntimeHealthFactsFactory.Create(
                new MCPSettings(),
                new RuntimeHealthInputs
                {
                    ServerRunning = true,
                    CompatibilityFacts = new ManagedCompatibilityFacts(
                        "1.0.2",
                        "3.5.0",
                        "net6.0",
                        isLiveVerified: true,
                        liveCompatibilityMatches: false)
                }),
            DateTimeOffset.UtcNow);

        Assert.Equal(HealthComponentStatus.Incompatible, snapshot.Compatibility.Status);
        Assert.Equal(HealthStatus.Incompatible, snapshot.OverallStatus);
    }

    [Fact]
    public void MissingHostContextRemainsNonBlockingAndExplicitlyUnverified()
    {
        var facts = RuntimeHealthFactsFactory.Create(
            new MCPSettings(),
            new RuntimeHealthInputs { ServerRunning = true });
        var snapshot = new HealthService().CreateSnapshot(facts, DateTimeOffset.UtcNow);

        Assert.Equal(HealthComponentStatus.NotChecked, facts.ArcGISHost.Status);
        Assert.Equal(HealthStatus.Running, snapshot.OverallStatus);
        Assert.Contains("not probed", facts.ArcGISHost.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PythonBridgeState.Stopped, HealthComponentStatus.Stopped, HealthStatus.Running)]
    [InlineData(PythonBridgeState.Running, HealthComponentStatus.Running, HealthStatus.Running)]
    [InlineData(PythonBridgeState.Starting, HealthComponentStatus.Degraded, HealthStatus.Unavailable)]
    [InlineData(PythonBridgeState.Stopping, HealthComponentStatus.Degraded, HealthStatus.Unavailable)]
    [InlineData(PythonBridgeState.Faulted, HealthComponentStatus.Failure, HealthStatus.BridgeFailure)]
    [InlineData(PythonBridgeState.Disposed, HealthComponentStatus.Stopped, HealthStatus.Running)]
    public void BridgeStatesMapToSafeComponentAndOverallStates(
        PythonBridgeState bridgeState,
        HealthComponentStatus expectedComponent,
        HealthStatus expectedOverall)
    {
        var snapshot = new HealthService().CreateSnapshot(
            RuntimeHealthFactsFactory.Create(
                new MCPSettings(),
                new RuntimeHealthInputs
                {
                    ServerRunning = true,
                    BridgeState = bridgeState,
                    BridgeErrorCode = "not-a-safe-code"
                }),
            DateTimeOffset.UtcNow);

        Assert.Equal(expectedComponent, snapshot.Bridge.Status);
        Assert.Equal(expectedOverall, snapshot.OverallStatus);
        Assert.DoesNotContain("not-a-safe-code", JsonSerializer.Serialize(snapshot), StringComparison.Ordinal);
    }

    [Fact]
    public void LoggingDegradedIsVisibleButDoesNotBlockRunningTransport()
    {
        var snapshot = new HealthService().CreateSnapshot(
            RuntimeHealthFactsFactory.Create(
                new MCPSettings(),
                new RuntimeHealthInputs
                {
                    ServerRunning = true,
                    LoggingHealth = new StructuredLogHealth
                    {
                        Status = StructuredLogHealthStatus.Degraded,
                        LastFailureCode = "LOG_WRITE_FAILED"
                    }
                }),
            DateTimeOffset.UtcNow);

        Assert.Equal(HealthComponentStatus.Degraded, snapshot.Logging.Status);
        Assert.Equal(HealthStatus.Running, snapshot.OverallStatus);
        Assert.Equal("LOGGING_DEGRADED", snapshot.Logging.ErrorCode);
    }

    [Fact]
    public void InsufficientServerFactBecomesUnavailableWithoutRawFailureText()
    {
        var snapshot = new HealthService().CreateSnapshot(
            RuntimeHealthFactsFactory.Create(
                new MCPSettings(),
                new RuntimeHealthInputs { ServerRunning = null }),
            DateTimeOffset.UtcNow);

        Assert.Equal(TransportHealthStatus.Unknown, snapshot.Server.Status);
        Assert.Equal(HealthStatus.Unavailable, snapshot.OverallStatus);
        Assert.DoesNotContain("Exception", JsonSerializer.Serialize(snapshot), StringComparison.OrdinalIgnoreCase);
    }
}
