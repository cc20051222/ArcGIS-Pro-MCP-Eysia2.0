namespace ArcGISProMCP.Core.Models;

/// <summary>地理处理结果（纯数据）。</summary>
public sealed class GeoprocessingResult
{
    public string ToolName { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public IReadOnlyList<string> Messages { get; set; } = Array.Empty<string>();

    /// <summary>Phase 8.4（D-016）：输出文件前后状态证明 JSON（before/after/verdict）。</summary>
    public string? StateProof { get; set; }

    /// <summary>
    /// D-026 F8：显式覆写（<c>overwrite=true</c> 且输出原已存在）时的审计提示。
    /// 仅此情形非空；fresh 执行与拒绝路径为 <c>null</c>（加字段=向后兼容扩展）。
    /// </summary>
    public string? OverwriteNote { get; set; }
}
