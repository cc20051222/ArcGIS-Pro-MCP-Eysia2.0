using System.Text;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGISProMCP.Core.Progress;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// 统一 Geoprocessing 执行器：封装 ArcGIS Pro Geoprocessing API。
/// 保留 GP 消息、警告、错误与输出信息。
/// </summary>
public sealed class GeoprocessingExecutor
{
    private static GPToolExecuteEventHandler ProgressHandler(string toolName)
    {
        var reporter = ToolProgress.Current;
        var stage = "gp:" + toolName + ":" + Guid.NewGuid().ToString("N")[..8];
        return (eventName, value) =>
        {
            switch (eventName)
            {
                case "OnBeginExecute":
                    reporter?.Report(stage, "RUNNING", "GP 开始执行：" + toolName, source: "ArcGIS.GP.OnBeginExecute");
                    break;
                case "OnProgressPos" when value is int position && position >= 0 && position <= 100:
                    reporter?.Report(stage, "RUNNING", toolName + " 当前 GP 阶段 " + position + "%（不是整单完成率）",
                        position, 100, "ArcGIS.GP.OnProgressPos");
                    break;
                case "OnProgressMessage" when value is string message:
                    reporter?.Report(stage, "RUNNING", message, source: "ArcGIS.GP.OnProgressMessage");
                    break;
                case "OnValidate":
                    reporter?.Report(stage, "RUNNING", "GP 参数校验事件已返回", source: "ArcGIS.GP.OnValidate");
                    break;
                case "OnEndExecute":
                    reporter?.Report(stage, "RUNNING", "GP 阶段已返回，等待工具结果判定", source: "ArcGIS.GP.OnEndExecute");
                    break;
            }
        };
    }
    public sealed class GpExecution
    {
        public bool Success { get; init; }
        public bool Cancelled { get; init; }
        public string? ReturnValue { get; init; }
        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> ErrorMessages { get; init; } = Array.Empty<string>();
    }

    /// <summary>对象参数重载（D-014 方案 A）。注意：Pro 3.5 SDK 的 ExecuteToolAsync 仅接受
    /// IEnumerable&lt;string&gt;，Layer 值在此折算为其名称字符串——GP 按**活动地图**工程内容解析图层名，
    /// 因此服务层必须校验目标地图=活动地图（见 GeoprocessingService 守卫）。</summary>
    public static async Task<GpExecution> ExecuteAsync(string toolName, IReadOnlyList<object> values, CancellationToken ct)
    {
        var inputs = new List<string>();
        foreach (var v in values ?? Array.Empty<object>())
        {
            inputs.Add(v switch
            {
                null => string.Empty,
                string str => str,
                ArcGIS.Desktop.Mapping.Layer lyr => lyr.Name ?? string.Empty,
                _ => v.ToString() ?? string.Empty,
            });
        }

        var result = await Geoprocessing.ExecuteToolAsync(
            toolName,
            inputs,
            null,
            ct,
            ProgressHandler(toolName),
            GPExecuteToolFlags.Default).ConfigureAwait(false);

        var messagesObj = result.Messages?.Select(m => m?.ToString() ?? string.Empty).ToList() ?? new List<string>();

        return new GpExecution
        {
            Success = result.IsFailed == false,
            Cancelled = result.IsCanceled,
            ReturnValue = result.ReturnValue?.ToString(),
            Messages = messagesObj,
            ErrorMessages = result.ErrorMessages?.Select(m => m?.ToString() ?? string.Empty).ToList() ?? new List<string>()
        };
    }

    public static async Task<GpExecution> ExecuteAsync(string toolName, IReadOnlyList<string> values, CancellationToken ct)
    {
        var inputs = new List<string>(values ?? Array.Empty<string>());
        var result = await Geoprocessing.ExecuteToolAsync(
            toolName,
            inputs,
            D064GpEnvironment.BuildEnvironment(),   // D-064：会话级 GP 环境下发（无设置 ⇒ null，保持既有行为不变）
            ct,                          // CancellationToken
            ProgressHandler(toolName),    // Captured request-scoped SDK progress reporter.
            GPExecuteToolFlags.Default).ConfigureAwait(false);

        var messages = result.Messages?.Select(m => m?.ToString() ?? string.Empty).ToList() ?? new List<string>();

        return new GpExecution
        {
            Success = result.IsFailed == false,
            Cancelled = result.IsCanceled,
            ReturnValue = result.ReturnValue?.ToString(),
            Messages = messages,
            ErrorMessages = result.ErrorMessages?.Select(m => m?.ToString() ?? string.Empty).ToList() ?? new List<string>()
        };
    }
}
