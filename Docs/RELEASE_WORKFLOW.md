# 发布与安全工作流

> **历史时点声明（D-129 README 链接陈旧面补声明批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **r5 代际（文内无 ISO 日期；时点由正文出现 `ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip` 构建目标与「Phase 7.7」/「Phase 7.8」表述推定）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。
> ★ 本头部点名的当时点位（由本批扫描件机械实测，非人工摘录；现值见上列两件）：`-r5`

本文是 `scripts/user-workflow.ps1` 的用户入口说明。它是薄编排器，不取代现有的打包、事务和客户端配置脚本，也不提供 `ApplyAll`。

## 状态与固定契约

当前状态：`Phase 7 overall = FORMALLY ACCEPTED / PASS / COMPLETE`；`Phase 7.8 = FORMALLY ACCEPTED / PASS`；`1.0.2 planned release scope / canonical 30-tool scope = FORMALLY ACCEPTED / PASS / COMPLETE`；独立 one-click deployment package=`PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER`。

clean-machine support remains `NOT VERIFIED`; Claude Desktop remains optional/template-only. Codex P0、Cursor P1、DeepSeek Harness P1 and installed runtime/UI evidence are accepted only within the Phase 7.7 evidence boundary.

- endpoint：`http://127.0.0.1:6520/mcp`
- server / namespace：`arcgis-pro-mcp`
- canonical production tool count：`30`
- catalog：`Config/client-catalog.json`
- wrapper：`scripts/user-workflow.ps1`
- package delegate：`scripts/package-addin.ps1`
- release delegate：`scripts/release-transaction.ps1`
- client delegate：`scripts/client-config.ps1`

## 1. Build

```powershell
. .\scripts\dev-env.ps1
dotnet build .\ArcGIS-Pro-MCP.sln --no-restore --nologo
```

Build 不会启动 ArcGIS Pro、MCP server 或 Python Bridge。

## 2. Plan 与 Validate

默认动作是只读 `Plan`：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Plan
```

`Validate` 委托给 client configurator 的只读路径：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Validate
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Validate -Client cursor
```

`Plan`/`Validate` 不应创建配置、备份、`.tmp`、安装目标、事务目录或进程。失败时 wrapper 返回非零退出码和固定安全 `errorCode`；不会直接转发 child 的原始输出。

## 3. Package-only

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 `
  -Action Package -Configuration Debug
```

wrapper 始终向 package delegate 传递 `-SkipRegistration`。该动作允许生成发布包和 manifest，但不调用 `RegisterAddIn.exe`，不安装，不启动 runtime。发布包仍需独立检查：版本、Config.daml、first-party DLL hash、ZIP logical path、Bridge artifact、manifest、nested package 和绝对路径泄露。

## 3.1 One-click share package candidate

一键部署分享包是独立于已接受 1.0.2 release 的 candidate。发布者可执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-one-click-package.ps1 -Configuration Debug -CandidateRevision r5 -Force
```

构建器复用并强校验已接受的 `.esriAddInX`，生成独立的 `Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip`，随后用 `scripts/verify-one-click-package.ps1` 审计 ZIP。构建器不注册、不安装、不启动 ArcGIS/Bridge/client、不写用户配置；接收者入口 `ONE-CLICK-SETUP.cmd` 仅在一次明确确认后委托既有安装/客户端事务。r1 的 Gate Keeper rejection 与 r2/r3/r4 不同 revision/hash 保留为历史记录；r5 的预置模拟 smoke、解压包实际只读按钮回调 smoke、完整中文路径/UTF-8 证据、Windows PowerShell 5.1 精确入口、真实 cancel callback、child-completion hard-interruption recovery、pending-index restart blocking 及 recovery-index write-failure/old-latest 证据均独立保留，但 real installation、clean-machine、真实用户 GUI click-through 和真实客户端连接仍为 `NOT VERIFIED`。

## 4. Install / Uninstall / Rollback

所有事务动作都必须明确传入同一套包、manifest、owned install root 和 transaction ledger 路径：

```powershell
$common = @(
  '-PackagePath', '<owned-package.esriAddInX>',
  '-ManifestPath', '<owned-package.release-manifest.json>',
  '-InstallRoot', '<owned-install-root>',
  '-TransactionRoot', '<owned-transaction-root>',
  '-LedgerPath', '<owned-transaction-root>\ledger.json'
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Install @common
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Uninstall @common
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Rollback @common
```

首次执行前应先用 `-DryRun` 做 owned-temp 预演。底层测试注入只在 `release-transaction.ps1` 的 owned-temp 测试中使用，不属于用户入口。wrapper 不复制 transaction ledger、Add-in ID containment、lock 检查、备份或回滚逻辑；它只读取显式 `LedgerPath` 中的 allowlisted 状态并生成安全汇总。本次 one-click candidate 的 builder/isolated tests 不执行真实事务、注册、卸载、回滚或 ArcGIS Pro runtime；真实用户动作仍需明确授权。

## 5. ClientApply / ClientRestore

客户端 mutation 必须一次且仅一次指定 `-Client`：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action ClientApply -Client codex
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action ClientRestore -Client codex
```

允许的 client id 来自 catalog：`codex`、`cursor`、`deepseek-harness`、`claude-desktop`。Claude Desktop 目前是 template-only，不能 Apply。Apply/Restore 不与 Install 组合；不存在 `ApplyAll`。真实路径操作前要先 `Validate -Client <id>`，停止对应客户端，并确认备份元数据和恢复条件。

## 6. 参数和委托规则

| wrapper 动作 | 子脚本 | 关键规则 |
|---|---|---|
| `Plan` | wrapper 内部只读 | 默认动作；不写文件 |
| `Validate` | `client-config.ps1 -Action Validate` | 可选一个 `-Client`；仍只读 |
| `Package` | `package-addin.ps1` | 总是追加 `-SkipRegistration` |
| `Install`/`Uninstall`/`Rollback` | `release-transaction.ps1` | 参数原样边界委托；不复制 mutation |
| `ClientApply` | `client-config.ps1 -Action Apply` | 必须一个明确 `-Client` |
| `ClientRestore` | `client-config.ps1 -Action Restore` | 必须一个明确 `-Client` |

wrapper 只输出 `arcgis-pro-mcp-user-workflow-ledger-v1`，包含 action、readOnly、mutationAttempted、completed、failed、notStarted、脱敏 transaction 状态、safe errorCode 和 allowlisted recovery action。它不输出 child 原始错误、绝对路径、hash、凭据或 transaction ledger 的自由文本。脚本失败必须向调用方传播非零退出码。

## 7. 不得绕过的边界

- 不修改 `MyProject1.aprx` 或 `TestDate/Phase4Test.gdb`。
- 不删除 historical outputs 或未知 `.lock/.sr.lock`。
- 不写入凭据，不打印 child 原始异常，不把历史连接证据当 fresh PASS。
- 不把 `mcp_auth` 计入 30 个生产工具。
- 不把 `1.0.1` rollback baseline 当作成功发布目标；当前 accepted installed release 是 `1.0.2`。
- 不把 clean-machine、HTTP cancellation 或 Claude Desktop template validation 宣称为 PASS。
- Phase 7 与 whole project 已按历史正式记录完成；不得把本 one-click candidate 在 Independent Gate Keeper 复核前写成 formal acceptance。
