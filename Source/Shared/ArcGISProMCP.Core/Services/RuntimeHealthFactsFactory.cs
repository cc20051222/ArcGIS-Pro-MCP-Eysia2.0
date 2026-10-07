using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.PythonBridge;
using ArcGISProMCP.Core.Runtime;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// Pure composition of read-only managed facts into the shared health contract.
/// </summary>
public static class RuntimeHealthFactsFactory
{
    public static HealthFacts Create(MCPSettings settings, RuntimeHealthInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(inputs);

        return new HealthFacts
        {
            ProductIdentity = inputs.CompatibilityFacts.IsComplete
                ? inputs.CompatibilityFacts.AddInVersion
                : null,
            Server = BuildServerFact(settings, inputs.ServerRunning),
            ArcGISHost = BuildHostFact(inputs.HostContextLoaded),
            Configuration = RuntimeConfigurationValidator.Validate(settings),
            Compatibility = BuildCompatibilityFact(inputs.CompatibilityFacts),
            Bridge = BuildBridgeFact(inputs.BridgeState, inputs.BridgeErrorCode),
            Logging = BuildLoggingFact(inputs.LoggingHealth),
            Clients = BuildExternalClientFacts()
        };
    }

    private static TransportHealthFacts BuildServerFact(MCPSettings settings, bool? running)
    {
        var host = RuntimeConfigurationValidator.IsLoopback(settings.Host)
            ? settings.Host
            : null;
        int? port = settings.Port is > 0 and <= 65535
            ? settings.Port
            : null;
        var endpoint = string.Equals(
                settings.Endpoint,
                MCPSettings.DefaultEndpoint,
                StringComparison.Ordinal)
            ? MCPSettings.DefaultEndpoint
            : null;

        if (!running.HasValue)
        {
            return new TransportHealthFacts(
                TransportHealthStatus.Unknown,
                host,
                port,
                endpoint,
                listenerFactExplicit: false,
                summary: "MCP server state was not available from managed memory.",
                errorCode: "SERVER_STATE_NOT_CHECKED");
        }

        return new TransportHealthFacts(
            running.Value ? TransportHealthStatus.Running : TransportHealthStatus.Stopped,
            host,
            port,
            endpoint,
            listenerFactExplicit: true,
            summary: running.Value
                ? "MCP server state is running."
                : "MCP server state is stopped.");
    }

    private static HealthComponent BuildCompatibilityFact(ManagedCompatibilityFacts facts)
    {
        if (!facts.IsComplete)
        {
            return new HealthComponent(
                "compatibility",
                HealthComponentStatus.NotChecked,
                summary: "Managed compatibility declaration is incomplete; live SDK/ArcPy compatibility is not checked.",
                errorCode: "COMPATIBILITY_NOT_CHECKED");
        }

        if (!facts.IsLiveVerified || !facts.LiveCompatibilityMatches.HasValue)
        {
            return new HealthComponent(
                "compatibility",
                HealthComponentStatus.DeclaredOnly,
                summary:
                    $"Declared target: Pro {facts.SupportedArcGISProVersion}; "
                    + "live compatibility not checked.",
                declaredTarget: facts.SupportedArcGISProVersion);
        }

        if (!facts.LiveCompatibilityMatches.Value)
        {
            return new HealthComponent(
                "compatibility",
                HealthComponentStatus.Incompatible,
                summary: "Live managed compatibility facts do not match the supported target.",
                errorCode: "COMPATIBILITY_MISMATCH",
                declaredTarget: facts.SupportedArcGISProVersion);
        }

        return new HealthComponent(
            "compatibility",
            HealthComponentStatus.Pass,
            summary:
                $"Managed declaration only: Add-in {facts.AddInVersion}; "
                + $"supported ArcGIS Pro {facts.SupportedArcGISProVersion}; "
                + $"target {facts.TargetFramework}; live managed compatibility matched.",
            declaredTarget: facts.SupportedArcGISProVersion);
    }

    private static HealthComponent BuildHostFact(bool? hostContextLoaded)
    {
        if (hostContextLoaded == true)
        {
            return new HealthComponent(
                "arcgisHost",
                HealthComponentStatus.Pass,
                requiredForOverall: false,
                summary: "Add-in host context is loaded; live map/project state is not probed.");
        }

        if (hostContextLoaded == false)
        {
            return new HealthComponent(
                "arcgisHost",
                HealthComponentStatus.Unavailable,
                requiredForOverall: false,
                summary: "Add-in host context is not loaded; live map/project state is not probed.",
                errorCode: "ARCGIS_HOST_CONTEXT_UNAVAILABLE");
        }

        return new HealthComponent(
            "arcgisHost",
            HealthComponentStatus.NotChecked,
            requiredForOverall: false,
            summary: "Add-in host context was not supplied; live map/project state is not probed.",
            errorCode: "ARCGIS_HOST_CONTEXT_NOT_CHECKED");
    }

    private static HealthComponent BuildBridgeFact(
        PythonBridgeState? state,
        string? errorCode)
    {
        var safeErrorCode = SafeBridgeErrorCode(errorCode);
        if (RuntimePathErrorCodes.IsSafe(safeErrorCode))
        {
            return new HealthComponent(
                "bridge",
                HealthComponentStatus.Failure,
                requiredForOverall: false,
                summary: "Python Bridge runtime paths failed a fixed safety gate.",
                errorCode: safeErrorCode);
        }

        if (!state.HasValue)
        {
            return new HealthComponent(
                "bridge",
                HealthComponentStatus.NotChecked,
                requiredForOverall: false,
                summary: "Bridge is not initialized; status does not start it.");
        }

        return state.Value switch
        {
            PythonBridgeState.Running => new HealthComponent(
                "bridge",
                HealthComponentStatus.Running,
                requiredForOverall: false,
                summary: "Python Bridge is running."),
            PythonBridgeState.Stopped => new HealthComponent(
                "bridge",
                HealthComponentStatus.Stopped,
                requiredForOverall: false,
                summary: "Python Bridge is stopped; status does not start it."),
            PythonBridgeState.Starting => new HealthComponent(
                "bridge",
                HealthComponentStatus.Degraded,
                requiredForOverall: false,
                summary: "Python Bridge is starting."),
            PythonBridgeState.Stopping => new HealthComponent(
                "bridge",
                HealthComponentStatus.Degraded,
                requiredForOverall: false,
                summary: "Python Bridge is stopping."),
            PythonBridgeState.Faulted => new HealthComponent(
                "bridge",
                HealthComponentStatus.Failure,
                requiredForOverall: false,
                summary: "Python Bridge reported a lifecycle failure.",
                errorCode: safeErrorCode ?? "PYTHON_BRIDGE_UNAVAILABLE"),
            PythonBridgeState.Disposed => new HealthComponent(
                "bridge",
                HealthComponentStatus.Stopped,
                requiredForOverall: false,
                summary: "Python Bridge is disposed; status does not restart it."),
            _ => new HealthComponent(
                "bridge",
                HealthComponentStatus.Unknown,
                requiredForOverall: false,
                summary: "Bridge state is unknown.",
                errorCode: "BRIDGE_STATE_UNKNOWN")
        };
    }

    private static HealthComponent BuildLoggingFact(StructuredLogHealth? health)
    {
        if (health is null)
        {
            return new HealthComponent(
                "logging",
                HealthComponentStatus.NotChecked,
                requiredForOverall: false,
                summary: "Structured logger health is not connected to the production logger.");
        }

        return new HealthComponent(
            "logging",
            health.Status == StructuredLogHealthStatus.Healthy
                ? HealthComponentStatus.Pass
                : HealthComponentStatus.Degraded,
            requiredForOverall: false,
            summary: health.Status == StructuredLogHealthStatus.Healthy
                ? "Structured logging health is healthy."
                : "Structured logging health is degraded.",
            errorCode: health.Status == StructuredLogHealthStatus.Healthy
                ? null
                : "LOGGING_DEGRADED");
    }

    private static IReadOnlyList<ClientHealthFact> BuildExternalClientFacts()
        => new[]
        {
            new ClientHealthFact(
                "codex",
                HealthComponentStatus.NotChecked,
                optional: false,
                summary: "External client configuration is not inspected."),
            new ClientHealthFact(
                "cursor",
                HealthComponentStatus.NotChecked,
                optional: false,
                summary: "External client configuration is not inspected."),
            new ClientHealthFact(
                "deepseek-harness",
                HealthComponentStatus.NotChecked,
                optional: false,
                summary: "External client configuration is not inspected."),
            new ClientHealthFact(
                "claude-desktop",
                HealthComponentStatus.NotChecked,
                optional: true,
                summary: "Optional external client configuration is not inspected.")
        };

    private static string? SafeBridgeErrorCode(string? errorCode)
        => errorCode is "PYTHON_BRIDGE_UNAVAILABLE"
            or "PYTHON_TIMEOUT"
            or "PYTHON_OUTPUT_LIMIT_EXCEEDED"
            or "PYTHON_PROTOCOL_ERROR"
            or "INTERNAL_ERROR"
            ? errorCode
            : RuntimePathErrorCodes.IsSafe(errorCode)
                ? errorCode
                : null;
}
