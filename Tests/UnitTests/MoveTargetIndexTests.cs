using ArcGISProMCP.Core.Services;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-044（O-D043-04 返工）：<see cref="MoveTargetIndex"/> 纯函数的位移索引矩阵。
/// ★ 本族用例**能抓住本缺陷**：把实现回退为历史公式（<c>AFTER ? r+1 : r</c>）后，
///   <c>s &lt; r</c> 与 <c>s == r</c> 的用例**必须变红**（反证留档见本报告 §R4）。
/// 语义依据：<c>Map.MoveLayer(Layer, int)</c> 的 index 是**移除源层之后**列表的插入索引。
/// </summary>
public class MoveTargetIndexTests
{
    /// <summary>复刻 SDK「先移除源、再按 target 插入」语义（与 LayerService 调用面同源）。</summary>
    internal static List<string> ApplySdkMove(IReadOnlyList<string> layers, int sourceIndex, int targetIndex)
    {
        var src = layers[sourceIndex];
        var rest = layers.Where((_, i) => i != sourceIndex).ToList();
        var t = Math.Max(0, Math.Min(targetIndex, rest.Count));
        var result = new List<string>(rest.Take(t));
        result.Add(src);
        result.AddRange(rest.Skip(t));
        return result;
    }

    private static string Move(IReadOnlyList<string> layers, int s, int r, string pos)
    {
        Assert.True(MoveTargetIndex.TryCompute(s, r, pos, layers.Count, out var plan),
            $"TryCompute 应成功: s={s} r={r} pos={pos}");
        if (plan.IsNoOp)
        {
            return string.Join(",", layers);
        }

        return string.Join(",", ApplySdkMove(layers, s, plan.TargetIndex));
    }

    private static readonly string[] Four = { "A", "B", "C", "D" };
    private static readonly string[] Seq9 = { "P_PTS", "Q_PTS", "E_PTS", "L_Group", "M_PTS" };

    // ---------- 正例：逐字顺序断言 ----------

    [Fact]
    public void Top_PutsSourceFirst()
        => Assert.Equal("C,A,B,D", Move(Four, 2, -1, "TOP"));

    [Fact]
    public void Bottom_PutsSourceLast()
        => Assert.Equal("A,C,D,B", Move(Four, 1, -1, "BOTTOM"));

    [Fact]
    public void SourceBeforeReference_Before_PutsSourceImmediatelyBeforeReference()
    {
        // s=1(B) < r=2(C)：BEFORE C ⇒ B 紧邻 C 之前 ⇒ A,B,C,D
        Assert.Equal("A,B,C,D", Move(Four, 1, 2, "BEFORE"));
    }

    [Fact]
    public void SourceBeforeReference_After_PutsSourceImmediatelyAfterReference()
    {
        // s=1(B) < r=2(C)：AFTER C ⇒ A,C,B,D
        Assert.Equal("A,C,B,D", Move(Four, 1, 2, "AFTER"));
    }

    [Fact]
    public void SourceAfterReference_Before_PutsSourceImmediatelyBeforeReference()
    {
        // s=2(C) > r=1(B)：BEFORE B ⇒ A,C,B,D
        Assert.Equal("A,C,B,D", Move(Four, 2, 1, "BEFORE"));
    }

    [Fact]
    public void SourceAfterReference_After_PutsSourceImmediatelyAfterReference()
    {
        // s=2(C) > r=1(B)：AFTER B ⇒ A,B,C,D
        Assert.Equal("A,B,C,D", Move(Four, 2, 1, "AFTER"));
    }

    [Fact]
    public void Adjacent_SourceIsJustBeforeReference_After_KeepsAdjacency()
        => Assert.Equal("A,C,B,D", Move(Four, 1, 2, "AFTER"));

    [Fact]
    public void Adjacent_SourceIsJustAfterReference_Before_KeepsAdjacency()
        => Assert.Equal("A,C,B,D", Move(Four, 2, 1, "BEFORE"));

    [Fact]
    public void FarApart_SourceBeforeReference_Before()
        => Assert.Equal("B,C,D,A,E", Move(new[] { "A", "B", "C", "D", "E" }, 0, 4, "BEFORE"));

    [Fact]
    public void FarApart_SourceAfterReference_After()
        => Assert.Equal("A,E,B,C,D", Move(new[] { "A", "B", "C", "D", "E" }, 4, 0, "AFTER"));

    [Fact]
    public void SelfReference_Before_IsIdempotent()
    {
        Assert.True(MoveTargetIndex.TryCompute(1, 1, "BEFORE", Four.Length, out var plan));
        Assert.True(plan.IsNoOp, "源即参考时必须为空操作（不调用 MoveLayer）");
        Assert.Equal(1, plan.TargetIndex);
        Assert.Equal("A,B,C,D", Move(Four, 1, 1, "BEFORE"));
    }

    [Fact]
    public void SelfReference_After_IsIdempotent()
    {
        Assert.True(MoveTargetIndex.TryCompute(1, 1, "AFTER", Four.Length, out var plan));
        Assert.True(plan.IsNoOp, "源即参考时必须为空操作（不调用 MoveLayer）");
        Assert.Equal(1, plan.TargetIndex);
        // ★ 历史实现此处会多移 1 位得到 A,C,B,D —— 本用例正是反证点。
        Assert.Equal("A,B,C,D", Move(Four, 1, 1, "AFTER"));
    }

    [Fact]
    public void LiveDefectReplay_Seq9_SourceBeforeReference_After()
    {
        // D-043 LIVE raw seq9 复刻：s=1(Q_PTS) < r=2(E_PTS)，AFTER E_PTS
        // 期望 Q_PTS 紧跟 E_PTS；历史实现得到 P_PTS,E_PTS,L_Group,Q_PTS,M_PTS（多移 1 位）。
        Assert.Equal("P_PTS,E_PTS,Q_PTS,L_Group,M_PTS", Move(Seq9, 1, 2, "AFTER"));
    }

    [Fact]
    public void PositionIsCaseAndWhitespaceInsensitiveViaNormalize()
    {
        Assert.Equal("TOP", MoveTargetIndex.Normalize(" top "));
        Assert.Equal("BEFORE", MoveTargetIndex.Normalize("before"));
        Assert.Null(MoveTargetIndex.Normalize("   "));
        Assert.Null(MoveTargetIndex.Normalize(null));
    }

    // ---------- 负例 ----------

    [Theory]
    [InlineData("MIDDLE")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("TOPP")]
    public void UnsupportedPosition_IsRejected(string? pos)
        => Assert.False(MoveTargetIndex.TryCompute(1, 2, MoveTargetIndex.Normalize(pos), 4, out _));

    [Fact]
    public void SourceOutOfRange_IsRejected()
    {
        Assert.False(MoveTargetIndex.TryCompute(-1, 2, "BEFORE", 4, out _));
        Assert.False(MoveTargetIndex.TryCompute(4, 2, "BEFORE", 4, out _));
    }

    [Fact]
    public void ReferenceOutOfRangeForBeforeAfter_IsRejected()
    {
        Assert.False(MoveTargetIndex.TryCompute(1, -1, "BEFORE", 4, out _));
        Assert.False(MoveTargetIndex.TryCompute(1, 9, "AFTER", 4, out _));
    }

    [Fact]
    public void EmptyRootContainer_IsRejected()
        => Assert.False(MoveTargetIndex.TryCompute(0, 0, "TOP", 0, out _));
}
