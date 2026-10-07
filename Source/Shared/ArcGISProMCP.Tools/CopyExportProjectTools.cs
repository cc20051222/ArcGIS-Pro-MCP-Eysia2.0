using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// Phase 9 第二批（D-035）：copy_dataset / export_table / project —— GP 产出写工具。
/// 复用既有覆写守卫（GpOverwriteGuard：默认拒绝 OUTPUT_EXISTS + overwrite 双信号）与
/// stateProof 三态（GeoprocessingService.RunToolAsync：文件 executed / 容器 unprovable）。
/// </summary>
public sealed class CopyDatasetTool : McpToolBase
{
    public override string Name => "copy_dataset";
    public override string Description =>
        "复制数据集（要素类/表/栅格）到目标路径。参数：inputPath（源数据集，GDB 内或文件）、outputPath（目标，scratch/自有 GDB 内）、overwrite（默认 false）。" +
        "契约：输出已存在且未显式 overwrite=true → OUTPUT_EXISTS（不执行 GP、零状态变更、不带 stateProof）；成功返回 stateProof（文件 executed / GDB 容器 unprovable）；空白参数 = 非法参数。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Source dataset path (GDB internal or file)." },
            ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target dataset path (scratch / owned GDB)." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "inputPath", "outputPath" }
    };
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var input = ToolArgs.GetString(context, "inputPath");
        var output = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputPath and outputPath are required.");
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

        var values = new List<string> { input, output };
        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "Copy_management", Values = values }, context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>
/// 导出表/要素类为 CSV（UTF-8）或 GDB 表（GP：conversion.ExportTable）。
/// <para>
/// **D-037 F-D035-1 修正**：GP 签名为
/// <c>ExportTable(in_table, out_table, {where_clause}, use_field_alias_as_name, {field_mapping}, {sort_field})</c>
/// （Esri 官方工具参考）。旧实现把 field_mapping 放在**位置 2（where_clause）**→ 映射串被当作 SQL 表达式
/// 吞掉 → 输出恒为全字段（LIVE 实测）。现按官方位置传参：0 输入、1 输出、2 where（空）、
/// 3 NOT_USE_ALIAS、4 field_mapping；并在执行后**核验输出字段集**（可核验且不符 → 显式报错，
/// 不可核验 → 在载荷 Messages 显式标注，绝不静默返回全字段）。
/// </para>
/// </summary>
public sealed class ExportTableTool : McpToolBase
{
    /// <summary>输出侧由平台自动维护、不属于"用户请求的字段子集"的系统字段（核验时忽略）。</summary>
    private static readonly HashSet<string> SystemFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "OBJECTID", "OID", "FID", "SHAPE", "Shape_Length", "Shape_Area",
        "Shape.STLength()", "Shape.STArea()", "GDB_GEOMATTR_DATA",
    };

    public override string Name => "export_table";
    public override string Description =>
        "导出表/要素类到 CSV（UTF-8，输出路径以 .csv 结尾）或 GDB 表（输出路径为 GDB 内表）。参数：inputPath、outputPath、fieldNames（可选字段子集，默认全部）、overwrite（默认 false）。" +
        "契约：输出已存在且未显式 overwrite=true → OUTPUT_EXISTS（不执行 GP、零状态变更、不带 stateProof）；成功返回 stateProof；空白参数 = 非法参数；" +
        "**指定 fieldNames 时输出字段集 = 请求子集（D-037 F-D035-1）：执行后核验，不符或不可核验均显式暴露，绝不静默返回全字段**。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Source table / feature class path." },
            ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target: .csv file (UTF-8) or GDB table path." },
            ["fieldNames"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "string" }, ["description"] = "Optional field subset; default all fields." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "inputPath", "outputPath" }
    };
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var input = ToolArgs.GetString(context, "inputPath");
        var output = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputPath and outputPath are required.");
        }

        // 可选字段子集 → GP field_mapping（短式 "<in> <out> VISIBLE NONE"，分号分隔；
        // 语法据 Esri 官方 000812/000813 说明："fldName newFldName visible;…"）。缺省 = 空串（GP 默认全字段）。
        string fieldMapping = string.Empty;
        List<string>? requested = null;
        if (context.Arguments is not null && context.Arguments.TryGetValue("fieldNames", out var fn) && fn is System.Collections.IEnumerable en)
        {
            var fields = new List<string>();
            foreach (var item in en)
            {
                if (item is string s && !string.IsNullOrWhiteSpace(s))
                {
                    fields.Add(s);
                }
            }

            if (fields.Count > 0)
            {
                requested = fields;
                fieldMapping = string.Join(";", fields.Select(f => $"{Quote(f)} {Quote(f)} VISIBLE NONE"));
            }
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

        // D-037 F-D035-1：按 Esri 官方参数位置下传（field_mapping 在第 5 位，旧实现错放在 where_clause 位）。
        var values = new List<string> { input, output, string.Empty, "NOT_USE_ALIAS", fieldMapping };
        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "ExportTable_conversion", Values = values }, context.CancellationToken).ConfigureAwait(false);
        var result = gate.Annotate(ToolResult.From(r));
        if (!result.Success || requested is null)
        {
            return result;
        }

        return await AnnotateFieldSubsetVerificationAsync(context, result, output, requested, context.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>字段名含空白时按 Esri 惯例加单引号（GDB 字段名通常无空白，此处为防御）。</summary>
    private static string Quote(string field)
        => field.Any(char.IsWhiteSpace) ? "'" + field + "'" : field;

    /// <summary>
    /// D-037 F-D035-1：核验"输出字段集 = 请求子集"。
    /// 可核验且不符（缺字段 / 多出非系统字段）→ **显式报错**（GEOPROCESSING_ERROR）；
    /// 不可核验（如 CSV 等非数据集输出读不到 schema）→ 在载荷 Messages 内显式标注（不静默）。
    /// </summary>
    private static async Task<OperationResult<object?>> AnnotateFieldSubsetVerificationAsync(
        ToolExecutionContext context, OperationResult<object?> result, string output,
        IReadOnlyList<string> requested, CancellationToken ct)
    {
        var schema = await context.Host!.Schema.GetSchemaInfoAsync(output, ct).ConfigureAwait(false);
        var fields = schema.Success ? schema.Data?.Fields : null;
        if (fields is not { Count: > 0 })
        {
            if (result.Data is GeoprocessingResult gp)
            {
                gp.Messages = new List<string>(gp.Messages ?? Array.Empty<string>())
                {
                    "fieldSubsetUnverified: the output field set could not be read back; the requested subset was passed to GP as field_mapping but is NOT verified (schema probe unavailable for this output kind)."
                };
            }

            return result;
        }

        var actual = fields.Select(f => f.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wanted = requested.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = wanted.Where(w => !actual.Contains(w)).ToList();
        var extra = actual.Where(a => !wanted.Contains(a) && !SystemFields.Contains(a)).ToList();

        if (missing.Count == 0 && extra.Count == 0)
        {
            if (result.Data is GeoprocessingResult ok)
            {
                ok.Messages = new List<string>(ok.Messages ?? Array.Empty<string>())
                {
                    "fieldSubsetVerified: output field set equals the requested subset."
                };
            }

            return result;
        }

        var detail = $"requested=[{string.Join(", ", requested)}]; actual=[{string.Join(", ", actual)}]"
            + (missing.Count > 0 ? $"; missing=[{string.Join(", ", missing)}]" : string.Empty)
            + (extra.Count > 0 ? $"; unrequested=[{string.Join(", ", extra)}]" : string.Empty);
        return OperationResult<object?>.Fail(
            ErrorCodes.GeoprocessingError,
            "export_table field subset did not take effect: the output field set does not equal the requested fieldNames subset.",
            detail);
    }
}

/// <summary>
/// 投影转换要素类到新坐标系（GP：management.Project）。
/// <para>
/// **D-037 F-D035-2 修正**：LIVE 实测 <c>out_coor_system</c> **仅 WKID 数值形态可解析**，
/// 名称形态（空格写法与下划线写法）一律 <c>ERROR 000735</c>（R-D035-phase2 PR2），而契约明示"WKID 或名称"。
/// 现以**契约为准**：名称形态先经宿主 <c>ISpatialReferenceService</c> 解析为 WKID 再下传（WKID 已实证可用）；
/// 解析不到 → 原样下传，由 GP 产出**带真实 GP 码**的显式错误（不静默、不猜坐标系）。
/// </para>
/// </summary>
public sealed class ProjectTool : McpToolBase
{
    public override string Name => "project";
    public override string Description =>
        "将要素类投影转换到新坐标系。参数：inputPath（源要素类）、outputPath（目标，scratch/自有 GDB 内）、outSR（目标坐标系 WKID 或名称，如 4326 或 WGS 1984）、overwrite（默认 false）。" +
        "契约：输出已存在且未显式 overwrite=true → OUTPUT_EXISTS（不执行 GP、零状态变更、不带 stateProof）；成功返回 stateProof（文件 executed / GDB 容器 unprovable）；空白参数 = 非法参数；" +
        "**outSR 名称形态（D-037 F-D035-2）由宿主解析为 WKID 后下传；无法解析的名称 → 显式报错（含 GP 000735），不静默回落**。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Source feature class path." },
            ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target feature class path." },
            ["outSR"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target spatial reference: WKID (e.g. 4326) or name (e.g. WGS 1984)." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "inputPath", "outputPath", "outSR" }
    };
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var input = ToolArgs.GetString(context, "inputPath");
        var output = ToolArgs.GetString(context, "outputPath");
        var outSR = ToolArgs.GetString(context, "outSR");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(outSR))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputPath, outputPath and outSR are required.");
        }

        // D-037 F-D035-2：名称 → WKID（宿主侧按 Pro 预置坐标系清单确定性匹配，归一化后全等）。
        // 解析成功 → 下传 WKID（LIVE 已证唯一可用形态）；未解析 → 原样下传，GP 给出含 000735 的显式错误。
        var outSRValue = outSR;
        var resolution = await context.Host.SpatialReferences.ResolveAsync(outSR, context.CancellationToken).ConfigureAwait(false);
        if (resolution.Success && resolution.Data is { Wkid: > 0 } sr)
        {
            outSRValue = sr.Wkid.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

        var values = new List<string> { input, output, outSRValue };
        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "Project_management", Values = values }, context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}
