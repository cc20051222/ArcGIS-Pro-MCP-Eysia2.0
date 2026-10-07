using System.Diagnostics;
using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Server;

namespace ArcGISProMCP.ServerTests;

/// <summary>
/// D-056 C1（13.2 挂账兑现；G-147 登记「等待队列无深度上限」→ G-149 批准执行）行为测试。
/// <para>
/// 契约面：队列深度**超出上限**时**不等待**直接拒绝，复用注册错误码 <c>REQUEST_TIMEOUT</c>
/// （以 <c>(queue-full)</c> 限定词与既有「等待超时」<c>(queued)</c> 区分）；错误码注册表保持 **33 项不变**。
/// </para>
/// <para><c>MaxQueuedRequests = 0</c> 表示**不设上限**（历史行为，向后兼容）。</para>
/// </summary>
public sealed class D056QueueLimitTests
{
    private static async Task<JsonElement> CallAsync(McpServer server, string json)
    {
        var response = await server.HandleRequestAsync(json);
        Assert.NotNull(response);
        return JsonDocument.Parse(response!).RootElement;
    }

    private static string CallJson(int id, string tool) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"tools/call\",\"params\":{\"name\":\"" + tool + "\",\"arguments\":{}}}";

    /// <summary>阻塞工具：进入即置位 <see cref="Started"/>，直到 <see cref="Release"/> 才返回（用于占住并发槽）。</summary>
    private sealed class BlockingTool : IMCPTool
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "block_probe";

        public string Description => "D-056 C1：队列上限测试用阻塞工具";

        public IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>(),
        };

        public Task Started => _started.Task;

        public void Release() => _release.TrySetResult();

        public async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        {
            _started.TrySetResult();
            await _release.Task.ConfigureAwait(false);
            return OperationResult<object?>.Ok("released");
        }
    }

    [Fact]
    public async Task QueueFull_ExcessWaiter_IsRejectedWithoutWaiting()
    {
        var tool = new BlockingTool();
        var settings = new MCPSettings { MaxConcurrentRequests = 1, MaxQueuedRequests = 1, RequestTimeoutMs = 30000 };
        var server = TestServerFactory.CreateWithSettings(settings, tool);

        var inflight = CallAsync(server, CallJson(1, "block_probe"));   // 占用唯一槽位（永久阻塞）
        await tool.Started.WaitAsync(TimeSpan.FromSeconds(10));

        var queued = CallAsync(server, CallJson(2, "block_probe"));     // 第 1 个等待者（= 上限，允许）
        await Task.Delay(300);                                          // 确保其已登记为等待者

        var sw = Stopwatch.StartNew();
        var overflow = await CallAsync(server, CallJson(3, "block_probe"));   // 超出上限 ⇒ 应即刻拒绝
        sw.Stop();

        var text = overflow.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();
        Assert.True(overflow.GetProperty("result").GetProperty("isError").GetBoolean(),
            "队列超限必须报错（isError=true）");
        Assert.Contains("REQUEST_TIMEOUT (queue-full)", text);
        Assert.Contains("queue depth limit (1)", text);
        Assert.True(sw.ElapsedMilliseconds < 3000,
            $"超限拒绝必须**不等待**（实测 {sw.ElapsedMilliseconds} ms；若等待将一直阻塞到 RequestTimeoutMs=30s）");

        tool.Release();
        var inflightRoot = await inflight;
        var queuedRoot = await queued;
        Assert.False(inflightRoot.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.False(queuedRoot.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task QueueLimitZero_MeansUnlimited_ExcessWaiterIsAdmitted()
    {
        var tool = new BlockingTool();
        var settings = new MCPSettings { MaxConcurrentRequests = 1, MaxQueuedRequests = 0, RequestTimeoutMs = 30000 };
        var server = TestServerFactory.CreateWithSettings(settings, tool);

        var inflight = CallAsync(server, CallJson(11, "block_probe"));
        await tool.Started.WaitAsync(TimeSpan.FromSeconds(10));

        var queued1 = CallAsync(server, CallJson(12, "block_probe"));
        await Task.Delay(300);
        var queued2 = CallAsync(server, CallJson(13, "block_probe"));   // 0 = 不设上限 ⇒ 必须被接受排队（历史行为）
        await Task.Delay(300);

        tool.Release();
        var r1 = await inflight;
        var r2 = await queued1;
        var r3 = await queued2;
        foreach (var root in new[] { r1, r2, r3 })
        {
            Assert.False(root.GetProperty("result").GetProperty("isError").GetBoolean(),
                "MaxQueuedRequests=0 时不得出现队列超限拒绝");
        }
    }

    [Fact]
    public async Task DefaultSettings_ConcurrentCalls_AllSucceed()
    {
        var server = TestServerFactory.Create();   // 默认 MaxConcurrentRequests=8 / MaxQueuedRequests=32
        var calls = Enumerable.Range(1, 8).Select(i => CallAsync(server, CallJson(100 + i, "ping"))).ToArray();
        var roots = await Task.WhenAll(calls);
        foreach (var root in roots)
        {
            Assert.False(root.GetProperty("result").GetProperty("isError").GetBoolean());
            Assert.Equal("pong", root.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
        }
    }
}
