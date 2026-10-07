using System.Globalization;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// 共享图层解析（D-022 / F1 收敛）：Attribute / Layer / Selection / Geoprocessing 四处 Service 的
/// "按名取图层"收敛到本类，禁止再各自 <c>FindLayer(...) ?? FirstOrDefault(...)</c>。
/// </summary>
/// <remarks>
/// 缺陷背景（D-019 F1 + GATE-D021 §5 K2/K2b）：历史写法在含同名图层的工程中**静默取第一个匹配**，
/// 与 <c>get_layer_info</c> 的歧义语义及 8.1/C-10「歧义不得静默取第一」冲突；Keeper 实测影响面
/// 为 4 站点 / ≥6 工具（<c>query_attributes</c>/<c>get_field_info</c>/<c>get_feature_count</c>/
/// <c>set_layer_visibility</c>/<c>remove_layer</c>/<c>select_layer</c>）。
///
/// 名称匹配的纯逻辑在 <see cref="LayerNameMatcher"/>（Shared/Core，无 SDK 依赖，可单测——Rule 5）；
/// 本类只负责 SDK 枚举与结果组装，**必须在 QueuedTask（MCT）内调用**（Rule 4）。
///
/// 语义（不得回退）：
/// ① 0 命中 → <see cref="LayerResolveStatus.NotFound"/>（调用方报 <c>LAYER_NOT_FOUND</c>）；
/// ② 1 命中 → 正常执行；
/// ③ &gt;1 命中 → <see cref="LayerResolveStatus.Ambiguous"/> + 候选 id（调用方报
///    <c>AMBIGUOUS_LAYER_NAME</c>，**不得**误报 <c>AMBIGUOUS_MAP_NAME</c>——K2b）；
/// ④ 枚举方式由 <c>flatten</c> 决定（默认 <c>true</c>，与 <c>get_layers</c>/<c>get_layer_info</c>
///    的 G-06 默认展开一致）。
/// </remarks>
public static class LayerResolver
{
    /// <summary>拿不到 <c>Layer.URI</c> 时的稳定可区分标识前缀（序号占位，保证计数与候选不折叠）。</summary>
    private const string IndexIdPrefix = "#";

    /// <summary>
    /// 图层枚举：<paramref name="flatten"/> 为 <c>true</c> 时展开组合图层
    /// （<c>Map.GetLayersAsFlattenedList()</c>，含嵌套子图层）；为 <c>false</c> 时保留层级
    /// （<c>Map.Layers</c>，仅顶层）。语义依据官方 ProConcepts Map Authoring（与 G-06 同源）。
    /// </summary>
    public static IReadOnlyList<Layer> EnumerateLayers(Map map, bool flatten)
    {
        IEnumerable<Layer> layers = flatten
            ? map.GetLayersAsFlattenedList()
            : map.Layers;
        return (layers ?? Enumerable.Empty<Layer>()).ToList();
    }

    /// <summary>
    /// 在地图内按名解析图层（三分支：0 / 1 / &gt;1）。必须在 QueuedTask（MCT）内调用。
    /// </summary>
    public static LayerResolveResult Resolve(Map map, string layerName, bool flatten = true)
    {
        if (map is null)
        {
            return new LayerResolveResult { Status = LayerResolveStatus.NotFound };
        }

        var layers = EnumerateLayers(map, flatten);
        var pairs = new List<KeyValuePair<string, string>>(layers.Count);
        for (var i = 0; i < layers.Count; i++)
        {
            var uri = layers[i].URI?.ToString() ?? string.Empty;
            var id = uri.Length > 0 ? uri : IndexIdPrefix + i.ToString(CultureInfo.InvariantCulture);
            pairs.Add(new KeyValuePair<string, string>(layers[i].Name ?? string.Empty, id));
        }

        var (status, matchedId, candidates) = LayerNameMatcher.MatchByName(layerName, pairs);
        if (status == LayerNameMatchStatus.Ambiguous)
        {
            return new LayerResolveResult { Status = LayerResolveStatus.Ambiguous, CandidateIds = candidates };
        }

        if (status != LayerNameMatchStatus.Ok || matchedId is null)
        {
            return new LayerResolveResult { Status = LayerResolveStatus.NotFound };
        }

        var index = pairs.FindIndex(p => string.Equals(p.Value, matchedId, StringComparison.Ordinal));
        return index < 0
            ? new LayerResolveResult { Status = LayerResolveStatus.NotFound }
            : new LayerResolveResult { Status = LayerResolveStatus.Ok, Layer = layers[index] };
    }

    /// <summary>
    /// 把解析结果转换为 OperationResult 失败值（歧义 → <c>AMBIGUOUS_LAYER_NAME</c> + 候选 id；
    /// 未找到 → <c>LAYER_NOT_FOUND</c>）。<see cref="LayerResolveStatus.Ok"/> 且 Layer 非 null 时
    /// 返回 <c>null</c>（调用方继续成功路径）。
    /// </summary>
    public static OperationResult<T>? FailIfNotOk<T>(LayerResolveResult result, string layerName)
    {
        if (result.Status == LayerResolveStatus.Ok && result.Layer is not null)
        {
            return null;
        }

        if (result.Status == LayerResolveStatus.Ambiguous)
        {
            var hint = result.CandidateIds.Count > 0
                ? " Candidate layer ids: " + string.Join(" | ", result.CandidateIds)
                : string.Empty;
            return OperationResult<T>.Fail(
                ErrorCodes.AmbiguousLayerName,
                $"Layer name '{layerName}' is ambiguous ({result.CandidateIds.Count} matches).{hint}");
        }

        return OperationResult<T>.Fail(ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
    }

    /// <summary>
    /// 旧式 (Layer?, error) 出口，供 GeoprocessingService 复用（保持其既有"顶层枚举"语义）。
    /// <paramref name="error"/> 为 <c>null</c> 表示未找到，非 <c>null</c> 表示歧义。
    /// </summary>
    public static Layer? ResolveExact(Map map, string layerName, out string? error, bool flatten = false)
    {
        error = null;
        var result = Resolve(map, layerName, flatten);
        if (result.Status == LayerResolveStatus.Ambiguous)
        {
            var uris = result.CandidateIds.Count > 0
                ? " Candidate layer ids: " + string.Join(" | ", result.CandidateIds)
                : string.Empty;
            error = $"Layer name '{layerName}' is ambiguous ({result.CandidateIds.Count} matches).{uris}";
            return null;
        }

        return result.Layer;
    }
}

/// <summary>图层解析状态（SDK 侧；名称匹配状态见 Core 的 <see cref="LayerNameMatchStatus"/>）。</summary>
public enum LayerResolveStatus
{
    /// <summary>命中唯一图层，<see cref="LayerResolveResult.Layer"/> 可用。</summary>
    Ok,

    /// <summary>按名未找到任何图层（含空白图层名）。</summary>
    NotFound,

    /// <summary>按名命中多个同名图层（重名）；候选 id 见 <see cref="LayerResolveResult.CandidateIds"/>。</summary>
    Ambiguous
}

/// <summary><see cref="LayerResolver.Resolve(Map, string, bool)"/> 的结果：图层实例与歧义候选 id。</summary>
public sealed class LayerResolveResult
{
    public LayerResolveStatus Status { get; init; }

    /// <summary>解析到的图层；仅 <see cref="LayerResolveStatus.Ok"/> 时非 null。</summary>
    public Layer? Layer { get; init; }

    /// <summary>
    /// 歧义时的候选 id：优先 <c>Layer.URI</c>；无法取得时为 <c>#序号</c> 稳定占位标识。
    /// </summary>
    public IReadOnlyList<string> CandidateIds { get; init; } = Array.Empty<string>();
}
