using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArcGISProMCP.Core.PythonBridge;

/// <summary>
/// bridge_runner.py 返回的 NDJSON 响应（每行一个 JSON）。ok=false 时 error 非空。
/// result 为 JSON 元素，供调用方自行反序列化为具体类型。
/// </summary>
public sealed class PythonBridgeResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    public PythonBridgeError? Error { get; set; }

    public bool IsOk => Ok && Error is null;

    public static PythonBridgeResponse? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PythonBridgeResponse>(line, PythonBridgeJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>bridge 返回的错误结构（透传，非本系统错误码）。</summary>
public sealed class PythonBridgeError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    public override string ToString()
        => $"{Code}: {Message}{(string.IsNullOrEmpty(Details) ? "" : " | " + Details)}";
}

/// <summary>共享 JSON 选项（大小写不敏感，便于容错）。</summary>
public static class PythonBridgeJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
