using System.IO;
using System.Net;
using System.Text;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Server.Transport;

/// <summary>
/// 本地 HTTP MCP 传输：HttpListener 仅监听回环地址，POST {endpoint}。
/// 不感知 GIS / JSON-RPC / 工具。
/// </summary>
public sealed class HttpMcpTransport : IMcpTransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _endpointPath;
    private readonly ILogger _logger;
    private readonly IInternalRouteHandler? _internalRoute;
    private readonly object _gate = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public HttpMcpTransport(string host, int port, string endpointPath, ILogger logger, IInternalRouteHandler? internalRoute = null)
    {
        _host = host;
        _port = port;
        _endpointPath = endpointPath;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _internalRoute = internalRoute;
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _listener?.IsListening == true;
            }
        }
    }

    public Func<string, CancellationToken, Task<string?>>? RequestHandler { get; set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_listener?.IsListening == true)
            {
                return Task.CompletedTask;
            }

            if (!IsLoopback(_host))
            {
                throw new InvalidOperationException($"MCP transport must bind to loopback only. Host '{_host}' is not allowed.");
            }

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://{_host}:{_port}/");
            listener.Start();

            _listener = listener;
            _cts = new CancellationTokenSource();
            _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, _cts.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        HttpListener? listener;
        CancellationTokenSource? cts;
        Task? acceptLoop;

        lock (_gate)
        {
            listener = _listener;
            cts = _cts;
            acceptLoop = _acceptLoop;
            _listener = null;
            _cts = null;
            _acceptLoop = null;
        }

        if (listener is null)
        {
            return;
        }

        try
        {
            listener.Stop();
        }
        catch
        {
            // already stopped
        }

        try
        {
            listener.Close();
        }
        catch
        {
            // already closed
        }

        cts?.Cancel();

        if (acceptLoop is not null)
        {
            try
            {
                await acceptLoop.ConfigureAwait(false);
            }
            catch
            {
                // accept loop may fault on cancellation; ignore
            }
        }
    }

    public Task SendAsync(ITransportConnection connection, string message, CancellationToken cancellationToken = default)
        => connection.SendAsync(message, cancellationToken);

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        while (listener.IsListening && !cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => HandleContextAsync(context, cancellationToken), CancellationToken.None);
        }
    }

    /// <summary>D-083：把内部路由请求交给处理器（**读取任何字节前**按 MaxRequestBodyBytes 拒绝超限）。</summary>
    private static async Task HandleInternalRouteAsync(
        HttpTransportConnection connection,
        HttpListenerRequest request,
        string path,
        IInternalRouteHandler handler,
        CancellationToken cancellationToken)
    {
        var cap = handler.MaxRequestBodyBytes;
        if (request.ContentLength64 > cap)
        {
            connection.SendStatus((HttpStatusCode)413, "Payload Too Large");
            return;
        }

        var read = false;
        string? body = null;
        var routeRequest = new InternalRouteRequest
        {
            Method = request.HttpMethod ?? string.Empty,
            Path = path,
            GetHeader = name => request.Headers[name],
            RemoteEndpoint = request.RemoteEndPoint?.ToString() ?? string.Empty,
            ReadBody = () =>
            {
                if (read)
                {
                    return body;
                }

                read = true;
                try
                {
                    using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? System.Text.Encoding.UTF8);
                    var buf = new char[cap + 1];
                    var n = reader.ReadBlock(buf, 0, buf.Length);
                    body = n > cap ? null : new string(buf, 0, n);
                    if (n > cap)
                    {
                        throw new InvalidOperationException("request-body-exceeds-cap");
                    }
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (Exception)
                {
                    body = null;
                }

                return body;
            },
        };

        InternalRouteResponse response;
        try
        {
            response = await handler.HandleAsync(routeRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex.Message == "request-body-exceeds-cap")
        {
            connection.SendStatus((HttpStatusCode)413, "Payload Too Large");
            return;
        }
        catch (Exception)
        {
            connection.SendStatus(HttpStatusCode.InternalServerError, "Internal Route Error");
            return;
        }

        if ((int)response.StatusCode == 200)
        {
            connection.SendJson(response.Body);
        }
        else
        {
            connection.SendStatus(response.StatusCode, response.Body);
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var connection = new HttpTransportConnection(context);
        try
        {
            var request = context.Request;
            var path = request.Url?.AbsolutePath ?? string.Empty;

            // D-083 B 组（A-13）：内部路由＝**现役监听上的新路径**（同一 HttpListener、同一 host:port）。
            // B-04 恒定：不新增 listener/端口、不放通 0.0.0.0、现役 /mcp 契约零变化。
            if (_internalRoute is not null
                && path.StartsWith(_internalRoute.RoutePrefix, StringComparison.OrdinalIgnoreCase))
            {
                await HandleInternalRouteAsync(connection, request, path, _internalRoute, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!string.Equals(path, _endpointPath, StringComparison.OrdinalIgnoreCase))
            {
                connection.SendStatus(HttpStatusCode.NotFound, "Not Found");
                return;
            }

            if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                connection.SendStatus(HttpStatusCode.MethodNotAllowed, "Method Not Allowed");
                return;
            }

            var contentType = request.ContentType ?? string.Empty;
            if (!contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                connection.SendStatus(HttpStatusCode.UnsupportedMediaType, "Unsupported Media Type");
                return;
            }

            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            var handler = RequestHandler;
            if (handler is null)
            {
                connection.SendStatus(HttpStatusCode.ServiceUnavailable, "Server Not Ready");
                return;
            }

            string? responseBody;
            try
            {
                responseBody = await handler(body, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                connection.SendStatus(HttpStatusCode.ServiceUnavailable, "Request Cancelled");
                return;
            }
            catch (Exception ex)
            {
                _logger.Error("MCP request handling failed.", exception: ex);
                connection.SendStatus(HttpStatusCode.InternalServerError, "Internal Server Error");
                return;
            }

            if (responseBody is null)
            {
                // 通知：无 JSON-RPC 响应体
                connection.SendStatus(HttpStatusCode.Accepted, "Accepted");
            }
            else
            {
                connection.SendJson(responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("HTTP transport error.", exception: ex);
            connection.Close();
        }
    }

    private static bool IsLoopback(string host)
        => host == "127.0.0.1" || host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
}
