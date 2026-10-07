using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>栅格服务。</summary>
public interface IRasterService
{
    Task<OperationResult<RasterInfo>> GetRasterInfoAsync(string datasetPath, CancellationToken ct = default);
}
