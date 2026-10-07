namespace ArcGISProMCP.Core.Runtime;

/// <summary>
/// Fixed, non-sensitive failure codes for runtime path resolution. These codes
/// are safe for health/UI consumption; they must not contain machine paths.
/// </summary>
public static class RuntimePathErrorCodes
{
    public const string AssemblyDirectoryUnavailable = "RUNTIME_ASSEMBLY_DIRECTORY_UNAVAILABLE";
    public const string AssemblyDirectoryInvalid = "RUNTIME_ASSEMBLY_DIRECTORY_INVALID";
    public const string BridgeScriptMissing = "RUNTIME_BRIDGE_SCRIPT_MISSING";
    public const string BridgeScriptInvalid = "RUNTIME_BRIDGE_SCRIPT_INVALID";
    public const string ArcGISProExecutableUnavailable = "RUNTIME_ARCGIS_PRO_EXECUTABLE_UNAVAILABLE";
    public const string ArcGISProExecutableInvalid = "RUNTIME_ARCGIS_PRO_EXECUTABLE_INVALID";
    public const string ArcGISProLayoutUnavailable = "RUNTIME_ARCGIS_PRO_LAYOUT_UNAVAILABLE";
    public const string PythonExecutableMissing = "RUNTIME_PYTHON_EXECUTABLE_MISSING";
    public const string PythonExecutableInvalid = "RUNTIME_PYTHON_EXECUTABLE_INVALID";
    public const string LocalApplicationDataUnavailable = "RUNTIME_LOCAL_APPDATA_UNAVAILABLE";
    public const string LocalApplicationDataInvalid = "RUNTIME_LOCAL_APPDATA_INVALID";
    public const string RuntimeRootInvalid = "RUNTIME_ROOT_INVALID";
    public const string ReleaseVersionInvalid = "RUNTIME_RELEASE_VERSION_INVALID";
    public const string ManagedLogDirectoryInvalid = "RUNTIME_MANAGED_LOG_DIRECTORY_INVALID";

    public static bool IsSafe(string? code)
        => code is AssemblyDirectoryUnavailable
            or AssemblyDirectoryInvalid
            or BridgeScriptMissing
            or BridgeScriptInvalid
            or ArcGISProExecutableUnavailable
            or ArcGISProExecutableInvalid
            or ArcGISProLayoutUnavailable
            or PythonExecutableMissing
            or PythonExecutableInvalid
            or LocalApplicationDataUnavailable
            or LocalApplicationDataInvalid
            or RuntimeRootInvalid
            or ReleaseVersionInvalid
            or ManagedLogDirectoryInvalid;
}

/// <summary>
/// Pure inputs for runtime path resolution. The Compatibility layer supplies
/// the loaded assembly directory, the active ArcGIS Pro executable path and
/// the standard per-user LocalApplicationData directory. The optional
/// <see cref="RuntimePathInputs.ManagedLogDirectoryOverride"/> is likewise
/// supplied by the Compatibility composition root, never read here.
/// </summary>
public sealed record RuntimePathInputs
{
    public string? CompatibilityAssemblyDirectory { get; init; }

    public string? ActiveArcGISProExecutablePath { get; init; }

    public string? LocalApplicationDataDirectory { get; init; }

    public string? ReleaseVersion { get; init; }

    /// <summary>
    /// D-125（O-D100-01／O-D120-01）可选受管日志目录覆盖。null 或空白＝不覆盖，
    /// 缺省派生结果逐字节不变；非空时必须是显式目录（见解析侧校验），且只替换日志叶节点，
    /// per-user runtime root 的派生与守卫保持不变。
    /// </summary>
    public string? ManagedLogDirectoryOverride { get; init; }
}

/// <summary>
/// All runtime paths used by the Compatibility composition root. The package
/// bridge path is relative to the loaded Compatibility assembly directory:
/// package logical path Install/PythonBridge/bridge_runner.py.
/// </summary>
public sealed record RuntimePaths
{
    public required string CompatibilityAssemblyDirectory { get; init; }

    public required string PythonBridgeScript { get; init; }

    public required string PythonWorkingDirectory { get; init; }

    public required string PythonExecutable { get; init; }

    public required string RuntimeRoot { get; init; }

    public required string ManagedLogDirectory { get; init; }

    public required string ReleaseVersion { get; init; }
}

/// <summary>
/// Result of a read-only runtime path resolution attempt.
/// </summary>
public sealed record RuntimePathResolution
{
    public required bool Succeeded { get; init; }

    public RuntimePaths? Paths { get; init; }

    public string? ErrorCode { get; init; }

    public static RuntimePathResolution Success(RuntimePaths paths)
        => new() { Succeeded = true, Paths = paths };

    public static RuntimePathResolution Failure(string errorCode)
        => new() { Succeeded = false, ErrorCode = errorCode };
}

/// <summary>
/// Resolves only the explicitly supported package/host layout. This class
/// never creates directories, starts processes, searches drives, reads the
/// registry or reads environment variables: every externally influenced path,
/// including the optional managed log directory override, arrives as an
/// explicit input supplied by the Compatibility composition root.
/// </summary>
public static class RuntimePathResolver
{
    /// <summary>
    /// Relative to the directory containing
    /// ArcGISProMCP.Compatibility.dll. The resulting package logical path is
    /// Install/PythonBridge/bridge_runner.py.
    /// </summary>
    public const string PackageBridgeRelativePath = "PythonBridge/bridge_runner.py";

    public const string PackageBridgeLogicalPath = "Install/PythonBridge/bridge_runner.py";

    public const string RuntimeProductDirectoryName = "ArcGISProMCP";

    /// <summary>Upper bound for an explicitly supplied managed log directory (Windows path budget).</summary>
    public const int MaxManagedLogDirectoryLength = 240;

    public static RuntimePathResolution Resolve(RuntimePathInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (!IsSafeReleaseVersion(inputs.ReleaseVersion))
        {
            return RuntimePathResolution.Failure(RuntimePathErrorCodes.ReleaseVersionInvalid);
        }

        if (!TryGetExistingDirectory(
                inputs.CompatibilityAssemblyDirectory,
                RuntimePathErrorCodes.AssemblyDirectoryUnavailable,
                RuntimePathErrorCodes.AssemblyDirectoryInvalid,
                out var assemblyDirectory,
                out var assemblyError))
        {
            return RuntimePathResolution.Failure(assemblyError!);
        }

        var bridgeScript = CombineContained(
            assemblyDirectory!,
            PackageBridgeRelativePath,
            RuntimePathErrorCodes.BridgeScriptInvalid);
        if (bridgeScript is null)
        {
            return RuntimePathResolution.Failure(RuntimePathErrorCodes.BridgeScriptInvalid);
        }

        if (!TryGetExistingFile(
                bridgeScript,
                RuntimePathErrorCodes.BridgeScriptMissing,
                RuntimePathErrorCodes.BridgeScriptInvalid,
                out var resolvedBridgeScript,
                out var bridgeError))
        {
            return RuntimePathResolution.Failure(bridgeError!);
        }

        if (!TryGetExistingFile(
                inputs.ActiveArcGISProExecutablePath,
                RuntimePathErrorCodes.ArcGISProExecutableUnavailable,
                RuntimePathErrorCodes.ArcGISProExecutableInvalid,
                out var arcGISProExecutable,
                out var processError))
        {
            return RuntimePathResolution.Failure(processError!);
        }

        if (!string.Equals(
                Path.GetFileName(arcGISProExecutable),
                "ArcGISPro.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            return RuntimePathResolution.Failure(RuntimePathErrorCodes.ArcGISProExecutableInvalid);
        }

        var proBin = Path.GetDirectoryName(arcGISProExecutable);
        if (string.IsNullOrWhiteSpace(proBin))
        {
            return RuntimePathResolution.Failure(RuntimePathErrorCodes.ArcGISProLayoutUnavailable);
        }

        var pythonExecutable = CombineContained(
            proBin,
            Path.Combine("Python", "envs", "arcgispro-py3", "python.exe"),
            RuntimePathErrorCodes.ArcGISProLayoutUnavailable);
        if (pythonExecutable is null)
        {
            return RuntimePathResolution.Failure(RuntimePathErrorCodes.ArcGISProLayoutUnavailable);
        }

        if (!TryGetExistingFile(
                pythonExecutable,
                RuntimePathErrorCodes.PythonExecutableMissing,
                RuntimePathErrorCodes.PythonExecutableInvalid,
                out var resolvedPythonExecutable,
                out var pythonError))
        {
            return RuntimePathResolution.Failure(pythonError!);
        }

        if (!TryGetExistingDirectory(
                inputs.LocalApplicationDataDirectory,
                RuntimePathErrorCodes.LocalApplicationDataUnavailable,
                RuntimePathErrorCodes.LocalApplicationDataInvalid,
                out var localApplicationData,
                out var localAppDataError))
        {
            return RuntimePathResolution.Failure(localAppDataError!);
        }

        var productRoot = CombineContained(
            localApplicationData!,
            RuntimeProductDirectoryName,
            RuntimePathErrorCodes.RuntimeRootInvalid);
        var releaseRoot = productRoot is null
            ? null
            : CombineContained(
                productRoot,
                inputs.ReleaseVersion!,
                RuntimePathErrorCodes.RuntimeRootInvalid);
        var runtimeRoot = releaseRoot is null
            ? null
            : CombineContained(
                releaseRoot,
                "runtime",
                RuntimePathErrorCodes.RuntimeRootInvalid);
        var derivedLogDirectory = runtimeRoot is null
            ? null
            : CombineContained(
                runtimeRoot,
                "managed-logs",
                RuntimePathErrorCodes.RuntimeRootInvalid);
        if (productRoot is null
            || releaseRoot is null
            || runtimeRoot is null
            || derivedLogDirectory is null
            || !IsWithin(productRoot, runtimeRoot)
            || !IsWithin(releaseRoot, runtimeRoot))
        {
            return RuntimePathResolution.Failure(RuntimePathErrorCodes.RuntimeRootInvalid);
        }

        if (!TryValidateExistingAncestors(runtimeRoot))
        {
            return RuntimePathResolution.Failure(RuntimePathErrorCodes.RuntimeRootInvalid);
        }

        // D-125（O-D100-01／O-D120-01）：未显式配置时逐字节沿用上面派生的 managed-logs；
        // 显式配置时只替换日志叶节点，per-user runtime root 的派生与上面的守卫全部保持不变。
        string effectiveManagedLogDirectory;
        if (string.IsNullOrWhiteSpace(inputs.ManagedLogDirectoryOverride))
        {
            effectiveManagedLogDirectory = derivedLogDirectory;
        }
        else if (TryResolveManagedLogDirectoryOverride(
            inputs.ManagedLogDirectoryOverride, out var overriddenLogDirectory))
        {
            effectiveManagedLogDirectory = overriddenLogDirectory;
        }
        else
        {
            return RuntimePathResolution.Failure(RuntimePathErrorCodes.ManagedLogDirectoryInvalid);
        }

        return RuntimePathResolution.Success(new RuntimePaths
        {
            CompatibilityAssemblyDirectory = assemblyDirectory!,
            PythonBridgeScript = resolvedBridgeScript!,
            PythonWorkingDirectory = Path.GetDirectoryName(resolvedBridgeScript!)!,
            PythonExecutable = resolvedPythonExecutable!,
            RuntimeRoot = runtimeRoot,
            ManagedLogDirectory = effectiveManagedLogDirectory,
            ReleaseVersion = inputs.ReleaseVersion!
        });
    }

    /// <summary>
    /// Validates an explicitly supplied managed log directory. Fail-closed and
    /// side-effect free: the directory is never created here, and an invalid
    /// override refuses the whole resolution instead of silently falling back.
    /// </summary>
    private static bool TryResolveManagedLogDirectoryOverride(
        string? value,
        out string resolved)
    {
        resolved = string.Empty;
        var candidate = (value ?? string.Empty).Trim();
        if (candidate.Length == 0
            || candidate.Length > MaxManagedLogDirectoryLength
            || candidate.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || !Path.IsPathRooted(candidate)
            || candidate.StartsWith(@"\\", StringComparison.Ordinal)
            || candidate.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(static segment => segment == "..")
            || !TryValidateExistingAncestors(candidate))
        {
            return false;
        }

        var normalized = Path.GetFullPath(candidate)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (normalized.Length == 0 || Path.GetPathRoot(normalized) == normalized)
        {
            // 不接受空路径或文件系统根（含盘根）：日志叶节点必须是具名目录。
            return false;
        }

        resolved = normalized;
        return true;
    }

    private static bool IsSafeReleaseVersion(string? value)
    {
        const int maxComponentLength = 9;
        const int maxValueLength = maxComponentLength * 3 + 2;

        if (string.IsNullOrEmpty(value) || value.Length > maxValueLength)
        {
            return false;
        }

        var components = value.Split('.', StringSplitOptions.None);
        if (components.Length != 3)
        {
            return false;
        }

        foreach (var component in components)
        {
            if (component.Length == 0
                || component.Length > maxComponentLength
                || (component.Length > 1 && component[0] == '0')
                || !component.All(character => character is >= '0' and <= '9'))
            {
                return false;
            }
        }

        return true;
    }

    private static string? CombineContained(
        string root,
        string relativePath,
        string errorCode)
    {
        try
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(rootFull, relativePath));
            if (!IsWithin(rootFull, candidate))
            {
                return null;
            }

            return candidate;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetExistingDirectory(
        string? value,
        string missingCode,
        string invalidCode,
        out string? resolved,
        out string? errorCode)
    {
        resolved = null;
        errorCode = null;
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value))
        {
            errorCode = missingCode;
            return false;
        }

        try
        {
            resolved = Path.GetFullPath(value);
            if (!Directory.Exists(resolved)
                || !TryValidateExistingAncestors(resolved))
            {
                errorCode = invalidCode;
                return false;
            }

            return true;
        }
        catch
        {
            errorCode = invalidCode;
            return false;
        }
    }

    private static bool TryGetExistingFile(
        string? value,
        string missingCode,
        string invalidCode,
        out string? resolved,
        out string? errorCode)
    {
        resolved = null;
        errorCode = null;
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value))
        {
            errorCode = missingCode;
            return false;
        }

        try
        {
            resolved = Path.GetFullPath(value);
            if (!File.Exists(resolved))
            {
                errorCode = missingCode;
                return false;
            }

            if ((File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0
                || !TryValidateExistingAncestors(Path.GetDirectoryName(resolved)!))
            {
                errorCode = invalidCode;
                return false;
            }

            return true;
        }
        catch
        {
            errorCode = invalidCode;
            return false;
        }
    }

    private static bool TryValidateExistingAncestors(string path)
    {
        try
        {
            var current = new DirectoryInfo(Path.GetFullPath(path));
            while (current is not null)
            {
                if (current.Exists
                    && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                var parent = current.Parent;
                if (parent is null || string.Equals(parent.FullName, current.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = parent;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWithin(string root, string candidate)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCandidate = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(
                normalizedRoot + Path.AltDirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }
}
