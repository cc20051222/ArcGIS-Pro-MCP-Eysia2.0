using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Tools;

/// <summary>Runs a tool through the shared validation, execution, and receipt pipeline.</summary>
public interface IToolInvoker
{
    OperationError? Validate(IMCPTool tool, ToolExecutionContext context);

    Task<OperationResult<object?>> InvokeAsync(IMCPTool tool, ToolExecutionContext context);
}

/// <summary>Optional centralized validation for output paths declared by a tool.</summary>
public interface IToolPathArgumentValidator
{
    OperationError? Validate(IMCPTool tool, IReadOnlyDictionary<string, object?>? arguments);
}

/// <summary>Durable or observable intent and completion record for a tool invocation.</summary>
public interface IToolInvocationJournal
{
    void RecordIntent(ToolInvocationManifest manifest, ArcGISProMCP.Logging.ILogger logger);

    void RecordReceipt(ToolInvocationReceipt receipt, ArcGISProMCP.Logging.ILogger logger);
}

public sealed record ToolInvocationManifest(
    string InvocationId,
    string? RequestId,
    string ToolName,
    DateTimeOffset CreatedAtUtc,
    string? ArgumentsSha256);

public sealed record ToolInvocationReceipt(
    string InvocationId,
    string? RequestId,
    string ToolName,
    DateTimeOffset CompletedAtUtc,
    bool ResultObserved,
    string Outcome,
    string? ResultCode,
    bool ReconcileRequired);
