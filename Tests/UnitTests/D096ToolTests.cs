using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>D-096: M2 P6 raster-family six tools (1R + 5W) contract paths and fail-closed cases.</summary>
public sealed class D096ToolTests
{
    private static ToolExecutionContext Ctx(
        Dictionary<string, object?>? args = null, IArcGISHost? host = null,
        bool readOnly = false, IPythonBridgeService? python = null)
    {
        var mode = new ReadOnlyModeService();
        if (readOnly) mode.Set(true);
        return new ToolExecutionContext
        {
            Host = host ?? new FakeArcGISHost(),
            Arguments = args ?? new Dictionary<string, object?>(),
            ReadOnly = mode,
            Python = python,
        };
    }

    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] values)
        => values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

    private static string? Code(OperationResult<object?> result)
        => result.Success ? null : result.Errors.FirstOrDefault()?.Code;

    private static IArcGISHost HostWithRaster(RasterFake? fake = null)
        => new FakeArcGISHost(d096Raster: fake ?? new RasterFake());

    // ═══════════════════ raster_pixel_inspect (R) ═══════════════════

    [Fact]
    public async Task RasterPixelInspect_RejectsWithoutPythonBridge()
    {
        var host = HostWithRaster();
        var result = await new RasterPixelInspectTool().ExecuteAsync(
            Ctx(Args(("raster", "a.tif"), ("points", new object?[] { Args(("x", 1.0), ("y", 2.0)) })), host, python: null));
        Assert.Equal(ErrorCodes.PythonBridgeUnavailable, Code(result));
    }

    [Fact]
    public async Task RasterPixelInspect_DelegatesWithBridgeAndRejectsMissingRaster()
    {
        var fake = new RasterFake();
        var host = HostWithRaster(fake);
        var bridge = new FakePythonBridgeService();
        var good = await new RasterPixelInspectTool().ExecuteAsync(
            Ctx(Args(("raster", "a.tif"), ("points", new object?[] { Args(("x", 1.0), ("y", 2.0)) })), host, python: bridge));
        Assert.True(good.Success);
        Assert.Equal(1, fake.InspectPixelsCalls);

        var bad = await new RasterPixelInspectTool().ExecuteAsync(
            Ctx(Args(("points", new object?[] { Args(("x", 1.0), ("y", 2.0)) })), host, python: bridge));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task RasterPixelInspect_RejectsEmptyPoints()
    {
        var host = HostWithRaster();
        var bridge = new FakePythonBridgeService();
        var bad = await new RasterPixelInspectTool().ExecuteAsync(
            Ctx(Args(("raster", "a.tif"), ("points", Array.Empty<object?>())), host, python: bridge));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task RasterPixelInspect_NoServiceReturnsNotImplemented()
    {
        var host = new FakeArcGISHost(); // no D096Raster
        var bridge = new FakePythonBridgeService();
        var result = await new RasterPixelInspectTool().ExecuteAsync(
            Ctx(Args(("raster", "a.tif"), ("points", new object?[] { Args(("x", 1.0), ("y", 2.0)) })), host, python: bridge));
        Assert.Equal(ErrorCodes.NotImplemented, Code(result));
    }

    // ═══════════════════ build_raster_pyramids (W, in-place) ═══════════════════

    [Fact]
    public async Task BuildRasterPyramids_DelegatesValidInputAndRejectsMissingInput()
    {
        var fake = new RasterFake();
        var host = HostWithRaster(fake);
        var good = await new BuildRasterPyramidsTool().ExecuteAsync(
            Ctx(Args(("input", "D:/raster.tif")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.BuildPyramidsCalls);

        var bad = await new BuildRasterPyramidsTool().ExecuteAsync(Ctx(Args(), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task BuildRasterPyramids_RejectsInvalidResampling()
    {
        var host = HostWithRaster();
        var bad = await new BuildRasterPyramidsTool().ExecuteAsync(
            Ctx(Args(("input", "D:/raster.tif"), ("resampling", "INVALID")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task BuildRasterPyramids_RejectsNegativeLevels()
    {
        var host = HostWithRaster();
        var bad = await new BuildRasterPyramidsTool().ExecuteAsync(
            Ctx(Args(("input", "D:/raster.tif"), ("levels", -1)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task BuildRasterPyramids_ReadOnlyRefuses()
    {
        var host = HostWithRaster();
        var result = await new BuildRasterPyramidsTool().ExecuteAsync(
            Ctx(Args(("input", "D:/raster.tif")), host, readOnly: true));
        Assert.Equal(ErrorCodes.PermissionDenied, Code(result));
    }

    // ═══════════════════ compose_raster_bands (W) ═══════════════════

    [Fact]
    public async Task ComposeRasterBands_DelegatesValidInputAndRejectsMissingOutputPath()
    {
        var fake = new RasterFake();
        var host = HostWithRaster(fake);
        var good = await new ComposeRasterBandsTool().ExecuteAsync(
            Ctx(Args(("inputs", new object?[] { "b1.tif", "b2.tif" }), ("outputPath", "D:/out.gdb/composed")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.ComposeBandsCalls);

        var bad = await new ComposeRasterBandsTool().ExecuteAsync(
            Ctx(Args(("inputs", new object?[] { "b1.tif" })), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task ComposeRasterBands_RejectsEmptyInputs()
    {
        var host = HostWithRaster();
        var bad = await new ComposeRasterBandsTool().ExecuteAsync(
            Ctx(Args(("inputs", Array.Empty<object?>()), ("outputPath", "D:/o")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task ComposeRasterBands_ReadOnlyRefuses()
    {
        var host = HostWithRaster();
        var result = await new ComposeRasterBandsTool().ExecuteAsync(
            Ctx(Args(("inputs", new object?[] { "b1.tif" }), ("outputPath", "D:/o")), host, readOnly: true));
        Assert.Equal(ErrorCodes.PermissionDenied, Code(result));
    }

    // ═══════════════════ raster_change_detection (W) ═══════════════════

    [Fact]
    public async Task RasterChangeDetection_DelegatesValidInputAndRejectsMissingMethod()
    {
        var fake = new RasterFake();
        var host = HostWithRaster(fake);
        var good = await new RasterChangeDetectionTool().ExecuteAsync(
            Ctx(Args(("before", "b.tif"), ("afterRaster", "a.tif"), ("outputPath", "D:/out.gdb/change"), ("method", "difference")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.ChangeDetectionCalls);

        var bad = await new RasterChangeDetectionTool().ExecuteAsync(
            Ctx(Args(("before", "b.tif"), ("afterRaster", "a.tif"), ("outputPath", "D:/o")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task RasterChangeDetection_RejectsInvalidMethod()
    {
        var host = HostWithRaster();
        var bad = await new RasterChangeDetectionTool().ExecuteAsync(
            Ctx(Args(("before", "b.tif"), ("afterRaster", "a.tif"), ("outputPath", "D:/o"), ("method", "arbitrary_con")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task RasterChangeDetection_ReadOnlyRefuses()
    {
        var host = HostWithRaster();
        var result = await new RasterChangeDetectionTool().ExecuteAsync(
            Ctx(Args(("before", "b.tif"), ("afterRaster", "a.tif"), ("outputPath", "D:/o"), ("method", "ratio")), host, readOnly: true));
        Assert.Equal(ErrorCodes.PermissionDenied, Code(result));
    }

    // ═══════════════════ raster_reproject (W) ═══════════════════

    [Fact]
    public async Task RasterReproject_DelegatesValidInputAndRejectsMissingTargetCrs()
    {
        var fake = new RasterFake();
        var host = HostWithRaster(fake);
        var good = await new RasterReprojectTool().ExecuteAsync(
            Ctx(Args(("input", "r.tif"), ("outputPath", "D:/out.gdb/reprojected"), ("targetCrs", "EPSG:3857")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.ReprojectCalls);

        var bad = await new RasterReprojectTool().ExecuteAsync(
            Ctx(Args(("input", "r.tif"), ("outputPath", "D:/o")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task RasterReproject_RejectsInvalidResampling()
    {
        var host = HostWithRaster();
        var bad = await new RasterReprojectTool().ExecuteAsync(
            Ctx(Args(("input", "r.tif"), ("outputPath", "D:/o"), ("targetCrs", "EPSG:3857"), ("resampling", "INVALID")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task RasterReproject_ReadOnlyRefuses()
    {
        var host = HostWithRaster();
        var result = await new RasterReprojectTool().ExecuteAsync(
            Ctx(Args(("input", "r.tif"), ("outputPath", "D:/o"), ("targetCrs", "EPSG:3857")), host, readOnly: true));
        Assert.Equal(ErrorCodes.PermissionDenied, Code(result));
    }

    // ═══════════════════ zonal_histogram (W) ═══════════════════

    [Fact]
    public async Task ZonalHistogram_DelegatesValidInputAndRejectsMissingValues()
    {
        var fake = new RasterFake();
        var host = HostWithRaster(fake);
        var good = await new ZonalHistogramTool().ExecuteAsync(
            Ctx(Args(("zones", "zones.shp"), ("values", "v.tif"), ("outputPath", "D:/out.gdb/zhist"), ("zoneField", "ZONE_ID")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.ZonalHistogramCalls);

        var bad = await new ZonalHistogramTool().ExecuteAsync(
            Ctx(Args(("zones", "zones.shp"), ("outputPath", "D:/o")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task ZonalHistogram_RejectsNegativeBinWidth()
    {
        var host = HostWithRaster();
        var bad = await new ZonalHistogramTool().ExecuteAsync(
            Ctx(Args(("zones", "z.shp"), ("values", "v.tif"), ("outputPath", "D:/o"), ("binWidth", -1.0)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task ZonalHistogram_ReadOnlyRefuses()
    {
        var host = HostWithRaster();
        var result = await new ZonalHistogramTool().ExecuteAsync(
            Ctx(Args(("zones", "z.shp"), ("values", "v.tif"), ("outputPath", "D:/o")), host, readOnly: true));
        Assert.Equal(ErrorCodes.PermissionDenied, Code(result));
    }

    // ═══════════════════ Cross-cutting ═══════════════════

    [Fact]
    public async Task AllWriteTools_NoServiceReturnsNotImplemented()
    {
        var host = new FakeArcGISHost(); // no D096Raster
        var tools = new (McpToolBase tool, Dictionary<string, object?> args)[]
        {
            (new BuildRasterPyramidsTool(), Args(("input", "D:/raster.tif"))),
            (new ComposeRasterBandsTool(), Args(("inputs", new object?[] { "b1.tif" }), ("outputPath", "D:/o"))),
            (new RasterChangeDetectionTool(), Args(("before", "b.tif"), ("afterRaster", "a.tif"), ("outputPath", "D:/o"), ("method", "difference"))),
            (new RasterReprojectTool(), Args(("input", "r.tif"), ("outputPath", "D:/o"), ("targetCrs", "EPSG:3857"))),
            (new ZonalHistogramTool(), Args(("zones", "z.shp"), ("values", "v.tif"), ("outputPath", "D:/o"))),
        };
        foreach (var (tool, args) in tools)
        {
            var result = await tool.ExecuteAsync(Ctx(args, host));
            Assert.Equal(ErrorCodes.NotImplemented, Code(result));
        }
    }

    [Fact]
    public void AllSixTools_HaveCorrectMetadata()
    {
        var tools = new McpToolBase[]
        {
            new RasterPixelInspectTool(), new BuildRasterPyramidsTool(), new ComposeRasterBandsTool(),
            new RasterChangeDetectionTool(), new RasterReprojectTool(), new ZonalHistogramTool(),
        };
        Assert.Equal(6, tools.Length);
        foreach (var t in tools)
        {
            Assert.True(t.Metadata.RequiresArcGIS);
            Assert.NotNull(t.InputSchema);
            Assert.Equal("object", t.InputSchema["type"]);
            Assert.Equal(false, t.InputSchema["additionalProperties"]);
        }
        // raster_pixel_inspect = Quality/Native (R); other five = Analysis/Geoprocessing (W)
        Assert.Equal(ToolCategories.Quality, new RasterPixelInspectTool().Metadata.Category);
        Assert.Equal(ExecutionTypes.Native, new RasterPixelInspectTool().Metadata.ExecutionType);
        Assert.Equal(5, tools.Count(t => t.Metadata.Category == ToolCategories.Analysis));
        Assert.Equal(5, tools.Count(t => t.Metadata.ExecutionType == ExecutionTypes.Geoprocessing));
        Assert.Equal(1, tools.Count(t => t.Metadata.ExecutionType == ExecutionTypes.Native));
    }

    [Fact]
    public void AllSixTools_InContractSnapshotWith224Total()
    {
        var snapshot = ProductionToolContractSnapshot.Tools;
        Assert.Equal(239, snapshot.Count);
        var names = new[]
        {
            "raster_pixel_inspect", "build_raster_pyramids", "compose_raster_bands",
            "raster_change_detection", "raster_reproject", "zonal_histogram",
        };
        foreach (var name in names)
            Assert.Contains(snapshot, t => t.Name == name);
        Assert.Equal(snapshot.Count, snapshot.Select(t => t.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AllSixTools_ClassifiedCorrectlyInRoster()
    {
        Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf("raster_pixel_inspect"));
        var writeTools = new[]
        {
            "build_raster_pyramids", "compose_raster_bands", "raster_change_detection",
            "raster_reproject", "zonal_histogram",
        };
        foreach (var name in writeTools)
            Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf(name));
    }

    [Fact]
    public void WriteTools_ReadOnlyRefusalMessageUsesOperationErrorMessage()
    {
        // 红线：ReadOnlyRefusal 返回 OperationError，工具侧取其 .Message（含 "(read-only)" 标记）。
        var err = ToolWriteClassification.ReadOnlyRefusal("build_raster_pyramids");
        Assert.Equal(ErrorCodes.PermissionDenied, err.Code);
        Assert.Contains("(read-only)", err.Message);
    }

    // ═══════════════════ Fake ═══════════════════

    private sealed class RasterFake : ID096RasterService
    {
        public int InspectPixelsCalls { get; private set; }
        public int BuildPyramidsCalls { get; private set; }
        public int ComposeBandsCalls { get; private set; }
        public int ChangeDetectionCalls { get; private set; }
        public int ReprojectCalls { get; private set; }
        public int ZonalHistogramCalls { get; private set; }

        private static OperationResult<IReadOnlyDictionary<string, object?>> Ok()
            => OperationResult<IReadOnlyDictionary<string, object?>>.Ok(new Dictionary<string, object?> { ["status"] = "ok" });

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> InspectRasterPixelsAsync(string r, IReadOnlyList<object?> p, IReadOnlyList<object?> b, bool inc, CancellationToken ct = default)
        { InspectPixelsCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> BuildRasterPyramidsAsync(string i, string res, int lv, bool sf, CancellationToken ct = default)
        { BuildPyramidsCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ComposeRasterBandsAsync(IReadOnlyList<object?> ins, string o, IReadOnlyList<object?> bo, string? pt, bool ow, CancellationToken ct = default)
        { ComposeBandsCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> RasterChangeDetectionAsync(string b, string a, string o, string m, double th, bool ow, CancellationToken ct = default)
        { ChangeDetectionCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> RasterReprojectAsync(string i, string o, string crs, string res, double? cs, string? tr, bool ow, CancellationToken ct = default)
        { ReprojectCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ZonalHistogramAsync(string z, string? zf, string v, string o, double bw, bool ow, CancellationToken ct = default)
        { ZonalHistogramCalls++; return Task.FromResult(Ok()); }
    }
}
