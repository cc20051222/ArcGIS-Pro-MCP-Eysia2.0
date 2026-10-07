using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>D-043：设置/清除图层定义查询（写 · Native · 零 GP）。</summary>
public sealed class SetDefinitionQueryTool : McpToolBase
{
    public override string Name => "set_definition_query";
    public override string Description =>
        "设置或清除要素图层的定义查询（**写操作：就地修改、不可回退，调用方自行备份**）。" +
        "参数：mapName（可省略 = 活动地图，G-22）、layerName、definitionQuery（**空串/省略 = 清除**，非错误）。" +
        "语义与 get_definition_query 三态严格对齐（G-82-C）：设置 → 存储并**读回逐字返回**；清除 → 空串；" +
        "不支持定义查询的图层类型（如 GroupLayer）→ INVALID_ARGUMENT（与 get 的 supportsDefinitionQuery=false 同义）。" +
        "重名 → AMBIGUOUS_LAYER_NAME；不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
            ["definitionQuery"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Where clause; empty or omitted clears the definition query." },
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

        var definitionQuery = ToolArgs.GetString(context, "definitionQuery");
        var r = await context.Host!.Layers.SetDefinitionQueryAsync(mapName, layerName, definitionQuery, context.CancellationToken)
            .ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>D-043：调整图层顺序（写 · Native · 零 GP；根容器语义）。</summary>
public sealed class MoveLayerTool : McpToolBase
{
    public override string Name => "move_layer";
    public override string Description =>
        "调整图层在地图 TOC **根容器**中的顺序（**写操作：就地修改**）。" +
        "参数：mapName（可省略 = 活动地图）、layerName、position（TOP/BOTTOM/BEFORE/AFTER，大小写不敏感）、" +
        "referenceLayer（BEFORE/AFTER 必填；缺 → INVALID_ARGUMENT）。" +
        "**范围披露**：仅支持根容器；嵌套/组内图层的移动不在本批语义（→ INVALID_ARGUMENT 显式报错，不做静默跨层级）。" +
        "**可复原性（O-D043-02 收口）**：本操作**可在工程内就地复原**（再次 move_layer 把该层移回原位即可），" +
        "仍建议调用方在写入前记录前照（get_layers 的 rootLayerOrder）。" +
        "**位置语义（O-D043-04 修正后）**：BEFORE/AFTER 均相对**参考层**定位（源层位于参考层之前时已按" +
        "Map.MoveLayer「先移除后插入」语义做索引补偿，不再多移 1 位）；position 指向源层自身（referenceLayer == layerName）时为**幂等空操作**（顺序不变）。" +
        "返回 rootLayerOrder（写入后读回顺序）+ targetIndex，可与 get_layers 前后对照证明。重名 → AMBIGUOUS_LAYER_NAME + 候选列全；" +
        "不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer to move." },
            ["position"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "TOP | BOTTOM | BEFORE | AFTER." },
            ["referenceLayer"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Reference layer name; required for BEFORE/AFTER." },
        },
        ["required"] = new[] { "layerName", "position" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var mapName = ToolArgs.GetString(context, "mapName");
        var layerName = ToolArgs.GetString(context, "layerName");
        var position = ToolArgs.GetString(context, "position");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        if (string.IsNullOrWhiteSpace(position))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "position is required.");
        }

        var pos = position.Trim().ToUpperInvariant();
        if (pos is not ("TOP" or "BOTTOM" or "BEFORE" or "AFTER"))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, "position must be one of: TOP, BOTTOM, BEFORE, AFTER.");
        }

        var referenceLayer = ToolArgs.GetString(context, "referenceLayer");
        if (pos is "BEFORE" or "AFTER" && string.IsNullOrWhiteSpace(referenceLayer))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, $"referenceLayer is required when position is {pos}.");
        }
        var r = await context.Host!.Layers.MoveLayerAsync(mapName, layerName, referenceLayer, position, context.CancellationToken)
            .ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>D-043：设置地图范围（写 · Native · 零 GP；写入面 = Map.SetCustomFullExtent）。</summary>
public sealed class SetMapExtentTool : McpToolBase
{
    public override string Name => "set_map_extent";
    public override string Description =>
        "设置地图范围（**写操作：就地修改、调用方自行备份**）。参数：mapName（可省略 = 活动地图）、" +
        "xMin/yMin/xMax/yMax（有限数且 xMin<xMax、yMin<yMax）、spatialReference（可选：**WKID 整数**或名称/WKT 字符串；" +
        "省略 = 沿用地图自身 SR；不可解析 → INVALID_ARGUMENT）。**★ D-052 O-D043-01 修复（类型面，允许的 schema 修复）**：" +
        "schema 由 `\"string\"` 扩为 `[\"string\",\"integer\"]` 并**真实接受整数 WKID** —— 修复前整数形态被工具层静默忽略（回落地图自身 SR）；" +
        "非整数值的数字 → INVALID_ARGUMENT。**★ 同源披露（O-D042-01 延伸）**：" +
        "写入面 = Map.SetCustomFullExtent（custom full extent；SDK 无 SetDefaultExtent），" +
        "而 get_map_extent 口径 = Map.GetDefaultExtent（default full extent）；" +
        "返回值含 readBack* 四至 + readBackSource + **sameSource** 判定（写入后立即以 get 同源口径读回，如实披露，不伪造）。" +
        "不写活动视图 Camera（规避 MG3′，与 D-042 一致）。地图不存在 → MAP_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["xMin"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["yMin"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["xMax"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["yMax"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["spatialReference"] = new Dictionary<string, object?> { ["type"] = new object?[] { "string", "integer" }, ["description"] = "WKID (integer) or name/WKT string. D-052: integer WKID is accepted and forwarded as its invariant string form." },
        },
        ["required"] = new[] { "xMin", "yMin", "xMax", "yMax" }
    };
    protected override string CategoryName => ToolCategories.Map;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var mapName = ToolArgs.GetString(context, "mapName");
        var xMin = ToolArgs.GetDouble(context, "xMin");
        var yMin = ToolArgs.GetDouble(context, "yMin");
        var xMax = ToolArgs.GetDouble(context, "xMax");
        var yMax = ToolArgs.GetDouble(context, "yMax");
        if (xMin is null || yMin is null || xMax is null || yMax is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "xMin, yMin, xMax and yMax are required numbers.");
        }

        if (!(xMin.Value < xMax.Value) || !(yMin.Value < yMax.Value)
            || double.IsNaN(xMin.Value) || double.IsNaN(yMin.Value)
            || double.IsNaN(xMax.Value) || double.IsNaN(yMax.Value))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "Extent is invalid: finite numbers with xMin < xMax and yMin < yMax are required.");
        }

        // ★ D-052 O-D043-01 修复：整数 WKID 此前被 GetString 静默忽略（只认 string → JSON 数字形态丢失，
        // 回落地图自身 SR，违背 Description 承诺）。现按 schema ["string","integer"] 真实接受：
        // int/long → 不变文化字符串；double 仅接受整数值；小数/其他类型 → INVALID_ARGUMENT。
        string? sr;
        object? srRaw = null;
        var srProvided = context.Arguments is not null && context.Arguments.TryGetValue("spatialReference", out srRaw) && srRaw is not null;
        if (!srProvided)
        {
            sr = null;   // 省略 = 沿用地图自身 SR（契约不变）
        }
        else if (srRaw is string srString)
        {
            sr = srString;
        }
        else if (srRaw is int || srRaw is long)
        {
            sr = Convert.ToInt64(srRaw, System.Globalization.CultureInfo.InvariantCulture)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (srRaw is double srDouble)
        {
            if (double.IsNaN(srDouble) || double.IsInfinity(srDouble) || srDouble != Math.Floor(srDouble))
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                    "spatialReference must be an integer WKID or a name/WKT string; fractional or non-finite numbers are not valid.");
            }

            sr = ((long)srDouble).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "spatialReference must be an integer WKID or a name/WKT string.");
        }

        var r = await context.Host!.Maps.SetMapExtentAsync(mapName, xMin.Value, yMin.Value, xMax.Value, yMax.Value, sr, context.CancellationToken)
            .ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}
