using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Selection;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.TestSupport;

/// <summary>
/// Fake 选择读取/快照服务：内存中模拟地图选择内容（Current），供无 Pro 环境下
/// 测试账本编排（写前核对/自动快照/恢复）。Current 为 null = 无活动视图。
/// </summary>
public sealed class FakeSelectionReadService : ISelectionReadService
{
    private readonly ISelectionStateStore _store;

    public FakeSelectionReadService(ISelectionStateStore store)
    {
        _store = store;
    }

    /// <summary>当前模拟选择内容；null = 无活动视图（GetSelectionContentAsync → NO_ACTIVE_VIEW）。</summary>
    public SelectionContent? Current { get; set; }

    /// <summary>无活动视图时返回的错误码（可模拟 MAP_NOT_FOUND）。</summary>
    public string UnavailableErrorCode { get; set; } = ErrorCodes.NoActiveView;

    /// <summary>恢复调用计数（测试断言）。</summary>
    public int RestoreCalls { get; private set; }

    public Task<OperationResult<SelectionContent>> GetSelectionContentAsync(string? mapName, CancellationToken ct = default)
    {
        if (Current is null)
        {
            return Task.FromResult(OperationResult<SelectionContent>.Fail(UnavailableErrorCode, "No active map view is available."));
        }

        var copy = Clone(Current);
        if (!string.IsNullOrWhiteSpace(mapName)
            && !string.Equals(copy.MapName, mapName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(OperationResult<SelectionContent>.Fail(ErrorCodes.MapNotFound, "Map not found: " + mapName));
        }

        return Task.FromResult(OperationResult<SelectionContent>.Ok(copy));
    }

    public Task<OperationResult<SelectionContent>> GetSelectionContentByUriAsync(string mapUri, CancellationToken ct = default)
    {
        if (Current is null)
        {
            return Task.FromResult(OperationResult<SelectionContent>.Fail(UnavailableErrorCode, "No active map view is available."));
        }

        if (!string.Equals(Current.MapUri, mapUri, StringComparison.Ordinal))
        {
            return Task.FromResult(OperationResult<SelectionContent>.Fail(
                ErrorCodes.SelectionSnapshotExpired, "map not found in current project (mapUri mismatch)."));
        }

        return Task.FromResult(OperationResult<SelectionContent>.Ok(Clone(Current)));
    }

    public Task<OperationResult<SelectionSnapshotRecord>> CreateSnapshotAsync(string? mapName, CancellationToken ct = default)
    {
        if (Current is null)
        {
            return Task.FromResult(OperationResult<SelectionSnapshotRecord>.Fail(UnavailableErrorCode, "No active map view is available."));
        }

        var content = Clone(Current);
        _store.TryGetLedgerRevision(content.MapUri, out var revision);
        var record = _store.AddSnapshot(content.MapUri, content.MapName, content, revision);
        return Task.FromResult(OperationResult<SelectionSnapshotRecord>.Ok(record));
    }

    public Task<OperationResult<SnapshotRestoreResult>> RestoreSnapshotAsync(string mapName, SelectionContent content, CancellationToken ct = default)
    {
        if (Current is null)
        {
            return Task.FromResult(OperationResult<SnapshotRestoreResult>.Fail(UnavailableErrorCode, "No active map view is available."));
        }

        if (!string.Equals(Current.MapUri, content.MapUri, StringComparison.Ordinal))
        {
            return Task.FromResult(OperationResult<SnapshotRestoreResult>.Fail(
                ErrorCodes.SelectionSnapshotExpired,
                "snapshot mapUri does not match current map."));
        }

        RestoreCalls++;
        Current = Clone(content);
        var result = new SnapshotRestoreResult
        {
            SnapshotId = string.Empty,
            RestoredLayers = content.Layers.ToList(),
            RestoredCount = content.TotalSelected,
        };
        return Task.FromResult(OperationResult<SnapshotRestoreResult>.Ok(result));
    }

    private static SelectionContent Clone(SelectionContent c) => new()
    {
        MapName = c.MapName,
        MapUri = c.MapUri,
        Layers = c.Layers
            .Select(l => new SelectionLayerContent
            {
                LayerName = l.LayerName,
                LayerUri = l.LayerUri,
                Oids = l.Oids.ToList(),
                SelectedCount = l.SelectedCount,
                Truncated = l.Truncated,
            })
            .ToList(),
    };
}
