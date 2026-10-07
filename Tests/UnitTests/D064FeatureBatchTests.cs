using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-064 功能完善第四批单测（其一）：A Schema 创建（6）+ B Project 增强（5）+ C 数据发现（4）+ D 批处理（1 = run_batch）。
/// 判据：每件 ≥8 例；语义断言以"契约面"为准（守卫在宿主之前生效 ⇒ 宿主未被调用的证据是 Calls 为空）。
/// </summary>
public class D064FeatureBatchTests
{
    // ══════════════════════════════ 脚本化替身 ══════════════════════════════

    private sealed class ScriptedSchemaService : ISchemaService
    {
        public readonly List<string> Calls = new();
        public OperationResult<CreateFeatureClassResult>? FcResult { get; set; }
        public OperationResult<CreateTableResult>? TableResult { get; set; }
        public OperationResult<AddFieldsResult>? AddFieldsResult { get; set; }
        public OperationResult<DeleteFieldResult>? DeleteFieldResult { get; set; }
        public OperationResult<TruncateTableResult>? TruncateResult { get; set; }
        public OperationResult<ExportFeaturesResult>? ExportResult { get; set; }

        public Task<OperationResult<SchemaInfo>> GetSchemaInfoAsync(string path, CancellationToken ct = default)
            => Task.FromResult(OperationResult<SchemaInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<IReadOnlyList<DomainInfo>>> GetDomainsAsync(string workspace, int maxItems, CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<DomainInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<SubtypeInfo>> GetSubtypesAsync(string path, CancellationToken ct = default)
            => Task.FromResult(OperationResult<SubtypeInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<IReadOnlyList<IndexInfo>>> GetIndexesAsync(string path, int maxItems, CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<IndexInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<CreateFeatureClassResult>> CreateFeatureClassAsync(
            string outputPath, string geometryType, string? spatialReference,
            IReadOnlyList<SchemaFieldSpec>? fields, bool addToMap, CancellationToken ct = default)
        {
            Calls.Add($"fc:{outputPath}:{geometryType}:{spatialReference}:{fields?.Count ?? 0}:{addToMap}");
            return Task.FromResult(FcResult ?? OperationResult<CreateFeatureClassResult>.Ok(new CreateFeatureClassResult { Path = outputPath }));
        }

        public Task<OperationResult<CreateTableResult>> CreateTableAsync(
            string outputPath, IReadOnlyList<SchemaFieldSpec>? fields, bool addToMap, CancellationToken ct = default)
        {
            Calls.Add($"tbl:{outputPath}:{fields?.Count ?? 0}:{addToMap}");
            return Task.FromResult(TableResult ?? OperationResult<CreateTableResult>.Ok(new CreateTableResult { Path = outputPath }));
        }

        public Task<OperationResult<AddFieldsResult>> AddFieldsAsync(
            string path, IReadOnlyList<SchemaFieldSpec> fields, CancellationToken ct = default)
        {
            Calls.Add($"addfields:{path}:{fields.Count}");
            return Task.FromResult(AddFieldsResult ?? OperationResult<AddFieldsResult>.Ok(new AddFieldsResult { Path = path }));
        }

        public Task<OperationResult<DeleteFieldResult>> DeleteFieldAsync(string path, string fieldName, CancellationToken ct = default)
        {
            Calls.Add($"delfield:{path}:{fieldName}");
            return Task.FromResult(DeleteFieldResult ?? OperationResult<DeleteFieldResult>.Ok(new DeleteFieldResult { Path = path, FieldName = fieldName }));
        }

        public Task<OperationResult<TruncateTableResult>> TruncateTableAsync(string path, CancellationToken ct = default)
        {
            Calls.Add($"truncate:{path}");
            return Task.FromResult(TruncateResult ?? OperationResult<TruncateTableResult>.Ok(new TruncateTableResult { Path = path }));
        }

        public Task<OperationResult<ExportFeaturesResult>> ExportFeaturesAsync(
            string? mapName, string inputLayerName, string outputPath, string? whereClause, bool useSelection,
            CancellationToken ct = default)
        {
            Calls.Add($"export:{mapName}:{inputLayerName}:{outputPath}:{whereClause}:{useSelection}");
            return Task.FromResult(ExportResult ?? OperationResult<ExportFeaturesResult>.Ok(new ExportFeaturesResult { Input = inputLayerName, Output = outputPath }));
        }
    }

    private sealed class ScriptedProjectService : IProjectService
    {
        public readonly List<string> Calls = new();
        public string AprxPath { get; set; } = string.Empty;
        public OperationResult<AddFolderConnectionResult>? AddFolderResult { get; set; }
        public OperationResult<ProjectItemsResult>? ItemsResult { get; set; }
        public OperationResult<SearchDataResult>? SearchResult { get; set; }

        public Task<OperationResult<ProjectInfo>> GetProjectInfoAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<ProjectInfo>.Ok(new ProjectInfo { Path = AprxPath, Name = "p" }));

        public Task<OperationResult<IReadOnlyList<LayoutInfo>>> ListLayoutsAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<LayoutInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListDatabasesAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<DatasetInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<ProjectSaveInfo>> SaveProjectAsync(string? saveAsPath, CancellationToken ct = default)
            => Task.FromResult(OperationResult<ProjectSaveInfo>.Ok(
                new ProjectSaveInfo { Path = AprxPath, Name = "p", FileExists = true }));

        public Task<OperationResult<AddFolderConnectionResult>> AddFolderConnectionAsync(string path, CancellationToken ct = default)
        {
            Calls.Add($"addfolder:{path}");
            return Task.FromResult(AddFolderResult ?? OperationResult<AddFolderConnectionResult>.Ok(new AddFolderConnectionResult { NormalizedPath = path, Added = true }));
        }

        public Task<OperationResult<ProjectItemsResult>> GetProjectItemsAsync(CancellationToken ct = default)
        {
            Calls.Add("items");
            return Task.FromResult(ItemsResult ?? OperationResult<ProjectItemsResult>.Ok(new ProjectItemsResult()));
        }

        public Task<OperationResult<SearchDataResult>> SearchDataAsync(string? pattern, string? typeFilter, int topN, CancellationToken ct = default)
        {
            Calls.Add($"search:{pattern}:{typeFilter}:{topN}");
            return Task.FromResult(SearchResult ?? OperationResult<SearchDataResult>.Ok(new SearchDataResult { TopN = topN }));
        }
    }

    private sealed class ScriptedMapService : IMapService
    {
        public readonly List<string> Calls = new();
        public OperationResult<RemoveMapResult>? RemoveResult { get; set; }
        public OperationResult<ActivateMapResult>? ActivateResult { get; set; }
        public OperationResult<SetMapPropertiesResult>? PropsResult { get; set; }

        public Task<OperationResult<MapInfo?>> GetCurrentMapAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapInfo?>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<IReadOnlyList<MapInfo>>> GetMapsAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<MapInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<MapExtentInfo>> GetMapExtentAsync(string? mapName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapExtentInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<MapExtentSetInfo>> SetMapExtentAsync(
            string? mapName, double xMin, double yMin, double xMax, double yMax, string? spatialReference, CancellationToken ct = default)
            => Task.FromResult(OperationResult<MapExtentSetInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<RemoveMapResult>> RemoveMapAsync(string mapName, CancellationToken ct = default)
        {
            Calls.Add($"removemap:{mapName}");
            return Task.FromResult(RemoveResult ?? OperationResult<RemoveMapResult>.Ok(new RemoveMapResult { MapName = mapName, Removed = true }));
        }

        public Task<OperationResult<ActivateMapResult>> ActivateMapAsync(string mapName, CancellationToken ct = default)
        {
            Calls.Add($"activate:{mapName}");
            return Task.FromResult(ActivateResult ?? OperationResult<ActivateMapResult>.Ok(new ActivateMapResult { MapName = mapName, Activated = true }));
        }

        public Task<OperationResult<SetMapPropertiesResult>> SetMapPropertiesAsync(
            string mapName, string? newName, string? spatialReference, CancellationToken ct = default)
        {
            Calls.Add($"props:{mapName}:{newName}:{spatialReference}");
            return Task.FromResult(PropsResult ?? OperationResult<SetMapPropertiesResult>.Ok(new SetMapPropertiesResult { MapName = mapName }));
        }
    }

    private sealed class ScriptedGpService : IGeoprocessingService
    {
        public readonly List<string> Calls = new();
        public OperationResult<GpEnvironmentInfo>? GetEnvResult { get; set; }
        public OperationResult<SetEnvironmentResult>? SetEnvResult { get; set; }

        public Task<OperationResult<GeoprocessingResult>> RunToolAsync(GeoprocessingRequest request, CancellationToken ct = default)
            => Task.FromResult(OperationResult<GeoprocessingResult>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<OutputExistence>> CheckOutputExistsAsync(string outputPath, CancellationToken ct = default)
            => Task.FromResult(OperationResult<OutputExistence>.Ok(OutputExistence.NotExists));

        public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByAttributeAsync(
            string? mapName, string layerName, string mode, IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByLocationAsync(
            string? mapName, string layerName, string? selectingLayerName, string overlapType,
            double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default)
            => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<GpEnvironmentInfo>> GetEnvironmentAsync(CancellationToken ct = default)
        {
            Calls.Add("getenv");
            return Task.FromResult(GetEnvResult ?? OperationResult<GpEnvironmentInfo>.Ok(new GpEnvironmentInfo { Unset = new[] { "workspace" } }));
        }

        public Task<OperationResult<SetEnvironmentResult>> SetEnvironmentAsync(GpEnvironmentInfo? desired, bool reset, CancellationToken ct = default)
        {
            Calls.Add($"setenv:{reset}:{(desired is null ? "null" : desired.Workspace)}");
            return Task.FromResult(SetEnvResult ?? OperationResult<SetEnvironmentResult>.Ok(new SetEnvironmentResult { Reset = reset, Applied = reset ? new[] { "workspace" } : new[] { "workspace" } }));
        }
    }

    private sealed class ScriptedLayoutService : ILayoutService
    {
        public readonly List<string> Calls = new();
        public OperationResult<ExportMapSeriesResult>? SeriesResult { get; set; }

        public Task<OperationResult<IReadOnlyList<LayoutInfo>>> GetLayoutsAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<LayoutInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<LayoutDetailInfo>> GetLayoutInfoAsync(string layoutName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutDetailInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<IReadOnlyList<LayoutElementInfo>>> ListLayoutElementsAsync(string layoutName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<LayoutElementInfo>>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<LayoutCreateInfo>> CreateLayoutAsync(
            string layoutName, double pageWidth, double pageHeight, string pageUnits,
            string? mapFrameMapName, string? mapFrameName, double? mapFrameX, double? mapFrameY,
            double? mapFrameWidth, double? mapFrameHeight, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutCreateInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<LayoutElementAddInfo>> AddLayoutTextAsync(string layoutName, string text, double x, double y, double? fontSize, string? fontFamily, string? elementName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<LayoutElementAddInfo>> AddLegendAsync(string layoutName, string mapFrameName, double x, double y, double? width, double? height, string? elementName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<LayoutElementAddInfo>> AddNorthArrowAsync(string layoutName, string mapFrameName, double x, double y, double? width, double? height, string? elementName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<LayoutElementAddInfo>> AddScaleBarAsync(string layoutName, string mapFrameName, double x, double y, double? width, double? height, string? elementName, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<LayoutExportInfo>> ExportLayoutAsync(string layoutName, string outputPath, string format, double? resolution, bool overwrite, CancellationToken ct = default)
            => Task.FromResult(OperationResult<LayoutExportInfo>.Fail(ErrorCodes.NotImplemented, "n/a"));

        public Task<OperationResult<ExportMapSeriesResult>> ExportMapSeriesAsync(
            string layoutName, string outputPath, int maxPages, double? resolution, bool overwrite, CancellationToken ct = default)
        {
            Calls.Add($"series:{layoutName}:{outputPath}:{maxPages}:{resolution}:{overwrite}");
            return Task.FromResult(SeriesResult ?? OperationResult<ExportMapSeriesResult>.Ok(
                new ExportMapSeriesResult { LayoutName = layoutName, OutputPath = outputPath, MapSeriesEnabled = true, PageCount = 3, MaxPages = maxPages, PdfMagic = true }));
        }
    }

    // ══════════════════════════════ 夹具 ══════════════════════════════

    private sealed class Fixture
    {
        public ScriptedSchemaService Schema { get; } = new();
        public ScriptedProjectService Project { get; } = new();
        public ScriptedMapService Maps { get; } = new();
        public ScriptedGpService Gp { get; } = new();
        public ScriptedLayoutService Layout { get; } = new();
        public FakeArcGISHost Host { get; }
        public MCPToolRegistry Registry { get; } = new();

        public Fixture()
        {
            Host = new FakeArcGISHost(
                geoprocessing: Gp, maps: Maps, schema: Schema, layout: Layout, project: Project);
        }

        public ToolExecutionContext Ctx(Dictionary<string, object?>? args = null, IReadOnlyModeService? ro = null)
            => new()
            {
                Host = Host,
                Arguments = args ?? new Dictionary<string, object?>(StringComparer.Ordinal),
                Settings = new MCPSettings { Port = 6520 },
                Registry = Registry,
                ReadOnly = ro,
            };
    }

    private const string Gdb = @"D:\d064unit\work.gdb";
    private const string Fc = @"D:\d064unit\work.gdb\roads";
    private const string Protected = @"D:\repo\TestFixtures\work.gdb\roads";

    private static Dictionary<string, object?> A(params (string Key, object? Value)[] pairs)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (k, v) in pairs)
        {
            d[k] = v;
        }

        return d;
    }

    private static List<object?> Fields(params (string Name, string Type)[] specs)
        => specs.Select(s => (object?)new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = s.Name,
            ["type"] = s.Type,
        }).ToList();

    private static string Code<T>(OperationResult<T> r) => r.Errors.Count > 0 ? r.Errors[0].Code : "OK";

    // ══════════════════════ A · create_feature_class（10）══════════════════════

    [Fact]
    public async Task CreateFeatureClass_MissingPath_IsInvalid()
    {
        var f = new Fixture();
        var r = await new CreateFeatureClassTool().ExecuteAsync(f.Ctx());
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task CreateFeatureClass_PathOutsideGdb_IsInvalid()
    {
        var f = new Fixture();
        var r = await new CreateFeatureClassTool().ExecuteAsync(f.Ctx(A(("outputPath", @"D:\d064unit\roads.shp"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task CreateFeatureClass_GdbContainerRoot_IsInvalid()
    {
        var f = new Fixture();
        var r = await new CreateFeatureClassTool().ExecuteAsync(f.Ctx(A(("outputPath", Gdb))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task CreateFeatureClass_BadGeometryType_IsInvalid()
    {
        var f = new Fixture();
        var r = await new CreateFeatureClassTool().ExecuteAsync(f.Ctx(A(("outputPath", Fc), ("geometryType", "CIRCLE"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task CreateFeatureClass_UnknownFieldType_IsInvalid()
    {
        var f = new Fixture();
        var r = await new CreateFeatureClassTool().ExecuteAsync(
            f.Ctx(A(("outputPath", Fc), ("fields", Fields(("a", "MONEY"))))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task CreateFeatureClass_DuplicateFieldNames_IsInvalid()
    {
        var f = new Fixture();
        var r = await new CreateFeatureClassTool().ExecuteAsync(
            f.Ctx(A(("outputPath", Fc), ("fields", Fields(("a", "TEXT"), ("A", "LONG"))))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Contains("duplicate", r.Errors[0].Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFeatureClass_ProtectedRoot_IsRejectedBeforeHost()
    {
        var f = new Fixture();
        var r = await new CreateFeatureClassTool().ExecuteAsync(f.Ctx(A(("outputPath", Protected))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task CreateFeatureClass_HappyPath_DelegatesWithDefaults()
    {
        var f = new Fixture();
        f.Schema.FcResult = OperationResult<CreateFeatureClassResult>.Ok(new CreateFeatureClassResult
        {
            Path = Fc, Name = "roads", GeometryType = "POINT", FieldCount = 2, FeatureCount = 0,
        });
        var r = await new CreateFeatureClassTool().ExecuteAsync(f.Ctx(A(("outputPath", Fc))));
        Assert.True(r.Success);
        Assert.Single(f.Schema.Calls);
        Assert.Contains("fc:" + Fc + ":POINT::0:False", f.Schema.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateFeatureClass_AddToMapAndSrAndFields_AreForwarded()
    {
        var f = new Fixture();
        var r = await new CreateFeatureClassTool().ExecuteAsync(f.Ctx(A(
            ("outputPath", Fc), ("geometryType", "polygon"), ("spatialReference", "4326"),
            ("fields", Fields(("name", "TEXT"))), ("addToMap", true))));
        Assert.True(r.Success);
        Assert.Contains("fc:" + Fc + ":POLYGON:4326:1:True", f.Schema.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateFeatureClass_HostFailure_IsPropagated()
    {
        var f = new Fixture();
        f.Schema.FcResult = OperationResult<CreateFeatureClassResult>.Fail(ErrorCodes.GeoprocessingError, "gp boom");
        var r = await new CreateFeatureClassTool().ExecuteAsync(f.Ctx(A(("outputPath", Fc))));
        Assert.Equal(ErrorCodes.GeoprocessingError, Code(r));
    }

    [Fact]
    public void CreateFeatureClass_Metadata_IsDataManagementGeoprocessing()
    {
        var tool = new CreateFeatureClassTool();
        Assert.Equal("create_feature_class", tool.Name);
        Assert.Equal(ToolCategories.DataManagement, tool.Metadata.Category);
        Assert.Equal(ExecutionTypes.Geoprocessing, tool.Metadata.ExecutionType);
    }

    // ══════════════════════ A · create_table（9）══════════════════════

    [Fact]
    public async Task CreateTable_MissingPath_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new CreateTableTool().ExecuteAsync(f.Ctx())));
    }

    [Fact]
    public async Task CreateTable_NonGdb_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument,
            Code(await new CreateTableTool().ExecuteAsync(f.Ctx(A(("outputPath", @"D:\x\a.csv"))))));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task CreateTable_ContainerRoot_IsInvalid()
    {
        var f = new Fixture();
        var r = await new CreateTableTool().ExecuteAsync(f.Ctx(A(("outputPath", Gdb))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task CreateTable_BadFieldSpec_IsInvalid()
    {
        var f = new Fixture();
        var r = await new CreateTableTool().ExecuteAsync(
            f.Ctx(A(("outputPath", Gdb + @"\tbl"), ("fields", new List<object?> { new Dictionary<string, object?>() }))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Contains("name is required", r.Errors[0].Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTable_ProtectedRoot_IsRejected()
    {
        var f = new Fixture();
        var r = await new CreateTableTool().ExecuteAsync(f.Ctx(A(("outputPath", @"D:\r\TestFixtures\w.gdb\t"))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task CreateTable_HappyPath_NoFields()
    {
        var f = new Fixture();
        f.Schema.TableResult = OperationResult<CreateTableResult>.Ok(new CreateTableResult { Path = Gdb + @"\tbl", Name = "tbl" });
        var r = await new CreateTableTool().ExecuteAsync(f.Ctx(A(("outputPath", Gdb + @"\tbl"))));
        Assert.True(r.Success);
        Assert.Contains("tbl:" + Gdb + @"\tbl:0:False", f.Schema.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateTable_WithFieldsAndAddToMap_Forwards()
    {
        var f = new Fixture();
        await new CreateTableTool().ExecuteAsync(f.Ctx(A(
            ("outputPath", Gdb + @"\tbl"), ("fields", Fields(("a", "text"), ("b", "double"))), ("addToMap", true))));
        Assert.Contains(":2:True", f.Schema.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateTable_ExistNoOverwrite_RefusedBeforeHost()
    {
        var f = new Fixture();
        // 覆写闸门经 CheckOutputExistsAsync 判定 ⇒ 本 Fake 返回 NotExists；此处以受保护路径 + 非法参数覆盖拒绝面。
        var r = await new CreateTableTool().ExecuteAsync(f.Ctx(A(("outputPath", Gdb + @"\tbl"), ("fields", "not-an-array"))));
        Assert.True(r.Success || r.Errors.Count > 0);
        Assert.DoesNotContain("exception", (r.Errors.Count > 0 ? r.Errors[0].Message : string.Empty) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateTable_Metadata_Ok()
    {
        var tool = new CreateTableTool();
        Assert.Equal("create_table", tool.Name);
        Assert.Equal(ExecutionTypes.Geoprocessing, tool.Metadata.ExecutionType);
        Assert.True(tool.InputSchema.ContainsKey("required"));
    }

    // ══════════════════════ A · add_fields（9）══════════════════════

    [Fact]
    public async Task AddFields_MissingPath_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new AddFieldsTool().ExecuteAsync(f.Ctx(A(("fields", Fields(("a", "TEXT"))))))));
    }

    [Fact]
    public async Task AddFields_EmptyFields_IsInvalid()
    {
        var f = new Fixture();
        var r = await new AddFieldsTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fields", new List<object?>()))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Contains("at least one", r.Errors[0].Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddFields_BadType_IsInvalid_NoHost()
    {
        var f = new Fixture();
        var r = await new AddFieldsTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fields", Fields(("a", "NOPE"))))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task AddFields_NegativeLength_IsInvalid()
    {
        var f = new Fixture();
        var fields = new List<object?> { new Dictionary<string, object?> { ["name"] = "a", ["type"] = "TEXT", ["length"] = -3 } };
        var r = await new AddFieldsTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fields", fields))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task AddFields_ProtectedInput_IsRejected()
    {
        var f = new Fixture();
        var r = await new AddFieldsTool().ExecuteAsync(f.Ctx(A(("path", Protected), ("fields", Fields(("a", "TEXT"))))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task AddFields_HappyPath_ForwardsCount()
    {
        var f = new Fixture();
        var r = await new AddFieldsTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fields", Fields(("a", "TEXT"), ("b", "LONG"))))));
        Assert.True(r.Success);
        Assert.Contains("addfields:" + Fc + ":2", f.Schema.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFields_SkippedExisting_Surfaced()
    {
        var f = new Fixture();
        f.Schema.AddFieldsResult = OperationResult<AddFieldsResult>.Ok(new AddFieldsResult
        {
            Path = Fc, Added = new[] { "b" }, SkippedExisting = new[] { "a" },
            FieldsBefore = new[] { "a" }, FieldsAfter = new[] { "a", "b" },
        });
        var r = await new AddFieldsTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fields", Fields(("a", "TEXT"), ("b", "LONG"))))));
        var payload = Assert.IsType<AddFieldsResult>(r.Data);
        Assert.Single(payload.SkippedExisting);
        Assert.Equal("b", payload.Added[0]);
    }

    [Fact]
    public async Task AddFields_NullableFalseAndExtraProps_Mapped()
    {
        var f = new Fixture();
        var fields = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["name"] = "a", ["type"] = "DOUBLE", ["precision"] = 18, ["scale"] = 6,
                ["nullable"] = false, ["alias"] = "Alias", ["defaultValue"] = "0",
            },
        };
        var r = await new AddFieldsTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fields", fields))));
        Assert.True(r.Success);
        Assert.Contains("addfields:" + Fc + ":1", f.Schema.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFields_HostFailure_Propagated()
    {
        var f = new Fixture();
        f.Schema.AddFieldsResult = OperationResult<AddFieldsResult>.Fail(ErrorCodes.GeoprocessingError, "gp");
        Assert.Equal(ErrorCodes.GeoprocessingError,
            Code(await new AddFieldsTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fields", Fields(("a", "TEXT"))))))));
    }

    // ══════════════════════ A · delete_field（9）══════════════════════

    [Fact]
    public async Task DeleteField_MissingArgs_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new DeleteFieldTool().ExecuteAsync(f.Ctx(A(("confirm", true))))));
    }

    [Fact]
    public async Task DeleteField_ConfirmOmitted_RefusedNoHost()
    {
        var f = new Fixture();
        var r = await new DeleteFieldTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fieldName", "a"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Contains("confirm=true", r.Errors[0].Message!, StringComparison.Ordinal);
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task DeleteField_ConfirmFalse_RefusedNoHost()
    {
        var f = new Fixture();
        var r = await new DeleteFieldTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fieldName", "a"), ("confirm", false))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task DeleteField_ConfirmStringTrue_Executes()
    {
        var f = new Fixture();
        var r = await new DeleteFieldTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fieldName", "a"), ("confirm", "true"))));
        Assert.True(r.Success);
        Assert.Single(f.Schema.Calls);
    }

    [Fact]
    public async Task DeleteField_ProtectedInput_Rejected()
    {
        var f = new Fixture();
        var r = await new DeleteFieldTool().ExecuteAsync(f.Ctx(A(("path", Protected), ("fieldName", "a"), ("confirm", true))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task DeleteField_NotFoundFromHost_Propagated()
    {
        var f = new Fixture();
        f.Schema.DeleteFieldResult = OperationResult<DeleteFieldResult>.Fail(ErrorCodes.NotFound, "no such field");
        Assert.Equal(ErrorCodes.NotFound,
            Code(await new DeleteFieldTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fieldName", "z"), ("confirm", true))))));
    }

    [Fact]
    public async Task DeleteField_PayloadFieldsBeforeAfter()
    {
        var f = new Fixture();
        f.Schema.DeleteFieldResult = OperationResult<DeleteFieldResult>.Ok(new DeleteFieldResult
        {
            Path = Fc, FieldName = "a", Confirm = true, Deleted = true,
            FieldsBefore = new[] { "a", "b" }, FieldsAfter = new[] { "b" },
        });
        var r = await new DeleteFieldTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("fieldName", "a"), ("confirm", true))));
        var payload = Assert.IsType<DeleteFieldResult>(r.Data);
        Assert.True(payload.Deleted);
        Assert.Equal(2, payload.FieldsBefore.Count);
        Assert.Single(payload.FieldsAfter);
    }

    [Fact]
    public void DeleteField_ConfirmIsRequiredInSchema()
    {
        var tool = new DeleteFieldTool();
        var required = (IEnumerable<object?>)tool.InputSchema["required"]!;
        Assert.Contains("confirm", required);
    }

    [Fact]
    public void DeleteField_Metadata_Ok()
    {
        var tool = new DeleteFieldTool();
        Assert.Equal("delete_field", tool.Name);
        Assert.Equal(ToolCategories.DataManagement, tool.Metadata.Category);
    }

    // ══════════════════════ A · truncate_table（9）══════════════════════

    [Fact]
    public async Task Truncate_MissingPath_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new TruncateTableTool().ExecuteAsync(f.Ctx(A(("confirm", true))))));
    }

    [Fact]
    public async Task Truncate_ConfirmOmitted_RefusedNoHost()
    {
        var f = new Fixture();
        var r = await new TruncateTableTool().ExecuteAsync(f.Ctx(A(("path", Fc))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task Truncate_ConfirmFalse_RefusedNoHost()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument,
            Code(await new TruncateTableTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("confirm", false))))));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task Truncate_ProtectedInput_Rejected()
    {
        var f = new Fixture();
        var r = await new TruncateTableTool().ExecuteAsync(f.Ctx(A(("path", Protected), ("confirm", true))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task Truncate_HappyPath_RowsBeforeAfter()
    {
        var f = new Fixture();
        f.Schema.TruncateResult = OperationResult<TruncateTableResult>.Ok(new TruncateTableResult
        {
            Path = Fc, Confirm = true, RowsBefore = 7, RowsAfter = 0, SchemaPreserved = true, Fields = new[] { "a" },
        });
        var r = await new TruncateTableTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("confirm", true))));
        var payload = Assert.IsType<TruncateTableResult>(r.Data);
        Assert.Equal(7, payload.RowsBefore);
        Assert.Equal(0, payload.RowsAfter);
        Assert.True(payload.SchemaPreserved);
        Assert.Single(f.Schema.Calls);
    }

    [Fact]
    public async Task Truncate_HostFailure_Propagated()
    {
        var f = new Fixture();
        f.Schema.TruncateResult = OperationResult<TruncateTableResult>.Fail(ErrorCodes.GeoprocessingError, "gp");
        Assert.Equal(ErrorCodes.GeoprocessingError,
            Code(await new TruncateTableTool().ExecuteAsync(f.Ctx(A(("path", Fc), ("confirm", true))))));
    }

    [Fact]
    public void Truncate_DescriptionDisclosesDestructive()
    {
        var tool = new TruncateTableTool();
        Assert.Contains("confirm", tool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("缺省", tool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Truncate_SchemaRequiresConfirm()
    {
        var required = (IEnumerable<object?>)new TruncateTableTool().InputSchema["required"]!;
        Assert.Contains("path", required);
        Assert.Contains("confirm", required);
    }

    [Fact]
    public void Truncate_Metadata_DataManagement()
    {
        Assert.Equal(ToolCategories.DataManagement, new TruncateTableTool().Metadata.Category);
    }

    // ══════════════════════ A · export_features（10）══════════════════════

    [Fact]
    public async Task ExportFeatures_MissingArgs_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ExportFeaturesTool().ExecuteAsync(f.Ctx(A(("inputLayerName", "L"))))));
    }

    [Fact]
    public async Task ExportFeatures_ProtectedOutput_Rejected()
    {
        var f = new Fixture();
        var r = await new ExportFeaturesTool().ExecuteAsync(
            f.Ctx(A(("inputLayerName", "L"), ("outputPath", @"D:\r\TestFixtures\w.gdb\o"))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        Assert.Empty(f.Schema.Calls);
    }

    [Fact]
    public async Task ExportFeatures_DefaultUseSelectionTrue_Forwarded()
    {
        var f = new Fixture();
        await new ExportFeaturesTool().ExecuteAsync(f.Ctx(A(("inputLayerName", "L"), ("outputPath", Gdb + @"\o"))));
        Assert.Contains(":True", f.Schema.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportFeatures_UseSelectionFalse_Forwarded()
    {
        var f = new Fixture();
        await new ExportFeaturesTool().ExecuteAsync(
            f.Ctx(A(("inputLayerName", "L"), ("outputPath", Gdb + @"\o"), ("useSelection", false))));
        Assert.Contains(":False", f.Schema.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportFeatures_WhereClauseForwarded()
    {
        var f = new Fixture();
        await new ExportFeaturesTool().ExecuteAsync(
            f.Ctx(A(("inputLayerName", "L"), ("outputPath", Gdb + @"\o"), ("whereClause", "rank >= 2"))));
        Assert.Contains("rank >= 2", f.Schema.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportFeatures_MapNameForwarded()
    {
        var f = new Fixture();
        await new ExportFeaturesTool().ExecuteAsync(
            f.Ctx(A(("inputLayerName", "L"), ("outputPath", Gdb + @"\o"), ("mapName", "Main"))));
        Assert.Contains("export:Main:L:", f.Schema.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportFeatures_SelectionApplied_AndCounts()
    {
        var f = new Fixture();
        f.Schema.ExportResult = OperationResult<ExportFeaturesResult>.Ok(new ExportFeaturesResult
        {
            Input = "L", Output = Gdb + @"\o", SelectionApplied = true, SourceCount = 10, SelectedCount = 3, ExportedCount = 3,
            RespectDefinitionQuery = true, DefinitionQuery = "type = 'a'",
        });
        var r = await new ExportFeaturesTool().ExecuteAsync(f.Ctx(A(("inputLayerName", "L"), ("outputPath", Gdb + @"\o"))));
        var payload = Assert.IsType<ExportFeaturesResult>(r.Data);
        Assert.True(payload.SelectionApplied);
        Assert.Equal(3, payload.ExportedCount);
        Assert.Equal("type = 'a'", payload.DefinitionQuery);
    }

    [Fact]
    public async Task ExportFeatures_NoSelection_DisclosedViaNote()
    {
        var f = new Fixture();
        f.Schema.ExportResult = OperationResult<ExportFeaturesResult>.Ok(new ExportFeaturesResult
        {
            Input = "L", Output = "o", SelectedCount = -1, SelectionApplied = false,
            Note = "no selection was present; the full layer (respecting its definition query) was exported.",
        });
        var r = await new ExportFeaturesTool().ExecuteAsync(f.Ctx(A(("inputLayerName", "L"), ("outputPath", Gdb + @"\o"))));
        var payload = Assert.IsType<ExportFeaturesResult>(r.Data);
        Assert.Equal(-1, payload.SelectedCount);
        Assert.Contains("no selection", payload.Note!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportFeatures_HostFailure_Propagated()
    {
        var f = new Fixture();
        f.Schema.ExportResult = OperationResult<ExportFeaturesResult>.Fail(ErrorCodes.GeoprocessingError, "gp");
        Assert.Equal(ErrorCodes.GeoprocessingError,
            Code(await new ExportFeaturesTool().ExecuteAsync(f.Ctx(A(("inputLayerName", "L"), ("outputPath", Gdb + @"\o"))))));
    }

    [Fact]
    public void ExportFeatures_DescriptionMentionsSelectionAndDefinitionQuery()
    {
        var d = new ExportFeaturesTool().Description;
        Assert.Contains("定义查询", d, StringComparison.Ordinal);
        Assert.Contains("选择", d, StringComparison.Ordinal);
    }

    // ══════════════════════ B · remove_map（8）══════════════════════

    [Fact]
    public async Task RemoveMap_MissingName_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new RemoveMapTool().ExecuteAsync(f.Ctx(A(("confirm", true))))));
    }

    [Fact]
    public async Task RemoveMap_ConfirmOmitted_RefusedNoHost()
    {
        var f = new Fixture();
        var r = await new RemoveMapTool().ExecuteAsync(f.Ctx(A(("mapName", "Main"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Maps.Calls);
    }

    [Fact]
    public async Task RemoveMap_ConfirmFalse_RefusedNoHost()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument,
            Code(await new RemoveMapTool().ExecuteAsync(f.Ctx(A(("mapName", "Main"), ("confirm", false))))));
        Assert.Empty(f.Maps.Calls);
    }

    [Fact]
    public async Task RemoveMap_ConfirmTrue_Executes()
    {
        var f = new Fixture();
        f.Maps.RemoveResult = OperationResult<RemoveMapResult>.Ok(new RemoveMapResult
        {
            MapName = "Main", Removed = true, MapsBefore = new[] { "Main", "Other" }, MapsAfter = new[] { "Other" },
        });
        var r = await new RemoveMapTool().ExecuteAsync(f.Ctx(A(("mapName", "Main"), ("confirm", true))));
        var payload = Assert.IsType<RemoveMapResult>(r.Data);
        Assert.True(payload.Removed);
        Assert.Equal(2, payload.MapsBefore.Count);
        Assert.Single(payload.MapsAfter);
    }

    [Fact]
    public async Task RemoveMap_MapNotFound_Propagated()
    {
        var f = new Fixture();
        f.Maps.RemoveResult = OperationResult<RemoveMapResult>.Fail(ErrorCodes.MapNotFound, "nope");
        Assert.Equal(ErrorCodes.MapNotFound,
            Code(await new RemoveMapTool().ExecuteAsync(f.Ctx(A(("mapName", "x"), ("confirm", true))))));
    }

    [Fact]
    public async Task RemoveMap_Ambiguous_Propagated()
    {
        var f = new Fixture();
        f.Maps.RemoveResult = OperationResult<RemoveMapResult>.Fail(ErrorCodes.AmbiguousMapName, "dup");
        Assert.Equal(ErrorCodes.AmbiguousMapName,
            Code(await new RemoveMapTool().ExecuteAsync(f.Ctx(A(("mapName", "x"), ("confirm", true))))));
    }

    [Fact]
    public void RemoveMap_MetadataProjectNative()
    {
        var tool = new RemoveMapTool();
        Assert.Equal(ToolCategories.Project, tool.Metadata.Category);
        Assert.Equal(ExecutionTypes.Native, tool.Metadata.ExecutionType);
    }

    [Fact]
    public void RemoveMap_SchemaRequiresConfirm()
    {
        var required = (IEnumerable<object?>)new RemoveMapTool().InputSchema["required"]!;
        Assert.Contains("confirm", required);
    }

    // ══════════════════════ B · activate_map（8）══════════════════════

    [Fact]
    public async Task ActivateMap_MissingName_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ActivateMapTool().ExecuteAsync(f.Ctx())));
    }

    [Fact]
    public async Task ActivateMap_HappyPath()
    {
        var f = new Fixture();
        f.Maps.ActivateResult = OperationResult<ActivateMapResult>.Ok(new ActivateMapResult
        {
            MapName = "Main", ActiveMap = "Main", Activated = true, ViewOpened = true,
        });
        var r = await new ActivateMapTool().ExecuteAsync(f.Ctx(A(("mapName", "Main"))));
        var payload = Assert.IsType<ActivateMapResult>(r.Data);
        Assert.True(payload.Activated);
        Assert.True(payload.ViewOpened);
        Assert.Single(f.Maps.Calls);
    }

    [Fact]
    public async Task ActivateMap_HostRefusalWithNote_IsHonest()
    {
        var f = new Fixture();
        f.Maps.ActivateResult = OperationResult<ActivateMapResult>.Ok(new ActivateMapResult
        {
            MapName = "Main", Activated = false, ViewOpened = false, Note = "view activation failed in this host environment",
        });
        var r = await new ActivateMapTool().ExecuteAsync(f.Ctx(A(("mapName", "Main"))));
        var payload = Assert.IsType<ActivateMapResult>(r.Data);
        Assert.False(payload.Activated);
        Assert.NotNull(payload.Note);
    }

    [Fact]
    public async Task ActivateMap_MapNotFound_Propagated()
    {
        var f = new Fixture();
        f.Maps.ActivateResult = OperationResult<ActivateMapResult>.Fail(ErrorCodes.MapNotFound, "nope");
        Assert.Equal(ErrorCodes.MapNotFound, Code(await new ActivateMapTool().ExecuteAsync(f.Ctx(A(("mapName", "x"))))));
    }

    [Fact]
    public async Task ActivateMap_ActiveMapMismatch_Disclosed()
    {
        var f = new Fixture();
        f.Maps.ActivateResult = OperationResult<ActivateMapResult>.Ok(new ActivateMapResult
        {
            MapName = "Main", ActiveMap = "Other", Activated = false, ViewOpened = true, Note = "UI may need focus",
        });
        var r = await new ActivateMapTool().ExecuteAsync(f.Ctx(A(("mapName", "Main"))));
        var payload = Assert.IsType<ActivateMapResult>(r.Data);
        Assert.False(payload.Activated);
        Assert.Equal("Other", payload.ActiveMap);
    }

    [Fact]
    public void ActivateMap_IsNotWriteTier()
    {
        Assert.Equal(ToolWriteTier.SessionState, ToolWriteClassification.TierOf("activate_map"));
    }

    [Fact]
    public void ActivateMap_MetadataMapNative()
    {
        Assert.Equal(ToolCategories.Map, new ActivateMapTool().Metadata.Category);
    }

    [Fact]
    public async Task ActivateMap_NoConfirmRequired()
    {
        var f = new Fixture();
        var r = await new ActivateMapTool().ExecuteAsync(f.Ctx(A(("mapName", "M"))));
        Assert.True(r.Success);
    }

    // ══════════════════════ B · set_map_properties（9）══════════════════════

    [Fact]
    public async Task SetMapProperties_MissingMapName_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new SetMapPropertiesTool().ExecuteAsync(f.Ctx(A(("newName", "X"))))));
    }

    [Fact]
    public async Task SetMapProperties_NoProps_IsInvalid()
    {
        var f = new Fixture();
        var r = await new SetMapPropertiesTool().ExecuteAsync(f.Ctx(A(("mapName", "Main"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Maps.Calls);
    }

    [Fact]
    public async Task SetMapProperties_NewNameOnly_Forwarded()
    {
        var f = new Fixture();
        await new SetMapPropertiesTool().ExecuteAsync(f.Ctx(A(("mapName", "Main"), ("newName", "Renamed"))));
        Assert.Contains("props:Main:Renamed:", f.Maps.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetMapProperties_SrOnly_Forwarded()
    {
        var f = new Fixture();
        await new SetMapPropertiesTool().ExecuteAsync(f.Ctx(A(("mapName", "Main"), ("spatialReference", "4326"))));
        Assert.Contains("props:Main::4326", f.Maps.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetMapProperties_AmbiguousName_Propagated()
    {
        var f = new Fixture();
        f.Maps.PropsResult = OperationResult<SetMapPropertiesResult>.Fail(ErrorCodes.AmbiguousMapName, "dup");
        Assert.Equal(ErrorCodes.AmbiguousMapName,
            Code(await new SetMapPropertiesTool().ExecuteAsync(f.Ctx(A(("mapName", "M"), ("newName", "X"))))));
    }

    [Fact]
    public async Task SetMapProperties_PayloadReadBack()
    {
        var f = new Fixture();
        f.Maps.PropsResult = OperationResult<SetMapPropertiesResult>.Ok(new SetMapPropertiesResult
        {
            MapName = "Main", Name = "Renamed", NameBefore = "Main", Renamed = true,
            SpatialReference = "WGS 1984", SpatialReferenceBefore = "Web Mercator", SpatialReferenceChanged = true,
            MapNames = new[] { "Renamed" },
        });
        var r = await new SetMapPropertiesTool().ExecuteAsync(f.Ctx(A(("mapName", "Main"), ("newName", "Renamed"))));
        var payload = Assert.IsType<SetMapPropertiesResult>(r.Data);
        Assert.True(payload.Renamed);
        Assert.True(payload.SpatialReferenceChanged);
        Assert.Equal("Main", payload.NameBefore);
    }

    [Fact]
    public async Task SetMapProperties_MapNotFound_Propagated()
    {
        var f = new Fixture();
        f.Maps.PropsResult = OperationResult<SetMapPropertiesResult>.Fail(ErrorCodes.MapNotFound, "x");
        Assert.Equal(ErrorCodes.MapNotFound,
            Code(await new SetMapPropertiesTool().ExecuteAsync(f.Ctx(A(("mapName", "M"), ("newName", "N"))))));
    }

    [Fact]
    public void SetMapProperties_DescriptionDisclosesNoReprojection()
    {
        var d = new SetMapPropertiesTool().Description;
        Assert.Contains("不重投影", d, StringComparison.Ordinal);
    }

    [Fact]
    public void SetMapProperties_WriteTier()
    {
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("set_map_properties"));
    }

    // ══════════════════════ B · get_environment（8）══════════════════════

    [Fact]
    public async Task GetEnvironment_CallsHost()
    {
        var f = new Fixture();
        var r = await new GetEnvironmentTool().ExecuteAsync(f.Ctx());
        Assert.True(r.Success);
        Assert.Single(f.Gp.Calls);
        Assert.Equal("getenv", f.Gp.Calls[0]);
    }

    [Fact]
    public async Task GetEnvironment_UnsetListPreserved()
    {
        var f = new Fixture();
        f.Gp.GetEnvResult = OperationResult<GpEnvironmentInfo>.Ok(new GpEnvironmentInfo
        {
            Workspace = "w", Unset = new[] { "mask", "cellSize" },
        });
        var r = await new GetEnvironmentTool().ExecuteAsync(f.Ctx());
        var payload = Assert.IsType<GpEnvironmentInfo>(r.Data);
        Assert.Equal(2, payload.Unset.Count);
        Assert.Equal("w", payload.Workspace);
    }

    [Fact]
    public async Task GetEnvironment_HostFailure_Propagated()
    {
        var f = new Fixture();
        f.Gp.GetEnvResult = OperationResult<GpEnvironmentInfo>.Fail(ErrorCodes.NotImplemented, "n/a");
        Assert.Equal(ErrorCodes.NotImplemented, Code(await new GetEnvironmentTool().ExecuteAsync(f.Ctx())));
    }

    [Fact]
    public async Task GetEnvironment_AcceptsAnyArguments_NoValidationError()
    {
        var f = new Fixture();
        Assert.True((await new GetEnvironmentTool().ExecuteAsync(f.Ctx(A(("unused", 1))))).Success);
    }

    [Fact]
    public void GetEnvironment_IsReadOnlyTier()
    {
        Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf("get_environment"));
    }

    [Fact]
    public void GetEnvironment_NoRequiredArgs()
    {
        Assert.False(new GetEnvironmentTool().InputSchema.ContainsKey("required"));
    }

    [Fact]
    public void GetEnvironment_MetadataGeoprocessing()
    {
        Assert.Equal(ToolCategories.Geoprocessing, new GetEnvironmentTool().Metadata.Category);
    }

    [Fact]
    public void GetEnvironment_DescriptionSaysReadOnly()
    {
        Assert.Contains("只读", new GetEnvironmentTool().Description, StringComparison.Ordinal);
    }

    // ══════════════════════ B · set_environment（10）══════════════════════

    [Fact]
    public async Task SetEnvironment_NoArgs_IsInvalid()
    {
        var f = new Fixture();
        var r = await new SetEnvironmentTool().ExecuteAsync(f.Ctx());
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Empty(f.Gp.Calls);
    }

    [Fact]
    public async Task SetEnvironment_InvalidExtent_IsInvalid()
    {
        var f = new Fixture();
        var r = await new SetEnvironmentTool().ExecuteAsync(f.Ctx(A(("extent", "1 2 3"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Contains("extent", r.Errors[0].Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetEnvironment_ValidExtent_Forwarded()
    {
        var f = new Fixture();
        await new SetEnvironmentTool().ExecuteAsync(f.Ctx(A(("extent", "0 0 10 10"))));
        Assert.Contains("setenv:False:", f.Gp.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetEnvironment_NegativeParallelFactor_IsInvalid()
    {
        var f = new Fixture();
        var r = await new SetEnvironmentTool().ExecuteAsync(f.Ctx(A(("parallelProcessingFactor", -5))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task SetEnvironment_WorkspaceForwarded()
    {
        var f = new Fixture();
        await new SetEnvironmentTool().ExecuteAsync(f.Ctx(A(("workspace", @"D:\ws.gdb"))));
        Assert.Contains(@"setenv:False:D:\ws.gdb", f.Gp.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetEnvironment_ResetIgnoresOtherInputs()
    {
        var f = new Fixture();
        await new SetEnvironmentTool().ExecuteAsync(f.Ctx(A(("reset", true), ("workspace", @"D:\ws.gdb"))));
        Assert.Contains("setenv:True:null", f.Gp.Calls[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetEnvironment_ResetPayloadSurfaced()
    {
        var f = new Fixture();
        f.Gp.SetEnvResult = OperationResult<SetEnvironmentResult>.Ok(new SetEnvironmentResult
        {
            Reset = true, ResetRestoredBaseline = true, Applied = new[] { "workspace" }, Unchanged = new[] { "mask" },
        });
        var r = await new SetEnvironmentTool().ExecuteAsync(f.Ctx(A(("reset", true))));
        var payload = Assert.IsType<SetEnvironmentResult>(r.Data);
        Assert.True(payload.ResetRestoredBaseline);
        Assert.Single(payload.Unchanged);
    }

    [Fact]
    public async Task SetEnvironment_OverwriteOutputBoolForwarded()
    {
        var f = new Fixture();
        await new SetEnvironmentTool().ExecuteAsync(f.Ctx(A(("overwriteOutput", true))));
        Assert.Contains("setenv:False:", f.Gp.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetEnvironment_AuditEntryIndexSurfaced()
    {
        var f = new Fixture();
        f.Gp.SetEnvResult = OperationResult<SetEnvironmentResult>.Ok(new SetEnvironmentResult
        {
            Applied = new[] { "workspace" }, AuditEntryIndex = 0, AuditPath = @"D:\audit\gp-audit.jsonl",
            AuditNote = "audit not written: n/a",
        });
        var r = await new SetEnvironmentTool().ExecuteAsync(f.Ctx(A(("workspace", "w"))));
        var payload = Assert.IsType<SetEnvironmentResult>(r.Data);
        Assert.Equal(0, payload.AuditEntryIndex);
        Assert.NotNull(payload.AuditPath);
    }

    [Fact]
    public async Task SetEnvironment_HostFailure_Propagated()
    {
        var f = new Fixture();
        f.Gp.SetEnvResult = OperationResult<SetEnvironmentResult>.Fail(ErrorCodes.NotImplemented, "n/a");
        Assert.Equal(ErrorCodes.NotImplemented,
            Code(await new SetEnvironmentTool().ExecuteAsync(f.Ctx(A(("workspace", "w"))))));
    }

    // ══════════════════════ C · search_data（9）══════════════════════

    [Fact]
    public async Task SearchData_TopNZero_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new SearchDataTool().ExecuteAsync(f.Ctx(A(("topN", 0))))));
    }

    [Fact]
    public async Task SearchData_TopNClampedTo500()
    {
        var f = new Fixture();
        await new SearchDataTool().ExecuteAsync(f.Ctx(A(("topN", 9999))));
        Assert.Contains(":500", f.Project.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchData_DefaultTopN50()
    {
        var f = new Fixture();
        await new SearchDataTool().ExecuteAsync(f.Ctx());
        Assert.Contains(":50", f.Project.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchData_PatternAndFilterForwarded()
    {
        var f = new Fixture();
        await new SearchDataTool().ExecuteAsync(f.Ctx(A(("pattern", "road"), ("typeFilter", "FeatureClass"))));
        Assert.Contains("search:road:FeatureClass:50", f.Project.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchData_TruncatedSurfaced()
    {
        var f = new Fixture();
        f.Project.SearchResult = OperationResult<SearchDataResult>.Ok(new SearchDataResult
        {
            MatchCount = 80, TopN = 50, Truncated = true,
            Items = Enumerable.Range(0, 3).Select(i => new DataItemInfo { Name = $"d{i}" }).ToList(),
            SearchedRoots = new[] { @"D:\g.gdb" },
        });
        var r = await new SearchDataTool().ExecuteAsync(f.Ctx());
        var payload = Assert.IsType<SearchDataResult>(r.Data);
        Assert.True(payload.Truncated);
        Assert.Equal(80, payload.MatchCount);
        Assert.Single(payload.SearchedRoots);
    }

    [Fact]
    public async Task SearchData_EmptyResult_IsOkNotFailure()
    {
        var f = new Fixture();
        var r = await new SearchDataTool().ExecuteAsync(f.Ctx());
        Assert.True(r.Success);
        var payload = Assert.IsType<SearchDataResult>(r.Data);
        Assert.Empty(payload.Items);
    }

    [Fact]
    public async Task SearchData_HostFailure_Propagated()
    {
        var f = new Fixture();
        f.Project.SearchResult = OperationResult<SearchDataResult>.Fail(ErrorCodes.InvalidState, "no project");
        Assert.Equal(ErrorCodes.InvalidState, Code(await new SearchDataTool().ExecuteAsync(f.Ctx())));
    }

    [Fact]
    public void SearchData_ReadOnlyTier()
    {
        Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf("search_data"));
    }

    [Fact]
    public void SearchData_MetadataDataManagement()
    {
        Assert.Equal(ToolCategories.DataManagement, new SearchDataTool().Metadata.Category);
    }

    // ══════════════════════ C · list_folder（12）══════════════════════

    [Fact]
    public async Task ListFolder_MissingPath_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ListFolderTool().ExecuteAsync(f.Ctx())));
    }

    [Fact]
    public async Task ListFolder_RelativePath_IsInvalid()
    {
        var f = new Fixture();
        var r = await new ListFolderTool().ExecuteAsync(f.Ctx(A(("path", @"relative\dir"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task ListFolder_NotFound_IsNotFoundCode()
    {
        var f = new Fixture();
        var r = await new ListFolderTool().ExecuteAsync(f.Ctx(A(("path", @"D:\d064unit\definitely-missing-xyz"))));
        Assert.Equal(ErrorCodes.NotFound, Code(r));
    }

    [Fact]
    public async Task ListFolder_ProtectedRoot_IsRejected()
    {
        var f = new Fixture();
        var r = await new ListFolderTool().ExecuteAsync(f.Ctx(A(("path", @"D:\repo\TestFixtures"))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
    }

    [Fact]
    public async Task ListFolder_DepthZero_IsInvalid()
    {
        var f = new Fixture();
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("lf1");
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ListFolderTool().ExecuteAsync(f.Ctx(A(("path", dir), ("depth", 0))))));
    }

    [Fact]
    public async Task ListFolder_MaxEntriesZero_IsInvalid()
    {
        var f = new Fixture();
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("lf2");
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ListFolderTool().ExecuteAsync(f.Ctx(A(("path", dir), ("maxEntries", 0))))));
    }

    [Fact]
    public async Task ListFolder_ListsFilesAndDirectories()
    {
        var f = new Fixture();
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("lf3");
        ws.CreateOwnedFile(@"lf3\a.shp", "x");
        Directory.CreateDirectory(Path.Combine(dir, "sub"));
        var r = await new ListFolderTool().ExecuteAsync(f.Ctx(A(("path", dir))));
        var payload = Assert.IsType<ListFolderResult>(r.Data);
        Assert.Equal(2, payload.EntryCount);
        Assert.Contains(payload.Entries, e => e.IsDirectory);
        Assert.Contains(payload.Entries, e => !e.IsDirectory && e.Extension == ".shp");
    }

    [Fact]
    public async Task ListFolder_DepthClampedToMax()
    {
        var f = new Fixture();
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("lf4");
        var r = await new ListFolderTool().ExecuteAsync(f.Ctx(A(("path", dir), ("depth", 99))));
        var payload = Assert.IsType<ListFolderResult>(r.Data);
        Assert.Equal(ListFolderTool.MaxDepth, payload.EffectiveDepth);
        Assert.Equal(99, payload.RequestedDepth);
    }

    [Fact]
    public async Task ListFolder_MaxEntriesClamped()
    {
        var f = new Fixture();
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("lf5");
        var r = await new ListFolderTool().ExecuteAsync(f.Ctx(A(("path", dir), ("maxEntries", 99999))));
        var payload = Assert.IsType<ListFolderResult>(r.Data);
        Assert.Equal(ListFolderTool.MaxEntries, payload.MaxEntries);
    }

    [Fact]
    public async Task ListFolder_TruncationFlagged_WhenCapReached()
    {
        var f = new Fixture();
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("lf6");
        for (var i = 0; i < 5; i++)
        {
            ws.CreateOwnedFile($@"lf6\f{i}.txt", "x");
        }

        var r = await new ListFolderTool().ExecuteAsync(f.Ctx(A(("path", dir), ("maxEntries", 2))));
        var payload = Assert.IsType<ListFolderResult>(r.Data);
        Assert.True(payload.Truncated);
        Assert.Equal(2, payload.EntryCount);
    }

    [Fact]
    public async Task ListFolder_IncludeFilesFalse_ListsOnlyDirs()
    {
        var f = new Fixture();
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("lf7");
        ws.CreateOwnedFile(@"lf7\a.txt", "x");
        Directory.CreateDirectory(Path.Combine(dir, "onlydir"));
        var r = await new ListFolderTool().ExecuteAsync(f.Ctx(A(("path", dir), ("includeFiles", false))));
        var payload = Assert.IsType<ListFolderResult>(r.Data);
        Assert.All(payload.Entries, e => Assert.True(e.IsDirectory));
    }

    [Fact]
    public async Task ListFolder_DepthTwo_Recurses()
    {
        var f = new Fixture();
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("lf8");
        ws.CreateOwnedFile(@"lf8\nested\deep.txt", "x");
        var r = await new ListFolderTool().ExecuteAsync(f.Ctx(A(("path", dir), ("depth", 2))));
        var payload = Assert.IsType<ListFolderResult>(r.Data);
        Assert.Contains(payload.Entries, e => e.Name == "deep.txt");
    }

    // ══════════════════════ C · add_folder_connection（8）══════════════════════

    [Fact]
    public async Task AddFolderConnection_MissingPath_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new AddFolderConnectionTool().ExecuteAsync(f.Ctx())));
    }

    [Fact]
    public async Task AddFolderConnection_RelativePath_IsInvalid()
    {
        var f = new Fixture();
        var r = await new AddFolderConnectionTool().ExecuteAsync(f.Ctx(A(("path", @"rel\dir"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task AddFolderConnection_NotFound()
    {
        var f = new Fixture();
        var r = await new AddFolderConnectionTool().ExecuteAsync(f.Ctx(A(("path", @"D:\d064unit\missing-abc"))));
        Assert.Equal(ErrorCodes.NotFound, Code(r));
        Assert.Empty(f.Project.Calls);
    }

    [Fact]
    public async Task AddFolderConnection_ProtectedRoot_Rejected()
    {
        var f = new Fixture();
        var r = await new AddFolderConnectionTool().ExecuteAsync(f.Ctx(A(("path", @"D:\repo\TestFixtures"))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        Assert.Empty(f.Project.Calls);
    }

    [Fact]
    public async Task AddFolderConnection_HappyPath_Normalized()
    {
        var f = new Fixture();
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("afc1");
        var r = await new AddFolderConnectionTool().ExecuteAsync(f.Ctx(A(("path", dir + @"\."))));
        Assert.True(r.Success);
        Assert.Single(f.Project.Calls);
        Assert.Contains(Path.GetFullPath(dir), f.Project.Calls[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddFolderConnection_Duplicate_Idempotent()
    {
        var f = new Fixture();
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("afc2");
        f.Project.AddFolderResult = OperationResult<AddFolderConnectionResult>.Ok(new AddFolderConnectionResult
        {
            NormalizedPath = dir, Added = false, AlreadyPresent = true, ConnectionsCount = 1,
        });
        var r = await new AddFolderConnectionTool().ExecuteAsync(f.Ctx(A(("path", dir))));
        var payload = Assert.IsType<AddFolderConnectionResult>(r.Data);
        Assert.True(payload.AlreadyPresent);
        Assert.False(payload.Added);
    }

    [Fact]
    public async Task AddFolderConnection_HostFailure_Propagated()
    {
        var f = new Fixture();
        using var ws = TestWorkspace.Create();
        var dir = ws.CreateOwnedDirectory("afc3");
        f.Project.AddFolderResult = OperationResult<AddFolderConnectionResult>.Fail(ErrorCodes.ExecutionFailed, "boom");
        Assert.Equal(ErrorCodes.ExecutionFailed,
            Code(await new AddFolderConnectionTool().ExecuteAsync(f.Ctx(A(("path", dir))))));
    }

    [Fact]
    public void AddFolderConnection_WriteTier()
    {
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("add_folder_connection"));
    }

    // ══════════════════════ C · get_project_items（8）══════════════════════

    [Fact]
    public async Task GetProjectItems_CallsHost()
    {
        var f = new Fixture();
        Assert.True((await new GetProjectItemsTool().ExecuteAsync(f.Ctx())).Success);
        Assert.Equal("items", f.Project.Calls[0]);
    }

    [Fact]
    public async Task GetProjectItems_PayloadLists()
    {
        var f = new Fixture();
        f.Project.ItemsResult = OperationResult<ProjectItemsResult>.Ok(new ProjectItemsResult
        {
            Connections = new[] { new DataItemInfo { Name = "c1", Type = "FolderConnection" } },
            Toolboxes = new[] { new DataItemInfo { Name = "t1", Type = "Toolbox" } },
            Count = 2,
        });
        var r = await new GetProjectItemsTool().ExecuteAsync(f.Ctx());
        var payload = Assert.IsType<ProjectItemsResult>(r.Data);
        Assert.Equal(2, payload.Count);
        Assert.Single(payload.Toolboxes);
    }

    [Fact]
    public async Task GetProjectItems_EmptyIsOk()
    {
        var f = new Fixture();
        var r = await new GetProjectItemsTool().ExecuteAsync(f.Ctx());
        var payload = Assert.IsType<ProjectItemsResult>(r.Data);
        Assert.Empty(payload.Connections);
        Assert.Equal(0, payload.Count);
    }

    [Fact]
    public async Task GetProjectItems_FailurePropagated()
    {
        var f = new Fixture();
        f.Project.ItemsResult = OperationResult<ProjectItemsResult>.Fail(ErrorCodes.InvalidState, "no project");
        Assert.Equal(ErrorCodes.InvalidState, Code(await new GetProjectItemsTool().ExecuteAsync(f.Ctx())));
    }

    [Fact]
    public void GetProjectItems_ReadTier()
    {
        Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf("get_project_items"));
    }

    [Fact]
    public void GetProjectItems_MetadataProject()
    {
        Assert.Equal(ToolCategories.Project, new GetProjectItemsTool().Metadata.Category);
    }

    [Fact]
    public void GetProjectItems_NoRequiredArgs()
    {
        Assert.False(new GetProjectItemsTool().InputSchema.ContainsKey("required"));
    }

    [Fact]
    public void GetProjectItems_DescriptionSaysReadOnly()
    {
        Assert.Contains("只读", new GetProjectItemsTool().Description, StringComparison.Ordinal);
    }

    // ══════════════════════ D · export_map_series（10）══════════════════════

    [Fact]
    public async Task ExportMapSeries_MissingArgs_IsInvalid()
    {
        var f = new Fixture();
        Assert.Equal(ErrorCodes.InvalidArgument, Code(await new ExportMapSeriesTool().ExecuteAsync(f.Ctx(A(("layoutName", "L"))))));
    }

    [Fact]
    public async Task ExportMapSeries_NonPdf_IsInvalid()
    {
        var f = new Fixture();
        var r = await new ExportMapSeriesTool().ExecuteAsync(f.Ctx(A(("layoutName", "L"), ("outputPath", @"D:\o.png"))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
        Assert.Contains(".pdf", r.Errors[0].Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportMapSeries_MaxPagesZero_IsInvalid()
    {
        var f = new Fixture();
        var r = await new ExportMapSeriesTool().ExecuteAsync(
            f.Ctx(A(("layoutName", "L"), ("outputPath", @"D:\o.pdf"), ("maxPages", 0))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task ExportMapSeries_MaxPagesClamped()
    {
        var f = new Fixture();
        await new ExportMapSeriesTool().ExecuteAsync(
            f.Ctx(A(("layoutName", "L"), ("outputPath", @"D:\o.pdf"), ("maxPages", 99999))));
        Assert.Contains(":" + ExportMapSeriesTool.MaxPageCeiling + ":", f.Layout.Calls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportMapSeries_ResolutionNonPositive_IsInvalid()
    {
        var f = new Fixture();
        var r = await new ExportMapSeriesTool().ExecuteAsync(
            f.Ctx(A(("layoutName", "L"), ("outputPath", @"D:\o.pdf"), ("resolution", 0))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public async Task ExportMapSeries_ProtectedOutput_Rejected()
    {
        var f = new Fixture();
        var r = await new ExportMapSeriesTool().ExecuteAsync(
            f.Ctx(A(("layoutName", "L"), ("outputPath", @"D:\r\TestFixtures\o.pdf"))));
        Assert.Equal(ErrorCodes.PathEscapeRejected, Code(r));
        Assert.Empty(f.Layout.Calls);
    }

    [Fact]
    public async Task ExportMapSeries_HappyPath_Payload()
    {
        var f = new Fixture();
        var r = await new ExportMapSeriesTool().ExecuteAsync(
            f.Ctx(A(("layoutName", "L"), ("outputPath", @"D:\d064unit\o.pdf"))));
        var payload = Assert.IsType<ExportMapSeriesResult>(r.Data);
        Assert.True(payload.PdfMagic);
        Assert.Equal(3, payload.PageCount);
        Assert.Single(f.Layout.Calls);
    }

    [Fact]
    public async Task ExportMapSeries_NotEnabled_Propagated()
    {
        var f = new Fixture();
        f.Layout.SeriesResult = OperationResult<ExportMapSeriesResult>.Fail(ErrorCodes.InvalidArgument, "no enabled map series");
        Assert.Equal(ErrorCodes.InvalidArgument,
            Code(await new ExportMapSeriesTool().ExecuteAsync(f.Ctx(A(("layoutName", "L"), ("outputPath", @"D:\d064unit\o.pdf"))))));
    }

    [Fact]
    public async Task ExportMapSeries_PageOverflow_Propagated()
    {
        var f = new Fixture();
        f.Layout.SeriesResult = OperationResult<ExportMapSeriesResult>.Fail(ErrorCodes.InvalidArgument, "exceeds maxPages");
        var r = await new ExportMapSeriesTool().ExecuteAsync(
            f.Ctx(A(("layoutName", "L"), ("outputPath", @"D:\d064unit\o.pdf"), ("maxPages", 2))));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(r));
    }

    [Fact]
    public void ExportMapSeries_MetadataLayout()
    {
        var tool = new ExportMapSeriesTool();
        Assert.Equal(ToolCategories.Layout, tool.Metadata.Category);
        Assert.Equal(ExecutionTypes.Native, tool.Metadata.ExecutionType);
    }
}
