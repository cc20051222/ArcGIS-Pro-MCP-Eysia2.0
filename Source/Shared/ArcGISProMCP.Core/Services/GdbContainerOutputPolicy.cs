namespace ArcGISProMCP.Core.Services;

/// <summary>
/// GDB 容器内数据集**名称命中**判定（纯逻辑，无 SDK 依赖，便于单测）。
/// <para>
/// **D-051（P1 维修单，O-D050-08）**：<c>GeoprocessingService.CheckGdbOutputExistsViaSdkAsync</c> 原先只枚举
/// <c>FeatureClassDefinition</c> + <c>TableDefinition</c>，**未枚举栅格数据集** ⇒ `<...>.gdb\<栅格名>` 恒判
/// <c>NotExists</c>（假阴性）⇒ ① 覆写守卫对 GDB 内栅格输出**失效**（静默覆盖既有栅格）；
/// ② 取消/超时清理把**先验存在**的栅格产物当"残留"删除（数据丢失）。
/// 本类把"三条定义任一命中"的判定抽成可测纯函数（枚举本身仍在 SDK 侧完成）。
/// </para>
/// </summary>
public static class GdbDefinitionMatcher
{
    /// <summary>SDK 枚举成功完成时的 detail 标记（预检**结论可靠**的唯一标记）。</summary>
    public const string SdkDetail = "sdk-geodatabase";

    /// <summary>三条定义（要素类 / 表 / 栅格数据集）任一命中即视为存在。</summary>
    public static bool AnyMatches(
        string? datasetName,
        IEnumerable<string>? featureClasses,
        IEnumerable<string>? tables,
        IEnumerable<string>? rasterDatasets)
        => Matches(datasetName, featureClasses)
           || Matches(datasetName, tables)
           || Matches(datasetName, rasterDatasets);

    /// <summary>名称集合命中（大小写不敏感；空名 / 空集合 → false）。</summary>
    public static bool Matches(string? datasetName, IEnumerable<string>? names)
    {
        if (string.IsNullOrWhiteSpace(datasetName) || names is null)
        {
            return false;
        }

        foreach (var n in names)
        {
            if (!string.IsNullOrEmpty(n) && string.Equals(n, datasetName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>预检结论是否**可靠**（仅 <see cref="SdkDetail"/> 视为可靠；其余 detail 一律不可靠）。</summary>
    public static bool IsReliablePrecheckDetail(string? detail)
        => string.Equals(detail, SdkDetail, StringComparison.Ordinal);
}

/// <summary>取消/超时后的残留清理决策（D-017 F2 / D-037 F-D036-1 / **D-051 B 加固**）。</summary>
public enum ResidualCleanupDecision
{
    /// <summary>先验存在 ⇒ 一律不动（护栏）。</summary>
    KeepPreExisting,

    /// <summary>可证为本轮新建的残留 ⇒ 尝试删除。</summary>
    DeleteResidual,

    /// <summary>文件型输出且 after 仍不存在 ⇒ 无残留可清。</summary>
    NothingToDelete,

    /// <summary>GDB 容器 + 预检不可得（<c>null</c>）⇒ 保守不清理（D-017 原保护）。</summary>
    SkipPreExistenceUnknown,

    /// <summary>GDB 容器 + 预检结论不可靠（枚举异常/路径形态异常等）⇒ 保守不清理（**D-051 新增加固**）。</summary>
    SkipPrecheckUnreliable,
}

/// <summary>
/// <c>CleanupResidualOutputAsync</c> 的决策纯函数（无 SDK 依赖）。
/// <para>
/// 红线（D-017 + D-051）：**不得在"无法证明产物是本轮新建"时删除任何东西**。
/// 先验存在 → 不动；预检不可得 → 不动；预检结论不可靠 → 不动；只有"可靠地判定先验不存在"才允许清理。
/// </para>
/// </summary>
public static class ResidualCleanupPolicy
{
    public static ResidualCleanupDecision Decide(bool isGdbContainer, bool? preExists, string? precheckDetail, bool afterExists)
    {
        if (isGdbContainer)
        {
            if (preExists is null)
            {
                return ResidualCleanupDecision.SkipPreExistenceUnknown;
            }

            if (preExists.Value)
            {
                return ResidualCleanupDecision.KeepPreExisting;
            }

            // ★ D-051 加固：GDB 容器下"先验不存在"这一结论必须来自**可靠的 SDK 枚举**；
            // 任何其它 detail（枚举异常 / 路径形态异常 / 无 .gdb 段）都不足以支撑删除。
            return GdbDefinitionMatcher.IsReliablePrecheckDetail(precheckDetail)
                ? ResidualCleanupDecision.DeleteResidual
                : ResidualCleanupDecision.SkipPrecheckUnreliable;
        }

        if (preExists == true)
        {
            return ResidualCleanupDecision.KeepPreExisting;
        }

        return afterExists ? ResidualCleanupDecision.DeleteResidual : ResidualCleanupDecision.NothingToDelete;
    }

    /// <summary>生成响应附注文本（null = 不附注）。删除成功的附注由调用方在删除后生成。</summary>
    public static string? Describe(ResidualCleanupDecision decision, string? precheckDetail) => decision switch
    {
        ResidualCleanupDecision.SkipPreExistenceUnknown =>
            $" (cleanup skipped: pre-existence unknown — precheck [{precheckDetail}])",
        ResidualCleanupDecision.SkipPrecheckUnreliable =>
            $" (cleanup skipped: pre-existence unreliable — precheck [{precheckDetail}])",
        _ => null
    };
}
