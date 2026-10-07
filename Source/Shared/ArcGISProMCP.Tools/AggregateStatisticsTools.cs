using System.Text.RegularExpressions;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// D-060（Phase 15 功能批 · B 项，G-160 已批准）：统计聚合 2 工具（78 → 80）。
/// ★ 参数序与可用性依据 = D-052 spike 实测（`e-spike-aggregates.json`）—— 不按文档臆断（O-D048-07 教训）：
///   · CellStatistics：`arcpy.gp.CellStatistics_sa(in;in, out, 'MEAN', 'DATA')` 实测 OK，输出 MEAN = 2.5（输入 2 与 3）；
///   · FocalStatistics：sa 形态 3×3 邻域 MEAN 实测 OK（输出 MEAN = 2）。
/// 纪律：沿用既有 <see cref="GpOverwriteGuard"/> 覆写前置闸门 ＋ D-052 守卫统一接入
/// （O-D049-08：输出型工具必须 `ProtectedOutputPathGuard.Match(output)`，先于闸门与 GP），错误码**零新增**。
/// </summary>
public static class AggregateStatisticsToolNotes
{
    /// <summary>本批公开 spike 结论（随 Description 披露要点）。</summary>
    public const string SpikeNote =
        "API spike (D-052): CellStatistics_sa MEAN verified live (2 & 3 -> 2.5); " +
        "FocalStatistics 3x3 CELL rectangle MEAN verified live (= 2).";

    /// <summary>统计类型白名单（官方文档面；非法值在工具层拦截，非 schema 层）。</summary>
    public static readonly HashSet<string> AllowedStatistics = new(StringComparer.OrdinalIgnoreCase)
    {
        "MEAN", "MAJORITY", "MAXIMUM", "MINIMUM", "RANGE", "STD", "SUM", "VARIETY", "MEDIAN"
    };

    /// <summary>邻域形态白名单（FocalStatistics 的 GP 字符串形态）。</summary>
    public static readonly string[] AllowedNeighborhoodShapes =
    {
        "Rectangle", "Circle", "Annulus", "Wedge", "Irregular"
    };

    /// <summary>
    /// 校验并规范化 neighborhood 字符串。接受形态：`Rectangle w h UNIT` / `Circle r UNIT` /
    /// `Annulus inner outer UNIT` / `Wedge r start end UNIT` / `Irregular &lt;file&gt;`（UNIT = CELL|MAP）。
    /// 规范化 = 形态名首字母大写 + 单位大写 + 空白折叠（GP 对大小写不敏感，但统一形态便于对账）。
    /// <para>★ 实现风险披露（D-052 spike 遗留）：FocalStatistics 的 neighborhood **对象**形态已实测；
    /// **GP 字符串形态**此前未实测，本批以白名单 + 规范化收敛，并纳入 LIVE 代表判据（见 R-D060）。</para>
    /// </summary>
    public static bool TryNormalizeNeighborhood(string? raw, out string normalized)
    {
        normalized = string.Empty;
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return false;
        }

        var parts = Regex.Split(text, @"\s+");
        var shape = parts[0].ToLowerInvariant() switch
        {
            "rectangle" => "Rectangle",
            "circle" => "Circle",
            "annulus" => "Annulus",
            "wedge" => "Wedge",
            "irregular" => "Irregular",
            _ => null
        };
        if (shape is null)
        {
            return false;
        }

        if (shape == "Irregular")
        {
            if (parts.Length < 2)
            {
                return false;
            }
            normalized = "Irregular " + string.Join(" ", parts.Skip(1));
            return true;
        }

        var expected = shape switch
        {
            "Rectangle" => 3,   // w h unit
            "Circle" => 2,      // r unit
            "Annulus" => 3,     // inner outer unit
            "Wedge" => 4,       // r start end unit
            _ => 0
        };
        if (parts.Length != expected + 1)
        {
            return false;
        }

        var unit = parts[^1].ToLowerInvariant() switch
        {
            "cell" => "CELL",
            "map" => "MAP",
            _ => null
        };
        if (unit is null)
        {
            return false;
        }

        var numbers = parts.Skip(1).Take(parts.Length - 2).ToArray();
        if (numbers.Any(n => !double.TryParse(n, System.Globalization.NumberStyles.Float,
                                              System.Globalization.CultureInfo.InvariantCulture, out _)))
        {
            return false;
        }

        normalized = shape + " " + string.Join(" ", numbers) + " " + unit;
        return true;
    }
}

/// <summary>cell_statistics：逐像元多栅格聚合统计（CellStatistics_sa）。</summary>
public sealed class CellStatisticsTool : McpToolBase
{
    public override string Name => "cell_statistics";
    public override string Description =>
        "逐像元聚合统计（CellStatistics_sa）：对**多个输入栅格**按像元位置做统计（如 MEAN/SUM/MAXIMUM），写入 output 新栅格（**输出独立**）。" +
        "参数：inputRasters（**分号分隔的多输入**，如 `r1;r2`）、output、statisticsType（默认 `MEAN`；白名单 " +
        "MEAN/MAJORITY/MAXIMUM/MINIMUM/RANGE/STD/SUM/VARIETY/MEDIAN）、ignoreNoData（`DATA` 默认 / `NODATA`）、overwrite（默认 false）。" +
        "许可：**Spatial Analyst**（D-052 spike 实测本机可用）。" +
        "参数序依据 spike 实测：`CellStatistics_sa(in;in, out, 'MEAN', 'DATA')`，实测输出 MEAN = 2.5（输入 2 与 3）。" +
        "输出请落**文件 GDB**（目录型路径按 GRID 处理，路径不得含空格）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputRasters"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Semicolon-separated input rasters, e.g. r1;r2" },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output raster path (file GDB recommended)." },
            ["statisticsType"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "MEAN (default) | MAJORITY | MAXIMUM | MINIMUM | RANGE | STD | SUM | VARIETY | MEDIAN." },
            ["ignoreNoData"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "DATA (default) | NODATA." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "inputRasters", "output" }
    };
    protected override string CategoryName => ToolCategories.Analysis;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var inputs = ToolArgs.GetString(context, "inputRasters");
        var output = ToolArgs.GetString(context, "output");
        if (string.IsNullOrWhiteSpace(inputs) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputRasters and output are required.");
        }

        var statistics = (ToolArgs.GetString(context, "statisticsType") ?? "MEAN").Trim().ToUpperInvariant();
        if (!AggregateStatisticsToolNotes.AllowedStatistics.Contains(statistics))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "statisticsType must be one of: " +
                string.Join(", ", AggregateStatisticsToolNotes.AllowedStatistics.OrderBy(x => x, StringComparer.Ordinal)) + ".");
        }

        var ignoreNoData = NormalizeIgnoreNoData(ToolArgs.GetString(context, "ignoreNoData"));
        if (ignoreNoData is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "ignoreNoData must be DATA or NODATA.");
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

        var gpInputs = GpMultiValueBuilder.FromSemicolonList(inputs);
        if (gpInputs is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "inputRasters must be a semicolon-separated list of raster paths or layer names (no empty items).");
        }

        // 参数序（spike 实测）：(in_rasters…, out_raster, statistics_type, ignore_nodata)
        var values = new List<string> { gpInputs, output, statistics, ignoreNoData };
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "CellStatistics_sa", Values = values },
            context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }

    internal static string? NormalizeIgnoreNoData(string? raw)
    {
        var v = (raw ?? "DATA").Trim().ToUpperInvariant();
        return v switch { "DATA" => "DATA", "NODATA" => "NODATA", _ => null };
    }
}

/// <summary>focal_statistics：邻域统计（FocalStatistics_sa）。</summary>
public sealed class FocalStatisticsTool : McpToolBase
{
    /// <summary>默认邻域（spike 实测形态：3×3 矩形，像元单位）。</summary>
    public const string DefaultNeighborhood = "Rectangle 3 3 CELL";

    public override string Name => "focal_statistics";
    public override string Description =>
        "邻域统计（FocalStatistics_sa）：对 inputRaster 的每个像元在**邻域**内做统计（如 MEAN），写入 output 新栅格（**输出独立**）。" +
        "参数：inputRaster、output、neighborhood（默认 `Rectangle 3 3 CELL`；形态白名单 Rectangle w h UNIT / Circle r UNIT / " +
        "Annulus inner outer UNIT / Wedge r start end UNIT / Irregular <file>，UNIT = CELL|MAP）、" +
        "statisticsType（默认 `MEAN`；白名单同 cell_statistics）、ignoreNoData（`DATA` 默认 / `NODATA`）、overwrite（默认 false）。" +
        "许可：**Spatial Analyst**（D-052 spike 实测本机可用）。3×3 矩形邻域 MEAN 实测输出 = 2（全 2 栅格）。" +
        "**风险披露（D-052 spike 遗留）**：邻域此前实测为**对象形态**，本批使用 **GP 字符串形态**（白名单 + 规范化收敛），已在 LIVE 判据中实测验证。" +
        "输出请落**文件 GDB**（目录型路径按 GRID 处理，路径不得含空格）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputRaster"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Input raster dataset path." },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output raster path (file GDB recommended)." },
            ["neighborhood"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Neighborhood, default 'Rectangle 3 3 CELL'. Shapes: Rectangle w h UNIT | Circle r UNIT | Annulus inner outer UNIT | Wedge r start end UNIT | Irregular <file>." },
            ["statisticsType"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "MEAN (default) | MAJORITY | MAXIMUM | MINIMUM | RANGE | STD | SUM | VARIETY | MEDIAN." },
            ["ignoreNoData"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "DATA (default) | NODATA." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "inputRaster", "output" }
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
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputRaster and output are required.");
        }

        var rawNeighborhood = ToolArgs.GetString(context, "neighborhood");
        var neighborhood = DefaultNeighborhood;
        if (!string.IsNullOrWhiteSpace(rawNeighborhood)
            && !AggregateStatisticsToolNotes.TryNormalizeNeighborhood(rawNeighborhood, out neighborhood))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "neighborhood must be one of: Rectangle w h UNIT | Circle r UNIT | Annulus inner outer UNIT | " +
                "Wedge r start end UNIT | Irregular <file> (UNIT = CELL or MAP).");
        }

        var statistics = (ToolArgs.GetString(context, "statisticsType") ?? "MEAN").Trim().ToUpperInvariant();
        if (!AggregateStatisticsToolNotes.AllowedStatistics.Contains(statistics))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "statisticsType must be one of: " +
                string.Join(", ", AggregateStatisticsToolNotes.AllowedStatistics.OrderBy(x => x, StringComparer.Ordinal)) + ".");
        }

        var ignoreNoData = CellStatisticsTool.NormalizeIgnoreNoData(ToolArgs.GetString(context, "ignoreNoData"));
        if (ignoreNoData is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "ignoreNoData must be DATA or NODATA.");
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

        // 参数序（spike 实测 + 对象面等价）：(in_raster, out_raster, neighborhood, statistics_type, ignore_nodata)
        var values = new List<string> { input, output, neighborhood, statistics, ignoreNoData };
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "FocalStatistics_sa", Values = values },
            context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}
