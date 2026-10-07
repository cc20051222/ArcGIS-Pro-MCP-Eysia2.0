// D-118 内核：15 件 M4 Native 工具的共用参数面、校验、取值、表格读取、统计核与输出闸门。
// 硬规则：Rule 5（Shared 零 ArcGIS Pro SDK 引用——本文件只用 BCL 与 Core）、Rule 8（策略复用 Core.Results）、
// fail-closed 语义（稳定 errorCode ＋ NOT_VERIFIED 措辞 ＋ sideEffects=false）。
using System.Globalization;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// 冻结 schema 嵌入文本 → MCP InputSchema 的物化与参数面校验。
/// 参数面 ≡ D-118 工单指定的 15 份 <c>f03b-5-schemas/&lt;name&gt;.schema.json</c> 正本（嵌入即等值，无人工转写）。
/// </summary>
internal static class D118SchemaFace
{
    private static readonly Dictionary<string, IReadOnlyDictionary<string, object?>> Cache = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    public static IReadOnlyDictionary<string, object?> For(string toolName)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(toolName, out var cached)) return cached;
            var text = D118FrozenSchemas.ByToolName[toolName];
            var materialized = (IReadOnlyDictionary<string, object?>)Materialize(JsonDocument.Parse(text).RootElement)!;
            Cache[toolName] = materialized;
            return materialized;
        }
    }

    private static object? Materialize(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => Materialize(p.Value), StringComparer.Ordinal),
        JsonValueKind.Array => e.EnumerateArray().Select(Materialize).ToList(),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? (object)l : e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    /// <summary>按冻结正本逐件校验：必填、类型、枚举、additionalProperties=false 的未知键拒绝。</summary>
    public static string? Validate(string toolName, IReadOnlyDictionary<string, object?>? args)
    {
        var schema = For(toolName);
        if (schema.TryGetValue("additionalProperties", out var ap) && ap is false)
        {
            var declared = (IDictionary<string, object?>)schema["properties"]!;
            foreach (var key in (args ?? new Dictionary<string, object?>()).Keys)
            {
                if (!declared.ContainsKey(key))
                    return $"Unknown parameter '{key}' is not declared by the frozen schema for '{toolName}' (additionalProperties=false).";
            }
        }

        var props = (IDictionary<string, object?>)schema["properties"]!;
        var required = ((IEnumerable<object?>)schema["required"]!).Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)!).ToHashSet(StringComparer.Ordinal);
        foreach (var name in required)
        {
            if (!args!.TryGetValue(name, out var value) || D118Values.IsBlank(value))
                return $"Required parameter '{name}' is missing or blank for '{toolName}'.";
        }

        foreach (var (name, value) in args ?? new Dictionary<string, object?>())
        {
            if (!props.TryGetValue(name, out var spec) || D118Values.IsBlank(value)) continue;
            if (spec is not IDictionary<string, object?> declared) continue;
            var declaredType = Convert.ToString(declared["type"], CultureInfo.InvariantCulture);
            var reason = D118Values.CheckType(name, declaredType, value);
            if (reason is not null) return reason;
            if (declared.TryGetValue("enum", out var enumObj)
                && enumObj is IEnumerable<object?> allowed
                && declaredType == "string")
            {
                var text = D118Values.AsString(value);
                if (!allowed.Any(a => string.Equals(Convert.ToString(a, CultureInfo.InvariantCulture), text, StringComparison.Ordinal)))
                    return $"Parameter '{name}' value '{text}' is outside the frozen enum [{string.Join(", ", allowed)}].";
            }
        }

        return null;
    }

    public static IReadOnlyList<string> EnumOf(string toolName, string parameter)
    {
        var props = (IDictionary<string, object?>)For(toolName)["properties"]!;
        if (props[parameter] is not IDictionary<string, object?> spec) return new List<string>();
        return spec.TryGetValue("enum", out var e) && e is IEnumerable<object?> list
            ? list.Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)!).ToList()
            : new List<string>();
    }

    public static int? IntDefault(string toolName, string parameter)
    {
        var props = (IDictionary<string, object?>)For(toolName)["properties"]!;
        if (props[parameter] is not IDictionary<string, object?> spec) return null;
        return spec.TryGetValue("default", out var d) ? Convert.ToInt32(d, CultureInfo.InvariantCulture) : null;
    }

    public static string SchemaSha256(string toolName) => D118FrozenSchemas.SchemaSha256ByToolName[toolName];
}

/// <summary>取值与类型判定（同时容忍服务端 JsonElement 面与本进程物化面）。</summary>
internal static class D118Values
{
    public static bool IsBlank(object? v) => v is null
        || (v is string s && string.IsNullOrWhiteSpace(s))
        || (v is JsonElement j && j.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            or JsonValueKind.String && string.IsNullOrWhiteSpace(j.GetString()));

    public static string? AsString(object? v) => v switch
    {
        null => null,
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } je => je.GetString(),
        JsonElement je => je.GetRawText(),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture),
    };

    public static bool AsBool(object? v, bool fallback) => v switch
    {
        null => fallback,
        bool b => b,
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        string s when bool.TryParse(s, out var p) => p,
        _ => fallback,
    };

    public static double? AsDouble(object? v) => v switch
    {
        null => null,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        JsonElement { ValueKind: JsonValueKind.Number } je => je.GetDouble(),
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) => p,
        _ => null,
    };

    public static int? AsInt(object? v)
    {
        var d = AsDouble(v);
        return d is { } x && double.IsFinite(x) && Math.Abs(x - Math.Truncate(x)) < 1e-9 ? (int)Math.Truncate(x) : null;
    }

    public static IReadOnlyList<object?> AsList(object? v) => v switch
    {
        null => Array.Empty<object?>(),
        IReadOnlyList<object?> list => list,
        System.Collections.IEnumerable seq and not string => seq.Cast<object?>().ToList(),
        JsonElement { ValueKind: JsonValueKind.Array } je => je.EnumerateArray().Select(x => (object?)x).ToList(),
        _ => new List<object?> { v },
    };

    public static IReadOnlyDictionary<string, object?>? AsObject(object? v) => v switch
    {
        null => null,
        IReadOnlyDictionary<string, object?> o => o,
        IDictionary<string, object?> d => d.ToDictionary(k => k.Key, k => k.Value, StringComparer.Ordinal),
        JsonElement { ValueKind: JsonValueKind.Object } je => je.EnumerateObject()
            .ToDictionary(p => p.Name, p => (object?)p.Value, StringComparer.Ordinal),
        _ => null,
    };

    public static string? CheckType(string name, string? declared, object? value)
    {
        switch (declared)
        {
            case "string":
                return value is string or JsonElement { ValueKind: JsonValueKind.String } || value is not null ? null
                    : $"Parameter '{name}' must be a string.";
            case "integer":
                return AsInt(value) is null ? $"Parameter '{name}' must be an integer." : null;
            case "number":
                return AsDouble(value) is null ? $"Parameter '{name}' must be a number." : null;
            case "boolean":
                return value is bool or JsonElement { ValueKind: JsonValueKind.True or JsonValueKind.False } or string ? null
                    : $"Parameter '{name}' must be a boolean.";
            case "array":
                return value is System.Collections.IEnumerable or JsonElement { ValueKind: JsonValueKind.Array } ? null
                    : $"Parameter '{name}' must be an array.";
            case "object":
                return AsObject(value) is null ? $"Parameter '{name}' must be an object." : null;
            default:
                return null;
        }
    }

    public static IReadOnlyList<double?> Column(IReadOnlyList<IReadOnlyDictionary<string, string?>> rows, string field)
        => rows.Select(r => r.TryGetValue(field, out var v) ? AsDouble(v) : (double?)null).ToList();
}

/// <summary>表格输入读取：仅支持显式声明的离线交换面（CSV / JSON 数组），其余不推定。</summary>
internal static class D118Tables
{
    public sealed record Table(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows, string Format);

    public static Table Load(string path, params string[] requiredColumns)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
            throw new D118RefusalException(ErrorCodes.NotFound, $"Input '{path}' was not found; nothing was read and no output was written.");
        var bytes = File.ReadAllBytes(full);
        var text = DecodeUtf8OrThrow(path, bytes);
        var trimmed = text.TrimStart();
        var isJson = full.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("{", StringComparison.Ordinal);
        var rows = isJson ? FromJson(text) : FromCsv(text);
        if (rows.Count == 0)
            throw new D118RefusalException(ErrorCodes.InvalidArgument, $"Input '{path}' contains no records.");
        var columns = rows[0].Keys.ToList();
        foreach (var need in requiredColumns)
        {
            if (!columns.Contains(need, StringComparer.Ordinal))
                throw new D118RefusalException(ErrorCodes.InvalidArgument,
                    $"Input '{path}' lacks column '{need}'; observed columns [{string.Join(", ", columns)}].");
        }

        return new Table(columns, rows, isJson ? "json" : "csv");
    }

    public static string DecodeUtf8OrThrow(string path, byte[] bytes)
    {
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new D118RefusalException(ErrorCodes.InvalidArgument, $"Input '{path}' is not valid UTF-8; no decoding fallback was attempted.");
        }
    }

    private static List<IReadOnlyDictionary<string, string?>> FromJson(string text)
    {
        using var doc = JsonDocument.Parse(text);
        var items = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToList()
            : new List<JsonElement> { doc.RootElement };
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new D118RefusalException(ErrorCodes.InvalidArgument, "JSON input must be an array of objects (one record per object).");
            var row = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var p in item.EnumerateObject())
                row[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
            rows.Add(row);
        }

        return rows;
    }

    private static List<IReadOnlyDictionary<string, string?>> FromCsv(string text)
    {
        var records = ParseCsvRows(text);
        if (records.Count < 2)
            throw new D118RefusalException(ErrorCodes.InvalidArgument, "CSV input needs a header row plus at least one data row.");
        var header = records[0];
        if (header.Distinct(StringComparer.Ordinal).Count() != header.Count)
            throw new D118RefusalException(ErrorCodes.InvalidArgument, $"CSV header has duplicate column names [{string.Join(", ", header)}].");
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        for (var i = 1; i < records.Count; i++)
        {
            if (records[i].Count != header.Count)
                throw new D118RefusalException(ErrorCodes.InvalidArgument,
                    $"CSV data row {i + 1} has {records[i].Count} fields but the header declares {header.Count}.");
            rows.Add(header.Zip(records[i], (k, v) => (k, v)).ToDictionary(x => x.k, x => (string?)x.v, StringComparer.Ordinal));
        }

        return rows;
    }

    private static List<List<string>> ParseCsvRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"': quoted = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    row.Add(field.ToString()); field.Clear();
                    if (row.Any(x => !string.IsNullOrEmpty(x))) rows.Add(row);
                    row = new List<string>();
                    break;
                default: field.Append(c); break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            if (row.Any(x => !string.IsNullOrEmpty(x))) rows.Add(row);
        }

        return rows;
    }
}

/// <summary>确定性统计核（无随机、无时钟；顺序与格式随输入固定）。</summary>
internal static class D118Stats
{
    public static double Mean(IReadOnlyList<double> v) => v.Count == 0 ? double.NaN : v.Average();
    public static double? SampleSd(IReadOnlyList<double> v) => v.Count < 2 ? null : Math.Sqrt(v.Select(x => (x - Mean(v)) * (x - Mean(v))).Sum() / (v.Count - 1));

    public static double? Skewness(IReadOnlyList<double> v)
    {
        if (v.Count < 3) return null;
        var m = Mean(v);
        var s = SampleSd(v);
        if (s is null or 0) return null;
        return v.Sum(x => Math.Pow((x - m) / s.Value, 3)) * v.Count / ((v.Count - 1.0) * (v.Count - 2.0));
    }

    public static double? ExcessKurtosis(IReadOnlyList<double> v)
    {
        if (v.Count < 4) return null;
        var m = Mean(v);
        var s = SampleSd(v);
        if (s is null or 0) return null;
        var n = v.Count;
        var sum4 = v.Sum(x => Math.Pow((x - m) / s.Value, 4));
        return (n * (n + 1.0) * sum4 / ((n - 1.0) * (n - 2.0) * (n - 3.0))) - 3.0 * (n - 1.0) * (n - 1.0) / ((n - 2.0) * (n - 3.0));
    }

    public static double Quantile(IReadOnlyList<double> sortedAscending, double q)
    {
        if (sortedAscending.Count == 0) return double.NaN;
        var pos = q * (sortedAscending.Count - 1);
        var lo = (int)Math.Floor(pos);
        var hi = (int)Math.Ceiling(pos);
        return lo == hi ? sortedAscending[lo] : sortedAscending[lo] + (pos - lo) * (sortedAscending[hi] - sortedAscending[lo]);
    }

    public static double[] Ranks(IReadOnlyList<double> v)
    {
        var order = Enumerable.Range(0, v.Count).OrderBy(i => v[i]).ToList();
        var ranks = new double[v.Count];
        for (var i = 0; i < order.Count;)
        {
            var j = i;
            while (j + 1 < order.Count && Math.Abs(v[order[j + 1]] - v[order[i]]) < 1e-12) j++;
            var avg = (i + j + 2.0) / 2.0;
            for (var k = i; k <= j; k++) ranks[order[k]] = avg;
            i = j + 1;
        }

        return ranks;
    }

    public static double? Pearson(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        if (x.Count != y.Count || x.Count < 2) return null;
        var mx = Mean(x); var my = Mean(y);
        var sxy = x.Select((v, i) => (v - mx) * (y[i] - my)).Sum();
        var sxx = x.Sum(v => (v - mx) * (v - mx));
        var syy = y.Sum(v => (v - my) * (v - my));
        if (sxx <= 0 || syy <= 0) return null;
        return sxy / Math.Sqrt(sxx * syy);
    }

    public static (double Slope, double Intercept, double? R2) Ols(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        var mx = Mean(x); var my = Mean(y);
        var sxx = x.Sum(v => (v - mx) * (v - mx));
        var slope = sxx == 0 ? 0 : x.Select((v, i) => (v - mx) * (y[i] - my)).Sum() / sxx;
        var intercept = my - slope * mx;
        var predicted = x.Select(v => slope * v + intercept).ToList();
        var ssRes = y.Select((v, i) => (v - predicted[i]) * (v - predicted[i])).Sum();
        var ssTot = y.Sum(v => (v - my) * (v - my));
        return (slope, intercept, ssTot == 0 ? null : 1 - ssRes / ssTot);
    }

    public static double? MannKellS(IReadOnlyList<double> y)
    {
        if (y.Count < 3) return null;
        var s = 0L;
        for (var i = 0; i < y.Count - 1; i++)
            for (var j = i + 1; j < y.Count; j++)
                s += Math.Sign(y[j] - y[i]);
        return s;
    }

    /// <summary>Pettitt 单变点检验（秩统计量；p 值用其渐近指数近似，声明为近似而非精确）。</summary>
    public static (int Index, double? ApproximateP) Pettitt(IReadOnlyList<double> y)
    {
        var n = y.Count;
        var ranks = Ranks(y);
        var rankPrefix = new double[n + 1];
        for (var i = 0; i < n; i++) rankPrefix[i + 1] = rankPrefix[i] + ranks[i];
        var best = 1;
        var bestStat = double.NegativeInfinity;
        for (var t = 1; t < n; t++)
        {
            var stat = Math.Abs(2.0 * rankPrefix[t] - (double)t * (n + 1));
            if (stat > bestStat) { bestStat = stat; best = t; }
        }

        var k = Math.Round(bestStat);
        var p = 2.0 * Math.Exp(-6.0 * k * k / ((double)n * (n + 1) * (n + 2)));
        return (best, Math.Min(1.0, Math.Max(0.0, p)));
    }

    /// <summary>两侧 CUSUM（标准化残差累加，drift=k、阈值=h 由调用方显式给出）。</summary>
    public static IReadOnlyList<int> Cusum(IReadOnlyList<double> y, double k, double h)
    {
        if (y.Count < 2) return Array.Empty<int>();
        var m = Mean(y);
        var s = SampleSd(y) ?? 0;
        if (s == 0) return Array.Empty<int>();
        var sp = 0.0; var sm = 0.0;
        var signals = new List<int>();
        for (var i = 0; i < y.Count; i++)
        {
            var z = (y[i] - m) / s;
            sp = Math.Max(0, sp + z - k);
            sm = Math.Max(0, sm - z - k);
            if (sp > h || sm > h) { signals.Add(i); sp = 0; sm = 0; }
        }

        return signals;
    }

    /// <summary>Mann-Kendall 分段：以 |S| 的最小化位置切分，返回内部切分点（不含首尾）。</summary>
    public static IReadOnlyList<int> MannKendallSegments(IReadOnlyList<double> y, int minSegmentLength)
    {
        var cuts = new List<int>();
        Split(y, 0, y.Count, minSegmentLength, cuts);
        return cuts;
    }

    private static void Split(IReadOnlyList<double> y, int lo, int hi, int minLen, List<int> cuts)
    {
        var len = hi - lo;
        if (len < 2 * minLen) return;
        var best = -1;
        var bestAbs = double.PositiveInfinity;
        for (var t = lo + minLen; t <= hi - minLen; t++)
        {
            var left = y.Skip(lo).Take(t - lo).ToList();
            var right = y.Skip(t).Take(hi - t).ToList();
            var s1 = Math.Abs(MannKellS(left) ?? 0) / Math.Max(1, left.Count * (left.Count - 1.0) / 2);
            var s2 = Math.Abs(MannKellS(right) ?? 0) / Math.Max(1, right.Count * (right.Count - 1.0) / 2);
            var score = s1 + s2;
            if (score < bestAbs - 1e-15) { bestAbs = score; best = t; }
        }

        if (best < 0) return;
        cuts.Add(best - lo);
        Split(y, lo, best, minLen, cuts);
        Split(y, best, hi, minLen, cuts);
    }

    public static (double Lower, double Upper) Wilson(double hits, double n, double z = 1.959963984540054)
    {
        if (n <= 0) return (double.NaN, double.NaN);
        var p = hits / n;
        var den = 1 + z * z / n;
        var centre = (p + z * z / (2 * n)) / den;
        var half = z * Math.Sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / den;
        return (Math.Max(0, centre - half), Math.Min(1, centre + half));
    }

    public static double? InverseOf(double[][] m, out double[][]? inv)
    {
        inv = null;
        var n = m.Length;
        var a = m.Select(r => r.ToArray()).ToArray();
        inv = Enumerable.Range(0, n).Select(i => Enumerable.Range(0, n).Select(j => j == i ? 1.0 : 0.0).ToArray()).ToArray();
        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            var pivotMagnitude = Math.Abs(a[col][col]);
            for (var r = col + 1; r < n; r++)
            {
                if (Math.Abs(a[r][col]) <= pivotMagnitude) continue;
                pivot = r;
                pivotMagnitude = Math.Abs(a[r][col]);
            }

            if (pivotMagnitude < 1e-12) { inv = null; return null; }
            (a[col], a[pivot]) = (a[pivot], a[col]);
            (inv[col], inv[pivot]) = (inv[pivot], inv[col]);
            var d = a[col][col];
            for (var j = 0; j < n; j++) { a[col][j] /= d; inv[col][j] /= d; }
            for (var r = 0; r < n; r++)
            {
                if (r == col) continue;
                var factor = a[r][col];
                if (factor == 0) continue;
                for (var j = 0; j < n; j++) { a[r][j] -= factor * a[col][j]; inv[r][j] -= factor * inv[col][j]; }
            }
        }

        return 1;
    }
}

/// <summary>mulberry32 —— 跨运行时确定的伪随机源（采样与分折用，种子显式）。</summary>
internal sealed class D118Rand
{
    private uint _state;

    public D118Rand(long seed) => _state = unchecked((uint)seed * 2654435761u + 1013904223u);

    public double Next()
    {
        _state += 4294967295u;
        var t = _state;
        t = unchecked((t ^ (t >> 16)) * 2246822507u);
        t = unchecked((t ^ (t >> 13)) * 3266489909u);
        return ((t ^ (t >> 16)) & 0xFFFFFF) / (double)0x1000000;
    }

    public int NextIndex(int exclusiveUpper) => Math.Min(exclusiveUpper - 1, (int)(Next() * exclusiveUpper));
}

/// <summary>输出闸门与写盘（先受保护根守卫，再覆写判定，两者均先于任何字节写出）。</summary>
internal static class D118Outputs
{
    public static string GuardPath(string? path, string parameter)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new D118RefusalException(ErrorCodes.InvalidArgument, $"Parameter '{parameter}' must be a non-empty path.");
        var hit = ProtectedOutputPathGuard.Match(path);
        if (hit is not null)
            throw new D118RefusalException(ErrorCodes.PathEscapeRejected,
                $"'{path}' is inside a protected root ('{hit}'); refused before any write (no artefacts).");
        return path;
    }

    public static string OverwriteGate(string path, bool overwrite)
    {
        var full = Path.GetFullPath(path);
        var existence = File.Exists(full) ? OutputExistence.Exists : OutputExistence.NotExists;
        var decision = OverwritePolicy.Decide(existence, overwrite, full, null);
        if (!decision.Proceed) throw new D118RefusalException(decision.ErrorCode!, decision.Message!);
        return full;
    }

    public static string WriteJson(string fullPath, object payload)
    {
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return fullPath;
    }

    public static string Abs(string path) => Path.GetFullPath(path);
}

/// <summary>可预期的拒绝路径（fail-closed）；由 D118M4ToolBase 统一映射为稳定错误码。</summary>
internal sealed class D118RefusalException : Exception
{
    public string Code { get; }

    public D118RefusalException(string code, string message) : base(message) => Code = code;
}

/// <summary>
/// D-118 十五件的共同骨架：冻结参数面校验 → 受保护根与覆写闸门 → 窄子集计算或 fail-closed。
/// 任何校验或闸门失败都发生在第一次字节写出之前（sideEffects=false）。
/// </summary>
public abstract class D118M4ToolBase : McpToolBase
{
    protected abstract string FrozenName { get; }
    /// <summary>类别取值（d118CategoryChoice：避开被全局计数守卫的 Python/Analysis 面）。</summary>
    protected abstract string FrozenCategory { get; }

    protected sealed override string CategoryName => FrozenCategory;

    public sealed override IReadOnlyDictionary<string, object?> InputSchema => D118SchemaFace.For(FrozenName);

    public sealed override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var reason = D118SchemaFace.Validate(FrozenName, context.Arguments);
        if (reason is not null)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, reason + " No computation ran and no output was written.");

        try
        {
            return await OnExecuteAsync(context).ConfigureAwait(false);
        }
        catch (D118RefusalException ex)
        {
            return OperationResult<object?>.Fail(ex.Code, ex.Message);
        }
        catch (FileNotFoundException)
        {
            return OperationResult<object?>.Fail(ErrorCodes.NotFound, "The declared input file was not found; no output was written.");
        }
        catch (DirectoryNotFoundException)
        {
            return OperationResult<object?>.Fail(ErrorCodes.NotFound, "The declared input or output directory does not exist; no output was written.");
        }
        catch (UnauthorizedAccessException)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, "The declared path cannot be accessed with the current permissions; no output was written.");
        }
        catch (JsonException ex)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"Structured input is not valid JSON: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return OperationResult<object?>.Fail(ErrorCodes.Cancelled, "Execution was cancelled; sideEffects=false was not exceeded.");
        }
        catch (IOException ex)
        {
            return OperationResult<object?>.Fail(ErrorCodes.ExecutionFailed, "A declared input or output path could not be read or written.", ex.Message);
        }
    }

    protected abstract Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context);

    /// <summary>原始参数取值（同时容忍服务端 JsonElement 面）。</summary>
    protected static object? ToolArg(ToolExecutionContext context, string name)
        => context.Arguments is { } args && args.TryGetValue(name, out var value) ? value : null;

    /// <summary>必填字符串面（冻结 schema 已保证在场，此处只做非空收敛）。</summary>
    protected static string RequiredString(ToolExecutionContext context, string name)
        => D118Values.AsString(ToolArg(context, name)) is { Length: > 0 } text ? text.Trim()
            : throw new D118RefusalException(ErrorCodes.InvalidArgument, $"Parameter '{name}' resolved to an empty value.");

    /// <summary>可选字符串面（空白按未提供处理，不代入默认语义之外的值）。</summary>
    protected static string? OptionalString(ToolExecutionContext context, string name)
    {
        var text = D118Values.AsString(ToolArg(context, name));
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>宿主依赖面未实证时的统一 fail-closed（不臆造数值，零副作用）。</summary>
    protected static OperationResult<object?> HostDependent(string capability, string neededFace)
        => OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
            $"{capability} is registered with its frozen parameter face, but the {neededFace} requires a live ArcGIS Pro host session. " +
            "This batch declares 真机面 NOT VERIFIED; the call was refused before any computation or write (sideEffects=false).");
}
