using System.IO;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// 栅格服务实现。提供栅格路径存在性/基础信息（真实文件系统检查）。
/// 说明：width/height/bands 的读取依赖后续（GP / RasterDataset），此处不伪造值。
/// </summary>
/// <remarks>Superseded by Bridge per PHASE_8_2_CHANNEL ADR（G-27 裁定：保留不删，8.6 统一清理）。工具路由已改指 Bridge。</remarks>
public sealed class RasterService : IRasterService
{
    public Task<OperationResult<RasterInfo>> GetRasterInfoAsync(string datasetPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(datasetPath))
        {
            return Task.FromResult(OperationResult<RasterInfo>.Fail(ErrorCodes.InvalidArgument, "datasetPath is required."));
        }

        if (!File.Exists(datasetPath) && !Directory.Exists(datasetPath))
        {
            return Task.FromResult(OperationResult<RasterInfo>.Fail(ErrorCodes.DatasetNotFound, $"Raster dataset not found: {datasetPath}"));
        }

        return Task.FromResult(OperationResult<RasterInfo>.Ok(new RasterInfo
        {
            Path = datasetPath,
            Name = Path.GetFileName(datasetPath.TrimEnd('\\', '/')) ?? datasetPath,
            PixelType = string.Empty,
            Width = 0,
            Height = 0,
            Bands = 0
        }));
    }
}
