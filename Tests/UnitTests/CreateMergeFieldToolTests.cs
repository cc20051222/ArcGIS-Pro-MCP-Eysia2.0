using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 9 第三批（D-036）：create_file_gdb / merge / add_field —— GP 产出写工具编排单测。
/// 记录器（RecordingGeoprocessingService）观测 Tool → Router → IGeoprocessingService 边界；
/// 不执行真实 ArcGIS Pro GP。覆写守卫（GpOverwriteGuard）与 stateProof 语义复用既有机制。
/// </summary>
public sealed class CreateMergeFieldToolTests
{
    private static async Task<OperationResult<object?>> CallAsync(
        RecordingGeoprocessingService service,
        IMCPTool tool,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, new FakeArcGISHost(service), new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    private static OperationResult<GeoprocessingResult> GpOk(string result)
        => OperationResult<GeoprocessingResult>.Ok(new GeoprocessingResult { Result = result, ToolName = "test" });

    // ---------------------------------------------------------------- create_file_gdb

    [Fact]
    public async Task CreateFileGdb_RoutesWithFolderAndName()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("gdb-ok") };
        var result = await CallAsync(service, new CreateFileGdbTool(), new Dictionary<string, object?>
        {
            ["outputPath"] = @"D:\scratch\wb_test.gdb"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("CreateFileGDB_management", request.ToolName);
        Assert.Equal(new[] { @"D:\scratch", "wb_test.gdb", "CURRENT" }, request.Values);
        Assert.Equal(@"D:\scratch\wb_test.gdb", Assert.Single(service.ExistenceChecks));
    }

    [Fact]
    public async Task CreateFileGdb_OutputExists_RefusesWithoutOverwrite()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new CreateFileGdbTool(), new Dictionary<string, object?>
        {
            ["outputPath"] = @"D:\scratch\wb_test.gdb"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests); // 未进 GP、零状态变更
    }

    [Fact]
    public async Task CreateFileGdb_OutputExists_WithOverwriteTrue_Runs()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists, Result = GpOk("gdb-ok") };
        var result = await CallAsync(service, new CreateFileGdbTool(), new Dictionary<string, object?>
        {
            ["outputPath"] = @"D:\scratch\wb_test.gdb", ["overwrite"] = true
        });

        Assert.True(result.Success);
        Assert.Single(service.Requests);
    }

    [Fact]
    public async Task CreateFileGdb_NonGdbSuffix_ReturnsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new CreateFileGdbTool(), new Dictionary<string, object?>
        {
            ["outputPath"] = @"D:\scratch\wb_test"
        });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task CreateFileGdb_BlankPath_ReturnsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new CreateFileGdbTool(), new Dictionary<string, object?> { ["outputPath"] = " " });
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task CreateFileGdb_NameOnly_ReturnsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new CreateFileGdbTool(), new Dictionary<string, object?> { ["outputPath"] = "wb_test.gdb" });
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------------------------------------------------------- merge

    [Fact]
    public async Task Merge_QuotesInputsSoPathsWithSpacesSurvive()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("merge-ok") };
        var result = await CallAsync(service, new MergeTool(), new Dictionary<string, object?>
        {
            ["inputs"] = @"D:\ArcGIS Pro data\fc_a;D:\ArcGIS Pro data\fc_b",
            ["output"] = @"D:\out.gdb\merged"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Merge_management", request.ToolName);
        Assert.Equal("'D:\\ArcGIS Pro data\\fc_a';'D:\\ArcGIS Pro data\\fc_b'", request.Values![0]);
        Assert.Equal(@"D:\out.gdb\merged", request.Values[1]);
    }

    [Fact]
    public async Task Merge_AcceptsLayerNamesAndEmptyMappingDefault()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("merge-ok") };
        var result = await CallAsync(service, new MergeTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "L_Points;L_Lines",
            ["output"] = "out"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("'L_Points';'L_Lines'", request.Values![0]);
        Assert.Equal(string.Empty, request.Values[2]);   // fieldMappings 缺省 = 空（GP 缺省行为）
    }

    [Fact]
    public async Task Merge_NonEmptyFieldMappingsIsExplicitlyRefused()
    {
        // ★ D-052 O-5 修复：非空 fieldMappings → INVALID_ARGUMENT 显式拒绝（spike 证实 GP 对
        // 自由串生成畸形输出 —— 'GARBAGE_NOT_A_MAPPING' 成为输出唯一字段），禁维持旧行为。
        var service = new RecordingGeoprocessingService { Result = GpOk("merge-ok") };
        var result = await CallAsync(service, new MergeTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "A;B", ["output"] = "out", ["fieldMappings"] = "NAME \"NAME\" true true false 64 Text 0 0 ,First,#"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Merge_EmptyItem_ReturnsInvalidArgumentAndDoesNotTouchGp()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new MergeTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "A;;B", ["output"] = "out"
        });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Merge_OutputExists_RefusesWithoutOverwrite()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new MergeTool(), new Dictionary<string, object?>
        {
            ["inputs"] = "A;B", ["output"] = "out"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------------------------------------------------------- add_field

    [Fact]
    public async Task AddField_RoutesWithDefaultTextType()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("addfield-ok") };
        var result = await CallAsync(service, new AddFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = @"D:\data.gdb\FC1", ["fieldName"] = "NOTE"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("AddField_management", request.ToolName);
        Assert.Equal(@"D:\data.gdb\FC1", request.Values![0]);
        Assert.Equal("NOTE", request.Values[1]);
        Assert.Equal("TEXT", request.Values[2]);
        Assert.Equal("NULLABLE", request.Values[7]);
        Assert.Equal("NON_REQUIRED", request.Values[8]);
    }

    [Fact]
    public async Task AddField_PassesTypeLengthAndAlias()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("addfield-ok") };
        var result = await CallAsync(service, new AddFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "FC1", ["fieldName"] = "CODE", ["fieldType"] = "LONG",
            ["fieldLength"] = 16, ["fieldAlias"] = "编码"
        });

        Assert.True(result.Success);
        var values = Assert.Single(service.Requests).Values!;
        Assert.Equal("LONG", values[2]);
        Assert.Equal("16", values[5]);
        Assert.Equal("编码", values[6]);
    }

    [Fact]
    public async Task AddField_BlankFieldName_ReturnsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new AddFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "FC1", ["fieldName"] = " "
        });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task AddField_BlankInput_ReturnsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new AddFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = " ", ["fieldName"] = "NOTE"
        });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }
}
