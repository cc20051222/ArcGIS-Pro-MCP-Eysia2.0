# GP 白名单 53 件·fixture 合成清单 · D-117 合成段（G-273 R-1）

- 裁定依据＝`G-273 R-1 (2026-10-04 15:0x, 用户裁定「授权合成 fixture」)`；生成器＝`run-20261004-d117/phase-fixture/synthesize_gp_fixtures_d117.py`（确定性种子 `seed=20261004`，无时钟、无随机）。
- **合成输入基准**＝本件全部产物均为 `synthetic: true`，一律标注「**合成输入基准**」，不冒称真实数据评测。
- 量测域＝64×48 像元 / cell 2.0 / EPSG:32651 / extent `[500000.0, 3999904.0, 500128.0, 4000000.0]`；GeoTIFF 上左原点约定与 ASCII 左下约定均**写入后再用读取器复解析**核对。
- 零执行＝no Pro, no MCP, no tools/call, no GP invocation, no GIS write；冻结面零改动＝既有 78 件语料与四锚字节改动了 `0` 处（只增不改）。
- 定标选择（`d117CalibrationChoice: true`）＝类型化输入池＋逐件参数映射；依据＝G-273 R-1 授权逐件合成合格输入；采用「类型化输入池＋逐件参数映射」以让 53 件共用在案量测域（同 EPSG/同格网/同范围），避免 53×N 副本造成的不可核对与噪声；先例＝D-101 组复用（groups ts08/x1..x8）
- CRS 偏差公开＝vector pool carries planar metres inside a GeoJSON-shaped document (not RFC 7946 lon/lat) — labelled, single measurement domain preferred

## 一、输入池（逐件字节＋SHA-256，可复算）

| 键 | 路径 | 类别 | 字节 | SHA-256（前 24） | 可承载声明类型 |
|---|---|---|---|---|---|
| `attrs` | `Benchmarks/inputs/gp53/gp_attrs.csv` | input | 86 | `43B496E35FB3C0C373BD50ED…` | `table`、`tableview`、`dataset` |
| `classes` | `Benchmarks/inputs/gp53/gp_classes.tif` | input | 6,448 | `F9949F5D786F9846F993927A…` | `rasterlayer`、`rasterdataset`、`dataset`、`multivalue` |
| `classesAsc` | `Benchmarks/inputs/gp53/gp_classes.asc` | input | 6,289 | `E05C310471103B3D42D366BD…` | `rasterlayer`、`rasterdataset`、`file`、`dataset` |
| `dem` | `Benchmarks/inputs/gp53/gp_dem.tif` | input | 6,448 | `85B918D5B0D8D0ADB8D67CAB…` | `rasterlayer`、`rasterdataset`、`dataset`、`multivalue` |
| `events` | `Benchmarks/inputs/gp53/gp_events.csv` | input | 302 | `066DF965B16B67FF7AB3647D…` | `table`、`tableview` |
| `featuresJson` | `Benchmarks/inputs/gp53/gp_features.json` | input | 342 | `FAF60A47DD69183EBB76C428…` | `file` |
| `flowacc` | `Benchmarks/inputs/gp53/gp_flowacc.tif` | input | 6,448 | `B2438706BD1B13A18A51875F…` | `rasterlayer`、`rasterdataset`、`dataset`、`multivalue` |
| `flowdir` | `Benchmarks/inputs/gp53/gp_flowdir.tif` | input | 6,448 | `9D8E6A877F5EAA309844C4D7…` | `rasterlayer`、`rasterdataset`、`dataset`、`multivalue` |
| `lines` | `Benchmarks/inputs/gp53/gp_lines.geojson` | input | 366 | `AC7F5A29FBB91C32F7F77BCA…` | `featurelayer`、`featureclass`、`dataset`、`multivalue` |
| `negative3band` | `Benchmarks/inputs/gp53/gp_negative_3band.tif` | negative | 18,748 | `F95967EECBEE79A5DE0C7E8A…` | — |
| `points` | `Benchmarks/inputs/gp53/gp_points.geojson` | input | 982 | `9E1399E08EB2EC9413EBC5DB…` | `featurelayer`、`featureclass`、`dataset`、`multivalue` |
| `polys_a` | `Benchmarks/inputs/gp53/gp_polys_a.geojson` | input | 841 | `5277B92BDCB5E7EFC3CF03E8…` | `featurelayer`、`featureclass`、`dataset`、`multivalue` |
| `polys_b` | `Benchmarks/inputs/gp53/gp_polys_b.geojson` | input | 613 | `B61E183675DAF1EEAF2DE677…` | `featurelayer`、`featureclass`、`dataset`、`multivalue` |
| `remap` | `Benchmarks/inputs/gp53/gp_remap.txt` | input | 43 | `F2C5AA394C2E1D7CB73D46DA…` | `remap`、`file`、`multivalue` |
| `slope` | `Benchmarks/inputs/gp53/gp_slope.tif` | input | 6,448 | `A1145A5CB6FC8197C86CD9F3…` | `rasterlayer`、`rasterdataset`、`dataset`、`multivalue` |

## 二、逐件参数映射（53 件，全量）

| # | 件名 | 参数数 | 输入数 | 承载体 | 字面量 | 输出模板 | 未闭合 | fullyCovered |
|---|---|---|---|---|---|---|---|---|
| 1 | `analysis.Buffer` | 8 | 7 | 2 | 5 | 1 | — | 是 |
| 2 | `analysis.Clip` | 4 | 3 | 2 | 1 | 1 | — | 是 |
| 3 | `analysis.Erase` | 4 | 3 | 2 | 1 | 1 | — | 是 |
| 4 | `analysis.Union` | 5 | 4 | 1 | 3 | 1 | — | 是 |
| 5 | `analysis.Intersect` | 5 | 4 | 1 | 3 | 1 | — | 是 |
| 6 | `analysis.Identity` | 5 | 4 | 2 | 2 | 1 | — | 是 |
| 7 | `analysis.SpatialJoin` | 8 | 7 | 2 | 5 | 1 | — | 是 |
| 8 | `analysis.Near` | 7 | 7 | 2 | 5 | 0 | — | 是 |
| 9 | `analysis.Statistics` | 4 | 3 | 3 | 0 | 1 | — | 是 |
| 10 | `analysis.Frequency` | 4 | 3 | 3 | 0 | 1 | — | 是 |
| 11 | `analysis.TabulateArea` | 6 | 5 | 2 | 3 | 1 | — | 是 |
| 12 | `analysis.TabulateIntersection` | 6 | 5 | 4 | 1 | 1 | — | 是 |
| 13 | `management.Dissolve` | 6 | 5 | 3 | 2 | 1 | — | 是 |
| 14 | `management.Merge` | 3 | 2 | 1 | 1 | 1 | — | 是 |
| 15 | `management.Project` | 6 | 5 | 1 | 3 | 1 | — | 否 |
| 16 | `management.RepairGeometry` | 3 | 3 | 1 | 2 | 0 | — | 是 |
| 17 | `management.CalculateField` | 5 | 5 | 1 | 4 | 0 | — | 是 |
| 18 | `management.CopyFeatures` | 3 | 2 | 1 | 1 | 1 | — | 是 |
| 19 | `management.MultipartToSinglepart` | 2 | 1 | 1 | 0 | 1 | — | 是 |
| 20 | `conversion.ExportFeatures` | 3 | 2 | 1 | 1 | 1 | — | 是 |
| 21 | `conversion.TableToTable` | 5 | 3 | 1 | 2 | 2 | — | 是 |
| 22 | `conversion.RasterToPolygon` | 4 | 3 | 1 | 2 | 1 | — | 是 |
| 23 | `conversion.RasterToPoint` | 3 | 2 | 1 | 1 | 1 | — | 是 |
| 24 | `conversion.PolygonToRaster` | 6 | 5 | 1 | 4 | 1 | — | 是 |
| 25 | `conversion.PointToRaster` | 6 | 5 | 1 | 4 | 1 | — | 是 |
| 26 | `conversion.PolylineToRaster` | 6 | 5 | 1 | 4 | 1 | — | 是 |
| 27 | `conversion.FeatureToRaster` | 4 | 3 | 1 | 2 | 1 | — | 是 |
| 28 | `conversion.ASCIIToRaster` | 3 | 2 | 1 | 1 | 1 | — | 是 |
| 29 | `conversion.RasterToASCII` | 2 | 1 | 1 | 0 | 1 | — | 是 |
| 30 | `conversion.FeaturesToJSON` | 5 | 4 | 1 | 3 | 1 | — | 是 |
| 31 | `conversion.JSONToFeatures` | 3 | 2 | 1 | 1 | 1 | — | 是 |
| 32 | `sa.Slope` | 5 | 4 | 1 | 3 | 1 | — | 是 |
| 33 | `sa.Aspect` | 4 | 3 | 1 | 2 | 1 | — | 是 |
| 34 | `sa.Hillshade` | 6 | 5 | 1 | 4 | 1 | — | 是 |
| 35 | `sa.Contour` | 6 | 5 | 1 | 4 | 1 | — | 是 |
| 36 | `sa.Curvature` | 3 | 2 | 1 | 1 | 1 | — | 是 |
| 37 | `sa.Fill` | 3 | 2 | 1 | 1 | 1 | — | 是 |
| 38 | `sa.FlowDirection` | 4 | 3 | 2 | 1 | 1 | — | 是 |
| 39 | `sa.FlowAccumulation` | 4 | 3 | 2 | 1 | 1 | — | 是 |
| 40 | `sa.Watershed` | 4 | 3 | 2 | 1 | 1 | — | 是 |
| 41 | `sa.SnapPourPoint` | 5 | 4 | 2 | 2 | 1 | — | 是 |
| 42 | `sa.ZonalStatistics` | 6 | 5 | 2 | 3 | 1 | — | 是 |
| 43 | `sa.ZonalStatisticsAsTable` | 6 | 5 | 2 | 3 | 1 | — | 是 |
| 44 | `sa.Reclassify` | 5 | 4 | 2 | 2 | 1 | — | 是 |
| 45 | `sa.ExtractByMask` | 5 | 4 | 2 | 2 | 1 | — | 是 |
| 46 | `sa.ExtractValuesToPoints` | 5 | 4 | 2 | 2 | 1 | — | 是 |
| 47 | `sa.Resample` | 4 | 3 | 1 | 2 | 1 | — | 是 |
| 48 | `sa.Con` | 5 | 4 | 3 | 1 | 1 | — | 是 |
| 49 | `sa.CellStatistics` | 4 | 3 | 1 | 2 | 1 | — | 是 |
| 50 | `sa.FocalStatistics` | 5 | 4 | 1 | 3 | 1 | — | 是 |
| 51 | `sa.RasterCalculator` | 2 | 1 | 0 | 1 | 1 | — | 是 |
| 52 | `management.AddJoin` | 5 | 5 | 2 | 3 | 0 | — | 是 |
| 53 | `management.RemoveJoin` | 2 | 2 | 1 | 1 | 0 | — | 是 |

## 三、算术闭合

- `52 fullyCovered + 1 openCarrierNeed = 53`；未闭合项一律如实登记（不猜测、不以非合格栅格充数），出处见每行 `basis`/`unresolved.why`。

## 四、逐件未闭合登记

- `management.Project`：`transform_method`(multivalue) — option-list argument whose member set is not enumerated by any accepted evidence — not guessed

## 五、Phase B 交接

- 本件只提供**输入侧**闭合；准入判定仍需真机窗（G-273 R-1②：合成段验收后另批派单），窗内逐件四查＝真名存在／许可面／参数签名实测／最小受控执行，产出只落 D 盘，并逐帧留 raw＋宿主 PID＋装机件哈希。
- 本段所有结论标注「**合成输入基准**」；未运行处一律 `NOT VERIFIED`。

