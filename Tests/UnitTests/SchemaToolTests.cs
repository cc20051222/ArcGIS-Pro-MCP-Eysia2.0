using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 9 第一批（D-034）：schema 只读信息族工具编排单测（路由/参数校验/契约形状）。
/// 真实 SDK 语义（GDB 容器/域/子类型/索引）由阶段二（安装 + 真实 Pro）覆盖；本类用 FakeArcGISHost。
/// </summary>
public sealed class SchemaToolTests
{
    private static async Task<OperationResult<object?>> CallAsync(IMCPTool tool, IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, new FakeArcGISHost(), new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    // ---------------------------------------------------------------- get_schema_info

    [Fact]
    public async Task GetSchemaInfo_BlankPath_ReturnsInvalidArgument()
    {
        var result = await CallAsync(new GetSchemaInfoTool(), new Dictionary<string, object?> { ["path"] = "  " });
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidArgument);
    }

    [Fact]
    public async Task GetSchemaInfo_ValidPath_ReturnsPresetSchema()
    {
        var result = await CallAsync(new GetSchemaInfoTool(), new Dictionary<string, object?> { ["path"] = "C:/data.gdb/FC1" });
        Assert.True(result.Success);
        var info = Assert.IsType<SchemaInfo>(result.Data);
        Assert.True(info.Exists);
        Assert.Equal("FeatureClass", info.DataType);
        Assert.Equal(2, info.Fields.Count);
        Assert.Equal("NAME", info.Fields[1].Name);
    }

    [Fact]
    public async Task GetSchemaInfo_MissingArgument_ReturnsInvalidArgument()
    {
        var result = await CallAsync(new GetSchemaInfoTool(), new Dictionary<string, object?>());
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidArgument);
    }

    // ---------------------------------------------------------------- get_domains

    [Fact]
    public async Task GetDomains_BlankWorkspace_ReturnsInvalidArgument()
    {
        var result = await CallAsync(new GetDomainsTool(), new Dictionary<string, object?> { ["workspace"] = "" });
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidArgument);
    }

    [Fact]
    public async Task GetDomains_ValidWorkspace_ReturnsPresetDomains()
    {
        var result = await CallAsync(new GetDomainsTool(), new Dictionary<string, object?> { ["workspace"] = "C:/data.gdb" });
        Assert.True(result.Success);
        var domains = Assert.IsAssignableFrom<IReadOnlyList<DomainInfo>>(result.Data);
        Assert.Single(domains);
        Assert.Equal("Category_D", domains[0].Name);
        Assert.Equal("CodedValue", domains[0].Type);
        Assert.Equal("Alpha", domains[0].CodedValues!["A"]);
    }

    // ---------------------------------------------------------------- get_subtypes

    [Fact]
    public async Task GetSubtypes_BlankPath_ReturnsInvalidArgument()
    {
        var result = await CallAsync(new GetSubtypesTool(), new Dictionary<string, object?> { ["path"] = null! });
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidArgument);
    }

    [Fact]
    public async Task GetSubtypes_ValidPath_ReturnsPresetSubtypes()
    {
        var result = await CallAsync(new GetSubtypesTool(), new Dictionary<string, object?> { ["path"] = "C:/data.gdb/FC1" });
        Assert.True(result.Success);
        var info = Assert.IsType<SubtypeInfo>(result.Data);
        Assert.Equal("CATEGORY", info.SubtypeField);
        Assert.Equal("First", info.Subtypes["1"]);
    }

    // ---------------------------------------------------------------- get_indexes

    [Fact]
    public async Task GetIndexes_BlankPath_ReturnsInvalidArgument()
    {
        var result = await CallAsync(new GetIndexesTool(), new Dictionary<string, object?> { ["path"] = null! });
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == ErrorCodes.InvalidArgument);
    }

    [Fact]
    public async Task GetIndexes_ValidPath_ReturnsPresetIndexes()
    {
        var result = await CallAsync(new GetIndexesTool(), new Dictionary<string, object?> { ["path"] = "C:/data.gdb/FC1" });
        Assert.True(result.Success);
        var indexes = Assert.IsAssignableFrom<IReadOnlyList<IndexInfo>>(result.Data);
        Assert.Single(indexes);
        Assert.Equal("IX_NAME", indexes[0].Name);
        Assert.Contains("NAME", indexes[0].Fields);
    }

    // ---------------------------------------------------------------- 契约形状（与 ProductionToolContractTests 同构）

    [Fact]
    public void SchemaTools_HaveExpectedNamesAndCategories()
    {
        var tools = new IMCPTool[] { new GetSchemaInfoTool(), new GetDomainsTool(), new GetSubtypesTool(), new GetIndexesTool() };
        var names = tools.Select(t => t.Name).ToArray();
        Assert.Equal(["get_schema_info", "get_domains", "get_subtypes", "get_indexes"], names);
        foreach (var tool in tools)
        {
            Assert.Equal(ToolCategories.DataManagement, tool.Metadata.Category);
            Assert.Equal(ExecutionTypes.Native, tool.Metadata.ExecutionType);
            Assert.True(tool.Metadata.RequiresArcGIS);
        }
    }
}
