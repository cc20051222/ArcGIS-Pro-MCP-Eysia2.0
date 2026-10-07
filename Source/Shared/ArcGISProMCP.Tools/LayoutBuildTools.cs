using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>D-045：新建布局（+ 可选绑定地图框）。</summary>
public sealed class CreateLayoutTool : McpToolBase
{
    public override string Name => "create_layout";
    public override string Description =>
        "新建**自有**布局（写操作；plan §7「默认新建自有布局，不改用户原布局」）。参数：name、pageWidth、pageHeight、" +
        "pageUnits（Inches/Centimeters/Millimeters/Points，大小写不敏感；**省略 = Inches**）、可选 mapName（给了则同时创建并绑定地图框）、" +
        "**★ 单位形态披露（O-D045-04 / G-112 裁定③）**：本工具**自报**的 pageUnits 为**归一复数形态**" +
        "（如 `Centimeters`），而 get_layout_info 返回的是 **CIM 枚举单数形态**（如 `Centimeter`）—— 两读面各自确定性成立、可判别；" +
        "跨工具逐字比较单位字符串时须做形态归一。" +
        "可选 mapFrameName（缺省 `Map Frame`）、可选 frameXMin/frameYMin/frameXMax/frameYMax（页面坐标定义地图框范围；未给 = 页面内缩 1 单位）。" +
        "**同名已存在 → INVALID_ARGUMENT（拒绝创建，不静默改名）**；mapName 不存在 → MAP_NOT_FOUND；页面尺寸非正 → INVALID_ARGUMENT。" +
        "**★ 参数面收窄披露（spike）**：`dpi` 不在参数面 —— SDK 无可达 DPI 成员（Core/Mapping/Layouts XML 全库 0 命中，不硬造）。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["name"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "New layout name (must be unique)." },
            ["pageWidth"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["pageHeight"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["pageUnits"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Inches | Centimeters | Millimeters | Points (default Inches)." },
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional map to bind a map frame to." },
            ["mapFrameName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["frameXMin"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["frameYMin"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["frameXMax"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["frameYMax"] = new Dictionary<string, object?> { ["type"] = "number" },
        },
        ["required"] = new[] { "name", "pageWidth", "pageHeight" }
    };
    protected override string CategoryName => ToolCategories.Layout;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var name = ToolArgs.GetString(context, "name");
        var w = ToolArgs.GetDouble(context, "pageWidth");
        var h = ToolArgs.GetDouble(context, "pageHeight");
        if (string.IsNullOrWhiteSpace(name))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "name is required.");
        }

        if (w is null || h is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "pageWidth and pageHeight are required numbers.");
        }

        var units = ToolArgs.GetString(context, "pageUnits");
        var mapName = ToolArgs.GetString(context, "mapName");
        if (!string.IsNullOrWhiteSpace(mapName))
        {
            var rasterSources = await context.Host!.Layers
                .GetRasterSourcePathsAsync(mapName, context.CancellationToken).ConfigureAwait(false);
            if (!rasterSources.Success || rasterSources.Data is null)
            {
                return OperationResult<object?>.Fail(rasterSources.Errors!);
            }

            var qualification = await RasterQualificationGate.ValidatePathsAsync(
                context.Python, rasterSources.Data, context.CancellationToken).ConfigureAwait(false);
            if (!qualification.Success)
            {
                return OperationResult<object?>.Fail(qualification.Errors!);
            }
        }

        var r = await context.Host!.Layout.CreateLayoutAsync(
            name, w.Value, h.Value, units ?? string.Empty,
            mapName, ToolArgs.GetString(context, "mapFrameName"),
            ToolArgs.GetDouble(context, "frameXMin"), ToolArgs.GetDouble(context, "frameYMin"),
            ToolArgs.GetDouble(context, "frameXMax"), ToolArgs.GetDouble(context, "frameYMax"),
            context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>D-045：向既有布局添加文本。</summary>
public sealed class AddLayoutTextTool : McpToolBase
{
    public override string Name => "add_layout_text";
    public override string Description =>
        "向既有布局**就地**添加文本元素（写操作，G-78-B 披露：就地修改既有布局；**元素级删除/重命名不在本批参数面（Phase 12）**，" +
        "不做元素级回滚承诺）。参数：layoutName、text、x、y（页面坐标 = 页面单位；越界 → INVALID_ARGUMENT）、" +
        "可选 fontSize（点，默认 12）、fontFamily（缺省 = Pro 默认字体）、elementName（缺省自动命名 `Text_n`）。" +
        "布局不存在 → LAYER_NOT_FOUND（消息明示 Layout）；重名布局 → AMBIGUOUS_LAYER_NAME。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["layoutName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["text"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["x"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["y"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["fontSize"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Points; default 12." },
            ["fontFamily"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["elementName"] = new Dictionary<string, object?> { ["type"] = "string" },
        },
        ["required"] = new[] { "layoutName", "text", "x", "y" }
    };
    protected override string CategoryName => ToolCategories.Layout;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layoutName = ToolArgs.GetString(context, "layoutName");
        var text = ToolArgs.GetString(context, "text");
        var x = ToolArgs.GetDouble(context, "x");
        var y = ToolArgs.GetDouble(context, "y");
        if (string.IsNullOrWhiteSpace(layoutName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layoutName is required.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "text is required.");
        }

        if (x is null || y is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "x and y are required numbers.");
        }

        var r = await context.Host!.Layout.AddLayoutTextAsync(
            layoutName, text, x.Value, y.Value,
            ToolArgs.GetDouble(context, "fontSize"), ToolArgs.GetString(context, "fontFamily"),
            ToolArgs.GetString(context, "elementName"), context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>D-045：图例/指北针/比例尺的公共基类（锚定地图框）。</summary>
public abstract class AddLayoutSurroundToolBase : McpToolBase
{
    protected override string CategoryName => ToolCategories.Layout;

    protected virtual string ExtraDisclosure => string.Empty;

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["layoutName"] = new Dictionary<string, object?> { ["type"] = "string" },
            ["mapFrameName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map frame to anchor to." },
            ["x"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["y"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["width"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Page units; default per element." },
            ["height"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["elementName"] = new Dictionary<string, object?> { ["type"] = "string" },
        },
        ["required"] = new[] { "layoutName", "mapFrameName", "x", "y" }
    };

    protected async Task<OperationResult<object?>> ExecuteCoreAsync(
        ToolExecutionContext context,
        Func<string, string, double, double, double?, double?, string?, CancellationToken,
            Task<OperationResult<ArcGISProMCP.Core.Models.LayoutElementAddInfo>>> call)
    {
        var layoutName = ToolArgs.GetString(context, "layoutName");
        var mapFrameName = ToolArgs.GetString(context, "mapFrameName");
        var x = ToolArgs.GetDouble(context, "x");
        var y = ToolArgs.GetDouble(context, "y");
        if (string.IsNullOrWhiteSpace(layoutName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layoutName is required.");
        }

        if (string.IsNullOrWhiteSpace(mapFrameName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "mapFrameName is required.");
        }

        if (x is null || y is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "x and y are required numbers.");
        }

        var r = await call(layoutName, mapFrameName, x.Value, y.Value,
            ToolArgs.GetDouble(context, "width"), ToolArgs.GetDouble(context, "height"),
            ToolArgs.GetString(context, "elementName"), context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>D-045：向既有布局添加图例（锚定地图框）。</summary>
public sealed class AddLegendTool : AddLayoutSurroundToolBase
{
    public override string Name => "add_legend";
    public override string Description =>
        "向既有布局**就地**添加图例（写操作，G-78-B 披露；元素级删除/重命名不在本批参数面）。参数：layoutName、" +
        "mapFrameName（**锚定地图框**，必须存在且为地图框，否则 INVALID_ARGUMENT）、x、y、可选 width/height（页面单位，默认 3×2）、" +
        "可选 elementName（缺省 `Legend_n`）。布局不存在 → LAYER_NOT_FOUND。**样式披露**：使用默认样式（样式项寻址语义未文档化，不在参数面）。零 GP。";

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        => ExecuteCoreAsync(context, (l, mf, x, y, w, h, en, ct) =>
            context.Host!.Layout.AddLegendAsync(l, mf, x, y, w, h, en, ct));
}

/// <summary>D-045：向既有布局添加指北针（锚定地图框）。</summary>
public sealed class AddNorthArrowTool : AddLayoutSurroundToolBase
{
    public override string Name => "add_north_arrow";
    public override string Description =>
        "向既有布局**就地**添加指北针（写操作，G-78-B 披露；元素级删除/重命名不在本批参数面）。参数：layoutName、" +
        "mapFrameName（**锚定地图框**）、x、y、可选 width/height（页面单位，默认 1×1）、可选 elementName（缺省 `NorthArrow_n`）。" +
        "**样式披露**：使用默认样式 —— `NorthArrowStyleItem(string)` 的 string 语义在 SDK XML 无文档，故**样式参数不在参数面**（收窄，不猜能力）；" +
        "若运行时默认样式不可创建 → 当场降级披露。布局不存在 → LAYER_NOT_FOUND。零 GP。";

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        => ExecuteCoreAsync(context, (l, mf, x, y, w, h, en, ct) =>
            context.Host!.Layout.AddNorthArrowAsync(l, mf, x, y, w, h, en, ct));
}

/// <summary>D-045：向既有布局添加比例尺（锚定地图框）。</summary>
public sealed class AddScaleBarTool : AddLayoutSurroundToolBase
{
    public override string Name => "add_scale_bar";
    public override string Description =>
        "向既有布局**就地**添加比例尺（写操作，G-78-B 披露；元素级删除/重命名不在本批参数面）。参数：layoutName、" +
        "mapFrameName（**锚定地图框**）、x、y、可选 width/height（页面单位，默认 2×0.5）、可选 elementName（缺省 `ScaleBar_n`）。" +
        "**★ 参数面收窄披露（spike）**：`units` **不在参数面** —— `ScaleBarInfo` 在 SDK 仅暴露 `ScaleBarStyleItem`（**无单位成员**），" +
        "单位为样式/地图单位驱动；自定义留待后续批次。布局不存在 → LAYER_NOT_FOUND。零 GP。";

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        => ExecuteCoreAsync(context, (l, mf, x, y, w, h, en, ct) =>
            context.Host!.Layout.AddScaleBarAsync(l, mf, x, y, w, h, en, ct));
}
