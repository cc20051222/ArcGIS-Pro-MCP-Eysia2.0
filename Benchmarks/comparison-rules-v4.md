# F05 comparison rules v4 — M4 additions (PASS CANDIDATE)

`comparison-rules-v4.json` is the machine source. v4 inherits v1 (8 ids), v2
(17) and v3 (22) by id; those three files are byte-unchanged and this
batch adds 26 new ids with zero collision and zero new error code.

One schema note for verifiers: `toolBinding` is a **list** in v4, because a breadth rule commonly
binds several candidates of one domain, while v1-v3 used a single string.

| id | category | bindings | severity | choice flag | blocked precondition |
|---|---|---|---|---|---|
| `KDA-001` | domain-known-answer | all 48 X candidates | critical | - | - |
| `PNA-001` | public-name-arithmetic | build_offline_map_package, compose_evidence_report, compose_presentation_deck | critical | - | - |
| `BRG-001` | python-channel-gate | forecast_spatiotemporal_series, infer_approved_raster_model, detect_raster_trends, propagate_spa | critical | - | yes |
| `MRC-001` | model-artefact-gate | infer_approved_raster_model, classify_land_cover | critical | d097CalibrationChoice | - |
| `STM-001` | space-time-trends | build_space_time_cube, compare_temporal_trajectories | critical | - | - |
| `STM-002` | space-time-trends | detect_temporal_change_points | major | d097CalibrationChoice | - |
| `STM-003` | space-time-trends | forecast_spatiotemporal_series | critical | d097CalibrationChoice | - |
| `STM-004` | space-time-trends | analyze_emerging_hotspots | critical | - | yes |
| `STM-005` | space-time-trends | export_time_animation | major | - | - |
| `SCN-001` | three-dimensional | configure_scene_environment, configure_layer_elevation, extrude_scene_features | major | - | yes |
| `SCN-002` | three-dimensional | analyze_viewshed, analyze_line_of_sight | critical | - | yes |
| `SCN-003` | three-dimensional | create_elevation_profile, calculate_cut_fill | critical | d097CalibrationChoice | - |
| `SCN-004` | three-dimensional | export_scene_package | major | - | - |
| `PCL-001` | point-cloud-terrain | inspect_point_cloud, filter_point_cloud | critical | - | - |
| `PCL-002` | point-cloud-terrain | classify_ground_points | critical | - | yes |
| `PCL-003` | point-cloud-terrain | derive_terrain_surface, derive_canopy_height, compare_point_cloud_epochs | critical | - | - |
| `RMO-001` | remote-sensing-models | assess_remote_sensing_quality | major | - | - |
| `RMO-002` | remote-sensing-models | mask_cloud_and_shadow, segment_raster_objects | critical | - | - |
| `RMO-003` | remote-sensing-models | classify_land_cover, assess_classification_accuracy | critical | d097CalibrationChoice | - |
| `RMO-004` | remote-sensing-models | build_raster_time_series, detect_raster_trends | critical | d097CalibrationChoice | - |
| `SDU-001` | science-decision-uncertainty | design_spatial_sample | major | - | - |
| `SDU-002` | science-decision-uncertainty | interpolate_surface, cross_validate_spatial_model | critical | - | yes |
| `SDU-003` | science-decision-uncertainty | analyze_scenario_sensitivity, propagate_spatial_uncertainty, compare_multi_criteria_scenarios, e | critical | - | - |
| `NFD-001` | network-facility | build_od_cost_matrix, find_closest_facilities, solve_location_allocation, optimize_vehicle_route | critical | - | yes |
| `LIX-001` | local-interchange | validate_interchange_conformance, transform_vertical_coordinates, build_offline_map_package, exp | critical | - | yes |
| `SRP-001` | same-source-report | compose_evidence_report, compose_presentation_deck | critical | - | yes |

## Rule bodies

### `KDA-001` — G-DOMAIN known answer, negative example, complete automatic result, updated instance
- bindings: all 48 X candidates
- channel: cross-cutting · side effect: n/a
- implementation status: ACCOUNTING-GATE-NOT-A-RUN-RESULT
- predicate: A breadth-domain row may only be scored complete when the case manifest supplies all four G-DOMAIN artefacts for that domain: a known answer produced by an independent method, at least one negative example, one fully automatic result with no manual step, and one re-run on updated input that keeps the design intent. A row that supplies fewer than four is booked as incomplete for that class and stays in the denominator.
- recompute: Count the four manifest fields per domain; assert they are non-empty and that the known answer came from a named independent path rather than the candidate tool itself; assert the updated instance deltas are the declared ones.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row G-DOMAIN

### `PNA-001` — M4 public-name dedup recomputed, never asserted from a nominal 48
- bindings: ['build_offline_map_package', 'compose_evidence_report', 'compose_presentation_deck']
- channel: cross-cutting · side effect: n/a
- implementation status: ACCOUNTING-GATE-NOT-A-RUN-RESULT
- predicate: Public-name arithmetic must be recomputed for every M4 count: 48 nominal X candidates minus 1 merged parameter branch (build_offline_map_package into export_data_package as a packageType mode) minus 2 internal services whose public names may be registered once at M4 (compose_evidence_report, compose_presentation_deck) equals 45 independent M4-batch public names. A run may not claim 48 new tools, and an internal service may not be booked as a new algorithm because it has a public name.
- recompute: Recompute the three sets from prereg-131 contract kinds and the frozen ledger, assert 48-1-2=45, and assert no merged or internal-service name appears in a tool-count claim.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:146 X45 merge wording; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:151 M2 row public-name-at-M4 wording; .runtime/evolution/v5-f/run-20260928-d082/f03a-2-new48-terminal-review.md §5

### `BRG-001` — Python-typed candidates are reviewed typed operations only, never an arbitrary-Python or extra-6511 path
- bindings: ['forecast_spatiotemporal_series', 'infer_approved_raster_model', 'detect_raster_trends', 'propagate_spatial_uncertainty', 'export_spatiotemporal_cube']
- channel: Python · side effect: n/a
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: A Python-channel candidate is eligible only when the executed call is one of the reviewed typed operations on the existing bridge, on the existing port, with the request, input hash, seed and library versions echoed. Any request that would add an interpreter, widen the bridge surface, open another port, or submit free-form source fails the criterion before output, and that refusal is the correct answer.
- recompute: Assert the echoed operation is in the reviewed typed-operation set; assert port and process identity unchanged from the accepted configuration; assert free-form source submission is refused with a named reason and no artifact written.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: typed-operation review ownership is an open item for the commander; until it is ruled the row stays blocked
- sourceRefs: AGENTS.md §3 security baseline (Python Bridge 6511, no new arbitrary Python, no bypass); GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §4:90 typed-operation wording; frozen candidate schema x-d082.pythonBridge recomputed for the 48

### `MRC-001` — A registered model is a gated artefact: weights, licence, bands, normalisation, applicability domain, accelerator
- bindings: ['infer_approved_raster_model', 'classify_land_cover']
- channel: Python / Native · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Inference output may only be scored when the model record names the weight artefact with a recomputed hash, the licence and its redistribution status, the expected band order and normalisation, the training applicability domain against the input, and the accelerator probe result. A placeholder, a randomly initialised net, or an undocumented weight file is not a capability and must be refused; the no-accelerator path must be executed or explicitly declared unsupported, never silently skipped.
- recompute: Recompute the weight hash against the register; assert band count/order and normalisation constants equal the register; assert the applicability-domain check ran and its verdict is in the report; assert the CPU path result is recorded separately from the accelerator path.
- severity: critical
- d097CalibrationChoice: True — FINAL requires the gates but publishes no weight-manifest field list; the six fields named here are the smallest set recomputable from the catalog row text, registered as a D-097 choice for review.
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:112 X26 row; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §4:82 X4 verification row; f03a-2-new48-terminal-review.md §4.2 N2

### `STM-001` — Binning conserves counts and the calendar is declared
- bindings: ['build_space_time_cube', 'compare_temporal_trajectories']
- channel: Geoprocessing / Native · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Every bin count, per-unit total and grand total must equal the source rows after the declared time field, interval, alignment, time zone and missing-value policy; alignment drift, daylight-shift and unequal bin length must be reported rather than absorbed. Trajectory comparison must show the alignment, standardisation and missing policy before any difference or cluster claim.
- recompute: Recompute bin membership from the declared calendar; assert sum of bins equals input rows minus the named exclusions; assert each excluded row is listed with its reason; assert the trajectory table carries the alignment method next to the difference.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:72 X01 row; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:76 X05 row; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §4:79 X1 verification row

### `STM-002` — Change points are verified against a synthetic known answer with fixed parameters
- bindings: ['detect_temporal_change_points']
- channel: Native · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: The method, its parameters and the minimum segment length are frozen before the run; detected change points are compared against a synthetic series whose true change points and effect sizes are known, reporting count, position error and effect-size error. Detecting fewer points is only acceptable when the manifest says the answer is zero.
- recompute: Recompute detection on the synthetic input with the echoed parameters; assert position error equals the published tolerance, assert every missed and spurious point is listed.
- severity: major
- d097CalibrationChoice: True — FINAL names the known-answer requirement but no tolerance; D-097 freezes position error at one sample interval and effect-size relative error at 0.10 as the candidate tolerance for commander review.
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:73 X02 row; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §4:79 X1 verification row

### `STM-003` — Forecast intervals come from a time-held-out backtest with no future leakage
- bindings: ['forecast_spatiotemporal_series']
- channel: Python · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Training rows must all precede the forecast origin, the origin and horizon must be echoed, and the interval method with its assumptions must be named. Interval coverage is measured on the held-out window, not on fitted values, and a point forecast without a defensible interval is reported as a point forecast only.
- recompute: Recompute the split so that max(train index) < min(test index); assert coverage of the named nominal interval on the held-out window; assert the assumptions text is present in the artifact.
- severity: critical
- d097CalibrationChoice: True — FINAL requires backtest and leakage checks but publishes no coverage target; D-097 freezes the nominal interval at 0.90 with coverage accepted in 0.85-0.95 as a candidate choice for review.
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:74 X03 row; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §4:79 X1 verification row

### `STM-004` — Emerging hotspots are not static hotspots and the multiple-testing policy is disclosed
- bindings: ['analyze_emerging_hotspots']
- channel: Geoprocessing · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: The output must distinguish the time-series classification (new, persistent, intensifying, diminishing, sporadic) from a single-epoch hotspot map, and must echo the spatial weights construction, the significance level and the correction for multiple comparison. Where the algorithm has no admitted path, the run returns a blocked result naming the missing precondition.
- recompute: Assert class labels come from the temporal statistic, not the epoch statistic; recompute the weight matrix declaration; assert the correction method and adjusted threshold are echoed; assert a blocked result writes no map.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: spatio-temporal pattern-mining algorithm has no entry in the frozen 53-name whitelist and the extension licence is unprobed; expansion is a user ruling
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:75 X04 row; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §4:90 licence/whitelist note

### `STM-005` — Frames are deterministic, the visual rules are frozen and encoding is verified
- bindings: ['export_time_animation']
- channel: Native · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Class breaks, colour tokens, camera extent, symbol size and timestamp text must be identical across all frames except the declared time dimension; the frame manifest must equal the rendered frame count, and any dropped or duplicated frame must fail the row. An encoded video may only be claimed after a read-back of frame count and duration, otherwise the deliverable is the frame list alone.
- recompute: Recompute the frame count from the time bins; diff colour/class tokens between sampled frames; assert the read-back duration equals frames times frame duration within the declared tolerance.
- severity: major
- d097CalibrationChoice: False
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:77 X06 row; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §5:172 animation row

### `SCN-001` — Scene and layer configuration is a host-state effect, asserted as state not as file overwrite
- bindings: ['configure_scene_environment', 'configure_layer_elevation', 'extrude_scene_features']
- channel: Native · side effect: in-project-resource
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: These candidates change project or layer state and have no output path, so the criterion asserts the declared state after the call (ground, lighting, coordinate mode, elevation surface, offset and extrusion field binding) plus the Z unit and vertical datum echo, and asserts that no user file was created or touched. An overwrite-refusal assertion is not applicable and must not be scored here. Where a merge with an existing accepted interface is still unresolved, the row is blocked on that adjudication.
- recompute: Read the state back through the accepted inspection tools and diff against the request; assert file fingerprints of the project and inputs unchanged; assert the field binding survives a reload.
- severity: major
- d097CalibrationChoice: False
- blocked precondition: equivalence with the accepted set_map_properties parameter surface is an open F03-a review point; until ruled, no new public name may be claimed
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:83-85 X07-X09 rows; f03a-2-new48-terminal-review.md §4.4 unresolved point 1

### `SCN-002` — Occlusion results are bound to a known-answer terrain with an explicit vertical datum
- bindings: ['analyze_viewshed', 'analyze_line_of_sight']
- channel: Geoprocessing · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Visibility output is eligible only when the elevation surface, observer and target heights, the height reference (ground or above ground) and the vertical datum are all declared, and the result is compared against a constructed surface whose visible and occluded cells are known. A flat attractive scene does not count as numeric 3D proof. Missing licence or algorithm admission returns a blocked result naming the precondition.
- recompute: Recompute visible-cell counts against the manifest answer; assert the occlusion boundary cells are listed; assert the vertical datum string equals the manifest; assert no partial result artifact is written on a blocked run.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: 3D Analyst licence and the analysis algorithm path are unprobed and unnamed in the frozen 53; both are preconditions, not this line's to grant
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:86-87 X10/X11 rows; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §4:80 X2 verification row

### `SCN-003` — Profiles and volumes are numeric answers with NoData and datum handling declared
- bindings: ['create_elevation_profile', 'calculate_cut_fill']
- channel: Geoprocessing / Native · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Profile vertices must equal the declared sampling interval along the path with breaks reported where the surface has no value; cut and fill volumes must recompute from cell area times height difference, with the before and after surfaces on a common grid, and the balance, NoData mask and vertical datum stated. A volume that cannot be recomputed from the published cells fails the row.
- recompute: Recompute volume as sum(cell area x delta z) over the named mask and compare with the reported figure; assert sampling interval, vertex count and break list; assert datum strings of both surfaces match.
- severity: critical
- d097CalibrationChoice: True — FINAL requires cell-area recomputation but publishes no rounding rule; D-097 freezes agreement at relative error 0.01 as a candidate choice for review.
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:88-89 X12/X13 rows; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:264 index row

### `SCN-004` — A scene package is judged by reopening it away from the source
- bindings: ['export_scene_package']
- channel: Native · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: The package is eligible only when its dependency closure is listed, every resource is inside the package, and an independent reopen on the same host succeeds with no path to the original working directory. Format and resource-boundary support must be probed, not declared from the format name.
- recompute: Assert dependency list equals the referenced item set; assert no absolute source path is resolvable from the reopened package; assert the reopened scene's layer and elevation state matches the manifest.
- severity: major
- d097CalibrationChoice: False
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:90 X14 row; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:265 index row

### `PCL-001` — Point-cloud formats are declared separately and filtering conserves what it must
- bindings: ['inspect_point_cloud', 'filter_point_cloud']
- channel: Native / Geoprocessing · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: LAS, LAZ and COPC support must be answered per format from a real read, never inferred from another format. A filtered copy must report input and output point counts, kept classes, returned-count distribution, bounding box and field list, and must not alter the source file. Any claim of maximum supported size must be the档 actually tested.
- recompute: Recompute point counts and class histograms from the read-back; assert source file hash unchanged; assert the format verdict row exists for each of LAS/LAZ/COPC with a named evidence path.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:96-97 X15/X16 rows; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §5:168 format row; f03b-8-support-matrix-freeze.md (no LAS/LAZ/COPC literals in Source)

### `PCL-002` — Ground classification is judged against a reference with slope and building error quantified
- bindings: ['classify_ground_points']
- channel: Geoprocessing · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Classification may only be scored against a reference point set, and must report commission and omission on steep slope and on building edges separately, plus the algorithm name, its parameters and the classification coding version. The operation must run on an owned copy, never in place on a user original.
- recompute: Recompute the two error classes from the confusion counts against the reference; assert the copy hash differs from the original path and the original hash is unchanged; assert parameter echo.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: the ground-classification algorithm path is unprobed and unnamed in the frozen 53; expansion is a user ruling
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:98 X17 row; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §4:90 licence/whitelist note

### `PCL-003` — Derived surfaces share one grid and epoch differences are qualified before they are believed
- bindings: ['derive_terrain_surface', 'derive_canopy_height', 'compare_point_cloud_epochs']
- channel: Geoprocessing · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: DTM, DSM and canopy-height products must declare cell size, extent, alignment and fill policy, and must report voids rather than interpolate them silently. Canopy height must state how negative differences and building pixels are treated. Two-epoch difference is eligible only after a registration and density-difference pre-check; without it the difference map is booked as unqualified, because a misregistration is not a real change.
- recompute: Assert the three grids are identical in extent and cell size; recompute void counts; assert negative difference cells are counted and explained; assert the registration pre-check result is in the report before any change statistic.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:99-101 X18-X20 rows; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:267 index row

### `RMO-001` — Unknown quality codes are refused, never guessed
- bindings: ['assess_remote_sensing_quality']
- channel: Native · side effect: read-only
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: A quality summary is eligible only when the product and sensor adapter are named and each quality bit, dimension and missing value is interpreted from that adapter. An unrecognised QA encoding, an undeclared unit, or a coverage figure the read cannot support must be returned as a named gap. If the same candidate offers to write the report to disk, that action is checked as a write.
- recompute: Assert every reported code resolves to the named adapter table; assert unknown codes appear in the gap list with no interpretation; assert the optional disk write honours the output-path and overwrite rules.
- severity: major
- d097CalibrationChoice: False
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:107 X21 row; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:66 R-becomes-W rule

### `RMO-002` — Masks and objects are judged against references with stable identifiers and a resource ceiling
- bindings: ['mask_cloud_and_shadow', 'segment_raster_objects']
- channel: Geoprocessing · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Cloud and shadow masking must be compared with a reference mask, must state which quality bits or model produced it, and must write a copy rather than alter the source. Object segmentation must produce identifiers that are stable across two runs of the same parameters, must report edge behaviour and scale, and must stop at the declared resource ceiling with an explanatory refusal instead of a truncated result.
- recompute: Recompute the confusion areas against the reference mask; assert source hash unchanged; run segmentation twice and diff the identifier sets; assert the ceiling refusal path leaves no partial object raster.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:108-109 X22/X23 rows; f03a-2-new48-terminal-review.md §4.2 N2

### `RMO-003` — Training and validation are spatially separated and the accuracy book is independent
- bindings: ['classify_land_cover', 'assess_classification_accuracy']
- channel: Geoprocessing / Native · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: A classification is eligible only with a fixed class mapping, a declared training-sample protocol, and spatial separation between training and validation; the class legend must equal the manifest class set. Accuracy must be computed from an independent reference with a stated sampling design, reporting the confusion matrix, per-class user and producer accuracy, and the imbalance and sample-size limits. Accuracy computed from the training pixels themselves fails the row.
- recompute: Assert class code set equals the manifest; assert a minimum distance between training and validation geometry; recompute overall accuracy from the confusion matrix; assert the reference source is not the training source.
- severity: critical
- d097CalibrationChoice: True — FINAL requires spatial isolation but publishes no distance; D-097 freezes the candidate floor at 10 training-sample buffer units between the two sets, for commander review.
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:110-111 X24/X25 rows; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:269 index row

### `RMO-004` — Multi-date structure and trend tests declare their grid, sensor and statistical conditions
- bindings: ['build_raster_time_series', 'detect_raster_trends']
- channel: Geoprocessing / Python · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: A time series must place all dates on one declared grid with a named no-data and sensor-consistency policy, and must state that this is a raster stack, not a per-unit aggregation. Trend output must report slope, significance and the effective sample count per cell, and must state the treatment of autocorrelation, seasonality, missing dates and the multiple-comparison policy; a trend on too few effective samples is refused, not smoothed into significance.
- recompute: Assert grid identity across dates; recompute effective sample counts; assert the correction method and adjusted threshold are echoed; assert cells below the declared minimum are reported as no-result.
- severity: critical
- d097CalibrationChoice: True — FINAL requires an effective-sample minimum but gives none; D-097 freezes the candidate at 8 usable observations per cell, for commander review.
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:113-114 X27/X28 rows; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:270 index row

### `SDU-001` — A sample design is reproducible from its seed and its rules are published
- bindings: ['design_spatial_sample']
- channel: Native · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: The design must echo extent, strata, constraints, minimum distance, seed and the inclusion rule, and a re-run with the same seed must produce the identical point set. Boundary handling must be stated where a candidate point falls on a stratum edge.
- recompute: Re-run with the same seed and diff coordinates; assert count per stratum equals the declared allocation; assert the boundary rule text is present.
- severity: major
- d097CalibrationChoice: False
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:120 X29 row; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:271 index row

### `SDU-002` — An error surface exists only where the method supports it, and folds are split spatially
- bindings: ['interpolate_surface', 'cross_validate_spatial_model']
- channel: Geoprocessing / Native · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Prediction is eligible only with the method and parameters echoed; a prediction interval or error surface may be produced only by a method whose assumptions support it, otherwise the row records that the method does not supply one. Cross-validation must split by spatial blocks with a stated buffer, must report the folds, the training extent per fold, and residuals, and must fail where a validation point lies inside its own training neighbourhood.
- recompute: Recompute fold membership from the block geometry; assert no validation point within the buffer of its training set; recompute the score from the published residuals; assert the error-surface field is absent or justified.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: whether interpolation is a new contract or a parameter branch of the controlled GP path is an open F03-a point (independence depends on the 53-entry content, unverified here)
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:121-122 X30/X31 rows; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §4:83 X5 verification row

### `SDU-003` — Sensitivity, uncertainty, weights and assumptions are four separate books
- bindings: ['analyze_scenario_sensitivity', 'propagate_spatial_uncertainty', 'compare_multi_criteria_scenarios', 'evaluate_scenario_ensemble', 'validate_statistical_assumptions']
- channel: Native / Python · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Perturbation must never be reported as a probability; uncertainty propagation requires an error model with a stated basis, and one without a basis must be refused. Multi-criteria weights must come from the brief, not from the assistant, and constraints must be evaluated independently of the score. An ensemble without probabilistic weights is reported as a scenario distribution. A statistical assumption check may only answer from its declared test catalogue; an unknown condition is reported as unknown.
- recompute: Assert the wording class in the output (perturbation vs probability, distribution vs credible interval); assert each weight traces to a brief field; assert constraint failures are excluded from the ranking and still listed; assert the catalogue name for every assumption verdict.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:123-127 X32-X36 rows; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §4:83 X5 verification row; f03a-2-new48-terminal-review.md §4.2 N6

### `NFD-001` — Network results keep their failures visible, and a cost surface is not road travel
- bindings: ['build_od_cost_matrix', 'find_closest_facilities', 'solve_location_allocation', 'optimize_vehicle_routes', 'analyze_network_disruption', 'calculate_cost_distance']
- channel: Geoprocessing · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Every network result must echo the network source, impedance attribute, units, direction and time window, and must list unlocated inputs, disconnected components, unserved demand, undistributed orders and the solver termination reason. A finite cost may not be manufactured across a disconnect. Cumulative cost on a cost surface must be labelled as cost distance and kept separate from road travel time. Where no solver path is admitted, the run is blocked with the precondition named, and that is the correct answer.
- recompute: Assert located count plus unlocated count equals input count; assert unserved and undistributed lists are complete; recompute one leg cost from the echoed impedance; assert the termination record; assert the cost-surface output never carries a travel-time label.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: Network Analyst solvers have no entry in the frozen 53-name whitelist (recomputed: 0 of 53 names a network solver) and the extension licence is unprobed; expansion and licence grant are user rulings
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:133-138 X37-X42 rows; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §4:84 X6 verification row; Config/gp-whitelist.json recomputed 53 entries

### `LIX-001` — Conformance, vertical transformation, offline reopen and cube round-trip are four distinct proofs
- bindings: ['validate_interchange_conformance', 'transform_vertical_coordinates', 'build_offline_map_package', 'export_spatiotemporal_cube']
- channel: Geoprocessing / Native / Python · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: A conformance report must name the standard version and the adapted format range, and must check schema and spatial semantics, not only package hashes. A vertical transformation must use a real grid: without one the run blocks, and writing a vertical CRS name into metadata is never accepted as a transformation. An offline package must reopen with the external path unavailable. A spatiotemporal cube must state dimensions, units, missing-value encoding and must round-trip to itself.
- recompute: Assert the version and format scope fields are non-empty and match the tested set; assert grid presence is a precondition of any success path; reopen the package with the source renamed; re-read the cube and diff dimension, unit and missing encodings.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: NetCDF, Zarr and modern-container adapters are not present in the accepted source; support may not be declared before evidence
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:144-147 X43-X46 rows; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §5:168-172 format rows; f03b-8-support-matrix-freeze.md (no NetCDF/Zarr/GeoPackage literals in Source)

### `SRP-001` — same-source report and deck with an editable-content proof
- bindings: ['compose_evidence_report', 'compose_presentation_deck']
- channel: Native (internal service until its one-time public registration) · side effect: file-output
- implementation status: PENDING-M4-BATCH-NOT-DISPATCHED-NOT-VERIFIED
- predicate: Report and deck rows are eligible only when every number, caption and figure in them resolves to the same fact table as the map products, references are present and correct, and the file opens on its own: text selectable, embedded fonts resolved or substituted with a stated reason, page count inside the declared budget and no overflow. A screenshot or a flattened image is never evidence of editability, and the deck path depends on a reviewed Open XML generator whose licence and dependency status must be recorded before any claim.
- recompute: Diff every reported figure against the fact table cell; assert the reference list resolves; reopen the artifact and probe text-layer presence, page count and overflow; assert the dependency and licence record is present for the deck channel.
- severity: critical
- d097CalibrationChoice: False
- blocked precondition: third-party Open XML generator is not approved or installed; until a reviewed dependency exists the deck row stays blocked with that named
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §4:153-154 X47/X48 rows; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:151 M2 row internal-service wording; f03a-2-new48-terminal-review.md §4.3 N4

## Inherited targets (unchanged, by reference)

108 standard maps, at least 98 first renders, at least 40 stress rows at 0.95 applicable
completion, at least 12 hidden variants at 0.9 first version, two reviewers with mean at least 4
and no item below 3, and zero serious scientific errors. Defined here means defined; none of it
has been run.

