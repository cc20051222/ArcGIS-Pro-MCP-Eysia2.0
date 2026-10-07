using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.IntegrationTests;

/// <summary>
/// Host 调用链集成测试：工具 → Router → Registry → Tool → Host → Service → Result。
/// 使用 FakeArcGISHost（无 ArcGIS Pro）。真实 ArcGIS 集成由 Add-in 内 SelfTestRunner 在 Pro 中执行。
/// </summary>
public class HostChainTests
{
    private static (MCPToolRouter router, FakeArcGISHost host) Build()
    {
        var host = new FakeArcGISHost();
        var registry = new MCPToolRegistry();
        registry.Register(new PingTool());
        registry.Register(new GetCurrentMapTool());
        registry.Register(new GetLayersTool());
        registry.Register(new GetProjectInfoTool());
        registry.Register(new GetArcGISVersionTool());
        registry.Register(new GetLicenseInfoTool());
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return (router, host);
    }

    private static async Task<object?> RunAsync(MCPToolRouter router, string name)
    {
        var result = await router.ExecuteAsync(new MCPToolCall { Name = name });
        Assert.True(result.Success, "Expected success: " + string.Join(";", result.Errors.Select(e => e.ToString())));
        return result.Data;
    }

    [Fact]
    public async Task GetCurrentMap_Returns_MapInfo()
    {
        var (router, _) = Build();
        var data = await RunAsync(router, "get_current_map");
        var info = Assert.IsType<MapInfo>(data);
        Assert.Equal("TestMap", info.Name);
    }

    [Fact]
    public async Task GetMaps_Host_Returns_Two_Maps()
    {
        var (router, host) = Build();
        var result = await host.Maps.GetMapsAsync();
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task GetLayers_Returns_FeatureLayers()
    {
        var (router, _) = Build();
        var data = await RunAsync(router, "get_layers");
        var layers = Assert.IsAssignableFrom<IReadOnlyList<LayerInfo>>(data);
        Assert.Equal(2, layers.Count);
        Assert.Equal("FeatureLayer", layers[0].LayerType);
    }

    [Fact]
    public async Task GetProjectInfo_Returns_ProjectInfo()
    {
        var (router, _) = Build();
        var data = await RunAsync(router, "get_project_info");
        var info = Assert.IsType<ProjectInfo>(data);
        Assert.Equal("TestProject", info.Name);
    }

    [Fact]
    public async Task GetVersion_Returns_ArcGISVersionInfo()
    {
        var (router, _) = Build();
        var data = await RunAsync(router, "get_arcgis_version");
        var info = Assert.IsType<ArcGISVersionInfo>(data);
        Assert.Equal("3.5.0", info.ProductVersion);
        Assert.Equal(3, info.Major);
        Assert.Equal(5, info.Minor);
    }

    [Fact]
    public async Task GetLicense_Returns_LicensingInfo()
    {
        var (router, _) = Build();
        var data = await RunAsync(router, "get_license_info");
        var info = Assert.IsType<LicensingInfo>(data);
        Assert.Equal("Advanced", info.LicenseLevel);
    }

    [Fact]
    public async Task Ping_Through_Full_Chain()
    {
        var (router, _) = Build();
        var data = await RunAsync(router, "ping");
        Assert.Equal("pong", data);
    }

    [Fact]
    public void Registry_Registers_All_Six_Tools()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new PingTool());
        registry.Register(new GetCurrentMapTool());
        registry.Register(new GetLayersTool());
        registry.Register(new GetProjectInfoTool());
        registry.Register(new GetArcGISVersionTool());
        registry.Register(new GetLicenseInfoTool());

        Assert.Equal(6, registry.Count);
        Assert.Contains("ping", registry.List().Select(t => t.Name));
        Assert.Contains("get_license_info", registry.List().Select(t => t.Name));
    }
}
