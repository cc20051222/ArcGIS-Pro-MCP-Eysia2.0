using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.IntegrationTests;

/// <summary>
/// Phase 9 第二批（D-035）：copy_dataset / export_table / project 集成测试
/// （工具 → Router → Registry → Host → GP 服务边界，Recording 数据；真实 GP 执行归阶段二）。
/// </summary>
public class CopyExportProjectIntegrationTests
{
    private static MCPToolRouter BuildRouter(out RecordingGeoprocessingService service)
    {
        service = new RecordingGeoprocessingService
        {
            Result = OperationResult<GeoprocessingResult>.Ok(new GeoprocessingResult { Result = "ok", ToolName = "test" })
        };
        var registry = new MCPToolRegistry();
        registry.Register(new CopyDatasetTool());
        registry.Register(new ExportTableTool());
        registry.Register(new ProjectTool());
        return new MCPToolRouter(registry, new FakeArcGISHost(service), new MCPSettings(), NullLogger.Instance);
    }

    private static async Task<OperationResult<object?>> CallAsync(MCPToolRouter router, string name, IReadOnlyDictionary<string, object?>? arguments = null)
        => await router.ExecuteAsync(new MCPToolCall { Name = name, Arguments = arguments ?? new Dictionary<string, object?>() });

    [Fact]
    public async Task CopyDataset_Chain_RoutesToGp()
    {
        var router = BuildRouter(out var service);
        var r = await CallAsync(router, "copy_dataset", new Dictionary<string, object?> { ["inputPath"] = "a", ["outputPath"] = "b" });
        Assert.True(r.Success);
        Assert.Equal("Copy_management", Assert.Single(service.Requests).ToolName);
    }

    [Fact]
    public async Task ExportTable_Chain_RoutesToGp()
    {
        var router = BuildRouter(out var service);
        var r = await CallAsync(router, "export_table", new Dictionary<string, object?> { ["inputPath"] = "a", ["outputPath"] = "b.csv" });
        Assert.True(r.Success);
        Assert.Equal("ExportTable_conversion", Assert.Single(service.Requests).ToolName);
    }

    [Fact]
    public async Task Project_Chain_RoutesToGp()
    {
        var router = BuildRouter(out var service);
        var r = await CallAsync(router, "project", new Dictionary<string, object?> { ["inputPath"] = "a", ["outputPath"] = "b", ["outSR"] = "4326" });
        Assert.True(r.Success);
        Assert.Equal("Project_management", Assert.Single(service.Requests).ToolName);
    }

    [Fact]
    public async Task AllThree_BlankRequired_ReturnInvalidArgument()
    {
        var router = BuildRouter(out var service);
        var r1 = await CallAsync(router, "copy_dataset", new Dictionary<string, object?> { ["inputPath"] = " ", ["outputPath"] = "b" });
        var r2 = await CallAsync(router, "export_table", new Dictionary<string, object?> { ["inputPath"] = "", ["outputPath"] = "b" });
        var r3 = await CallAsync(router, "project", new Dictionary<string, object?> { ["inputPath"] = "a", ["outputPath"] = "b", ["outSR"] = " " });
        Assert.All(new[] { r1, r2, r3 }, r => Assert.False(r.Success));
        Assert.All(new[] { r1, r2, r3 }, r => Assert.Contains(r.Errors, e => e.Code == ErrorCodes.InvalidArgument));
        Assert.Empty(service.Requests);
    }
}
