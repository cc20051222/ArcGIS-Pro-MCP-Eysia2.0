namespace ArcGISProMCP.Logging;

public enum StructuredLogHealthStatus
{
    Healthy,
    Degraded
}

/// <summary>不含绝对路径的 structured sink 运行状态。</summary>
public sealed record StructuredLogHealth
{
    public StructuredLogHealthStatus Status { get; init; }

    public string? ActiveFileName { get; init; }

    public int ManagedFileCount { get; init; }

    public long DroppedCount { get; init; }

    public long RedactionFailureCount { get; init; }

    public string? LastFailureCode { get; init; }

    public string? LastFailureMessage { get; init; }

    public DateTimeOffset? LastFailureAtUtc { get; init; }

    public string RedactionPolicyVersion { get; init; } = LogRedactionPolicy.PolicyVersion;
}
