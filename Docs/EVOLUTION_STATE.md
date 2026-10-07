# EVOLUTION_STATE.md — 演进状态（新基线）

> 本文件是 **Phase 8 及以后**的状态入口。旧 `Docs/PROJECT_STATE.md`、`Docs/CURRENT_TASK.md`
> 保留为**历史来源**，其"下一步"不构成当前授权。
> 最后更新：2026-09-10，派工单 D-001 ——**该行是 2026-09-10 时点的历史记录**。
>
> **末次更新：2026-10-06（D-126 项目完结批，G-306 派・G-307 扩批）。**
> **当前有效派单：D-126**——本工作空间演进主线随本批封版；其后无预登记批次。
> **基线现值（D-126 逐项现场复算；权威口径以 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md` 为准）**：
> 生产工具 **239**・源码聚合 `1750FED84B9282D19…`/277・注册 239 ≡ 契约 239・装机 `59F376555025…`/1,103,697・
> 发布面 r25 `7969C220…`/2,270,984・测试基线 **1,843 ＝ 1,840 通过／0 失败／3 NE**・五锚 rc=0×5（四锚并集 48／五锚并集 56）。
> ★ 下文 §1 当前阶段、§2 基线表与 §3 进度表（含「30 工具／Native 21・GP 4・Python 5／1.0.2 插件 `361FC84F…`／r5 包 `3924F9B3…`」）
> 为 **2026-09-10 时点的当时事实**，按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不作现行基线**。

## 1. 当前阶段

**迁移准备 Gate（D-001）** —— 迁移已完成，Phase 8.1 设计已交付。状态：**PASS CANDIDATE**，等待独立验收。

## 2. 基线事实（实测，非文档转述）

| 项 | 值 | 证据 |
|---|---|---|
| 新工作空间 | `D:\ArcGIS-Pro-MCP 2.0` | 开工前 `ls` 确认存在且为空 |
| 旧仓库 Git | 0 提交 / 0 跟踪 | `git log` → fatal: no commits yet；`git ls-files` → 0 |
| 迁移文件数 | 327 | `migration-manifest.json` |
| 迁移总字节 | 3,623,935 | 同上 |
| 哈希一致 | 327/327 | 复制前后 SHA256 逐项相等 |
| 冲突覆盖 / 链接逃逸 | 0 / 0 | 脚本 `status` 字段 |
| 生产工具数 | 30 | `Composition.BuildRegistry()` 源码计数 |
| 通道分布 | Native 21 / GP 4 / Python 5 | 源码 `ExecutionTypeName` + 服务划分 |
| 1.0.2 插件 SHA256 | `361FC84F…436A2A` | `sha256sum` 实测，与派工单一致 |
| r5 ZIP SHA256 | `3924F9B3…29AB652` | `sha256sum` 实测，与派工单一致 |
| Phase4Test.gdb | 104 文件 / 0 锁 | `find` 实测 |

## 3. 阶段进度

| 阶段 | 内容 | 状态 |
|---|---|---|
| 迁移准备 Gate | 白名单复制 + manifest + SHA256 核验 | **PASS**（D-001，2026-09-10 独立验收） |
| 文档一致性 + SDK Spike + 基线二进制补复制 | C-ID 统一 / S1–S5 ADR / C-11 | **PASS**（D-002，2026-09-10 独立验收） |
| Phase 8.1 | Map Discovery 设计与实现 | 设计 = 已接受；**实现 + 隔离测试 = PASS**（D-003 rev2）；**真实验证 = BLOCKED**（D-004：§4 测试工程已建成，§5 A1–A20 待用户启动 ArcGIS Pro） |
| Phase 8.2 | 数据集/栅格完善 | PLANNED |
| Phase 8.3 | 选择契约 V2 | PLANNED |
| Phase 8.4 | HTTP 取消完善 | PLANNED |
| Phase 8.5 | WorkBuddy 接入 | **8.5.3 连接 Gate = 全表 PASS（2026-09-11，双通道证据）**：Apply 双轨完成（项目级 00:39 用户授权 + 用户级 08:16 授权，均 SHA256 `e0a2139f…` 核验）；服务侧 6/6 PASS（R-W7 + B1 第 1–6 行）；**客户端层 3/3 PASS**（R-W8：新会话 ToolSearch 见 30 工具、`mcp__arcgis-pro-mcp__ping`→pong、get_project_info 逐字匹配 fixture）；**W2/W3/W4/W6 全部定论通过**（W5 受限 Skill 留 8.5.5）；状态 = **PASS CANDIDATE**，待 Gate Keeper 独立验收 |
| Phase 8.6 | 30 工具整体回归 | PLANNED |
| Phase 8.7 | Phase 8 阶段验收 | PLANNED |
| Phase 9–12 | 扩展至 112+ | PLANNED（每批 3–5 工具，逐批评审） |
| Phase 13 | 生产加固 / 功能冻结 | PLANNED |
| Phase 14 | 一键安装 / 干净机 / 最终验收 | PLANNED（**功能冻结后才允许**） |

## 4. 关键限制（不得升格为 PASS）

1. **WorkBuddy 接入（V2.1 P0）= 8.5.3 双通道 PASS（待独立验收）**。W2（loopback `http://` 客户端接受）、
   W3（`/mcp` 后缀有效）、W4（新配置需新会话生效，不热加载）、W6（工具数 30）均已定论；
   仍 NOT VERIFIED：**W5（受限 Skill，留 8.5.5）**；W4 的"重启后自动重连"部分留 8.5.7。
   连接对象 = 已安装 1.0.2 服务（**不含** D-003 的 8.1 修复，连接验收不代表 8.1 已验证）。
   端口可达/配置存在 ≠ WorkBuddy 真实调用成功（V2.1 R6）。
2. `MyProject1.aprx` 在旧仓库内不存在，位置未知 → 保护性 post-check 该项无法覆盖。
3. 本轮**未启动** ArcGIS Pro / Python Bridge / MCP Server，无任何 fresh 真实运行证据。
4. r5 仅"实现 + 隔离验证"范围通过；干净机、真实客户端、公开发布仍 NOT VERIFIED。
5. 30 工具中已知 PARTIAL/NOT VERIFIED 项见 `Docs/phases/PHASE_08/02_TOOL_GAP_MATRIX.md`。
6. **C-15（已更正/CLOSED）**：构建失败的真实根因是**沙箱 shell 剥离了 Windows 系统环境变量**
   （非"NuGet 不可用"）。执行 `dotnet` 前须补齐 `Docs/_gatekeeper/BUILD_ENV_FIX.md` 所列变量；
   补齐后 `restore` 12/12、`build` 0 错误/0 警告。
7. **C-16（待裁定）**：UnitTests 全量 47 项失败已精确定性为
   **环境 43 项**（29 缺可选 7.x 宿主 + 14 缺打包产物）+ **白名单 2 项**
   （`Distribution/`、`.runtime/` 未迁移导致文档策略用例失败）；**0 项**与 D-003 改动相关。
8. **环境纪律（后续所有派工必须遵守）**：本 Harness 执行任何 `dotnet` 命令前先补齐环境变量，
   否则会重现"环境阻断"误判。构建前建议先 `dotnet build-server shutdown` 以避免 `CS2012` 文件占用。

## 5. 下一步（2026-09-11 08:41 更新：8.5.3 双通道 PASS，待独立验收）

1. ~~Apply~~ → **已完成**：项目级（00:39 用户授权）+ 用户级（08:16 用户授权）双轨写入并核验
   （162/177 B；UI 回写追加 `disabled:false` 即启用态）。
2. ~~8.5.3 连接验收~~ → **双通道全表 PASS（PASS CANDIDATE）**：
   服务侧 = `outbox/R-W7_…md` + `.runtime/…/B1_EVIDENCE_SERVICE_SIDE.md`（6/6）；
   客户端侧 = `outbox/R-W8_…md`（30 工具 / pong / get_project_info 逐字匹配 / python_runtime_info）。
   **W2/W3/W4/W6 定论通过**；W5 留 8.5.5。
3. **【待 Gate Keeper】** 独立复核 R-W7 / R-W8，裁决 8.5.3 是否升格 PASS。
4. **【环境已就绪，可立即回溯】D-004 §5 的 A1–A20**：Pro 3.5 运行中、fixture 已打开、MCP 已启动、
   WorkBuddy 通道已通——A1–A20 可在 8.5.3 获批后直接执行（或由 Gate Keeper 并行批准）。
5. 之后：8.5.4 只读验收（11 个只读工具）→ 8.5.5 工具选择与受限 Skill（W5）→ Phase 8.2 → 8.6 整体回归。
