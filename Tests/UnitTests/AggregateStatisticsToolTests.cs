using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-060（Phase 15 功能批 · B 项，G-160 批准，78 → 80）：统计聚合 2 工具行为测试。
/// 与 AnalysisRasterToolTests 同法：recorder 观察 Tool → Router → IGeoprocessingService 边界，不触真实 Pro GP。
/// ★ 参数序断言来自 D-052 spike 实测（`e-spike-aggregates.json`）：
///   CellStatistics_sa(in;in, out, 'MEAN', 'DATA') / FocalStatistics 3×3 CELL 矩形 MEAN。
/// 反证锚点 = <see cref="FocalStatisticsTool"/> 的 neighborhood 白名单与规范化（工具层业务校验，非 schema 层）＋
/// <see cref="AggregateStatisticsToolNotes.TryNormalizeNeighborhood"/> 的形态/元数/数值校验。
/// </summary>
public sealed class AggregateStatisticsToolTests
{
    // ════════════════ cell_statistics ════════════════

    [Fact]
    public async Task CellStatistics_RoutesWithSpikeVerifiedOrderAndDefaults()
    {
        var service = new RecordingGeoprocessingService { Result = Success("cell") };
        var result = await CallAsync(service, new CellStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRasters"] = "r1;r2",
            ["output"] = "cell_out"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("CellStatistics_sa", request.ToolName);
        // spike 实测序：(in_rasters…, out_raster, statistics_type, ignore_nodata)；默认 MEAN / DATA
        // 多值参数逐项加引号（GpMultiValueBuilder，F11/Union 同法）
        Assert.Equal(new[] { "'r1';'r2'", "cell_out", "MEAN", "DATA" }, request.Values);
        Assert.Equal(["cell_out"], service.ExistenceChecks);   // 覆写闸门先于 GP
    }

    [Fact]
    public async Task CellStatistics_MissingRequiredArgumentIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new CellStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRasters"] = "r1;r2"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task CellStatistics_ProtectedOutputPathIsRefusedBeforeAnyGp()
    {
        var service = new RecordingGeoprocessingService { Result = Success("cell") };
        var result = await CallAsync(service, new CellStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRasters"] = "r1",
            ["output"] = ProtectedFixtureOutput
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);          // 守卫先于 GP：零副作用
        Assert.Empty(service.ExistenceChecks);   // 且先于覆写闸门的探测
    }

    [Fact]
    public async Task CellStatistics_OverwriteGateRefusesBeforeAnyGp()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new CellStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRasters"] = "r1",
            ["output"] = "out"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task CellStatistics_ExplicitOverwriteAllowsExistingOutput()
    {
        var service = new RecordingGeoprocessingService
        {
            Result = Success("cell"),
            ExistenceResult = OutputExistence.Exists
        };
        var result = await CallAsync(service, new CellStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRasters"] = "r1",
            ["output"] = "out",
            ["overwrite"] = true
        });

        Assert.True(result.Success);
        Assert.Single(service.Requests);
    }

    [Fact]
    public async Task CellStatistics_InvalidStatisticsTypeIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new CellStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRasters"] = "r1",
            ["output"] = "out",
            ["statisticsType"] = "AVERAGE"     // 非白名单
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task CellStatistics_StatisticsTypeIsTrimmedAndUpperInvariant()
    {
        var service = new RecordingGeoprocessingService { Result = Success("cell") };
        var result = await CallAsync(service, new CellStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRasters"] = "r1;r2",
            ["output"] = "out",
            ["statisticsType"] = "  sum  ",
            ["ignoreNoData"] = "nodata"
        });

        Assert.True(result.Success);
        Assert.Equal(new[] { "'r1';'r2'", "out", "SUM", "NODATA" }, Assert.Single(service.Requests).Values);
    }

    [Fact]
    public async Task CellStatistics_InvalidIgnoreNoDataIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new CellStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRasters"] = "r1",
            ["output"] = "out",
            ["ignoreNoData"] = "MAYBE"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task CellStatistics_MultiValueWithEmptyItemIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new CellStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRasters"] = "r1;;r2",
            ["output"] = "out"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ════════════════ focal_statistics ════════════════

    [Fact]
    public async Task FocalStatistics_RoutesWithDefaultNeighborhood()
    {
        var service = new RecordingGeoprocessingService { Result = Success("focal") };
        var result = await CallAsync(service, new FocalStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "r1",
            ["output"] = "focal_out"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("FocalStatistics_sa", request.ToolName);
        // spike 实测默认邻域：3×3 矩形（像元单位）⇒ "Rectangle 3 3 CELL"
        Assert.Equal(new[] { "r1", "focal_out", "Rectangle 3 3 CELL", "MEAN", "DATA" }, request.Values);
        Assert.Equal(["focal_out"], service.ExistenceChecks);
    }

    [Fact]
    public async Task FocalStatistics_CustomNeighborhoodIsNormalized()
    {
        var service = new RecordingGeoprocessingService { Result = Success("focal") };
        var result = await CallAsync(service, new FocalStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "r1",
            ["output"] = "out",
            ["neighborhood"] = "  rectangle   5 7   cell ",
            ["statisticsType"] = "maximum"
        });

        Assert.True(result.Success);
        // 规范化：形态首字母大写、空白折叠、单位大写
        Assert.Equal(new[] { "r1", "out", "Rectangle 5 7 CELL", "MAXIMUM", "DATA" },
                     Assert.Single(service.Requests).Values);
    }

    [Theory]
    [InlineData("Square 3 3 CELL")]        // 未知形态
    [InlineData("Rectangle 3 CELL")]       // 元数不足
    [InlineData("Rectangle 3 3 3 CELL")]   // 元数过多
    [InlineData("Circle 5 METERS")]        // 单位非法
    [InlineData("Circle abc CELL")]        // 数值非法
    [InlineData("Irregular")]              // Irregular 缺文件
    public async Task FocalStatistics_InvalidNeighborhoodIsInvalidArgument(string neighborhood)
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new FocalStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "r1",
            ["output"] = "out",
            ["neighborhood"] = neighborhood
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task FocalStatistics_IrregularNeighborhoodWithFileIsAccepted()
    {
        var service = new RecordingGeoprocessingService { Result = Success("focal") };
        var result = await CallAsync(service, new FocalStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "r1",
            ["output"] = "out",
            ["neighborhood"] = "irregular D:/tmp/kernel.txt"
        });

        Assert.True(result.Success);
        Assert.Equal(new[] { "r1", "out", "Irregular D:/tmp/kernel.txt", "MEAN", "DATA" },
                     Assert.Single(service.Requests).Values);
    }

    [Fact]
    public async Task FocalStatistics_ProtectedOutputPathIsRefusedBeforeAnyGp()
    {
        var service = new RecordingGeoprocessingService { Result = Success("focal") };
        var result = await CallAsync(service, new FocalStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "r1",
            ["output"] = ProtectedFixtureOutput
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
        Assert.Empty(service.ExistenceChecks);
    }

    [Fact]
    public async Task FocalStatistics_OverwriteGateRefusesBeforeAnyGp()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new FocalStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "r1",
            ["output"] = "out"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task FocalStatistics_MissingRequiredArgumentIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new FocalStatisticsTool(), new Dictionary<string, object?>
        {
            ["output"] = "out"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task FocalStatistics_InvalidStatisticsTypeIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new FocalStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "r1",
            ["output"] = "out",
            ["statisticsType"] = "AVG"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task FocalStatistics_InvalidIgnoreNoDataIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new FocalStatisticsTool(), new Dictionary<string, object?>
        {
            ["inputRaster"] = "r1",
            ["output"] = "out",
            ["ignoreNoData"] = "NULL"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ════════════════ 纯函数判据（反证锚点）════════════════

    [Theory]
    [InlineData("Rectangle 3 3 CELL", "Rectangle 3 3 CELL")]
    [InlineData("circle 2 map", "Circle 2 MAP")]
    [InlineData("Annulus 1 5 CELL", "Annulus 1 5 CELL")]
    [InlineData("Wedge 4 0 90 CELL", "Wedge 4 0 90 CELL")]
    [InlineData("Irregular D:/k.txt", "Irregular D:/k.txt")]
    public void NeighborhoodNormalizationAcceptsWhitelistedShapes(string raw, string expected)
    {
        Assert.True(AggregateStatisticsToolNotes.TryNormalizeNeighborhood(raw, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Hexagon 3 CELL")]
    [InlineData("Rectangle 3 3")]
    public void NeighborhoodNormalizationRejectsInvalidInput(string? raw)
    {
        Assert.False(AggregateStatisticsToolNotes.TryNormalizeNeighborhood(raw, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    // ════════════════ 注册与披露 ════════════════

    [Fact]
    public void AggregateToolsAreRegisteredAsAnalysisGeoprocessing()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new CellStatisticsTool());
        registry.Register(new FocalStatisticsTool());

        var tools = registry.List();
        Assert.Equal(2, registry.Count);
        Assert.All(tools, t => Assert.Equal(ExecutionTypes.Geoprocessing, t.Metadata.ExecutionType));
        Assert.All(tools, t => Assert.Equal(ToolCategories.Analysis, t.Metadata.Category));
        Assert.All(tools, t => Assert.True(t.Metadata.RequiresArcGIS));
        Assert.Equal(new[] { "cell_statistics", "focal_statistics" },
                     tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        // 与既有工具无重名
        Assert.DoesNotContain("raster_statistics", tools.Select(t => t.Name));
        Assert.DoesNotContain("raster_calc", tools.Select(t => t.Name));
    }

    [Fact]
    public void AggregateDescriptionsDiscloseLicenseSpikeAndGuard()
    {
        var cell = new CellStatisticsTool();
        var focal = new FocalStatisticsTool();

        foreach (var description in new[] { cell.Description, focal.Description })
        {
            Assert.Contains("Spatial Analyst", description);   // 许可依赖披露
            Assert.Contains("文件 GDB", description);           // 输出路径约束披露（O-D048-06）
        }
        Assert.Contains("2.5", cell.Description);              // spike 数值披露（MEAN=2.5）
        Assert.Contains("对象形态", focal.Description);         // 风险披露：GP 字符串形态
        Assert.Contains(FocalStatisticsTool.DefaultNeighborhood, focal.Description);
    }

    // ════════════════ helpers ════════════════

    /// <summary>受保护根内路径（与 D052 守卫统一接入测试同源样例）。</summary>
    private const string ProtectedFixtureOutput =
        "D:/ArcGIS-Pro-MCP 2.0/TestFixtures/Phase8_5_6/WorkBuddyTest.gdb/D060AggX";

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
