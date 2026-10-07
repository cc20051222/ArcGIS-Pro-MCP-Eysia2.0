namespace ArcGISProMCP.Core.Models;

/// <summary>
/// D-061：工作区上下文状态。<c>Set=false</c> 表示**尚未设定**（不报错），
/// 由 <c>get_workspace</c> 如实返回空字符串而非伪造默认值。
/// </summary>
public sealed class WorkspaceInfo
{
    /// <summary>当前工作区路径（规范化后）；未设定 = 空字符串。</summary>
    public string WorkspacePath { get; set; } = string.Empty;

    /// <summary>是否已显式设定过（区别于"设定为空值"）。</summary>
    public bool Set { get; set; }
}
