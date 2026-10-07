using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

public class MCPToolRouterTests
{
    private static (MCPToolRouter router, MCPToolRegistry registry) Build(string? toolName = null, params IMCPTool[] extra)
    {
        var registry = new MCPToolRegistry();
        var ping = new PingTool();
        registry.Register(ping);
        foreach (var t in extra)
        {
            registry.Register(t);
        }

        var router = new MCPToolRouter(registry, new FakeArcGISHost(), new MCPSettings(), NullLogger.Instance);
        return (router, registry);
    }

    [Fact]
    public async Task ToolFound_Runs_Successfully()
    {
        var (router, _) = Build();
        var result = await router.ExecuteAsync(new MCPToolCall { Name = "ping" });

        Assert.True(result.Success);
        Assert.Equal("pong", result.Data);
    }

    [Fact]
    public async Task ToolNotFound_Returns_ToolNotFound()
    {
        var (router, _) = Build();
        var result = await router.ExecuteAsync(new MCPToolCall { Name = "nope" });

        Assert.False(result.Success);
        Assert.Contains(ErrorCodes.ToolNotFound, result.Errors.Select(e => e.Code));
    }

    [Fact]
    public async Task InvalidParameter_MissingRequired_Returns_InvalidArgument()
    {
        var requiredTool = new RequiredArgumentTool();
        var (router, _) = Build(extra: requiredTool);

        var result = await router.ExecuteAsync(new MCPToolCall { Name = "required-tool" });

        Assert.False(result.Success);
        Assert.Contains(ErrorCodes.InvalidArgument, result.Errors.Select(e => e.Code));
    }

    [Fact]
    public async Task ToolThrows_Returns_InternalError()
    {
        var (router, _) = Build(extra: new ThrowingTool());

        var result = await router.ExecuteAsync(new MCPToolCall { Name = "throwing-tool" });

        Assert.False(result.Success);
        Assert.Contains(ErrorCodes.InternalError, result.Errors.Select(e => e.Code));
    }

    [Fact]
    public async Task Cancelled_Returns_Cancelled()
    {
        var (router, _) = Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await router.ExecuteAsync(new MCPToolCall { Name = "ping", CancellationToken = cts.Token });

        Assert.False(result.Success);
        Assert.Contains(ErrorCodes.Cancelled, result.Errors.Select(e => e.Code));
    }

    private sealed class RequiredArgumentTool : IMCPTool
    {
        public string Name => "required-tool";
        public string Description => "requires name";
        public IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?> { ["name"] = new Dictionary<string, object?> { ["type"] = "string" } },
            ["required"] = new[] { "name" }
        };

        public Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
            => Task.FromResult(OperationResult<object?>.Ok("ok"));
    }

    private sealed class ThrowingTool : IMCPTool
    {
        public string Name => "throwing-tool";
        public string Description => "always throws";
        public IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>();

        public Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
            => throw new InvalidOperationException("boom");
    }
}
