# GIS→设计链路：首波工具规格草案

日期：2026-09-27。配套 `GIS_TO_DESIGN_MASTER_PLAN_20260927.md`。**设计草案，不是已实现契约或执行授权**。基于本轮源码154工具；正式派单前重新去重。用户要求同时覆盖规划汇报与科研专题，本规格先建设共同底座。

## 1. 通用约定

- 工具名使用现有 `snake_case`；参数沿用camelCase。下面为JSON Schema风格简写，`*`表示必填，`?`为可选，enum用`|`表示。正式schema需要补长度、数值边界、additionalProperties及版本测试，不直接将这些文本当作JSON执行。
- 成功/失败继续使用项目现有OperationResult及MCP转换方式，不建立第二种顶层响应。本文件“成功”栏指Data内拟增加的结构；“失败”栏指复用错误码与结构化原因，具体Errors字段扩展需评审。
- 成功Data最低包含实际目标、实际输出或状态、warnings、未验证项；写任务关联jobId/auditId/产物清单。不能用`success=true`隐去局部失败。
- 可执行任务显式预检；dryRun返回计划不等于已经执行。新写接口建议`dryRun=true`、`confirm=false`为安全缺省，正式形式与既有工具一致后定版。不得声称这两个参数已存在于当前实现。
- 读类R：不创建业务文件、不改工程/PS状态；常规审计按既有策略。新增持续状态/缓存不能隐藏在R实现内。地图/文档会话修改S与文件/数据产出W均受只读模式、路径/目标守卫和必要确认约束。
- 路径只接受允许根的绝对路径或本会话签发的受限artifactId；拒绝目录穿越、符号链接/junction逃逸、不受信URL和任意UNC访问。输出先列精确路径，已有文件默认OUTPUT_EXISTS；只操作新建/获授权资产。
- schema预检失败用INVALID_ARGUMENT；目标缺失用NOT_FOUND或既有细化码；冲突/过期状态用INVALID_STATE；越界PATH_ESCAPE_REJECTED；只读/未确认PERMISSION_DENIED；许可LICENSE_REQUIRED；未实现NOT_IMPLEMENTED；取消CANCELLED；执行失败EXECUTION_FAILED或既有宿主码。禁止把所有失败都包装为INTERNAL_ERROR。
- 新增作业状态/响应字段、取消控制面、PS宿主通道都要契约评审；以“追加字段”为由跳过评审也不合适。
- `[VERIFY]`：需要目标Pro/PS版本的编译或LIVE spike；“官方存在”不表示本项目已经接通。

## 2. B1：可靠控制面（4个新增）

### 01 · list_jobs

- 描述/分类：分页列出用户可见的历史与当前作业，R。
- 输入：`{status?: string[], after?: string(date-time), before?: string(date-time), cursor?: string, limit?: integer[1..200]=50}`。
- 成功：`{jobs:[{jobId,kind,status,startedAt,updatedAt,done,total,ownerScope,resumable}], nextCursor, catalogRevision}`；路径脱敏，空集与存储不可读区分。
- 失败：INVALID_ARGUMENT（过滤条件/游标不符）、PERMISSION_DENIED（越所有者范围）、EXECUTION_FAILED（索引损坏）。
- 实现：复用作业存储，读旧断点；分页以固定快照/稳定排序避免并发新增造成漏项。不用扫描任意磁盘目录充当目录服务。
- 测试：0/1/1000作业、相同时间戳、分页中新作业、旧断点、损坏文件隔离、跨OwnerToken/会话可见性、中文job摘要。

### 02 · cancel_job

- 描述/分类：请求停止指定作业，S（控制面）。
- 输入：`{jobId*:string, reason?:string(max512), confirm?:boolean=false}`。取消不另要求dryRun，但须符合控制面确认决策。
- 成功：`{jobId,accepted,state:'cancel_requested|cancelled|completed|failed',alreadyTerminal,currentItem,partialArtifacts,cleanupStatus}`。accepted不等于cancelled。
- 失败：NOT_FOUND、INVALID_STATE（非本所有者/过期控制权等，具体权限边界另明确）、PERMISSION_DENIED（只读或未确认）、EXECUTION_FAILED。
- 实现：独立控制路径、CancellationToken与已有任务锁配合；请求生命周期与作业生命周期分离。底层不响应时保留cancel_requested和原因；不得先写cancelled再继续写产物。官方ExecuteToolAsync有取消相关重载；不同GP的取消行为`[VERIFY]`。
- 测试：排队取消、运行取消、完成与取消竞态、重复取消、PS模态取消、Pro退出、部分输出安全保留、只读开关与取消冲突。

### 03 · validate_plan

- 描述/分类：检查既有ProcessingPlan及未来设计步骤，R。
- 输入：`{plan*:object(existingPlanSchema), validationLevel?:'schema|metadata'=metadata}`；不在此增加不兼容plan格式，版本升级另审。
- 成功：`{valid,planDigest,issues:[{severity,stepId,path,code,message}],requiredCapabilities,estimatedOutputs,unknowns,estimatedResources,validationScope}`。资源估计带依据与区间；检测不全要写unknowns。
- 失败：INVALID_ARGUMENT（plan不合法）、NOT_FOUND、LICENSE_REQUIRED、PATH_ESCAPE_REJECTED；业务预检未通过可返回正常执行的检查报告`valid=false`，不可误称任务执行成功。
- 实现：从apply_processing_plan的dryRun提炼同一验证核心；源文件、字段、输出策略绑定digest。schema_only不访问宿主；metadata模式严格只读。
- 测试：合法/未知步骤、失效图层、歧义地图名、CRS未知、磁盘信息不可得、字段变化、输出冲突、同计划dryRun与validate结论一致。

### 04 · describe_tool_catalog

- 描述/分类：按场景与能力检索真实工具目录，R。
- 输入：`{query?:string, category?:string, effect?:'read|state|write', availableOnly?:boolean=false, cursor?:string, limit?:integer[1..100]=20}`。
- 成功：`{catalogRevision,tools:[{name,summary,effect,parameters,requiresHost,license,capabilityStatus,evidenceLevel,source}],nextCursor}`；区分registered/hostAvailable/liveVerified。
- 失败：INVALID_ARGUMENT；宿主不在线时返回静态目录并标unknown，而非捏造可执行。
- 实现：唯一registry元数据生成；证据/版本manifest可附加但不能成为第二注册中心。可提供只读MCP资源或提示模板，具体客户端兼容性先测。
- 测试：154当前全名去重、别名只作检索、无PS/许可不足、分页token、只读标注一致、tool schema哈希与实际tools/list对账。

## 3. B2：数据质量与专业制图（6个新增）

### 05 · get_geometry_info

- 描述/分类：按明确度量模型读取几何摘要，R。
- 输入：`{dataset*:string, filter?:object(existingFilter), metrics*:('area|length|centroid|envelope')[], measurement*: 'planar|geodesic', linearUnit?:string, areaUnit?:string, limit?:integer[1..1000]=100, cursor?:string}`。
- 成功：`{crs,measurement,units,features:[{featureId,metrics,geometryState}],nextCursor,warnings}`；精度、Z/M处理与空几何明确。
- 失败：NOT_FOUND、INVALID_ARGUMENT（单位/投影不相容）、LICENSE_REQUIRED如实际依赖需要、EXECUTION_FAILED。
- 实现：宿主几何服务；面积长度、质心定义分清，质心不必在多边形内部。具体GeometryEngine重载与目标版本`[VERIFY]`，不猜测方法签名。
- 测试：已知正方形、折线、多部件、孔洞、空/无效几何、经纬度输入、跨日界线、高纬区域、不同单位和分页。

### 06 · find_identical

- 描述/分类：首版限定属性重复检测，R；不宣称等同Esri完整同名GP语义。
- 输入：`{dataset*:string, fields*:string[min1], nullPolicy*: 'equal|distinct', stringPolicy*: 'exact|case_insensitive', limit?:integer[1..1000]=100, cursor?:string}`。
- 成功：`{comparisonMode:'attributes',groups:[{groupId,key,featureIds,count}],scannedCount,truncated,nextCursor}`；处理超限要区分结果截断与未全量扫描。
- 失败：INVALID_ARGUMENT、NOT_FOUND、TIMEOUT/REQUEST_TIMEOUT按现有边界。
- 实现：只读游标与有界分组；大数据超预算应拒绝或明确不完整，不能偷偷落TEMP。几何重复作为后续schema变更；如选Esri GP路线，其产出表须改标W并增加outputPath，不能在本R工具暗藏临时写。
- 测试：大小写、空串与null、浮点精确值、跨分页组、中文、超大组、无重复、扫描中源变化。

### 07 · generate_quality_report

- 描述/分类：组合现有读取能力生成可追溯QC报告，W（新文件）。
- 输入：`{datasets*:string[], checks*:('schema|crs|geometry|duplicates|statistics')[], outputDir*:string, formats?:('json|markdown')[], dryRun?:boolean=true, confirm?:boolean=false}`。
- 成功：`{jobId,reports:[{path,sha256}],findings:[{dataset,check,severity,evidenceRef}],coverage,unknowns}`；未实现检查标unverified，不能写“无问题”。
- 失败：PATH_ESCAPE_REJECTED、OUTPUT_EXISTS、PERMISSION_DENIED、NOT_FOUND、EXECUTION_FAILED。
- 实现：已有schema/统计+05/06；第一版检测不自动修复。源只读，报告输出单独守卫。
- 测试：健康数据/混合CRS/空几何/重复/空表、部分不可访问、统计采样与全量区别、输出冲突、只读模式、报告与原结果逐项一致。

### 08 · set_layout_element_properties

- 描述/分类：受控修改布局元素，S。
- 输入：`{layoutId*:string, elementId*:string, expectedRevision*:string, patch*:{text?:string, x?:number, y?:number, width?:number>0, height?:number>0, rotation?:number}, unit?:'mm|inch|pt', dryRun?:boolean=true, confirm?:boolean=false}`。
- 成功：`{elementId,before,after,layoutRevision,unsupportedProperties:[]}`；如任何属性不支持，默认执行前整项拒绝，不默默半成功。
- 失败：NOT_FOUND、INVALID_STATE（版本冲突）、INVALID_ARGUMENT（属性不适用于元素类型）、PERMISSION_DENIED。
- 实现：类型白名单适配；Element尺寸相关API官方存在，文本/组/地图框等具体属性`[VERIFY]`。明确锚点，单位只接受一套，锁定元素不可绕过。
- 测试：文本/图例/比例尺/组元素、元素同名、越页边界、负尺寸、中文长文本、期望版本不符、取消回退、保存/重开后读回。

### 09 · set_label_properties

- 描述/分类：配置图层标注的有限属性，S；补当前仅可见性控制的体验缺口。
- 输入：`{layerId*:string, labelClassId*:string, patch*:{field?:string,fontFamily?:string,fontSizePt?:number[4..72],color?:string,haloSizePt?:number[0..5],placementPreset?:string}, dryRun?:boolean=true, confirm?:boolean=false}`。
- 成功：`{layerId,labelClassId,before,after,missingFonts,warnings}`。
- 失败：LAYER_NOT_FOUND、INVALID_ARGUMENT、NOT_IMPLEMENTED（不支持的引擎/预设）、PERMISSION_DENIED。
- 实现：字段或固定表达式模板，不接受任意代码；Maplex/标准标注引擎的能力、字体查询与CIM读写`[VERIFY]`。自动无重叠不是保证值，导出后视觉检查。
- 测试：中英文字体、缺失字体、字段null、密集点/线/面、不同缩放级别、重复类名、保存重开、地图标签与图例/标题不混淆。

### 10 · configure_map_series

- 描述/分类：创建或更新空间地图序列配置，S；导出仍复用export_map_series。
- 输入：`{layoutId*:string, mapFrameId*:string, indexLayerId*:string, nameField*:string, sortField?:string, extentPolicy*: 'best_fit|fixed_scale', scale?:number>0, marginPercent?:number[0..50], dryRun?:boolean=true, confirm?:boolean=false}`。
- 成功：`{seriesId,pageCount,pageNames,configRevision,duplicateNamePolicy,exportReady}`；重名页不直接覆盖输出文件。
- 失败：NOT_FOUND、INVALID_ARGUMENT、INVALID_STATE、NOT_IMPLEMENTED。
- 实现：从既有export实现反向核对可配置模型；不同Pro版本SpatialMapSeries/CIM入口`[VERIFY]`；先空间索引序列，不扩展专题序列和3D。
- 测试：索引层0/1/10页、空/重复页名、排序稳定、异常尺度、多地图框、长中文文件名、逐页图面检查、旧export功能回归。

## 4. B3：GIS→PS完整链（6个新增）

### 11 · export_design_bundle

- 描述/分类：从固定布局导出可对齐的语义分层素材包，W。
- 输入：`{layoutId*:string, mapFrameId*:string, layerGroups*:object[], outputDir*:string, dpi?:integer[72..600]=300, colorProfile*:string, labelStrategy?:'frozen_overlay|grouped'=frozen_overlay, dryRun?:boolean=true, confirm?:boolean=false}`。
- 成功：`{jobId,bundleId,manifestPath,referencePath,artifacts,layoutSignature,degradations}`。
- 失败：INVALID_ARGUMENT、OUTPUT_EXISTS、PATH_ESCAPE_REJECTED、LICENSE_REQUIRED、EXECUTION_FAILED。
- 实现：独立副本/保存恢复的临时显示状态，禁止改变用户原布局；每层固定canvas而非按内容裁剪。透明背景、标注冻结和复杂混合效果`[VERIFY]`。MVP允许把不可分离效果组合成一层并披露。
- 测试：棋盘格已知定位、全透明/空层、不同图层顺序、透明度与混合、标注位置变化、输出失败/取消后原状态不变、路径中中文、多地图框限制。

### 12 · validate_design_bundle

- 描述/分类：校验素材包结构、定位和完整性，R。
- 输入：`{manifestPath*:string, level?:'structure|hashes|alignment'=hashes}`。
- 成功：`{valid,bundleId,checks,issues,alignmentResidualPx,coverage}`。未运行alignment应为not_checked，不能写0。
- 失败：NOT_FOUND、INVALID_ARGUMENT、PATH_ESCAPE_REJECTED；不合格素材返回valid=false及对应issues。
- 实现：所有相对路径归一后重新守卫；文件缺失/哈希/尺寸/DPI/图层ID/z序/地图框签名校验；需要渲染写文件的检查不在R工具偷偷执行，可只读已有reference/preview。
- 测试：缺件、篡改、尺寸差1px、ICC缺失、路径穿越、重复ID、错z序、未知schemaVersion、完整合法包。

### 13 · ps_get_capabilities

- 描述/分类：只读查询实际连接的PS与已验证操作，R。
- 输入：`{documentId?:string}`。
- 成功：`{connected,hostVersion,uxpVersion,adapterVersion,documentState,capabilities:[{name,status,minVersion,reason}],writableRoots}`；status为verified/unverified/unavailable/requires_user。
- 失败：INVALID_ARGUMENT；PS未连接时返回connected=false与指导，不假称可执行。
- 实现：B0受限握手/能力协商；模型输出不能自行把unverified升级为verified。版本和能力信息需来自PS插件，不仅读安装目录。
- 测试：PS未安装/未开/插件未加载/未配对/旧版本、忙态、断线后缓存过期、只读查询不改变活动文档。

### 14 · ps_import_design_bundle

- 描述/分类：校验素材后创建本批PS文档与稳定图层组，S+W。
- 输入：`{manifestPath*:string, outputDocumentPath*:string, templateId?:string, dryRun?:boolean=true, confirm?:boolean=false}`。
- 成功：`{jobId,documentId,documentRevision,path,bundleId,layerBindings:[{layerId,psLayerId,role}],importWarnings,previewArtifact}`。
- 失败：INVALID_ARGUMENT、NOT_FOUND、PERMISSION_DENIED、OUTPUT_EXISTS、INVALID_STATE（PS忙/版本不符）、EXECUTION_FAILED。
- 实现：经13确认能力后用UXP DOM/有限batchPlay创建；地图内容层与用户组分离。智能对象置入/替换、中文文本、PSD/PSB保存`[VERIFY]`。没有能力时停在素材包，不自动转用任意脚本。
- 测试：尺寸/锚点逐项对齐、分层顺序、字体/ICC、原文档不变、未保存文档、导入半程取消、断线重试不复制图层、PSD重开。

### 15 · ps_apply_design_recipe

- 描述/分类：在允许组执行版本化设计配方，S。
- 输入：`{documentId*:string, expectedRevision*:string, recipeId*:string, recipeVersion*:string, parameters?:object(recipeSchema), dryRun?:boolean=true, confirm?:boolean=false}`。
- 成功：`{jobId,beforeRevision,afterRevision,appliedSteps,protectedGroupsUnchanged,previewArtifact,warnings}`。
- 失败：INVALID_ARGUMENT（未知配方/越界参数）、INVALID_STATE（人工编辑冲突/忙态）、PERMISSION_DENIED、CANCELLED、EXECUTION_FAILED。
- 实现：允许有限调整层、文字、蒙版和排版操作；不能发送自由JS/原始descriptor。规划模板允许较丰富装饰；科研模板锁定定量颜色/图例与几何。先执行模态操作与历史分组，最终仍保文件版本。
- 测试：两类模板各3场景、重复应用幂等、参数越界、定量色阶保护、User_Custom不变、模态冲突、取消、重开后状态/视觉检查。

### 16 · ps_export_deliverables

- 描述/分类：另存PS母版并导出预先声明的屏幕/印刷图件，W。
- 输入：`{documentId*:string, expectedRevision*:string, outputDir*:string, outputs*:[{format:'psd|psb|png|tiff',name:string,profile:string}], dryRun?:boolean=true, confirm?:boolean=false}`。
- 成功：`{jobId,artifacts:[{path,sha256,format,pixelSize,profile,bytes}],documentRevision,unverified}`。
- 失败：OUTPUT_EXISTS、PATH_ESCAPE_REJECTED、INVALID_ARGUMENT、INVALID_STATE、EXECUTION_FAILED。
- 实现：格式与profile实际支持情况`[VERIFY]`，PS保存到新路径；多产物部分成功需返回逐项状态，不把先导出的PNG当整单成功。不擅自覆盖用户母版、转CMYK或压平主文件。
- 测试：不同格式、文件名冲突、磁盘不足、大画布预算、取消/进程关闭、重新打开PSD/PSB、嵌入ICC、导出图尺寸与manifest一致。

## 5. B4：更新、配方与交付（5个新增）

### 17 · refresh_design_bundle

- 描述/分类：根据新GIS结果生成素材包新版本及差量表，W。
- 输入：`{baseManifestPath*:string, layoutId*:string, changedLayerIds?:string[], outputDir*:string, dryRun?:boolean=true, confirm?:boolean=false}`。
- 成功：`{newBundleId,parentBundleId,diff:{added,changed,removed,unchanged},newManifestPath,requiresFullRebuild,reason}`。
- 失败：NOT_FOUND、INVALID_STATE（数据/布局签名不符）、INVALID_ARGUMENT、OUTPUT_EXISTS。
- 实现：复用11，差量不仅检查数据，也检查渲染器、字体、标注依赖、画布/范围。未变文件可引用已验证只读包或复制，关系必须在manifest可追溯；不能让交付包依赖用户临时目录。
- 测试：单层数据改、样式改、图层删/加、extent改、关联标注连带改变、旧包不变、断点恢复。

### 18 · ps_refresh_design_bundle

- 描述/分类：替换受控GIS内容并保留人工美化，S+W。
- 输入：`{documentId*:string, expectedRevision*:string, newManifestPath*:string, outputDocumentPath*:string, conflictPolicy?:'fail'=fail, dryRun?:boolean=true, confirm?:boolean=false}`。
- 成功：`{jobId,newDocumentPath,updatedBindings,preservedGroups,conflicts,oldBundleId,newBundleId,previewArtifact}`。
- 失败：INVALID_STATE（双边修改/绑定丢失/合并图层）、INVALID_ARGUMENT、OUTPUT_EXISTS、EXECUTION_FAILED。
- 实现：三方比较原bundle绑定、当前PS文档、新bundle；只替换工具拥有内容，smart object更新`[VERIFY]`。稳定ID丢失时不按图层名猜测。为所有权/转换签名提供持久存储方案，不能只放进易丢的名称。
- 测试：用户新建组/修改标题/移动内容/栅格化/合并/改名、受控内容+源同时变化、无变化幂等、取消失败恢复、重开后再刷新。

### 19 · validate_delivery_package

- 描述/分类：检查既有交付包的完整性和声明符合度，R。
- 输入：`{manifestPath*:string, target*: 'planning|research', checks?:('files|hashes|layout|provenance')[]}`。
- 成功：`{valid,blockingIssues,warnings,manualReviewRequired,checks,evidenceRefs}`；人工审阅未完成不能自动升格为终验PASS。
- 失败：INVALID_ARGUMENT、NOT_FOUND、PATH_ESCAPE_REJECTED；规范不符合返回valid=false。
- 实现：只读现有文件与PS验证报告；PSD真实重开验证由16/18等在新建会话中产证，本R工具不隐式打开或修改PS。比较图例/比例尺/指北针等既有证据，不能凭存在文件断言视觉正确。
- 测试：缺母版、缺来源、图例颜色漂移、输出路径不一致、过期预览、缺字体报告、未人工确认、合法包。

### 20 · suggest_workflow

- 描述/分类：根据场景从已验配方生成候选处理计划，R。
- 输入：`{scenario*:string, inputs*:object[], deliverable*: 'planning|research', constraints?:object}`。
- 成功：`{templateId,templateVersion,proposedPlan,assumptions,questions,requiredCapabilities,unsupportedGoals}`。
- 失败：INVALID_ARGUMENT、NOT_FOUND（无对应配方）；不应凭空返回一条貌似可运行的流水线。
- 实现：可控模板/语义检索优先；LLM负责解释和映射，输出必须经03验证；不能静默选CRS、统计分母、覆盖策略。
- 测试：同义中文描述、缺字段、不同单位、任务不支持、PS缺失降级、模板版本变化、检索结果与真实registry一致。

### 21 · get_performance_stats

- 描述/分类：聚合已采集的调用/作业性能与失败指标，R。
- 输入：`{from*:string(date-time), to*:string(date-time), groupBy?:'tool|job|host'=tool, toolName?:string}`。
- 成功：`{groups:[{key,count,success,failed,cancelled,p50Ms,p95Ms,queueWaitMs,executionMs}],window,coverage,sampleSize}`。
- 失败：INVALID_ARGUMENT、PERMISSION_DENIED（访问范围）。没有采集的数据标unknown，不能用0代表没有延迟/失败。
- 实现：复用统一诊断/审计采集，不另写每工具日志；区分排队、宿主执行、序列化、导出耗时。无敏感参数、令牌、图片内容进日志。
- 测试：零样本/单样本/大样本、时间区间边界、取消计数、聚合分位数、日志缺口、隐私字段、读取不影响作业。

## 6. 既有接口增强与后置能力

不新增工具计数的增强：

| 项 | 草案 | 必须证明 |
|---|---|---|
| get_job_report | 增artifacts[]，区分file/dataset/directory，带fingerprintMethod/complete/limitations；缺哈希给原因 | 普通文件现有sha256字段兼容；GDB不冒充普通文件；失败汇总同轮 |
| apply_processing_plan续跑 | 可评审retryFailed子语义，绑定旧planDigest、新jobAttempt与幂等键 | 成功项不重跑；数据变更后拒旧token；非幂等项先检查中间结果 |
| get_audit_log摘要 | 可由report组合步骤、结果和证据引用 | “解释”不虚构原因；每句可回到记录 |
| run_batch图层操作 | 模板展开稳定ID并先回显目标，再逐项执行既有工具 | 不因批处理绕过confirm/只读/目标歧义 |
| 渲染器/配色 | 配方驱动set_layer_renderer；分类边界、颜色与legend共同更新 | 数值语义不变；颜色和类别无错配 |

后置数据治理能力不在首波21个承诺内。每项进入工单前另补正式规格：几何属性（单位/投影/字段冲突与copy-output）、重复删除（保留规则/数据级备份/可复验恢复）、简化（算法/拓扑/容差/许可）、CRS定义（原CRS与新定义理由）、域/子类型/关系类（数据库支持/Schema锁/引用依赖/版本支持/失败回滚）。公开文档证明部分DDL接口存在，不能据此宣称全部数据库类型和Pro版本已可用。

## 7. B0必须产出的可行性证据

| Spike | 最小问题 | 通过门 / 降级 |
|---|---|---|
| GP取消 | 当前宿主取消令牌是否贯通？哪些调用只能等安全点？ | 控制请求可及时返回；终态不虚报；取消后既有数据不误删 |
| 布局分层 | 固定画布透明导出能否保持标签、效果与定位？ | 基准点≤0.5px数值误差（目标，渲染差异另设容差）；复杂效果可成组降级 |
| PS通信 | 目标版本UXP能否使用受限本地通道并通过配对？ | 断线/过期/重放拒绝；无任意脚本出口；端口与权限设计经评审 |
| PS编辑 | 智能对象创建/替换、中文文本、调整层、模态取消是否可用？ | 只把实测能力标verified，其他明确降级 |
| PS保存 | PSD/PSB/TIFF/PNG是否满足尺寸、ICC、重开与取消恢复？ | 每种声称支持的格式独立留证；不支持就不在catalog宣称 |
| 刷新保持设计 | 数据替换能否保留User_Custom及合法设计变换？ | 故意制造人工冲突时拒绝；保存新版本，旧文件逐字节不变 |
| 双代矩阵 | net6/net8真实工具面与包内/外manifest是否一致？ | 构建、运行、LIVE三列分开；未测Pro版本不推定兼容 |

依据：本地 `Composition.cs`、`FolderWorkflowTools.cs`、`ErrorCodes.cs`、`gp-whitelist.json`、D-079工单；外部官方API与竞品来源见总体计划对应段落。所有成功与失败例子是拟定测试要求，不是本轮测得成绩。
