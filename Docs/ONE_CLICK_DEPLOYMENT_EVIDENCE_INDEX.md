# One-click Deployment Share Package — Evidence Index

> **历史时点声明（D-129 README 链接陈旧面补声明批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **r5 candidate 代际（文内无 ISO 日期；时点由正文「Deployment version」行取 `one-click-1.0.2-r5` 与「r5 is the current separately named remediation candidate」两处表述推定）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。
> ★ 本头部点名的当时点位（由本批扫描件机械实测，非人工摘录；现值见上列两件）：`-r5`・`361FC84F`・`3924F9B3`

This index points to evidence for the separate one-click deployment candidate and its limited Independent Gate Keeper decision. It is not a replacement for the historical Phase 7 acceptance record and does not constitute real-installation, real-client, clean-machine, or public-release acceptance.

## Candidate identity

| Evidence | Value |
|---|---|
| Candidate status | `INDEPENDENT GATE PASSED / AUTHORIZED FOR USER REAL-MACHINE TRIAL` |
| Deployment version | `one-click-1.0.2-r5` |
| ZIP | `Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip` |
| ZIP size | `331679` bytes |
| ZIP SHA-256 | `3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652` |
| External hash file | `Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip.sha256` |
| Embedded bundle manifest | `bundle-manifest.json` inside the ZIP; `23` physical files, with `22` manifest-listed files |
| Accepted payload | `269548` bytes; SHA-256 `361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A` |
| Previous r4 candidate | Preserved unchanged: `330602` bytes; SHA-256 `AE9C5EF2421D4E73F8BA62AEA5FBFEF2B4EFF0DA7D2EF24B01EED93D3103A134` |
| Previous r3 candidate | Preserved unchanged: `329568` bytes; SHA-256 `7EEC03C0E0DAD1F3363147FE614B72C0CB41DB3A80262CC76453F4AAC8B1E3EA` |
| Rejected r1 | Preserved unchanged: `316207` bytes; SHA-256 `3303E02CFAC50EB02B74F4C0B4BDA77C3BE2788607920EE1402B43B19102D7B1` |

## Evidence map

## Rejected r1 and r2/r3/r4/r5 revision record

- Independent Gate Keeper rejected r1: repeated GUI starts left status at `执行中`, the target display was empty, and PowerShell emitted worker lifecycle and null-array errors. The full r1 candidate and its hash remain unchanged; this rejection is historical evidence, not a current PASS.
- The original r2 review hash was `CC1CC37C8B00918313203E45656183A9FE6656D0E9D1FC664D096349D45AFD53`; the same-name r2 file was later overwritten by a different `329024`-byte candidate with hash `33AC18A10E2DF4EA7D0D0BE646C777BC2EB535D8C552F1EA846B75956936DABF`. Those r2 evidence roots remain distinct and are not relabeled as r3.
- r3 remediation remains preserved and source-visible in `scripts/one-click-setup.ps1`: typed .NET child-process bridge, unified state, main-thread polling, safe child-result parsing, explicit UTF-8 stdout/stderr capture and decode, captured GUI target scope, complete InstallRoot + Add-in GUID display, sanitized GUI smoke lifecycle log, comma-delimited multi-client rejection, and recovery-index registration before state persistence.
- r4 adds and verifies the real cancel callback predicate, child-completion hard-interruption recovery, pending recovery-index restart blocking, index+ledger rollback, fresh transaction allocation, and recovery-index write-failure with a distinct old-latest entry. Its candidate ZIP and evidence remain preserved at `Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r4-Windows-x64.zip` and `.runtime/one-click-deployment-r4/run-20260910-013551-7399ba24eb5740598656e5640761f465/`.
- r5 is the current separately named remediation candidate. It adds UTF-8 BOMs for Windows PowerShell 5.1 package/test entry scripts, pure .NET SHA-256 fallback in the package client configurator/test harness, and ProcessStartInfo-based synchronous child exit capture. The test harness additionally uses an explicit UTF-8 encoded bootstrap, explicit parent stdout/stderr UTF-8 decode, dot-source scope preservation, a top-level Unicode path assertion, and failure-time owned-temp artifact preservation. Independent Gate Keeper decision: r5 implementation and local isolated automation `PASS`, authorized for user real-machine trial only; real installation, real AI-client connection, clean-machine and public release remain `NOT VERIFIED`. Approval record: `Docs/ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md`; current independent evidence root: `.runtime/one-click-deployment-r5/run-20260910-052628-6f6d5d15ae56481092d378c74dd8e856`; local entry/verifier raw evidence remains under `.runtime/one-click-deployment-r5/ps51-entry-validation-20260910-044723-unicode-fix/` and `.runtime/one-click-deployment-r5/ps51-package-verifier-20260910-032800/`; prior r1/r2/r3/r4 references and failed diagnostics remain preserved.

| Gate | Result | Source / command |
|---|---|---|
| Solution build | `PASS` — `0` errors, `3` NU1900 warnings | `dotnet build .\ArcGIS-Pro-MCP.sln --no-restore --nologo` |
| Unit regression | `PASS` — `233` passed, `3` skipped, `0` failed, `236` total | `dotnet test .\Tests\UnitTests\ArcGISProMCP.UnitTests.csproj --no-build --no-restore --nologo` |
| Integration regression | `PASS` — `23/23` | `dotnet test .\Tests\IntegrationTests\ArcGISProMCP.IntegrationTests.csproj --no-build --no-restore --nologo` |
| Server regression | `PASS` — `41/41` | `dotnet test .\Tests\ArcGISProMCP.ServerTests\ArcGISProMCP.ServerTests.csproj --no-build --no-restore --nologo` |
| CLI Plan | `PASS` | `scripts/one-click-setup.ps1 -Action Plan -Json` under Windows PowerShell 5.1 |
| ZIP audit | `PASS` | `scripts/verify-one-click-package.ps1 -ZipPath ... -Json` |
| Extracted-root audit | `PASS` | Same verifier against a Chinese/space owned temporary extraction root |
| Isolated orchestration tests | `PASS` — `122` assertions; exact Windows PowerShell 5.1 entry `EXIT_CODE=0`; top-level Unicode path round-trip has no U+FFFD | `Tests/OneClickDeployment/one-click-deployment.tests.ps1`; persistent evidence `.runtime/one-click-deployment-r5/run-20260910-044724-cf90381b4f1c4b6b82d881d25cc7888b` |
| Registration | `SKIPPED` | Package builder and tests do not call `RegisterAddIn.exe` |
| Real installation | `NOT PERFORMED` | No real install root was used |
| Real client mutation | `NOT PERFORMED` | No user client configuration was applied |
| Real ArcGIS/MCP runtime | `NOT PERFORMED` | No ArcGIS Pro, Bridge or MCP runtime was started |
| Clean-machine acceptance | `NOT VERIFIED` | Requires a separately authorized clean/isolated machine gate |
| Preset GUI smoke | `PASS_SIMULATED_PRESET_ONLY` — 6 screenshots / 5 scenarios | Explicitly simulated worker scenarios; business callbacks are `NOT_VERIFIED`; `realMutation=NOT_PERFORMED` |
| Real GUI button callback smoke | `PASS_REAL_READ_ONLY_CALLBACKS` — 3 screenshots | Final extracted r5 package; actual `Preflight.PerformClick`=`PASS`, `Diagnose.PerformClick`=`WAITING_USER_START`; full Chinese-path InstallRoot + GUID and Codex target visible; exact Chinese messages survive UTF-8 round-trip with no replacement characters; controls/target/no-dialog/worker exits pass; `realMutation=NOT_PERFORMED` |
| Recovery-index fault injection | `PASS` | Owned-temp install-child failure, one-click state-write failure, real cancel callback, child-completion hard interruption, pending-index restart blocking, index+ledger rollback, fresh transaction, and recovery-index write failure with a distinct old-latest entry all pass; failed/cancelled/pending ledgers remain recoverable until explicit rollback |
| Fresh build/test baseline | `PASS` | Build `0 errors / 3 NU1900`; Unit `233 PASS / 3 SKIP / 0 FAIL` (`236` total); Integration `23/23`; Server `41/41`; baseline `.runtime/one-click-deployment-r5/baseline-20260910-032900` |
| Protected/process/listener post-check | `PASS` | `.runtime/one-click-deployment-r5/run-20260910-044724-cf90381b4f1c4b6b82d881d25cc7888b/49-post-check.json`; retained RunId `P57_B8C6FE8E`; retained GDB `65/0 locks`; `Phase4Test.gdb` `104 files / 0 locks`; no listeners on `6511/6520`; no orphan deployment processes |
| GUI visual/manual acceptance | `NOT VERIFIED` | Automated screenshots and callback evidence were visually inspected; real user deployment click-through was not performed |

## Protected-state evidence

- Actual retained fixture marker was read from `<user-home>\AppData\Local\Temp\ArcGISProMCP\Phase5_7_4_P57_B8C6FE8E\.arcgis-pro-mcp-test-owned` and contains `run=P57_B8C6FE8E`.
- Actual `fixture-manifest.json` at the same retained root also contains `runId=P57_B8C6FE8E`. Marker SHA-256 is `EC5CA5EFA9AA020698BD518395FF369B5726AC4641D736479299CC024ABE643B`; manifest SHA-256 is `390B3F30B5E50156147DDD6E120E1392BBF6E80146B839131438223411DF820A`.
- Retained fixture was not recreated, renamed, moved, or modified. Its accepted semantic health is carried forward unchanged; this candidate did not rerun native fixture health.
- `D:\ArcGIS-Pro-MCP\TestDate\Phase4Test.gdb` was read-only checked at `104` files and `0` lock files. No `MyProject1.aprx` copy was found under the repository tree, and no protected project/GDB target was passed to a mutating command.
- No historical ZIP, historical output, unknown lock, production registry, or client configuration was changed.

## Interpretation boundary

`PASS` above means the stated automated/local gate only. The Independent Gate Keeper has now approved the r5 implementation and local isolated automation for user real-machine trial; this does not mean the one-click package has completed real installation, connected to a real AI client, passed clean-machine acceptance, achieved public-release acceptance, or started a new Phase. The authoritative candidate report is [`ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md`](ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md), and the limited decision is recorded in [`ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md`](ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md).
