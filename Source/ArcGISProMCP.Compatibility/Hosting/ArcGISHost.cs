using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Selection;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Hosting;

/// <summary>
/// ArcGIS Pro 宿主实现（聚合各服务）。实现位于 Shared 的 <see cref="IArcGISHost"/>。
/// </summary>
public sealed class ArcGISHost : IArcGISHost
{
    public ArcGISHost(
        IMapService maps,
        ILayerService layers,
        IAttributeService attributes,
        ISelectionService selection,
        IGeoprocessingService geoprocessing,
        IRasterService raster,
        IDataManagementService data,
        ILayoutService layout,
        IProjectService project,
        ILicenseService license,
        IArcGISVersionService version,
        ISelectionReadService selectionRead,
        ISelectionStateStore selectionState,
        ISchemaService schema,
        ISpatialReferenceService spatialReferences,
        IEditService? edits = null,
        IDiagnosticsService? diagnostics = null,
        ISnapshotService? snapshots = null)
    {
        Maps = maps;
        Layers = layers;
        Attributes = attributes;
        Selection = selection;
        Geoprocessing = geoprocessing;
        Raster = raster;
        Data = data;
        Layout = layout;
        Project = project;
        License = license;
        Version = version;
        SelectionRead = selectionRead;
        SelectionState = selectionState;
        Schema = schema;
        SpatialReferences = spatialReferences;
        Edits = edits ?? new Services.EditService();
        Diagnostics = diagnostics;
        Snapshots = snapshots;
        D084Analysis = new Services.D084AnalysisService();
        D088Quality = new Services.D088QualityService();
        D092Domain = new Services.D092DomainService();
        D094Analysis = new Services.D094AnalysisService();
        D096Raster = new Services.D096RasterService();
    }

    public IMapService Maps { get; }

    public ILayerService Layers { get; }

    public IAttributeService Attributes { get; }

    public ISelectionService Selection { get; }

    public IGeoprocessingService Geoprocessing { get; }

    public IRasterService Raster { get; }

    public IDataManagementService Data { get; }

    public ILayoutService Layout { get; }

    public IProjectService Project { get; }

    public ILicenseService License { get; }

    public IArcGISVersionService Version { get; }

    public ISelectionReadService SelectionRead { get; }

    public ISelectionStateStore SelectionState { get; }

    public ISchemaService Schema { get; }

    public ISpatialReferenceService SpatialReferences { get; }

    /// <summary>D-062 · B 段：编辑栈（缺省 EditService；可注入替身）。</summary>
    public IEditService Edits { get; }

    /// <summary>D-064 · G-166：<c>diagnose</c> 宿主事实采集（可空 ⇒ 工具侧降级为 Core 自证事实集）。</summary>
    public IDiagnosticsService? Diagnostics { get; }

    /// <summary>D-064 · G-166：项目快照/恢复（可空 ⇒ 相关工具返回 NOT_IMPLEMENTED）。</summary>
    public ISnapshotService? Snapshots { get; }

    public ID084AnalysisService? D084Analysis { get; }

    public ID088QualityService? D088Quality { get; }

    public ID092DomainService? D092Domain { get; }

    public ID094AnalysisService? D094Analysis { get; }

    public ID096RasterService? D096Raster { get; }
}
