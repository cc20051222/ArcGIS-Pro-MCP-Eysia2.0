# 模块接口、实际扩展点与开发工作项

> 修订：**V11-DATA-METHOD-AND-MEDIA-20261002**；日期：2026-10-02。状态：**完整范围创新设计 / PASS CANDIDATE**。八周加四天为条件工程目标，原所有功能与质量门保留，未实施或实测。继承段保留旧功能合同，V5章节更新组织、自动化和实现路径；旧源码/运行快照仅作历史来源。V3规格中的180项/周及原工期不构成V5承诺，V5以D15–42四周资格窗、两池交集去重后的完整750–1275净项/周区间及主计划为准。

日期：2026-10-02。拟议开发拆分，**没有派发 D 工单或修改产品代码**。工作项属于既有 U/I/R/GA 包，估工必须去重。配套[领域合同](<DOMAIN_CONTRACTS_AND_STATE_MACHINES.md>)、[机器设计](<D:/ArcGIS-Pro-MCP 2.0/Docs/Final/20261001-implementation-details/DESIGN_SPECIFICATION.json>)。

> V6增量说明：保留V5全部具体范围与表行；新增六项原J/WI内行为、四项提效实验与余期/停止/迁移合同。原门不减；软件实现与真实容量尚未证明。当前V6章节解释更精确状态，历史V5/V4章节保留来源。

> V7实现深化：V6全部原范围保留；新增七个内部机制与完整排程证书合同，实际工期UNKNOWN。当前章节对动态覆盖、PS测量写效果、收益/拒用/激活给精确实现约束；不新增公开工具数或产品完成结论。

> V8实现深化：保留V7全部范围；读集/三轴复用/科学视觉/用途闭包/同版补齐/契约投影六机制具体化，收益与60日容量仍待实测，非产品完成。

> V9实现深化：保留原全部范围；资格依赖/当前集合/联合准入/有限公平/合同工作包/有界接力具体化。产品未执行，新增成本与60日容量待实测。

> V10实现深化：保留原全部范围；事实粒度/科学支持域/偏好解析/批量参考/合同一致/保真反例具体化。原门不省，成本、真实容量与净收益尚未测得。

> V11实现深化：原范围/门保留，资料适用/缺口采用/方法比较/稳健性/语义展示/可访问媒介具体化；实际成本与完整60日容量仍UNKNOWN。

## 每个WI的提效交付合同

30工作项原验收不变，新增产物归属、可复用资产、影响映射、资源lane、冷/热指标和质量回退。SV为职责边界，仍复用现单体服务。Composition、Configuration与项目依赖的变更先由模块提交完整合同/变更清单，再进入已授权短集成写窗；五执行端隔离准备，共享文件与宿主写入仍由受控写窗协调。

| 工作组 | 共建一次的资产 | 并行与独立验收边界 |
|---|---|---|
| WI01–04 | durable journal/fence/对账模型及故障最小反例 | fake准备可并行；实际效果恢复逐动作读回，T0未过不扩自动写 |
| WI05–10/16 | 输入与typed DAG、资源预算、事实合同 | DTO/schema/公式纯测试并行；当前真实输入/方法/批准仍检查 |
| WI11–15 | 可信快照、通用codec、策略与逐名资格队列 | 同族基座一次；每操作首次真正例/关键负例/oracle/复核不能省 |
| WI17–23 | DesignSpec/固定CommandPlan/OfficeSpec及三类缓存键 | 准备并行；PS全局单写；当前所有保存/交付仍持久核验 |
| WI24–30 | 同口径业务oracle/行业资源锁/用户任务/兼容规则 | 内容建设与代码准备并行；108/60/隐藏/新手等终门保持 |

成本按[唯一WI台账](<D:/ArcGIS-Pro-MCP 2.0/同步/3.0项目规划/05_效率优化/优化执行规格.json>)归属，U/I/R/GA标签不重复累加。尚无每WI工时实测，初值保留unknown；八周计划的首两周分层试验记录工程准备、Host、返工、专业与复核等待，按资源约束DAG重排，不按代理数乘吞吐。

## 1. 先以源码划清已有与待补

以下继承旧V3静态源码审查，原行号和差距不是本轮重审结论；实施前应复核源码漂移。没有本轮故障复现或新运行测试，不取代历史范围内的已验裁定。

| 已有基础 / 位置 | 可以复用 | 需要开发与验证 |
|---|---|---|
| [IToolInvoker](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Core/Tools/IToolInvoker.cs>)、[ToolInvoker](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Core/Tools/ToolInvoker.cs>) 38–105 | 校验后调用一次，取消/异常带 ReconcileRequired，回执存上下文 | 默认 journal 在 145–178 只调 ILogger；不是独立持久确认。需可靠 intent/receipt 与失败处理 |
| [StructuredFileLogger](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Logging/StructuredFileLogger.cs>) 126–129 | 诊断日志、可降级 | 吞 I/O 不适合作为写操作 durable intent 的成功依据 |
| [WorkflowJobStore](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Core/Jobs/WorkflowJobStore.cs>) 145–237、568–586、713–726 | claim、lease/fence、JSONL、Flush(true)、旧 JSON | SaveCore 602–614 / SaveJournal 618–640 吞保存失败；630 先更新 WrittenItems，636 才追加。需可靠确认与失败补写 |
| [FolderWorkflowTools](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Tools/FolderWorkflowTools.cs>) 1213–1219、1285–1346 | shard、子步骤共用 Invoker、已有项跳过 | 未消费子步骤 LastInvocationReceipt.ReconcileRequired；取消中的项可回 pending。需先对账再续作 |
| [JobManifest](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Core/Jobs/JobManifest.cs>) 56–89 | artifact/revision/fence/receipt DTO | 增输入、批准、预算、逻辑步骤、effects、oracle 与交付引用；保留旧载体 |
| [ToolValidatorPipeline](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Core/Tools/ToolValidatorPipeline.cs>) 19–31 | 只读、required、可选路径检查 | 条件参数、完整类型/范围、方法/批准/效果/版本统一准备；保留既有拒绝优先级 |
| [D086DesignTools](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Tools/D086DesignTools.cs>) 550–635、799–831 | PNG/PDF/SVG staging、hash/manifest 和素材目录发布 | 字体/ICC 未验；锚点为 manifest shape 检查；dataUpdates 是声明。补实际内容检查和更新语义 |
| 同文件 1115–1163 | 六场景关键词建议、缺输入/工具提示 | 不是语义理解、方法选择、资源可行性；成本不能只数工具 |
| [PsChannelHandler](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Server/Internal/PsChannelHandler.cs>) 291–356 | session/replay/fence、信封验证与 echo | 文档认领、实际批准、file grant、业务 peer、宿主单写/取消待实现 |
| D091PsTools / D102PsTools | 已注册 20 PS 合同、未实现时明确拒绝 | 真实 Adobe 执行、独立读回、保存重开；listing/route 健康不代替业务成功 |

EPS 已有独立 export_layout_eps，不代表 D086 素材包已含 EPS。是否加入素材包依实际输出合同、SC 裁定及真文件验证处理；PS 栅格图层不冒充 GIS 矢量。

## 2. 建议十二个服务边界

名称是内部职责草案，不是现有类或新增 MCP 工具。接口输入使用 DC 合同和 immutable 引用，拒绝来自模型的任意代码；端口/路径/超时/预算开关统一在 Configuration。

| 服务 | 输入 → 输出 / 必要行为 | 落点 |
|---|---|---|
| SV01 DataBindingService | 原资料/Host DTO/用户对象选择 → DatasetContract、InputSnapshot；输出采集局限 | Shared 应用层 + Host DTO 适配 |
| SV02 AdequacyAndDecisionService | 意图/数据/方法规则 → DataAdequacyReport、DecisionRecord、最小关键问题集 | I01–I04；无 SDK |
| SV03 PlanCompiler | 已采用决定/输出合同 → typed DAG、effects/资源初估、planDigest | 复用 validate_plan 的适配边界，不另建执行器 |
| SV04 InvocationPreparation | 单步骤/版本/准入/批准/资源 → PreparedInvocation 或明确缺口 | 统一 Validator/Invoker 前段 |
| SV05 ExecutionJournalRepository | intent/dispatch/receipt → durable ack；read → 版本化重放状态 | 包装现 WorkflowJobStore，诊断日志独立 |
| SV06 ResourceScheduler | 根预算/实际资源/执行约束 → reservation、排队/取消状态 | Shared；Host lane 由具体执行约束决定 |
| SV07 ReconciliationService | PreparedInvocation/执行历史/当前观测 → 四类对账结论与证据 | 内部固定策略目录，未配置策略不自动恢复 |
| SV08 FactAndClaimService | 已验分析结果 → TypedFactCell、ClaimBinding、科学解释依据 | 数据/方法 oracle 之后 |
| SV09 DesignAndRenderService | 事实/布局/模板/风格 → DesignSpec、GIS 素材、受限 PS CommandPlan | D086 与现布局服务适配，Host 分工 |
| SV10 PsExecutionServices | 固定 command/拥有文档/file grant/批准 → host receipt + 独立观测 | 现内部通道 + UXP peer；宿主全局单写 |
| SV11 DeliveryService | 同版事实/设计/办公/检查 → candidate/active DeliveryRevision | 固定 DOCX/XLSX/PPTX adapter 与打开验证 |
| SV12 UpdateAndCompatibilityService | 差异/旧检查点/新环境 → UpdateImpactPlan、兼容/迁移决定 | 复用/重验/停用均给依据 |

SV10 内拆 Gateway、HostQueue、DocumentOwnership、FileGrant，服务间共享同一个逻辑动作和 effect 记录。SV05 的持久确认是执行门，不把返回日志或内存状态当确认。

## 3. 三十项可拆分工作与验收

| ID / 既有包 | 交付内容 | 核心完成证据 |
|---|---|---|
| WI01 / R03 | durable intent/receipt 内部合同，和 ILogger 分离 | intent 写失败实际 dispatch=0；receipt 失败效果不重放 |
| WI02 / R03,R05 | JobStore 可靠保存、写后才记指纹、完整提交组重放 | item 追加失败后能补写；撕裂/损坏/缺失分开 |
| WI03 / I06,R03 | 每动作对账及子步骤 receipt 传播，失败项续作闸门 | 效果后取消/异常不能自动 pending 重发 |
| WI04 / R01,R03 | current-fence 校验、迟到效果隔离、并发认领 | 旧 owner 不能写回执/激活；实际效果仍能追踪 |
| WI05 / R04,U03 | InputSnapshot/选择全集/一致窗口策略 | 输入变化或低隔离来源能使对应计划失效 |
| WI06 / U06,I02 | DatasetContract 与字段/单位/时相/实体映射 | 同名对象、错单位、重复 ID、缺 CRS 均不猜 |
| WI07 / I01,I03,I04 | DataAdequacyReport 与集中关键问题 | 捕获会改变方法/分母的未知，低影响项可解释默认 |
| WI08 / I03,I04 | MethodCard 适用判断、DecisionRecord、可行性 | 缺网络不偷偷用缓冲代替沿路时间；排除原因有依据 |
| WI09 / U05,I04 | typed DAG、OutputContract、PreparedInvocation | 参数/依赖/effects/批准/预算一致；无任意代码 |
| WI10 / R01,I05 | 根任务预算与资源预约、控制通道、模型路由 | restart/子任务/换模型不重置预算；无饿死与无限试验 |
| WI11 / GA00,GA01 | 可信 GP 全集采集、规范 ID、冻结 metadata/diff | 发现分母可复算；未知 pyt 不自动加载 |
| WI12 / GA02 | 标量/单位/多值/值表/复合/FieldMap codecs | typed 往返正例及特殊字符/条件负例，声明未支持域 |
| WI13 / GA03 | 条件 effects、env、许可与逐操作准入 | 原位修改/派生/侧车均入 scope；不可凭方向当只读 |
| WI14 / GA04,GA05 | 旧/compact/granular 统一准备执行及发现投影 | 同参数三入口同准入/拒绝；唯一注册、旧 list 不静默变义 |
| WI15 / GA06,GA07 | 逐操作 fixture/oracle/evidence 和升级影响 | 每操作真运行/关键负例/环境绑定；别名不重复计 |
| WI16 / I07,U08 | TypedFactCell、比例/单位/空值、ClaimBinding | 故意错分母、混期或未知当零均被查出 |
| WI17 / I08,U08 | DesignSpec/语义图层/受保护区域与独立检查 | 遮挡、错误颜色、父组效果、失真变换被拒绝 |
| WI18 / U08,R03 | PS peer、批准验证、DocumentOwnership/file grant | wrong document、外部编辑、保存重开和撤销 grant 不越权 |
| WI19 / U08,R01,R03 | PS 宿主单写、协作取消、固定命令和对账 | 两文档写互斥；取消后效果分类、状态查询可响应 |
| WI20 / I08,U08 | 20 PS 合同逐业务实现、测量/有限修复与保存重开 | 每合同真实成功与关键负例；小图片 spike 不代表全通过 |
| WI21 / U08 | Office 三种 Spec/adapter、原生可编辑性、同数值 | 真打开、文本/表/声明图表可编辑、ClaimBinding 一致 |
| WI22 / R04,U08 | candidate DeliveryRevision、整体验证/激活、部分领取 | 必需文件缺失不激活；激活中断可以对账 |
| WI23 / I10,R04,R05 | UpdateImpactPlan、依赖闭包、缓存复用资格 | 人口/类别/规则变重算；纯字号变只重排且同数值 |
| WI24 / U12,I03 | 规划覆盖/方案链、网络条件、去重人口 | 手算覆盖规格、缺人口变体、双方案同口径 |
| WI25 / U12,I03,I07 | 科研同格网/掩膜/转移/科学解释链 | 手算转移/守恒/NoData、净变化≠总变化 |
| WI26 / U11,U12 | 资料定位→规则采用→副本建库→QC 覆盖链 | 正负/例外/空值/适用范围，未知不假称完全合规 |
| WI27 / U09,U10,I09 | 操作记录、RecordingGap、SkillFit 与跨项目回放 | 至少两新数据回放；不带旧路径、凭据或批准 |
| WI28 / R06,U01 | 工作台状态/出处/更新影响、可访问/离线流程 | 新手全任务验收，PS 手工设计=0，确认/异常另计 |
| WI29 / I01,I07,I08,I10 | 六创新实验的最小实现/对照/采用门 | 有固定分母、失败样本和反例；不改冻结黄金刷绿 |
| WI30 / R05,U12,GA07 | 支持/兼容矩阵、资源包锁、历史载体迁移与交接 | Pro/PS/协议/文件版本不兼容能停止并解释；新写不覆盖旧件 |

单项完成不是整模块 PASS。执行会话提交代码/实证/限制的 PASS CANDIDATE，指挥侧按当轮工单独立验收。公开合同变更和新增错误语义先映射现 33 码；如确需更改，走裁定，不能本稿直接增加。

## 4. 开发顺序与纵向切片

| 切片 | 工作项依赖 | 首个可演示结果 / 退出条件 |
|---|---|---|
| T0 可靠执行 | WI01→WI02→WI03，WI04 配套 | 故障后不重复写；没有此门不扩自动写流水线 |
| T1 数据到事实 | WI05–WI09 + WI16，依 T0 | 真实输入→适用方法→已验指标；缺条件明确停止 |
| T2 Adobe 最小真链 | WI17–WI20 + WI10，依 T1 | GIS 素材→拥有文档→真实保存重开→受保护内容核验 |
| T3 两旗舰交付 | WI21–WI25 + WI28，依 T2 | 普通用户一张任务卡到 PSD/图/表/PPT，能替换资料出新版 |
| T4 行业与复用 | WI26–WI27 + WI30，依 T1/T3 | 已验规范包和技能换数据复用；不把缺口隐藏在技能里 |
| T5 GP 规模 | WI11→WI12→WI13→WI14→WI15，执行依 T0 | 全目录发现/类型域/逐操作真实验收，逐级形成数量实证 |
| T6 创新采用 | WI29，依相应基础合同 | 六项实验实测达到采用条件；失败候选不自动入默认产品 |

这张表是规划依赖，不是并行写域授权。代码与合同研究可并行；真实写切片必须经过 T0、输入/批准和 WI10 资源预算门。机器设计给 WI14/17/19/21 的实际写路径显式补可靠性/资源前置，WI26 的真实建库依 WI14、完整版本交付依 WI22。规则草案可较早开发，不可把草案完成报成写库交付完成。现有任务先遵守有效派工；V5目标组织采用C0与E1–E5协调；真机/源文件/Host 串行限制不因规划表允许并行而取消。

每工作项交付模板：目标与范围、现有源码差距、内部合同/公开合同影响、own 输出计划、正例/关键反例/故障点、独立 oracle、环境与许可、兼容/回退、证据索引与 NOT VERIFIED。评审依据最终行为，不能仅验类名存在或测试数上升。


## 5. V5服务接口扩展：版本、学习与自动资格

以下名称都是内部建议职责；实现时优先扩展已有类，不增加第二执行器或第二注册表。Shared中的对象是序列化DTO，不传SDK对象。实际服务之间调用仍汇入统一IToolInvoker；Host中的ArcGIS SDK访问走QueuedTask.Run。模型只提交受schema限制的建议数据，任何代码、SQL、表达式或外部来源的执行必须来自已审固定支持域。

| 服务扩展 | 输入合同 → 输出合同 | 主责与失败行为 |
|---|---|---|
| SV13 CapabilityEvolutionService | SourceTrustRecord、CatalogSnapshot、CompatibilityProfile → CatalogDiff、CapabilityBundleCandidate | E2；未知来源/参数/effects停待证，不修改生产policy |
| SV14 QualificationFactory | 规范操作、codec域、已审fixture目录、独立oracle → QualificationCase、逐项EvidenceRecord | E2，C0验收；无许可或oracle不产生资格，失败保留在分母 |
| SV15 SemanticCoverageService | canonical IDs、MethodCard、输入输出/域、provider版本 → SemanticCapabilityGraph、去重计数 | E2+E3；语义等价未知标待审，不能按别名、参数分支或provider重复计 |
| SV16 ReferenceLearningService | ReferenceGrant、参考图/数据不可变副本 → StyleDNA/DatasetDNA候选 | E3提取、E4设计；置信不足列未知，不复原原始矢量/真值 |
| SV17 PreferenceAndExperienceService | 用户显式保存、任务事件、已验技能/FailureCase → PreferenceProfile、推荐依据 | E3；学习范围可撤销，推荐不继承输出授权或旧路径 |
| SV18 EvolutionProjectionService | 已提交事件、能力/偏好/候选版本、资源状态 → 工作台投影与可采用建议 | E5；显示版本/出处/待证，不把候选显示成可用PASS |

新增六个职责可以作为原服务中的组件存在。例如SV13/SV14扩展SV12的目录与兼容部分，SV16扩展SV09前置设计准备，SV17复用SV02/SV12的决策与技能适配，SV18复用工作台事件投影。模块合并不减少各项行为的成本、测试和资格要求；服务名数量也不算产品核心能力数。

### 5.1 三个代表接口的实现顺序

`PrepareCapabilityBundle(diffRef, trustRef, baseManifestRef)`先核对diff来源、Host/许可和metadata版本，再把变化分为新增、语义变化、仅说明变化、已删除、不确定。新增/语义变化生成QualificationCase，不直接准入；仅说明变化仍检查其不影响默认值/域/方法。返回candidate引用、所需adapter差距、资格队列和不支持原因，不返回“生产已更新”。

`DeriveReferenceStyle(referenceRef, taskFactRef, intendedUse)`先验证只读参考grant与允许用途，然后在D盘拥有副本提取区域、文字、色板、层级、图例容量、版式与可疑科学语义。视觉观察形成StyleDNA，再转成StyleIR候选，锁定事实和地理框。返回可迁移装饰、冲突/未知、建议模板及约束，不把OCR读出的数字并入任务FactTable。高置信也不提供原始PSD结构证明。

`RecommendPreference(taskRef, profileRef, projectRuleRef)`按当前显式指令、项目约束、已存个人偏好和全局默认的优先级生成建议，记录证据事件和适用scope。返回推荐与撤销入口，不返回新的ApprovalScope。用户拒绝推荐时保存负反馈，不能为了“越用越智能”继续应用已拒绝风格。

## 6. 三十WI的五线主责与集成合同

| 执行端 | 唯一costOwner WI | 最早可并行准备 | 真实执行前依赖 |
|---|---|---|---|
| E1 平台集成 | WI01、02、03、04、10 | durable DTO、故障载体、资源lane、合并审查 | 写类扩展须T0通过；所有共享文件写窗集中集成 |
| E2 GP与兼容 | WI11、12、13、14、15、30 | catalog、类型族、可信fixture、目录diff | Invoker可靠门、effects/许可、根资源预约；每操作真实资格 |
| E3 数据与智能 | WI05、06、07、08、09、16、23、27、29 | 事实/单位/充分性/方法/依赖图/技能/学习合同 | 真输入快照、专业依据、所用能力资格；新写依T0 |
| E4 成果工程 | WI17、18、19、20、21、22 | 模板槽位、StyleIR、OfficeSpec、固定PS计划 | T0、事实、DocumentOwnership、file grant、宿主modal预约 |
| E5 业务工作台 | WI24、25、26、28 | 公开业务规格、普通用户流程、行业内容 | 完整分析/设计/交付合同；三链独立oracle；隐藏门由C0保管 |

每个WI只归属一端，跨端是consumer不是第二份成本。Composition、Configuration、公共依赖和错误码变更由E1收集最小差异统一集成；E2提供注册贡献和参数变更清单，E3提供schema，E4提供固定命令，不同时编辑公共文件。公开schema变更走现有裁定；内部版本V5不自动授权修改公开API或现33码。

代码候选必须同时带有行为合同、受影响闭包、回退说明和与已验基线比较。候选集成后生成精确payload hash，再在该payload上验收；不能把A候选的测试报告绑定B候选。某端继续准备下一项时，不覆盖C0正在审的不可变候选目录。一个未完成WI不能以多个子PR重复计“完成数量”。

## 7. J01–J12新增边际工作账

| J / 创新 | 主责、依赖WI | 最小可运行行为 | 正例/关键反例/回退 |
|---|---|---|---|
| J01 可信能力更新 | E2；11–15、30 | 已审来源diff生成版本化候选包，stage/canary后manifest切换 | 新合法操作/恶意或未知来源；旧job继续旧锁；停用候选退旧manifest |
| J02 逐项资格工厂 | E2；12–15、E1平台 | case并行准备、真实首次运行、独立oracle、证据逐项打包 | 合格工具/复杂域和错许可；无真实证据不得计数；退回待证队列 |
| J03 语义能力图谱/去重 | E2+E3；8、11、15 | provider操作映射MethodCard和支持域，计算可解释的去重集合 | 真不同方法/别名同法多provider；等价未知人工审；保守不计新增 |
| J04 参考成图StyleDNA | E3提取+E4布局；17、20 | 图片结构候选变为受约束StyleIR，真实成图保存重开 | 合法风格/错误标尺或缺项参考；不改变事实；失败退已验模板 |
| J05 参考数据DatasetDNA | E3；5–9、27 | 提取角色/单位/拓扑/时相候选并用新数据验证映射 | 可复用结构/缺CRS同名字段；未知需决定；退逐项手动角色选择 |
| J06 用户偏好记忆 | E3+E5；27、28 | 显式保存scope偏好、解释推荐、撤销/忘记 | 跨项目适用偏好/一次临时修改；不迁移批准；撤销恢复默认 |
| J07 失败与成功经验回放 | E3；3、23、27、29 | 失败反例进入候选经验包，成功步骤生成受限技能建议 | 新资料回放/旧路径与未知效果；先对账；无采用证据停候选 |
| J08 主动补缺与多方案探索 | E3；7–10、29 | 识别高影响缺口，按固定变量/预算比较可行方案 | 缺网/低影响字号；必要问题不省；失败回原方法选择流程 |
| J09 科学约束自动设计 | E4；16–20 | 锁FactTable和保护区，真实测量后有界布局修复 | 长标题/污染父组效果；不能删类别；退已验结构仍报冲突 |
| J10 因果增量更新/对账 | E3+E1；3、5、9、22、23 | 用真实依赖闭包解释复用/重算，未知effects先对账 | 纯字号/人口和类别变更；未知依赖MISS；退完整闭包重算 |
| J11 业务技能编译 | E3+E5；24–27、30 | recording/新字段映射编译typed技能候选，在至少两个新数据回放 | 兼容映射/缺方法条件；不含凭据批准；停用新技能留旧版 |
| J12 演进工作台/资源调度 | E5+E1；10、28、30 | 展示真实资格增长、待证/资源队列、偏好采用与版本回退 | 事件投影/过期owner和迟到效果；不假报已停止；退只读状态显示 |

J01–J12属于新需求边际量，不能仅挂在WI29里视为全部已有开发量。每J分别记录设计/实现分钟、专业标注分钟、真实Host占用、独立复核、失败返工、数据/许可等待和资源包维护；共享字段/编译器成本只在主WI计一次。待首两周采样后才能写非零成本点估计和区间，现在不能编造“每项半天”并倒排成保证。

## 8. 八周交付次序与每日可审产物

首周每天提交一个有明确输入输出的冻结切片，而不是每天创建一批类。E1首先给intent失败零dispatch、receipt失败不重写、旧fence不激活的最小故障证据；E2给来源/目录/复杂参数分层清单；E3给三组黄金事实和关键未知图；E4给固定PS支持域/拥有文档/字体色彩与版式合同；E5给普通用户三链任务卡和公开业务回归。C0每天形成接受、需改、待资源三个队列。

第二周交付真实最小链和更新/学习最小反例，检查分支资源约束而非只看build绿灯。第3–6周各线用可集成小批扩展完整覆盖，E2真实资格按完整四周窗口持续执行；E4每周增加模板与特殊尺寸/字体/ICC证据，E5逐链做新用户任务而非最后才找问题。第7周冻结完整candidate，C0开始隐藏/恢复/公平比较；第8周处理已定位缺陷并复跑影响闭包及所有必需最终门。最后仅在功能门全部通过后进入真实安装候选，不提前制作最终一键包。

采用契约优先和生成器可省重复入口、表单、文档、DTO与引用检查；生成器输出不能包含未审effects或“自动PASS”。共享资产优先顺序是T0、类型合同、fixture/独立oracle目录、版本依赖图、StyleIR和OfficeSpec，再扩展各行业内容。开发阶段测试选择必须在保守完整回归的影子对照证明不漏后启用；公共合同、执行门或未知影响变化回完整范围，最终候选仍跑所有原必需门。

## 9. 指挥验收容量与关键瓶颈

一个C0可以使用机器化证据索引、已审独立oracle和差异归档提高效率；它仍必须独立接受每操作资格，不能把工具族代表例当2100或3000项实证。批量签收允许一份裁定包含多个独立证据行，但每行保存真实正例、关键负例、支持域、环境、归属、check结果和异议。隐藏答案不由实现者E5创建或查看；C0或独立专家在candidate冻结后生成并隔离，失败不被删掉。

E2维护GP资格池和跨来源语义资格池的证据交集：`K_union=K_GP+K_sem−K_overlap`。只有同一可共享资格工作项的实际证据满足两个门，才能计一次成本；未证明重合保留unknown。2100/3000两池时四周需求是750–1275净项/周，而不是无条件750。该区间还未计复杂支持域追加案例及其余功能/J验收分钟；这些分钟进入B或独立工作账，不能被union条数吞掉。

令每周C0实际可用验收分钟为A，非GP必要验收为B，每净GP操作独立审查均时为m，则GP审查上限为floor((A−B)/m)。这只是审查资源上界，实际吞吐还取决于Host、oracle、失败与返工。示例m=4分钟时，750项仅GP审查就要3000分钟即50小时/周，尚未计B；该示例不是现有实测，说明不能默认一个验收端有无限容量。首两周必须测m和异常率，通过标准化逐行机读证据减少无意义阅读，但不能降低独立性或科学判断。

真实Host按平均占用、排队、许可、内存和各lane分别测容量，不能把多个AI并行思考等同多个Pro实例或无限modal并发。若D14无法证明所需净吞吐、关键J边际成本和C0容量，计划应立即给出缺口、授权资源选项和实际关键路径；不能隐瞒延期风险、取消困难样本或把所有新增核心功能推到“以后”。

## 10. 完成证据模板与维护规则

每份交付记录统一包含WI/J id、owner、冻结输入与oracle、payload/版本hash、支持域、正例/负例/故障点、真实效果读回、依赖闭包、许可环境、resource时间、失败分母、兼容与回退、候选资格状态。记录中区分DECLARED、CONTRACT_COMPLETE、RUNTIME_VERIFIED、INDEPENDENTLY_ACCEPTED；模型自述、文件存在、缓存旧回执或注册数量都不能提高状态。

每周C0检查一次范围追踪：30WI无遗漏且无重复主责，12J边际量已计，原品质门无降低，代码影响与资格环境无漂移，UI声称与证据状态一致。产品维护迭代可按相同合同自动生成候选队列，生产采用仍遵守可信源/既定批准/独立接受/版本锁。此流程为持续成长机制，不承诺永久领先或未经证据的即时全适配。

## 11. V5增量接口：补强已有底座，避免并行重造执行器

2026-10-02只读源码确认已有[ToolInvoker](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Core/Tools/ToolInvoker.cs>)默认`LoggerToolInvocationJournal`，其RecordIntent/RecordReceipt调用日志；日志文案包含persisted不证明可回读的durable提交确认。[WorkflowJobStore](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Core/Jobs/WorkflowJobStore.cs>)已具FencingGeneration、journal与lease机制。上述只属SOURCE VERIFIED，不是LIVE恢复证明；V5在这些扩展点增加可靠确认、稳定业务effect key、真实状态观测与Host reconciler，保留旧JSON/JSONL兼容，不能按“新可靠模块”重写并免费计量。

| 拟议内部接口 | 输入 → 输出 | 所属WI/J与验收边界 |
|---|---|---|
| CommitIntentAsync | PreparedInvocation、stableEffectKey、fence → CommitAck或明确失败 | E1/WI01–02；ack含frame/group/sequence/digest及可回读提交证明；失败dispatch=0 |
| ObserveEffectAsync | Host类别、精确对象、旧intent/receipt → EffectObservation | E1/WI03，各Host端提供固定observer；缺文件不证明原位操作未执行 |
| ReconcileAsync | 原执行锁、当前观测、动作策略 → NoneProven/CompletedVerified/Partial/Unknown及证据 | E1/WI03–04/J10；UNKNOWN不能重新dispatch，旧owner迟到效果不得激活 |
| CompileProviderIR | SourceTrustRecord、metadata → ProviderIR、CompilationDiagnostic | E2/WI11–14/J01；输出仅固定类型树/条件谓词，未知类型不降为自由字符串 |
| MaterializeDomain | ProviderIR、InputSnapshot、精确对象/选择 → DomainBinding、证据或缺口 | E2/WI12–13；真实字段/枚举/条件域依输入变化失效，不能把静态schema当全域证明 |
| IssueOracleCase | QualificationCase、独立OracleSpec → 密封预期引用、公开输入与检查器引用 | E2提交case；C0/专家冻结独立oracle，执行者不可生成隐藏答案 |
| AssembleEvidenceRow | 真实正负例、effects、oracle、环境/成本 → QualificationEvidenceRow | E2/WI15/J02；损坏/缺负例/错误payload行拒绝，不丢失败分母 |
| SignAcceptanceBatch | 冻结逐行证据、审查决定 → BatchAcceptanceManifest | C0验收；按每行scope接受/拒绝，签证摘要不自动替每行资格 |
| ScheduleResourceDag | ResourceDag、真实capacity/calendar、预算 → ReservationPlan、排队/阻断/最早完成区间 | E1/WI10/J12；不假设新增Host或许可；所有实际写仍原Invoker |

这些接口可以以现有服务适配层实现；名称不是新增MCP API。每接口带调用幂等/取消/错误映射/版本规则，生产拒绝语义映射现有已审错误码，确需新增按SC裁定。IExecutionJournal的持久确认与ILogger诊断降级分开；不能为了兼容void返回把提交失败吞掉。旧载体读到损坏、缺失、不支持分别报状态，迁移写新副本而不覆旧件。

## 12. ProviderIR编译器与可组合codec

编译步骤是：固定来源和provider build；按规范ID归一化aliases；抽取参数树、dependencies和固定条件谓词；绑定MethodCard/effects/env/许可；组合已审codec；生成DTO/schema/表单/发现贡献；检查全部不支持域；形成候选；以真实InputSnapshot再materialize动态域，最后进入PreparedInvocation。ProviderIR不包含任意provider安装命令、JS、Python、字符串eval或拼接shell。

ParameterType树由Scalar、Enum、DatasetRef、FieldRef、UnitValue、List、ValueTable、FieldMap、Union、Derived组成。List保留顺序/集合语义，ValueTable组合列codec与行依赖，Union必须显式discriminator，Optional区分missing/null/default。父codec只有全部必需子类型、条件和效果规则完整才完整；一个子类型未知使对应域待证，不能仅把json序列化成功称已适配。可组合部分复用WI12一次，每操作动态差异和独立oracle在WI15继续计边际成本。

例如ValueTable列1是DatasetRef、列2是依列1真实字段集的FieldRef、列3是固定Statistic枚举。类型树生成阶段可以证明列数/静态类型；materialize阶段必须在Host解析精确对象的实际字段、类型、domain和selection。数据schema或列1改变使DomainBinding失效；不能把第一个资料的字段缓存用于第二资料。同名图层不退化为名称绑定，缺日期/单位/CRS不自动猜。

## 13. 构建与资格DAG的有限资源算法

ResourceDag节点分PREPARE、HOST_EXECUTE、OBSERVE、VERIFY、REVIEW、ACTIVATE，每节点绑定唯一WI/J边际账、输入输出强引用、估计区间/实测成本、资源向量、lane、owner/fence和截止预算。构建DAG与资格DAG分开：编译/测试产物只有绑定精确payload后才能解锁真实case；fixture和oracle纯准备可以先做，但不提前把未受验payload运行结果计资格。

调度先检拓扑与缺依赖，再维护Ready集合。排序优先级是关键路径剩余时长、截止余量、等待老化和阻断下游范围，不是工具简单先跑或模型自选；用固定规则打破同分，保证可回放。准入时以当前capacity核CPU/RAM/VRAM/磁盘、Host、license、MCT、GP环境、GDB和PS全局锁，全部资源原子预约或全部不占；不能握住一个锁等待另一个形成死锁。租约过期撤销新派发并对账，实际旧写未证明停止前不释放冲突写lane给新owner。

MCT只在实际SDK段占用，Host证明该段结束后才释放；不能从后台直接访问SDK或在等待长GP期间猜已释放。共享GP env、同GDB写保守单lane，PS全局modal写即使两个文档也单lane；受支持只读控制查询保持可响应。license按真实checkout/seat/Host可用条件登记，未知capacity为不可预约，不能把软件安装或新seat默认为计划已提供。

实际占用超过估计时停止新增资源准入，保留已发生effects并检查最小可行续作；预算和根取消延续，不以换模型或节点重启重置。调度器只安排固定Host动作，不代替十闸门。每次dispatch前还检查lock、准入、批准、输入、预算和fence，等待期间条件变则重新准备。

### 13.1 周期计算与敏感性

下界至少为`max(关键路径长度，各有限资源总占用/可用容量)`，下界不是可行工期；真实finish由依赖、日历、锁、失败/返工和独立QA有限队列共同模拟。每WI/J使用分层样本区间，无法测的许可/方法缺口记UNKNOWN或硬阻断，不塞零天。按资源约束列表调度产生日历，再用p50/p95占用、困难参数占比和返工率变化做敏感性；如做Monte Carlo必须公布输入分布与相关性，不把假分布的P90说成实际90%保证。

完整资格工作池依`K_union=K_GP+K_sem−K_overlap`逐行建立，当前750–1275净项/周是两门2100/3000、Q_union=0下的必要要求。它不覆盖全部J、行业/PS/Office/108图和安装成本。单C0容量与Host容量任一不足都会变关键路径；五AI只可能减少可并行准备和部分工程等待，不能把每种真实资源扩为五份。

## 14. 独立oracle、逐项证据与批量签证

OracleSpec由独立科学预期、容差、实体/单位/CRS/选择、NoData、适用域、版本与来源组成。E2可以准备fixture与公开反例，C0或独立专家冻结科学预期；隐藏输入/答案在candidate冻结后产生并隔离，E5只准备公开业务回归。oracle可用手算、可证明微型fixture、可信独立参考方法；同一待测算法生成expected不合格，跨provider也须排查共用同算法/误差的相关性。

QualificationEvidenceRow保存真实首次正例、关键负例、当前Host/provider/policy/codec许可、effect observations、实际结果内容、OracleResult、失败/超时、cost记录、qualifiedDomain和接受者。BatchAcceptanceManifest以规范排序的rowDigest集合和payloadDigest绑定签证；row缺失、错环境、oracle不独立、支持域扩大、预期泄漏或来源不可核时逐行拒绝。C0可以批量执行已审机器校验并集中审异常，但批量签证不可用抽样代表例替未运行行，也不把脚本绿灯直接变C0裁定。

失败行保留冻结分母并进入精确修复队列；修复后新增attempt而非覆盖原失败。公共codec失败使其受影响闭包所有资格待复核，不仅重跑失败行；纯文档/报告展示变更若不改事实、执行或oracle支持域，可按证据影响图重验。全部最终门仍完整保留，选择性检查只优化开发反馈。

## 15. V5证明实验与成本归属

| 实验 | 输入与注入 | 判据、成本与回退 |
|---|---|---|
| P01组合codec | 多值/ValueTable/Union/FieldMap、未知子类型、数据schema变化 | 完整往返和真实动态域一致；未知拒绝；WI12/15边际计；退已验域 |
| P02 oracle污染 | 同待测算法生成expected、改分母/单位/NoData | 独立性审查和错结果必须被检出；正确样本仍可完成；WI15/16/24–26计 |
| P03签证篡改 | 删除一证据行、换payload、替环境、复用失效oracle | 不接受错误行/批摘要；其他已验行可保留；WI01/15计，退逐行人工核验 |
| P04资源冲突 | 同GDB双写、两PSD写、license不足、旧owner迟到 | 禁错误并行、控制可响应、效果可追溯；WI10/19计，退保守串行 |
| P05恢复与重放 | intent/dispatch/receipt各间隔故障、模型切换、旧格式 | 无盲目重写、锁/预算不漂移、结果分类正确；WI01–04/30计 |
| P06学习fallback | OCR错数/缺图例/风格冲突/参考撤销 | 不改事实，明确NOT_APPLIED，已验模板仍交完整全链；WI17–22/29边际计 |

P01–P06是现有WI/J的可行性实验编号，不增加六个免费功能或工具。实验产物必须报告原始分母、困难样本、失败与成本；当前所有实验均NOT RUN，不能把本规划表计入八周性能证据。

## 16. V6接口与唯一主责

以下为拟议C#接口语义，使用已有TypedResult/receipt/事件体系，不允许业务层绕过唯一Invoker。每方法返回版本化值对象和UNKNOWN/失败，不以bool掩盖未决。主责负责实现候选；跨线贡献不改原WI owner。

| 拟议接口 | 输入→输出 | 主责/原WI | 前置/失败 |
|---|---|---|---|
| IAdequacyCompiler.Evaluate | DC01/MethodCard/规则→DC02+PredicateGraphDigest | E3 WI07/08 | 有版本规则与确定性单位；矛盾保留证据 |
| IQuestionPlanner.Plan | DC02/QuestionCandidate/预算→QuestionSet+未决集 | E3 WI09 | 有界启发式；到限不猜答案 |
| IScenarioPlanner.Compile | ScenarioSpec/DC24→有限DAG+成本报价 | E3 WI09/29 | 前置方法/支持域成立；科学变化需新DecisionRecord |
| IFactDeltaBuilder.Compare | 两DC08集合/实体映射→FactDelta | E3 WI16/23 | 单位/分母/时间不可比则NotComparable |
| IStopCoordinator.Request | jobId/actor/observedRevision→持久StopIntentReceipt | E1 WI03/10 | 收到即本地封新业务dispatch；journal失败不报受理，保持REQUESTED_NOT_DURABLE与dispatch=0 |
| IHostEffectObserver.Observe | 不可变AttemptToken/effect refs/独立诊断budget→StopBarrier+ReconcileDecision | E1 WI04；E4 WI19 | 只读有限观测；不能自行杀未知进程或复发写入 |
| IDeliveryActivator.TryCommit | DC10/Checks/AttemptToken/sequence→Activated或Rejected | E4 WI22；E1提供提交载体 | 同durable顺序判断停止和fence；旧writer只能留下待核拥有暂存 |
| IQualificationResidualCalculator.Compute | 冻结目标/当前有效资格/日末→余期集合+需求 | E2 WI15/30 | 目标供给/实际服务未知则不能给完成承诺 |
| IRelocationVerifier.VerifyCopy | 拥有manifest/批准目标DC11→逐件可迁移检查 | E2 WI30；E4提供载体检查 | 不移动源；须重开真载体与保留独立科学检查 |

E5 WI24–26/28负责公开fixture、可复用解析器的mutation与UI/性能证据；不得接触C0独立隐藏答案，不签最终PASS。新接口合计不是新工具数量；实际边际成本按设计/代码/公开测试/Host/独立oracle/C0/E1/重资格/文档分账，null不是零。

## 17. V7接口与成本key

| 机制/拟议接口 | 输入→输出 | 主成本归属/协作 |
|---|---|---|
| M01 IProviderDescriptorCollector.Collect | trusted scope/Host/build→完整性清单+原始/规范descriptor | WI11/E2；E3方法前提 |
| M02 IConditionalProbeCompiler.Compile | 描述/已审codec/MethodCard/预算→有限ProbePlan+coverage gap | WI12/E2；WI13/15真实资格 |
| M03 IBundleHeadCommitter.TryCommit | manifest/expectedHead/epoch/fence→durable commit或冲突/待核 | WI30/E2；E1 WI01/03提交底座 |
| M04 ILayoutFeasibilitySolver.Evaluate | DesignSpec/required集合/真实TextMeasurement/预算→证书+未满足项 | WI17/E4；WI19/20真PS |
| M05 ILearningAdoptionEvaluator.Compare | baseline/candidate/冻结项目隔离case→BenefitReport | WI27/E3；E5公开资产、C0独立判据 |
| M06 IGrantRevocationCoordinator.Revoke | source/grant/purpose/scope→deny-use generation+闭包收据 | WI23/E3；E1耐久事件、E4设计影响 |
| M07 ICompleteScopeSchedulePlanner.Plan | ActivityLedger/日历/全部门/实况→ScheduleWitness或UNKNOWN | WI10/E1；E5独立证书检查 |

CostKey归主WI，协作实际子活动仍入账。共享底座与七项新增子行为分别登记一次，设计/测试/Host/专业/复核/集成/重资格/维护各分项待测；归属标签不是零成本。E5不接隐藏答案、不自签PASS；E1仍唯一产品集成。纯编译算法失败不能调用任意脚本修复，未知条件列缺口。

## 18. V8内部接口与开发任务

| 内部接口草案 | 输入→输出 | 固定任务及归属 |
|---|---|---|
| CompileStageDependencies | 锁定MethodCard/ProviderIR/格式/当前绑定→阶段读集/缺口 | WI23 E3，WI05/09/13；列必需全集和完整性证据 |
| CaptureConsistentInput | 来源/范围/有效上下文→一致性证书或UNKNOWN | WI05 E3，Host适配协作E2；全部成员、快照限制、复制receipt |
| DecideReuse | 当前用途/变化/候选成员/资格→三轴决定 | WI23 E3；与独立全重跑对照，不将trace当完整合同 |
| CheckScientificVisual | FactTable/DesignSpec/实际PS与最终carrier→贯通见证 | WI19 E4；单位/地理/legend/category/color/alpha/最终合成 |
| CompileDeliveryClosure | 原必需输出/用途/接收条件/引用→闭包和拥有复制计划 | WI22 E4；格式固定解析器和循环/未知处理 |
| DecideMissingArtifact | 同版manifest/receipt/实际状态→对账或合法补齐计划 | WI23 E3，WI03 E1；无新随机id盲重试 |
| GenerateConsumerProjections | 冻结真实registry合同/变体/消费者清单→D staging投影组 | WI04 E1；同list派生count、保留归档/不同语义字段 |

每接口开发包包含类型字段、有限判定/固定codec、失败/未知分支、原载体兼容、可审日志/工作台原因、公开反例和真实正负例。分设计/代码/Host/故障/oracle/E1集成/C0独立复核/文档再资格记唯一costKey。M01与M02共享底座只记一次；同样字段不能免去不同格式/提供者的支持域实证。

## 19. V9内部接口及工作包

| 接口草案 | 输入→输出 | 原归属与必要工作 |
|---|---|---|
| BuildQualificationIndex | 耐久forward/全集/adapter维度→镜像/水位/缺口 | WI15 E2；逐边校验，重建、循环、预算截断与旧引用 |
| DecideQualificationInvalidation | 当前来源差异/完整边界→deny事件/重验计划 | WI15 E2；WI01/03 E1控制排序，真实Host迟到反例 |
| CertifyCurrentQualificationSet | 固定目标/成员/环境/当前依赖→三清单/域缺口 | WI15 E2；WI30消费，不混当前条件与历史成熟度 |
| TryCommitJointAdmission | 完整需求/容量/冲突/批准/根预算→耐久票据或等待 | WI10 E1；唯一提交者与重启重放，WI13/19实际适配 |
| SelectReadyAndDrain | 固定ready集/年龄/绕行/释放证据→候选/屏障 | WI10 E1；超大分流、稳定身份、队列上界、无硬SLA |
| SettleAdmissionAxes | 精确Host/效果/账单/存储证明→分资源事件 | WI10 E1；不能通用dispose归还全部，不覆盖旧资产 |
| CompileContractWorkPacket | 当前源/合同/所有consumer/语义diff→版本矩阵/上下文 | WI04 E1；E2–5贡献自己的消费假设与公开正负例 |
| FreezeIntegratedCandidate | 当前基线/有序候选/编译/资格闭包→精确载荷/交接 | WI10 E1；C0独立验收，主基线变化须重冻结与补验 |

ContractWorkPacket字段：packetId/revision、originalWI/owner、requirement/gateRefs、authoritativeInstructionsRef及读取日期、basePayload/contract/SDK/TFM/generator/config身份、D候选根/批准写域、完整writeSet与dependencyReadSet、consumer版本矩阵、publicCases及checkRefs、支持域/method/effects/批准/根预算、公开已知缺口、回退/迁移、资源/成本/截止证据、候选摘要、PASS CANDIDATE结论。C0隐藏答案、密钥、私有原始数据不进入执行包。

ContextCapsule仅索引精确来源、摘要和缺口；权威全文/当轮必须自读要求不缩成摘要。固定版本consumer矩阵至少绑定providerPayload、consumerPayload、contractDigest和qualificationEnvironment；missing不是兼容。结构diff覆盖删除、类型/必需/nullable/default/enum/方向/精度；语义diff覆盖单位/CRS/分母/NoData、科学域、效果/许可/批准/预算/fence/错误details。结构相同不能抹去语义变化。

开发反馈测试保留真实准备/验证/准入/序列化路径，只替实际效果边界；不能mock路由/批准之后宣称产品安全。完整编译Shared/binary变化往往影响大域，接口局部绿灯不能转移到新载荷科学资格。CONTRACT_READY、CANDIDATE_FROZEN、INTEGRATION_READY是工作进度，不产生ACTIVE工单或生产写许可。[Pact.NET消费者/生产者原仓库](https://github.com/pact-foundation/pact-net)和[固定版本矩阵](https://docs.pact.io/pact_broker/can_i_deploy)启发此方法，本项目用现有合同/测试体系实现，不默认引入Pact/Broker。

## 20. V10内部接口和一次开发归属

| 草案接口 | 输入→输出 | 原主责/工作 |
|---|---|---|
| CompileFactGrain | Dataset/Method/Output合同→typed粒度/度量/分母 | WI16 E3；WI06/08键与可加性、业务原输入 |
| ObserveJoinCardinality | 一致完整左右域/typed键/连接配方→基数/未匹配/覆盖见证 | WI16 E3；实际Host只读/副本批准仍原门 |
| CompileScientificSupport | 原操作/独立方法期望/当前格网域→expected支持/检查合同 | WI15 E2；WI08/16/25科学协作，未知不编expected |
| ResolveScopedPreferences | 原事件/Profile/当前用途/规范→每role决定/组合缺口 | WI17 E4仅采用器；原事件/检索J06 E3，UI WI28 E5 |
| SelectReferenceRoles | 合法成员/当前role要求/重复关系/根预算→来源/冲突/缺口 | WI17 E4；WI20真实测量/科学/可编辑交付 |
| CompileContractParity | IR/codec/固定规则和审定公开见证→各投影rule map/差异 | WI12 E2；WI09/14/28消费，E1唯一集成 |
| CompileExtraInteractions | 完整mandatory/有限因子约束/t→额外公开rows/tuple见证 | WI15 E2；原case不删，独立核数/实际执行 |
| MinimizeWitnessedFailure | 原不可变失败/保真谓词/当前准入/有限变换→更小同故障候选 | WI15 E2；WI03对账/WI27经验，非自动产品修复 |

所有接口输出缺口、来源/版本、适用支持域和证据状态。错误回填ValidationIssue含ruleId/instancePath/phase、准确input revision、安全摘要/缺证/合法选择；用户只看到该改哪个字段或缺什么条件，内部dialect/fence只留诊断。旧错误不能在改值后继续当当前完成/阻断依据。

每合同维护一次事实源，多视图的生成artifact/rule maps按V8投影与V9consumer矩阵绑定实际载荷。科学预期来自独立审定MethodCard/公开小答案，不能用相同生成器造expected再自证。仅静态等价的UI规则可延迟动态/授权/science到服务器，不能通过“NoValidation”按钮绕準入；原公开语义需变更仍走现审批，错误码零新增。

## 21. V11接口与已有任务的一次归属

| 设计接口 | 输入→输出与界限 | 原主责 |
|---|---|---|
| CompileTaskDatasetNeed | 当前Brief/Method/Output→必要变量、空间时间支持/精度/单位/质量/用途 | WI07 E3 |
| DiscoverDatasetOffers | Need/固定源能力/当前READ预算→tri-state offers/完整页证据及限制 | WI07 E3；WI05/06只读/实际资产核 |
| ProposeAndAdoptGapResolution | 原Need/gap/inputRevision→新候选计划；原执行后→ClosureWitness | WI23 E3；Invoker/恢复/工作台协作 |
| CompileMethodDiagnosticStudy | 独立采用分支/共同目标与域→可比与替代独立判据 | WI08 E3；原J03 E2、J09 E4保持 |
| EvaluateBoundedStability | 已验事实/固定transform与有限或解析域→ties/翻转/缺口及精确边界 | WI16 E3；非任意求解器/概率模型 |
| CompileSemanticDisplay | FactCell/locale/术语/单位/舍入→不可变tokens/损失/说明bindings | WI21 E4；WI16事实、WI28 UI消费 |
| CompileAccessibleMediaProjection | 同Output/DesignSpec→阅读角色/冗余编码/短长说明/媒体成员 | WI17 E4；WI20/21真实保存读回 |
| VerifyActualMediaCapabilities | 当前Host/build及实际成员→tags/text/order/language/attribute/editability见证 | WI21 E4；全部原科学视觉/交付门 |

新增子记录按原DC/SV传递，作用于当前Need、FactCell或OutputContract而不是第二注册/权限中心。类型/格式/合法域未知产生结构化缺口，由原QuestionSet只集中问会影响业务目标/方法/交付的关键条件。SDK访问仍经IArcGISHost/MCT；Shared不得引SDK；所有配置沿原统一配置，端口/白名单/错误码/部署身份不改。
