# DECISION-004-python-bridge-process-lifecycle

## Status
CONFIRMED (Phase 5.3)

## Context
Phase 5.3 需要 Python Bridge 的进程生命周期：持久化 Python 子进程（启动一次、复用多次），避免每请求重新启动 python(~8s ArcPy import 成本)。
DECISION-003 选定了子进程 stdin/stdout NDJSON，但未固定 ProcessManager 的代码放置与具体生命周期模型。

## Architecture Audit（Phase 5.3，真实）
- **无既有 Process/ProcessManager/BackgroundService/IHostedService**（唯一 Process 引用在无关的 StartButton.cs）。→ 需新建最小进程管理抽象。
- **无第二套注册/配置/错误/日志的必要**：
  - 注册：Core `ServiceContainer`（唯一）；
  - 配置：`MCPSettings`（唯一活跃配置）集中（Rule 8）；
  - 错误：`ErrorCodes` + `OperationResult/OperationError`（唯一）；
  - 日志：Shared `ILogger`（Compatibility 有 `FileLogger` 实现）；
  - Cancellation：.NET `CancellationToken`（McpServer/服务已用）。

## Decision
### 1. 代码放置（精化 DECISION-003）
- Phase 5.3 的 **ProcessManager/Runner/NDJSON protocol = 纯 .NET，不依赖 ArcGIS SDK** → 放入 **`ArcGISProMCP.Core`（Shared，无 ArcGIS SDK，Rule 5）** 的 `PythonBridge` 子命名空间。
  - 理由：可被 Unit/Integration 测试项目直接引用并做真实子进程验证（无需 ArcGIS Pro）；Core 本已是纯 Runner/Router/Registry 之家；不引入新 csproj（最小变更）。
- DECISION-003 原先倾向 Compatibility 实现 —— 精化：**凡不依赖 ArcGIS SDK 的部分放 Core（可测、复用）**；依赖 Add-in/Host/ToolExecutionContext 的 PythonBridgeService 工具接线留到 Phase 5.4/5.5（届时在 Compatibility/Tools 组合 Core 的纯实现）。

### 2. 生命周期状态模型
`Stopped → Starting → Running → Stopping → Stopped`；异常 `Starting→Faulted`、`Running→Faulted`；有限恢复 `Faulted → Restart → Starting → Running`。
- 无无限重试 / 指数退避 / 熔断（归 Future）。

### 3. 核心能力（接口方法依项目风格）
- `StartAsync(CancellationToken)` / `EnsureStartedAsync(ct)` / `SendRequestAsync(PythonBridgeRequest, ct)` / `StopAsync(ct)` / `RestartAsync(ct)` / `DisposeAsync()`。
- **Persistent**：启动一次 python.exe 跑 bridge_runner.py，跨请求复用；禁止每请求 Process.Start。
- **stdout=机器 NDJSON 协议（只读行，禁止 ReadToEnd）**；**stderr=诊断（持续消费，进 ILogger，防 buffer 满阻塞）**。
- **Request correlation**：递增 request id 作 key，缓存在单飞 TCS，套接语义 TaskCompletionSource；stdout reader 按 id 路由。
- **并发访问**：Serialized / single-flight（SemaphoreSlim），防 stdin 交错 / response 错配；真并发归 Future。
- **exit detection**：进程正常/非正常退出、stdout EOF、stdin broken pipe → 转为 `Faulted`/`Exited`，返回统一错误。
- **最小 Restart**：受控有限（非自动无限）。
- **Stop → Dispose**：关 stdin → 尝试 graceful → 等 → 必要时 Kill → 释放 streams/Process。

### 4. Startup 与 Request 超时区分（重要）
- Python 启动 + ArcPy import ≈ **8 秒**（Phase 5.2 实测）不得误判为 request 超时。
- 本阶段只做 `ProcessStartupTimeout`（配置）与生命周期必要等待；完整 request 超时/Kill/restart policy 归 Phase 5.6。

### 5. 配置（集中 MCPSettings，Rule 8；不建独立 Settings）
新增：`PythonExecutable`、`PythonBridgeScript`、`PythonWorkingDirectory`、`ProcessStartupTimeoutMs`（默认如 30s）。禁止业务代码硬编码路径。

### 6. 错误码（复用 + 最小增补，不重复通用码）
- 增补 `PYTHON_BRIDGE_UNAVAILABLE`（进程未运行/Faulted/Exited）。bridge 返回的 `ok:false.error.code` 由桥接方透传（如 UNKNOWN_ACTION / PYTHON_EXECUTION_ERROR）。
- 不重复 `Timeout`/`Cancelled`/`InternalError`。

### 7. 不越界
Phase 5.3 **不**实现：Python Tool、Registry/Router 扩展、完整 Security/Timeout/Kill policy、HTTP 6511、修改 Phase 4、第二套任何系统。相关归 Future（Phase 5.4/5.5/5.6）。

## Reason
遵守无重复架构、配置集中、Shared 隔离、可测、低耦合；解决 8s 单次启动成本（持久化复用）；安全先做串行。

## Consequences
- Core 新增 `PythonBridge` 命名空间（models + process manager）。
- MCPSettings 增 Python 字段；ErrorCodes 增 `PYTHON_BRIDGE_UNAVAILABLE`。
- bridge_runner.py 协议保持不变（NDJSON，stdin/stdout/stderr 语义延续 5.2）；若需关闭信号再评估（不改变契约）。

## Date
2026-09-02

## Phase
Phase 5.3
