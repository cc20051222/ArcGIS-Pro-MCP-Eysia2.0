using ArcGIS.Core.Licensing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>许可服务实现（真实读取 ArcGIS Pro 许可）。</summary>
public sealed class LicenseService : ILicenseService
{
    public async Task<OperationResult<LicensingInfo>> GetLicenseInfoAsync(CancellationToken ct = default)
    {
        var level = await QueuedTask.Run(() => LicenseInformation.Level, TaskCreationOptions.None).ConfigureAwait(false);
        var info = new LicensingInfo
        {
            LicenseLevel = level.ToString() ?? string.Empty,
            ExtensionsAvailable = Array.Empty<string>()
        };
        return OperationResult<LicensingInfo>.Ok(info);
    }

    public async Task<OperationResult<bool>> CheckExtensionAsync(string extensionCode, CancellationToken ct = default)
    {
        if (!Enum.TryParse<LicenseCodes>(extensionCode, true, out var code))
        {
            return OperationResult<bool>.Fail(ErrorCodes.InvalidArgument, $"Unknown extension code '{extensionCode}'.");
        }

        var available = await QueuedTask.Run(
            () => LicenseInformation.GetAvailabilityCount(code) > 0,
            TaskCreationOptions.None).ConfigureAwait(false);
        return OperationResult<bool>.Ok(available);
    }

    public async Task<OperationResult<bool>> RequireExtensionAsync(string extensionCode, CancellationToken ct = default)
    {
        var check = await CheckExtensionAsync(extensionCode, ct).ConfigureAwait(false);
        if (!check.Success)
        {
            return check;
        }

        if (check.Data)
        {
            return check;
        }

        return OperationResult<bool>.Fail(
            ErrorCodes.LicenseRequired,
            $"Extension '{extensionCode}' license is required but not available.");
    }

    /// <summary>D-062 · A5：扩展许可检查（可选签出）。</summary>
    public async Task<OperationResult<ExtensionCheckInfo>> CheckExtensionDetailedAsync(
        string extensionCode, bool checkout, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(extensionCode))
        {
            return OperationResult<ExtensionCheckInfo>.Fail(
                ErrorCodes.InvalidArgument, "extensionCode is required (e.g. SpatialAnalyst).");
        }

        if (!Enum.TryParse<LicenseCodes>(extensionCode, true, out var code))
        {
            return OperationResult<ExtensionCheckInfo>.Fail(
                ErrorCodes.InvalidArgument, $"Unknown extension code '{extensionCode}'.");
        }

        return await QueuedTask.Run<OperationResult<ExtensionCheckInfo>>(
            () =>
            {
                var info = new ExtensionCheckInfo
                {
                    Code = code.ToString(),
                    Available = LicenseInformation.GetAvailabilityCount(code) > 0,
                };

                if (checkout)
                {
                    info.CheckoutAttempted = true;
                    try
                    {
                        // SDK 3.x：LicenseInformation.CheckoutLicense（签出）+ IsCheckedOut（真值复核）。
                        LicenseInformation.CheckoutLicense(code);
                        info.CheckoutSucceeded = LicenseInformation.IsCheckedOut(code);
                    }
                    catch (Exception ex)
                    {
                        // 签出异常 → 如实回报失败（不抛错，不臆测）。
                        info.CheckoutSucceeded = false;
                        return OperationResult<ExtensionCheckInfo>.Ok(info,
                            "checkout attempt threw: " + ex.Message);
                    }
                }

                return OperationResult<ExtensionCheckInfo>.Ok(info);
            },
            TaskCreationOptions.None).ConfigureAwait(false);
    }
}
