using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Core.Services;

public interface IDiagnosticExportService
{
    DiagnosticExportResult Export(string destinationDirectory, HealthSnapshot snapshot);
}

public enum DiagnosticExportStatus
{
    Success,
    Degraded,
    Failed
}

/// <summary>Safe result for the UI; it deliberately carries no filesystem path.</summary>
public sealed record DiagnosticExportResult(
    DiagnosticExportStatus Status,
    string ErrorCode,
    int ArtifactCount,
    int ManagedLogRecordCount)
{
    public bool Succeeded => Status is DiagnosticExportStatus.Success or DiagnosticExportStatus.Degraded;

    public string UserMessage
        => Status switch
        {
            DiagnosticExportStatus.Success => "Diagnostic export completed.",
            DiagnosticExportStatus.Degraded => "Diagnostic export completed with limited managed-log data.",
            _ => "Diagnostic export could not be created."
        };
}

/// <summary>
/// Builds a small, privacy-safe diagnostic directory. The source managed-log
/// root is read-only; legacy runtime files and unknown files are never copied.
/// </summary>
public sealed class DiagnosticExportService : IDiagnosticExportService
{
    public const string ExportManifestSchema = "arcgis-pro-mcp-diagnostic-export-manifest-v1";
    public const string ExportPolicyVersion = "arcgis-pro-mcp-diagnostic-export-v1";
    public const string HealthArtifactName = "health-snapshot.json";
    public const string ManagedLogArtifactName = "managed-log.jsonl";
    public const string ExportManifestName = "manifest.json";
    public const int MaxManagedLogRecords = 2048;
    public const int MaxManagedLogBytes = 4 * 1024 * 1024;
    public const int MaxHealthSnapshotBytes = 256 * 1024;
    public const int MaxSourceManifestBytes = 256 * 1024;
    public const int MaxSourceManagedFiles = 64;
    public const int MaxSourceRecordBytes = 64 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> OwnedArtifactNames = new(StringComparer.Ordinal)
    {
        HealthArtifactName,
        ManagedLogArtifactName,
        ExportManifestName
    };

    private static readonly HashSet<string> KnownErrorCodes = new(StringComparer.Ordinal)
    {
        "NONE",
        "SERVER_STATE_NOT_CHECKED",
        "COMPATIBILITY_NOT_CHECKED",
        "COMPATIBILITY_MISMATCH",
        "ARCGIS_HOST_CONTEXT_UNAVAILABLE",
        "ARCGIS_HOST_CONTEXT_NOT_CHECKED",
        "PYTHON_BRIDGE_UNAVAILABLE",
        "PYTHON_TIMEOUT",
        "PYTHON_OUTPUT_LIMIT_EXCEEDED",
        "PYTHON_PROTOCOL_ERROR",
        "INTERNAL_ERROR",
        "BRIDGE_STATE_UNKNOWN",
        "LOGGING_DEGRADED",
        "MANAGED_LOG_UNAVAILABLE",
        "MANAGED_LOG_RECORD_INVALID",
        "MANAGED_LOG_BOUNDS_EXCEEDED"
    };

    private readonly string _ownedLogDirectory;
    private readonly StructuredLogFilePolicy _logPolicy;
    private readonly Action<string, string>? _afterArtifactWritten;

    public DiagnosticExportService(
        string ownedLogDirectory,
        StructuredLogFilePolicy? logPolicy = null,
        Action<string, string>? afterArtifactWritten = null)
    {
        if (string.IsNullOrWhiteSpace(ownedLogDirectory) || !Path.IsPathRooted(ownedLogDirectory))
        {
            throw new ArgumentException("An absolute owned managed-log directory is required.", nameof(ownedLogDirectory));
        }

        _ownedLogDirectory = Path.GetFullPath(ownedLogDirectory);
        _logPolicy = logPolicy ?? new StructuredLogFilePolicy();
        _afterArtifactWritten = afterArtifactWritten;
    }

    public DiagnosticExportResult Export(string destinationDirectory, HealthSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!TryNormalizeDestination(destinationDirectory, out var destination, out var destinationError))
        {
            return Failed(destinationError);
        }

        var parent = Directory.GetParent(destination);
        if (parent is null
            || !Directory.Exists(parent.FullName)
            || HasReparsePointInAncestorChain(parent.FullName))
        {
            return Failed("DESTINATION_PARENT_UNAVAILABLE");
        }

        if (File.Exists(destination) || Directory.Exists(destination))
        {
            return Failed("DESTINATION_EXISTS");
        }

        var temporary = Path.Combine(
            parent.FullName,
            ".arcgis-pro-mcp-diagnostic-" + Guid.NewGuid().ToString("N") + ".writing");
        var published = false;
        try
        {
            Directory.CreateDirectory(temporary);
            if (HasReparsePointInAncestorChain(temporary))
            {
                return Failed("EXPORT_TEMP_REPARSE_POINT");
            }

            var healthBytes = SerializeHealthSnapshot(snapshot);
            if (healthBytes.Length > MaxHealthSnapshotBytes)
            {
                return Failed("HEALTH_SNAPSHOT_TOO_LARGE");
            }

            var logRead = ReadManagedLogs();
            var logBytes = SerializeManagedLogs(logRead.Records);
            if (logBytes.Length > MaxManagedLogBytes)
            {
                return Failed("MANAGED_LOG_BOUNDS_EXCEEDED");
            }

            WriteNewFile(temporary, HealthArtifactName, healthBytes);
            NotifyArtifactWritten(temporary, HealthArtifactName);
            WriteNewFile(temporary, ManagedLogArtifactName, logBytes);
            NotifyArtifactWritten(temporary, ManagedLogArtifactName);

            var artifacts = new[]
            {
                CreateArtifact(HealthArtifactName, healthBytes),
                CreateArtifact(ManagedLogArtifactName, logBytes)
            };
            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(
                new ExportManifest
                {
                    Schema = ExportManifestSchema,
                    PolicyVersion = ExportPolicyVersion,
                    GeneratedAtUtc = DateTimeOffset.UtcNow,
                    Artifacts = artifacts
                },
                JsonOptions);
            WriteNewFile(temporary, ExportManifestName, manifestBytes);
            NotifyArtifactWritten(temporary, ExportManifestName);

            if (File.Exists(destination) || Directory.Exists(destination))
            {
                return Failed("DESTINATION_EXISTS");
            }

            Directory.Move(temporary, destination);
            published = true;
            return new DiagnosticExportResult(
                logRead.Degraded ? DiagnosticExportStatus.Degraded : DiagnosticExportStatus.Success,
                logRead.Degraded ? logRead.ErrorCode : "NONE",
                ArtifactCount: 3,
                ManagedLogRecordCount: logRead.Records.Count);
        }
        catch (IOException)
        {
            return Failed("EXPORT_IO_FAILED");
        }
        catch (UnauthorizedAccessException)
        {
            return Failed("EXPORT_ACCESS_DENIED");
        }
        catch (JsonException)
        {
            return Failed("EXPORT_SERIALIZATION_FAILED");
        }
        catch (ArgumentException)
        {
            return Failed("EXPORT_ARGUMENT_INVALID");
        }
        catch (Exception)
        {
            return Failed("EXPORT_FAILED");
        }
        finally
        {
            if (!published)
            {
                TryDeleteOwnedTemporary(temporary, parent.FullName);
            }
        }
    }

    private static DiagnosticExportResult Failed(string errorCode)
        => new(DiagnosticExportStatus.Failed, errorCode, 0, 0);

    private bool TryNormalizeDestination(
        string? requested,
        out string destination,
        out string errorCode)
    {
        destination = string.Empty;
        errorCode = "DESTINATION_INVALID";
        if (string.IsNullOrWhiteSpace(requested)
            || !Path.IsPathRooted(requested)
            || ContainsTraversalSegment(requested))
        {
            return false;
        }

        try
        {
            destination = Path.GetFullPath(requested);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (string.Equals(destination, _ownedLogDirectory, StringComparison.OrdinalIgnoreCase)
            || IsDescendant(destination, _ownedLogDirectory))
        {
            errorCode = "DESTINATION_NOT_OWNED";
            return false;
        }

        return true;
    }

    private ManagedLogReadResult ReadManagedLogs()
    {
        var records = new List<SanitizedLogRecord>();
        var degraded = false;
        var errorCode = "NONE";

        if (!Directory.Exists(_ownedLogDirectory)
            || HasReparsePointInAncestorChain(_ownedLogDirectory))
        {
            return DegradedLog("MANAGED_LOG_UNAVAILABLE");
        }

        var manifestPath = Path.Combine(_ownedLogDirectory, _logPolicy.ManifestFileName);
        if (!File.Exists(manifestPath)
            || HasReparsePointInAncestorChain(manifestPath))
        {
            return DegradedLog("MANAGED_LOG_UNAVAILABLE");
        }

        try
        {
            if (new FileInfo(manifestPath).Length > MaxSourceManifestBytes)
            {
                return DegradedLog("MANAGED_LOG_BOUNDS_EXCEEDED");
            }
        }
        catch
        {
            return DegradedLog("MANAGED_LOG_UNAVAILABLE");
        }

        SourceManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<SourceManifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
        }
        catch
        {
            return DegradedLog("MANAGED_LOG_UNAVAILABLE");
        }

        if (manifest is null
            || manifest.Schema != StructuredLogFilePolicy.ManifestSchema
            || manifest.Owner != StructuredLogFilePolicy.Owner
            || manifest.FilePrefix != _logPolicy.FilePrefix
            || manifest.ManagedFiles is null
            || manifest.FileCreatedUtc is null
            || manifest.ManagedFiles.Count > MaxSourceManagedFiles
            || manifest.ManagedFiles.Distinct(StringComparer.Ordinal).Count() != manifest.ManagedFiles.Count
            || manifest.ManagedFiles.Any(file => !IsManagedFileName(file))
            || manifest.FileCreatedUtc.Count != manifest.ManagedFiles.Count
            || manifest.FileCreatedUtc.Keys.Any(file => !manifest.ManagedFiles.Contains(file, StringComparer.Ordinal)))
        {
            return DegradedLog("MANAGED_LOG_UNAVAILABLE");
        }

        foreach (var fileName in manifest.ManagedFiles)
        {
            if (records.Count >= MaxManagedLogRecords)
            {
                degraded = true;
                errorCode = "MANAGED_LOG_BOUNDS_EXCEEDED";
                break;
            }

            var path = Path.Combine(_ownedLogDirectory, fileName);
            if (!IsDescendant(path, _ownedLogDirectory)
                || !File.Exists(path)
                || HasReparsePointInAncestorChain(path))
            {
                degraded = true;
                errorCode = "MANAGED_LOG_UNAVAILABLE";
                continue;
            }

            try
            {
                var info = new FileInfo(path);
                if (info.Length > _logPolicy.MaxFileBytes)
                {
                    degraded = true;
                    errorCode = "MANAGED_LOG_BOUNDS_EXCEEDED";
                    continue;
                }

                using var reader = new StreamReader(
                    path,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                    detectEncodingFromByteOrderMarks: true);
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (Encoding.UTF8.GetByteCount(line) > MaxSourceRecordBytes)
                    {
                        degraded = true;
                        errorCode = "MANAGED_LOG_BOUNDS_EXCEEDED";
                        continue;
                    }

                    if (!TryReadSafeRecord(line, out var record))
                    {
                        degraded = true;
                        errorCode = "MANAGED_LOG_RECORD_INVALID";
                        continue;
                    }

                    var candidateBytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
                    var currentBytes = records.Sum(SerializedRecordLength);
                    if (records.Count >= MaxManagedLogRecords
                        || currentBytes + candidateBytes.Length + 1 > MaxManagedLogBytes)
                    {
                        degraded = true;
                        errorCode = "MANAGED_LOG_BOUNDS_EXCEEDED";
                        break;
                    }

                    records.Add(record);
                }
            }
            catch
            {
                degraded = true;
                errorCode = "MANAGED_LOG_UNAVAILABLE";
            }
        }

        if (degraded || records.Count == 0)
        {
            var marker = DegradedRecord(errorCode == "NONE" ? "MANAGED_LOG_UNAVAILABLE" : errorCode);
            if (records.Count < MaxManagedLogRecords
                && SerializedRecordLength(marker) + records.Sum(SerializedRecordLength) + 1 <= MaxManagedLogBytes)
            {
                records.Add(marker);
            }
        }

        return new ManagedLogReadResult(records, degraded, errorCode == "NONE" ? "MANAGED_LOG_UNAVAILABLE" : errorCode);
    }

    private static bool TryReadSafeRecord(string line, out SanitizedLogRecord record)
    {
        record = new SanitizedLogRecord();
        try
        {
            var parsed = JsonSerializer.Deserialize<SanitizedLogRecord>(line, JsonOptions);
            if (parsed is null || parsed.Schema != "arcgis-pro-mcp-log-record-v1"
                || !Enum.TryParse<LogLevel>(parsed.Level, ignoreCase: false, out var level))
            {
                return false;
            }

            var normalized = ProductionLogEntryNormalizer.Normalize(new LogEntry
            {
                Timestamp = parsed.TimestampUtc,
                Level = level,
                Category = parsed.Category,
                RequestId = parsed.RequestId,
                CorrelationId = parsed.CorrelationId,
                Client = parsed.Client,
                Tool = parsed.Tool,
                Component = parsed.Component,
                Operation = parsed.Operation,
                ErrorCode = parsed.ErrorCode,
                Outcome = parsed.Outcome,
                DurationMs = parsed.DurationMs,
                MessageCode = parsed.MessageCode
            });
            var safe = new LogRedactionPolicy().Sanitize(normalized);
            record = safe.Record with
            {
                RedactionFailed = parsed.RedactionFailed,
                RedactionFailureCount = parsed.RedactionFailed
                    ? Math.Max(1, parsed.RedactionFailureCount)
                    : safe.FailureCount,
                Flags = parsed.RedactionFailed
                    ? new[] { "REDACTION_FAILED" }
                    : Array.Empty<string>()
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ManagedLogReadResult DegradedLog(string code)
        => new(new[] { DegradedRecord(code) }, true, code);

    private static SanitizedLogRecord DegradedRecord(string code)
        => LogRedactionPolicyRecord(
            LogLevel.Warning,
            "diagnostic-export",
            "read-managed-log",
            "DIAGNOSTIC_EXPORT_DEGRADED",
            code,
            "DEGRADED");

    private static SanitizedLogRecord LogRedactionPolicyRecord(
        LogLevel level,
        string component,
        string operation,
        string messageCode,
        string errorCode,
        string outcome)
        => new LogRedactionPolicy().Sanitize(new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Category = component,
            Component = component,
            Operation = operation,
            CorrelationId = "diagnostic-export",
            MessageCode = messageCode,
            ErrorCode = errorCode,
            Outcome = outcome
        }).Record;

    private static byte[] SerializeHealthSnapshot(HealthSnapshot snapshot)
        => JsonSerializer.SerializeToUtf8Bytes(ToExportSnapshot(snapshot), JsonOptions);

    private static byte[] SerializeManagedLogs(IReadOnlyList<SanitizedLogRecord> records)
    {
        using var stream = new MemoryStream();
        foreach (var record in records)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
            stream.Write(bytes);
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static ExportHealthSnapshot ToExportSnapshot(HealthSnapshot snapshot)
        => new()
        {
            Schema = HealthSnapshot.SchemaName,
            GeneratedAtUtc = snapshot.GeneratedAtUtc.ToUniversalTime(),
            ProductIdentity = ManagedCompatibilityFacts.IsSafeVersionToken(snapshot.ProductIdentity)
                ? snapshot.ProductIdentity
                : null,
            OverallStatus = snapshot.OverallStatus.ToString(),
            RecommendedAction = HealthStatusPolicy.RecommendedAction(snapshot.OverallStatus),
            Server = new ExportServerFact
            {
                Status = snapshot.Server.Status.ToString(),
                ListenerFactExplicit = snapshot.Server.ListenerFactExplicit,
                CanonicalEndpoint = string.Equals(snapshot.Server.Host, "127.0.0.1", StringComparison.Ordinal)
                    && snapshot.Server.Port == 6520
                    && string.Equals(snapshot.Server.Endpoint, "/mcp", StringComparison.Ordinal),
                ErrorCode = SafeErrorCode(snapshot.Server.ErrorCode)
            },
            Components = new[]
            {
                ToExportComponent("arcgisHost", snapshot.ArcGISHost),
                ToExportComponent("configuration", snapshot.Configuration),
                ToExportComponent("compatibility", snapshot.Compatibility),
                ToExportComponent("bridge", snapshot.Bridge),
                ToExportComponent("logging", snapshot.Logging)
            },
            ConnectedClients = new ExportConnectedClients
            {
                Status = HealthComponentStatus.NotTracked.ToString(),
                Display = "N/A"
            },
            Clients = (snapshot.Clients ?? Array.Empty<ClientHealthFact>())
                .Select(ToExportClient)
                .Where(client => client is not null)
                .Cast<ExportClient>()
                .ToArray()
        };

    private static ExportComponent ToExportComponent(string name, HealthComponent component)
        => new()
        {
            Name = name,
            Status = component.Status.ToString(),
            RequiredForOverall = component.RequiredForOverall,
            DeclaredTarget = name == "compatibility"
                && ManagedCompatibilityFacts.IsSafeVersionToken(component.DeclaredTarget)
                ? component.DeclaredTarget
                : null,
            ErrorCode = SafeErrorCode(component.ErrorCode)
        };

    private static ExportClient? ToExportClient(ClientHealthFact client)
    {
        if (client is null)
        {
            return null;
        }

        var allowed = client.ClientId switch
        {
            "codex" => "codex",
            "cursor" => "cursor",
            "deepseek-harness" => "deepseek-harness",
            "claude-desktop" => "claude-desktop",
            _ => null
        };
        return allowed is null
            ? null
            : new ExportClient { ClientId = allowed, Status = client.Status.ToString(), Optional = client.Optional };
    }

    private static string? SafeErrorCode(string? value)
        => value is not null && KnownErrorCodes.Contains(value) ? value : null;

    private static ExportArtifact CreateArtifact(string name, byte[] bytes)
        => new()
        {
            Name = name,
            ByteLength = bytes.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes))
        };

    private static long SerializedRecordLength(SanitizedLogRecord record)
        => JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions).LongLength;

    private static void WriteNewFile(string directory, string fileName, byte[] content)
    {
        var path = Path.Combine(directory, fileName);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(content, 0, content.Length);
        stream.Flush(flushToDisk: true);
    }

    private void NotifyArtifactWritten(string temporary, string artifactName)
        => _afterArtifactWritten?.Invoke(temporary, artifactName);

    private static bool IsManagedFileName(string fileName)
        => !string.IsNullOrWhiteSpace(fileName)
           && fileName == Path.GetFileName(fileName)
           && fileName.StartsWith("mcp-structured-", StringComparison.Ordinal)
           && fileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
           && fileName.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.');

    private static bool ContainsTraversalSegment(string path)
        => path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or "..");

    private static bool IsDescendant(string candidate, string root)
    {
        var fullCandidate = Path.GetFullPath(candidate);
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var relative = Path.GetRelativePath(fullRoot, fullCandidate);
        return relative is not "."
            && !relative.StartsWith("..", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static bool HasReparsePointInAncestorChain(string path)
    {
        try
        {
            var current = Path.GetFullPath(path);
            while (true)
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        return true;
                    }
                }
                catch (FileNotFoundException)
                {
                    // A not-yet-created leaf is safe; inspect its ancestors.
                }
                catch (DirectoryNotFoundException)
                {
                    // A not-yet-created leaf is safe; inspect its ancestors.
                }

                var parent = Directory.GetParent(current);
                if (parent is null
                    || string.Equals(parent.FullName, current, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                current = parent.FullName;
            }
        }
        catch
        {
            // Fail closed when an existing ancestor cannot be inspected.
            return true;
        }
    }

    private static void TryDeleteOwnedTemporary(string temporary, string parent)
    {
        try
        {
            if (!Directory.Exists(temporary)
                || !IsDescendant(temporary, parent)
                || !Path.GetFileName(temporary).StartsWith(".arcgis-pro-mcp-diagnostic-", StringComparison.Ordinal)
                || HasReparsePointInAncestorChain(parent)
                || HasReparsePointInAncestorChain(temporary))
            {
                return;
            }

            foreach (var entry in Directory.GetFileSystemEntries(temporary))
            {
                if (!OwnedArtifactNames.Contains(Path.GetFileName(entry))
                    || Directory.Exists(entry)
                    || IsReparsePoint(entry))
                {
                    return;
                }
            }

            foreach (var artifactName in OwnedArtifactNames)
            {
                var path = Path.Combine(temporary, artifactName);
                if (!File.Exists(path))
                {
                    continue;
                }

                if (HasReparsePointInAncestorChain(path) || IsReparsePoint(path))
                {
                    return;
                }

                File.Delete(path);
            }

            Directory.Delete(temporary, recursive: false);
        }
        catch
        {
            // Cleanup is best effort and is restricted to this service-owned temp root.
        }
    }

    private sealed record ManagedLogReadResult(
        IReadOnlyList<SanitizedLogRecord> Records,
        bool Degraded,
        string ErrorCode);

    private sealed class SourceManifest
    {
        public string? Schema { get; set; }
        public string? Owner { get; set; }
        public string? FilePrefix { get; set; }
        public List<string>? ManagedFiles { get; set; }
        public Dictionary<string, DateTimeOffset>? FileCreatedUtc { get; set; }
    }

    private sealed class ExportManifest
    {
        [JsonPropertyName("schema")]
        public string Schema { get; init; } = string.Empty;

        [JsonPropertyName("policyVersion")]
        public string PolicyVersion { get; init; } = string.Empty;

        [JsonPropertyName("generatedAtUtc")]
        public DateTimeOffset GeneratedAtUtc { get; init; }

        [JsonPropertyName("artifacts")]
        public IReadOnlyList<ExportArtifact> Artifacts { get; init; } = Array.Empty<ExportArtifact>();
    }

    private sealed class ExportArtifact
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("byteLength")]
        public long ByteLength { get; init; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; init; } = string.Empty;
    }

    private sealed class ExportHealthSnapshot
    {
        [JsonPropertyName("schema")]
        public string Schema { get; init; } = string.Empty;

        [JsonPropertyName("generatedAtUtc")]
        public DateTimeOffset GeneratedAtUtc { get; init; }

        [JsonPropertyName("productIdentity")]
        public string? ProductIdentity { get; init; }

        [JsonPropertyName("overallStatus")]
        public string OverallStatus { get; init; } = string.Empty;

        [JsonPropertyName("recommendedAction")]
        public string RecommendedAction { get; init; } = string.Empty;

        [JsonPropertyName("server")]
        public ExportServerFact Server { get; init; } = new();

        [JsonPropertyName("components")]
        public IReadOnlyList<ExportComponent> Components { get; init; } = Array.Empty<ExportComponent>();

        [JsonPropertyName("connectedClients")]
        public ExportConnectedClients ConnectedClients { get; init; } = new();

        [JsonPropertyName("clients")]
        public IReadOnlyList<ExportClient> Clients { get; init; } = Array.Empty<ExportClient>();
    }

    private sealed class ExportServerFact
    {
        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("listenerFactExplicit")]
        public bool ListenerFactExplicit { get; init; }

        [JsonPropertyName("canonicalEndpoint")]
        public bool CanonicalEndpoint { get; init; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; init; }
    }

    private sealed class ExportComponent
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("requiredForOverall")]
        public bool RequiredForOverall { get; init; }

        [JsonPropertyName("declaredTarget")]
        public string? DeclaredTarget { get; init; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; init; }
    }

    private sealed class ExportConnectedClients
    {
        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("display")]
        public string Display { get; init; } = "N/A";
    }

    private sealed class ExportClient
    {
        [JsonPropertyName("clientId")]
        public string ClientId { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("optional")]
        public bool Optional { get; init; }
    }
}
