using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-048（Phase 11 第一批）空间分析 3 工具行为测试。
/// 与 GeoprocessingToolBehaviorTests 同法：recorder 观察 Tool → Router → IGeoprocessingService 边界，
/// 不触达真实 Pro GP。反证锚点 = <see cref="NearTool"/> 的 method 白名单（工具层业务校验，非 schema 层）。
/// </summary>
public sealed class SpatialAnalysisToolTests
{
    // ---------------- spatial_join ----------------

    [Fact]
    public async Task SpatialJoin_RoutesWithProductionDefaults()
    {
        var service = new RecordingGeoprocessingService { Result = Success("sj") };
        var result = await CallAsync(service, new SpatialJoinTool(), new Dictionary<string, object?>
        {
            ["targetFeatures"] = "parcels",
            ["joinFeatures"] = "zones",
            ["output"] = "parcels_sj"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("SpatialJoin_analysis", request.ToolName);
        Assert.Equal(
            new[] { "parcels", "zones", "parcels_sj", "JOIN_ONE_TO_ONE", "KEEP_ALL", "", "INTERSECT" },
            request.Values);
        Assert.Equal(["parcels_sj"], service.ExistenceChecks);   // 覆写闸门先于 GP
    }

    [Fact]
    public async Task SpatialJoin_ForwardsExplicitOptionsAndRadius()
    {
        var service = new RecordingGeoprocessingService { Result = Success("sj2") };
        var result = await CallAsync(service, new SpatialJoinTool(), new Dictionary<string, object?>
        {
            ["targetFeatures"] = "a",
            ["joinFeatures"] = "b",
            ["output"] = "out",
            ["joinOperation"] = "join_one_to_many",
            ["joinType"] = "keep_common",
            ["matchOption"] = "WITHIN_A_DISTANCE",
            ["searchRadius"] = 100,
            ["searchRadiusUnit"] = "Meters"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal(
            new[] { "a", "b", "out", "JOIN_ONE_TO_MANY", "KEEP_COMMON", "", "WITHIN_A_DISTANCE", "100 Meters" },
            request.Values);
    }

    [Theory]
    [InlineData("joinOperation", "MANY_TO_MANY")]
    [InlineData("joinType", "DROP_ALL")]
    public async Task SpatialJoin_RejectsWhitelistViolationsAtToolLayer(string key, string value)
    {
        var service = new RecordingGeoprocessingService();
        var args = new Dictionary<string, object?>
        {
            ["targetFeatures"] = "a",
            ["joinFeatures"] = "b",
            ["output"] = "out",
            [key] = value
        };
        var result = await CallAsync(service, new SpatialJoinTool(), args);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);   // 未触 GP
    }

    [Fact]
    public async Task SpatialJoin_RejectsNonPositiveRadius()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new SpatialJoinTool(), new Dictionary<string, object?>
        {
            ["targetFeatures"] = "a", ["joinFeatures"] = "b", ["output"] = "out", ["searchRadius"] = 0
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task SpatialJoin_MissingRequiredArgumentIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new SpatialJoinTool(), new Dictionary<string, object?>
        {
            ["targetFeatures"] = "a"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------- near ----------------

    [Fact]
    public async Task Near_CopiesFirstThenRunsNearOnTheCopy()
    {
        var service = new RecordingGeoprocessingService { Result = Success("near") };
        var result = await CallAsync(service, new NearTool(), new Dictionary<string, object?>
        {
            ["input"] = "wells",
            ["nearFeatures"] = "faults",
            ["output"] = "wells_near"
        });

        Assert.True(result.Success);
        Assert.Equal(2, service.Requests.Count);
        Assert.Equal("CopyFeatures_management", service.Requests[0].ToolName);
        Assert.Equal(new[] { "wells", "wells_near" }, service.Requests[0].Values);
        Assert.Equal("Near_analysis", service.Requests[1].ToolName);
        Assert.Equal(
            new[] { "wells_near", "faults", "", "NO_LOCATION", "NO_ANGLE", "PLANAR" },
            service.Requests[1].Values);
    }

    [Fact]
    public async Task Near_ForwardsRadiusLocationAngleMethod()
    {
        var service = new RecordingGeoprocessingService { Result = Success("near2") };
        var result = await CallAsync(service, new NearTool(), new Dictionary<string, object?>
        {
            ["input"] = "wells",
            ["nearFeatures"] = "faults",
            ["output"] = "out",
            ["searchRadius"] = 50,
            ["searchRadiusUnit"] = "Kilometers",
            ["location"] = true,
            ["angle"] = true,
            ["method"] = "geodesic"
        });

        Assert.True(result.Success);
        var near = service.Requests[1];
        Assert.Equal(
            new[] { "out", "faults", "50 Kilometers", "LOCATION", "ANGLE", "GEODESIC" },
            near.Values);
    }

    [Fact]
    public async Task Near_RejectsBadMethodAtToolLayer()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new NearTool(), new Dictionary<string, object?>
        {
            ["input"] = "wells", ["nearFeatures"] = "faults", ["output"] = "out", ["method"] = "SPHEROID"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Near_CopyFailureStopsBeforeNear()
    {
        var service = new RecordingGeoprocessingService
        {
            Result = OperationResult<GeoprocessingResult>.Fail(ErrorCodes.InternalError, "copy failed")
        };
        var result = await CallAsync(service, new NearTool(), new Dictionary<string, object?>
        {
            ["input"] = "wells", ["nearFeatures"] = "faults", ["output"] = "out"
        });

        Assert.False(result.Success);
        Assert.Single(service.Requests);                      // Near 未执行（O-D049-03：xUnit2013 → Assert.Single）
        Assert.Equal("CopyFeatures_management", service.Requests[0].ToolName);
    }

    [Fact]
    public async Task Near_OverwriteGateRefusesBeforeAnyGp()
    {
        var service = new RecordingGeoprocessingService
        {
            ExistenceResult = OutputExistence.Exists,
            ExistenceDetail = "pre-existing"
        };
        var result = await CallAsync(service, new NearTool(), new Dictionary<string, object?>
        {
            ["input"] = "wells", ["nearFeatures"] = "faults", ["output"] = "out"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);                        // 零写入：未触 GP
        Assert.Equal(["out"], service.ExistenceChecks);
    }

    // ---------------- raster_clip ----------------

    [Fact]
    public async Task RasterClip_RoutesWithDefaults()
    {
        var service = new RecordingGeoprocessingService { Result = Success("rc") };
        var result = await CallAsync(service, new RasterClipTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "dem",
            ["maskFeatures"] = "boundary",
            ["output"] = "dem_clip"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("ExtractByMask_sa", request.ToolName);
        // O-D048-07 修复后：GP 工具序 = (in_raster, in_mask_data, out_raster, extraction_area, analysis_extent)
        Assert.Equal(new[] { "dem", "boundary", "dem_clip", "INSIDE", "" }, request.Values);
    }

    [Fact]
    public async Task RasterClip_SupportsOutsideExtraction()
    {
        var service = new RecordingGeoprocessingService { Result = Success("rc2") };
        var result = await CallAsync(service, new RasterClipTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "dem", ["maskFeatures"] = "boundary", ["output"] = "out",
            ["extractionArea"] = "outside"
        });

        Assert.True(result.Success);
        Assert.Equal(new[] { "dem", "boundary", "out", "OUTSIDE", "" }, Assert.Single(service.Requests).Values);
    }

    [Fact]
    public async Task RasterClip_RejectsBadExtractionArea()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterClipTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "dem", ["maskFeatures"] = "boundary", ["output"] = "out",
            ["extractionArea"] = "BOTH"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task RasterClip_OverwriteGateRefusesBeforeAnyGp()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new RasterClipTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "dem", ["maskFeatures"] = "boundary", ["output"] = "out"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------- 通用 ----------------

    [Fact]
    public async Task SpatialToolsPropagateCancellationTokenToService()
    {
        using var cts = new CancellationTokenSource();
        var service = new RecordingGeoprocessingService
        {
            Result = OperationResult<GeoprocessingResult>.Fail(ErrorCodes.Cancelled, "cancelled by GP service")
        };
        var result = await CallAsync(service, new SpatialJoinTool(), new Dictionary<string, object?>
        {
            ["targetFeatures"] = "a", ["joinFeatures"] = "b", ["output"] = "out"
        }, cts.Token);

        Assert.Equal(ErrorCodes.Cancelled, Assert.Single(result.Errors).Code);
        Assert.Equal(cts.Token, Assert.Single(service.CancellationTokens));
    }

    [Fact]
    public void SpatialToolsAreRegisteredWithGpExecutionType()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new SpatialJoinTool());
        registry.Register(new NearTool());
        registry.Register(new RasterClipTool());

        var tools = registry.List();
        Assert.Equal(3, registry.Count);
        Assert.All(tools, t => Assert.Equal(ExecutionTypes.Geoprocessing, t.Metadata.ExecutionType));
        Assert.All(tools, t => Assert.Equal(ToolCategories.Analysis, t.Metadata.Category));
        Assert.All(tools, t => Assert.True(t.Metadata.RequiresArcGIS));
        Assert.Equal(
            new[] { "near", "raster_clip", "spatial_join" },
            tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
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
