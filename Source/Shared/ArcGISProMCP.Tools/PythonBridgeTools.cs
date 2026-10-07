using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>Python Bridge Ping：通过 PythonBridgeService → ProcessManager → python → bridge_runner 返回 pong。</summary>
public sealed class PythonBridgePingTool : McpToolBase
{
    public override string Name => "python_bridge_ping";
    public override string Description => "检查 Python Bridge（python + bridge_runner + ArcPy 环境）是否可用，返回 pong 或错误。";
    public override IReadOnlyDictionary<string, object?> InputSchema => ToolSchemas.Object();
    protected override string CategoryName => ToolCategories.Python;
    protected override string ExecutionTypeName => ExecutionTypes.Python;
    protected override bool? RequiresArcGISOverride => false;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var bridge = context.Python;
        if (bridge is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PythonBridgeUnavailable, "Python Bridge service is not available in this host.");
        }

        var r = await bridge.PingAsync(context.CancellationToken).ConfigureAwait(false);
        return r.Success
            ? OperationResult<object?>.Ok(r.Data)
            : OperationResult<object?>.Fail(r.Errors[0]);
    }
}

/// <summary>Python Runtime Info：获取 Python / ArcPy / ArcGIS API 运行时信息。</summary>
public sealed class PythonRuntimeInfoTool : McpToolBase
{
    public override string Name => "python_runtime_info";
    public override string Description => "返回 Python Bridge 运行时信息：Python 版本 / ArcPy 版本(3.5) / arcgis 版本(2.4.1) 等。";
    public override IReadOnlyDictionary<string, object?> InputSchema => ToolSchemas.Object();
    protected override string CategoryName => ToolCategories.Python;
    protected override string ExecutionTypeName => ExecutionTypes.Python;
    protected override bool? RequiresArcGISOverride => false;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var bridge = context.Python;
        if (bridge is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PythonBridgeUnavailable, "Python Bridge service is not available in this host.");
        }

        var r = await bridge.GetRuntimeInfoAsync(context.CancellationToken).ConfigureAwait(false);
        if (!r.Success)
        {
            return OperationResult<object?>.Fail(r.Errors[0]);
        }

        return OperationResult<object?>.Ok(r.Data.HasValue ? r.Data.Value : JsonValueKind.Null);
    }
}
