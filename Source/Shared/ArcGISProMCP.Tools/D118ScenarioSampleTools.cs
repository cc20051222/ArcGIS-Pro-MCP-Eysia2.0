// D-118 之二：空间采样设计／空间交叉验证／情景敏感性／多准则比较／集成评估（五件，离线可实算的窄子集）。
using System.Globalization;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>X29 design_spatial_sample — 由坐标表构造确定性的抽样方案（SRS／分层／系统网格）。</summary>
public sealed class DesignSpatialSampleTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.DesignSpatialSampleToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.DataManagement;
    public override string Name => ToolKey;
    public override string Description =>
        "空间抽样设计（M4 X29）。窄子集＝CSV/JSON 坐标表（列名 x/y，或头两列可解析为数值）；设计＝总体简单随机、按 strataField 分层（比例分配，每层至少 1）、" +
        "或系统网格（按 extent 均分至 sampleSize）；seed 确定复现。constraints 只支持 {} 空对象——任何约束键均 NOT_IMPLEMENTED（不臆造求解器）。" +
        "输出＝被选记录的设计清单 JSON（不含任何属性推断）。";

    protected override async Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        await Task.CompletedTask;
        var population = RequiredString(context, "population");
        var outputPath = D118Outputs.GuardPath(RequiredString(context, "outputPath"), "outputPath");
        var sampleSize = D118Values.AsInt(ToolArg(context, "sampleSize")) ?? 0;
        if (sampleSize < 1)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "sampleSize must be a positive integer.");
        var strataField = OptionalString(context, "strataField");
        var constraints = D118Values.AsObject(ToolArg(context, "constraints"));
        if (constraints is { Count: > 0 })
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
                $"constraints keys [{string.Join(", ", constraints.Keys)}] would require a solver this batch does not ship; refusing to emit an unconstrained plan labelled as constrained. Nothing was written.");
        var seed = D118Values.AsInt(ToolArg(context, "seed")) ?? 42;

        var table = D118Tables.Load(population);
        var (xColumn, yColumn) = ResolveCoordinates(table);
        var records = new List<(int Index, string? Stratum, double X, double Y)>();
        for (var i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var x = D118Values.AsDouble(row.TryGetValue(xColumn, out var xv) ? xv : null);
            var y = D118Values.AsDouble(row.TryGetValue(yColumn, out var yv) ? yv : null);
            if (x is null || y is null || !double.IsFinite(x.Value) || !double.IsFinite(y.Value)) continue;
            var stratum = strataField is null ? null : (row.TryGetValue(strataField, out var s) ? s : null);
            records.Add((i, stratum, x.Value, y.Value));
        }

        if (records.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"No finite coordinate rows were parsed from '{population}' using columns {xColumn}/{yColumn}.");
        if (sampleSize > records.Count)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                $"sampleSize {sampleSize} exceeds the {records.Count} usable population records; nothing was written.");

        var rand = new D118Rand(seed);
        List<int> selected;
        string design;
        if (strataField is not null)
        {
            design = "stratified-proportional-min-one";
            selected = new List<int>();
            foreach (var group in records.GroupBy(r => r.Stratum ?? "<null>").OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var share = (int)Math.Round((double)group.Count() / records.Count * sampleSize, MidpointRounding.AwayFromZero);
                var take = Math.Max(1, Math.Min(group.Count(), share));
                selected.AddRange(Shuffle(group.Select(g => g.Index).ToList(), rand).Take(take));
            }

            if (selected.Count > sampleSize) selected = selected.OrderBy(i => i).Take(sampleSize).ToList();
        }
        else if (string.Equals(OptionalString(context, "design") ?? "simple_random", "systematic", StringComparison.Ordinal))
        {
            design = "systematic-grid";
            selected = Systematic(records, sampleSize);
        }
        else
        {
            design = "simple-random-without-replacement";
            selected = Shuffle(records.Select(r => r.Index).ToList(), rand).Take(sampleSize).OrderBy(i => i).ToList();
        }

        var chosen = selected.OrderBy(i => i).Select(i => records[i]).ToList();
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tool"] = ToolKey,
            ["schemaSha256"] = D118SchemaFace.SchemaSha256(ToolKey),
            ["population"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = D118Outputs.Abs(population),
                ["xColumn"] = xColumn,
                ["yColumn"] = yColumn,
                ["strataField"] = strataField,
                ["usableRecords"] = records.Count,
            },
            ["design"] = design,
            ["sampleSizeRequested"] = sampleSize,
            ["sampleSizeProduced"] = chosen.Count,
            ["seed"] = seed,
            ["selected"] = chosen.Select(c => (IDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["rowIndex"] = c.Index,
                ["x"] = c.X,
                ["y"] = c.Y,
                ["stratum"] = c.Stratum,
            }).ToList(),
            ["verification"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["deterministic"] = "selection order comes from the declared seed and row order; re-running with the same seed reproduces the same plan",
                ["attributeDerivation"] = "NOT VERIFIED — no attribute values are read or implied; the plan lists coordinates and row indices only",
                ["constraintSolving"] = "NOT VERIFIED (no solver shipped)",
                ["sideEffects"] = "one JSON plan file at outputPath",
            },
        };

        var full = D118Outputs.OverwriteGate(outputPath, ToolArgs.GetBool(context, "overwrite"));
        D118Outputs.WriteJson(full, payload);
        return OperationResult<object?>.Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outputPath"] = full,
            ["report"] = payload,
        }, $"Spatial sampling plan ({design}) written for {chosen.Count} of {records.Count} records.");
    }

    private static (string X, string Y) ResolveCoordinates(D118Tables.Table table)
    {
        if (table.Columns.Contains("x", StringComparer.Ordinal) && table.Columns.Contains("y", StringComparer.Ordinal)) return ("x", "y");
        var numeric = table.Columns.Where(c => table.Rows.Any(r => r.TryGetValue(c, out var v) && D118Values.AsDouble(v) is not null)).ToList();
        if (numeric.Count >= 2) return (numeric[0], numeric[1]);
        throw new D118RefusalException(ErrorCodes.InvalidArgument,
            $"Cannot resolve coordinate columns from [{string.Join(", ", table.Columns)}]; name them x/y or provide at least two numeric columns.");
    }

    private static List<int> Shuffle(List<int> source, D118Rand rand)
    {
        var a = source.ToList();
        for (var i = a.Count - 1; i > 0; i--)
        {
            var j = rand.NextIndex(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }

        return a;
    }

    private static List<int> Systematic(List<(int Index, string? Stratum, double X, double Y)> records, int sampleSize)
    {
        var minX = records.Min(r => r.X); var maxX = records.Max(r => r.X);
        var minY = records.Min(r => r.Y); var maxY = records.Max(r => r.Y);
        var side = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(sampleSize)));
        var stepX = (maxX - minX) / side;
        var stepY = (maxY - minY) / side;
        var picked = new List<(double Distance, int Index)>();
        for (var gx = 0; gx < side; gx++)
        {
            for (var gy = 0; gy < side; gy++)
            {
                if (picked.Count >= sampleSize) break;
                var tx = minX + (gx + 0.5) * stepX;
                var ty = minY + (gy + 0.5) * stepY;
                var nearest = records.MinBy(r => Math.Sqrt((r.X - tx) * (r.X - tx) + (r.Y - ty) * (r.Y - ty)));
                picked.Add((Math.Sqrt((nearest.X - tx) * (nearest.X - tx) + (nearest.Y - ty) * (nearest.Y - ty)), nearest.Index));
            }
        }

        return picked.OrderBy(p => p.Distance).Select(p => p.Index).Distinct().Take(sampleSize).ToList();
    }
}

/// <summary>X31 cross_validate_spatial_model — 分折划分与留出指标（均值／一元线性两种声明预测器）。</summary>
public sealed class CrossValidateSpatialModelTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.CrossValidateSpatialModelToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Quality;
    public override string Name => ToolKey;
    public override string Description =>
        "空间交叉验证（M4 X31）。窄子集＝CSV/JSON 观测表＋modelSpec {response, predictor: mean|linear, predictors:[列名]}；" +
        "blocking＝random／spatial_blocks（需 x/y 列，按 k-means 分块）／leave_one_out（记录数 ≤ 60）；folds 默认 5。" +
        "输出＝每折训练/留出索引与留出 RMSE/MAE/R²，池化指标；任何其它模型族（地理加权回归、克里金等）均 NOT_IMPLEMENTED。";

    protected override async Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        await Task.CompletedTask;
        var observations = RequiredString(context, "observations");
        var outputPath = D118Outputs.GuardPath(RequiredString(context, "outputPath"), "outputPath");
        var blocking = RequiredString(context, "blocking");
        var modelSpec = D118Values.AsObject(ToolArg(context, "modelSpec"));
        if (modelSpec is null)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "modelSpec must be an object declaring response, predictor and predictors.");
        var response = D118Values.AsString(ToolArgIn(modelSpec, "response"));
        var predictor = (D118Values.AsString(ToolArgIn(modelSpec, "predictor")) ?? "mean").Trim().ToLowerInvariant();
        if (predictor is not ("mean" or "linear"))
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
                $"modelSpec.predictor '{predictor}' is outside the implemented subset [mean, linear]; no surrogate model was substituted.");

        var table = D118Tables.Load(observations, response!);
        var featureColumns = D118Values.AsList(ToolArgIn(modelSpec, "predictors")).Select(D118Values.AsString).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToList();
        if (predictor == "linear" && featureColumns.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "modelSpec.predictors must list at least one column when predictor='linear'.");
        foreach (var column in featureColumns)
            if (!table.Columns.Contains(column, StringComparer.Ordinal))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"modelSpec.predictors column '{column}' is absent from the observations header [{string.Join(", ", table.Columns)}].");

        var rows = new List<(int Index, double Y, List<double> X)>();
        for (var i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var y = D118Values.AsDouble(row.TryGetValue(response!, out var rv) ? rv : null);
            if (y is null || !double.IsFinite(y.Value)) continue;
            var xs = featureColumns.Select(c => D118Values.AsDouble(row.TryGetValue(c, out var v) ? v : null) ?? double.NaN).ToList();
            if (xs.Any(double.IsNaN)) continue;
            rows.Add((i, y.Value, xs));
        }

        var folds = D118Values.AsInt(ToolArg(context, "folds")) ?? 5;
        if (folds < 2)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "folds must be at least 2.");
        var seed = D118Values.AsInt(ToolArg(context, "seed")) ?? 42;

        List<List<int>> parts;
        switch (blocking)
        {
            case "leave_one_out":
                if (rows.Count > 60)
                    return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                        $"leave_one_out with {rows.Count} records exceeds the 60-record cap declared for this batch; use spatial_blocks or random blocking.");
                parts = rows.Select((_, i) => new List<int> { i }).ToList();
                break;
            case "random":
                parts = Partition(Enumerable.Range(0, rows.Count).ToList(), folds, new D118Rand(seed));
                break;
            case "spatial_blocks":
                parts = SpatialBlocks(table, rows, folds, seed);
                break;
            default:
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                    $"blocking '{blocking}' is outside the frozen enum [random, spatial_blocks, leave_one_out].");
        }

        var perFold = new List<IDictionary<string, object?>>();
        var pooledErrors = new List<double>();
        var actuals = new List<double>();
        var predictions = new List<double>();
        for (var f = 0; f < parts.Count; f++)
        {
            var test = parts[f];
            var train = Enumerable.Range(0, rows.Count).Where(i => !test.Contains(i)).ToList();
            if (train.Count == 0 || test.Count == 0) continue;
            var predicted = new List<double>();
            string mode;
            if (predictor == "mean")
            {
                var m = D118Stats.Mean(train.Select(i => rows[i].Y).ToList());
                predicted.AddRange(test.Select(_ => m));
                mode = "training-mean";
            }
            else
            {
                var feature = 0;
                var x = train.Select(i => rows[i].X[feature]).ToList();
                var y = train.Select(i => rows[i].Y).ToList();
                var (slope, intercept, _) = D118Stats.Ols(x, y);
                predicted.AddRange(test.Select(i => slope * rows[i].X[feature] + intercept));
                mode = $"univariate-ols-on-{featureColumns[feature]}";
            }

            var actual = test.Select(i => rows[i].Y).ToList();
            pooledErrors.AddRange(actual.Select((a, k) => a - predicted[k]));
            actuals.AddRange(actual);
            predictions.AddRange(predicted);
            perFold.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["fold"] = f,
                ["fitMode"] = mode,
                ["trainIndices"] = train,
                ["testIndices"] = test,
                ["foldRmse"] = Math.Sqrt(actual.Select((a, k) => (a - predicted[k]) * (a - predicted[k])).Average()),
                ["foldMae"] = actual.Select((a, k) => Math.Abs(a - predicted[k])).Average(),
            });
        }

        var rmse = Math.Sqrt(pooledErrors.Select(e => e * e).Average());
        var meanActual = D118Stats.Mean(actuals);
        var r2 = actuals.Sum(a => (a - meanActual) * (a - meanActual)) == 0
            ? (double?)null
            : 1 - pooledErrors.Sum(e => e * e) / actuals.Sum(a => (a - meanActual) * (a - meanActual));
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tool"] = ToolKey,
            ["schemaSha256"] = D118SchemaFace.SchemaSha256(ToolKey),
            ["observations"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = D118Outputs.Abs(observations),
                ["response"] = response,
                ["predictors"] = featureColumns,
                ["usableRecords"] = rows.Count,
            },
            ["validation"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["blocking"] = blocking,
                ["foldsRequested"] = folds,
                ["foldsProduced"] = perFold.Count,
                ["seed"] = seed,
            },
            ["metrics"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pooledRmse"] = rmse,
                ["pooledMae"] = pooledErrors.Select(Math.Abs).Average(),
                ["pooledR2"] = r2,
            },
            ["folds"] = perFold,
            ["verification"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["metrics"] = "measured by holding out the listed test indices; the fitted predictor is the declared narrow model",
                ["modelFidelity"] = "NOT VERIFIED beyond the declared mean/univariate-OLS predictor (no geostatistical or ML model is shipped)",
                ["arcgisHostFace"] = "NOT VERIFIED (no Pro session executed in D-118)",
                ["sideEffects"] = "one JSON report file at outputPath",
            },
        };

        var full = D118Outputs.OverwriteGate(outputPath, ToolArgs.GetBool(context, "overwrite"));
        D118Outputs.WriteJson(full, payload);
        return OperationResult<object?>.Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outputPath"] = full,
            ["report"] = payload,
        }, $"Cross-validation report written over {perFold.Count} fold(s) with pooled RMSE {rmse.ToString("G6", CultureInfo.InvariantCulture)}.");
    }

    private static List<List<int>> Partition(List<int> indices, int folds, D118Rand rand)
    {
        var shuffled = indices.ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = rand.NextIndex(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var parts = Enumerable.Range(0, folds).Select(_ => new List<int>()).ToList();
        for (var i = 0; i < shuffled.Count; i++) parts[i % folds].Add(shuffled[i]);
        return parts.Where(p => p.Count > 0).ToList();
    }

    private static List<List<int>> SpatialBlocks(D118Tables.Table table, List<(int Index, double Y, List<double> X)> rows, int folds, int seed)
    {
        var coords = rows.Select(r => r.Index).ToList();
        var (xColumn, yColumn) = (table.Columns.Contains("x", StringComparer.Ordinal) ? "x" : null, table.Columns.Contains("y", StringComparer.Ordinal) ? "y" : null);
        if (xColumn is null || yColumn is null)
            throw new D118RefusalException(ErrorCodes.InvalidArgument, "blocking='spatial_blocks' needs x/y columns in the observations table.");
        var points = rows.Select(r => new[]
        {
            D118Values.AsDouble(table.Rows[r.Index].TryGetValue(xColumn, out var xv) ? xv : null) ?? 0,
            D118Values.AsDouble(table.Rows[r.Index].TryGetValue(yColumn, out var yv) ? yv : null) ?? 0,
        }).ToList();
        return KMeansBatches(points, folds, seed);
    }

    private static List<List<int>> KMeansBatches(List<double[]> points, int k, int seed)
    {
        var rand = new D118Rand(seed);
        var centroids = Enumerable.Range(0, k).Select(c => points[(c * points.Count) / Math.Max(1, k)].ToArray()).ToList();
        var assign = new int[points.Count];
        for (var iteration = 0; iteration < 16; iteration++)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var best = 0; var bestD = double.PositiveInfinity;
                for (var c = 0; c < k; c++)
                {
                    var dx = points[i][0] - centroids[c][0]; var dy = points[i][1] - centroids[c][1];
                    var d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = c; }
                }

                assign[i] = best;
            }

            for (var c = 0; c < k; c++)
            {
                var members = points.Select((p, i) => (p, i)).Where(x => assign[x.i] == c).Select(x => x.p).ToList();
                if (members.Count == 0) continue;
                centroids[c] = new[] { members.Average(m => m[0]), members.Average(m => m[1]) };
            }
        }

        return Enumerable.Range(0, k).Select(c => Enumerable.Range(0, points.Count).Where(i => assign[i] == c).ToList()).Where(l => l.Count > 0).ToList();
    }

    private static object? ToolArgIn(IReadOnlyDictionary<string, object?> container, string key)
        => container.TryGetValue(key, out var v) ? v : null;
}

/// <summary>X32 analyze_scenario_sensitivity — 参数域枚举与情景差异表（不含模型求解）。</summary>
public sealed class AnalyzeScenarioSensitivityTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.AnalyzeScenarioSensitivityToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Quality;
    public override string Name => ToolKey;
    public override string Description =>
        "情景敏感性分析（M4 X32）。窄子集＝parameters 对象（每参数给 [low, high]、{low, high} 或 levels 列表）与 scenarios 数组（每情景为参数→取值对象）；" +
        "输出＝三水平全因子设计矩阵（受 maxSolves 截断）、情景与参数域的一致性核对（越界标记、缺项标记）与逐参数极差。" +
        "**目标函数求解不在本子集内**：任何 objective/solve 字段一律 NOT_IMPLEMENTED，报告只声明设计面事实。";

    protected override async Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        await Task.CompletedTask;
        var outputPath = D118Outputs.GuardPath(RequiredString(context, "outputPath"), "outputPath");
        var parameters = D118Values.AsObject(ToolArg(context, "parameters"));
        var scenarios = D118Values.AsList(ToolArg(context, "scenarios"));
        if (parameters is null || parameters.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "parameters must declare at least one named parameter range.");
        var maxSolves = D118Values.AsInt(ToolArg(context, "maxSolves")) ?? 243;
        var seed = D118Values.AsInt(ToolArg(context, "seed")) ?? 42;

        var ranges = new Dictionary<string, (double Low, double High, List<double> Levels)>(StringComparer.Ordinal);
        foreach (var (name, spec) in parameters)
        {
            var parsed = ParseRange(spec);
            if (parsed is null)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                    $"parameter '{name}' is not expressed as [low, high], {{low, high}} or a numeric levels list; nothing was enumerated.");
            ranges[name] = parsed.Value;
        }

        var grid = new List<IDictionary<string, object?>> { new Dictionary<string, object?>(StringComparer.Ordinal) };
        foreach (var (name, range) in ranges)
        {
            var next = new List<IDictionary<string, object?>>();
            foreach (var existing in grid)
                foreach (var level in range.Levels)
                {
                    var copy = new Dictionary<string, object?>(existing, StringComparer.Ordinal) { [name] = level };
                    next.Add(copy);
                }

            grid = next;
            if (grid.Count > maxSolves)
            {
                grid = grid.Take(maxSolves).ToList();
                break;
            }
        }

        var scenarioRows = new List<IDictionary<string, object?>>();
        foreach (var item in scenarios)
        {
            var obj = D118Values.AsObject(item);
            if (obj is null)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "each scenario must be an object mapping parameter names to numeric values.");
            if (obj.ContainsKey("objective") || obj.ContainsKey("solve") || obj.ContainsKey("result"))
                return OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
                    "scenarios carrying objective/solve/result fields would require a model solver this batch does not ship; refusing to echo unverified numbers. Nothing was written.");
            var issues = new List<string>();
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (name, range) in ranges)
            {
                if (!obj.TryGetValue(name, out var raw) || D118Values.AsDouble(raw) is not { } v) { issues.Add($"missing:{name}"); continue; }
                if (v < range.Low || v > range.High) issues.Add($"out-of-range:{name}={v.ToString(CultureInfo.InvariantCulture)}");
                values[name] = v;
            }

            foreach (var extra in obj.Keys.Where(k => !ranges.ContainsKey(k)))
                issues.Add($"undeclared-parameter:{extra}");
            scenarioRows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["values"] = values,
                ["issues"] = issues,
            });
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tool"] = ToolKey,
            ["schemaSha256"] = D118SchemaFace.SchemaSha256(ToolKey),
            ["parameters"] = ranges.ToDictionary(kv => kv.Key,
                kv => (object?)new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["low"] = kv.Value.Low, ["high"] = kv.Value.High, ["span"] = kv.Value.High - kv.Value.Low, ["levels"] = kv.Value.Levels,
                }, StringComparer.Ordinal),
            ["designMatrix"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["rows"] = grid,
                ["rowsProduced"] = grid.Count,
                ["maxSolves"] = maxSolves,
                ["truncated"] = grid.Count == maxSolves,
                ["seed"] = seed,
            },
            ["scenarios"] = scenarioRows,
            ["verification"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["designEnumeration"] = "enumerated exactly from the declared ranges (three levels per parameter: low, midpoint, high, or the supplied level list)",
                ["sensitivityOfObjective"] = "NOT VERIFIED — no objective function or solver is shipped; ranking/sensitivity numbers would be invented",
                ["sideEffects"] = "one JSON design file at outputPath",
            },
        };

        var full = D118Outputs.OverwriteGate(outputPath, ToolArgs.GetBool(context, "overwrite"));
        D118Outputs.WriteJson(full, payload);
        return OperationResult<object?>.Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outputPath"] = full,
            ["report"] = payload,
        }, $"Sensitivity design face written ({grid.Count} design row(s), {scenarioRows.Count} scenario check(s)); objective solve remains NOT VERIFIED.");
    }

    private static (double Low, double High, List<double> Levels)? ParseRange(object? spec)
    {
        var list = D118Values.AsList(spec);
        if (list.Count >= 2 && list.All(x => D118Values.AsDouble(x) is not null))
        {
            var nums = list.Select(x => D118Values.AsDouble(x)!.Value).ToList();
            return (nums.Min(), nums.Max(), nums.Count == 2 ? new List<double> { nums[0], (nums[0] + nums[1]) / 2, nums[1] } : nums);
        }

        if (D118Values.AsObject(spec) is { } obj
            && D118Values.AsDouble(ToolArgIn(obj, "low")) is { } low
            && D118Values.AsDouble(ToolArgIn(obj, "high")) is { } high)
        {
            var levels = D118Values.AsList(ToolArgIn(obj, "levels")).Select(x => D118Values.AsDouble(x)).Where(x => x is not null).Select(x => x!.Value).ToList();
            return (low, high, levels.Count >= 2 ? levels : new List<double> { low, (low + high) / 2, high });
        }

        return null;
    }

    private static object? ToolArgIn(IReadOnlyDictionary<string, object?> container, string key)
        => container.TryGetValue(key, out var v) ? v : null;
}

/// <summary>X34 compare_multi_criteria_scenarios — 加权求和与支配关系（可实算）。</summary>
public sealed class CompareMultiCriteriaScenariosTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.CompareMultiCriteriaScenariosToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Quality;
    public override string Name => ToolKey;
    public override string Description =>
        "多准则情景比较（M4 X34）。窄子集＝alternatives 表（每行一个备选，列含 id 与各准则数值）、criteria 列表（准则 id，或 {id, direction: max|min}）、" +
        "weights 对象（id→非负数，和须为 1±1e-6）；输出＝min-max 规范化矩阵、加权得分、名次（并列按 id 确定复现）与 Pareto 支配标记。" +
        "constraints 数组属未实现子集（不引入约束求解）。";

    protected override async Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        await Task.CompletedTask;
        var alternativesPath = RequiredString(context, "alternatives");
        var outputPath = D118Outputs.GuardPath(RequiredString(context, "outputPath"), "outputPath");
        if (D118Values.AsList(ToolArg(context, "constraints")).Count(x => x is not null) > 0)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
                "constraints would require a constraint solver this batch does not ship; refusing to report an unconstrained ranking as constrained. Nothing was written.");

        var criteriaIds = new List<string>();
        var directions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in D118Values.AsList(ToolArg(context, "criteria")))
        {
            if (D118Values.AsString(item) is { Length: > 0 } plain && D118Values.AsObject(item) is null)
            {
                criteriaIds.Add(plain);
                directions[plain] = "max";
                continue;
            }

            var obj = D118Values.AsObject(item);
            var id = D118Values.AsString(obj is not null && obj.TryGetValue("id", out var iv) ? iv : null);
            if (string.IsNullOrWhiteSpace(id))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "each criteria object must declare an 'id'.");
            var direction = (D118Values.AsString(obj!.TryGetValue("direction", out var dv) ? dv : null) ?? "max").Trim().ToLowerInvariant();
            if (direction is not ("max" or "min"))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"criteria '{id}' direction '{direction}' must be 'max' or 'min'.");
            criteriaIds.Add(id);
            directions[id] = direction;
        }

        var weights = D118Values.AsObject(ToolArg(context, "weights"));
        if (weights is null || criteriaIds.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "criteria and weights must both be non-empty.");
        var weightMap = criteriaIds.ToDictionary(id => id, id => D118Values.AsDouble(weights.TryGetValue(id, out var w) ? w : null), StringComparer.Ordinal);
        if (weightMap.Any(kv => kv.Value is null || kv.Value < 0))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "every criterion needs a non-negative numeric weight.");
        var sum = weightMap.Values.Sum(v => v!.Value);
        if (Math.Abs(sum - 1.0) > 1e-6)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"weights must sum to 1 (observed {sum.ToString("G17", CultureInfo.InvariantCulture)}); no renormalisation is silently applied.");

        var table = D118Tables.Load(alternativesPath);
        var idColumn = table.Columns.Contains("id", StringComparer.Ordinal) ? "id" : (table.Columns.Contains("name", StringComparer.Ordinal) ? "name" : table.Columns[0]);
        foreach (var criterion in criteriaIds)
            if (!table.Columns.Contains(criterion, StringComparer.Ordinal))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                    $"alternatives table lacks criterion column '{criterion}'; observed columns [{string.Join(", ", table.Columns)}].");

        var alternatives = new List<(string Id, double[] Values)>();
        foreach (var row in table.Rows)
        {
            var id = row.TryGetValue(idColumn, out var rawId) ? rawId : null;
            if (string.IsNullOrWhiteSpace(id)) continue;
            var values = criteriaIds.Select(c => D118Values.AsDouble(row.TryGetValue(c, out var v) ? v : null)).ToList();
            if (values.Any(v => v is null || !double.IsFinite(v.Value))) continue;
            alternatives.Add((id!, values.Select(v => v!.Value).ToArray()));
        }

        if (alternatives.Count < 2)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"Fewer than two usable alternatives rows were parsed from '{alternativesPath}'.");

        var normalized = alternatives.Select(a =>
        {
            var row = new double[criteriaIds.Count];
            for (var c = 0; c < criteriaIds.Count; c++)
            {
                var column = alternatives.Select(x => x.Values[c]).ToList();
                var min = column.Min(); var max = column.Max();
                var scaled = max - min == 0 ? 0.0 : (a.Values[c] - min) / (max - min);
                row[c] = directions[criteriaIds[c]] == "min" ? 1 - scaled : scaled;
            }

            return row;
        }).ToList();

        var scores = normalized.Select((n, i) => (Id: alternatives[i].Id, Raw: alternatives[i].Values, Score: n.Zip(criteriaIds, (v, c) => v * weightMap[c]!.Value).Sum())).ToList();
        var ranked = scores.OrderByDescending(s => s.Score).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();
        var dominated = ranked.ToDictionary(s => s.Id, s => DominatedBy(s, scores), StringComparer.Ordinal);

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tool"] = ToolKey,
            ["schemaSha256"] = D118SchemaFace.SchemaSha256(ToolKey),
            ["criteria"] = criteriaIds.Select(c => (IDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = c, ["direction"] = directions[c], ["weight"] = weightMap[c],
            }).ToList(),
            ["alternativesSource"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = D118Outputs.Abs(alternativesPath), ["idColumn"] = idColumn, ["usableRows"] = alternatives.Count,
            },
            ["ranking"] = ranked.Select((s, i) => (IDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["rank"] = i + 1,
                ["id"] = s.Id,
                ["weightedScore"] = s.Score,
                ["rawValues"] = s.Raw,
                ["paretoDominatedBy"] = dominated[s.Id],
            }).ToList(),
            ["verification"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["scoreBasis"] = "min-max normalisation per criterion (direction-aware) times the declared weights; sums are exact in double precision",
                ["tieHandling"] = "equal scores order by alternative id (ordinal), so the ranking is reproducible",
                ["constraintSolving"] = "NOT VERIFIED (no solver shipped)",
                ["sideEffects"] = "one JSON report file at outputPath",
            },
        };

        var full = D118Outputs.OverwriteGate(outputPath, ToolArgs.GetBool(context, "overwrite"));
        D118Outputs.WriteJson(full, payload);
        return OperationResult<object?>.Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outputPath"] = full,
            ["report"] = payload,
        }, $"Multi-criteria ranking written for {ranked.Count} alternative(s).");
    }

    private static List<string> DominatedBy((string Id, double[] Raw, double Score) target, List<(string Id, double[] Raw, double Score)> all)
        => all.Where(o => o.Id != target.Id && o.Raw.Zip(target.Raw, (a, b) => a >= b).All(x => x) && o.Raw.Zip(target.Raw, (a, b) => a > b).Any(x => x))
            .Select(o => o.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
}

/// <summary>X35 evaluate_scenario_ensemble — 集成成员的一致性与分歧度量。</summary>
public sealed class EvaluateScenarioEnsembleTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.EvaluateScenarioEnsembleToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Quality;
    public override string Name => ToolKey;
    public override string Description =>
        "情景集成评估（M4 X35）。窄子集＝ensemble 数组（每成员 {member/id, value} 或 {scenario, value}）＋可选 weights（成员→权重，和须为 1±1e-6）；" +
        "输出＝逐情景（或全局）的均值/极差/标准差、离散度（sd/|mean|，声明公式）、超阈分歧成员名单与加权均值。阈值 disagreementThreshold 默认 0.25。";

    protected override async Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        await Task.CompletedTask;
        var outputPath = D118Outputs.GuardPath(RequiredString(context, "outputPath"), "outputPath");
        var threshold = D118Values.AsDouble(ToolArg(context, "disagreementThreshold")) ?? 0.25;
        if (threshold <= 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "disagreementThreshold must be a positive number.");

        var members = new List<(string Member, string Scenario, double Value)>();
        var index = 0;
        foreach (var item in D118Values.AsList(ToolArg(context, "ensemble")))
        {
            index++;
            var obj = D118Values.AsObject(item);
            if (obj is null)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                    $"ensemble item {index} is not an object with member/scenario/value fields; no positional guessing was applied.");
            var member = D118Values.AsString(Read(obj, "member")) ?? D118Values.AsString(Read(obj, "id")) ?? ("member_" + index.ToString(CultureInfo.InvariantCulture));
            var scenario = D118Values.AsString(Read(obj, "scenario")) ?? "__global__";
            var value = D118Values.AsDouble(Read(obj, "value"));
            if (value is null || !double.IsFinite(value.Value))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"ensemble item {index} lacks a finite numeric 'value'.");
            members.Add((member, scenario, value.Value));
        }

        if (members.Count < 2)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "an ensemble needs at least two member values.");

        var weights = D118Values.AsObject(ToolArg(context, "weights"));
        Dictionary<string, double>? weightMap = null;
        if (weights is { Count: > 0 })
        {
            var observed = weights.ToDictionary(k => k.Key, k => D118Values.AsDouble(k.Value) ?? double.NaN, StringComparer.Ordinal);
            if (observed.Values.Any(v => !double.IsFinite(v) || v < 0))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "weights must be non-negative finite numbers keyed by member id.");
            var sum = observed.Values.Sum();
            if (Math.Abs(sum - 1.0) > 1e-6)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"weights must sum to 1 (observed {sum.ToString("G17", CultureInfo.InvariantCulture)}); no renormalisation is silently applied.");
            var unknown = observed.Keys.Where(k => !members.Any(m => string.Equals(m.Member, k, StringComparison.Ordinal))).ToList();
            if (unknown.Count > 0)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"weights reference unknown ensemble members [{string.Join(", ", unknown)}].");
            weightMap = observed;
        }

        var groups = members.GroupBy(m => m.Scenario).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
        {
            var values = g.Select(m => m.Value).ToList();
            var mean = D118Stats.Mean(values);
            var sd = D118Stats.SampleSd(values) ?? 0;
            var median = Median(values);
            var dispersion = Math.Abs(mean) < 1e-12 ? (double?)null : sd / Math.Abs(mean);
            return (IDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["scenario"] = g.Key,
                ["memberCount"] = values.Count,
                ["mean"] = mean,
                ["median"] = median,
                ["min"] = values.Min(),
                ["max"] = values.Max(),
                ["range"] = values.Max() - values.Min(),
                ["sampleSd"] = sd,
                ["relativeDispersion"] = dispersion,
                ["dispersionFormula"] = "sampleSd / abs(mean)",
                ["exceedsThreshold"] = dispersion is not null && dispersion.Value > threshold,
                ["outliersBeyondThreshold"] = values.Where(v => Math.Abs(v - median) > threshold * Math.Max(1e-12, Math.Abs(median))).Count(),
                ["weightedMean"] = weightMap is null ? null : g.Sum(m => m.Value * weightMap[m.Member]),
            };
        }).ToList();

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tool"] = ToolKey,
            ["schemaSha256"] = D118SchemaFace.SchemaSha256(ToolKey),
            ["memberCount"] = members.Select(m => m.Member).Distinct(StringComparer.Ordinal).Count(),
            ["valueCount"] = members.Count,
            ["disagreementThreshold"] = threshold,
            ["weightsDeclared"] = weightMap,
            ["groups"] = groups,
            ["verification"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["computed"] = "means, medians, spread and the declared relative-dispersion formula over the provided member values",
                ["notComputed"] = new[] { "member skill weighting", "physical plausibility of members", "scenario generation" },
                ["sideEffects"] = "one JSON report file at outputPath",
            },
        };

        var full = D118Outputs.OverwriteGate(outputPath, ToolArgs.GetBool(context, "overwrite"));
        D118Outputs.WriteJson(full, payload);
        return OperationResult<object?>.Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["outputPath"] = full,
            ["report"] = payload,
        }, $"Ensemble disagreement report written for {members.Count} member value(s) across {groups.Count} scenario group(s).");
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    private static object? Read(IReadOnlyDictionary<string, object?> obj, string key) => obj.TryGetValue(key, out var v) ? v : null;
}
