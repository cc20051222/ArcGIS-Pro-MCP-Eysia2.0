using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 8.2 (D-011)：get_dataset_info / get_raster_info 已按 PHASE_8_2_CHANNEL ADR
/// 由 Native 文件系统占位实现改为 Bridge facade。本测试类验证 Tool 层的
/// 转发/参数校验/不可用错误行为；真实 ArcPy 数据语义由 D-011 复验（协议层实机）覆盖。
/// </summary>
public sealed class PlaceholderToolBehaviorTests
{
    [Fact]
    public async Task GetDatasetInfo_ForwardsToBridgeAndPreservesPayload()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            path = "D:/x/roads.gdb/FC1",
            exists = true,
            dataType = "FeatureClass",
            reason = (string?)null
        });
        var bridge = new FakePythonBridgeService { DatasetInfoResult = OperationResult<JsonElement?>.Ok(payload) };

        var result = await CallAsync(new GetDatasetInfoTool(), bridge,
            new Dictionary<string, object?> { ["path"] = "D:/x/roads.gdb/FC1" });

        Assert.True(result.Success);
        Assert.Equal(JsonValueKind.Object, Assert.IsType<JsonElement>(result.Data).ValueKind);
    }

    [Fact]
    public async Task GetDatasetInfo_MissingPathReturnsExistsFalseWithReason()
    {
        // GATE-D010 §4：exists=false 成功返回必须带 reason（不得用 null/错误掩盖）。
        var payload = JsonSerializer.SerializeToElement(new { exists = false, reason = "NOT_FOUND" });
        var bridge = new FakePythonBridgeService { DatasetInfoResult = OperationResult<JsonElement?>.Ok(payload) };

        var result = await CallAsync(new GetDatasetInfoTool(), bridge,
            new Dictionary<string, object?> { ["path"] = "D:/nowhere.gdb/X" });

        Assert.True(result.Success);
        var element = Assert.IsType<JsonElement>(result.Data);
        Assert.False(element.GetProperty("exists").GetBoolean());
        Assert.Equal("NOT_FOUND", element.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task GetDatasetInfo_InvalidArgumentIsRejectedByTool()
    {
        var bridge = new FakePythonBridgeService();

        var empty = await CallAsync(new GetDatasetInfoTool(), bridge,
            new Dictionary<string, object?> { ["path"] = " " });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(empty.Errors).Code);
    }

    [Fact]
    public async Task GetDatasetInfo_WithoutServiceReturnsUnavailable()
    {
        var result = await CallAsync(new GetDatasetInfoTool(), null,
            new Dictionary<string, object?> { ["path"] = "D:/x" });

        Assert.Equal(ErrorCodes.PythonBridgeUnavailable, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task GetRasterInfo_ForwardsToBridgeAndPreservesPayload()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            dataset_path = "D:/x/r.tif",
            exists = true,
            width = 4000,
            height = 2000,
            bandCount = 1,
            statistics = "unknown"
        });
        var bridge = new FakePythonBridgeService { RasterInfoResult = OperationResult<JsonElement?>.Ok(payload) };

        var result = await CallAsync(new GetRasterInfoTool(), bridge,
            new Dictionary<string, object?> { ["datasetPath"] = "D:/x/r.tif" });

        Assert.True(result.Success);
        Assert.Equal(JsonValueKind.Object, Assert.IsType<JsonElement>(result.Data).ValueKind);
    }

    [Fact]
    public async Task GetRasterInfo_UnknownStatisticsSentinelIsPreserved()
    {
        // 哨兵 "unknown" 必须原样透传（禁止实现层现算后替换为数值）。
        var payload = JsonSerializer.SerializeToElement(new { exists = true, statistics = "unknown" });
        var bridge = new FakePythonBridgeService { RasterInfoResult = OperationResult<JsonElement?>.Ok(payload) };

        var result = await CallAsync(new GetRasterInfoTool(), bridge,
            new Dictionary<string, object?> { ["datasetPath"] = "D:/x/nostats.tif" });

        Assert.True(result.Success);
        var element = Assert.IsType<JsonElement>(result.Data);
        Assert.Equal("unknown", element.GetProperty("statistics").GetString());
    }

    [Fact]
    public async Task GetRasterInfo_InvalidArgumentIsRejectedByTool()
    {
        var bridge = new FakePythonBridgeService();

        var empty = await CallAsync(new GetRasterInfoTool(), bridge,
            new Dictionary<string, object?> { ["datasetPath"] = string.Empty });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(empty.Errors).Code);
    }

    [Fact]
    public async Task GetRasterInfo_WithoutServiceReturnsUnavailable()
    {
        var result = await CallAsync(new GetRasterInfoTool(), null,
            new Dictionary<string, object?> { ["datasetPath"] = "D:/x/r.tif" });

        Assert.Equal(ErrorCodes.PythonBridgeUnavailable, Assert.Single(result.Errors).Code);
    }

    private static async Task<OperationResult<object?>> CallAsync(
        IMCPTool tool,
        FakePythonBridgeService? bridge,
        IReadOnlyDictionary<string, object?> arguments)
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
            Arguments = arguments
        });
    }
}
