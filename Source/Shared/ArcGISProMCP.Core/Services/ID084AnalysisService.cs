using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>D-084 B2 read-only geometry and quality analysis contract. Implementations keep ArcGIS SDK access in Compatibility.</summary>
public interface ID084AnalysisService
{
    Task<OperationResult<IReadOnlyDictionary<string, object?>>> GetGeometryInfoAsync(
        string layerOrPath, string geometryUnit, int maxFeatures, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> FindIdenticalAsync(
        string layerOrPath, string mode, IReadOnlyList<string> fields, double tolerance, int maxFeatures, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> GenerateQualityReportAsync(
        string dataset, IReadOnlyList<string> rules, string severityFloor, int maxFeatures, CancellationToken ct = default);
}
