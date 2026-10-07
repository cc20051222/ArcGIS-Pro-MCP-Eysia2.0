using System.Text.Json;

namespace ArcGISProMCP.Core.PythonBridge;

/// <summary>进程管理器发送请求后的结果（成功→OK JsonElement；失败→errorCode/message）。</summary>
public sealed record PythonBridgeCallResult(bool Ok, JsonElement? Data, string? ErrorCode, string? ErrorMessage)
{
    public static PythonBridgeCallResult Succeeded(JsonElement? data)
        => new(true, data, null, null);

    public static PythonBridgeCallResult Failed(string code, string message)
        => new(false, null, code, message);
}
