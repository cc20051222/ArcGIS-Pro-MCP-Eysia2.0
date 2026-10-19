# 矢量、字段治理与受控 GP

## 选择真实可达的算法

缓冲/裁剪/融合/叠加对应 `buffer`、`clip`、`dissolve`、`intersect`、`union`、`erase`；空间连接用 `spatial_join`，最近距离用 `near`。先检查几何类型、选择范围、CRS、距离单位、输出类型与许可。`near` 和字段计算等会就地改输入，不把“分析”理解成只读。

当前 `buffer` 的输入名为 `input`、`output`、`distance`，可选 `distanceUnit`、`dissolve`、`overwrite`，**没有 method 参数**。若需要指定测地线算法，应先判断受控 `analysis.Buffer` 是否实际可达、白名单是否包含所需参数，再准备命名参数；不能给封装工具添加猜测参数。以下仅为 schema 例，未授权时不得执行：

```json
{"input":"D:/GIS/input.gdb/roads","output":"D:/GIS/owned-run/result.gdb/roads_buffer_500m","distance":500,"distanceUnit":"Meters","dissolve":false,"overwrite":false}
```

经纬度数据不能把 500 当成 500 度或盲目平面度量。依据目标精度选择合适投影/算法；`project` 变换坐标，`define_projection` 仅改 CRS 声明且可能就地修改，只有真实源 CRS 已有可靠依据且获授权才用。转换参数、方法和 GIS 环境不足时提问，不暗中改变研究方法。

## 受控通用 GP

1. `list_geoprocessing_tools` 查当前白名单；`describe_geoprocessing_tool` 读具体条目的命名参数、次序和 destructive 标记。
2. 参数先按真实 schema 和条目元数据准备；`parameters` 与 `positionalValues` 二选一，优先命名参数。多值/ValueTable/含空格路径按当前工具支持格式，不把未转义路径随手拼成字符串。
3. 明确输入、输出、方法、许可、环境、是否就地修改；输出和 `auditDir` 都指向已获准的 D 盘目录。默认程序集旁审计目录不一定满足 D 盘政策，不能忽略。
4. destructive 条目只有明确授权时传 `confirm=true`。`run_geoprocessing` 当前无覆盖语义；输出存在则另取获准新名称，不能靠 `overwrite=true`、改全局环境或删旧结果绕过。
5. 核业务结果、GP messages 与审计位置。`auditEntryIndex=0` 表示审计未落盘，不能把响应成功写成审计完整。白名单有条目仍不证明 alias、许可或实际调用成功；失败保持原判据。

白名单外不走 ArcPy、任意 Python、6511、PowerShell 执行 GIS 或另装 CLI 来绕过。行业社区的“任意 GP 可达”不能复制为本产品授权。

## 环境与编辑

先读 `get_environment`，只设置本次需要且 schema 支持的范围/CRS/工作空间/scratch；记录原值和修改范围。环境影响哪些工具以各工具真实支持为准。恢复仅限本任务实际改变的原值，且先核没有用户/并行会话后续改动；无法判定不盲目写回旧环境。进程内默认值不是科学方法的替代。

字段、域/子类型、关系类操作先读现状及依赖；按用户目标选择 `copy_dataset`、`export_features`、`merge`、`append_features`、`add_fields`、`assign_domain_to_field` 等。就地编辑前读 `get_edit_session`，存在用户未决编辑时不得自动保存或丢弃。`snapshot_project` 不等于 GDB 全备份或事务回滚；保留原件、副本与输出身份。

完成检查：输出可读、类型/CRS/数量/字段符合计划，面积/距离单位可核，空间关系符合预期，失败与未处理对象保留。方法不满足时停止并解释，不用看起来相似的分析替代。

## 导出与工作区转换

复制不必然支持格式转换。2026-10-18 现场 copy_dataset 底层 Copy_management 将 GDB 要素复制成 Shapefile 时返回 ERROR 000979，未创建目标；核不存在后改用 export_features 才成功。按真实数据类型和目标格式选择工具，不因输出扩展名就假定会转换。导出全量时显式 useSelection=false，并核 exportedCount、selectionApplied、定义查询与实际成果；响应 note 与结构字段矛盾时记录差异，不把警告文字当成存在选择的证据。

Shapefile 导出会把真曲线离散为顶点，不能假定与 GDB 曲线面积逐位相等；比较格式损失和可解释容差，必要时保留 GDB 为分析权威件。完整侧车与实际打开另行检查。导出与 GP 可能自动把结果加入活动地图，下一次制图前核图层全集及顺序。

格式选择依据：[Esri Copy 的工作区限制](https://pro.arcgis.com/en/pro-app/3.4/tool-reference/data-management/copy.htm)、[Esri 曲线与 Shapefile 的说明](https://desktop.arcgis.com/en/arcmap/latest/manage-data/editing-fundamentals/about-editing-shapefiles.htm)。这些说明用于选择方法，不能替代当前 MCP 调用与成果读回。
