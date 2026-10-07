using System.Text.Json;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-056 C2（13.2 挂账兑现；G-147 登记「掩码能力已具备但未接线」→ G-149 批准接线）行为测试。
/// <para>
/// **契约面语义变化（须在冻结文档显著披露）**：
/// 命中绝对路径形态且**掩码确实生效**（用户主目录账户段被替换为 <c>%USERPROFILE%</c>）的**标识符字段**，
/// 由「丢弃」改为「**掩码保留**」—— 该字段不再计入 <c>RedactionFailed</c>（该标志现仅表示「有字段被拒绝/丢弃」），
/// 并新增 <c>RedactionMaskedCount</c> 与 <c>REDACTION_MASKED</c> 标记以便审计区分。
/// </para>
/// <para>**未放宽**：不含主目录段的绝对路径、敏感标识符、超长值、非法字符仍一律丢弃（fail-closed 不变）。</para>
/// </summary>
public sealed class D056MaskWiringTests
{
    private static string Json(SanitizedLogRecord record) => JsonSerializer.Serialize(record);

    [Fact]
    public void UserProfilePathIdentifier_IsMaskedAndRetained_NotCountedAsFailure()
    {
        var result = new LogRedactionPolicy().Sanitize(new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Information,
            Component = @"C:\Users\alice\proj\arcgis\thing.dll",
            MessageCode = "PRODUCTION_EVENT"
        });

        Assert.Equal(0, result.FailureCount);
        Assert.False(result.Record.RedactionFailed);
        Assert.Equal(1, result.MaskedCount);
        Assert.Equal(1, result.Record.RedactionMaskedCount);
        Assert.Equal(@"C:\Users\%USERPROFILE%\proj\arcgis\thing.dll", result.Record.Component);

        var json = Json(result.Record);
        Assert.Contains("%USERPROFILE%", json, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("REDACTION_MASKED", result.Record.Flags);
        Assert.DoesNotContain("REDACTION_FAILED", result.Record.Flags);
    }

    [Fact]
    public void AbsolutePathWithoutUserProfileSegment_IsStillDroppedFailClosed()
    {
        // **不放宽**：非用户主目录的绝对路径仍按既有策略丢弃（否则等于把任意路径放进日志）
        var result = new LogRedactionPolicy().Sanitize(new LogEntry
        {
            Level = LogLevel.Information,
            Component = @"D:\ArcGIS-Pro-MCP 2.0\.runtime\logs\mcp-structured-1.jsonl",
            MessageCode = "PRODUCTION_EVENT"
        });

        Assert.True(result.Record.RedactionFailed);
        Assert.True(result.FailureCount > 0);
        Assert.Equal(0, result.MaskedCount);
        Assert.Null(result.Record.Component);
        Assert.Contains("REDACTION_FAILED", result.Record.Flags);
        Assert.DoesNotContain("REDACTION_MASKED", result.Record.Flags);
    }

    [Fact]
    public void UserProfilePathWithoutTrailingSeparator_IsStillDropped()
    {
        // 边界：账户段后无分隔符 ⇒ 掩码器不作用于该形态（避免凭空造段）⇒ 维持丢弃（既有契约不变）
        var result = new LogRedactionPolicy().Sanitize(new LogEntry
        {
            Level = LogLevel.Information,
            Client = @"C:\Users\alice",
            MessageCode = "PRODUCTION_EVENT"
        });

        Assert.True(result.Record.RedactionFailed);
        Assert.Null(result.Record.Client);
        Assert.Equal(0, result.MaskedCount);
        Assert.DoesNotContain("alice", Json(result.Record), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MaskedIdentifier_SerializedRecord_IsAllowlistOnlyAndMaskCounted()
    {
        var result = new LogRedactionPolicy().Sanitize(new LogEntry
        {
            Level = LogLevel.Warning,
            Tool = "get_dataset_info",
            Operation = @"/Users/bob/work/out.gdb",
            Outcome = "DEGRADED",
            MessageCode = "REQUEST_FAILED"
        });

        var json = Json(result.Record);
        Assert.Equal("/Users/%USERPROFILE%/work/out.gdb", result.Record.Operation);
        Assert.Equal(1, result.Record.RedactionMaskedCount);
        Assert.False(result.Record.RedactionFailed);
        Assert.DoesNotContain("bob", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"redactionMaskedCount\":1", json, StringComparison.Ordinal);
    }
}
