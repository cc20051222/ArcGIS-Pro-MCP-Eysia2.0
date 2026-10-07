# 能力演进、学习与全链路执行的技术合同

> V11-DATA-METHOD-AND-MEDIA-20261002。本文是内部模型与配方设计，**不是可运行MCP请求、已实现API或真实回执**；公开工具参数仍依据有效schema。原合同/配方在文末保留，新增改变通过SC裁定与实际资格后使用。

> V6增量说明：保留V5全部具体范围与表行；新增六项原J/WI内行为、四项提效实验与余期/停止/迁移合同。原门不减；软件实现与真实容量尚未证明。当前V6章节解释更精确状态，历史V5/V4章节保留来源。

> V7实现深化：V6全部原范围保留；新增七个内部机制与完整排程证书合同，实际工期UNKNOWN。当前章节对动态覆盖、PS测量写效果、收益/拒用/激活给精确实现约束；不新增公开工具数或产品完成结论。

> V8实现深化：保留V7全部范围；读集/三轴复用/科学视觉/用途闭包/同版补齐/契约投影六机制具体化，收益与60日容量仍待实测，非产品完成。

> V9实现深化：保留原全部范围；资格依赖/当前集合/联合准入/有限公平/合同工作包/有界接力具体化。产品未执行，新增成本与60日容量待实测。

> V10实现深化：保留原全部范围；事实粒度/科学支持域/偏好解析/批量参考/合同一致/保真反例具体化。原门不省，成本、真实容量与净收益尚未测得。

> V11实现深化：原范围/门保留，资料适用/缺口采用/方法比较/稳健性/语义展示/可访问媒介具体化；实际成本与完整60日容量仍UNKNOWN。

## 1. 合同层级与唯一调用路径

`可信元数据/参考资料/偏好 → 候选IR → 类型化计划 → policy/范围/许可/预算 → durable intent → Host效果 → 独立内容与状态检查 → durable receipt → 完整交付版本`。

能力目录和学习资源只能提供候选，不直接触发写入。所有入口包括MCP、Pro工作台、技能、更新、自动修复仍复用唯一Invoker；Composition为唯一注册，Shared不引SDK，Configuration集中端口/路径/超时/预算/开关；SDK对象访问经MCT，既有6511仍受限。固定额外provider需经已审Host接口，不能把原桥改成任意脚本执行器。

## 2. CapabilityPack与QualificationRecord

CapabilityPack至少含packId/version/provider/buildDigest/catalogDigest/contractSetDigest/codecRevision/effectPolicyDigest/resourceLocks/qualificationSetDigest/compatCanaryRef/requirements/limitations/activationPolicy/previousAcceptedVersion。每操作列canonicalId、semanticGroup、参数域、环境/许可、必要负例、oracle、事实和效果证据、独立接受引用。

CapabilityPack与模块DC16 CapabilityBundle为同一内部合同；DatasetProfile/DatasetDNA对应DC20，ReferenceStyleDNA/StyleDNA对应DC19。实施时由唯一版本化schema归一，名称别称不产生第二份真值或新的工具数。ReferenceAsset是资料身份，ReferenceGrant/DC18是允许用途，两者明确关联。

```json
{
  "designExample": true,
  "packId": "capability-provider-A",
  "revision": "candidate-001",
  "state": "GENERATED",
  "trustedSourceRef": "registered-source-A",
  "canonicalOperationRefs": ["operation-A"],
  "qualificationRefs": [],
  "activationAllowed": false,
  "limitation": "design example; no runtime qualification attached"
}
```

GENERATED不能标已可用，qualificationRefs为空不能激活；hash一致只证明包身份，不能证明参数/科学结果正确。实际build/依赖/许可/SBOM属于资格条件，不用仓库一个LICENSE替代分发组合许可。

QualificationRecord包含caseId/operation/domain/host/license/catalog/codec/policy/inputDigest/typedArgs/env/effects/intent/actualResult/CheckReport/negativeCases/receipt/reviewer/acceptedAt/validity。每个计入操作首次合法真实正例和必要负例都保留，不因同codec族有代表成功跳过其余操作。

## 3. 升级状态机与生产切换

`DISCOVERED → GENERATED → QUARANTINED/QUALIFYING → CANDIDATE_ACCEPTED → STAGED → CANARY_PASSED → ACTIVE → RETIRED/REVOKED`。

候选可自动生成；进入真实试验需拥有数据/许可/范围和预算，涉及准入/安全/公共契约变化需相应授权。CANARY在已批准的隔离域回放固定已知答案、兼容与故障案例，不拿未允许的用户工程试运行。

原子切换仅针对完整已验manifest引用，不代表跨Pro/PS/Office效果的全局事务。切换前持久化旧/新身份与采用记录；崩溃后读取有效manifest及journal做对账，候选目录存在不能推断已采用。新包失败停用并回到仍合法且已验版本；依赖撤销、科学无效或许可失效时，即使旧版本被锁也必须暂停，不继续执行撤销资格。

旧任务固定原catalog/contract/codec/method/provider版本，不静默漂移；恢复需再次核有效policy、许可、fence和输入。升级资源不携带旧用户批准，系统不能把自动更新配置当所有操作永久授权。

## 4. 自主适配的编译与effects合同

MetadataIR有参数序号、名称、方向、required/optional、默认表达、类型/复合域、multiValue、ValueTable列、filters、dependencyRefs、derivedOutputs、支持env和来源。CodecIR将值变为typed值，不用简单split分号或用字符串化覆盖所有语义。

EffectIR由可信规则和逐项审查得出readSet/writeSet/outputSet/derivedSet/sidecarSet/DDL/delete/external/fee、条件表达和未知分支。没有out参数不等于只读；RepairGeometry/CalculateField/Near等可能修改输入。未分类拒绝派发，LLM只提审查候选。

动态参数、默认env和真实许可由指定Host探测/资格，静态Parameter对象不能独自证明所有域。编译产生DTO/schema/表单/帮助/发现投影/测试目录，但它们是准备资产；结果oracle必须来自独立已知答案/已审专业规则或可信不同实现。[Esri Parameter官方说明](https://doc.esri.com/en/arcgis-pro/latest/arcpy/classes/parameter.html)

同一操作旧/compact/granular入口共用canonical与策略，生成名字不增数或绕准入。数千完整schema不反复传模型，先按硬前提筛候选再给完整单操作schema；小上下文不删掉单位、分母和风险。

## 5. ReferenceAsset、StyleDNA与DatasetDNA

ReferenceAsset有sourceDigest/type/sourceRights/specifiedUse/ownerScope/uploadTime/featureEvidence，不默认授权复制所有原素材。StyleDNA有canvasRatio/regionRelations/margins/colorRoles/typeHierarchy/legendPattern/lineRoles/referenceEvidence；DatasetDNA有schemaSemantics/unitTransforms/encoding/dateCalendar/crsSource/entityKeys/categoryMapping/validityExamples。

StyleIR只能绑定已允许的视觉角色，FactBinding与ProtectedRegion另锁真实地理框、分类/数值、比例/指北/图例和定量颜色。参考像素坐标不是地理坐标，OCR数值不是黄金，仿图相似度不是科学质量门。

用户可选择只学习抽象风格、学习数据组织、指定素材复用或指定真实分析输入；每种行为的来源、权限与验证分别记录。PSD/布局有可编辑结构时可读取真实层级候选，但字体/素材/ID/颜色/单位仍检查；不能声称任意图片可无损还原原工程。

## 6. PreferenceProfile与ExperienceRecord

PreferenceProfile分personal/project/organization/session，包含用途、显式/隐式来源、支持案例、采用版本、有效期和冲突规则。当前显式要求与有效科学/规范优先于历史习惯。含单位、方法、结果分母和原件写入的配置不因偏好自动改变。

用户遗忘会撤销后续偏好采用、移除相应派生索引/本地候选，并给结果回执；历史交付与审计不被改写。技能/规范/事实依据不是一般喜好；只撤销其学习引用，不伪造当时执行历史。

ExperienceRecord分别记录verifiedSuccess/verifiedFailure/hypothesis/userPreference。RepairCandidate和SkillCandidate必须声明适用域、固定方法/效果、回放样例/负例、最大修复、费用和回退。至少两份不同合格数据复放且独立核内容，不能用同一结果反复训练/验收后称跨域泛化。

## 7. 计划、预算与数据一致性补强

PlanRevision绑定inputSnapshot、methodSet、effectSet、OutputContract、dependencyDigests、resourceReservation与RootBudget。学习候选改变方法/范围/效果时产生新revision并重新采用；仅合法样式偏好仍记录来源和DesignPatch身份。

BudgetLedger包含模型使用、候选搜索、试验、修复、资格/升级和周期次数。超时未知费用预留、晚到generation采用门、重启/换模型不重置根预算。预测成本是估计；真实Host和模型usage分别记，不用估计收据伪作实际成本。

InputSnapshot对完整selection、字段/实体/时相、来源版本、锁/隔离机制负责；无法得到一致读取则拥有副本或等待。多成果必须引用同一FactTable版本，地图已成但Office失败只能领明确部分，不激活完整成功。

## 8. 三类因果键与效果回执

ArtifactKey锁输入/方法/默认/units/CRS转换/env/格网/NoData/随机性/Host/算法/codec/catalog和影响结果的模型；RenderKey锁FactTable、StyleIR/DesignSpec、字体/度量/ICC/DPI/位深/PS/UXP/编码器；EvidenceKey锁oracle/容差/验证器/适用Host/许可/policy。

对不影响分析事实渲染的验证判据变化可先reverify；其他规则变化沿完整影响闭包重算/重排/重验。删除合同、文件集合改变、改名、默认/env/字体ICC变化属于显式失效输入，不能只看mtime。

复用不可变产物与当前副作用分离：复制到新目录、地图/GDB修改、PS/Office保存仍具当前intent/fence/批准/receipt。执行journal不得先效果后补写；group commit只有durable ack成功才放行其动作，诊断日志批量与执行记录不是同一安全等级。

## 9. 参考风格迁移的完整配方

| 步骤 | 实现动作 | 独立检查/失败处理 |
|---|---|---|
| 资料接入 | 分离参考图、实际数据、指定素材和规范 | 来源/许可/用途不足不复制素材 |
| 特征提取 | 视觉区域、文字层级/色彩角色和DataProfile候选 | OCR/字段/CRS关键未知待核 |
| 风格采用 | StyleIR映射当前用途、项目规范及显式偏好 | 不沿用参考图的科学数值和错误比例 |
| 事实绑定 | 当前FactTable/分类/地理框与图例结构绑定 | 类别不匹配、缺项、遮挡即失败 |
| 布局求解 | 真实字体度量、约束候选、有限搜索 | 无可行布局解释缺口，不缩到不可读 |
| 真宿主渲染 | 固定命令、有界modal、逐descriptor观察 | partial/unknown先对账，不整段盲重发 |
| 交付 | 真实PSD保存关闭重开、同事实Office、完整revision | 编辑性/字体/ICC/事实与原完整门全部检查 |
| 学习回放 | 用户明确接受反馈，另一数据回放候选风格 | 未回放只记候选，旧任务不漂移 |

## 10. 更新、学习和发布的回执

TaskReceipt保留原task/job/step/plan/input/catalog/policy/owner/fence/intent/hostResult/effectState/CheckReport/预算/限制；新增adoptedCapabilityPack、styleReferenceDigest、preferenceRefs、experienceRefs和updateImpactRef。它们只解释采用依据，不表示由学习获得权限。

EvolutionReceipt记录sourceChange、before/after manifest、逐项资格/拒绝、审核与canary、采用/撤销/回退及原因。LearningReceipt记录参考类型、所学特征、保护约束、采用范围、反馈、回放和遗忘状态。界面只投影实际记录，不用模型总结制造“已学会”“永久领先”或全绿状态。

八周冲刺的每个学习功能必须有真实正例、关键负例、恢复与操作体验；格式/行业/参考学习未验证不能移到发行后仍称完整版。文末旧旗舰配方、计划批准、对账和受控探索继续全部有效。

## 11. V5源码复用与可靠提交合同

2026-10-02只读检查发现现有[IToolInvoker](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Core/Tools/IToolInvoker.cs>)、[ToolInvoker](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Core/Tools/ToolInvoker.cs>)、[WorkflowJobStore](<D:/ArcGIS-Pro-MCP 2.0/Source/Shared/ArcGISProMCP.Core/Jobs/WorkflowJobStore.cs>)。本轮仅SOURCE VERIFIED：接口、默认logger journal、manifest/receipt和fencing/journal字段存在。未启动产品或证明日志落盘/恢复/所有写入口覆盖；“intent persisted”的日志文字不证明durable提交。

ReliableIntentCommit补强现有接口：归一StableEffectKey绑定task/logicalAction、输入、方法、typed参数、逻辑目标与effects，ExecutionLock另绑定plan/payload/policy/批准预算/owner/fence。批准或policy版本改变不伪造旧真实效果不存在，也不以新的授权编号自动产生可重复业务写入。intent记录完整参数域/预计所有产物与两组身份；写入成功必须收到可回读的持久提交确认后才调用Host。存储失败或提交身份不符拒绝dispatch。刷新/rename/flush等细节依D盘文件系统及崩溃模型实测，不声称append或fsync覆盖断电的一切情况。

ReceiptCommit在Host结果和真实效果已观测后提交，保存CheckReport与产物/状态身份；失败/部分/未知亦保留对应记录，成功receipt还需内容/科学门通过。回执提交失败标待对账，恢复不重发原效果。HostReconciler根据操作类读取当前拥有输出/实际对象/文档与不可变manifest。EffectObservation的NOT_APPLIED/APPLIED/PARTIAL/UNKNOWN表示观测，ReconcileDecision的NoneProven/CompletedVerified/Partial/Unknown表示决策；APPLIED只说明效果发生，CompletedVerified还必须通过原内容/科学/支持条件检查。状态未知只能暂停相关写。不能把恢复动作生成新的随机InvocationId后当新的授权效果。旧任务格式保持读回和generation语义，必要迁移单独有receipt。

## 12. ProviderIR、逐项证据胶囊与批量验收

ProviderIR扩展现有MetadataIR：origin/version/build/descriptorDigest、canonical/semanticGroup、typedParameters、defaults、dynamicPredicateRefs、effects、requirements、fixedHostAdapterRef、oracleRef和limitations。不可序列化的SDK对象不进入Shared，provider raw descriptor保持只读出处。可组合codec只处理已验类型，动态域结果带具体输入/Host，不能一个静态schema覆盖所有域。

ProofCapsule是QualificationRecord的审阅载体，与模块QualificationEvidenceRow及研究记录Evidence Certificate为同一版本化证据投影，不新增工具或第二真值：逐操作包含case输入/参数、Host/许可/contract/policy身份、原始结果、实际效果/完整内容摘要、所有关键负例及assertion、oracle来源/实现身份、validator身份、原始产物链接、未决项和独立接受引用。摘要必须可重算，hash只核身份；检查器仍读取真实几何、值、字段、栅格/文档内容。

BatchAcceptanceManifest只聚合胶囊ID/digest和逐项判定，允许C0一次签一批已逐项独立检查的记录。**100%项运行冻结的独立检查，抽样人工复读是附加，不替代每项资格**。家族规则可以复用，但每操作实际正例与必要负例保留；同源生成expected或反射出来的supports=true不得过门。未知oracle、内容差异、专业歧义及异常全部逐项转审；系统性checker缺陷使受影响整批/既有资格待重新检查。实现端不能给自己写独立通过字段。

## 13. 不确定性与可行性合同

AssumptionRecord记录id、主张、来源、影响WI/门、当前值或null、验证实验、负责人、最晚验证日、失败动作与失效条件。核心N、合法候选池、Q_union、Host窗口、各WI剩余工程量、困难分布、净吞吐和Oracle成本必须有记录；没有实测不得填0或乐观数。

FeasibilityReport输入冻结范围和来源池、完整30WI DAG、工作/返工估时区间、实际日历与资源容量、可复用证据。输出可证明的前提、阻断、关键路径、有限资源排程、最晚日期范围与估计可信依据。null节点成本不能输出DEADLINE_PROVEN；一份离线可排的示例也不能升级现场已可交付。

OpenSourceAdoptionRecord记录项目/具体primaryURL/检索日、借鉴机制、版本与license证据、采用方式（机制/标准/依赖）、额外成本、当前环境与失败回退。复制代码/分发binary另核实际组件许可，不因机制借鉴就偷偷引入服务、端口、数据库、Python依赖或GPL代码。上游变动使本地资格待检查，不直接覆盖旧包。

## 14. 跨软件交付的效果配方

固定流程为：输入/事实版本锁→持久intent→Pro拥有素材→PS拥有文档import/design→真实状态检查→保存/关闭重开→Office同事实生成/实际读回→全部产物检查→原子激活交付manifest。manifest引用切换是完整交付版本原子性，不是Pro/PS/Office的跨进程事务。

PS在保存后失联时先核拥有路径与保存版本，重开检查和指纹成立才补receipt；同名用户文档、外部编辑、未知关闭状态不强行覆盖。部分Office已写不表示整链完成，继续合法缺项或生成新拥有revision。应用未知时拒绝全链盲重试，保留原产物和完整诊断。该设计借鉴durable执行，但外部效果语义由各Host验证。

## 15. V6不可变尝试、停止屏障与效果对账

`AttemptToken={jobId,logicalStepId,attemptId,ownerId,fence,executionLockDigest}`在派发前持久写入；回调携原token，不能读取后来被改写的JobState补造身份。StableEffectKey仍是V5业务效果身份，attempt/fence不是增加副作用的理由。新owner续作先核旧效果和旧writer占用，过期lease只证明租约过期。

停止有三个不同轴：StopIntent收到并持久确认；HostStopObservation确认各已派发调用结束/终止/未知；EffectReconciliation判定每个效果NOT_APPLIED/APPLIED/PARTIAL/UNKNOWN及其科学核实结果。收到请求不等于宿主停止；宿主停止也不等于没有写入。收到停止请求立即本地封闭本任务后续业务dispatch，记录尚未持久也不继续派新写。未知或部分效果阻止相关写入续发，允许不冲突的只读诊断。用户看到已核事实及未决集合。

有效停止屏障不会因对账成功自动解除。已经完成的效果可以补记录/核验，但后继业务写仍停止；只有用户或有权控制端的明确新ResumeDecision，且当前锁/批准/根预算、旧Host结束证明和lane准入全部成立，才能派发恢复后的业务。模型不得把“已证无效果/已完成”解释为用户愿意继续。

取消传播给GP/PS adapter，但取消后的观测使用单独有界只读诊断预算，不能重复使用已取消ct让对账立即取消。子调用receipt中的effect与ReconcileRequired逐项进入父步骤，不把工作流pending当成未发生。全系统总预算仍约束诊断；到限保留UNKNOWN，禁止无限等待。

## 16. 停止与交付激活的裁定顺序

采用原journal加单任务durable sequence/单写入提交闸，固定事件类型StopIntentPersisted、HostStopObserved、EffectObserved、OutputVerified、DeliveryActivated、AttemptRejected、JournalWriteFailed。候选成果使用拥有新路径；完整Checks成立后才能激活，旧writer即使写出暂存文件也不能借过期token更新当前成果索引。

在同一顺序载体上：已持久确认停止先于激活，则不能再激活该待发成果；激活已确认先于停止，则保留已完成成果并停止后继工作。请求在UI发出但尚未持久确认时，界面先显示“停止请求处理中”。durable日志失败后保持REQUESTED_NOT_DURABLE，本地后续新业务dispatch=0；不能报告请求已受理/效果不存在。仍可能已派发的调用纳入待核，不立即从零重跑；重启时无法证明控制历史完整的非终态任务先停业务写，补记录/对账并取得新恢复决定。部署需验证flush/原子替换及故障后读取，不以进程内lock声称跨进程、跨Host事务。

MCP取消是可选通知且存在完成竞态，progress也是可选、不能替代产物核验；新增协议行为只在实际协商版本支持时使用。[MCP取消](https://modelcontextprotocol.io/specification/2025-06-18/basic/utilities/cancellation)、[MCP进度](https://modelcontextprotocol.io/specification/2025-06-18/basic/utilities/progress)

## 17. 可迁移交付与学习角色配方

交付manifest逐件列相对路径、摘要、内容/结构检查、版本、编辑性、派生输入、外部引用和许可约束。仅在已授权D盘拥有目录另建副本，禁止移动原资产。新位置重开APRX/PSD/Office等实际载体，检查链接、字体/ICC、事实、图例/比例与编辑结构；便携成立范围明确，不承诺无Pro/PS许可也能编辑。BagIt校验思路只证明文件完整性，不证明GIS科学内容。[RFC8493](https://www.rfc-editor.org/rfc/rfc8493)

参考资料按用途授权并分角色抽取。STYLE学习布局，DATA_MAPPING学习结构，METHOD_HINT提供待审方法线索，OUTPUT_EXAMPLE用于期望说明；原始数据、方法合格和输出oracle另有证据。候选StyleIR/DatasetDNA绑定适用域，在独立新资料上迁移验证后才能进入学习采用账，失败保留差异与必要明确确认。

## 18. V7能力包激活使用一个耐久裁定源

BundleHeadCommit包含expectedHeadDigest、candidateManifestDigest、commitSeq、revocationEpoch、fence、所有成员资格与兼容引用。stage内容先在D盘拥有目录写入并验证，再由现有journal的串行提交闸核当前head/epoch/fence；选择**耐久ActivationCommitted事件为权威head，外部head文件仅可重建投影**。不得把独立JSONL追加与JSON替换说成两个文件原子事务。成功持久确认且readback同manifest后才报Activated；故障或回报丢失先对账，无法证明日志完整时拒新写。

旧任务读pinnedBundle；每次派发仍核最新紧急撤销/许可/Host相容，不能在锁内换provider，也不能忽略撤销。回退形成新的commit事件，epoch不倒退、旧效果不消失。未知二进制/软件依赖不自动安装；catalog更新限已审固定adapter/codec/policy域，不能以env白名单文件即时编辑绕过资格与激活。Composition仍唯一注册中心。

借鉴[TUF 1.0.36快照与更新版本绑定](https://theupdateframework.github.io/specification/latest/)以防混版本/过期来源；本项目仅采用明确机制，不声称完整TUF兼容或默认部署其密钥体系。摘要只核内容身份/一致性，不能认证来源；签名仅在可信密钥、来源、有效期与撤销校验通过时提供相应来源认证。二者都不证明功能/科学资格。MCP发现通知/分页只在实际协商支持时启用，通知不代表已验运行或准入批准。

## 19. 布局证书、学习采用与遗忘回执

LayoutFeasibility绑定required集合、每项placement、真实TextMeasurement、硬约束、软分数、未满足项及预算。首版关键文字优先已验point text/显式合法断行；采用paragraph text须另证全文无裁剪，不能降低多行文本支持。字体脚本域/中文/单位符号独立资格保留，预览和最终渲染继续原12候选/3首轮/5总预览/2修复/2最终渲染根预算，多页与重启不重置。

PairedAdoptionStudy把明确默认与自动优化分开。比较指标先冻结完整任务/科学/编辑性/零PS手工及确认、异常、墙钟、费用；C0独立隐藏资料不用于训练或调参。候选只在已证用途/项目作用域采用，失败回退完整已验业务路径，不删除显式个人默认或参考失败记录。

ForgetReceipt分LOCAL_USE_DISABLED、OWNED_DERIVED_CLEANUP_VERIFIED、EXTERNAL_OR_RETAINED_LIMITATIONS、UNRESOLVED。用户看见拒用即时生效与实际清理程度；提交删除请求不等于外部删除完成。保留最小必要撤销事实/摘要，不将私有原内容暗藏在经验或模型上下文。

## 20. V8输入一致性与三轴复用合同

StageDependencyManifest包含stageId/revision、supportedDomain、method/adapter/codec、canonicalConsumedFields、角色/实际选择、有效默认/env、上游key、成员覆盖、随机性合同、COMPUTE/RENDER/VERIFY/CURRENT_USE_RIGHT/MATERIALIZATION类别、closureCompleteness与独立完整性证据。TaskRevision不是唯一内容键，也不能为提高命中擅自丢掉字段。

SnapshotConsistencyCertificate包含对象身份、workspace/地图图层URI/查询/join/selection/timezone/locale、读取窗口、成员全集、内容/元数据/语义指纹、来源快照或版本保证、拥有副本receipt、跨源一致性与限制。来源支持的单调版本/原子快照才能支撑指定窗口的一致判定；普通前后hash相同、mtime未变或A→B→A不证明中间未变。逐源一致不自动证明跨源同一业务时点。需要同版而无协调能力时等待；不能宣称任意FileGDB或远端服务都可瞬间原子复制。

StageReuseDecision分别记录contentCheck、qualificationCheck、currentUseCheck和currentEffectCheck。先确认当前可读/来源grant与用途，之后才能复读候选；内容EXACT_VALID还需完整成员/真实语义/闭合依赖/确定性成立。历史资格可用于定位，当前资格/许可/policy/oracle/denyUseGeneration另判；当前不可运行不因旧内容好就变可执行。

ComputeKey变化重算受影响闭包；仅RenderKey变化复用合法事实后完整重渲染新拥有稿；仅EvidenceKey变化且不改变事实/方法/渲染可对原不可变成果重验。旧PSD磁盘hash不能证明打开文档无未保存编辑；未知dirty状态保留原稿并等待或另建新拥有版本，首路径不做未知层合并。

ReuseCertificate绑定旧成员/摘要与checks、精确目标spec/支持域、当前oracle/Host/许可/policy/grant、当前重验、拥有输出路径/fence/预算以及新materializationPlan。复制、导入、地图/GDB修改、PS/Office创建保存仍需要当前approval、durable intent、实际效果观察和receipt；没有新效果不伪造执行回执。未知旧效果先对账，内容命中不释放旧writer/lane，不代表作业SUCCEEDED。

借鉴[Bazel动作缓存](https://bazel.build/remote/caching)对动作输入和结果的区分及[DVC逐阶段参数锁](https://doc.dvc.org/user-guide/project-structure/dvcyaml-files)的粒度，采用是本项目工程推论；不部署Bazel/DVC/远端缓存，GIS/PS副作用无法由构建缓存机制自动证明安全。摘要不认证来源，更不认证科学正确性。

## 21. V9控制提交、派发和Host占用的边界

QualificationInvalidationDecision锁eventId、changedDependencyIDs/old-newIdentity、changeClass、conservativeBoundary、denyGeneration、dependencySnapshot及commitSeq。失效先安装本地deny屏障，耐久提交成功才报告已持久撤销；失败是REQUESTED_NOT_DURABLE，仍阻断新相关入队，重启未知控制史保持等待。最终派发与撤销走同一已验控制排序，短临界区核最新批准/输入/资格/预算/fence并交固定Host通道入队，不持锁等待长GP。

RuntimeAdmissionTicket锁rootJob/logicalStep/attempt、ExecutionLock、authority writer、host/run/boot identity、resource ledger seq、control/revocation代、各resourceKey/单位/需求/冲突域、根预算/chargeIds、approvedRoots、commit及释放依据。唯一准入协调者在同一权威journal提交组同时确认资源与预算；原intent必须另经已验耐久边界，才可dispatch。多个job文件各flush并不形成联合事务，进程内lock也不支持多协调者全局保证；首实现限制为已验单writer，不新造分布式数据库。

全部资源满足才预约，任何不满足都不持部分资源等待。AdmissionCommitted/intent/dispatch-started任一步确认不明，派发状态保守对账，不按缺文件重发。正式派发及实际进入MCT/PS modal/受控GP效果边界各核最新控制/Host身份；外部来源不可原子锁定的限制如实记录。派发前撤销可阻止新命令，已入Host队列/送达/执行则是in-flight；未支持真实Host再检查时不能承诺撤销后零效果。

| 轴 | 状态/证明 | 到期或取消不能推导 |
|---|---|---|
| Authority | VALID/REVOKED/EXPIRED/UNKNOWN | 宿主已经停止、旧命令不会迟到 |
| HostLane | NOT_ENTERED_PROVEN/IN_USE/SETTLED_PROVEN/UNKNOWN | effects正确或原交付完整 |
| EffectQuarantine | NONE/OBJECT_SCOPED/CLOSURE_UNKNOWN | 所有无关CPU/内存永远不能释放 |
| BudgetStorage | HELD/SPENT_KNOWN/COST_UNKNOWN/RELEASED_PROVEN | 取消即退款、留存文件不占空间 |

可信零派发或精确Host结束且未起旧命令不可迟到，才可释放相应物理lane。receipt缺失但Host结束可证时lane可释放，未知效果对象及依赖范围仍隔离；闭包未知扩大隔离。费用按真实账单核销，未知保留批准最坏成本；文件留存转storage allocation继续占用。授权lease到期只限制后续派发，绝不调用通用ReleaseAll。

取消响应与HostStopped证明分开；对账不自动Resume。elapsed测量绑定本次进程/boot/run及timer资格，UTC供审计；不跨重启复用ticks，不用计时结束证明Host结束。[Stopwatch官方说明](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch?view=net-8.0)支持elapsed计量，不自动提供本项目跨进程时间合同。[Hazelcast fencing说明](https://docs.hazelcast.com/hazelcast/5.5/data-structures/fencedlock)启发拒绝旧代写，但Pro/PS不天然识别任意token，已发生效果不能撤回。

## 22. V10数据粒度、表单及反例合同

FactGrainContract锁source/snapshot、entityKeys/orderedTypedKey、semanticGrain、measureKind/unit/可加性、population/eligibleDomain、weight/denominator、joinRecipe、missing/duplicatePolicy、MethodCard及原需求。JoinCardinalityWitness锁左右完整域/真实版本、key规范化、每key multiplicity、declared/observed cardinality、unmatched/conflicting实体、域覆盖/读取限制及receipt。资格域未支持的分配/预聚合不自动修复；合法转换仍是当前新计划与Invoker效果。

ScientificSupportContract锁operation/method/Host/build、inputGrid/actualGrid/CRS变换/snap/cell/mask、zone/类别支持、NoData/count/eligibleDomain规则、expected来源、numeric/category oracle类型、固定abs/rel误差及单位、排除原因/正负例和科学/效果门。无独立期望或期望域不完整返回UNKNOWN；类别不能因近似值就相同，单个总体和不验证局部、地理或实际效果。

ContractRuleSet锁IR/codec/dialect/vocabulary/validator配置、ruleId/instancePath、STATIC/DYNAMIC/SCIENCE/AUTH类别、missing/null/规范化/default/扩展字段policy、unsupported条款与兼容版本。default是提示还是采用值分别记录；ExplicitDefaultResolution来自原合法配方/DecisionRecord，绑定准确typed值及科学/effect影响，不隐式把数字串变数字、删未知参数、合并null与缺值。原错误码33及既有拒绝优先级保持。

ContractParityCertificate绑定原legacy/compact/granular/UI投影及共有静态规则全集、独立审定公开正负见证、规范值/接受拒绝/ruleId比较、动态deferred和缺口。UI能提前指出结构错误，动态FieldRef/版本/实际selection等仍在当前Preparation/dispatch核，科学门另验。同生成器与同validator全一致只证明一致性，不证明规则完备或科学正确。[JSON Schema注解说明](https://json-schema.org/understanding-json-schema/reference/annotations)明确default本身不填值；[Ajv修改数据机制](https://ajv.js.org/guide/modifying-data.html)启发显式控制选项，未安装这些库或改变现框架。

InteractionCoveragePlan锁有限因子/约束/风险t、mandatory原case、额外公开rows/可行tuple全集与预算；Witness由独立枚举核row→tuple及真实执行/oraclerefs。CounterexampleWitness锁不可变原fixture/失败谓词、科学前提/期望、effects、payload/env、允许变换、每次attempt与预算/停止理由。仅拥有副本上允许缩减，同故障与原科学前提仍成立；未知效果先对账，不由缩减器绕批准/intent/票据/receipt。只能声称指定策略内更小反例，不声称真正原因或全局最小。

## 23. V11资料、方法和展示合同的一次事实源

TaskDatasetNeed与DatasetOfferDecision锁每necessary criterion的语义、适用/未知原因、当前证据与排序；DiscoveryWitness锁adapter/conformance/query/pagechain/sourceSnapshot/assetOverride/extension/partial，不把搜索完毕当V8真实读集。GapResolutionProposal锁Need/gap、base input revision、候选输入/变换/明确方法差异、当前授权与根预算；GapClosureWitness只能在实际采用读回后出具，当前版本漂移重新评价。

MethodDiagnosticStudy各方法独立冻结；ComparisonEligibility锁evaluationTarget、populationMembers/完整性、时相、support/单位、metric/normalization/转换及失落/排除成员。ProviderSubstitutionWitness另锁原method等价域、真实build/effects/独立oracle，不与比较证书合并。SensitivityStudy锁baseline、varyingParameters/approvedDomain、constraints、transforms/方向/weights、original mandatory cases、budget/qualified invocations；ConclusionStabilityWitness明确FINITE_CASES或QUALIFIED_ANALYTIC_DOMAIN、ties/rankReversals/missing/failed/unknown和局限。

SemanticDisplayContract锁FactCell revision、值/单位/分母/percentageRepresentation、culture、termId/approved glossary、精度/roundingMode、loss与DisplayDecision。RenderedLexemeWitness用实际载体抽取/读回的结构化token与预计词元核对，单位/数值/术语分开；不靠从自由文本反解析一个数字相同来过门。翻译只作用合法术语和叙述模板，地理名称/ID/数值/类别语义保留，未知专业词保留原词与待证。

AccessibleMediaProjection锁OutputContract/media/member集合、语义角色/阅读顺序、语言/短长说明、冗余类别编码、实际字体/布局与科学保护。MediaCapabilityWitness逐实际成员绑定Host/build、tag/text/语言/阅读顺序/属性/编辑性/原视觉科学门；HTML、Word、PDF及PNG不互相冒充。明确需要同一文件的能力组合未支持时保持缺口，不自动拆分降低合同。

## 原具体范围合同的继承记录

下列为V3源文的具体行为/阈值/方法细则。历史措辞不构成本轮现场证据；旧目录和时点明确保留，新实现及60日目标以本文件前述V5章节为准。旧产品范围与质量门继续有效。

### 1. 能力记录的机器可读字段

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

### 2. 计划、批准与预算怎么绑定

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

### 3. GP类型/环境/effects的执行顺序

1. algorithmId/canonicalOperationId解析到固定可信catalog snapshot，来源/版本/准入digest一致。
2. Codec处理参数类型/顺序/多值/值表/复合/单位/字段依赖；不能拼任意源码，含分号/引号的合法值也不能错误split。
3. 输入/对象版本与完整选择复核；方法EligibilityReport适用且许可条件实际成立。
4. 计算条件effects与所有原位输入/派生/侧车/中间件。CalculateField、Near或RepairGeometry类输入修改不能因无显式out参数被当只读。
5. 显式workspace/scratch/extent/mask/CRS/overwrite及并发隔离；支持的环境集合有版本，禁止隐式继承上任务状态。
6. 批准/资源/磁盘/路径检查，持久intent成功后才调用真实Host。
7. 输出/修改状态/内容oracle和环境恢复检查，receipt持久化；unknown与失败进入对账。

预检或intent失败＝零宿主执行；宿主已成功但receipt失败＝产物保留/待对账，不自动重发。旧run/compact/granular路径同策略，生成工具名不能绕过分类。

### 4. 规划汇报旗舰链的实施配方

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

### 5. 科研多期分类旗舰链的实施配方

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

### 6. 规范建库与资料接入配方

DocumentProfile→ExtractedCell/Clause→Schema/RuleCandidate→冲突/单位/否定/例外识别→专业已知答案/坏例→采用RulePack→SchemaPlan→拥有副本建库→QC→同源合规覆盖报告。

关键OCR数值、缺CRS、脚注改变适用范围、应/宜/可/不得和不小于/不超过均不能猜。模型提取的规则只是候选；普通用户使用已验资源包。报告列已查/未查条款，不笼统“完全合规”。字段alias/default/required分别走真实可写合同，宿主不支持属性不得用另一个属性假装完成。

### 7. 每动作对账与失败项续作

ReconciliationStrategy应列proofOfNotExecuted/proofOfCompleted/partialCriteria/unknownCriteria、idempotentRetryAllowed、allowedCompensation和manualDecision。每类写动作有实际策略，receipt的存在不是唯一证明。

失败项续作读取同输入/计划/依赖版本及检查结果：已完成项复验后复用，未执行项可排队，部分/未知项先对账，输入变化形成新计划。不能把失败列表整体再次调用，不能靠删用户输入或改失败分母修好作业。

PS对账核对拥有文件/文档新身份、语义对象/Spec版本、实际动作/图层/保存结果；文件对账核对内容hash/结构/oracle；GDB修改核对预期schema/状态与作用范围。跨宿主没有全局事务，补偿只在已批准且证明安全的范围。

### 8. 受控方案探索

ScenarioStudySpec定义允许变化的方案变量、范围/离散候选、目标指标、硬约束、方法版本、预算/最大评估次数与比较合同。ExperimentPlan各候选有明确身份和相同输入/方法，统一Jobs执行；变量不包括偷偷改数据质量、删类别、换统计分母或放宽科学门。

例如设施候选位置/有限配置方案在真实网络前提下，比较覆盖、成本与公平性；权重与目标来自业务采用记录。输出可行方案及trade-off/Pareto候选，不把模型审美分或一句“最佳”当专业最优证明。

方法/变量或输出范围超出批准生成新计划。试验结果明确标试验；用户采用后才形成正式方案revision。固定预算/候选数/同一oracle防止无界搜索。方案比较属于X5/X6现语义，新增实现/接口走SC，不能把每候选算新增算法。

### 9. 持续改进与创新如何进入产品

FeedbackRecord记录已验证任务、明确用户接受/拒绝的Patch、真实故障和所用版本；不自动上传用户数据。RecipeCandidate/MethodRuleCandidate/TemplateCandidate从反馈形成待验证版本，说明改动依据、适用范围和回退。

成熟配方需满足：至少两个不同合格数据集/工程回放、正负例/方法/科学门、预算/效果对照与独立复核；涉及公共契约/白名单/依赖/安全变更再走裁定。通过后以不可变新版本采用；旧任务继续锁旧依赖，质量回退停用新候选并可回退。

模型可提出新思路、发现覆盖缺口或辅助生成资源草案，但不能自行安装代码、执行任意脚本、扩大effects或在线改核心规则。创新迭代用真实任务成功/质量/介入/时间成本验证，而不是持续增加按钮和名字。

### 10. 固定回执字段

每步回执含task/job/step/request、plan/input/catalog/policy/spec revision、owner/fence、intent/host result、sideEffectState、artifact/state evidence、CheckReport/oracle等级、预算消耗、最终状态和限制。模型总结只投影这些记录。

完整任务成功必须同时满足：批准范围成立、输入一致、方法有效、必需操作/产物验证、无严重科学错误、receipt与交付revision齐全。部分结果可以领取但标部分状态；不能由对话一句“完成”生成全绿卡。

## 24. 当前最终稿的资产物化与里程碑合同

本节为2026-10-03原位修订，保留上述全部合同、原范围及质量门。远端读取新增AssetAcquisitionRecord细则，见[领域合同的远端资产物化](../02_模块实现详稿（5份）/DOMAIN_CONTRACTS_AND_STATE_MACHINES.md)。目录发现、字节完整、格式可用、满足原Need四层分开；片段不能进入事实或跨任务缓存，完整件仍走原Invoker与输入依赖核查。

开发接受新增[MilestoneAcceptanceRecord](../../05_一指挥五执行计划/30WI工作分解与依赖.md#子交付里程碑与完整wi关闭)：CONTRACT只允许相应纯准备，VERTICAL_SLICE必须有完整限定前置及真实Host证据，FULL_WI保持原30WI的完整DAG和全范围退出门。D14验证真实接缝，不关闭完整WI20–22。任何子范围接受不扩大effects、授权、支持域或产品门。

## 25. 时间有效性与离线资格的有界合同

本节为2026-10-03原位规划补强。TimeValidityWitness是原WI10/15/23/28共享的值合同，补齐§21、环境快照与离线资格的判定，不增设时间服务、工具、软件依赖、授权或WI；全部实现与真实Host证据仍待验。

| 字段组 | 必须绑定的内容 | 不可替代的事实 |
|---|---|---|
| subject | witnessId、subjectKind/id/digest、dependencyIds、policyVersion、必要用途与作用域 | 一个缓存TTL不能覆盖许可、输入一致性和学习采用资格 |
| clockContext | bootIdentity及取得依据、processRunId、clockEpoch、实际timerBackend/frequency/resolution、suspendSemantics、qualificationRef | PID或由本机UTC相减推测的boot时间不是可靠身份；未知上下文不能续接旧ticks |
| relativeWindow | observedTick、checkedTick、ttl、elapsed上下界、边界规则、休眠/异常事件 | 持久UTC标签不能证明跨运行的真实经过时间 |
| absoluteWindow | notBeforeUtc/expiresAtUtc、期限来源及语义、trustedAnchorRef、锚定ticks、currentUtcLower/Upper、不确定度/增长上界与适用期间 | 本机wall-clock、HTTP Date或有签名的到期字段本身不能证明可信当前UTC |
| controlContext | authorityId/leaseId/owner/fence、controlSeq、denyGeneration、当前撤销要求及证据、Host/peer/session身份 | 未过期不代表未撤销；续租不改变旧命令身份或证明Host已停 |
| decision | relativeStatus、absoluteStatus、revocationStatus、identityStatus、affectedDependencies、原因及重新取证路径 | 显示用时间不能代替最终准入决定 |

各判定轴允许NOT_REQUIRED，但必须由冻结的用途/期限合同证明该要求不适用，不能把缺字段、取证失败或离线状态改写成“不需要”。TTL为0即无复用窗口；缺ttl是UNKNOWN而非永久。有效期一侧未声明时仅按来源合同处理已声明的边界，不自行补造无限期限。

首实现仅在同一已验clockContext内使用单调差值；ttl不填默认永久，负值、溢出、频率变化、未知计时域或边界不确定返回UNKNOWN。仅elapsed上界小于ttl时相对窗口可判VALID，elapsed下界达到ttl为EXPIRED，其余为UNKNOWN；审计UTC单列，不参与延长TTL。排队到期限制后续派发，业务elapsed不等于CPU活跃时间，租约有效性仍核owner/fence/控制代及用途。

绝对有效期只在有已资格来源/语义及有界当前UTC区间时判定：整个区间位于允许窗口内才VALID，整个区间早于notBefore为NOT_YET_VALID、达到expiresAt为EXPIRED，跨边界或无界为UNKNOWN。锚来自已允许且已验的现有证据渠道，绑定来源身份、收到窗口、抗重放、适用用途、期限和误差；从锚外推还需同一计时域与已验漂移/误差增长界。不得把任意联网响应、较精细时间API或本机NTP配置升格为可信锚，也不为获得锚新增联网或安装授权。

1. **回拨/前跳**：比较相邻审计UTC变化与单调经过时间，按已验容差记录异常；异常只能发现部分问题，不能证明此前wall-clock准确。相对TTL在合格计时域保持原起点；若绝对判定依赖的锚/误差界失效，仅相关依赖转UNKNOWN，既有可证明EXPIRED不因回拨自动恢复。
2. **休眠/恢复**：TTL按包含真实等待的原合同继续计，不暂停Stopwatch给资格续命。恢复后资源、Host/peer、许可与当前grant先重新探测；已确认计时域可判断相对到期，无法确认休眠计入或上下文连续则UNKNOWN。休眠没有证明未决命令已停，也没有释放物理lane。
3. **进程/Host重启或换机**：旧ticks不直接与新运行相减，原相对资格转待重新观测；绝对期限只有新可信证据或合同明确支持且已验的连续性证明才能重建。保留耐久enqueueSequence、firstEnqueued审计值与bypassCount，不以重启重置任务顺序/年龄；年龄未知时按原公平规则保留优先关系并取证。重取资格不隐式延长原队列截止、批准或租约；如需新准备版本，须显式记录原关系并走原准入。
4. **离线**：时间新鲜度、当前撤销和内容完整性分别判定。合格锚与计时域仍能证明有限窗口时可依原合同使用；需要当前在线撤销而无证据的依赖保持UNKNOWN。没有时效/在线撤销要求、完整已验且仍具本地当前合法资格的路径继续运行；不以全局“时间待核”封停全部离线功能，也不把“有缓存”当有效许可。
5. **租约到期或回复丢失**：只阻止相应后续派发/采用。Authority、HostLane、EffectQuarantine、BudgetStorage仍按§21分轴核验；到期/撤销/计时UNKNOWN均不证明旧writer结束，不自动退款、重发或ReleaseAll。

派发及真实效果入口沿原控制顺序核所需witness、denyGeneration与身份；tick只用于时间窗口，不能充当耐久commitSeq或跨线程因果排序。当前撤销可使尚未到期的对象不可用；新witness不能复活旧owner/fence或消除已发生效果。

公开负例归原WI10/15/23/28验收：本机UTC回拨一天不能让过期证据变新；TTL五分钟后休眠十分钟不能显示剩余五分钟；进程重启不能从新ticks得到负年龄再重置队首；有效期区间跨边界不能判VALID；grant尚未到期但已撤销仍拒绝；时间证据不明只阻相应依赖；过期lease且Pro/PS仍在执行必须保持lane/效果对账。另保留无时效依赖的完整离线正例。未运行这些用例前不宣称时间可靠性、离线能力或产品门PASS。

2026-10-03核查范围为平台时间机制：[Microsoft QPC说明](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps)说明QPC与系统UTC独立、计入Windows休眠时长；[Stopwatch说明](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch?view=net-8.0)说明实际后端/频率与elapsed测量。上述来源不证明本项目使用中的Host、跨重启连续性、许可或可信UTC已验；witness字段、保守分支和离线分域均为本项目工程推论，不据此改变现有TFM或安装依赖。

## 当前最终稿的来源与内容采用边界

§18的唯一ActivationCommitted裁定源保持。[领域合同§28](<../02_模块实现详稿（5份）/DOMAIN_CONTRACTS_AND_STATE_MACHINES.md>)细化来源认证、水位、成员闭合、采用入口及故障判别；不创建第二activeHead。参考/资料/模型/工具结果的共同ContentOrigin、CandidateConsumptionDecision与IntentAlignment定义见[智能编排](<INTELLIGENCE_AND_ORCHESTRATION.md>)，所有消费 adapter复用。来源可信、语法有效、独立科学资格、当前合法采用各有证据，不能互代；时间、撤销、保护根与效果对账继续消费原合同，合法已定范围不增加逐项人工确认。
