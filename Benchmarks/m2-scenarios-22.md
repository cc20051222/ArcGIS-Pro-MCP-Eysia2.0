# M2 二十二场景定义 — PASS CANDIDATE

场景全部取自能力目录 §6 的 S01–S60 既有编号（含 §10 索引行显式绑定 TS15/TS10/TP10 的 S46、S52、S60），按 §7 与路线图 §9/§10 的 M2 要求映射到本批新定义的十六模板之一。定义件是未来执行基准的判据面，不是当前功能的通过证据；M1 面（含 TS08 与其 6 场景）字节不变，本批不重定义、不复制。

| 场景 | 主模板 | M2 支柱 | 工具面（现役/在途） | 判据 | 工作包 | 输入资格状态 |
|---|---|---|---|---|---|---|
| S01 文件夹资料体检入库 / data-folder check-in | TP08 | quality-provenance | 14 件（在途 check_topology_rules, get_dataset_lineage, validate_geometries） | 6 条（LNG-001, NUM-001, OUT-001, PRM-001, QLT-001, RPT-001） | Z01–Z08, Z09 | NOT-REQUIRED |
| S02 多源坐标统一 / multi-source coordinate unification | TP05 | quality-provenance | 10 件（在途 get_dataset_lineage, inspect_raster_alignment, list_geographic_transformations） | 6 条（ALN-001, GEO-001, LNG-001, OUT-001, PRM-001, TRF-001） | Z01–Z08, Z09 | NOT-REQUIRED |
| S03 属性/几何重复审查 / attribute and geometry duplicate review | TP06 | quality-provenance | 11 件（在途 compare_datasets, validate_geometries） | 5 条（CMP-001, DUP-001, NUM-001, QLT-001, RPT-001） | Z01–Z08, Z09 | NOT-REQUIRED |
| S04 值域与子类型治理 / domain and subtype governance review | TP17 | quality-provenance | 10 件（在途 compare_schemas, validate_field_constraints） | 6 条（CMP-002, FLD-001, NUM-001, PRM-001, RPT-001, SCI-001） | Z01–Z08, Z09 | NOT-REQUIRED |
| S05 数据版本变更审计 / data version change audit | TS03 | complex-content-protection | 8 件（在途 compare_datasets, compare_schemas, get_dataset_lineage, trace_dataset_dependencies） | 7 条（CMP-001, CMP-002, DEP-001, LNG-001, NUM-001, PRT-001, RPT-001） | Z01–Z08, Z11 | NOT-REQUIRED |
| S06 带依赖检查的数据交接包 / dependency-checked data handover package | TP10 | complex-content-protection | 9 件（在途 get_dataset_lineage, trace_dataset_dependencies） | 5 条（DEP-001, LNG-001, OUT-001, PRM-001, PRT-001） | Z01–Z08, Z11 | NOT-REQUIRED |
| S08 真实路网服务区 / real-network service area | TP04 | quality-provenance | 13 件（在途 get_dataset_lineage） | 6 条（BAT-001, GEO-001, LNG-001, NUM-001, OUT-001, PRM-001） | Z01–Z08, Z09 | PENDING-DOMAIN-BATCH |
| S10 规划叠加冲突 / plan overlay conflict | TP06 | quality-provenance | 11 件（在途 compare_datasets, validate_geometries） | 6 条（CMP-001, GEO-001, NUM-001, QLT-001, RPT-001, SCI-001） | Z01–Z08, Z09 | NOT-REQUIRED |
| S11 邻接与格网统计 / adjacency and grid statistics | TS05 | quality-provenance | 9 件（在途 compare_datasets, get_dataset_lineage） | 5 条（LNG-001, NUM-001, OUT-001, SCI-001, SRC-001） | Z01–Z08, Z11 | PENDING-DOMAIN-BATCH |
| S12 空间自相关与热点 / spatial autocorrelation and hotspot | TS06 | quality-provenance | 10 件（在途 get_dataset_lineage, validate_field_constraints） | 6 条（LNG-001, NUM-001, OUT-001, PRM-001, SCI-001, SRC-001） | Z01–Z08, Z11 | PENDING-DOMAIN-BATCH |
| S13 DEM坡度坡向地形专题 / DEM slope and aspect terrain map | TS07 | multi-media-reflow | 11 件（在途 inspect_raster_alignment） | 5 条（ALN-001, GEO-001, OUT-001, RFL-001, TXT-001） | Z01–Z08, Z10 | PENDING-DOMAIN-BATCH |
| S15 分区土地类别统计 / per-zone land class statistics | TP09 | batch-and-style | 11 件（在途 无） | 6 条（BAT-001, NUM-001, OUT-001, SCI-001, STY-001, WF-001） | Z01–Z08, Z09, Z10 | NOT-REQUIRED |
| S16 前后期栅格变化 / two-period raster change | TS04 | quality-provenance | 11 件（在途 compare_datasets, inspect_raster_alignment） | 6 条（ALN-001, CMP-001, NUM-001, OUT-001, SCI-001, SRC-001） | Z01–Z08, Z10 | NOT-REQUIRED |
| S21 方案A/B对照 / option A/B comparison | TP07 | quality-provenance | 9 件（在途 compare_datasets, compare_schemas） | 6 条（CMP-001, CMP-002, NUM-001, RFL-001, RPT-001, SRC-001） | Z01–Z08, Z10 | NOT-REQUIRED |
| S26 规划汇报自动成稿 / planning report auto-compose | TP10 | same-source-report | 12 件（在途 无） | 7 条（NUM-001, OUT-001, SCI-001, SRC-001, STY-001, TXT-001, WF-001） | Z01–Z08, Z09, Z10, Z11 | NOT-REQUIRED |
| S27 科研插图自动成稿 / scientific figure auto-compose | TS09 | batch-and-style | 10 件（在途 无） | 6 条（BAT-001, NUM-001, OUT-001, SCI-001, STY-001, TXT-001） | Z01–Z08, Z09, Z10 | NOT-REQUIRED |
| S28 屏幕/打印双版本交付 / screen and print dual-version delivery | TP09 | multi-media-reflow | 8 件（在途 无） | 6 条（LAY-001, NUM-001, OUT-001, PRM-001, RFL-001, TXT-001） | Z01–Z08, Z10 | NOT-REQUIRED |
| S29 换数据保留设计意图 / data swap retaining design intent | TS03 | complex-content-protection | 10 件（在途 compare_datasets, get_dataset_lineage, trace_dataset_dependencies） | 6 条（CMP-001, DEP-001, LNG-001, OUT-001, PRT-001, STY-001） | Z01–Z08, Z11 | NOT-REQUIRED |
| S30 外部修改保护与交付验收 / external modification protection and delivery acceptance | TP10 | complex-content-protection | 10 件（在途 trace_dataset_dependencies） | 6 条（DEP-001, OUT-001, PRM-001, PRT-001, SCI-001, WF-001） | Z01–Z08, Z11 | NOT-REQUIRED |
| S46 独立分类精度评估 / independent classification accuracy assessment | TS15 | same-source-report | 9 件（在途 compare_datasets） | 6 条（CMP-001, NUM-001, OUT-001, SCI-001, SRC-001, TXT-001） | Z01–Z08, Z10, Z11 | PENDING-DOMAIN-BATCH |
| S52 量测误差传播 / measurement error propagation | TS10 | complex-content-protection | 11 件（在途 compare_datasets, get_dataset_lineage） | 6 条（LNG-001, NUM-001, OUT-001, RFL-001, SCI-001, SRC-001） | Z01–Z08, Z10, Z11 | PENDING-DOMAIN-BATCH |
| S60 同源报告/PPTX自动交付 / same-source report and PPTX delivery | TP10 | same-source-report | 9 件（在途 compare_datasets, get_dataset_lineage） | 6 条（NUM-001, OUT-001, PRM-001, RFL-001, SRC-001, TXT-001） | Z01–Z08, Z10, Z11 | PENDING-DOMAIN-BATCH |

每条场景定义 normal/boundary/invalid 三档输入档案：合格正常与边界档案须自动完成，异常档案须在写出前安全拒绝并给出原因；九件 P1/P2 工具系 L1（D-088）在途，绑定其的判据一律 `PENDING-D088-NOT-ACCEPTED-NOT-VERIFIED`；`workflowCoverage` 含 M1 主干 Z01–Z08 与本批新增支柱包（Z09/Z10/Z11），逐行可见于机读件。

`TestFixtures` 各件仅按只读结构引用（清单指纹见机读行），不建立业务语义或黄金制图答案；TS08 黄金输入继续按 G-205 记 `NOT-AVAILABLE-PENDING-USER`，本批未以任何 `multi.tif` 充数。
