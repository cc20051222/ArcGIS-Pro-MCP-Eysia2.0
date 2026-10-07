namespace ArcGISProMCP.Security;

public enum SecurityDecision
{
    Allowed,
    SecurityBlocked
}

/// <summary>安全检查结果。</summary>
public sealed class SecurityCheckResult
{
    public SecurityDecision Decision { get; set; }
    public string? Reason { get; set; }
}
