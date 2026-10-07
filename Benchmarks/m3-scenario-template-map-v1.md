# M3 模板/场景映射表（D-093 产出 ①）— PASS CANDIDATE

逐项映射 场景 ↔ 模板 ↔ 工具面 ↔ 判据，机读同行见 `m3-scenario-template-map-v1.jsonl`；能力解锁面另件 `m3-capability-unlock-v1.jsonl`。

## 计数口径（消歧，供零采信复算）

- 本批新定义场景 **5**：S09、S14、S17、S24、S25（全部属基础 30）
- 携自 M1 冻结面：**6**：S07、S18、S19、S20、S22、S23；携自 M2 冻结面：**22**（其中基础段 19 条，广度段 3 条）
- 基础 30 场景闭合算式：6 + 19 + 5 = 30
- 基础 20 模板闭合算式：6 + 14 + 0 = 20（M3 不新增模板，改为把 M2 冻结的「合格输入资格」升级为工具面判据）
- 目录全集算式：场景 33 定义（M1 6＋M2 22＋M3 5）＋27 延期（M4 广度）= 60；模板 22 定义（M1 6＋M2 16）＋14 延期 = 36
- 压力算式：12（M1）＋16（M2）＋14（M3）＝42 条本地定义 ≥ FINAL 至少 40；本地 ID 不冒充 FINAL 编号，完成度须待执行批测量。
- 隐藏候选：12（M1）＋32（M2）＋5（M3）＝49 条本地候选，仅含 ID 与判据引用，无隐藏内容或黄金路径。

## 逐项映射

| 场景 | 模板 | 来源面 | M3 族 | 工具面件数 | 候选工具 | 判据引用 | 输入资格 |
|---|---|---|---|---|---|---|---|
| S09 | TP04 | M2 | network-accessibility | 10 | calculate_service_areas, solve_routes | GBC-001, GEO-001, NET-001, NET-002, NUM-001, OUT-001, PRM-001 | PENDING-DOMAIN-BATCH |
| S14 | TS07 | M2 | terrain-hydrology | 11 | — | ALN-001, GEO-001, HYD-001, OUT-001, PRM-001, RFL-001, TXT-001 | PARTIAL-RUNTIME-NOT-VERIFIED |
| S17 | TS01 | M1 | sample-extraction | 10 | — | GEO-001, LNG-001, NUM-001, OUT-001, PRM-001, RAS-003 | NOT-REQUIRED |
| S24 | TS04 | M2 | class-time-comparison | 11 | — | ALN-001, NUM-001, OUT-001, RAS-003, RAS-006, SCI-001, SRC-001 | PARTIAL-RUNTIME-NOT-VERIFIED |
| S25 | TP10 | M2 | design-asset-handover | 10 | — | DEP-001, LNG-001, OUT-001, PKG-001, PRM-001, PRT-001, PSG-001 | OWNED-ELSEWHERE-NOT-CALIBRATED |
| S07 | TP03 | M1 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M1 |
| S18 | TS08 | M1 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M1 |
| S19 | TP01 | M1 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M1 |
| S20 | TP02 | M1 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M1 |
| S22 | TS02 | M1 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M1 |
| S23 | TS01 | M1 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M1 |
| S01 | TP08 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S02 | TP05 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S03 | TP06 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S04 | TP17 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S05 | TS03 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S06 | TP10 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S08 | TP04 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S10 | TP06 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S11 | TS05 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S12 | TS06 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S13 | TS07 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S15 | TP09 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S16 | TS04 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S21 | TP07 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S26 | TP10 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S27 | TS09 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S28 | TP09 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S29 | TS03 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S30 | TP10 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S46 | TS15 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S52 | TS10 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |
| S60 | TP10 | M2 | （携自冻结面） | — | — | 见对应冻结面 | CARRIED_FROZEN_M2 |

## 能力解锁面（M2 冻结的合格输入 → M3 工具判据）

| 延期能力引用 | 类型 | 来源行 | M3 规则 | 白名单状态 | 解锁状态 |
|---|---|---|---|---|---|
| build_raster_pyramids (P6) | scenario-input-qualification | S13 | RAS-002 | present=[] gap=['management.BuildRasterPyramids'] | CALIBRATED-AT-M3-DEFINITION |
| calculate_service_areas (P5) | scenario-input-qualification | S08 | NET-001 | present=[] gap=['Network Analyst service area'] | CALIBRATED-AT-M3-DEFINITION |
| generate_tessellation (P5) | scenario-input-qualification | S11 | AGR-001 | present=[] gap=['management.CreateTessellation'] | CALIBRATED-AT-M3-DEFINITION |
| hotspot_analysis (P5) | scenario-input-qualification | S12 | STA-001 | present=[] gap=['stats.HotSpotAnalysis'] | CALIBRATED-AT-M3-DEFINITION |
| image classification and accuracy algorithms (X4/X5 domain batch, not implemented) | scenario-input-qualification | S46 | not covered by an M3 rule | no controlled-GP entry required | STILL-DEFERRED |
| measurement error propagation (X5 domain batch, not implemented) | scenario-input-qualification | S52 | not covered by an M3 rule | no controlled-GP entry required | STILL-DEFERRED |
| polygon_neighbors (P5) | scenario-input-qualification | S11 | AGR-001 | present=[] gap=['analysis.FeatureNeighborhood'] | CALIBRATED-AT-M3-DEFINITION |
| raster_reproject (P6) | scenario-input-qualification | S13 | RAS-001 | present=['management.Project'] gap=['management.ProjectRaster'] | CALIBRATED-AT-M3-DEFINITION |
| report and PPTX internal service (X8, non-tool capability, not implemented) | scenario-input-qualification | S60 | not covered by an M3 rule | no controlled-GP entry required | STILL-DEFERRED |
| solve_routes (P5) | scenario-input-qualification | S08 | NET-002 | present=[] gap=['Network Analyst route solve'] | CALIBRATED-AT-M3-DEFINITION |
| spatial_autocorrelation (P5) | scenario-input-qualification | S11 | STA-001 | present=[] gap=['stats.SpatialAutocorrelation'] | CALIBRATED-AT-M3-DEFINITION |
| zonal_histogram (P6) | scenario-input-qualification | S13 | RAS-004 | present=['sa.ZonalStatisticsAsTable', 'analysis.Frequency', 'analysis.TabulateIntersection'] gap=[] | CALIBRATED-AT-M3-DEFINITION |
| calculate_service_areas (P5, not implemented) | template-input-policy | TP04 | NET-001 | present=[] gap=['Network Analyst service area'] | CALIBRATED-AT-M3-DEFINITION |
| hotspot_analysis (P5, not implemented) | template-input-policy | TS06 | STA-001 | present=[] gap=['stats.HotSpotAnalysis'] | CALIBRATED-AT-M3-DEFINITION |
| measurement error propagation (X5 domain batch, not implemented) | template-input-policy | TS10 | not covered by an M3 rule | no controlled-GP entry required | STILL-DEFERRED |
| raster_reproject (P6, not implemented) | template-input-policy | TS07 | RAS-001 | present=['management.Project'] gap=['management.ProjectRaster'] | CALIBRATED-AT-M3-DEFINITION |
| solve_routes (P5, not implemented) | template-input-policy | TP04 | NET-002 | present=[] gap=['Network Analyst route solve'] | CALIBRATED-AT-M3-DEFINITION |
| spatial_autocorrelation (P5, not implemented) | template-input-policy | TS05 | STA-001 | present=[] gap=['stats.SpatialAutocorrelation'] | CALIBRATED-AT-M3-DEFINITION |
| zonal_histogram (P6, not implemented) | template-input-policy | TS07 | RAS-004 | present=['sa.ZonalStatisticsAsTable', 'analysis.Frequency', 'analysis.TabulateIntersection'] gap=[] | CALIBRATED-AT-M3-DEFINITION |

`CALIBRATED-AT-M3-DEFINITION` 只表示原先冻结为「合格输入」的路径如今有了可复算的工具判据，**不表示工具已存在或已运行**；`STILL-DEFERRED` 逐项点名归属（后续领域批或他线所有权）。

## 暂缓与不做（完成门⑨）

- G-190 在线/发布三件 `discover_remote_datasets`、`import_remote_dataset`、`publish_map_service`：状态 `DEFERRED-G190-NOT-CALIBRATED`，本面不预定标其具体行为，仅登记占位与裁定依据（路线图 §9「三项在线门先裁定」、对标书 §9 独立决策门、`f03b-9` L-17 不实现、`f03b-7` ADR 不得借道旁路）；若终局不获准，须由用户明确移出发行范围并如实标注本地版边界。
- 内部服务两件 `raster_reclassify`／`extract_raster_values`：F03-a 终审不计工具数，本面按受控 GP 路径（`RAS-003`）定标，不称新契约、不增公开名。
- 扩参数一件 `create_layout_map_frame`：F03-a 结论为 `create_layout` 既有可选面，按 `FRM-001` 定参数路径。
- 误差可视化 ≠ 误差计算：按路线图 §9 M3 行，`OSV-001` 把两者分账，任何表达式通过不得闭合计算行（计算行维持 NOT VERIFIED）。
- PS 组稿侧：S25 的组稿半边属他线所有权，本面判据止于 GIS 边界（`PSG-001` 自陈界限），M3 机读件内不出现该侧任何工具名。

## 与 FINAL 名义集的差异

- 标准图 108：本批不新增模板定义，故模板侧档案面零新增；场景侧 5 条各三档档案＝15 条档案定义。档案定义不等于已产出标准图，正向冻结输入与黄金产出仍需逐例指认。
- 核心控制任务：本批 5 个 `CORE-M3-*` × 3 轮＝15 条定义级计划；三面合计 33 个核心定义行，未跑任何一轮。
- 隐藏集：新增 5 条本地候选；充分性、保管人隔离与新领域覆盖仍 NOT VERIFIED。
- 工具计数：本面把 32 候选按 F03-a 口径拆为 29 独立契约＋2 内部服务＋1 扩参数，并复算 88 → 84 公开名减法；候选在**已验收基线 189 名**（G-215，170＋九件＋P3/P4 十件）中逐一核无，故不存在任何已验收实现证据；P5 八件已由 G-215 派为 D-094（在飞未验收），P6/P7a/P7b 尚无派单批，逐件状态见机读行 `implementationStatus`。
- 白名单 53 恒定：本批只做逐名比对与缺口登记，不提出扩项（属用户裁定）；受白名单前置阻塞的候选一律 fail-closed。
