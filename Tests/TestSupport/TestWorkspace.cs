namespace ArcGISProMCP.TestSupport;

/// <summary>Result of a best-effort test workspace cleanup.</summary>
public sealed class TestWorkspaceCleanupResult
{
    internal TestWorkspaceCleanupResult(
        TestWorkspaceCleanupStatus status,
        IReadOnlyList<string> remainingPaths,
        string? failureMessage = null,
        Exception? exception = null)
    {
        Status = status;
        RemainingPaths = remainingPaths;
        FailureMessage = failureMessage;
        Exception = exception;
    }

    public TestWorkspaceCleanupStatus Status { get; }

    public bool Succeeded => Status == TestWorkspaceCleanupStatus.Succeeded;

    public IReadOnlyList<string> RemainingPaths { get; }

    public string? FailureMessage { get; }

    /// <summary>Original cleanup exception, if one was raised; never thrown by Cleanup.</summary>
    public Exception? Exception { get; }
}

public enum TestWorkspaceCleanupStatus
{
    Succeeded,
    BlockedByOwnership,
    BlockedByReparsePoint,
    BlockedByRuntimeLock,
    Failed
}

/// <summary>
/// Test-owned filesystem root with marker-based ownership, safe relative paths,
/// short run-scoped dataset names and non-throwing cleanup reporting.
/// </summary>
public sealed class TestWorkspace : IDisposable
{
    public const string OwnershipMarkerName = ".arcgis-pro-mcp-test-owned";

    private const int DatasetNamePurposeLimit = 20;
    private const int DatasetNameMaximumLength = 64;
    private const int CreateAttempts = 8;
    private const string ParentDirectoryName = "ArcGISProMCP";

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly object _gate = new();
    private readonly HashSet<string> _ownedPaths = new(PathComparer);
    private readonly List<string> _creationOrder = new();
    private readonly string _markerContents;
    private TestWorkspaceCleanupResult? _lastCleanup;
    private int _datasetSequence;

    private TestWorkspace(string rootPath, string runId)
    {
        RootPath = Path.GetFullPath(rootPath);
        RunId = runId;
        MarkerPath = Path.Combine(RootPath, OwnershipMarkerName);
        CreatedUtc = DateTimeOffset.UtcNow;
        _markerContents =
            $"owner=ArcGIS-Pro-MCP;run={RunId};createdUtc={CreatedUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture)}{Environment.NewLine}";

        Directory.CreateDirectory(RootPath);
        using var stream = new FileStream(
            MarkerPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        writer.Write(_markerContents);

        _ownedPaths.Add(RootPath);
        _ownedPaths.Add(MarkerPath);
        _creationOrder.Add(RootPath);
        _creationOrder.Add(MarkerPath);
    }

    public string RunId { get; }

    public string RootPath { get; }

    public string MarkerPath { get; }

    /// <summary>UTC time recorded in the ownership marker before child-process writes.</summary>
    public DateTimeOffset CreatedUtc { get; }

    public TestWorkspaceCleanupResult? LastCleanupResult
    {
        get
        {
            lock (_gate)
            {
                return _lastCleanup;
            }
        }
    }

    /// <summary>Snapshot of paths created by this workspace, including root and marker.</summary>
    public IReadOnlyList<string> OwnedPaths
    {
        get
        {
            lock (_gate)
            {
                return _creationOrder.ToArray();
            }
        }
    }

    public static TestWorkspace Create()
    {
        // G-198 E②：测试临时根可经环境变量重定向到 D 盘（默认行为不变；仅测试用）。
        var overrideRoot = Environment.GetEnvironmentVariable("ARCGIS_PRO_MCP_TEST_TEMP_ROOT");
        var parent = string.IsNullOrWhiteSpace(overrideRoot)
            ? Path.Combine(Path.GetTempPath(), ParentDirectoryName)
            : Path.Combine(overrideRoot!, ParentDirectoryName);
        Directory.CreateDirectory(parent);

        for (var attempt = 0; attempt < CreateAttempts; attempt++)
        {
            var runId = "P57_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var root = Path.Combine(parent, "Phase5_7_4_" + runId);
            if (File.Exists(root) || Directory.Exists(root))
            {
                continue;
            }

            try
            {
                return new TestWorkspace(root, runId);
            }
            catch (IOException)
            {
                // A concurrent creator may have won this generated name. Remove only
                // an empty root that this attempt could have created, then retry.
                try
                {
                    if (Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
                    {
                        Directory.Delete(root, recursive: false);
                    }
                }
                catch
                {
                    // The root is not ours to recursively remove after an uncertain race.
                }
            }
        }

        throw new IOException("Could not create a unique test-owned workspace root.");
    }

    /// <summary>Returns a short ArcGIS-compatible name unique within this test run.</summary>
    public string GetUniqueDatasetName(string purpose)
    {
        var normalized = NormalizeDatasetPurpose(purpose);
        var sequence = Interlocked.Increment(ref _datasetSequence);
        var name = $"{RunId}_{normalized}_{sequence:D2}";
        return name.Length <= DatasetNameMaximumLength
            ? name
            : name[..DatasetNameMaximumLength];
    }

    public string CreateOwnedDirectory(string relativePath)
    {
        var fullPath = ResolveNewPath(relativePath);
        var missing = new Stack<string>();
        var current = fullPath;

        while (!PathComparer.Equals(current, RootPath))
        {
            if (File.Exists(current))
            {
                throw new IOException($"A file already exists at the requested directory path: {current}");
            }

            if (Directory.Exists(current))
            {
                lock (_gate)
                {
                    if (!_ownedPaths.Contains(current))
                    {
                        throw new InvalidOperationException($"Existing directory is not test-owned: {current}");
                    }
                }

                break;
            }

            missing.Push(current);
            current = Path.GetDirectoryName(current)
                ?? throw new InvalidOperationException("The requested path has no parent directory.");
            if (!IsWithinRoot(current))
            {
                throw new ArgumentException("The requested directory escapes the test workspace.", nameof(relativePath));
            }
        }

        while (missing.Count > 0)
        {
            var directory = missing.Pop();
            Directory.CreateDirectory(directory);
            RegisterCreatedPath(directory);
        }

        return fullPath;
    }

    public string CreateOwnedFile(string relativePath, string contents = "")
    {
        var fullPath = ResolveNewPath(relativePath);
        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The requested file path has no parent directory.");

        if (!PathComparer.Equals(parent, RootPath))
        {
            CreateOwnedDirectory(Path.GetRelativePath(RootPath, parent));
        }

        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            throw new IOException($"A filesystem entry already exists at the requested path: {fullPath}");
        }

        using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
        {
            writer.Write(contents);
        }

        RegisterCreatedPath(fullPath);
        return fullPath;
    }

    /// <summary>True only for the root, marker or an artifact created by this instance.</summary>
    public bool IsOwnedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            lock (_gate)
            {
                return _ownedPaths.Contains(fullPath);
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Registers an artifact created by a child process or external test helper.
    /// Registration is accepted only for an existing, non-reparse path inside this
    /// workspace; a failed registration is reported as false and never throws.
    /// </summary>
    public bool TryRegisterOwnedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!IsWithinRoot(fullPath)
                || PathComparer.Equals(fullPath, RootPath)
                || PathComparer.Equals(fullPath, MarkerPath)
                || (!File.Exists(fullPath) && !Directory.Exists(fullPath)))
            {
                return false;
            }

            var attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            lock (_gate)
            {
                if (_ownedPaths.Contains(fullPath))
                {
                    return true;
                }

                _ownedPaths.Add(fullPath);
                _creationOrder.Add(fullPath);
                return true;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Best-effort cleanup. It never throws, refuses unowned/reparse content,
    /// and returns a separate result so a test failure cannot be masked.
    /// </summary>
    public TestWorkspaceCleanupResult Cleanup()
    {
        lock (_gate)
        {
            if (_lastCleanup?.Succeeded == true)
            {
                return _lastCleanup;
            }

            if (!Directory.Exists(RootPath))
            {
                return Remember(new TestWorkspaceCleanupResult(
                    TestWorkspaceCleanupStatus.Succeeded,
                    Array.Empty<string>()));
            }

            try
            {
                if (!File.Exists(MarkerPath))
                {
                    return Remember(Failure(
                        TestWorkspaceCleanupStatus.BlockedByOwnership,
                        "Ownership marker is missing.",
                        new[] { RootPath }));
                }

                var marker = File.ReadAllText(MarkerPath);
                if (!string.Equals(marker, _markerContents, StringComparison.Ordinal))
                {
                    return Remember(Failure(
                        TestWorkspaceCleanupStatus.BlockedByOwnership,
                        "Ownership marker content does not match this workspace.",
                        new[] { MarkerPath }));
                }

                var reparsePoint = FindReparsePoint(RootPath);
                if (reparsePoint is not null)
                {
                    return Remember(Failure(
                        TestWorkspaceCleanupStatus.BlockedByReparsePoint,
                        "Cleanup refused because a reparse point was found.",
                        new[] { reparsePoint }));
                }

                var unknownPaths = EnumeratePathsWithoutFollowingReparsePoints(RootPath)
                    .Where(path => !_ownedPaths.Contains(path))
                    .ToArray();
                if (unknownPaths.Length > 0)
                {
                    return Remember(Failure(
                        TestWorkspaceCleanupStatus.BlockedByOwnership,
                        "Cleanup refused because unowned content is present.",
                        unknownPaths));
                }

                var runtimeLock = EnumeratePathsWithoutFollowingReparsePoints(RootPath)
                    .FirstOrDefault(IsLockFilePath);
                if (runtimeLock is not null)
                {
                    return Remember(Failure(
                        TestWorkspaceCleanupStatus.BlockedByRuntimeLock,
                        "Cleanup refused because an ArcGIS runtime lock artifact remains.",
                        new[] { runtimeLock }));
                }

                string? currentPath = null;
                foreach (var path in _creationOrder
                             .Where(path => !PathComparer.Equals(path, RootPath)
                                            && !PathComparer.Equals(path, MarkerPath))
                             .OrderByDescending(PathDepth)
                             .ToArray())
                {
                    currentPath = path;
                    DeleteOwnedEntry(path);
                }

                currentPath = MarkerPath;
                File.Delete(MarkerPath);
                currentPath = RootPath;
                Directory.Delete(RootPath, recursive: false);

                return Remember(new TestWorkspaceCleanupResult(
                    TestWorkspaceCleanupStatus.Succeeded,
                    Array.Empty<string>()));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Remember(Failure(
                    IsLockPath(ex) ? TestWorkspaceCleanupStatus.BlockedByRuntimeLock : TestWorkspaceCleanupStatus.Failed,
                    ex.Message,
                    ExistingPaths(),
                    ex));
            }
            catch (IOException ex)
            {
                return Remember(Failure(
                    IsLockPath(ex) ? TestWorkspaceCleanupStatus.BlockedByRuntimeLock : TestWorkspaceCleanupStatus.Failed,
                    ex.Message,
                    ExistingPaths(),
                    ex));
            }
            catch (Exception ex)
            {
                return Remember(Failure(
                    TestWorkspaceCleanupStatus.Failed,
                    ex.Message,
                    ExistingPaths(),
                    ex));
            }
        }
    }

    public void Dispose() => Cleanup();

    private void RegisterCreatedPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!IsWithinRoot(fullPath) || PathComparer.Equals(fullPath, RootPath))
        {
            throw new ArgumentException("Only non-root paths inside the test workspace can be owned.", nameof(path));
        }

        lock (_gate)
        {
            _ownedPaths.Add(fullPath);
            _creationOrder.Add(fullPath);
        }
    }

    private string ResolveNewPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("A non-empty relative path is required.", nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Test workspace paths must be relative.", nameof(relativePath));
        }

        var fullPath = Path.GetFullPath(Path.Combine(RootPath, relativePath));
        if (!IsWithinRoot(fullPath) || PathComparer.Equals(fullPath, RootPath))
        {
            throw new ArgumentException("The requested path escapes the test workspace.", nameof(relativePath));
        }

        return fullPath;
    }

    private bool IsWithinRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(RootPath, fullPath);
        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        return !PathComparer.Equals(relative, "..")
               && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
               && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string NormalizeDatasetPurpose(string purpose)
    {
        var chars = (purpose ?? string.Empty)
            .Select(character => IsAsciiLetterOrDigit(character) || character == '_' ? character : '_')
            .ToArray();
        var normalized = new string(chars).Trim('_');
        if (normalized.Length == 0)
        {
            normalized = "Output";
        }

        if (!IsAsciiLetter(normalized[0]))
        {
            normalized = "Output_" + normalized;
        }

        return normalized.Length <= DatasetNamePurposeLimit
            ? normalized
            : normalized[..DatasetNamePurposeLimit].TrimEnd('_');
    }

    private static bool IsAsciiLetter(char character)
        => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsAsciiLetterOrDigit(char character)
        => IsAsciiLetter(character) || character is >= '0' and <= '9';

    private static int PathDepth(string path)
        => path.Count(character => character == '\\' || character == '/');

    private void DeleteOwnedEntry(string path)
    {
        if (IsLockFilePath(path))
        {
            throw new IOException($"Runtime lock artifact cannot be deleted: {path}");
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: false);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string? FindReparsePoint(string rootPath)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return current;
            }

            if ((attributes & FileAttributes.Directory) == 0)
            {
                continue;
            }

            foreach (var child in Directory.EnumerateFileSystemEntries(current))
            {
                pending.Push(child);
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumeratePathsWithoutFollowingReparsePoints(string rootPath)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var child in Directory.EnumerateFileSystemEntries(current))
            {
                yield return child;
                var attributes = File.GetAttributes(child);
                if ((attributes & FileAttributes.Directory) != 0
                    && (attributes & FileAttributes.ReparsePoint) == 0)
                {
                    pending.Push(child);
                }
            }
        }
    }

    private IReadOnlyList<string> ExistingPaths()
    {
        try
        {
            if (!Directory.Exists(RootPath))
            {
                return Array.Empty<string>();
            }

            return new[] { RootPath }
                .Concat(EnumeratePathsWithoutFollowingReparsePoints(RootPath))
                .ToArray();
        }
        catch
        {
            return new[] { RootPath };
        }
    }

    private static bool IsLockPath(Exception exception)
    {
        var message = exception.Message;
        return message.Contains("lock", StringComparison.OrdinalIgnoreCase)
               || message.Contains("used by another process", StringComparison.OrdinalIgnoreCase)
               || message.Contains("另一个进程", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLockFilePath(string path)
        => Path.GetFileName(path).EndsWith(".lock", StringComparison.OrdinalIgnoreCase);

    private TestWorkspaceCleanupResult Remember(TestWorkspaceCleanupResult result)
    {
        _lastCleanup = result;
        return result;
    }

    private static TestWorkspaceCleanupResult Failure(
        TestWorkspaceCleanupStatus status,
        string message,
        IReadOnlyList<string> remainingPaths,
        Exception? exception = null)
        => new(status, remainingPaths, message, exception);
}
