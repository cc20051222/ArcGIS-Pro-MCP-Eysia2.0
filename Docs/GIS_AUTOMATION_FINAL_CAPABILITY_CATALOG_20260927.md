# 最终能力范围：工具候选、60场景与36模板

规划编号日期：2026-09-27；完成整理：2026-09-28。配套[最终总纲](GIS_AUTOMATION_FINAL_BLUEPRINT_20260927.md)。状态：规划目录；新增候选不是实现、授权或验收结论。

## 1. 计数和范围冻结规则

当前源码154；G-190基础候选88；本轮跨领域候选48。总名义候选池290，其中新增候选136。以下基础17批＋扩展8批＝25个候选批，每批最多8项；只是工作分解编号，不是D工单、不产生ACTIVE。

维护五本账：①公开命名接口；②独立业务操作；③逐名受控GP算法；④完整任务；⑤环境实际可用能力。只把真正注册且验收的工具计入①；通用GP、参数分支、模板与内部服务分别计数，不混算。

F03逐项判断：独立契约／扩既有参数／内部服务／不支持。不同名称不证明语义独立；依赖既有算法也不必然取消有价值的独立契约。候选合并后保留业务范围、调整数量，禁止为达到目标拆同义接口。

原详细契约来源：[首波21规格](GIS_TO_DESIGN_TOOL_SPECS_20260927.md)、[后续65清单](GIS_TO_DESIGN_CAPABILITY_BACKLOG_V2_20260927.md)。历史描述与本稿冲突时采用最新规划意图，再经正式F03裁定；尤其PS更新默认安全生成新版，不把自动保留任意人工编辑作为首版保证。

## 2. 基础88项：逐名保留并纠正批次

| 批次 | 数 | 候选工具名 | 业务边界 |
|---|---:|---|---|
| B1 | 4 | `list_jobs`, `cancel_job`, `validate_plan`, `describe_tool_catalog` | 作业控制、只读计划检查、全部MCP能力检索 |
| B2 | 6 | `get_geometry_info`, `find_identical`, `generate_quality_report`, `set_layout_element_properties`, `set_label_properties`, `configure_map_series` | 数据度量/检查，已有元素属性，标注与图册配置 |
| B3 | 6 | `export_design_bundle`, `validate_design_bundle`, `ps_get_capabilities`, `ps_import_design_bundle`, `ps_apply_design_recipe`, `ps_export_deliverables` | 精确GIS素材→PS自动成稿→实际成果 |
| B4 | 5 | `refresh_design_bundle`, `ps_refresh_design_bundle`, `validate_delivery_package`, `suggest_workflow`, `get_performance_stats` | 更新、完整交付、场景配方与观测 |
| P1 | 5 | `validate_geometries`, `check_topology_rules`, `detect_geometry_duplicates`, `compare_datasets`, `compare_schemas` | 几何、拓扑与版本质量 |
| P2 | 5 | `validate_field_constraints`, `inspect_raster_alignment`, `list_geographic_transformations`, `trace_dataset_dependencies`, `get_dataset_lineage` | 字段规则、格网/转换、可证明的来源依赖 |
| P3 | 5 | `create_domain`, `update_domain`, `delete_domain`, `assign_domain_to_field`, `remove_domain_from_field` | 域治理；引用检查、锁与恢复 |
| P4 | 5 | `configure_subtypes`, `create_relationship_class`, `calculate_geometry_attributes`, `define_projection`, `validate_relationship_class` | 子类型/关系类/量测写入；投影定义不等于变换 |
| P5 | 8 | `simplify_features`, `smooth_features`, `polygon_neighbors`, `generate_tessellation`, `calculate_service_areas`, `solve_routes`, `spatial_autocorrelation`, `hotspot_analysis` | 综合、邻接、格网、路网与空间统计 |
| P6 | 8 | `raster_reproject`, `build_raster_pyramids`, `raster_reclassify`, `extract_raster_values`, `zonal_histogram`, `compose_raster_bands`, `raster_change_detection`, `raster_pixel_inspect` | 栅格转换、分类、取样、波段与两期变化 |
| P7a | 8 | `import_layout_template`, `export_layout_template`, `clone_layout`, `remove_layout`, `create_layout_map_frame`, `set_map_frame_properties`, `configure_legend`, `configure_scale_bar` | 布局资源和精确图框/整饰 |
| P7b | 2 | `add_layout_picture`, `delete_layout_element` | G-190批准补差；不把原8＋2塞进同一批 |
| P8 | 3 | `discover_remote_datasets`, `describe_remote_dataset`, `import_remote_dataset` | 受控提供者；发现和导入两项当前暂缓 |
| P9 | 3 | `export_data_package`, `validate_data_package`, `publish_map_service` | 数据交接；发布当前暂缓 |
| P10 | 5 | `ps_list_documents`, `ps_get_document_info`, `ps_list_layers`, `ps_get_layer_info`, `ps_preview_document` | 文档/图层只读观察与有限预览 |
| P11 | 5 | `ps_validate_document`, `ps_create_document_snapshot`, `ps_restore_document_snapshot`, `ps_set_layer_properties`, `ps_create_adjustment_layer` | 检查、版本副本、允许范围的属性与调整 |
| P12 | 5 | `ps_set_layer_mask`, `ps_set_text_properties`, `ps_place_design_asset`, `ps_manage_artboards`, `ps_compare_document_versions` | 蒙版、文字、素材、多画板、版本比较 |

算式：B批21＋P1–P4/P10–P12共35＋P5–P9含P7拆批32＝88。PS名称为20个；G-190“19项PS依赖”是其门禁分组口径，不改写成目录只有19个。所有真实PS试验和开发按P-05及正式工单门执行。

## 3. 原16项语义重叠的复审表

D-080记录“70新增＋16重叠”原样保留，本表是F03复审建议，不覆盖其验收裁定。

| 候选 | 最终建议的判据 |
|---|---|
| `validate_plan` | 若现有apply_processing_plan有等价无副作用预检则合并；否则独立只读契约有价值 |
| `describe_tool_catalog` | 全部MCP工具检索与53项GP目录不同，优先保留；检索结果绑定真实Registry |
| `generate_quality_report` | 独立可复算报告、issue定位和版本结构成立才保留；仅拼接文本则内部配方 |
| `set_label_properties` | 可见性不等于表达式/字体/放置规则；检查现schema后优先保留有限写契约 |
| `calculate_geometry_attributes` | 可复用字段计算执行层，但须固定CRS/度量/单位；只算新入口，不算新数学算法 |
| `configure_map_series` | 配置index/排序/范围不同于导出已有图册，优先保留 |
| `configure_legend` | 核验add_legend能否更新已有对象及完整属性；能等价则合并，不能则保留 |
| `configure_scale_bar` | 同上；单位、样式、绑定图框需完整可验 |
| `raster_reclassify` | 专用契约与已白名单算法分账；不算新增算法 |
| `extract_raster_values` | 同上；多波段、插值、NoData和样点身份需明确 |
| `zonal_histogram` | 类别×分区频数与连续统计不等价；组合实现必须证明相同语义 |
| `raster_change_detection` | 两期变化方法边界明确；不能把任意Con表达式当所有变化分析 |
| `compose_raster_bands` | 波段堆栈与空间镶嵌/计算不同；核对band order、位深和元数据 |
| `raster_reproject` | 现project是否真正覆盖栅格待核；ProjectRaster白名单未获准不能假定已支持 |
| `find_identical` | 原定义为属性重复；与几何重复可选择一个参数化契约或两个清晰契约 |
| `refresh_design_bundle` | GIS文件包更新与ps_refresh_design_bundle跨宿主写不同，分别设计幂等/对账边界 |

F03每行输出 schema diff、等价/差异样例、能力账、最终处理、证据。没有证据时标待验证，不硬保数字。

## 4. 本轮48项跨领域候选：逐项实现意图

公共规则：以下名称是拟议工具名，不是SDK API。所有具体API、宿主版本、许可和额外依赖均为 **[VERIFY]**，正式批前提供文档＋最小真实试验。R＝读/内存分析；S＝宿主状态修改；W＝新文件/数据副本。R若增加报告落盘选项，该动作按W检查。除明确批准的治理写入，默认不改原数据。

### X1 时空与趋势（6）

| ID | 候选 | 类别 | 输入→输出与实现、验收重点 |
|---|---|---|---|
| X01 | `build_space_time_cube` | W | 带时间事件/空间单元＋间隔→时空聚合立方；受控时空算法，时区/缺测/分箱/总量守恒 |
| X02 | `detect_temporal_change_points` | W | 时间序列＋限定方法→变化点、效应量与条件；固定参数，合成已知变化点数值验证 |
| X03 | `forecast_spatiotemporal_series` | W | 有足够历史的序列＋预测窗→预测/区间；时间回测，数据泄漏检查；区间方法和假设可查 |
| X04 | `analyze_emerging_hotspots` | W | 时空立方→热点演变类别/统计量；区别静态热点，空间权重和多重检验政策留证 |
| X05 | `compare_temporal_trajectories` | W | 多区同量纲序列→轨迹差异/聚类/摘要；对齐、标准化和缺测政策明确 |
| X06 | `export_time_animation` | W | 时间层＋固定视觉规则→帧清单/动画；固定分类/相机/时间戳，帧丢失与编码验证 |

### X2 三维表达与分析（8）

| ID | 候选 | 类别 | 输入→输出与实现、验收重点 |
|---|---|---|---|
| X07 | `configure_scene_environment` | S | 场景＋地面/光照/坐标配置→受控场景；先核set_map_properties等价性，有则扩参数 |
| X08 | `configure_layer_elevation` | S | 图层＋绝对/相对/贴地政策→高程表达；Z单位、垂直基准、偏移可查；既有接口等价则合并 |
| X09 | `extrude_scene_features` | S | 要素＋高度字段/单位→拉伸表达；缺值、负值和字段绑定检查；不改变真实几何源 |
| X10 | `analyze_viewshed` | W | 高程面＋观察点/高度→可视域；受控分析，已知遮挡样例；版本/许可探测 |
| X11 | `analyze_line_of_sight` | W | 观察线＋地形/障碍→可见与遮挡段；高度基准和垂直误差披露 |
| X12 | `create_elevation_profile` | W | 路径＋高程面→距离/高程剖面与图表；抽样间隔、Z单位和断点准确 |
| X13 | `calculate_cut_fill` | W | 对齐前后地形＋边界→挖填方量/分布；单元面积、NoData和垂直基准验证 |
| X14 | `export_scene_package` | W | 场景与依赖→可交接三维包；格式/资源边界经验证，独立重开检查 |

### X3 点云与地形产品（6）

| ID | 候选 | 类别 | 输入→输出与实现、验收重点 |
|---|---|---|---|
| X15 | `inspect_point_cloud` | R | 点云→格式/分类/回波/密度/CRS/Z摘要；LAS/LAZ/COPC分别声明支持，不相互推定 |
| X16 | `filter_point_cloud` | W | 点云＋范围/类别/回波→过滤副本；点数量/范围/字段守恒检查 |
| X17 | `classify_ground_points` | W | 拥有的点云副本＋限定算法→地面分类；已知参考和坡地/建筑误分类评估 |
| X18 | `derive_terrain_surface` | W | 已分类点云＋分辨率→DTM/DSM；空洞、插值、格网和高程参考验收 |
| X19 | `derive_canopy_height` | W | DTM＋DSM/点云→冠层高度；共同格网、负差值/建筑掩膜、参考误差披露 |
| X20 | `compare_point_cloud_epochs` | W | 两期配准点云→差异与有效区；配准误差/密度差先检，不能将差值一概当真实变化 |

### X4 遥感与登记模型（8）

| ID | 候选 | 类别 | 输入→输出与实现、验收重点 |
|---|---|---|---|
| X21 | `assess_remote_sensing_quality` | R | 影像＋产品元数据→质量/覆盖/量纲/缺测摘要；传感器适配器，未知QA编码拒猜 |
| X22 | `mask_cloud_and_shadow` | W | 产品QA/已有模型＋影像→云影掩膜及副本；质量位定义和参考掩膜验证 |
| X23 | `segment_raster_objects` | W | 多波段影像＋限定参数→对象分区；稳定ID/边缘/尺度与资源上限验证 |
| X24 | `classify_land_cover` | W | 影像＋类别/训练样本→分类与模型元数据；训练/验证空间隔离，类别映射固定 |
| X25 | `assess_classification_accuracy` | W | 分类＋独立参考→混淆矩阵/分层精度/条件允许的区间；抽样设计和不平衡披露 |
| X26 | `infer_approved_raster_model` | W | 登记模型ID＋合规影像→真实推理结果；权重hash、许可、波段/归一化、训练适用域和GPU探测 |
| X27 | `build_raster_time_series` | W | 多期影像＋共同格网/时间政策→影像时间结构；区别X01的空间单元聚合，NoData和传感器一致性 |
| X28 | `detect_raster_trends` | W | 影像时间结构＋限定趋势方法→斜率/显著性/有效样本；自相关/季节/缺测和多重检验政策 |

### X5 科研、决策与不确定性（8）

| ID | 候选 | 类别 | 输入→输出与实现、验收重点 |
|---|---|---|---|
| X29 | `design_spatial_sample` | W | 范围/分层/约束/种子→样点与纳入规则；边界、距离、权重及可复现 |
| X30 | `interpolate_surface` | W | 样点＋限定方法/参数→预测面与可支持的误差面；不承诺所有方法有概率误差 |
| X31 | `cross_validate_spatial_model` | W | 观测＋模型＋空间分块→验证分数/残差；空间泄漏、折数、训练范围明确 |
| X32 | `analyze_scenario_sensitivity` | W | 明确参数域＋场景→敏感性/排名稳定性；扰动不冒充概率，预算限制求解次数 |
| X33 | `propagate_spatial_uncertainty` | W | 有依据的误差模型＋分析链→误差传播结果；相关性、分布和随机种子留证 |
| X34 | `compare_multi_criteria_scenarios` | W | 指标/方向/归一化/权重→多准则比较；限制条件独立于评分，权重不由AI偷定 |
| X35 | `evaluate_scenario_ensemble` | W | 有限场景集合＋可选依据充分的权重→汇总/分歧区；无概率权重时只称情景分布 |
| X36 | `validate_statistical_assumptions` | R | 变量/设计/拟用方法→适用条件检查；有限检验目录，未知不自动判适用 |

### X6 网络与设施决策（6）

| ID | 候选 | 类别 | 输入→输出与实现、验收重点 |
|---|---|---|---|
| X37 | `build_od_cost_matrix` | W | 起终点＋真实路网/阻抗→OD成本/未定位点；断连、时间限制与单位核验 |
| X38 | `find_closest_facilities` | W | 需求点＋设施＋方向/路网→最近设施和路线；不是欧氏near |
| X39 | `solve_location_allocation` | W | 候选点/需求/容量/目标→设施选择与分配；未服务需求明确，问题规模与终止条件可查 |
| X40 | `optimize_vehicle_routes` | W | 订单/车队/时窗/容量/路网→车辆路线与未分配订单；区别单路线，约束逐项验算 |
| X41 | `analyze_network_disruption` | W | 路网＋中断情景→前后可达/成本/关键路径；依赖复用，断连不伪造有限成本 |
| X42 | `calculate_cost_distance` | W | 成本面＋源→累积成本/可支持路径；非道路导航，单位/屏障/NoData明确 |

### X7 本地互通（4）

| ID | 候选 | 类别 | 输入→输出与实现、验收重点 |
|---|---|---|---|
| X43 | `validate_interchange_conformance` | R | 文件＋标准版本→schema/空间语义一致性报告；区别只校包hash，格式适配范围明确 |
| X44 | `transform_vertical_coordinates` | W | 带Z数据＋源/目标垂直CRS/格网→转换副本；元数据赋值不能代替转换，缺格网阻断 |
| X45 | `build_offline_map_package` | W | 地图/资源/范围→离线地图包；与export_data_package等价则合并，必须离线重开验收 |
| X46 | `export_spatiotemporal_cube` | W | 带时间/空间/变量语义的数据→科学数据立方文件；维度、单位、缺测、往返检验 |

### X8 同源报告与汇报（2）

| ID | 候选 | 类别 | 输入→输出与实现、验收重点 |
|---|---|---|---|
| X47 | `compose_evidence_report` | W | FactTable＋方法/图件/模板→HTML/PDF及经验证的DOCX；图文数字同源、引用正确、渲染检查 |
| X48 | `compose_presentation_deck` | W | 同源事实＋受众/页数/母版→PPTX；原生文字/有限图表可编辑、地图可用图片、字体与重开检查 |

X批合计6＋8＋6＋8＋8＋6＋4＋2＝48。现代格式和长图在下节作为既有入口增强，不另凑新增工具。

## 5. 数据与输出格式矩阵

F03冻结每种格式的“探测/读/查询/写/往返”支持，标版本、最大规模和丢失项，不能一句“支持”涵盖全部。

| 对象 | 目标路径 | 特有检查 |
|---|---|---|
| FileGDB、SHP、CSV/Excel、GeoJSON、常见栅格 | 现Host/受控导入导出增强 | 编码、字段截断、空值、CRS、Z/M、精度、sheet与日期语义 |
| GeoPackage、GeoParquet、FlatGeobuf | 优先复用宿主；缺失时经评审引入固定适配器 | 标准版本、几何列、多几何、轴顺序、CRS和往返；不按格式开新工具 |
| COG | 栅格读/写适配；本地优先 | tiled/overview结构与像元语义；本地TIFF不自动等于合规COG |
| NetCDF、Zarr | 多维栅格/科学数据适配 | 时间历法、维度、变量单位、chunks、NoData、垂直维 |
| LAS、LAZ、COPC | X3逐格式适配 | 压缩/随机读取并非互相等价；密度、类别、Z基准 |
| STAC、OGC API/服务、ArcGIS Feature/Image Service | M18受控提供者适配 | 能力发现、分页/范围、凭据、时效、许可和预算；在线执行遵守G-190暂缓门 |
| PDF/SVG/PNG/TIFF/JPEG | GIS矢量版与PS视觉版并存 | 字体、物理尺寸、ICC、位深/透明度、图件内容对应 |
| PSD/PSB | UXP保存，PSB大画布需真实验证 | 分组/语义ID、关联素材、重开、尺寸和内存限制 |
| PPTX、报告、长图 | X8＋自动排版/现有导出增强 | 内容同源、页面/长图文字溢出、可编辑性、文件真实打开 |

ArcGIS Pro官方文档确有多维NetCDF/Zarr路径，但这不证明当前插件、白名单或目标宿主已经支持。[Esri多维栅格说明](https://pro.arcgis.com/en/pro-app/3.5/help/analysis/spatial-analyst/multidimensional-analysis/multidimensional-raster-types.htm)

## 6. 60条端到端场景目录

每条固定输入、合法范围、方法/许可、产物、质量/负例/更新用例；底层API成功不等于场景通过。原30保留，S26/S27从“精修”明确为自动成稿，S30强调原件保护，不要求普通用户手工合并。

| ID | 场景 | ID | 场景 |
|---|---|---|---|
| S01 | 文件夹资料体检入库 | S31 | 时空事件分箱与趋势 |
| S02 | 多源坐标统一 | S32 | 时间变化点与前后比较 |
| S03 | 属性/几何重复审查 | S33 | 时序预测与区间表达 |
| S04 | 值域与子类型治理 | S34 | 新兴热点演变 |
| S05 | 数据版本变更审计 | S35 | 稳定分类时间动画 |
| S06 | 带依赖检查的数据交接包 | S36 | 城市立体高度与剖面 |
| S07 | 设施直线覆盖 | S37 | 可视域辅助选点 |
| S08 | 真实路网服务区 | S38 | 通视走廊分析 |
| S09 | 设施最短路线 | S39 | 挖填方平衡 |
| S10 | 规划叠加冲突 | S40 | 点云质量审查 |
| S11 | 邻接与格网统计 | S41 | 地面分类与DTM |
| S12 | 空间自相关与热点 | S42 | 冠层高度与参考误差 |
| S13 | DEM坡度坡向地形专题 | S43 | 点云跨期变化 |
| S14 | 流域与汇流分析 | S44 | 影像质量与云影筛除 |
| S15 | 分区土地类别统计 | S45 | 土地覆被分类 |
| S16 | 前后期栅格变化 | S46 | 独立分类精度评估 |
| S17 | 点位像元采样 | S47 | 影像对象分割 |
| S18 | 多波段专题合成 | S48 | 登记模型遥感推理 |
| S19 | 规划区位现状图 | S49 | 多年遥感趋势 |
| S20 | 用地/约束专题图 | S50 | 空间采样设计 |
| S21 | 方案A/B对照 | S51 | 插值与空间交叉验证 |
| S22 | 科研分级设色图 | S52 | 量测误差传播 |
| S23 | 研究区域系列图册 | S53 | 决策权重敏感性 |
| S24 | 固定分类时间对比 | S54 | 情景集合统计 |
| S25 | GIS语义素材自动导入PS | S55 | OD通勤成本 |
| S26 | 规划汇报自动成稿 | S56 | 公共设施选址优化 |
| S27 | 科研插图自动成稿 | S57 | 多车辆配送 |
| S28 | 屏幕/打印双版本交付 | S58 | 路网中断韧性 |
| S29 | 换数据保留设计意图 | S59 | 离线地图交接与往返检查 |
| S30 | 外部修改保护与交付验收 | S60 | 同源报告/PPTX自动交付 |

适用于市政/国土规划、公共服务、生态资源、应急、商业区位和科研，但不把每个行业别名重复计入场景数。完整产品另需在线提供者与发布专项用例；其门禁未解不记“已完成全在线链路”。

## 7. 36套自动模板：规划18＋科研18

模板是可执行约束、例图、字体政策、颜色/单位/图例、坏例及修正规则的版本包。不是一张PSD底图。专业设计投入用于开发模板和规则，最终用户不承担这些工作。

| 规划ID | 模板 | 科研ID | 模板 |
|---|---|---|---|
| TP01 | 区位与研究范围 | TS01 | 研究区域与采样点 |
| TP02 | 现状用地结构 | TS02 | 分级设色与分布 |
| TP03 | 公共服务直线覆盖 | TS03 | 变化量与差异分布 |
| TP04 | 真实路网可达性 | TS04 | 分类转移与对照 |
| TP05 | 生态约束叠加 | TS05 | 空间自相关结果 |
| TP06 | 规划冲突诊断 | TS06 | 热点显著性分布 |
| TP07 | 方案A/B对比 | TS07 | 地形水文多面板 |
| TP08 | 指标统计与空间分布 | TS08 | 遥感多波段对照 |
| TP09 | 分区管控图册 | TS09 | 时间序列小多图 |
| TP10 | 项目综合汇报展板 | TS10 | 不确定性与误差表达 |
| TP11 | 道路中断与替代通道 | TS11 | 预测区间与空间分布 |
| TP12 | 选址收益比较 | TS12 | 时空剖面 |
| TP13 | 配送空间图与时序表 | TS13 | 点云剖面与密度 |
| TP14 | 立体高度与地面关系 | TS14 | 冠层高度与参考误差 |
| TP15 | 视域覆盖决策 | TS15 | 分类图与混淆矩阵 |
| TP16 | 土方平衡 | TS16 | 插值残差与不确定性 |
| TP17 | 公共服务分组公平性 | TS17 | 情景集合分布 |
| TP18 | 多准则决策与敏感性 | TS18 | 多尺度采样设计 |

TP17需有合法人口分组与分母，指标定义固定，不能由地图视觉推断社会结论。TS10/11/14/16/17对应误差、区间和集合含义分别说明，不能互换“可信度”。

## 8. 不增加工具数但必须完成的产品能力

能力按任务渐进发现；明确歧义一次集中问；模板搜索与项目风格；真实预览；中文排版；20类常用改稿意图；多媒介重排；批量页一致性；颜色/字号可访问性；印刷ICC与灰度检查；质量预算；取消/恢复；数据依赖失效与强指纹缓存；科学图/视觉图双轨；同源说明与方法引用；可离线帮助；模型费用/数据外发可见。

典型20类改稿：标题、副标题、说明文字、字体方案、字号层级、配色风格、图例位置、图例列数、版面留白、横竖版、输出尺寸、地图框位置、插图布局、表格位置、图表样式、单位显示、来源脚注、分组排序、系列页统一、恢复上版。涉及单位换算、分类/筛选、分析方法的请求必须重新做语义检查；不是一律当纯样式修改。

## 9. 范围变更规则

本文件冻结业务意图和完成门；F03可以合并等价接口，但不能静默取消对应功能。广度48项、额外算法/依赖和在线发布仍须正式审查。若验证证明不可行，给出方法等价替代、支持限制或用户裁定，不把未经验证项藏在开关后宣称完善。

本次未批准任意脚本执行、模型训练平台、多人在线协同编辑、通用商业软件遥控或自动公网发布。扩展边界清楚有助于把本次60场景真正交付，而不是以无边界承诺推迟完成。

## 10. 能力→场景→成果的验收索引

下列子例属于既有场景，不增加60计数。数据/质量类工具可以交结构化报告，不强迫每项制造地图。基础B/P批由第2节业务边界与原逐工具规格承接；F03须将其逐项补入同一机器可读索引。

| 能力ID | 场景及子例 | 模板或非地图成果；方法门 |
|---|---|---|
| X01/X02/X03/X04 | S31聚合/S32变化点/S33预测/S34新兴热点 | TS09/TS12/TS11/TS06；时间、缺测、显著性与回测 |
| X05 | S31的区域轨迹比较子例 | TS09/TS12＋轨迹差异表；对齐/标准化与聚类定义 |
| X06 | S35 | TS09视觉规则＋动画/帧表；固定分类、相机、时间 |
| X07/X08/X09 | S36的场景环境/高程方式/拉伸子例 | TP14；Z基准、单位、真实字段绑定 |
| X10/X11/X12/X13 | S37视域/S38通视/S36剖面/S39土方 | TP15/TP14/TS13/TP16；地形遮挡和已知体积 |
| X14 | S59的三维场景离线交接子例 | 场景包＋TP14预览；断开原路径后重开 |
| X15/X16 | S40的质量读取与范围/类别过滤子例 | TS13＋点云质检/副本清单；点数和分类保持 |
| X17/X18/X19/X20 | S41地面分类/DTM、S42冠层、S43跨期差 | TS07/TS14/TS03；参考高程/配准和误差 |
| X21/X22 | S44质量与云影筛除 | TS08＋质量报告；产品QA与参考掩膜 |
| X23/X24/X25/X26 | S47分割/S45分类/S46精度/S48推理 | TS08/TS15＋模型卡；训练验证隔离和真实权重 |
| X27/X28 | S49时间结构与趋势子例 | TS09/TS12；共同格网、有效样本和趋势条件 |
| X29/X30/X31 | S50采样/S51插值与空间验证 | TS18/TS16；种子、空间分块和误差定义 |
| X32/X33 | S53敏感性/S52误差传播 | TP18/TS10；扰动范围与有依据的误差分布 |
| X34/X35 | S53的多准则比较子例/S54集合统计 | TP18/TS17；权重/约束/概率边界 |
| X36 | S12、S51、S53的方法适用检查子例 | 方法条件报告；未知不自动判适用 |
| X37/X38 | S55的OD子例/S09的最近设施子例 | TP04/TP08；路网方向、未定位和断连 |
| X39/X40/X41 | S56选址/S57车队/S58中断 | TP12/TP13/TP11，S56另覆盖TP17；容量/时窗/服务人口分母 |
| X42 | S11的连续成本面格网可达子例 | TP04的成本面变体＋累积成本栅格；与道路时间严格区分 |
| X43 | S06/S59标准与往返校验子例 | 合规/丢失项报告；schema、CRS、精度、维度 |
| X44 | S02的垂直转换子例，S36消费 | 高程转换副本＋TP14；真实格网、Z单位和已知控制点 |
| X45 | S59二维离线地图交接 | 离线包＋预览；无外部路径仍能用 |
| X46 | S31/S49的科学维度输出子例 | 科学数据立方＋TS09索引图；时间历法/维度/单位往返 |
| X47/X48 | S60报告/PPTX两条交付子例 | 同源报告/PPTX，配套TP10与各域模板；数字一致/真实渲染/可编辑性 |

各行的自动设计复用已验模板规则，方法测试、设计测试、文件测试三者缺一不算完整场景验收。F03索引字段至少包括tool/operation ID、scenario ID、template ID、数值参考、输入hash、宿主许可、证据路径和独立裁定。
