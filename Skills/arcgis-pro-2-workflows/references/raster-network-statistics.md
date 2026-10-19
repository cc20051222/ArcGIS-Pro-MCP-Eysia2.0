# 栅格、网络、空间统计与高级计算

## 栅格和影像

先读 `get_raster_info`、`raster_pixel_inspect`、`inspect_raster_alignment`，按需 `raster_statistics`。确认波段、像元类型、单位/缩放、CRS、分辨率、原点/对齐、范围、NoData 和时相。检测工具的支持格式/采样范围以返回结果为准，不凭扩展名承诺可处理。

`raster_clip`、`raster_resample`、`raster_reproject`、`raster_mosaic`、`compose_raster_bands`、`cell_statistics`、`focal_statistics`、`zonal_histogram`、`raster_change_detection` 各自读取 schema 和许可；环境的 cell size、snap raster、extent、mask 只有后端支持时才设置。分类数据采用保类别的已支持重采样，连续数据按研究目的选方法。NoData 不自动填 0；更细输出格网不增加真实分辨率。`raster_calc` 仅使用该工具接受的受限表达式，不当作任意代码入口。

比较多期影像先统一可比较的单位、时相、掩膜、CRS 和格网；缺乏可比性时不把差分解释成真实地物变化。核输出像元、范围/对齐、统计分母和 NoData 数量。`build_raster_pyramids` 会生成附属产物，也需明确输出/副作用范围。

## 网络与空间统计

`calculate_service_areas`、`solve_routes` 需要真实网络来源、impedance、时间/距离单位、行驶方式、限制、必要时段及许可。无法满足时阻断相应步骤；普通缓冲只能以明确不同的业务方法重新确认，不能冒称驾车服务区。

`spatial_autocorrelation`、`hotspot_analysis`、`validate_statistical_assumptions` 先查坐标/尺度、邻接/距离、权重、样本、缺失、检验前提及多重比较口径。输出统计量、适用范围与失败对象；图好看不证明显著或因果。许可不足不切另一种方法后沿用原标题。

## 已注册的高级计算

时序：`detect_temporal_change_points`、`compare_temporal_trajectories`；影像质量：`assess_remote_sensing_quality`、`assess_classification_accuracy`；采样/验证：`design_spatial_sample`、`cross_validate_spatial_model`；情景：`analyze_scenario_sensitivity`、`compare_multi_criteria_scenarios`、`evaluate_scenario_ensemble`。

这些工具中部分计算针对显式提供的表格/数组，而非自动读取任意图层。必须先读 schema 的载体、样本量、时间顺序、单位、种子、缺失政策与支持的算法；不假定同名功能覆盖完整 ArcGIS 专业工具箱。模型交叉验证注意空间/时序泄漏，分类精度需要独立参照及混淆矩阵分母，情景权重/归一化/方向和敏感区间要可追溯。

`inspect_point_cloud` 当前源码只承诺 LAS 1.2 / PDRF 0 子集，不能扩大到任意 LAZ、LAS 版本或点格式。`validate_interchange_conformance` 依具体格式子集报告，不冒称全面标准认证。

快照中以下四件参数面已注册，但执行返回 `NOT_IMPLEMENTED`：`export_time_animation`、`extrude_scene_features`、`create_elevation_profile`、`export_scene_package`。对应任务显示受阻并保留可交付的其他成果；不循环调用、不造示例文件、不用外部程序悄悄补齐。未来经真实证据确认修复后再更新此限制。
