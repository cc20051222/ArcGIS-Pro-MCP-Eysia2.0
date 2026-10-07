using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Runtime;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Core.PythonBridge;

/// <summary>
/// Python Bridge 持久化进程管理器。
/// 职责：启动一次 python.exe 跑 bridge_runner.py，跨请求复用；stdin/stdout NDJSON；
/// stderr 持续消费到 ILogger；按请求 id 关联响应；检测退出；有界停止/清理。
/// 并发访问：串行（single-flight），避免 stdin 交错与响应错配。
/// 本管理器不依赖 ArcGIS SDK，属纯 .NET 进程生命周期实现（Rule 5）。
/// </summary>
public sealed class PythonBridgeProcessManager : IAsyncDisposable
{
    private readonly MCPSettings _settings;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, PendingRequest> _pending = new();
    private readonly SemaphoreSlim _flight = new(1, 1);
    // 生命周期锁只保护短生命周期状态/句柄转换；不在持有它时等待 _flight。
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly TaskCompletionSource<object?> _operationsDrained =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<object?> _disposeCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Process? _process;
    private StreamWriter? _stdin;
    private TextReader? _stdout;
    private TextReader? _stderr;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    private CancellationTokenSource? _readerCts;
    private PythonBridgeState _state = PythonBridgeState.Stopped;
    private string? _lastSafeErrorCode;

    private long _nextGeneration;
    private long _nextSequence;
    private long _generation;
    private int _activeOperations;
    private int _disposeStarted;
    private bool _disposing;
    private bool _disposed;

    private enum DispatchState
    {
        NotDispatched,
        PendingRegistered,
        Dispatching,
        Dispatched,
        Completed,
    }

    private sealed class PendingRequest
    {
        public PendingRequest(string id, long generation, bool startupProbe)
        {
            Id = id;
            Generation = generation;
            StartupProbe = startupProbe;
            Completion = new TaskCompletionSource<PythonBridgeResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            DispatchState = DispatchState.PendingRegistered;
        }

        public string Id { get; }
        public long Generation { get; }
        public bool StartupProbe { get; }
        public TaskCompletionSource<PythonBridgeResponse> Completion { get; }
        public DispatchState DispatchState { get; set; }
    }

    private sealed class PythonBridgeTransportException : Exception
    {
        public PythonBridgeTransportException(
            string message,
            string code = ErrorCodes.PythonBridgeUnavailable)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }

    private sealed class StderrLogBudget
    {
        private readonly long _limit;
        private long _consumed;

        public StderrLogBudget(int limit)
        {
            _limit = Math.Max(0, limit);
        }

        public string? Take(string text)
        {
            if (_limit <= 0 || string.IsNullOrEmpty(text))
            {
                return null;
            }

            while (true)
            {
                var consumed = Interlocked.Read(ref _consumed);
                var remaining = _limit - consumed;
                if (remaining <= 0)
                {
                    return null;
                }

                var maxBytes = (int)Math.Min(int.MaxValue, remaining);
                var bounded = TakeUtf8Prefix(text, maxBytes);
                var bytes = Encoding.UTF8.GetByteCount(bounded);
                if (bytes == 0)
                {
                    return null;
                }

                if (Interlocked.CompareExchange(
                        ref _consumed,
                        consumed + bytes,
                        consumed) == consumed)
                {
                    return bounded;
                }
            }
        }

        private static string TakeUtf8Prefix(string text, int maxBytes)
        {
            if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
            {
                return text;
            }

            var characterCount = 0;
            var byteCount = 0;
            while (characterCount < text.Length)
            {
                var nextBytes = Encoding.UTF8.GetByteCount(
                    text.AsSpan(characterCount, 1));
                if (byteCount + nextBytes > maxBytes)
                {
                    break;
                }

                byteCount += nextBytes;
                characterCount++;
            }

            return text[..characterCount];
        }
    }

    public PythonBridgeProcessManager(MCPSettings settings, ILogger? logger = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? NullLogger.Instance;
    }

    public PythonBridgeState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public bool IsRunning => State == PythonBridgeState.Running;

    /// <summary>
    /// Last lifecycle/protocol error as a controlled code only. Raw exception
    /// text is intentionally not retained for health/UI consumption.
    /// </summary>
    public string? LastSafeErrorCode
    {
        get
        {
            lock (_gate)
            {
                return _lastSafeErrorCode;
            }
        }
    }

    /// <summary>当前 python 子进程 PID（未运行返回 -1）。</summary>
    public int ProcessId
    {
        get
        {
            lock (_gate)
            {
                if (_process is null)
                {
                    return -1;
                }

                try
                {
                    return _process.HasExited ? -1 : _process.Id;
                }
                catch (InvalidOperationException)
                {
                    return -1;
                }
            }
        }
    }

    /// <summary>启动 python + bridge_runner.py；若已运行则幂等返回。</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        ThrowIfOperationNotAllowed();
        try
        {
            await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await StartLockedAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _lifecycle.Release();
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>确保进程已启动（幂等）。</summary>
    public async Task EnsureStartedAsync(CancellationToken ct = default)
        => await StartAsync(ct).ConfigureAwait(false);

    /// <summary>
    /// 发送一个请求，等待对应 id 的响应。超时/取消按 dispatch boundary 分类；
    /// 已派发请求在返回前完成 process quarantine，不自动 replay。
    /// </summary>
    public async Task<PythonBridgeCallResult> SendRequestAsync(
        PythonBridgeRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryEnterOperation())
        {
            return PythonBridgeCallResult.Failed(
                ErrorCodes.PythonBridgeUnavailable,
                "Python Bridge is disposed or disposing.");
        }

        try
        {
            // 启动时间由独立 startup timeout 管理，不计入普通 Python action deadline。
            try
            {
                await EnsureStartedForRequestAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return PythonBridgeCallResult.Failed(
                    ErrorCodes.Cancelled,
                    "Python Bridge request cancelled before dispatch.");
            }
            catch (PythonBridgeTransportException ex)
            {
                return PythonBridgeCallResult.Failed(
                    ex.Code,
                    "Python Bridge startup was refused by a fixed runtime safety gate.");
            }
            catch (Exception)
            {
                return PythonBridgeCallResult.Failed(
                    ErrorCodes.PythonBridgeUnavailable,
                    "Python Bridge startup failed.");
            }

            using var pythonTimeoutCts = new CancellationTokenSource();
            if (_settings.PythonRequestTimeoutMs > 0)
            {
                pythonTimeoutCts.CancelAfter(Math.Max(1, _settings.PythonRequestTimeoutMs));
            }

            using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
                ct,
                pythonTimeoutCts.Token);
            var operationToken = operationCts.Token;

            var flightAcquired = false;
            try
            {
                try
                {
                    // Python deadline 从启动完成后开始，但包含等待 single-flight 的时间。
                    await _flight.WaitAsync(operationToken).ConfigureAwait(false);
                    flightAcquired = true;
                }
                catch (OperationCanceledException)
                {
                    return PythonBridgeCallResult.Failed(
                        CancellationCode(ct, pythonTimeoutCts),
                        CancellationMessage(ct, pythonTimeoutCts));
                }

                if (!IsAcceptingRequests())
                {
                    return PythonBridgeCallResult.Failed(
                        ErrorCodes.PythonBridgeUnavailable,
                        "Python Bridge is not accepting requests.");
                }

                Process? process = null;
                StreamWriter? stdin = null;
                long generation = 0;
                try
                {
                    await _lifecycle.WaitAsync(operationToken).ConfigureAwait(false);
                    try
                    {
                        lock (_gate)
                        {
                            if (_disposing || _disposed)
                            {
                                return PythonBridgeCallResult.Failed(
                                    ErrorCodes.PythonBridgeUnavailable,
                                    "Python Bridge is disposing.");
                            }

                            process = _process;
                            stdin = _stdin;
                            generation = _generation;
                            if (_state != PythonBridgeState.Running
                                || process is null
                                || SafeHasExited(process)
                                || stdin is null
                                || _stdout is null)
                            {
                                return PythonBridgeCallResult.Failed(
                                    ErrorCodes.PythonBridgeUnavailable,
                                    "Python Bridge is not running (state=" + _state + ").");
                            }
                        }
                    }
                    finally
                    {
                        _lifecycle.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    return PythonBridgeCallResult.Failed(
                        CancellationCode(ct, pythonTimeoutCts),
                        CancellationMessage(ct, pythonTimeoutCts));
                }

                var wireRequest = request.WithCorrelationId(
                    CreateCorrelationId(generation));
                PendingRequest pending;
                try
                {
                    pending = RegisterPending(wireRequest, generation, startupProbe: false);
                }
                catch (InvalidOperationException ex)
                {
                    return PythonBridgeCallResult.Failed(ErrorCodes.InternalError, ex.Message);
                }

                var line = string.Empty;
                try
                {
                    // 序列化失败发生在进入写入域之前，不污染健康进程。
                    line = wireRequest.ToLine();
                    operationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    RemovePending(pending);
                    return PythonBridgeCallResult.Failed(
                        CancellationCode(ct, pythonTimeoutCts),
                        CancellationMessage(ct, pythonTimeoutCts));
                }
                catch (Exception ex)
                {
                    RemovePending(pending);
                    return PythonBridgeCallResult.Failed(
                        ErrorCodes.InternalError,
                        "Python Bridge request serialization failed: " + ex.Message);
                }

                try
                {
                    // 一旦设置为 Dispatching，任何写入异常都按 possibly-dispatched 处理。
                    SetDispatchState(pending, DispatchState.Dispatching);
                    await stdin!.WriteLineAsync(line.AsMemory(), operationToken).ConfigureAwait(false);
                    #if NET8_0_OR_GREATER
                    await stdin!.FlushAsync(operationToken).ConfigureAwait(false);
                    #else
                    // .NET 6 无 StreamWriter.FlushAsync(CancellationToken)（该重载 .NET 8 才新增）；
                    // 等价实现：先做取消检查，再执行无 token 刷新（取消语义由调用方的 token 检查与后续等待承担）。
                    operationToken.ThrowIfCancellationRequested();
                    await stdin!.FlushAsync().ConfigureAwait(false);
                    #endif
                    SetDispatchState(pending, DispatchState.Dispatched);
                }
                catch (OperationCanceledException)
                {
                    var dispatchState = RemovePending(pending);
                    if (IsPossiblyDispatched(dispatchState))
                    {
                        return await AbortAfterDispatchAsync(
                            process!,
                            generation,
                            CancellationCode(ct, pythonTimeoutCts),
                            CancellationMessage(ct, pythonTimeoutCts),
                            "request write cancelled").ConfigureAwait(false);
                    }

                    return PythonBridgeCallResult.Failed(
                        CancellationCode(ct, pythonTimeoutCts),
                        CancellationMessage(ct, pythonTimeoutCts));
                }
                catch (Exception ex) when (
                    ex is IOException or ObjectDisposedException or InvalidOperationException)
                {
                    var dispatchState = RemovePending(pending);
                    if (IsPossiblyDispatched(dispatchState))
                    {
                        return await AbortAfterDispatchAsync(
                            process!,
                            generation,
                            ErrorCodes.PythonBridgeUnavailable,
                            "Python Bridge stream unavailable: " + ex.Message,
                            "request stream failure").ConfigureAwait(false);
                    }

                    return PythonBridgeCallResult.Failed(
                        ErrorCodes.PythonBridgeUnavailable,
                        "Python Bridge stream unavailable: " + ex.Message);
                }

                try
                {
                    var response = await pending.Completion.Task
                        .WaitAsync(operationToken)
                        .ConfigureAwait(false);
                    RemovePending(pending);
                    return ToCallResult(response);
                }
                catch (OperationCanceledException)
                {
                    var dispatchState = RemovePending(pending);
                    if (IsPossiblyDispatched(dispatchState))
                    {
                        return await AbortAfterDispatchAsync(
                            process!,
                            generation,
                            CancellationCode(ct, pythonTimeoutCts),
                            CancellationMessage(ct, pythonTimeoutCts),
                            "response wait cancelled").ConfigureAwait(false);
                    }

                    return PythonBridgeCallResult.Failed(
                        CancellationCode(ct, pythonTimeoutCts),
                        CancellationMessage(ct, pythonTimeoutCts));
                }
                catch (PythonBridgeTransportException ex)
                {
                    RemovePending(pending);
                    if (ex.Code is ErrorCodes.PythonOutputLimitExceeded
                        or ErrorCodes.PythonProtocolError)
                    {
                        return await AbortAfterDispatchAsync(
                            process!,
                            generation,
                            ex.Code,
                            ex.Message,
                            "protocol safety fault").ConfigureAwait(false);
                    }

                    return PythonBridgeCallResult.Failed(ex.Code, ex.Message);
                }
                catch (Exception ex)
                {
                    RemovePending(pending);
                    return PythonBridgeCallResult.Failed(
                        ErrorCodes.PythonBridgeUnavailable,
                        "Python Bridge response wait failed: " + ex.Message);
                }
            }
            finally
            {
                if (flightAcquired)
                {
                    _flight.Release();
                }
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>停止进程：关闭 stdin，执行有界等待，必要时 Kill 整个 process tree。</summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        ThrowIfOperationNotAllowed();
        try
        {
            await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // 一旦拿到生命周期锁，cleanup 不再使用 caller token，避免取消逃逸。
                await StopCoreAsync("explicit stop").ConfigureAwait(false);
            }
            finally
            {
                _lifecycle.Release();
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>有限重启：清理旧进程后启动一个新进程；不接收业务 request，不 replay。</summary>
    public async Task RestartAsync(CancellationToken ct = default)
    {
        ThrowIfOperationNotAllowed();
        try
        {
            await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var stopped = await StopCoreAsync("explicit restart").ConfigureAwait(false);
                if (!stopped)
                {
                    throw new InvalidOperationException(
                        "Python Bridge process could not be confirmed stopped.");
                }

                await StartLockedAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _lifecycle.Release();
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposeStarted, 1, 0) == 0)
        {
            return new ValueTask(DisposeCoreAsync());
        }

        return new ValueTask(_disposeCompleted.Task);
    }

    // ------------------------------------------------------------ lifecycle

    private async Task EnsureStartedForRequestAsync(CancellationToken ct)
    {
        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await StartLockedAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task StartLockedAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (_disposed || _disposing)
            {
                throw new ObjectDisposedException(nameof(PythonBridgeProcessManager));
            }

            if (_state == PythonBridgeState.Running
                && _process is not null
                && !SafeHasExited(_process))
            {
                return;
            }

            _state = PythonBridgeState.Starting;
            _lastSafeErrorCode = null;
        }

        try
        {
            // Faulted/Stopped 可能还保留已退出或未能完全终止的旧 Process。
            if (HasResources())
            {
                var staleClean = await CleanupProcessLockedAsync(
                    "stale process before start").ConfigureAwait(false);
                if (!staleClean)
                {
                    throw new InvalidOperationException(
                        "Previous Python Bridge process could not be confirmed stopped.");
                }
            }

            await StartProcessCoreAsync(ct).ConfigureAwait(false);
            lock (_gate)
            {
                _state = PythonBridgeState.Running;
                _lastSafeErrorCode = null;
            }

            _logger.Info("Python Bridge started.", category: "pythonbridge");
        }
        catch (Exception ex)
        {
            try
            {
                await CleanupProcessLockedAsync("startup failure").ConfigureAwait(false);
            }
            catch (Exception cleanupEx)
            {
                _logger.Error(
                    "Python Bridge startup cleanup failed.",
                    category: "pythonbridge",
                    exception: cleanupEx);
            }

            lock (_gate)
            {
                _state = PythonBridgeState.Faulted;
                _lastSafeErrorCode = ErrorCodes.PythonBridgeUnavailable;
            }

            _logger.Error("Python Bridge start failed.", category: "pythonbridge", exception: ex);
            throw;
        }
    }

    private async Task<bool> StopCoreAsync(string reason)
    {
        lock (_gate)
        {
            if (_state == PythonBridgeState.Disposed && _process is null)
            {
                return true;
            }

            if (!HasResourcesUnsafe())
            {
                _state = PythonBridgeState.Stopped;
                return true;
            }

            _state = PythonBridgeState.Stopping;
        }

        var clean = await CleanupProcessLockedAsync(reason).ConfigureAwait(false);
        lock (_gate)
        {
            if (_state != PythonBridgeState.Disposed)
            {
                _state = clean ? PythonBridgeState.Stopped : PythonBridgeState.Faulted;
                if (!clean)
                {
                    _lastSafeErrorCode = ErrorCodes.PythonBridgeUnavailable;
                }
            }
        }

        if (clean)
        {
            _logger.Info("Python Bridge stopped.", category: "pythonbridge");
        }
        else
        {
            _logger.Warning(
                "Python Bridge cleanup could not confirm process exit: " + reason,
                category: "pythonbridge");
        }

        return clean;
    }

    private async Task<bool> QuarantineProcessAsync(
        Process expectedProcess,
        long expectedGeneration,
        string reason)
    {
        // 不使用 caller token；post-dispatch cleanup 必须独立、有界且不可被原取消打断。
        await _lifecycle.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                // Stop/Restart may have already replaced the target while this request
                // was unwinding. Never quarantine a newer generation on behalf of an old one.
                if (!ReferenceEquals(_process, expectedProcess)
                    || _generation != expectedGeneration)
                {
                    return true;
                }
            }

            return await StopCoreAsync("quarantine: " + reason).ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task<PythonBridgeCallResult> AbortAfterDispatchAsync(
        Process process,
        long generation,
        string resultCode,
        string resultMessage,
        string reason)
    {
        var clean = false;
        try
        {
            clean = await QuarantineProcessAsync(process, generation, reason)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(
                "Python Bridge quarantine failed.",
                category: "pythonbridge",
                exception: ex);
        }

        if (!clean)
        {
            return PythonBridgeCallResult.Failed(
                ErrorCodes.PythonBridgeUnavailable,
                resultMessage + " Process quarantine was not confirmed.");
        }

        return PythonBridgeCallResult.Failed(resultCode, resultMessage);
    }

    private async Task DisposeCoreAsync()
    {
        Exception? failure = null;
        CancellationTokenSource? readerCts = null;
        var lifecycleAcquired = false;

        try
        {
            lock (_gate)
            {
                _disposing = true;
                if (_activeOperations == 0)
                {
                    _operationsDrained.TrySetResult(null);
                }
            }

            await _lifecycle.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            lifecycleAcquired = true;
            try
            {
                await StopCoreAsync("dispose").ConfigureAwait(false);
            }
            finally
            {
                _lifecycle.Release();
                lifecycleAcquired = false;
            }

            // StopCore 已让当前 process 不可用；这里等待所有已进入的 public operation 退出，
            // 使其 finally 中的 Semaphore.Release 先于 Semaphore.Dispose。
            await _operationsDrained.Task.ConfigureAwait(false);

            lock (_gate)
            {
                _disposed = true;
                _state = PythonBridgeState.Disposed;
                readerCts = _readerCts;
                _readerCts = null;
            }

            readerCts?.Dispose();
            _flight.Dispose();
            _lifecycle.Dispose();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            if (lifecycleAcquired)
            {
                _lifecycle.Release();
            }

            // 正常路径已经完成；异常路径也禁止新操作并尽量完成 dispose 通知。
            lock (_gate)
            {
                _disposed = true;
                _disposing = true;
                _state = PythonBridgeState.Disposed;
                readerCts ??= _readerCts;
                _readerCts = null;
            }

            try
            {
                readerCts?.Dispose();
            }
            catch
            {
                // ignore dispose cleanup failure
            }

            if (failure is null)
            {
                _disposeCompleted.TrySetResult(null);
            }
            else
            {
                _disposeCompleted.TrySetException(failure);
            }
        }
    }

    // ------------------------------------------------------------ process startup

    private async Task StartProcessCoreAsync(CancellationToken ct)
    {
        var (pythonExe, script, workingDir) = ResolvePaths();
        var readerCts = new CancellationTokenSource();
        Process? process = null;
        StreamWriter? stdin = null;
        TextReader? stdout = null;
        TextReader? stderr = null;
        var registered = false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = "\"" + script + "\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.Environment[MCPSettings.PythonAllowTestActionsEnvironmentVariable] =
                _settings.PythonAllowTestActions ? "1" : "0";
            if (!string.IsNullOrWhiteSpace(workingDir))
            {
                psi.WorkingDirectory = workingDir;
            }

            process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.Exited += OnProcessExited;
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start python process.");
            }

            stdin = process.StandardInput;
            stdout = process.StandardOutput;
            stderr = process.StandardError;
            var generation = Interlocked.Increment(ref _nextGeneration);

            lock (_gate)
            {
                _process = process;
                _stdin = stdin;
                _stdout = stdout;
                _stderr = stderr;
                _readerCts = readerCts;
                _generation = generation;
                registered = true;
            }

            var readerToken = readerCts.Token;
            var stdoutTask = Task.Run(
                () => ReadStdoutLoop(process!, generation, stdout!, readerToken),
                CancellationToken.None);
            var stderrBudget = new StderrLogBudget(_settings.PythonMaxStderrBytes);
            var stderrTask = Task.Run(
                () => ReadStderrLoop(stderr!, readerToken, stderrBudget),
                CancellationToken.None);
            lock (_gate)
            {
                if (ReferenceEquals(_process, process) && _generation == generation)
                {
                    _stdoutTask = stdoutTask;
                    _stderrTask = stderrTask;
                }
            }

            // startup probe 是基础设施健康检查，不是业务 request，使用独立 startup timeout。
            var startupTimeoutMs = Math.Max(1, _settings.PythonProcessStartupTimeoutMs);
            var probe = PythonBridgeRequest.Ping()
                .WithCorrelationId(CreateCorrelationId(generation));
            var pending = RegisterPending(probe, generation, startupProbe: true);
            try
            {
                ct.ThrowIfCancellationRequested();
                SetDispatchState(pending, DispatchState.Dispatching);
                await stdin.WriteLineAsync(probe.ToLine().AsMemory(), ct).ConfigureAwait(false);
                #if NET8_0_OR_GREATER
                await stdin.FlushAsync(ct).ConfigureAwait(false);
                #else
                // .NET 6 无 StreamWriter.FlushAsync(CancellationToken)（该重载 .NET 8 才新增）；
                // 等价实现：先做取消检查，再执行无 token 刷新（取消语义由调用方的 token 检查与后续等待承担）。
                ct.ThrowIfCancellationRequested();
                await stdin.FlushAsync().ConfigureAwait(false);
                #endif
                SetDispatchState(pending, DispatchState.Dispatched);

                var response = await pending.Completion.Task
                    .WaitAsync(TimeSpan.FromMilliseconds(startupTimeoutMs), ct)
                    .ConfigureAwait(false);
                if (!response.IsOk
                    || response.Result is not { } result
                    || result.ValueKind != JsonValueKind.String
                    || !string.Equals(result.GetString(), "pong", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Python Bridge startup probe returned an invalid response.");
                }
            }
            finally
            {
                RemovePending(pending);
            }
        }
        catch
        {
            if (!registered && process is not null)
            {
                await CleanupDetachedProcessAsync(
                    process,
                    stdin,
                    stdout,
                    stderr,
                    readerCts,
                    "startup setup failure").ConfigureAwait(false);
            }

            throw;
        }
    }

    private async Task CleanupDetachedProcessAsync(
        Process process,
        StreamWriter? stdin,
        TextReader? stdout,
        TextReader? stderr,
        CancellationTokenSource readerCts,
        string reason)
    {
        readerCts.Cancel();
        DisposeQuietly(stdin);
        DisposeQuietly(stdout);
        DisposeQuietly(stderr);
        var stopped = await TerminateAndWaitAsync(
            process,
            stdoutTask: null,
            stderrTask: null,
            readerCts,
            reason).ConfigureAwait(false);
        if (stopped)
        {
            DisposeQuietly(process);
        }
    }

    // ------------------------------------------------------------ readers / protocol

    private void ReadStdoutLoop(
        Process process,
        long generation,
        TextReader stdout,
        CancellationToken ct)
    {
        try
        {
            // stdout 是机器协议：逐行 NDJSON，不得 ReadToEnd。
            while (!ct.IsCancellationRequested)
            {
                var line = stdout.ReadLine();
                if (line is null)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        MarkTransportFault(process, generation, "stdout EOF");
                    }

                    return;
                }

                // 空白行不承载协议数据，视为可恢复的 framing noise。
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var responseBytes = (long)Encoding.UTF8.GetByteCount(line) + 1;
                var maxResponseBytes = Math.Max(1, _settings.PythonMaxResponseBytes);
                if (responseBytes > maxResponseBytes)
                {
                    _logger.Warning(
                        "bridge stdout response exceeded configured limit; bytes="
                            + responseBytes + ", limit=" + maxResponseBytes + ".",
                        category: "pythonbridge");
                    MarkTransportFault(
                        process,
                        generation,
                        "stdout response exceeded configured byte limit",
                        ErrorCodes.PythonOutputLimitExceeded);
                    return;
                }

                // D-018 A 节：区分「传输真不可用」与「单帧不可解析」。
                // 帧污染（如历史 NaN 字面量）属**响应级**问题，不得降级为"bridge 不可用"语义；
                // 但流已失同步，仍需回收进程——仅错误语义与文案按响应级上报（不得放宽 JSON 校验）。
                var response = PythonBridgeResponse.TryParse(line);
                if (response is null)
                {
                    _logger.Warning(
                        "bridge stdout response frame unparsable (response-level); process recycled, transport-unavailable semantics NOT applied.",
                        category: "pythonbridge");
                    MarkTransportFault(
                        process,
                        generation,
                        "response frame is not valid JSON (transport recycling; not a bridge-unavailable condition)",
                        ErrorCodes.PythonProtocolError,
                        responseLevelMessage: true);
                    return;
                }

                if (!HasExpectedResponseSchema(response))
                {
                    _logger.Warning(
                        "bridge stdout response schema invalid; process marked unsafe.",
                        category: "pythonbridge");
                    MarkTransportFault(
                        process,
                        generation,
                        "stdout JSON does not match the bridge response schema",
                        ErrorCodes.PythonProtocolError);
                    return;
                }

                if (!TryGetCorrelationGeneration(response.Id!, out var responseGeneration))
                {
                    _logger.Warning(
                        "bridge stdout response has invalid correlation id; process marked unsafe.",
                        category: "pythonbridge");
                    MarkTransportFault(
                        process,
                        generation,
                        "stdout response correlation id is invalid",
                        ErrorCodes.PythonProtocolError);
                    return;
                }

                if (responseGeneration != generation)
                {
                    _logger.Warning(
                        "bridge response for old or foreign generation dropped: " + response.Id,
                        category: "pythonbridge");
                    continue;
                }

                PendingRequest? pending = null;
                lock (_gate)
                {
                    _pending.TryGetValue(response.Id!, out pending);
                }

                if (pending is null)
                {
                    _logger.Warning(
                        "bridge response for unknown or completed id dropped: " + response.Id,
                        category: "pythonbridge");
                    continue;
                }

                pending.Completion.TrySetResult(response);
            }
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                MarkTransportFault(process, generation, "stdout reader stopped: " + ex.Message);
            }
        }
    }

    private void ReadStderrLoop(
        TextReader stderr,
        CancellationToken ct,
        StderrLogBudget logBudget)
    {
        // 持续消费 stderr，防 buffer 满阻塞 Python；日志进入 ILogger。
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var text = stderr.ReadLine();
                if (text is null)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                // stderr 必须持续 drain；超出 logging budget 只丢弃记录，不能停止 reader。
                // 预算应用在最终 logger 文本上，包含 traceback 的固定前缀。
                var isRunnerDiagnostic = text.StartsWith(
                    "bridge_runner",
                    StringComparison.OrdinalIgnoreCase);
                var isTraceback = text.Contains(
                    "Traceback",
                    StringComparison.OrdinalIgnoreCase);
                var logText = isTraceback
                    ? "bridge stderr traceback: " + text
                    : text;
                var boundedText = logBudget.Take(logText);
                if (boundedText is null)
                {
                    continue;
                }

                if (isRunnerDiagnostic)
                {
                    _logger.Debug(boundedText, category: "pythonbridge");
                }
                else if (isTraceback)
                {
                    _logger.Warning(boundedText, category: "pythonbridge");
                }
                else
                {
                    _logger.Info(boundedText, category: "pythonbridge");
                }
            }
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                _logger.Warning(
                    "bridge stderr reader stopped: " + ex.Message,
                    category: "pythonbridge");
            }
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (sender is not Process process)
        {
            return;
        }

        List<PendingRequest> toFail;
        PythonBridgeState state;
        lock (_gate)
        {
            // 旧 Process 的延迟 Exited 事件不得影响新 generation。
            if (!ReferenceEquals(process, _process))
            {
                return;
            }

            toFail = TakePendingUnsafe();
            if (_state is not (PythonBridgeState.Stopping or PythonBridgeState.Disposed))
            {
                _state = PythonBridgeState.Faulted;
                _lastSafeErrorCode = ErrorCodes.PythonBridgeUnavailable;
            }

            state = _state;
        }

        _logger.Warning(
            "python bridge process exited (pending=" + toFail.Count + ").",
            category: "pythonbridge");

        FailPending(
            toFail,
            new PythonBridgeTransportException(
                "Python bridge process exited unexpectedly; state=" + state + "."));
    }

    private void MarkTransportFault(
        Process process,
        long generation,
        string reason,
        string code = ErrorCodes.PythonBridgeUnavailable,
        bool responseLevelMessage = false)
    {
        List<PendingRequest> toFail;
        lock (_gate)
        {
            if (!ReferenceEquals(process, _process)
                || _generation != generation
                || _state is PythonBridgeState.Stopping or PythonBridgeState.Disposed)
            {
                return;
            }

            _state = PythonBridgeState.Faulted;
            _lastSafeErrorCode = SafeErrorCode(code);
            toFail = TakePendingUnsafe();
        }

        _logger.Warning("Python Bridge transport fault: " + reason, category: "pythonbridge");
        FailPending(
            toFail,
            new PythonBridgeTransportException(
                responseLevelMessage
                    // D-018：帧污染按响应级错误上报——不得声称"bridge 不可用"。
                    ? "Python Bridge response frame invalid: " + reason
                    : "Python Bridge transport became unavailable: " + reason,
                code));
    }

    // ------------------------------------------------------------ pending / correlation

    private PendingRequest RegisterPending(
        PythonBridgeRequest request,
        long generation,
        bool startupProbe)
    {
        var pending = new PendingRequest(request.Id, generation, startupProbe);
        lock (_gate)
        {
            if (_pending.ContainsKey(request.Id))
            {
                throw new InvalidOperationException(
                    "Duplicate Python Bridge correlation id: " + request.Id);
            }

            _pending.Add(request.Id, pending);
        }

        return pending;
    }

    private DispatchState RemovePending(PendingRequest pending)
    {
        lock (_gate)
        {
            if (_pending.TryGetValue(pending.Id, out var existing)
                && ReferenceEquals(existing, pending))
            {
                _pending.Remove(pending.Id);
                var state = pending.DispatchState;
                pending.DispatchState = DispatchState.Completed;
                return state;
            }

            return pending.DispatchState;
        }
    }

    private void SetDispatchState(PendingRequest pending, DispatchState state)
    {
        lock (_gate)
        {
            if (_pending.TryGetValue(pending.Id, out var existing)
                && ReferenceEquals(existing, pending))
            {
                pending.DispatchState = state;
            }
        }
    }

    private List<PendingRequest> TakePendingUnsafe()
    {
        var pending = new List<PendingRequest>(_pending.Values);
        _pending.Clear();
        foreach (var item in pending)
        {
            item.DispatchState = DispatchState.Completed;
        }

        return pending;
    }

    private static void FailPending(
        IEnumerable<PendingRequest> pending,
        PythonBridgeTransportException failure)
    {
        foreach (var item in pending)
        {
            item.Completion.TrySetException(failure);
        }
    }

    private static string SafeErrorCode(string code)
        => code is ErrorCodes.PythonBridgeUnavailable
            or ErrorCodes.PythonTimeout
            or ErrorCodes.PythonOutputLimitExceeded
            or ErrorCodes.PythonProtocolError
            or ErrorCodes.InternalError
            ? code
            : ErrorCodes.PythonBridgeUnavailable;

    private string CreateCorrelationId(long generation)
    {
        var sequence = Interlocked.Increment(ref _nextSequence);
        return "py-" + generation + "-" + sequence;
    }

    private static bool HasExpectedResponseSchema(PythonBridgeResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Id))
        {
            return false;
        }

        if (response.Ok)
        {
            return response.Error is null && response.Result is not null;
        }

        return response.Error is { } error
            && !string.IsNullOrWhiteSpace(error.Code)
            && !string.IsNullOrWhiteSpace(error.Message);
    }

    private static bool TryGetCorrelationGeneration(
        string id,
        out long generation)
    {
        generation = 0;
        var parts = id.Split('-');
        if (parts.Length != 3
            || !string.Equals(parts[0], "py", StringComparison.Ordinal)
            || !long.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out generation)
            || generation <= 0
            || !long.TryParse(
                parts[2],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var sequence)
            || sequence <= 0)
        {
            generation = 0;
            return false;
        }

        return true;
    }

    private static bool IsPossiblyDispatched(DispatchState state)
        => state is DispatchState.Dispatching or DispatchState.Dispatched;

    private static PythonBridgeCallResult ToCallResult(PythonBridgeResponse response)
    {
        if (response.IsOk && response.Result is { } data)
        {
            return PythonBridgeCallResult.Succeeded(data);
        }

        var error = response.Error;
        var code = string.IsNullOrWhiteSpace(error?.Code)
            ? ErrorCodes.InternalError
            : error!.Code!;
        var message = string.IsNullOrWhiteSpace(error?.Message)
            ? "Python bridge error"
            : error!.Message!;
        return PythonBridgeCallResult.Failed(code, message);
    }

    private static string CancellationCode(
        CancellationToken callerToken,
        CancellationTokenSource pythonTimeoutCts)
        => callerToken.IsCancellationRequested
            ? ErrorCodes.Cancelled
            : pythonTimeoutCts.IsCancellationRequested
                ? ErrorCodes.PythonTimeout
                : ErrorCodes.Cancelled;

    private static string CancellationMessage(
        CancellationToken callerToken,
        CancellationTokenSource pythonTimeoutCts)
        => callerToken.IsCancellationRequested
            ? "Python Bridge request cancelled."
            : pythonTimeoutCts.IsCancellationRequested
                ? "Python Bridge request deadline exceeded."
                : "Python Bridge request cancelled.";

    // ------------------------------------------------------------ bounded cleanup

    private async Task<bool> CleanupProcessLockedAsync(string reason)
    {
        Process? process;
        StreamWriter? stdin;
        TextReader? stdout;
        TextReader? stderr;
        Task? stdoutTask;
        Task? stderrTask;
        CancellationTokenSource? readerCts;
        List<PendingRequest> pending;

        lock (_gate)
        {
            process = _process;
            stdin = _stdin;
            stdout = _stdout;
            stderr = _stderr;
            stdoutTask = _stdoutTask;
            stderrTask = _stderrTask;
            readerCts = _readerCts;
            pending = TakePendingUnsafe();
        }

        FailPending(
            pending,
            new PythonBridgeTransportException(
                "Python Bridge stopped while request pending; reason=" + reason + "."));

        readerCts?.Cancel();
        DisposeQuietly(stdin);
        DisposeQuietly(stdout);
        DisposeQuietly(stderr);

        var clean = process is null;
        if (process is not null)
        {
            clean = await TerminateAndWaitAsync(
                process,
                stdoutTask,
                stderrTask,
                readerCts,
                reason).ConfigureAwait(false);
        }
        else
        {
            await WaitReaderTasksAsync(
                stdoutTask,
                stderrTask,
                _settings.PythonProcessShutdownTimeoutMs).ConfigureAwait(false);
            DisposeQuietly(readerCts);
        }

        var processExited = process is null || SafeHasExited(process);
        if (process is not null && processExited)
        {
            DisposeQuietly(process);
        }

        lock (_gate)
        {
            if (ReferenceEquals(_process, process))
            {
                if (processExited)
                {
                    _process = null;
                    _generation = 0;
                }

                _stdin = null;
                _stdout = null;
                _stderr = null;
                _stdoutTask = null;
                _stderrTask = null;
                _readerCts = null;
            }
        }

        DisposeQuietly(readerCts);
        return clean && processExited;
    }

    private async Task<bool> TerminateAndWaitAsync(
        Process process,
        Task? stdoutTask,
        Task? stderrTask,
        CancellationTokenSource? readerCts,
        string reason)
    {
        var timeoutMs = Math.Max(1, _settings.PythonProcessShutdownTimeoutMs);
        var stopwatch = Stopwatch.StartNew();
        var totalBudget = TimeSpan.FromMilliseconds(timeoutMs);
        var processExited = SafeHasExited(process);
        Task? exitTask = null;

        try
        {
            process.Exited -= OnProcessExited;
        }
        catch
        {
            // ignore
        }

        if (!processExited)
        {
            try
            {
                exitTask = process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    "bridge process wait setup failed: " + ex.Message,
                    category: "pythonbridge");
            }

            var gracefulBudget = TimeSpan.FromMilliseconds(Math.Max(1, timeoutMs / 2));
            await WaitTaskBoundedAsync(exitTask, gracefulBudget).ConfigureAwait(false);
            processExited = SafeHasExited(process);

            if (!processExited)
            {
                _logger.Warning(
                    "killing Python Bridge process: " + reason,
                    category: "pythonbridge");
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    processExited = SafeHasExited(process);
                }
                catch (Exception ex)
                {
                    _logger.Warning(
                        "kill bridge process failed: " + ex.Message,
                        category: "pythonbridge");
                }
            }

            var remaining = Remaining(stopwatch, totalBudget);
            if (!processExited && remaining > TimeSpan.Zero)
            {
                await WaitTaskBoundedAsync(exitTask, remaining).ConfigureAwait(false);
                processExited = SafeHasExited(process);
            }
        }

        readerCts?.Cancel();
        var readerRemaining = Remaining(stopwatch, totalBudget);
        await WaitReaderTasksAsync(
            stdoutTask,
            stderrTask,
            (int)Math.Max(1, readerRemaining.TotalMilliseconds)).ConfigureAwait(false);

        processExited = processExited || SafeHasExited(process);
        if (!processExited)
        {
            _logger.Warning(
                "Python Bridge process still alive after bounded cleanup.",
                category: "pythonbridge");
        }

        return processExited;
    }

    private static async Task WaitReaderTasksAsync(
        Task? stdoutTask,
        Task? stderrTask,
        int timeoutMs)
    {
        var totalBudget = TimeSpan.FromMilliseconds(Math.Max(1, timeoutMs));
        var stopwatch = Stopwatch.StartNew();
        await WaitTaskBoundedAsync(
            stdoutTask,
            Remaining(stopwatch, totalBudget)).ConfigureAwait(false);
        await WaitTaskBoundedAsync(
            stderrTask,
            Remaining(stopwatch, totalBudget)).ConfigureAwait(false);
    }

    private static async Task WaitTaskBoundedAsync(Task? task, TimeSpan timeout)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch
        {
            // teardown is bounded; caller decides whether process exit was confirmed
        }
    }

    private static TimeSpan Remaining(Stopwatch stopwatch, TimeSpan total)
    {
        var remaining = total - stopwatch.Elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    // ------------------------------------------------------------ state / utilities

    private bool TryEnterOperation()
    {
        lock (_gate)
        {
            if (_disposed || _disposing)
            {
                return false;
            }

            _activeOperations++;
            return true;
        }
    }

    private void ThrowIfOperationNotAllowed()
    {
        if (!TryEnterOperation())
        {
            throw new ObjectDisposedException(nameof(PythonBridgeProcessManager));
        }
    }

    private void ExitOperation()
    {
        lock (_gate)
        {
            if (_activeOperations > 0)
            {
                _activeOperations--;
            }

            if (_disposing && _activeOperations == 0)
            {
                _operationsDrained.TrySetResult(null);
            }
        }
    }

    private bool IsAcceptingRequests()
    {
        lock (_gate)
        {
            return !_disposing && !_disposed && _state == PythonBridgeState.Running;
        }
    }

    private bool HasResources()
    {
        lock (_gate)
        {
            return HasResourcesUnsafe();
        }
    }

    private bool HasResourcesUnsafe()
        => _process is not null
            || _stdin is not null
            || _stdout is not null
            || _stderr is not null
            || _stdoutTask is not null
            || _stderrTask is not null
            || _readerCts is not null
            || _pending.Count > 0;

    private static bool SafeHasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static void DisposeQuietly(IDisposable? disposable)
    {
        try
        {
            disposable?.Dispose();
        }
        catch
        {
            // ignore teardown errors
        }
    }

    private (string exe, string script, string workdir) ResolvePaths()
    {
        if (RuntimePathErrorCodes.IsSafe(_settings.RuntimePathErrorCode))
        {
            throw new PythonBridgeTransportException(
                "Runtime path resolution failed.",
                _settings.RuntimePathErrorCode!);
        }

        var exe = _settings.PythonExecutable;
        if (string.IsNullOrWhiteSpace(exe))
        {
            throw new PythonBridgeTransportException(
                "Python executable is not configured.",
                RuntimePathErrorCodes.PythonExecutableMissing);
        }

        var script = _settings.PythonBridgeScript;
        if (string.IsNullOrWhiteSpace(script))
        {
            throw new PythonBridgeTransportException(
                "Python Bridge script is not configured.",
                RuntimePathErrorCodes.BridgeScriptMissing);
        }

        if (!Path.IsPathRooted(script))
        {
            script = Path.GetFullPath(Path.Combine(
                string.IsNullOrWhiteSpace(_settings.PythonWorkingDirectory)
                    ? Directory.GetCurrentDirectory()
                    : _settings.PythonWorkingDirectory,
                script));
        }

        return (exe, script, _settings.PythonWorkingDirectory);
    }
}
