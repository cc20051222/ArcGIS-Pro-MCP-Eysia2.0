# ArcGIS-Pro-MCP — 项目长期状态（PROJECT_STATE）

> **历史时点声明（D-126 项目完结批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **2026-09-10（文内最晚 ISO 日期实测；正文跨度 2026-09-02…2026-09-10）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。

> 本文件是 AI 读取项目的**第一入口**。新会话请先读取本文件、PROJECT_RULES.md、CURRENT_TASK.md。
> 不依赖聊天历史。若与实际代码冲突，以实际代码与测试结果为准并更新本文件。

---

## 1. 项目目标

开发一个运行于 ArcGIS Pro 内部的 MCP Add-in，使支持 MCP 的 AI 客户端通过自然语言控制 ArcGIS Pro，
调用 GIS 工具完成任务（数据处理 / 空间分析 / 制图 / 栅格 / 属性 / 数据管理等）。

```
AI Client → MCP → ArcGIS Pro MCP Add-in → Tool Router → GIS Tools → ArcGIS Host → ArcGIS Pro SDK/Geoprocessing/ArcPy → GIS结果
```

## 2. 环境基线（真实）

| 项 | 值 |
|----|----|
| OS | Windows 11 Professional 64-bit |
| ArcGIS Pro | 3.5.0 (Build 57366, x64, 简体中文) |
| ArcGIS Pro SDK | 3.5 (程序集 13.5.0.57366) |
| .NET SDK | 8.0.424（当前实际路径：`C:\Program Files\dotnet`） |
| Python | 3.11.11（arcgispro-py3，Pro 自带） |
| ArcPy | 3.5 / 57366 |
| Visual Studio | Community 2026 |
| 工作区 | `D:\ArcGIS-Pro-MCP` |
| Add-in 打包 | `scripts/package-addin.ps1`（官方 targets 因 CodeTaskFactory 在 dotnet build 下的限制） |

## 3. 当前 Phase

### Current Phase 7 Final Project Acceptance（2026-09-07）

Independent Gate Keeper 已正式接受 Phase 7 overall、1.0.2 planned release scope 与 implemented canonical 30-tool whole-project scope 为 **FORMALLY ACCEPTED / PASS / COMPLETE**。本节只记录正式决定与 preservation boundary；不重跑 Build/Test，不启动 Codex、Cursor、DeepSeek、ArcGIS Pro、Bridge、MCP、测试或 RegisterAddIn，不修改配置、production registry、GIS 数据、retained fixture、installed package、历史输出或锁。

- Phase 7.7 overall = **FORMALLY ACCEPTED / PASS**；Phase 7.8 = **FORMALLY ACCEPTED / PASS**；Phase 7 overall = **FORMALLY ACCEPTED / PASS / COMPLETE**；1.0.2 planned release scope / implemented canonical 30-tool whole-project scope = **FORMALLY ACCEPTED / PASS / COMPLETE**。未来 `112+` expansion = **NOT IMPLEMENTED / NOT ACCEPTED**。
- 当前入口：[PHASE_07_FINAL_PROJECT_ACCEPTANCE.md](phases/PHASE_07_FINAL_PROJECT_ACCEPTANCE.md)。不可变 pre-acceptance candidate：[PHASE_07_FINAL_PROJECT_ACCEPTANCE_CANDIDATE.md](phases/PHASE_07_FINAL_PROJECT_ACCEPTANCE_CANDIDATE.md)。Phase 7.8 acceptance：[PHASE_07_8_FINAL_HANDOFF_CANDIDATE.md](phases/PHASE_07_8_FINAL_HANDOFF_CANDIDATE.md)。正式记录：历史归档（该 JSON 不在当前工作树，入口以其替代件为准）。发布说明：[RELEASE_NOTES_1.0.2.md](RELEASE_NOTES_1.0.2.md)。
- Final release identity：installed `1.0.2`，269548 bytes，SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`；canonical production tools=`30` distinct，`mcp_auth` excluded；Claude Desktop remains optional/template-only。
- 7.7 overall 与 7.8 acceptance basis、27 项 handoff manifest、current ledger `5/5 PASS`、historical rollback semantics `7/7 PASS`、protected asset/process/listener snapshot 和所有 limitations 已在专项报告及最终项目候选报告中交叉索引。
- raw envelope、PARTIAL/LIMITED、`NOT VERIFIED`、`BLOCKED_BY_HARNESS`、clean-machine limitation、`ArcGISProject.isDirty`、HTTP cancellation、DeepSeek 当前 not-running/reconnection-not-verified 和 `select_layer` no-OID limitation 原样保留。已接受范围不再有待决 Phase；未来 `112+` expansion 不属于本次接受范围。
- Final read-only state audit (`2026-09-07T22:54:53.5312216+08:00`) distinguishes the accepted historical DeepSeek identity `PID 20424 / node / port 3080` from the current external state: PID `20424` is absent and port `3080` is clear. Cause/actor is **NOT VERIFIED / external user-owned state change**. The launcher shortcut and five profile/config file hashes match their accepted snapshots; no credentials were read or output, and no restart or fresh connection was performed.

### Separate One-click Deployment Share Package（2026-09-10）

在 Phase 7 formal acceptance 之后，新增的一键部署分享包是独立的 deployment workstream，不是 Phase 7 重开，也不是新的 Phase。它复用已接受的 `1.0.2` Add-in artifact；Independent Gate Keeper 已对 r5 的实现与本机隔离自动化验证作出有限放行，当前状态为 **INDEPENDENT GATE PASSED / AUTHORIZED FOR USER REAL-MACHINE TRIAL**。这不等于真实安装、真实 AI 客户端连接、clean-machine acceptance 或公开发布通过。

- Current candidate package：`Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip`；size=`331679` bytes；SHA-256=`3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652`；sidecar 为同名 `.sha256`。上一版 r4 candidate 保留不变：`330602` bytes / SHA-256=`AE9C5EF2421D4E73F8BA62AEA5FBFEF2B4EFF0DA7D2EF24B01EED93D3103A134`；r3 candidate 保留不变：`329568` bytes / SHA-256=`7EEC03C0E0DAD1F3363147FE614B72C0CB41DB3A80262CC76453F4AAC8B1E3EA`；Rejected r1 remains unchanged at `316207` bytes / SHA-256=`3303E02CFAC50EB02B74F4C0B4BDA77C3BE2788607920EE1402B43B19102D7B1`。
- Revision lineage is explicit：原始 r2 Gate review 证据记录 hash=`CC1CC37C8B00918313203E45656183A9FE6656D0E9D1FC664D096349D45AFD53`；其后同名 r2 文件曾被不同内容覆盖为 `329024` bytes / hash=`33AC18A10E2DF4EA7D0D0BE646C777BC2EB535D8C552F1EA846B75956936DABF`。r3、r4 与 r5 均使用独立文件名和独立 evidence root，历史候选不被改写或冒充当前 PASS。
- Reused payload identity 未改变：`1.0.2`、`269548` bytes、SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`；Add-in ID=`{BAA5628C-3C08-4AD5-A6C0-915ADA475709}`；endpoint=`http://127.0.0.1:6520/mcp`；canonical production tools=`30`，`mcp_auth` excluded。
- r1 Gate Keeper rejection is preserved as historical evidence: repeated GUI starts left the window at `执行中`, the target was empty, and PowerShell emitted lifecycle/worker/null-array errors. r1 was not accepted; no historical result is reused as a current PASS。
- r5 carries forward the r4/r3 typed .NET worker bridge, unified UI state/main-thread polling, safe child-result parsing, captured GUI target scope, complete InstallRoot + Add-in GUID display, child-process UTF-8 round-trip and recovery-index entry. It additionally makes the package/test PowerShell entry PS5.1-safe with UTF-8 BOMs, replaces PS5.1-incompatible hash calls in the package client configurator and test harness with .NET SHA-256, and uses ProcessStartInfo for synchronous child exit-code capture. The test harness now launches target scripts through an explicit UTF-8 encoded PowerShell bootstrap, explicitly decodes parent stdout/stderr as UTF-8, and dot-sources the target to preserve script scope; failure runs snapshot complete owned-temp artifacts before cleanup. These are test/evidence-only changes; the real cancel callback after PluginOnly child completion, recoverable `PENDING` transaction registration, pending-index restart blocking, index+actual-ledger recovery, and atomic recovery-index write-failure evidence remain covered. Production Tools/Registry, transport, Python Bridge lifecycle semantics, client policy and GIS data are unchanged。
- Fresh r5 candidate evidence：one-click isolated harness `122 assertions PASS`；the exact Windows PowerShell 5.1 entry command returned `EXIT_CODE=0` and `PASS`；top-level stdout preserved the Chinese/space GUI paths without U+FFFD replacement characters；actual ZIP and Chinese/space extracted-root audit `PASS`（23 files）；preset GUI smoke explicitly `PASS_SIMULATED_PRESET_ONLY`（`6` screenshots / `5` scenarios，business callbacks `NOT_VERIFIED`）；final extracted-package real read-only GUI button callbacks `PASS_REAL_READ_ONLY_CALLBACKS`（`3` screenshots；完整插件目标含中文 InstallRoot 与 Add-in GUID；`Preflight=PASS`、`Diagnose=WAITING_USER_START`；中文消息原样往返、无替换字符、controls/target/no-dialog/worker exits all PASS）；owned recovery matrix `PASS`，包括 real callback cancellation、parent hard interruption after child ledger/install completion, pending-index restart blocking, index-ledger rollback, fresh transaction allocation, recovery-index write failure and old latest coexistence。fresh solution baseline：Build `0 errors / 3 NU1900`；Unit `233 PASS / 3 SKIP / 0 FAIL`（236 total）；Integration `23/23`；Server `41/41`。registration、real installation、real client mutation、real ArcGIS/MCP runtime 均 `SKIPPED / NOT PERFORMED`；clean-machine 和真实用户 GUI click-through acceptance 仍 `NOT VERIFIED`。失败诊断和完整 owned-temp failure artifacts 均保留为非 PASS 历史证据。
- 实际 retained marker 与 `fixture-manifest.json` 已在记录本候选 identity 前只读读取，二者均为 `P57_B8C6FE8E`；retained fixture 未重建、重命名、移动或修改，accepted semantic health 仅 carry-forward。`TestDate/Phase4Test.gdb` post-check=`104 files / 0 locks`；`MyProject1.aprx` SHA-256=`ECF25A8791219435CE9AC44F7A50B28468074ACDF96C9F55455106FB376BCD14`，未修改；历史 1.0.2 ZIP 未覆盖。
- r5 local evidence root：`.runtime/one-click-deployment-r5/run-20260910-044724-cf90381b4f1c4b6b82d881d25cc7888b`；Independent Gate Keeper fresh evidence root：`.runtime/one-click-deployment-r5/run-20260910-052628-6f6d5d15ae56481092d378c74dd8e856`；fresh Build/Test baseline：`.runtime/one-click-deployment-r5/baseline-20260910-032900`；Windows PowerShell 5.1 entry evidence：`.runtime/one-click-deployment-r5/ps51-entry-validation-20260910-044723-unicode-fix`；PS5.1 package verifier：`.runtime/one-click-deployment-r5/ps51-package-verifier-20260910-032800`；local protected/process/listener post-check=`run-20260910-044724-cf90381b4f1c4b6b82d881d25cc7888b/49-post-check.json`。独立审批记录：[ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md](ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md)。r4 root `.runtime/one-click-deployment-r4/run-20260910-013551-7399ba24eb5740598656e5640761f465`、r3 root `.runtime/one-click-deployment-r3/run-20260910-001827-c85bb9b9e5fc45f283e32f07accc9a19` 与 r2 roots remain historical and distinct；r5 root 保留 numbered command reports、package hash、PS5.1 raw stdout/stderr/metadata、simulated 与 real-button GUI evidence、recovery/cancellation/hard-interruption/index-write-failure reports、post-check 和 prior failed-entry diagnostics；Unicode 修复前的失败 diagnostics 也保留，最近一次失败 run 的完整 owned-temp snapshot 见 `run-20260910-044131-5d0b13e51ab74d15b0fef348813c3f62/failure-artifacts.json`。r5 不再等待 Gate Keeper，当前等待用户实机证据；不得把有限放行改写为整体 formal acceptance，不得开始新的 Phase。

### Historical Phase 7.7.4.4 Multi-client Consolidation（2026-09-07）

Independent Gate Keeper 已正式接受 Phase 7.7 Entry Preflight、7.7.1、7.7.2、7.7.3、7.7.4.1、7.7.4.2 与 7.7.4.3 为 **FORMALLY ACCEPTED / PASS**。Phase 7.7.4.4 Multi-client Consolidation 已按授权完成，当前结论为 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**。

- Phase 7 = **IN PROGRESS**；7.1/7.2/7.3/7.4/7.5/7.5.1/7.5.2/7.5.3/7.5 overall/7.6 Entry Preflight/7.6.1/7.6.2/7.6 overall/7.7 Entry Preflight/7.7.1/7.7.2/7.7.3/7.7.4.1/7.7.4.2/7.7.4.3 = **FORMALLY ACCEPTED / PASS**；7.7.4.4 = **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；7.7.5+ = **NOT STARTED**。
- 当前 production architecture 仍为单一 MCP Add-in → Registry/Router → GIS/GP/PythonBridge 链路；production registry 为 `30`，Python Bridge lifecycle semantics 未改变。固定 endpoint 是 `http://127.0.0.1:6520/mcp`，catalog 为 `Config/client-catalog.json`。
- 本阶段新增中文优先 [`README.md`](../README.md)、[`Docs/USER_GUIDE.md`](USER_GUIDE.md)、[`Docs/RELEASE_WORKFLOW.md`](RELEASE_WORKFLOW.md) 和薄编排器 [`scripts/user-workflow.ps1`](../scripts/user-workflow.ps1)。默认 `Plan`、`Validate` 只读；Package 强制 `-SkipRegistration`；安装/回滚和客户端 mutation 只委托现有脚本，并对显式 LedgerPath 做 allowlisted outcome 汇总。
- Phase 7.6 formal acceptance basis：Build `0 errors / 3 NU1900`；focused workflow/document policy `16/16`；Unit `232 PASS / 3 SKIP / 0 FAIL`（235 total）；Integration `23/23`；Server `41/41`；production registry `30`；package-only `1.0.2` / `20` entries / exact Bridge `15494` bytes / Bridge SHA-256=`40B2A00061EB35EA83A7729E9E56FC020B020C2476E2D60123CA2356B33A6CA7` / package SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`；registration `SKIPPED`。
- Fresh 7.7.1 evidence：Build `0 errors / 3 NU1900`；focused release/workflow/runtime/logging/client/document policy `152 PASS / 3 SKIP / 0 FAIL`（155 total）；Unit `232 PASS / 3 SKIP / 0 FAIL`（235 total）；Integration `23/23`；Server `41/41`；ProductionToolContract `11/11`；registry `30` with `mcp_auth` excluded；package-only exact audit passed and registration `SKIPPED`。详见 [PHASE_07_7_1_FRESH_BUILD_TEST_PACKAGE.md](phases/PHASE_07_7_1_FRESH_BUILD_TEST_PACKAGE.md)。
- clean-machine 和后续 phase acceptance 尚未宣称 PASS；Cursor 与 DeepSeek fresh gate 已由 Independent Gate Keeper 正式接受为 **FORMALLY ACCEPTED / PASS**。7.7.4.1 的 raw initialize/tools-list envelope 与独立 session identity 仍是 Codex CLI 非阻断接口限制；Cursor 与 DeepSeek 的 raw-surface limitation 也按各自报告保留。7.7.4.4 只允许文档/evidence consolidation，不得启动 7.7.5+。
- 7.7.4.4 已只读取三个已完成 fresh client gate 的报告/evidence、配置/资产哈希和进程/端口状态并生成汇总；本轮未重新启动或操作 Codex/Cursor/DeepSeek/ArcGIS runtime，未停止、重启或修改 DeepSeek PID `20424` / port `3080`，未修改任何客户端配置、production registry、MyProject1.aprx、Phase4Test.gdb、retained fixture、历史输出或未知锁，未执行 native GIS mutation。
- 7.7.4.1 approval-remediation 使用 `--approve-for-me`、`--ephemeral` 和仅针对 `arcgis-pro-mcp` 的 `default_tools_approval_mode="approve"`；新 session 暴露 `mcp__arcgis_pro_mcp__`，`ping`、`python_bridge_ping`、`get_arcgis_version` 均 `isError=false`。Gate Keeper 正式接受完整排序 30-name set 为 initialize-equivalent live discovery，raw envelope limitation 保留；Bridge PID 17344 的官方 Python/AssemblyCache 路径由 instrumentation 记录。证据根为 `.runtime/phase77_4_1_codex/run_768234A4148B4B83A33C77344958EEA2`。
- 7.7.2 transaction 已按授权执行并完成 installed package match，且已由 Independent Gate Keeper 正式接受：新 owned root 为 `phase77_2_AB60AFC410CA47719330397DFBFC5012`，ledger `status=PASS`，`RegisterAddIn.exe /s` exitCode=`0`；installed package 为 `269548` bytes，SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`，与 1.0.2 source candidate 完全匹配。ArcGIS/Bridge/client/MCP 未启动，受保护 APRX/GDB/retained fixture/client config 未变化；7.7.4.2 为当前记录阶段。
- 7.7.3 已正式接受：仅使用 retained `Phase58Controlled.aprx`；installed MCP UI Status/Diagnostics、三项 allowlisted export、managed-log placement/privacy、graceful close、官方 AssemblyCache module chain 和 fresh runtime `tools/list=30` 均按证据记录。7.7.4.1 approval-remediation 已取得 fresh client calls、official Bridge child path evidence 和 Gate Keeper formal acceptance；raw envelope/session identity limitation 保留。7.7.4.2 Cursor P1 已由 Independent Gate Keeper 正式接受，具体连接、调用、raw-surface limitation 和 graceful-close evidence 见专项报告。
- 7.7.4.3 DeepSeek P1 fresh evidence 已由 Independent Gate Keeper 正式接受：exact raw exposed tool set=`30`、distinct=`30`、duplicate=`0`，全部映射到 `arcgis-pro-mcp`；同一 fresh session 唯一调用 `mcp__arcgis-pro-mcp__ping({})` 返回 `pong`、`isError=false`。Session ZIP 核对为 exactly one `tool/call` 与 one matching `tool/result`；raw initialize envelope 未由 client surface 暴露。7.7.4.4 并列 consolidation 已完成，当前等待其独立复核，不把 Phase 6 历史证据冒充本次 fresh evidence。

详细记录：[PHASE_07_7_4_4_MULTI_CLIENT_CONSOLIDATION.md](phases/PHASE_07_7_4_4_MULTI_CLIENT_CONSOLIDATION.md)。当前结论为 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**，7.7.5+ 未开始。7.7.4.3 formal acceptance：[PHASE_07_7_4_3_DEEPSEEK_P1_FRESH_CLIENT_GATE.md](phases/PHASE_07_7_4_3_DEEPSEEK_P1_FRESH_CLIENT_GATE.md)。7.7.4.2 acceptance：[PHASE_07_7_4_2_CURSOR_P1_FRESH_CLIENT_GATE.md](phases/PHASE_07_7_4_2_CURSOR_P1_FRESH_CLIENT_GATE.md)。7.7.4.1 acceptance：[PHASE_07_7_4_1_CODEX_P0_FRESH_CLIENT_GATE.md](phases/PHASE_07_7_4_1_CODEX_P0_FRESH_CLIENT_GATE.md)。

### Historical Phase 7.6.1 Release Portability Foundation（2026-09-06）

Independent Gate Keeper 已正式接受 Phase 7.6 Entry Preflight 为 **FORMALLY ACCEPTED / PASS**。当前只执行 Phase 7.6.1 Release Portability Foundation；本阶段状态为 **IN PROGRESS / PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**。

- Phase 7 = **IN PROGRESS**；7.1/7.2/7.3/7.4/7.5/7.5.1/7.5.2/7.5.3 = **FORMALLY ACCEPTED / PASS**；7.6 Entry Preflight = **FORMALLY ACCEPTED / PASS**；7.6.1 = **IN PROGRESS / PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；7.6.2/7.7+ = **NOT STARTED**。
- 7.6.1 仅建立可测试的 RuntimePathResolver、包内 `Install/PythonBridge/bridge_runner.py` artifact/manifest audit、LocalApplicationData runtime-root policy、1.0.2 release candidate identity 和 fail-closed safe codes；没有安装、注册、runtime、client 或 UI acceptance。
- 当前 production architecture 仍是单一 MCP Add-in → Registry/Router → GIS/GP/PythonBridge 链路；production registry 仍为 `30`，Python Bridge lifecycle semantics 未改变。
- Fresh evidence：Build `0 errors / 3 NU1900`；focused `65 PASS / 3 SKIP`（total `68`）；Unit `216 PASS / 3 SKIP`（total `219`）；Integration `23/23`；Server `41/41`；package `20` entries，唯一 Bridge artifact length `15494`，source/package SHA-256=`40B2A00061EB35EA83A7729E9E56FC020B020C2476E2D60123CA2356B33A6CA7`。
- Release token gate：只接受 canonical `major.minor.patch`，adversarial dots/separators/signs/whitespace/oversized values 均在 path construction 前返回 `RUNTIME_RELEASE_VERSION_INVALID`；成功的 `RuntimeRoot` 同时被验证为 `LocalApplicationData/ArcGISProMCP` 与 `LocalApplicationData/ArcGISProMCP/1.0.2` 的 descendant。
- source candidate release=`1.0.2`；protected installed copy remains `1.0.1` / `202613` bytes / SHA-256=`62FBE89CAE9F03077C797A146662AFC81ED29E59539327D85FBC190A179B8D65`；该 source/installed difference 是 `EXPECTED_PENDING_PHASE_7_7`，不是本轮安装结果。
- protected-state read-only post-check unchanged：`MyProject1.aprx`、shared `Phase4Test.gdb` `104/0 locks`、retained `P57_B8C6FE8E` fixture、historical outputs、unknown locks、client configs 和 legacy log timestamps；ArcGIS Pro/Python/testhost/RegisterAddIn 均为 `0`，6511/6520 无 listener。
- 详细报告：[PHASE_07_6_1_RELEASE_PORTABILITY_FOUNDATION.md](phases/PHASE_07_6_1_RELEASE_PORTABILITY_FOUNDATION.md)。

### Historical Phase 7.6 User Documentation and One-click Materials — Entry Preflight（2026-09-06）

Independent Gate Keeper 已正式接受 Phase 7.5.3 及 Phase 7.5 overall 为 **FORMALLY ACCEPTED / PASS**（限授权实现与自动化证据范围）。当前只执行 Phase 7.6 User Documentation and One-click Materials 的 Entry Preflight / audit-design；不实施脚本、不把用户文档改写成已完成行为、不打包/安装/注册、不启动 ArcGIS/Bridge/client，也不调用 MCP。

- Phase 7 = **IN PROGRESS**；7.1/7.2/7.3/7.4 = **FORMALLY ACCEPTED / PASS**；7.5/7.5.1/7.5.2/7.5.3 = **FORMALLY ACCEPTED / PASS**；7.6 Entry Preflight = **IN PROGRESS / PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；7.7+ = **NOT STARTED**。
- 正式支持仅覆盖 ArcGIS Pro 3.5/current host；其他版本不作推断支持。

7.5.3 不新增 MCP tool、不修改 transport/endpoint/Start/Stop/Python Bridge lifecycle、不写客户端配置、不读取 GIS/project/workspace/config secrets，不启动 ArcGIS/Bridge/client。历史 `.runtime` 日志只读保护；managed sink 仅使用新的 owned `.runtime/managed-logs` root。UI runtime 与 installed production managed-sink/export observation 仍为 `NOT VERIFIED`，转交 Phase 7.7。

7.5.1/7.5.2 的历史 candidate、Build/Test 和 acceptance evidence 保留在各自 phase reports。7.5.3 正式报告：[PHASE_07_5_3_STRUCTURED_LOGGING_DIAGNOSTIC_EXPORT.md](phases/PHASE_07_5_3_STRUCTURED_LOGGING_DIAGNOSTIC_EXPORT.md)。7.6 preflight：[PHASE_07_6_USER_DOCUMENTATION_PREFLIGHT.md](phases/PHASE_07_6_USER_DOCUMENTATION_PREFLIGHT.md)。

7.5.3 已由 Independent Gate Keeper 正式接受：Composition 只创建一个新的 `.runtime/managed-logs` `ManagedFileLogger`；生产调用点经 privacy-safe normalizer 路由到该 sink；SelfTest 不再写 `.runtime/selftest.log`/`.runtime/selftest.marker` 或 `result.Data`；Shared Core diagnostic export 只允许 `health-snapshot.json`、`managed-log.jsonl`、`manifest.json`，并使用 owned temporary sibling + atomic publish。UI runtime 观察保持 `NOT VERIFIED`，因为该阶段禁止启动 Pro/add-in runtime。

本轮 fresh evidence：solution Build `0 errors / 3 NU1900`；Phase 7.5.3 focused `15 total = 13 PASS + 2 capability-aware SKIPPED`；Unit `188/188 PASS + 2 SKIPPED (190 total)`；Integration `23/23`；Server `41/41`；production registry `30` 未改变。两项 reparse-point/ancestor tests 因当前环境创建 directory symbolic link 返回 `IOException` 而 skipped，未宣称 reparse behavior executed PASS。受保护 APRX、shared/retained GDB、installed package、repository-scoped client config、legacy logs 和监听状态只读 post-check 均保持不变。详见正式接受记录。

Phase 7.6 Entry Preflight 已发现 `Source/ArcGISProMCP.Compatibility/Composition.cs` 将 `RepoRoot`、`PythonBridgeScript`、`PythonWorkingDirectory` 和 managed-log root 绑定到 `D:\ArcGIS-Pro-MCP`；当前 19-entry `.esriAddInX` 不包含 `bridge_runner.py`。该事实分类为 `RELEASE_PORTABILITY_BLOCKER`；在解决或明确阻断前，不得宣传 clean-machine support 或 one-click installation。详见 [PHASE_07_6_USER_DOCUMENTATION_PREFLIGHT.md](phases/PHASE_07_6_USER_DOCUMENTATION_PREFLIGHT.md)。

此前使用用户 profile 路径检查客户端配置，属于 **VERIFICATION_PATH_ERROR**，不是配置漂移。正确 repository-scoped post-check 已确认：`D:\ArcGIS-Pro-MCP\.codex\config.toml` SHA-256=`2E4A919F011A0D8F5F175B7F85C0E5236C0923D67BDC632EF331221E17AD55E2`；`D:\ArcGIS-Pro-MCP\.cursor\mcp.json` SHA-256=`C5CF47ED4759FDBF391838C112971BAB5C6A3F6BD135F846675AD708C5AD135B`；DeepSeek hash=`0BC84530A43B06A7FFE789F7C0369F3B973A264F9EA2A8D84B41206683C7ED4B`。无相关 sidecar，未修改真实配置。

### Phase 7.4 Independent Gate Keeper Formal Acceptance（2026-09-06）

- Independent Gate Keeper 正式裁定：**FORMALLY ACCEPT — PHASE 7.4 PASS**。
- 接受依据：Build `0 errors / 3 NU1900`；focused client policy `18/18`；full Unit `108/108`；Integration `20/20`；Server `41/41`；真实 Codex/Cursor/DeepSeek 配置只读 Validate PASS 且哈希保持不变；无真实配置 `.tmp/.bak/meta` sidecar。
- 受保护状态：`MyProject1.aprx` SHA-256=`ECF25A8791219435CE9AC44F7A50B28468074ACDF96C9F55455106FB376BCD14`；shared `Phase4Test.gdb`=`104 files / 0 locks`；实际安装目标 Add-in ID=`{BAA5628C-3C08-4AD5-A6C0-915ADA475709}`；注册包基线 SHA-256=`62FBE89CAE9F03077C797A146662AFC81ED29E59539327D85FBC190A179B8D65`。历史 registered-package backup prefix `7CEB2A...` 按 Gate Keeper 提供的截断值保留；`A97AE2...` 保持为另一历史 Add-in 包哈希/证据，不作为安装 GUID。
- Phase 7.4 的历史 `PASS CANDIDATE`、18/18 前置记录、配置安全边界和未执行真实 Apply/Restore 记录均保留；详细报告见 [PHASE_07_4_MULTI_CLIENT_HANDOFF.md](phases/PHASE_07_4_MULTI_CLIENT_HANDOFF.md)。

### Historical Phase 7.1 boundary（2026-09-05）

Phase 6 = **FORMALLY ACCEPTED / PASS / COMPLETE**。Phase 7 = **IN PROGRESS**；Phase 7.0 Entry Preflight = **PASS**；Phase 7.1 Package and Release Identity = **FORMALLY ACCEPTED / PASS**；Phase 7.2 Install, Uninstall and Rollback = **FORMALLY ACCEPTED / PASS**；Phase 7.3 = **IN PROGRESS / PASS CANDIDATE pending review**；Phase 7.4+ **NOT STARTED**。

- Release identity policy: release/product version `1.0.1`; first-party CLR AssemblyVersion `1.0.0.0`; FileVersion `1.0.1.0`; ProductVersion/InformationalVersion `1.0.1`; AssemblyVersion intentionally remains `1.0.0.0` for binary compatibility.
- Fresh evidence: Build `0 errors / 3 NU1900`; Unit `72/72`; Integration `20/20`; Server `41/41`。
- Package-only smoke: `202887` bytes, SHA-256 `C5541E428476A14B25C983FF67F549617F64C20EF4651348DE50D4962049CA7E`; independent ZIP audit `19` entries, `8` first-party DLLs, required core files present, nested add-ins `0`。
- Release manifest: schema `arcgis-pro-mcp-release-manifest-v1`; Config.daml/package identity consistent; all 8 first-party DLL source/ZIP hashes and CLR AssemblyVersion/FileVersion/ProductVersion values independently matched；manifest contains no absolute path/user identity。
- Registration was explicitly skipped with `-SkipRegistration`; installed copy remains the carried-forward baseline SHA-256 `62FBE89CAE9F03077C797A146662AFC81ED29E59539327D85FBC190A179B8D65` and was not updated.
- This subphase changed only centralized release metadata, packaging validation/manifest behavior, and tests；production GIS tool/Bridge lifecycle semantics were not changed。

Detailed report: [PHASE_07_1_PACKAGE_RELEASE_IDENTITY.md](phases/PHASE_07_1_PACKAGE_RELEASE_IDENTITY.md)。Current 7.2 report: [PHASE_07_2_INSTALL_UNINSTALL_ROLLBACK.md](phases/PHASE_07_2_INSTALL_UNINSTALL_ROLLBACK.md)。

> 客户端连接事实保持不变：Codex P0、Cursor P1 与 DeepSeek Harness P1 的 MCP connection / initialize-equivalent session / canonical tools-list=30 / safe read-only ping 均已 **FORMALLY ACCEPTED / PASS**。Phase 6 = **FORMALLY ACCEPTED / PASS / COMPLETE**；Phase 7 = **IN PROGRESS**；Phase 7.1 = **FORMALLY ACCEPTED / PASS**；Phase 7.2 = **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**，Phase 7.3+ 尚未开始。

DeepSeek P1 专项报告：[PHASE_06_DEEPSEEK_HARNESS_P1_PREFLIGHT.md](phases/PHASE_06_DEEPSEEK_HARNESS_P1_PREFLIGHT.md)。

### Current DeepSeek MCP gate（2026-09-05）

Independent Gate Keeper formally accepted the DeepSeek Harness identity/launcher/local-runtime preflight as **PASS** and subsequently formally accepted the same-client MCP connection gate as **PASS**. The audited DeepSeek namespace `arcgis-pro-mcp` enumerated the canonical 30 unique server tools and its only call, `ping({})`, returned `pong`; initialize-equivalent status is accepted by same-client discovery/use, without claiming a raw `initialize` envelope. The earlier process/ledger **BLOCKED_BY_USER_UI** state remains historical and is superseded by [PHASE_06_DEEPSEEK_HARNESS_P1_MCP_CONNECTION_GATE.md](phases/PHASE_06_DEEPSEEK_HARNESS_P1_MCP_CONNECTION_GATE.md). Overall reconciliation: [PHASE_06_OVERALL_ACCEPTANCE_AUDIT.md](phases/PHASE_06_OVERALL_ACCEPTANCE_AUDIT.md)。

### Phase 7 entry boundary（historical pre-7.1 snapshot）

The original Phase 7 entry inventory and dependency plan remain available in [PHASE_07_ENTRY_PREFLIGHT.md](phases/PHASE_07_ENTRY_PREFLIGHT.md) and [PHASE_07_IMPLEMENTATION_PLAN.md](phases/PHASE_07_IMPLEMENTATION_PLAN.md)。The current authoritative state is the Phase 7.1 boundary above: **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 7.2+ **NOT STARTED**。

- **Phase 5.7.5 Test Consolidation & Acceptance = PASS（2026-09-03）**；Phase 5.7.1–5.7.4 均 PASS；Phase 5.7 = **PASS**；Phase 5.6.4（Real ArcGIS Pro / ArcPy / MCP E2E Final Acceptance）= **PASS**，Phase 5.6 = **PASS**，真实 ArcGIS Pro runtime tools/list=30。
- **当前执行状态：Phase 6 Entry Preflight = FORMALLY ACCEPTED / PASS；客户端优先级为 Codex P0 首选必验、Cursor P1 必验、DeepSeek Harness P1 必验、Claude Desktop P2 可选；Codex P0、Cursor P1、DeepSeek Harness P1 MCP connection / initialize-equivalent session / canonical tools-list=30 / safe read-only ping 均 FORMALLY ACCEPTED / PASS；Phase 6 = FORMALLY ACCEPTED / PASS / COMPLETE；Phase 7 = IN PROGRESS；Phase 7.1 = PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER；Phase 7.2+ NOT STARTED**。Phase 5.11 与 Phase 5 overall 继续为 FORMALLY ACCEPTED / PASS / COMPLETE。旧的 DeepSeek external-client/runtime blocker 属于历史快照。
- Phase 5.10 fresh baseline：Build 0 errors / 3 NU1900；Unit 68/68、Integration 20/20、Server 41/41（MCPProtocol 9、MCPServer 25、MCPTransport 7）；PythonBridgeLifecycle 16/16 连续 3 次；去重后 129 Facts。历史 Case H / `BLOCKED_BY_HARNESS` 记录保留，本轮当前主机 7/7 transport fresh PASS。
- Phase 5.8 preflight 的完整 scope、30-tool real-evidence matrix、fixture/project policy、GP/mutation/cleanup gates 见 [`PHASE_05_8_REAL_VERIFICATION_PREFLIGHT.md`](phases/PHASE_05_8_REAL_VERIFICATION_PREFLIGHT.md)；5.8.1 fixture attempt 见 [`PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md`](phases/PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md)。
- Phase 5.7.1 已完成生产 30-tool registry snapshot；5.7.2 已完成 Python facade、GP 独立路由/参数、placeholder、错误传播和 MCP error rendering；5.7.3 已完成 JSON-RPC/MCP server edge coverage，并将 7 个 HttpListener 用例以 raw fixed/dynamic/child-process evidence 分类为 `BLOCKED_BY_HARNESS`；5.7.4 已完成 test-owned workspace、命名、cleanup 和 mutation restore policy；5.7.5 已完成 129-test inventory、coverage diagnostic、30-tool final matrix、P0/P1 review 和阶段验收；Unit 68/68、Integration 20/20、Server non-HTTP filters 34/34 PASS。
- 仓库没有完整、独立的 Phase 5.6 implementation scope 文档；已确认的仓库信号是保持单进程 NDJSON Bridge，并在本阶段完善 request timeout / Kill / restart policy。`DECISION-003` 仍为 PROPOSED，其中的 `python_execute`/任意 Python 执行不是当前实现或已确认需求。
- **Phase 5.4 PythonBridgeService & MCP Integration = PASS**（历史真实 MCP E2E；工具 27）。Phase 4 = 正式 PASS（见下）。
- **Phase 5 已完成范围（非整体完成）**：
  - 5.1 架构预检完成（DECISION-003 仍为 PROPOSED；没有独立的最终 PASS 记录）。
  - 5.2 Minimal Bridge（bridge_runner.py 真实 PASS）。
  - 5.3 Process Lifecycle = PASS（PythonBridgeProcessManager）。
  - **5.4 = PASS**：F1 生命周期互斥修复（Send+Dispose 并发 2155ms 无 hang）、F2 Composition 绝对路径；`IPythonBridgeService`+`PythonBridgeService`（Ping/RuntimeInfo/ArcpyExists/Describe→OperationResult）；工具 `python_bridge_ping`/`python_runtime_info`（**合计 27**）；ServiceContainer 单例 + 卸载 dispose；**真实 MCP E2E PASS**（tools/list=27、python_bridge_ping→pong、python_runtime_info→3.11.11/arcpy3.5/arcgis2.4.1、持久化 1 进程/kill 自愈/无泄漏、Phase 4 spot 无回归）；Build `-m:1` 0 errors（NU1900 环境告警）。
  - 5.5.1–5.5.3：**正式 PASS**。已完成 discovery 设计/Bridge action、MCP Tool 实现、Composition 注册和静态 Build；Level-4 MCP Runtime 明确延期至 5.5.4。
  - 5.5.4 final：完成真实 MCP E2E、参数校验、错误隔离/持久化、30-tool runtime inventory 和 Phase 4 spot regression；`dataset_summary.fieldsSummary.oidCount` 最小契约修复经真实 `TestPolygons` 验证为 1。
  - 5.6 preflight：完成范围、架构、安全、超时/取消、恢复、输出、路径和执行面审计；未修改 Phase 5.6 功能代码。
  - 5.6.1：完成并接受 Python Bridge timeout/cancellation/correlation/recovery contract design；`DECISION-005` 已在 5.6.2 Core 验证后更新为 ACCEPTED / IMPLEMENTED。
  - 5.6.2：完成 generation-safe correlation、dispatch tracking、独立 Python timeout、post-dispatch quarantine、bounded kill/cleanup、startup/pending/late-response cleanup 和 Dispose 修复；Core lifecycle tests 9/9 PASS。
  - 5.6.3：完成 stdout response limit、stderr bounded logging/drain、malformed protocol fail-fast、production/test action registry 与默认禁用；路径 allowlist 完成兼容性审计并延期实现。
  - 5.6.4：完成修改包的真实 ArcGIS Pro / ArcPy / MCP E2E 最终验收，Phase 5.6 = PASS。
  - 历史 roadmap 摘要：5.8 overall 已由独立 Gate Keeper 正式接受为 `FORMALLY ACCEPTED / PASS`；5.8.1–5.8.5 均已正式验收。Phase 5.9 也已正式接受为 `FORMALLY ACCEPTED / PASS`；5.7.5 已关闭 Tests gate，但不关闭 Phase 5 整体；该摘要当时仍写作 Phase 5.10–5.11 未开始，现由下方最新状态覆盖。
  - 历史状态覆盖（已由当前 Phase 7 入口状态 supersede）：Independent Gate Keeper 已正式接受 Phase 5.11 PASS 及 Phase 5 overall PASS；Phase 5 overall 现为 COMPLETE；该旧摘要中的 Phase 6 connection pending 与 Phase 7 NOT STARTED 仅为正式复核前历史快照。
  - Automated dotnet test = BLOCKED_BY_HARNESS（本 Harness）。
- **Phase 4（GIS Tool 体系建设）— 正式 PASS（2026-09-02）**：
- 已完成：4.1 架构审查、4.2 Tool Metadata/Contract、4.3–4.6（真实服务：Attribute/Selection/Geoprocessing(+GP Executor)/DataManagement/Raster/Layout/Layer/Project/Map；新增注册工具）、4.12（真实 Pro 核心工具验证）、4.13（Phase 3 回归绿色）。
- **当前工具数：25（基础 6 + Phase 4 新增 19）**；**Build 0/0；测试 57/57**（历史记录）。
- 真实 Pro 验证核心工具（get_current_map/get_layers/get_layer_info/set_layer_visibility/get_project_info/list_databases/clear_selection）；部分工具 NOT VERIFIED（add/remove/select_layer、GP 端到端、feature 数据查询）。
- **Phase 4 Final Verdict = PASS**（含 NOT VERIFIED 明示；112+ 目标为 Planned）。
- **2026-09-02 Phase 4 真实运行验证（连接用户已开 Pro，MCP HTTP 127.0.0.1:6520/mcp）**：MCP 基础、基础 6/6、Layer、Geoprocessing(buffer/clip/intersect/dissolve)、Error Handling 均 **真实 PASS**。发现+修复真实 BUG（Attribute/Selection 对 FeatureLayer 误报 LAYER_NOT_FOUND，缺 `.Layers` 回退），build 0/0。
- **2026-09-02 最终 PASS（干净重启 Pro，MCP HTTP 6520，25 工具包已部署验证）**：tools/list=25；**query_attributes 无 fieldNames = PASS**（真实全字段，不再 Internal error，第 2 修复生效）；回归全 PASS（get_feature_count=4/get_field_info/select_layer/clear_selection/ping/buffer）。**2 处真实 bug 均已修复并部署**：#1 LAYER_NOT_FOUND 回退、#2 query_attributes 几何序列化。**Phase 4 = PASS**（核心+属性+选择+GP+错误处理真实验证；get_dataset_info/get_raster_info 为 filesystem-only 占位 real 解析 NOT VERIFIED；Automated dotnet test 本 Harness BLOCKED）。历史 57/57 = 历史基线。**Phase 4 正式 PASS，等待用户进入 Phase 5**。

## 4. Phase 状态与已完成项

| Phase | 内容 | 状态 |
|-------|------|------|
| Phase 0 | 环境检测（15 项 + 版本矩阵） | PASS |
| Phase 1 | 最小 ArcGIS Pro Add-in（Ribbon/Start/Status/.esriAddInX/RegisterAddIn/真实运行） | PASS |
| Phase 2 | ArcGIS Host + Tool 架构（Services/Registry/Router/Result/Error/License/Version/Logger/Config） | PASS |
| Phase 3 | MCP Server 核心通信（真实 HTTP + ArcGIS Pro 3.5 最终验收通过） | PASS |
| Phase 4 | GIS Tool 体系（真实服务 + 基础工具集 + 测试 + 真实核心验证）＝ PASS(带 NOT VERIFIED) | **PASS** |
| Phase 5.5.1–5.5.3 | Python discovery tools / implementation / static integration | **PASS**（Level 4 deferred） |
| Phase 5.5.4 | Real verification / MCP E2E / regression | **PASS** |
| Phase 5.6 | Architecture / Scope / Preflight / Reliability / Guardrails / Runtime Acceptance | **PASS** |
| Phase 5.6.1 | Timeout / Cancellation / Correlation / Recovery contract design | **DESIGN COMPLETE；implemented through 5.6.2** |
| Phase 5.6.2 | Core lifecycle reliability implementation | **PASS（Core + real ArcGIS Python runtime verified）** |
| Phase 5.6.3 | Output / Protocol / Action / Access Guardrails | **PASS（runtime boundary verified；Path Guardrail deferred）** |
| Phase 5.6.4 | Real ArcGIS Pro / ArcPy / MCP E2E final acceptance | **PASS** |
| Phase 5.7 | Tests（由 PHASE_05 roadmap 定义；本轮由 5.7.5 汇总验收） | **PASS（5.7.1–5.7.5 PASS）** |
| Phase 5.7.1 | Production 30-tool contract snapshot & test foundation | **PASS** |
| Phase 5.7.2 | Python facade / GP / placeholder / error behavior tests | **PASS** |
| Phase 5.7.3 | Server protocol coverage & HTTP harness resolution | **PASS（7 HTTP cases BLOCKED_BY_HARNESS）** |
| Phase 5.7.4 | Test data ownership, mutation safety & repeatable fixtures | **PASS** |
| Phase 5.7.5 | Test consolidation, execution matrix & Phase 5.7 acceptance | **PASS** |
| Phase 5.8 | Real Verification architecture, fixture & scope preflight | **FORMALLY ACCEPTED / PASS** |
| Phase 5.8.1 | Real fixture & controlled project preparation | **PASS；independent Gate Keeper formally accepted** |
| Phase 5.8.2 | Native Real Verification | **PASS**（independent Gate Keeper formally accepted） |
| Phase 5.8.3 | GP / ArcPy Real Verification | **PASS**（independent Gate Keeper formally accepted） |
| Phase 5.8.4–5.8.5 | Mutation & cleanup / evidence consolidation | **5.8.4 PASS；5.8.5 FORMALLY ACCEPTED / PASS** |
| Phase 5.9–5.11 | MCP→ArcPy / Regression / Acceptance（roadmap-defined） | **5.9 FORMALLY ACCEPTED / PASS；5.10 FORMALLY ACCEPTED / PASS；5.11 FORMALLY ACCEPTED / PASS** |
| Phase 5.10 | Regression Verification | **FORMALLY ACCEPTED / PASS** |
| Phase 6 | 客户端连接验证 | **FORMALLY ACCEPTED / PASS / COMPLETE** |
| Phase 7 | 部署与用户体验 / 1.0.2 final acceptance | **Phase 7.0–7.8 gates = FORMALLY ACCEPTED / PASS；Phase 7 overall = FORMALLY ACCEPTED / PASS / COMPLETE；1.0.2 planned release scope / implemented canonical 30-tool whole-project scope = FORMALLY ACCEPTED / PASS / COMPLETE** |

## 5. 架构

严格分层（Shared 层**不引用** ArcGIS Pro SDK）：
```
AI Client → MCP Transport → MCP Protocol → MCP Server → Tool Router → Tool Registry → GIS Tool → IArcGISHost → ArcGIS Services → ArcGIS Pro SDK
```
- MCP Server 不直接操作 ArcGIS SDK；Tool 不绕过 Host。
- ArcGIS SDK 线程规则：Sdk 对象访问一律 `QueuedTask.Run`（MCT）。
- 版本差异集中于 Host 层（Compatibility=Pro3.3–3.6/.NET8；Current=Pro3.7+/.NET10，占位）。

## 6. 项目结构（12 个项目）

```
Source/Shared/
  ArcGISProMCP.Core           # 模型/接口/结果/错误/Tool抽象/Router/Registry/ServiceContainer
  ArcGISProMCP.Protocol       # JSON-RPC/MCP DTO
  ArcGISProMCP.Configuration  # MCPSettings
  ArcGISProMCP.Logging        # ILogger/LogEntry/LogLevel/NullLogger
  ArcGISProMCP.Security       # 安全模型
  ArcGISProMCP.Tools          # ping/get_current_map/get_layers/get_project_info/get_arcgis_version/get_license_info
  ArcGISProMCP.Server         # McpServer/HttpMcpTransport/JsonRpc/McpProtocolHandler（无 ArcGIS SDK）
Source/ArcGISProMCP.Compatibility  # Add-in：Host/Services/Ribbon/Composition/SelfTestRunner
Tests/
  ArcGISProMCP.TestSupport / UnitTests / IntegrationTests / ServerTests
Docs/  scripts/
```

## 7. 已注册工具（Phase 2 真实验证，不得删除）

`ping`, `get_current_map`, `get_layers`, `get_project_info`, `get_arcgis_version`, `get_license_info`
（已存在 / 已注册 / 已单测 / 已在 ArcGIS Pro 3.5 真实运行）

## 8. 端口 / 连接状态

| 项 | 状态 |
|----|------|
| MCP Server (6520) | 当前真实 endpoint 可用；运行实例已加载 30-tool 包 |
| Python Bridge (6511) | 不监听（stdin/stdout NDJSON 设计）|
| DeepSeek Harness / Claude / Cursor | NOT CONNECTED；仓库无 AI/MCP client 实现 |

## 9. 关键决策（Decision 详见 Docs/decisions/）

- 严格分层 + Shared 无 ArcGIS SDK 引用（Rule 5）。
- Add-in 打包用 `scripts/package-addin.ps1`（CodeTaskFactory 限制）。
- Tool 名使用 snake_case（MCP 规范）。
- MCP 传输无状态/无 session（HTTP 请求-响应）。

## 10. 关键测试结果（详见 VERIFICATION.md）

- 最新 Build（`-m:1 --no-restore`）：0 errors，3×NU1900 网络告警；普通 restore 受 NuGet.Config Access Denied 阻塞。
- 最新测试：Unit 68/68、Integration 20/20、Protocol 9/9、MCPServer 25/25 PASS；完整 Server 的 7 项 HTTP case 因既有 Harness `HttpListenerException: 句柄无效` 保持 `BLOCKED_BY_HARNESS / Case H`。coverage diagnostic 已收集，无阈值 gate。历史 57/57 保留为历史基线。

## 11. 兼容性

3.3 / 3.4 / 3.6：NOT TESTED；**3.5：PASS**；3.7+：不作为 3.3–3.6 目标。

## 12. 已知问题（保留，勿删）

1. 官方 Esri.ProApp.SDK.Desktop.targets 的 CodeTaskFactory 在 `dotnet build` 下不支持（MSB4801）。
2. VS 2026 MSBuild 环境缺 .NET 桌面工作负载/SDK 解析器。
3. `GetMapsAsync` 使用 `MapFactory.CreateMapFromItem`，语义持续关注。
4. ArcGIS Pro 优雅关闭偶发首次超时需强制结束。
5. Git identity 未配置（不 commit）。
6. 仅 Pro 3.5 真实验证。
7. 6 个 Service 尚未实现（Attributes/Selection/Geoprocessing/Raster/DataManagement/Layout，接口已建、NOT_IMPLEMENTED 占位）。
8. Add-in 部署曾遇到 `RegisterAddIn` 返回 0 但不替换已安装包；已在 Pro 完全退出后通过保留旧包备份并覆盖官方现有安装路径完成恢复。该历史部署阻塞已解除；本次 5.6.4 包为版本 1.0.1、202613B，已与工作区包 SHA-256 一致并由新 Pro runtime 证明加载。
9. 当前 Harness 下 `HttpListener` 测试启动报“句柄无效”；直接 Codex 进程运行 ArcPy import 也出现环境级退出码 `-1073741819`，但已加载的 Pro 内 Bridge 真实返回 Python 3.11.11/ArcPy 3.5。
10. Phase 5.6 audit baseline：`RequestTimeoutMs=30000` 曾同时作用于 MCP 与 Bridge；旧实现的超时/已发送取消不会终止 Python action。5.6.2 已加入独立 `PythonRequestTimeoutMs=18000`、`PythonProcessShutdownTimeoutMs=3000` 和 quarantine；5.6.3 已加入 response/stderr/action/protocol guardrails，`PythonAllowedPaths` 因兼容性审计延期。

## 13. 下一步

Phase 5.6.2 = **PASS（Core + real runtime）**；Phase 5.6.3 = **PASS**；Phase 5.6.4 = **PASS**。保留单一 persistent Bridge、stdin/stdout NDJSON、serialized execution；已加入 1 MiB stdout response limit、64 KiB/进程 stderr logging budget、protocol fail-fast、production/test action allowlist 与默认关闭 test action。路径 allowlist 因 ArcGIS FileGDB/UNC/SDE/reparse 兼容性完成设计并延期，不改变 Phase 5.5 默认行为。不开放任意 Python、不改 HTTP/6511、不引入第二 Bridge 或多 worker。**Phase 5.7.1–5.7.5 已 PASS；7 个 HTTP 用例因 Case H 保持 BLOCKED_BY_HARNESS；Phase 5.8 preflight 已 COMPLETE/ACCEPTED；外部 native host probe 已使 Recovery Gate PASS，下一步是在同一 native prompt 运行 test-only `create-retained` fixture recipe。**

## 14. Phase 5.6.1 Timeout / Cancellation / Recovery Design（2026-09-03）

### Status

- `Phase 5.6.1 = DESIGN COMPLETE`。
- `Implementation = NOT STARTED`。
- `Phase 5.5 = PASS`；当前真实 ArcGIS Pro 3.5 runtime `tools/list=30`。
- Formal decision：[`DECISION-005-python-bridge-timeout-cancellation-recovery.md`](decisions/DECISION-005-python-bridge-timeout-cancellation-recovery.md)。

### Contract summary

- Python deadline 使用新 `PYTHON_TIMEOUT`；caller cancellation 使用现有 `CANCELLED`。
- pre-dispatch timeout/cancel 保持健康 process 不变；post-dispatch timeout/cancel 先 quarantine、terminate process tree、bounded wait/cleanup，再返回；下一次独立 request 才启动 fresh process。
- 写入开始后但 flush 结果不确定的 request 按 possibly-dispatched 处理；不得自动 replay。
- correlation ID 由 ProcessManager 生成，包含 generation 和 monotonic/不可复用序列；duplicate pending insertion 拒绝；late/unknown 或旧 generation response 丢弃。
- cleanup 必须覆盖 normal completion、pre/post-dispatch abort、process exit、startup failure 和 dispose；不得在 semaphore dispose 后 Release。
- 5.6.2 最小实现只覆盖 ProcessManager/config/error/pending/lifecycle cleanup；output/stderr limits、path guardrails、action classification 和 runtime acceptance 留在后续子阶段。

### Gate

**READY FOR PHASE 5.6.2 IMPLEMENTATION**；本阶段不实现 5.6.2。

## 15. Phase 5.6.2 Core Lifecycle Reliability Implementation（2026-09-03）

### Status

- `Phase 5.6.2 = PASS（Core）`。
- `Implementation = COMPLETE`。
- `Phase 5.6.3 = NOT STARTED`。
- Phase 5.5 remains PASS；current active ArcGIS Pro baseline remains `tools/list=30`。

### Implementation evidence

- ProcessManager now owns `py-{generation}-{sequence}` wire correlation IDs and rewrites business factory IDs at the transport boundary.
- Dispatch state distinguishes pending registration, dispatching, dispatched, and completed; uncertain writes are quarantined.
- `PythonRequestTimeoutMs=18000` excludes Bridge startup; `PythonProcessShutdownTimeoutMs=3000` bounds graceful/kill/reader cleanup. The values are based on the measured approximately 8-second startup and existing 30-second outer MCP deadline.
- Post-dispatch timeout/cancellation uses an internal cleanup path independent from the caller token and releases `_flight` only after quarantine completes. No business request replay exists.
- Quarantine is Process-instance and generation-aware, preventing an old request's delayed unwind from stopping a newer generation after explicit Stop/Restart.
- Startup failure, process exit, duplicate/late response, pending TCS, explicit restart, and Dispose paths are covered by the implementation and tests.

### Verification boundary

- Core lifecycle tests: 9/9 PASS with a temporary standard-library Python fixture; full automated run: Unit 20/20 PASS, Integration 20/20 PASS, Server 19/26 with 7 pre-existing Harness blocks.
- Read-only MCP regression against the currently loaded Pro instance: initialize PASS, `tools/list=30`, Python ping/runtime/summary PASS. The newly modified Add-in was not deployed in this phase; modified-package ArcGIS Pro timeout E2E remains Phase 5.6.4.

### Final Gate

**PHASE 5.6.2 PASS**；**STOP — WAIT FOR PHASE 5.6.3 INSTRUCTION**。

## 16. Phase 5.6.3 Output / Protocol / Action / Access Guardrails（2026-09-03）

### Status

- `Phase 5.6.3 = PASS`。
- `Implementation = COMPLETE`。
- `Phase 5.6.4 = NOT STARTED`。
- Path Guardrail = `DESIGN COMPLETE / DEFERRED`；modified-package ArcGIS Pro runtime acceptance remains pending.

### Implementation evidence

- `PythonMaxResponseBytes=1048576`：ReadLine 后按 UTF-8 bytes 检查；超限不截断，返回 `PYTHON_OUTPUT_LIMIT_EXCEEDED`，标记 process unsafe 并进入既有 quarantine/stale cleanup 路径。
- 非空 malformed JSON、invalid response schema 和 invalid correlation ID 返回 `PYTHON_PROTOCOL_ERROR`，不伪装成 `PYTHON_TIMEOUT`；空白 framing noise 忽略。
- `PythonMaxStderrBytes=65536` 为每进程 logger diagnostic 累计上限；stderr reader 在超限后继续 drain 并丢弃日志，避免 pipe backpressure。
- `bridge_runner.py` 使用 `PRODUCTION_ACTIONS` / `TEST_ACTIONS` 显式 allowlist；`PythonAllowTestActions=false` 默认通过 `ARCGIS_PRO_MCP_ALLOW_TEST_ACTIONS` 禁用测试 action，启用必须显式配置。
- 未加入 `PythonAllowedPaths`、`PYTHON_PATH_NOT_ALLOWED` 或伪 sandbox；absolute/relative、FileGDB child、UNC、mapped drive、SDE、`..`、case、reparse/junction 和 future output path 已完成兼容性审计，限制策略延期。

### Measurements and verification

- 当前真实 MCP runtime 五个代表调用的业务文本 payload：4、146、536、1060、900 bytes；最大 MCP 封装响应观测 2048 bytes；选择 1 MiB 默认值，明显高于当前正常响应。
- Guardrail/lifecycle tests：`16/16 PASS`；完整 Unit `27/27 PASS`、Integration `20/20 PASS`、Server `19/26`，7 项既有 Harness `HttpListenerException: 句柄无效`。
- 当前已加载旧 package 的 Pro 只读 regression：initialize、`tools/list=30`、Python ping/runtime/summary PASS；修改包未部署，modified-package runtime 为 `NOT VERIFIED`，留待 5.6.4。

### Final Gate

**PHASE 5.6.3 PASS**；**STOP — WAIT FOR PHASE 5.6.4 INSTRUCTION**。

## 17. Phase 5.6.4 Real ArcGIS Pro / ArcPy / MCP E2E Final Acceptance（2026-09-03）

### Status

- **Phase 5.6.4 = PASS**。
- **Phase 5.6 = PASS**。
- Detailed report：[`Docs/phases/PHASE_05_6_4_FINAL_REPORT.md`](phases/PHASE_05_6_4_FINAL_REPORT.md)。

### Evidence summary

- Latest Add-in package: 202613 bytes, SHA-256 `62FBE89CAE9F03077C797A146662AFC81ED29E59539327D85FBC190A179B8D65`; workspace and installed package match, embedded assembly hashes match current builds.
- New Pro PID 15740 loaded the package. MCP `initialize` passed with protocol `2024-11-05`; `tools/list=30`; test actions and `python_execute` are absent. Normal production defaults keep test actions disabled.
- Real ArcPy discovery passed (`TestPolygons`: FeatureClass/Polygon/WKID 3857/count 4/6 fields/oidCount 1); current workspace discovery returned 9 feature classes after this run's buffer output. Phase 5.5 error/argument contracts remained unchanged.
- Real ArcGIS Python production-runner probes passed: timeout 31092→1320 (`PYTHON_TIMEOUT`, 19555ms), cancellation 25112→31872 (`CANCELLED`, 3050ms), same-PID structured exception 6032, crash 8460→17380 (`PYTHON_BRIDGE_UNAVAILABLE`), and Dispose with no probe child.
- Guardrail/lifecycle 16/16, Unit 27/27, Integration 20/20; Server 19/26 with 7 `HttpListenerException: 句柄无效` cases classified `BLOCKED_BY_HARNESS`. No functional Phase 5.6 blocker remains.

### Boundary and final gate

`PythonAllowedPaths` remains `DESIGN COMPLETE / DEFERRED`; the Bridge is a controlled structured-action subprocess, not an OS sandbox. HTTP client-disconnect cancellation remains `NOT VERIFIED / future scope`; complete Phase 4 matrix was not rerun, and the performed regression is explicitly spot-only.

**PHASE 5.6.4 PASS**；**PHASE 5.6 PASS**；**STOP — WAIT FOR NEXT PHASE INSTRUCTION**。

## 18. Post-Phase-5.6 Transition Gate（2026-09-03）

### Decision

- `PHASE_05.md:20` 明确列出 `5.7 Tests → 5.8 Real Verification → 5.9 MCP→…→ArcPy → 5.10 Regression → 5.11 Acceptance`。
- 全仓搜索只找到上述 roadmap 定义；不存在 `PHASE_05_7*` 独立文件或其他 Phase 5.7 scope/acceptance 文档。
- 因而 Phase 5.7 **正式存在（roadmap-defined）**，但尚未开始；Phase 5 整体不能标记 COMPLETE。
- `PHASE_06.md` 的本条为 2026-09-03 transition gate 历史记录；当时为 NOT STARTED。当前最新状态已由第 47 节覆盖为 Entry Preflight COMPLETE、Phase 6 IN PROGRESS / BLOCKED_BY_EXTERNAL_CLIENT。

### Transition baseline

- Build：`dotnet build -m:1 -c Debug --no-restore`，0 errors，3×NU1900 网络/漏洞源告警。
- Runtime：`initialize` HTTP 200；`tools/list` HTTP 200 / 30；`python_bridge_ping` HTTP 200 / `pong`。
- Runtime：ArcGIS Pro 3.5.0 build 57366 PID 15740；MCP `127.0.0.1:6520/mcp` 可用；Python child 31400；6511 未监听。
- Git：无 commit；`git diff --stat` 为空；当前仓库内容显示为未跟踪文件；本轮未执行 commit/push/config。

### Phase 6 preflight boundary

- Phase 6 的精确文件范围只有“各客户端配置与真实连接验证”，并要求 `initialize / tools/list / tools/call / GIS 真实执行`；未定义 provider abstraction、credential store、conversation loop 或独立 subphases。
- 当前 server/runtime/Bridge 基础条件已具备，但 AI client、MCP client、model integration、provider credentials 和 AI→MCP→ArcGIS E2E 均缺失或未验证。
- 这些是未来 Phase 6 的入口条件，不改变本次 Case A 决策，也不授权本轮实现。

### Final gate

**READY FOR PHASE 5.7**。本轮仅完成 transition gate 与 next-phase preflight；不开始 Phase 5.7 实现，不开始 Phase 6。

## 19. Phase 5.7 Test Architecture & Coverage Preflight（2026-09-03）

### Result

- 测试源码 inventory：73 个 xUnit Fact；Unit 27、Integration 20、Server 26。
- 本轮 baseline：Unit 27/27 PASS；Integration 20/20 PASS；Server 19/26 PASS，7 项因当前宿主 HttpListener harness 的 HttpListenerException: 句柄无效。
- Build：dotnet build -m:1 -c Debug --no-restore，0 errors，3×NU1900 环境告警。
- 静态 tool class / Composition registration / runtime tools/list 均为 30，名称集合无差异。
- 当前只读 MCP：initialize、tools/list=30、python_bridge_ping=pong PASS。
- 已识别 P0/P1 测试缺口：30-tool snapshot/schema、5 个 Python tool facade、clip/intersect/dissolve 单独 call、完整 error matrix、HTTP harness 可复验性和完整 Phase 4 regression。

### Boundary

本轮没有修改 Source、Tests、配置、package 或 AI/provider；没有执行昂贵的 Pro E2E、test action 或 Phase 5.8/Phase 6 工作。详细报告见 [PHASE_05_7_TEST_ARCHITECTURE_COVERAGE_PREFLIGHT.md](phases/PHASE_05_7_TEST_ARCHITECTURE_COVERAGE_PREFLIGHT.md)。

### Final gate

**READY FOR PHASE 5.7 TEST IMPLEMENTATION**。Phase 5.7 尚未实现或验收；Phase 5 整体仍 NOT COMPLETE。

## 20. Phase 5.7.1 Production Tool Contract Snapshot & Test Foundation（2026-09-03）

### Status

- **Phase 5.7.1 = PASS**。
- **Phase 5.7 = IN PROGRESS**；Phase 5.7.2 尚未开始。
- Phase 5 overall = **NOT COMPLETE**；Phase 5.8–5.11 仍未独立验收。

### Implementation and verification

- 新增集中式 30-tool contract snapshot 与 production contract UnitTests；没有新增 production tool、Registry/Router/Bridge、配置或测试项目。
- Unit 从 27 增至 **38/38 PASS**，其中新增 11 项 Fact。
- 真实 production Composition.BuildRegistry() 通过反射访问；测试未手工注册 30 个工具。
- 覆盖 registry count/unique/name exact set、class↔registration、metadata/category/execution/RequiresArcGIS、schema structural/required/serialization、5 Python、4 GP 和 forbidden tools。
- Integration **20/20 PASS**；Server **19/26 PASS**，7 项保持 BLOCKED_BY_HARNESS（HttpListener 句柄无效）。
- 真实 MCP 只读 baseline：initialize HTTP 200；tools/list HTTP 200/count 30，runtime name set 与 snapshot 完全一致。
- Build dotnet build -m:1 -c Debug --no-restore：0 errors，3×NU1900 环境告警。

### Boundary

本阶段没有实现 Python facade 行为测试、GP 参数/执行测试、真实 business tool E2E、完整 error matrix、HTTP harness workaround、Phase 5.7.2 或 Phase 5.8。详细报告见 Docs/phases/PHASE_05_7_1_TOOL_CONTRACT_TESTS.md。

### Final Gate

**PHASE 5.7.1 PASS**；**STOP — WAIT FOR PHASE 5.7.2 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS。

## 21. Phase 5.7.2 Tool Behavior Tests（2026-09-03）

### Status

- **Phase 5.7.2 = PASS**。
- **Phase 5.7 = IN PROGRESS**；Phase 5.7.3 尚未开始；Phase 5 overall = **NOT COMPLETE**。

### Implementation and verification

- 新增 25 个 Unit Fact：5 个 Python facade、4 个 GP 独立路由/参数、两个 filesystem placeholder、error/cancellation/argument contract coverage。
- 新增 1 个无 HTTP Server Fact：生产 Buffer tool failure 经 Router/McpServer 渲染为 `isError=true`，并保留 error code/message。
- TestSupport 新增 configurable Python fake 和 GP recorder；FakeArcGISHost 支持注入 test service；未修改 production Source、Registry/Router/Bridge 或配置。
- Unit **63/63 PASS**；Integration **20/20 PASS**；Server **20/27 PASS**，7 项仍为既有 HttpListener `句柄无效` harness blocker。
- 30-tool production snapshot regression 保持 PASS；静态 class/registration/runtime tools/list 仍为 30 且名称集合一致。
- 真实 MCP 只读 baseline：initialize HTTP 200；tools/list HTTP 200/count 30；python_bridge_ping HTTP 200/pong。
- Build `dotnet build -m:1 -c Debug --no-restore`：0 errors，3×NU1900 环境告警。

### Boundary

本轮没有运行真实 GP/业务 placeholder、没有重跑完整 Phase 5.6、没有修复 HTTP harness、没有实现 Phase 5.7.3、Phase 5.8、Phase 6 或 AI/provider。详细报告见 [PHASE_05_7_2_TOOL_BEHAVIOR_TESTS.md](phases/PHASE_05_7_2_TOOL_BEHAVIOR_TESTS.md)。

### Final Gate

**PHASE 5.7.2 PASS**；**STOP — WAIT FOR PHASE 5.7.3 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS。

## 22. Phase 5.7.3 Server Protocol & HTTP Harness Resolution（2026-09-03）

### Status

- **Phase 5.7.3 = PASS**；**Phase 5.7 = IN PROGRESS**；Phase 5.7.4 尚未开始。
- Phase 5 overall = **NOT COMPLETE**；Phase 5.8–5.11 尚未独立验收。

### Evidence

- ServerTests 从 27 增至 41；完整结果 **34/41 PASS**，7 个 HTTP transport case 在 `HttpListener.Start()` 报 `HttpListenerException (6): 句柄无效`，分类为 **BLOCKED_BY_HARNESS / Case H**。
- 独立 BCL raw probe 对固定端口 `16521` 和释放后的动态端口 `54450` 均在 `System.Net.HttpListener.SetupV2Config()` 失败；probe 由独立 child process 执行，排除固定端口、xUnit fixture 和单一 testhost 的解释。
- HTTP-independent `MCPProtocolTests` **9/9 PASS**；`MCPServerTests` **25/25 PASS**。新增覆盖 string/null/missing ID、string ID preservation、params/name/arguments shape、batch invalid request、server timeout token 和 caller cancellation。
- Unit **63/63 PASS**；Integration **20/20 PASS**；Build **0 errors / 3×NU1900**。
- 当前 Pro 3.5.0 build 57366 只读 MCP：initialize HTTP 200、protocol `2024-11-05`、`tools/list=30` distinct、`python_bridge_ping=pong`。

### Boundary

本阶段没有修改 production transport、Registry/Router/Bridge、配置、package 或 AI/provider；没有进行 URLACL/firewall/权限变更。HTTP client-disconnect cancellation、large request policy、完整 Phase 4 regression 和 Phase 5.7.4 仍未完成。详细报告见 [PHASE_05_7_3_SERVER_PROTOCOL_HARNESS.md](phases/PHASE_05_7_3_SERVER_PROTOCOL_HARNESS.md)。

### Final Gate

**PHASE 5.7.3 PASS**；**STOP — WAIT FOR PHASE 5.7.4 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS。

## 23. Phase 5.7.4 Test Data Ownership & Mutation Safety（2026-09-03）

### Status

- **Phase 5.7.4 = PASS**；**Phase 5.7 = IN PROGRESS**；Phase 5 overall = **NOT COMPLETE**。
- Phase 5.7.5–5.11 仍未独立验收；Phase 5.8 未开始。

### Evidence

- 新增 `Tests/TestSupport/TestWorkspace.cs`：每次运行生成带 ownership marker 的唯一 `%TEMP%` root，提供短且 ArcGIS-compatible 的 run-scoped dataset name、separator-aware containment、owned-path registration、reparse refusal 和非抛异常 cleanup result。
- Placeholder filesystem tests 和 Python Bridge lifecycle tests 使用统一 test-owned workspace；Python child 创建的 `requests.ndjson` 在 manager 停止后登记，再执行 cleanup。Unit **68/68 PASS**；Integration **20/20 PASS**。
- `MCPProtocolTests` **9/9**、`MCPServerTests` **25/25** PASS；没有重新诊断 HTTP harness，既有完整 Server **34/41** 与 7 个 `BLOCKED_BY_HARNESS / Case H` 保持不变。
- `TestDate/Phase4Test.gdb` 仅做只读 inventory：top-level 仍只有该 GDB，115 个物理文件（104 个其他文件、11 个 `.sr.lock`），没有修改、删除或清理历史输出。Real Pro 只读 baseline：initialize/tools/list HTTP 200，tools/list=30 distinct。
- solution build：**0 errors / 3×NU1900**。详细报告见 [`PHASE_05_7_4_TEST_DATA_MUTATION_FIXTURES.md`](phases/PHASE_05_7_4_TEST_DATA_MUTATION_FIXTURES.md)。

### Boundary

本阶段未执行完整 Real GP、30-tool business execution、ArcGIS map mutation、HTTP workaround、Phase 5.8/5.10 或生产代码改造。共享 `TestDate/Phase4Test.gdb` 仍为 historical shared verification workspace；下一步为 Phase 5.7.5。

### Final Gate

**PHASE 5.7.4 PASS**；**STOP — WAIT FOR PHASE 5.7.5 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS。

## 24. Phase 5.7.5 Test Consolidation & Acceptance（2026-09-03）

### Status

- **Phase 5.7.5 = PASS**；**Phase 5.7 = PASS**；**Phase 5 overall = NOT COMPLETE**。
- Phase 5.8 = **NOT STARTED**；下一正式 gate 为 **READY FOR PHASE 5.8 PREFLIGHT**，不是本轮开始 Phase 5.8。

### Evidence

- Final inventory：Unit 68、Integration 20、Server 41，共发现 129 Facts；Unit 68/68、Integration 20/20、MCPProtocol 9/9、MCPServer 25/25 PASS。
- Server HTTP 7 项按 `BLOCKED_BY_HARNESS / Case H` 记录；它们在 `HttpListener.Start()` 抛出 `HttpListenerException (6): 句柄无效`，不是 PASS 或代码 FAIL。
- 30-tool contract、Python 5 facade、GP 4 routing、placeholder、error rendering、protocol edge、TestWorkspace、coverage diagnostic、P0/P1 review 均已汇总。
- Real Pro read-only baseline：initialize HTTP 200、tools/list=30 distinct、python_bridge_ping=pong；本轮没有业务 mutation/GP/AI/provider。
- Coverage 仅诊断用途：Unit line/branch 0.68/0.57，Integration 0.21/0.15，Server non-HTTP 0.19/0.17；无阈值 gate。

### Boundary

Real GP、完整 30-tool Pro business execution、selected OID restore、dedicated mutation project、supported-host HTTP rerun 和 full Phase 4 regression 仍留在后续阶段；shared TestDate/Phase4Test.gdb 保持 untouched。

### Report

完整 41 节报告：[`PHASE_05_7_5_TEST_ACCEPTANCE.md`](phases/PHASE_05_7_5_TEST_ACCEPTANCE.md)。

### Final Gate

**PHASE 5.7.5 PASS**；**PHASE 5.7 PASS**；**READY FOR PHASE 5.8 PREFLIGHT**；**STOP — WAIT FOR PHASE 5.8 INSTRUCTION**。

## 25. Phase 5.8.1 Initial Fixture Attempt（2026-09-03）

### Status

- Phase 5.8 preflight = **COMPLETE / ACCEPTED**。
- Phase 5.8.1 = **BLOCKED_BY_ENVIRONMENT**；Phase 5.8 = **IN PROGRESS**；Phase 5.9 = **NOT STARTED**；Phase 5 overall = **NOT COMPLETE**。

### Evidence

- `TestPolygons` read-only source health PASS；`TestWorkspace` file cleanup probe PASS。
- Test-only fixture helper已实现 fresh FileGDB + `CopyFeatures` + controlled project recipe，但 ArcGIS 官方 Python standalone `import arcpy`/`arcgisscripting` 以 Windows access violation `-1073741819` 退出；owned GDB/source/ClipMask/project 均未创建。
- 用户 `MyProject1.aprx` `isDirty=true` 且不属于 test-owned project，未修改；project mutation fixture = **BLOCKED_BY_FIXTURE**。
- MCP 只读 baseline 仍为 initialize/tools/list HTTP 200、tools/list=30、python_bridge_ping=pong；shared `TestDate/Phase4Test.gdb` 未写入、清理或删锁。
- solution build 0 errors/3×NU1900；fixture runner 0 errors；Unit 68/68；Integration 20/20。

### Final Gate

**PHASE 5.8.1 BLOCKED_BY_ENVIRONMENT**；GP/ArcPy fixture **NOT READY**；project mutation fixture **NOT READY / BLOCKED_BY_FIXTURE**。停止，不进入 Phase 5.8.2。

详细报告：[`PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md`](phases/PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md)。

## 27. Phase 5.8.1 ArcGIS Python Execution-Context Recovery（2026-09-03）

- **Recovery status**：Recovery Gate **PASS**；外部 native host 中同一 probe `ARCPY_OK=true`、exit code 0；Phase 5.8.1 fixture preparation **IN PROGRESS / NOT YET ACCEPTED**；Phase 5.8.2/5.9 尚未开始。
- **Working Bridge**：ArcGISPro PID 15740 → production Bridge PID 16064；`python_runtime_info`、`dataset_summary`、`list_fields`、workspace discovery、attributes 均真实 PASS。
- **Standalone matrix**：direct official Python 与 `propy.bat` 的 core/stdlib/NumPy 1.26.4 PASS；`arcgisscripting`、normal `arcpy`、`ARCPY_NO_IMPORTS=1` 均以 `-1073741819` / `0xC0000005` 退出。
- **Diagnosis**：未发现目标模块 shadowing 或简单白名单环境污染；Application/WER 无匹配 crash event，faulting module UNKNOWN；license 只读为 Advanced。外部 native host 成功而 current Codex child 失败，故分类为 **BLOCKED_BY_HARNESS / execution-context specific**，不是 machine-global ArcPy 或 license failure。
- **Safety**：没有 production Source/security boundary、用户 `MyProject1.aprx`、shared `TestDate/Phase4Test.gdb` 或系统环境修改；最终一个 production Bridge child、零 fixture probe orphan。
- **Next action**：在同一外部 native prompt 执行 `call proenv.bat`，再运行 test-only fixture runner 的 `create-retained`；成功后保留 owned root 和 JSON manifest，继续留在 5.8.1，不跳到 5.8.2。
- **Report**：[`PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md`](phases/PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md)。

## 28. Phase 5.8.1 Native Host Fixture Resume Implementation（2026-09-04）

- Recovery Gate 保持 **PASS**；本轮没有在当前 Codex harness 执行任何 ArcPy fixture creation。
- test-only runner 的真实 CLI 模式为 `create-retained`、`cleanup-probe`、`project-probe`；用户可见参数仍只有一个 positional mode。
- `TestWorkspace.Create()` 使用 `%TEMP%\ArcGISProMCP\Phase5_7_4_P57_<runId>`，先创建 `.arcgis-pro-mcp-test-owned` marker（包含 RunId、root 和 UTC creation time），再启动 ArcPy。
- `create-retained` 目标为 `Phase58_<RunId>.gdb`、`P58_TestPolygons`、`P58_ClipMask` 和 root 下的 `Phase58Controlled.aprx`；source health、copy health、ClipMask health、controlled map/layer datasource、project reopen、selection/visibility baseline 均由 direct test-only ArcPy payload 记录。
- 成功 manifest 为 `<root>\fixture-manifest.json`，schema 为 `phase-5.8.1-fixture-manifest-v1`；成功结果由 `FIXTURE_RESULT_BEGIN`/`FIXTURE_RESULT_END` 包围，包含 `status=READY`、`retained=true`、owned paths 和 manifest path。
- `cleanup-probe` 与 `project-probe` 使用独立 disposable root；Python 进程退出后由 TestWorkspace cleanup 验证 GDB/project 删除、root residual 和 ownership/lock 结果。
- TestWorkspace cleanup 对残留 `.lock`/`.sr.lock` 先行返回 `BLOCKED_BY_RUNTIME_LOCK`，不手工删除锁文件。
- 本轮验证：fixture helper 0 warnings/0 errors；solution 0 errors/3×NU1900；Unit 68/68；Integration 20/20。
- Phase 5.8.1 仍 **IN PROGRESS / NOT YET ACCEPTED**；待 external native host 返回完整 JSON 后再做只读 artifact validation。未修改 production Source、用户工程或 shared GDB。

## 29. Phase 5.8.1 Unit Regression / Process-Lock Recovery（2026-09-04）

- **Regression Gate**：修复前完整 Unit 的 `PostDispatchCancellationKillsOldProcessAndNextRequestStartsFreshProcess` 在 `PythonBridgeLifecycleTests.cs:526` 的 `File.ReadAllLines` 发生间歇性 `requests.ndjson` sharing violation；目标测试 isolated 10/10、生命周期类 16/16，而完整 Unit 在并行调度下复现 67/68。
- **Root cause**：`TEST DEFECT`。fake Python child 在 test-owned root 写日志时，helper 在 writer handle 释放前直接读取；不是 production ProcessManager、ArcGIS runtime 或用户进程锁。未使用自定义 blocker code，也未杀未知进程。
- **最小修复**：仅在 `Tests/UnitTests/PythonBridgeLifecycleTests.cs` 的 diagnostic log getter 捕获 transient `IOException` 并返回空快照，保留既有 polling、cancel/quarantine/kill、PID、generation、pending 和 no-replay assertions；没有修改 production Source。
- **Fresh evidence**：目标测试 10/10；完整 Unit 5/5 次 68/68；Integration 20/20；solution Build 0 errors/3×NU1900。运行期观察到 test-owned Python PID `23576 → 3384`，旧/新不同且测试后无 Python child。
- **Retained fixture**：`P57_B8C6FE8E` 保持 retained；marker、manifest、owned GDB、`P58_TestPolygons`、`P58_ClipMask`、`Phase58Controlled.aprx` 和 owned datasource/project/selection/visibility evidence 仍 PASS；shared GDB、`MyProject1.aprx` 和未知 locks 未修改。
- **Report**：[`PHASE_05_8_1_UNIT_REGRESSION_RECOVERY.md`](phases/PHASE_05_8_1_UNIT_REGRESSION_RECOVERY.md)。

### Final Gate

**PHASE 5.8.1 FINAL GATE = PASS CANDIDATE；等待独立 Gate Keeper review**。Phase 5.8.1 仍 `IN PROGRESS / NOT YET ACCEPTED`；Phase 5.8 仍 `IN PROGRESS`；Phase 5.8.2 保持 `NOT STARTED`。

## 30. Phase 5.8.1 Formal Acceptance — Repository State Reconciliation（2026-09-04）

本节追加记录独立 Gate Keeper 的正式验收，不改写前述历史记录。

### Gate Keeper Decision

```text
FORMALLY ACCEPT — PHASE 5.8.1 PASS
READY FOR PHASE 5.8.2
```

### Reconciled Status

```text
Phase 5.7 = PASS
Phase 5.8 = IN PROGRESS
Phase 5.8.1 = PASS
Phase 5.8.2 = NOT STARTED / READY TO START
Phase 5.8.3 = NOT STARTED
Phase 5.8.4 = NOT STARTED
Phase 5.8.5 = NOT STARTED
Phase 5.9 = NOT STARTED
Phase 5.10 = NOT STARTED
Phase 5.11 = NOT STARTED
Phase 5 overall = NOT COMPLETE
```

### Accepted Evidence Basis

- Source health **PASS**；external native ArcPy **PASS**。
- Owned FileGDB **PASS**；`P58_TestPolygons` **PASS**；`P58_ClipMask` **PASS**。
- Controlled `Phase58Controlled.aprx` **PASS**；explicit project reopen **PASS**；owned datasource **PASS**。
- Selection baseline **PASS**；visibility baseline **PASS**；cleanup probe **PASS**；project cleanup probe **PASS**。
- Manifest **PASS**；no orphan process **PASS**。
- Build **PASS**（0 errors，3 `NU1900` environment warnings）；Unit **68/68 PASS**；Integration **20/20 PASS**。
- Fixture Preparation = **PASS**；Regression Gate = **PASS**；previous Final Gate = **PASS CANDIDATE**；Independent Gate Keeper = **FORMALLY ACCEPT**；Final Phase 5.8.1 status = **PASS**。
- Unit regression root cause remains **TEST DEFECT**；production Python Bridge lifecycle semantics were not modified。

### Retained Fixture Identity

Read-only marker and manifest both identify the retained fixture as:

```text
RunId: P57_B8C6FE8E
Root: <user-home>\AppData\Local\Temp\ArcGISProMCP\Phase5_7_4_P57_B8C6FE8E
Manifest schema: phase-5.8.1-fixture-manifest-v1
Retention: INTENTIONALLY_RETAINED_OWNED_FIXTURE
```

The inherited `P57` naming is **KNOWN NAMING DEBT / NON-BLOCKING**. The retained fixture was not recreated, renamed, moved, or modified. The shared `Phase4Test.gdb` and `MyProject1.aprx` remain untouched.

### Carried-Forward Limitations

- `list_maps` = **PARTIAL**；`get_map_info` = **PARTIAL**。
- `get_dataset_info` = **LIMITED IMPLEMENTATION**；`get_raster_info` = **LIMITED IMPLEMENTATION**。
- `ArcGISProject.isDirty` on Pro 3.5 = unavailable / non-blocking。
- HTTP client-disconnect cancellation = **NOT VERIFIED**。
- Seven HTTP transport tests = **BLOCKED_BY_HARNESS**。
- `select_layer` production schema contains only `mapName` and `layerName`；it does not accept deterministic OID selection. This is carried into Phase 5.8.2 planning; no selection mutation PASS is claimed here。

### Next Authorized Task

```text
Phase 5.8.2 — Native Real Verification
Execution status: NOT STARTED / READY TO START
```

This reconciliation did not begin Native mutation, Phase 5.8.3, or Phase 5.9.

## 32. Phase 5.8.2 Native Real Verification（2026-09-04）

本轮已在 retained owned fixture P57_B8C6FE8E 上完成授权的 Native real verification；详细 32 项证据见 Docs/phases/PHASE_05_8_2_NATIVE_REAL_VERIFICATION.md。受控运行时使用 PID 2796 和显式 owned APRX；get_current_map、活动地图 get_layers/get_layer_info、visibility mutation/restore、remove/add layer 及 owned feature 读取均取得 fresh evidence。list_maps/get_map_info、dataset info、selection count 和 add_layer reorder limitation 按实际结果分类，未伪造 PASS。未修改 production code、MyProject1.aprx、shared Phase4Test.gdb 或 retained fixture；专用实例已关闭且未保存。

当前状态：Phase 5.8.2 = **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 5.8.3–5.8.5、5.9–5.11 = **NOT STARTED**；Phase 5 overall = **NOT COMPLETE**。

## 33. Phase 5.8.2 Formal Acceptance（2026-09-04）

Independent Gate Keeper decision:

FORMALLY ACCEPT — PHASE 5.8.2 PASS

本节追加正式 acceptance，不改写前述 Phase 5.8.2 历史报告。Selection 结论按 Gate Keeper 要求校正：select_layer = **PARTIAL / LIMITED IMPLEMENTATION**；clear_selection 的执行路径已观察，但真实 non-zero → zero selection mutation 在 Phase 5.8.2 **NOT VERIFIED**。当前下一授权任务为 Phase 5.8.3 GP / ArcPy Real Verification，状态 **READY TO START**。

## 34. Phase 5.8.3 GP / ArcPy Real Verification（2026-09-04）

- 四个生产 GP capability（buffer、clip、intersect、dissolve）均通过真实 ArcGIS Pro SDK GP 执行，并由生产 ArcPy Bridge `dataset_summary` 证明输出存在、为 Polygon FeatureClass、WKID 3857、数量合理且保留 GP messages。
- 生产 Python Bridge 的 `python_bridge_ping`、`python_runtime_info`、`dataset_summary`、`list_fields`、`list_workspace_datasets` 均有 fresh evidence；Bridge PID `1028` 在独立重复调用中复用。
- retained fixture `P57_B8C6FE8E` post-health PASS；输出只写入 current-phase owned GDB，并按 `INTENTIONALLY_RETAINED_OWNED_GP_OUTPUTS` 保留给 Phase 5.8.4。
- 详细报告：[`PHASE_05_8_3_GP_ARCPY_REAL_VERIFICATION.md`](phases/PHASE_05_8_3_GP_ARCPY_REAL_VERIFICATION.md)；输出清单：[`PHASE_05_8_3_GP_OUTPUT_MANIFEST.md`](phases/PHASE_05_8_3_GP_OUTPUT_MANIFEST.md)。
- 当前状态：**PHASE 5.8.3 PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 5.8.4、Phase 5.9 及后续阶段未开始。

## 35. Phase 5.8.3 Formal Acceptance（2026-09-04）

Independent Gate Keeper decision:

**FORMALLY ACCEPT — PHASE 5.8.3 PASS**

接受依据：四个生产 GP tool 的 fresh `CALL → OperationResult → output path → FileGDB object → ArcPy Describe → geometry/WKID/count → GP messages` 证据链完整；`P583_Buffer`、`P583_Clip`、`P583_Intersect`、`P583_Dissolve` 均为 current-phase owned、唯一、非覆盖输出，count 分别为 4、4、4、1。五个指定 production persistent Python Bridge tool 均通过；Bridge PID `1028` 在独立重复调用中复用；retained fixture post-health、安全边界和无锁收尾均 PASS。

本节只追加正式 acceptance，不改写前述 PASS CANDIDATE、环境启动事件、known limitations、historical Build/Test baseline 或 `INTENTIONALLY_RETAINED_OWNED_GP_OUTPUTS` 记录。未修改 Word、代码、配置、`MyProject1.aprx` 或 shared `Phase4Test.gdb`。

### Current authorized next task

```text
Phase 5.8.4 — Mutation & Cleanup Verification
NOT STARTED / READY TO START
```

Phase 5.8.4 尚未执行；Phase 5.9–5.11 仍 NOT STARTED；Phase 5 overall = NOT COMPLETE。

## 36. Phase 5.8.4 Mutation & Cleanup Verification（2026-09-04）

- 本阶段已完成真实 visibility mutation/restore、owned current-phase output add/remove、selection contract boundary observation、controlled APRX discard-without-save、runtime lock release 和 exact disposable-root cleanup。
- P58_TestPolygons 可见性 false/true 真实观察并恢复；P583_Buffer 以精确生产路径加入为第四层，返回身份为 P583_Buffer，随后按返回身份移除，原始三层顺序/可见性恢复。
- select_layer 返回 count=0；由于生产 schema 只有 mapName/layerName，结论保持 PARTIAL / LIMITED IMPLEMENTATION；true non-zero → zero clear_selection 仍 NOT VERIFIED。
- Pro PID 25540、Bridge PID 3036 和 6520 listener 已退出；运行时 _gdb.PC.22716.3036.sr.lock 由运行时释放，未手工删除。
- 仅将精确的 current-phase root <user-home>\AppData\Local\Temp\ArcGISProMCP\Phase5_8_3_GP_P57_B8C6FE8E 移入回收站；post-check 证明 root/GDB 消失，retained P57_B8C6FE8E、MyProject1.aprx 和 shared Phase4Test.gdb 未变。
- retained marker/manifest 实际 RunId 均为 P57_B8C6FE8E；fresh production retained counts 为 P58_TestPolygons=4、P58_ClipMask=1。
- 详细报告：[PHASE_05_8_4_MUTATION_CLEANUP_VERIFICATION.md](phases/PHASE_05_8_4_MUTATION_CLEANUP_VERIFICATION.md)。
- 本阶段未重新 Build/Test；accepted baseline 保持 Build 0 errors/3 NU1900、Unit 68/68、Integration 20/20；生产 Bridge lifecycle semantics 未修改。

### Current status

Phase 5.8 = **IN PROGRESS**；Phase 5.8.1 = **PASS**；Phase 5.8.2 = **PASS**；Phase 5.8.3 = **PASS**；Phase 5.8.4 = **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 5.8.5 = **NOT STARTED**；Phase 5.9–5.11 = **NOT STARTED**；Phase 5 overall = **NOT COMPLETE**。

本阶段停止，不开始 Phase 5.8.5 或 Phase 5.9。

## 37. Post-cleanup Retained Semantic Health Recovery Gate（2026-09-04）

Independent Gate Keeper interim review 的 PARTIAL 结论已按要求完成最小 recovery gate。未从回收站恢复 current-phase root，未创建输出，未调用 map/layer/visibility/selection/GP mutation；仅使用 production MCP 对 retained fixture 做 fresh read-only health verification。

- Phase 5.8.3 disposable root、GDB 及 P583_Buffer/P583_Clip/P583_Intersect/P583_Dissolve 均保持 absent。
- retained P57_B8C6FE8E 的 P58_TestPolygons = FeatureClass/Polygon/WKID 3857/count 4；P58_ClipMask = FeatureClass/Polygon/WKID 3857/count 1。
- list_workspace_datasets fresh 返回 exactly two FeatureClasses：P58_TestPolygons、P58_ClipMask；totalCount=2、truncated=false。
- controlled APRX pre/post SHA256 均为 0F514C540553118C443A91C188D8D69E14EEC4EB7C33897D34A82290639AF2F3；marker/manifest hash、RunId P57_B8C6FE8E 均不变；retained GDB 为 65 physical files、0 locks。
- 显式关闭后无 ArcGISPro.exe、production bridge_runner.py child 或 6520 listener；MyProject1.aprx 和 shared Phase4Test.gdb 的既有 hash/inventory 保持不变。

Recovery Gate = **PASS**（post-cleanup retained semantic health and retained fixture integrity）。Phase 5.8.4 仍为 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 5.8.5、Phase 5.9–5.11 未开始，Phase 5 overall 仍 **NOT COMPLETE**。本节不构成正式 Phase 5.8.4 acceptance。

## 38. Phase 5.8.4 Independent Gate Keeper Formal Acceptance（2026-09-04）

Independent Gate Keeper decision:

FORMALLY ACCEPT — PHASE 5.8.4 PASS

接受依据：visibility mutation→observe→restore、P583_Buffer exact-path add→identity/source/count=4 observe→remove→原三图层恢复、controlled APRX discard-without-save、精确 disposable-root cleanup、自然锁释放、受保护路径完整性和 post-cleanup retained semantic health 均已通过。Recovery Gate fresh production MCP 重新证明 P58_TestPolygons count=4、P58_ClipMask count=1，retained workspace 恰好仅含这两个 FeatureClass；清理 root 仍 absent；无 ArcGIS Pro、Bridge 或 6520 listener 遗留。

Selection 结论继续保持 PARTIAL / LIMITED IMPLEMENTATION；clear_selection 的真实 non-zero → zero 仍为 NOT VERIFIED，不影响本阶段其余验收。历史 PASS CANDIDATE、67/68、TEST DEFECT、Recovery Gate 及所有 known limitations 均保留；生产 Bridge lifecycle semantics 未修改。Build/Test 继续沿用 accepted baseline：Build 0 errors/3 NU1900、Unit 68/68、Integration 20/20。

### Current authorized next task

Phase 5.8.5 = **NOT STARTED / READY TO START**。本次仅完成 Phase 5.8.4 文档验收同步；不启动 5.8.5 或 Phase 5.9。Phase 5 overall = **NOT COMPLETE**。

## 39. Phase 5.8.5 Evidence Consolidation（2026-09-04）

- Phase 5.8.5 已授权并进入 **IN PROGRESS**；完整汇总报告见 [PHASE_05_8_5_EVIDENCE_CONSOLIDATION.md](phases/PHASE_05_8_5_EVIDENCE_CONSOLIDATION.md)。
- 基于 Phase 5.8 preflight 与 5.8.1–5.8.4 formal acceptance，30-tool matrix 已完成：PASS 24、PARTIAL 3、LIMITED IMPLEMENTATION 2、NOT VERIFIED 1；tool-level BLOCKED_BY_HARNESS=0、OUT-OF-SCOPE=0。
- Fresh、carried-forward accepted 和 historical evidence 已分栏区分；未把历史证据伪装成本轮 fresh。完整 Phase 5.9 MCP business chain、AI/provider 和 HTTP client-disconnect 不属于本轮闭环。
- 保留唯一已发现的非阻断文档 provenance gap：shared Phase4Test.gdb 历史记录 115/11 与后续盘点 104/0 的差异；没有猜测原因，没有触碰或修复 shared GDB。
- 本轮未启动 Pro/Bridge、未恢复回收站、未创建 output、未执行 mutation；最终只读状态为 disposable root absent、retained GDB 0 locks、Pro/Bridge/6520 均为 0。Build/Test 沿用 accepted baseline：0 errors/3 NU1900、Unit 68/68、Integration 20/20。

### Current status

Phase 5.8.5 = **IN PROGRESS / PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 5.9–5.11 = **NOT STARTED**；Phase 5 overall = **NOT COMPLETE**。本轮停止，等待独立 Gate Keeper，不开始 Phase 5.9。

## 40. Phase 5.8.5 Evidence Consolidation and Phase 5.8 Independent Gate Keeper Formal Acceptance（2026-09-04）

Independent Gate Keeper decision:

FORMALLY ACCEPT — PHASE 5.8.5 PASS

FORMALLY ACCEPT — PHASE 5.8 PASS

接受依据：

- 30-tool matrix 已完成：PASS 24、PARTIAL 3、LIMITED IMPLEMENTATION 2、NOT VERIFIED 1；总数 30。7 个 HTTP transport tests 继续保留为 BLOCKED_BY_HARNESS，位于测试 harness 边界，不升格为 production tool failure。
- fresh、carried-forward accepted、historical evidence 已明确分离；四个 GP、五个 production persistent Python Bridge，以及 Native/map/layer/data/project/system 工具链均已按既有正式报告闭环。
- retained marker/manifest 的实际 RunId 为 P57_B8C6FE8E；retained fixture health unchanged。controlled APRX、shared GDB、retained GDB、disposable root、lock 和 no-orphan process 的独立安全审查结果保持接受记录。
- select_layer、clear_selection、list_maps/get_map_info、dataset/raster placeholders、ArcGISProject.isDirty、HTTP client-disconnect cancellation 等 known limitations 原样保留；GAP-01（shared GDB 历史 115/11 与后续 104/0 的 provenance 差异）保持 NON-BLOCKING，不猜测、不修复。
- Source/Tests 未变更；accepted baseline 继续为 Build 0 errors/3 NU1900、Unit 68/68、Integration 20/20。生产 Python Bridge lifecycle semantics 未修改。

### Final status and authorized boundary

```
Phase 5.8.5 = FORMALLY ACCEPTED / PASS
Phase 5.8 = FORMALLY ACCEPTED / PASS
Phase 5.9 = NOT STARTED / READY TO START
Phase 5.10 = NOT STARTED
Phase 5.11 = NOT STARTED
Phase 5 overall = NOT COMPLETE
```

下一授权工作为 Phase 5.9，但本次只完成文档状态同步；未开始 Phase 5.9、5.10、5.11 或 Phase 6。

## 41. Phase 5.9 Full MCP E2E Verification（2026-09-04）

- 已完成 fresh production MCP HTTP initialize、notifications/initialized、tools/list 和 30-tool direct matrix；tools/list=30 unique，分类为 PASS 24、PARTIAL 3、LIMITED IMPLEMENTATION 2、NOT VERIFIED 1。
- 已完成 MCP → Router → Native/GP/Python → ArcGIS Pro/ArcPy business chain：active Phase58_TestMap 的 native read、visibility mutation/restore、owned output add/remove/restore；buffer/clip/intersect/dissolve 四个 production GP；五个 persistent Python Bridge tools；direct ArcPy/FileGDB Describe。
- 本轮唯一 disposable RunId 为 P59_A3FBDFB2；marker 先于 GDB 写入；四个 outputs 均在 P59 owned GDB。生产响应中的 OperationResult toolName、output path 和 GP messages 均已观察；Python Bridge PID 26700 在 Python calls 及 add/remove observations 中复用。
- 显式 mapName lookup 失败、list_maps/get_map_info partial、dataset/raster limited、select_layer schema 无 OID、clear_selection 无 non-zero→zero 证据等限制均原样记录；未修改 production code、Tests、配置、Word 或受保护资产。
- Pro PID 8388 与 Bridge 已优雅退出；精确 save dialog 选择“否(N)”；6520、进程和 GDB lock 自然消失。P59 root 经过 ownership/canonical/reparse/containment/lock/no-orphan gate 后移入回收站，post-check root/GDB/outputs absent。
- controlled APRX SHA256 仍为 0F514C540553118C443A91C188D8D69E14EEC4EB7C33897D34A82290639AF2F3；MyProject1.aprx SHA256 仍为 ECF25A8791219435CE9AC44F7A50B28468074ACDF96C9F55455106FB376BCD14；retained marker/manifest RunId 仍为 P57_B8C6FE8E，retained semantic counts=4/1，retained GDB=65 files/0 locks，shared GDB=104 files/0 locks。
- 详细报告：[PHASE_05_9_FULL_MCP_E2E_VERIFICATION.md](phases/PHASE_05_9_FULL_MCP_E2E_VERIFICATION.md)。

### Current status

Phase 5.9 = **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 5.10、5.11 = **NOT STARTED**；Phase 5 overall = **NOT COMPLETE**。本轮不构成 Phase 5.9 formal acceptance，也不开始下一 Phase。

## 42. Phase 5.9 Independent Gate Keeper Formal Acceptance（2026-09-05）

- Independent Gate Keeper 正式裁定：**FORMALLY ACCEPT — PHASE 5.9 PASS**。
- 接受依据包括 fresh MCP initialize/tools/list=30、30-tool matrix（PASS 24、PARTIAL 3、LIMITED 2、NOT VERIFIED 1）、Native read/mutation/restore、四个 GP 的 OperationResult/messages→ArcPy/FileGDB Describe、五个 persistent Python tools/PID 26700、discard-without-save、自然锁释放、cleanup 和 protected-path post-check。
- usage-limit interruption/stale PID recovery 已正确归类为 harness/tooling event；无 force-kill、无重复 root、无 arbitrary/test-only action。
- Source/Tests、production code/config、Word、MyProject1.aprx、controlled APRX、retained fixture 和 shared Phase4Test.gdb 未修改；历史 candidate、限制、GAP-01 与 baseline 均保留。

### Current status

Phase 5.9 = **FORMALLY ACCEPTED / PASS**；Phase 5.10 Regression = **NOT STARTED / READY TO START**；Phase 5.11 = **NOT STARTED**；Phase 5 overall = **NOT COMPLETE**。本次仅同步正式 acceptance，不启动 Phase 5.10。

## 43. Phase 5.10 Regression Verification（2026-09-05）

- 本轮已按授权完成 Phase 5.10 Regression Verification；详细 fresh evidence 见 [PHASE_05_10_REGRESSION_VERIFICATION.md](phases/PHASE_05_10_REGRESSION_VERIFICATION.md)。
- RunId=`P510_BDE10BCD`；test-owned root 已先写入 ownership marker，再写入 inventory/results；全部 gates 通过后精确移入 Windows Recycle Bin，active temp tree 中已 absent 且可恢复。
- Build：0 errors；3 个 NU1900 为 NuGet vulnerability service index 环境告警。Unit 68/68、Integration 20/20、MCPProtocol 9/9、MCPServer core 25/25、MCPTransport 7/7、Server full 41/41 均 fresh PASS；去重后 129 Facts。
- `PythonBridgeLifecycleTests` 16/16 连续三次 PASS；每次结束均无 test bridge child，最终无 ArcGIS Pro、production/test Bridge、testhost、dotnet test 或 6520/6511 listener 遗留。production Python Bridge lifecycle semantics 未修改。
- 当前主机 fresh MCPTransport 7/7 PASS；历史 Phase 5.7/5.8 Case H / `BLOCKED_BY_HARNESS` 记录原样保留，HTTP client-disconnect cancellation 仍 NOT VERIFIED。
- retained fixture marker/manifest 实际 RunId 仍为 `P57_B8C6FE8E`，P58_TestPolygons/P58_ClipMask 语义健康保持 4/1；controlled APRX、MyProject1.aprx、shared Phase4Test.gdb 和 Source/Tests 未变。
- `select_layer` 当前 schema 仍只有 `mapName`/`layerName`，不支持确定性 OID selection；clear_selection 的真实 non-zero→zero mutation 仍 NOT VERIFIED。其余 list/map/dataset/raster/isDirty/path guardrail/GAP-01 等 known limitations 继续保留。

### Pre-acceptance status and gate boundary（historical snapshot）

~~~text
Phase 5.10 = PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER
Phase 5.11 = NOT STARTED
Phase 5 overall = NOT COMPLETE
~~~

本报告不构成 formal acceptance；下一动作仅为 Independent Gate Keeper review。未开始 Phase 5.11 或 Phase 6。

## 44. Phase 5.10 Independent Gate Keeper Formal Acceptance（2026-09-05）

- Independent Gate Keeper 正式裁定：**FORMALLY ACCEPT — PHASE 5.10 PASS**。
- Fresh build：exit 0、0 errors、exactly 3 NU1900 environment warnings。Fresh matrix：Unit 68/68、Integration 20/20、MCPProtocol 9/9、MCPServer core 25/25、MCPTransport 7/7、Server full 41/41；failed/skipped/error 均为 0；去重后 129 Facts。
- `PythonBridgeLifecycleTests` 16/16 连续 3 轮 PASS；timeout/cancellation/crash/restart/dispose/pending/late/protocol/limits/action isolation 均覆盖，逐轮无 test bridge child。Independent Gate re-run 的 Unit 68/68、Integration 20/20、Server full 41/41 亦 PASS。
- Production registry 保持 30 unique tools；forbidden test actions 未注册；无 arbitrary Python MCP。Source、Tests、配置无非 artifact 变更；production Python Bridge lifecycle semantics 未修改。
- 当前 host 的 7 个 HTTP transport cases fresh 7/7 PASS；历史 Case H / `BLOCKED_BY_HARNESS` 记录继续保留；HTTP client-disconnect cancellation 仍 NOT VERIFIED。
- P510 root 已通过 exact ownership/canonical/reparse/containment/no-lock/no-orphan gates 后移入回收站；P59/P510 active roots absent；Pro、production/test Bridge、testhost、6511/6520 全部为 0。
- MyProject1.aprx、controlled APRX、retained fixture、marker/manifest、retained GDB 65/0 locks、shared GDB 104/0 locks 均保持不变；所有 PARTIAL/LIMITED/NOT VERIFIED/deferred/GAP-01 分类继续保留。

### Current status and next authorized boundary

~~~text
Phase 5.10 = FORMALLY ACCEPTED / PASS
Phase 5.11 = FORMALLY ACCEPTED / PASS
Phase 5 overall = FORMALLY ACCEPTED / PASS / COMPLETE
Phase 6 = NOT STARTED / AWAITING SEPARATE AUTHORIZATION
~~~

本轮完成 Phase 5.11 formal acceptance 的文档同步；未运行测试或 runtime，不进入 Phase 6。Phase 5 overall 已正式完成。

## 45. Phase 5.11 Final Acceptance Audit（2026-09-05）

- Phase 5.11 已按 Independent Gate Keeper 授权进入 IN PROGRESS；本轮只执行 repository lineage、architecture/scope、accepted evidence、safety/ownership 和 documentation consistency audit。
- 审计结论：**Phase 5.11 = PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；**Phase 5 overall = PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**。这不是 formal acceptance；Phase 6 = NOT STARTED。
- Phase 5.1 没有独立 formal-PASS 记录，DECISION-003 仍为 PROPOSED；由 5.2–5.10 的实现和接受证据覆盖的架构链将其分类为 NON-BLOCKING HISTORICAL ACCEPTANCE-RECORD / PROVENANCE GAP，不虚构 5.1 PASS。
- 实际 retained fixture identity 已从 marker 和 manifest 读取：RunId = P57_B8C6FE8E；marker = .arcgis-pro-mcp-test-owned；source/owned source/clip mask/controlled project 均 PASS，fixture 未重建、重命名、移动或修改。
- 只读安全结果：MyProject1.aprx SHA-256 = ECF25A8791219435CE9AC44F7A50B28468074ACDF96C9F55455106FB376BCD14；controlled APRX SHA-256 = 0F514C540553118C443A91C188D8D69E14EEC4EB7C33897D34A82290639AF2F3；retained GDB 65 files / 0 locks；shared Phase4Test.gdb 104 files / 0 locks；P59/P510 active roots absent；Pro/Bridge/testhost/dotnet/6511/6520 均无活动。
- 本轮未修改 Source、Tests、Tools、schema、configuration、Word、MyProject1.aprx、Phase4Test.gdb、retained fixture 或历史输出；完整审计见 [PHASE_05_11_FINAL_ACCEPTANCE.md](phases/PHASE_05_11_FINAL_ACCEPTANCE.md)。

## 46. Phase 5.11 and Phase 5 Overall Independent Gate Keeper Formal Acceptance（2026-09-05）

- Independent Gate Keeper 正式裁定：**FORMALLY ACCEPT — PHASE 5.11 PASS**；**FORMALLY ACCEPT — PHASE 5 OVERALL PASS**。
- Phase 5.2–5.10 的实现/验证链闭环；Phase 5.8、5.9、5.10 已有独立 formal acceptance；Phase 5.11 的 lineage、architecture、scope、evidence、safety 和 consistency audit 已完成。
- Phase 5.1 仍准确记录为 architecture/preflight complete、无 standalone independent formal-PASS record；DECISION-003 仍为 PROPOSED。该历史 acceptance-record/provenance gap 为 NON-BLOCKING，不补写虚假旧 PASS。
- 正式接受依据包括：source health、external native ArcPy、owned FileGDB、P58_TestPolygons、P58_ClipMask、controlled APRX、explicit project reopen、owned datasource、selection/visibility baseline、cleanup/project cleanup probe、manifest 和 no-orphan process 均 PASS；Build 0 errors / 3 NU1900；Unit 68/68、Integration 20/20、MCPProtocol 9/9、MCPServer 25/25、MCPTransport 7/7、Server 41/41；Bridge lifecycle 16/16 连续 3 轮。
- Production scope 保持 single persistent NDJSON Bridge、ProcessManager lifecycle/correlation/generation/timeout/cancel/recovery/limits/action isolation、5 production Python tools 和 30 unique MCP tools（Native 21 + GP 4 + Python 5）；arbitrary Python MCP 未实现且禁止。
- 实际 retained RunId = P57_B8C6FE8E；retained GDB 65/0 locks；shared Phase4Test.gdb 104/0 locks；MyProject1.aprx、controlled APRX、fixture、历史输出未修改；P59/P510 active roots、Pro/Bridge/testhost/dotnet/6511/6520 均无活动。
- 所有 PARTIAL、LIMITED IMPLEMENTATION、NOT VERIFIED、BLOCKED_BY_HARNESS、deferred、GAP-01、P57 naming debt 和历史 TEST DEFECT 均继续保留；production Python Bridge lifecycle semantics 未修改。

~~~text
Phase 5.11 = FORMALLY ACCEPTED / PASS
Phase 5 overall = FORMALLY ACCEPTED / PASS / COMPLETE
Phase 6 = NOT STARTED
~~~

本次只同步 Independent Gate Keeper 的正式裁定；不启动 Phase 6。

## 47. Phase 6 Entry Preflight（2026-09-05，安装前历史快照）

- Phase 5.11 与 Phase 5 overall 已 FORMALLY ACCEPTED / PASS / COMPLETE；本轮按 Independent Gate Keeper 授权进入 Phase 6 Entry Preflight，Phase 7 不开始。
- Repository-only preflight 已完成并获 Independent Gate Keeper 正式接受为 **Phase 6 Entry Preflight = PASS**；Phase 6.1 Cursor Availability 也已正式接受为 **PASS**。当前结论为 **Phase 6 = IN PROGRESS / READY FOR CURSOR MCP CONFIGURATION**，首个客户端为 **Cursor**，Cursor configuration = **NOT STARTED / READY TO START**。此前的 **BLOCKED_BY_UNDEFINED_ACCEPTANCE** 已对首个客户端 gate 解决；安装前的 **BLOCKED_BY_EXTERNAL_CLIENT** 已对 Cursor availability 解决；更广泛 Phase 6 breadth 仍待后续明确验收。
- 当时的安装前 probe 未发现 Claude Desktop、Cursor、DeepSeek Harness 的命令、常见安装路径、配置路径和运行进程；6511/6520 无 listener。没有读取配置内容、secret/token 或环境变量值。Cursor 后续可用性结果见第 49 节。
- 仓库仅有 MCP Server-side capability：loopback 127.0.0.1:6520/mcp、initialize/tools/list/tools/call handler、30-tool registry；没有 MCP client caller、client config wizard、provider SDK、credential store 或 conversation loop。
- Phase 5 server-side accepted evidence 不替代真实外部客户端证据；该入口预检快照中未安装、登录、配置或连接客户端，未启动 ArcGIS Pro/Bridge/Server，未调用工具。
- Phase 6 入口报告：[PHASE_06_ENTRY_PREFLIGHT.md](phases/PHASE_06_ENTRY_PREFLIGHT.md)。当前下一动作是等待 Phase 6.1 Cursor availability 独立复核；Cursor 配置仍需单独授权，Phase 7 保持 NOT STARTED。

## 48. Phase 6 Entry Preflight Formal Acceptance and Cursor First-Client Decision（2026-09-05，安装前决策记录）

- Independent Gate Keeper 正式裁定：**FORMALLY ACCEPT — PHASE 6 ENTRY PREFLIGHT PASS**。
- 首个 Phase 6 客户端确定为 **Cursor**；第一客户端 gate 采用 client-to-server MCP 边界，不要求 provider-mediated natural-language agent behavior。
- 已接受的第一客户端 gate：识别 Cursor 版本/配置面；配置现有 `http://127.0.0.1:6520/mcp` Streamable HTTP endpoint（不含 secrets）；获取 Cursor 自身的 initialize/session 与精确 `tools/list=30` fresh evidence；执行一个授权的安全只读 `tools/call`。真实 GIS mutation 另需独立授权，并限于 owned/controlled target。
- 当前状态：Phase 6 Entry Preflight = **FORMALLY ACCEPTED / PASS**；Phase 6 = **IN PROGRESS / BLOCKED_BY_EXTERNAL_CLIENT**；Cursor configuration = **NOT STARTED**；Phase 7 = **NOT STARTED**。
- 原 **BLOCKED_BY_UNDEFINED_ACCEPTANCE** 仅保留为 pre-decision historical evidence，现对首个 Cursor gate 标记为 **RESOLVED FOR FIRST-CLIENT GATE**；Phase 6 overall 不因 Cursor 单客户端通过而自动 PASS，三类命名客户端的 breadth 需后续显式 reconcile。
- 该节记录安装前的决策边界：当时仍未安装/下载/配置 Cursor，未创建 `.cursor/mcp.json`，未登录、访问凭据、调用网络/model/工具、启动 ArcGIS Pro/Bridge/Server、运行 Build/Test 或修改代码/测试/配置/Word/fixture/受保护资产。后续授权安装结果见第 49 节。

## 49. Phase 6.1 Cursor Availability（2026-09-05）

- 授权范围仅为 Cursor availability gate。Winget 官方包安装已成功完成：`Anysphere.Cursor`，版本 `3.19.7`，source `winget`。
- Fresh evidence：可执行文件为 `<user-home>\AppData\Local\Programs\Cursor\Cursor.exe`；File/ProductVersion=`3.19.7`；SHA-256=`9FE9867E4D697774D21A05E1D2216A79C02A24AF1DA64072C57C3A8AD1CF8BEE`；卸载注册信息为 `Cursor (User)` / `3.19.7` / `Anysphere`。
- Winget、Cursor、CursorUserSetup 当前均未运行；6511/6520 无监听。`cursor` 命令未进入当前 shell PATH，但完整可执行路径已确认。未读取任何配置内容。
- Independent Gate Keeper 已正式裁定：**FORMALLY ACCEPT — PHASE 6.1 CURSOR AVAILABILITY PASS**。当前结论：**Cursor availability = FORMALLY ACCEPTED / PASS**；Phase 6.2 Cursor MCP Configuration File = **FORMALLY ACCEPTED / PASS**；**Cursor client load/connection = NOT STARTED / READY TO START**；Phase 6 = **IN PROGRESS / READY FOR CURSOR CLIENT LOAD**；Phase 7 = **NOT STARTED**。
- 未启动 Cursor、未登录、未创建 `.cursor/mcp.json`、未连接 `127.0.0.1:6520/mcp`、未调用工具、未运行 Build/Test，未修改 Source/Tests/config/Word/fixture/受保护资产。详细报告：[PHASE_06_1_CURSOR_AVAILABILITY.md](phases/PHASE_06_1_CURSOR_AVAILABILITY.md)。

## 50. Phase 6.2 Cursor MCP Configuration File（2026-09-05）

- 按 Independent Gate Keeper 授权，仅创建项目级 `D:\ArcGIS-Pro-MCP\.cursor\mcp.json`；创建前确认文件不存在，未覆盖既有配置。
- 配置严格为单一 `arcgis-pro-mcp` server 和 `http://127.0.0.1:6520/mcp` URL；无 headers/env/token/credentials/commands/stdio/SSE/second endpoint/provider/model settings。
- Fresh validation：strict JSON parse PASS；top-level/server/entry keys 精确匹配；size 97 bytes；SHA-256=`C5CF47ED4759FDBF391838C112971BAB5C6A3F6BD135F846675AD708C5AD135B`；global/user MCP configs 仍 absent，且未读取无关设置内容。
- Independent Gate Keeper 已正式裁定：**FORMALLY ACCEPT — PHASE 6.2 CURSOR MCP CONFIGURATION FILE PASS**。当前结论：**Cursor MCP configuration file = FORMALLY ACCEPTED / PASS**；**Cursor client load/connection = NOT STARTED / READY TO START**；Phase 6 = **IN PROGRESS / READY FOR CURSOR CLIENT LOAD**；Phase 7 = **NOT STARTED**。
- 未启动 Cursor、未登录、未连接 MCP Server、未执行 initialize/tools/list/tools/call，未启动 ArcGIS Pro/Bridge/Server，未运行 Build/Test，未修改生产 Source/Tests/Word/fixture/受保护资产。详细报告：[PHASE_06_2_CURSOR_MCP_CONFIGURATION.md](phases/PHASE_06_2_CURSOR_MCP_CONFIGURATION.md)。

## 51. Phase 6.3 Cursor Client Load / Configuration Recognition（2026-09-05）

- 按授权可见启动 Cursor，使用工作区 `D:\ArcGIS-Pro-MCP`；主窗口标题为 `ArcGIS-Pro-MCP - Cursor`，工作区加载已观察到。
- Cursor 进程均来自已安装路径；主窗口 PID=`12148`；ArcGISPro/dotnet=0；6511/6520 无监听。Cursor 保持打开，未执行 UI 点击或键盘输入。
- 初始仅检查三个 `anysphere.cursor-mcp` 文件时未发现 `arcgis-pro-mcp`、`127.0.0.1:6520/mcp`、`mcpServers` 或 connection-term；该子集后来确认不完整。对精确当前会话文件的补充扫描观察到：`cursor-sentry-events.log` 第 2–5 行含 server name/endpoint，且第 4 行含 `mcpServers`；`Mcp FileSystem Writer.log` 第 5–7 行含 server name 与 recognition/registration 语义；`workbench.mcp.oauth.log` 第 1、4 行含 server name，第 2–3 行含 error/offline 语义。仅输出脱敏语义分类，不输出原始日志或凭据。
- 当前结论：**Phase 6.3 Cursor Client Load / Configuration Recognition = FORMALLY ACCEPTED / PASS**；**Cursor MCP connection/initialize/tools-list = NOT VERIFIED / READY TO START**；Phase 6 = **IN PROGRESS / READY FOR CURSOR MCP CONNECTION VERIFICATION**；Phase 7 = **NOT STARTED**。Cursor 配置文件本身仍为 FORMALLY ACCEPTED / PASS。
- 若需要 onboarding/terms/login/provider/permission UI，必须由用户直接处理；本轮不读取凭据、不自动点击或输入。详细报告：[PHASE_06_3_CURSOR_CLIENT_LOAD.md](phases/PHASE_06_3_CURSOR_CLIENT_LOAD.md)。

## 52. Phase 6.4 Cursor MCP Connection / Initialize / Tools List（2026-09-05）

- 按 Independent Gate Keeper 授权，仅启动 retained controlled APRX `Phase58Controlled.aprx`，未打开 `MyProject1.aprx`，未写入或清理 `Phase4Test.gdb`，未执行任何 `tools/call`。
- 生产受控 runtime PID `24636` 正常打开 controlled APRX；以 process-scoped verification auto-start 环境变量启动后，6520 listener 出现，6511 保持未监听。随后仅优雅关闭该受控 Pro，post-check ArcGIS Pro/dotnet/6511/6520 均为 0。
- Cursor 当前会话日志观察到 server name、exact endpoint、`mcpServers` 及 initialize-related semantic activity；但未暴露可验证的成功 connection/session、initialize result、`tools/list` 或 exact `tools/list=30`。未执行 `tools/call`。
- Gate classification：**Phase 6.4 = BLOCKED_BY_CLIENT_LOG_VISIBILITY**；**Cursor MCP connection/initialize/tools-list = NOT VERIFIED**。这不是 server/runtime failure；受控 runtime 已到达 6520。详细报告：[PHASE_06_4_CURSOR_MCP_CONNECTION.md](phases/PHASE_06_4_CURSOR_MCP_CONNECTION.md)。
- Post-check：config hash、controlled APRX、`MyProject1.aprx`、marker/manifest、shared/retained GDB inventory and locks 均保持不变；retained RunId 仍为 `P57_B8C6FE8E`。

### Phase 6.4 minimal workspace-launch diagnostic（2026-09-05）

- 本机 `Cursor.exe --help` 未显示帮助而启动 Cursor，诊断实例已优雅关闭。随后尝试显式 `--new-window`；实际参数含带空格的工作区参数，窗口仍为 `Cursor Agents`，新工作区未被证明加载。
- 主窗口 PID `29016` 的 `CloseMainWindow()` 在 30 秒有界等待内未退出；未强制终止。按 Gate 规则当前分类为 **BLOCKED_BY_CLIENT_PROCESS**，未启动 controlled runtime，未继续重试。
- 最终 post-check：ArcGIS Pro/dotnet/6511/6520=0；配置、protected APRX、controlled APRX、marker/manifest 与 GDB inventory/locks 未变化。此前 **BLOCKED_BY_CLIENT_LOG_VISIBILITY** 结果保留为历史证据。

### Phase 6.4 Gate Keeper workspace correction and Pro-first recovery（2026-09-05）

- Gate Keeper 确认会话 `20260905T143455` 不是 empty-window：workspaceId 非空、`workspacePaths=d:\ArcGIS-Pro-MCP`、`projectServers=1`，并识别 `project-0-ArcGIS-Pro-MCP-arcgis-pro-mcp`。
- 最终 Pro-first 会话 `20260905T144521` 先启动 controlled Pro PID `24088` 到达 6520，再启动 Cursor PID `26032`；新会话保持工作区和 project server 发现，但没有可审计 connection success、initialize/session success、`tools/list` 或 exact `tools/list=30`。
- 最终结果为 **BLOCKED_BY_CLIENT_LOG_VISIBILITY**；未执行 `tools/call`。两者优雅退出，post-check 无 Cursor/ArcGIS Pro/dotnet/6511/6520，保护资产、fixture 与 GDB inventory/locks 未变化。此前 **BLOCKED_BY_CLIENT_PROCESS** 保留为历史 launcher 诊断结果。

### Phase 6.4 controlled recovery attempt（2026-09-05）

- 按同一 Gate 授权，先优雅关闭此前 Cursor 主窗口 PID `12148`，确认 Cursor=0 后先启动受控 APRX，再启动 Cursor；未强制终止。
- Controlled Pro PID `28284` 以 process-scoped verification auto-start 到达 6520，6511 保持未监听。随后 Cursor PID `6136` 以 `D:\ArcGIS-Pro-MCP` 参数启动，产生新日志会话 `20260905T142036`。
- 新会话未提供项目 server identity、endpoint、可验证 connection success、successful initialize/session、`tools/list` 或 exact `tools/list=30` 证据；未执行 `tools/call`。因此仍为 **BLOCKED_BY_CLIENT_LOG_VISIBILITY**。
- 两个受控应用均已优雅关闭；post-check Cursor/ArcGIS Pro/dotnet/6511/6520=0，保护资产、fixture、GDB inventory/locks 和配置哈希不变。

## Phase 6 Codex P0 Project Configuration Recognition（2026-09-05）

- 客户端优先级已迁移为 Codex=P0 首选必验、Cursor=P1 必验、DeepSeek Harness=P1 必验、Claude Desktop=P2 可选。旧 Cursor 6.1–6.3 PASS 与 6.4 `BLOCKED_BY_CLIENT_LOG_VISIBILITY` 保留；Phase 6 不宣称 PASS，Phase 7 仍 `NOT STARTED`。
- 创建最小项目级 `.codex/config.toml`，仅包含 `[mcp_servers.arcgis-pro-mcp]` 与 loopback URL；未修改全局 Codex 配置，未写入 token、credential、provider 或 secret。
- Codex CLI=`codex-cli 0.153.0`；`codex mcp list` 明确列出 `arcgis-pro-mcp`、`http://127.0.0.1:6520/mcp`、`enabled`、`Auth=Unknown`，故 Codex project configuration recognition = **PASS**。
- 配置 SHA-256=`2E4A919F011A0D8F5F175B7F85C0E5236C0923D67BDC632EF331221E17AD55E2`，文件大小 63 bytes。
- 本 Gate 未启动 Codex MCP connection、ArcGIS Pro、Cursor 或 dotnet；Codex connection/initialize/tools-list/tools-call 仍 `NOT VERIFIED`。6520 当前无监听，不能以配置识别代替 Runtime 证据。

## Phase 6 Codex P0 MCP Connection / Initialize / Exact Tools List / Safe Read-only Call（2026-09-05）

- Independent Gate Keeper 已授权本 Gate。按 Pro-first 顺序启动 retained owned controlled APRX `Phase58Controlled.aprx`，controlled ArcGIS Pro PID=`4064`，6520 listener=`1`，6511 listener=`0`；完成后优雅关闭，最终 ArcGISPro/dotnet/6511/6520 均为 `0`。
- 新 Codex 客户端面：Codex CLI `0.153.0`；命令为 `codex exec --json --ephemeral --approve-for-me -C D:\ArcGIS-Pro-MCP -`；cwd=`D:\ArcGIS-Pro-MCP`；使用项目 `.codex/config.toml`；配置 SHA-256=`2E4A919F011A0D8F5F175B7F85C0E5236C0923D67BDC632EF331221E17AD55E2`。项目受信任状态未由客户端单独暴露；未出现信任阻断。
- Fresh Codex client evidence：server=`arcgis-pro-mcp`；connection=`SUCCEEDED`；initialize/equivalent session=`SUCCEEDED`（客户端完成成功的 MCP server tool call）；`tools/list`=`30 unique tools`；完整工具名称集合记录于专门报告。唯一调用为 `mcp__arcgis_pro_mcp__ping`，arguments=`{}`，result=`{"content":[{"type":"text","text":"pong"}],"isError":false}`。
- Raw MCP `initialize` response envelope 未单独暴露于 Codex exec JSONL；因此本 Gate 严格分类为 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**，不把客户端声明升级为 Formal PASS。未执行任何 mutation。
- Post-check：`MyProject1.aprx` SHA-256=`ECF25A8791219435CE9AC44F7A50B28468074ACDF96C9F55455106FB376BCD14`；retained marker SHA-256=`EC5CA5EFA9AA020698BD518395FF369B5726AC4641D736479299CC024ABE643B`；manifest SHA-256=`390B3F30B5E50156147DDD6E120E1392BBF6E80146B839131438223411DF820A`；retained GDB=`65 files / 0 locks`；shared `Phase4Test.gdb`=`104 files / 0 locks`，均未改变。
- 当前：**Phase 6 = IN PROGRESS / AWAITING CODEX P0 GATE REVIEW**；Cursor 6.1–6.3 PASS 与 6.4 `BLOCKED_BY_CLIENT_LOG_VISIBILITY` 保留为历史证据；Phase 7=`NOT STARTED`。专门报告：[PHASE_06_CODEX_P0_MCP_CONNECTION_GATE.md](phases/PHASE_06_CODEX_P0_MCP_CONNECTION_GATE.md)。

## Phase 6 Codex P0 MCP Connection Gate Formal Acceptance（2026-09-05）

- Independent Gate Keeper 正式裁定：**FORMALLY ACCEPT — Codex P0 MCP Connection / Initialize-equivalent Session / Exact Tools List / Safe Read-only Call Gate = PASS**。
- 接受依据：新 Codex CLI `0.153.0` 会话、Pro-first controlled APRX、`arcgis-pro-mcp` connection=`SUCCEEDED`、initialize-equivalent session=`SUCCEEDED`、30 个唯一工具名称、同一客户端 `ping({})` 返回 `pong` 且 `isError=false`。
- `raw initialize` envelope 未由 Codex CLI JSONL 单独暴露，继续记录为非阻塞限制；该 Formal PASS 不扩展到其他 tools/call 或 mutation。
- 当前唯一下一 Gate：**Cursor P1 MCP Connection / Initialize / Exact 30 Tools / Safe Read-only Call Recovery**。Phase 6 仍 `IN PROGRESS`，Phase 7 仍 `NOT STARTED`。

## Phase 6 Cursor P1 MCP Connection Gate（2026-09-05）

- Pro-first 顺序完成：controlled Pro PID=`29764` 先达到 6520，6511 保持未监听；随后新 Cursor 会话启动。完成后 Cursor/ArcGISPro/dotnet 与 6511/6520 均为 `0`。
- 实际 Cursor 客户端版本为 `3.19.13`；项目工作区 `D:\ArcGIS-Pro-MCP` 被识别，project server 数=`1`，目标 server=`arcgis-pro-mcp`。
- Cursor 当前会话日志显示目标 server 状态为 `disconnected`，未提供 successful connection、successful initialize/session、`tools/list`、exact 30-tool set 或 `tools/call` 证据。目标 server 的状态不能用内置 `cursor-app-control` 的 connected 状态替代。
- 已耗尽安全的本机只读诊断；安装目录未发现可用 Cursor MCP/agent CLI 或导出接口可代替 UI。当前分类：**BLOCKED_BY_USER_UI**；Cursor connection/initialize/tools-list/tools-call = **NOT VERIFIED**。
- 下一动作仅为用户在 Cursor 自身 MCP UI 中对 `arcgis-pro-mcp` 执行连接/重连并取得客户端证据；不修改配置、不登录、不读取凭据、不执行 mutation。专门报告：[PHASE_06_CURSOR_P1_MCP_CONNECTION_GATE.md](phases/PHASE_06_CURSOR_P1_MCP_CONNECTION_GATE.md)。
