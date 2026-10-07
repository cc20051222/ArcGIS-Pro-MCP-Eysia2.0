using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 9 第二批（D-035）：copy_dataset / export_table / project —— GP 产出写工具编排单测。
/// 记录器（RecordingGeoprocessingService）观测 Tool → Router → IGeoprocessingService 边界；
/// 不执行真实 ArcGIS Pro GP。覆写守卫（GpOverwriteGuard）与 stateProof 语义复用既有机制。
/// </summary>
public sealed class CopyExportProjectToolTests
{
    // D-037：schema / 坐标系服务可注入（export_table 字段子集核验与 project outSR 解析需要可编程替身）。
    private static async Task<OperationResult<object?>> CallAsync(
        RecordingGeoprocessingService service,
        IMCPTool tool,
        IReadOnlyDictionary<string, object?> arguments,
        FakeFieldProbeSchemaService? schema = null,
        FakeSpatialReferenceService? spatialReferences = null)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var host = new FakeArcGISHost(service, schema: schema, spatialReferences: spatialReferences);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    private static OperationResult<GeoprocessingResult> GpOk(string result)
        => OperationResult<GeoprocessingResult>.Ok(new GeoprocessingResult { Result = result, ToolName = "test" });

    // ---------------------------------------------------------------- copy_dataset

    [Fact]
    public async Task CopyDataset_RoutesWithExactValues()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("copy-ok") };
        var result = await CallAsync(service, new CopyDatasetTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "D:/src.gdb/FC1",
            ["outputPath"] = "D:/dst.gdb/FC1_copy"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Copy_management", request.ToolName);
        Assert.Equal(new[] { "D:/src.gdb/FC1", "D:/dst.gdb/FC1_copy" }, request.Values);
        Assert.Equal("D:/dst.gdb/FC1_copy", Assert.Single(service.ExistenceChecks));
    }

    [Fact]
    public async Task CopyDataset_BlankPath_ReturnsInvalidArgumentAndDoesNotTouchGp()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new CopyDatasetTool(), new Dictionary<string, object?> { ["inputPath"] = " ", ["outputPath"] = "out" });
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task CopyDataset_OutputExists_RefusesWithoutOverwrite()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new CopyDatasetTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src", ["outputPath"] = "out"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests); // 未进 GP
    }

    [Fact]
    public async Task CopyDataset_OutputExists_WithOverwriteTrue_Runs()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists, Result = GpOk("ok") };
        var result = await CallAsync(service, new CopyDatasetTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src", ["outputPath"] = "out", ["overwrite"] = true
        });

        Assert.True(result.Success);
        Assert.Single(service.Requests);
    }

    // ---------------------------------------------------------------- export_table

    [Fact]
    public async Task ExportTable_RoutesWithAllFieldsDefault()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("export-ok") };
        var result = await CallAsync(service, new ExportTableTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src.gdb/TBL", ["outputPath"] = "D:/out/tbl.csv"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("ExportTable_conversion", request.ToolName);
        // D-037 F-D035-1：按 Esri 官方位置传参（field_mapping 在第 5 位，旧实现错放在 where_clause 位）。
        Assert.Equal(new[] { "src.gdb/TBL", "D:/out/tbl.csv", string.Empty, "NOT_USE_ALIAS", string.Empty }, request.Values);
    }

    [Fact]
    public async Task ExportTable_WithFieldSubset_BuildsFieldMapping()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("export-ok") };
        var result = await CallAsync(service, new ExportTableTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src.gdb/TBL",
            ["outputPath"] = "D:/out/tbl.csv",
            ["fieldNames"] = new[] { "NAME", "VALUE" }
        }, schema: new FakeFieldProbeSchemaService
        {
            Fields = new List<SchemaFieldInfo>
            {
                new() { Name = "OBJECTID" }, new() { Name = "NAME" }, new() { Name = "VALUE" },
            }
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.NotNull(request.Values);
        // D-037 F-D035-1：映射串改在 field_mapping 位（索引 4），且执行后核验输出字段集。
        Assert.Equal("NAME NAME VISIBLE NONE;VALUE VALUE VISIBLE NONE", request.Values![4]);
    }

    [Fact]
    public async Task ExportTable_BlankPath_ReturnsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new ExportTableTool(), new Dictionary<string, object?> { ["inputPath"] = "", ["outputPath"] = "out" });
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------------------------------------------------------- project

    [Fact]
    public async Task Project_RoutesWithExactValues()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("project-ok") };
        var result = await CallAsync(service, new ProjectTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src.gdb/FC",
            ["outputPath"] = "D:/out.gdb/FC_p",
            ["outSR"] = "4326"
        });

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Project_management", request.ToolName);
        Assert.Equal(new[] { "src.gdb/FC", "D:/out.gdb/FC_p", "4326" }, request.Values);
    }

    [Fact]
    public async Task Project_BlankOutSr_ReturnsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new ProjectTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src", ["outputPath"] = "out", ["outSR"] = " "
        });
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Project_OutputExists_RefusesWithoutOverwrite()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new ProjectTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src", ["outputPath"] = "out", ["outSR"] = "4326"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ---------------------------------------------------------------- 契约形状

    [Fact]
    public void CopyExportProjectTools_HaveExpectedNamesCategoriesAndExecutionType()
    {
        var tools = new IMCPTool[] { new CopyDatasetTool(), new ExportTableTool(), new ProjectTool() };
        Assert.Equal(["copy_dataset", "export_table", "project"], tools.Select(t => t.Name).ToArray());
        foreach (var tool in tools)
        {
            Assert.Equal(ToolCategories.DataManagement, tool.Metadata.Category);
            Assert.Equal(ExecutionTypes.Geoprocessing, tool.Metadata.ExecutionType);
            Assert.True(tool.Metadata.RequiresArcGIS);
        }
    }
}
