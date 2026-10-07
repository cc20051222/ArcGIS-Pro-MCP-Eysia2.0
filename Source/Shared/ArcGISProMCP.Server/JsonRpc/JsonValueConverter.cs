using System.Text.Json;

namespace ArcGISProMCP.Server.JsonRpc;

/// <summary>
/// 把 JSON 参数（JsonElement）递归转换为 CLR 基础类型，
/// 使工具能以 string / bool / double / 嵌套字典 / 列表读取参数。
/// </summary>
public static class JsonValueConverter
{
    public static object? Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var i) ? i : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => Convert(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(Convert).ToList(),
        _ => element.GetRawText()
    };
}
