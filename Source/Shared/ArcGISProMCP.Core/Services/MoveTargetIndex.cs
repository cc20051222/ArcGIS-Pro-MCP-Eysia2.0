namespace ArcGISProMCP.Core.Services;

/// <summary><c>move_layer</c> 的位移目标索引计算结果（纯逻辑，无 SDK 依赖）。</summary>
public readonly record struct MoveTargetPlan
{
    /// <summary>传给 <c>Map.MoveLayer(Layer, int)</c> 的 target 索引（"移除源后"列表的插入位）。</summary>
    public int TargetIndex { get; init; }

    /// <summary>为 true 时表示本次为幂等空操作（源即参考），调用方**不应**调用 <c>Map.MoveLayer</c>。</summary>
    public bool IsNoOp { get; init; }
}

/// <summary>
/// <c>move_layer</c> 位移目标索引的**纯函数**（D-044 返工；Rule 5：Shared 不引用 ArcGIS Pro SDK）。
/// </summary>
/// <remarks>
/// ★ 机制根因（务必保留，避免下轮重犯）：
/// <c>ArcGIS.Desktop.Mapping.Map.MoveLayer(Layer layer, int index)</c> 的 <c>index</c> 是
/// **「把源层移除之后」的图层列表**中的插入索引 —— 即"先移除、再按 index 插入"。
/// 因此当源层位于参考层之前（<c>sourceIndex &lt; referenceIndex</c>）时，移除动作会让参考层的
/// 下标整体下移 1 位，索引必须做 −1 补偿；历史实现（<c>AFTER ? idx + 1 : idx</c>）缺该补偿，
/// 导致 <c>s &lt; r</c> 时 BEFORE/AFTER 恒定多移 1 位（缺陷 O-D043-04）。
///
/// 语义（不得回退）：
/// ① <c>TOP</c> → 0；<c>BOTTOM</c> → count − 1；二者与源/参考位置无关；
/// ② <c>BEFORE</c> → (s &lt; r ? r − 1 : r)；<c>AFTER</c> → (s &lt; r ? r : r + 1)；
/// ③ <c>s == r</c>（源即参考）→ **幂等**：<see cref="MoveTargetPlan.IsNoOp"/> = true，
///    <see cref="MoveTargetPlan.TargetIndex"/> = s，调用方不执行移动、直接返回当前顺序；
/// ④ 越界索引 / 非法 position / 空参考（BEFORE、AFTER 必需）→ <c>TryCompute</c> 返回 false，
///    由 SDK 侧调用方维持既有 <c>INVALID_ARGUMENT</c> 消息（本类不生产用户可见文案）。
/// </remarks>
public static class MoveTargetIndex
{
    /// <summary>TOP：移到根容器最前。</summary>
    public const string PositionTop = "TOP";

    /// <summary>BOTTOM：移到根容器最后。</summary>
    public const string PositionBottom = "BOTTOM";

    /// <summary>BEFORE：紧邻参考层之前。</summary>
    public const string PositionBefore = "BEFORE";

    /// <summary>AFTER：紧邻参考层之后。</summary>
    public const string PositionAfter = "AFTER";

    /// <summary>把用户输入归一化为大写 position（空白 → null）。</summary>
    public static string? Normalize(string? position)
    {
        var p = (position ?? string.Empty).Trim();
        return p.Length == 0 ? null : p.ToUpperInvariant();
    }

    /// <summary>是否为受支持的 position（归一化后）。</summary>
    public static bool IsSupported(string? normalizedPosition)
        => normalizedPosition is PositionTop or PositionBottom or PositionBefore or PositionAfter;

    /// <summary>计算位移目标索引；参数非法时返回 false（<paramref name="plan"/> 为 default）。</summary>
    /// <param name="sourceIndex">源层**移动前**在根容器的索引。</param>
    /// <param name="referenceIndex">参考层**移动前**在根容器的索引；TOP/BOTTOM 可传任意值。</param>
    /// <param name="normalizedPosition">已归一化的 position（见 <see cref="Normalize"/>）。</param>
    /// <param name="count">根容器图层总数（移动前）。</param>
    /// <param name="plan">计算出的目标索引与是否为空操作。</param>
    public static bool TryCompute(
        int sourceIndex,
        int referenceIndex,
        string? normalizedPosition,
        int count,
        out MoveTargetPlan plan)
    {
        plan = default;
        if (!IsSupported(normalizedPosition) || count <= 0)
        {
            return false;
        }

        if (sourceIndex < 0 || sourceIndex >= count)
        {
            return false;
        }

        // TOP / BOTTOM：与历史实现逐字一致地照常调用 MoveLayer（结果本就幂等），避免任何行为差。
        if (normalizedPosition is PositionTop)
        {
            plan = new MoveTargetPlan { TargetIndex = 0, IsNoOp = false };
            return true;
        }

        if (normalizedPosition is PositionBottom)
        {
            plan = new MoveTargetPlan { TargetIndex = count - 1, IsNoOp = false };
            return true;
        }

        // BEFORE / AFTER —— 参考层必须在根容器内。
        if (referenceIndex < 0 || referenceIndex >= count)
        {
            return false;
        }

        if (sourceIndex == referenceIndex)
        {
            // 幂等：不移动（返回当前顺序）。
            plan = new MoveTargetPlan { TargetIndex = sourceIndex, IsNoOp = true };
            return true;
        }

        // ★ 补偿：源在参考之前时，移除源后参考下标下移 1 位。
        var beforeReference = sourceIndex < referenceIndex;
        if (normalizedPosition is PositionBefore)
        {
            plan = new MoveTargetPlan
            {
                TargetIndex = beforeReference ? referenceIndex - 1 : referenceIndex,
                IsNoOp = false,
            };
            return true;
        }

        plan = new MoveTargetPlan
        {
            TargetIndex = beforeReference ? referenceIndex : referenceIndex + 1,
            IsNoOp = false,
        };
        return true;
    }
}
