using System.Net;
using System.Text;

namespace ArcGISProMCP.Server.Transport;

/// <summary>HTTP 传输连接（包装 HttpListenerContext）。</summary>
internal sealed class HttpTransportConnection : ITransportConnection
{
    private readonly HttpListenerContext _context;
    private int _closed;

    public HttpTransportConnection(HttpListenerContext context)
    {
        _context = context;
        Id = Guid.NewGuid().ToString("N");
    }

    public string Id { get; }

    public bool IsOpen => Volatile.Read(ref _closed) == 0;

    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        SendJson(message);
        return Task.CompletedTask;
    }

    public void SendJson(string json)
    {
        if (Volatile.Read(ref _closed) != 0)
        {
            return;
        }

        WriteResponse(HttpStatusCode.OK, "application/json; charset=utf-8", json);
    }

    public void SendStatus(HttpStatusCode code, string text)
    {
        if (Volatile.Read(ref _closed) != 0)
        {
            return;
        }

        WriteResponse(code, "text/plain; charset=utf-8", text);
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
        {
            return;
        }

        try
        {
            _context.Response.Abort();
        }
        catch
        {
            // ignore
        }
    }

    private void WriteResponse(HttpStatusCode code, string contentType, string body)
    {
        var response = _context.Response;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            response.StatusCode = (int)code;
            response.ContentType = contentType;
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }
        catch
        {
            // client may have disconnected; ignore
        }
        finally
        {
            Interlocked.Exchange(ref _closed, 1);
        }
    }
}
