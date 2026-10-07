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

## Software compatibility (can this machine run it?)

**Shortcuts**: the installer is described in the next section **"Get the installer"** (★ it is *not* inside this repository)｜install guide [`Docs/教程-1-安装教程.md`](教程-1-安装教程.md)｜usage guide [`Docs/教程-2-使用教程.md`](教程-2-使用教程.md).

Every value below was measured by the execution seat on **2026-10-08**, on this host and on Esri's official pages; version facts drift, so each row carries its own As-of date and source.

| Dimension | Measured conclusion | Evidence |
|---|---|---|
| **Operating system** | **Windows 11 x64 = tested** (this host is **build 26300**)｜★**Windows 10 = NOT VERIFIED** (no evidence taken, so no support claim) | `sys.getwindowsversion()` + registry `CurrentBuildNumber=26300` (the compat `ProductName` literal is stale, so the build number governs) |
| **ArcGIS Pro version** | ★**This project was tested on ArcGIS Pro 3.5 (`FileVersion=3.5.0.57366`)**; the one-click package ships **two payloads whose minimum host differs row by row**: **net6 payload → `3.0`** / **net8 payload → `3.5.0`** (★ installing the wrong payload fails); **3.6 / 3.7 supported per Esri's own compatibility promise, not verified on a machine in this project**; **anything outside 3.x (2.x and older, future 4.x) is not supported** | PE version resource (`\StringFileInfo\000004b0\FileVersion`) + `payloads[].arcgisProDesktopVersion` in `bundle-manifest.json` |
| **.NET runtime** | Runtime side = **.NET 8 runtime**; ★**source compile target = `net6.0`** - the **8 csproj under `Source/` are all net6** (`Compatibility` = `net6.0-windows`, the 7 shared ones = `net6.0`), while the **5 under `Tests/` = `net8.0`** | `<TargetFramework>` read from every `.csproj` (`bin`/`obj` excluded) |
| **PowerShell** | **Windows PowerShell 5.1** (ships with Windows; no extra install. PowerShell 7 optional) | Installer and verifier entry scripts |
| **Python environment** | ArcGIS Pro's built-in **`arcgispro-py3`** (present on this host, measured) | host probe |
| **MCP clients** | `codex` **P0** / `cursor` **P1** / `deepseek-harness` **P1** / `claude-desktop` **P2 = optional, template validation only** | `priority` / `required` / `applyMode` fields of `Config/client-catalog.json` |
| **Disk / memory** | **NOT VERIFIED** (minimum disk and memory requirement was not evidenced in this batch; this row claims no figures) | see above: no unevidenced claim |
| **Latest-version comparison (As-of 2026-10-08)** | this project is pinned to **3.5**; the newest 3.7-line item readable on Esri's patch index the same day = **3.7.2** (announcement page states *Published October 5, 2026*) ⇒ **this project trails the official latest minor by 2**; ★ the official SDK repository additionally lists **3.7 SDK = 3.7.0.1901 (requires the .NET 10 runtime)** / **3.6 SDK = 3.6.0.59527** | [Esri patch index](https://support.esri.com/en-us/patches-updates/2026) | [3.7.2 announcement](https://support.esri.com/en-us/patches-updates/2026/arcgis-pro-3-7-patch-2-3-7-2-announcement) | [official SDK releases](https://github.com/esri/arcgis-pro-sdk/releases) |
| **Pro patch level** | newest official 3.5-line patch = **3.5.9** (announcement page states *Published September 17, 2026*); ★**this host is 3.5.0 = unpatched**; highest 3.6-line item on the same index = **3.6.5** | [3.5.9 announcement](https://support.esri.com/en-us/patches-updates/2026/arcgis-pro-3-5-patch-9-3-5-9-announcement) |
| **Life-cycle window** | **NOT VERIFIED** - ★ this seat could not retrieve the official life-cycle matrix as readable text in this round, so **no date values are written here**; the matrix is published as a document, linked to the right | [ArcGIS Pro life-cycle page](https://support.esri.com/en-us/products/arcgis-pro/life-cycle) | [Esri Product Life Cycle document](https://content.esri.com/support/techarticles/product-life-cycle.pdf) |
| Install-location corroboration | on this host `%USERPROFILE%\Documents\ArcGIS\AddIns\ArcGISPro` **exists** = 2 GUID subdirectories / 3 files / **2,148,771 B** in total, of which this project's `ArcGISProMCP.Compatibility.esriAddInX` = **1,103,697 B** (plus a `.pre-5.5.4-185110.bak` of 185,110 B and an unrelated `GeoSceneProAIAppModule.esriAddinX` of 859,964 B); **no `AddIns` directory** under `%APPDATA%\ESRI` or `%LOCALAPPDATA%\ESRI` | recursive host enumeration (matches the install location recorded in the package manifest) |

★**Four honest boundaries (never omitted)**: (1) `cleanMachineAcceptance = NOT VERIFIED` (the package's own `bundle-manifest.json` declaration - a clean machine was never accepted); (2) **Windows 10 not verified**; (3) **ArcGIS Pro outside 3.x is not supported**; (4) **3.6 / 3.7 never verified on a real machine in this project**.

**Key sentence**: this project was measured on ArcGIS Pro **3.5** (`3.5.0.57366`). ArcGIS Pro **3.6** (officially .NET 8, same core as 3.5) and **3.7** (Esri states 3.x SDK extensions need no recompilation; this project's 264 `.cs` files under `Source/` contain zero `Clipboard` / `BinaryFormatter` / `DragAndDrop` hits, so they are not in the category Esri lists as requiring a rebuild) are **supported on the strength of Esri's compatibility promise but were not verified on a real machine here. Versions outside 3.x are not supported.**

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
