namespace ArcGISProMCP.Server.Internal;

/// <summary>
/// D-083 组 B——重放抵抗（ADR D-106「重放校验」面）。
/// <para>
/// 语义：<c>requestId</c> 一次性 + TTL 记账；同 <c>requestId</c> 第二次出现即拒绝。
/// 容量上限（<see cref="PsChannelPolicy.MaxReplayEntries"/>）触顶时 **fail-closed 拒绝**而非淘汰最旧项
/// ——淘汰最旧项会让攻击者用填充把历史 id 挤出窗口从而重放成功。
/// </para>
/// <para>仅在内存，不落盘（零副作用：拒绝路径不写任何文件/队列）。</para>
/// </summary>
public sealed class PsReplayGuard
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _seen = new(StringComparer.Ordinal);

    public PsReplayGuard(int capacity)
    {
        _capacity = capacity > 0 ? capacity : PsChannelPolicy.DefaultMaxReplayEntries;
    }

    /// <summary>当前记账条数（仅测试/诊断用）。</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _seen.Count;
            }
        }
    }

    /// <summary>尝试登记一次 requestId。首次 ⇒ true；重复/容量触顶 ⇒ false（调用方必须拒绝且零副作用）。</summary>
    public bool TryRegister(string requestId, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(requestId))
        {
            return false;
        }

        var nowTicks = now.UtcTicks;
        lock (_gate)
        {
            if (_seen.TryGetValue(requestId, out var expiryTicks))
            {
                if (expiryTicks > nowTicks)
                {
                    return false;   // 重放
                }

                _seen.Remove(requestId);   // 过期条目可回收
            }

            PruneExpired(nowTicks);

            if (_seen.Count >= _capacity)
            {
                return false;   // fail-closed：容量触顶不淘汰
            }

            _seen[requestId] = nowTicks + TimeSpan.FromHours(1).Ticks;
            return true;
        }
    }

    private void PruneExpired(long nowTicks)
    {
        if (_seen.Count == 0)
        {
            return;
        }

        List<string>? expired = null;
        foreach (var pair in _seen)
        {
            if (pair.Value <= nowTicks)
            {
                (expired ??= new List<string>()).Add(pair.Key);
            }
        }

        if (expired is null)
        {
            return;
        }

        foreach (var key in expired)
        {
            _seen.Remove(key);
        }
    }
}
