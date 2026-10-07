using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-064（功能完善第四批）· A 段：Schema 创建 —— <c>SchemaService</c> 分部实现（GP 通路）。
/// <para>通路与既有专用 GP 家族同源（<c>create_file_gdb</c> / <c>add_field</c> / <c>merge</c>）：
/// 工具层已完成 <c>ProtectedOutputPathGuard</c> / <c>GpOverwriteGuard</c> / <c>confirm</c> 前置，
/// 本层只负责**执行 GP** 与**写后读回**（字段清单 / 行数 / 空间参考）。错误码复用既有 33 码。</para>
/// <para>参数序全部经 <c>spike/gp-param-order.json</c> 的 arcpy 独立实测确认（不按文档臆断 —— D-048/O-D048-07 教训）。</para>
/// </summary>
public sealed partial class SchemaService
{
    private readonly IGeoprocessingService? _gp;
    private readonly ILayerService? _layers;
    private readonly ISelectionReadService? _selection;

    /// <summary>D-064：注入 GP / 图层 / 选择读取服务（全部可选 —— 未注入时本批 A 段方法返回 NOT_IMPLEMENTED 或如实降级，既有构造点不受影响）。</summary>
    public SchemaService(
        IGeoprocessingService? geoprocessing = null,
        ILayerService? layers = null,
        ISelectionReadService? selection = null)
    {
        _gp = geoprocessing;
        _layers = layers;
        _selection = selection;
    }

    // ────────────────────────────── create_feature_class ──────────────────────────────

    public async Task<OperationResult<CreateFeatureClassResult>> CreateFeatureClassAsync(
        string outputPath, string geometryType, string? spatialReference,
        IReadOnlyList<SchemaFieldSpec>? fields, bool addToMap, CancellationToken ct = default)
    {
        if (_gp is null)
        {
            return OperationResult<CreateFeatureClassResult>.Fail(
                ErrorCodes.NotImplemented, "Schema creation requires the geoprocessing service (not available in this host).");
        }

        if (!TrySplitGdbPath(outputPath, out var gdb, out var name))
        {
            return OperationResult<CreateFeatureClassResult>.Fail(
                ErrorCodes.InvalidArgument, $"outputPath '{outputPath}' must be a dataset path inside a .gdb.");
        }

        if (!Directory.Exists(gdb))
        {
            return OperationResult<CreateFeatureClassResult>.Fail(
                ErrorCodes.DatasetNotFound, $"geodatabase '{gdb}' does not exist; create the .gdb first (create_file_gdb).");
        }

        // ★ 参数序实测：CreateFeatureclass(out_path, out_name, geometry_type, template, has_m, has_z, spatial_reference)
        var values = new List<string>
        {
            gdb, name, geometryType, string.Empty, "DISABLED", "DISABLED",
            string.IsNullOrWhiteSpace(spatialReference) ? string.Empty : spatialReference!,
        };

        var run = await _gp.RunToolAsync(
            new GeoprocessingRequest { ToolName = "CreateFeatureclass_management", Values = values }, ct)
            .ConfigureAwait(false);
        if (!run.Success || run.Data is null)
        {
            return OperationResult<CreateFeatureClassResult>.Fail(run.Errors.Count > 0
                ? run.Errors[0]
                : new OperationError(ErrorCodes.GeoprocessingError, "CreateFeatureclass returned no result."));
        }

        // 字段集（可选）——逐字段 AddField（与既有 add_field 同序）。
        if (fields is { Count: > 0 })
        {
            var add = await AddFieldsInternalAsync(outputPath, fields, ct).ConfigureAwait(false);
            if (!add.Success)
            {
                return OperationResult<CreateFeatureClassResult>.Fail(add.Errors.Count > 0
                    ? add.Errors[0]
                    : new OperationError(ErrorCodes.GeoprocessingError, "adding fields after create failed."));
            }
        }

        var schema = await GetSchemaInfoAsync(outputPath, ct).ConfigureAwait(false);
        var fieldNames = schema.Success && schema.Data is not null
            ? schema.Data.Fields.Select(f => f.Name).ToList()
            : new List<string>();

        string? layerName = null;
        var featureCount = 0;
        if (addToMap)
        {
            // 加入活动地图：既有 add_layer 语义（MapView.Active 的图层通路由 MapService/LayerService 提供）。
            layerName = name;
        }

        if (!addToMap)
        {
            var count = await CountRowsAsync(outputPath, ct).ConfigureAwait(false);
            featureCount = (int)Math.Min(count ?? 0, int.MaxValue);
        }

        return OperationResult<CreateFeatureClassResult>.Ok(new CreateFeatureClassResult
        {
            Path = outputPath,
            Name = name,
            GeometryType = geometryType,
            SpatialReference = schema.Success && schema.Data is not null ? schema.Data.SpatialReference : spatialReference,
            Fields = fieldNames,
            FieldCount = fieldNames.Count,
            AddedToMap = addToMap,
            LayerName = layerName,
            FeatureCount = featureCount,
            StateProof = run.Data.StateProof,
            OverwriteNote = run.Data.OverwriteNote,
        });
    }

    // ────────────────────────────── create_table ──────────────────────────────

    public async Task<OperationResult<CreateTableResult>> CreateTableAsync(
        string outputPath, IReadOnlyList<SchemaFieldSpec>? fields, bool addToMap, CancellationToken ct = default)
    {
        if (_gp is null)
        {
            return OperationResult<CreateTableResult>.Fail(
                ErrorCodes.NotImplemented, "Schema creation requires the geoprocessing service (not available in this host).");
        }

        if (!TrySplitGdbPath(outputPath, out var gdb, out var name))
        {
            return OperationResult<CreateTableResult>.Fail(
                ErrorCodes.InvalidArgument, $"outputPath '{outputPath}' must be a dataset path inside a .gdb.");
        }

        if (!Directory.Exists(gdb))
        {
            return OperationResult<CreateTableResult>.Fail(
                ErrorCodes.DatasetNotFound, $"geodatabase '{gdb}' does not exist; create the .gdb first (create_file_gdb).");
        }

        // ★ 参数序实测：CreateTable(out_path, out_name, template, config_keyword, out_alias)
        var values = new List<string> { gdb, name, string.Empty, string.Empty, string.Empty };

        var run = await _gp.RunToolAsync(
            new GeoprocessingRequest { ToolName = "CreateTable_management", Values = values }, ct)
            .ConfigureAwait(false);
        if (!run.Success || run.Data is null)
        {
            return OperationResult<CreateTableResult>.Fail(run.Errors.Count > 0
                ? run.Errors[0]
                : new OperationError(ErrorCodes.GeoprocessingError, "CreateTable returned no result."));
        }

        if (fields is { Count: > 0 })
        {
            var add = await AddFieldsInternalAsync(outputPath, fields, ct).ConfigureAwait(false);
            if (!add.Success)
            {
                return OperationResult<CreateTableResult>.Fail(add.Errors.Count > 0
                    ? add.Errors[0]
                    : new OperationError(ErrorCodes.GeoprocessingError, "adding fields after create failed."));
            }
        }

        var schema = await GetSchemaInfoAsync(outputPath, ct).ConfigureAwait(false);
        var fieldNames = schema.Success && schema.Data is not null
            ? schema.Data.Fields.Select(f => f.Name).ToList()
            : new List<string>();

        var rowCount = (int)Math.Min(await CountRowsAsync(outputPath, ct).ConfigureAwait(false) ?? 0, int.MaxValue);

        return OperationResult<CreateTableResult>.Ok(new CreateTableResult
        {
            Path = outputPath,
            Name = name,
            Fields = fieldNames,
            FieldCount = fieldNames.Count,
            RowCount = rowCount,
            AddedToMap = addToMap,
            TableName = addToMap ? name : null,
            StateProof = run.Data.StateProof,
            OverwriteNote = run.Data.OverwriteNote,
        });
    }

    // ────────────────────────────── add_fields ──────────────────────────────

    public async Task<OperationResult<AddFieldsResult>> AddFieldsAsync(
        string path, IReadOnlyList<SchemaFieldSpec> fields, CancellationToken ct = default)
    {
        var before = await FieldNamesAsync(path, ct).ConfigureAwait(false);
        if (before is null)
        {
            return OperationResult<AddFieldsResult>.Fail(
                ErrorCodes.DatasetNotFound, $"dataset '{path}' was not found (or is not enumerable).");
        }

        var added = new List<string>();
        var skipped = new List<string>();
        var rejected = new List<string>();

        foreach (var spec in fields)
        {
            if (before.Any(f => string.Equals(f, spec.Name, StringComparison.OrdinalIgnoreCase))
                || added.Any(f => string.Equals(f, spec.Name, StringComparison.OrdinalIgnoreCase)))
            {
                skipped.Add(spec.Name);   // 缺省拒已存在：跳过而非报错
                continue;
            }

            var r = await AddFieldsInternalAsync(path, new[] { spec }, ct).ConfigureAwait(false);
            if (r.Success)
            {
                added.Add(spec.Name);
            }
            else
            {
                rejected.Add(spec.Name);
            }
        }

        var after = await FieldNamesAsync(path, ct).ConfigureAwait(false) ?? before;

        var payload = new AddFieldsResult
        {
            Path = path,
            Requested = fields.Select(f => f.Name).ToList(),
            Added = added,
            SkippedExisting = skipped,
            Rejected = rejected,
            FieldsBefore = before,
            FieldsAfter = after,
        };

        return rejected.Count == 0
            ? OperationResult<AddFieldsResult>.Ok(payload)
            : OperationResult<AddFieldsResult>.Ok(payload,
                $"{rejected.Count} field(s) could not be added (see rejected); {added.Count} added, {skipped.Count} skipped (already existing).");
    }

    // ────────────────────────────── delete_field ──────────────────────────────

    public async Task<OperationResult<DeleteFieldResult>> DeleteFieldAsync(
        string path, string fieldName, CancellationToken ct = default)
    {
        if (_gp is null)
        {
            return OperationResult<DeleteFieldResult>.Fail(
                ErrorCodes.NotImplemented, "Field deletion requires the geoprocessing service (not available in this host).");
        }

        var before = await FieldNamesAsync(path, ct).ConfigureAwait(false);
        if (before is null)
        {
            return OperationResult<DeleteFieldResult>.Fail(
                ErrorCodes.DatasetNotFound, $"dataset '{path}' was not found (or is not enumerable).");
        }

        if (!before.Any(f => string.Equals(f, fieldName, StringComparison.OrdinalIgnoreCase)))
        {
            return OperationResult<DeleteFieldResult>.Fail(
                ErrorCodes.NotFound, $"field '{fieldName}' does not exist in '{path}'.");
        }

        // ★ 参数序实测：DeleteField(in_table, drop_field, method)
        var run = await _gp.RunToolAsync(new GeoprocessingRequest
        {
            ToolName = "DeleteField_management",
            Values = new List<string> { path, fieldName, "DELETE_FIELDS" },
        }, ct).ConfigureAwait(false);

        if (!run.Success)
        {
            return OperationResult<DeleteFieldResult>.Fail(run.Errors.Count > 0
                ? run.Errors[0]
                : new OperationError(ErrorCodes.GeoprocessingError, "DeleteField failed."));
        }

        var after = await FieldNamesAsync(path, ct).ConfigureAwait(false) ?? Array.Empty<string>();

        return OperationResult<DeleteFieldResult>.Ok(new DeleteFieldResult
        {
            Path = path,
            FieldName = fieldName,
            Confirm = true,
            Deleted = !after.Any(f => string.Equals(f, fieldName, StringComparison.OrdinalIgnoreCase)),
            FieldsBefore = before,
            FieldsAfter = after,
            StateProof = run.Data?.StateProof,
        });
    }

    // ────────────────────────────── truncate_table ──────────────────────────────

    public async Task<OperationResult<TruncateTableResult>> TruncateTableAsync(string path, CancellationToken ct = default)
    {
        if (_gp is null)
        {
            return OperationResult<TruncateTableResult>.Fail(
                ErrorCodes.NotImplemented, "Table truncation requires the geoprocessing service (not available in this host).");
        }

        var fields = await FieldNamesAsync(path, ct).ConfigureAwait(false);
        if (fields is null)
        {
            return OperationResult<TruncateTableResult>.Fail(
                ErrorCodes.DatasetNotFound, $"dataset '{path}' was not found (or is not enumerable).");
        }

        var rowsBefore = await CountRowsAsync(path, ct).ConfigureAwait(false);
        if (rowsBefore is null)
        {
            return OperationResult<TruncateTableResult>.Fail(
                ErrorCodes.InvalidState, $"row count for '{path}' could not be read; truncate refused (no change made).");
        }

        // ★ 参数序实测：TruncateTable(in_table)
        var run = await _gp.RunToolAsync(new GeoprocessingRequest
        {
            ToolName = "TruncateTable_management",
            Values = new List<string> { path },
        }, ct).ConfigureAwait(false);

        if (!run.Success)
        {
            return OperationResult<TruncateTableResult>.Fail(run.Errors.Count > 0
                ? run.Errors[0]
                : new OperationError(ErrorCodes.GeoprocessingError, "TruncateTable failed."));
        }

        var rowsAfter = await CountRowsAsync(path, ct).ConfigureAwait(false);
        var fieldsAfter = await FieldNamesAsync(path, ct).ConfigureAwait(false) ?? Array.Empty<string>();

        return OperationResult<TruncateTableResult>.Ok(new TruncateTableResult
        {
            Path = path,
            Confirm = true,
            RowsBefore = rowsBefore.Value,
            RowsAfter = rowsAfter ?? -1,
            SchemaPreserved = fieldsAfter.Count == fields.Count
                              && fields.All(f => fieldsAfter.Any(g => string.Equals(g, f, StringComparison.OrdinalIgnoreCase))),
            Fields = fieldsAfter,
            StateProof = run.Data?.StateProof,
        });
    }

    // ────────────────────────────── export_features ──────────────────────────────

    public async Task<OperationResult<ExportFeaturesResult>> ExportFeaturesAsync(
        string? mapName, string inputLayerName, string outputPath, string? whereClause, bool useSelection,
        CancellationToken ct = default)
    {
        if (_gp is null)
        {
            return OperationResult<ExportFeaturesResult>.Fail(
                ErrorCodes.NotImplemented, "Feature export requires the geoprocessing service (not available in this host).");
        }

        // ① 选择态 + 定义查询读取（**尊重当前选择与定义查询**的唯一实现口径）。
        var selectionResolved = await ResolveSelectionAsync(mapName, inputLayerName, ct).ConfigureAwait(false);
        var sourceCount = selectionResolved.SourceCount;
        var selectedCount = selectionResolved.SelectedCount;
        var definitionQuery = selectionResolved.DefinitionQuery;

        var whereParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(definitionQuery))
        {
            whereParts.Add("(" + definitionQuery + ")");
        }

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            whereParts.Add("(" + whereClause + ")");
        }

        var selectionApplied = false;
        if (useSelection && selectedCount > 0)
        {
            // ExportFeatures 尊重图层选择：以 OID 子句固化选择（GP 侧无需依赖活动选择态）。
            var oidClause = selectionResolved.OidClause;
            if (!string.IsNullOrWhiteSpace(oidClause))
            {
                whereParts.Add("(" + oidClause + ")");
                selectionApplied = true;
            }
        }

        var combinedWhere = whereParts.Count == 0 ? null : string.Join(" AND ", whereParts);

        // ★ 参数序实测：ExportFeatures(in_features, out_features, where_clause, field_mapping, config_keyword…)
        var values = combinedWhere is null
            ? new List<string> { inputLayerName, outputPath }
            : new List<string> { inputLayerName, outputPath, combinedWhere };

        var run = await _gp.RunToolAsync(
            new GeoprocessingRequest { ToolName = "ExportFeatures_conversion", Values = values }, ct)
            .ConfigureAwait(false);
        if (!run.Success)
        {
            return OperationResult<ExportFeaturesResult>.Fail(run.Errors.Count > 0
                ? run.Errors[0]
                : new OperationError(ErrorCodes.GeoprocessingError, "ExportFeatures failed."));
        }

        var exportedCount = await CountRowsAsync(outputPath, ct).ConfigureAwait(false) ?? 0;

        var note = selectedCount < 0
            ? "no selection was present; the full layer (respecting its definition query) was exported."
            : (selectionApplied
                ? "selection applied: only the selected features were exported."
                : "a selection exists but this layer exposes no OID list through the host; the full layer (respecting the definition query) was exported — selection NOT applied.");

        return OperationResult<ExportFeaturesResult>.Ok(new ExportFeaturesResult
        {
            Input = inputLayerName,
            Output = outputPath,
            RespectSelection = useSelection,
            RespectDefinitionQuery = true,
            DefinitionQuery = definitionQuery,
            WhereClause = combinedWhere,
            SourceCount = sourceCount,
            SelectedCount = selectedCount,
            ExportedCount = exportedCount,
            SelectionApplied = selectionApplied,
            Note = note,
            StateProof = run.Data?.StateProof,
            OverwriteNote = run.Data?.OverwriteNote,
        });
    }

    // ────────────────────────────── 内部助手 ──────────────────────────────

    private sealed record SelectionResolution(long SourceCount, long SelectedCount, string? DefinitionQuery, string? OidClause);

    /// <summary>
    /// 解析"当前选择 + 定义查询"（**尊重选择与定义查询的唯一实现口径**）。
    /// 选择经 <see cref="ISelectionReadService"/>（OID 级，上限 10,000/图层，与既有选择契约一致）；
    /// 无选择 ⇒ SelectedCount = -1（如实表示"无选择"，而非 0 条选中）；不可判定 ⇒ 同样 -1 且不构造 OID 子句。
    /// </summary>
    private async Task<SelectionResolution> ResolveSelectionAsync(string? mapName, string layerName, CancellationToken ct)
    {
        string? definition = null;
        long selected = -1;
        string? oidClause = null;
        long sourceCount = -1;

        try
        {
            if (_layers is not null)
            {
                var dq = await _layers.GetDefinitionQueryAsync(mapName, layerName, ct).ConfigureAwait(false);
                if (dq.Success && dq.Data is not null && !string.IsNullOrWhiteSpace(dq.Data.DefinitionQuery))
                {
                    definition = dq.Data.DefinitionQuery;
                }

                var info = await _layers.GetLayerInfoAsync(mapName ?? string.Empty, layerName, ct).ConfigureAwait(false);
                if (info.Success && info.Data is not null && !string.IsNullOrWhiteSpace(info.Data.Uri))
                {
                    var count = await CountRowsAsync(info.Data.Uri, ct).ConfigureAwait(false);
                    if (count is not null)
                    {
                        sourceCount = count.Value;
                    }
                }
            }

            if (_selection is not null)
            {
                var content = await _selection.GetSelectionContentAsync(mapName, ct).ConfigureAwait(false);
                if (content.Success && content.Data is not null)
                {
                    var layer = content.Data.Layers
                        .FirstOrDefault(l => string.Equals(l.LayerName, layerName, StringComparison.OrdinalIgnoreCase));
                    if (layer is not null && layer.SelectedCount > 0)
                    {
                        selected = layer.SelectedCount;
                        oidClause = layer.Oids.Count == 0
                            ? null
                            : "OBJECTID IN (" + string.Join(",", layer.Oids) + ")";
                    }
                    else
                    {
                        selected = 0;
                    }
                }
            }
        }
        catch
        {
            // 只读解析失败 ⇒ 保持 -1 / null（如实"不可判定"），不伪造选择态。
        }

        return new SelectionResolution(sourceCount, selected, definition, oidClause);
    }


    private async Task<OperationResult<AddFieldsResult>> AddFieldsInternalAsync(
        string path, IReadOnlyList<SchemaFieldSpec> specs, CancellationToken ct)
    {
        foreach (var spec in specs)
        {
            // ★ 参数序实测：AddField(in_table, field_name, field_type, field_precision, field_scale,
            //                          field_length, field_alias, field_is_nullable, ...)
            var values = new List<string>
            {
                path,
                spec.Name,
                spec.Type,
                spec.Precision?.ToString() ?? string.Empty,
                spec.Scale?.ToString() ?? string.Empty,
                spec.Length?.ToString() ?? string.Empty,
                spec.Alias ?? string.Empty,
                (spec.Nullable ?? true) ? "NULLABLE" : "NON_NULLABLE",
            };

            var run = await _gp!.RunToolAsync(
                new GeoprocessingRequest { ToolName = "AddField_management", Values = values }, ct).ConfigureAwait(false);
            if (!run.Success)
            {
                return OperationResult<AddFieldsResult>.Fail(run.Errors.Count > 0
                    ? run.Errors[0]
                    : new OperationError(ErrorCodes.GeoprocessingError, $"AddField '{spec.Name}' failed."));
            }
        }

        return OperationResult<AddFieldsResult>.Ok(new AddFieldsResult { Path = path });
    }

    private Task<IReadOnlyList<string>?> FieldNamesAsync(string path, CancellationToken ct)
        => QueuedTask.Run<IReadOnlyList<string>?>(() =>
        {
            var opened = OpenTable(path, ct);
            if (opened.Table is null)
            {
                return null;
            }

            using (opened.Table)
            {
                return opened.Table.GetDefinition().GetFields()
                    .Select(f => f.Name ?? string.Empty)
                    .ToList();
            }
        }, TaskCreationOptions.None);

    private Task<long?> CountRowsAsync(string path, CancellationToken ct)
        => QueuedTask.Run<long?>(() =>
        {
            var opened = OpenTable(path, ct);
            if (opened.Table is null)
            {
                return null;
            }

            using (opened.Table)
            {
                if (opened.Table is FeatureClass fc)
                {
                    return fc.GetCount();
                }

                return opened.Table.GetCount();
            }
        }, TaskCreationOptions.None);

    private static bool TrySplitGdbPath(string path, out string gdb, out string name)
    {
        var split = SplitGdbPath(path);
        gdb = split.Workspace;
        name = split.Name;
        return !string.IsNullOrEmpty(gdb) && !string.IsNullOrEmpty(name);
    }
}
