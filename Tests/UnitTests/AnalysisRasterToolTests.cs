using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-049（Phase 11 第二批）Analysis/Raster 4 工具行为测试。
/// 与 GeoprocessingToolBehaviorTests 同法：recorder 观察 Tool → Router → IGeoprocessingService 边界，不触真实 Pro GP。
/// ★ 参数序断言全部来自 api-spike.md 的 A/B 实测结论（O-D048-07 教训）。
/// 反证锚点 = <see cref="RasterStatisticsTool"/> 的 propertyType 白名单（工具层业务校验，非 schema 层）。
/// </summary>
public sealed class AnalysisRasterToolTests
{
    // ---------------- erase ----------------

    [Fact]
    public async Task Erase_RoutesWithSpikeVerifiedOrder()
    {
        var service = new RecordingGeoprocessingService { Result = Success("erase") };
        var result = await CallAsync(service, new EraseTool(), new Dictionary<string, object?>
        {
            ["input"] = "parcels",
            ["eraseFeatures"] = "flood",
            ["output"] = "parcels_erase"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Erase_analysis", request.ToolName);
        // A/B 实测序：(in_features, erase_features, out_feature_class, {cluster_tolerance})
        Assert.Equal(new[] { "parcels", "flood", "parcels_erase", "" }, request.Values);
        Assert.Equal(["parcels_erase"], service.ExistenceChecks);   // 覆写闸门先于 GP
    }

    [Fact]
    public async Task Erase_MissingRequiredArgumentIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new EraseTool(), new Dictionary<string, object?>
        {
            ["input"] = "a"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Erase_OverwriteGateRefusesBeforeAnyGp()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new EraseTool(), new Dictionary<string, object?>
        {
            ["input"] = "a", ["eraseFeatures"] = "b", ["output"] = "out"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------- union ----------------

    [Fact]
    public async Task Union_RoutesWithDefaultsAndQuotedMultiValue()
    {
        var service = new RecordingGeoprocessingService { Result = Success("union") };
        var result = await CallAsync(service, new UnionTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "zonesA;zonesB",
            ["output"] = "union_out"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Union_analysis", request.ToolName);
        // A/B 实测序：(in_features, out_feature_class, {join_attributes}, {cluster_tolerance}, {gaps})
        Assert.Equal(new[] { "'zonesA';'zonesB'", "union_out", "ALL", "", "NO_GAPS" }, request.Values);
    }

    [Fact]
    public async Task Union_ForwardsJoinAttributesAndGaps()
    {
        var service = new RecordingGeoprocessingService { Result = Success("union2") };
        var result = await CallAsync(service, new UnionTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "A;B", ["output"] = "out",
            ["joinAttributes"] = "no_fid", ["gaps"] = true
        });

        Assert.True(result.Success);
        Assert.Equal(new[] { "'A';'B'", "out", "NO_FID", "", "GAPS" },
            Assert.Single(service.Requests).Values);
    }

    [Fact]
    public async Task Union_RejectsBadJoinAttributesAtToolLayer()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new UnionTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "A;B", ["output"] = "out", ["joinAttributes"] = "SOME_FID"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Theory]
    [InlineData("A;;B")]
    [InlineData(";")]
    public async Task Union_RejectsEmptyMultiValueItems(string inputs)
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new UnionTool(), new Dictionary<string, object?>
        {
            ["inputs"] = inputs, ["output"] = "out"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------- raster_resample ----------------

    [Fact]
    public async Task RasterResample_RoutesWithDefaults()
    {
        var service = new RecordingGeoprocessingService { Result = Success("rs") };
        var result = await CallAsync(service, new RasterResampleTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "dem", ["output"] = "dem_rs", ["cellSize"] = "2"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Resample_management", request.ToolName);
        // A/B 实测序：(in_raster, out_raster, {cell_size}, {resampling_type})
        Assert.Equal(new[] { "dem", "dem_rs", "2", "NEAREST" }, request.Values);
    }

    [Theory]
    [InlineData("bilinear")]
    [InlineData("cubic")]
    [InlineData("majority")]
    public async Task RasterResample_ForwardsResamplingType(string method)
    {
        var service = new RecordingGeoprocessingService { Result = Success("rs2") };
        var result = await CallAsync(service, new RasterResampleTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "dem", ["output"] = "out", ["cellSize"] = "2", ["resamplingType"] = method
        });

        Assert.True(result.Success);
        Assert.Equal(new[] { "dem", "out", "2", method.ToUpperInvariant() },
            Assert.Single(service.Requests).Values);
    }

    [Fact]
    public async Task RasterResample_RejectsBadResamplingType()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterResampleTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "dem", ["output"] = "out", ["cellSize"] = "2", ["resamplingType"] = "LANCZOS"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task RasterResample_MissingCellSizeIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterResampleTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "dem", ["output"] = "out"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------- raster_statistics（只读） ----------------

    [Fact]
    public async Task RasterStatistics_ReadOnlyNoOutputGate()
    {
        var service = new RecordingGeoprocessingService { Result = Success("0") };
        var result = await CallAsync(service, new RasterStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "dem"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("GetRasterProperties_management", request.ToolName);
        // A/B 实测序：(in_raster, {property_type}, {band_index})
        Assert.Equal(new[] { "dem", "MINIMUM", "" }, request.Values);
        Assert.Empty(service.ExistenceChecks);   // 只读工具无覆写闸门
    }

    [Fact]
    public async Task RasterStatistics_ForwardsPropertyAndBand()
    {
        var service = new RecordingGeoprocessingService { Result = Success("3") };
        var result = await CallAsync(service, new RasterStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "dem", ["propertyType"] = "bandcount", ["bandIndex"] = "1"
        });

        Assert.True(result.Success);
        Assert.Equal(new[] { "dem", "BANDCOUNT", "1" }, Assert.Single(service.Requests).Values);
    }

    [Fact]
    public async Task RasterStatistics_RejectsBadPropertyTypeAtToolLayer()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "dem", ["propertyType"] = "MEDIAN"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task RasterStatistics_MissingInputRasterIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterStatisticsTool(), new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------- 通用 ----------------

    [Fact]
    public void FourNewToolsAreRegisteredAsGeoprocessingAnalysis()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new EraseTool());
        registry.Register(new UnionTool());
        registry.Register(new RasterResampleTool());
        registry.Register(new RasterStatisticsTool());

        var tools = registry.List();
        Assert.Equal(4, registry.Count);
        Assert.All(tools, t => Assert.Equal(ExecutionTypes.Geoprocessing, t.Metadata.ExecutionType));
        Assert.All(tools, t => Assert.Equal(ToolCategories.Analysis, t.Metadata.Category));
        Assert.All(tools, t => Assert.True(t.Metadata.RequiresArcGIS));
        Assert.Equal(
            new[] { "erase", "raster_resample", "raster_statistics", "union" },
            tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        // 与 D-048 三工具无重名
        Assert.DoesNotContain("spatial_join", tools.Select(t => t.Name));
        Assert.DoesNotContain("raster_clip", tools.Select(t => t.Name));
    }

    [Fact]
    public void RasterClipDescriptionDisclosesGridSpacePathConstraint()
    {
        // 随车义务（O-D048-06 / G-122 裁定①）
        var tool = new RasterClipTool();
        Assert.Contains("GRID", tool.Description);
        Assert.Contains("不得含空格", tool.Description);
        Assert.Contains("文件 GDB", tool.Description);
    }

    private static async Task<OperationResult<object?>> CallAsync(
        RecordingGeoprocessingService service,
        IMCPTool tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(
            registry,
            new FakeArcGISHost(service),
            new MCPSettings(),
            NullLogger.Instance);

        return await router.ExecuteAsync(new MCPToolCall
        {
            Name = tool.Name,
            Arguments = arguments,
            CancellationToken = cancellationToken
        });
    }

    private static OperationResult<GeoprocessingResult> Success(string result)
        => OperationResult<GeoprocessingResult>.Ok(new GeoprocessingResult
        {
            Result = result,
            ToolName = "test"
        });
}
