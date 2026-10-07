using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>属性服务实现（真实访问 ArcGIS Pro 要素属性）。所有 SDK 访问通过 QueuedTask.Run。</summary>
/// <remarks>D-009：地图解析收敛至 <see cref="MapResolver"/>（去 CreateMapFromItem 依赖）。</remarks>
public sealed class AttributeService : IAttributeService
{
    public Task<OperationResult<IReadOnlyList<FeatureInfo>>> QueryFeaturesAsync(AttributeQueryRequest request, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<FeatureInfo>>>(
            () =>
            {
                if (string.IsNullOrWhiteSpace(request.LayerName))
                {
                    return OperationResult<IReadOnlyList<FeatureInfo>>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
                }

                // D-040 B 组：ResolveTable 返回 (Table?, ErrorCode?, ErrorMessage?) —— 分流判定统一走 Core policy。
                var (tableCandidate, accessErrorCode, accessErrorMessage) = ResolveTable(request.MapName, request.LayerName);
                if (accessErrorCode is not null)
                {
                    // K2b（D-022）：错误码由 ResolveTable 判定（地图歧义 → AMBIGUOUS_MAP_NAME；
                    // 图层歧义 → AMBIGUOUS_LAYER_NAME；broken → LAYER_DATA_SOURCE_UNAVAILABLE），
                    // 此处不得再硬编码为地图级错误码。
                    return OperationResult<IReadOnlyList<FeatureInfo>>.Fail(accessErrorCode, accessErrorMessage!);
                }
                var table = tableCandidate!;   // D-040：policy 放行（accessErrorCode==null）=> Table 必非 null（ClassifyLayerAccess 保证）。

                var oidUnavailable = false;
                using (table)
                {
                    var qf = new QueryFilter { WhereClause = request.WhereClause ?? string.Empty };
                    if (request.FieldNames is { Count: > 0 })
                    {
                        // F2（D-022）：OID 必须始终在投影内——否则 row.GetObjectID() 退化为 -1、
                        // 属性里 OBJECTID 为 null。OID 客观不可得时在响应消息中显式披露（禁止静默降级）。
                        var projection = OidProjection.Build(request.FieldNames, TryGetOidField(table));
                        qf.SubFields = string.Join(",", projection.SubFields);
                        oidUnavailable = !projection.OidAvailable;
                    }

                    var features = new List<FeatureInfo>();
                    using (var cursor = table.Search(qf, false))
                    {
                        var max = Math.Max(1, request.MaxFeatures);
                        while (cursor.MoveNext())
                        {
                            if (features.Count >= max)
                            {
                                break;
                            }

                            using var row = cursor.Current;
                            var attrs = new Dictionary<string, object?>();
                            var fields = row.GetFields();
                            foreach (var f in fields)
                            {
                                if (string.IsNullOrEmpty(f.Name))
                                {
                                    continue;
                                }

                                // 跳过几何字段：Shape 的值为非 JSON 可序列化的 ArcGIS 几何对象，
                                // 会令 System.Text.Json 在结果序列化时抛错（Internal error）。
                                if (f.FieldType == FieldType.Geometry)
                                {
                                    continue;
                                }

                                object? value;
                                try
                                {
                                    value = row[f.Name];
                                }
                                catch
                                {
                                    value = null;
                                }

                                attrs[f.Name] = Simplify(value);
                            }

                            features.Add(new FeatureInfo { Oid = row.GetObjectID(), Attributes = attrs });
                        }
                    }

                    return oidUnavailable
                        ? OperationResult<IReadOnlyList<FeatureInfo>>.Ok(features, OidProjection.UnavailableMessage())
                        : OperationResult<IReadOnlyList<FeatureInfo>>.Ok(features);
                }
            },
            TaskCreationOptions.None);

    public Task<OperationResult<IReadOnlyList<FieldInfo>>> GetFieldInfoAsync(string mapName, string layerName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<FieldInfo>>>(
            () =>
            {
                var (tableCandidate, accessErrorCode, accessErrorMessage) = ResolveTable(mapName, layerName);
                if (accessErrorCode is not null)
                {
                    return OperationResult<IReadOnlyList<FieldInfo>>.Fail(accessErrorCode, accessErrorMessage!);
                }
                var table = tableCandidate!;   // D-040：policy 放行（accessErrorCode==null）=> Table 必非 null（ClassifyLayerAccess 保证）。

                using (table)
                {
                    var fields = table.GetDefinition().GetFields()
                        .Select(f => new FieldInfo
                        {
                            Name = f.Name,
                            Alias = f.AliasName ?? f.Name,
                            TypeName = f.FieldType.ToString(),
                            Length = f.Length
                        })
                        .ToList();
                    return OperationResult<IReadOnlyList<FieldInfo>>.Ok(fields);
                }
            },
            TaskCreationOptions.None);

    public Task<OperationResult<long>> GetFeatureCountAsync(string mapName, string layerName, string? whereClause = null, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<long>>(
            () =>
            {
                var (tableCandidate, accessErrorCode, accessErrorMessage) = ResolveTable(mapName, layerName);
                if (accessErrorCode is not null)
                {
                    return OperationResult<long>.Fail(accessErrorCode, accessErrorMessage!);
                }
                var table = tableCandidate!;   // D-040：policy 放行（accessErrorCode==null）=> Table 必非 null（ClassifyLayerAccess 保证）。

                using (table)
                {
                    long count = string.IsNullOrWhiteSpace(whereClause)
                        ? table.GetCount()
                        : table.GetCount(new QueryFilter { WhereClause = whereClause });
                    return OperationResult<long>.Ok(count);
                }
            },
            TaskCreationOptions.None);

    /// <summary>
    /// Phase 9 第四批（D-038）：字段值域画像（**只读**，零写入）。
    /// 先取计数（大表保护：超过 MaxScanRows 显式拒绝，不做无界全扫），再单字段光标全扫：
    /// 去重值上限 maxDistinct（默认 200 / 上限 2000），超出 → Truncated=true 且如实给扫描数。
    /// 字段不存在 → InvalidArgument（错误码零新增）；空表与全空字段可通过 TotalCount/NonNullCount 区分。
    /// </summary>
    public Task<OperationResult<FieldValuesInfo>> GetFieldValuesAsync(
        string mapName, string layerName, string fieldName, int maxDistinct = 200, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<FieldValuesInfo>>(
            () =>
            {
                if (string.IsNullOrWhiteSpace(layerName) || string.IsNullOrWhiteSpace(fieldName))
                {
                    return OperationResult<FieldValuesInfo>.Fail(ErrorCodes.InvalidArgument, "layerName and fieldName are required.");
                }

                var (tableCandidate, accessErrorCode, accessErrorMessage) = ResolveTable(mapName, layerName);
                if (accessErrorCode is not null)
                {
                    return OperationResult<FieldValuesInfo>.Fail(accessErrorCode, accessErrorMessage!);
                }
                var table = tableCandidate!;   // D-040：policy 放行（accessErrorCode==null）=> Table 必非 null（ClassifyLayerAccess 保证）。

                using (table)
                {
                    var definition = table.GetDefinition();
                    var actualField = definition.GetFields()
                        .FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
                    if (actualField is null)
                    {
                        return OperationResult<FieldValuesInfo>.Fail(
                            ErrorCodes.InvalidArgument,
                            $"field '{fieldName}' not found in '{layerName}'.");
                    }

                    var actualName = actualField.Name;
                    long total = table.GetCount();
                    if (AttributeProfilingPolicy.ExceedsScanLimit(total, MaxScanRows))
                    {
                        // 消息文本由 AttributeProfilingPolicy 拼接（与原实现逐字一致，D-040 C 组）。
                        return OperationResult<FieldValuesInfo>.Fail(
                            ErrorCodes.InvalidArgument,
                            AttributeProfilingPolicy.BuildScanLimitMessage(layerName, total, MaxScanRows));
                    }

                    var info = new FieldValuesInfo
                    {
                        MapName = mapName ?? string.Empty,
                        LayerName = layerName,
                        FieldName = actualName,
                        TotalCount = total,
                    };

                    var limit = AttributeProfilingPolicy.ResolveDistinctLimit(maxDistinct, DefaultMaxDistinct, MaxDistinctCap);
                    var distinct = new HashSet<string>(StringComparer.Ordinal);
                    object? min = null;
                    object? max = null;
                    var minMaxType = false;

                    using (var cursor = table.Search(new QueryFilter { SubFields = actualName }, true))
                    {
                        while (cursor.MoveNext())
                        {
                            using var row = cursor.Current;
                            if (row is null) continue;
                            var value = row[actualName];
                            if (value is null || value is DBNull)
                            {
                                info.NullCount++;
                                continue;
                            }

                            info.NonNullCount++;
                            var text = value is IFormattable formattable
                                ? formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                                : value.ToString();
                            if (distinct.Count < limit)
                            {
                                distinct.Add(text!);   // value 非 null（上方已 continue），ToString 实际不为 null。
                            }
                            else
                            {
                                info.Truncated = true;
                            }

                            if (value is IComparable comparable)
                            {
                                if (!minMaxType)
                                {
                                    min = value;
                                    max = value;
                                    minMaxType = true;
                                }
                                else if (min is not null && comparable.CompareTo(min) < 0)
                                {
                                    min = value;
                                }
                                else if (max is not null && comparable.CompareTo(max) > 0)
                                {
                                    max = value;
                                }
                            }
                        }
                    }

                    info.DistinctValues = distinct.OrderBy(v => v, StringComparer.Ordinal).ToList();
                    info.DistinctCount = info.DistinctValues.Count;
                    info.MinValue = ToInvariantString(min);
                    info.MaxValue = ToInvariantString(max);
                    return OperationResult<FieldValuesInfo>.Ok(info);
                }
            },
            TaskCreationOptions.None);

    /// <summary>值域画像的最大扫描行数（大表保护，显式拒绝而非无界扫描）。</summary>
    internal const long MaxScanRows = 200_000;

    /// <summary>maxDistinct 缺省值。</summary>
    internal const int DefaultMaxDistinct = 200;

    /// <summary>maxDistinct 上限。</summary>
    internal const int MaxDistinctCap = 2000;

    private static string? ToInvariantString(object? value)
        => value switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

    /// <summary>按图层名解析 Table。返回 (Table, mapName)。Table 需释放。</summary>
    /// <remarks>
    /// D-009：地图解析收敛至 <see cref="MapResolver"/>；
    /// D-022（F1 + K2b）：图层解析收敛至 <see cref="LayerResolver"/>（三分支 0/1/&gt;1，
    /// 禁止静默取第一）。<paramref name="errorCode"/> 承载**正确层级**的错误码：
    /// 地图歧义 → <see cref="ErrorCodes.AmbiguousMapName"/>；图层歧义 → <see cref="ErrorCodes.AmbiguousLayerName"/>。
    /// 二者绝不可混用（历史缺陷：重名图层被误报为"地图重名"）。
    /// </remarks>
    // D-040 B 组：返回约定改为 (Table?, ErrorCode?, ErrorMessage?) —— "图层存在但数据源不可用"与
    // "对象不存在"在语义上严格可分（G-82-C）；判定逻辑统一走 Core 的 AttributeProfilingPolicy（可单测）。
    private static (Table? Table, string? ErrorCode, string? ErrorMessage) ResolveTable(
        string? mapName,
        string layerName)
    {

        var resolved = MapResolver.Resolve(mapName);
        if (resolved.Status == MapResolveStatus.Ambiguous)
        {
            var hint = resolved.CandidateIds.Count > 0
                ? " Candidate ids: " + string.Join(" | ", resolved.CandidateIds)
                : string.Empty;
            return (null, ErrorCodes.AmbiguousMapName,
                $"Map name '{mapName}' is ambiguous ({resolved.CandidateIds.Count} matches).{hint}");
        }

        var map = resolved.Map;
        if (map is null)
        {
            return (null, ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
        }

        // F1（D-022）：与 get_layer_info / get_layers 同源（flatten=true，G-06），重名 → AMBIGUOUS_LAYER_NAME。
        var layerResult = LayerResolver.Resolve(map, layerName, flatten: true);
        if (layerResult.Status == LayerResolveStatus.Ambiguous)
        {
            var hint = layerResult.CandidateIds.Count > 0
                ? " Candidate layer ids: " + string.Join(" | ", layerResult.CandidateIds)
                : string.Empty;
            return (null, ErrorCodes.AmbiguousLayerName,
                $"Layer name '{layerName}' is ambiguous ({layerResult.CandidateIds.Count} matches).{hint}");
        }

        bool layerExists = layerResult.Layer is not null;
        bool isFeatureLayer = layerResult.Layer is BasicFeatureLayer;
        Table? resolvedTable = null;
        if (isFeatureLayer)
        {
            resolvedTable = ((BasicFeatureLayer)layerResult.Layer!).GetTable();
        }

        var (code, message) = AttributeProfilingPolicy.ClassifyLayerAccess(
            layerExists, isFeatureLayer, resolvedTable is not null, layerName);
        return code is null
            ? (resolvedTable, null, null)
            : (null, code, message);
    }

    /// <summary>
    /// 取该表的实际 OID 字段名（F2）。不可得时返回 <c>null</c>（调用方据此显式披露，禁止静默降级）。
    /// </summary>
    private static string? TryGetOidField(Table table)
    {
        try
        {
            var oidField = table.GetDefinition()?.GetObjectIDField();
            return string.IsNullOrWhiteSpace(oidField) ? null : oidField;
        }
        catch
        {
            // 判定不可得 → 保守：视为无 OID，由调用方在响应消息中披露。
            return null;
        }
    }

    private static object? Simplify(object? value)
        => value is null or DBNull
            ? null
            : value is DateTime dt
                ? dt.ToString("O")
                : value;

    // ═══════════════════════════════════════════════════════════════════
    // D-062 · C 段统计聚合（只读；不落 GP）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>单次统计扫描行数上限（超出 → INVALID_ARGUMENT 保守拒绝，与既有 MaxScanRows 口径一致）。</summary>
    public const long StatisticsScanLimit = 1_000_000;

    /// <summary>分组聚合组数上限（超出 → INVALID_ARGUMENT；提示收窄 where/分组字段）。</summary>
    public const long SummaryGroupLimit = 10_000;

    public Task<OperationResult<FieldStatisticsInfo>> GetFieldStatisticsAsync(
        string? mapName, string layerName, string fieldName, string? whereClause = null, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<FieldStatisticsInfo>>(
            () =>
            {
                if (string.IsNullOrWhiteSpace(layerName) || string.IsNullOrWhiteSpace(fieldName))
                {
                    return OperationResult<FieldStatisticsInfo>.Fail(
                        ErrorCodes.InvalidArgument, "layerName and fieldName are required.");
                }

                var (tableCandidate, accessErrorCode, accessErrorMessage) = ResolveTable(mapName, layerName);
                if (accessErrorCode is not null)
                {
                    return OperationResult<FieldStatisticsInfo>.Fail(accessErrorCode, accessErrorMessage!);
                }
                var table = tableCandidate!;

                using (table)
                {
                    var definition = table.GetDefinition();
                    var actualField = definition.GetFields()
                        .FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
                    if (actualField is null)
                    {
                        return OperationResult<FieldStatisticsInfo>.Fail(
                            ErrorCodes.InvalidArgument, $"field '{fieldName}' not found in '{layerName}'.");
                    }

                    if (!IsNumericFieldType(actualField.FieldType))
                    {
                        return OperationResult<FieldStatisticsInfo>.Fail(
                            ErrorCodes.InvalidArgument,
                            $"field '{actualField.Name}' is {actualField.FieldType} (numeric required for statistics).");
                    }

                    var info = new FieldStatisticsInfo
                    {
                        MapName = mapName ?? string.Empty,
                        LayerName = layerName,
                        FieldName = actualField.Name,
                        WhereClause = whereClause,
                    };

                    var values = new List<double>();
                    var qf = new QueryFilter { WhereClause = whereClause ?? string.Empty };
                    qf.SubFields = actualField.Name;
                    using (var cursor = table.Search(qf, false))
                    {
                        while (cursor.MoveNext())
                        {
                            using var row = cursor.Current;
                            var raw = row[actualField.Name];
                            if (raw is null or DBNull)
                            {
                                info.Skipped++;
                                continue;
                            }

                            if (raw is double d) values.Add(d);
                            else if (raw is float f) values.Add(f);
                            else if (raw is int i) values.Add(i);
                            else if (raw is long l) values.Add(l);
                            else if (raw is short s) values.Add(s);
                            else if (raw is decimal m) values.Add((double)m);
                            else if (double.TryParse(raw.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) values.Add(parsed);
                            else info.Skipped++;

                            if (values.Count + info.Skipped > StatisticsScanLimit)
                            {
                                return OperationResult<FieldStatisticsInfo>.Fail(
                                    ErrorCodes.InvalidArgument,
                                    $"Scan exceeds the statistics limit ({StatisticsScanLimit} rows); narrow with whereClause.");
                            }
                        }
                    }

                    info.Count = values.Count;
                    var (min, max, mean, median, sum, stddev) = FieldMath.Reduce(values);
                    info.Min = min;
                    info.Max = max;
                    info.Mean = mean;
                    info.Median = median;
                    info.Sum = sum;
                    info.StdDev = stddev;

                    return OperationResult<FieldStatisticsInfo>.Ok(info);
                }
            },
            TaskCreationOptions.None);

    public Task<OperationResult<GroupSummaryInfo>> SummarizeFeaturesAsync(
        string? mapName, string layerName,
        IReadOnlyList<string> groupByFields, string? aggField, IReadOnlyList<string> aggregations,
        string? whereClause = null, int topN = 0, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<GroupSummaryInfo>>(
            () =>
            {
                if (groupByFields is null || groupByFields.Count == 0)
                {
                    return OperationResult<GroupSummaryInfo>.Fail(
                        ErrorCodes.InvalidArgument, "groupByFields (at least one) is required.");
                }

                var numericAggs = new[] { "sum", "min", "max", "mean" };
                var requested = (aggregations ?? Array.Empty<string>())
                    .Select(a => a.Trim().ToLowerInvariant())
                    .Where(a => a is "count" or "sum" or "min" or "max" or "mean" or "first" or "last")
                    .Distinct()
                    .ToList();
                if (requested.Count == 0)
                {
                    requested.Add("count");
                }

                if (requested.Any(a => numericAggs.Contains(a)) && string.IsNullOrWhiteSpace(aggField))
                {
                    return OperationResult<GroupSummaryInfo>.Fail(
                        ErrorCodes.InvalidArgument, $"aggField is required for aggregations [{string.Join(", ", requested.Where(a => numericAggs.Contains(a)))}].");
                }

                var (tableCandidate, accessErrorCode, accessErrorMessage) = ResolveTable(mapName, layerName);
                if (accessErrorCode is not null)
                {
                    return OperationResult<GroupSummaryInfo>.Fail(accessErrorCode, accessErrorMessage!);
                }
                var table = tableCandidate!;

                using (table)
                {
                    var definition = table.GetDefinition();
                    var groupFields = new List<Field>();
                    foreach (var name in groupByFields)
                    {
                        var f = definition.GetFields()
                            .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                        if (f is null)
                        {
                            return OperationResult<GroupSummaryInfo>.Fail(
                                ErrorCodes.InvalidArgument, $"group-by field '{name}' not found in '{layerName}'.");
                        }

                        groupFields.Add(f);
                    }

                    Field? agg = null;
                    if (!string.IsNullOrWhiteSpace(aggField))
                    {
                        agg = definition.GetFields()
                            .FirstOrDefault(x => string.Equals(x.Name, aggField, StringComparison.OrdinalIgnoreCase));
                        if (agg is null)
                        {
                            return OperationResult<GroupSummaryInfo>.Fail(
                                ErrorCodes.InvalidArgument, $"aggField '{aggField}' not found in '{layerName}'.");
                        }

                        if (requested.Any(a => numericAggs.Contains(a)) && !IsNumericFieldType(agg.FieldType))
                        {
                            return OperationResult<GroupSummaryInfo>.Fail(
                                ErrorCodes.InvalidArgument,
                                $"aggField '{agg.Name}' is {agg.FieldType} (numeric required for sum/min/max/mean).");
                        }
                    }

                    var scan = new List<string> { "OBJECTID" };
                    scan.AddRange(groupFields.Select(f => f.Name));
                    if (agg is not null)
                    {
                        scan.Add(agg.Name);
                    }

                    var qf = new QueryFilter { WhereClause = whereClause ?? string.Empty };
                    qf.SubFields = string.Join(",", scan.Distinct());

                    var groups = new Dictionary<string, GroupAccumulator>(StringComparer.Ordinal);
                    using (var cursor = table.Search(qf, false))
                    {
                        while (cursor.MoveNext())
                        {
                            using var row = cursor.Current;
                            var groupValues = groupFields.Select(f => Simplify(row[f.Name])?.ToString()).ToList();
                            var key = string.Join(" | ", groupValues);
                            if (!groups.TryGetValue(key, out var acc))
                            {
                                if (groups.Count >= SummaryGroupLimit)
                                {
                                    return OperationResult<GroupSummaryInfo>.Fail(
                                        ErrorCodes.InvalidArgument,
                                        $"Groups exceed the limit ({SummaryGroupLimit}); narrow with whereClause or fewer group-by fields.");
                                }

                                acc = new GroupAccumulator(groupValues);
                                groups[key] = acc;
                            }

                            acc.Push(agg is null ? null : Simplify(row[agg.Name]), requested);
                        }
                    }

                    var info = new GroupSummaryInfo
                    {
                        MapName = mapName ?? string.Empty,
                        LayerName = layerName,
                        GroupByFields = groupFields.Select(f => f.Name).ToList(),
                        AggField = agg?.Name,
                        Aggregations = requested,
                        WhereClause = whereClause,
                        TotalGroups = groups.Count,
                    };

                    IEnumerable<GroupAccumulator> ordered = groups.Values.OrderByDescending(g => g.Count);
                    if (topN > 0 && groups.Count > topN)
                    {
                        ordered = ordered.Take(topN);
                        info.TruncatedAt = topN;
                    }

                    info.Rows = ordered.Select(g => g.ToRow()).ToList();
                    return OperationResult<GroupSummaryInfo>.Ok(info);
                }
            },
            TaskCreationOptions.None);

    private static bool IsNumericFieldType(FieldType type) => type switch
    {
        FieldType.Integer or FieldType.Single or FieldType.Double or FieldType.SmallInteger => true,
        _ => false,
    };

    /// <summary>分组累加器（遍历序 first/last；数值聚合双精度累计）。</summary>
    private sealed class GroupAccumulator
    {
        public GroupAccumulator(IReadOnlyList<string?> groupValues)
        {
            GroupValues = groupValues;
        }

        public IReadOnlyList<string?> GroupValues { get; }

        public long Count { get; private set; }

        private double _sum;
        private double? _min;
        private double? _max;
        private double _numericSum;
        private long _numericCount;
        private string? _first;
        private string? _last;

        public void Push(object? value, List<string> aggs)
        {
            Count++;
            var text = value?.ToString();
            if (_first is null)
            {
                _first = text;
            }

            _last = text;

            if (value is null or DBNull)
            {
                return;
            }

            if (double.TryParse(value.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
            {
                _sum += d;
                _min = _min is null ? d : Math.Min(_min.Value, d);
                _max = _max is null ? d : Math.Max(_max.Value, d);
                _numericSum += d;
                _numericCount++;
            }
        }

        public GroupSummaryRow ToRow()
        {
            var row = new GroupSummaryRow
            {
                Key = string.Join(" | ", GroupValues),
                GroupValues = GroupValues,
                Count = Count,
                First = _first,
                Last = _last,
            };

            if (Count > 0)
            {
                row.Sum = _sum;
                row.Min = _min;
                row.Max = _max;
                // mean 分母 = 可解析数值行数（空值/非数值不计入，与 C1 空值口径一致）。
                row.Mean = _numericCount > 0 ? _numericSum / _numericCount : null;
            }

            return row;
        }
    }
}
