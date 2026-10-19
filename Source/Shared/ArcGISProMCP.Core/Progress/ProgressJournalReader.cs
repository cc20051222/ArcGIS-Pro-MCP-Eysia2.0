using System.Text;
using System.Text.Json;

namespace ArcGISProMCP.Core.Progress;

/// <summary>Bounded, read-only view of live JSONL. Tolerates partial final writes and rotated files.</summary>
public static class ProgressJournalReader
{
    public static IReadOnlyList<ToolProgressEvent> Read(string directory, int maxEvents = 300)
    {
        if (!Directory.Exists(directory)) return Array.Empty<ToolProgressEvent>();
        var result = new List<ToolProgressEvent>();
        foreach (var path in Directory.EnumerateFiles(directory, "progress-*.jsonl")
            .OrderByDescending(File.GetLastWriteTimeUtc).Take(5))
        {
            try
            {
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var offset = Math.Max(0, file.Length - 256 * 1024);
                file.Seek(offset, SeekOrigin.Begin);
                using var reader = new StreamReader(file, Encoding.UTF8);
                var text = reader.ReadToEnd();
                if (offset > 0) { var first = text.IndexOf('\n'); text = first < 0 ? string.Empty : text[(first+1)..]; }
                var end = text.LastIndexOf('\n');
                if (end < 0) continue;
                foreach (var line in text[..end].Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        var ev = JsonSerializer.Deserialize<ToolProgressEvent>(line);
                        if (ev is not null && !string.IsNullOrEmpty(ev.InvocationId)) result.Add(ev);
                    }
                    catch (JsonException) { /* Corrupt record is not a successful event. */ }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return result.OrderBy(e => e.ObservedAt).ThenBy(e => e.Sequence).TakeLast(Math.Clamp(maxEvents, 1, 2000)).ToArray();
    }
}
