# Z12 预定标判据（M3 涉及）— PASS CANDIDATE（定义件，非结果）

对应路线图 §10 Z12「持续设计验收」（所有者 M19＋独立专业人员；完成证据「每模板标准/坏例，开发期人工修图不计自动成功」）。M3 把基础 30 场景与基础 20 模板的定标面闭合，首次可对整套基础模板逐件检查「标准例＋坏例」是否齐备，故 Z12 在此定标；评审组织、盲序与专业 panel 属后续执行批，本件只固定「查什么、怎么复算」。

| 判据 | 断言 | 复算办法 | 引用规则 | 严重度 | 阈值来源 |
|---|---|---|---|---|---|
| Z12-C01 | Every template in the base set has one standard example and at least one bad example frozen before any result, and the pair is identified by template id plus revision. | Walk the template definitions across the three faces and assert each base template has a frozen standard row and at least one bad-example row with distinct ids. | WF-001, PRT-001 | major | 继承冻结 v1 目标块（按引用），本批未新增任何数值阈值 |
| Z12-C02 | Development-time manual repair is never counted as an automatic success; a repaired row is booked as regression and a fresh unseen set is required for a generalisation claim. | Compare the repair ledger against the success ledger and assert no row appears in both; assert the generalisation statement cites a different unseen set. | WF-001, GBC-001 | critical | 继承冻结 v1 目标块（按引用），本批未新增任何数值阈值 |
| Z12-C03 | Every acceptance row carries an evidence level (DECLARED, SOURCE VERIFIED, RUNTIME VERIFIED, E2E VERIFIED, INDEPENDENTLY ACCEPTED) or the explicit NOT VERIFIED marker; blocked rows stay in the denominator. | Recount rows by evidence level and assert the sum equals the total row count and that no row is level-less; assert blocked and deferred rows are still counted. | GBC-001, PRM-001 | critical | 继承冻结 v1 目标块（按引用），本批未新增任何数值阈值 |
| Z12-C04 | Reviewer discipline stays as frozen: at least two independent reviewers, five-point scale, mean at least 4, no item below 3, with scientific correctness judged separately and never offset by appearance. | Assert reviewer count and scale equality with the inherited v1 target block; assert a scientific failure cannot be averaged away by a visual pass. | SCI-001, WF-001 | critical | 继承冻结 v1 目标块（按引用），本批未新增任何数值阈值 |
| Z12-C05 | Error expression and error computation are booked on separate rows for every uncertainty-bearing template, so a visualisation pass never closes a computation row. | Assert the two row ids are distinct for TS10 and any uncertainty row; assert the computation row remains NOT VERIFIED when the expression row passes. | OSV-001, SRC-001 | critical | 继承冻结 v1 目标块（按引用），本批未新增任何数值阈值 |

## 校准声明

- 全部判据状态 `PRE_CALIBRATION_DEFINITION_ONLY_NOT_VERIFIED`；无任何一条声称已做评审、已出图或已跑隐藏集。
- Z12-C02/C03 把「开发期人工修图不计自动成功」与「阻塞/延期行留在分母」写成可复算等式，直接对应路线图 §10 与对标书 §2 五本账、§4「保存失败/超时/断线不能事后移出分母」。
- Z12-C05 与 `OSV-001` 同源：误差表达式与误差计算分列两行，前者通过不得使后者转绿。
- 阈值一律按引用继承 v1（最小字号 8 pt、数值精确相等、语义色 token ICC 前 ΔE00=0、叠加位移 0 px、栅格格网完全匹配），本批不改 v1/v2 字节、不重述数值。
