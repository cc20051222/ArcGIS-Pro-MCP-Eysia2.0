# F05 M4 benchmark calibration extension — PASS CANDIDATE

This directory now carries four calibration faces. The **M1 face** (D-085, 9 members, aggregate
`92ECD265B1DCE88AF3C55913F3B05AC6CFF6BAFF78B29D003B5E0D3EFB52B9FD`), the **M2 face** (D-089, 13 members,
`501A7E15600850E64FA078972F41DF4F22D1E10A2E62EBFC5063358FF13E10A7`) and the **M3 face** (D-093, 12 members,
`94577E76D1602F61A8A5B292E07EC5B0C93668597A2D002A706F7A1055E44D12`) are frozen contract surfaces (GATE-G206,
GATE-G214, GATE-G216) and are byte-unchanged here: this batch references them by id and path and copies or edits
neither. The **M4 face** (D-097) is additive with its own independent anchor.

Like the earlier faces this is a **definition candidate**: it fixes criteria before results. It is not evidence
that any M4 path works, that any map rendered, that any tool ran, or that any benchmark target was met.

## What the M4 face contains

| Class (D-097 scope) | Files |
|---|---|
| 1 scenarios, templates and mapping | `m4-scenarios-27.jsonl`, `m4-scenarios-27.md`, `m4-templates-14.jsonl`, `m4-templates-14.md`, `m4-scenario-template-map-v1.jsonl`, `m4-scenario-template-map-v1.md` |
| 2 capability surface (X1-X8, 48 candidates) | `m4-capability-surface-v1.jsonl` |
| 3 comparison rules v4 (additive) | `comparison-rules-v4.json`, `comparison-rules-v4.md` |
| 4 stress subset extension | `stress-40-m4-subset.jsonl` |
| 5 G-DOMAIN pre-calibration | `g-domain-precalibration-v1.json`, `g-domain-precalibration-v1.md` |
| closure hub | `m4-core-hidden-split-v1.json` |
| 6 freeze extension and anchor | `../tools/benchmark/freeze-benchmark-m4.py`, `benchmark-m4-s0.json`, `benchmark-m4-s0.txt` |

## Counting closure (the declaration this face is judged against)

- Scenarios: 6 (M1) + 22 (M2) + 5 (M3) + **27 (M4)** = **60 defined, 0 deferred**. The 27 are exactly the
  recomputed remainder of catalog §6: `S31`-`S45`, `S47`-`S51`, `S53`-`S59`.
- Templates: 6 (M1) + 16 (M2) + 0 (M3) + **14 (M4)** = **36 defined, 0 deferred**. The 14 are exactly the
  recomputed remainder of catalog §7: `TP11`-`TP16`, `TP18`, `TS11`-`TS14`, `TS16`-`TS18` — all breadth, since
  the 20 base templates closed at M3.
- Breadth arithmetic: base scenarios 6+19+5+0=30, breadth scenarios 0+3+0+27=30; base templates 6+14+0+0=20,
  breadth templates 0+2+0+14=16.
- Standard images: the nominal frame is 36 x 3 = 108, and all 36 templates now have frozen definitions. The three
  positive frozen inputs and their gold outputs per template are still not identified, so no standard-image count
  is claimed.
- Stress: 12 + 16 + 14 + **18** = **60 local rows** against the FINAL minimum of 40. Local ids,
  not FINAL numbering; completion is only measured when the runs happen.
- Core: 6 + 22 + 5 + **27** = **60 `CORE-*` rows**, all 60 catalogue scenarios now covered; M4
  plans 27 x 3 = 81 definition-level rounds and ran none.
- Hidden: 12 + 32 + 5 + **28** = **77 local candidates**, two per new template, references only,
  no gold content or path.
- Rules: v1 8 + v2 17 + v3 22 + **v4 26** = **73 ids** with zero collision and
  no new error code (the set stays 33).
- Capability surface: 48 rows, groups 6+8+6+8+8+6+4+2 = 48, classes R4/S3/W41 = 48, channels
  Geoprocessing 22 / Native 21 / Python 5 = 48, pre-registration coverage 47/48.

## Scope of the M4 slice

- Eight breadth domains, 27 scenarios, 14 templates, 48 candidates, one gate (G-DOMAIN).
- Public-name arithmetic recomputed and disclosed: 48 nominal become **45** public M4-batch names after removing
  one merged parameter branch (`build_offline_map_package` into `export_data_package` as `packageType`) and two
  internal services whose public names may be registered once at M4 (`compose_evidence_report`,
  `compose_presentation_deck`).
- Side-effect classes are read from the frozen candidate schemas, never assumed: 41 `file-output`, 4 `read-only`,
  3 `in-project-resource` (the scene and layer configuration trio) and **0** `in-place-raster` in this face. Only
  the file-output class may carry an overwrite-refusal assertion; the configuration trio is asserted as host state.
- Channels: 22 candidates are Geoprocessing-typed and **no accepted document names a GP tool for any of them**, so
  the whitelist axis stays unresolved and fail-closed rather than asserted either way; 21 are Native; 5 are Python,
  of which exactly two (`forecast_spatiotemporal_series`, `infer_approved_raster_model`) carry
  `pythonBridge=true` and are pinned to reviewed typed operations on the existing bridge with no new port and no
  free-form source (BRG-001).
- The M3 carried GP register is re-verified, not restated: all 12 cited-absent names still have no entry (0 of 53
  whitelist names is a Network Analyst solver), and the 9 cited-present plus 6 hydrology names are still present.
- Fixtures are cited read-only for structure. **No qualifying domain input exists in this workspace** for any of the
  eight domains — no time-enabled series, no vertical-datum elevation surface with known answers, no
  LAS/LAZ/COPC file, no multi-band product with published QA, no network dataset, no modern container adapter — so
  every M4 scenario carries `inputQualification.state = NOT-AVAILABLE-PENDING-USER` and its rules stay
  `PRE_CALIBRATION_DEFINITION_ONLY_NOT_VERIFIED`.
- Carried and still deferred: the three G-190 online/publish items stay `DEFERRED-G190-NOT-CALIBRATED` and S59 does
  not borrow their behaviour; TS08 keeps `NOT-AVAILABLE-PENDING-USER` under G-205 and no non-qualifying raster
  appears anywhere in this face.

## Fail-closed discipline introduced or extended here

1. **A domain row cannot be booked complete without the four G-DOMAIN artefacts** (known answer from an
   independent path, negative example, complete automatic result, updated instance). `g-domain-precalibration-v1`
   freezes that gate and states plainly that today the answer is missing for all eight domains.
2. **A missing algorithm is never inferred from a general executor.** Where FINAL names no GP tool, the criterion
   freezes the blocked assertion shape; expanding the 53-entry whitelist or granting a host licence is a user
   ruling, and `OSV-001` (error expression is not error computation) continues to bind.
3. **The 6511 boundary is a criterion.** BRG-001 fails any request that would add an interpreter, widen the bridge,
   open another port, or submit free-form source; the refusal is the correct answer, not a defect.
4. **Weights and models are gated artefacts.** MRC-001 requires a recomputable weight hash, licence, band order,
   normalisation, applicability domain and accelerator probe; a placeholder model is not a capability.
5. **Merge adjudications stay visible.** SCN-001 records that the scene and layer configuration trio may collapse
   into an accepted tool's parameter surface and blocks the new-name claim on that ruling instead of assuming it.
6. **Nothing pre-admits another line.** All 48 candidates carry
   `PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED`, and every count uses the accepted 197-tool baseline (G-216)
   recomputed at build time; the in-flight L1 and L4 working trees are not counted.

## Evidence limits

- No runtime evidence exists: no tool call, render, export, batch, cancellation drill, reviewer score or benchmark
  measurement. Nothing was installed, built or tested by this batch.
- Host extension availability (3D Analyst, Spatial Analyst, Image Analyst, Geostatistical Analyst, Network
  Analyst, pattern mining) is NOT VERIFIED in this workspace; nothing was started.
- Six numeric or structural candidate values are frozen as choices, each marked `d097CalibrationChoice` with its
  basis: the weight-manifest field set (MRC-001), the change-point position tolerance with effect-size error
  (STM-002), the forecast coverage band (STM-003), the volume recomputation agreement (SCN-003), the
  train/validation separation floor (RMO-003) and the effective-sample minimum for trends (RMO-004). FINAL
  requires the gates but publishes no number anywhere; these are candidates for commander review, not quotations.
- The 108/98/40/12/2x5-point/zero-serious-error global targets stay inherited unchanged and unrun.
- Any change to the M1, M2, M3 or M4 face after acceptance requires a Commander ruling; until then all four anchors
  are recomputable by the four freeze scripts.

## Sources

- `GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md` §§4, 8-10 (the §9:153 M4 exit row is the scope).
- `GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md` §4:64-156 (the 48 candidates), §5:158-174 (formats),
  §6:180-212 (the 60 scenarios), §7:215-240 (the 36 templates), §10:254-284 (the acceptance index).
- `GIS_AUTOMATION_FINAL_BENCHMARK_AND_DECISIONS_20260927.md` §§1-5, 7-8.
- `Docs/_gatekeeper/inbox/L2_INBOX.md` (the board carrying the D-097 ticket) and the GATE-G216 ruling.
- `run-20260928-d082/f03a-2-new48-terminal-review.md` (X01-X48 adjudication, §5 registration wording),
  `f03a-catalog-ledger.json` (the machine ledger), `f03b-5-schemas/` (48 frozen input surfaces),
  `f03b-8-support-matrix-freeze.md` (format literals absent), `f03b-9-licensing-approval-policy.md`.
- `run-20260928-d083/a-roster/prereg-131-table.md` (131 rows = 84 base + 47 new; contract kinds).
- `GATE-G205`, `GATE-G206`, `GATE-G214`, `GATE-G216` rulings; `DECISIONS_LOG.md` G-190 (deferred trio).
- `.runtime/evolution/v5-f/run-20260929-d097/field-schema-draft-m4.md` (schema first), `m4-mapping-notes.md`,
  `evidence-index.md`.
