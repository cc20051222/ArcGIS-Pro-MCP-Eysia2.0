using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// D-062 · B 段：编辑栈（6 件，EditOperation 撤销栈）。
/// <b>事务铁律（工单强制）</b>：insert/update/delete **默认不自动提交** —— 编辑进撤销栈，
/// 由 save_edits / discard_edits 显式收束；跨调用编辑状态保持；get_edit_session 查询未提交状态
/// （超 Knight60 能力件）。B2/B3 破坏性（confirm 缺省拒）；B4/B5 就地写（save）/ 会话回滚（discard）。
/// </summary>
public sealed class InsertFeaturesTool : McpToolBase
{
    public override string Name => "insert_features";

    public override string Description =>
        "**向图层插入行（默认不提交）**。参数：layerName（必填）、mapName（可选，缺省活动地图）、" +
        "rows（**必填**，行字典数组：键 = 字段名，值为字符串/数值/布尔；几何以 `geometry` 键给 WKT 字符串，可选）。" +
        "事务：编辑进 EditOperation 撤销栈，**默认不提交** —— 需 save_edits 提交或 discard_edits 回滚（本契约明示）；" +
        "返回 rowsAffected 与 pendingChangeCount（会话累计）。" +
        "守卫：目标图层的底层数据集命中受保护根（TestFixtures／旧仓库／ARCGIS_PRO_MCP_PROTECTED_ROOTS）→ " +
        "`PATH_ESCAPE_REJECTED`（零变更）；图层非要素图层 → INVALID_ARGUMENT（独立表编辑本批不支持，如实披露）；" +
        "目标不存在 → LAYER_NOT_FOUND；数据源断链 → LAYER_DATA_SOURCE_UNAVAILABLE。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target feature layer name." },
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional map name (default: active map)." },
            ["rows"] = new Dictionary<string, object?>
            {
                ["type"] = "array",
                ["items"] = new Dictionary<string, object?> { ["type"] = "object" },
                ["description"] = "Row dictionaries keyed by field name; geometry via 'geometry' key (WKT).",
            },
        },
        ["required"] = new[] { "layerName", "rows" },
    };

    protected override string CategoryName => ToolCategories.DataManagement;

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

        var rows = JsonHelpers.ToRowList(
            context.Arguments is not null && context.Arguments.TryGetValue("rows", out var rowsRaw) ? rowsRaw : null);
        if (rows is null || rows.Count == 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "rows is required (non-empty array of row objects).");
        }

        var result = await context.Host.Edits.InsertFeaturesAsync(
            ToolArgs.GetString(context, "mapName"), layerName.Trim(), rows, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }

    protected override string ExecutionTypeName => ExecutionTypes.Native;
}

/// <summary>B2 · update_features（破坏性：confirm 缺省拒；默认不提交）。</summary>
public sealed class UpdateFeaturesTool : McpToolBase
{
    public override string Name => "update_features";

    public override string Description =>
        "**按 where/OID 更新要素属性或几何（破坏性：confirm 缺省拒；默认不提交）**。参数：layerName（必填）、" +
        "mapName（可选）、where（ArcGIS SQL，与 oidList 二选一）、oidList（OID 数组）、" +
        "attributes（字段字典，可选）、geometryWkt（WKT 改形，可选 —— 二者至少其一）、" +
        "confirm（**必须显式 true**，缺省或 false → INVALID_ARGUMENT 拒绝执行）。" +
        "事务：编辑进撤销栈**默认不提交**（需 save_edits / discard_edits 显式收束）；" +
        "where 无匹配 → NOT_FOUND（零变更）；匹配行超单次上限（10000）→ INVALID_ARGUMENT（披露上限）。" +
        "守卫同 insert_features（目标数据集受保护根 → PATH_ESCAPE_REJECTED，零变更）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target feature layer name." },
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional map name." },
            ["where"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "ArcGIS SQL where (or oidList; exactly one required)." },
            ["oidList"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "integer" }, ["description"] = "OID list (alternative to where)." },
            ["attributes"] = new Dictionary<string, object?> { ["type"] = "object", ["description"] = "Field-name → new-value dictionary (optional)." },
            ["geometryWkt"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional new geometry as WKT." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Must be explicitly true; otherwise refused (INVALID_ARGUMENT)." },
        },
        ["required"] = new[] { "layerName", "confirm" },
    };

    protected override string CategoryName => ToolCategories.DataManagement;

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

        // 工具层 confirm 前置拒（服务层 EditService 同判 —— 纵深防御；本层可单测）。
        if (!ToolArgs.GetBool(context, "confirm"))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "update_features is destructive: confirm=true is required (default-refuse).");
        }

        IReadOnlyDictionary<string, object?>? attributes = null;
        if (context.Arguments is not null && context.Arguments.TryGetValue("attributes", out var attrsRaw))
        {
            attributes = JsonHelpers.ToNamedDictionary(attrsRaw);
            if (attributes is null)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "attributes must be an object (field → value).");
            }
        }

        IReadOnlyList<long>? oidList = null;
        if (context.Arguments is not null && context.Arguments.TryGetValue("oidList", out var oidsRaw))
        {
            oidList = JsonHelpers.ToLongList(oidsRaw);
            if (oidList is null)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "oidList must be an array of integers.");
            }
        }

        var result = await context.Host.Edits.UpdateFeaturesAsync(
            ToolArgs.GetString(context, "mapName"), layerName.Trim(),
            ToolArgs.GetString(context, "where"), oidList,
            attributes, ToolArgs.GetString(context, "geometryWkt"),
            ToolArgs.GetBool(context, "confirm"), context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }

    protected override string ExecutionTypeName => ExecutionTypes.Native;
}

/// <summary>B3 · delete_features（破坏性：confirm 缺省拒；默认不提交）。</summary>
public sealed class DeleteFeaturesTool : McpToolBase
{
    public override string Name => "delete_features";

    public override string Description =>
        "**按 where/OID 删除要素行（破坏性：confirm 缺省拒；默认不提交）**。参数：layerName（必填）、mapName（可选）、" +
        "where（与 oidList 二选一；两者皆缺 → INVALID_ARGUMENT，**拒绝全表删除**）、oidList（OID 数组）、" +
        "confirm（**必须显式 true**，缺省或 false → INVALID_ARGUMENT）。" +
        "事务：删除进撤销栈**默认不提交** —— discard_edits 可完整回滚，save_edits 才落盘（本契约明示）；" +
        "where 无匹配 → NOT_FOUND（零变更）；超单次上限（10000）→ INVALID_ARGUMENT。" +
        "守卫：目标数据集受保护根 → PATH_ESCAPE_REJECTED（零变更）。语义区分：本工具删**要素行**；" +
        "delete_dataset 删**磁盘数据集**；remove_layer 只移除**地图引用**。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target feature layer name." },
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional map name." },
            ["where"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "ArcGIS SQL where (or oidList; exactly one required)." },
            ["oidList"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "integer" }, ["description"] = "OID list (alternative to where)." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Must be explicitly true; otherwise refused (INVALID_ARGUMENT)." },
        },
        ["required"] = new[] { "layerName", "confirm" },
    };

    protected override string CategoryName => ToolCategories.DataManagement;

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

        // 工具层 confirm 前置拒（服务层 EditService 同判 —— 纵深防御；本层可单测）。
        if (!ToolArgs.GetBool(context, "confirm"))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "delete_features is destructive: confirm=true is required (default-refuse).");
        }

        IReadOnlyList<long>? oidList = null;
        if (context.Arguments is not null && context.Arguments.TryGetValue("oidList", out var oidsRaw))
        {
            oidList = JsonHelpers.ToLongList(oidsRaw);
            if (oidList is null)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "oidList must be an array of integers.");
            }
        }

        var result = await context.Host.Edits.DeleteFeaturesAsync(
            ToolArgs.GetString(context, "mapName"), layerName.Trim(),
            ToolArgs.GetString(context, "where"), oidList,
            ToolArgs.GetBool(context, "confirm"), context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }

    protected override string ExecutionTypeName => ExecutionTypes.Native;
}

/// <summary>B4 · save_edits（提交编辑会话；就地写）。</summary>
public sealed class SaveEditsTool : McpToolBase
{
    public override string Name => "save_edits";

    public override string Description =>
        "**提交当前编辑会话**（insert/update/delete 累积的未提交编辑落盘）。无必填参数。" +
        "就地写 ⇒ 输入守卫已在会话建立时判（目标数据集受保护根的编辑根本进不了会话）。" +
        "无未提交编辑 → Ok + hasEdits=false（幂等空态，不伪造提交）；提交失败 → INTERNAL_ERROR 且编辑保持未提交。" +
        "返回 hasEdits / pendingChangeCount（归零）/ affectedLayers（清空）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>(),
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var result = await context.Host.Edits.SaveEditsAsync(context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }

    protected override string ExecutionTypeName => ExecutionTypes.Native;
}

/// <summary>B5 · discard_edits（丢弃编辑会话；非磁盘写）。</summary>
public sealed class DiscardEditsTool : McpToolBase
{
    public override string Name => "discard_edits";

    public override string Description =>
        "**丢弃当前编辑会话**（撤销栈回退：insert/update/delete 的未提交编辑全部作废；非磁盘写）。" +
        "无未提交编辑 → Ok + hasEdits=false（幂等空态）；丢弃失败 → INTERNAL_ERROR 且编辑保持未提交。" +
        "返回 hasEdits / pendingChangeCount（归零）/ affectedLayers（清空）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>(),
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var result = await context.Host.Edits.DiscardEditsAsync(context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }

    protected override string ExecutionTypeName => ExecutionTypes.Native;
}

/// <summary>B6 · get_edit_session（未提交编辑状态查询；只读；超 Knight60 能力件）。</summary>
public sealed class GetEditSessionTool : McpToolBase
{
    public override string Name => "get_edit_session";

    public override string Description =>
        "**查询当前未提交编辑状态**（计数/涉及图层；只读）。无参数。" +
        "返回：hasEdits（Project.HasEdits，**权威真值**）/ pendingChangeCount（会话跟踪计数）/ affectedLayers（跟踪图层）" +
        "/ trackingNote（口径披露：count 为本 MCP 会话内累计，save/discard 归零；与 SDK 真值不一致时以 hasEdits 为准）。" +
        "★ Knight60 无此查询能力（本项目差异化件）。无工程打开 → INVALID_STATE。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>(),
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var result = await context.Host.Edits.GetEditSessionAsync(context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }

    protected override string ExecutionTypeName => ExecutionTypes.Native;
}
