# ArcGIS-Pro-MCP — 当前任务（CURRENT_TASK）

> **历史时点声明（D-126 项目完结批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **2026-09-10（文内最晚 ISO 日期实测；正文跨度 2026-09-03…2026-09-10）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。

## 当前 Phase

### Current Phase 7 Final Project Acceptance（2026-09-07）

Independent Gate Keeper 已正式接受 Phase 7 overall、1.0.2 planned release scope 与 implemented canonical 30-tool whole-project scope 为 **FORMALLY ACCEPTED / PASS / COMPLETE**。当前任务记录正式接受结果与 preservation boundary；不重跑 Build/Test，不启动客户端、ArcGIS Pro、Bridge、MCP、测试或 RegisterAddIn，不修改配置、registry、GIS 数据、retained fixture、installed package、历史输出或锁。

状态：Phase 7.7 overall = **FORMALLY ACCEPTED / PASS**；Phase 7.8 = **FORMALLY ACCEPTED / PASS**；Phase 7 overall = **FORMALLY ACCEPTED / PASS / COMPLETE**；1.0.2 planned release scope / implemented canonical 30-tool whole-project scope = **FORMALLY ACCEPTED / PASS / COMPLETE**；未来 `112+` expansion = **NOT IMPLEMENTED / NOT ACCEPTED**。

当前入口：[PHASE_07_FINAL_PROJECT_ACCEPTANCE.md](phases/PHASE_07_FINAL_PROJECT_ACCEPTANCE.md)。不可变 pre-acceptance candidate：[PHASE_07_FINAL_PROJECT_ACCEPTANCE_CANDIDATE.md](phases/PHASE_07_FINAL_PROJECT_ACCEPTANCE_CANDIDATE.md)。正式记录：历史归档（该 JSON 不在当前工作树，入口以其替代件为准）。Phase 7.8 acceptance：[PHASE_07_8_FINAL_HANDOFF_CANDIDATE.md](phases/PHASE_07_8_FINAL_HANDOFF_CANDIDATE.md)。发布说明：[RELEASE_NOTES_1.0.2.md](RELEASE_NOTES_1.0.2.md)。

Final handoff 保留 installed/source/manifest `1.0.2` exact match，269548 bytes，SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`；canonical production tools=`30` distinct，`mcp_auth` excluded；Codex P0、Cursor P1 和 DeepSeek Harness P1 已正式接受，Claude Desktop 为 optional/template-only。

Build/Test、27 项 handoff manifest、current ledger `5/5 PASS`、historical rollback `7/7 PASS`、32 项 pre-acceptance index 和 final acceptance record 均已交叉索引；raw envelope、PARTIAL/LIMITED、NOT VERIFIED、BLOCKED_BY_HARNESS、clean-machine、`ArcGISProject.isDirty`、HTTP cancellation、DeepSeek 当前 not-running/reconnection-not-verified 和 `select_layer` no-OID limitation 不变。已接受范围已完成；未来 `112+` expansion 不属于本次接受范围。
- Final read-only state audit (`2026-09-07T22:54:53.5312216+08:00`) records the accepted historical DeepSeek identity as `PID 20424 / node / port 3080`; current PID `20424` is absent and port `3080` is clear. Cause/actor is **NOT VERIFIED / external user-owned state change**. The launcher shortcut and five profile/config file hashes are unchanged; no credentials were read or output, and no restart or fresh connection was performed.

## Current separate workstream: One-click Deployment Share Package（2026-09-10）

状态：**INDEPENDENT GATE PASSED / AUTHORIZED FOR USER REAL-MACHINE TRIAL**。Independent Gate Keeper 的有限决定仅覆盖 r5 实现与本机隔离自动化验证；真实安装、真实客户端连接、clean-machine 和公开发布仍 `NOT VERIFIED`。这是围绕已接受 `1.0.2` artifact 的独立部署体验工作，不重新打开 Phase 7，也不代表新的 Phase 已开始。

- 当前候选输出：`Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip`，`331679` bytes，SHA-256=`3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652`；同名 `.sha256` 与实际 ZIP 一致。上一版 r4 candidate 保留不变：`330602` bytes，SHA-256=`AE9C5EF2421D4E73F8BA62AEA5FBFEF2B4EFF0DA7D2EF24B01EED93D3103A134`；r3 candidate 保留不变：`329568` bytes，SHA-256=`7EEC03C0E0DAD1F3363147FE614B72C0CB41DB3A80262CC76453F4AAC8B1E3EA`；r1 被 Gate Keeper 退回但保留不变：`316207` bytes，SHA-256=`3303E02CFAC50EB02B74F4C0B4BDA77C3BE2788607920EE1402B43B19102D7B1`；原 accepted `Release/ArcGIS-Pro-MCP-1.0.2-Windows-x64.zip` 保持不变。原始 r2 review hash=`CC1CC37C8B00918313203E45656183A9FE6656D0E9D1FC664D096349D45AFD53` 与后续被覆盖 r2 hash=`33AC18A10E2DF4EA7D0D0BE646C777BC2EB535D8C552F1EA846B75956936DABF` 均按历史/候选 lineage 区分保留。
- 复用 payload：`1.0.2`、`269548` bytes、SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`；production endpoint/tool contract 仍为 `http://127.0.0.1:6520/mcp` / `30`。
- r5 已完成并保留 r4/r3 的 GUI/UTF-8/recovery 基础，并修复独立复核发现的 Windows PowerShell 5.1 入口兼容性：package/test scripts 加入 UTF-8 BOM，package `client-config.ps1` 和 test harness 的 SHA-256 改为纯 .NET 实现，test harness 同步子进程改用 ProcessStartInfo 保留实际退出码。随后 test harness 又加入显式 UTF-8 encoded bootstrap、父进程 stdout/stderr UTF-8 decode、dot-source scope preservation、顶层中文路径 U+FFFD 断言，以及失败时清理前的完整 owned-temp artifact snapshot；这些均为 test/evidence-only changes，生产 Python Bridge lifecycle semantics 未修改。Fresh r5 harness=`122/122 assertions PASS`；精确 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\OneClickDeployment\one-click-deployment.tests.ps1` 返回 `EXIT_CODE=0`；ZIP/extracted-root audit=`PASS`（23 files）；preset GUI smoke=`PASS_SIMULATED_PRESET_ONLY`；final extracted-package real read-only button callbacks=`PASS_REAL_READ_ONLY_CALLBACKS`（3 screenshots、中文路径 InstallRoot + GUID、中文消息无替换字符、controls/target/no-dialog/worker exits PASS）；r4 的失败/候选证据仍保留。
- r1 rejected evidence is preserved：GUI repeated-start/`执行中`/empty-target/null-array failure and full stack references remain under the persistent r2 evidence `prior-failure-evidence` folder and historical docs; it is not used as current PASS。
- Fresh final baseline：solution Build `0 errors / 3 NU1900`；Unit `233 PASS / 3 SKIP / 0 FAIL`（236 total）；Integration `23/23`；Server `41/41`。PS5.1 package verifier=`PASS`（`one-click-1.0.2-r5`、23 files、payload `269548` bytes / SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`）。尚未完成/不宣称：real installation、real client mutation、ArcGIS/MCP runtime、clean-machine acceptance、真实用户 GUI click-through；均为 `NOT PERFORMED / NOT VERIFIED`。生产 registry、Bridge lifecycle、GIS/fixture 与 historical evidence 不变。
- r5 local evidence：`.runtime/one-click-deployment-r5/run-20260910-044724-cf90381b4f1c4b6b82d881d25cc7888b`；Independent Gate Keeper fresh evidence：`.runtime/one-click-deployment-r5/run-20260910-052628-6f6d5d15ae56481092d378c74dd8e856`；fresh baseline：`.runtime/one-click-deployment-r5/baseline-20260910-032900`；PS5.1 entry evidence：`.runtime/one-click-deployment-r5/ps51-entry-validation-20260910-044723-unicode-fix`；package verifier：`.runtime/one-click-deployment-r5/ps51-package-verifier-20260910-032800`；protected/process/listener post-check：`run-20260910-044724-cf90381b4f1c4b6b82d881d25cc7888b/49-post-check.json`。独立审批记录：[ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md](ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md)。r4/r3 evidence roots 与 r2 roots remain historical and distinct；r5 失败入口诊断日志及最近一次完整 failure-artifacts snapshot 未删除。
- 下一步仅为用户控制的实机试用与证据采集：[ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md](ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md)。本轮不执行安装/注册、不修改真实客户端、不 commit/push、不开始新的 Phase；用户提交实机日志、包哈希、tools/list、ping、只读工具与恢复证据后再进行下一次独立审批。

### Historical Phase 7.7.4.4 Multi-client Consolidation（2026-09-07）

Independent Gate Keeper 已正式接受 Phase 7.7 Entry Preflight、7.7.1、7.7.2、7.7.3、7.7.4.1、7.7.4.2 与 7.7.4.3 为 **FORMALLY ACCEPTED / PASS**。Phase 7.7.4.4 Multi-client Consolidation 已按授权完成，当前结论为 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**。

7.7.4.1 的前两次 Codex approval refusal 与第三次成功 remediation 证据、7.7.4.2 Cursor P1 的 fresh client evidence 与正式验收、7.7.4.3 DeepSeek P1 的 fresh evidence 与正式 acceptance、以及 7.7.4.4 的并列汇总均已保留在专项报告。7.7.4.4 只读取并列审计三类 fresh client evidence、carried Build/Test、配置/资产/进程 post-check 和历史限制；本轮未重新启动或操作任何 client/ArcGIS runtime，未修改配置、production registry、MyProject1.aprx、Phase4Test.gdb、retained fixture 或 DeepSeek PID `20424`。

7.7.4.1 approval-remediation run 的新鲜 Codex CLI session 暴露 `mcp__arcgis_pro_mcp__`；`ping({})`、`python_bridge_ping({})` 和 `get_arcgis_version({})` 均返回成功且 `isError=false`。Independent Gate Keeper 正式接受客户端初始 30 个 canonical production names（distinct 30、duplicate 0、`mcp_auth` absent）为 initialize-equivalent live discovery，同时保留 Codex CLI 不暴露 raw envelope 的非阻断限制。7.7.4.2 Cursor evidence root 为 `.runtime/phase77_4_2_cursor/run_B11B3BCBB3AC40BC84019204D9CFCD68`；Cursor 3.19.13 的 project snapshot 为 `connected`、`toolCount=30`，同一客户端 `ping({})` 返回 `pong` 且 `isError=false`。Cursor raw initialize/tools-list/name envelope 未暴露，但该连接已由 Independent Gate Keeper 接受为 **FORMALLY ACCEPTED / PASS**。

状态：7.6.1 = **FORMALLY ACCEPTED / PASS**；7.6.2 = **FORMALLY ACCEPTED / PASS**；7.6 overall = **FORMALLY ACCEPTED / PASS**；7.7 Entry Preflight = **FORMALLY ACCEPTED / PASS**；7.7.1 = **FORMALLY ACCEPTED / PASS**；7.7.2 = **FORMALLY ACCEPTED / PASS**；7.7.3 = **FORMALLY ACCEPTED / PASS**；7.7.4.1 = **FORMALLY ACCEPTED / PASS**；7.7.4.2 = **FORMALLY ACCEPTED / PASS**；7.7.4.3 = **FORMALLY ACCEPTED / PASS**；7.7.4.4 = **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；7.7.5+ = **NOT STARTED**。

当前入口：[PHASE_07_7_4_4_MULTI_CLIENT_CONSOLIDATION.md](phases/PHASE_07_7_4_4_MULTI_CLIENT_CONSOLIDATION.md)。7.7.4.3 formal acceptance 记录见 [PHASE_07_7_4_3_DEEPSEEK_P1_FRESH_CLIENT_GATE.md](phases/PHASE_07_7_4_3_DEEPSEEK_P1_FRESH_CLIENT_GATE.md)；7.7.4.2 formal acceptance 记录见 [PHASE_07_7_4_2_CURSOR_P1_FRESH_CLIENT_GATE.md](phases/PHASE_07_7_4_2_CURSOR_P1_FRESH_CLIENT_GATE.md)。

7.7.1 已正式接受：Build `0 errors / 3 NU1900`；focused `152 PASS / 3 SKIP`；Unit `232 PASS / 3 SKIP`；Integration `23/23`；Server `41/41`；ProductionToolContract `11/11`；registry `30`、`mcp_auth` excluded；package-only exact audit passed、registration `SKIPPED`；protected/process/listener/client-config post-check unchanged。7.7.2、7.7.3、7.7.4.1、7.7.4.2 与 7.7.4.3 已正式接受；7.7.4.4 文档/evidence consolidation 已完成，当前等待 Independent Gate Keeper，7.7.5+ 未开始。

7.7.2 formal acceptance evidence：owned transaction root `phase77_2_AB60AFC410CA47719330397DFBFC5012`；release ledger `PASS`，官方 `RegisterAddIn.exe /s` exitCode=`0`；installed 1.0.2 为 `269548` bytes、SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`，与 source candidate 完全匹配。受保护资产与外部 DeepSeek 状态未变化，ArcGIS/Bridge/client/MCP 未启动。该阶段已由 Independent Gate Keeper 正式接受。

7.7.3 已正式接受：installed UI Status 与 Diagnostics、managed-log/export/privacy/manifest、graceful close、official AssemblyCache loaded-module chain 和 fresh runtime `tools/list=30` 均按授权范围记录。7.7.4.1 approval-remediation 的 Codex P0 fresh client 已取得 `ping=pong`、`python_bridge_ping=pong` 和 ArcGIS 3.5.0 native read；Bridge 子进程路径由 instrumentation 确认为官方 AssemblyCache。Gate Keeper 已正式接受 30-name catalog 为 initialize-equivalent live discovery，raw envelope limitation 保留；7.7.4.2 Cursor P1 已由 Independent Gate Keeper 正式接受。

7.7.4.3 DeepSeek P1 fresh evidence 已由 Independent Gate Keeper 正式接受：fresh conversation 的 exact raw exposed tool set 为 30 个、distinct 30、duplicate 0，全部属于 `arcgis-pro-mcp`；同一 session 唯一调用 `mcp__arcgis-pro-mcp__ping({})` 返回 `pong`、`isError=false`。Session ZIP 核对为 exactly one `tool/call` 与 one matching `tool/result`；UI 未暴露 raw initialize envelope，限制已如实保留。7.7.4.4 已在不启动运行时的条件下完成并列审计、canonical 30-tool normalization 和 protected/config/process post-check。

7.7.4.4 当前结论为 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；不进入 7.7.5。

### Historical Phase 7.6.1 Release Portability Foundation（2026-09-06）

Independent Gate Keeper 已正式接受 Phase 7.6 Entry Preflight 为 **FORMALLY ACCEPTED / PASS**。当前授权任务是 Phase 7.6.1 Release Portability Foundation，状态为 **IN PROGRESS / PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**。

当前只允许：RuntimePathResolver/包内 Bridge artifact/manifest audit、active-Pro Python derivation、LocalApplicationData runtime-root policy、1.0.2 candidate identity、safe-code 和 focused regression evidence。不得进入 7.6.2 或 7.7，不得安装/注册，不得启动 ArcGIS Pro、Bridge、client 或 MCP runtime，不得创建 final README/user guide/one-click wrapper。

状态：Phase 7.5.3 = **FORMALLY ACCEPTED / PASS**；Phase 7.5 = **FORMALLY ACCEPTED / PASS**；Phase 7.6 Entry Preflight = **FORMALLY ACCEPTED / PASS**；Phase 7.6.1 = **IN PROGRESS / PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 7.6.2/7.7+ = **NOT STARTED**。

Fresh baseline：Build `0 errors / 3 NU1900`；focused `65 PASS / 3 SKIP`（total `68`）；Unit `216 PASS / 3 SKIP`（total `219`）；Integration `23/23`；Server `41/41`；production registry `30`；1.0.2 package audit `20` entries and exact normalized Bridge logical path `Install/PythonBridge/bridge_runner.py`。Release token adversarial cases and exact two-level runtime-root containment passed. 已安装 1.0.1 copy 保持 protected，source/installed diff 为 `EXPECTED_PENDING_PHASE_7_7`。完整记录见 [PHASE_07_6_1_RELEASE_PORTABILITY_FOUNDATION.md](phases/PHASE_07_6_1_RELEASE_PORTABILITY_FOUNDATION.md)。

### Historical Phase 7.6 User Documentation and One-click Materials — Entry Preflight（2026-09-06）

Phase 7.4 report: [PHASE_07_4_MULTI_CLIENT_HANDOFF.md](phases/PHASE_07_4_MULTI_CLIENT_HANDOFF.md)。Independent Gate Keeper 已正式接受 Phase 7.5.3 与 Phase 7.5 overall 为 **FORMALLY ACCEPTED / PASS**（限授权实现与自动化证据范围）。当前只执行 Phase 7.6 Entry Preflight / audit-design。

当前状态：Phase 7.5.3 = **FORMALLY ACCEPTED / PASS**；Phase 7.5 = **FORMALLY ACCEPTED / PASS**；Phase 7.6 Entry Preflight = **IN PROGRESS / PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 7.7+ = **NOT STARTED**。

Formal 7.4 acceptance basis: Build `0 errors / 3 NU1900`；focused client policy `18/18`；Unit `108/108`；Integration `20/20`；Server `41/41`。实际安装目标 Add-in ID 为 `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}`；历史 registered-package backup prefix `7CEB2A...` 保留为原始截断证据，`A97AE2...` 仍不是安装 GUID。

Formal 7.4 acceptance basis: Build `0 errors / 3 NU1900`；focused client policy `18/18`；Unit `108/108`；Integration `20/20`；Server `41/41`。实际安装目标 Add-in ID 为 `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}`；历史 registered-package backup prefix `7CEB2A...` 保留为原始截断证据，`A97AE2...` 仍不是安装 GUID。

- Phase 7.1/7.2/7.3/7.4 = **FORMALLY ACCEPTED / PASS**；Phase 7.5/7.5.1/7.5.2/7.5.3 = **FORMALLY ACCEPTED / PASS**；Phase 7.6 Entry Preflight = **IN PROGRESS / PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 7.7+ = **NOT STARTED**。
- 正式支持只覆盖 ArcGIS Pro 3.5/current host；其他版本保持 `NOT_TESTED` 或 `UNSUPPORTED_BY_CURRENT_POLICY`。

Phase 7.6 Entry Preflight 只允许盘点、冲突分类、可移植性审计、文档架构和 one-click 行为设计；本轮不实现脚本、不重写用户文档为已完成行为、不打包/安装/注册、不启动 runtime/client、不调用 MCP。

7.5.3 正式报告：[PHASE_07_5_3_STRUCTURED_LOGGING_DIAGNOSTIC_EXPORT.md](phases/PHASE_07_5_3_STRUCTURED_LOGGING_DIAGNOSTIC_EXPORT.md)。7.6 Entry Preflight 报告：[PHASE_07_6_USER_DOCUMENTATION_PREFLIGHT.md](phases/PHASE_07_6_USER_DOCUMENTATION_PREFLIGHT.md)。

Phase 7.5.3 已正式接受：唯一 production managed sink 已接入 Composition；日志记录补齐固定审计字段并丢弃 free-text/result/error；SelfTest 只写固定 structured records；diagnostic export 只发布三个 allowlisted artifacts，支持 bounded revalidation、hash/length manifest、traversal/reparse/existing-destination rejection 和 atomic publish。UI/runtime observation = `NOT VERIFIED`，转交 7.7。

本轮 fresh verification：Build `0 errors / 3 NU1900`；focused `15 total = 13 PASS + 2 capability-aware SKIPPED`；Unit `188/188 PASS + 2 SKIPPED (190 total)`；Integration `23/23`；Server `41/41`。两项 reparse-point/ancestor tests 因当前环境创建 directory symbolic link 返回 `IOException` 而 skipped，未宣称 reparse behavior executed PASS。F1 cleanup、F2 adversarial identifiers、protected-state post-check 均按正式接受记录保留。未启动 Phase 7.7。

7.6 preflight 当前已确认：生产 Composition 和 managed-log destination 存在 `D:\ArcGIS-Pro-MCP` 机器路径绑定，发布 ZIP 的 19 entries 不含 `bridge_runner.py`，因此 `RELEASE_PORTABILITY_BLOCKER` 必须在用户文档承诺前处理或明确阻断。

配置复核更正：此前使用 `<user-home>\.codex/.cursor` 是 **VERIFICATION_PATH_ERROR**，不是配置漂移。正确 repository-scoped 文件 `D:\ArcGIS-Pro-MCP\.codex\config.toml` 与 `D:\ArcGIS-Pro-MCP\.cursor\mcp.json` 的 hash 分别为 `2E4A919F011A0D8F5F175B7F85C0E5236C0923D67BDC632EF331221E17AD55E2` 与 `C5CF47ED4759FDBF391838C112971BAB5C6A3F6BD135F846675AD708C5AD135B`；DeepSeek hash 保持 `0BC84530A43B06A7FFE789F7C0369F3B973A264F9EA2A8D84B41206683C7ED4B`，无 sidecar，真实配置未修改。

### Historical Phase 7.1 boundary（2026-09-05）

Phase 7.1 Package and Release Identity 已由 Independent Gate Keeper **FORMALLY ACCEPTED / PASS**。Phase 7.2 Install/Uninstall/Rollback 已由 Independent Gate Keeper **FORMALLY ACCEPTED / PASS**。Phase 7.3 当前 **IN PROGRESS / PASS CANDIDATE pending review**；Phase 7.4+ **NOT STARTED**。

- Release/product `1.0.1`；first-party CLR AssemblyVersion `1.0.0.0`；FileVersion `1.0.1.0`；ProductVersion/InformationalVersion `1.0.1`；AssemblyVersion `1.0.0.0` 为有意保留的 binary compatibility identity。
- Build `0 errors / 3 NU1900`；Unit `72/72`；Integration `20/20`；Server `41/41`。
- Package-only smoke 使用 `scripts/package-addin.ps1 -SkipRegistration`，package `202887` bytes，SHA-256 `C5541E428476A14B25C983FF67F549617F64C20EF4651348DE50D4962049CA7E`；独立 ZIP 审计 `19` entries、8 个 first-party DLL、核心文件齐全、无 nested `.esriAddInX`。
- Manifest schema 为 `arcgis-pro-mcp-release-manifest-v1`；8 个 first-party DLL 的 source/ZIP hash、CLR AssemblyVersion、FileVersion 和 ProductVersion 与 manifest/策略一致；无绝对路径或用户名。
- 未调用 `RegisterAddIn.exe`；已注册副本仍为 carried-forward baseline，未更新。受保护 APRX、shared GDB、retained fixture、历史输出和未知锁未修改。

详细报告：[PHASE_07_1_PACKAGE_RELEASE_IDENTITY.md](phases/PHASE_07_1_PACKAGE_RELEASE_IDENTITY.md)。当前报告：[PHASE_07_2_INSTALL_UNINSTALL_ROLLBACK.md](phases/PHASE_07_2_INSTALL_UNINSTALL_ROLLBACK.md)。

> 客户端连接事实保持不变：Codex P0、Cursor P1 与 DeepSeek Harness P1 均已 FORMALLY ACCEPTED / PASS；Cursor raw client bindings=31，canonical production tools=30，额外 `mcp_auth` 不计入生产 Registry；DeepSeek 审计后的唯一服务工具集合为 30。Phase 6 = **FORMALLY ACCEPTED / PASS / COMPLETE**；Phase 7 = **IN PROGRESS**；Phase 7.1 = **FORMALLY ACCEPTED / PASS**；Phase 7.2 = **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**，Phase 7.3+ 尚未开始。

DeepSeek P1 专项报告：[PHASE_06_DEEPSEEK_HARNESS_P1_PREFLIGHT.md](phases/PHASE_06_DEEPSEEK_HARNESS_P1_PREFLIGHT.md)。

### Current DeepSeek MCP gate（2026-09-05）

Identity / launcher / local runtime preflight = **FORMALLY ACCEPTED / PASS**。Configuration is composed in `<user-home>\.dsh\profiles\web\cordis.patch.yml`; latest same-client DeepSeek evidence for namespace `arcgis-pro-mcp` = connection **PASS**, initialize-equivalent **PASS by same-client discovery/use**, canonical unique tools **30/30**, and `ping({}) → pong` **PASS**. The earlier process/ledger **BLOCKED_BY_USER_UI** state is retained as historical evidence. Report: [PHASE_06_DEEPSEEK_HARNESS_P1_MCP_CONNECTION_GATE.md](phases/PHASE_06_DEEPSEEK_HARNESS_P1_MCP_CONNECTION_GATE.md)。

### Current DeepSeek state reconciliation（2026-09-05）

产品身份、启动器和 local runtime 的 preflight 已由 Independent Gate Keeper 接受为 PASS；随后同一 DeepSeek Harness Web UI 的 MCP connection gate 也已接受为 PASS。`BLOCKED_BY_EXTERNAL_CLIENT / CLI_NOT_ON_PATH / RUNTIME_NOT_VERIFIED` 与 `BLOCKED_BY_USER_UI` 均保留为历史阶段快照，不是当前 blocker。

> 下方旧的客户端汇总保留为历史快照；当前权威状态以上述 DeepSeek state reconciliation 及“Current Phase 7 boundary”为准。
**Phase 5.11 = FORMALLY ACCEPTED / PASS**（Phase 5.10 = FORMALLY ACCEPTED / PASS；Phase 5.9 = FORMALLY ACCEPTED / PASS；Phase 5.8 = FORMALLY ACCEPTED / PASS）  
**Phase 5.7 = PASS；Phase 5.8 Preflight = COMPLETE/ACCEPTED；Phase 5.8.1 = PASS；Phase 5.8.2 = PASS；Phase 5.8.3 = PASS；Phase 5.8.4 = PASS；Phase 5.8.5 = PASS；Phase 5.9 = PASS；Phase 5.10 regression = FORMALLY ACCEPTED / PASS；Phase 5 overall = FORMALLY ACCEPTED / PASS / COMPLETE；Phase 6 Entry Preflight = FORMALLY ACCEPTED / PASS；client priority = Codex P0 required, Cursor P1 required, DeepSeek Harness P1 required, Claude Desktop P2 optional；Codex project configuration recognition = PASS；Codex P0 MCP connection/initialize-equivalent session/exact tools-list=30/safe read-only ping = FORMALLY ACCEPTED / PASS；raw initialize envelope not separately exposed（non-blocking）；other Codex tools/call = NOT VERIFIED；Cursor 6.1–6.3 = FORMALLY ACCEPTED / PASS；Cursor 6.4 = BLOCKED_BY_CLIENT_LOG_VISIBILITY（historical evidence retained）；Cursor P1 connection gate = BLOCKED_BY_USER_UI；Cursor connection/initialize/tools-list/tools-call = NOT VERIFIED；Phase 6 = IN PROGRESS / BLOCKED_BY_USER_UI；Phase 7 = NOT STARTED；Phase 5.6.4 = PASS**

Phase 5.5.1/5.5.2/5.5.3/5.5.4、Phase 5.5 和 Phase 5.6.1–5.6.4 已正式完成；当前真实 MCP Runtime tools/list=30。独立 Gate Keeper 已正式接受 Phase 5.8.1–5.8.5、Phase 5.8 overall、Phase 5.9、Phase 5.10 和 Phase 5.11；**Phase 5 overall = FORMALLY ACCEPTED / PASS / COMPLETE**。Independent Gate Keeper 已正式接受 **Phase 6 Entry Preflight = PASS**、**Phase 6.1 Cursor Availability = PASS** 和 **Phase 6.2 Cursor MCP Configuration File = PASS**；Cursor 相关 6.1–6.4 记录保留为历史证据，当前 P0 客户端为 Codex；Phase 6 当前 **IN PROGRESS / PASS CANDIDATE / AWAITING CODEX P0 GATE REVIEW**。

### Phase 6 completed / current Phase 7 boundary

~~~text
Phase 6 Codex P0 — MCP Connection / Initialize / Exact Tools List / Safe Read-only Call
FORMALLY ACCEPTED / PASS
Raw initialize envelope = NOT SEPARATELY EXPOSED（non-blocking）
~~~

Phase 5.10 已由 Independent Gate Keeper 正式接受为 PASS；完整回归证据见 [PHASE_05_10_REGRESSION_VERIFICATION.md](phases/PHASE_05_10_REGRESSION_VERIFICATION.md)。Phase 5.11 已正式接受为 PASS；Phase 6 Entry Preflight、Codex P0、Cursor P1 与 DeepSeek Harness P1 均已正式接受，Phase 6 overall = **FORMALLY ACCEPTED / PASS / COMPLETE**。当前只执行 [PHASE_07_ENTRY_PREFLIGHT.md](phases/PHASE_07_ENTRY_PREFLIGHT.md) 与 [PHASE_07_IMPLEMENTATION_PLAN.md](phases/PHASE_07_IMPLEMENTATION_PLAN.md) 所定义的 Phase 7 入口盘点和规划。

### Historical next boundary before Phase 6 closure

~~~text
DeepSeek Harness P1 — MCP Configuration / Connection / Initialize-equivalent Session / Canonical 30 Tools / Safe Read-only Ping
FORMALLY ACCEPTED / PASS (latest same-client evidence)
Phase 6 overall = PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER
~~~

### Current Phase 7 boundary

~~~text
Phase 7 — Deployment and User Experience
IN PROGRESS / 7.1 and 7.2 FORMALLY ACCEPTED / PASS / 7.3 IN PROGRESS
7.1 Package and Release Identity = IMPLEMENTED / FRESHLY VERIFIED
Next authorized implementation after Gate Keeper review = 7.2 Install, Uninstall and Rollback
~~~

### Phase 5.8.1 Gate

- Source health：PASS；`TestPolygons` 为 FeatureClass/Polygon/WKID 3857/count 4/6 fields，Type 值为 Type1–Type4。
- TestWorkspace file cleanup：PASS；current Codex harness 的 ArcPy GDB probe 为 `BLOCKED_BY_ENVIRONMENT` 且临时 root 已清理；外部 native host 的同一 ArcPy probe 已 PASS。
- Retained owned GDB、owned source、`P58_ClipMask`、controlled project/map/layer：已由 external native host 准备并通过正式 acceptance；实际 RunId 为 `P57_B8C6FE8E`。
- 当前用户 `MyProject1.aprx` dirty 且禁止 mutation；Phase 5.8.2 必须使用 retained 的 test-owned `.aprx`，不得切换或修改用户工程。
- ArcPy standalone `python.exe`/`propy.bat` 在 current Codex harness 中 import `arcpy`/`arcgisscripting` 复现 `-1073741819`；外部 native host 已成功，Bridge 已自愈为新 PID。
- 详细报告：[`PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md`](phases/PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md)。
- Recovery Gate 已通过：当前 Bridge PID 16064 的真实 ArcPy discovery 仍 PASS；外部 native host 同一 probe 输出 `ARCPY_OK=true`、ArcGIS Pro 3.5/Build 57366、Advanced、exit code 0；current Codex child 的 ArcPy crash 定性为 harness-specific。
- Recovery 报告：[`PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md`](phases/PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md)。

## Phase 5.8.1 Formal Acceptance — Repository State Reconciliation（2026-09-04）

- Independent Gate Keeper decision: **FORMALLY ACCEPT — PHASE 5.8.1 PASS**；**READY FOR PHASE 5.8.2**。
- Accepted evidence: source health、external native ArcPy、owned FileGDB、`P58_TestPolygons`、`P58_ClipMask`、controlled APRX、explicit reopen、owned datasource、selection/visibility baseline、cleanup probe、project cleanup probe、manifest 和 no-orphan-process 均 PASS。
- Regression basis: Build 0 errors/3 `NU1900`；Unit 68/68；Integration 20/20；root cause **TEST DEFECT**；production Python Bridge lifecycle semantics 未修改。
- Actual retained identity: `P57_B8C6FE8E`，root 为 `<user-home>\AppData\Local\Temp\ArcGISProMCP\Phase5_7_4_P57_B8C6FE8E`；marker/manifest 已读核对，未重建、重命名、移动或修改 fixture。`P57` naming debt 为 non-blocking。
- 历史 `PASS CANDIDATE`、67/68 regression、recovery evidence 均保留；本记录只追加正式 acceptance。
- Carried forward: `list_maps`/`get_map_info` PARTIAL、dataset/raster LIMITED IMPLEMENTATION、`ArcGISProject.isDirty` unavailable/non-blocking、HTTP disconnect NOT VERIFIED、7 HTTP tests BLOCKED_BY_HARNESS。
- `select_layer` 仍只有 `mapName`/`layerName`，不支持 OID 参数；仅记录供 5.8.2 规划，不宣称 selection mutation PASS。

## Phase 6 Entry Preflight（2026-09-05，安装前历史快照）

- Entry Preflight 已完成；当前 Phase 6 = **IN PROGRESS / ENTRY PREFLIGHT / BLOCKED_BY_EXTERNAL_CLIENT**。
- 本机未发现 Claude Desktop、Cursor、DeepSeek Harness 的可执行文件、常见配置文件或运行进程；6511/6520 当前均无监听。未读取配置内容、环境变量或任何凭据。
- 仓库具备 server-side MCP protocol/transport/tool 能力，但没有 client-side MCP caller、client config wizard、provider SDK、credential store 或 conversation loop；现有 HTTP/xUnit harness 不能替代真实客户端验收。
- 次级阻塞为 **BLOCKED_BY_UNDEFINED_ACCEPTANCE**：尚未指定首个客户端，也未定义客户端/provider/login 的精确验收阈值；不标记为 BLOCKED_BY_CREDENTIAL。
- 当时下一步是由用户提供/安装并指定首个外部 MCP Client，再由 Gate Keeper 单独授权客户端配置；该动作已在 Phase 6.1 完成 Cursor availability 后闭合。当前等待独立复核，Cursor 配置仍未开始；Phase 7 继续 NOT STARTED。
- 详细报告：[PHASE_06_ENTRY_PREFLIGHT.md](phases/PHASE_06_ENTRY_PREFLIGHT.md)。

## Phase 6 Entry Preflight Formal Acceptance（2026-09-05）

- Independent Gate Keeper 正式接受 **PHASE 6 ENTRY PREFLIGHT PASS**；首个客户端确定为 **Cursor**。
- 第一客户端最低 gate：Cursor 版本/配置面、现有 `http://127.0.0.1:6520/mcp` Streamable HTTP 配置、Cursor 自身 initialize/session、精确 `tools/list=30` 和一个授权安全只读 `tools/call`。真实 GIS mutation 需另行授权并限于 owned/controlled target。
- 原 **BLOCKED_BY_UNDEFINED_ACCEPTANCE** 已标记为历史 pre-decision evidence，并对第一客户端 gate 解决；Phase 6 overall 仍不因单一客户端通过而自动 PASS。
- 当前：Phase 6 Entry Preflight = **FORMALLY ACCEPTED / PASS**；Phase 6.1 Cursor Availability = **FORMALLY ACCEPTED / PASS**；Phase 6 = **IN PROGRESS / READY FOR CURSOR MCP CONFIGURATION**；Cursor configuration = **NOT STARTED / READY TO START**；Phase 7 = **NOT STARTED**。

## Phase 6.1 Cursor Availability（2026-09-05）

- 已按授权执行指定 Winget 安装并完成 fresh availability verification：`Anysphere.Cursor` / `3.19.7` / `winget`。
- 可执行文件：`<user-home>\AppData\Local\Programs\Cursor\Cursor.exe`；完整版本与卸载注册信息一致；`cursor` 当前 shell command 缺失仅表示 PATH 尚未刷新。
- Winget/Cursor/安装器均未运行，6511/6520 无监听；未启动 Cursor、未登录、未读取凭据、未创建 MCP 配置、未连接服务器或调用工具。
- Independent Gate Keeper 已正式接受 **Phase 6.1 Cursor Availability = PASS**。当前状态：**Cursor availability = FORMALLY ACCEPTED / PASS**；**Cursor MCP configuration = NOT STARTED / READY TO START**；Phase 6 = **IN PROGRESS / READY FOR CURSOR MCP CONFIGURATION**；Phase 7 = **NOT STARTED**。详细报告：[PHASE_06_1_CURSOR_AVAILABILITY.md](phases/PHASE_06_1_CURSOR_AVAILABILITY.md)。

## Phase 6.2 Cursor MCP Configuration File（2026-09-05）

- 已按授权确认项目级 `.cursor/mcp.json` 创建前不存在，并创建最小配置：单一 `arcgis-pro-mcp` server，URL 为 `http://127.0.0.1:6520/mcp`。
- Strict JSON parse 与 exact-key validation = PASS；无 headers/env/token/credentials/commands/stdio/SSE/second endpoint/provider settings；文件大小 97 bytes，SHA-256=`C5CF47ED4759FDBF391838C112971BAB5C6A3F6BD135F846675AD708C5AD135B`。
- Global/user MCP configs 仍 absent；未读取无关 Cursor settings 或 secret-bearing 内容。
- 当前状态：**Phase 6.3 Cursor Client Load / Configuration Recognition = FORMALLY ACCEPTED / PASS**；**Phase 6.4 Cursor MCP Connection / Initialize / Tools List = BLOCKED_BY_CLIENT_LOG_VISIBILITY**；**Cursor MCP connection/initialize/tools-list = NOT VERIFIED**；Phase 6 = **IN PROGRESS / READY FOR CURSOR MCP CONNECTION VERIFICATION**；Phase 7 = **NOT STARTED**。详细报告：[PHASE_06_3_CURSOR_CLIENT_LOAD.md](phases/PHASE_06_3_CURSOR_CLIENT_LOAD.md)。

## Phase 6.4 Cursor MCP Connection / Initialize / Tools List（2026-09-05）

- 仅启动 retained controlled APRX，使用 process-scoped verification auto-start；controlled Pro PID `24636`，6520 listener 出现，6511 未监听；随后受控 Pro 优雅关闭，post-check 无 ArcGIS Pro/dotnet/6511/6520。
- Cursor 当前会话日志包含 `arcgis-pro-mcp`、exact endpoint、`mcpServers` 和 initialize-related semantic activity，但未提供可验证的成功 connection/session、initialize result、`tools/list` 或 exact `tools/list=30`；未执行 `tools/call`。
- 当前 gate：**BLOCKED_BY_CLIENT_LOG_VISIBILITY**；**Cursor MCP connection/initialize/tools-list = NOT VERIFIED**。报告：[PHASE_06_4_CURSOR_MCP_CONNECTION.md](phases/PHASE_06_4_CURSOR_MCP_CONNECTION.md)。

### Phase 6.4 controlled recovery attempt（2026-09-05）

- 已按 Gate 顺序先关闭旧 Cursor，再先启动 controlled APRX（PID `28284`，6520 到达，6511 未监听），随后启动 Cursor（PID `6136`，新会话 `20260905T142036`）。
- 新 Cursor 会话仍未暴露 project server identity、endpoint、成功 connection/session、成功 initialize、`tools/list` 或 exact `tools/list=30`；未执行 `tools/call`。结果保持 **BLOCKED_BY_CLIENT_LOG_VISIBILITY**。
- Cursor 与受控 Pro 均已优雅退出，post-check 无 Cursor/ArcGIS Pro/dotnet/6511/6520 残留。

### Historical transition record

```text
Phase 5.8.4 — Mutation & Cleanup Verification
PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER
```

该段保留 Phase 5.8.4 当时的 transition history；后续 5.8.4、5.8.5、5.8 overall 和 5.9 均已正式接受，当前任务以文档顶部和末尾最新状态为准。

## Phase 5.8.2 Native Real Verification（2026-09-04）

受控 runtime PID 2796 使用显式 retained APRX 执行。get_current_map 返回 Phase58_TestMap；活动地图图层、visibility false/restore、select_layer current-contract、clear_selection、P58_ClipMask remove/owned-path add/usable read 均已取得 fresh evidence。list_maps/get_map_info、dataset info、selection count 和 add_layer 顺序限制均按实际结果记录；select_layer 不宣称 PASS。未修改 production code、MyProject1.aprx、shared Phase4Test.gdb 或 retained fixture；专用实例已关闭且未保存。

当前下一动作：等待 independent Gate Keeper review，不进入 Phase 5.8.3、Phase 5.9 或 Phase 5 overall acceptance。

## Phase 5.8.2 Formal Acceptance（2026-09-04）

Independent Gate Keeper 已正式裁定：FORMALLY ACCEPT — PHASE 5.8.2 PASS。Selection 结论校正为 select_layer = **PARTIAL / LIMITED IMPLEMENTATION**；clear_selection execution path observed，但 true non-zero → zero mutation **NOT VERIFIED**。Phase 5.8.3 GP / ArcPy Real Verification 已授权，状态为 **READY TO START**；不得进入 Phase 5.8.4 或 Phase 5.9。

## Phase 5.8.3 Verification Result（2026-09-04）

- 四个生产 GP 输出和 ArcPy Describe proof 均 PASS；生产 Bridge PID `1028` 在独立重复调用中复用。
- retained fixture post-health PASS；四个 current-phase outputs 按 `INTENTIONALLY_RETAINED_OWNED_GP_OUTPUTS` 保留给 Phase 5.8.4。
- 详细报告：[`PHASE_05_8_3_GP_ARCPY_REAL_VERIFICATION.md`](phases/PHASE_05_8_3_GP_ARCPY_REAL_VERIFICATION.md)；输出 manifest：[`PHASE_05_8_3_GP_OUTPUT_MANIFEST.md`](phases/PHASE_05_8_3_GP_OUTPUT_MANIFEST.md)。
- 当前状态：**PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 5.8.4、Phase 5.9 及后续阶段未开始。

## Phase 5.8.3 Formal Acceptance（2026-09-04）

Independent Gate Keeper 已正式裁定：**FORMALLY ACCEPT — PHASE 5.8.3 PASS**。接受依据、fresh GP/ArcPy evidence、production persistent Bridge PID 复用、ownership、lock/cleanup、baseline、limitations 和 boundary 均保留在详细报告中。

### Historical next task before Phase 5.11 formal acceptance

```text
Phase 5.8.4 — Mutation & Cleanup Verification
NOT STARTED / READY TO START
```

本轮已完成 Phase 5.8.4 execution 并形成 PASS CANDIDATE；Phase 5.9 及后续阶段未开始。

## Phase 5.8.4 Mutation & Cleanup Verification（2026-09-04）

- 详细报告：[PHASE_05_8_4_MUTATION_CLEANUP_VERIFICATION.md](phases/PHASE_05_8_4_MUTATION_CLEANUP_VERIFICATION.md)。
- Visibility mutation/restore 与 current-phase owned P583_Buffer add/remove 取得 fresh production evidence；原始三层顺序/可见性恢复，未保存 APRX。
- select_layer 仅返回 count=0，保持 **PARTIAL / LIMITED IMPLEMENTATION**；clear_selection execution path 已观察，但 true non-zero → zero 仍 **NOT VERIFIED**。
- 精确 current-phase root 已在 Pro/Bridge 关闭、锁为 0、canonical/reparse/containment 检查通过后移入回收站；root/GDB 消失，retained fixture、MyProject1.aprx 和 shared GDB 保持不变。
- controlled APRX hash/mtime baseline 相同；retained marker/manifest RunId 均为 P57_B8C6FE8E；fresh retained counts 为 4/1；无 orphan Pro/Bridge。

### Historical authorized state snapshot (before later formal acceptances)

Phase 5.8.4 — Mutation & Cleanup Verification

**PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**

Phase 5.8.5 = **NOT STARTED**；Phase 5.9–5.11 = **NOT STARTED**；Phase 5 overall = **NOT COMPLETE**。

本轮已停止，不开始 Phase 5.8.5 或 Phase 5.9。

## Transition Gate Result（2026-09-03）

- `Phase 5.7 formally exists: YES`：由 `PHASE_05.md:20` 的正式 roadmap 定义；没有独立 `PHASE_05_7*` 文件。
- `Phase 5`：已完成至 5.6；5.7–5.11 未独立验收。下一个正式阶段是 **Phase 5.7**，不是 Phase 6。（历史记录）
- `Phase 6`：文档当时为 NOT STARTED，范围是 DeepSeek Harness / Claude Desktop / Cursor 等 MCP 客户端的配置与真实连接验证；当前仓库没有 AI client、MCP client 或 provider/credential 实现。（历史记录；当前状态见本文顶部和 Phase 6 Entry Preflight 节）
- 本轮只读基线：build 0 errors/3×NU1900；runtime initialize/tools30/python ping PASS；无测试套件重跑、无源代码/配置/package 修改。
- 详细报告：[`POST_PHASE_05_6_TRANSITION_REPORT.md`](phases/POST_PHASE_05_6_TRANSITION_REPORT.md)。

## Phase 5.7 Preflight Result（2026-09-03）

- Repository-defined scope 只有 PHASE_05.md 第 20 行的 Tests 标签；没有独立 Phase 5.7 scope/acceptance 文件，因此推荐范围与正式范围分开记录。
- 源码 inventory 为 73 个 xUnit Fact：Unit 27、Integration 20、Server 26；无 Theory、Trait、Collection。
- 本轮实际 baseline：Build 0 errors/3×NU1900；Unit 27/27 PASS；Integration 20/20 PASS；Server 19/26 PASS，7 项 HTTP transport 因 HttpListenerException: 句柄无效阻塞。
- 静态 30 tool class、Composition 30 registration 与真实 tools/list=30 名称集合一致；Python 5 tool 尚无自动化 facade 覆盖，GP 的 clip/intersect/dissolve 尚无独立 call 覆盖。
- 当前只读 MCP initialize/tools/list/python_bridge_ping 均 PASS；未重跑昂贵 Pro E2E，未执行 test action、AI/provider 或 Phase 5.8。
- 详细报告：Docs/phases/PHASE_05_7_TEST_ARCHITECTURE_COVERAGE_PREFLIGHT.md。

### Next Action

在同一已成功的外部 native prompt 中执行 `call proenv.bat`，再运行 test-only fixture runner 的 `create-retained`；在 owned GDB、ClipMask、controlled project 和 cleanup gates 通过前，不进入 5.8.2 mutation scope。

## 已完成（5.4，真实）
- **F1（生命周期竞态）已修复**：`PythonBridgeProcessManager` 增加 `_lifecycle` 信号量，Start/Stop/Restart/Dispose 互斥；SendRequest 在生命周期锁内取稳定快照、锁外等待响应；写流异常→统一 UNAVAILABLE；`FinalizeAndClear` drain+fail 所有残留 pending TCS（**保证无永久等待**）。
- **F2（路径）已解决**：Composition 集中设 `PythonExecutable`（绝对）、`PythonBridgeScript=Path.Combine(RepoRoot,...bridge_runner.py)`（绝对，不依赖 CWD）、`PythonWorkingDirectory=RepoRoot`。
- **PythonBridgeService（Core）+ IPythonBridgeService**：Ping/GetRuntimeInfo/ArcpyExists/Describe → OperationResult。
- **MCP 接线**：ToolExecutionContext.Python；Router 注入；ToolCategories.Python；2 最小工具 `python_bridge_ping`/`python_runtime_info`（**合计 27 工具**）；ServiceContainer 单例；Module1 卸载 dispose。
- **Service 链真实验证 PASS**（探针）：ping/runtime_info/arcpy_exists/describe 全真实；same-pid；crash→自愈；**F1 race Send+Dispose 并发 2155ms 无 hang**；dispose 0 残留。
- **真实 MCP E2E = PASS（用户部署 185110B 27 工具版后）**：
  - tools/list = **27**（python_bridge_ping / python_runtime_info 在列）。
  - MCP→python_bridge_ping → **pong**；MCP→python_runtime_info → Python 3.11.11 / ArcPy 3.5 / arcgis 2.4.1。
  - 持久化：python 进程恰好 1 个；kill 后 MCP python_bridge_ping 自愈返回 pong，恢复 1 个进程（无泄漏）。
  - Phase 4 回归 spot：ping=pong、get_feature_count=4、buffer 真实 GP 成功（27 工具版下无回归）。
- **Build `-m:1` 0 errors**（6×NU1900=离线 nuget 环境告警）；6511 未监听。

## 5.5.1–5.5.3 状态
- **Phase 5.5.1 = PASS**：Design / Preflight Gate 已接受。
- **Phase 5.5.2 = PASS**：此前已有真实 Level-3 ArcPy/Bridge 验证证据；本次保留历史证据，不因独立复验受限而降级。
- **Phase 5.5.3 = PASS**：MCP Tool implementation、Composition registration、Build/static integration 已完成；Level-4 MCP Runtime 明确属于 5.5.4。
- `PythonDiscoveryTools.cs` 已包含 `dataset_summary`、`list_fields`、`list_workspace_datasets`；三个工具均为 Python execution、`RequiresArcGIS=false`。
- `Composition.BuildRegistry()` 静态注册数为 **30**，未发现重复注册；链路仍为 Registry → Router → `IPythonBridgeService` → `PythonBridgeService` → `PythonBridgeProcessManager` → `bridge_runner.py`。
- 发现并完成一个最小契约修复：`dataset_summary.fieldsSummary.oidCount` 从错误的全部字段数改为 OID 字段数；AST、fake contract check 和真实 `TestPolygons` MCP 结果均 PASS（`oidCount=1`）。

## 5.5.4 最终状态（2026-09-03）
- 正式状态：**PASS**。
- Code inspection：PASS；静态 registry count=30，无重复注册。
- Build `dotnet build -m:1 -c Debug --no-restore`：PASS，0 errors，3×NU1900（无法访问 nuget.org 漏洞数据）。普通 restore/build 受 `<user-home>\AppData\Roaming\NuGet\NuGet.Config` Access Denied 阻塞，标记 BLOCKED_BY_ENVIRONMENT。
- Tests：Unit 11/11 PASS、Integration 20/20 PASS；Server 19/26，7 项均因 Harness 的 `HttpListenerException: 句柄无效`，标记 BLOCKED_BY_HARNESS；历史 57/57 仅作历史基线。
- Runtime：ArcGIS Pro 3.5.0/57366 重启后 `initialize` PASS，真实 `tools/list=30`；`dataset_summary`、`list_fields`、`list_workspace_datasets` 均可发现并真实调用。
- Runtime package：版本 1.0.1、188678B；已部署到官方 AddIns 路径，工作区包与安装包 SHA-256 一致；旧 185110B 包已保留为 `.pre-5.5.4-185110.bak` 备份。
- 新工具真实 MCP E2E、缺参/空值/错误类型、错误隔离、Python PID 持久化和 Phase 4 spot regression 均 PASS。

## Phase 5.6 Architecture / Scope / Preflight（2026-09-03）

### Repository-defined scope
- **Repository does not fully define Phase 5.6 implementation scope.** 没有独立的 Phase 5.6 文档；`PHASE_06.md` 描述的是后续 AI Client 集成，不是本阶段。
- 已确认的仓库约束：保持单一 persistent Python process、stdin/stdout NDJSON、request correlation、serialized execution、lifecycle synchronization；不得新增 HTTP 6511、第二 Bridge/ProcessManager、第二 Registry/Router、多 worker 或任意 Python 执行。
- `DECISION-004` 明确把完整 request timeout / Kill / restart policy 留给 Phase 5.6；`DECISION-003` 仍是 PROPOSED，不能把其中的 `python_execute` 或 arbitrary Python 当作当前需求。

### Recommended Phase 5.6 scope（architecture recommendation）
- 为 Python request 建立独立且可区分的 timeout/cancellation/recovery 语义，并保证已发送的 hung action 不再永久占用唯一 Bridge。
- 修复 correlation ID 必须在整个生命周期唯一的问题；当前 request factories 使用 path `GetHashCode()`，timeout 后可能与 late response 复用 ID。
- 对 stdout protocol line / response bytes 与 stderr diagnostic 设置有界策略；超大 JSON 不截断后继续解析，应 fail + 重建 protocol 状态。
- 将 production actions 与 `raise_test_exception` / `sleep_test` 明确分类；保持固定 structured action allowlist，不开放 arbitrary Python。
- 评估 `AllowedPaths`/workspace guardrail，但先定义 FileGDB、UNC、relative path、normalization、reparse/symlink 和 backward compatibility，不在本轮直接加限制。

### Current bridge audit
- Configuration 现有：`PythonExecutable`、`PythonBridgeScript`、`PythonWorkingDirectory`、`PythonProcessStartupTimeoutMs`（默认 30000；Composition 覆盖 40000）、共享 `RequestTimeoutMs=30000`、`MaxConcurrentRequests=8`；`PythonBridgePort=6511` 仅保留配置，未监听。
- 不存在：Python request 独立 timeout、output/stderr limit、restart limit、script/action limit、AllowedPaths/AllowedWorkspaces、cancellation policy、PythonSecurityValidator。
- `ToolMetadata.TimeoutSeconds` 已定义但没有执行方；不能视为有效的 per-tool timeout。
- 当前架构是 Registry → Router → `IPythonBridgeService` → `PythonBridgeService` → `PythonBridgeProcessManager` → `bridge_runner.py`；单飞 `_flight` 保证 Python 请求串行。

### Timeout / cancellation / recovery conclusions
- MCP server 用 `RequestTimeoutMs` 对整个请求 `CancelAfter`；ProcessManager 又以同一设置（最多 120000ms）等待 response。启动使用独立 `PythonProcessStartupTimeoutMs`。
- CancellationToken 会经过 `McpServer`（但 HTTP transport 当前传入的是 server lifetime token，而非可靠的 client-disconnect token）→ protocol → Router → Tool → Python service → manager 的等待/写入层。
- timeout/cancel 会结束 C# await 并清除 pending TCS，但不会向 Python 发送取消消息，也不会 Kill；`bridge_runner.py` 的 `time.sleep` 和 ArcPy 调用没有协作取消点。
- 因 `_flight` 在 caller 返回后释放，下一请求可能写入 pipe 但只能等旧 action 完成；旧 response 变为 late/unknown。若复用相同 factory ID，存在错配风险。
- 普通 action error / Python exception 返回错误响应且进程应存活；process crash/EOF 标记 Faulted，但当前 pending exception 可能被 Router 映射为 `INTERNAL_ERROR`，不是统一 `PYTHON_BRIDGE_UNAVAILABLE`。
- `StopCoreAsync(CancellationToken.None)` 没有配置的 graceful deadline；startup failure/timeout 也缺少统一 cleanup。`DisposeAsync` 的 semaphore dispose/release 顺序需专门测试。
- 结论：timeout 不能真正终止 Python/ArcPy；对已发送且可能正在运行的 timeout/cancel，可靠恢复需要 fail current request → terminate process tree → bounded restart → allow next request；不得自动重试可能有副作用的 action。caller cancellation 与 timeout 的错误语义应区分，但已发送后的 process quarantine 可采用同一安全路径。

### Output / path / execution / security conclusions
- stdout 逐行读取，stderr 逐行持续消费；当前两者都没有字节上限。Malformed/unexpected stdout 只记录并丢弃，可能让 pending request 等到 timeout；Python stderr traceback 会同时进入 response details 和日志。
- Python action dispatch 是固定分支 allowlist；当前 MCP runtime 没有 `python_execute`、`sleep_test` 或 `raise_test_exception` 工具，未发现 `exec/eval/compile/os.system/Popen/shell=True` 用户代码入口。当前系统不是 sandbox，只是受控 subprocess。
- `dataset_summary`、`list_fields`、`list_workspace_datasets` 直接把路径交给 ArcPy；没有 AllowedPaths、canonical containment、UNC/reparse 处理，因此可读取运行账户可访问的任意 ArcPy 路径，但当前 discovery actions 本身是只读的。
- `list_workspace_datasets` 有 top-level 语义和部分数量截断；`tables`/`featureDatasets` 与 response bytes 仍无统一上限。
- blacklist arbitrary Python syntax 在当前固定-action architecture 中不是首选；allowlist structured actions 优先。现有 `SecurityModels` 只有 `Allowed/SecurityBlocked` 数据模型，没有实际 validator。

### Error and configuration recommendation
- Required when implemented: `PythonRequestTimeoutMs`、graceful-stop/recovery bound、response byte limit、stderr limit、bounded recovery count；`PythonAllowedPaths` 仅在选择路径策略后加入。
- Required new error codes for selected capabilities: `PYTHON_TIMEOUT`、`PYTHON_OUTPUT_LIMIT_EXCEEDED`；`PYTHON_PATH_NOT_ALLOWED` 仅随 path guardrail；restart failure 先复用 `PYTHON_BRIDGE_UNAVAILABLE`，不新增重复码。
- Optional: `PYTHON_PROTOCOL_ERROR` / `PYTHON_RECOVERY_FAILED`，需证明现有 `INTERNAL_ERROR` / `PYTHON_BRIDGE_UNAVAILABLE` 不足后再加。
- Unnecessary now: arbitrary-script blacklist/sandbox violation/script execution error；当前没有 arbitrary script surface。
- 新配置默认值均 **TBD after runtime measurement**；迁移期可显式继承现有 `RequestTimeoutMs=30000`，不得再散落 magic numbers。

### Required / forbidden source changes
- Likely required during implementation: `MCPSettings.cs`、`PythonBridgeProcessManager.cs`、可能的 `PythonBridgeCallResult.cs`/`PythonBridgeService.cs`、`ErrorCodes.cs`；如要真实传递 client cancellation，还需 `HttpMcpTransport.cs`/`McpServer.cs`。
- Conditional: `bridge_runner.py` 只为 action registry/test gating 或协作式 action 需要时修改；`Module1.cs` 需要为 deterministic unload cleanup 设计时修改。
- Must not change in this scope: discovery tool contracts/results、唯一 Registry/Router、MCP HTTP→6511 architecture、`ArcGISProject("CURRENT")` 约束、single-worker model、Phase 5.5 missing-path/raster/top-level semantics；不得新增 GIS business tools。

### Runtime and regression plan
- Normal: `initialize`、`tools/list=30`、`python_bridge_ping`、`python_runtime_info`、三个 discovery tools；维持 `dataset_summary` missing→success+`exists=false`、另两工具 missing→NOT_FOUND。
- Internal implementation probe: gated `sleep_test` with short timeout and cancellation；assert caller completion bound、process state/PID、old action termination、next ping, no duplicate process, no late response misroute。当前 MCP 不暴露该 action，本轮不执行。
- Error/crash/protocol probes：`raise_test_exception`→same-PID ping；forced crash→Faulted→bounded restart；synthetic malformed/oversized stdout/stderr fixture；startup timeout and restart failure。
- Path matrix：absolute/relative, FileGDB inner dataset, UNC, case variants, `..`, symlink/reparse, allowed/outside roots；只读数据，不改变现有 workflow 默认行为。
- Regression：`python_bridge_ping`、`python_runtime_info`、`dataset_summary`、`list_fields`、`list_workspace_datasets`，并 spot `ping`、`get_feature_count`、`buffer`。

### Proposed implementation subphases（recommendation, not repository-defined）
1. 5.6.1 — timeout/cancellation/correlation/recovery contract and unit fixtures。
2. 5.6.2 — ProcessManager bounded wait, kill/restart, pending/late-response and deterministic cleanup。
3. 5.6.3 — response/stderr limits, action classification, optional compatible path guardrails。
4. 5.6.4 — ArcGIS Pro runtime, MCP E2E, recovery probes and regression acceptance。

### Final Gate
**READY FOR PHASE 5.6 IMPLEMENTATION DESIGN**

本轮到此停止，不开始写 Phase 5.6 功能代码。

## Phase 5.6.1 Timeout / Cancellation / Recovery Contract Design（2026-09-03）

### Status and decision

- **Phase 5.6.1 = DESIGN COMPLETE**。
- **Implementation = NOT STARTED**。
- Formal decision：[`DECISION-005-python-bridge-timeout-cancellation-recovery.md`](decisions/DECISION-005-python-bridge-timeout-cancellation-recovery.md)。
- Phase 5.5 remains **PASS**；real MCP baseline remains `tools/list=30`。

### Contract selected

- Distinguish pre-dispatch from post-dispatch using an explicit internal request phase. A write that has started but has uncertain completion is treated as possibly dispatched.
- Caller cancellation returns `CANCELLED`; Python deadline returns `PYTHON_TIMEOUT`。
- Before dispatch, remove the pending entry and keep the healthy child unchanged. After dispatch, quarantine the child, terminate the process tree, perform bounded wait/cleanup, and return only after the old child is no longer usable.
- Use Option C recovery ownership: keep the single-flight slot through quarantine, do not start a replacement child before returning the failed call, and let the next independent request perform a fresh bounded startup probe.
- Never replay a dispatched action. A timeout/cancel/crash makes its business result unknown even for a read-only action.
- Generate unique generation-aware correlation IDs in ProcessManager; reject duplicate pending insertion; discard late, unknown, and old-generation responses.
- Keep lifecycle state API stable; use internal quarantine/generation fields. Do not hold `_lifecycle` while waiting for `_flight`; do not await under `_gate`; clean pending on every exit/startup/dispose path.

### Configuration and implementation boundary

- 5.6.2 will add a centralized Python-specific timeout and bounded stop/kill/recovery settings, replacing the hard-coded response wait cap; defaults remain **TBD after runtime measurement**。
- Process crash, pipe failure, startup failure, and recovery failure initially reuse `PYTHON_BRIDGE_UNAVAILABLE`。
- Output/stderr byte limits, path guardrails, and action classification remain later 5.6.3 scope; no new tool, HTTP 6511 listener, second Bridge, multi-worker, or arbitrary Python execution is approved。

### Verification required later

The next implementation must test pre/post-dispatch timeout and cancellation, same-PID ordinary action errors, crash/startup cleanup, unique IDs and late responses, no replay, dispose ordering, bounded completion, and one-child fresh-PID recovery. The current MCP runtime does not expose a safe sleep test hook, so no timeout recovery probe was run in this design phase.

### Final Gate

**READY FOR PHASE 5.6.2 IMPLEMENTATION**。本轮停止，不开始 5.6.2 实现。

## Phase 5.6.2 Core Lifecycle Reliability Implementation（2026-09-03）

### Status

- **Phase 5.6.2 = PASS（Core）**。
- **Implementation = COMPLETE**。
- **Phase 5.6.3 = NOT STARTED**。
- `DECISION-005` 已更新为 `ACCEPTED / IMPLEMENTED`；完整修改包 ArcGIS Pro runtime acceptance 留待 Phase 5.6.4。

### Files changed

- `Source/Shared/ArcGISProMCP.Configuration/MCPSettings.cs`
- `Source/Shared/ArcGISProMCP.Core/Results/ErrorCodes.cs`
- `Source/Shared/ArcGISProMCP.Core/PythonBridge/PythonBridgeRequest.cs`
- `Source/Shared/ArcGISProMCP.Core/PythonBridge/PythonBridgeProcessManager.cs`
- `Tests/UnitTests/PythonBridgeLifecycleTests.cs`
- `Docs/ERROR_CODES.md` and phase/memory/decision records。

### Implemented contract

- ProcessManager now owns generation-aware `py-{generation}-{sequence}` IDs; request factories no longer generate fixed/path-hash defaults.
- Dispatch tracking distinguishes registered, dispatching, dispatched, and completed states; write-started uncertainty is treated as possibly dispatched.
- `PythonRequestTimeoutMs=18000` is separate from startup; `PythonProcessShutdownTimeoutMs=3000` bounds cleanup. The action budget excludes the approximately 8-second startup and leaves margin under the 30-second outer MCP deadline.
- Pre-dispatch timeout/cancel leaves a healthy child unchanged. Post-dispatch timeout/cancel uses caller-token-independent quarantine, kill/wait/reader cleanup, and releases `_flight` only after cleanup. Business actions are never replayed.
- Quarantine validates the captured Process instance and generation, so an old request cannot kill a newer process after Stop/Restart.
- Startup failure, process exit, pending TCS, late/old-generation responses, explicit restart, and Dispose/semaphore ordering are handled.

### Verification

- New Core lifecycle tests: **9/9 PASS**。
- Full automated run: Unit **20/20 PASS**、Integration **20/20 PASS**、Server **19/26**；7 Server tests remain `BLOCKED_BY_HARNESS` due `HttpListenerException: 句柄无效`。
- Existing active Pro read-only regression: `initialize` PASS, `tools/list=30`, `python_bridge_ping`, `python_runtime_info`, and `dataset_summary` PASS. The new Add-in binary was not deployed during this phase, so modified-package timeout E2E is not claimed.

### Final Gate

**PHASE 5.6.2 PASS**；**STOP — WAIT FOR PHASE 5.6.3 INSTRUCTION**。

## Phase 5.6.3 — Python Bridge Output / Protocol / Action / Access Guardrails（2026-09-03）

### Status

- **Phase 5.6.3 = PASS**。
- **Implementation = COMPLETE**。
- **Phase 5.6.4 = NOT STARTED**。
- Path Guardrail = **DESIGN COMPLETE / DEFERRED**。

### Files changed

- `Source/Shared/ArcGISProMCP.Configuration/MCPSettings.cs`
- `Source/Shared/ArcGISProMCP.Core/PythonBridge/PythonBridgeProcessManager.cs`
- `Source/Shared/ArcGISProMCP.Core/Results/ErrorCodes.cs`
- `Source/ArcGISProMCP.PythonBridge/bridge_runner.py`
- `Tests/UnitTests/PythonBridgeLifecycleTests.cs`
- `Docs/decisions/DECISION-006-python-bridge-output-protocol-action-guardrails.md` and phase/memory records。

### Contract

- stdout response line limit is 1 MiB；ReadLine 后检查 UTF-8 bytes，超限不截断，返回 `PYTHON_OUTPUT_LIMIT_EXCEEDED` 并保护当前 process。
- 非空 malformed JSON、invalid schema 和 invalid correlation ID 返回 `PYTHON_PROTOCOL_ERROR`；空白行忽略；unknown/late/old-generation valid IDs 仍 log + discard。
- stderr 使用 64 KiB/进程 logger budget；超限只限制记录，reader 继续 drain。
- runner 使用 production/test action allowlists；`PythonAllowTestActions=false` 默认，显式环境变量才能启用 test actions。
- `PythonAllowedPaths` 未实现；完成 ArcGIS path compatibility audit 后延期，不改变 Phase 5.5 默认访问语义。

### Verification

- 当前真实 runtime 测量：业务 payload 最大 1060 bytes，MCP 封装最大观测 2048 bytes；1 MiB 为第一版默认安全余量。
- Guardrail/lifecycle tests **16/16 PASS**；Unit **27/27 PASS**、Integration **20/20 PASS**、Server **19/26**，7 项既有 Harness blocked。
- 当前 Pro 旧 package 只读 regression 仍 PASS；修改包 runtime acceptance 明确留待 Phase 5.6.4。

### Final Gate

**PHASE 5.6.3 PASS**；**STOP — WAIT FOR PHASE 5.6.4 INSTRUCTION**。

## Phase 5.6.4 — Real ArcGIS Pro / ArcPy / MCP E2E Final Acceptance（2026-09-03）

### Status

- **Phase 5.6.4 = PASS**；**Phase 5.6 = PASS**。
- Detailed report：[`Docs/phases/PHASE_05_6_4_FINAL_REPORT.md`](phases/PHASE_05_6_4_FINAL_REPORT.md)。

### Current evidence

- Registered/workspace package identity matched at 202613 bytes and SHA-256 `62FBE89CAE9F03077C797A146662AFC81ED29E59539327D85FBC190A179B8D65`; new Pro PID 15740 loaded it and MCP 6520 passed.
- Real MCP `initialize`/`tools/list=30`, production action isolation, persistent Python PID 31400, real ArcPy discovery, missing-path/argument contracts, `ping`/map/layers/count/buffer spot regression all passed.
- Real production-runner lifecycle: timeout PID 31092→1320 (`PYTHON_TIMEOUT`, 19555ms), cancellation 25112→31872 (`CANCELLED`, 3050ms), structured error same PID 6032, crash 8460→17380, Dispose `Disposed` with no probe child.
- Guardrail/lifecycle 16/16, Unit 27/27, Integration 20/20; Server 19/26 with 7 `BLOCKED_BY_HARNESS` cases. Path Guardrail remains `DESIGN COMPLETE / DEFERRED`; HTTP client-disconnect cancellation is `NOT VERIFIED / future scope`.

### Final Gate

**PHASE 5.6.4 PASS**；**PHASE 5.6 PASS**；**STOP — WAIT FOR NEXT PHASE INSTRUCTION**。

## Post-Phase-5.6 Transition Gate Record（2026-09-03）

本文件当前任务已从 Phase 5.6.4 验收切换为过渡闸门完成状态。下一正式 roadmap item 为 Phase 5.7；Phase 5.7 implementation 尚未开始。详细证据见 [`POST_PHASE_05_6_TRANSITION_REPORT.md`](phases/POST_PHASE_05_6_TRANSITION_REPORT.md)。

## Phase 5.7.1 Production Tool Contract Snapshot & Test Foundation（2026-09-03）

### Completed

- 新增 Tests/UnitTests/ProductionToolContractSnapshot.cs 与 ProductionToolContractTests.cs。
- 测试通过反射调用真实 Composition.BuildRegistry()；没有手工注册 30 个工具，没有修改 production API。
- 覆盖 registry count/unique/name exact set、class↔registration、metadata、category、execution type、RequiresArcGIS、schema structural/required/serialization、5 Python、4 GP 和 forbidden-tool regression。
- Unit 从 27 增至 38，完整 UnitTests **38/38 PASS**；Integration **20/20 PASS**。
- Server **19/26 PASS**；7 个 HttpListener 测试继续为 BLOCKED_BY_HARNESS。
- Build **0 errors / 3×NU1900**；真实 MCP 只读 initialize HTTP 200、tools/list HTTP 200/count 30，runtime name set 与 snapshot 一致。

### Boundary

未实现 Python facade 行为测试、GP 独立参数/执行测试、完整 error matrix、HTTP workaround、Phase 5.7.2、Phase 5.8 或 Phase 6。

### Final Gate

**PHASE 5.7.1 PASS**；**STOP — WAIT FOR PHASE 5.7.2 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS。

## Phase 5.7.2 Tool Behavior Tests（2026-09-03）

### Completed

- 新增 25 个 Unit Fact：Python facade 13、GP behavior 8、filesystem placeholder 4。
- 新增 1 个 Server Fact：生产 Buffer tool failure → Router → McpServer `tools/call` error rendering。
- TestSupport 增加可配置 `FakePythonBridgeService`、`RecordingGeoprocessingService` 和可注入的 `FakeArcGISHost` service；未改 production Source 或配置。
- Python 5 tool 的 success/failure/unavailable、路径/参数转发、错误矩阵和 cancellation token 已覆盖。
- Buffer/Clip/Intersect/Dissolve 独立 routing、values 顺序、可选参数、错误和 cancellation 已覆盖。
- `get_dataset_info`/`get_raster_info` 使用当前生产 filesystem-only service + test-owned temp data 覆盖 existing/missing/invalid。

### Verification

- Build：`dotnet build ArcGIS-Pro-MCP.sln -m:1 -c Debug --no-restore`，0 errors，3×NU1900 环境告警。
- Unit：**63/63 PASS**（含 5.7.1 的 38 个 contract tests）。
- Integration：**20/20 PASS**。
- Server：**20/27 PASS**；7 项继续为 `HttpListenerException: 句柄无效` / `BLOCKED_BY_HARNESS`。
- Runtime read-only：initialize HTTP 200、tools/list=30、python_bridge_ping=pong；未调用 GP 或其他 business action。

### Boundary

Phase 5.7.3、Phase 5.8、Phase 6、AI/provider、HTTP harness workaround、完整 Phase 4 regression 和真实业务 tool E2E 均未开始。详细报告：[`PHASE_05_7_2_TOOL_BEHAVIOR_TESTS.md`](phases/PHASE_05_7_2_TOOL_BEHAVIOR_TESTS.md)。

### Final Gate

**PHASE 5.7.2 PASS**；**STOP — WAIT FOR PHASE 5.7.3 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS。

## Phase 5.7.3 Server Protocol & HTTP Harness Resolution（2026-09-03）

### Result

- Phase 5.7.3 = **PASS**；Phase 5.7 = **IN PROGRESS**；Phase 5.7.4 尚未开始。
- ServerTests 共 41 项：34 PASS；7 个 HTTP transport tests 在 `HttpListener.Start()` 以 `HttpListenerException (6): 句柄无效` 阻断，Case H = `BLOCKED_BY_HARNESS`。
- 独立 BCL probe 在 fixed `16521`、dynamic `54450` 和 child process 三个维度复现同一 `SetupV2Config()` failure；真实 Pro 6520 listener 可用，因此没有 production defect 证据。
- HTTP-independent protocol/server coverage：`MCPProtocolTests` 9/9、`MCPServerTests` 25/25 PASS；新增 ID、params/name/arguments、batch、timeout 和 caller cancellation edge tests。
- Unit 63/63、Integration 20/20、Build 0 errors/3×NU1900；runtime initialize/tools/list=30/python_bridge_ping=pong 只读 baseline PASS。

### Boundary

本轮未修改 production Source、transport、Registry/Router/Bridge、配置、package 或 AI/provider；未修改 OS URLACL/firewall/权限；未开始 Phase 5.7.4、5.8 或 Phase 6。HTTP client-disconnect cancellation、large request policy 和完整 Phase 4 regression 仍 NOT VERIFIED。

### Final Gate

详细报告：[`PHASE_05_7_3_SERVER_PROTOCOL_HARNESS.md`](phases/PHASE_05_7_3_SERVER_PROTOCOL_HARNESS.md)。

**PHASE 5.7.3 PASS**；**STOP — WAIT FOR PHASE 5.7.4 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS；Phase 5 overall NOT COMPLETE。

## Phase 5.7.4 Test Data Ownership & Mutation Safety（2026-09-03）

### Result

- Phase 5.7.4 = **PASS**；Phase 5.7 = **IN PROGRESS**；Phase 5 overall = **NOT COMPLETE**；Phase 5.8 = **NOT STARTED**。
- 新增统一 `Tests/TestSupport/TestWorkspace`：marker、run-scoped unique names、safe containment、owned child-process artifacts、reparse refusal 和独立 cleanup result。
- Placeholder/Python temp fixtures 已统一到 test-owned workspace；已验证 cleanup 不删除 external sibling，且完整 Unit **68/68 PASS** 无 Phase 5.7.4 temp root 残留。
- Integration **20/20 PASS**；Protocol **9/9 PASS**；MCPServer **25/25 PASS**；solution build **0 errors / 3×NU1900**。
- `TestDate/Phase4Test.gdb` 只读 inventory 后保持原状；没有重开既有 HttpListener Case H。

### Boundary

本轮没有执行完整 Real GP、30-tool business E2E、ArcGIS map/selection mutation、HTTP workaround、Phase 5.8/5.10 或 production Source 修改。详细报告：[`PHASE_05_7_4_TEST_DATA_MUTATION_FIXTURES.md`](phases/PHASE_05_7_4_TEST_DATA_MUTATION_FIXTURES.md)。

### Final Gate

**PHASE 5.7.4 PASS**；**STOP — WAIT FOR PHASE 5.7.5 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS；Phase 5 overall NOT COMPLETE。

## Phase 5.7.5 Test Consolidation & Acceptance（2026-09-03）

### Result

- **Phase 5.7.5 = PASS**；**Phase 5.7 = PASS**；Phase 5 overall = **NOT COMPLETE**。
- Final inventory：129 discovered Facts；Unit 68/68、Integration 20/20、Protocol 9/9、MCPServer 25/25 PASS；HTTP 7 项为 Case H / BLOCKED_BY_HARNESS。
- 30-tool contract、Python facade、GP routing、placeholder、error/protocol coverage、TestWorkspace、coverage diagnostic、P0/P1 review 已收口。
- Real Pro 只读 baseline：initialize 200、tools/list 30 distinct、python_bridge_ping=pong；没有执行业务 mutation、GP、AI/provider 或 test action。

### Boundary

完整 real GP、30-tool Pro business execution、selected OID restore、dedicated mutation project、supported-host HTTP rerun 和 Phase 5.10 full regression 留在后续范围；shared TestDate/Phase4Test.gdb 保持 untouched。

### Report

完整报告：[`PHASE_05_7_5_TEST_ACCEPTANCE.md`](phases/PHASE_05_7_5_TEST_ACCEPTANCE.md)。

### Final Gate

**PHASE 5.7.5 PASS**；**PHASE 5.7 PASS**；**READY FOR PHASE 5.8 PREFLIGHT**；**STOP — WAIT FOR PHASE 5.8 INSTRUCTION**。

## Phase 5.8.1 Recovery Gate（2026-09-03）

- Current Bridge PID 16064（parent=ArcGISPro PID 15740）真实 ArcPy discovery PASS。
- direct/propy standalone 的 core/stdlib/NumPy 1.26.4 PASS；`arcgisscripting`、normal `arcpy`、`ARCPY_NO_IMPORTS=1` 均以 `-1073741819` / `0xC0000005` 退出。
- Application/WER 没有匹配 crash event，faulting module UNKNOWN；license Advanced；没有 shadowing；用户项目/shared GDB untouched；无 probe orphan。
- 外部 native host 已执行同一 probe：`ARCPY_OK=true`、ArcGIS Pro 3.5/Build 57366、LicenseLevel Advanced、exit code 0；因此判定为 **BLOCKED_BY_HARNESS / execution-context specific**。完整报告：[`PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md`](phases/PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md)。

**PHASE 5.8.1 RECOVERY GATE PASS**；**PHASE 5.8.1 FIXTURE PREPARATION MAY RESUME / IN PROGRESS**；owned GDB/project 尚未验收，停止，不进入 Phase 5.8.2。

## Phase 5.8.1 Native Host Fixture Resume Implementation（2026-09-04）

- test-only runner 的真实模式为 `create-retained`、`cleanup-probe`、`project-probe`；输出由 `FIXTURE_RESULT_BEGIN` 与 `FIXTURE_RESULT_END` 包围。
- `TestWorkspace` 使用 `%TEMP%\ArcGISProMCP\Phase5_7_4_P57_<runId>`，marker 在 ArcPy 写入前创建并记录 RunId/root/UTC creation time。
- `create-retained` 真实目标为 `Phase58_<RunId>.gdb`、`P58_TestPolygons`、`P58_ClipMask`、root 下的 `Phase58Controlled.aprx` 和 `<root>\fixture-manifest.json`。
- helper 已加入 source/copy/ClipMask/project health payload、explicit owned datasource checks、project reopen、selection/visibility baseline，以及独立 disposable cleanup probes；成功路径保留，失败路径 cleanup。
- TestWorkspace 对残留 `.lock`/`.sr.lock` 先行返回 `BLOCKED_BY_RUNTIME_LOCK`，不手工删除锁文件。
- 验证：fixture helper 0 warnings/0 errors；solution 0 errors/3×NU1900；Unit 68/68；Integration 20/20。
- 下一步只在 external native ArcGIS Python Command Prompt 执行 `call proenv.bat` 后的真实 runner command；收到完整 JSON 前继续停留在 Phase 5.8.1，不进入 5.8.2。

## Phase 5.8.1 Unit Regression / Process-Lock Recovery（2026-09-04）

- 修复前 Unit failure 已定位为 test-owned fake Python 日志读取竞态：`PythonBridgeLifecycleTests.cs:526` 的 `File.ReadAllLines` 与 child 对 `requests.ndjson` 的 exclusive append handle 冲突；分类为 `TEST DEFECT`，不是 `BLOCKED_BY_HARNESS` 或 `BLOCKED_BY_RUNTIME_LOCK`。
- 仅修改 `Tests/UnitTests/PythonBridgeLifecycleTests.cs` 的测试 helper：transient `IOException` 返回空快照，继续使用已有 polling 等待 writer 关闭；production ProcessManager 和 Phase 5.6 semantics 未改变。
- 目标测试修复后 10/10 PASS；完整 Unit 5/5 次 68/68 PASS；Integration 20/20 PASS；solution Build 0 errors/3×NU1900。
- retained fixture `P57_B8C6FE8E` 轻量 health recheck PASS；cleanup/project probes 均 PASS、residual=0；shared GDB 与用户工程保持 untouched。
- 完整报告：[`PHASE_05_8_1_UNIT_REGRESSION_RECOVERY.md`](phases/PHASE_05_8_1_UNIT_REGRESSION_RECOVERY.md)。

### Final Gate

**PHASE 5.8.1 FINAL GATE = PASS CANDIDATE；提交独立 Gate Keeper 审核**。不进入 Phase 5.8.2。

## Phase 5.8.4 Post-cleanup Retained Semantic Health Recovery Gate（2026-09-04）

- 针对 Independent Gate Keeper interim PARTIAL，已完成最小 recovery gate；未恢复回收站中的 disposable root，未创建输出，未执行 map/layer/visibility/selection/GP mutation。
- Fresh production MCP read-only 结果：retained P58_TestPolygons 为 FeatureClass/Polygon/WKID 3857/count 4；P58_ClipMask 为 FeatureClass/Polygon/WKID 3857/count 1；list_workspace_datasets 恰好返回两个 FeatureClasses，totalCount=2。
- disposable root/GDB/P583 outputs 仍 absent；controlled APRX pre/post SHA256 均为 0F514C540553118C443A91C188D8D69E14EEC4EB7C33897D34A82290639AF2F3；marker/manifest 的 RunId 均为 P57_B8C6FE8E 且 hash 不变；retained GDB 为 65 files/0 locks。
- 显式关闭后无 Pro、Bridge 或 6520 listener；MyProject1.aprx 与 shared Phase4Test.gdb 未变。

Recovery Gate = **PASS**。当前任务仍为 Phase 5.8.4 final independent review，状态 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 5.8.5 与 Phase 5.9 均 **NOT STARTED**。本轮不开始下一 Phase。

## Phase 5.8.4 Independent Gate Keeper Formal Acceptance（2026-09-04）

- Independent Gate Keeper：**FORMALLY ACCEPT — PHASE 5.8.4 PASS**。
- 接受依据包括 visibility restore、P583_Buffer 精确 add/remove 与原图层恢复、controlled APRX discard-without-save、精确 disposable-root 回收站 cleanup、自然锁释放、受保护文件完整性及 post-cleanup retained semantic health。
- Fresh production MCP 重新确认 P58_TestPolygons count=4、P58_ClipMask count=1，retained workspace 恰好两个 FeatureClasses；disposable root 仍 absent；无 Pro、Bridge 或 6520 listener。
- select_layer 继续为 PARTIAL / LIMITED IMPLEMENTATION；真实 non-zero → zero clear_selection 继续 NOT VERIFIED。历史 candidate、Recovery Gate、known limitations 和 TEST DEFECT 均保留；production Bridge lifecycle semantics 未修改。

### Current authorized task

Phase 5.8.5 = **NOT STARTED / READY TO START**。本轮只完成文档同步，不启动 Phase 5.8.5 或 Phase 5.9；Phase 5 overall = **NOT COMPLETE**。

## Phase 5.8.5 Evidence Consolidation（2026-09-04）

- 本阶段已进入 **IN PROGRESS**，报告为 [PHASE_05_8_5_EVIDENCE_CONSOLIDATION.md](phases/PHASE_05_8_5_EVIDENCE_CONSOLIDATION.md)。
- 30-tool matrix：PASS 24、PARTIAL 3、LIMITED IMPLEMENTATION 2、NOT VERIFIED 1；tool-level BLOCKED_BY_HARNESS 与 OUT-OF-SCOPE 均为 0。
- Fresh / carried-forward accepted / historical evidence 已明确分离；完整 Phase 5.9 business chain 不在本阶段。
- 受保护 fixture、APRX、MyProject1.aprx、shared GDB、cleanup、lock 和 no-orphan 证据按 5.8.1–5.8.4 formal acceptance 汇总；唯一非阻断 provenance gap 为 shared GDB 历史 115/11 与后续 104/0 记录差异，未猜测、未修复。
- 本轮无 runtime、代码、配置、测试或资产变更；accepted Build/Test baseline 沿用。

### Current authorized task

Phase 5.8.5 = **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**。等待正式裁定；不得开始 Phase 5.9、5.10、5.11 或 Phase 6。

## Phase 5.8.5 / Phase 5.8 Independent Gate Keeper Formal Acceptance（2026-09-04）

- Independent Gate Keeper 正式裁定：**FORMALLY ACCEPT — PHASE 5.8.5 PASS**；**FORMALLY ACCEPT — PHASE 5.8 PASS**。
- 30-tool matrix：PASS 24、PARTIAL 3、LIMITED IMPLEMENTATION 2、NOT VERIFIED 1；fresh、carried-forward accepted、historical evidence 已分离；7 个 HTTP transport tests 继续为 BLOCKED_BY_HARNESS。
- 四个 GP、五个 production persistent Python Bridge、Native/map/layer/data/project/system evidence 已闭环；retained marker/manifest 实际 RunId 为 P57_B8C6FE8E，fixture health unchanged。
- select_layer、clear_selection、list_maps/get_map_info、dataset/raster、isDirty 和 HTTP cancellation 等限制继续保留；shared GDB 115/11 与 104/0 的 GAP-01 provenance 差异保持 non-blocking；不猜测、不修复。
- Source/Tests、production code/config、Word、runtime 和受保护资产均未因本次同步改变；accepted baseline 为 Build 0 errors/3 NU1900、Unit 68/68、Integration 20/20。

### Current authorized task

```
Phase 5.9 — MCP → Router → Native/GP/Python → ArcGIS/ArcPy business chain
NOT STARTED / READY TO START
```

Phase 5.9 尚未执行；Phase 5.10、Phase 5.11 和 Phase 6 仍 NOT STARTED，Phase 5 overall = **NOT COMPLETE**。等待用户明确确认后再开始。

## Phase 5.9 Full MCP E2E Verification（2026-09-04）

- 已按明确授权完成 fresh production HTTP 验证：initialize/tools/list 成功，tools/list 为 30 unique tools；30-tool matrix 为 PASS 24、PARTIAL 3、LIMITED IMPLEMENTATION 2、NOT VERIFIED 1。
- 已完成 Native active-map read/mutation/restore、四个 production GP 到 owned FileGDB 的 output chain、五个 persistent Python Bridge tools，以及 direct ArcPy/FileGDB read-only Describe audit。
- 本轮 RunId 为 P59_A3FBDFB2；所有输出均在唯一 disposable root 下；显式 mapName lookup、select_layer、clear_selection、list_maps/get_map_info 等限制按实际结果保留，没有代码修复或伪造 PASS。
- ArcGIS Pro PID 8388、persistent Bridge PID 26700；关闭时对精确受控 APRX 选择“否(N)”；controlled APRX、MyProject1.aprx、retained P57 fixture 和 shared Phase4Test.gdb 未变。
- exact P59 root 在 lock/no-orphan/reparse/containment/ownership gates 通过后移入回收站；post-check root/GDB/outputs absent，retained counts 4/1、GDB 65/0 locks。
- 详细报告：[PHASE_05_9_FULL_MCP_E2E_VERIFICATION.md](phases/PHASE_05_9_FULL_MCP_E2E_VERIFICATION.md)。

### Final Gate

**PHASE 5.9 = PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**。不构成 formal acceptance；Phase 5.10、5.11、Phase 6 未开始，Phase 5 overall = **NOT COMPLETE**。

## Phase 5.9 Independent Gate Keeper Formal Acceptance（2026-09-05）

- Independent Gate Keeper 正式裁定：**FORMALLY ACCEPT — PHASE 5.9 PASS**。
- 接受依据：fresh MCP entry/tools/list=30、30-tool matrix 分类一致；Native read/mutation/restore；四个 GP 的 OperationResult/messages→ArcPy/FileGDB Describe；五个 persistent Python tools/PID 26700；discard-without-save、自然锁释放、exact-root cleanup 和 post-check 均通过。
- usage-limit interruption/stale PID 已分类为 harness/tooling event；无 force-kill、无重复 root、无 arbitrary/test-only action。所有历史 candidate、限制、GAP-01 和 baseline 均保留。

### Current authorized task

~~~text
Phase 5.10 — Regression
NOT STARTED / READY TO START
~~~

本轮只同步 formal acceptance，不执行 Phase 5.10、Phase 5.11 或 Phase 6；Phase 5 overall = **NOT COMPLETE**。

## Phase 5.10 Regression Verification（2026-09-05）

- 授权边界：仅执行回归验证；没有修改 production source、Tests、Tools、schema、配置、Word 或 Python Bridge lifecycle semantics，没有开始 5.11/Phase 6。
- RunId=`P510_BDE10BCD`；root=`<user-home>\AppData\Local\Temp\ArcGISProMCP\Phase5_10_Regression_P510_BDE10BCD`。marker 先于 inventory/results 创建；通过 ownership/canonical/containment/reparse/lock/no-orphan gate 后移入回收站，root active path absent 且可恢复。
- Build 0 errors / 3 NU1900；Unit 68/68、Integration 20/20、MCPProtocol 9/9、MCPServer 25/25、MCPTransport 7/7、Server full 41/41；去重后 129 Facts。
- `PythonBridgeLifecycleTests` 16/16 连续三次 PASS；每次无 test bridge 泄漏，最终无 Pro/Bridge/testhost/6520/6511 listener。历史 Unit 67/68 与 TEST DEFECT 根因保持历史事实。
- 当前 host 的 7 个 MCPTransport cases fresh 7/7 PASS；不改变历史 Case H / `BLOCKED_BY_HARNESS` 记录，也未验证 HTTP client-disconnect cancellation。
- retained RunId 实际由 marker/manifest 确认仍为 `P57_B8C6FE8E`；retained fixture semantic health、controlled APRX、MyProject1.aprx、shared GDB 均保持不变。

### Phase 5.10 Pre-acceptance Final Gate（historical snapshot）

**PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**。本节保留正式接受前的历史状态；Phase 5.11、Phase 6 未开始，Phase 5 overall = **NOT COMPLETE**。

## Phase 5.10 Independent Gate Keeper Formal Acceptance（2026-09-05）

- Independent Gate Keeper 正式裁定：**FORMALLY ACCEPT — PHASE 5.10 PASS**。
- Independent re-run 确认 Unit 68/68、Integration 20/20、Server full 41/41；fresh matrix 的 Protocol 9/9、MCPServer core 25/25、MCPTransport 7/7 及全部 failed/skipped/error=0 记录保留。
- 30 unique production tools、forbidden test action isolation、3 轮 Bridge lifecycle 16/16、exact-root cleanup、protected-path integrity 和所有 known limitations 均获接受。
- Phase 5.10 formal acceptance 仅为文档状态同步；没有运行测试或 runtime，没有修改 code/config/Word，不执行 Phase 5.11 或 Phase 6。

### Current authorized next task

~~~text
Independent Gate Keeper review of Phase 5.11 candidate
COMPLETED — FORMAL ACCEPTANCE RECORDED
~~~

## Phase 5.11 Final Acceptance Audit（2026-09-05）

- 授权来源：Independent Gate Keeper 在 Phase 5.10 FORMALLY ACCEPTED / PASS 后授权 Final Acceptance Audit；本轮不执行 Native/GP/Python mutation，不运行 Build/Test/runtime，不进入 Phase 6。
- 当前正式结论：**Phase 5.11 = FORMALLY ACCEPTED / PASS**；**Phase 5 overall = FORMALLY ACCEPTED / PASS / COMPLETE**；Phase 6 = NOT STARTED。
- Phase 5.1 无 standalone independent formal-PASS 记录，DECISION-003 = PROPOSED；下游 5.2–5.10 已接受架构证据足以覆盖实现边界，但该历史 provenance gap 保持 NON-BLOCKING，不虚构 PASS。
- 实际 retained RunId = P57_B8C6FE8E；marker/manifest、fixture health、protected APRX hashes、GDB counts/locks 和 P59/P510/process/listener checks 已只读核对，均与已接受证据一致。
- 详细 lineage、architecture/tool inventory、fresh/carry-forward/historical evidence、known limitations、consistency audit 和 Phase 6 boundary 见 [PHASE_05_11_FINAL_ACCEPTANCE.md](phases/PHASE_05_11_FINAL_ACCEPTANCE.md)。

## Phase 5.11 Independent Gate Keeper Formal Acceptance（2026-09-05）

- Independent Gate Keeper 正式裁定：**FORMALLY ACCEPT — PHASE 5.11 PASS**；**FORMALLY ACCEPT — PHASE 5 OVERALL PASS**。
- Phase 5.1 的 standalone formal-PASS 记录缺失、DECISION-003 = PROPOSED、python_execute/arbitrary Python 禁止、PythonAllowedPaths DESIGN COMPLETE / DEFERRED 等边界均按审计报告原样保留；5.1 gap 分类为 NON-BLOCKING HISTORICAL PROVENANCE GAP。
- 正式接受依据包括 Phase 5.2–5.10 完整证据链、30 unique tools、5 production Python tools、single persistent NDJSON Bridge、Build/Test/lifecycle baseline、Phase 5.8/5.9 real evidence、protected-path/fixture/cleanup/process safety checks 及全部已知限制。
- Phase 5 overall 现为 **FORMALLY ACCEPTED / PASS / COMPLETE**。Phase 6 保持 **NOT STARTED**，不得从本次文档同步自动开始。
- 完整正式 acceptance 记录见 [PHASE_05_11_FINAL_ACCEPTANCE.md](phases/PHASE_05_11_FINAL_ACCEPTANCE.md)。

## Phase 6.4 Minimal Workspace-Launch Diagnostic（2026-09-05）

- Gate Keeper 授权的本机 `Cursor.exe --help` 未提供帮助而启动 Cursor，诊断实例已优雅关闭。随后尝试 `Cursor.exe --new-window "D:\ArcGIS-Pro-MCP"`；实际参数含带空格的工作区参数，窗口仍为 `Cursor Agents`，未证明工作区加载。
- 主窗口 PID `29016` 的 `CloseMainWindow()` 在 30 秒有界等待内未退出；未强制终止。按 Gate 规则，当前分类为 **BLOCKED_BY_CLIENT_PROCESS**；未启动 runtime、未执行 `tools/call`、未进入 Phase 7。
- 最终 post-check：Cursor/ArcGIS Pro/dotnet/6511/6520=0；配置、protected APRX、controlled APRX、marker/manifest 与 GDB inventory/locks 未变化。此前 **BLOCKED_BY_CLIENT_LOG_VISIBILITY** 结果保留为历史证据。

## Phase 6.4 Gate Keeper Workspace Correction and Pro-First Recovery（2026-09-05）

- Gate Keeper 确认会话 `20260905T143455` 为有效工作区：workspaceId 非空、`workspacePaths=d:\ArcGIS-Pro-MCP`、`projectServers=1`，并识别 `project-0-ArcGIS-Pro-MCP-arcgis-pro-mcp`。
- 最终 Pro-first 会话 `20260905T144521` 先启动 controlled Pro PID `24088` 到达 6520，再启动 Cursor PID `26032`；Cursor 仍未暴露可审计 connection success、successful initialize/session、`tools/list` 或 exact `tools/list=30`。
- 结果保持 **BLOCKED_BY_CLIENT_LOG_VISIBILITY**；未执行 `tools/call`。Cursor 与受控 Pro 均已优雅退出，最终 post-check 无 Cursor/ArcGIS Pro/dotnet/6511/6520；保护资产与 GDB inventory/locks 未变化。此前 **BLOCKED_BY_CLIENT_PROCESS** 保留为历史诊断结果。

## Phase 6 Codex P0 Project Configuration Recognition（2026-09-05）

- 客户端优先级已迁移为 Codex=P0 首选必验、Cursor=P1 必验、DeepSeek Harness=P1 必验、Claude Desktop=P2 可选。旧 Cursor 6.1–6.3 PASS 与 6.4 `BLOCKED_BY_CLIENT_LOG_VISIBILITY` 保留；Phase 6 仍未完成，Phase 7 未开始。
- 已创建项目级 `D:\ArcGIS-Pro-MCP\.codex\config.toml`，内容仅为：

```toml
[mcp_servers.arcgis-pro-mcp]
url = "http://127.0.0.1:6520/mcp"
```

- Codex CLI=`codex-cli 0.153.0`；`codex mcp list` 明确列出 `arcgis-pro-mcp` 与正确 URL，状态 `enabled`，无 token/credential/provider 配置。Codex 配置识别 = **PASS**。
- config SHA-256=`2E4A919F011A0D8F5F175B7F85C0E5236C0923D67BDC632EF331221E17AD55E2`，63 bytes。
- Codex connection / initialize / exact `tools/list=30` / read-only `tools/call` = **NOT VERIFIED**；本轮没有启动 Codex MCP、ArcGIS Pro、Cursor 或 dotnet。下一唯一执行 Gate：Codex P0 MCP connection / initialize / tools-list / authorized read-only call。

## Phase 6 Codex P0 MCP Connection / Initialize / Exact Tools List / Safe Read-only Call（2026-09-05）

- Pro-first 顺序已完成：retained controlled APRX 启动时 ArcGIS Pro PID=`4064`，6520 listener=`1`，6511=`0`；Codex 客户端验证完成后 Pro 已优雅退出，最终 ArcGISPro/dotnet/6511/6520 均为 `0`。
- 新 Codex 客户端：Codex CLI `0.153.0`；命令 `codex exec --json --ephemeral --approve-for-me -C D:\ArcGIS-Pro-MCP -`；cwd=`D:\ArcGIS-Pro-MCP`；项目配置 SHA-256=`2E4A919F011A0D8F5F175B7F85C0E5236C0923D67BDC632EF331221E17AD55E2`。
- Fresh client evidence：`arcgis-pro-mcp` connection=`SUCCEEDED`；initialize/equivalent session=`SUCCEEDED`；`tools/list=30 unique tools`；完整名称集合见 [PHASE_06_CODEX_P0_MCP_CONNECTION_GATE.md](phases/PHASE_06_CODEX_P0_MCP_CONNECTION_GATE.md)。唯一只读调用为 `mcp__arcgis_pro_mcp__ping({})`，返回 `pong`、`isError=false`。
- Codex exec JSONL 未单独暴露 raw `initialize` envelope；严格分类为 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**，不是 Formal PASS。未调用 mutation tool。
- 保护核验：`MyProject1.aprx`、retained marker/manifest、retained GDB（65/0 locks）和 shared `Phase4Test.gdb`（104/0 locks）均未改变。详细报告：[PHASE_06_CODEX_P0_MCP_CONNECTION_GATE.md](phases/PHASE_06_CODEX_P0_MCP_CONNECTION_GATE.md)。

## Phase 6 Cursor P1 MCP Connection / Initialize / Exact Tools List / Safe Read-only Call（2026-09-05）

- 实际客户端：Cursor `3.19.13`，命令行启动 `Cursor.exe --new-window "D:\ArcGIS-Pro-MCP"`，新会话日志目录为 `<user-home>\AppData\Roaming\Cursor\logs\20260905T210108`；`.cursor/mcp.json` 为 97 bytes，SHA-256=`C5CF47ED4759FDBF391838C112971BAB5C6A3F6BD135F846675AD708C5AD135B`。
- Pro-first：controlled Pro PID=`29764` 先到达 6520，6511=`0`；完成后 Cursor/ArcGISPro/dotnet/6511/6520 均为 `0`。
- Cursor 工作区 `D:\ArcGIS-Pro-MCP` 和 project server 数=`1` 已识别；目标 `arcgis-pro-mcp` 在 Cursor 日志中为 `disconnected`。未发现成功 connection、initialize/session、`tools/list`、exact 30-tool set 或 `tools/call`。
- 安全只读诊断已到边界；未发现可用 Cursor MCP/agent CLI 或导出接口。分类为 **BLOCKED_BY_USER_UI**，不将 Codex 证据或 server-side evidence 代替 Cursor 客户端证据。
- 下一步需用户在 Cursor MCP UI 中对目标 server 执行连接/重连并取得自身客户端证据；不登录、不读凭据、不 mutation。报告：[PHASE_06_CURSOR_P1_MCP_CONNECTION_GATE.md](phases/PHASE_06_CURSOR_P1_MCP_CONNECTION_GATE.md)。
