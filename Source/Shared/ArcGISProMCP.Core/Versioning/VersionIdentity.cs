using System;

namespace ArcGISProMCP.Core.Versioning;

/// <summary>
/// D-070（P-23）：目标框架短名映射 —— 运行程序集 <c>TargetFrameworkAttribute.FrameworkName</c> → 对外短名。
/// 映射表：<c>.NETCoreApp,Version=v6.0</c>（net6.0-windows）→ <c>"net6.0"</c>；
///         <c>.NETCoreApp,Version=v8.0</c>（net8.0-windows）→ <c>"net8.0"</c>；
/// 未知/空 → <c>"net6.0"</c>（保守兜底，net6 世代语义不变）。
/// 纯逻辑（Rule 5 无 SDK）；供 VersionService 与双口径单测使用。
/// </summary>
public static class VersionIdentity
{
    public static string MapTargetFramework(string? frameworkName)
    {
        // 双形态判定：TFM 短名（net8.0-windows）与特性名（.NETCoreApp,Version=v8.0）均归 net8.0
        if (!string.IsNullOrWhiteSpace(frameworkName) &&
            (frameworkName.Contains("net8.0", StringComparison.OrdinalIgnoreCase) ||
             frameworkName.Contains("v8.0", StringComparison.OrdinalIgnoreCase)))
        {
            return "net8.0";
        }

        return "net6.0";
    }
}
