using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-043（Phase 10 第二批）：地图设置写类三工具的 Fake 层单测。
/// 覆盖：① set_definition_query —— 设置逐字返回 / 空串=清除（非错误）/ 缺 layerName 校验 /
/// 不支持层 → INVALID_ARGUMENT（与 get 的 supports=false 同义）；
/// ② move_layer —— 位置枚举校验 / BEFORE·AFTER 缺 referenceLayer 校验 / 参数透传；
/// ③ set_map_extent —— 四至与 SR 透传 / 缺参校验 / 范围自洽校验 / readBack·sameSource 披露字段。
/// </summary>
public class MapSettingsToolTests
{
    private sealed class FakeLayerSettingsService : ILayerService
    {
        public string? FailCode { get; set; }
        public string? LastDefinitionQuery { get; private set; }
        public (string Layer, string? Reference, string Position)? LastMove { get; private set; }

        /// <summary>
        /// 替身持有的**根容器顺序**（D-044：替身必须复刻 SDK 语义，否则缺陷被放过 ——
        /// 历史替身只记录入参、恒返 <c>TargetIndex=0</c>，因此任何位移缺陷都不会变红）。
        /// </summary>
        public List<string> RootLayers { get; set; } = new() { "P_PTS", "Q_PTS", "E_PTS", "L_Group" };

        /// <summary>最近一次计算出的 target 索引（与真实实现同源：均出自 <see cref="MoveTargetIndex"/>）。</summary>
        public int LastTargetIndex { get; private set; }

        /// <summary>最近一次是否真的执行了移动（<c>s == r</c> 幂等时为 false）。</summary>
        public bool LastMoveInvoked { get; private set; }

        private static int IndexOfLayer(IReadOnlyList<string> root, string? name)
            => root.ToList().FindIndex(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        private static OperationResult<LayerOrderInfo> NotAtRoot(string layerName)
            => OperationResult<LayerOrderInfo>.Fail(
                ErrorCodes.InvalidArgument,
                $"Layer '{layerName}' is not at the root of the map TOC; moving nested/grouped layers is out of scope for this batch.");

        public Task<OperationResult<IReadOnlyList<LayerInfo>>> GetLayersAsync(string? mapName = null, bool flatten = true, CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<LayerInfo>>.Ok(new List<LayerInfo>()));

        public Task<OperationResult<LayerInfo?>> FindLayerAsync(string mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerInfo?>.Ok(null));

        public Task<OperationResult<LayerInfo?>> GetLayerInfoAsync(string mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerInfo?>.Ok(null));

        public Task<OperationResult<bool>> SetLayerVisibilityAsync(string mapName, string layerName, bool visible, CancellationToken ct = default)
            => Task.FromResult(OperationResult<bool>.Ok(true));

        public Task<OperationResult<LayerInfo?>> AddLayerAsync(string mapName, string layerPathOrUri, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerInfo?>.Ok(null));

        public Task<OperationResult<bool>> RemoveLayerAsync(string mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<bool>.Ok(true));

        public Task<OperationResult<LayerSymbologyInfo>> GetLayerSymbologyAsync(string? mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerSymbologyInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<SymbologySetInfo>> SetSimpleSymbologyAsync(string? mapName, string layerName, string? fillColor, string? outlineColor, double? pointSize, double? lineWidth, CancellationToken ct = default)
            => Task.FromResult(OperationResult<SymbologySetInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<LabelInfo>> GetLabelInfoAsync(string? mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LabelInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<LabelVisibilityInfo>> SetLabelVisibilityAsync(string? mapName, string layerName, bool enabled, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LabelVisibilityInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<DefinitionQueryInfo>> GetDefinitionQueryAsync(string? mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<DefinitionQueryInfo>.Ok(new DefinitionQueryInfo
            {
                LayerName = layerName,
                SupportsDefinitionQuery = true,
                DefinitionQuery = string.Empty,
            }));

        public Task<OperationResult<DefinitionQuerySetInfo>> SetDefinitionQueryAsync(
            string? mapName, string layerName, string? definitionQuery, CancellationToken ct = default)
        {
            LastDefinitionQuery = definitionQuery;
            if (FailCode is not null)
            {
                return Task.FromResult(OperationResult<DefinitionQuerySetInfo>.Fail(
                    FailCode, "Layer does not support a definition query."));
            }

            return Task.FromResult(OperationResult<DefinitionQuerySetInfo>.Ok(new DefinitionQuerySetInfo
            {
                LayerName = layerName,
                Applied = true,
                SupportsDefinitionQuery = true,
                // 逐字返回（写入后读回）
                DefinitionQuery = definitionQuery ?? string.Empty,
            }));
        }

        public Task<OperationResult<LayerOrderInfo>> MoveLayerAsync(
            string? mapName, string layerName, string? referenceLayer, string position, CancellationToken ct = default)
        {
            LastMove = (layerName, referenceLayer, position);
            return Task.FromResult(OperationResult<LayerOrderInfo>.Ok(new LayerOrderInfo
            {
                MapName = mapName ?? string.Empty,
                LayerName = layerName,
                Position = position.ToUpperInvariant(),
                TargetIndex = 0,
                RootLayerOrder = new[] { layerName },
            }));
        }
    }

    private sealed class FakeMapSettingsService : IMapService
    {
        public double XMin { get; private set; }
        public double YMin { get; private set; }
        public double XMax { get; private set; }
        public double YMax { get; private set; }
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
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
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

    private static async Task<OperationResult<object?>> CallAsync(
        FakeArcGISHost host, IMCPTool tool, IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    private static (FakeArcGISHost Host, FakeLayerSettingsService Layers) BuildLayerHost()
    {
        var layers = new FakeLayerSettingsService();
        var host = new FakeArcGISHost(layers: layers);
        return (host, layers);
    }

    // ---------- set_definition_query ----------

    [Fact]
    public async Task SetDefinitionQuery_SetsAndReturnsVerbatim()
    {
        var (host, layers) = BuildLayerHost();
        var tool = new SetDefinitionQueryTool();
        var args = new Dictionary<string, object?>
        {
            ["mapName"] = "L_Map",
            ["layerName"] = "Q_PTS",
            ["definitionQuery"] = "TAG = 'A'",
        };

        var r = await CallAsync(host, tool, args);

        Assert.True(r.Success);
        Assert.Equal("TAG = 'A'", layers.LastDefinitionQuery);
        var data = Assert.IsType<DefinitionQuerySetInfo>(r.Data);
        Assert.Equal("TAG = 'A'", data.DefinitionQuery);
        Assert.True(data.Applied);
        Assert.True(data.SupportsDefinitionQuery);
    }

    [Fact]
    public async Task SetDefinitionQuery_EmptyClearsAndIsNotAnError()
    {
        var (host, layers) = BuildLayerHost();
        var tool = new SetDefinitionQueryTool();
        var args = new Dictionary<string, object?>
        {
            ["layerName"] = "Q_PTS",
            ["definitionQuery"] = string.Empty,
        };

        var r = await CallAsync(host, tool, args);

        Assert.True(r.Success);
        Assert.Equal(string.Empty, layers.LastDefinitionQuery);
        var data = Assert.IsType<DefinitionQuerySetInfo>(r.Data);
        Assert.Equal(string.Empty, data.DefinitionQuery);
    }

    [Fact]
    public async Task SetDefinitionQuery_OmittedQueryIsTreatedAsClear()
    {
        var (host, layers) = BuildLayerHost();
        var tool = new SetDefinitionQueryTool();
        var args = new Dictionary<string, object?> { ["layerName"] = "Q_PTS" };

        var r = await CallAsync(host, tool, args);

        Assert.True(r.Success);
        Assert.Null(layers.LastDefinitionQuery);
        var data = Assert.IsType<DefinitionQuerySetInfo>(r.Data);
        Assert.Equal(string.Empty, data.DefinitionQuery);
    }

    [Fact]
    public async Task SetDefinitionQuery_MissingLayerNameIsInvalidArgument()
    {
        var (host, _) = BuildLayerHost();
        var tool = new SetDefinitionQueryTool();
        var args = new Dictionary<string, object?>
        {
            ["definitionQuery"] = "TAG = 'A'",
        };

        var r = await CallAsync(host, tool, args);

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    [Fact]
    public async Task SetDefinitionQuery_UnsupportedLayerIsInvalidArgument()
    {
        var layers = new FakeLayerSettingsService { FailCode = ErrorCodes.InvalidArgument };
        var host = new FakeArcGISHost(layers: layers);
        var tool = new SetDefinitionQueryTool();
        var args = new Dictionary<string, object?>
        {
            ["layerName"] = "L_Group",
            ["definitionQuery"] = "1=1",
        };

        var r = await CallAsync(host, tool, args);

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    // ---------- move_layer ----------

    [Fact]
    public async Task MoveLayer_TopPassesThroughAndNormalizesPosition()
    {
        var (host, layers) = BuildLayerHost();
        var tool = new MoveLayerTool();
        var args = new Dictionary<string, object?>
        {
            ["mapName"] = "L_Map",
            ["layerName"] = "Q_PTS",
            ["position"] = "top",
        };

        var r = await CallAsync(host, tool, args);

        Assert.True(r.Success);
        var data = Assert.IsType<LayerOrderInfo>(r.Data);
        Assert.Equal("TOP", data.Position);
        Assert.Equal("Q_PTS", layers.LastMove!.Value.Layer);
    }

    [Fact]
    public async Task MoveLayer_BeforeWithoutReferenceIsInvalidArgument()
    {
        var (host, _) = BuildLayerHost();
        var tool = new MoveLayerTool();
        var args = new Dictionary<string, object?>
        {
            ["layerName"] = "Q_PTS",
            ["position"] = "BEFORE",
        };

        var r = await CallAsync(host, tool, args);

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    [Fact]
    public async Task MoveLayer_UnknownPositionIsInvalidArgument()
    {
        var (host, _) = BuildLayerHost();
        var tool = new MoveLayerTool();
        var args = new Dictionary<string, object?>
        {
            ["layerName"] = "Q_PTS",
            ["position"] = "MIDDLE",
        };

        var r = await CallAsync(host, tool, args);

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    [Fact]
    public async Task MoveLayer_AfterWithReferencePassesBoth()
    {
        var (host, layers) = BuildLayerHost();
        var tool = new MoveLayerTool();
        var args = new Dictionary<string, object?>
        {
            ["layerName"] = "Q_PTS",
            ["position"] = "AFTER",
            ["referenceLayer"] = "E_PTS",
        };

        var r = await CallAsync(host, tool, args);

        Assert.True(r.Success);
        Assert.Equal("Q_PTS", layers.LastMove!.Value.Layer);
        Assert.Equal("E_PTS", layers.LastMove!.Value.Reference);
        Assert.Equal("AFTER", layers.LastMove!.Value.Position);
    }

    // ---------- set_map_extent ----------

    [Fact]
    public async Task SetMapExtent_PassesExtentAndSpatialReferenceThrough()
    {
        var maps = new FakeMapSettingsService();
        var host = new FakeArcGISHost(maps: maps);
        var tool = new SetMapExtentTool();
        var args = new Dictionary<string, object?>
        {
            ["mapName"] = "L_Map",
            ["xMin"] = -1.0,
            ["yMin"] = -1.0,
            ["xMax"] = 3.0,
            ["yMax"] = 7.0,
            ["spatialReference"] = "4326",
        };

        var r = await CallAsync(host, tool, args);

        Assert.True(r.Success);
        Assert.Equal(-1.0, maps.XMin);
        Assert.Equal(7.0, maps.YMax);
        Assert.Equal("4326", maps.SpatialReference);
        var data = Assert.IsType<MapExtentSetInfo>(r.Data);
        Assert.Equal("default-extent", data.ReadBackSource);
        Assert.True(data.SameSource);
        Assert.Equal("Map.SetCustomFullExtent", data.WriteTarget);
    }

    [Fact]
    public async Task SetMapExtent_MissingCoordinateIsInvalidArgument()
    {
        var host = new FakeArcGISHost(maps: new FakeMapSettingsService());
        var tool = new SetMapExtentTool();
        var args = new Dictionary<string, object?>
        {
            ["xMin"] = 0.0,
            ["yMin"] = 0.0,
            ["xMax"] = 1.0,
        };

        var r = await CallAsync(host, tool, args);

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    [Fact]
    public async Task SetMapExtent_InvertedExtentIsInvalidArgument()
    {
        var host = new FakeArcGISHost(maps: new FakeMapSettingsService());
        var tool = new SetMapExtentTool();
        var args = new Dictionary<string, object?>
        {
            ["xMin"] = 5.0,
            ["yMin"] = 0.0,
            ["xMax"] = 1.0,
            ["yMax"] = 1.0,
        };

        var r = await CallAsync(host, tool, args);

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }
}
