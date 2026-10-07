# 架构（ARCHITECTURE）

> 本文记录 ArcGIS Pro MCP 的架构设计与当前（Phase 1）实现状态。

---

## 1. 总体架构

AI Client
↓
MCP Client（DeepSeek Harness / Claude Desktop / Cursor / …）
↓
MCP Protocol（JSON-RPC 2.0，Streamable HTTP 优先）
↓
**ArcGIS Pro MCP Server**
↓
Tool Registry → Tool Router → GIS Tool
↓
ArcGIS Pro SDK / Geoprocessing / ArcPy
↓
ArcGIS Pro

原则：
- 所有 AI 客户端统一通过标准 MCP 连接，**禁止**为单个客户端设计私有 GIS 协议。
- 版本差异集中在 **Host 层**，通过 Shared Core + Compatibility Host + Current Host 实现。

---

## 2. Shared Core（不含 ArcGIS Pro SDK）

Shared 只保存与 ArcGIS Pro SDK 无关的模型与接口：

| 项目 | 内容 |
|------|------|
| ArcGISProMCP.Core | 通用 `Result` / `Result<T>` 结果模型 |
| ArcGISProMCP.Protocol | JSON-RPC 2.0 模型（`JsonRpcRequest` / `JsonRpcResponse` / `JsonRpcErrorResponse` / `JsonRpcError`）、MCP 工具定义 `McpToolDefinition` |
| ArcGISProMCP.Configuration | `McpServerOptions`（Host=127.0.0.1、Port=6520、Endpoint=/mcp、Transport） |
| ArcGISProMCP.Logging | `LogLevel` / `LogEntry`（Timestamp/Level/RequestId/Client/Tool/ExecutionTime/Result/Error）/ `ILogger` |
| ArcGISProMCP.Security | `SecurityDecision` / `SecurityCheckResult` |

约束：**Shared 不得引用任何 ArcGIS Pro SDK 程序集。**

---

## 3. Host 层（ArcGIS 相关）

### 3.1 Compatibility Host（当前重点）
- 项目：`ArcGISProMCP.Compatibility`
- TFM：`net8.0-windows`（x64，UseWPF）
- 目标：ArcGIS Pro 3.3–3.6
- 当前真实验证：**ArcGIS Pro 3.5**

### 3.2 Current Host（占位）
- 目标：ArcGIS Pro 3.7+（.NET 10）
- 当前仅占位说明，未编译。

### 3.3 ArcGIS Host（Phase 2）
- 接口：`Core.Hosting.IArcGISHost`（Shared，聚合 11 个服务，返回纯数据模型）。
- 实现：`Compatibility.Hosting.ArcGISHost`（聚合 Maps/Layers/Attributes/Selection/Geoprocessing/Raster/Data/Layout/Project/License/Version）。
- Services（Compatibility/Services）：Map、Layer、Project、License、Version 真实实现；其余 6 个为 NOT_IMPLEMENTED 占位。
- **线程模型**：所有 Map / MapView / Project / Licensing 对象访问均通过 `QueuedTask.Run`（MCT），不在 UI 线程直接操作（详见 Docs/THREADING.md）。

---

## 4. Add-in / Ribbon

- `Config.daml`：MCP 选项卡 → MCP 组 → Start / Status / Run Tests 按钮。
- `StartButton`：显示「MCP Server尚未实现，当前为Phase 1测试。」（占位）。
- `StatusButton`：调 `IArcGISHost.Maps.GetMapsAsync()`，显示「ArcGIS Pro Host正常」。
- `RunTestsButton`：触发 SelfTestRunner 集成自测。
- 模块：`Module1`（订阅 `ProjectOpenedAsyncEvent`，工程打开后自动运行自测）。
- SelfTestRunner：通过 Router 执行 6 个内置工具；仅将固定 test id/category、outcome、duration 和计数写入 Composition 提供的唯一 `.runtime\managed-logs` structured sink，不持久化 `result.Data`，也不打开历史 `selftest.log`/`selftest.marker`。

---

## 5. 打包与安装

- 官方 `Esri.ProApp.SDK.Desktop.targets` 使用 `CodeTaskFactory`，在 .NET Core MSBuild（`dotnet build`）中不受支持（MSB4801）。
- 本机 VS 2026 MSBuild.exe 无 .NET SDK 解析器 / 桌面工作负载，无法编译 SDK 风格工程。
- 因此 Add-in 打包由 `scripts/package-addin.ps1` 复刻官方目标布局完成，安装使用官方 `RegisterAddIn.exe /s`。

---

## 6. 当前阶段完成的能力（Phase 1 + Phase 2 + Phase 3）

Phase 1：
- 解决方案 + Shared Core（5 项目）+ Compatibility Host 建立并编译通过（0/0）。
- 最小 Add-in 编译、打包 `.esriAddInX`、安装进 ArcGIS Pro 3.5、启动加载、MCP 选项卡与 Start/Status 按钮、正常退出。

Phase 2：
- Shared：统一 `OperationResult<T>` / `OperationError` / `ErrorCodes`；MapInfo/LayerInfo 等纯数据模型；11 个 Service 接口；`IArcGISHost`；`IMCPTool` / `ToolExecutionContext` / `MCPToolRegistry` / `MCPToolRouter`；轻量 `ServiceContainer`。
- 新项目 `ArcGISProMCP.Tools`：6 个内置测试工具。
- Compatibility：Map/Layer/Project/License/Version 服务真实实现；Composition 组合根；FileLogger；SelfTestRunner。
- Tests：UnitTests（11）+ IntegrationTests（8），均通过。

Phase 3（本阶段）：
- 新项目 `ArcGISProMCP.Server`（net8.0，无 ArcGIS SDK）：分层实现 Transport → JSON-RPC → MCP Protocol → Tool Router。
- 真实 MCP Server：`POST http://127.0.0.1:6520/mcp`（loopback-only），支持 initialize / notifications/initialized / tools/list / tools/call。
- Ribbon：Start（幂等）/ Stop / Status（显示 Running/Stopped/端口/Endpoint/Connected Clients=N/A）。
- Module 退出自动 Stop Server。
- MCP Server 日志（Start/Stop/Request/Response/Method/RequestId/Error/ExecutionTime）。
- Tests：新增 ServerTests（26），合计 45/45 通过。
- 真实 HTTP + 真实 ArcGIS Pro 3.5 运行验证通过（见 Docs/MCP_TESTING.md）。

---

## 7. 后续阶段方向（Phase 4+）

- 增加更多真实 GIS 工具（替换 NOT_IMPLEMENTED 占位服务 → 工具），并让各家 MCP Client 接入验证。
- Python Bridge（127.0.0.1:6511）、ArcPy 安全与 License 检查。
- Current Host（Pro 3.7+ / .NET 10）。
