using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>D-063 · B 段：渲染进阶（4 件新增；get_layer_symbology 为既有件增强，不新增注册）。</summary>
public sealed class SetLayerRendererTool : McpToolBase
{
    public override string Name => "set_layer_renderer";
    public override string Description =>
        "设置图层渲染器（**三模式 · 写操作**）。参数：mapName（可省略）、layerName、mode（single | unique | graduated）、" +
        "field（unique / graduated 必填）、classCount（graduated 分级数，默认 5）、colorRamp（graduated 色带名，可选）。" +
        "**写后读回**：返回 mode / rendererType / field / classCount / colorRamp / applied。" +
        "**非法字段 ⇒ INVALID_ARGUMENT**（字段不存在于图层，如实拒绝、不静默降级）；" +
        "unique / graduated 缺 field ⇒ INVALID_ARGUMENT；未知 mode ⇒ INVALID_ARGUMENT（列出受支持值）；" +
        "非要素图层 ⇒ INVALID_ARGUMENT；SDK 拒收该 renderer ⇒ INVALID_ARGUMENT。" +
        "**就地替换渲染器**：旧渲染器**不自动保存** —— 需要复原请先 `save_layer_file` 留档。" +
        "重名 → AMBIGUOUS_LAYER_NAME；不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
            ["mode"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "single | unique | graduated." },
            ["field"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Classification field (unique/graduated)." },
            ["classCount"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Break count for graduated (default 5)." },
            ["colorRamp"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Color ramp name for graduated." },
        },
        ["required"] = new[] { "layerName", "mode" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layerName = ToolArgs.GetString(context, "layerName");
        var mode = ToolArgs.GetString(context, "mode");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        if (string.IsNullOrWhiteSpace(mode))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "mode is required (single | unique | graduated).");
        }

        var r = await context.Host!.Layers.SetLayerRendererAsync(
            ToolArgs.GetString(context, "mapName"), layerName, mode,
            ToolArgs.GetString(context, "field"),
            ToolArgs.GetInt(context, "classCount"),
            ToolArgs.GetString(context, "colorRamp"),
            context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class ListColorRampsTool : McpToolBase
{
    public override string Name => "list_color_ramps";
    public override string Description =>
        "列出工程可用色带（**只读**）。无参数。返回每条：name / category，以及 styleName 与 totalCount。" +
        "来源：当前工程的样式项（StyleProjectItem）经 SDK `StyleHelper.SearchColorRamps` 枚举。" +
        "无样式项 ⇒ 空清单（totalCount=0，非错误）。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>(),
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var r = await context.Host!.Layers.ListColorRampsAsync(context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class ApplySymbologyFromLayerTool : McpToolBase
{
    public override string Name => "apply_symbology_from_layer";
    public override string Description =>
        "从 .lyrx 图层文件应用符号到目标图层（**写操作 · 只替换渲染器**）。参数：mapName（可省略）、layerName、layerFilePath（.lyrx 路径）。" +
        "**★ 只替换 renderer，不替换数据连接**（目标图层的数据源保持不变）—— 与“导入图层”语义严格区分。" +
        "文件不可读 / 无要素图层 renderer ⇒ INVALID_ARGUMENT（如实拒绝）。非要素图层 ⇒ INVALID_ARGUMENT。" +
        "**就地替换渲染器**：旧渲染器不自动保存 —— 复原请先 `save_layer_file` 留档。" +
        "重名 → AMBIGUOUS_LAYER_NAME；不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target layer name." },
            ["layerFilePath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Path to the source .lyrx file." },
        },
        ["required"] = new[] { "layerName", "layerFilePath" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layerName = ToolArgs.GetString(context, "layerName");
        var path = ToolArgs.GetString(context, "layerFilePath");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerFilePath is required.");
        }

        var r = await context.Host!.Layers.ApplySymbologyFromLayerAsync(
            ToolArgs.GetString(context, "mapName"), layerName, path, context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class SaveLayerFileTool : McpToolBase
{
    public override string Name => "save_layer_file";
    public override string Description =>
        "把图层保存为 .lyrx 图层文件（**写文件 ⇒ 输出路径守卫**）。参数：mapName（可省略）、layerName、outputPath（输出 .lyrx 绝对路径）。" +
        "**★ 输出路径守卫（O-D049-08）**：outputPath 命中受保护根（TestFixtures／旧仓库／ARCGIS_PRO_MCP_PROTECTED_ROOTS）" +
        "⇒ PATH_ESCAPE_REJECTED（**先于任何写入，零产物**）。G-138：请落在 D 盘工作目录（禁 %TEMP%／C 盘）。" +
        "输出内容 = 图层 CIM 定义（CIMLayerDocument JSON），与 Pro 的 .lyrx 同源自洽。" +
        "返回 path / bytes / ok。空白参数 ⇒ INVALID_ARGUMENT。重名 → AMBIGUOUS_LAYER_NAME；不存在 → LAYER_NOT_FOUND。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name to save." },
            ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Absolute output .lyrx path." },
        },
        ["required"] = new[] { "layerName", "outputPath" }
    };
    protected override string CategoryName => ToolCategories.Layer;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layerName = ToolArgs.GetString(context, "layerName");
        var output = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "outputPath is required.");
        }

        // D-052 守卫统一接入（O-D049-08）：受保护输出路径守卫，先于任何写入。
        var hit = ProtectedOutputPathGuard.Match(output);
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"outputPath '{output}' is inside a protected root ('{hit}'); refused before writing (no artefacts).");
        }

        var r = await context.Host!.Layers.SaveLayerFileAsync(
            ToolArgs.GetString(context, "mapName"), layerName, output, context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}
