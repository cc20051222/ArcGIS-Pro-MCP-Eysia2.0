using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-094 · M2 P5 八件分析工具服务（ArcGIS Pro SDK / GP 实现）。
/// Rule 4：所有 SDK 对象访问经 QueuedTask.Run。
/// Rule 5：SDK 引用只在 Compatibility 层。
/// 真机验证状态：NOT VERIFIED（禁安装；SDK/GP 依赖件如实登记）。
/// [VERIFY] calculate_service_areas / solve_routes 系网络分析族——Network Analyst 许可未核；
///          GP 白名单本批零改（53 恒定），所需 GP 函数准入待安装批真机核实。
/// </summary>
public sealed class D094AnalysisService : ID094AnalysisService
{
    private static readonly string NotVerified = "requires a live ArcGIS Pro session (SDK/GP dependency; NOT VERIFIED in build-only mode).";

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> SimplifyFeaturesAsync(
        string input, string outputPath, string algorithm, double tolerance,
        bool preserveTopology, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"simplify_features {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> SmoothFeaturesAsync(
        string input, string outputPath, string algorithm, double tolerance,
        bool preserveEndpoints, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"smooth_features {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> PolygonNeighborsAsync(
        string input, string outputPath, bool includeEdgeLength, bool includePointTouches,
        bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"polygon_neighbors {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> GenerateTessellationAsync(
        IReadOnlyDictionary<string, object?> extent, string outputPath, string shapeType,
        double size, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"generate_tessellation {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CalculateServiceAreasAsync(
        string network, string facilities, string outputPath, IReadOnlyList<object?> breaks,
        string impedance, string travelDirection, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"calculate_service_areas {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> SolveRoutesAsync(
        string network, string stops, string outputPath, string impedance,
        bool findBestOrder, bool preserveFirstLast, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"solve_routes {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> SpatialAutocorrelationAsync(
        string input, string outputPath, string field, string conceptualization,
        string standardization, bool local, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"spatial_autocorrelation {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> HotspotAnalysisAsync(
        string input, string outputPath, string field, string conceptualization,
        bool falseDiscoveryRate, bool local, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"hotspot_analysis {NotVerified}"));
}
