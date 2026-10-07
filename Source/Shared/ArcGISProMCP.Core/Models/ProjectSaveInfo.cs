namespace ArcGISProMCP.Core.Models;

/// <summary>
/// D-061：工程保存结果。
/// 字段均为**保存后实测**（<c>FileSizeBytes</c> / <c>LastWriteTimeUtc</c> 由文件系统读取），
/// 不臆测、不回填请求值（N1 纪律：产物事实必须实测）。
/// </summary>
public sealed class ProjectSaveInfo
{
    /// <summary>保存后的工程文件路径（原位保存 = 原路径；另存 = 新路径）。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>工程名（不含扩展名）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>true = 另存到新路径（saveAs）；false = 原位保存。</summary>
    public bool SavedAs { get; set; }

    /// <summary>保存后工程的脏标记（来自 <c>Project.IsDirty</c>，保存成功应为 false）。</summary>
    public bool IsDirty { get; set; }

    /// <summary>产物是否确已在位（存在性实测；缺失 ⇒ FAILURE，不谎报成功）。</summary>
    public bool FileExists { get; set; }

    /// <summary>产物字节数（实测）。</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>产物最后写入时间（UTC，ISO-8601 往返格式；实测）。</summary>
    public string? LastWriteTimeUtc { get; set; }
}
