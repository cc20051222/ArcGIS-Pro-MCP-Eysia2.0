namespace ArcGISProMCP.Logging;

/// <summary>
/// 空日志器（无操作实现），作为默认空对象避免引入外部依赖。
/// </summary>
public sealed class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();

    private NullLogger()
    {
    }

    public void Log(LogEntry entry)
    {
    }

    public void Log(LogLevel level, string message, LogContext? context = null)
    {
    }

    public void Debug(string message, string? category = null, string? requestId = null)
    {
    }

    public void Info(string message, string? category = null, string? requestId = null)
    {
    }

    public void Warning(string message, string? category = null, string? requestId = null)
    {
    }

    public void Error(string message, string? category = null, string? requestId = null, Exception? exception = null)
    {
    }
}
