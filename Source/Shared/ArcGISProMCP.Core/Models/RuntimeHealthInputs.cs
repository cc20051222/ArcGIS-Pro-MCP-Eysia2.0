using ArcGISProMCP.Core.PythonBridge;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Core.Models;

/// <summary>
/// In-memory facts collected by Compatibility. It contains no paths, payloads
/// or raw exception text and is safe for the pure health composer to consume.
/// </summary>
public sealed record RuntimeHealthInputs
{
    public bool? ServerRunning { get; init; }

    /// <summary>
    /// Managed Add-in context fact. It must not be obtained by probing maps,
    /// projects, files, processes or the ArcGIS SDK.
    /// </summary>
    public bool? HostContextLoaded { get; init; }

    public PythonBridgeState? BridgeState { get; init; }

    public string? BridgeErrorCode { get; init; }

    public StructuredLogHealth? LoggingHealth { get; init; }

    public ManagedCompatibilityFacts CompatibilityFacts { get; init; } =
        ManagedCompatibilityFacts.Current;
}
