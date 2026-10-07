# One-click Deployment Share Package — Plan

## Status and boundary

这是一个独立于历史 Phase 7 formal acceptance 的部署体验工作流。它复用已接受的 ArcGIS Pro MCP `1.0.2` binary artifact，不重新打开 Phase 7，也不改变生产 Tool Registry、MCP endpoint、Python Bridge lifecycle 或既有 acceptance 结论。

当前状态：

```text
ONE_CLICK_DEPLOYMENT = PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER
Phase 7 = FORMALLY ACCEPTED / PASS / COMPLETE（历史/基础范围保持不变）
Real installation, real client mutation, clean-machine acceptance = NOT VERIFIED
```

r1 was rejected by the independent Gate Keeper because repeated GUI starts left the window at `执行中` with an empty target and exposed PowerShell worker/null-array errors. r1 remains historical evidence and is not reused as a PASS. The original r2 review hash was `CC1CC37C8B00918313203E45656183A9FE6656D0E9D1FC664D096349D45AFD53`; the same-name r2 file was later overwritten by a different `33AC18A10E2DF4EA7D0D0BE646C777BC2EB535D8C552F1EA846B75956936DABF` candidate. r3 and r4 remain preserved previous candidates; r5 is the current separately named candidate and formal acceptance remains pending.

## Fixed release contract

| Item | Contract |
|---|---|
| Deployment package | `ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip` |
| Current candidate ZIP | `331679` bytes; SHA-256=`3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652` |
| Previous r4 candidate ZIP | `330602` bytes; SHA-256=`AE9C5EF2421D4E73F8BA62AEA5FBFEF2B4EFF0DA7D2EF24B01EED93D3103A134` |
| Previous r3 candidate | `ArcGIS-Pro-MCP-OneClick-1.0.2-r3-Windows-x64.zip`, preserved unchanged; `329568` bytes; SHA-256=`7EEC03C0E0DAD1F3363147FE614B72C0CB41DB3A80262CC76453F4AAC8B1E3EA` |
| Rejected historical package | `ArcGIS-Pro-MCP-OneClick-1.0.2-r1-Windows-x64.zip` (preserved unchanged) |
| Base release | `1.0.2` |
| Accepted payload size | `269548` bytes |
| Accepted payload SHA-256 | `361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A` |
| Add-in ID | `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}` |
| MCP endpoint | `http://127.0.0.1:6520/mcp` |
| Canonical production tools | `30` (`mcp_auth` excluded) |
| Supported host | Windows x64 + ArcGIS Pro 3.5 |

The previously accepted `Release/ArcGIS-Pro-MCP-1.0.2-Windows-x64.zip` is a separate historical artifact and must remain unchanged.

## Delivered scope

- `scripts/one-click-setup.ps1`: Windows PowerShell 5.1 GUI/CLI orchestrator with read-only Plan/Preflight, one explicit plugin transaction, independent single-client transactions, bounded connection diagnosis, cancellation-safe worker flow, recovery-index-backed Retry/Rollback/Uninstall controls, explicit UTF-8 child-process I/O, complete plugin target display, and no `ApplyAll`.
- `scripts/verify-one-click-package.ps1`: dependency-free ZIP/extracted-root verifier. It checks logical-path safety, manifest completeness, per-file hash/length, exact accepted payload identity, release manifest, endpoint/tool-count contract, and forbidden user/GIS/runtime/lock content.
- `scripts/build-one-click-package.ps1`: reproducible staging and packaging from the exact accepted payload. It performs no registration, installation, runtime launch, client mutation, or dependency installation.
- `Distribution/ONE-CLICK-SETUP.cmd`, `RECOVERY-MENU.cmd`, `ROLLBACK-PLUGIN.cmd`, `UNINSTALL-PLUGIN.cmd`, `RESTORE-CLIENT.cmd` and the Chinese quick-start README.
- `Tests/OneClickDeployment/one-click-deployment.tests.ps1`: owned-temporary, mutation-isolated verification harness covering the r5 transaction, Windows PowerShell 5.1 exact entry, real read-only GUI button callbacks, Chinese-path round-trip, cancellation callback, hard-interruption recovery, and recovery-index fault-injection lifecycle.

## Safety contract

- The package never includes `MyProject1.aprx`, `TestDate/Phase4Test.gdb`, retained fixture data, historical outputs, user client configuration, credentials, backups, or unknown locks.
- No real installation or real client mutation is performed by the builder or automated tests. TestMode performs only controlled mutations under owned temporary roots, then verifies recovery/idempotency; dry-run transactions remain non-mutating.
- ArcGIS Pro, Cursor, Codex, DeepSeek, Bridge and MCP are not started by the package build/tests. The installer waits for the user to open ArcGIS Pro and click MCP → Start; it does not force-close processes.
- Install, each client configuration, rollback and uninstall retain separate transaction boundaries and safe failure codes; a recoverable failure/cancel transaction is indexed before state mutation and cannot be silently overwritten by a later deployment.
- HTTP port listening is never treated as MCP connection proof. Real client connection evidence remains a separate gate.

## Verification sequence

1. Verify the repository source and exact accepted payload identity.
2. Build the solution with `--no-restore` and record warnings/errors.
3. Build the independent r5 one-click ZIP and verify the actual ZIP plus extracted-root contents; retain the rejected r1 artifact and the previous r2/r3/r4 candidate lineage and hashes.
4. Run the owned-temp one-click test harness against the separately named r5 package, including an explicitly simulated preset smoke, extracted-package GUI `Preflight.PerformClick`/`Diagnose.PerformClick` read-only callbacks, full target and Chinese-path assertions, exact UTF-8 message round-trip, a Windows PowerShell 5.1 exact-entry run, a real cancel callback at the post-plugin boundary, self-owned child-completion hard-interruption recovery, pending-index restart blocking, recovery-index write-failure with distinct old-latest coexistence, install failure/state-write failure, same-index rollback, duplicate blocking, fresh-transaction proof, tamper/traversal, missing-state, multi-client/unknown-client rejection and pre-mutation client-preflight cases.
5. Perform a read-only protected-state/process/listener post-check.
6. Publish a candidate report and stop for Independent Gate Keeper review.

## Explicit non-goals

This work does not change production tools, add arbitrary Python MCP, add production fixture actions, alter the accepted Add-in, modify ArcGIS projects/GDBs, apply real client configuration, prove clean-machine support, or begin another Phase.
