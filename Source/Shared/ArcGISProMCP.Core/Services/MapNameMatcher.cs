namespace ArcGISProMCP.Core.Services;

/// <summary>按名匹配工程地图项的状态（纯逻辑，无 SDK 依赖）。</summary>
public enum MapNameMatchStatus
{
    /// <summary>匹配成功（<paramref name="MatchedId"/> 可用）；空白输入也返回此状态（语义由调用方处理为活动地图）。</summary>
    Ok,

    /// <summary>按名未找到任何匹配项。</summary>
    NotFound,

    /// <summary>按名命中多个同名项（重名）；候选 id 见候选列表。</summary>
    Ambiguous
}

/// <summary>
/// 共享的地图名称匹配纯逻辑（D-009）：供 Compatibility 的 MapResolver 使用，可在单元测试中直接验证。
/// </summary>
/// <remarks>
/// 语义（不得回退）：① 空白输入 → <see cref="MapNameMatchStatus.Ok"/> 且 MatchedId=null
/// （"空白 mapName → 活动地图"由 SDK 侧调用方处理，G-22）；
/// ② 重名 → <see cref="MapNameMatchStatus.Ambiguous"/> + 候选 id（Distinct 保序），禁止静默取第一；
/// ③ 名称匹配大小写不敏感（与 1.0.2 既有行为一致）。
/// </remarks>
public static class MapNameMatcher
{
    /// <summary>在 (名称, id) 集合中按 OrdinalIgnoreCase 查找 <paramref name="mapName"/>。</summary>
    /// <param name="mapName">目标地图名；空白视为"未指定"。</param>
    /// <param name="items">工程地图项的 (名称, id) 集合（id 与 list_maps 的 <c>id</c> 字段同源）。</param>
    /// <returns>(状态, 命中 id, 歧义候选 id 列表)。</returns>
    public static (MapNameMatchStatus Status, string? MatchedId, IReadOnlyList<string> Candidates) MatchByName(
        string? mapName,
        IEnumerable<KeyValuePair<string, string>> items)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return (MapNameMatchStatus.Ok, null, Array.Empty<string>());
        }

        var matches = items
            .Where(kv => string.Equals(kv.Key, mapName, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Value)
            .Where(v => v.Length > 0)
            .Distinct()
            .ToList();

        if (matches.Count == 0)
        {
            return (MapNameMatchStatus.NotFound, null, Array.Empty<string>());
        }

        if (matches.Count > 1)
        {
            return (MapNameMatchStatus.Ambiguous, null, matches);
        }

        return (MapNameMatchStatus.Ok, matches[0], Array.Empty<string>());
    }
}
