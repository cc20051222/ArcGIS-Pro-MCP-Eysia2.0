using System.IO;
using System.Text;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Compatibility.Logging;

/// <summary>简单文件日志器。</summary>
public sealed class FileLogger : ILogger
{
    private readonly string _path;
    private readonly object _gate = new();

    public FileLogger(string path)
    {
        _path = path;
    }

    public void Log(LogEntry entry)
    {
        var line = $"{entry.Timestamp:O} [{entry.Level}] {(entry.Category ?? "-")} {(entry.RequestId ?? "-")} {(entry.Message ?? string.Empty)}{(entry.Error is null ? string.Empty : " | " + entry.Error)}";
        lock (_gate)
        {
            File.AppendAllText(_path, line + Environment.NewLine, new UTF8Encoding(false));
        }
    }

    public void Debug(string message, string? category = null, string? requestId = null)
        => Log(new LogEntry { Timestamp = DateTimeOffset.Now, Level = LogLevel.Debug, Category = category, RequestId = requestId, Message = message });

    public void Info(string message, string? category = null, string? requestId = null)
        => Log(new LogEntry { Timestamp = DateTimeOffset.Now, Level = LogLevel.Information, Category = category, RequestId = requestId, Message = message });

    public void Warning(string message, string? category = null, string? requestId = null)
        => Log(new LogEntry { Timestamp = DateTimeOffset.Now, Level = LogLevel.Warning, Category = category, RequestId = requestId, Message = message });

    public void Error(string message, string? category = null, string? requestId = null, Exception? exception = null)
        => Log(new LogEntry { Timestamp = DateTimeOffset.Now, Level = LogLevel.Error, Category = category, RequestId = requestId, Message = message, Error = exception?.ToString() });
}
