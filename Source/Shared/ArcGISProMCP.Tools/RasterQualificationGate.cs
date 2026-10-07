using System.Text.Json;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Tools;

/// <summary>
/// Internal gate for raster inputs whose directory carries a qualification sidecar.
/// No public MCP tool, argument, registry entry, or error code is added.
/// </summary>
internal static class RasterQualificationGate
{
    public static async Task<OperationResult<bool>> ValidatePathsAsync(
        IPythonBridgeService? bridge,
        IEnumerable<string> paths,
        CancellationToken cancellationToken)
    {
        foreach (var rawPath in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var localPath = ToLocalPath(rawPath);
            if (!TryHasQualificationSidecar(localPath, out var hasSidecar, out var detectionError))
            {
                return OperationResult<bool>.Fail(
                    ErrorCodes.InvalidArgument,
                    $"Cannot determine whether raster input '{rawPath}' has a qualification sidecar: {detectionError}");
            }

            if (!hasSidecar)
            {
                continue;
            }

            if (bridge is null)
            {
                return OperationResult<bool>.Fail(
                    ErrorCodes.InvalidArgument,
                    $"Raster input '{rawPath}' has a qualification sidecar but the qualification bridge is unavailable.");
            }

            OperationResult<JsonElement?> qualification;
            try
            {
                qualification = await bridge.RasterQualificationAsync(localPath, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return OperationResult<bool>.Fail(ErrorCodes.Cancelled, "Raster qualification was cancelled before the write operation.");
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail(
                    ErrorCodes.InvalidArgument,
                    $"Raster qualification failed closed for '{rawPath}': {ex.Message}");
            }

            if (!qualification.Success || !qualification.Data.HasValue)
            {
                var detail = qualification.Errors is null
                    ? "no qualification result was returned"
                    : string.Join("; ", qualification.Errors.Select(error => error.Message));
                return OperationResult<bool>.Fail(
                    ErrorCodes.InvalidArgument,
                    $"Raster qualification failed closed for '{rawPath}': {detail}");
            }

            var payload = qualification.Data.Value;
            if (payload.ValueKind != JsonValueKind.Object
                || !payload.TryGetProperty("applies", out var applies)
                || applies.ValueKind != JsonValueKind.True
                || !payload.TryGetProperty("qualified", out var qualified)
                || qualified.ValueKind != JsonValueKind.True
                || !HasSha256(payload, "inputSha256")
                || !HasSha256(payload, "sidecarSha256"))
            {
                return OperationResult<bool>.Fail(
                    ErrorCodes.InvalidArgument,
                    $"Raster qualification failed closed for '{rawPath}': sidecar did not return a hash-bound qualified verdict.");
            }
        }

        return OperationResult<bool>.Ok(true);
    }

    private static bool HasSha256(JsonElement payload, string propertyName)
        => payload.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: 64 } hash
            && hash.All(Uri.IsHexDigit);

    private static string ToLocalPath(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return uri.LocalPath;
        }

        return path;
    }

    private static bool TryHasQualificationSidecar(string path, out bool hasSidecar, out string error)
    {
        hasSidecar = false;
        error = string.Empty;
        var extension = Path.GetExtension(path);
        if (!string.Equals(extension, ".tif", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".tiff", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return true;
            }

            hasSidecar = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                .Any(candidate => Path.GetFileName(candidate).Contains("qualification", StringComparison.OrdinalIgnoreCase));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
