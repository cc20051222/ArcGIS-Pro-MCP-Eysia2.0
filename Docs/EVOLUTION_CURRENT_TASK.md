# EVOLUTION_CURRENT_TASK.md — 当前任务（新基线）

> 覆盖旧 `Docs/CURRENT_TASK.md` 的后续工作顺序。**历史时点：2026-09-11（D-005·D-006 批次）——下表与下文要点为该时点记录。**
> **2026-09-11 章程 V2.1 生效**：P0 = WorkBuddy 连接验收；**D-004 → DEFERRED**（非取消，8.5.3 通过后回溯）。
>
> **末次更新：2026-10-06（D-126 项目完结批，G-306 派・G-307 扩批）。**
> **当前有效派单：D-126**（项目完结批——基线文档刷新 ＋ 完结报告 ＋ 三账封版草案）；本批之后无预登记批次。
> **基线现值（D-126 逐项现场复算；权威口径以 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md` 为准）**：
> 生产工具 **239**・源码聚合 `1750FED84B9282D19…`/277・注册 239 ≡ 契约 239・装机 `59F376555025…`/1,103,697・
> 发布面 r25 `7969C220…`/2,270,984・测试基线 **1,843 ＝ 1,840 通过／0 失败／3 NE**・五锚 rc=0×5（四锚并集 48／五锚并集 56）。
> ★ 本文「当前有效派工」表内的 D-005／D-006／D-004 状态、以及 WorkBuddy／NOT VERIFIED 段落，均为 **2026-09-11 时点事实**；
> 按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源**——「当前」二字请以上述末次更新为准。

## 当前有效派工（V2.1 批次）

| 单号 | 内容 | 状态 |
|---|---|---|
| **D-005** | WorkBuddy 最小基线 + 8.5.1 能力预检（只读）+ 8.5.2 配置只读方案 | **DELIVERED**（`outbox/R-D005_…md`，PASS CANDIDATE） |
| **D-006** | P2 实机取证 + 8.5.3 证据模板物化 + 8.5.2 校验补充 | **DELIVERED**（`outbox/R-D006_…md`，PASS CANDIDATE） |
| D-004 | Phase 8.1 真实验证（A1–A20） | **DEFERRED**（§4 测试工程已完成；§5 待 8.5.3 通过后回溯） |

## D-005/D-006 交付要点

- **P2（Go/No-Go）= LOCAL（Go）**：11 个本机 WorkBuddy 进程 + 自有 loopback 监听 + 无容器/虚拟网卡 +
  安装目录自带本地 CLI/沙箱运行时 + 执行体（Agent 自身）直接读写本机文件系统。
  判定矩阵与可复现脚本：`.runtime/evolution/phase08/run-20260911-d005d006-workbuddy-preflight/`。
- **WorkBuddy 版本 = 5.5.4**（≥4.22.15/5.0.0 字段门控全部满足）。
- **候选配置字段论证合规**（C-03 部分结案）；项目级与用户级 `mcp.json` 当前均**不存在**（Apply 属新建）。
- **8.5.3 三份模板已物化**：`Docs/_gatekeeper/TEMPLATES/`（证据登记表 / 只读工具选例 / 字段校验表）。

## 遗留 NOT VERIFIED（不阻断验收，但不得升格 PASS）

| 编号 | 项 | 定论途径 |
|---|---|---|
| W2 | loopback `http://` 是否豁免 HTTPS | 8.5.2 Apply 实测 / 8.5.3 |
| W3 | `/mcp` 后缀是否必需 | 8.5.3（带/不带各测一次） |
| W4 | 配置生效方式（自动生效 vs 重启）、重连行为 | 8.5.3 / 8.5.7 |
| W5 | 受限 Skill 支持形态 | 8.5.5 |
| W6 | 实际可暴露工具数 | 8.5.3 `tools/list`（1.0.2 预期 30） |

## 需用户操作 / 授权（按顺序）

1. **【已完成】Apply（用户于 2026-09-11 00:39 明确授权）**：
   已写入项目级 `D:\ArcGIS-Pro-MCP 2.0\.workbuddy\mcp.json`
   （162 B，SHA256 `e0a2139f…db38`；写入前目标不存在 → 新建，无覆盖；原子写 + 回读 roundtrip 校验一致）。
   内容 = 候选配置（`type: streamableHttp` / `url: http://127.0.0.1:6520/mcp` / `timeout: 30000`）。
   ⚠ **W2/W3 仍 NOT VERIFIED**：写入成功 ≠ 连接成功，须由 WorkBuddy 实际连接（状态灯 🟢/🔴 或 8.5.3 调用）定论。
2. **【操作】** 启动 ArcGIS Pro 3.5 → 打开 `TestFixtures\Phase8_1\Phase8_1_MapDiscovery.aprx`
   （**不要**开 `MyProject1.aprx`）→ 确认活动视图为含 6 图层的 `MD_Active` → Start MCP（`127.0.0.1:6520/mcp`）→ 通知执行会话。
3. 就绪后执行 **8.5.3 连接验收**（照填三份模板；选例优先 `get_project_info` + `python_runtime_info`，
   避开 C-05/C-06 路径；工具数按当次 manifest 核对，1.0.2 = 30）。

## 前序单结论

| 单号 | 内容 | 结论 |
|---|---|---|
| D-001 | 迁移准备 Gate + Phase 8.1 设计 | **PASS** |
| D-002 | C-ID 统一 + SDK Spike S1–S5 + C-11 补复制 | **PASS** |
| D-003 rev2 | Phase 8.1 实现 + 隔离测试 | **PASS**（Build 0/0；8.1 用例 13/13；Integration 23/23；Server 41/41；注册表 30；Shared 无 SDK） |
| D-004 | 测试工程 + 真实验证 | §4 完成 ／ §5 **DEFERRED**（V2.1） |

## 遗留（已裁定，非阻断）

- 白名单 2 项 Unit 失败 → `BLOCKED_BY_WHITELIST`（G-08/G-09）。
- 打包产物类 16 项 Unit 失败 → `BLOCKED_BY_ENVIRONMENT`（G-11，Phase 13/14 范围）。
- CR-4 能力感知跳过 → 方向已批准（G-10），随 8.6 实施并修正 `USER_GUIDE.md` 表述。
