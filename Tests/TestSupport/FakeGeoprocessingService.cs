using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Selection;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.TestSupport;

/// <summary>
/// Fake GP 服务（Phase 8.3，D-014 方案 A 单测用）：模拟 Pro 进程内 SelectLayerByAttribute/Location
/// 对地图选择集的真实变更（直接改写 FakeSelectionReadService.Current），供账本编排断言。
/// </summary>
public sealed class FakeGeoprocessingService : IGeoprocessingService
{
    private readonly ISelectionStateStore _store;
    private readonly FakeSelectionReadService _read;

    public FakeGeoprocessingService(ISelectionStateStore store, FakeSelectionReadService read)
    {
        _store = store;
        _read = read;
    }

    /// <summary>模拟的要素全集（switch 取补集用）；默认 {1,2,3}∪当前选择∪请求 OID。</summary>
    public HashSet<long> FeatureUniverse { get; } = new() { 1, 2, 3 };

    /// <summary>调用计数（测试断言“未触达写入”用）。</summary>
    public int CallCount { get; private set; }

    public Task<OperationResult<GeoprocessingResult>> RunToolAsync(GeoprocessingRequest request, CancellationToken ct = default)
        => Task.FromResult(OperationResult<GeoprocessingResult>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    /// <summary>D-021：本替身只覆盖选择语义，存在性探测一律"不存在"（不阻断既有选择用例）。</summary>
    public Task<OperationResult<OutputExistence>> CheckOutputExistsAsync(string outputPath, CancellationToken ct = default)
        => Task.FromResult(OperationResult<OutputExistence>.Ok(OutputExistence.NotExists, "fake-selection-service"));

    public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByAttributeAsync(
        string? mapName, string layerName, string mode,
        IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default)
    {
        CallCount++;
        var current = _read.Current;
        if (current is null)
        {
            return Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NoActiveView, "No active map view is available."));
        }

        var layer = current.Layers.FirstOrDefault(l => string.Equals(l.LayerName, layerName, StringComparison.OrdinalIgnoreCase));
        var oldSet = new HashSet<long>(layer?.Oids ?? new List<long>());

        HashSet<long> newSet;
        if (oidList is not null)
        {
            newSet = mode switch
            {
                "replace" => new HashSet<long>(oidList),
                "add" => new HashSet<long>(oldSet.Union(oidList)),
                "remove" => new HashSet<long>(oldSet.Except(oidList)),
                "switch" => Complement(oldSet),
                _ => new HashSet<long>(oldSet),
            };
        }
        else if (mode == "switch")
        {
            newSet = Complement(oldSet);
        }
        else
        {
            // where 模拟：视为匹配空集（replace → 空；add/remove 不变）。
            newSet = mode == "replace" ? new HashSet<long>() : new HashSet<long>(oldSet);
        }

        ApplySelection(current, layerName, newSet);
        return Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Ok(Payload(current, layerName, mode, nonIdempotent: mode == "switch")));
    }

    public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByLocationAsync(
        string? mapName, string layerName, string? selectingLayerName,
        string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default)
    {
        CallCount++;
        var current = _read.Current;
        if (current is null)
        {
            return Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NoActiveView, "No active map view is available."));
        }

        // 空间选择模拟：replace → 全集（自交场景，语义上“与已选相交”）。
        var layer = current.Layers.FirstOrDefault(l => string.Equals(l.LayerName, layerName, StringComparison.OrdinalIgnoreCase));
        var oldSet = new HashSet<long>(layer?.Oids ?? new List<long>());
        var newSet = mode == "replace" ? new HashSet<long>(FeatureUniverse)
            : mode == "add" ? new HashSet<long>(oldSet.Union(FeatureUniverse))
            : new HashSet<long>(oldSet.Except(FeatureUniverse));

        ApplySelection(current, layerName, newSet);
        return Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Ok(Payload(current, layerName, mode, nonIdempotent: false)));
    }

    private HashSet<long> Complement(HashSet<long> oldSet)
    {
        foreach (var oid in oldSet)
        {
            FeatureUniverse.Add(oid);
        }

        return new HashSet<long>(FeatureUniverse.Except(oldSet));
    }

    private static void ApplySelection(SelectionContent current, string layerName, HashSet<long> newSet)
    {
        foreach (var oid in newSet)
        {
            current.GetType(); // no-op，保持形状
        }

        var layer = current.Layers.FirstOrDefault(l => string.Equals(l.LayerName, layerName, StringComparison.OrdinalIgnoreCase));
        if (layer is null)
        {
            layer = new SelectionLayerContent
            {
                LayerName = layerName,
                LayerUri = "fake://" + layerName,
            };
            current.Layers.Add(layer);
        }

        layer.Oids = newSet.OrderBy(o => o).ToList();
        layer.SelectedCount = newSet.Count;
        layer.Truncated = false;
    }

    private static System.Text.Json.JsonElement Payload(SelectionContent current, string layerName, string mode, bool nonIdempotent)
    {
        var layer = current.Layers.FirstOrDefault(l => string.Equals(l.LayerName, layerName, StringComparison.OrdinalIgnoreCase));
        return System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            map_name = current.MapName,
            layer_name = layerName,
            layer_uri = layer?.LayerUri,
            mode,
            nonIdempotent,
            selected_count = layer?.SelectedCount ?? 0,
        });
    }
}
