namespace ArcGISProMCP.Server.JsonRpc;

/// <summary>未知 MCP method（映射为 JSON-RPC -32601）。</summary>
public sealed class JsonRpcMethodNotFoundException : Exception
{
    public JsonRpcMethodNotFoundException(string method)
        : base($"Method not found: {method}")
    {
    }
}

/// <summary>非法参数（映射为 JSON-RPC -32602）。</summary>
public sealed class JsonRpcInvalidParamsException : Exception
{
    public JsonRpcInvalidParamsException(string message)
        : base(message)
    {
    }
}
