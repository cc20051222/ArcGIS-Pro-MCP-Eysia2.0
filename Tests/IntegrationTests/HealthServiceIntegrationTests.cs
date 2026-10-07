using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.IntegrationTests;

/// <summary>健康契约组合测试。只使用注入 facts，不读取 ArcGIS、进程、端口或仓库路径。</summary>
public sealed class HealthServiceIntegrationTests
{
    [Fact]
    public void InjectedFactsComposeIntoAStableSnapshotWithoutOptionalClientPromotion()
    {
        var snapshot = new HealthService().CreateSnapshot(
            new HealthFacts
            {
                ProductIdentity = "1.0.2",
                Server = new TransportHealthFacts(
                    TransportHealthStatus.Running,
                    host: "127.0.0.1",
                    port: 6520,
                    endpoint: "/mcp",
                    listenerFactExplicit: true),
                Compatibility = new HealthComponent("compatibility", HealthComponentStatus.Pass),
                Configuration = new HealthComponent("configuration", HealthComponentStatus.Pass),
                Bridge = new HealthComponent("bridge", HealthComponentStatus.NotChecked, requiredForOverall: false),
                Clients = new[]
                {
                    ClientHealthFact.OptionalNotChecked("claude-desktop")
                }
            },
            DateTimeOffset.UtcNow);

        Assert.Equal(HealthStatus.Running, snapshot.OverallStatus);
        Assert.Equal(HealthComponentStatus.NotTracked, snapshot.ConnectedClients.Status);
        Assert.Null(snapshot.ConnectedClients.Value);
        Assert.Single(snapshot.Clients);
        Assert.Equal(HealthComponentStatus.NotChecked, snapshot.Clients[0].Status);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Clients[0].RecommendedAction));
        Assert.Equal("1.0.2", snapshot.ProductIdentity);
    }

    [Fact]
    public void InsufficientInjectedFactsBecomeUnavailableInsteadOfPass()
    {
        var snapshot = new HealthService().CreateSnapshot(
            new HealthFacts
            {
                Server = TransportHealthFacts.Unknown(),
                Compatibility = HealthComponent.NotChecked("compatibility"),
                Configuration = HealthComponent.NotChecked("configuration")
            },
            DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(snapshot);
        Assert.Equal(HealthStatus.Unavailable, snapshot.OverallStatus);
        Assert.DoesNotContain("Running", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopped", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiredExternalClientNotCheckedRemainsVisibleWithoutChangingServerOverall()
    {
        var snapshot = new HealthService().CreateSnapshot(
            new HealthFacts
            {
                Server = new TransportHealthFacts(
                    TransportHealthStatus.Running,
                    host: "127.0.0.1",
                    port: 6520,
                    endpoint: "/mcp",
                    listenerFactExplicit: true),
                Compatibility = new HealthComponent("compatibility", HealthComponentStatus.Pass),
                Configuration = new HealthComponent("configuration", HealthComponentStatus.Pass),
                Clients = new[]
                {
                    new ClientHealthFact("codex", HealthComponentStatus.NotChecked, optional: false)
                }
            },
            DateTimeOffset.UtcNow);

        Assert.Equal(HealthStatus.Running, snapshot.OverallStatus);
        Assert.Equal(HealthComponentStatus.NotChecked, snapshot.Clients[0].Status);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Clients[0].RecommendedAction));
    }
}
