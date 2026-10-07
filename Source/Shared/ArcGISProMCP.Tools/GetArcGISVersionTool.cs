using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>获取 ArcGIS 版本工具。</summary>
public sealed class GetArcGISVersionTool : IMCPTool
{
    public string Name => "get_arcgis_version";

    public string Description => "返回 ArcGIS Pro 版本信息（ArcGISVersionInfo）。";

    public IReadOnlyDictionary<string, object?> InputSchema => ToolSchemas.Object();

    public async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var result = await context.Host.Version.GetVersionAsync(context.CancellationToken).ConfigureAwait(false);
        return result.Success
            ? OperationResult<object?>.Ok(result.Data, result.Message)
            : OperationResult<object?>.Fail(result.Errors);
    }
}
