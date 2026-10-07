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
/// D-066：add_join / remove_join 受控 GP 代理（白名单 51→53，Keeper 批准）行为测试。
/// 与 AggregateStatisticsToolTests 同法：recorder 观察 Tool → Router → IGeoprocessingService /
/// ILayerService / ISchemaService 边界，不触真实 Pro GP。
/// ★ 参数与真名断言来自 D-066 spike 实测（standalone arcpy 3.5，spike/spike-result.json）：
///   management.AddJoin(in_layer_or_view, in_field, join_table, join_field, join_type KEEP_ALL|KEEP_COMMON)；
///   management.RemoveJoin(in_layer_or_view[, join_name])；GP 就地修改输入图层（连接字段以 表名.字段名 现身）。
/// 反证锚点 = confirm 缺省拒（GP 服务 destructive 闸门）＋ 白名单准入（未入白名单 → INVALID_ARGUMENT，零副作用）。
/// </summary>
public sealed class JoinProxyToolTests
{
    private const string JoinTablePath = @"D:\D061Live\d066-test\look.gdb\look";
    private const string LayerUri = @"D:\D061Live\d066-test\look.gdb\base";

    // ════════════════ add_join ════════════════

    [Fact]
    public async Task AddJoin_MissingLayerName_IsInvalidArgumentWithoutGp()
    {
        var gp = new RecordingGeoprocessingService();
        var result = await CallJoinAsync(gp, NewHost(gp), new Dictionary<string, object?> { ["joinTable"] = JoinTablePath, ["joinField"] = "bid" });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Null(gp.LastRunRequest);
    }

    [Fact]
    public async Task AddJoin_MissingJoinTable_IsInvalidArgumentWithoutGp()
    {
        var gp = new RecordingGeoprocessingService();
        var result = await CallJoinAsync(gp, NewHost(gp), new Dictionary<string, object?> { ["layerName"] = "Roads", ["joinField"] = "bid" });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Null(gp.LastRunRequest);
    }

    [Fact]
    public async Task AddJoin_MissingJoinField_IsInvalidArgumentWithoutGp()
    {
        var gp = new RecordingGeoprocessingService();
        var result = await CallJoinAsync(gp, NewHost(gp), new Dictionary<string, object?> { ["layerName"] = "Roads", ["joinTable"] = JoinTablePath });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Null(gp.LastRunRequest);
    }

    [Fact]
    public async Task AddJoin_LayerNotFound_IsNotFoundWithoutGp()
    {
        var gp = new RecordingGeoprocessingService();
        var result = await CallJoinAsync(gp, NewHost(gp), new Dictionary<string, object?>
        {
            ["layerName"] = "Nope", ["joinTable"] = JoinTablePath, ["joinField"] = "bid", ["confirm"] = true
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.LayerNotFound, Assert.Single(result.Errors).Code);
        Assert.Null(gp.LastRunRequest);
    }

    [Fact]
    public async Task AddJoin_JoinTableNotFound_IsNotFoundWithoutGp()
    {
        var host = NewHost(new RecordingGeoprocessingService());
        host.SchemaHandler = path => OperationResult<SchemaInfo>.Ok(new SchemaInfo { Path = path, Exists = false, Reason = "not found (test)" });
        var gp = (RecordingGeoprocessingService)host.Geoprocessing;
        var result = await CallJoinAsync(gp, host, new Dictionary<string, object?>
        {
            ["layerName"] = "Roads", ["joinTable"] = JoinTablePath, ["joinField"] = "bid", ["confirm"] = true
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.NotFound, Assert.Single(result.Errors).Code);
        Assert.Null(gp.LastRunRequest);
    }

    [Fact]
    public async Task AddJoin_JoinFieldNotOnTable_IsNotFoundWithoutGp()
    {
        var host = NewHost(new RecordingGeoprocessingService());
        host.SchemaHandler = path => OperationResult<SchemaInfo>.Ok(new SchemaInfo
        {
            Path = path,
            Exists = true,
            Fields = new List<SchemaFieldInfo> { new() { Name = "OBJECTID", Type = "OID" }, new() { Name = "label", Type = "String" } },
        });
        var gp = (RecordingGeoprocessingService)host.Geoprocessing;
        var result = await CallJoinAsync(gp, host, new Dictionary<string, object?>
        {
            ["layerName"] = "Roads", ["joinTable"] = JoinTablePath, ["joinField"] = "nope", ["confirm"] = true
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.NotFound, Assert.Single(result.Errors).Code);
        Assert.Contains("nope", Assert.Single(result.Errors).Message, StringComparison.Ordinal);
        Assert.Null(gp.LastRunRequest);
    }

    [Fact]
    public async Task AddJoin_ConfirmDefaultRefuse_PropagatesGpInvalidArgument()
    {
        var gp = new RecordingGeoprocessingService
        {
            RunResult = OperationResult<GpRunResult>.Fail(
                ErrorCodes.InvalidArgument, "'management.AddJoin' is destructive (in-place/overwrite semantics): confirm=true is required (default-refuse)."),
        };
        var result = await CallJoinAsync(gp, NewHost(gp), new Dictionary<string, object?>
        {
            ["layerName"] = "Roads", ["joinTable"] = JoinTablePath, ["joinField"] = "bid",
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.NotNull(gp.LastRunRequest);
        Assert.False(gp.LastRunRequest!.Confirm);   // 缺省拒：请求未携带 confirm
    }

    [Fact]
    public async Task AddJoin_BuildsSpikeVerifiedRequest_NamedForm()
    {
        var gp = OkGp();
        var host = NewHost(gp);
        host.SchemaHandler = path => path == LayerUri
            ? OperationResult<SchemaInfo>.Ok(new SchemaInfo
            {
                Path = path, Exists = true,
                Fields = new List<SchemaFieldInfo> { new() { Name = "OBJECTID", Type = "OID" }, new() { Name = "bid", Type = "Long" } },
            })
            : OperationResult<SchemaInfo>.Ok(new SchemaInfo
            {
                Path = path, Exists = true,
                Fields = new List<SchemaFieldInfo> { new() { Name = "OBJECTID", Type = "OID" }, new() { Name = "look_bid", Type = "Long" } },
            });
        var result = await CallJoinAsync(gp, host, new Dictionary<string, object?>
        {
            ["layerName"] = "Roads",
            ["joinTable"] = JoinTablePath,
            ["joinField"] = "bid",
            ["joinTableField"] = "look_bid",
            ["keepAll"] = true,
            ["confirm"] = true,
        });

        Assert.True(result.Success);
        var request = Assert.IsType<GpRunRequest>(gp.LastRunRequest);
        Assert.Equal("management.AddJoin", request.ToolName);
        Assert.True(request.Confirm);
        Assert.NotNull(request.Parameters);
        Assert.Equal("Roads", request.Parameters!["in_layer_or_view"]);
        Assert.Equal("bid", request.Parameters["in_field"]);
        Assert.Equal(JoinTablePath, request.Parameters["join_table"]);
        Assert.Equal("look_bid", request.Parameters["join_field"]);
        Assert.Equal("KEEP_ALL", request.Parameters["join_type"]);
    }

    [Fact]
    public async Task AddJoin_KeepAllOmitted_MapsToKeepCommon()
    {
        var gp = OkGp();
        var result = await CallJoinAsync(gp, NewHost(gp), new Dictionary<string, object?>
        {
            ["layerName"] = "Roads", ["joinTable"] = JoinTablePath, ["joinField"] = "bid", ["confirm"] = true
        });

        Assert.True(result.Success);
        Assert.Equal("KEEP_COMMON", gp.LastRunRequest!.Parameters!["join_type"]);
    }

    [Fact]
    public async Task AddJoin_Success_ReturnsBeforeAfterEvidence()
    {
        var host = NewHost(OkGp());
        var state = 0;
        host.JoinStateHandler = (_, _) =>
        {
            state++;
            return state == 1
                ? OperationResult<JoinStateInfo>.Ok(State("Roads", 3, Array.Empty<string>()))
                : OperationResult<JoinStateInfo>.Ok(State("Roads", 5, new[] { "look.label", "look.bid" }));
        };

        var gp = (RecordingGeoprocessingService)host.Geoprocessing;
        var result = await CallJoinAsync(gp, host, new Dictionary<string, object?>
        {
            ["layerName"] = "Roads", ["joinTable"] = JoinTablePath, ["joinField"] = "bid", ["confirm"] = true
        });

        Assert.True(result.Success);
        var payload = Assert.IsType<Dictionary<string, object?>>(result.Data);
        Assert.Equal("management.AddJoin", payload["gpTool"]);
        Assert.True((bool)payload["joinedDetected"]!);
        var before = Assert.IsType<Dictionary<string, object?>>(payload["fieldsBefore"]!);
        var after = Assert.IsType<Dictionary<string, object?>>(payload["fieldsAfter"]!);
        Assert.Equal(3, before["count"]);
        Assert.Equal(5, after["count"]);
        var newFields = Assert.IsType<List<string>>(payload["newFields"]);
        Assert.Contains("look.label", newFields);
    }

    [Fact]
    public async Task AddJoin_InFieldNotOnLayerSource_IsNotFoundWithoutGp()
    {
        var host = NewHost(OkGp());
        host.SchemaHandler = path => path == LayerUri
            ? OperationResult<SchemaInfo>.Ok(new SchemaInfo
            {
                Path = path,
                Exists = true,
                Fields = new List<SchemaFieldInfo> { new() { Name = "OBJECTID", Type = "OID" }, new() { Name = "name", Type = "String" } },
            })
            : OperationResult<SchemaInfo>.Ok(new SchemaInfo { Path = path, Exists = true, Fields = new List<SchemaFieldInfo> { new() { Name = "bid", Type = "Long" } } });
        var gp = (RecordingGeoprocessingService)host.Geoprocessing;
        var result = await CallJoinAsync(gp, host, new Dictionary<string, object?>
        {
            ["layerName"] = "Roads", ["joinTable"] = JoinTablePath, ["joinField"] = "nope", ["confirm"] = true
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.NotFound, Assert.Single(result.Errors).Code);
        Assert.Null(gp.LastRunRequest);
    }

    // ════════════════ remove_join ════════════════

    [Fact]
    public async Task RemoveJoin_MissingLayerName_IsInvalidArgumentWithoutGp()
    {
        var gp = new RecordingGeoprocessingService();
        var result = await CallRemoveAsync(gp, NewHost(gp), new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Null(gp.LastRunRequest);
    }

    [Fact]
    public async Task RemoveJoin_LayerNotFound_IsNotFoundWithoutGp()
    {
        var gp = new RecordingGeoprocessingService();
        var result = await CallRemoveAsync(gp, NewHost(gp), new Dictionary<string, object?> { ["layerName"] = "Nope", ["confirm"] = true });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.LayerNotFound, Assert.Single(result.Errors).Code);
        Assert.Null(gp.LastRunRequest);
    }

    [Fact]
    public async Task RemoveJoin_ConfirmDefaultRefuse_PropagatesGpInvalidArgument()
    {
        var gp = new RecordingGeoprocessingService
        {
            RunResult = OperationResult<GpRunResult>.Fail(
                ErrorCodes.InvalidArgument, "'management.RemoveJoin' is destructive (in-place/overwrite semantics): confirm=true is required (default-refuse)."),
        };
        var result = await CallRemoveAsync(gp, NewHost(gp), new Dictionary<string, object?> { ["layerName"] = "Roads" });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.NotNull(gp.LastRunRequest);
        Assert.False(gp.LastRunRequest!.Confirm);
    }

    [Fact]
    public async Task RemoveJoin_BuildsRequest_WithJoinNamePassthrough()
    {
        var gp = OkGp();
        var result = await CallRemoveAsync(gp, NewHost(gp), new Dictionary<string, object?>
        {
            ["layerName"] = "Roads", ["joinName"] = "look", ["confirm"] = true
        });

        Assert.True(result.Success);
        var request = Assert.IsType<GpRunRequest>(gp.LastRunRequest);
        Assert.Equal("management.RemoveJoin", request.ToolName);
        Assert.True(request.Confirm);
        Assert.Equal("Roads", request.Parameters!["in_layer_or_view"]);
        Assert.Equal("look", request.Parameters["join_name"]);
    }

    [Fact]
    public async Task RemoveJoin_JoinNameOmitted_RemovesAll()
    {
        var gp = OkGp();
        var result = await CallRemoveAsync(gp, NewHost(gp), new Dictionary<string, object?>
        {
            ["layerName"] = "Roads", ["confirm"] = true
        });

        Assert.True(result.Success);
        Assert.False(gp.LastRunRequest!.Parameters!.ContainsKey("join_name"));
    }

    [Fact]
    public async Task RemoveJoin_Success_ReturnsIdempotentNoJoinEvidence()
    {
        var host = NewHost(OkGp());
        var state = 0;
        host.JoinStateHandler = (_, _) =>
        {
            state++;
            return state == 1
                ? OperationResult<JoinStateInfo>.Ok(State("Roads", 5, new[] { "look.label" }))
                : OperationResult<JoinStateInfo>.Ok(State("Roads", 3, Array.Empty<string>()));
        };

        var gp = (RecordingGeoprocessingService)host.Geoprocessing;
        var result = await CallRemoveAsync(gp, host, new Dictionary<string, object?>
        {
            ["layerName"] = "Roads", ["confirm"] = true
        });

        Assert.True(result.Success);
        var payload = Assert.IsType<Dictionary<string, object?>>(result.Data);
        Assert.Equal("management.RemoveJoin", payload["gpTool"]);
        Assert.Equal("remove", payload["operation"]);
        Assert.False((bool)payload["joined"]!);
        Assert.True((bool)payload["idempotentNoJoin"]!);
        var removed = Assert.IsType<List<string>>(payload["removedFields"]);
        Assert.Contains("look.label", removed);
    }

    // ════════════════ 白名单契约（53；diff 仅两条） ════════════════

    [Fact]
    public void WhitelistFile_HasExactly53Entries_WithJoinPairDestructive()
    {
        var whitelist = LoadWhitelist();
        Assert.Equal(53, whitelist.Count);
        var add = Assert.Single(whitelist, e => e.Tool == "management.AddJoin");
        var remove = Assert.Single(whitelist, e => e.Tool == "management.RemoveJoin");
        Assert.True(add.Destructive);
        Assert.True(remove.Destructive);
        Assert.Equal(new[] { "in_layer_or_view", "in_field", "join_table", "join_field", "join_type" },
            add.Parameters.Select(p => p.Name).ToArray());
        Assert.Equal(new[] { "in_layer_or_view", "join_name" },
            remove.Parameters.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void WhitelistFile_Original51EntriesUnchanged()
    {
        string[] original51 =
        {
            "analysis.Buffer", "analysis.Clip", "analysis.Erase", "analysis.Union", "analysis.Intersect", "analysis.Identity",
            "analysis.SpatialJoin", "analysis.Near", "analysis.Statistics", "analysis.Frequency", "analysis.TabulateArea",
            "analysis.TabulateIntersection", "management.Dissolve", "management.Merge", "management.Project",
            "management.RepairGeometry", "management.CalculateField", "management.CopyFeatures", "management.MultipartToSinglepart",
            "conversion.ExportFeatures", "conversion.TableToTable", "conversion.RasterToPolygon", "conversion.RasterToPoint",
            "conversion.PolygonToRaster", "conversion.PointToRaster", "conversion.PolylineToRaster", "conversion.FeatureToRaster",
            "conversion.ASCIIToRaster", "conversion.RasterToASCII", "conversion.FeaturesToJSON", "conversion.JSONToFeatures",
            "sa.Slope", "sa.Aspect", "sa.Hillshade", "sa.Contour", "sa.Curvature", "sa.Fill", "sa.FlowDirection",
            "sa.FlowAccumulation", "sa.Watershed", "sa.SnapPourPoint", "sa.ZonalStatistics", "sa.ZonalStatisticsAsTable",
            "sa.Reclassify", "sa.ExtractByMask", "sa.ExtractValuesToPoints", "sa.Resample", "sa.Con", "sa.CellStatistics",
            "sa.FocalStatistics", "sa.RasterCalculator",
        };
        var whitelist = LoadWhitelist();
        Assert.Equal(original51, whitelist.Where(e => !e.Tool.StartsWith("management.AddJoin", StringComparison.Ordinal) && e.Tool != "management.RemoveJoin").Select(e => e.Tool).ToArray());
    }

    // ════════════════ 基础设施 ════════════════

    private static FakeArcGISHost NewHost(RecordingGeoprocessingService gp)
    {
        var host = new FakeArcGISHost(geoprocessing: gp);
        host.SchemaHandler = path => path == LayerUri
            ? OperationResult<SchemaInfo>.Ok(new SchemaInfo
            {
                Path = path,
                Exists = true,
                Fields = new List<SchemaFieldInfo> { new() { Name = "OBJECTID", Type = "OID" }, new() { Name = "bid", Type = "Long" } },
            })
            : OperationResult<SchemaInfo>.Ok(new SchemaInfo
            {
                Path = path,
                Exists = true,
                Fields = new List<SchemaFieldInfo> { new() { Name = "OBJECTID", Type = "OID" }, new() { Name = "bid", Type = "Long" }, new() { Name = "label", Type = "String" } },
            });
        host.JoinStateHandler = (_, _) => OperationResult<JoinStateInfo>.Ok(State("Roads", 3, Array.Empty<string>()));
        return host;
    }

    private static JoinStateInfo State(string layerName, int fieldCount, string[] dotted)
        => new()
        {
            LayerName = layerName,
            MapName = "TestMap",
            FieldCount = fieldCount,
            Fields = Enumerable.Range(0, Math.Max(0, fieldCount - dotted.Length)).Select(i => "f" + i)
                .Concat(dotted).ToList(),
            DottedFields = dotted,
            JoinedDetected = dotted.Length > 0,
        };

    private static RecordingGeoprocessingService OkGp()
        => new()
        {
            RunResult = OperationResult<GpRunResult>.Ok(new GpRunResult
            {
                ToolName = "management.AddJoin",
                DurationMs = 4,
                AuditPath = @"D:\audit",
                AuditEntryIndex = 1,
                ParameterForm = "named",
                Messages = new[] { "executed (test)" },
            }),
        };

    private static async Task<OperationResult<object?>> CallJoinAsync(
        RecordingGeoprocessingService gp,
        FakeArcGISHost host,
        IReadOnlyDictionary<string, object?> arguments)
        => await CallAsync(gp, host, new AddJoinTool(), arguments);

    private static async Task<OperationResult<object?>> CallRemoveAsync(
        RecordingGeoprocessingService gp,
        FakeArcGISHost host,
        IReadOnlyDictionary<string, object?> arguments)
        => await CallAsync(gp, host, new RemoveJoinTool(), arguments);

    private static async Task<OperationResult<object?>> CallAsync(
        RecordingGeoprocessingService gp,
        FakeArcGISHost host,
        IMCPTool tool,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(
            registry,
            host,
            new MCPSettings(),
            NullLogger.Instance);

        return await router.ExecuteAsync(new MCPToolCall
        {
            Name = tool.Name,
            Arguments = arguments,
        });
    }

    private static List<GpWhitelistEntry> LoadWhitelist()
    {
        var root = FindRepositoryRoot();
        var json = File.ReadAllText(Path.Combine(root, "Config", "gp-whitelist.json"));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var list = new List<GpWhitelistEntry>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var entry = new GpWhitelistEntry
            {
                Tool = element.GetProperty("tool").GetString() ?? string.Empty,
                Destructive = element.GetProperty("destructive").GetBoolean(),
            };
            foreach (var p in element.GetProperty("parameters").EnumerateArray())
            {
                entry.Parameters.Add(new GpWhitelistParameter
                {
                    Name = p.GetProperty("name").GetString() ?? string.Empty,
                    Direction = p.GetProperty("direction").GetString() ?? "input",
                    Required = p.GetProperty("required").GetBoolean(),
                    Type = p.GetProperty("type").GetString() ?? "string",
                });
            }

            list.Add(entry);
        }

        return list;
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ArcGIS-Pro-MCP.sln"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("repository root not found (ArcGIS-Pro-MCP.sln)");
    }
}
