# ArcGIS Pro MCP Server

## 开源发布说明（AGPL-3.0）

- **本仓库包含完整治理历史记录（含 AI 协作轨迹）**：过程文档、评审记录与验收台账随源码一并公开；
  其中治理代号（角色名/席位名）予以保留，**本机绝对路径与身份信息不在公开范围内**
  （相关目录已由 `.gitignore` 排除）。
- **许可**：本仓库以 **AGPL-3.0** 发布（见 `LICENSE`）。本项目为上游 `ArcGIS-Pro-MCP` 项目的衍生作品，
  上游自 v1.1 起采用 AGPL-3.0／商业双许可（v1.0 及更早为 MIT，该授予永久有效）；**本项目在上游基础上的全部修改**
  同样以 AGPL-3.0 提供完整源码，并按 AGPL §5 的要求在此说明"本仓库含修改"，修改明细见提交历史与 `Docs/`。
  AGPL §13 的网络条款意味着：即使不分发副本、仅经网络提供远程交互使用，也触发完整源码提供义务。
- **Esri SDK 不随仓库分发**：`sdk-refs/` 已被完全排除（编译期引用 Esri 官方 SDK 与 `EULA-Esri.txt`，
  再分发将违反 Esri EULA）。使用者需自行安装 ArcGIS Pro 及其 SDK。
- **发布件二进制不在仓库内**：`Release/*.zip`、`Release/发送给同事-*/`、`Archive/` 均被排除，
  一键安装包在本地产出后另行分发，不入库。

这是一个运行在 ArcGIS Pro 内部的 MCP Add-in。它通过本机回环地址向支持 MCP 的客户端提供 GIS 工具。

## 当前状态

```text
Current baseline (recomputed 2026-10-06, D-126 closeout batch):
  canonical production tools = 239 (registry 239 == contract snapshot 239)
  one-click release revision = r29  (ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip)
  installed add-in (239 live) = 1,103,697 bytes / SHA256 59F376555025052DE905DCA05D2902CEBF9915DD4DCD26F4198C9EE2B659E738
  unit test baseline = 1,843 = 1,840 passed / 0 failed / 3 not-executed
Historical acceptance records (kept as history, not current scope):
Phase 7 = FORMALLY ACCEPTED / PASS / COMPLETE
1.0.2 planned release scope / whole-project canonical 30-tool scope = FORMALLY ACCEPTED / PASS / COMPLETE
ONE_CLICK_DEPLOYMENT one-click-1.0.2-r5 = INDEPENDENT GATE PASSED / AUTHORIZED FOR USER REAL-MACHINE TRIAL
Real one-click installation / clean-machine acceptance = NOT VERIFIED
Independent Gate scope = r5 implementation + local isolated automation only; real installation/client connection/public release = NOT VERIFIED
```

本仓库当前 accepted release 为 `1.0.2`，目标为 Windows x64、ArcGIS Pro 3.5。**现值分发代际为 r29**（一键包 `ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip`，2,278,077 字节／SHA256 `099A4F3A575A65A1A8F9A7F424414465…`；其 net8 payload 与现役安装位逐字节同一，装机工具数 **239**）。历史时点记录保留不动：Phase 7 overall、Phase 7.8 Final Handoff 和 canonical **30-tool** scope 已正式接受；r5 一键包曾获 Independent Gate Keeper 有限放行并授权进入用户实机试用——这些都不等同于 clean-machine、真实客户端连接或公开发布 acceptance，r29 同样不改变该边界（`Real installation / clean-machine = NOT VERIFIED`）。Claude Desktop 仍为 optional/template-only，不从历史证据推断额外支持。审批记录见 [ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md](Docs/ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md)（**历史时点审批记录**・对象为 **r5 代际**一键包的有限放行・**不等同于现役代际的当前放行**）；**项目完结基线与全部 NOT VERIFIED 在册清单见 [`AGENTS.md` §4](AGENTS.md) 与 [PROJECT_CLOSEOUT_20261006.md](Docs/PROJECT_CLOSEOUT_20261006.md)**。

## 固定连接契约

| 项目 | 值 |
|---|---|
| Server name / namespace | `arcgis-pro-mcp` |
| Streamable HTTP endpoint | `http://127.0.0.1:6520/mcp` |
| canonical production tools | `239`（D-126 现算：注册 239 ≡ 契约快照 239 ≡ 装机 239） |
| client-scoped helper | `mcp_auth`，不计入生产工具数 |
| client priority | Codex P0、Cursor P1、DeepSeek Harness P1、Claude Desktop P2 optional |

唯一权威客户端目录是 [`Config/client-catalog.json`](Config/client-catalog.json)。叙述性工具数不能替代该目录或实际 `tools/list` 证据。

## 前置条件

- Windows 11 x64；正式支持范围是当前 ArcGIS Pro 3.5 host。
- ArcGIS Pro 3.5 及其 `arcgispro-py3` 环境；其他 ArcGIS Pro 版本不由本阶段推断支持。
- 二进制分享包用户需要 .NET 8 runtime 和系统自带 Windows PowerShell 5.1；不需要 Git、Visual Studio 或 .NET SDK。
- 从源码构建的开发者需要 .NET 8 SDK；PowerShell 7 可选。
- 在执行任何真实安装、客户端 Apply/Restore 或 runtime 检查前，先确认对应阶段已授权，并保存可回滚的 owned target。

## 最短安全入口

### 给普通用户的分享安装包

发布者可从已校验的二进制产物生成不需要开发环境的分享 ZIP：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-share-package.ps1 -Configuration Debug
```

接收者完整解压后，先双击 `INSTALL-PLUGIN.cmd`，再双击 `CONFIGURE-CODEX.cmd`；Cursor、DeepSeek Harness、检查、卸载和回滚统一在 `START-HERE.cmd` 菜单中。详细边界与分发检查见 [分享与简易安装方案](Docs/SHARING_AND_SIMPLE_INSTALL.md)。此入口减少手工参数，但仍保持安装与客户端配置为两个明确动作，不提供 `ApplyAll`。

### 一键部署分享版（当前候选）

发布者可以生成独立的 `ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip`（现役代际；历史代际 r1–r28 已封存，禁止重打包）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-one-click-package.ps1 -Configuration Debug -CandidateRevision r29 -Force
```

**现役 r29 事实（D-143 现算）**：包 2,278,077 字节／SHA256 `099A4F3A…`；双 payload——net8 `59F376555025052D…`/1,103,697 **≡ 现役 239 装机位**（逐字节同一），net6 `ADBF848C4ABC1BCB…`/1,107,215；`canonicalProductionToolCount` 239；包内 verifier `scripts/verify-one-click-package.ps1` 复验通过。签名事实：`scripts/one-click-setup.ps1` 已施加**自签** Authenticode，`Get-AuthenticodeSignature` 读回 **`UnknownError`（系统不信任该证书链，从未 `Valid`）**。真实安装、真实客户端连接、clean-machine、公开发布仍为 `NOT VERIFIED`。逐件台账见 `Release/distribution-ledger-r29.json` 与 `Release/release-record-r29-d143.json`。

以下段落是 **r5 代际的历史记录**（当时的审计与放行范围），保留不改写：

接收者完整解压后双击 `ONE-CLICK-SETUP.cmd`；窗口默认选择 Codex，也可选择 Cursor、DeepSeek Harness 或“仅安装插件”。它先做只读预检，再执行一个插件事务和零个或多个独立客户端事务，最后等待用户在 ArcGIS Pro 中点击 `MCP → Start` 后进行 loopback 诊断。安装器不自动安装依赖、不启动/强制关闭程序、不使用 `ApplyAll`。r1 的 GUI worker 缺陷记录、r2 的两个不同内容 hash、r3 candidate 和 r4 candidate 均已保留；当前 r5 已获 Independent Gate Keeper 有限放行，授权进入用户实机试用。r5 已通过 ZIP/解压根审计、Windows PowerShell 5.1 精确入口、122 项 owned-temp recovery/取消/硬中断断言、显式 UTF-8 encoded bootstrap/父进程解码与 dot-source scope 的顶层中文路径 round-trip 断言、预置模拟 smoke，以及最终解压包实际只读 `Preflight`/`Diagnose` 按钮回调（完整中文路径目标和 UTF-8 中文消息）；失败诊断会在清理 owned temp 前保留完整必要 artifacts；real installation、真实客户端连接、clean-machine、公开发布和真实用户 GUI click-through 仍 `NOT VERIFIED`。详见 [一键部署指南](Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md)（历史时点）、[候选报告](Docs/ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md)（历史时点） 和 [独立审批记录](Docs/ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md)（历史时点）。

### 开发者工作流

在你实际克隆或解压的仓库根目录执行。先把当前目录切换到该根目录；下面命令都使用仓库相对路径。默认入口是只读 `Plan`，不会创建目录、配置、备份、包、安装目标或进程：

```powershell
Set-Location '<your-clone-or-extract-root>'
. .\scripts\dev-env.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Plan
```

只读检查客户端目录和现有配置：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Validate
```

编译和仅生成发布包（始终跳过 Add-in 注册）：

```powershell
dotnet build .\ArcGIS-Pro-MCP.sln --no-restore --nologo
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Package -Configuration Debug
```

`user-workflow.ps1` 只做参数边界、catalog 校验、安全 ledger 和委托；实际打包、事务安装和客户端配置逻辑仍由现有脚本负责。没有 `ApplyAll`，也没有把安装和客户端配置合并成一个动作。

Install、Uninstall、Rollback 必须提供显式 owned `LedgerPath`。wrapper 只读取其中的 allowlisted event/status，输出 `completed`、`failed`、`notStarted` 和可验证的 recovery action，不转发 child 原始输出或绝对路径。

## 当前用户文档

- [用户指南](Docs/USER_GUIDE.md)（历史时点）：前置条件、支持范围、状态/健康、日志、故障恢复和限制。
- [发布与安全工作流](Docs/RELEASE_WORKFLOW.md)（历史时点）：Build、Package、安装/卸载/回滚、Plan/Validate 和客户端单目标操作。
- [客户端配置指南](Docs/CLIENT_CONFIGURATION_GUIDE.md)：Codex/Cursor/DeepSeek/Claude 的目录、模板和凭据边界。
- [1.0.2 发布说明](Docs/RELEASE_NOTES_1.0.2.md)（历史时点）：精确 release identity、支持矩阵、限制和回滚规则。
- [一键部署用户指南](Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md)（历史时点）：分享包入口、预检、独立事务、恢复和实机证据边界。
- [一键部署候选报告](Docs/ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md)（历史时点） 与 [证据索引](Docs/ONE_CLICK_DEPLOYMENT_EVIDENCE_INDEX.md)（历史时点）：当前 one-click candidate 的 hash、测试和未验证范围。
- [Phase 7 项目最终接受候选](Docs/phases/PHASE_07_FINAL_PROJECT_ACCEPTANCE_CANDIDATE.md)（历史时点）：Phase 0–7 状态矩阵、1.0.2 scope boundary 和最终候选证据索引。
- [Phase 7.8 最终交接候选](Docs/phases/PHASE_07_8_FINAL_HANDOFF_CANDIDATE.md)（历史时点）：最终 evidence index 和当前 acceptance boundary。
- [Phase 7.7 overall acceptance](Docs/phases/PHASE_07_7_OVERALL_RELEASE_ACCEPTANCE_CANDIDATE.md)（历史时点）：完整 7.7 证据矩阵及正式接受记录。
- [当前项目状态](Docs/PROJECT_STATE.md)（历史时点） 与 [当前任务](Docs/CURRENT_TASK.md)（历史时点）：跨会话接管入口。
- **[项目完结报告 · 2026-10-06](Docs/PROJECT_CLOSEOUT_20261006.md)**：五项完结条件对账、六基线现值、D-116…D-125 十批 CLOSED 链、全部 NOT VERIFIED 在册清单与复裁触发四项。**现行基线口径以 [`AGENTS.md` §4](AGENTS.md) 为准。**

历史报告中的 `PASS CANDIDATE`、`BLOCKED_BY_HARNESS`、`NOT VERIFIED` 和旧阶段状态仍是历史证据；当前正式状态以 `AGENTS.md`（§3 安全基线／§4 基线现值／§5 受保护资产）与 [项目完结报告](Docs/PROJECT_CLOSEOUT_20261006.md) 为准，Phase 7 项目最终接受候选作历史时点记录。

## 安全边界

以下资产不属于普通用户工作流的可写目标：

- `MyProject1.aprx`；
- `TestDate/Phase4Test.gdb`；
- historical outputs、未知 `.lock/.sr.lock`；
- 未明确 owned 的安装目录、客户端配置或凭据。

客户端操作必须一次指定一个明确的 `-Client`。不要把 token、password、secret、provider、model、header 或 login 写入模板或命令行。生产 MCP endpoint 默认只绑定 `127.0.0.1:6520/mcp`。

## 当前未宣称的能力

clean-machine support 仍为 `NOT VERIFIED`；HTTP client-disconnect cancellation 仍为 `NOT VERIFIED`；7 个 HTTP transport tests 仍为 `BLOCKED_BY_HARNESS`；raw initialize/tools-list envelope 仍受 client surface 限制；`list_maps`/`get_map_info` 为 `PARTIAL`；`get_dataset_info`/`get_raster_info` 为 `LIMITED IMPLEMENTATION`；ArcGISProject.isDirty 在 Pro 3.5 不可用；`select_layer` 没有确定性 OID contract。Codex P0、Cursor P1、DeepSeek Harness P1 的 Phase 7.7 fresh client gates 已正式接受，Claude Desktop 不在 fresh acceptance 范围内。
