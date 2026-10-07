namespace ArcGISProMCP.Tools;

/// <summary>
/// D-062 · 复合参数读取助手。服务端 JsonValueConverter 已把 JSON 递归转换为 CLR 原生类型：
/// object → Dictionary&lt;string, object?&gt;、array → List&lt;object?&gt;、number → long/double、bool → bool。
/// </summary>
internal static class JsonHelpers
{
    public static IReadOnlyDictionary<string, object?>? ToNamedDictionary(object? raw)
        => raw is IReadOnlyDictionary<string, object?> dict ? dict : null;

    public static IReadOnlyList<string>? ToStringList(object? raw)
    {
        if (raw is not IEnumerable<object?> list || raw is string)
        {
            return null;
        }

        return list.Select(v => v?.ToString() ?? string.Empty).ToList();
    }

    /// <summary>行数组 → 字典列表（任一元素非对象 → null，由调用方拒绝）。</summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, object?>>? ToRowList(object? raw)
    {
        if (raw is not IEnumerable<object?> list || raw is string)
        {
            return null;
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var item in list)
        {
            if (item is not IReadOnlyDictionary<string, object?> row)
            {
                return null;
            }

            rows.Add(row);
        }

        return rows;
    }

    public static IReadOnlyList<long>? ToLongList(object? raw)
    {
        if (raw is not IEnumerable<object?> list || raw is string)
        {
            return null;
        }

        var result = new List<long>();
        foreach (var item in list)
        {
            switch (item)
            {
                case long l:
                    result.Add(l);
                    break;
                case int i:
                    result.Add(i);
                    break;
                case double d when d == Math.Floor(d) && d >= long.MinValue && d <= long.MaxValue:
                    result.Add((long)d);
                    break;
                default:
                    return null;
            }
        }

        return result;
    }
}
