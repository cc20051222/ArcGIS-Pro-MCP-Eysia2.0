using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.UnitTests;

public sealed class HealthStatusPolicyTests
{
    [Fact]
    public void IncompatiblePrecedesAllLowerPriorityStates()
    {
        var facts = Facts(
            server: TransportHealthStatus.Running,
            compatibility: HealthComponentStatus.Incompatible,
            configuration: HealthComponentStatus.MalformedConfiguration,
            bridge: HealthComponentStatus.Failure);

        Assert.Equal(HealthStatus.Incompatible, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void MalformedConfigurationPrecedesBridgeFailure()
    {
        var facts = Facts(
            configuration: HealthComponentStatus.MalformedConfiguration,
            bridge: HealthComponentStatus.Failure);

        Assert.Equal(HealthStatus.MalformedConfiguration, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void BridgeFailureIsDistinctFromUnavailable()
    {
        var facts = Facts(bridge: HealthComponentStatus.Failure);

        Assert.Equal(HealthStatus.BridgeFailure, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void UnknownTransportCannotBeReportedAsRunningOrStopped()
    {
        var facts = Facts(server: TransportHealthStatus.Unknown);

        Assert.Equal(HealthStatus.Unavailable, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void RunningRequiresAnExplicitTransportFact()
    {
        var facts = Facts(server: TransportHealthStatus.Running);

        Assert.Equal(HealthStatus.Running, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void StoppedIsReportedOnlyWhenTransportExplicitlySaysStopped()
    {
        var facts = Facts(server: TransportHealthStatus.Stopped);

        Assert.Equal(HealthStatus.Stopped, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void BridgeStoppedDoesNotBecomeBridgeFailure()
    {
        var facts = Facts(bridge: HealthComponentStatus.Stopped);

        Assert.Equal(HealthStatus.Running, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void BridgeNotCheckedDoesNotBecomeBridgeFailure()
    {
        var facts = Facts(bridge: HealthComponentStatus.NotChecked);

        Assert.Equal(HealthStatus.Running, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void RequiredNotCheckedFactDoesNotMakeRunningServerUnavailable()
    {
        var facts = Facts(configuration: HealthComponentStatus.NotChecked);

        Assert.Equal(HealthStatus.Running, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void RequiredDegradedFactProducesUnavailable()
    {
        var facts = Facts(configuration: HealthComponentStatus.Degraded);

        Assert.Equal(HealthStatus.Unavailable, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void ExplicitBridgeUnavailableIsNotReportedAsHealthy()
    {
        var facts = Facts(bridge: HealthComponentStatus.Unavailable);

        Assert.Equal(HealthStatus.Unavailable, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void OptionalClientNotCheckedDoesNotChangeOverallStatus()
    {
        var facts = Facts();
        facts = facts with
        {
            Clients = new[] { ClientHealthFact.OptionalNotChecked("claude-desktop") }
        };

        Assert.Equal(HealthStatus.Running, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void RequiredClientMalformedConfigurationUsesConfigurationState()
    {
        var facts = Facts() with
        {
            Clients = new[]
            {
                new ClientHealthFact(
                    "codex",
                    HealthComponentStatus.MalformedConfiguration,
                    optional: false)
            }
        };

        Assert.Equal(HealthStatus.MalformedConfiguration, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void OptionalClientMalformedConfigurationDoesNotChangeOverallStatus()
    {
        var facts = Facts() with
        {
            Clients = new[]
            {
                new ClientHealthFact(
                    "claude-desktop",
                    HealthComponentStatus.MalformedConfiguration,
                    optional: true)
            }
        };

        Assert.Equal(HealthStatus.Running, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void RequiredClientNotCheckedDoesNotMakeRunningServerUnavailable()
    {
        var facts = Facts() with
        {
            Clients = new[]
            {
                new ClientHealthFact("codex", HealthComponentStatus.NotChecked, optional: false)
            }
        };

        Assert.Equal(HealthStatus.Running, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void RequiredClientNotCheckedDoesNotMakeStoppedServerUnavailable()
    {
        var facts = Facts(server: TransportHealthStatus.Stopped) with
        {
            Clients = new[]
            {
                new ClientHealthFact("codex", HealthComponentStatus.NotChecked, optional: false)
            }
        };

        Assert.Equal(HealthStatus.Stopped, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void LoggingDegradedDoesNotHideHealthyTransport()
    {
        var facts = Facts();
        facts = facts with
        {
            Logging = new HealthComponent(
                "logging",
                HealthComponentStatus.Degraded,
                requiredForOverall: false)
        };

        Assert.Equal(HealthStatus.Running, HealthStatusPolicy.DetermineOverall(facts));
    }

    [Fact]
    public void RecommendedActionIsStableForEachUserVisibleState()
    {
        foreach (var status in Enum.GetValues<HealthStatus>())
        {
            Assert.False(string.IsNullOrWhiteSpace(HealthStatusPolicy.RecommendedAction(status)));
        }
    }

    private static HealthFacts Facts(
        TransportHealthStatus server = TransportHealthStatus.Running,
        HealthComponentStatus compatibility = HealthComponentStatus.Pass,
        HealthComponentStatus configuration = HealthComponentStatus.Pass,
        HealthComponentStatus bridge = HealthComponentStatus.Stopped)
        => new()
        {
            Server = new TransportHealthFacts(
                server,
                host: "127.0.0.1",
                port: 6520,
                endpoint: "/mcp",
                listenerFactExplicit: server != TransportHealthStatus.Unknown),
            Compatibility = new HealthComponent("compatibility", compatibility),
            Configuration = new HealthComponent("configuration", configuration),
            ArcGISHost = new HealthComponent("arcgisHost", HealthComponentStatus.Pass, requiredForOverall: false),
            Bridge = new HealthComponent("bridge", bridge, requiredForOverall: false),
            Logging = new HealthComponent("logging", HealthComponentStatus.Pass, requiredForOverall: false)
        };
}
