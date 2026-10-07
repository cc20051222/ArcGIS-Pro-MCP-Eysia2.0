namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-062 · C1 统计纯数学（Core 可单测；SDK 无关）。
/// 口径：min/max/sum 直读；mean = 算术均值；median = 排序中位（偶数取均值）；
/// stddev = <b>样本</b>标准差（n-1；n&lt;2 → null）。空值由调用方先行剔除并以 skipped 披露。
/// </summary>
public static class FieldMath
{
    public static (double? Min, double? Max, double? Mean, double? Median, double? Sum, double? StdDev) Reduce(
        IReadOnlyList<double> values)
    {
        if (values is null || values.Count == 0)
        {
            return (null, null, null, null, null, null);
        }

        var sorted = values.OrderBy(v => v).ToList();
        var sum = sorted.Sum();
        var mean = sum / sorted.Count;
        double? median = sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0;
        double? stddev = sorted.Count >= 2
            ? Math.Sqrt(sorted.Sum(v => (v - mean) * (v - mean)) / (sorted.Count - 1))
            : null;

        return (sorted[0], sorted[^1], mean, median, sum, stddev);
    }
}
