namespace ArcGISProMCP.Configuration;

/// <summary>
/// 全局 MCP 配置。当前阶段不启动服务器，仅建立配置模型。
/// </summary>
public sealed class MCPSettings
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 6520;
    public const string DefaultEndpoint = "/mcp";
    public const int DefaultPythonBridgePort = 6511;
    public const string PythonAllowTestActionsEnvironmentVariable =
        "ARCGIS_PRO_MCP_ALLOW_TEST_ACTIONS";
    /// <summary>
    /// 仅供受控真实 runtime 验收启动 MCP Server；未设置时不改变生产启动行为。
    /// </summary>
    public const string VerificationAutoStartServerEnvironmentVariable =
        "ARCGIS_PRO_MCP_AUTOSTART_SERVER_FOR_VERIFICATION";
    /// <summary>
    /// D-125（O-D100-01／O-D120-01）：受管日志目录开关。未设置＝缺省逐字节不变（仍按 per-user
    /// runtime 派生）；设置时由 Compatibility 组合根读取并显式注入 runtime path resolution，
    /// 解析器自身不读环境变量。
    /// </summary>
    public const string LogDirectoryEnvironmentVariable = "ARCGIS_PRO_MCP_LOG_DIRECTORY";

    /// <summary>监听地址（默认仅环回）。</summary>
    public string Host { get; set; } = DefaultHost;

    /// <summary>MCP HTTP 端口。</summary>
    public int Port { get; set; } = DefaultPort;

    /// <summary>Endpoint 路径。</summary>
    public string Endpoint { get; set; } = DefaultEndpoint;

    /// <summary>Python Bridge 端口。</summary>
    public int PythonBridgePort { get; set; } = DefaultPythonBridgePort;

    /// <summary>日志级别。</summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>请求超时（毫秒）。</summary>
    public int RequestTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// **GP/Python 执行类**请求的独立预算（毫秒；0 = 不限时）。
    /// <para><b>D-064 阶段二真缺陷 ⑨ 修复</b>：LIVE 实测 <c>create_feature_class</c>（内部串联
    /// CreateFeatureclass + AddField 两次 GP）在**冷启动初次落盘的 GDB** 上超过 30 s 交互预算 ⇒
    /// 服务器在 GP 执行中被取消（响应被改写为 <c>REQUEST_TIMEOUT (executing)</c>），
    /// 并且**留下半成品数据集**（字段只加了一半）⇒ 后续 schema 操作连锁失败。
    /// GP 的固有耗时与交互查询不是一个量级 ⇒ 单独预算（缺省 15 分钟），交互类工具仍受
    /// <see cref="RequestTimeoutMs"/> 约束。</para>
    /// </summary>
    public int GeoprocessingRequestTimeoutMs { get; set; } = 900000;

    /// <summary>最大并发请求数。</summary>
    public int MaxConcurrentRequests { get; set; } = 8;

    /// <summary>
    /// 等待队列**深度上限**（D-056 C1 兑现 G-147 挂账）：超出即在**不等待**的情况下拒绝。
    /// <c>0</c> = 不设上限（保持历史行为）。默认 32 = 并发上限的 4 倍，正常使用不会触达。
    /// 拒绝复用注册错误码 <c>REQUEST_TIMEOUT</c>（以 <c>(queue-full)</c> 限定词与等待超时 <c>(queued)</c> 区分），
    /// 错误码注册表保持 33 项不变。
    /// </summary>
    public int MaxQueuedRequests { get; set; } = 32;

    // ---- Python Bridge（Phase 5） ----

    /// <summary>Python 可执行文件完整路径，由 Compatibility runtime path resolution 注入。</summary>
    public string PythonExecutable { get; set; } = string.Empty;

    /// <summary>bridge_runner.py 完整路径，由 packaged Compatibility assembly directory 注入。</summary>
    public string PythonBridgeScript { get; set; } = string.Empty;

    /// <summary>Python 进程工作目录，由 packaged bridge script directory 注入。</summary>
    public string PythonWorkingDirectory { get; set; } = string.Empty;

    /// <summary>Per-user runtime root；由 Compatibility runtime path resolution 注入。</summary>
    public string RuntimeRoot { get; set; } = string.Empty;

    /// <summary>唯一 production managed structured sink 的 owned directory。</summary>
    public string ManagedLogDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 受管日志目录的显式开关值（D-125）；空＝不覆盖，缺省派生路径逐字节不变。
    /// 由 Compatibility 组合根从 <see cref="LogDirectoryEnvironmentVariable"/> 读取后注入，
    /// 因此该字段与 <see cref="ManagedLogDirectory"/> 同为解析结果的真实镜像（不再是零消费者元数据）。
    /// </summary>
    public string ManagedLogDirectoryOverride { get; set; } = string.Empty;

    /// <summary>Path resolution failure code；只允许固定 safe code。</summary>
    public string? RuntimePathErrorCode { get; set; }

    /// <summary>Python 进程启动（含 ArcPy import，约 8s）的等待超时。</summary>
    public int PythonProcessStartupTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// 单个 Python action 的请求 deadline（不含 EnsureStarted 的启动时间）。
    /// 当前外层 MCP deadline 为 30s，默认 18s 为 cleanup 保留余量。
    /// </summary>
    public int PythonRequestTimeoutMs { get; set; } = 18000;

    /// <summary>
    /// Python 进程停止/kill/reader cleanup 的总预算（毫秒）。
    /// </summary>
    public int PythonProcessShutdownTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// 单条 Python stdout NDJSON response 的最大 UTF-8 payload bytes。
    /// 当前 reader 在 ReadLine 后检查，因此可限制后续解析/路由，但不能阻止 ReadLine 的单行分配。
    /// </summary>
    public int PythonMaxResponseBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// 每个 Python 进程最多写入 logger 的 stderr UTF-8 bytes；超过后仍持续 drain 但丢弃日志。
    /// 0 或负数表示不记录 stderr（不表示停止读取）。
    /// </summary>
    public int PythonMaxStderrBytes { get; set; } = 64 * 1024;

    /// <summary>
    /// 是否显式开启 bridge_runner 的测试/诊断 actions。生产默认关闭。
    /// </summary>
    public bool PythonAllowTestActions { get; set; } = false;
}
