using System.IO;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-061：进程内「默认工作区上下文」（供 <c>set_workspace</c> / <c>get_workspace</c> 写入与读取，
/// 并为 <c>list_rasters</c> / <c>list_tables</c> 提供缺省工作区）。
/// </summary>
/// <remarks>
/// 设计取舍（如实披露）：
/// ① 状态为**进程级静态**，随插件进程生命周期存在；Pro 重启后为空 —— 工具返回值明确标注 <c>set</c> 标志，
///    未设定与设定为空均如实区分，不伪造路径。
/// ② 路径写入前统一规范化（<see cref="TrySet"/>）：去首尾空白、去尾部目录分隔符、折叠 <c>..</c> 与重复分隔符。
/// ③ 本类不校验路径是否存在 —— 存在性是宿主/工具的职责；也不做存在性预检（工作区可在之后创建）。
/// ④ 所有访问加锁，避免并发工具调用竞态；<see cref="Reset"/> 仅供测试与显式清理。
/// </remarks>
public static class WorkspaceContext
{
    private static readonly object Sync = new();
    private static string _current = string.Empty;

    /// <summary>当前工作区（规范化后）；未设定 = 空字符串。</summary>
    public static string Current
    {
        get { lock (Sync) { return _current; } }
    }

    /// <summary>是否已设定（未经 <see cref="Reset"/> 清除且非空）。</summary>
    public static bool IsSet => Current.Length > 0;

    /// <summary>
    /// 尝试设定工作区路径。成功返回 true 并输出规范化路径；失败（空白 / 非法字符 / 无法规范化）返回 false 并给出原因。
    /// 失败时**原值保持不变**（失败关闭，不写半截状态）。
    /// </summary>
    public static bool TrySet(string? path, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "path is required.";
            return false;
        }

        var candidate = Normalize(path);
        if (candidate is null)
        {
            error = "path contains invalid characters or cannot be normalized.";
            return false;
        }

        if (candidate.Length == 0)
        {
            error = "path is required.";
            return false;
        }

        lock (Sync)
        {
            _current = candidate;
        }

        normalized = candidate;
        return true;
    }

    /// <summary>清除上下文（返回是否曾设定）。</summary>
    public static bool Reset()
    {
        lock (Sync)
        {
            var had = _current.Length > 0;
            _current = string.Empty;
            return had;
        }
    }

    /// <summary>
    /// 路径规范化：去首尾空白 → <c>Path.GetFullPath</c>（折叠 <c>..</c> / <c>.</c>）→ 去尾部目录分隔符
    /// （根目录如 <c>D:\</c> 与 UNC 根保留）。非法输入返回 null。
    /// </summary>
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(trimmed);
        }
        catch (Exception)
        {
            return null;
        }

        var result = full.TrimEnd('\\', '/');
        return result.Length == 0 ? full : result;
    }
}
