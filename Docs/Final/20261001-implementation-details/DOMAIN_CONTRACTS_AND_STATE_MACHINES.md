# 领域合同、执行状态与恢复实现

日期：2026-10-01。内部拟议合同；必须经实际源码适配和合同裁定，不能当公开请求示例。配套[总入口](README_IMPLEMENTATION_DETAILS.md)。

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

