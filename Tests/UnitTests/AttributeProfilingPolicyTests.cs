using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-040 B/C 组：AttributeService 族访问语义分流 + 大表保护 / 钳制纯函数。
/// 说明：UnitTests 工程不引用 Compatibility（引用方向），故判定逻辑抽为 Core 纯函数
/// AttributeProfilingPolicy（真实 AttributeService / GeoprocessingService 统一调用同一实现——
/// "既可单测、又被真实调用"；四站点直测受引用方向限制，等价性由同源 policy + 阶段二 LIVE L4 保障）。
/// </summary>
public class AttributeProfilingPolicyTests
{
    // ---------- B 组：ClassifyLayerAccess 语义分流（G-82-C） ----------

    [Theory]
    [InlineData("L_Points")]
    [InlineData("L_Sub_A")]
    [InlineData("L_Lines")]
    [InlineData("T_CLAMP")]
    public void Classify_BrokenLayer_ReturnsDataSourceUnavailable_WithLayerName(string layerName)
    {
        // 四站点（QueryFeatures/GetFieldInfo/GetFeatureCount/GetFieldValues）共用同一 policy；
        // 参数化覆盖四站点各自的 layerName 形态，防"只改一处"。
        var (code, message) = AttributeProfilingPolicy.ClassifyLayerAccess(
            layerExists: true, isFeatureLayer: true, tableAvailable: false, layerName: layerName);

        Assert.Equal(ErrorCodes.LayerDataSourceUnavailable, code);
        Assert.Contains(layerName, message, StringComparison.Ordinal);
        Assert.Contains("data source is unavailable", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_MissingLayer_ReturnsLayerNotFound()
    {
        var (code, message) = AttributeProfilingPolicy.ClassifyLayerAccess(
            layerExists: false, isFeatureLayer: false, tableAvailable: false, layerName: "GHOST");

        Assert.Equal(ErrorCodes.LayerNotFound, code);
        Assert.Equal("Layer 'GHOST' not found.", message);
    }

    [Fact]
    public void Classify_NonFeatureLayer_ReturnsLayerNotFound()
    {
        // 组图层等非 BasicFeatureLayer：原实现同样归入"对象不存在"语义（保持不回退）。
        var (code, message) = AttributeProfilingPolicy.ClassifyLayerAccess(
            layerExists: true, isFeatureLayer: false, tableAvailable: false, layerName: "L_Group");

        Assert.Equal(ErrorCodes.LayerNotFound, code);
        Assert.Equal("Layer 'L_Group' not found.", message);
    }

    [Fact]
    public void Classify_HealthyLayer_ReturnsPass()
    {
        var (code, message) = AttributeProfilingPolicy.ClassifyLayerAccess(
            layerExists: true, isFeatureLayer: true, tableAvailable: true, layerName: "L_Points");

        Assert.Null(code);
        Assert.Null(message);
    }

    [Fact]
    public void Classify_TwoFailureBranches_AreStrictlyDistinguishable()
    {
        // G-82-C：'不存在' 与 '存在但不可用' 必须可分。
        var missing = AttributeProfilingPolicy.ClassifyLayerAccess(false, false, false, "X");
        var broken = AttributeProfilingPolicy.ClassifyLayerAccess(true, true, false, "X");
        Assert.NotEqual(missing.Code, broken.Code);
    }

    // ---------- C 组：大表保护（边界三连 + 消息逐字） ----------

    [Theory]
    [InlineData(199_999L, false)]
    [InlineData(200_000L, false)]   // 200_000 恰好 = MaxScanRows → 放行（> 而非 >=）
    [InlineData(200_001L, true)]
    public void ExceedsScanLimit_Boundary(long total, bool expected)
    {
        Assert.Equal(expected, AttributeProfilingPolicy.ExceedsScanLimit(total, 200_000L));
    }

    [Fact]
    public void BuildScanLimitMessage_Verbatim()
    {
        var msg = AttributeProfilingPolicy.BuildScanLimitMessage("T_BIG", 200_001L, 200_000L);
        // 与 D-041 LIVE 基线逐字一致。
        Assert.Contains("exceeds the field-value profiling limit (200000)", msg, StringComparison.Ordinal);
        Assert.Equal(
            "table 'T_BIG' has 200001 rows which exceeds the field-value profiling limit (200000); " +
            "use a smaller dataset or a scoped subset instead of an unbounded scan.",
            msg);
    }

    // ---------- C 组：maxDistinct 钳制（三连 + 负例） ----------

    [Theory]
    [InlineData(0, 200)]        // 0 → 缺省 200
    [InlineData(-5, 200)]       // 负数 → 缺省 200
    [InlineData(2, 2)]          // 小于缺省 → 原样
    [InlineData(200, 200)]      // 恰为缺省
    [InlineData(2000, 2000)]    // 恰为上限
    [InlineData(2001, 2000)]    // 超上限 → 钳到 2000
    [InlineData(5000, 2000)]    // D-041 V2 LIVE 口径
    public void ResolveDistinctLimit_Clamps(int raw, int expected)
    {
        Assert.Equal(expected, AttributeProfilingPolicy.ResolveDistinctLimit(raw, 200, 2000));
    }
}
