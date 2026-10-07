using System.Text.Json;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.TestSupport;

/// <summary>
/// Configurable test double for the high-level Python facade. It records calls and
/// arguments without starting a Python process or duplicating production behavior.
/// </summary>
public sealed class FakePythonBridgeService : IPythonBridgeService
{
    public OperationResult<string> PingResult { get; set; } = OperationResult<string>.Ok("pong");
    public OperationResult<JsonElement?> RuntimeInfoResult { get; set; } = OperationResult<JsonElement?>.Ok(null);
    public OperationResult<bool> ArcpyExistsResult { get; set; } = OperationResult<bool>.Ok(false);
    public OperationResult<JsonElement?> DescribeResult { get; set; } = OperationResult<JsonElement?>.Ok(null);
    public OperationResult<JsonElement?> DatasetSummaryResult { get; set; } = OperationResult<JsonElement?>.Ok(null);
    public OperationResult<JsonElement?> ListFieldsResult { get; set; } = OperationResult<JsonElement?>.Ok(null);
    public OperationResult<JsonElement?> ListWorkspaceDatasetsResult { get; set; } = OperationResult<JsonElement?>.Ok(null);
    // Phase 8.2 (D-011)
    public OperationResult<JsonElement?> DatasetInfoResult { get; set; } = OperationResult<JsonElement?>.Ok(null);
    public OperationResult<JsonElement?> RasterInfoResult { get; set; } = OperationResult<JsonElement?>.Ok(null);
    public OperationResult<JsonElement?> RasterQualificationResult { get; set; } = OperationResult<JsonElement?>.Ok(null);
    public List<string> RasterQualificationPaths { get; } = new();

    // Phase 8.3 (D-013)
    public OperationResult<JsonElement?> SelectByAttributeResult { get; set; } = OperationResult<JsonElement?>.Ok(null);
    public OperationResult<JsonElement?> SelectByLocationResult { get; set; } = OperationResult<JsonElement?>.Ok(null);
    public (string? MapName, string LayerName, string Mode, IReadOnlyList<long>? OidList, string? Where)? LastSelectByAttribute { get; private set; }
    public (string? MapName, string LayerName, string? SelectingLayerName, string OverlapType, double? SearchDistance, string Mode)? LastSelectByLocation { get; private set; }

    public int PingCallCount { get; private set; }
    public int RuntimeInfoCallCount { get; private set; }
    public int ArcpyExistsCallCount { get; private set; }
    public int DescribeCallCount { get; private set; }
    public int DatasetSummaryCallCount { get; private set; }
    public int ListFieldsCallCount { get; private set; }
    public int ListWorkspaceDatasetsCallCount { get; private set; }

    public CancellationToken PingCancellationToken { get; private set; }
    public CancellationToken RuntimeInfoCancellationToken { get; private set; }
    public CancellationToken ArcpyExistsCancellationToken { get; private set; }
    public CancellationToken DescribeCancellationToken { get; private set; }
    public CancellationToken DatasetSummaryCancellationToken { get; private set; }
    public CancellationToken ListFieldsCancellationToken { get; private set; }
    public CancellationToken ListWorkspaceDatasetsCancellationToken { get; private set; }

    public string? ArcpyExistsPath { get; private set; }
    public string? DescribePath { get; private set; }
    public string? LastDatasetSummaryPath => DatasetSummaryPaths.LastOrDefault();
    public string? LastListFieldsPath => ListFieldsPaths.LastOrDefault();
    public string? LastListWorkspaceDatasetsPath => ListWorkspaceDatasetsPaths.LastOrDefault();
    public List<string> DatasetSummaryPaths { get; } = new();
    public List<string> ListFieldsPaths { get; } = new();
    public List<string> ListWorkspaceDatasetsPaths { get; } = new();

    public Task<OperationResult<string>> PingAsync(CancellationToken ct = default)
    {
        PingCallCount++;
        PingCancellationToken = ct;
        return Task.FromResult(PingResult);
    }

    public Task<OperationResult<JsonElement?>> GetRuntimeInfoAsync(CancellationToken ct = default)
    {
        RuntimeInfoCallCount++;
        RuntimeInfoCancellationToken = ct;
        return Task.FromResult(RuntimeInfoResult);
    }

    public Task<OperationResult<bool>> ArcpyExistsAsync(string path, CancellationToken ct = default)
    {
        ArcpyExistsCallCount++;
        ArcpyExistsPath = path;
        ArcpyExistsCancellationToken = ct;
        return Task.FromResult(ArcpyExistsResult);
    }

    public Task<OperationResult<JsonElement?>> DescribeAsync(string path, CancellationToken ct = default)
    {
        DescribeCallCount++;
        DescribePath = path;
        DescribeCancellationToken = ct;
        return Task.FromResult(DescribeResult);
    }

    public Task<OperationResult<JsonElement?>> DatasetSummaryAsync(string path, CancellationToken ct = default)
    {
        DatasetSummaryCallCount++;
        DatasetSummaryPaths.Add(path);
        DatasetSummaryCancellationToken = ct;
        return Task.FromResult(DatasetSummaryResult);
    }

    public Task<OperationResult<JsonElement?>> ListFieldsAsync(string datasetPath, CancellationToken ct = default)
    {
        ListFieldsCallCount++;
        ListFieldsPaths.Add(datasetPath);
        ListFieldsCancellationToken = ct;
        return Task.FromResult(ListFieldsResult);
    }

    public Task<OperationResult<JsonElement?>> ListWorkspaceDatasetsAsync(string workspacePath, bool recursive = false, int maxDepth = 3, int maxItems = 500, CancellationToken ct = default)
    {
        ListWorkspaceDatasetsCallCount++;
        ListWorkspaceDatasetsPaths.Add(workspacePath);
        ListWorkspaceDatasetsCancellationToken = ct;
        return Task.FromResult(ListWorkspaceDatasetsResult);
    }
    // Phase 8.2 (D-011)：fake 默认返回 null 载荷（工具层透传）；具体断言用 ServerTests 覆盖。
    public Task<OperationResult<JsonElement?>> DatasetInfoAsync(string path, CancellationToken ct = default)
        => Task.FromResult(DatasetInfoResult);

    public Task<OperationResult<JsonElement?>> RasterInfoAsync(string datasetPath, CancellationToken ct = default)
        => Task.FromResult(RasterInfoResult);

    public Task<OperationResult<JsonElement?>> RasterQualificationAsync(string datasetPath, CancellationToken ct = default)
    {
        RasterQualificationPaths.Add(datasetPath);
        return Task.FromResult(RasterQualificationResult);
    }
    // Phase 8.3 (D-013)
    public Task<OperationResult<JsonElement?>> SelectByAttributeAsync(
        string? mapName, string layerName, string mode,
        IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default)
    {
        LastSelectByAttribute = (mapName, layerName, mode, oidList, where);
        return Task.FromResult(SelectByAttributeResult);
    }

    public Task<OperationResult<JsonElement?>> SelectByLocationAsync(
        string? mapName, string layerName, string? selectingLayerName,
        string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default)
    {
        LastSelectByLocation = (mapName, layerName, selectingLayerName, overlapType, searchDistance, mode);
        return Task.FromResult(SelectByLocationResult);
    }
}
