namespace ArcGISProMCP.Logging;

/// <summary>
/// 日志条目。统一记录 Timestamp / Level / Category / RequestId / Message，
/// 不记录密码、Token 等敏感信息。
/// </summary>
public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string? Category { get; set; }
    public string? RequestId { get; set; }

    public string? CorrelationId { get; set; }

    public string? Component { get; set; }

    public string? Operation { get; set; }

    public string? ErrorCode { get; set; }

    public string? Outcome { get; set; }
    public string? Client { get; set; }
    public string? Tool { get; set; }
    public string? Message { get; set; }

    /// <summary>仅允许写入受控模板标识；Message 保留为 legacy free-text 输入。</summary>
    public string? MessageCode { get; set; }
    public TimeSpan? ExecutionTime { get; set; }

    /// <summary>规范化耗时字段；ExecutionTime 保留为 legacy compatibility。</summary>
    public long? DurationMs { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
}
