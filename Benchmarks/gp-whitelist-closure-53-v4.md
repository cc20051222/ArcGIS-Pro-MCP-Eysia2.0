# GP 白名单 53 件评测闭环台账 · v4（D-117 收官终版·四查取证面）

- 规范标注＝**合成输入基准**（53/53 逐行在账）；取代 `gp-whitelist-closure-53-v3.jsonl`，v3/v2/v1 三件字节全部未动（`v3RetainedUnchanged: true`）。
- 派单依据＝**G-294 R-3③「L2 收官窗放行」**（M7 PROCESSING→HANDOFF 闭锁＋R-D117 终版评估，证据面＝239 基线）；定性依 G-291（乙）「本阶段不再开真机窗」＝**零执行收官**，非 G-274 R-3 的真机评测窗。
- 四查口径＝G-274（真名/许可/签名/行为）；收官窗纪律逐字＝「**NOT VERIFIED 一律不升格**」。
- 证据基线（生成时现算）＝源码 `97DE7764A7B1A2F9…`/277・注册 **239**≡契约 **239**・装机 `59F376555025052D…`/1,103,697 B・43 条目・白名单 53/`F355705BF2C4…`・错误码 33。
- 真机侧在场证据＝L1/D-119 `.runtime/evolution/v5-f/run-20261005-d119/f4-live/live-summary.json`（3,248 B／`5AF978FB9EC2A1EB…`／14/14 全绿；tools/list 239≡契约・GP 枚举 53≡Config・`zeroRunGeoprocessingCalls=True`）——**只读引用他线在案证据，非本席自测，本席未开窗**。
- 入场复算＝`.runtime/evolution/v5-f/run-20261004-d117/closeout/result-intake-20261005T154630.json` 23/23 全绿（四锚 rc=0×4・16 已验收 gp-* 面零漂移・37 钉位・53 行窗计划全 PENDING）。

## 一、四查列现值账（逐列合计＝53）

| 查项 | 状态分布 | 取证面 | 该状态不证明 |
|---|---|---|---|
| realName | `EVIDENCED-LIVE-WHITELIST-ENUM-239`×53 | Config 名册＋LIVE gpEnumeration＋v3 行 | 宿主 GP 目录可解析性与调用行为 |
| licence | `NOT-VERIFIED-PER-ITEM-ON-239`×53 | HANDOVER:17 历史扩展级 spike＋Config notes | 逐件在 239 基线的许可可达 |
| signature | `DATA-LAYER-EVIDENCED-PENDING-REAL-SIGNATURE`×52・`DATA-LAYER-EVIDENCED-PENDING-REAL-SIGNATURE-1-OPEN-OPTION-SET`×1 | Config parameters＋v3＋v2 钉位＋载体期望＋调用参数面 | arcpy 实参序与选项集的真机实测（O-D048-07） |
| behaviour | `NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED`×3・`NOT-VERIFIED-ZERO-GP-CALLS-EVER-ISSUED`×50 | 窗计划 PENDING＋LIVE 零 GP 调用＋v3 在案证据 | 任何产出/失败码层面的准入 |

- 许可归类合计＝核心 **33** 件（analysis/management/conversion）＋Spatial Analyst **20** 件（sa.*）＝53。
- 参数账合计＝**242**（≡ v2 `parameterCensus.total`），其中产出参数 **49** 个、语义钉位 **37** 键。
- 准入态不变（承 v3・零升格）＝47 NONE-IN-CASE＋3 NOT-VERIFIED-NO-DATA＋3 NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED＝53，其中实证 VERIFIED＝**0**（G-271 R-1「实证 VERIFIED 不得由本批自造」维持）。

## 二、逐件四查台账（53 行）

| # | 件名 | destruct | 准入态（承 v3） | 真名 | 许可 | 签名 | 行为 | 开放项 |
|---|---|---|---|---|---|---|---|---|
| 1 | `analysis.Buffer` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 2 | `analysis.Clip` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 3 | `analysis.Erase` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 4 | `analysis.Union` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 5 | `analysis.Intersect` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 6 | `analysis.Identity` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 7 | `analysis.SpatialJoin` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 8 | `analysis.Near` | 是 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 9 | `analysis.Statistics` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 10 | `analysis.Frequency` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 11 | `analysis.TabulateArea` | 否 | `NOT-VERIFIED-NO-DATA` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 12 | `analysis.TabulateIntersection` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 13 | `management.Dissolve` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 14 | `management.Merge` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 15 | `management.Project` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓-开放选项集 | NOT VERIFIED(零调用) | `transform_method` |
| 16 | `management.RepairGeometry` | 是 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 17 | `management.CalculateField` | 是 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 18 | `management.CopyFeatures` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 19 | `management.MultipartToSinglepart` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 20 | `conversion.ExportFeatures` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 21 | `conversion.TableToTable` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 22 | `conversion.RasterToPolygon` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 23 | `conversion.RasterToPoint` | 否 | `NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(在案未裁) | — |
| 24 | `conversion.PolygonToRaster` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 25 | `conversion.PointToRaster` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 26 | `conversion.PolylineToRaster` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 27 | `conversion.FeatureToRaster` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 28 | `conversion.ASCIIToRaster` | 否 | `NOT-VERIFIED-NO-DATA` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 29 | `conversion.RasterToASCII` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 30 | `conversion.FeaturesToJSON` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 31 | `conversion.JSONToFeatures` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 32 | `sa.Slope` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 33 | `sa.Aspect` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 34 | `sa.Hillshade` | 否 | `NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(在案未裁) | — |
| 35 | `sa.Contour` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 36 | `sa.Curvature` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 37 | `sa.Fill` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 38 | `sa.FlowDirection` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 39 | `sa.FlowAccumulation` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 40 | `sa.Watershed` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 41 | `sa.SnapPourPoint` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 42 | `sa.ZonalStatistics` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 43 | `sa.ZonalStatisticsAsTable` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 44 | `sa.Reclassify` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 45 | `sa.ExtractByMask` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 46 | `sa.ExtractValuesToPoints` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 47 | `sa.Resample` | 否 | `NOT-VERIFIED-NO-DATA` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 48 | `sa.Con` | 否 | `NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(在案未裁) | — |
| 49 | `sa.CellStatistics` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 50 | `sa.FocalStatistics` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 51 | `sa.RasterCalculator` | 否 | `NONE-IN-CASE` | 枚举✓LIVE | spatial-analyst | 数据层✓ | NOT VERIFIED(零调用) | — |
| 52 | `management.AddJoin` | 是 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |
| 53 | `management.RemoveJoin` | 是 | `NONE-IN-CASE` | 枚举✓LIVE | core | 数据层✓ | NOT VERIFIED(零调用) | — |

## 三、收官结论（D-117 终版评估·零执行）

1. **53 件数据层闭环完成**：真名逐件对表且在 239 基线真机枚举面内全命中；签名＝Config 声明与已验收三面（v3・v2 钉位・载体/调用面）逐参数一致；许可归类入册（核心 33／SA 20）但逐件可达性未复测。
2. **行为面一律 NOT VERIFIED**：真机评测窗从未开（G-291（乙）后置→G-294 收官窗放行仍带「NOT VERIFIED 一律不升格」；操作席每轮红线逐字「不启动 Pro/Bridge/MCP、不调用工具面」未解除）。L1/D-119 f4-live 面自证 `zeroRunGeoprocessingCalls=True`，即本结论的在场证据而非反证。
3. **准入账不改**：0 VERIFIED＋3 EXEC-SUCCEEDED-ADMISSION-UNRULED＋3 NO-DATA＋47 NONE-IN-CASE＝53；`management.Project.transform_method` 选项集（第 15 件）依 G-274 R-2(b) 仍 NOT-VERIFIED，为唯一声明级开放项。
4. **开窗条件如实在册**：若要行为面升格，须（i）操作席并轨放行词（G-287 R-8「放行权属用户」），并（ii）按已登记的窗计划 53 行/8 族纯窗/守卫 120 s·280 s/≤9 件逐窗执行，输入依据＝`gp-fixture-inputs-v2`＋`closure-53-v3`＋`gap-v3`（G-276 R-3②）；本席已备好数据层与客户端，未自造放行件。
5. **承载面守恒**：本段仅新增本两面（`.jsonl`＋`.md`）；v1/v2/v3 六件字节不改；四锚不改；无第五锚（M5 终裁属 D-120）；白名单 53／错误码 33／名册 285 零变更；G-197 全段在 D 盘。
