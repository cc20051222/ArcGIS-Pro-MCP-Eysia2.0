using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace ArcGISProMCP.Logging;

/// <summary>
/// New, separately owned structured sink. It never reads or rotates legacy log files.
/// All I/O failures degrade the sink and are swallowed so logging cannot fail business work.
/// </summary>
public sealed class StructuredFileLogger : ILogger, IDisposable
{
    private static readonly ConcurrentDictionary<string, object> ProcessGates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _ownedDirectory;
    private readonly string _manifestPath;
    private readonly StructuredLogFilePolicy _policy;
    private readonly LogRedactionPolicy _redactionPolicy;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate;
    private StructuredLogManifest _manifest;
    private string? _activeFileName;
    private bool _disposed;
    private bool _degraded;
    private long _droppedCount;
    private long _redactionFailureCount;
    private string? _lastFailureCode;
    private DateTimeOffset? _lastFailureAtUtc;
    private bool _manifestInitialized;
    private bool _initializationFailed;

    public StructuredFileLogger(
        string ownedDirectory,
        StructuredLogFilePolicy? policy = null,
        LogRedactionPolicy? redactionPolicy = null,
        Func<DateTimeOffset>? clock = null)
    {
        if (string.IsNullOrWhiteSpace(ownedDirectory))
        {
            throw new ArgumentException("An explicit owned directory is required.", nameof(ownedDirectory));
        }

        if (!Path.IsPathRooted(ownedDirectory))
        {
            throw new ArgumentException("The structured sink directory must be absolute and explicitly owned.", nameof(ownedDirectory));
        }

        _policy = policy ?? new StructuredLogFilePolicy();
        _policy.Validate();
        _redactionPolicy = redactionPolicy ?? new LogRedactionPolicy(_policy.MaxMessageLength);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _ownedDirectory = Path.GetFullPath(ownedDirectory);
        _manifestPath = Path.Combine(_ownedDirectory, _policy.ManifestFileName);
        _gate = ProcessGates.GetOrAdd(_manifestPath, static _ => new object());
        _manifest = NewManifest();

        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(_ownedDirectory);
                _manifest = LoadOrCreateManifestUnsafe();
                _manifestInitialized = true;
                RefreshActiveFileUnsafe();
            }
        }
        catch (InvalidDataException)
        {
            // An external/invalid manifest is an ownership violation and must fail closed.
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            _initializationFailed = true;
            MarkDegradedUnsafe("LOG_INIT_FAILED");
        }
        catch (IOException)
        {
            _initializationFailed = true;
            MarkDegradedUnsafe("LOG_INIT_FAILED");
        }
    }

    public StructuredLogHealth Health
    {
        get
        {
            lock (_gate)
            {
                return GetHealthUnsafe();
            }
        }
    }

    public void Log(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            if (_disposed)
            {
                _droppedCount++;
                MarkDegradedUnsafe("LOG_DISPOSED");
                return;
            }

            try
            {
                RefreshManifestUnsafe();
                var redacted = _redactionPolicy.Sanitize(entry);
                _redactionFailureCount += redacted.FailureCount;
                var payload = JsonSerializer.SerializeToUtf8Bytes(redacted.Record, JsonOptions);
                var now = _clock().ToUniversalTime();

                EnsureActiveFileUnsafe(now, payload.Length + 1);
                var path = Path.Combine(_ownedDirectory, _activeFileName!);
                WriteLineUnsafe(path, payload);
                ApplyRetentionUnsafe();
            }
            catch
            {
                _droppedCount++;
                MarkDegradedUnsafe(_initializationFailed ? "LOG_INIT_FAILED" : "LOG_WRITE_FAILED");
            }
        }
    }

    public void Debug(string message, string? category = null, string? requestId = null)
        => Log(new LogEntry { Timestamp = DateTimeOffset.UtcNow, Level = LogLevel.Debug, Category = category, RequestId = requestId, Message = message });

    public void Info(string message, string? category = null, string? requestId = null)
        => Log(new LogEntry { Timestamp = DateTimeOffset.UtcNow, Level = LogLevel.Information, Category = category, RequestId = requestId, Message = message });

    public void Warning(string message, string? category = null, string? requestId = null)
        => Log(new LogEntry { Timestamp = DateTimeOffset.UtcNow, Level = LogLevel.Warning, Category = category, RequestId = requestId, Message = message });

    public void Error(string message, string? category = null, string? requestId = null, Exception? exception = null)
        => Log(new LogEntry { Timestamp = DateTimeOffset.UtcNow, Level = LogLevel.Error, Category = category, RequestId = requestId, Message = message, Error = exception?.ToString() });

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }

    private StructuredLogManifest LoadOrCreateManifestUnsafe()
    {
        if (!File.Exists(_manifestPath))
        {
            var fresh = NewManifest();
            SaveManifestUnsafe(fresh);
            return fresh;
        }

        return ReadManifestUnsafe();
    }

    private StructuredLogManifest ReadManifestUnsafe()
    {
        StructuredLogManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<StructuredLogManifest>(File.ReadAllText(_manifestPath, Encoding.UTF8));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The structured log manifest is not valid JSON.", exception);
        }

        if (manifest is null
            || manifest.Schema != StructuredLogFilePolicy.ManifestSchema
            || manifest.Owner != StructuredLogFilePolicy.Owner
            || manifest.FilePrefix != _policy.FilePrefix
            || manifest.ManagedFiles is null
            || manifest.FileCreatedUtc is null
            || manifest.ManagedFiles.Any(file => !_policy.IsManagedFileName(file))
            || manifest.ManagedFiles.Distinct(StringComparer.Ordinal).Count() != manifest.ManagedFiles.Count
            || manifest.FileCreatedUtc.Count != manifest.ManagedFiles.Count
            || manifest.ManagedFiles.Any(file => !manifest.FileCreatedUtc.ContainsKey(file))
            || manifest.FileCreatedUtc.Keys.Any(file => !manifest.ManagedFiles.Contains(file, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("The structured log manifest is invalid or owned by another sink.");
        }

        return manifest;
    }

    private void RefreshManifestUnsafe()
    {
        _manifest = _manifestInitialized
            ? ReadManifestUnsafe()
            : LoadOrCreateManifestUnsafe();
        _manifestInitialized = true;
        RefreshActiveFileUnsafe();
    }

    private void RefreshActiveFileUnsafe()
        => _activeFileName = _manifest.ManagedFiles.LastOrDefault(
            file => File.Exists(Path.Combine(_ownedDirectory, file)));

    private StructuredLogManifest NewManifest()
        => new()
        {
            Schema = StructuredLogFilePolicy.ManifestSchema,
            Owner = StructuredLogFilePolicy.Owner,
            FilePrefix = _policy.FilePrefix,
            CreatedUtc = _clock().ToUniversalTime()
        };

    private void EnsureActiveFileUnsafe(DateTimeOffset nowUtc, int incomingBytes)
    {
        if (_activeFileName is null || !File.Exists(Path.Combine(_ownedDirectory, _activeFileName)))
        {
            CreateActiveFileUnsafe();
            return;
        }

        var path = Path.Combine(_ownedDirectory, _activeFileName);
        var fileInfo = new FileInfo(path);
        var createdUtc = _manifest.FileCreatedUtc[_activeFileName];
        var age = nowUtc - createdUtc;
        var shouldRotate = fileInfo.Length > 0
                           && (fileInfo.Length + incomingBytes > _policy.MaxFileBytes || age >= _policy.MaxFileAge);
        if (shouldRotate)
        {
            CreateActiveFileUnsafe();
        }
    }

    private void CreateActiveFileUnsafe()
    {
        var sequence = 0;
        while (true)
        {
            var fileName = _policy.FileName(sequence);
            var path = Path.Combine(_ownedDirectory, fileName);
            if (!_manifest.ManagedFiles.Contains(fileName, StringComparer.Ordinal) && !File.Exists(path))
            {
                _manifest.ManagedFiles.Add(fileName);
                _manifest.FileCreatedUtc[fileName] = _clock().ToUniversalTime();
                SaveManifestUnsafe(_manifest);
                _activeFileName = fileName;
                return;
            }

            sequence++;
        }
    }

    private void WriteLineUnsafe(string path, byte[] payload)
    {
        using var stream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.SequentialScan);
        stream.Write(payload, 0, payload.Length);
        stream.WriteByte((byte)'\n');
        stream.Flush(flushToDisk: true);
    }

    private void ApplyRetentionUnsafe()
    {
        try
        {
            var existing = _manifest.ManagedFiles
                .Where(file => File.Exists(Path.Combine(_ownedDirectory, file)))
                .ToList();
            var changed = existing.Count != _manifest.ManagedFiles.Count;
            _manifest.ManagedFiles = existing;
            foreach (var fileName in _manifest.FileCreatedUtc.Keys
                         .Where(fileName => !existing.Contains(fileName, StringComparer.Ordinal))
                         .ToArray())
            {
                _manifest.FileCreatedUtc.Remove(fileName);
            }

            var totalBytes = existing.Sum(file => new FileInfo(Path.Combine(_ownedDirectory, file)).Length);
            foreach (var fileName in existing.ToArray())
            {
                if (string.Equals(fileName, _activeFileName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (existing.Count <= _policy.MaxRetainedFiles && totalBytes <= _policy.MaxRetainedBytes)
                {
                    break;
                }

                var path = Path.Combine(_ownedDirectory, fileName);
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    MarkDegradedUnsafe("LOG_RETENTION_REPARSE_POINT");
                    break;
                }

                var length = new FileInfo(path).Length;
                File.Delete(path);
                _manifest.ManagedFiles.Remove(fileName);
                _manifest.FileCreatedUtc.Remove(fileName);
                existing.Remove(fileName);
                totalBytes -= length;
                changed = true;
            }

            if (changed)
            {
                SaveManifestUnsafe(_manifest);
            }
        }
        catch
        {
            MarkDegradedUnsafe("LOG_RETENTION_FAILED");
        }
    }

    private void SaveManifestUnsafe(StructuredLogManifest manifest)
    {
        var temporaryPath = Path.Combine(_ownedDirectory, "." + _policy.ManifestFileName + ".writing");
        try
        {
            var json = JsonSerializer.Serialize(manifest, JsonOptions);
            File.WriteAllText(temporaryPath, json + Environment.NewLine, new UTF8Encoding(false));
            File.Move(temporaryPath, _manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private StructuredLogHealth GetHealthUnsafe()
        => new()
        {
            Status = _degraded ? StructuredLogHealthStatus.Degraded : StructuredLogHealthStatus.Healthy,
            ActiveFileName = _activeFileName,
            ManagedFileCount = _manifest.ManagedFiles.Count,
            DroppedCount = _droppedCount,
            RedactionFailureCount = _redactionFailureCount,
            LastFailureCode = _lastFailureCode,
            LastFailureMessage = _lastFailureCode is null ? null : "Structured logging is degraded.",
            LastFailureAtUtc = _lastFailureAtUtc,
            RedactionPolicyVersion = LogRedactionPolicy.PolicyVersion
        };

    private void MarkDegradedUnsafe(string code)
    {
        _degraded = true;
        _lastFailureCode = code;
        _lastFailureAtUtc = _clock().ToUniversalTime();
    }

    private sealed class StructuredLogManifest
    {
        public string Schema { get; set; } = string.Empty;

        public string Owner { get; set; } = string.Empty;

        public string FilePrefix { get; set; } = string.Empty;

        public DateTimeOffset CreatedUtc { get; set; }

        public List<string> ManagedFiles { get; set; } = new();

        public Dictionary<string, DateTimeOffset> FileCreatedUtc { get; set; } = new();
    }
}
