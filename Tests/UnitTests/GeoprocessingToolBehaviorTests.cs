using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 5.7.2 GP behavior tests. The recorder observes the Tool → Router →
/// IGeoprocessingService boundary; no ArcGIS Pro GP execution is attempted.
/// </summary>
public sealed class GeoprocessingToolBehaviorTests
{
    [Fact]
    public async Task Buffer_RoutesIndependentlyAndAssemblesExactValues()
    {
        var service = new RecordingGeoprocessingService
        {
            Result = Success("buffer-result")
        };
        var result = await CallAsync(
            service,
            new BufferTool(),
            new Dictionary<string, object?>
            {
                ["input"] = "roads",
                ["output"] = "roads_buffer",
                ["distance"] = 12,
                ["distanceUnit"] = "Kilometers",
                ["dissolve"] = true
            });

        Assert.True(result.Success);
        Assert.Equal("buffer-result", Assert.IsType<GeoprocessingResult>(result.Data).Result);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Buffer_analysis", request.ToolName);
        Assert.Equal(
            new[] { "roads", "roads_buffer", "12 Kilometers", "FULL", "ROUND", "ALL" },
            request.Values);
    }

    [Fact]
    public async Task Buffer_UsesCurrentOptionalDefaultsAndIgnoresUnknownFields()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(
            service,
            new BufferTool(),
            new Dictionary<string, object?>
            {
                ["input"] = "roads",
                ["output"] = "roads_buffer",
                ["distance"] = 5,
                ["unknown"] = "ignored"
            });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal(
            new[] { "roads", "roads_buffer", "5 Meters", "FULL", "ROUND", "NONE" },
            request.Values);
    }

    [Fact]
    public async Task Clip_RoutesIndependentlyWithProductionOrder()
    {
        var service = new RecordingGeoprocessingService
        {
            Result = Success("clip-result")
        };
        var result = await CallAsync(
            service,
            new ClipTool(),
            new Dictionary<string, object?>
            {
                ["input"] = "roads",
                ["clipFeatures"] = "boundary",
                ["output"] = "roads_clip"
            });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Clip_analysis", request.ToolName);
        Assert.Equal(new[] { "roads", "boundary", "roads_clip" }, request.Values);
    }

    [Fact]
    public async Task Intersect_RoutesIndependentlyWithMultipleInputs()
    {
        var service = new RecordingGeoprocessingService
        {
            Result = Success("intersect-result")
        };
        var result = await CallAsync(
            service,
            new IntersectTool(),
            new Dictionary<string, object?>
            {
                ["inputs"] = "roads;parcels",
                ["output"] = "road_parcel_intersection"
            });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Intersect_analysis", request.ToolName);
        // D-028（F11）：多值参数逐项加单引号（支持含空格的数据集路径；空项 → INVALID_ARGUMENT）。
        Assert.Equal(
            new[] { "'roads';'parcels'", "road_parcel_intersection", "ALL", string.Empty },
            request.Values);
    }

    [Fact]
    public async Task Dissolve_RoutesWithAndWithoutOptionalField()
    {
        var service = new RecordingGeoprocessingService
        {
            Result = Success("dissolve-result")
        };

        var withField = await CallAsync(
            service,
            new DissolveTool(),
            new Dictionary<string, object?>
            {
                ["input"] = "parcels",
                ["output"] = "parcels_dissolved",
                ["dissolveField"] = "ZONE"
            });

        Assert.True(withField.Success);
        var first = Assert.Single(service.Requests);
        Assert.Equal("Dissolve_management", first.ToolName);
        Assert.Equal(new[] { "parcels", "parcels_dissolved", "ZONE", string.Empty }, first.Values);

        service.Requests.Clear();
        var withoutField = await CallAsync(
            service,
            new DissolveTool(),
            new Dictionary<string, object?>
            {
                ["input"] = "parcels",
                ["output"] = "parcels_dissolved"
            });

        Assert.True(withoutField.Success);
        var second = Assert.Single(service.Requests);
        Assert.Equal(new[] { "parcels", "parcels_dissolved", string.Empty, string.Empty }, second.Values);
    }

    [Fact]
    public async Task GpToolsRejectToolSpecificEmptyAndWrongTypeBeforeService()
    {
        var service = new RecordingGeoprocessingService();

        var empty = await CallAsync(
            service,
            new ClipTool(),
            new Dictionary<string, object?>
            {
                ["input"] = " ",
                ["clipFeatures"] = "boundary",
                ["output"] = "out"
            });
        var wrongType = await CallAsync(
            service,
            new BufferTool(),
            new Dictionary<string, object?>
            {
                ["input"] = 123,
                ["output"] = "out",
                ["distance"] = "not-a-number"
            });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(empty.Errors).Code);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(wrongType.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task GpToolsPreserveServiceErrorCodesAndMessages()
    {
        var codes = new[]
        {
            ErrorCodes.GeoprocessingError,
            ErrorCodes.OutputExists,
            ErrorCodes.Cancelled
        };

        foreach (var code in codes)
        {
            var service = new RecordingGeoprocessingService
            {
                Result = OperationResult<GeoprocessingResult>.Fail(
                    code,
                    $"service message for {code}")
            };

            var result = await CallAsync(
                service,
                new BufferTool(),
                new Dictionary<string, object?>
                {
                    ["input"] = "roads",
                    ["output"] = "out",
                    ["distance"] = 1
                });

            var error = Assert.Single(result.Errors);
            Assert.Equal(code, error.Code);
            Assert.Equal($"service message for {code}", error.Message);
        }
    }

    [Fact]
    public async Task GpToolsPropagateCancellationTokenToService()
    {
        using var cts = new CancellationTokenSource();
        var service = new RecordingGeoprocessingService
        {
            Result = OperationResult<GeoprocessingResult>.Fail(
                ErrorCodes.Cancelled,
                "cancelled by GP service")
        };

        var result = await CallAsync(
            service,
            new IntersectTool(),
            new Dictionary<string, object?>
            {
                ["inputs"] = "A;B",
                ["output"] = "out"
            },
            cts.Token);

        Assert.Equal(ErrorCodes.Cancelled, Assert.Single(result.Errors).Code);
        Assert.Equal(cts.Token, Assert.Single(service.CancellationTokens));
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
