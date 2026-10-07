# GP 白名单 53 件评测闭环台账 · D-117 Phase A（零执行）

- 工单＝`D-117 (G-268 2026-10-04 00:1x ACTIVE-D117)`；派发依据＝G-267 R-6；四查＝G-268。
- 名册来源＝`Config/gp-whitelist.json`（`F355705BF2C44459…`／30,634 B／**53** 条，destructive **5**）——评测对象名册 ≡ 该 53 条（G-268 四查③）。
- 输入基准＝合成语料 revision-3（`D283D4CF98B89037…`／78 件／450,414 B）⇒ **一切结论标注「合成输入基准」**。
- Phase B＝**STOP-WAIT（本批未开窗、未调用任何工具面）**；本批未开窗、未发任何 `tools/call`、未做 GP 调用。

## 一、闭环算术（逐态计数，零缺口）

- `0 VERIFIED-EVIDENCED + 3 EXEC-SUCCEEDED-ADMISSION-UNRULED + 3 NOT-VERIFIED-NO-DATA + 47 NONE-IN-CASE = 53`
- 实证 `VERIFIED-EVIDENCED`＝**0**。原因＝G-267 R-1 终裁 N=0；本批禁止自造实证 VERIFIED（X20 先例）。
- 未裁态词元＝`NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED`（工单四族无『执行成功而准入未裁』态；本批以 d117CalibrationChoice 登记该词元并挂请裁项 3）

## 二、逐件台账（53 行）

| # | 件名 | destructive | 态（evalStatus） | 工单四族映射 | 在案执行证据 | 反查异常 | 语料类型兼容载体数 |
|---|---|---|---|---|---|---|---|
| 1 | `analysis.Buffer` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 27 |
| 2 | `analysis.Clip` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 3 | `analysis.Erase` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 4 | `analysis.Union` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 5 | `analysis.Intersect` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 6 | `analysis.Identity` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 7 | `analysis.SpatialJoin` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 8 | `analysis.Near` | 是 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 27 |
| 9 | `analysis.Statistics` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 10 | `analysis.Frequency` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 11 | `analysis.TabulateArea` | 否 | `NOT-VERIFIED-NO-DATA` | NOT-VERIFIED-NO-INPUT | Docs/_gatekeeper/outbox/h-receipt-d116.md:71、Docs/_gatekeeper/outbox/h-receipt-d116.md:417 | analysis.TabulateArea`（装机 `TabulateArea` 只在 **`sa`**） | 17 |
| 12 | `analysis.TabulateIntersection` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 27 |
| 13 | `management.Dissolve` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 27 |
| 14 | `management.Merge` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 15 | `management.Project` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 16 | `management.RepairGeometry` | 是 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 17 | `management.CalculateField` | 是 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 14 |
| 18 | `management.CopyFeatures` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 19 | `management.MultipartToSinglepart` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 20 | `conversion.ExportFeatures` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 21 | `conversion.TableToTable` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 14 |
| 22 | `conversion.RasterToPolygon` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 10 |
| 23 | `conversion.RasterToPoint` | 否 | `NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | Docs/_gatekeeper/outbox/h-receipt-d116.md:171、Docs/_gatekeeper/outbox/h-receipt-d116.md:407、Docs/_gatekeeper/outbox/h-receipt-d116.md:414、Docs/_gatekeeper/outbox/h-receipt-d116.md:416、Docs/_gatekeeper/outbox/h-receipt-d116.md:522 | — | 10 |
| 24 | `conversion.PolygonToRaster` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 25 | `conversion.PointToRaster` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 26 | `conversion.PolylineToRaster` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 27 | `conversion.FeatureToRaster` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 28 | `conversion.ASCIIToRaster` | 否 | `NOT-VERIFIED-NO-DATA` | NOT-VERIFIED-NO-INPUT | Docs/_gatekeeper/outbox/h-receipt-d116.md:170、Docs/_gatekeeper/outbox/h-receipt-d116.md:312、Docs/_gatekeeper/outbox/h-receipt-d116.md:354 | — | 0 |
| 29 | `conversion.RasterToASCII` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 10 |
| 30 | `conversion.FeaturesToJSON` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 31 | `conversion.JSONToFeatures` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 0 |
| 32 | `sa.Slope` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 10 |
| 33 | `sa.Aspect` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 10 |
| 34 | `sa.Hillshade` | 否 | `NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | Docs/_gatekeeper/outbox/h-receipt-d116.md:54、Docs/_gatekeeper/outbox/h-receipt-d116.md:317、Docs/_gatekeeper/outbox/h-receipt-d116.md:461、Docs/_gatekeeper/outbox/h-receipt-d116.md:461、Docs/_gatekeeper/outbox/h-receipt-d116.md:469 | 装机实名为 **`HillShade`** | 10 |
| 35 | `sa.Contour` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 10 |
| 36 | `sa.Curvature` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 10 |
| 37 | `sa.Fill` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 10 |
| 38 | `sa.FlowDirection` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 10 |
| 39 | `sa.FlowAccumulation` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 10 |
| 40 | `sa.Watershed` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 41 | `sa.SnapPourPoint` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 42 | `sa.ZonalStatistics` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 43 | `sa.ZonalStatisticsAsTable` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 44 | `sa.Reclassify` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 10 |
| 45 | `sa.ExtractByMask` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 46 | `sa.ExtractValuesToPoints` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 27 |
| 47 | `sa.Resample` | 否 | `NOT-VERIFIED-NO-DATA` | NOT-VERIFIED-NO-INPUT | Docs/_gatekeeper/outbox/h-receipt-d116.md:71、Docs/_gatekeeper/outbox/h-receipt-d116.md:522 | sa.Resample`（装机 `Resample` 只在 **`management`**） | 10 |
| 48 | `sa.Con` | 否 | `NOT-VERIFIED-EXEC-SUCCEEDED-ADMISSION-UNRULED` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | Docs/_gatekeeper/outbox/h-receipt-d116.md:64、Docs/_gatekeeper/outbox/h-receipt-d116.md:317、Docs/_gatekeeper/outbox/h-receipt-d116.md:415、Docs/_gatekeeper/outbox/h-receipt-d116.md:416 | 装机实测件名为 **`Con_`**（尾下划线） | 10 |
| 49 | `sa.CellStatistics` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 24 |
| 50 | `sa.FocalStatistics` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 10 |
| 51 | `sa.RasterCalculator` | 否 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 0 |
| 52 | `management.AddJoin` | 是 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |
| 53 | `management.RemoveJoin` | 是 | `NONE-IN-CASE` | NOT-VERIFIED-NO-EXECUTION-EVIDENCE | — | — | 17 |

## 三、在案证据逐条（可复核行号引用）

- **`analysis.TabulateArea`**
  - `ANOMALY-ALIAS-BOX` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:71`「`analysis.TabulateArea`（装机 `TabulateArea` 只在 **`sa`**）」——alias 与装机工具箱归属不符
  - `ATTEMPT-GP-ERROR` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:417`「`unknown GP error`（TabulateArea/Resample）」——数据面失败，非准入失败
- **`conversion.ASCIIToRaster`**
  - `GATE-SIGNATURE` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:170`「INVALID_ARGUMENT: Unknown parameter 'out_raster' for 'conversion.ASCIIToRaster' (not in the whitelist signature)」——产品按白名单条目声明签名严格校验参数名＝该条目契约面在案实证
  - `ATTEMPT-BLOCKED` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:312`「conversion.ASCIIToRaster` → `in_memory\dem_a` 报」——in_memory 承载栅格产出不可行（010152），段 C 不得用于准入判定
  - `ATTEMPT-GP-ERROR` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:354`「ERROR 010328: 符号 , 处或其附近存在语法错误」——ASCII 解析面阻断；G-263 定性＝本席构造输入不适配＋ASCII 解析局限（L462 同源）
- **`conversion.RasterToPoint`**
  - `ATTEMPT-GP-ERROR` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:171`「第二对照件 `conversion.RasterToPoint` 返回真实 GP 错误」——首个控制段确证请求抵达 arcpy（000860）
  - `ATTEMPT-GP-ERROR` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:407`「ERROR 001000: 字段 grid_code 不存在」——执行席臆造可选 raster_field，r5b 去除后成功
  - `ATTEMPT-SUCCESS-DISK` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:414`「conversion.RasterToPoint`（现役 53 控制段）→ `D:\d116-out\f\pts.shp」——真磁盘产出 58,905,100 B＋伴生件
  - `ATTEMPT-AUDIT-SUCCESS` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:416`「`Success=true` **恰 2 行**＝`conversion.RasterToPoint`、`sa.Con`」——产品审计件独立等值，非执行席自述
  - `ATTEMPT-GP-ERROR` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:522`「conversion.RasterToPoint(res1000)` 报 `000865 输入栅格不存在」——下游连带失败（源自 sa.Resample 阻断）
- **`management.Project`**
  - `MENTION-NAMING-ONLY` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:97`「22 件候选集（装机实测点串，节选）」——该提及属候选命名讨论，非本条目执行证据
- **`sa.Con`**
  - `ANOMALY-NAME-FORM` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:64`「装机实测件名为 **`Con_`**（尾下划线）」——§七初判「无同名 .tool」已被本席当场更正撤回
  - `ATTEMPT-BLOCKED` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:317`「含现役 53 内 `sa.Hillshade`/`sa.Con` 对照件」——010818 空格阻断面（后经 G-262 甲面消除）
  - `ATTEMPT-SUCCESS-DISK` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:415`「ESRI Grid `D:\d116-out\r\con1\`」——真产出＋派生 con1c1/con1c2
  - `ATTEMPT-AUDIT-SUCCESS` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:416`「`Success=true` **恰 2 行**＝`conversion.RasterToPoint`、`sa.Con`」——同上审计行
- **`sa.ExtractByMask`**
  - `MENTION-NAMING-ONLY` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:78`「`Mask` 族 7 件为 `ia.CreateBinaryMask`/`sa.ExtractByMask`」——装机族扫描提及，非本条目执行证据
- **`sa.Hillshade`**
  - `ANOMALY-NAME-FORM` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:54`「装机实名为 **`HillShade`**」——复核成立（3d.HillShade＋sa.Hillshade 两处，见 §七 L64）
  - `ATTEMPT-BLOCKED` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:317`「含现役 53 内 `sa.Hillshade`/`sa.Con` 对照件」——010818 空格阻断
  - `ATTEMPT-GP-ERROR` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:461`「ERROR 000864＋010582 输入栅格必须是单波段栅格」——多波段构造不适配（非白名单准入失败）
  - `ATTEMPT-SUCCESS-DISK` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:461`「改 `D:\d116-out\in\dsrc.tif/Band_1` → **执行成功**」——产出 r\hs_micro2，复点 exists＝目录已建
  - `ATTEMPT-AUDIT-SUCCESS` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:469`「现役 53 对照面（O-D116-03）本轮再得一件真产出：`sa.Hillshade`（Band_1）成功落盘」——G-265/G-267 采信段
- **`sa.Resample`**
  - `ANOMALY-ALIAS-BOX` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:71`「`sa.Resample`（装机 `Resample` 只在 **`management`**）」——alias 与装机工具箱归属不符
  - `ATTEMPT-GP-ERROR` ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:522`「**`GEOPROCESSING_ERROR: unknown GP error`**」——live-g9 第一次／live-g10 第二次同码复现，raw＋审计在场＝O-D116-03 第二次复现

## 四、结构性缺口（逐条带出处）

- **folder-workspace-unsupported** ← `.runtime/evolution/v5-f/run-20260930-d103/phase-r/defects-d103.md:58`「workspace must be a file geodatabase ('*.gdb') in this batch; folder and enterprise workspaces are not supported yet」
- **in-memory-carrier-infeasible** ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:312`「ERROR 010152: 对象 IWorkspace 为空」
- **space-in-output-path** ← `Docs/_gatekeeper/outbox/h-receipt-d116.md:317`「ERROR 010818: 输出路径包含空格」
- **n-zero-ruled** ← `Docs/_gatekeeper/GATE-G267-D116-FINAL_RULING.md:24`「X20 终裁＝REJECTED-SEMANTIC-MISMATCH，N＝0」

- ⇒ 结论：现役 53 件在**当前已验收基线**上的可执行输入面＝**0**——语料 78 件系文件级合成件，而 folder 工作区与 `in_memory` 两种承载均已在案证伪；因此 Phase A 只能产出在册闭环台账，任何逐件准入实证都必须经 Phase B 真机窗（另批授权）方成立。

