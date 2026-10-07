using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// Schema 只读信息服务（Phase 9 第一批，D-034）。
/// 契约：路径空白 = 非法参数；`exists=false` + reason 不掩盖；空态区分"确实为空"与"未支持/未枚举"；
/// 容器内（GDB）用 SDK 定义枚举；错误码复用既有 32 码，不新增。
/// </summary>
public interface ISchemaService
{
    /// <summary>数据集 schema：字段列表 + 几何/空间参考（要素类）。GDB 内路径或文件路径。</summary>
    Task<OperationResult<SchemaInfo>> GetSchemaInfoAsync(string path, CancellationToken ct = default);

    /// <summary>GDB 工作空间域列表（名称/类型/编码值或范围）；非 GDB 工作空间 → 空 + 显式说明。分页上限 maxItems。</summary>
    Task<OperationResult<IReadOnlyList<DomainInfo>>> GetDomainsAsync(string workspace, int maxItems, CancellationToken ct = default);

    /// <summary>要素类/表子类型：子类型字段 + 码→名映射；无子类型 → 空（区分"确实为空"）。</summary>
    Task<OperationResult<SubtypeInfo>> GetSubtypesAsync(string path, CancellationToken ct = default);

    /// <summary>要素类/表索引列表（名称/字段/唯一/空间）；分页上限 maxItems。</summary>
    Task<OperationResult<IReadOnlyList<IndexInfo>>> GetIndexesAsync(string path, int maxItems, CancellationToken ct = default);

    /// <summary>
    /// D-079 · A2/A3（**只读**）：枚举容器（文件地理数据库）内的**直系子项**并尽力给出行数
    /// （要素类/表 = 计数；要素集/栅格等容器型或非行式项 = null + 原因）。
    /// 上限 maxItems（超出 ⇒ 调用方按截断披露）；不递归进要素集（限深 1 层）。
    /// </summary>
    /// <remarks>以**默认接口实现**提供（既有宿主实现与测试替身零改动即可编译）；未覆写 ⇒ NOT_IMPLEMENTED。</remarks>
    Task<OperationResult<IReadOnlyList<DatasetMemberInfo>>> ListDatasetMembersAsync(
        string containerPath, int maxItems, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<DatasetMemberInfo>>.Fail(
            ErrorCodes.NotImplemented, "Container member enumeration is not implemented by this host."));

    // ══════════════════════════ D-064 · A 段：Schema 创建（写类） ══════════════════════════
    //  契约面（工具层强制，宿主只负责执行与写后读回）：
    //  · 输出路径守卫（ProtectedOutputPathGuard）+ 覆写闸门（GpOverwriteGuard，缺省 OUTPUT_EXISTS）由工具层前置；
    //  · 就地修改类（add_fields / delete_field / truncate_table）以**输入数据集**过同一守卫；
    //  · 破坏性类（delete_field / truncate_table）由工具层做 confirm 缺省拒。
    //  默认实现一律 NOT_IMPLEMENTED（保既有宿主实现零改动编译）。

    /// <summary>D-064：在 GDB 内建要素类（几何类型 / 空间参考 / 字段集可选），可选加入地图。</summary>
    Task<OperationResult<CreateFeatureClassResult>> CreateFeatureClassAsync(
        string outputPath, string geometryType, string? spatialReference,
        IReadOnlyList<SchemaFieldSpec>? fields, bool addToMap, CancellationToken ct = default)
        => Task.FromResult(OperationResult<CreateFeatureClassResult>.Fail(
            ErrorCodes.NotImplemented, "Feature class creation is not implemented by this host."));

    /// <summary>D-064：在 GDB 内建独立表（字段集可选），可选加入地图。</summary>
    Task<OperationResult<CreateTableResult>> CreateTableAsync(
        string outputPath, IReadOnlyList<SchemaFieldSpec>? fields, bool addToMap, CancellationToken ct = default)
        => Task.FromResult(OperationResult<CreateTableResult>.Fail(
            ErrorCodes.NotImplemented, "Table creation is not implemented by this host."));

    /// <summary>D-064：**就地**批量加字段（缺省拒已存在；返回加前/加后字段清单）。</summary>
    Task<OperationResult<AddFieldsResult>> AddFieldsAsync(
        string path, IReadOnlyList<SchemaFieldSpec> fields, CancellationToken ct = default)
        => Task.FromResult(OperationResult<AddFieldsResult>.Fail(
            ErrorCodes.NotImplemented, "Batch field addition is not implemented by this host."));

    /// <summary>D-064：**就地**删字段（破坏性；confirm 由工具层前置）。</summary>
    Task<OperationResult<DeleteFieldResult>> DeleteFieldAsync(
        string path, string fieldName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<DeleteFieldResult>.Fail(
            ErrorCodes.NotImplemented, "Field deletion is not implemented by this host."));

    /// <summary>D-064：**就地**清空全部行（保 schema；破坏性；confirm 由工具层前置）+ 行数前后照。</summary>
    Task<OperationResult<TruncateTableResult>> TruncateTableAsync(string path, CancellationToken ct = default)
        => Task.FromResult(OperationResult<TruncateTableResult>.Fail(
            ErrorCodes.NotImplemented, "Table truncation is not implemented by this host."));

    /// <summary>
    /// D-064：导出图层/表到新数据集（**尊重当前选择与定义查询**）。工具层负责输出守卫与覆写闸门。
    /// </summary>
    Task<OperationResult<ExportFeaturesResult>> ExportFeaturesAsync(
        string? mapName, string inputLayerName, string outputPath, string? whereClause, bool useSelection,
        CancellationToken ct = default)
        => Task.FromResult(OperationResult<ExportFeaturesResult>.Fail(
            ErrorCodes.NotImplemented, "Feature export is not implemented by this host."));
}
