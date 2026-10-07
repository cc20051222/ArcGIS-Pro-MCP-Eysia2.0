# 模块接口、实际扩展点与开发工作项

日期：2026-10-01。拟议开发拆分，**没有派发 D 工单或修改产品代码**。工作项属于既有 U/I/R/GA 包，估工必须去重。配套[领域合同](DOMAIN_CONTRACTS_AND_STATE_MACHINES.md)、[机器设计](DESIGN_SPECIFICATION.json)。

## 1. 先以源码划清已有与待补

以下为本轮静态审查，未做故障复现或新运行测试；它说明开发优先级，不取代历史范围内的已验裁定。

| 已有基础 / 位置 | 可以复用 | 需要开发与验证 |
|---|---|---|
| [IToolInvoker](../../../Source/Shared/ArcGISProMCP.Core/Tools/IToolInvoker.cs)、[ToolInvoker](../../../Source/Shared/ArcGISProMCP.Core/Tools/ToolInvoker.cs) 38–105 | 校验后调用一次，取消/异常带 ReconcileRequired，回执存上下文 | 默认 journal 在 145–178 只调 ILogger；不是独立持久确认。需可靠 intent/receipt 与失败处理 |
| [StructuredFileLogger](../../../Source/Shared/ArcGISProMCP.Logging/StructuredFileLogger.cs) 126–129 | 诊断日志、可降级 | 吞 I/O 不适合作为写操作 durable intent 的成功依据 |
| [WorkflowJobStore](../../../Source/Shared/ArcGISProMCP.Core/Jobs/WorkflowJobStore.cs) 145–237、568–586、713–726 | claim、lease/fence、JSONL、Flush(true)、旧 JSON | SaveCore 602–614 / SaveJournal 618–640 吞保存失败；630 先更新 WrittenItems，636 才追加。需可靠确认与失败补写 |
| [FolderWorkflowTools](../../../Source/Shared/ArcGISProMCP.Tools/FolderWorkflowTools.cs) 1213–1219、1285–1346 | shard、子步骤共用 Invoker、已有项跳过 | 未消费子步骤 LastInvocationReceipt.ReconcileRequired；取消中的项可回 pending。需先对账再续作 |
| [JobManifest](../../../Source/Shared/ArcGISProMCP.Core/Jobs/JobManifest.cs) 56–89 | artifact/revision/fence/receipt DTO | 增输入、批准、预算、逻辑步骤、effects、oracle 与交付引用；保留旧载体 |
| [ToolValidatorPipeline](../../../Source/Shared/ArcGISProMCP.Core/Tools/ToolValidatorPipeline.cs) 19–31 | 只读、required、可选路径检查 | 条件参数、完整类型/范围、方法/批准/效果/版本统一准备；保留既有拒绝优先级 |
| [D086DesignTools](../../../Source/Shared/ArcGISProMCP.Tools/D086DesignTools.cs) 550–635、799–831 | PNG/PDF/SVG staging、hash/manifest 和素材目录发布 | 字体/ICC 未验；锚点为 manifest shape 检查；dataUpdates 是声明。补实际内容检查和更新语义 |
| 同文件 1115–1163 | 六场景关键词建议、缺输入/工具提示 | 不是语义理解、方法选择、资源可行性；成本不能只数工具 |
| [PsChannelHandler](../../../Source/Shared/ArcGISProMCP.Server/Internal/PsChannelHandler.cs) 291–356 | session/replay/fence、信封验证与 echo | 文档认领、实际批准、file grant、业务 peer、宿主单写/取消待实现 |
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

这张表是规划依赖，不是并行写域授权。代码与合同研究可并行；真实写切片必须经过 T0、输入/批准和 WI10 资源预算门。机器设计给 WI14/17/19/21 的实际写路径显式补可靠性/资源前置，WI26 的真实建库依 WI14、完整版本交付依 WI22。规则草案可较早开发，不可把草案完成报成写库交付完成。现有任务先遵守有效派工及四线协调；真机/源文件/Host 串行限制不因规划表允许并行而取消。

每工作项交付模板：目标与范围、现有源码差距、内部合同/公开合同影响、own 输出计划、正例/关键反例/故障点、独立 oracle、环境与许可、兼容/回退、证据索引与 NOT VERIFIED。评审依据最终行为，不能仅验类名存在或测试数上升。

