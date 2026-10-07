using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Core.Tools;

/// <summary>工具执行上下文。</summary>
public sealed class ToolExecutionContext
{
    public string? RequestId { get; init; }

    public CancellationToken CancellationToken { get; init; }

    public ILogger Logger { get; init; } = NullLogger.Instance;

    public IArcGISHost? Host { get; init; }

    /// <summary>Python Bridge 高层服务（Phase 5.4；无可用桥接时为 null）。</summary>
    public IPythonBridgeService? Python { get; init; }

    public MCPSettings Settings { get; init; } = new MCPSettings();

    public IReadOnlyDictionary<string, object?>? Arguments { get; init; }

    // ═══════════════ D-064 新增（均可空；既有构造点零改动）═══════════════

    /// <summary>D-064：工具注册表（<c>run_batch</c> 据此做**白名单准入**并调用同批工具）。</summary>
    public MCPToolRegistry? Registry { get; init; }

    /// <summary>D-064：会话级只读模式服务（路由层闸门 + <c>run_batch</c> 逐项闸门 + <c>set_readonly_mode</c>）。</summary>
    public IReadOnlyModeService? ReadOnly { get; init; }

    /// <summary>Shared invocation boundary used by the router and nested tool workflows.</summary>
    public IToolInvoker? Invoker { get; init; }

    /// <summary>Last in-process receipt; durable receipt persistence is handled by the invoker journal.</summary>
    public ToolInvocationReceipt? LastInvocationReceipt { get; internal set; }
}
