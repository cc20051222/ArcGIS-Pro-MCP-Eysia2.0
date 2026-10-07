using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>查询图层要素属性。</summary>
public sealed class QueryAttributesTool : McpToolBase
{
    public override string Name => "query_attributes";
    public override string Description => "查询指定地图中某图层的要素属性，支持 where 过滤与字段过滤，返回结构化要素记录。参数：mapName、layerName、whereClause（可选）、fieldNames（可选字符串数组）、maxFeatures（可选）。契约：重名图层返回 AMBIGUOUS_LAYER_NAME（不静默取第一个）；fieldNames 未含 OID 时服务端会自动补入 OID 字段以保证每条记录的 oid 有效；数据源无 OID 字段时 oid=-1 且 message 会显式说明。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["whereClause"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "SQL where clause, e.g. POP>100" },
            ["fieldNames"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "string" } },
            ["maxFeatures"] = new Dictionary<string, object?> { ["type"] = "integer" }
        },
        ["required"] = new[] { "layerName" }
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

        var request = new AttributeQueryRequest
        {
            MapName = ToolArgs.GetString(context, "mapName") ?? string.Empty,
            LayerName = layerName,
            WhereClause = ToolArgs.GetString(context, "whereClause"),
            MaxFeatures = ToolArgs.GetInt(context, "maxFeatures") ?? 1000
        };

        if (context.Arguments is not null && context.Arguments.TryGetValue("fieldNames", out var fn) && fn is System.Collections.IEnumerable en)
        {
            var fields = new List<string>();
            foreach (var item in en)
            {
                if (item is string s && !string.IsNullOrWhiteSpace(s))
                {
                    fields.Add(s);
                }
            }

            if (fields.Count > 0)
            {
                request.FieldNames = fields;
            }
        }

        var r = await context.Host.Attributes.QueryFeaturesAsync(request, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>获取图层字段信息。</summary>
public sealed class GetFieldInfoTool : McpToolBase
{
    public override string Name => "get_field_info";
    public override string Description => "返回指定图层的所有字段（名称/别名/类型/长度）。参数：mapName、layerName。契约：重名图层返回 AMBIGUOUS_LAYER_NAME（不静默取第一个）。";
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

        var r = await context.Host.Attributes.GetFieldInfoAsync(ToolArgs.GetString(context, "mapName") ?? string.Empty, layerName, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>获取图层要素数量。</summary>
public sealed class GetFeatureCountTool : McpToolBase
{
    public override string Name => "get_feature_count";
    public override string Description => "返回指定图层的要素数量，支持 where 过滤。参数：mapName、layerName、whereClause（可选）。契约：重名图层返回 AMBIGUOUS_LAYER_NAME（不静默取第一个）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["whereClause"] = new Dictionary<string, object?> { ["type"] = "string" }
        },
        ["required"] = new[] { "layerName" }
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

        var r = await context.Host.Attributes.GetFeatureCountAsync(ToolArgs.GetString(context, "mapName") ?? string.Empty, layerName, ToolArgs.GetString(context, "whereClause"), context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}
