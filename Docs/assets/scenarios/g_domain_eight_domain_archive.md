# G-DOMAIN 八域判据档案 — D-099 产出⑤

> 来源：`Benchmarks/g-domain-precalibration-v1.json/.md`（D-097，M4 锚 `F71BF6CD…`）｜版本：v0.1（D-099 初版，2026-09-30）
> 定位：roadmap §9 **M4 出口门 G-DOMAIN**——每个域须集齐「已知答案/负向例/全自动结果/更新实例」四件方可记账完成。
> **本件冻结"将检查什么"，不是"任何域已通过"的证据**；八域全部 `PRE_CALIBRATION_DEFINITION_ONLY_NOT_VERIFIED`。

---

## 一、五判据（GDOM-01…05）

| 判据 | 谓词（要点） | 严重度 | 今日状态 |
|---|---|---|---|
| `GDOM-01` 独立路径已知答案 | 每域清单给出由**被测之外路径**算出的答案，含方法/输入/可复算步骤 | critical | 八域全 NOT-AVAILABLE-PENDING-USER |
| `GDOM-02` 负向例 | 每域至少一件**须在输出前拒绝**的输入，预期原因事先写明 | critical | 逐场景定义为 invalid profile；未运行 |
| `GDOM-003` 全自动结果 | 无手工步骤产出；开发期手工修补**不得**记为自动成功 | major | PENDING-EXECUTION |
| `GDOM-004` 更新实例 | 输入变更后重跑保持设计意图、仅公布声明的 delta | major | PENDING-EXECUTION |
| `GDOM-005` 域账与工具账分离 | 域通过本身不增工具可用账；白名单下 GP/Bridge 路径不自动扩面 | critical | 仅定义；继承 GBC-001/RAS-003 |

---

## 二、八域 readiness（构建时重算）

| 域 | 场景 | 合格输入在场 | 阻断前置 |
|---|---|---|---|
| space-time-trends | S31,S32,S33,S34,S35 | 否 | 带日历、已知变化/预测答案的时间启用序列 |
| three-dimensional | S36,S37,S38,S39 | 否 | 带公布控制高程、声明垂直基准的高程面 |
| point-cloud-terrain | S40,S41,S42,S43 | 否 | 逐格式真实点云＋参考地面集（无 .las/.laz/.copc） |
| remote-sensing-models | S44,S45,S47,S48,S49 | 否 | 公布 QA 多波段影像＋参考掩膜/类图例/模型登记 |
| science-decision-uncertainty | S50,S51,S53,S54 | 否 | 观测＋公布设计/方法/折面几何 |
| network-facility | S55,S56,S57,S58 | 否 | 带阻抗/方向网络数据集（冻结 53 无求解器条目） |
| local-interchange | S59 | 否 | 交换标准版本＋现代容器适配器（已验收源码无） |
| same-source-report | （无场景） | 否 | 已批准 Open XML 生成器＋经验证文档通道 |

> 无任何一行声称工具/许可/fixture/运行；`NOT-AVAILABLE-PENDING-USER` 是八域在本工作空间的诚实状态。

---

## 三、逐域档案（四件要求 × 当前缺口）

### 域 1 · space-time-trends（S31–35）
- **四件要求**：独立路径的分箱总量/变化点/预测答案（GDOM-01）；无时间字段或时区未声明例（GDOM-02）；分箱→趋势全自动（GDOM-03）；追加周期重算（GDOM-04）。
- **缺口**：无任何带日期时间属性 fixture；无时空立方体。
- **挂账能力**：build_space_time_cube/detect_change_points/forecast/emerging_hotspots/time_animation（X01-X06）均未实现。

### 域 2 · three-dimensional（S36–39）
- **四件要求**：控制高程已知答案面；无基准/未许可例；场景配置→剖面/视域/土方全自动；高程面变更重算。
- **缺口**：工作空间栅格无垂直基准、非已知答案面；3D Analyst 未探测。
- **挂账能力**：viewshed/LoS/profile/cut-fill/scene config/scene package（X07-X14）未实现。

### 域 3 · point-cloud-terrain（S40–43）
- **四件要求**：参考地面/冠层/跨期答案；格式缺失/密度不足例；读取→分类/派生全自动；历元替换重算。
- **缺口**：无 .las/.laz/.copc，源码无点云读取字面。
- **挂账能力**：inspect/filter/ground classify/derive surface/canopy/epoch compare（X15-X20）未实现。

### 域 4 · remote-sensing-models（S44,45,47,48,49）
- **四件要求**：参考掩膜/类图例/模型登记答案；未知 QA/超上限例；QA→掩膜/分类/分割/推理/趋势全自动；影像替换重算。
- **缺口**：无公布 QA 多波段产品；multi.tif 非合格（G-205）；无登记模型。
- **挂账能力**：QA/mask/segment/classify/infer/trend（X21-X28,X46）未实现。

### 域 5 · science-decision-uncertainty（S50,51,53,54）
- **四件要求**：公布采样/插值/权重/集合答案；无种子/折面泄漏/无权重例；设计→采样/插值/敏感性/集合全自动；观测或情景变更重算。
- **缺口**：无带公布设计/折面的观测集。
- **挂账能力**：sample design/interpolate/cross-validate/sensitivity/ensemble（X29-X36）未实现。

### 域 6 · network-facility（S55–58）
- **四件要求**：可达性/选址/配送/中断已知答案；无阻抗/跨断开例；网络求解全自动；网络或中断场景变更重算。
- **缺口**：无网络数据集；0 of 53 白名单为网络求解器。
- **挂账能力**：OD/nearest/location-allocation/VRP/disruption（X37-X41）未实现。

### 域 7 · local-interchange（S59）
- **四件要求**：往返重开/符合性已知答案；静默读原件/仅哈希例；打包→重开全自动；包内容变更重算。
- **缺口**：无实测包格式/重开行为；无现代容器适配器。
- **挂账能力**：conformance/vertical transform/offline package/cube round-trip（X14,X43,X45）未实现；X45 校准为 export_data_package 的 packageType 分支。

### 域 8 · same-source-report（无场景，报告/PPTX 通道）
- **四件要求**：同源报告/PPTX 的已知内容答案；缺生成器例；报告/PPTX 全自动生成；数据变更后仅更新声明 delta。
- **缺口**：Open XML 生成器**未批准/未安装**；compose_evidence_report/compose_presentation_deck 为内部服务、未注册。
- **挂账能力**：compose report/deck（SRP-001）阻塞；不声称任何文档已生成。

---

## 四、fail-closed 纪律（域门）

1. 无四件（GDOM-01…04）不得记任何域行为完成（GDOM-05 同步记账分离）。
2. 缺失算法不由通用执行器推断；扩 53 白名单/授许可属用户裁定（OSV-001 约束）。
3. 6511 边界即判据（BRG-001）：加解释器/拓宽/新端口/自由源码的请求，拒绝即正解。
4. 权重/模型为门控制品（MRC-001）：占位模型不是能力。
5. 合并裁定保持可见（SCN-001）：配置三件并入 set_map_properties 前阻塞新名主张。

---

## 五、[VERIFY] 与结论

| 项 | 状态 |
|---|---|
| 八域合格输入 | 全 NOT-AVAILABLE-PENDING-USER |
| 六类宿主扩展许可 | NOT VERIFIED（未启动 Pro） |
| 网络/3D/点云/容器/Open XML 能力 | 未实现/未批准，不声称达成 |
| 任何 G-DOMAIN 运行/渲染/导出 | **无**（PRE_CALIBRATION_DEFINITION_ONLY_NOT_VERIFIED） |
| 域通过是否已记账 | **否**：四件全缺，无域可记完成 |

**总结**：G-DOMAIN 门的判据（GDOM-01…05）已冻结、八域档案与缺口已逐域建账；今日八域无一具备四件、合格输入全缺，故全部停在定义态。执行席不把任何域记为通过，不预采信他线，后续准入（数据/许可/白名单/Bridge/合并/生成器）由指挥席与用户裁定。
