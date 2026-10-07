namespace ArcGISProMCP.Logging;

/// <summary>可选的结构化日志上下文；不承载请求 payload 或凭据。</summary>
public sealed record LogContext
{
    public string? RequestId { get; init; }

    public string? MessageCode { get; init; }

    public string? CorrelationId { get; init; }

    public string? Component { get; init; }

    public string? Operation { get; init; }

    public string? ErrorCode { get; init; }

    public string? Outcome { get; init; }

    public TimeSpan? ExecutionTime { get; init; }

    public long? DurationMs { get; init; }
}
