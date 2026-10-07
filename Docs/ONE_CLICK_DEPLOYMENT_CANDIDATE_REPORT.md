# One-click Deployment Share Package — Candidate Report

> **历史时点声明（D-129 README 链接陈旧面补声明批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **2026-09-10（文内唯一 ISO 日期实测；正文跨 `1.0.2-r1`…`1.0.2-r5` candidate 与 Phase 5.8.2／Phase 7 表述）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。
> ★ 本头部点名的当时点位（由本批扫描件机械实测，非人工摘录；现值见上列两件）：`-r5`・`361FC84F`・`3924F9B3`

## 1. Candidate conclusion

```text
ONE_CLICK_DEPLOYMENT = INDEPENDENT GATE PASSED / AUTHORIZED FOR USER REAL-MACHINE TRIAL
Phase 7 historical formal acceptance = PRESERVED
Phase 7 was not reopened and no new Phase was started
```

The candidate package adds a bounded one-click deployment experience around the already accepted ArcGIS Pro MCP `1.0.2` payload. The Independent Gate Keeper has approved the r5 implementation and local isolated automation scope for user real-machine trial. This is a limited gate decision, not formal acceptance of real installation, real AI-client connection, clean-machine deployment, or public release.

The previous r1 candidate was rejected by the independent Gate Keeper: repeated GUI starts left the UI at `执行中`, the target display was empty, and PowerShell exposed worker/null-array lifecycle errors. r1 and its hash are preserved unchanged as historical evidence. The original r2 review hash was `CC1CC37C8B00918313203E45656183A9FE6656D0E9D1FC664D096349D45AFD53`; the same-name r2 file was later overwritten by a different `33AC18A10E2DF4EA7D0D0BE646C777BC2EB535D8C552F1EA846B75956936DABF` candidate. r3 and r4 remain preserved prior remediation candidates; this report is for the separately named r5 candidate. The current limited decision is recorded in [ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md](ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md).

### Independent Gate Keeper decision（2026-09-10）

- Decision: **r5 implementation and local isolated automation = PASS**; **AUTHORIZED FOR USER REAL-MACHINE TRIAL**.
- Independent evidence: `.runtime/one-click-deployment-r5/run-20260910-052628-6f6d5d15ae56481092d378c74dd8e856`; exact Windows PowerShell 5.1 entry returned `EXIT_CODE=0`, `122/122` assertions passed, and the three real read-only GUI button screenshots were independently inspected.
- Scope boundary: this does not verify or accept real Add-in installation, real AI-client connection, clean-machine deployment, public release, or overall new-package acceptance. Those remain `NOT VERIFIED`.

## 2. Package identity

| Field | Result |
|---|---|
| ZIP | `Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip` |
| ZIP size | `331679` bytes |
| ZIP SHA-256 | `3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652` |
| SHA-256 sidecar | `Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip.sha256` |
| Previous r4 candidate | Preserved unchanged: `330602` bytes; SHA-256 `AE9C5EF2421D4E73F8BA62AEA5FBFEF2B4EFF0DA7D2EF24B01EED93D3103A134` |
| Previous r3 candidate | Preserved unchanged: `329568` bytes; SHA-256 `7EEC03C0E0DAD1F3363147FE614B72C0CB41DB3A80262CC76453F4AAC8B1E3EA` |
| Bundle manifest | `arcgis-pro-mcp-one-click-bundle-v1`; `cleanMachineAcceptance=NOT VERIFIED` |
| ZIP/extracted file audit | `PASS`; `23` physical files, with `22` manifest-listed files plus the manifest |
| Reused accepted payload | `269548` bytes; SHA-256 `361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A` |
| Add-in ID | `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}` |
| Endpoint/tool contract | `http://127.0.0.1:6520/mcp`; canonical production tools `30` |

The rejected r1 artifact remains a separate preserved file: `Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r1-Windows-x64.zip`, `316207` bytes, SHA-256 `3303E02CFAC50EB02B74F4C0B4BDA77C3BE2788607920EE1402B43B19102D7B1`.

The existing `Release/ArcGIS-Pro-MCP-1.0.2-Windows-x64.zip` was preserved as a separate historical artifact.

## 3. Implementation gates

| Gate | Result | Notes |
|---|---|---|
| Dependency-free verifier | `PASS` | Checks ZIP or extracted root, traversal/duplicates, listed-file hashes, payload, release manifest, forbidden content, endpoint and tool count. |
| One-click CLI plan | `PASS` | Windows PowerShell 5.1 `-Action Plan -Json` returned success; no `ApplyAll`; mutation requires explicit confirmation in GUI. |
| One-click orchestration | `PASS CANDIDATE` | Read-only preflight precedes plugin mutation; install and each selected client are separate transactions; recovery index is registered before state persistence and retry/recovery use the indexed ledger. |
| Cancellation | `PASS` in isolated test | Pre-mutation and post-plugin cancellation boundaries are explicit; a real cancel callback is exercised after plugin child completion; cancelled transactions remain indexed/recoverable; GUI uses a cancellable background worker and does not force-kill. |
| Failure handling | `PASS` in isolated test | Tamper, traversal, missing state and missing client target fail closed; child failure and one-click state-write failure retain the original recovery index/ledger and block silent overwrite. A self-owned hard interruption after child completion is recovered by restart blocking on the pending transaction; recovery-index write failure leaves a distinct old latest entry intact. |
| Launcher package | `PASS` | Package-root `.cmd` files resolve package-local `scripts/`; recovery and uninstall/rollback entries are included. |
| GUI worker lifecycle | `PASS CANDIDATE` in two separated smoke modes | Preset worker smoke is explicitly `PASS_SIMULATED_PRESET_ONLY`; final extracted r5 package invokes real read-only `Preflight.PerformClick` and `Diagnose.PerformClick`, with 3 screenshots, complete Chinese-path InstallRoot + GUID and Codex target, exact UTF-8 Chinese messages, recovered controls, no dialog/error, and worker exit codes `0/0`. |

## 4. Verification executed

Commands executed in the repository root:

```powershell
dotnet build .\ArcGIS-Pro-MCP.sln --no-restore --nologo
dotnet test .\Tests\UnitTests\ArcGISProMCP.UnitTests.csproj --no-build --no-restore --nologo
dotnet test .\Tests\IntegrationTests\ArcGISProMCP.IntegrationTests.csproj --no-build --no-restore --nologo
dotnet test .\Tests\ArcGISProMCP.ServerTests\ArcGISProMCP.ServerTests.csproj --no-build --no-restore --nologo
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\one-click-setup.ps1 -Action Plan -Json
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-one-click-package.ps1 -Configuration Debug -CandidateRevision r5 -Force
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests\OneClickDeployment\one-click-deployment.tests.ps1
```

Results:

- Solution build: `0` errors; `3` `NU1900` vulnerability-feed warnings.
- Unit: `233 PASS / 3 SKIP / 0 FAIL` (`236` total).
- Integration: `23/23 PASS`.
- Server: `41/41 PASS`.
- Package builder: `PASS`; registration, real installation and client configuration were `SKIPPED`.
- Isolated one-click harness: `PASS`, `122` assertions; it uses only owned temporary install/state/project roots for TestMode mutation and recovery checks. The recovery matrix covers install-child failure, state-write failure, real post-plugin cancellation callback, child-completion hard interruption and restart blocking, recovery-index write failure with distinct old-latest coexistence, duplicate blocking, same-index rollback and fresh transaction allocation. The GUI assertions also require the exact plugin target, Chinese path, UTF-8 callback messages, and a top-level stdout/screenshot-path round-trip without U+FFFD replacement characters; failure runs preserve complete owned-temp artifacts before cleanup.
- ZIP verifier: `PASS` for the actual r5 ZIP under Windows PowerShell 5.1; extracted-root verifier: `PASS` for an owned temporary path containing Chinese characters and spaces.
- Exact Windows PowerShell 5.1 entry validation: `PASS`, `EXIT_CODE=0`, `122` assertions; explicit UTF-8 encoded bootstrap, parent stdout/stderr UTF-8 decoding, dot-source scope preservation, and top-level Chinese/space path round-trip all pass without U+FFFD replacement characters. Raw stdout/stderr and runtime metadata are retained under `.runtime/one-click-deployment-r5/ps51-entry-validation-20260910-044723-unicode-fix`. Earlier r4/r5 ParserError, hash-compatibility, wrapper/relative-path, and intermediate Unicode diagnostics remain preserved and are not counted as PASS evidence.
- Preset GUI smoke: `PASS_SIMULATED_PRESET_ONLY`; six screenshots and five serial scenarios, with business callbacks explicitly `NOT_VERIFIED`.
- Extracted-package real GUI button smoke: `PASS_REAL_READ_ONLY_CALLBACKS`; three screenshots from the final r5 extraction, actual `Preflight.PerformClick`/`Diagnose.PerformClick`, complete plugin InstallRoot + Add-in GUID and Chinese Codex target visible, `Preflight=PASS`, `Diagnose=WAITING_USER_START`, exact Chinese messages in the report/UI log, no U+FFFD replacement characters, `controlsRecovered=true`, `correctTarget=true`, `noUnexpectedDialog=true`, `workerExitCodes=[0,0]`, and no error log. This is automated callback evidence, not real user click-through acceptance.

The `NU1900` result is a network/package-audit warning from the existing build baseline, not a build error. Historical test counts in older Phase documents remain historical facts; this report records the fresh counts from this candidate run without rewriting those records.

## 5. Protected-state and process post-check

- The actual retained marker and manifest were read before recording fixture identity. Both identify `P57_B8C6FE8E`; the retained fixture was not touched. r5 post-check confirms the same identity and retained GDB `65` files / `0` locks; semantic fixture health remains carried forward, not re-executed by this package workstream.
- `TestDate/Phase4Test.gdb` remained `104` files / `0` locks. No package/test target referenced `MyProject1.aprx`, retained fixture data, historical outputs, or unknown locks.
- No `RegisterAddIn.exe`, ArcGIS Pro, Bridge, testhost or MCP listener was started by the package build/tests. At the r5 post-check, `ArcGISPro`, `RegisterAddIn` and `testhost` were absent and TCP `6511/6520` had no listener; no relevant deployment process remained.
- .NET MSBuild node-reuse processes from the normal solution build were observed; they were not deployment processes and were not force-terminated. No force-kill or broad process cleanup was used.
- The previous accepted ZIP and accepted `1.0.2` payload identity remain intact.

Persistent local r5 evidence is stored at `.runtime/one-click-deployment-r5/run-20260910-044724-cf90381b4f1c4b6b82d881d25cc7888b`. It contains numbered command reports, the package hash record, the 122-assertion final report, raw per-command stdout/stderr logs, separate simulated and real-button GUI evidence, Chinese-path/UTF-8 assertions, real-callback cancellation, hard-interruption recovery, pending-index restart, recovery-index write-failure/old-latest and rollback/fresh reports, and the r5 protected post-check (`49-post-check.json`). Independent Gate Keeper evidence is separately preserved at `.runtime/one-click-deployment-r5/run-20260910-052628-6f6d5d15ae56481092d378c74dd8e856`. The r1 rejection plus r2, r3 and r4 roots remain preserved, distinct, and are not counted as r5 evidence. The prior Unicode failure and intermediate diagnostic runs remain preserved; the latest failed diagnostic’s complete owned-temp snapshot is indexed by `.runtime/one-click-deployment-r5/run-20260910-044131-5d0b13e51ab74d15b0fef348813c3f62/failure-artifacts.json`.

## 6. Not verified / not performed

- Real Add-in installation into the user’s ArcGIS Pro Add-ins directory.
- Real ArcGIS Pro launch, Add-in `Start`, MCP `tools/list`, or HTTP `ping` in this candidate.
- Real Codex, Cursor or DeepSeek client mutation/connection verification.
- Clean-machine installation, dependency installation/repair, code signing, distribution permissions, and GUI click-through visual acceptance.
- Existing project/GIS mutation, fixture preparation, production fixture actions, or any Phase 5.8.2+ work.

These are deliberate evidence boundaries, not silently converted to PASS.

## 7. Known limitations carried forward

The accepted project limitations remain unchanged: `list_maps`/`get_map_info=PARTIAL`; dataset/raster info `LIMITED IMPLEMENTATION`; ArcGIS Pro 3.5 `ArcGISProject.isDirty` unavailable but non-blocking; HTTP client-disconnect cancellation `NOT VERIFIED`; seven historical HTTP transport cases `BLOCKED_BY_HARNESS`; `select_layer` exposes only `mapName` and `layerName` and has no deterministic OID selection contract; Claude Desktop remains optional/template-only. The one-click package does not reopen or resolve these items.

## 8. Independent review request and stop point

Independent Gate Keeper has reviewed the package, manifest, verifier output, isolated test output, protected-state evidence and the explicit `NOT VERIFIED` boundaries. The limited post-review status is:

```text
ONE_CLICK_DEPLOYMENT = INDEPENDENT GATE PASSED / AUTHORIZED FOR USER REAL-MACHINE TRIAL
```

No commit or push was performed; no real installation or client mutation has been performed in this repository session. The decision authorizes a separately controlled user real-machine trial only. Public release and overall new-package acceptance remain `NOT VERIFIED`; no new Phase is started by this report.

Evidence index: [`ONE_CLICK_DEPLOYMENT_EVIDENCE_INDEX.md`](ONE_CLICK_DEPLOYMENT_EVIDENCE_INDEX.md). Plan: [`ONE_CLICK_DEPLOYMENT_PLAN.md`](ONE_CLICK_DEPLOYMENT_PLAN.md).
