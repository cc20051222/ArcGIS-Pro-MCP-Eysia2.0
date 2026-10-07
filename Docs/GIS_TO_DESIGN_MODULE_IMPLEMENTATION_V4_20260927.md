# ArcGIS Pro MCP：逐模块实现设计 V4

日期：2026-09-27。状态：**实现设计建议，未写入产品代码，未完成运行验证**。

配套：《创新与产品方案 V4》《开发执行计划 V4》。下述新增类型、目录和字段均为设计草案；正式名称、schema、兼容策略和权限在对应工单冻结。`[VERIFY]` 表示需目标主程序版本的编译或真实验证，官方存在相关能力不等于本项目已支持。

## 0. 代码组织与共同契约

保留分层 `Client → Transport → Protocol → Server → Router → Registry/Tool → Host → Services → SDK`。唯一生产工具注册点保持 `Composition.BuildRegistry()`；Shared 不引用 ArcGIS SDK。以下20项是逻辑模块，第一版不为每一项新建程序集。

| 位置 | 放置内容 |
|---|---|
| `Source/Shared/ArcGISProMCP.Core` | Capabilities/DataContracts/Planning/Jobs/Artifacts/Design 等拟新增领域目录与无宿主接口 |
| `Source/Shared/ArcGISProMCP.Tools` | 现有工具及新增薄适配，参数/结果与公共服务对接 |
| `Source/Shared/ArcGISProMCP.Server`、Protocol | MCP 兼容、受限 PS 内部传输、能力协商 |
| `Source/Shared/ArcGISProMCP.Security`、Configuration、Logging | 路径/授权/会话、统一配置、脱敏审计 |
| `Source/ArcGISProMCP.Compatibility` | SDK服务、调度、WPF DockPane 与 ViewModel |
| `Source/PhotoshopPlugin`（拟） | UXP TypeScript 源码、编译后的 JavaScript、manifest、受限命令和 PS 面板 |
| `Resources/Design`、`Resources/Workflows`（拟） | 模板、风格、配方、schema、许可与样例 |
| `.runtime/design-workflows`（拟，D盘可配置） | 作业、产物、设计绑定、预览、日志；版本化且有清理边界 |

`Source/ArcGISProMCP.Current` 目前仅占位；不得在它未形成真实构建/运行证据时把它列作生产 host。新链路先选定一个实际可测主版本，再扩展宿主范围。

### 0.1 八种核心对象

| 对象 | 最小信息 | 目的 |
|---|---|---|
| CapabilityDescriptor | toolName/schemaDigest/effect/hostRequirement/licenseRequirement/evidenceRef | 区分已注册、当前可用、已验证 |
| DatasetContract | stableId/source/schema/crs/units/time/nodata/semanticBindings/fingerprintStrength | 避免字段同名就误认含义 |
| ProcessingPlan | schemaVersion/steps/dependsOn/inputs/outputs/methods/limits/planDigest | 确定执行什么以及输出在哪里 |
| JobRecord | jobId/revision/ownerScope/steps/lease/state/events/checkpoints | 长任务和恢复；与HTTP请求分离 |
| ArtifactManifest | artifactId/path/mediaType/contentDigest/logicalDigest/dependencies/producer | 多产物和来源链 |
| DesignSpec | canvas/mapFrames/roles/tokens/protectionRules/templateVersion | 约束专业地图设计 |
| DesignBinding | bundleId/documentBindingId/semanticId/hostLayerId/baseState/manualState | 支持重开、三方刷新与冲突审阅 |
| EvidenceRecord | caseId/buildDigest/hostVersions/inputDigest/assertions/status/outputs | 独立复核真实结果 |

hash 只说明完整性，不证明来源可信。没有历史来源的输入记 `unknown`；没有完整读取的数据指纹不得标 `verified-content`。所有持久对象含 schemaVersion，未知主版本拒绝执行并保留原件。

### 0.2 公共执行入口

提出 `IToolInvoker` 作为**唯一调用公共内核**，由现有 Router 委托，而非替代 Registry：

```text
Invoke(call, identity, cancellation)
  → 解析唯一注册工具
  → 执行既有只读/权限门（保持旧拒绝优先级）
  → schema与目标解析
  → 工具所需的路径/许可/输入版本/授权范围校验
  → 创建完整执行上下文与审计关联
  → Tool.ExecuteAsync
  → 检查产物/副作用并返回既有结果封套
```

面板、普通 MCP 调用、`run_batch`、`apply_processing_plan` 的步骤均经此内核；禁止递归调用作业编排器导致死循环。抽离前后用相同正负例作差分验证，不能把兼容性修复夹进 D-079 冻结范围。本设计不是对当前代码作安全漏洞定论，而是预防新增执行路径守卫分叉。

## M01 · 能力目录与工具检索

**职责/接口**：拟 `ICapabilityCatalog.Query/GetSnapshot`；输入检索词、环境与分页，输出能力条目及不可用原因。外部入口复用/新增 `describe_tool_catalog`、既有 tools/list。

**实现步骤**：
1. 从 `MCPToolRegistry.List()` 和真实 `IMCPTool.InputSchema/Metadata` 建基础目录，不手写另一份工具注册清单。
2. 附加只读的业务标签、依赖、读写范围和证据索引，未知项不推断为支持。
3. 通过 Host/许可/PS握手快照计算可用性，环境变化即使旧缓存失效；缺PS不影响GIS工具。
4. 检索先采用中文/英文关键词、场景标签和同义词；别名不新增工具数。需要时再评估本地语义检索，第一版不引入向量数据库。
5. 固定目录 revision 的分页；schema hash 与实际 tools/list 对账。客户端是否完整遍历分页、处理列表变更分别实测；分页不自动意味着模型上下文更小。

**代码/依赖**：Core/Tools 目录；依赖 M05/M19 的分类和证据。MCP 协议适配保留已有客户端路径，不能强制仅返回“相关工具”破坏发现。

**失败与验收**：无Host时返回静态目录+unknown；目录损坏与空结果区分。154基础逐名核对、别名去重、许可变化、PS断线、稳定分页、未知字段、工具名大小写按实际契约验证。

## M02 · 数据发现与语义契约

**职责/接口**：拟 `IDataProfiler.Inspect`、`IDataContractResolver.Resolve`；扩展既有 scan/load/schema 能力。输出 `DatasetContract`、问题清单与待确认语义。

**实现步骤**：
1. 复用 `scan_data_folder` 的目录/深度/reparse 守卫，以及 Host 的 Schema、Attributes、Raster、SpatialReferences；读取成本设预算。
2. 记录 CRS、地理转换需求、坐标单位、几何类型、字段类型、时间范围、NoData、编码和来源。不从一个文件名推断真实坐标系。
3. 用规则建议字段角色，如设施类别、唯一键、人口、时间；候选带依据和不确定原因。涉及距离模型、分母、单位、关键字段歧义时收束后才能执行。
4. 普通文件计算SHA256；大库先用元数据快速指纹并明确 `weak`，只有稳定快照/可靠版本号/逻辑内容摘要才能用于安全结果复用。
5. GDB逻辑摘要按稳定键排序、字段规范化、几何/CRS规则计算；无稳定键时不得拿 ObjectID 当跨版本身份。大数据采用有界分块，主动记录读取失败/并发编辑。

**代码/依赖**：Core/DataContracts＋现有Host服务。没有必要一次扫描全部要素；preview样本不代表全库体检。

**失败与验收**：中文字段、空表、混合单位、缺CRS、锁库、同名图层、目录越界、计数不可得、大库预算耗尽；分别输出“未知/抽样/完整”，不将抽样无问题写成全库正常。

## M03 · 数据质量与治理

**职责/接口**：拟 `IQualityRuleEngine.Evaluate`、`IGovernancePlanner.Plan`；检测与修复拆开。承载 B2、P1–P4 的几何、拓扑、重复、值域、子类型、关系类等能力。

**实现步骤**：
1. 规则定义包含适用数据、阈值、容差、严重度、读取范围和已知答案样例，输出带要素标识/位置的 issue records。
2. 先做必需字段、Null/唯一键/值域、CRS和几何有效性；复杂拓扑用批准的GP或SDK，限制范围避免全量两两比较。
3. 属性重复采用规范化键＋分组；几何重复先空间索引筛候选，再按明确几何等价/容差确认，不能仅靠WKB字节相同决定业务重复。
4. 数据差异区分schema、属性、几何、来源；增删改需稳定业务键；无键时报告无法可靠配对。
5. DDL/修复默认在新副本执行；先检测schema锁/依赖，生成变更计划，再通过对应宿主适配或批准GP执行。域/子类型/关系类具体SDK版本能力 `[VERIFY]`，不虚构SchemaEditor方法。
6. APRX快照不是GDB数据恢复。库备份/新副本验证完毕，才考虑明确授权的原位修改；不把不同DDL步骤宣传为全事务。

**失败与验收**：拓扑正负样本、容差边界、域被引用、关系孤立记录、锁失败、缺许可、修复后新问题。报告文件生成是产物写；只读查询和报告落盘分类分开。

## M04 · 意图解析、配方与计划编译

**职责/接口**：拟 `IWorkflowMatcher.Match`、`IPlanCompiler.Compile/Validate`；外部是 suggest_workflow/validate_plan 与既有 apply_processing_plan，不新增任意脚本执行面。

**实现步骤**：
1. AI客户端或场景表单提出目标与候选参数；服务器不依赖另一个隐藏模型调用。
2. 匹配有版本的30场景配方；匹配失败列能力缺口，不凭自然语言自动生成Python/JS。
3. 用类型化步骤表示工具名、输入artifact引用、输出声明、dependsOn、方法/单位/约束；拓扑排序并拒绝循环和不支持的嵌套执行。
4. 编译阶段验证工具/schema、数据类型、单位、CRS、许可和依赖；运行前再检查易变化的输入、输出存在性与权限。
5. 从现有dryRun提取同一验证器，保证预检和执行使用同样规则。旧计划可转内部IR，但不能默默添加“默认 overwrite”。
6. 规范化计划绑定输入指纹、输出路径、能力/schema版本形成digest；批准范围变更需要新预览。资源估计给范围与依据，缺历史不造精确ETA。

**失败与验收**：500米直线/路网歧义、百分比/比值混淆、未知CRS、单位错误、循环DAG、旧计划兼容、恶意数据说明、dryRun与执行不一致。

**技术边界**：任务条件表达式采用有限枚举/类型规则，不引入通用eval、模板代码执行或可联网动态脚本。

## M05 · 授权、路径与执行安全

**职责/接口**：拟 `IExecutionPolicy.Evaluate`、`IPathPolicy.Resolve`、`IApprovalScope.Validate`；复用已有Security、只读分类、受保护路径和审计。

**实现步骤**：
1. 逐工具定义R读取/S状态/F产物/D数据破坏等内部effect，映射现有分类；文件预览也可能有写副作用，不为方便伪装只读。
2. 同一预览批准范围记录主体、计划digest、输入版本、输出根、工具集合和有效期；控制和高风险动作保留既有确认要求，不能把客户端可填token当授权证明。
3. Windows路径做规范化、边界分隔判断、reparse检查、实际文件解析及写前复核；拒绝UNC/ADS/设备路径等未明确支持形式，限制压缩包展开路径和大小。
4. 项目缓存/产物/日志固定在配置的D盘拥有根；落盘前可写探针。保护未知文件及用户后续修改，清理仅凭拥有清单+身份校验。
5. 运行中切只读时不启动新的写步骤；已进入不可中断宿主调用的写操作不能承诺立即撤回，记录状态并走取消/对账。只读下是否允许cancel需单独契约裁定。
6. 外部数据、模板文字和图层metadata只当数据；网络/云/发布权限独立于GIS写许可。凭据不得进入模型参数回显/审计。

**失败与验收**：路径前缀碰撞、符号链接变化、过期批准、改参数复用批准、跨作业/跨文档、输出已有、只读拒绝优先级、批处理与面板绕过。既有33错误码优先复用。

## M06 · 作业调度、持久化与恢复

**职责/接口**：拟 `IJobService.Submit/Get/RequestCancel/Resume`、`IJobStore`、`IResourceScheduler`；先抽离 FolderWorkflowTools，再提供B1控制工具。

**实现步骤**：
1. 等D-079最终格式落定后实现旧格式读取和显式迁移；旧OwnerToken/Running/并发拒绝语义保留到独立变更获准。重启不擅自夺取外来任务。
2. 设计新状态：queued→running→completed/failed；running→cancel_requested→cancelled或needs_reconcile。paused仅在安全边界和获准协议支持后提供，第一版不假装能暂停所有GP。
3. 调用寿命与job寿命分离。默认断开请求如何处理沿用旧契约；新异步提交/跨会话恢复需要明确授权与兼容适配，不能悄悄改变旧工具返回方式。
4. 初版采用单写者的不可变步骤分片＋原子manifest（或D-079已验等价方案），记录revision/checksum；写前持久化意图、写后持久化宿主结果。恢复忽略不完整尾部，隔离未提交片段，不能静默吞掉持久化失败仍称可恢复。
5. 资源锁按真实数据源、项目/布局、PS宿主等归属，统一顺序取锁；持有短锁更新状态，等待外部宿主时不长期占用作业索引锁。
6. 稳定stepId和输入/参数digest防重复；跨软件无法保证exactly-once。结果未知时先核对输出/文档版本，再决定继续、另存或人工处理；非幂等操作不自动重试。
7. 取消使用独立控制通道和token，先标cancel_requested；宿主确认停止或步骤边界停止后才标cancelled。已产生结果记partialArtifacts，不自动删用户文件。

**代码/依赖**：Core/Jobs，Tools薄适配，M05策略/M16产物。阻塞SDK只在Host调度；独立文件哈希/报告IO使用有界后台并发。

**失败与验收**：同job竞争、进程重启、写意图后崩溃、宿主成功回执丢失、磁盘满、损坏checkpoint、取消/完成竞态、旧格式续跑、外来所有者。APRX恢复、GDB恢复、PS历史撤回各有独立边界。

## M07 · 矢量、空间统计与受控 GP

**职责/接口**：复用 `IArcGISHost.Geoprocessing/Attributes/Data/Edits` 等；承载既有分析工具及P5高级空间候选。

**实现步骤**：
1. 每项能力选 Native SDK、批准GP、现有受限Python之一；路由选择固定在服务映射中，模型不能自己换为任意脚本。
2. GP描述符记录真名、参数顺序/类型、许可、输入输出、副作用和取消行为；白名单扩容要源码快照、嵌入资源和LIVE一致。
3. Buffer/面积/长度明确planar或geodesic和单位；投影转换与define_projection分开。环境参数在计划显式绑定，不继承不可见的上一任务环境。
4. 邻接/格网/综合明确边界、容差、单位；制图平滑另存副本，不回写科学分析原数据。
5. 路网先核数据、旅行模式、阻抗、限制、许可和服务成本；无路网明确不可用，不退化成直线后仍叫步行可达性。
6. 自相关/热点保存权重、距离、样本限制、显著性与多重检验政策；方法说明取真实参数和结果。
7. SDK对象访问遵循项目MCT规则；GP异步调用使用目标版本官方支持方式，不将整段网络/文件IO包进长时间QueuedTask。[Esri GP调用](https://pro.arcgis.com/en/pro-app/latest/sdk/api-reference/topic9385.html)。

**失败与验收**：小型已知答案与人工独立结果比较；确定性算法用预定数值容差，含随机过程记录seed与允许误差；许可不足、空几何、地理转换缺失、部分输出、取消分别测试。

## M08 · 栅格、地形与遥感

**职责/接口**：复用 `IRasterService` 与批准GP，补P6候选；输出栅格contract和可复核统计。

**实现步骤**：
1. 在运算前固定CRS、像元大小、snap/origin、extent、波段语义、位深、NoData和重采样策略。
2. 连续变量和分类变量采用不同重采样/比较规则；分类数据不能默默双线性混合。
3. 用有界窗口读取/分块统计；需邻域的算法增加halo，拼接时裁掉重复边缘；全局水文算法不能随意按块计算再拼图。
4. DEM地形/水文先复用已批准GP；新增白名单按需求逐项提名。遥感首版限定已定义的波段组合、指数或时序差异，不把云/阴影/传感器差异解释成真实地物变化。
5. `raster_change_detection` 检查配准、日期、量纲、云/质量掩膜；来源未知则在报告给出不可比较原因。
6. pyramids/统计/侧车文件均登记写副作用。远程影像流另经M18，不无上限下载。

**失败与验收**：小矩阵逐像元已知答案、NoData传播、不同origin、位深溢出、多波段顺序、分类重采样、块边缘、超大图预算/取消；与M16做数值与图面双验。

## M09 · 地图状态、符号化与标注

**职责/接口**：复用 Maps/Layers/Selection/Layout 宿主服务；在Core定义不依赖SDK的地图样式规格。承载现有图层/符号工具和新增set_label_properties。

**实现步骤**：
1. 通过明确project/map/layer身份解析对象，同名返回候选；跨运行保存数据来源绑定，不只保存活动视图指针。
2. 对制图任务创建拥有的地图/布局副本或显式授权目标；不为导出连续改用户活动地图的可见性并忘记恢复。
3. 分类渲染保存字段、方法、断点、颜色、空值类别和时间可比性政策；跨时期比较默认固定断点，自动重新分类要明示。
4. 标注保存字段表达式白名单、优先级、比例范围、冲突规则；表达式不能变为任意脚本通道。字体缺失检测与替代需要可见。
5. 基础地图/底图记录来源与署名约束；离线缺底图不伪报完整地图。
6. 3D/时序属于对标补差候选：若纳入Q-DELTA，先明确Scene/Time能力、许可证和二维成果投影方式，不假定现有2D接口已覆盖。

**失败与验收**：改同名错误对象、图例与分类不一致、单位错误、中文字体回退、标注遮挡、不同DPI、用户并行改地图、退出时事件解绑；前后snapshot只证明地图状态，不证明源库可回滚。

## M10 · 布局、地图系列与模板

**职责/接口**：拟 `ILayoutComposer.Compose/Validate`，底层复用ILayoutService；承载B2布局工具和P7出版工具。

**实现步骤**：
1. 模板用版本化LayoutSpec＋DesignSpec描述槽位、地图框、标题、图例、比例尺、北箭头、指标和署名；Native布局模板仅作为可选宿主载体。
2. 模板输入先绑定DatasetContract和指标fact，缺必需数据时不可只填外观。
3. 页内采用固定网格、边距和明确约束布局；度量文字边界后做有限候选调整，禁止无限试图自动排版。
4. 每个地图框绑定独立地图/extent/scale/rotation，比例尺和北箭头跟随对应框；通用元素位置修改与地图框相机设置共享规则内核。
5. 地图系列固定索引字段、页排序、页名冲突策略、extent规则和页清单；逐页导出与检查，不能首页成功就算整册成功。
6. 导出保存页面物理尺寸、DPI、ICC/色彩策略、字体策略与透明度。多比例尺/多地图框不能共用一个无归属比例尺。

**实现资源**：20模板每套含 `template.json`、样例输入contract、预览、许可、验证规则与坏样例。先各3套，后各8套，再各10套；改颜色/纸张只算同模板参数。

**失败与验收**：长中文标题、地图框为空、图例项过多、十页以上系列、同页多图框、缺字体、输出越界、比例尺/北向/legend绑定和实际路径；逐图视觉检查配合元素边界检查。

## M11 · GIS→PS 分层素材包

**职责/接口**：拟 `IDesignBundleExporter.Export`、`IDesignBundleValidator.Validate`；外部export_design_bundle/validate_design_bundle。输出版本化目录及manifest，传输归档可后加。

**首版路线**：在同一布局快照和地图框条件下导出透明全画布栅格分层，配独立文字/整饰描述；在PS置入受控组。不是把一份PDF交给PS就认为保留了GIS对象结构。

**实现步骤**：
1. 固定页面毫米尺寸、最终像素尺寸、DPI、每个地图框extent/CRS/rotation、像素原点和渲染环境。所有层使用相同画布，禁止按非透明区域自动裁切后丢偏移。
2. 把底图、专题、道路边界、标注/关联整饰、可编辑标题等分成语义组；共享遮罩/跨层混合的对象可合为一个渲染组，保留视觉正确性并披露粒度。
3. 在拥有的临时地图/布局副本上导出。标注单独导出可能改变避让布局，因此须验证共同布局结果；不能保真时把标注与相关地图层一并渲染。
4. 文字单独保存内容、样式、页坐标、字体；比例尺/北箭头/定量图例为受保护关联整饰，不默认交给自由文字自动重排。
5. 清单记录semanticId、zOrder、placement、alpha、mediaType、contentDigest、依赖以及“可编辑文字/受控栅格组/可替换智能对象”等实际能力。
6. 置入后的五点锚和像素尺寸验证配准；简化的north-up地理到像素公式仅适用于无旋转/倾斜的地图框，其他情况以宿主导出变换和锚点为准。
7. 大画布按内存预算选择格式/分层策略；是否用PSD或PSB由实际尺寸/版本能力决定并 `[VERIFY]`，不承诺无限大小。

**清单草案**：

```json
{
  "schemaVersion": "1.0",
  "bundleId": "owned-bundle-id",
  "revision": 1,
  "canvas": {"widthPx": 4961, "heightPx": 3508, "dpi": 300},
  "mapFrames": [{"id": "main", "crsRef": "contract-crs", "transformRef": "main-transform"}],
  "layers": [{
    "semanticId": "main.coverage",
    "file": "layers/coverage.png",
    "role": "quantitative-map",
    "mapFrameId": "main",
    "placementPx": [0, 0],
    "editableAs": "controlled-raster-group",
    "contentDigest": "computed-at-export",
    "dependencies": ["artifact-coverage"]
  }],
  "checks": {"registration": "pending", "compositeVisual": "pending"}
}
```

上例是结构草案，像素由页尺寸取整得出，不是已生成文件。科研高精度矢量成果仍保留GIS PDF/SVG等独立交付；可选Illustrator链不阻塞PS主链。

**失败与验收**：透明边缘/叠加色、标签位置、不同extent误置、单位和DPI混淆、图层漏项、多地图框、missing assets、篡改hash、中文路径。预定配准容差和视觉容差在spike冻结。

## M12 · Pro↔Photoshop 受限通信

**职责/接口**：拟 `ICreativeHost`、`ICreativeSessionBroker`；桥接是内部宿主适配，所有业务命令先经过M01/M05/M06，不开放第二MCP服务或6511旁路。

**首选设计（需ADR批准和spike）**：UXP主动向现有6520回环监听器的受限内部路径拉取已批准命令并回送结果；保留`/mcp`原语义。当前HttpMcpTransport未实现该路径，必须单独实现路由隔离/认证/兼容，不能直接发请求就算打通。先短轮询验证，之后才评估有界长轮询，不依赖UXP可启动本地监听服务器。

**消息与流程**：
1. 首次用户在两端配对，短期配对凭据换会话；session随机令牌不进入模型上下文或日志。会话绑定插件身份、目标PS实例、允许D盘根与协议版本。
2. 启动握手返回PS/UXP/插件版本、有限commandIds、文档能力、可读写目录状态。关闭面板、重启PS、重启Pro分别验证，不假设插件永远在后台活跃。
3. 拟命令信封含sessionId、commandId、requestId、jobId、stepId、expectedDocumentRevision、deadline和严格类型payload；只有服务器已通过授权的命令能进入队列。
4. 拉取、认领、开始、完成/失败分状态；重复requestId返回已知状态，回执丢失先对账。命令过期后拒绝迟到执行；令牌撤销后停止取新命令。
5. 仅允许明确回环host与方法，验证请求来源/Host、会话和大小，拒绝开放CORS、任意URL、任意文件路径与任意actionJSON；PS token没有调用GIS工具的权限。
6. UXP网络和本地文件权限按manifest显式声明。首选用户选择D盘工作根，通过存储entry/token访问；token失效要重选，不自动扩为fullAccess。[Adobe权限模型](https://developer.adobe.com/photoshop/uxp/2022/guides/uxp-guide/uxp-misc/manifest-v5/)。

**技术边界**：路径方案、端口、超时、轮询预算均集中Configuration；新增内部协议需要架构授权。若回环网络/UXP权限不可达，先停在导出素材包和人工导入降级，不能称自动闭环已完成，不能转为任意脚本执行。

**失败与验收**：错误/过期令牌、重放、串会话、跨文档、断线重连、迟到回执、未知command、尺寸超限、PS忙、Pro退出、插件面板隐藏后行为。UI应区分未连接、已配对、忙、版本不支持。

## M13 · UXP 文档、图层与受控操作

**职责/接口**：在拟`Source/PhotoshopPlugin`实现命令handler；业务暴露仍是唯一MCP registry内的20个PS工具。UXP内部handler表只接受固定commandIds。

**实现步骤**：
1. TypeScript构建为UXP可运行JavaScript；不假定浏览器/Node.js所有API可用。先用轻量UXP面板，避免新增WebView或另一个桌面壳。
2. 观察命令读取明确documentId及绑定；变更命令进入宿主级串行队列，使用`executeAsModal`。读取若需临时激活也应记录并恢复，不伪装绝无副作用。
3. 首选Photoshop DOM；DOM缺口用开发阶段记录并审查的内部batchPlay描述符，字段枚举/范围/目标由schema固定，外部不接受原始描述符。[Adobe batchPlay](https://developer.adobe.com/photoshop/uxp/ps_reference/media/batchplay/)。
4. 一次受控操作可合并历史记录；异常尝试恢复本次历史，但必须实测取消/失败行为。历史栈是会话内撤回机制，不能替代落盘快照和崩溃恢复。[Adobe模态/历史控制](https://developer.adobe.com/photoshop/uxp/2022/ps-reference/media/executeasmodal)。
5. 首次导入创建拥有的新文档/受控组：GIS_Content、Linked_Decorations、Design_Text、User_Custom。不强制已有用户文档遵循命名后就视为本产品所有。
6. 保存至批准D盘新版本，检查文件与重开能力；保存前确认未保存用户修改策略。PS忙返回可行动状态，不自动抢占用户模态操作。
7. 字体缺失、ICC转换、智能对象替换、PSB保存、蒙版、画板等逐项spike；API不可达则在能力表限制，不能用全局UI鼠标自动化偷偷替代并声称稳定API支持。

**失败与验收**：中文文字、文档关闭/改名、多文档同名、只读输出、磁盘满、模态超时/取消、历史回退、PS崩溃重开、缺字体、ICC差异、图层顺序、嵌套组、母版不被覆盖。

## M14 · 设计规则、风格配方与地理语义保护

**职责/接口**：拟 `IDesignRuleEngine.Validate`、`IDesignRecipeCompiler.Compile`；输入DesignSpec与用户目标，输出受控PS步骤和违规列表。

**实现步骤**：
1. 风格token定义字体层级、字号、颜色、间距、线宽、图例样式、页面网格与目标介质；科研和规划共一执行内核，分别加载规则集。
2. 地图保护分为Geometry（缩放/旋转/裁切）、QuantitativeColor（定量颜色）、LinkedDecoration（比例尺/北向/图例）和FreeDesign（标题/装饰）等角色。
3. 地图与整饰只能通过共同变换保持关系；非等比缩放、任意扭曲默认拒绝。旋转地图却不更新北箭头、改变地图尺寸却沿用旧比例尺都不能通过交付门。
4. 调整层作用范围限批准组；改变定量色阶时必须同步图例并验证语义，不能对科研图全局套滤镜后视作数据颜色未变。
5. 首版模板规则产生少量候选（如3个），以溢出/遮挡、字号、留白、对比度和地图占比排序；给低分辨率预览，最终高分辨率重新检查。
6. 标题/指标/图注可绑定M16事实表；用户改为自由文本时解除自动更新并明确显示，不能下次刷新悄悄改回。
7. 规划装饰素材记录许可/来源；素材和模板不能带可执行脚本。以后接生成式装饰时必须限FreeDesign且显式启用，不能修改分析或遥感真实内容。

**失败与验收**：量化色域被调整、字体最小值、长标题溢出、图例跨页、多图框绑定错误、设计旋转、缺图标、用户自由文字保留。审美排序由用户评阅，不将像素相似分数当“美观”真值。

## M15 · 增量计算、版本分支与三方刷新

**职责/接口**：拟 `IChangeImpactAnalyzer`、`IResultReusePolicy`、`IDesignMergePlanner`；外部 refresh_design_bundle、ps_refresh_design_bundle，版本比较用既定PS候选。

**依赖键**：输入强指纹＋规范化参数＋工具/schema版本＋算法/Host版本＋CRS/环境＋模板/样式/ICC版本。数据、分析、地图、设计各有依赖，修改标题不应重跑空间分析。

**安全复用步骤**：
1. 找变更节点并沿依赖边求受影响闭包，列出重算/复用/无法判定项；不能只比较路径和mtime。
2. 仅复用声明为确定性、只产出拥有结果、输入强指纹充分、产物hash/状态完整的步骤；外部服务变化或弱指纹默认重算。
3. 副作用写入、远程发布、原位编辑不作为普通缓存命中跳过；清缓存与删结果是不同操作。
4. 首版以完整步骤/语义组为复用粒度；要素/像元级增量以后只对已验证可增量算法开放，不笼统承诺所有GIS算法都能局部更新。

**三方刷新：B=上次系统基线；U=当前用户文档；N=新GIS内容。**

| B/U/N关系 | 行为 |
|---|---|
| U与B一致，N变了 | 对受控绑定生成替换计划，验证后应用到新版本 |
| U变了，N与B一致 | 保留U；标记人工修改，必要时更新绑定保护状态 |
| U和N都变了 | 只对独立可合并属性自动合并；同属性冲突进入审阅 |
| 无法判断U是否变了 | 视为冲突，另存候选；不以“没收到事件”推断未修改 |
| 用户删除了受控层 | 询问保留删除或恢复；不悄悄复活 |
| N删除了有人工修改的层 | 保留到冲突分支/归档组供选择，不静默删除 |

**身份实现**：语义ID由绑定服务分配，不由可改的显示名决定。文档/图层身份可用侧车映射＋可持久化宿主metadata（支持情况 `[VERIFY]`）；PS临时layerId重开后可能需重绑定。若无法可靠保存/匹配，第一版限制为整受控组替换并另存新文档。

**人工变更检测**：组合结构/属性快照、受控组内容校验、版本/事件信息；所有值都需能在目标PS版本可靠读取。不能以只比较位移/名称覆盖用户在图层内的手工像素修改。未知修改类别默认冲突。

**分支**：设计方案A/B共享只读分析artifact，拥有独立DesignSpec/binding/PSD；比较后选择版本，不在同一PSD里反复破坏式试错。合并仅限定义过的字段，未定义即冲突。

**失败与验收**：只换标题不跑GP、只换数据保留User_Custom、受控像素人工编辑、复制导致ID冲突、PSD重开、侧车丢失、删除图层、源数据原地变化、缓存污染、跨项目误复用。要求可证明的非冲突人工内容保留率100%，无法判定时安全停在冲突态。

## M16 · 来源、事实绑定、检查与成果交付

**职责/接口**：拟 `IArtifactStore`、`IProvenanceRecorder`、`IDeliveryValidator`；增强get_job_report并实现validate_delivery_package等入口。

**实现步骤**：
1. 每一步真实执行生成产物manifest，支持一工具多输出、sidecar、目录/GDB逻辑摘要。预期输出和实得输出分别记，不能仅有一个主文件hash。
2. 记录输入来源、数据版本、真实参数、工具/build/Host版本、许可条件、时间和可分发声明；原始数据历史未知不补写虚构来源。
3. 分析结果生成FactTable：指标ID/value/unit/numerator/denominator/rounding/sourceArtifact/method；地图图例、PS指标文本、报告表格从同一事实来源绑定。
4. 方法与结果报告优先结构化模板；AI润色仅可转述已存在事实，未验证推论明确区分。既定范围先交JSON/Markdown/已验图件，DOCX/PPTX专用导出如新增则独立排期，不默认为现成能力。
5. 交付检查分语义/结构/视觉三层：数据和单位、文件与引用、实际渲染。图像diff可定位变化，不能证明GIS语义或统计正确。
6. 导出目录先staging，逐项验证后生成最终manifest/完成标记；同卷受控rename可用于普通文件提交，GDB/PS多文件不能笼统声称全局原子。失败保留拥有的中间结果与恢复说明。
7. 最终结果包含GIS数据、图件、PSD/PSB、配方/参数、方法与来源清单、已知限制及版本；模板/字体/底图不可再分发的内容列外部依赖，不私自打包。

**失败与验收**：缺sidecar、GDB锁、损坏图件、错误图例、模板数字没更新、路径泄漏、输入来源不明、文件被替换、字体未嵌入/缺失、母版重开失败；必须检查实际导出文件而不是只看API返回成功。

## M17 · Pro 工作台与 PS 交互面板

**职责/接口**：Pro内五区DockPane（数据/任务/地图/设计/成果），PS提供连接、当前绑定、冲突和预览的轻量面板；均不自建第二套业务规则。

**实现步骤**：
1. 在Compatibility的Config.daml注册DockPane，View用WPF/XAML，ViewModel仅管理展示状态；通过IToolInvoker和作业查询服务操作。遵循Esri UI线程与MCT分工，不让ViewModel持有跨线程SDK对象。[Esri框架](https://github.com/Esri/arcgis-pro-sdk/wiki/ProConcepts-Framework)。
2. 普通模式选场景、数据、模板；专家模式展开同一份计划的参数，切换模式不能生成不同执行语义。
3. UI状态绑定jobrevision/events，断线可按revision恢复；进度区分阶段完成与子项完成，无法估计时显示当前工作而非假百分比。
4. 预览异步生成并带文档/计划revision；旧预览不能冒充最新结果。取消、重试、查看冲突、打开输出均有明确可用条件。
5. 关闭面板不等于自动取消任务；Pro退出的作业行为在M06定义。对事件订阅和预览资源做生命周期释放。
6. 错误展示“发生了什么、结果是否已写入、下一步”，技术堆栈在脱敏详情中；用户不需要理解OwnerToken或hash才能继续。

**验收**：键盘导航、Windows缩放/中文、长列表虚拟化、无PS模式、无AI客户端时运行配方、重复点击、UI关闭重开、过期预览、防误选同名文件；至少5名目标用户完成首次任务/改稿/更新三轮体验评阅。

## M18 · 外部数据与发布适配

**职责/接口**：拟 `IRemoteDataProvider`、`IPublishAdapter`；P8/P9只对逐个已验提供者承诺支持。

**实现步骤**：
1. Provider描述符声明支持协议/鉴权/分页/许可/成本/下载格式；先选一个真实需求源实现，未实现的STAC/OGC/ArcGIS服务不靠统一接口名宣称支持。
2. 发现→描述→用户选择→下载/导入分离。外发AOI、上传数据、服务发布各有范围预览，不把“查询网络”授权扩大为公开发布。
3. URL构造由适配器控制，提供者/域白名单、重定向复核、私网/本机访问限制、响应大小/文件类型/解压限制；私有企业服务必须有显式配置范围。
4. 凭据从现有或经批准的安全存储读取，以credentialRef关联；不存入配方、日志或PS bundle。缓存遵守许可和TTL。
5. 下载保存来源、时间/ETag等版本信息，断点续传需确认对象版本未变；不能用URL不变假定数据不变。
6. 发布到明确目标和可见性，先暂存/草稿（服务支持时），验证后再单独授权发布；失败仅清理本作业创建且身份可证的对象。

**验收**：分页漏重、限流重试、断网、许可不明、凭据过期、地址重定向、巨大下载、源版本变化、发布部分成功；外部服务无条件时不将“计划支持”计为已验证。

## M19 · 基准、观测与版本兼容

**职责/接口**：EvidenceRecord与支持矩阵；性能工具get_performance_stats；支持排障但默认不上传遥测。

**实现步骤**：
1. 复用现有UnitTests/IntegrationTests/ServerTests；新增用例围绕独立风险和行为，不机械为每个包装方法照抄实现断言。
2. 黄金数据包含确定答案、CRS/单位、许可、故障版本；几何/数值容差在看结果之前确定。浮点和渲染不要求跨宿主逐字节相同。
3. 记录语义正确性、文件完整性、实际渲染三种证据；截图/返回success/源码存在任何一种单独都不足以证明端到端成功。
4. buildDigest、工具/schema、Host/PS/UXP版本、模板版本和数据hash绑定每份报告。库/产品版本变化后相关证据标过期并重验，不从相近版本推定通过。
5. 统计工具/作业/Host耗时、排队、p50/p95、失败类型、内存、磁盘、缓存命中率和人工步骤；日志脱敏，诊断导出仅包含允许项。
6. 对标冻结竞品commit/tag和声明；共同任务同预算独立运行，对不可比较的宿主算法标注差异。功能数量由实际清单生成，source/LIVE/supported/available分别展示。
7. 初始体验预算：任务控制请求响应p95≤1秒（不等于底层已停），参考小任务预览p95≤5秒，元数据操作p95相对基线回退≤10%。F05固定机器/数据后校准并冻结；大栅格单列预算，不能硬套小任务数字。
8. 每次任务结束/故障注入检查资源回收、拥有文件、残留进程；若执行了进程清理必须复核remaining=0，不依赖管道首行推断成功。

**兼容策略**：先明确一个实际主力Pro/PS版本组合，维护旧net6已验范围；net8构件由真实构建脚本/宿主验证确定。Current占位不自动进入支持矩阵。MCP协议/长任务适配按真实客户端协商，不追随draft立即破坏旧契约。

**验收**：24×3核心、5隐藏、全部工具正负例、30场景、20模板×至少2种输入，安全故障注入、旧断点与旧调用兼容；支持矩阵每格有证据或明确NOT VERIFIED。执行报告只交PASS CANDIDATE，独立验收方裁定。

## M20 · 最终安装、升级与恢复

**启动条件**：G-FUNCTIONAL通过。此前只收集分发需求，不实施统一安装器改造或制作新一键包；必要内部构建/真实功能验证另按工单授权，不属于新分发包。

**实现步骤**：
1. 扩展已有one-click-setup及事务恢复流程；冻结已验payload、版本、hash/长度和依赖，不在安装脚本中动态拉源码编译。
2. 一个入口选择GIS-only或GIS＋PS，检测真实宿主版本，选择支持的esriAddInX，PS使用有效CCX及官方安装通道。Adobe可能要求系统/应用确认，不能宣传所有环境零提示。[Adobe安装机制](https://developer.adobe.com/uxp/guides/how-to/distribution/install/)。
3. 每组件事务记录Plan→Installing→Installed/Failed/NeedsRecovery；总单允许PARTIAL，不把Pro成功、PS失败显示为全部成功。
4. 客户端配置继续Plan/Validate/Apply/Restore，校验基线hash防陈旧备份覆盖，仅改用户选中客户端；令牌不入包。
5. 产物、模板、文档可随包；商业宿主、许可证、SDK、用户工程、测试fixture、凭据与不可分发素材不入包。
6. 官方安装位或Adobe管理位置如涉及C盘，列清精确例外并按实际授权执行；项目临时/日志/备份仍在D盘。不能更改系统临时目录或PS首选项来规避审批。
7. 使用实际候选包做干净环境、升级、重复安装、取消、失败恢复、卸载、中文路径、权限拒绝和实际MCP/PS握手验证。最后固定同一候选hash再交付。

**验收**：保护用户已有PSD/APRX/数据和较新配置；未完成实装不写“安装通过”；没有干净机证据就不宣称干净机一键稳定部署。签名与公开发布单独按授权，不因制作本地包自动外发。

## 21. 错误、状态与失败处理共约定

下表为拟映射，不增加现有33码。`details.reason`、`retryable`等新增字段需正式契约评审；不得私自改变旧调用的错误优先级。

| 场景 | 优先现有错误码 | 处理 |
|---|---|---|
| schema/单位/目标参数无效 | INVALID_ARGUMENT | 不执行；返回具体参数路径 |
| 未批准/只读/越权 | PERMISSION_DENIED | 不执行；保留原只读拒绝顺序 |
| 输出路径逃逸 | PATH_ESCAPE_REJECTED | 不写入，也不另找目录偷偷执行 |
| 缺许可证 | LICENSE_REQUIRED | 明示所需条件，不改为其他算法 |
| 文档忙/输入版本变了/三方冲突 | INVALID_STATE | 有界重试或重新预览；冲突不盲重试 |
| 功能版本不支持 | NOT_IMPLEMENTED | 能力目录标不可用，不伪造结果 |
| 输出已存在 | OUTPUT_EXISTS | 按已批准冲突策略处理 |
| 取消确认完成 | CANCELLED | 附部分产物；请求取消尚未完成是状态而非终态成功 |
| 宿主请求超时/失联 | TIMEOUT或既有适用码 | 标执行结果可能未知，先对账副作用 |
| 保存/断点失败 | EXECUTION_FAILED | 区分GIS计算成功与持久化失败，不宣称任务已完整完成 |

会话身份、授权范围、状态机属于内部新契约，不能仅因复用旧错误码就跳过兼容评审。

## 22. 模块之间如何接起来：一条实作样例

场景：设施直线500米覆盖，规划展板＋科研插图，第二期换数据保留PS排版。

1. M17收集目标；M01确认能力；M02检查设施、研究区、坐标与分母。
2. M03质检；M04将Buffer→Clip→统计→布局→素材包→PS→交付编译为受控DAG。
3. M05检查批准范围/输出位置；M06记录作业和步骤意图。
4. M07执行分析；M16保存coverage与facts等多个artifact；数据未变的下次重跑才考虑复用。
5. M09/M10生成两类布局，固定extent与分类；M11导出bundle及锚点。
6. M12向已配对UXP发送固定命令；M13创建新PSD；M14应用已选配方并保护地图含义。
7. M16核对指标、图例、比例、文件与渲染；用户在User_Custom增加标识和说明。
8. 第二期M15找受影响节点，M06重跑必要步骤；比较B/U/N后另存新版；M16再次验收并关联两期来源。

此样例不需要远程数据或路网许可证；后续路网、遥感、发布仍走同一套骨架，分别启用M08/M18等适配器。

## 23. 第一版明确不作虚假承诺的地方

- 不保证所有GIS算法可局部增量，只保证有依据的依赖失效与步骤级复用。
- 不保证任意PSD人工修改自动合并；未知变化必须进入冲突审阅。
- 不保证GIS要素导入PS仍是逐要素矢量；显示真实分层/编辑能力，另保留原生GIS矢量成果。
- 不保证跨Pro/PS全局原子回滚；保留可核对的步骤状态和版本副本。
- 不将PS关闭时仍可自动修改文档、无主程序许可证、所有版本兼容列作已支持。
- 不承诺一个240工具数字等于全面领先；必须完成冻结对标范围的数量、能力与质量验证。
