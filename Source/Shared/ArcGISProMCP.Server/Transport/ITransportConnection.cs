namespace ArcGISProMCP.Server.Transport;

/// <summary>
/// 传输层连接抽象。Transport 不感知 GIS。
/// </summary>
public interface ITransportConnection
{
    string Id { get; }

    bool IsOpen { get; }

    Task SendAsync(string message, CancellationToken cancellationToken = default);
}
