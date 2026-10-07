using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// 获取数据集基本信息（Phase 8.2：通道 Native→Bridge，per PHASE_8_2_CHANNEL ADR）。
/// 轻量存在性 + Geo 类型判定（arcpy.Exists/Describe）；重结构摘要走 dataset_summary。
/// 不存在路径成功返回 exists=false + reason（不得用错误掩盖/不得用 null 掩盖）。
/// </summary>
public sealed class GetDatasetInfoTool : McpToolBase
{
    public override string Name => "get_dataset_info";
    public override string Description =>
        "返回数据集路径的轻量判定：exists/dataType(name/catalogPath)/resolvedPath。磁盘数据集(ArcPy)语义，" +
        "对 FileGDB 内要素类/表/栅格同样有效（arcpy.Exists，非 File.Exists）。不存在路径成功返回 exists=false + reason。 " +
        "重结构摘要（要素数/范围/字段）请用 dataset_summary。不访问 CURRENT。参数：path。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["path"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Dataset path." }
        },
        ["required"] = new[] { "path" }
    };
    protected override string CategoryName => ToolCategories.Python;
    protected override string ExecutionTypeName => ExecutionTypes.Python;
    protected override bool? RequiresArcGISOverride => false;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var bridge = context.Python;
        if (bridge is null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.PythonBridgeUnavailable,
                "Python Bridge service is not available in this host.");
        }

        var path = ToolArgs.GetString(context, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "path is required.");
        }

        var r = await bridge.DatasetInfoAsync(path, context.CancellationToken).ConfigureAwait(false);
        return r.Success
            ? OperationResult<object?>.Ok(r.Data.HasValue ? r.Data.Value : JsonValueKind.Null)
            : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>
/// 获取栅格数据集全字段信息（Phase 8.2 重写：Bridge/ArcPy Raster，per ADR）。
/// width/height/bandCount/pixelType/pixelSize/SR/extent/noDataValue；
/// statistics 仅在持久化统计存在时返回数值（getStatistics("") 条目 count>0 判据，spike 实证），
/// 否则哨兵 "unknown"——**禁止任何现算路径**（GetRasterProperties/CalculateStatistics 均禁用）。
/// </summary>
public sealed class GetRasterInfoTool : McpToolBase
{
    public override string Name => "get_raster_info";
    public override string Description =>
        "返回栅格数据集全字段信息：exists/isRaster/width/height/bandCount/pixelType/pixelSize/spatialReference/extent/" +
        "noDataValue/statistics。statistics 仅在持久化统计存在时返回数值，否则为 \"unknown\"（绝不现算生成）。" +
        "磁盘栅格(ArcPy)语义，GDB 内栅格亦有效。不访问 CURRENT。参数：datasetPath。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["datasetPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Raster dataset path." }
        },
        ["required"] = new[] { "datasetPath" }
    };
    protected override string CategoryName => ToolCategories.Python;
    protected override string ExecutionTypeName => ExecutionTypes.Python;
    protected override bool? RequiresArcGISOverride => false;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var bridge = context.Python;
        if (bridge is null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.PythonBridgeUnavailable,
                "Python Bridge service is not available in this host.");
        }

        var path = ToolArgs.GetString(context, "datasetPath");
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "datasetPath is required.");
        }

        var r = await bridge.RasterInfoAsync(path, context.CancellationToken).ConfigureAwait(false);
        return r.Success
            ? OperationResult<object?>.Ok(r.Data.HasValue ? r.Data.Value : JsonValueKind.Null)
            : OperationResult<object?>.Fail(r.Errors);
    }
}
