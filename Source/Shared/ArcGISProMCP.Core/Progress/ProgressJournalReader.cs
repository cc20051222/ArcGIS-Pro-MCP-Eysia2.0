using System.Text;
using System.Text.Json;

namespace ArcGISProMCP.Core.Progress;

public sealed record ProgressJournalReadResult(IReadOnlyList<ToolProgressEvent> Events,int UnreadableFiles,int InvalidRecords,bool DirectoryUnavailable)
{
    public string? Error=>DirectoryUnavailable?"事件日志目录暂不可读":UnreadableFiles==0&&InvalidRecords==0?null:
        string.Join("；",new[] {UnreadableFiles>0?$"{UnreadableFiles} 个事件文件不可读":null,InvalidRecords>0?$"已忽略 {InvalidRecords} 条损坏事件":null}.Where(x=>x is not null));
}

/// <summary>Bounded, read-only view of live JSONL. Tolerates partial final writes and rotated files.</summary>
public static class ProgressJournalReader
{
    public static IReadOnlyList<ToolProgressEvent> Read(string directory, int maxEvents = 300)
        =>ReadWithStatus(directory,maxEvents).Events;

    public static ProgressJournalReadResult ReadWithStatus(string directory,int maxEvents=300)
    {
        if (!Directory.Exists(directory)) return new(Array.Empty<ToolProgressEvent>(),0,0,false);
        var result = new List<ToolProgressEvent>();var unreadable=0;var invalid=0;
        string[] paths;
        try { paths=Directory.EnumerateFiles(directory,"progress-*.jsonl").OrderByDescending(File.GetLastWriteTimeUtc).Take(5).ToArray(); }
        catch(DirectoryNotFoundException) { return new(Array.Empty<ToolProgressEvent>(),0,0,false); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { return new(Array.Empty<ToolProgressEvent>(),0,0,true); }
        foreach (var path in paths)
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
                        else invalid++;
                    }
                    catch (JsonException) { invalid++; }
                }
            }
            catch (FileNotFoundException) { /* Normal concurrent rotation. */ }
            catch (DirectoryNotFoundException) { /* Normal concurrent rotation. */ }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { unreadable++; }
        }
        return new(result.OrderBy(e => e.ObservedAt).ThenBy(e => e.Sequence).TakeLast(Math.Clamp(maxEvents, 1, 2000)).ToArray(),unreadable,invalid,false);
    }
}
