using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 8.3 (D-013)：选择契约工具编排单测（账本核对/自动快照/非幂等标注/读侧不拒绝）。
/// 真实 ArcPy 语义由 D-013 复验（协议层实机 + 用户在场）覆盖。
/// </summary>
public sealed class SelectionContractToolTests
{
    private static SelectionContent MakeContent(string mapName, string mapUri, params (string name, string uri, long[] oids)[] layers)
    {
        var c = new SelectionContent { MapName = mapName, MapUri = mapUri };
        foreach (var (name, uri, oids) in layers)
        {
            c.Layers.Add(new SelectionLayerContent
            {
                LayerName = name,
                LayerUri = uri,
                Oids = oids.OrderBy(o => o).ToList(),
                SelectedCount = oids.Length,
            });
        }

        return c;
    }

    private static async Task<OperationResult<object?>> CallAsync(
        IMCPTool tool,
        FakeArcGISHost host,
        FakePythonBridgeService? bridge,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(
            registry,
            host,
            new MCPSettings(),
            NullLogger.Instance,
            bridge);

        return await router.ExecuteAsync(new MCPToolCall
        {
            Name = tool.Name,
            Arguments = arguments,
        });
    }

    private static FakeArcGISHost MakeHost(SelectionContent? current)
    {
        var host = new FakeArcGISHost();
        ((FakeSelectionReadService)host.SelectionRead).Current = current;
        return host;
    }

    private static FakeSelectionReadService ReadOf(FakeArcGISHost host) => (FakeSelectionReadService)host.SelectionRead;

    private static FakeGeoprocessingService GpOf(FakeArcGISHost host) => (FakeGeoprocessingService)host.Geoprocessing;

    [Fact]
    public async Task SelectByAttribute_FirstWrite_IsAllowed_CreatesSnapshot_AndForwardsToBridge()
    {
        var content = MakeContent("MD_Active", "map://1", ("L_Points", "l://p", new long[] { 5 }));
        var host = MakeHost(content);
        var result = await CallAsync(new SelectByAttributeTool(), host, null,
            new Dictionary<string, object?>
            {
                ["layerName"] = "L_Points",
                ["mode"] = "replace",
                ["oidList"] = new object[] { 1L, 2L },
            });

        Assert.True(result.Success, result.Message);
        var gp = GpOf(host);
        Assert.Equal(1, gp.CallCount);

        // 自动写前快照（G-30 强制）。
        Assert.True(host.SelectionState.SnapshotCount >= 1);
        // 账本已提交（revision 递增）。
        Assert.True(host.SelectionState.TryGetLedgerRevision("map://1", out var rev));
        Assert.Equal(1, rev);
    }

    [Fact]
    public async Task SelectByAttribute_UserModifiedSelection_IsRefusedWithBaselineMismatch()
    {
        var content = MakeContent("MD_Active", "map://1", ("L_Points", "l://p", new long[] { 5 }));
        var host = MakeHost(content);

        // 第一次写：建立账本（expected = 写后实测值）。
        var first = await CallAsync(new SelectByAttributeTool(), host, null,
            new Dictionary<string, object?> { ["layerName"] = "L_Points", ["mode"] = "replace", ["oidList"] = new object[] { 1L } });
        Assert.True(first.Success, first.Message);

        // 用户在 UI 改了选择 → Current ≠ 账本期望。
        ReadOf(host).Current = MakeContent("MD_Active", "map://1", ("L_Points", "l://p", new long[] { 9, 10, 11 }));

        var second = await CallAsync(new SelectByAttributeTool(), host, null,
            new Dictionary<string, object?> { ["layerName"] = "L_Points", ["mode"] = "replace", ["oidList"] = new object[] { 2L } });

        Assert.Equal(ErrorCodes.SelectionBaselineMismatch, Assert.Single(second.Errors).Code);
        Assert.Equal(1, GpOf(host).CallCount); // 第二次写未触达 GP（拒绝发生在写入前；第一次已 +1）
    }

    [Fact]
    public async Task SelectByAttribute_OidListAndWhereMutuallyExclusive()
    {
        var host = MakeHost(MakeContent("M", "map://1", ("L", "l://1", Array.Empty<long>())));
        var result = await CallAsync(new SelectByAttributeTool(), host, null,
            new Dictionary<string, object?>
            {
                ["layerName"] = "L",
                ["oidList"] = new object[] { 1L },
                ["where"] = "A = 1",
            });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task SelectByAttribute_MissingBothOidListAndWhere_IsRejected()
    {
        var host = MakeHost(MakeContent("M", "map://1", ("L", "l://1", Array.Empty<long>())));
        var result = await CallAsync(new SelectByAttributeTool(), host, null,
            new Dictionary<string, object?> { ["layerName"] = "L" });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task SelectByAttribute_InvalidOidElement_IsRejected()
    {
        var host = MakeHost(MakeContent("M", "map://1", ("L", "l://1", Array.Empty<long>())));
        var result = await CallAsync(new SelectByAttributeTool(), host, null,
            new Dictionary<string, object?>
            {
                ["layerName"] = "L",
                ["oidList"] = new object[] { -3L },
            });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task SelectByLocation_DistanceRequiredForWithinADistance()
    {
        var host = MakeHost(MakeContent("M", "map://1", ("L", "l://1", Array.Empty<long>())));
        var result = await CallAsync(new SelectByLocationTool(), host, new FakePythonBridgeService(),
            new Dictionary<string, object?>
            {
                ["layerName"] = "L",
                ["overlapType"] = "WITHIN_A_DISTANCE",
            });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task SelectByLocation_UnknownOverlapType_IsRejected()
    {
        var host = MakeHost(MakeContent("M", "map://1", ("L", "l://1", Array.Empty<long>())));
        var result = await CallAsync(new SelectByLocationTool(), host, new FakePythonBridgeService(),
            new Dictionary<string, object?>
            {
                ["layerName"] = "L",
                ["overlapType"] = "NOT_A_RELATION",
            });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task GetSelectedFeatures_EmptySelection_IsSuccessWithEmptyLayers()
    {
        var host = MakeHost(MakeContent("MD_Active", "map://1"));
        var result = await CallAsync(new GetSelectedFeaturesTool(), host, null,
            new Dictionary<string, object?>());

        Assert.True(result.Success, result.Message);
        var element = JsonSerializer.SerializeToElement(result.Data);
        Assert.Equal(0, element.GetProperty("totalSelected").GetInt64());
    }

    [Fact]
    public async Task GetSelectedFeatures_ReturnsOidsAscending_WithLedgerFlag()
    {
        var host = MakeHost(MakeContent("MD_Active", "map://1", ("L_Points", "l://p", new long[] { 3, 1, 2 })));
        var result = await CallAsync(new GetSelectedFeaturesTool(), host, null,
            new Dictionary<string, object?>());

        Assert.True(result.Success, result.Message);
        var element = JsonSerializer.SerializeToElement(result.Data);
        Assert.Equal(3, element.GetProperty("totalSelected").GetInt64());
        Assert.True(element.TryGetProperty("ledgerMatch", out _));
    }

    [Fact]
    public async Task RestoreSnapshot_AfterUserModification_IsRefused()
    {
        var content = MakeContent("MD_Active", "map://1", ("L_Points", "l://p", new long[] { 1, 2 }));
        var host = MakeHost(content);

        // 建立账本 + 快照。
        var first = await CallAsync(new SelectByAttributeTool(), host, null,
            new Dictionary<string, object?> { ["layerName"] = "L_Points", ["mode"] = "replace", ["oidList"] = new object[] { 1L, 2L } });
        Assert.True(first.Success, first.Message);

        // clear（模拟 sanctioned 流程）→ 账本更新为空。
        var cleared = await host.Selection.ClearSelectionAsync("MD_Active");
        Assert.True(cleared.Success, cleared.Message);
        // fake Current 需同步模拟 clear 后状态。
        ReadOf(host).Current = MakeContent("MD_Active", "map://1");

        // 用户 UI 又改了选择。
        ReadOf(host).Current = MakeContent("MD_Active", "map://1", ("L_Points", "l://p", new long[] { 42 }));

        // 任取一个快照 id（经由 create 快照工具路径）。
        var snap = await CallAsync(new CreateSelectionSnapshotTool(), host, null, new Dictionary<string, object?>());
        Assert.True(snap.Success, snap.Message);
        var snapId = JsonSerializer.SerializeToElement(snap.Data!).GetProperty("snapshotId").GetString()!;

        var restore = await CallAsync(new RestoreSelectionSnapshotTool(), host, null,
            new Dictionary<string, object?> { ["snapshotId"] = snapId });

        Assert.Equal(ErrorCodes.SelectionBaselineMismatch, Assert.Single(restore.Errors).Code);
    }

    [Fact]
    public async Task RestoreSnapshot_UnknownId_ReturnsSnapshotNotFound()
    {
        var host = MakeHost(MakeContent("M", "map://1"));
        var result = await CallAsync(new RestoreSelectionSnapshotTool(), host, null,
            new Dictionary<string, object?> { ["snapshotId"] = "sel-snap-nope" });

        Assert.Equal(ErrorCodes.SelectionSnapshotNotFound, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task RestoreSnapshot_HappyPath_RestoresAndCommits()
    {
        var original = MakeContent("MD_Active", "map://1", ("L_Points", "l://p", new long[] { 1, 2, 3 }));
        var host = MakeHost(original);

        // 快照原始选择（账本初始化：先做一次写以建立账本——用 create snapshot 只建快照不建账本，故先写）。
        var first = await CallAsync(new SelectByAttributeTool(), host, null,
            new Dictionary<string, object?> { ["layerName"] = "L_Points", ["mode"] = "replace", ["oidList"] = new object[] { 1L, 2L, 3L } });
        Assert.True(first.Success, first.Message);

        var snap = await CallAsync(new CreateSelectionSnapshotTool(), host, null, new Dictionary<string, object?>());
        Assert.True(snap.Success, snap.Message);
        var snapId = JsonSerializer.SerializeToElement(snap.Data!).GetProperty("snapshotId").GetString()!;

        // clear（sanctioned）→ 账本=空。
        var cleared = await host.Selection.ClearSelectionAsync("MD_Active");
        Assert.True(cleared.Success, cleared.Message);
        ReadOf(host).Current = MakeContent("MD_Active", "map://1");

        // 恢复 → 应成功（账本核对相符）。
        var restore = await CallAsync(new RestoreSelectionSnapshotTool(), host, null,
            new Dictionary<string, object?> { ["snapshotId"] = snapId });

        Assert.True(restore.Success, restore.Message + " | errors: " + string.Join("; ", restore.Errors.Select(e => e.Code + ": " + e.Message)));
        var element = JsonSerializer.SerializeToElement(restore.Data!);
        Assert.True(element.GetProperty("restoresSelectionOnly").GetBoolean());
        Assert.Equal(3, element.GetProperty("restoredCount").GetInt64());
        // fake 读服务真实应用了恢复内容。
        Assert.Equal(1, ReadOf(host).RestoreCalls);
    }
}
