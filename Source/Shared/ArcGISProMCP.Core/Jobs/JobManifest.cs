using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArcGISProMCP.Core.Jobs;

[JsonConverter(typeof(ArtifactHashJsonConverter))]
public readonly record struct ArtifactHash
{
    public string Value { get; }

    [JsonConstructor]
    public ArtifactHash(string value)
    {
        if (value is null || value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Artifact SHA-256 must contain exactly 64 hexadecimal characters.", nameof(value));
        }

        Value = value.ToUpperInvariant();
    }

    public override string ToString() => Value;

    public static bool TryParse(string? value, out ArtifactHash hash)
    {
        if (value is not null && value.Length == 64 && value.All(Uri.IsHexDigit))
        {
            hash = new ArtifactHash(value);
            return true;
        }

        hash = default;
        return false;
    }
}

[JsonConverter(typeof(JobRevisionJsonConverter))]
public readonly record struct JobRevision
{
    public string Value { get; }

    [JsonConstructor]
    public JobRevision(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
        {
            throw new ArgumentException("A revision must be a non-empty identifier of at most 256 characters.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}

public sealed record JobArtifactReference(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("sha256")] ArtifactHash? Sha256,
    [property: JsonPropertyName("sizeBytes")] long? SizeBytes,
    [property: JsonPropertyName("expectedRevision")] JobRevision? ExpectedRevision);

/// <summary>Typed manifest record serialized in the same compact, lower-camel JSONL style as D-079.</summary>
public sealed record JobManifest(
    [property: JsonPropertyName("t")] string T,
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("jobId")] string JobId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("createdUtc")] string CreatedUtc,
    [property: JsonPropertyName("expectedRevision")] JobRevision? ExpectedRevision,
    [property: JsonPropertyName("fencingGeneration")] long FencingGeneration,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<JobArtifactReference> Artifacts)
{
    public const string SchemaName = "arcgis-pro-mcp-job-manifest-v1";
}

/// <summary>Typed durable result record; unknown outcomes request reconciliation instead of blind retry.</summary>
public sealed record JobReceipt(
    [property: JsonPropertyName("t")] string T,
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("jobId")] string JobId,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("createdUtc")] string CreatedUtc,
    [property: JsonPropertyName("expectedRevision")] JobRevision? ExpectedRevision,
    [property: JsonPropertyName("actualRevision")] JobRevision? ActualRevision,
    [property: JsonPropertyName("reconcileRequired")] bool ReconcileRequired,
    [property: JsonPropertyName("fencingGeneration")] long FencingGeneration,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<JobArtifactReference> Artifacts)
{
    public const string SchemaName = "arcgis-pro-mcp-job-receipt-v1";
}

public sealed class ArtifactHashJsonConverter : JsonConverter<ArtifactHash>
{
    public override ArtifactHash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, ArtifactHash value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

public sealed class JobRevisionJsonConverter : JsonConverter<JobRevision>
{
    public override JobRevision Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, JobRevision value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
