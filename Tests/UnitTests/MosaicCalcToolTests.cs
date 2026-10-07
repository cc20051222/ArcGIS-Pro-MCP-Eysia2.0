using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-050（Phase 11 第三批 · 收官批）栅格镶嵌 + 受约束计算 2 工具行为测试。
/// 与 GeoprocessingToolBehaviorTests 同法：recorder 观察 Tool → Router → IGeoprocessingService 边界，不触真实 Pro GP。
/// ★ 参数序断言全部来自 api-spike.md 的 A/B 实测结论（O-D048-07 教训）。
/// 反证锚点 = <see cref="RasterCalcTool"/> 的表达式白名单（工具层业务校验，非 schema 层）。
/// </summary>
public sealed class MosaicCalcToolTests
{
    // ---------------- raster_mosaic ----------------

    [Fact]
    public async Task Mosaic_RoutesWithSpikeVerifiedOrderAndComposedOutput()
    {
        var service = new RecordingGeoprocessingService { Result = Success("mosaic") };
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "C:/x.gdb/R1;C:/x.gdb/R2",
            ["output"] = "C:/out/out.gdb/MOS",
            ["numberOfBands"] = "1",
            ["cellSize"] = "1",
            ["pixelType"] = "32_BIT_FLOAT",
            ["mosaicMethod"] = "MEAN"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("MosaicToNewRaster_management", request.ToolName);
        // A/B 实测序：(in_rasters, out_loc, name, {sr}, {pixel}, {cell}, {bands}, {method}, {cmap})
        Assert.Equal(
            new[] { "'C:/x.gdb/R1';'C:/x.gdb/R2'", "C:/out/out.gdb", "MOS", "", "32_BIT_FLOAT", "1", "1", "MEAN", "" },
            request.Values!);
        // 覆写闸门先于 GP，且探测目标是**组合后的输出路径**
        Assert.Equal(["C:/out/out.gdb/MOS"], service.ExistenceChecks);
    }

    [Fact]
    public async Task Mosaic_OptionalParametersDefaultToEmptyStrings()
    {
        var service = new RecordingGeoprocessingService { Result = Success("mosaic2") };
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b",
            ["output"] = "C:/out/out.gdb/M2",
            ["numberOfBands"] = "1"
        });

        Assert.True(result.Success);
        Assert.Equal(new[] { "'a';'b'", "C:/out/out.gdb", "M2", "", "", "", "1", "", "" }, Assert.Single(service.Requests).Values!);
    }

    [Fact]
    public async Task Mosaic_RequiresAtLeastTwoInputs()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "only_one",
            ["output"] = "C:/out/out.gdb/M3",
            ["numberOfBands"] = "1"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Mosaic_RejectsEmptyMultiValueItem()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;;b",
            ["output"] = "C:/out/out.gdb/M4",
            ["numberOfBands"] = "1"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Mosaic_RejectsOutputOutsideFileGdb()
    {
        // 工具层业务校验②：目录型输出按 GRID 处理（O-D048-06），本工具要求文件 GDB
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b",
            ["output"] = "C:/out/MOS_DIR",
            ["numberOfBands"] = "1"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
        Assert.Empty(service.ExistenceChecks);
    }

    [Fact]
    public async Task Mosaic_RequiresNumberOfBands()
    {
        // 实测依据：缺 number_of_bands → GP ERROR 000735（api-spike-v7）
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b",
            ["output"] = "C:/out/out.gdb/M5"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Theory]
    [InlineData("FLOAT")]          // 实测非法（000800）—— 合法值为 32_BIT_FLOAT 等枚举
    [InlineData("12_BIT")]
    public async Task Mosaic_RejectsPixelTypeOutsideEvidenceBasedWhitelist(string pixelType)
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b",
            ["output"] = "C:/out/out.gdb/M6",
            ["numberOfBands"] = "1",
            ["pixelType"] = pixelType
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Mosaic_RejectsUnknownMosaicMethod()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b",
            ["output"] = "C:/out/out.gdb/M7",
            ["numberOfBands"] = "1",
            ["mosaicMethod"] = "AVERAGE"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Mosaic_OverwriteGateRefusesBeforeAnyGp()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b",
            ["output"] = "C:/out/out.gdb/M8",
            ["numberOfBands"] = "1"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------- 输出路径守卫（O-D049-08 试点） ----------------

    [Fact]
    public async Task Mosaic_ProtectedFixtureOutputIsRejectedBeforeGp()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b",
            ["output"] = "D:/ArcGIS-Pro-MCP 2.0/TestFixtures/Phase8_5_6/WorkBuddyTest.gdb/M9",
            ["numberOfBands"] = "1"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
        Assert.Empty(service.ExistenceChecks);   // 守卫先于覆写闸门
    }

    [Fact]
    public async Task Mosaic_ProtectedPathViaTraversalIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b",
            ["output"] = "C:/tmp/out.gdb/../../../TestFixtures/Phase8_5_6/x.gdb/M10",
            ["numberOfBands"] = "1"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Mosaic_LegacyRepoOutputIsRejected()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterMosaicTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "a;b",
            ["output"] = "D:/ArcGIS-Pro-MCP/out.gdb/M11",
            ["numberOfBands"] = "1"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------- raster_calc ----------------

    [Fact]
    public async Task Calc_RebuildsExpressionFromWhitelistWithRawRasterPaths()
    {
        var service = new RecordingGeoprocessingService { Result = Success("calc") };
        var result = await CallAsync(service, new RasterCalcTool(), new Dictionary<string, object?>
        {
            ["rasters"] = @"a=D:/x.gdb/S_R1;b=D:/x.gdb/S_R2",
            ["expression"] = "Con(a > 2, a, b)",
            ["output"] = "C:/out/out.gdb/C1"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("RasterCalculator_sa", request.ToolName);
        // A/B 实测序：(expression, out_raster)；表达式由白名单 token 重建（别名 → Raster(r"path") 注入形态）
        Assert.Equal(
            "Con ( Raster(r\"D:/x.gdb/S_R1\") > 2 , Raster(r\"D:/x.gdb/S_R1\") , Raster(r\"D:/x.gdb/S_R2\") )",
            request.Values![0]);
        Assert.Equal("C:/out/out.gdb/C1", request.Values![1]);
    }

    [Fact]
    public async Task Calc_ArithmeticAndWhitelistedFunctionsAreAccepted()
    {
        var service = new RecordingGeoprocessingService { Result = Success("calc2") };
        var result = await CallAsync(service, new RasterCalcTool(), new Dictionary<string, object?>
        {
            ["rasters"] = @"a=D:/x.gdb/S_R1",
            ["expression"] = "SquareRoot(Abs(a) ** 2) + 1.5",
            ["output"] = "C:/out/out.gdb/C2"
        });

        Assert.True(result.Success);
        Assert.Equal("SquareRoot ( Abs ( Raster(r\"D:/x.gdb/S_R1\") ) ** 2 ) + 1.5", Assert.Single(service.Requests).Values![0]);
    }

    [Theory]
    [InlineData("__import__('os').system('echo pwn')")]
    [InlineData("a + 1; import os")]
    [InlineData("open(r'D:/tmp/x','w').write('x')")]
    [InlineData("a.__class__")]
    [InlineData("exec('x=1') or a")]
    [InlineData("a\n+ 1")]
    [InlineData("'D:/x.gdb/S_R1' + a")]
    public async Task Calc_RejectsNonWhitelistedConstructsBeforeGp(string expression)
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterCalcTool(), new Dictionary<string, object?>
        {
            ["rasters"] = @"a=D:/x.gdb/S_R1",
            ["expression"] = expression,
            ["output"] = "C:/out/out.gdb/C3"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
        Assert.Empty(service.ExistenceChecks);
    }

    [Fact]
    public async Task Calc_RejectsUnknownIdentifier()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterCalcTool(), new Dictionary<string, object?>
        {
            ["rasters"] = @"a=D:/x.gdb/S_R1",
            ["expression"] = "a + undeclared",
            ["output"] = "C:/out/out.gdb/C4"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Calc_RejectsNonWhitelistedFunctionName()
    {
        // Min/Max/Sqrt 实测**不在** RCEXEC 命名空间（api-spike-v6 dump）⇒ 不放行
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterCalcTool(), new Dictionary<string, object?>
        {
            ["rasters"] = @"a=D:/x.gdb/S_R1;b=D:/x.gdb/S_R2",
            ["expression"] = "Min(a, b)",
            ["output"] = "C:/out/out.gdb/C5"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Calc_RejectsAliasThatShadowsWhitelistedFunction()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterCalcTool(), new Dictionary<string, object?>
        {
            ["rasters"] = @"Con=D:/x.gdb/S_R1",
            ["expression"] = "Con + 1",
            ["output"] = "C:/out/out.gdb/C6"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Calc_RejectsMalformedRasterMapping()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterCalcTool(), new Dictionary<string, object?>
        {
            ["rasters"] = "D:/x.gdb/S_R1",
            ["expression"] = "a + 1",
            ["output"] = "C:/out/out.gdb/C7"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Calc_RejectsRasterPathWithQuoteCharacters()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterCalcTool(), new Dictionary<string, object?>
        {
            ["rasters"] = "a=D:/x.gdb/S\"_R1",
            ["expression"] = "a + 1",
            ["output"] = "C:/out/out.gdb/C8"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Calc_ProtectedOutputIsRejectedBeforeGp()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterCalcTool(), new Dictionary<string, object?>
        {
            ["rasters"] = @"a=D:/x.gdb/S_R1",
            ["expression"] = "a + 1",
            ["output"] = "D:/ArcGIS-Pro-MCP 2.0/TestFixtures/Phase8_5_6/WorkBuddyTest.gdb/C9"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
        Assert.Empty(service.ExistenceChecks);
    }

    [Fact]
    public async Task Calc_OverwriteGateRefusesBeforeAnyGp()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new RasterCalcTool(), new Dictionary<string, object?>
        {
            ["rasters"] = @"a=D:/x.gdb/S_R1",
            ["expression"] = "a + 1",
            ["output"] = "C:/out/out.gdb/C10"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Calc_RequiresAllMandatoryArguments()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RasterCalcTool(), new Dictionary<string, object?>
        {
            ["expression"] = "a + 1",
            ["output"] = "C:/out/out.gdb/C11"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------- 注册/元数据/披露 ----------------

    [Fact]
    public void NewToolsAreRegisteredInAnalysisCategoryWithGpExecutionType()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new RasterMosaicTool());
        registry.Register(new RasterCalcTool());

        Assert.Equal(2, registry.Count);
        var tools = registry.List();
        Assert.Equal(
            new[] { "raster_calc", "raster_mosaic" },
            tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.All(tools, t => Assert.Equal(ToolCategories.Analysis, t.Metadata.Category));
        Assert.All(tools, t => Assert.Equal(ExecutionTypes.Geoprocessing, t.Metadata.ExecutionType));
        Assert.All(tools, t => Assert.True(t.Metadata.RequiresArcGIS));
    }

    [Fact]
    public void DescriptionsDiscloseEvidenceBasedConstraints()
    {
        var mosaic = new RasterMosaicTool().Description;
        Assert.Contains("文件 GDB", mosaic);
        Assert.Contains("PATH_ESCAPE_REJECTED", mosaic);
        Assert.Contains("000735", mosaic);

        var calc = new RasterCalcTool().Description;
        Assert.Contains("白名单", calc);
        Assert.Contains("任意 Python", calc);
        Assert.Contains("PATH_ESCAPE_REJECTED", calc);
        Assert.Contains("010092", calc);
    }

    // ---------------- helpers ----------------

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
