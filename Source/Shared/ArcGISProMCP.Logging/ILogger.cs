namespace ArcGISProMCP.Logging;

/// <summary>
/// 日志抽象。RequestId 保持兼容；结构化记录可额外使用 CorrelationId、Component、Operation、ErrorCode、Outcome 和 DurationMs。
/// </summary>
public interface ILogger
{
    void Log(LogEntry entry);

    /// <summary>
    /// 结构化便捷入口。默认实现只组装 allowlist-able fields，具体 sink 决定脱敏和落盘策略。
    /// </summary>
    void Log(LogLevel level, string message, LogContext? context = null)
    {
        context ??= new LogContext();
        Log(new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Message = message,
            RequestId = context.RequestId,
            MessageCode = context.MessageCode,
            CorrelationId = context.CorrelationId,
            Component = context.Component,
            Operation = context.Operation,
            ErrorCode = context.ErrorCode,
            Outcome = context.Outcome,
            ExecutionTime = context.ExecutionTime,
            DurationMs = context.DurationMs
                ?? (context.ExecutionTime.HasValue
                    ? (long)Math.Max(0, Math.Round(context.ExecutionTime.Value.TotalMilliseconds))
                    : null)
        });
    }

    void Debug(string message, string? category = null, string? requestId = null);

    void Info(string message, string? category = null, string? requestId = null);

    void Warning(string message, string? category = null, string? requestId = null);

    void Error(string message, string? category = null, string? requestId = null, Exception? exception = null);
}
