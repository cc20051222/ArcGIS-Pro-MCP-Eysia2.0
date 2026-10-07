using System.Security.Cryptography;
using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Jobs;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

// ═══════════════════════════════════════════════════════════════════════════════
// D-088 · M2 批一：P1/P2 质量与溯源只读工具（9 件全 Read 层；170→179）
// 冻结契约：.runtime/evolution/v5-f/run-20260928-d082/f03b-5-schemas/
// 实现先例：D084Tools.cs（只读工具注册/守卫/响应惯例）
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>D-088 P1：几何质量检查（只读）。</summary>
public sealed class ValidateGeometriesTool : McpToolBase
{
    private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };
    public override string Name => "validate_geometries";
    public override string Description => "检查要素类几何质量：空几何、自相交、极短边、重复顶点；不带 reportPath 时仅返回内存结果，带路径时按 W 检查落盘。";
    protected override string CategoryName => ToolCategories.Quality;
    public override IReadOnlyDictionary<string, object?> InputSchema => D088Schemas.ValidateGeometries;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D088Quality is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-088 quality analysis service is unavailable.");

        var dataset = ToolArgs.GetString(context, "dataset");
        if (string.IsNullOrWhiteSpace(dataset))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "dataset is required.");

        var checks = ToolArgs.GetStringList(context, "checks");
        if (checks.Count == 0)
            checks = new[] { "null_geometry", "self_intersection", "short_segment", "duplicate_vertex" };
        var allowedChecks = new HashSet<string>(new[] { "null_geometry", "self_intersection", "short_segment", "duplicate_vertex" }, StringComparer.Ordinal);
        if (checks.Any(c => !allowedChecks.Contains(c)))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "checks contains an unsupported geometry check type.");

        var minSegLen = ToolArgs.GetDouble(context, "minimumSegmentLength") ?? 0.0;
        if (!double.IsFinite(minSegLen) || minSegLen < 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "minimumSegmentLength must be a non-negative finite number.");

        var max = ToolArgs.GetInt(context, "maxFeatures") ?? 1000;
        if (max is < 1 or > 1_000_000)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxFeatures must be between 1 and 1000000.");

        var reportPath = ToolArgs.GetString(context, "reportPath");
        if (reportPath is not null && context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "reportPath is refused while read-only mode is enabled; no file was written.");

        var result = await service.ValidateGeometriesAsync(dataset!, checks, minSegLen, max, context.CancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Data is null) return ToolResult.From(result);

        var report = new Dictionary<string, object?>(result.Data, StringComparer.Ordinal)
        {
            ["toolVersion"] = "1.0",
            ["generatedUtc"] = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["requestedChecks"] = checks,
        };

        if (reportPath is null) return OperationResult<object?>.Ok(report, "Geometry validation completed in memory; no file was written.");
        return D088ReportWriter.WriteReport(report, reportPath, "geometry-validation-report", ReportJson);
    }
}

/// <summary>D-088 P1：拓扑规则检查（只读）。</summary>
public sealed class CheckTopologyRulesTool : McpToolBase
{
    private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };
    public override string Name => "check_topology_rules";
    public override string Description => "对要素类执行拓扑规则检查；不带 reportPath 时仅返回内存结果，带路径时按 W 检查落盘。";
    protected override string CategoryName => ToolCategories.Quality;
    public override IReadOnlyDictionary<string, object?> InputSchema => D088Schemas.CheckTopologyRules;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D088Quality is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-088 quality analysis service is unavailable.");

        var dataset = ToolArgs.GetString(context, "dataset");
        if (string.IsNullOrWhiteSpace(dataset))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "dataset is required.");

        var rules = ToolArgs.GetStringList(context, "rules");
        if (rules.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "rules must be a non-empty array.");

        var clusterTolerance = ToolArgs.GetDouble(context, "clusterTolerance") ?? 0.0;
        if (!double.IsFinite(clusterTolerance) || clusterTolerance < 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "clusterTolerance must be a non-negative finite number.");

        var reportPath = ToolArgs.GetString(context, "reportPath");
        if (reportPath is not null && context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "reportPath is refused while read-only mode is enabled; no file was written.");

        var result = await service.CheckTopologyRulesAsync(dataset!, rules, clusterTolerance, context.CancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Data is null) return ToolResult.From(result);

        var report = new Dictionary<string, object?>(result.Data, StringComparer.Ordinal)
        {
            ["toolVersion"] = "1.0",
            ["generatedUtc"] = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["requestedRules"] = rules,
        };

        if (reportPath is null) return OperationResult<object?>.Ok(report, "Topology check completed in memory; no file was written.");
        return D088ReportWriter.WriteReport(report, reportPath, "topology-check-report", ReportJson);
    }
}

/// <summary>D-088 P1：数据集版本比较（只读）。</summary>
public sealed class CompareDatasetsTool : McpToolBase
{
    private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };
    public override string Name => "compare_datasets";
    public override string Description => "比较两个数据集的属性与几何差异；不带 reportPath 时仅返回内存结果，带路径时按 W 检查落盘。";
    protected override string CategoryName => ToolCategories.Quality;
    public override IReadOnlyDictionary<string, object?> InputSchema => D088Schemas.CompareDatasets;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D088Quality is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-088 quality analysis service is unavailable.");

        var left = ToolArgs.GetString(context, "left");
        var right = ToolArgs.GetString(context, "right");
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "left and right dataset paths are required.");

        var keyFields = ToolArgs.GetStringList(context, "keyFields");
        var compareGeometry = ToolArgs.GetBool(context, "compareGeometry", true);
        var tolerance = ToolArgs.GetDouble(context, "tolerance") ?? 0.001;
        if (!double.IsFinite(tolerance) || tolerance < 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "tolerance must be a non-negative finite number.");

        var max = ToolArgs.GetInt(context, "maxFeatures") ?? 1000;
        if (max is < 1 or > 1_000_000)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxFeatures must be between 1 and 1000000.");

        var reportPath = ToolArgs.GetString(context, "reportPath");
        if (reportPath is not null && context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "reportPath is refused while read-only mode is enabled; no file was written.");

        var result = await service.CompareDatasetsAsync(left!, right!, keyFields, compareGeometry, tolerance, max, context.CancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Data is null) return ToolResult.From(result);

        var report = new Dictionary<string, object?>(result.Data, StringComparer.Ordinal)
        {
            ["toolVersion"] = "1.0",
            ["generatedUtc"] = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["matchBasis"] = keyFields.Count > 0 ? "keyFields" : "oid_order",
        };

        if (reportPath is null) return OperationResult<object?>.Ok(report, "Dataset comparison completed in memory; no file was written.");
        return D088ReportWriter.WriteReport(report, reportPath, "dataset-comparison-report", ReportJson);
    }
}

/// <summary>D-088 P1：schema 比较（只读）。</summary>
public sealed class CompareSchemasTool : McpToolBase
{
    public override string Name => "compare_schemas";
    public override string Description => "比较两个数据集的字段 schema 差异（名称、类型、长度、别名、顺序）。";
    protected override string CategoryName => ToolCategories.Quality;
    public override IReadOnlyDictionary<string, object?> InputSchema => D088Schemas.CompareSchemas;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D088Quality is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-088 quality analysis service is unavailable.");

        var left = ToolArgs.GetString(context, "left");
        var right = ToolArgs.GetString(context, "right");
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "left and right dataset paths are required.");

        var ignoreOrder = ToolArgs.GetBool(context, "ignoreOrder", false);

        var result = await service.CompareSchemasAsync(left!, right!, ignoreOrder, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-088 P2：字段规则校验（只读）。</summary>
public sealed class ValidateFieldConstraintsTool : McpToolBase
{
    private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };
    public override string Name => "validate_field_constraints";
    public override string Description => "按约束清单校验字段值（null/range/domain/subtype/unique/length/literal_pattern）；不带 reportPath 时仅返回内存结果。";
    protected override string CategoryName => ToolCategories.Quality;
    public override IReadOnlyDictionary<string, object?> InputSchema => D088Schemas.ValidateFieldConstraints;

    private static readonly HashSet<string> AllowedKinds = new(StringComparer.Ordinal)
    { "null", "range", "domain", "subtype", "unique", "length", "literal_pattern" };

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D088Quality is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-088 quality analysis service is unavailable.");

        var dataset = ToolArgs.GetString(context, "dataset");
        if (string.IsNullOrWhiteSpace(dataset))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "dataset is required.");

        var constraints = ToolArgs.GetObjectList(context, "constraints");
        if (constraints.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "constraints must be a non-empty array.");

        foreach (var c in constraints)
        {
            if (c is null)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "each constraint must be an object with field and kind.");
            var field = ToolArgs.ReadString(c, "field");
            var kind = ToolArgs.ReadString(c, "kind");
            if (string.IsNullOrWhiteSpace(field))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "each constraint must specify a non-empty 'field'.");
            if (kind is not null && !AllowedKinds.Contains(kind))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"constraint kind '{kind}' is not supported; allowed: null|range|domain|subtype|unique|length|literal_pattern.");
        }

        var max = ToolArgs.GetInt(context, "maxFeatures") ?? 1000;
        if (max is < 1 or > 1_000_000)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxFeatures must be between 1 and 1000000.");

        var reportPath = ToolArgs.GetString(context, "reportPath");
        if (reportPath is not null && context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "reportPath is refused while read-only mode is enabled; no file was written.");

        var constraintDicts = constraints.Where(c => c is not null).Select(c => (IReadOnlyDictionary<string, object?>)c!).ToList();
        var result = await service.ValidateFieldConstraintsAsync(dataset!, constraintDicts, max, context.CancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Data is null) return ToolResult.From(result);

        var report = new Dictionary<string, object?>(result.Data, StringComparer.Ordinal)
        {
            ["toolVersion"] = "1.0",
            ["generatedUtc"] = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        };

        if (reportPath is null) return OperationResult<object?>.Ok(report, "Field constraint validation completed in memory; no file was written.");
        return D088ReportWriter.WriteReport(report, reportPath, "field-constraint-report", ReportJson);
    }
}

/// <summary>D-088 P2：格网对齐检查（只读；Python Bridge 数据通道）。</summary>
public sealed class InspectRasterAlignmentTool : McpToolBase
{
    public override string Name => "inspect_raster_alignment";
    public override string Description => "检查多个栅格的原点、像元大小、范围与 CRS 对齐状态；使用 Python Bridge 数据通道。";
    protected override string CategoryName => ToolCategories.Quality;
    public override IReadOnlyDictionary<string, object?> InputSchema => D088Schemas.InspectRasterAlignment;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Python is null)
            return OperationResult<object?>.Fail(ErrorCodes.PythonBridgeUnavailable, "Python Bridge service is not available; inspect_raster_alignment requires the bridge channel for raster metadata.");

        if (context.Host?.D088Quality is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-088 quality analysis service is unavailable.");

        var rasters = ToolArgs.GetStringList(context, "rasters");
        if (rasters.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "rasters must be a non-empty array of raster paths.");

        var reference = ToolArgs.GetString(context, "reference");
        var snapTolerance = ToolArgs.GetDouble(context, "snapTolerance") ?? 0.0;
        if (!double.IsFinite(snapTolerance) || snapTolerance < 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "snapTolerance must be a non-negative finite number.");

        var result = await service.InspectRasterAlignmentAsync(rasters, reference, snapTolerance, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-088 P2：地理变换列举（只读）。</summary>
public sealed class ListGeographicTransformationsTool : McpToolBase
{
    public override string Name => "list_geographic_transformations";
    public override string Description => "列举源 CRS 到目标 CRS 的可用地理变换，支持范围适用性筛选和分页上限。";
    protected override string CategoryName => ToolCategories.DataManagement;
    public override IReadOnlyDictionary<string, object?> InputSchema => D088Schemas.ListGeographicTransformations;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D088Quality is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-088 quality analysis service is unavailable.");

        var sourceCrs = ToolArgs.GetString(context, "sourceCrs");
        var targetCrs = ToolArgs.GetString(context, "targetCrs");
        if (string.IsNullOrWhiteSpace(sourceCrs) || string.IsNullOrWhiteSpace(targetCrs))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "sourceCrs and targetCrs are required.");

        var extent = ToolArgs.GetObject(context, "extent");
        var maxItems = ToolArgs.GetInt(context, "maxItems") ?? 200;
        if (maxItems is < 1 or > 1000)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxItems must be between 1 and 1000.");

        var result = await service.ListGeographicTransformationsAsync(sourceCrs!, targetCrs!, extent, maxItems, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-088 P2：依赖追踪（只读）。</summary>
public sealed class TraceDatasetDependenciesTool : McpToolBase
{
    public override string Name => "trace_dataset_dependencies";
    public override string Description => "从起点数据集/工程项追踪依赖关系图；只给可核对证据，不补造历史。";
    protected override string CategoryName => ToolCategories.DataManagement;
    public override IReadOnlyDictionary<string, object?> InputSchema => D088Schemas.TraceDatasetDependencies;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D088Quality is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-088 quality analysis service is unavailable.");

        var root = ToolArgs.GetString(context, "root");
        if (string.IsNullOrWhiteSpace(root))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "root is required.");

        var maxDepth = ToolArgs.GetInt(context, "maxDepth") ?? 3;
        if (maxDepth is < 1 or > 6)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxDepth must be between 1 and 6.");

        var includeBroken = ToolArgs.GetBool(context, "includeBroken", true);

        var result = await service.TraceDatasetDependenciesAsync(root!, maxDepth, includeBroken, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-088 P2：来源谱系（只读）。</summary>
public sealed class GetDatasetLineageTool : McpToolBase
{
    public override string Name => "get_dataset_lineage";
    public override string Description => "向上追溯数据集来源谱系（含 ArtifactManifest 产物 hash）；没有证据不补造历史。与 get_dataset_info 划界：本工具聚焦来源链而非当前属性。";
    protected override string CategoryName => ToolCategories.DataManagement;
    public override IReadOnlyDictionary<string, object?> InputSchema => D088Schemas.GetDatasetLineage;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D088Quality is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-088 quality analysis service is unavailable.");

        var dataset = ToolArgs.GetString(context, "dataset");
        if (string.IsNullOrWhiteSpace(dataset))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "dataset is required.");

        var maxDepth = ToolArgs.GetInt(context, "maxDepth") ?? 5;
        if (maxDepth is < 1 or > 10)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxDepth must be between 1 and 10.");

        var includeArtifacts = ToolArgs.GetBool(context, "includeArtifacts", true);

        var result = await service.GetDatasetLineageAsync(dataset!, maxDepth, includeArtifacts, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// 共享 reportPath 写盘助手（复用 D-084 GenerateQualityReport 守卫模式）
// ═══════════════════════════════════════════════════════════════════════════════

internal static class D088ReportWriter
{
    internal static OperationResult<object?> WriteReport(
        Dictionary<string, object?> report, string reportPath, string artifactKind, JsonSerializerOptions jsonOptions)
    {
        if (!Path.IsPathFullyQualified(reportPath))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "reportPath must be an absolute path.");

        string fullPath;
        try { fullPath = Path.GetFullPath(reportPath); }
        catch (Exception ex) { return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "reportPath is invalid.", ex.Message); }

        if (!string.Equals(Path.GetPathRoot(fullPath), @"D:\", StringComparison.OrdinalIgnoreCase))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "G-197: reportPath must be on D:; no file was written.");

        var protectedHit = ProtectedOutputPathGuard.Match(fullPath);
        if (protectedHit is not null)
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, $"reportPath is refused by the path guard ('{protectedHit}').");

        if (WorkflowJobStore.IsTempLikePath(fullPath))
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, "reportPath must not live under %TEMP%.");

        var bytes = JsonSerializer.SerializeToUtf8Bytes(report, jsonOptions);
        try
        {
            if (Directory.Exists(fullPath))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "reportPath names an existing directory.");
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
        }
        catch (IOException ex) when (File.Exists(fullPath))
        {
            return OperationResult<object?>.Fail(ErrorCodes.OutputExists, $"Report file already exists: {fullPath}.", ex.Message);
        }
        catch (Exception ex)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Writing report failed.", ex.Message);
        }

        var manifest = new Dictionary<string, object?>
        {
            ["artifacts"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["path"] = fullPath,
                    ["kind"] = artifactKind,
                    ["bytes"] = bytes.LongLength,
                    ["sha256"] = Convert.ToHexString(SHA256.HashData(bytes)),
                }
            }
        };
        report["reportPath"] = fullPath;
        report["artifactManifest"] = manifest;
        return OperationResult<object?>.Ok(report, "Report file created with OUTPUT_EXISTS protection; ArtifactManifest included.");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// D088Schemas：冻结契约在 D-082 f03b-5-schemas 中；此处只将其映射为 MCP InputSchema。
// 逐字对齐（addProps=false）。
// ═══════════════════════════════════════════════════════════════════════════════

internal static class D088Schemas
{
    private static Dictionary<string, object?> S(string type, string? description = null, object? defaultValue = null,
        object? minimum = null, object? maximum = null, string[]? values = null)
    {
        var d = new Dictionary<string, object?> { ["type"] = type };
        if (description is not null) d["description"] = description;
        if (defaultValue is not null) d["default"] = defaultValue;
        if (minimum is not null) d["minimum"] = minimum;
        if (maximum is not null) d["maximum"] = maximum;
        if (values is not null) d["enum"] = values;
        return d;
    }

    private static IReadOnlyDictionary<string, object?> Obj(Dictionary<string, object?> props, params string[] required)
        => new Dictionary<string, object?>
        {
            ["type"] = "object", ["properties"] = props, ["required"] = required,
            ["additionalProperties"] = false,
        };

    private static Dictionary<string, object?> P(params (string Name, object? Schema)[] fields)
        => fields.ToDictionary(x => x.Name, x => x.Schema, StringComparer.Ordinal);

    private static Dictionary<string, object?> Arr(string itemType, string? description = null, object? defaultValue = null)
    {
        var d = new Dictionary<string, object?>
        {
            ["type"] = "array",
            ["items"] = new Dictionary<string, object?> { ["type"] = itemType },
        };
        if (description is not null) d["description"] = description;
        if (defaultValue is not null) d["default"] = defaultValue;
        return d;
    }

    // ── P1 ──

    internal static readonly IReadOnlyDictionary<string, object?> ValidateGeometries = Obj(P(
        ("dataset", S("string", "要素类路径或图层名。")),
        ("checks", new Dictionary<string, object?>
        {
            ["type"] = "array",
            ["default"] = new[] { "null_geometry", "self_intersection", "short_segment", "duplicate_vertex" },
            ["description"] = "启用的检查目录。",
        }),
        ("minimumSegmentLength", S("number", "极短边阈值（CRS 单位；0＝不检查）。", 0.0)),
        ("maxFeatures", S("integer", "参与计算/采样/返回的要素上限（缺省 1000）。超限不静默：必须给 countBasis + truncated。", 1000, 1, 1000000)),
        ("reportPath", S("string", "可选：结构化报告落盘绝对路径。一旦提供，该工具按 W（写）检查：路径守卫 + OUTPUT_EXISTS 闸门 + 产物进 ArtifactManifest。未提供则只返回内存结果。"))),
        "dataset");

    internal static readonly IReadOnlyDictionary<string, object?> CheckTopologyRules = Obj(P(
        ("dataset", S("string", "要素类路径。")),
        ("rules", Arr("string", "拓扑规则清单。")),
        ("clusterTolerance", S("number", "聚类容差（CRS 单位）。", 0.0)),
        ("reportPath", S("string", "可选：结构化报告落盘绝对路径。一旦提供，该工具按 W（写）检查：路径守卫 + OUTPUT_EXISTS 闸门 + 产物进 ArtifactManifest。未提供则只返回内存结果。"))),
        "dataset", "rules");

    internal static readonly IReadOnlyDictionary<string, object?> CompareDatasets = Obj(P(
        ("left", S("string", "基线数据集。")),
        ("right", S("string", "对比数据集。")),
        ("keyFields", new Dictionary<string, object?>
        {
            ["type"] = "array",
            ["default"] = Array.Empty<string>(),
            ["description"] = "行匹配键字段；空 ⇒ 按 OID 顺序（须回显 matchBasis）。",
        }),
        ("compareGeometry", S("boolean", "是否比较几何（含容差）。", true)),
        ("tolerance", S("number", "几何比较容差。", 0.001)),
        ("maxFeatures", S("integer", "参与计算/采样/返回的要素上限（缺省 1000）。超限不静默：必须给 countBasis + truncated。", 1000, 1, 1000000)),
        ("reportPath", S("string", "可选：结构化报告落盘绝对路径。一旦提供，该工具按 W（写）检查：路径守卫 + OUTPUT_EXISTS 闸门 + 产物进 ArtifactManifest。未提供则只返回内存结果。"))),
        "left", "right");

    internal static readonly IReadOnlyDictionary<string, object?> CompareSchemas = Obj(P(
        ("left", S("string", "基线数据集。")),
        ("right", S("string", "对比数据集。")),
        ("ignoreOrder", S("boolean", "是否忽略字段顺序差异。", false))),
        "left", "right");

    // ── P2 ──

    internal static readonly IReadOnlyDictionary<string, object?> ValidateFieldConstraints = Obj(P(
        ("dataset", S("string", "表或要素类路径。")),
        ("constraints", new Dictionary<string, object?>
        {
            ["type"] = "array",
            ["description"] = "约束清单（field/kind/params）；kind ∈ null|range|domain|subtype|unique|length|literal_pattern。",
        }),
        ("maxFeatures", S("integer", "参与计算/采样/返回的要素上限（缺省 1000）。超限不静默：必须给 countBasis + truncated。", 1000, 1, 1000000)),
        ("reportPath", S("string", "可选：结构化报告落盘绝对路径。一旦提供，该工具按 W（写）检查：路径守卫 + OUTPUT_EXISTS 闸门 + 产物进 ArtifactManifest。未提供则只返回内存结果。"))),
        "dataset", "constraints");

    internal static readonly IReadOnlyDictionary<string, object?> InspectRasterAlignment = Obj(P(
        ("rasters", Arr("string", "参与比较的栅格路径清单（≥1）。")),
        ("reference", S("string", "参考栅格；省略 ⇒ 以第一个为参考并回显。")),
        ("snapTolerance", S("number", "原点/像元容许偏差。", 0.0))),
        "rasters");

    internal static readonly IReadOnlyDictionary<string, object?> ListGeographicTransformations = Obj(P(
        ("sourceCrs", S("string", "源 CRS（WKID/WKT）。")),
        ("targetCrs", S("string", "目标 CRS。")),
        ("extent", S("object", "可选范围（用于适用性筛选）。")),
        ("maxItems", S("integer", "返回条目上限（缺省 200，上限 1000）。超限 ⇒ 截断并在响应中如实披露 truncated/returnedCount/totalMatched。", 200, 1, 1000))),
        "sourceCrs", "targetCrs");

    internal static readonly IReadOnlyDictionary<string, object?> TraceDatasetDependencies = Obj(P(
        ("root", S("string", "起点数据集/工程项路径。")),
        ("maxDepth", S("integer", "遍历深度（缺省 3，上限 6）。", 3)),
        ("includeBroken", S("boolean", "是否包含断链节点。", true))),
        "root");

    internal static readonly IReadOnlyDictionary<string, object?> GetDatasetLineage = Obj(P(
        ("dataset", S("string", "目标数据集路径。")),
        ("maxDepth", S("integer", "向上追溯深度（缺省 5，上限 10）。", 5)),
        ("includeArtifacts", S("boolean", "是否含产物 hash（来自 ArtifactManifest）。", true))),
        "dataset");
}
