using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>获取许可信息工具。</summary>
public sealed class GetLicenseInfoTool : IMCPTool
{
    public string Name => "get_license_info";

    public string Description => "返回 ArcGIS Pro 许可信息（LicensingInfo）。";

    public IReadOnlyDictionary<string, object?> InputSchema => ToolSchemas.Object();

    public async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var result = await context.Host.License.GetLicenseInfoAsync(context.CancellationToken).ConfigureAwait(false);
        return result.Success
            ? OperationResult<object?>.Ok(result.Data, result.Message)
            : OperationResult<object?>.Fail(result.Errors);
    }
}
