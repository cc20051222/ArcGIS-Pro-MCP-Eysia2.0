using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-088 · M2 P1/P2 质量与溯源分析服务（ArcGIS Pro SDK 实现）。
/// Rule 4：所有 SDK 对象访问经 QueuedTask.Run。
/// Rule 5：SDK 引用只在 Compatibility 层。
/// 真机验证状态：NOT VERIFIED（禁安装；SDK 依赖件如实登记）。
/// </summary>
public sealed partial class D088QualityService : ID088QualityService
{
    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ValidateGeometriesAsync(
        string dataset, IReadOnlyList<string> checks, double minimumSegmentLength, int maxFeatures, CancellationToken ct = default)
        => RunGeometryValidation(dataset, checks, minimumSegmentLength, maxFeatures, ct);

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CheckTopologyRulesAsync(
        string dataset, IReadOnlyList<string> rules, double clusterTolerance, CancellationToken ct = default)
        => RunTopologyValidation(dataset, rules, clusterTolerance, ct);

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CompareDatasetsAsync(
        string left, string right, IReadOnlyList<string> keyFields, bool compareGeometry, double tolerance, int maxFeatures, CancellationToken ct = default)
    {
        return Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(
            ErrorCodes.NotImplemented,
            "compare_datasets requires a live ArcGIS Pro session (SDK dependency; NOT VERIFIED in build-only mode)."));
    }

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CompareSchemasAsync(
        string left, string right, bool ignoreOrder, CancellationToken ct = default)
    {
        return Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(
            ErrorCodes.NotImplemented,
            "compare_schemas requires a live ArcGIS Pro session (SDK dependency; NOT VERIFIED in build-only mode)."));
    }

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ValidateFieldConstraintsAsync(
        string dataset, IReadOnlyList<IReadOnlyDictionary<string, object?>> constraints, int maxFeatures, CancellationToken ct = default)
    {
        return Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(
            ErrorCodes.NotImplemented,
            "validate_field_constraints requires a live ArcGIS Pro session (SDK dependency; NOT VERIFIED in build-only mode)."));
    }

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> InspectRasterAlignmentAsync(
        IReadOnlyList<string> rasters, string? reference, double snapTolerance, CancellationToken ct = default)
    {
        return Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(
            ErrorCodes.NotImplemented,
            "inspect_raster_alignment requires a live ArcGIS Pro session with Python Bridge (SDK dependency; NOT VERIFIED in build-only mode)."));
    }

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ListGeographicTransformationsAsync(
        string sourceCrs, string targetCrs, IReadOnlyDictionary<string, object?>? extent, int maxItems, CancellationToken ct = default)
    {
        return Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(
            ErrorCodes.NotImplemented,
            "list_geographic_transformations requires a live ArcGIS Pro session (SDK dependency; NOT VERIFIED in build-only mode)."));
    }

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> TraceDatasetDependenciesAsync(
        string root, int maxDepth, bool includeBroken, CancellationToken ct = default)
    {
        return Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(
            ErrorCodes.NotImplemented,
            "trace_dataset_dependencies requires a live ArcGIS Pro session (SDK dependency; NOT VERIFIED in build-only mode)."));
    }

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> GetDatasetLineageAsync(
        string dataset, int maxDepth, bool includeArtifacts, CancellationToken ct = default)
    {
        return Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(
            ErrorCodes.NotImplemented,
            "get_dataset_lineage requires a live ArcGIS Pro session (SDK dependency; NOT VERIFIED in build-only mode)."));
    }
}
