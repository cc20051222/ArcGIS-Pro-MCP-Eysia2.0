using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.TestSupport;

/// <summary>
/// Phase 8.1 隔离测试用的可配置地图服务替身（不依赖 ArcGIS Pro）。
/// 与 <c>FakeMapService</c> 的区别：本替身可显式表达"无活动视图"与"重名地图"，
/// 用于验证 C-05 / C-06 修复后的错误契约。
/// </summary>
public sealed class ScriptedMapService : IMapService
{
    /// <summary>是否有活动地图视图。<c>false</c> 时 <see cref="GetCurrentMapAsync"/> 返回 NO_ACTIVE_VIEW。</summary>
    public bool HasActiveView { get; set; } = true;

    /// <summary>活动地图（<see cref="HasActiveView"/> 为 true 时返回）。</summary>
    public MapInfo? ActiveMap { get; set; }

    /// <summary>工程内全部地图（可含重名）。</summary>
    public List<MapInfo> Maps { get; } = new();

    public Task<OperationResult<MapInfo?>> GetCurrentMapAsync(CancellationToken ct = default)
        => Task.FromResult(HasActiveView
            ? OperationResult<MapInfo?>.Ok(ActiveMap)
            : OperationResult<MapInfo?>.Fail(
                ErrorCodes.NoActiveView,
                "No active map view is available."));

    public Task<OperationResult<IReadOnlyList<MapInfo>>> GetMapsAsync(CancellationToken ct = default)
        => Task.FromResult(
            OperationResult<IReadOnlyList<MapInfo>>.Ok((IReadOnlyList<MapInfo>)Maps.ToList()));

    public Task<OperationResult<MapExtentInfo>> GetMapExtentAsync(string? mapName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapExtentInfo>.Ok(new MapExtentInfo { MapName = mapName ?? string.Empty }));

    public Task<OperationResult<MapExtentSetInfo>> SetMapExtentAsync(
        string? mapName, double xMin, double yMin, double xMax, double yMax, string? spatialReference,
        CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapExtentSetInfo>.Ok(new MapExtentSetInfo
        {
            MapName = mapName ?? string.Empty,
            XMin = xMin,
            YMin = yMin,
            XMax = xMax,
            YMax = yMax,
            SpatialReferenceName = string.IsNullOrWhiteSpace(spatialReference) ? null : spatialReference,
            ReadBackXMin = xMin,
            ReadBackYMin = yMin,
            ReadBackXMax = xMax,
            ReadBackYMax = yMax,
            SameSource = true,
        }));
}

/// <summary>
/// Phase 8.1 隔离测试用的可配置图层服务替身。
/// 记录每次调用传入的 <c>flatten</c>，用于验证 G-06 的默认展开行为；
/// 并支持重名图层以验证 AMBIGUOUS_LAYER_NAME。
/// </summary>
public sealed class ScriptedLayerService : ILayerService
{
    /// <summary><c>flatten=false</c> 时返回（仅顶层）。</summary>
    public List<LayerInfo> TopLevel { get; } = new();

    /// <summary><c>flatten=true</c> 时返回（含组合图层子图层）。</summary>
    public List<LayerInfo> Flattened { get; } = new();

    /// <summary>按调用顺序记录 <c>flatten</c> 实参。</summary>
    public List<bool> FlattenCalls { get; } = new();

    public Task<OperationResult<IReadOnlyList<LayerInfo>>> GetLayersAsync(
        string? mapName = null,
        bool flatten = true,
        CancellationToken ct = default)
    {
        FlattenCalls.Add(flatten);

        if (string.Equals(mapName, "MissingMap", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(
                OperationResult<IReadOnlyList<LayerInfo>>.Fail(ErrorCodes.MapNotFound, "Map not found."));
        }

        var source = flatten ? Flattened : TopLevel;
        return Task.FromResult(
            OperationResult<IReadOnlyList<LayerInfo>>.Ok((IReadOnlyList<LayerInfo>)source.ToList()));
    }

    public Task<OperationResult<LayerInfo?>> FindLayerAsync(string mapName, string layerName, CancellationToken ct = default)
        => GetLayerInfoAsync(mapName, layerName, ct);

    public Task<OperationResult<LayerInfo?>> GetLayerInfoAsync(string mapName, string layerName, CancellationToken ct = default)
    {
        var matches = Flattened
            .Where(l => string.Equals(l.Name, layerName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            return Task.FromResult(
                OperationResult<LayerInfo?>.Fail(ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found."));
        }

        if (matches.Count > 1)
        {
            return Task.FromResult(
                OperationResult<LayerInfo?>.Fail(
                    ErrorCodes.AmbiguousLayerName,
                    $"Layer name '{layerName}' is ambiguous ({matches.Count} matches)."));
        }

        return Task.FromResult(OperationResult<LayerInfo?>.Ok(matches[0]));
    }

    public Task<OperationResult<bool>> SetLayerVisibilityAsync(string mapName, string layerName, bool visible, CancellationToken ct = default)
        => Task.FromResult(OperationResult<bool>.Ok(true));

    public Task<OperationResult<LayerInfo?>> AddLayerAsync(string mapName, string layerPathOrUri, CancellationToken ct = default)
        => Task.FromResult(OperationResult<LayerInfo?>.Ok(new LayerInfo { Name = "Added", MapName = mapName }));

    public Task<OperationResult<bool>> RemoveLayerAsync(string mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<bool>.Ok(true));
    public Task<OperationResult<LayerSymbologyInfo>> GetLayerSymbologyAsync(string? mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<LayerSymbologyInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    public Task<OperationResult<SymbologySetInfo>> SetSimpleSymbologyAsync(string? mapName, string layerName, string? fillColor, string? outlineColor, double? pointSize, double? lineWidth, CancellationToken ct = default)
        => Task.FromResult(OperationResult<SymbologySetInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    public Task<OperationResult<LabelInfo>> GetLabelInfoAsync(string? mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<LabelInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    public Task<OperationResult<LabelVisibilityInfo>> SetLabelVisibilityAsync(string? mapName, string layerName, bool enabled, CancellationToken ct = default)
        => Task.FromResult(OperationResult<LabelVisibilityInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    public Task<OperationResult<DefinitionQueryInfo>> GetDefinitionQueryAsync(string? mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<DefinitionQueryInfo>.Ok(new DefinitionQueryInfo
        {
            LayerName = layerName ?? string.Empty,
            SupportsDefinitionQuery = true,
            DefinitionQuery = string.Empty,
        }));


    /// <summary>记录最近一次定义查询写入（供测试断言「空 = 清除」等语义）。</summary>
    public string? LastDefinitionQueryWrite { get; private set; }

    public Task<OperationResult<DefinitionQuerySetInfo>> SetDefinitionQueryAsync(
        string? mapName, string layerName, string? definitionQuery, CancellationToken ct = default)
    {
        LastDefinitionQueryWrite = definitionQuery;
        return Task.FromResult(OperationResult<DefinitionQuerySetInfo>.Ok(new DefinitionQuerySetInfo
        {
            LayerName = layerName ?? string.Empty,
            Applied = true,
            SupportsDefinitionQuery = true,
            DefinitionQuery = definitionQuery ?? string.Empty,
        }));
    }

    /// <summary>记录最近一次移层调用（供测试断言 position/referenceLayer 透传）。</summary>
    public (string Layer, string? Reference, string Position)? LastMoveLayer { get; private set; }

    public Task<OperationResult<LayerOrderInfo>> MoveLayerAsync(
        string? mapName, string layerName, string? referenceLayer, string position, CancellationToken ct = default)
    {
        LastMoveLayer = (layerName ?? string.Empty, referenceLayer, position ?? string.Empty);
        return Task.FromResult(OperationResult<LayerOrderInfo>.Ok(new LayerOrderInfo
        {
            MapName = mapName ?? string.Empty,
            LayerName = layerName ?? string.Empty,
            Position = (position ?? string.Empty).ToUpperInvariant(),
            TargetIndex = 0,
            RootLayerOrder = new[] { layerName ?? string.Empty },
        }));
    }

}
