using System.Text.Json;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.UnitTests;

public sealed class DiagnosticRedactionTests
{
    [Fact]
    public void SafeStructuredFieldsAreRetained()
    {
        var result = new LogRedactionPolicy().Sanitize(new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Information,
            Category = "mcp",
            RequestId = "request-1",
            CorrelationId = "correlation-1",
            Client = "codex",
            Tool = "get_dataset_info",
            Component = "server",
            Operation = "handle-request",
            ErrorCode = "OK",
            Outcome = "SUCCESS",
            DurationMs = 12,
            MessageCode = "REQUEST_COMPLETED"
        });

        Assert.Equal(0, result.FailureCount);
        Assert.False(result.Record.RedactionFailed);
        Assert.Equal("REQUEST_COMPLETED", result.Record.MessageCode);
        Assert.Equal("correlation-1", result.Record.CorrelationId);
        Assert.Equal(12, result.Record.DurationMs);
        Assert.Equal("SUCCESS", result.Record.Outcome);
    }

    [Fact]
    public void OperationalToolAndErrorCodeNamesRemainAllowlisted()
    {
        var result = new LogRedactionPolicy().Sanitize(new LogEntry
        {
            Level = LogLevel.Warning,
            Tool = "get_feature_count",
            ErrorCode = "DATASET_NOT_FOUND",
            MessageCode = "REQUEST_FAILED"
        });

        Assert.Equal("get_feature_count", result.Record.Tool);
        Assert.Equal("DATASET_NOT_FOUND", result.Record.ErrorCode);
        Assert.Equal(0, result.FailureCount);
    }

    [Theory]
    [InlineData("sk-proj-1234567890abcdef1234567890abcdef")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyIn0.signature")]
    [InlineData("a9F4xQ7mN2vR8kL5pS1dH6cJ3wE0zY")]
    [InlineData("normal free text must not be persisted")]
    [InlineData("C:\\Users\\Alice\\private.gdb")]
    [InlineData("payload: {\"name\":\"secret\"}")]
    [InlineData("provider=external-model login=Alice")]
    [InlineData("username=Alice account=private")]
    [InlineData("Traceback (most recent call last): token=abc")]
    public void SensitiveMessageVariantsAreDroppedFailClosed(string sensitiveMessage)
    {
        var result = new LogRedactionPolicy().Sanitize(new LogEntry
        {
            Level = LogLevel.Warning,
            Message = sensitiveMessage
        });

        Assert.True(result.Record.RedactionFailed);
        Assert.True(result.FailureCount > 0);
        var json = JsonSerializer.Serialize(result.Record);
        Assert.DoesNotContain("\"message\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("REDACTION_FAILED", result.Record.Flags);
        Assert.DoesNotContain("Alice", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyErrorAndResultNeverBecomeRawStructuredFields()
    {
        var result = new LogRedactionPolicy().Sanitize(new LogEntry
        {
            Level = LogLevel.Error,
            Message = "request failed",
            Error = "System.Exception: C:\\Users\\Alice\\private.gdb\\data",
            Result = "Seattle"
        });

        var json = JsonSerializer.Serialize(result.Record);
        Assert.True(result.Record.RedactionFailed);
        Assert.DoesNotContain("System.Exception", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private.gdb", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Seattle", json, StringComparison.Ordinal);
        Assert.DoesNotContain("credential", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"error\":", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"result\":", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidStructuredIdentifierIsOmittedWithoutOriginalValue()
    {
        var result = new LogRedactionPolicy().Sanitize(new LogEntry
        {
            Level = LogLevel.Information,
            Client = "C:\\Users\\Alice",
            Operation = "provider/model",
            Message = "request completed"
        });

        var json = JsonSerializer.Serialize(result.Record);
        Assert.True(result.Record.RedactionFailed);
        Assert.Null(result.Record.Client);
        Assert.Null(result.Record.Operation);
        Assert.DoesNotContain("Alice", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider/model", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("REDACTION_FAILED", result.Record.Flags);
    }

    [Fact]
    public void OutcomeMustBeAnExplicitControlledValue()
    {
        var result = new LogRedactionPolicy().Sanitize(new LogEntry
        {
            Level = LogLevel.Information,
            Outcome = "Seattle",
            MessageCode = "REQUEST_COMPLETED"
        });

        var json = JsonSerializer.Serialize(result.Record);
        Assert.True(result.Record.RedactionFailed);
        Assert.Null(result.Record.Outcome);
        Assert.DoesNotContain("Seattle", json, StringComparison.Ordinal);
    }
}
