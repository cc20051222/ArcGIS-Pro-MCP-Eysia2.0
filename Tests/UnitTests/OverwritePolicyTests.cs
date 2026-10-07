using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-021 / F6 覆写策略单测：S1（默认拒绝）+ S2（显式 overwrite 逃生舱）。
/// 覆盖：纯策略矩阵 / 4 个 GP 写工具的前置闸门（拒绝时不进入 GP）/ 容器语义分支 / 错误码 / schema 声明。
/// 全部走 Tool → Router → IGeoprocessingService 边界的测试替身，**不触碰 ArcGIS Pro**。
/// </summary>
public sealed class OverwritePolicyTests
{
    // ------------------------------------------------------------------ 1. 纯策略矩阵

    [Theory]
    [InlineData(OutputExistence.NotExists, false, true)]
    [InlineData(OutputExistence.NotExists, true, true)]
    [InlineData(OutputExistence.Exists, false, false)]
    [InlineData(OutputExistence.Exists, true, true)]
    [InlineData(OutputExistence.Unknown, false, false)]
    [InlineData(OutputExistence.Unknown, true, true)]
    public void Policy_DecisionMatrix(OutputExistence existence, bool overwrite, bool expectedProceed)
    {
        var decision = OverwritePolicy.Decide(existence, overwrite, "C:\\out\\a.shp", "detail-x");

        Assert.Equal(expectedProceed, decision.Proceed);
        if (expectedProceed)
        {
            Assert.Null(decision.ErrorCode);
            Assert.Null(decision.Message);
        }
        else
        {
            // 红线：拒绝一律复用既有 OUTPUT_EXISTS，不得新造错误码。
            Assert.Equal(ErrorCodes.OutputExists, decision.ErrorCode);
            Assert.Contains("C:\\out\\a.shp", decision.Message!);
        }
    }

    [Fact]
    public void Policy_UnknownRefusalExplainsUndeterminableExistence()
    {
        var decision = OverwritePolicy.Decide(OutputExistence.Unknown, false, "C:\\x.gdb\\fc", "sdk-error: boom");

        Assert.False(decision.Proceed);
        Assert.Equal(ErrorCodes.OutputExists, decision.ErrorCode);
        Assert.Contains("could not be determined", decision.Message!);
        Assert.Contains("sdk-error: boom", decision.Message!);
        Assert.Contains("overwrite=true", decision.Message!);
    }

    [Fact]
    public void Policy_ExistsRefusalGivesMigrationGuidance()
    {
        var decision = OverwritePolicy.Decide(OutputExistence.Exists, false, "C:\\x.gdb\\fc");

        Assert.Contains("overwrite=true", decision.Message!);
        Assert.Contains("delete the output first", decision.Message!);
    }

    [Fact]
    public void Policy_OverwriteNoteIsAttachedOnlyWhenAuthorised()
    {
        Assert.Contains("overwrite=true", OverwritePolicy.OverwriteNote("C:\\x.gdb\\fc"));
    }

    /// <summary>
    /// K3（D-022 转入，来自 GATE-D021 §5）：为 <c>Unknown + overwrite:true</c> 补一条**显式的具名断言**。
    /// D-021 §2.2 既定语义 = **放行执行**（判定不可得 + 用户已显式授权覆写 → 不做保守拦截）。
    /// 该分支此前仅存在于 <see cref="Policy_DecisionMatrix"/> 的行内数据里，易被后来者误读为红线违反
    /// （"Unknown 必须保守"）——故单独成例并写明理由。
    /// 红线提示：本例**只加测试**，D-021 的覆写策略实现（OverwritePolicy / GpOverwriteGuard）语义冻结，不得改动。
    /// </summary>
    [Fact]
    public void Policy_UnknownWithExplicitOverwrite_IsAllowed_ByDesign()
    {
        var decision = OverwritePolicy.Decide(OutputExistence.Unknown, true, "C:\\x.gdb\\fc", "sdk-error: boom");

        Assert.True(decision.Proceed);
        Assert.Null(decision.ErrorCode);
        Assert.Null(decision.Message);
    }

    /// <summary>
    /// K3 对照：同一 Unknown 状态在未授权时**必须**保守拒绝（与上一例共同钉死"开关决定放行"）。
    /// </summary>
    [Fact]
    public void Policy_UnknownWithoutExplicitOverwrite_IsRefused()
    {
        var decision = OverwritePolicy.Decide(OutputExistence.Unknown, false, "C:\\x.gdb\\fc", "sdk-error: boom");

        Assert.False(decision.Proceed);
        Assert.Equal(ErrorCodes.OutputExists, decision.ErrorCode);
    }

    // ------------------------------------------------------------------ 2. 四个写工具的前置闸门

    public static IEnumerable<object[]> GpWriteTools()
    {
        yield return new object[] { "buffer" };
        yield return new object[] { "clip" };
        yield return new object[] { "dissolve" };
        yield return new object[] { "intersect" };
    }

    [Theory]
    [MemberData(nameof(GpWriteTools))]
    public async Task GpWriteTools_RefuseExistingOutput_BeforeRunningGp(string toolName)
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };

        var result = await CallAsync(service, toolName, OutputPath());

        // 拒绝：OUTPUT_EXISTS、无 stateProof（Details 为空）、**未进入 GP**。
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.OutputExists, error.Code);
        Assert.Null(error.Details);
        Assert.Empty(service.Requests);
        Assert.Equal(new[] { OutputPath() }, service.ExistenceChecks);
    }

    [Theory]
    [MemberData(nameof(GpWriteTools))]
    public async Task GpWriteTools_RefuseWhenExistenceIsUnknown(string toolName)
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Unknown, ExistenceDetail = "sdk-error: boom" };

        var result = await CallAsync(service, toolName, OutputPath());

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.OutputExists, error.Code);
        Assert.Contains("could not be determined", error.Message);
        Assert.Empty(service.Requests);
    }

    [Theory]
    [MemberData(nameof(GpWriteTools))]
    public async Task GpWriteTools_RefuseWhenExistenceProbeFails(string toolName)
    {
        var service = new RecordingGeoprocessingService { ExistenceProbeFails = true };

        var result = await CallAsync(service, toolName, OutputPath());

        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    [Theory]
    [MemberData(nameof(GpWriteTools))]
    public async Task GpWriteTools_RunWhenOutputDoesNotExist(string toolName)
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.NotExists };

        var result = await CallAsync(service, toolName, OutputPath());

        Assert.True(result.Success);
        Assert.Single(service.Requests);
        Assert.DoesNotContain("overwrite=true", result.Message ?? string.Empty);
    }

    [Theory]
    [MemberData(nameof(GpWriteTools))]
    public async Task GpWriteTools_OverwriteTrue_AllowsAndAnnotates(string toolName)
    {
        var service = new RecordingGeoprocessingService
        {
            ExistenceResult = OutputExistence.Exists,
            // D-026 F8：预置 stateProof 以验证 overwrite 标记增补（生产路径服务必返 stateProof）。
            Result = OperationResult<GeoprocessingResult>.Ok(new GeoprocessingResult
            {
                ToolName = toolName,
                Result = OutputPath(),
                StateProof = StateProof.ToJson(
                    new StateProof.FileSnapshot(false, null, null),
                    new StateProof.FileSnapshot(true, 100, "AB"),
                    "executed"),
            }),
        };

        var result = await CallAsync(service, toolName, OutputPath(), overwrite: true);

        Assert.True(result.Success);
        Assert.Single(service.Requests);

        // D-026 F8：覆写提示从 OperationResult.Message（不进序列化载荷）迁移到
        // GeoprocessingResult.OverwriteNote 字段，并在 stateProof 内增补 overwrite:true 标记。
        var gp = Assert.IsType<GeoprocessingResult>(result.Data);
        Assert.Contains("overwrite=true", gp.OverwriteNote!);
        Assert.Contains("was replaced", gp.OverwriteNote!);
        Assert.NotNull(gp.StateProof);
        Assert.Contains("\"overwrite\":true", gp.StateProof!.Replace(" ", string.Empty));
    }

    [Theory]
    [MemberData(nameof(GpWriteTools))]
    public async Task GpWriteTools_ExplicitOverwriteFalse_BehavesLikeDefault(string toolName)
    {
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };

        var result = await CallAsync(service, toolName, OutputPath(), overwrite: false);

        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Empty(service.Requests);
    }

    // ------------------------------------------------------------------ 3. 容器语义分支

    [Fact]
    public async Task GpWriteTools_ProbeGdbContainerPathThroughHost()
    {
        // 容器内路径必须经宿主探测（工具层无 SDK，不能自行用文件语义判断）。
        var gdbOutput = @"C:\scratch\d021.gdb\buf_out";
        var service = new RecordingGeoprocessingService { ExistenceResult = OutputExistence.Exists };

        var result = await CallAsync(service, "buffer", gdbOutput);

        Assert.Equal(ErrorCodes.OutputExists, Assert.Single(result.Errors).Code);
        Assert.Equal([gdbOutput], service.ExistenceChecks);
        Assert.Empty(service.Requests);
    }

    // ------------------------------------------------------------------ 4. schema 与工具数

    [Theory]
    [MemberData(nameof(GpWriteTools))]
    public void GpWriteTools_DeclareOverwriteBooleanDefaultingToFalse(string toolName)
    {
        var tool = Create(toolName);
        using var schema = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(tool.InputSchema));
        var properties = schema.RootElement.GetProperty("properties");

        Assert.True(properties.TryGetProperty("overwrite", out var overwrite));
        Assert.Equal("boolean", overwrite.GetProperty("type").GetString());
        Assert.Contains("Default false", overwrite.GetProperty("description").GetString()!);

        // 仅加参数：overwrite 不得成为必填（保证老客户端零改动仍能调用）。
        if (schema.RootElement.TryGetProperty("required", out var required))
        {
            Assert.DoesNotContain("overwrite", required.EnumerateArray().Select(item => item.GetString()!));
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>本单使用的输出路径（容器内，覆盖 D-017 盲区场景）。</summary>
    private static string OutputPath() => @"C:\scratch\d021.gdb\out_fc";

    private static IMCPTool Create(string toolName) => toolName switch
    {
        "buffer" => new BufferTool(),
        "clip" => new ClipTool(),
        "dissolve" => new DissolveTool(),
        "intersect" => new IntersectTool(),
        _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "unsupported gp write tool"),
    };

    private static async Task<OperationResult<object?>> CallAsync(
        RecordingGeoprocessingService service,
        string toolName,
        string output,
        CancellationToken cancellationToken = default)
        => await CallAsync(service, toolName, output, null, cancellationToken);

    private static async Task<OperationResult<object?>> CallAsync(
        RecordingGeoprocessingService service,
        string toolName,
        string output,
        bool? overwrite,
        CancellationToken cancellationToken = default)
    {
        var tool = Create(toolName);
        var arguments = new Dictionary<string, object?>
        {
            ["input"] = @"C:\scratch\d021.gdb\src",
            ["clipFeatures"] = @"C:\scratch\d021.gdb\clip",
            ["inputs"] = @"C:\scratch\d021.gdb\src;C:\scratch\d021.gdb\clip",
            ["output"] = output,
            ["distance"] = 10,
        };

        if (overwrite.HasValue)
        {
            arguments["overwrite"] = overwrite.Value;
        }

        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(
            registry,
            new FakeArcGISHost(service),
            new MCPSettings(),
            NullLogger.Instance);

        return await router.ExecuteAsync(new MCPToolCall
        {
            Name = tool.Name,
            Arguments = arguments,
            CancellationToken = cancellationToken
        });
    }
}
