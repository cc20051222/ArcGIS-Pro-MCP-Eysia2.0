using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// 共享地图解析（D-009）：Layer / Attribute / Selection 三处 Service 的 ResolveMap 收敛到本类。
/// </summary>
/// <remarks>
/// Phase 8.1 收尾（D6' 修复）：
/// 旧实现以 <c>MapFactory.Instance.CreateMapFromItem</c> 在解析路径上重复创建地图实例，导致
/// <c>get_layers</c> 等报 <c>MAP_NOT_FOUND</c>。本类改为官方"打开已有地图"姿势：
/// <c>Project.Current.GetItems&lt;MapProjectItem&gt;()</c> 经 <see cref="MapNameMatcher.MatchByName"/>
/// 按名匹配后调用 <see cref="MapProjectItem.GetMap()"/>（按需加载；Pro 3.0+；官方文档明确用于获取工程内已有地图）。
/// 名称匹配纯逻辑在 <see cref="MapNameMatcher"/>（Shared/Core，无 SDK 依赖，可单测——Rule 5）。
/// 语义保持（不得回退）：
/// ① 空白 mapName → 活动地图（G-22）；
/// ② 重名 → <see cref="MapResolveStatus.Ambiguous"/> + 候选 id，禁止静默取第一（D-008 已实证行为）；
/// ③ 名称匹配大小写不敏感（与 1.0.2 既有行为一致）。
/// <see cref="Resolve"/> 内含 SDK 调用，必须在 QueuedTask（MCT）内使用（Rule 4）。
/// </remarks>
public static class MapResolver
{
    /// <summary>
    /// 共享解析：供 Layer / Attribute / Selection Service 使用。
    /// 必须在 QueuedTask（MCT）内调用（Rule 4）。
    /// </summary>
    public static MapResolveResult Resolve(string? mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            // G-22：空白 mapName → 活动地图；无活动视图时 Map=null（调用方报 MAP_NOT_FOUND）。
            return new MapResolveResult { Status = MapResolveStatus.Ok, Map = MapView.Active?.Map };
        }

        var items = (Project.Current?.GetItems<MapProjectItem>() ?? Enumerable.Empty<MapProjectItem>()).ToList();
        var pairs = items
            .Select(i => new KeyValuePair<string, string>(i.Name ?? string.Empty, i.Path ?? i.Name ?? string.Empty))
            .ToList();

        var (status, id, candidates) = MapNameMatcher.MatchByName(mapName, pairs);
        if (status == MapNameMatchStatus.Ambiguous)
        {
            return new MapResolveResult { Status = MapResolveStatus.Ambiguous, CandidateIds = candidates };
        }

        if (status == MapNameMatchStatus.NotFound)
        {
            return new MapResolveResult { Status = MapResolveStatus.NotFound };
        }

        // GetMap()：按需加载工程内已有地图（官方姿势，无 CreateMapFromItem 的创建副作用）。
        var matched = items.FirstOrDefault(
            i => string.Equals(i.Path ?? i.Name ?? string.Empty, id, StringComparison.OrdinalIgnoreCase));
        var map = matched?.GetMap();

        return map is null
            ? new MapResolveResult { Status = MapResolveStatus.NotFound }
            : new MapResolveResult { Status = MapResolveStatus.Ok, Map = map };
    }

    /// <summary>
    /// 把解析结果转换为 OperationResult 失败值（歧义 → AMBIGUOUS_MAP_NAME + 候选 id；未找到/空 → MAP_NOT_FOUND）。
    /// <see cref="MapResolveStatus.Ok"/> 且 Map 非 null 时返回 null（调用方继续成功路径）。
    /// </summary>
    public static OperationResult<T>? FailIfNotOk<T>(MapResolveResult result, string? mapName)
    {
        if (result.Status == MapResolveStatus.Ok && result.Map is not null)
        {
            return null;
        }

        if (result.Status == MapResolveStatus.Ambiguous)
        {
            var hint = result.CandidateIds.Count > 0
                ? " Candidate ids: " + string.Join(" | ", result.CandidateIds)
                : string.Empty;
            return OperationResult<T>.Fail(
                ErrorCodes.AmbiguousMapName,
                $"Map name '{mapName}' is ambiguous ({result.CandidateIds.Count} matches).{hint}");
        }

        return OperationResult<T>.Fail(ErrorCodes.MapNotFound, "Map not found.");
    }
}

/// <summary>地图解析状态（SDK 侧；名称匹配状态见 Core 的 <see cref="MapNameMatchStatus"/>）。</summary>
public enum MapResolveStatus
{
    /// <summary>解析成功（<see cref="MapResolveResult.Map"/> 可用；空白 mapName 时可能为 null=无活动视图）。</summary>
    Ok,

    /// <summary>按名未找到任何工程地图项。</summary>
    NotFound,

    /// <summary>按名命中多个同名工程地图项（重名）；候选 id 见 <see cref="MapResolveResult.CandidateIds"/>。</summary>
    Ambiguous
}

/// <summary><see cref="MapResolver.Resolve(string?)"/> 的结果：Map 实例与歧义候选 id。</summary>
public sealed class MapResolveResult
{
    public MapResolveStatus Status { get; init; }

    /// <summary>解析到的地图；仅 <see cref="MapResolveStatus.Ok"/> 时可能非 null。</summary>
    public Map? Map { get; init; }

    /// <summary>歧义时的候选 id（工程项 Path，与 list_maps 的 <c>id</c> 字段同源）。</summary>
    public IReadOnlyList<string> CandidateIds { get; init; } = Array.Empty<string>();
}
