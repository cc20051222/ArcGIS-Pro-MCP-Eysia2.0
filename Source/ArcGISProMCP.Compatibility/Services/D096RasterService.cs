using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-096 · M2 P6 栅格族六件工具服务（ArcGIS Pro SDK / GP 实现）。
/// Rule 4：所有 SDK 对象访问经 QueuedTask.Run。
/// Rule 5：SDK 引用只在 Compatibility 层。
/// 真机验证状态：NOT VERIFIED（禁安装；SDK/GP 依赖件如实登记）。
/// [VERIFY] GP 准入：management.Project 在白名单；management.ProjectRaster / BuildRasterPyramids /
///          MosaicToNewRaster **不在白名单 53**——待安装批真机核实；GP 白名单本批零改（53 恒定）。
/// [VERIFY] build_raster_pyramids 系 in-place-raster（原位变更输入栅格，无 outputPath）；
///          raster_pixel_inspect 系 R（pythonBridge=true 数据通道，但 ps_* 零执行红线恒定）。
/// </summary>
public sealed class D096RasterService : ID096RasterService
{
    private static readonly string NotVerified = "requires a live ArcGIS Pro session (SDK/GP dependency; NOT VERIFIED in build-only mode).";

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> InspectRasterPixelsAsync(
        string raster, IReadOnlyList<object?> points, IReadOnlyList<object?> bands,
        bool includeNeighborhood, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"raster_pixel_inspect {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> BuildRasterPyramidsAsync(
        string input, string resampling, int levels, bool skipFirst, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"build_raster_pyramids {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ComposeRasterBandsAsync(
        IReadOnlyList<object?> inputs, string outputPath, IReadOnlyList<object?> bandOrder,
        string? outputPixelType, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"compose_raster_bands {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> RasterChangeDetectionAsync(
        string before, string afterRaster, string outputPath, string method,
        double threshold, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"raster_change_detection {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> RasterReprojectAsync(
        string input, string outputPath, string targetCrs, string resampling,
        double? cellSize, string? transformation, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"raster_reproject {NotVerified}"));

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ZonalHistogramAsync(
        string zones, string? zoneField, string values, string outputPath,
        double binWidth, bool overwrite, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.NotImplemented, $"zonal_histogram {NotVerified}"));
}
