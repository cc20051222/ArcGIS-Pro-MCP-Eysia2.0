using ArcGISProMCP.Core.PythonBridge;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 8.4 派生（D-018 A 节）：bridge stdout JSON 帧污染隔离。
/// 1) 证明历史故障机制：非有限浮点（NaN）字面量 → 严格解析必然失败（不得改用容忍式解析掩盖）；
/// 2) 证明修复后契约：不可得数值降级为 null 后帧合法且可解析。
/// </summary>
public sealed class PythonBridgeFrameIsolationTests
{
    private const string NanFrame =
        "{\"id\":\"t2\",\"ok\":true,\"result\":{\"path\":\"D:\\\\ws.gdb\\\\EMPTY\",\"featureCount\":0,"
        + "\"extent\":{\"xMin\": NaN,\"yMin\": NaN,\"xMax\": NaN,\"yMax\": NaN}}}";

    private const string SanitizedFrame =
        "{\"id\":\"t2\",\"ok\":true,\"result\":{\"path\":\"D:\\\\ws.gdb\\\\EMPTY\",\"featureCount\":0,"
        + "\"extent\":{\"xMin\":null,\"yMin\":null,\"xMax\":null,\"yMax\":null}}}";

    [Fact]
    public void TryParse_NonFiniteFloatFrame_IsRejected_StrictJsonMaintained()
    {
        // 修复前：空数据集 extent 为 NaN，json.dumps 输出裸 NaN → 帧不可解析 → 曾被升级为"传输不可用"。
        Assert.Null(PythonBridgeResponse.TryParse(NanFrame));
    }

    [Fact]
    public void TryParse_SanitizedFrame_NullExtent_IsAccepted()
    {
        var parsed = PythonBridgeResponse.TryParse(SanitizedFrame);
        Assert.NotNull(parsed);
        Assert.Equal("t2", parsed!.Id);
        Assert.True(parsed.IsOk);
        Assert.NotNull(parsed.Result);
    }

    [Fact]
    public void TryParse_InfinityLiteral_IsRejected()
    {
        var frame = "{\"id\":\"t3\",\"ok\":true,\"result\":{\"mean\": Infinity}}";
        Assert.Null(PythonBridgeResponse.TryParse(frame));
    }

    [Fact]
    public void TryParse_ValidFrameWithNulls_PreservesNullSemantics()
    {
        var frame = "{\"id\":\"t4\",\"ok\":true,\"result\":{\"statistics\":\"unknown\",\"pixelSize\":null}}";
        var parsed = PythonBridgeResponse.TryParse(frame);
        Assert.NotNull(parsed);
        Assert.True(parsed!.IsOk);
    }

    [Fact]
    public void TryParse_ErrorFrame_CarriesDatasetLevelCode()
    {
        // 数据集级错误（arcpy 抛错）走既有 PYTHON_EXECUTION_ERROR 语义码，不是传输不可用码。
        var frame = "{\"id\":\"t5\",\"ok\":false,\"error\":{\"code\":\"PYTHON_EXECUTION_ERROR\","
                    + "\"message\":\"Cannot find field\",\"details\":\"arcpy traceback\"}}";
        var parsed = PythonBridgeResponse.TryParse(frame);
        Assert.NotNull(parsed);
        Assert.False(parsed!.IsOk);
        Assert.Equal("PYTHON_EXECUTION_ERROR", parsed.Error!.Code);
        Assert.Contains("arcpy traceback", parsed.Error.Details);
    }
}
