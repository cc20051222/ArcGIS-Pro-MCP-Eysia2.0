using System.Text.Json;
using ArcGISProMCP.Configuration;

namespace ArcGISProMCP.Core.Progress;

/// <summary>Observed execution events, never a claim of artifact verification.</summary>
public sealed record ToolProgressEvent(long Sequence, string InvocationId, string? RequestId,
    string Tool, string Stage, string State, double? Completed, double? Total,
    string Message, string Source, DateTimeOffset ObservedAt);

/// <summary>SDK-free, request-scoped progress. Capturing a reporter also works in SDK callbacks
/// which do not flow ExecutionContext. Observation failure must not change GIS effects.</summary>
public static class ToolProgress
{
    private static readonly AsyncLocal<ProgressScope?> Ambient = new();
    public static ToolProgressHub DefaultHub { get; } = new();
    public static ProgressScope? Current => Ambient.Value;
    public static ProgressScope Begin(ToolProgressHub hub, string? requestId, string tool,
        Action<ToolProgressEvent>? observer = null)
    {
        var scope = new ProgressScope(hub, requestId, tool, observer, Ambient.Value);
        Ambient.Value = scope;
        return scope;
    }
    public static void Report(string stage, string state, string message,
        double? completed = null, double? total = null, string source = "tool")
        => Ambient.Value?.Report(stage, state, message, completed, total, source);

    public sealed class ProgressScope : IDisposable
    {
        private readonly ToolProgressHub _hub;
        private readonly Action<ToolProgressEvent>? _observer;
        private readonly ProgressScope? _parent;
        private readonly object _gate = new();
        private bool _closed;
        internal ProgressScope(ToolProgressHub hub, string? requestId, string tool,
            Action<ToolProgressEvent>? observer, ProgressScope? parent)
        { _hub = hub; RequestId = requestId; Tool = tool; _observer = observer; _parent = parent; }
        public string InvocationId { get; } = Guid.NewGuid().ToString("N");
        public string? RequestId { get; }
        public string Tool { get; }
        public void Report(string stage, string state, string message,
            double? completed = null, double? total = null, string source = "tool")
        {
            lock (_gate)
            {
                if (_closed) return;
                // No manufactured denominator or invalid percentage.
                if (completed is { } c && (!double.IsFinite(c) || c < 0)) completed = null;
                if (total is { } t && (!double.IsFinite(t) || t <= 0)) total = null;
                if (completed > total) { completed = null; total = null; }
                var ev = _hub.Publish(InvocationId, RequestId, Tool, stage, state,
                    completed, total, message, source);
                try { _observer?.Invoke(ev); } catch { /* Observer is not an execution guard. */ }
            }
        }
        public void Dispose()
        {
            lock (_gate) _closed = true;
            if (ReferenceEquals(Ambient.Value, this)) Ambient.Value = _parent;
        }
    }
}

/// <summary>Bounded in-memory feed; optional append-only D-drive journal for a separate panel.
/// No arguments or tool results are persisted, and no network listener is opened.</summary>
public sealed class ToolProgressHub
{
    private readonly object _gate = new();
    private readonly Queue<ToolProgressEvent> _events = new();
    private long _sequence;
    private string? _journal;
    public const int Capacity = MCPSettings.ProgressEventCapacity;
    public string? JournalError { get; private set; }
    public string? JournalPath { get { lock (_gate) return _journal; } }
    public void ConfigureJournal(string directory)
    {
        var full = Path.GetFullPath(directory);
        if (!string.Equals(Path.GetPathRoot(full), @"D:\", StringComparison.OrdinalIgnoreCase)
            || full.Contains(@"\appdata\local\temp", StringComparison.OrdinalIgnoreCase)
            || full.Contains(@"\windows\temp", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Progress journal must use an explicit owned D-drive directory.");
        Directory.CreateDirectory(full);
        var path = Path.Combine(full, $"progress-{Environment.ProcessId}-{Guid.NewGuid():N}.jsonl");
        using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite)) { }
        lock (_gate) { _journal = path; JournalError = null; }
    }
    public IReadOnlyList<ToolProgressEvent> Snapshot()
    { lock (_gate) return _events.ToArray(); }
    internal ToolProgressEvent Publish(string invocationId, string? requestId, string tool,
        string stage, string state, double? completed, double? total, string message, string source)
    {
        lock (_gate)
        {
            var ev = new ToolProgressEvent(++_sequence, invocationId, requestId, tool,
                stage, state, completed, total, message.Length > 1000 ? message[..1000] : message,
                source, DateTimeOffset.UtcNow);
            _events.Enqueue(ev);
            while (_events.Count > Capacity) _events.Dequeue();
            if (_journal is not null)
            {
                try
                {
                    // Bounded disk growth for one session; no deletion of historical records.
                    if (new FileInfo(_journal).Length > MCPSettings.ProgressJournalMaxBytes)
                    {
                        JournalError = "journal-size-limit-reached; in-memory feed remains available";
                        _journal = null;
                    }
                    else File.AppendAllText(_journal, JsonSerializer.Serialize(ev) + "\n");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { JournalError = ex.GetType().Name; }
            }
            return ev;
        }
    }
}
