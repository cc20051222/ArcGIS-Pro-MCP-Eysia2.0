using System.Collections.Generic;
using System.Threading.Tasks;
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
/// D-028：四项缺陷修复的回归锚点
/// — F10 组子层选择快照恢复（可解析性判定 + 不可解析时显式报错，不再静默 restoredCount=0）
/// — F11 intersect 多值输入：支持"数据集路径"与"活动地图图层名"双形态，空项 → INVALID_ARGUMENT（不再误报 000735）
/// — F12 buffer.distance 精度：schema=number，小数不得被截断为 0
/// 说明：F10 的"展平索引"本身需 ArcGIS 运行时（LIVE 覆盖），此处覆盖其纯逻辑判定（Core.SelectionRestoreResolver）。
/// </summary>
public sealed class D028FourDefectFixTests
{
    // ----------------------------------------------------------------- F10

    [Fact]
    public void F10_AllSnapshotLayersResolvable_ReturnsEmpty()
    {
        var available = new[] { "CIMPATH=map/L_Points.json", "CIMPATH=map/L_Sub_A.json" };
        var snapshot = new[]
        {
            new SelectionLayerContent { LayerName = "L_Points", LayerUri = "CIMPATH=map/L_Points.json" },
            new SelectionLayerContent { LayerName = "L_Sub_A", LayerUri = "CIMPATH=map/L_Sub_A.json" }
        };

        Assert.Empty(SelectionRestoreResolver.UnresolvableLayerNames(available, snapshot));
    }

    [Fact]
    public void F10_GroupChildLayerIsResolvableOnlyWhenIndexIsFlattened()
    {
        // 回归根因：非展平索引只有顶层 → 组子层 URI 不在集合中 → 曾静默跳过（restoredCount=0）。
        var topLevelOnly = new[] { "CIMPATH=map/L_Group.json", "CIMPATH=map/L_Points.json" };
        var flattened = new[] { "CIMPATH=map/L_Group.json", "CIMPATH=map/L_Sub_A.json", "CIMPATH=map/L_Points.json" };
        var snapshot = new[] { new SelectionLayerContent { LayerName = "L_Sub_A", LayerUri = "CIMPATH=map/L_Sub_A.json" } };

        Assert.Equal(new[] { "L_Sub_A" }, SelectionRestoreResolver.UnresolvableLayerNames(topLevelOnly, snapshot));
        Assert.Empty(SelectionRestoreResolver.UnresolvableLayerNames(flattened, snapshot));
    }

    [Fact]
    public void F10_UnresolvableLayer_ReportsNameForExplicitError()
    {
        var available = new[] { "CIMPATH=map/L_Points.json" };
        var snapshot = new[]
        {
            new SelectionLayerContent { LayerName = "L_Points", LayerUri = "CIMPATH=map/L_Points.json" },
            new SelectionLayerContent { LayerName = "L_Gone", LayerUri = "CIMPATH=map/L_Gone.json" },
            new SelectionLayerContent { LayerUri = "CIMPATH=map/no-name.json" }   // 无名层 → 回退 URI
        };

        var unresolvable = SelectionRestoreResolver.UnresolvableLayerNames(available, snapshot);

        Assert.Equal(new[] { "L_Gone", "CIMPATH=map/no-name.json" }, unresolvable);
    }

    [Fact]
    public void F10_NullInputsAreHandledWithoutThrowing()
    {
        Assert.Empty(SelectionRestoreResolver.UnresolvableLayerNames(null, null));
        Assert.Empty(SelectionRestoreResolver.UnresolvableLayerNames(new[] { "a" }, null));
    }

    // ----------------------------------------------------------------- F11

    [Fact]
    public void F11_BuilderQuotesEveryItem()
    {
        Assert.Equal("'A';'B'", GpMultiValueBuilder.FromSemicolonList("A;B"));
        Assert.Equal("'D:\\data with space\\fc'", GpMultiValueBuilder.FromSemicolonList("D:\\data with space\\fc"));
        Assert.Equal("'L_Points'", GpMultiValueBuilder.FromSemicolonList(" L_Points "));
    }

    [Fact]
    public void F11_BuilderRejectsEmptyItemsAndBlankInput()
    {
        Assert.Null(GpMultiValueBuilder.FromSemicolonList("A;;B"));
        Assert.Null(GpMultiValueBuilder.FromSemicolonList(";"));
        Assert.Null(GpMultiValueBuilder.FromSemicolonList("   "));
        Assert.Null(GpMultiValueBuilder.FromSemicolonList(null));
    }

    [Fact]
    public async Task F11_IntersectAcceptsDatasetPathsAndQuotesThem()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(
            service,
            new IntersectTool(),
            new Dictionary<string, object?>
            {
                ["inputs"] = @"D:\ArcGIS Pro data\fc_a;D:\ArcGIS Pro data\fc_b",
                ["output"] = @"D:\out\ix"
            });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Intersect_analysis", request.ToolName);
        Assert.Equal("'D:\\ArcGIS Pro data\\fc_a';'D:\\ArcGIS Pro data\\fc_b'", request.Values![0]);
    }

    [Fact]
    public async Task F11_IntersectEmptyItemFailsWithInvalidArgumentNotGpError()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(
            service,
            new IntersectTool(),
            new Dictionary<string, object?>
            {
                ["inputs"] = "A;;B",
                ["output"] = "out"
            });

        Assert.False(result.Success);
        Assert.Contains(ErrorCodes.InvalidArgument, string.Join(",", ErrCodesOf(result)));
        Assert.Empty(service.Requests);   // 未进入 GP
    }

    // ----------------------------------------------------------------- F12

    [Fact]
    public async Task F12_BufferKeepsFractionalDistance()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(
            service,
            new BufferTool(),
            new Dictionary<string, object?>
            {
                ["input"] = "points",
                ["output"] = "points_buf",
                ["distance"] = 0.5,
                ["distanceUnit"] = "DecimalDegrees"
            });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("0.5 DecimalDegrees", request.Values![2]);   // 回归：曾因 GetInt 截断为 "0" → GP 000026
    }

    [Fact]
    public async Task F12_BufferStillFormatsIntegralDistance()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(
            service,
            new BufferTool(),
            new Dictionary<string, object?>
            {
                ["input"] = "roads",
                ["output"] = "roads_buf",
                ["distance"] = 12
            });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("12 Meters", request.Values![2]);
    }

    // ----------------------------------------------------------------- helpers

    private static IEnumerable<string> ErrCodesOf(OperationResult<object?> result)
    {
        foreach (var e in result.Errors)
        {
            yield return e.Code ?? string.Empty;
        }
    }

    private static async Task<OperationResult<object?>> CallAsync(
        RecordingGeoprocessingService service,
        IMCPTool tool,
        IReadOnlyDictionary<string, object?> arguments,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, new FakeArcGISHost(service), new MCPSettings(), NullLogger.Instance);

        return await router.ExecuteAsync(new MCPToolCall
        {
            Name = tool.Name,
            Arguments = arguments,
            CancellationToken = cancellationToken
        });
    }
}
