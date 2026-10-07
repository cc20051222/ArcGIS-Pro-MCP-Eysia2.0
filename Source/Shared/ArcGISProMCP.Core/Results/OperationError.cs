namespace ArcGISProMCP.Core.Results;

/// <summary>统一错误对象。</summary>
public sealed class OperationError
{
    public string Code { get; }

    public string Message { get; }

    public string? Details { get; }

    public OperationError(string code, string message, string? details = null)
    {
        Code = code;
        Message = message;
        Details = details;
    }

    public override string ToString() => string.IsNullOrEmpty(Details) ? $"{Code}: {Message}" : $"{Code}: {Message} ({Details})";
}
