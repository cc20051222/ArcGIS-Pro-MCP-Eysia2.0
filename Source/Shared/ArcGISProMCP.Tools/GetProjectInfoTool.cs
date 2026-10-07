using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>获取工程信息工具。</summary>
public sealed class GetProjectInfoTool : IMCPTool
{
    public string Name => "get_project_info";

    public string Description => "返回当前 ArcGIS Pro 工程信息（ProjectInfo）。";

    public IReadOnlyDictionary<string, object?> InputSchema => ToolSchemas.Object();

    public async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var result = await context.Host.Project.GetProjectInfoAsync(context.CancellationToken).ConfigureAwait(false);
        return result.Success
            ? OperationResult<object?>.Ok(result.Data, result.Message)
            : OperationResult<object?>.Fail(result.Errors);
    }
}
