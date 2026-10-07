using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Core.Tools;

/// <summary>
/// Shared invocation boundary. It persists an intent before dispatch, executes once, and records
/// an observed receipt. A cancellation or exception after dispatch is marked for reconciliation;
/// this layer never blindly retries a possibly completed side effect.
/// </summary>
public sealed class ToolInvoker : IToolInvoker
{
    private readonly ToolValidatorPipeline _validator;
    private readonly IToolInvocationJournal? _journal;

    public static IToolInvoker Default { get; } = new ToolInvoker();

    public ToolInvoker(
        ToolValidatorPipeline? validator = null,
        IToolInvocationJournal? journal = null)
    {
        _validator = validator ?? new ToolValidatorPipeline();
        _journal = journal ?? new LoggerToolInvocationJournal();
    }

    public OperationError? Validate(IMCPTool tool, ToolExecutionContext context)
        => _validator.Validate(tool, context);

    public async Task<OperationResult<object?>> InvokeAsync(IMCPTool tool, ToolExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(context);
        var stopwatch = Stopwatch.StartNew();

        if (context.CancellationToken.IsCancellationRequested)
        {
            return Finalize(Cancelled(tool, context), stopwatch.Elapsed, context, null, dispatched: false);
        }

        var validation = Validate(tool, context);
        if (validation is not null)
        {
            return Finalize(OperationResult<object?>.Fail(validation), stopwatch.Elapsed, context, null, dispatched: false);
        }

        var manifest = CreateManifest(tool, context);
        _journal?.RecordIntent(manifest, context.Logger);
        OperationResult<object?> result;
        var dispatched = true;
        var reconcileRequired = false;
        var resultObserved = false;

        try
        {
            result = await tool.ExecuteAsync(context).ConfigureAwait(false);
            resultObserved = result is not null;
            if (result is null)
            {
                result = OperationResult<object?>.Fail(
                    ErrorCodes.InternalError,
                    $"Tool '{tool.Name}' returned no result.");
                reconcileRequired = true;
            }
        }
        catch (OperationCanceledException)
        {
            result = Cancelled(tool, context);
            reconcileRequired = dispatched;
        }
        catch (Exception exception)
        {
            context.Logger.Error($"Tool '{tool.Name}' threw an exception.", requestId: context.RequestId, exception: exception);
            result = OperationResult<object?>.Fail(
                ErrorCodes.InternalError,
                $"Tool '{tool.Name}' failed: {exception.Message}",
                exception.ToString());
            reconcileRequired = dispatched;
        }

        var code = result.Success ? null : result.Errors.FirstOrDefault()?.Code;
        var receipt = new ToolInvocationReceipt(
            manifest.InvocationId,
            context.RequestId,
            tool.Name,
            DateTimeOffset.UtcNow,
            resultObserved,
            result.Success ? "SUCCESS" : reconcileRequired ? "UNKNOWN" : "FAILED",
            code,
            reconcileRequired);
        context.LastInvocationReceipt = receipt;
        _journal?.RecordReceipt(receipt, context.Logger);
        return Finalize(result, stopwatch.Elapsed, context, receipt, dispatched);
    }

    private static ToolInvocationManifest CreateManifest(IMCPTool tool, ToolExecutionContext context)
    {
        string? argumentsHash = null;
        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(context.Arguments ?? new Dictionary<string, object?>());
            argumentsHash = Convert.ToHexString(SHA256.HashData(json));
        }
        catch
        {
            // Argument hashing is evidence metadata. It must not change tool execution semantics.
        }

        return new ToolInvocationManifest(
            Guid.NewGuid().ToString("N"),
            context.RequestId,
            tool.Name,
            DateTimeOffset.UtcNow,
            argumentsHash);
    }

    private static OperationResult<object?> Finalize(
        OperationResult<object?> result,
        TimeSpan elapsed,
        ToolExecutionContext context,
        ToolInvocationReceipt? receipt,
        bool dispatched)
    {
        result.ExecutionTime = elapsed;
        result.RequestId = context.RequestId;
        context.Logger.Log(new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = result.Success ? LogLevel.Information : LogLevel.Warning,
            Category = "router",
            RequestId = context.RequestId,
            Tool = receipt?.ToolName,
            Message = receipt is null ? (dispatched ? "tool invocation failed" : "tool invocation rejected") : "tool call " + receipt.Outcome.ToLowerInvariant(),
            ExecutionTime = elapsed,
            Result = result.Success ? "SUCCESS" : string.Join(";", result.Errors.Select(error => error.Code))
        });
        return result;
    }

    private static OperationResult<object?> Cancelled(IMCPTool tool, ToolExecutionContext context)
        => OperationResult<object?>.Fail(ErrorCodes.Cancelled, $"Tool '{tool.Name}' was cancelled.");

    private sealed class LoggerToolInvocationJournal : IToolInvocationJournal
    {
        public void RecordIntent(ToolInvocationManifest manifest, ILogger logger)
        {
            logger.Log(new LogEntry
            {
                Timestamp = manifest.CreatedAtUtc,
                Level = LogLevel.Information,
                Category = "invocation",
                Component = "ToolInvoker",
                Operation = "intent",
                RequestId = manifest.RequestId,
                Tool = manifest.ToolName,
                Message = "tool invocation intent persisted before dispatch",
                Result = manifest.ArgumentsSha256 ?? "arguments-hash-unavailable"
            });
        }

        public void RecordReceipt(ToolInvocationReceipt receipt, ILogger logger)
        {
            logger.Log(new LogEntry
            {
                Timestamp = receipt.CompletedAtUtc,
                Level = receipt.Outcome == "SUCCESS" ? LogLevel.Information : LogLevel.Warning,
                Category = "invocation",
                Component = "ToolInvoker",
                Operation = "receipt",
                RequestId = receipt.RequestId,
                Tool = receipt.ToolName,
                ErrorCode = receipt.ResultCode,
                Outcome = receipt.Outcome,
                Message = receipt.ReconcileRequired ? "completion is uncertain; reconcile before retry" : "tool invocation receipt recorded",
                Result = receipt.InvocationId
            });
        }
    }
}
