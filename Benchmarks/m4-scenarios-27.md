# F05 M4 scenarios — 27 breadth rows (PASS CANDIDATE, definition only)

Emitted by `.runtime/evolution/v5-f/run-20260929-d097/build_m4_face.py`; the JSONL file is the machine
source of truth. These rows fix criteria before results: nothing here claims a tool exists, ran, or passed.

| id | scenario | template (face) | domain | capabilities | qualifying input |
|---|---|---|---|---|---|
| `S31` | 时空事件分箱与趋势 / space-time binning and trajectories | `TS09` (M2) | space-time-trends | X01, X05, X46 | NOT-AVAILABLE-PENDING-USER |
| `S32` | 时间变化点与前后比较 / temporal change points and before-after comparison | `TS12` (M4) | space-time-trends | X02 | NOT-AVAILABLE-PENDING-USER |
| `S33` | 时序预测与区间表达 / series forecasting with intervals | `TS11` (M4) | space-time-trends | X03 | NOT-AVAILABLE-PENDING-USER |
| `S34` | 新兴热点演变 / emerging hotspot evolution | `TS06` (M2) | space-time-trends | X04 | NOT-AVAILABLE-PENDING-USER |
| `S35` | 稳定分类时间动画 / stable-classification time animation | `TS09` (M2) | space-time-trends | X06 | NOT-AVAILABLE-PENDING-USER |
| `S36` | 城市立体高度与剖面 / urban height expression and elevation profile | `TP14` (M4) | three-dimensional | X07, X08, X09, X12, X44 | NOT-AVAILABLE-PENDING-USER |
| `S37` | 可视域辅助选点 / viewshed-aided siting | `TP15` (M4) | three-dimensional | X10 | NOT-AVAILABLE-PENDING-USER |
| `S38` | 通视走廊分析 / line-of-sight corridor analysis | `TP14` (M4) | three-dimensional | X11 | NOT-AVAILABLE-PENDING-USER |
| `S39` | 挖填方平衡 / cut and fill balance | `TP16` (M4) | three-dimensional | X13 | NOT-AVAILABLE-PENDING-USER |
| `S40` | 点云质量审查 / point cloud quality review | `TS13` (M4) | point-cloud-terrain | X15, X16 | NOT-AVAILABLE-PENDING-USER |
| `S41` | 地面分类与DTM / ground classification and DTM | `TS07` (M2) | point-cloud-terrain | X17, X18 | NOT-AVAILABLE-PENDING-USER |
| `S42` | 冠层高度与参考误差 / canopy height with reference error | `TS14` (M4) | point-cloud-terrain | X19 | NOT-AVAILABLE-PENDING-USER |
| `S43` | 点云跨期变化 / point cloud epoch change | `TS03` (M2) | point-cloud-terrain | X20 | NOT-AVAILABLE-PENDING-USER |
| `S44` | 影像质量与云影筛除 / imagery quality and cloud-shadow masking | `TS08` (M1) | remote-sensing-models | X21, X22 | NOT-AVAILABLE-PENDING-USER |
| `S45` | 土地覆被分类 / land cover classification | `TS15` (M2) | remote-sensing-models | X24 | NOT-AVAILABLE-PENDING-USER |
| `S47` | 影像对象分割 / raster object segmentation | `TS08` (M1) | remote-sensing-models | X23 | NOT-AVAILABLE-PENDING-USER |
| `S48` | 登记模型遥感推理 / registered model inference | `TS08` (M1) | remote-sensing-models | X26 | NOT-AVAILABLE-PENDING-USER |
| `S49` | 多年遥感趋势 / multi-year remote sensing trends | `TS09` (M2) | remote-sensing-models | X27, X28, X46 | NOT-AVAILABLE-PENDING-USER |
| `S50` | 空间采样设计 / spatial sample design | `TS18` (M4) | science-decision-uncertainty | X29 | NOT-AVAILABLE-PENDING-USER |
| `S51` | 插值与空间交叉验证 / interpolation with spatial cross-validation | `TS16` (M4) | science-decision-uncertainty | X30, X31, X36 | NOT-AVAILABLE-PENDING-USER |
| `S53` | 决策权重敏感性 / decision weight sensitivity | `TP18` (M4) | science-decision-uncertainty | X32, X34, X36 | NOT-AVAILABLE-PENDING-USER |
| `S54` | 情景集合统计 / scenario ensemble statistics | `TS17` (M4) | science-decision-uncertainty | X35 | NOT-AVAILABLE-PENDING-USER |
| `S55` | OD通勤成本 / origin-destination commuting cost | `TP04` (M2) | network-facility | X37, X38 | NOT-AVAILABLE-PENDING-USER |
| `S56` | 公共设施选址优化 / facility location allocation | `TP12` (M4) | network-facility | X39 | NOT-AVAILABLE-PENDING-USER |
| `S57` | 多车辆配送 / multi-vehicle routing | `TP13` (M4) | network-facility | X40 | NOT-AVAILABLE-PENDING-USER |
| `S58` | 路网中断韧性 / network disruption resilience | `TP11` (M4) | network-facility | X41 | NOT-AVAILABLE-PENDING-USER |
| `S59` | 离线地图交接与往返检查 / offline handover with round-trip verification | `TP14` (M4) | local-interchange | X14, X43, X45 | NOT-AVAILABLE-PENDING-USER |

## Profiles and criteria

### `S31` 时空事件分箱与趋势 / space-time binning and trajectories
- selectionBasis: catalog §6 row S31 is the first breadth scenario of the space-time family and roadmap §9 M4 assigns the X1 time-series candidates here; catalog §10 rows 260-261 bind X01/X05 to S31 with TS09/TS12, and TS09 is already a defined M2 template so the fan-in is by reference only.
- missing qualifying input: a time-enabled event or unit-series with a declared time field, interval, time zone and missing policy (no fixture in this workspace carries a date or time attribute)
- normal: Events are binned into units and periods, totals are conserved and the small-multiples series reads from the same table.
- boundary: Unequal bin lengths and one gap period are declared; the gap is shown as no-data, not interpolated.
- invalid: No time field, an undeclared time zone, or bin totals that do not equal the source rows.
- rules: `ALN-001`, `GEO-001`, `KDA-001`, `LAY-001`, `LIX-001`, `NUM-001`, `OSV-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `STM-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_field_statistics, get_layer_info, get_map_info, summarize_features; candidates build_space_time_cube, compare_temporal_trajectories, export_spatiotemporal_cube
- stress: ST-M4-01
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:182; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:229; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:260; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S32` 时间变化点与前后比较 / temporal change points and before-after comparison
- selectionBasis: catalog §6 row S32 is the change-point scenario of the breadth block; catalog §10 row 260 binds X02 to the S32 sub-case and lists TS12 among its artifacts, which M4 defines here.
- missing qualifying input: a synthetic or observed series with published true change points and effect sizes
- normal: A fixed method and parameters detect the change points and report position and effect size next to the known answer.
- boundary: Two change points inside one minimum segment are reported as one with the reason stated.
- invalid: A method with no published parameters, or a demand to report a change point the manifest says is absent.
- rules: `KDA-001`, `LAY-001`, `NUM-001`, `OSV-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `STM-002`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_field_values, get_layer_info, get_map_info, list_geographic_transformations; candidates detect_temporal_change_points
- stress: ST-M4-02
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:183; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:232; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:260; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S33` 时序预测与区间表达 / series forecasting with intervals
- selectionBasis: catalog §6 row S33 pairs forecasting with interval expression; catalog §10 row 260 binds X03 to S33 with TS11 in the artifact list, so M4 defines TS11 here rather than borrowing another template.
- missing qualifying input: a history long enough to hold out, with a declared origin, horizon and interval method
- normal: A time-held-out backtest produces a forecast and an interval whose method and assumptions are named.
- boundary: A short history where the interval method is not supported is reported as a point forecast only.
- invalid: Any split that places training rows after the forecast origin, or an interval with no stated method.
- rules: `KDA-001`, `LAY-001`, `NUM-001`, `OSV-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `STM-003`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_field_statistics, get_layer_info, get_map_info; candidates forecast_spatiotemporal_series
- stress: ST-M4-03
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:184; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:231; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:260; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S34` 新兴热点演变 / emerging hotspot evolution
- selectionBasis: catalog §6 row S34 belongs to the space-time family; catalog §10 row 260 binds X04 to the S34 emerging-hotspot sub-case with TS06 among the artifacts, and TS06 is an M2-defined template used by reference.
- missing qualifying input: a space-time cube with enough periods per unit, plus a declared spatial weights construction
- normal: Time-series classes (new, persistent, intensifying, diminishing, sporadic) are distinguished from a single-epoch hotspot map and the significance policy is echoed.
- boundary: Units with too few effective periods are shown as no-result instead of being classified.
- invalid: A request to present a static hotspot map as an emerging-hotspot result, or an undisclosed multiple-comparison setting.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OSV-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `STM-004`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_field_values, get_layer_info, get_map_info; candidates analyze_emerging_hotspots
- stress: ST-M4-04
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:185; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:226; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:260; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S35` 稳定分类时间动画 / stable-classification time animation
- selectionBasis: catalog §6 row S35 is the animation scenario of the X1 family; catalog §10 row 262 binds X06 to S35 with TS09 visual rules plus a frame table, so the template stays the carried M2 TS09.
- missing qualifying input: a multi-period classified raster stack on one declared grid with a frozen class legend
- normal: Frames share class breaks, colour tokens, camera extent and timestamp text; the frame manifest equals the rendered count.
- boundary: A period with no data appears as an explicit no-data frame rather than a silent skip.
- invalid: Class breaks or colours that change between frames, or a frame count that does not equal the manifest.
- rules: `BAT-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `RFL-001`, `SCI-001`, `STM-005`, `STY-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, configure_map_series, create_layout, create_map, export_layout_pdf, export_map_series, get_feature_count, get_layer_info, get_map_info; candidates export_time_animation
- stress: ST-M4-05
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:186; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:229; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:262; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S36` 城市立体高度与剖面 / urban height expression and elevation profile
- selectionBasis: catalog §6 row S36 covers both the 3D height expression and the profile; catalog §10 row 263 binds X07/X08/X09 to the S36 sub-cases with TP14, and row 264 binds X12's profile sub-case to the same scenario with TS13, which is why TP14 is primary and TS13 is carried as a secondary reference.
- missing qualifying input: an elevation surface with published heights at named control points, and a Z-bearing feature class (the workspace rasters carry no vertical datum and are not a known-answer surface)
- normal: Scene environment, layer elevation policy and extrusion field binding are all echoed, and a profile along a declared path reports vertices at the stated interval.
- boundary: A negative or missing height field value is reported and excluded from the profile rather than zeroed.
- invalid: An extrusion bound to a non-numeric field, a missing Z unit or vertical datum, or a demand to treat the float raster as a certified terrain.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `LIX-001`, `NUM-001`, `OSV-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `SCN-001`, `SCN-003`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, get_raster_info, set_layer_visibility; candidates configure_layer_elevation, configure_scene_environment, create_elevation_profile, extrude_scene_features, transform_vertical_coordinates
- stress: ST-M4-06
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:187; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:234; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:263; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S37` 可视域辅助选点 / viewshed-aided siting
- selectionBasis: catalog §6 row S37 is the viewshed siting scenario; catalog §10 row 264 binds X10 to the S37 视域 sub-case with TP15 as its artifact, and TP15 is a breadth template M4 defines here.
- missing qualifying input: a DEM-like surface with a published occlusion answer for a known observer set
- normal: Visibility cells and siting candidates are computed from a declared surface, observer height and reference, and match the manifest answer.
- boundary: An observer inside the surface cell or on its boundary is reported with its treatment.
- invalid: A missing vertical datum, an unlicensed analysis path, or a demand to score a pleasant 3D view as proof.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `SCN-002`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, get_raster_info, list_rasters; candidates analyze_viewshed
- stress: ST-M4-07
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:188; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:235; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:264; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S38` 通视走廊分析 / line-of-sight corridor analysis
- selectionBasis: catalog §6 row S38 is the corridor scenario; catalog §10 row 264 binds X11 to the S38 通视 sub-case, whose artifact is listed as TP14, so the scenario fans into the template M4 defines for S36.
- missing qualifying input: a terrain with published visible and blocked segments for the given observer and target heights
- normal: Each sight line is split into visible and blocked stretches with the obstruction location and the vertical error disclosed.
- boundary: A line that grazes the surface with no obstruction is reported as fully visible with the margin stated.
- invalid: Observer or target height without a height reference, or a blocked line reported as a pass.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OSV-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `SCN-002`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, get_raster_info, list_rasters; candidates analyze_line_of_sight
- stress: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:189; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:234; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:264; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S39` 挖填方平衡 / cut and fill balance
- selectionBasis: catalog §6 row S39 is the earthworks scenario; catalog §10 row 264 binds X13 to the S39 土方 sub-case with TP16 as its artifact, which M4 defines here.
- missing qualifying input: two aligned surfaces (before and after) on a common grid with a published volume answer and a stated vertical datum
- normal: Cut, fill and balance recompute from cell area times height difference over the named boundary, with the NoData mask published.
- boundary: A boundary that clips part of a cell reports the fractional treatment used.
- invalid: Grids that differ in cell size or extent, a missing datum, or a balance that cannot be recomputed from the published cells.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `SCN-003`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, cell_statistics, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, raster_calc; candidates calculate_cut_fill
- stress: ST-M4-08
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:190; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:236; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:264; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S40` 点云质量审查 / point cloud quality review
- selectionBasis: catalog §6 row S40 is the point-cloud quality scenario; catalog §10 row 266 binds X15/X16 to the S40 read-and-filter sub-cases with TS13 plus a copy manifest, so TS13 is defined here as the point-cloud profile and density template.
- missing qualifying input: a real point cloud in each claimed format (LAS, LAZ, COPC): this workspace holds no .las, .laz or .copc file and the accepted source contains no reader literal for any of them
- normal: Format, classification codes, returns, density, CRS and Z summary are read back and reported per format.
- boundary: A file whose class encoding differs from the declared version is reported as such, not normalised.
- invalid: A request to infer LAZ or COPC support from a successful LAS read, or to alter the source in place.
- rules: `KDA-001`, `LAY-001`, `NUM-001`, `OUT-001`, `PCL-001`, `PRM-001`, `PRT-001`, `QLT-001`, `SCI-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, get_raster_info, list_tables; candidates filter_point_cloud, inspect_point_cloud
- stress: ST-M4-09
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:191; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:233; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:266; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S41` 地面分类与DTM / ground classification and DTM
- selectionBasis: catalog §6 row S41 covers ground classification and DTM; catalog §10 row 267 binds X17/X18 to S41 with TS07 among the artifacts, and TS07 is the M2-defined terrain and hydrology multi-panel template.
- missing qualifying input: a classified point cloud plus a reference ground set with published slope and building-edge answers
- normal: Ground classification is scored against the reference with slope and building commission and omission reported separately, and the DTM states cell size, extent and void policy.
- boundary: A void area is published as no-data rather than interpolated, when the fill policy says so.
- invalid: Classification run in place on a user original, or a DTM whose grid differs from the declared one.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OUT-001`, `PCL-002`, `PCL-003`, `PRM-001`, `PRT-001`, `SCI-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, raster_statistics; candidates classify_ground_points, derive_terrain_surface
- stress: ST-M4-10
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:192; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:227; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:267; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S42` 冠层高度与参考误差 / canopy height with reference error
- selectionBasis: catalog §6 row S42 pairs canopy height with reference error; catalog §10 row 267 binds X19 to S42 with TS14 as its artifact, the canopy-height template M4 defines here.
- missing qualifying input: a DTM and DSM pair on a common grid with a reference canopy height and a stated vertical datum
- normal: Canopy height is computed on the shared grid with negative differences and building pixels counted and explained, and the reference error is disclosed.
- boundary: A cell where the two grids disagree is excluded with a count published.
- invalid: A height model presented as probability when no error basis exists, or grids silently resampled to fit.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OSV-001`, `OUT-001`, `PCL-003`, `PRM-001`, `PRT-001`, `SCI-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, raster_calc; candidates derive_canopy_height
- stress: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:193; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:234; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:267; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S43` 点云跨期变化 / point cloud epoch change
- selectionBasis: catalog §6 row S43 is the epoch-change scenario; catalog §10 row 267 binds X20 to the S43 跨期差 sub-case with TS03 (change magnitude and difference distribution), a template already defined in M2 and used here by reference.
- missing qualifying input: two registered epochs with a published registration error and density difference per cell
- normal: The registration and density pre-check runs first, then the difference is reported only over the valid area with its effective resolution stated.
- boundary: Cells below the density floor are published as no-result rather than as no-change.
- invalid: A difference map presented as real change without the pre-check, or epochs on different datums.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OUT-001`, `PCL-003`, `PRM-001`, `PRT-001`, `SCI-001`, `SRC-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, cell_statistics, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, raster_calc; candidates compare_point_cloud_epochs
- stress: ST-M4-11
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:194; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:223; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:267; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S44` 影像质量与云影筛除 / imagery quality and cloud-shadow masking
- selectionBasis: catalog §6 row S44 is the quality-and-mask scenario; catalog §10 row 268 binds X21/X22 to S44 with TS08 plus a quality report, so this scenario inherits the frozen TS08 input-qualification predicate and its NOT-AVAILABLE-PENDING-USER gold status rather than substituting any raster.
- missing qualifying input: a multi-band product with published QA bits and a reference cloud and shadow mask; the workspace has no such product, and per G-205 multi.tif is not a qualifying input or gold example
- normal: Quality, coverage, dimension and missing values are read from a named sensor adapter, and the mask is written as a copy compared against the reference.
- boundary: A quality code the adapter knows but that is ambiguous for the scene is published as ambiguous.
- invalid: An unrecognised QA encoding guessed into meaning, or the source raster altered in place.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `RMO-001`, `RMO-002`, `SCI-001`, `TS08-INPUT-QUAL-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, get_raster_info, raster_statistics; candidates assess_remote_sensing_quality, mask_cloud_and_shadow
- stress: ST-M4-12
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:195; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:228; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:268; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S45` 土地覆被分类 / land cover classification
- selectionBasis: catalog §6 row S45 is the supervised classification scenario; catalog §10 row 269 binds X24 to the S45 分类 sub-case with TS15 (classification map plus confusion matrix), an M2-defined template used by reference.
- missing qualifying input: multi-band imagery with a class legend, spatially separated training and validation samples
- normal: Class mapping is fixed to the legend, training and validation are separated by the declared distance, and the model metadata are written with the product.
- boundary: A class present in the legend but absent from the scene is published as empty, not fabricated.
- invalid: Training samples inside the validation buffer, or a class set that differs from the legend.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `RMO-003`, `SCI-001`, `SRC-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, apply_symbology_from_layer, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, list_color_ramps; candidates classify_land_cover
- stress: ST-M4-13
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:196; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:235; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:269; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S47` 影像对象分割 / raster object segmentation
- selectionBasis: catalog §6 row S47 is the segmentation scenario; catalog §10 row 269 binds X23 to the S47 分割 sub-case with TS08/TS15 among its artifacts, so M4 fans this scenario onto the frozen TS08 template by reference only.
- missing qualifying input: a multi-band image with a published object answer set and identifier stability expectation
- normal: Objects get stable identifiers across two runs of the same parameters, with edge behaviour and scale reported and a resource ceiling declared.
- boundary: An input above the declared ceiling is refused with the ceiling named, not silently downsampled.
- invalid: Identifiers that shift between identical runs, or a partial object raster left after a refusal.
- rules: `ALN-001`, `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `RMO-002`, `SCI-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, raster_resample; candidates segment_raster_objects
- stress: ST-M4-14
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:198; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:228; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:269; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S48` 登记模型遥感推理 / registered model inference
- selectionBasis: catalog §6 row S48 is the registered-model scenario; catalog §10 row 269 binds X26 to the S48 推理 sub-case with TS08/TS15 and a model card, and roadmap §4:82 requires that a placeholder model never count as a capability, which MRC-001 freezes.
- missing qualifying input: an approved model register entry with a weight artefact hash, licence, band order, normalisation and applicability domain, plus compliant imagery; none of these exist in this workspace
- normal: Inference runs only against a register entry whose hash recomputes, and the band and normalisation check plus the accelerator probe are published with the result.
- boundary: The no-accelerator path is either executed and reported or declared unsupported with a named reason.
- invalid: A placeholder or undocumented weight file, an input outside the applicability domain, or a silent device fallback.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `MRC-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `SRC-001`, `TS08-INPUT-QUAL-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, check_extension, create_layout, create_map, describe_geoprocessing_tool, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, run_geoprocessing; candidates infer_approved_raster_model
- stress: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:199; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:228; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:269; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S49` 多年遥感趋势 / multi-year remote sensing trends
- selectionBasis: catalog §6 row S49 is the multi-year trend scenario; catalog §10 row 270 binds X27/X28 to the S49 sub-cases with TS09/TS12 artifacts, and row 281 binds the X46 scientific-cube sub-case to the same scenario, which is why TS12 is carried as a secondary reference.
- missing qualifying input: a multi-date image stack on one grid with sensor-consistency notes and enough effective observations per cell to test a trend
- normal: The stack declares grid, missing and sensor policy before any statistic, and trend output reports slope, significance and effective sample count per cell.
- boundary: Cells below the effective-sample minimum are published as no-result.
- invalid: A trend presented as significant without a multiple-comparison policy, or dates silently resampled to force grid agreement.
- rules: `ALN-001`, `GEO-001`, `KDA-001`, `LAY-001`, `LIX-001`, `NUM-001`, `OSV-001`, `OUT-001`, `PRM-001`, `PRT-001`, `RMO-004`, `SCI-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, cell_statistics, create_layout, create_map, export_layout_pdf, focal_statistics, get_feature_count, get_layer_info, get_map_info; candidates build_raster_time_series, detect_raster_trends, export_spatiotemporal_cube
- stress: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:200; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:229; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:270; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S50` 空间采样设计 / spatial sample design
- selectionBasis: catalog §6 row S50 is the sampling-design scenario; catalog §10 row 271 binds X29 to S50 采样 with TS18 as its artifact, the multi-scale sampling-design template M4 defines here.
- missing qualifying input: a design brief with strata, constraints, minimum distance and a published seed
- normal: The design echoes extent, strata, constraints, seed and inclusion rule, and the same seed reproduces the identical point set.
- boundary: A point falling on a stratum boundary is handled by the published boundary rule.
- invalid: A design with no seed, or a re-run that returns a different set under the same parameters.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `SDU-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_field_values, get_layer_info, get_map_info, select_by_attribute; candidates design_spatial_sample
- stress: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:201; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:238; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:271; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S51` 插值与空间交叉验证 / interpolation with spatial cross-validation
- selectionBasis: catalog §6 row S51 pairs interpolation with spatial validation; catalog §10 row 271 binds X30/X31 to S51 with TS16 (interpolation residual and uncertainty) and row 274 binds the X36 method-applicability sub-case to the same scenario, so TS16 is defined here and the assumption check is booked separately.
- missing qualifying input: observations with values, a declared method and parameters, and spatial blocks with a buffer for the folds
- normal: Prediction echoes the method; an error surface appears only where the method supports one; folds are split by spatial blocks with the training extent of each fold published.
- boundary: A method without a supportable error surface is reported as prediction only.
- invalid: A validation point inside its own training buffer, or a score not recomputable from the published residuals.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NUM-001`, `OSV-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `SDU-002`, `SDU-003`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_field_statistics, get_layer_info, get_map_info, summarize_features; candidates cross_validate_spatial_model, interpolate_surface, validate_statistical_assumptions
- stress: ST-M4-15
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:202; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:236; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:271; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S53` 决策权重敏感性 / decision weight sensitivity
- selectionBasis: catalog §6 row S53 is the sensitivity scenario; catalog §10 row 272 binds X32 to S53 敏感性 with TP18, and row 273 binds X34 to the S53 multi-criteria sub-case with the same TP18 artifact, so M4 defines TP18 here.
- missing qualifying input: a declared parameter domain, criteria directions, normalisation and weights traceable to a brief
- normal: Perturbation ranges and step counts are echoed, ranking stability is reported, and every weight traces to the brief; constraints are applied before scoring and independently of it.
- boundary: An alternative that fails a constraint is excluded from the ranking yet still listed.
- invalid: A perturbation reported as a probability, or a weight invented by the assistant with no provenance.
- rules: `KDA-001`, `LAY-001`, `NUM-001`, `OSV-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `SDU-003`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, summarize_features, validate_plan; candidates analyze_scenario_sensitivity, compare_multi_criteria_scenarios, validate_statistical_assumptions
- stress: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:204; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:238; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:272; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S54` 情景集合统计 / scenario ensemble statistics
- selectionBasis: catalog §6 row S54 is the ensemble scenario; catalog §10 row 273 binds X35 to S54 集合统计 with TS17, the scenario-ensemble template M4 defines here.
- missing qualifying input: a bounded set of scenarios with their provenance, plus weights only where a basis is published
- normal: Without probabilistic weights the output is labelled a scenario distribution; with them the basis is cited. Disagreement areas are published next to the aggregate.
- boundary: An ensemble where one scenario dominates is reported with its weight and reason.
- invalid: A distribution of scenarios described as a confidence interval.
- rules: `KDA-001`, `LAY-001`, `NUM-001`, `OSV-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `SDU-003`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_job_status, get_layer_info, get_map_info, run_batch; candidates evaluate_scenario_ensemble
- stress: ST-M4-16
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:205; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:237; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:273; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S55` OD通勤成本 / origin-destination commuting cost
- selectionBasis: catalog §6 row S55 is the OD scenario; catalog §10 row 275 binds X37 to the S55 OD sub-case with TP04/TP08 and X38 to the S09 nearest-facility sub-case, so TP04 stays the M2-defined template and the blocked whitelist precondition from the frozen M3 register is inherited by reference.
- missing qualifying input: a real network dataset with an impedance attribute, direction and units; this workspace has none, and the frozen 53-name whitelist contains no network solver entry
- normal: The OD matrix echoes network, impedance, units, direction and time window, and reports unlocated and disconnected pairs.
- boundary: A pair with no path is published as unreachable with the reason, not as a large finite cost.
- invalid: Any result computed across a disconnect with a fabricated cost, or without a declared impedance.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NFD-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, near, spatial_join; candidates build_od_cost_matrix, find_closest_facilities
- stress: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:206; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:224; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:275; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S56` 公共设施选址优化 / facility location allocation
- selectionBasis: catalog §6 row S56 is the location-allocation scenario; catalog §10 row 276 binds X39 to S56 选址 with TP12 and states that S56 also covers TP17, which is why TP12 is primary and TP17 is a cited secondary reference into the frozen M2 face.
- missing qualifying input: a network with capacity, demand weights and an objective, plus a small-instance optimum that can be verified by hand
- normal: Chosen facilities, allocated demand, capacity use and unserved demand are published with the objective value and the termination condition.
- boundary: Demand that cannot be served is listed with its reason and stays in the denominator.
- invalid: A capacity violation reported as feasible, or a solver silence read as an optimum.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NFD-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `SRC-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, select_by_location, summarize_features; candidates solve_location_allocation
- stress: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:207; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:232; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:276; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S57` 多车辆配送 / multi-vehicle routing
- selectionBasis: catalog §6 row S57 is the vehicle-routing scenario; catalog §10 row 276 binds X40 to S57 车队 with TP13, the delivery space-and-time template M4 defines here.
- missing qualifying input: an order set with time windows, fleet capacity and a network, plus a small instance with a verifiable solution
- normal: Every route is echoed with load, time-window compliance and distance, each constraint is checked item by item, and undistributed orders are listed.
- boundary: An order that fits no vehicle is reported undistributed with the binding constraint named.
- invalid: A time-window violation presented as feasible, or a single-route result passed off as a fleet solution.
- rules: `GEO-001`, `KDA-001`, `LAY-001`, `NFD-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, export_table, get_feature_count, get_layer_features, get_layer_info, get_map_info; candidates optimize_vehicle_routes
- stress: ST-M4-17
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:208; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:233; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:276; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S58` 路网中断韧性 / network disruption resilience
- selectionBasis: catalog §6 row S58 is the disruption scenario; catalog §10 row 276 binds X41 to S58 中断 with TP11, so TP11 (disruption and alternative corridor) is defined here.
- missing qualifying input: a network with a declared disruption scenario and a before-and-after reachability answer
- normal: Before and after reachability, cost and critical paths are reported from the same model version, and disconnects are published as disconnects.
- boundary: A disruption that isolates a component lists the isolated demand instead of absorbing it.
- invalid: A reuse of the baseline result after a model change, or a finite cost asserted across a break.
- rules: `DEP-001`, `GEO-001`, `KDA-001`, `LAY-001`, `NFD-001`, `NUM-001`, `OUT-001`, `PRM-001`, `PRT-001`, `SCI-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_layout_pdf, get_broken_layers, get_feature_count, get_layer_info, get_map_info, trace_dataset_dependencies; candidates analyze_network_disruption
- stress: none
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:209; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:231; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:276; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

### `S59` 离线地图交接与往返检查 / offline handover with round-trip verification
- selectionBasis: catalog §6 row S59 is the offline-handover scenario; catalog §10 row 265 binds X14 to the S59 3D handover sub-case with a TP14 preview, rows 278 and 280 bind X43 and X45 to the S59 round-trip and offline sub-cases, and X45 is calibrated as the packageType branch of export_data_package rather than a new public name.
- missing qualifying input: a package format whose resource boundary and reopen behaviour are actually probed, plus a standard version for the conformance report; neither exists in this workspace
- normal: The package lists its dependency closure, reopens with the source path unavailable, and the conformance report names the standard version and the adapted format range.
- boundary: A package containing one external resource that is intentionally left out must declare the exclusion.
- invalid: A conformance claim built only on package hashes, or a reopen that silently reads the original path.
- rules: `DEP-001`, `KDA-001`, `LAY-001`, `LIX-001`, `NUM-001`, `OUT-001`, `PKG-001`, `PNA-001`, `PRM-001`, `PRT-001`, `QLT-001`, `SCI-001`, `SCN-004`, `SRC-001`, `TXT-001`, `WF-001`
- tools: accepted add_layer, add_legend, add_north_arrow, add_scale_bar, create_layout, create_map, export_features, export_layout_pdf, get_feature_count, get_layer_info, get_map_info, save_layer_file, validate_delivery_package; candidates build_offline_map_package, export_scene_package, validate_interchange_conformance
- stress: ST-M4-18
- sourceRefs: GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §6:210; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §7:234; GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md §10:265; GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md §9:153 M4 exit row

