namespace ArcGISProMCP.Core;

/// <summary>通用操作结果（无返回值）。</summary>
public sealed class Result
{
    public bool Success { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool success, string? error, string? errorCode)
    {
        Success = success;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result Ok() => new(true, null, null);

    public static Result Fail(string error, string? errorCode = null) => new(false, error, errorCode);
}

/// <summary>通用操作结果（带返回值）。</summary>
public sealed class Result<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool success, T? value, string? error, string? errorCode)
    {
        Success = success;
        Value = value;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result<T> Ok(T value) => new(true, value, null, null);

    public static Result<T> Fail(string error, string? errorCode = null) => new(false, default, error, errorCode);
}
