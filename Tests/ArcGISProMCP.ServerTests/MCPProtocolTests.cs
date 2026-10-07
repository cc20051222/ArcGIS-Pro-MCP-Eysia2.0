using System.Text.Json;
using ArcGISProMCP.Server.JsonRpc;

namespace ArcGISProMCP.ServerTests;

/// <summary>JSON-RPC 2.0 编解码测试。</summary>
public class MCPProtocolTests
{
    [Fact]
    public void ValidRequest_Parses()
    {
        const string json = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""";

        var ok = JsonRpcCodec.TryParseRequest(json, out var request, out var error);

        Assert.True(ok, "Expected successful parse.");
        Assert.Null(error);
        Assert.NotNull(request);
        Assert.Equal("initialize", request!.Method);
        Assert.True(request.Id.HasValue);
        Assert.Equal(JsonValueKind.Number, request.Id!.Value.ValueKind);
    }

    [Fact]
    public void StringRequestId_Parses_AsNonNotification()
    {
        const string json = """{"jsonrpc":"2.0","id":"request-1","method":"tools/list"}""";

        var ok = JsonRpcCodec.TryParseRequest(json, out var request, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(request);
        Assert.True(request!.Id.HasValue);
        Assert.Equal(JsonValueKind.String, request.Id!.Value.ValueKind);
        Assert.Equal("request-1", request.Id.Value.GetString());
        Assert.False(request.IsNotification);
    }

    [Fact]
    public void NullRequestId_Parses_AsNotification()
    {
        const string json = """{"jsonrpc":"2.0","id":null,"method":"notifications/initialized"}""";

        var ok = JsonRpcCodec.TryParseRequest(json, out var request, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(request);
        Assert.True(request!.Id.HasValue);
        Assert.Equal(JsonValueKind.Null, request.Id!.Value.ValueKind);
        Assert.True(request.IsNotification);
    }

    [Fact]
    public void MissingRequestId_Parses_AsNotification()
    {
        const string json = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";

        var ok = JsonRpcCodec.TryParseRequest(json, out var request, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(request);
        Assert.False(request!.Id.HasValue);
        Assert.True(request.IsNotification);
    }

    [Fact]
    public void InvalidJSON_Returns_ParseError()
    {
        const string json = "{ not valid json";

        var ok = JsonRpcCodec.TryParseRequest(json, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(JsonRpcErrorCodes.ParseError, error!.Code);
    }

    [Fact]
    public void InvalidRequest_Returns_InvalidRequest()
    {
        const string json = """{"jsonrpc":"1.0","method":"x","id":1}""";

        var ok = JsonRpcCodec.TryParseRequest(json, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(JsonRpcErrorCodes.InvalidRequest, error!.Code);
    }

    [Fact]
    public void MissingMethod_Returns_InvalidRequest()
    {
        const string json = """{"jsonrpc":"2.0","id":1}""";

        var ok = JsonRpcCodec.TryParseRequest(json, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(JsonRpcErrorCodes.InvalidRequest, error!.Code);
    }

    [Fact]
    public void BatchRequest_Returns_InvalidRequest_Under_Current_Contract()
    {
        const string json = """[{"jsonrpc":"2.0","id":1,"method":"tools/list"}]""";

        var ok = JsonRpcCodec.TryParseRequest(json, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(JsonRpcErrorCodes.InvalidRequest, error!.Code);
    }

    [Fact]
    public void BuildError_Has_JsonRpc_Id_Null_And_Error()
    {
        var json = JsonRpcCodec.BuildError(null, JsonRpcErrorCodes.ParseError, "Parse error");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("id").ValueKind);
        Assert.Equal(JsonRpcErrorCodes.ParseError, root.GetProperty("error").GetProperty("code").GetInt32());
    }
}
