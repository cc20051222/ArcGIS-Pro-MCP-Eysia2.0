namespace ArcGISProMCP.Core.Models;

/// <summary>由 Compatibility 层或测试注入的 transport 事实，不读取进程、端口或文件。</summary>
public sealed record TransportHealthFacts
{
    public TransportHealthFacts(
        TransportHealthStatus status,
        string? host = null,
        int? port = null,
        string? endpoint = null,
        bool listenerFactExplicit = false,
        string? summary = null,
        string? errorCode = null)
    {
        Status = status;
        Host = host;
        Port = port;
        Endpoint = endpoint;
        ListenerFactExplicit = listenerFactExplicit;
        Summary = summary;
        ErrorCode = errorCode;
    }

    public TransportHealthStatus Status { get; }

    public string? Host { get; }

    public int? Port { get; }

    public string? Endpoint { get; }

    public bool ListenerFactExplicit { get; }

    public string? Summary { get; }

    public string? ErrorCode { get; }

    public static TransportHealthFacts Unknown(string? summary = null, string? errorCode = null)
        => new(TransportHealthStatus.Unknown, summary: summary, errorCode: errorCode);
}

/// <summary>客户端配置/连接摘要。Optional=true 的 NotChecked 不改变整体状态。</summary>
public sealed record ClientHealthFact
{
    public ClientHealthFact(
        string clientId,
        HealthComponentStatus status,
        bool optional = true,
        string? summary = null,
        string? recommendedAction = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("Client id must not be empty.", nameof(clientId));
        }

        ClientId = clientId;
        Status = status;
        Optional = optional;
        Summary = summary;
        RecommendedAction = recommendedAction
            ?? (status == HealthComponentStatus.NotChecked
                ? "Validate this client configuration before use."
                : string.Empty);
    }

    public string ClientId { get; }

    public HealthComponentStatus Status { get; }

    public bool Optional { get; }

    public string? Summary { get; }

    public string RecommendedAction { get; }

    public static ClientHealthFact OptionalNotChecked(string clientId, string? summary = null)
        => new(clientId, HealthComponentStatus.NotChecked, optional: true, summary: summary);
}

/// <summary>
/// 整体健康评估的纯输入。所有事实必须由调用方注入，不能在 Shared 层访问机器环境。
/// </summary>
public sealed record HealthFacts
{
    public TransportHealthFacts Server { get; init; } = TransportHealthFacts.Unknown();

    public HealthComponent ArcGISHost { get; init; } = HealthComponent.NotChecked("arcgisHost", requiredForOverall: false);

    public HealthComponent Configuration { get; init; } = HealthComponent.NotChecked("configuration");

    public HealthComponent Compatibility { get; init; } = HealthComponent.NotChecked("compatibility");

    /// <summary>Bridge 是 lazy dependency；Stopped/NotChecked 不自动变成 BridgeFailure。</summary>
    public HealthComponent Bridge { get; init; } = HealthComponent.NotChecked("bridge", requiredForOverall: false);

    /// <summary>日志降级不应让业务整体状态伪装成不可用。</summary>
    public HealthComponent Logging { get; init; } = HealthComponent.NotChecked("logging", requiredForOverall: false);

    public string? ProductIdentity { get; init; }

    public IReadOnlyList<ClientHealthFact> Clients { get; init; } = Array.Empty<ClientHealthFact>();
}
