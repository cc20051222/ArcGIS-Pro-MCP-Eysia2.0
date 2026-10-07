using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Compatibility.Logging;

/// <summary>
/// Compatibility-layer adapter for the explicitly owned production structured
/// sink. Legacy log files are never opened, migrated, rotated or deleted.
/// </summary>
public sealed class ManagedFileLogger : ILogger, IDisposable
{
    private readonly StructuredFileLogger _inner;

    public ManagedFileLogger(
        string ownedDirectory,
        StructuredLogFilePolicy? policy = null,
        LogRedactionPolicy? redactionPolicy = null,
        Func<DateTimeOffset>? clock = null)
    {
        _inner = new StructuredFileLogger(ownedDirectory, policy, redactionPolicy, clock);
    }

    public StructuredLogHealth Health => _inner.Health;

    public void Log(LogEntry entry)
        => _inner.Log(ProductionLogEntryNormalizer.Normalize(entry));

    public void Debug(string message, string? category = null, string? requestId = null)
        => Log(new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Debug,
            Category = category,
            RequestId = requestId,
            Message = message
        });

    public void Info(string message, string? category = null, string? requestId = null)
        => Log(new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Information,
            Category = category,
            RequestId = requestId,
            Message = message
        });

    public void Warning(string message, string? category = null, string? requestId = null)
        => Log(new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Warning,
            Category = category,
            RequestId = requestId,
            Message = message
        });

    public void Error(string message, string? category = null, string? requestId = null, Exception? exception = null)
        => Log(new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Error,
            Category = category,
            RequestId = requestId,
            Message = message,
            Error = exception?.ToString()
        });

    public void Dispose() => _inner.Dispose();
}
