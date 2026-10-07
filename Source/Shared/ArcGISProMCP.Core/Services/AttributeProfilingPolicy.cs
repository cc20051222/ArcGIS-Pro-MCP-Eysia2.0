using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-040 B/C 组：AttributeService 族"图层访问/画像"判定的**纯函数**抽取。
/// 目的：让"图层存在但数据源不可用 → LAYER_DATA_SOURCE_UNAVAILABLE"的分流与
/// 大表保护 / maxDistinct 钳制**可被单元测试直接覆盖**（此前位于 Compatibility 真实服务内，
/// 单测工程不引用该工程 ⇒ 全部绕开；O-D038-06）。真实服务（AttributeService / GeoprocessingService）
/// 统一调用本类，保证"既可单测、又被真实调用"同一实现。
/// </summary>
public static class AttributeProfilingPolicy
{
    /// <summary>
    /// B 组分流：按"图层是否存在 / 是否要素图层 / 数据表是否可用"三要素判定错误语义。
    /// 返回 (code, message)；code == null 表示放行（调用方继续使用 table）。
    /// 语义层级（不得混用，G-82-C）：
    ///  - 图层不存在 / 非要素图层 → <see cref="ErrorCodes.LayerNotFound"/>（对象不存在）；
    ///  - 图层存在但数据表不可用（broken / 连接打不开）→ <see cref="ErrorCodes.LayerDataSourceUnavailable"/>。
    /// </summary>
    public static (string? Code, string? Message) ClassifyLayerAccess(
        bool layerExists, bool isFeatureLayer, bool tableAvailable, string layerName)
    {
        if (!layerExists || !isFeatureLayer)
        {
            return (ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
        }

        if (!tableAvailable)
        {
            return (ErrorCodes.LayerDataSourceUnavailable,
                $"Layer '{layerName}' exists but its data source is unavailable " +
                "(the layer is broken or its connection cannot be opened). Verify the layer's data source path.");
        }

        return (null, null);
    }

    /// <summary>C 组：大表保护判定（逐字保持原实现语义：total &gt; maxScanRows 即拒）。</summary>
    public static bool ExceedsScanLimit(long totalRows, long maxScanRows) => totalRows > maxScanRows;

    /// <summary>C 组：拒绝消息拼接（与原实现**逐字一致**，含 layerName / 总数 / 上限）。</summary>
    public static string BuildScanLimitMessage(string layerName, long totalRows, long maxScanRows)
        => $"table '{layerName}' has {totalRows} rows which exceeds the field-value profiling limit ({maxScanRows}); " +
           "use a smaller dataset or a scoped subset instead of an unbounded scan.";

    /// <summary>C 组：maxDistinct 钳制（逐字保持原实现语义：&lt;=0 → default；否则 min(raw, cap)）。</summary>
    public static int ResolveDistinctLimit(int maxDistinctRaw, int defaultMaxDistinct, int maxDistinctCap)
        => maxDistinctRaw <= 0 ? defaultMaxDistinct : Math.Min(maxDistinctRaw, maxDistinctCap);
}
