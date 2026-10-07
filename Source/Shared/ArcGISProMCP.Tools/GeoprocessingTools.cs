using System.Globalization;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>GP 多值参数（ValueTable）构造（F11，D-028）。
/// 实测：Pro 的 Intersect_analysis 的 in_features 多值参数按空白切分，未加引号的含空格路径会被拆坏，
/// 误报 ERROR 000735「值是必需的」；逐项加单引号后，数据集路径与活动地图图层名两种形态均可用，
/// 无效输入改报 000732（数据集不存在），错误如实。</summary>
public static class GpMultiValueBuilder
{
    /// <summary>把分号分隔的原始输入归一化为 GP 多值字符串（每项单引号包裹）。
    /// 空项（如 "A;;B" 或 ";"）视为非法参数 → 返回 null，由调用方报 INVALID_ARGUMENT（不再以 000735 误导）。</summary>
    public static string? FromSemicolonList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Split(';');
        if (parts.Length == 0) return null;
        var items = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            var v = part.Trim().Trim('\'').Trim('"').Trim();
            if (v.Length == 0) return null;      // 空项 → 非法
            items.Add("'" + v + "'");
        }
        return items.Count == 0 ? null : string.Join(";", items);
    }
}

/// <summary>Buffer：对输入要素建立缓冲。</summary>
public sealed class BufferTool : McpToolBase
{
    public override string Name => "buffer";
    public override string Description => "对输入要素建立缓冲（Buffer）。参数：input(要素路径)、output(输出路径)、distance(数值)、distanceUnit(如 Meters, 默认)、dissolve(是否融合)、overwrite(默认 false)。默认拒绝覆写：输出已存在且不显式 overwrite=true 时返回 OUTPUT_EXISTS 且不执行 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["input"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Input feature dataset path." },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output feature class path." },
            ["distance"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Buffer distance." },
            ["distanceUnit"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Distance unit, e.g. Meters (default)." },
            ["dissolve"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Dissolve output. Default false." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "input", "output", "distance" }
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
        var output = ToolArgs.GetString(context, "output");
        var distance = ToolArgs.GetDouble(context, "distance");   // F12（D-028）：schema=number → 用 GetDouble，禁止截断小数
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output) || !distance.HasValue)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "input, output and distance are required.");
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

        var unit = ToolArgs.GetString(context, "distanceUnit") ?? "Meters";
        var dissolve = ToolArgs.GetBool(context, "dissolve");
        // F12：按不变文化格式化，避免区域小数分隔符（如 ","）污染 GP 参数。
        var distanceText = distance.Value.ToString("0.############", CultureInfo.InvariantCulture);
        var values = new List<string> { input, output, $"{distanceText} {unit}", "FULL", "ROUND", dissolve ? "ALL" : "NONE" };

        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "Buffer_analysis", Values = values }, context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>Clip：按裁剪要素裁剪输入要素。</summary>
public sealed class ClipTool : McpToolBase
{
    public override string Name => "clip";
    public override string Description => "用裁剪要素(clipFeatures)裁剪输入要素(input)。参数：input、clipFeatures、output、overwrite(默认 false)。默认拒绝覆写：输出已存在且不显式 overwrite=true 时返回 OUTPUT_EXISTS 且不执行 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["input"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["clipFeatures"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "input", "clipFeatures", "output" }
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
        var clip = ToolArgs.GetString(context, "clipFeatures");
        var output = ToolArgs.GetString(context, "output");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(clip) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "input, clipFeatures and output are required.");
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

        var values = new List<string> { input, clip, output };
        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "Clip_analysis", Values = values }, context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>Intersect：计算输入要素的几何交集。</summary>
public sealed class IntersectTool : McpToolBase
{
    public override string Name => "intersect";
    public override string Description => "计算输入要素的交集（Intersect）。参数：inputs(用分号分隔的多个输入，每项可为数据集路径或活动地图图层名)、output、overwrite(默认 false)。默认拒绝覆写：输出已存在且不显式 overwrite=true 时返回 OUTPUT_EXISTS 且不执行 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputs"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Semicolon-separated inputs; each item may be a dataset path or a layer name in the active map, e.g. A;B" },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string" },
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

        // F11（D-028）：多值参数逐项加引号 → 支持"数据集路径"与"活动地图图层名"双形态，
        // 且无效输入由 GP 如实报 000732（不再误报 000735「值是必需的」）。
        var gpInputs = GpMultiValueBuilder.FromSemicolonList(inputs);
        if (gpInputs is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputs must be a semicolon-separated list of dataset paths or layer names (no empty items).");
        }

        var values = new List<string> { gpInputs, output, "ALL", string.Empty };
        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "Intersect_analysis", Values = values }, context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>Dissolve：按字段融合要素。</summary>
public sealed class DissolveTool : McpToolBase
{
    public override string Name => "dissolve";
    public override string Description => "按可选字段融合输入要素（Dissolve）。参数：input、output、dissolveField（可选）、overwrite(默认 false)。默认拒绝覆写：输出已存在且不显式 overwrite=true 时返回 OUTPUT_EXISTS 且不执行 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["input"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["dissolveField"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "input", "output" }
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
        var output = ToolArgs.GetString(context, "output");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "input and output are required.");
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

        var field = ToolArgs.GetString(context, "dissolveField") ?? string.Empty;
        var values = new List<string> { input, output, field, string.Empty };
        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "Dissolve_management", Values = values }, context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

// --------------------------------------------------------------------------- F6 覆写前置判定（D-021）

/// <summary>覆写前置判定结果：是否显式授权 / 输出原是否已存在 / 拒绝时的错误响应（null = 放行）。</summary>
internal sealed record GpOverwriteGate(string Output, bool Overwrite, bool PreExisting, OperationResult<object?>? Refusal)
{
    /// <summary>
    /// 在成功响应上附加"已发生覆写"审计信息（仅 overwrite=true 且输出原已存在时）。
    /// D-026 F8：提示改入成功载荷字段 <see cref="GeoprocessingResult.OverwriteNote"/>
    /// （旧实现写 OperationResult.Message——该字段不进序列化载荷，导致提示不可观测）；
    /// 同时在 stateProof 内增补 <c>overwrite:true</c> 标记（方案 b：加字段，verdict 三态不变——
    /// 方案 a 新 verdict 值因消费方耦合面大（ADR 三态契约/受限 Skill/测试断言）上报等裁定）。
    /// Data 形态不符时保持旧 Message 行为兜底（不丢提示）。
    /// </summary>
    public OperationResult<object?> Annotate(OperationResult<object?> result)
    {
        if (!(result.Success && Overwrite && PreExisting))
        {
            return result;
        }

        if (result.Data is GeoprocessingResult gp)
        {
            gp.OverwriteNote = OverwritePolicy.OverwriteNote(Output);
            gp.StateProof = StateProof.AddOverwriteMarker(gp.StateProof);
            return result;
        }

        return OperationResult<object?>.Ok(result.Data, (result.Message ?? string.Empty) + OverwritePolicy.OverwriteNote(Output));
    }
}

/// <summary>
/// 4 个 GP 写工具共用的前置闸门（D-021 / F6）：**进入 GP 之前**判定输出存在性，
/// 未获显式授权且输出已存在（或存在性不可判定）→ 直接返回 <see cref="ErrorCodes.OutputExists"/>，
/// **不执行 GP、零状态变更、响应不带 stateProof**。
/// 存在性探测本身由宿主完成（GDB 容器内复用 D-017 的 SDK 枚举），本类不重复实现、不引用 SDK。
/// </summary>
internal static class GpOverwriteGuard
{
    public static async Task<GpOverwriteGate> EvaluateAsync(ToolExecutionContext context, string output, CancellationToken ct)
    {
        var overwrite = ToolArgs.GetBool(context, OverwritePolicy.ParameterName);
        OutputExistence existence;
        string? detail;
        try
        {
            var probe = await context.Host!.Geoprocessing.CheckOutputExistsAsync(output, ct).ConfigureAwait(false);
            if (probe.Success)
            {
                existence = probe.Data;
                detail = probe.Message;
            }
            else
            {
                // 探测失败 → 不可判定 → 保守拒绝（红线：不得默认放行覆写）。
                existence = OutputExistence.Unknown;
                var first = probe.Errors.Count > 0 ? probe.Errors[0] : null;
                detail = "probe-failed: " + (first is null ? "no existence result" : $"{first.Code} {first.Message}");
            }
        }
        catch (Exception ex)
        {
            existence = OutputExistence.Unknown;
            detail = "probe-exception: " + ex.Message;
        }

        var decision = OverwritePolicy.Decide(existence, overwrite, output, detail);
        var refusal = decision.Proceed
            ? null
            : OperationResult<object?>.Fail(decision.ErrorCode!, decision.Message!);

        return new GpOverwriteGate(output, overwrite, existence == OutputExistence.Exists, refusal);
    }
}
