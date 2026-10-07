using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-096 M2 批四：P6 栅格族六件工具服务契约（1R＋5W，197→203）。
/// 实现保持 ArcGIS SDK / GP 访问在 Compatibility 层（Rule 5：Shared 零 SDK 引用）。
/// 输入面逐字对齐冻结 schema：.runtime/evolution/v5-f/run-20260928-d082/f03b-5-schemas/。
/// </summary>
public interface ID096RasterService
{
    /// <summary>raster_pixel_inspect（R）：按点位采样栅格像元值（可选邻域/波段子集）。</summary>
    Task<OperationResult<IReadOnlyDictionary<string, object?>>> InspectRasterPixelsAsync(
        string raster, IReadOnlyList<object?> points, IReadOnlyList<object?> bands,
        bool includeNeighborhood, CancellationToken ct = default);

    /// <summary>build_raster_pyramids（W；in-place-raster，原位变更输入栅格，无 outputPath）。</summary>
    Task<OperationResult<IReadOnlyDictionary<string, object?>>> BuildRasterPyramidsAsync(
        string input, string resampling, int levels, bool skipFirst, CancellationToken ct = default);

    /// <summary>compose_raster_bands（W）：按目标波段顺序堆叠多栅格为新多波段栅格。</summary>
    Task<OperationResult<IReadOnlyDictionary<string, object?>>> ComposeRasterBandsAsync(
        IReadOnlyList<object?> inputs, string outputPath, IReadOnlyList<object?> bandOrder,
        string? outputPixelType, bool overwrite, CancellationToken ct = default);

    /// <summary>raster_change_detection（W）：两期栅格变化检测（固定方法目录，不接受任意 Con 表达式）。</summary>
    Task<OperationResult<IReadOnlyDictionary<string, object?>>> RasterChangeDetectionAsync(
        string before, string afterRaster, string outputPath, string method,
        double threshold, bool overwrite, CancellationToken ct = default);

    /// <summary>raster_reproject（W）：栅格重投影到目标 CRS。</summary>
    Task<OperationResult<IReadOnlyDictionary<string, object?>>> RasterReprojectAsync(
        string input, string outputPath, string targetCrs, string resampling,
        double? cellSize, string? transformation, bool overwrite, CancellationToken ct = default);

    /// <summary>zonal_histogram（W）：分区×取值栅格的类别频数直方图。</summary>
    Task<OperationResult<IReadOnlyDictionary<string, object?>>> ZonalHistogramAsync(
        string zones, string? zoneField, string values, string outputPath,
        double binWidth, bool overwrite, CancellationToken ct = default);
}
