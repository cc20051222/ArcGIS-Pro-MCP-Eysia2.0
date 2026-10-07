namespace ArcGISProMCP.Core.Models;

/// <summary>版本化、可序列化的健康快照。该模型不包含 ArcGIS SDK 类型或原始 GIS 数据。</summary>
public sealed record HealthSnapshot
{
    public const string SchemaName = "arcgis-pro-mcp-health-snapshot-v1";

    public string Schema { get; init; } = SchemaName;

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public string? ProductIdentity { get; init; }

    public HealthStatus OverallStatus { get; init; }

    public string RecommendedAction { get; init; } = string.Empty;

    public TransportHealthFacts Server { get; init; } = TransportHealthFacts.Unknown();

    public HealthComponent ArcGISHost { get; init; } = HealthComponent.NotChecked("arcgisHost", requiredForOverall: false);

    public HealthComponent Configuration { get; init; } = HealthComponent.NotChecked("configuration");

    public HealthComponent Compatibility { get; init; } = HealthComponent.NotChecked("compatibility");

    public HealthComponent Bridge { get; init; } = HealthComponent.NotChecked("bridge", requiredForOverall: false);

    public ConnectedClientsHealth ConnectedClients { get; init; } = ConnectedClientsHealth.NotTracked();

    public HealthComponent Logging { get; init; } = HealthComponent.NotChecked("logging", requiredForOverall: false);

    public IReadOnlyList<ClientHealthFact> Clients { get; init; } = Array.Empty<ClientHealthFact>();
}
