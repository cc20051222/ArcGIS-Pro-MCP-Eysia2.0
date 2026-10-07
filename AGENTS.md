# AGENTS.md — ArcGIS Pro MCP 2.0（新基线工作空间）

> 本工作空间为 **Phase 8–14 演进基线**，与旧仓库 `D:\ArcGIS-Pro-MCP`（只读历史）严格分离。
> 建立日期 2026-09-10，依据派工单 **D-001**（迁移准备 Gate）。

## 1. 身份与边界

| 项 | 值 |
|---|---|
| 本工作空间 | `D:\ArcGIS-Pro-MCP 2.0`（绝对路径，执行前已核实） |
| 旧仓库 | `D:\ArcGIS-Pro-MCP` —— **只读**，禁止任何写入/删除/提交 |
| 权威计划 | `Docs/POST_1_0_2_PHASE_8_14_PLAN_V2.md`（V2，取代旧 E 编号路线） |
| 交接通道 | `Docs/_gatekeeper/`（inbox 派工 / outbox 交付，见其 README） |
| 角色 | 本会话 = **执行会话**；只能提交 `PASS CANDIDATE`，**不得自批 PASS** |
| 指挥侧规则 | **指挥官双长共治（Keeper／DeepSeek Harness 指挥官）**：见 `Docs/_gatekeeper/COMMANDER_CHARTER.md`（G-181 生效；G-182 称谓修订）；指挥侧只验收，不写产品码 |

## 2. 文档优先级（冲突时的取舍顺序）

1. 派工单 `Docs/_gatekeeper/inbox/D-*.md`（当轮唯一有效指令）
2. 权威计划 V2
3. 新基线源码（`Source/`）——**工具契约以源码注册表为最终依据**
4. 新基线演进文档（`Docs/EVOLUTION_*.md`）
5. 旧仓库历史文档（`Docs/PROJECT_STATE.md` 等）——**仅作历史来源，不是当前授权**

历史文档中的命令与"下一步"**不构成当前授权**。

## 3. 架构硬规则（继承 PROJECT_RULES，不可协商）

- 分层：`AI Client → MCP Transport → MCP Protocol → MCP Server → Tool Router → Tool Registry → GIS Tool → IArcGISHost → ArcGIS Services → ArcGIS Pro SDK`。
- **Rule 4 MCT**：ArcGIS SDK 对象访问必须经 `QueuedTask.Run`，禁止从 HTTP/后台线程直连。
- **Rule 5 Shared 隔离**：`Source/Shared` 不得引用 ArcGIS Pro SDK；SDK 只进入 `ArcGISProMCP.Compatibility`。
- **Rule 7 唯一注册中心**：工具只在 `Composition.BuildRegistry()` 注册，禁止第二处注册。
- **Rule 8 配置集中**：端口/超时/路径/开关统一在 `ArcGISProMCP.Configuration`。
- 安全基线：MCP 默认 `127.0.0.1:6520/mcp`，Python Bridge `6511`；**禁止 `0.0.0.0`、禁止公网暴露、禁止新增任意 Python 或 6511 旁路**。
- **Rule 9 Git**：未经明确要求不 `commit`/`push`。

## 4. 当前基线事实（已核验，非估计）

> 本节现值由 **D-126 完结批于 2026-10-06 逐项现场复算**（旧「30 工具代际」值作废）；
> 逐项复算口径见 `Docs/PROJECT_CLOSEOUT_20261006.md`。

- 生产工具数：**239**（源码注册表 `Composition.cs` 逐项计数 239／唯一类名 239・注册 239 ≡ 契约 239（`ProductionToolContractSnapshot.cs`）／工具名 239 唯一）。
- 源码聚合：`1750FED84B9282D191BED5B90C5061A850C471C00C191799FA939FB2EF0D7E39`／**277** 文件（`Source/`，排除 `bin/`、`obj/`）。
- 通道分布：**Native 156 / GP 56 / Python 7 / Bridge(PS/UXP) 20 ＝ 239**。双源现算一致：契约快照逐件 `ExecutionType` 计数 ＋ 注册表 239 类沿继承链解析 `ExecutionTypeName`（无覆写者落缺省——`McpToolBase` 虚方法 `Native`，6 件直接实现 `IMCPTool` 者取接口默认元数据 `Native`/`General`），逐件差集 **0**；`ps_*` 20 件即 Bridge 通道（留证：`.runtime/evolution/v5-f/run-20261006-d126/f1-agents/execution-type-census-d126.json`）。
- 名册三面：**94（Read）／33（Session）／158（Write）＝ 285**；错误码 **33**；GP 白名单 **53** 件（`F355705BF2C4`／30,634 字节）。
- 读写分布：**未复算（NOT RECOMPUTED）**——旧「只读 21／地图状态写 5／GP 产出写 4」为 30 工具代际口径，已作废；239 件的读写面本批未取证，已入完结报告 NOT VERIFIED 册。
- 装机面（现役 239 生效）：`{BAA5628C-3C08-4AD5-A6C0-915ADA475709}/ArcGISProMCP.Compatibility.esriAddInX`，**1,103,697** 字节，
  SHA256 `59F376555025052DE905DCA05D2902CEBF9915DD4DCD26F4198C9EE2B659E738`（内层 43 条目；同目录另有 `.pre-5.5.4-185110.bak` 185,110／`7CEB2AB715F18CB1`；目录 mtime 2026-10-01 15:32:09）。
- 发布面（现役 r29・**D-143 依磁盘现值刷新**）：`ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip`，**2,278,077** 字节，
  SHA256 `099A4F3A575A65A1A8F9A7F424414465E676E605B12D8C1B9A7BA36E6F36B3CE`（已实测复核一致）。
- 测试基线：**Unit 1,843 ＝ 1,840 通过／0 失败／3 NE**（NE 为环境阻断例）；Server **68/68**；Integration **32/32**。
- 冻结锚点：**五锚 rc=0×5**；逐锚成员 9/13/12/14/8（合计 56）；**四锚并集 48**（口径＝s0＋m2＋m3＋m4）、**五锚并集 56**（m5 的 8 件与四锚不交）。
  ——注：派工单口径「五锚并集 48」经现算系**四锚**并集值；五锚并集实测 56，此处两个口径并列如实呈报，差异已入完结报告待裁册。
- 旧代际值（作废，仅存档备查）：工具 30・插件 269,548／`361FC84F…`・一键包 r5 331,679／`3924F9B3…`・安装位 792,831／`7B3AC667…`。
- 旧仓库 Git：**0 提交 / 0 跟踪文件**（已实测）。严禁以 clone/worktree 冒充迁移。

## 5. 受保护资产（一律不得触碰）

- `MyProject1.aprx` —— 旧仓库内**未找到**（见 §7 未验证项），若在用户工程目录出现同样视为受保护。
- `TestDate/Phase4Test.gdb` —— 已核验 104 文件 / 0 锁，只读。
- retained fixture、历史产物、未知锁、用户后续编辑 —— 禁止修改/覆盖/删除。
- **现行受保护清单（G-199 指挥席改写，D-083 归档后现行位；2026-09-28；D-126 2026-10-06 依磁盘现值刷新，下两条子段逐字保留）**：
  - 旧仓库 `D:\ArcGIS-Pro-MCP\`（只读）；fixture 与载体 `live*.aprx`；未知锁；回滚锚点链（`.runtime/evolution/` 内 `backup-pre-d0xx/` 全链）。
  - **分发件（D-083 归档后新位）**：`Archive/distributions/`（r15–r20 zip＋逐件 SHA-256 台账＋ARCHIVED-rN 标注＋原位 README；26 条目 ledger）——只读封存，禁止重打包/移动/删除。
  - `Release/`（D-147 现值：树内 **144** 文件／`9C16E34E6C56D8BC…`）：一键包 **r21–r29 九代**（r21 `C74A790C…`/1,615,423 → r29 现役 `099A4F3A…`/2,278,077）＋ `distribution-ledger-r22…r29.json` **八代台账**＋ `release-record`×6（r24–r29）/`release-acceptance-record`×1（r23-d112）**七件**＋ `发送给同事-r21…r29` **九代教程载体**＋ `SEALED-r6.md`／`ARCHIVED-README.md`——同受保护，禁止覆盖/重打包/删除。
  - 安装位 `{BAA5628C…}`（现役 **239 件生效**：`59F376555025…`/1,103,697，内层 43 条目；同目录另有 `.pre-5.5.4-185110.bak` `7CEB2AB715F18CB1`/185,110；目录 mtime 2026-10-01 15:32:09）——真实安装属用户单独授权（§6），卸载仅限本项目插件且可复原（G-195 A-04）。
  - `%TEMP%` 不落任何项目产物（G-197 红线：一切在 D 盘）。

## 6. 需用户单独授权的动作（派工单默认不包含）

真实安装 · 真实客户端配置写入 · GIS 写入 · 依赖/软件安装 · 签名与公开发布 · 干净机部署。

## 7. 已知未验证项（NOT VERIFIED，不得升格）

- `MyProject1.aprx` 的绝对路径与内容状态 —— 旧仓库内不存在，位置未知。
- 全部真实 ArcGIS Pro 运行证据（本轮未启动 Pro/Bridge/MCP）。
- WorkBuddy 接入 —— **PLANNED / NOT VERIFIED**，见 `Docs/phases/PHASE_08/05_WORKBUDDY_PREFLIGHT_PLAN.md`。

## 8. 构建与测试纪律（本 Harness 强制）

> 依据 `Docs/_gatekeeper/BUILD_ENV_FIX.md` 与 `Docs/EVOLUTION_DECISIONS.md` C-15。
> **不遵守会重现"环境阻断"误判**（曾导致 D-003 首次交付被 CHANGES REQUESTED）。

1. **执行任何 `dotnet` 命令前，必须先补齐 Windows 系统环境变量**——本 Harness 的 Bash shell 会剥离它们，
   导致 NuGet 抛 `ArgumentNullException('path1')`（`NuGet.targets(745,5)`）：

   ```bash
   export APPDATA="$USERPROFILE/AppData/Roaming"
   export LOCALAPPDATA="$USERPROFILE/AppData/Local"
   export ProgramData="C:\\ProgramData"
   export ALLUSERSPROFILE="C:\\ProgramData"
   export ProgramFiles="C:\\Program Files"
   export CommonProgramFiles="C:\\Program Files\\Common Files"
   export SystemRoot="C:\\Windows"
   export windir="C:\\Windows"
   export PUBLIC="C:\\Users\\Public"
   export USERPROFILE="$USERPROFILE"
   ```

2. **构建前先 `dotnet build-server shutdown`**，避免残留 MSBuild 进程持有 `obj/.../refint/*.dll` 导致 `CS2012 Access denied`。
3. **沙箱可能拒绝写入既有构建产物**（如 `coverlet` 映射文件）→ 构建需在沙箱外执行。
4. 标准命令序列：

   ```bash
   dotnet restore ArcGIS-Pro-MCP.sln
   dotnet build   ArcGIS-Pro-MCP.sln -c Debug --no-restore     # 期望 0 警告 / 0 错误
   dotnet test    Tests/UnitTests/ArcGISProMCP.UnitTests.csproj       --no-build --no-restore
   dotnet test    Tests/IntegrationTests/ArcGISProMCP.IntegrationTests.csproj --no-build --no-restore
   dotnet test    Tests/ArcGISProMCP.ServerTests/ArcGISProMCP.ServerTests.csproj --no-build --no-restore
   ```
   注意 `Compatibility` 项目 `Platforms=x64`；`UnitTests` 会从
   `Source/ArcGISProMCP.Compatibility/bin` **加载真实程序集**，故必须先构建该插件项目。
5. **已知非缺陷失败**：UnitTests 有 43 项因缺可选 7.x 脚本宿主与打包产物而失败
   （`BLOCKED_BY_ENVIRONMENT`），另 2 项因白名单排除 `Distribution/`、`.runtime/` 而失败
   （`BLOCKED_BY_WHITELIST`）——详见 `EVOLUTION_DECISIONS.md` C-16。

## 9. 目录约定

```
AGENTS.md                     本文件
migration-manifest.json       D-001 迁移逐文件 SHA256 清单
tools/migration/migrate.py    迁移脚本（可复跑，幂等跳过已存在文件）
Docs/EVOLUTION_STATE.md       演进状态（新，覆盖旧 CURRENT_TASK 的后续顺序）
Docs/EVOLUTION_CURRENT_TASK.md
Docs/EVOLUTION_DECISIONS.md   ADR 与冲突登记
Docs/phases/PHASE_08/         Phase 8 交付（迁移报告/差距矩阵/backlog/8.1设计/WorkBuddy预检）
Docs/_gatekeeper/             与总指挥·Gate Keeper 的文件信箱
```
