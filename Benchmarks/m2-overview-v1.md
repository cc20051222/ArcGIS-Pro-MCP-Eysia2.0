# F05 M2 benchmark calibration extension — PASS CANDIDATE

This directory now carries two calibration faces. The **M1 face** (D-085, nine member files, aggregate `92ECD265B1DCE88AF3C55913F3B05AC6CFF6BAFF78B29D003B5E0D3EFB52B9FD`) is frozen as contract surface by GATE-G206 and is byte-unchanged by this batch: nothing here edits, renames or re-freezes it. The **M2 face** (D-089) is additive: new files, new rule bodies, new local IDs, and an independent anchor recorded in `benchmark-m2-s0.json`.

Like the M1 face, this is a **definition candidate**. It fixes judgement criteria before results. It is not evidence that any M2 path works, that any map rendered, that any tool ran, or that any benchmark target was met.

## What the M2 face contains

| Class (D-089 scope) | Files |
|---|---|
| 1 M2 template/scenario mapping | `m2-scenario-template-map-v1.jsonl`, `m2-scenario-template-map-v1.md`, `m2-scenarios-22.jsonl`, `m2-scenarios-22.md`, `m2-templates-16.jsonl`, `m2-templates-16.md` |
| 2 Comparison rules v2 (additive) | `comparison-rules-v2.json`, `comparison-rules-v2.md` |
| 3 Stress subset extension | `stress-40-m2-subset.jsonl` |
| 4 Z09-Z11 pre-calibration criteria | `z09-z11-precalibration-v1.json`, `z09-z11-precalibration-v1.md` |
| Closure hub (reference integrity) | `m2-core-hidden-split-v1.json` |
| 5 Freeze extension and independent anchor | `../tools/benchmark/freeze-benchmark-m2.py`, `benchmark-m2-s0.json`, `benchmark-m2-s0.txt` |

## Scope of the M2 slice

- Templates: 16 newly defined (TP04, TP05, TP06, TP07, TP08, TP09, TP10, TP17, TS03, TS04, TS05, TS06, TS07, TS09, TS10, TS15) plus the 6 carried-in M1 templates (TP01, TP02, TP03, TS01, TS02, TS08) referenced by ID = 22 in surface; 14 deferred.
- Scenarios: 22 newly defined (S01, S02, S03, S04, S05, S06, S08, S10, S11, S12, S13, S15, S16, S21, S26, S27, S28, S29, S30, S46, S52, S60) plus the 6 carried-in M1 scenarios = 28 in surface; 32 deferred.
- Pillars: quality and provenance tool surface (the nine P1/P2 tools), batch pages and project style (Z09), multi-media reflow (Z10), complex-content protection (Z11), same-source report/PPTX (X8).
- Comparison rules v2 adds 17 rule bodies and inherits the 8 v1 rules by ID; v1 bytes are unchanged.
- Stress adds 16 local IDs `ST-M2-01`-`ST-M2-16`; the M1 12 stay in the frozen file.
- Hidden set adds 32 candidate IDs (two per newly defined template) with criterion references only; no gold content or path.
- TS08 keeps its G-205 status: gold artifact `NOT-AVAILABLE-PENDING-USER`, and `multi.tif` is used neither as a qualifying input nor as a substitute anywhere in the M2 face.

## Counting convention

FINAL §9 M2 states the exit needs at least 16 templates and 18 scenarios but does not enumerate which ones, and it does not say whether M1 templates count. This batch therefore states both counts: 16 new templates and 22 new scenarios on their own, and 22 templates and 28 scenarios on the combined M2 surface. The 6 M1 templates and 6 M1 scenarios are carried by reference only, so no M1 byte is restated or modified.

## Closure definitions recomputable by the verifier

1. scenario.templateId is in the 16 newly defined or the 6 carried templates.
2. template.scenarioIds equals the set of scenarios whose templateId names that template (bidirectional).
3. every rule ID referenced by a scenario, template, stress row or hidden criterion exists in v1 or v2; each single-tool rule binding also appears in the referencing scenario's toolSurface (multi-binding design rules stay named in the rule object).
4. core and hidden IDs: scenario.coreHiddenRefs equals CORE-M2-<scenarioId> plus the two hidden IDs of its template, and m2-core-hidden-split-v1.json maps the same sets back.
5. stress: scenario.stressRefs and stress.m2ScenarioRefs agree in both directions; stress.templateRefs and stress.coreRefs resolve.
6. toolSurface names are a subset of the 170 active contract-snapshot names plus exactly the nine D-088 planned tools; no ps_* name appears.
7. arithmetic: 16 + 6 + 14 = 36 templates, 22 + 6 + 32 = 60 scenarios.

## Evidence limits

- No runtime evidence exists for any of this: no tool call, render, export, batch run, reviewer score or benchmark result.
- The nine P1/P2 tools are concurrent L1 work (D-088); every criterion bound to them is `PENDING-D088-NOT-ACCEPTED-NOT-VERIFIED`. The accepted baseline is 170 tools (G-209); at build time the working-tree contract snapshot listed 179 unique names = the accepted 170 plus exactly those nine, which the builder re-checks and refuses if it ever differs. This line wrote no byte into Source.
- Deferred domain capabilities are named per row (network, autocorrelation/hotspot, raster reprojection/zonal, error propagation, classification accuracy, report/PPTX internal service); naming them is not a claim they exist.
- Fixtures are cited read-only for structure (schema, row counts, grid fields); they establish no business semantics and no golden cartographic answer.
- Global targets stay inherited from v1 unchanged (108 standard maps, at least 98 first renders, at least 40 stress cases at 0.95 applicable completion, at least 12 hidden variants at 0.9 first-version, two reviewers with mean at least 4 and no item below 3, zero serious scientific errors).
- Any change to the M1 face or to this M2 face after acceptance requires a Commander ruling; until then both anchors are recomputable by the two freeze scripts.

## Sources

- `GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md` §§8-10.
- `GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md` §§2, 6-8, 10.
- `GIS_AUTOMATION_FINAL_BENCHMARK_AND_DECISIONS_20260927.md` §§3-5, 7-8.
- `Docs/_gatekeeper/inbox/D-089_l2-f05-m2-calibration.md` (scope 1-5), `D-088_l1-m2-p1p2-quality-tools.md` (nine tools), `GATE-G205`, `GATE-G206`, `GATE-G202` reportPath ruling.
- `run-20260928-d082/f03b-5-schemas/` frozen input surfaces of the nine tools plus `find_identical`; `run-20260928-d083/a-roster/prereg-131-table.md` classification rows.
- `.runtime/evolution/v5-f/run-20260929-d089/field-schema-draft-m2.md` (schema-first draft) and `m2-mapping-notes.md` (builder and self-check notes).
