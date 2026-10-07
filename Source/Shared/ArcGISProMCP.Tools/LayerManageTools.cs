using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>D-063 · A 段：图层管理增强（10 件）。</summary>
public sealed class SetLayerTransparencyTool : McpToolBase
{
    public override string Name => "set_layer_transparency";
    public override string Description =>
        "设置图层透明度（0–100，CIM 写回）。参数：mapName（可省略 = 活动地图）、layerName、transparency（0–100）。" +
        "**写操作：就地修改图层外观，可经前照原值复原**；返回值为**写后读回**态（transparency/minScale/maxScale），" +
        "可与前照对照证明。透明度越界（<0 或 >100）→ INVALID_ARGUMENT。重名 → AMBIGUOUS_LAYER_NAME；不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
            ["transparency"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Transparency percent, 0-100." },
        },
        ["required"] = new[] { "layerName", "transparency" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var t = ToolArgs.GetDouble(context, "transparency");
        if (t is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "transparency is required (number 0-100).");
        }

        if (t.Value < 0 || t.Value > 100)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, $"transparency must be between 0 and 100 (got {t.Value}).");
        }

        var r = await context.Host!.Layers.SetLayerAppearanceAsync(
            ToolArgs.GetString(context, "mapName"), layerName, t, null, null, context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class SetLayerScaleRangeTool : McpToolBase
{
    public override string Name => "set_layer_scale_range";
    public override string Description =>
        "设置图层显示比例范围（CIM 写回）。参数：mapName（可省略）、layerName、minScale、maxScale（**null/省略 = 不限**）。" +
        "**写操作：就地修改**；返回写后读回态。二者须**至少提供一项**，否则 INVALID_ARGUMENT；负值 → INVALID_ARGUMENT；" +
        "**不限显示**的语义由 SDK 以 0 表达（读回 minScale/maxScale 均为 0 ⇒ showLayerAtAllScales=true）。" +
        "重名 → AMBIGUOUS_LAYER_NAME；不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
            ["minScale"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Minimum scale denominator; null = unlimited." },
            ["maxScale"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Maximum scale denominator; null = unlimited." },
        },
        ["required"] = new[] { "layerName" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var min = ToolArgs.GetDouble(context, "minScale");
        var max = ToolArgs.GetDouble(context, "maxScale");
        if (min is null && max is null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, "at least one of minScale / maxScale is required.");
        }

        if (min is < 0 || max is < 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "scale denominators must be >= 0.");
        }

        var r = await context.Host!.Layers.SetLayerAppearanceAsync(
            ToolArgs.GetString(context, "mapName"), layerName, null, min, max, context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class CreateGroupLayerTool : McpToolBase
{
    public override string Name => "create_group_layer";
    public override string Description =>
        "创建组图层（可选把既有图层移入组内）。参数：mapName（可省略）、groupName、layerNames（可选，要移入的图层名数组）。" +
        "**写操作：新建组图层（TOC 结构变更）**；移入失败的图层会在结果中如实体现（movedLayerCount 与实际一致，不虚报）。" +
        "组名空白 → INVALID_ARGUMENT；组名与既有图层重名 → 由 SDK 判定并返回如实结果。" +
        "返回：groupName / movedLayers / movedLayerCount。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["groupName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Name of the group layer to create." },
            ["layerNames"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "string" }, ["description"] = "Optional layer names to move into the group." },
        },
        ["required"] = new[] { "groupName" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var groupName = ToolArgs.GetString(context, "groupName");
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "groupName is required.");
        }

        var names = JsonHelpers.ToStringList(
            context.Arguments is not null && context.Arguments.TryGetValue("layerNames", out var raw) ? raw : null);

        var r = await context.Host!.Layers.CreateGroupLayerAsync(
            ToolArgs.GetString(context, "mapName"), groupName, names, context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class SetBasemapTool : McpToolBase
{
    public override string Name => "set_basemap";
    public override string Description =>
        "设置地图底图（**状态类**）。参数：mapName（可省略）、basemap（底图名，如 Topographic / Imagery / Streets / OSM）。" +
        "**★ 需网络**：底图库来自 ArcGIS Portal；**离线环境 ⇒ 明确报错（LAYER_DATA_SOURCE_UNAVAILABLE），绝不挂起、不静默成功**——" +
        "工具对 portal 调用带**限时保护**，超时同样转为明确错误。" +
        "成功返回 applied=true 与可用底图清单；失败返回 reason（未登录 / 底图不在库中 / 库不可达）。" +
        "**写操作：改变地图底图构成，可经前照复原**。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["basemap"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Basemap name as shown in the portal gallery." },
        },
        ["required"] = new[] { "basemap" }
    };
    protected override string CategoryName => ToolCategories.Map;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var basemap = ToolArgs.GetString(context, "basemap");
        if (string.IsNullOrWhiteSpace(basemap))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "basemap is required.");
        }

        // 超时保护：底图库访问可能受网络阻塞；超时 ⇒ 明确报错（不挂起）。
        var work = context.Host!.Layers.SetBasemapAsync(
            ToolArgs.GetString(context, "mapName"), basemap, context.CancellationToken);
        var timeout = Task.Delay(TimeSpan.FromSeconds(20), context.CancellationToken);
        var done = await Task.WhenAny(work, timeout).ConfigureAwait(false);
        if (done == timeout)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.Timeout,
                "basemap gallery lookup timed out after 20s (offline or slow portal); " +
                "no basemap was applied (explicit failure, not a hang).");
        }

        var r = await work.ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class GetBrokenLayersTool : McpToolBase
{
    public override string Name => "get_broken_layers";
    public override string Description =>
        "列出当前工程内的断源图层（**只读**）。无参数。" +
        "返回每条：layerName / mapName / layerType / dataSourcePath（不可得时 null）/ isBroken。" +
        "**口径**：扫描当前工程**全部地图**的展开图层清单，判定依据为 SDK ConnectionStatus（≠ Connected 即断源）。" +
        "无断源图层 ⇒ 空清单（totalCount=0，非错误）。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>(),
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var r = await context.Host!.Layers.GetBrokenLayersAsync(context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class RepairLayerSourceTool : McpToolBase
{
    public override string Name => "repair_layer_source";
    public override string Description =>
        "重指图层数据源（**写操作 · 改引用不改数据**）。参数：mapName（可省略）、layerName、" +
        "newWorkspacePath（新工作区路径，替换旧工作区）或 newDatasetName（同工作区内换数据集），**至少一项**。" +
        "**前后照**：结果含 oldPath → newPath 与 repaired（修复后是否仍断源），调用方据此对账。" +
        "**不改数据本身**（仅重绑引用），但**调用方须自行备份原路径以便复原**。" +
        "无法判别当前工作区 → INVALID_ARGUMENT；数据连接类型不支持数据集重指 → INVALID_ARGUMENT（如实拒绝，不静默）。" +
        "重名 → AMBIGUOUS_LAYER_NAME；不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
            ["newWorkspacePath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Replacement workspace path (gdb/folder)." },
            ["newDatasetName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Replacement dataset name within the same workspace." },
        },
        ["required"] = new[] { "layerName" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var ws = ToolArgs.GetString(context, "newWorkspacePath");
        var ds = ToolArgs.GetString(context, "newDatasetName");
        if (string.IsNullOrWhiteSpace(ws) && string.IsNullOrWhiteSpace(ds))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, "newWorkspacePath or newDatasetName is required.");
        }

        var r = await context.Host!.Layers.RepairLayerSourceAsync(
            ToolArgs.GetString(context, "mapName"), layerName, ws, ds, context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>
/// D-066：add_join —— <b>受控 GP 代理</b>（management.AddJoin）。
/// 管线：前置校验（图层/连接表/字段存在）→ confirm 缺省拒（GP 服务 destructive 闸门）→
/// 白名单准入 → 路径守卫 → GP → 审计 → 后置回读（字段前后照）。
/// 禁止绕道：本工具<b>不直连 GP</b>，全部经 <see cref="IGeoprocessingService.RunWhitelistedAsync"/>。
/// </summary>
public sealed class AddJoinTool : McpToolBase
{
    public override string Name => "add_join";
    public override string Description =>
        "建立图层连接（**状态类 · ★ D-066 受控 GP 代理 · destructive=true**）。参数：mapName（可省略）、layerName*、joinTable*（连接表/要素类路径）、joinField*（公共字段名，两侧同名；表侧不同名时另传 joinTableField）、joinTableField（可省略）、keepAll（缺省 false=KEEP_COMMON；true=KEEP_ALL 保留无匹配行）、confirm（**缺省拒**，破坏性操作须显式 true）。" +
        "管线：前置校验（图层存在 / 连接表存在 / 连接字段存在）→ confirm → **白名单准入（management.AddJoin）** → 路径守卫（G-138）→ GP → 审计 → **后置回读（字段前后照）**。" +
        "★ 语义（D-066 spike 实测：standalone arcpy，Pro 3.5）：GP AddJoin **就地修改输入图层**——连接后图层字段以「表名.字段名」形式现身（FieldsBefore/After + NewFields 为证据）；**RemoveJoin 可复原**；对同一图层反复 join 可能触发 arcpy 160091 既有 quirk（如实透传）。" +
        "错误码复用（33 零新增）：NOT_FOUND（图层/表/字段）/ INVALID_ARGUMENT（缺参、confirm 缺省、白名单外）/ GEOPROCESSING_ERROR（GP 失败）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name (modified in place by GP AddJoin)." },
            ["joinTable"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Path of the join table / feature class." },
            ["joinField"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Common field name (used on both sides unless joinTableField is given)." },
            ["joinTableField"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Field name on the join table (defaults to joinField)." },
            ["keepAll"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "true = KEEP_ALL (left outer, keeps unmatched rows); false = KEEP_COMMON. Default false." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Required true (destructive GP tool; default-refuse)." },
        },
        ["required"] = new[] { "layerName", "joinTable", "joinField" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var joinTable = ToolArgs.GetString(context, "joinTable");
        if (string.IsNullOrWhiteSpace(joinTable))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "joinTable is required.");
        }

        var joinField = ToolArgs.GetString(context, "joinField");
        if (string.IsNullOrWhiteSpace(joinField))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "joinField is required.");
        }

        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var mapName = ToolArgs.GetString(context, "mapName");
        var joinTableField = ToolArgs.GetString(context, "joinTableField");
        var keepAll = ToolArgs.GetBool(context, "keepAll");
        var confirm = ToolArgs.GetBool(context, "confirm");

        // ① 前置：图层存在（兼取 AMBIGUOUS 语义）。
        var layer = await context.Host.Layers.FindLayerAsync(mapName ?? string.Empty, layerName, context.CancellationToken).ConfigureAwait(false);
        if (!layer.Success || layer.Data is null)
        {
            return layer.Errors.Count > 0
                ? OperationResult<object?>.Fail(layer.Errors)
                : OperationResult<object?>.Fail(ErrorCodes.NotFound, $"Layer '{layerName}' not found.");
        }

        // ② 前置：连接状态前照（证据通道）。
        var before = await context.Host.Layers.GetJoinStateAsync(mapName, layerName, context.CancellationToken).ConfigureAwait(false);
        if (!before.Success || before.Data is null)
        {
            return OperationResult<object?>.Fail(before.Errors);
        }

        // ③ 前置：连接表与字段存在。
        var table = await context.Host.Schema.GetSchemaInfoAsync(joinTable, context.CancellationToken).ConfigureAwait(false);
        if (!table.Success || table.Data is null)
        {
            return OperationResult<object?>.Fail(table.Errors);
        }

        if (!table.Data.Exists)
        {
            return OperationResult<object?>.Fail(ErrorCodes.NotFound,
                $"join table '{joinTable}' not found (reason: {table.Data.Reason ?? "n/a"}).");
        }

        var tableFieldNames = table.Data.Fields.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tableField = string.IsNullOrWhiteSpace(joinTableField) ? joinField : joinTableField;
        if (!tableFieldNames.Contains(tableField))
        {
            return OperationResult<object?>.Fail(ErrorCodes.NotFound,
                $"field '{tableField}' not found on join table '{joinTable}'.");
        }

        // ④ 前置（best-effort）：输入图层侧字段（图层源可静态解析时才检；否则披露由 GP 校验）。
        var layerFieldNote = "in_field not pre-checked (layer source not statically resolvable); enforced by GP.";
        if (!string.IsNullOrWhiteSpace(layer.Data.Uri))
        {
            var layerSchema = await context.Host.Schema.GetSchemaInfoAsync(layer.Data.Uri, context.CancellationToken).ConfigureAwait(false);
            if (layerSchema.Success && layerSchema.Data is { } lsi && lsi.Exists)
            {
                var layerFieldNames = lsi.Fields.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!layerFieldNames.Contains(joinField))
                {
                    return OperationResult<object?>.Fail(ErrorCodes.NotFound,
                        $"field '{joinField}' not found on layer '{layerName}'.");
                }

                layerFieldNote = "in_field pre-checked on layer source schema (present).";
            }
        }

        // ⑤ 受控 GP 代理（白名单准入 / confirm 缺省拒 / 守卫 / 审计全部由 GP 服务执行）。
        var request = new GpRunRequest
        {
            ToolName = "management.AddJoin",
            Parameters = new Dictionary<string, object?>
            {
                ["in_layer_or_view"] = layerName,
                ["in_field"] = joinField,
                ["join_table"] = joinTable,
                ["join_field"] = tableField,
                ["join_type"] = keepAll ? "KEEP_ALL" : "KEEP_COMMON",
            },
            Confirm = confirm,
            AuditNote = "add_join (D-066 controlled GP proxy)",
        };

        var gp = await context.Host.Geoprocessing.RunWhitelistedAsync(request, context.CancellationToken).ConfigureAwait(false);
        if (!gp.Success || gp.Data is null)
        {
            return OperationResult<object?>.Fail(gp.Errors);
        }

        // ⑥ 后置回读：字段前后照（连接字段现身证据）。
        var after = await context.Host.Layers.GetJoinStateAsync(mapName, layerName, context.CancellationToken).ConfigureAwait(false);
        if (!after.Success || after.Data is null)
        {
            return OperationResult<object?>.Fail(after.Errors);
        }

        var newFields = after.Data.Fields.Except(before.Data.Fields, StringComparer.OrdinalIgnoreCase).ToList();

        return OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["tool"] = "add_join",
            ["gpTool"] = "management.AddJoin",
            ["layerName"] = after.Data.LayerName,
            ["mapName"] = after.Data.MapName,
            ["joinType"] = keepAll ? "KEEP_ALL" : "KEEP_COMMON",
            ["confirmUsed"] = confirm,
            ["fieldsBefore"] = new Dictionary<string, object?> { ["count"] = before.Data.FieldCount, ["dotted"] = before.Data.DottedFields.Count },
            ["fieldsAfter"] = new Dictionary<string, object?> { ["count"] = after.Data.FieldCount, ["dotted"] = after.Data.DottedFields.Count },
            ["newFields"] = newFields,
            ["joinedDetected"] = after.Data.JoinedDetected,
            ["evidenceNote"] = after.Data.Evidence,
            ["layerFieldNote"] = layerFieldNote,
            ["gpMessages"] = gp.Data.Messages,
        });
    }
}

/// <summary>
/// D-066：remove_join —— <b>受控 GP 代理</b>（management.RemoveJoin）。
/// 前置（图层存在 + 连接前照）→ confirm 缺省拒 → 白名单准入 → GP → 审计 → 后照（无连接 ⇒ 幂等返回）。
/// 禁止绕道：不直连 GP；run_batch 批内调用各自 confirm（不豁免）。
/// </summary>
public sealed class RemoveJoinTool : McpToolBase
{
    public override string Name => "remove_join";
    public override string Description =>
        "解除图层连接（**状态类 · ★ D-066 受控 GP 代理 · destructive=true**）。参数：mapName（可省略）、layerName*、joinName（可省略 = 移除全部连接）、confirm（**缺省拒**）。" +
        "管线：前置（图层存在 + 连接前照）→ confirm → **白名单准入（management.RemoveJoin）** → 路径守卫 → GP → 审计 → **后照**（后照 joined=false ⇒ 幂等返回；源数据不变——连接为虚拟层语义，D-066 spike 实测 RemoveJoin 后字段名复原）。" +
        "错误码复用：NOT_FOUND / INVALID_ARGUMENT / GEOPROCESSING_ERROR（33 零新增）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name (modified in place)." },
            ["joinName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Join name to remove; omit to remove all joins." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Required true (destructive GP tool; default-refuse)." },
        },
        ["required"] = new[] { "layerName" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var mapName = ToolArgs.GetString(context, "mapName");
        var joinName = ToolArgs.GetString(context, "joinName");
        var confirm = ToolArgs.GetBool(context, "confirm");

        // ① 前置：图层存在。
        var layer = await context.Host.Layers.FindLayerAsync(mapName ?? string.Empty, layerName, context.CancellationToken).ConfigureAwait(false);
        if (!layer.Success || layer.Data is null)
        {
            return layer.Errors.Count > 0
                ? OperationResult<object?>.Fail(layer.Errors)
                : OperationResult<object?>.Fail(ErrorCodes.NotFound, $"Layer '{layerName}' not found.");
        }

        // ② 前照。
        var before = await context.Host.Layers.GetJoinStateAsync(mapName, layerName, context.CancellationToken).ConfigureAwait(false);
        if (!before.Success || before.Data is null)
        {
            return OperationResult<object?>.Fail(before.Errors);
        }

        // ③ 受控 GP 代理。
        var parameters = new Dictionary<string, object?>
        {
            ["in_layer_or_view"] = layerName,
        };
        if (!string.IsNullOrWhiteSpace(joinName))
        {
            parameters["join_name"] = joinName;
        }

        var request = new GpRunRequest
        {
            ToolName = "management.RemoveJoin",
            Parameters = parameters,
            Confirm = confirm,
            AuditNote = "remove_join (D-066 controlled GP proxy)",
        };

        var gp = await context.Host.Geoprocessing.RunWhitelistedAsync(request, context.CancellationToken).ConfigureAwait(false);
        if (!gp.Success || gp.Data is null)
        {
            return OperationResult<object?>.Fail(gp.Errors);
        }

        // ④ 后照（无连接 ⇒ 幂等返回）。
        var after = await context.Host.Layers.GetJoinStateAsync(mapName, layerName, context.CancellationToken).ConfigureAwait(false);
        if (!after.Success || after.Data is null)
        {
            return OperationResult<object?>.Fail(after.Errors);
        }

        var removedFields = before.Data.Fields.Except(after.Data.Fields, StringComparer.OrdinalIgnoreCase).ToList();

        return OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["tool"] = "remove_join",
            ["gpTool"] = "management.RemoveJoin",
            ["layerName"] = after.Data.LayerName,
            ["mapName"] = after.Data.MapName,
            ["operation"] = "remove",
            ["joined"] = after.Data.JoinedDetected,
            ["confirmUsed"] = confirm,
            ["fieldsBefore"] = new Dictionary<string, object?> { ["count"] = before.Data.FieldCount, ["dotted"] = before.Data.DottedFields.Count },
            ["fieldsAfter"] = new Dictionary<string, object?> { ["count"] = after.Data.FieldCount, ["dotted"] = after.Data.DottedFields.Count },
            ["removedFields"] = removedFields,
            ["idempotentNoJoin"] = !after.Data.JoinedDetected,
            ["gpMessages"] = gp.Data.Messages,
        });
    }
}

public sealed class RenameLayerTool : McpToolBase
{
    public override string Name => "rename_layer";
    public override string Description =>
        "TOC 图层改名（**写操作**）。参数：mapName（可省略）、layerName、newName。" +
        "返回 oldName → newName（写后读回）。newName 空白 → INVALID_ARGUMENT。" +
        "**仅改显示名，不改数据源**；调用方保存前照即可复原。重名 → AMBIGUOUS_LAYER_NAME；不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
            ["newName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "New TOC name." },
        },
        ["required"] = new[] { "layerName", "newName" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layerName = ToolArgs.GetString(context, "layerName");
        var newName = ToolArgs.GetString(context, "newName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "newName is required.");
        }

        var r = await context.Host!.Layers.RenameLayerAsync(
            ToolArgs.GetString(context, "mapName"), layerName, newName, context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class DuplicateLayerTool : McpToolBase
{
    public override string Name => "duplicate_layer";
    public override string Description =>
        "复制图层（同图内副本，**写操作**）。参数：mapName（可省略）、layerName、newName（可选，省略则追加 \" copy\"）。" +
        "返回 sourceLayerName → newLayerName。副本**共享原数据源**（非数据复制），**不改数据**。" +
        "复制失败 → ARCGIS_ERROR 如实返回。重名 → AMBIGUOUS_LAYER_NAME；不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name to duplicate." },
            ["newName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional name for the copy." },
        },
        ["required"] = new[] { "layerName" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var r = await context.Host!.Layers.DuplicateLayerAsync(
            ToolArgs.GetString(context, "mapName"), layerName,
            ToolArgs.GetString(context, "newName"), context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}
