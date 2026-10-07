// D-118 之三：需真机宿主面的四件（动画导出／场景拉伸／高程剖面／场景打包）。
// 注册与冻结参数面齐备；执行面在宿主能力不可判定时一律 fail-closed（NOT_IMPLEMENTED ＋ sideEffects=false）。
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>X06 export_time_animation — 时间动画导出（需活动地图时序图层与编码器宿主面）。</summary>
public sealed class ExportTimeAnimationTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.ExportTimeAnimationToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Layout;
    public override string Name => ToolKey;
    public override string Description =>
        "时间动画导出（M4 X06）。冻结参数面齐备（timeLayers/format/frameIntervalMs/visualRules/overwrite）；" +
        "但帧合成需要活动 Pro 会话的时序图层渲染与编码器，本批不虚构产物：调用即在参数校验通过后返回 NOT_IMPLEMENTED，**零字节写出**。";

    protected override Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        var outputPath = D118Outputs.GuardPath(RequiredString(context, "outputPath"), "outputPath");
        var format = RequiredString(context, "format");
        var frames = D118Values.AsInt(ToolArg(context, "frameIntervalMs"));
        if (frames is { } f && f <= 0) return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "frameIntervalMs must be a positive integer."));
        return Task.FromResult(HostDependent("export_time_animation", $"rendering and {format} encoding face needed to produce animation frames"));
    }
}

/// <summary>X09 extrude_scene_features — 场景要素拉伸（需场景图层与在投影资源写权）。</summary>
public sealed class ExtrudeSceneFeaturesTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.ExtrudeSceneFeaturesToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Layer;
    public override string Name => ToolKey;
    public override string Description =>
        "场景要素拉伸（M4 X09，名册 Session 层）。参数面齐备（layer/heightField/unit/missingPolicy/negativePolicy）；" +
        "在投影资源变更需要活动 Pro 场景会话，故校验通过后 fail-closed（NOT_IMPLEMENTED），不谎报已改变场景。";

    protected override Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        var layer = RequiredString(context, "layer");
        var heightField = RequiredString(context, "heightField");
        var unit = RequiredString(context, "unit");
        var missing = RequiredString(context, "missingPolicy");
        var negative = RequiredString(context, "negativePolicy");
        if (missing is not ("skip" or "zero" or "error")) return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "missingPolicy must be skip, zero or error."));
        if (negative is not ("reflect" or "clamp" or "error")) return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "negativePolicy must be reflect, clamp or error."));
        _ = (layer, heightField, unit);
        return Task.FromResult(HostDependent("extrude_scene_features", "in-scene layer access (this tool is registered at the Session roster layer and mutates in-project resources only inside a live scene)"));
    }
}

/// <summary>X12 create_elevation_profile — 高程剖面（需 Pro 栅格表面与线要素几何）。</summary>
public sealed class CreateElevationProfileTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.CreateElevationProfileToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Raster;
    public override string Name => ToolKey;
    public override string Description =>
        "高程剖面（M4 X12）。参数面齐备（path/surface/outputPath/sampleInterval/zUnit/overwrite）；" +
        "剖面取样需要 Pro 栅格表面与线要素几何，本批不读取不可判定的数据集，校验通过后 fail-closed（NOT_IMPLEMENTED），零写出。";

    protected override Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        var path = RequiredString(context, "path");
        var surface = RequiredString(context, "surface");
        var outputPath = D118Outputs.GuardPath(RequiredString(context, "outputPath"), "outputPath");
        var interval = D118Values.AsDouble(ToolArg(context, "sampleInterval"));
        if (interval is { } v && v <= 0) return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "sampleInterval must be a positive number."));
        _ = (path, surface, outputPath);
        return Task.FromResult(HostDependent("create_elevation_profile", "raster surface sampling and line geometry of a project dataset"));
    }
}

/// <summary>X14 export_scene_package — 场景打包（需 Pro 工程与打包服务）。</summary>
public sealed class ExportScenePackageTool : D118M4ToolBase
{
    public const string ToolKey = D118FrozenSchemas.ExportScenePackageToolName;

    protected override string FrozenName => ToolKey;
    protected override string FrozenCategory => ToolCategories.Project;
    public override string Name => ToolKey;
    public override string Description =>
        "场景打包（M4 X14）。参数面齐备（scene/outputPath/includeTextures/verifyReopen/overwrite）；" +
        "打包需要工程内场景与内容清单，本批不生成无法核验的容器文件：校验通过后 fail-closed（NOT_IMPLEMENTED），零写出。";

    protected override Task<OperationResult<object?>> OnExecuteAsync(ToolExecutionContext context)
    {
        var scene = RequiredString(context, "scene");
        var outputPath = D118Outputs.GuardPath(RequiredString(context, "outputPath"), "outputPath");
        if (ToolArgs.GetBool(context, "verifyReopen", false))
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.NotImplemented,
                "verifyReopen would claim a reopen check that cannot run without a package and a live host; refusing it keeps the claim honest (no output was written)."));
        _ = (scene, outputPath);
        return Task.FromResult(HostDependent("export_scene_package", "scene content enumeration and package container writing"));
    }
}
