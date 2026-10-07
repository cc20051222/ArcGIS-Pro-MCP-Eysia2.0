using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.IntegrationTests;

/// <summary>
/// Phase 4 工具集成测试：工具 → Router → Registry → Host → Service → Result（Fake Host）。
/// 覆盖 Map / Layer / Project / Attribute / Selection / GP 工具（Fake 数据；GP 走 Fake 占位返回 NOT_IMPLEMENTED）。
/// </summary>
public class Phase4ToolTests
{
    private static MCPToolRouter BuildRouter(out FakeArcGISHost host)
    {
        host = new FakeArcGISHost();
        var registry = new MCPToolRegistry();
        registry.Register(new PingTool());
        registry.Register(new ListMapsTool());
        registry.Register(new GetMapInfoTool());
        registry.Register(new GetLayersTool());
        registry.Register(new GetLayerInfoTool());
        registry.Register(new SetLayerVisibilityTool());
        registry.Register(new AddLayerTool());
        registry.Register(new RemoveLayerTool());
        registry.Register(new ListLayoutsTool());
        registry.Register(new ListDatabasesTool());
        registry.Register(new GetProjectInfoTool());
        registry.Register(new QueryAttributesTool());
        registry.Register(new GetFieldInfoTool());
        registry.Register(new GetFeatureCountTool());
        registry.Register(new ClearSelectionTool());
        registry.Register(new SelectLayerTool());
        registry.Register(new BufferTool());
        registry.Register(new ClipTool());
        registry.Register(new IntersectTool());
        registry.Register(new DissolveTool());
        return new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
    }

    private static async Task<OperationResult<object?>> CallAsync(MCPToolRouter router, string name, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        return await router.ExecuteAsync(new MCPToolCall { Name = name, Arguments = arguments ?? new Dictionary<string, object?>() });
    }

    [Fact]
    public async Task ListMaps_Returns_Two_Maps()
    {
        var router = BuildRouter(out _);
        var r = await CallAsync(router, "list_maps");
        Assert.True(r.Success);
        var maps = Assert.IsAssignableFrom<IReadOnlyList<MapInfo>>(r.Data);
        Assert.Equal(2, maps.Count);
    }

    [Fact]
    public async Task GetMapInfo_ByName_Returns_Map()
    {
        var router = BuildRouter(out _);
        var r = await CallAsync(router, "get_map_info", new Dictionary<string, object?> { ["mapName"] = "AnalysisMap" });
        Assert.True(r.Success);
        var info = Assert.IsType<MapInfo>(r.Data);
        Assert.Equal("AnalysisMap", info.Name);
    }

    [Fact]
    public async Task GetLayerInfo_Returns_Layer()
    {
        var router = BuildRouter(out _);
        var r = await CallAsync(router, "get_layer_info", new Dictionary<string, object?> { ["layerName"] = "Roads" });
        Assert.True(r.Success);
        var info = Assert.IsType<LayerInfo>(r.Data);
        Assert.Equal("Roads", info.Name);
    }

    [Fact]
    public async Task SetVisibility_Returns_Ok()
    {
        var router = BuildRouter(out _);
        var r = await CallAsync(router, "set_layer_visibility", new Dictionary<string, object?> { ["layerName"] = "Roads", ["visible"] = true });
        Assert.True(r.Success);
    }

    [Fact]
    public async Task AddLayer_Returns_Layer()
    {
        var router = BuildRouter(out _);
        var r = await CallAsync(router, "add_layer", new Dictionary<string, object?> { ["layerPathOrUri"] = "C:/data/fc" });
        Assert.True(r.Success);
        var info = Assert.IsType<LayerInfo>(r.Data);
        Assert.Equal("AddedLayer", info.Name);
    }

    [Fact]
    public async Task RemoveLayer_Returns_Ok()
    {
        var router = BuildRouter(out _);
        var r = await CallAsync(router, "remove_layer", new Dictionary<string, object?> { ["layerName"] = "Roads" });
        Assert.True(r.Success);
    }

    [Fact]
    public async Task ListLayouts_And_Databases()
    {
        var router = BuildRouter(out _);
        var layouts = await CallAsync(router, "list_layouts");
        var dbs = await CallAsync(router, "list_databases");
        Assert.True(layouts.Success);
        Assert.True(dbs.Success);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<LayoutInfo>>(layouts.Data));
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<DatasetInfo>>(dbs.Data));
    }

    [Fact]
    public async Task QueryAttributes_Returns_Feature()
    {
        var router = BuildRouter(out _);
        var r = await CallAsync(router, "query_attributes", new Dictionary<string, object?> { ["layerName"] = "Roads" });
        Assert.True(r.Success);
        var features = Assert.IsAssignableFrom<IReadOnlyList<FeatureInfo>>(r.Data);
        Assert.Single(features);
        Assert.Equal("A", features[0].Attributes["NAME"]);
    }

    [Fact]
    public async Task GetFieldInfo_And_FeatureCount()
    {
        var router = BuildRouter(out _);
        var fields = await CallAsync(router, "get_field_info", new Dictionary<string, object?> { ["layerName"] = "Roads" });
        var count = await CallAsync(router, "get_feature_count", new Dictionary<string, object?> { ["layerName"] = "Roads" });
        Assert.True(fields.Success);
        Assert.True(count.Success);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<FieldInfo>>(fields.Data));
        Assert.Equal(5L, count.Data);
    }

    [Fact]
    public async Task Selection_Tools_Route()
    {
        var router = BuildRouter(out _);
        var clear = await CallAsync(router, "clear_selection");
        var select = await CallAsync(router, "select_layer", new Dictionary<string, object?> { ["layerName"] = "Roads" });
        Assert.True(clear.Success);
        Assert.True(select.Success);
    }

    [Fact]
    public async Task Gp_Tools_Route_Via_Fake_Placeholder()
    {
        // GP 工具经 Fake 占位服务返回 NOT_IMPLEMENTED（无 ArcGIS Pro）；验证工具→Router→Host 调用链不抛异常、返回 OperationResult。
        var router = BuildRouter(out _);
        var buffer = await CallAsync(router, "buffer", new Dictionary<string, object?> { ["input"] = "C:/a", ["output"] = "C:/ab", ["distance"] = 100 });
        Assert.False(buffer.Success);
        Assert.Contains(ErrorCodes.NotImplemented, buffer.Errors.Select(e => e.Code));
    }

    [Fact]
    public void Tool_Metadata_Is_Categorized()
    {
        Assert.Equal(ToolCategories.Analysis, new BufferTool().Metadata.Category);
        Assert.Equal(ExecutionTypes.Geoprocessing, new BufferTool().Metadata.ExecutionType);
        Assert.Equal("list_maps", new ListMapsTool().Metadata.Name);
        Assert.True(new ListMapsTool().Metadata.RequiresArcGIS);
    }
}
