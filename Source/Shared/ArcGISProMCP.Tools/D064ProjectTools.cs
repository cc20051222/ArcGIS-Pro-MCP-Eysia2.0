using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

// ══════════════════════════════════════════════════════════════════════════════
// D-064 · B 段：Project 增强（5 件）
//   remove_map（confirm 缺省拒） / activate_map / set_map_properties
//   get_environment（只读） / set_environment（会话级 + 审计附注 + reset 还原）
// 错误码零新增。
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>D-064 · 删除工程内地图（破坏性 ⇒ confirm 缺省拒）。</summary>
public sealed class RemoveMapTool : McpToolBase
{
    public override string Name => "remove_map";

    public override string Description =>
        "删除当前工程内的地图（**破坏性**：地图及其图层组织不可恢复 —— 注意这不删除磁盘数据，但地图配置丢失）。" +
        "参数：mapName、confirm。契约：**confirm 必须显式 true**（缺省或 false → INVALID_ARGUMENT，零变更）；" +
        "地图不存在 → MAP_NOT_FOUND；重名/歧义 → AMBIGUOUS_MAP_NAME；返回 mapsBefore/mapsAfter 对照证明已删除。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map to remove." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Must be explicitly true; otherwise refused (INVALID_ARGUMENT)." },
        },
        ["required"] = new[] { "mapName", "confirm" }
    };

    protected override string CategoryName => ToolCategories.Project;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var mapName = ToolArgs.GetString(context, "mapName");
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "mapName is required.");
        }

        if (!ToolArgs.GetBool(context, "confirm"))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "remove_map is destructive: confirm=true is required (default-refuse); no change was made.");
        }

        var r = await context.Host.Maps.RemoveMapAsync(mapName!, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · 激活/打开地图视图（UI 联动；会话/视图态）。</summary>
public sealed class ActivateMapTool : McpToolBase
{
    public override string Name => "activate_map";

    public override string Description =>
        "激活当前工程内的地图：若该地图视图未打开则打开，已打开则置前激活（**仅切换视图态，不改数据/工程**）。" +
        "参数：mapName。契约：地图不存在 → MAP_NOT_FOUND；无 UI/视图能力时如实披露（viewOpened=false + note），不伪报成功。" +
        "提示：后续需要活动视图的工具（回图、书签、相机读写）依赖本工具打开的视图。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map to activate (view opened or brought to front)." },
        },
        ["required"] = new[] { "mapName" }
    };

    protected override string CategoryName => ToolCategories.Map;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var mapName = ToolArgs.GetString(context, "mapName");
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "mapName is required.");
        }

        var r = await context.Host.Maps.ActivateMapAsync(mapName!, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · 地图属性（改名 / 换空间参考；写后读回）。</summary>
public sealed class SetMapPropertiesTool : McpToolBase
{
    public override string Name => "set_map_properties";

    public override string Description =>
        "修改地图属性：**改名**与/或**换空间参考**（至少提供一项）。参数：mapName、newName（可选）、spatialReference（可选；WKID 或名称）。" +
        "契约：两项都未提供 → INVALID_ARGUMENT；地图不存在 → MAP_NOT_FOUND；新名与既有地图重名 → AMBIGUOUS_MAP_NAME（拒绝且零变更）；" +
        "返回 nameBefore/name/renamed + spatialReferenceBefore/spatialReference 写后读回对照。**注意：换 SR 只改地图坐标系声明，不重投影数据。**";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map to modify." },
            ["newName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "New map name (optional)." },
            ["spatialReference"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "WKID or SR name (optional; changes the map SR declaration only — data is NOT reprojected)." },
        },
        ["required"] = new[] { "mapName" }
    };

    protected override string CategoryName => ToolCategories.Map;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var mapName = ToolArgs.GetString(context, "mapName");
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "mapName is required.");
        }

        var newName = ToolArgs.GetString(context, "newName");
        var sr = ToolArgs.GetString(context, "spatialReference");
        if (string.IsNullOrWhiteSpace(newName) && string.IsNullOrWhiteSpace(sr))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, "at least one of newName or spatialReference must be provided.");
        }

        var r = await context.Host.Maps.SetMapPropertiesAsync(mapName!, newName, sr, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · 读取 GP 环境设置（只读）。</summary>
public sealed class GetEnvironmentTool : McpToolBase
{
    public override string Name => "get_environment";

    public override string Description =>
        "读取当前 GP（地理处理）环境设置：workspace / scratchWorkspace / outputCoordinateSystem / extent / mask / " +
        "cellSize / overwriteOutput / parallelProcessing / parallelProcessingFactor / outputMFlag / outputZFlag。" +
        "**只读**，零状态变更；未设置项列入 <code>unset</code>（如实披露缺省，**不伪造值**）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>(),
    };

    protected override string CategoryName => ToolCategories.Geoprocessing;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var r = await context.Host.Geoprocessing.GetEnvironmentAsync(context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · 设置 GP 环境（会话级 + 审计附注 + reset 还原）。</summary>
public sealed class SetEnvironmentTool : McpToolBase
{
    public override string Name => "set_environment";

    public override string Description =>
        "设置 GP 环境（**会话级**：影响其后所有 GP / 受控调用，**不落盘、进程结束失效**；每次调用写审计附注）。参数（全部可选，至少一项）：" +
        "workspace、scratchWorkspace、outputCoordinateSystem、extent（\"xmin ymin xmax ymax\"）、mask、cellSize、" +
        "overwriteOutput（boolean）、parallelProcessing（boolean）、parallelProcessingFactor（int）、reset（boolean）。" +
        "契约：**reset=true 时忽略其它入参**，还原到**会话初始快照**（首见环境态）并返回 resetRestoredBaseline；" +
        "未提供的键**保持不动**（列入 unchanged）；无任何有效入参且 reset=false → INVALID_ARGUMENT；" +
        "返回 before/after 前后照 + 审计条号（auditEntryIndex=0 表示审计未落盘，如实披露不掩盖）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["workspace"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "GP workspace path." },
            ["scratchWorkspace"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Scratch workspace path." },
            ["outputCoordinateSystem"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output coordinate system (WKID or name)." },
            ["extent"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Processing extent 'xmin ymin xmax ymax'." },
            ["mask"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Analysis mask dataset." },
            ["cellSize"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output cell size (number or dataset)." },
            ["overwriteOutput"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "GP overwriteOutput setting (NOTE: tool-level 'overwrite' guards are independent)." },
            ["parallelProcessing"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Enable parallel processing." },
            ["parallelProcessingFactor"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Parallel processing factor (e.g. 0, 50, 100, -1)." },
            ["reset"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Restore the session-initial GP environment; other inputs are ignored." },
        },
    };

    protected override string CategoryName => ToolCategories.Geoprocessing;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var reset = ToolArgs.GetBool(context, "reset");
        var hasFalse = context.Arguments is not null
                       && D064EnvironmentKeys.All.Any(k => ToolArgs.GetValue(context, k) is not null);

        if (!reset && !hasFalse)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "at least one environment key or reset=true must be provided: " + string.Join(", ", D064EnvironmentKeys.All) + ".");
        }

        if (!reset)
        {
            if (ToolArgs.GetValue(context, "extent") is string ext && !D064EnvironmentKeys.IsValidExtent(ext))
            {
                return OperationResult<object?>.Fail(
                    ErrorCodes.InvalidArgument, "extent must be 4 space-separated doubles: 'xmin ymin xmax ymax'.");
            }

            var pf = ToolArgs.GetInt(context, "parallelProcessingFactor");
            if (pf is not null && pf.Value < -1)
            {
                return OperationResult<object?>.Fail(
                    ErrorCodes.InvalidArgument, "parallelProcessingFactor must be >= -1 (-1 = 100%).");
            }
        }

        var desired = reset ? null : new GpEnvironmentInfo
        {
            Workspace = ToolArgs.GetString(context, "workspace"),
            ScratchWorkspace = ToolArgs.GetString(context, "scratchWorkspace"),
            OutputCoordinateSystem = ToolArgs.GetString(context, "outputCoordinateSystem"),
            Extent = ToolArgs.GetString(context, "extent"),
            Mask = ToolArgs.GetString(context, "mask"),
            CellSize = ToolArgs.GetString(context, "cellSize"),
            OverwriteOutput = ToolArgs.GetValue(context, "overwriteOutput") is null
                ? null
                : ToolArgs.GetBool(context, "overwriteOutput"),
            ParallelProcessing = ToolArgs.GetValue(context, "parallelProcessing") is null
                ? null
                : ToolArgs.GetBool(context, "parallelProcessing"),
            ParallelProcessingFactor = ToolArgs.GetInt(context, "parallelProcessingFactor"),
        };

        var r = await context.Host.Geoprocessing.SetEnvironmentAsync(desired, reset, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · B 段 GP 环境键清单（工具层准入；纯数据）。</summary>
internal static class D064EnvironmentKeys
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "workspace", "scratchWorkspace", "outputCoordinateSystem", "extent", "mask", "cellSize",
        "overwriteOutput", "parallelProcessing", "parallelProcessingFactor",
    };

    /// <summary>extent 形态校验：4 个空格分隔的有限双精度数。</summary>
    public static bool IsValidExtent(string? extent)
    {
        if (string.IsNullOrWhiteSpace(extent))
        {
            return false;
        }

        var parts = extent!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            return false;
        }

        foreach (var p in parts)
        {
            if (!double.TryParse(p, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var d)
                || double.IsNaN(d) || double.IsInfinity(d))
            {
                return false;
            }
        }

        return true;
    }
}
