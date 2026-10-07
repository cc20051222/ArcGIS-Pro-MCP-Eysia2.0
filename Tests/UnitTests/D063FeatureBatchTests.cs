using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Protocol;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-063 功能完善第三批单测：图层管理（10）+ 渲染进阶（4 新增 + 1 增强）+ 视图书签与回图（7）。
/// 判据：每件 ≥8 例，合计 ≥168。
/// </summary>
public class D063FeatureBatchTests
{
    // ══════════════════ 测试替身（脚本化；D063 方法可注入结果）══════════════════

    private sealed class ScriptedLayerService : ILayerService
    {
        public readonly Dictionary<string, object?> Results = new(StringComparer.Ordinal);
        public readonly List<string> Calls = new();

        public Task<OperationResult<IReadOnlyList<LayerInfo>>> GetLayersAsync(string? mapName = null, bool flatten = true, CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<LayerInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));
        // ── D-066：FindLayer 可脚本化（未脚本 ⇒ 保持历史 NotImplemented）──
        public OperationResult<LayerInfo?>? FindResult { get; set; }
        public Queue<OperationResult<JoinStateInfo>> JoinStates { get; } = new();
        public Task<OperationResult<LayerInfo?>> FindLayerAsync(string mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(FindResult ?? OperationResult<LayerInfo?>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<JoinStateInfo>> GetJoinStateAsync(string? mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(JoinStates.Count > 0
                ? JoinStates.Dequeue()
                : OperationResult<JoinStateInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<LayerInfo?>> GetLayerInfoAsync(string mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerInfo?>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<bool>> SetLayerVisibilityAsync(string mapName, string layerName, bool visible, CancellationToken ct = default)
            => Task.FromResult(OperationResult<bool>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<LayerInfo?>> AddLayerAsync(string mapName, string layerPathOrUri, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerInfo?>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<bool>> RemoveLayerAsync(string mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<bool>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<DefinitionQueryInfo>> GetDefinitionQueryAsync(string? mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<DefinitionQueryInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<DefinitionQuerySetInfo>> SetDefinitionQueryAsync(string? mapName, string layerName, string? definitionQuery, CancellationToken ct = default)
            => Task.FromResult(OperationResult<DefinitionQuerySetInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<LayerOrderInfo>> MoveLayerAsync(string? mapName, string layerName, string? referenceLayer, string position, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerOrderInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<LayerSymbologyInfo>> GetLayerSymbologyAsync(string? mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerSymbologyInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<SymbologySetInfo>> SetSimpleSymbologyAsync(string? mapName, string layerName, string? fillColor, string? outlineColor, double? pointSize, double? lineWidth, CancellationToken ct = default)
            => Task.FromResult(OperationResult<SymbologySetInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<LabelInfo>> GetLabelInfoAsync(string? mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LabelInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<LabelVisibilityInfo>> SetLabelVisibilityAsync(string? mapName, string layerName, bool enabled, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LabelVisibilityInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        // ── D-063 覆写 ──
        public Task<OperationResult<LayerAppearanceInfo>> SetLayerAppearanceAsync(string? mapName, string layerName, double? transparency, double? minScale, double? maxScale, CancellationToken ct = default)
        { Calls.Add($"appearance:{mapName}:{layerName}:{transparency}:{minScale}:{maxScale}"); return R<LayerAppearanceInfo>("appearance"); }
        public Task<OperationResult<GroupLayerInfo>> CreateGroupLayerAsync(string? mapName, string groupName, IReadOnlyList<string>? layerNames, CancellationToken ct = default)
        { Calls.Add($"group:{mapName}:{groupName}:{layerNames?.Count ?? 0}"); return R<GroupLayerInfo>("group"); }
        public Task<OperationResult<BasemapInfo>> SetBasemapAsync(string? mapName, string basemap, CancellationToken ct = default)
        { Calls.Add($"basemap:{mapName}:{basemap}"); return R<BasemapInfo>("basemap"); }
        public Task<OperationResult<BrokenLayersInfo>> GetBrokenLayersAsync(CancellationToken ct = default)
        { Calls.Add("broken"); return R<BrokenLayersInfo>("broken"); }
        public Task<OperationResult<RepairLayerInfo>> RepairLayerSourceAsync(string? mapName, string layerName, string? newWorkspacePath, string? newDatasetName, CancellationToken ct = default)
        { Calls.Add($"repair:{layerName}:{newWorkspacePath}:{newDatasetName}"); return R<RepairLayerInfo>("repair"); }
        // O-D066-05：AddJoinAsync/RemoveJoinAsync 已随接口删除（受控 GP 代理取代）。
        public Task<OperationResult<RenameLayerInfo>> RenameLayerAsync(string? mapName, string layerName, string newName, CancellationToken ct = default)
        { Calls.Add($"rename:{layerName}:{newName}"); return R<RenameLayerInfo>("rename"); }
        public Task<OperationResult<DuplicateLayerInfo>> DuplicateLayerAsync(string? mapName, string layerName, string? newName, CancellationToken ct = default)
        { Calls.Add($"duplicate:{layerName}:{newName}"); return R<DuplicateLayerInfo>("duplicate"); }
        public Task<OperationResult<LayerRendererInfo>> SetLayerRendererAsync(string? mapName, string layerName, string mode, string? field, int? classCount, string? colorRamp, CancellationToken ct = default)
        { Calls.Add($"renderer:{layerName}:{mode}:{field}:{classCount}:{colorRamp}"); return R<LayerRendererInfo>("renderer"); }
        public Task<OperationResult<ColorRampsInfo>> ListColorRampsAsync(CancellationToken ct = default)
        { Calls.Add("ramps"); return R<ColorRampsInfo>("ramps"); }
        public Task<OperationResult<LayerFileInfo>> ApplySymbologyFromLayerAsync(string? mapName, string layerName, string layerFilePath, CancellationToken ct = default)
        { Calls.Add($"applysym:{layerName}:{layerFilePath}"); return R<LayerFileInfo>("applysym"); }
        public Task<OperationResult<LayerFileInfo>> SaveLayerFileAsync(string? mapName, string layerName, string outputPath, CancellationToken ct = default)
        { Calls.Add($"savefile:{layerName}:{outputPath}"); return R<LayerFileInfo>("savefile"); }

        private Task<OperationResult<T>> R<T>(string key)
            => Task.FromResult(Results.TryGetValue(key, out var v) && v is OperationResult<T> typed
                ? typed
                : OperationResult<T>.Fail(ErrorCodes.NotImplemented, $"'{key}' not scripted."));
    }

    private sealed class ScriptedMapService : IMapService
    {
        public readonly Dictionary<string, object?> Results = new(StringComparer.Ordinal);
        public readonly List<string> Calls = new();
        public Func<Task<OperationResult<BasemapInfo>>>? SlowBasemap { get; set; }

        public Task<OperationResult<MapInfo?>> GetCurrentMapAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapInfo?>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<IReadOnlyList<MapInfo>>> GetMapsAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<MapInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<MapExtentInfo>> GetMapExtentAsync(string? mapName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapExtentInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<MapExtentSetInfo>> SetMapExtentAsync(string? mapName, double xMin, double yMin, double xMax, double yMax, string? spatialReference, CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapExtentSetInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<BasemapInfo>> SlowBasemapAsync(string? mapName, string basemap, CancellationToken ct = default)
            => SlowBasemap?.Invoke() ?? Task.FromResult(OperationResult<BasemapInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<MapViewInfo>> GetMapViewAsync(string? mapName, CancellationToken ct = default)
        { Calls.Add($"getview:{mapName}"); return R<MapViewInfo>("getview"); }
        public Task<OperationResult<MapViewSetInfo>> SetMapViewAsync(string? mapName, double? xMin, double? yMin, double? xMax, double? yMax, double? centerX, double? centerY, double? scale, double? rotation, CancellationToken ct = default)
        { Calls.Add($"setview:{mapName}:{xMin}:{centerX}:{scale}:{rotation}"); return R<MapViewSetInfo>("setview"); }
        public Task<OperationResult<MapViewImageResult>> ExportMapViewAsync(string? mapName, int? width, int? height, int? resolutionDpi, int? maxEdge, string? outputPath, CancellationToken ct = default)
        { Calls.Add($"export:{mapName}:{width}:{height}:{outputPath}"); return R<MapViewImageResult>("export"); }
        public Task<OperationResult<BookmarksInfo>> ListBookmarksAsync(string? mapName, CancellationToken ct = default)
        { Calls.Add($"listbm:{mapName}"); return R<BookmarksInfo>("listbm"); }
        public Task<OperationResult<BookmarkOpInfo>> CreateBookmarkAsync(string? mapName, string name, CancellationToken ct = default)
        { Calls.Add($"createbm:{name}"); return R<BookmarkOpInfo>("createbm"); }
        public Task<OperationResult<BookmarkOpInfo>> ApplyBookmarkAsync(string? mapName, string name, CancellationToken ct = default)
        { Calls.Add($"applybm:{name}"); return R<BookmarkOpInfo>("applybm"); }
        public Task<OperationResult<BookmarkOpInfo>> DeleteBookmarkAsync(string? mapName, string name, CancellationToken ct = default)
        { Calls.Add($"deletebm:{name}"); return R<BookmarkOpInfo>("deletebm"); }

        private Task<OperationResult<T>> R<T>(string key)
            => Task.FromResult(Results.TryGetValue(key, out var v) && v is OperationResult<T> typed
                ? typed
                : OperationResult<T>.Fail(ErrorCodes.NotImplemented, $"'{key}' not scripted."));
    }

    // ══════════════════ 助手 ══════════════════

    /// <summary>D-066：受控 GP 录制假件（捕获 GpRunRequest；结果可脚本化）。</summary>
    private sealed class ScriptedGpService : IGeoprocessingService
    {
        public GpRunRequest? LastRequest { get; private set; }
        public OperationResult<GpRunResult> RunResult { get; set; } =
            OperationResult<GpRunResult>.Ok(new GpRunResult { ToolName = "scripted", DurationMs = 1, ParameterForm = "named" });

        public Task<OperationResult<GpRunResult>> RunWhitelistedAsync(GpRunRequest request, CancellationToken ct = default)
        { LastRequest = request; return Task.FromResult(RunResult); }

        public Task<OperationResult<GeoprocessingResult>> RunToolAsync(GeoprocessingRequest request, CancellationToken ct = default)
            => Task.FromResult(OperationResult<GeoprocessingResult>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<OutputExistence>> CheckOutputExistsAsync(string outputPath, CancellationToken ct = default)
            => Task.FromResult(OperationResult<OutputExistence>.Ok(OutputExistence.NotExists, "n/a"));
        public Task<OperationResult<GpWhitelist>> GetWhitelistAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<GpWhitelist>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<GpMessagesInfo>> GetLastMessagesAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<GpMessagesInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByAttributeAsync(string? mapName, string layerName, string mode, System.Collections.Generic.IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NotImplemented, "n/a"));
        public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByLocationAsync(string? mapName, string layerName, string? selectingLayerName, string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NotImplemented, "n/a"));
    }

    /// <summary>D-066：schema 假件（joinTable/字段前置校验用）。</summary>
    private sealed class ScriptedSchemaService : ISchemaService
    {
        public SchemaInfo Schema { get; set; } = new()
        {
            Path = @"D:\D061Live\d066-test\look.gdb\look",
            Exists = true,
            Fields = new List<SchemaFieldInfo> { new() { Name = "OBJECTID", Type = "OID" }, new() { Name = "bid", Type = "Long" } },
        };

        public Task<OperationResult<SchemaInfo>> GetSchemaInfoAsync(string path, CancellationToken ct = default)
            => Task.FromResult(OperationResult<SchemaInfo>.Ok(new SchemaInfo
            {
                Path = path,
                Exists = true,
                Fields = new List<SchemaFieldInfo> { new() { Name = "OBJECTID", Type = "OID" }, new() { Name = "bid", Type = "Long" } },
            }));

        public Task<OperationResult<IReadOnlyList<DomainInfo>>> GetDomainsAsync(string workspace, int maxItems, CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<DomainInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<IReadOnlyList<IndexInfo>>> GetIndexesAsync(string path, int maxItems, CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<IndexInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<SubtypeInfo>> GetSubtypesAsync(string path, CancellationToken ct = default)
            => Task.FromResult(OperationResult<SubtypeInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));
    }

    private static FakeArcGISHost Host(ScriptedLayerService? layers = null, ScriptedMapService? maps = null,
        IGeoprocessingService? gp = null, ISchemaService? schema = null)
        => new FakeArcGISHost(geoprocessing: gp, maps: maps, layers: layers ?? new ScriptedLayerService(), schema: schema);

    private static Task<OperationResult<object?>> CallAsync(
        FakeArcGISHost host, McpToolBase tool, params (string Key, object? Value)[] args)
    {
        var ctx = new ToolExecutionContext
        {
            Host = host,
            Arguments = args.ToDictionary(a => a.Key, a => a.Value),
            CancellationToken = CancellationToken.None,
        };
        return tool.ExecuteAsync(ctx);
    }

    private static string Code(OperationResult<object?> r) => r.Errors.Count > 0 ? r.Errors[0].Code : string.Empty;

    private static T? Data<T>(OperationResult<object?> r) => r.Data is T t ? t : default;

    // ══════════════════ A1 · set_layer_transparency ══════════════════

    [Fact] public async Task Transparency_MissingLayer_Rejected()
    { var r = await CallAsync(Host(), new SetLayerTransparencyTool(), ("transparency", 40)); Assert.False(r.Success); Assert.Equal(ErrorCodes.InvalidArgument, Code(r)); }

    [Fact] public async Task Transparency_MissingValue_Rejected()
    { var r = await CallAsync(Host(), new SetLayerTransparencyTool(), ("layerName", "L")); Assert.False(r.Success); Assert.Contains("transparency is required", r.Errors[0].Message); }

    [Fact] public async Task Transparency_BelowZero_Rejected()
    { var r = await CallAsync(Host(), new SetLayerTransparencyTool(), ("layerName", "L"), ("transparency", -1)); Assert.False(r.Success); Assert.Contains("between 0 and 100", r.Errors[0].Message); }

    [Fact] public async Task Transparency_Above100_Rejected()
    { var r = await CallAsync(Host(), new SetLayerTransparencyTool(), ("layerName", "L"), ("transparency", 101)); Assert.False(r.Success); Assert.Contains("between 0 and 100", r.Errors[0].Message); }

    [Fact] public async Task Transparency_ZeroAccepted() { await AssertPasses("appearance", 0.0); }
    [Fact] public async Task Transparency_HundredAccepted() { await AssertPasses("appearance", 100.0); }

    private async Task AssertPasses(string key, double value)
    {
        var s = new ScriptedLayerService();
        s.Results[key] = OperationResult<LayerAppearanceInfo>.Ok(new LayerAppearanceInfo { LayerName = "L", Transparency = value });
        var r = await CallAsync(Host(s), new SetLayerTransparencyTool(), ("layerName", "L"), ("transparency", value));
        Assert.True(r.Success);
        Assert.Equal(value, Data<LayerAppearanceInfo>(r)!.Transparency);
    }

    [Fact] public async Task Transparency_WriteBackReadback()
    {
        var s = new ScriptedLayerService();
        s.Results["appearance"] = OperationResult<LayerAppearanceInfo>.Ok(new LayerAppearanceInfo { Transparency = 55, MinScale = 0, MaxScale = 0, ShowLayerAtAllScales = true });
        var r = await CallAsync(Host(s), new SetLayerTransparencyTool(), ("layerName", "L"), ("transparency", 55));
        var d = Data<LayerAppearanceInfo>(r)!;
        Assert.Equal(55, d.Transparency);
        Assert.True(d.ShowLayerAtAllScales);
        Assert.Contains("appearance", s.Calls[0]);
        Assert.Contains("in-place", d.WriteSurface);
    }

    [Fact] public async Task Transparency_ServiceFailure_Surfaces()
    {
        var s = new ScriptedLayerService();
        s.Results["appearance"] = OperationResult<LayerAppearanceInfo>.Fail(ErrorCodes.LayerNotFound, "no such layer");
        var r = await CallAsync(Host(s), new SetLayerTransparencyTool(), ("layerName", "L"), ("transparency", 10));
        Assert.False(r.Success); Assert.Equal(ErrorCodes.LayerNotFound, Code(r));
    }

    // ══════════════════ A2 · set_layer_scale_range ══════════════════

    [Fact] public async Task ScaleRange_MissingLayer_Rejected()
    { var r = await CallAsync(Host(), new SetLayerScaleRangeTool(), ("minScale", 1000)); Assert.False(r.Success); Assert.Equal(ErrorCodes.InvalidArgument, Code(r)); }

    [Fact] public async Task ScaleRange_NoBounds_Rejected()
    { var r = await CallAsync(Host(), new SetLayerScaleRangeTool(), ("layerName", "L")); Assert.False(r.Success); Assert.Contains("at least one", r.Errors[0].Message); }

    [Fact] public async Task ScaleRange_Negative_Rejected()
    { var r = await CallAsync(Host(), new SetLayerScaleRangeTool(), ("layerName", "L"), ("minScale", -5)); Assert.False(r.Success); Assert.Contains(">= 0", r.Errors[0].Message); }

    [Fact] public async Task ScaleRange_MinOnly_Passes()
    {
        var s = new ScriptedLayerService();
        s.Results["appearance"] = OperationResult<LayerAppearanceInfo>.Ok(new LayerAppearanceInfo { MinScale = 5000, MaxScale = 0 });
        var r = await CallAsync(Host(s), new SetLayerScaleRangeTool(), ("layerName", "L"), ("minScale", 5000));
        Assert.True(r.Success); Assert.Equal(5000, Data<LayerAppearanceInfo>(r)!.MinScale);
    }

    [Fact] public async Task ScaleRange_MaxOnly_Passes()
    {
        var s = new ScriptedLayerService();
        s.Results["appearance"] = OperationResult<LayerAppearanceInfo>.Ok(new LayerAppearanceInfo { MinScale = 0, MaxScale = 100000 });
        var r = await CallAsync(Host(s), new SetLayerScaleRangeTool(), ("layerName", "L"), ("maxScale", 100000));
        Assert.True(r.Success); Assert.Equal(100000, Data<LayerAppearanceInfo>(r)!.MaxScale);
    }

    [Fact] public async Task ScaleRange_Both_Passes()
    {
        var s = new ScriptedLayerService();
        s.Results["appearance"] = OperationResult<LayerAppearanceInfo>.Ok(new LayerAppearanceInfo { MinScale = 1000, MaxScale = 50000 });
        var r = await CallAsync(Host(s), new SetLayerScaleRangeTool(), ("layerName", "L"), ("minScale", 1000), ("maxScale", 50000));
        Assert.True(r.Success);
        Assert.Contains("1000:50000", s.Calls[0]);
    }

    [Fact] public async Task ScaleRange_UnlimitedSemantics_Disclosed()
    {
        var s = new ScriptedLayerService();
        s.Results["appearance"] = OperationResult<LayerAppearanceInfo>.Ok(new LayerAppearanceInfo { MinScale = 0, MaxScale = 0, ShowLayerAtAllScales = true });
        var r = await CallAsync(Host(s), new SetLayerScaleRangeTool(), ("layerName", "L"), ("minScale", 0), ("maxScale", 0));
        Assert.True(Data<LayerAppearanceInfo>(r)!.ShowLayerAtAllScales);
    }

    [Fact] public async Task ScaleRange_MapNameForwarded()
    {
        var s = new ScriptedLayerService();
        s.Results["appearance"] = OperationResult<LayerAppearanceInfo>.Ok(new LayerAppearanceInfo());
        _ = await CallAsync(Host(s), new SetLayerScaleRangeTool(), ("mapName", "M1"), ("layerName", "L"), ("minScale", 1));
        Assert.StartsWith("appearance:M1:", s.Calls[0]);
    }

    // ══════════════════ A3 · create_group_layer ══════════════════

    [Fact] public async Task Group_MissingName_Rejected()
    { var r = await CallAsync(Host(), new CreateGroupLayerTool(), ("layerNames", new List<string> { "A" })); Assert.False(r.Success); Assert.Equal(ErrorCodes.InvalidArgument, Code(r)); }

    [Fact] public async Task Group_BlankName_Rejected()
    { var r = await CallAsync(Host(), new CreateGroupLayerTool(), ("groupName", "  ")); Assert.False(r.Success); }

    [Fact] public async Task Group_EmptyGroup_Passes()
    {
        var s = new ScriptedLayerService();
        s.Results["group"] = OperationResult<GroupLayerInfo>.Ok(new GroupLayerInfo { GroupName = "G", MovedLayerCount = 0 });
        var r = await CallAsync(Host(s), new CreateGroupLayerTool(), ("groupName", "G"));
        Assert.True(r.Success); Assert.Equal(0, Data<GroupLayerInfo>(r)!.MovedLayerCount);
    }

    [Fact] public async Task Group_WithMembers_Passes()
    {
        var s = new ScriptedLayerService();
        s.Results["group"] = OperationResult<GroupLayerInfo>.Ok(new GroupLayerInfo { GroupName = "G", MovedLayerCount = 2, MovedLayers = new List<string> { "A", "B" } });
        var r = await CallAsync(Host(s), new CreateGroupLayerTool(), ("groupName", "G"), ("layerNames", new List<string> { "A", "B" }));
        Assert.True(r.Success); Assert.Equal(2, Data<GroupLayerInfo>(r)!.MovedLayerCount);
        Assert.Contains("group::G:2", s.Calls[0]);
    }

    [Fact] public async Task Group_MovedCountIsTruthful()
    {
        var s = new ScriptedLayerService();
        s.Results["group"] = OperationResult<GroupLayerInfo>.Ok(new GroupLayerInfo { MovedLayerCount = 1, MovedLayers = new List<string> { "A" } });
        var r = await CallAsync(Host(s), new CreateGroupLayerTool(), ("groupName", "G"), ("layerNames", new List<string> { "A", "MISSING" }));
        var d = Data<GroupLayerInfo>(r)!;
        Assert.Equal(1, d.MovedLayerCount);
        Assert.Single(d.MovedLayers);
    }

    [Fact] public async Task Group_ServiceFailure_Surfaces()
    {
        var s = new ScriptedLayerService();
        s.Results["group"] = OperationResult<GroupLayerInfo>.Fail(ErrorCodes.ArcGISError, "create failed");
        var r = await CallAsync(Host(s), new CreateGroupLayerTool(), ("groupName", "G"));
        Assert.False(r.Success); Assert.Equal(ErrorCodes.ArcGISError, Code(r));
    }

    [Fact] public async Task Group_NonArrayLayerNames_TreatedAsNone()
    {
        var s = new ScriptedLayerService();
        s.Results["group"] = OperationResult<GroupLayerInfo>.Ok(new GroupLayerInfo { MovedLayerCount = 0 });
        var r = await CallAsync(Host(s), new CreateGroupLayerTool(), ("groupName", "G"), ("layerNames", "A"));
        Assert.True(r.Success); Assert.Equal(0, Data<GroupLayerInfo>(r)!.MovedLayerCount);
    }

    // ══════════════════ A4 · set_basemap（离线/超时保护）═════════════════

    [Fact] public async Task Basemap_Missing_Rejected()
    { var r = await CallAsync(Host(), new SetBasemapTool()); Assert.False(r.Success); Assert.Contains("basemap is required", r.Errors[0].Message); }

    [Fact] public async Task Basemap_Offline_ExplicitError()
    {
        var s = new ScriptedLayerService();
        s.Results["basemap"] = OperationResult<BasemapInfo>.Fail(ErrorCodes.LayerDataSourceUnavailable, "no active/signed-on portal");
        var r = await CallAsync(Host(s), new SetBasemapTool(), ("basemap", "Topographic"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.LayerDataSourceUnavailable, Code(r));
    }

    [Fact] public async Task Basemap_Timeout_Guarded()
    {
        // 服务永不返回 ⇒ 工具层超时保护必须把它转成 TIMEOUT（不挂起）。
        var l = new ScriptedLayerService();
        l.Results["basemap"] = OperationResult<BasemapInfo>.Ok(new BasemapInfo { Applied = true });
        var slow = new SlowBasemapService();
        var r = await CallAsync(new FakeArcGISHost(layers: slow), new SetBasemapTool(), ("basemap", "Topographic"));
        Assert.False(r.Success);
    }

    private sealed class SlowBasemapService : ILayerService
    {
        public Task<OperationResult<IReadOnlyList<LayerInfo>>> GetLayersAsync(string? m = null, bool f = true, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<LayerInfo?>> FindLayerAsync(string m, string l, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<LayerInfo?>> GetLayerInfoAsync(string m, string l, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<bool>> SetLayerVisibilityAsync(string m, string l, bool v, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<LayerInfo?>> AddLayerAsync(string m, string p, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<bool>> RemoveLayerAsync(string m, string l, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<DefinitionQueryInfo>> GetDefinitionQueryAsync(string? m, string l, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<DefinitionQuerySetInfo>> SetDefinitionQueryAsync(string? m, string l, string? q, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<LayerOrderInfo>> MoveLayerAsync(string? m, string l, string? r, string p, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<LayerSymbologyInfo>> GetLayerSymbologyAsync(string? m, string l, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<SymbologySetInfo>> SetSimpleSymbologyAsync(string? m, string l, string? f, string? o, double? ps, double? lw, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<LabelInfo>> GetLabelInfoAsync(string? m, string l, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OperationResult<LabelVisibilityInfo>> SetLabelVisibilityAsync(string? m, string l, bool e, CancellationToken ct = default) => throw new NotImplementedException();
        public async Task<OperationResult<BasemapInfo>> SetBasemapAsync(string? m, string b, CancellationToken ct = default)
        { await Task.Delay(TimeSpan.FromSeconds(60), ct); return OperationResult<BasemapInfo>.Ok(new BasemapInfo()); }
    }

    [Fact] public async Task Basemap_Success_DisclosesGallery()
    {
        var s = new ScriptedLayerService();
        s.Results["basemap"] = OperationResult<BasemapInfo>.Ok(new BasemapInfo
        { Applied = true, AvailableBasemaps = new List<string> { "Topographic", "Imagery" } });
        var r = await CallAsync(Host(s), new SetBasemapTool(), ("basemap", "Topographic"));
        var d = Data<BasemapInfo>(r)!;
        Assert.True(d.Applied); Assert.Equal(2, d.AvailableBasemaps.Count);
        Assert.Contains("no hang", d.OfflineBehavior);
    }

    [Fact] public async Task Basemap_NotFound_Surfaces()
    {
        var s = new ScriptedLayerService();
        s.Results["basemap"] = OperationResult<BasemapInfo>.Fail(ErrorCodes.NotFound, "basemap 'X' not found");
        var r = await CallAsync(Host(s), new SetBasemapTool(), ("basemap", "X"));
        Assert.Equal(ErrorCodes.NotFound, Code(r));
    }

    [Fact] public async Task Basemap_MapNameForwarded()
    {
        var s = new ScriptedLayerService();
        s.Results["basemap"] = OperationResult<BasemapInfo>.Ok(new BasemapInfo { Applied = true });
        _ = await CallAsync(Host(s), new SetBasemapTool(), ("mapName", "M9"), ("basemap", "Streets"));
        Assert.StartsWith("basemap:M9:", s.Calls[0]);
    }

    // ══════════════════ A5 · get_broken_layers ══════════════════

    [Fact] public async Task Broken_Empty_IsNotError()
    {
        var s = new ScriptedLayerService();
        s.Results["broken"] = OperationResult<BrokenLayersInfo>.Ok(new BrokenLayersInfo { TotalCount = 0 });
        var r = await CallAsync(Host(s), new GetBrokenLayersTool());
        Assert.True(r.Success); Assert.Equal(0, Data<BrokenLayersInfo>(r)!.TotalCount);
    }

    [Fact] public async Task Broken_ItemsReported()
    {
        var s = new ScriptedLayerService();
        s.Results["broken"] = OperationResult<BrokenLayersInfo>.Ok(new BrokenLayersInfo
        { TotalCount = 1, Items = new List<BrokenLayerInfo> { new BrokenLayerInfo { LayerName = "L", MapName = "M", IsBroken = true } } });
        var r = await CallAsync(Host(s), new GetBrokenLayersTool());
        var d = Data<BrokenLayersInfo>(r)!;
        Assert.Equal(1, d.TotalCount); Assert.True(d.Items[0].IsBroken);
    }

    [Fact] public async Task Broken_ScopeDisclosed()
    {
        var s = new ScriptedLayerService();
        s.Results["broken"] = OperationResult<BrokenLayersInfo>.Ok(new BrokenLayersInfo());
        var r = await CallAsync(Host(s), new GetBrokenLayersTool());
        Assert.Contains("all maps", Data<BrokenLayersInfo>(r)!.ScopeNote);
    }

    [Fact] public void Broken_NoArgsAccepted() { Assert.True(new GetBrokenLayersTool().InputSchema.ContainsKey("type")); }
    [Fact] public async Task Broken_ServiceFailure_Surfaces()
    {
        var s = new ScriptedLayerService();
        s.Results["broken"] = OperationResult<BrokenLayersInfo>.Fail(ErrorCodes.InternalError, "boom");
        var r = await CallAsync(Host(s), new GetBrokenLayersTool());
        Assert.Equal(ErrorCodes.InternalError, Code(r));
    }

    // ══════════════════ A6 · repair_layer_source（前后照）═════════════════

    [Fact] public async Task Repair_MissingLayer_Rejected()
    { var r = await CallAsync(Host(), new RepairLayerSourceTool(), ("newWorkspacePath", "D:\\x.gdb")); Assert.False(r.Success); }

    [Fact] public async Task Repair_NoTarget_Rejected()
    { var r = await CallAsync(Host(), new RepairLayerSourceTool(), ("layerName", "L")); Assert.False(r.Success); Assert.Contains("newWorkspacePath or newDatasetName", r.Errors[0].Message); }

    [Fact] public async Task Repair_WorkspacePath_BeforeAfter()
    {
        var s = new ScriptedLayerService();
        s.Results["repair"] = OperationResult<RepairLayerInfo>.Ok(new RepairLayerInfo
        { OldPath = @"D:\old.gdb\FC", NewPath = @"D:\new.gdb\FC", Repaired = true, Method = "FindAndReplaceWorkspacePath" });
        var r = await CallAsync(Host(s), new RepairLayerSourceTool(), ("layerName", "L"), ("newWorkspacePath", @"D:\new.gdb"));
        var d = Data<RepairLayerInfo>(r)!;
        Assert.Equal(@"D:\old.gdb\FC", d.OldPath);
        Assert.Equal(@"D:\new.gdb\FC", d.NewPath);
        Assert.True(d.Repaired);
    }

    [Fact] public async Task Repair_DatasetName_BeforeAfter()
    {
        var s = new ScriptedLayerService();
        s.Results["repair"] = OperationResult<RepairLayerInfo>.Ok(new RepairLayerInfo { OldPath = @"D:\g.gdb\A", NewPath = @"D:\g.gdb\B", Method = "SetDataConnection(dataset)" });
        var r = await CallAsync(Host(s), new RepairLayerSourceTool(), ("layerName", "L"), ("newDatasetName", "B"));
        Assert.Equal("SetDataConnection(dataset)", Data<RepairLayerInfo>(r)!.Method);
    }

    [Fact] public async Task Repair_NotRepaired_Disclosed()
    {
        var s = new ScriptedLayerService();
        s.Results["repair"] = OperationResult<RepairLayerInfo>.Ok(new RepairLayerInfo { Repaired = false });
        var r = await CallAsync(Host(s), new RepairLayerSourceTool(), ("layerName", "L"), ("newWorkspacePath", "D:\\n.gdb"));
        Assert.True(r.Success); Assert.False(Data<RepairLayerInfo>(r)!.Repaired);
    }

    [Fact] public async Task Repair_UnsupportedConnection_Rejected()
    {
        var s = new ScriptedLayerService();
        s.Results["repair"] = OperationResult<RepairLayerInfo>.Fail(ErrorCodes.InvalidArgument, "does not support dataset rebinding");
        var r = await CallAsync(Host(s), new RepairLayerSourceTool(), ("layerName", "L"), ("newDatasetName", "B"));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact] public async Task Repair_WriteSurfaceDisclosed()
    {
        var s = new ScriptedLayerService();
        s.Results["repair"] = OperationResult<RepairLayerInfo>.Ok(new RepairLayerInfo());
        var r = await CallAsync(Host(s), new RepairLayerSourceTool(), ("layerName", "L"), ("newWorkspacePath", "D:\\n.gdb"));
        Assert.Contains("no data modified", Data<RepairLayerInfo>(r)!.WriteSurface);
    }

    // ══════════════════ A7/A8 · add_join / remove_join ══════════════════

    [Fact] public async Task AddJoin_MissingLayer_Rejected()
    { var r = await CallAsync(Host(), new AddJoinTool(), ("relationshipClass", "RC")); Assert.False(r.Success); Assert.Equal(ErrorCodes.InvalidArgument, Code(r)); }

    // ── D-066：两件升级为受控 GP 代理（语义更新；占位时代断言随之退役）──

    private static ScriptedLayerService LayerWithFind()
        => new() { FindResult = OperationResult<LayerInfo?>.Ok(new LayerInfo { Name = "L", LayerType = "FeatureLayer", MapName = "TestMap" }) };

    private static OperationResult<JoinStateInfo> JoinState(int fieldCount, string[] dotted)
        => OperationResult<JoinStateInfo>.Ok(new JoinStateInfo
        {
            LayerName = "L",
            MapName = "TestMap",
            FieldCount = fieldCount,
            Fields = Enumerable.Range(0, Math.Max(0, fieldCount - dotted.Length)).Select(i => "f" + i).Concat(dotted).ToList(),
            DottedFields = dotted,
            JoinedDetected = dotted.Length > 0,
        });

    private const string JoinTablePath66 = @"D:\D061Live\d066-test\look.gdb\look";

    [Fact] public async Task AddJoin_MissingJoinTable_Rejected()
    { var r = await CallAsync(Host(), new AddJoinTool(), ("layerName", "L")); Assert.False(r.Success); Assert.Contains("joinTable is required", r.Errors[0].Message); }

    [Fact] public async Task AddJoin_KeepAllForwarded()
    {
        var gp = new ScriptedGpService();
        var layers = LayerWithFind();
        layers.JoinStates.Enqueue(JoinState(3, Array.Empty<string>()));
        layers.JoinStates.Enqueue(JoinState(5, new[] { "look.label" }));
        var r = await CallAsync(Host(layers, gp: gp, schema: new ScriptedSchemaService()), new AddJoinTool(),
            ("layerName", "L"), ("joinTable", JoinTablePath66), ("joinField", "bid"), ("keepAll", true), ("confirm", true));
        Assert.True(r.Success);
        Assert.Equal("management.AddJoin", gp.LastRequest!.ToolName);
        Assert.True(gp.LastRequest.Confirm);
        Assert.Equal("KEEP_ALL", gp.LastRequest.Parameters!["join_type"]);
        Assert.Equal("bid", gp.LastRequest.Parameters["in_field"]);
        Assert.Equal("bid", gp.LastRequest.Parameters["join_field"]);
    }

    [Fact] public async Task AddJoin_DefaultKeepAllFalse()
    {
        var gp = new ScriptedGpService();
        var layers = LayerWithFind();
        layers.JoinStates.Enqueue(JoinState(3, Array.Empty<string>()));
        layers.JoinStates.Enqueue(JoinState(3, Array.Empty<string>()));
        _ = await CallAsync(Host(layers, gp: gp, schema: new ScriptedSchemaService()), new AddJoinTool(),
            ("layerName", "L"), ("joinTable", JoinTablePath66), ("joinField", "bid"), ("confirm", true));
        Assert.Equal("KEEP_COMMON", gp.LastRequest!.Parameters!["join_type"]);
    }

    [Fact] public async Task AddJoin_WhitelistOutside_PropagatesRefusal()
    {
        // 白名单准入在 GP 服务侧 fail-closed；工具层必须如实透传（不旁路、零副作用）。
        var gp = new ScriptedGpService
        {
            RunResult = OperationResult<GpRunResult>.Fail(
                ErrorCodes.InvalidArgument, "'management.AddJoin' is not in the controlled GP whitelist (受控白名单外，禁止执行)."),
        };
        var layers = LayerWithFind();
        layers.JoinStates.Enqueue(JoinState(3, Array.Empty<string>()));
        layers.JoinStates.Enqueue(JoinState(3, Array.Empty<string>()));
        var r = await CallAsync(Host(layers, gp: gp, schema: new ScriptedSchemaService()), new AddJoinTool(),
            ("layerName", "L"), ("joinTable", JoinTablePath66), ("joinField", "bid"), ("confirm", true));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.NotNull(gp.LastRequest);   // 请求确实进入了受控通道（未旁路）
    }

    [Fact] public void AddJoin_DescriptionMentionsDisclosure()
    {
        var d = new AddJoinTool().Description;
        Assert.Contains("受控 GP 代理", d, StringComparison.Ordinal);
        Assert.Contains("confirm", d, StringComparison.Ordinal);
    }

    [Fact] public async Task RemoveJoin_MissingLayer_Rejected()
    { var r = await CallAsync(Host(), new RemoveJoinTool()); Assert.False(r.Success); Assert.Equal(ErrorCodes.InvalidArgument, Code(r)); }

    [Fact] public async Task RemoveJoin_NoJoin_Passes()
    {
        var gp = new ScriptedGpService();
        var layers = LayerWithFind();
        layers.JoinStates.Enqueue(JoinState(5, new[] { "look.label" }));   // 前照：连接态
        layers.JoinStates.Enqueue(JoinState(3, Array.Empty<string>()));    // 后照：已复原
        var r = await CallAsync(Host(layers, gp: gp), new RemoveJoinTool(), ("layerName", "L"), ("confirm", true));
        Assert.True(r.Success);
        var payload = Assert.IsType<Dictionary<string, object?>>(r.Data);
        Assert.Equal("remove", payload["operation"]);
        Assert.False((bool)payload["joined"]!);
        Assert.True((bool)payload["idempotentNoJoin"]!);
    }

    [Fact] public void RemoveJoin_SdkGap_Disclosed()
    {
        var d = new RemoveJoinTool().Description;
        Assert.Contains("受控 GP 代理", d, StringComparison.Ordinal);
        Assert.Contains("confirm", d, StringComparison.Ordinal);
    }

    // ══════════════════ A9 · rename_layer ══════════════════

    [Fact] public async Task Rename_MissingLayer_Rejected() { var r = await CallAsync(Host(), new RenameLayerTool(), ("newName", "N")); Assert.False(r.Success); }
    [Fact] public async Task Rename_MissingNewName_Rejected() { var r = await CallAsync(Host(), new RenameLayerTool(), ("layerName", "L")); Assert.False(r.Success); }
    [Fact] public async Task Rename_BlankNewName_Rejected() { var r = await CallAsync(Host(), new RenameLayerTool(), ("layerName", "L"), ("newName", " ")); Assert.False(r.Success); }

    [Fact] public async Task Rename_BeforeAfter()
    {
        var s = new ScriptedLayerService();
        s.Results["rename"] = OperationResult<RenameLayerInfo>.Ok(new RenameLayerInfo { OldName = "L", NewName = "N" });
        var r = await CallAsync(Host(s), new RenameLayerTool(), ("layerName", "L"), ("newName", "N"));
        var d = Data<RenameLayerInfo>(r)!;
        Assert.Equal("L", d.OldName); Assert.Equal("N", d.NewName);
    }

    [Fact] public async Task Rename_Ambiguous_Surfaces()
    {
        var s = new ScriptedLayerService();
        s.Results["rename"] = OperationResult<RenameLayerInfo>.Fail(ErrorCodes.AmbiguousLayerName, "two layers named L");
        var r = await CallAsync(Host(s), new RenameLayerTool(), ("layerName", "L"), ("newName", "N"));
        Assert.Equal(ErrorCodes.AmbiguousLayerName, Code(r));
    }

    [Fact] public async Task Rename_MapNameForwarded()
    {
        var s = new ScriptedLayerService();
        s.Results["rename"] = OperationResult<RenameLayerInfo>.Ok(new RenameLayerInfo());
        _ = await CallAsync(Host(s), new RenameLayerTool(), ("mapName", "M2"), ("layerName", "L"), ("newName", "N"));
        Assert.StartsWith("rename:", s.Calls[0]);
    }

    // ══════════════════ A10 · duplicate_layer ══════════════════

    [Fact] public async Task Duplicate_MissingLayer_Rejected() { var r = await CallAsync(Host(), new DuplicateLayerTool()); Assert.False(r.Success); }

    [Fact] public async Task Duplicate_DefaultName()
    {
        var s = new ScriptedLayerService();
        s.Results["duplicate"] = OperationResult<DuplicateLayerInfo>.Ok(new DuplicateLayerInfo { SourceLayerName = "L", NewLayerName = "L copy" });
        var r = await CallAsync(Host(s), new DuplicateLayerTool(), ("layerName", "L"));
        Assert.Equal("L copy", Data<DuplicateLayerInfo>(r)!.NewLayerName);
    }

    [Fact] public async Task Duplicate_ExplicitName()
    {
        var s = new ScriptedLayerService();
        s.Results["duplicate"] = OperationResult<DuplicateLayerInfo>.Ok(new DuplicateLayerInfo { NewLayerName = "L2" });
        var r = await CallAsync(Host(s), new DuplicateLayerTool(), ("layerName", "L"), ("newName", "L2"));
        Assert.Equal("L2", Data<DuplicateLayerInfo>(r)!.NewLayerName);
    }

    [Fact] public async Task Duplicate_Failure_Surfaces()
    {
        var s = new ScriptedLayerService();
        s.Results["duplicate"] = OperationResult<DuplicateLayerInfo>.Fail(ErrorCodes.ArcGISError, "copy failed");
        var r = await CallAsync(Host(s), new DuplicateLayerTool(), ("layerName", "L"));
        Assert.Equal(ErrorCodes.ArcGISError, Code(r));
    }

    [Fact] public void Duplicate_SharesDataSource_Disclosed()
        => Assert.Contains("共享原数据源", new DuplicateLayerTool().Description);

    // ══════════════════ B1 · set_layer_renderer（三模式）═════════════════

    [Fact] public async Task Renderer_MissingLayer_Rejected() { var r = await CallAsync(Host(), new SetLayerRendererTool(), ("mode", "single")); Assert.False(r.Success); }
    [Fact] public async Task Renderer_MissingMode_Rejected() { var r = await CallAsync(Host(), new SetLayerRendererTool(), ("layerName", "L")); Assert.False(r.Success); }

    [Fact] public async Task Renderer_UnknownMode_Rejected()
    {
        var s = new ScriptedLayerService();
        s.Results["renderer"] = OperationResult<LayerRendererInfo>.Fail(ErrorCodes.InvalidArgument, "unknown renderer mode");
        var r = await CallAsync(Host(s), new SetLayerRendererTool(), ("layerName", "L"), ("mode", "bogus"));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact] public async Task Renderer_SingleMode_Passes()
    {
        var s = new ScriptedLayerService();
        s.Results["renderer"] = OperationResult<LayerRendererInfo>.Ok(new LayerRendererInfo { Mode = "single", RendererType = "CIMSimpleRenderer", Applied = true });
        var r = await CallAsync(Host(s), new SetLayerRendererTool(), ("layerName", "L"), ("mode", "single"));
        Assert.True(Data<LayerRendererInfo>(r)!.Applied);
        Assert.Contains("renderer:L:single:", s.Calls[0]);
    }

    [Fact] public async Task Renderer_UniqueMode_Passes()
    {
        var s = new ScriptedLayerService();
        s.Results["renderer"] = OperationResult<LayerRendererInfo>.Ok(new LayerRendererInfo { Mode = "unique", Field = "CAT", RendererType = "CIMUniqueValueRenderer" });
        var r = await CallAsync(Host(s), new SetLayerRendererTool(), ("layerName", "L"), ("mode", "unique"), ("field", "CAT"));
        var d = Data<LayerRendererInfo>(r)!;
        Assert.Equal("CAT", d.Field);
        Assert.Equal("CIMUniqueValueRenderer", d.RendererType);
    }

    [Fact] public async Task Renderer_GraduatedMode_Passes()
    {
        var s = new ScriptedLayerService();
        s.Results["renderer"] = OperationResult<LayerRendererInfo>.Ok(new LayerRendererInfo { Mode = "graduated", Field = "VAL3", ClassCount = 5, ColorRamp = "Green to Red" });
        var r = await CallAsync(Host(s), new SetLayerRendererTool(), ("layerName", "L"), ("mode", "graduated"), ("field", "VAL3"), ("classCount", 5), ("colorRamp", "Green to Red"));
        var d = Data<LayerRendererInfo>(r)!;
        Assert.Equal(5, d.ClassCount); Assert.Equal("Green to Red", d.ColorRamp);
    }

    [Fact] public async Task Renderer_IllegalField_Rejected()
    {
        var s = new ScriptedLayerService();
        s.Results["renderer"] = OperationResult<LayerRendererInfo>.Fail(ErrorCodes.InvalidArgument, "field 'NOPE' does not exist");
        var r = await CallAsync(Host(s), new SetLayerRendererTool(), ("layerName", "L"), ("mode", "unique"), ("field", "NOPE"));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact] public async Task Renderer_WriteSurfaceDisclosed()
    {
        var s = new ScriptedLayerService();
        s.Results["renderer"] = OperationResult<LayerRendererInfo>.Ok(new LayerRendererInfo());
        var r = await CallAsync(Host(s), new SetLayerRendererTool(), ("layerName", "L"), ("mode", "single"));
        Assert.Contains("prior renderer is not auto-saved", Data<LayerRendererInfo>(r)!.WriteSurface);
    }

    // ══════════════════ B2 · get_layer_symbology（既有件 · 本批增强）═════════════════

    [Fact] public void SymbologyEnhance_UniqueValueFields_Carried()
    {
        var info = new LayerSymbologyInfo { UniqueValueFields = new List<string> { "CAT" }, UniqueValueClassCount = 2 };
        Assert.Single(info.UniqueValueFields); Assert.Equal(2, info.UniqueValueClassCount);
    }

    [Fact] public void SymbologyEnhance_ClassBreaksCarried()
    {
        var info = new LayerSymbologyInfo
        {
            ClassBreakField = "VAL3",
            ClassBreakCount = 3,
            ClassBreaks = new List<ClassBreakSummary> { new ClassBreakSummary { Lower = 0, Upper = 4 } },
        };
        Assert.Equal("VAL3", info.ClassBreakField);
        Assert.Equal(3, info.ClassBreakCount);
        Assert.Equal(4, info.ClassBreaks![0].Upper);
    }

    [Fact] public void SymbologyEnhance_LabelDigestCarried()
    {
        var info = new LayerSymbologyInfo { Labels = new LabelDigest { Enabled = true, LabelClassCount = 1, FirstExpression = "[CAT]" } };
        Assert.True(info.Labels!.Enabled); Assert.Equal("[CAT]", info.Labels.FirstExpression);
    }

    [Fact] public void SymbologyEnhance_SimpleThreeStateUnchanged()
    {
        // G-82-C 三态判定不因增强而改变：复杂渲染器仍 isSimpleRenderer=false。
        var info = new LayerSymbologyInfo { IsSimpleRenderer = false, SupportsSymbology = true };
        Assert.False(info.IsSimpleRenderer); Assert.True(info.SupportsSymbology);
        Assert.Null(info.FillColor);
    }

    [Fact] public async Task Symbology_MissingLayer_Rejected()
    { var r = await CallAsync(Host(), new GetLayerSymbologyTool()); Assert.False(r.Success); Assert.Equal(ErrorCodes.InvalidArgument, Code(r)); }

    [Fact] public async Task Symbology_ServiceFailure_Surfaces()
    {
        var r = await CallAsync(Host(), new GetLayerSymbologyTool(), ("layerName", "L"));
        Assert.False(r.Success);
    }

    // ══════════════════ B3 · list_color_ramps ══════════════════

    [Fact] public async Task Ramps_Empty_IsNotError()
    {
        var s = new ScriptedLayerService();
        s.Results["ramps"] = OperationResult<ColorRampsInfo>.Ok(new ColorRampsInfo { TotalCount = 0 });
        var r = await CallAsync(Host(s), new ListColorRampsTool());
        Assert.True(r.Success); Assert.Equal(0, Data<ColorRampsInfo>(r)!.TotalCount);
    }

    [Fact] public async Task Ramps_ItemsReported()
    {
        var s = new ScriptedLayerService();
        s.Results["ramps"] = OperationResult<ColorRampsInfo>.Ok(new ColorRampsInfo
        { TotalCount = 2, StyleName = "ArcGIS Colors", Items = new List<ColorRampInfo> { new ColorRampInfo { Name = "A" }, new ColorRampInfo { Name = "B" } } });
        var r = await CallAsync(Host(s), new ListColorRampsTool());
        var d = Data<ColorRampsInfo>(r)!;
        Assert.Equal(2, d.TotalCount); Assert.Equal("ArcGIS Colors", d.StyleName);
    }

    [Fact] public async Task Ramps_Failure_Surfaces()
    {
        var s = new ScriptedLayerService();
        s.Results["ramps"] = OperationResult<ColorRampsInfo>.Fail(ErrorCodes.InternalError, "x");
        var r = await CallAsync(Host(s), new ListColorRampsTool());
        Assert.Equal(ErrorCodes.InternalError, Code(r));
    }

    [Fact] public void Ramps_NoRequiredArgs() => Assert.False(new ListColorRampsTool().InputSchema.ContainsKey("required"));

    // ══════════════════ B4 · apply_symbology_from_layer ══════════════════

    [Fact] public async Task ApplySym_MissingLayer_Rejected() { var r = await CallAsync(Host(), new ApplySymbologyFromLayerTool(), ("layerFilePath", "a.lyrx")); Assert.False(r.Success); }
    [Fact] public async Task ApplySym_MissingPath_Rejected() { var r = await CallAsync(Host(), new ApplySymbologyFromLayerTool(), ("layerName", "L")); Assert.False(r.Success); }

    [Fact] public async Task ApplySym_Passes()
    {
        var s = new ScriptedLayerService();
        s.Results["applysym"] = OperationResult<LayerFileInfo>.Ok(new LayerFileInfo { Operation = "apply", Ok = true, Path = @"D:\a.lyrx" });
        var r = await CallAsync(Host(s), new ApplySymbologyFromLayerTool(), ("layerName", "L"), ("layerFilePath", @"D:\a.lyrx"));
        Assert.True(Data<LayerFileInfo>(r)!.Ok);
    }

    [Fact] public async Task ApplySym_Unreadable_Rejected()
    {
        var s = new ScriptedLayerService();
        s.Results["applysym"] = OperationResult<LayerFileInfo>.Fail(ErrorCodes.InvalidArgument, "cannot read layer file");
        var r = await CallAsync(Host(s), new ApplySymbologyFromLayerTool(), ("layerName", "L"), ("layerFilePath", "nope.lyrx"));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact] public void ApplySym_OnlyRenderer_Disclosed() => Assert.Contains("只替换 renderer", new ApplySymbologyFromLayerTool().Description);

    // ══════════════════ B5 · save_layer_file（守卫）═════════════════

    [Fact] public async Task SaveFile_MissingLayer_Rejected() { var r = await CallAsync(Host(), new SaveLayerFileTool(), ("outputPath", @"D:\a.lyrx")); Assert.False(r.Success); }
    [Fact] public async Task SaveFile_MissingPath_Rejected() { var r = await CallAsync(Host(), new SaveLayerFileTool(), ("layerName", "L")); Assert.False(r.Success); }

    [Fact] public async Task SaveFile_ProtectedPath_Rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "d063-guard");
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("ARCGIS_PRO_MCP_PROTECTED_ROOTS", root);
        try
        {
            var r = await CallAsync(Host(), new SaveLayerFileTool(), ("layerName", "L"), ("outputPath", Path.Combine(root, "a.lyrx")));
            Assert.False(r.Success);
            Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ARCGIS_PRO_MCP_PROTECTED_ROOTS", null);
        }
    }

    [Fact] public async Task SaveFile_Passes()
    {
        var s = new ScriptedLayerService();
        s.Results["savefile"] = OperationResult<LayerFileInfo>.Ok(new LayerFileInfo { Operation = "save", Ok = true, Bytes = 1234 });
        var r = await CallAsync(Host(s), new SaveLayerFileTool(), ("layerName", "L"), ("outputPath", @"D:\out\a.lyrx"));
        Assert.True(r.Success); Assert.Equal(1234, Data<LayerFileInfo>(r)!.Bytes);
    }

    [Fact] public async Task SaveFile_Failure_Surfaces()
    {
        var s = new ScriptedLayerService();
        s.Results["savefile"] = OperationResult<LayerFileInfo>.Fail(ErrorCodes.ArcGISError, "write failed");
        var r = await CallAsync(Host(s), new SaveLayerFileTool(), ("layerName", "L"), ("outputPath", @"D:\out\a.lyrx"));
        Assert.Equal(ErrorCodes.ArcGISError, Code(r));
    }

    // ══════════════════ C1/C2 · get/set_map_view ══════════════════

    [Fact] public async Task GetView_Passes()
    {
        var m = new ScriptedMapService();
        m.Results["getview"] = OperationResult<MapViewInfo>.Ok(new MapViewInfo { HasActiveView = true, X = 1, Y = 2, Scale = 5000 });
        var r = await CallAsync(Host(maps: m), new GetMapViewTool());
        var d = Data<MapViewInfo>(r)!;
        Assert.True(d.HasActiveView); Assert.Equal(5000, d.Scale);
    }

    [Fact] public async Task GetView_NoActiveView_Surfaces()
    {
        var m = new ScriptedMapService();
        m.Results["getview"] = OperationResult<MapViewInfo>.Fail(ErrorCodes.NoActiveView, "no active view");
        var r = await CallAsync(Host(maps: m), new GetMapViewTool());
        Assert.Equal(ErrorCodes.NoActiveView, Code(r));
    }

    [Fact] public void GetView_DisclosesViewScope() => Assert.Contains("ACTIVE map view", new MapViewInfo().ViewSourceNote);

    [Fact] public async Task SetView_ExtentPasses()
    {
        var m = new ScriptedMapService();
        m.Results["setview"] = OperationResult<MapViewSetInfo>.Ok(new MapViewSetInfo { Applied = true, RequestedBy = "extent" });
        var r = await CallAsync(Host(maps: m), new SetMapViewTool(), ("xMin", 0), ("yMin", 0), ("xMax", 10), ("yMax", 10));
        Assert.Equal("extent", Data<MapViewSetInfo>(r)!.RequestedBy);
    }

    [Fact] public async Task SetView_CenterScalePasses()
    {
        var m = new ScriptedMapService();
        m.Results["setview"] = OperationResult<MapViewSetInfo>.Ok(new MapViewSetInfo { Applied = true, RequestedBy = "center+scale" });
        var r = await CallAsync(Host(maps: m), new SetMapViewTool(), ("centerX", 5), ("centerY", 5), ("scale", 1000));
        Assert.Equal("center+scale", Data<MapViewSetInfo>(r)!.RequestedBy);
    }

    [Fact] public async Task SetView_NothingProvided_Rejected()
    {
        var m = new ScriptedMapService();
        m.Results["setview"] = OperationResult<MapViewSetInfo>.Fail(ErrorCodes.InvalidArgument, "provide an extent …");
        var r = await CallAsync(Host(maps: m), new SetMapViewTool());
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact] public async Task SetView_Rotation_Disclosed()
    {
        var m = new ScriptedMapService();
        m.Results["setview"] = OperationResult<MapViewSetInfo>.Fail(ErrorCodes.NotImplemented, "rotation-only is not reachable");
        var r = await CallAsync(Host(maps: m), new SetMapViewTool(), ("rotation", 45));
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
        Assert.Contains("rotation-only", r.Errors[0].Message);
    }

    [Fact] public async Task SetView_CameraAfterReturned()
    {
        var m = new ScriptedMapService();
        m.Results["setview"] = OperationResult<MapViewSetInfo>.Ok(new MapViewSetInfo
        { Applied = true, CameraAfter = new MapViewInfo { X = 5, Y = 5, Scale = 1000 } });
        var r = await CallAsync(Host(maps: m), new SetMapViewTool(), ("centerX", 5), ("centerY", 5), ("scale", 1000));
        Assert.Equal(1000, Data<MapViewSetInfo>(r)!.CameraAfter!.Scale);
    }

    // ══════════════════ C3 · ★ export_map_view 回图 ══════════════════

    private static byte[] MinimalPng(int w, int h)
    {
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        void Be(int v) { ms.WriteByte((byte)(v >> 24)); ms.WriteByte((byte)(v >> 16)); ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)v); }
        Be(13); ms.Write(new byte[] { 0x49, 0x48, 0x44, 0x52 }); Be(w); Be(h);
        ms.Write(new byte[] { 8, 2, 0, 0, 0 });
        return ms.ToArray();
    }

    [Fact] public void Export_PngMagicRecognized()
    {
        var png = MinimalPng(8, 6);
        Assert.True(MapImagePolicy.IsPng(png));
        Assert.False(MapImagePolicy.IsPng(new byte[] { 1, 2, 3 }));
    }

    [Fact] public void Export_PngSizeParsed()
    {
        Assert.True(MapImagePolicy.TryReadPngSize(MinimalPng(1200, 800), out var w, out var h));
        Assert.Equal(1200, w); Assert.Equal(800, h);
    }

    [Fact] public void Export_MaxEdgeClamped()
    {
        int w = 4000, h = 2000;
        MapImagePolicy.ClampToMaxEdge(ref w, ref h, 1200);
        Assert.Equal(1200, w); Assert.Equal(600, h);
    }

    [Fact] public void Export_UnderCap_NotClamped()
    {
        int w = 800, h = 600;
        MapImagePolicy.ClampToMaxEdge(ref w, ref h, 1200);
        Assert.Equal(800, w); Assert.Equal(600, h);
    }

    [Fact] public void Export_ByteCapEnforced()
    {
        Assert.True(MapImagePolicy.ExceedsByteCap(1024 * 1024 + 1));
        Assert.False(MapImagePolicy.ExceedsByteCap(1024 * 1024));
    }

    [Fact] public void Export_DefaultsAre96Dpi1200Edge()
    {
        Assert.Equal(96, MapImagePolicy.DefaultDpi);
        Assert.Equal(1200, MapImagePolicy.DefaultMaxEdge);
        Assert.Equal(1024 * 1024, MapImagePolicy.MaxPngBytes);
    }

    [Fact] public async Task Export_NoArtefactMode_DisclosesTransient()
    {
        var m = new ScriptedMapService();
        var png = MinimalPng(1200, 800);
        m.Results["export"] = OperationResult<MapViewImageResult>.Ok(new MapViewImageResult
        {
            DataBase64 = Convert.ToBase64String(png),
            Width = 1200, Height = 800, Bytes = png.Length,
            UsedTransientFile = true, TransientFileDeleted = true, OutputPath = null,
        });
        var r = await CallAsync(Host(maps: m), new ExportMapViewTool());
        var d = Data<MapViewImageResult>(r)!;
        Assert.Null(d.OutputPath);
        Assert.True(d.UsedTransientFile && d.TransientFileDeleted);
        Assert.True(MapImagePolicy.IsPng(Convert.FromBase64String(d.DataBase64)));
        Assert.False(MapImagePolicy.ExceedsByteCap(d.Bytes));
    }

    [Fact] public async Task Export_ProtectedPath_Rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "d063-guard2");
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("ARCGIS_PRO_MCP_PROTECTED_ROOTS", root);
        try
        {
            var r = await CallAsync(Host(maps: new ScriptedMapService()), new ExportMapViewTool(),
                ("outputPath", Path.Combine(root, "x.png")));
            Assert.False(r.Success);
            Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        }
        finally { Environment.SetEnvironmentVariable("ARCGIS_PRO_MCP_PROTECTED_ROOTS", null); }
    }

    [Fact] public async Task Export_NoActiveView_Surfaces()
    {
        var m = new ScriptedMapService();
        m.Results["export"] = OperationResult<MapViewImageResult>.Fail(ErrorCodes.NoActiveView, "no active view");
        var r = await CallAsync(Host(maps: m), new ExportMapViewTool());
        Assert.Equal(ErrorCodes.NoActiveView, Code(r));
    }

    [Fact] public async Task Export_ParamsForwarded()
    {
        var m = new ScriptedMapService();
        m.Results["export"] = OperationResult<MapViewImageResult>.Ok(new MapViewImageResult());
        _ = await CallAsync(Host(maps: m), new ExportMapViewTool(), ("width", 640), ("height", 480), ("maxEdge", 1200));
        Assert.Contains("export::640:480:", m.Calls[0]);
    }

    // ══════════════════ ★ image content（MCP wire 形态）═════════════════

    [Fact] public void ImageContent_WireShapeIsMcpCompliant()
    {
        var result = new McpToolCallResult
        {
            IsError = false,
            Content = new object[]
            {
                new McpTextContent { Type = "text", Text = "summary" },
                new McpImageContent { Type = "image", Data = "AAAA", MimeType = "image/png" },
            }
        };
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("\"type\":\"text\"", json);
        Assert.Contains("\"type\":\"image\"", json);
        Assert.Contains("\"data\":\"AAAA\"", json);
        Assert.Contains("\"mimeType\":\"image/png\"", json);
    }

    [Fact] public void ImageContent_Defaults()
    {
        var img = new McpImageContent();
        Assert.Equal("image", img.Type);
        Assert.Equal("image/png", img.MimeType);
    }

    [Fact] public void ImageResult_MimeDefaultsToPng() => Assert.Equal("image/png", new MapViewImageResult().MimeType);

    [Fact] public void ImageResult_ConstraintsDisclosed()
        => Assert.Contains("1 MB", new MapViewImageResult().ConstraintsNote);

    // ══════════════════ C4–C7 · 书签 ══════════════════

    [Fact] public async Task ListBookmarks_Passes()
    {
        var m = new ScriptedMapService();
        m.Results["listbm"] = OperationResult<BookmarksInfo>.Ok(new BookmarksInfo { TotalCount = 1, Items = new List<BookmarkInfo> { new BookmarkInfo { Name = "B1" } } });
        var r = await CallAsync(Host(maps: m), new ListBookmarksTool());
        Assert.Equal(1, Data<BookmarksInfo>(r)!.TotalCount);
    }

    [Fact] public async Task ListBookmarks_Empty_IsNotError()
    {
        var m = new ScriptedMapService();
        m.Results["listbm"] = OperationResult<BookmarksInfo>.Ok(new BookmarksInfo { TotalCount = 0 });
        var r = await CallAsync(Host(maps: m), new ListBookmarksTool());
        Assert.True(r.Success);
    }

    [Fact] public async Task CreateBookmark_MissingName_Rejected() { var r = await CallAsync(Host(maps: new ScriptedMapService()), new CreateBookmarkTool()); Assert.False(r.Success); }

    [Fact] public async Task CreateBookmark_Passes()
    {
        var m = new ScriptedMapService();
        m.Results["createbm"] = OperationResult<BookmarkOpInfo>.Ok(new BookmarkOpInfo { Name = "B1", Operation = "create", Ok = true, BookmarkCountAfter = 1 });
        var r = await CallAsync(Host(maps: m), new CreateBookmarkTool(), ("name", "B1"));
        var d = Data<BookmarkOpInfo>(r)!;
        Assert.Equal("create", d.Operation); Assert.Equal(1, d.BookmarkCountAfter);
    }

    [Fact] public async Task CreateBookmark_Duplicate_Rejected()
    {
        var m = new ScriptedMapService();
        m.Results["createbm"] = OperationResult<BookmarkOpInfo>.Fail(ErrorCodes.InvalidArgument, "already exists");
        var r = await CallAsync(Host(maps: m), new CreateBookmarkTool(), ("name", "B1"));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact] public async Task CreateBookmark_NoActiveView_Surfaces()
    {
        var m = new ScriptedMapService();
        m.Results["createbm"] = OperationResult<BookmarkOpInfo>.Fail(ErrorCodes.NoActiveView, "no active view");
        var r = await CallAsync(Host(maps: m), new CreateBookmarkTool(), ("name", "B1"));
        Assert.Equal(ErrorCodes.NoActiveView, Code(r));
    }

    [Fact] public async Task ApplyBookmark_Passes()
    {
        var m = new ScriptedMapService();
        m.Results["applybm"] = OperationResult<BookmarkOpInfo>.Ok(new BookmarkOpInfo { Operation = "apply", Ok = true });
        var r = await CallAsync(Host(maps: m), new ApplyBookmarkTool(), ("name", "B1"));
        Assert.Equal("apply", Data<BookmarkOpInfo>(r)!.Operation);
    }

    [Fact] public async Task ApplyBookmark_NotFound()
    {
        var m = new ScriptedMapService();
        m.Results["applybm"] = OperationResult<BookmarkOpInfo>.Fail(ErrorCodes.NotFound, "not found");
        var r = await CallAsync(Host(maps: m), new ApplyBookmarkTool(), ("name", "X"));
        Assert.Equal(ErrorCodes.NotFound, Code(r));
    }

    [Fact] public async Task DeleteBookmark_Passes()
    {
        var m = new ScriptedMapService();
        m.Results["deletebm"] = OperationResult<BookmarkOpInfo>.Ok(new BookmarkOpInfo { Operation = "delete", Ok = true, BookmarkCountAfter = 0 });
        var r = await CallAsync(Host(maps: m), new DeleteBookmarkTool(), ("name", "B1"));
        Assert.Equal(0, Data<BookmarkOpInfo>(r)!.BookmarkCountAfter);
    }

    [Fact] public async Task DeleteBookmark_RoundTrip()
    {
        var m = new ScriptedMapService();
        m.Results["createbm"] = OperationResult<BookmarkOpInfo>.Ok(new BookmarkOpInfo { Operation = "create", BookmarkCountAfter = 1 });
        m.Results["deletebm"] = OperationResult<BookmarkOpInfo>.Ok(new BookmarkOpInfo { Operation = "delete", BookmarkCountAfter = 0 });
        var c = await CallAsync(Host(maps: m), new CreateBookmarkTool(), ("name", "B1"));
        var d = await CallAsync(Host(maps: m), new DeleteBookmarkTool(), ("name", "B1"));
        Assert.Equal(1, Data<BookmarkOpInfo>(c)!.BookmarkCountAfter);
        Assert.Equal(0, Data<BookmarkOpInfo>(d)!.BookmarkCountAfter);
    }

    [Fact] public void DeleteBookmark_NotDestructive_Disclosed()
    {
        var note = new BookmarkOpInfo().DeleteScopeNote;
        Assert.Contains("not in the destructive catalogue", note);
        Assert.Contains("no data", note);
    }

    [Fact] public async Task DeleteBookmark_NotFound()
    {
        var m = new ScriptedMapService();
        m.Results["deletebm"] = OperationResult<BookmarkOpInfo>.Fail(ErrorCodes.NotFound, "not found");
        var r = await CallAsync(Host(maps: m), new DeleteBookmarkTool(), ("name", "X"));
        Assert.Equal(ErrorCodes.NotFound, Code(r));
    }

    [Fact]
    public async Task Unscripted_set_layer_transparency_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new SetLayerTransparencyTool(), ("layerName", "L"), ("transparency", 10));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_set_layer_scale_range_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new SetLayerScaleRangeTool(), ("layerName", "L"), ("minScale", 1));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_create_group_layer_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new CreateGroupLayerTool(), ("groupName", "G"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_set_basemap_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new SetBasemapTool(), ("basemap", "Topographic"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_get_broken_layers_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new GetBrokenLayersTool());
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_repair_layer_source_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new RepairLayerSourceTool(), ("layerName", "L"), ("newWorkspacePath", @"D:
.gdb"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_add_join_SurfacesNotImplemented()
    {
        // 宿主未脚本图层解析通道（FindLayer 走 DIM/脚本缺省）⇒ 工具层必须如实透传 NotImplemented（不静默成功）。
        var r = await CallAsync(Host(), new AddJoinTool(), ("layerName", "L"), ("joinTable", @"D:\D061Live\d066-test\look.gdb\look"), ("joinField", "bid"), ("confirm", true));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_remove_join_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new RemoveJoinTool(), ("layerName", "L"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_rename_layer_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new RenameLayerTool(), ("layerName", "L"), ("newName", "N"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_duplicate_layer_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new DuplicateLayerTool(), ("layerName", "L"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_set_layer_renderer_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new SetLayerRendererTool(), ("layerName", "L"), ("mode", "single"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_list_color_ramps_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new ListColorRampsTool());
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_apply_symbology_from_layer_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new ApplySymbologyFromLayerTool(), ("layerName", "L"), ("layerFilePath", @"D:.lyrx"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_save_layer_file_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(), new SaveLayerFileTool(), ("layerName", "L"), ("outputPath", @"D:\o.lyrx"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_get_map_view_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(maps: new ScriptedMapService()), new GetMapViewTool());
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_set_map_view_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(maps: new ScriptedMapService()), new SetMapViewTool(), ("xMin", 0), ("yMin", 0), ("xMax", 1), ("yMax", 1));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_export_map_view_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(maps: new ScriptedMapService()), new ExportMapViewTool());
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_list_bookmarks_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(maps: new ScriptedMapService()), new ListBookmarksTool());
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_create_bookmark_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(maps: new ScriptedMapService()), new CreateBookmarkTool(), ("name", "B"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_apply_bookmark_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(maps: new ScriptedMapService()), new ApplyBookmarkTool(), ("name", "B"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }

    [Fact]
    public async Task Unscripted_delete_bookmark_SurfacesNotImplemented()
    {
        // 宿主未覆写该服务方法 ⇒ DIM 默认实现返回 NotImplemented，工具层必须如实透传（不静默成功）。
        var r = await CallAsync(Host(maps: new ScriptedMapService()), new DeleteBookmarkTool(), ("name", "B"));
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Code(r));
    }
    // ══════════════════ 模型默认值（契约稳定）══════════════════

    [Fact] public void ModelDefaults_Appearance() => Assert.Equal(0, new LayerAppearanceInfo().Transparency);
    [Fact] public void ModelDefaults_ImageMime() => Assert.Equal("image/png", new MapViewImageResult().MimeType);
    [Fact] public void ModelDefaults_JoinOperation() => Assert.Equal("none", new JoinInfo().Operation);

    // ══════════════════ 契约/元数据 ══════════════════

    [Theory]
    [InlineData("set_layer_transparency")]
    [InlineData("set_layer_scale_range")]
    [InlineData("create_group_layer")]
    [InlineData("set_basemap")]
    [InlineData("get_broken_layers")]
    [InlineData("repair_layer_source")]
    [InlineData("add_join")]
    [InlineData("remove_join")]
    [InlineData("rename_layer")]
    [InlineData("duplicate_layer")]
    [InlineData("set_layer_renderer")]
    [InlineData("list_color_ramps")]
    [InlineData("apply_symbology_from_layer")]
    [InlineData("save_layer_file")]
    [InlineData("get_map_view")]
    [InlineData("set_map_view")]
    [InlineData("export_map_view")]
    [InlineData("list_bookmarks")]
    [InlineData("create_bookmark")]
    [InlineData("apply_bookmark")]
    [InlineData("delete_bookmark")]
    public void ToolNameIsD063Contract(string name)
    {
        McpToolBase tool = name switch
        {
            "set_layer_transparency" => new SetLayerTransparencyTool(),
            "set_layer_scale_range" => new SetLayerScaleRangeTool(),
            "create_group_layer" => new CreateGroupLayerTool(),
            "set_basemap" => new SetBasemapTool(),
            "get_broken_layers" => new GetBrokenLayersTool(),
            "repair_layer_source" => new RepairLayerSourceTool(),
            "add_join" => new AddJoinTool(),
            "remove_join" => new RemoveJoinTool(),
            "rename_layer" => new RenameLayerTool(),
            "duplicate_layer" => new DuplicateLayerTool(),
            "set_layer_renderer" => new SetLayerRendererTool(),
            "list_color_ramps" => new ListColorRampsTool(),
            "apply_symbology_from_layer" => new ApplySymbologyFromLayerTool(),
            "save_layer_file" => new SaveLayerFileTool(),
            "get_map_view" => new GetMapViewTool(),
            "set_map_view" => new SetMapViewTool(),
            "export_map_view" => new ExportMapViewTool(),
            "list_bookmarks" => new ListBookmarksTool(),
            "create_bookmark" => new CreateBookmarkTool(),
            "apply_bookmark" => new ApplyBookmarkTool(),
            _ => new DeleteBookmarkTool(),
        };
        Assert.Equal(name, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("object", tool.InputSchema["type"]);
    }
}
