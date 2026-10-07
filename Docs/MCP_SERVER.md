# MCP Server（MCP_SERVER）

> 项目：`ArcGISProMCP.Server`（net8.0，无 ArcGIS SDK）。运行于 ArcGIS Pro Add-in 进程内。

## 1. 分层
```
Transport (HttpMcpTransport)
   ↓ 请求/响应文本
JSON-RPC (JsonRpcCodec)
   ↓ method dispatch
MCP Protocol (McpProtocolHandler)
   ↓ tools/call
Tool Router (MCPToolRouter)
   ↓
Tool Registry → IMCPTool
   ↓
IArcGISHost → Services → QueuedTask.Run → ArcGIS Pro SDK
```
各层严格分离，不使用巨型 `MCPServer.cs`。

## 2. 文件
| 文件 | 职责 |
|------|------|
| `Transport/IMcpTransport.cs` | 传输抽象（Start/Stop/Send/IsRunning/RequestHandler） |
| `Transport/ITransportConnection.cs` | 连接抽象 |
| `Transport/HttpMcpTransport.cs` + `HttpTransportConnection.cs` | HTTP Loopback 传输实现 |
| `JsonRpc/JsonRpcCodec.cs` | JSON-RPC 2.0 解析 / 序列化 |
| `JsonRpc/JsonRpcErrorCodes.cs` / `JsonRpcExceptions.cs` | JSON-RPC 错误码与异常 |
| `JsonRpc/JsonValueConverter.cs` | 将 JsonElement 参数转 CLR 类型 |
| `Mcp/McpConstants.cs` / `McpProtocolHandler.cs` | MCP method 处理 |
| `McpServer.cs` | 编排 Transport → JSON-RPC → MCP method → Router |

## 3. 生命周期
- `StartAsync`：设置 RequestHandler → 启动 Transport。重复调用幂等（不会启动第二个 Server）。
- `StopAsync`：停止 Transport（幂等）。
- **ArcGIS Pro 退出**：`Module1.CanUnload` 触发 Server 停止；即使未及时停止，进程退出同样释放端口。

## 4. 并发 / 取消 / 超时
- 并发：`SemaphoreSlim(MaxConcurrentRequests)` 限制并发处理。
- 取消：HTTP 请求 `CancellationToken` 传入 `ToolExecutionContext`。
- 超时：`MCPSettings.RequestTimeoutMs` 通过 `CancelAfter` 实现，不阻塞线程、不用 `Thread.Sleep`。

## 5. 日志（记录字段）
`Server Start` / `Server Stop` / `Request` / `Response` / `Method` / `RequestId` / `Error` / `ExecutionTime`。
**不记录**：用户敏感数据、完整大型 GIS 数据集、完整 ArcPy 代码。

## 6. Ribbon
- `Start`：启动 Server（运行中则提示已运行）。
- `Stop`：停止 Server。
- `Status`：显示 Server 状态（Running/Stopped）、端口、Endpoint、Connected Clients（HTTP 无法可靠统计，显示 N/A），以及 ArcGIS Pro Host 状态。

## 7. Endpoint
- 默认 `http://127.0.0.1:6520/mcp`，由 `MCPSettings`（Host=127.0.0.1, Port=6520, Endpoint=/mcp）配置，不在代码中散落常量。
