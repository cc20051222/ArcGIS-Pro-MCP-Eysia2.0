using System.Text;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// 统一 Geoprocessing 执行器：封装 ArcGIS Pro Geoprocessing API。
/// 保留 GP 消息、警告、错误与输出信息。
/// </summary>
public sealed class GeoprocessingExecutor
{
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
            null,
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
            null,                        // GPToolExecuteEventHandler
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
