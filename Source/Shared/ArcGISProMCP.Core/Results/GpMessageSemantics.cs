namespace ArcGISProMCP.Core.Results;

/// <summary>
/// GP 消息语义判读（D-037 F-D036-2；纯逻辑，无 SDK、无 IO）。
/// <para>
/// 背景：`AddField_management` 对**已存在**字段名只发 <c>WARNING 000012</c>（<c>IsFailed=false</c>），
/// 旧实现把"GP 未失败"直接当作成功 → 契约承诺的"错误如实"未达成（LIVE 实测：isError=false）。
/// 本类把 GP 消息流中的"目标已存在"语义显式识别出来，供工具层升级为显式错误。
/// </para>
/// 判据（保守、可解释）：命中 GP 码 <c>000012</c>（ArcGIS "already exists" 标准码），
/// 或命中 <c>WARNING</c> + （<c>already exists</c> / <c>已存在</c>）组合。
/// </summary>
public static class GpMessageSemantics
{
    /// <summary>GP 码 000012：目标（字段/表/要素类…）已存在。</summary>
    public const string AlreadyExistsCode = "000012";

    /// <summary>在 GP 消息流中查找"目标已存在"语义的第一条消息；未命中 → null。</summary>
    public static string? FindPreExistingMemberMessage(IEnumerable<string>? messages)
    {
        if (messages is null)
        {
            return null;
        }

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            if (message.Contains(AlreadyExistsCode, StringComparison.Ordinal)
                || (message.Contains("WARNING", StringComparison.OrdinalIgnoreCase)
                    && (message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("已存在", StringComparison.Ordinal))))
            {
                return message;
            }
        }

        return null;
    }

    /// <summary>消息流是否含"目标已存在"语义。</summary>
    public static bool IndicatesPreExistingMember(IEnumerable<string>? messages)
        => FindPreExistingMemberMessage(messages) is not null;
}
