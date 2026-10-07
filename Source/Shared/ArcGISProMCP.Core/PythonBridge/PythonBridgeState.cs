namespace ArcGISProMCP.Core.PythonBridge;

/// <summary>Python Bridge 进程生命周期状态。</summary>
public enum PythonBridgeState
{
    /// <summary>未启动 / 已停止。</summary>
    Stopped,

    /// <summary>正在启动（Python + ArcPy import ≈8s）。</summary>
    Starting,

    /// <summary>运行中，可接收请求。</summary>
    Running,

    /// <summary>正在停止（graceful shutdown 等待中）。</summary>
    Stopping,

    /// <summary>异常状态（进程退出 / stdout EOF / 崩溃）。可 Restart。</summary>
    Faulted,

    /// <summary>已释放（dispose 后不可再用）。</summary>
    Disposed,
}
