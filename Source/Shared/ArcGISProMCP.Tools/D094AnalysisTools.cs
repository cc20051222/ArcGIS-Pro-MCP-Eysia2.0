using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

// ═══════════════════════════════════════════════════════════════════════════════
// D-094 · M2 批三：P5 八件分析工具（全 W；189→197）
// 冻结契约：.runtime/evolution/v5-f/run-20260928-d082/f03b-5-schemas/
// 实现先例：D092DomainTools.cs＋D088QualityTools.cs＋D086DesignTools.cs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>D-094 P5：简化要素（W）。</summary>
public sealed class SimplifyFeaturesTool : McpToolBase
{
    public override string Name => "simplify_features";
    public override string Description => "对线/面要素执行几何简化（POINT_REMOVE/BEND_SIMPLIFY/WEIGHTED_AREA）；容差须带单位，不静默换算。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D094Schemas.SimplifyFeatures;

    private static readonly HashSet<string> Algorithms = new(StringComparer.Ordinal)
    { "POINT_REMOVE", "BEND_SIMPLIFY", "WEIGHTED_AREA" };

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D094Analysis is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-094 analysis service is unavailable.");

        var input = ToolArgs.GetString(context, "input");
        var outputPath = ToolArgs.GetString(context, "outputPath");
        var algorithm = ToolArgs.GetString(context, "algorithm");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(outputPath))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "input and outputPath are required.");
        if (algorithm is null || !Algorithms.Contains(algorithm))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "algorithm must be POINT_REMOVE, BEND_SIMPLIFY or WEIGHTED_AREA.");

        var tolerance = ToolArgs.GetDouble(context, "tolerance");
        if (tolerance is null)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "tolerance is required.");

        var preserveTopology = ToolArgs.GetBool(context, "preserveTopology", true);
        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.SimplifyFeaturesAsync(input!, outputPath!, algorithm!, tolerance.Value, preserveTopology, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-094 P5：平滑要素（W）。</summary>
public sealed class SmoothFeaturesTool : McpToolBase
{
    public override string Name => "smooth_features";
    public override string Description => "对线/面要素执行几何平滑（PAEK/BEZIER）；容差须带单位，不静默换算。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D094Schemas.SmoothFeatures;

    private static readonly HashSet<string> Algorithms = new(StringComparer.Ordinal)
    { "PAEK", "BEZIER" };

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D094Analysis is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-094 analysis service is unavailable.");

        var input = ToolArgs.GetString(context, "input");
        var outputPath = ToolArgs.GetString(context, "outputPath");
        var algorithm = ToolArgs.GetString(context, "algorithm");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(outputPath))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "input and outputPath are required.");
        if (algorithm is null || !Algorithms.Contains(algorithm))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "algorithm must be PAEK or BEZIER.");

        var tolerance = ToolArgs.GetDouble(context, "tolerance");
        if (tolerance is null)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "tolerance is required.");

        var preserveEndpoints = ToolArgs.GetBool(context, "preserveEndpoints", true);
        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.SmoothFeaturesAsync(input!, outputPath!, algorithm!, tolerance.Value, preserveEndpoints, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-094 P5：面邻接分析（W；Native 执行）。</summary>
public sealed class PolygonNeighborsTool : McpToolBase
{
    public override string Name => "polygon_neighbors";
    public override string Description => "计算面要素间的邻接关系（公共边/点接触）；输出邻接表。";
    protected override string CategoryName => ToolCategories.Analysis;
    public override IReadOnlyDictionary<string, object?> InputSchema => D094Schemas.PolygonNeighbors;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D094Analysis is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-094 analysis service is unavailable.");

        var input = ToolArgs.GetString(context, "input");
        var outputPath = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(outputPath))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "input and outputPath are required.");

        var includeEdgeLength = ToolArgs.GetBool(context, "includeEdgeLength", true);
        var includePointTouches = ToolArgs.GetBool(context, "includePointTouches", false);
        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.PolygonNeighborsAsync(input!, outputPath!, includeEdgeLength, includePointTouches, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-094 P5：生成镶嵌网格（W）。</summary>
public sealed class GenerateTessellationTool : McpToolBase
{
    public override string Name => "generate_tessellation";
    public override string Description => "在指定范围内生成规则镶嵌网格（SQUARE/HEXAGON/TRANSVERSE_HEXAGON/DIAMOND/TRIANGLE）。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D094Schemas.GenerateTessellation;

    private static readonly HashSet<string> ShapeTypes = new(StringComparer.Ordinal)
    { "SQUARE", "HEXAGON", "TRANSVERSE_HEXAGON", "DIAMOND", "TRIANGLE" };

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D094Analysis is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-094 analysis service is unavailable.");

        var extent = ToolArgs.GetObject(context, "extent");
        var outputPath = ToolArgs.GetString(context, "outputPath");
        var shapeType = ToolArgs.GetString(context, "shapeType");
        if (extent is null)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "extent is required.");
        if (string.IsNullOrWhiteSpace(outputPath))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "outputPath is required.");
        if (shapeType is null || !ShapeTypes.Contains(shapeType))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "shapeType must be SQUARE, HEXAGON, TRANSVERSE_HEXAGON, DIAMOND or TRIANGLE.");

        var size = ToolArgs.GetDouble(context, "size");
        if (size is null)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "size is required.");

        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.GenerateTessellationAsync(extent, outputPath!, shapeType!, size.Value, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-094 P5：计算服务区（W；网络分析族）。</summary>
public sealed class CalculateServiceAreasTool : McpToolBase
{
    public override string Name => "calculate_service_areas";
    public override string Description => "基于路网与设施点计算服务区（等时圈/等距圈）；阻抗须显式指定，不默认。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D094Schemas.CalculateServiceAreas;

    private static readonly HashSet<string> TravelDirections = new(StringComparer.Ordinal)
    { "toward_facility", "away_from_facility" };

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D094Analysis is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-094 analysis service is unavailable.");

        var network = ToolArgs.GetString(context, "network");
        var facilities = ToolArgs.GetString(context, "facilities");
        var outputPath = ToolArgs.GetString(context, "outputPath");
        var impedance = ToolArgs.GetString(context, "impedance");
        if (string.IsNullOrWhiteSpace(network) || string.IsNullOrWhiteSpace(facilities)
            || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(impedance))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "network, facilities, outputPath and impedance are all required.");

        var breaks = ToolArgs.GetList(context, "breaks");
        if (breaks.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "breaks must be a non-empty array.");

        var travelDirection = ToolArgs.GetString(context, "travelDirection") ?? "toward_facility";
        if (!TravelDirections.Contains(travelDirection))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "travelDirection must be toward_facility or away_from_facility.");

        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.CalculateServiceAreasAsync(network!, facilities!, outputPath!, breaks, impedance!, travelDirection, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-094 P5：求解路线（W；网络分析族）。</summary>
public sealed class SolveRoutesTool : McpToolBase
{
    public override string Name => "solve_routes";
    public override string Description => "基于路网与停靠点求解最优路线；阻抗须显式指定，不默认。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D094Schemas.SolveRoutes;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D094Analysis is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-094 analysis service is unavailable.");

        var network = ToolArgs.GetString(context, "network");
        var stops = ToolArgs.GetString(context, "stops");
        var outputPath = ToolArgs.GetString(context, "outputPath");
        var impedance = ToolArgs.GetString(context, "impedance");
        if (string.IsNullOrWhiteSpace(network) || string.IsNullOrWhiteSpace(stops)
            || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(impedance))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "network, stops, outputPath and impedance are all required.");

        var findBestOrder = ToolArgs.GetBool(context, "findBestOrder", false);
        var preserveFirstLast = ToolArgs.GetBool(context, "preserveFirstLast", true);
        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.SolveRoutesAsync(network!, stops!, outputPath!, impedance!, findBestOrder, preserveFirstLast, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-094 P5：空间自相关分析（W）。</summary>
public sealed class SpatialAutocorrelationTool : McpToolBase
{
    public override string Name => "spatial_autocorrelation";
    public override string Description => "计算全局/局部空间自相关（Moran's I / LISA）；概念化与标准化须显式。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D094Schemas.SpatialAutocorrelation;

    private static readonly HashSet<string> Conceptualizations = new(StringComparer.Ordinal)
    { "INVERSE_DISTANCE", "INVERSE_DISTANCE_SQUARED", "FIXED_DISTANCE_BAND", "K_NEAREST_NEIGHBORS", "CONTIGUITY_EDGES_ONLY", "CONTIGUITY_EDGES_CORNERS" };

    private static readonly HashSet<string> Standardizations = new(StringComparer.Ordinal)
    { "ROW", "NONE" };

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D094Analysis is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-094 analysis service is unavailable.");

        var input = ToolArgs.GetString(context, "input");
        var outputPath = ToolArgs.GetString(context, "outputPath");
        var field = ToolArgs.GetString(context, "field");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(field))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "input, outputPath and field are all required.");

        var conceptualization = ToolArgs.GetString(context, "conceptualization") ?? "INVERSE_DISTANCE";
        if (!Conceptualizations.Contains(conceptualization))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "conceptualization is not a valid value.");

        var standardization = ToolArgs.GetString(context, "standardization") ?? "ROW";
        if (!Standardizations.Contains(standardization))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "standardization must be ROW or NONE.");

        var local = ToolArgs.GetBool(context, "local", true);
        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.SpatialAutocorrelationAsync(input!, outputPath!, field!, conceptualization, standardization, local, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-094 P5：热点分析（W）。</summary>
public sealed class HotspotAnalysisTool : McpToolBase
{
    public override string Name => "hotspot_analysis";
    public override string Description => "执行热点分析（Getis-Ord Gi*）；FDR 多重检验校正默认启用（政策留证）。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D094Schemas.HotspotAnalysis;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D094Analysis is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-094 analysis service is unavailable.");

        var input = ToolArgs.GetString(context, "input");
        var outputPath = ToolArgs.GetString(context, "outputPath");
        var field = ToolArgs.GetString(context, "field");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(field))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "input, outputPath and field are all required.");

        var conceptualization = ToolArgs.GetString(context, "conceptualization") ?? "FIXED_DISTANCE_BAND";
        var falseDiscoveryRate = ToolArgs.GetBool(context, "falseDiscoveryRate", true);
        var local = ToolArgs.GetBool(context, "local", true);
        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.HotspotAnalysisAsync(input!, outputPath!, field!, conceptualization, falseDiscoveryRate, local, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// D094Schemas：冻结契约在 D-082 f03b-5-schemas 中；此处只将其映射为 MCP InputSchema。
// ═══════════════════════════════════════════════════════════════════════════════

internal static class D094Schemas
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

    internal static readonly IReadOnlyDictionary<string, object?> SimplifyFeatures = Obj(P(
        ("input", S("string", "输入要素。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("algorithm", S("string", "简化算法。", values: new[] { "POINT_REMOVE", "BEND_SIMPLIFY", "WEIGHTED_AREA" })),
        ("tolerance", S("number", "容差（须带单位；不静默换算）。")),
        ("preserveTopology", S("boolean", "是否保持拓扑（避免自相交）。", true)),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "input", "outputPath", "algorithm", "tolerance");

    internal static readonly IReadOnlyDictionary<string, object?> SmoothFeatures = Obj(P(
        ("input", S("string", "输入要素。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("algorithm", S("string", "平滑算法。", values: new[] { "PAEK", "BEZIER" })),
        ("tolerance", S("number", "容差。")),
        ("preserveEndpoints", S("boolean", "是否保持端点（避免整体漂移）。", true)),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "input", "outputPath", "algorithm", "tolerance");

    internal static readonly IReadOnlyDictionary<string, object?> PolygonNeighbors = Obj(P(
        ("input", S("string", "面要素。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("includeEdgeLength", S("boolean", "是否输出公共边长度。", true)),
        ("includePointTouches", S("boolean", "是否把点接触也算邻接（缺省 false，须显式）。", false)),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "input", "outputPath");

    internal static readonly IReadOnlyDictionary<string, object?> GenerateTessellation = Obj(P(
        ("extent", S("object", "生成范围（xmin/ymin/xmax/ymax + sr）。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("shapeType", S("string", "单元形状。", values: new[] { "SQUARE", "HEXAGON", "TRANSVERSE_HEXAGON", "DIAMOND", "TRIANGLE" })),
        ("size", S("number", "单元尺寸（须带单位）。")),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "extent", "outputPath", "shapeType", "size");

    internal static readonly IReadOnlyDictionary<string, object?> CalculateServiceAreas = Obj(P(
        ("network", S("string", "路网数据集。")),
        ("facilities", S("string", "设施点。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("breaks", new Dictionary<string, object?> { ["type"] = "array", ["description"] = "中断值清单（时间/距离，须带单位与阻抗）。" }),
        ("impedance", S("string", "阻抗属性名（显式，不默认）。")),
        ("travelDirection", S("string", "行驶方向。", "toward_facility", values: new[] { "toward_facility", "away_from_facility" })),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "network", "facilities", "outputPath", "breaks", "impedance");

    internal static readonly IReadOnlyDictionary<string, object?> SolveRoutes = Obj(P(
        ("network", S("string", "路网数据集。")),
        ("stops", S("string", "停靠点。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("impedance", S("string", "阻抗属性名。")),
        ("findBestOrder", S("boolean", "是否求解最佳顺序。", false)),
        ("preserveFirstLast", S("boolean", "是否固定首末点。", true)),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "network", "stops", "outputPath", "impedance");

    internal static readonly IReadOnlyDictionary<string, object?> SpatialAutocorrelation = Obj(P(
        ("input", S("string", "要素类或图层。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("field", S("string", "分析字段（数值）。")),
        ("conceptualization", S("string", "空间关系概念化。", "INVERSE_DISTANCE", values: new[] { "INVERSE_DISTANCE", "INVERSE_DISTANCE_SQUARED", "FIXED_DISTANCE_BAND", "K_NEAREST_NEIGHBORS", "CONTIGUITY_EDGES_ONLY", "CONTIGUITY_EDGES_CORNERS" })),
        ("standardization", S("string", "行标准化。", "ROW", values: new[] { "ROW", "NONE" })),
        ("local", S("boolean", "是否同时输出局部指标（LISA）。", true)),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "input", "outputPath", "field");

    internal static readonly IReadOnlyDictionary<string, object?> HotspotAnalysis = Obj(P(
        ("input", S("string", "要素类或图层。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("field", S("string", "分析字段。")),
        ("conceptualization", S("string", "空间关系概念化。", "FIXED_DISTANCE_BAND")),
        ("falseDiscoveryRate", S("boolean", "是否启用 FDR 多重检验校正（政策留证）。", true)),
        ("local", S("boolean", "是否输出局部统计（Gi* / Gi_Bin）。", true)),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "input", "outputPath", "field");
}
