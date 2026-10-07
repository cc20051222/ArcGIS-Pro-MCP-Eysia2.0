# 扩展储备场景转正评估报告 · M4 延伸 — D-099 产出④

> 版本：v0.1（D-099 初版，2026-09-30）
> 承接：`reserve_promotion_assessment.md`（D-095）、`reserve_promotion_assessment_m3.md`（D-098）——**本件是 M4 延伸，不覆盖、不改写前件**。
> 聚焦：M4 广度收官后 deferredRegister **六分类**储备的状态清点与转正前置。

---

## ⚠️ 纪律声明（最高优先级）

1. **评估≠纳入**：储备清点不改变任何定标面；任何能力/场景/工具转正**必须由指挥席裁定**。
2. M1/M2/M3/M4 冻结面字节不改（四锚 `92ECD265…`/`501A7E15…`/`94577E76…`/`F71BF6CD…` 零触碰）。
3. 不自定义判据/阈值；白名单缺口、许可、Bridge、合并未裁定项一律 **fail-closed 到阻塞态**。
4. 跨线（L1 实现/L4 PS）结果不预采信、不代行。

---

## 一、deferredRegister 六分类总览

| # | 分类 | 关联能力/场景 | 当前状态 | 转正权 |
|---|---|---|---|---|
| 1 | **onlinePublishTrio** | 在线发布三件（G-190）；S59 不借用 | DEFERRED-G190-NOT-CALIBRATED | 指挥席＋用户（公开发布红线） |
| 2 | **ts08GoldInput** | TS08 黄金输入（G-205）；S44/47/48 | NOT-AVAILABLE-PENDING-USER | 用户提供合格多波段产品 |
| 3 | **whitelistExpansion** | GP 白名单 53 扩项；网络/3D 缺口 12 名 | BLOCKED（0 of 53 网络求解器） | 用户裁定（安全/许可） |
| 4 | **licenceProbe** | 六类宿主扩展许可探测 | NOT VERIFIED（未启动 Pro） | 真机探测＋用户 |
| 5 | **pythonBridgeReview** | Python Bridge typed 操作（BRG-001）；X03/X26 | 阻塞（审查归属未裁定） | 指挥席＋用户 |
| 6 | **x07x08Merge** | 配置三件并入 set_map_properties（SCN-001） | 合并未决，新名主张阻塞 | 指挥席裁定 |

---

## 二、分类 1 · onlinePublishTrio（G-190 在线发布三件）

- **是什么**：面向在线服务/门户发布的三件能力，FINAL 要求但未在任何面标定。
- **关联**：S59（离线交接）**不借用**其行为；离线包与在线发布严格分开。
- **状态**：`DEFERRED-G190-NOT-CALIBRATED`；无门户连接/凭据/发布验收。
- **转正前置**：① 公开/签名发布属用户红线授权；② 门户地址与认证（凭据不入 query）；③ 独立发布-回滚演练。
- **风险**：把本地离线包当作已发布服务（冒名，G-212 同类）。
- **结论**：维持延期，不预采信。

## 三、分类 2 · ts08GoldInput（G-205 黄金输入）

- **是什么**：TS08 模板所需的合格多波段影像＋公布 QA＋参考掩膜/类图例/模型登记。
- **关联**：S44（质量云影）、S47（分割）、S48（登记推理）均依赖。
- **状态**：`NOT-AVAILABLE-PENDING-USER`；现有 multi.tif 经 G-205 判定**非合格输入/非黄金例**。
- **转正前置**：用户提供带公布 QA 位、参考掩膜、传感器说明的合规产品。
- **风险**：以任意栅格冒充合格遥感输入（M1 已立 TS08-INPUT-QUAL-001 防此）。
- **结论**：数据缺口挂账，不伪造。

## 四、分类 3 · whitelistExpansion（GP 白名单扩项）

- **是什么**：冻结 53 名 GP 白名单之外、M4 广度所需的求解器/3D 工具（缺口 12 名）。
- **现状（零采信复算）**：**0 of 53** 白名单名为 Network Analyst 求解器；M3 引用的 12 个 cited-absent 名仍无条目；9 cited-present＋6 水文名仍在。
- **转正前置**：扩 GP 白名单属**用户裁定**（涉安全面与许可），非执行席可推进；须逐名给工具名/许可/参数面。
- **风险**：由通用执行器推断缺失算法已验（KDA/OSV 纪律明确禁止）。
- **结论**：fail-closed 阻塞；扩项前 BLOCKED 即正解。

## 五、分类 4 · licenceProbe（宿主许可探测）

- **六类扩展**：3D Analyst、Spatial Analyst、Image Analyst、Geostatistical Analyst、Network Analyst、模式挖掘（pattern mining）。
- **状态**：全部 **NOT VERIFIED**——本工作空间未启动 Pro，未做任何 check_extension 实测。
- **转正前置**：真机启动 Pro，逐扩展 check_extension 并留证；许可归属/采购由用户处理。
- **风险**：未探测却在文档暗示许可可用（即伪证）。
- **结论**：如实挂 NOT VERIFIED。

## 六、分类 5 · pythonBridgeReview（Python Bridge 审查，BRG-001）

- **是什么**：M4 中两个 `pythonBridge=true` 候选——`forecast_spatiotemporal_series`（X03）、`infer_approved_raster_model`（X26）——须钉在现有 Bridge 的**已审查 typed 操作**上。
- **状态**：typed-operation 审查归属未裁定 ⇒ 阻塞。
- **硬约束（BRG-001）**：不新增解释器、不拓宽 Bridge、不开新端口（现 6511）、不提交自由形式源码。
- **转正前置**：指挥席＋用户裁定审查口径；逐操作签名/参数/返回 schema。
- **风险**：以自由 Python 旁路绕过 MCT/安全基线（红线）。
- **结论**：审查通过前阻塞，拒绝即正解。

## 七、分类 6 · x07x08Merge（配置三件合并，SCN-001）

- **是什么**：场景环境/图层高程/拉伸三件（X07/X08 等）可能**并入已验收 `set_map_properties` 的参数面**，而非各自成为新公开名。
- **状态**：合并裁定未决；在裁定前，**新名称主张阻塞**（SCN-001）。
- **转正前置**：指挥席裁定是否合并；若合并，参数面扩展须可复算且不增注册中心第二处（Rule 7）。
- **风险**：未裁定即按新工具名申报（重复注册/冒名）。
- **结论**：维持 blocked-by-ruling，不预设结果。

---

## 八、M4 后整体储备状态

- M4 为**目录收官**：60 场景/36 模板全定义（0 延期）；剩余缺口全部集中在**输入数据、宿主许可、白名单/Bridge/合并裁定**，而非目录定义。
- 48 公开名算式：48 名义 −1 合并分支（build_offline_map_package→export_data_package 的 packageType）−2 内部服务（compose_evidence_report/compose_presentation_deck）＝**45 公开 M4 名**。
- 副作用：41 file-output/4 read-only/3 in-project-resource/0 in-place；仅 file-output 可带覆盖拒绝断言。

| 项 | 状态 |
|---|---|
| 在线发布三件（G-190） | DEFERRED-NOT-CALIBRATED |
| TS08 黄金输入（G-205） | NOT-AVAILABLE-PENDING-USER |
| 白名单 53 扩项（12 缺口） | BLOCKED（fail-closed），用户裁定 |
| 六类宿主许可 | NOT VERIFIED |
| Python Bridge typed 审查 | 阻塞（BRG-001） |
| X07/X08 合并 | blocked-by-ruling（SCN-001） |
| 是否改动冻结面/擅自转正 | **否**：评估≠纳入，一切待指挥席裁定 |

**总结**：M4 收官后无"目录类"储备，六分类全是**准入与裁定类**前置（发布/黄金输入/白名单/许可/Bridge/合并）；执行席已逐类清点挂账，未擅自纳入、转正或预采信他线结果。后续是否由"定义"进入"运行/实现"阶段，由指挥席与用户裁定。
