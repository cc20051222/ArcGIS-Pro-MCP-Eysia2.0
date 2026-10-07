# GP 白名单 53 件评测闭环台账 · v3（D-117 合成段整改重建后刷新）

- 规范标注＝**合成输入基准**；取代 `gp-whitelist-closure-53-v2.jsonl`，v2 保持字节不变（`v2RetainedUnchanged: true`），v1 亦未动。
- 依据＝G-273 R-1（用户裁定「授权合成 fixture」）＋G-271（Phase A 验收 PASS）；输入侧证据＝`Benchmarks/gp-fixture-inputs-v2.json`（15 件池＋逐件参数映射＋37 处语义/形态钉位）。
- 准入态＝`NOT VERIFIED`：本段零真机、零 GP 调用；执行侧判定待 Phase B 窗。
- 瞬态引用换代（G-269 ④／G-267 R-4）＝原 `D:\d116-out` 指向一律改为 L1 保全件 `.runtime/evolution/v5-f/run-20261002-d116/final-evidence/x20-semantics/q20221.TXT`；该根现 **ABSENT**。

## 逐件台账（53 行，v3）

| # | 件名 | destructive | 态 | 输入可用（声明级） | 钉位 | fixture 承载 | 未闭合 |
|---|---|---|---|---|---|---|---|
| 1 | `analysis.Buffer` | 否 | `NONE-IN-CASE` | 是 | 0 | `attrs`、`polys_a` | — |
| 2 | `analysis.Clip` | 否 | `NONE-IN-CASE` | 是 | 1 | `polys_a`、`polys_b` | — |
| 3 | `analysis.Erase` | 否 | `NONE-IN-CASE` | 是 | 1 | `polys_a`、`polys_b` | — |
| 4 | `analysis.Union` | 否 | `NONE-IN-CASE` | 是 | 1 | `polys_a`、`polys_b` | — |
| 5 | `analysis.Intersect` | 否 | `NONE-IN-CASE` | 是 | 1 | `polys_a`、`polys_b` | — |
| 6 | `analysis.Identity` | 否 | `NONE-IN-CASE` | 是 | 1 | `polys_a`、`polys_b` | — |
| 7 | `analysis.SpatialJoin` | 否 | `NONE-IN-CASE` | 是 | 1 | `polys_a`、`polys_b` | — |
| 8 | `analysis.Near` | 是 | `NONE-IN-CASE` | 是 | 1 | `polys_a`、`polys_b` | — |
| 9 | `analysis.Statistics` | 否 | `NONE-IN-CASE` | 是 | 0 | `attrs` | — |
| 10 | `analysis.Frequency` | 否 | `NONE-IN-CASE` | 是 | 1 | `attrs`、`events` | — |
| 11 | `analysis.TabulateArea` | 否 | `NOT-VERIFIED-NO-DATA` | 是 | 2 | `polys_a`、`polys_b` | — |
| 12 | `analysis.TabulateIntersection` | 否 | `NONE-IN-CASE` | 是 | 2 | `attrs`、`polys_a`、`polys_b` | — |
| 13 | `management.Dissolve` | 否 | `NONE-IN-CASE` | 是 | 0 | `attrs`、`polys_a` | — |
| 14 | `management.Merge` | 否 | `NONE-IN-CASE` | 是 | 0 | `polys_a`、`polys_b` | — |
| 15 | `management.Project` | 否 | `NONE-IN-CASE` | 否 | 0 | `polys_a` | `transform_method` |
| 16 | `management.RepairGeometry` | 是 | `NONE-IN-CASE` | 是 | 0 | `polys_a` | — |
| 17 | `management.CalculateField` | 是 | `NONE-IN-CASE` | 是 | 0 | `attrs` | — |
| 18 | `management.CopyFeatures` | 否 | `NONE-IN-CASE` | 是 | 0 | `polys_a` | — |
| 19 | `management.MultipartToSinglepart` | 否 | `NONE-IN-CASE` | 是 | 0 | `polys_a` | — |
| 20 | `conversion.ExportFeatures` | 否 | `NONE-IN-CASE` | 是 | 0 | `polys_a` | — |
| 21 | `conversion.TableToTable` | 否 | `NONE-IN-CASE` | 是 | 0 | `attrs` | — |
| 22 | `conversion.RasterToPolygon` | 否 | `NONE-IN-CASE` | 是 | 2 | `classes` | — |
| 23 | `conversion.RasterToPoint` | 否 | `NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED` | 是 | 2 | `classes` | — |
| 24 | `conversion.PolygonToRaster` | 否 | `NONE-IN-CASE` | 是 | 0 | `polys_a` | — |
| 25 | `conversion.PointToRaster` | 否 | `NONE-IN-CASE` | 是 | 1 | `points` | — |
| 26 | `conversion.PolylineToRaster` | 否 | `NONE-IN-CASE` | 是 | 1 | `lines` | — |
| 27 | `conversion.FeatureToRaster` | 否 | `NONE-IN-CASE` | 是 | 0 | `polys_a` | — |
| 28 | `conversion.ASCIIToRaster` | 否 | `NOT-VERIFIED-NO-DATA` | 是 | 0 | `classesAsc` | — |
| 29 | `conversion.RasterToASCII` | 否 | `NONE-IN-CASE` | 是 | 0 | `dem` | — |
| 30 | `conversion.FeaturesToJSON` | 否 | `NONE-IN-CASE` | 是 | 0 | `polys_a` | — |
| 31 | `conversion.JSONToFeatures` | 否 | `NONE-IN-CASE` | 是 | 0 | `featuresJson` | — |
| 32 | `sa.Slope` | 否 | `NONE-IN-CASE` | 是 | 0 | `dem` | — |
| 33 | `sa.Aspect` | 否 | `NONE-IN-CASE` | 是 | 0 | `dem` | — |
| 34 | `sa.Hillshade` | 否 | `NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED` | 是 | 1 | `slope` | — |
| 35 | `sa.Contour` | 否 | `NONE-IN-CASE` | 是 | 0 | `dem` | — |
| 36 | `sa.Curvature` | 否 | `NONE-IN-CASE` | 是 | 0 | `dem` | — |
| 37 | `sa.Fill` | 否 | `NONE-IN-CASE` | 是 | 0 | `dem` | — |
| 38 | `sa.FlowDirection` | 否 | `NONE-IN-CASE` | 是 | 1 | `dem`、`flowdir` | — |
| 39 | `sa.FlowAccumulation` | 否 | `NONE-IN-CASE` | 是 | 2 | `flowdir` | — |
| 40 | `sa.Watershed` | 否 | `NONE-IN-CASE` | 是 | 3 | `flowdir`、`points` | — |
| 41 | `sa.SnapPourPoint` | 否 | `NONE-IN-CASE` | 是 | 3 | `flowacc`、`points` | — |
| 42 | `sa.ZonalStatistics` | 否 | `NONE-IN-CASE` | 是 | 2 | `classes`、`dem` | — |
| 43 | `sa.ZonalStatisticsAsTable` | 否 | `NONE-IN-CASE` | 是 | 2 | `classes`、`dem` | — |
| 44 | `sa.Reclassify` | 否 | `NONE-IN-CASE` | 是 | 2 | `classes`、`remap` | — |
| 45 | `sa.ExtractByMask` | 否 | `NONE-IN-CASE` | 是 | 0 | `dem`、`polys_a` | — |
| 46 | `sa.ExtractValuesToPoints` | 否 | `NONE-IN-CASE` | 是 | 0 | `dem`、`points` | — |
| 47 | `sa.Resample` | 否 | `NOT-VERIFIED-NO-DATA` | 是 | 0 | `dem` | — |
| 48 | `sa.Con` | 否 | `NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED` | 是 | 1 | `classes`、`dem` | — |
| 49 | `sa.CellStatistics` | 否 | `NONE-IN-CASE` | 是 | 0 | `classes`、`dem` | — |
| 50 | `sa.FocalStatistics` | 否 | `NONE-IN-CASE` | 是 | 0 | `dem` | — |
| 51 | `sa.RasterCalculator` | 否 | `NONE-IN-CASE` | 是 | 1 | — | — |
| 52 | `management.AddJoin` | 是 | `NONE-IN-CASE` | 是 | 1 | `events`、`polys_a` | — |
| 53 | `management.RemoveJoin` | 是 | `NONE-IN-CASE` | 是 | 0 | `polys_a` | — |

## 闭环算术（v3）

- 输入侧：`52 声明级可执行 ＋ 1 仍不可 ＝ 53`；语义钉位 `37` 处；未引用池件＝阴性对照 1 件（依设计）。
- 准入侧：`0 VERIFIED ＋ 3 EXEC-SUCCEEDED-ADMISSION-UNRULED ＋ 3 NO-DATA ＋ 47 NONE-IN-CASE ＝ 53`（与 v1／v2／G-271 在案值逐字相同，本段未新增任何执行证据）。
- 全部结论标注 **合成输入基准**。

