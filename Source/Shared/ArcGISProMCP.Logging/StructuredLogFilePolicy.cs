namespace ArcGISProMCP.Logging;

/// <summary>
/// Structured sink policy. The directory and manifest are owned by the caller;
/// the sink never manages files outside the manifest allowlist.
/// </summary>
public sealed record StructuredLogFilePolicy
{
    public const string ManifestSchema = "arcgis-pro-mcp-managed-log-manifest-v1";
    public const string Owner = "ArcGISProMCP.StructuredFileLogger";

    public string FilePrefix { get; init; } = "mcp-structured-";

    public string FileExtension { get; init; } = ".jsonl";

    public string ManifestFileName { get; init; } = "structured-log.manifest.json";

    public long MaxFileBytes { get; init; } = 1024 * 1024;

    public TimeSpan MaxFileAge { get; init; } = TimeSpan.FromHours(24);

    public int MaxRetainedFiles { get; init; } = 5;

    public long MaxRetainedBytes { get; init; } = 10 * 1024 * 1024;

    public int MaxMessageLength { get; init; } = 512;

    internal void Validate()
    {
        if (!IsSafeNamePart(FilePrefix) || FilePrefix.EndsWith(".", StringComparison.Ordinal))
        {
            throw new ArgumentException("FilePrefix must be a relative safe name prefix.", nameof(FilePrefix));
        }

        if (FileExtension is not ".jsonl")
        {
            throw new ArgumentException("Structured logs must use the .jsonl extension.", nameof(FileExtension));
        }

        if (!IsSafeFileName(ManifestFileName) || ManifestFileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("ManifestFileName must be a standalone relative file name.", nameof(ManifestFileName));
        }

        if (MaxFileBytes < 128)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFileBytes), "MaxFileBytes must leave room for one structured record.");
        }

        if (MaxFileAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFileAge));
        }

        if (MaxRetainedFiles < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRetainedFiles));
        }

        if (MaxRetainedBytes < MaxFileBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRetainedBytes), "Retention bytes must be at least MaxFileBytes.");
        }
    }

    internal string FileName(int sequence)
        => FilePrefix + sequence.ToString("D8", System.Globalization.CultureInfo.InvariantCulture) + FileExtension;

    internal bool IsManagedFileName(string fileName)
        => IsSafeFileName(fileName)
           && fileName.StartsWith(FilePrefix, StringComparison.Ordinal)
           && fileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeFileName(string value)
        => !string.IsNullOrWhiteSpace(value)
           && value == Path.GetFileName(value)
           && value is not "." and not ".."
           && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.');

    private static bool IsSafeNamePart(string value)
        => !string.IsNullOrWhiteSpace(value)
           && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-');
}
