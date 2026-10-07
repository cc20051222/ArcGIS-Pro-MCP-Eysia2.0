# 工具目录（TOOL_CATALOG）

> Phase 4/5 工具清单。历史 Phase 4 基础目录仍保留；当前实现与真实 Runtime 数量必须以最新 Transition/Verification 记录为准。
> 目标 ≥112（分批扩充），当前首批（阶段目标基础工具集）可见下。

## 计数（诚实）
- **Phase 4 历史已注册工具：25**（基础 6 + Phase 4 新增 19）。
- **当前静态注册工具：30；当前真实 Runtime 工具：30**（在上述 25 个基础/GP 工具上增加 Python Bridge 2 个与 Python discovery 3 个）。
- **已集成测试（Fake Host）：Phase 4 新增 12 项用例覆盖 13 个新工具**（map/layer/project/attribute/selection + GP 路由 + metadata）。
- **真实 ArcGIS Pro 3.5：核心工具已验证部分**（见下）；其余 NOT VERIFIED。

| Name | Category | Type | Status | Unit | Integration | Real Pro |
|------|----------|------|--------|------|-------------|----------|
| ping | System | Native | Implemented | PASS | PASS | PASS |
| get_current_map | Map | Native | Implemented | PASS | PASS | PASS |
| get_layers | Layer | Native | Implemented | PASS | PASS | PASS |
| get_project_info | Project | Native | Implemented | PASS | PASS | PASS |
| get_arcgis_version | System | Native | Implemented | PASS | PASS | PASS |
| get_license_info | System | Native | Implemented | PASS | PASS | PASS |
| list_maps | Map | Native | Implemented | PASS | PASS | PARTIAL（GetItems 路径) |
| get_map_info | Map | Native | Implemented | PASS | PASS | PARTIAL |
| get_layer_info | Layer | Native | Implemented | PASS | PASS | PASS |
| set_layer_visibility | Layer | Native | Implemented | PASS | PASS | PASS |
| add_layer | Layer | Native | Implemented | PASS | PASS | NOT VERIFIED |
| remove_layer | Layer | Native | Implemented | PASS | PASS | NOT VERIFIED |
| list_layouts | Layout | Native | Implemented | PASS | PASS | PASS（空→[]）|
| list_databases | Project | Native | Implemented | PASS | PASS | PASS |
| query_attributes | Attribute | Native | Implemented | PASS | PASS | PASS（无 FeatureLayer→LAYER_NOT_FOUND）|
| get_field_info | Attribute | Native | Implemented | PASS | PASS | PASS |
| get_feature_count | Attribute | Native | Implemented | PASS | PASS | PASS |
| clear_selection | Selection | Native | Implemented | PASS | PASS | PASS |
| select_layer | Selection | Native | Implemented | PASS | PASS | NOT VERIFIED |
| buffer | Analysis | GP | Implemented | PASS | PASS(路由) | NOT VERIFIED |
| clip | Analysis | GP | Implemented | PASS | PASS(路由) | NOT VERIFIED |
| intersect | Analysis | GP | Implemented | PASS | PASS(路由) | NOT VERIFIED |
| dissolve | Analysis | GP | Implemented | PASS | PASS(路由) | NOT VERIFIED |
| get_dataset_info | DataManagement | Native | Implemented | PASS | PASS | NOT VERIFIED |
| get_raster_info | Raster | Native | Implemented | PASS | PASS | NOT VERIFIED |
| python_bridge_ping | Python | Python | Implemented | PASS | PASS | PASS |
| python_runtime_info | Python | Python | Implemented | PASS | PASS | PASS |
| dataset_summary | Python | Python | Implemented | PASS | PASS | PASS |
| list_fields | Python | Python | Implemented | PASS | PASS | PASS |
| list_workspace_datasets | Python | Python | Implemented | PASS | PASS | PASS |

> Real Pro（2026-09-02 最终 PASS，干净重启 Pro + MCP HTTP 6520，25 工具最终包验证）：基础/Layer/GP/Error/FeatureLayer属性/Selection 全 PASS；get_feature_count=4、get_field_info、query_attributes（带与不带 fieldNames 均返回真实数据，无 Internal error）、select_layer/clear_selection 恢复。2 处 bug（LAYER_NOT_FOUND 回退、query_attributes 几何序列化）已修复并部署。get_dataset_info/get_raster_info = filesystem-only 占位（FileGDB FC → DATASET_NOT_FOUND，real 解析 NOT VERIFIED）。

## Planned（后续子阶段，勿标 Implemented）
- Data Management：copy_features / create_feature_class / create_file_geodatabase / delete_dataset / rename_dataset …
- Raster：raster_info / raster_clip / raster_project / raster_resample / raster_calculator / raster_to_polygon …
- Layout：create_layout / add_map_frame / add_text / export_layout / export_map …
- Attribute 扩展 / Selection 扩展（select_by_attribute / select_by_location / get_selected_features）。
> Real Pro（2026-09-03 Phase 5.6.4）：修改包已加载；`tools/list=30`；`python_bridge_ping`、`python_runtime_info`、`dataset_summary`、`list_fields`、`list_workspace_datasets` 真实 MCP/ArcPy 验证 PASS。静态/运行时分类为 Native 21、GP 4、Python 5；当前未新增 arbitrary Python 或测试 action 到生产工具目录。
