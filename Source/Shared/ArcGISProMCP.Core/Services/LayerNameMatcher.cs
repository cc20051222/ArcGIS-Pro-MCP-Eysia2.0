namespace ArcGISProMCP.Core.Services;

/// <summary>按名匹配地图内图层的状态（纯逻辑，无 SDK 依赖）。</summary>
public enum LayerNameMatchStatus
{
    /// <summary>命中唯一图层（<paramref name="MatchedId"/> 可用）。</summary>
    Ok,

    /// <summary>按名未找到任何图层（含空白图层名）。</summary>
    NotFound,

    /// <summary>按名命中多个同名图层（重名）；候选 id 见候选列表。</summary>
    Ambiguous
}

/// <summary>
/// 共享的图层名称匹配纯逻辑（D-022 / F1 收敛）：供 Compatibility 的 <c>LayerResolver</c> 使用，
/// 可在单元测试中直接验证，无需 ArcGIS Pro SDK（Rule 5）。
/// </summary>
/// <remarks>
/// 背景：历史实现为 <c>map.FindLayer(name, true) ?? map.Layers.FirstOrDefault(...)</c>，
/// 在含同名图层的工程中会**静默取第一个匹配**，与 <c>get_layer_info</c> 的
/// <c>AMBIGUOUS_LAYER_NAME</c> 语义及 8.1/C-10「歧义不得静默取第一」直接冲突（D-019 F1）。
/// 本类把"枚举 → 计数 → 三分支"的判定抽为可测纯逻辑，4 个站点（Attribute / Layer×2 / Selection）
/// 全部经 SDK 侧 <c>LayerResolver</c> 复用，避免第 5 个站点复发。
///
/// 语义（不得回退）：
/// ① 空白图层名 → <see cref="LayerNameMatchStatus.NotFound"/>（无"活动图层"概念；
///    空白 layerName 由工具层以 <c>INVALID_ARGUMENT</c> 先行拒绝，与既有行为一致）；
/// ② 重名 → <see cref="LayerNameMatchStatus.Ambiguous"/> + 候选 id，禁止静默取第一；
/// ③ 名称匹配大小写不敏感（与 1.0.2 既有行为一致）。
/// </remarks>
public static class LayerNameMatcher
{
    /// <summary>在 (图层名, 图层 id) 集合中按 OrdinalIgnoreCase 查找 <paramref name="layerName"/>。</summary>
    /// <param name="layerName">目标图层名；空白视为未指定 → NotFound。</param>
    /// <param name="layers">
    /// 地图内图层的 (名称, id) 集合。id 必须是**稳定且可区分**的标识：
    /// 优先 <c>Layer.URI</c>；拿不到 URI 时由 SDK 侧调用方以序号占位符（如 <c>#3</c>）补齐，
    /// 保证计数与候选列表不因空 id 被折叠。
    /// </param>
    /// <returns>(状态, 命中 id, 歧义候选 id 列表)。</returns>
    public static (LayerNameMatchStatus Status, string? MatchedId, IReadOnlyList<string> Candidates) MatchByName(
        string? layerName,
        IEnumerable<KeyValuePair<string, string>> layers)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return (LayerNameMatchStatus.NotFound, null, Array.Empty<string>());
        }

        var matches = new List<string>();
        foreach (var kv in layers)
        {
            if (string.Equals(kv.Key, layerName, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(kv.Value);
            }
        }

        if (matches.Count == 0)
        {
            return (LayerNameMatchStatus.NotFound, null, Array.Empty<string>());
        }

        if (matches.Count > 1)
        {
            // 候选去重但计数不去重：id 重复（极端情况）不得把"重名"降为"唯一"。
            var candidates = matches.Distinct(StringComparer.Ordinal).ToList();
            return (LayerNameMatchStatus.Ambiguous, null, candidates);
        }

        return (LayerNameMatchStatus.Ok, matches[0], Array.Empty<string>());
    }
}
