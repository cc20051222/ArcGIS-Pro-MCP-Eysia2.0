using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>列出工程中的所有布局。</summary>
public sealed class ListLayoutsTool : McpToolBase
{
    public override string Name => "list_layouts";
    public override string Description => "列出当前 ArcGIS Pro 工程中的所有布局（制图输出页面）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => Core.Tools.ToolSchemas.Object();
    protected override string CategoryName => ToolCategories.Layout;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var r = await context.Host.Project.ListLayoutsAsync(context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>列出工程中的所有数据库（file geodatabase / database）。</summary>
public sealed class ListDatabasesTool : McpToolBase
{
    public override string Name => "list_databases";
    public override string Description => "列出当前 ArcGIS Pro 工程中的数据库连接（file geodatabase 等）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => Core.Tools.ToolSchemas.Object();
    protected override string CategoryName => ToolCategories.Project;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var r = await context.Host.Project.ListDatabasesAsync(context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}
