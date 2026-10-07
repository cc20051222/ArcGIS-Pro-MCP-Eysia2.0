# English index — ArcGIS Pro MCP Server

> This page is an **index**, not a translation of the tutorials. The tutorials below stay the single
> source of truth in Chinese; everything linked here is verified to exist in the published repository
> (checked with `git ls-files`, case-sensitive).

**ArcGIS Pro MCP Server** is an add-in that runs *inside* ArcGIS Pro and serves **239 GIS tools** to
MCP-speaking clients over a strictly local endpoint, `http://127.0.0.1:6520/mcp`. There is no public
listener, no `ApplyAll`, and no silent dependency installation: the plugin transaction and each client
configuration are separate, explicit actions.

**Supported target:** Windows x64, ArcGIS Pro 3.5 (other hosts are *not* inferred to work).
**Runtime needed for the shared binary:** .NET 8 runtime plus the built-in Windows PowerShell 5.1.
**License:** AGPL-3.0 (see [`LICENSE`](../LICENSE)); a derivative of the upstream `ArcGIS-Pro-MCP`
project, which has been AGPL-3.0/commercial dual-licensed since v1.1 (v1.0 and earlier remain under
their permanent MIT grant). Per AGPL §5 this repository contains modifications, documented in the
commit history and under `Docs/`; per AGPL §13, offering remote interaction over a network triggers the
full-source obligation.
**Not redistributed:** the Esri SDK (`sdk-refs/` is excluded — redistributing it would breach the Esri
EULA) and every release binary (`Release/*.zip`, the per-colleague folders, `Archive/`).

## Get the installer (read this first)

The one-click package is **not** in this repository. Ask the publisher for
`ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip` (**r29**, 2,278,077 bytes, SHA256
`099A4F3A575A65A1A8F9A7F424414465E676E605B12D8C1B9A7BA36E6F36B3CE`), then verify before unpacking:

```powershell
certutil -hashfile .\ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip SHA256
```

The value must match the shipped `.sha256` sidecar character for character. If it does not, stop and
report it to whoever gave you the file. Building the package yourself from source is described in
[`Docs/RELEASE_WORKFLOW.md`](RELEASE_WORKFLOW.md); the output stays local and is never committed.

## Install

| What | Where | In one line |
|---|---|---|
| Step-by-step install (6 steps) | [`Docs/教程-1-安装教程.md`](教程-1-安装教程.md) | verify → extract to a fresh D: folder → double-click `ONE-CLICK-SETUP.cmd` → start the MCP server inside ArcGIS Pro → connect one client; includes an error table and the uninstall/rollback route |
| Share-package entry points | [`Docs/SHARING_AND_SIMPLE_INSTALL.md`](SHARING_AND_SIMPLE_INSTALL.md) | what `INSTALL-PLUGIN.cmd` / `CONFIGURE-CODEX.cmd` / `START-HERE.cmd` do, and the distribution checks |
| One-click deployment guide (historical) | [`Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md`](ONE_CLICK_DEPLOYMENT_USER_GUIDE.md) | preflight, one plugin transaction plus zero or more client transactions, recovery semantics |
| Package-level verification | [`scripts/verify-one-click-package.ps1`](../scripts/verify-one-click-package.ps1) | re-checks layout, per-file digests, tool-count contract and tamper rejection |

## Use

| What | Where | In one line |
|---|---|---|
| Usage tutorial (9 chapters) | [`Docs/教程-2-使用教程.md`](教程-2-使用教程.md) | the **239** tools grouped by purpose, with example prompts, map/folder workflows, read-only vs write boundaries, and how to read logs |
| User guide (historical) | [`Docs/USER_GUIDE.md`](USER_GUIDE.md) | prerequisites, support scope, health/status, diagnostics, limitations |
| Client configuration | [`Docs/CLIENT_CONFIGURATION_GUIDE.md`](CLIENT_CONFIGURATION_GUIDE.md) | Codex / Cursor / DeepSeek Harness / Claude Desktop: directories, templates, credential boundaries |
| Authoritative client catalog | [`Config/client-catalog.json`](../Config/client-catalog.json) | 4 entries today: `codex`, `cursor`, `deepseek-harness`, `claude-desktop`; prose tool counts never substitute for this file or a real `tools/list` |

## For developers

- [`Docs/RELEASE_WORKFLOW.md`](RELEASE_WORKFLOW.md) — build, package, install/uninstall/rollback, `Plan`/`Validate` read-only entry points.
- [`Docs/RELEASE_NOTES_1.0.2.md`](RELEASE_NOTES_1.0.2.md) — release identity, support matrix, limits, rollback rules.
- `scripts/dev-env.ps1` and `scripts/user-workflow.ps1 -Action Plan|Validate|Package` in the repo root:
  the default action is read-only and creates nothing; there is no combined install-plus-configure action.

## Governance, baselines and honest status

English readers who want to judge the project's state should read these rather than infer from prose:

- [`AGENTS.md`](../AGENTS.md) — §3 security baseline, **§4 current baseline values**, §5 protected assets.
- [`Docs/PROJECT_CLOSEOUT_20261006.md`](PROJECT_CLOSEOUT_20261006.md) — closeout reconciliation and the
  full register of what is still **NOT VERIFIED**.
- [`Docs/ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md`](ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md) and
  [`Docs/ONE_CLICK_DEPLOYMENT_EVIDENCE_INDEX.md`](ONE_CLICK_DEPLOYMENT_EVIDENCE_INDEX.md) — hashes,
  tests and the unverified scope of the one-click candidate.

**Status vocabulary used in this repository:** `PASS CANDIDATE` means an implementation batch awaiting
independent acceptance; `NOT VERIFIED` means the capability was never demonstrated on a real machine;
`BLOCKED_BY_HARNESS` means the test could not execute in this environment. Historical acceptance of an
older package revision is **not** acceptance of the current one, and nothing here claims clean-machine
support, real client connectivity, or real installation success.
