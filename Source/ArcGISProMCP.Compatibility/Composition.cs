using System.Diagnostics;
using ArcGISProMCP.Core.Selection;
using System.IO;
using ArcGISProMCP.Compatibility.Health;
using ArcGISProMCP.Compatibility.Hosting;
using ArcGISProMCP.Compatibility.Services;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Container;
using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.PythonBridge;
using ArcGISProMCP.Core.Runtime;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.Server;
using ArcGISProMCP.Server.Transport;
using ArcGISProMCP.Tools;
using ArcGISProMCP.Compatibility.Logging;

namespace ArcGISProMCP.Compatibility;

/// <summary>组合根：负责装配 Host / Services / Tools / Registry / Router / MCP Server。</summary>
internal static class Composition
{
    private static readonly Lazy<ServiceContainer> _lazy = new(BuildContainer);
    private static readonly object _gate = new();

    private static MCPToolRegistry? _registry;
    private static McpServer? _server;
    private static ILogger? _serverLogger;
    private static RuntimePathResolution? _runtimePathResolution;

    public static ServiceContainer Container => _lazy.Value;

    public static string LogDir
        => RuntimePathResolution.Paths?.RuntimeRoot ?? string.Empty;

    /// <summary>唯一生产 managed structured sink 的 owned root。</summary>
    public static string ManagedLogDir
        => RuntimePathResolution.Paths?.ManagedLogDirectory ?? string.Empty;

    public static RuntimePathResolution RuntimePathResolution
        => _runtimePathResolution ??= ResolveRuntimePaths();

    public static MCPSettings Settings => Container.Resolve<MCPSettings>();

    public static string Endpoint
    {
        get
        {
            var s = Settings;
            return $"http://{s.Host}:{s.Port}{s.Endpoint}";
        }
    }

    public static MCPToolRegistry Registry
    {
        get
        {
            lock (_gate)
            {
                return _registry ??= BuildRegistry();
            }
        }
    }

    public static ILogger ServerLogger
    {
        get
        {
            lock (_gate)
            {
                if (_serverLogger is null)
                {
                    _serverLogger = RuntimePathResolution.Paths is { } paths
                        ? new ManagedFileLogger(paths.ManagedLogDirectory)
                        : NullLogger.Instance;
                }

                return _serverLogger;
            }
        }
    }

    public static McpServer Server
    {
        get
        {
            lock (_gate)
            {
                return _server ??= CreateServer();
            }
        }
    }

    /// <summary>释放 Python Bridge 持久进程（Pro 退出/卸载时调用，避免残留 python.exe）。</summary>
    public static async Task DisposePythonBridgeAsync()
    {
        try
        {
            if (_lazy.IsValueCreated)
            {
                var manager = Container.Resolve<PythonBridgeProcessManager>();
                await manager.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            ServerLogger.Warning("dispose python bridge failed: " + ex.Message, category: "pythonbridge");
        }
    }

    private static ServiceContainer BuildContainer()
    {
        var c = new ServiceContainer();

        var resolution = RuntimePathResolution;
        var paths = resolution.Paths;
        var settings = new MCPSettings
        {
            PythonExecutable = paths?.PythonExecutable ?? string.Empty,
            PythonBridgeScript = paths?.PythonBridgeScript ?? string.Empty,
            PythonWorkingDirectory = paths?.PythonWorkingDirectory ?? string.Empty,
            RuntimeRoot = paths?.RuntimeRoot ?? string.Empty,
            ManagedLogDirectory = paths?.ManagedLogDirectory ?? string.Empty,
            ManagedLogDirectoryOverride = ManagedLogDirectoryOverrideValue ?? string.Empty,
            RuntimePathErrorCode = resolution.ErrorCode,
            PythonProcessStartupTimeoutMs = 40000,
        };
        c.RegisterInstance(settings);

        c.Register<IHealthService>(_ => new HealthService());
        c.Register<IDiagnosticExportService>(_ => new DiagnosticExportService(ManagedLogDir));
        c.Register<IStatusPresenter>(_ => new StatusPresenter());
        c.Register<IHealthFactsProvider>(s => new RuntimeHealthFactsProvider(
            s.Resolve<MCPSettings>(),
            () => _server?.IsRunning,
            () => true,
            () => _lazy.IsValueCreated
                ? Container.TryResolveExisting<PythonBridgeProcessManager>()
                : null,
            () => _serverLogger,
            ManagedCompatibilityFacts.Current));

        // Python Bridge：单例 ProcessManager → 单例 Service（全局一个持久 python 进程）。
        c.Register<PythonBridgeProcessManager>(_ => new PythonBridgeProcessManager(settings, ServerLogger));
        c.Register<IPythonBridgeService>(s => new PythonBridgeService(s.Resolve<PythonBridgeProcessManager>(), ServerLogger));

        c.Register<IMapService>(_ => new MapService());
        c.Register<ILayerService>(_ => new LayerService());
        c.Register<IProjectService>(_ => new ProjectService());
        c.Register<ILicenseService>(_ => new LicenseService());
        c.Register<IArcGISVersionService>(_ => new VersionService());
        c.Register<IAttributeService>(_ => new AttributeService());
        c.Register<ISelectionService>(sc => new SelectionService(sc.Resolve<ISelectionStateStore>()));
        c.Register<ISelectionStateStore>(_ => new SelectionStateStore());
        c.Register<ISelectionReadService>(sc => new SelectionReadService(sc.Resolve<ISelectionStateStore>()));
        c.Register<IGeoprocessingService>(_ => new GeoprocessingService());
        c.Register<IRasterService>(_ => new RasterService());
        c.Register<IDataManagementService>(_ => new DataManagementService());
        c.Register<ILayoutService>(_ => new LayoutService());
        // Phase 9 (D-034)：schema 只读服务（get_schema_info / get_domains / get_subtypes / get_indexes）。
        // D-064：同一服务承载 A 段 schema 创建（GP 通路）⇒ 注入 GP / 图层 / 选择读取。
        c.Register<ISchemaService>(s => new SchemaService(
            s.Resolve<IGeoprocessingService>(),
            s.Resolve<ILayerService>(),
            s.Resolve<ISelectionReadService>()));
        // Phase 9 (D-037 / F-D035-2)：坐标系解析（project.outSR 名称形态 → WKID）。
        c.Register<ISpatialReferenceService>(_ => new SpatialReferenceService());
        // D-064：会话级只读模式（G-166 支柱一）+ diagnose 事实采集 + 项目快照（支柱二）。
        c.Register<IReadOnlyModeService>(_ => new ReadOnlyModeService());
        c.Register<IDiagnosticsService>(s => new D064DiagnosticsService(
            s.Resolve<MCPSettings>(),
            s.Resolve<IArcGISVersionService>(),
            s.Resolve<IProjectService>(),
            s.Resolve<IReadOnlyModeService>(),
            () => Registry.Count));
        c.Register<ISnapshotService>(s => new D064SnapshotService(
            s.Resolve<IProjectService>(),
            s.Resolve<ILayerService>(),
            CloseProjectForRestoreAsync,
            ReopenProjectAfterRestoreAsync));
        c.Register<IArcGISHost>(s => new ArcGISHost(
            s.Resolve<IMapService>(),
            s.Resolve<ILayerService>(),
            s.Resolve<IAttributeService>(),
            s.Resolve<ISelectionService>(),
            s.Resolve<IGeoprocessingService>(),
            s.Resolve<IRasterService>(),
            s.Resolve<IDataManagementService>(),
            s.Resolve<ILayoutService>(),
            s.Resolve<IProjectService>(),
            s.Resolve<ILicenseService>(),
            s.Resolve<IArcGISVersionService>(),
            s.Resolve<ISelectionReadService>(),
            s.Resolve<ISelectionStateStore>(),
            s.Resolve<ISchemaService>(),
            s.Resolve<ISpatialReferenceService>(),
            null,                                   // edits：保持既有缺省（D-062 EditService）
            s.Resolve<IDiagnosticsService>(),
            s.Resolve<ISnapshotService>()));
        return c;
    }

    private static RuntimePathResolution ResolveRuntimePaths()
    {
        string? assemblyDirectory = null;
        try
        {
            assemblyDirectory = Path.GetDirectoryName(
                typeof(Composition).Assembly.Location);
        }
        catch
        {
            // Resolver returns a fixed safe code for an unavailable assembly path.
        }

        string? activeArcGISProPath = null;
        try
        {
            activeArcGISProPath = Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch
        {
            // Resolver returns a fixed safe code for an unavailable process path.
        }

        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        return RuntimePathResolver.Resolve(new RuntimePathInputs
        {
            CompatibilityAssemblyDirectory = assemblyDirectory,
            ActiveArcGISProExecutablePath = activeArcGISProPath,
            LocalApplicationDataDirectory = localApplicationData,
            ManagedLogDirectoryOverride = ManagedLogDirectoryOverrideValue,
            ReleaseVersion = ManagedCompatibilityFacts.Current.AddInVersion
        });
    }

    /// <summary>
    /// D-125（O-D100-01／O-D120-01）：受管日志目录开关在此读取，RuntimePathResolver 不接触环境变量。
    /// 未设置或空白＝null＝缺省派生路径逐字节不变；读取失败同样按未设置处理（不改生产行为）。
    /// </summary>
    private static string? ReadManagedLogDirectoryOverride()
    {
        try
        {
            var value = Environment.GetEnvironmentVariable(MCPSettings.LogDirectoryEnvironmentVariable);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static readonly string? ManagedLogDirectoryOverrideValue = ReadManagedLogDirectoryOverride();

    public static MCPToolRegistry BuildRegistry()
    {
        var registry = new MCPToolRegistry();
        // 基础/系统
        registry.Register(new PingTool());
        // 地图
        registry.Register(new GetCurrentMapTool());
        registry.Register(new ListMapsTool());
        registry.Register(new GetMapInfoTool());
        // 图层
        registry.Register(new GetLayersTool());
        registry.Register(new GetLayerInfoTool());
        registry.Register(new SetLayerVisibilityTool());
        registry.Register(new AddLayerTool());
        registry.Register(new RemoveLayerTool());
        // 工程
        registry.Register(new GetProjectInfoTool());
        registry.Register(new ListLayoutsTool());
        registry.Register(new ListDatabasesTool());
        // 属性
        registry.Register(new QueryAttributesTool());
        registry.Register(new GetFieldInfoTool());
        registry.Register(new GetFeatureCountTool());
        // 选择
        registry.Register(new ClearSelectionTool());
        registry.Register(new SelectLayerTool());
        // Phase 8.3（D-013, G-32）：Selection Contract V2（30 → 35）
        registry.Register(new SelectByAttributeTool());
        registry.Register(new SelectByLocationTool());
        registry.Register(new GetSelectedFeaturesTool());
        registry.Register(new CreateSelectionSnapshotTool());
        registry.Register(new RestoreSelectionSnapshotTool());
        // 版本/许可
        registry.Register(new GetArcGISVersionTool());
        registry.Register(new GetLicenseInfoTool());
        // 地理处理
        registry.Register(new BufferTool());
        registry.Register(new ClipTool());
        registry.Register(new IntersectTool());
        registry.Register(new DissolveTool());
        // Phase 11 第一批（D-048，66 -> 69）：空间分析首批（GP 类首批）
        registry.Register(new SpatialJoinTool());
        registry.Register(new NearTool());
        registry.Register(new RasterClipTool());
        // Phase 11 第二批（D-049，69 -> 73）：Erase/Union + 栅格扩展
        registry.Register(new EraseTool());
        registry.Register(new UnionTool());
        registry.Register(new RasterResampleTool());
        registry.Register(new RasterStatisticsTool());
        // Phase 11 第三批（D-050，73 -> 75）：栅格镶嵌 + 受约束栅格计算（Phase 11 收官批）
        registry.Register(new RasterMosaicTool());
        registry.Register(new RasterCalcTool());
        // Phase 12 第二批（D-053，75 -> 78）：编辑类新工具（删除/重命名/Append；全部破坏性或就地修改，守卫+预检+披露）
        registry.Register(new DeleteDatasetTool());
        registry.Register(new RenameDatasetTool());
        registry.Register(new AppendFeaturesTool());
        // Phase 15 功能批（D-060 · B 项，G-160 已批准，78 -> 80）：统计聚合 2 工具（CellStatistics/FocalStatistics）
        registry.Register(new CellStatisticsTool());
        registry.Register(new FocalStatisticsTool());
        // 数据管理 / 栅格
        registry.Register(new GetDatasetInfoTool());
        registry.Register(new GetRasterInfoTool());
        // Python Bridge（Phase 5）
        registry.Register(new PythonBridgePingTool());
        registry.Register(new PythonRuntimeInfoTool());
        // Python Discovery / ArcPy（Phase 5.5.3）
        registry.Register(new DatasetSummaryTool());
        registry.Register(new ListFieldsTool());
        registry.Register(new ListWorkspaceDatasetsTool());
        // Phase 9（D-034，35 → 39）：schema 只读信息族
        registry.Register(new GetSchemaInfoTool());
        registry.Register(new GetDomainsTool());
        registry.Register(new GetSubtypesTool());
        registry.Register(new GetIndexesTool());
        // Phase 9（D-035，39 → 42）：GP 产出写族（复制/导出/投影）
        registry.Register(new CopyDatasetTool());
        registry.Register(new ExportTableTool());
        registry.Register(new ProjectTool());
        // Phase 9 第三批（D-036，42 → 45）：GP 产出写族（建库/合并/字段）
        registry.Register(new CreateFileGdbTool());
        registry.Register(new MergeTool());
        registry.Register(new AddFieldTool());
        // Phase 9 第四批（D-038，45 → 48）：字段信息扩展 + 计算字段（受限表达式）
        registry.Register(new AlterFieldTool());
        registry.Register(new CalculateFieldTool());
        registry.Register(new GetFieldValuesTool());
        // Phase 10 第一批（D-042，48 → 52）：布局与范围只读信息
        registry.Register(new GetLayoutInfoTool());
        registry.Register(new ListLayoutElementsTool());
        registry.Register(new GetMapExtentTool());
        registry.Register(new GetDefinitionQueryTool());
        // Phase 10 第二批（D-043，52 -> 55）：地图设置（写类首批）
        registry.Register(new SetDefinitionQueryTool());
        registry.Register(new MoveLayerTool());
        registry.Register(new SetMapExtentTool());
        // Phase 10 第三批（D-045，55 -> 60）：自有布局构建
        registry.Register(new CreateLayoutTool());
        registry.Register(new AddLayoutTextTool());
        registry.Register(new AddLegendTool());
        registry.Register(new AddNorthArrowTool());
        registry.Register(new AddScaleBarTool());
        // Phase 10 第四批（D-046，60 -> 64）：简单符号与标注设置
        registry.Register(new GetLayerSymbologyTool());
        registry.Register(new SetSimpleSymbologyTool());
        registry.Register(new GetLabelInfoTool());
        registry.Register(new SetLabelVisibilityTool());
        // Phase 10 第五批（D-047，64 -> 66）：布局 PNG/PDF 导出（收官批；export_map_png 因 MG3' 降级不实现）
        registry.Register(new ExportLayoutPdfTool());
        registry.Register(new ExportLayoutPngTool());
        // D-061（功能完善第一批 · 生态对齐，80 -> 93）：
        //   布局导出四新格式复用同一基类与守卫。
        registry.Register(new ExportLayoutJpgTool());
        registry.Register(new ExportLayoutTifTool());
        registry.Register(new ExportLayoutSvgTool());
        registry.Register(new ExportLayoutEpsTool());
        //   工程/地图/数据/属性九件。
        registry.Register(new SaveProjectTool());
        registry.Register(new CreateMapTool());
        registry.Register(new SetWorkspaceTool());
        registry.Register(new GetWorkspaceTool());
        registry.Register(new ListRastersTool());
        registry.Register(new ListTablesTool());
        registry.Register(new GetUniqueValuesTool());
        registry.Register(new GetLayerFeaturesTool());
        registry.Register(new RepairGeometryTool());
        // ── D-062 · 旗舰批：受控 GP（5）+ 编辑栈（6）+ 统计聚合（2）+ 审计查询（1）⇒ 107 ──
        registry.Register(new RunGeoprocessingTool());
        registry.Register(new ListGeoprocessingToolsTool());
        registry.Register(new DescribeGeoprocessingToolTool());
        registry.Register(new GetMessagesTool());
        registry.Register(new CheckExtensionTool());
        registry.Register(new GetAuditLogTool());
        registry.Register(new InsertFeaturesTool());
        registry.Register(new UpdateFeaturesTool());
        registry.Register(new DeleteFeaturesTool());
        registry.Register(new SaveEditsTool());
        registry.Register(new DiscardEditsTool());
        registry.Register(new GetEditSessionTool());
        registry.Register(new GetFieldStatisticsTool());
        registry.Register(new SummarizeFeaturesTool());

        // ── D-063 · 功能完善第三批：图层管理（10）+ 渲染进阶（4 新增）+ 视图书签与回图（7）⇒ 128 ──
        // 注：枚举 22 件中的 get_layer_symbology 已是现役 107 之一（D-046）⇒ 本批按 B2 语义**增强**该件，不重复注册。
        // A · 图层管理
        registry.Register(new SetLayerTransparencyTool());
        registry.Register(new SetLayerScaleRangeTool());
        registry.Register(new CreateGroupLayerTool());
        registry.Register(new SetBasemapTool());
        registry.Register(new GetBrokenLayersTool());
        registry.Register(new RepairLayerSourceTool());
        registry.Register(new AddJoinTool());
        registry.Register(new RemoveJoinTool());
        registry.Register(new RenameLayerTool());
        registry.Register(new DuplicateLayerTool());
        // B · 渲染进阶（新增 4；get_layer_symbology 为既有件增强）
        registry.Register(new SetLayerRendererTool());
        registry.Register(new ListColorRampsTool());
        registry.Register(new ApplySymbologyFromLayerTool());
        registry.Register(new SaveLayerFileTool());
        // C · 视图书签 ＋ ★回图
        registry.Register(new GetMapViewTool());
        registry.Register(new SetMapViewTool());
        registry.Register(new ExportMapViewTool());
        registry.Register(new ListBookmarksTool());
        registry.Register(new CreateBookmarkTool());
        registry.Register(new ApplyBookmarkTool());
        registry.Register(new DeleteBookmarkTool());

        // ── D-064 · 功能完善第四批（128 → **149**）：schema 创建（6）+ project 增强（5）
        //    + 数据发现（4）+ 批处理与系列输出（2）+ G-166 差异化（4）⇒ 21 净新增（三步法；与现役零重名）──
        // A · Schema 创建（GP 通路；输出守卫 / 输入守卫 / confirm 缺省拒）
        registry.Register(new CreateFeatureClassTool());
        registry.Register(new CreateTableTool());
        registry.Register(new AddFieldsTool());
        registry.Register(new DeleteFieldTool());
        registry.Register(new TruncateTableTool());
        registry.Register(new ExportFeaturesTool());
        // B · Project 增强（状态类 + confirm；GP 环境会话级）
        registry.Register(new RemoveMapTool());
        registry.Register(new ActivateMapTool());
        registry.Register(new SetMapPropertiesTool());
        registry.Register(new GetEnvironmentTool());
        registry.Register(new SetEnvironmentTool());
        // C · 数据发现（路径守卫 + 深度/数量上限；连接注册）
        registry.Register(new SearchDataTool());
        registry.Register(new ListFolderTool());
        registry.Register(new AddFolderConnectionTool());
        registry.Register(new GetProjectItemsTool());
        // D · 批处理与系列输出（run_batch = 安全重点：白名单 + 30s 硬约束 + 不豁免）
        registry.Register(new RunBatchTool());
        registry.Register(new ExportMapSeriesTool());
        // ★ G-166 差异化增补（支柱一/二；竞品零覆盖）
        registry.Register(new DiagnoseTool());
        registry.Register(new SnapshotProjectTool());
        registry.Register(new RestoreSnapshotTool());
        registry.Register(new SetReadOnlyModeTool());

        // ★ D-073 数据文件夹工作流（5 件；G-184/C-018）
        registry.Register(new ScanDataFolderTool());
        registry.Register(new LoadFolderDataTool());
        registry.Register(new ApplyProcessingPlanTool());
        registry.Register(new GetJobStatusTool());
        registry.Register(new GetJobReportTool());

        // ★ D-084 M1 前置（154 → 164）：B1 作业控制/检索（4）＋B2 度量/配置（6）
        registry.Register(new ListJobsTool());
        registry.Register(new CancelJobTool());
        registry.Register(new ValidatePlanTool());
        registry.Register(new DescribeToolCatalogTool());
        registry.Register(new GetGeometryInfoTool());
        registry.Register(new FindIdenticalTool());
        registry.Register(new GenerateQualityReportTool());
        registry.Register(new SetLayoutElementPropertiesTool());
        registry.Register(new SetLabelPropertiesTool());
        registry.Register(new ConfigureMapSeriesTool());
        // ★ D-086 B3/B4 非 PS 工具（164 → 170）：素材包、交付校验、建议与账本统计
        registry.Register(new ExportDesignBundleTool());
        registry.Register(new ValidateDesignBundleTool());
        registry.Register(new RefreshDesignBundleTool());
        registry.Register(new ValidateDeliveryPackageTool());
        registry.Register(new SuggestWorkflowTool());
        registry.Register(new GetPerformanceStatsTool());
        // ★ D-088 M2 批一 P1/P2 质量与溯源（170 → 179）：9 件全 Read 层
        registry.Register(new ValidateGeometriesTool());
        registry.Register(new CheckTopologyRulesTool());
        registry.Register(new CompareDatasetsTool());
        registry.Register(new CompareSchemasTool());
        registry.Register(new ValidateFieldConstraintsTool());
        registry.Register(new InspectRasterAlignmentTool());
        registry.Register(new ListGeographicTransformationsTool());
        registry.Register(new TraceDatasetDependenciesTool());
        registry.Register(new GetDatasetLineageTool());
        // ★ D-092 M2 批二 P3/P4 域治理与数据管理（179 → 189）：9W＋1R
        registry.Register(new CreateDomainTool());
        registry.Register(new UpdateDomainTool());
        registry.Register(new DeleteDomainTool());
        registry.Register(new AssignDomainToFieldTool());
        registry.Register(new RemoveDomainFromFieldTool());
        registry.Register(new ConfigureSubtypesTool());
        registry.Register(new CreateRelationshipClassTool());
        registry.Register(new CalculateGeometryAttributesTool());
        registry.Register(new DefineProjectionTool());
        registry.Register(new ValidateRelationshipClassTool());
        // ★ D-094 M2 批三 P5 八件分析工具（189 → 197）：全 W
        registry.Register(new SimplifyFeaturesTool());
        registry.Register(new SmoothFeaturesTool());
        registry.Register(new PolygonNeighborsTool());
        registry.Register(new GenerateTessellationTool());
        registry.Register(new CalculateServiceAreasTool());
        registry.Register(new SolveRoutesTool());
        registry.Register(new SpatialAutocorrelationTool());
        registry.Register(new HotspotAnalysisTool());
        // ★ D-096 M2 批四 P6 栅格族六件（197 → 203）：1R＋5W
        registry.Register(new RasterPixelInspectTool());
        registry.Register(new BuildRasterPyramidsTool());
        registry.Register(new ComposeRasterBandsTool());
        registry.Register(new RasterChangeDetectionTool());
        registry.Register(new RasterReprojectTool());
        registry.Register(new ZonalHistogramTool());

        // ── D-091 · PS 通道五件（203 → 208）；PS/UXP 端到端行为保持 NOT_VERIFIED ──
        registry.Register(new PsGetCapabilitiesTool());
        registry.Register(new PsApplyDesignRecipeTool());
        registry.Register(new PsImportDesignBundleTool());
        registry.Register(new PsRefreshDesignBundleTool());
        registry.Register(new PsExportDeliverablesTool());

        // ── D-102 · PS 剩余 15 件（208 → 223）；真机/UXP 保持 NOT_VERIFIED ──
        registry.Register(new PsCompareDocumentVersionsTool());
        registry.Register(new PsGetDocumentInfoTool());
        registry.Register(new PsGetLayerInfoTool());
        registry.Register(new PsListDocumentsTool());
        registry.Register(new PsListLayersTool());
        registry.Register(new PsValidateDocumentTool());
        registry.Register(new PsCreateAdjustmentLayerTool());
        registry.Register(new PsManageArtboardsTool());
        registry.Register(new PsPlaceDesignAssetTool());
        registry.Register(new PsRestoreDocumentSnapshotTool());
        registry.Register(new PsSetLayerMaskTool());
        registry.Register(new PsSetLayerPropertiesTool());
        registry.Register(new PsSetTextPropertiesTool());
        registry.Register(new PsCreateDocumentSnapshotTool());
        registry.Register(new PsPreviewDocumentTool());

        // ── D-104 M4 X15：点云只读检查；LAS 1.2 点格式 0 子集，LAZ/COPC 不作支持推定 ──
        registry.Register(new InspectPointCloudTool());

        // ── D-118 M4 Native 十五件（G-275 一期 48→15；冻结 schema 逐字对齐；真机面 NOT VERIFIED）──
        registry.Register(new DetectTemporalChangePointsTool());
        registry.Register(new CompareTemporalTrajectoriesTool());
        registry.Register(new ExportTimeAnimationTool());
        registry.Register(new ExtrudeSceneFeaturesTool());
        registry.Register(new CreateElevationProfileTool());
        registry.Register(new ExportScenePackageTool());
        registry.Register(new AssessRemoteSensingQualityTool());
        registry.Register(new AssessClassificationAccuracyTool());
        registry.Register(new DesignSpatialSampleTool());
        registry.Register(new CrossValidateSpatialModelTool());
        registry.Register(new AnalyzeScenarioSensitivityTool());
        registry.Register(new CompareMultiCriteriaScenariosTool());
        registry.Register(new EvaluateScenarioEnsembleTool());
        registry.Register(new ValidateStatisticalAssumptionsTool());
        registry.Register(new ValidateInterchangeConformanceTool());
        return registry;
    }

    public static MCPToolRouter BuildRouter(MCPSettings settings, ILogger logger)
        => new(
            Registry,
            Container.Resolve<IArcGISHost>(),
            settings,
            logger,
            Container.Resolve<IPythonBridgeService>(),
            Container.Resolve<IReadOnlyModeService>(),
            new ToolInvoker(new ToolValidatorPipeline(new ProtectedOutputPathArgumentValidator())));

    /// <summary>
    /// D-064：恢复快照前**关闭当前工程**，释放 Pro 对 .aprx 的独占句柄
    /// （LIVE 实测：工程打开态下恢复写失败 "file is being used by another process"）。
    /// <para><b>可达性处置（如实披露）</b>：本产品**编译面固定 Pro 3.0 引用集**（`sdk-refs/pro-13.0-net6`，
    /// 以覆盖 3.0–3.5），而 `Project.CloseAsync` 在 3.0 引用集中**不可直接引用**（构建期 CS1061 证伪）⇒
    /// 改为**运行时反射调用**（3.5 实机具备；缺失则如实说明并落到"不关闭"降级路径）。
    /// 失败不阻断恢复（返回 Ok + 说明；内层复制另有重试兜底）。</para>
    /// </summary>
    private static async Task<ArcGISProMCP.Core.Results.OperationResult<object?>> CloseProjectForRestoreAsync(
        string aprxPath, CancellationToken ct)
    {
        var notes = new List<string>();
        try
        {
            var project = ArcGIS.Desktop.Core.Project.Current;
            if (project is null)
            {
                return ArcGISProMCP.Core.Results.OperationResult<object?>.Ok(
                    null, "no project was open; nothing to release before restore");
            }

            notes.Add("dirtyBefore=" + project.IsDirty);

            // ① **先保存**：脏工程关闭时 Pro 会弹「是否保存更改？」⇒ 自动化场景下关闭会被取消（LIVE 实测
            //    句柄始终不释放）。保存动作必须派发到 UI 线程（Project.SaveAsync 访问 WPF DispatcherObject ——
            //    D-061 阶段二同源教训）。
            if (project.IsDirty)
            {
                try
                {
                    async Task<bool> SaveCore() => await project.SaveAsync();
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    var saved = (dispatcher is null || dispatcher.CheckAccess())
                        ? await SaveCore().ConfigureAwait(false)
                        : await dispatcher.InvokeAsync(SaveCore).Task.Unwrap().ConfigureAwait(false);
                    notes.Add("save=" + saved + "; dirtyAfterSave=" + project.IsDirty);
                }
                catch (Exception ex)
                {
                    notes.Add("save-failed=" + ex.Message);
                }
            }

            // ② 关闭工程。注：公开 SDK 对 Project.CloseAsync 标注 "DO NOT USE - WILL BE REMOVED"，
            //    但它是当前唯一可用的关闭入口 ⇒ 保留并**以句柄实测**为准（不臆测）。
            //    ★ D-064 阶段二真缺陷 ⑥ 修复：Pro 3.5 运行时的 CloseAsync **不是公开无参方法**
            //    （LIVE 实测 `GetMethod("CloseAsync", Type.EmptyTypes)` 返回 null ⇒ 旧实现静默"未找到"
            //    ⇒ 工程从未关闭 ⇒ 恢复恒失败，且失败原因被"未释放"掩盖）。改为：公开+非公开、任意重载，
            //    参数缺省值兜底，并**读回返回值**。
            var closeCandidates = project.GetType()
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance)
                .Where(m => m.Name == "CloseAsync" && m.GetParameters().All(p => p.ParameterType != typeof(string)))
                .OrderBy(m => m.GetParameters().Length)
                .ToList();

            if (closeCandidates.Count == 0)
            {
                notes.Add("closeAsync=no-usable-overload");
            }
            else
            {
                var closeMethod = closeCandidates[0];
                var closeArgs = closeMethod.GetParameters()
                    .Select(p => p.HasDefaultValue
                        ? p.DefaultValue
                        : p.ParameterType == typeof(bool)
                            ? (object)true
                            : p.ParameterType == typeof(CancellationToken)
                                ? CancellationToken.None
                                : null)
                    .ToArray();
                try
                {
                    // ★ 真缺陷 ⑥ 加固（第二轮）：CloseAsync 内部访问 WPF DispatcherObject ⇒ **必须派发到 UI 线程**
                    //   （LIVE 实测：后台线程直接调用抛「调用线程无法访问此对象，因为另一个线程拥有该对象」，
                    //   工程因此从未关闭、.aprx 句柄从未释放 —— 与 D-061 保存动作同源教训）。
                    async Task<object?> CloseCore()
                    {
                        var r = closeMethod.Invoke(project, closeArgs);
                        if (r is Task<bool> t)
                        {
                            return await t.ConfigureAwait(false);
                        }

                        if (r is Task plain)
                        {
                            await plain.ConfigureAwait(false);
                            return "invoked";
                        }

                        return r?.GetType().Name ?? "null";
                    }

                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    var outcome = (dispatcher is null || dispatcher.CheckAccess())
                        ? await CloseCore().ConfigureAwait(false)
                        : await dispatcher.InvokeAsync(CloseCore).Task.Unwrap().ConfigureAwait(false);
                    notes.Add("closeAsync=" + outcome);
                }
                catch (Exception ex)
                {
                    notes.Add("closeAsync-threw=" + (ex.InnerException?.Message ?? ex.Message));
                }
            }

            // ③ 句柄释放**实测**（N1：产物事实读回）：轮询独占打开 .aprx。
            var released = false;
            for (var i = 0; i < 20 && !released; i++)
            {
                try
                {
                    using (new FileStream(aprxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        released = true;
                    }
                }
                catch
                {
                    try
                    {
                        await Task.Delay(250, ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        break;
                    }
                }
            }

            notes.Add("handleReleased=" + released);
            return ArcGISProMCP.Core.Results.OperationResult<object?>.Ok(null, string.Join("; ", notes));
        }
        catch (Exception ex)
        {
            notes.Add("prepare-error=" + ex.Message);
            return ArcGISProMCP.Core.Results.OperationResult<object?>.Ok(null, string.Join("; ", notes));
        }
    }

    /// <summary>D-064：恢复后**重开工程**（使 UI 立即反映恢复态）。失败 ⇒ 如实说明（磁盘已逐字节恢复）。</summary>
    private static async Task<ArcGISProMCP.Core.Results.OperationResult<object?>> ReopenProjectAfterRestoreAsync(
        string aprxPath, CancellationToken ct)
    {
        var notes = new List<string>();
        try
        {
            var current = ArcGIS.Desktop.Core.Project.Current;
            if (current is not null
                && string.Equals(current.URI?.ToString(), aprxPath, StringComparison.OrdinalIgnoreCase))
            {
                return ArcGISProMCP.Core.Results.OperationResult<object?>.Ok(
                    null, "project already open from the restored .aprx");
            }

            var openMethod = typeof(ArcGIS.Desktop.Core.Project)
                .GetMethod("OpenAsync", new[] { typeof(string) });
            if (openMethod is null)
            {
                return ArcGISProMCP.Core.Results.OperationResult<object?>.Ok(
                    null, "Project.OpenAsync(string) is not present in this host build; reopen manually.");
            }

            // ★ 真缺陷 ⑥ 同源：OpenAsync 同样触碰 UI/窗格状态 ⇒ 派发到 UI 线程并**读回返回值**。
            async Task<object?> OpenCore()
            {
                var r = openMethod.Invoke(null, new object?[] { aprxPath });
                if (r is Task<bool> tb)
                {
                    return await tb.ConfigureAwait(false);
                }

                if (r is Task t)
                {
                    await t.ConfigureAwait(false);
                    return "invoked";
                }

                return r?.GetType().Name ?? "null";
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            var outcome = (dispatcher is null || dispatcher.CheckAccess())
                ? await OpenCore().ConfigureAwait(false)
                : await dispatcher.InvokeAsync(OpenCore).Task.Unwrap().ConfigureAwait(false);
            notes.Add("openAsync=" + outcome);

            // N1：**读回验证**（不臆测"已重开"；旧实现只 await ⇒ 失败也报成功 ⇒ 后续工具全 NRE）
            var opened = false;
            for (var i = 0; i < 40 && !opened; i++)
            {
                var cur = ArcGIS.Desktop.Core.Project.Current;
                if (cur is not null
                    && string.Equals(cur.URI?.ToString(), aprxPath, StringComparison.OrdinalIgnoreCase))
                {
                    opened = true;
                    break;
                }

                try
                {
                    await Task.Delay(500, ct).ConfigureAwait(false);
                }
                catch
                {
                    break;
                }
            }

            notes.Add("projectOpenVerified=" + opened);
            return ArcGISProMCP.Core.Results.OperationResult<object?>.Ok(null, string.Join("; ", notes));
        }
        catch (Exception ex)
        {
            notes.Add("reopen-error=" + (ex.InnerException?.Message ?? ex.Message));
            return ArcGISProMCP.Core.Results.OperationResult<object?>.Ok(null, string.Join("; ", notes));
        }
    }

    private static McpServer CreateServer()
    {
        var settings = Settings;
        var logger = ServerLogger;
        // D-083 B 组（A-13）：注入 PS 内部路由处理器——挂在同一 transport 的**同一监听**上（不新增监听/端口）。
        // 构造失败 ⇒ 保持 null（既有 /mcp 行为零变化，内部路由不可用）。
        ArcGISProMCP.Server.Transport.IInternalRouteHandler? internalPsRoute = null;
        try
        {
            internalPsRoute = new ArcGISProMCP.Server.Internal.PsChannelHandler(
                new ArcGISProMCP.Server.Internal.PsChannelPolicy(),
                new ArcGISProMCP.Server.Internal.PsSessionStore(),
                "1.0.2");
        }
        catch (Exception)
        {
            // 构造失败 ⇒ 内部路由不可用；既有 /mcp 行为零变化（不抛出）。
        }

        var transport = new HttpMcpTransport(settings.Host, settings.Port, settings.Endpoint, logger, internalPsRoute);
        return new McpServer(transport, Registry, BuildRouter(settings, logger), settings, logger);
    }
}
