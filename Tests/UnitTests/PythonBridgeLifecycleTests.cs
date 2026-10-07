using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.PythonBridge;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;

namespace ArcGISProMCP.UnitTests;

public sealed class PythonBridgeLifecycleTests
{
    [Fact]
    public void RequestFactoriesDoNotReuseFixedOrPathHashIds()
    {
        var requests = new[]
        {
            PythonBridgeRequest.Ping(),
            PythonBridgeRequest.Ping(),
            PythonBridgeRequest.RuntimeInfo(),
            PythonBridgeRequest.RuntimeInfo(),
            PythonBridgeRequest.DatasetSummary(@"C:\same.gdb\fc"),
            PythonBridgeRequest.DatasetSummary(@"C:\same.gdb\fc"),
        };

        Assert.Equal(requests.Length, requests.Select(r => r.Id).Distinct().Count());
        Assert.DoesNotContain(requests, r => r.Id is "ping" or "info");
        Assert.DoesNotContain(requests, r => r.Id.Contains("same.gdb", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TestActionsAreDisabledUnlessExplicitlyEnabled()
    {
        await using var fixture = await TestBridge.CreateAsync(allowTestActions: false);
        var ready = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        var pid = fixture.Manager.ProcessId;

        var result = await fixture.Manager.SendRequestAsync(
            new PythonBridgeRequest("sleep_test", new { seconds = 0 }));

        Assert.False(result.Ok);
        Assert.Equal("UNKNOWN_ACTION", result.ErrorCode);
        Assert.Equal(pid, fixture.Manager.ProcessId);

        var after = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(ready.Ok);
        Assert.True(after.Ok);
        Assert.Equal(pid, fixture.Manager.ProcessId);
    }

    [Fact]
    public async Task ProductionRunnerRequiresExplicitTestActionEnablement()
    {
        var script = FindProductionBridgeScript();
        var baseSettings = new MCPSettings
        {
            PythonExecutable = TestBridge.FindPython(),
            PythonBridgeScript = script,
            PythonWorkingDirectory = Path.GetDirectoryName(script)!,
            PythonProcessStartupTimeoutMs = 15000,
            PythonRequestTimeoutMs = 3000,
            PythonProcessShutdownTimeoutMs = 2000,
        };

        baseSettings.PythonAllowTestActions = false;
        await using (var disabled = new PythonBridgeProcessManager(baseSettings))
        {
            var result = await disabled.SendRequestAsync(
                new PythonBridgeRequest("sleep_test", new { seconds = 0 }));
            Assert.False(result.Ok);
            Assert.Equal("UNKNOWN_ACTION", result.ErrorCode);
        }

        baseSettings.PythonAllowTestActions = true;
        await using (var enabled = new PythonBridgeProcessManager(baseSettings))
        {
            var result = await enabled.SendRequestAsync(
                new PythonBridgeRequest("sleep_test", new { seconds = 0.01 }));
            Assert.True(result.Ok, result.ErrorCode + ": " + result.ErrorMessage);
        }
    }

    [Fact]
    public async Task NormalCallsAndStructuredExceptionPreserveTheSameProcess()
    {
        await using var fixture = await TestBridge.CreateAsync();

        var first = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        var firstPid = fixture.Manager.ProcessId;
        var second = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        var exception = await fixture.Manager.SendRequestAsync(
            new PythonBridgeRequest("raise_test_exception"));
        var afterException = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.False(exception.Ok);
        Assert.Equal("PYTHON_EXECUTION_ERROR", exception.ErrorCode);
        Assert.True(afterException.Ok);
        Assert.Equal(firstPid, fixture.Manager.ProcessId);
        Assert.Equal(1, fixture.Requests.Count(r => r.Action == "raise_test_exception"));
    }

    [Fact]
    public async Task PreDispatchTimeoutLeavesHealthyProcessUnchanged()
    {
        await using var fixture = await TestBridge.CreateAsync(timeoutMs: 250);
        var initial = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(initial.Ok);
        var pid = fixture.Manager.ProcessId;

        var flightField = typeof(PythonBridgeProcessManager).GetField(
            "_flight",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(flightField);
        var flight = Assert.IsType<SemaphoreSlim>(flightField!.GetValue(fixture.Manager));
        await flight.WaitAsync();
        try
        {
            var result = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());

            Assert.False(result.Ok);
            Assert.Equal(ErrorCodes.PythonTimeout, result.ErrorCode);
            Assert.Equal(pid, fixture.Manager.ProcessId);
            Assert.Equal(0, fixture.PendingCount);
        }
        finally
        {
            flight.Release();
        }

        var after = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(after.Ok);
        Assert.Equal(pid, fixture.Manager.ProcessId);
    }

    [Fact]
    public async Task PostDispatchTimeoutKillsOldProcessAndNextRequestStartsFreshProcess()
    {
        await using var fixture = await TestBridge.CreateAsync(timeoutMs: 250);
        var ready = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(ready.Ok);
        var oldPid = fixture.Manager.ProcessId;

        var result = await fixture.Manager.SendRequestAsync(
            new PythonBridgeRequest("sleep_test", new { seconds = 5 }));

        Assert.False(result.Ok);
        Assert.Equal(ErrorCodes.PythonTimeout, result.ErrorCode);
        Assert.Equal(-1, fixture.Manager.ProcessId);
        Assert.Equal(0, fixture.PendingCount);
        Assert.Equal(1, fixture.Requests.Count(r => r.Action == "sleep_test"));

        var recovered = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());

        Assert.True(recovered.Ok);
        Assert.NotEqual(oldPid, fixture.Manager.ProcessId);
        Assert.Equal(PythonBridgeState.Running, fixture.Manager.State);
    }

    [Fact]
    public async Task PostDispatchCancellationKillsOldProcessAndNextRequestStartsFreshProcess()
    {
        await using var fixture = await TestBridge.CreateAsync(timeoutMs: 5000);
        var ready = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(ready.Ok);
        var oldPid = fixture.Manager.ProcessId;

        using var cts = new CancellationTokenSource();
        var requestTask = fixture.Manager.SendRequestAsync(
            new PythonBridgeRequest("sleep_test", new { seconds = 5 }),
            cts.Token);
        await fixture.WaitForActionAsync("sleep_test");
        cts.Cancel();
        var result = await requestTask;

        Assert.False(result.Ok);
        Assert.Equal(ErrorCodes.Cancelled, result.ErrorCode);
        Assert.Equal(-1, fixture.Manager.ProcessId);
        Assert.Equal(0, fixture.PendingCount);

        var recovered = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());

        Assert.True(recovered.Ok);
        Assert.NotEqual(oldPid, fixture.Manager.ProcessId);
    }

    [Fact]
    public async Task ProcessCrashFailsCurrentCallAndNextRequestUsesFreshProcess()
    {
        await using var fixture = await TestBridge.CreateAsync();
        var ready = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(ready.Ok);
        var oldPid = fixture.Manager.ProcessId;

        var result = await fixture.Manager.SendRequestAsync(
            new PythonBridgeRequest("exit_test"));

        Assert.False(result.Ok);
        Assert.Equal(ErrorCodes.PythonBridgeUnavailable, result.ErrorCode);
        Assert.Equal(0, fixture.PendingCount);

        var recovered = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());

        Assert.True(recovered.Ok);
        Assert.NotEqual(oldPid, fixture.Manager.ProcessId);
    }

    [Fact]
    public async Task ExplicitRestartReplacesProcessWithoutReplayingBusinessRequest()
    {
        await using var fixture = await TestBridge.CreateAsync();
        var first = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        var oldPid = fixture.Manager.ProcessId;

        await fixture.Manager.RestartAsync();
        var second = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.NotEqual(oldPid, fixture.Manager.ProcessId);
        Assert.Equal(0, fixture.Requests.Count(r => r.Action == "business_action"));
        Assert.Equal(
            fixture.Requests.Count,
            fixture.Requests.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task DisposeDuringRequestCompletesWithoutObjectDisposedLeak()
    {
        await using var fixture = await TestBridge.CreateAsync(timeoutMs: 5000);
        var ready = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(ready.Ok);

        var requestTask = fixture.Manager.SendRequestAsync(
            new PythonBridgeRequest("sleep_test", new { seconds = 5 }));
        await fixture.WaitForActionAsync("sleep_test");
        var disposeTask = fixture.Manager.DisposeAsync().AsTask();

        await Task.WhenAll(requestTask, disposeTask).WaitAsync(TimeSpan.FromSeconds(10));
        var result = await requestTask;

        Assert.False(result.Ok);
        Assert.Equal(ErrorCodes.PythonBridgeUnavailable, result.ErrorCode);
        Assert.Equal(PythonBridgeState.Disposed, fixture.Manager.State);
        Assert.Equal(-1, fixture.Manager.ProcessId);
        await fixture.Manager.DisposeAsync();
    }

    [Fact]
    public async Task StartupFailureCleansProcessAndLeavesRecoverableFaultedState()
    {
        using var workspace = TestWorkspace.Create();
        var directory = workspace.RootPath;
        var settings = new MCPSettings
        {
            PythonExecutable = TestBridge.FindPython(),
            PythonBridgeScript = Path.Combine(directory, "missing_bridge.py"),
            PythonWorkingDirectory = directory,
            PythonProcessStartupTimeoutMs = 500,
            PythonProcessShutdownTimeoutMs = 1500,
        };
        await using var manager = new PythonBridgeProcessManager(settings);

        await Assert.ThrowsAnyAsync<Exception>(() => manager.StartAsync());

        Assert.Equal(PythonBridgeState.Faulted, manager.State);
        Assert.Equal(-1, manager.ProcessId);
        Assert.Equal(0, GetPendingCount(manager));
    }

    [Fact]
    public async Task OversizedResponseFailsWithStructuredLimitAndRecovers()
    {
        await using var fixture = await TestBridge.CreateAsync(
            maxResponseBytes: 1024,
            allowTestActions: true);
        var ready = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(ready.Ok);
        var oldPid = fixture.Manager.ProcessId;

        var result = await fixture.Manager.SendRequestAsync(
            new PythonBridgeRequest("oversized_test", new { bytes = 4096 }));

        Assert.False(result.Ok);
        Assert.Equal(ErrorCodes.PythonOutputLimitExceeded, result.ErrorCode);
        Assert.Equal(-1, fixture.Manager.ProcessId);
        Assert.Equal(0, fixture.PendingCount);

        var recovered = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(recovered.Ok);
        Assert.NotEqual(oldPid, fixture.Manager.ProcessId);
    }

    [Fact]
    public async Task MalformedResponseFailsAsProtocolErrorInsteadOfWaitingForTimeout()
    {
        await using var fixture = await TestBridge.CreateAsync(
            timeoutMs: 5000,
            allowTestActions: true);
        var ready = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(ready.Ok);

        var stopwatch = Stopwatch.StartNew();
        var result = await fixture.Manager.SendRequestAsync(
            new PythonBridgeRequest("malformed_test"));

        Assert.False(result.Ok);
        Assert.Equal(ErrorCodes.PythonProtocolError, result.ErrorCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3));
        Assert.Equal(-1, fixture.Manager.ProcessId);
        Assert.Equal(0, fixture.PendingCount);
    }

    [Fact]
    public async Task InvalidResponseSchemaFailsAsProtocolError()
    {
        await using var fixture = await TestBridge.CreateAsync(
            timeoutMs: 5000,
            allowTestActions: true);
        var ready = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(ready.Ok);

        var result = await fixture.Manager.SendRequestAsync(
            new PythonBridgeRequest("invalid_schema_test"));

        Assert.False(result.Ok);
        Assert.Equal(ErrorCodes.PythonProtocolError, result.ErrorCode);
        Assert.Equal(-1, fixture.Manager.ProcessId);
        Assert.Equal(0, fixture.PendingCount);
    }

    [Fact]
    public async Task UnknownLateResponseIsDroppedAndNextValidResponseSucceeds()
    {
        await using var fixture = await TestBridge.CreateAsync(allowTestActions: true);
        var ready = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(ready.Ok);
        var pid = fixture.Manager.ProcessId;

        var result = await fixture.Manager.SendRequestAsync(
            new PythonBridgeRequest("unknown_response_test"));

        Assert.True(result.Ok);
        Assert.Equal(pid, fixture.Manager.ProcessId);
        Assert.Equal(0, fixture.PendingCount);
    }

    [Fact]
    public async Task StderrFloodIsDrainedAndLoggingRemainsBounded()
    {
        var logger = new RecordingLogger();
        await using var fixture = await TestBridge.CreateAsync(
            allowTestActions: true,
            maxStderrBytes: 4096,
            logger: logger);

        var result = await fixture.Manager.SendRequestAsync(
            new PythonBridgeRequest("stderr_flood_test"));

        Assert.True(result.Ok);
        Assert.True(
            logger.Messages
                .Where(message => message.Contains("diagnostic-", StringComparison.Ordinal))
                .Sum(message => Encoding.UTF8.GetByteCount(message)) <= 4096);

        var after = await fixture.Manager.SendRequestAsync(PythonBridgeRequest.Ping());
        Assert.True(after.Ok);
    }

    private static string FindProductionBridgeScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "Source",
                "ArcGISProMCP.PythonBridge",
                "bridge_runner.py");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "The production bridge_runner.py could not be located from the test base directory.");
    }

    private static int GetPendingCount(PythonBridgeProcessManager manager)
    {
        var field = typeof(PythonBridgeProcessManager).GetField(
            "_pending",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var dictionary = Assert.IsAssignableFrom<System.Collections.IDictionary>(
            field!.GetValue(manager));
        return dictionary.Count;
    }

    private sealed class TestBridge : IAsyncDisposable
    {
        private static readonly JsonSerializerOptions LogJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private const string Script = """
            import json
            import os
            import sys
            import time

            log_path = os.path.join(os.getcwd(), "requests.ndjson")

            def write_log(request):
                with open(log_path, "a", encoding="utf-8") as log:
                    log.write(json.dumps({
                        "id": request.get("id"),
                        "action": request.get("action")
                    }) + "\n")

            def respond(request_id, ok, result=None, error=None):
                body = {"id": request_id, "ok": ok}
                if result is not None:
                    body["result"] = result
                if error is not None:
                    body["error"] = error
                sys.stdout.write(json.dumps(body) + "\n")
                sys.stdout.flush()

            allow_test_actions = os.environ.get("ARCGIS_PRO_MCP_ALLOW_TEST_ACTIONS") == "1"
            test_actions = {
                "sleep_test",
                "raise_test_exception",
                "exit_test",
                "oversized_test",
                "malformed_test",
                "invalid_schema_test",
                "unknown_response_test",
                "stderr_flood_test",
            }

            for raw in sys.stdin:
                try:
                    request = json.loads(raw)
                    write_log(request)
                    request_id = request.get("id")
                    action = request.get("action")
                    args = request.get("args") or {}
                    if action in test_actions and not allow_test_actions:
                        respond(request_id, False, error={
                            "code": "UNKNOWN_ACTION",
                            "message": str(action)
                        })
                    elif action == "ping":
                        respond(request_id, True, "pong")
                    elif action == "runtime_info":
                        respond(request_id, True, {"pythonVersion": "test"})
                    elif action == "sleep_test":
                        time.sleep(float(args.get("seconds", 5)))
                        respond(request_id, True, "slept")
                    elif action == "raise_test_exception":
                        respond(request_id, False, error={
                            "code": "PYTHON_EXECUTION_ERROR",
                            "message": "intentional test exception"
                        })
                    elif action == "exit_test":
                        os._exit(23)
                    elif action == "oversized_test":
                        respond(request_id, True, "x" * int(args.get("bytes", 4096)))
                    elif action == "malformed_test":
                        sys.stdout.write("not-json\n")
                        sys.stdout.flush()
                    elif action == "invalid_schema_test":
                        respond(request_id, True)
                    elif action == "unknown_response_test":
                        respond("py-999-999", True, "stale")
                        respond(request_id, True, "fresh")
                    elif action == "stderr_flood_test":
                        for _ in range(128):
                            sys.stderr.write("diagnostic-" + ("x" * 2048) + "\n")
                        sys.stderr.flush()
                        respond(request_id, True, "drained")
                    else:
                        respond(request_id, False, error={
                            "code": "UNKNOWN_ACTION",
                            "message": str(action)
                        })
                except Exception as exc:
                    respond(None, False, error={
                        "code": "PYTHON_EXECUTION_ERROR",
                        "message": str(exc)
                    })
            """;

        private TestBridge(
            TestWorkspace workspace,
            PythonBridgeProcessManager manager)
        {
            Workspace = workspace;
            Directory = workspace.RootPath;
            Manager = manager;
        }

        private TestWorkspace Workspace { get; }

        public string Directory { get; }
        public PythonBridgeProcessManager Manager { get; }

        public IReadOnlyList<RequestLog> Requests
        {
            get
            {
                var path = Path.Combine(Directory, "requests.ndjson");
                if (!File.Exists(path))
                {
                    return Array.Empty<RequestLog>();
                }

                try
                {
                    return File.ReadAllLines(path)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => JsonSerializer.Deserialize<RequestLog>(line, LogJsonOptions))
                        .Where(log => log is not null)
                        .Select(log => log!)
                        .ToArray();
                }
                catch (IOException)
                {
                    // The fake bridge may still hold its exclusive append handle while
                    // WaitForActionAsync polls for the just-dispatched request. Treat the
                    // log as not visible yet and let the existing polling deadline observe
                    // it after the writer closes the handle.
                    return Array.Empty<RequestLog>();
                }
            }
        }

        public int PendingCount => GetPendingCount(Manager);

        public static async Task<TestBridge> CreateAsync(
            int timeoutMs = 3000,
            bool allowTestActions = true,
            int? maxResponseBytes = null,
            int maxStderrBytes = 64 * 1024,
            ILogger? logger = null)
        {
            var workspace = TestWorkspace.Create();
            PythonBridgeProcessManager? manager = null;
            try
            {
                var directory = workspace.RootPath;
                var script = workspace.CreateOwnedFile("test_bridge.py", Script);
                var settings = new MCPSettings
                {
                    PythonExecutable = FindPython(),
                    PythonBridgeScript = script,
                    PythonWorkingDirectory = directory,
                    PythonProcessStartupTimeoutMs = 3000,
                    PythonRequestTimeoutMs = timeoutMs,
                    PythonProcessShutdownTimeoutMs = 2000,
                    PythonMaxResponseBytes = maxResponseBytes ?? 1024 * 1024,
                    PythonMaxStderrBytes = maxStderrBytes,
                    PythonAllowTestActions = allowTestActions,
                };
                manager = new PythonBridgeProcessManager(settings, logger);
                return await Task.FromResult(new TestBridge(workspace, manager));
            }
            catch
            {
                if (manager is not null)
                {
                    try
                    {
                        await manager.DisposeAsync();
                    }
                    catch
                    {
                        // Preserve the original fixture construction failure.
                    }
                }

                workspace.Dispose();
                throw;
            }
        }

        public static string FindPython()
        {
            var candidates = new[]
            {
                Environment.GetEnvironmentVariable("PYTHON"),
                "python",
                "python3",
            }.Where(value => !string.IsNullOrWhiteSpace(value));

            foreach (var candidate in candidates)
            {
                try
                {
                    using var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = candidate!,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    });
                    if (process is not null
                        && process.WaitForExit(3000)
                        && process.ExitCode == 0)
                    {
                        return candidate!;
                    }
                }
                catch
                {
                    // Try the next executable name.
                }
            }

            throw new InvalidOperationException(
                "A Python executable is required for Python Bridge lifecycle tests.");
        }

        public async Task WaitForActionAsync(string action)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (Requests.Any(request => request.Action == action))
                {
                    return;
                }

                await Task.Delay(25);
            }

            Assert.Fail("Timed out waiting for test bridge action: " + action);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await Manager.DisposeAsync();
            }
            finally
            {
                // The child process owns this request log, but creates it after the
                // fixture root is initialized. Register it only after the process
                // has stopped so cleanup can remove it safely.
                Workspace.TryRegisterOwnedPath(Path.Combine(Directory, "requests.ndjson"));
                // Cleanup is non-throwing and independently reports any remaining path.
                Workspace.Dispose();
            }
        }
    }

    private sealed record RequestLog(string? Id, string? Action);

    private sealed class RecordingLogger : ILogger
    {
        private readonly object _gate = new();
        private readonly List<string> _messages = new();

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_gate)
                {
                    return _messages.ToArray();
                }
            }
        }

        public void Log(LogEntry entry) => Add(entry.Message);

        public void Debug(string message, string? category = null, string? requestId = null)
            => Add(message);

        public void Info(string message, string? category = null, string? requestId = null)
            => Add(message);

        public void Warning(string message, string? category = null, string? requestId = null)
            => Add(message);

        public void Error(
            string message,
            string? category = null,
            string? requestId = null,
            Exception? exception = null)
            => Add(message);

        private void Add(string? message)
        {
            if (message is null)
            {
                return;
            }

            lock (_gate)
            {
                _messages.Add(message);
            }
        }
    }
}
