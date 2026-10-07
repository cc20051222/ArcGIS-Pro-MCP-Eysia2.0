using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// Pure status formatter. It deliberately renders only fixed labels and enum
/// states; injected summaries, identifiers and error text never reach the UI.
/// </summary>
public sealed class StatusPresenter : IStatusPresenter
{
    public StatusPresentation Present(HealthSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var statusLabel = OverallStatusLabel(snapshot.OverallStatus);
        var details = string.Join(
            Environment.NewLine,
            "Server: " + TransportStatusLabel(snapshot.Server.Status),
            CanonicalEndpointLine(snapshot.Server),
            HostLine(snapshot.ArcGISHost),
            "Configuration: " + ComponentStatusLabel(snapshot.Configuration.Status),
            CompatibilityLine(snapshot.Compatibility),
            ProductIdentityLine(snapshot.ProductIdentity),
            "Bridge: " + ComponentStatusLabel(snapshot.Bridge.Status),
            "Logging: " + ComponentStatusLabel(snapshot.Logging.Status),
            "Connected Clients: N/A (not tracked)",
            ClientLines(snapshot.Clients));

        return new StatusPresentation(
            "MCP Status: " + statusLabel,
            OverallSummary(snapshot.OverallStatus),
            details,
            RecommendedAction(snapshot));
    }

    /// <summary>用于 StatusButton 异常路径的固定、脱敏 fallback。</summary>
    public static StatusPresentation UnavailableFallback()
        => new(
            "MCP Status: Unavailable",
            "Health facts could not be read safely.",
            "Overall: Unavailable\nConnected Clients: N/A (not tracked)",
            "Refresh status after reviewing the local runtime.");

    private static string RecommendedAction(HealthSnapshot snapshot)
    {
        if (snapshot.OverallStatus == HealthStatus.Running
            && HasStatus(snapshot, HealthComponentStatus.Degraded))
        {
            return "Review the degraded component and refresh the read-only status.";
        }

        if (snapshot.OverallStatus == HealthStatus.Running
            && (HasStatus(snapshot, HealthComponentStatus.NotChecked)
                || HasStatus(snapshot, HealthComponentStatus.DeclaredOnly)))
        {
            return "Review the declared or unverified component facts; this view does not run probes.";
        }

        return HealthStatusPolicy.RecommendedAction(snapshot.OverallStatus);
    }

    private static bool HasStatus(HealthSnapshot snapshot, HealthComponentStatus status)
        => snapshot.ArcGISHost.Status == status
           || snapshot.Configuration.Status == status
           || snapshot.Compatibility.Status == status
           || snapshot.Bridge.Status == status
           || snapshot.Logging.Status == status
           || (snapshot.Clients ?? Array.Empty<ClientHealthFact>()).Any(client => client.Status == status);

    private static string ClientLines(IReadOnlyList<ClientHealthFact>? clients)
    {
        if (clients is null || clients.Count == 0)
        {
            return "External clients: NotChecked";
        }

        return string.Join(
            Environment.NewLine,
            clients.Select(client =>
                "Client " + ClientLabel(client.ClientId) + ": " + ComponentStatusLabel(client.Status)));
    }

    private static string CanonicalEndpointLine(TransportHealthFacts server)
        => string.Equals(server.Host, MCPSettings.DefaultHost, StringComparison.Ordinal)
           && server.Port == MCPSettings.DefaultPort
           && string.Equals(server.Endpoint, MCPSettings.DefaultEndpoint, StringComparison.Ordinal)
            ? "Endpoint: http://127.0.0.1:6520/mcp"
            : "Endpoint: not displayed (managed canonical endpoint not confirmed)";

    private static string HostLine(HealthComponent host)
        => "ArcGIS Host: " + ComponentStatusLabel(host.Status)
           + "; live map/project not probed";

    private static string CompatibilityLine(HealthComponent compatibility)
        => compatibility.Status == HealthComponentStatus.DeclaredOnly
           && ManagedCompatibilityFacts.IsSafeVersionToken(compatibility.DeclaredTarget)
            ? "Compatibility: Declared target: Pro " + compatibility.DeclaredTarget
              + " / live compatibility not checked"
            : "Compatibility: " + ComponentStatusLabel(compatibility.Status);

    private static string ProductIdentityLine(string? productIdentity)
        => ManagedCompatibilityFacts.IsSafeVersionToken(productIdentity)
            ? "Add-in: " + productIdentity
            : "Add-in: not displayed";

    private static string ClientLabel(string clientId)
        => clientId switch
        {
            "codex" => "Codex",
            "cursor" => "Cursor",
            "deepseek-harness" => "DeepSeek Harness",
            "claude-desktop" => "Claude Desktop",
            _ => "External client"
        };

    private static string OverallSummary(HealthStatus status)
        => status switch
        {
            HealthStatus.Running => "MCP Server transport is running.",
            HealthStatus.Stopped => "MCP Server transport is stopped.",
            HealthStatus.Unavailable => "A required runtime fact is unavailable or unverified.",
            HealthStatus.Incompatible => "Managed compatibility facts are incompatible.",
            HealthStatus.BridgeFailure => "Python Bridge reported a safe lifecycle failure.",
            HealthStatus.MalformedConfiguration => "Runtime server configuration is malformed.",
            _ => "Health status is unavailable."
        };

    private static string OverallStatusLabel(HealthStatus status)
        => status switch
        {
            HealthStatus.Running => "Running",
            HealthStatus.Stopped => "Stopped",
            HealthStatus.Unavailable => "Unavailable",
            HealthStatus.Incompatible => "Incompatible",
            HealthStatus.BridgeFailure => "Bridge Failure",
            HealthStatus.MalformedConfiguration => "Malformed Configuration",
            _ => "Unavailable"
        };

    private static string TransportStatusLabel(TransportHealthStatus status)
        => status switch
        {
            TransportHealthStatus.Running => "Running",
            TransportHealthStatus.Stopped => "Stopped",
            TransportHealthStatus.Unknown => "Unknown",
            _ => "Unknown"
        };

    private static string ComponentStatusLabel(HealthComponentStatus status)
        => status switch
        {
            HealthComponentStatus.Pass => "Pass",
            HealthComponentStatus.DeclaredOnly => "DeclaredOnly",
            HealthComponentStatus.Running => "Running",
            HealthComponentStatus.Stopped => "Stopped",
            HealthComponentStatus.Failure => "Failure",
            HealthComponentStatus.Unavailable => "Unavailable",
            HealthComponentStatus.Incompatible => "Incompatible",
            HealthComponentStatus.MalformedConfiguration => "Malformed Configuration",
            HealthComponentStatus.Unknown => "Unknown",
            HealthComponentStatus.NotChecked => "NotChecked",
            HealthComponentStatus.NotTracked => "NotTracked",
            HealthComponentStatus.Degraded => "Degraded",
            _ => "Unknown"
        };
}
