# ArcGIS-Pro-MCP — 验证记录（VERIFICATION）

> **历史时点声明（D-126 项目完结批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **2026-09-10（文内最晚 ISO 日期实测；正文跨度 2026-09-01…2026-09-10）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。

> 只保存**最终重要结果**，不复制巨大日志。

## Current Status（2026-09-07）

### Current Phase 7 Final Project Acceptance（2026-09-07）

Independent Gate Keeper 已正式接受 Phase 7 overall、1.0.2 planned release scope 与 implemented canonical 30-tool whole-project scope 为 **FORMALLY ACCEPTED / PASS / COMPLETE**。本节只记录正式接受与证据边界；不重跑 Build/Test，不启动 runtime/client/test/RegisterAddIn，不修改配置、registry、GIS 数据、retained fixture、installed package、历史输出或锁。

- Phase 7.7 overall = **FORMALLY ACCEPTED / PASS**；Phase 7.8 = **FORMALLY ACCEPTED / PASS**；Phase 7 overall = **FORMALLY ACCEPTED / PASS / COMPLETE**；1.0.2 planned release scope / implemented canonical 30-tool whole-project scope = **FORMALLY ACCEPTED / PASS / COMPLETE**；未来 `112+` expansion = **NOT IMPLEMENTED / NOT ACCEPTED**。
- 当前报告：[PHASE_07_FINAL_PROJECT_ACCEPTANCE.md](phases/PHASE_07_FINAL_PROJECT_ACCEPTANCE.md)。不可变 pre-acceptance candidate：[PHASE_07_FINAL_PROJECT_ACCEPTANCE_CANDIDATE.md](phases/PHASE_07_FINAL_PROJECT_ACCEPTANCE_CANDIDATE.md)。正式记录：final-project-acceptance-record.json（历史归档；文件不在当前工作树）。Phase 7.8 acceptance：[PHASE_07_8_FINAL_HANDOFF_CANDIDATE.md](phases/PHASE_07_8_FINAL_HANDOFF_CANDIDATE.md)。发布说明：[RELEASE_NOTES_1.0.2.md](RELEASE_NOTES_1.0.2.md)。
- Accepted identity：installed/source/manifest `1.0.2` exact match，269548 bytes，SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`；canonical production tool count=`30`，`mcp_auth` excluded。
- Final handoff index preserves 27 exact path/byte/SHA entries, current ledger `5/5 PASS`, historical rollback semantics `7/7 PASS`, protected asset/config/process/listener snapshot, client priority matrix and exact limitation classifications。
- raw-surface、PARTIAL/LIMITED、NOT VERIFIED、BLOCKED_BY_HARNESS、clean-machine、`ArcGISProject.isDirty`、HTTP cancellation、DeepSeek 当前 not-running/reconnection-not-verified 和 `select_layer` no-OID limitation 继续保持原分类；Claude Desktop 仍为 optional/template-only。已接受范围已完成，未来 `112+` expansion 不属于本次接受范围。
- Final read-only state audit (`2026-09-07T22:54:53.5312216+08:00`) records accepted historical DeepSeek `PID 20424 / node / port 3080`; current PID `20424` is absent and port `3080` is clear. Cause/actor is **NOT VERIFIED / external user-owned state change**. The launcher shortcut and five profile/config hashes match the accepted snapshots; no credentials were read or output, and no restart or fresh connection was performed.

## Separate One-click Deployment Share Package Candidate（2026-09-10）

本节记录新的独立 deployment candidate 及 Independent Gate Keeper 的有限放行，不改变上方 Phase 7 `FORMALLY ACCEPTED / PASS / COMPLETE` 的历史/基础结论，也不把有限放行写成真实安装、真实客户端连接、clean-machine 或公开发布的整体 formal acceptance。

- Candidate status=`INDEPENDENT GATE PASSED / AUTHORIZED FOR USER REAL-MACHINE TRIAL`；current ZIP=`Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip`；size=`331679` bytes；SHA-256=`3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652`；同名 `.sha256` 与实际 ZIP 一致，原 accepted 1.0.2 ZIP 未覆盖。该有限决定只覆盖 r5 实现与本机隔离自动化验证；真实安装、真实 AI 客户端连接、clean-machine 和公开发布仍 `NOT VERIFIED`。独立审批记录：[ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md](ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md)；独立 evidence root：`.runtime/one-click-deployment-r5/run-20260910-052628-6f6d5d15ae56481092d378c74dd8e856`。上一版 r4 candidate 保留在 `330602` bytes / SHA-256=`AE9C5EF2421D4E73F8BA62AEA5FBFEF2B4EFF0DA7D2EF24B01EED93D3103A134`；r3 candidate 保留在 `329568` bytes / SHA-256=`7EEC03C0E0DAD1F3363147FE614B72C0CB41DB3A80262CC76453F4AAC8B1E3EA`；Rejected r1 remains preserved at `316207` bytes / SHA-256=`3303E02CFAC50EB02B74F4C0B4BDA77C3BE2788607920EE1402B43B19102D7B1`。原始 r2 review hash=`CC1CC37C8B00918313203E45656183A9FE6656D0E9D1FC664D096349D45AFD53` 与后续同名 r2 hash=`33AC18A10E2DF4EA7D0D0BE646C777BC2EB535D8C552F1EA846B75956936DABF` 仅作为区分的历史/候选证据。
- r1 Gate Keeper rejection is preserved：GUI repeated starts left status at `执行中`, target empty, and exposed PowerShell worker/null-array errors；r1 is historical failure evidence, not current PASS evidence。r2、r3、r4 的修复与历史证据继续保留；r5 adds PS5.1-safe UTF-8 entry encoding, .NET hash fallback in the package client configurator/test harness, and ProcessStartInfo-based synchronous child exit capture while preserving the real cancel-callback, child-completion hard-interruption recovery, pending-index restart blocking, and recovery-index write-failure/old-latest checks。生产 Python Bridge lifecycle semantics 未修改。
- Bundle manifest/verifier=`PASS`；r5 physical file count=`23` (manifest-listed files plus manifest)；embedded accepted payload=`269548` bytes、SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`；endpoint=`http://127.0.0.1:6520/mcp`；canonical production tools=`30`；`cleanMachineAcceptance=NOT VERIFIED`。Windows PowerShell 5.1 verifier raw evidence=`PASS`，stderr 为空。
- Fresh r5 verification：one-click owned-temp harness=`122/122 assertions PASS`；exact Windows PowerShell 5.1 entry command `EXIT_CODE=0`；explicit UTF-8 bootstrap/parent decode and dot-source scope preserve top-level Chinese/space GUI paths without U+FFFD replacement characters；actual ZIP and Chinese/space extracted root audits=`PASS`；preset GUI smoke=`PASS_SIMULATED_PRESET_ONLY`；final extracted-package real read-only button callbacks=`PASS_REAL_READ_ONLY_CALLBACKS`（3 screenshots、完整中文路径 InstallRoot + GUID 实际显示、`Preflight=PASS`、`Diagnose=WAITING_USER_START`、中文消息原样往返且无替换字符、controls/target/no-dialog/worker exits PASS）；cancellation callback、owned child-completion hard interruption、pending recovery-index restart block、index+ledger rollback、fresh transaction、recovery-index write failure with distinct old latest=`PASS`。Fresh solution baseline：Build `0 errors / 3 NU1900`；Unit `233 PASS / 3 SKIP / 0 FAIL`（236 total）；Integration `23/23`；Server `41/41`。失败运行的完整 owned-temp artifacts 已在清理前复制保留。
- Builder/test boundary：registration=`SKIPPED`；real installation、real client mutation、real ArcGIS/Bridge/MCP runtime=`NOT PERFORMED`；clean-machine、real GUI click-through、real client connection=`NOT VERIFIED`。没有启动或强制终止用户程序；hard-interruption 只强制终止 owned test child process。
- Retained fixture identity was read before documentation: actual marker and manifest both identify `P57_B8C6FE8E`；fixture not recreated/renamed/moved/modified，semantic health carry-forward only。`TestDate/Phase4Test.gdb` read-only post-check=`104 files / 0 locks`。
- r5 local persistent evidence：`.runtime/one-click-deployment-r5/run-20260910-044724-cf90381b4f1c4b6b82d881d25cc7888b`；Independent Gate Keeper evidence：`.runtime/one-click-deployment-r5/run-20260910-052628-6f6d5d15ae56481092d378c74dd8e856`；fresh baseline：`.runtime/one-click-deployment-r5/baseline-20260910-032900`；PS5.1 entry evidence：`.runtime/one-click-deployment-r5/ps51-entry-validation-20260910-044723-unicode-fix`；PS5.1 package verifier：`.runtime/one-click-deployment-r5/ps51-package-verifier-20260910-032800`；protected/process/listener post-check=`run-20260910-044724-cf90381b4f1c4b6b82d881d25cc7888b/49-post-check.json`；其中包含 numbered command reports、package hash、PS5.1 raw stdout/stderr/metadata、simulated 与 real-button GUI evidence、中文 round-trip assertions、recovery/cancellation/hard-interruption/index-write-failure reports、prior failed-entry diagnostics 与 r4 historical references。Unicode 修复前后的失败 diagnostics 均保留，最近一次失败 run 的完整 owned-temp snapshot 见 `run-20260910-044131-5d0b13e51ab74d15b0fef348813c3f62/failure-artifacts.json`。独立审批记录：[ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md](ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md)。r3 evidence root 与 r2 roots remain historical and distinct。详细计划/证据索引/候选报告：[ONE_CLICK_DEPLOYMENT_PLAN.md](ONE_CLICK_DEPLOYMENT_PLAN.md)、[ONE_CLICK_DEPLOYMENT_EVIDENCE_INDEX.md](ONE_CLICK_DEPLOYMENT_EVIDENCE_INDEX.md)、[ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md](ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md)。

### Historical Phase 7.7.4.4 Multi-client Consolidation（2026-09-07）

Independent Gate Keeper 已正式接受 Phase 7.7 Entry Preflight、7.7.1、7.7.2、7.7.3、7.7.4.1、7.7.4.2 与 7.7.4.3 为 **FORMALLY ACCEPTED / PASS**。Phase 7.7.4.4 Multi-client Consolidation 已完成，当前为 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**。

- Phase 7 = **IN PROGRESS**；7.6.2 = **FORMALLY ACCEPTED / PASS**；7.6 overall = **FORMALLY ACCEPTED / PASS**；7.7 Entry Preflight/7.7.1/7.7.2/7.7.3/7.7.4.1/7.7.4.2/7.7.4.3 = **FORMALLY ACCEPTED / PASS**；7.7.4.4 = **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；7.7.5+ = **NOT STARTED**。
- Phase 7.6 formal acceptance basis：focused `16/16 PASS`；Unit `232 PASS / 3 SKIP / 0 FAIL`；Integration `23/23`；Server `41/41`；Build `0 errors / 3 NU1900`；production registry `30`；1.0.2 package exact audit、registration `SKIPPED`；protected-state post-check matched。
- Fresh 7.7.1 result：focused release/workflow/runtime/logging/client/document policy `152 PASS / 3 SKIP / 0 FAIL`（155 total）；Unit `232 PASS / 3 SKIP / 0 FAIL`（235 total）；Integration `23/23`；Server `41/41`；Build `0 errors / 3 NU1900`；ProductionToolContract `11/11`；registry `30`、`mcp_auth` excluded；package-only 1.0.2 exact audit passed、registration `SKIPPED`。
- Historical 7.7.2 was bounded to exact official Add-in target mutation and installed package match; its formal acceptance is recorded above. 7.7.3、7.7.4.1、7.7.4.2 和 7.7.4.3 均已正式接受。7.7.4.4 documentation/evidence consolidation 已完成且只保留 candidate 结论；no protected GIS/project/client-config mutation is permitted，7.7.5+ 不得开始。
- clean-machine support 和后续 client formal acceptance 尚 **NOT VERIFIED**；7.7.4.1 的 raw envelope/session identity limitation 与前两次 approval refusal 继续原样保留，第三次 remediation 已由 Independent Gate Keeper 正式接受。Cursor raw envelopes 的限制已在 7.7.4.2 正式验收中按 initialize-equivalent 规则保留。旧 `BLOCKED_BY_HARNESS` 和 capability-aware `SKIPPED` 继续原样保留。

7.7.4.1 formal acceptance：Codex CLI `0.153.0` session 的 `ping`、`python_bridge_ping`、`get_arcgis_version` 均 `isError=false`；Gate Keeper 接受客户端初始 canonical 30-name catalog（distinct 30、duplicate 0、`mcp_auth` absent）为 initialize-equivalent live discovery。raw envelope/session identity 未暴露仍为非阻断限制；Bridge PID 17344 的官方 Python/AssemblyCache 路径已由 instrumentation 记录。证据 root 为 `.runtime/phase77_4_1_codex/run_768234A4148B4B83A33C77344958EEA2`。

7.7.4.2 Cursor P1 evidence：fresh Cursor 3.19.13 的 project snapshot 为 `connected`、`toolCount=30`，同一客户端 `ping({})` 返回 `pong` 且 `isError=false`；Cursor 与 ArcGIS Pro 均 graceful close，post-check=`PASS`。其 raw initialize/tools-list/name envelope limitation 已由 Independent Gate Keeper 按 initialize-equivalent 规则正式接受为 **FORMALLY ACCEPTED / PASS**。

7.7.3、7.7.4.1、7.7.4.2 and 7.7.4.3 formal PASS evidence is preserved in their dedicated reports. The Codex remediation used the canonical config plus an ephemeral server-scoped approval override; all three required client calls passed, and the Gate Keeper accepted the 30-name catalog as initialize-equivalent live discovery while retaining the raw-envelope limitation. The Cursor P1 evidence was formally accepted with its raw-surface limitation. The DeepSeek P1 evidence was formally accepted with exact raw names=`30`、distinct=`30`、duplicate=`0`，all under `arcgis-pro-mcp`; one same-session `ping({})` returned `pong` / `isError=false`；Session ZIP verified exactly one MCP call/result. All three client gates are now formally accepted; 7.7.4.4 only consolidates them and does not rerun runtime operations。

7.7.2 formal acceptance evidence：新 owned transaction root `phase77_2_AB60AFC410CA47719330397DFBFC5012`，explicit ledger；workflow/delegated transaction `Install=PASS`，release ledger `dryRun=false`、`testMode=false`、5 个事件全部 `PASS`。官方 `Esri RegisterAddIn.exe /s` event 的 `exitCode=0`。installed package=`269548` bytes，SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`，与 1.0.2 source package exact match；target locks=`0`。post-check 中 APRX、Phase4Test.gdb、retained fixture、repository Codex/Cursor config、DeepSeek PID 20424/3080、6511/6520 listener 状态均保持基线；ArcGIS/Bridge/client/MCP 未启动。Independent Gate Keeper 已正式接受 7.7.2。

当前报告：[PHASE_07_7_4_4_MULTI_CLIENT_CONSOLIDATION.md](phases/PHASE_07_7_4_4_MULTI_CLIENT_CONSOLIDATION.md)。当前结论为 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；7.7.5+ 未开始。7.7.4.3 formal acceptance：[PHASE_07_7_4_3_DEEPSEEK_P1_FRESH_CLIENT_GATE.md](phases/PHASE_07_7_4_3_DEEPSEEK_P1_FRESH_CLIENT_GATE.md)。7.7.4.2 acceptance：[PHASE_07_7_4_2_CURSOR_P1_FRESH_CLIENT_GATE.md](phases/PHASE_07_7_4_2_CURSOR_P1_FRESH_CLIENT_GATE.md)。

### Historical Phase 7.6.1 Release Portability Foundation（2026-09-06）

Independent Gate Keeper 已正式接受 Phase 7.6 Entry Preflight 为 **FORMALLY ACCEPTED / PASS**。本轮 fresh 结果为 Phase 7.6.1 **IN PROGRESS / PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**，不是正式 Phase 7.6.1 acceptance。

- Phase 7 = **IN PROGRESS**；7.5.3/7.5 overall = **FORMALLY ACCEPTED / PASS**；7.6 Entry Preflight = **FORMALLY ACCEPTED / PASS**；7.6.1 = **PASS CANDIDATE**；7.6.2/7.7+ = **NOT STARTED**。
- Build：`0 errors / 3 NU1900`；focused portability/health/package tests：`65 PASS / 3 SKIP`，total `68`；Unit：`216 PASS / 3 SKIP / 0 FAIL`，total `219`；Integration `23/23`；Server `41/41`；production registry `30`。
- Release token gate：canonical `major.minor.patch` 与 adversarial token rejection 均 PASS；`RuntimeRoot` 同时被验证为 `LocalApplicationData/ArcGISProMCP` 和 `LocalApplicationData/ArcGISProMCP/1.0.2` 的 descendant。
- Package-only `-SkipRegistration`：release candidate `1.0.2`，20 normalized entries，Bridge exact logical path `Install/PythonBridge/bridge_runner.py` count `1`，length `15494`，SHA-256=`40B2A00061EB35EA83A7729E9E56FC020B020C2476E2D60123CA2356B33A6CA7`；package size `269548`，SHA-256=`361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`；RegisterAddIn 未调用。
- Protected post-check：`MyProject1.aprx` SHA-256=`ECF25A8791219435CE9AC44F7A50B28468074ACDF96C9F55455106FB376BCD14`；shared GDB=`104/0 locks`；retained marker/manifest actual RunId=`P57_B8C6FE8E`；installed 1.0.1 package=`202613` bytes，SHA-256=`62FBE89CAE9F03077C797A146662AFC81ED29E59539327D85FBC190A179B8D65`；6511/6520 无 listener。
- `list_maps/get_map_info`、dataset/raster limited implementation、`isDirty` unavailable、HTTP client-disconnect cancellation、7 个历史 HTTP harness blockers、`select_layer` 无 OID contract 和 reparse capability skips 均继续原样保留；installed runtime/UI/path behavior = **NOT VERIFIED**，转交 7.7。
- 详细报告：[PHASE_07_6_1_RELEASE_PORTABILITY_FOUNDATION.md](phases/PHASE_07_6_1_RELEASE_PORTABILITY_FOUNDATION.md)。

### Historical Phase 7.6 User Documentation and One-click Materials — Entry Preflight（2026-09-06）

Phase 7.5.3 与 Phase 7.5 overall 已由 Independent Gate Keeper 正式接受为 **FORMALLY ACCEPTED / PASS**（限授权实现与自动化证据范围）。当前只执行 7.6 Entry Preflight / audit-design。

当前状态：7.5.3 = **FORMALLY ACCEPTED / PASS**；7.5 = **FORMALLY ACCEPTED / PASS**；7.6 Entry Preflight = **IN PROGRESS / PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；7.7+ = **NOT STARTED**。

- Phase 7.4 formal acceptance basis: Build `0 errors / 3 NU1900`；focused client policy `18/18`；Unit `108/108`；Integration `20/20`；Server `41/41`；actual installed target Add-in ID `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}`。
- Historical registered-package backup prefix `7CEB2A...` is retained as supplied evidence；`A97AE2...` remains another historical package hash, not an installation GUID。

Phase 7.5.1 formal acceptance: **FORMALLY ACCEPTED / PASS**。Phase 7.5.2 formal acceptance: **FORMALLY ACCEPTED / PASS**。历史 candidate 与原始 evidence 保留在各自 phase reports；不重写历史证据。

Phase 7.5.3 formal acceptance report：[PHASE_07_5_3_STRUCTURED_LOGGING_DIAGNOSTIC_EXPORT.md](phases/PHASE_07_5_3_STRUCTURED_LOGGING_DIAGNOSTIC_EXPORT.md)。7.6 Entry Preflight report：[PHASE_07_6_USER_DOCUMENTATION_PREFLIGHT.md](phases/PHASE_07_6_USER_DOCUMENTATION_PREFLIGHT.md)。7.5.2 acceptance report：[PHASE_07_5_2_RUNTIME_HEALTH_STATUS_UX.md](phases/PHASE_07_5_2_RUNTIME_HEALTH_STATUS_UX.md)。

- Fresh solution build：`0 errors / 3 NU1900`。
- Focused 7.5.3：`15 total = 13 PASS + 2 capability-aware SKIPPED`；full Unit：`188/188 PASS + 2 SKIPPED (190 total)`；Integration：`23/23 PASS`；Server：`41/41 PASS`。两项 reparse-point/ancestor tests 因当前环境创建 directory symbolic link 返回 `IOException` 而 skipped，未宣称 reparse behavior executed PASS；F1 exact cleanup and F2 adversarial identifier tests executed PASS。
- Production registry：`30`，未新增 tool；没有启动 ArcGIS/Server/Bridge/client，没有真实 UI runtime observation。UI runtime 与 installed production managed-sink/export observation = `NOT VERIFIED`，转交 Phase 7.7。
- Protected-state read-only post-check：MyProject1.aprx、shared Phase4Test.gdb `104/0 locks`、retained fixture `P57_B8C6FE8E`、installed package、repository-scoped client configs 和 legacy log timestamps unchanged；6511/6520 无 listener。

当前 7.6 只允许文档/脚本/包/配置入口盘点、冲突分类、路径可移植性审计和设计；不得实现 one-click wrapper，不得执行 ArcGIS/SDK/ArcPy/map/project/file/process/TCP probe，不得启动 runtime/client，不得把历史 evidence 冒充本轮 PASS。

预检已发现 `Composition.RepoRoot`、`PythonBridgeScript`、`PythonWorkingDirectory` 和 managed-log destination 绑定到 `D:\ArcGIS-Pro-MCP`，且当前 `.esriAddInX` 19-entry package 不包含 `bridge_runner.py`。分类：`RELEASE_PORTABILITY_BLOCKER`；因此当前不宣称 clean-machine support、one-click installation 或 self-contained package。

配置复核更正：此前检查用户 profile 路径属于 **VERIFICATION_PATH_ERROR**，不是配置漂移。正确 repository-scoped `D:\ArcGIS-Pro-MCP\.codex\config.toml` SHA-256=`2E4A919F011A0D8F5F175B7F85C0E5236C0923D67BDC632EF331221E17AD55E2`、`D:\ArcGIS-Pro-MCP\.cursor\mcp.json` SHA-256=`C5CF47ED4759FDBF391838C112971BAB5C6A3F6BD135F846675AD708C5AD135B`；DeepSeek hash=`0BC84530A43B06A7FFE789F7C0369F3B973A264F9EA2A8D84B41206683C7ED4B`。无相关 `.tmp/.bak/.meta`、baseline 或 backup sidecar，外部配置未修改。

- Phase 7.4 formal acceptance basis: Build `0 errors / 3 NU1900`；focused client policy `18/18`；Unit `108/108`；Integration `20/20`；Server `41/41`；actual installed target Add-in ID `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}`。
- Historical registered-package backup prefix `7CEB2A...` is retained as supplied evidence；`A97AE2...` remains another historical package hash, not an installation GUID。

当前 checker 只以实际 Windows/x64、ArcGIS Pro/SDK、.NET 8、ArcGIS Python/ArcPy、release manifest 和 Config.daml 事实判定，不读取项目/GIS 数据。

### Historical Phase 7.1 verification（2026-09-05）

Phase 7.1 Package and Release Identity = **FORMALLY ACCEPTED / PASS**。Phase 7.2 Install/Uninstall/Rollback = **FORMALLY ACCEPTED / PASS**；Phase 7.3 = **IN PROGRESS / PASS CANDIDATE pending review**；Phase 7.4+ = **NOT STARTED**。

- Fresh build: `0 errors / 3 NU1900`；Unit `72/72`、Integration `20/20`、Server `41/41`。
- Package-only command: `scripts/package-addin.ps1 -Configuration Debug -SkipRegistration`，exit `0`；`RegisterAddIn.exe` 未调用。
- Package: `202887` bytes；SHA-256 `C5541E428476A14B25C983FF67F549617F64C20EF4651348DE50D4962049CA7E`。Independent ZIP audit: 19 entries, 8 first-party DLLs, Config.daml/Icon/main DLL present, nested `.esriAddInX=0`。
- Identity: Config.daml/package/release manifest `1.0.1` / AddIn ID `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}` / ArcGIS Pro `3.5.0` consistent；first-party DLLs CLR `AssemblyVersion=1.0.0.0`、`FileVersion=1.0.1.0`、`ProductVersion=1.0.1`；the AssemblyVersion is an intentional compatibility identity。
- Manifest schema `arcgis-pro-mcp-release-manifest-v1`；8 个 DLL 的 source hash 与 ZIP entry、CLR AssemblyVersion、FileVersion 和 ProductVersion independent match；无 absolute path/user identity/self-reference。
- Registered copy remains unchanged at carried-forward SHA-256 `62FBE89CAE9F03077C797A146662AFC81ED29E59539327D85FBC190A179B8D65`；protected APRX hash and shared GDB file/lock counts remained unchanged。
- Added negative/positive release-policy tests: mismatch fails closed before registration；package-only produces manifest without registration。

Detailed report: [PHASE_07_1_PACKAGE_RELEASE_IDENTITY.md](phases/PHASE_07_1_PACKAGE_RELEASE_IDENTITY.md)。Current 7.2 report: [PHASE_07_2_INSTALL_UNINSTALL_ROLLBACK.md](phases/PHASE_07_2_INSTALL_UNINSTALL_ROLLBACK.md)。

> 客户端连接事实保持不变：Codex P0、Cursor P1 与 DeepSeek Harness P1 MCP connection / initialize-equivalent session / canonical tools-list=30 / safe read-only ping 均 **FORMALLY ACCEPTED / PASS**。Phase 6 = **FORMALLY ACCEPTED / PASS / COMPLETE**；Phase 7 = **IN PROGRESS**；Phase 7.1/7.2 = **FORMALLY ACCEPTED / PASS**；Phase 7.3 = **IN PROGRESS / PASS CANDIDATE**，Phase 7.4+ 尚未开始。

DeepSeek P1 专项报告：[PHASE_06_DEEPSEEK_HARNESS_P1_PREFLIGHT.md](phases/PHASE_06_DEEPSEEK_HARNESS_P1_PREFLIGHT.md)。

### Current DeepSeek MCP gate（2026-09-05）

Independent Gate Keeper formally accepted **DeepSeek Harness P1 Identity / Launcher / Local Runtime Availability Preflight = PASS** and later accepted the same-client MCP gate = **PASS**. In namespace `arcgis-pro-mcp`, the auditable unique server set is 30/30 with missing 0 and unexpected 0; the only call was read-only `ping({}) → pong`. The prior task-board ledger **BLOCKED_BY_USER_UI** is historical and superseded. Detailed report: [PHASE_06_DEEPSEEK_HARNESS_P1_MCP_CONNECTION_GATE.md](phases/PHASE_06_DEEPSEEK_HARNESS_P1_MCP_CONNECTION_GATE.md)。

### Current Phase 7 entry boundary（2026-09-05）

Phase 6 overall = **FORMALLY ACCEPTED / PASS / COMPLETE**。Phase 7 = **IN PROGRESS / ENTRY PREFLIGHT / PLAN CANDIDATE**；implementation = **NOT STARTED**。See [PHASE_07_ENTRY_PREFLIGHT.md](phases/PHASE_07_ENTRY_PREFLIGHT.md) and [PHASE_07_IMPLEMENTATION_PLAN.md](phases/PHASE_07_IMPLEMENTATION_PLAN.md)。

### Current DeepSeek state reconciliation（2026-09-05）

产品身份、启动器、local runtime 与同一客户端 MCP gate 均已由 Independent Gate Keeper 接受；`BLOCKED_BY_EXTERNAL_CLIENT / CLI_NOT_ON_PATH / RUNTIME_NOT_VERIFIED` 与 `BLOCKED_BY_USER_UI` 保留为历史快照，不是当前 blocker。

> 下方旧的客户端汇总保留为历史快照；当前权威状态以上述 DeepSeek preflight correction 为准。

- **Phase 5.11 = FORMALLY ACCEPTED / PASS**；Phase 5.7 = **PASS**；Phase 5.8 preflight = **COMPLETE / ACCEPTED**；Phase 5.8.1–5.8.5、Phase 5.8 overall、Phase 5.9 和 Phase 5.10 均已由 Independent Gate Keeper 正式接受为 PASS；Phase 5 overall = **FORMALLY ACCEPTED / PASS / COMPLETE**；Phase 6 Entry Preflight = **FORMALLY ACCEPTED / PASS**；client priority = **Codex P0 required、Cursor P1 required、DeepSeek Harness P1 required、Claude Desktop P2 optional**；Codex project configuration recognition = **PASS**；Codex P0 connection/initialize-equivalent session/tools-list=30/safe read-only ping = **FORMALLY ACCEPTED / PASS**；raw initialize envelope not separately exposed（non-blocking）；other Codex tools/call = **NOT VERIFIED**；Phase 6.1–6.3 Cursor = **FORMALLY ACCEPTED / PASS**；Phase 6.4 Cursor = **BLOCKED_BY_CLIENT_LOG_VISIBILITY**（historical evidence retained）；Cursor P1 connection gate = **BLOCKED_BY_USER_UI**；Cursor connection/initialize/tools-list/tools-call = **NOT VERIFIED**；Phase 6 = **IN PROGRESS / BLOCKED_BY_USER_UI**；Phase 7 = **NOT STARTED**。
- Latest Phase 5.10 fresh automated results：Unit **68/68 PASS**、Integration **20/20 PASS**、MCPProtocol **9/9 PASS**、MCPServer **25/25 PASS**、MCPTransport **7/7 PASS**、完整 Server **41/41 PASS**；Build **0 errors / 3 NU1900**。
- Final discovered inventory：129 distinct Facts；`PythonBridgeLifecycleTests` 16/16 连续 3 次 PASS；历史 Case H / `BLOCKED_BY_HARNESS` 记录保留，但当前 host fresh transport 为 7/7 PASS。
- Latest Phase 5.8.3 real evidence：controlled `Phase58Controlled.aprx` 上四个生产 GP 与五个指定 production persistent Python Bridge tool 均完成 fresh verification；四个 owned outputs 保留给 Phase 5.8.4。
- 详细阶段报告：[`PHASE_05_8_3_GP_ARCPY_REAL_VERIFICATION.md`](phases/PHASE_05_8_3_GP_ARCPY_REAL_VERIFICATION.md)；历史 Phase 5.7.5 报告：[`PHASE_05_7_5_TEST_ACCEPTANCE.md`](phases/PHASE_05_7_5_TEST_ACCEPTANCE.md)。

## Phase 5.8.2 Native Real Verification（2026-09-04）

- 详细 32 项报告：[`PHASE_05_8_2_NATIVE_REAL_VERIFICATION.md`](phases/PHASE_05_8_2_NATIVE_REAL_VERIFICATION.md)。
- 专用 ArcGIS Pro PID 2796 显式打开 retained owned APRX，活动地图 fresh 返回 Phase58_TestMap；Native read、visibility mutation/restore、selection current-contract、clear_selection、remove_layer、owned-path add_layer 和实际 feature read 均已验证。
- list_maps/get_map_info 复现 PARTIAL；get_dataset_info 对 GDB 内部 FeatureClass 复现 DATASET_NOT_FOUND，保持 LIMITED IMPLEMENTATION；select_layer 返回 count=0，保持 PARTIAL/LIMITED；add_layer 恢复 membership/visibility/usable layer/no duplicate，但无法通过当前 contract 恢复原层序。
- 未执行 GP、Phase 5.8.3、Phase 5.9 或 full regression；未修改 production code、MyProject1.aprx、Phase4Test.gdb 或 retained fixture。专用 runtime 已关闭且未保存。

**PHASE 5.8.2 PASS CANDIDATE**；等待 independent Gate Keeper，不进入下一 Phase。

## Phase 5.8.2 Formal Acceptance（2026-09-04）

- Independent Gate Keeper：**FORMALLY ACCEPT — PHASE 5.8.2 PASS**。
- Selection correction: select_layer = **PARTIAL / LIMITED IMPLEMENTATION**；clear_selection execution path observed, but true non-zero → zero selection mutation = **NOT VERIFIED**。此前报告中的历史调用结果保留，本文不将 clear_selection 升格为已验证的 non-zero→zero PASS。
- Phase 5.8.3 GP / ArcPy Real Verification：**AUTHORIZED / READY TO START**。本次 reconciliation 未执行 GP/ArcPy、未创建输出、未进入 Phase 5.8.4 或 Phase 5.9。

## Phase 5.8.3 GP / ArcPy Real Verification（2026-09-04）

- 当前状态：**PASS**（independent Gate Keeper formally accepted）。本 Phase 已执行四个生产 GP 工具和 Gate 指定的生产 Python Bridge 工具；Phase 5.8.4、Phase 5.9 及后续阶段未开始。
- 当前使用 retained fixture `P57_B8C6FE8E` 的 owned 输入，以及独立的 Phase 5.8.3 owned output workspace；输出完成后按 Gate 要求保留，清理责任属于 Phase 5.8.4。
- 详细报告：[`PHASE_05_8_3_GP_ARCPY_REAL_VERIFICATION.md`](phases/PHASE_05_8_3_GP_ARCPY_REAL_VERIFICATION.md)；输出清单：[`PHASE_05_8_3_GP_OUTPUT_MANIFEST.md`](phases/PHASE_05_8_3_GP_OUTPUT_MANIFEST.md)。

## Phase 5.8.3 Formal Acceptance（2026-09-04）

- Independent Gate Keeper：**FORMALLY ACCEPT — PHASE 5.8.3 PASS**。
- 接受依据：四个生产 GP 的 fresh output/Describe/geometry/WKID/count/messages 链、五个 production persistent Python Bridge tool、PID `1028` 复用、owned fixture post-health、安全边界和 lock count=0 均已记录。
- `INTENTIONALLY_RETAINED_OWNED_GP_OUTPUTS` 保留不变；未重跑 Build/Test，仍明确沿用 accepted baseline；known limitations、环境启动事件和 HTTP/harness blocker 保留。
- 下一授权任务：**Phase 5.8.4 — Mutation & Cleanup Verification**，此前状态 **NOT STARTED / READY TO START**；该阶段的 execution result 见下节。

## Phase 5.8.4 Mutation & Cleanup Verification（2026-09-04）

- 当前状态：**PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 5.8.5、Phase 5.9–5.11 和 Phase 5 overall acceptance 未开始。
- 详细报告：[PHASE_05_8_4_MUTATION_CLEANUP_VERIFICATION.md](phases/PHASE_05_8_4_MUTATION_CLEANUP_VERIFICATION.md)。
- Visibility false/true mutation/restore、P583_Buffer exact-path add/remove、controlled APRX discard-without-save、runtime lock release 和 exact disposable-root cleanup 均取得 fresh evidence。
- select_layer 返回 count=0，保持 **PARTIAL / LIMITED IMPLEMENTATION**；clear_selection execution path observed，但 true non-zero → zero = **NOT VERIFIED**。
- Cleanup 只将 <user-home>\AppData\Local\Temp\ArcGISProMCP\Phase5_8_3_GP_P57_B8C6FE8E 移入回收站；post-check root/GDB 不存在，retained P57_B8C6FE8E、MyProject1.aprx 和 shared GDB 未变。
- retained marker/manifest 实际 RunId 均为 P57_B8C6FE8E；fresh production retained counts 为 4/1；Pro/Bridge/6520 无残留。
- 未重新 Build/Test；沿用 Build 0 errors/3 NU1900、Unit 68/68、Integration 20/20；production Bridge lifecycle semantics 未修改。

本轮停止，不进入 Phase 5.8.5 或 Phase 5.9。

## Phase 5.8 Real Verification Preflight（2026-09-03）

- 完成 Phase 5.8 的 scope boundary、30-tool real inventory、历史 real-evidence matrix、fixture ownership、per-run FileGDB、controlled project、mutation/selection/visibility、GP/ArcPy、cleanup、serialization、failure isolation 和 acceptance criteria 预检。
- 当前仓库的正式 roadmap 只有 `5.8 Real Verification` label；独立 scope/acceptance 尚不存在。完整策略报告：[`PHASE_05_8_REAL_VERIFICATION_PREFLIGHT.md`](phases/PHASE_05_8_REAL_VERIFICATION_PREFLIGHT.md)。
- 本轮 runtime 只读 probe：initialize 200、tools/list=30 distinct、python_bridge_ping=pong；project `MyProject1.aprx` 为 user Documents 路径且 `isDirty=true`，不作为 mutation project。
- `TestPolygons` 只读 health：FeatureClass / Polygon / WKID 3857 / featureCount 4 / fields `OBJECTID, Shape, Shape_Length, Shape_Area, Name, Type`；shared GDB TestDate 顶层仍只有该 GDB、115 physical entries、11 observed locks，未写入/清理/删锁。
- 自动化基线：Build 0 errors/3×NU1900；Unit 68/68；Integration 20/20；MCPProtocol 9/9；MCPServer 25/25。HTTP 7 项 Case H 不重开。
- 本轮没有执行 real GP、add/remove/select/visibility/clear mutation、完整 30-tool real business execution、AI/provider 或 Phase 5.9 chain；没有修改 production Source、配置或 deployment。

**READY FOR PHASE 5.8 REAL VERIFICATION IMPLEMENTATION**；推荐顺序为 5.8.1 fixture/project preparation → 5.8.2 native → 5.8.3 GP/ArcPy → 5.8.4 mutation/cleanup → 5.8.5 evidence consolidation。

## Phase 5.8.1 Initial Fixture Attempt（2026-09-03）

- Source health read-only gate PASS：`TestPolygons` exists、FeatureClass、Polygon、WKID 3857、featureCount 4、required six fields；Type observed values `Type1`–`Type4`。
- `TestWorkspace` file artifact cleanup probe PASS；ArcPy GDB fixture probe使用正式 ArcGIS Python executable，但 `import arcpy`/`arcgisscripting` 在当前环境以 `-1073741819` 退出，分类 `BLOCKED_BY_ENVIRONMENT`。没有 GDB、owned source 或 ClipMask 生成。
- 当前 `MyProject1.aprx` path 在用户 Documents 且 `isDirty=true`，严格 read-only；Tests 下没有现成 `.aprx`，因此 project mutation fixture 为 `BLOCKED_BY_FIXTURE`。
- 通过精确停止 Bridge PID 31400 释放后，MCP `python_bridge_ping` 自愈为 pong，new Python child PID 16064；ArcGISPro PID 15740 未停止/重启。
- Build：solution 0 errors/3×NU1900；fixture runner 0 errors；Unit 68/68；Integration 20/20。
- 本轮无 production Source、Bridge action、配置、deployment、shared GDB、用户工程、GP/mutation tool 修改；无 5.8.2/5.8.3/5.8.4/5.8.5/5.9 execution。
- 详细报告：[`PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md`](phases/PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md)。

**PHASE 5.8.1 BLOCKED_BY_ENVIRONMENT**；GP/ArcPy fixture NOT READY；Project mutation fixture NOT READY / BLOCKED_BY_FIXTURE。

## Phase 5.8.1 ArcGIS Python Execution-Context Recovery（2026-09-03）

- Current Bridge PID 16064（parent=ArcGISPro PID 15740）仍可执行真实 ArcPy discovery；runtime `arcpyImportOk=true`，summary/fields/workspace/attributes 均 PASS。
- direct/propy standalone 的 Python core、stdlib、NumPy 1.26.4 PASS；`arcgisscripting`、normal `arcpy`、`ARCPY_NO_IMPORTS=1` 均在 import/native initialization 以 `-1073741819` / `0xC0000005` 退出。
- 未发现目标 shadowing 或简单白名单环境污染；Application/WER 没有对应 Python crash event，faulting module UNKNOWN；license 只读为 Advanced。
- 没有修改用户 dirty project 或 shared TestDate；最终只有一个 production Bridge child、无 fixture probe orphan。用户在外部 native host 执行同一 probe 得到 `ARCPY_OK=true`、ArcGIS Pro 3.5/Build 57366、Advanced、exit code 0；current harness child failure 分类为 `BLOCKED_BY_HARNESS`。
- Build 0 errors/3×NU1900；fixture helper 0 warnings/0 errors；Unit 68/68；Integration 20/20；最终 MCP initialize/tools/list=30/ping/discovery PASS。
- 完整报告：[`PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md`](phases/PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md)。

**PHASE 5.8.1 RECOVERY GATE PASS**；**PHASE 5.8.1 FIXTURE PREPARATION IN PROGRESS / NOT YET ACCEPTED**；继续 5.8.1，停止，不进入 Phase 5.8.2。

## Phase 5.8.1 Native Host Fixture Resume Implementation（2026-09-04）

- test-only runner 的真实模式为 `create-retained`、`cleanup-probe`、`project-probe`；`prepare_arcgis_fixture.py` 固定执行 source health、CreateFileGDB、CopyFeatures、owned preparation Buffer、controlled project copy/reopen 和 manifest payload。
- `Program.cs` 以 `FIXTURE_RESULT_BEGIN`/`FIXTURE_RESULT_END` 包围 JSON；成功 schema 为 `phase-5.8.1-fixture-result-v1`，manifest schema 为 `phase-5.8.1-fixture-manifest-v1`。
- marker 在 ArcPy import/写操作前已存在并记录 RunId/root/creation time；retained GDB 名为 `Phase58_<RunId>.gdb`，owned project 为 root 下的 `Phase58Controlled.aprx`。
- payload 显式记录 source/copy/ClipMask/project health、owned datasource、reopen、selection/visibility baseline、ownership 和 retention；disposable probes 单独验证 cleanup。
- cleanup 在发现残留 `.lock`/`.sr.lock` 时返回 `BLOCKED_BY_RUNTIME_LOCK`，不删除锁文件。
- 本轮没有在当前 Codex harness 执行 ArcPy 创建，也没有修改用户项目或 shared GDB。
- Build/test：fixture helper 0 warnings/0 errors；solution 0 errors/3×NU1900；Unit 68/68；Integration 20/20。
- 外部执行完成前，Phase 5.8.1 仍为 **IN PROGRESS / NOT YET ACCEPTED**。

## Historical Build Baseline
- 解决方案 12 项目，`dotnet build -c Debug`：**0 warnings / 0 errors**。

## Historical Test Baseline（dotnet test）
| 套件 | 结果 |
|------|------|
| UnitTests | 11 / 11 PASS |
| IntegrationTests | **20 / 20 PASS**（含 12 项 Phase 4 工具） |
| ServerTests | 26 / 26 PASS |
| **合计** | **57 / 57 PASS** |

## Phase 4 工具（Fake 集成测试已覆盖；真实 Pro 验证进行中）
- 真实服务：Attribute / Selection / Geoprocessing(+统一 GeoprocessingExecutor) / Layer 扩展 / Project 扩展。
- 新增注册工具：list_maps, get_map_info, get_layer_info, set_layer_visibility, add_layer, remove_layer, list_layouts, list_databases, query_attributes, get_field_info, get_feature_count, clear_selection, select_layer, buffer, clip, intersect, dissolve。
- 状态：已实现 + 已单测/集成测试（Fake）；真实 ArcGIS Pro 验证待补。

## Phase 2 真实 ArcGIS 集成自测（ArcGIS Pro 3.5，Add-in 内 SelfTestRunner）
- ping → "pong"；get_current_map → 真实当前地图；get_layers → 真实图层；
- get_project_info → 真实工程；get_arcgis_version → 3.5.0/57366/net8.0；get_license_info → Advanced。均 success=True。

## Phase 3 真实 HTTP（ArcGIS Pro 3.5 内启动 Server，HTTP 客户端 POST /mcp）
- initialize → 返回 protocolVersion/capabilities/serverInfo ✅
- notifications/initialized → HTTP 202（无响应体）✅
- tools/list → 6 工具（get_arcgis_version, get_current_map, get_layers, get_license_info, get_project_info, ping），JSON Schema 合法 ✅
- tools/call ping → `{"content":[{"type":"text","text":"pong"}],"isError":false}` ✅
- tools/call get_current_map → 真实 ArcGIS 地图（非 Mock）✅
- unknown method → -32601；unknown tool → 工具结果 isError:true；malformed json → -32700；Content-Type 非 json → 415 ✅
- Start → 6520 LISTENING；double Start 幂等；Stop → 6520 停、HTTP connection refused；重新 Start → 6520 恢复；关闭 Pro → 6520 停（无残留）✅

## Phase 3 真实 HTTP（ArcGIS Pro 3.5 内启动 Server，HTTP 客户端 POST /mcp；本次最终验收结果）
- initialize → 返回 protocolVersion=2024-11-05 / capabilities.tools / serverInfo=arcgis-pro-mcp ✅
- notifications/initialized → HTTP 202（无响应体）✅
- tools/list → 6 工具（get_arcgis_version, get_current_map, get_layers, get_license_info, get_project_info, ping），JSON Schema type=object ✅
- tools/call ping → "pong" ✅
- tools/call get_current_map → 真实当前地图（"地图"，非 Mock）✅
- tools/call get_layers → 真实图层（"World Topographic Map"/VectorTileLayer）✅
- tools/call get_project_info → 真实工程（MyProjectA）✅
- tools/call get_arcgis_version → 真实 3.5.0 / 57366 / net8.0 ✅
- tools/call get_license_info → 真实 LicenseLevel=Advanced ✅
- 异常路径：malformed→-32700；invalid request→-32600；unknown method→-32601；invalid params/缺 name→-32602；unknown tool→工具结果 isError:true；empty→-32700；Content-Type 非 json→415；服务器全程不崩溃、之后 ping 正常 ✅
- 生命周期：double Start 幂等；Stop→6520 停、HTTP connection refused；double Stop 无异常；重新 Start→6520 恢复、ping=pong ✅
- 退出清理：关闭 ArcGIS Pro→6520 停止监听、无残留进程/监听 ✅

## Phase 4 真实 ArcGIS Pro 3.5（经 MCP HTTP tools/call）
- tools/list：23 个工具可发现（含 category/executionType 元数据）。
- tools/call 真实成功：get_current_map、get_layers、get_layer_info、set_layer_visibility（修复 FindLayer 回退后 isError=false→true）、get_project_info、list_databases、clear_selection。
- get_feature_count/query_attributes → 真实 LAYER_NOT_FOUND（测试工程无 FeatureLayer；验证类型/校验与不崩溃）。
- list_maps → 经 GetItems+MapFactory 返回空（已知限制）。
- 服务器不崩溃；Phase 3 回归保持。

## 文档（Phase 4）
- TOOL_CATALOG / GEOPROCESSING / TOOL_TESTING 已建立；ARCHITECTURE 待更新。

## Phase 4 完整环境诊断 & 复验（2026-09-01）— 环境 BLOCKED，未执行完整 build/test
- 目的：重跑 restore → build(0/0) → 全部测试 → .esriAddInX → 注册 → Pro 3.5 真实验证 → 临时 FileGDB 要素数据（TestPoints/Polygons/Lines）真实查询/GP/Selection 验证。
- 结论：**无法执行**。根因为 .NET SDK 构建环境故障，非业务代码问题；未伪造任何通过结果。

### 环境诊断（本机，全量核实）
- `dotnet --info` / `--list-sdks`：仅一个 SDK = **8.0.424**（`%LOCALAPPDATA%\Microsoft\dotnet\sdk`）；运行时 8.0.30（AspNetCore/NETCore/WindowsDesktop）；host 8.0.30。
- `DOTNET_ROOT` 未设置；PATH 由脚本前置 `%LOCALAPPDATA%\Microsoft\dotnet`；MSBuild host = SDK 自带 17.11.48。
- **SDK 8.0.424 不完整**：缺失 `Sdks\Microsoft.NET.Sdk\Sdks\Microsoft.NET.SDK.WorkloadAutoImportPropsLocator\Sdk\` 与 `Sdks\Microsoft.NET.Sdk\Sdks\Microsoft.NET.SDK.WorkloadManifestTargetsLocator\Sdk\`（整个 `Sdks\Microsoft.NET.Sdk\Sdks` 目录不存在）；但 `sdk-manifests\8.0.100\` 下装有 android/ios/maui/macos/tvos/aspire 等 **workload manifest**，故 workload 解析器被激活却找不到 locator → 致命 MSB4276。
- **无其他完整 SDK**：`C:\Program Files\dotnet` 无 SDK；`C:\Program Files (x86)\dotnet` 仅运行时 6.0.36（无 SDK）；VS Community 2026 (18.1) MSBuild=18.0.5 无该 locator、无 `MSBuild\Sdks`；VS BuildTools 2026 (18.9) MSBuild=18.9.1 同样无；ArcGIS Pro bin、NuGet 缓存均无。全局无第二个可用 .NET 8 SDK。
- **无网络**：dotnetcli.azureedge.net 与 nuget.org 均不可达（无法下载完整 SDK / msbuild-sdks 包）。
- **写权限（用户级 SDK 修复被拒的原因）**：进程身份 `<machine>\<account>`，但为 **UAC 过滤令牌**（`IsInRole(Administrator)`=False，`BUILTIN\Administrators` "Group used for deny only"，Integrity=Medium）→ 对 `%LOCALAPPDATA%\Microsoft\dotnet` **无法写入**（任何探针写入均 denied），故无法原位补齐 locator 目录，也无法创建官方 `DisableWorkloadResolver.sentinel`。

### 已实证的 SDK 行为
- Leaf（0 引用）/单引用项目可 build（临时工程：net8.0、net8.0-windows app→1 lib 均 0/0 通过）。
- **含 ≥2 ProjectReference 的项目**（Core/Server/Tools/Compatibility 及整 .sln）在 restore 图遍历或 build 跨项目 TargetFramework 发现处，子项目求值报致命 MSB4276 或**静默失败**。
- `WorkloadResolver workaround`（`-p:MSBuildEnableWorkloadResolver=false`）：仅消除 MSB4276，但多引用 build 的 `_GetProjectReferenceTargetFrameworkProperties` 子项目求值仍失败（实测 Core→Configuration，0 MSB4276 仍失败）→ 记录为 **WorkloadResolver workaround insufficient**，不作为修复方案（Rule 8 / 用户禁令）。
- 官方 sentinel 修复不可用（写权限被拒）。

### 未做的事 / 完整性声明
- **未**删除/伪装 ProjectReference、未临时复制 DLL、未改 csproj 隐藏依赖、未删改测试/断言、未用历史 57/57 冒充本次复验、未用旧 DLL/esriAddInX 冒充新构建、未仅凭源码评审宣称真实运行 PASS。
- 曾尝试**克隆 SDK 到工作区并重建缺失 locator**，因无法获取权威内容（无网络/无完整 SDK），手写内容无法验证，**主动放弃**以免引入不可信 SDK 行为（遵守 Rule 3 / 不伪造）。
- 期间清理的 obj/bin 已从 `.runtime\obj-backup-ensure` 完整还原，`project.assets.json`/`project.nuget.cache` 均已恢复；临时工程/日志/SDK 克隆均已清理，工作区干净；真实源码未改动。

### 用户侧可执行的修复（需提权或缺省网络）
1. 以**管理员/提权** PowerShell 在 SDK 中重建两个目录：
   - `%LOCALAPPDATA%\Microsoft\dotnet\sdk\8.0.424\Sdks\Microsoft.NET.Sdk\Sdks\Microsoft.NET.SDK.WorkloadAutoImportPropsLocator\Sdk\`
   - `%LOCALAPPDATA%\Microsoft\dotnet\sdk\8.0.424\Sdks\Microsoft.NET.Sdk\Sdks\Microsoft.NET.SDK.WorkloadManifestTargetsLocator\Sdk\`
   （内容可从任一完整 .NET 8 SDK 复制；或建立网络后 `dotnet` 会自动补齐 / 通过 Vs/BuildTools 安装）。
2. 或**提供网络/完整 .NET 8 SDK**，由我完成原位修复或切换 SDK。
3. 修复后再执行本复验流程。

### 复验状态
- **BLOCKED（环境）**：Phase 4 代码经源码评审确认完整（25 工具/真实服务/注册/QST），但 **build/test/runtime 复验未执行**，**不**升级为已复验 PASS，也**不**进入 Phase 5。

## Phase 4 重新验收（2026-09-01，本 Harness 会话真实结果）
- **Build = PASS**：`dotnet build -m:1 -c Debug`（绕过 SDK 8.0.424 多节点 MSB4276）。`dotnet clean` 后全新重建 12 工程 = **0 警告 / 0 错误 / exit 0**。
- **打包+注册 = PASS**：`package-addin.ps1` → `.esriAddInX`（163469B），`RegisterAddIn.exe /s` = exit 0，安装至 `Documents\ArcGIS\AddIns\ArcGISPro\{BAA5628C-...}\`；包内含 Config.daml+Images+Install\Compatibility.dll(+deps)+全部 7 个 Shared DLL。
- **工具数 = 25（代码级）**：Composition 注册 25；实现 25 Tool 类；元数据/执行类型/11 错误码/三层机制(Native/Geoprocessing/Python)均核实存在。
- **Automated tests = BLOCKED_BY_HARNESS**：`dotnet test` 与 `dotnet test -m:1` 的 vstest testhost 启动即崩（对父进程 `EnableRaisingEvents` → Win32 error 5 Access Denied）。空测试工程同失败；本会话连对自身 EnableRaisingEvents 均被拒 → 会话级进程访问限制，非项目问题。历史 57/57 = 历史基线，非本次。
- **Real ArcGIS Pro 3.5 = BLOCKED_BY_HARNESS**：本会话启动 ArcGISPro.exe 无法完成 GUI 初始化（90s 后仍 73MB WS / 1 CPU 秒 / 无主窗口 / 自测未跑 / 6520 未监听）→ 无法做 MCP Ribbon/6520/project-UI 验证；add-in 未 GUI 加载。故 MCP HTTP/FeatureLayer/Selection/GP 真实端到端**未能在本会话执行**（NOT VERIFIED，保持历史状态）。
- 已清理：临时 Pro 进程、`.runtime\probetest`、`.runtime\pro-launch.pid`；未改源码/环境/注册表/SDK。

## Phase 4 真实 ArcGIS Pro 3.5 运行验证（2026-09-02，连接用户已开 Pro，经 MCP HTTP 127.0.0.1:6520/mcp）
- 环境：Pro 3.5.0/57366，工程 MyProject1；真实 FeatureLayer = TestPolygons；FileGDB = `D:\ArcGIS-Pro-MCP\TestDate\Phase4Test.gdb`。通过 UI Automation 在 Pro 内确认 MCP Tab → Start，再经 HTTP 调用。
- **MCP 基础**：initialize(protocolVersion=2024-11-05, capabilities.tools, serverInfo=arcgis-pro-mcp) → PASS；notifications/initialized → HTTP 202；tools/list 返回 **23 个工具**（**注意：运行实例为旧版 Add-in，缺 get_dataset_info/get_raster_info；当前源码注册 25 个**）。
- **基础工具 6/6 PASS（真实数据）**：ping、get_project_info(MyProject1)、get_arcgis_version(3.5.0/57366)、get_license_info(Advanced)、get_current_map(地图)、get_layers(2×FeatureLayer TestPolygons + Topographic)。
- **Layer PASS**：get_layer_info(TestPolygons2)、set_layer_visibility(调用前后 isVisible true→false→true 且恢复原值)、add_layer(新增图层真实进入 Map.Layers)、remove_layer(真实移除)、list_databases(MyProject1.gdb + Phase4Test.gdb)。
- **list_maps = [ ] / get_map_info=null / list_layouts = [ ]**（均真实空；list_maps 为既有 GetItems→MapFactory 限制，PARTIAL/KNOWN）。
- **Geoprocessing PASS（真实执行）**：buffer/clip/intersect/dissolve 经 GeoprocessingExecutor→ExecuteToolAsync→Pro GP 真实运行（真实 "运行成功" 消息），输出 FeatureClass 真实存在且可 add_layer 到地图。dissolve 传不存在字段 Category 时正确返回 GEOPROCESSING_ERROR(ERROR 000728)，服务器不崩。
- **Error Handling PASS**：unknown tool→TOOL_NOT_FOUND；缺失图层→LAYER_NOT_FOUND；缺失地图→MAP_NOT_FOUND；buffer 缺参→INVALID_ARGUMENT；随后 ping 仍 pong。
- **发现并修复真实 BUG（FeatureLayer 属性/选择）**：运行实例 `get_feature_count/get_field_info/query_attributes/select_layer` 对真实 TestPolygons **误报 LAYER_NOT_FOUND**（`map.FindLayer(name,true)` 返回 null；Attribute/Selection 服务缺 `.Layers` 回退，LayerService 有）。已修复 `AttributeService.cs`、`SelectionService.cs`（加与 LayerService 一致的回退），`dotnet build -m:1 -c Debug` = 0/0，重打包 `.esriAddInX` 163663B + RegisterAddIn exit 0。**新 DLL 需用户重启 Pro 加载后再复验**（Rule：不强行关闭/重启用户 Pro）。

## Phase 4 最终部署状态（2026-09-02，用户重启 Pro 后）
- **工具数根因确认**：运行中 Pro 仍加载**旧 23 工具 Add-in**（`Documents\ArcGIS\AddIns\ArcGISPro\{BAA5628C-...}\ArcGISProMCP.Compatibility.esriAddInX` = 159706B，2026-09-01 06:59 旧版）。我构建的 25 工具+修复 包（163663B）**未部署**：`RegisterAddIn /s` 返回 0 但未替换旧文件，且本 Harness 进程对 Add-ins 目录无写权限（写入被拒）。
- 结论：**25 工具 tools/list、get_dataset_info/get_raster_info、FeatureLayer 属性/选择修复复验需用户手动安装新包并重启 Pro**（安装 `Source\ArcGISProMCP.Compatibility\bin\x64\Debug\net8.0-windows\ArcGISProMCP.Compatibility.esriAddInX`，163663B）。

## Phase 4 最终复验（2026-09-02，用户重启 Pro 已加载 25 工具版，MCP HTTP 127.0.0.1:6520/mcp）
- **tools/list = 25**（含 get_dataset_info / get_raster_info）→ 部署成功（安装在 AddIns 目录的 .esriAddInX = 163663B/163679B 新版）。
- **FeatureLayer（第一处修复已验证）**：get_feature_count(TestPolygons) = **4**；get_field_info = 真实字段(OBJECTID/Shape/Shape_Length/Shape_Area/Name/Type)；query_attributes 带 fieldNames 返回真实 Name(A/B/C)。
- **Selection（第一处修复已验证）**：select_layer(TestPolygons) = {mapName:"地图",layerName:"TestPolygons",count:0}；clear_selection = true。
- **第 2 个真实 BUG 发现并修复**：query_attributes **不带 fieldNames**（读全部字段含 Shape 几何）→ `System.Text.Json` 序列化崩溃（JSON-RPC -32603 Internal error，日志定位到 CallToolAsync 序列化副作用）。已修 `AttributeService.cs`：**跳过 Geometry 字段**；`dotnet build -m:1` = 0/0；最终包 163679B 已生成（含 2 处修复）→ **待用户部署最终包复验**。
- **Regression（25 工具版）PASS**：ping、get_project_info、get_layer_info、get_layers、buffer 真实运行、query_attributes(fieldNames)=真实值。
- **get_dataset_info / get_raster_info**：基础实现（filesystem-only Directory/File.Exists 检查）→ 对 FileGDB FeatureClass 返回 DATASET_NOT_FOUND（FileGDB FC 非 OS 文件）；实现注释声明"此处不伪造" → **real FileGDB 数据集解析 NOT VERIFIED（占位），错误处理(无效路径/缺参)已验证**。

## Phase 4 最终 PASS 复验（2026-09-02，干净重启 Pro + MCP HTTP 6520）
- **tools/list = 25**；**query_attributes 无 fieldNames = PASS**（真实全字段：oid 1-4，OBJECTID/Shape_Length/Shape_Area/Name=A-D/Type=Type1-4，无 Shape 几何，isError=false，不再 Internal error）→ 第 2 修复（跳过 Geometry 字段）生效。
- 回归全 PASS：get_feature_count=4、get_field_info(OBJECTID/Shape/Shape_Length/Shape_Area/Name/Type)、select_layer=成功、clear_selection=true、ping=pong、buffer(GP)=成功。
- 至此 Phase 4 核心 + FeatureLayer 属性 + Selection + GP + 错误处理均真实验证通过；2 处 bug 已修复并部署。
- get_dataset_info/get_raster_info = filesystem-only 占位（FileGDB FC → DATASET_NOT_FOUND，real 解析 NOT VERIFIED）。
- Automated `dotnet test` = BLOCKED_BY_HARNESS（历史 57/57 为历史基线，非本次）。

## Phase 5 Python Bridge — 5.2 Minimal Bridge Prototype（2026-09-02，真实）
- **Python Runtime（真实）**：`C:\Program Files\ArcGIS\Pro\bin\Python\envs\arcgispro-py3\python.exe`=**Python 3.11.11**；`import arcpy`=OK、`GetInstallInfo()`=3.5；`import arcgis`=OK(2.4.1)。
- **Standalone CURRENT 限制（真实）**：独立子进程 `arcpy.mp.ArcGISProject('CURRENT')` → `OSError: CURRENT`（独立 Python ≠ Pro 嵌入上下文）。Bridge 首批只走 arcpy GP/数据集路径。
- **bridge_runner.py（新建，NDJSON）验证 PASS**：ping→"pong"；runtime_info→3.11.11/arcpy3.5/arcgis2.4.1；arcpy_exists(Phase4Test.gdb)=true；arcpy_exists(TestPolygons)=true；describe(TestPolygons)=FeatureClass/Polygon/3857(真实 ArcPy)；未知 action→UNKNOWN_ACTION(ok=false)；raise_test_exception→PYTHON_EXECUTION_ERROR(含 Traceback)；错误后 ping 仍 "pong"（**错误隔离**）；stdout 纯 JSON、stderr 仅诊断。
- **C# 探针（临时）端到端 PASS**：`Process.Start(python)→bridge_runner→stdin NDJSON→stdout 逐行 JSON`；ping/runtime_info/arcpy_exists/describe/未知/malformed 全对、exitCode=0、stdout 纯 JSON。
- **sleep_test（C# 可检测超时思想）**：3s sleep 后返回 "slept"；注意 Python 启动(import arcpy)额外 ~8s → 后续 C# 超时必须区分启动与执行。
- **Phase 4 回归 PASS**：`dotnet build -m:1 -c Debug` exit 0、0 errors（6×NU1900=离线 nuget.org 漏洞数据告警，环境非代码）；6520/MCP up、25 工具；6511 未监听；ArcGIS Pro 未关。
- **Automated `dotnet test` = BLOCKED_BY_HARNESS**（testhost 无法连接，同一 EnableRaisingEvents 限制）。

## Phase 5.3 Python Bridge Process Lifecycle（2026-09-02，真实 C# 探针 + 真实 ArcPy）
- **ProcessManager 实现（Core/PythonBridge）**：`PythonBridgeProcessManager` 持久化 python.exe 子进程；串行单飞；stdin/stdout NDJSON；stderr→ILogger；按 id 关联；退出→Faulted；有限 Restart；Stop/Dispose；无二次系统。
- **真实验证 PASS**：
  - Start → Running（~8463ms 含 ArcPy import）。
  - **Persistent 复用**：3×ping + runtime_info + arcpy_exists + describe 全部同一 python 进程（same-pid=True）。
  - Real ArcPy：arcpy_exists(Phase4Test.gdb)=true、arcpy_exists(TestPolygons)=true、describe(TestPolygons)=FeatureClass/Polygon/3857。
  - 崩溃检测：kill python 子进程 → state=Faulted（日志 "process exited"）；下一请求自动重启恢复（on-demand EnsureStarted）。有限 Restart → 新 pid + ping=pong。
  - Stop（graceful）→ Stopped/pid=-1；Dispose → Disposed、无残留 python 进程。
- Build `-m:1` = 0 errors（6×NU1900=离线 nuget 漏洞源，环境告警）。Phase 4 未受影响（6520/25 工具/Pro 运行/6511 未监听）。
- dotnet test = BLOCKED_BY_HARNESS（testhost 无法连接）。
- 行为注记：SendRequestAsync 内含 EnsureStarted → crash 后请求自动恢复；调用方可先查 State 决定报 PYTHON_BRIDGE_UNAVAILABLE。

## Phase 5.4 PythonBridgeService & MCP Integration（2026-09-02，真实 Service 链探针）
- **F1 修复（真实）**：`_lifecycle` 互斥 + pending drain/fail；**SendRequest+DisposeAsync 并发 → 2155ms 完成、hung=False、无泄漏**；dispose 后残留 python=0。
- **F2 修复（真实）**：Composition 以 RepoRoot 绝对化 PythonExecutable/PythonBridgeScript/PythonWorkingDirectory（不依赖 CWD）。
- **Service 链真实验证（真实 ArcPy/FileGDB）**：PingAsync=pong；RuntimeInfo=3.11.11/arcpy3.5/arcgis2.4.1；ArcpyExists(Phase4Test.gdb)=true；ArcpyExists(TestPolygons)=true；Describe(TestPolygons)=FeatureClass/Polygon/3857；persistent same-pid=True；kill→Faulted→下请求自愈(pong, 新 pid)。
- **Build `-m:1` = 0 errors**（6×NU1900=离线 nuget 环境）。打包 `.esriAddInX`=185110B（**27 工具**）+ RegisterAddIn exit 0。6511 未监听；Phase 4 工具未改；运行中 Pro 仍 25 工具旧版（**真实 MCP E2E 待 Pro 重启后复验**）。
- dotnet test = BLOCKED_BY_HARNESS（testhost 限制）。

## Phase 5.4 真实 MCP E2E（2026-09-02，用户部署 185110B 27 工具版 + 重启 Pro，MCP HTTP 6520）
- tools/list = **27**（python_bridge_ping / python_runtime_info 在列）。
- MCP→python_bridge_ping → **pong**（isError=False）；MCP→python_runtime_info → pythonVersion=3.11.11 / arcpyVersion=3.5 / arcgisVersion=2.4.1。
- 持久化：python 进程恰 1 个；kill 后 MCP python_bridge_ping 自愈 pong、恢复 1 进程（无泄漏）。
- Phase 4 回归 spot（27 工具版下）：ping=pong、get_feature_count=4、buffer 真实 GP 成功 → 无回归。
- Full Phase 4 功能回归：仅 spot-check → NOT VERIFIED（未逐项）。

## Phase 5.5.4 Codex Takeover & Preflight（2026-09-03，部署前历史快照）

### Code / Build
- `Source/Shared/ArcGISProMCP.Tools/PythonDiscoveryTools.cs` 存在并包含 `DatasetSummaryTool`、`ListFieldsTool`、`ListWorkspaceDatasetsTool`；`Composition.BuildRegistry()` 静态注册 **30** 个工具，无重复注册名。
- 三个工具均通过 `ToolExecutionContext.Python` 进入既有 Python Bridge 链路；未发现 HTTP 6511 或 `ArcGISProject("CURRENT")` 旁路。
- 发现并最小修复 `bridge_runner.py` 中 `dataset_summary.fieldsSummary.oidCount` 误填全部字段数的问题；AST 检查 PASS，使用 fake ArcPy 对象的契约检查 PASS（该检查不等于真实 ArcPy PASS）。
- `dotnet build -m:1 -c Debug --no-restore` = **PASS**，0 errors，3×NU1900（无法访问 nuget.org 漏洞数据）。普通 build/test restore 因读取 `<user-home>\AppData\Roaming\NuGet\NuGet.Config` 被拒绝。

### Tests / Runtime
- `dotnet test -m:1 --no-restore --no-build`：Unit **11/11 PASS**，Integration **20/20 PASS**；Server **19/26**，7 项统一因当前 Harness 的 `HttpListenerException: 句柄无效`，标记 **BLOCKED_BY_HARNESS**。历史 57/57 保留为历史基线。
- 真实 ArcGIS Pro 3.5.0/57366 进程与 MCP `127.0.0.1:6520/mcp` 可用；`initialize` PASS。当前 `tools/list=27`，三个 5.5 discovery tool 均不在运行列表。
- 当前运行版真实抽样：`python_bridge_ping`、`python_runtime_info`、`ping` PASS；两次 Bridge 调用复用 Python PID 23984；`get_feature_count(TestPolygons)=4`、真实字段 6 个、`list_databases` 可见 `Phase4Test.gdb`。
- 工作区新包已生成：`Source/ArcGISProMCP.Compatibility/bin/x64/Debug/net8.0-windows/ArcGISProMCP.Compatibility.esriAddInX`，188675B。`RegisterAddIn` 返回 0，但安装目录仍为旧 185110B/27-tool 包；当前 Pro 未强制重启。
- 三个新工具真实 MCP E2E、runtime `tools/list=30`、真实 `dataset_summary/list_fields/list_workspace_datasets` 结果、三种参数错误和新包加载后的完整回归：**NOT VERIFIED**。

### Phase status and final verdict
- Phase 5.5.1 = **PASS**（Design / Preflight Gate）。
- Phase 5.5.2 = **PASS**（历史真实 Level-3 ArcPy/Bridge evidence 保留）。
- Phase 5.5.3 = **PASS**（MCP Tool implementation + Composition registration + static Build）。
- Phase 5.5.4 = **BLOCKED_BY_RUNTIME_DEPLOYMENT**：先在 ArcGIS Pro 完全退出后安装/加载 188675B 新包，再继续真实新工具 E2E；不得进入 Phase 5.6。

## Phase 5.5.4 Final Verification（2026-09-03，进入 Phase 5.6 前的历史验收）

### Memory correction and deployment
- Phase 5.5.1、5.5.2、5.5.3 均保持正式 **PASS**；Level-4 runtime 属于 5.5.4。
- 已先停止旧 ArcGIS Pro 实例，确认 6520/旧 Python child 清理，再部署并重启 Pro。优雅关闭首次未完成，按既有授权仅终止已核实的 ArcGISPro PID；此行为记录为环境/生命周期现象，不判为新代码回归。
- `Config.daml` 版本从 1.0.0 更新为 1.0.1 作为部署元数据变更；工作区 Add-in 为 188678B，已覆盖官方现有安装路径，旧 185110B 包保留为 `.pre-5.5.4-185110.bak` 备份，工作区/安装包 SHA-256 一致。

### MCP initialize and runtime inventory
- ArcGIS Pro 3.5.0 / build 57366 重启后，MCP `initialize` 返回 `protocolVersion=2024-11-05`、`capabilities.tools`、`serverInfo=arcgis-pro-mcp`：**PASS**。
- `tools/list` 返回 **30** 个工具；`dataset_summary`、`list_fields`、`list_workspace_datasets` 全部可发现，schema 为 object，分别要求 string 类型的 `dataset_path`、`dataset_path`、`workspace_path`。

### Real discovery-tool E2E
- `dataset_summary(TestPolygons)`：**PASS**。`exists=true`、`dataType=FeatureClass`、`shapeType=Polygon`、空间参考 WKID 3857、`featureCount=4`、`objectIdField=OBJECTID`、`shapeFieldName=Shape`、六个字段、`fieldsSummary.oidCount=1`、`truncated=false`。
- `list_fields(TestPolygons)`：**PASS**。返回 OBJECTID、Shape、Shape_Length、Shape_Area、Name、Type；OBJECTID 为 OID 且 `isOid=true`，Name 为 String 且 length=255。
- `list_workspace_datasets(Phase4Test.gdb)`：**PASS**。返回 8 个 Polygon feature classes、`totalCount=8`、`truncated=false`；tables/rasterDatasets 等空集合按当前实现契约保留，未解释为“无栅格”。
- 缺失 dataset/workspace：分别按预期返回 NOT_FOUND；三个工具的缺参、空字符串和 number 类型 9/9 均返回 INVALID_ARGUMENT。

### Isolation, persistence, and regression
- 有效 summary → 缺失路径错误 → 有效 summary → `python_bridge_ping` 全部成功；错误未污染后续请求。
- Python child 在序列中保持同一 PID（44320），进程数为 1；workspace discovery 后再次 summary 成功，支持状态恢复/无交叉污染。
- Phase 4 spot regression：`ping`、`get_current_map`、`get_layers`、`get_feature_count(TestPolygons)=4`、真实 `buffer` 及 buffer 输出数据集的 `dataset_summary` 均 PASS。此项是 spot regression，不宣称逐项完整 Phase 4 回归。

### Build and automated tests
- `dotnet build -m:1 -c Debug --no-restore`：**PASS**，0 errors，3×NU1900 网络告警。
- `dotnet test -m:1 --no-restore --no-build`：Unit **11/11 PASS**、Integration **20/20 PASS**、Server **19/26**；7 项因 Harness `HttpListenerException: 句柄无效`，标记 **BLOCKED_BY_HARNESS**。
- 普通 restore/build 读取 `<user-home>\AppData\Roaming\NuGet\NuGet.Config` 被拒绝，标记 **BLOCKED_BY_ENVIRONMENT**。

### Final acceptance
- Phase 5.5.1 = **PASS**。
- Phase 5.5.2 = **PASS**。
- Phase 5.5.3 = **PASS**。
- Phase 5.5.4 = **PASS**。
- Phase 5.5 = **PASS**；不自动进入 Phase 5.6。

## Phase 5.6 Architecture & Preflight（2026-09-03）

### Memory / scope gate
- Phase 5.5 remains **PASS**; current real MCP runtime `tools/list=30`。
- **Phase 5.6 = PREFLIGHT COMPLETE；Implementation = NOT STARTED。**
- Repository does not fully define Phase 5.6 implementation scope。`DECISION-004`（CONFIRMED）only assigns complete request timeout / Kill / restart policy to Phase 5.6；`DECISION-003` remains PROPOSED。`PHASE_06.md` describes later AI Client integration and is not this phase。
- Recommended boundary：Python Bridge reliability / execution guardrails / recovery / bounded protocol handling；preserve one persistent stdin/stdout NDJSON process；no HTTP 6511、second bridge、multi-worker、arbitrary Python、new GIS business tools or Registry/Router rewrite。

### Existing architecture and configuration audit
- Confirmed chain：MCP → `MCPToolRouter` → Python Tool → `IPythonBridgeService` → `PythonBridgeService` → `PythonBridgeProcessManager` → ArcGIS Pro `python.exe` → `bridge_runner.py` → stdin/stdout NDJSON → ArcPy。
- Existing settings：`PythonExecutable`、`PythonBridgeScript`、`PythonWorkingDirectory`、`PythonProcessStartupTimeoutMs`（MCPSettings default 30000ms；Composition 40000ms）、`RequestTimeoutMs=30000`、`MaxConcurrentRequests=8`。`PythonBridgePort=6511` exists as a reserved setting but no listener is started。
- Missing settings：independent Python request timeout、graceful-stop deadline、response/stderr limits、restart/recovery limit、AllowedPaths/AllowedWorkspaces、script/action limit and cancellation policy。`ToolMetadata.TimeoutSeconds` is declared but has no consumer。
- Security model：`SecurityModels` only contains `Allowed/SecurityBlocked`; no validator. Bridge is a controlled subprocess, not a security sandbox。

### Timeout / cancellation / recovery audit
- MCP `RequestTimeoutMs` links a cancellation token around the whole request; the ProcessManager uses the same setting (capped at 120000ms) while awaiting a response. Startup has a separate probe timeout。
- The token reaches Router → Tool → Python Service → ProcessManager write/wait operations, but HTTP transport currently supplies a server-lifetime token rather than a reliable client-disconnect token。
- After a request is sent, timeout/cancellation ends the C# wait and removes its pending TCS; it does not interrupt Python `time.sleep` or synchronous ArcPy and does not restart the process。The single-flight slot then opens while Python may still be busy, so the next Python request can queue/block and late responses can be dropped。
- Production action errors and Python exceptions return structured errors and normally preserve the process。Unexpected exit/EOF marks Faulted and fails pending TCS, but the exception can surface as Router `INTERNAL_ERROR` rather than `PYTHON_BRIDGE_UNAVAILABLE`。
- Stop has no configured graceful deadline when called with `CancellationToken.None`; startup failure cleanup and semaphore dispose/release ordering require implementation tests。
- Factory correlation IDs reuse path `GetHashCode()` values and pending insertion has no duplicate guard; timeout plus late response can misroute a response to a later request。

### Output / path / execution audit
- stdout/stderr are read line-by-line, but neither channel has a byte limit。Malformed/unexpected stdout is logged/dropped; a pending request can then wait for timeout。Direct JSON truncation would break NDJSON, so the recommended policy is bounded read → structured failure → process restart。
- Fixed dispatch actions found in `bridge_runner.py` are `ping`、`runtime_info`、`arcpy_exists`、`describe`、`dataset_summary`、`list_fields`、`list_workspace_datasets` plus test hooks。No `exec`/`eval`/`compile`/`os.system`/`Popen`/`shell=True` user-code surface was found。
- Runtime probe confirmed MCP does not expose `python_execute`、`sleep_test` or `raise_test_exception`；current Python Bridge exposes fixed structured actions only。
- Discovery paths pass directly to ArcPy. There is no allowlist, canonical containment, UNC/reparse/symlink policy; current discovery actions are read-only but can query any path accessible to the process account。
- `list_workspace_datasets` restores `arcpy.env.workspace` and remains top-level only; raster enumeration semantics remain unchanged。
- Fixed action allowlist is preferable to blacklist-based arbitrary Python syntax checks；do not call the current process a sandbox。

### Required design decisions and acceptance
- Recommended new settings (defaults **TBD after runtime measurement**)：`PythonRequestTimeoutMs`、graceful-stop/recovery bound、`PythonMaxResponseBytes`、`PythonMaxStderrBytes`/line limit、bounded recovery count；`PythonAllowedPaths` only after compatibility policy is approved。
- Required new errors if those capabilities are implemented：`PYTHON_TIMEOUT`、`PYTHON_OUTPUT_LIMIT_EXCEEDED`；`PYTHON_PATH_NOT_ALLOWED` only with path guardrails。Restart failure should initially reuse `PYTHON_BRIDGE_UNAVAILABLE`。
- Future probes：normal calls; gated internal sleep timeout/cancellation; exception and crash recovery; malformed/oversized protocol fixtures; startup/restart failure; path matrix; five Python tools plus `ping`/`get_feature_count`/`buffer` regression。This preflight did not invoke the non-MCP test hook。
- Candidate subphases：5.6.1 semantics/correlation; 5.6.2 ProcessManager recovery; 5.6.3 output/path/action guardrails; 5.6.4 real runtime and regression acceptance。
- Phase 5.5 contracts must remain unchanged：`dataset_summary` missing→success + `exists=false`; other missing paths→NOT_FOUND; top-level workspace discovery; `rasterDatasets=[]` semantics。

### Final Gate
**READY FOR PHASE 5.6 IMPLEMENTATION DESIGN**

No Phase 5.6 implementation was started in this preflight。

## Phase 5.6.1 Timeout / Cancellation / Recovery Design Verification（2026-09-03）

### Design status

- **Phase 5.6.1 = DESIGN COMPLETE**。
- **Implementation = NOT STARTED**。
- Phase 5.5 remains **PASS**；the current real ArcGIS Pro 3.5 runtime baseline remains `initialize` PASS and `tools/list=30`。
- Formal decision：[`DECISION-005-python-bridge-timeout-cancellation-recovery.md`](decisions/DECISION-005-python-bridge-timeout-cancellation-recovery.md)。

### Contract verified by source audit/design review

- The request contract has an explicit pre/post-dispatch boundary. A write-started but uncertain operation is treated as possibly dispatched.
- `CANCELLED` is reserved for caller cancellation; a Python execution deadline uses `PYTHON_TIMEOUT`.
- Pre-dispatch timeout/cancellation leaves a healthy process unchanged. Post-dispatch timeout/cancellation quarantines and terminates the child process tree, completes bounded cleanup, and returns without replaying the action; the next independent request starts a fresh child.
- Recovery ownership is Option C: retain the single-flight slot through quarantine, defer fresh startup to the next request, and keep public lifecycle state stable with internal quarantine/generation tracking.
- ProcessManager-owned generation-aware unique IDs replace deterministic payload/hash IDs; duplicate pending insertion fails; late/unknown/old-generation responses are discarded.
- Pending cleanup, startup-failure cleanup, process-exit cleanup, and dispose ordering are explicit requirements. Semaphore disposal must occur only after the final release.

### Current runtime evidence and limits

- A read-only probe confirmed `initialize` protocol `2024-11-05`, `serverInfo=arcgis-pro-mcp`, `tools/list=30`, and successful `python_bridge_ping`, `python_runtime_info`, and `dataset_summary` calls.
- The runtime exposes neither `sleep_test` nor `raise_test_exception` nor `python_execute`; no timeout/cancellation recovery probe was run in this design phase.
- Therefore this record is a design-gate result, not functional proof of post-dispatch kill/recovery. Those tests are required in Phase 5.6.2/5.6.4.

### Phase boundary

Phase 5.6.2 may implement the timeout/cancellation/correlation/recovery contract and its automated fixtures. Response/stderr byte limits, path guardrails, action classification, and final real-runtime acceptance remain outside this design phase. No new Bridge, HTTP 6511 listener, worker pool, arbitrary Python execution, or GIS business tool is approved.

### Final Gate

**READY FOR PHASE 5.6.2 IMPLEMENTATION**。本轮未开始实现。

## Phase 5.6.2 Core Lifecycle Reliability Implementation Verification（2026-09-03）

### Status

- **Phase 5.6.2 = PASS（Core）**。
- **Implementation = COMPLETE**。
- **Phase 5.6.3 = NOT STARTED**。
- Phase 5.5 remains **PASS**；the active Pro runtime baseline remains `tools/list=30`。

### Files and behavior verified

- `MCPSettings` adds `PythonRequestTimeoutMs=18000` and `PythonProcessShutdownTimeoutMs=3000`.
- `ErrorCodes` adds `PYTHON_TIMEOUT`。
- `PythonBridgeRequest` no longer defaults factory IDs to fixed names or path hashes; ProcessManager injects generation-aware `py-{generation}-{sequence}` IDs.
- `PythonBridgeProcessManager` tracks dispatch boundary, rejects duplicate pending IDs, discards unknown/old-generation responses, performs token-independent post-dispatch quarantine, bounds process-tree termination and reader cleanup, clears startup/pending state, and waits for active operations before disposing semaphores.
- `bridge_runner.py` and the production MCP tool surface were not expanded；no HTTP 6511、arbitrary Python、second Bridge or new GIS tool was added。

### Automated verification

- New Core lifecycle fixture tests: **9/9 PASS**。
- Full automated run: Unit **20/20 PASS**、Integration **20/20 PASS**、Server **19/26**；7 Server cases remain **BLOCKED_BY_HARNESS** because of `HttpListenerException: 句柄无效`。
- Covered: unique IDs, same-PID normal exception, pre-dispatch timeout, post-dispatch timeout/cancellation quarantine, fresh-PID recovery, crash, explicit restart/no replay, startup failure cleanup, pending=0 and Dispose during request.

### Runtime verification boundary

- Internal temporary standard-library Python fixture verified the new manager behavior, including old PID termination and fresh PID recovery.
- Active ArcGIS Pro read-only regression verified `initialize`, `tools/list=30`, `python_bridge_ping`, `python_runtime_info`, and `dataset_summary`.
- The modified Add-in binary was not deployed to the active Pro instance in this phase；therefore modified-package ArcGIS Pro timeout/cancellation E2E is **NOT VERIFIED** and remains Phase 5.6.4 scope.

### Build

- `dotnet build -m:1 -c Debug --no-restore`：**PASS**，0 errors，3×NU1900 environment warnings。
- Normal restore remains environment-blocked by NuGet.Config access; this does not change the Core implementation result。

### Final Gate

**PHASE 5.6.2 PASS**；**STOP — WAIT FOR PHASE 5.6.3 INSTRUCTION**。

## ArcGIS Pro 版本
- Pro 3.5 = **本次真实运行 PASS（完整）**。3.3 / 3.4 / 3.6 = NOT TESTED。3.7+ = 不在 3.3–3.6 兼容目标。

## MCP / Python / AI Client
- MCP Server：Phase 3/5.5 runtime 已验证。Python Bridge：已实现并经真实 ArcGIS Pro MCP E2E 验证。
- DeepSeek Harness / Claude / Cursor：NOT CONNECTED（按规则禁用于正式生产连接）。

## Phase 5.6.3 Output / Protocol / Action / Access Guardrails Verification（2026-09-03）

### Status

- **Phase 5.6.3 = PASS**。
- **Implementation = COMPLETE**。
- **Phase 5.6.4 = NOT STARTED**。
- Path Guardrail = **DESIGN COMPLETE / DEFERRED**。

### Implemented behavior

- `MCPSettings` adds `PythonMaxResponseBytes=1048576`、`PythonMaxStderrBytes=65536` and `PythonAllowTestActions=false`。
- ProcessManager checks each complete stdout line after `ReadLine` using UTF-8 byte count. Oversized responses fail with `PYTHON_OUTPUT_LIMIT_EXCEEDED` without JSON truncation and use the 5.6.2 unsafe-process cleanup path.
- Non-empty malformed JSON, incomplete response schema and invalid correlation ID fail with `PYTHON_PROTOCOL_ERROR` instead of waiting for `PYTHON_TIMEOUT`; whitespace lines remain recoverable noise. Valid unknown/late/old-generation responses remain log + discard.
- stderr uses a per-process cumulative logger budget and continues draining after the budget is exhausted. It does not stop the pipe reader.
- `bridge_runner.py` has explicit `PRODUCTION_ACTIONS` and `TEST_ACTIONS`; the manager passes `ARCGIS_PRO_MCP_ALLOW_TEST_ACTIONS`, defaulting to disabled. Disabled test actions return existing `UNKNOWN_ACTION`.
- No `PythonAllowedPaths` was added. Path compatibility is documented as deferred because FileGDB child paths, UNC/mapped/SDE workspaces, `..`, case and reparse points cannot be safely covered by a naive containment rule.

### Response measurement

| Call | Business text payload bytes |
|---|---:|
| `python_bridge_ping` | 4 |
| `python_runtime_info` | 146 |
| `dataset_summary` | 536 |
| `list_fields` | 1,060 |
| `list_workspace_datasets` | 900 |

最大 MCP 封装响应为 2,048 bytes；因此 1 MiB transport line limit 对当前 Phase 5.5 workload 提供了明显余量。

### Tests

- Guardrail/lifecycle class: **16/16 PASS**。
- Full Unit: **27/27 PASS**。
- Integration: **20/20 PASS**。
- Server: **19/26 PASS**；7 项因 `HttpListenerException: 句柄无效`，标记 **BLOCKED_BY_HARNESS**。
- Build: **0 errors**，3×NU1900 network warnings。

### Runtime boundary

当前 ArcGIS Pro 旧 package 的只读 MCP regression 仍为 initialize、`tools/list=30`、Python ping/runtime/summary PASS。修改后的 Add-in 未部署，modified-package response/protocol/action runtime acceptance 为 **NOT VERIFIED**，留待 Phase 5.6.4。

### Final Gate

**PHASE 5.6.3 PASS**；**STOP — WAIT FOR PHASE 5.6.4 INSTRUCTION**。

## Phase 5.6.4 Real Runtime / ArcPy / MCP E2E Final Acceptance（2026-09-03）

### Pre-acceptance final status（historical snapshot）

- **Phase 5.6.4 = PASS**。
- **Phase 5.6 = PASS**。
- Detailed report：[`PHASE_05_6_4_FINAL_REPORT.md`](phases/PHASE_05_6_4_FINAL_REPORT.md)。

### Package and runtime identity

- `scripts/package-addin.ps1 -Configuration Debug` generated and registered the package. Workspace and installed package both measured 202613 bytes, last write `2026-09-03T18:17:20.5079733+08:00`, SHA-256 `62FBE89CAE9F03077C797A146662AFC81ED29E59539327D85FBC190A179B8D65`.
- Embedded Compatibility/Configuration/Core assembly hashes matched current build outputs. Old Pro PID 38884 and old Bridge PID 44320 were gone before launch; new Pro PID 15740 loaded the package. Because the headless session had no usable window handle, the minimal environment-gated verification auto-start branch was used; normal production start remains the Start button.

### Real MCP and ArcPy evidence

- MCP `initialize` passed at `127.0.0.1:6520/mcp` with protocol `2024-11-05`, `serverInfo=arcgis-pro-mcp`, and tools capability. `tools/list=30`; no `sleep_test`, `raise_test_exception`, or `python_execute`.
- New runtime persistent Python PID 31400 passed ping/runtime/discovery calls. `TestPolygons` returned FeatureClass/Polygon/WKID 3857/featureCount 4/6 fields/oidCount 1. Current workspace discovery returned 9 feature classes, `totalCount=9`, `truncated=false`, after the additional Phase 5.6.4 buffer output.
- Missing dataset/workspace and missing/empty/wrong-type arguments retained Phase 5.5 `exists=false`, `NOT_FOUND`, and `INVALID_ARGUMENT` contracts. `ping`, `get_current_map`, `get_layers`, `get_feature_count=4`, real `buffer`, and output `dataset_summary` passed.

### Real production-runner lifecycle evidence

- Timeout: PID 31092 → `PYTHON_TIMEOUT` after 19555 ms → PID 1320 on the next independent ping; no probe child remained.
- Cancellation: PID 25112 → `CANCELLED` after 3050 ms → PID 31872 on the next independent ping.
- Structured error: PID 6032 returned `PYTHON_EXECUTION_ERROR`; same PID ping passed.
- Crash: verified probe-owned PID 8460 → `PYTHON_BRIDGE_UNAVAILABLE`/stdout EOF → fresh PID 17380.
- Dispose: request returned `PYTHON_BRIDGE_UNAVAILABLE`, state `Disposed`, manager PID `-1`, and no probe-owned child. Final inspection showed only intended Pro PID 31400 Python child.

### Automated and boundary evidence

- Guardrail/lifecycle **16/16**, Unit **27/27**, Integration **20/20** passed. Server **19/26** passed; 7 `HttpListenerException: 句柄无效` cases remain `BLOCKED_BY_HARNESS`.
- Oversized response, malformed/invalid-schema protocol, late response, and stderr drain/budget paths remain internal fixture verified; production MCP exposure is not applicable. `PythonAllowedPaths` remains `DESIGN COMPLETE / DEFERRED`; HTTP client-disconnect cancellation remains `NOT VERIFIED / future scope`.

### Final Gate

**PHASE 5.6.4 PASS**；**PHASE 5.6 PASS**；**STOP — WAIT FOR NEXT PHASE INSTRUCTION**。

## Phase 5.7 Test Architecture & Coverage Preflight（2026-09-03）

### Scope and inventory

- PHASE_05.md 第 20 行仍是唯一 repository-defined Phase 5.7 signal：5.7 Tests；没有独立 Phase 5.7 scope/acceptance 文件。
- 从测试源码盘点到 73 个 xUnit Fact：Unit 27、Integration 20、Server 26；无 Theory/Trait/Collection。
- 30 个 tool class、30 个 Composition registrations、runtime tools/list=30；静态与 runtime 名称集合无差异。
- 当前 Fake/Host integration 实际调用了 19/21 Native tool、1/4 GP tool；5/5 Python tool facade、3 个 GP tool 的独立 call、两个 data/raster placeholder tool 尚无自动化 call。

### Current execution evidence

- dotnet build -m:1 -c Debug --no-restore：PASS，0 errors，3×NU1900 环境告警。
- Unit project：27/27 PASS。
- Integration project：20/20 PASS。
- Server project：19/26 PASS；7 项在 HttpMcpTransport.StartAsync 的 HttpListener.Start() 抛出 HttpListenerException: 句柄无效，归类 BLOCKED_BY_HARNESS。
- 真实 MCP 受限只读 baseline：initialize HTTP 200；tools/list HTTP 200/count 30；python_bridge_ping HTTP 200/pong。

### Coverage conclusion and boundary

- P0 gaps：30-tool/schema/registry snapshot、Python facade、clip/intersect/dissolve 独立 call、完整 error matrix、HTTP harness 可复验性。
- P1 gaps：完整 Phase 4 regression、GP output/data isolation、native mutation restore、protocol edge cases、dynamic test resource policy。
- 本轮未新增测试，未执行 test action、其他业务 tool、完整 Phase 4/5.6 suite、昂贵 Pro E2E、AI/provider 或 Phase 5.8/Phase 6。
- 详细审计报告：PHASE_05_7_TEST_ARCHITECTURE_COVERAGE_PREFLIGHT.md。

### Final Gate

**READY FOR PHASE 5.7 TEST IMPLEMENTATION**。此处不是 Phase 5.7 acceptance，也不表示 Phase 5 整体完成。

## Post-Phase-5.6 Transition Gate / Next-Phase Preflight（2026-09-03）

### Phase decision

- `PHASE_05.md:20` 是全仓唯一匹配 `5.7`/`Phase 5.7` 的 roadmap 定义：`5.7 Tests → 5.8 Real Verification → 5.9 MCP→…→ArcPy → 5.10 Regression → 5.11 Acceptance`。
- 未发现 `PHASE_05_7*` 独立文件，也未发现另一个冲突的 Phase 5.7/Phase 6 顺序定义。Phase 5.7 正式存在但尚未独立完成；Phase 5 整体不标记 COMPLETE。
- `PHASE_06.md` 状态为 NOT STARTED，精确范围是 DeepSeek Harness / Claude Desktop / Cursor 等 MCP 客户端配置与真实连接验证（initialize / tools/list / tools/call / GIS 真实执行）。

### Transition build/runtime baseline

- 本轮 `dotnet build -m:1 -c Debug --no-restore`：**PASS**，0 errors，3×NU1900（NuGet 漏洞数据源不可访问）环境告警。
- 本轮只读 MCP：`initialize` HTTP 200 / protocol `2024-11-05` / server `arcgis-pro-mcp`；`tools/list` HTTP 200 / **30**；`python_bridge_ping` HTTP 200 / `pong`。
- 当前 ArcGIS Pro：3.5.0 build 57366，PID 15740；endpoint `127.0.0.1:6520/mcp`；Python child 31400；6511 未监听。
- 本轮未重跑完整 Phase 5.6 suite，未执行任何 AI/provider/client 调用。

### Phase 6 readiness audit

- PASS：当前 MCP server、30-tool static/runtime inventory、Pro runtime、Python Bridge 和 server-side initialize/tools/list/tools/call 基础均已有真实证据。
- MISSING：AI client、MCP client、model/provider integration、credential mechanism、conversation/tool-selection loop；AI→MCP→ArcGIS 的客户端闭环为 NOT VERIFIED。
- NOT DEFINED：Phase6-specific provider abstraction、entry criteria、expected deliverables、verification-level matrix 和 subphase numbering；这些不能从标题推断。

### Final Gate

**READY FOR PHASE 5.7**；本条只记录 transition decision，不宣称 Phase 5.7 或 Phase 6 已实现/验收。

## Phase 5.7.1 Production Tool Contract Snapshot & Test Foundation（2026-09-03）

### Implementation

- 新增 Tests/UnitTests/ProductionToolContractSnapshot.cs：集中保存当前 30-tool Name/Category/ExecutionType/RequiresArcGIS contract。
- 新增 Tests/UnitTests/ProductionToolContractTests.cs：通过反射加载真实 Compatibility 构建输出并调用 Composition.BuildRegistry()；没有手工注册 30 个工具。
- 新增 11 个 Fact，覆盖 registry count/unique/name exact set、class↔registration、metadata/category/execution/RequiresArcGIS、lower snake_case、schema structural/required/serialization、5 Python、4 GP 和 forbidden tool regression。
- 没有修改 Source、production API、Registry/Router/Bridge、配置、package 或 AI/provider。

### Automated verification

- Build：dotnet build -m:1 -c Debug --no-restore，0 errors，3×NU1900 环境告警。
- Unit：基线 27；新增 11；完整结果 **38/38 PASS**，0 failed，0 skipped。
- Integration：**20/20 PASS**。
- Server：**19/26 PASS**；7 个 HTTP transport 测试在 HttpListener.Start() 报“句柄无效”，保持 BLOCKED_BY_HARNESS。

### Runtime verification

- 当前 MCP endpoint 的只读 initialize：HTTP 200，protocol 2024-11-05，server arcgis-pro-mcp。
- tools/list：HTTP 200，count 30；runtime name set 与 production snapshot 完全一致。
- 本轮没有调用 business tool、Python action 或 test action，没有重跑完整 Phase 4/5.6 E2E。

### Boundary

Python facade 行为、GP 独立参数/执行、data/raster placeholder call、完整 error matrix、HTTP harness workaround、Phase 5.7.2 和 Phase 5.8 均不在本阶段。

### Final Gate

**PHASE 5.7.1 PASS**；**STOP — WAIT FOR PHASE 5.7.2 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS；Phase 5 overall NOT COMPLETE。

## Phase 5.7.2 Tool Behavior Tests（2026-09-03）

### Scope and implementation

- 新增 Unit behavior tests：Python 5 facade、GP buffer/clip/intersect/dissolve、两个 filesystem-only placeholder、错误/取消/空值/错误类型契约。
- 新增无 HTTP Server boundary test，使用生产 `BufferTool` 和注入 GP recorder 验证 failure → Router → `McpServer.HandleRequestAsync` → MCP `isError` text。
- 新增 test-only `FakePythonBridgeService` 与 `RecordingGeoprocessingService`；不启动 Python，不执行 ArcGIS Pro GP，不复制 production implementation。
- 生产 Composition/30-tool contract snapshot 保持原测试路径，未修改 registration、Registry、Router 或 Bridge。

### Automated results

| 套件 | 结果 |
|------|------|
| UnitTests | **63 / 63 PASS** |
| IntegrationTests | **20 / 20 PASS** |
| ServerTests | **20 / 27 PASS**；7 项 `HttpListenerException: 句柄无效`，`BLOCKED_BY_HARNESS` |

- Build：`dotnet build ArcGIS-Pro-MCP.sln -m:1 -c Debug --no-restore`，**0 errors / 3×NU1900**。
- Unit 全量包含 5.7.1 的 38 个 contract tests；30-tool class/registration/runtime snapshot regression 保持 PASS。

### Runtime read-only baseline

- `initialize`：HTTP 200，protocol `2024-11-05`，server `arcgis-pro-mcp`。
- `tools/list`：HTTP 200，30 个 distinct tools，名称集合与 production snapshot 一致。
- `python_bridge_ping`：HTTP 200，`isError=false`，text `pong`。
- 未执行 GP、placeholder、Python discovery business action、test action、AI/provider 或 Phase 5.8/6。

### Final Gate

**PHASE 5.7.2 PASS**；**STOP — WAIT FOR PHASE 5.7.3 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS；Phase 5 overall NOT COMPLETE。

## Phase 5.7.3 Server Protocol & HTTP Harness Resolution（2026-09-03）

### Current result

- Phase 5.7.3 = **PASS**；Phase 5.7 = **IN PROGRESS**；Phase 5.7.4 尚未开始；Phase 5 overall = **NOT COMPLETE**。
- ServerTests 共 **41** 项：**34 PASS**；7 个 HTTP transport case 在 `HttpMcpTransport.StartAsync` 的 `HttpListener.Start()` 失败，异常为 `HttpListenerException (6): 句柄无效`，分类 **BLOCKED_BY_HARNESS / Case H**。
- 独立 raw BCL probe 在 fixed `16521` 与释放后的 dynamic `54450` 都从 `System.Net.HttpListener.SetupV2Config()` 失败；独立 child process 也复现。真实 Pro 6520 listener 仍可用。

### HTTP-independent protocol coverage

- `MCPProtocolTests`：**9/9 PASS**。
- `MCPServerTests`：**25/25 PASS**。
- 新增 string/null/missing request ID、string ID response preservation、initialize/tools/call params、missing/non-string name、non-object arguments、batch invalid request、server timeout cancellation 和 caller cancellation 覆盖。
- HTTP transport 的 method/path/content-type/lifecycle tests 仍保留为阻断事实，未 skip、未伪造 PASS、未修改 production transport。

### Regression and runtime

- Unit：**63/63 PASS**；Integration：**20/20 PASS**。
- Build：**0 errors，3×NU1900** 环境告警。
- 真实 Pro 3.5.0 build 57366 只读 MCP：initialize HTTP 200、protocol `2024-11-05`、tools/list **30 distinct**、python_bridge_ping `pong`。
- 未执行 GP/placeholder/Python discovery business action、test action、AI/provider 或 Phase 5.8/6。

### Boundary

没有修改 `HttpMcpTransport`、Registry/Router/Bridge、配置、package 或 OS URLACL/firewall/权限。HTTP client-disconnect cancellation、large request policy、完整 Phase 4 regression 和 Phase 5.7.4 尚未完成。

详细报告：[`PHASE_05_7_3_SERVER_PROTOCOL_HARNESS.md`](phases/PHASE_05_7_3_SERVER_PROTOCOL_HARNESS.md)。

### Final Gate

**PHASE 5.7.3 PASS**；**STOP — WAIT FOR PHASE 5.7.4 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS；Phase 5 overall NOT COMPLETE。

## Phase 5.7.4 Test Data Ownership & Mutation Safety（2026-09-03）

### Current result

- Phase 5.7.4 = **PASS**；Phase 5.7 = **IN PROGRESS**；Phase 5.7.5 是下一子阶段；Phase 5.8 = **NOT STARTED**。
- `Tests/TestSupport/TestWorkspace` 已提供 marker-based ownership、run-scoped names、separator-aware containment、child-process artifact registration、reparse refusal 和 non-throwing cleanup reporting。
- Placeholder 和 Python Bridge temporary fixtures 已迁移到统一 helper；Unit **68/68 PASS**，并验证没有 Phase 5.7.4 temp root 残留。Integration **20/20 PASS**。
- `MCPProtocolTests` **9/9 PASS**；`MCPServerTests` **25/25 PASS**。既有完整 Server 结果 **34/41** 与 7 个 `BLOCKED_BY_HARNESS / Case H` 保持不变，本轮没有重新诊断 HttpListener。

### Data and runtime evidence

- `D:\ArcGIS-Pro-MCP\TestDate` top-level 仍只有 `Phase4Test.gdb`；只读 inventory 为 115 个物理文件（104 个普通文件、11 个 `.sr.lock`）。共享 GDB 未写入、未清理、未删除。
- Real Pro 3.5.0 build 57366 read-only baseline：`initialize` HTTP 200、`tools/list` HTTP 200、30 distinct tools；没有执行 business tool。
- Solution build：**0 errors / 3×NU1900**。完整报告：[`PHASE_05_7_4_TEST_DATA_MUTATION_FIXTURES.md`](phases/PHASE_05_7_4_TEST_DATA_MUTATION_FIXTURES.md)。

### Boundary

本轮未执行完整 Real GP、map/layer/selection mutation、HTTP workaround、Phase 5.8/5.10 或生产代码修改。

### Final Gate

**PHASE 5.7.4 PASS**；**STOP — WAIT FOR PHASE 5.7.5 INSTRUCTION**。Phase 5.7 仍 IN PROGRESS；Phase 5 overall NOT COMPLETE。

## Phase 5.7.5 Test Consolidation & Acceptance（2026-09-03）

### Final automated result

- Build：dotnet build ArcGIS-Pro-MCP.sln -m:1 -c Debug --no-restore，0 errors，3×NU1900 环境告警。
- Unit：68/68 PASS；Integration：20/20 PASS；MCPProtocol：9/9 PASS；MCPServer：25/25 PASS。
- MCPTransport：7/7 在 HttpListener.Start() 处为 HttpListenerException (6): 句柄无效，正式分类 BLOCKED_BY_HARNESS / Case H。
- Coverage collection：Unit、Integration、Server non-HTTP 均成功生成诊断 Cobertura；无阈值 gate。

### Final runtime entry

- ArcGIS Pro 3.5.0 build 57366 / http://127.0.0.1:6520/mcp：initialize HTTP 200，protocol 2024-11-05，server arcgis-pro-mcp。
- tools/list HTTP 200，30 distinct names；tools/call python_bridge_ping HTTP 200，isError=false，text=pong。
- 只读入口复核完成；未执行完整 real GP、mutation、test action、AI/provider 或 Phase 5.8。

### Acceptance

- 30-tool contract、Python facade、GP routing、placeholder、error/protocol rendering、ownership/mutation policy 和 P0 review 已关闭。
- Phase 5.7.5 = PASS；Phase 5.7 = PASS；Phase 5 overall = NOT COMPLETE；Phase 5.8 = NOT STARTED。
- 完整报告：[`PHASE_05_7_5_TEST_ACCEPTANCE.md`](phases/PHASE_05_7_5_TEST_ACCEPTANCE.md)。

### Final Gate

**PHASE 5.7.5 PASS**；**PHASE 5.7 PASS**；**READY FOR PHASE 5.8 PREFLIGHT**；**STOP — WAIT FOR PHASE 5.8 INSTRUCTION**。

## Phase 5.8.1 Unit Regression / Process-Lock Recovery（2026-09-04）

- 修复前完整 Unit 的唯一失败为 `PostDispatchCancellationKillsOldProcessAndNextRequestStartsFreshProcess`：`PythonBridgeLifecycleTests.cs:526` 读取 test-owned `requests.ndjson` 时与 fake Python writer 的短暂 exclusive handle 冲突。
- 隔离目标测试 10/10、生命周期类 16/16；完整 Unit 修复前间歇性 67/68。根因确认是 `TEST DEFECT`，不是 ArcGIS runtime、production ProcessManager 或未知外部进程。
- 最小测试 helper 修复后：目标测试 10/10、完整 Unit 连续 5/5 为 68/68、Integration 20/20、solution Build 0 errors/3×NU1900。
- 运行期 PID 观察：test-owned Python child `23576 → 3384`，旧/新不同；取消后 old PID 不可用，测试结束后无 Python child。既有 no-replay/cancel/quarantine/generation assertions 保持不变。
- retained fixture `P57_B8C6FE8E` marker/manifest/owned GDB/owned source/ClipMask/controlled project health PASS；两个 disposable probes cleanup PASS、residual=0；shared GDB 和 `MyProject1.aprx` untouched。
- 详细报告：[`PHASE_05_8_1_UNIT_REGRESSION_RECOVERY.md`](phases/PHASE_05_8_1_UNIT_REGRESSION_RECOVERY.md)。

### Final Gate

**PHASE 5.8.1 FINAL GATE = PASS CANDIDATE；等待独立 Gate Keeper review**。Phase 5.8.1 仍 IN PROGRESS / NOT YET ACCEPTED；Phase 5.8.2 NOT STARTED。

## Phase 5.8.1 Formal Acceptance — Repository State Reconciliation（2026-09-04）

Independent Gate Keeper formally accepted the previous `PASS CANDIDATE`:

```text
FORMALLY ACCEPT — PHASE 5.8.1 PASS
READY FOR PHASE 5.8.2
```

### Final accepted basis

- Source health PASS；external native ArcPy PASS。
- Owned FileGDB、`P58_TestPolygons`、`P58_ClipMask`、controlled APRX、explicit project reopen、owned datasource、selection baseline、visibility baseline、cleanup probe、project cleanup probe、manifest、no orphan process：均 PASS。
- Build PASS（0 errors，3 `NU1900`）；Unit **68/68 PASS**；Integration **20/20 PASS**。
- Fixture Preparation = PASS；Regression Gate = PASS；previous Final Gate = PASS CANDIDATE；Final Phase 5.8.1 status = PASS。
- Unit regression root cause = **TEST DEFECT**；production Python Bridge lifecycle semantics 未修改。

### Retained identity and safety

只读读取 marker 与 manifest 后确认：

```text
RunId = P57_B8C6FE8E
Root = <user-home>\AppData\Local\Temp\ArcGISProMCP\Phase5_7_4_P57_B8C6FE8E
Manifest = phase-5.8.1-fixture-manifest-v1
```

Fixture health unchanged；没有重建、重命名、移动或修改 retained fixture。`MyProject1.aprx`、shared `TestDate\Phase4Test.gdb`、历史 outputs 和未知 locks 均未触碰。

### Carried-forward limitations

- `list_maps` = PARTIAL；`get_map_info` = PARTIAL。
- `get_dataset_info` / `get_raster_info` = LIMITED IMPLEMENTATION。
- `ArcGISProject.isDirty` on Pro 3.5 = unavailable / non-blocking。
- HTTP client-disconnect cancellation = NOT VERIFIED；7 HTTP transport tests = BLOCKED_BY_HARNESS。
- `select_layer` 只接受 `mapName`、`layerName`，没有 OID 参数；此事实留给 Phase 5.8.2 规划，不构成当前 selection mutation PASS。

### Current status

```text
Phase 5.8 = IN PROGRESS
Phase 5.8.1 = PASS
Phase 5.8.2 = NOT STARTED / READY TO START
Phase 5.8.3–5.8.5 = NOT STARTED
Phase 5.9–5.11 = NOT STARTED
Phase 5 overall = NOT COMPLETE
```

本次只完成状态 reconciliation；Phase 5.8.2 Native mutation 尚未开始。

## Phase 5.8.4 Post-cleanup Retained Semantic Health Recovery Gate（2026-09-04）

Independent Gate Keeper interim PARTIAL 后执行的最小恢复验证已完成。该验证没有恢复回收站项目、没有重新创建 current-phase output，也没有调用 map/layer/visibility/selection/GP mutation。

- disposable Phase 5.8.3 root、GDB 及四个 P583 output path 仍不存在。
- fresh production MCP dataset_summary：P58_TestPolygons = FeatureClass/Polygon/WKID 3857/count 4；P58_ClipMask = FeatureClass/Polygon/WKID 3857/count 1。
- fresh list_workspace_datasets：恰好两个 FeatureClasses（P58_TestPolygons、P58_ClipMask），totalCount=2，truncated=false。
- retained RunId 仍为 P57_B8C6FE8E；controlled APRX pre/post SHA256 均为 0F514C540553118C443A91C188D8D69E14EEC4EB7C33897D34A82290639AF2F3；marker/manifest hash 均不变；retained GDB 65 files/0 locks。
- 显式关闭后无 ArcGISPro.exe、Bridge 或 6520 listener；MyProject1.aprx 和 shared Phase4Test.gdb 保持不变。

Recovery Gate = **PASS**（retained semantic health and integrity）。Phase 5.8.4 总状态仍 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**，不是正式 acceptance；Phase 5.8.5、Phase 5.9–5.11 未开始，Phase 5 overall = **NOT COMPLETE**。

## Phase 5.8.4 Independent Gate Keeper Formal Acceptance（2026-09-04）

- Independent Gate Keeper：**FORMALLY ACCEPT — PHASE 5.8.4 PASS**。
- 接受依据：visibility mutation→observe→restore、P583_Buffer exact-path add/remove/恢复、controlled APRX discard-without-save、精确 disposable-root cleanup、自然锁释放、受保护基线完整性及 post-cleanup retained semantic health。
- Fresh production MCP：P58_TestPolygons = FeatureClass/Polygon/WKID 3857/count 4；P58_ClipMask = FeatureClass/Polygon/WKID 3857/count 1；retained workspace 恰好两个 FeatureClasses；disposable root 仍 absent；无 Pro、Bridge 或 6520 listener。
- select_layer 保持 PARTIAL / LIMITED IMPLEMENTATION；true non-zero → zero clear_selection 保持 NOT VERIFIED。历史 candidate、67/68、TEST DEFECT、Recovery Gate 和 known limitations 均保留，production Bridge lifecycle semantics 未修改。

Phase 5.8.4 = **PASS**；Phase 5.8.5 = **NOT STARTED / READY TO START**；Phase 5.9–5.11 = **NOT STARTED**；Phase 5 overall = **NOT COMPLETE**。本轮仅完成文档同步。

## Phase 5.8.5 Evidence Consolidation（2026-09-04）

- 执行状态：**IN PROGRESS**；当前结果：**PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**。详细报告：[PHASE_05_8_5_EVIDENCE_CONSOLIDATION.md](phases/PHASE_05_8_5_EVIDENCE_CONSOLIDATION.md)。
- 30-tool matrix 分类：PASS 24、PARTIAL 3、LIMITED IMPLEMENTATION 2、NOT VERIFIED 1；tool-level BLOCKED_BY_HARNESS=0、OUT-OF-SCOPE=0。
- 5.8.1–5.8.4 的 fresh、carried-forward accepted、historical evidence 已分离；四个 GP、五个 production Python Bridge、Native/map/layer/data/project、基础/protocol 工具均有明确归属。
- 完整 MCP → Router → Native/GP/Python → ArcGIS/ArcPy business chain 保持 Phase 5.9 boundary；不把本轮汇总写成 Phase 5.9 acceptance。
- 保护资产、RunId、path/hash/count/WKID/PID/cleanup/lock 交叉核验完成。保留 shared GDB 历史 115/11 与后续 104/0 的非阻断 provenance gap，不猜测原因。

Phase 5.8.5 = **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**；Phase 5.9–5.11 = **NOT STARTED**；Phase 5 overall = **NOT COMPLETE**。本轮没有启动 runtime、mutation 或下一阶段。

## Phase 5.8.5 / Phase 5.8 Independent Gate Keeper Formal Acceptance（2026-09-04）

- Independent Gate Keeper：**FORMALLY ACCEPT — PHASE 5.8.5 PASS**；**FORMALLY ACCEPT — PHASE 5.8 PASS**。
- 接受依据：30-tool matrix 总数 30，分类为 PASS 24、PARTIAL 3、LIMITED IMPLEMENTATION 2、NOT VERIFIED 1；7 个 HTTP transport tests 继续为 BLOCKED_BY_HARNESS。
- fresh、carried-forward accepted、historical evidence 已分离；四个 GP、五个 production persistent Python Bridge、Native/map/layer/data/project/system evidence 均按既有报告闭环。
- retained marker/manifest 实际 RunId 为 P57_B8C6FE8E，fixture health unchanged；controlled APRX、shared GDB、retained GDB、disposable root、locks 和 no-orphan safety evidence 保持已接受结果。
- select_layer、clear_selection、list_maps/get_map_info、dataset/raster placeholders、ArcGISProject.isDirty、HTTP client-disconnect cancellation 及 GAP-01 provenance gap 均原样保留，不猜测、不修复、不升格。
- Source/Tests 未变更；accepted baseline 为 Build 0 errors/3 NU1900、Unit 68/68、Integration 20/20；production Python Bridge lifecycle semantics 未修改。

```
Phase 5.8.5 = FORMALLY ACCEPTED / PASS
Phase 5.8 = FORMALLY ACCEPTED / PASS
Phase 5.9 = NOT STARTED / READY TO START
Phase 5.10 = NOT STARTED
Phase 5.11 = NOT STARTED
Phase 5 overall = NOT COMPLETE
```

本次仅完成 repository documentation sync；未启动 Phase 5.9、5.10、5.11 或 Phase 6。

## Phase 5.9 Full MCP E2E Verification（2026-09-04）

- fresh production endpoint：http://127.0.0.1:6520/mcp；initialize HTTP 200、notifications/initialized HTTP 202、tools/list HTTP 200；server=arcgis-pro-mcp 0.1.0、protocol=2024-11-05、tools/list=30 unique。
- 30-tool direct matrix：PASS 24、PARTIAL 3、LIMITED IMPLEMENTATION 2、NOT VERIFIED 1；额外一次 set_layer_visibility restore call 使调用数为 31，但 unique tool 仍为 30。没有 tool-level BLOCKED_BY_HARNESS 或 OUT-OF-SCOPE。
- RunId=P59_A3FBDFB2；production ArcGIS Pro PID=8388；persistent Bridge PID=26700；Bridge executable 为 arcgispro-py3/python.exe，bridge_runner.py command line 在全部 Python observations 中一致。
- Native fresh chain：Phase58_TestMap current map、三层 baseline、P58_TestPolygons count=4/6 fields、attributes OID 1–4、visibility false→observe→true、P59 owned output add→identity/source/count=4→remove→baseline restore。select_layer 只接受 mapName/layerName，clear_selection 的真实 non-zero→zero 未验证。
- GP fresh chain：buffer/clip/intersect/dissolve 均 HTTP 200/isError=false，OperationResult 分别为 Buffer_analysis、Clip_analysis、Intersect_analysis、Dissolve_management；P59 outputs 均为 Polygon/WKID 3857，counts=4/4/4/1，field counts=8/8/14/4。production dataset_summary/list_fields 与 Pro 关闭后的 direct ArcPy Describe 一致。
- Python fresh chain：python_bridge_ping、python_runtime_info、dataset_summary、list_fields、list_workspace_datasets 均成功；runtime 为 Python 3.11.11、ArcPy 3.5、arcgis 2.4.1；PID 26700 被复用。
- cleanup：未保存 controlled APRX；精确 P59 root 在 ownership/canonical/reparse/containment/lock/no-orphan gate 通过后发送到回收站；post-check root/GDB/outputs absent。controlled APRX、MyProject1.aprx、retained P57 fixture 和 shared GDB 未变，无 Pro/Bridge/6520 orphan。
- 详细报告：[PHASE_05_9_FULL_MCP_E2E_VERIFICATION.md](phases/PHASE_05_9_FULL_MCP_E2E_VERIFICATION.md)。本轮没有重新 Build/Test；沿用 accepted baseline Build 0 errors/3 NU1900、Unit 68/68、Integration 20/20。

### Final status

~~~text
Phase 5.9 = PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER
Phase 5.10 = NOT STARTED
Phase 5.11 = NOT STARTED
Phase 5 overall = NOT COMPLETE
~~~

本记录不构成 Phase 5.9 formal acceptance；已知 limitations、GAP-01、7 个 HTTP transport BLOCKED_BY_HARNESS 和历史证据边界继续保留。

## Phase 5.9 Independent Gate Keeper Formal Acceptance（2026-09-05）

- Independent Gate Keeper：**FORMALLY ACCEPT — PHASE 5.9 PASS**。
- 接受依据：initialize=200、initialized=202、tools/list=30 unique；30 个 unique tools 均真实调用（visibility 两次，共 31 calls）；分类 PASS 24、PARTIAL 3、LIMITED IMPLEMENTATION 2、NOT VERIFIED 1。
- Native active-map read/mutation/restore、四个 GP 的 MCP→OperationResult/messages→ArcPy/FileGDB Describe（Polygon/WKID 3857/count 4/4/4/1）、五个 Python tools 及 Bridge PID 26700 复用均获接受。
- usage-limit interruption/stale PID/旧 PID 420 graceful close/PID 8388 recovery 的证据边界正确；discard-without-save、自然锁释放、exact-root cleanup、protected-path post-check 均获接受。
- Source/Tests、production code/config、Word、controlled APRX、MyProject1.aprx、retained fixture、shared GDB 未修改；历史 candidate、limitations、GAP-01 和 7 个 HTTP harness blocker 保留。

~~~text
Phase 5.9 = FORMALLY ACCEPTED / PASS
Phase 5.10 Regression = NOT STARTED / READY TO START
Phase 5.11 = NOT STARTED
Phase 5 overall = NOT COMPLETE
~~~

本次只完成 formal acceptance 的文档同步，未启动 Phase 5.10。

## Phase 5.10 Regression Verification（2026-09-05）

- 详细报告：[`PHASE_05_10_REGRESSION_VERIFICATION.md`](phases/PHASE_05_10_REGRESSION_VERIFICATION.md)。
- RunId=`P510_BDE10BCD`；test-owned root 通过 marker-before-artifacts、canonical/containment/reparse/lock/no-orphan gates 后移入 Windows Recycle Bin，active temp tree 中 absent 且可恢复。
- Build 0 errors / 3 NU1900；Unit 68/68、Integration 20/20、MCPProtocol 9/9、MCPServer 25/25、MCPTransport 7/7、Server full 41/41。去重后 129 Facts。
- Bridge lifecycle 16/16 连续三次；所有 TRX failed/skipped/error 均为 0；无 test bridge、testhost、Pro、production Bridge 或 6520/6511 listener 遗留。
- 受保护 APRX、MyProject1.aprx、retained P57 fixture、shared GDB 与 Source/Tests 均未改变。retained marker/manifest 实际 RunId 为 `P57_B8C6FE8E`，fixture semantic health unchanged。
- 当前 transport fresh 7/7 PASS；历史 Case H 记录继续保留，不修改 URLACL/firewall/OS；HTTP client-disconnect cancellation 仍 NOT VERIFIED。

### Final status

~~~text
Phase 5.10 = PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER
Phase 5.11 = NOT STARTED
Phase 5 overall = NOT COMPLETE
~~~

本轮不构成 formal acceptance；等待 Independent Gate Keeper，停止于 Phase 5.10。

## Phase 5.10 Independent Gate Keeper Formal Acceptance（2026-09-05）

- Independent Gate Keeper 正式裁定：**FORMALLY ACCEPT — PHASE 5.10 PASS**。
- Independent re-run：Unit 68/68、Integration 20/20、Server full 41/41；fresh Protocol 9/9、MCPServer core 25/25、MCPTransport 7/7；所有 failed/skipped/error 均为 0。
- 30 unique production tools、forbidden test action isolation、Bridge lifecycle 16/16 连续 3 轮、exact-root cleanup、protected-path integrity 和 known limitations 均获接受。
- 历史 Case H / `BLOCKED_BY_HARNESS` 记录继续保留；当前 host fresh transport 7/7 PASS；HTTP client-disconnect cancellation 仍 NOT VERIFIED。
- 本次仅同步 formal acceptance 文档状态，未运行测试或 runtime，未修改 code/config/Word，不执行 Phase 5.11 或 Phase 6。

### Historical authorized next task snapshot

~~~text
Phase 5.11 — Acceptance
NOT STARTED / READY TO START
~~~

~~~text
Phase 5.10 = FORMALLY ACCEPTED / PASS
Phase 5.11 = FORMALLY ACCEPTED / PASS
Phase 5 overall = FORMALLY ACCEPTED / PASS / COMPLETE
Phase 6 = NOT STARTED
~~~

## Phase 5.11 Final Acceptance Audit（2026-09-05）

- 本轮按 Independent Gate Keeper 授权执行 repository lineage、production architecture/scope、accepted evidence、safety/ownership 和 documentation consistency 的只读审计。
- Independent Gate Keeper 已正式裁定：**FORMALLY ACCEPT — PHASE 5.11 PASS**；**FORMALLY ACCEPT — PHASE 5 OVERALL PASS**。Phase 5 overall 已 COMPLETE；Phase 6 未开始。
- Phase 5.10 fresh baseline 保持：Build 0 errors / 3 NU1900；Unit 68/68；Integration 20/20；MCPProtocol 9/9；MCPServer core 25/25；MCPTransport 7/7；Server full 41/41；lifecycle 16/16 连续三轮；deduplicated Facts 129。
- Phase 5.9 fresh accepted chain 保持：initialize 200、initialized 202、tools/list 30 unique；矩阵 PASS 24 / PARTIAL 3 / LIMITED IMPLEMENTATION 2 / NOT VERIFIED 1；Bridge PID 26700 reuse；GP counts buffer/clip/intersect/dissolve = 4/4/4/1。
- 实际 retained RunId = P57_B8C6FE8E；marker/manifest 与 source/owned/clip-mask/controlled-project health 均 PASS；retained GDB 65/0 locks；shared GDB 104/0 locks；protected hashes、P59/P510 absence 和 no-process/listener checks 与接受记录一致。
- Phase 5.1 缺少 standalone formal-PASS 记录，DECISION-003 仍为 PROPOSED；此项归类为 NON-BLOCKING HISTORICAL ACCEPTANCE-RECORD / PROVENANCE GAP。list_maps/get_map_info、dataset/raster、select_layer、clear_selection、isDirty、HTTP disconnect、path guardrail、GAP-01 等限制继续原样保留。
- 本轮仅同步 formal acceptance，未运行 Build/Test/ArcGIS Pro/Bridge，未修改代码、配置、Word、fixture 或受保护数据。完整审计及正式裁定见 [PHASE_05_11_FINAL_ACCEPTANCE.md](phases/PHASE_05_11_FINAL_ACCEPTANCE.md)。

## Phase 5.11 and Phase 5 Overall Independent Gate Keeper Formal Acceptance（2026-09-05）

- 正式裁定：**FORMALLY ACCEPT — PHASE 5.11 PASS**；**FORMALLY ACCEPT — PHASE 5 OVERALL PASS**。
- 正式接受依据：Phase 5.2–5.10 implementation/verification chain closed；Phase 5.8、5.9、5.10 已 formal accepted；Phase 5.11 lineage、architecture、scope、fresh/carry-forward/historical evidence、safety/ownership 和 consistency audit 通过。
- Phase 5.1 仍为 architecture/preflight complete、无 standalone independent formal-PASS record；DECISION-003 仍 PROPOSED；该历史 gap 为 NON-BLOCKING，不伪造 5.1 PASS。python_execute/arbitrary Python 继续禁止，PythonAllowedPaths 继续 DESIGN COMPLETE / DEFERRED。
- Production scope remains single persistent NDJSON Bridge、ProcessManager lifecycle/correlation/generation/timeout/cancellation/recovery/limits/action isolation、5 production Python tools、30 unique tools（Native 21 + GP 4 + Python 5）。
- Acceptance basis preserves source/native ArcPy/owned FileGDB/P58 fixtures/controlled project/reopen/datasource/selection/visibility/cleanup/manifest/no-orphan PASS；Build 0 errors/3 NU1900；Unit 68/68；Integration 20/20；Protocol 9/9；Server core 25/25；Transport 7/7；Server full 41/41；Lifecycle 16/16 × 3。
- Retained RunId = P57_B8C6FE8E；retained GDB 65/0 locks；shared GDB 104/0 locks；protected hashes and P59/P510/process/listener safety remain unchanged. All PARTIAL/LIMITED/NOT VERIFIED/deferred/BLOCKED_BY_HARNESS/GAP-01/P57 naming debt/TEST DEFECT history remains explicit.

~~~text
Phase 5.11 = FORMALLY ACCEPTED / PASS
Phase 5 overall = FORMALLY ACCEPTED / PASS / COMPLETE
Phase 6 Entry Preflight = FORMALLY ACCEPTED / PASS
Phase 6 = IN PROGRESS / READY FOR CURSOR MCP CONNECTION VERIFICATION
Phase 6.1 Cursor Availability = FORMALLY ACCEPTED / PASS
Phase 6.2 Cursor MCP Configuration File = FORMALLY ACCEPTED / PASS
Phase 6.3 Cursor Client Load / Configuration Recognition = FORMALLY ACCEPTED / PASS
First client = Cursor
Cursor MCP connection/initialize/tools-list = NOT VERIFIED / READY TO START
Phase 7 = NOT STARTED
~~~

## Phase 6 Entry Preflight（2026-09-05，安装前历史快照）

- Repository-only entry preflight completed; no ArcGIS Pro/Bridge/Server, client, tool call, Build/Test, login, credential access, model/network call, code/config/Word/fixture or protected-data change was performed.
- Primary verdict: **BLOCKED_BY_EXTERNAL_CLIENT**. Claude Desktop、Cursor、DeepSeek Harness 的命令、常见安装路径、配置路径和运行进程均未发现；6511/6520 无监听属于 runtime 未启动状态，不是本轮运行失败。
- Secondary verdict: **BLOCKED_BY_UNDEFINED_ACCEPTANCE**。Phase 6 尚未指定首个客户端，也未定义客户端配置、provider/login 和 client-facing acceptance 的精确门槛；不标记 **BLOCKED_BY_CREDENTIAL**。
- Server-side accepted evidence remains carried evidence only;现有 HTTP/xUnit harness 不替代真实客户端证据。详细报告：[PHASE_06_ENTRY_PREFLIGHT.md](phases/PHASE_06_ENTRY_PREFLIGHT.md)。
- 下一步：用户提供/安装并指定首个外部 MCP Client，随后由 Gate Keeper 单独授权配置步骤；Phase 7 保持 NOT STARTED。

## Phase 6 Entry Preflight Formal Acceptance and Cursor First-Client Gate（2026-09-05，安装前决策记录）

- Independent Gate Keeper：**FORMALLY ACCEPT — PHASE 6 ENTRY PREFLIGHT PASS**。
- First client target = **Cursor**；minimum gate is client-to-server MCP, not provider-mediated natural-language agent behavior。
- Required future fresh evidence: Cursor version/build and configuration surface；existing `http://127.0.0.1:6520/mcp` Streamable HTTP configuration without secrets；Cursor initialize/session；exact `tools/list=30`；one authorized safe read-only `tools/call`。Real GIS mutation requires separate authorization and an owned/controlled target。
- Earlier `BLOCKED_BY_UNDEFINED_ACCEPTANCE` is preserved as historical pre-decision evidence and is **RESOLVED FOR THE FIRST-CLIENT GATE**. Passing Cursor alone does not close Phase 6 overall; named-client breadth remains open。
- Historical pre-availability/configuration boundary: Phase 6 Entry Preflight = **FORMALLY ACCEPTED / PASS**；Phase 6 = **IN PROGRESS / READY FOR CURSOR MCP CONFIGURATION**；Cursor configuration = **NOT STARTED / READY TO START**；Phase 7 = **NOT STARTED**。

## Phase 6.1 Cursor Availability（2026-09-05）

- Authorized Winget installation completed successfully: package `Anysphere.Cursor`, version `3.19.7`, source `winget`。
- Fresh evidence: executable `<user-home>\AppData\Local\Programs\Cursor\Cursor.exe` exists；File/ProductVersion=`3.19.7`；uninstall registry metadata matches；SHA-256=`9FE9867E4D697774D21A05E1D2216A79C02A24AF1DA64072C57C3A8AD1CF8BEE`。
- Winget/Cursor/installer process count is 0；6511/6520 listener count is 0；`cursor` command is absent from the current shell PATH；MCP config presence checks were missing and no contents were read。
- No Cursor launch, login, credential access, MCP configuration, server connection, tool call, Build/Test, ArcGIS Pro/Bridge/Server start, or source/test/config/Word/fixture/protected-asset change occurred.
- Independent Gate Keeper result: **FORMALLY ACCEPT — PHASE 6.2 CURSOR MCP CONFIGURATION FILE PASS**。Current: **Cursor MCP configuration file = FORMALLY ACCEPTED / PASS**；**Cursor client load/configuration recognition = FORMALLY ACCEPTED / PASS**；**Cursor MCP connection/initialize/tools-list = NOT VERIFIED / READY TO START**；Phase 6 = **IN PROGRESS / READY FOR CURSOR MCP CONNECTION VERIFICATION**；Phase 7 = **NOT STARTED**。Detailed report：[PHASE_06_3_CURSOR_CLIENT_LOAD.md](phases/PHASE_06_3_CURSOR_CLIENT_LOAD.md)。

## Phase 6.3 Cursor Client Load / Configuration Recognition（2026-09-05）

- 按授权可见启动 Cursor，工作区 `D:\ArcGIS-Pro-MCP` 加载成功，主窗口标题为 `ArcGIS-Pro-MCP - Cursor`；Cursor 保持打开，未执行 UI 点击或键盘输入。
- Fresh non-secret observation：主窗口 PID=`12148`，Cursor 进程均来自 `<user-home>\AppData\Local\Programs\Cursor\Cursor.exe`，File/ProductVersion=`3.19.7`；ArcGISPro/dotnet=0；6511/6520 无监听。
- 初始仅扫描三个 `anysphere.cursor-mcp` 文件时未出现 `arcgis-pro-mcp`、`127.0.0.1:6520/mcp`、`mcpServers` 或 connection-term；该子集后来确认不完整。对精确当前会话文件的补充扫描观察到：`cursor-sentry-events.log` 第 2–5 行含 server name/endpoint，且第 4 行含 `mcpServers`；`Mcp FileSystem Writer.log` 第 5–7 行含 server name 与 recognition/registration 语义；`workbench.mcp.oauth.log` 第 1、4 行含 server name，第 2–3 行含 error/offline 语义。仅输出脱敏语义分类，不输出原始日志或凭据。
- MCP Server 保持停止；未执行 initialize/tools/list/tools/call。结论为 **Phase 6.3 = FORMALLY ACCEPTED / PASS**；配置识别与 endpoint reference/attempt 已观察到，但 **Cursor MCP connection/initialize/tools-list = NOT VERIFIED / READY TO START**；Phase 6 = **IN PROGRESS / READY FOR CURSOR MCP CONNECTION VERIFICATION**；Phase 7 = **NOT STARTED**。
- 未读取设置、聊天、账户、环境值或凭据；未启动 ArcGIS Pro/Bridge，未运行 Build/Test，未修改 Source/Tests/Word/fixture/受保护资产。详细报告：[PHASE_06_3_CURSOR_CLIENT_LOAD.md](phases/PHASE_06_3_CURSOR_CLIENT_LOAD.md)。

## Phase 6.4 Cursor MCP Connection / Initialize / Tools List（2026-09-05）

- Fresh preflight retained the accepted config hash `C5CF47ED4759FDBF391838C112971BAB5C6A3F6BD135F846675AD708C5AD135B`，retained RunId `P57_B8C6FE8E`，controlled APRX hash `0F514C540553118C443A91C188D8D69E14EEC4EB7C33897D34A82290639AF2F3`，and protected `MyProject1.aprx` hash `ECF25A8791219435CE9AC44F7A50B28468074ACDF96C9F55455106FB376BCD14`。
- The retained controlled APRX was launched only with the established process-scoped verification auto-start environment; ArcGIS Pro PID `24636` opened `Phase58Controlled`，6520 listener appeared with owning PID `4`，and 6511 remained unbound. The first launch without that process-scoped variable was gracefully closed and is recorded as a procedure correction。
- Cursor current-session logs showed server-name/endpoint/`mcpServers` evidence and initialize-related semantic activity, but did not expose a verifiable successful client connection/session, initialize result, `tools/list`, or exact `tools/list=30` result. No `tools/call` was executed.
- Result: **Phase 6.4 = BLOCKED_BY_CLIENT_LOG_VISIBILITY**；**Cursor MCP connection/initialize/tools-list = NOT VERIFIED**。This is not a server/runtime failure; the controlled runtime reached 6520. Detailed report：[PHASE_06_4_CURSOR_MCP_CONNECTION.md](phases/PHASE_06_4_CURSOR_MCP_CONNECTION.md)。
- Controlled Pro exited gracefully. Post-check: ArcGIS Pro/dotnet/listeners 0；shared GDB 104/0 locks；retained GDB 65/0 locks；config, APRX, protected APRX, marker, and manifest hashes unchanged。Phase 7 remains NOT STARTED。

### Phase 6.4 Gate Keeper workspace correction and Pro-first recovery（2026-09-05）

- Gate Keeper confirmed session `20260905T143455` was not `empty-window`: structured evidence showed a non-empty workspaceId, `workspacePaths=d:\ArcGIS-Pro-MCP`, `projectServers=1`, and `project-0-ArcGIS-Pro-MCP-arcgis-pro-mcp`.
- Final Pro-first session `20260905T144521` started controlled Pro PID `24088` first and reached 6520 while 6511 stayed unbound; Cursor PID `26032` then launched with `--new-window D:\ArcGIS-Pro-MCP`.
- The fresh Cursor evidence still did not expose verifiable connection success, successful initialize/session, `tools/list`, or exact `tools/list=30`; no `tools/call` was executed. Result remains **BLOCKED_BY_CLIENT_LOG_VISIBILITY**；both applications exited gracefully and post-checks remained clean. Earlier **BLOCKED_BY_CLIENT_PROCESS** is preserved as historical launcher-diagnostic evidence。

### Phase 6.4 minimal workspace-launch diagnostic（2026-09-05）

- Gate Keeper authorized local launcher diagnostics only. `Cursor.exe --help` launched Cursor instead of presenting help and was gracefully closed. An explicit `--new-window` retry used `D:\ArcGIS-Pro-MCP` but the observed argument contained surrounding spaces; the window remained `Cursor Agents` and workspace loading was not proven.
- Owned Cursor main PID `29016` did not exit within the bounded 30-second `CloseMainWindow()` wait. No force termination was used. Current classification: **BLOCKED_BY_CLIENT_PROCESS**；no runtime or `tools/call` was started in this final retry.
- Final post-check: Cursor/ArcGIS Pro/dotnet/listeners 0；config, controlled/protected APRX, marker/manifest, and GDB inventory/locks unchanged. Earlier **BLOCKED_BY_CLIENT_LOG_VISIBILITY** remains historical evidence。

### Phase 6.4 controlled recovery attempt（2026-09-05）

- Gate Keeper authorized a same-gate recovery: Cursor main PID `12148` exited gracefully; controlled APRX was started first with process-scoped verification auto-start and reached 6520 (6511 remained unbound); Cursor was then relaunched visibly with `D:\ArcGIS-Pro-MCP` as PID `6136`.
- Fresh Cursor session `20260905T142036` did not expose project server identity, endpoint, verifiable connection/session success, successful initialize, `tools/list`, or exact `tools/list=30`; no `tools/call` was executed.
- Recovery result remains **BLOCKED_BY_CLIENT_LOG_VISIBILITY**；both owned applications exited gracefully. Final post-check: Cursor/ArcGIS Pro/dotnet/listeners 0；protected hashes and GDB inventory/locks unchanged。

## Phase 6 Codex P0 Project Configuration Recognition（2026-09-05）

- 客户端规划已迁移为 Codex=P0 首选必验、Cursor=P1 必验、DeepSeek Harness=P1 必验、Claude Desktop=P2 可选。旧 Cursor 6.1–6.3 PASS 和 6.4 `BLOCKED_BY_CLIENT_LOG_VISIBILITY` 保留；本记录不把 Phase 6 宣布为 PASS。
- 项目级 `D:\ArcGIS-Pro-MCP\.codex\config.toml` 已创建，严格为：

```toml
[mcp_servers.arcgis-pro-mcp]
url = "http://127.0.0.1:6520/mcp"
```

- Fresh CLI evidence：`codex-cli 0.153.0`；`codex mcp list` 明确列出 `arcgis-pro-mcp`、正确 URL、`enabled`、`Auth Unknown`。Codex project configuration recognition = **PASS**。
- Config metadata：63 bytes；SHA-256=`2E4A919F011A0D8F5F175B7F85C0E5236C0923D67BDC632EF331221E17AD55E2`。
- Connection/initialize/tools-list/tools-call 均仍 **NOT VERIFIED**。本 Gate 未启动 ArcGIS Pro、Cursor、dotnet 或 MCP Server；6520 当前无监听，不能把配置识别误写为 Runtime PASS。

## Phase 6 Codex P0 MCP Connection / Initialize / Exact Tools List / Safe Read-only Call（2026-09-05）

- Fresh Codex CLI client：`codex-cli 0.153.0`；`codex exec --json --ephemeral --approve-for-me -C D:\ArcGIS-Pro-MCP -`；cwd=`D:\ArcGIS-Pro-MCP`；项目配置 SHA-256=`2E4A919F011A0D8F5F175B7F85C0E5236C0923D67BDC632EF331221E17AD55E2`。
- Pro-first runtime：retained `Phase58Controlled.aprx`，ArcGIS Pro PID=`4064`，6520 listener=`1`，6511=`0`；调用结束后优雅退出，最终 ArcGISPro/dotnet/6511/6520=`0`。
- Codex 客户端新会话报告 `arcgis-pro-mcp` connection=`SUCCEEDED`、initialize/equivalent session=`SUCCEEDED`、`tools/list=30 unique tools`。完整工具名称集合见专门报告。
- 唯一安全只读调用：`mcp__arcgis_pro_mcp__ping`，arguments `{}`；MCP result：`{"content":[{"type":"text","text":"pong"}],"isError":false}`。未调用 mutation tool。
- Raw `initialize` envelope 未单独暴露；严格结论为 **PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER**，不是 Formal PASS。报告：[PHASE_06_CODEX_P0_MCP_CONNECTION_GATE.md](phases/PHASE_06_CODEX_P0_MCP_CONNECTION_GATE.md)。
- Protected post-check：`MyProject1.aprx`、retained marker/manifest hashes unchanged；retained GDB=`65/0 locks`；shared `Phase4Test.gdb`=`104/0 locks`。

## Codex P0 MCP Connection Gate Formal Acceptance（2026-09-05）

- Independent Gate Keeper：**FORMALLY ACCEPT — Codex P0 MCP Connection / Initialize-equivalent Session / Exact Tools List / Safe Read-only Call Gate = PASS**。
- Accepted fresh evidence：Codex CLI `0.153.0` new session；Pro-first retained controlled APRX；connection=`SUCCEEDED`；initialize-equivalent session=`SUCCEEDED`；exact `tools/list=30` and complete unique name set；same session `ping({})` → `pong`、`isError=false`。
- Raw `initialize` envelope remains not separately exposed, recorded as a non-blocking limitation. No mutation or other tools/call is implied.
- Next authorized gate：**Cursor P1 MCP Connection / Initialize / Exact 30 Tools / Safe Read-only Call Recovery**；Phase 6 remains IN PROGRESS and Phase 7 remains NOT STARTED。

## Cursor P1 MCP Connection Gate（2026-09-05）

- Fresh Cursor `3.19.13` session launched with `D:\ArcGIS-Pro-MCP`; workspace and one project server were recognized. `.cursor/mcp.json` hash remained `C5CF47ED4759FDBF391838C112971BAB5C6A3F6BD135F846675AD708C5AD135B`.
- Pro-first runtime reached 6520 before Cursor launch; 6511 stayed unbound. After bounded graceful cleanup, ArcGISPro/Cursor/dotnet and listeners 6511/6520 were all `0`.
- Cursor client logs recorded target `arcgis-pro-mcp` as `disconnected`; no successful connection, initialize/session, `tools/list`, exact 30-tool set, or `tools/call` evidence was exposed. No Cursor MCP/agent CLI or export interface was found for a non-UI substitute.
- Classification: **BLOCKED_BY_USER_UI**；Cursor connection/initialize/tools-list/tools-call = **NOT VERIFIED**。No credentials, UI input, mutation, or protected-data change occurred.
- Dedicated report：[PHASE_06_CURSOR_P1_MCP_CONNECTION_GATE.md](phases/PHASE_06_CURSOR_P1_MCP_CONNECTION_GATE.md)。
- Protected post-check unchanged：`MyProject1.aprx`、shared `Phase4Test.gdb`、retained controlled APRX/fixture、历史输出和 locks 均未修改或删除。
