using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArcGISProMCP.Core.PythonBridge;

/// <summary>
/// 发往 bridge_runner.py 的 NDJSON 请求（每行一个 JSON；字段名与 Python 端契约一致）。
/// </summary>
public sealed class PythonBridgeRequest
{
    public PythonBridgeRequest(string action, object? args = null, string? correlationId = null)
    {
        Id = correlationId ?? Guid.NewGuid().ToString("N");
        Action = action;
        Args = args;
    }

    /// <summary>correlation key（与 Python 响应 id 对应）。</summary>
    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("action")]
    public string Action { get; }

    [JsonPropertyName("args")]
    public object? Args { get; }

    public string ToLine()
    {
        return JsonSerializer.Serialize(this, PythonBridgeJson.Options);
    }

    public static PythonBridgeRequest Ping(string? id = null)
        => new("ping", correlationId: id);

    public static PythonBridgeRequest RuntimeInfo(string? id = null)
        => new("runtime_info", correlationId: id);

    public static PythonBridgeRequest Exists(string path, string? id = null)
        => new("arcpy_exists", new { path }, correlationId: id);

    public static PythonBridgeRequest Describe(string path, string? id = null)
        => new("describe", new { path }, correlationId: id);

    // Phase 5.5.2：三个只读 production action 工厂（契约见 5.5.1/5.5.2）。
    // 字段名与 bridge_runner.py 侧契约锁定，不得随意改名。
    public static PythonBridgeRequest DatasetSummary(string path, string? id = null)
        => new("dataset_summary", new { path }, correlationId: id);

    public static PythonBridgeRequest ListFields(string datasetPath, string? id = null)
        => new("list_fields", new { dataset_path = datasetPath }, correlationId: id);

    public static PythonBridgeRequest ListWorkspaceDatasets(string workspacePath, string? id = null)
        => new("list_workspace_datasets", new { workspace_path = workspacePath }, correlationId: id);

    public static PythonBridgeRequest ListWorkspaceDatasets(string workspacePath, bool recursive, int maxDepth, int maxItems, string? id = null)
        => new("list_workspace_datasets", new { workspace_path = workspacePath, recursive, max_depth = maxDepth, max_items = maxItems }, correlationId: id);

    // Phase 8.2 (D-011)：数据集轻量判定与栅格全字段（get_dataset_info / get_raster_info 的 Bridge action）。
    public static PythonBridgeRequest DatasetInfo(string path, string? id = null)
        => new("dataset_info", new { path }, correlationId: id);

    public static PythonBridgeRequest RasterInfo(string datasetPath, string? id = null)
        => new("raster_info", new { dataset_path = datasetPath }, correlationId: id);

    public static PythonBridgeRequest RasterQualification(string datasetPath, string? id = null)
        => new("raster_qualification", new { dataset_path = datasetPath }, correlationId: id);

    // Phase 8.3 (D-013, G-32)：选择写入走 Bridge GP（SelectLayerByAttribute/Location，Live Layer 操作）。
    public static PythonBridgeRequest SelectByAttribute(
        string? mapName, string layerName, string mode,
        IReadOnlyList<long>? oidList, string? where, string? id = null)
        => new("select_by_attribute", new
        {
            map_name = mapName,
            layer_name = layerName,
            mode,
            oid_list = oidList,
            where,
        }, correlationId: id);

    public static PythonBridgeRequest SelectByLocation(
        string? mapName, string layerName, string? selectingLayerName,
        string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, string? id = null)
        => new("select_by_location", new
        {
            map_name = mapName,
            layer_name = layerName,
            selecting_layer_name = selectingLayerName,
            overlap_type = overlapType,
            search_distance = searchDistance,
            search_distance_unit = searchDistanceUnit,
            mode,
        }, correlationId: id);

    /// <summary>
    /// 由 ProcessManager 注入 transport correlation。业务 factory 不拥有 wire identity。
    /// </summary>
    internal PythonBridgeRequest WithCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation id is required.", nameof(correlationId));
        }

        return new PythonBridgeRequest(Action, Args, correlationId);
    }
}
