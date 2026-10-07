# GP 白名单 53 件·覆盖差集 v2 · D-117 合成段（G-273 R-1）

- 规范标注＝**合成输入基准**（W2 整改：本 token 在本件与 closure v2 两面逐字出现）。
- 本件取代 `gp-input-coverage-gap-v1.json`；**v1 四件字节未动**（本脚本写盘前按冻结索引逐件复算哈希通过）。
- 形态自述（W1 整改）＝W1 整改：本件 perEntryCoverage 每条**确实含有**下列字段，交付文字只声称本件真实存在的字段。
- 准入态＝NOT VERIFIED（准入判定属 Phase B 真机窗，本段零执行）。

## 算术：52 件 executableInputAvailable=true ＋ 1 件仍 false ＝ 53

| # | 件名 | 输入可用 | 承载限定语 | fixture 承载 | 未闭合参数 |
|---|---|---|---|---|---|
| -01 | `analysis.Buffer` | 是 | 声明级完备，准入未验 | `attrs`、`polys_a` | — |
| -02 | `analysis.Clip` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -03 | `analysis.Erase` | 是 | 声明级完备，准入未验 | `polys_a`、`polys_b` | — |
| -04 | `analysis.Union` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -05 | `analysis.Intersect` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -06 | `analysis.Identity` | 是 | 声明级完备，准入未验 | `polys_a`、`polys_b` | — |
| -07 | `analysis.SpatialJoin` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -08 | `analysis.Near` | 是 | 声明级完备，准入未验 | `points`、`polys_a` | — |
| -09 | `analysis.Statistics` | 是 | 声明级完备，准入未验 | `attrs` | — |
| -10 | `analysis.Frequency` | 是 | 声明级完备，准入未验 | `attrs` | — |
| -11 | `analysis.TabulateArea` | 是 | 声明级完备，准入未验 | `polys_a`、`polys_b` | — |
| -12 | `analysis.TabulateIntersection` | 是 | 声明级完备，准入未验 | `attrs`、`polys_a`、`polys_b` | — |
| -13 | `management.Dissolve` | 是 | 声明级完备，准入未验 | `attrs`、`polys_a` | — |
| -14 | `management.Merge` | 是 | 声明级完备，准入未验 | `polys_a`、`polys_b` | — |
| -15 | `management.Project` | 否 | 声明级完备，准入未验 | `polys_a` | `transform_method` |
| -16 | `management.RepairGeometry` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -17 | `management.CalculateField` | 是 | 声明级完备，准入未验 | `attrs` | — |
| -18 | `management.CopyFeatures` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -19 | `management.MultipartToSinglepart` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -20 | `conversion.ExportFeatures` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -21 | `conversion.TableToTable` | 是 | 声明级完备，准入未验 | `attrs` | — |
| -22 | `conversion.RasterToPolygon` | 是 | 声明级完备，准入未验 | `dem` | — |
| -23 | `conversion.RasterToPoint` | 是 | 声明级完备，准入未验 | `dem` | — |
| -24 | `conversion.PolygonToRaster` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -25 | `conversion.PointToRaster` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -26 | `conversion.PolylineToRaster` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -27 | `conversion.FeatureToRaster` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -28 | `conversion.ASCIIToRaster` | 是 | 声明级完备，准入未验 | `classesAsc` | — |
| -29 | `conversion.RasterToASCII` | 是 | 声明级完备，准入未验 | `dem` | — |
| -30 | `conversion.FeaturesToJSON` | 是 | 声明级完备，准入未验 | `polys_a` | — |
| -31 | `conversion.JSONToFeatures` | 是 | 声明级完备，准入未验 | `featuresJson` | — |
| -32 | `sa.Slope` | 是 | 声明级完备，准入未验 | `dem` | — |
| -33 | `sa.Aspect` | 是 | 声明级完备，准入未验 | `dem` | — |
| -34 | `sa.Hillshade` | 是 | 声明级完备，准入未验 | `dem` | — |
| -35 | `sa.Contour` | 是 | 声明级完备，准入未验 | `dem` | — |
| -36 | `sa.Curvature` | 是 | 声明级完备，准入未验 | `dem` | — |
| -37 | `sa.Fill` | 是 | 声明级完备，准入未验 | `dem` | — |
| -38 | `sa.FlowDirection` | 是 | 声明级完备，准入未验 | `dem` | — |
| -39 | `sa.FlowAccumulation` | 是 | 声明级完备，准入未验 | `dem` | — |
| -40 | `sa.Watershed` | 是 | 声明级完备，准入未验 | `dem`、`polys_a` | — |
| -41 | `sa.SnapPourPoint` | 是 | 声明级完备，准入未验 | `dem`、`polys_a` | — |
| -42 | `sa.ZonalStatistics` | 是 | 声明级完备，准入未验 | `dem`、`polys_a`、`polys_b` | — |
| -43 | `sa.ZonalStatisticsAsTable` | 是 | 声明级完备，准入未验 | `dem`、`polys_a`、`polys_b` | — |
| -44 | `sa.Reclassify` | 是 | 声明级完备，准入未验 | `dem`、`remap` | — |
| -45 | `sa.ExtractByMask` | 是 | 声明级完备，准入未验 | `dem`、`polys_a` | — |
| -46 | `sa.ExtractValuesToPoints` | 是 | 声明级完备，准入未验 | `dem`、`points` | — |
| -47 | `sa.Resample` | 是 | 声明级完备，准入未验 | `dem` | — |
| -48 | `sa.Con` | 是 | 声明级完备，准入未验 | `dem` | — |
| -49 | `sa.CellStatistics` | 是 | 声明级完备，准入未验 | `classes`、`dem` | — |
| -50 | `sa.FocalStatistics` | 是 | 声明级完备，准入未验 | `dem` | — |
| -51 | `sa.RasterCalculator` | 是 | 声明级完备，准入未验 | — | — |
| -52 | `management.AddJoin` | 是 | 声明级完备，准入未验 | `attrs`、`polys_a` | — |
| -53 | `management.RemoveJoin` | 是 | 声明级完备，准入未验 | `polys_a` | — |

## 逐件声明的限定语原文

> 仍为 false 的件：management.Project

