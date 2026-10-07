using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>许可服务。</summary>
public interface ILicenseService
{
    Task<OperationResult<LicensingInfo>> GetLicenseInfoAsync(CancellationToken ct = default);

    Task<OperationResult<bool>> CheckExtensionAsync(string extensionCode, CancellationToken ct = default);

    Task<OperationResult<bool>> RequireExtensionAsync(string extensionCode, CancellationToken ct = default);

    /// <summary>
    /// D-062 · A5：扩展许可检查（可选签出）。checkout=true 时尝试签出并如实回报是否成功；
    /// 签出失败不抛错（Ok + CheckoutSucceeded=false，交由调用方判定）。
    /// </summary>
    Task<OperationResult<ExtensionCheckInfo>> CheckExtensionDetailedAsync(
        string extensionCode, bool checkout, CancellationToken ct = default)
        => Task.FromResult(OperationResult<ExtensionCheckInfo>.Fail(
            ErrorCodes.NotImplemented, "CheckExtensionDetailedAsync is not implemented by this host."));
}
