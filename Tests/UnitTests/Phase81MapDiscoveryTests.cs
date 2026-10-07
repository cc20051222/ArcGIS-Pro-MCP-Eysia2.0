using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 8.1 Map Discovery Completion 的隔离单元测试（不启动 ArcGIS Pro）。
/// 覆盖缺陷修复：C-05（get_map_info 取首项 / 重名静默取第一）、
/// C-06（无活动视图不得回退）、C-10（get_layer_info schema 与实现契约）、
/// G-06（get_layers 默认展开组合图层）。
/// </summary>
public sealed class Phase81MapDiscoveryTests
{
    private static ToolExecutionContext Context(FakeArcGISHost host, IReadOnlyDictionary<string, object?>? args = null)
        => new()
        {
            Host = host,
            Arguments = args,
            CancellationToken = CancellationToken.None
        };

    private static FakeArcGISHost HostWith(ScriptedMapService maps, ScriptedLayerService? layers = null)
        => new(maps: maps, layers: layers ?? new ScriptedLayerService());

    // ---------- C-06 / C-05a：get_map_info 省略 mapName ----------

    [Fact]
    public async Task GetMapInfo_WhenNameOmitted_ReturnsActiveMap_NotFirstListItem()
    {
        var maps = new ScriptedMapService
        {
            HasActiveView = true,
            ActiveMap = new MapInfo { Name = "ActiveMap", Kind = "Map", Id = "path/ActiveMap", IsActive = true }
        };
        // 列表首项与活动地图不同：若实现回退取首项，本用例必须失败。
        maps.Maps.Add(new MapInfo { Name = "FirstInList", Kind = "Map", Id = "path/FirstInList" });
        maps.Maps.Add(new MapInfo { Name = "ActiveMap", Kind = "Map", Id = "path/ActiveMap" });

        var result = await new GetMapInfoTool().ExecuteAsync(Context(HostWith(maps)));

        Assert.True(result.Success);
        var info = Assert.IsType<MapInfo>(result.Data);
        Assert.Equal("ActiveMap", info.Name);
        Assert.True(info.IsActive);
    }

    [Fact]
    public async Task GetMapInfo_WhenNoActiveView_ReturnsNoActiveView()
    {
        var maps = new ScriptedMapService { HasActiveView = false };
        maps.Maps.Add(new MapInfo { Name = "SomeMap", Kind = "Map" });

        var result = await new GetMapInfoTool().ExecuteAsync(Context(HostWith(maps)));

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.NoActiveView, result.Errors[0].Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetMapInfo_WhenNameIsAmbiguous_ReturnsAmbiguousWithCandidateIds()
    {
        var maps = new ScriptedMapService();
        maps.Maps.Add(new MapInfo { Name = "Dup", Kind = "Map", Id = "path/Dup#1" });
        maps.Maps.Add(new MapInfo { Name = "Dup", Kind = "Map", Id = "path/Dup#2" });

        var result = await new GetMapInfoTool().ExecuteAsync(
            Context(HostWith(maps), new Dictionary<string, object?> { ["mapName"] = "Dup" }));

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.AmbiguousMapName, result.Errors[0].Code);
        Assert.Contains("path/Dup#1", result.Errors[0].Message);
        Assert.Contains("path/Dup#2", result.Errors[0].Message);
    }

    [Fact]
    public async Task GetMapInfo_WhenNameUnique_ReturnsThatMap()
    {
        var maps = new ScriptedMapService();
        maps.Maps.Add(new MapInfo { Name = "Alpha", Kind = "Map", Id = "path/Alpha" });
        maps.Maps.Add(new MapInfo { Name = "Beta", Kind = "Scene", Id = "path/Beta" });

        var result = await new GetMapInfoTool().ExecuteAsync(
            Context(HostWith(maps), new Dictionary<string, object?> { ["mapName"] = "beta" }));

        Assert.True(result.Success);
        Assert.Equal("Beta", Assert.IsType<MapInfo>(result.Data).Name);
        Assert.Equal("Scene", Assert.IsType<MapInfo>(result.Data).Kind);
    }

    [Fact]
    public async Task GetMapInfo_WhenNameMissing_ReturnsMapNotFound()
    {
        var maps = new ScriptedMapService();
        maps.Maps.Add(new MapInfo { Name = "Alpha", Kind = "Map" });

        var result = await new GetMapInfoTool().ExecuteAsync(
            Context(HostWith(maps), new Dictionary<string, object?> { ["mapName"] = "Nope" }));

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.MapNotFound, result.Errors[0].Code);
    }

    // ---------- G-06：get_layers 默认展开 ----------

    [Fact]
    public async Task GetLayers_DefaultsToFlattened()
    {
        var layers = new ScriptedLayerService();
        layers.TopLevel.Add(new LayerInfo { Name = "GroupA" });
        layers.Flattened.Add(new LayerInfo { Name = "GroupA" });
        layers.Flattened.Add(new LayerInfo { Name = "ChildA" });
        layers.Flattened.Add(new LayerInfo { Name = "ChildB" });

        var result = await new GetLayersTool().ExecuteAsync(Context(HostWith(new ScriptedMapService(), layers)));

        Assert.True(result.Success);
        Assert.Single(layers.FlattenCalls);
        Assert.True(layers.FlattenCalls[0], "flatten 默认必须为 true");
        Assert.Equal(3, Assert.IsAssignableFrom<IReadOnlyList<LayerInfo>>(result.Data).Count);
    }

    [Fact]
    public async Task GetLayers_WhenFlattenFalse_ReturnsTopLevelOnly()
    {
        var layers = new ScriptedLayerService();
        layers.TopLevel.Add(new LayerInfo { Name = "GroupA" });
        layers.Flattened.Add(new LayerInfo { Name = "GroupA" });
        layers.Flattened.Add(new LayerInfo { Name = "ChildA" });

        var result = await new GetLayersTool().ExecuteAsync(
            Context(HostWith(new ScriptedMapService(), layers),
                new Dictionary<string, object?> { ["flatten"] = false }));

        Assert.True(result.Success);
        Assert.False(layers.FlattenCalls[0]);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<LayerInfo>>(result.Data));
    }

    [Fact]
    public void GetLayers_Schema_DeclaresBooleanFlattenDefaultingToTrue()
    {
        var schema = new GetLayersTool().InputSchema;
        var json = JsonSerializer.Serialize(schema);
        using var document = JsonDocument.Parse(json);

        var flatten = document.RootElement.GetProperty("properties").GetProperty("flatten");
        Assert.Equal("boolean", flatten.GetProperty("type").GetString());
        Assert.True(flatten.GetProperty("default").GetBoolean());
    }

    // ---------- C-10：get_layer_info schema 与实现契约 ----------

    [Fact]
    public async Task GetLayerInfo_WhenLayerNameMissing_ReturnsInvalidArgument()
    {
        var layers = new ScriptedLayerService();
        layers.Flattened.Add(new LayerInfo { Name = "Roads" });

        var result = await new GetLayerInfoTool().ExecuteAsync(Context(HostWith(new ScriptedMapService(), layers)));

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Errors[0].Code);
    }

    [Fact]
    public void GetLayerInfo_Schema_DeclaresLayerNameAsRequired()
    {
        var json = JsonSerializer.Serialize(new GetLayerInfoTool().InputSchema);
        using var document = JsonDocument.Parse(json);

        var required = document.RootElement.GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToList();

        Assert.Contains("layerName", required);
        Assert.True(document.RootElement.GetProperty("properties").TryGetProperty("layerName", out _));
    }

    [Fact]
    public async Task GetLayerInfo_WhenNameAmbiguous_ReturnsAmbiguousLayerName()
    {
        var layers = new ScriptedLayerService();
        layers.Flattened.Add(new LayerInfo { Name = "Roads", Uri = "uri#1" });
        layers.Flattened.Add(new LayerInfo { Name = "Roads", Uri = "uri#2" });

        var result = await new GetLayerInfoTool().ExecuteAsync(
            Context(HostWith(new ScriptedMapService(), layers),
                new Dictionary<string, object?> { ["layerName"] = "Roads" }));

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.AmbiguousLayerName, result.Errors[0].Code);
    }

    [Fact]
    public async Task GetLayerInfo_WhenNameUnique_ReturnsLayer()
    {
        var layers = new ScriptedLayerService();
        layers.Flattened.Add(new LayerInfo { Name = "Roads", LayerType = "FeatureLayer" });

        var result = await new GetLayerInfoTool().ExecuteAsync(
            Context(HostWith(new ScriptedMapService(), layers),
                new Dictionary<string, object?> { ["layerName"] = "Roads" }));

        Assert.True(result.Success);
        Assert.Equal("Roads", Assert.IsType<LayerInfo>(result.Data).Name);
    }

    // ---------- 错误码新增不得影响既有语义 ----------

    [Fact]
    public void NewErrorCodes_AreDeclaredAndDistinct()
    {
        Assert.Equal("NO_ACTIVE_VIEW", ErrorCodes.NoActiveView);
        Assert.Equal("AMBIGUOUS_MAP_NAME", ErrorCodes.AmbiguousMapName);
        Assert.Equal("AMBIGUOUS_LAYER_NAME", ErrorCodes.AmbiguousLayerName);

        var all = new[] { ErrorCodes.NoActiveView, ErrorCodes.AmbiguousMapName, ErrorCodes.AmbiguousLayerName };
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
    }
}
