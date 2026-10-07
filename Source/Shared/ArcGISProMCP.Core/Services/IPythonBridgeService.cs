using System.Text.Json;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// Python Bridge 高层服务接口（Phase 5.4）。
/// 供未来 Python Tool 使用；实现基于 PythonBridgeProcessManager（transport），
/// 返回统一 OperationResult（business boundary）。不暴露 Process/Stream 等底层对象。
/// </summary>
public interface IPythonBridgeService
{
    /// <summary>桥接可用性检查（ping）。返回文本 "pong" 或失败。</summary>
    Task<OperationResult<string>> PingAsync(CancellationToken ct = default);

    /// <summary>获取 Python / ArcPy / ArcGIS API 运行时信息（原始 JSON 对象）。</summary>
    Task<OperationResult<JsonElement?>> GetRuntimeInfoAsync(CancellationToken ct = default);

    /// <summary>用真实 ArcPy 检查数据集路径是否存在。</summary>
    Task<OperationResult<bool>> ArcpyExistsAsync(string path, CancellationToken ct = default);

    /// <summary>用真实 ArcPy 描述数据集路径（原始 JSON 对象）。</summary>
    Task<OperationResult<JsonElement?>> DescribeAsync(string path, CancellationToken ct = default);

    // Phase 5.5.2：三个只读 production action（契约见 5.5.1/5.5.2）。
    // dataset_summary：不存在路径→Success + 结果体 exists=false（非错误，契约）。
    Task<OperationResult<JsonElement?>> DatasetSummaryAsync(string path, CancellationToken ct = default);

    /// <summary>磁盘数据集字段 schema 查询；不存在→NOT_FOUND。</summary>
    Task<OperationResult<JsonElement?>> ListFieldsAsync(string datasetPath, CancellationToken ct = default);

    /// <summary>顶层 workspace 数据集发现（Phase 8.2 扩展：递归/上限/栅格枚举）。</summary>
    Task<OperationResult<JsonElement?>> ListWorkspaceDatasetsAsync(
        string workspacePath, bool recursive = false, int maxDepth = 3, int maxItems = 500, CancellationToken ct = default);

    // Phase 8.2 (D-011)：get_dataset_info / get_raster_info 的 Bridge action（通道变更见 08 ADR）。
    /// <summary>轻量存在性 + Geo 类型判定；不存在 → Success + exists=false + reason。</summary>
    Task<OperationResult<JsonElement?>> DatasetInfoAsync(string path, CancellationToken ct = default);

    /// <summary>栅格全字段契约；statistics 哨兵 "unknown"（不现算）。</summary>
    Task<OperationResult<JsonElement?>> RasterInfoAsync(string datasetPath, CancellationToken ct = default);

    /// <summary>内部 fail-closed 输入资格检查；不注册为 MCP 工具。</summary>
    Task<OperationResult<JsonElement?>> RasterQualificationAsync(string datasetPath, CancellationToken ct = default)
        => Task.FromResult(OperationResult<JsonElement?>.Fail(
            ErrorCodes.NotImplemented, "Raster input qualification is not implemented by this bridge."));

    // Phase 8.3 (D-013, G-32)：select_by_attribute / select_by_location 的 Bridge GP action。
    /// <summary>按属性/OID 选择。oidList 与 where 二选一（调用方校验）；mode=replace|add|remove|switch。</summary>
    Task<OperationResult<JsonElement?>> SelectByAttributeAsync(
        string? mapName, string layerName, string mode,
        IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default);

    /// <summary>按空间关系选择。overlapType 白名单见设计 §2.2；mode=replace|add|remove（无 switch）。</summary>
    Task<OperationResult<JsonElement?>> SelectByLocationAsync(
        string? mapName, string layerName, string? selectingLayerName,
        string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default);
}
