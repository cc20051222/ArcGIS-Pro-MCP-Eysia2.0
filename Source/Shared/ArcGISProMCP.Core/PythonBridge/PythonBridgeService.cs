using System.Text.Json;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Core.PythonBridge;

/// <summary>
/// Python Bridge 高层服务实现（Phase 5.4）。
/// 职责：构造 PythonBridgeRequest → 调用 PythonBridgeProcessManager（transport）
/// → 解析 PythonBridgeResponse → 映射为统一 OperationResult / ErrorCodes。
/// 不负责进程启动/流/ArcPy（属 ProcessManager / Python 侧）。
/// </summary>
public sealed class PythonBridgeService : IPythonBridgeService
{
    private readonly PythonBridgeProcessManager _manager;
    private readonly ILogger _logger;

    public PythonBridgeService(PythonBridgeProcessManager manager, ILogger? logger = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<OperationResult<string>> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var call = await _manager.SendRequestAsync(PythonBridgeRequest.Ping(), ct).ConfigureAwait(false);
            if (!call.Ok || call.Data is null)
            {
                return OperationResult<string>.Fail(MapCode(call), call.ErrorMessage ?? "python ping failed");
            }

            var text = call.Data.Value.GetString() ?? call.Data.Value.ToString();
            return string.Equals(text, "pong", StringComparison.OrdinalIgnoreCase)
                ? OperationResult<string>.Ok(text!)
                : OperationResult<string>.Fail(ErrorCodes.ExecutionFailed, "unexpected ping response: " + text);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<string>.Fail(ErrorCodes.Cancelled, "python ping cancelled");
        }
    }

    public async Task<OperationResult<JsonElement?>> GetRuntimeInfoAsync(CancellationToken ct = default)
    {
        var call = await SendRawAsync(PythonBridgeRequest.RuntimeInfo(), ct).ConfigureAwait(false);
        return call;
    }

    public async Task<OperationResult<bool>> ArcpyExistsAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<bool>.Fail(ErrorCodes.InvalidArgument, "path is required");
        }

        var call = await _manager.SendRequestAsync(PythonBridgeRequest.Exists(path), ct).ConfigureAwait(false);
        if (!call.Ok || call.Data is null)
        {
            return OperationResult<bool>.Fail(MapCode(call), call.ErrorMessage ?? "arcpy_exists failed");
        }

        return OperationResult<bool>.Ok(call.Data.Value.ValueKind == JsonValueKind.True);
    }

    public async Task<OperationResult<JsonElement?>> DescribeAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "path is required");
        }

        return await SendRawAsync(PythonBridgeRequest.Describe(path), ct).ConfigureAwait(false);
    }

    // ---------------------------------------------------- Phase 5.5.2 production actions

    public async Task<OperationResult<JsonElement?>> DatasetSummaryAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "path is required");
        }

        // 契约：不存在路径→Success+exists=false（Python 侧保证）。长度/4096 边界属 Tool(5.5.3) 层校验。
        return await SendRawAsync(PythonBridgeRequest.DatasetSummary(path), ct).ConfigureAwait(false);
    }

    public async Task<OperationResult<JsonElement?>> ListFieldsAsync(string datasetPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(datasetPath))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "dataset_path is required");
        }

        return await SendRawAsync(PythonBridgeRequest.ListFields(datasetPath), ct).ConfigureAwait(false);
    }

    public async Task<OperationResult<JsonElement?>> ListWorkspaceDatasetsAsync(string workspacePath, bool recursive = false, int maxDepth = 3, int maxItems = 500, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "workspace_path is required");
        }

        return await SendRawAsync(PythonBridgeRequest.ListWorkspaceDatasets(workspacePath, recursive, maxDepth, maxItems), ct).ConfigureAwait(false);
    }

    // Phase 8.2 (D-011)：get_dataset_info / get_raster_info 的 Bridge action。
    public async Task<OperationResult<JsonElement?>> DatasetInfoAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "path is required");
        }

        return await SendRawAsync(PythonBridgeRequest.DatasetInfo(path), ct).ConfigureAwait(false);
    }

    public async Task<OperationResult<JsonElement?>> RasterInfoAsync(string datasetPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(datasetPath))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "dataset_path is required");
        }

        return await SendRawAsync(PythonBridgeRequest.RasterInfo(datasetPath), ct).ConfigureAwait(false);
    }

    public async Task<OperationResult<JsonElement?>> RasterQualificationAsync(string datasetPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(datasetPath))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "dataset_path is required");
        }

        return await SendRawAsync(PythonBridgeRequest.RasterQualification(datasetPath), ct).ConfigureAwait(false);
    }

    // Phase 8.3 (D-013, G-32)：选择写入 Bridge GP action。
    public async Task<OperationResult<JsonElement?>> SelectByAttributeAsync(
        string? mapName, string layerName, string mode,
        IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "layerName is required");
        }

        return await SendRawAsync(
            PythonBridgeRequest.SelectByAttribute(mapName, layerName, mode, oidList, where), ct).ConfigureAwait(false);
    }

    public async Task<OperationResult<JsonElement?>> SelectByLocationAsync(
        string? mapName, string layerName, string? selectingLayerName,
        string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "layerName is required");
        }

        if (string.IsNullOrWhiteSpace(overlapType))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "overlapType is required");
        }

        return await SendRawAsync(
            PythonBridgeRequest.SelectByLocation(mapName, layerName, selectingLayerName, overlapType, searchDistance, searchDistanceUnit, mode), ct).ConfigureAwait(false);
    }

    // ------------------------------------------------------------ helpers

    private async Task<OperationResult<JsonElement?>> SendRawAsync(
        PythonBridgeRequest request, CancellationToken ct)
    {
        try
        {
            var call = await _manager.SendRequestAsync(request, ct).ConfigureAwait(false);
            if (!call.Ok)
            {
                return OperationResult<JsonElement?>.Fail(
                    MapCode(call),
                    call.ErrorMessage ?? "python bridge error",
                    null);
            }

            return OperationResult<JsonElement?>.Ok(call.Data);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.Cancelled, "python request cancelled");
        }
    }

    private static string MapCode(PythonBridgeCallResult call)
    {
        // transport 层的错误码透传；进程不可用统一映射（其余保留 Python 侧语义码）。
        return call.ErrorCode switch
        {
            ErrorCodes.PythonBridgeUnavailable => ErrorCodes.PythonBridgeUnavailable,
            null => ErrorCodes.InternalError,
            _ => call.ErrorCode,
        };
    }
}
