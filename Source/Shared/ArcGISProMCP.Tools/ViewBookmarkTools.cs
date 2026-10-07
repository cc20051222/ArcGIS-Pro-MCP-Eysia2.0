using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>D-063 · C 段：视图书签 ＋ ★回图（7 件）。</summary>
public sealed class GetMapViewTool : McpToolBase
{
    public override string Name => "get_map_view";
    public override string Description =>
        "读取地图视图相机态（**只读 · 依赖活动视图**）。参数：mapName（可省略 = 活动地图）。" +
        "返回：x / y（中心）、scale（比例分母）、heading / pitch / roll、可见范围四至（xMin/yMin/xMax/yMax）、hasActiveView。" +
        "**★ 口径披露（重要）**：相机与范围是**活动视图态**（不是工程态）—— 打开/切换地图会改变其可用性；" +
        "**无活动视图 ⇒ NO_ACTIVE_VIEW 明确报错**（不返回伪造值）。若 mapName 与活动视图地图不符 ⇒ INVALID_ARGUMENT。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for the active view's map." },
        },
    };
    protected override string CategoryName => ToolCategories.Map;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var r = await context.Host!.Maps.GetMapViewAsync(
            ToolArgs.GetString(context, "mapName"), context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class SetMapViewTool : McpToolBase
{
    public override string Name => "set_map_view";
    public override string Description =>
        "移动/设置地图视图（**视图态写入，不改数据**）。参数：mapName（可省略）、" +
        "**三选一**：① extent（xMin/yMin/xMax/yMax 四至全给）② centerX+centerY（可选 scale）③ rotation（**当前不可达**）。" +
        "返回 applied / requestedBy / cameraAfter（写后读回），可与前照对照。" +
        "**★ 口径披露**：center+scale 的比例→范围换算按 **96 DPI + 地图 SR 线性单位**推导（**近似**，披露不隐藏）；" +
        "**rotation 独立设置不可达**（SDK Camera 公开构造为 (x,y,z,scale,SR,viewpoint)，heading 无公开 setter）" +
        "⇒ NOT_IMPLEMENTED 并明确披露，**不静默忽略**。无活动视图 ⇒ NO_ACTIVE_VIEW。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for the active view's map." },
            ["xMin"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["yMin"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["xMax"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["yMax"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["centerX"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["centerY"] = new Dictionary<string, object?> { ["type"] = "number" },
            ["scale"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Scale denominator (used with centerX/centerY)." },
            ["rotation"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "NOT reachable in v1 — see description." },
        },
    };
    protected override string CategoryName => ToolCategories.Map;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var r = await context.Host!.Maps.SetMapViewAsync(
            ToolArgs.GetString(context, "mapName"),
            ToolArgs.GetDouble(context, "xMin"), ToolArgs.GetDouble(context, "yMin"),
            ToolArgs.GetDouble(context, "xMax"), ToolArgs.GetDouble(context, "yMax"),
            ToolArgs.GetDouble(context, "centerX"), ToolArgs.GetDouble(context, "centerY"),
            ToolArgs.GetDouble(context, "scale"), ToolArgs.GetDouble(context, "rotation"),
            context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>
/// D-063 ★ 回图：渲染地图视图并以 **MCP image content** 回传（对齐并超越 Knight60 的 🖼️ 能力 —— 我们可不落盘回图）。
/// </summary>
public sealed class ExportMapViewTool : McpToolBase
{
    public override string Name => "export_map_view";
    public override string Description =>
        "★ 回图：渲染当前地图视图并**直接回传 PNG 图像**（MCP **image content**，AI 客户端可“看”图）。" +
        "参数：mapName（可省略）、width / height（可选像素）、resolutionDpi（默认 96）、maxEdge（长边上限，默认 1200）、" +
        "outputPath（**可选**；省略 = **不落盘模式**）。" +
        "**硬约束**：默认 96 DPI；长边 ≤1200 px（超限等比缩放）；PNG ≤1 MB（超限自动降采样重试，仍超限 ⇒ **明确报错**而非截断）。" +
        "**★ 不落盘模式（默认）＝零文件副作用**：SDK ExportFormat 仅支持 OutputFileName（**无内存流 API**，已实证），" +
        "故实现上**经受控临时文件中转（D 盘，禁 %TEMP%/C 盘）→ 读取字节 → 立即删除**，对外零残留；" +
        "结果中 `usedTransientFile=true` 与 `transientFileDeleted=true` **如实披露该中转事实，绝不静默**。" +
        "给定 outputPath ⇒ 落盘模式，**先经输出路径守卫**（命中受保护根 ⇒ PATH_ESCAPE_REJECTED，零产物）。" +
        "无活动视图 ⇒ NO_ACTIVE_VIEW。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for the active view's map." },
            ["width"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Output width in pixels (capped by maxEdge)." },
            ["height"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Output height in pixels (capped by maxEdge)." },
            ["resolutionDpi"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Render DPI (default 96)." },
            ["maxEdge"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Long-edge cap in pixels (default 1200)." },
            ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional absolute .png path; omit for no-artefact mode." },
        },
    };
    protected override string CategoryName => ToolCategories.Map;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var output = ToolArgs.GetString(context, "outputPath");
        if (!string.IsNullOrWhiteSpace(output))
        {
            // D-052 守卫统一接入（O-D049-08）：写文件 ⇒ 先过受保护输出路径守卫。
            var hit = ProtectedOutputPathGuard.Match(output);
            if (hit is not null)
            {
                return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                    $"outputPath '{output}' is inside a protected root ('{hit}'); refused before rendering (no artefacts).");
            }
        }

        var r = await context.Host!.Maps.ExportMapViewAsync(
            ToolArgs.GetString(context, "mapName"),
            ToolArgs.GetInt(context, "width"), ToolArgs.GetInt(context, "height"),
            ToolArgs.GetInt(context, "resolutionDpi"), ToolArgs.GetInt(context, "maxEdge"),
            output, context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class ListBookmarksTool : McpToolBase
{
    public override string Name => "list_bookmarks";
    public override string Description =>
        "列出地图书签（**只读**）。参数：mapName（可省略 = 活动地图）。返回每条 name / mapName 与 totalCount。" +
        "无书签 ⇒ 空清单（totalCount=0，非错误）。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
        },
    };
    protected override string CategoryName => ToolCategories.Map;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var r = await context.Host!.Maps.ListBookmarksAsync(
            ToolArgs.GetString(context, "mapName"), context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class CreateBookmarkTool : McpToolBase
{
    public override string Name => "create_bookmark";
    public override string Description =>
        "以当前视图创建书签（**写操作 · 视图资产**）。参数：mapName（可省略）、name。" +
        "**依赖活动视图**（书签捕获当前视图，无活动视图 ⇒ NO_ACTIVE_VIEW）。同名已存在 ⇒ INVALID_ARGUMENT（不覆盖、不静默改名）。" +
        "返回 operation=create 与 bookmarkCountAfter（供增删往返断言）。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["name"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Bookmark name." },
        },
        ["required"] = new[] { "name" }
    };
    protected override string CategoryName => ToolCategories.Map;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var name = ToolArgs.GetString(context, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "name is required.");
        }

        var r = await context.Host!.Maps.CreateBookmarkAsync(
            ToolArgs.GetString(context, "mapName"), name, context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class ApplyBookmarkTool : McpToolBase
{
    public override string Name => "apply_bookmark";
    public override string Description =>
        "应用书签（视图跳转到书签相机；**视图态写入，不改数据**）。参数：mapName（可省略）、name。" +
        "**依赖活动视图**；书签不存在 ⇒ NOT_FOUND；无活动视图 ⇒ NO_ACTIVE_VIEW。" +
        "返回 operation=apply 与 bookmarkCountAfter。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["name"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Bookmark name." },
        },
        ["required"] = new[] { "name" }
    };
    protected override string CategoryName => ToolCategories.Map;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var name = ToolArgs.GetString(context, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "name is required.");
        }

        var r = await context.Host!.Maps.ApplyBookmarkAsync(
            ToolArgs.GetString(context, "mapName"), name, context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

public sealed class DeleteBookmarkTool : McpToolBase
{
    public override string Name => "delete_bookmark";
    public override string Description =>
        "删除书签（**写操作 · 仅视图资产**）。参数：mapName（可省略）、name。" +
        "**★ 破坏性口径（明确披露）**：书签是**视图资产**，删除**不破坏任何数据**，故本工具**不在 destructive 名录内**" +
        "（因此**不需要 confirm 参数**），但此处**显式披露**该语义以免误判。" +
        "书签不存在 ⇒ NOT_FOUND（不静默成功）。返回 operation=delete 与 bookmarkCountAfter（供增删往返断言）。零 GP。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for active map." },
            ["name"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Bookmark name." },
        },
        ["required"] = new[] { "name" }
    };
    protected override string CategoryName => ToolCategories.Map;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var name = ToolArgs.GetString(context, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "name is required.");
        }

        var r = await context.Host!.Maps.DeleteBookmarkAsync(
            ToolArgs.GetString(context, "mapName"), name, context.CancellationToken).ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}
