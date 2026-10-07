using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-046（Phase 10 第四批）：简单符号与标注 4 工具的 Fake 层单测。
/// 覆盖：① get_layer_symbology —— SimpleRenderer 字段透出 / 缺 layerName 校验 / 非要素图层 supports=false 透出；
/// ② set_simple_symbology —— 全参数透传 + 读回字段透出 / 零参数校验（工具层）/ 复杂渲染器拒绝透出；
/// ③ get_label_info —— 启用态与 labelClass 数透出 / 缺 layerName 校验；
/// ④ set_label_visibility —— enabled 透传 / 缺 enabled 校验（工具层）/ 无 labelClass 拒绝透出。
/// </summary>
public class SymbologyLabelToolTests
{
    private static async Task<OperationResult<object?>> CallAsync(
        FakeArcGISHost host, IMCPTool tool, IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    private static FakeArcGISHost HostWithLayers(FakeLayerService layers) => new(layers: layers);

    // ---------- ① get_layer_symbology ----------

    [Fact]
    public async Task GetSymbology_SimpleRenderer_ReturnsSymbolFields()
    {
        var layers = new FakeLayerService
        {
            SymbologyHook = (_, name) => OperationResult<LayerSymbologyInfo>.Ok(new LayerSymbologyInfo
            {
                LayerName = name,
                SupportsSymbology = true,
                RendererType = "CIMSimpleRenderer",
                IsSimpleRenderer = true,
                SymbolKind = "Point",
                FillColor = "#FF8000",
                OutlineColor = "#204020",
                PointSize = 9.5,
            }),
        };
        var host = HostWithLayers(layers);

        var r = await CallAsync(host, new GetLayerSymbologyTool(),
            new Dictionary<string, object?> { ["layerName"] = "P_PTS" });

        Assert.True(r.Success);
        var info = Assert.IsType<LayerSymbologyInfo>(r.Data);
        Assert.True(info.SupportsSymbology);
        Assert.True(info.IsSimpleRenderer);
        Assert.Equal("Point", info.SymbolKind);
        Assert.Equal("#FF8000", info.FillColor);
        Assert.Equal("#204020", info.OutlineColor);
        Assert.Equal(9.5, info.PointSize);
    }

    [Fact]
    public async Task GetSymbology_NonFeatureLayer_SupportsFalse()
    {
        var layers = new FakeLayerService
        {
            SymbologyHook = (_, name) => OperationResult<LayerSymbologyInfo>.Ok(new LayerSymbologyInfo
            {
                LayerName = name,
                SupportsSymbology = false,
            }),
        };
        var host = HostWithLayers(layers);

        var r = await CallAsync(host, new GetLayerSymbologyTool(),
            new Dictionary<string, object?> { ["layerName"] = "L_Group" });

        Assert.True(r.Success);
        var info = Assert.IsType<LayerSymbologyInfo>(r.Data);
        Assert.False(info.SupportsSymbology);
        Assert.Null(info.RendererType);
        Assert.Null(info.FillColor);
    }

    [Fact]
    public async Task GetSymbology_MissingLayerName_InvalidArgument()
    {
        var host = HostWithLayers(new FakeLayerService());
        var r = await CallAsync(host, new GetLayerSymbologyTool(), new Dictionary<string, object?>());
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    // ---------- ② set_simple_symbology ----------

    [Fact]
    public async Task SetSimpleSymbology_PassesAllParams_AndReturnsReadBack()
    {
        string? seenFill = null;
        string? seenOutline = null;
        double? seenSize = null;
        double? seenWidth = null;
        var layers = new FakeLayerService
        {
            SymbologySetHook = (_, name, fill, outline, size, width) =>
            {
                seenFill = fill;
                seenOutline = outline;
                seenSize = size;
                seenWidth = width;
                return OperationResult<SymbologySetInfo>.Ok(new SymbologySetInfo
                {
                    LayerName = name,
                    FillColor = fill,
                    OutlineColor = outline,
                    PointSize = size,
                    LineWidth = width,
                });
            },
        };
        var host = HostWithLayers(layers);

        var r = await CallAsync(host, new SetSimpleSymbologyTool(), new Dictionary<string, object?>
        {
            ["layerName"] = "L_Polygons",
            ["fillColor"] = "#00FF00",
            ["outlineColor"] = "#000000",
            ["pointSize"] = 6.0,
            ["lineWidth"] = 1.25,
        });

        Assert.True(r.Success);
        Assert.Equal("#00FF00", seenFill);
        Assert.Equal("#000000", seenOutline);
        Assert.Equal(6.0, seenSize);
        Assert.Equal(1.25, seenWidth);
        var info = Assert.IsType<SymbologySetInfo>(r.Data);
        Assert.Equal("#00FF00", info.FillColor);
        Assert.Equal(6.0, info.PointSize);
        Assert.Equal(1.25, info.LineWidth);
    }

    [Fact]
    public async Task SetSimpleSymbology_NoParams_InvalidArgument()
    {
        var host = HostWithLayers(new FakeLayerService());
        var r = await CallAsync(host, new SetSimpleSymbologyTool(),
            new Dictionary<string, object?> { ["layerName"] = "P_PTS" });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    [Fact]
    public async Task SetSimpleSymbology_MissingLayerName_InvalidArgument()
    {
        var host = HostWithLayers(new FakeLayerService());
        var r = await CallAsync(host, new SetSimpleSymbologyTool(),
            new Dictionary<string, object?> { ["fillColor"] = "#112233" });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    [Fact]
    public async Task SetSimpleSymbology_ComplexRenderer_RejectionSurfaces()
    {
        var layers = new FakeLayerService
        {
            SymbologySetHook = (_, name, _, _, _, _) => OperationResult<SymbologySetInfo>.Fail(
                ErrorCodes.InvalidArgument,
                $"Renderer of layer '{name}' is CIMUniqueValueRenderer; only SimpleRenderer is supported (no silent downgrade)."),
        };
        var host = HostWithLayers(layers);

        var r = await CallAsync(host, new SetSimpleSymbologyTool(), new Dictionary<string, object?>
        {
            ["layerName"] = "L_Points",
            ["fillColor"] = "#AABBCC",
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
        Assert.Contains("SimpleRenderer", r.Errors![0].Message);
    }

    // ---------- ③ get_label_info ----------

    [Fact]
    public async Task GetLabelInfo_ReturnsEnabledAndClassCount()
    {
        var layers = new FakeLayerService
        {
            LabelHook = (_, name) => OperationResult<LabelInfo>.Ok(new LabelInfo
            {
                LayerName = name,
                SupportsLabels = true,
                Enabled = true,
                LabelClassCount = 1,
                Expression = "$feature.NAME",
                ExpressionEngine = "Arcade",
                FontFamily = "Arial",
                FontSize = 10.0,
            }),
        };
        var host = HostWithLayers(layers);

        var r = await CallAsync(host, new GetLabelInfoTool(),
            new Dictionary<string, object?> { ["layerName"] = "L_Points" });

        Assert.True(r.Success);
        var info = Assert.IsType<LabelInfo>(r.Data);
        Assert.True(info.SupportsLabels);
        Assert.True(info.Enabled);
        Assert.Equal(1, info.LabelClassCount);
        Assert.Equal("$feature.NAME", info.Expression);
        Assert.Equal("Arcade", info.ExpressionEngine);
        Assert.Equal("Arial", info.FontFamily);
        Assert.Equal(10.0, info.FontSize);
    }

    [Fact]
    public async Task GetLabelInfo_NotEnabled_IsNotError()
    {
        var layers = new FakeLayerService
        {
            LabelHook = (_, name) => OperationResult<LabelInfo>.Ok(new LabelInfo
            {
                LayerName = name,
                SupportsLabels = true,
                Enabled = false,
                LabelClassCount = 0,
            }),
        };
        var host = HostWithLayers(layers);

        var r = await CallAsync(host, new GetLabelInfoTool(),
            new Dictionary<string, object?> { ["layerName"] = "L_Lines" });

        Assert.True(r.Success);
        var info = Assert.IsType<LabelInfo>(r.Data);
        Assert.False(info.Enabled);
        Assert.Equal(0, info.LabelClassCount);
        Assert.Null(info.Expression);
    }

    [Fact]
    public async Task GetLabelInfo_MissingLayerName_InvalidArgument()
    {
        var host = HostWithLayers(new FakeLayerService());
        var r = await CallAsync(host, new GetLabelInfoTool(), new Dictionary<string, object?>());
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    // ---------- ④ set_label_visibility ----------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetLabelVisibility_PassesFlag_AndReturnsReadBack(bool flag)
    {
        bool? seen = null;
        var layers = new FakeLayerService
        {
            LabelVisibilityHook = (_, name, enabled) =>
            {
                seen = enabled;
                return OperationResult<LabelVisibilityInfo>.Ok(new LabelVisibilityInfo
                {
                    LayerName = name,
                    Enabled = enabled,
                    LabelClassCount = 1,
                });
            },
        };
        var host = HostWithLayers(layers);

        var r = await CallAsync(host, new SetLabelVisibilityTool(), new Dictionary<string, object?>
        {
            ["layerName"] = "L_Points",
            ["enabled"] = flag,
        });

        Assert.True(r.Success);
        Assert.Equal(flag, seen);
        var info = Assert.IsType<LabelVisibilityInfo>(r.Data);
        Assert.Equal(flag, info.Enabled);
        Assert.Equal(1, info.LabelClassCount);
    }

    [Fact]
    public async Task SetLabelVisibility_MissingEnabled_InvalidArgument()
    {
        var host = HostWithLayers(new FakeLayerService());
        var r = await CallAsync(host, new SetLabelVisibilityTool(),
            new Dictionary<string, object?> { ["layerName"] = "L_Points" });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    [Fact]
    public async Task SetLabelVisibility_NoLabelClass_RejectionSurfaces()
    {
        var layers = new FakeLayerService
        {
            LabelVisibilityHook = (_, name, _) => OperationResult<LabelVisibilityInfo>.Fail(
                ErrorCodes.InvalidArgument,
                $"Layer '{name}' has no label classes; enabling labels requires an existing label class (this batch does not create one)."),
        };
        var host = HostWithLayers(layers);

        var r = await CallAsync(host, new SetLabelVisibilityTool(), new Dictionary<string, object?>
        {
            ["layerName"] = "L_Lines",
            ["enabled"] = true,
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
        Assert.Contains("label classes", r.Errors![0].Message);
    }
}
