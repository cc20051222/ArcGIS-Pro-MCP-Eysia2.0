# ArcGIS Pro MCP：从 GIS 分析到设计交付的总体计划

日期：2026-09-27。状态：**方案建议 / 待项目所有者与现任指挥席评审，非派工单、非 PASS 裁定**。

用户目标：全面提升相对开源 GIS/MCP 项目的竞争力，打通后续 Photoshop 美化；已明确选择“先建通用链路，再做规划汇报、科研专题两类模板”。Photoshop 具体版本、可用许可、硬件与样例成果尚未确定。

本文将用户提供的 `TOOL_EXPANSION_BRIEF_FOR_CHATGPT.md` 作为待核对的参考材料，未将其中任务书或 P1 标签当成执行授权。本轮只研究与写方案，不安装、不运行 LIVE、不改产品码、不新派 ACTIVE 工单。当前 D-079 仍为 ACTIVE，后续由现任指挥席串行派工。

## 1. 产品目标：交付完整成果，而非累计接口数量

建议定位为：**可验证、可继续编辑、可随数据更新的 GIS 分析与地图设计工作台**。

用户表达目标后，系统应完成：识别数据 → 澄清业务口径 → 预览计划 → 分析与质量检查 → 地图设计 → 分层素材交接 → Photoshop 美化 → 视觉与内容检查 → 交付工程及报告 → 数据变更后更新成果。

“全面超越”必须转换成可测范围：在选定专业场景中，完成率、正确性、制图品质、重复执行成本、故障恢复、安装体验与可编辑交付领先。不能承诺超过所有项目的每项能力：QGIS/GDAL 的免费生态与跨平台优势、Mapbox 的在线数据服务覆盖，并不是多写几个 ArcGIS 工具可以替代的。ArcGIS Pro 与 Photoshop 的软件成本也不会因为 MCP 而消失。

优先形成三个产品价值：

1. **任务可完成**：拿到一批数据，能稳定形成分析结果与成图，而不是让用户反复选择工具。
2. **结果可信且好看**：方法、单位、分类、图例与数值相互一致，图面经过检查。
3. **成果能更新**：数据更新后，替换 GIS 内容，保留设计师在 PS 中完成的样式与排版。

## 2. 本轮核对的基线与简报纠偏

| 项目 | 本轮证据 | 规划含义 |
|---|---|---|
| 注册工具 | `Composition.BuildRegistry()` 静态注册 154 件 | 源码计数已核对，不代表本轮已检验安装运行态 |
| GP 白名单 / 错误码 | `Config/gp-whitelist.json` 53；`ErrorCodes.cs` 33，已有 `CANCELLED`、`LICENSE_REQUIRED` | 取消和许可问题优先复用现有码 |
| 工具命名 | `FolderWorkflowTools.cs` 等实际公开名为 `snake_case` | 保持 `snake_case` 工具名、既有 camelCase 参数；不按简报整体改名 |
| 作业产物哈希 | 已有 `get_job_report`，逐项 `sha256`；实现仅在 `File.Exists(artifact)` 成立时算普通文件 SHA256 | 应补多产物、GDB 数据集与目录的完整性模型，不重复新增报告工具 |
| 多部件拆分 | 白名单已有 `management.MultipartToSinglepart` | 先做模板与发现，不当成全新 GIS 算法能力 |
| 现有水文/地形入口 | 白名单已有 Slope、Hillshade、Fill、FlowDirection、Watershed、ZonalStatistics 等 | 先核真实可用性/许可与任务模板，再决定专用封装 |
| 作业完善 | D-079 ACTIVE：计划歧义/默认值、计数原因、断点写放大 | 第一批建立在其验收基线上，不同时改同一状态机 |
| 测试 / r20 | 1565=1562/0/3 等为简报与历史记录，本轮未复跑 | 不用历史回归冒充新增 PS 链路验收 |
| 双代包 | r20 外侧 release-manifest：net6 为 D3133E0F/759448；net8 为 2F5406BA/779759 | 必须分别列功能与宿主支持矩阵，不能把当前源码154直接套到两代包 |
| 包内元数据 | 本轮只读查看 r20 两个 payload 的嵌入 deployment-manifest，仍见旧149/net6元数据 | 先核元数据的权威性与生成策略；这说明文档/包内标记需对账，不足以直接判定net8运行工具数 |

本轮未启动 ArcGIS Pro 或 Photoshop。SDK API 的“官方文档存在”与“目标版本实机通过”分开记录。现有旧差异化规划中的“竞品零覆盖”“工具数即五维领先”等表述，不作为新计划依据。

## 3. 开源对标：先学习长处，再组织公平比较

研究日期为本文件日期；下表来自维护者仓库与 README，**非安装实测排名**。后续基准测试必须固定 commit/release，不把不同版本结果拼起来。

| 对象 | 本轮文档可见优势 | 我方应对 |
|---|---|---|
| [Knight60/ArcGIS-Pro-MCP](https://github.com/Knight60/ArcGIS-Pro-MCP) | 112 命名工具，通用 GP、ArcPy 入口，与运行中的 Pro 集成 | 比较真实复杂任务；受控工具集要补足高频场景，不能用154>112证明功能覆盖更广 |
| [nkarasiak/qgis-mcp](https://github.com/nkarasiak/qgis-mcp) | README列125工具、27组复合模式、样式与编辑等操作 | 学习分组发现和较低的 schema 负担；保持既有工具兼容，另做目录/场景入口 |
| [jjsantos01/qgis_mcp](https://github.com/jjsantos01/qgis_mcp) | Processing、回图、任意 PyQGIS 等直接控制路径 | 对标从安装到首个任务的步骤数，以及透明的失败反馈 |
| [geo2004/MCP-ArcGISPro](https://github.com/geo2004/MCP-ArcGISPro) | Python 桥、批量操作配方、QC报告与发布入口 | 把数据到报告的完整配方产品化；发布能力另设授权边界 |
| [JordanGunn/gdal-mcp](https://github.com/JordanGunn/gdal-mcp) | 数据目录、栅格/矢量处理、CRS等方法论提示与预检 | 将“为什么使用该坐标系/重采样方法”写入分析计划 |
| [Mapbox MCP](https://github.com/mapbox/mcp-server) | 地理编码、路线、等时圈、交互地图等在线服务 | 作为后续可选数据/服务适配方向；不自建全球路网，不默认上传用户数据 |
| [StarBoze/Photoshop-MCP-Server](https://github.com/StarBoze/Photoshop-MCP-Server/blob/main/README.en.md) | UXP等后端及图像操作接口 | 参考适配实现，不直接照搬临时目录、端口和命令面 |
| [photoshop-full-mcp](https://github.com/muhwagwa0112/photoshop-full-mcp) | 文档描述能力分级、操作队列、UXP桥及LIVE证据要求 | 说明“受控/有证据”并非独有；我们的差异应落在GIS语义和数据更新保持设计 |

不以 README 未写某功能证明该功能不存在；不把有任意代码入口直接判成存在安全漏洞。比较开放执行与受控执行时，应同时展示覆盖面和风险控制的取舍。

### 3.1 超越的验收仪表板

建立24个固定用例，四组各6：数据接入与质检、分析与统计、制图与系列图、PS交接与增量更新。共同GIS任务比较同一输入数据、目标、模型版本、提示、调用预算与硬件；PS端到端能力另与可组合的GIS+设计工具链比较，标明组合成本。

每个关键场景至少重复3次；保存原始调用、输出、时间、人工介入次数和失败原因。文档宣称覆盖与LIVE测得覆盖分列。新增5个隐藏变体用于防止只适配演示数据。

| 维度 | 提议验收目标，非当前成绩 |
|---|---|
| 场景完成率 | 预先定义的核心流程重复执行≥95%；数字正确性硬判据全部通过 |
| 正确性 | 已知答案：要素数、面积/长度容差、CRS、单位、NoData、统计分母均核对 |
| 受保护资产 | 所有越界/误覆盖/歧义目标负例拒绝，验证零副作用 |
| 恢复能力 | 取消、Pro崩溃、PS关闭、磁盘满、断电式中断五类演练；不得重复已完成写操作 |
| 制图品质 | 规划/科研模板各至少3例；专家盲评与自动检查并行，重大误导性图面0件 |
| 编辑与更新 | PSD/PSB重新打开成功；只改一个源层时，用户自定义设计组保留，未变图层不重做 |
| 成本/性能 | 记录p50/p95、峰值内存、模型token、人工步骤；对自家基线与对照系统分别比较，不预造倍数 |
| 安装 | 干净机、升级、失败回滚、自诊断均有独立证据；双代/PS各版本逐格标记 |

## 4. 全链路架构与职责

```mermaid
flowchart LR
 U[自然语言目标与交付要求] --> P[结构化任务计划与预检]
 P --> J[作业状态与证据清单]
 J --> G[现有 ArcGIS MCP 工具与宿主服务]
 G --> A[数据分析与质量检查]
 A --> M[制图模板与固定地图框]
 M --> B[Design Bundle 分层素材包]
 B --> X[受控 Photoshop UXP 适配器]
 X --> E[可编辑 PSD / PSB 与两类模板]
 E --> Q[数值检查 + 视觉检查 + 人工确认]
 Q --> D[成图 / 分析报告 / 工程包]
 D --> V[版本化交付与来源追溯]
 A -. 数据变更 .-> B
 B -. 差量素材刷新 .-> E
```

GIS继续沿现有分层访问 `IArcGISHost`。Shared不引用ArcGIS SDK；注册只在 `Composition.BuildRegistry()`。所有GIS SDK对象遵守MCT规则，另按具体官方API核实UI线程、异步GP线程要求；不把PS等待、文件哈希和长时间I/O塞进MCT。

**PS为可选适配模块**。建议新增无Adobe/ArcGIS具体类型的 `ICreativeHost` 抽象，PS工具仍在唯一注册中心注册，经受控适配器与UXP插件交互。这是需评审的架构扩展，不把PS操作伪装成GIS SDK调用。没有PS时GIS流程照常完成，返回缺失能力与可用素材包。

优先验证“UXP插件连接现有本地服务的受限适配通道”，避免默认再开端口；通信方式、会话配对、鉴权与生命周期必须先spike。若现有HTTP实现不支持所需桥接，则独立loopback通道需单独审批并纳入Configuration，不能借用6511或增加任意脚本旁路。

MCP不提供raw `batchPlay`、JSX、Python、shell执行入口。PS适配器内部把有限的声明式操作映射到经过验证的UXP DOM/`batchPlay`描述符；文件访问限定批准根，命令携带jobId、requestId、过期时间、会话身份与预期文档版本。防路径逃逸、重放、跨文档误执行；模态写操作串行。

Adobe官方说明：修改PS状态须使用相应模态作用域，可提供取消、进度和历史分组；文件与网络权限在UXP manifest中声明。具体选项有最低版本差异，因此PS版本探测先于执行。[Adobe模态执行](https://developer.adobe.com/photoshop/uxp/2022/ps-reference/media/executeasmodal)、[batchPlay](https://developer.adobe.com/photoshop/uxp/ps_reference/media/batchplay/)、[权限声明](https://developer.adobe.com/photoshop/uxp/2022/guides/uxp-guide/uxp-misc/manifest-v5/)。

### 4.1 作业控制的实质工作

- 将“请求超时”“停止后续步骤”“正在执行的GP取消”“回滚已写结果”分开。取消请求被接受不等于已经取消，也不等于已复原。
- `running → cancel_requested → cancelled / completed / failed`：终态必须由执行结果确认；完成与取消竞态不能写两次终态。底层暂不可取消时报告等待安全点，不强杀Pro。
- 取消信号必须能到达运行作业，不能排在同一个长作业之后。采用独立控制路径；原子状态转换与OwnerToken兼容。
- 预检只估计、不写数据：CRS/字段/许可/输出冲突/磁盘需求/可用能力/未知输入/长任务上限。需要创建探针文件的检查属于部署或执行准备，不伪装成纯只读预检。
- 使用planDigest绑定参数、目标、输入指纹与覆盖策略；执行前再核对，防止预检后源数据改变。
- 重试只覆盖可判定幂等项；非幂等编辑在确认中间状态前不自动重试。APRX快照不等于GDB数据备份，不承诺跨GIS/PS分布式事务。
- 作业OwnerToken、租约/心跳、进程退出、人工编辑冲突采用明确恢复政策；不得未经审核更改D-077/D-079既有所有者语义。
- 文档描述、数据字段、元数据内容都视作输入数据，不得变成执行指令；不从数据附件自动执行脚本。

GP取消的SDK入口官方存在，但不同GP工具的响应速度、部分产物与宿主适配需LIVE验证。[Esri ExecuteToolAsync重载](https://pro.arcgis.com/en/pro-app/3.4/sdk/api-reference/topic9383.html)。

## 5. GIS → Photoshop 的关键交接契约

### 5.1 三种交付精度

| 路线 | 适用 | 可编辑性边界 |
|---|---|---|
| 整图PNG/TIFF置入PS | 首个演示与快速汇报 | 地图内部文字/要素不可独立编辑；不能作为最终全链路验收 |
| **语义分层素材包 → PS智能对象/图层组** | 推荐主线，两类模板共用 | 保留语义层、排版与替换能力；不承诺每个GIS要素成为PS矢量对象 |
| AIX → Illustrator → PS/排版 | 精细矢量地图、出版支线 | 另需Illustrator及相应扩展/账户条件，增加部署与验收成本 |

AIX官方工作流面向Illustrator中的组织化图层编辑；PS打开PDF涉及栅格化选项，不能假设ArcGIS的PDF图层自动变为可编辑PSD层。GeoTIFF的空间参考也不能当作PS精确定位的保证。[Esri AIX](https://doc.arcgis.com/en/maps-for-adobecc/latest/install/using-aix-files.htm)、[Adobe导入说明](https://helpx.adobe.com/photoshop/using/creating-opening-importing-images.html)。

### 5.2 Design Bundle v1

建议每轮独立目录，所有路径为示意，执行时须重新声明实际绝对输出路径：

```text
D:\ArcGIS-Pro-MCP 2.0\.runtime\design-delivery\<run-id>\
  analysis\         分析结果、表格、方法与参数
  design-bundle\
    manifest.json   固定画布、图层顺序、定位、来源、哈希、版本
    reference.png   GIS完整参考图
    layers\         底图/影像/分析面/道路/边界/标注等语义层
    furniture\      地图整饰或可控文字数据
  photoshop\        master.psd或master.psb、预览与设计操作记录
  delivery\         成图、说明、打印/屏幕版本、交付索引
  evidence\         GIS与PS检查、视觉审阅、失败恢复记录
```

manifest至少包含：schemaVersion、jobId、bundleId、planDigest、sourceFingerprint、CRS/WKT引用、mapFrameId、extent、scale、rotation、页面毫米尺寸、DPI、画布像素、地图框像素范围、ICC标识、透明通道、渲染器分类/断点/颜色、图层稳定ID和z序、图层哈希、素材文件相对路径、源工具与版本、字体清单、NoData规则、来源日期和使用限制。

坐标规则：地理坐标→地图框→页面→像素分别记录；页面单位不默认等于像素。像素计算 `px = round(mm / 25.4 × dpi)`，x向右、PS y向下；固定原点与裁切框。第一版只承诺2D单地图框；多地图框逐框记录，3D和非仿射场景后置。

**所有分层导出使用同一extent、地图框、页面、DPI与像素尺寸；禁每层按内容自动裁边。** 透明层叠合后应接近GIS参考图。逐层隐藏会改变标注避让、混合模式和掩膜效果：第一版应冻结标注为整体覆盖层，或在副本中转为稳定注记；复杂跨层效果按语义组合导出并披露降级，不承诺任意地图无损拆层。

基础PSD组：`GIS_Content`、`GIS_Furniture`、`Design_Adjustments`、`Titles_Notes`、`User_Custom`。地图内容与比例尺/指北针保持同一变换体系；禁止独立拉伸地图导致比例失真。连续色带和分类颜色与图例一起更新，不能只在PS随意改分析颜色。

可编辑文本分级：标题、说明和页码用结构化文本重建；复杂地图标注第一版保真导出，明确非逐字可编辑。字体缺失不能静默替换；报告替代方案并要求视觉复核。

### 5.3 美化策略与更新机制

- 规划汇报：信息层级、底图弱化、研究区强调、标题/说明、插图、统计卡片、适量阴影。分析数值和边界来源受保护。
- 科研专题：稳定色阶与分级、单位、样本量/时间段、同系列同色域、色觉友好、数据与方法来源、误差/不确定性说明。不得对定量图使用改变含义的任意调色。
- 用版本化 `DesignRecipe` 声明允许操作、目标组、参数边界和模板版本。智能对象、调整层与蒙版优先；具体UXP能力逐项验证，不能把菜单可见当成可自动化。
- `bundle v1 → v2` 依据layerId、sourceFingerprint、样式和画布签名计算差异；只更新工具拥有的内容。保留`User_Custom`、手工文字与调整层。
- 两边都改同一层时报告冲突，不按mtime静默覆盖；用户已栅格化/合并智能对象、移动受控层时应降级为人工合并。
- 刷新写新PSD版本；失败保留上一版。PS历史记录只是会话内辅助，跨关闭/崩溃恢复依靠文件副本和manifest。
- 不把生成式美化用于修改真实边界、测量结果或遥感证据。若后续加生成背景，只作用装饰区，另标生成来源。

### 5.4 输出质量

RGB屏幕预览与印刷交付分别配置；300 DPI是常用默认候选，不是所有图件的强制答案。ICC、CMYK、出血和PDF标准依印厂/期刊要求确定。第一版优先PSD/PSB+PNG/TIFF，不承诺PS自动输出任意PDF/X规格。

大画布先算内存：A1约594×841 mm，300 DPI约7016×9933像素；单RGBA 8-bit图层约279 MB（十进制，不含历史/蒙版/缓存），多层很快达到GB级。设计预览与最终导出分开；设置像素/图层/内存预算，必要时PSB或拆页，实际阈值按PS版本验证。

GIS侧检查值、空间位置与整饰；PS侧检查画布、图层、字体、图例一致性、ICC与输出；最终100%与缩略视图都看。AI视觉检查只能辅助发现文字遮挡/层次问题，不替代数值对账。

## 6. 原28项候选的逐项处置

简报“28项”混合了新增工具与增强项；`get_job_report`并非新增，因此154→182不是可直接使用的算术。下表拆开create/delete_domain后按28项复核。

| # | 原候选 | 处置与理由 |
|---|---|---|
| 1 | cancel_job | **采纳，B1**。先设计取消请求/终态/部分产物，复用CANCELLED |
| 2 | list_jobs | **采纳，B1**。分页、稳定排序、所有者可见性、脱敏路径 |
| 3 | validate_plan | **采纳并改造，B1**。复用现有dryRun验证核心，禁止另一套验证规则 |
| 4 | get_job_report manifest增强 | **改造既有，B1贯穿后续**。已含文件哈希，补多产物与GDB逻辑指纹/缺失原因 |
| 5 | retry_job | **合并**到既有续跑设计评审。先做失败项过滤与幂等语义，不新增重复接口 |
| 6 | describe_tool_catalog | **采纳，B1**。由唯一registry生成，含当前宿主能力与已验证状态 |
| 7 | suggest_workflow | **采纳，B4**。用已测配方产生计划，不能伪装成万能自由代码生成器 |
| 8 | explain_operations | **合并**进get_job_report/get_audit_log的人读摘要，保留每句证据关联 |
| 9 | get_geometry_info | **采纳，B2**。明确投影/椭球、面积长度单位、空/无效几何及采样上限 |
| 10 | add_geometry_attributes | **后置改造**。先支持copy-output；共享几何计算内核，新增GP走评审 |
| 11 | find_identical | **采纳但重定义，B2**。首版只读属性重复检测；几何等价需spike。若走GP写表，应改为产出写工具，不混标只读 |
| 12 | delete_identical | **后置高风险**。重复报告→明确保留规则→数据级备份→确认；APRX快照不够 |
| 13 | simplify_features | **后置**。区分线/面、拓扑、容差/单位与成果用途，输出新数据，先验证许可与算法 |
| 14 | define_projection | **后置**。定义CRS与重投影区别必须回显，禁止猜测；在未知CRS数据副本上试验 |
| 15 | multipart_to_singlepart | **合并**到GP配方和catalog；已有白名单入口，先补LIVE场景 |
| 16 | generate_quality_report | **采纳，B2**。组合现有schema/statistics/geometry与重复检测；输出文件为写类 |
| 17 | create_domain | **后置治理批**。DDL有官方基础，数据库类型、锁与引用关系逐版本spike |
| 18 | delete_domain | **后置高风险**。被字段/子类型引用时拒绝，不能默认级联删除 |
| 19 | assign_domain_to_field | **后置治理批**。域类型相容、子类型覆盖关系、存量非法值先报告 |
| 20 | create_subtype | **后置治理批**。字段类型、代码唯一、默认子类型、作用域必须明确 |
| 21 | create_relationship_class | **后置治理批**。基数/主外键/关联规则/许可与恢复策略需专门验证 |
| 22 | set_field_properties | **合并/拆分**。alias等复用alter_field；默认值/可空/必填不能假定统一可改 |
| 23 | set_layout_element_properties | **采纳，B2**。元素类型与属性白名单；稳定ID、单位、锚点；不开放原始CIM任意写 |
| 24 | create_map_series | **改造为configure_map_series，B2**。与现有export_map_series区分，先验证索引/页名/排序/范围 |
| 25 | batch_layer_visibility | **合并**进run_batch+精确目标展开；通配符先预览，逐项守卫不绕过 |
| 26 | apply_color_scheme | **合并**进模板/DesignRecipe和既有renderer；统一分类与图例，不做泛化调色入口 |
| 27 | get_performance_stats | **采纳，B4**。先有可关联job/tool/host的测量，后聚合；无数据不返回假零值 |
| 28 | clear_result_cache | **暂删**。目前无明确缓存契约，先定义失效策略再决定是否需要接口 |

DDL官方提供SchemaBuilder方向，不能把简报中的“SchemaEditor能力”直接当作实现承诺；布局元素有可写尺寸等API，但文字/锚点/组元素属性并非同一套字段。[Esri DDL](https://www.esri.com/arcgis-blog/products/arcgis-pro-net/developers/ddl-in-pro-sdk)、[布局元素API](https://pro.arcgis.com/en/pro-app/3.0/sdk/api-reference/topic11072.html)。Find Identical官方工具会生成表，应据真实副作用分类。[Esri Find Identical](https://doc.esri.com/en/arcgis-pro/latest/tool-reference/data-management/find-identical.html)。

## 7. 最值得补的五个方向

1. **GIS与设计软件之间的稳定交接和刷新**：分层素材包、受控PS操作、语义ID、冲突检测、交付验证。
2. **专业制图质量**：布局模板、标注属性与避让策略、统一渲染/图例、系列图、字体/配色/比例尺一致性；不仅开关标注。
3. **方法正确性与数据质量**：CRS/量纲、几何有效性、统计口径、重复数据、NoData、来源与时效、不确定性。
4. **可持续运行的作业系统**：预检、取消、断点、幂等、版本化产物、跨程序恢复、人工修改冲突。
5. **专业场景与生态覆盖**：先本地GIS，再按用户任务逐项补遥感/水文/可达性/空间统计、OGC/STAC/在线服务、三维；以现有GP覆盖为基础，不能一次铺开全部领域。

## 8. 实施：四个交付批次，加一个不增工具的预研门

批次名为本方案编号，不占D编号；实际派工需等D-079裁定并现场去重。每批可拆成源码、LIVE、分发子阶段，仍保持单ACTIVE。新增数是预算，不是KPI；前置发现已有等价工具须扣除。

| 批次 | 新增候选 | 数量 | 主要交付与验收 |
|---|---|---:|---|
| B0：基线与PS可行性 | 无 | 0 | D-079承接；双代包能力矩阵；PS版本/UXP/权限/智能对象/中文字体/文件保存/取消最小spike；冻结Design Bundle v1；24场景基准集 |
| B1：可靠控制面 | list_jobs、cancel_job、validate_plan、describe_tool_catalog | 4 | 现有作业语义不回退；取消竞态/重试幂等/旧断点读取；report多产物设计；所有新工具契约和异常证据 |
| B2：GIS质量与制图 | get_geometry_info、find_identical、generate_quality_report、set_layout_element_properties、set_label_properties、configure_map_series | 6 | 质量报告与地图逐项对账；规划/科研各1套纯GIS模板；系列图至少10页逐页检查；旧工具仍兼容 |
| B3：GIS→PS最小完整链 | export_design_bundle、validate_design_bundle、ps_get_capabilities、ps_import_design_bundle、ps_apply_design_recipe、ps_export_deliverables | 6 | 两类场景各一套可重开PSD/PSB+最终图；分层配准/图例/字体检查；PS关闭/忙/取消/保存失败反例；源工程未损坏 |
| B4：刷新与产品化 | refresh_design_bundle、ps_refresh_design_bundle、validate_delivery_package、suggest_workflow、get_performance_stats | 5 | 换数据保留人工美化；冲突拒绝；打通24场景；安装/升级/恢复与性能报告，完成两类模板库 |

第一波候选新增21个，若无重名或合并，源码154→175；其中PS专用5个。不能把175宣传为两代包和所有版本已全部支持。

估算（非承诺）：以一名熟悉仓库的开发者、独立验收者及兼职制图审阅者为前提，B0约3–5工作日、B1约1–2周、B2约2–3周、B3约2–4周、B4约2–3周；合计约8–13周，双代实机不足/PS权限问题另影响进度。先取得B0结果再排正式日历，不能把长期“全面超越”压成四个一两天工具扩容批。

### 8.1 首个可展示的端到端样例

选“研究区道路与公共设施服务覆盖”数据：确定CRS与距离单位→缓冲/叠加→统计→质量报告→规划汇报图→分层导入PS→调版式与标题→交付。随后复用同一底座做“分区统计科研专题”：固定分级与色域→多页图→PS论文插图→数据替换→保持样式。第一版无需新增路网求解器或下载外部数据。

模板库起步各3套：规划汇报（区位现状、服务覆盖、方案对比）；科研专题（分级设色、变化对比、分区统计）。每套包含适用数据、字段语义、输入校验、示例、方法说明、色彩/字体token、设计配方、验收图和失败示例。

### 8.2 后续能力包

第一波闭环后，按真实需求排序：① 数据治理DDL与安全清理；② 地形水文/遥感现有GP配方与缺口；③ 空间统计与可达性（邻域定义/显著性/许可/路网来源）；④ 数据服务和联网协同（鉴权/分页/限流/来源权利）；⑤ 3D场景与高阶出版。每包先列“已有入口、未验证入口、真实缺口”，再定专用工具。

## 9. 分层验收与长期维护

所有批次证据分列：源码/单测、构建/包身份、运行时、真实E2E、人工视觉验收。执行交付只写PASS CANDIDATE；现任指挥席裁定。完整回归“零产品性失败”；环境阻断单列，不能从旧失败族自动推定新失败可忽略。

必须覆盖：读写模式、确认缺省、目标歧义、路径逃逸、输出已存在、许可不足、Unicode/中文路径、超大数据、取消/超时、中断恢复、跨版本、未知字段/未知属性拒绝。大量图件使用固定渲染环境、元素几何检查和容差图像比较；不要以不同字体/抗锯齿造成的逐字节差异误判GIS错误。

产物指纹区分普通文件SHA256、目录树manifest、GDB逻辑内容摘要。GDB不能靠当前被锁库目录的文件哈希证明逻辑数据相同；稳定字段/几何/CRS规范化、排序、大数据分块与验证成本需要明确协议。SHA256用于完整性，不自动证明来源可信；正式发布另需签名/来源证据。

维护工作纳入容量：版本支持表、迁移说明、示例数据、依赖清单/SBOM、敏感日志脱敏、安装回滚、用户复现包、每月场景回归。在线源不可用不应拖垮离线制图；PS不可用仍能交付GIS成果及素材包。

## 10. 需要项目所有者/指挥席裁定的事项

以下是计划进入实施前的明确决策点，不是本轮索要安装或配置授权：

| 决策 | 建议 |
|---|---|
| 产品主线 | 通用GIS→PS链路优先，两类模板共底座；工具总数作为统计指标 |
| PS支持版本 | 按用户实际安装版本先验证一个主版本，再扩展；现在不虚构支持范围 |
| 架构/通信 | 批准后才增加ICreativeHost与受限UXP通道；唯一registry和既有GIS路径保持 |
| Windows写入边界 | 项目素材/临时/审计全部D盘；PS安装、插件安装、字体/ICC及PS自身首选项/暂存盘行为分别核验；不能承诺正常运行PS绝对不写C盘；如需例外须明列并授权 |
| 只读模式与取消 | 默认新增写工具继续拒绝；若希望只读态允许取消现有写任务，需把控制面例外单独评审，不能偷改R6 |
| 白名单扩容 | 先复用53项；C/DDL后续如走GP逐工具提名，按快照diff+包内语料双锚，不一次开放任意GP |
| 错误码 | 首波优先复用33码，细分原因入details；新码另批，不先造PHOTOSHOP_*一整套 |
| 高风险修改 | 数据copy-output优先；删除/DDL必须数据级恢复；PS输出新版本，不覆盖用户母版 |
| 双代包功能政策 | 建议net8先做新链路、net6维持明确已验功能；若两代功能对齐，则增加实现与LIVE预算 |
| AI与外部数据 | 敏感数据/预览外发、生成式图像、联网下载分别明示；默认不因PS美化自动启用云服务 |
| 可选Illustrator | 仅确有逐要素矢量编辑/出版需求时进入，不阻塞PS主线 |
| 发布与许可 | 正式分发前审查所选依赖、字体、模板、底图许可和安装权限；不复制竞品代码后假定许可兼容 |

## 11. 近期建议

先完成D-079既有验收，再用一份B0预研单收集：目标PS版本、2份用户认可的参考成果、可公开的样例数据、固定GIS输入、输出规格和PS适配证据。下一步交付应是一条可重跑的完整示范链与接口草案，而不是再加28个独立按钮。

配套规格见 `GIS_TO_DESIGN_TOOL_SPECS_20260927.md`。它定义首波21个候选工具的参数草案、成功/失败信息、测试重点及实现验证门，正式开发前仍需与当时注册表逐名核对。
