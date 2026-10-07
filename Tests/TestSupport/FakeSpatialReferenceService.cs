using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.TestSupport;

/// <summary>
/// D-037（F-D035-2）测试替身：可编程的坐标系解析服务（不触碰 ArcGIS Pro SDK）。
/// 预置条目用于验证"名称 → WKID"解析；<see cref="Resolves"/> = false 时模拟"宿主侧无此坐标系"。
/// </summary>
public sealed class FakeSpatialReferenceService : ISpatialReferenceService
{
    /// <summary>预置坐标系条目（WKID + 名称）。</summary>
    public List<SpatialReferenceEntry> Entries { get; } = new();

    /// <summary>false → 一律返回未解析（模拟名称不在 Pro 预置清单内）。</summary>
    public bool Resolves { get; set; } = true;

    /// <summary>false → 返回失败结果（模拟宿主服务异常，调用方须如实报错并交回 GP）。</summary>
    public bool Succeeds { get; set; } = true;

    /// <summary>收到的原始输入（断言用）。</summary>
    public List<string?> Calls { get; } = new();

    public Task<OperationResult<SpatialReferenceResolution>> ResolveAsync(string? value, CancellationToken ct = default)
    {
        Calls.Add(value);

        if (!Succeeds)
        {
            return Task.FromResult(OperationResult<SpatialReferenceResolution>.Fail(
                ErrorCodes.ArcGISError, "spatial reference service unavailable (test)"));
        }

        if (SpatialReferenceName.IsWkidLiteral(value, out var literal))
        {
            return Task.FromResult(OperationResult<SpatialReferenceResolution>.Ok(
                new SpatialReferenceResolution(literal, null, "wkid-literal"), "wkid-literal"));
        }

        if (Resolves && SpatialReferenceName.TryResolve(value, Entries, out var match) && match is not null)
        {
            return Task.FromResult(OperationResult<SpatialReferenceResolution>.Ok(
                new SpatialReferenceResolution(match.Wkid, match.Name, "sdk-predefined-list"), "sdk-predefined-list"));
        }

        return Task.FromResult(OperationResult<SpatialReferenceResolution>.Ok(
            new SpatialReferenceResolution(0, null, "unresolved-name"), "unresolved-name"));
    }
}

/// <summary>
/// D-037（F-D035-1）测试替身：可编程的 schema 服务（用于核验 export_table 的输出字段集）。
/// </summary>
public sealed class FakeFieldProbeSchemaService : ISchemaService
{
    /// <summary>GetSchemaInfoAsync 返回的字段列表（空 = 模拟"读不到 schema"，如 CSV）。</summary>
    public List<SchemaFieldInfo> Fields { get; set; } = new();

    /// <summary>false → GetSchemaInfoAsync 失败。</summary>
    public bool Succeeds { get; set; } = true;

    /// <summary>被探测过的路径（断言用）。</summary>
    public List<string> SchemaChecks { get; } = new();

    public Task<OperationResult<SchemaInfo>> GetSchemaInfoAsync(string path, CancellationToken ct = default)
    {
        SchemaChecks.Add(path);
        if (!Succeeds)
        {
            return Task.FromResult(OperationResult<SchemaInfo>.Fail(ErrorCodes.DatasetNotFound, "schema probe failed (test)"));
        }

        return Task.FromResult(OperationResult<SchemaInfo>.Ok(new SchemaInfo
        {
            Path = path,
            Exists = true,
            DataType = "Table",
            Fields = Fields,
        }));
    }

    public Task<OperationResult<IReadOnlyList<DomainInfo>>> GetDomainsAsync(string workspace, int maxItems, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<DomainInfo>>.Ok(new List<DomainInfo>()));

    public Task<OperationResult<SubtypeInfo>> GetSubtypesAsync(string path, CancellationToken ct = default)
        => Task.FromResult(OperationResult<SubtypeInfo>.Ok(new SubtypeInfo { Path = path }));

    public Task<OperationResult<IReadOnlyList<IndexInfo>>> GetIndexesAsync(string path, int maxItems, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<IndexInfo>>.Ok(new List<IndexInfo>()));
}
