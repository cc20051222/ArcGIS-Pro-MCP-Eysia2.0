using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-064 · 工具写类分层（G-166 差异化支柱一：<c>set_readonly_mode</c> 的判定依据）。
/// 三层语义（比布尔二分更精确，避免"只读模式一开连查询都用不了"的过度阻断）：
/// <list type="bullet">
/// <item><b>Read（只读）</b>：不改变任何状态 —— 纯查询/解析/只读枚举；只读模式下**不受影响**。</item>
/// <item><b>Session（会话/视图态）</b>：只改**内存中的会话态或视口态**（选择集、相机、书签跳转），
/// **不写数据、不改工程、不落盘**；只读模式下**仍允许**（否则只读模式无法用于勘查）。</item>
/// <item><b>Write（写类/破坏类）</b>：就地修改数据/工程、删除、或**产生文件产物**；只读模式下**一律拒绝**，
/// 返回 <see cref="ErrorCodes.PermissionDenied"/> 且消息带 <c>(read-only)</c> 限定词（**错误码 33 零新增**）。</item>
/// </list>
/// 覆盖性由单测强制：全部已注册工具必须**恰好落在**某一层（不重不漏）—— 分类一旦漏项，测试立刻变红，
/// 不会出现"新工具默认放行"。未知工具名按**最严**处理（视为 <see cref="Write"/>，fail-closed）。
/// </summary>
public enum ToolWriteTier
{
    /// <summary>只读（查询/解析）。</summary>
    Read = 0,

    /// <summary>会话/视图态（不落盘的瞬态状态）。</summary>
    SessionState = 1,

    /// <summary>写类/破坏类/产物生成（只读模式下拒绝）。</summary>
    Write = 2,
}

/// <summary>D-064 · 会话级只读模式服务（<c>set_readonly_mode</c> 的宿主侧状态）。</summary>
/// <remarks>
/// **不落盘**（会话级，进程重启即失效）；**线程安全**（路由层与 run_batch 并发读）；
/// 状态可见面：<c>set_readonly_mode</c> 每次调用返回当前态 + <c>diagnose</c> 报告的 <c>readOnlyMode</c> 字段。
/// </remarks>
public interface IReadOnlyModeService
{
    /// <summary>当前是否处于只读模式（默认 false）。</summary>
    bool IsReadOnly { get; }

    /// <summary>设置只读模式；返回**设置前**的值（便于"Changed"判定与审计）。</summary>
    bool Set(bool enabled);

    /// <summary>工具写类分层判定（未知工具名 ⇒ <see cref="ToolWriteTier.Write"/>，fail-closed）。</summary>
    ToolWriteTier TierOf(string? toolName);

    /// <summary>写类工具名清单（拒绝清单；供审计与测试断言，稳定排序）。</summary>
    IReadOnlyCollection<string> WriteToolNames { get; }

    /// <summary>只读类工具名清单（稳定排序）。</summary>
    IReadOnlyCollection<string> ReadToolNames { get; }

    /// <summary>会话/视图态工具名清单（稳定排序）。</summary>
    IReadOnlyCollection<string> SessionToolNames { get; }
}

/// <summary>D-064 · 只读模式 + 写类分层的默认实现（纯内存，无 IO、无 SDK）。</summary>
public sealed class ReadOnlyModeService : IReadOnlyModeService
{
    private int _readOnly;

    public bool IsReadOnly => Volatile.Read(ref _readOnly) == 1;

    public bool Set(bool enabled)
    {
        var previous = Interlocked.Exchange(ref _readOnly, enabled ? 1 : 0) == 1;
        return previous;
    }

    public ToolWriteTier TierOf(string? toolName)
        => ToolWriteClassification.TierOf(toolName);

    public IReadOnlyCollection<string> WriteToolNames => ToolWriteClassification.WriteTools;

    public IReadOnlyCollection<string> ReadToolNames => ToolWriteClassification.ReadTools;

    public IReadOnlyCollection<string> SessionToolNames => ToolWriteClassification.SessionTools;
}

/// <summary>
/// D-064 · 工具写类分层名册（**唯一事实来源**）。
/// 三层名册合计 = 149（现役 128 基线 + 本批 21 净新增），由单测强制核对（不重不漏）。
/// </summary>
public static class ToolWriteClassification
{
    /// <summary>只读类（59 件：既有 50 ＋ D-064 6 ＋ D-073 3）。</summary>
    public static readonly IReadOnlyCollection<string> ReadTools = new HashSet<string>(StringComparer.Ordinal)
    {
        // ── 既有 50（D-064 前基线）──
        "check_extension", "dataset_summary", "describe_geoprocessing_tool", "get_arcgis_version",
        "get_audit_log", "get_broken_layers", "get_current_map", "get_dataset_info", "get_definition_query",
        "get_domains", "get_edit_session", "get_feature_count", "get_field_info", "get_field_statistics",
        "get_field_values", "get_indexes", "get_label_info", "get_layer_features", "get_layer_info",
        "get_layer_symbology", "get_layers", "get_layout_info", "get_license_info", "get_map_extent",
        "get_map_info", "get_map_view", "get_messages", "get_project_info", "get_raster_info",
        "get_schema_info", "get_selected_features", "get_subtypes", "get_unique_values", "get_workspace",
        "list_bookmarks", "list_color_ramps", "list_databases", "list_fields", "list_geoprocessing_tools",
        "list_layout_elements", "list_layouts", "list_maps", "list_rasters", "list_tables",
        "list_workspace_datasets", "ping", "python_bridge_ping", "python_runtime_info",
        "raster_statistics", "query_attributes",
        // ── D-064 新增 6 ──
        "get_environment", "search_data", "list_folder", "get_project_items", "diagnose", "set_readonly_mode",
        // ── D-073 新增 3（数据文件夹工作流：只读）──
        "scan_data_folder", "get_job_status", "get_job_report",
        // ── D-083 F03 冻结候选预登记（Read（只读）；35 件）──
        //    D-084 将其中 7 件注册为生产工具；其余候选仍为预登记。未知名仍默认 Write（fail-closed）。
        "assess_remote_sensing_quality", "check_topology_rules", "compare_datasets", "compare_schemas",
        "describe_remote_dataset", "describe_tool_catalog", "discover_remote_datasets", "find_identical",
        "generate_quality_report", "get_dataset_lineage", "get_geometry_info", "get_performance_stats",
        "inspect_point_cloud", "inspect_raster_alignment", "list_geographic_transformations", "list_jobs",
        "ps_compare_document_versions", "ps_get_capabilities", "ps_get_document_info", "ps_get_layer_info",
        "ps_list_documents", "ps_list_layers", "ps_validate_document", "raster_pixel_inspect",
        "suggest_workflow", "trace_dataset_dependencies", "validate_data_package", "validate_delivery_package",
        "validate_design_bundle", "validate_field_constraints", "validate_geometries", "validate_interchange_conformance",
        "validate_plan", "validate_relationship_class", "validate_statistical_assumptions",
    
    };

    /// <summary>会话/视图态类（9 件：既有 8 ＋ 本批 1）。</summary>
    public static readonly IReadOnlyCollection<string> SessionTools = new HashSet<string>(StringComparer.Ordinal)
    {
        // ── 既有 8 ──
        "clear_selection", "select_layer", "select_by_attribute", "select_by_location",
        "create_selection_snapshot", "restore_selection_snapshot", "set_map_view", "apply_bookmark",
        // ── D-064 新增 1 ──
        "activate_map",
        // ── D-083 F03 冻结候选预登记（Session（会话/视图态）；24 件）──
        //    D-084 将其中 3 件注册为生产工具；其余候选仍为预登记。未知名仍默认 Write（fail-closed）。
        "add_layout_picture", "cancel_job", "clone_layout", "configure_layer_elevation",
        "configure_legend", "configure_map_series", "configure_scale_bar", "configure_scene_environment",
        "delete_layout_element", "extrude_scene_features", "ps_apply_design_recipe", "ps_create_adjustment_layer",
        "ps_import_design_bundle", "ps_manage_artboards", "ps_place_design_asset", "ps_refresh_design_bundle",
        "ps_restore_document_snapshot", "ps_set_layer_mask", "ps_set_layer_properties", "ps_set_text_properties",
        "remove_layout", "set_label_properties", "set_layout_element_properties", "set_map_frame_properties",
    
    };

    /// <summary>写类/破坏类/产物类（86 件：既有 70 ＋ D-064 14 ＋ D-073 2）。只读模式下拒绝清单。</summary>
    public static readonly IReadOnlyCollection<string> WriteTools = new HashSet<string>(StringComparer.Ordinal)
    {
        // ── D-073 新增 2（数据文件夹工作流：写）──
        "load_folder_data", "apply_processing_plan",
        // ── 既有 70 ──
        "add_field", "add_join", "add_layer", "add_layout_text", "add_legend", "add_north_arrow",
        "add_scale_bar", "alter_field", "append_features", "apply_symbology_from_layer", "buffer",
        "calculate_field", "cell_statistics", "clip", "copy_dataset", "create_bookmark", "create_file_gdb",
        "create_group_layer", "create_layout", "create_map", "delete_bookmark", "delete_dataset",
        "delete_features", "discard_edits", "dissolve", "duplicate_layer", "erase", "export_layout_eps",
        "export_layout_jpg", "export_layout_pdf", "export_layout_png", "export_layout_svg",
        "export_layout_tif", "export_map_view", "export_table", "focal_statistics", "insert_features",
        "intersect", "merge", "move_layer", "near", "project", "raster_calc", "raster_clip",
        "raster_mosaic", "raster_resample", "remove_join", "remove_layer", "rename_dataset", "rename_layer",
        "repair_geometry", "repair_layer_source", "run_geoprocessing", "save_edits", "save_layer_file",
        "save_project", "set_basemap", "set_definition_query", "set_label_visibility", "set_layer_renderer",
        "set_layer_scale_range", "set_layer_transparency", "set_layer_visibility", "set_map_extent",
        "set_simple_symbology", "set_workspace", "spatial_join", "summarize_features", "union",
        "update_features",
        // ── D-064 新增 14 ──
        "create_feature_class", "create_table", "add_fields", "delete_field", "truncate_table",
        "export_features", "remove_map", "set_map_properties", "set_environment", "add_folder_connection",
        "run_batch", "export_map_series", "snapshot_project", "restore_snapshot",
        // ── D-083 F03 冻结候选预登记（Write（写/破坏/产物）；72 件）──
        //    D-084 未新增 Write 项。未知名仍默认 Write（fail-closed）。
        "analyze_emerging_hotspots", "analyze_line_of_sight", "analyze_network_disruption", "analyze_scenario_sensitivity",
        "analyze_viewshed", "assess_classification_accuracy", "assign_domain_to_field", "build_od_cost_matrix",
        "build_raster_pyramids", "build_raster_time_series", "build_space_time_cube", "calculate_cost_distance",
        "calculate_cut_fill", "calculate_geometry_attributes", "calculate_service_areas", "classify_ground_points",
        "classify_land_cover", "compare_multi_criteria_scenarios", "compare_point_cloud_epochs", "compare_temporal_trajectories",
        "compose_evidence_report", "compose_presentation_deck", "compose_raster_bands", "configure_subtypes",
        "create_domain", "create_elevation_profile", "create_relationship_class", "cross_validate_spatial_model",
        "define_projection", "delete_domain", "derive_canopy_height", "derive_terrain_surface",
        "design_spatial_sample", "detect_raster_trends", "detect_temporal_change_points", "evaluate_scenario_ensemble",
        "export_data_package", "export_design_bundle", "export_layout_template", "export_scene_package",
        "export_spatiotemporal_cube", "export_time_animation", "filter_point_cloud", "find_closest_facilities",
        "forecast_spatiotemporal_series", "generate_tessellation", "hotspot_analysis", "import_layout_template",
        "import_remote_dataset", "infer_approved_raster_model", "interpolate_surface", "mask_cloud_and_shadow",
        "optimize_vehicle_routes", "polygon_neighbors", "propagate_spatial_uncertainty", "ps_create_document_snapshot",
        "ps_export_deliverables", "ps_preview_document", "publish_map_service", "raster_change_detection",
        "raster_reproject", "refresh_design_bundle", "remove_domain_from_field", "segment_raster_objects",
        "simplify_features", "smooth_features", "solve_location_allocation", "solve_routes",
        "spatial_autocorrelation", "transform_vertical_coordinates", "update_domain", "zonal_histogram",
    
    };

    /// <summary>三层名册合计（应为 149）。</summary>
    public static int Total => ReadTools.Count + SessionTools.Count + WriteTools.Count;

    /// <summary>分层判定：未知工具名 ⇒ <see cref="ToolWriteTier.Write"/>（fail-closed）。</summary>
    public static ToolWriteTier TierOf(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return ToolWriteTier.Write;
        }

        if (ReadTools.Contains(toolName))
        {
            return ToolWriteTier.Read;
        }

        if (SessionTools.Contains(toolName))
        {
            return ToolWriteTier.SessionState;
        }

        return ToolWriteTier.Write;
    }

    /// <summary>只读模式下是否拒绝该工具（仅 <see cref="ToolWriteTier.Write"/> 拒绝）。</summary>
    public static bool RefusedInReadOnly(string? toolName) => TierOf(toolName) == ToolWriteTier.Write;

    /// <summary>只读模式拒绝的统一错误（复用 <see cref="ErrorCodes.PermissionDenied"/>；消息带 <c>(read-only)</c>）。</summary>
    public static OperationError ReadOnlyRefusal(string toolName)
        => new(
            ErrorCodes.PermissionDenied,
            $"'{toolName}' is a write/destructive tool and the server is in read-only mode (read-only); the call was refused before execution (no state change). "
            + "Call set_readonly_mode with enabled=false to leave read-only mode.",
            "reason=read-only-mode; tier=Write");
}
