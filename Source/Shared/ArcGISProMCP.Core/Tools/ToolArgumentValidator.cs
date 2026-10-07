using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Tools;

/// <summary>
/// D-064：<c>required</c> 参数前置校验的**共享实现**（路由层与 <c>run_batch</c> 批内逐项共用同一实现，
/// 保证"批内不豁免"是**代码级**保证而非口头承诺）。纯函数：无 IO、无状态。
/// </summary>
public static class ToolArgumentValidator
{
    /// <summary>校验 <c>required</c> 声明；通过 ⇒ null，否则返回错误。</summary>
    public static OperationError? Validate(IMCPTool tool, IReadOnlyDictionary<string, object?>? arguments)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (!tool.InputSchema.TryGetValue("required", out var requiredRaw) || requiredRaw is not IEnumerable<object?> required)
        {
            return null;
        }

        var requiredNames = required.OfType<string>().Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
        if (requiredNames.Length == 0)
        {
            return null;
        }

        arguments ??= new Dictionary<string, object?>();

        foreach (var name in requiredNames)
        {
            if (!arguments.TryGetValue(name, out var value) || value is null)
            {
                return new OperationError(
                    ErrorCodes.InvalidArgument, $"Missing required argument '{name}' for tool '{tool.Name}'.");
            }
        }

        return null;
    }
}
