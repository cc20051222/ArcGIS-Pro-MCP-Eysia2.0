# DECISION-005 — Python Bridge Timeout / Cancellation / Recovery Contract

## Status

**ACCEPTED / IMPLEMENTED / RUNTIME VERIFIED (Phase 5.6.4)**

## Context

Phase 5.5 已在真实 ArcGIS Pro 3.5 runtime 中验收通过。当前 Python Bridge 是单个持久化子进程，使用 stdin/stdout NDJSON、单飞请求和 request correlation。现有 `RequestTimeoutMs` 同时覆盖 MCP 请求与 Bridge response 等待；调用方取消或超时只会结束 C# 等待，不会终止已经进入 Python/ArcPy 的 action。这样会使唯一 Bridge 被长时间占用，并且在 timeout 后可能收到 late response。

现有 request factory 还使用固定的 `ping`/`info` ID 及路径 `GetHashCode()`，不能保证跨请求、跨进程 generation 唯一。`StopCoreAsync`、startup failure cleanup、`DisposeAsync` 的等待和资源释放也缺少统一的有界契约。

## Decision

### 1. Dispatch-aware request lifecycle

ProcessManager 为每个 request 维护内部阶段：

`Created → WaitingForFlight → EnsuringProcess → PendingRegistered → Dispatching → Dispatched → WaitingForResponse → Completed`。

请求只有在写入和 flush 完成后才算 `Dispatched`。进入写入调用后若发生异常，发送结果可能不确定，必须按“possibly dispatched”处理并隔离进程；只有在写入尚未开始前才能确认是 pre-dispatch。

### 2. Timeout and cancellation semantics

- Python deadline 超时统一返回 `PYTHON_TIMEOUT`。
- caller `CancellationToken` 取消统一返回现有 `CANCELLED`。
- pre-dispatch timeout/cancel：移除当前 pending entry，保持健康进程不变。
- post-dispatch timeout/cancel：当前请求不重放；先将进程标记为 quarantine，终止整个 process tree，按有界 deadline 等待退出并清理资源，再向调用方返回对应错误。

取消不会向 Python 发送伪造的取消消息，也不依赖 `bridge_runner.py` 的协作式取消点。

### 3. Recovery ownership and restart policy

采用 Option C：`SendRequestAsync` 持有 `_flight` 直到已派发请求的 quarantine/cleanup 完成，但不在本次调用中启动新 Python 进程。下一次独立请求通过 `EnsureStartedAsync` 进行一次新的 startup probe。这样返回路径不会额外承受完整 startup latency，同时保证任何后续 request 都不会写入 tainted process。

Process crash、EOF、pipe failure、startup failure 和 recovery failure 必须清空 pending、关闭 streams、终止/等待/Dispose 旧 Process，并映射为 `PYTHON_BRIDGE_UNAVAILABLE`。基础设施 startup recovery 可有界尝试；业务 action 不得自动重试。

### 4. No automatic replay

任何已派发 action 在 timeout、cancel、pipe break、crash 或强制终止后都视为执行结果未知。无论 action 当前是只读还是未来可能有写入/GP 副作用，都不自动 replay。恢复只负责建立下一次独立 request 的健康执行条件。

### 5. Correlation IDs and late responses

ID 必须由 ProcessManager 生成，并在 manager 生命周期及 process generation 内唯一；推荐 `py-{generation}-{monotonic counter}` 或不可复用的 Guid，不得再由 payload/path hash 生成，也不得使用固定 ping/info ID。pending insertion 遇到重复 ID 必须失败，不能静默覆盖旧 TCS。

每次新 Process 使用新的 generation。没有对应 pending entry 的 stdout response 是 late/unknown response，记录诊断后丢弃；generation 不匹配的 response 也必须丢弃，绝不能按顺序匹配到新 request。

### 6. Locking and cleanup

`_flight` 是请求 admission/quarantine 的唯一串行门；`_lifecycle` 只保护 process/state/stream 的短状态转换；`_gate` 只保护 pending 和字段快照，禁止在 `_gate` 内 await。保持当前 send 的 `_flight → brief _lifecycle` 方向；任何 Stop/Restart/Dispose 路径都不得持有 `_lifecycle` 再等待 `_flight`，以避免反向等待死锁。quarantine 必须在释放 `_flight` 前完成。

所有正常完成、pre-dispatch abort、post-dispatch quarantine、process exit、startup failure 和 dispose 路径都必须清理 pending TCS。`TrySet*` 用于防止重复完成；reader 退出、stderr drain、Process wait 和 Process dispose 必须有明确的 bounded cleanup 行为。

### 7. Configuration and errors

增加独立的 `PythonRequestTimeoutMs`，并将 graceful-stop/kill wait、bounded recovery count 等 deadline/limit 集中放入 `MCPSettings`；移除 `120000ms` 等 manager 内 magic number。迁移期可显式继承现有 `RequestTimeoutMs=30000`，新默认值须在 runtime measurement 后确认。

错误分层如下：

| Condition | Error code | Process action |
|---|---|---|
| caller cancel before dispatch | `CANCELLED` | unchanged |
| caller cancel after dispatch | `CANCELLED` | quarantine and cleanup |
| Python deadline before dispatch | `PYTHON_TIMEOUT` | unchanged |
| Python deadline after dispatch | `PYTHON_TIMEOUT` | quarantine and cleanup |
| process/pipe/start/recovery failure | `PYTHON_BRIDGE_UNAVAILABLE` | cleanup; next request may bounded-start |
| structured Python action error | existing bridge execution error | preserve healthy process |

`TIMEOUT` 保留为通用/外层语义；不使用重复的 recovery error code，除非实现验证证明 `PYTHON_BRIDGE_UNAVAILABLE` 不足。

### 8. Lifecycle and disposal

不新增 public `Tainted`/`Recovering` state。内部使用 quarantine flag、generation 和 request dispatch phase 表达状态；对外仍使用现有 Running/Starting/Stopping/Stopped/Faulted/Disposed 语义。`DisposeAsync` 必须先禁止新 request，完成当前 flight/recovery 的有界收敛，清空 pending，终止并等待 process/reader，释放 streams/CTS，最后再 dispose semaphores；不得在 semaphore dispose 后再 Release。多次 dispose 必须幂等。

## Consequences

- 已派发 timeout/cancel 的调用者会承担有界 kill/wait cleanup latency，但不会承担本次调用的完整新进程 startup latency。
- 下一次 request 可能承担新进程 startup latency；它不会复用 tainted child。
- ArcPy/GP 的部分副作用不能由 kill 回滚，因此 no-replay 是安全默认值。
- HTTP transport 是否能可靠传递 client disconnect token 仍是独立验证项；ProcessManager 必须先对收到的 token 实现正确语义。
- response/stderr byte limits、path guardrails 和 action classification 继续由后续 5.6.3 处理，不在本决策中扩大执行面。

## Rejected alternatives

1. **Return before killing（Option B）**：会让下一 request 进入仍可能运行旧 action 的 child，违反单飞和隔离目标。
2. **Restart before returning（Option A）**：把 startup latency 叠加到已经超时的 caller；可作为后续优化，不作为最小实现默认。
3. **Automatic retry/replay**：无法证明 ArcPy/GP action 无副作用，拒绝。
4. **Public Tainted/Recovering enum**：增加外部 API 面；内部 quarantine/generation 足够。
5. **第二 Bridge、HTTP 6511、多 worker 或 arbitrary Python**：违反既定架构与安全边界，拒绝。

## Required Phase 5.6.2 verification

必须覆盖 pre/post-dispatch timeout、pre/post-dispatch cancellation、same-PID normal exception、crash/EOF cleanup、unique IDs/generation/late response、no replay、startup failure cleanup、dispose ordering 和 bounded completion。真实 ArcGIS Pro 验证需确认旧 PID 已退出、下一次 ping 使用新 PID且仅一个 child；Harness 限制只能标记为 `BLOCKED_BY_HARNESS`，不得伪造 PASS。

## Phase 5.6.2 implementation evidence（2026-09-03）

- PythonBridgeProcessManager now owns generation-aware wire IDs (py-{generation}-{sequence}), tracks dispatch state, rejects duplicate pending IDs, and drops old/unknown responses.
- MCPSettings now supplies PythonRequestTimeoutMs=18000 and PythonProcessShutdownTimeoutMs=3000. The 18-second action budget excludes startup; with the measured approximately 8-second ArcPy startup and 3-second shutdown budget, it leaves practical margin under the existing 30-second outer MCP deadline.
- Post-dispatch timeout/cancellation performs token-independent bounded quarantine before releasing _flight; no business request is replayed. Startup failure, process exit, pending cleanup, and Dispose ordering are covered.
- Quarantine is bound to the timed-out request's Process instance and generation; an old request unwinding after Stop/Restart cannot terminate a newer Bridge generation.
- Core lifecycle tests: 9/9 PASS using a temporary standard-library Python fixture; full automated baseline: Unit 20/20 PASS, Integration 20/20 PASS, Server 19/26 with 7 existing HttpListenerException: 句柄无效 Harness blocks.
- The modified Add-in has not been deployed into the active ArcGIS Pro instance in this phase. Existing read-only MCP regression remains healthy (initialize, tools/list=30, Python ping/runtime/summary); final modified-package runtime acceptance remains Phase 5.6.4.

## Phase 5.6.4 runtime acceptance evidence

- The registered 202613-byte package and workspace package matched by SHA-256 (`62FBE89CAE9F03077C797A146662AFC81ED29E59539327D85FBC190A179B8D65`); the new Pro PID 15740 loaded it and started MCP through the verification-only environment-gated path.
- Real ArcGIS Pro Python + production `bridge_runner.py` timeout probe: old PID 31092 was terminated after `PYTHON_TIMEOUT` at 19555 ms; the next independent ping used fresh PID 1320. No business action replay was observed; the automated fixture independently counted one dispatched timeout action.
- Real Bridge-level cancellation returned `CANCELLED` after dispatch, terminated PID 25112, and recovered with fresh PID 31872. Structured exception preserved PID 6032; verified crash of probe-owned PID 8460 mapped to `PYTHON_BRIDGE_UNAVAILABLE` and recovered with PID 17380.
- Real Dispose-during-request ended in `Disposed`, `ProcessId=-1`, and no probe-owned child. The only remaining matching Python process was the intended Pro-owned child.
- The 18000 ms action deadline plus bounded 3000 ms shutdown budget completed before the outer 30000 ms MCP deadline. HTTP client-disconnect cancellation propagation remains outside this decision and is not verified.
