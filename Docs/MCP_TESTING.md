# MCP Testing（MCP_TESTING）

## 1. 测试项目
`Tests/ArcGISProMCP.ServerTests`（xUnit），包含三类测试套件：

| 套件 | 覆盖 |
|------|------|
| `MCPProtocolTests` | JSON-RPC 编解码：ValidRequest / InvalidJSON(-32700) / InvalidRequest(-32600) / BuildError |
| `MCPServerTests` | initialize+InvalidInitialize / initialized 通知(无响应) / tools/list(ListTools+ToolSchema) / tools/call(Ping, GetCurrentMap, GetLayers, UnknownTool, InvalidArguments, ToolException) / UnknownMethod(-32601) / InvalidParams(-32602) / InternalError(-32603) |
| `MCPTransportTests` | Start / Stop / DoubleStart / DoubleStop / POST 成功 / InvalidContentType(415) / MalformedJSON(-32700) |

## 2. 结果
- ServerTests：**26 通过 / 0 失败**（Phase 3 构建运行）
- UnitTests：11 通过
- IntegrationTests：8 通过
- 合计 **45 / 45**。

## 3. 真实 HTTP 测试（启动真实 Server 后，用 HTTP 客户端）
启动：`ArcGIS Pro → MCP → Start`（127.0.0.1:6520）。
- `POST /mcp` initialize → 返回 protocolVersion/capabilities/serverInfo ✅
- `notifications/initialized` → HTTP 202（无响应体）✅
- `tools/list` → 返回 6 个工具（get_arcgis_version, get_current_map, get_layers, get_license_info, get_project_info, ping，JSON Schema 合法）✅
- `tools/call ping` → `{"content":[{"type":"text","text":"pong"}],"isError":false}` ✅
- `tools/call get_current_map` → 返回真实 ArcGIS 地图信息（非 Mock）✅
- unknown method → `-32601`；unknown tool → 工具结果 `isError:true`；malformed json → `-32700`；Content-Type 非 json → 415 ✅

## 4. Server 生命周期测试
- Start → 6520 LISTENING ✅
- double Start → 不创建第二个 Server（幂等）✅
- Stop → 6520 停止监听，HTTP 请求失败（connection refused）✅
- 重新 Start → 6520 重新监听 ✅
- 关闭 ArcGIS Pro → 6520 最终停止（无残留监听）✅

## 5. 复现
```powershell
. .\scripts\dev-env.ps1
dotnet test ArcGIS-Pro-MCP.sln
```
