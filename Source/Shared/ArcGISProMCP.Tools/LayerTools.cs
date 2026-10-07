using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>获取指定图层的详细信息。</summary>
public sealed class GetLayerInfoTool : McpToolBase
{
    public override string Name => "get_layer_info";
    public override string Description => "返回指定地图中某图层的详细信息（名称/URI/类型/可见性）。layerName 为必填；名称匹配到多个图层时返回 AMBIGUOUS_LAYER_NAME（不会静默取第一个）。参数：mapName、layerName。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name. When empty uses current map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name. Required." }
        },
        // C-10：schema 原先未声明 required，而实现要求 layerName，属契约不自洽；此处补齐。
        ["required"] = new[] { "layerName" }
    };
    protected override string CategoryName => ToolCategories.Layer;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var mapName = ToolArgs.GetString(context, "mapName") ?? string.Empty;
        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var r = await context.Host.Layers.GetLayerInfoAsync(mapName, layerName, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>设置图层可见性。</summary>
public sealed class SetLayerVisibilityTool : McpToolBase
{
    public override string Name => "set_layer_visibility";
    public override string Description => "设置指定图层是否可见。参数：mapName、layerName、visible(bool)。契约：重名图层返回 AMBIGUOUS_LAYER_NAME 且不产生任何状态变更（不静默作用于第一个匹配层）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["visible"] = new Dictionary<string, object?> { ["type"] = "boolean" }
        },
        ["required"] = new[] { "layerName", "visible" }
    };
    protected override string CategoryName => ToolCategories.Layer;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var layerName = ToolArgs.GetString(context, "layerName");
        var visible = ToolArgs.GetBool(context, "visible");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var r = await context.Host.Layers.SetLayerVisibilityAsync(ToolArgs.GetString(context, "mapName") ?? string.Empty, layerName, visible, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>向地图添加图层（按数据集路径/URI）。</summary>
public sealed class AddLayerTool : McpToolBase
{
    public override string Name => "add_layer";
    public override string Description => "向指定地图添加一个图层。参数：mapName、layerPathOrUri（数据集路径）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["layerPathOrUri"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Dataset path or URI of the layer to add." }
        },
        ["required"] = new[] { "layerPathOrUri" }
    };
    protected override string CategoryName => ToolCategories.Layer;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var path = ToolArgs.GetString(context, "layerPathOrUri");
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerPathOrUri is required.");
        }

        var qualification = await RasterQualificationGate.ValidatePathsAsync(
            context.Python, new[] { path }, context.CancellationToken).ConfigureAwait(false);
        if (!qualification.Success)
        {
            return OperationResult<object?>.Fail(qualification.Errors!);
        }

        var r = await context.Host.Layers.AddLayerAsync(ToolArgs.GetString(context, "mapName") ?? string.Empty, path, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>从地图移除图层。</summary>
public sealed class RemoveLayerTool : McpToolBase
{
    public override string Name => "remove_layer";
    public override string Description => "从指定地图移除一个图层。参数：mapName、layerName。契约：重名图层返回 AMBIGUOUS_LAYER_NAME 且不移除任何图层（不静默移除第一个匹配层）。";
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
    protected override string CategoryName => ToolCategories.Layer;

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

        var r = await context.Host.Layers.RemoveLayerAsync(ToolArgs.GetString(context, "mapName") ?? string.Empty, layerName, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}
