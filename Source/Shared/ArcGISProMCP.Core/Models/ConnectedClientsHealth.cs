namespace ArcGISProMCP.Core.Models;

/// <summary>
/// Connected Clients 的诚实表示。当前 transport 没有 session registry，因此 Value 永远为空。
/// </summary>
public sealed record ConnectedClientsHealth
{
    private ConnectedClientsHealth()
    {
    }

    public HealthComponentStatus Status { get; init; } = HealthComponentStatus.NotTracked;

    public int? Value { get; init; }

    public string Display { get; init; } = "N/A";

    public string Reason { get; init; } = "HTTP transport has no durable client/session registry";

    public static ConnectedClientsHealth NotTracked()
        => new();
}
