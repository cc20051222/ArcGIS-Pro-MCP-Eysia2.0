using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-092 · M2 P3/P4 域治理与数据管理服务（ArcGIS Pro SDK / GP 实现）。
/// Rule 4：所有 SDK 对象访问经 QueuedTask.Run。
/// Rule 5：SDK 引用只在 Compatibility 层。
/// 真机验证状态：NOT VERIFIED（禁安装；SDK/GP 依赖件如实登记）。
/// </summary>
public sealed class D092DomainService : ID092DomainService
{
    private static readonly string NotVerified = "requires a live ArcGIS Pro session (SDK/GP dependency; NOT VERIFIED in build-only mode).";

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CreateDomainAsync(
        string workspace, string domainName, string domainType,
        IReadOnlyList<object?> codedValues, IReadOnlyDictionary<string, object?>? range, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"create_domain {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> UpdateDomainAsync(
        string workspace, string domainName,
        IReadOnlyList<object?>? codedValues, IReadOnlyDictionary<string, object?>? range, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"update_domain {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> DeleteDomainAsync(
        string workspace, string domainName, bool force, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"delete_domain {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> AssignDomainToFieldAsync(
        string workspace, string dataset, string field, string domainName, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"assign_domain_to_field {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> RemoveDomainFromFieldAsync(
        string workspace, string dataset, string field, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"remove_domain_from_field {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ConfigureSubtypesAsync(
        string dataset, string subtypeField, IReadOnlyList<object?> subtypes, bool clearExisting, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"configure_subtypes {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CreateRelationshipClassAsync(
        string originTable, string destinationTable, string relationshipName, string cardinality,
        string? forwardLabel, string? backwardLabel, bool attributed, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"create_relationship_class {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CalculateGeometryAttributesAsync(
        string dataset, IReadOnlyList<object?> fields, string units, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"calculate_geometry_attributes {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> DefineProjectionAsync(
        string dataset, string crs, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"define_projection {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ValidateRelationshipClassAsync(
        string relationshipName, string? workspace, int maxItems, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"validate_relationship_class {NotVerified}"));
}
