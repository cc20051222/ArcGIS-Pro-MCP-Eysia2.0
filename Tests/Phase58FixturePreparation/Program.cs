using System.Diagnostics;
using System.Text.Json;
using ArcGISProMCP.TestSupport;

namespace ArcGISProMCP.Phase58FixturePreparation;

internal static class Program
{
    private const string PythonExecutable =
        @"C:\Program Files\ArcGIS\Pro\bin\Python\envs\arcgispro-py3\python.exe";
    private const string SourceFeatureClass =
        @"D:\ArcGIS-Pro-MCP\TestDate\Phase4Test.gdb\TestPolygons";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static async Task<int> Main(string[] args)
    {
        var mode = args.Length == 1 ? args[0] : string.Empty;
        return mode switch
        {
            "create-retained" => await CreateRetainedFixtureAsync().ConfigureAwait(false),
            "cleanup-probe" => await RunCleanupProbeAsync().ConfigureAwait(false),
            "project-probe" => await RunProjectProbeAsync().ConfigureAwait(false),
            _ => PrintUsage(),
        };
    }

    private static async Task<int> CreateRetainedFixtureAsync()
    {
        var workspace = TestWorkspace.Create();
        var retainWorkspace = false;
        try
        {
            var result = await RunPythonAsync(
                    workspace.RootPath,
                    workspace.RunId,
                    "create-retained")
                .ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                Console.Error.WriteLine(result.StandardError);
                return WritePythonFailure(
                    workspace,
                    result,
                    "python_fixture_preparation",
                    "ArcGIS Python fixture preparation returned a non-zero exit code.");
            }

            var pythonPayload = ParseObjectJson(result.StandardOutput);
            RegisterExistingTree(workspace);

            var manifestPath = Path.Combine(workspace.RootPath, "fixture-manifest.json");
            var manifestPreview = BuildRetainedManifest(
                workspace,
                manifestPath,
                pythonPayload,
                workspace.OwnedPaths
                    .Append(manifestPath)
                    .Distinct(GetPathComparer())
                    .ToArray());
            await WriteManifestAsync(manifestPath, manifestPreview).ConfigureAwait(false);

            if (!workspace.TryRegisterOwnedPath(manifestPath))
            {
                throw new InvalidOperationException(
                    $"Could not register the owned fixture manifest: {manifestPath}");
            }

            RegisterExistingTree(workspace);
            var manifest = BuildRetainedManifest(
                workspace,
                manifestPath,
                pythonPayload,
                workspace.OwnedPaths);
            await WriteManifestAsync(manifestPath, manifest).ConfigureAwait(false);

            WriteFixtureResult(new
            {
                schemaVersion = "phase-5.8.1-fixture-result-v1",
                mode = "create-retained",
                status = "READY",
                runId = workspace.RunId,
                rootPath = workspace.RootPath,
                markerPath = workspace.MarkerPath,
                markerCreatedUtc = workspace.CreatedUtc,
                ownedPaths = workspace.OwnedPaths,
                manifestPath,
                manifestSchemaVersion = "phase-5.8.1-fixture-manifest-v1",
                pythonResult = pythonPayload,
                sourceFeatureClass = SourceFeatureClass,
                retained = true,
                exitCode = 0,
            });

            retainWorkspace = true;
            return 0;
        }
        catch (Exception ex)
        {
            return WriteExceptionFailure(workspace, ex, "fixture_runner");
        }
        finally
        {
            if (!retainWorkspace)
            {
                workspace.Dispose();
            }
        }
    }

    private static Task<int> RunCleanupProbeAsync()
        => RunDisposableProbeAsync("cleanup-probe");

    private static Task<int> RunProjectProbeAsync()
        => RunDisposableProbeAsync("project-probe");

    private static async Task<int> RunDisposableProbeAsync(string mode)
    {
        var workspace = TestWorkspace.Create();
        try
        {
            var result = await RunPythonAsync(workspace.RootPath, workspace.RunId, mode)
                .ConfigureAwait(false);
            RegisterExistingTree(workspace);
            var createdPaths = workspace.OwnedPaths;
            var cleanup = workspace.Cleanup();
            var succeeded = result.ExitCode == 0 && cleanup.Succeeded;

            WriteFixtureResult(new
            {
                schemaVersion = "phase-5.8.1-fixture-result-v1",
                mode,
                status = succeeded
                    ? "PASS"
                    : result.ExitCode != 0
                        ? "BLOCKED_BY_ENVIRONMENT"
                        : "BLOCKED_BY_RUNTIME_LOCK",
                runId = workspace.RunId,
                rootPath = workspace.RootPath,
                markerPath = workspace.MarkerPath,
                markerCreatedUtc = workspace.CreatedUtc,
                createdPaths,
                pythonExitCode = result.ExitCode,
                pythonStdout = result.StandardOutput,
                pythonStderr = result.StandardError,
                cleanupAttempted = true,
                cleanupStatus = cleanup.Status.ToString(),
                cleanupSucceeded = cleanup.Succeeded,
                cleanupFailure = cleanup.FailureMessage,
                remainingPaths = cleanup.RemainingPaths,
                rootExistsAfterCleanup = Directory.Exists(workspace.RootPath),
                residualCount = cleanup.RemainingPaths.Count,
            });

            return succeeded ? 0 : 3;
        }
        catch (Exception ex)
        {
            return WriteExceptionFailure(workspace, ex, $"{mode}_runner");
        }
        finally
        {
            workspace.Dispose();
        }
    }

    private static async Task<PythonResult> RunPythonAsync(
        string rootPath,
        string runId,
        string mode)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "prepare_arcgis_fixture.py");
        var startInfo = new ProcessStartInfo
        {
            FileName = PythonExecutable,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add(rootPath);
        startInfo.ArgumentList.Add(runId);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start ArcGIS Pro Python.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return new PythonResult(
            process.ExitCode,
            await stdoutTask.ConfigureAwait(false),
            await stderrTask.ConfigureAwait(false));
    }

    private static Dictionary<string, object?> BuildRetainedManifest(
        TestWorkspace workspace,
        string manifestPath,
        JsonElement pythonPayload,
        IReadOnlyList<string> ownedPaths)
        => new()
        {
            ["schemaVersion"] = "phase-5.8.1-fixture-manifest-v1",
            ["phase"] = "5.8.1",
            ["preparationKind"] = "PREPARATION",
            ["runId"] = workspace.RunId,
            ["rootPath"] = workspace.RootPath,
            ["markerPath"] = workspace.MarkerPath,
            ["markerCreatedUtc"] = workspace.CreatedUtc,
            ["sourceFeatureClass"] = SourceFeatureClass,
            ["pythonExecutable"] = PythonExecutable,
            ["ownedPaths"] = ownedPaths,
            ["manifestPath"] = manifestPath,
            ["ownership"] = new
            {
                markerPresentBeforeArcPyWrites = true,
                runId = workspace.RunId,
                rootPath = workspace.RootPath,
                markerPath = workspace.MarkerPath,
                markerCreatedUtc = workspace.CreatedUtc,
            },
            ["sourceHealth"] = TryGetJsonProperty(pythonPayload, "sourceHealth"),
            ["copyHealth"] = TryGetJsonProperty(pythonPayload, "copyHealth"),
            ["clipMaskHealth"] = TryGetJsonProperty(pythonPayload, "clipMaskHealth"),
            ["controlledProject"] = TryGetJsonProperty(pythonPayload, "controlledProject"),
            ["pythonResult"] = pythonPayload,
            ["retention"] = "INTENTIONALLY_RETAINED_OWNED_FIXTURE",
            ["retentionReason"] =
                "Retain the complete Phase 5.8.1 fixture for subsequent read-only verification.",
            ["nextConsumer"] = "Phase 5.8.1 controlled project setup/read-only validation",
        };

    private static async Task WriteManifestAsync(
        string manifestPath,
        IReadOnlyDictionary<string, object?> manifest)
    {
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(manifestPath, json).ConfigureAwait(false);
    }

    private static JsonElement ParseObjectJson(string text)
    {
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The fixture helper returned a non-object JSON payload.");
        }

        return document.RootElement.Clone();
    }

    private static JsonElement? TryGetJsonProperty(JsonElement payload, string name)
        => payload.ValueKind == JsonValueKind.Object
           && payload.TryGetProperty(name, out var value)
            ? value.Clone()
            : null;

    private static int WritePythonFailure(
        TestWorkspace workspace,
        PythonResult result,
        string stage,
        string message)
    {
        var registrationFailure = TryRegisterExistingTree(workspace);
        var createdPaths = workspace.OwnedPaths;
        var cleanup = workspace.Cleanup();
        var status = ClassifyPythonFailure(result);
        WriteFixtureResult(new
        {
            schemaVersion = "phase-5.8.1-fixture-result-v1",
            mode = "create-retained",
            status,
            stage,
            exceptionType = (string?)null,
            message,
            runId = workspace.RunId,
            rootPath = workspace.RootPath,
            markerPath = workspace.MarkerPath,
            markerCreatedUtc = workspace.CreatedUtc,
            createdPaths,
            cleanupAttempted = true,
            cleanupStatus = cleanup.Status.ToString(),
            cleanupSucceeded = cleanup.Succeeded,
            cleanupFailure = cleanup.FailureMessage,
            registrationFailure,
            retainedArtifacts = cleanup.RemainingPaths,
            exitCode = 2,
            pythonExitCode = result.ExitCode,
            stdout = result.StandardOutput,
            stderr = result.StandardError,
            arcPyMessages = result.StandardError,
        });
        return 2;
    }

    private static string ClassifyPythonFailure(PythonResult result)
    {
        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            if (document.RootElement.TryGetProperty("fixtureFailure", out var failure)
                && failure.TryGetProperty("stage", out var stage))
            {
                var value = stage.GetString();
                if (value is not null
                    && (value.Contains("source", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("shared", StringComparison.OrdinalIgnoreCase)))
                {
                    return "BLOCKED_BY_SOURCE_HEALTH";
                }
                if (value is not null
                    && value.Contains("project", StringComparison.OrdinalIgnoreCase))
                {
                    return "BLOCKED_BY_FIXTURE";
                }
            }
        }
        catch (JsonException)
        {
            // The raw stdout/stderr remains in the failure marker for inspection.
        }

        return "BLOCKED_BY_ENVIRONMENT";
    }

    private static int WriteExceptionFailure(
        TestWorkspace workspace,
        Exception exception,
        string stage)
    {
        var registrationFailure = TryRegisterExistingTree(workspace);
        var createdPaths = workspace.OwnedPaths;
        var cleanup = workspace.Cleanup();
        WriteFixtureResult(new
        {
            schemaVersion = "phase-5.8.1-fixture-result-v1",
            status = "BLOCKED_BY_ENVIRONMENT",
            stage,
            exceptionType = exception.GetType().FullName,
            message = exception.Message,
            runId = workspace.RunId,
            rootPath = workspace.RootPath,
            markerPath = workspace.MarkerPath,
            markerCreatedUtc = workspace.CreatedUtc,
            createdPaths,
            cleanupAttempted = true,
            cleanupStatus = cleanup.Status.ToString(),
            cleanupSucceeded = cleanup.Succeeded,
            cleanupFailure = cleanup.FailureMessage,
            registrationFailure,
            retainedArtifacts = cleanup.RemainingPaths,
            pythonExitCode = (int?)null,
            exitCode = 2,
            arcPyMessages = (string?)null,
        });
        return 2;
    }

    private static string? TryRegisterExistingTree(TestWorkspace workspace)
    {
        try
        {
            RegisterExistingTree(workspace);
            return null;
        }
        catch (Exception ex)
        {
            return $"{ex.GetType().FullName}: {ex.Message}";
        }
    }

    private static void WriteFixtureResult(object payload)
    {
        Console.WriteLine("FIXTURE_RESULT_BEGIN");
        Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        Console.WriteLine("FIXTURE_RESULT_END");
    }

    private static void RegisterExistingTree(TestWorkspace workspace)
    {
        if (!Directory.Exists(workspace.RootPath))
        {
            return;
        }

        var pending = new Stack<string>();
        pending.Push(workspace.RootPath);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var child in Directory.EnumerateFileSystemEntries(current))
            {
                if (Path.GetFullPath(child).Equals(
                        Path.GetFullPath(workspace.MarkerPath),
                        OperatingSystem.IsWindows()
                            ? StringComparison.OrdinalIgnoreCase
                            : StringComparison.Ordinal))
                {
                    continue;
                }

                if (!workspace.TryRegisterOwnedPath(child))
                {
                    throw new InvalidOperationException(
                        $"Could not register owned fixture path: {child}");
                }

                if (Directory.Exists(child))
                {
                    var attributes = File.GetAttributes(child);
                    if ((attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(child);
                    }
                }
            }
        }
    }

    private static StringComparer GetPathComparer()
        => OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static int PrintUsage()
    {
        Console.Error.WriteLine("Usage: create-retained | cleanup-probe | project-probe");
        return 1;
    }

    private sealed record PythonResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
