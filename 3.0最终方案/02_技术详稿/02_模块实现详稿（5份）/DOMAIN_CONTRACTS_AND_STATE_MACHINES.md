# 领域合同、执行状态与恢复实现

> 修订：**V11-DATA-METHOD-AND-MEDIA-20261002**；完善日期：2026-10-03（资产物化合同深化）。状态：**完整范围创新设计 / PASS CANDIDATE**。八周加四天为条件工程目标，原所有功能与质量门保留，未实施或实测。继承段保留旧功能合同，V5章节更新组织、自动化和实现路径；旧源码/运行快照仅作历史来源。V3规格中的180项/周及原工期不构成V5承诺，V5以D15–42四周资格窗、两池交集去重后的完整750–1275净项/周区间及主计划为准。

日期：2026-10-03。内部拟议合同；必须经实际源码适配和合同裁定，不能当公开请求示例。配套[总入口](<README_IMPLEMENTATION_DETAILS.md>)。

> V6增量说明：保留V5全部具体范围与表行；新增六项原J/WI内行为、四项提效实验与余期/停止/迁移合同。原门不减；软件实现与真实容量尚未证明。当前V6章节解释更精确状态，历史V5/V4章节保留来源。

> V7实现深化：V6全部原范围保留；新增七个内部机制与完整排程证书合同，实际工期UNKNOWN。当前章节对动态覆盖、PS测量写效果、收益/拒用/激活给精确实现约束；不新增公开工具数或产品完成结论。

> V8实现深化：保留V7全部范围；读集/三轴复用/科学视觉/用途闭包/同版补齐/契约投影六机制具体化，收益与60日容量仍待实测，非产品完成。

> V9实现深化：保留原全部范围；资格依赖/当前集合/联合准入/有限公平/合同工作包/有界接力具体化。产品未执行，新增成本与60日容量待实测。

> V10实现深化：保留原全部范围；事实粒度/科学支持域/偏好解析/批量参考/合同一致/保真反例具体化。原门不省，成本、真实容量与净收益尚未测得。

> V11实现深化：原范围/门保留，资料适用/缺口采用/方法比较/稳健性/语义展示/可访问媒介具体化；实际成本与完整60日容量仍UNKNOWN。

## 缓存、批处理与持久状态的附加约束

沿用原任务/步骤/副作用状态，不新增“cache命中即SUCCEEDED”捷径。CacheDecision只给reuse/reverify/recompute/wait及依据；复制/导入/修改/保存等当前效果仍独立进入原十闸门。未解决UNKNOWN/部分完成或旧owner效果时不能靠cache绕过恢复。

ArtifactManifest关联完整成员内容、父依赖、ArtifactKey/RenderKey/EvidenceKey、当前资格与检查版本；可变GDB/PSD采用拥有副本。输入/算法随机性或隐藏依赖无法冻结时禁止复用。原子激活必需交付成员与当前事实/设计revision不变；cache损坏或eviction不改用户已领成果状态。

batch段各子动作仍有逻辑ID、durable intent、观测和receipt。group commit按完整提交组落盘后再ack；记录失败不先登记written指纹。效果后的receipt失败保留真实产物并对账，不自动pending重发。资源lease超时只撤销后续授权并发起对账，不能当作已停止宿主写入的证明。详见[优化合同字段](<D:/ArcGIS-Pro-MCP 2.0/同步/3.0项目规划/05_效率优化/优化执行规格.json>)。

## 1. 十二个共同合同

所有合同带 schemaVersion、不可变 id/revision、来源引用；SDK 对象在 Host 内转 DTO。引用指向精确版本，不依赖“当前”“最后一个”或 activeDocument。下面名称描述职责，若已有等价类型则扩展/适配，不重复造第二套执行系统。

| ID / 合同 | 必需信息 | 主要生产者 → 消费者 |
|---|---|---|
| DC01 DatasetContract | 稳定实体键/角色、几何、CRS/变换、字段语义与单位、时间、NoData、来源/覆盖/限制 | 接入 → 充分性/方法/计划 |
| DC02 DataAdequacyReport | 每 requirement 的 SATISFIED/MISSING/AMBIGUOUS/CONTRADICTED；证据、受影响指标/步骤、可采用假设 | 数据/方法检查 → 提问/计划 |
| DC03 InputSnapshot | 绑定对象、选择全集、数据/语义指纹、采集窗口、隔离等级、版本机制与局限 | Host/数据服务 → 执行/复用 |
| DC04 DecisionRecord | 合格候选、排除依据、选中项、采用的假设、actor、输入/规则版本、影响 | 方法/用户决定 → 计划/解释 |
| DC05 PreparedInvocation | plan/step/input/catalog/policy/approval/budget 引用，规范参数、effects、输出/检查/对账策略、owner/fence | 执行准备 → Invoker/Host |
| DC06 ExecutionEvent | eventId、根任务/步骤/逻辑尝试、递增 sequence、类型、前序/载荷摘要、fence、持久确认 | 执行日志 → 状态投影/恢复 |
| DC07 StepCheckpoint | 输入/依赖强指纹、效果分类、输出/宿主证据、oracle 版本/结果、复用资格/失效原因 | verifier → 续作/更新 |
| DC08 TypedFactCell | factKey/value 或 nullReason、unit、denominatorRef、时间/方案/方法/输入/来源、displayPrecision | 分析验证 → 图/表/文字 |
| DC09 CheckReport | checkId、目标版本、PASS/FAIL/UNKNOWN/NOT_APPLICABLE、证据、oracle、严重性、覆盖范围 | 独立检查 → 交付门 |
| DC10 DeliveryRevision | 父版本、事实/设计/办公/规则/依赖引用、必需文件、逐件内容/结构检查、激活状态 | 交付服务 → 成果工作台 |
| DC11 CompatibilityProfile | 主机/协议/插件/目录/参数/codec/资源包/文件版本、能力探测、支持矩阵/迁移决定 | 探测/发行 → 准入/恢复 |
| DC12 UpdateImpactPlan | 每变更的依赖闭包、重算/重排/重验/复用/停用集合、失效原因、预算与新批准需求 | 更新服务 → 计划修订 |

DataAdequacyReport 不能被“文件都存在”替代。充分性按实际问题判定：人口覆盖需要人口口径，沿路时间需要真实网络与成本模式，分类精度需要参考样本，规范合规需要已采用且适用的规则。

### TypedFactCell 的数值约束

- factKey 至少包含实体/指标/时间/方案/方法/分母/InputSnapshot；重复 key 且值不同是冲突。
- value=null 必须有 nullReason，如 missing、not-observed、not-applicable、invalid-source；缺失不转 0。
- 比例绑定独立分子/分母事实及零分母规则。已知记录口径不冒充全体口径，记录覆盖率不冒充人口量覆盖率。
- 计算保留原精度；显示格式集中规则。四舍五入后的类别比例不必恰好加到 100%，应提供明确显示处理，不篡改底层数值。
- displayPrecision 不是不确定度。没有参考/估计模型和采用依据，不生成误差条或可信区间。
- ClaimBinding 引用 factKey + revision + 展示变换；报告里的解释只能来自 DecisionRecord 和已验事实，不由模型补造依据。

## 2. 三层状态分开保存

### 任务状态

`DRAFT → PREFLIGHT → READY → QUEUED → RUNNING → VERIFYING → PREPARING_DELIVERY → DELIVERED`。

分支为 AWAITING_DECISION、RECONCILIATION_REQUIRED、PARTIAL、FAILED、CANCELLED、SUPERSEDED。状态转移表见机器设计。READY 表示准备有效，DELIVERED 表示必需结果已经验证并激活；任一状态不能仅从自然语言回复推导。

### 步骤状态

`NEW → VALIDATED → INTENT_DURABLE → EXECUTING → EFFECT_OBSERVED → VERIFYING → VERIFIED → RECORDED`。

分支为 FAILED_NO_EFFECT、FAILED_VERIFICATION、PARTIAL_EFFECT、UNKNOWN_EFFECT、CANCELLED_NO_EFFECT、BLOCKED、RECORD_PENDING。VERIFIED 表示结果检查通过，RECORDED 还要求对应持久回执成功。实际宿主执行成功但回执写失败进入 RECORD_PENDING，效果可仍为 VERIFIED，不能显示最终完成；完整但不合格的输出是 FAILED_VERIFICATION，不必误称部分效果。

### 副作用状态

NOT_STARTED / NONE_PROVEN / IN_FLIGHT / OBSERVED_UNVERIFIED / VERIFIED / PARTIAL / UNKNOWN。

它独立于步骤结果和取消请求。失败响应不是没有副作用的证明；取消请求只是控制意图。只有可靠“未执行/无效果”证据允许自动重派非幂等动作。UNKNOWN 与 PARTIAL 进入对账，不能把它们转为普通 pending 后续直接重跑。失败、部分、未知和取消结论也必须保存持久结果事件/回执；RECORDED 是成功链状态，不表示失败无需保存，失败回执不产生可复用成功检查点。

## 3. 统一执行的十个闸门

1. 读取冻结的 plan/input/catalog/policy/spec/compatibility 引用，核对来源及当前可用条件。
2. 规范化类型、单位、字段依赖和参数；保留原用户输入与转换决定。
3. 按规范参数计算条件 effects：文件、GDB 成员、地图状态、PS 文档、侧车/中间件及外部成本。
4. 验证批准绑定、所有路径、只读限制、许可、方法前提和输出合同；缺失关键条件不得开始。
5. 按根任务预算预约实际资源；Pro/Bridge/PS 等执行通道遵守各自并发约束。
6. 写 durable intent，并收到明确成功确认；失败时实际宿主 dispatch 次数必须是 0。
7. 在派发前最后复核当前计划/输入/批准/准入/许可、预约/根预算、fence 和该逻辑动作的历史歧义已解决；等待资源或记录期间条件变化则停止并重新准备。持久记录 dispatch-started，调用 Host 一次。即使记录显示 EXECUTING，重启也不能推定已执行或未执行。该复核不能为任意外部来源提供原子一致性保证，仍受 InputSnapshot 隔离等级约束。
8. 观测宿主/产物，先分类效果，再运行内容/结构/科学 oracle；只看到文件存在不能通过。
9. 写 durable receipt。写失败保留候选产物并置待对账，不再次执行宿主动作。
10. 依赖步骤全部有效后进入组合交付验证；必要文件齐全才激活 DeliveryRevision。

ILogger 是诊断日志，允许降级；IExecutionJournal 是执行事实，不允许把落盘失败当成功。拟议 journal 的确认应包含 sequence、持久载体/摘要与结果，不能仅返回 void。现有 legacy API 的兼容影响需 SC 裁定。

## 4. 持久载体与并发

优先复用 WorkflowJobStore 的 JSON/JSONL、lease 和 fencing，新增 IJobRepository / IExecutionJournal 内部适配层，避免两份互相矛盾的事实账本。旧任务不就地改载体，v2 明确版本。

建议 v2 事件采用 UTF-8、单调 sequence、eventId、frame/groupId 与 commit 标记；重放只采纳校验完整的提交组。先写并 flush，再更新内存的已写指纹；失败时待写差异保留。撕裂末行、半组、重复记录与摘要不符有不同诊断，不能统一按未执行处理。

当前 JSONL 已调用 Flush(true)。[Microsoft 文档](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)说明其刷新中间文件缓冲；这不等于多文件事务或跨主机 exactly-once，也不能免除文件系统/真实中断验收。

同逻辑步骤使用稳定 logicalOperationId；requestId 只关联传输。幂等键绑定规范参数、输入快照、方法、effects 和依赖摘要，不依赖字典迭代顺序或随机 invocationId。数字/字符串/多值顺序、缺省/显式 null、路径大小写规则按 codec 版本定义，不能因 hash 相同就推定动作幂等。

持有当前 lease 的 owner 才能推进状态和激活交付。Host dispatch 前核对 fence；长操作完成后再核对。旧 owner 的迟到效果可能已经发生，必须隔离/对账，拒绝旧回执或旧激活不是撤销效果的证明。

通知采用已提交事件的投影和去重键；通知失败可以补投递，不能为补通知重复执行 GIS/PS。磁盘预算不足先拒绝新写；intent/receipt 等必要记录优先于可丢弃预览缓存。

## 5. 对账、取消和部分成果

| 观测结论 | 需要的证明 | 允许下一步 |
|---|---|---|
| NotExecuted / NoneProven | 精确动作未派发，或该动作策略证明无效果；仅缺文件不够 | 同输入/批准/预算仍有效时重新预约执行 |
| CompletedVerified | 产物内容/状态、归属、版本、oracle 全匹配 | 补记回执、复用检查点，不再调用 Host |
| PartialEffect | 证明部分对象/文件已改变，列明范围与未知项 | 只执行已批准且可验证的补全/补偿方案；否则等待决定 |
| Unknown | 无法可靠区分未执行/完成/部分 | 保留、暂停有影响的依赖步骤；给出最小核查动作 |

不能对“不存在文件”一概判断 NotExecuted：动作可能是原位 GDB 更新、会话写，或文件生成后被外部移走。每动作必须配置 ReconciliationStrategy。

取消队列项可证明零 dispatch；取消运行项发送已支持的 Host 协作取消并停止后续有副作用步骤。收到取消后仍核对最终效果。PS modal 内取消限制和清理按实际 API 状态处理，不靠 catch 后继续发修改命令。用户可以领取已验部分成果，界面同时列未完成项；不能激活完整交付。

## 6. 输入快照与可复用检查点

隔离等级明确区分 immutable、可信版本化快照、拥有副本、稳定观测和 unknown。文件内容 hash 比 mtime 强，但单次 hash 不证明整个多步执行期间未发生变化。GDB/服务也不能靠文件夹大小或抽样记录证明全量一致。

优先读取支持一致版本的来源，或在已授权范围创建 job-owned 副本并证明复制的一致窗口。无法保证时披露限制，对要求严格同版的任务停在充分性检查。不得修改用户原件、抢占未知锁，或把检查样本的结论写成全数据证明。

复用条件必须全部成立：输入/选择与语义指纹一致；方法/参数/env/codec/规则依赖一致；输出仍存在且完整；oracle 版本有效并复验；effects/归属/批准符合当前任务；未存在未知宿主状态。纯样式修改不应重算 GIS；类别/单位/格网/人口/规则变化按依赖失效重算。

## 7. 交付激活与兼容迁移

先生成 candidate revision，再验证 GIS 数据、图件、PS 母版、Office 和必需报告；最后在当前 fence 下更新一个可核验的 active manifest。消费者仅从 active manifest 领取完整成果。不同磁盘、文件和主机不承诺全局事务，激活中断通过 journal 与实际 manifest 对账。

历史 JSON/JSONL、包和资源版本读取结果区分 MISSING/CORRUPT/UNSUPPORTED/VALID；损坏不能悄悄回到空任务。迁移写新版本并保持原件，提供版本/字段映射与回退依据。当前主机不支持某能力时返回具体差异，不迁移后假称继续成功。


## 8. V5扩展合同：同一执行系统内的演进对象

原DC01–DC12、三层状态和十闸门全部保留。以下DC13–DC24是拟议内部版本扩展，不是新增公开工具、现有实现或越过SC裁定的API。所有引用绑定精确id/revision/digest；引用中的字符串不能解析为自由代码。对象只在D盘拥有目录保存，可写探针、磁盘预算、访问范围和归属检查先于持久写。

| 合同 | 关键字段 | 生产者→使用者及不可省略校验 |
|---|---|---|
| DC13 SourceTrustRecord | sourceId、owner、kind、canonicalRoot、allowlistedAdapter、内容/签名摘要、许可/授权依据、审查版本、撤销状态 | E2/C0→目录采集；网页宣称开源不是可加载代码或可商用证明 |
| DC14 CatalogSnapshot/Diff | Host/provider版本、来源与metadata摘要、完整性、规范ID集合、added/changed/removed/unknown、条件差异 | 已审采集器→能力更新；失败项不删分母，参数/effects未知不自动准入 |
| DC15 QualificationCase | canonicalOperationId、semanticCapabilityId、supportedDomain、fixtures、真实通道、oracle版本、关键负例、许可/环境、输出范围、预算 | E2→真实Host与C0；首次资格不能由缓存旧产物代替 |
| DC16 CapabilityBundle | 版本/父包、可信来源、catalog/codec/policy引用、逐项接受证据、支持域、compatibility、撤销表、manifest摘要 | 资格工厂→候选/影子/激活；未接受项不进入可用清单 |
| DC17 SemanticCapabilityGraph | 方法规范、输入输出角色、适用域、精度/效果差异、provider实现关系、等价证据、未知等价 | E2/E3→覆盖计数；相同方法多provider记录实现数而非多算语义数 |
| DC18 ReferenceGrant | referenceId、拥有只读副本、来源/上传者声明、允许用途、可联网/可保存范围、expiry、revoke事件 | 用户/参考服务→StyleDNA/DatasetDNA；上传不隐含再分发或模型训练授权 |
| DC19 StyleDNA/StyleIR | 观察区域、OCR及定位、颜色/文字/布局候选、观察置信、保护语义、适用模板、事实隔离、冲突与采纳记录 | E3→E4；像素风格不证明原始矢量、真比例或业务数据 |
| DC20 DatasetDNA | 字段角色/单位/编码候选、几何/CRS/拓扑、时间、样本/全量依据、可迁移映射、limitations | 参考数据→新资料映射；抽样结构不冒充全数据或真值验证 |
| DC21 PreferenceProfile | user/project/organisation scope、显式保存事件、正负反馈、属性值、来源任务、适用条件、优先级、撤销/忘记状态 | E3→建议；不存凭据、输出授权、科学数值或自动放宽准入 |
| DC22 ExperienceCase | success/failure、逻辑动作、输入/环境/版本、EffectState、复现规格、原因候选、已验原因、修复建议与采用证据 | 对账/技能→回放；失败原因未经验证必须标hypothesis |
| DC23 LearningProposal | proposalType、来源grant/经验、作用域、候选内容、评估集、反例、影响、成本、独立接受、回退 | 学习组件→C0/用户采用；模型生成不是生产自批 |
| DC24 ExecutionLock | input/method/capability/design/resource/model/approval/output合同版本、根预算、锁定时间、允许迁移规则 | 计划→全部步骤；后台更新不修改在途任务的锁 |

新增对象沿原JobStore/journal保存，不建立与执行事实相冲突的学习事实数据库。偏好索引或图谱可以是事件投影；写入成功之前不更新已写指纹。投影缺失或损坏可从完整已提交事件重建，重建失败显示明确状态，不以空偏好或空能力清单静默替代原数据。

### 8.1 固定能力包的资格行

一行QualificationRecord至少包含operation与语义ID、Host版本与session证据、provider/adapter/catalog/codec/policy版本、许可探测、实际支持的参数域、固定fixture内容摘要、真实正例结果、关键负例结果、effects范围与读回、独立oracle结果、接受者及时间。只保存“命令退出0”或“模型说正确”不能生成INDEPENDENTLY_ACCEPTED。工具所有参数族都覆盖与仅一个子域可用是不同状态，应对具体domain公开。

资格证据与运行能力分开。已验工具在当前机器无许可时仍可保留历史资格，但当前`availability=UNAVAILABLE`、`availabilityReason=LICENSE_UNAVAILABLE`，不显示可执行；可用性轴只取AVAILABLE/CONDITIONAL/UNAVAILABLE/UNKNOWN。Host升级而证据依赖变化时记录`qualificationReason=REQUALIFICATION_REQUIRED`，不得沿旧证据授予当前scope资格；复杂参数未知时记录`qualificationReason=CONTRACT_INCOMPLETE`而不是猜序列化。这两个原因另字段保存，不混入availability或成熟度枚举。

### 8.2 两类数量键防止虚增

canonicalOperationId表示可信provider中的独立规范操作，别名映射到同一个ID。semanticCapabilityId表示明确方法、输入输出语义和适用域的独立能力，provider implementations是其关系集合。一个操作的参数组合、任务配方、分发文件和MCP compact/granular投影均不新增规范操作；相同方法迁移到另一provider也不自动新增语义能力。

DC17不强制把不确定等价合并成一个，也不允许将不确定差异当新增：先标EQUIVALENCE_UNKNOWN，冻结比较时给出已确定集合及待审集合。跨provider的新方法只有在真实新增支持域/输出/科学方法证据和逐操作资格齐备时才增加对应去重计数。历史K与新K*分别维护，不能用较大“发现量”替代较小“已验能力量”。

## 9. 演进状态机与在途任务隔离

能力更新链为`SOURCE_APPROVED → DISCOVERED → DIFF_CLASSIFIED → CONTRACT_CANDIDATE → EFFECTS_LICENCE_CHECKED → QUALIFICATION_QUEUED → RUNTIME_VERIFIED → INDEPENDENTLY_ACCEPTED → BUNDLE_STAGED → SHADOW_VERIFIED → CANARY_VERIFIED → ACTIVE`。分支为SOURCE_REJECTED、CONTRACT_INCOMPLETE、LICENCE_BLOCKED、FAILED_QUALIFICATION、EQUIVALENCE_UNKNOWN、QUARANTINED、REVOKED。某个操作失败只允许候选包保留原已验功能且新增项待证，不能把失败项偷偷删掉统计分母后说全通过。

目录diff是后台读准备，只有已审采集源和已授权更新范围可以自动执行。未知.pyt/.tbx加载可能运行代码，因此不可把用户上传的工具箱当无害metadata自动读取。未知来源只能记录文件摘要与候选来源信息，审核/隔离/授权后使用固定采集adapter。生产白名单、policy、命令集合和registry不会因模型生成了合同而扩大。

候选包中每个独立接受记录绑定精确payload和支持域。影子回放使用拥有沙箱，回放不对用户工程写入；它比较新旧语义、effects、拒绝和兼容。canary只在获授权任务/拥有输出范围内进行，根预算、intent、fence与receipt完全保留。候选满足采用门后生成不可变manifest，核验内容，再在当前fence下切换单个active引用。该操作只保证消费者观察到完整某版本引用，不承诺GIS/PS/文件系统跨主机原子事务。

旧job钉住旧ExecutionLock，正常新版本激活不改变旧job。旧版本紧急撤销时停止其新的dispatch并分类已发生effects；不能为了保证“旧job继续运行”而忽略安全/许可撤销。迁移必须产生新的revision、兼容与影响记录，显示需重验/重算/补批准的集合，不把新包隐塞旧锁。回退恢复一个已验manifest引用，也不撤销已经发生的Host效果；这些效果按原DC05和动作策略对账。

## 10. 参考学习状态机和事实防火墙

参考链为`REFERENCE_REGISTERED → GRANT_VALIDATED → OWNED_COPY_VERIFIED → OBSERVATIONS_EXTRACTED → STYLE_OR_DATA_CANDIDATE → CONFLICT_CHECKED → TASK_MAPPING_CONFIRMED → SHADOW_PREVIEWED → ADOPTED_FOR_SCOPE`。未知用途、不可读文件、缺许可、结构不确定、错误科学表达、来源撤销各有分支；任何状态都不允许把参考图数字混进任务FactTable。

StyleDNA仅承载观察和候选风格。StyleIR将其转成固定槽位、网格、字体候选、颜色关系、装饰、留白和图例结构，映射到任务自己的DesignSpec。FactTable、地理框、图层身份、尺度、单位、科学类别和定量颜色由已验任务锁定。参考图有错误比例尺、缺项图例、夸大的色彩对比或不适用长宽比时，应标可迁移与不可迁移部分；相似度高不能覆盖正确性门。

DatasetDNA保留样本范围和全量结构证据。角色/单位映射是候选，须用新资料的schema和业务前提验证：同名“人口”可能是人、千人或密度，同名“面积”可能来自不同CRS，同类别代码可能语义不同。数据集上传不能自动证明地理CRS、日期、来源或标注可靠；缺条件时仍进入DC02关键问题链。

用户撤销grant后不再生成新衍生候选，不再将该参考用于跨项目推荐，并按其约定删除可删除缓存和偏好索引。已交付任务的证据引用以最小必要元数据标明撤销状态；需要保留的审计记录和已领成果不能伪报删除。向云模型发送参考、保存长期特征和训练权重是不同用途，默认不等同；没有必要进行在线fine-tune，首版以本地结构提取、受限检索和已验模板映射完成学习行为。

## 11. 偏好、经验和技能的采用状态

偏好事件分`OBSERVED → SUGGESTED → EXPLICITLY_SAVED → ACTIVE_FOR_SCOPE → DISABLED/FORGOTTEN`。一次说“这张图例放左边”只影响本次StylePatch，不自动成为个人长期默认。作用域显式保存后才能复用，系统给出来源和优先级；同属性冲突由当前指令、项目规则、个人偏好、默认逐层解析。科学与安全硬约束独立于优先级，不提供“常用即许可”的规则。

ExperienceCase的“原因候选”必须与“已验原因”分开。某次超时可能是receipt丢失而Host成功，不能学习成“超时就重试”。仅在对账完成后建立可复现最小反例，提出修复候选并在正反例与新数据回放通过后采用。根预算、失败分母与原始证据固定；经验不能替换黄金答案或降低oracle容差使绿灯出现。

技能状态沿原RecordingGap/SkillFit增强：`RECORDING_CANDIDATE → GAP_DECLARED → TYPED_SKILL_COMPILED → FIT_VERIFIED → NEW_DATA_REPLAYED → INDEPENDENTLY_ACCEPTED → AVAILABLE_FOR_DOMAIN`。普通用户看到的是能复用的任务卡，而不是脚本。新项目必须重绑定数据角色、路径、输出授权、方法前提和版本；旧凭据、旧approval和未知动作不被带入技能。至少两个不同新资料回放与错映射/缺许可负例保留。

## 12. 强依赖键、局部更新和可证明复用

ArtifactKey覆盖输入内容与完整selection、实体/字段语义、CRS轴序/transform、时相、mask/格网、方法/算法build、参数默认/单位/env、随机种子及所有影响结果的模型权重与配置。RenderKey额外覆盖StyleIR/DesignSpec、模板、字体实际文件、ICC、分辨率、图层和图例语义；EvidenceKey额外覆盖Host/许可/policy资格、oracle版本、容差和检查范围。未知依赖、无法锁定随机性或不一致快照产生MISS/WAIT而不是猜命中。

仅检查oracle或验证标准变更且分析、事实、渲染依赖没有变化时，可先对不可变成果重验；方法、数据、事实、类别、规则适用、输出语义或科学渲染变化须沿完整闭包重算/重排并重验。不能用“规则变了只需复验”涵盖实际改变计算或布局的规则。复制/导入/保存等当前写效果即使素材命中缓存，仍需要当前approval、intent、fence与receipt。

依赖闭包描述“哪个输入导致哪个结果失效”的计算关系，不自动证明现实因果关系。J10因果增量更新指可解释的计算依赖和effects对账，不宣称从观察数据自动推断真实地理因果。输出或缓存损坏、eviction、外部PSD编辑时保留已领旧成果，当前任务重新产生拥有的新版本；原PSD不合并未知外部修改。

## 13. V5不变量与故障验收

| 不变量 | 故障注入 | 必须观察的结果 |
|---|---|---|
| 新包不能改在途锁 | 执行中激活新catalog/模板/偏好 | 老job版本不变；新job绑定新版本；撤销时停止后续dispatch并对账 |
| 参考观察不是事实 | OCR把71.4%识别为714%、参考缺图例 | 不写FactTable；仍以任务原值生成；冲突明确显示 |
| 偏好不是授权 | 保存常用路径或同名图层风格 | 当前批准/精确对象仍检查，越界dispatch=0 |
| 候选不是资格 | contract生成成功但真实oracle失败 | 未计已验数量；失败留在分母；生产包保持旧功能 |
| 缓存不是成功回执 | output被移走、receipt写失败 | 当前资格失效或RECORD_PENDING；不盲目重复Host |
| 原子引用不是跨Host事务 | 激活前后断电/PS保存半途取消 | 根据真实manifest和journal对账；不假报全交付 |
| 忘记偏好可撤销 | 删除profile索引后重启 | 不再推荐被忘内容；必要归档证据按范围保留 |

以上每项由实现端交PASS CANDIDATE，C0使用独立预期和精确受验payload接受。机器schema/状态图检查只证明结构自洽，不能替代真实GIS/PS、文件打开、逐操作资格、全部模板及新手任务门。

### 13.1 资格交集与边际成本字段

QualificationCase补充qualificationWorkItemId、gpGateMembership、semanticGateMembership、shareableEvidenceRefs、overlapProofStatus和marginalCases。两个门工作池只有逐项共享证据通过C0认可才属于K_overlap；同方法多provider不是新增语义，也不意味着新provider不用真实运行。完整工件池`K_union=K_GP+K_sem−K_overlap`，重合未知时输出上下界；可复用Q_union按同一工作项去重，不把两个池的Q直接相加。以2100和3000门为例，union为3000–5100，D15–42需750–1275净项/周；支持域复杂度与新增J仍另算分钟，不由条数代替工作量。

## 14. V5内部子合同：补充已有DC，不新增第二套账本

下列对象是DC05/07/11/14–17/19/22–24的组成部分，沿原WI/J归属实现并记边际成本，不新增独立免费模块。引用皆为immutable id/revision/digest；SDK对象不跨Shared边界。maturity仍用DECLARED/CONTRACT_DEFINED/IMPLEMENTED/RUNTIME_VERIFIED/E2E_VERIFIED/INDEPENDENTLY_ACCEPTED；availability仍用AVAILABLE/CONDITIONAL/UNAVAILABLE/UNKNOWN。目录编译阶段、资格阶段、演进阶段和原因字段分别命名，不能把CONTRACT_INCOMPLETE或REQUALIFICATION_REQUIRED写成这两轴枚举。

| 内部合同 | 完整字段组 | 主要不变量 |
|---|---|---|
| ProviderIR | irSchema、sourceTrustRef、provider/build/host、canonicalOperationId/aliases、MethodCardRef、parameterTypeTree、dependencyGraph、fixedPredicates、defaults、env、effectClassifier、licence、supportedDomain/unknownDomain、reflectionDigest | metadata结构完整不推出runtime成功；未知类型/effects无写dispatch |
| CodecPlan | codecRevision、组合树、null/default策略、单位转换决定、locale/转义规则、对象解析、动态域引用、失真诊断 | 子类型不支持不得让父类型默默降为自由字符串；encode成功不证明方法适用 |
| DomainBinding | inputSnapshot/selection/schemaDigest、精确对象、真实字段/枚举/条件结果、采集窗口、Host/policy、域版本、验证证据、局限 | 换对象/字段/选择/版本使对应动态域失效；抽样域不等于全数据域 |
| StageLock | stage/producerVersion、输入/参数具体值/依赖/输出内容digest、env与随机性、能力/资源版本、oracle/evidence引用 | 未知依赖MISS；强指纹不替当前写的approval/intent/receipt |
| StableEffectKey | task/revision/logicalAction、规范operation/method/parameters/input、逻辑目标/预期效果、EffectClassifierRevision | 不含随机requestId或短生命周期docId；稳定键只关联同业务效果，不自动证明幂等或已发生 |
| EffectObservation | Host/session、当前对象与ownership、观测时间/窗口/可靠度、实际内容/状态摘要、期望与差异、未观测范围 | 只有失败响应/无文件不能证明NONE；观测局限必须保留 |
| OracleSpec | author/reviewer、来源独立性、fixture与支持域、expected/canonical化、单位/CRS/选择/NoData、容差及依据、正负与反例、冻结版本 | 待测实现不能自行生成科学expected；纯守恒只是补充检查 |
| QualificationEvidenceRow | case/attempt、payload/ProviderIR/DomainBinding、真实正负运行、observations、OracleResult、cost、failures、qualifiedDomain、接受记录 | 行绑定当前scope和环境；首次资格不cache-only，失败不删除 |
| BatchAcceptanceManifest | candidate/payload、rowIds与排序rowDigests、根摘要、接受/拒绝/待证逐行决定、审查规则/actor、时间、理由、回退引用 | 摘要不代替逐行证据；缺行或跨payload不得由整批签字洗白 |
| ResourceDag/Reservation | nodes/edges、WI/J/costOwner、阶段、容量/日历/估计区间、真实资源/lease/fence、等待原因与当前观测 | 预约过期不证明旧写停止；actual usage超界不能放更多新写 |
| ReplayProjection/InnovationBudget | event序列、原lock、版本化reducer、记录的外部/model结果、已消费试验/修复预算、候选/失败引用 | replay不调用Host重做外部效果、不重置预算、不改黄金 |

## 15. 持久提交确认和稳定业务效果键

CommitAck至少含eventId/sequence、frame/groupId、commitDigest、carrierRevision和readback位置；消费者校验完整提交组后才能确认intent durable。适配现JobStore时继续先写/flush/提交再更新内存指纹。可回读证明不是跨Host事务或硬件永久存活承诺，还需在允许的真实故障实验验证载体边界。ILogger允许诊断降级，不能用含persisted的日志字符串代替成功CommitAck。

StableEffectKey按规范typed值生成，绑定原input/method/逻辑目标及effects，不随重启、requestId、模型切换或Host会话变化。路径别名/大小写、数值格式、missing与null、列表顺序按固定codec规范；未采用的单位/CRS转换不偷偷归一化。新业务口径、方法、输入或输出目标改变产生新key；审批版本改变可能需要重新准备，但不能伪装旧真实效果不存在。新的授权或新key也不许可覆盖用户原件。

每类Host observer是固定受审实现：GIS文件/GDB检查实际记录/几何/CRS/字段和归属；地图状态检查对象身份、选择、可见性及需保证状态；PS检查精确逻辑文档/图层、语义内容、文件保存重开及外部编辑；Office检查实际可打开、内容与可编辑承诺。策略只对其可观察域给结论，无法确认的效果保持UNKNOWN。租约/回执异常不转普通pending，即使StableEffectKey相同也不盲目重发。

## 16. 确定性恢复与外部变化的观测分离

重放输入为完整已提交ExecutionEvent和原ExecutionLock，reducer输出任务/步骤/效果/预算/待对账投影。模型输出、时间相关决定、随机种子、provider返回与用户选择都作为原事件的数据引用保存；重放读取记录，不重新询问模型以产生另一套计划。event缺失/乱序/摘要不符或reducer版本不兼容分别诊断并停止，不能回到空任务。

重放后尚未解决的外部状态由新的只读EffectObservation补充，observer事件具有新的sequence与时间，不能改写旧记录。CompletedVerified可以补receipt和检查点；NoneProven才可能在当前锁/批准/预算仍有效时重新预约；Partial与Unknown保持动作特定对账。当前reducer或新模型不能改掉旧失败，使报表只有成功路径。

shadow replay用于比较候选与已验流程：无effects部分从固定输入和记录回放，需Host效果部分在独立拥有sandbox真执行，两者分别记runtime类型。只读模拟与真实试验不混计；当前资格第一次运行不能用replay记录当新执行。在新能力包激活前验证结果、拒绝、效果范围、成本和兼容；差异未知先隔离候选，旧任务继续其可用旧锁。

## 17. 独立科学oracle和批量签证的精确边界

OracleSpec中author、expectedProducer与待测实现必须可区分；E2准备case不自动获得密封expected。公开微型手算可由各线使用，隐藏答案在candidate冻结后由C0/独立专家产生并保管。不同provider给相同值只是证据之一，若共享同实现、同错误输入或同不足假设，不构成独立科学真值。容差由方法/精度依据固定，不为了通过而调宽。

OracleResult记录PASS/FAIL/UNKNOWN/NOT_APPLICABLE、实际值、expected引用、差异和coverage。性质检查可验证守恒/边际/单位等不变量，内容检查验证预期实体与数值，两类都要；对明确不适用项不伪称PASS。失败后新增attempt与修复证据，不改expected或删除失败。更换oracle版本须区分仅验证标准变化与影响分析/事实/渲染的方法变化，沿原依赖闭包处理。

批量签证可以对规范排序rowDigest构建可核根摘要，C0确认逐行规则和异常；任何成员缺少真实正例、关键负例、独立oracle、精确环境或支持域即不具资格。机器check完成后仍保存C0逐行接受决定；一个manifest签名不能替缺行，也不能由E2签成自验PASS。E2可以获得公开回归检查结果，不得查看隐藏预期以修特定答案。

## 18. StyleIR失败、fallback与有界反例状态

StyleIR结果分APPLIED/NOT_APPLIED/REQUIRES_DECISION，并关联原因、允许风格元素、受保护语义和当前FactTable。参考观察失败、撤销grant或科学冲突时允许确定性已验模板生成完整GIS→PS→Office新revision，任务可以在其已批准业务范围完成；这只是基本链恢复，不算J04参考学习成功，不豁免其已声明支持域和独立采用门。

MinimalFailureCase包含原case引用、保持的失败谓词、owned副本、简化变更、每次真实/模拟试验、effects结果、预算和最终局限。缩减只能在固定支持域内减少无关字段/对象/参数，同时验证失败仍复现且问题语义未变。新trial需要新的拥有输出和正常intent；UNKNOWN效果先对账，不能继续试到“复现”。缩减预算、创新试验和修复次数从同根累计，重启/换模型/换provider不重置。

实验可以提出candidate patch，但采用需原失败case、缩减case、正确对照和新的不同资料回放均通过独立检查；不得修改黄金、缩数据或放宽事实/科学门。任何缩减后的“通过”若只因把困难前提删掉，就是无效反例，不可以生成修复经验。

## 19. V6子记录：沿用DC01–24

| 子记录及父DC | 最少字段 | 状态/身份约束 |
|---|---|---|
| AdequacyPredicate（DC02） | predicateId/version, requirement, typedInputs, evidenceRefs, state, severity, affectedStepIds | 沿用四态；未知不替代false；可追溯叶证据 |
| ScenarioSpec（DC04/DC24） | scenarioId, methodLock, inputLock, changedSlots, metrics, constraints, maxBranches/maxRuns/rootBudget | 同方法才可共用该锁；变方法重新决策/批准 |
| FactDelta（DC08/DC12） | ComparisonIdentity, old/new factKey, entityMapping, typedValue/unit/denominator/time/method, deltaKind, comparability, provenanceRefs | 先独立语义配对再核版本口径；版本factKey不要求相等；值/含义分开 |
| AttemptToken（DC05/DC06） | jobId, logicalStepId, attemptId, ownerId, fence, executionLockDigest | 派发前durable不可变；事件回带原token |
| StopBarrier（DC06/DC07） | stopIntentSeq, admittedAttempts, perHostStopObservation, perEffectObservation, occupiedLaneRefs, diagnosticBudget | 多轴；Stopped不推导NoEffects；全冲突lane待核前不可释放给新writer |
| ActivationDecision（DC10） | candidateRevision, checksDigest, attemptToken, decisionSeq, precedingStopSeq, durableReceipt | 同任务顺序载体裁定；持久失败不报Activated |
| QualificationResidual（DC15/DC16） | targetSetVersion, Q0Set, acceptedValidSet, revokedReasons, remainingSet, asOfDay, deadlineDay, serviceCosts | Q0不移动；集合去重；42日到期失败与未知容量分开 |
| RelocationCheck（DC10/DC11） | sourceManifestDigest, ownedTargetRoot, externalRefs, reopen/content/editabilityResults, domain/Host/license | hash仅完整性；重开及内容质量另证 |

StopObservation取NOT_STARTED/RUNNING/STOPPED/COMPLETED/UNKNOWN；EffectObservation仍为V5 NOT_APPLIED/APPLIED/PARTIAL/UNKNOWN，科学ReconcileDecision继续独立。AttemptExecutionEnded不代表所有child/effect/lane完成；只有明确实际观测和不冲突证明才能解除对应阻挡。

## 20. 恢复状态转换与日志耐久失败

恢复读取最后durable事件及拥有产物，再核Host/效果；可变投影不是执行身份来源。未知副作用步骤保持RECONCILE_REQUIRED。有效StopIntent/本地停止封锁存在时只补记录和只读对账，不因无效果或CompletedVerified自动续作；须用户或有权控制端的新ResumeDecision、当前锁/批准/根预算、旧Host结束证明及lane准入全部成立，才可重试或续派后继业务。未请求停止且控制历史完整的其他可恢复任务仍按原已批准恢复policy核上述准入。部分效果走明确补偿或新计划，不隐式覆盖。重设CancelRequested不销毁历史StopIntent。

journal追加与projection写之间崩溃按durable顺序重建；日志确认字段只有成功持久之后更新。append失败不吞为成功，不声称原指纹已耐久，不重新派发可能发生过的动作。具体文件flush/原子替换及并发writer能力必须实测；单进程锁仅保护对应范围，跨进程并发不成立时采用已验单owner/单Host冲突lane，而非伪造分布式事务。

能力包更新影响资格集时，当前有效接受与“历史曾接受”分开保存。某单元有多合格见证，只要目标条件所需见证仍有效才留在acceptedValid；不因任一历史receipt存在就算通过，恢复同一单元不再次加数。

## 21. V7子合同不增加DC编号

| 子合同/父DC | 最少字段 | 必须成立的不变量 |
|---|---|---|
| CollectorScopeManifest/DescriptorCoverage DC13–14 | provider/Host/build/collector, scopes, completeness, canonical IDs/order, rawDigest, known/unknown conditions | PARTIAL不能判REMOVED；display顺序不替参数位置 |
| ConditionalCoverage/ProbePlan DC15 | predicate来源、supportedDomain、已验/未覆盖组合、per-operation case、fixture/预算/effects/oracle | 有限不是穷尽；新操作真实资格不复用家族代表 |
| BundleHeadCommit DC06/16/24 | expectedHead, candidateDigest, epoch/fence/commitSeq, member certificates, durableAck/readback | journal head权威、projection可重建；epoch不倒退 |
| LayoutFeasibility/TextMeasurement DC19/DC09 | required role/item IDs, text/full glyph/font identity, unit/DPI/transform/Host/doc/layer, measurements/checks/receipt, constraints/budget | 创建测量对象是WRITE；bounds/字体存在不等全文/字形合格 |
| PairedAdoptionStudy DC21–23 | direct vs optimization、train cutoff、project holdout、baseline/candidate、cold/warm/order、attempts/failures/quality/cost | 不剔失败，不让显式配置冒充统计收益 |
| RevokeIntent/ForgetReceipt DC18/21–23 | all parents/grants/purpose、denyUseGeneration、closure/visited、in-flight effects、owned cleanup/retained/export/unknown | 拒用先于异步清理；用户已领资产不删除 |
| ActivityLedger/ResourceCalendar/ScheduleWitness DC12/24 | unique costKey、原WI/J/PF/gate refs、duration bounds/source、demands/occupancy、relative intervals/calendar/capacity、uncertainty/status | unknown≠0；联合所有资源；只核证书，不生成真实PASS |

时间使用相对StartDate的分钟，区间[start,end)，Dk末=起算后k日。离散搜索仅候选生成，时长向上/可用窗向内取整保持保守；独立证书检查真实分钟。既有Host运行占用和未知效果保持固定直到有结束/隔离证明，不因重新估时释放lane。

## 22. V8数据合同与禁止升格的状态轴

读集完整性是`COMPLETE_WITHIN_DECLARED_DOMAIN/PARTIAL/UNKNOWN`；输入一致性是`SUPPORTED_SNAPSHOT/DECLARED_LIMITED/UNKNOWN`并附业务是否允许限定的明确决定。sourceMemberCoverage、bindingContext、effectiveEnv、语义指纹、读取窗口和crossSourceConsistency分别存；相同内容字节不合并权限或科学单位。

复用状态不是一个PASS位：content为`EXACT_VALID/MISSING/CORRUPT/UNKNOWN`；qualification为`CURRENT_VALID/REVERIFY/REQUALIFY/UNAVAILABLE/UNKNOWN`；currentUse为`ALLOWED/DENIED/UNKNOWN`；currentEffect为`NOT_DISPATCHED/RECEIPTED/RECONCILE_REQUIRED/UNKNOWN`。决定为RECOMPUTE/RERENDER/REUSE_CONTENT_REVERIFY/REQUALIFY_BEFORE_USE/WAIT_CONDITION/WAIT_RECONCILIATION；当前recheck和materialization完成前不写SUCCEEDED。

ScientificVisualWitness含对象身份/必需role-set、FactBinding/LegendBinding、分类阈值/定量颜色语义、CRS/地图框变换、实际图层/混合/遮挡、文档profile/转换策略、采样分辨率/alpha/未覆盖像素、最终carrier和oracle版本。形状/像素/结构观测均有精确支持域，不能从一轴自动推导另一轴正确。

DeliveryUseProfile记录原输出规格、VIEW/EDIT/RECOMPUTE所需能力、接收Host/字体/许可与限制；ReferenceClosurePlan记录每引用解析器、父/子稳定ID、归属、内容身份、可复制/可外发范围、内含/外部必要/未知/禁止状态、拥有目标与集合完整性。visited去重防循环，但循环引用仍须实际载体可解析；unknown parser或运行时生成引用不得标全集完整。

MissingArtifactDecision绑定同一DeliveryRevision、stage/invocation/attempt、实际成员、receipt/effect、dirty/外部编辑、当前资格/预算/fence、StopBarrier/控制历史、恢复策略和剩余检查。缺失或未识别receipt/dispatch状态进入对账，不因文件缺失当成未尝试；互相矛盾记录待核。只有当前控制屏障已明确清除才进入补齐；Cancelled任务仍需有效显式ResumeDecision，控制历史未知或封锁未清不恢复派发。未经派发可新执行；确未发生须固定oracle证明才可有界重试；APPLIED/RECORD_PENDING先补对账；PARTIAL/UNKNOWN停相关写。外部编辑产生新拥有revision候选，绝不静默覆盖用户稿。

ProjectionGroupContract记录冻结sourceSnapshot/variant/list/schema、消费者语义字段清单、生成器版本/声明输入、历史排除、期望目标集合、逐投影摘要/diff、未知消费点、资格与裁定。单个staging组验证不意味着GIS/PS/多文件安装的原子事务，提交/失败恢复仍走原版本与控制记录。

## 23. V9资格、资源及交接子记录

| 子记录 | 至少固定字段 | 事实源/状态边界 |
|---|---|---|
| QualificationDependencyUniverse | universe/version、完整ID/维度、Host/build、forward边、seq、未观测依赖 | 已提交forward是事实；COMPLETE须声明完整性独立证明 |
| QualificationReverseIndex | universe/forward身份、indexedThroughSeq、逐边镜像、rebuild | 派生投影，缺失/错水位不能用空结果 |
| QualificationSetCertificate | target/version、head/generation/seq、E/asOf/expiry、selectedWitness、三清单/缺口 | 同成员派生数量，目标不随失败缩窄 |
| ResourceDemand | resourceKey/kind/unit/峰值/增量、allocationRef、冲突域、PPF、存储、许可、rootBudget | method/adapter锁域内的实际证据，未知不为0 |
| CapacitySnapshot | host/boot/run、观测/free/foreign/安全余量、物化/未物化预约、seq、freshness/缺口 | 对齐计量口径才可使用；非外部软件的物理隔离 |
| RuntimeAdmissionTicket | job/step/attempt/fence、authority writer、lock、需求/批准/根预算、charge/commit | 同权威提交组，不主张多job文件原子 |
| ResourceReleaseWitness | 精确ticket/命令、未入或已结束证据、无迟到、冲突scope、费用/存储事件 | 分轴释放，不以authority到期证明Host结束 |
| IntegratedCandidateManifest | currentBase、orderedCandidateSet、actualResultPayload、SDK/TFM/deps、消费者/科学闭包、publicResults | 证据绑定具体result；候选原绿灯不能证明新二进制 |

资格状态沿原DECLARED→INVOKABLE→TYPE_SAFE→REAL_EXECUTED→QUALIFIED→INDEPENDENTLY_ACCEPTED；新撤销/待重验及当前availability是独立轴。未知许可不删除历史科学资格，也不维持当前目标有效；恢复不新计数。

票据可投影PREPARED→QUEUED→ADMITTED_DURABLE→DISPATCH_AMBIGUOUS/IN_FLIGHT→SETTLEMENT_PENDING→SETTLED；另外NO_DISPATCH_PROVEN/CAPACITY_MISMATCH/ADMISSION_UNKNOWN/QUARANTINED。它们不是任务成功状态；任务继续沿原状态机，以实际效果、科学检查和完整交付裁定。重启先重放控制/预算/存储/未知Host，StopIntent不清零；对账成功不等于ResumeDecision。

资格失效及准入需同一短控制排序；反向闭包和UI可异步重建，控制屏障不可延后。首次实现只承诺已验单writer/本机受控适配器范围；多进程/多Pro协调无证明则禁止并行准入。日志ack不明、index水位落后、unknown谓词、逾期环境快照均不消费旧数/旧票。来源/效果科学身份来自独立见证，不是有SHA字段就完整可信。

## 24. V10typed子记录与状态边界

| 子记录 | 最少字段 | 保守状态 |
|---|---|---|
| FactGrainContract | 实体/typed键/语义粒度、measure可加性、单位/权重/分母、预期域、join/NoData/method | GRAIN_DEFINED不是当前完整输入已经核实 |
| JoinCardinalityWitness | 左右snapshot/域全集、每key次数、declared/observed基数、丢失/重复/规范化、完整性 | 采样不证全集；CONFLICT/UNKNOWN不自动去重 |
| ScientificSupportContract | 版本/实际格网/变换/mask/NoData、eligible域、独立expected、类型化oracle/固定误差 | 支持域齐全不等于数值/地理/effects正确 |
| PreferenceResolutionCertificate | task/用途/原tier、scope/Profile/事件seq、每rule MATCH/NO_MATCH/UNKNOWN/ERROR/REVOKED、替代链、role冲突/组合 | RESOLVED_CANDIDATE非科学PASS/批准；必需未知WAIT |
| ReferenceCorpusSelectionCertificate | 完整成员/各grant、内容/decoder/ICC、EXACT/DERIVED/NEAR关系、当前role证据、pinned/冲突/缺口/预算 | BEST_FOUND_WITH_GAPS不是最大覆盖或完整迁移 |
| ContractParityCertificate | IR/rule全集/validator配置、artifact/投影/规则映射、公开独立正负/动态deferred/差异 | 静态通过不产生Host/任务SUCCEEDED资格 |
| OracleCalibrationRecord | oracle/独立期望、正确controls、冻结适用注入、检出/漏检/异常/原始证据/C0 | parser失败不当科学检出；漏关键错停签相关资格 |
| CounterexampleWitness | 原fixture/失败谓词/前提/expected、payload/env/effects、允许变换、每次attempt/预算、repeatability | 不稳定/新错误/未知效果则保留原case并停止缩减 |

M01的固定代数规则：canonical复合键使用有序typed tuple，原文本ID前导零保留；只采用明确批准的规范化，规范化碰撞拒绝。新增内部科学配方的空键默认NEVER_MATCH；REJECT或明确NULL相等必须有MethodCard/DecisionRecord及对应资格支持，现有公开工具语义不暗改。实体集合、事件和连接行分别计量，人口存量不跨年直接相加；预聚合、实体去重或分配若产生输出仍经新计划、原Invoker/批准及receipt。

WEIGHTED_MEAN锁同一eligible支持集S，值与权重成对有效且有限，权重非负并要求sum(w[S])>0，才按sum(v[S]*w[S])/sum(w[S])求值；被排除或缺失的值不能把对应权重静默留在分母。RATIO_OF_SUMS要求已审定可比、互斥且各实体只计一次的分区，并核分子/分母的适用域及覆盖差异；比例/密度不直接相加。零分母、全缺失、非法或未知权重给typed缺失/原因，不伪造0；CUSTOM_REVIEWED仍按原MethodCard，不用此段代替所有统计方法。

M02锁实际坐标轴序/单位、选定transform及资源/版本；动态CRS适用时绑定epoch，实际格网保留完整affine、像元支持和NoData。用独立非对称控制点核正向变换，roundtrip/peer一致仅补充。数值rule先核absTol/relTol有限非负、单位一致并在运行前冻结；独立审定的有限数值域可采用abs(a-e)<=max(absTol,relTol*abs(e))，但不把这一个公式推广为所有GP/统计的统一oracle。该有限规则拒绝非有限实际值；null检查原因且不转0，类别/ID/成员/mask按相应精确规则，数值容差不等于真实地理准确度。

这些是原DC内子记录，不新增业务执行器或新的“自动批准”状态。当前输入/规则/来源/Host/oracle/字体/模板等变化沿V8/V9依赖处理；正常业务输入变化只重当前任务，资格fixture/合同域变化按原重资格。Preference同tier、来源unknown、动态schema词汇不支持不能以置信度变TRUE。

引用seq必须明确已提交；同两个unknown不构成一致证据。偏好supersedes只沿同owner/key/合法scope已保存无环链，临时task override不修改长期档。每次参考读取与效果采用仍当前用途/grant/deny-use；same bytes保留来源权限独立。布局组合、PS保存重开、科学视觉、完整交付原状态机继续，角色覆盖及反馈减少均不表示任务完成。

## 25. V11typed子记录与不自动升格的状态

| 子记录 | 至少绑定 | 状态边界 |
|---|---|---|
| TaskDatasetNeed | need/brief/method revision、mandatory criteria、AOI/axis/时相/resolution/单位/population/quality/source/use | NEED_DEFINED不等资料当前齐全 |
| DiscoveryWitness | adapter/conformance、准确query/page/limits/partial/duplicates/source revision、asset覆盖字段与权利 | QUERY_MATCHED非ACTUAL_SUPPORT_VERIFIED |
| GapResolutionProposal/ClosureWitness | gap/原Need/base revision、计划/作用/许可/预算、实际Invoker/readset/receipt、closed/residual/new gaps | PROPOSED/READY非EXECUTED；download/open非CLOSED |
| MethodDiagnosticStudy/ComparisonEligibility | 各methodLock/DecisionRecord/资格、共同评价目标/完整域/时相/单位/metric与loss | COMPARABLE不等SUBSTITUTABLE或科学PASS |
| SensitivityStudy/StabilityWitness | 固定模型/参数/transform/方向/约束、approved domain/finite rows、完整结果/ties/unknown/proof type | FINITE_STABLE非连续全域/现实概率/因果 |
| SemanticDisplayContract/LexemeWitness | FactCell/单位/分母/ratio表示、culture/termId/rounding/loss、实际carrier/token/字形 | TEXT_MATCH非科学视觉/阅读/完整交付PASS |
| AccessibleMediaProjection/CapabilityWitness | output/member/media/Host、阅读角色与顺序/语言/说明/冗余编码/真实各能力 | TAG_PRESENT非完整可访问；不可用成员不被另载体代替 |

gap采用前重核base input revision、当前READ/use/deny-use、批准/根预算/资源票据和实际依赖；版本漂移回PROPOSED_REEVALUATION，未知副作用先对账。闭合逐原Need重评，不用新Need覆盖原缺口分母；扩大方法/评价域/分母要显式新决定，旧任务保持历史依据。

比较先冻结共同评价域和完整population，记录没有共同支持或缺失成员而非只取成功交集；同一metric名不代表同公式/归一化/时相。原ScenarioSpec保持同方法，跨方法诊断另记录。稳健性解析证明只针对已资格固定线性模型与精确区间，tie单独展示；其他模型保持原真实方法/独立oracle，有限case未跑完含UNKNOWN，不得删失败后报100%。

展示先明确输入是ratio/百分数/百分点，单位转换锁比例与offset，roundingMode/精度/culture不靠运行机默认。所有tokens带FactCell/termId，不接受自由文本解析回数值作为唯一证据。描述保留类别/时相/单位/支持与限制，实际语言字形和阅读顺序另验；当前来源撤销、Host或文化规则变化沿V8/V9闭包。

M04冻结NormalizationSpec的anchor总体/时间/单位、方向/公式/参数、fit版本和合法域；各scenario共用同一已采用变换，候选集合或scenario变化不得静默refit、clip或填必需缺测。必要coverage/独立科学oracle与原硬门先成立，零业务权重不豁免必需数据。改变anchor/模型/缺失或外推配方是新方法决定和资格；科学模型系数不能作为可自由学习的个人偏好。首解析域仅S=w*x1+(1-w)*x2、0<=lo<=hi<=1的已验未取整两指标仿射评分，指标和单位先固定；其他取整/分段/非线性/多指标模型不借此声称全域稳定。

M05的DisplayTransformChain固定quantityKind、原typed精度/单位→已资格语义/单位变换→目标表达倍率→明示舍入与loss→culture词元→实际载体。绝对温度可用对应affine offset，温差只用已验比例不套该offset；ratio、百分数与百分点分开，类别/ID不走数值变换。非零舍成0或负零须依原合同采用精度、近似/阈值说明及原值绑定，不把裸0说成原事实为0；必要表达不能满足时保留缺口。有限percent toy只产词元，不证明quantityKind、loss决定、科学表达或实际文件已合格。

M06的ReadingRoleManifest锁完整必要语义节点与唯一稳定ID、明确的阅读序列/依赖和有理由的装饰排除；重复/遗漏必要节点或必要内容误标装饰不能过门。期望阅读顺序和导出器drawing order不合时保留科学绘图/保护层顺序，不为了tags强改遮挡；走原同合同已资格路径或等待。图例类别/数值/同源说明及表头关系保留，真实辅助阅读与原科学视觉、编辑性分别核。

## 26. 资产物化与同版本续传合同

本节补充原DC01/DC02/DC05、V8 ReadSet及V11 DiscoveryWitness/GapClosureWitness，不新增DC编号、服务、公开工具或执行权限。以下是拟议实现规则，未下载资产或证明运行收益。固定来源adapter经原Invoker取得资料；目录命中、HTTP成功、文件可解码和原Need闭合分别保存证据。

### 26.1 AssetAcquisitionRecord的固定字段

| 字段组 | 必需绑定及限制 |
|---|---|
| 业务与采用身份 | acquisitionId、rootTask/plan/step、原Need/asset/sourceRevision、adapter/build、input base revision、logicalAction/attempt、当前grant/use/denyUseGeneration、approval/根预算及资源票据 |
| 请求表示身份 | requestRepresentationDigest、实际来源origin、representation选择字段、固定contentEncoding及解码策略、strongValidator/validatorKind、响应版本依据与一致性限制；凭据用引用，签名URL、token和敏感请求头不入日志/项目包 |
| 部分传输与耐久载体 | owningDroot/partialPath、文件归属、fence、durableChunkRefs及逐段digest、已验receivedRanges、预期totalBytes/未知原因、实际transferBytes、attempt状态与journal提交水位 |
| 来源与完整性证据 | 脱敏redirectChain、每跳准入决定及最终origin、expectedDigest及其可信来源/算法、实际完整digest、传输完整性报告、格式内容/实际支持检查引用、物化receipt和ReadSet引用 |
| 限额与限制 | 最大实际传输字节/尝试/deadline、压缩及解码后峰值/存储上限、费用预留、许可/再分发限制、残余UNKNOWN和用户可见原因 |

requestRepresentationDigest覆盖确实影响表示的查询、协商头和adapter策略；敏感字段在授权内形成受保护身份引用，不以脱敏后的显示URL充当唯一内容身份。validator只证明其声明域中的表示一致，不认证来源、版权或科学准确；expectedDigest未有可信来源时仍可记录实得digest作为本地身份，但不能伪报已核对上游摘要。HTTP接收区间与格式解码后的字节域分开，压缩前后偏移不混用；首实现优先固定identity传输编码，仅在已验adapter支持域内开放其他编码。

### 26.2 续传、重定向和完整性判定

续传前重核当前READ/use/deny-use、来源/adapter域、原input base、批准、预算、拥有D路径与候选归属；重启先重放实际已提交区间，再对账partial文件。文件长度、最后修改时间或原GET曾成功均不能证明该范围已耐久且属于同一表示。新增请求沿原attempt/根预算累计，断线或换URL不重置费用与传输上限。

1. **206**：首版只接受已资格的单一连续byte-range。核状态、Content-Range起止/总长度、实际响应字节数、编码及表示身份；续传请求使用原强ETag的If-Range，返回仍须核同一强validator。只有同一表示且每区间内容/边界一致，才能合并无洞的区间；重叠部分不一致、长度或validator变化使该候选待对账/失效，不把不同时期数据拼起来。
2. **200**：来源忽略Range或If-Range不成立时，将响应当新的完整候选，从偏移0取得；不得追加旧partial。新表示版本绑定新候选/记录并沿依赖重评；是否允许重取仍受当前范围、总费用和D盘空间限制。
3. **416**：只说明本次范围请求未被满足，不能直接标完整。核返回的长度信息与原记录；没有同表示的完整区间、可信字节及必要检查就保持待对账/重取。服务端缩短、对象变版或错误偏移分别保留原因。
4. **缺可靠validator**：弱ETag不能用于If-Range。首版不采用HTTP-date或跨请求拼接作为等价替代；改为一次完整获取并记录来源一致性局限，必要时由受验源快照/不可变对象机制另证。重取失败留下原候选和缺口，不能因没有validator删掉该来源功能或悄悄报资料完整。
5. **重定向**：固定adapter显式处理，逐跳核scheme/origin/来源白名单、当前授权、请求及响应预算和有界跳数；默认不把凭据、cookie或自定义敏感头带往未授权origin。合法同源/跨源跳转按各自已验策略继续，签名URL刷新仅在同一已审来源流程内重新准备；相同asset名称或URL相似不证明相同表示。重定向循环、未知去向或凭据范围不明保持准确阻断，不新造通用抓取器。
6. **物化完成**：准确总长和全部无洞区间、实际digest与约定完整性均满足后，才提交不可变拥有asset manifest/receipt并登记ReadSet；长度未知不凭“进度100%”授予TRANSFER_COMPLETE。完整GET开始时无Content-Length不直接判失败；受验HTTP framing正常结束后记录实际totalBytes及其结束依据，关闭连接而完整性无法确认时保持UNKNOWN，不能把任意EOF当完整来源。当前grant、原Need和依赖再次核验，取消/撤销之后不得继续采用或自动启动解码/分析。传输完整随后才核真实格式/成员、AOI/时相/单位/CRS/NoData/有效支持、许可及当前科学方法，独立检查通过才可产GapClosureWitness。传输层成功不产生科学PASS或完整DeliveryActivated。

上述强validator/If-Range及范围合并条件依据[RFC 9110 §13.1.5](https://httpwg.org/specs/rfc9110.html#field.if-range)、[§15.3.7.3](https://httpwg.org/specs/rfc9110.html#combining.parts)；字段、固定identity首路径、许可和准入组合为本项目工程设计。固定adapter显式重定向参考[.NET官方行为](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclienthandler.allowautoredirect?view=net-8.0)：自动重定向处理Authorization不等于本项目所有敏感头都已受控；实际adapter与目标框架须分别验，不因该链接采用新运行库。

### 26.3 取消、耐久失败及原工作项归属

资产状态单独投影REQUESTED、TRANSFERRING、PARTIAL_RECONCILE、TRANSFER_COMPLETE、CONTENT_VERIFYING、SUPPORT_VERIFIED及FAILED/UNKNOWN/STOPPED；它们不重定义任务/步骤成功状态。取消先按原StopIntent拒绝新请求，已在途响应和拥有partial仍需观测，不能因传输取消假定磁盘零写或费用归零。分段回执不明只读对账实际字节、已提交区间和表示身份；未知不续写，Host/网络停止与文件空间核销分轴处理。可信完整候选可以补记既有真实效果，但有持久Stop/CancelDecision时不得自动续传或分析，恢复沿原ResumeDecision与当前完整门。

WI05/E3定义实际asset与支持语义，WI02–03/10/E1实现Invoker接入、durable区间/物化回执和D盘资源，WI23/E3登记变版与失效闭包，WI28/E5投影进度及缺口。成本记入原WI，不另开免费功能。公开验收至少覆盖同强ETag合法续传、改版/弱ETag、200忽略Range、416、错误/截断Content-Range、编码变化、跨域/循环重定向、取消与重启、分段提交失败、空间不足，以及字节完整但时相/覆盖/单位/科学不适用。全部原格式、离线、学习、数量、108图/60任务及发行门保留；实际净收益和完整60日容量仍待实测。

## 27. D盘长期留存与可再生缓存回收合同

本节细化原DC06/DC07/DC10/DC12、V9 ResourceReleaseWitness及[格式合同§28](<../03_支持与用户体验详稿（4份）/FORMAT_AND_RESOURCE_PACK_CONTRACTS.md>)，不新增DC编号、执行系统、服务、公开工具或权限。它规定未来实现如何保护长期资产并处理回收竞态；本轮没有删除任何文件，合同不是现成的删除授权。首版回收范围仅为既有明确授权下、本项目拥有且已有合法再生配方的D盘缓存。用户原件、retained fixture、历史证据/交付、归档分发、回滚链和未知锁均不进入该缓存回收器；撤销学习用途先deny-use，其遗忘范围与必要保留仍按原独立合同处理。

### 27.1 RetentionPin和ReclaimPlan的精确身份

| 子记录 | 至少固定字段 | 必须证明的边界 |
|---|---|---|
| RetentionPin | pinId/revision、asset/member/version、physicalObjectRef、ownerScope、grant/purpose、rootKind/rootRevision、sourceEventSeq、createdCommitRef、releaseDecision/ref、state及retentionPolicyRef | ACTIVE必须先耐久提交才允许消费；RELEASE_PENDING/UNKNOWN继续保护，期限只触发复核 |
| RetentionGraphWitness | universeId/generation、原已提交forward引用/全部root集合、各来源throughSeq、indexedThroughSeq、未展开引用/不支持parser/缺页/未知历史、completeProofRef | 反向索引是投影；完整域及逐源水位未齐不能由“查不到引用”证明可回收 |
| ReclaimPlan | plan/revision、固定policy/当前授权、拥有Droot与候选精确member清单、graphWitness/baseGeneration、保护排除理由、再生配方/当前合法依赖、expectedLogicalBytes/expectedPhysicalBytes、每件claim/fence、根预算/控制记录余量、执行及对账策略 | 干运行清单只给候选；预计释放量不进入可用容量，实际效果仍逐件intent/观测/receipt |
| ReclaimItemReceipt | plan/item/logicalAction/attempt、claimCommit/fence、实际文件身份/拥有路径、pre/post观测、logicalReferenceDelta、physicalDisposition、confirmedPhysicalBytes/测量依据、UNKNOWN/保留原因、settlementCommitRef | 逻辑解除引用、文件移除及物理容量归还分轴；缺回执不重发删除，不因路径不存在伪造释放字节 |

pin状态沿`REQUESTED → ACTIVE_DURABLE → RELEASE_PENDING → RELEASED_DURABLE`，另有REJECTED/UNKNOWN；只有ACTIVE_DURABLE允许进入其声明消费域，RELEASE_PENDING与UNKNOWN仍是保护根，release确认不明不得推定已释放。ReclaimPlan单独投影`DRAFT → DRY_RUN_READY → CLAIMED_DURABLE → RECHECKING → PROCESSING → RECONCILING → SETTLED`，分支BLOCKED、STOP_REQUESTED、PARTIAL_RECONCILE、ABORTED_NO_EFFECT；SETTLED必须明确实际处理/保留清单、未决为0及结算，不表示全量空间均已释放，更不产生任务SUCCEEDED或产品PASS。多成员部分/未知未闭合不能进入SETTLED，取消也不跳过逐件对账。

rootKind至少覆盖当前已准入任务/读者、排队和暂停后的续作依赖、未决intent/receipt与UNKNOWN/PARTIAL effects、完整DeliveryRevision、历史必要证据、迁移源/目标及回滚依赖。候选交付或任务在可能实际消费资产前也须提交对应pin；激活完整交付时不得出现“manifest已可领而成员pin尚未耐久”的窗口。暂停、StopIntent、owner lease过期或资格撤销均不自动释放pin；确认读者/Host已结束、无迟到效果及保留义务解除后，才提交明确release事件。不能将任务状态CANCELLED单独作为可删证明。

pin与grant是不同轴：pin只保护必要留存，绝不恢复已撤销的使用权。相同hash的不同来源、owner、grant和purpose分别保存逻辑身份及撤销记录，不能借GC合并许可或把“忘记A”说成已忘记仍被B合法引用的相同物理对象。若已有受验共享字节映射，一个物理对象必须纳入全部合法逻辑引用/pin的并集；该映射未知则禁止物理回收。首版不为此引入跨项目硬链接或新共享缓存机制。

### 27.2 pin申请与回收claim必须处于同一控制排序

沿用原已验单writer控制载体与durable提交组，不建立第二份回收事实账本。以下“原子”仅指该控制域内一个明确提交/条件判定，不承诺JSONL与外部文件删除的跨载体事务。无法证明同一控制排序、完整graph或当前拥有文件身份的环境只运行干运行/保护诊断，不能降级为先查一遍再删。

1. **生成候选**：固定retention policy、当前明确缓存管理范围及root全集；取得完整forward引用和索引齐平水位，有限遍历闭包/循环。只有不被任何保护root到达、确属允许缓存且再生依赖目前合法可用的成员进入候选；未知引用、来源缺页、旧载体尚未迁移或cache标记缺失分别保留，不把它们记作零引用。先保护控制journal的最低空间，再按释放收益与重建成本给有界候选排次。
2. **同序claim**：在同一短控制排序中校验候选generation、policy、无未决pin申请/消费及所有现存保护引用，然后durable提交精确对象的RECLAIM_CLAIMED及fence；提交确认不明即停止。所有新的pin、任务准入、manifest引用发布及消费开始必须参与这个排序。pin先赢则本项不可claim；claim先赢则同步拒绝该对象的新pin/消费并显示待重建或待重新准备，不能先读旧缓存再补pin。干运行与claim之间有新引用时，该项失效而非沿旧清单执行。
3. **派发前再核**：claim持续排除新消费；逐件再次核当前policy/授权、grant用途、全部pin状态、fence、真实D盘resolved路径/文件身份、完整成员、归属与保护排除。任何reparse/路径逃逸、未知硬链接或外部写者/锁、实际文件变版、缺可信归属或on-disk状态不明均停止该项并保留。固定adapter须证明其实际支持的排他占用/文件身份核对能防止检查后被替换；仅路径前缀比较或一次hash不够，做不到则拒绝实际删除。执行前durable记录该精确文件操作intent，再由原Invoker在既有授权内处理拥有缓存，不递归删除未知子项。
4. **观测并核销**：删除/释放的文件动作与账本receipt分别确认。逐件读回实际存在/身份/成员及占用状态，记录REMOVED_CONFIRMED、RETAINED、PARTIAL、UNKNOWN或ABSENT_UNATTRIBUTED，再提交receipt和storage settlement。重启先重放claim/intent，再读回对账；文件已消失但缺效果来源只说明当前缺失，不证明本次释放了预计字节。对账未闭合继续保留claim，不解除屏障让新消费者踩到半清理资产。
5. **结束claim**：已确认回收的对象不再可消费；合法新需求走当前原权限/预算/方法配方生成新的完整候选及pin，不能复活已移除对象的旧receipt。未动且完整归属/无清理效果已证的成员，只有显式abort提交后才可解除claim；部分多成员缓存保留不完整标识，不以取消复活成完整输入。实际dispatch次数、待核成员和费用不因恢复重置。

进程内lock本身不能证明跨进程排他；存在未参与控制排序的读者或文件writer时，这一回收域保持不可执行。claim的lease/owner到期也不表示外部删除已结束，新owner必须先对账而不能再次删除；控制代际更换不清除历史claim、停止意图和UNKNOWN。

### 27.3 空间结算、取消与公开反例

storage allocation继续反映实际留存；logicalReferenceDelta单独记录每owner/grant的索引解除，不能按原文件大小释放票据。物理占用未实测、共享对象仍保留、文件handle延迟删除或底层空间回收不明时，confirmedPhysicalBytes保持unknown/0的准确含义，当前free capacity另用新CapacitySnapshot观测；不能由两次全盘free差值唯一归因到本次回收。rename到隔离位置只是位置变化，也不产生空间归还。已证释放先按对应allocation核销，不能重复当新增可用容量加两次。

取消停止后继删除，但已派发逐件观测/对账；部分回收清单和原intent/receipt必须保留且可恢复。每轮限制成员数、扫描深度、字节/时间及重建代价，未知或低收益时先暂停后台更新/学习与可再生预览，保证原科学精度、格式/编辑性和必需成果不降级。空间不足宁可拒绝新写，也不为了保存预览丢弃控制journal、完整证据或用户成果。

公开验收至少包含：干运行后新任务pin竞态、claim后新pin拒绝、暂停/lease过期仍被保护、未知/落后索引、同hash不同grant撤销、交付激活与pin提交中断、未知锁/reparse/文件替换、逐件删除后receipt失败、取消半清理后重启、延迟删除与logical/physical不一致、空间紧张下journal优先、受保护历史/回滚被误标cache。正确拒绝与保护不是“成功清理”；每项原始结果留分母。WI02–03/10由E1实现同序pin/claim、耐久/存储核销；WI22承接交付/用途保护，WI23承接依赖闭包，WI28由E5展示范围/留存理由及恢复，原主责不变。没有新增WI或免验免费功能，成本和60日容量仍须同完整质量任务实测。

机制参考[Nix保留代际及GC依赖](https://nix.dev/manual/nix/2.24/package-management/garbage-collection)与[DVC共享缓存回收范围](https://doc.dvc.org/command-reference/gc)，核验日2026-10-03：前者说明旧代际引用保护回滚，后者说明单项目回收可能破坏其他共享引用。我们借鉴保护根/引用并集及干运行机制；durable pin、同序claim、固定D盘拥有范围与分轴核销是本项目拟议设计，不直接采用其清理命令、默认路径、远端删除或引入两套依赖。

## 28. 自主更新的来源见证、闭合成员与采用恢复

本节补足原DC13–16/DC24及§9/21的可执行子记录，唯一采用头仍由[执行合同§18](<../01_主方案（6份）/CONTRACTS_AND_EXECUTION_RECIPES.md>)的ActivationCommitted产生。字段复用既有JobStore、SourceTrustRecord和CapabilityBundle；不另设Head、注册中心、来源服务或密钥管理平台。本节是待实现合同，不证明生产已具备自主更新、来源认证或完整标准合规；真实来源/更新/Host写入和安装仍遵守既有具体授权。

### 28.1 三份子记录与来源验证的不同路径

| 子记录/父合同 | 至少固定字段 | 采用前必须证明 |
|---|---|---|
| SourceVerificationWitness / DC13–14 | witness/revision、sourceId/namespace、声明及实际contentDigest/length、来源机制/证据、collector/adapter/build、trustReview/anchor/允许用途、sourceVersion及其语义、当前撤销、TimeValidityWitness、解析/验真诊断、已提交seq | 内容身份、来源认证、合法用途、时效分别成立；摘要相同不认证来源，网页/模型声明不增加许可 |
| SourceUpdateWatermark / DC13–14 | sourceId/namespace、trustGeneration及依据、可靠版本语义、最高已验sourceVersion和对应声明digest、verificationRef/commitSeq、已知撤销/失效、无单调语义的限制 | 只让已验且授权范围内的声明推进接收水位；接收不代表资格通过或已采用 |
| BundleClosureWitness / DC16/DC24 | 根manifest digest/schema、完整member universe、逐成员id/role/length/actualDigest/schema/build/provider/Host域/证书与来源引用、必需依赖边及闭合证明、current qualification/use/deny refs、兼容/采用域、retention pins、检查版本/seq | 根及全部必要成员是同一已验闭包；不得分别解析catalog、codec、policy、oracle、资源的latest而拼包 |

来源机制按冻结的SourceTrustRecord分域，不把远端信封规则强套于所有本机目录。已审固定Host来源可以依可信collector、精确Host/已审安装根及payload身份、来源审查证据取得有界来源保证；单独路径、文件名、版本文字或hash不够。合法本地/离线来源没有签名时，仍可由已审来源机制与独立接受判定其实际保证范围，不默认为恶意，也不冒充密码学认证。来源无法确认的项留候选/待核，不能为了数量自动准入。

使用签名信封的已授权远端路径，固定信封格式、解析/大小深度限制、允许算法、签名覆盖的确切内容、可信key/role/scope及当前有效性。签名和消费者必须解释同一声明；重复字段、未知关键语义或模糊规范化不能一边验A一边消费B。同一key的重复签名不增加信任权重，候选附带新key、自签或TLS下载成功不能自行替换已审信任锚。轮换只接受已审连续性证明或明确的新信任审查；缺连续性不从更新包内部自举信任。不为此自动联网、安装依赖、生成私钥或部署新密钥服务。

来源认证不等于来源拥有全部素材权利，也不等于方法科学正确。SourceVerificationWitness之外仍核当前许可/用途、effects、逐操作支持域和独立资格；新软件/执行代码走正常产品升级，不作为metadata更新静默加载。远端没有受验来源机制时可登记候选并说明限制，普通完整合法旧链不因此失去功能。

### 28.2 接收水位、可信闭包与合法回退

只在完整验真、来源授权和声明绑定通过后，由原已验控制载体提交接收水位；未验高版本不能锁死后续正常来源。可靠版本关系必须按该source冻结，不能把semver、mtime、本机UTC或显示名称强称单调序列。相同可信版本且digest相同是重复接收，既不新计能力也不自动延长期限；相同版本但内容冲突隔离；低于已验水位的声明不得作为自动新更新采用。没有可靠版本语义时依内容身份/来源/时效检查保守处理，明确不能证明版本反重放，不伪造递增序号。

接收版本、逐操作资格状态、已采用manifest与revocationEpoch是不同轴。验真新版本可以推进接收水位，即使它尚未通过资格或canary；不能据此删除旧已验包，也不能用其失败重置水位。来源trustGeneration变化须有受审连续性/恢复记录，不能清空水位以接受被拒旧声明；任何重新初始化的保证和未知部分均须明示。

闭包验证读取精确成员并核实际bytes/length/schema及依赖身份；只保存成员名或某个包级hash不足证明所有成员齐备。跨provider/source引用分别保留来源和许可，不能借一方签名认证另一方内容。缺成员、资格域不匹配、Host/build不相容或依赖完整性UNKNOWN都阻止该候选采用；依赖环需明确有限闭合语义，不靠递归深度截断伪报完整。stage、候选资格与旧job所需成员按§27先持久pin，pin仅保护留存，不恢复已撤销使用权。

所有采用消费者一次绑定根manifest及其精确闭包，不在一个ExecutionLock里切换成员。原提交闸重核expectedHead、candidateDigest、closure、当前fence/control/deny、Host相容、许可和既定采用策略，耐久确认与readback后才报告Activated。读取时若内容可能被外部改变，须有受验不可变/拥有隔离保证及实际必要复核；不能拿旧hash见证当永远未变。真实效果入口仍核当前限制，不因准备时合格绕过撤销竞态。

回退形成新的ActivationCommitted，引用仍完整、已验且当前合法的旧manifest；它不降低接收水位、commitSeq或revocationEpoch，不恢复被撤销来源/许可/资格，也不撤销真实GIS/PS效果。若来源规则要求新鲜的采用声明，回退也须满足该要求或取得原授权体系内具体的受审回退决定；不能重放旧信封冒充新指令。旧job继续锁旧依赖，但每次后续派发仍核当前撤销/许可；无法恢复合法旧域时说明具体缺项，而不是暗换provider。

### 28.3 采用恢复判别表

| 当前可证明状态 | 恢复动作 | 不得作出的推断 |
|---|---|---|
| stage完成，无耐久ActivationCommitted | 留候选、旧合法head保持；重新核采用条件后可提出新commit | stage存在不等已采用，不由UI缓存补造commit |
| 完整commit耐久且成员闭包完整，head投影缺失/陈旧 | 从权威完整已提交事件重建同head投影并读回 | 不另切头、不增加第二次采用或能力数量 |
| commit的ack/返回丢失，提交状态可查 | 按原逻辑采用ID、expectedHead和digest查权威事件后对账 | 超时不等未采用，不盲目重复切换 |
| 两候选竞争expectedHead或旧fence | 拒绝落败提交；保留候选，依据新head重评影响/资格/预算 | 不能last-writer-wins或把候选嵌入已钉旧job |
| 头记录完整但必要成员损坏/缺失 | 阻该包受影响新派发/采用，保护证据及未决effects；只按原合法再生/回退配方恢复 | 文件存在或历史资格不证明当前可用，修复不重复加数 |
| 权威日志撕裂/中段损坏、版本不支持或水位/当前撤销不可证明 | 保留原件和诊断，按实际依赖影响域拒新采用/新业务派发；只有完整权威控制史、当前限制与受审恢复依据重建后再准入 | 旧snapshot不能证明损坏段无撤销；不把缺记录当空白/未执行，不降低水位或忽略停止意图 |
| 独立更新候选缺证，既有合法旧链控制与依赖完整 | 仅暂停相关候选/依赖；历史查看和完整合法旧路径继续 | 不能因一次更新失败全局封禁，也不能把旧链天然视为不受撤销影响 |
| 回退commit完整但在途effects未知 | head按新回退记录投影，旧effects按原策略隔离/对账，资源按原四轴保持 | 回退、lease到期或拒用不证明旧Host已结束或空间已释放 |

影响域无法证明时采用原保守边界；共用权威控制载体损坏可能影响全部依赖它的非终态任务，不能凭“这次只更新目录”缩小范围。恢复不通过重新执行Host或重新生成模型答案补历史；原intent/receipt、Stop/Resume与根预算继续有效。TimeValidityWitness控制期限，RetentionPin控制留存，两者都不替代当前来源信任和实际采用顺序。

### 28.4 原工作项、证明门与机制来源

WI11/E2承担来源采集与验证、接收水位；WI12–14/E2消费固定codec/准入/投影及包闭合；WI15/E2保留每操作支持域、当前证书和canary证据；WI30/E2在原兼容/资源锁/迁移范围内实现采用/回退与恢复。WI23/E3承担完整影响闭包，E1原WI02–04提供单writer耐久控制及恢复，主责和原完整DAG不变。PF01/D7确认来源机制、许可与版本语义实际存在；PF04/D14核底座竞争/提交故障切片，不能提前关闭WI30；PF08/D42验混版、来源重放、当前撤销、合法回退与旧任务全程。额外验真/闭包遍历/摘要复核、来源审查、Host/canary及C0工时计原WI边际，未知填null，不当免费功能或塞进应急四日。

2026-10-03核查[TUF 1.0.36客户端机制](https://theupdateframework.github.io/specification/latest/)与[SLSA v1.2产物验证](https://slsa.dev/spec/v1.2/verifying-artifacts)：前者区分可信连续性、版本/期限与快照绑定，后者核来源信封、subject实际digest及来源/build期望。三份子记录、单writer恢复和本机/远端分域为本项目工程推论；不声称完整TUF/SLSA实现，不直接采用其工具、联网/默认路径或新的安装权限，也不由这些规范授予科学资格。
