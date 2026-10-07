using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Selection;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// Phase 8.3 Selection Contract V2（D-013，G-32 批准）。
/// 编排约定（D-014 方案 A）：写操作 = 账本写前核对（不符即拒绝）→ 自动写前快照（G-30 强制）→ Pro 进程内 SDK GP 写 →
/// Native 读后实测 → 账本提交（revision 单调递增）。读操作不拒绝，仅标注 ledgerMatch。
/// </summary>
internal static class SelectionContractOrchestration
{
    public const long MaxOidList = 100_000;
    public const int MaxWhereLength = 8_192;

    internal static readonly IReadOnlyDictionary<string, string> ModeMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["replace"] = "NEW_SELECTION",
        ["add"] = "ADD_TO_SELECTION",
        ["remove"] = "REMOVE_FROM_SELECTION",
        ["switch"] = "SWITCH_SELECTION",
    };

    internal static readonly IReadOnlyCollection<string> LocationModes = new[] { "replace", "add", "remove" };

    internal static readonly IReadOnlyCollection<string> OverlapTypes = new[]
    {
        "INTERSECT", "INTERSECT_3D", "INTERSECT_DBMS",
        "WITHIN_A_DISTANCE", "WITHIN_A_DISTANCE_GEODESIC", "WITHIN_A_DISTANCE_3D",
        "CONTAINS", "COMPLETELY_CONTAINS", "CONTAINS_CLEMENTINI",
        "WITHIN", "COMPLETELY_WITHIN", "WITHIN_CLEMENTINI",
        "ARE_IDENTICAL_TO", "BOUNDARY_TOUCHES", "SHARE_A_LINE_SEGMENT_WITH",
        "CROSSED_BY_THE_OUTLINE_OF", "HAVE_THEIR_CENTER_IN",
    };

    /// <summary>写前：读当前选择 → 账本核对（不符拒绝）→ 自动快照。返回 (content, snapshotId)。</summary>
    internal static async Task<OperationResult<(SelectionContent Content, string? SnapshotId)>> PrepareWriteAsync(
        ToolExecutionContext context, string? mapName, string op)
    {
        var host = context.Host!;
        var read = await host.SelectionRead.GetSelectionContentAsync(mapName, context.CancellationToken).ConfigureAwait(false);
        if (!read.Success)
        {
            return OperationResult<(SelectionContent, string?)>.Fail(read.Errors);
        }

        var content = read.Data!;
        var check = host.SelectionState.CheckWritePrecondition(content.MapUri, content);
        if (!check.Matched)
        {
            return OperationResult<(SelectionContent, string?)>.Fail(
                ErrorCodes.SelectionBaselineMismatch,
                check.DiffSummary ?? "selection baseline mismatch.");
        }

        var snapshot = host.SelectionState.AddSnapshot(
            content.MapUri, content.MapName, content, check.LedgerExists ? check.Revision : 0);
        return OperationResult<(SelectionContent, string?)>.Ok((content, snapshot.SnapshotId));
    }

    /// <summary>写后：读实测内容 → 账本提交 → 返回 revision。</summary>
    internal static async Task<OperationResult<(SelectionContent Content, long Revision)>> CommitWriteAsync(
        ToolExecutionContext context, string? mapName, string op)
    {
        var host = context.Host!;
        var read = await host.SelectionRead.GetSelectionContentAsync(mapName, context.CancellationToken).ConfigureAwait(false);
        if (!read.Success)
        {
            return OperationResult<(SelectionContent, long)>.Fail(read.Errors);
        }

        var content = read.Data!;
        var revision = host.SelectionState.CommitWrite(content.MapUri, content, op);
        return OperationResult<(SelectionContent, long)>.Ok((content, revision));
    }

    /// <summary>解析 oidList（非负整数数组；上限拒绝；非法元素拒绝）。</summary>
    internal static OperationResult<IReadOnlyList<long>?> ParseOidList(ToolExecutionContext context, string key)
    {
        if (context.Arguments is null || !context.Arguments.TryGetValue(key, out var raw) || raw is null)
        {
            return OperationResult<IReadOnlyList<long>?>.Ok(null);
        }

        if (raw is not System.Collections.IEnumerable enumerable || raw is string)
        {
            return OperationResult<IReadOnlyList<long>?>.Fail(ErrorCodes.InvalidArgument, "oidList must be an array of integers.");
        }

        var list = new List<long>();
        foreach (var item in enumerable)
        {
            switch (item)
            {
                case int i when i >= 0:
                    list.Add(i);
                    break;
                case long l when l >= 0:
                    list.Add(l);
                    break;
                case double d when d >= 0 && d == Math.Floor(d) && d <= long.MaxValue:
                    // JsonValueConverter 把所有 JSON 数字统一为 double（long→double 隐转），整数值 double 需接受。
                    list.Add((long)d);
                    break;
                default:
                    return OperationResult<IReadOnlyList<long>?>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"invalid OID element: {item} (non-negative integers required).");
            }
        }

        if (list.Count > MaxOidList)
        {
            return OperationResult<IReadOnlyList<long>?>.Fail(
                ErrorCodes.SelectionLimitExceeded,
                $"oidList length {list.Count} exceeds limit {MaxOidList}.");
        }

        return OperationResult<IReadOnlyList<long>?>.Ok(list);
    }

    /// <summary>写后内容中找指定图层（供响应 layerUri/selectedCount）。</summary>
    internal static SelectionLayerContent? FindLayer(SelectionContent content, string layerName)
        => content.Layers.FirstOrDefault(l => string.Equals(l.LayerName, layerName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>按属性/OID 选择要素（Phase 8.3；写操作，G-30 账本+自动快照）。</summary>
public sealed class SelectByAttributeTool : McpToolBase
{
    public override string Name => "select_by_attribute";
    public override string Description =>
        "按 where 子句或 OID 列表选择要素（replace/add/remove/switch）。oidList 与 where 二选一（两者皆缺拒绝，" +
        "不提供隐式全选）。写操作：写前核对选择基线（用户 UI 改动后拒绝 SELECTION_BASELINE_MISMATCH），" +
        "自动创建写前快照（snapshotBefore，默认开）。switch 非幂等（响应 nonIdempotent:true）。参数：layerName（必填）、mapName、mode、oidList、where、snapshotBefore。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "地图名（可选，空白=活动地图）。" },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "目标图层名。" },
            ["mode"] = new Dictionary<string, object?> { ["type"] = "string", ["enum"] = new[] { "replace", "add", "remove", "switch" }, ["default"] = "replace" },
            ["oidList"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "integer" }, ["description"] = "OID 列表（与 where 互斥；≤100000）。" },
            ["where"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "ArcGIS SQL where 子句（与 oidList 互斥；≤8192 字符）。" },
            ["snapshotBefore"] = new Dictionary<string, object?> { ["type"] = "boolean", ["default"] = true, ["description"] = "写前自动快照（G-30 建议保持开启）。" },
        },
        ["required"] = new[] { "layerName" }
    };
    protected override string CategoryName => ToolCategories.Selection;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    protected override bool? RequiresArcGISOverride => true;

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

        var mapName = ToolArgs.GetString(context, "mapName");
        var mode = ToolArgs.GetString(context, "mode") ?? "replace";
        if (!SelectionContractOrchestration.ModeMap.ContainsKey(mode))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "mode must be one of: replace, add, remove, switch.");
        }

        var oidResult = SelectionContractOrchestration.ParseOidList(context, "oidList");
        if (!oidResult.Success)
        {
            return OperationResult<object?>.Fail(oidResult.Errors);
        }

        var oidList = oidResult.Data;
        var where = ToolArgs.GetString(context, "where");
        if (oidList is not null && !string.IsNullOrWhiteSpace(where))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "oidList and where are mutually exclusive.");
        }

        if (mode == "switch" && (oidList is not null || !string.IsNullOrWhiteSpace(where)))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "switch takes no oidList or where (it toggles the current selection).");
        }

        if (mode != "switch" && oidList is null && string.IsNullOrWhiteSpace(where))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "either oidList or where is required (implicit select-all is rejected).");
        }

        if (where is not null && where.Length > SelectionContractOrchestration.MaxWhereLength)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"where exceeds {SelectionContractOrchestration.MaxWhereLength} characters.");
        }

        var prepare = await SelectionContractOrchestration.PrepareWriteAsync(context, mapName, "select_by_attribute").ConfigureAwait(false);
        if (!prepare.Success)
        {
            return OperationResult<object?>.Fail(prepare.Errors);
        }

        var write = await context.Host!.Geoprocessing.SelectLayerByAttributeAsync(mapName, layerName, mode, oidList, where, context.CancellationToken).ConfigureAwait(false);
        if (!write.Success)
        {
            // G-30：写失败禁止自动重试——如实返回，账本保持写前状态。
            return OperationResult<object?>.Fail(write.Errors);
        }

        var commit = await SelectionContractOrchestration.CommitWriteAsync(context, mapName, "select_by_attribute").ConfigureAwait(false);
        if (!commit.Success)
        {
            return OperationResult<object?>.Fail(commit.Errors);
        }

        var (content, revision) = commit.Data;
        var layer = SelectionContractOrchestration.FindLayer(content, layerName);
        return OperationResult<object?>.Ok(new
        {
            mapName = content.MapName,
            layerName,
            layerUri = layer?.LayerUri,
            mode,
            selectedCount = layer?.SelectedCount ?? 0,
            nonIdempotent = mode == "switch",
            snapshotId = prepare.Data.SnapshotId,
            revision,
        });
    }
}

/// <summary>按空间关系选择要素（Phase 8.3；17 overlap 白名单；写操作）。</summary>
public sealed class SelectByLocationTool : McpToolBase
{
    public override string Name => "select_by_location";
    public override string Description =>
        "按空间关系选择要素（overlapType 17 种：INTERSECT/WITHIN/CONTAINS/ARE_IDENTICAL_TO/BOUNDARY_TOUCHES 等）。" +
        "WITHIN_A_DISTANCE 系必须给 searchDistance(>0)，其余系不得给。mode=replace/add/remove（无 switch）。" +
        "写操作：写前核对基线 + 自动快照。参数：layerName（必填）、mapName、selectingLayerName（缺省=同图层）、overlapType、searchDistance、searchDistanceUnit、mode、snapshotBefore。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "被选图层（in_layer）。" },
            ["selectingLayerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "选择源图层（缺省=同图层自选）。" },
            ["overlapType"] = new Dictionary<string, object?> { ["type"] = "string", ["default"] = "INTERSECT", ["description"] = "17 种空间关系之一。" },
            ["searchDistance"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "仅 WITHIN_A_DISTANCE 系；>0。" },
            ["searchDistanceUnit"] = new Dictionary<string, object?> { ["type"] = "string", ["default"] = "Meters" },
            ["mode"] = new Dictionary<string, object?> { ["type"] = "string", ["enum"] = new[] { "replace", "add", "remove" }, ["default"] = "replace" },
            ["snapshotBefore"] = new Dictionary<string, object?> { ["type"] = "boolean", ["default"] = true },
        },
        ["required"] = new[] { "layerName" }
    };
    protected override string CategoryName => ToolCategories.Selection;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    protected override bool? RequiresArcGISOverride => true;

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

        var mapName = ToolArgs.GetString(context, "mapName");
        var selectingLayerName = ToolArgs.GetString(context, "selectingLayerName");
        var overlapType = ToolArgs.GetString(context, "overlapType") ?? "INTERSECT";
        if (!SelectionContractOrchestration.OverlapTypes.Contains(overlapType))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "overlapType must be one of: " + string.Join(", ", SelectionContractOrchestration.OverlapTypes) + ".");
        }

        double? searchDistance = null;
        if (context.Arguments is not null && context.Arguments.TryGetValue("searchDistance", out var sdRaw) && sdRaw is not null)
        {
            searchDistance = ToolArgs.GetInt(context, "searchDistance") ?? ToolArgs.GetDouble(context, "searchDistance");
        }

        var needsDistance = overlapType.StartsWith("WITHIN_A_DISTANCE", StringComparison.Ordinal);
        if (needsDistance && (searchDistance is null || searchDistance <= 0))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "searchDistance is required for overlapType " + overlapType + ".");
        }

        if (!needsDistance && searchDistance is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "searchDistance is not applicable to overlapType " + overlapType + ".");
        }

        var mode = ToolArgs.GetString(context, "mode") ?? "replace";
        if (!SelectionContractOrchestration.LocationModes.Contains(mode))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "mode must be one of: replace, add, remove.");
        }

        var prepare = await SelectionContractOrchestration.PrepareWriteAsync(context, mapName, "select_by_location").ConfigureAwait(false);
        if (!prepare.Success)
        {
            return OperationResult<object?>.Fail(prepare.Errors);
        }

        var write = await context.Host!.Geoprocessing.SelectLayerByLocationAsync(
            mapName, layerName, selectingLayerName, overlapType, searchDistance,
            ToolArgs.GetString(context, "searchDistanceUnit") ?? "Meters", mode, context.CancellationToken).ConfigureAwait(false);
        if (!write.Success)
        {
            return OperationResult<object?>.Fail(write.Errors);
        }

        var commit = await SelectionContractOrchestration.CommitWriteAsync(context, mapName, "select_by_location").ConfigureAwait(false);
        if (!commit.Success)
        {
            return OperationResult<object?>.Fail(commit.Errors);
        }

        var (content, revision) = commit.Data;
        var layer = SelectionContractOrchestration.FindLayer(content, layerName);
        return OperationResult<object?>.Ok(new
        {
            mapName = content.MapName,
            layerName,
            layerUri = layer?.LayerUri,
            overlapType,
            mode,
            selectedCount = layer?.SelectedCount ?? 0,
            snapshotId = prepare.Data.SnapshotId,
            revision,
        });
    }
}

/// <summary>读取当前选择（OID 级，逐图层；只读不拒绝）。</summary>
public sealed class GetSelectedFeaturesTool : McpToolBase
{
    public override string Name => "get_selected_features";
    public override string Description =>
        "返回当前地图选择集：逐图层 OID（升序）、selectedCount、truncated（>10000 截断告警）。" +
        "不返回属性字段（字段级读取请用 query_attributes——结构性杜绝敏感字段泄露）。空选择成功返回空列表。" +
        "参数：mapName（可选）、layerName（可选，过滤单图层）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "过滤单图层（可选）。" },
        }
    };
    protected override string CategoryName => ToolCategories.Selection;
    protected override bool? RequiresArcGISOverride => true;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var mapName = ToolArgs.GetString(context, "mapName");
        var layerName = ToolArgs.GetString(context, "layerName");

        var read = await context.Host.SelectionRead.GetSelectionContentAsync(mapName, context.CancellationToken).ConfigureAwait(false);
        if (!read.Success)
        {
            return OperationResult<object?>.Fail(read.Errors);
        }

        var content = read.Data!;
        var layers = content.Layers
            .Where(l => string.IsNullOrWhiteSpace(layerName) || string.Equals(l.LayerName, layerName, StringComparison.OrdinalIgnoreCase))
            .Select(l => new
            {
                layerName = l.LayerName,
                layerUri = l.LayerUri,
                oids = l.Oids,
                selectedCount = l.SelectedCount,
                truncated = l.Truncated,
            })
            .ToList();

        var ledgerMatch = context.Host.SelectionState.IsLedgerMatch(content.MapUri, content);
        return OperationResult<object?>.Ok(new
        {
            mapName = content.MapName,
            layers,
            totalSelected = layers.Sum(l => l.selectedCount),
            ledgerMatch,
        });
    }
}

/// <summary>创建选择快照（服务端内存，会话级，LRU 8）。</summary>
public sealed class CreateSelectionSnapshotTool : McpToolBase
{
    public override string Name => "create_selection_snapshot";
    public override string Description =>
        "创建当前地图选择集快照（服务端存储，返回 snapshotId 句柄）。会话级：Pro 重启/工程切换/服务重启后失效（惰性判定）。" +
        "快照内容=各图层 OID 成员；不含层序/可见性（恢复亦然）。参数：mapName（可选）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string" }
        }
    };
    protected override string CategoryName => ToolCategories.Selection;
    protected override bool? RequiresArcGISOverride => true;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var mapName = ToolArgs.GetString(context, "mapName");
        var r = await context.Host.SelectionRead.CreateSnapshotAsync(mapName, context.CancellationToken).ConfigureAwait(false);
        if (!r.Success)
        {
            return OperationResult<object?>.Fail(r.Errors);
        }

        var snap = r.Data!;
        return OperationResult<object?>.Ok(new
        {
            snapshotId = snap.SnapshotId,
            mapName = snap.MapName,
            mapUri = snap.MapUri,
            layerCount = snap.Content.Layers.Count,
            totalSelected = snap.Content.TotalSelected,
            revisionAtCreation = snap.RevisionAtCreation,
            createdAtUtc = snap.CreatedAtUtc,
        });
    }
}

/// <summary>恢复选择快照（恢复前核对账本基线；不得覆盖用户后续选择）。</summary>
public sealed class RestoreSelectionSnapshotTool : McpToolBase
{
    public override string Name => "restore_selection_snapshot";
    public override string Description =>
        "恢复选择快照。恢复前核对账本基线：快照后若用户/UI 改动过选择（账本不符）→ 拒绝（SELECTION_BASELINE_MISMATCH+diff），" +
        "绝不覆盖。快照失效（工程切换/地图关闭/Pro 重启/LRU 淘汰）→ SELECTION_SNAPSHOT_EXPIRED。" +
        "恢复仅选择成员，不含层序/可见性（restoresSelectionOnly:true）。参数：snapshotId（必填）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["snapshotId"] = new Dictionary<string, object?> { ["type"] = "string" }
        },
        ["required"] = new[] { "snapshotId" }
    };
    protected override string CategoryName => ToolCategories.Selection;
    protected override bool? RequiresArcGISOverride => true;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var snapshotId = ToolArgs.GetString(context, "snapshotId");
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "snapshotId is required.");
        }

        var snapshot = context.Host.SelectionState.TryGetSnapshot(snapshotId);
        if (snapshot is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.SelectionSnapshotNotFound, "snapshot not found: " + snapshotId);
        }

        // 目标地图存在性/同一性（惰性过期判定）——按 mapUri（重名地图不能按名解析，D-014）。
        var current = await context.Host.SelectionRead.GetSelectionContentByUriAsync(snapshot.MapUri, context.CancellationToken).ConfigureAwait(false);
        if (!current.Success)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.SelectionSnapshotExpired,
                "snapshot expired or map unavailable: " + current.Errors[0].Message);
        }

        var currentContent = current.Data!;

        // 账本基线核对：快照后用户/UI 改动 → 拒绝。
        var check = context.Host.SelectionState.CheckWritePrecondition(currentContent.MapUri, currentContent);
        if (!check.Matched)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.SelectionBaselineMismatch,
                "user modified selection after snapshot; refusing to overwrite. " + check.DiffSummary);
        }

        var restore = await context.Host.SelectionRead.RestoreSnapshotAsync(snapshot.MapName, snapshot.Content, context.CancellationToken).ConfigureAwait(false);
        if (!restore.Success)
        {
            return OperationResult<object?>.Fail(restore.Errors);
        }

        var after = await context.Host.SelectionRead.GetSelectionContentByUriAsync(snapshot.MapUri, context.CancellationToken).ConfigureAwait(false);
        if (!after.Success)
        {
            return OperationResult<object?>.Fail(after.Errors);
        }

        var revision = context.Host.SelectionState.CommitWrite(after.Data!.MapUri, after.Data!, "restore_selection_snapshot");
        var result = restore.Data!;
        return OperationResult<object?>.Ok(new
        {
            snapshotId = snapshot.SnapshotId,
            restoredLayers = result.RestoredLayers.Select(l => new { layerName = l.LayerName, layerUri = l.LayerUri, restoredCount = l.SelectedCount }),
            restoredCount = result.RestoredCount,
            restoresSelectionOnly = result.RestoresSelectionOnly,
            revision,
        });
    }
}
