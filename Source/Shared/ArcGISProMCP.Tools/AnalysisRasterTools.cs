using System.Globalization;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// D-049（Phase 11 第二批）：Erase/Union + 栅格扩展 4 工具。
/// ★ 参数序全部经 A/B 实测确认（见 run-20260916-d049-analysis-raster/api-spike.md）—— 不按文档臆断（O-D048-07 教训）。
/// 沿用 D-021/F6 覆写前置闸门（<see cref="GpOverwriteGuard"/>）与既有错误码（零新增）。
/// </summary>
public static class AnalysisRasterToolNotes
{
    /// <summary>本批 API spike 结论（随 Description 披露要点）。</summary>
    public const string SpikeNote =
        "API spike (D-049): GP parameter orders A/B verified via arcpy.gp; " +
        "raster_project abandoned (ProjectRaster aborted by environment 3x, O-D049-01).";
}

/// <summary>erase：用擦除要素擦除输入要素（Erase_analysis）。</summary>
public sealed class EraseTool : McpToolBase
{
    public override string Name => "erase";
    public override string Description =>
        "擦除分析（Erase_analysis）：用 eraseFeatures 擦除 input 的重叠部分，写入 output 新要素类（**输出独立，不写回源**）。" +
        "参数：input、eraseFeatures、output（必填）、overwrite（默认 false；已存在且未显式授权 → `OUTPUT_EXISTS`，不执行 GP）。" +
        "输出请落**文件 GDB**（目录型输出会落成 shapefile；栅格类还会受 GRID 空格路径约束）。" +
        "许可：Advanced（D-048 spike 实测可达）。GP 失败 → 既有码包装 + GP 消息透出。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["input"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Input features (dataset path or layer name)." },
            ["eraseFeatures"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Erase features." },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output feature class path (file GDB recommended)." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "input", "eraseFeatures", "output" }
    };
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var input = ToolArgs.GetString(context, "input");
        var erase = ToolArgs.GetString(context, "eraseFeatures");
        var output = ToolArgs.GetString(context, "output");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(erase) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "input, eraseFeatures and output are required.");
        }

        // D-052 守卫统一接入（O-D049-08 全量兑现）：受保护输出路径守卫，先于覆写闸门与 GP。
        var protectedHit = ProtectedOutputPathGuard.Match(output);
        if (protectedHit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"output '{output}' is inside a protected root ('{protectedHit}'); refused before geoprocessing (no artefacts).");
        }

        var gate = await GpOverwriteGuard.EvaluateAsync(context, output, context.CancellationToken).ConfigureAwait(false);
        if (gate.Refusal is not null)
        {
            return gate.Refusal;
        }

        // 参数序 A/B 实测：(in_features, erase_features, out_feature_class, {cluster_tolerance})
        var values = new List<string> { input, erase, output, string.Empty };
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "Erase_analysis", Values = values },
            context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>union：求多个要素类的并集（Union_analysis）。</summary>
public sealed class UnionTool : McpToolBase
{
    public override string Name => "union";
    public override string Description =>
        "并集分析（Union_analysis）：求多个输入要素类的几何并集，写入 output 新要素类（**输出独立，不写回源**）。" +
        "参数：inputs（**分号分隔的多输入**，每项可为数据集路径或图层名，如 `A;B`）、output（必填）；" +
        "joinAttributes（`ALL` 默认 / `NO_FID` / `ONLY_FID`）、gaps（是否生成间隙要素，默认 false）、overwrite（默认 false）。" +
        "输出请落**文件 GDB**。许可：Advanced（D-048 spike 实测可达）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputs"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Semicolon-separated inputs; each item may be a dataset path or a layer name, e.g. A;B" },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output feature class path (file GDB recommended)." },
            ["joinAttributes"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "ALL (default) | NO_FID | ONLY_FID." },
            ["gaps"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Create gap features. Default false." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "inputs", "output" }
    };
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var inputs = ToolArgs.GetString(context, "inputs");
        var output = ToolArgs.GetString(context, "output");
        if (string.IsNullOrWhiteSpace(inputs) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputs and output are required.");
        }

        var joinAttrs = (ToolArgs.GetString(context, "joinAttributes") ?? "ALL").Trim().ToUpperInvariant();
        if (joinAttrs is not ("ALL" or "NO_FID" or "ONLY_FID"))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "joinAttributes must be ALL, NO_FID or ONLY_FID.");
        }

        // D-052 守卫统一接入（O-D049-08 全量兑现）：受保护输出路径守卫，先于覆写闸门与 GP。
        var protectedHit = ProtectedOutputPathGuard.Match(output);
        if (protectedHit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"output '{output}' is inside a protected root ('{protectedHit}'); refused before geoprocessing (no artefacts).");
        }

        var gate = await GpOverwriteGuard.EvaluateAsync(context, output, context.CancellationToken).ConfigureAwait(false);
        if (gate.Refusal is not null)
        {
            return gate.Refusal;
        }

        // 多值参数逐项加引号（GpMultiValueBuilder 先例，F11）
        var gpInputs = GpMultiValueBuilder.FromSemicolonList(inputs);
        if (gpInputs is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "inputs must be a semicolon-separated list of dataset paths or layer names (no empty items).");
        }

        // 参数序 A/B 实测：(in_features, out_feature_class, {join_attributes}, {cluster_tolerance}, {gaps})
        var values = new List<string>
        {
            gpInputs, output, joinAttrs, string.Empty,
            ToolArgs.GetBool(context, "gaps") == true ? "GAPS" : "NO_GAPS"
        };
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "Union_analysis", Values = values },
            context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>raster_resample：按新像元尺寸重采样栅格（Resample_management）。</summary>
public sealed class RasterResampleTool : McpToolBase
{
    public override string Name => "raster_resample";
    public override string Description =>
        "栅格重采样（Resample_management）：按 cellSize 重采样 inputRaster，写入 output 新栅格（**输出独立**）。" +
        "参数：inputRaster、output、cellSize（如 `2`）、resamplingType（默认 `NEAREST`；`NEAREST`/`BILINEAR`/`CUBIC`/`MAJORITY`）、overwrite（默认 false）。" +
        "**方法约束披露（O-D049-02，实测）**：`MAJORITY` **仅支持整型栅格** —— 浮点栅格传 MAJORITY 会由 GP 报 `ERROR 001847 多数重采样不支持浮点数据类型` 并透出消息。" +
        "**输出路径披露（O-D048-06）**：输出请落**文件 GDB**（目录型路径按 GRID 处理，路径不得含空格）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputRaster"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Input raster dataset path." },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output raster path (file GDB recommended)." },
            ["cellSize"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output cell size, e.g. \"2\"." },
            ["resamplingType"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "NEAREST (default) | BILINEAR | CUBIC | MAJORITY." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "inputRaster", "output", "cellSize" }
    };
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var input = ToolArgs.GetString(context, "inputRaster");
        var output = ToolArgs.GetString(context, "output");
        var cellSize = ToolArgs.GetString(context, "cellSize");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(cellSize))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "inputRaster, output and cellSize are required.");
        }

        var method = (ToolArgs.GetString(context, "resamplingType") ?? "NEAREST").Trim().ToUpperInvariant();
        if (method is not ("NEAREST" or "BILINEAR" or "CUBIC" or "MAJORITY"))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "resamplingType must be NEAREST, BILINEAR, CUBIC or MAJORITY.");
        }

        // D-052 守卫统一接入（O-D049-08 全量兑现）：受保护输出路径守卫，先于覆写闸门与 GP。
        var protectedHit = ProtectedOutputPathGuard.Match(output);
        if (protectedHit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"output '{output}' is inside a protected root ('{protectedHit}'); refused before geoprocessing (no artefacts).");
        }

        var gate = await GpOverwriteGuard.EvaluateAsync(context, output, context.CancellationToken).ConfigureAwait(false);
        if (gate.Refusal is not null)
        {
            return gate.Refusal;
        }

        // 参数序 A/B 实测：(in_raster, out_raster, {cell_size}, {resampling_type})
        var values = new List<string> { input, output, cellSize.Trim(), method };
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "Resample_management", Values = values },
            context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>raster_statistics：读取栅格属性（GetRasterProperties_management，**只读、无输出路径**）。</summary>
public sealed class RasterStatisticsTool : McpToolBase
{
    /// <summary>propertyType 白名单（官方文档 + MINIMUM 本机实测；非法值在工具层拦截）。</summary>
    private static readonly HashSet<string> AllowedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "MINIMUM", "MAXIMUM", "MEAN", "STD", "UNIQUEVALUECOUNT",
        "TOP", "LEFT", "RIGHT", "BOTTOM", "CELLSIZEX", "CELLSIZEY",
        "VALUETYPE", "COLUMNCOUNT", "ROWCOUNT", "BANDCOUNT",
        "ANYNODATA", "ALLNODATA"
    };

    public override string Name => "raster_statistics";
    public override string Description =>
        "栅格属性读取（GetRasterProperties_management，**只读**、不产出文件）。" +
        "参数：inputRaster（必填）、propertyType（默认 `MINIMUM`；白名单：MINIMUM/MAXIMUM/MEAN/STD/UNIQUEVALUECOUNT/TOP/LEFT/RIGHT/BOTTOM/" +
        "CELLSIZEX/CELLSIZEY/VALUETYPE/COLUMNCOUNT/ROWCOUNT/BANDCOUNT/ANYNODATA/ALLNODATA）、bandIndex（可选）。" +
        "非法 propertyType → `INVALID_ARGUMENT`（工具层白名单，非 schema 层）。返回值经 GP 透出（字符串形态）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputRaster"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Input raster dataset path." },
            ["propertyType"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Property type, default MINIMUM." },
            ["bandIndex"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional band index." }
        },
        ["required"] = new[] { "inputRaster" }
    };
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var input = ToolArgs.GetString(context, "inputRaster");
        if (string.IsNullOrWhiteSpace(input))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputRaster is required.");
        }

        var property = (ToolArgs.GetString(context, "propertyType") ?? "MINIMUM").Trim();
        if (!AllowedProperties.Contains(property))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "propertyType must be one of: " + string.Join(", ", AllowedProperties.OrderBy(x => x, StringComparer.Ordinal)) + ".");
        }

        // 参数序 A/B 实测：(in_raster, {property_type}, {band_index})；只读工具无输出 → 无覆写闸门
        var values = new List<string> { input, property.ToUpperInvariant(), ToolArgs.GetString(context, "bandIndex") ?? string.Empty };
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "GetRasterProperties_management", Values = values },
            context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}
