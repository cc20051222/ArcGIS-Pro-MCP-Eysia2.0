using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.UnitTests;

public sealed class HealthSnapshotTests
{
    [Fact]
    public void SnapshotUsesUtcTimeAndStableClientOrdering()
    {
        var facts = new HealthFacts
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
                ClientHealthFact.OptionalNotChecked("zeta"),
                ClientHealthFact.OptionalNotChecked("alpha")
            }
        };

        var inputTime = new DateTimeOffset(2026, 9, 6, 1, 2, 3, TimeSpan.FromHours(8));
        var snapshot = new HealthService().CreateSnapshot(facts, inputTime);

        Assert.Equal(HealthSnapshot.SchemaName, snapshot.Schema);
        Assert.Equal(inputTime.ToUniversalTime(), snapshot.GeneratedAtUtc);
        Assert.Equal(HealthStatus.Running, snapshot.OverallStatus);
        Assert.Equal(new[] { "alpha", "zeta" }, snapshot.Clients.Select(client => client.ClientId));
        Assert.Equal(HealthComponentStatus.NotTracked, snapshot.ConnectedClients.Status);
        Assert.Null(snapshot.ConnectedClients.Value);
        Assert.Equal("N/A", snapshot.ConnectedClients.Display);
    }

    [Fact]
    public void SnapshotSerializationContainsContractFieldsWithoutRawPayloadSurface()
    {
        var facts = new HealthFacts
        {
            Server = new TransportHealthFacts(
                TransportHealthStatus.Stopped,
                host: "127.0.0.1",
                port: 6520,
                endpoint: "/mcp",
                listenerFactExplicit: true),
            Compatibility = new HealthComponent("compatibility", HealthComponentStatus.Pass),
            Configuration = new HealthComponent("configuration", HealthComponentStatus.Pass)
        };

        var snapshot = new HealthService().CreateSnapshot(facts, DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(snapshot);

        Assert.Contains("arcgis-pro-mcp-health-snapshot-v1", json);
        Assert.Contains("ConnectedClients", json);
        Assert.Contains("NotTracked", json);
        Assert.DoesNotContain("payload", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("absolutePath", json, StringComparison.OrdinalIgnoreCase);
    }
}
