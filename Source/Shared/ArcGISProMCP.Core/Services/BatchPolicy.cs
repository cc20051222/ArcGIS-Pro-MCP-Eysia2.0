using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-064 · <c>run_batch</c> 安全策略（**纯策略层**：无 IO、无 SDK —— 便于单测穷举）。
/// <list type="number">
/// <item><b>白名单准入</b>：批内每一项必须是**已注册工具**，且不在**批内禁用名录**内
/// （禁 <c>run_batch</c> 自身 ⇒ 禁止嵌套；禁 <c>set_readonly_mode</c> ⇒ 禁止批内翻转只读闸门绕过）；
/// 调用方可用 <c>allowedTools</c> 进一步**收窄**（不得放宽 —— 收窄后仍须 ⊂ 已注册集）。</item>
/// <item><b>广度上限</b>：单次最多 <see cref="MaxItems"/> 项（防"一条请求做无限事"）。</item>
/// <item><b>总耗时硬约束</b>：默认预算 <see cref="DefaultBudgetMs"/> = 30 s（可下调，**不得上调**）；
/// 预算耗尽 ⇒ **截断**（后续项标记 <c>budget-exceeded</c> 且不执行）并如实报告已完成项。</item>
/// <item><b>不豁免</b>：每项仍走**同一**守卫/confirm/审计/只读闸门 —— 批处理不提供任何绕过通道。</item>
/// </list>
/// </summary>
public static class BatchPolicy
{
    /// <summary>默认总耗时预算（毫秒）—— 工单硬约束：≤30 s。</summary>
    public const int DefaultBudgetMs = 30_000;

    /// <summary>预算上限（毫秒）：请求值超过即钳制到该值（**只允许更严，不允许更松**）。</summary>
    public const int MaxBudgetMs = 30_000;

    /// <summary>单次批处理项数上限。</summary>
    public const int MaxItems = 50;

    /// <summary>批内禁用名录（安全重点）。</summary>
    public static readonly IReadOnlyCollection<string> DeniedInBatch = new HashSet<string>(StringComparer.Ordinal)
    {
        "run_batch",         // 禁止嵌套（否则预算/审计语义不可控）
        "set_readonly_mode", // 禁止批内翻转只读闸门（防绕过）
    };

    /// <summary>钳制预算：null/≤0 ⇒ 默认；&gt;30 s ⇒ 30 s。</summary>
    public static int ClampBudget(int? requestedMs)
        => requestedMs is null || requestedMs.Value <= 0
            ? DefaultBudgetMs
            : Math.Min(requestedMs.Value, MaxBudgetMs);

    /// <summary>
    /// 批次**执行前**静态校验。返回被拒绝的工具名（空 = 全部准入）。
    /// <paramref name="isRegistered"/> 由调用方以注册表判定（本层不持有注册表）。
    /// </summary>
    public static IReadOnlyList<string> Validate(
        IReadOnlyList<BatchItemRequest>? items,
        Func<string, bool> isRegistered,
        IReadOnlyList<string>? allowedTools = null)
    {
        var rejected = new List<string>();
        if (items is null)
        {
            return rejected;
        }

        var allow = NormalizeAllowed(allowedTools);
        foreach (var item in items)
        {
            var name = item?.Tool;
            if (string.IsNullOrWhiteSpace(name))
            {
                rejected.Add("<empty>");
                continue;
            }

            if (DeniedInBatch.Contains(name)
                || !isRegistered(name)
                || (allow is not null && !allow.Contains(name)))
            {
                rejected.Add(name);
            }
        }

        return rejected;
    }

    private static HashSet<string>? NormalizeAllowed(IReadOnlyList<string>? allowedTools)
    {
        if (allowedTools is null || allowedTools.Count == 0)
        {
            return null;   // 未提供 ⇒ 不额外收窄
        }

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in allowedTools)
        {
            if (!string.IsNullOrWhiteSpace(t))
            {
                set.Add(t);
            }
        }

        return set.Count == 0 ? null : set;
    }

    /// <summary>项数超限判定。</summary>
    public static bool ExceedsItemLimit(int count) => count > MaxItems;

    /// <summary>预算耗尽判定（已用 &gt;= 预算 ⇒ 后续项不再执行）。</summary>
    public static bool BudgetExhausted(long elapsedMs, int budgetMs) => elapsedMs >= budgetMs;
}
