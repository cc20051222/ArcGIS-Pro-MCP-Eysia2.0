# MCP Transport（MCP_TRANSPORT）

## 1. 抽象
```csharp
public interface IMcpTransport {
    bool IsRunning { get; }
    Func<string, CancellationToken, Task<string?>>? RequestHandler { get; set; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task SendAsync(ITransportConnection connection, string message, CancellationToken ct = default);
}
public interface ITransportConnection {
    string Id { get; }
    bool IsOpen { get; }
    Task SendAsync(string message, CancellationToken ct = default);
}
```
Transport 不感知 GIS / JSON-RPC / 工具（只收发文本）。

## 2. HTTP Transport（`HttpMcpTransport`）
- 使用 `HttpListener`，仅监听 `http://{host}:{port}/`，且 **host 必须是 loopback（127.0.0.1 / localhost）**。
- 路径匹配 `MCPSettings.Endpoint`（默认 `/mcp`）。
- 每次请求：读取 body → 调 `RequestHandler(body, ct)` → 写响应。

## 3. HTTP 行为
| 情况 | 响应 |
|------|------|
| 正确 JSON + 有响应 | `200 application/json`（JSON-RPC 响应） |
| 通知（handler 返回 null） | `202 Accepted`（无 JSON-RPC 响应体） |
| 未知路径 | `404` |
| 非 POST | `405` |
| Content-Type 非 application/json | `415` |
| Server 未就绪（handler 为 null） | `503` |
| handler 抛异常 | `500`（不得崩溃） |

## 4. Session
- **无状态 / 无 session**。HTTP 请求-响应模型天然无状态，不使用非标准的 `X-Session-ID` 机制。

## 5. 安全
- 仅回环（loopback）监听；`StartAsync` 对非回环地址抛 `InvalidOperationException`。
- 无公网监听、无身份认证/OAuth（Phase 3 阶段）。

## 6. 备注
- `SendAsync` 作用于连接，用于未来可能的服务器推送；当前 HTTP 为请求-响应，未使用服务器推送。
