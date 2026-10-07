using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// 整体状态优先级策略：incompatible → malformed configuration → bridge failure → unavailable → running → stopped。
/// </summary>
public static class HealthStatusPolicy
{
    public static HealthStatus DetermineOverall(HealthFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.Compatibility.Status == HealthComponentStatus.Incompatible
            || facts.Configuration.Status == HealthComponentStatus.Incompatible
            || HasRequiredClientStatus(facts, HealthComponentStatus.Incompatible))
        {
            return HealthStatus.Incompatible;
        }

        if (facts.Configuration.Status == HealthComponentStatus.MalformedConfiguration
            || HasRequiredClientStatus(facts, HealthComponentStatus.MalformedConfiguration))
        {
            return HealthStatus.MalformedConfiguration;
        }

        if (facts.Bridge.Status == HealthComponentStatus.Failure)
        {
            return HealthStatus.BridgeFailure;
        }

        if (facts.Bridge.Status is HealthComponentStatus.Unavailable or HealthComponentStatus.Unknown or HealthComponentStatus.Degraded
            || facts.ArcGISHost.Status is HealthComponentStatus.Unavailable or HealthComponentStatus.Unknown or HealthComponentStatus.Degraded
            || facts.Server.Status == TransportHealthStatus.Unknown
            || HasUnavailableRequiredComponent(facts)
            || facts.Clients.Any(client => !client.Optional && client.Status is
                HealthComponentStatus.Unavailable
                or HealthComponentStatus.Unknown
                or HealthComponentStatus.Degraded
                or HealthComponentStatus.Failure))
        {
            return HealthStatus.Unavailable;
        }

        return facts.Server.Status == TransportHealthStatus.Running
            ? HealthStatus.Running
            : HealthStatus.Stopped;
    }

    public static string RecommendedAction(HealthStatus status)
        => status switch
        {
            HealthStatus.Running => "Use the canonical loopback MCP endpoint.",
            HealthStatus.Stopped => "Start the MCP server after reviewing compatibility and configuration facts.",
            HealthStatus.Unavailable => "Check the unavailable component and refresh the read-only diagnostics.",
            HealthStatus.Incompatible => "Install or select a supported ArcGIS Pro/Add-in/runtime combination.",
            HealthStatus.BridgeFailure => "Review the sanitized Bridge error and retry only through the approved lifecycle.",
            HealthStatus.MalformedConfiguration => "Correct the client configuration and run Validate again.",
            _ => "Review the health snapshot."
        };

    private static bool HasUnavailableRequiredComponent(HealthFacts facts)
        => RequiredComponentUnavailable(facts.Compatibility)
           || RequiredComponentUnavailable(facts.Configuration)
           || RequiredComponentUnavailable(facts.ArcGISHost)
           || RequiredComponentUnavailable(facts.Bridge)
           || RequiredComponentUnavailable(facts.Logging);

    private static bool HasRequiredClientStatus(HealthFacts facts, HealthComponentStatus status)
        => facts.Clients.Any(client => !client.Optional && client.Status == status);

    private static bool RequiredComponentUnavailable(HealthComponent component)
    {
        if (!component.RequiredForOverall)
        {
            return false;
        }

        return component.Status is HealthComponentStatus.Unavailable
            or HealthComponentStatus.Unknown
            or HealthComponentStatus.Degraded
            or HealthComponentStatus.Failure;
    }
}
