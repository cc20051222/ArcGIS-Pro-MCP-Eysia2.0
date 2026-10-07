using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>列出工程中的所有地图。</summary>
public sealed class ListMapsTool : McpToolBase
{
    public override string Name => "list_maps";
    public override string Description => "列出当前 ArcGIS Pro 工程中的所有地图（地图/场景）。适合在操作前了解可用地图。";
    public override IReadOnlyDictionary<string, object?> InputSchema => Core.Tools.ToolSchemas.Object();
    protected override string CategoryName => ToolCategories.Map;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var r = await context.Host.Maps.GetMapsAsync(context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>获取指定地图（默认当前）的信息。</summary>
public sealed class GetMapInfoTool : McpToolBase
{
    public override string Name => "get_map_info";
    public override string Description => "返回指定地图的信息（名称/稳定标识 id/种类 kind/URI/是否活动）。省略 mapName 时返回**当前活动地图**；名称匹配到多个地图时返回 AMBIGUOUS_MAP_NAME（不会静默取第一个）。参数：mapName（可选）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional map name. When omitted, the current active map is used." }
        }
    };
    protected override string CategoryName => ToolCategories.Map;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var mapName = ToolArgs.GetString(context, "mapName");

        // C-05a：省略 mapName 时必须返回**活动地图**，禁止取列表首项。
        // 无活动视图时由 Service 返回 NO_ACTIVE_VIEW，不再回退（C-06）。
        if (string.IsNullOrWhiteSpace(mapName))
        {
            var current = await context.Host.Maps.GetCurrentMapAsync(context.CancellationToken).ConfigureAwait(false);
            return ToolResult.From(current);
        }

        var r = await context.Host.Maps.GetMapsAsync(context.CancellationToken).ConfigureAwait(false);
        if (!r.Success)
        {
            return ToolResult.From(r);
        }

        var matches = r.Data?
            .Where(m => string.Equals(m.Name, mapName, StringComparison.OrdinalIgnoreCase))
            .ToList() ?? new List<MapInfo>();

        if (matches.Count == 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.MapNotFound, $"Map '{mapName}' not found.");
        }

        // C-05b：重名歧义不得静默取第一个，返回候选稳定标识。
        if (matches.Count > 1)
        {
            var ids = matches
                .Select(m => string.IsNullOrWhiteSpace(m.Id) ? m.Name : m.Id)
                .ToList();
            return OperationResult<object?>.Fail(
                ErrorCodes.AmbiguousMapName,
                $"Map name '{mapName}' is ambiguous ({matches.Count} matches). Candidate ids: {string.Join(" | ", ids)}");
        }

        return OperationResult<object?>.Ok(matches[0]);
    }
}
