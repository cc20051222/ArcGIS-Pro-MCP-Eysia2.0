# M2 模板/场景映射表（D-089 产出 ①）— PASS CANDIDATE

逐项映射 场景 ↔ 模板 ↔ 工具面 ↔ 判据，格式对齐 D-085 名义集映射表；机读同行见 `m2-scenario-template-map-v1.jsonl`。

## 计数口径（消歧，供零采信复算）

- 本批新定义模板 **16**：TP04、TP05、TP06、TP07、TP08、TP09、TP10、TP17、TS03、TS04、TS05、TS06、TS07、TS09、TS10、TS15
- 携自 M1 冻结面（仅按 ID 引用）：**6**：TP01、TP02、TP03、TS01、TS02、TS08；M2 面模板合计 **22**（工单下限 16）
- 本批新定义场景 **22**：S01、S02、S03、S04、S05、S06、S08、S10、S11、S12、S13、S15、S16、S21、S26、S27、S28、S29、S30、S46、S52、S60
- 携自 M1 冻结面：**6**：S19、S20、S07、S23、S22、S18；M2 面场景合计 **28**（工单下限 18）
- 延期模板 **14**：TP11、TP12、TP13、TP14、TP15、TP16、TP18、TS11、TS12、TS13、TS14、TS16、TS17、TS18；算术自校 16 + 6 + 14 = 36
- 延期场景 **32**；算术自校 22 + 6 + 32 = 60

## 逐项映射

| 场景 | 模板 | 工具面件数 | 在途工具（D-088） | 判据引用 | 工作包 | 输入资格 |
|---|---|---|---|---|---|---|
| S01 | TP08 | 14 | check_topology_rules, get_dataset_lineage, validate_geometries | LNG-001, NUM-001, OUT-001, PRM-001, QLT-001, RPT-001 | Z01–Z08 + Z09 | NOT-REQUIRED |
| S02 | TP05 | 10 | get_dataset_lineage, inspect_raster_alignment, list_geographic_transformations | ALN-001, GEO-001, LNG-001, OUT-001, PRM-001, TRF-001 | Z01–Z08 + Z09 | NOT-REQUIRED |
| S03 | TP06 | 11 | compare_datasets, validate_geometries | CMP-001, DUP-001, NUM-001, QLT-001, RPT-001 | Z01–Z08 + Z09 | NOT-REQUIRED |
| S04 | TP17 | 10 | compare_schemas, validate_field_constraints | CMP-002, FLD-001, NUM-001, PRM-001, RPT-001, SCI-001 | Z01–Z08 + Z09 | NOT-REQUIRED |
| S05 | TS03 | 8 | compare_datasets, compare_schemas, get_dataset_lineage, trace_dataset_dependencies | CMP-001, CMP-002, DEP-001, LNG-001, NUM-001, PRT-001, RPT-001 | Z01–Z08 + Z11 | NOT-REQUIRED |
| S06 | TP10 | 9 | get_dataset_lineage, trace_dataset_dependencies | DEP-001, LNG-001, OUT-001, PRM-001, PRT-001 | Z01–Z08 + Z11 | NOT-REQUIRED |
| S08 | TP04 | 13 | get_dataset_lineage | BAT-001, GEO-001, LNG-001, NUM-001, OUT-001, PRM-001 | Z01–Z08 + Z09 | PENDING-DOMAIN-BATCH |
| S10 | TP06 | 11 | compare_datasets, validate_geometries | CMP-001, GEO-001, NUM-001, QLT-001, RPT-001, SCI-001 | Z01–Z08 + Z09 | NOT-REQUIRED |
| S11 | TS05 | 9 | compare_datasets, get_dataset_lineage | LNG-001, NUM-001, OUT-001, SCI-001, SRC-001 | Z01–Z08 + Z11 | PENDING-DOMAIN-BATCH |
| S12 | TS06 | 10 | get_dataset_lineage, validate_field_constraints | LNG-001, NUM-001, OUT-001, PRM-001, SCI-001, SRC-001 | Z01–Z08 + Z11 | PENDING-DOMAIN-BATCH |
| S13 | TS07 | 11 | inspect_raster_alignment | ALN-001, GEO-001, OUT-001, RFL-001, TXT-001 | Z01–Z08 + Z10 | PENDING-DOMAIN-BATCH |
| S15 | TP09 | 11 | — | BAT-001, NUM-001, OUT-001, SCI-001, STY-001, WF-001 | Z01–Z08 + Z09, Z10 | NOT-REQUIRED |
| S16 | TS04 | 11 | compare_datasets, inspect_raster_alignment | ALN-001, CMP-001, NUM-001, OUT-001, SCI-001, SRC-001 | Z01–Z08 + Z10 | NOT-REQUIRED |
| S21 | TP07 | 9 | compare_datasets, compare_schemas | CMP-001, CMP-002, NUM-001, RFL-001, RPT-001, SRC-001 | Z01–Z08 + Z10 | NOT-REQUIRED |
| S26 | TP10 | 12 | — | NUM-001, OUT-001, SCI-001, SRC-001, STY-001, TXT-001, WF-001 | Z01–Z08 + Z09, Z10, Z11 | NOT-REQUIRED |
| S27 | TS09 | 10 | — | BAT-001, NUM-001, OUT-001, SCI-001, STY-001, TXT-001 | Z01–Z08 + Z09, Z10 | NOT-REQUIRED |
| S28 | TP09 | 8 | — | LAY-001, NUM-001, OUT-001, PRM-001, RFL-001, TXT-001 | Z01–Z08 + Z10 | NOT-REQUIRED |
| S29 | TS03 | 10 | compare_datasets, get_dataset_lineage, trace_dataset_dependencies | CMP-001, DEP-001, LNG-001, OUT-001, PRT-001, STY-001 | Z01–Z08 + Z11 | NOT-REQUIRED |
| S30 | TP10 | 10 | trace_dataset_dependencies | DEP-001, OUT-001, PRM-001, PRT-001, SCI-001, WF-001 | Z01–Z08 + Z11 | NOT-REQUIRED |
| S46 | TS15 | 9 | compare_datasets | CMP-001, NUM-001, OUT-001, SCI-001, SRC-001, TXT-001 | Z01–Z08 + Z10, Z11 | PENDING-DOMAIN-BATCH |
| S52 | TS10 | 11 | compare_datasets, get_dataset_lineage | LNG-001, NUM-001, OUT-001, RFL-001, SCI-001, SRC-001 | Z01–Z08 + Z10, Z11 | PENDING-DOMAIN-BATCH |
| S60 | TP10 | 9 | compare_datasets, get_dataset_lineage | NUM-001, OUT-001, PRM-001, RFL-001, SRC-001, TXT-001 | Z01–Z08 + Z10, Z11 | PENDING-DOMAIN-BATCH |
| S19 | TP01 | （见冻结 M1 面） | — | v1 八条 | Z01–Z08 | 冻结 M1 |
| S20 | TP02 | （见冻结 M1 面） | — | v1 八条 | Z01–Z08 | 冻结 M1 |
| S07 | TP03 | （见冻结 M1 面） | — | v1 八条 | Z01–Z08 | 冻结 M1 |
| S23 | TS01 | （见冻结 M1 面） | — | v1 八条 | Z01–Z08 | 冻结 M1 |
| S22 | TS02 | （见冻结 M1 面） | — | v1 八条 | Z01–Z08 | 冻结 M1 |
| S18 | TS08 | （见冻结 M1 面） | — | v1 八条 | Z01–Z08 | `NOT-AVAILABLE-PENDING-USER`（G-205） |

## 与 FINAL 名义集的差异

- 标准图 108 = 36 模板 × 3 冻结输入：本批定义 16 模板 × 3 档案 = 48 条档案定义，加上 M1 的 18 条，共 66 条档案面；**档案定义不等于已产出标准图**（异常档案预期的是安全拒绝，且正向冻结输入与黄金产出仍需逐例指认），不声称任何渲染成果。
- 压力 ≥40：FINAL 给具名维度与最少数，未给 40 行稳定编号；本批新增 16 条本地 `ST-M2-*`，与 M1 的 12 条 `ST-M1-*` 合计 28 条本地定义，其余至少 12 条仍待后续批圈定，本地 ID 不冒充 FINAL 标识。
- 核心控制任务 24：本批定义 22 个 `CORE-M2-*`（每个至少 3 轮的定义级计划），与 M1 的 6 个 `CORE-M1-*` 合计 28 个定义行；**未跑任何一轮**。
- 隐藏集 ≥12：本批新增 32 个隐藏候选 ID（16 模板 × 2），仅含 ID 与判据引用，无隐藏内容或黄金路径；隐藏集充分性、保管人隔离与新领域覆盖仍 NOT VERIFIED。
- 模板 `analysisInputPolicy = qualified-frozen-input` 的行（TP04/TS05/TS06/TS07/TS10）不声称相应领域算法可用；延期能力逐件点名。
- 九件 P1/P2 工具与 `reportPath` 条件写面均属 L1（D-088）在途，本批判据为定标定义，非实现证据；错误码 33、白名单 53、名册 285、170 工具在本批零改动（交付时现场复算）。
