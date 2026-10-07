using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>工具参数读取助手（从 ToolExecutionContext.Arguments 读取强类型值）。</summary>
internal static class ToolResult
{
    public static OperationResult<object?> From<T>(OperationResult<T> r)
        => r.Success
            ? OperationResult<object?>.Ok(r.Data, r.Message)
            : OperationResult<object?>.Fail(r.Errors);
}

/// <summary>工具参数读取助手（从 ToolExecutionContext.Arguments 读取强类型值）。</summary>
internal static class ToolArgs
{
    public static string? GetString(ToolExecutionContext context, string key)
    {
        if (context.Arguments is not null && context.Arguments.TryGetValue(key, out var v) && v is string s)
        {
            return s;
        }

        return null;
    }

    public static int? GetInt(ToolExecutionContext context, string key)
    {
        if (context.Arguments is not null && context.Arguments.TryGetValue(key, out var v))
        {
            if (v is int i)
            {
                return i;
            }

            if (v is long l && l >= int.MinValue && l <= int.MaxValue)
            {
                return (int)l;
            }

            if (v is double d)
            {
                return (int)d;
            }

            if (v is string s && double.TryParse(s, out var sd))
            {
                return (int)sd;
            }
        }

        return null;
    }

    public static double? GetDouble(ToolExecutionContext context, string key)
    {
        if (context.Arguments is not null && context.Arguments.TryGetValue(key, out var v))
        {
            if (v is double d)
            {
                return d;
            }

            if (v is int i)
            {
                return i;
            }

            if (v is long l)
            {
                return l;
            }

            if (v is string s && double.TryParse(s, out var sd))
            {
                return sd;
            }
        }

        return null;
    }

    public static bool GetBool(ToolExecutionContext context, string key, bool defaultValue = false)
    {
        if (context.Arguments is not null && context.Arguments.TryGetValue(key, out var v))
        {
            if (v is bool b)
            {
                return b;
            }

            if (v is string s && bool.TryParse(s, out var sb))
            {
                return sb;
            }
        }

        return defaultValue;
    }

    // ══════════════════ D-064 新增：复合参数读取（数组/对象）══════════════════
    //  反序列化形态（JsonValueConverter）：array → List<object?>；object → Dictionary<string, object?>；
    //  number → long|double；bool → bool；string → string。以下助手统一收敛形态差异。

    /// <summary>读取原始值（无形态转换）。</summary>
    public static object? GetValue(ToolExecutionContext context, string key)
        => context.Arguments is not null && context.Arguments.TryGetValue(key, out var v) ? v : null;

    /// <summary>读取数组参数；非数组/缺失 ⇒ 空列表。</summary>
    public static IReadOnlyList<object?> GetList(ToolExecutionContext context, string key)
    {
        var v = GetValue(context, key);
        return Flatten(v);
    }

    /// <summary>读取数组参数并逐项转字符串（跳过 null/空白项）。</summary>
    public static IReadOnlyList<string> GetStringList(ToolExecutionContext context, string key)
    {
        var v = GetValue(context, key);
        if (v is string s)
        {
            // 兼容"分号分隔"的退化形态（与既有 GP 多值参数风格一致）。
            return s.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
        }

        return Flatten(v)
            .Select(x => x?.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();
    }

    /// <summary>读取对象参数（字典形态）；非对象/缺失 ⇒ null。</summary>
    public static IReadOnlyDictionary<string, object?>? GetObject(ToolExecutionContext context, string key)
    {
        var v = GetValue(context, key);
        return v switch
        {
            IReadOnlyDictionary<string, object?> ro => ro,
            IDictionary<string, object?> d => new Dictionary<string, object?>(d),
            _ => null,
        };
    }

    /// <summary>读取"对象数组"参数（如 batch items / field specs）；元素非对象 ⇒ 保留 null 占位。</summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, object?>?> GetObjectList(ToolExecutionContext context, string key)
    {
        var items = GetList(context, key);
        var outList = new List<IReadOnlyDictionary<string, object?>?>(items.Count);
        foreach (var item in items)
        {
            outList.Add(item switch
            {
                IReadOnlyDictionary<string, object?> ro => ro,
                IDictionary<string, object?> d => new Dictionary<string, object?>(d),
                _ => null,
            });
        }

        return outList;
    }

    /// <summary>从任意 object 形态读取子值（字典键查找；形态不匹配 ⇒ null）。</summary>
    public static object? Read(object? container, string key)
    {
        switch (container)
        {
            case IReadOnlyDictionary<string, object?> ro when ro.TryGetValue(key, out var v):
                return v;
            case IDictionary<string, object?> d when d.TryGetValue(key, out var v2):
                return v2;
            case System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Object
                                                      && je.TryGetProperty(key, out var pv):
                return pv.Clone();
            default:
                return null;
        }
    }

    /// <summary>从字典读取字符串子值。</summary>
    public static string? ReadString(object? container, string key)
    {
        var v = Read(container, key);
        if (v is null)
        {
            return null;
        }

        var s = v switch
        {
            string str => str,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString(),
            _ => v.ToString(),
        };

        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    /// <summary>从字典读取布尔子值（缺省 false）。</summary>
    public static bool ReadBool(object? container, string key, bool defaultValue = false)
    {
        var v = Read(container, key);
        return v switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var sb) => sb,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.False => false,
            _ => defaultValue,
        };
    }

    /// <summary>从字典读取整数子值。</summary>
    public static int? ReadInt(object? container, string key)
    {
        var v = Read(container, key);
        return v switch
        {
            int i => i,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, out var si) => si,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number
                                                 && je.TryGetInt64(out var jl) => (int)jl,
            _ => null,
        };
    }

    /// <summary>把 object? 收敛为列表（List/数组/JSON 数组）。</summary>
    private static List<object?> Flatten(object? v)
    {
        switch (v)
        {
            case null:
                return new List<object?>();
            case List<object?> list:
                return list;
            case System.Collections.IEnumerable en when v is not string:
            {
                var result = new List<object?>();
                foreach (var item in en)
                {
                    result.Add(item);
                }

                return result;
            }
            case System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Array:
                return je.EnumerateArray().Select(x => (object?)x.Clone()).ToList();
            default:
                return new List<object?> { v };
        }
    }
}
