using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-062 · B 段编辑栈实现（EditOperation 撤销栈）。
/// <b>事务铁律</b>：B1–B3 只把编辑送进撤销栈（EditOperation.Execute），**不调用 SaveEdits**；
/// 提交/回滚由 B4/B5 显式执行；B6 只读查询。
/// 会话跟踪：PendingChangeCount 为本服务进程内累计（save/discard 归零）；
/// <c>Project.Current.HasEdits</c> 为权威真值（两者不一致以 SDK 为准，如实披露）。
/// 目标守卫：底层数据集路径命中受保护根（TestFixtures / 旧仓库 / ARCGIS_PRO_MCP_PROTECTED_ROOTS）→
/// PATH_ESCAPE_REJECTED（零变更）。
/// </summary>
public sealed class EditService : IEditService
{
    /// <summary>单次 update/delete 的 OID 上限（超出 → INVALID_ARGUMENT，披露上限）。</summary>
    public const long MaxRowsPerOperation = 10_000;

    /// <summary>会话跟踪状态（本服务实例内；跨 MCP 调用保持）。</summary>
    private long _pendingCount;
    private readonly List<string> _affectedLayers = new();
    private readonly object _gate = new();

    public Task<OperationResult<EditOpResult>> InsertFeaturesAsync(
        string? mapName, string layerName,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<EditOpResult>>(
            () =>
            {
                if (rows is null || rows.Count == 0)
                {
                    return OperationResult<EditOpResult>.Fail(
                        ErrorCodes.InvalidArgument, "rows is required (at least one row dictionary).");
                }

                var (layer, fail) = ResolveFeatureLayer(mapName, layerName);
                if (fail is not null)
                {
                    return OperationResult<EditOpResult>.Fail(fail.Value.Code, fail.Value.Message);
                }

                var guard = GuardTarget(layer!);
                if (guard is not null)
                {
                    return OperationResult<EditOpResult>.Fail(ErrorCodes.PathEscapeRejected, guard);
                }

                var op = new EditOperation
                {
                    Name = "MCP insert_features",
                };

                var inserted = 0L;
                try
                {
                    foreach (var row in rows)
                    {
                        var attrs = ToAttributeDictionary(row, out var geometry, out var attrError);
                        if (attrError is not null)
                        {
                            return OperationResult<EditOpResult>.Fail(ErrorCodes.InvalidArgument, attrError);
                        }

                        if (geometry is not null)
                        {
                            op.Create((ArcGIS.Desktop.Mapping.Layer)layer!, geometry, attrs);
                        }
                        else
                        {
                            op.Create((ArcGIS.Desktop.Mapping.MapMember)layer!, attrs);
                        }

                        inserted++;
                    }

                    if (!op.Execute())
                    {
                        return OperationResult<EditOpResult>.Fail(
                            ErrorCodes.InternalError,
                            "EditOperation.Execute() failed: " + DescribeErrors(op));
                    }
                }
                catch (Exception ex)
                {
                    return OperationResult<EditOpResult>.Fail(
                        ErrorCodes.InternalError, "Insert failed: " + ex.Message);
                }

                Track(layer!.Name ?? layerName, inserted);
                return OperationResult<EditOpResult>.Ok(new EditOpResult
                {
                    Action = "insert",
                    MapName = layer.Map?.Name ?? mapName ?? string.Empty,
                    LayerName = layer.Name ?? layerName,
                    Executed = true,
                    RowsAffected = inserted,
                    PendingChangeCount = _pendingCount,
                });
            },
            TaskCreationOptions.None);

    public Task<OperationResult<EditOpResult>> UpdateFeaturesAsync(
        string? mapName, string layerName,
        string? where, IReadOnlyList<long>? oidList,
        IReadOnlyDictionary<string, object?>? attributes, string? geometryWkt,
        bool confirm, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<EditOpResult>>(
            () =>
            {
                if (!confirm)
                {
                    return OperationResult<EditOpResult>.Fail(
                        ErrorCodes.InvalidArgument,
                        "update_features is destructive: confirm=true is required (default-refuse).");
                }

                if (string.IsNullOrWhiteSpace(where) && (oidList is null || oidList.Count == 0))
                {
                    return OperationResult<EditOpResult>.Fail(
                        ErrorCodes.InvalidArgument, "where or oidList is required (refusing table-wide update).");
                }

                if ((attributes is null || attributes.Count == 0) && string.IsNullOrWhiteSpace(geometryWkt))
                {
                    return OperationResult<EditOpResult>.Fail(
                        ErrorCodes.InvalidArgument, "attributes or geometryWkt is required.");
                }

                var (layer, fail) = ResolveFeatureLayer(mapName, layerName);
                if (fail is not null)
                {
                    return OperationResult<EditOpResult>.Fail(fail.Value.Code, fail.Value.Message);
                }

                var guard = GuardTarget(layer!);
                if (guard is not null)
                {
                    return OperationResult<EditOpResult>.Fail(ErrorCodes.PathEscapeRejected, guard);
                }

                Geometry? newGeometry = null;
                if (!string.IsNullOrWhiteSpace(geometryWkt))
                {
                    var wktResult = GeometryWkt.Parse(geometryWkt!, GetSpatialReference(layer!));
                    if (!wktResult.Success)
                    {
                        return OperationResult<EditOpResult>.Fail(ErrorCodes.InvalidArgument, wktResult.Error!);
                    }

                    newGeometry = wktResult.Geometry;
                }

                var oids = ResolveOids(layer!, where, oidList);
                if (oids.Code is not null)
                {
                    return OperationResult<EditOpResult>.Fail(oids.Code, oids.Message!);
                }

                if (oids.Values.Count == 0)
                {
                    return OperationResult<EditOpResult>.Fail(
                        ErrorCodes.NotFound, "No matching features for the given where/oidList (zero rows).");
                }

                var op = new EditOperation
                {
                    Name = "MCP update_features",
                };

                var updated = 0L;
                try
                {
                    foreach (var oid in oids.Values)
                    {
                        if (newGeometry is not null)
                        {
                            op.Modify((ArcGIS.Desktop.Mapping.Layer)layer!, oid, newGeometry, ToMutable(attributes));
                        }
                        else
                        {
                            op.Modify((ArcGIS.Desktop.Mapping.MapMember)layer!, oid, ToMutable(attributes));
                        }

                        updated++;
                    }

                    if (!op.Execute())
                    {
                        return OperationResult<EditOpResult>.Fail(
                            ErrorCodes.InternalError,
                            "EditOperation.Execute() failed: " + DescribeErrors(op));
                    }
                }
                catch (Exception ex)
                {
                    return OperationResult<EditOpResult>.Fail(
                        ErrorCodes.InternalError, "Update failed: " + ex.Message);
                }

                Track(layer!.Name ?? layerName, updated);
                return OperationResult<EditOpResult>.Ok(new EditOpResult
                {
                    Action = "update",
                    MapName = layer.Map?.Name ?? mapName ?? string.Empty,
                    LayerName = layer.Name ?? layerName,
                    Executed = true,
                    RowsAffected = updated,
                    PendingChangeCount = _pendingCount,
                });
            },
            TaskCreationOptions.None);

    public Task<OperationResult<EditOpResult>> DeleteFeaturesAsync(
        string? mapName, string layerName,
        string? where, IReadOnlyList<long>? oidList,
        bool confirm, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<EditOpResult>>(
            () =>
            {
                if (!confirm)
                {
                    return OperationResult<EditOpResult>.Fail(
                        ErrorCodes.InvalidArgument,
                        "delete_features is destructive: confirm=true is required (default-refuse).");
                }

                if (string.IsNullOrWhiteSpace(where) && (oidList is null || oidList.Count == 0))
                {
                    return OperationResult<EditOpResult>.Fail(
                        ErrorCodes.InvalidArgument, "where or oidList is required (refusing table-wide delete).");
                }

                var (layer, fail) = ResolveFeatureLayer(mapName, layerName);
                if (fail is not null)
                {
                    return OperationResult<EditOpResult>.Fail(fail.Value.Code, fail.Value.Message);
                }

                var guard = GuardTarget(layer!);
                if (guard is not null)
                {
                    return OperationResult<EditOpResult>.Fail(ErrorCodes.PathEscapeRejected, guard);
                }

                var oids = ResolveOids(layer!, where, oidList);
                if (oids.Code is not null)
                {
                    return OperationResult<EditOpResult>.Fail(oids.Code, oids.Message!);
                }

                if (oids.Values.Count == 0)
                {
                    return OperationResult<EditOpResult>.Fail(
                        ErrorCodes.NotFound, "No matching features for the given where/oidList (zero rows).");
                }

                var op = new EditOperation
                {
                    Name = "MCP delete_features",
                };

                var deleted = 0L;
                try
                {
                    op.Delete((ArcGIS.Desktop.Mapping.MapMember)layer!, oids.Values);
                    deleted = oids.Values.Count;

                    if (!op.Execute())
                    {
                        return OperationResult<EditOpResult>.Fail(
                            ErrorCodes.InternalError,
                            "EditOperation.Execute() failed: " + DescribeErrors(op));
                    }
                }
                catch (Exception ex)
                {
                    return OperationResult<EditOpResult>.Fail(
                        ErrorCodes.InternalError, "Delete failed: " + ex.Message);
                }

                Track(layer!.Name ?? layerName, deleted);
                return OperationResult<EditOpResult>.Ok(new EditOpResult
                {
                    Action = "delete",
                    MapName = layer.Map?.Name ?? mapName ?? string.Empty,
                    LayerName = layer.Name ?? layerName,
                    Executed = true,
                    RowsAffected = deleted,
                    PendingChangeCount = _pendingCount,
                });
            },
            TaskCreationOptions.None);

    public Task<OperationResult<EditSessionState>> SaveEditsAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<EditSessionState>>(
            async () =>
            {
                var prj = Project.Current;
                if (prj is null)
                {
                    return OperationResult<EditSessionState>.Fail(
                        ErrorCodes.InvalidState, "No current project is available.");
                }

                if (!prj.HasEdits)
                {
                    // 幂等：无编辑可提交 → Ok + 零计数（不伪造成功，如实空态）。
                    return OperationResult<EditSessionState>.Ok(SessionState(prj, resetTracked: true));
                }

                var ok = await prj.SaveEditsAsync().ConfigureAwait(false);
                if (!ok)
                {
                    return OperationResult<EditSessionState>.Fail(
                        ErrorCodes.InternalError, "SaveEditsAsync reported failure; edits remain uncommitted.");
                }

                return OperationResult<EditSessionState>.Ok(SessionState(prj, resetTracked: true));
            },
            TaskCreationOptions.None);

    public Task<OperationResult<EditSessionState>> DiscardEditsAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<EditSessionState>>(
            async () =>
            {
                var prj = Project.Current;
                if (prj is null)
                {
                    return OperationResult<EditSessionState>.Fail(
                        ErrorCodes.InvalidState, "No current project is available.");
                }

                if (!prj.HasEdits)
                {
                    return OperationResult<EditSessionState>.Ok(SessionState(prj, resetTracked: true));
                }

                var ok = await prj.DiscardEditsAsync().ConfigureAwait(false);
                if (!ok)
                {
                    return OperationResult<EditSessionState>.Fail(
                        ErrorCodes.InternalError, "DiscardEditsAsync reported failure; edits remain pending.");
                }

                return OperationResult<EditSessionState>.Ok(SessionState(prj, resetTracked: true));
            },
            TaskCreationOptions.None);

    public Task<OperationResult<EditSessionState>> GetEditSessionAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<EditSessionState>>(
            () =>
            {
                var prj = Project.Current;
                if (prj is null)
                {
                    return OperationResult<EditSessionState>.Fail(
                        ErrorCodes.InvalidState, "No current project is available.");
                }

                return OperationResult<EditSessionState>.Ok(SessionState(prj, resetTracked: false));
            },
            TaskCreationOptions.None);

    // ── 内部 ──

    private EditSessionState SessionState(Project prj, bool resetTracked)
    {
        if (resetTracked)
        {
            lock (_gate)
            {
                _pendingCount = 0;
                _affectedLayers.Clear();
            }
        }

        lock (_gate)
        {
            return new EditSessionState
            {
                HasEdits = prj.HasEdits,
                PendingChangeCount = _pendingCount,
                AffectedLayers = _affectedLayers.ToArray(),
            };
        }
    }

    private void Track(string layerName, long rows)
    {
        lock (_gate)
        {
            _pendingCount += rows;
            if (!_affectedLayers.Contains(layerName))
            {
                _affectedLayers.Add(layerName);
            }
        }
    }

    private (BasicFeatureLayer? Layer, (string Code, string Message)? Fail) ResolveFeatureLayer(
        string? mapName, string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return (null, (ErrorCodes.InvalidArgument, "layerName is required."));
        }

        var resolved = MapResolver.Resolve(mapName);
        if (MapResolver.FailIfNotOk<object>(resolved, mapName) is { } mapFail)
        {
            return (null, (GetCode(mapFail), mapFail.Message ?? "map resolution failed."));
        }

        var map = resolved.Map!;
        var layerResult = LayerResolver.Resolve(map, layerName, flatten: true);
        if (LayerResolver.FailIfNotOk<object>(layerResult, layerName) is { } layerFail)
        {
            return (null, (GetCode(layerFail), layerFail.Message ?? "layer resolution failed."));
        }

        if (layerResult.Layer is not BasicFeatureLayer featureLayer)
        {
            return (null, (ErrorCodes.InvalidArgument,
                $"'{layerName}' is not a feature layer (standalone-table editing is not supported in this batch; disclosed)."));
        }

        return (featureLayer, null);
    }

    private static string GetCode<T>(OperationResult<T> fail)
        => fail.Errors.FirstOrDefault()?.Code ?? ErrorCodes.InternalError;

    /// <summary>目标守卫：底层数据集路径命中受保护根 → 说明（ProtectedOutputPathGuard 同判定）。</summary>
    private static string? GuardTarget(BasicFeatureLayer layer)
    {
        try
        {
            var fc = layer.GetTable() as FeatureClass;
            if (fc is null)
            {
                return "Target layer has no underlying feature class (broken or virtual layer).";
            }

            var path = fc.GetPath();
            var pathText = path.IsAbsoluteUri ? path.LocalPath : path.OriginalString;
            var hit = ProtectedOutputPathGuard.Match(pathText);
            return hit is null ? null : $"Target dataset '{pathText}' is protected ({hit}); edit refused (zero changes).";
        }
        catch (Exception ex)
        {
            // 判定失败 → 失败关闭（不因探测失败而放行编辑）。
            return "Target dataset path could not be determined (" + ex.Message + "); edit refused (fail-closed).";
        }
    }

    private static SpatialReference? GetSpatialReference(BasicFeatureLayer layer)
    {
        try
        {
            return layer.GetSpatialReference();
        }
        catch
        {
            return null;
        }
    }

    private (List<long> Values, string? Code, string? Message) ResolveOids(
        BasicFeatureLayer layer, string? where, IReadOnlyList<long>? oidList)
    {
        if (oidList is { Count: > 0 })
        {
            if (oidList.Count > MaxRowsPerOperation)
            {
                return (new List<long>(), ErrorCodes.InvalidArgument,
                    $"oidList exceeds the per-operation cap ({MaxRowsPerOperation}).");
            }

            return (oidList.ToList(), null, null);
        }

        var oids = new List<long>();
        var table = layer.GetTable();
        if (table is null)
        {
            return (oids, ErrorCodes.InvalidState, "Layer table is unavailable.");
        }

        using (table)
        {
            var qf = new QueryFilter { WhereClause = where ?? string.Empty };
            using var cursor = table.Search(qf, false);
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                oids.Add(row.GetObjectID());
                if (oids.Count >= MaxRowsPerOperation)
                {
                    return (oids, ErrorCodes.InvalidArgument,
                        $"Matching rows exceed the per-operation cap ({MaxRowsPerOperation}); narrow the where clause.");
                }
            }
        }

        return (oids, null, null);
    }

    private static Dictionary<string, object> ToAttributeDictionary(
        IReadOnlyDictionary<string, object?> row, out Geometry? geometry, out string? error)
    {
        geometry = null;
        error = null;
        var attrs = new Dictionary<string, object>();
        foreach (var (key, value) in row)
        {
            if (string.Equals(key, "geometry", StringComparison.OrdinalIgnoreCase))
            {
                if (value is string wkt && !string.IsNullOrWhiteSpace(wkt))
                {
                    var parsed = GeometryWkt.Parse(wkt, null);
                    if (!parsed.Success)
                    {
                        error = parsed.Error;
                        return attrs;
                    }

                    geometry = parsed.Geometry;
                }

                continue;
            }

            if (value is null)
            {
                attrs[key] = null!;
                continue;
            }

            attrs[key] = value switch
            {
                bool b => b,
                int i => i,
                long l => l,
                double d => d,
                float f => f,
                short s => s,
                byte by => by,
                string str => str,
                _ => value.ToString() ?? string.Empty,
            };
        }

        return attrs;
    }

    private static Dictionary<string, object>? ToMutable(IReadOnlyDictionary<string, object?>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return new Dictionary<string, object>();
        }

        var result = new Dictionary<string, object>();
        foreach (var (key, value) in attributes)
        {
            result[key] = value switch
            {
                null => null!,
                bool b => b,
                int i => i,
                long l => l,
                double d => d,
                float f => f,
                short s => s,
                byte by => by,
                string str => str,
                _ => value.ToString() ?? string.Empty,
            };
        }

        return result;
    }

    private static string DescribeErrors(EditOperation op)
    {
        try
        {
            var errs = op.ErrorMessage;
            return string.IsNullOrWhiteSpace(errs) ? "(no error message)" : errs;
        }
        catch
        {
            return "(error message unavailable)";
        }
    }
}
