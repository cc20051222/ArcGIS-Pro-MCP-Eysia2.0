using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-092 M2 批二：P3/P4 域治理与数据管理服务契约（9W＋1R）。
/// 实现保持 ArcGIS SDK / GP 访问在 Compatibility 层（Rule 5）。
/// </summary>
public interface ID092DomainService
{
    Task<OperationResult<IReadOnlyDictionary<string, object?>>> CreateDomainAsync(
        string workspace, string domainName, string domainType,
        IReadOnlyList<object?> codedValues, IReadOnlyDictionary<string, object?>? range, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> UpdateDomainAsync(
        string workspace, string domainName,
        IReadOnlyList<object?>? codedValues, IReadOnlyDictionary<string, object?>? range, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> DeleteDomainAsync(
        string workspace, string domainName, bool force, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> AssignDomainToFieldAsync(
        string workspace, string dataset, string field, string domainName, bool overwrite, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> RemoveDomainFromFieldAsync(
        string workspace, string dataset, string field, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> ConfigureSubtypesAsync(
        string dataset, string subtypeField, IReadOnlyList<object?> subtypes, bool clearExisting, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> CreateRelationshipClassAsync(
        string originTable, string destinationTable, string relationshipName, string cardinality,
        string? forwardLabel, string? backwardLabel, bool attributed, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> CalculateGeometryAttributesAsync(
        string dataset, IReadOnlyList<object?> fields, string units, bool overwrite, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> DefineProjectionAsync(
        string dataset, string crs, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyDictionary<string, object?>>> ValidateRelationshipClassAsync(
        string relationshipName, string? workspace, int maxItems, CancellationToken ct = default);
}
