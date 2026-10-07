# ArcGIS-Pro-MCP — 永久项目规则（PROJECT_RULES）

> 永久规则。新会话先读本文件 + PROJECT_STATE.md + CURRENT_TASK.md。
> 最高优先级：**仓库 > 文档 > 当前代码 > 当前聊天历史**。

## Phase 规则（Rule 1）
- 严格执行 Phase 0 → 7。不得跳 Phase、不得提前实现后续 Phase。
- 当前 Phase 完成后必须停止，等待用户明确进入下一 Phase。

## 每个 Phase 验收流程（Rule 2）
需求分析 → 代码实现 → Build → Unit Test → Integration Test → Real Runtime Test → Regression Test → Documentation → Final Acceptance Report → 停止。
未完成最终验收不得宣布 PASS。

## 不伪造测试（Rule 3）
- 禁止假测试 / 假运行 / 假兼容 / 假结果 / 假 ArcGIS 数据 / 假 AI 连接。
- 未真实验证必须写 `NOT TESTED`。已实现但未验证写 `IMPLEMENTED / NOT VERIFIED`。

## ArcGIS 线程规则（Rule 4）
- ArcGIS SDK 对象访问必须遵守 Pro 线程模型，按 API 使用 `QueuedTask.Run`（MCT）。
- 禁止为方便从 HTTP/MCP/后台线程直接访问 ArcGIS 对象。

## Shared 层隔离（Rule 5）
- Shared 项目**不得引用** ArcGIS Pro SDK；只存协议/模型/接口/配置/日志/安全/工具抽象/服务器抽象。
- ArcGIS SDK 只能进入 Compatibility / Host 实现层。

## 架构分层（Rule 6）
- `AI Client → MCP Transport → MCP Protocol → MCP Server → Tool Router → Tool Registry → GIS Tool → IArcGISHost → ArcGIS Services → ArcGIS Pro SDK`。
- MCP Server 不得直接操作 ArcGIS SDK；Tool 不得绕过 Host 访问 ArcGIS。

## 不重复实现（Rule 7）
- 同一功能只保留一个权威实现（例如 Tool Registry 唯一注册中心）。

## 配置集中管理（Rule 8）
- 端口 / 超时 / 路径 / 开关统一进入 `ArcGISProMCP.Configuration`，不得散落代码。

## Git 规则（Rule 9）
- 未经用户明确要求，不 `git config` / `commit` / `push`。可 `git status` / `diff` / `log`。

## 系统环境规则（Rule 10）
- 未经授权不安装/卸载软件、不改系统 PATH/注册表/服务、不改 ArcGIS 安装目录、不改用户系统配置。需要安装先报告。

## 上下文 / 记忆规则
- 每次工作开始第一步：读 `PROJECT_STATE.md`、`PROJECT_RULES.md`、`CURRENT_TASK.md`，再读当前 Phase 文件与当前任务相关代码。
- 禁止递归读整个 Docs；禁止把所有 Phase 报告加载进上下文；禁止复制大段源码到回复；禁止重复解释已写入 PROJECT_STATE 的历史。
- 上下文接近上限时执行 `CONTEXT CHECKPOINT`：更新 `PROJECT_STATE.md` + `CURRENT_TASK.md` + 当前 Phase 文件 → 输出 `Context checkpoint created.` → 建议新会话。

## 错误报告规则（Rule 30）
- 不隐藏错误。报告：Error / Cause / Impact / Fix / Verification。无法解决写 `BLOCKED`。

## 用户交互规则（Rule 28）
- 用户说“继续”：先读 PROJECT_STATE + CURRENT_TASK；当前 Phase 未验收则继续，已 PASS 则询问是否进入下一 Phase（不得自动进入）。

## 完整实现策略（Rule 29）
- 用户要求“完整实现”时拆成多个 Task，每个 Task：实现 → Build → Test → 检查 → 更新状态。

## 安全基线
- MCP 默认 `127.0.0.1`，禁止 `0.0.0.0`；端口 6520；Python 6511。不得未经授权开放公网。
- 不执行危险系统命令 / 任意 PowerShell；不绕过 Windows 安全机制；不改 ArcGIS Pro 安装目录。

## 测试基线
- 每个新 Tool 至少具备 Unit Test + Integration Test；涉及 ArcGIS 还需 Real ArcGIS Runtime Test。
- 必须区分 Mock / Fake / Unit / Integration / Real Runtime，不得把 Fake 写成 Real。

## 打包基线
- 使用 `scripts/package-addin.ps1` 生成 `.esriAddInX`（CodeTaskFactory 限制下）。不得未经验证修改打包机制。
