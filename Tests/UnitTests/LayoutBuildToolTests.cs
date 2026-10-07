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
/// D-045（Phase 10 第三批）：布局构建写类 5 工具的 Fake 层单测（每工具 ≥1 正例 ≥1 负例）。
/// 覆盖：create_layout 新建成功/同名拒绝/缺参；add_layout_text 正例与空文本负例、服务错误码透传；
/// 图例/指北针/比例尺三元素正例与「地图框缺失」负例；参数透传（坐标/尺寸/命名/字体）核对。
/// </summary>
public class LayoutBuildToolTests
{
    private sealed class FakeLayoutBuildService : ILayoutService
    {
        public string? FailCode { get; set; }
        public string? FailMessage { get; set; }
        public LayoutCreateInfo CreateResult { get; set; } = new()
        {
            Name = "L1", PageWidth = 11, PageHeight = 8.5, PageUnits = "Inches", ElementCount = 1, MapFrameName = "Map Frame"
        };
        public LayoutElementAddInfo AddResult { get; set; } = new()
        {
            LayoutName = "L1", ElementName = "Text_1", ElementType = "TextElement", X = 1, Y = 2, ElementCount = 2
        };
        public (string Name, double W, double H, string Units, string? Map, string? Frame)? LastCreate { get; private set; }
        public (string Layout, string Text, double X, double Y, double? Size, string? Font, string? Elem)? LastText { get; private set; }
        public (string Kind, string Layout, string Frame, double X, double Y, double? W, double? H)? LastSurround { get; private set; }

        public Task<OperationResult<IReadOnlyList<LayoutInfo>>> GetLayoutsAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<LayoutInfo>>.Ok(new List<LayoutInfo>()));

        public Task<OperationResult<LayoutDetailInfo>> GetLayoutInfoAsync(string layoutName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutDetailInfo>.Ok(new LayoutDetailInfo { Name = layoutName }));

        public Task<OperationResult<IReadOnlyList<LayoutElementInfo>>> ListLayoutElementsAsync(string layoutName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<LayoutElementInfo>>.Ok(new List<LayoutElementInfo>()));

        public Task<OperationResult<LayoutExportInfo>> ExportLayoutAsync(
            string layoutName, string outputPath, string format, double? resolution, bool overwrite,
            CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutExportInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

        public Task<OperationResult<LayoutCreateInfo>> CreateLayoutAsync(
            string name, double pageWidth, double pageHeight, string pageUnits, string? mapName,
            string? mapFrameName, double? frameXMin, double? frameYMin, double? frameXMax, double? frameYMax,
            CancellationToken ct = default)
        {
            LastCreate = (name, pageWidth, pageHeight, pageUnits, mapName, mapFrameName);
            if (FailCode is not null)
            {
                return Task.FromResult(OperationResult<LayoutCreateInfo>.Fail(FailCode, FailMessage ?? "fail"));
            }

            return Task.FromResult(OperationResult<LayoutCreateInfo>.Ok(CreateResult));
        }

        public Task<OperationResult<LayoutElementAddInfo>> AddLayoutTextAsync(
            string layoutName, string text, double x, double y, double? fontSize, string? fontFamily,
            string? elementName, CancellationToken ct = default)
        {
            LastText = (layoutName, text, x, y, fontSize, fontFamily, elementName);
            if (FailCode is not null)
            {
                return Task.FromResult(OperationResult<LayoutElementAddInfo>.Fail(FailCode, FailMessage ?? "fail"));
            }

            return Task.FromResult(OperationResult<LayoutElementAddInfo>.Ok(AddResult));
        }

        private Task<OperationResult<LayoutElementAddInfo>> Surround(
            string kind, string layoutName, string mapFrameName, double x, double y,
            double? width, double? height, string? elementName)
        {
            LastSurround = (kind, layoutName, mapFrameName, x, y, width, height);
            if (FailCode is not null)
            {
                return Task.FromResult(OperationResult<LayoutElementAddInfo>.Fail(FailCode, FailMessage ?? "fail"));
            }

            return Task.FromResult(OperationResult<LayoutElementAddInfo>.Ok(new LayoutElementAddInfo
            {
                LayoutName = layoutName,
                ElementName = elementName ?? kind + "_1",
                ElementType = kind,
                X = x,
                Y = y,
                AnchorMapFrame = mapFrameName,
                ElementCount = 3,
            }));
        }

        public Task<OperationResult<LayoutElementAddInfo>> AddLegendAsync(
            string layoutName, string mapFrameName, double x, double y, double? width, double? height,
            string? elementName, CancellationToken ct = default)
            => Surround("Legend", layoutName, mapFrameName, x, y, width, height, elementName);

        public Task<OperationResult<LayoutElementAddInfo>> AddNorthArrowAsync(
            string layoutName, string mapFrameName, double x, double y, double? width, double? height,
            string? elementName, CancellationToken ct = default)
            => Surround("NorthArrow", layoutName, mapFrameName, x, y, width, height, elementName);

        public Task<OperationResult<LayoutElementAddInfo>> AddScaleBarAsync(
            string layoutName, string mapFrameName, double x, double y, double? width, double? height,
            string? elementName, CancellationToken ct = default)
            => Surround("ScaleBar", layoutName, mapFrameName, x, y, width, height, elementName);
    }

    private static async Task<OperationResult<object?>> CallAsync(
        FakeLayoutBuildService layout, IMCPTool tool, IReadOnlyDictionary<string, object?> args)
    {
        var host = new FakeArcGISHost(layout: layout);
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = args });
    }

    // ---------------- create_layout ----------------
    [Fact]
    public async Task CreateLayout_Positive_PassesParamsAndReturnsPageInfo()
    {
        var svc = new FakeLayoutBuildService();
        svc.CreateResult.MapFrameName = "MF1"; // 替身回读值对齐请求（断言「读回值被透出」）
        var r = await CallAsync(svc, new CreateLayoutTool(), new Dictionary<string, object?>
        {
            ["name"] = "L1",
            ["pageWidth"] = 11.0,
            ["pageHeight"] = 8.5,
            ["pageUnits"] = "Inches",
            ["mapName"] = "D_Map",
            ["mapFrameName"] = "MF1",
        });

        Assert.True(r.Success);
        Assert.Equal(("L1", 11.0, 8.5, "Inches", "D_Map", "MF1"), svc.LastCreate);
        var data = Assert.IsType<LayoutCreateInfo>(r.Data);
        Assert.Equal("Inches", data.PageUnits);
        Assert.Equal("MF1", data.MapFrameName);
    }

    [Fact]
    public async Task CreateLayout_Negative_DuplicateNameRejected()
    {
        var svc = new FakeLayoutBuildService { FailCode = ErrorCodes.InvalidArgument, FailMessage = "already exists" };
        var r = await CallAsync(svc, new CreateLayoutTool(), new Dictionary<string, object?>
        {
            ["name"] = "L1", ["pageWidth"] = 11.0, ["pageHeight"] = 8.5,
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    [Fact]
    public async Task CreateLayout_Negative_MissingPageSizeIsInvalidArgument()
    {
        var svc = new FakeLayoutBuildService();
        var r = await CallAsync(svc, new CreateLayoutTool(), new Dictionary<string, object?> { ["name"] = "L1" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
        Assert.Null(svc.LastCreate);
    }

    [Fact]
    public async Task CreateLayout_Negative_MapNotFoundPropagates()
    {
        var svc = new FakeLayoutBuildService { FailCode = ErrorCodes.MapNotFound };
        var r = await CallAsync(svc, new CreateLayoutTool(), new Dictionary<string, object?>
        {
            ["name"] = "L2", ["pageWidth"] = 8.0, ["pageHeight"] = 6.0, ["mapName"] = "NO_MAP",
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.MapNotFound, r.Errors![0].Code);
    }

    // ---------------- add_layout_text ----------------
    [Fact]
    public async Task AddLayoutText_Positive_PassesFontAndPosition()
    {
        var svc = new FakeLayoutBuildService();
        var r = await CallAsync(svc, new AddLayoutTextTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L1", ["text"] = "Hello", ["x"] = 2.0, ["y"] = 3.0,
            ["fontSize"] = 14.0, ["fontFamily"] = "Arial", ["elementName"] = "T1",
        });

        Assert.True(r.Success);
        Assert.Equal(("L1", "Hello", 2.0, 3.0, 14.0, "Arial", "T1"), svc.LastText);
        var data = Assert.IsType<LayoutElementAddInfo>(r.Data);
        Assert.Equal("Text_1", data.ElementName);
    }

    [Fact]
    public async Task AddLayoutText_Negative_EmptyTextIsInvalidArgument()
    {
        var svc = new FakeLayoutBuildService();
        var r = await CallAsync(svc, new AddLayoutTextTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L1", ["text"] = "  ", ["x"] = 1.0, ["y"] = 1.0,
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
        Assert.Null(svc.LastText);
    }

    [Fact]
    public async Task AddLayoutText_Negative_LayoutNotFoundPropagates()
    {
        var svc = new FakeLayoutBuildService { FailCode = ErrorCodes.LayerNotFound };
        var r = await CallAsync(svc, new AddLayoutTextTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "NO_LAYOUT", ["text"] = "x", ["x"] = 1.0, ["y"] = 1.0,
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.LayerNotFound, r.Errors![0].Code);
    }

    [Fact]
    public async Task AddLayoutText_Negative_OutOfPageBoundsPropagatesInvalidArgument()
    {
        var svc = new FakeLayoutBuildService { FailCode = ErrorCodes.InvalidArgument, FailMessage = "outside the page bounds" };
        var r = await CallAsync(svc, new AddLayoutTextTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L1", ["text"] = "x", ["x"] = 99.0, ["y"] = 1.0,
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    // ---------------- legend / north arrow / scale bar ----------------
    [Fact]
    public async Task AddLegend_Positive_AnchorsMapFrame()
    {
        var svc = new FakeLayoutBuildService();
        var r = await CallAsync(svc, new AddLegendTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L1", ["mapFrameName"] = "MF1", ["x"] = 7.0, ["y"] = 1.0, ["width"] = 3.0, ["height"] = 2.0,
        });

        Assert.True(r.Success);
        Assert.Equal(("Legend", "L1", "MF1", 7.0, 1.0, 3.0, 2.0), svc.LastSurround);
        var data = Assert.IsType<LayoutElementAddInfo>(r.Data);
        Assert.Equal("MF1", data.AnchorMapFrame);
    }

    [Fact]
    public async Task AddLegend_Negative_MissingMapFrameNameIsInvalidArgument()
    {
        var svc = new FakeLayoutBuildService();
        var r = await CallAsync(svc, new AddLegendTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L1", ["x"] = 1.0, ["y"] = 1.0,
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
        Assert.Null(svc.LastSurround);
    }

    [Fact]
    public async Task AddNorthArrow_Positive_DefaultsApplied()
    {
        var svc = new FakeLayoutBuildService();
        var r = await CallAsync(svc, new AddNorthArrowTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L1", ["mapFrameName"] = "MF1", ["x"] = 1.0, ["y"] = 7.0,
        });

        Assert.True(r.Success);
        Assert.Equal(("NorthArrow", "L1", "MF1", 1.0, 7.0, null, null), svc.LastSurround);
        var data = Assert.IsType<LayoutElementAddInfo>(r.Data);
        Assert.Equal("NorthArrow_1", data.ElementName);
    }

    [Fact]
    public async Task AddNorthArrow_Negative_MapFrameNotFoundPropagatesInvalidArgument()
    {
        var svc = new FakeLayoutBuildService { FailCode = ErrorCodes.InvalidArgument, FailMessage = "Map frame 'X' not found" };
        var r = await CallAsync(svc, new AddNorthArrowTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L1", ["mapFrameName"] = "X", ["x"] = 1.0, ["y"] = 1.0,
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    [Fact]
    public async Task AddScaleBar_Positive_PassesSize()
    {
        var svc = new FakeLayoutBuildService();
        var r = await CallAsync(svc, new AddScaleBarTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L1", ["mapFrameName"] = "MF1", ["x"] = 4.0, ["y"] = 0.5,
            ["width"] = 2.5, ["height"] = 0.6, ["elementName"] = "SB_X",
        });

        Assert.True(r.Success);
        Assert.Equal(("ScaleBar", "L1", "MF1", 4.0, 0.5, 2.5, 0.6), svc.LastSurround);
        var data = Assert.IsType<LayoutElementAddInfo>(r.Data);
        Assert.Equal("SB_X", data.ElementName);
    }

    [Fact]
    public async Task AddScaleBar_Negative_ServiceInvalidArgumentPropagates()
    {
        var svc = new FakeLayoutBuildService { FailCode = ErrorCodes.InvalidArgument, FailMessage = "width must be positive" };
        var r = await CallAsync(svc, new AddScaleBarTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L1", ["mapFrameName"] = "MF1", ["x"] = 1.0, ["y"] = 1.0, ["width"] = -1.0,
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }
}
