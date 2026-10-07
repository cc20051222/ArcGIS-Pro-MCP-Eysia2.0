using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// D-042（Phase 10 第一批）：布局与范围**只读**信息 4 工具（零写、零 GP、Native 执行）。
/// SDK 依据见 run-20260914-d042-layout-readonly/sdk-spike.md（不猜能力）。
/// </summary>
public sealed class GetLayoutInfoTool : McpToolBase
{
    public override string Name => "get_layout_info";
    public override string Description =>
        "获取单个布局详情（只读）：页面尺寸（CIMPage.Width/Height）、单位（Units 枚举名）、元素计数。" +
        "**★ 单位形态披露（O-D045-04 / G-112 裁定③）**：本工具返回的 pageUnits 为 **CIM 枚举单数形态**" +
        "（如 `Centimeter` / `Inch`），而 create_layout 自报的是**归一复数形态**（如 `Centimeters`）—— 两读面各自确定性成立、可判别；" +
        "跨工具逐字比较单位字符串时须做形态归一。" +
        "参数：layoutName。同名多布局 → AMBIGUOUS_LAYER_NAME + 候选 URI；布局不存在 → LAYER_NOT_FOUND（消息明示 Layout，错误码复用见工单披露）。" +
        "注意：只读、零写、零 GP；本工具不要求布局视图处于激活状态（CIM/工程项直读口径）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["layoutName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layout name (case-insensitive)." },
        },
        ["required"] = new[] { "layoutName" }
    };
    protected override string CategoryName => ToolCategories.Layout;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layoutName = ToolArgs.GetString(context, "layoutName");
        if (string.IsNullOrWhiteSpace(layoutName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layoutName is required.");
        }
        var r = await context.Host!.Layout.GetLayoutInfoAsync(layoutName).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }

    internal static OperationResult<object?> MapFrom(OperationResult<LayoutDetailInfo> r)
        => r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
}

/// <summary>D-042：布局元素清单（只读、展平）。</summary>
public sealed class ListLayoutElementsTool : McpToolBase
{
    public override string Name => "list_layout_elements";
    public override string Description =>
        "列出布局的全部元素（只读、展平）：Name、ElementType（运行时类型名，如 TextElement/MapFrame/GroupElement）、IsVisible、X/Y。" +
        "参数：layoutName。空布局 → 空数组（非错误）。语义边界同 get_layout_info。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["layoutName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layout name (case-insensitive)." },
        },
        ["required"] = new[] { "layoutName" }
    };
    protected override string CategoryName => ToolCategories.Layout;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layoutName = ToolArgs.GetString(context, "layoutName");
        if (string.IsNullOrWhiteSpace(layoutName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layoutName is required.");
        }
        var r = await context.Host!.Layout.ListLayoutElementsAsync(layoutName).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>D-042：地图范围（只读；口径 = Map.GetDefaultExtent 工程态，不依赖活动视图）。</summary>
public sealed class GetMapExtentTool : McpToolBase
{
    public override string Name => "get_map_extent";
    public override string Description =>
        "获取地图范围（只读）：XMin/YMin/XMax/YMax + SpatialReferenceName。**口径 = Map.GetDefaultExtent（工程态默认范围）**，" +
        "不依赖活动地图视图（MG3′ 语义：无活动视图亦可返回）；Extentsource 字段标注口径为 \"default-extent\"。" +
        "参数：mapName（省略 = 当前活动地图，G-22）。范围无法确定 → 数值为 null（显式披露而非报错）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
        },
        ["required"] = new string[] { }
    };
    protected override string CategoryName => ToolCategories.Map;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var mapName = ToolArgs.GetString(context, "mapName");
        var r = await context.Host!.Maps.GetMapExtentAsync(mapName).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }

    internal static OperationResult<object?> MapFrom(OperationResult<MapExtentInfo> r)
        => r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
}

/// <summary>D-042：图层定义查询（只读；null/空串可判别）。</summary>
public sealed class GetDefinitionQueryTool : McpToolBase
{
    public override string Name => "get_definition_query";
    public override string Description =>
        "读取图层定义查询（只读）：supportsDefinitionQuery=false（如 GroupLayer）→ definitionQuery=null；" +
        "支持但未设置 → 空串；已设置 → 查询原文（G-82-C：三种状态可判别）。参数：mapName（可省略）、layerName。" +
        "只读、零写、零 GP。";
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
        var r = await context.Host!.Layers.GetDefinitionQueryAsync(mapName, layerName).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}
