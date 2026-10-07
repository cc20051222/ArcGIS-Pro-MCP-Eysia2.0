using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>数据集 schema 信息（字段/几何/空间参考）。Phase 9 第一批（D-034）。Native 通道。</summary>
public sealed class GetSchemaInfoTool : McpToolBase
{
    public override string Name => "get_schema_info";
    public override string Description =>
        "返回数据集 schema 信息：字段列表（name/type/nullable/default/length/alias/domain）+" +
        "几何类型与空间参考（要素类）。参数：path（GDB 内数据集路径或文件路径）。" +
        "契约：不存在路径成功返回 exists=false + reason（不掩盖）；GDB 容器内用 SDK 定义枚举；空白 path = 非法参数。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["path"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Dataset path (GDB internal dataset or file path)." }
        },
        ["required"] = new[] { "path" }
    };
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var path = ToolArgs.GetString(context, "path");
        var r = await context.Host.Schema.GetSchemaInfoAsync(path ?? string.Empty, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>GDB 工作空间域列表。Phase 9 第一批（D-034）。Native 通道。</summary>
public sealed class GetDomainsTool : McpToolBase
{
    public override string Name => "get_domains";
    public override string Description =>
        "返回 GDB 工作空间的域（Domain）列表：name/type（CodedValue 编码值域 / Range 范围域）/codedValues/range。参数：workspace（GDB 工作空间路径，如 ...\\.gdb）、maxItems（可选，默认 500，上限 2000）。" +
        "契约：非 GDB 工作空间 → 空 + 显式说明（确实为空）；分页上限不返回无界；空白 workspace = 非法参数。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["workspace"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "GDB workspace path." },
            ["maxItems"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Max result items (1-2000, default 500)." }
        },
        ["required"] = new[] { "workspace" }
    };
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var ws = ToolArgs.GetString(context, "workspace");
        var max = ToolArgs.GetInt(context, "maxItems") ?? 500;
        var r = await context.Host.Schema.GetDomainsAsync(ws ?? string.Empty, max, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>要素类/表子类型信息。Phase 9 第一批（D-034）。Native 通道。</summary>
public sealed class GetSubtypesTool : McpToolBase
{
    public override string Name => "get_subtypes";
    public override string Description =>
        "返回要素类/表的子类型（Subtype）信息：子类型字段名 + 子类型码→名映射。参数：path（GDB 内要素类/表）。" +
        "契约：无子类型 → 空 + 显式区分（确实为空 vs 未支持/不可判定）；不存在路径成功返回 exists=false + reason；空白 path = 非法参数。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["path"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Feature class / table path (GDB internal)." }
        },
        ["required"] = new[] { "path" }
    };
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var path = ToolArgs.GetString(context, "path");
        var r = await context.Host.Schema.GetSubtypesAsync(path ?? string.Empty, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>要素类/表索引列表。Phase 9 第一批（D-034）。Native 通道。</summary>
public sealed class GetIndexesTool : McpToolBase
{
    public override string Name => "get_indexes";
    public override string Description =>
        "返回要素类/表的索引列表：name/fields/isUnique/isSpatial。参数：path（GDB 内要素类/表）、maxItems（可选，默认 500，上限 2000）。" +
        "契约：只读；分页上限；不存在路径 → 空 + 显式说明；空白 path = 非法参数。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["path"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Feature class / table path (GDB internal)." },
            ["maxItems"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Max result items (1-2000, default 500)." }
        },
        ["required"] = new[] { "path" }
    };
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var path = ToolArgs.GetString(context, "path");
        var max = ToolArgs.GetInt(context, "maxItems") ?? 500;
        var r = await context.Host.Schema.GetIndexesAsync(path ?? string.Empty, max, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}
