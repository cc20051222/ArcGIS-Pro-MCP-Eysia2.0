using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

// ═══════════════════════════════════════════════════════════════════════════════
// D-096 · M2 批四：P6 栅格族六件工具（1R＋5W；197→203）
// 冻结契约：.runtime/evolution/v5-f/run-20260928-d082/f03b-5-schemas/
// 实现先例：D094AnalysisTools.cs（W 层 GP 守卫）＋D088QualityTools.cs（R 层 Python Bridge 委托）
// 范围勘误（G-216）：raster_reclassify／extract_raster_values 系内部服务，本批零注册零实现。
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>D-096 P6：栅格像元采样检查（R；Python Bridge 数据通道）。</summary>
public sealed class RasterPixelInspectTool : McpToolBase
{
    public override string Name => "raster_pixel_inspect";
    public override string Description => "按点位采样栅格像元值（可选波段子集与邻域像元）；只读，经 Python Bridge 数据通道读取栅格元数据。";
    protected override string CategoryName => ToolCategories.Quality;
    public override IReadOnlyDictionary<string, object?> InputSchema => D096Schemas.RasterPixelInspect;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Python is null)
            return OperationResult<object?>.Fail(ErrorCodes.PythonBridgeUnavailable, "Python Bridge service is not available; raster_pixel_inspect requires the bridge channel for raster pixel sampling.");
        if (context.Host?.D096Raster is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-096 raster service is unavailable.");

        var raster = ToolArgs.GetString(context, "raster");
        if (string.IsNullOrWhiteSpace(raster))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "raster is required.");

        var points = ToolArgs.GetList(context, "points");
        if (points.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "points must be a non-empty array.");

        var bands = ToolArgs.GetList(context, "bands");
        var includeNeighborhood = ToolArgs.GetBool(context, "includeNeighborhood", false);

        var result = await service.InspectRasterPixelsAsync(raster!, points, bands, includeNeighborhood, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-096 P6：构建栅格金字塔（W；in-place-raster，原位变更输入栅格，无 outputPath/overwrite）。</summary>
public sealed class BuildRasterPyramidsTool : McpToolBase
{
    public override string Name => "build_raster_pyramids";
    public override string Description => "为输入栅格原位构建金字塔（in-place）；levels=0 表示工具自定并回显 resolvedLevels。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D096Schemas.BuildRasterPyramids;

    private static readonly HashSet<string> Resampling = new(StringComparer.Ordinal)
    { "NEAREST", "BILINEAR", "CUBIC", "MAJORITY" };

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D096Raster is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-096 raster service is unavailable.");

        var input = ToolArgs.GetString(context, "input");
        if (string.IsNullOrWhiteSpace(input))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "input is required.");

        var resampling = ToolArgs.GetString(context, "resampling") ?? "NEAREST";
        if (!Resampling.Contains(resampling))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "resampling must be NEAREST, BILINEAR, CUBIC or MAJORITY.");

        var levels = ToolArgs.GetInt(context, "levels") ?? 0;
        if (levels < 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "levels must be non-negative (0 = tool-determined).");

        var skipFirst = ToolArgs.GetBool(context, "skipFirst", false);

        var result = await service.BuildRasterPyramidsAsync(input!, resampling, levels, skipFirst, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-096 P6：合成多波段栅格（W）。</summary>
public sealed class ComposeRasterBandsTool : McpToolBase
{
    public override string Name => "compose_raster_bands";
    public override string Description => "按目标波段顺序将多个输入栅格堆叠为新多波段栅格；波段顺序与位深须回显。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D096Schemas.ComposeRasterBands;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D096Raster is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-096 raster service is unavailable.");

        var inputs = ToolArgs.GetList(context, "inputs");
        if (inputs.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputs must be a non-empty array of raster paths.");

        var outputPath = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(outputPath))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "outputPath is required.");

        var bandOrder = ToolArgs.GetList(context, "bandOrder");
        var outputPixelType = ToolArgs.GetString(context, "outputPixelType");
        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.ComposeRasterBandsAsync(inputs, outputPath!, bandOrder, outputPixelType, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-096 P6：栅格变化检测（W；固定方法目录，不接受任意 Con 表达式）。</summary>
public sealed class RasterChangeDetectionTool : McpToolBase
{
    public override string Name => "raster_change_detection";
    public override string Description => "对两期栅格执行变化检测（difference/ratio/normalized_difference/categorical_transition）；方法须显式，不接受任意 Con 表达式。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D096Schemas.RasterChangeDetection;

    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
    { "difference", "ratio", "normalized_difference", "categorical_transition" };

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D096Raster is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-096 raster service is unavailable.");

        var before = ToolArgs.GetString(context, "before");
        var afterRaster = ToolArgs.GetString(context, "afterRaster");
        var outputPath = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(before) || string.IsNullOrWhiteSpace(afterRaster) || string.IsNullOrWhiteSpace(outputPath))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "before, afterRaster and outputPath are all required.");

        var method = ToolArgs.GetString(context, "method");
        if (method is null || !Methods.Contains(method))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "method must be difference, ratio, normalized_difference or categorical_transition.");

        var threshold = ToolArgs.GetDouble(context, "threshold") ?? 0.0;
        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.RasterChangeDetectionAsync(before!, afterRaster!, outputPath!, method!, threshold, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-096 P6：栅格重投影（W）。</summary>
public sealed class RasterReprojectTool : McpToolBase
{
    public override string Name => "raster_reproject";
    public override string Description => "将输入栅格重投影到目标 CRS；像元大小与地理变换省略时由服务解析并回显。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D096Schemas.RasterReproject;

    private static readonly HashSet<string> Resampling = new(StringComparer.Ordinal)
    { "NEAREST", "BILINEAR", "CUBIC", "MAJORITY" };

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D096Raster is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-096 raster service is unavailable.");

        var input = ToolArgs.GetString(context, "input");
        var outputPath = ToolArgs.GetString(context, "outputPath");
        var targetCrs = ToolArgs.GetString(context, "targetCrs");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(targetCrs))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "input, outputPath and targetCrs are all required.");

        var resampling = ToolArgs.GetString(context, "resampling") ?? "NEAREST";
        if (!Resampling.Contains(resampling))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "resampling must be NEAREST, BILINEAR, CUBIC or MAJORITY.");

        var cellSize = ToolArgs.GetDouble(context, "cellSize");
        var transformation = ToolArgs.GetString(context, "transformation");
        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.RasterReprojectAsync(input!, outputPath!, targetCrs!, resampling, cellSize, transformation, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-096 P6：分区直方图（W；类别×分区频数，与连续统计不等价）。</summary>
public sealed class ZonalHistogramTool : McpToolBase
{
    public override string Name => "zonal_histogram";
    public override string Description => "计算各分区内取值栅格的类别频数直方图；binWidth=0 表示按类别值精确分组。";
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D096Schemas.ZonalHistogram;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D096Raster is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-096 raster service is unavailable.");

        var zones = ToolArgs.GetString(context, "zones");
        var values = ToolArgs.GetString(context, "values");
        var outputPath = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(zones) || string.IsNullOrWhiteSpace(values) || string.IsNullOrWhiteSpace(outputPath))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "zones, values and outputPath are all required.");

        var zoneField = ToolArgs.GetString(context, "zoneField");
        var binWidth = ToolArgs.GetDouble(context, "binWidth") ?? 0.0;
        if (binWidth < 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "binWidth must be non-negative (0 = exact categorical grouping).");

        var overwrite = ToolArgs.GetBool(context, "overwrite", false);

        var result = await service.ZonalHistogramAsync(zones!, zoneField, values!, outputPath!, binWidth, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// D096Schemas：冻结契约在 D-082 f03b-5-schemas 中；此处只将其映射为 MCP InputSchema。
// 输入面逐字对齐（props＋required＋additionalProperties=false）。
// ═══════════════════════════════════════════════════════════════════════════════

internal static class D096Schemas
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

    private static Dictionary<string, object?> Arr(string description, bool emptyDefault = false)
    {
        var d = new Dictionary<string, object?> { ["type"] = "array", ["description"] = description };
        if (emptyDefault) d["default"] = Array.Empty<object?>();
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

    internal static readonly IReadOnlyDictionary<string, object?> RasterPixelInspect = Obj(P(
        ("raster", S("string", "栅格路径。")),
        ("points", Arr("点位清单 [{x,y}]（栅格 CRS 或显式带 sr）。")),
        ("bands", Arr("波段子集（空＝全部）。", emptyDefault: true)),
        ("includeNeighborhood", S("boolean", "是否返回邻域像元。", false))),
        "raster", "points");

    internal static readonly IReadOnlyDictionary<string, object?> BuildRasterPyramids = Obj(P(
        ("input", S("string", "输入栅格（须为可写格式）。")),
        ("resampling", S("string", "重采样方法（同上枚举）。", "NEAREST")),
        ("levels", S("integer", "层级数（0＝工具自定，须回显 resolvedLevels）。", 0)),
        ("skipFirst", S("boolean", "是否跳过第一层。", false))),
        "input");

    internal static readonly IReadOnlyDictionary<string, object?> ComposeRasterBands = Obj(P(
        ("inputs", Arr("输入栅格清单（按目标波段顺序给出）。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("bandOrder", Arr("显式波段顺序（省略 ⇒ 按 inputs 顺序，须回显）。", emptyDefault: true)),
        ("outputPixelType", S("string", "输出像元类型（省略 ⇒ 取最大覆盖面类型并回显）。")),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "inputs", "outputPath");

    internal static readonly IReadOnlyDictionary<string, object?> RasterChangeDetection = Obj(P(
        ("before", S("string", "前期栅格。")),
        ("afterRaster", S("string", "后期栅格。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("method", S("string", "变化方法（固定目录，不接受任意 Con 表达式）。", values: new[] { "difference", "ratio", "normalized_difference", "categorical_transition" })),
        ("threshold", S("number", "变化阈值（0＝不阈值，须回显）。", 0.0)),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "before", "afterRaster", "outputPath", "method");

    internal static readonly IReadOnlyDictionary<string, object?> RasterReproject = Obj(P(
        ("input", S("string", "输入栅格。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("targetCrs", S("string", "目标 CRS。")),
        ("resampling", S("string", "重采样方法。", "NEAREST", values: new[] { "NEAREST", "BILINEAR", "CUBIC", "MAJORITY" })),
        ("cellSize", S("number", "目标像元大小（省略 ⇒ 由 CRS 推得并回显）。")),
        ("transformation", S("string", "地理变换名（省略 ⇒ 服务解析并回显）。")),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "input", "outputPath", "targetCrs");

    internal static readonly IReadOnlyDictionary<string, object?> ZonalHistogram = Obj(P(
        ("zones", S("string", "分区要素或栅格。")),
        ("zoneField", S("string", "分区字段（矢量分区时必填）。")),
        ("values", S("string", "取值栅格。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("binWidth", S("number", "直方图箱宽（0＝按类别值精确分组）。", 0.0)),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "zones", "values", "outputPath");
}
