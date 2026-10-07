namespace ArcGISProMCP.Core.Models;

/// <summary>
/// Managed, declared support facts supplied by the Add-in composition root.
/// This is not a live SDK, ArcPy or installed-Pro compatibility probe.
/// </summary>
public sealed record ManagedCompatibilityFacts
{
    public ManagedCompatibilityFacts(
        string? addInVersion,
        string? supportedArcGISProVersion,
        string? targetFramework,
        bool isLiveVerified = false,
        bool? liveCompatibilityMatches = null)
    {
        AddInVersion = addInVersion;
        SupportedArcGISProVersion = supportedArcGISProVersion;
        TargetFramework = targetFramework;
        IsLiveVerified = isLiveVerified;
        LiveCompatibilityMatches = liveCompatibilityMatches;
    }

    public string? AddInVersion { get; }

    public string? SupportedArcGISProVersion { get; }

    public string? TargetFramework { get; }

    /// <summary>
    /// True only when a managed runtime compatibility check supplied these
    /// facts. The static composition declaration intentionally remains false.
    /// </summary>
    public bool IsLiveVerified { get; }

    /// <summary>
    /// Explicit result of a live managed compatibility check. A missing value
    /// cannot be promoted to Pass.
    /// </summary>
    public bool? LiveCompatibilityMatches { get; }

    public bool IsComplete
        => IsSafeVersion(AddInVersion)
           && IsSafeVersion(SupportedArcGISProVersion)
           && IsSafeVersion(TargetFramework);

    public static ManagedCompatibilityFacts Current { get; } =
        new("1.0.2", "3.0", "net6.0");

    public static bool IsSafeVersionToken(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Length <= 32
           && value.All(IsSafeVersionCharacter);

    private static bool IsSafeVersionCharacter(char character)
        => character is (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z')
            or (>= '0' and <= '9')
            or '.' or '-' or '+';

    private static bool IsSafeVersion(string? value)
        => IsSafeVersionToken(value);
}
