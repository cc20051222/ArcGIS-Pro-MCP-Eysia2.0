using System.IO;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.Protocol;

namespace ArcGISProMCP.Server.JsonRpc;

/// <summary>
/// 标准 JSON-RPC 2.0 编解码：解析请求、序列化响应/错误。
/// </summary>
public static class JsonRpcCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    /// <summary>
    /// 解析 JSON-RPC 2.0 请求。失败时返回 error。
    /// </summary>
    public static bool TryParseRequest(string json, out JsonRpcRequest? request, out JsonRpcError? error)
    {
        request = null;
        error = null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            error = new JsonRpcError { Code = JsonRpcErrorCodes.ParseError, Message = "Parse error" };
            return false;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = InvalidRequest();
                return false;
            }

            if (!root.TryGetProperty("jsonrpc", out var jsonrpcEl) ||
                jsonrpcEl.ValueKind != JsonValueKind.String ||
                jsonrpcEl.GetString() != "2.0")
            {
                error = InvalidRequest();
                return false;
            }

            if (!root.TryGetProperty("method", out var methodEl) ||
                methodEl.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(methodEl.GetString()))
            {
                error = InvalidRequest();
                return false;
            }

            JsonElement? id = null;
            if (root.TryGetProperty("id", out var idEl))
            {
                if (idEl.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.Null)
                {
                    id = idEl.Clone();
                }
                else
                {
                    error = InvalidRequest();
                    return false;
                }
            }

            JsonElement? prms = null;
            if (root.TryGetProperty("params", out var paramsEl))
            {
                prms = paramsEl.Clone();
            }

            request = new JsonRpcRequest
            {
                Id = id,
                Method = methodEl.GetString()!,
                Params = prms
            };
            return true;
        }
    }

    public static string BuildResponse(JsonElement? id, object? result)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            WriteId(writer, id);
            writer.WritePropertyName("result");
            if (result is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                JsonSerializer.Serialize(writer, result, result.GetType(), Options);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static string BuildError(JsonElement? id, int code, string message)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            WriteId(writer, id);
            writer.WritePropertyName("error");
            writer.WriteStartObject();
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteId(Utf8JsonWriter writer, JsonElement? id)
    {
        writer.WritePropertyName("id");
        if (id is { } idEl)
        {
            idEl.WriteTo(writer);
        }
        else
        {
            writer.WriteNullValue();
        }
    }

    private static JsonRpcError InvalidRequest()
        => new() { Code = JsonRpcErrorCodes.InvalidRequest, Message = "Invalid Request" };
}
