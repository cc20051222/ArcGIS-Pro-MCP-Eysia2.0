using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-094 M2 批三：P5 八件分析工具服务契约（全 W，189→197）。
/// 实现保持 ArcGIS SDK / GP 访问在 Compatibility 层（Rule 5）。
/// </summary>
public interface ID094AnalysisService
{
    Task<OperationResult<IReadOnlyDictionary<string, object?>>> SimplifyFeaturesAsync(
        string input, string outputPath, string algorithm, double tolerance,
        bool preserveTopology, bool overwrite, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> SmoothFeaturesAsync(
        string input, string outputPath, string algorithm, double tolerance,
        bool preserveEndpoints, bool overwrite, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> PolygonNeighborsAsync(
        string input, string outputPath, bool includeEdgeLength, bool includePointTouches,
        bool overwrite, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> GenerateTessellationAsync(
        IReadOnlyDictionary<string, object?> extent, string outputPath, string shapeType,
        double size, bool overwrite, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> CalculateServiceAreasAsync(
        string network, string facilities, string outputPath, IReadOnlyList<object?> breaks,
        string impedance, string travelDirection, bool overwrite, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> SolveRoutesAsync(
        string network, string stops, string outputPath, string impedance,
        bool findBestOrder, bool preserveFirstLast, bool overwrite, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> SpatialAutocorrelationAsync(
        string input, string outputPath, string field, string conceptualization,
        string standardization, bool local, bool overwrite, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> HotspotAnalysisAsync(
        string input, string outputPath, string field, string conceptualization,
        bool falseDiscoveryRate, bool local, bool overwrite, CancellationToken ct = default);
}
