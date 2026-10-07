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
/// D-061（功能完善第一批 · 生态对齐）：13 件新工具的 Fake 层行为测试（每工具 ≥8 例）。
/// 覆盖：参数透传 / 默认值 / 夹取语义（clamped、truncated）/ 负例（空白、非正数）/ 守卫（受保护根）/ 预检（confirm、存在性 fail-closed）/
/// 宿主错误透传 / 默认接口实现 ⇒ NOT_IMPLEMENTED。
/// 反证锚点 = 工具层业务校验与预检（非 schema required）。
/// </summary>
/// <remarks>
/// <c>set_workspace</c> / <c>get_workspace</c> 读写**进程级**静态状态（<see cref="WorkspaceContext"/>）；
/// 本类内用例执行前均先 <c>Reset()</c>，且仅本类使用该状态。
/// </remarks>
public sealed class D061FeatureBatchTests
{
    private const string FixtureRoot = "D:/ArcGIS-Pro-MCP 2.0/TestFixtures/Phase8_5_6";

    private static async Task<OperationResult<object?>> CallAsync(
        FakeArcGISHost host, IMCPTool tool, IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    // ============================================================ save_project

    [Fact]
    public async Task SaveProject_InPlace_PassesNullToHost_AndSurfacesFacts()
    {
        string? seen = "unset";
        var prj = new D061ProjectService
        {
            SaveHook = s =>
            {
                seen = s;
                return OperationResult<ProjectSaveInfo>.Ok(new ProjectSaveInfo
                {
                    Path = "C:/test.aprx", Name = "test", SavedAs = false, IsDirty = false,
                    FileExists = true, FileSizeBytes = 4096, LastWriteTimeUtc = "2026-09-19T15:00:00.0000000Z"
                });
            }
        };
        var r = await CallAsync(new FakeArcGISHost(project: prj), new SaveProjectTool(), new Dictionary<string, object?>());

        Assert.True(r.Success);
        Assert.Null(seen);                       // 未给 saveAsPath ⇒ 宿主收到 null（原位保存）
        var info = Assert.IsType<ProjectSaveInfo>(r.Data);
        Assert.False(info.SavedAs);
        Assert.True(info.FileExists);
        Assert.Equal(4096, info.FileSizeBytes);
    }

    [Fact]
    public async Task SaveProject_BlankSaveAsTreatsAsInPlace()
    {
        string? seen = "unset";
        var prj = new D061ProjectService
        {
            SaveHook = s => { seen = s; return OperationResult<ProjectSaveInfo>.Ok(new ProjectSaveInfo()); }
        };
        var r = await CallAsync(new FakeArcGISHost(project: prj), new SaveProjectTool(),
            new Dictionary<string, object?> { ["saveAsPath"] = "   " });

        Assert.True(r.Success);
        Assert.Null(seen);
    }

    [Fact]
    public async Task SaveProject_SaveAsPassesTrimmedPath()
    {
        string? seen = null;
        var prj = new D061ProjectService
        {
            SaveHook = s => { seen = s; return OperationResult<ProjectSaveInfo>.Ok(new ProjectSaveInfo { SavedAs = true, Path = s ?? "" }); }
        };
        var r = await CallAsync(new FakeArcGISHost(project: prj), new SaveProjectTool(),
            new Dictionary<string, object?> { ["saveAsPath"] = "  C:/out copy.aprx  " });

        Assert.True(r.Success);
        Assert.Equal("C:/out copy.aprx", seen);
        Assert.True(Assert.IsType<ProjectSaveInfo>(r.Data).SavedAs);
    }

    [Fact]
    public async Task SaveProject_WrongExtension_InvalidArgument_NoHostCall()
    {
        var prj = new D061ProjectService();
        var r = await CallAsync(new FakeArcGISHost(project: prj), new SaveProjectTool(),
            new Dictionary<string, object?> { ["saveAsPath"] = "C:/out.shp" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, prj.SaveCalls);
    }

    [Fact]
    public async Task SaveProject_ExtensionCheckIsCaseInsensitive()
    {
        var prj = new D061ProjectService { SaveHook = _ => OperationResult<ProjectSaveInfo>.Ok(new ProjectSaveInfo()) };
        var r = await CallAsync(new FakeArcGISHost(project: prj), new SaveProjectTool(),
            new Dictionary<string, object?> { ["saveAsPath"] = "C:/OUT.APRX" });

        Assert.True(r.Success);
        Assert.Equal(1, prj.SaveCalls);
    }

    [Fact]
    public async Task SaveProject_ProtectedRoot_PathEscapeRejected_NoHostCall()
    {
        var prj = new D061ProjectService();
        var r = await CallAsync(new FakeArcGISHost(project: prj), new SaveProjectTool(),
            new Dictionary<string, object?> { ["saveAsPath"] = FixtureRoot + "/copy.aprx" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, prj.SaveCalls);
    }

    [Fact]
    public async Task SaveProject_HostErrorIsPropagated()
    {
        var prj = new D061ProjectService
        {
            SaveHook = _ => OperationResult<ProjectSaveInfo>.Fail(ErrorCodes.InvalidState, "no project")
        };
        var r = await CallAsync(new FakeArcGISHost(project: prj), new SaveProjectTool(), new Dictionary<string, object?>());

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidState, Assert.Single(r.Errors!).Code);
    }

    /// <summary>反证：宿主未覆写新方法时，**默认接口实现**返回 NOT_IMPLEMENTED（不得伪造成功）。</summary>
    [Fact]
    public async Task SaveProject_UnimplementedHost_ReturnsNotImplemented()
    {
        var r = await CallAsync(new FakeArcGISHost(), new SaveProjectTool(), new Dictionary<string, object?>());

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Assert.Single(r.Errors!).Code);
    }

    // ============================================================ create_map

    [Fact]
    public async Task CreateMap_PassesNameBasemapAndSpatialReference()
    {
        var maps = new D061MapService();
        var r = await CallAsync(new FakeArcGISHost(maps: maps), new CreateMapTool(), new Dictionary<string, object?>
        {
            ["name"] = "  NewMap  ", ["basemap"] = "Topographic", ["spatialReference"] = "4326"
        });

        Assert.True(r.Success);
        Assert.Equal("NewMap", maps.SeenName);
        Assert.Equal("Topographic", maps.SeenBasemap);
        Assert.Equal("4326", maps.SeenSpatialReference);
        Assert.Equal(1, maps.CreateCalls);
    }

    [Fact]
    public async Task CreateMap_BlankName_InvalidArgument_NoHostCall()
    {
        var maps = new D061MapService();
        var r = await CallAsync(new FakeArcGISHost(maps: maps), new CreateMapTool(),
            new Dictionary<string, object?> { ["name"] = "   " });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, maps.CreateCalls);
    }

    [Fact]
    public async Task CreateMap_MissingName_InvalidArgument()
    {
        var maps = new D061MapService();
        var r = await CallAsync(new FakeArcGISHost(maps: maps), new CreateMapTool(), new Dictionary<string, object?>());

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, maps.CreateCalls);
    }

    [Fact]
    public async Task CreateMap_DuplicateName_InvalidArgument_NoHostCall()
    {
        var maps = new D061MapService();
        var r = await CallAsync(new FakeArcGISHost(maps: maps), new CreateMapTool(),
            new Dictionary<string, object?> { ["name"] = "TestMap" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, maps.CreateCalls);
    }

    [Fact]
    public async Task CreateMap_DuplicateNameIsCaseInsensitive()
    {
        var maps = new D061MapService();
        var r = await CallAsync(new FakeArcGISHost(maps: maps), new CreateMapTool(),
            new Dictionary<string, object?> { ["name"] = "analysismap" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, maps.CreateCalls);
    }

    [Fact]
    public async Task CreateMap_MapsListFailureIsPropagated_NoHostCall()
    {
        var maps = new D061MapService
        {
            MapsResult = OperationResult<IReadOnlyList<MapInfo>>.Fail(ErrorCodes.MapNotFound, "no maps")
        };
        var r = await CallAsync(new FakeArcGISHost(maps: maps), new CreateMapTool(),
            new Dictionary<string, object?> { ["name"] = "Whatever" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.MapNotFound, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, maps.CreateCalls);
    }

    [Fact]
    public async Task CreateMap_HostResultIsSurfaced()
    {
        var maps = new D061MapService
        {
            CreateHook = _ => OperationResult<MapInfo>.Ok(new MapInfo { Name = "NewMap", Uri = "map://new", IsActive = false })
        };
        var r = await CallAsync(new FakeArcGISHost(maps: maps), new CreateMapTool(),
            new Dictionary<string, object?> { ["name"] = "NewMap" });

        Assert.True(r.Success);
        var info = Assert.IsType<MapInfo>(r.Data);
        Assert.Equal("NewMap", info.Name);
        Assert.Equal("map://new", info.Uri);
        Assert.False(info.IsActive);
    }

    [Fact]
    public async Task CreateMap_UnimplementedHost_ReturnsNotImplemented()
    {
        // FakeMapService 未覆写 CreateMapAsync ⇒ 走默认接口实现。
        var r = await CallAsync(new FakeArcGISHost(), new CreateMapTool(),
            new Dictionary<string, object?> { ["name"] = "BrandNew" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Assert.Single(r.Errors!).Code);
    }

    // ============================================================ set_workspace / get_workspace

    [Fact]
    public async Task SetWorkspace_NormalizesTrailingSeparator()
    {
        WorkspaceContext.Reset();
        var r = await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(),
            new Dictionary<string, object?> { ["path"] = "C:/work.gdb/" });

        Assert.True(r.Success);
        var info = Assert.IsType<WorkspaceInfo>(r.Data);
        Assert.Equal("C:\\work.gdb", info.WorkspacePath);
        Assert.True(info.Set);
    }

    [Fact]
    public async Task SetWorkspace_CollapsesDotDotAndSurvivesRoundTrip()
    {
        WorkspaceContext.Reset();
        await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(),
            new Dictionary<string, object?> { ["path"] = "C:/work/../data.gdb" });

        var back = await CallAsync(new FakeArcGISHost(), new GetWorkspaceTool(), new Dictionary<string, object?>());

        Assert.True(back.Success);
        var info = Assert.IsType<WorkspaceInfo>(back.Data);
        Assert.Equal("C:\\data.gdb", info.WorkspacePath);
        Assert.True(info.Set);
    }

    [Fact]
    public async Task SetWorkspace_BlankPath_InvalidArgument_StateUnchanged()
    {
        WorkspaceContext.Reset();
        await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(),
            new Dictionary<string, object?> { ["path"] = "C:/keep.gdb" });

        var r = await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(),
            new Dictionary<string, object?> { ["path"] = "   " });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Equal("C:\\keep.gdb", WorkspaceContext.Current);   // 失败关闭：原值不变
    }

    [Fact]
    public async Task SetWorkspace_MissingPath_InvalidArgument()
    {
        WorkspaceContext.Reset();
        var r = await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(), new Dictionary<string, object?>());

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
    }

    [Fact]
    public async Task SetWorkspace_NonStringScalar_InvalidArgument()
    {
        WorkspaceContext.Reset();
        var r = await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(),
            new Dictionary<string, object?> { ["path"] = 123 });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.False(WorkspaceContext.IsSet);
    }

    [Fact]
    public async Task SetWorkspace_OverwritesPreviousValue()
    {
        WorkspaceContext.Reset();
        await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(),
            new Dictionary<string, object?> { ["path"] = "C:/first.gdb" });
        var r = await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(),
            new Dictionary<string, object?> { ["path"] = "C:/second.gdb" });

        Assert.True(r.Success);
        Assert.Equal("C:\\second.gdb", Assert.IsType<WorkspaceInfo>(r.Data).WorkspacePath);
        Assert.Equal("C:\\second.gdb", WorkspaceContext.Current);
    }

    [Fact]
    public async Task GetWorkspace_UnsetIsSuccessNotError()
    {
        WorkspaceContext.Reset();
        var r = await CallAsync(new FakeArcGISHost(), new GetWorkspaceTool(), new Dictionary<string, object?>());

        Assert.True(r.Success);
        var info = Assert.IsType<WorkspaceInfo>(r.Data);
        Assert.False(info.Set);
        Assert.Equal(string.Empty, info.WorkspacePath);
    }

    [Fact]
    public async Task GetWorkspace_RepeatedReadsAreStable()
    {
        WorkspaceContext.Reset();
        await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(),
            new Dictionary<string, object?> { ["path"] = "C:/stable.gdb" });

        var a = await CallAsync(new FakeArcGISHost(), new GetWorkspaceTool(), new Dictionary<string, object?>());
        var b = await CallAsync(new FakeArcGISHost(), new GetWorkspaceTool(), new Dictionary<string, object?>());

        Assert.Equal(Assert.IsType<WorkspaceInfo>(a.Data).WorkspacePath, Assert.IsType<WorkspaceInfo>(b.Data).WorkspacePath);
    }

    // ============================================================ list_rasters / list_tables

    [Fact]
    public async Task ListRasters_PassesExplicitWorkspace()
    {
        var data = new D061DataService();
        var r = await CallAsync(new FakeArcGISHost(data: data), new ListRastersTool(),
            new Dictionary<string, object?> { ["workspace"] = "C:/ws.gdb" });

        Assert.True(r.Success);
        Assert.Equal("C:/ws.gdb", data.LastWorkspace);
    }

    [Fact]
    public async Task ListRasters_PassesExplicitFolderWorkspace()
    {
        var data = new D061DataService();
        const string folder = "D:/owned/ts08-inputs";
        var r = await CallAsync(new FakeArcGISHost(data: data), new ListRastersTool(),
            new Dictionary<string, object?> { ["workspace"] = folder });

        Assert.True(r.Success);
        Assert.Equal(folder, data.LastWorkspace);
    }

    [Fact]
    public async Task ListRasters_FallsBackToWorkspaceContext()
    {
        WorkspaceContext.Reset();
        await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(),
            new Dictionary<string, object?> { ["path"] = "C:/ctx.gdb" });
        var data = new D061DataService();

        var r = await CallAsync(new FakeArcGISHost(data: data), new ListRastersTool(), new Dictionary<string, object?>());

        Assert.True(r.Success);
        Assert.Equal("C:\\ctx.gdb", data.LastWorkspace);
    }

    [Fact]
    public async Task ListRasters_NoWorkspaceAnywhere_InvalidArgument()
    {
        WorkspaceContext.Reset();
        var data = new D061DataService();
        var r = await CallAsync(new FakeArcGISHost(data: data), new ListRastersTool(), new Dictionary<string, object?>());

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Contains("set_workspace", Assert.Single(r.Errors!).Message);
        Assert.Equal(0, data.RasterCalls);
    }

    [Fact]
    public async Task ListRasters_BlankWorkspaceFallsBackToContext()
    {
        WorkspaceContext.Reset();
        await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(),
            new Dictionary<string, object?> { ["path"] = "C:/ctx.gdb" });
        var data = new D061DataService();

        var r = await CallAsync(new FakeArcGISHost(data: data), new ListRastersTool(),
            new Dictionary<string, object?> { ["workspace"] = "  " });

        Assert.True(r.Success);
        Assert.Equal("C:\\ctx.gdb", data.LastWorkspace);
    }

    [Fact]
    public async Task ListRasters_EmptyWorkspaceIsSuccessWithEmptyCollection()
    {
        var data = new D061DataService
        {
            RastersResult = OperationResult<IReadOnlyList<DatasetInfo>>.Ok(new List<DatasetInfo>())
        };
        var r = await CallAsync(new FakeArcGISHost(data: data), new ListRastersTool(),
            new Dictionary<string, object?> { ["workspace"] = "C:/empty.gdb" });

        Assert.True(r.Success);
        Assert.Empty(Assert.IsType<List<DatasetInfo>>(r.Data));
    }

    [Fact]
    public async Task ListRasters_HostErrorIsPropagated()
    {
        var data = new D061DataService
        {
            RastersResult = OperationResult<IReadOnlyList<DatasetInfo>>.Fail(ErrorCodes.DatasetNotFound, "missing")
        };
        var r = await CallAsync(new FakeArcGISHost(data: data), new ListRastersTool(),
            new Dictionary<string, object?> { ["workspace"] = "C:/nope.gdb" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.DatasetNotFound, Assert.Single(r.Errors!).Code);
    }

    [Fact]
    public async Task ListRasters_UnimplementedHost_ReturnsNotImplemented()
    {
        var r = await CallAsync(new FakeArcGISHost(), new ListRastersTool(),
            new Dictionary<string, object?> { ["workspace"] = "C:/ws.gdb" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Assert.Single(r.Errors!).Code);
    }

    [Fact]
    public async Task ListRasters_ReturnsTypedDatasetCollection()
    {
        var data = new D061DataService
        {
            RastersResult = OperationResult<IReadOnlyList<DatasetInfo>>.Ok(new List<DatasetInfo>
            {
                new() { Name = "R1", Path = "C:/ws.gdb/R1", Type = "Raster" }
            })
        };
        var r = await CallAsync(new FakeArcGISHost(data: data), new ListRastersTool(),
            new Dictionary<string, object?> { ["workspace"] = "C:/ws.gdb" });

        Assert.True(r.Success);
        var list = Assert.IsType<List<DatasetInfo>>(r.Data);
        Assert.Single(list);
        Assert.Equal("Raster", Assert.Single(list).Type);
    }

    [Fact]
    public async Task ListTables_PassesExplicitWorkspace()
    {
        var data = new D061DataService();
        var r = await CallAsync(new FakeArcGISHost(data: data), new ListTablesTool(),
            new Dictionary<string, object?> { ["workspace"] = "C:/ws.gdb" });

        Assert.True(r.Success);
        Assert.Equal("C:/ws.gdb", data.LastWorkspace);
        Assert.Equal(1, data.TableCalls);
        Assert.Equal(0, data.RasterCalls);
    }

    [Fact]
    public async Task ListTables_FallsBackToWorkspaceContext()
    {
        WorkspaceContext.Reset();
        await CallAsync(new FakeArcGISHost(), new SetWorkspaceTool(),
            new Dictionary<string, object?> { ["path"] = "C:/ctx.gdb" });
        var data = new D061DataService();

        var r = await CallAsync(new FakeArcGISHost(data: data), new ListTablesTool(), new Dictionary<string, object?>());

        Assert.True(r.Success);
        Assert.Equal("C:\\ctx.gdb", data.LastWorkspace);
    }

    [Fact]
    public async Task ListTables_NoWorkspaceAnywhere_InvalidArgument()
    {
        WorkspaceContext.Reset();
        var data = new D061DataService();
        var r = await CallAsync(new FakeArcGISHost(data: data), new ListTablesTool(), new Dictionary<string, object?>());

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, data.TableCalls);
    }

    [Fact]
    public async Task ListTables_EmptyIsSuccessWithEmptyCollection()
    {
        var data = new D061DataService
        {
            TablesResult = OperationResult<IReadOnlyList<DatasetInfo>>.Ok(new List<DatasetInfo>())
        };
        var r = await CallAsync(new FakeArcGISHost(data: data), new ListTablesTool(),
            new Dictionary<string, object?> { ["workspace"] = "C:/empty.gdb" });

        Assert.True(r.Success);
        Assert.Empty(Assert.IsType<List<DatasetInfo>>(r.Data));
    }

    [Fact]
    public async Task ListTables_HostErrorIsPropagated()
    {
        var data = new D061DataService
        {
            TablesResult = OperationResult<IReadOnlyList<DatasetInfo>>.Fail(ErrorCodes.DatasetNotFound, "missing")
        };
        var r = await CallAsync(new FakeArcGISHost(data: data), new ListTablesTool(),
            new Dictionary<string, object?> { ["workspace"] = "C:/nope.gdb" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.DatasetNotFound, Assert.Single(r.Errors!).Code);
    }

    [Fact]
    public async Task ListTables_UnimplementedHost_ReturnsNotImplemented()
    {
        var r = await CallAsync(new FakeArcGISHost(), new ListTablesTool(),
            new Dictionary<string, object?> { ["workspace"] = "C:/ws.gdb" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Assert.Single(r.Errors!).Code);
    }

    [Fact]
    public async Task ListTables_ReturnsTypedDatasetCollection()
    {
        var data = new D061DataService
        {
            TablesResult = OperationResult<IReadOnlyList<DatasetInfo>>.Ok(new List<DatasetInfo>
            {
                new() { Name = "T1", Path = "C:/ws.gdb/T1", Type = "Table" }
            })
        };
        var r = await CallAsync(new FakeArcGISHost(data: data), new ListTablesTool(),
            new Dictionary<string, object?> { ["workspace"] = "C:/ws.gdb" });

        Assert.True(r.Success);
        var list = Assert.IsType<List<DatasetInfo>>(r.Data);
        Assert.Equal("Table", Assert.Single(list).Type);
    }

    [Fact]
    public async Task ListTables_DoesNotTouchRasterChannel()
    {
        var data = new D061DataService();
        await CallAsync(new FakeArcGISHost(data: data), new ListTablesTool(),
            new Dictionary<string, object?> { ["workspace"] = "C:/ws.gdb" });

        Assert.Equal(0, data.RasterCalls);
    }

    // ============================================================ get_unique_values

    [Fact]
    public async Task GetUniqueValues_DefaultTopNIs200()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetUniqueValuesTool(),
            new Dictionary<string, object?> { ["layerName"] = "Roads", ["fieldName"] = "CAT" });

        Assert.True(r.Success);
        Assert.Equal(200, attrs.LastMaxDistinct);
        var info = Assert.IsType<FieldValuesInfo>(r.Data);
        Assert.Null(info.RequestedMaxDistinct);
        Assert.Equal(200, info.AppliedMaxDistinct);
        Assert.False(info.Clamped);
    }

    [Fact]
    public async Task GetUniqueValues_ExplicitTopNIsForwarded()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetUniqueValuesTool(),
            new Dictionary<string, object?>
            {
                ["layerName"] = "Roads", ["fieldName"] = "CAT", ["mapName"] = "M1", ["topN"] = 50
            });

        Assert.True(r.Success);
        Assert.Equal(50, attrs.LastMaxDistinct);
        Assert.Equal("M1", attrs.LastMapName);
        var info = Assert.IsType<FieldValuesInfo>(r.Data);
        Assert.Equal(50, info.RequestedMaxDistinct);
        Assert.False(info.Clamped);
    }

    [Fact]
    public async Task GetUniqueValues_AboveCeilingIsClampedTo1000AndFlagged()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetUniqueValuesTool(),
            new Dictionary<string, object?>
            {
                ["layerName"] = "Roads", ["fieldName"] = "CAT", ["topN"] = 5000
            });

        Assert.True(r.Success);
        Assert.Equal(1000, attrs.LastMaxDistinct);
        var info = Assert.IsType<FieldValuesInfo>(r.Data);
        Assert.True(info.Clamped);
        Assert.True(info.Truncated);
        Assert.Equal(1000, info.AppliedMaxDistinct);
        Assert.Equal(5000, info.RequestedMaxDistinct);
    }

    [Fact]
    public async Task GetUniqueValues_NonPositiveTopN_InvalidArgument_NoHostCall()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetUniqueValuesTool(),
            new Dictionary<string, object?>
            {
                ["layerName"] = "Roads", ["fieldName"] = "CAT", ["topN"] = 0
            });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, attrs.FieldValuesCalls);
    }

    [Fact]
    public async Task GetUniqueValues_NegativeTopN_InvalidArgument()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetUniqueValuesTool(),
            new Dictionary<string, object?>
            {
                ["layerName"] = "Roads", ["fieldName"] = "CAT", ["topN"] = -3
            });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
    }

    [Fact]
    public async Task GetUniqueValues_MissingLayerName_InvalidArgument()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetUniqueValuesTool(),
            new Dictionary<string, object?> { ["fieldName"] = "CAT" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, attrs.FieldValuesCalls);
    }

    [Fact]
    public async Task GetUniqueValues_MissingFieldName_InvalidArgument()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetUniqueValuesTool(),
            new Dictionary<string, object?> { ["layerName"] = "Roads" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
    }

    [Fact]
    public async Task GetUniqueValues_HostErrorIsPropagatedVerbatim()
    {
        var attrs = new D061AttributeService
        {
            FieldValuesResult = OperationResult<FieldValuesInfo>.Fail(ErrorCodes.LayerNotFound, "bad field")
        };
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetUniqueValuesTool(),
            new Dictionary<string, object?> { ["layerName"] = "Roads", ["fieldName"] = "NOPE" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.LayerNotFound, Assert.Single(r.Errors!).Code);
    }

    // ============================================================ get_layer_features

    [Fact]
    public async Task GetLayerFeatures_DefaultLimitIs10()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetLayerFeaturesTool(),
            new Dictionary<string, object?> { ["layerName"] = "Roads" });

        Assert.True(r.Success);
        Assert.Equal(10, attrs.LastQuery!.MaxFeatures);
        var payload = Assert.IsType<Dictionary<string, object?>>(r.Data);
        Assert.Equal("row-preview", payload["preset"]);
        Assert.Equal("query_attributes", payload["sourceTool"]);
        Assert.False((bool)payload["clamped"]!);
        Assert.Equal(10, payload["requestedLimit"]);
        Assert.Equal(10, payload["appliedLimit"]);
    }

    [Fact]
    public async Task GetLayerFeatures_ExplicitLimitIsForwarded()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetLayerFeaturesTool(),
            new Dictionary<string, object?> { ["layerName"] = "Roads", ["limit"] = 5 });

        Assert.True(r.Success);
        Assert.Equal(5, attrs.LastQuery!.MaxFeatures);
        var payload = Assert.IsType<Dictionary<string, object?>>(r.Data);
        Assert.Equal(5, payload["appliedLimit"]);
    }

    [Fact]
    public async Task GetLayerFeatures_LimitAbove100IsClamped()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetLayerFeaturesTool(),
            new Dictionary<string, object?> { ["layerName"] = "Roads", ["limit"] = 9999 });

        Assert.True(r.Success);
        Assert.Equal(100, attrs.LastQuery!.MaxFeatures);   // 硬夹 100
        var payload = Assert.IsType<Dictionary<string, object?>>(r.Data);
        Assert.True((bool)payload["clamped"]!);
        Assert.Equal(9999, payload["requestedLimit"]);
        Assert.Equal(100, payload["appliedLimit"]);
    }

    [Fact]
    public async Task GetLayerFeatures_NonPositiveLimit_InvalidArgument()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetLayerFeaturesTool(),
            new Dictionary<string, object?> { ["layerName"] = "Roads", ["limit"] = 0 });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Null(attrs.LastQuery);
    }

    [Fact]
    public async Task GetLayerFeatures_MissingLayerName_InvalidArgument()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetLayerFeaturesTool(),
            new Dictionary<string, object?>());

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
    }

    [Fact]
    public async Task GetLayerFeatures_RowsAreSortedByOidAscending()
    {
        var attrs = new D061AttributeService
        {
            QueryResult = OperationResult<IReadOnlyList<FeatureInfo>>.Ok(new List<FeatureInfo>
            {
                new() { Oid = 7, Attributes = new Dictionary<string, object?> { ["NAME"] = "g" } },
                new() { Oid = 2, Attributes = new Dictionary<string, object?> { ["NAME"] = "b" } },
                new() { Oid = 5, Attributes = new Dictionary<string, object?> { ["NAME"] = "e" } },
            })
        };
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetLayerFeaturesTool(),
            new Dictionary<string, object?> { ["layerName"] = "Roads", ["limit"] = 3 });

        Assert.True(r.Success);
        var payload = Assert.IsType<Dictionary<string, object?>>(r.Data);
        var rows = Assert.IsType<List<FeatureInfo>>(payload["features"]);
        Assert.Equal(new long[] { 2, 5, 7 }, rows.Select(x => x.Oid).ToArray());
        Assert.Equal(3, payload["count"]);
    }

    [Fact]
    public async Task GetLayerFeatures_WhereAndFieldNamesAreForwarded()
    {
        var attrs = new D061AttributeService();
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetLayerFeaturesTool(),
            new Dictionary<string, object?>
            {
                ["layerName"] = "Roads", ["mapName"] = "M1", ["where"] = "POP>100",
                ["fieldNames"] = new[] { "NAME", "POP" }
            });

        Assert.True(r.Success);
        Assert.Equal("POP>100", attrs.LastQuery!.WhereClause);
        Assert.Equal("M1", attrs.LastQuery!.MapName);
        Assert.Equal(new[] { "NAME", "POP" }, attrs.LastQuery!.FieldNames!.ToArray());
    }

    [Fact]
    public async Task GetLayerFeatures_HostErrorIsPropagated()
    {
        var attrs = new D061AttributeService
        {
            QueryResult = OperationResult<IReadOnlyList<FeatureInfo>>.Fail(ErrorCodes.LayerNotFound, "no layer")
        };
        var r = await CallAsync(new FakeArcGISHost(attributes: attrs), new GetLayerFeaturesTool(),
            new Dictionary<string, object?> { ["layerName"] = "Nope" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.LayerNotFound, Assert.Single(r.Errors!).Code);
    }

    // ============================================================ export_layout_*（四新格式）

    [Theory]
    [InlineData("jpg", "JPEG", @"D:\out\p.jpg")]
    [InlineData("jpeg", "JPEG", @"D:\out\p.jpeg")]
    [InlineData("tif", "TIFF", @"D:\out\p.tif")]
    [InlineData("tiff", "TIFF", @"D:\out\p.tiff")]
    [InlineData("svg", "SVG", @"D:\out\p.svg")]
    [InlineData("eps", "EPS", @"D:\out\p.eps")]
    public async Task Export_AllParamsAreForwarded(string ext, string expectedFormat, string path)
    {
        string? seenFormat = null; string? seenPath = null; string? seenLayout = null;
        double? seenRes = null; bool? seenOverwrite = null;
        var fake = ExportFake((layout, p, fmt, res, ow) =>
        {
            seenLayout = layout; seenPath = p; seenFormat = fmt; seenRes = res; seenOverwrite = ow;
            return OperationResult<LayoutExportInfo>.Ok(new LayoutExportInfo
            {
                LayoutName = layout, OutputPath = p, Format = fmt, Resolution = res,
                FileSizeBytes = 2048, MagicBytesHex = "FFFF", Overwritten = ow
            });
        });
        var host = new FakeArcGISHost(layout: fake);
        IMCPTool tool = ToolFor(ext);

        var r = await CallAsync(host, tool, new Dictionary<string, object?>
        {
            ["layoutName"] = "L_Map", ["outputPath"] = path, ["resolution"] = 300.0, ["overwrite"] = true
        });

        Assert.True(r.Success);
        Assert.Equal("L_Map", seenLayout);
        Assert.Equal(expectedFormat, seenFormat);
        Assert.Equal(path, seenPath);
        Assert.Equal(300.0, seenRes);
        Assert.True(seenOverwrite);
        var info = Assert.IsType<LayoutExportInfo>(r.Data);
        Assert.Equal(2048, info.FileSizeBytes);
        Assert.Equal("FFFF", info.MagicBytesHex);
    }

    [Theory]
    [InlineData("jpg", @"D:\out\p.png")]
    [InlineData("tif", @"D:\out\p.jpg")]
    [InlineData("svg", @"D:\out\p.pdf")]
    [InlineData("eps", @"D:\out\p.svg")]
    public async Task Export_WrongExtension_InvalidArgument_NoHostCall(string ext, string badPath)
    {
        int calls = 0;
        var fake = ExportFake((layout, p, fmt, res, ow) =>
        {
            calls++;
            return OperationResult<LayoutExportInfo>.Ok(new LayoutExportInfo());
        });

        var r = await CallAsync(new FakeArcGISHost(layout: fake), ToolFor(ext), new Dictionary<string, object?>
        {
            ["layoutName"] = "L_Map", ["outputPath"] = badPath
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("tif")]
    [InlineData("svg")]
    [InlineData("eps")]
    public async Task Export_ProtectedRoot_PathEscapeRejected_NoHostCall(string ext)
    {
        int calls = 0;
        var fake = ExportFake((layout, p, fmt, res, ow) =>
        {
            calls++;
            return OperationResult<LayoutExportInfo>.Ok(new LayoutExportInfo());
        });

        var r = await CallAsync(new FakeArcGISHost(layout: fake), ToolFor(ext), new Dictionary<string, object?>
        {
            ["layoutName"] = "L_Map", ["outputPath"] = FixtureRoot + "/out." + ext
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(r.Errors!).Code);
        Assert.Equal(0, calls);   // 零写入
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("tif")]
    [InlineData("svg")]
    [InlineData("eps")]
    public async Task Export_InvalidResolution_InvalidArgument(string ext)
    {
        var r = await CallAsync(new FakeArcGISHost(layout: new NotImplementedService()), ToolFor(ext),
            new Dictionary<string, object?>
            {
                ["layoutName"] = "L_Map", ["outputPath"] = @"D:\out\p." + ext, ["resolution"] = 0.0
            });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("tif")]
    [InlineData("svg")]
    [InlineData("eps")]
    public async Task Export_MissingLayoutName_InvalidArgument(string ext)
    {
        var r = await CallAsync(new FakeArcGISHost(layout: new NotImplementedService()), ToolFor(ext),
            new Dictionary<string, object?> { ["outputPath"] = @"D:\out\p." + ext });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("tif")]
    [InlineData("svg")]
    [InlineData("eps")]
    public async Task Export_BlankOutputPath_InvalidArgument(string ext)
    {
        var r = await CallAsync(new FakeArcGISHost(layout: new NotImplementedService()), ToolFor(ext),
            new Dictionary<string, object?> { ["layoutName"] = "L_Map", ["outputPath"] = "  " });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("tif")]
    [InlineData("svg")]
    [InlineData("eps")]
    public async Task Export_OutputExistsIsPropagated(string ext)
    {
        var fake = ExportFake((layout, p, fmt, res, ow) => OperationResult<LayoutExportInfo>.Fail(
            ErrorCodes.OutputExists, "Output file already exists: " + p));

        var r = await CallAsync(new FakeArcGISHost(layout: fake), ToolFor(ext),
            new Dictionary<string, object?> { ["layoutName"] = "L_Map", ["outputPath"] = @"D:\out\p." + ext });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(r.Errors!).Code);
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("tif")]
    [InlineData("svg")]
    [InlineData("eps")]
    public async Task Export_UnimplementedHost_ReturnsNotImplemented(string ext)
    {
        var r = await CallAsync(new FakeArcGISHost(layout: new NotImplementedService()), ToolFor(ext),
            new Dictionary<string, object?> { ["layoutName"] = "L_Map", ["outputPath"] = @"D:\out\p." + ext });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.NotImplemented, Assert.Single(r.Errors!).Code);
    }

    private static IMCPTool ToolFor(string ext) => ext switch
    {
        "jpg" or "jpeg" => new ExportLayoutJpgTool(),
        "tif" or "tiff" => new ExportLayoutTifTool(),
        "svg" => new ExportLayoutSvgTool(),
        _ => new ExportLayoutEpsTool()
    };

    private static NotImplementedService ExportFake(
        Func<string, string, string, double?, bool, OperationResult<LayoutExportInfo>> hook)
        => new() { ExportLayoutHook = hook };

    // ============================================================ repair_geometry

    [Fact]
    public async Task Repair_ConfirmTrue_ProbesThenRunsGpWithCorrectParameterOrder()
    {
        var gp = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var r = await CallAsync(new FakeArcGISHost(gp), new RepairGeometryTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = "C:/work.gdb/FC1", ["confirm"] = true
        });

        Assert.True(r.Success);
        Assert.Equal(new[] { "C:/work.gdb/FC1" }, gp.ExistenceChecks.ToArray());
        var request = Assert.Single(gp.Requests);
        Assert.Equal("RepairGeometry_management", request.ToolName);
        Assert.Equal(new[] { "C:/work.gdb/FC1", "KEEP_NULL" }, request.Values!.ToArray());
    }

    [Fact]
    public async Task Repair_DeleteNullTrue_PassesDeleteNullKeyword()
    {
        var gp = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var r = await CallAsync(new FakeArcGISHost(gp), new RepairGeometryTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = "C:/work.gdb/FC1", ["confirm"] = true, ["deleteNull"] = true
        });

        Assert.True(r.Success);
        Assert.Equal(new[] { "C:/work.gdb/FC1", "DELETE_NULL" }, Assert.Single(gp.Requests).Values!.ToArray());
    }

    [Fact]
    public async Task Repair_MissingConfirm_InvalidArgument_NoProbeNoGp()
    {
        var gp = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var r = await CallAsync(new FakeArcGISHost(gp), new RepairGeometryTool(),
            new Dictionary<string, object?> { ["datasetPath"] = "C:/work.gdb/FC1" });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Empty(gp.ExistenceChecks);
        Assert.Empty(gp.Requests);
    }

    [Fact]
    public async Task Repair_ConfirmFalse_InvalidArgument_NoProbeNoGp()
    {
        var gp = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var r = await CallAsync(new FakeArcGISHost(gp), new RepairGeometryTool(),
            new Dictionary<string, object?> { ["datasetPath"] = "C:/work.gdb/FC1", ["confirm"] = false });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Empty(gp.Requests);
    }

    [Fact]
    public async Task Repair_ProtectedRoot_PathEscapeRejected_NoProbeNoGp()
    {
        var gp = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var r = await CallAsync(new FakeArcGISHost(gp), new RepairGeometryTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = FixtureRoot + "/FC_Points", ["confirm"] = true
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(r.Errors!).Code);
        Assert.Empty(gp.ExistenceChecks);
        Assert.Empty(gp.Requests);
    }

    [Fact]
    public async Task Repair_MissingDatasetPath_InvalidArgument_NoProbeNoGp()
    {
        var gp = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var r = await CallAsync(new FakeArcGISHost(gp), new RepairGeometryTool(),
            new Dictionary<string, object?> { ["confirm"] = true });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(r.Errors!).Code);
        Assert.Empty(gp.Requests);
    }

    [Fact]
    public async Task Repair_NotExists_DatasetNotFound_NoGp()
    {
        var gp = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.NotExists };
        var r = await CallAsync(new FakeArcGISHost(gp), new RepairGeometryTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = "C:/work.gdb/Gone", ["confirm"] = true
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.DatasetNotFound, Assert.Single(r.Errors!).Code);
        Assert.Empty(gp.Requests);   // 不存在 ⇒ 不进 GP（消除假成功）
    }

    [Fact]
    public async Task Repair_UnprovableExistence_InvalidState_NoGp()
    {
        var gp = new RecordingGeoprocessingService
        {
            ExistenceResult = OutputExistence.Unknown,
            ExistenceProbeFails = false
        };
        var r = await CallAsync(new FakeArcGISHost(gp), new RepairGeometryTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = "C:/work.gdb/Maybe", ["confirm"] = true
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidState, Assert.Single(r.Errors!).Code);
        Assert.Empty(gp.Requests);
    }

    [Fact]
    public async Task Repair_ProbeFailure_InvalidState_NoGp()
    {
        var gp = new RecordingGeoprocessingService
        {
            ExistenceResult = OutputExistence.Exists,
            ExistenceProbeFails = true
        };
        var r = await CallAsync(new FakeArcGISHost(gp), new RepairGeometryTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = "C:/work.gdb/FC1", ["confirm"] = true
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidState, Assert.Single(r.Errors!).Code);
        Assert.Empty(gp.Requests);
    }

    [Fact]
    public async Task Repair_GpErrorIsPropagated()
    {
        var gp = new RecordingGeoprocessingService
        {
            ExistenceResult = OutputExistence.Exists,
            Result = OperationResult<GeoprocessingResult>.Fail(ErrorCodes.GeoprocessingError, "GP failed")
        };
        var r = await CallAsync(new FakeArcGISHost(gp), new RepairGeometryTool(), new Dictionary<string, object?>
        {
            ["datasetPath"] = "C:/work.gdb/FC1", ["confirm"] = true
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.GeoprocessingError, Assert.Single(r.Errors!).Code);
    }
}

// ============================================================ 测试替身（D-061）

internal sealed class D061ProjectService : IProjectService
{
    public Func<string?, OperationResult<ProjectSaveInfo>>? SaveHook { get; set; }
    public int SaveCalls { get; private set; }

    public Task<OperationResult<ProjectInfo>> GetProjectInfoAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<ProjectInfo>.Ok(new ProjectInfo { Name = "TestProject", Path = "C:/test.aprx" }));

    public Task<OperationResult<IReadOnlyList<LayoutInfo>>> ListLayoutsAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<LayoutInfo>>.Ok(new List<LayoutInfo>()));

    public Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListDatabasesAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<DatasetInfo>>.Ok(new List<DatasetInfo>()));

    public Task<OperationResult<ProjectSaveInfo>> SaveProjectAsync(string? saveAsPath, CancellationToken ct = default)
    {
        SaveCalls++;
        return Task.FromResult(SaveHook is null
            ? OperationResult<ProjectSaveInfo>.Fail(ErrorCodes.NotImplemented, "not implemented")
            : SaveHook(saveAsPath));
    }
}

internal sealed class D061MapService : IMapService
{
    public OperationResult<IReadOnlyList<MapInfo>> MapsResult { get; set; } =
        OperationResult<IReadOnlyList<MapInfo>>.Ok(new List<MapInfo>
        {
            new() { Name = "TestMap", Uri = "map://test" },
            new() { Name = "AnalysisMap", Uri = "map://analysis" }
        });

    public Func<string, OperationResult<MapInfo>>? CreateHook { get; set; }
    public int CreateCalls { get; private set; }
    public string? SeenName { get; private set; }
    public string? SeenBasemap { get; private set; }
    public string? SeenSpatialReference { get; private set; }

    public Task<OperationResult<MapInfo?>> GetCurrentMapAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapInfo?>.Ok(new MapInfo { Name = "TestMap" }));

    public Task<OperationResult<IReadOnlyList<MapInfo>>> GetMapsAsync(CancellationToken ct = default)
        => Task.FromResult(MapsResult);

    public Task<OperationResult<MapExtentInfo>> GetMapExtentAsync(string? mapName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapExtentInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    public Task<OperationResult<MapExtentSetInfo>> SetMapExtentAsync(
        string? mapName, double xMin, double yMin, double xMax, double yMax, string? spatialReference,
        CancellationToken ct = default)
        => Task.FromResult(OperationResult<MapExtentSetInfo>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    public Task<OperationResult<MapInfo>> CreateMapAsync(
        string name, string? basemap, string? spatialReference, CancellationToken ct = default)
    {
        CreateCalls++;
        SeenName = name;
        SeenBasemap = basemap;
        SeenSpatialReference = spatialReference;
        return Task.FromResult(CreateHook is null
            ? OperationResult<MapInfo>.Ok(new MapInfo { Name = name, Uri = "map://" + name, IsActive = false })
            : CreateHook(name));
    }
}

internal sealed class D061DataService : IDataManagementService
{
    public OperationResult<IReadOnlyList<DatasetInfo>> RastersResult { get; set; } =
        OperationResult<IReadOnlyList<DatasetInfo>>.Ok(new List<DatasetInfo>());

    public OperationResult<IReadOnlyList<DatasetInfo>> TablesResult { get; set; } =
        OperationResult<IReadOnlyList<DatasetInfo>>.Ok(new List<DatasetInfo>());

    public string? LastWorkspace { get; private set; }
    public int RasterCalls { get; private set; }
    public int TableCalls { get; private set; }

    public Task<OperationResult<DatasetInfo>> GetDatasetInfoAsync(string path, CancellationToken ct = default)
        => Task.FromResult(OperationResult<DatasetInfo>.Fail(ErrorCodes.DatasetNotFound, "not implemented"));

    public Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListRastersAsync(string? workspace, CancellationToken ct = default)
    {
        RasterCalls++;
        LastWorkspace = workspace;
        return Task.FromResult(RastersResult);
    }

    public Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListTablesAsync(string? workspace, CancellationToken ct = default)
    {
        TableCalls++;
        LastWorkspace = workspace;
        return Task.FromResult(TablesResult);
    }
}

internal sealed class D061AttributeService : IAttributeService
{
    public OperationResult<IReadOnlyList<FeatureInfo>> QueryResult { get; set; } =
        OperationResult<IReadOnlyList<FeatureInfo>>.Ok(new List<FeatureInfo>
        {
            new() { Oid = 1, Attributes = new Dictionary<string, object?> { ["NAME"] = "A" } }
        });

    public OperationResult<FieldValuesInfo> FieldValuesResult { get; set; } =
        OperationResult<FieldValuesInfo>.Ok(new FieldValuesInfo
        {
            MapName = "M", LayerName = "L", FieldName = "F", TotalCount = 5, DistinctCount = 3,
            DistinctValues = new List<string> { "A", "B", "C" }
        });

    public AttributeQueryRequest? LastQuery { get; private set; }
    public int LastMaxDistinct { get; private set; } = -1;
    public string? LastMapName { get; private set; }
    public int FieldValuesCalls { get; private set; }

    public Task<OperationResult<IReadOnlyList<FeatureInfo>>> QueryFeaturesAsync(AttributeQueryRequest request, CancellationToken ct = default)
    {
        LastQuery = request;
        return Task.FromResult(QueryResult);
    }

    public Task<OperationResult<IReadOnlyList<FieldInfo>>> GetFieldInfoAsync(string mapName, string layerName, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<FieldInfo>>.Ok(new List<FieldInfo>()));

    public Task<OperationResult<long>> GetFeatureCountAsync(string mapName, string layerName, string? whereClause = null, CancellationToken ct = default)
        => Task.FromResult(OperationResult<long>.Ok(0));

    public Task<OperationResult<FieldValuesInfo>> GetFieldValuesAsync(
        string mapName, string layerName, string fieldName, int maxDistinct = 200, CancellationToken ct = default)
    {
        FieldValuesCalls++;
        LastMaxDistinct = maxDistinct;
        LastMapName = mapName;
        return Task.FromResult(FieldValuesResult);
    }
}
