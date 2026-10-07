using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Selection;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.TestSupport;

/// <summary>
/// 测试用 Fake ArcGIS Host。返回预置数据，用于在无 ArcGIS Pro 环境下
/// 测试 Tool → Router → Registry → Tool → Host → Service → Result 调用链。
/// </summary>
public sealed class FakeArcGISHost : IArcGISHost
{
    public FakeArcGISHost(
        IGeoprocessingService? geoprocessing = null,
        IRasterService? raster = null,
        IDataManagementService? data = null,
        IMapService? maps = null,
        ILayerService? layers = null,
        ISchemaService? schema = null,
        ISpatialReferenceService? spatialReferences = null,
        ILayoutService? layout = null,
        // D-061：工程与属性服务亦可注入（新工具 save_project / create_map / list_* / get_layer_features 需要）。
        IProjectService? project = null,
        IAttributeService? attributes = null,
        // D-062：编辑栈服务亦可注入（B 段 6 工具需要）。
        IEditService? edits = null,
        // D-064：diagnose 事实采集与项目快照服务亦可注入（G-166 差异化四件需要）。
        IDiagnosticsService? diagnostics = null,
        ISnapshotService? snapshots = null,
        ID084AnalysisService? d084Analysis = null,
        ID088QualityService? d088Quality = null,
        ID092DomainService? d092Domain = null,
        ID094AnalysisService? d094Analysis = null,
        ID096RasterService? d096Raster = null)
    {
        Maps = maps ?? new FakeMapService();
        Layers = layers ?? new FakeLayerService();
        Layout = layout ?? new NotImplementedService();
        Project = project ?? new FakeProjectService();
        License = new FakeLicenseService();
        Version = new FakeVersionService();
        Attributes = attributes ?? new FakeAttributeService();
        Edits = edits ?? new FakeEditService();
        SelectionState = new SelectionStateStore();
        SelectionRead = new FakeSelectionReadService(SelectionState);
        // 默认存在活动地图（空选择），保持 Phase 4 语义：clear_selection 成功。
        ((FakeSelectionReadService)SelectionRead).Current = new SelectionContent
        {
            MapName = "TestMap",
            MapUri = "map://test",
        };
        Selection = new FakeSelectionService(SelectionState, (FakeSelectionReadService)SelectionRead);
        Geoprocessing = geoprocessing ?? new FakeGeoprocessingService(SelectionState, (FakeSelectionReadService)SelectionRead);
        Raster = raster ?? new NotImplementedService();
        Data = data ?? new NotImplementedService();
        Schema = schema ?? new FakeSchemaService();
        SpatialReferences = spatialReferences ?? new FakeSpatialReferenceService();
        Diagnostics = diagnostics;
        Snapshots = snapshots;
        D084Analysis = d084Analysis;
        D088Quality = d088Quality;
        D092Domain = d092Domain;
        D094Analysis = d094Analysis;
        D096Raster = d096Raster;
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

    /// <summary>D-066：schema 处理器（注入测试用 SchemaInfo；null ⇒ 既有默认行为）。</summary>
    public Func<string, OperationResult<SchemaInfo>>? SchemaHandler
    {
        get => ((FakeSchemaService)Schema).SchemaHandler;
        set => ((FakeSchemaService)Schema).SchemaHandler = value;
    }

    /// <summary>D-079 · A2/A3：容器子项枚举处理器（注入 GDB 成员与行数；null ⇒ NOT_IMPLEMENTED 走接口默认）。</summary>
    public Func<string, int, OperationResult<IReadOnlyList<DatasetMemberInfo>>>? MembersHandler
    {
        get => ((FakeSchemaService)Schema).MembersHandler;
        set => ((FakeSchemaService)Schema).MembersHandler = value;
    }

    /// <summary>D-066：连接状态回读处理器（注入前后照证据；null ⇒ NotImplemented）。</summary>
    public Func<string?, string, OperationResult<JoinStateInfo>>? JoinStateHandler
    {
        get => ((FakeLayerService)Layers).JoinStateHandler;
        set => ((FakeLayerService)Layers).JoinStateHandler = value;
    }

    public ISpatialReferenceService SpatialReferences { get; }

    /// <summary>D-062 · B 段：编辑栈（可注入）。</summary>
    public IEditService Edits { get; }

    /// <summary>D-064 · G-166：diagnose 事实采集（可空 ⇒ 工具侧降级为 Core 自证事实集）。</summary>
    public IDiagnosticsService? Diagnostics { get; }

    /// <summary>D-064 · G-166：项目快照/恢复（可空 ⇒ 相关工具返回 NOT_IMPLEMENTED）。</summary>
    public ISnapshotService? Snapshots { get; }

    public ID084AnalysisService? D084Analysis { get; }

    public ID088QualityService? D088Quality { get; }

    public ID092DomainService? D092Domain { get; }

    public ID094AnalysisService? D094Analysis { get; }

    public ID096RasterService? D096Raster { get; }
}

/// <summary>
/// D-062 · FakeEditService：可脚本化的编辑栈替身。
/// 行为钩子：InsertResult/UpdateResult/DeleteResult/SaveResult/DiscardResult/SessionResult 可按需替换；
/// 记录调用（Calls）供断言；默认走「成功 + 计数累计」的最小语义（save/discard 归零）。
/// </summary>
public sealed class FakeEditService : IEditService
{
    public List<string> Calls { get; } = new();
    public long Pending { get; private set; }
    public List<string> Affected { get; } = new();

    public OperationResult<EditOpResult>? InsertResult { get; set; }
    public string? LastInsertMap { get; private set; }
    public OperationResult<EditOpResult>? UpdateResult { get; set; }
    public OperationResult<EditOpResult>? DeleteResult { get; set; }
    public OperationResult<EditSessionState>? SaveResult { get; set; }
    public OperationResult<EditSessionState>? DiscardResult { get; set; }
    public OperationResult<EditSessionState>? SessionResult { get; set; }

    public Task<OperationResult<EditOpResult>> InsertFeaturesAsync(
        string? mapName, string layerName, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, CancellationToken ct = default)
    {
        Calls.Add($"insert:{layerName}:{rows?.Count ?? 0}");
        LastInsertMap = mapName;
        if (InsertResult is not null)
        {
            return Task.FromResult(InsertResult);
        }

        Pending += rows?.Count ?? 0;
        Track(layerName);
        return Task.FromResult(OperationResult<EditOpResult>.Ok(new EditOpResult
        {
            Action = "insert",
            LayerName = layerName,
            Executed = true,
            RowsAffected = rows?.Count ?? 0,
            PendingChangeCount = Pending,
        }));
    }

    public Task<OperationResult<EditOpResult>> UpdateFeaturesAsync(
        string? mapName, string layerName, string? where, IReadOnlyList<long>? oidList,
        IReadOnlyDictionary<string, object?>? attributes, string? geometryWkt, bool confirm, CancellationToken ct = default)
    {
        Calls.Add($"update:{layerName}:where={where}:oids={oidList?.Count ?? 0}:confirm={confirm}:wkt={(geometryWkt is null ? "no" : "yes")}");
        if (UpdateResult is not null)
        {
            return Task.FromResult(UpdateResult);
        }

        var n = oidList?.Count ?? 0;
        Pending += n;
        Track(layerName);
        return Task.FromResult(OperationResult<EditOpResult>.Ok(new EditOpResult
        {
            Action = "update",
            LayerName = layerName,
            Executed = true,
            RowsAffected = n,
            PendingChangeCount = Pending,
        }));
    }

    public Task<OperationResult<EditOpResult>> DeleteFeaturesAsync(
        string? mapName, string layerName, string? where, IReadOnlyList<long>? oidList,
        bool confirm, CancellationToken ct = default)
    {
        Calls.Add($"delete:{layerName}:where={where}:oids={oidList?.Count ?? 0}:confirm={confirm}");
        if (DeleteResult is not null)
        {
            return Task.FromResult(DeleteResult);
        }

        var n = oidList?.Count ?? 0;
        Pending += n;
        Track(layerName);
        return Task.FromResult(OperationResult<EditOpResult>.Ok(new EditOpResult
        {
            Action = "delete",
            LayerName = layerName,
            Executed = true,
            RowsAffected = n,
            PendingChangeCount = Pending,
        }));
    }

    public Task<OperationResult<EditSessionState>> SaveEditsAsync(CancellationToken ct = default)
    {
        Calls.Add("save");
        if (SaveResult is not null)
        {
            return Task.FromResult(SaveResult);
        }

        Pending = 0;
        Affected.Clear();
        return Task.FromResult(OperationResult<EditSessionState>.Ok(new EditSessionState { HasEdits = false }));
    }

    public Task<OperationResult<EditSessionState>> DiscardEditsAsync(CancellationToken ct = default)
    {
        Calls.Add("discard");
        if (DiscardResult is not null)
        {
            return Task.FromResult(DiscardResult);
        }

        Pending = 0;
        Affected.Clear();
        return Task.FromResult(OperationResult<EditSessionState>.Ok(new EditSessionState { HasEdits = false }));
    }

    public Task<OperationResult<EditSessionState>> GetEditSessionAsync(CancellationToken ct = default)
    {
        Calls.Add("session");
        if (SessionResult is not null)
        {
            return Task.FromResult(SessionResult);
        }

        return Task.FromResult(OperationResult<EditSessionState>.Ok(new EditSessionState
        {
            HasEdits = Pending > 0,
            PendingChangeCount = Pending,
            AffectedLayers = Affected.ToArray(),
        }));
    }

    private void Track(string? layerName)
    {
        if (!string.IsNullOrEmpty(layerName) && !Affected.Contains(layerName))
        {
            Affected.Add(layerName);
        }
    }
}

internal sealed class FakeSchemaService : ISchemaService
{
    /// <summary>D-066：schema 处理器（未配置 ⇒ 走既有默认行为）。</summary>
    public Func<string, OperationResult<SchemaInfo>>? SchemaHandler { get; set; }

    /// <summary>D-079 · A2/A3：容器子项枚举处理器（未配置 ⇒ 走接口默认 NOT_IMPLEMENTED）。</summary>
    public Func<string, int, OperationResult<IReadOnlyList<DatasetMemberInfo>>>? MembersHandler { get; set; }

    public Task<OperationResult<SchemaInfo>> GetSchemaInfoAsync(string path, CancellationToken ct = default)
    {
        if (SchemaHandler is not null)
        {
            return Task.FromResult(SchemaHandler(path));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(OperationResult<SchemaInfo>.Fail(ErrorCodes.InvalidArgument, "path is required."));
        }

        return Task.FromResult(OperationResult<SchemaInfo>.Ok(new SchemaInfo
        {
            Path = path,
            Exists = true,
            DataType = "FeatureClass",
            GeometryType = "Point",
            SpatialReference = "GCS_WGS_1984",
            Fields = new List<SchemaFieldInfo>
            {
                new() { Name = "OBJECTID", Alias = "OBJECTID", Type = "OID", IsNullable = false, Length = 4 },
                new() { Name = "NAME", Alias = "NAME", Type = "String", IsNullable = true, Length = 64 },
            },
        }));
    }

    public Task<OperationResult<IReadOnlyList<DomainInfo>>> GetDomainsAsync(string workspace, int maxItems, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workspace))
        {
            return Task.FromResult(OperationResult<IReadOnlyList<DomainInfo>>.Fail(ErrorCodes.InvalidArgument, "workspace is required."));
        }

        return Task.FromResult(OperationResult<IReadOnlyList<DomainInfo>>.Ok(new List<DomainInfo>
        {
            new() { Name = "Category_D", Type = "CodedValue", CodedValues = new Dictionary<string, string> { ["A"] = "Alpha", ["B"] = "Beta" } }
        }));
    }

    public Task<OperationResult<SubtypeInfo>> GetSubtypesAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(OperationResult<SubtypeInfo>.Fail(ErrorCodes.InvalidArgument, "path is required."));
        }

        return Task.FromResult(OperationResult<SubtypeInfo>.Ok(new SubtypeInfo
        {
            Path = path,
            Exists = true,
            SubtypeField = "CATEGORY",
            Subtypes = new Dictionary<string, string> { ["1"] = "First", ["2"] = "Second" },
        }));
    }

    public Task<OperationResult<IReadOnlyList<IndexInfo>>> GetIndexesAsync(string path, int maxItems, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(OperationResult<IReadOnlyList<IndexInfo>>.Fail(ErrorCodes.InvalidArgument, "path is required."));
        }

        return Task.FromResult(OperationResult<IReadOnlyList<IndexInfo>>.Ok(new List<IndexInfo>
        {
            new() { Name = "IX_NAME", Fields = new List<string> { "NAME" }, IsUnique = false, IsSpatial = false }
        }));
    }

    public Task<OperationResult<IReadOnlyList<DatasetMemberInfo>>> ListDatasetMembersAsync(
        string containerPath, int maxItems, CancellationToken ct = default)
        // 注意：接口默认实现**不能**从实现类内部经接口转型调用（会重派回本方法 ⇒ 无限递归）⇒ 直接给同语义结果。
        => Task.FromResult(MembersHandler is not null
            ? MembersHandler(containerPath, maxItems)
            : OperationResult<IReadOnlyList<DatasetMemberInfo>>.Fail(
                ErrorCodes.NotImplemented, "Container member enumeration is not implemented by this host."));
}

public sealed class FakeAttributeService : IAttributeService
{
    public Task<OperationResult<IReadOnlyList<FeatureInfo>>> QueryFeaturesAsync(AttributeQueryRequest request, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<FeatureInfo>>.Ok(new List<FeatureInfo>
        {
            new() { Oid = 1, Attributes = new Dictionary<string, object?> { ["NAME"] = "A", ["POP"] = 100L } }
        }));

    public Task<OperationResult<IReadOnlyList<FieldInfo>>> GetFieldInfoAsync(string mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<FieldInfo>>.Ok(new List<FieldInfo>
        {
            new() { Name = "NAME", Alias = "NAME", TypeName = "String", Length = 255 }
        }));

    public Task<OperationResult<long>> GetFeatureCountAsync(string mapName, string layerName, string? whereClause = null, CancellationToken ct = default)
        => Task.FromResult(OperationResult<long>.Ok(5));

    public int? LastMaxDistinct { get; private set; }

    // ── D-062 · C 段脚本化 ──
    public OperationResult<FieldStatisticsInfo>? StatsResult { get; set; }
    public OperationResult<GroupSummaryInfo>? SummaryResult { get; set; }
    public (string? Map, string Layer, string Field, string? Where)? LastStatsCall { get; private set; }
    public (string? Map, string Layer, IReadOnlyList<string> Groups, string? AggField, IReadOnlyList<string> Aggs, string? Where, int TopN)? LastSummaryCall { get; private set; }

    public Task<OperationResult<FieldStatisticsInfo>> GetFieldStatisticsAsync(
        string? mapName, string layerName, string fieldName, string? whereClause = null, CancellationToken ct = default)
    {
        LastStatsCall = (mapName, layerName, fieldName, whereClause);
        return Task.FromResult(StatsResult ?? OperationResult<FieldStatisticsInfo>.Fail(
            ErrorCodes.NotImplemented, "StatsResult not scripted in fake."));
    }

    public Task<OperationResult<GroupSummaryInfo>> SummarizeFeaturesAsync(
        string? mapName, string layerName, IReadOnlyList<string> groupByFields, string? aggField,
        IReadOnlyList<string> aggregations, string? whereClause = null, int topN = 0, CancellationToken ct = default)
    {
        LastSummaryCall = (mapName, layerName, groupByFields, aggField, aggregations, whereClause, topN);
        return Task.FromResult(SummaryResult ?? OperationResult<GroupSummaryInfo>.Fail(
            ErrorCodes.NotImplemented, "SummaryResult not scripted in fake."));
    }

    public Task<OperationResult<FieldValuesInfo>> GetFieldValuesAsync(
        string mapName, string layerName, string fieldName, int maxDistinct = 200, CancellationToken ct = default)
    {
        LastMaxDistinct = maxDistinct;
        return Task.FromResult(OperationResult<FieldValuesInfo>.Ok(new FieldValuesInfo
        {
            MapName = mapName,
            LayerName = layerName,
            FieldName = fieldName,
            TotalCount = 5,
            NonNullCount = 5,
            DistinctCount = 5,
            Truncated = false,
            DistinctValues = new List<string> { "A", "B", "C", "D", "E" },
            MinValue = "A",
            MaxValue = "E",
        }));
    }
}

/// <summary>
/// Fake 选择服务：clear_selection 镜像真实编排（账本核对→自动快照→清空→提交），
/// 与 SelectionRead.Current（模拟地图状态）联动。Phase 8.3 (D-013)。
/// </summary>
internal sealed class FakeSelectionService : ISelectionService
{
    private readonly ISelectionStateStore _store;
    private readonly FakeSelectionReadService _read;

    public FakeSelectionService(ISelectionStateStore store, FakeSelectionReadService read)
    {
        _store = store;
        _read = read;
    }

    public Task<OperationResult<SelectionInfo>> GetSelectionAsync(string? mapName = null, CancellationToken ct = default)
        => Task.FromResult(OperationResult<SelectionInfo>.Ok(new SelectionInfo { MapName = "TestMap", LayerName = "Roads", Count = 3 }));

    public Task<OperationResult<bool>> ClearSelectionAsync(string? mapName = null, CancellationToken ct = default)
    {
        if (_read.Current is null)
        {
            return Task.FromResult(OperationResult<bool>.Fail(ErrorCodes.NoActiveView, "No active map view is available."));
        }

        var before = Clone(_read.Current);
        var check = _store.CheckWritePrecondition(before.MapUri, before);
        if (!check.Matched)
        {
            return Task.FromResult(OperationResult<bool>.Fail(
                ErrorCodes.SelectionBaselineMismatch, check.DiffSummary ?? "selection baseline mismatch."));
        }

        _store.AddSnapshot(before.MapUri, before.MapName, before, check.Revision);

        // 模拟清空：保留地图身份，清掉图层选择。
        var after = new SelectionContent { MapName = before.MapName, MapUri = before.MapUri };
        _read.Current = after;
        _store.CommitWrite(after.MapUri, after, "clear_selection");
        return Task.FromResult(OperationResult<bool>.Ok(true));
    }

    public Task<OperationResult<SelectionInfo>> SelectLayerAsync(string mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<SelectionInfo>.Ok(new SelectionInfo { MapName = mapName, LayerName = layerName, Count = 0 }));

    private static SelectionContent Clone(SelectionContent c) => new()
    {
        MapName = c.MapName,
        MapUri = c.MapUri,
        Layers = c.Layers
            .Select(l => new SelectionLayerContent
            {
                LayerName = l.LayerName,
                LayerUri = l.LayerUri,
                Oids = l.Oids.ToList(),
                SelectedCount = l.SelectedCount,
                Truncated = l.Truncated,
            })
            .ToList(),
    };
}

internal sealed class FakeMapService : IMapService
{
    public Task<OperationResult<MapInfo?>> GetCurrentMapAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapInfo?>.Ok(new MapInfo { Name = "TestMap", Uri = "file:///C:/test.aprx", Kind = "Map" }));

    public Task<OperationResult<IReadOnlyList<MapInfo>>> GetMapsAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<MapInfo>>.Ok(new List<MapInfo>
        {
            new() { Name = "TestMap", Kind = "Map" },
            new() { Name = "AnalysisMap", Kind = "Map" }
        }));

    public Task<OperationResult<MapExtentInfo>> GetMapExtentAsync(string? mapName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapExtentInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    public Task<OperationResult<MapExtentSetInfo>> SetMapExtentAsync(
        string? mapName, double xMin, double yMin, double xMax, double yMax, string? spatialReference,
        CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapExtentSetInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));
}

public sealed class FakeLayerService : ILayerService
{
    /// <summary>D-066：连接状态回读处理器（未配置 ⇒ NotImplemented，保持历史行为）。</summary>
    public Func<string?, string, OperationResult<JoinStateInfo>>? JoinStateHandler { get; set; }
    public Func<string?, string, string?, string?, double?, string?, bool?, OperationResult<object?>>? SetLabelPropertiesHandler { get; set; }
    public IReadOnlyList<string> RasterSourcePaths { get; set; } = Array.Empty<string>();
    public int AddLayerCallCount { get; private set; }

    public Task<OperationResult<IReadOnlyList<string>>> GetRasterSourcePathsAsync(
        string? mapName = null, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<string>>.Ok(RasterSourcePaths));

    /// <inheritdoc />
    public Task<OperationResult<JoinStateInfo>> GetJoinStateAsync(string? mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(JoinStateHandler is not null
            ? JoinStateHandler(mapName, layerName)
            : OperationResult<JoinStateInfo>.Fail(ErrorCodes.NotImplemented, "join state probe not configured"));

    public Task<OperationResult<IReadOnlyList<LayerInfo>>> GetLayersAsync(string? mapName = null, bool flatten = true, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<LayerInfo>>.Ok(new List<LayerInfo>
        {
            new() { Name = "Roads", LayerType = "FeatureLayer", IsVisible = true, MapName = "TestMap" },
            new() { Name = "Boundaries", LayerType = "FeatureLayer", IsVisible = true, MapName = "TestMap" }
        }));

    public Task<OperationResult<LayerInfo?>> FindLayerAsync(string mapName, string layerName, CancellationToken ct = default)
    {
        if (layerName == "Roads")
        {
            return Task.FromResult(OperationResult<LayerInfo?>.Ok(new LayerInfo { Name = "Roads", LayerType = "FeatureLayer", MapName = mapName }));
        }

        return Task.FromResult(OperationResult<LayerInfo?>.Fail(ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found."));
    }

    public Task<OperationResult<LayerInfo?>> GetLayerInfoAsync(string mapName, string layerName, CancellationToken ct = default)
        => FindLayerAsync(mapName, layerName, ct);

    public Task<OperationResult<bool>> SetLayerVisibilityAsync(string mapName, string layerName, bool visible, CancellationToken ct = default)
        => Task.FromResult(OperationResult<bool>.Ok(true));

    public Task<OperationResult<LayerInfo?>> AddLayerAsync(string mapName, string layerPathOrUri, CancellationToken ct = default)
    {
        AddLayerCallCount++;
        return Task.FromResult(OperationResult<LayerInfo?>.Ok(new LayerInfo { Name = "AddedLayer", LayerType = "FeatureLayer", MapName = mapName }));
    }

    public Task<OperationResult<bool>> RemoveLayerAsync(string mapName, string layerName, CancellationToken ct = default)
    {
        if (layerName == "Roads")
        {
            return Task.FromResult(OperationResult<bool>.Ok(true));
        }

        return Task.FromResult(OperationResult<bool>.Fail(ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found."));
    }

    // D-046：可注入钩子（未设置 → NOT_IMPLEMENTED）
    public Func<string?, string, OperationResult<LayerSymbologyInfo>>? SymbologyHook { get; set; }

    public Func<string?, string, string?, string?, double?, double?, OperationResult<SymbologySetInfo>>? SymbologySetHook { get; set; }

    public Func<string?, string, OperationResult<LabelInfo>>? LabelHook { get; set; }

    public Func<string?, string, bool, OperationResult<LabelVisibilityInfo>>? LabelVisibilityHook { get; set; }

    public Task<OperationResult<LayerSymbologyInfo>> GetLayerSymbologyAsync(string? mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(SymbologyHook is null
            ? OperationResult<LayerSymbologyInfo>.Fail(ErrorCodes.NotImplemented, "not implemented")
            : SymbologyHook(mapName, layerName));

    public Task<OperationResult<SymbologySetInfo>> SetSimpleSymbologyAsync(string? mapName, string layerName, string? fillColor, string? outlineColor, double? pointSize, double? lineWidth, CancellationToken ct = default)
        => Task.FromResult(SymbologySetHook is null
            ? OperationResult<SymbologySetInfo>.Fail(ErrorCodes.NotImplemented, "not implemented")
            : SymbologySetHook(mapName, layerName, fillColor, outlineColor, pointSize, lineWidth));

    public Task<OperationResult<LabelInfo>> GetLabelInfoAsync(string? mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(LabelHook is null
            ? OperationResult<LabelInfo>.Fail(ErrorCodes.NotImplemented, "not implemented")
            : LabelHook(mapName, layerName));

    public Task<OperationResult<LabelVisibilityInfo>> SetLabelVisibilityAsync(string? mapName, string layerName, bool enabled, CancellationToken ct = default)
        => Task.FromResult(LabelVisibilityHook is null
            ? OperationResult<LabelVisibilityInfo>.Fail(ErrorCodes.NotImplemented, "not implemented")
            : LabelVisibilityHook(mapName, layerName, enabled));

    public Task<OperationResult<object?>> SetLabelPropertiesAsync(
        string? mapName, string layerName, string? expression, string? fontFamily, double? fontSize,
        string? placement, bool? visible, CancellationToken ct = default)
        => Task.FromResult(SetLabelPropertiesHandler is null
            ? OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "not implemented")
            : SetLabelPropertiesHandler(mapName, layerName, expression, fontFamily, fontSize, placement, visible));

    public Task<OperationResult<DefinitionQueryInfo>> GetDefinitionQueryAsync(string? mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<DefinitionQueryInfo>.Ok(new DefinitionQueryInfo
        {
            LayerName = layerName,
            SupportsDefinitionQuery = true,
            DefinitionQuery = string.Empty,
        }));


    public Task<OperationResult<DefinitionQuerySetInfo>> SetDefinitionQueryAsync(
        string? mapName, string layerName, string? definitionQuery, CancellationToken ct = default)
        => Task.FromResult(OperationResult<DefinitionQuerySetInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    public Task<OperationResult<LayerOrderInfo>> MoveLayerAsync(
        string? mapName, string layerName, string? referenceLayer, string position, CancellationToken ct = default)
        => Task.FromResult(OperationResult<LayerOrderInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));


}

internal sealed class FakeProjectService : IProjectService
{
    public Task<OperationResult<ProjectInfo>> GetProjectInfoAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<ProjectInfo>.Ok(new ProjectInfo { Name = "TestProject", Path = "C:/test.aprx", IsDirty = false }));

    public Task<OperationResult<IReadOnlyList<LayoutInfo>>> ListLayoutsAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<LayoutInfo>>.Ok(new List<LayoutInfo> { new() { Name = "Layout1" } }));

    public Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListDatabasesAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<DatasetInfo>>.Ok(new List<DatasetInfo> { new() { Name = "TestData.gdb", Path = "C:/TestData.gdb", Type = "FileGeodatabase" } }));
}

internal sealed class FakeLicenseService : ILicenseService
{
    public Task<OperationResult<LicensingInfo>> GetLicenseInfoAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<LicensingInfo>.Ok(new LicensingInfo { LicenseLevel = "Advanced" }));

    public Task<OperationResult<bool>> CheckExtensionAsync(string extensionCode, CancellationToken ct = default)
        => Task.FromResult(OperationResult<bool>.Ok(true));

    public Task<OperationResult<bool>> RequireExtensionAsync(string extensionCode, CancellationToken ct = default)
    {
        if (extensionCode == "NotLicensed")
        {
            return Task.FromResult(OperationResult<bool>.Fail(ErrorCodes.LicenseRequired, "required"));
        }

        return Task.FromResult(OperationResult<bool>.Ok(true));
    }
}

internal sealed class FakeVersionService : IArcGISVersionService
{
    public Task<OperationResult<ArcGISVersionInfo>> GetVersionAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<ArcGISVersionInfo>.Ok(new ArcGISVersionInfo { ProductVersion = "3.5.0", Major = 3, Minor = 5, Build = 57366, TargetFramework = "net6.0" }));
}

public sealed class NotImplementedService :
    IAttributeService, ISelectionService, IGeoprocessingService, IRasterService, IDataManagementService, ILayoutService
{
    private static OperationResult<T> Missing<T>() => OperationResult<T>.Fail(ErrorCodes.NotImplemented, "not implemented");

    public Func<string, string, IReadOnlyDictionary<string, object?>, string, OperationResult<object?>>? SetElementPropertiesHook { get; set; }
    public Func<string, string, string?, string, string?, OperationResult<object?>>? ConfigureMapSeriesHook { get; set; }
    public Func<string, OperationResult<LayoutDetailInfo>>? GetLayoutInfoHook { get; set; }
    public Func<string, OperationResult<IReadOnlyList<LayoutElementInfo>>>? ListLayoutElementsHook { get; set; }
    public IReadOnlyList<string> RasterSourcePaths { get; set; } = Array.Empty<string>();
    public int RasterSourcePathCallCount { get; private set; }
    public int CreateLayoutCallCount { get; private set; }
    public int ExportLayoutCallCount { get; private set; }

    public Task<OperationResult<IReadOnlyList<string>>> GetRasterSourcePathsAsync(
        string layoutName, CancellationToken ct = default)
    {
        RasterSourcePathCallCount++;
        return Task.FromResult(OperationResult<IReadOnlyList<string>>.Ok(RasterSourcePaths));
    }

    // D-021：本替身不承载覆写判定语义，存在性探测一律 NOT_IMPLEMENTED（既有用例如依赖 GP 行为不受影响）。
    public Task<OperationResult<OutputExistence>> CheckOutputExistsAsync(string outputPath, CancellationToken ct = default)
        => Task.FromResult(Missing<OutputExistence>());

    public Task<OperationResult<IReadOnlyList<FeatureInfo>>> QueryFeaturesAsync(AttributeQueryRequest request, CancellationToken ct = default)
        => Task.FromResult(Missing<IReadOnlyList<FeatureInfo>>());

    public Task<OperationResult<IReadOnlyList<FieldInfo>>> GetFieldInfoAsync(string mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(Missing<IReadOnlyList<FieldInfo>>());

    public Task<OperationResult<FieldValuesInfo>> GetFieldValuesAsync(
        string mapName, string layerName, string fieldName, int maxDistinct = 200, CancellationToken ct = default)
        => Task.FromResult(Missing<FieldValuesInfo>());

    public Task<OperationResult<long>> GetFeatureCountAsync(string mapName, string layerName, string? whereClause = null, CancellationToken ct = default)
        => Task.FromResult(Missing<long>());

    public Task<OperationResult<SelectionInfo>> GetSelectionAsync(string? mapName = null, CancellationToken ct = default)
        => Task.FromResult(Missing<SelectionInfo>());

    public Task<OperationResult<bool>> ClearSelectionAsync(string? mapName = null, CancellationToken ct = default)
        => Task.FromResult(OperationResult<bool>.Ok(true));

    public Task<OperationResult<SelectionInfo>> SelectLayerAsync(string mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(Missing<SelectionInfo>());

    public Task<OperationResult<GeoprocessingResult>> RunToolAsync(GeoprocessingRequest request, CancellationToken ct = default)
        => Task.FromResult(Missing<GeoprocessingResult>());

    public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByAttributeAsync(string? mapName, string layerName, string mode, System.Collections.Generic.IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default)
        => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByLocationAsync(string? mapName, string layerName, string? selectingLayerName, string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default)
        => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    public Task<OperationResult<RasterInfo>> GetRasterInfoAsync(string datasetPath, CancellationToken ct = default)
        => Task.FromResult(Missing<RasterInfo>());

    public Task<OperationResult<DatasetInfo>> GetDatasetInfoAsync(string path, CancellationToken ct = default)
        => Task.FromResult(Missing<DatasetInfo>());

    public Task<OperationResult<IReadOnlyList<LayoutInfo>>> GetLayoutsAsync(CancellationToken ct = default)
        => Task.FromResult(Missing<IReadOnlyList<LayoutInfo>>());

    public Task<OperationResult<LayoutDetailInfo>> GetLayoutInfoAsync(string layoutName, CancellationToken ct = default)
        => Task.FromResult(GetLayoutInfoHook?.Invoke(layoutName) ?? Missing<LayoutDetailInfo>());

    public Task<OperationResult<IReadOnlyList<LayoutElementInfo>>> ListLayoutElementsAsync(string layoutName, CancellationToken ct = default)
        => Task.FromResult(ListLayoutElementsHook?.Invoke(layoutName) ?? Missing<IReadOnlyList<LayoutElementInfo>>());

    // D-045：布局构建（本替身一律 NOT_IMPLEMENTED）
    // D-047：导出（可注入钩子；未设置 → NOT_IMPLEMENTED）
    public Func<string, string, string, double?, bool, OperationResult<LayoutExportInfo>>? ExportLayoutHook { get; set; }

    public Task<OperationResult<LayoutExportInfo>> ExportLayoutAsync(
        string layoutName, string outputPath, string format, double? resolution, bool overwrite,
        CancellationToken ct = default)
    {
        ExportLayoutCallCount++;
        return Task.FromResult(ExportLayoutHook is null
            ? Missing<LayoutExportInfo>()
            : ExportLayoutHook(layoutName, outputPath, format, resolution, overwrite));
    }

    public Func<string, string, string, double?, bool, bool?, OperationResult<LayoutExportInfo>>? ExportLayoutWithOptionsHook { get; set; }

    public Task<OperationResult<LayoutExportInfo>> ExportLayoutWithOptionsAsync(
        string layoutName, string outputPath, string format, double? resolution, bool overwrite,
        bool? transparentBackground, CancellationToken ct = default)
        => Task.FromResult(ExportLayoutWithOptionsHook is not null
            ? ExportLayoutWithOptionsHook(layoutName, outputPath, format, resolution, overwrite, transparentBackground)
            : transparentBackground is null
                ? ExportLayoutHook is null
                    ? Missing<LayoutExportInfo>()
                    : ExportLayoutHook(layoutName, outputPath, format, resolution, overwrite)
                : Missing<LayoutExportInfo>());

    public Task<OperationResult<LayoutCreateInfo>> CreateLayoutAsync(
        string name, double pageWidth, double pageHeight, string pageUnits, string? mapName,
        string? mapFrameName, double? frameXMin, double? frameYMin, double? frameXMax, double? frameYMax,
        CancellationToken ct = default)
    {
        CreateLayoutCallCount++;
        return Task.FromResult(Missing<LayoutCreateInfo>());
    }

    public Task<OperationResult<LayoutElementAddInfo>> AddLayoutTextAsync(
        string layoutName, string text, double x, double y, double? fontSize, string? fontFamily,
        string? elementName, CancellationToken ct = default)
        => Task.FromResult(Missing<LayoutElementAddInfo>());

    public Task<OperationResult<LayoutElementAddInfo>> AddLegendAsync(
        string layoutName, string mapFrameName, double x, double y, double? width, double? height,
        string? elementName, CancellationToken ct = default)
        => Task.FromResult(Missing<LayoutElementAddInfo>());

    public Task<OperationResult<LayoutElementAddInfo>> AddNorthArrowAsync(
        string layoutName, string mapFrameName, double x, double y, double? width, double? height,
        string? elementName, CancellationToken ct = default)
        => Task.FromResult(Missing<LayoutElementAddInfo>());

    public Task<OperationResult<LayoutElementAddInfo>> AddScaleBarAsync(
        string layoutName, string mapFrameName, double x, double y, double? width, double? height,
        string? elementName, CancellationToken ct = default)
        => Task.FromResult(Missing<LayoutElementAddInfo>());

    public Task<OperationResult<object?>> SetElementPropertiesAsync(
        string layoutName, string elementId, IReadOnlyDictionary<string, object?> properties, string units, CancellationToken ct = default)
        => Task.FromResult(SetElementPropertiesHook is null
            ? Missing<object?>()
            : SetElementPropertiesHook(layoutName, elementId, properties, units));

    public Task<OperationResult<object?>> ConfigureMapSeriesAsync(
        string mapName, string indexField, string? sortField, string extentSource, string? nameField, CancellationToken ct = default)
        => Task.FromResult(ConfigureMapSeriesHook is null
            ? Missing<object?>()
            : ConfigureMapSeriesHook(mapName, indexField, sortField, extentSource, nameField));
}
