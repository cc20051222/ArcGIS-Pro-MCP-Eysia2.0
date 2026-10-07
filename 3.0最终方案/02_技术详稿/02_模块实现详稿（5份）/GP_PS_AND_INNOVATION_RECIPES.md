# 全量 GP、真实 Adobe 执行与六项创新实验

> 修订：**V11-DATA-METHOD-AND-MEDIA-20261002**；日期：2026-10-02。状态：**完整范围创新设计 / PASS CANDIDATE**。八周加四天为条件工程目标，原所有功能与质量门保留，未实施或实测。继承段保留旧功能合同，V5章节更新组织、自动化和实现路径；旧源码/运行快照仅作历史来源。V3规格中的180项/周及原工期不构成V5承诺，V5以D15–42四周资格窗、两池交集去重后的完整750–1275净项/周区间及主计划为准。

日期：2026-10-02。技术实现建议与采用门，**不是运行证据或额外能力计数**。配套[工作项](<MODULE_INTERFACES_AND_BACKLOG.md>)、[状态合同](<DOMAIN_CONTRACTS_AND_STATE_MACHINES.md>)。

> V6增量说明：保留V5全部具体范围与表行；新增六项原J/WI内行为、四项提效实验与余期/停止/迁移合同。原门不减；软件实现与真实容量尚未证明。当前V6章节解释更精确状态，历史V5/V4章节保留来源。

> V7实现深化：V6全部原范围保留；新增七个内部机制与完整排程证书合同，实际工期UNKNOWN。当前章节对动态覆盖、PS测量写效果、收益/拒用/激活给精确实现约束；不新增公开工具数或产品完成结论。

> V8实现深化：保留V7全部范围；读集/三轴复用/科学视觉/用途闭包/同版补齐/契约投影六机制具体化，收益与60日容量仍待实测，非产品完成。

> V9实现深化：保留原全部范围；资格依赖/当前集合/联合准入/有限公平/合同工作包/有界接力具体化。产品未执行，新增成本与60日容量待实测。

> V10实现深化：保留原全部范围；事实粒度/科学支持域/偏好解析/批量参考/合同一致/保真反例具体化。原门不省，成本、真实容量与净收益尚未测得。

> V11实现深化：原范围/门保留，资料适用/缺口采用/方法比较/稳健性/语义展示/可访问媒介具体化；实际成本与完整60日容量仍UNKNOWN。

## 优化实现：生成、资格与Adobe固定批次

GA01快照→规范值模型→合同/表单/文档/发现及注册贡献生成→通用codec＋家族规则＋逐操作差异→真实资格任务。编译期增量生成只依稳定合同，不与Host运行时发现混同；缺effects/许可/复杂条件保持拒绝。完整codec两条Host入口同时验证对象绑定与env，不保留Layer→名称的隐式歧义。

资格case按canonicalOperationId分层，先并行准备不可变fixture/独立oracle，写case各自拥有副本。一个受验payload绑定的Host窗口可执行多个准入case；不按每case重建/重安装，仍保存各自正例、关键负例、oracle、许可与receipt。工具族代表例只验证基座，首次真实运行和独立复核仍逐操作；case失败留在分母。

PS CommandPlan在写lane外准备素材与定向属性读取，精确document/layer ID；将固定descriptor组合为有界modal段，每项检查error/部分效果与控制响应。global单写、取消/重连对账、保存关闭重开和保护区/字体/科学颜色检查保留。首版更新采用新PSD完整渲染，外部母版原样留存；cache只复用已验纯素材。

增量构建和测试选择覆盖删除/改名/TFM/SDK/策略与公共合同失效。FsCheck/Hypothesis只用于受限支持域和fake故障探索，科学oracle与LIVE审查另验；新依赖后续评审，本轮未安装。方法出处与采用门统一见[优化总方案](<D:/ArcGIS-Pro-MCP 2.0/同步/3.0项目规划/05_效率优化/质量不变的效率优化方案.md>)。

## 1. GP 自动适配器的编译流水线

`可信来源 → metadata snapshot → ParameterContract → typed Codec → ConditionalEffects → AdmissionManifest → PreparedInvocation → Host → oracle → evidence`。

“自动适配”指避免为每个工具重复手写入口和通用参数转换，不能省略专业适用性、复杂类型、条件 effects、许可和真实验收。GetParameterInfo/Parameter 的 metadata 是起点；[Esri Parameter 文档](https://pro.arcgis.com/en/pro-app/3.5/arcpy/classes/parameter.htm)包含多值、值表列、依赖、复合过滤与派生参数，也指出条件必需参数需验证逻辑，静态 required 字段不能替代它。

### A. 采集与差异

固定可信 Esri 目录和 Host 版本；每工具保存 canonicalOperationId、参数位置/名称/类型/方向/多值/依赖/过滤、支持环境、来源/许可探测、metadataDigest。采集失败/动态字段未完整暴露单列，不从统计分母删除。

未知 .pyt/.tbx 的加载可能执行代码；不能因“只想读取 metadata”就自动加载用户任意路径。扩展来源先审查、隔离和授权，登记真实新增操作，不用别名扩大数量。

### B. 七类 codec 合同

| 类型族 | 内部规范表示 | 正例与关键反例 |
|---|---|---|
| 标量/枚举/范围 | 类型化值、版本化范围/枚举、missing 与 null 区分 | 数值边界、locale 小数、非有限数、误把字符串当布尔 |
| 对象/路径 | 稳定 DatasetRef/LayerRef/OwnedOutputRef，Host 内解析 | 同名图层、GDB 成员、Unicode/空格、路径逃逸、陈旧引用 |
| 单位/时间/CRS | 原值+规范值+单位/时区/CRS 转换决定 | 米/度、日期歧义、缺 transform、误用显示 CRS |
| 多值 | 有类型的数组，声明顺序与集合语义 | 合法分号/引号/空值、重复元素；禁止朴素 split |
| ValueTable | 列 schema + typed rows，列依赖/过滤独立 | 列数量/类型、字段关联、空单元、合法特殊字符 |
| FieldMap / 复杂映射 | 声明式源字段/目标/merge rule/类型与长度规则 | 同名不同类型、截断/冲突、无法无损转换；不能接受任意源码 |
| 复合/条件/派生 | 显式选择分支、条件必需/互斥、派生预期状态 | 条件变更后旧参数失效；派生 out 不代表没有 in-place effects |

每 codec 声明 supportedDomain、encode/decode、默认值/省略语义、失真条件、Host 适配与测试。仅支持一个子域时不能标整个参数族或整工具完整支持。字段默认、格式、脚本表达式、SQL 等只开放已审查的结构化子集；模型输出自由脚本不能作为通用逃生口。

### C. 环境、effects 与执行

环境从明确声明生成，记录 workspace/scratch、extent/mask、输出 CRS/变换、overwrite、栅格格网及工具实际支持项。Python typed handler 可用固定 EnvManager 作用域；[Esri 文档](https://pro.arcgis.com/en/pro-app/3.5/arcpy/classes/envmanager.htm)说明所传环境在作用域结束恢复。仍要按实际通道检测泄漏与并发共享，不能把它当进程隔离或所有环境的自动恢复证明。

共享环境的操作使用相应 lane 串行，或在已验隔离机制下并发；不为追求速度新增任意 Python/6511 旁路。ParallelProcessingFactor 只用于实际支持的工具，分块/并行需整体等价与资源实测。

ConditionalEffects 列参数谓词、操作范围、原位输入、派生/侧车、中间产物、删除/覆盖、会话写与外部服务成本。未知 effects 拒绝写执行；准入/全局只读/批准对 compact、granular 和原 run 完全一致。

### D. 数量与质量账本

逐工具维护 DISCOVERED、CONTRACT_COMPLETE、ADMITTED、RUNTIME_VERIFIED、INDEPENDENTLY_ACCEPTED；同时记录环境可用性和参数覆盖域。完整类型合同目标至少 95% 的发现全集，可信全集 100% 发现，目标实际能力数 ≥max(2100,ceil(1.1N))，均为未来目标。

完整实证至少含真实正例、内容/状态 oracle、关键负例、Host/许可/codec/catalog/policy 版本及独立复核。工具族代表例可验证基座，不能代替每操作实证。发现数量、全部安装可用数量、许可下可用数量、已验数量分开展示。

推荐 30–50 工具先验证参数/准入/Host 基座，后按工具箱和参数难度分层扩大；完整数量门仍逐操作。旧 list_geoprocessing_tools 的白名单语义保留；全目录元数据视图和版本接口需 SC02，不能直接把 53 项变成全目录并称兼容。

## 2. Photoshop 真执行：从路由健康到交付

### A. 每次业务动作的顺序

1. 探测真实 Photoshop/UXP 版本、插件/session、固定能力、协议和资源包；echo 只证明通道。
2. 核对 CommandPlan 的 plan/input/spec/approval 引用、固定命令及作用域；未知/越权命令拒绝。
3. 解析 job-owned file grant 和 DocumentOwnership；不以 activeDocument 代替拥有文档身份。
4. 预约 Photoshop 宿主全局写队列；持久 intent 后才发业务命令。两文档也不能默认并行 modal 写。
5. 在实际 executeAsModal 内对精确 document/layer identity 执行；每阶段保存独立观测，不靠回显宣称改图成功。
6. 检查语义图层、文字测量、变换/裁剪/遮挡/全局效果、量化颜色与同源图例；运行有限修复。
7. 保存到新的拥有目录，关闭/重开验证，重绑定文档身份，核验母版/预览/最终文件内容与可编辑承诺。
8. durable receipt 与候选交付记录完成后再汇总状态；中断按动作策略对账。

[Adobe executeAsModal 文档](https://developer.adobe.com/photoshop/uxp/2022/ps-reference/media/executeasmodal)说明修改 PS 状态需要 modal，且同时只有一个插件使用该状态；history suspension 只管理指定文档的历史，不能当跨文件/GIS/Adobe 事务。取消后不能随意继续发修改命令。

### B. 文档与文件身份

DocumentOwnership 包含 task/revision、逻辑文档、当前宿主会话/docId、持久文件引用/hash、spec/语义图层映射和归属证据。docId 是运行期身份，保存重开后必须重绑定；名同或路径同不能代替正确状态验证。

持久 file token 的有效性依实际 UXP/文件状态，需显式检查；失效后获取新 grant，不把令牌文本当内容证据，也不在日志/项目包暴露凭据。超出已有访问范围属于新授权，不静默扫描/写入其他用户目录。

外部改过的 PSD 保留原件。v1 默认新版本全量重渲染；“合并外部改动”若未来实现，需显式范围/差异/oracle，不能本稿自动承诺。全量重渲染也必须保留科学事实与可编辑设计层。

### C. 排版与保护怎么实现

TemplateManifest 定义语义槽位、网格/边距/字体候选、图例容量、多面板关系与保护区域。真实文字测量和当前字体可用性驱动布局；修复动作只来自有限 RepairRule，如增加列、调整非科学区域间距、换预验字体、在允许字号区间缩放文字。

科学区域不允许非均匀变形、改变定量色带、缺项图例、无比例地裁剪、遮挡以及父组/全局效果污染。必要等比缩放需同步验证图框/比例尺等关系；只比较顶层图层属性不够。核验同时使用语义状态、原 GIS 精确素材与实际渲染检查。

根任务的候选/修复预算延续 V2：候选≤12，首轮预览≤3、总预览≤5、修复≤2、最终渲染≤2；多页、重启、换模板不重置。超预算时返回具体未满足项，不能不断生成直到出现好图。

### D. 二十接口与模板证据

20 项已有注册合同逐项建立真实正例/关键负例/对账策略；读类接口也必须绑定真实 peer、正确文档和读回状态。PSB、大尺寸、多页、字体、ICC、图层/智能对象/蒙版等按明确支持范围逐项验，不用 2×3 小图代表全支持。

PSD 内可编辑文字/设计层、嵌入的精确 GIS 栅格、独立 GIS 矢量输出分别说明。导出图像通过不等于母版可编辑；母版存在不等于已正确保存重开。

## 3. 六项创新实验的实现与采用门

下列 EX01–EX06 属于 WI29 及既有 I/U/R 职责；是候选方法，不是新增六个工具或已达成指标。实验数据、对照和分母先冻结，失败与超时样本不得删掉。先做最小有价值切片，再决定采用与额外专业内容工作量。

| 实验 | 核心实现 | 对照/采用证据 |
|---|---|---|
| EX01 关键问题选择器 | 在 DataAdequacyReport 上建立“未知→方法/分母/输出影响”图，合并能一次解决多依赖的问题；只对低影响样式使用默认 | 对照固定问卷；必要歧义捕获不降低、关键未知执行=0、重复问题与确认时间下降；未校准不虚构概率 |
| EX02 数字出处面板 | FactCell/ClaimBinding 解析为原值、公式、分母、单位、输入/方法/期次定位；记录真实 DecisionRecord | 注入错分母/单位/混期/空值/无依据精度；全部关键错例被拦截，正确任务能完成；面板说明覆盖局限 |
| EX03 更新影响预览 | 根据版本化依赖 DAG，先算重算/重排/复验/复用闭包和费用，再排队新 revision；保守默认失效 | 对照全部重跑和故意漏依赖；复用成果与全重跑等价、关键漏失效=0；不同变更逐类验，省时不是唯一门 |
| EX04 有界多方案探索 | ScenarioStudySpec 固定变量/输入/方法/约束/目标/最大评估数；先可行性，再给成本/覆盖/公平等权衡 | 对照固定候选人工流程；不超预算、不放宽硬门、Pareto/可行集合算术可复核；不宣称全局最优 |
| EX05 自动排版修复 | 用实际文本/图例 bounds 与保护区域求冲突；固定 RepairRule 按优先级提出 typed patch，真实重渲染核验 | 注入中文长标题/密集图例/字体缺失/遮挡/父组污染；科学门不退让，修复次数有上限，零 PS 手工设计按全任务测 |
| EX06 新项目适配技能 | SkillFit 对输入角色/字段/单位/依赖与方法前提做显式映射，RecordingGap 显示非系统记录缺口；生成新技能候选 revision | 至少两个不同新数据集回放、错映射负例、版本不兼容；不携带旧授权/路径/凭据，不以“录过一次”称普遍技能 |

模型可以辅助提出假设、候选布局和修复理由，最终采用依据必须来自结构化状态/规则和实际检查。布局求解失败不删除类别、缩小数据或放宽科学门；方案探索失败不删失败候选重算成功率。

## 4. 创新如何成为默认能力

实验版本锁定；资源作者给适用范围/失败边界和黄金反例，独立审查真实新数据回放；通过后发布不可变 Recipe/Rule/Template/Skill 新版本，保留旧版与停用方案。用户偏好仅在明确保存时进入偏好库，不能把一次临时改稿推广为长期规则。

每项实验至少展示：完整任务成功率、严重科学错误、PS 手工设计、业务确认/异常次数、总耗时/费用、恢复成功和支持范围。没有达到采用门时保持候选或停用，不让“持续创新”变成线上自动改规则、代码或白名单。


## 5. V5核心能力策略：大数量必须是真方法、真资格

保留旧GP门`K=max(2100,ceil(1.10×N))`，N为冻结竞品同条件的独立规范GP操作数；N未完成现场盘点，不能把“1900+”当恰好1900。新版另提出断档数量候选目标`K*=max(3000,ceil(1.50×N_semantic))`，N_semantic是在同支持条件下去重的真实语义能力数。K*是新增跨可信来源语义目标，尚未有真实catalog证明可行，不能当保证或拿它替代旧K的GP数量门。

K_sem与N_semantic的核心分子限定为核心GIS、空间计算和转换治理的独立语义能力；PS、Office、UI、安装及学习管理另账，不计入3000核心分子。

同一Esri全集共享实际工具上限，自动生成再多MCP入口也不能创造新工具。真正扩展须来自经过可信来源、许可和固定adapter审查的不同科学方法/支持域，并逐项实证。相同方法换provider、参数组合、别名、工作流配方、安装包和示例都不叠算语义能力。一个新provider若只有等价实现，可以增加实现可用性和兼容冗余，但语义数量不增长。

### 5.1 六本数量与支持账

| 账本 | 分母/证据 | 对外可说什么 |
|---|---|---|
| 可信发现全集 | 固定来源/版本的完整catalog，采集失败单列 | 已发现多少规范操作，不能说全部可执行 |
| 参数合同完整 | 每操作必要类型/条件/env/effects完整，支持域明确 | 哪些可生成完整请求，不能说真实运行通过 |
| 当前准入/许可 | 当前Host、policy和license探测 | 哪些当前允许且环境可用，不能用历史许可替代 |
| 真实资格 | 首次真运行、关键负例、独立oracle、精确版本 | 哪些在所声明域已实证，不能扩张到未知参数域 |
| 独立接受 | C0接受逐行证据、失败分母固定 | 哪些可进入已验产品能力包 |
| 语义去重覆盖 | MethodCard、输入输出/域关系、等价证据 | 有多少真不同能力及任务覆盖，不能用wrapper名数冒充 |

发现目标100%、完整必要合同目标≥95%、原数量目标与目标支持域覆盖要求全部保留。某机器无某扩展许可属于环境限制，历史资格和当前可用量分别显示；候选数量、已验数、当前可执行数不能合并成“支持3000+”。同时维护格式、行业、PS、Office、任务完成、质量与恢复覆盖，数量不能掩盖其他维度退步。

## 6. 自主更新适配流水线的逐步实现

### 6.1 可信来源与差异采集

固定SourceTrustRecord列出允许的内建目录、provider版本、采集adapter、更新周期、签名/内容摘要和许可审查。事件可以来自Host版本变化、已审资源包更新或用户手动“检查更新”；定时检查只读允许目录，不扫描未知用户资产、不自动加载上传工具箱。采集生成不可变CatalogSnapshot，用规范ID和完整字段diff识别新增、移除、条件/默认值/effects/许可变化。

metadata不充分的类型记录`qualificationReason=CONTRACT_INCOMPLETE`，不作为availability枚举值。模型可以建议codec和文档说明，但不能把猜测放入生产合同。读取未知.pyt/.tbx可能执行代码，需先审来源并在授权隔离环境以固定adapter处理；“读取metadata”不是任意代码权限。新增动态代码adapter属于正式产品变更，经过完整评审/独立接受，不作为运行时自动下载脚本。

### 6.2 合同生成与固定参数编译

合同生成器输出schema、DTO/codec绑定、表单、文档、发现投影和注册贡献，最终仍由Composition.BuildRegistry()唯一组合。编译期生成和运行时catalog是不同对象：前者改变代码需受验payload，后者只在已编译固定codec/adapter能力内扩展数据目录，不生成第二注册表。旧compact/granular/run入口通过同PreparedInvocation保持同参数同准入/拒绝，旧list语义不静默改变。

七类codec的往返、default/missing/null、条件依赖和特殊字符测试由共享基座生成，但工具实际字段域、原位effects、派生状态、环境和许可逐项保留。新类型优先形成可审候选和所需fixture，不自动降级字符串绕过语义。复杂SQL/表达式/FieldMap只能来自已审结构化子域，不给模型自由Python、JS或6511旁路。

### 6.3 effects、资源、许可和方法前提

每操作ConditionalEffects列读/写对象、输入原位修改、侧车/中间件、地图状态、外部服务成本和条件谓词；静态方向不是只读证明。PreparedInvocation绑定可信catalog、codec、policy、method、输入、approve和rootBudget，在资源等待后dispatch前再核对。对已授权范围外的新增effects只能停止并要求具体新决定，不能为了自主适配隐含扩大批准。

GP共用环境写、同GDB写、MCT和许可按实际lane约束。固定EnvManager作用域只是一种恢复机制，不能证明跨进程隔离或所有状态自动回滚。每工具ParallelProcessingFactor或分块策略只在其支持域、完整等价oracle和资源测量通过后启用，禁止统一强设100%或盲目切片。跨机器资格池可提高吞吐，但只在真实可用、合法许可、固定协议和授权资源下成立，不把五AI视为五个Host。

### 6.4 首次真实资格与独立oracle

QualificationFactory先按参数族/工具箱/方法难度构建拥有fixtures和受限用例；准备可以并行，首次真实每操作正例仍必须运行。正例检查实际内容/状态/单位/几何/记录、支持域和effects；关键负例包含非法或缺条件、错误对象/域、许可阻断、越界或未支持类型以及适用的不良科学输入。负例不必胡乱穷举所有参数，但必须覆盖该操作最重要的风险和声明支持域。

oracle来自独立手算、小型可证明规格、可信参考结果或不同实现的明确对照，并记录其局限。用同一待测算法生成expected再比较自己不能证明科学正确；纯性质/守恒也不能替代所有内容预期。C0可以用已审自动检查器批量核逐行证据，但每操作保留独立资格，不以家族抽样代替。失败、超时和待许可仍在冻结分母，工具族代表例只证明公共codec基座。

### 6.5 候选包、影子回放、canary和切换

独立接受的资格行构成CapabilityBundle候选。影子回放在拥有沙箱比较新旧同请求的结果、effects、拒绝、资源与恢复，不触碰用户真工程。canary只对已批准范围、具备独立接受和回退的候选能力进行；写前intent、fence与对账策略仍完整。候选全部必要检查通过后stage不可变manifest，再切换active引用，不能把草案policy直接覆盖生产。

旧jobs钉死旧版本；紧急撤销只阻止新dispatch并对账已发生effects，不装作撤销消除了真实写入。更新包不自动安装外部软件、未知插件或新执行引擎。已接受固定adapter范围内的metadata/资源更新可在既定批准策略内自动采用，新增执行代码/授权范围需要正常审查。失败回退只切回已验引用，保留候选证据与产物，未知effects先对账后恢复。

## 7. 四周GP资格窗与物理容量模型

D15–D42是四个完整周资格窗口。Q=0时旧K2100需要525项/周，K*3000需要750项/周；K或K*因冻结比较提高时按ceil((目标−可复用Q)/4)重算。可复用Q不是旧registry条目或白名单计数，必须逐操作证明旧资格环境、依赖、输出/oracle和当前准入仍有效。首次资格不能只用cache。

这两个分池数不能当完整项目资格工作量。以可共享且已证明满足两门的资格工作项建立交集：`K_union=K_GP+K_sem−K_overlap`，完整四周需求为`ceil(max(0,K_union−Q_union)/4)`。2100GP与3000语义、零可复用时，交集全覆盖旧池才是3000/750每周；零交集是5100/1275每周。交集未知保留750–1275区间；一对多方法映射、追加参数域与负例以及其余J专业验证仍按边际分钟计账。不要把发现名相同或共用codec当作已证明证据重合。

资格吞吐上界为`min(case准备能力, 独立oracle供给, 真实Host净执行能力, C0净复核能力, licence/资源能力)`。如果一次净资格需要运行正负例、验证、对账和返工的平均Host分钟h，一个周实际可用Host分钟H，则单Host能力最多floor(H/h)。同样C0除去其余完整功能验收后可用分钟A、平均独立复核m，其资格能力最多floor(A/m)。这些都需实测，不能把24小时无人值守假定为安全、可用或永久满载。

首两周用30–50项跨至少三个工具箱和多类型/条件族的困难混合样本测准备、Host、oracle、C0、失败与返工。试验不是完整数量资格，也不能用易工具峰值代表750净/周。D14给出60天可达性审计；不足时说明需要的真实资源/缺口和关键路径，继续完成全部范围与质量，不能把所有待证工具标可用赶日期。

自动化减少重复的是固定fixture模板、序列化、公共codec、错误分类、证据打包和队列资源调度，省不掉的是专业适用性、第一次实际效果、独立判断和各操作边际差异。批量Host窗口绑定同一受验payload可以省重复构建/安装时间，每case仍自己的输入、scope、receipt和oracle。这种优化应测完整任务与p95，不能只数提交数或工具数。

## 8. 参考成图StyleDNA到真实PS的工程配方

输入为只读ReferenceGrant、参考副本、任务FactTable、GIS素材、支持模板与目标尺寸。步骤一用固定图片/OCR/布局提取adapter识别地图区、标题、图例、标尺、页脚、色板、文字层级、网格、留白和面板关系，保留每项位置与观察置信。步骤二分离科学语义和装饰，标记错误/不适用内容；不声称恢复原PSD、矢量、坐标或真实数值。

步骤三StyleDNA转成受schema约束StyleIR，映射到本任务自己的地理框、类别/事实与模板槽位。无法确定科学色带意义时只迁移非科学色板，不让参考强覆盖已锁颜色。步骤四用实际字体文件、文字测量、图例容量和保护区形成有界布局候选，按根预算最多12候选、首轮3预览、总5预览、2修复、2最终渲染；多页和重启不重置。

步骤五生成固定PS CommandPlan，在全局modal lane中执行精确document/layer ID。准备素材和定向只读查询可并行，写段有边界、单项errors、取消deadline、独立观测和receipt；batch不是事务，错误后不能假定整段零效果。步骤六核文字可读、图例完整、地理关系/定量颜色/保护区/父组污染，然后有限RepairRule修复。不能通过删类别、改变事实或缩小科学图刷布局绿灯。

步骤七保存新拥有PSD，关闭重开和身份重绑定；真实检查可编辑文字/设计层、精确GIS栅格引用与导出预览。外部已编辑PSD保留原件，新revision默认完整重渲染，可复用已验纯GIS素材；不承诺自动合并未知外部图层。最终与Office共用同一FactTable和Claims版本，必要文件全部通过才激活。

自动模仿的衡量是允许视觉结构相似度、人工PS设计=0、事实一致、科学错误=0、错误目标写=0与完整用户任务成功，而非单独图片相似分。参考权利、字体/素材授权和上传用途分别声明；上传不默认允许再分发、联网发送或模型训练。没有必要在线微调，受限结构提取与已验模板库可以实现首版参考学习。

## 9. DatasetDNA、用户习惯与经验学习配方

DatasetDNA从允许的参考数据中提取schema、字段角色、单位/类别候选、几何/CRS、时间、拓扑和组织层级。全量检查与抽样观察分开，字段名相同不自动合并。将候选映射应用于新TaskData前，检查同名异义、缺CRS/变换、重复ID、日期/单位与资料充分性；依旧无法判定的关键项集中确认。只读参考不会被改写为本项目fixture或事实。

偏好学习基于明确的“设为本项目/个人默认”和接受/拒绝/撤销事件；观察临时改稿可提出建议，不立即成为默认。项目规则、当前任务指令、个人偏好和全球默认的优先级可解释；偏好不能决定事实、方法许可、输出批准或长期记住凭据。用户有查看来源、停用类别、删单条、忘记全部、迁移作用域的入口，撤销后推荐和缓存索引停止使用该条。

J07经验学习先做真实效果对账，再将可复现反例与已验成功步骤编译成候选经验/技能包。搜索命中只是建议，相似任务不证明新任务前提一致。错误原因模型只是假设，需独立反例和新数据回放验真；不能把receipt丢失学成“重试一次即可”，不能在未知effects状态自动再次写。

J11业务技能把记录中固定操作、方法条件、参数角色、输出槽位和缺口编译成typed DAG。跨项目去除旧路径/凭据/批准并重新绑定；两组不同新数据回放和错映射负例通过后独立接受。技能配方可增强完整任务成功和用户效率，但不增加规范GP或语义能力计数。J08主动补缺和多方案探索沿原EX01/04增强，J09沿EX05自动布局，J10沿EX03强依赖增量，全部保留原失败边界。

## 10. 创新默认采用、长期领先与终门

EX01–EX06原实验仍完整，新增J01–J12分别有输入输出、成本、回放、正负例、采用与回退，不用换名字称旧项已开发。每项先冻结分母和对照：正确任务能完成，关键歧义/错科学表达全部被拦，失败不隐藏，资源与用户确认不恶化，真实效果恢复可靠。专业资源与独立QA的成本逐项保留，C0维护一份唯一已接受能力清单。

持续迭代自动发现catalog/数据/格式/参考/习惯变化，生成候选，隔离回放，独立采用后才进入生产。长期指标包括净已验能力增长、完整任务成功、语义/行业覆盖、事实一致、PS人工设计、冷/热端到端时间、p95/费用和恢复成功；按每次冻结竞品条件比较，不沿用一次README或一次胜出永久证明领先。竞品变强、可信全集上限、许可变化或专家资源不足可能构成硬阻断，必须显示而非承诺“永久领先”。

原20PS合同逐业务真实正负/对账、36模板/108图、所有事实和科学门、核心/压力/隐藏/新手门、完整三链/Office/兼容和最终安装门全部保留。本版构建自动成长体系，不在本轮运行GP/PS、制作安装包或写生产policy；开发执行仍需有效派工和授权资源范围。

## 11. V5 ProviderIR落地配方：一次编译通用结构，逐项验证动态语义

ProviderIR属于WI11–14/J01–J02的边际增强，复用Composition与Invoker，不新造通用执行器。接口和合同见[模块接口](<MODULE_INTERFACES_AND_BACKLOG.md>)、[领域子合同](<DOMAIN_CONTRACTS_AND_STATE_MACHINES.md>)。官方反射机制来源见[实现入口机制表](<README_IMPLEMENTATION_DETAILS.md>)；借鉴可枚举参数和结构化用法的思路，不宣称GDAL或ArcGIS所有工具已可无验证接入。

### 11.1 编译与执行的两阶段算法

第一阶段针对可信固定metadata做静态编译。规范ID保留provider操作身份，aliases只是映射；按参数依赖图拓扑排序，发现环/未知子类型/冲突default时产诊断。类型归一化保留原类型与全部限制，组合Scalar/Unit/List/ValueTable/Union/FieldMap等已审codec，effects和licence均显式绑定。编译器生成schema/表单/文档/DTO和注册贡献，但不生成未知effects的默认许可，也不生成生产自由执行代码。

第二阶段针对真实输入materialize。Host解析精确DatasetRef/LayerRef与selection，得到实际字段/域/关联/CRS/单位/许可条件，形成DomainBinding。CodecPlan依该binding验证条件required、互斥、派生与原位范围，再生成PreparedInvocation。域采集失败、输入变化或等待后binding过期，回准备或停止；不能“只要json合规就调用”。准备期间的真实对象访问仍在MCT/已审Host，Shared只持DTO。

例如一个列为输入数据、统计字段、统计方法的值表，静态codec保证列和枚举结构；真实Host保证统计字段属于对应输入且数值类型适用。字符串中合法分号/引号不可split丢失，复杂FieldMap也不是可注入源码。未知新provider datatype保留UNSUPPORTED_DOMAIN和qualificationReason，不进入生产可用清单。域完整性和发现全集分母分别统计，不因不适配而删工具。

### 11.2 固定provider执行段

新增非Esri provider必须先完成SourceTrustRecord、合法依赖、固定adapter、配置/路径/参数支持域、独立effects策略和资格。provider不拥有产品第二注册中心；Composition注册的是已审固定入口，catalog仅在其支持的IR/codec范围投影。真实执行统一PreparedInvocation/Invoker，观察与receipt共享stableEffectKey；driver、表达式、外部服务或插件动态加载不能借结构化JSON逃逸准入。

若provider执行后需Finalize/保存完成等固定收尾，则把它列为同逻辑动作中的可观察子效果，不能在主调用退出0时提前标VERIFIED。失败/取消可能已产部分输出，必须按observer分类；仅缺一个目标文件不能证明未执行。科学输出检查与provider退出状态分开，固定adapter能够运行不等于每项方法都正确。

## 12. 独立科学oracle库：用小型真值覆盖方法风险

OracleCatalog按方法语义、对象/单位/CRS/时相、输入类型、边界/NoData/选择/效果风险索引，关联独立来源和支持域。初期优先利用原三链手算黄金和专业可证明小样本，不拿模型自拟expected覆盖全量工具。每类新增方法需要独立正确预期和适用负例；许可、专有算法或难以得到真值的项目显式计专业成本，未解决不计已验。

| oracle层 | 可证明内容 | 不能替代的部分 |
|---|---|---|
| 内容/手算微型真值 | 精确实体、关键指标、单位/分母、几何/分类/规则预期 | 真Host对象绑定、完整effects与当前许可仍另验 |
| 结构与完整性 | CRS/字段/记录/图层/元数据/可打开、声明输出齐全 | 文件结构正确不证明科学结果正确 |
| 守恒/变形关系 | 在锁定支持域内的集合去重、转移边际/面积、单位等价 | 两个都错但满足性质的结果仍可能通过，不能唯一oracle |
| 独立实现对照 | 不同已审方法实现对特定域给出的结果关系 | 共用相同底层算法、误差/输入假设时独立性不足 |
| 拒绝与恢复 | 错对象/缺许可/非法域零dispatch，未知effects正确对账 | 负例成功拒绝不能替正例首次真实业务成功 |

变形实验必须声明前提：全人口已知且权重非负时联合覆盖不超过总人口；交换两期、相同mask/格网与类别语义时转移矩阵转置；采用100cm=1m单位规则时规则判断不变。条件不满足标NOT_APPLICABLE/UNKNOWN，不为凑实验数量强做断言。手算内容、性质、真实Host和关键负例共同形成每操作资格。

### 12.1 证据生成与批量签证算法

case发行时冻结独立OracleSpec与fixture引用，公开输入和限制给执行端，隐藏expected隔离。执行每case写intent→调用固定Host→observations→结构/科学/效果检查→receipt，失败同样记录attempt。Assembler将原始事件/输出/实际检查/Host/许可/IR域/成本绑定到行摘要；Validator检查重复case、缺项、错版本和未解决UNKNOWN，不替C0产生裁定。

C0读取规范排序行索引、异常摘要和可回读原始证据，使用已审机器检查器做逐行核验，并记录每行接受/拒绝/待证与范围。批量manifest可降低重复阅读与签字成本，但所有独立操作仍真实首次正负运行；不能抽50个代表推3000项。签证后新payload或公共codec影响闭包发生变化，相关行重验/重跑，不沿旧整批绿灯。证据缺失时可以保留其他完整行，不把缺失行删分母或改名成另一行。

## 13. 语义去重算法与增长证据

先形成候选SemanticSignature：MethodCard方法、输入输出角色、科学单位/CRS、效果类别和声明支持域。名称、翻译、aliases、参数排列和provider只是检索信息。相同signature只作为等价候选边，C0还要核方法和内容证据；嵌入向量近似相似度不能直接决定等价或新增。不同默认值/参数选择不自动分裂新能力，真正不同方法/科学输出才可形成新语义候选。

等价边绑定适用域与witness。A≈B在域D1、B≈C在D2，不得直接union-find推出A≈C全域等价；只在有完整相同域等价证明的连通分量合并，其余待审或对声明共同域计算。增长清单分别记录规范操作、provider实现、独立核心语义、支持域扩大和完整任务成功。K_sem核心GIS/空间计算/转换治理分子不含PS/Office/UI/安装/学习管理，也不以配方和图例模板扩数。

K_GP和K_sem工作池按实际资格工作项/共享证据建立交集，不能因为名字近似就免费重合；新provider等价实现不增加语义数，仍消耗其Host与资格成本。当前真实方法全集、交集和N未知，3000目标可因同Esri上限、许可或缺独立方法源硬阻断，编译器无法凭空创造方法。自动成长的有效指标是净新增已验语义和完整任务覆盖，版本升级时重新冻结比较。

## 14. StyleIR的确定性模板fallback与PS固定段

参考解析形成观察候选，规则检查将可迁移项与科学冲突分开。选择模板时先过滤业务/尺寸/图例容量/字体/地理框/已锁颜色，再按固定适配评分与模板ID打破同分；模型可以提候选但不能最终重写科学约束。参考分数不足、OCR失败、未知类别色义或grant撤销，产生NOT_APPLIED原因，确定性采用已验同业务模板，并用原任务事实走完整GIS→PS→Office。

fallback也是同一个Invoker/原预算/同版输出合同，不能只导出PNG当完成；必须生成PSD并真实保存重开，Office按可编辑承诺验证，图/表/正文同FactTable。它不算J04参考学习成功：在声明支持域内参考学习系统性失败仍属于该功能缺陷，原J门须修复通过，不因为基本链还能跑就发行时删掉参考学习。

PS CommandPlan先在写lane外准备已验素材、定向read和布局测量。固定descriptor按逻辑效果风险/取消响应预算切有界段，每项保存commandId、document/layer目标、intent、观测与receipt；全局modal写互斥，不按文档数量扩并发。段内失败根据已发生effects分Partial/Unknown，不能推全段原子回滚。PS保存/close/reopen是实际效果与身份重绑定步骤，父组/全局效果和量化色带重新检，不只看顶层属性。

## 15. Shadow replay、失败最小例与有界创新闭环

shadow分纯reducer回放与拥有sandbox真实试验，证据标类型。回放消费原已提交事件及锁定的模型/外部结果引用，不新生成另一套解释；需要Host真实效果的比较只在批准拥有输出执行。候选新IR、codec、模板、方法或技能按相同固定输入与预算比较正确结果、拒绝、effects、成本与恢复，失败留样本，不能只比较最漂亮截图。

失败最小化采用受限delta debugging：先确定错误谓词和原正确对照，按不改变问题语义的组尝试去除无关字段/记录/参数，若失败仍重现则采纳缩减，否则恢复。单位/CRS/方法前提、科学分母和关键类别禁止为了缩小case而丢弃；缩减后的通过不算修复。每trial用新的拥有副本，实际effects未知先对账；限制总trial数、时间、资源、模型费用与修复次数，并从根预算持续扣减。

最小例、原case、新资料正确对照和关键负例全部保留。修复候选需C0/独立oracle在这些集合验证，才能进入经验/能力/技能候选包；模型自行总结的“经验”仍是hypothesis。J07/J08/J09沿现有WI29与关联WI计边际工作，不新增免费模块。采用后的再现失败暂停对应优化、保留证据并退已验基线，黄金和科学门不变。

## 16. V5的实施证明包和期望收益

最小证明包含：带动态ValueTable/FieldMap的ProviderIR；至少一种正例和关键复杂反例的真实资格；独立oracle污染拦截；逐行签证篡改拦截；有限license/MCT/PS锁的冲突与过期owner恢复；参考解析失败仍完成全链但标NOT_APPLIED；在原失败谓词下的有界最小化。这里要求的是未来真实结果，当前NOT RUN；不能以文档条目计新增资格或吞吐。

预期省的是重复入口/序列化/fixture准备、证据打包、已审纯产物复用、无关重算和验收重复阅读；没有实测不填写提升百分比。完整目标仍取有限资源DAG实际finish，若oracle/C0/Host是瓶颈，生成器无法解决其全部耗时。原所有量质、体验、完整业务、兼容与最终安装门保持，并行优化只改变实现与等待，不改变完成定义。

## 17. V6 GP资格工厂的四个实际提效点

ready检查在派发前编译原参数/来源/输出/许可/资源，并明确哪些负例需要真实Host仍必须执行。版本化case母本共享可复制拥有输入，但操作参数、真实正例、关键负例和独立科学检查保持逐项。对同一不可变产物只做一次完整读取形成typed ComparatorVector，原check逐条消费；独立mutation包括错CRS/单位/分母/范围/空值/字段/几何/统计/渲染，不仅改摘要。

候选包E5公开预检再交E1唯一集成；每次生成器/checker/codec/配置变动列受影响旧证据和真实重资格成本。batch只决定固定准备/汇总签证粒度，不能跳项；同难度试点比较净分钟、尾延迟、失败隔离和内存，最小化成本的批次不一定适用于Host或PS。独立oracle未知的项留待专业核验，不自动PASS。

## 18. 真PS停止、观察和参考角色迁移

PS adapter派发前绑定AttemptToken/document identity/固定动作/拥有输出/保护语义。全局modal占用与具体文档/资源lane分别观测；UI已点停止不能证明Adobe退出modal或保存未发生。超时先只读观测实际文档/文件状态及期望内容，不能再发同保存/覆写动作。旧owner不得更新当前交付索引；不支持隔离或停止证明时冲突作业单lane，而非开多AI并行操作同PS。

StyleDNA的候选在不同内容密度、字段长度、页面尺寸、字体/ICC等实际支持域中迁移验证；DatasetDNA用新数据独立核结构、单位、实体和全量约束。归零人工PS设计仍按原完整20业务/36模板/108标准图验收，参考模仿和模板回退分别报告满足程度。真实PSD图层/蒙版/文字编辑性需重开验证，PNG不能冒充PSD。

## 19. 现场源码锚点与开发风险

2026-10-02静态审查可见GPExecutor传CancellationToken并读取IsCanceled，JobStore有取消/fence字段；现有PS handler主要为握手/ping，不能推定已有真PS动作。FolderWorkflowTools的取消pending与子receipt待对账传播、可变state上的fence判定、journal提交失败确认，以及whitelist调用取消后的观测是需要原型验证的风险。详细路径/行号/来源和故障配方见[取消审查](<D:/ArcGIS-Pro-MCP 2.0/同步/3.0项目规划创新/临时/V6_取消与恢复审查_20261002.json>)。**未实际复现，不能登记为产品已证缺陷或已修复**。

采用现有层增量补强，保持MCT、Shared隔离、唯一注册/配置、loopback和受控6511。不会以任意Python/JavaScript、后台直接SDK或新网络旁路降低开发成本；真实安装/Host写入仍按届时有效派工和用户授权。

## 20. V7 GP采集、探针和激活的实现细节

采集器逐scope保留完成/失败与原始descriptor；规范ID不以显示名或别名重排。静态模型方向/required/type不能完整表达动态条件；已审codec编译后仍保留unknown条件、来源与最小待证case。接口不存在时如实UNKNOWN，不新造SDK方法或import未知Pyt/ToolValidator。

ProbePlan先预检明确条件与拥有输出，真实正负例使用受验Host/许可/lane与每项独立oracle。支持域切片不声称覆盖任意输入，参数实例和代理不扩算法数。manifest成员先受验，stage写入后append耐久ActivationCommitted，head投影失败从权威journal重建；竞争expectedHead/epoch/fence拒绝而非last writer wins。实际whitelist env/file兼容来源须走同一冻结准入，不能即时改文件即激活。

## 21. 真PS布局测量不能伪装只读

先用纯模板/已验元数据排有限候选；精确文字测量若创建doc/layer，则是Host写，走原Invoker/批准/拥有临时对象/全局modal、intent/receipt及取消对账。测量缓存只在完整字体/文本/单位/DPI/变换/Host身份一致且glyph资格有效时复用，第一次真实资格不靠缓存。

每标题/图例/来源/比例等required ID逐项绑定。point text显式合法断行保存原字符串到display映射，paragraph另证全文无裁剪；字体可找到/boundsNoEffects正常仍不足证明缺字/裁剪/视觉合格。最终真PSD与Office重开、科学保护/必要元素/全文/可编辑性完整。最多12候选、5总预览、2修复、2最终渲染沿根预算；13号候选拒绝，不重启预算。

采用[QGIS障碍与标签位置策略](https://docs.qgis.org/3.40/en/docs/user_manual/style_library/label_settings.html)的几何约束思想；其省略标签策略不用于我方科学必需项。Adobe[字体接口](https://developer.adobe.com/photoshop/uxp/2022/ps-reference/classes/textfonts)与[图层bounds](https://developer.adobe.com/photoshop/uxp/ps_reference/classes/layer/)提供真实观察基础，具体完整字形/裁剪oracle为本项目新增待验实现。

## 22. V8科学颜色和实际载体贯通

AssignProfile仅重新解释同数值颜色，ConvertProfile按色彩转换改变编码；不能二者互换。[Adobe profile区别](https://helpx.adobe.com/ie/photoshop/desktop/adjust-color/color-profiles/change-color-profile-for-documents.html)。固定ColorPolicy记录源/目标ICC实际文件摘要、色彩空间/位深、rendering intent、黑点补偿适用条件、嵌入profile和实际操作见证。convert/assign写操作仍走批准/Invoker/modal/lane/intent/receipt；测量需要临时对象时同样是写。ICC处理不证明科学量正确；分类/连续定量颜色映射另锁FactTable→category/value→GIS资产→PS图层→最终载体的身份与语义。

科学保护对象包括地图框/变换/比例、北箭头、全部类别图例、轴/单位、定量色带、数值与来源。PS实际混合模式、全局调整、图层透明/遮挡/裁剪可能改变最终表达，不能只核原始GIS图层。装饰映射在已批准区域与固定约束内；参考模仿相似度不能凌驾科学门。

像素观测记录actual dimensions、source/target rect、document/layer版本、分辨率/位深、ICC、alpha与compositing背景、采集方式及coverage。[Adobe imaging API](https://developer.adobe.com/photoshop/uxp/2022/ps_reference/media/imaging/)的缩小读取可能用缓存层级，applyAlpha白底合成与原始alpha不同，16/32位数值范围亦不能统一按8位解码。固定adapter检查实际返回level/bounds和像素约定，逐块dispose；低分辨率或白底结果不能自动证明原生全图。用有限分块遍历全分辨率支持域、重叠边界/坐标映射和未覆盖集合，预算/内存不足待证，不能缩小后称完整。字形/长文本、绑定/数值、地理与编辑oracle仍分别执行。

最终PDF/Office载体必须保存后真实打开/读回；[Esri 3.5 PDFFormat](https://pro.arcgis.com/en/pro-app/3.5/arcpy/mapping/pdfformat-class.htm)的simulateOverprint强制栅格化，imageQuality重采样影响有效栅格dpi，embedFonts不能保证不可嵌字体进入文件。各provider分别核支持域，不能把该Esri属性假设为Photoshop API。PDF能显示不证明矢量或文字编辑性；原要求可编辑的成果需真实编辑见证或原规范允许的对应可编辑文件，不以PNG替代。保存重开后重绑定实际document/layer身份，Office读取typed cells/公式与原生结构，不只OCR预览。每carrier科学/用途合同由WI19与WI22共同提供，避免双建交付检查服务。

## 23. V9自主GP更新及真实PS资源配方

GP配方：锁可信catalog/实现与Host build→采集参数/codec/环境/方法/effects及完整依赖→提交保守失效→逐操作正例/全部关键负例和实际产出/效果→当前独立oracle→C0签目标域资格→更新同水位集合。仅元数据可发现、同名或schema未变都不能跳真实资格；若共享provider改变影响全部目标，须计全部重资格成本。读源和提案可自动化，扩受控白名单、安装/真实写和发布仍走原授权及门。

持续增长分别记录新唯一资格、恢复、撤销、重复、失败、未知。当前有效数与原目标余期从成员集合重算；许可证临时不可用不抹历史，也不让当前域通过。反向index完整只证明已声明边镜像，必需实现/环境/fixture/oracle/policy依赖是否完整还需适配器审查。借鉴[Bazel rdeps显式universe](https://bazel.build/query/language#transitive-closure-of-reverse-dependencies-rdeps)，不把不完整图上的快速查询当完整影响证明；[DVC逐阶段重跑](https://doc.dvc.org/command-reference/repro)启发分轴变化，禁止照搬删除用户旧产物。

PS配方：profile绑定真实Host/文档/已验操作及峰值→联合预约RAM/PS全局lane/输出空间/根预算→实际executeAsModal内再核文档身份、取消/资格/票据→固定CommandPlan→真实保存/重开→科学视觉/编辑性/同版交付。每文档锁不替全局modal；别的插件占modal时本项目预约不等于拿到宿主。进入Host后取消可能到中断点才生效，吞异常继续写禁止。[Adobe官方modal合同](https://developer.adobe.com/photoshop/uxp/2022/ps-reference/media/executeasmodal)提供版本条件，真实支持按已验环境判。

MCT排队不是整个GP占用，PPF worker数与常驻模型/每请求KV峰值分别计；shared allocation只计一次，增量逐任务计。真实许可Available观测不等于全局保留，Concurrent Use checkout与其他许可模式区分；不新增任意Python或6511旁路。

创新合并到原合同与成熟适配器：用户习惯/风格/方法/质量建议继续在原有限预算和来源权利内学习；协议使新能力可查可退、资源可解释、五端可接力。学习推荐和contract绿灯均不签科学PASS；不得让自学习改oracle/目标分母从而给自己合格。

## 24. V10科学检查与参考学习的固定配方

GP逐操作配方：可信描述/codec候选→原MethodCard和独立expected→锁实际Host/build与完整输入→原正例/全部关键负例→支持域/数值或类别/地理/effects独立读回→当前C0接受。自动typed编译只处理声明域；工具名称、统计函数signature或两个provider同值不生成科学期望。Esri不同版本格网/重采样默认按准确版本锁，不能把某版本规则推广到全部兼容矩阵。

统计要求明确DATA/NODATA与样本/格网/eligible count，null/全空不是0；范围截断/TopN不能冒充全域总量。连接有1:N时先确认真实业务grain/可加性，采用已审实体去重、预聚合或权重配方需要新计划、原准入与科学检查；未知重复或分母不靠LLM猜。数值近似阈值独立冻结，categorical ID/单位/掩膜/地理/effects另查。

实现时消费[数据合同当前V10固定代数与oracle规则](<DOMAIN_CONTRACTS_AND_STATE_MACHINES.md>)：空键/规范化策略显式，值/权重同一eligible集、有限非负权重和正分母，ratio-of-sums核兼容互斥分区；轴序/transform/affine与每种统计公式绑定版本，正向独立控制点和固定有限误差分别核。不复制另一套默认，不扩大公开join语义，不把这些内部检查计为新增工具。

参考学习先逐成员当前READ/用途/deny-use，后固定decoder/ICC/方向与角色观察→精确/已证衍生组及近似候选→有界当前角色来源选择→同层偏好与组合裁决→原DesignSpec/真实PS→V8科学视觉和全部交付门。相同内容可减少重复特征计算，前提当前每次实际来源合法；授权不随内容哈希合并。

参考的地图几何、OCR数字、比例尺长度、分类和别人的数据不自动进入当前事实。疑似近重复不被自动合票，孤立embedding阈值不保证科学图例同义；组留出按已知同作品/衍生组，未知泄漏如实挂账。采用来自[OPA冲突机制](https://www.openpolicyagent.org/docs/errors/eval-conflict-error/complete-rules-must-not-produce-multiple-outputs)和[FiftyOne近重复限制](https://docs.voxel51.com/brain/index.html#near-duplicates)的工程推论，不安装OPA/FiftyOne、训练服务或额外端口。

Counterexample/公开tuple的额外运行仍原Invoker/intent/资源票据/receipt，不能借“实验”绕效果审批。全部新机制辅助自主适配、普通用户自动设计和质量定位，不新增核心算法数量，不改变现53受控GP白名单、错误码33或部署身份。

## 25. V11方法、事实表达与实际媒介的固定配方

资料配方：TaskDatasetNeed→固定已授权source/本地查询→保留全部页/失败/未知→逐asset当前READ/use→原Invoker获准读取/物化→V8完整实际readset→V10支持/粒度/NoData/单位/时相核→原Need gap重评。STAC资产的投影覆盖Item默认需按实际extension核，shape/affine顺序按对应规范解释；不把目录schema通过当正确像元。

方法配方：独立采用不同方法的DiagnosticStudy→锁共同评价目标/全部域→实际独立科学和效果→比较适用/缺失及转换；provider替代另核原method等价与当前资格。稳健性配方锁事实/指标变换/方向/weight domain→明确解析或全部有限scenario→原Invoker与rootBudget下独立运行/复用→ties/翻转/failed/unknown与范围→不自动修改真实决定。取整WeightedOverlay和线性WeightedSum不能套同一证明。

当前[领域合同](<DOMAIN_CONTRACTS_AND_STATE_MACHINES.md>)是NormalizationSpec、两指标仿射域/硬门、DisplayTransformChain和ReadingRoleManifest的一次规则源：scenario不得静默refit/clip/填必需缺测，0权重不豁免必要数据；科学系数非用户偏好；单位/语义变换先于舍入与culture；阅读节点完整唯一，drawing冲突不改变科学遮挡。各真实adapter消费同版本规则，不复制另一套默认。

表达配方：FactCell→SemanticDisplayContract→批准术语/单位/百分比/舍入tokens→语言模板和阅读角色→真实字体/PS/Office/媒体导出→实际token/阅读结构/科学视觉/编辑性独立读回。有限文字匹配不能代替真实布局，不生成未经事实支撑的结论。

Pro 3.5 PDF资料说明tagged PDF从layout导出，阅读顺序依drawing order，accessibility tags与feature attributes存在组合限制；不能泛化到全部Host版本。[Esri PDF说明](https://pro.arcgis.com/en/pro-app/3.5/help/sharing/overview/pdf-export.htm)。OutputContract已允许多成员时可计划同事实的属性查询PDF和阅读PDF，各自验收及清楚用途；明确同一文件两能力时须等已资格方案，不暗拆、不给同一文件虚报能力。真实screen reader/office能力仍原Host验收而非本文推定。

## 26. 自主更新的可采用证明与有界恢复配方

本节消费[领域合同§28](<DOMAIN_CONTRACTS_AND_STATE_MACHINES.md>)的SourceVerificationWitness、SourceUpdateWatermark和BundleClosureWitness，不复制第二套来源/包schema。更新已在§6/20/23具备主链；本节只把验真、闭包和故障判别具体化。首版仍固定已审adapter/codec/来源域；新执行代码、依赖、软件和扩大授权不能借自动更新混入。当前这些步骤均是待实现/待实测，不增加已验工具或语义数量。

### 26.1 同一版本从发现到采用

1. **发现并绑定来源。** 固定source/namespace、collector、Host/provider/build及来源审查版本，保留全部scope的完整/失败/未知项。允许的本机固定Host路径按已审来源机制验证；不能仅凭路径/名称/hash认定真来源。已授权远端或离线信封按固定验真规则与可信锚核确切声明和实际产物；无签名的合法来源按已有受审机制说明保证范围，不一律拒绝，也不凭摘要宣称认证。候选自带key不自动受信，信任变更走受审连续性或新决定。
2. **提交来源见证及接收水位。** 核来源身份、实际digest/length、当前用途/许可、撤销与TimeValidityWitness，再提交已验证版本。未验高版本、同版异内容和低版重放分别诊断；重复同版不加数也不续期。没有可靠source版本语义就公开该反重放限制。接收新版本不表示生成合同、资格、canary或活动版本均已完成。
3. **生成合同与影响候选。** 对该不可变CatalogSnapshot做完整diff，固定所需IR/codec/policy/method/effects/环境及来源许可。未知动态类型/方法前提继续待核；binary变化即使schema相同仍沿WI23完整影响闭包。原三入口同PreparedInvocation，Composition仍唯一注册。
4. **逐项资格并构造闭包。** 每操作原真实正例、关键负例、独立内容/效果oracle和C0接受保留。生成根manifest及完整成员宇宙，逐件读实际内容与schema/build/Host/支持域证书及必需依赖；不分别拿catalog A、codec B、policy C的latest。跨来源逐来源验真与许可，相关stage/证据/旧任务依赖先有durable pin。
5. **有界shadow/canary。** 固定baseline/candidate根、输入/case和明确domain、全部必要正确/拒绝/effects/恢复/成本检查、失败分母与停止条件。纯reducer回放不做Host效果；真实试验只在获准拥有范围、资源票据和根预算中进行。canary消费已独立接受候选，不替未资格操作授予资格；小域通过只允许该受验域采用。
6. **单控制源采用。** 原BundleHeadCommit闸重核expectedHead、根/闭包、最新控制/deny/fence、当前Host/许可及既定采用策略；append完整耐久ActivationCommitted并读回后报告Activated。并发落败保持候选，按新head重评而非覆盖。每个新job一次锁根和精确成员；旧job不混版，真实效果入口仍当前再核撤销/准入。
7. **恢复与合法回退。** ack丢失先查原逻辑采用ID的权威事件；投影丢失只重建同head；stage没有commit不激活。包成员损坏阻相应依赖，控制史/水位/撤销不可证明按真实保守影响域等待。合法回退产生新commit引用当前仍合法旧包，不倒来源水位或revocationEpoch；旧effects继续对账，不能通过换版本重发未知写。

用于用户工作台的状态分别来自真实来源接收、候选资格、canary、采用head、当前可用性与故障恢复投影。检查失败时显示“更新待核/保留合法旧版”和具体受影响范围，不能显示“已是最新”或“所有能力已恢复”；候选缺证不封停已有完整合法链，共同控制史损坏也不能虚报只影响候选。来源无法证新鲜时不承诺已取得最新全集；合法离线业务仍按其实际资格和期限运行。

### 26.2 公开验收反例及成功边界

| case | 必须观察到的结果 |
|---|---|
| 未验高source版本、同版异digest、低版本重放 | 未验内容不推进水位；冲突/重放不采用、不加数；原合法已验链按实际当前限制保留 |
| 未知/撤销key、自签新key、信封解析歧义 | 固定来源验真拒绝/待核，不能靠payload摘要、模型解释或TLS替换信任锚 |
| 合法固定Host/离线来源没有远端签名 | 按已有已审来源机制和用途资格判定，不因缺签名全域封禁，不冒充密码学认证 |
| 根manifest正确但缺member、A目录+B codec、跨源许可不明 | 闭包/来源/许可分别不通过，未形成可采用完整包；每缺项留证 |
| 单操作资格后policy/effects/许可/Host变化 | 提交/dispatch前最新依赖与支持域再核；旧证书不偷渡新域 |
| shadow或canary仅小支持域通过 | 只能形成该精确域证据；首次未执行操作、未覆关键负例或其他域仍待验 |
| 两候选同expectedHead、旧fence晚到 | 只有符合当前条件的durable commit推进head；落败不覆盖、不改旧ExecutionLock |
| stage后崩溃、commit前后崩溃、ack丢失、旧head投影 | stage不激活；完整commit从权威史对账/重建；未确认采用不盲重切 |
| 权威控制日志损坏、旧snapshot漏撤销或水位缺失 | 原件保留，受影响非终态/新派发按保守域等待；不清空控制史伪称安全 |
| 合法回退后旧job许可撤销、Host仍写或effects未知 | 当前后续dispatch拒绝，旧效果/Host/lane继续分类对账；回退不复活许可、不自动释放资源 |
| 更新候选缺证而正常旧链合法完整 | 相关候选暂停，完整合法旧任务/历史查看按原合同继续；不伪报新候选成功 |
| 包内容损坏后恢复、重复相同更新 | 修复/重复恢复原成员，不增加唯一资格；根预算、失败样本与原始记录保留 |

真实测试分别记录attempt、实际内容/状态、EffectState、来源/时间/控制seq、采用根、成本与独立裁定。正确阻断不是功能成功；故障case不能从分母删除。隐藏oracle和来源审查依据由C0/独立专业方保管，实现者不修改黄金或许可规则给自己过门。

上述补强归原WI11–15/23/30及E1耐久底座，不新增WI/PF/产品服务/公开工具/端口或权限。PF01确认真实可信源与版本语义，PF04先证控制底座竞争/恢复切片，PF08验证完整更新/撤销/回退与旧任务；原数量、108图、60任务、学习/支持/发行门保持。净提效须同时计来源验真、成员扫描/哈希、依赖维护、实际资格/canary和C0成本，当前仍null，60日完整交付未被本文证明。

机制借鉴依据[TUF客户端更新验证](https://theupdateframework.github.io/specification/latest/)与[SLSA产物验证](https://slsa.dev/spec/v1.2/verifying-artifacts)，核查日2026-10-03；实现配方为本项目工程推论，不安装标准平台、默认获取网络权限或声称完整合规。规范身份验证不能替代科学oracle、当前来源权利和真实Host效果检查。
