using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 9 P2「契约-实现差距」维修单 **D-037** 回归用例（阶段一，不启 Pro、不装包）。
/// 覆盖：F-D036-1（a 末段 .gdb 走文件系统语义 / b 容器内判定行为不变 / c overwrite 双信号 / d 行为确定）、
/// F-D036-2（add_field 字段名冲突 → 错误如实）、F-D035-1（export_table fieldNames 子集必须生效，禁静默）、
/// F-D035-2（project outSR 名称形态以契约为准 → 名称解析为 WKID 后下传）。
/// </summary>
public sealed class D037ContractGapRepairTests
{
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

    private static OperationResult<GeoprocessingResult> GpOk(string result, IEnumerable<string>? messages = null, string? stateProof = null)
        => OperationResult<GeoprocessingResult>.Ok(new GeoprocessingResult
        {
            Result = result,
            ToolName = "test",
            Messages = messages is null ? Array.Empty<string>() : messages.ToList(),
            StateProof = stateProof,
        });

    // ================================================================ F-D036-1 (a)(b)

    [Theory]
    [InlineData(@"D:\scratch\out.gdb", true)]
    [InlineData(@"D:\scratch\out.GDB", true)]
    [InlineData("out.gdb", true)]
    [InlineData(@"D:\scratch\out.gdb\FC", false)]
    [InlineData(@"D:\scratch\out.gdb\ds\FC", false)]
    [InlineData(@"D:\scratch\out.tbx", false)]
    [InlineData(@"D:\scratch\out.gdb.bak", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsGdbContainerRoot_OnlyMatchesWhenThePathItselfIsTheContainer(string? path, bool expected)
        => Assert.Equal(expected, StateProof.IsGdbContainerRoot(path));

    [Theory]
    [InlineData(@"D:\scratch\out.gdb", true)]
    [InlineData(@"D:\scratch\out.gdb\FC", true)]
    [InlineData(@"D:\scratch\out.tbx", false)]
    public void IsGdbContainerPath_BehaviorUnchangedForInContainerPaths(string? path, bool expected)
        // D-037 (b)：容器内路径判定**行为不变**（旧语义：任一段以 .gdb 结尾即命中）。
        => Assert.Equal(expected, StateProof.IsGdbContainerPath(path));

    [Fact]
    public void IsGdbContainerRoot_AndInContainer_AreMutuallyExclusiveForContainerPaths()
    {
        // 容器本体 → Root=true；容器内数据集 → Root=false 且 ContainerPath=true（两条判定分道，不得再混用）。
        Assert.True(StateProof.IsGdbContainerRoot(@"D:\s\a.gdb"));
        Assert.False(StateProof.IsGdbContainerRoot(@"D:\s\a.gdb\FC"));
        Assert.True(StateProof.IsGdbContainerPath(@"D:\s\a.gdb\FC"));
    }

    // ================================================================ F-D036-1 (c)(d)

    [Fact]
    public async Task CreateFileGdb_ExistingGdb_WithOverwriteTrue_EmitsBothOverwriteSignals()
    {
        var service = new RecordingGeoprocessingService
        {
            ExistenceResult = OutputExistence.Exists,
            Result = GpOk("gdb-ok", stateProof: "{\"before\":null,\"after\":null,\"verdict\":\"unprovable\"}")
        };

        var result = await CallAsync(service, new CreateFileGdbTool(), new Dictionary<string, object?>
        {
            ["outputPath"] = @"D:\scratch\out.gdb", ["overwrite"] = true
        });

        Assert.True(result.Success);
        var gp = Assert.IsType<GeoprocessingResult>(result.Data);
        // 双信号（D-026 F8 / D-036 契约）：overwriteNote ≠ null 且 stateProof.overwrite == true。
        Assert.False(string.IsNullOrWhiteSpace(gp.OverwriteNote));
        Assert.NotNull(gp.StateProof);
        using var doc = JsonDocument.Parse(gp.StateProof!);
        Assert.True(doc.RootElement.GetProperty("overwrite").GetBoolean());
    }

    [Fact]
    public async Task CreateFileGdb_ExistingGdb_DeterministicAcrossRepeatedCalls()
    {
        // F-D036-1 (d)：同一输入两次调用必须**结果一致**（历史缺陷：一次 GP 000258、一次 GP 成功）。
        for (var i = 0; i < 2; i++)
        {
            var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists, Result = GpOk("gdb-ok") };
            var result = await CallAsync(service, new CreateFileGdbTool(), new Dictionary<string, object?>
            {
                ["outputPath"] = @"D:\scratch\out.gdb"
            });

            Assert.False(result.Success);
            Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
            Assert.Empty(service.Requests); // 未进 GP → 不可能出现"GP 成功"的第二种行为
        }
    }

    [Fact]
    public async Task CreateFileGdb_ExistingGdb_WithoutOverwrite_RefusesBeforeGp()
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };
        var result = await CallAsync(service, new CreateFileGdbTool(), new Dictionary<string, object?>
        {
            ["outputPath"] = @"D:\scratch\out.gdb"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ================================================================ F-D036-2

    [Fact]
    public async Task AddField_ExistingFieldWarning000012_IsSurfacedAsExplicitError()
    {
        var service = new RecordingGeoprocessingService
        {
            Result = GpOk("addfield-ok", new[] { "WARNING 000012: NOTE already exists" })
        };

        var result = await CallAsync(service, new AddFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = @"D:\data.gdb\FC1", ["fieldName"] = "NOTE"
        });

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.GeoprocessingError, error.Code);
        Assert.Contains("000012", error.Message, StringComparison.Ordinal);
        Assert.Contains("NOTE", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddField_LocalizedWarningAlreadyExists_IsSurfacedAsExplicitError()
    {
        var service = new RecordingGeoprocessingService
        {
            Result = GpOk("addfield-ok", new[] { "WARNING 000012: 字段 NOTE 已存在" })
        };

        var result = await CallAsync(service, new AddFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "FC1", ["fieldName"] = "NOTE"
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.GeoprocessingError, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task AddField_NoConflictWarning_RemainsSuccess()
    {
        var service = new RecordingGeoprocessingService
        {
            Result = GpOk("addfield-ok", new[] { "Start Time: x", "Succeeded at y" })
        };

        var result = await CallAsync(service, new AddFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "FC1", ["fieldName"] = "NOTE"
        });

        Assert.True(result.Success);
    }

    [Fact]
    public void GpMessageSemantics_DetectsOnlyPreExistingMemberMessages()
    {
        Assert.True(GpMessageSemantics.IndicatesPreExistingMember(new[] { "WARNING 000012: X already exists" }));
        Assert.True(GpMessageSemantics.IndicatesPreExistingMember(new[] { "WARNING 000012: 已存在" }));
        Assert.True(GpMessageSemantics.IndicatesPreExistingMember(new[] { "WARNING: field X already exists" }));
        Assert.False(GpMessageSemantics.IndicatesPreExistingMember(new[] { "Start Time: 1", "Succeeded at 2" }));
        Assert.False(GpMessageSemantics.IndicatesPreExistingMember(Array.Empty<string>()));
        Assert.False(GpMessageSemantics.IndicatesPreExistingMember(null));
    }

    // ================================================================ F-D035-1

    [Fact]
    public async Task ExportTable_FieldMappingIsPassedAtTheFieldMappingPosition()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("export-ok") };
        var result = await CallAsync(service, new ExportTableTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src.gdb/TBL",
            ["outputPath"] = "D:/out/tbl.csv",
            ["fieldNames"] = new[] { "NAME" }
        }, schema: new FakeFieldProbeSchemaService { Fields = new List<SchemaFieldInfo> { new() { Name = "NAME" } } });

        Assert.True(result.Success);
        var values = Assert.Single(service.Requests).Values!;
        Assert.Equal(5, values.Count);
        Assert.Equal("src.gdb/TBL", values[0]);
        Assert.Equal("D:/out/tbl.csv", values[1]);
        Assert.Equal(string.Empty, values[2]);          // where_clause（空）
        Assert.Equal("NOT_USE_ALIAS", values[3]);       // use_field_alias_as_name
        Assert.Equal("NAME NAME VISIBLE NONE", values[4]); // field_mapping（旧实现错放在索引 2）
    }

    [Fact]
    public async Task ExportTable_OutputContainsUnrequestedFields_IsAnExplicitError()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("export-ok") };
        var result = await CallAsync(service, new ExportTableTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src.gdb/TBL",
            ["outputPath"] = "D:/out.gdb/TBL_OUT",
            ["fieldNames"] = new[] { "NAME" }
        }, schema: new FakeFieldProbeSchemaService
        {
            // 映射未生效：输出仍是全字段（不含任何请求外字段的"静默成功"必须被拦下）。
            Fields = new List<SchemaFieldInfo> { new() { Name = "OBJECTID" }, new() { Name = "NAME" }, new() { Name = "VALUE" } }
        });

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.GeoprocessingError, error.Code);
        Assert.Contains("did not take effect", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportTable_RequestedFieldMissingFromOutput_IsAnExplicitError()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("export-ok") };
        var result = await CallAsync(service, new ExportTableTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src.gdb/TBL",
            ["outputPath"] = "D:/out.gdb/TBL_OUT",
            ["fieldNames"] = new[] { "NAME", "VALUE" }
        }, schema: new FakeFieldProbeSchemaService
        {
            Fields = new List<SchemaFieldInfo> { new() { Name = "OBJECTID" }, new() { Name = "NAME" } }
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.GeoprocessingError, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task ExportTable_SystemFieldsAreIgnoredAndVerifiedSubsetSucceeds()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("export-ok") };
        var result = await CallAsync(service, new ExportTableTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src.gdb/FC",
            ["outputPath"] = "D:/out.gdb/FC_OUT",
            ["fieldNames"] = new[] { "NAME" }
        }, schema: new FakeFieldProbeSchemaService
        {
            Fields = new List<SchemaFieldInfo>
            {
                new() { Name = "OBJECTID" }, new() { Name = "Shape" }, new() { Name = "NAME" },
            }
        });

        Assert.True(result.Success);
        var gp = Assert.IsType<GeoprocessingResult>(result.Data);
        Assert.Contains(gp.Messages, m => m.Contains("fieldSubsetVerified", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExportTable_UnverifiableOutput_IsAnnotatedNotSilent()
    {
        var service = new RecordingGeoprocessingService { Result = GpOk("export-ok") };
        var result = await CallAsync(service, new ExportTableTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src.gdb/TBL",
            ["outputPath"] = "D:/out/tbl.csv",
            ["fieldNames"] = new[] { "NAME" }
        }, schema: new FakeFieldProbeSchemaService { Succeeds = false });

        Assert.True(result.Success); // 导出本身成功
        var gp = Assert.IsType<GeoprocessingResult>(result.Data);
        Assert.Contains(gp.Messages, m => m.Contains("fieldSubsetUnverified", StringComparison.Ordinal));
    }

    // ================================================================ F-D035-2

    [Fact]
    public void SpatialReferenceName_NormalizationIgnoresCaseSpacesUnderscoresAndPunctuation()
    {
        Assert.Equal(
            SpatialReferenceName.Normalize("WGS 1984 Web Mercator (auxiliary sphere)"),
            SpatialReferenceName.Normalize("WGS_1984_Web_Mercator_Auxiliary_Sphere"));
        Assert.Equal("wgs1984", SpatialReferenceName.Normalize("WGS_1984"));
        Assert.Equal(string.Empty, SpatialReferenceName.Normalize("   "));
    }

    [Theory]
    [InlineData("4326", true, 4326)]
    [InlineData(" 3857 ", true, 3857)]
    [InlineData("-1", false, 0)]
    [InlineData("+4326", false, 0)]
    [InlineData("4326.0", false, 0)]
    [InlineData("WGS 1984", false, 0)]
    [InlineData("", false, 0)]
    public void SpatialReferenceName_IsWkidLiteral(string? value, bool expected, int wkid)
    {
        Assert.Equal(expected, SpatialReferenceName.IsWkidLiteral(value, out var actual));
        Assert.Equal(wkid, actual);
    }

    [Fact]
    public void SpatialReferenceName_TryResolveMatchesNormalizedNames()
    {
        var entries = new[]
        {
            new SpatialReferenceEntry(4326, "GCS_WGS_1984"),
            new SpatialReferenceEntry(3857, "WGS_1984_Web_Mercator_Auxiliary_Sphere"),
        };

        Assert.True(SpatialReferenceName.TryResolve("WGS 1984 Web Mercator (auxiliary sphere)", entries, out var m1));
        Assert.Equal(3857, m1!.Wkid);
        Assert.True(SpatialReferenceName.TryResolve("GCS_WGS_1984", entries, out var m2));
        Assert.Equal(4326, m2!.Wkid);
        Assert.False(SpatialReferenceName.TryResolve("Not A Coordinate System", entries, out _));
    }

    [Fact]
    public async Task Project_NameFormOutSr_IsResolvedToWkidBeforeGp()
    {
        var sr = new FakeSpatialReferenceService();
        sr.Entries.Add(new SpatialReferenceEntry(3857, "WGS_1984_Web_Mercator_Auxiliary_Sphere"));

        var service = new RecordingGeoprocessingService { Result = GpOk("project-ok") };
        var result = await CallAsync(service, new ProjectTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src.gdb/FC", ["outputPath"] = "D:/out.gdb/FC_p",
            ["outSR"] = "WGS 1984 Web Mercator (auxiliary sphere)"
        }, spatialReferences: sr);

        Assert.True(result.Success);
        Assert.Equal("3857", Assert.Single(service.Requests).Values![2]);
    }

    [Fact]
    public async Task Project_WkidLiteral_PassesThroughUnchanged()
    {
        var sr = new FakeSpatialReferenceService();
        sr.Entries.Add(new SpatialReferenceEntry(3857, "WGS_1984_Web_Mercator_Auxiliary_Sphere"));

        var service = new RecordingGeoprocessingService { Result = GpOk("project-ok") };
        var result = await CallAsync(service, new ProjectTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src.gdb/FC", ["outputPath"] = "D:/out.gdb/FC_p", ["outSR"] = "4326"
        }, spatialReferences: sr);

        Assert.True(result.Success);
        Assert.Equal("4326", Assert.Single(service.Requests).Values![2]);
    }

    [Fact]
    public async Task Project_UnresolvableName_PassesThroughSoGpReportsTheRealCode()
    {
        var sr = new FakeSpatialReferenceService { Resolves = false };

        var service = new RecordingGeoprocessingService
        {
            Result = OperationResult<GeoprocessingResult>.Fail(
                ErrorCodes.GeoprocessingError, "Geoprocessing tool 'Project_management' failed.", "ERROR 000735: out_coor_system: 值是必需的")
        };

        var result = await CallAsync(service, new ProjectTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "src.gdb/FC", ["outputPath"] = "D:/out.gdb/FC_p", ["outSR"] = "Not A Coordinate System"
        }, spatialReferences: sr);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.GeoprocessingError, Assert.Single(result.Errors).Code);
        // 未解析 → 原样下传（GP 产出含真实 GP 码的显式错误），不静默回落到任意坐标系。
        Assert.Equal("Not A Coordinate System", Assert.Single(service.Requests).Values![2]);
    }
}
