using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.Server;
using ArcGISProMCP.Server.Transport;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.ServerTests;

/// <summary>Server 测试工厂：用 FakeArcGISHost 组装 McpServer。</summary>
internal static class TestServerFactory
{
    public static McpServer Create(params IMCPTool[] extraTools)
        => CreateWithHostAndSettings(new FakeArcGISHost(), null, new MCPSettings(), extraTools);

    public static McpServer CreateWithHost(
        IArcGISHost host,
        IPythonBridgeService? pythonBridge,
        params IMCPTool[] extraTools)
        => CreateWithHostAndSettings(host, pythonBridge, new MCPSettings(), extraTools);

    public static McpServer CreateWithSettings(
        MCPSettings settings,
        params IMCPTool[] extraTools)
        => CreateWithHostAndSettings(new FakeArcGISHost(), null, settings, extraTools);

    private static McpServer CreateWithHostAndSettings(
        IArcGISHost host,
        IPythonBridgeService? pythonBridge,
        MCPSettings settings,
        params IMCPTool[] extraTools)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(settings);

        var registry = new MCPToolRegistry();
        registry.Register(new PingTool());
        registry.Register(new GetCurrentMapTool());
        registry.Register(new GetLayersTool());
        registry.Register(new GetProjectInfoTool());
        registry.Register(new GetArcGISVersionTool());
        registry.Register(new GetLicenseInfoTool());
        foreach (var tool in extraTools)
        {
            registry.Register(tool);
        }

        var router = new MCPToolRouter(registry, host, settings, NullLogger.Instance, pythonBridge);
        var transport = new NullMcpTransport();
        return new McpServer(transport, registry, router, settings, NullLogger.Instance);
    }

    public static HttpMcpTransport CreateHttpTransport(int port)
        => new("127.0.0.1", port, "/mcp", NullLogger.Instance);

    private sealed class NullMcpTransport : IMcpTransport
    {
        public bool IsRunning => false;

        public Func<string, CancellationToken, Task<string?>>? RequestHandler { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendAsync(ITransportConnection connection, string message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
