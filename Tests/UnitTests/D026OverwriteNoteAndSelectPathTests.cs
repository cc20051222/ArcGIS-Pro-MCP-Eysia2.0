using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-026：F8 覆写提示入成功载荷 + F9 select 类组子层 SDK 直路径。
/// </summary>
public sealed class D026OverwriteNoteAndSelectPathTests
{
    private const string CompatibilityServices = "Source/ArcGISProMCP.Compatibility/Services";

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ArcGIS-Pro-MCP.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Repository root could not be located from test base directory: {AppContext.BaseDirectory}");
    }

    private static string ReadSource(string relativePath)
    {
        var full = Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(full), $"Source file is missing: {relativePath}");
        return File.ReadAllText(full);
    }

    private static int CountOccurrences(string content, string needle)
        => content.Split(needle, StringSplitOptions.None).Length - 1;

    // ------------------------------------------------------- F8：OverwriteNote 入载荷

    [Fact]
    public void Serialize_OverwriteNoteSet_AppearsInPayload()
    {
        var gp = new GeoprocessingResult
        {
            ToolName = "Buffer_analysis",
            Result = "D:\\x\\out.shp",
            OverwriteNote = " (overwrite=true: existing output 'D:\\x\\out.shp' was replaced by this run)",
        };
        var json = JsonSerializer.Serialize(gp, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"overwriteNote\":", json);
        Assert.Contains("was replaced", json);
    }

    [Fact]
    public void Serialize_FreshPath_OverwriteNoteIsNull()
    {
        // 缺省路径：字段存在且为 null（向后兼容扩展，非缺字段）。
        var gp = new GeoprocessingResult { ToolName = "Buffer_analysis", Result = "D:\\x\\out.shp" };
        var json = JsonSerializer.Serialize(gp, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"overwriteNote\":null", json);
    }

    [Fact]
    public void AddOverwriteMarker_AddsTrueFlag()
    {
        var input = "{\"before\":{\"exists\":true},\"after\":{\"exists\":true},\"verdict\":\"not_executed\"}";
        var outJson = StateProof.AddOverwriteMarker(input);
        Assert.NotNull(outJson);
        Assert.Contains("\"overwrite\":true", outJson!.Replace(" ", string.Empty));
        Assert.Contains("\"verdict\":\"not_executed\"", outJson);
    }

    [Fact]
    public void AddOverwriteMarker_Idempotent_WhenAlreadyPresent()
    {
        var input = "{\"verdict\":\"not_executed\",\"overwrite\":true}";
        Assert.Equal(input, StateProof.AddOverwriteMarker(input));
    }

    [Fact]
    public void AddOverwriteMarker_InvalidOrEmptyJson_ReturnsOriginal()
    {
        Assert.Equal("not-json", StateProof.AddOverwriteMarker("not-json"));
        Assert.Equal("", StateProof.AddOverwriteMarker(""));
        Assert.Null(StateProof.AddOverwriteMarker(null));
    }

    [Fact]
    public void Structure_Annotate_FillsOverwriteNote_And_Marker()
    {
        var src = ReadSource("Source/Shared/ArcGISProMCP.Tools/GeoprocessingTools.cs");
        Assert.Contains("gp.OverwriteNote = OverwritePolicy.OverwriteNote(Output)", src);
        Assert.Contains("StateProof.AddOverwriteMarker(gp.StateProof)", src);
    }

    // ------------------------------------------------------- F9：where 子句构造（纯逻辑）

    [Fact]
    public void Where_OidList_NonEmpty_UsesRealOidField()
    {
        Assert.Equal("OBJECTID IN (1,2,3)", SelectWhereClause.Build(new List<long> { 1, 2, 3 }, null, "OBJECTID"));
        Assert.Equal("FID IN (7)", SelectWhereClause.Build(new List<long> { 7 }, "1=1", "FID"));
    }

    [Fact]
    public void Where_OidList_Empty_MatchesNothing()
    {
        Assert.Equal("1=0", SelectWhereClause.Build(new List<long>(), null, "OBJECTID"));
    }

    [Fact]
    public void Where_NoOidList_PassesThroughWhere()
    {
        Assert.Equal("NAME = 'x'", SelectWhereClause.Build(null, "NAME = 'x'", "OBJECTID"));
        Assert.Equal(string.Empty, SelectWhereClause.Build(null, null, "OBJECTID"));
    }

    // ------------------------------------------------------- 结构断言（先断言样本条数）

    [Fact]
    public void Structure_AttributeSelect_NoLonger_RoutesThrough_GP()
    {
        // F9：select_by_attribute 的匹配集不再经 GP 引擎（组子层 000732 根因）。
        var src = ReadSource($"{CompatibilityServices}/GeoprocessingService.cs");
        Assert.Equal(0, CountOccurrences(src, "management.SelectLayerByAttribute"));
        Assert.Contains("SelectWhereClause.Build", src);
        Assert.Contains("featureLayer.Search(new QueryFilter", src);
    }

    [Fact]
    public void Structure_LocationSelect_Retains_GP_Path_PendingRuling()
    {
        // F9 评估结论：select_by_location 维持 GP 路径（17 种 overlapType 映射不等价风险，上报等裁定）。
        var src = ReadSource($"{CompatibilityServices}/GeoprocessingService.cs");
        Assert.Contains("management.SelectLayerByLocation", src);
    }
}
