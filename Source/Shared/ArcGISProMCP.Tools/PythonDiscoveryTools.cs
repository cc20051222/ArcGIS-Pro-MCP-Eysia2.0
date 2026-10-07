using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// Phase 5.5.3 三个只读 Python/ArcPy discovery Tool。
/// 全部通过 <see cref="ToolExecutionContext.Python"/> 访问 PythonBridgeService（磁盘数据集路径/ArcPy）。
/// 不访问当前 ArcGIS Pro 工程图层 / CURRENT，不直接 Open subprocess / bridge_runner / HTTP。
/// 仅做有限 Tool 层封装，复用 Service/Bridge 已锁定的语义与错误码。
/// </summary>
public sealed class DatasetSummaryTool : McpToolBase
{
    public override string Name => "dataset_summary";

    public override string Description =>
        "对磁盘数据集路径（FileGDB/要素类/表等）的只读结构摘要查询：存在性/类型/几何类型/坐标系/要素数量/OID 字段/范围/字段名。" +
        "这是磁盘数据集路径(ArcPy)查询，不访问当前 ArcGIS Pro 工程图层/CURRENT。不存在路径成功返回 exists=false。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["dataset_path"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "磁盘数据集路径（要素类/表等），如 FileGDB 内的要素类。"
            }
        },
        ["required"] = new[] { "dataset_path" }
    };

    protected override string CategoryName => ToolCategories.Python;
    protected override string ExecutionTypeName => ExecutionTypes.Python;
    protected override bool? RequiresArcGISOverride => false;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var bridge = context.Python;
        if (bridge is null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.PythonBridgeUnavailable,
                "Python Bridge service is not available in this host.");
        }

        var path = ToolArgs.GetString(context, "dataset_path");
        // 缺失参数由 Router(required) 拦截；空/错误类型在 Service 层映射 INVALID_ARGUMENT。
        // 这里 ToolArgs 只负责取原始字符串，不重复造校验框架。
        var r = await bridge.DatasetSummaryAsync(path ?? string.Empty, context.CancellationToken).ConfigureAwait(false);
        return r.Success
            ? OperationResult<object?>.Ok(r.Data.HasValue ? r.Data.Value : JsonValueKind.Null)
            : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>磁盘数据集字段结构查询（通过 Python Bridge / ArcPy）。与 Native get_field_info（Live Pro Layer）语义不同。</summary>
public sealed class ListFieldsTool : McpToolBase
{
    public override string Name => "list_fields";

    public override string Description =>
        "返回磁盘数据集路径（dataset path）的字段结构：字段名/类型/别名/长度/精度/尺度/领域/默认值/是否 OID。 " +
        "这是磁盘数据集(ArcPy)字段查询；与 get_field_info（当前 ArcGIS Pro Layer / Native SDK）语义不同，两者不可互换。不访问 CURRENT。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["dataset_path"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "磁盘数据集路径（要素类/表等）。"
            }
        },
        ["required"] = new[] { "dataset_path" }
    };

    protected override string CategoryName => ToolCategories.Python;
    protected override string ExecutionTypeName => ExecutionTypes.Python;
    protected override bool? RequiresArcGISOverride => false;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var bridge = context.Python;
        if (bridge is null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.PythonBridgeUnavailable,
                "Python Bridge service is not available in this host.");
        }

        var path = ToolArgs.GetString(context, "dataset_path");
        var r = await bridge.ListFieldsAsync(path ?? string.Empty, context.CancellationToken).ConfigureAwait(false);
        return r.Success
            ? OperationResult<object?>.Ok(r.Data.HasValue ? r.Data.Value : JsonValueKind.Null)
            : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>指定 workspace 的顶层数据集发现（Python Bridge / ArcPy，top-level only）。</summary>
public sealed class ListWorkspaceDatasetsTool : McpToolBase
{
    public override string Name => "list_workspace_datasets";

    public override string Description =>
        "枚举指定 workspace（FileGDB 或文件夹）的数据集：要素类/要素数据集/表/**栅格**（rasterDatasets 为真实枚举）。 " +
        "rasterEnumeration 字段区分 done/skipped/unsupported（\"确实为空\"与\"未枚举\"机器可判）。 " +
        "recursive=true 时递归（FeatureDataset 子要素类 / 文件夹子目录，受 max_depth/max_items 上限约束，" +
        "超限 truncated=true + truncationReason）。junction/symlink 逃出声明根 → PATH_ESCAPE_REJECTED。不访问 CURRENT。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["workspace_path"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "workspace 路径（如 FileGDB 或文件夹）。"
            },
            ["recursive"] = new Dictionary<string, object?>
            {
                ["type"] = "boolean",
                ["description"] = "递归枚举（FeatureDataset 子要素类 / 文件夹子目录）。默认 false。",
                ["default"] = false
            },
            ["max_depth"] = new Dictionary<string, object?>
            {
                ["type"] = "integer",
                ["description"] = "递归深度上限（1-5）。默认 3。仅 recursive=true 时生效。",
                ["default"] = 3,
                ["minimum"] = 1,
                ["maximum"] = 5
            },
            ["max_items"] = new Dictionary<string, object?>
            {
                ["type"] = "integer",
                ["description"] = "单响应条目上限（1-2000）。超限截断并置 truncated=true + truncationReason。默认 500。",
                ["default"] = 500,
                ["minimum"] = 1,
                ["maximum"] = 2000
            }
        },
        ["required"] = new[] { "workspace_path" }
    };

    protected override string CategoryName => ToolCategories.Python;
    protected override string ExecutionTypeName => ExecutionTypes.Python;
    protected override bool? RequiresArcGISOverride => false;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var bridge = context.Python;
        if (bridge is null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.PythonBridgeUnavailable,
                "Python Bridge service is not available in this host.");
        }

        var ws = ToolArgs.GetString(context, "workspace_path");
        var recursive = ToolArgs.GetBool(context, "recursive", false);
        var maxDepth = ToolArgs.GetInt(context, "max_depth") ?? 3;
        var maxItems = ToolArgs.GetInt(context, "max_items") ?? 500;
        var r = await bridge.ListWorkspaceDatasetsAsync(ws ?? string.Empty, recursive, maxDepth, maxItems, context.CancellationToken).ConfigureAwait(false);
        return r.Success
            ? OperationResult<object?>.Ok(r.Data.HasValue ? r.Data.Value : JsonValueKind.Null)
            : OperationResult<object?>.Fail(r.Errors);
    }
}
