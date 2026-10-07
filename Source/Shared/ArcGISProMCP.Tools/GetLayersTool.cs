using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>获取图层列表工具。</summary>
public sealed class GetLayersTool : IMCPTool
{
    public string Name => "get_layers";

    public string Description => "返回指定地图（默认当前地图）的图层列表。默认展开组合图层内的子图层（flatten=true，G-06）；传 flatten=false 可仅返回顶层并保留层级结构。参数：mapName（可选）、flatten（可选，默认 true）。";

    public IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "Optional map name. When omitted, the current map is used."
            },
            ["flatten"] = new Dictionary<string, object?>
            {
                ["type"] = "boolean",
                ["default"] = true,
                ["description"] = "When true (default), returns a flattened list including nested group-layer children. When false, returns top-level layers only, preserving group hierarchy."
            }
        }
    };

    public async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        string? mapName = null;
        if (context.Arguments is not null && context.Arguments.TryGetValue("mapName", out var value) && value is string s)
        {
            mapName = s;
        }

        // G-06：默认展开组合图层。缺省或非法值一律按 true（缺省即默认行为，不因非法输入报错）。
        var flatten = ToolArgs.GetBool(context, "flatten", defaultValue: true);

        var result = await context.Host.Layers.GetLayersAsync(mapName, flatten, context.CancellationToken).ConfigureAwait(false);
        return result.Success
            ? OperationResult<object?>.Ok(result.Data, result.Message)
            : OperationResult<object?>.Fail(result.Errors);
    }
}
