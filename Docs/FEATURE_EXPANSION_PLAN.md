# 功能完善总体规划（Feature Expansion Plan）— 目标：开源 ArcGIS Pro MCP 功能最完善

- **制定**：Keeper（**G-161**，2026-09-19 20:2x）｜**依据**：用户指令「目前 gispo 暴露的工具有多少，我们最多可以接入多少，现在暂停适配优化，全力进行功能完善。需要最大程度完善功能，达到现在开源项目里功能最完善的项目」
- **定位**：规划产物（方案 = Keeper；执行 = 执行者；验收 = Keeper）｜生态对比 = WebSearch 实测（2026-09-19）
- **治理动作**：**D-060 A 项（Pro 版本兼容适配）→ PAUSED 转挂账**；B 项（聚合类）继续；**功能完善分批派单（D-061 起）**

---

## 一、用户三问 · 结论先行

### 1）「gispo 暴露的工具有多少？」

**未检索到名为 "gispo" 的开源项目**（WebSearch 实测 2026-09-19）。按最接近理解（GIS Pro / ArcGIS Pro MCP 开源生态），**头部项目实测工具数**：

| 项目 | 架构 | 工具数 | 特点 |
|---|---|---|---|
| **★ Knight60/ArcGIS-Pro-MCP（用户指定对标，09-19 实测）** | **C# AddIn 进程内 MCP**（`127.0.0.1:6520/mcp` HTTP） | **112**（三层：112 命名 + `run_geoprocessing_tool` 任意 GP ~2,000 + `execute_arcpy_code` 任意 Python） | **与我们同源同架构**（AddIn + 6520 端口 + 大量同名工具）；QueuedTask 主线程泵 ≈9ms/调用；要求 **Pro 3.7**（旧版不加载）；**v1.1 起双许可 AGPL-3.0/Commercial**（v1.0 及以前 MIT）；「回图」能力（export_map_view/preview_layout/export_layout 直接返回图像给 AI 看）；09-06 仍在活跃整理 |
| **Geo2004/MCP-ArcGISPro** | 纯 Python + 文件 IPC（arcpy bridge） | **30** | 覆盖面较广：含 `run_geoprocessing` 与 `execute_python` 两个「万能入口」 |
| **SojiroPopo/arcgis-mcp** | Python/FastMCP + arcpy | **30** | Data I/O 6 + Vector GP 10 + 地形 9 + 栅格 5 |
| qiobn/arcpro-mcp | Python bridge（socket） | ~10（v0.1 只读） | 审计日志/双门 `execute_python` |
| nicogis/MCP-Server-ArcGIS-Pro-AddIn | C# AddIn + Named Pipes | ~6 类 | AddIn + .NET，功能面小 |
| jjsantos01/qgis_mcp（参照系） | QGIS 插件 + socket | ~10（Processing 万能入口 = 数百算法） | 生态最热（~623★） |

**★ Knight60 精读要点（对超越策略有决定性影响）**：
1. **它就是我们旧仓库的上游形态**（同 AddIn + 6520 + 同名工具族）—— 它走到了 **112 命名工具 + 两层万能入口**；我们 fork 后在**工程纪律**（单测/守卫/LIVE/审计/冻结）上远超它，但在**工具数量与个别能力域**上落后（78 vs 112）。
2. **它的领先域（我方 gap，~34 项）**：编辑栈（`EditOperation` 行级 insert/update/delete + save/discard_edits）、字段统计/分组聚合（get_field_statistics/summarize_features）、图层管理增强（transparency/scale_range/group_layer/basemap/broken_layers/repair_layer_source/join）、渲染进阶（renderer 三模式/lyrx/label 表达式）、视图与书签（get/set_map_view/**回图**/bookmarks×4）、schema 创建（create_feature_class/create_table/add_fields/delete_field/truncate_table/export_features）、project 增强（environments get/set/remove_map/activate_map/set_map_properties）、GP 元工具（list/describe/check_extension/get_messages）、数据发现（search_data/list_folder/folder_connection）、**回图**（export_map_view/preview_layout/export_layout 返回图像）、run_batch、map series 导出。
3. **我方领先域（它没有）**：选择快照（create/restore_selection_snapshot）、`dataset_summary`、`copy_dataset`、`rename_dataset`（数据集级）、`append_features`、栅格三件（clip/mosaic/resample）、`export_table`、字段三件套（add/alter/calculate 之外还有 get_field_info）、**全部工具的单测+守卫+LIVE+审计工程纪律**、Pro 3.5 可用（它 3.7 only）。
4. **风险提示（Keeper→用户，非阻断）**：其 v1.1 起 **AGPL-3.0/商业双许可**（v1.0 前为 MIT 不可撤回）。我方与其同源，**分发/开源前建议确认我方派生基线版本与许可义务**（法务层面，Keeper 不作判断）。
5. **超越定义（用户指令「我们要超越他」量化）**：**数量 ≥125（>112）＋ 质量（每工具单测/守卫/LIVE/审计，它 README 无测试声明）＋ 安全（受控 GP 白名单 vs 它的无界任意 GP/Python）＋ 体验（回图/run_batch 对齐）＋ 兼容面（3.5–3.6+ vs 它 3.7-only）**。

> 若你说的 "gispo" 是另一具体项目，给我仓库地址即可精确对比。

### 2）「我们最多可以接入多少？」

- **架构层：无硬上限**。MCP 协议不限制工具数；我们的注册表/快照/守卫/测试基建按「每工具一个条目」线性扩展。
- **能力面锚点**：两条腿 ——
  1. **逐工具精细化**（现状路线）：每工具 = 实现 + schema + 单测 + LIVE 判据，成本最高、质量最高；
  2. **受控 GP 通用调用**（新增，见 §三）：一个白名单工具 ⇒ 能力面直接覆盖 **arcpy 全量 GP（Pro 3.x 约 1,000+ 工具）**；
  - 天花板 = **SDK 原生 API 面**（我们已持有的 13.0/13.5 ref 程序集含数千公开类型）—— 工程上不按 API 数接入，按「高价值能力」接入。
- **务实规划目标（G-162 按 Knight60 上调；G-163 工单化定数；G-166 差异化增补定稿）**：**78 → 93（D-061）→ 107（D-062，G-171 件数裁定 = 14 枚举【Keeper 算术自纠】）→ 127（D-063，+2 候选）→ 145（D-064，+信任层/安全网四件；＞ Knight60 的 112）**，后续上不封顶；达成后**工具数、覆盖域、工程质量（每工具单测+LIVE+守卫+审计）、安全（受控 GP 白名单 vs 无界任意执行）、兼容面（3.5–3.6+ vs 它 3.7-only）五维全面超越**。
- **★ 独特产品定位（G-166，`PRODUCT_DIFFERENTIATION_PLAN.md`）**：**「可信赖的 AI GIS 操作员平台」四支柱 = 信任层（diagnose/get_audit_log/set_readonly_mode——竞品零覆盖）＋ 安全网（snapshot/restore_project——竞品零覆盖）＋ 洞察闭环（回图/统计/map_series）＋ 兼容×质量（3.5 可用 + 每工具五件套）**；差异化增补全部并入既有批次（D-062 +1 / D-064 +4），**不新开单、不破坏串行**。
- **★ 链式派发机制（G-163）**：四批工单+转交件**全部备妥**、状态串行 QUEUED（D-060 → D-061 → D-062 → D-063 → D-064），**每单收官即转发下一单转交件，零等待**；D-064 收官 ⇒ 数量维度正式超越 ＋ **D-065 适配恢复评估**（A 项挂账解封——按用户指令：功能完善收官后才做适配）。

### 3）「目前我们有什么？」

**现役 78 工具（Keeper 实测分组）**：get_* 22｜add_* 6｜list_* 6｜raster_* 5｜set_* 5｜create_* 3｜export_* 3｜select_* 3｜python_* 2｜其余（buffer/clip/erase/dissolve/merge/union/intersect/near/spatial_join/project/copy/delete/rename/append/alter/calculate/add_field/query/clear/move/remove/ping/dataset_summary…）
**我方独有卖点（竞品均无）**：布局元素族（legend/north_arrow/scale_bar/layout_text）、`set_simple_symbology`/`set_label_visibility`/`set_definition_query`、选择快照（create/restore）、`dataset_summary`、数据管理族（copy/delete/rename/append + 字段三件套）——**每工具都有单测+守卫+LIVE+审计**（竞品脚本级项目普遍没有）。

---

## 二、Gap 清单（竞品有 → 我们补齐；D-061 覆盖）

| # | 竞品工具 | 我们现状 | 处置 |
|---|---|---|---|
| 1 | `save_project` | **缺** | D-061 新增 |
| 2 | `create_map` | **缺**（有 get_current_map/list_maps） | D-061 新增 |
| 3 | `set_workspace` / `get_workspace` | 仅有 list_workspace_datasets | D-061 新增（工作区上下文） |
| 4 | `list_rasters` / `list_tables` | 并入 list_workspace_datasets 语义 | D-061 拆独立工具 |
| 5 | `get_unique_values` | **缺** | D-061 新增 |
| 6 | `get_layer_features`（行预览） | query_attributes 可能部分覆盖 | D-061 新增（分页/limit；若重叠则在工单内并入并披露） |
| 7 | `export_layout` 多格式 | 仅 pdf/png | D-061 新增 **jpg/tif/svg/eps**（复用导出基类守卫） |
| 8 | `repair_geometry` | **缺** | D-061 新增（就地破坏性 ⇒ 输入守卫 + confirm，沿 D-053 模式） |
| 9 | `run_geoprocessing`（万能 GP） | **刻意无**（安全哲学） | **D-062 旗舰：受控白名单版**（见 §三） |
| 10 | `execute_python`（任意代码） | 无（有 python_bridge 管理面） | **维持不暴露**（安全红线，见 §三） |

## 三、战略决策（Keeper 裁定）

1. **受控 GP 通用调用（D-062 旗舰）**：`run_geoprocessing` = 工具名（点串）+ 参数字典；**白名单**（`Config/gp-whitelist.json` 起步 ~50 高频工具）+ **破坏性类 confirm** + **输出/输入守卫** + **审计日志** + 参数序 A/B 实测。⇒ 白名单可随时扩充，**能力面 = arcpy 全量 GP（~1,000+）的受控子集**，一步奠定「功能最完善」。
2. **`execute_python`（任意代码）维持不暴露**：安全红线不变（任意代码执行 = 不可审计、可绕过全部守卫）；竞品以此换覆盖面，我们以受控 GP 白名单换同等能力面。
3. **A 项暂停（用户指令）**：D-060 **A 项（Pro 3.0–3.5 兼容适配）→ PAUSED 转挂账**（方案书 v3/引用集资产/差集方案全部保留，随时可恢复）；**B 项（聚合类 78→80）继续执行**；C/D 项不变。
4. **冻结期例外延续**：新增工具 = 经用户批准的契约面变更（本规划即批准依据）；错误码 33 零新增红线不变。

## 四、批次规划

| 批次 | 内容 | 工具数 | 状态 |
|---|---|---|---|
| **D-060**（在途） | B 聚合类（CellStats/FocalStats）；C 封存落定；A 项 **PAUSED** | 78→80 | **ACTIVE** |
| **D-061**（本规划第一批） | §二 #1–#8：save_project、create_map、set/get_workspace、list_rasters、list_tables、get_unique_values、get_layer_features、export_layout ×4 新格式、repair_geometry —— **13 工具** | **80 → 93** | 工单已备（**QUEUED**，D-060 收官即派） |
| **D-062**（旗舰，按 Knight60 扩充＋G-166 差异化增补） | **受控 GP 通用调用 + GP 元工具**（run 白名单版 / `list_geoprocessing_tools` / `describe_geoprocessing_tool` / `get_messages` / `check_extension` —— 对齐其 Layer2 但**白名单+confirm+审计**）＋ **编辑栈**（insert/update/delete_features + save/discard_edits + `get_edit_session`，EditOperation 撤销栈）＋ **字段统计/分组聚合**（get_field_statistics / summarize_features）＋ **★ `get_audit_log`**（审计查询——支柱一信任层）—— **12 件** | 93 → **105** | **QUEUED（工单+转交件已备，含 G-166 增补段）** |
| **D-063** | **图层管理增强**（transparency/scale_range/group_layer/basemap/broken_layers/repair_layer_source/join·remove_join/rename_layer/duplicate_layer）＋ **渲染进阶**（renderer 三模式/`get_layer_symbology`/color_ramps/lyrx 应用与保存）＋ **视图与书签**（get/set_map_view/**export_map_view 回图**/bookmarks×3）—— **17 件** | 104 → **121** | **QUEUED（工单+转交件已备）** |
| **D-064** | **schema 创建**（create_feature_class/create_table/add_fields/delete_field/truncate_table/export_features）＋ **project 增强**（remove_map/activate_map/set_map_properties/get·set_environment）＋ **数据发现**（search_data/list_folder/add_folder_connection/get_project_items）＋ **run_batch** ＋ `export_map_series` ＋ **★ G-166 信任层/安全网四件**（`diagnose` 自诊断 / `snapshot_project` / `restore_snapshot` / `set_readonly_mode`——均竞品零覆盖）—— **21 件** | 121 → **142（＞ Knight60 112 ＋ 四支柱独占，数量与差异化双超越）** | **QUEUED（工单+转交件已备，含 G-166 增补段）** |
| 后续（D-065 起） | **适配恢复评估**（Pro 3.0–3.5 兼容，A 项挂账解封——用户指令：功能完善收官后才做适配）；场景/3D、Network Analyst（许可评估）…… | 上不封顶 | 待 D-064 收官 |

## 五、纪律（每批通用）

两阶段｜回执｜roundstart+锁统计前置｜**错误码 33 零新增**｜每工具单测 ≥8 + LIVE 代表判据｜守卫全接入（输出型 Match(output)/就地输入型）｜反证落盘｜快照/registry/names/冻结文档同步＋显著披露｜artifact 语义注释｜G-138 全 D 盘｜G-140 环境适配｜§W2 判定器铁律。

## 六、验收（Keeper）

每批 = 「计数三向（registry/快照/tools/list）+ 每工具新单测族全绿 + LIVE 代表判据 + 守卫/OUTPUT_EXISTS 回归不破 + 表述审计（冻结文档/教程同步）」；收官 ⇒ 基线推进 ＋ 冻结文档版本化增补。

> **G-171 件数裁定补记（09-20 19:3x）**：D-062 工单文本枚举恰 14 个工具名（无斜杠合写）——执行侧按枚举实现 107 正确；Keeper「11→12 ⇒ 105」系斜杠算术错误（自纠入账）。链号 +2 方向采纳；D-063 派单时以枚举重算精确件数（127 为候选值）。
