namespace ArcGISProMCP.Core.Models;

/// <summary>
/// 不依赖 ArcGIS SDK 的组件事实摘要。Summary 只允许调用方提供已脱敏短文本。
/// </summary>
public sealed record HealthComponent
{
    public HealthComponent(
        string name,
        HealthComponentStatus status,
        bool requiredForOverall = true,
        string? summary = null,
        string? errorCode = null,
        string? declaredTarget = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Component name must not be empty.", nameof(name));
        }

        Name = name;
        Status = status;
        RequiredForOverall = requiredForOverall;
        Summary = summary;
        ErrorCode = errorCode;
        DeclaredTarget = ManagedCompatibilityFacts.IsSafeVersionToken(declaredTarget)
            ? declaredTarget
            : null;
    }

    public string Name { get; }

    public HealthComponentStatus Status { get; }

    public bool RequiredForOverall { get; }

    public string? Summary { get; }

    public string? ErrorCode { get; }

    /// <summary>
    /// A strictly validated declared target version. This is metadata only and
    /// is never a live ArcGIS/SDK compatibility result.
    /// </summary>
    public string? DeclaredTarget { get; }

    public static HealthComponent NotChecked(string name, bool requiredForOverall = true)
        => new(name, HealthComponentStatus.NotChecked, requiredForOverall);
}
