# ArcGIS Pro MCP Server 1.0.2 — Release Notes and Handoff Basis

> **历史时点声明（D-129 README 链接陈旧面补声明批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **无可锚 ISO 日期的 Phase 7.7／7.8 正式接受代际（文内不含任何 `2026-mm-dd` 式日期，故本头部不写绝对日期；时点由正文「Phase 7.7」与「Phase 7.8」阶段表述推定。该代际的绝对日期由同批已声明的 `Docs/phases/PHASE_07_7_OVERALL_RELEASE_ACCEPTANCE_CANDIDATE.md` 与 `PHASE_07_8_FINAL_HANDOFF_CANDIDATE.md` 两件头部所锚，本件不代为断言）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。
> ★ 本头部点名的当时点位（由本批扫描件机械实测，非人工摘录；现值见上列两件）：`361FC84F`

## Release status

This document records the formally accepted `1.0.2` release identity and the
operator-facing handoff boundary. Phase 7 overall and the implemented
canonical 30-tool whole-project scope are formally accepted and complete. The
future `112+` expansion remains outside the accepted release scope.

```text
Phase 7.7 overall = FORMALLY ACCEPTED / PASS
Phase 7.8 = FORMALLY ACCEPTED / PASS
Phase 7 overall = FORMALLY ACCEPTED / PASS / COMPLETE
1.0.2 planned release scope / implemented canonical 30-tool whole-project scope = FORMALLY ACCEPTED / PASS / COMPLETE
```

Final acceptance report: [PHASE_07_FINAL_PROJECT_ACCEPTANCE.md](phases/PHASE_07_FINAL_PROJECT_ACCEPTANCE.md). The prior candidate report and manifests remain preserved as immutable pre-acceptance evidence.

## Exact release identity

| Item | Value |
|---|---|
| Product | ArcGIS Pro MCP |
| Release version | `1.0.2` |
| ArcGIS Pro target | `3.5.0`, x64 |
| Target framework | `net8.0-windows` |
| Add-in ID | `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}` |
| AssemblyVersion | `1.0.0.0` (intentional binary-compatibility identity) |
| Package | `ArcGISProMCP.Compatibility.esriAddInX` |
| Package bytes | `269548` |
| Package SHA-256 | `361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A` |
| Release manifest | `Source/ArcGISProMCP.Compatibility/bin/x64/Debug/net8.0-windows/ArcGISProMCP.Compatibility.release-manifest.json` |
| Manifest bytes | `7952` |
| Manifest SHA-256 | `335A652AE3CFB880E63F01AFC03ADF0D1E8C3C31E2041E2529EA68429E912393` |
| Package entries | `20`; nested Add-in count `0` |
| Bridge artifact | `Install/PythonBridge/bridge_runner.py`, one entry, `15494` bytes |
| Bridge SHA-256 | `40B2A00061EB35EA83A7729E9E56FC020B020C2476E2D60123CA2356B33A6CA7` |

The installed package is byte-identical to the accepted source candidate. The
historical installed `1.0.1` package is retained only as the failed-run
rollback baseline; it must not be restored after a successful `1.0.2` release
path.

## Supported contract and client matrix

Production architecture:

```text
AI Client -> arcgis-pro-mcp -> http://127.0.0.1:6520/mcp
           -> ArcGIS Pro MCP Add-in -> Tool Router
           -> GIS / Geoprocessing / PythonBridge -> ArcGIS Pro host
```

The canonical production catalog contains exactly `30` distinct tools.
`mcp_auth` is a client-scoped helper and is excluded from the production
registry. The project does not claim that `112+` tools are implemented.

| Priority | Client | Handoff status | Boundary |
|---|---|---|---|
| P0 | Codex | FORMALLY ACCEPTED / PASS | Fresh session; `ping`, `python_bridge_ping`, `get_arcgis_version` passed; exact 30-name catalog accepted as initialize-equivalent. Raw protocol envelopes/session identity are not exposed by the client surface. |
| P1 | Cursor | FORMALLY ACCEPTED / PASS | Fresh project snapshot connected with `toolCount=30`; same-session `ping` returned `pong`. Raw initialize/tools-list/name envelopes were not exposed. |
| P1 | DeepSeek Harness | FORMALLY ACCEPTED / PASS | Fresh conversation exposed exact raw names `30`, distinct `30`, duplicate `0`; one same-session `ping` returned `pong`. Raw initialize envelope was not separately exposed. The accepted client gate preserved user-owned PID `20424`/port `3080` in its historical snapshot; the final read-only audit classified the current Harness as NOT RUNNING / external user-owned state, without a fresh reconnection claim. |
| P2 | Claude Desktop | OPTIONAL / TEMPLATE-ONLY | Configuration template validation only; no fresh client connection or tools-call acceptance is claimed. |

## Operator workflow references

Use the following documents in this order:

1. [README.md](../README.md) — project entry, fixed contract and safety boundary.
2. [USER_GUIDE.md](USER_GUIDE.md) — prerequisites, Plan/Validate, package, client and recovery operations.
3. [RELEASE_WORKFLOW.md](RELEASE_WORKFLOW.md) — package-only, transactional Install/Uninstall/Rollback and explicit ledger rules.
4. [CLIENT_CONFIGURATION_GUIDE.md](CLIENT_CONFIGURATION_GUIDE.md) — one-client-at-a-time configuration and Restore rules.
5. [PHASE_07_8_FINAL_HANDOFF_CANDIDATE.md](phases/PHASE_07_8_FINAL_HANDOFF_CANDIDATE.md) — final evidence index and current acceptance boundary.

The safe default is read-only `Plan`, followed by read-only `Validate`.
Package generation uses `-SkipRegistration`. Install, Uninstall, Rollback,
ClientApply and ClientRestore require explicit authorization and exact owned
targets; there is no `ApplyAll` action.

## Rollback and recovery rule

For a failed transaction:

1. Preserve the owned ledger and all evidence.
2. Do not delete unknown locks or manually delete a target directory.
3. Accept automatic recovery only when the same ledger records verified rollback `PASS`.
4. Otherwise use the exact `Rollback` action with the same owned transaction root and explicit `LedgerPath`.
5. Reconcile the protected `1.0.1` baseline only for a failed-run recovery; do not silently downgrade a successful installed `1.0.2` release.

For client configuration failures, stop the affected client and use the same
client's `Validate`/`ClientRestore`; stale-backup detection is a safety stop.
Do not print or add credentials.

## Known limitations and evidence classification

- `3 NU1900` are retained offline vulnerability-feed warnings, not product failures.
- Capability-aware test `SKIPPED` results remain skips and are not converted to PASS.
- Clean-machine support remains `NOT VERIFIED`.
- HTTP client-disconnect cancellation remains `NOT VERIFIED`.
- Seven HTTP transport tests remain `BLOCKED_BY_HARNESS`.
- Raw `initialize`/`tools/list` envelope visibility remains client-surface limited.
- `list_maps` and `get_map_info` remain `PARTIAL`.
- `get_dataset_info` and `get_raster_info` remain `LIMITED IMPLEMENTATION`.
- `ArcGISProject.isDirty` is unavailable/non-blocking on ArcGIS Pro 3.5.
- `select_layer` exposes only `mapName` and `layerName`; deterministic OID selection and selection-mutation PASS are not claimed.
- Package-only registration is recorded as `SKIPPED`; it is not a registration failure.

These classifications are part of the release truth and must remain visible in
future handoffs.

## Protected assets

Do not modify `MyProject1.aprx`, `TestDate/Phase4Test.gdb`, the retained
fixture `P57_B8C6FE8E`, historical outputs, or unknown `.lock/.sr.lock` files.
The accepted handoff evidence recorded unchanged `MyProject1.aprx`, shared
GDB `104 files / 0 locks`, retained GDB `65 files / 0 locks`, the accepted
historical DeepSeek PID/3080 snapshot, and the installed release identity
above. The final read-only audit found the user-owned Harness not running;
cause/actor is NOT VERIFIED / external user-owned state change.
