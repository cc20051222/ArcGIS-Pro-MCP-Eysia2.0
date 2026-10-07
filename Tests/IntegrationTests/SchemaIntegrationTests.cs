using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.IntegrationTests;

/// <summary>
/// Phase 9 第一批（D-034）：schema 只读信息族集成测试（工具 → Router → Registry → Host → SchemaService → Result，Fake 数据）。
/// 覆盖 4 工具的正向全链 + 参数校验；真实 SDK 语义由阶段二（安装 + 真实 Pro）覆盖。
/// </summary>
public class SchemaIntegrationTests
{
    private static MCPToolRouter BuildRouter(out FakeArcGISHost host)
    {
        host = new FakeArcGISHost();
        var registry = new MCPToolRegistry();
        registry.Register(new GetSchemaInfoTool());
        registry.Register(new GetDomainsTool());
        registry.Register(new GetSubtypesTool());
        registry.Register(new GetIndexesTool());
        return new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
    }

    private static async Task<OperationResult<object?>> CallAsync(MCPToolRouter router, string name, IReadOnlyDictionary<string, object?>? arguments = null)
        => await router.ExecuteAsync(new MCPToolCall { Name = name, Arguments = arguments ?? new Dictionary<string, object?>() });

    [Fact]
    public async Task GetSchemaInfo_Chain_ReturnsSchema()
    {
        var router = BuildRouter(out _);
        var r = await CallAsync(router, "get_schema_info", new Dictionary<string, object?> { ["path"] = "C:/data.gdb/FC1" });
        Assert.True(r.Success);
        var info = Assert.IsType<SchemaInfo>(r.Data);
        Assert.Equal("FeatureClass", info.DataType);
        Assert.Equal(2, info.Fields.Count);
    }

    [Fact]
    public async Task GetDomains_Chain_ReturnsDomains()
    {
        var router = BuildRouter(out _);
        var r = await CallAsync(router, "get_domains", new Dictionary<string, object?> { ["workspace"] = "C:/data.gdb" });
        Assert.True(r.Success);
        var domains = Assert.IsAssignableFrom<IReadOnlyList<DomainInfo>>(r.Data);
        Assert.Single(domains);
    }

    [Fact]
    public async Task GetSubtypes_Chain_ReturnsSubtypes()
    {
        var router = BuildRouter(out _);
        var r = await CallAsync(router, "get_subtypes", new Dictionary<string, object?> { ["path"] = "C:/data.gdb/FC1" });
        Assert.True(r.Success);
        var info = Assert.IsType<SubtypeInfo>(r.Data);
        Assert.Equal("CATEGORY", info.SubtypeField);
        Assert.Equal(2, info.Subtypes.Count);
    }

    [Fact]
    public async Task GetIndexes_Chain_ReturnsIndexes()
    {
        var router = BuildRouter(out _);
        var r = await CallAsync(router, "get_indexes", new Dictionary<string, object?> { ["path"] = "C:/data.gdb/FC1", ["maxItems"] = 10 });
        Assert.True(r.Success);
        var indexes = Assert.IsAssignableFrom<IReadOnlyList<IndexInfo>>(r.Data);
        Assert.Single(indexes);
    }

    [Fact]
    public async Task AllFourTools_BlankPath_ReturnInvalidArgument()
    {
        var router = BuildRouter(out _);
        var r1 = await CallAsync(router, "get_schema_info", new Dictionary<string, object?> { ["path"] = " " });
        var r2 = await CallAsync(router, "get_domains", new Dictionary<string, object?> { ["workspace"] = " " });
        var r3 = await CallAsync(router, "get_subtypes", new Dictionary<string, object?> { ["path"] = " " });
        var r4 = await CallAsync(router, "get_indexes", new Dictionary<string, object?> { ["path"] = " " });
        Assert.All(new[] { r1, r2, r3, r4 }, r => Assert.False(r.Success));
        Assert.All(new[] { r1, r2, r3, r4 }, r => Assert.Contains(r.Errors, e => e.Code == ErrorCodes.InvalidArgument));
    }
}
