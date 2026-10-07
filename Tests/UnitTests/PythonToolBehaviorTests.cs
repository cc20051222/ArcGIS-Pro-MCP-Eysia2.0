using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 5.7.2 behavior tests for the five production Python facade tools.
/// Calls stop at the injected interface; no Python process or ArcPy runtime is used.
/// </summary>
public sealed class PythonToolBehaviorTests
{
    [Fact]
    public async Task PythonBridgePing_ForwardsOnceAndPreservesPayload()
    {
        var bridge = new FakePythonBridgeService
        {
            PingResult = OperationResult<string>.Ok("pong-from-service")
        };

        var result = await CallAsync(new PythonBridgePingTool(), bridge);

        Assert.True(result.Success);
        Assert.Equal("pong-from-service", result.Data);
        Assert.Equal(1, bridge.PingCallCount);
    }

    [Fact]
    public async Task PythonBridgePing_WithoutServiceReturnsExactUnavailableError()
    {
        var result = await CallAsync(new PythonBridgePingTool(), null);

        var error = Assert.Single(result.Errors);
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PythonBridgeUnavailable, error.Code);
        Assert.Equal(
            "Python Bridge service is not available in this host.",
            error.Message);
    }

    [Fact]
    public async Task PythonBridgePing_PreservesServiceFailureCodeAndMessage()
    {
        const string code = "PYTHON_EXECUTION_ERROR";
        const string message = "bridge action raised a structured error";
        var bridge = new FakePythonBridgeService
        {
            PingResult = OperationResult<string>.Fail(code, message)
        };

        var result = await CallAsync(new PythonBridgePingTool(), bridge);

        var error = Assert.Single(result.Errors);
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task PythonRuntimeInfo_ForwardsCallAndPreservesJson()
    {
        var payload = ParseJson("{\"python\":\"3.11.11\",\"arcpy\":\"3.5\"}");
        var bridge = new FakePythonBridgeService
        {
            RuntimeInfoResult = OperationResult<JsonElement?>.Ok(payload)
        };

        var result = await CallAsync(new PythonRuntimeInfoTool(), bridge);

        Assert.True(result.Success);
        Assert.Equal(payload.GetRawText(), Assert.IsType<JsonElement>(result.Data).GetRawText());
        Assert.Equal(1, bridge.RuntimeInfoCallCount);
    }

    [Fact]
    public async Task PythonRuntimeInfo_PreservesFailureAndUnavailableStates()
    {
        var failedBridge = new FakePythonBridgeService
        {
            RuntimeInfoResult = OperationResult<JsonElement?>.Fail(
                ErrorCodes.PythonProtocolError,
                "runtime payload was malformed")
        };

        var failed = await CallAsync(new PythonRuntimeInfoTool(), failedBridge);
        Assert.Equal(ErrorCodes.PythonProtocolError, Assert.Single(failed.Errors).Code);

        var unavailable = await CallAsync(new PythonRuntimeInfoTool(), null);
        Assert.Equal(ErrorCodes.PythonBridgeUnavailable, Assert.Single(unavailable.Errors).Code);
    }

    [Fact]
    public async Task DatasetSummary_ForwardsExactPathAndPreservesExistsFalseSuccess()
    {
        const string path = @"C:\phase572\missing.gdb\roads";
        var payload = ParseJson("{\"exists\":false,\"path\":\"C:\\\\phase572\\\\missing.gdb\\\\roads\"}");
        var bridge = new FakePythonBridgeService
        {
            DatasetSummaryResult = OperationResult<JsonElement?>.Ok(payload)
        };

        var result = await CallAsync(
            new DatasetSummaryTool(),
            bridge,
            new Dictionary<string, object?> { ["dataset_path"] = path });

        Assert.True(result.Success);
        Assert.Equal(payload.GetRawText(), Assert.IsType<JsonElement>(result.Data).GetRawText());
        Assert.Equal(1, bridge.DatasetSummaryCallCount);
        Assert.Equal(path, bridge.LastDatasetSummaryPath);
        Assert.False(Assert.IsType<JsonElement>(result.Data).GetProperty("exists").GetBoolean());
    }

    [Fact]
    public async Task DatasetSummary_EmptyAndWhitespaceUseCurrentServiceValidation()
    {
        var bridge = new FakePythonBridgeService
        {
            DatasetSummaryResult = OperationResult<JsonElement?>.Fail(
                ErrorCodes.InvalidArgument,
                "path is required")
        };
        var tool = new DatasetSummaryTool();

        var empty = await CallAsync(
            tool,
            bridge,
            new Dictionary<string, object?> { ["dataset_path"] = string.Empty });
        var whitespace = await CallAsync(
            tool,
            bridge,
            new Dictionary<string, object?> { ["dataset_path"] = "  " });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(empty.Errors).Code);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(whitespace.Errors).Code);
        Assert.Equal(new[] { string.Empty, "  " }, bridge.DatasetSummaryPaths);
    }

    [Fact]
    public async Task ListFields_ForwardsExactPathAndPreservesJson()
    {
        const string path = @"C:\phase572\roads.gdb\roads";
        var payload = ParseJson("{\"fields\":[{\"name\":\"OBJECTID\",\"isOid\":true}]}");
        var bridge = new FakePythonBridgeService
        {
            ListFieldsResult = OperationResult<JsonElement?>.Ok(payload)
        };

        var result = await CallAsync(
            new ListFieldsTool(),
            bridge,
            new Dictionary<string, object?> { ["dataset_path"] = path });

        Assert.True(result.Success);
        Assert.Equal(payload.GetRawText(), Assert.IsType<JsonElement>(result.Data).GetRawText());
        Assert.Equal(path, bridge.LastListFieldsPath);
        Assert.Equal(1, bridge.ListFieldsCallCount);
    }

    [Fact]
    public async Task ListFields_PreservesNotFoundAndUnavailableErrors()
    {
        const string path = @"C:\phase572\missing.gdb\roads";
        var bridge = new FakePythonBridgeService
        {
            ListFieldsResult = OperationResult<JsonElement?>.Fail(
                ErrorCodes.NotFound,
                "dataset was not found")
        };

        var notFound = await CallAsync(
            new ListFieldsTool(),
            bridge,
            new Dictionary<string, object?> { ["dataset_path"] = path });
        var unavailable = await CallAsync(
            new ListFieldsTool(),
            null,
            new Dictionary<string, object?> { ["dataset_path"] = path });

        Assert.Equal(ErrorCodes.NotFound, Assert.Single(notFound.Errors).Code);
        Assert.Equal(ErrorCodes.PythonBridgeUnavailable, Assert.Single(unavailable.Errors).Code);
    }

    [Fact]
    public async Task PythonDiscoveryTools_WithoutServiceReturnUnavailableError()
    {
        var cases = new (IMCPTool Tool, string Argument, string Value)[]
        {
            (new DatasetSummaryTool(), "dataset_path", "dataset"),
            (new ListFieldsTool(), "dataset_path", "dataset"),
            (new ListWorkspaceDatasetsTool(), "workspace_path", "workspace")
        };

        foreach (var (tool, argument, value) in cases)
        {
            var result = await CallAsync(
                tool,
                null,
                new Dictionary<string, object?> { [argument] = value });

            Assert.Equal(ErrorCodes.PythonBridgeUnavailable, Assert.Single(result.Errors).Code);
        }
    }

    [Fact]
    public async Task ListWorkspaceDatasets_ForwardsExactPathAndPreservesSuccessAndNotFound()
    {
        const string workspace = @"C:\phase572\workspace.gdb";
        var payload = ParseJson("{\"featureClasses\":[\"roads\"],\"totalCount\":1}");
        var bridge = new FakePythonBridgeService
        {
            ListWorkspaceDatasetsResult = OperationResult<JsonElement?>.Ok(payload)
        };

        var success = await CallAsync(
            new ListWorkspaceDatasetsTool(),
            bridge,
            new Dictionary<string, object?> { ["workspace_path"] = workspace });

        Assert.True(success.Success);
        Assert.Equal(payload.GetRawText(), Assert.IsType<JsonElement>(success.Data).GetRawText());
        Assert.Equal(workspace, bridge.LastListWorkspaceDatasetsPath);
        Assert.Equal(1, bridge.ListWorkspaceDatasetsCallCount);

        bridge.ListWorkspaceDatasetsResult = OperationResult<JsonElement?>.Fail(
            ErrorCodes.NotFound,
            "workspace was not found");
        var notFound = await CallAsync(
            new ListWorkspaceDatasetsTool(),
            bridge,
            new Dictionary<string, object?> { ["workspace_path"] = workspace });

        Assert.Equal(ErrorCodes.NotFound, Assert.Single(notFound.Errors).Code);
    }

    [Fact]
    public async Task PythonFacade_PropagatesCancellationTokenToService()
    {
        using var cts = new CancellationTokenSource();
        var bridge = new FakePythonBridgeService();

        var result = await CallAsync(new PythonBridgePingTool(), bridge, cancellationToken: cts.Token);

        Assert.True(result.Success);
        Assert.Equal(cts.Token, bridge.PingCancellationToken);
    }

    [Fact]
    public async Task PythonFacade_PreservesRepresentativeErrorCodeMatrix()
    {
        var codes = new[]
        {
            ErrorCodes.PythonTimeout,
            ErrorCodes.PythonBridgeUnavailable,
            "PYTHON_EXECUTION_ERROR",
            ErrorCodes.PythonOutputLimitExceeded,
            ErrorCodes.PythonProtocolError,
            ErrorCodes.Cancelled,
            ErrorCodes.NotFound,
            ErrorCodes.InvalidArgument
        };

        foreach (var code in codes)
        {
            var bridge = new FakePythonBridgeService
            {
                PingResult = OperationResult<string>.Fail(code, $"preserved message for {code}")
            };

            var result = await CallAsync(new PythonBridgePingTool(), bridge);

            var error = Assert.Single(result.Errors);
            Assert.Equal(code, error.Code);
            Assert.Equal($"preserved message for {code}", error.Message);
        }
    }

    private static async Task<OperationResult<object?>> CallAsync(
        IMCPTool tool,
        FakePythonBridgeService? bridge,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(
            registry,
            new FakeArcGISHost(),
            new MCPSettings(),
            NullLogger.Instance,
            bridge);

        return await router.ExecuteAsync(new MCPToolCall
        {
            Name = tool.Name,
            Arguments = arguments,
            CancellationToken = cancellationToken
        });
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
