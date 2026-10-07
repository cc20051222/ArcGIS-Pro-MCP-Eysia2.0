# ArcGIS Pro MCP Server

运行在 ArcGIS Pro 内部的 MCP Add-in：通过本机回环 `http://127.0.0.1:6520/mcp` 向支持 MCP 的客户端提供 **239 个 GIS 工具**（注册表 239 ≡ 契约快照 239 ≡ 装机 239）。

## English summary

**ArcGIS Pro MCP Server** is an ArcGIS Pro add-in that exposes **239 GIS tools** to MCP-speaking
clients over a **local loopback** endpoint (`http://127.0.0.1:6520/mcp`) — no public network listener.
It is installed on Windows x64 with ArcGIS Pro 3.5 and can be wired to Codex, Cursor, DeepSeek
Harness and (as a template only) Claude Desktop. A one-click installer package is produced locally and
**shipped outside this repository**; the step-by-step install and usage tutorials live under `Docs/`.
Licensed **AGPL-3.0**; the Esri SDK is referenced at compile time and never redistributed.
English index of what to read first: [`Docs/README_EN.md`](Docs/README_EN.md).

## 特性清单

- [x] **239 个生产 GIS 工具**，按用途分组并附示例提示词（见使用教程）
- [x] **多客户端接入**：Codex（P0）、Cursor（P1）、DeepSeek Harness（P1）、Claude Desktop（P2，模板可选）
- [x] **一键部署安装器**：只读预检 → 单个插件事务 → 零或多个独立客户端事务 → 回环诊断（无 `ApplyAll`）
- [x] **工具可视化与数据文件夹工作流**（含只读/会话/写入名册三面与错误码契约）
- [x] **纯本机回环安全边界**：默认仅绑定 `127.0.0.1:6520/mcp`，禁止 `0.0.0.0` 与公网暴露
- [x] **发布可复核**：每个安装包附同名 `.sha256`，`scripts/verify-one-click-package.ps1` 逐件复验

> 未宣称的能力与 `NOT VERIFIED` 全清单见文末[当前未宣称的能力](#当前未宣称的能力)一节，本仓库不以任何历史放行替代真机验收。

## 软件适配（先判断「我这台机器能不能用」）

**首屏指路**：安装包见下一节「**安装包获取**」（★不在本仓库内）｜安装教程 [`Docs/教程-1-安装教程.md`](Docs/教程-1-安装教程.md)｜使用教程 [`Docs/教程-2-使用教程.md`](Docs/教程-2-使用教程.md)。

下表全部数值为 **2026-10-08** 由执行席在本机与 Esri 官方页面现测；版本类事实随时间漂移，故逐行标注 As-of 与来源。

| 维度 | 实测结论 | 取证依据 |
|---|---|---|
| **操作系统** | **Windows 11 x64 ＝ 已实测**（本机内部版本 **build 26300**）｜★**Windows 10 ＝ NOT VERIFIED**（未取证，不做支持宣称） | `sys.getwindowsversion()` ＋ 注册表 `CurrentBuildNumber=26300`（注：`ProductName` 因兼容仍为旧字面值，故以 build 号为准） |
| **ArcGIS Pro 版本** | ★**本项目在 ArcGIS Pro 3.5 上实测（`FileVersion=3.5.0.57366`）**；一键包**双 payload 的最小宿主逐行不同**：**net6 payload → `3.0`**／**net8 payload → `3.5.0`**（★装错 payload 会失败）；**3.6／3.7 依官方兼容性承诺支持・未在本项目实机验证**；**3.x 以外（2.x 及更早、未来 4.x）不支持** | PE 版本资源（`\StringFileInfo\000004b0\FileVersion`）＋ 包内 `bundle-manifest.json` 的 `payloads[].arcgisProDesktopVersion` |
| **.NET Runtime** | 运行端＝**.NET 8 runtime**；★**源码编译目标＝`net6.0`**——`Source/` **8 个 csproj 全为 net6**（`Compatibility`＝`net6.0-windows`，Shared 下 7 个＝`net6.0`），`Tests/` **5 个＝`net8.0`** | 逐 `.csproj` 读取 `<TargetFramework>`（排除 `bin`/`obj`） |
| **PowerShell** | **Windows PowerShell 5.1**（系统自带，无需另装；PowerShell 7 可选） | 安装器与校验脚本入口口径 |
| **Python 环境** | ArcGIS Pro 内置 **`arcgispro-py3`**（本机实测存在） | 本机宿主检查 |
| **MCP 客户端** | `codex` **P0**／`cursor` **P1**／`deepseek-harness` **P1**／`claude-desktop` **P2＝optional・仅模板校验** | `Config/client-catalog.json` 的 `priority`／`required`／`applyMode` 逐字段实测 |
| **磁盘／内存** | **NOT VERIFIED**（最低磁盘与内存需求本批未取证，本行不做数值宣称） | 见上：不做未取证宣称 |
| **「最新版本」对照（As-of 2026-10-08）** | 本项目锁定 **3.5**；同日在 Esri 官方修补程序页可读到的 3.7 线最新件＝**3.7.2**（公告页自述 *Published October 5, 2026*）⇒ **本项目落后官方最新次版本 2 档**；★官方 SDK 仓库另列 **3.7 SDK＝3.7.0.1901（要求 .NET 10 运行时）**／**3.6 SDK＝3.6.0.59527** | [Esri 修补程序索引](https://support.esri.com/en-us/patches-updates/2026)｜[3.7.2 公告](https://support.esri.com/en-us/patches-updates/2026/arcgis-pro-3-7-patch-2-3-7-2-announcement)｜[官方 SDK Releases](https://github.com/esri/arcgis-pro-sdk/releases) |
| **Pro 补丁版本** | 3.5 线官方最新补丁＝**3.5.9**（公告页自述 *Published September 17, 2026*）；★**本机实测 3.5.0 ＝ 未打补丁**；3.6 线同日索引最高＝**3.6.5** | [3.5.9 公告](https://support.esri.com/en-us/patches-updates/2026/arcgis-pro-3-5-patch-9-3-5-9-announcement) |
| **生命周期时间窗** | **NOT VERIFIED**——★本席本轮无法以可读文本取回官方生命周期矩阵中的具体日期，故本行**不写任何日期数值**；官方矩阵以文档形式发布，链接附右 | [ArcGIS Pro 生命周期页](https://support.esri.com/en-us/products/arcgis-pro/life-cycle)｜[Esri Product Life Cycle 文档](https://content.esri.com/support/techarticles/product-life-cycle.pdf) |
| 装机位置佐证 | 本机 `%USERPROFILE%\Documents\ArcGIS\AddIns\ArcGISPro` **存在**＝2 个 GUID 子目录／3 个文件／合计 **2,148,771 B**，其中本项目 `ArcGISProMCP.Compatibility.esriAddInX` **1,103,697 B**（另有同目录 `.pre-5.5.4-185110.bak` 185,110 B 与一个与本无关的 `GeoSceneProAIAppModule.esriAddinX` 859,964 B）；`%APPDATA%\ESRI`／`%LOCALAPPDATA%\ESRI` 下**无 `AddIns` 目录** | 本机递归枚举（与包内 manifest 的安装位置记载一致） |

★**四项诚实边界（不得省略）**：①`cleanMachineAcceptance = NOT VERIFIED`（包内 `bundle-manifest.json` 的自我声明，★干净机验收未做）｜②**Windows 10 未验证**｜③**非 3.x 的 ArcGIS Pro 不支持**｜④**3.6／3.7 未在本项目实机验证**。

**关键句**：本项目在 ArcGIS Pro **3.5** 上实测（`3.5.0.57366`）。ArcGIS Pro **3.6**（官方要求 .NET 8，与 3.5 同内核）与 **3.7**（官方声明 3.x SDK 扩展免重编；本项目 `Source/` 264 件 `.cs` 内 `Clipboard`／`BinaryFormatter`／`DragAndDrop` 命中文件数**全 0**，故不属官方列出的需重编情形）**按官方兼容性承诺支持，但未在本项目实机验证。3.x 以外的版本未支持。**

## 安装包获取（★先读这一节）

**一键安装包不在本仓库内。** 仓库只含源码、脚本与文档；`Release/*.zip`、`Release/发送给同事-*/`、`Archive/`
均已被 `.gitignore` 排除，因此**克隆本仓库不能直接得到可双击的安装包**。获取方式：

1. **由发布者线下提供**当前代际分享包 `ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip`
   （现役 **r29**：**2,278,077 字节**／SHA256 `099A4F3A575A65A1A8F9A7F424414465E676E605B12D8C1B9A7BA36E6F36B3CE`，
   简写 `099A4F3A…`）；历史代际 r21–r28 已封存，禁止重打包或覆盖。
2. **拿到后先校验，再解压**（Windows 内置工具，无需额外安装）：

   ```powershell
   certutil -hashfile .\ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip SHA256
   ```

   结果须与同名 `.sha256` 侧车文件内记录的摘要逐字符一致；不一致即停止使用并向索取方回报。
3. **自行从源码产出**（开发者路径，需要 .NET 8 SDK 与编译期 SDK 引用）：见下文[开发者工作流](#开发者工作流)。
   产物落在本地 `Release/`，**不会**也不应被提交进仓库。

★真实安装、真实客户端连接、clean-machine 验收与公开发布均为 **NOT VERIFIED**：包内自动化与只读复验通过，不等于在用户机器上安装成功过。

## 安装教程（6 步・首屏指路）

**普通用户请看：[`Docs/教程-1-安装教程.md`](Docs/教程-1-安装教程.md)** — 7,201 B／139 行／6 步：
拿到包 → 校验 SHA-256 → 解压到 D 盘新目录 → 双击 `ONE-CLICK-SETUP.cmd` 按界面确认 → 在 ArcGIS Pro 的 MCP 页启动本机服务 → 客户端接入；含**出错对照表**与**卸载回滚**路径。

## 使用教程（9 章・首屏指路）

**装好之后请看：[`Docs/教程-2-使用教程.md`](Docs/教程-2-使用教程.md)** — 27,856 B／401 行／9 章：
按用途分组的 **239 工具**清单与**示例提示词**、地图与数据文件夹工作流、只读/写入边界、日志与诊断读法。

## 快速开始

```powershell
# 1) 校验并解压（保持包内目录结构），双击 ONE-CLICK-SETUP.cmd，按界面选择客户端
# 2) 启动 ArcGIS Pro → MCP 页 → Start（本机服务监听 127.0.0.1:6520/mcp）
# 3) 在客户端配置里接入该 endpoint（Codex / Cursor / DeepSeek Harness 各选一，不批量）
# 4) 需要回滚时：同一目录双击 RECOVERY-MENU.cmd，按界面操作并保留原有备份
```

安装器**不自动安装依赖**、**不启动或强杀程序**、**不提供 `ApplyAll`**；插件安装与客户端配置是两个明确动作。

## 固定连接契约

| 项目 | 值 |
|---|---|
| Server name / namespace | `arcgis-pro-mcp` |
| Streamable HTTP endpoint | `http://127.0.0.1:6520/mcp` |
| canonical production tools | `239`（本批现算：注册 239 ≡ 契约快照 239 ≡ 装机 239） |
| client-scoped helper | `mcp_auth`，不计入生产工具数 |
| client priority | Codex P0、Cursor P1、DeepSeek Harness P1、Claude Desktop P2 optional |

唯一权威客户端目录是 [`Config/client-catalog.json`](Config/client-catalog.json)（现算含 4 个条目：`codex`、`cursor`、`deepseek-harness`、`claude-desktop`）。叙述性工具数不能替代该目录或实际 `tools/list` 证据。

## 前置条件

- Windows 11 x64；正式支持范围是当前 ArcGIS Pro 3.5 host。
- ArcGIS Pro 3.5 及其 `arcgispro-py3` 环境；其他 ArcGIS Pro 版本不由本阶段推断支持。
- 二进制分享包用户需要 .NET 8 runtime 和系统自带 Windows PowerShell 5.1；不需要 Git、Visual Studio 或 .NET SDK。
- 从源码构建的开发者需要 .NET 8 SDK；PowerShell 7 可选。
- 在执行任何真实安装、客户端 Apply/Restore 或 runtime 检查前，先确认对应阶段已授权，并保存可回滚的 owned target。

## 开发者工作流

在实际克隆或解压的仓库根目录执行（全部使用仓库相对路径；默认入口是只读 `Plan`，不创建目录、配置、备份、包、安装目标或进程）：

```powershell
Set-Location '<your-clone-or-extract-root>'
. .\scripts\dev-env.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Plan
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Validate
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Package -Configuration Debug
```

`user-workflow.ps1` 只做参数边界、catalog 校验、安全 ledger 和委托；实际打包、事务安装和客户端配置逻辑仍由现有脚本负责。没有 `ApplyAll`，也没有把安装和客户端配置合并成一个动作。Install、Uninstall、Rollback 必须提供显式 owned `LedgerPath`；wrapper 只读取 allowlisted event/status，不转发 child 原始输出或绝对路径。

## 当前状态与基线现值

```text
Current baseline (recomputed 2026-10-06, D-126 closeout batch; 本批 2026-10-07 现场复算一致):
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

本仓库当前 accepted release 为 `1.0.2`，目标为 Windows x64、ArcGIS Pro 3.5。**现值分发代际为 r29**（一键包 `ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip`，2,278,077 字节／SHA256 `099A4F3A…`；其 net8 payload 与现役安装位逐字节同一，装机工具数 **239**）。历史时点记录保留不动：Phase 7 overall、Phase 7.8 Final Handoff 和 canonical **30-tool** scope 已正式接受；r5 一键包曾获 Independent Gate Keeper 有限放行并授权进入用户实机试用——这些都不等同于 clean-machine、真实客户端连接或公开发布 acceptance，r29 同样不改变该边界（`Real installation / clean-machine = NOT VERIFIED`）。Claude Desktop 仍为 optional/template-only，不从历史证据推断额外支持。审批记录见 [ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md](Docs/ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md)（**历史时点审批记录**・对象为 **r5 代际**一键包的有限放行・**不等同于现役代际的当前放行**）；**项目完结基线与全部 NOT VERIFIED 在册清单见 [`AGENTS.md` §4](AGENTS.md) 与 [PROJECT_CLOSEOUT_20261006.md](Docs/PROJECT_CLOSEOUT_20261006.md)**。

现役 r29 事实（本批现算）：包 2,278,077 字节／SHA256 `099A4F3A…`；双 payload——net8 `59F376555025052D…`/1,103,697 **≡ 现役 239 装机位**（逐字节同一），net6 `ADBF848C4ABC1BCB…`/1,107,215；`canonicalProductionToolCount` 239；包内 verifier `scripts/verify-one-click-package.ps1` 复验通过。签名事实：`scripts/one-click-setup.ps1` 已施加**自签** Authenticode，`Get-AuthenticodeSignature` 读回 **`UnknownError`（系统不信任该证书链，从未 `Valid`）**。真实安装、真实客户端连接、clean-machine、公开发布仍为 `NOT VERIFIED`。逐件台账见 `Release/distribution-ledger-r29.json` 与 `Release/release-record-r29-d143.json`（★二者在本地发布目录中，**不随仓库分发**）。

以下段落是 **r5 代际的历史记录**（当时的审计与放行范围），保留不改写：

接收者完整解压后双击 `ONE-CLICK-SETUP.cmd`；窗口默认选择 Codex，也可选择 Cursor、DeepSeek Harness 或“仅安装插件”。它先做只读预检，再执行一个插件事务和零个或多个独立客户端事务，最后等待用户在 ArcGIS Pro 中点击 `MCP → Start` 后进行 loopback 诊断。安装器不自动安装依赖、不启动/强制关闭程序、不使用 `ApplyAll`。r1 的 GUI worker 缺陷记录、r2 的两个不同内容 hash、r3 candidate 和 r4 candidate 均已保留；当前 r5 已获 Independent Gate Keeper 有限放行，授权进入用户实机试用。r5 已通过 ZIP/解压根审计、Windows PowerShell 5.1 精确入口、122 项 owned-temp recovery/取消/硬中断断言、显式 UTF-8 encoded bootstrap/父进程解码与 dot-source scope 的顶层中文路径 round-trip 断言、预置模拟 smoke，以及最终解压包实际只读 `Preflight`/`Diagnose` 按钮回调（完整中文路径目标和 UTF-8 中文消息）；失败诊断会在清理 owned temp 前保留完整必要 artifacts；real installation、真实客户端连接、clean-machine、公开发布和真实用户 GUI click-through 仍 `NOT VERIFIED`。详见 [一键部署指南](Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md)（历史时点）、[候选报告](Docs/ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md)（历史时点） 和 [独立审批记录](Docs/ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md)（历史时点）。

## 文档索引（按「谁该读什么」重组）

★每条后的**在库**＝该文件在 `git ls-files` 克隆面内（本批以大小写敏感方式逐条实测）；**本地**＝只存在于发布者的本地发布目录，不随仓库分发。

**① 普通用户（先装，再用）**

- **在库** [`Docs/教程-1-安装教程.md`](Docs/教程-1-安装教程.md)：6 步安装・出错对照表・卸载回滚。
- **在库** [`Docs/教程-2-使用教程.md`](Docs/教程-2-使用教程.md)：9 章・239 工具分组与示例提示词。
- **在库** [`Docs/USER_GUIDE.md`](Docs/USER_GUIDE.md)（历史时点）：前置条件、支持范围、状态/健康、日志、故障恢复和限制。
- **在库** [`Docs/SHARING_AND_SIMPLE_INSTALL.md`](Docs/SHARING_AND_SIMPLE_INSTALL.md)：分享包入口与分发检查。
- **在库** [`Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md`](Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md)（历史时点）：分享包入口、预检、独立事务、恢复和实机证据边界。

**② 客户端配置**

- **在库** [`Docs/CLIENT_CONFIGURATION_GUIDE.md`](Docs/CLIENT_CONFIGURATION_GUIDE.md)：Codex/Cursor/DeepSeek/Claude 的目录、模板和凭据边界。
- **在库** [`Config/client-catalog.json`](Config/client-catalog.json)：唯一权威客户端目录（4 条目）。

**③ 开发者与打包**

- **在库** [`Docs/RELEASE_WORKFLOW.md`](Docs/RELEASE_WORKFLOW.md)（历史时点）：Build、Package、安装/卸载/回滚、Plan/Validate 和客户端单目标操作。
- **在库** [`Docs/RELEASE_NOTES_1.0.2.md`](Docs/RELEASE_NOTES_1.0.2.md)（历史时点）：精确 release identity、支持矩阵、限制和回滚规则。
- **在库** [`Docs/ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md`](Docs/ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md)・[`Docs/ONE_CLICK_DEPLOYMENT_EVIDENCE_INDEX.md`](Docs/ONE_CLICK_DEPLOYMENT_EVIDENCE_INDEX.md)（均历史时点）：one-click candidate 的 hash、测试和未验证范围。
- **在库** [`scripts/verify-one-click-package.ps1`](scripts/verify-one-click-package.ps1)・[`scripts/one-click-setup.ps1`](scripts/one-click-setup.ps1)：包级复验与部署入口实现。

**④ 治理与审计（本项目把协作轨迹一并公开）**

- **在库** [`AGENTS.md`](AGENTS.md)：§3 安全基线／**§4 基线现值**／§5 受保护资产——★现行基线口径以此为准。
- **在库** [`Docs/PROJECT_CLOSEOUT_20261006.md`](Docs/PROJECT_CLOSEOUT_20261006.md)：五项完结条件对账、六基线现值、十批 CLOSED 链、**全部 NOT VERIFIED 在册清单**与复裁触发四项。
- **在库** [`Docs/PROJECT_STATE.md`](Docs/PROJECT_STATE.md)・[`Docs/CURRENT_TASK.md`](Docs/CURRENT_TASK.md)（历史时点）：跨会话接管入口。
- **在库** [`Docs/ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md`](Docs/ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md)（历史时点审批，对象 r5 代际）。
- **本地**（不随仓库分发）：`Release/distribution-ledger-r21…r29.json`、`Release/release-record-r24…r29*.json`、`Release/发送给同事-r29/` 教程载体、九代一键包 zip 本体。
- ★Phase 0–7 的三份历史候选（final acceptance／final handoff／overall acceptance）位于 `Docs/phases/`，该目录已被 `.gitignore` 排除以守住「不公开本机路径与身份」的裁定，因此**本 README 不再提供其链接**；其结论已在 [`AGENTS.md` §4](AGENTS.md) 与 [`Docs/PROJECT_CLOSEOUT_20261006.md`](Docs/PROJECT_CLOSEOUT_20261006.md) 中复述并可复核。

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

## 安全边界

以下资产不属于普通用户工作流的可写目标：

- `MyProject1.aprx`；
- `TestDate/Phase4Test.gdb`；
- historical outputs、未知 `.lock/.sr.lock`；
- 未明确 owned 的安装目录、客户端配置或凭据。

客户端操作必须一次指定一个明确的 `-Client`。不要把 token、password、secret、provider、model、header 或 login 写入模板或命令行。生产 MCP endpoint 默认只绑定 `127.0.0.1:6520/mcp`。

## 当前未宣称的能力

clean-machine support 仍为 `NOT VERIFIED`；HTTP client-disconnect cancellation 仍为 `NOT VERIFIED`；7 个 HTTP transport tests 仍为 `BLOCKED_BY_HARNESS`；raw initialize/tools-list envelope 仍受 client surface 限制；`list_maps`/`get_map_info` 为 `PARTIAL`；`get_dataset_info`/`get_raster_info` 为 `LIMITED IMPLEMENTATION`；ArcGISProject.isDirty 在 Pro 3.5 不可用；`select_layer` 没有确定性 OID contract。Codex P0、Cursor P1、DeepSeek Harness P1 的 Phase 7.7 fresh client gates 已正式接受，Claude Desktop 不在 fresh acceptance 范围内。

历史报告中的 `PASS CANDIDATE`、`BLOCKED_BY_HARNESS`、`NOT VERIFIED` 和旧阶段状态仍是历史证据；当前正式状态以 `AGENTS.md`（§3 安全基线／§4 基线现值／§5 受保护资产）与 [项目完结报告](Docs/PROJECT_CLOSEOUT_20261006.md) 为准，Phase 7 项目最终接受候选作历史时点记录。
