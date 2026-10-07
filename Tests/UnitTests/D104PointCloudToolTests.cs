using System.Security.Cryptography;
using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>D-104 X15: read-only LAS 1.2 point format 0 subset against frozen synthetic inputs.</summary>
public sealed class D104PointCloudToolTests
{
    private static readonly string WorkspaceRoot = FindWorkspaceRoot();
    private static readonly string LasFixture = Path.Combine(
        WorkspaceRoot, "Benchmarks", "inputs", "x3", "x3_cloud_epoch1.las");

    private static ToolExecutionContext Context(params (string Key, object? Value)[] values)
        => new()
        {
            Arguments = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
        };

    private static string? ErrorCode(OperationResult<object?> result)
        => result.Success ? null : result.Errors.FirstOrDefault()?.Code;

    [Fact]
    public async Task InspectPointCloud_ReadsKnownLasSummaryWithoutChangingFixture()
    {
        var before = SHA256.HashData(await File.ReadAllBytesAsync(LasFixture));

        var result = await new InspectPointCloudTool().ExecuteAsync(
            Context(("input", LasFixture)));

        var after = SHA256.HashData(await File.ReadAllBytesAsync(LasFixture));
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.ToString())));
        Assert.Equal(before, after);

        var data = Assert.IsType<Dictionary<string, object?>>(result.Data);
        Assert.Equal("LAS", data["format"]);
        Assert.Equal("1.2", data["formatVersion"]);
        Assert.Equal(0, data["pointDataRecordFormat"]);
        Assert.Equal(2400L, data["totalPointCount"]);
        Assert.Equal(2400L, data["pointsRead"]);
        Assert.Equal(false, data["sampled"]);

        var classes = Assert.IsType<SortedDictionary<string, long>>(data["classificationCounts"]);
        Assert.Equal(1275L, classes["2"]);
        Assert.Equal(1000L, classes["5"]);
        Assert.Equal(125L, classes["6"]);

        var returns = Assert.IsType<SortedDictionary<string, long>>(data["returnNumberCounts"]);
        var returnsPerPoint = Assert.IsType<SortedDictionary<string, long>>(data["numberOfReturnsCounts"]);
        Assert.Equal(2400L, returns["1"]);
        Assert.Equal(1600L, returnsPerPoint["1"]);
        Assert.Equal(800L, returnsPerPoint["2"]);

        var bounds = Assert.IsType<Dictionary<string, object?>>(data["bounds"]);
        Assert.Equal(-0.5d, Assert.IsType<double>(bounds["minX"]), 8);
        Assert.Equal(19.4d, Assert.IsType<double>(bounds["maxX"]), 8);
        Assert.Equal(-0.45d, Assert.IsType<double>(bounds["minY"]), 8);
        Assert.Equal(19.45d, Assert.IsType<double>(bounds["maxY"]), 8);
        Assert.Equal(50d, Assert.IsType<double>(bounds["minZ"]), 8);
        Assert.Equal(59.8d, Assert.IsType<double>(bounds["maxZ"]), 8);
        Assert.Equal(true, data["headerBoundsMatch"]);

        var density = Assert.IsType<Dictionary<string, object?>>(data["density"]);
        Assert.Equal("computed", density["status"]);
        Assert.Equal("points per squared coordinate unit; CRS linear/angular units are not normalized", density["units"]);
        var area = Assert.IsType<double>(density["xyExtentAreaCoordinateUnitsSquared"]);
        var value = Assert.IsType<double>(density["value"]);
        Assert.Equal(2400d / area, value, 10);

        var crs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(data["crs"]);
        Assert.Equal("not_declared_in_supported_metadata", crs["status"]);
        Assert.Equal(new[] { "LAZ", "COPC" }, Assert.IsType<string[]>(data["notClaimedFormats"]));
    }

    [Fact]
    public async Task InspectPointCloud_ReadsTheDeclaredUpdatedInstance()
    {
        var updatedFixture = Path.Combine(WorkspaceRoot, "Benchmarks", "inputs", "x3", "x3_cloud_epoch1_updated.las");
        var before = SHA256.HashData(await File.ReadAllBytesAsync(updatedFixture));

        var result = await new InspectPointCloudTool().ExecuteAsync(Context(("input", updatedFixture)));

        var after = SHA256.HashData(await File.ReadAllBytesAsync(updatedFixture));
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.ToString())));
        Assert.Equal(before, after);
        var data = Assert.IsType<Dictionary<string, object?>>(result.Data);
        Assert.Equal(2600L, data["totalPointCount"]);
        Assert.Equal(2600L, data["pointsRead"]);
    }

    [Fact]
    public async Task InspectPointCloud_RejectsPointCountAboveRequestedLimitWithoutPartialResult()
    {
        var result = await new InspectPointCloudTool().ExecuteAsync(
            Context(("input", LasFixture), ("maxPoints", 2399)));

        Assert.Equal(ErrorCodes.InvalidArgument, ErrorCode(result));
        Assert.Null(result.Data);
        Assert.Contains("no partial summary", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InspectPointCloud_CanSkipDensityCalculation()
    {
        var result = await new InspectPointCloudTool().ExecuteAsync(
            Context(("input", LasFixture), ("computeDensity", false)));

        Assert.True(result.Success);
        var data = Assert.IsType<Dictionary<string, object?>>(result.Data);
        var density = Assert.IsType<Dictionary<string, object?>>(data["density"]);
        Assert.Equal("not_requested", density["status"]);
        Assert.Null(density["value"]);
    }

    [Fact]
    public async Task InspectPointCloud_RejectsMissingOrInvalidArguments()
    {
        var missing = await new InspectPointCloudTool().ExecuteAsync(Context());
        var zeroLimit = await new InspectPointCloudTool().ExecuteAsync(
            Context(("input", LasFixture), ("maxPoints", 0)));
        var fractionalLimit = await new InspectPointCloudTool().ExecuteAsync(
            Context(("input", LasFixture), ("maxPoints", 1.5d)));
        var stringDensity = await new InspectPointCloudTool().ExecuteAsync(
            Context(("input", LasFixture), ("computeDensity", "true")));

        Assert.Equal(ErrorCodes.InvalidArgument, ErrorCode(missing));
        Assert.Equal(ErrorCodes.InvalidArgument, ErrorCode(zeroLimit));
        Assert.Equal(ErrorCodes.InvalidArgument, ErrorCode(fractionalLimit));
        Assert.Equal(ErrorCodes.InvalidArgument, ErrorCode(stringDensity));
    }

    [Fact]
    public async Task InspectPointCloud_ReportsMissingInputFile()
    {
        var missing = Path.Combine(WorkspaceRoot, "Benchmarks", "inputs", "x3", "does-not-exist.las");
        var result = await new InspectPointCloudTool().ExecuteAsync(Context(("input", missing)));

        Assert.Equal(ErrorCodes.NotFound, ErrorCode(result));
    }

    [Fact]
    public async Task InspectPointCloud_HonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new ToolExecutionContext
        {
            Arguments = new Dictionary<string, object?> { ["input"] = LasFixture },
            CancellationToken = cancellation.Token,
        };

        var result = await new InspectPointCloudTool().ExecuteAsync(context);

        Assert.Equal(ErrorCodes.Cancelled, ErrorCode(result));
    }

    [Fact]
    public void InspectPointCloud_PublishesTheFrozenNativeReadContract()
    {
        var tool = new InspectPointCloudTool();
        var properties = Assert.IsType<Dictionary<string, object?>>(tool.InputSchema["properties"]);
        var frozenSchemaPath = Path.Combine(
            WorkspaceRoot, ".runtime", "evolution", "v5-f", "run-20260928-d082", "f03b-5-schemas", "inspect_point_cloud.schema.json");
        using var frozen = JsonDocument.Parse(File.ReadAllText(frozenSchemaPath));
        using var actual = JsonDocument.Parse(JsonSerializer.Serialize(tool.InputSchema));
        var expectedProperties = frozen.RootElement.GetProperty("properties");
        var actualProperties = actual.RootElement.GetProperty("properties");

        Assert.Equal("inspect_point_cloud", tool.Name);
        Assert.Equal(ToolCategories.Quality, tool.Metadata.Category);
        Assert.Equal(ExecutionTypes.Native, tool.Metadata.ExecutionType);
        Assert.True(tool.Metadata.RequiresArcGIS);
        Assert.False(actual.RootElement.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "input" }, Assert.IsType<string[]>(tool.InputSchema["required"]));
        Assert.Equal(
            expectedProperties.EnumerateObject().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal),
            actualProperties.EnumerateObject().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Contains("input", properties.Keys);
        Assert.Contains("computeDensity", properties.Keys);
        Assert.Contains("maxPoints", properties.Keys);
        foreach (var expected in expectedProperties.EnumerateObject())
        {
            var actualProperty = actualProperties.GetProperty(expected.Name);
            Assert.Equal(expected.Value.GetProperty("type").GetString(), actualProperty.GetProperty("type").GetString());
            Assert.Equal(expected.Value.GetProperty("description").GetString(), actualProperty.GetProperty("description").GetString());
            if (expected.Value.TryGetProperty("default", out var expectedDefault))
                Assert.Equal(expectedDefault.ToString(), actualProperty.GetProperty("default").ToString());
        }
    }

    private static string FindWorkspaceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var fixture = Path.Combine(directory.FullName, "Benchmarks", "inputs", "x3", "x3_cloud_epoch1.las");
            if (File.Exists(fixture)) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the workspace root from the test output directory.");
    }
}
