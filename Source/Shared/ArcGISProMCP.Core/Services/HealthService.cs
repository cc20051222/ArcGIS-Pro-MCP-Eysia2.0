using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Services;

/// <summary>使用注入事实生成稳定快照的纯 Shared 实现。</summary>
public sealed class HealthService : IHealthService
{
    public HealthSnapshot CreateSnapshot(HealthFacts facts, DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var clients = facts.Clients
            .OrderBy(client => client.ClientId, StringComparer.Ordinal)
            .ToArray();

        var overall = HealthStatusPolicy.DetermineOverall(facts);
        return new HealthSnapshot
        {
            GeneratedAtUtc = generatedAtUtc.ToUniversalTime(),
            ProductIdentity = facts.ProductIdentity,
            OverallStatus = overall,
            RecommendedAction = HealthStatusPolicy.RecommendedAction(overall),
            Server = facts.Server,
            ArcGISHost = facts.ArcGISHost,
            Configuration = facts.Configuration,
            Compatibility = facts.Compatibility,
            Bridge = facts.Bridge,
            ConnectedClients = ConnectedClientsHealth.NotTracked(),
            Logging = facts.Logging,
            Clients = clients
        };
    }
}
