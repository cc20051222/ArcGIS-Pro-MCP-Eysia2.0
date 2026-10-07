using System.Text.Json;

namespace ArcGISProMCP.Protocol;

/// <summary>JSON-RPC 2.0 请求。</summary>
public sealed class JsonRpcRequest
{
    public string Jsonrpc { get; set; } = "2.0";

    /// <summary>请求 id（字符串/数字/null）。通知则无 id。</summary>
    public JsonElement? Id { get; set; }

    public string Method { get; set; } = string.Empty;

    public JsonElement? Params { get; set; }

    public bool IsNotification => Id is null || (Id.Value.ValueKind == JsonValueKind.Null);
}

/// <summary>JSON-RPC 2.0 成功响应。</summary>
public sealed class JsonRpcResponse
{
    public string Jsonrpc { get; set; } = "2.0";
    public JsonElement? Id { get; set; }
    public JsonElement? Result { get; set; }
}

/// <summary>JSON-RPC 2.0 错误响应。</summary>
public sealed class JsonRpcErrorResponse
{
    public string Jsonrpc { get; set; } = "2.0";
    public JsonElement? Id { get; set; }
    public JsonRpcError? Error { get; set; }
}

/// <summary>JSON-RPC 2.0 错误对象。</summary>
public sealed class JsonRpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public JsonElement? Data { get; set; }
}
