# GP 白名单 53 件·覆盖差集 v3 · D-117 合成段整改重建（r2／G-273 R-1）

- 规范标注＝**合成输入基准**（本件与 closure v3 两面逐字出现）。
- 取代 `gp-input-coverage-gap-v2.json`；**v1／v2 面字节未动**（写盘前按冻结索引 #1／#2 逐件复算哈希通过）。
- 形态自述（W1 延续）＝W1 整改延续：本件 perEntryCoverage 每条**确实含有**下列字段，交付文字只声称本件真实存在的字段。
- 整改内容＝承载由「按声明类型」收紧为「按 (件, 参数) 语义钉位」；池字节与 v1/v2 面均未回改（写盘前按索引 #1/#2 复算通过）。
- 准入态＝NOT VERIFIED（准入判定属 Phase B 真机窗，本段零执行）。

## 算术：52 件 executableInputAvailable=true ＋ 1 件仍 false ＝ 53；语义钉位 37 处；未被引用件＝`negative3band`（阴性对照，依设计）

| # | 件名 | 输入可用 | 钉位数 | fixture 承载 | 未闭合参数 |
|---|---|---|---|---|---|
| -01 | `analysis.Buffer` | 是 | 0 | `attrs`、`polys_a` | — |
| -02 | `analysis.Clip` | 是 | 1 | `polys_a`、`polys_b` | — |
| -03 | `analysis.Erase` | 是 | 1 | `polys_a`、`polys_b` | — |
| -04 | `analysis.Union` | 是 | 1 | `polys_a`、`polys_b` | — |
| -05 | `analysis.Intersect` | 是 | 1 | `polys_a`、`polys_b` | — |
| -06 | `analysis.Identity` | 是 | 1 | `polys_a`、`polys_b` | — |
| -07 | `analysis.SpatialJoin` | 是 | 1 | `polys_a`、`polys_b` | — |
| -08 | `analysis.Near` | 是 | 1 | `polys_a`、`polys_b` | — |
| -09 | `analysis.Statistics` | 是 | 0 | `attrs` | — |
| -10 | `analysis.Frequency` | 是 | 1 | `attrs`、`events` | — |
| -11 | `analysis.TabulateArea` | 是 | 2 | `polys_a`、`polys_b` | — |
| -12 | `analysis.TabulateIntersection` | 是 | 2 | `attrs`、`polys_a`、`polys_b` | — |
| -13 | `management.Dissolve` | 是 | 0 | `attrs`、`polys_a` | — |
| -14 | `management.Merge` | 是 | 0 | `polys_a`、`polys_b` | — |
| -15 | `management.Project` | 否 | 0 | `polys_a` | `transform_method` |
| -16 | `management.RepairGeometry` | 是 | 0 | `polys_a` | — |
| -17 | `management.CalculateField` | 是 | 0 | `attrs` | — |
| -18 | `management.CopyFeatures` | 是 | 0 | `polys_a` | — |
| -19 | `management.MultipartToSinglepart` | 是 | 0 | `polys_a` | — |
| -20 | `conversion.ExportFeatures` | 是 | 0 | `polys_a` | — |
| -21 | `conversion.TableToTable` | 是 | 0 | `attrs` | — |
| -22 | `conversion.RasterToPolygon` | 是 | 2 | `classes` | — |
| -23 | `conversion.RasterToPoint` | 是 | 2 | `classes` | — |
| -24 | `conversion.PolygonToRaster` | 是 | 0 | `polys_a` | — |
| -25 | `conversion.PointToRaster` | 是 | 1 | `points` | — |
| -26 | `conversion.PolylineToRaster` | 是 | 1 | `lines` | — |
| -27 | `conversion.FeatureToRaster` | 是 | 0 | `polys_a` | — |
| -28 | `conversion.ASCIIToRaster` | 是 | 0 | `classesAsc` | — |
| -29 | `conversion.RasterToASCII` | 是 | 0 | `dem` | — |
| -30 | `conversion.FeaturesToJSON` | 是 | 0 | `polys_a` | — |
| -31 | `conversion.JSONToFeatures` | 是 | 0 | `featuresJson` | — |
| -32 | `sa.Slope` | 是 | 0 | `dem` | — |
| -33 | `sa.Aspect` | 是 | 0 | `dem` | — |
| -34 | `sa.Hillshade` | 是 | 1 | `slope` | — |
| -35 | `sa.Contour` | 是 | 0 | `dem` | — |
| -36 | `sa.Curvature` | 是 | 0 | `dem` | — |
| -37 | `sa.Fill` | 是 | 0 | `dem` | — |
| -38 | `sa.FlowDirection` | 是 | 1 | `dem`、`flowdir` | — |
| -39 | `sa.FlowAccumulation` | 是 | 2 | `flowdir` | — |
| -40 | `sa.Watershed` | 是 | 3 | `flowdir`、`points` | — |
| -41 | `sa.SnapPourPoint` | 是 | 3 | `flowacc`、`points` | — |
| -42 | `sa.ZonalStatistics` | 是 | 2 | `classes`、`dem` | — |
| -43 | `sa.ZonalStatisticsAsTable` | 是 | 2 | `classes`、`dem` | — |
| -44 | `sa.Reclassify` | 是 | 2 | `classes`、`remap` | — |
| -45 | `sa.ExtractByMask` | 是 | 0 | `dem`、`polys_a` | — |
| -46 | `sa.ExtractValuesToPoints` | 是 | 0 | `dem`、`points` | — |
| -47 | `sa.Resample` | 是 | 0 | `dem` | — |
| -48 | `sa.Con` | 是 | 1 | `classes`、`dem` | — |
| -49 | `sa.CellStatistics` | 是 | 0 | `classes`、`dem` | — |
| -50 | `sa.FocalStatistics` | 是 | 0 | `dem` | — |
| -51 | `sa.RasterCalculator` | 是 | 1 | — | — |
| -52 | `management.AddJoin` | 是 | 1 | `events`、`polys_a` | — |
| -53 | `management.RemoveJoin` | 是 | 0 | `polys_a` | — |

## 限定语原文（逐件同值）

> carriers pinned per (tool, parameter) over the synthesised pool; declared parameter types alone were not accepted as coverage; admission remains NOT VERIFIED until a Phase B real-device window

> 仍为 false 的件＝`management.Project`

