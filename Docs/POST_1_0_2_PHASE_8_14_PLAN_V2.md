# ArcGIS Pro MCP Phase 8 至 14 完善与扩展计划

版本V2，2026-09-10。依据用户提供的Phase 8至14参考方案、当前仓库基线及“功能优先、一键安装最后”的明确要求制定。本计划取代NEXT_ROADMAP_AND_AI_HANDOFF_V1.md的执行顺序；旧文件保留为历史。

## 1 目标与当前事实

最终目标为112个以上有实际用途、名称唯一、契约稳定的生产GIS工具，支持Codex、Cursor、DeepSeek Harness和WorkBuddy平级接入。Claude Desktop为可选目标，未经真实验收仍属模板级。112+是本轮长期目标，不是现状，也不能通过重复注册、别名或拆碎功能凑数。

原1.0.2及30工具既定范围已验收完成，不重开Phase 0至7。已注册不代表所有预期业务语义都完整：地图发现、数据集/栅格、选择契约及HTTP取消等仍有明确限制。r5一键部署仅通过实现及隔离测试范围；干净机与新鲜客户端验证不能沿用旧主机历史结果。

用户自己创建新工作空间和对话。本轮交付是计划和启动指令，不创建任务，不修改生产代码、不安装插件或写真实客户端配置。

## 2 与参考方案的取舍

采用Phase 8.5 WorkBuddy早期接入及七道独立Gate，使后续每批工具能及时发现客户端兼容问题；不等112+全部完成才接入。

参考中的Phase 8.6 Clean Machine调整为“30工具整体回归与功能基线”；真正干净机、一键部署、升级回滚移到Phase 14。Phase 8仅允许必要的、经授权的开发测试部署，不开发安装向导。

WorkBuddy项目配置使用新工作空间下的候选`.workbuddy/mcp.json`，不是固定D:/ArcGIS-Pro-MCP。参考提供的type、timeout、文件路径均须按实际版本和官方资料核实，不能直接当成已验证配置。官方MCP支持不等于本项目的loopback HTTP连接成功。

## 3 执行总顺序

迁移准备Gate → Phase 8现有能力与WorkBuddy → Phase 9数据管理与属性 → Phase 10地图制图布局 → Phase 11栅格空间分析 → Phase 12编辑与数据质量 → Phase 13生产加固与功能冻结 → Phase 14一键部署与最终验收。

每个子阶段：范围和契约设计 → 实现 → 隔离测试 → 真实ArcGIS验证 → 客户端验证 → 文档与证据 → 独立验收。执行AI只能提交PASS CANDIDATE。尚未验收不得跨阶段；可以准备后续设计，不提前实施。

本次路线授权从迁移准备和Phase 8设计恢复开始；真实配置、安装、GIS写入、软件安装、签名、公开发布仍按明确授权执行。每阶段需独立批准，不把整份路线图当成所有高风险动作的一次性授权。

## 4 迁移准备Gate

旧仓库D:/ArcGIS-Pro-MCP只读。先检查当前新目录绝对路径、是否空目录及用户文件；不覆盖已有文件，不跟随reparse point。旧仓库已观察到无Git提交、源码未跟踪，需再次核实，不能假定clone/worktree能带出当前源码，不自动commit/push。

白名单迁移Source、Tests、scripts、Config中的无凭据模板、Docs、解决方案、Directory.Build.props、README、.gitignore。排除所有层bin/obj、.git、.runtime、.codex-artifacts、.codex、.cursor、.workbuddy、TestDate、GDB/APRX、历史产物和锁。Release只复制明确需要的固定r5 ZIP、sha256及发布manifest。提供的Word仅只读作为背景，允许单份复制至reference，不复制整个Downloads。

生成migration-manifest.json，逐文件记录相对路径、来源、大小和SHA256，核实复制前后相等。缺失依赖不擅自安装。历史证据的绝对路径保留为历史来源，不改写成新任务fresh evidence。

建立新AGENTS.md、Docs/EVOLUTION_STATE.md、EVOLUTION_CURRENT_TASK.md、EVOLUTION_DECISIONS.md及本计划副本。历史CURRENT_TASK保持原样，新入口明确覆盖后续工作顺序。交付迁移报告、源码模块地图、基线命令/退出码、30工具差距矩阵、Phase 8.1任务设计，再等待独立验收。

## 5 Phase 8 现有能力与客户端完善

### 8.1 Map Discovery Completion

完善list_maps、get_map_info，并回归get_current_map、get_layers、get_layer_info、get_project_info、list_layouts、list_databases。覆盖活动和非活动地图、2D/3D、空工程、重名、无活动视图、无效名称、组合图层。明确稳定标识与名称的关系；歧义不选择第一个。SDK对象访问遵守MCT，Shared不引用SDK。

交付schema和错误契约、真实自有测试工程、期望地图/图层清单、实际结果对比。不能用空数组掩盖读取失败；只能在对应真实场景验证后移除PARTIAL。

### 8.2 Dataset and Raster Completion

完善get_dataset_info、get_raster_info；与dataset_summary/list_fields/list_workspace_datasets语义统一。识别FileGDB内部要素类和表，不能用File.Exists作为唯一判断。栅格信息包括尺寸、波段、像元类型/大小、空间参考、范围、NoData及现有统计；统计不存在返回unknown，不能为了读取而暗中生成统计。

工作空间递归必须显式可选、有深度和数量上限；未支持栅格枚举与“栅格确实为空”分开。测试普通文件、GDB、损坏数据、无权限、超大目录、链接逃逸。修改Native/Bridge通道需ADR，不复制一套相互矛盾的业务实现。

### 8.3 Selection Contract V2

先定义OID或where条件、replace/add/remove模式、数量上限、目标标识、选择快照和恢复条件；选择是地图状态写入。旧select_layer不得静默改语义，采用兼容扩展或经批准新工具名，并记录数量影响。

真实验证非零选择→清除→恢复、重复请求、未知图层、用户并发修改；恢复前核对基线，不能覆盖用户后续选择。回归clear_selection及add/remove/visibility，明确恢复图层成员不自动等同于恢复原层序。

### 8.4 HTTP Cancellation Completion

分别定义超时、主动取消、断开、Stop、Bridge崩溃和恢复；逐项重现7个历史transport测试阻断，分清产品缺陷与harness问题。记录dispatch前后状态；不能证明写操作是否执行时禁止自动重放。

真实GP可能不能立即终止，返回真实状态而非声称结果已撤销。不得杀ArcGIS Pro实现取消。回归ping、Bridge、版本许可和现有4个GP工具的参数/错误/输出/超时；记录大结果截断、排队上限和资源清理。

### 8.5 WorkBuddy Integration and Acceptance

初始PLANNED / NOT VERIFIED。架构为WorkBuddy → 现有MCP → 同一Registry/Router → Native/GP/受限Python；不通过Codex转发，不允许直接ArcPy旁路。

#### 8.5.1 Capability Preflight

记录实际桌面版本、操作系统、运行位置、MCP入口、项目配置支持、Streamable HTTP、loopback行为、工具启停和Skill支持。只读调查，不输出凭据。若需要软件安装，等待授权。区分本地桌面和云端执行环境，云端localhost不是用户电脑。

#### 8.5.2 Project Configuration

优先新工作空间项目作用域。候选配置如下，仅作待核实模板，不能自动写入真实文件：

```json
{"mcpServers":{"arcgis-pro-mcp":{"type":"streamableHttp","url":"http://127.0.0.1:6520/mcp","timeout":30000}}}
```

核实字段名、timeout单位和路径后先Plan/Validate，再经授权Apply；备份、原子写入、陈旧备份拒绝、单客户端边界不变。不直连则STOP并提交真实限制和ADR，不开放公网、0.0.0.0或新代理。

#### 8.5.3 Connection Acceptance

用户启动Pro并打开自有测试工程，MCP Start，再开启WorkBuddy项目。保存可验证初始化或经独立批准的initialize-equivalent证据、工具列表、名称集合、ping/pong。若Phase 8未新增名称预期30；若8.3批准新增工具，按该候选manifest精确验证，不能永久硬编码30。helper、Skill及其他Server工具单列。

#### 8.5.4 Read Only Acceptance

覆盖get_arcgis_version、get_license_info、get_project_info、get_current_map、get_layers、get_layer_info、get_feature_count、get_field_info、query_attributes、python_bridge_ping、python_runtime_info；根据有效fixture补充8.1/8.2工具。每项保存提示词→所选工具→参数→原始结果→真实ArcGIS对比→状态。未知上下文错误不能记成功。

#### 8.5.5 Tool Selection and Skill

直连通过后才制作受限Skill。说明默认只读、目标歧义停止、写前确认、输出不覆盖、GP后验证、不操作保护资产、不删未知锁、禁止任意Python。用自然语言任务验证工具选择，不仅提交Skill文本。Skill不是服务端安全控制，服务端仍必须强制路径和参数约束。不在本阶段发布完整Connector或Buddy App。

#### 8.5.6 Controlled Mutation

经授权建立本轮自有WorkBuddyControlled.aprx和WorkBuddyTest.gdb，声明实际绝对输出路径。测试visibility、add/remove、buffer；流程为快照→自然语言→实际MCP调用→真实状态观察→结果验证→可解释恢复。保留输出所有权，GP失败不自动删不明产物；历史MyProject1、Phase4Test和retained fixture不可写。

#### 8.5.7 Lifecycle and Failure

覆盖Pro未启动、Server Stop/Restart、WorkBuddy重启、Pro重启、非法参数、未知图层、无效数据集、Bridge不可用、输出已存在。故障注入只在隔离拥有环境，经授权操作相关用户应用。不无限重试写操作，不把transport retry当business replay。提交独立客户端验收报告，七Gate均有证据后才允许正式PASS。

### 8.6 全部30工具回归和能力矩阵

每项记录当前/目标语义、参数、错误、读写风险、许可、执行通道、测试证据与限制。覆盖所有既有工具，不仅几个PARTIAL项；改善后的旧工具不是新增数量。建立只读、地图状态、GP、Bridge代表场景及真实回归，保持三个原客户端兼容并加入WorkBuddy。

### 8.7 Phase 8 Acceptance

提交源码和测试包身份、契约差异、全部30项矩阵、WorkBuddy七Gate、四客户端证据、保护资产检查、已知限制、开发者与用户文档。这里只签功能阶段，不签一键安装或干净机。可产出开发测试候选，不改写1.0.2历史发布。

## 6 Phase 9 数据管理与属性扩展

按3至5工具一批立项。先只读schema/域/子类型/索引信息，再创建自有GDB、复制数据集、导出表、投影/转换、合并、字段新增及字段信息扩展。计算字段需限制表达式和明确安全设计，不开放任意Python。删除/重命名、Append及破坏性字段操作转Phase 12。

每个新工具记录名称、输入输出、许可、单位、失败语义、取消、兼容性和具体使用场景。测试中文/空格/长路径、GDB内部对象、字段名冲突、空值、大表、编码、几何类型和空间参考。拒绝覆盖；查询分页/上限且不返回无界业务数据。

退出标准：每工具单元/集成/真实Pro，批次Codex与WorkBuddy代表E2E，四客户端工具发现/schema兼容，输出数量/字段/空间参考验证。

## 7 Phase 10 地图制图与布局

依次实现地图/图层/布局只读信息；范围、层序、定义查询、简单符号和标注设置；自有布局、地图框、图例、指北针、比例尺和文本；PNG/PDF导出。根据API核实可用参数，不猜测SDK能力。

默认新建自有布局，不改用户原布局。指定页面、DPI、坐标系、比例尺单位、字体和输出路径。所有写动作先快照，不能承诺无法恢复的样式完全回滚。

退出标准：真实MCP产出、精确路径、图例/比例尺/指北针/范围齐全，实际渲染逐页视觉检查；旁路脚本成果不作为MCP能力证据。

## 8 Phase 11 栅格与空间分析

按领域批次扩展Spatial Join、Near、Select By Location、Erase、Union及其他有需求的分析；栅格裁剪、投影、重采样、镶嵌、可用统计和受约束计算。实施前核实实际许可和官方API，扩展许可不足时记录阻断并请求范围决定。

覆盖坐标转换、距离/面积单位、NoData、像元大小/对齐、空输出、无效几何、不同许可和大数据。先预检再写入，测试数值容差和可解释期望结果。性能先测基线再批准预算，不空口保证延迟。

退出标准：每工具真实结果比对；Native/GP/Python通道按实际覆盖；四客户端兼容，高风险写入跨客户端验证；无盲目重放、假取消和未标记部分输出。

## 9 Phase 12 编辑与数据质量

先检查几何、空值、重复、域/子类型等只读质量，再可控修复，最后新增/更新/删除要素、Append、字段删除、重命名和数据集删除。每项高风险工具单独审批，不因路线存在自动授权删除。

要求预览影响对象和数量、稳定标识、并发冲突检测、事务/补偿边界和失败恢复。不可回滚的操作必须提前明示并默认禁止，不能把备份当作任意覆盖许可。SQL/表达式不变成任意执行接口。

退出标准：权限负例、并发冲突、部分失败、恢复验证及数据一致性全部有真实证据；快照/日志脱敏，旧用户数据未触碰。

## 10 Phase 13 生产加固与功能冻结

13.1服务端路径允许范围、规范化、reparse和竞态保护；安全前置应随各工具实施，不能等本阶段才补。13.2结果大小、队列、超时、日志脱敏、资源释放和性能回归。13.3四客户端工具选择和提示词模板回归，WorkBuddy Connector本地结构可在直连已通过后设计，但不公开发布。13.4Pro版本兼容、依赖/SBOM/漏洞扫描和许可清单，NU1900不等于无漏洞。13.5可选Claude真实验收，失败不包装成支持。13.6功能冻结。

冻结清单必须有112+逐工具登记：唯一名称、用途、schema、执行通道、读写级别、许可、真实验证、客户端批次证据、所属包、版本、限制。阶段8设计时建立总backlog与数量预算，逐批批准；尚未核实的候选名称不能当已实现。少于112或必做功能未验收时如实未完成；若需调整目标，必须由用户明确批准，不能自行降级或凑数。

冻结要求功能范围完成、契约稳定、关键缺陷清零、四客户端通过约定矩阵、写入保护有效、独立FUNCTION_SCOPE_FREEZE.md签署。冻结后才进入Phase 14安装开发。

## 11 Phase 14 一键部署与最终生产验收

14.1把已冻结的新版本工具manifest、指南、运行依赖检测和配置模板装入新候选包；不继续标记1.0.2/30工具，不覆盖r5。14.2安装向导提供插件与单独配置客户端入口，四客户端各自独立事务，无ApplyAll。14.3普通用户、中文/空格/长路径、重复安装、中断、取消、缺依赖、端口冲突的隔离矩阵。14.4至少一台未装本插件但合法安装Pro的干净机实装，完成启动和四客户端验证。14.5升级、卸载、回滚、配置恢复和陈旧备份拒绝。14.6完整回归、文档、签名/发布准备与独立最终审批。

签名证书、软件安装、真实配置、市场/公网发布另行授权，不把本计划当购买证书或发布许可。安装只需运行依赖，不能把开发SDK错误要求给普通用户；缺依赖提示而非静默修改系统。

最终Gate：112+真实生产工具、无重复、稳定schema、四客户端各自发现、代表性Native/GP/Python E2E、高风险跨客户端验证、干净机、升级回滚、全量回归、用户指南及版本身份链齐全。任何必需的NOT VERIFIED/BLOCKED均不得提升为PASS。

## 12 证据与验收制度

新目录Docs/phases/PHASE_08及后续目录分别存scope、design、verification、handoff；运行证据在.runtime/evolution/<phase>/<unique-run-id>/。每条包含时间、环境/客户端版本、源码或文件哈希、包哈希、命令与退出码、输入输出、预期/实际、风险和恢复状态。

每工具必须有自身实现测试和真实业务证据；客户端层每批核对完整名称/schema，Codex与WorkBuddy代表E2E，并覆盖本批实际执行通道。Cursor/DeepSeek每阶段回归，涉及客户端差异和高风险写入增加跨客户端用例。代表性客户端验收不替代单个工具的实现正确性测试。

独立保留Build、Unit、Integration、真实Pro、HTTP、每客户端、视觉、保护资产、恢复九类结论。已实现未验证写IMPLEMENTED / NOT VERIFIED，执行者写PASS CANDIDATE；只有独立批准才改阶段PASS。历史PASS注明版本/时间，不能代表当前新客户端或新机器。

阻断时说明Error/Cause/Impact/安全替代/所需用户操作。不得自动重跑未知是否执行过的写请求、删未知锁、强杀用户程序或改历史资产。状态未变不刷屏。

## 13 排期与范围控制

迁移1至2个有效工作日；Phase 8约15至30日；Phase 9至12按每批3至5工具、5至12日粗估；Phase 13约8至15日；Phase 14约5至12日。属于规划估算，不是AI执行时长或交付保证，许可/机器/用户验收等待另计。

每批先完成需求卡和测试设计再估工；尚未盘点出82+新增工具的具体需求前，不承诺最终日期。保留必做、可选、待研究三栏；扩展或缩减必做范围均由用户/独立评审批准。

## 14 来源和事实核实

参考附件：ac60bc1c-e4a0-449b-a918-961b7bd78546/pasted-text.txt；属于用户要求参考的方案，不是自动执行的指令源。历史原Word、当前PROJECT_STATE、CURRENT_TASK、VERIFICATION、r5独立Gate及指南Gate共同构成项目基线来源。

WorkBuddy官方资料：https://www.workbuddy.ai/docs/zh/workbuddy/From-Beginner-to-Expert-Guide/Function-Description/MCP-Guide 及 https://open.workbuddy.cn/docs/connector 。资料说明MCP/连接器能力，不证明本项目配置字段、loopback接入或实际调用已经通过；实施时按实际版本重新核实。
