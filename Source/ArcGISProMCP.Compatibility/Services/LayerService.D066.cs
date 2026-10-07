using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-066：连接状态回读（add_join/remove_join 前后照证据通道）。
/// 证据口径＝字段命名探针（GetFieldDescriptions；点名字段 ⇒ 连接现身形态，D-066 spike 实测）。
/// </summary>
public sealed partial class LayerService
{
    /// <inheritdoc />
    public Task<OperationResult<JoinStateInfo>> GetJoinStateAsync(
        string? mapName, string layerName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<JoinStateInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<JoinStateInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var lr = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (lr.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<JoinStateInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, lr));
                }

                if (lr.Status != LayerResolveStatus.Ok || lr.Layer is null)
                {
                    return OperationResult<JoinStateInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                if (lr.Layer is not BasicFeatureLayer bfl)
                {
                    return OperationResult<JoinStateInfo>.Fail(
                        ErrorCodes.InvalidArgument, $"layer '{layerName}' is not a feature layer (join state unsupported).");
                }

                var names = bfl.GetFieldDescriptions().Select(f => f.Name).ToList();
                var dotted = names.Where(n => n.Contains('.')).ToList();

                return OperationResult<JoinStateInfo>.Ok(new JoinStateInfo
                {
                    LayerName = bfl.Name ?? layerName,
                    MapName = resolved.Map!.Name ?? string.Empty,
                    FieldCount = names.Count,
                    Fields = names.Take(400).ToList(),
                    DottedFields = dotted.Take(200).ToList(),
                    JoinedDetected = dotted.Count > 0,
                });
            });
}
