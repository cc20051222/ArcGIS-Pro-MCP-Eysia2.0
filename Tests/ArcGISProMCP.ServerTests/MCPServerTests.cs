using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Server;
using ArcGISProMCP.Server.JsonRpc;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.ServerTests;

/// <summary>MCPServer 行为测试（通过 HandleRequestAsync 直连，无 HTTP）。</summary>
public class MCPServerTests
{
    private static McpServer Build(params IMCPTool[] extraTools) => TestServerFactory.Create(extraTools);

    private static async Task<JsonElement> CallAsync(
        McpServer server,
        string json,
        CancellationToken cancellationToken = default)
    {
        var response = await server.HandleRequestAsync(json, cancellationToken);
        Assert.NotNull(response);
        return JsonDocument.Parse(response!).RootElement;
    }

    [Fact]
    public async Task Initialize_Returns_ServerInfo_And_Tools_Capability()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}""";

        var root = await CallAsync(server, json);

        var result = root.GetProperty("result");
        Assert.Equal("2024-11-05", result.GetProperty("protocolVersion").GetString());
        Assert.True(result.GetProperty("capabilities").GetProperty("tools").ValueKind == JsonValueKind.Object);
        Assert.Equal("arcgis-pro-mcp", result.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.False(string.IsNullOrEmpty(result.GetProperty("serverInfo").GetProperty("version").GetString()));
    }

    [Fact]
    public async Task InvalidInitialize_Returns_InvalidParams()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":2,"method":"initialize","params":"not-an-object"}""";

        var root = await CallAsync(server, json);

        Assert.Equal(JsonRpcErrorCodes.InvalidParams, root.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Initialize_WithoutParams_Succeeds()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":15,"method":"initialize"}""";

        var root = await CallAsync(server, json);

        Assert.Equal("2024-11-05", root.GetProperty("result").GetProperty("protocolVersion").GetString());
    }

    [Fact]
    public async Task Initialize_NullParams_Returns_InvalidParams()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":16,"method":"initialize","params":null}""";

        var root = await CallAsync(server, json);

        Assert.Equal(JsonRpcErrorCodes.InvalidParams, root.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Initialized_Notification_Returns_Null()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";

        var response = await server.HandleRequestAsync(json);

        Assert.Null(response);
    }

    [Fact]
    public async Task ListTools_Returns_Six_Tools()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":3,"method":"tools/list"}""";

        var root = await CallAsync(server, json);
        var tools = root.GetProperty("result").GetProperty("tools");

        Assert.Equal(6, tools.GetArrayLength());
        var names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToArray();
        Assert.Contains("ping", names);
        Assert.Contains("get_current_map", names);
        Assert.Contains("get_layers", names);
        Assert.Contains("get_project_info", names);
        Assert.Contains("get_arcgis_version", names);
        Assert.Contains("get_license_info", names);
    }

    [Fact]
    public async Task StringRequestId_IsPreservedInResponse()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":"request-17","method":"tools/list"}""";

        var root = await CallAsync(server, json);

        Assert.Equal(JsonValueKind.String, root.GetProperty("id").ValueKind);
        Assert.Equal("request-17", root.GetProperty("id").GetString());
    }

    [Fact]
    public async Task ToolSchema_Is_JsonSchema()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":4,"method":"tools/list"}""";

        var root = await CallAsync(server, json);
        var tools = root.GetProperty("result").GetProperty("tools");

        foreach (var tool in tools.EnumerateArray())
        {
            var schema = tool.GetProperty("inputSchema");
            Assert.Equal("object", schema.GetProperty("type").GetString());
            Assert.Equal(JsonValueKind.Object, schema.GetProperty("properties").ValueKind);
        }
    }

    [Fact]
    public async Task Ping_Returns_Pong()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"ping","arguments":{}}}""";

        var root = await CallAsync(server, json);
        var result = root.GetProperty("result");

        Assert.False(result.GetProperty("isError").GetBoolean());
        var content = result.GetProperty("content");
        Assert.Equal(1, content.GetArrayLength());
        Assert.Equal("text", content[0].GetProperty("type").GetString());
        Assert.Equal("pong", content[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task GetCurrentMap_Returns_MapInfo()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"get_current_map","arguments":{}}}""";

        var root = await CallAsync(server, json);
        var text = root.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();

        Assert.Contains("TestMap", text);
    }

    [Fact]
    public async Task GetLayers_Returns_Layers()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"get_layers","arguments":{}}}""";

        var root = await CallAsync(server, json);
        var text = root.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();

        Assert.Contains("Roads", text);
    }

    [Fact]
    public async Task UnknownTool_Returns_IsError()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"does_not_exist"}}""";

        var root = await CallAsync(server, json);
        var result = root.GetProperty("result");

        Assert.True(result.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task ToolsCall_WithoutArguments_SucceedsForNoArgumentTool()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":18,"method":"tools/call","params":{"name":"ping"}}""";

        var root = await CallAsync(server, json);

        Assert.False(root.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task ToolsCall_MissingName_Returns_InvalidParams()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":19,"method":"tools/call","params":{"arguments":{}}}""";

        var root = await CallAsync(server, json);

        Assert.Equal(JsonRpcErrorCodes.InvalidParams, root.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task ToolsCall_NonStringName_Returns_InvalidParams()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":20,"method":"tools/call","params":{"name":42}}""";

        var root = await CallAsync(server, json);

        Assert.Equal(JsonRpcErrorCodes.InvalidParams, root.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task ToolsCall_NullParams_Returns_InvalidParams()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":21,"method":"tools/call","params":null}""";

        var root = await CallAsync(server, json);

        Assert.Equal(JsonRpcErrorCodes.InvalidParams, root.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task ToolsCall_NonObjectArguments_ReachCurrentRouterValidation()
    {
        var server = Build(new RequiredArgumentTool());
        const string nullArguments = """{"jsonrpc":"2.0","id":22,"method":"tools/call","params":{"name":"required-tool","arguments":null}}""";
        const string arrayArguments = """{"jsonrpc":"2.0","id":23,"method":"tools/call","params":{"name":"required-tool","arguments":[]}}""";

        var nullRoot = await CallAsync(server, nullArguments);
        var arrayRoot = await CallAsync(server, arrayArguments);

        AssertMcpInvalidArgumentResult(nullRoot);
        AssertMcpInvalidArgumentResult(arrayRoot);
    }

    [Fact]
    public async Task InvalidArguments_Returns_IsError()
    {
        var server = Build(new RequiredArgumentTool());
        const string json = """{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"required-tool","arguments":{}}}""";

        var root = await CallAsync(server, json);
        var result = root.GetProperty("result");

        Assert.True(result.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task ToolException_Returns_IsError()
    {
        var server = Build(new ThrowingTool());
        const string json = """{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"throwing-tool"}}""";

        var root = await CallAsync(server, json);
        var result = root.GetProperty("result");

        Assert.True(result.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task UnknownMethod_Returns_MethodNotFound()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":11,"method":"no_such_method"}""";

        var root = await CallAsync(server, json);

        Assert.Equal(JsonRpcErrorCodes.MethodNotFound, root.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task InvalidParams_Returns_InvalidParams()
    {
        var server = Build();
        const string json = """{"jsonrpc":"2.0","id":12,"method":"tools/call","params":"not-an-object"}""";

        var root = await CallAsync(server, json);

        Assert.Equal(JsonRpcErrorCodes.InvalidParams, root.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task RequestTimeout_CancelsToolExecution()
    {
        var tool = new CancellationProbeTool();
        var server = TestServerFactory.CreateWithSettings(
            new MCPSettings { RequestTimeoutMs = 50 },
            tool);
        const string json = """{"jsonrpc":"2.0","id":24,"method":"tools/call","params":{"name":"cancellation-probe"}}""";

        var requestTask = server.HandleRequestAsync(json);
        await tool.Entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(response);
        await tool.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));

        using var document = JsonDocument.Parse(response!);
        var result = document.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        // Phase 8.4（D-016，G-37）：服务器超时（非用户取消）→ REQUEST_TIMEOUT (executing)。
        Assert.Contains("REQUEST_TIMEOUT (executing)", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task CallerCancellation_PropagatesToToolExecution()
    {
        var tool = new CancellationProbeTool();
        var server = Build(tool);
        using var cancellation = new CancellationTokenSource();
        const string json = """{"jsonrpc":"2.0","id":25,"method":"tools/call","params":{"name":"cancellation-probe"}}""";

        var requestTask = server.HandleRequestAsync(json, cancellation.Token);
        await tool.Entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();
        var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(response);
        await tool.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));

        using var document = JsonDocument.Parse(response!);
        var result = document.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains(ErrorCodes.Cancelled, result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task InternalError_Returns_InternalError()
    {
        var server = Build(new CircularDataTool());
        const string json = """{"jsonrpc":"2.0","id":13,"method":"tools/call","params":{"name":"circular-tool"}}""";

        var root = await CallAsync(server, json);

        Assert.Equal(JsonRpcErrorCodes.InternalError, root.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task ProductionToolFailure_RendersCodeAndMessageThroughMcpBoundary()
    {
        var gp = new RecordingGeoprocessingService
        {
            Result = OperationResult<ArcGISProMCP.Core.Models.GeoprocessingResult>.Fail(
                ErrorCodes.GeoprocessingError,
                "controlled GP failure")
        };
        var server = TestServerFactory.CreateWithHost(
            new FakeArcGISHost(gp),
            null,
            new BufferTool());
        const string json = "{\"jsonrpc\":\"2.0\",\"id\":14,\"method\":\"tools/call\",\"params\":{\"name\":\"buffer\",\"arguments\":{\"input\":\"roads\",\"output\":\"out\",\"distance\":1}}}";

        var root = await CallAsync(server, json);
        var result = root.GetProperty("result");
        var text = result.GetProperty("content")[0].GetProperty("text").GetString();

        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains(ErrorCodes.GeoprocessingError, text);
        Assert.Contains("controlled GP failure", text);
    }

    private static void AssertMcpInvalidArgumentResult(JsonElement root)
    {
        var result = root.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains(ErrorCodes.InvalidArgument, result.GetProperty("content")[0].GetProperty("text").GetString());
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
        public IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>()
        };

        public Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
            => throw new InvalidOperationException("boom");
    }

    private sealed class CircularDataTool : IMCPTool
    {
        public string Name => "circular-tool";
        public string Description => "returns non-serializable data";
        public IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>()
        };

        public Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        {
            var node = new Node();
            node.Self = node;
            return Task.FromResult(OperationResult<object?>.Ok(node));
        }

        private sealed class Node
        {
            public Node? Self { get; set; }
        }
    }

    private sealed class CancellationProbeTool : IMCPTool
    {
        public string Name => "cancellation-probe";
        public string Description => "waits until its execution token is cancelled";
        public IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>()
        };

        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        {
            Entered.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
                return OperationResult<object?>.Ok("unexpected completion");
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                CancellationObserved.TrySetResult(true);
                throw;
            }
        }
    }
}
