# 模型提供者、提案信封与不中断的标准任务

日期：2026-10-01。内部拟议合同；提供者接口、SDK和账户能力须真实验证，不以“兼容 OpenAI”推定所有功能。使用 OpenAI Docs 技能检索并读取官方文档，本文没有调用付费模型或写入凭据。

## 1. 模型只生产提案

模型路线为 `ModelRequest → ProposalEnvelope → 类型/语义/上下文检查 → IntentFrame / PlanPatch / DesignPatch 候选`。Host 执行仍经共同准备、批准、预算、Invoker、结果检查与回执。

拟议 IModelProvider 接口只负责读取 Profile、提交固定任务类型、返回规范事件/结果、取消语言请求、报告 usage。接口不接收 GIS 写权限，不调用 Pro/PS，不自行选择可执行脚本或新增端口。工具调用也先转换为 ToolProposal，再进入相同准入；不能让提供者 SDK 自动执行一套旁路工具。

OpenAI 的 function-calling 流程由应用侧执行工具；结构化输出能约束格式，但仍需处理拒绝、截断与语义错误。本产品始终继续校验参数、资料、方法及范围。[Function calling](https://developers.openai.com/api/docs/guides/function-calling)、[Structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs)

## 2. 三个内部合同

| 合同 | 必要字段 | 约束 |
|---|---|---|
| SP01 ProviderCapabilityProfile | provider/model/endpoint-family、adapter/SDK、模型或权重/tokenizer/量化身份、输出/工具/图像能力、上下文/usage/资源、实际测试域/时间/证据 | 每能力区分声明/探测/任务资格；账户不可用或限额不标已可用 |
| SP02 ProposalEnvelope | proposalId/requestId/logicalLanguageRequestId/attempt、logicalProposalSlotId/requestGeneration/activeAttempt、task/plan/input/catalog/schema revision、providerProfileRef、terminalStatus、完整 typed payload、usage/限制 | 完整有效提案才进入语义审核；活跃槽位原子采用一次，晚到/错版本/半 JSON 不执行 |
| SP03 ExposurePolicy | 本地/远端路线、允许发送的数据类别/范围、脱敏规则、原资料/图片权限、提供者与费用范围、有效期 | 确认模型连接不自动批准上传原表或地图；本地转远端需核对范围 |

终态至少区分 VALID_PROPOSAL、INVALID_SCHEMA、SEMANTIC_REJECTED、REFUSED、INCOMPLETE、TRANSIENT_FAULT、CANCELLED、UNAVAILABLE、BUDGET_EXHAUSTED。VALID_PROPOSAL 仍不代表可执行计划、更不代表实际执行。

UsageReceipt 记录原始 usage、adapter 解读、已知/未知计费信息、重试与根预算。费用未知时不能记 0；若允许的预算政策不能处理未知值则不提交请求。语言费用和 GIS/PS资源按根任务分别计入实际账本，不因换模型重置。

请求发出后超时、取消或缺 usage，仍可能发生计费。保留该 attempt 的未知费用状态与保守预约，不能结为 0 后释放占用；再次尝试须另预约允许的最坏费用，预算不能容纳时等待。结算主键是发送前持久记录的本地不可变 logicalLanguageRequestId/attempt；providerRequestId 仅在收到后绑定，不能用空 ID 加 attempt 合并多个并发请求。迟到 usage 必须有可核验归属再去重回填，归属未知保持待对账，不能猜；重复事件不能重复结账，预算对账不触发 GIS/PS 重执行。

Profile、Proposal、项目包和诊断只携带 credentialRef。凭据不得进入 endpoint 查询串、原始 HTTP 头转储、错误详情或 debug trace；具体载体须在已有授权范围内确定，不默认新增 C 盘存储。

## 3. 五类接入路径

| 路径 | 实现方法 | 不推定的功能 |
|---|---|---|
| OpenAI Responses | 官方 API adapter；分别解析结构化提案、工具提案、终态与 usage | 具体账户/模型/工具/数据策略由 Profile 实测，不写死“最新全能” |
| Chat Completions 兼容 | 独立 adapter 和 endpoint/profile，规范字段映射 | URL可用不等于支持 strict/tool/image/stream/usage全部特性 |
| 其他提供者原生 | 固定 HTTP/SDK adapter，显式映射其字段/终态/重试 | 不以换 baseURL 代替合同；新增依赖单独评审 |
| 已部署本地 HTTP | 受控本机端点、模型/权重/后端和能力实测 | 不自动安装运行器、拉取权重、启动任意 shell；资源未知不承诺速度 |
| 确定性标准任务 | 已验 RecipeFormSpec、方法/布局/办公固定服务 | 无自由自然语言理解承诺；不是新增一个模型 |

首个可交付切片只需一个真实远端 adapter、一个用户已有本地 endpoint 和确定性表单，再按相同测试扩展。模型名称和价格不固化为长期产品规则；Profile 与评测版本锁定，升级后重验发生变化的能力。

## 4. 输出合同与流式处理

先定义领域内完整 schema，再为提供者编译支持的 schema 子集；条件依赖、方法约束和作用范围仍在本地全量检查。不能为了提供者不支持某关键字而删除业务限制。严格格式要求/额外字段策略由实际 API 和模型版本决定，不把 JSON mode 当完整 schema保证。

流式事件在 UI 仅显示生成中草稿；按 `(requestGeneration, outputItemId/callId)` 分桶拼接，不能把同一响应中交错的不同调用参数合并。每项完整且响应终态有效后，才形成完整 typed payload 并审核。设累计 bytes、events 和 typed payload 深度硬上限，超限显式返回 INCOMPLETE 或 BUDGET_EXHAUSTED，不把截断结果当 VALID，不无界缓冲。tool arguments 的部分增量不能触发执行；缺结束事件、取消、拒绝或上下文不符均不采用。保留必要请求/版本/用量，不落密钥或未获授权的原始数据。

提案的事实数值来自 FactTable 引用，不让模型自行返回另一个算术真值。解释结果通过 ClaimBinding 与 DecisionRecord 检查；模型拒绝解释时可以使用已验确定性方法说明，不能将拒绝伪装成解释已生成。

## 5. 有界重试与晚到回复

重试只有一个责任层，或明确计入 SDK 的实际重试；请求总尝试数、总 deadline、费用预约和取消状态均受根预算控制。临时 429/503可依合法 Retry-After或有抖动的退避；quota/账户/费用或配置错误不反复重发。服务延时超过本任务剩余期限时延后或换合法路径，不能提早重试。[OpenAI rate limits](https://developers.openai.com/api/docs/guides/rate-limits)

本产品初始配置建议每个 logicalLanguageRequest 最多 3 次总尝试，包含 SDK 重试和坏 schema 修复；这是待实测的应用配置，不是提供者保证。RootBudget 另限制全任务总请求、费用和时间，换模型、计划修订或重启均不重置；新语言请求仍消耗同一根预算。SDK 与应用双重重试不能产生 3×3 等倍增。坏 schema 的修复提案同样消耗尝试/费用；根计划的两次修订与设计预算不因语言重试而重置。

requestId 关联语言传输，proposalId 标识候选，logicalProposalSlotId 约束一次提案采用，逻辑 GIS operationId 关联实际副作用。这些 ID 不能混为一谈。采用时原子复核 task/plan/input/catalog revision、当前 requestGeneration/activeAttempt、未取消且未过 deadline、槽位尚未采用；采用决定和槽位关闭须作为同一持久事务提交。切模型、取消或替换请求使旧 generation 失效，即使计划版本没变也拒绝迟到回复。不同 requestId/proposalId 的重复回复也只能在该槽位采用一次。通知或语言解释丢失可重新生成已验事实说明，不能重复 GIS/PS 执行。

## 6. 模型切换时的行为

| 时点 | 可继续内容 | 需要停止/重审的内容 |
|---|---|---|
| 草案 | 已确定的用途、对象、单位和采用决定 | 新模型提案仍审核；旧回复不覆盖新 revision |
| 已批准待执行 | 完全相同且有效的固定计划保留批准 | 方法/参数/effects/费用或数据发送范围变化按实际影响重审 |
| GP/PS 运行中 | 固定执行、查询、协作取消、对账继续 | 模型故障不触发宿主重发；动态环节缺能力时等到真实安全边界 |
| 回执/交付 | 观测效果、补 durable receipt、核验文件 | 不能为重写一段说明重新分析或生成第二份非幂等效果 |
| 本地转远端 | 已批准的合法摘要范围可发送 | 未获批准的原表/影像/地名/工程信息不能随连接自动外发 |
| 模型无图像能力 | 确定性布局/内容/字体检查照常 | 不能将不存在的模型视觉评分记成通过；必要检查没有替代时等待 |

“已批准固定步骤继续”以每步仍具备必要能力/输入/许可/预算为前提。若当前步骤依赖新的模型判断或必要生成内容，缺模型时必须用已验等价路径或等待；不笼统保证任意任务都不中断。

## 7. 无模型标准任务表单

SP07 RecipeFormSpec 包含 taskPurpose、recipe/version、输入角色、字段/枚举/单位/时间控件、条件必需项、已验默认及来源、MethodEligibility、PlanTemplate、OutputPreset、能力/资源要求与证据。

表单从已验标准配方生成：选择任务 → 绑定真实对象 → 数据充分性 → 补关键条件 → 同一 IntentFrame → 同一 PlanCompiler → 同一 Invoker/检查/交付。字段选择器读取实际 schema；对象选择不依赖用户输入正确工具名。不能要求用户写脚本或手工 PS设计。

标准说明和方法卡、布局约束、文字测量、Office 模板必须能在其依赖齐备时确定性运行；模型只是可选理解/表达增强。规划覆盖、科研分类变化、规范建库分别有无模型标准链验收；仍满足原方法、PS、数值和必需文件要求。

## 8. 最小验收工作

验证结构化成功、语义错误、拒绝、截断、坏schema、重复/晚到回复、账户不可用、临时限流、服务延时超deadline、usage未知、用户取消和本地转远端越范围。模拟 provider响应只验证应用处理，不能称某真实模型已通过；真实 adapter另按具体版本/账户留证。

在提案、GP/PS派发、回执、解释四阶段注入模型故障，检查实际副作用次数、事实/批准保留和预算。三个标准链在无模型但实际必要环境齐备时完成；缺 PS/方法/资料分别应得到真实缺口，不能用“无模型完成”掩盖其他依赖。

