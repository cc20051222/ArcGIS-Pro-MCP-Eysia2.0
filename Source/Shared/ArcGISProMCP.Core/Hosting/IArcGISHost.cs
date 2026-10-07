using ArcGISProMCP.Core.Selection;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Core.Hosting;

/// <summary>
/// ArcGIS Pro 宿主抽象，聚合各 ArcGIS 服务。此接口位于 Shared，不引用 ArcGIS Pro SDK。
/// </summary>
public interface IArcGISHost
{
    IMapService Maps { get; }

    ILayerService Layers { get; }

    IAttributeService Attributes { get; }

    ISelectionService Selection { get; }

    IGeoprocessingService Geoprocessing { get; }

    IRasterService Raster { get; }

    IDataManagementService Data { get; }

    ILayoutService Layout { get; }

    IProjectService Project { get; }

    ILicenseService License { get; }

    IArcGISVersionService Version { get; }

    // Phase 8.3 (D-013, G-32)：选择读取/快照（Native）与账本存储。
    ISelectionReadService SelectionRead { get; }

    ISelectionStateStore SelectionState { get; }

    // Phase 9 (D-034)：schema 只读服务（get_schema_info / get_domains / get_subtypes / get_indexes）。
    ISchemaService Schema { get; }

    // Phase 9 (D-037 / F-D035-2)：坐标系解析服务（名称 → WKID；project 的 outSR 用）。
    ISpatialReferenceService SpatialReferences { get; }

    // D-062 · B 段：编辑栈（EditOperation 撤销栈；默认不提交，save/discard 显式收束）。
    IEditService Edits { get; }

    // ═══════════════════ D-064 · G-166 差异化（支柱一/二）═══════════════════
    // 均以**默认接口实现**提供（null = 该宿主不支持）：既有宿主实现（含全部测试替身）零改动即可编译。

    /// <summary>D-064 · <c>diagnose</c> 的宿主侧事实采集服务（只读）。</summary>
    IDiagnosticsService? Diagnostics => null;

    /// <summary>D-064 · <c>snapshot_project</c> / <c>restore_snapshot</c> 的宿主侧服务。</summary>
    ISnapshotService? Snapshots => null;

    /// <summary>D-084 · M1 前置 read-only 数据质量分析服务（ArcGIS SDK 实现只存在于 Compatibility）。</summary>
    ID084AnalysisService? D084Analysis => null;

    /// <summary>D-088 · M2 P1/P2 质量与溯源只读分析服务（ArcGIS SDK 实现只存在于 Compatibility）。</summary>
    ID088QualityService? D088Quality => null;

    /// <summary>D-092 · M2 P3/P4 域治理与数据管理服务（ArcGIS SDK/GP 实现只存在于 Compatibility）。</summary>
    ID092DomainService? D092Domain => null;

    /// <summary>D-094 · M2 P5 八件分析工具服务（ArcGIS SDK/GP 实现只存在于 Compatibility）。</summary>
    ID094AnalysisService? D094Analysis => null;

    /// <summary>D-096 · M2 P6 栅格族六件工具服务（ArcGIS SDK/GP 实现只存在于 Compatibility）。</summary>
    ID096RasterService? D096Raster => null;
}
