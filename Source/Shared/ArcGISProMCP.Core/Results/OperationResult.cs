namespace ArcGISProMCP.Core.Results;

/// <summary>
/// 统一操作结果模型。
/// </summary>
public sealed class OperationResult<T>
{
    public bool Success { get; }

    public T? Data { get; }

    public string? Message { get; set; }

    public IReadOnlyList<OperationError> Warnings { get; set; } = Array.Empty<OperationError>();

    public IReadOnlyList<OperationError> Errors { get; } = Array.Empty<OperationError>();

    public TimeSpan ExecutionTime { get; set; }

    public string? RequestId { get; set; }

    public bool IsError => !Success;

    public OperationResult(bool success, T? data, IReadOnlyList<OperationError>? errors = null)
    {
        Success = success;
        Data = data;
        if (errors is { Count: > 0 })
        {
            Errors = errors;
        }
    }

    public static OperationResult<T> Ok(T data, string? message = null)
        => new(true, data) { Message = message };

    public static OperationResult<T> Fail(OperationError error)
        => new(false, default, new[] { error });

    public static OperationResult<T> Fail(string code, string message, string? details = null)
        => new(false, default, new[] { new OperationError(code, message, details) });

    public static OperationResult<T> Fail(IReadOnlyList<OperationError> errors)
        => new(false, default, errors);
}
