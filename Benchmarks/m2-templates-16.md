# M2 十六模板定义 — PASS CANDIDATE

模板取自能力目录 §7 的 TP/TS 既有编号：本批新定义 16 件（TP04 TP05 TP06 TP07 TP08 TP09 TP10 TP17 / TS03 TS04 TS05 TS06 TS07 TS09 TS10 TS15），M1 已冻结的 6 件（TP01 TP02 TP03 TS01 TS02 TS08）在本面只按 ID 引用、不改字节。圈定为 D-089 校准选择（FINAL §9 M2 只给「至少 16 模板/18 场景」与 Z09–Z11、批量页、项目风格、多媒介、同源报告 PPTX 的要求，未逐名指定），逐行 `selectionBasis` 可回溯。

| 模板 | 域 | M2 支柱 | 输入政策 | 场景 | 判据 |
|---|---|---|---|---|---|
| TP04 真实路网可达性 / real-network accessibility | planning | quality-provenance | qualified-frozen-input（延期：calculate_service_areas (P5, not implemented); solve_routes (P5, not implemented)） | S08 | BAT-001, GEO-001, LNG-001, NUM-001, OUT-001, PRM-001 |
| TP05 生态约束叠加 / ecological constraint overlay | planning | quality-provenance | current-tool-surface | S02 | ALN-001, GEO-001, LNG-001, OUT-001, PRM-001, TRF-001 |
| TP06 规划冲突诊断 / plan conflict diagnosis | planning | quality-provenance | current-tool-surface | S03, S10 | CMP-001, DUP-001, GEO-001, NUM-001, QLT-001, RPT-001, SCI-001 |
| TP07 方案A/B对比 / option A/B comparison | planning | quality-provenance | current-tool-surface | S21 | CMP-001, CMP-002, NUM-001, RFL-001, RPT-001, SRC-001 |
| TP08 指标统计与空间分布 / indicator statistics and spatial distribution | planning | batch-and-style | current-tool-surface | S01 | LNG-001, NUM-001, OUT-001, PRM-001, QLT-001, RPT-001 |
| TP09 分区管控图册 / zonal regulation atlas | planning | batch-and-style | current-tool-surface | S15, S28 | BAT-001, LAY-001, NUM-001, OUT-001, PRM-001, RFL-001, SCI-001, STY-001, TXT-001, WF-001 |
| TP10 项目综合汇报展板 / project summary report board | planning | same-source-report | current-tool-surface | S06, S26, S30, S60 | DEP-001, LNG-001, NUM-001, OUT-001, PRM-001, PRT-001, RFL-001, SCI-001, SRC-001, STY-001, TXT-001, WF-001 |
| TP17 公共服务分组公平性 / public-service grouped fairness | planning | quality-provenance | current-tool-surface | S04 | CMP-002, FLD-001, NUM-001, PRM-001, RPT-001, SCI-001 |
| TS03 变化量与差异分布 / change magnitude and difference distribution | scientific | complex-content-protection | current-tool-surface | S05, S29 | CMP-001, CMP-002, DEP-001, LNG-001, NUM-001, OUT-001, PRT-001, RPT-001, STY-001 |
| TS04 分类转移与对照 / class transition and comparison | scientific | quality-provenance | current-tool-surface | S16 | ALN-001, CMP-001, NUM-001, OUT-001, SCI-001, SRC-001 |
| TS05 空间自相关结果 / spatial autocorrelation results | scientific | quality-provenance | qualified-frozen-input（延期：spatial_autocorrelation (P5, not implemented)） | S11 | LNG-001, NUM-001, OUT-001, SCI-001, SRC-001 |
| TS06 热点显著性分布 / hotspot significance distribution | scientific | quality-provenance | qualified-frozen-input（延期：hotspot_analysis (P5, not implemented)） | S12 | LNG-001, NUM-001, OUT-001, PRM-001, SCI-001, SRC-001 |
| TS07 地形水文多面板 / terrain and hydrology multi-panel | scientific | multi-media-reflow | qualified-frozen-input（延期：raster_reproject (P6, not implemented); zonal_histogram (P6, not implemented)） | S13 | ALN-001, GEO-001, OUT-001, RFL-001, TXT-001 |
| TS09 时间序列小多图 / time-series small multiples | scientific | batch-and-style | current-tool-surface | S27 | BAT-001, NUM-001, OUT-001, SCI-001, STY-001, TXT-001 |
| TS10 不确定性与误差表达 / uncertainty and error expression | scientific | complex-content-protection | qualified-frozen-input（延期：measurement error propagation (X5 domain batch, not implemented)） | S52 | LNG-001, NUM-001, OUT-001, RFL-001, SCI-001, SRC-001 |
| TS15 分类图与混淆矩阵 / classification map and confusion matrix | scientific | same-source-report | current-tool-surface | S46 | CMP-001, NUM-001, OUT-001, SCI-001, SRC-001, TXT-001 |

硬约束沿用已冻结 v1 数值并按引用标注（A4/A3、300 DPI、最小字号 8 pt），本批只增 M2 侧设计要求：样式令牌须来自显式保存且带 hash 的项目风格件（STY-001）；批量页计数/顺序/类别集/图例序/单位/来源在要求一致的范围内跨页相同（BAT-001）；媒介重排只改版面不改事实（RFL-001）；外部母版与用户原件哈希前后相同、只生新版不覆盖、细粒度合并限经验证范围（PRT-001）；报告/PPTX 与地图数字同源同修订（SRC-001）；许可与副作用全部如实计数（PRM-001）。

输入政策为 `qualified-frozen-input` 的模板（TP04、TS05、TS06、TS07、TS10）其分析侧能力属后续领域批，本批只定标设计面并逐件点名延期能力，不声称相应分析算法已完成（对齐路线图 §9 M1 行「不提前声称新增遥感算法已完成」的同类纪律）。
