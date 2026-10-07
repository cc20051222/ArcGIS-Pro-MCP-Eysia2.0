# DECISION-006 — Python Bridge Output / Protocol / Action Guardrails

## Status

**ACCEPTED / IMPLEMENTED / RUNTIME VERIFIED (Phase 5.6.4 production boundary；synthetic fault paths internal-fixture verified；Path Guardrail DESIGN COMPLETE / DEFERRED)**

## Context

Phase 5.6.2 已完成 single-flight、generation-aware correlation、post-dispatch quarantine 和 bounded process cleanup。现有 Bridge 仍以 stdout NDJSON 作为机器协议、stderr 作为诊断，但 line、response、diagnostic logging 和 test action surface 没有统一 guardrail。Discovery action 的 ArcPy path 语义还覆盖 FileGDB child、UNC、mapped drive、SDE 和用户选择目录，不能用简单 repo-root containment 取代现有行为。

## Decisions

### 1. stdout response limit

- `MCPSettings.PythonMaxResponseBytes` 默认 **1 MiB (1,048,576 bytes)**。
- ProcessManager 在 `ReadLine` 得到完整 line 后按 UTF-8 payload bytes（含 framing allowance）检查；超限不截断 JSON。
- 超限标记当前 generation 为 Faulted，pending request 返回 `PYTHON_OUTPUT_LIMIT_EXCEEDED`，并使用 Phase 5.6.2 的 quarantine/cleanup 路径；下一独立 request 才能建立 fresh process。
- 当前实现基于已有 `TextReader.ReadLine`，因此可限制后续解析/路由和进程复用，但不能完全阻止超长单行在 `ReadLine` 内的首次字符串分配。真正 byte-level streaming enforcement 留作单独 IPC 改造，不在本阶段扩大范围。

### 2. stdout protocol integrity

- 空白行属于可恢复 framing noise，记录行为不变并继续读取。
- 非空非法 JSON、缺失/不完整 response schema 或非法 correlation ID 直接返回 `PYTHON_PROTOCOL_ERROR`，不再等到 Python timeout。
- 这类 protocol fault 不会复用当前 child；当前 request 通过现有 targeted quarantine 清理，若没有 pending request，则 process 保持 Faulted，由下一次启动的 stale cleanup 有界清理。
- 格式正确但 unknown、late、completed 或旧 generation 的 correlation response 继续 log + discard，不 quarantine 健康 process。

### 3. stderr diagnostics

- `MCPSettings.PythonMaxStderrBytes` 默认 **64 KiB per process**，语义是 logger diagnostic text 的累计 UTF-8 上限。
- 超过上限只停止记录/截断当前诊断文本，reader 继续消费并丢弃后续内容；不得停止 stderr drain，避免 OS pipe backpressure 卡住 Python。
- 不按 request 关联 stderr；不新增大型隐私框架。stdout protocol 日志不输出完整 payload，stderr 记录现在有界但不宣称内容脱敏。

### 4. Action classification

- `bridge_runner.py` 以 `PRODUCTION_ACTIONS` 和 `TEST_ACTIONS` 两个显式 allowlist dispatch。
- `PythonAllowTestActions` 默认 **false**，ProcessManager 通过受控启动环境变量 `ARCGIS_PRO_MCP_ALLOW_TEST_ACTIONS` 传递；测试/诊断 action 只有显式 true 才执行。
- production default 下 test action 使用既有 `UNKNOWN_ACTION`，不额外暴露 test hook；生产 MCP `tools/list` 不增加 action/tool。
- 不加入 arbitrary Python、eval/import/module blacklist 或 sandbox 声明。

### 5. Path compatibility

- 本阶段不加入 restrictive `PythonAllowedPaths` 或 `PYTHON_PATH_NOT_ALLOWED`。
- 继续保持 Phase 5.5 的 fixed structured readonly action 行为，允许 ArcPy 处理当前运行账户可访问的路径。
- 已完成 policy audit：absolute/relative、FileGDB child、folder workspace、UNC、mapped drive、SDE、`..`、case、reparse/junction、nonexistent/future output path 都不能由 naive `StartsWith` 安全表达。
- 若后续启用 path restriction，必须先定义 canonicalization、reparse handling、ArcPy dataset containment 和 empty-list backward compatibility；该设置是 guardrail，不是 OS sandbox。

## Consequences

- 正常 Phase 5.5 response 远低于 1 MiB；当前实际 MCP payload 最大观测约 1,060 bytes，MCP 封装最大观测约 2,048 bytes（封装大小会随 JSON-RPC id 等 framing 字段变化）。
- 超限或协议破坏会牺牲当前 child 的复用以保护协议可靠性；不会截断后继续解析，也不会 replay 业务 action。
- stderr flood 不会因 logger limit 停止 drain；日志可观测性在预算耗尽后降低，但 Bridge 不会因日志管道填满而被动阻塞。
- Path guardrail 的安全性和 GIS 兼容性仍需单独决策与真实 runtime matrix；Phase 5.6.4 负责修改包 runtime acceptance。

## Verification

- Guardrail/lifecycle tests: **16/16 PASS**，包括 oversized response、malformed JSON、invalid schema、unknown/late ID、stderr flood、production runner test-action disabled/enabled，以及全部 Phase 5.6.2 lifecycle cases。
- Build：0 errors，3×NU1900 environment warnings。
- 当前 ArcGIS Pro 只读 MCP baseline：`tools/list=30`，既有 ping/runtime/discovery regression PASS；修改后的 Add-in 尚未部署，因此 modified-package runtime acceptance remains **NOT VERIFIED**。

## Date

2026-09-03

## Phase

Phase 5.6.3

## Phase 5.6.4 acceptance note

- The new package/runtime preserved the production action boundary: MCP `tools/list=30` contains no `sleep_test`, `raise_test_exception`, or `python_execute`; production defaults keep `PythonAllowTestActions=false`.
- Real ArcGIS Python discovery payloads remained far below the 1 MiB response limit and the persistent process remained usable. Oversized response, malformed JSON/schema, late-response, and stderr-flood behavior remains verified by the internal guardrail/lifecycle fixtures (`16/16 PASS`); those synthetic fault paths are not production MCP business actions and their MCP exposure is not applicable.
- `PythonAllowedPaths` remains `DESIGN COMPLETE / DEFERRED`; this decision does not claim an OS sandbox.
