# ArcGIS Pro MCP 2.0 · 工具扩展计划（外部顾问简报 v1）

> **本文档用途**：交由外部 AI 顾问（ChatGPT 等）做进一步方案制定。文档自包含——读完即可理解项目全貌、现有能力、扩展候选与工程约束，无需访问代码仓库。
> **你需要产出什么**：见 §8「任务书」。请全文读完再回答。

---

## 1. 项目是什么

**ArcGIS Pro MCP 2.0** 是一个把 **Esri ArcGIS Pro**（桌面 GIS 软件）暴露为 **MCP（Model Context Protocol）服务器**的插件系统。任何支持 MCP 的 AI 客户端（Claude、Cursor、自研 Agent 等）可以通过标准协议调用本服务器提供的 GIS 工具，实现对地图、图层、数据、分析、制图的程序化操控。

一句话：**让 AI 助手真正"会用"ArcGIS Pro**——查询数据、跑空间分析、编辑要素、批量出图，全部通过受控、可审计的工具调用完成。

## 2. 技术栈与架构

- **语言/运行时**：C# / .NET（插件 payload 同时面向 net6 与 net8 双代分发）
- **宿主**：ArcGIS Pro 插件（.esriAddInX），进程内嵌 MCP 服务器
- **传输**：MCP over HTTP，默认仅本机回环 `127.0.0.1:6520/mcp`；辅助 Python Bridge `6511`（仅本机，无公网暴露）
- **测试**：xUnit，当前 **1565 项 = 1562 通过 / 0 失败 / 3 跳过**（截至 2026-09-24 回归轮）

分层架构（自上而下，不可跳层）：

```
AI Client → MCP Transport → MCP Protocol → MCP Server → Tool Router
          → Tool Registry → GIS Tool → IArcGISHost → ArcGIS Services → ArcGIS Pro SDK
```

## 3. 硬性工程规则（不可协商，方案必须兼容）

| # | 规则 | 含义 |
|---|------|------|
| R1 | **MCT 线程规则** | 一切 ArcGIS Pro SDK 对象访问必须经 `QueuedTask.Run` 调度，禁止 HTTP/后台线程直连 SDK |
| R2 | **Shared 隔离** | 公共库不得引用 ArcGIS Pro SDK；SDK 只允许出现在宿主插件项目 |
| R3 | **唯一注册中心** | 所有工具只在 `Composition.BuildRegistry()` 注册，禁止第二处注册点 |
| R4 | **配置集中** | 端口/超时/路径/开关统一在 Configuration 模块 |
| R5 | **仅本机回环** | 禁止 `0.0.0.0`、禁止公网暴露、禁止新增任意脚本旁路 |
| R6 | **只读模式** | 全局只读开关生效时，一切写类工具必须拒绝执行 |
| R7 | **审计** | 关键操作落审计日志，可查询回放 |
| R8 | **稳定契约** | 工具参数面/响应结构/错误码变更需评审，禁止静默破坏既有调用方 |

## 4. 现状基线（2026-09-26 现场核验）

| 指标 | 数值 | 备注 |
|------|------|------|
| 注册工具数 | **154** | 从注册中心源码逐项计数核验 |
| 单元/集成测试 | 1565（1562/0/3） | 连续多轮零产品性失败 |
| 错误码 | **33 个，恒定不增** | 扩容需单独评审 |
| GP 白名单 | 53 条 | `run_geoprocessing` 允许调用的系统 GP 工具清单 |
| 一键安装包 | r20 | 双代分发（net6 payload + net8 插件） |
| 特性基线 | 受控 GP、元工具、审计、编辑栈、统计、快照/恢复、只读模式、批量执行、图像回传、数据文件夹工作流（scan→load→plan→job）、作业并发安全（OwnerToken/原子写/断点续跑） | 近期批次刚完成 |

**类别分布（合计 154）**：系统与会话治理 10 ｜ 项目/工作空间/环境 12 ｜ 地图与视图 17 ｜ 图层管理 17 ｜ 选择与快照 7 ｜ 数据发现与 Schema 11 ｜ 属性查询 6 ｜ 空间分析（原生封装）8 ｜ GP 通道 5 ｜ 栅格处理 7 ｜ 数据集管理 7 ｜ 要素与字段编辑 15 ｜ 统计分析 2 ｜ 布局与出图 15 ｜ 符号化与标注 8 ｜ 批处理与作业流 6 ｜ 几何修复 1

## 5. 现有 154 工具全名单

> 命名以注册中心类名为准（运行名取 camelCase 形态）。这是**完整清单**，请据此做差距分析，不要假设缺什么——先对照再建议。

**① 系统与会话治理（10）**：Ping, GetArcGISVersion, GetLicenseInfo, PythonBridgePing, PythonRuntimeInfo, Diagnose, GetAuditLog, SetReadOnlyMode, SnapshotProject, RestoreSnapshot

**② 项目/工作空间/环境（12）**：GetProjectInfo, SaveProject, ListDatabases, SetWorkspace, GetWorkspace, AddFolderConnection, GetProjectItems, ListFolder, SearchData, CreateFileGdb, GetEnvironment, SetEnvironment

**③ 地图与视图（17）**：ListMaps, GetCurrentMap, GetMapInfo, CreateMap, RemoveMap, ActivateMap, SetMapProperties, SetBasemap, GetMapView, SetMapView, ExportMapView, GetMapExtent, SetMapExtent, ListBookmarks, CreateBookmark, ApplyBookmark, DeleteBookmark

**④ 图层管理（17）**：GetLayers, GetLayerInfo, SetLayerVisibility, AddLayer, RemoveLayer, MoveLayer, RenameLayer, DuplicateLayer, CreateGroupLayer, GetBrokenLayers, RepairLayerSource, AddJoin, RemoveJoin, SetLayerTransparency, SetLayerScaleRange, GetDefinitionQuery, SetDefinitionQuery

**⑤ 选择与快照（7）**：ClearSelection, SelectLayer, SelectByAttribute, SelectByLocation, GetSelectedFeatures, CreateSelectionSnapshot, RestoreSelectionSnapshot

**⑥ 数据发现与 Schema（11）**：GetDatasetInfo, GetRasterInfo, DatasetSummary, ListFields, ListWorkspaceDatasets, GetSchemaInfo, GetDomains, GetSubtypes, GetIndexes, ListRasters, ListTables

**⑦ 属性查询（6）**：QueryAttributes, GetFieldInfo, GetFeatureCount, GetLayerFeatures, GetFieldValues, GetUniqueValues

**⑧ 空间分析·原生封装（8）**：Buffer, Clip, Intersect, Dissolve, SpatialJoin, Near, Erase, Union

**⑨ GP 通道（5）**：RunGeoprocessing, ListGeoprocessingTools, DescribeGeoprocessingTool, GetMessages, CheckExtension

**⑩ 栅格处理（7）**：RasterClip, RasterResample, RasterStatistics, RasterMosaic, RasterCalc, CellStatistics, FocalStatistics

**⑪ 数据集管理（7）**：CopyDataset, DeleteDataset, RenameDataset, Merge, Project, ExportFeatures, ExportTable

**⑫ 要素与字段编辑（15）**：InsertFeatures, UpdateFeatures, DeleteFeatures, SaveEdits, DiscardEdits, GetEditSession, AppendFeatures, CreateFeatureClass, CreateTable, TruncateTable, AddField, AddFields, DeleteField, AlterField, CalculateField

**⑬ 统计分析（2）**：GetFieldStatistics, SummarizeFeatures

**⑭ 布局与出图（15）**：ListLayouts, GetLayoutInfo, ListLayoutElements, CreateLayout, AddLayoutText, AddLegend, AddNorthArrow, AddScaleBar, ExportLayoutPdf, ExportLayoutPng, ExportLayoutJpg, ExportLayoutTif, ExportLayoutSvg, ExportLayoutEps, ExportMapSeries

**⑮ 符号化与标注（8）**：GetLayerSymbology, SetSimpleSymbology, SetLayerRenderer, ListColorRamps, ApplySymbologyFromLayer, SaveLayerFile, GetLabelInfo, SetLabelVisibility

**⑯ 批处理与作业流（6）**：RunBatch, ScanDataFolder, LoadFolderData, ApplyProcessingPlan, GetJobStatus, GetJobReport

**⑰ 几何修复（1）**：RepairGeometry

## 6. 扩展候选清单（28 项，154 → ≈182）

> 分六个方向。读写分类中「写」= 修改数据/地图状态/产出文件，受 R6 只读模式与 R7 审计约束。优先级：P1 = 下批必做；P2 = 次批；P3 = 远期/需评审。

### A · 工作流控制面（+5）——补齐作业系统最后缺口

| 工具 | 读写 | 说明 | 优先级 |
|------|------|------|--------|
| cancel_job | 写 | 取消运行中/排队作业，状态机新增 cancelled 态；与现有 OwnerToken 并发模型兼容 | P1 |
| list_jobs | 只读 | 历史作业清单概览（状态/起止/计数） | P1 |
| validate_plan | 只读 | 作业计划 dry-run 预检：schema、路径、字段存在性，不落盘 | P1 |
| get_job_report 增强（manifest） | 响应字段 | 产物清单附 SHA256，不改参数面 | P1 |
| retry_job | 写 | 对失败项重试（续跑已有，补"仅失败项"语义） | P2 |

### B · 智能协同元工具（+3）——MCP 产品差异化

| 工具 | 读写 | 说明 | 优先级 |
|------|------|------|--------|
| describe_tool_catalog | 只读 | 154 工具分类索引 + 关键词检索 + 读写标记，解决 AI 客户端"选工具难" | P1 |
| suggest_workflow | 只读 | 场景模板库（缓冲分析/批量出图/数据入库等）→ 生成 pipeline JSON | P2 |
| explain_operations | 只读 | 审计日志摘要回放（自然语言可读的操作历史） | P2 |

### C · 几何与数据质检（+8）——高频实战缺口

| 工具 | 读写 | 说明 | 优先级 |
|------|------|------|--------|
| get_geometry_info | 只读 | 在线计算面积/长度/周长/质心/包络，不落盘 | P2 |
| add_geometry_attributes | 写 | 几何属性批量写入字段 | P2 |
| find_identical | 只读 | 重复要素/重复属性检测 | P2 |
| delete_identical | 写 | 删除重复要素（高风险，强制先建快照） | P3 |
| simplify_features | 写 | 几何简化/综合 | P2 |
| define_projection | 写 | 定义坐标系（仅元数据，不做变换） | P2 |
| multipart_to_singlepart | 写 | 多部件拆分 | P3 |
| generate_quality_report | 写(产出文件) | 一键数据质量报告：schema 异常/空几何/重复/字段统计汇总 → markdown/json | P2 |

### D · 数据治理写入面（+6）——补齐只读侧的写入半边

| 工具 | 读写 | 说明 | 优先级 |
|------|------|------|--------|
| create_domain / delete_domain | 写 | 值域（coded/range）管理 | P3 |
| assign_domain_to_field | 写 | 值域赋给字段 | P3 |
| create_subtype | 写 | 子类型定义 | P3 |
| create_relationship_class | 写 | 关系类创建 | P3 |
| set_field_properties | 写 | 字段别名/默认值/必填设置 | P3 |

### E · 制图自动化深化（+4）——模板化批量出图

| 工具 | 读写 | 说明 | 优先级 |
|------|------|------|--------|
| set_layout_element_properties | 写 | 修改布局元素文本/位置/大小——模板出图的核心缺口 | P2 |
| create_map_series | 写 | 定义地图序列（index layer 驱动） | P2 |
| batch_layer_visibility | 写 | 按名称模式批量开关图层 | P2 |
| apply_color_scheme | 写 | 批量应用配色方案 | P3 |

### F · 运维与可观测（+2）

| 工具 | 读写 | 说明 | 优先级 |
|------|------|------|--------|
| get_performance_stats | 只读 | 每工具调用计数/耗时统计 | P3 |
| clear_result_cache | 写 | 结果缓存管理（若引入缓存层） | P3 |

## 7. 质量与交付约束（方案必须写明如何满足）

1. **测试**：每新增工具至少 1 组单测；工具计数断言需同步更新；全量回归保持"零产品性失败"。
2. **错误码**：优先映射现有 33 码；确需新增（如 cancelled、license 缺失）须单独列申请表，说明为何不可复用。
3. **白名单**：凡经 `run_geoprocessing` 通道的新 GP 能力，白名单加条需走变更校验（快照 diff + 包内语料计数双锚）。
4. **命名**：工具名 camelCase；参数面与现有工具风格一致；响应含结构化状态字段。
5. **写类工具**：必须声明与只读模式/审计/快照机制的交互方式。
6. **兼容性**：不破坏现有 154 工具的参数面与响应结构；纯新增优先，改契约需评审记录。

## 8. 任务书——请你（ChatGPT）完成

1. **评审候选清单（§6）**：对 28 项逐一给出「采纳 / 合并 / 删除 / 改造」意见与理由；特别审查 ArcGIS Pro SDK API 可行性（如异步 GP 作业取消语义、SchemaEditor 域/子类型能力、布局元素属性可写范围）。**不确定的 API 请标注 `[VERIFY]`，不要虚构。**
2. **盲区挑战**：对照 §5 全名单，指出我们遗漏的扩展方向（至多 5 个，按价值排序）。
3. **逐工具规格**：对最终采纳的每个工具给出——工具名、一句话描述、读写分类、参数草案（JSON Schema 风格）、成功/失败响应结构、错误码映射（33 码复用或申请扩容）、测试矩阵要点。
4. **实施批次设计**：编排 3~4 个批次（每批 ≤8 工具），说明批次内回归策略与验收判据。
5. **裁定项清单**：汇总所有需要项目所有者拍板的事项（错误码扩容、白名单加条、契约变更、高风险写操作策略）。

## 9. 输出格式建议

- 用中文回复；工具名/参数/代码保持英文。
- 按「①评审表 → ②盲区 → ③工具规格（逐个） → ④批次计划 → ⑤裁定项」五段输出。
- 工具规格用统一模板，便于后续直接转成开发派工单。

## 10. 禁止与标注要求

- 禁止虚构 ArcGIS Pro SDK API 名称与签名；不确定即标 `[VERIFY]`。
- 禁止建议违反 §3 任何硬规则的方案（如绕过 QueuedTask、新增公网端口、第二注册点）。
- 若某候选在 SDK 层不可行，给出最接近的替代路径或明确降级结论。

---

*文档版本 v1 · 2026-09-26 · 依据当日注册中心现场核验编制*
