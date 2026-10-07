using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>清除当前地图选择集。</summary>
public sealed class ClearSelectionTool : McpToolBase
{
    public override string Name => "clear_selection";
    public override string Description => "清除当前（或指定）地图中的所有要素选择。参数：mapName（可选）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string" }
        }
    };
    protected override string CategoryName => ToolCategories.Selection;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var r = await context.Host.Selection.ClearSelectionAsync(ToolArgs.GetString(context, "mapName"), context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>选择（高亮）指定图层。</summary>
public sealed class SelectLayerTool : McpToolBase
{
    public override string Name => "select_layer";
    public override string Description => "在活动视图中选择（高亮）指定图层。参数：mapName、layerName。契约：重名图层返回 AMBIGUOUS_LAYER_NAME 且不改变选择状态（不静默高亮第一个匹配层）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string" }
        },
        ["required"] = new[] { "layerName" }
    };
    protected override string CategoryName => ToolCategories.Selection;

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

        var r = await context.Host.Selection.SelectLayerAsync(ToolArgs.GetString(context, "mapName") ?? string.Empty, layerName, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}
