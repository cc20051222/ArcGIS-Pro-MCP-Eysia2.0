using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>D-046（Phase 10 第四批）：读取图层符号（只读 · Native · 零 GP）。</summary>
public sealed class GetLayerSymbologyTool : McpToolBase
{
    public override string Name => "get_layer_symbology";
    public override string Description =>
        "读取图层当前渲染器形态（只读）：renderer 类型、简单符号的填充色/描边色/点大小/线宽。" +
        "参数：mapName（可省略 = 活动地图，G-22）、layerName。" +
        "**三态可判别（G-82-C）**：非要素图层（如 GroupLayer）→ supportsSymbology=false 且符号字段为 null；" +
        "复杂渲染器（唯一值/分级/热力图等）→ isSimpleRenderer=false 且符号字段为 null（**不静默降级**）；" +
        "SimpleRenderer → isSimpleRenderer=true 且符号字段按可得性填充（不可达字段为 null）。" +
        "色值统一 `#RRGGBB` 形态（不含 alpha；底层 CIMRGBColor 三分量 0–255）；" +
        "**★ 点符号可达性披露（O-D046-05 / G-116 裁定①）**：点符号（`CIMCharacterMarker` 形态）的 " +
        "`fillColor`/`outlineColor` 恒为 **null（不可达，非未设置）**，`lineWidth` 同；仅 `pointSize` 可达；" +
        "面/线图层（`CIMSolidFill`/`CIMSolidStroke`）不受此限。" +
        "重名 → AMBIGUOUS_LAYER_NAME + 候选列全；不存在 → LAYER_NOT_FOUND；零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
        },
        ["required"] = new[] { "layerName" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var mapName = ToolArgs.GetString(context, "mapName");
        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var r = await context.Host!.Layers.GetLayerSymbologyAsync(mapName, layerName, context.CancellationToken)
            .ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>D-046（Phase 10 第四批）：设置简单符号（写 · Native · 零 GP）。</summary>
public sealed class SetSimpleSymbologyTool : McpToolBase
{
    public override string Name => "set_simple_symbology";
    public override string Description =>
        "设置图层**简单符号**属性（**写操作：就地修改渲染器；可经前照原值复原**）。" +
        "参数：mapName（可省略 = 活动地图）、layerName、fillColor（#RRGGBB 或 RRGGBB）、outlineColor、pointSize、lineWidth；" +
        "**至少提供一项**，否则 INVALID_ARGUMENT。色值非法（非 6 位十六进制）→ INVALID_ARGUMENT；点大小/线宽须 > 0。" +
        "**受理范围**：仅 SimpleRenderer 上的简单符号；目标图层为复杂渲染器（唯一值/分级/热力图等）" +
        "→ INVALID_ARGUMENT（**如实拒绝，不静默降级**）；非要素图层 → INVALID_ARGUMENT。" +
        "**不代建渲染器/符号层**：仅修改既有简单符号的层（面填充 CIMSolidFill / 描边 CIMSolidStroke / 点标记 CIMMarker.Size）。" +
        "**★ 点符号可达性披露（O-D046-05 / G-116 裁定①）**：点符号（`CIMCharacterMarker` 形态）的 " +
        "`fillColor`/`outlineColor`/`lineWidth` **不可达**（请求后 readback = null，≠ 请求值 ⇒ 调用方可判别、非静默）；" +
        "**仅 `pointSize` 可达**；面/线图层三项均可达（对照实测全绿）。" +
        "返回值为**写后读回**态（fillColor/outlineColor/pointSize/lineWidth），可与 get_layer_symbology 前后对照证明。" +
        "重名 → AMBIGUOUS_LAYER_NAME；不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
            ["fillColor"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Fill color, #RRGGBB or RRGGBB." },
            ["outlineColor"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Outline/stroke color, #RRGGBB or RRGGBB." },
            ["pointSize"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Marker size (points)." },
            ["lineWidth"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Stroke width." },
        },
        ["required"] = new[] { "layerName" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var mapName = ToolArgs.GetString(context, "mapName");
        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var fillColor = ToolArgs.GetString(context, "fillColor");
        var outlineColor = ToolArgs.GetString(context, "outlineColor");
        var pointSize = ToolArgs.GetDouble(context, "pointSize");
        var lineWidth = ToolArgs.GetDouble(context, "lineWidth");

        if (fillColor is null && outlineColor is null && pointSize is null && lineWidth is null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "at least one of fillColor / outlineColor / pointSize / lineWidth is required.");
        }

        var r = await context.Host!.Layers
            .SetSimpleSymbologyAsync(mapName, layerName, fillColor, outlineColor, pointSize, lineWidth, context.CancellationToken)
            .ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>D-046（Phase 10 第四批）：读取图层标注配置（只读 · Native · 零 GP）。</summary>
public sealed class GetLabelInfoTool : McpToolBase
{
    public override string Name => "get_label_info";
    public override string Description =>
        "读取图层标注配置（只读）：是否启用、labelClass 数、首个 labelClass 的表达式与主要字体属性。" +
        "参数：mapName（可省略 = 活动地图）、layerName。" +
        "**三态可判别（G-82-C）**：非要素图层 → supportsLabels=false；要素图层但**标注未启用** → enabled=false + " +
        "labelClassCount 如实（**非错误**）；无 labelClass → labelClassCount=0 且 expression/fontFamily 为 null。" +
        "字体属性取自首个 labelClass 的 TextSymbol（fontFamily/fontSize），不可达时为 null（如实披露）。" +
        "重名 → AMBIGUOUS_LAYER_NAME + 候选列全；不存在 → LAYER_NOT_FOUND；零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
        },
        ["required"] = new[] { "layerName" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var mapName = ToolArgs.GetString(context, "mapName");
        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var r = await context.Host!.Layers.GetLabelInfoAsync(mapName, layerName, context.CancellationToken)
            .ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>D-046（Phase 10 第四批）：标注开关（写 · Native · 零 GP）。</summary>
public sealed class SetLabelVisibilityTool : McpToolBase
{
    public override string Name => "set_label_visibility";
    public override string Description =>
        "开关图层标注显示（**写操作：就地修改；可再次调用复原**）。" +
        "参数：mapName（可省略 = 活动地图）、layerName、enabled（布尔，缺省 false）。" +
        "**不代建 labelClass**：enabled=true 且图层尚无 labelClass → INVALID_ARGUMENT（如实拒绝；" +
        "创建标注类属后续批次）；enabled=false 恒受理。非要素图层 → INVALID_ARGUMENT。" +
        "返回值为**写后读回**态（enabled + labelClassCount），可与 get_label_info 前后对照证明。" +
        "重名 → AMBIGUOUS_LAYER_NAME；不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
            ["enabled"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "true to draw labels; false to hide." },
        },
        ["required"] = new[] { "layerName", "enabled" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var mapName = ToolArgs.GetString(context, "mapName");
        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        if (context.Arguments is null || !context.Arguments.TryGetValue("enabled", out var raw) || raw is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "enabled is required.");
        }

        var enabled = ToolArgs.GetBool(context, "enabled");
        var r = await context.Host!.Layers.SetLabelVisibilityAsync(mapName, layerName, enabled, context.CancellationToken)
            .ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}
