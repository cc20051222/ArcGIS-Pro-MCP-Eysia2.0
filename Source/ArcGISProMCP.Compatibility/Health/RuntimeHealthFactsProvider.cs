using ArcGISProMCP.Compatibility.Logging;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.PythonBridge;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Compatibility.Health;

/// <summary>
/// Read-only Compatibility provider. It samples only already-owned managed
/// objects and delegates pure contract assembly to Shared Core.
/// </summary>
public sealed class RuntimeHealthFactsProvider : IHealthFactsProvider
{
    private readonly MCPSettings _settings;
    private readonly Func<bool?> _serverRunning;
    private readonly Func<bool?> _hostContextLoaded;
    private readonly Func<PythonBridgeProcessManager?> _bridgeAccessor;
    private readonly Func<ILogger?> _loggerAccessor;
    private readonly ManagedCompatibilityFacts _compatibilityFacts;

    public RuntimeHealthFactsProvider(
        MCPSettings settings,
        Func<bool?> serverRunning,
        Func<bool?> hostContextLoaded,
        Func<PythonBridgeProcessManager?> bridgeAccessor,
        Func<ILogger?> loggerAccessor,
        ManagedCompatibilityFacts? compatibilityFacts = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _serverRunning = serverRunning ?? throw new ArgumentNullException(nameof(serverRunning));
        _hostContextLoaded = hostContextLoaded ?? throw new ArgumentNullException(nameof(hostContextLoaded));
        _bridgeAccessor = bridgeAccessor ?? throw new ArgumentNullException(nameof(bridgeAccessor));
        _loggerAccessor = loggerAccessor ?? throw new ArgumentNullException(nameof(loggerAccessor));
        _compatibilityFacts = compatibilityFacts ?? ManagedCompatibilityFacts.Current;
    }

    public HealthFacts GetFacts()
    {
        var bridge = TryReadBridgeFacts();
        var loggerHealth = TryReadLoggerHealth();

        return RuntimeHealthFactsFactory.Create(
            _settings,
            new RuntimeHealthInputs
            {
                ServerRunning = TryReadServerState(),
                HostContextLoaded = TryReadHostContext(),
                BridgeState = bridge.State,
                BridgeErrorCode = bridge.ErrorCode ?? _settings.RuntimePathErrorCode,
                LoggingHealth = loggerHealth,
                CompatibilityFacts = _compatibilityFacts
            });
    }

    private bool? TryReadHostContext()
    {
        try
        {
            return _hostContextLoaded();
        }
        catch
        {
            return null;
        }
    }

    private bool? TryReadServerState()
    {
        try
        {
            return _serverRunning();
        }
        catch
        {
            return null;
        }
    }

    private (PythonBridgeState? State, string? ErrorCode) TryReadBridgeFacts()
    {
        try
        {
            var bridge = _bridgeAccessor();
            return bridge is null
                ? (null, null)
                : (bridge.State, bridge.LastSafeErrorCode);
        }
        catch
        {
            return (null, null);
        }
    }

    private StructuredLogHealth? TryReadLoggerHealth()
    {
        ILogger? logger;
        try
        {
            logger = _loggerAccessor();
        }
        catch
        {
            return null;
        }

        try
        {
            return logger switch
            {
                StructuredFileLogger structured => structured.Health,
                ManagedFileLogger managed => managed.Health,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
