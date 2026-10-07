namespace ArcGISProMCP.Core.Results;

/// <summary>统一错误码。各 Service / Tool 必须复用，不得各自造不同格式。</summary>
public static class ErrorCodes
{
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string NotFound = "NOT_FOUND";
    public const string LicenseRequired = "LICENSE_REQUIRED";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string ExecutionFailed = "EXECUTION_FAILED";
    public const string Timeout = "TIMEOUT";
    public const string Cancelled = "CANCELLED";
    public const string ThreadingError = "THREADING_ERROR";
    public const string InternalError = "INTERNAL_ERROR";
    public const string ToolNotFound = "TOOL_NOT_FOUND";
    public const string NotImplemented = "NOT_IMPLEMENTED";

    // Phase 4 扩展
    public const string LayerNotFound = "LAYER_NOT_FOUND";
    // D-040 B 组：图层存在但数据源不可用（broken / 连接打不开）——与"对象不存在"严格可分（G-82-C）。
    public const string LayerDataSourceUnavailable = "LAYER_DATA_SOURCE_UNAVAILABLE";
    public const string MapNotFound = "MAP_NOT_FOUND";
    public const string DatasetNotFound = "DATASET_NOT_FOUND";
    public const string InvalidState = "INVALID_STATE";
    public const string ArcGISError = "ARCGIS_ERROR";
    public const string GeoprocessingError = "GEOPROCESSING_ERROR";
    public const string OutputExists = "OUTPUT_EXISTS";

    // Phase 5 扩展
    public const string PythonBridgeUnavailable = "PYTHON_BRIDGE_UNAVAILABLE";
    public const string PythonTimeout = "PYTHON_TIMEOUT";
    public const string PythonOutputLimitExceeded = "PYTHON_OUTPUT_LIMIT_EXCEEDED";
    public const string PythonProtocolError = "PYTHON_PROTOCOL_ERROR";

    // Phase 8.1 地图发现完善（C-05 / C-06 / C-10）
    /// <summary>无活动地图视图。禁止以回退首个地图的方式掩盖此状态。</summary>
    public const string NoActiveView = "NO_ACTIVE_VIEW";

    /// <summary>地图名称匹配到多个候选，歧义不得静默取第一个。</summary>
    public const string AmbiguousMapName = "AMBIGUOUS_MAP_NAME";

    /// <summary>图层名称匹配到多个候选，歧义不得静默取第一个。</summary>
    public const string AmbiguousLayerName = "AMBIGUOUS_LAYER_NAME";
    // Phase 8.2 (D-011)
    public const string PathEscapeRejected = "PATH_ESCAPE_REJECTED";
    // Phase 8.3 (D-013, G-32 批准)
    public const string SelectionLimitExceeded = "SELECTION_LIMIT_EXCEEDED";
    public const string SelectionBaselineMismatch = "SELECTION_BASELINE_MISMATCH";
    public const string SelectionSnapshotNotFound = "SELECTION_SNAPSHOT_NOT_FOUND";
    public const string SelectionSnapshotExpired = "SELECTION_SNAPSHOT_EXPIRED";
    public const string LayerNotSelectable = "LAYER_NOT_SELECTABLE";
    // Phase 8.4 (D-016, G-37)
    public const string RequestTimeout = "REQUEST_TIMEOUT";
    // D-017 F4（选项 A）：PYTHON_BRIDGE_CRASHED 常量已删除——全仓零发射点、白名单零引用；
    // bridge 进程死亡实测由 safety gate 返回 PYTHON_BRIDGE_UNAVAILABLE。不得在 8.6 复评前为它造发射点。
}
