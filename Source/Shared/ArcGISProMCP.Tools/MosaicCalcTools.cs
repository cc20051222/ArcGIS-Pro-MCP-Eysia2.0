using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// D-050（Phase 11 第三批 · 收官批）：栅格镶嵌 + 受约束栅格计算 2 工具。
/// ★ 参数序全部经 A/B 实测确认（见 run-20260916-d050-mosaic-calc/api-spike.md）—— 不按文档臆断（O-D048-07 教训）。
/// ★ 本批同时落地 **输出路径守卫**（G-129 裁定：O-D049-08 随车义务 ①「试点实现」）。
/// </summary>
public static class RasterMosaicCalcNotes
{
    /// <summary>本批 API spike 结论（随 Description 披露要点）。</summary>
    public const string SpikeNote =
        "API spike (D-050): MosaicToNewRaster_management / RasterCalculator_sa parameter orders A/B verified via arcpy.gp; " +
        "RCEXEC namespace dumped (Con/SquareRoot present, Min/Max/Sqrt absent); GP expression is arbitrary Python → tool-layer whitelist mandatory.";

    /// <summary>GP 表达式命名空间实测 dump（白名单依据，390 名）。</summary>
    public const string NamespaceEvidence = "expr-namespace.txt";
}

/// <summary>
/// 受保护输出路径守卫（D-050 / O-D049-08 试点；D-055 A2 加固 13.1）：输出落在受保护根之下 →
/// <see cref="ErrorCodes.PathEscapeRejected"/>，**在进入 GP 之前拒绝**（零产物）。受保护根：
/// <list type="number">
/// <item>任意路径段为 <c>TestFixtures</c>（仓库 fixture 目录，含全部受保护 GDB/aprx）；</item>
/// <item>旧仓库根 <c>&lt;盘符&gt;\ArcGIS-Pro-MCP\</c>（只读历史仓库，与现仓库 <c>ArcGIS-Pro-MCP 2.0</c> 严格区分）；</item>
/// <item>环境变量 <c>ARCGIS_PRO_MCP_PROTECTED_ROOTS</c> 追加的根（分号分隔，部署时可扩展）。</item>
/// </list>
/// 接入范围（D-052 起全量兑现 O-D049-08）：除只读 GetRasterProperties 外，全部 GP 写类调用点统一前置本守卫；
/// 就地修改类（add_field/alter_field/calculate_field，无输出数据集）以同判定守**输入数据集**（命中 → 拒绝执行）。
/// **D-055 A2 加固（13.1）**：
/// <list type="bullet">
/// <item><b>reparse point / symlink / junction → 拒绝</b>（不解析穿越）：路径上任一**已存在**祖先含
/// <c>FileAttributes.ReparsePoint</c> 即命中，命中说明为 <c>reparse-point</c>；</item>
/// <item><b>规范化后二次判界</b>：除对原串做段/根判定外，另对 <see cref="Path.GetFullPath(string)"/> 归一化结果
/// 再判一次（前缀与段），消除 <c>..</c> / 相对路径 / 混合分隔符的绕行；</item>
/// <item><b>允许范围白名单（可选）</b>：环境变量 <c>ARCGIS_PRO_MCP_ALLOWED_ROOTS</c> 配置后，
/// 输出必须落在其中之一（否则拒绝）；未配置 → 保持既有行为（不改变契约面）。</item>
/// </list>
/// </summary>
public static class ProtectedOutputPathGuard
{
    /// <summary>追加受保护根的环境变量名（分号分隔）。</summary>
    public const string EnvironmentVariable = "ARCGIS_PRO_MCP_PROTECTED_ROOTS";

    /// <summary>允许范围白名单的环境变量名（分号分隔；D-055 A2 新增，**未配置则不启用**）。</summary>
    public const string AllowedRootsVariable = "ARCGIS_PRO_MCP_ALLOWED_ROOTS";

    /// <summary>reparse point / symlink / junction 的命中说明。</summary>
    public const string ReparseHit = "reparse-point";

    /// <summary>白名单外命中的说明前缀。</summary>
    public const string NotInAllowedRootsHit = "outside-allowed-roots";

    /// <summary>受保护路径段（大小写不敏感）。</summary>
    private const string FixtureSegment = "TestFixtures";

    /// <summary>旧仓库目录名（只读历史仓库；现仓库为 "ArcGIS-Pro-MCP 2.0"，不匹配）。</summary>
    private const string LegacyRepoDirectory = "ArcGIS-Pro-MCP";

    /// <summary>判定输出是否命中受保护路径。命中 → 返回命中说明（用于错误消息）；放行 → null。</summary>
    public static string? Match(string? output, IEnumerable<string>? extraRoots = null)
    {
        // 第一轮：原串归一化判界（历史行为，保持兼容）
        var hit = MatchNormalized(Normalize(output), extraRoots);
        if (hit is not null)
        {
            return hit;
        }

        // 第二轮（D-055 A2）：规范化（full path resolve）后**二次判界**
        var resolved = ResolveFullPath(output);
        if (resolved is not null)
        {
            hit = MatchNormalized(Normalize(resolved), extraRoots);
            if (hit is not null)
            {
                return hit;
            }

            // reparse point / symlink / junction → 拒绝（不解析穿越）
            if (HasReparsePointAncestor(resolved))
            {
                return ReparseHit;
            }

            // 允许范围白名单（仅在配置时启用）
            hit = MatchAllowedRoots(resolved);
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    /// <summary>段/根判定（对已归一化路径）。命中 → 说明；放行 → null。</summary>
    private static string? MatchNormalized(string? normalized, IEnumerable<string>? extraRoots)
    {
        if (normalized is null)
        {
            return null;   // 无法归一路径 → 交由覆写闸门保守处置（不确定即不放行 GP）
        }

        var segments = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => string.Equals(s, FixtureSegment, StringComparison.OrdinalIgnoreCase)))
        {
            return FixtureSegment;
        }

        if (segments.Length >= 2
            && string.Equals(segments[1], LegacyRepoDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return LegacyRepoDirectory;
        }

        foreach (var root in EnumerateRoots(extraRoots))
        {
            var nroot = Normalize(root);
            if (nroot is null || nroot.Length == 0)
            {
                continue;
            }

            if (normalized.Equals(nroot, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(nroot + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return nroot;
            }
        }

        return null;
    }

    /// <summary>Full path 解析（含 <c>..</c> / 相对路径 / 混合分隔符消解）。失败 → null（交由覆写闸门保守处置）。</summary>
    private static string? ResolveFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>路径上任一**已存在**祖先（含自身）是否为 reparse point（junction / symlink / mount point）。</summary>
    private static bool HasReparsePointAncestor(string resolvedPath)
    {
        try
        {
            var current = resolvedPath;
            // 逐级上溯到根；只检查**实际存在**的段（不存在的段不可能已是 reparse point）
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(current) || Directory.Exists(current))
                {
                    var attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        return true;
                    }
                }

                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = parent;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;   // 无法判定 → 不误报（其余判界仍然生效）
        }

        return false;
    }

    /// <summary>允许范围白名单判定（仅在 <see cref="AllowedRootsVariable"/> 配置时启用）。</summary>
    private static string? MatchAllowedRoots(string resolvedPath)
    {
        var env = Environment.GetEnvironmentVariable(AllowedRootsVariable);
        if (string.IsNullOrWhiteSpace(env))
        {
            return null;   // 未配置 → 不启用（不改变既有契约面）
        }

        var roots = env.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (roots.Length == 0)
        {
            return null;
        }

        var normalized = Normalize(resolvedPath);
        if (normalized is null)
        {
            return null;
        }

        foreach (var root in roots)
        {
            var nroot = Normalize(root);
            if (nroot is null || nroot.Length == 0)
            {
                continue;
            }

            if (normalized.Equals(nroot, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(nroot + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return null;   // 在白名单内 → 放行
            }
        }

        return NotInAllowedRootsHit + " (" + string.Join(';', roots) + ")";
    }

    private static IEnumerable<string> EnumerateRoots(IEnumerable<string>? extraRoots)
    {
        if (extraRoots is not null)
        {
            foreach (var r in extraRoots)
            {
                yield return r;
            }
        }

        var env = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(env))
        {
            yield break;
        }

        foreach (var part in env.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }

    /// <summary>路径归一化：统一分隔符、消解 "." 与 ".."，用于前缀/路径段判定。</summary>
    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var raw in path.Trim().Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw == ".")
            {
                continue;
            }

            if (raw == "..")
            {
                if (parts.Count > 1)
                {
                    parts.RemoveAt(parts.Count - 1);
                }

                continue;
            }

            parts.Add(raw);
        }

        return parts.Count == 0 ? null : string.Join('\\', parts);
    }
}

/// <summary>raster_mosaic：多栅格镶嵌为单栅格（MosaicToNewRaster_management）。</summary>
public sealed class RasterMosaicTool : McpToolBase
{
    /// <summary>pixel_type 白名单（来源：GP 实测 000800 合法值清单）。</summary>
    private static readonly HashSet<string> AllowedPixelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "1_BIT", "2_BIT", "4_BIT", "8_BIT_UNSIGNED", "8_BIT_SIGNED",
        "16_BIT_UNSIGNED", "16_BIT_SIGNED", "32_BIT_UNSIGNED", "32_BIT_SIGNED",
        "32_BIT_FLOAT", "64_BIT"
    };

    /// <summary>mosaic_method 白名单（来源：GP 实测 000800 合法值清单）。</summary>
    private static readonly HashSet<string> AllowedMosaicMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "FIRST", "LAST", "BLEND", "MEAN", "MINIMUM", "MAXIMUM", "SUM"
    };

    public override string Name => "raster_mosaic";

    public override string Description =>
        "栅格镶嵌（MosaicToNewRaster_management）：把 2 个及以上栅格镶嵌为**单个新栅格**（输出独立，不写回源）。" +
        "参数：inputs（**分号分隔的多栅格**，≥2，每项为数据集路径或图层名）、output（**文件 GDB 内的新栅格路径**，如 `...\\out.gdb\\MOS`）、" +
        "numberOfBands（**必填**：实测 GP 缺该参数即 `ERROR 000735 波段数: 值是必需的`）、" +
        "cellSize（可选，实测可空）、pixelType（可选，白名单：1_BIT/2_BIT/4_BIT/8_BIT_UNSIGNED/8_BIT_SIGNED/16_BIT_UNSIGNED/16_BIT_SIGNED/" +
        "32_BIT_UNSIGNED/32_BIT_SIGNED/32_BIT_FLOAT/64_BIT）、mosaicMethod（可选，白名单：FIRST/LAST/BLEND/MEAN/MINIMUM/MAXIMUM/SUM）、" +
        "overwrite（默认 false；已存在且未显式授权 → `OUTPUT_EXISTS`，不执行 GP）。" +
        "**输出路径披露（O-D048-06）**：output 必须位于**文件 GDB 内**（目录型输出按 GRID 处理，路径不得含空格；实测目录输出直接 `ERROR 999999`）。" +
        "**受保护路径守卫（O-D049-08 试点）**：输出命中仓库 `TestFixtures`、旧仓库 `ArcGIS-Pro-MCP` 或环境变量 `ARCGIS_PRO_MCP_PROTECTED_ROOTS` 所列根 → " +
        "`PATH_ESCAPE_REJECTED`，**不执行 GP、零产物**。GP 失败 → 既有码包装 + GP 消息透出。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputs"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Semicolon-separated raster datasets (>=2), e.g. A;B" },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output raster path inside a file GDB, e.g. C:/tmp/out.gdb/MOS" },
            ["numberOfBands"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Required: number of bands for the output raster (e.g. \"1\")." },
            ["cellSize"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional output cell size, e.g. \"1\"." },
            ["pixelType"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional pixel type, e.g. 32_BIT_FLOAT." },
            ["mosaicMethod"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional mosaic method: FIRST (default) | LAST | BLEND | MEAN | MINIMUM | MAXIMUM | SUM." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "inputs", "output", "numberOfBands" }
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
        var bands = ToolArgs.GetString(context, "numberOfBands");
        if (string.IsNullOrWhiteSpace(inputs) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(bands))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "inputs, output and numberOfBands are required.");
        }

        // 工具层业务校验 ①：输入数 ≥2（镶嵌语义要求）+ 多值形态合法（无空项）
        var gpInputs = GpMultiValueBuilder.FromSemicolonList(inputs);
        var itemCount = inputs.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
        if (gpInputs is null || itemCount < 2)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "inputs must be a semicolon-separated list of at least two raster datasets (no empty items).");
        }

        // 工具层业务校验 ②：输出必须落文件 GDB（GRID 空格约束，O-D048-06 实测）
        var split = SplitGdbOutput(output);
        if (split is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "output must be a raster dataset path inside a file GDB (e.g. .../out.gdb/MOS); " +
                "directory outputs are treated as GRID and fail on paths containing spaces (O-D048-06).");
        }

        var (location, datasetName) = split.Value;

        if (!int.TryParse(bands.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bandCount) || bandCount <= 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "numberOfBands must be a positive integer.");
        }

        var cellSize = (ToolArgs.GetString(context, "cellSize") ?? string.Empty).Trim();
        if (cellSize.Length > 0
            && (!double.TryParse(cellSize, NumberStyles.Float, CultureInfo.InvariantCulture, out var cellValue) || cellValue <= 0))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "cellSize must be a positive number when provided.");
        }

        var pixelType = (ToolArgs.GetString(context, "pixelType") ?? string.Empty).Trim();
        if (pixelType.Length > 0 && !AllowedPixelTypes.Contains(pixelType))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "pixelType must be one of: " + string.Join(", ", AllowedPixelTypes.OrderBy(x => x, StringComparer.Ordinal)) + ".");
        }

        var method = (ToolArgs.GetString(context, "mosaicMethod") ?? string.Empty).Trim();
        if (method.Length > 0 && !AllowedMosaicMethods.Contains(method))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "mosaicMethod must be one of: " + string.Join(", ", AllowedMosaicMethods.OrderBy(x => x, StringComparer.Ordinal)) + ".");
        }

        // 受保护输出路径守卫（进入 GP 之前）
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

        // 参数序 A/B 实测：(in_rasters, output_location, raster_dataset_name, {coordinate_system},
        //                    {pixel_type}, {cellsize}, {number_of_bands}, {mosaic_method}, {mosaic_colormap_mode})
        var values = new List<string>
        {
            gpInputs, location, datasetName, string.Empty, pixelType, cellSize,
            bandCount.ToString(CultureInfo.InvariantCulture),
            method.Length == 0 ? string.Empty : method.ToUpperInvariant(), string.Empty
        };
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "MosaicToNewRaster_management", Values = values },
            context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }

    /// <summary>把 <c>...\out.gdb\NAME</c> 拆成 (容器目录, 数据集名)；不满足文件 GDB 形态 → null。</summary>
    internal static (string Location, string Name)? SplitGdbOutput(string output)
    {
        var trimmed = output.Trim();
        var idx = trimmed.LastIndexOfAny(new[] { '\\', '/' });
        if (idx <= 0 || idx == trimmed.Length - 1)
        {
            return null;
        }

        var location = trimmed[..idx];
        var name = trimmed[(idx + 1)..];
        if (!location.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
        {
            return null;
        }

        return (location, name);
    }
}

/// <summary>
/// raster_calc：**受约束**栅格计算（RasterCalculator_sa）。
/// ★ plan §8「受约束计算」+ §7「不开放任意 Python」红线：表达式在**工具层**按白名单 tokenize 后**重建**，
/// 用户文本中任何非白名单构造都会被拒绝（含 <c>__import__</c>/<c>import</c>/<c>open</c>/引号/分号/换行等）。
/// 实测旁证：GP 侧 RCEXEC 表达式本质是**任意 Python**（spike 中 <c>__import__('os').system('echo pwn')</c> 真的执行并输出 pwn、
/// <c>open(...).write(...)</c> 真的写出文件）⇒ 白名单不是"锦上添花"而是**硬性红线**。
/// </summary>
public sealed class RasterCalcTool : McpToolBase
{
    /// <summary>允许的函数名（逐个来自 RCEXEC 命名空间实测 dump，未实测到的一律不放行 —— 如 Min/Max/Sqrt 实测不存在）。</summary>
    private static readonly HashSet<string> AllowedFunctions = new(StringComparer.Ordinal)
    {
        "Con", "IsNull", "SetNull", "Pick",
        "Abs", "SquareRoot", "Square", "Exp", "Ln", "Log10", "Log2", "Power",
        "Int", "Float", "RoundDown", "RoundUp", "Sin", "Cos", "Tan", "Negate"
    };

    /// <summary>允许的布尔关键字。</summary>
    private static readonly HashSet<string> AllowedKeywords = new(StringComparer.Ordinal) { "and", "or", "not" };

    private const int MaxExpressionLength = 512;
    private const int MaxTokens = 256;

    private static readonly Regex AliasPattern = new(@"^[A-Za-z_][A-Za-z0-9_]{0,31}$", RegexOptions.Compiled);

    private static readonly Regex TokenPattern = new(
        @"\G\s*(?:(?<num>\d+(?:\.\d+)?)|(?<id>[A-Za-z_][A-Za-z0-9_]*)|(?<op>\*\*|<=|>=|==|!=|[()+\-*/%,<>]))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public override string Name => "raster_calc";

    public override string Description =>
        "受约束栅格计算（RasterCalculator_sa）：按**白名单表达式**对 1 个或多个栅格做逐像元计算，输出新栅格（**输出独立**）。" +
        "参数：rasters（**分号分隔的 `别名=栅格路径`**，如 `a=D:\\x.gdb\\S_R1;b=D:\\x.gdb\\S_R2`）、" +
        "expression（**受约束表达式**，只能用别别名、数字、运算符 `+ - * / % ** ( ) ,` 与比较/布尔运算、" +
        "以及白名单函数 Con/IsNull/SetNull/Pick/Abs/SquareRoot/Square/Exp/Ln/Log10/Log2/Power/Int/Float/RoundDown/RoundUp/Sin/Cos/Tan/Negate）、" +
        "output（新栅格路径，建议文件 GDB）、overwrite（默认 false）。" +
        "**安全红线（plan §7/§8）**：表达式**不开放任意 Python** —— 工具层先 tokenize 再重建，`import`/`__import__`/`open`/引号/分号/换行等一律 `INVALID_ARGUMENT`，**不进入 GP**；" +
        "（实测旁证：GP 侧表达式本身是任意 Python，可执行 `__import__('os').system(...)` 与文件写入，故白名单为硬性要求。）" +
        "**输入披露**：参与运算的栅格**范围必须重叠**，否则 GP 报 `ERROR 010092 无效的输出范围`（实测）。" +
        "**受保护路径守卫（O-D049-08 试点）**：输出命中 `TestFixtures`／旧仓库／`ARCGIS_PRO_MCP_PROTECTED_ROOTS` → `PATH_ESCAPE_REJECTED`，不执行 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["rasters"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Semicolon-separated alias=path pairs, e.g. a=D:/x.gdb/S_R1;b=D:/x.gdb/S_R2" },
            ["expression"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Whitelisted expression using aliases, e.g. Con(a > 2, a, b)" },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output raster path (file GDB recommended)." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "rasters", "expression", "output" }
    };

    protected override string CategoryName => ToolCategories.Analysis;

    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var rasters = ToolArgs.GetString(context, "rasters");
        var expression = ToolArgs.GetString(context, "expression");
        var output = ToolArgs.GetString(context, "output");
        if (string.IsNullOrWhiteSpace(rasters) || string.IsNullOrWhiteSpace(expression) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "rasters, expression and output are required.");
        }

        if (!TryParseRasters(rasters, out var aliases, out var parseError))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, parseError!);
        }

        if (!TryBuildExpression(expression, aliases, out var gpExpression, out var expressionError))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, expressionError!);
        }

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

        // 参数序 A/B 实测：(expression, out_raster)
        var values = new List<string> { gpExpression, output };
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "RasterCalculator_sa", Values = values },
            context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }

    /// <summary>解析 <c>alias=path;alias=path</c>（别名白名单校验；路径禁引号/换行 ⇒ 防注入）。</summary>
    private static bool TryParseRasters(string raw, out Dictionary<string, string> aliases, out string? error)
    {
        aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        error = null;
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var item = part.Trim();
            if (item.Length == 0)
            {
                continue;
            }

            var eq = item.IndexOf('=');
            if (eq <= 0 || eq == item.Length - 1)
            {
                error = $"rasters item '{Truncate(item)}' must be in the form alias=path.";
                return false;
            }

            var alias = item[..eq].Trim();
            var path = item[(eq + 1)..].Trim();
            if (!AliasPattern.IsMatch(alias))
            {
                error = $"raster alias '{Truncate(alias)}' must match [A-Za-z_][A-Za-z0-9_]{{0,31}}.";
                return false;
            }

            if (AllowedFunctions.Contains(alias) || AllowedKeywords.Contains(alias))
            {
                error = $"raster alias '{alias}' shadows a whitelisted function/keyword; pick another alias.";
                return false;
            }

            if (path.Length == 0 || path.IndexOfAny(new[] { '"', '\'', '\n', '\r', ';' }) >= 0)
            {
                error = $"raster path for alias '{alias}' is empty or contains forbidden characters (\" ' ; newline).";
                return false;
            }

            if (!aliases.TryAdd(alias, path))
            {
                error = $"duplicate raster alias '{alias}'.";
                return false;
            }
        }

        if (aliases.Count == 0)
        {
            error = "rasters must contain at least one alias=path item.";
            return false;
        }

        return true;
    }

    /// <summary>白名单 tokenize + **重建**表达式（别名 → <c>Raster(r"path")</c>），任何越界构造 → 拒绝。</summary>
    private static bool TryBuildExpression(string expression, Dictionary<string, string> aliases, out string built, out string? error)
    {
        built = string.Empty;
        error = null;
        var trimmed = expression.Trim();
        if (trimmed.Length == 0)
        {
            error = "expression must not be empty.";
            return false;
        }

        if (trimmed.Length > MaxExpressionLength)
        {
            error = $"expression exceeds the maximum length of {MaxExpressionLength} characters.";
            return false;
        }

        // 单行约束：显式拒绝换行/回车（tokenizer 的 \s* 本会吞掉 "\n"，此处把「只能单行」写成硬约束）
        if (trimmed.IndexOfAny(new[] { '\n', '\r' }) >= 0)
        {
            error = "expression must be a single line (no CR/LF).";
            return false;
        }

        var sb = new StringBuilder(trimmed.Length + 32);
        var pos = 0;
        var tokens = 0;
        while (pos < trimmed.Length)
        {
            var m = TokenPattern.Match(trimmed, pos);
            if (!m.Success || m.Length == 0)
            {
                error = $"expression contains a token outside the whitelist near '{Truncate(trimmed[pos..])}' " +
                        "(allowed: aliases, numbers, + - * / % ** ( ) , comparisons, and, or, not, whitelisted functions).";
                return false;
            }

            pos += m.Length;
            tokens++;
            if (tokens > MaxTokens)
            {
                error = $"expression exceeds the maximum token count of {MaxTokens}.";
                return false;
            }

            if (m.Groups["num"].Success)
            {
                sb.Append(sb.Length == 0 ? string.Empty : " ").Append(m.Groups["num"].Value);
                continue;
            }

            if (m.Groups["op"].Success)
            {
                sb.Append(sb.Length == 0 ? string.Empty : " ").Append(m.Groups["op"].Value);
                continue;
            }

            var id = m.Groups["id"].Value;
            if (AllowedFunctions.Contains(id))
            {
                sb.Append(sb.Length == 0 ? string.Empty : " ").Append(id);
                continue;
            }

            if (AllowedKeywords.Contains(id))
            {
                sb.Append(sb.Length == 0 ? string.Empty : " ").Append(id);
                continue;
            }

            if (aliases.TryGetValue(id, out var path))
            {
                // 注入形态用 raw 字符串：反斜杠路径不会被 Python 转义（实测 r"..." 必需 —— v3 spike）
                sb.Append(sb.Length == 0 ? string.Empty : " ").Append("Raster(r\"").Append(path).Append("\")");
                continue;
            }

            error = $"identifier '{Truncate(id)}' is neither a declared raster alias nor a whitelisted function.";
            return false;
        }

        if (tokens == 0)
        {
            error = "expression must not be empty.";
            return false;
        }

        built = sb.ToString();
        return true;
    }

    private static string Truncate(string value, int max = 40)
        => value.Length <= max ? value : value[..max] + "…";
}
