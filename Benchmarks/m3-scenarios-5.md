# M3 五场景定义（原 30 场景闭合）— PASS CANDIDATE

本批新定义的五条场景全部取自能力目录 §6 的 S01–S30 基础段，用于把 M1／M2 之后**唯一未定义的残段补齐**：定义后基础 30 场景全部有冻结判据（6 携自 M1、19 携自 M2、5 本批新定）。M1／M2 面字节不改，本批只按 ID 引用；模板侧不再新增（基础 20 模板已由 M1 六件＋M2 十四件覆盖），故 M3 的实质在工具面判据、能力解锁面、现代格式第一轮与暂缓登记。

| 场景 | 模板（来源面） | M3 族 | 工具面（现役/候选） | 判据 | GP 前置 | 工作包 | 输入资格状态 |
|---|---|---|---|---|---|---|---|
| S09 设施最短路线 / shortest-route service to facilities | TP04（M2 携入） | network-accessibility | 10 件（候选 calculate_service_areas, solve_routes） | GBC-001, GEO-001, NET-001, NET-002, NUM-001, OUT-001, PRM-001 | 缺口 Network Analyst route solve, Network Analyst service area；已覆盖 无 | Z01–Z08, Z10, Z12 | PENDING-DOMAIN-BATCH |
| S14 流域与汇流分析 / watershed and flow analysis | TS07（M2 携入） | terrain-hydrology | 11 件（候选 无） | ALN-001, GEO-001, HYD-001, OUT-001, PRM-001, RFL-001, TXT-001 | 缺口 无；已覆盖 无 | Z01–Z08, Z10, Z12 | PARTIAL-RUNTIME-NOT-VERIFIED |
| S17 点位像元采样 / point cell value sampling | TS01（M1 携入） | sample-extraction | 10 件（候选 无） | GEO-001, LNG-001, NUM-001, OUT-001, PRM-001, RAS-003 | 缺口 无；已覆盖 sa.ExtractValuesToPoints | Z01–Z08, Z09, Z12 | NOT-REQUIRED |
| S24 固定分类时间对比 / fixed-classification time comparison | TS04（M2 携入） | class-time-comparison | 11 件（候选 无） | ALN-001, NUM-001, OUT-001, RAS-003, RAS-006, SCI-001, SRC-001 | 缺口 无；已覆盖 sa.CellStatistics, sa.Con, sa.Reclassify | Z01–Z08, Z11, Z12 | PARTIAL-RUNTIME-NOT-VERIFIED |
| S25 GIS语义素材自动导入PS / semantic asset handover to the composition side | TP10（M2 携入） | design-asset-handover | 10 件（候选 无） | DEP-001, LNG-001, OUT-001, PKG-001, PRM-001, PRT-001, PSG-001 | 缺口 无；已覆盖 无 | Z01–Z08, Z11, Z12 | OWNED-ELSEWHERE-NOT-CALIBRATED |

每行同样给 normal／boundary／invalid 三档档案：合格正常与边界档案须自动完成，异常档案须在写出前安全拒绝并给出原因。凡判据绑到尚未派单实现批的候选件，一律标 `PENDING-M3-BATCH-NOT-DISPATCHED-NOT-VERIFIED`；凡受白名单 53 与宿主许可前置约束者，**阻塞态即今日正解**，不写成可达成的通过判据，也不据通用执行器推断已验算法（对标书 §2 第三本账）。

`TestFixtures` 各件仅按只读结构引用（指纹见机读行），不建立业务语义或黄金制图答案；TS08 黄金输入继续按 G-205 记 `NOT-AVAILABLE-PENDING-USER`，本面不以任何非合格栅格充数。
