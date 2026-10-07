namespace ArcGISProMCP.Core.Results;

/// <summary>
/// GP 输出的存在性判定结果（D-021 / F6 覆写策略；三态）。
/// 三态而非布尔：存在性"不可判定"必须与"确定不存在"区分开（D-017 教训——文件语义在 GDB 容器内恒 false）。
/// </summary>
public enum OutputExistence
{
    /// <summary>输出确定不存在（可安全新建）。</summary>
    NotExists = 0,

    /// <summary>输出确定已存在。</summary>
    Exists = 1,

    /// <summary>存在性不可判定（探测失败 / 能力不足）——必须保守处理。</summary>
    Unknown = 2,
}

/// <summary>覆写前置判定结论（纯数据，便于单测断言）。</summary>
public sealed record OverwriteDecision(bool Proceed, string? ErrorCode, string? Message)
{
    public static OverwriteDecision Allow() => new(true, null, null);

    public static OverwriteDecision Refuse(string code, string message) => new(false, code, message);
}

/// <summary>
/// F6（D-021）覆写策略：**S1（默认拒绝）为默认语义 + S2（显式 `overwrite`）为迁移逃生舱**（S3/S1′ 不实施）。
/// 本类是**纯策略层**：只依据"存在性判定结果 + overwrite 开关"决策，不做任何 IO、不引用 ArcGIS SDK。
/// 真实存在性探测由宿主服务（<c>IGeoprocessingService.CheckOutputExistsAsync</c>）完成，
/// GDB 容器内输出复用 D-017 已落地的 SDK Geodatabase 定义枚举，本层不重复实现。
/// 红线：判定不可得（Unknown）且未显式授权 → **保守拒绝**，绝不默认放行覆写。
/// </summary>
public static class OverwritePolicy
{
    /// <summary>写工具的显式覆写开关参数名（schema 须声明，默认 false）。</summary>
    public const string ParameterName = "overwrite";

    /// <summary>覆写开关的 schema 声明（4 个 GP 写工具共用，保证默认值与描述一致）。</summary>
    public static IReadOnlyDictionary<string, object?> SchemaProperty { get; } = new Dictionary<string, object?>
    {
        ["type"] = "boolean",
        ["description"] = "Allow replacing an existing output. Default false: the tool refuses to run when the output already exists (OUTPUT_EXISTS)."
    };

    /// <summary>
    /// 前置判定：NotExists → 放行；Exists/Unknown + <paramref name="overwrite"/> → 放行（含覆写风险）；
    /// Exists/Unknown + 未授权 → 拒绝（错误码 <see cref="ErrorCodes.OutputExists"/>，不进入 GP）。
    /// </summary>
    public static OverwriteDecision Decide(OutputExistence existence, bool overwrite, string? outputPath, string? detail = null)
    {
        if (existence == OutputExistence.NotExists || overwrite)
        {
            return OverwriteDecision.Allow();
        }

        if (existence == OutputExistence.Exists)
        {
            return OverwriteDecision.Refuse(
                ErrorCodes.OutputExists,
                $"Output '{outputPath}' already exists. Overwriting is refused by default; pass {ParameterName}=true to replace it, or delete the output first.");
        }

        var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : " (" + detail + ")";
        return OverwriteDecision.Refuse(
            ErrorCodes.OutputExists,
            $"Existence of output '{outputPath}' could not be determined{suffix}; refusing to run rather than risk overwriting existing data. "
            + $"Pass {ParameterName}=true to force execution.");
    }

    /// <summary>显式覆写且输出原已存在时的审计提示（附加在成功响应消息上）。</summary>
    public static string OverwriteNote(string? outputPath)
        => $" ({ParameterName}=true: existing output '{outputPath}' was replaced by this run)";
}
