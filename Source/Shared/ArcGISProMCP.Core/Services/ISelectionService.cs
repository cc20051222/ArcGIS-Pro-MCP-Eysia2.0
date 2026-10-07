using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>选择集服务。</summary>
public interface ISelectionService
{
    Task<OperationResult<SelectionInfo>> GetSelectionAsync(string? mapName = null, CancellationToken ct = default);

    Task<OperationResult<bool>> ClearSelectionAsync(string? mapName = null, CancellationToken ct = default);

    Task<OperationResult<SelectionInfo>> SelectLayerAsync(string mapName, string layerName, CancellationToken ct = default);
}
