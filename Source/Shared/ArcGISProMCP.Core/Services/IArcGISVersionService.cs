using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>ArcGIS Pro 版本服务。</summary>
public interface IArcGISVersionService
{
    Task<OperationResult<ArcGISVersionInfo>> GetVersionAsync(CancellationToken ct = default);
}
