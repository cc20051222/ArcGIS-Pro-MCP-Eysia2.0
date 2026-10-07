# F05 M4 templates — 14 breadth rows (PASS CANDIDATE, definition only)

Machine source: `m4-templates-14.jsonl`. Values marked inherited come from the frozen rule files by
reference; those files are byte-unchanged.

| id | template | domain | primary scenarios | secondary scenarios | hidden ids |
|---|---|---|---|---|---|
| `TP11` | 道路中断与替代通道 / disruption and alternative corridor | planning | S58 | none | H-M4-TP11-01, H-M4-TP11-02 |
| `TP12` | 选址收益比较 / site selection benefit comparison | planning | S56 | none | H-M4-TP12-01, H-M4-TP12-02 |
| `TP13` | 配送空间图与时序表 / delivery routes with a time sequence table | planning | S57 | none | H-M4-TP13-01, H-M4-TP13-02 |
| `TP14` | 立体高度与地面关系 / vertical height against the ground surface | planning | S36, S38, S59 | none | H-M4-TP14-01, H-M4-TP14-02 |
| `TP15` | 视域覆盖决策 / viewshed coverage for a siting decision | planning | S37 | none | H-M4-TP15-01, H-M4-TP15-02 |
| `TP16` | 土方平衡 / cut and fill balance | planning | S39 | none | H-M4-TP16-01, H-M4-TP16-02 |
| `TP18` | 多准则决策与敏感性 / multi-criteria decision and sensitivity | planning | S53 | none | H-M4-TP18-01, H-M4-TP18-02 |
| `TS11` | 预测区间与空间分布 / forecast intervals in space | scientific | S33 | none | H-M4-TS11-01, H-M4-TS11-02 |
| `TS12` | 时空剖面 / space-time profile | scientific | S32 | S31, S49 | H-M4-TS12-01, H-M4-TS12-02 |
| `TS13` | 点云剖面与密度 / point cloud profile and density | scientific | S40 | S36 | H-M4-TS13-01, H-M4-TS13-02 |
| `TS14` | 冠层高度与参考误差 / canopy height with reference error | scientific | S42 | none | H-M4-TS14-01, H-M4-TS14-02 |
| `TS16` | 插值残差与不确定性 / interpolation residuals and uncertainty | scientific | S51 | none | H-M4-TS16-01, H-M4-TS16-02 |
| `TS17` | 情景集合分布 / scenario ensemble distribution | scientific | S54 | none | H-M4-TS17-01, H-M4-TS17-02 |
| `TS18` | 多尺度采样设计 / multi-scale sampling design | scientific | S50 | none | H-M4-TS18-01, H-M4-TS18-02 |

## Slot and demand detail

### `TP11` 道路中断与替代通道 / disruption and alternative corridor
- selectionBasis: catalog §7 breadth planning block row TP11; catalog §10 row 276 binds X41 to S58 with TP11, so the template is defined with the disruption scenario it serves.
- semantic slots: title, studyArea, networkBase, disruptedLinks, alternativeCorridors, reachabilityLegend, beforeAfterTable, units, source, mapFrame, northArrow, scaleBar, revision
- design demands: batch-page: one page per disruption scenario with identical classification; same-source: reachability figures bind to the fact table
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `ALN-001`, `GBC-001`, `KDA-001`, `NFD-001`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TP11; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:276; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TP12` 选址收益比较 / site selection benefit comparison
- selectionBasis: catalog §7 row TP12; catalog §10 row 276 binds X39 to S56 选址 with TP12, and the same row says S56 also covers TP17, which is honoured here as a secondary reference into the frozen M2 face.
- semantic slots: title, candidateSet, chosenFacilities, allocatedDemand, unservedDemand, objectiveTable, capacityBars, units, source, mapFrame, legend, revision
- design demands: batch-page: one page per alternative with the same class breaks; same-source: every figure in the map comes from the allocation table
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `BAT-001`, `GBC-001`, `KDA-001`, `NFD-001`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TP12; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:276; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TP13` 配送空间图与时序表 / delivery routes with a time sequence table
- selectionBasis: catalog §7 row TP13; catalog §10 row 276 binds X40 to S57 车队 with TP13 as its artifact.
- semantic slots: title, depot, orders, vehicleRoutes, timeWindowTable, undistributedOrders, loadBars, units, source, mapFrame, legend, revision
- design demands: batch-page: per-vehicle pages keep order identity; same-source: the sequence table and the route geometry bind to one run
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `KDA-001`, `NFD-001`, `NUM-001`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TP13; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:276; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TP14` 立体高度与地面关系 / vertical height against the ground surface
- selectionBasis: catalog §7 row TP14; catalog §10 rows 263-265 bind X07/X08/X09 to S36 and X11 to S38 with TP14, and row 265 binds the S59 3D handover sub-case to a TP14 preview, so three scenarios fan onto this template.
- semantic slots: title, sceneView, groundSurface, elevationPolicy, extrudedFeatures, heightLegend, verticalDatum, zUnits, profileLink, source, scaleBar, revision
- design demands: state-echo: scene and layer configuration is asserted as host state, never as a written file; same-source: heights bind to the named field and datum
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `KDA-001`, `LIX-001`, `OSV-001`, `SCN-001`, `SCN-002`, `SCN-003`, `SCN-004`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TP14; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:263; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TP15` 视域覆盖决策 / viewshed coverage for a siting decision
- selectionBasis: catalog §7 row TP15; catalog §10 row 264 binds X10 to the S37 视域 sub-case with TP15.
- semantic slots: title, surface, observers, observerHeights, visibilityClasses, candidatePoints, verticalDatum, units, source, mapFrame, legend, revision
- design demands: known-answer: visible and occluded counts bind to the manifest answer; licence-echo: the analysis precondition is printed on the page
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `GEO-001`, `KDA-001`, `SCN-002`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TP15; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:264; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TP16` 土方平衡 / cut and fill balance
- selectionBasis: catalog §7 row TP16; catalog §10 row 264 binds X13 to the S39 土方 sub-case with TP16.
- semantic slots: title, boundary, beforeSurface, afterSurface, cutFillRaster, balanceTable, cellArea, noDataMask, verticalDatum, units, source, revision
- design demands: recompute: the printed balance equals sum(cell area x delta z) over the published mask; same-source: the table and the raster come from one run
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `KDA-001`, `NUM-001`, `SCN-003`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TP16; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:264; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TP18` 多准则决策与敏感性 / multi-criteria decision and sensitivity
- selectionBasis: catalog §7 row TP18; catalog §10 rows 272-273 bind X32 to S53 and X34 to the S53 multi-criteria sub-case, both with TP18 as the artifact.
- semantic slots: title, criteriaTable, directions, normalisation, weightsWithProvenance, constraints, rankingStability, sensitivityPlot, source, legend, revision
- design demands: provenance: every weight cites the brief field that supplied it; separation: constraint failure is never folded into the score
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `KDA-001`, `OSV-001`, `SDU-003`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TP18; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:272; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TS11` 预测区间与空间分布 / forecast intervals in space
- selectionBasis: catalog §7 row TS11; catalog §10 row 260 binds X03 to the S33 预测 sub-case with TS11.
- semantic slots: title, forecastMap, intervalBands, origin, horizon, backtestPanel, coverageNote, assumptions, units, source, legend, revision
- design demands: held-out: coverage is measured out of sample, never on fitted values; disclosure: interval method and assumptions are printed
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `KDA-001`, `OSV-001`, `STM-003`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TS11; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:260; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TS12` 时空剖面 / space-time profile
- selectionBasis: catalog §7 row TS12; catalog §10 row 260 binds X02 to the S32 变化点 sub-case with TS12 among the artifacts, and rows 261 and 270 attach TS12 to the S31 and S49 sub-cases, which M4 records as secondary references.
- semantic slots: title, profileAxis, timeAxis, seriesLines, changePointMarks, knownAnswerNote, samplingInterval, units, source, legend, revision
- design demands: known-answer: detected change points are plotted against the published truth; same-source: the profile and the table bind to one computation
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `KDA-001`, `NUM-001`, `STM-001`, `STM-002`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TS12; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:260; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TS13` 点云剖面与密度 / point cloud profile and density
- selectionBasis: catalog §7 row TS13; catalog §10 row 266 binds X15/X16 to the S40 read and filter sub-cases with TS13 plus a copy manifest, and row 264 attaches TS13 to the S36 profile sub-case as a secondary reference.
- semantic slots: title, pointProfile, densityMap, classHistogram, returnCountChart, formatVerdictTable, pointCounts, crsAndZ, copyManifestRef, units, source, revision
- design demands: per-format: LAS, LAZ and COPC each get their own verdict row; conservation: filtered counts reconcile with the source counts
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `KDA-001`, `PCL-001`, `QLT-001`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TS13; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:266; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TS14` 冠层高度与参考误差 / canopy height with reference error
- selectionBasis: catalog §7 row TS14; catalog §10 row 267 binds X19 to S42 冠层 with TS14.
- semantic slots: title, canopyHeightMap, referenceScatter, errorBand, negativeDifferenceNote, buildingMaskTreatment, gridDeclaration, verticalDatum, units, source, legend, revision
- design demands: grid-identity: DTM and DSM share one declared grid before any difference; error-disclosure: reference error is published with the height
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `KDA-001`, `OSV-001`, `PCL-003`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TS14; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:267; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TS16` 插值残差与不确定性 / interpolation residuals and uncertainty
- selectionBasis: catalog §7 row TS16; catalog §10 row 271 binds X30/X31 to S51 插值与空间验证 with TS16.
- semantic slots: title, predictionSurface, errorSurfaceOrAbsence, residualMap, foldMap, bufferNote, methodParameters, leakageCheck, units, source, legend, revision
- design demands: no-unsupported-error: a method without a supportable error surface publishes none; spatial-folds: block geometry and buffer are shown next to the score
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `KDA-001`, `OSV-001`, `SDU-002`, `SDU-003`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TS16; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:271; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TS17` 情景集合分布 / scenario ensemble distribution
- selectionBasis: catalog §7 row TS17; catalog §10 row 273 binds X35 to S54 集合统计 with TS17.
- semantic slots: title, ensembleMap, divergenceAreas, weightBasisOrNone, distributionPlot, scenarioRoster, labelClass, units, source, legend, revision
- design demands: wording-diff: a scenario distribution is never labelled a credible interval; roster: every member scenario is listed with its provenance
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `KDA-001`, `SDU-003`, `SRC-001`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TS17; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:273; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `TS18` 多尺度采样设计 / multi-scale sampling design
- selectionBasis: catalog §7 row TS18; catalog §10 row 271 binds X29 to S50 采样 with TS18.
- semantic slots: title, strataMap, samplePoints, allocationTable, seedAndRules, distanceCheck, boundaryRule, reproducibilityNote, units, source, legend, revision
- design demands: seeded: the same seed reproduces the same coordinates; rule-published: inclusion and boundary rules are printed, not implicit
- analysis input policy: domain-known-answer-required / NOT-AVAILABLE-PENDING-USER
- rules: `GBC-001`, `GEO-001`, `KDA-001`, `SDU-001`
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7 template row TS18; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:271; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

