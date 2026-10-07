using System.Text;
using System.Text.Json;
using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-062 · 受控 GP 审计日志（jsonl，一行一调用）。
/// G-138：审计文件**禁 %TEMP%**；默认落程序集旁 <c>gp-audit</c> 目录，可用环境变量
/// <c>ARCGIS_PRO_MCP_GP_AUDIT_DIR</c> 覆盖（LIVE 期指向 run 目录）。
/// 写侧：仅追加（append-only）；读侧：<see cref="GpAuditLog.Read"/> 为 get_audit_log 提供
/// 过滤/截断/损坏行跳过。**不提供删除/改写能力（不可篡改性）**。
/// </summary>
public static class GpAuditLog
{
    /// <summary>审计目录环境变量名。</summary>
    public const string AuditDirVariable = "ARCGIS_PRO_MCP_GP_AUDIT_DIR";

    public const string FileName = "gp-audit.jsonl";

    /// <summary>解析审计文件全路径（目录 → 文件名固定）。</summary>
    public static string ResolvePath(string? explicitDir = null)
    {
        var dir = explicitDir;
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Environment.GetEnvironmentVariable(AuditDirVariable);
        }

        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Path.Combine(AppContext.BaseDirectory, "gp-audit");
        }

        var full = Path.GetFullPath(dir);
        var tempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        if (full.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "G-138: the GP audit log must not be written under %TEMP% (got '" + full + "').");
        }

        return Path.Combine(full, FileName);
    }

    /// <summary>
    /// 追加一条审计（append-only；目录自动创建；写入失败返回 false 由调用方披露 —— 审计失败**不阻断** GP 结果，
    /// 但结果中 AuditEntryIndex=0 明示未落盘）。
    /// </summary>
    public static bool TryAppend(string path, GpAuditEntry entry, out long entryIndex, out string error)
    {
        entryIndex = 0;
        error = string.Empty;
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            lock (Gate)
            {
                entryIndex = File.Exists(path) ? File.ReadAllLines(path).Length + 1 : 1;
                var line = JsonSerializer.Serialize(entry, JsonOptions);
                File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            entryIndex = 0;
            return false;
        }
    }

    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// 读取审计（get_audit_log）：时间窗/工具名/结果过滤 + 条数上限；损坏行跳过并计数（不抛错）。
    /// </summary>
    public static GpAuditQueryResult Read(
        string path,
        string? toolFilter = null,
        bool? successFilter = null,
        int limit = 50,
        int skip = 0)
    {
        var result = new GpAuditQueryResult { Path = path };
        if (limit < 0)
        {
            limit = 0;
        }

        try
        {
            if (!File.Exists(path))
            {
                result.FileExists = false;
                return result;
            }
        }
        catch (Exception ex)
        {
            result.CorruptLines = -1;
            result.Error = ex.Message;
            return result;
        }

        result.FileExists = true;
        var rows = new List<GpAuditEntry>();
        lock (Gate)
        {
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var e = JsonSerializer.Deserialize<GpAuditEntry>(line, JsonOptions);
                    if (e is not null)
                    {
                        rows.Add(e);
                    }
                }
                catch
                {
                    result.CorruptLines++;
                }
            }
        }

        result.TotalEntries = rows.Count;

        IEnumerable<GpAuditEntry> q = rows;
        if (!string.IsNullOrWhiteSpace(toolFilter))
        {
            q = q.Where(e => string.Equals(e.Tool, toolFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (successFilter.HasValue)
        {
            q = q.Where(e => e.Success == successFilter.Value);
        }

        var list = q.ToList();
        result.MatchedEntries = list.Count;
        result.Entries = list.Skip(Math.Max(0, skip)).Take(limit).ToList();
        result.Truncated = list.Count - Math.Max(0, skip) > limit;
        return result;
    }
}

/// <summary>D-062 · 审计行（jsonl 单行结构）。</summary>
public sealed class GpAuditEntry
{
    /// <summary>调用时间 UTC ISO-8601。</summary>
    public string TimestampUtc { get; set; } = string.Empty;

    public string Tool { get; set; } = string.Empty;

    /// <summary>参数摘要（键=值[截断]；不含值体原文以控制体积，完整参数以请求侧为准）。</summary>
    public string ParameterDigest { get; set; } = string.Empty;

    /// <summary>传参形态（named/positional）。</summary>
    public string ParameterForm { get; set; } = string.Empty;

    public bool Destructive { get; set; }

    public bool Confirm { get; set; }

    public bool Success { get; set; }

    /// <summary>结果码（成功 "OK"；失败为错误码）。</summary>
    public string ResultCode { get; set; } = string.Empty;

    public long DurationMs { get; set; }

    public string? AuditNote { get; set; }

    /// <summary>进程号（调用方追溯）。</summary>
    public int Pid { get; set; }

    /// <summary>D-073：数据文件夹工作流 jobId（逐项留证关联；旧行为 null）。</summary>
    public string? JobId { get; set; }
}

/// <summary>D-062 · get_audit_log 查询结果。</summary>
public sealed class GpAuditQueryResult
{
    public string Path { get; set; } = string.Empty;

    /// <summary>审计文件不存在 → false（**优雅空结果，不抛错**）。</summary>
    public bool FileExists { get; set; } = true;

    public long TotalEntries { get; set; }

    public long MatchedEntries { get; set; }

    public IReadOnlyList<GpAuditEntry> Entries { get; set; } = Array.Empty<GpAuditEntry>();

    public bool Truncated { get; set; }

    /// <summary>损坏行计数（跳过不抛错）；-1 = 读取本身失败（Error 携带原因）。</summary>
    public long CorruptLines { get; set; }

    public string? Error { get; set; }
}
