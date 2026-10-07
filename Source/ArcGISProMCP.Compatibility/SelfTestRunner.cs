using System.Diagnostics;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;

namespace ArcGISProMCP.Compatibility;

/// <summary>
/// 内部集成自测：通过 MCPToolRouter 真实执行内置工具，但只把固定的
/// 测试标识、状态、耗时和计数写入 production managed sink。
/// </summary>
internal static class SelfTestRunner
{
    private static readonly string[] ToolNames =
    {
        "ping", "get_current_map", "get_layers", "get_project_info", "get_arcgis_version", "get_license_info"
    };

    public static async Task<string> RunAllAsync()
    {
        try
        {
            // 等待工程与地图视图就绪。
            await Task.Delay(TimeSpan.FromSeconds(12), CancellationToken.None).ConfigureAwait(false);

            var settings = Composition.Container.Resolve<MCPSettings>();
            var router = Composition.BuildRouter(settings, Composition.ServerLogger);
            var passed = 0;
            var failed = 0;

            foreach (var name in ToolNames)
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await router.ExecuteAsync(new MCPToolCall { Name = name }).ConfigureAwait(false);
                stopwatch.Stop();
                if (result.Success)
                {
                    passed++;
                }
                else
                {
                    failed++;
                }

                Composition.ServerLogger.Log(new LogEntry
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Level = result.Success ? LogLevel.Information : LogLevel.Warning,
                    Category = "selftest",
                    Component = "selftest",
                    Operation = "tool-check",
                    CorrelationId = "selftest",
                    Tool = name,
                    MessageCode = "SELFTEST_TOOL_COMPLETED",
                    ErrorCode = result.Success ? "NONE" : "SELFTEST_TOOL_FAILED",
                    Outcome = result.Success ? "SUCCESS" : "FAILURE",
                    DurationMs = Math.Max(0, (long)Math.Round(stopwatch.Elapsed.TotalMilliseconds))
                });
            }

            Composition.ServerLogger.Log(new LogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = failed == 0 ? LogLevel.Information : LogLevel.Warning,
                Category = "selftest",
                Component = "selftest",
                Operation = "run",
                CorrelationId = "selftest",
                MessageCode = "SELFTEST_RUN_COMPLETED",
                ErrorCode = failed == 0 ? "NONE" : "SELFTEST_TOOL_FAILED",
                Outcome = failed == 0 ? "SUCCESS" : "DEGRADED"
            });

            return $"Self-test completed. tests={ToolNames.Length} passed={passed} failed={failed}.";
        }
        catch
        {
            Composition.ServerLogger.Log(new LogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = LogLevel.Error,
                Category = "selftest",
                Component = "selftest",
                Operation = "run",
                CorrelationId = "selftest",
                MessageCode = "SELFTEST_RUN_FAILED",
                ErrorCode = "SELFTEST_RUN_FAILED",
                Outcome = "FAILURE"
            });
            throw;
        }
    }
}
