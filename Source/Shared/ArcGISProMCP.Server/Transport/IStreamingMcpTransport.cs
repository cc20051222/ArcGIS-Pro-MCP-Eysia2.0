namespace ArcGISProMCP.Server.Transport;

/// <summary>Optional request-scoped SSE; existing JSON-only transports remain unchanged.</summary>
public interface IStreamingMcpTransport
{
    Func<string, Action<string>, CancellationToken, Task<string?>>? StreamingRequestHandler { get; set; }
}
