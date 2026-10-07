# F05 M3 benchmark calibration extension — PASS CANDIDATE

This directory now carries three calibration faces. The **M1 face** (D-085, nine members, aggregate `92ECD265B1DCE88AF3C55913F3B05AC6CFF6BAFF78B29D003B5E0D3EFB52B9FD`) and the **M2 face** (D-089, thirteen members, aggregate `501A7E15600850E64FA078972F41DF4F22D1E10A2E62EBFC5063358FF13E10A7`) are frozen contract surfaces (GATE-G206 and GATE-G214) and are byte-unchanged here: this batch references them by ID and path, and copies or edits neither. The **M3 face** (D-093) is additive with its own independent anchor.

Like the earlier faces this is a **definition candidate**: it fixes criteria before results. It is not evidence that any M3 path works, that any map rendered, that any tool ran, or that any benchmark target was met.

## What the M3 face contains

| Class (D-093 scope) | Files |
|---|---|
| 1 scenarios and mapping | `m3-scenarios-5.jsonl`, `m3-scenarios-5.md`, `m3-scenario-template-map-v1.jsonl`, `m3-scenario-template-map-v1.md`, `m3-capability-unlock-v1.jsonl` |
| 2 comparison rules v3 (additive) | `comparison-rules-v3.json`, `comparison-rules-v3.md` |
| 3 stress subset extension | `stress-40-m3-subset.jsonl` |
| 4 Z12 pre-calibration | `z12-precalibration-v1.json`, `z12-precalibration-v1.md` |
| closure hub | `m3-core-hidden-split-v1.json` |
| 5 freeze extension and anchor | `../tools/benchmark/freeze-benchmark-m3.py`, `benchmark-m3-s0.json`, `benchmark-m3-s0.txt` |

## Counting closure (the declaration this face is judged against)

- Base scenarios: 6 (M1) + 19 (M2) + **5 (M3)** = 30, so the base thirty are now fully defined.
- Base templates: 6 (M1) + 14 (M2) + 0 (M3) = 20, already complete, so M3 adds tool-facing criteria and an unlock surface instead of new templates.
- Catalog total: scenarios 33 defined (6 + 22 + 5) and 27 deferred to M4 = 60; templates 22 defined (6 + 16) and 14 deferred = 36.
- Stress: 12 (M1) + 16 (M2) + 14 (M3) = 42 local rows against the FINAL minimum of 40. Local ids, not FINAL numbering; completion is only measured when the runs happen.
- Hidden: 12 + 32 + 5 = 49 local candidates, references only, no gold content or path.
- Rules: v1 8 + v2 17 + v3 22 = 47 ids with zero collision; no new error code (the set stays 33).
- Core: 5 new `CORE-M3-*` rows times 3 defined rounds = 15 planned definition-level rounds; no round was run.

## Scope of the M3 slice

- Five new scenarios: S09, S14, S17, S24, S25, mapped to TP04, TP10, TS01, TS04, TS07, all carried by ID from the M1/M2 faces.
- Families: network accessibility, terrain and hydrology, sample extraction, fixed-class time comparison, and the GIS half of the semantic-asset handover.
- The 32 candidates read as 29 independent contracts, 2 internal services (raster_reclassify, extract_raster_values) and 1 parameter extension (create_layout_map_frame). Zero of the 32 appear in the accepted 189-tool baseline; the P5 eight are dispatched to the build line as D-094 and are bound here as in flight and not accepted, while P6, P7a and P7b have no dispatched batch yet.
- GP provenance recomputed from the frozen 53-entry whitelist: present = analysis.Frequency, analysis.TabulateIntersection, management.Project, sa.CellStatistics, sa.Con, sa.ExtractValuesToPoints, sa.RasterCalculator, sa.Reclassify, sa.ZonalStatisticsAsTable; gap = Network Analyst route solve, Network Analyst service area, analysis.FeatureNeighborhood, management.BuildRasterPyramids, management.CreateTessellation, management.MosaicToNewRaster, management.ProjectRaster, management.Simplify, management.SmoothLine, management.SmoothPolygon, stats.HotSpotAnalysis, stats.SpatialAutocorrelation; the hydrology chain is fully present, so HYD-001 is the only fully nameable family today.
- Deferred and not calibrated: the three G-190 online/publish items, the composition side of S25 (owned by another line), measurement error propagation and classification accuracy (later domain batches).

## Fail-closed discipline introduced in this face

1. A whitelist gap is a precondition on the frozen 53-name list plus the host licence. Expanding that list is a user ruling, so the criterion freezes the assertion shape **and the blocked state**; it never asserts a completed result and never treats a general executor as an unlimited verified algorithm set.
2. Side-effect classes are read from the frozen candidate schemas, not assumed: file-output, in-place raster (build_raster_pyramids), in-project resource (layout family) and read-only (raster_pixel_inspect). Only the file-output class may carry an overwrite-refusal assertion.
3. Error expression and error computation are booked separately (OSV-001), so a visualisation pass cannot close a computation row.
4. Public-name arithmetic is recomputed and disclosed: 88 nominal base candidates become 84 public names after removing two internal services, the merged duplicate contract and one parameter extension.
5. Nothing here pre-admits another line's result: each candidate carries its own dispatch status (P5 = dispatched as D-094, in flight, not accepted; the rest = no dispatched batch), and counting uses the accepted 189-tool baseline (G-215) recomputed at delivery rather than any declared number.

## Evidence limits

- No runtime evidence exists: no tool call, render, export, batch, cancellation drill, reviewer score or benchmark measurement.
- Host extension availability (Spatial Analyst, Network Analyst) is NOT VERIFIED in this workspace; nothing was started.
- Fixtures are cited read-only for structure; they establish no business semantics and no golden cartographic answer.
- Global targets stay inherited unchanged (108 standard maps, at least 98 first renders, at least 40 stress rows at 0.95 applicable completion, at least 12 hidden variants at 0.9 first version, two reviewers with mean at least 4 and no item below 3, zero serious scientific errors).
- TS08 keeps its G-205 status `NOT-AVAILABLE-PENDING-USER`; no non-qualifying raster appears as input or gold anywhere in this face.
- Profile definitions are not standard maps: the invalid profile expects a refusal, and the three positive frozen inputs plus gold outputs per template still need case-level identification.
- Any change to the M1, M2 or M3 face after acceptance requires a Commander ruling; until then all three anchors are recomputable by the three freeze scripts.

## Sources

- `GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md` §§8-10.
- `GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md` §§2-3, 6-8, 10.
- `GIS_AUTOMATION_FINAL_BENCHMARK_AND_DECISIONS_20260927.md` §§1-2, 4-5, 7-8.
- `Docs/_gatekeeper/inbox/L2_INBOX.md` (board with the D-093 ticket text) and `Docs/_gatekeeper/inbox/L1_INBOX.md` (D-092 CLOSED by G-215, D-094 ACTIVE-D094 = the P5 eight tools) for the concurrent build line's scope; boards replaced the per-batch HANDOFF pair under G-214 R-3.
- `run-20260928-d082/f03a-1`, `f03a-3`, `f03a-4` (internal services, parameter extension, quantity arithmetic), `f03b-5-schemas` (32 frozen input surfaces), `f03b-8` support matrix, `f03b-9` licensing row L-17, `f03b-7` ADR (no bypass).
- `run-20260928-d083/a-roster/prereg-131-table.md` (classification rows for the 29 independent contracts).
- `GATE-G205`, `GATE-G206`, `GATE-G207`, `GATE-G213`, `GATE-G214` rulings; `DECISIONS_LOG.md` G-190 (deferred trio).
- `.runtime/evolution/v5-f/run-20260929-d093/field-schema-draft-m3.md` (schema first), `m3-mapping-notes.md`, `evidence-index.md`.
