using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-042（Phase 10 第一批）：布局与范围只读 4 工具的 Fake 层单测。
/// 覆盖：路由与 Native 类别、layoutName/mapName 缺失校验、布局未找到语义、
/// 空元素列表（非错误）、GroupLayer → supports=false + null（G-82-C 可判别）、空串（支持但未设置）。
/// </summary>
public class LayoutReadToolTests
{
    private sealed class FakeLayoutService : ILayoutService
    {
        public LayoutDetailInfo? Detail { get; set; }
        public List<LayoutElementInfo> Elements { get; set; } = new();
        public string? FailCode { get; set; }
        public string? FailMessage { get; set; }
        public string? LastLayoutName { get; private set; }

        public Task<OperationResult<IReadOnlyList<LayoutInfo>>> GetLayoutsAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<LayoutInfo>>.Ok(new List<LayoutInfo>()));

        public Task<OperationResult<LayoutDetailInfo>> GetLayoutInfoAsync(string layoutName, CancellationToken ct = default)
        {
            LastLayoutName = layoutName;
            if (FailCode is not null)
            {
                return Task.FromResult(OperationResult<LayoutDetailInfo>.Fail(FailCode, FailMessage ?? "fail"));
            }
            if (Detail is null)
            {
                return Task.FromResult(OperationResult<LayoutDetailInfo>.Fail(
                    ErrorCodes.LayerNotFound, $"Layout '{layoutName}' not found."));
            }
            return Task.FromResult(OperationResult<LayoutDetailInfo>.Ok(Detail));
        }

        public Task<OperationResult<IReadOnlyList<LayoutElementInfo>>> ListLayoutElementsAsync(string layoutName, CancellationToken ct = default)
        {
            LastLayoutName = layoutName;
            if (FailCode is not null)
            {
                return Task.FromResult(OperationResult<IReadOnlyList<LayoutElementInfo>>.Fail(FailCode, FailMessage ?? "fail"));
            }
            return Task.FromResult(OperationResult<IReadOnlyList<LayoutElementInfo>>.Ok(Elements));
        }

        public Task<OperationResult<LayoutExportInfo>> ExportLayoutAsync(
            string layoutName, string outputPath, string format, double? resolution, bool overwrite,
            CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutExportInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<LayoutCreateInfo>> CreateLayoutAsync(
            string name, double pageWidth, double pageHeight, string pageUnits, string? mapName,
            string? mapFrameName, double? frameXMin, double? frameYMin, double? frameXMax, double? frameYMax,
            CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutCreateInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<LayoutElementAddInfo>> AddLayoutTextAsync(
            string layoutName, string text, double x, double y, double? fontSize, string? fontFamily,
            string? elementName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<LayoutElementAddInfo>> AddLegendAsync(
            string layoutName, string mapFrameName, double x, double y, double? width, double? height,
            string? elementName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<LayoutElementAddInfo>> AddNorthArrowAsync(
            string layoutName, string mapFrameName, double x, double y, double? width, double? height,
            string? elementName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<LayoutElementAddInfo>> AddScaleBarAsync(
            string layoutName, string mapFrameName, double x, double y, double? width, double? height,
            string? elementName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));
    }

    private sealed class FakeMapServiceWithExtent : IMapService
    {
        public MapExtentInfo? Extent { get; set; }
        public string? FailCode { get; set; }

        public Task<OperationResult<MapInfo?>> GetCurrentMapAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapInfo?>.Ok(new MapInfo()));

        public Task<OperationResult<IReadOnlyList<MapInfo>>> GetMapsAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<MapInfo>>.Ok(new List<MapInfo>()));

        public Task<OperationResult<MapExtentInfo>> GetMapExtentAsync(string? mapName, CancellationToken ct = default)
        {
            if (FailCode is not null)
            {
                return Task.FromResult(OperationResult<MapExtentInfo>.Fail(FailCode, "fail"));
            }
            if (Extent is null)
            {
                return Task.FromResult(OperationResult<MapExtentInfo>.Ok(new MapExtentInfo
                {
                    MapName = mapName ?? string.Empty,
                    ExtentSource = "default-extent",
                    // 数值未定 → 显式 null（披露口径）
                }));
            }
            return Task.FromResult(OperationResult<MapExtentInfo>.Ok(Extent));
        }

        public Task<OperationResult<MapExtentSetInfo>> SetMapExtentAsync(
            string? mapName, double xMin, double yMin, double xMax, double yMax, string? spatialReference,
            CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapExtentSetInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));
    }

    private sealed class FakeLayerServiceWithDq : ILayerService
    {
        public bool Supports { get; set; } = true;
        public string Query { get; set; } = string.Empty;
        public string LastLayerName { get; private set; } = string.Empty;

        public Task<OperationResult<IReadOnlyList<LayerInfo>>> GetLayersAsync(string? mapName = null, bool flatten = true, CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<LayerInfo>>.Ok(new List<LayerInfo>()));

        public Task<OperationResult<LayerInfo?>> FindLayerAsync(string mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerInfo?>.Ok(null));

        public Task<OperationResult<LayerInfo?>> GetLayerInfoAsync(string mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerInfo?>.Ok(null));

        public Task<OperationResult<bool>> SetLayerVisibilityAsync(string mapName, string layerName, bool visible, CancellationToken ct = default)
            => Task.FromResult(OperationResult<bool>.Ok(true));

        public Task<OperationResult<LayerInfo?>> AddLayerAsync(string mapName, string layerPathOrUri, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerInfo?>.Ok(null));

        public Task<OperationResult<bool>> RemoveLayerAsync(string mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<bool>.Ok(true));

        public Task<OperationResult<LayerSymbologyInfo>> GetLayerSymbologyAsync(string? mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerSymbologyInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<SymbologySetInfo>> SetSimpleSymbologyAsync(string? mapName, string layerName, string? fillColor, string? outlineColor, double? pointSize, double? lineWidth, CancellationToken ct = default)
            => Task.FromResult(OperationResult<SymbologySetInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<LabelInfo>> GetLabelInfoAsync(string? mapName, string layerName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LabelInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<LabelVisibilityInfo>> SetLabelVisibilityAsync(string? mapName, string layerName, bool enabled, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LabelVisibilityInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<DefinitionQueryInfo>> GetDefinitionQueryAsync(string? mapName, string layerName, CancellationToken ct = default)
        {
            LastLayerName = layerName;
            var info = new DefinitionQueryInfo
            {
                LayerName = layerName,
                SupportsDefinitionQuery = Supports,
                DefinitionQuery = Supports ? Query : null,
            };
            return Task.FromResult(OperationResult<DefinitionQueryInfo>.Ok(info));
        }


        public Task<OperationResult<DefinitionQuerySetInfo>> SetDefinitionQueryAsync(
            string? mapName, string layerName, string? definitionQuery, CancellationToken ct = default)
            => Task.FromResult(OperationResult<DefinitionQuerySetInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<LayerOrderInfo>> MoveLayerAsync(
            string? mapName, string layerName, string? referenceLayer, string position, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayerOrderInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));    }

    private static async Task<OperationResult<object?>> CallAsync(
        FakeArcGISHost host, IMCPTool tool, IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    // ---------- get_layout_info ----------

    [Fact]
    public async Task GetLayoutInfo_RoutesNative_AndReturnsDetail()
    {
        var layout = new FakeLayoutService
        {
            Detail = new LayoutDetailInfo
            {
                Name = "Main", Uri = "CIMPATH=Main.json", PageWidth = 8.5, PageHeight = 11,
                PageUnits = "Inches", ElementCount = 2
            }
        };
        var host = new FakeArcGISHost(layout: layout);
        var result = await CallAsync(host, new GetLayoutInfoTool(),
            new Dictionary<string, object?> { ["layoutName"] = "Main" });

        Assert.True(result.Success);
        var info = Assert.IsType<LayoutDetailInfo>(result.Data);
        Assert.Equal(8.5, info.PageWidth);
        Assert.Equal("Inches", info.PageUnits);
        Assert.Equal(2, info.ElementCount);
        Assert.Equal("Main", layout.LastLayoutName);
    }

    [Fact]
    public async Task GetLayoutInfo_MissingLayout_PreservesNotFoundCode()
    {
        var host = new FakeArcGISHost(layout: new FakeLayoutService()); // Detail null → not found
        var result = await CallAsync(host, new GetLayoutInfoTool(),
            new Dictionary<string, object?> { ["layoutName"] = "NOPE" });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.LayerNotFound, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task GetLayoutInfo_Ambiguous_PreservesCode()
    {
        var host = new FakeArcGISHost(layout: new FakeLayoutService
        {
            FailCode = ErrorCodes.AmbiguousLayerName, FailMessage = "Layout name 'A' is ambiguous (2 matches)."
        });
        var result = await CallAsync(host, new GetLayoutInfoTool(),
            new Dictionary<string, object?> { ["layoutName"] = "A" });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.AmbiguousLayerName, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task GetLayoutInfo_MissingArg_FailsWithInvalidArgument()
    {
        var host = new FakeArcGISHost(layout: new FakeLayoutService());
        var result = await CallAsync(host, new GetLayoutInfoTool(), new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }

    // ---------- list_layout_elements ----------

    [Fact]
    public async Task ListLayoutElements_EmptyLayout_ReturnsEmptyArrayNotError()
    {
        // 单子 §1：空集合返回空列表而非错误（与 list_maps 空工程口径一致）。
        var host = new FakeArcGISHost(layout: new FakeLayoutService());
        var result = await CallAsync(host, new ListLayoutElementsTool(),
            new Dictionary<string, object?> { ["layoutName"] = "Main" });

        Assert.True(result.Success);
        var list = Assert.IsType<List<LayoutElementInfo>>(result.Data);
        Assert.Empty(list);
    }

    [Fact]
    public async Task ListLayoutElements_ReturnsElements()
    {
        var host = new FakeArcGISHost(layout: new FakeLayoutService
        {
            Elements =
            {
                new LayoutElementInfo { Name = "Map Frame", ElementType = "MapFrame", IsVisible = true, X = 0.5, Y = 0.5 },
                new LayoutElementInfo { Name = "Title", ElementType = "TextElement", IsVisible = true, X = 1, Y = 10 },
            }
        });
        var result = await CallAsync(host, new ListLayoutElementsTool(),
            new Dictionary<string, object?> { ["layoutName"] = "Main" });

        Assert.True(result.Success);
        var list = Assert.IsType<List<LayoutElementInfo>>(result.Data);
        Assert.Equal(2, list.Count);
    }

    // ---------- get_map_extent ----------

    [Fact]
    public async Task GetMapExtent_ReturnsValuesAndSource()
    {
        var maps = new FakeMapServiceWithExtent
        {
            Extent = new MapExtentInfo
            {
                MapName = "WB_Map", XMin = 1, YMin = 2, XMax = 3, YMax = 4,
                SpatialReferenceName = "NAD_1983_StatePlane", ExtentSource = "default-extent"
            }
        };
        var host = new FakeArcGISHost(maps: maps);
        var result = await CallAsync(host, new GetMapExtentTool(),
            new Dictionary<string, object?> { ["mapName"] = "WB_Map" });

        Assert.True(result.Success);
        var info = Assert.IsType<MapExtentInfo>(result.Data);
        Assert.Equal(1, info.XMin);
        Assert.Equal("NAD_1983_StatePlane", info.SpatialReferenceName);
        Assert.Equal("default-extent", info.ExtentSource);   // 口径披露字段
    }

    [Fact]
    public async Task GetMapExtent_NullNumbers_StillSuccess()
    {
        // 范围无法确定 → 数值 null（显式披露口径），非错误。
        var maps = new FakeMapServiceWithExtent();
        var host = new FakeArcGISHost(maps: maps);
        var result = await CallAsync(host, new GetMapExtentTool(), new Dictionary<string, object?>());

        Assert.True(result.Success);
        var info = Assert.IsType<MapExtentInfo>(result.Data);
        Assert.Null(info.XMin);
        Assert.Equal("default-extent", info.ExtentSource);
    }

    // ---------- get_definition_query ----------

    [Fact]
    public async Task GetDefinitionQuery_SupportsAndSet_ReturnsVerbatim()
    {
        var layers = new FakeLayerServiceWithDq { Supports = true, Query = "TAG = 'A'" };
        var host = new FakeArcGISHost(layers: layers);
        var result = await CallAsync(host, new GetDefinitionQueryTool(),
            new Dictionary<string, object?> { ["layerName"] = "L_Points" });

        Assert.True(result.Success);
        var info = Assert.IsType<DefinitionQueryInfo>(result.Data);
        Assert.True(info.SupportsDefinitionQuery);
        Assert.Equal("TAG = 'A'", info.DefinitionQuery);
        Assert.Equal("L_Points", layers.LastLayerName);
    }

    [Fact]
    public async Task GetDefinitionQuery_SupportsButEmpty_ReturnsEmptyString()
    {
        // 支持但未设置 → 空串（与 null 可判别，G-82-C）。
        var layers = new FakeLayerServiceWithDq { Supports = true, Query = "" };
        var host = new FakeArcGISHost(layers: layers);
        var result = await CallAsync(host, new GetDefinitionQueryTool(),
            new Dictionary<string, object?> { ["layerName"] = "L_Points" });

        Assert.True(result.Success);
        var info = Assert.IsType<DefinitionQueryInfo>(result.Data);
        Assert.True(info.SupportsDefinitionQuery);
        Assert.Equal(string.Empty, info.DefinitionQuery);
    }

    [Fact]
    public async Task GetDefinitionQuery_GroupLayer_ReturnsNullAndUnsupported()
    {
        // GroupLayer 等不支持 → null + supports=false（可判别）。
        var layers = new FakeLayerServiceWithDq { Supports = false, Query = null! };
        var host = new FakeArcGISHost(layers: layers);
        var result = await CallAsync(host, new GetDefinitionQueryTool(),
            new Dictionary<string, object?> { ["layerName"] = "L_Group" });

        Assert.True(result.Success);
        var info = Assert.IsType<DefinitionQueryInfo>(result.Data);
        Assert.False(info.SupportsDefinitionQuery);
        Assert.Null(info.DefinitionQuery);
    }

    [Fact]
    public async Task GetDefinitionQuery_MissingLayerName_FailsInvalidArgument()
    {
        var host = new FakeArcGISHost(layers: new FakeLayerServiceWithDq());
        var result = await CallAsync(host, new GetDefinitionQueryTool(), new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }
}
