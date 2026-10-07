using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-053（Phase 12 第二批）编辑类新工具行为测试：delete_dataset / rename_dataset / append_features。
/// 每工具 ≥8 例：正例 / 负例 / 守卫（受保护根 + .. 穿越）/ 预检（存在性 fail-closed、confirm、名称合法性、schema 白名单）。
/// 错误码零新增（复用 InvalidArgument / DatasetNotFound / InvalidState / PathEscapeRejected / OutputExists）。
/// 反证锚点 = 守卫与预检（工具层业务校验，非 schema 层）。
/// </summary>
public sealed class D053EditToolTests
{
    private const string FixturePath = "D:/ArcGIS-Pro-MCP 2.0/TestFixtures/Phase8_5_6/WorkBuddyTest.gdb";
    private const string Traversal = "C:/tmp/out.gdb/../../../TestFixtures/Phase8_5_6/x.gdb";
    private const string Work = "C:/work.gdb";

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

    // ============================================================ delete_dataset

    [Fact]
    public async Task Delete_ProtectedTargetIsRefusedWithNoGp()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new DeleteDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = FixturePath + "/FC_Points", ["confirm"] = true
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
        Assert.Empty(service.ExistenceChecks);
    }

    [Fact]
    public async Task Delete_TraversalTargetIsRefused()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new DeleteDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Traversal + "/FC", ["confirm"] = true
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Delete_MissingConfirmIsRefusedBeforeProbe()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new DeleteDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Work + "/FC"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
        Assert.Empty(service.ExistenceChecks);   // confirm 校验先于预检
    }

    [Fact]
    public async Task Delete_ConfirmFalseIsRefused()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new DeleteDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Work + "/FC", ["confirm"] = false
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Delete_MissingTargetIsRefusedBeforeGp()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.NotExists };
        var result = await CallAsync(service, new DeleteDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Work + "/GONE", ["confirm"] = true
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.DatasetNotFound, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);          // 消除 GP「不存在也静默成功」的假成功
    }

    [Fact]
    public async Task Delete_UnprovableExistenceIsRefusedFailClosed()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Unknown };
        var result = await CallAsync(service, new DeleteDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Work + "/FC", ["confirm"] = true
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidState, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Delete_ProbeFailureIsRefusedFailClosed()
    {
        var service = new RecordingGeoprocessingService { ExistenceProbeFails = true };
        var result = await CallAsync(service, new DeleteDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Work + "/FC", ["confirm"] = true
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidState, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Delete_ConfirmedExistingTargetRunsDeleteOnce()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists, Result = GpOk("del") };
        var result = await CallAsync(service, new DeleteDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Work + "/FC", ["confirm"] = true
        });
        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Delete_management", request.ToolName);
        Assert.Equal(new[] { Work + "/FC" }, request.Values!);
        Assert.Equal([Work + "/FC"], service.ExistenceChecks);
    }

    [Fact]
    public async Task Delete_BlankPathIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new DeleteDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = " ", ["confirm"] = true
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ============================================================ rename_dataset

    [Fact]
    public async Task Rename_ProtectedSourceIsRefused()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RenameDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = FixturePath + "/FC_Points", ["newName"] = "NEW"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
        Assert.Empty(service.ExistenceChecks);
    }

    [Fact]
    public async Task Rename_BlankNewNameIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RenameDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Work + "/FC", ["newName"] = " "
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Theory]
    [InlineData("sub\\name")]
    [InlineData("sub/name")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    public async Task Rename_IllegalNameCharactersAreRejected(string bad)
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RenameDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Work + "/FC", ["newName"] = bad
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Rename_ExistingTargetNameIsRefusedBeforeGp()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new RenameDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Work + "/OLD", ["newName"] = "NEW"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Rename_ValidTargetRunsRenameOnce()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.NotExists, Result = GpOk("ren") };
        var result = await CallAsync(service, new RenameDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Work + "/OLD", ["newName"] = "NEW"
        });
        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Rename_management", request.ToolName);
        // Path.Combine 在本机产出反斜杠（工具行为正确，仅平台分隔符差异）
        Assert.Equal(new[] { Work + "/OLD", System.IO.Path.Combine("C:\\work.gdb", "NEW"), string.Empty }, request.Values!);
    }

    [Fact]
    public async Task Rename_BlankSourceIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RenameDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = " ", ["newName"] = "NEW"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Rename_TraversalSourceIsRefused()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new RenameDatasetTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = Traversal + "/FC", ["newName"] = "NEW"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public void Rename_DescriptionDisclosesCrossWorkspaceRefusal()
    {
        // spike 实测：跨工作区 rename 会「成功」但源与目的都不存在 ⇒ 工具层只接受裸名（含分隔符即拒），并须披露。
        var text = new RenameDatasetTool().Description;
        Assert.Contains("跨工作区", text);
        Assert.Contains("不可回退", text);
    }

    // ============================================================ append_features

    [Fact]
    public async Task Append_ProtectedTargetIsRefused()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new AppendFeaturesTool(), new Dictionary<string, object?>
        {
            ["targetPath"] = FixturePath + "/FC_Points", ["sourcePaths"] = Work + "/SRC"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Append_ProtectedSourceIsRefused()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new AppendFeaturesTool(), new Dictionary<string, object?>
        {
            ["targetPath"] = Work + "/TGT", ["sourcePaths"] = FixturePath + "/FC_Points"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Append_IllegalSchemaTypeIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new AppendFeaturesTool(), new Dictionary<string, object?>
        {
            ["targetPath"] = Work + "/TGT", ["sourcePaths"] = Work + "/SRC", ["schemaType"] = "MAYBE"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Append_EmptySourceItemIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new AppendFeaturesTool(), new Dictionary<string, object?>
        {
            ["targetPath"] = Work + "/TGT", ["sourcePaths"] = "A;;B"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Append_DefaultsToStrictTestSchema()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("app") };
        var result = await CallAsync(service, new AppendFeaturesTool(), new Dictionary<string, object?>
        {
            ["targetPath"] = Work + "/TGT", ["sourcePaths"] = "A;B"
        });
        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("Append_management", request.ToolName);
        Assert.Equal(new[] { "'A';'B'", Work + "/TGT", "TEST", string.Empty, string.Empty }, request.Values!);
    }

    [Fact]
    public async Task Append_NoTestIsAllowedWhenExplicitlyRequested()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("app") };
        var result = await CallAsync(service, new AppendFeaturesTool(), new Dictionary<string, object?>
        {
            ["targetPath"] = Work + "/TGT", ["sourcePaths"] = "A", ["schemaType"] = "no_test"
        });
        Assert.True(result.Success);
        Assert.Equal("NO_TEST", Assert.Single(service.Requests).Values![2]);
    }

    [Fact]
    public async Task Append_BlankTargetIsInvalidArgument()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new AppendFeaturesTool(), new Dictionary<string, object?>
        {
            ["targetPath"] = " ", ["sourcePaths"] = "A"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Append_TraversalTargetIsRefused()
    {
        var service = new RecordingGeoprocessingService();
        var result = await CallAsync(service, new AppendFeaturesTool(), new Dictionary<string, object?>
        {
            ["targetPath"] = Traversal + "/TGT", ["sourcePaths"] = "A"
        });
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }
}
