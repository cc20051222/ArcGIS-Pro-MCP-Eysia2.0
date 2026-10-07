# MCP Protocol（MCP_PROTOCOL）

> Phase 3 实现的 MCP 协议。基于 JSON-RPC 2.0，本地 HTTP 传输。

## 1. MCP 版本
- 协议版本：`2024-11-05`（`ArcGISProMCP.Server.Mcp.McpConstants`）
- 服务器名称：`arcgis-pro-mcp`
- 服务器版本：`0.1.0`

## 2. Transport / Endpoint
- 传输：Streamable over HTTP（每次请求一个 JSON-RPC）
- Endpoint：`POST http://127.0.0.1:6520/mcp`（默认，可配置于 `MCPSettings`）
- Content-Type：`application/json`

## 3. Methods
| Method | 类型 | 说明 |
|--------|------|------|
| `initialize` | request | 握手，返回 protocolVersion/capabilities/serverInfo |
| `notifications/initialized` | notification | 客户端通知已初始化，无响应体 |
| `tools/list` | request | 列出工具（来源：MCPToolRegistry，单一来源） |
| `tools/call` | request | 调用工具（name + arguments） |

## 4. JSON-RPC 2.0
- 请求：`jsonrpc`, `id`, `method`, `params`
- 响应：`jsonrpc`, `id`, `result`
- 错误：`jsonrpc`, `id`, `error`
- 通知（无 id）：服务端不回复（含错误）。

### 错误码
| Code | 含义 |
|------|------|
| -32700 | Parse Error（无效 JSON） |
| -32600 | Invalid Request（非对象 / 缺 method / 缺 jsonrpc="2.0" / 非法 id） |
| -32601 | Method Not Found（未知 method） |
| -32602 | Invalid Params（params 非对象 / 缺工具 name / initialize params 非法） |
| -32603 | Internal Error（意外异常） |

## 5. Tool 执行错误
- 工具不存在 / 参数校验失败 / GIS 执行失败 → 放在**工具结果**中返回（`result.isError = true`），
  而不是 JSON-RPC 错误。格式：
  ```json
  { "content": [ { "type": "text", "text": "TOOL_NOT_FOUND: ..." } ], "isError": true }
  ```

## 6. 安全
- 仅监听 `127.0.0.1`（loopback），拒绝 `0.0.0.0` / 非回环地址。
- 无 HTTP 认证 / OAuth / 公网监听（Phase 3 不需要）。

## 7. Session
- **无 session / 无状态**：HTTP 每次请求独立处理，不需要 `X-Session-ID` 等非标准机制。
- 原因：Streamable over HTTP 的请求-响应模型本身无状态；`initialize` 后无需维持连接状态即可
  调用 `tools/list` / `tools/call`。
