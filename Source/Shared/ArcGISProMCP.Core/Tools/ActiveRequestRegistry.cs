using System.Collections.Concurrent;

namespace ArcGISProMCP.Core.Tools;

/// <summary>
/// 活动请求注册表（Phase 8.4，D-016）：requestId → CTS，供 notifications/cancelled 取消。
/// 线程安全；请求完成必须 Remove（弱引用语义：完成即移除，取消迟到则幂等忽略）。
/// </summary>
public sealed class ActiveRequestRegistry
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _requests = new(StringComparer.Ordinal);

    /// <summary>注册活动请求（requestId 非空才有效）。</summary>
    public void Register(string? requestId, CancellationTokenSource cts)
    {
        if (string.IsNullOrEmpty(requestId))
        {
            return;
        }

        _requests[requestId] = cts;
    }

    /// <summary>取消指定请求；未知/已完成 → false（幂等忽略）。</summary>
    public bool Cancel(string? requestId)
    {
        if (string.IsNullOrEmpty(requestId))
        {
            return false;
        }

        if (_requests.TryRemove(requestId, out var cts))
        {
            cts.Cancel();
            return true;
        }

        return false;
    }

    /// <summary>请求完成移除（finally；与 Cancel 竞态时先到先得，均为幂等）。</summary>
    public void Remove(string? requestId)
    {
        if (string.IsNullOrEmpty(requestId))
        {
            return;
        }

        _requests.TryRemove(requestId, out _);
    }

    public int Count => _requests.Count;
}
