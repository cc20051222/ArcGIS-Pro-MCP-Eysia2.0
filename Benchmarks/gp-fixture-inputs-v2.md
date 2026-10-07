# GP 白名单 53 件·fixture 合成清单 v2（整改重建 r2）· D-117 合成段（G-273 R-1）

- 裁定依据＝`G-273 R-1 (2026-10-04 15:0x, 用户裁定「授权合成 fixture」)`；本件取代 `gp-fixture-inputs-v1.json`（v1 字节未动，索引 #2 记录值 `126,937 B/7D0F45477D5303330D6F34CF…`）。
- 规范标注＝**合成输入基准**（逐件 `synthetic: true`＋`lineage`，标题与本面逐字出现）。
- 量测域＝64×48 像元 / cell 2.0 / EPSG:32651 / extent `[500000.0, 3999904.0, 500128.0, 4000000.0]`。
- 零执行＝no Pro, no MCP, no tools/call, no GP invocation, no GIS write。

## 一、整改登记（本席自查，先于交件）

- 缺陷＝r1 keyed carriers on the declared parameter type, so every raster slot received the DEM and six pool artefacts went unreferenced。
- 发现方式＝L2 executor, pre-delivery re-read of perToolPlan (not reported by the commander)。
- 整改＝37 (tool, parameter) carrier pins plus 3 name-driven literal rules; pool bytes unchanged, faces additive (v1 kept)；已钉位并生效者 37 处。

| 槽位 | 承载件 |
|---|---|
| `analysis.Clip.clip_features` | `Benchmarks/inputs/gp53/gp_polys_b.geojson` |
| `analysis.Erase.erase_features` | `Benchmarks/inputs/gp53/gp_polys_b.geojson` |
| `analysis.Frequency.in_table` | `Benchmarks/inputs/gp53/gp_events.csv` |
| `analysis.Identity.identity_features` | `Benchmarks/inputs/gp53/gp_polys_b.geojson` |
| `analysis.Intersect.in_features` | `['polys_a', 'polys_b']` |
| `analysis.Near.near_features` | `['polys_a', 'polys_b']` |
| `analysis.SpatialJoin.join_features` | `Benchmarks/inputs/gp53/gp_polys_b.geojson` |
| `analysis.TabulateArea.in_class_data` | `Benchmarks/inputs/gp53/gp_polys_b.geojson` |
| `analysis.TabulateArea.in_zone_data` | `Benchmarks/inputs/gp53/gp_polys_a.geojson` |
| `analysis.TabulateIntersection.in_class_features` | `Benchmarks/inputs/gp53/gp_polys_b.geojson` |
| `analysis.TabulateIntersection.in_zone_features` | `Benchmarks/inputs/gp53/gp_polys_a.geojson` |
| `analysis.Union.in_features` | `['polys_a', 'polys_b']` |
| `conversion.PointToRaster.in_features` | `Benchmarks/inputs/gp53/gp_points.geojson` |
| `conversion.PolylineToRaster.in_features` | `Benchmarks/inputs/gp53/gp_lines.geojson` |
| `conversion.RasterToPoint.in_raster` | `Benchmarks/inputs/gp53/gp_classes.tif` |
| `conversion.RasterToPoint.raster_field` | `VALUE` |
| `conversion.RasterToPolygon.in_raster` | `Benchmarks/inputs/gp53/gp_classes.tif` |
| `conversion.RasterToPolygon.raster_field` | `VALUE` |
| `management.AddJoin.join_table` | `Benchmarks/inputs/gp53/gp_events.csv` |
| `sa.Con.in_conditional_raster` | `Benchmarks/inputs/gp53/gp_classes.tif` |
| `sa.FlowAccumulation.in_flow_direction_raster` | `Benchmarks/inputs/gp53/gp_flowdir.tif` |
| `sa.FlowAccumulation.in_weight_raster` | `Benchmarks/inputs/gp53/gp_flowdir.tif` |
| `sa.FlowDirection.in_weight_raster` | `Benchmarks/inputs/gp53/gp_flowdir.tif` |
| `sa.Hillshade.in_raster` | `Benchmarks/inputs/gp53/gp_slope.tif` |
| `sa.RasterCalculator.expression` | `"gp_dem.tif" * 1.0` |
| `sa.Reclassify.in_raster` | `Benchmarks/inputs/gp53/gp_classes.tif` |
| `sa.Reclassify.reclass_field` | `VALUE` |
| `sa.SnapPourPoint.in_accumulation_raster` | `Benchmarks/inputs/gp53/gp_flowacc.tif` |
| `sa.SnapPourPoint.in_pour_point_data` | `Benchmarks/inputs/gp53/gp_points.geojson` |
| `sa.SnapPourPoint.pour_point_field` | `id` |
| `sa.Watershed.in_flow_direction_raster` | `Benchmarks/inputs/gp53/gp_flowdir.tif` |
| `sa.Watershed.in_pour_point_data` | `Benchmarks/inputs/gp53/gp_points.geojson` |
| `sa.Watershed.pour_point_field` | `id` |
| `sa.ZonalStatistics.in_zone_data` | `Benchmarks/inputs/gp53/gp_classes.tif` |
| `sa.ZonalStatistics.zone_field` | `VALUE` |
| `sa.ZonalStatisticsAsTable.in_zone_data` | `Benchmarks/inputs/gp53/gp_classes.tif` |
| `sa.ZonalStatisticsAsTable.zone_field` | `VALUE` |

## 二、输入池（逐件字节＋SHA-256＋被引用数，可复算）

| 键 | 路径 | 类别 | 字节 | SHA-256（前 24） | 被引用件数 |
|---|---|---|---|---|---|
| `attrs` | `Benchmarks/inputs/gp53/gp_attrs.csv` | input | 86 | `43B496E35FB3C0C373BD50ED…` | 7 |
| `classes` | `Benchmarks/inputs/gp53/gp_classes.tif` | input | 6,448 | `F9949F5D786F9846F993927A…` | 7 |
| `classesAsc` | `Benchmarks/inputs/gp53/gp_classes.asc` | input | 6,289 | `E05C310471103B3D42D366BD…` | 1 |
| `dem` | `Benchmarks/inputs/gp53/gp_dem.tif` | input | 6,448 | `85B918D5B0D8D0ADB8D67CAB…` | 15 |
| `events` | `Benchmarks/inputs/gp53/gp_events.csv` | input | 302 | `066DF965B16B67FF7AB3647D…` | 2 |
| `featuresJson` | `Benchmarks/inputs/gp53/gp_features.json` | input | 342 | `FAF60A47DD69183EBB76C428…` | 1 |
| `flowacc` | `Benchmarks/inputs/gp53/gp_flowacc.tif` | input | 6,448 | `B2438706BD1B13A18A51875F…` | 1 |
| `flowdir` | `Benchmarks/inputs/gp53/gp_flowdir.tif` | input | 6,448 | `9D8E6A877F5EAA309844C4D7…` | 3 |
| `lines` | `Benchmarks/inputs/gp53/gp_lines.geojson` | input | 366 | `AC7F5A29FBB91C32F7F77BCA…` | 1 |
| `negative3band` | `Benchmarks/inputs/gp53/gp_negative_3band.tif` | negative | 18,748 | `F95967EECBEE79A5DE0C7E8A…` | 0 |
| `points` | `Benchmarks/inputs/gp53/gp_points.geojson` | input | 982 | `9E1399E08EB2EC9413EBC5DB…` | 4 |
| `polys_a` | `Benchmarks/inputs/gp53/gp_polys_a.geojson` | input | 841 | `5277B92BDCB5E7EFC3CF03E8…` | 23 |
| `polys_b` | `Benchmarks/inputs/gp53/gp_polys_b.geojson` | input | 613 | `B61E183675DAF1EEAF2DE677…` | 10 |
| `remap` | `Benchmarks/inputs/gp53/gp_remap.txt` | input | 43 | `F2C5AA394C2E1D7CB73D46DA…` | 1 |
| `slope` | `Benchmarks/inputs/gp53/gp_slope.tif` | input | 6,448 | `A1145A5CB6FC8197C86CD9F3…` | 1 |

- 未被任何 plan 引用的件＝`negative3band`，均为**阴性对照件**（其用途是复现在案 ERROR 000864 多波段面，依设计不作合格输入）。

## 三、规则账

- 声明规则 43 条／实际触发 38 条；未触发＝`R-BAND`、`R-LINE`、`R-OUTFILE`、`R-REMR`、`R-WS`（词表余量，非覆盖证据）。
- 参数账＝{"total": 242, "artefactCarriers": 80, "literalCarriers": 112, "markedCalibrationDefaults": 110, "semanticPins": 37}

## 四、算术闭合

- `52 fullyCovered + 1 openCarrierNeed = 53`；未闭合＝`management.Project`，其逐参数原因见 `unresolved.why`（不猜测、不以非合格栅格充数）。

## 五、Phase B 交接

- 本件只闭合**输入侧**；准入判定仍需真机窗（G-273 R-1②：合成段验收后另批派单）。 窗内逐件四查＝真名存在／许可面／参数签名实测／最小受控执行，产出只落 D 盘，逐帧留 raw＋宿主 PID＋装机件哈希。
- 本段所有结论标注 **合成输入基准**；未运行处一律 `NOT VERIFIED`。

