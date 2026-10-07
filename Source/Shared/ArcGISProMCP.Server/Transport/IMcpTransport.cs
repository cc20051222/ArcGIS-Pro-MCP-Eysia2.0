namespace ArcGISProMCP.Server.Transport;

/// <summary>
/// MCP 传输抽象。只负责收发文本消息，不感知 GIS / JSON-RPC / 工具。
/// </summary>
public interface IMcpTransport
{
    bool IsRunning { get; }

    /// <summary>
    /// 收到请求文本时回调：返回响应文本；返回 null 表示通知（无响应）。
    /// </summary>
    Func<string, CancellationToken, Task<string?>>? RequestHandler { get; set; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task SendAsync(ITransportConnection connection, string message, CancellationToken cancellationToken = default);
}
