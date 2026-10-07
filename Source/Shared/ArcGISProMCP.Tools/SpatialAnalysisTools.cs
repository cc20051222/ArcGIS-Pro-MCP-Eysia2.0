using System.Globalization;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// D-048（Phase 11 第一批 · GP 类首批）：空间分析 3 工具。
/// 均沿用 D-021/F6 覆写前置闸门（<see cref="GpOverwriteGuard"/>）与既有错误码（零新增）；
/// 许可依据见 <c>run-20260915-d048-spatial-analysis/license-spike.md</c>（本机 ArcInfo=Advanced、
/// Spatial/ImageAnalyst 扩展 Available；候选 GP 全部本机实测通过）。
/// </summary>
public static class SpatialAnalysisLicenses
{
    /// <summary>本批许可实测结论（spike 落盘），随工具 Description 披露。</summary>
    public const string SpikeNote =
        "License spike (D-048): machine license = ArcInfo(Advanced); " +
        "Spatial Analyst / Image Analyst extensions = Available (CheckOut verified).";
}

/// <summary>spatial_join：按空间关系把连接要素的属性连接到目标要素，输出新要素类（Analysis）。</summary>
public sealed class SpatialJoinTool : McpToolBase
{
    public override string Name => "spatial_join";
    public override string Description =>
        "空间连接（SpatialJoin_analysis）：按空间关系把 joinFeatures 的属性连接到 targetFeatures，写入 output 新要素类。" +
        "参数：targetFeatures、joinFeatures、output（必填）；joinOperation（JOIN_ONE_TO_ONE 默认 / JOIN_ONE_TO_MANY）、" +
        "joinType（KEEP_ALL 默认 / KEEP_COMMON）、matchOption（默认 INTERSECT）、searchRadius + searchRadiusUnit（可选，" +
        "如 100 + Meters）、overwrite（默认 false；已存在且未显式授权 → OUTPUT_EXISTS，不执行 GP）。" +
        "许可：Pro 全等级可用（官方许可页；本机实测通过）。输出落 run 自有目录；**GP 输出禁入 fixture GDB**（由调用方保证）。零状态：源数据只读。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["targetFeatures"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target features (dataset path or layer name)." },
            ["joinFeatures"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Join features." },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output feature class path." },
            ["joinOperation"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "JOIN_ONE_TO_ONE (default) | JOIN_ONE_TO_MANY." },
            ["joinType"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "KEEP_ALL (default) | KEEP_COMMON." },
            ["matchOption"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Spatial match option, default INTERSECT." },
            ["searchRadius"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Optional search radius (> 0)." },
            ["searchRadiusUnit"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Radius unit, e.g. Meters (default)." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "targetFeatures", "joinFeatures", "output" }
    };
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var target = ToolArgs.GetString(context, "targetFeatures");
        var join = ToolArgs.GetString(context, "joinFeatures");
        var output = ToolArgs.GetString(context, "output");
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(join) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "targetFeatures, joinFeatures and output are required.");
        }

        var joinOperation = (ToolArgs.GetString(context, "joinOperation") ?? "JOIN_ONE_TO_ONE").Trim().ToUpperInvariant();
        if (joinOperation is not ("JOIN_ONE_TO_ONE" or "JOIN_ONE_TO_MANY"))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "joinOperation must be JOIN_ONE_TO_ONE or JOIN_ONE_TO_MANY.");
        }

        var joinType = (ToolArgs.GetString(context, "joinType") ?? "KEEP_ALL").Trim().ToUpperInvariant();
        if (joinType is not ("KEEP_ALL" or "KEEP_COMMON"))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "joinType must be KEEP_ALL or KEEP_COMMON.");
        }

        var radius = ToolArgs.GetDouble(context, "searchRadius");
        if (radius is not null && (double.IsNaN(radius.Value) || radius.Value <= 0))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "searchRadius must be a positive number.");
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

        var matchOption = (ToolArgs.GetString(context, "matchOption") ?? "INTERSECT").Trim();
        var values = new List<string>
        {
            target, join, output, joinOperation, joinType, string.Empty, matchOption
        };
        if (radius is not null)
        {
            var unit = ToolArgs.GetString(context, "searchRadiusUnit") ?? "Meters";
            values.Add(radius.Value.ToString("0.############", CultureInfo.InvariantCulture) + " " + unit);
        }

        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "SpatialJoin_analysis", Values = values },
            context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>near：计算输入要素到最近要素的距离（Near_analysis）。
/// ★ 设计决定：Near 会把 NEAR_* 字段**写回 in_features 所在工作空间** ⇒ 为守住「GP 输出禁入 fixture GDB」红线，
/// 本工具先 CopyFeatures 把输入复制到 run 自有 output，再对**副本**执行 Near —— 源数据保持只读、结果落 output。</summary>
public sealed class NearTool : McpToolBase
{
    public override string Name => "near";
    public override string Description =>
        "近邻分析（Near_analysis）：计算 input 各要素到 nearFeatures 最近要素的距离/位置，结果写入 output 新要素类。" +
        "参数：input、nearFeatures、output（必填）；searchRadius + searchRadiusUnit（可选）、location（是否写 NEAR_X/NEAR_Y，默认 false）、" +
        "angle（是否写 NEAR_ANGLE，默认 false）、method（PLANAR 默认 / GEODESIC）、overwrite（默认 false；已存在且未显式授权 → OUTPUT_EXISTS）。" +
        "**实现披露**：Near 会把 NEAR_* 字段写回 in_features 工作空间 ⇒ 本工具先 CopyFeatures 复制 input → output，再对**副本**执行 Near；" +
        "源数据零改动。若 Copy 成功而 Near 失败，副本会保留（GP 失败不自动删产物，如实披露）。" +
        "许可：Pro 全等级可用（官方许可页 基本/标准/高级 = 是；工单「Advanced 条件」系 ArcMap 时代表述，已按官方许可页 + 本机实测更正）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["input"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Input features (copied to output before Near runs; source stays read-only)." },
            ["nearFeatures"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Near features." },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output feature class path (copy of input + NEAR_* fields)." },
            ["searchRadius"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Optional search radius (> 0)." },
            ["searchRadiusUnit"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Radius unit, e.g. Meters (default)." },
            ["location"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Write NEAR_X/NEAR_Y. Default false." },
            ["angle"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Write NEAR_ANGLE. Default false." },
            ["method"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "PLANAR (default) | GEODESIC." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "input", "nearFeatures", "output" }
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
        var near = ToolArgs.GetString(context, "nearFeatures");
        var output = ToolArgs.GetString(context, "output");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(near) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "input, nearFeatures and output are required.");
        }

        var radius = ToolArgs.GetDouble(context, "searchRadius");
        if (radius is not null && (double.IsNaN(radius.Value) || radius.Value <= 0))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "searchRadius must be a positive number.");
        }

        var method = (ToolArgs.GetString(context, "method") ?? "PLANAR").Trim().ToUpperInvariant();
        if (method is not ("PLANAR" or "GEODESIC"))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "method must be PLANAR or GEODESIC.");
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

        // 1) 复制 input → output（源数据零改动；Near 的 NEAR_* 只落到副本）
        var copy = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "CopyFeatures_management", Values = new List<string> { input, output } },
            context.CancellationToken).ConfigureAwait(false);
        if (!copy.Success)
        {
            return ToolResult.From(copy);
        }

        // 2) 对副本执行 Near
        var radiusText = radius is null
            ? string.Empty
            : radius.Value.ToString("0.############", CultureInfo.InvariantCulture) + " " +
              (ToolArgs.GetString(context, "searchRadiusUnit") ?? "Meters");
        var values = new List<string>
        {
            output, near, radiusText,
            ToolArgs.GetBool(context, "location") == true ? "LOCATION" : "NO_LOCATION",
            ToolArgs.GetBool(context, "angle") == true ? "ANGLE" : "NO_ANGLE",
            method
        };
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "Near_analysis", Values = values },
            context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>raster_clip：按掩膜提取栅格（ExtractByMask_sa，需 Spatial Analyst 扩展）。</summary>
public sealed class RasterClipTool : McpToolBase
{
    public override string Name => "raster_clip";
    public override string Description =>
        "栅格裁剪（ExtractByMask_sa）：按 maskFeatures（要素/栅格掩膜）提取 inputRaster 单元格，写入 output 新栅格。" +
        "参数：inputRaster、maskFeatures、output（必填）；extractionArea（INSIDE 默认 / OUTSIDE）、overwrite（默认 false；" +
        "已存在且未显式授权 → OUTPUT_EXISTS，不执行 GP）。" +
        "**许可披露**：需 **Spatial Analyst 扩展**（spike 实测本机 Available + CheckOut 成功；扩展未启用时 GP 报许可错误并透出）。" +
        "**★ 输出路径披露（O-D048-06 / G-122 裁定①，本机实测）**：output 指向**目录**（非文件 GDB）时会按 **GRID** 格式处理，" +
        "**路径不得含空格**（否则 GP 报 `ERROR 010818 输出路径包含空格`）；**推荐输出到文件 GDB**。" +
        "输出落 run 自有目录；**GP 输出禁入 fixture GDB**。零状态：源栅格只读。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputRaster"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Input raster dataset path." },
            ["maskFeatures"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Mask data (feature class / raster)." },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output raster path." },
            ["extractionArea"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "INSIDE (default) | OUTSIDE." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "inputRaster", "maskFeatures", "output" }
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
        var mask = ToolArgs.GetString(context, "maskFeatures");
        var output = ToolArgs.GetString(context, "output");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(mask) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "inputRaster, maskFeatures and output are required.");
        }

        var area = (ToolArgs.GetString(context, "extractionArea") ?? "INSIDE").Trim().ToUpperInvariant();
        if (area is not ("INSIDE" or "OUTSIDE"))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "extractionArea must be INSIDE or OUTSIDE.");
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

        // ★ O-D048-07 修复（LIVE 发现）：ExtractByMask_sa 的 **GP 工具**参数序为
        //   (in_raster, in_mask_data, **out_raster**, {extraction_area}, {analysis_extent})。
        //   此前误用 arcpy **函数**签名（映射代数函数无 out_raster）→ out_raster 被当作 "INSIDE"
        //   → ERROR 000875 / 010818，工具恒失败。实测（A/B 对照）确认本序可写出产物（6×6 像元）。
        var values = new List<string> { input, mask, output, area, string.Empty };
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "ExtractByMask_sa", Values = values },
            context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}
