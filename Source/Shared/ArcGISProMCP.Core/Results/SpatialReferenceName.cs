using System.Globalization;

namespace ArcGISProMCP.Core.Results;

/// <summary>预定义坐标系条目（WKID + 名称）——宿主（SDK）侧枚举，本层只做匹配。</summary>
public sealed record SpatialReferenceEntry(int Wkid, string Name);

/// <summary>
/// 坐标系名称 / WKID 解析（D-037 F-D035-2；纯逻辑，无 SDK、无 IO）。
/// <para>
/// 背景：LIVE 实测 <c>Project_management</c> 的 <c>out_coor_system</c> **仅 WKID 数值形态可解析**；
/// 名称形态（<c>WGS 1984 Web Mercator (auxiliary sphere)</c> 与下划线写法）一律 <c>ERROR 000735</c>，
/// 而契约明示"WKID 或名称"→ 契约-实现差距。修复方向：名称 → WKID 解析后再下传（WKID 已实证可用）。
/// </para>
/// 归一化：忽略大小写与**全部非字母数字字符**（空格 / 下划线 / 括号 / 连字符）→
/// <c>WGS 1984 Web Mercator (auxiliary sphere)</c>、<c>WGS_1984_Web_Mercator_Auxiliary_Sphere</c>
/// 与 SDK 预置名 <c>WGS_1984_Web_Mercator_Auxiliary_Sphere</c> 归一后同一键，确定性匹配。
/// </summary>
public static class SpatialReferenceName
{
    /// <summary>归一化键：仅保留字母与数字并转小写；空白/输入 → 空串。</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var buffer = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Append(char.ToLowerInvariant(ch));
            }
        }

        return buffer.ToString();
    }

    /// <summary>是否为纯 WKID 数值形态（正整数的十进制写法，无前导符号）。</summary>
    public static bool IsWkidLiteral(string? value, out int wkid)
    {
        wkid = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (!text.All(char.IsDigit))
        {
            return false;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out wkid) && wkid > 0;
    }

    /// <summary>
    /// 在预置条目中解析名称（归一化后**全等**优先，其次前缀容差？——不：只做全等，避免误匹配）。
    /// 命中 → true 且 <paramref name="match"/> 为对应条目。
    /// </summary>
    public static bool TryResolve(string? value, IEnumerable<SpatialReferenceEntry>? entries, out SpatialReferenceEntry? match)
    {
        match = null;
        var key = Normalize(value);
        if (key.Length == 0 || entries is null)
        {
            return false;
        }

        foreach (var entry in entries)
        {
            if (Normalize(entry.Name) == key)
            {
                match = entry;
                return true;
            }
        }

        return false;
    }
}
