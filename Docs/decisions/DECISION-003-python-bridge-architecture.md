# DECISION-003-python-bridge-architecture

## Status
PROPOSED (Phase 5.1 Preflight) — 待 Phase 5 后续实现时确认/完善。

## Context
Phase 5 需要建立 Python Bridge，作为 Phase 4 Native / Geoprocessing 之外的 Python/ArcPy 扩展执行通道。
需确定通信方式（IPC）、进程生命周期、安全边界、Tool/Service 放置层。

## 关键真实调查结果（Phase 5.1，本机）
- ArcGIS Pro Python 环境：`C:\Program Files\ArcGIS\Pro\bin\Python\envs\arcgispro-py3\python.exe` = Python 3.11.11（conda env，ArcGIS Pro 自带）。
- `import arcpy` = OK；`arcpy.GetInstallInfo()` → Version=3.5；`import arcgis` = OK，版本 2.4.1。
- **critical**：独立子进程运行上述 python.exe 时，`arcpy.mp.ArcGISProject("CURRENT")` **报错 `CURRENT`** → 脱离 Pro 进程的 Python **无法访问运行中 Pro 的实时 CURRENT 工程/地图**；只能通过**数据集路径**做 arcpy GP/数据操作，或由 Add-in 在 Pro 进程内嵌入执行 Python。
- MCP Server 架构：`McpServer → McpProtocolHandler → Tool Router → IMCPTool → (Host) → Services`；Shared 层不得引用 ArcGIS SDK（Rule 5）；工具经唯一 `MCPToolRegistry` 注册（Rule 7）。

## Decision
### 1. 通信方式：方案 A — Python 子进程 + stdin/stdout 逐行 JSON（推荐首选）
对比后综合稳定性/可验证/可维护/低耦合/易恢复/易测试，优先采用：
**C# 启动 `arcgispro-py3\python.exe` 子进程，通过 stdin/stdout 传递一行一个 JSON 请求/响应（newline-delimited JSON）。**
- 生命周期由 C# `PythonBridgeProcessManager` 管理（Start/Stop/Restart/Kill；超时强制 Kill；崩溃检测 PID 退出）。
- 串行模型：单次一个请求（ArcPy 环境非线程安全，安全优先），MCP 端 `_concurrency` 已有并基于请求串行。
- Python 端：`bridge_runner.py`（纯 stdlib，无第三方依赖）逐行读 stdin → 执行 → 逐行写 JSON 响应。
- 超时/取消：C# 用 CancellationToken + 进程级 Kill 兜底；请求级 Timeout。
- 不硬编码路径：路径/超时/允许目录统一进 `MCPSettings`（Rule 8）。
- 备选：方案 B（localhost HTTP 6511）复杂度更低端到端但多一层网络 + 需处理端口/绑定，作为后续可选；方案 C（命名管道）Windows 原生但 stdlib 需额外处理、测试/调试成本高；方案 D 无。
- 采用 stdin/stdout 而非 6511 HTTP 的原因：单进程内、无需额外监听端口（规避端口占用/0.0.0.0 安全面）、天然绑定进程生命周期、更易做 Crash/Timeout Kill 与恢复、测试可控。（后续若需多请求/独立服务再评估 HTTP。）

### 2. 放置层
- Shared 层（`ArcGISProMCP.Core/Services`）：新增 `IPythonBridgeService`（接口 + 模型，无 ArcGIS SDK，Rule 5）。
- Compatibility 层（`ArcGISProMCP.Compatibility`，可引用 ArcGIS SDK/进程管理）：`PythonBridgeProcessManager`、`PythonBridgeSubprocessRunner`、`PythonBridgeService`（实现 IPythonBridgeService，负责进程与协议）。
- **不**机械加入 `IArcGISHost`（IArcGISHost 聚合的是 ArcGIS 数据/许可等宿主服务；Python Bridge 是执行通道而非地图数据服务）。Python Tool 通过 `ToolExecutionContext.Host` 访问独立注册的 bridge（或经 Composition 注入）。

### 3. Tool
- 复用唯一 `MCPToolRegistry`；新增 `PythonBridgePingTool`（`python_bridge_ping`）与 `PythonExecuteTool`（`python_execute`），`ExecutionType = ExecutionTypes.Python`，`ToolCategories.Python`（新增分类）。
- 禁止第二 Registry / giant switch（Rule 7）。

### 4. 安全边界（defense-in-depth，非 sandbox）
- 明确：允许执行传入 Python 代码（MCP 定义行为），但**不伪装为沙箱**。
- C# 侧 PythonSecurityValidator：限制脚本长度、超时、Cancellation、输出截断、危险模块/调用黑名单（os/subprocess/sys/eval/open/网络等）的**告警性检查**，文档诚实说明属于 defense-in-depth，非安全隔离。
- WorkingDirectory/PATH 集中配置；不开放 0.0.0.0；6511 仅作文档保留（stdin/stdout 方案不使用 6511 监听）。

### 5. 已知限制（诚实记录）
- 独立 Python 子进程**不能** `arcpy.mp.ArcGISProject("CURRENT")` 读实时 Pro 工程/地图（本次已真实验证报错）。Python 侧访问实时工程需 Add-in 内嵌执行（后续）。Bridge 首批验证 arcpy GP/数据路径即可。
- Python Bridge 崩溃/停止不应导致 MCP Server / ArcGIS Pro 崩溃；错误不污染下一请求。

## Reason
遵守 Shared 隔离/单一 Registry/配置集中；选择可验证、可恢复、易测试、Windows/ArcPy 兼容的子进程 stdin/stdout 方案；如实标注 CURRENT 工程访问限制，不伪造。

## Alternatives（已评估）
- B localhost HTTP 6511：可行，多一层网络/端口，生命周期与进程绑定弱，后续如需独立服务再评估。
- C Named Pipe：Windows 原生但与 ArcGIS Python stdlib 交互/调试/测试成本高。
- 嵌入 Pro 进程执行 Python：可读 CURRENT，但需 ArcGIS Pro SDK Python 嵌入机制，复杂度高，Phase 5 首批不做。

## Consequences
- 新增 `IPythonBridgeService`（Shared）+ Compatibility 实现。
- `MCPSettings` 增加 PythonExecutable / WorkingDirectory / AllowedPaths / DefaultTimeoutMs / MaxScriptLength / 危险调用黑名单等配置。
- `ErrorCodes` 增补 Python 语义错误（不重复 Timeout/Cancelled/InternalError 等通用码）。

## Date
2026-09-02

## Phase
Phase 5
