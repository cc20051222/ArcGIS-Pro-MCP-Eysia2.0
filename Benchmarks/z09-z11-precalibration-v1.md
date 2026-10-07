# Z09–Z11 预定标判据草案 — PASS CANDIDATE（定义件，非结果）

对应路线图 §10 的 Z09（风格与批量）、Z10（多用途重排）、Z11（复杂内容保护）与 §9 M2 行退出门。所有判据在看结果前冻结，逐条给出可复算办法；本件不声称任何一次运行、渲染或评测结果。Z01–Z08 主干判据仍在已冻结的 M1 面（比较规则 v1 `WF-001` 与 M1 场景行），此处只引用不重定义。

## Z09 风格与批量 / style and batch

- 所有者（路线图 §10）：M10/M15 (roadmap §10)
- FINAL 完成证据原文要义：显式保存项目风格，十页分类/整饰一致 (roadmap §10 Z09)
- 适用模板（本批新定义）：TP04、TP05、TP06、TP08、TP09、TP10、TP17、TS09
- 延期与限制：Ten-page atlas is a defined profile, not a measured run; project-style artifact custody and the style token set still need a real saved style to bind against.

| 判据 | 断言 | 复算办法 | 引用规则 | 严重度 |
|---|---|---|---|---|
| Z09-C01 | A project style artifact is explicitly saved and identified by hash before any page is produced; a run that styles pages from defaults has no bound style and fails. | Read the saved style artifact path and hash from the case manifest; assert the manifest hash equals the recomputed file hash; assert every page receipt names that style id. | STY-001 | major |
| Z09-C02 | Token equality: font ladder, colour and class scheme, margin set, legend layout, units, north-arrow and scale-bar style equal the saved style on every page. | For each page compare the reported token values with the saved style tokens; assert exact equality, no tolerance. | STY-001、TXT-001、SCI-001 | major |
| Z09-C03 | Ten-page atlas consistency: page count, page order, per-page class set, legend entry order, units and source line identical where the brief requires it. | Assert page count equals the frozen expected count; assert cross-page set equality of the listed fields. | BAT-001、NUM-001 | major |
| Z09-C04 | One data fingerprint governs the whole batch; a fingerprint change re-validates all pages inside one job rather than patching single pages. | Assert one fingerprint value in every page receipt; after a fingerprint change assert a job-level re-validation record covering all pages. | BAT-001、DEP-001 | critical |
| Z09-C05 | Batch budget discipline: attempts, cancellations and refusals stay in the applicable denominator and per-page candidate/preview/repair budgets are not exceeded. | Compare per-page counters against the frozen WF-001 budgets and recompute the denominator from the run log. | WF-001、PRM-001 | critical |

## Z10 多用途重排 / multi-purpose reflow

- 所有者（路线图 §10）：M10/M14/M16 (roadmap §10)
- FINAL 完成证据原文要义：横竖/打印/论文/PPT/长图规格可验证 (roadmap §10 Z10)
- 适用模板（本批新定义）：TP07、TP09、TP10、TS04、TS07、TS09、TS10、TS15
- 延期与限制：The six media specifications are named by FINAL; per-specification render evidence is a future execution batch, and the PS-side media export surface depends on the PS half-zone line (D-091) which is outside this line's surface.

| 判据 | 断言 | 复算办法 | 引用规则 | 严重度 |
|---|---|---|---|---|
| Z10-C01 | Every declared media profile is separately verifiable: portrait, landscape, print A4/A3 at 300 DPI, paper, PPT 16:9, long image. | For each profile assert the deliverable exists, opens, and matches its manifest hash, size and type. | RFL-001、OUT-001 | major |
| Z10-C02 | Semantic-slot completeness after reflow: no required slot of the template is dropped in any profile. | Diff the reported slot list per profile against the template slot list; assert set equality. | RFL-001、LAY-001 | major |
| Z10-C03 | Numeric invariance across media: numbers, units and category labels equal across profiles at canonical decimal precision. | Extract the numeric and label sets per profile and assert exact cross-profile equality. | RFL-001、NUM-001、SRC-001 | critical |
| Z10-C04 | Typography floor under reflow: no clipped, truncated or substituted text and no rendered size below the frozen font floor inherited from comparison rules v1. | Assert each profile's reported minimum rendered text size >= the v1 floor and that every required semantic string appears intact. | RFL-001、TXT-001 | major |
| Z10-C05 | Legend and source retention: legend entries, scale bar, north arrow where applicable and the source note survive in every profile, including the long-image one. | Assert presence and value equality of the listed elements per profile against the print profile reference. | RFL-001、LAY-001、SCI-001 | major |
| Z10-C06 | Geometry may change, facts may not: reflow differences are permitted only in placement and never in class assignment, NoData handling or uncertainty meaning. | Compare class sets, NoData semantics and uncertainty method notes across profiles; assert equality and flag any change as a scientific-protection failure. | SCI-001、RFL-001、GEO-001 | critical |

## Z11 复杂内容保护 / complex content protection

- 所有者（路线图 §10）：M13/M15/M16 (roadmap §10)
- FINAL 完成证据原文要义：密集文字、外部修改、新版生成；细粒度合并仅经验证范围 (roadmap §10 Z11)
- 适用模板（本批新定义）：TP10、TS03、TS05、TS06、TS10、TS15
- 延期与限制：Fine-grained merge remains limited to an explicitly verified scope; FINAL does not guarantee arbitrary manual-edit retention in the first version, so no criterion here claims it.

| 判据 | 断言 | 复算办法 | 引用规则 | 严重度 |
|---|---|---|---|---|
| Z11-C01 | Dense text integrity: long mixed-language narrative and annotation blocks retain every required semantic string with no clipping after composition. | Assert the extracted string set equals the frozen expected set per page; assert no truncation flag and the font floor holds. | PRT-001、TXT-001 | major |
| Z11-C02 | External original protection: the recomputed hash of a user original or external master is identical before and after the job. | Hash the original before and after and assert equality; assert the job wrote only to a new revision path. | PRT-001、OUT-001 | critical |
| Z11-C03 | New-version generation: output is produced as a new revision, never by overwriting the original artifact. | Assert the output path is a new revision directory and that any overwrite attempt is refused with the pre-existing OUTPUT_EXISTS gate. | PRT-001、RPT-001、OUT-001 | critical |
| Z11-C04 | Fine-grained merge is permitted only inside an explicitly verified scope; an unverified scope generates a new version or refuses and names the scope. | For the verified case assert the merge scope list is non-empty and stated; for the unverified case assert a refusal whose reason names the scope. | PRT-001、DEP-001 | critical |
| Z11-C05 | Design-intent retention under data swap: class scheme, units, legend order, source note and style binding survive while data values update. | Compare design tokens before and after the swap and assert equality; assert data values changed and both facts are disclosed in the receipt. | STY-001、BAT-001、NUM-001 | major |
| Z11-C06 | Dependency-aware refresh: a data change resolves the dependency graph, re-runs only the affected chain, and reports unresolved or broken links instead of hiding them. | Assert the dependency set equals the frozen manifest for the changed root; assert broken links are counted and named in the receipt. | DEP-001、LNG-001、PRT-001 | major |

## 校准声明

- 十条以上判据的阈值一律按引用继承已冻结的 v1 值（最小字号 8 pt、栅格格网完全匹配、数值精确相等、语义色 token ICC 前 ΔE00=0、叠加位移 0 px），本批未改 v1 字节、未新增阈值数值。
- Z10 的六种媒介规格（横竖/打印/论文/PPT/长图）系 FINAL §10 具名；逐规格渲染证据属未来执行批，且 PS 侧导出面属 L4（D-091），不在本线面内。
- Z11 的「细粒度合并仅经验证范围」按 FINAL 原文保留为限制条件；本批不声称首版可保留任意人工编辑。
- 全部判据当前状态：`PRE_CALIBRATION_DEFINITION_ONLY_NOT_VERIFIED`。
