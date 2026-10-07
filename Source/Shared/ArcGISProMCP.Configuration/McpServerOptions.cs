namespace ArcGISProMCP.Configuration;

/// <summary>MCP Server 配置。</summary>
public sealed class McpServerOptions
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 6520;
    public const string DefaultEndpointPath = "/mcp";

    public string Host { get; set; } = DefaultHost;
    public int Port { get; set; } = DefaultPort;
    public string EndpointPath { get; set; } = DefaultEndpointPath;
    public string Transport { get; set; } = "http";
}
