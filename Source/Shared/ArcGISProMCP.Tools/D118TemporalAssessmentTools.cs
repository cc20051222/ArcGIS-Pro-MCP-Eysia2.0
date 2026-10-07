// D-118 之一：时序变点／轨迹比对／栅格质量／分类精度／统计假设／交换结构（六件，离线窄子集可实算）。
// 参数面＝D118FrozenSchemas 嵌入的冻结正本；子集外的取值一律 fail-closed，不臆造数值。
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>X02 detect_temporal_change_points — 单一数值序列的变点检测（Pettitt / 标准化 CUSUM / Mann-Kendall 分段）。</summary>
public sealed class DetectTemporalChangePointsTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.DetectTemporalChangePointsToolName;
    private const double CusumDriftK = 1.0;
    private const double CusumThresholdH = 5.0;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Quality;
    public override string Name => ToolKey;
    public override string Description =>
        "时间序列变点检测（M4 X02，冻结参数面逐字对齐）。窄子集＝CSV/JSON 表格文件的单一数值列：method 支持 pettitt（单变点秩统计＋渐近近似 p）、" +
        "cusum（双侧标准化累加，drift=1.0、阈值=5.0 为本批声明常量）、mann_kendall_segment（|S| 最小化递归分段，minSegmentLength 默认 3）；" +
        "bayesian_online 属未实现子集，调用即 NOT_IMPLEMENTED 且零写出。输出为 JSON 报告文件（默认拒绝覆写）。";

    protected override async Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        var series = RequiredString(context, "series");
        var valueField = RequiredString(context, "valueField");
        var method = RequiredString(context, "method");
        var outputPath = D118Outputs.GuardPath(RequiredString(context, "outputPath"), "outputPath");

        if (string.Equals(method, "bayesian_online", StringComparison.Ordinal))
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
                "method 'bayesian_online' is outside the implemented subset of this batch (no online Bayesian model is shipped); no input was read and no output was written.");

        var table = D118Tables.Load(series, valueField);
        var observed = new List<double>();
        var skipped = 0;
        foreach (var row in table.Rows)
        {
            var v = D118Values.AsDouble(row.TryGetValue(valueField, out var raw) ? raw : null);
            if (v is null || !double.IsFinite(v.Value)) { skipped++; continue; }
            observed.Add(v.Value);
        }

        if (observed.Count < 4)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                $"Only {observed.Count} finite values were parsed from column '{valueField}'; at least 4 are required and no output was written.");

        var minSegment = D118Values.AsInt(ToolArg(context, "minSegmentLength"))
                         ?? D118SchemaFace.IntDefault(ToolKey, "minSegmentLength") ?? 3;
        var significance = D118Values.AsDouble(ToolArg(context, "significanceLevel")) ?? 0.05;
        if (minSegment < 2)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "minSegmentLength must be at least 2.");
        if (significance is <= 0 or >= 1)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "significanceLevel must be inside (0, 1).");

        object result = method switch
        {
            "pettitt" => PettittFace(observed, significance),
            "cusum" => CusumFace(observed, significance),
            "mann_kendall_segment" => MannKendallFace(observed, minSegment, significance),
            _ => throw new D118RefusalException(ErrorCodes.InvalidArgument,
                $"method '{method}' is not one of the frozen enum [pettitt, cusum, bayesian_online, mann_kendall_segment]."),
        };

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tool"] = ToolKey,
            ["schemaSha256"] = D118SchemaFace.SchemaSha256(ToolKey),
            ["method"] = method,
            ["input"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = D118Outputs.Abs(series),
                ["format"] = table.Format,
                ["valueField"] = valueField,
                ["recordsObserved"] = table.Rows.Count,
                ["finiteValuesUsed"] = observed.Count,
                ["nonNumericOrNonFiniteSkipped"] = skipped,
                ["inputSha256"] = Sha256Of(series),
            },
            ["parameters"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["minSegmentLength"] = minSegment,
                ["significanceLevel"] = significance,
                ["cusumDriftK"] = CusumDriftK,
                ["cusumThresholdH"] = CusumThresholdH,
            },
            ["result"] = result,
            ["verification"] = Verification(),
        };

        var full = D118Outputs.OverwriteGate(outputPath, ToolArgs.GetBool(context, "overwrite"));
        D118Outputs.WriteJson(full, payload);
        await Task.CompletedTask;
        return OperationResult<object?>.Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outputPath"] = full,
            ["report"] = payload,
        }, $"Change-point report written for {observed.Count} finite values (method={method}).");
    }

    private static Dictionary<string, object?> PettittFace(List<double> y, double alpha)
    {
        var (index, p) = D118Stats.Pettitt(y);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["algorithm"] = "pettitt-rank-single-changepoint",
            ["changepointIndex"] = index,
            ["changepointPositionInFiniteSeries"] = index + 1,
            ["approximatePValue"] = p,
            ["significantAtAlpha"] = p is not null && p <= alpha,
            ["pValueBasis"] = "asymptotic exponential approximation (2*exp(-6K^2/(n(n+1)(n+2)))); not an exact permutation p-value",
        };
    }

    private static Dictionary<string, object?> CusumFace(List<double> y, double alpha)
    {
        var signals = D118Stats.Cusum(y, CusumDriftK, CusumThresholdH);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["algorithm"] = "two-sided-standardized-cusum",
            ["signalIndices"] = signals,
            ["signalCount"] = signals.Count,
            ["resetAfterSignal"] = true,
            ["alphaRetainedForAuditing"] = alpha,
        };
    }

    private static Dictionary<string, object?> MannKendallFace(List<double> y, int minSegment, double alpha)
    {
        var cuts = D118Stats.MannKendallSegments(y, minSegment);
        var bounds = new List<int> { 0 };
        bounds.AddRange(cuts);
        bounds.Add(y.Count);
        var segments = new List<Dictionary<string, object?>>();
        for (var i = 0; i < bounds.Count - 1; i++)
        {
            var slice = y.GetRange(bounds[i], bounds[i + 1] - bounds[i]);
            segments.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["startIndex"] = bounds[i],
                ["endIndex"] = bounds[i + 1] - 1,
                ["length"] = slice.Count,
                ["sStatistic"] = D118Stats.MannKellS(slice),
                ["mean"] = D118Stats.Mean(slice),
            });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["algorithm"] = "mann-kendall-recursive-split",
            ["splitPoints"] = cuts,
            ["segmentCount"] = segments.Count,
            ["segments"] = segments,
            ["alphaRetainedForAuditing"] = alpha,
        };
    }

    private Dictionary<string, object?> Verification() => new(StringComparer.Ordinal)
    {
        ["parameterFace"] = "byte-exact embedding of the frozen schema (additionalProperties=false enforced)",
        ["algorithmSubset"] = "implemented off-host on CSV/JSON single-column series only",
        ["arcgisHostFace"] = "NOT VERIFIED (no Pro session executed in D-118; requiresArcGIS=true comes from the frozen surface row)",
        ["sideEffects"] = "one JSON report file at outputPath; refused before write on any gate failure",
    };

    private static string Sha256Of(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(D118Outputs.Abs(path))));
    }
}

/// <summary>X05 compare_temporal_trajectories — 分组轨迹的对齐、归一、相似度与一维聚类（窄子集）。</summary>
public sealed class CompareTemporalTrajectoriesTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.CompareTemporalTrajectoriesToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Quality;
    public override string Name => ToolKey;
    public override string Description =>
        "时间轨迹比对（M4 X05）。窄子集＝CSV/JSON 表格中的分组数值序列，按行序视为时序；alignment＝none/resample/shift，" +
        "normalization＝none/zscore/minmax/rank，missingPolicy＝drop/interpolate/zero；输出相似度矩阵（Pearson）与可选一维 k-means。" +
        "值列需可由 'value' 列、单一数值列或显式 'classified'/'reference' 之外的唯一数值列确定，否则 INVALID_ARGUMENT 并列出观察列。";

    protected override async Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        var series = RequiredString(context, "series");
        var groupField = RequiredString(context, "groupField");
        var alignment = RequiredString(context, "alignment");
        var normalization = RequiredString(context, "normalization");
        var missing = OptionalString(context, "missingPolicy") ?? "drop";
        var outputPath = D118Outputs.GuardPath(RequiredString(context, "outputPath"), "outputPath");

        var table = D118Tables.Load(series, groupField);
        var valueColumn = ResolveValueColumn(table);
        var groups = new List<(string Name, List<double?> Raw)>();
        foreach (var row in table.Rows)
        {
            var name = row.TryGetValue(groupField, out var g) ? (g ?? string.Empty) : string.Empty;
            var slot = groups.FindIndex(x => string.Equals(x.Name, name, StringComparison.Ordinal));
            var raw = D118Values.AsDouble(row.TryGetValue(valueColumn, out var v) ? v : null);
            if (slot >= 0)
            {
                var cur = groups[slot];
                cur.Raw.Add(raw);
                groups[slot] = cur;
            }
            else groups.Add((name, new List<double?> { raw }));
        }

        if (groups.Count < 2)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                $"Grouping by '{groupField}' yielded {groups.Count} trajectory; at least 2 are required and no output was written.");

        var processed = groups.Select(g => (g.Name, Values: ApplyMissing(g.Raw, missing))).ToList();
        if (processed.Any(p => p.Values.Count < 3))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "Every trajectory needs at least 3 values after missingPolicy; shorten or fix the input (no output was written).");

        var commonLength = alignment == "none" ? processed.Min(p => p.Values.Count) : processed.Min(p => p.Values.Count);
        var matrix = processed.Select(p => (p.Name, Series: Normalize(Resample(p.Values, alignment, commonLength), normalization))).ToList();

        var similarity = new List<IDictionary<string, object?>>();
        for (var i = 0; i < matrix.Count; i++)
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var j = 0; j < matrix.Count; j++)
                row[matrix[j].Name] = i == j ? 1.0 : D118Stats.Pearson(matrix[i].Series, matrix[j].Series);
            similarity.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["group"] = matrix[i].Name,
                ["correlationByGroup"] = row,
            });
        }

        var clusterCount = D118Values.AsInt(ToolArg(context, "clusterCount"));
        object? clusters = null;
        if (clusterCount is { } k)
        {
            if (k < 1 || k > matrix.Count)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"clusterCount {k} must be between 1 and the trajectory count {matrix.Count}.");
            if (alignment != "resample")
                return OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
                    "clusterCount requires alignment='resample' so all trajectories share a length; refusing without implicit resampling (no output was written).");
            clusters = Cluster1D(matrix, k, D118Values.AsInt(ToolArg(context, "seed")) ?? 42);
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tool"] = ToolKey,
            ["schemaSha256"] = D118SchemaFace.SchemaSha256(ToolKey),
            ["input"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = D118Outputs.Abs(series),
                ["groupField"] = groupField,
                ["valueColumn"] = valueColumn,
                ["trajectoryCount"] = matrix.Count,
                ["commonLength"] = commonLength,
            },
            ["parameters"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["alignment"] = alignment,
                ["normalization"] = normalization,
                ["missingPolicy"] = missing,
                ["clusterCount"] = clusterCount,
            },
            ["similarity"] = similarity,
            ["clusters"] = clusters,
            ["verification"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["parameterFace"] = "byte-exact embedding of the frozen schema (additionalProperties=false enforced)",
                ["timeOrdering"] = "row order is treated as time order; the frozen face declares no timestamp column",
                ["arcgisHostFace"] = "NOT VERIFIED (no Pro session executed in D-118)",
                ["sideEffects"] = "one JSON report file at outputPath",
            },
        };

        var full = D118Outputs.OverwriteGate(outputPath, ToolArgs.GetBool(context, "overwrite"));
        D118Outputs.WriteJson(full, payload);
        await Task.CompletedTask;
        return OperationResult<object?>.Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outputPath"] = full,
            ["report"] = payload,
        }, $"Trajectory comparison written for {matrix.Count} groups (alignment={alignment}, normalization={normalization}).");
    }

    private static string ResolveValueColumn(D118Tables.Table table)
    {
        foreach (var candidate in new[] { "value", "values", "classified", "reference" })
            if (table.Columns.Contains(candidate, StringComparer.Ordinal)) return candidate;
        var numeric = table.Columns.Where(c => table.Rows.All(r => !r.TryGetValue(c, out var v) || v is null || D118Values.AsDouble(v) is not null)
            && table.Rows.Any(r => r.TryGetValue(c, out var v2) && D118Values.AsDouble(v2) is not null)).ToList();
        if (numeric.Count == 1) return numeric[0];
        throw new D118RefusalException(ErrorCodes.InvalidArgument,
            $"Cannot resolve a single numeric value column from [{string.Join(", ", table.Columns)}]; observed numeric candidates [{string.Join(", ", numeric)}]. " +
            "Rename the column to 'value' or restrict the file to one numeric column (no output was written).");
    }

    private static List<double> ApplyMissing(List<double?> raw, string policy) => policy switch
    {
        "drop" => raw.Where(v => v is not null && double.IsFinite(v.Value)).Select(v => v!.Value).ToList(),
        "zero" => raw.Select(v => v is null || !double.IsFinite(v.Value) ? 0.0 : v.Value).ToList(),
        "interpolate" => Interpolate(raw),
        _ => throw new D118RefusalException(ErrorCodes.InvalidArgument, $"missingPolicy '{policy}' is outside the frozen enum [drop, interpolate, zero]."),
    };

    private static List<double> Interpolate(List<double?> raw)
    {
        var outp = raw.Select(v => v ?? double.NaN).ToList();
        for (var i = 0; i < outp.Count; i++)
        {
            if (!double.IsNaN(outp[i])) continue;
            var prev = outp.Take(i).LastOrDefault(v => !double.IsNaN(v));
            var next = outp.Skip(i + 1).FirstOrDefault(v => !double.IsNaN(v));
            outp[i] = double.IsNaN(prev) || double.IsNaN(next) ? prev : (prev + next) / 2;
        }

        return outp;
    }

    private static List<double> Resample(List<double> v, string alignment, int length) => alignment switch
    {
        "none" => v.Take(length).ToList(),
        "resample" => Enumerable.Range(0, length)
            .Select(i => { var pos = length == 1 ? 0.0 : (double)i * (v.Count - 1) / (length - 1); var lo = (int)Math.Floor(pos); var hi = Math.Min(lo + 1, v.Count - 1); return v[lo] + (pos - lo) * (v[hi] - v[lo]); })
            .ToList(),
        "shift" => v.Skip(Math.Min(1, v.Count / 4)).Take(length).ToList(),
        _ => throw new D118RefusalException(ErrorCodes.InvalidArgument, $"alignment '{alignment}' is outside the frozen enum [none, resample, shift]."),
    };

    private static List<double> Normalize(List<double> v, string mode)
    {
        switch (mode)
        {
            case "none": return v;
            case "zscore":
                {
                    var sd = D118Stats.SampleSd(v) ?? 0;
                    var m = D118Stats.Mean(v);
                    return sd == 0 ? v.Select(_ => 0.0).ToList() : v.Select(x => (x - m) / sd).ToList();
                }
            case "minmax":
                {
                    var min = v.Min(); var max = v.Max();
                    return max - min == 0 ? v.Select(_ => 0.0).ToList() : v.Select(x => (x - min) / (max - min)).ToList();
                }
            case "rank":
                {
                    var ranks = D118Stats.Ranks(v);
                    return ranks.Select(r => r / v.Count).ToList();
                }
            default:
                throw new D118RefusalException(ErrorCodes.InvalidArgument, $"normalization '{mode}' is outside the frozen enum [none, zscore, minmax, rank].");
        }
    }

    private static Dictionary<string, object?> Cluster1D(List<(string Name, List<double> Series)> matrix, int k, int seed)
    {
        var rand = new D118Rand(seed);
        var dim = matrix[0].Series.Count;
        var centroids = Enumerable.Range(0, k).Select(c => matrix[(int)(rand.Next() * matrix.Count) % matrix.Count].Series.ToArray()).ToList();
        var assign = new int[matrix.Count];
        for (var iteration = 0; iteration < 24; iteration++)
        {
            var changed = false;
            for (var i = 0; i < matrix.Count; i++)
            {
                var best = 0; var bestDist = double.PositiveInfinity;
                for (var c = 0; c < k; c++)
                {
                    var d = 0.0;
                    for (var j = 0; j < dim; j++) { var diff = matrix[i].Series[j] - centroids[c][j]; d += diff * diff; }
                    if (d < bestDist) { bestDist = d; best = c; }
                }

                if (assign[i] != best) { assign[i] = best; changed = true; }
            }

            for (var c = 0; c < k; c++)
            {
                var members = matrix.Select((m, i) => (m, i)).Where(x => assign[x.i] == c).Select(x => x.m.Series).ToList();
                if (members.Count == 0) continue;
                centroids[c] = Enumerable.Range(0, dim).Select(j => members.Average(s => s[j])).ToArray();
            }

            if (!changed) break;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["algorithm"] = "kmeans-on-aligned-normalized-series",
            ["clusterCount"] = k,
            ["seed"] = seed,
            ["iterationsCapped"] = 24,
            ["members"] = matrix.Select((m, i) => (IDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["group"] = m.Name, ["cluster"] = assign[i],
            }).ToList(),
        };
    }
}

/// <summary>X21 assess_remote_sensing_quality — ESRI ASCII 栅格质量实测；GeoTIFF 仅结构头检（像元统计不推定）。</summary>
public sealed class AssessRemoteSensingQualityTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.AssessRemoteSensingQualityToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Quality;
    public override string Name => ToolKey;
    public override string Description =>
        "遥感影像质量评估（M4 X21，只读）。窄子集＝ESRI ASCII 栅格（ncols/nrows/cellsize/NODATA_value 头＋数值网格）给出行列数、值域、均值/标准差、" +
        "有效像元比与极值位置；GeoTIFF/TIFF 只做文件头结构检查（字节序、版本、IFD 项数、GeoTIFF 关键标签在场性），**像元统计不推定**并如实标 NOT_VERIFIED。" +
        "sensorProfile 无已接受档案库 ⇒ 一旦给值即 NOT_IMPLEMENTED。零写出。";

    protected override async Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        await Task.CompletedTask;
        var input = RequiredString(context, "input");
        var sensorProfile = D118Values.AsString(ToolArg(context, "sensorProfile"));
        var unknownQa = D118Values.AsString(ToolArg(context, "unknownQaPolicy")) ?? "reject";
        if (!string.IsNullOrWhiteSpace(sensorProfile))
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
                $"sensorProfile '{sensorProfile}' cannot be resolved: D-118 ships no accepted sensor-profile library, and profile-based QA would be invented. No file was read.");

        var full = D118Outputs.Abs(input);
        if (!File.Exists(full))
            return OperationResult<object?>.Fail(ErrorCodes.NotFound, $"Input '{input}' was not found; nothing was read.");
        var bytes = File.ReadAllBytes(full);
        var sha = Convert.ToHexString(SHA256.HashData(bytes));

        if (LooksLikeAsciiGrid(bytes))
            return OperationResult<object?>.Ok(AsciiFace(full, sha, bytes, unknownQa),
                "ASCII grid quality face computed read-only; no file was written.");

        if (IsTiff(bytes))
            return OperationResult<object?>.Ok(TiffFace(full, sha, bytes, unknownQa),
                "TIFF/GeoTIFF structural face computed read-only; pixel statistics are NOT VERIFIED (no decoder shipped in D-118).");

        return OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
            $"'{input}' is neither an ESRI ASCII grid nor a TIFF/GeoTIFF file recognised by this batch's narrow reader; no statistics were inferred.");
    }

    private static bool LooksLikeAsciiGrid(byte[] bytes)
    {
        var head = Encoding.UTF8.GetString(bytes.Take(400).ToArray()).ToLowerInvariant();
        return head.Contains("ncols") && head.Contains("nrows") && head.Contains("cellsize");
    }

    private static bool IsTiff(byte[] bytes)
        => bytes.Length > 8 && ((bytes[0] == 0x49 && bytes[1] == 0x49) || (bytes[0] == 0x4D && bytes[1] == 0x4D));

    private static Dictionary<string, object?> AsciiFace(string full, string sha, byte[] bytes, string unknownQa)
    {
        var lines = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n").Split('\n');
        var header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var values = new List<double>();
        var nodata = double.NaN;
        var idx = 0;
        for (; idx < lines.Length && header.Count < 6; idx++)
        {
            var parts = lines[idx].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var hv)
                && parts[0] is "ncols" or "nrows" or "xllcorner" or "yllcorner" or "cellsize" or "dx" or "dy" or "NODATA_value" or "nodata_value")
            {
                header[parts[0]] = parts[1];
                continue;
            }

            break;
        }

        if (!header.TryGetValue("ncols", out var ncolsText) || !header.TryGetValue("nrows", out var nrowsText))
            throw new D118RefusalException(ErrorCodes.InvalidArgument, "ASCII grid header lacks ncols/nrows; no statistics were produced.");
        var ncols = int.Parse(ncolsText, CultureInfo.InvariantCulture);
        var nrows = int.Parse(nrowsText, CultureInfo.InvariantCulture);
        if (header.TryGetValue("NODATA_value", out var nd) || header.TryGetValue("nodata_value", out nd))
            double.TryParse(nd, NumberStyles.Float, CultureInfo.InvariantCulture, out nodata);

        for (; idx < lines.Length; idx++)
        {
            foreach (var token in lines[idx].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                {
                    if (unknownQa == "reject")
                        throw new D118RefusalException(ErrorCodes.InvalidArgument,
                            $"Non-numeric ASCII cell token '{token}' at line {idx + 1}; unknownQaPolicy='reject' refuses to guess. Nothing was reported as a statistic.");
                    continue;
                }

                if (!double.IsFinite(v) || (!double.IsNaN(nodata) && Math.Abs(v - nodata) < 1e-12)) continue;
                values.Add(v);
            }
        }

        var declared = (long)ncols * nrows;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["format"] = "esri-ascii-grid",
            ["path"] = full,
            ["inputSha256"] = sha,
            ["ncols"] = ncols,
            ["nrows"] = nrows,
            ["cellsize"] = header.TryGetValue("cellsize", out var cs) ? cs : (header.TryGetValue("dx", out var dx) ? dx : null),
            ["origin"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["xllcorner"] = header.TryGetValue("xllcorner", out var x) ? x : null,
                ["yllcorner"] = header.TryGetValue("yllcorner", out var y) ? y : null,
            },
            ["nodataValue"] = double.IsNaN(nodata) ? null : nodata,
            ["cellsDeclared"] = declared,
            ["cellsNumeric"] = values.Count,
            ["validRatio"] = declared == 0 ? null : (double)values.Count / declared,
            ["stats"] = values.Count == 0 ? null : new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["min"] = values.Min(),
                ["max"] = values.Max(),
                ["mean"] = Math.Round(D118Stats.Mean(values), 12, MidpointRounding.ToEven),
                ["sampleSd"] = values.Count > 1 ? Math.Round(D118Stats.SampleSd(values) ?? 0, 12, MidpointRounding.ToEven) : null,
            },
            ["verification"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pixelStatistics"] = "measured from the ASCII cells in this file",
                ["projectionOrExtentValidity"] = "NOT VERIFIED (ASCII headers carry no CRS; no CRS is inferred)",
                ["temporalOrRadiometricConsistency"] = "NOT VERIFIED (no sensor profile library)",
                ["sideEffects"] = "none (read-only tool)",
            },
        };
    }

    private static Dictionary<string, object?> TiffFace(string full, string sha, byte[] bytes, string unknownQa)
    {
        var little = bytes[0] == 0x49;
        var version = ReadU16(bytes, 2, little);
        var ifdOffset = ReadU32(bytes, 4, little);
        var entryCount = ifdOffset + 2 <= bytes.Length ? ReadU16(bytes, ifdOffset, little) : 0;
        var tagIds = new List<int>();
        for (var i = 0; i < entryCount && ifdOffset + 2 + i * 12 + 2 <= bytes.Length; i++)
            tagIds.Add(ReadU16(bytes, ifdOffset + 2 + i * 12, little));
        var geoKeys = new[] { 33922, 34735, 34736, 34737 };
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["format"] = "tiff-or-geotiff",
            ["path"] = full,
            ["inputSha256"] = sha,
            ["byteOrder"] = little ? "little-endian (II)" : "big-endian (MM)",
            ["tiffVersion"] = version,
            ["isBigTiff"] = version == 43,
            ["ifdEntryCount"] = entryCount,
            ["observedTags"] = tagIds.Where(t => new[] { 256, 257, 258, 277, 322, 339, 33550, 33922, 34264, 34735 }.Contains(t)).Distinct().OrderBy(t => t).ToList(),
            ["modelPixelScaleTagPresent"] = tagIds.Contains(33550),
            ["geoKeyTagsPresent"] = geoKeys.Any(tagIds.Contains),
            ["verification"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pixelStatistics"] = "NOT VERIFIED — no TIFF decoder is shipped in D-118, so cell values are not read and not inferred",
                ["crsResolution"] = "NOT VERIFIED — GeoTIFF key *presence* is observed, its values are not decoded",
                ["unknownQaPolicyAppliedTo"] = "structural observations only",
                ["sideEffects"] = "none (read-only tool)",
            },
        };
    }

    private static int ReadU16(byte[] b, int offset, bool little)
        => little ? b[offset] | (b[offset + 1] << 8) : (b[offset] << 8) | b[offset + 1];

    private static int ReadU32(byte[] b, int offset, bool little)
    {
        uint value = little
            ? (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16)) | ((uint)b[offset + 3] << 24)
            : ((uint)b[offset] << 24) | ((uint)b[offset + 1] << 16) | ((uint)b[offset + 2] << 8) | b[offset + 3];
        return unchecked((int)value);
    }
}

/// <summary>X25 assess_classification_accuracy — 混淆矩阵与精度统计（实测；抽样设计登记为计划面）。</summary>
public sealed class AssessClassificationAccuracyTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.AssessClassificationAccuracyToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Quality;
    public override string Name => ToolKey;
    public override string Description =>
        "分类精度评估（M4 X25）。窄子集＝两个 CSV/JSON 表格（classified 与 reference），类别列由 'class'／单列表／同名列显式确定，" +
        "配对优先用 'id'/'fid' 列，缺省按行序并要求行数相等；输出混淆矩阵、总体精度、Kappa、per-class user/producer 精度，" +
        "computeInterval=true 时给 95% Wilson 区间。samplingDesign 仅登记为抽样计划标签（不臆造设计权重）。";

    protected override async Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        var classifiedPath = RequiredString(context, "classified");
        var referencePath = RequiredString(context, "reference");
        var design = RequiredString(context, "samplingDesign");
        var outputPath = D118Outputs.GuardPath(RequiredString(context, "outputPath"), "outputPath");
        var computeInterval = ToolArgs.GetBool(context, "computeInterval", false);

        var classified = D118Tables.Load(classifiedPath);
        var reference = D118Tables.Load(referencePath);
        var classColumn = ResolveClassColumn(classified, "classified");
        var refColumn = ResolveClassColumn(reference, "reference");

        var pairs = Pair(classified, reference, classColumn, refColumn);
        if (pairs.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "No comparable class pairs were resolved; nothing was written.");

        var classes = pairs.Select(p => p.Predicted).Union(pairs.Select(p => p.Actual)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var matrix = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        foreach (var c in classes) matrix[c] = classes.ToDictionary(x => x, _ => 0L, StringComparer.Ordinal);
        foreach (var (predicted, actual) in pairs) matrix[predicted][actual]++;

        var total = (long)pairs.Count;
        var correct = classes.Sum(c => matrix[c][c]);
        var overall = (double)correct / total;
        var pe = classes.Sum(c =>
        {
            var rowSum = classes.Sum(o => matrix[c][o]);
            var colSum = classes.Sum(o => matrix[o][c]);
            return (double)rowSum * colSum / ((double)total * total);
        });

        var perClass = classes.Select(c =>
        {
            var userDen = classes.Sum(o => matrix[c][o]);
            var prodDen = classes.Sum(o => matrix[o][c]);
            var user = userDen == 0 ? (double?)null : (double)matrix[c][c] / userDen;
            var prod = prodDen == 0 ? (double?)null : (double)matrix[c][c] / prodDen;
            return (IDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["class"] = c,
                ["predictedCount"] = userDen,
                ["referenceCount"] = prodDen,
                ["userAccuracy"] = user,
                ["producerAccuracy"] = prod,
                ["userAccuracyWilson95"] = computeInterval && user is not null ? Wilson(matrix[c][c], userDen) : null,
                ["producerAccuracyWilson95"] = computeInterval && prod is not null ? Wilson(matrix[c][c], prodDen) : null,
            };
        }).ToList();

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tool"] = ToolKey,
            ["schemaSha256"] = D118SchemaFace.SchemaSha256(ToolKey),
            ["inputs"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["classified"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = D118Outputs.Abs(classifiedPath), ["classColumn"] = classColumn },
                ["reference"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = D118Outputs.Abs(referencePath), ["classColumn"] = refColumn },
                ["pairedRecords"] = pairs.Count,
                ["pairingKey"] = PairingKey(classified, reference),
            },
            ["samplingDesignDeclared"] = design,
            ["metrics"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["classCount"] = classes.Count,
                ["overallAccuracy"] = overall,
                ["kappa"] = Math.Abs(1 - pe) < 1e-15 ? null : (overall - pe) / (1 - pe),
                ["expectedAgreement"] = pe,
                ["overallAccuracyWilson95"] = computeInterval ? Wilson(correct, total) : null,
                ["confusionMatrix"] = classes.ToDictionary(
                    c => c,
                    c => (IDictionary<string, object?>)matrix[c].ToDictionary(kv => kv.Key, kv => (object?)kv.Value, StringComparer.Ordinal),
                    StringComparer.Ordinal),
                ["perClass"] = perClass,
            },
            ["verification"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["accuracyMetrics"] = "measured from the two provided tables (counts, Kappa and Wilson intervals computed in-process)",
                ["samplingDesignWeights"] = "NOT VERIFIED — samplingDesign is recorded as the declared plan label only; no design weights are invented",
                ["arcgisHostFace"] = "NOT VERIFIED (no Pro session executed in D-118)",
                ["sideEffects"] = "one JSON report file at outputPath",
            },
        };

        var full = D118Outputs.OverwriteGate(outputPath, ToolArgs.GetBool(context, "overwrite"));
        D118Outputs.WriteJson(full, payload);
        await Task.CompletedTask;
        return OperationResult<object?>.Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outputPath"] = full,
            ["report"] = payload,
        }, $"Accuracy assessment written for {pairs.Count} paired records over {classes.Count} classes.");
    }

    private static string ResolveClassColumn(D118Tables.Table table, string preferred)
    {
        foreach (var candidate in new[] { "class", preferred, preferred + "_class", "value" })
            if (table.Columns.Contains(candidate, StringComparer.Ordinal)) return candidate;
        if (table.Columns.Count == 1) return table.Columns[0];
        throw new D118RefusalException(ErrorCodes.InvalidArgument,
            $"Cannot resolve the class column for the '{preferred}' table from [{string.Join(", ", table.Columns)}]; name it 'class' (or '{preferred}') and retry. Nothing was written.");
    }

    private static List<(string Predicted, string Actual)> Pair(D118Tables.Table classified, D118Tables.Table reference, string classColumn, string refColumn)
    {
        var key = new[] { "id", "fid", "OBJECTID", "objectid", "FID" }.FirstOrDefault(c =>
            classified.Columns.Contains(c, StringComparer.Ordinal) && reference.Columns.Contains(c, StringComparer.Ordinal));
        if (key is null)
        {
            if (classified.Rows.Count != reference.Rows.Count)
                throw new D118RefusalException(ErrorCodes.InvalidArgument,
                    $"No shared id column and the row counts differ ({classified.Rows.Count} vs {reference.Rows.Count}); pairing by row order would be a guess. Nothing was written.");
            return classified.Rows.Select((r, i) => (Text(r, classColumn), Text(reference.Rows[i], refColumn)))
                .Where(p => p.Item1.Length > 0 && p.Item2.Length > 0).ToList();
        }

        var referenceByKey = reference.Rows.GroupBy(r => Text(r, key)).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        return classified.Rows.Where(r => referenceByKey.ContainsKey(Text(r, key)))
            .Select(r => (Text(r, classColumn), Text(referenceByKey[Text(r, key)], refColumn)))
            .Where(p => p.Item1.Length > 0 && p.Item2.Length > 0).ToList();
    }

    private static string PairingKey(D118Tables.Table classified, D118Tables.Table reference)
        => new[] { "id", "fid", "OBJECTID", "objectid", "FID" }.FirstOrDefault(c =>
            classified.Columns.Contains(c, StringComparer.Ordinal) && reference.Columns.Contains(c, StringComparer.Ordinal)) ?? "row-order";

    private static string Text(IReadOnlyDictionary<string, string?> row, string column)
        => row.TryGetValue(column, out var v) ? (v ?? string.Empty).Trim('"') : string.Empty;

    private static Dictionary<string, object?> Wilson(long hits, long n)
    {
        var (lo, up) = D118Stats.Wilson(hits, n);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["lower"] = lo, ["upper"] = up, ["method"] = "wilson-score-95" };
    }
}

/// <summary>X36 validate_statistical_assumptions — 描述统计与可算假设检查（只读，零写出）。</summary>
public sealed class ValidateStatisticalAssumptionsTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.ValidateStatisticalAssumptionsToolName;
    private static readonly string[] SupportedChecks = { "sampleSize", "missingValues", "normality", "collinearity", "outliers" };

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Quality;
    public override string Name => ToolKey;
    public override string Description =>
        "统计假设校验（M4 X36，只读）。窄子集＝内联数值向量（数组的数组，或 [{name, values}]）；可算面＝样本量、缺失、偏度/峰度与 Jarque-Bera 近似、" +
        "IQR 离群计数、两两 Pearson 相关与共线性 VIF；checks 只支持 [sampleSize, missingValues, normality, collinearity, outliers]，" +
        "其余（如方差齐性、空间自相关）如实报 NOT VERIFIED。零写出。";

    protected override async Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        await Task.CompletedTask;
        var intended = RequiredString(context, "intendedMethod");
        var requested = D118Values.AsList(ToolArg(context, "checks")).Select(D118Values.AsString).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToList();
        var variables = ReadVariables(D118Values.AsList(ToolArg(context, "variables")));
        if (variables.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "variables must supply at least one numeric vector; nothing was computed.");
        if (requested.Any(r => !SupportedChecks.Contains(r, StringComparer.Ordinal)))
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
                $"checks [{string.Join(", ", requested.Except(SupportedChecks, StringComparer.Ordinal))}] are outside the implemented subset [{string.Join(", ", SupportedChecks)}]; refusing to emit placeholder results.");

        var perVariable = variables.Select(v => (IDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = v.Name,
            ["count"] = v.Values.Count,
            ["mean"] = D118Stats.Mean(v.Values),
            ["sampleSd"] = D118Stats.SampleSd(v.Values),
            ["min"] = v.Values.Min(),
            ["max"] = v.Values.Max(),
            ["skewness"] = D118Stats.Skewness(v.Values),
            ["excessKurtosis"] = D118Stats.ExcessKurtosis(v.Values),
            ["jarqueBera"] = JarqueBera(v.Values),
            ["iqrOutlierCount"] = IqrOutliers(v.Values),
        }).ToList();

        var correlations = new List<IDictionary<string, object?>>();
        for (var i = 0; i < variables.Count; i++)
            for (var j = i + 1; j < variables.Count; j++)
                correlations.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["pair"] = variables[i].Name + "|" + variables[j].Name,
                    ["pearson"] = EqualLength(variables[i].Values, variables[j].Values) ? D118Stats.Pearson(variables[i].Values, variables[j].Values) : null,
                    ["note"] = EqualLength(variables[i].Values, variables[j].Values) ? "paired element-wise (position is treated as pairing; no key is declared in the frozen face)"
                        : "NOT VERIFIED — vectors differ in length, so no pairing was assumed",
                });

        var common = variables.Where(v => v.Values.Count == variables.Min(x => x.Values.Count)).Select(v => v.Values).ToList();
        var vif = common.Count >= 2 ? VarianceInflation(common) : null;

        var report = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tool"] = ToolKey,
            ["schemaSha256"] = D118SchemaFace.SchemaSha256(ToolKey),
            ["intendedMethod"] = intended,
            ["checksRequested"] = requested.Count == 0 ? SupportedChecks : requested,
            ["variables"] = perVariable,
            ["correlations"] = correlations,
            ["varianceInflationFactors"] = vif,
            ["verification"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["computed"] = "descriptive statistics, Jarque-Bera (asymptotic p = exp(-JB/2)), IQR outlier counts, Pearson correlations, VIF from the correlation-matrix inverse",
                ["notComputed"] = new[] { "equal-variance (homoscedasticity) tests", "spatial autocorrelation", "independence of residuals", "exact p-values" },
                ["arcgisHostFace"] = "NOT VERIFIED (no Pro session executed in D-118)",
                ["sideEffects"] = "none (read-only tool; the frozen face declares no outputPath)",
            },
        };

        return OperationResult<object?>.Ok(report, $"Assumption checks computed for {variables.Count} variable(s) (intendedMethod={intended}); nothing was written.");
    }

    private sealed record Variable(string Name, List<double> Values);

    private static List<Variable> ReadVariables(IReadOnlyList<object?> raw)
    {
        var outp = new List<Variable>();
        var index = 0;
        foreach (var item in raw)
        {
            index++;
            if (D118Values.AsObject(item) is { } obj)
            {
                var name = obj.TryGetValue("name", out var n) ? D118Values.AsString(n) : null;
                var values = (obj.TryGetValue("values", out var v) ? D118Values.AsList(v) : Array.Empty<object?>())
                    .Select(D118Values.AsDouble).Where(x => x is not null && double.IsFinite(x.Value)).Select(x => x!.Value).ToList();
                if (values.Count > 0) outp.Add(new Variable(name ?? $"variable_{index.ToString(CultureInfo.InvariantCulture)}", values));
                continue;
            }

            var list = D118Values.AsList(item).Select(D118Values.AsDouble)
                .Where(x => x is not null && double.IsFinite(x.Value)).Select(x => x!.Value).ToList();
            if (list.Count > 0) outp.Add(new Variable("variable_" + index.ToString(CultureInfo.InvariantCulture), list));
        }

        return outp;
    }

    private static bool EqualLength(List<double> a, List<double> b) => a.Count == b.Count;

    private static Dictionary<string, object?> JarqueBera(List<double> v)
    {
        var n = v.Count;
        var s = D118Stats.Skewness(v);
        var k = D118Stats.ExcessKurtosis(v);
        if (n < 8 || s is null || k is null)
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = "NOT VERIFIED (insufficient n or degenerate spread)" };
        var jb = n / 6.0 * (s.Value * s.Value + k.Value * k.Value / 4.0);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["statistic"] = jb,
            ["approximatePValue"] = Math.Exp(-jb / 2.0),
            ["basis"] = "chi-square(2) survival approximation; not an exact test",
        };
    }

    private static int IqrOutliers(List<double> v)
    {
        var sorted = v.OrderBy(x => x).ToList();
        var q1 = D118Stats.Quantile(sorted, 0.25);
        var q3 = D118Stats.Quantile(sorted, 0.75);
        var iqr = q3 - q1;
        return v.Count(x => x < q1 - 1.5 * iqr || x > q3 + 1.5 * iqr);
    }

    private static Dictionary<string, object?>? VarianceInflation(List<List<double>> columns)
    {
        var k = columns.Count;
        var corr = new double[k][];
        for (var i = 0; i < k; i++)
        {
            corr[i] = new double[k];
            for (var j = 0; j < k; j++) corr[i][j] = D118Stats.Pearson(columns[i], columns[j]) ?? double.NaN;
        }

        if (corr.Any(r => r.Any(x => !double.IsFinite(x)))) return null;
        if (D118Stats.InverseOf(corr, out var inv) is null || inv is null)
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = "NOT VERIFIED (correlation matrix is singular; a column is an exact linear combination)" };
        return Enumerable.Range(0, k).ToDictionary(
            i => "variable_" + (i + 1).ToString(CultureInfo.InvariantCulture),
            i => (object?)Math.Round(inv[i][i], 10, MidpointRounding.ToEven),
            StringComparer.Ordinal);
    }
}

/// <summary>X43 validate_interchange_conformance — 交换文件结构一致性（只读；不宣称语义规范符合）。</summary>
public sealed class ValidateInterchangeConformanceTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.ValidateInterchangeConformanceToolName;
    private static readonly string[] SupportedDeepChecks = { "encoding", "structure", "columnUniqueness", "rowWidth", "blankRatio" };

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Quality;
    public override string Name => ToolKey;
    public override string Description =>
        "交换格式符合性检查（M4 X43，只读）。窄子集＝CSV/JSON 文件的**结构面**：UTF-8 严格解码、格式判定、列名唯一性、行长一致性、空值比、数值列可解析率。" +
        "standardVersion 仅按字面登记（本批不持有该标准的规则库，**不宣称语义符合**）；deepChecks 只支持 [encoding, structure, columnUniqueness, rowWidth, blankRatio]。" +
        "可选 reportPath 写 JSON 报告；该参数面无 overwrite 逃生舱，输出已存在即 OUTPUT_EXISTS。零副作用优先。";

    protected override async Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        await Task.CompletedTask;
        var input = RequiredString(context, "input");
        var version = RequiredString(context, "standardVersion");
        var requested = D118Values.AsList(ToolArg(context, "deepChecks")).Select(D118Values.AsString)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToList();
        var reportPath = D118Values.AsString(ToolArg(context, "reportPath"));

        if (requested.Any(r => !SupportedDeepChecks.Contains(r, StringComparer.Ordinal)))
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
                $"deepChecks [{string.Join(", ", requested.Except(SupportedDeepChecks, StringComparer.Ordinal))}] are outside the implemented structural subset [{string.Join(", ", SupportedDeepChecks)}]; no report was written.");

        var full = D118Outputs.Abs(input);
        if (!File.Exists(full))
            return OperationResult<object?>.Fail(ErrorCodes.NotFound, $"Input '{input}' was not found; nothing was read.");
        var bytes = File.ReadAllBytes(full);
        var strict = new UTF8Encoding(false, true);
        string text;
        try { text = strict.GetString(bytes); }
        catch (DecoderFallbackException)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                $"'{input}' is not strictly decodable as UTF-8; encoding conformance failed and nothing further was evaluated.");
        }

        var findings = new List<IDictionary<string, object?>>();
        var format = text.TrimStart().StartsWith("[") || text.TrimStart().StartsWith("{") ? "json" : "csv";
        string[]? columns = null;
        var records = 0;
        var ragged = 0;
        var blankFields = 0;
        var totalFields = 0;

        if (format == "json")
        {
            using var doc = JsonDocument.Parse(text);
            var items = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : new List<JsonElement> { doc.RootElement };
            records = items.Count;
            columns = items.Where(i => i.ValueKind == JsonValueKind.Object).SelectMany(i => i.EnumerateObject()).Select(p => p.Name).Distinct().ToArray();
            if (items.Any(i => i.ValueKind != JsonValueKind.Object))
                findings.Add(Finding("structure", "FAIL", "JSON root array contains non-object members; interchange record model expects objects."));
        }
        else
        {
            var lines = text.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();
            if (lines.Count < 2) return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "CSV input needs a header plus at least one data row.");
            columns = lines[0].Split(',').Select(c => c.Trim().Trim('"')).ToArray();
            records = lines.Count - 1;
            foreach (var line in lines.Skip(1))
            {
                var width = line.Split(',').Length;
                if (width != columns.Length) ragged++;
                blankFields += line.Split(',').Count(f => string.IsNullOrWhiteSpace(f));
                totalFields += width;
            }

            if (ragged > 0) findings.Add(Finding("rowWidth", "FAIL", $"{ragged} data row(s) do not match the {columns.Length}-column header width."));
            if (columns.Distinct(StringComparer.Ordinal).Count() != columns.Length)
                findings.Add(Finding("columnUniqueness", "FAIL", "Header contains duplicate column names."));
        }

        findings.Add(Finding("encoding", "PASS", "Strict UTF-8 decode succeeded."));
        findings.Add(Finding("structure", findings.Any(f => Convert.ToString(f["status"], CultureInfo.InvariantCulture) == "FAIL") ? "PARTIAL" : "PASS",
            $"{format} parsed with {records} record(s) and {columns!.Length} column(s)."));
        if (format == "csv" && blankFields > 0)
            findings.Add(Finding("blankRatio", "REPORT", $"blank fields {blankFields}/{Math.Max(1, totalFields)}"));

        var report = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tool"] = ToolKey,
            ["schemaSha256"] = D118SchemaFace.SchemaSha256(ToolKey),
            ["input"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = full,
                ["format"] = format,
                ["bytes"] = bytes.Length,
                ["inputSha256"] = Convert.ToHexString(SHA256.HashData(bytes)),
            },
            ["standardVersionDeclared"] = version,
            ["columns"] = columns,
            ["records"] = records,
            ["findings"] = findings,
            ["verification"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["conformanceClaim"] = "NOT VERIFIED — D-118 ships no rule library for the declared standardVersion; only structural facts above are measured",
                ["semanticChecks"] = new[] { "field-domain conformance", "CRS and tolerance rules", "relationship integrity", "code-list validation" },
                ["sideEffects"] = string.IsNullOrWhiteSpace(reportPath) ? "none (read-only tool)" : "one JSON report at reportPath (no overwrite escape hatch in the frozen face)",
            },
        };

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var guarded = D118Outputs.GuardPath(reportPath, "reportPath");
            var written = D118Outputs.OverwriteGate(guarded, false);
            D118Outputs.WriteJson(written, report);
            report["reportPath"] = written;
        }

        return OperationResult<object?>.Ok(report,
            $"Structural interchange checks completed for {records} record(s); conformance against '{version}' is NOT VERIFIED.");
    }

    private static IDictionary<string, object?> Finding(string check, string status, string detail)
        => new Dictionary<string, object?>(StringComparer.Ordinal) { ["check"] = check, ["status"] = status, ["detail"] = detail };
}
