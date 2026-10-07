using System.Net;

namespace ArcGISProMCP.Server.Transport;

/// <summary>
/// D-083 组 B——现役监听上的**内部路由**（PS 通道）处理器契约。
/// <para>
/// <b>B-04</b>：实现方挂在 <see cref="HttpMcpTransport"/> 已经绑定的**同一个** <c>HttpListener</c> 上，
/// 不是新监听、不是新端口；<see cref="HttpMcpTransport"/> 只把 <c>/mcp</c> 之外的已知内部前缀交给它。
/// </para>
/// </summary>
public interface IInternalRouteHandler
{
    /// <summary>该处理器认领的路径前缀（如 <c>/internal/ps/v1</c>）。</summary>
    string RoutePrefix { get; }

    /// <summary>请求体硬上限（UTF-8 字节）。传输层在**读取任何字节前**据此拒绝（超限拒绝而非截断）。</summary>
    int MaxRequestBodyBytes { get; }

    /// <summary>处理一条内部路由请求，返回不晚于 <paramref name="cancellationToken"/> 的响应。</summary>
    Task<InternalRouteResponse> HandleAsync(InternalRouteRequest request, CancellationToken cancellationToken = default);
}

/// <summary>内部路由请求（只在传输层内部构造；<b>不</b>承载 HttpListenerContext，便于无端口单测）。</summary>
public sealed class InternalRouteRequest
{
    /// <summary>HTTP 方法（原样，比较时大小写不敏感）。</summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>不含查询串的绝对路径。</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>请求体读取委托（传输层实现读取并在同一上限内截断检测；返回 null 表示超限）。</summary>
    public Func<string?> ReadBody { get; init; } = () => string.Empty;

    /// <summary>取单个请求头（缺失 ⇒ null）。</summary>
    public Func<string, string?> GetHeader { get; init; } = _ => null;

    /// <summary>远端地址（诊断用；不参与授权判定）。</summary>
    public string RemoteEndpoint { get; init; } = string.Empty;
}

/// <summary>
/// 内部路由响应。
/// <para>
/// <b>错误口径</b>：状态码一律 <c>400</c>，机器可判性由响应体承载
/// （<c>{ ok:false, code, message, details:{ detail, field } }</c>），
/// <c>code</c> **只取现役 33 码注册表**（<c>ArcGISProMCP.Core.Results.ErrorCodes</c>）——零新增。
/// </para>
/// </summary>
public sealed class InternalRouteResponse
{
    private InternalRouteResponse(HttpStatusCode statusCode, string contentType, string body)
    {
        StatusCode = statusCode;
        ContentType = contentType;
        Body = body;
    }

    public HttpStatusCode StatusCode { get; }

    public string ContentType { get; }

    public string Body { get; }

    public static InternalRouteResponse Json(string json)
        => new(HttpStatusCode.OK, "application/json; charset=utf-8", json);

    public static InternalRouteResponse Error(string json)
        => new(HttpStatusCode.BadRequest, "application/json; charset=utf-8", json);
}
