using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-088 M2 批一：P1/P2 质量与溯源只读分析服务契约。
/// 实现保持 ArcGIS SDK 访问在 Compatibility 层（Rule 5）。
/// </summary>
public interface ID088QualityService
{
    Task<OperationResult<IReadOnlyDictionary<string, object?>>> ValidateGeometriesAsync(
        string dataset, IReadOnlyList<string> checks, double minimumSegmentLength, int maxFeatures, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> CheckTopologyRulesAsync(
        string dataset, IReadOnlyList<string> rules, double clusterTolerance, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> CompareDatasetsAsync(
        string left, string right, IReadOnlyList<string> keyFields, bool compareGeometry, double tolerance, int maxFeatures, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> CompareSchemasAsync(
        string left, string right, bool ignoreOrder, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> ValidateFieldConstraintsAsync(
        string dataset, IReadOnlyList<IReadOnlyDictionary<string, object?>> constraints, int maxFeatures, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> InspectRasterAlignmentAsync(
        IReadOnlyList<string> rasters, string? reference, double snapTolerance, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> ListGeographicTransformationsAsync(
        string sourceCrs, string targetCrs, IReadOnlyDictionary<string, object?>? extent, int maxItems, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> TraceDatasetDependenciesAsync(
        string root, int maxDepth, bool includeBroken, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> GetDatasetLineageAsync(
        string dataset, int maxDepth, bool includeArtifacts, CancellationToken ct = default);
}
