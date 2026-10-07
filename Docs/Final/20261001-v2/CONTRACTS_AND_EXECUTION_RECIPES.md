# 核心合同、执行配方与受控创新细节

日期：2026-10-01。本文是开发前的内部合同示意，**不是可运行MCP请求、已实现API或真实执行回执**。public tool参数仍以冻结schema为准；示意新增字段放应用/envelope层，需改变公开合同的部分走SC裁定。配套[智能设计](INTELLIGENCE_AND_ORCHESTRATION.md)、[开发计划](IMPLEMENTATION_AND_RELEASE_PLAN.md)。

## 1. 能力记录的机器可读字段

CapabilitySupportManifest每行至少满足以下关系，而非只写supports=true：

```json
{
  "capabilityId": "CAP-GP-OPERATION-EXAMPLE",
  "canonicalOperationId": "trusted-catalog-operation-id",
  "catalogDigest": "catalog-digest-placeholder",
  "requirementRefs": ["BR15"],
  "moduleRefs": ["M03", "M05", "M07"],
  "scenarioRefs": ["S03"],
  "invocationModes": ["compact", "granular"],
  "maturity": "CONTRACT_DEFINED",
  "environmentAvailability": "UNKNOWN",
  "supportedParameterDomainRef": "parameter-contract-placeholder",
  "effectPolicyRef": "admission-policy-placeholder",
  "hostAndLicenseProfileRef": "reference-environment-placeholder",
  "executionTraitsRef": "traits-placeholder",
  "resultOracleRef": "oracle-placeholder",
  "evidenceRefs": [],
  "limitations": ["design example; no runtime evidence attached"]
}
```

同canonicalOperationId两入口不重复计。runtime证据空不能标RUNTIME_VERIFIED；缺当前环境探测保持UNKNOWN。UI/API/帮助由同账本投影，不生成另一份口径不同的“支持列表”。

格式记录另外绑定format/action/schemaVersion、read/write/query/edit/roundTrip/offline、最大范围、lossProfile和真实测试。工具有string format参数不是各格式成功证据。

## 2. 计划、批准与预算怎么绑定

AnalysisPlan包含任务用途、不可变revision、InputSnapshotRef、步骤DAG、方法/参数来源、effects、OutputContract、资源/模型总预算与必需质量门。步骤引用准入的规范操作，不能自由写Python/JS/任意SQL。

批准记录绑定planDigest、inputScope/digest或专门持续策略、允许方法集合、outputRoot/effects、费用/资源/期限与actor。现公开工具的confirm不能代替应用服务核对；不擅自为旧schema加入这些字段。

BudgetLedger按根任务保存消耗/预约/剩余，所有revision、试验、修复、子任务共享；job重启、转模型或重跑不能重置。A3另绑定周期总预算与每次限额。费用/资源未知超出容许区间时停在明确状态，不无限试错。

示意输出合同：

```json
{
  "purpose": "planning-report",
  "ownedOutputRoot": "D:/Projects/Example/Outputs/revision-001",
  "requiredArtifacts": ["gis-result", "map-image", "psd-master", "xlsx-indicators", "pptx-deck"],
  "optionalArtifacts": ["docx-report"],
  "qualityGates": ["method-valid", "facts-consistent", "map-protected", "files-reopened"],
  "editableParts": ["psd-text-and-design-layers", "xlsx-tables", "pptx-text-and-supported-charts"],
  "originalsPolicy": "preserve-and-write-new-revision"
}
```

这些路径是产品使用示例，本轮没有创建工程或运行写入。实际任务卡在执行前显示精确输出目录与文件计划。

## 3. GP类型/环境/effects的执行顺序

1. algorithmId/canonicalOperationId解析到固定可信catalog snapshot，来源/版本/准入digest一致。
2. Codec处理参数类型/顺序/多值/值表/复合/单位/字段依赖；不能拼任意源码，含分号/引号的合法值也不能错误split。
3. 输入/对象版本与完整选择复核；方法EligibilityReport适用且许可条件实际成立。
4. 计算条件effects与所有原位输入/派生/侧车/中间件。CalculateField、Near或RepairGeometry类输入修改不能因无显式out参数被当只读。
5. 显式workspace/scratch/extent/mask/CRS/overwrite及并发隔离；支持的环境集合有版本，禁止隐式继承上任务状态。
6. 批准/资源/磁盘/路径检查，持久intent成功后才调用真实Host。
7. 输出/修改状态/内容oracle和环境恢复检查，receipt持久化；unknown与失败进入对账。

预检或intent失败＝零宿主执行；宿主已成功但receipt失败＝产物保留/待对账，不自动重发。旧run/compact/granular路径同策略，生成工具名不能绕过分类。

## 4. 规划汇报旗舰链的实施配方

| 阶段 | 输入/算法选择/实际动作 | 验证与缺口 |
|---|---|---|
| 数据 | 社区/人口/设施、合法路网/时段、已验项目规范 | CRS/单位/身份/版本/字段/选择完整；缺值/重复/未知关键条件显式 |
| 意图 | 服务覆盖、方案比较、图/PPT与指标表 | 不把“15分钟沿路”编译成直线距离；合法方法集合和结果合同 |
| 方法 | 真实网络service-area/OD条件成立时选已准入方法 | 路网/成本模式/许可不足给缺口；其他方法为不同问题，需明确采用 |
| 执行 | 统一Invoker/Jobs，拥有输出/资源预约/实际GIS | 路网可行性、对象/覆盖/统计分母、scope与取消/对账 |
| 事实 | FactTable记录数量/比例/时间/单位/方法/局限 | 每个图/表/段落绑定factId；多个分母不能混用 |
| 图件 | GIS精确范围/分类/图框→规划DesignSpec→真实PS | 指北/比例/图例同源、中文/密集图例/布局/预览修正 |
| 办公 | XLSX指标/PPTX图与说明，按合同可选DOCX | 真打开/编辑性/同数值，无旧标题/旧期次 |
| 交付/更新 | 必需文件齐全激活revision，新资料另成新版 | 原件保留，输入中途变更不能交混期结果 |

此配方是跨BP/场景的应用示例，不构成新增一个独立GP算法或宣称现网络通道已准入。

## 5. 科研多期分类旗舰链的实施配方

| 阶段 | 实际实施 | 科学边界 |
|---|---|---|
| 输入 | 多期分类栅格、来源/QA、时段、参考与分类版本 | 若只有影像而非分类结果，另需登记模型/训练/验证方法；两者不混 |
| 同格网 | 冻结CRS/变换、范围/分辨率/NoData/分类码 | 分类重采样与连续值不同；不凭外观配准，未覆盖区域分母明确 |
| 计算 | 转移矩阵/类别面积/分区统计/对比 | 类别映射/守恒/单位真值；参考不足不编分类精度/误差区间 |
| 科学图 | 固定分类/跨期一致、论文多面板/图例/比例 | 原精确GIS素材与PS视觉版同源，不重绘科学内容 |
| 设计 | 模板/真实文字测量/有限RepairRule | 字号/图例完整、颜色绑定；父组/全局效果不得污染量化图层 |
| 交付 | GIS数据/矢量图、PNG/TIFF/PSD、统计表/方法说明 | 实际文件/印刷/字体/颜色与可编辑承诺，引用定位 |
| 新版 | 新期次/规则→依赖失效→重算/重排→新revision | 旧成果历史依据和外部母版保留；缓存键不得只看mtime |

预测、因果解释与真实观测分别表达；分类变化统计不自动等同真实地物变化量，输入质量/时相差异进入限制。

## 6. 规范建库与资料接入配方

DocumentProfile→ExtractedCell/Clause→Schema/RuleCandidate→冲突/单位/否定/例外识别→专业已知答案/坏例→采用RulePack→SchemaPlan→拥有副本建库→QC→同源合规覆盖报告。

关键OCR数值、缺CRS、脚注改变适用范围、应/宜/可/不得和不小于/不超过均不能猜。模型提取的规则只是候选；普通用户使用已验资源包。报告列已查/未查条款，不笼统“完全合规”。字段alias/default/required分别走真实可写合同，宿主不支持属性不得用另一个属性假装完成。

## 7. 每动作对账与失败项续作

ReconciliationStrategy应列proofOfNotExecuted/proofOfCompleted/partialCriteria/unknownCriteria、idempotentRetryAllowed、allowedCompensation和manualDecision。每类写动作有实际策略，receipt的存在不是唯一证明。

失败项续作读取同输入/计划/依赖版本及检查结果：已完成项复验后复用，未执行项可排队，部分/未知项先对账，输入变化形成新计划。不能把失败列表整体再次调用，不能靠删用户输入或改失败分母修好作业。

PS对账核对拥有文件/文档新身份、语义对象/Spec版本、实际动作/图层/保存结果；文件对账核对内容hash/结构/oracle；GDB修改核对预期schema/状态与作用范围。跨宿主没有全局事务，补偿只在已批准且证明安全的范围。

## 8. 受控方案探索

ScenarioStudySpec定义允许变化的方案变量、范围/离散候选、目标指标、硬约束、方法版本、预算/最大评估次数与比较合同。ExperimentPlan各候选有明确身份和相同输入/方法，统一Jobs执行；变量不包括偷偷改数据质量、删类别、换统计分母或放宽科学门。

例如设施候选位置/有限配置方案在真实网络前提下，比较覆盖、成本与公平性；权重与目标来自业务采用记录。输出可行方案及trade-off/Pareto候选，不把模型审美分或一句“最佳”当专业最优证明。

方法/变量或输出范围超出批准生成新计划。试验结果明确标试验；用户采用后才形成正式方案revision。固定预算/候选数/同一oracle防止无界搜索。方案比较属于X5/X6现语义，新增实现/接口走SC，不能把每候选算新增算法。

## 9. 持续改进与创新如何进入产品

FeedbackRecord记录已验证任务、明确用户接受/拒绝的Patch、真实故障和所用版本；不自动上传用户数据。RecipeCandidate/MethodRuleCandidate/TemplateCandidate从反馈形成待验证版本，说明改动依据、适用范围和回退。

成熟配方需满足：至少两个不同合格数据集/工程回放、正负例/方法/科学门、预算/效果对照与独立复核；涉及公共契约/白名单/依赖/安全变更再走裁定。通过后以不可变新版本采用；旧任务继续锁旧依赖，质量回退停用新候选并可回退。

模型可提出新思路、发现覆盖缺口或辅助生成资源草案，但不能自行安装代码、执行任意脚本、扩大effects或在线改核心规则。创新迭代用真实任务成功/质量/介入/时间成本验证，而不是持续增加按钮和名字。

## 10. 固定回执字段

每步回执含task/job/step/request、plan/input/catalog/policy/spec revision、owner/fence、intent/host result、sideEffectState、artifact/state evidence、CheckReport/oracle等级、预算消耗、最终状态和限制。模型总结只投影这些记录。

完整任务成功必须同时满足：批准范围成立、输入一致、方法有效、必需操作/产物验证、无严重科学错误、receipt与交付revision齐全。部分结果可以领取但标部分状态；不能由对话一句“完成”生成全绿卡。
