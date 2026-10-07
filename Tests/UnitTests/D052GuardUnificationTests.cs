using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-052（Phase 12 第一批 · 修复与加固批）行为测试。
/// A ★守卫统一接入：6 文件全部 GP 写类调用点前置 <see cref="ProtectedOutputPathGuard"/>（每文件 ≥2 例）；
///   就地修改类（add_field/alter_field/calculate_field）以同判定守**输入数据集**。
/// B O-3：add_field 前照→写→后照断言（FakePythonBridge 可编程；桥缺失 → 跳过断言，GP WARNING 升级兜底）。
/// C O-5：merge 非空 fieldMappings → INVALID_ARGUMENT 显式拒绝（spike 证实 GP 对自由串生成畸形输出）。
/// D O-D043-01：set_map_extent.spatialReference schema ["string","integer"] + 整数 WKID 真实接受。
/// 反证锚点：A = 守卫调用点（工具层业务校验，非 schema 层）；D = 整数分支（工具层）。
/// </summary>
public sealed class D052GuardUnificationTests
{
    private const string FixtureOutput = "D:/ArcGIS-Pro-MCP 2.0/TestFixtures/Phase8_5_6/WorkBuddyTest.gdb/D052X";
    private const string TraversalOutput = "C:/tmp/out.gdb/../../../TestFixtures/Phase8_5_6/x.gdb/D052T";
    private const string FixtureInput = "D:/ArcGIS-Pro-MCP 2.0/TestFixtures/Phase8_5_6/WorkBuddyTest.gdb";

    // ============================================================ A：输出型守卫（6 文件）

    [Fact]
    public async Task Buffer_FixtureOutputIsRejectedBeforeOverwriteGate()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new BufferTool(), new Dictionary<string, object?>
        {
            ["input"] = "in_fc", ["output"] = FixtureOutput, ["distance"] = 10.0
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
        Assert.Empty(service.ExistenceChecks);   // 守卫先于覆写闸门
    }

    [Fact]
    public async Task Clip_TraversalOutputIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new ClipTool(), new Dictionary<string, object?>
        {
            ["input"] = "in_fc", ["clipFeatures"] = "mask", ["output"] = TraversalOutput
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task SpatialJoin_FixtureOutputIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new SpatialJoinTool(), new Dictionary<string, object?>
        {
            ["targetFeatures"] = "t", ["joinFeatures"] = "j", ["output"] = FixtureOutput
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task RasterClip_TraversalOutputIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterClipTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "r", ["maskFeatures"] = "m", ["output"] = TraversalOutput
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Near_FixtureOutputIsRejectedAndNeitherGpCallRuns()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new NearTool(), new Dictionary<string, object?>
        {
            ["input"] = "wells", ["nearFeatures"] = "faults", ["output"] = FixtureOutput
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);          // CopyFeatures 与 Near 两个 GP 调用点全部不执行
    }

    [Fact]
    public async Task Erase_FixtureOutputIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new EraseTool(), new Dictionary<string, object?>
        {
            ["input"] = "in_fc", ["eraseFeatures"] = "e", ["output"] = FixtureOutput
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task RasterResample_TraversalOutputIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterResampleTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "r", ["output"] = TraversalOutput, ["cellSize"] = "10"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task CreateFileGdb_FixtureOutputIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new CreateFileGdbTool(), new Dictionary<string, object?>
        {
            ["outputPath"] = "D:/ArcGIS-Pro-MCP 2.0/TestFixtures/Phase8_5_6/WorkBuddyTest.gdb"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
        Assert.Empty(service.ExistenceChecks);
    }

    [Fact]
    public async Task Merge_FixtureOutputIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new MergeTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b", ["output"] = FixtureOutput
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task CopyDataset_TraversalOutputIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new CopyDatasetTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "C:/x.gdb/fc", ["outputPath"] = TraversalOutput
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task ExportTable_FixtureOutputIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new ExportTableTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "C:/x.gdb/tbl", ["outputPath"] = FixtureOutput
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Project_TraversalOutputIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new ProjectTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "C:/x.gdb/fc", ["outputPath"] = TraversalOutput, ["outSR"] = "4326"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ============================================================ A：就地修改类输入守卫

    [Fact]
    public async Task AlterField_ProtectedInputIsRefusedWithNoGp()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new AlterFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = FixtureInput, ["fieldName"] = "SOME_FIELD"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Contains("in-place modification", Assert.Single(result.Errors).Message);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task CalculateField_TraversalInputIsRefusedWithNoGp()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new CalculateFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "C:/tmp/../TestFixtures/Phase8_5_6/WorkBuddyTest.gdb/fc",
            ["fieldName"] = "F",
            ["expression"] = "F + 1"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ============================================================ B：add_field 前后照断言

    [Fact]
    public async Task AddField_ProtectedInputIsRefusedWithNoGp()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new AddFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = FixtureInput, ["fieldName"] = "NEW_F"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task AddField_PreSnapshotFindsExistingFieldAndRefusesBeforeGp()
    {
        var service = new RecordingGeoprocessingService();
        var bridge = MakeBridgeWithFields("EXISTING_F");
        var result = await CallAsync(service, new AddFieldTool(),
            new Dictionary<string, object?> { ["inputPath"] = "C:/w/fc", ["fieldName"] = "existing_f" },
            bridge);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.GeoprocessingError, Assert.Single(result.Errors).Code);
        Assert.Contains("pre-write snapshot", Assert.Single(result.Errors).Message);
        Assert.Empty(service.Requests);          // 未入 GP
        Assert.Equal(1, bridge.ListFieldsCallCount);
    }

    [Fact]
    public async Task AddField_PostSnapshotMissingFieldFailsClosed()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("add") };
        var bridge = new FakePythonBridgeService();
        bridge.ListFieldsResult = OperationResult<JsonElement?>.Ok(ParseFields("OTHER"));
        var result = await CallAsync(service, new AddFieldTool(),
            new Dictionary<string, object?> { ["inputPath"] = "C:/w/fc", ["fieldName"] = "NEW_F" },
            bridge);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.GeoprocessingError, Assert.Single(result.Errors).Code);
        Assert.Contains("Post-write assertion failed", Assert.Single(result.Errors).Message);
        Assert.Single(service.Requests);         // GP 已执行，但变更未证实 → 显式失败
        Assert.Equal(2, bridge.ListFieldsCallCount);
    }

    [Fact]
    public async Task AddField_PostSnapshotConfirmsChangeSucceeds()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("add") };
        var bridge = new SequencedListFieldsBridge(ParseFields("OTHER"), ParseFields("NEW_F"));
        var result = await CallAsync(service, new AddFieldTool(),
            new Dictionary<string, object?> { ["inputPath"] = "C:/w/fc", ["fieldName"] = "NEW_F" },
            bridge);

        Assert.True(result.Success);
        Assert.Single(service.Requests);
        Assert.Equal(2, bridge.ListFieldsCallCount);
    }

    [Fact]
    public async Task AddField_UnparsablePostSnapshotDisclosesAndReturnsGpResult()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("add") };
        var bridge = new FakePythonBridgeService();
        bridge.ListFieldsResult = OperationResult<JsonElement?>.Ok(null);   // 不可解析
        var result = await CallAsync(service, new AddFieldTool(),
            new Dictionary<string, object?> { ["inputPath"] = "C:/w/fc", ["fieldName"] = "NEW_F" },
            bridge);

        Assert.True(result.Success);             // 不可证 ≠ 失败（对齐 stateProof.unprovable 先例）
        Assert.NotNull(result.Message);
        Assert.Contains("post-write assertion skipped", result.Message);
        Assert.Equal(2, bridge.ListFieldsCallCount);
    }

    // ============================================================ C：merge.fieldMappings 显式拒绝

    [Fact]
    public async Task Merge_NonEmptyFieldMappingsIsExplicitlyRefused()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new MergeTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b", ["output"] = "C:/out/out.gdb/M", ["fieldMappings"] = "SOME_MAPPING"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Contains("not supported", Assert.Single(result.Errors).Message);
        Assert.Empty(service.Requests);
        Assert.Empty(service.ExistenceChecks);   // 拒绝发生在覆写闸门之前
    }

    [Fact]
    public async Task Merge_EmptyFieldMappingsRoutesWithDefaultMapping()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("merge") };
        var result = await CallAsync(service, new MergeTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b", ["output"] = "C:/out/out.gdb/M", ["fieldMappings"] = " "
        });
        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Merge_management", request.ToolName);
        Assert.Equal("", request.Values![2]);    // 空白映射 = GP 默认
    }

    // ============================================================ D：set_map_extent.spatialReference 类型面

    [Fact]
    public void SetMapExtent_SpatialReferenceSchemaAllowsStringAndInteger()
    {
        var tool = new SetMapExtentTool();
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(tool.InputSchema));
        var type = document.RootElement
            .GetProperty("properties")
            .GetProperty("spatialReference")
            .GetProperty("type");
        Assert.Equal(JsonValueKind.Array, type.ValueKind);
        var values = type.EnumerateArray().Select(v => v.GetString()!).ToArray();
        Assert.Equal(new[] { "string", "integer" }, values);
    }

    [Fact]
    public async Task SetMapExtent_IntegerWkidIsForwardedAsInvariantString()
    {
        var maps = new RecordingMapService();
        var host = new FakeArcGISHost(maps: maps);
        var result = await CallAsync(host, new SetMapExtentTool(), new Dictionary<string, object?>
        {
            ["xMin"] = 0.0, ["yMin"] = 0.0, ["xMax"] = 1.0, ["yMax"] = 1.0,
            ["spatialReference"] = 4326
        });

        Assert.True(result.Success);
        Assert.Equal("4326", maps.SpatialReference);   // 修复前：整数被静默忽略（null）
    }

    [Fact]
    public async Task SetMapExtent_IntegralDoubleWkidIsForwarded()
    {
        var maps = new RecordingMapService();
        var host = new FakeArcGISHost(maps: maps);
        var result = await CallAsync(host, new SetMapExtentTool(), new Dictionary<string, object?>
        {
            ["xMin"] = 0.0, ["yMin"] = 0.0, ["xMax"] = 1.0, ["yMax"] = 1.0,
            ["spatialReference"] = 3857.0
        });

        Assert.True(result.Success);
        Assert.Equal("3857", maps.SpatialReference);
    }

    [Fact]
    public async Task SetMapExtent_FractionalNumberIsInvalidArgumentBeforeHost()
    {
        var maps = new RecordingMapService();
        var host = new FakeArcGISHost(maps: maps);
        var result = await CallAsync(host, new SetMapExtentTool(), new Dictionary<string, object?>
        {
            ["xMin"] = 0.0, ["yMin"] = 0.0, ["xMax"] = 1.0, ["yMax"] = 1.0,
            ["spatialReference"] = 4326.5
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Null(maps.SpatialReference);      // 未触达 Host（零写入）
    }

    [Fact]
    public async Task SetMapExtent_StringFormStillPassesThrough()
    {
        var maps = new RecordingMapService();
        var host = new FakeArcGISHost(maps: maps);
        var result = await CallAsync(host, new SetMapExtentTool(), new Dictionary<string, object?>
        {
            ["xMin"] = 0.0, ["yMin"] = 0.0, ["xMax"] = 1.0, ["yMax"] = 1.0,
            ["spatialReference"] = "WGS 1984"
        });

        Assert.True(result.Success);
        Assert.Equal("WGS 1984", maps.SpatialReference);
    }

    [Fact]
    public async Task SetMapExtent_OmittedSpatialReferenceStaysNull()
    {
        var maps = new RecordingMapService();
        var host = new FakeArcGISHost(maps: maps);
        var result = await CallAsync(host, new SetMapExtentTool(), new Dictionary<string, object?>
        {
            ["xMin"] = 0.0, ["yMin"] = 0.0, ["xMax"] = 1.0, ["yMax"] = 1.0
        });

        Assert.True(result.Success);
        Assert.Null(maps.SpatialReference);      // 省略 = 沿用地图自身 SR（契约不变）
    }

    // ============================================================ helpers

    private static FakePythonBridgeService MakeBridgeWithFields(params string[] names)
    {
        var bridge = new FakePythonBridgeService();
        bridge.ListFieldsResult = OperationResult<JsonElement?>.Ok(ParseFields(names));
        return bridge;
    }

    private static JsonElement ParseFields(params string[] names)
    {
        var payload = new Dictionary<string, object?>
        {
            ["fields"] = names.Select(n => (object?)new Dictionary<string, object?> { ["name"] = n }).ToList()
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(payload)).RootElement.Clone();
    }

    private static async Task<OperationResult<object?>> CallAsync(
        RecordingGeoprocessingService service,
        IMCPTool tool,
        IReadOnlyDictionary<string, object?> arguments,
        IPythonBridgeService? bridge = null)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(
            registry,
            new FakeArcGISHost(service),
            new MCPSettings(),
            NullLogger.Instance,
            bridge);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    private static async Task<OperationResult<object?>> CallAsync(
        FakeArcGISHost host,
        IMCPTool tool,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    private static OperationResult<GeoprocessingResult> GpOk(string result)
        => OperationResult<GeoprocessingResult>.Ok(new GeoprocessingResult { Result = result, ToolName = "test" });

    /// <summary>ListFields 序列化响应桥：第 1 次调用返回 first，之后返回 rest（B 项前/后照分态专用）。</summary>
    private sealed class SequencedListFieldsBridge : IPythonBridgeService
    {
        private readonly FakePythonBridgeService _inner = new();
        private readonly JsonElement? _first;
        private readonly JsonElement? _rest;

        public SequencedListFieldsBridge(JsonElement? first, JsonElement? rest)
        {
            _first = first;
            _rest = rest;
        }

        public int ListFieldsCallCount => _inner.ListFieldsCallCount;

        public Task<OperationResult<JsonElement?>> ListFieldsAsync(string datasetPath, CancellationToken ct = default)
        {
            var r = _inner.ListFieldsAsync(datasetPath, ct).ConfigureAwait(false).GetAwaiter().GetResult();
            var payload = _inner.ListFieldsCallCount == 1 ? _first : _rest;
            return Task.FromResult(OperationResult<JsonElement?>.Ok(payload));
        }

        public Task<OperationResult<string>> PingAsync(CancellationToken ct = default) => _inner.PingAsync(ct);
        public Task<OperationResult<JsonElement?>> GetRuntimeInfoAsync(CancellationToken ct = default) => _inner.GetRuntimeInfoAsync(ct);
        public Task<OperationResult<bool>> ArcpyExistsAsync(string path, CancellationToken ct = default) => _inner.ArcpyExistsAsync(path, ct);
        public Task<OperationResult<JsonElement?>> DescribeAsync(string path, CancellationToken ct = default) => _inner.DescribeAsync(path, ct);
        public Task<OperationResult<JsonElement?>> DatasetSummaryAsync(string path, CancellationToken ct = default) => _inner.DatasetSummaryAsync(path, ct);
        public Task<OperationResult<JsonElement?>> ListWorkspaceDatasetsAsync(
            string workspacePath, bool recursive = false, int maxDepth = 3, int maxItems = 500, CancellationToken ct = default)
            => _inner.ListWorkspaceDatasetsAsync(workspacePath, recursive, maxDepth, maxItems, ct);
        public Task<OperationResult<JsonElement?>> DatasetInfoAsync(string path, CancellationToken ct = default) => _inner.DatasetInfoAsync(path, ct);
        public Task<OperationResult<JsonElement?>> RasterInfoAsync(string datasetPath, CancellationToken ct = default) => _inner.RasterInfoAsync(datasetPath, ct);
        public Task<OperationResult<JsonElement?>> SelectByAttributeAsync(
            string? mapName, string layerName, string mode,
            IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default)
            => _inner.SelectByAttributeAsync(mapName, layerName, mode, oidList, where, ct);
        public Task<OperationResult<JsonElement?>> SelectByLocationAsync(
            string? mapName, string layerName, string? selectingLayerName,
            string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default)
            => _inner.SelectByLocationAsync(mapName, layerName, selectingLayerName, overlapType, searchDistance, searchDistanceUnit, mode, ct);
    }

    /// <summary>记录 SetMapExtentAsync 的 SR 透传值（D 项专项；其余成员最小实现）。</summary>
    private sealed class RecordingMapService : IMapService
    {
        public string? SpatialReference { get; private set; }

        public Task<OperationResult<MapInfo?>> GetCurrentMapAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapInfo?>.Ok(new MapInfo()));

        public Task<OperationResult<IReadOnlyList<MapInfo>>> GetMapsAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<MapInfo>>.Ok(new List<MapInfo>()));

        public Task<OperationResult<MapExtentInfo>> GetMapExtentAsync(string? mapName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapExtentInfo>.Ok(new MapExtentInfo
            {
                MapName = mapName ?? string.Empty,
                ExtentSource = "default-extent",
            }));

        public Task<OperationResult<MapExtentSetInfo>> SetMapExtentAsync(
            string? mapName, double xMin, double yMin, double xMax, double yMax, string? spatialReference,
            CancellationToken ct = default)
        {
            SpatialReference = spatialReference;
            return Task.FromResult(OperationResult<MapExtentSetInfo>.Ok(new MapExtentSetInfo
            {
                MapName = mapName ?? string.Empty,
                XMin = xMin,
                YMin = yMin,
                XMax = xMax,
                YMax = yMax,
                SpatialReferenceName = spatialReference,
                ReadBackXMin = xMin,
                ReadBackYMin = yMin,
                ReadBackXMax = xMax,
                ReadBackYMax = yMax,
                SameSource = true,
            }));
        }
    }
}
