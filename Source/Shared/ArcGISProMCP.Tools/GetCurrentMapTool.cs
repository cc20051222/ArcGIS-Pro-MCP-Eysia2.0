using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>获取当前地图工具。</summary>
public sealed class GetCurrentMapTool : IMCPTool
{
    public string Name => "get_current_map";

    public string Description => "返回 ArcGIS Pro 当前活动地图（MapInfo）。";

    public IReadOnlyDictionary<string, object?> InputSchema => ToolSchemas.Object();

    public async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var result = await context.Host.Maps.GetCurrentMapAsync(context.CancellationToken).ConfigureAwait(false);
        return result.Success
            ? OperationResult<object?>.Ok(result.Data, result.Message)
            : OperationResult<object?>.Fail(result.Errors);
    }
}
