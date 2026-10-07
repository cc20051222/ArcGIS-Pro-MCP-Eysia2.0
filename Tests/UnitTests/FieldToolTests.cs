using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 9 第四批（D-038）：alter_field / calculate_field / get_field_values 编排单测 +
/// FieldExpressionPolicy 安全白名单（calculate_field 禁任意 Python/代码块/多语句/注释/函数）。
/// 记录器观测 Tool → Router → IGeoprocessingService / IAttributeService 边界，不执行真实 GP。
/// </summary>
public sealed class FieldToolTests
{
    private static async Task<OperationResult<object?>> CallAsync(
        FakeArcGISHost host,
        IMCPTool tool,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    private static (FakeArcGISHost Host, RecordingGeoprocessingService Gp) HostWithGp()
    {
        var gp = new RecordingGeoprocessingService { Result = GpOk("ok") };
        return (new FakeArcGISHost(gp), gp);
    }

    private static OperationResult<GeoprocessingResult> GpOk(string result)
        => OperationResult<GeoprocessingResult>.Ok(new GeoprocessingResult { Result = result, ToolName = "test" });

    // ---------------------------------------------------------------- FieldExpressionPolicy

    [Theory]
    [InlineData("VALUE * 2")]
    [InlineData("CASE WHEN NOTE IS NULL THEN 'x' ELSE NOTE END")]
    [InlineData("'固定值'")]
    [InlineData("POP = 100 AND NOTE IS NOT NULL")]
    [InlineData("[FIELD] + 1")]
    [InlineData("NOT (A > 1 OR B <= 2)")]
    public void Policy_AcceptsRestrictedSqlSubset(string expression)
    {
        Assert.True(FieldExpressionPolicy.IsSafe(expression, out var reason), reason);
    }

    [Theory]
    [InlineData("import os")]
    [InlineData("1;2")]
    [InlineData("--comment")]
    [InlineData("/*block*/")]
    [InlineData("SELECT 1")]
    [InlineData("__x + 1")]
    [InlineData("UPPER(NAME)")]
    [InlineData("code_block")]
    [InlineData("PYTHON3")]
    [InlineData("xp_cmdshell")]
    [InlineData("sp_help")]
    [InlineData("EXEC proc")]
    [InlineData("a union b")]
    [InlineData("DROP TABLE t")]
    [InlineData("")]
    [InlineData("   ")]
    public void Policy_RejectsDangerousOrUnsupportedExpressions(string expression)
    {
        Assert.False(FieldExpressionPolicy.IsSafe(expression, out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void Policy_RejectsControlCharsAndBackquoteAndOversize()
    {
        Assert.False(FieldExpressionPolicy.IsSafe("A" + (char)1 + "B", out _));
        Assert.False(FieldExpressionPolicy.IsSafe("`x`", out _));
        Assert.False(FieldExpressionPolicy.IsSafe(new string('1', FieldExpressionPolicy.MaxLength + 1), out _));
    }

    // ---------------------------------------------------------------- calculate_field

    [Fact]
    public async Task CalculateField_DefaultOnlyWhenNull_WrapsInCaseAndHardcodesSql()
    {
        var (host, gp) = HostWithGp();
        var result = await CallAsync(host, new CalculateFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = @"D:\data.gdb\FC1", ["fieldName"] = "NOTE", ["expression"] = "'fixed'"
        });

        Assert.True(result.Success);
        var request = Assert.Single(gp.Requests);
        Assert.Equal("CalculateField_management", request.ToolName);
        Assert.Equal(@"D:\data.gdb\FC1", request.Values![0]);
        Assert.Equal("NOTE", request.Values[1]);
        Assert.Equal("CASE WHEN NOTE IS NULL THEN ('fixed') ELSE NOTE END", request.Values[2]); // 默认 onlyWhenNull=true
        Assert.Equal("SQL", request.Values[3]);                                                // 硬编码 SQL
        Assert.Equal(string.Empty, request.Values[4]);                                          // code_block 恒空
    }

    [Fact]
    public async Task CalculateField_OnlyWhenNullFalse_PassesExpressionThrough()
    {
        var (host, gp) = HostWithGp();
        var result = await CallAsync(host, new CalculateFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "FC1", ["fieldName"] = "VALUE", ["expression"] = "VALUE * 2", ["onlyWhenNull"] = false
        });

        Assert.True(result.Success);
        Assert.Equal("VALUE * 2", Assert.Single(gp.Requests).Values![2]);
    }

    [Theory]
    [InlineData("import os")]
    [InlineData("1;2")]
    [InlineData("--x")]
    [InlineData("__x")]
    [InlineData("UPPER(NAME)")]
    public async Task CalculateField_DangerousExpression_ReturnsInvalidArgumentAndNeverTouchesGp(string expression)
    {
        var (host, gp) = HostWithGp();
        var result = await CallAsync(host, new CalculateFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "FC1", ["fieldName"] = "NOTE", ["expression"] = expression
        });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Empty(gp.Requests);   // 先校验后下传：不进 GP
    }

    [Fact]
    public async Task CalculateField_ExpressionTypeArgumentIsIgnored_SqlIsHardcoded()
    {
        var (host, gp) = HostWithGp();
        var result = await CallAsync(host, new CalculateFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "FC1", ["fieldName"] = "NOTE", ["expression"] = "1", ["expression_type"] = "PYTHON3"
        });

        Assert.True(result.Success);   // expression_type 不在参数面 → 被忽略
        Assert.Equal("SQL", Assert.Single(gp.Requests).Values![3]);
    }

    [Fact]
    public async Task CalculateField_BlankExpression_ReturnsInvalidArgument()
    {
        var host = new FakeArcGISHost();
        var result = await CallAsync(host, new CalculateFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "FC1", ["fieldName"] = "NOTE", ["expression"] = " "
        });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }

    // ---------------------------------------------------------------- alter_field

    [Fact]
    public async Task AlterField_RoutesWithRenameNoOpAndDefaults()
    {
        var (host, gp) = HostWithGp();
        var result = await CallAsync(host, new AlterFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = @"D:\data.gdb\FC1", ["fieldName"] = "NOTE", ["fieldAlias"] = "备注"
        });

        Assert.True(result.Success);
        var request = Assert.Single(gp.Requests);
        Assert.Equal("AlterField_management", request.ToolName);
        // [in_table, field, new_field_name(=原字段名 no-op), alias, field_type(""), length, is_nullable, clear_alias]
        Assert.Equal("NOTE", request.Values![2]);
        Assert.Equal("备注", request.Values[3]);
        Assert.Equal(string.Empty, request.Values[4]);   // 不改类型
        Assert.Equal(string.Empty, request.Values[6]);   // 未给可空性 → 保持
        // F-D038-2 回归防线：clear_field_alias 必须为 DO_NOT_CLEAR（空串被 GP 视为"已指定清除" → 与 alias 冲突 → 001656）。
        Assert.Equal("DO_NOT_CLEAR", request.Values[7]);
        Assert.NotEqual(string.Empty, request.Values[7]);
        // 互斥不变量：new_field_alias 与 clear_field_alias 不可能"同时指定清除"。
        Assert.True(string.IsNullOrEmpty(request.Values[3] as string) || (request.Values[7] as string) == "DO_NOT_CLEAR");
    }

    [Fact]
    public async Task AlterField_BlankAlias_ReturnsInvalidArgumentAndNeverTouchesGp()
    {
        // D-040 A 组（A6 收口）：显式传空白 alias → policy 层拒绝，零 GP 执行（G-78-A 口径）。
        var (host, gp) = HostWithGp();
        var result = await CallAsync(host, new AlterFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = @"D://data.gdb//FC1", ["fieldName"] = "NOTE", ["fieldAlias"] = "   "
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
        Assert.Contains("fieldAlias", Assert.Single(result.Errors).Message, StringComparison.Ordinal);
        Assert.Empty(gp.Requests);
    }

    [Fact]
    public async Task AlterField_OmittedAlias_StillPassesDoNotClear()
    {
        // D-040 A 组：**未传** alias = 保持别名不变 → 正常进 GP，且 clear 位仍为 DO_NOT_CLEAR。
        var (host, gp) = HostWithGp();
        var result = await CallAsync(host, new AlterFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = @"D://data.gdb//FC1", ["fieldName"] = "NOTE"
        });

        Assert.True(result.Success);
        var values = Assert.Single(gp.Requests).Values!;
        Assert.Equal(string.Empty, values[3]);          // 未传 → 别名位空串（保持不变语义）
        Assert.Equal("DO_NOT_CLEAR", values[7]);        // clear 位固定不清除
    }

    [Fact]
    public async Task AlterField_PassesNullabilityAndLength()
    {
        var (host, gp) = HostWithGp();
        var result = await CallAsync(host, new AlterFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "FC1", ["fieldName"] = "NOTE", ["fieldIsNullable"] = false, ["fieldLength"] = 64
        });

        Assert.True(result.Success);
        var values = Assert.Single(gp.Requests).Values!;
        Assert.Equal("64", values[5]);
        Assert.Equal("NON_NULLABLE", values[6]);
    }

    [Fact]
    public async Task AlterField_BlankFieldName_ReturnsInvalidArgument()
    {
        var host = new FakeArcGISHost();
        var result = await CallAsync(host, new AlterFieldTool(), new Dictionary<string, object?>
        {
            ["inputPath"] = "FC1", ["fieldName"] = " "
        });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }

    // ---------------------------------------------------------------- get_field_values

    [Fact]
    public async Task GetFieldValues_ReadOnlyNative_ReturnsProfile()
    {
        var (host, gp) = HostWithGp();
        var result = await CallAsync(host, new GetFieldValuesTool(), new Dictionary<string, object?>
        {
            ["mapName"] = "WB_Map", ["layerName"] = "L_Points", ["fieldName"] = "NAME"
        });

        Assert.True(result.Success);
        var info = Assert.IsType<FieldValuesInfo>(result.Data);
        Assert.Equal(5, info.TotalCount);
        Assert.Equal(5, info.DistinctCount);
        Assert.False(info.Truncated);
        Assert.Empty(gp.Requests);   // 只读：零 GP
    }

    [Fact]
    public async Task GetFieldValues_ClampsMaxDistinctToCap()
    {
        var host = new FakeArcGISHost();
        await CallAsync(host, new GetFieldValuesTool(), new Dictionary<string, object?>
        {
            ["layerName"] = "L_Points", ["fieldName"] = "NAME", ["maxDistinct"] = 5000
        });

        Assert.Equal(2000, ((FakeAttributeService)host.Attributes).LastMaxDistinct);   // 上限钳制（契约：默认 200 / 上限 2000）
    }

    [Fact]
    public async Task GetFieldValues_BlankFieldName_ReturnsInvalidArgument()
    {
        var host = new FakeArcGISHost();
        var result = await CallAsync(host, new GetFieldValuesTool(), new Dictionary<string, object?>
        {
            ["layerName"] = "L_Points", ["fieldName"] = " "
        });

        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }
}
