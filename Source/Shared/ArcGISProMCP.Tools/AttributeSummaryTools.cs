using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// D-062 · C 段：统计聚合（2 件，只读；回答「这字段什么分布」「每类多少」不落 GP）。
/// 已知答案断言（fixture 预置）见 D062SummaryTests。
/// </summary>
public sealed class GetFieldStatisticsTool : McpToolBase
{
    public override string Name => "get_field_statistics";

    public override string Description =>
        "**数值字段统计**（min/max/mean/median/sum/stddev/count；只读、不落 GP）。参数：layerName（必填）、" +
        "fieldName（必填，数值字段）、mapName（可选）、where（可选过滤）。" +
        "口径披露：① 空值/非数值行**跳过**并以 skipped 计数披露（不臆测为零）；② stddev = **样本**标准差（n-1；" +
        "n<2 → null）；③ median 为排序中位（偶数取均值）；④ 扫描上限 100 万行，超出 → INVALID_ARGUMENT（提示加 where 收窄）。" +
        "非数值字段 → INVALID_ARGUMENT；字段不存在 → INVALID_ARGUMENT（消息含字段名）；图层断链 → LAYER_DATA_SOURCE_UNAVAILABLE。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer (or table) name." },
            ["fieldName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Numeric field name." },
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional map name (default: active map)." },
            ["where"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional ArcGIS SQL where filter." },
        },
        ["required"] = new[] { "layerName", "fieldName" },
    };

    protected override string CategoryName => ToolCategories.Attribute;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var layerName = ToolArgs.GetString(context, "layerName");
        var fieldName = ToolArgs.GetString(context, "fieldName");
        if (string.IsNullOrWhiteSpace(layerName) || string.IsNullOrWhiteSpace(fieldName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName and fieldName are required.");
        }

        var result = await context.Host.Attributes.GetFieldStatisticsAsync(
            ToolArgs.GetString(context, "mapName"), layerName.Trim(), fieldName.Trim(),
            ToolArgs.GetString(context, "where"), context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }

    protected override string ExecutionTypeName => ExecutionTypes.Native;
}

/// <summary>C2 · summarize_features（group-by 聚合；只读、不落 GP）。</summary>
public sealed class SummarizeFeaturesTool : McpToolBase
{
    public override string Name => "summarize_features";

    public override string Description =>
        "**分组聚合**（group-by 一/多字段 + count/sum/min/max/mean/first/last + topN；只读、不落 GP —— 回答「每类多少」）。" +
        "参数：layerName（必填）、groupByFields（必填，字段名数组，1 个以上）、aggField（sum/min/max/mean 时必填，数值字段）、" +
        "aggregations（可选数组，默认 [\"count\"]；合法值 count/sum/min/max/mean/first/last）、where（可选）、" +
        "mapName（可选）、topN（可选，按 count 降序截断前 N 组）。" +
        "口径披露：① mean 分母 = 该组内可解析数值行数（空值不计，与 C1 一致）；② first/last 为遍历序首/末非空值；" +
        "③ 行按 count 降序；④ 组数上限 10000，超出 → INVALID_ARGUMENT；⑤ totalGroups 给出真实组数（topN 截断时与返回行数不同，truncatedAt 披露）。" +
        "未知聚合名/未知字段 → INVALID_ARGUMENT；图层断链 → LAYER_DATA_SOURCE_UNAVAILABLE。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer (or table) name." },
            ["groupByFields"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "string" }, ["description"] = "Group-by field names (at least one)." },
            ["aggField"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Numeric aggregation field (required for sum/min/max/mean)." },
            ["aggregations"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "string" }, ["description"] = "Aggregations: count/sum/min/max/mean/first/last (default [count])." },
            ["where"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional ArcGIS SQL where filter." },
            ["topN"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Optional: keep top-N groups by count (desc)." },
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional map name." },
        },
        ["required"] = new[] { "layerName", "groupByFields" },
    };

    protected override string CategoryName => ToolCategories.Attribute;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        IReadOnlyList<string> groupBy = Array.Empty<string>();
        if (context.Arguments is not null && context.Arguments.TryGetValue("groupByFields", out var gbRaw))
        {
            var list = JsonHelpers.ToStringList(gbRaw);
            if (list is null || list.Count == 0)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "groupByFields must be a non-empty array of field names.");
            }

            groupBy = list;
        }

        if (groupBy.Count == 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "groupByFields (at least one) is required.");
        }

        IReadOnlyList<string> aggs = Array.Empty<string>();
        if (context.Arguments is not null && context.Arguments.TryGetValue("aggregations", out var aggsRaw))
        {
            var list = JsonHelpers.ToStringList(aggsRaw);
            if (list is null)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "aggregations must be an array of strings.");
            }

            aggs = list;
        }

        var result = await context.Host.Attributes.SummarizeFeaturesAsync(
            ToolArgs.GetString(context, "mapName"), layerName.Trim(), groupBy,
            ToolArgs.GetString(context, "aggField"), aggs,
            ToolArgs.GetString(context, "where"), ToolArgs.GetInt(context, "topN") ?? 0,
            context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }

    protected override string ExecutionTypeName => ExecutionTypes.Native;
}
