using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// D-061（功能完善第一批 · 生态对齐）：新增 9 件工具的实现集合。
/// <c>save_project</c> / <c>create_map</c> / <c>set_workspace</c> / <c>get_workspace</c> /
/// <c>list_rasters</c> / <c>list_tables</c> / <c>get_unique_values</c> / <c>get_layer_features</c> /
/// <c>repair_geometry</c>（另 4 件布局导出见 <c>LayoutExportTools.cs</c>）。
/// </summary>
/// <remarks>
/// 纪律：错误码 **33 零新增** —— 本文件仅使用既有错误码；
/// 写类调用点前置路径守卫（<see cref="ProtectedOutputPathGuard"/>）；破坏性操作须显式 <c>confirm</c>。
/// </remarks>

/// <summary>D-061：保存当前工程（原位 / 另存）。写 · Native · 零 GP。</summary>
public sealed class SaveProjectTool : McpToolBase
{
    public override string Name => "save_project";

    public override string Description =>
        "保存当前 ArcGIS Pro 工程（写操作 = **修改 .aprx 工程文件**；可复原性取决于调用方备份）。" +
        "参数：saveAsPath（可选，另存目标路径，**必须以 `.aprx` 结尾**；省略 ⇒ 原位保存当前工程）。" +
        "返回保存后**实测**事实：路径 / 工程名 / 是否另存 / 脏标记（IsDirty）/" +
        "文件是否确已在位（FileExists）/ 字节数 / 最后写入时间（UTC）。" +
        "契约：saveAs 目标落在受保护根（`TestFixtures` / 旧仓库 / 环境变量追加根）→ `PATH_ESCAPE_REJECTED`，" +
        "**不执行保存、不写任何文件**；saveAsPath 扩展名非 `.aprx` 或路径非法 → `INVALID_ARGUMENT`；" +
        "宿主未实现保存 → `NOT_IMPLEMENTED`（不伪造成功）。" +
        "**注意（如实披露）**：本工具保存的是**工程文件**，不含地图图层数据的单独导出；" +
        "另存后 `Project.Current` 指向新路径，后续写操作落在另存副本上。零 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["saveAsPath"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["description"] = "Optional 'save as' target path (.aprx); omit to save in place."
            }
        }
    };

    protected override string CategoryName => ToolCategories.Project;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var saveAs = ToolArgs.GetString(context, "saveAsPath");
        if (!string.IsNullOrWhiteSpace(saveAs))
        {
            var target = saveAs!.Trim();

            // 写类调用点统一前置守卫（D-055 A2 / 13.1）：受保护根 → 拒绝且零写入。
            var protectedHit = ProtectedOutputPathGuard.Match(target);
            if (protectedHit is not null)
            {
                return OperationResult<object?>.Fail(
                    ErrorCodes.PathEscapeRejected,
                    $"saveAsPath '{target}' was refused by the protected-path guard ('{protectedHit}'); no file was written.");
            }

            if (!target.EndsWith(".aprx", StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<object?>.Fail(
                    ErrorCodes.InvalidArgument, "saveAsPath must end with '.aprx'.");
            }
        }

        var r = await context.Host.Project
            .SaveProjectAsync(string.IsNullOrWhiteSpace(saveAs) ? null : saveAs!.Trim(), context.CancellationToken)
            .ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-061：新建地图（2D Map）。写 · Native · 零 GP。</summary>
public sealed class CreateMapTool : McpToolBase
{
    public override string Name => "create_map";

    public override string Description =>
        "在当前工程内**新建地图（2D Map）**并返回其信息（写操作 = **新增工程项**；可复原 = 删除该地图项）。" +
        "参数：name（**必填**，新地图名）、basemap（可选，底图名）、spatialReference（可选，WKID 整数或名称/WKT）。" +
        "契约：**重名 → `INVALID_ARGUMENT` 提前拒绝（不静默覆盖既有地图）**；名称空白 → `INVALID_ARGUMENT`；" +
        "spatialReference 无法解析 → `INVALID_ARGUMENT`（宿主口径与 `set_map_extent` 同源：WKID 整数 / 名称 / WKT）；" +
        "**★ 本批 basemap 未实现（如实披露）** —— 传入非空 basemap ⇒ 宿主**显式返回 `NOT_IMPLEMENTED`**" +
        "（不会「忽略参数假装成功」）；省略 basemap 即创建无底图地图。宿主未实现地图创建 → `NOT_IMPLEMENTED`。" +
        "新建地图默认**不自动成为活动地图**（活动地图以 `get_current_map` 为准）。" +
        "LIVE 判据：创建后 `list_maps` 出现该名称。零 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["name"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "New map name (must not duplicate an existing map)." },
            ["basemap"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional basemap name." },
            ["spatialReference"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional spatial reference (WKID integer, name, or WKT)." }
        },
        ["required"] = new[] { "name" }
    };

    protected override string CategoryName => ToolCategories.Map;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var name = ToolArgs.GetString(context, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "name is required.");
        }

        // 重名提前拒绝：不静默覆盖既有地图（写安全：先读后写，读失败则保守拒绝）。
        var existing = await context.Host.Maps.GetMapsAsync(context.CancellationToken).ConfigureAwait(false);
        if (!existing.Success)
        {
            return OperationResult<object?>.Fail(existing.Errors);
        }

        var hit = existing.Data?.FirstOrDefault(
            m => string.Equals(m.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                $"A map named '{name.Trim()}' already exists; create_map refuses to overwrite it.");
        }

        var r = await context.Host.Maps.CreateMapAsync(
            name.Trim(),
            ToolArgs.GetString(context, "basemap"),
            ToolArgs.GetString(context, "spatialReference"),
            context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-061：设定默认工作区上下文（进程内状态）。写 · Native · 零 GP · 零宿主调用。</summary>
public sealed class SetWorkspaceTool : McpToolBase
{
    public override string Name => "set_workspace";

    public override string Description =>
        "设定当前会话的**默认工作区上下文**（写操作 = **修改进程内状态**，不触碰磁盘）。" +
        "参数：path（**必填**，文件夹 / `.gdb` 容器路径）。返回规范化后的路径。" +
        "契约：空白 → `INVALID_ARGUMENT`（失败关闭：**原值保持不变**）；路径含非法字符或无法规范化 → `INVALID_ARGUMENT`" +
        "（同样不改动状态）；**不校验存在性** —— 工作区允许在设定之后才创建，存在性由使用该工作区的工具负责判定；" +
        "覆盖既有值是允许的（后写胜出），但返回值始终为规范化后的新路径。" +
        "规范化规则：去首尾空白 → 折叠 `..` 与重复分隔符 → 去尾部目录分隔符（根目录除外）。" +
        "**生命周期如实披露**：状态为**进程级**，Pro / 宿主进程重启后复位为空（不是持久化配置）。零 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["path"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Workspace path (folder or .gdb container)." }
        },
        ["required"] = new[] { "path" }
    };

    protected override string CategoryName => ToolCategories.Project;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    /// <summary>本工具**不触碰 ArcGIS 宿主**（纯进程内状态写入）⇒ 显式声明不需要 ArcGIS。</summary>
    protected override bool? RequiresArcGISOverride => false;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var raw = ToolArgs.GetString(context, "path");

        // 非字符串（标量形态）也走同一判决：缺失/非法 → INVALID_ARGUMENT，且状态不变。
        if (raw is null)
        {
            return Task.FromResult(OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, "path is required."));
        }

        if (!WorkspaceContext.TrySet(raw, out var normalized, out var error))
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, error));
        }

        return Task.FromResult(OperationResult<object?>.Ok(new WorkspaceInfo
        {
            WorkspacePath = normalized,
            Set = true
        }));
    }
}

/// <summary>D-061：读取默认工作区上下文。只读 · Native · 零 GP · 零宿主调用。</summary>
public sealed class GetWorkspaceTool : McpToolBase
{
    public override string Name => "get_workspace";

    public override string Description =>
        "读取当前会话的**默认工作区上下文**（只读）。无参数。" +
        "返回：`workspacePath`（规范化路径）+ `set`（是否已显式设定）。" +
        "契约：**未设定时成功返回**（`set=false` 且 `workspacePath` 为空字符串）——不报错、**不伪造**默认值；" +
        "本工具不依赖宿主（纯进程内读取），宿主缺失仍可用；" +
        "与 `set_workspace` 在同一进程中往返一致（`set_workspace` → `get_workspace` 取回同一规范化路径）。" +
        "注：状态随进程生命周期，重启后 `set=false`。零 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>(),
        ["additionalProperties"] = false
    };

    protected override string CategoryName => ToolCategories.Project;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    /// <summary>本工具**不触碰 ArcGIS 宿主**（纯进程内状态读取）⇒ 显式声明不需要 ArcGIS。</summary>
    protected override bool? RequiresArcGISOverride => false;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        => Task.FromResult(OperationResult<object?>.Ok(new WorkspaceInfo
        {
            WorkspacePath = WorkspaceContext.Current,
            Set = WorkspaceContext.IsSet
        }));
}

/// <summary>D-061：枚举工作区内的栅格数据集。只读 · Native · 零 GP。</summary>
public sealed class ListRastersTool : McpToolBase
{
    public override string Name => "list_rasters";

    public override string Description =>
        "枚举工作区内的**栅格数据集**（只读）。参数：workspace（可选；省略 ⇒ 使用 `get_workspace` 的上下文值）。" +
        "返回每项：名称 / 类型 / 路径 —— **分类由定义枚举保证**（`RasterDatasetDefinition` 即栅格，非运行时猜测）。" +
        "契约：既无显式 workspace 也无上下文 ⇒ `INVALID_ARGUMENT`（提示先 `set_workspace`）；" +
        "**非 `.gdb` 工作区（文件夹 / 企业级连接）本批不支持 ⇒ `INVALID_ARGUMENT` 明示，不以空集冒充成功**；" +
        "`.gdb` 路径不存在 → `DATASET_NOT_FOUND`；**空工作区 ⇒ 成功返回空集合**（不是错误）；" +
        "不含要素类与独立表（`list_tables` 负责表）；宿主未实现 → `NOT_IMPLEMENTED`。" +
        "**如实披露（本批覆盖度）**：`spatialReference` / `cellSizeX` / `cellSizeY` / `bandCount` 本批宿主**不采集**，恒定返回 null" +
        "（未 LIVE 验证的字段不臆造值）——需要这些元数据请改用 `get_raster_info` 等专用工具。零 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["workspace"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Workspace path (folder or .gdb); defaults to the workspace context." }
        }
    };

    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        if (!TryResolveWorkspace(context, out var workspace, out var fail))
        {
            return fail!;
        }

        var r = await context.Host.Data.ListRastersAsync(workspace, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }

    internal static bool TryResolveWorkspace(ToolExecutionContext context, out string workspace, out OperationResult<object?>? fail)
    {
        workspace = string.Empty;
        fail = null;

        var explicitWs = ToolArgs.GetString(context, "workspace");
        if (!string.IsNullOrWhiteSpace(explicitWs))
        {
            workspace = explicitWs!.Trim();
            return true;
        }

        var contextWs = WorkspaceContext.Current;
        if (contextWs.Length == 0)
        {
            fail = OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "workspace is required (or set it once with set_workspace).");
            return false;
        }

        workspace = contextWs;
        return true;
    }
}

/// <summary>D-061：枚举工作区内的独立表。只读 · Native · 零 GP。</summary>
public sealed class ListTablesTool : McpToolBase
{
    public override string Name => "list_tables";

    public override string Description =>
        "枚举工作区内的**独立表（standalone table）**（只读）。参数：workspace（可选；省略 ⇒ 使用 `get_workspace` 的上下文值）。" +
        "返回每项：名称 / 类型 / 空间参考（独立表无空间参考 ⇒ null）。" +
        "契约：既无显式 workspace 也无上下文 ⇒ `INVALID_ARGUMENT`；" +
        "**非 `.gdb` 工作区本批不支持 ⇒ `INVALID_ARGUMENT` 明示（不以空集冒充成功）**；`.gdb` 不存在 → `DATASET_NOT_FOUND`；" +
        "**空工作区 ⇒ 成功返回空集合**（不是错误）；**不含要素类**（要素类属另一口径）；" +
        "宿主未实现 → `NOT_IMPLEMENTED`。本批不采集 `spatialReference`（独立表无空间参考，恒为 null）。零 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["workspace"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Workspace path (folder or .gdb); defaults to the workspace context." }
        }
    };

    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        if (!ListRastersTool.TryResolveWorkspace(context, out var workspace, out var fail))
        {
            return fail!;
        }

        var r = await context.Host.Data.ListTablesAsync(workspace, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-061：字段唯一值枚举（get_field_values 的「唯一值」专用档）。只读 · Native · 零 GP。</summary>
public sealed class GetUniqueValuesTool : McpToolBase
{
    /// <summary>topN 上限（硬夹）：超限夹取并在结果标注 Clamped=true，不报错。</summary>
    internal const int MaxDistinctCeiling = 1000;

    public override string Name => "get_unique_values";

    public override string Description =>
        "枚举指定图层某字段的**唯一值**（只读）。参数：layerName（必填）、fieldName（必填）、" +
        "mapName（可选；省略 ⇒ 活动地图）、topN（可选，默认 200，**上限 1000**）。" +
        "返回：去重后的字符串化取值 + 总数/空值数/去重计数 + 最小/最大值 + `appliedMaxDistinct` / `clamped` / `truncated` 标注。" +
        "契约：layerName / fieldName 空白 ⇒ `INVALID_ARGUMENT`；**topN ≤ 0 ⇒ `INVALID_ARGUMENT`**；" +
        "**topN > 1000 ⇒ 硬夹到 1000 并标注 `clamped=true`**（不报错，防止无界返回）；" +
        "未知字段 / 未知图层的错误由宿主透传（不吞错）；宿主未实现 → `NOT_IMPLEMENTED`。零 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; defaults to the active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
            ["fieldName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Field whose distinct values are enumerated." },
            ["topN"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Max distinct values (1-1000, default 200; larger values are clamped)." }
        },
        ["required"] = new[] { "layerName", "fieldName" }
    };

    protected override string CategoryName => ToolCategories.Attribute;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var fieldName = ToolArgs.GetString(context, "fieldName");
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "fieldName is required.");
        }

        var requestedTopN = ToolArgs.GetInt(context, "topN");
        if (requestedTopN is not null && requestedTopN.Value <= 0)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, "topN must be a positive integer (1-" + MaxDistinctCeiling + ").");
        }

        var applied = requestedTopN ?? 200;
        var clamped = requestedTopN is not null && requestedTopN.Value > MaxDistinctCeiling;
        if (clamped)
        {
            applied = MaxDistinctCeiling;
        }

        var r = await context.Host.Attributes.GetFieldValuesAsync(
            ToolArgs.GetString(context, "mapName") ?? string.Empty,
            layerName.Trim(),
            fieldName.Trim(),
            applied,
            context.CancellationToken).ConfigureAwait(false);

        if (!r.Success || r.Data is null)
        {
            return ToolResult.From(r);
        }

        var info = r.Data;
        info.RequestedMaxDistinct = requestedTopN;
        info.AppliedMaxDistinct = applied;
        info.Clamped = clamped;
        if (clamped)
        {
            // 夹取意味着调用方请求的窗口未被完整满足 ⇒ 与宿主侧去重上限截断同义，显式置位。
            info.Truncated = true;
        }

        return OperationResult<object?>.Ok(info);
    }
}

/// <summary>
/// D-061：图层行预览。**★ 工单内裁定：本工具与 `query_attributes` 语义重叠 ⇒ 并入同一实现，作其行预览预设档。**
/// 只读 · Native · 零 GP。
/// </summary>
public sealed class GetLayerFeaturesTool : McpToolBase
{
    /// <summary>limit 默认 10（行预览语义）。</summary>
    internal const int DefaultLimit = 10;

    /// <summary>limit 上限 100（硬夹）：超限夹到 100 并标注 clamped=true，不报错。</summary>
    internal const int LimitCeiling = 100;

    public override string Name => "get_layer_features";

    public override string Description =>
        "**图层行预览**（只读）：取图层前若干行的字段子集，用于快速查看数据长什么样。" +
        "参数：layerName（必填）、mapName（可选）、where（可选过滤条件）、fieldNames（可选字段裁剪）、" +
        "limit（可选，**默认 10，上限 100**）。结果按 OID 升序返回。" +
        "**★ 显著披露（D-061 工单内裁定）**：本工具与 `query_attributes` 读的是**同一数据源**，" +
        "**语义完全重叠** —— 差别仅在默认/上限档位，**故裁定「并入而非双轨」**：" +
        "本工具实现为 `query_attributes` 的**行预览预设档（preset=row-preview）**，" +
        "**共享同一宿主调用 `QueryFeaturesAsync` 与同一数据源，不复制实现**。" +
        "需要更强能力（更大 maxFeatures / 字段全取）请直接用 `query_attributes`（其默认上限 1000）。" +
        "契约：limit ≤ 0 ⇒ `INVALID_ARGUMENT`；**limit > 100 ⇒ 夹到 100 并标注 `clamped=true`**（不报错）；" +
        "返回体附 `preset` / `sourceTool` / `requestedLimit` / `appliedLimit` / `clamped` 供调用方判别。" +
        "零 GP。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; defaults to the active map." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name." },
            ["where"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional SQL where clause." },
            ["fieldNames"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "string" } },
            ["limit"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Max rows to preview (1-100, default 10; larger values are clamped)." }
        },
        ["required"] = new[] { "layerName" }
    };

    protected override string CategoryName => ToolCategories.Attribute;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var layerName = ToolArgs.GetString(context, "layerName");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName is required.");
        }

        var requestedLimit = ToolArgs.GetInt(context, "limit") ?? DefaultLimit;
        if (requestedLimit <= 0)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, "limit must be a positive integer (1-" + LimitCeiling + ").");
        }

        var clamped = requestedLimit > LimitCeiling;
        var appliedLimit = clamped ? LimitCeiling : requestedLimit;

        // ★ 并入实现：与 query_attributes 同为 QueryFeaturesAsync —— 新增宿主方法一律不做。
        var request = new AttributeQueryRequest
        {
            MapName = ToolArgs.GetString(context, "mapName") ?? string.Empty,
            LayerName = layerName.Trim(),
            WhereClause = ToolArgs.GetString(context, "where"),
            MaxFeatures = appliedLimit
        };

        if (context.Arguments is not null
            && context.Arguments.TryGetValue("fieldNames", out var fn)
            && fn is System.Collections.IEnumerable en)
        {
            var fields = new List<string>();
            foreach (var item in en)
            {
                if (item is string s && !string.IsNullOrWhiteSpace(s))
                {
                    fields.Add(s);
                }
            }

            if (fields.Count > 0)
            {
                request.FieldNames = fields;
            }
        }

        var r = await context.Host.Attributes.QueryFeaturesAsync(request, context.CancellationToken).ConfigureAwait(false);
        if (!r.Success)
        {
            return ToolResult.From(r);
        }

        // 行预览契约：OID 升序 + 不超过施加档位（宿主返回已在 MaxFeatures 内，此处仅保证顺序与上限不被突破）。
        var rows = (r.Data ?? new List<FeatureInfo>())
            .OrderBy(f => f.Oid)
            .Take(appliedLimit)
            .ToList();

        return OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["preset"] = "row-preview",
            ["sourceTool"] = "query_attributes",
            ["mapName"] = request.MapName,
            ["layerName"] = request.LayerName,
            ["requestedLimit"] = requestedLimit,
            ["appliedLimit"] = appliedLimit,
            ["clamped"] = clamped,
            ["count"] = rows.Count,
            ["features"] = rows
        });
    }
}

/// <summary>
/// D-061：几何修复（GP RepairGeometry_management；**就地破坏性**）。
/// 写 · Geoprocessing · 需显式 confirm（缺省拒）。
/// </summary>
public sealed class RepairGeometryTool : McpToolBase
{
    public override string Name => "repair_geometry";

    public override string Description =>
        "**修复数据集几何（就地破坏性：直接改写目标数据集几何，不可回退，调用方须自行备份）**。GP 工具：`RepairGeometry_management`。" +
        "参数：datasetPath（目标要素类全路径，**必填**）、confirm（**必须显式传 true**，缺省/false ⇒ `INVALID_ARGUMENT` 拒绝执行）、" +
        "deleteNull（可选，默认 false ⇒ `KEEP_NULL`；true ⇒ `DELETE_NULL`，即几何为空的要素会被删除）。" +
        "预检（失败关闭，零变更）：① **输入守卫** —— datasetPath 命中受保护根（`TestFixtures` / 旧仓库 / 环境变量追加根）→ " +
        "`PATH_ESCAPE_REJECTED` ② **存在性预检** —— 目标确定不存在 → `DATASET_NOT_FOUND`（消除 GP 对不存在目标静默成功的假成功）；" +
        "存在性不可判定 → `INVALID_STATE` 保守拒绝 ③ confirm 未显式 true → `INVALID_ARGUMENT`（不执行）。" +
        "**破坏性披露**：就地修改，OID 集合不变（不增删要素，除非 deleteNull=true 删除空几何要素）。" +
        "GP 参数序：`[in_features, {KEEP_NULL|DELETE_NULL}]`。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["datasetPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target feature class path (repaired in place)." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Must be explicitly true; otherwise the call is refused (INVALID_ARGUMENT)." },
            ["deleteNull"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "true => DELETE_NULL (features with null geometry are deleted); default false => KEEP_NULL." }
        },
        ["required"] = new[] { "datasetPath", "confirm" }
    };

    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var target = ToolArgs.GetString(context, "datasetPath");
        if (string.IsNullOrWhiteSpace(target))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "datasetPath is required.");
        }

        target = target!.Trim();

        // 1) 输入守卫（受保护根 → 拒绝，零变更）
        var protectedHit = ProtectedOutputPathGuard.Match(target);
        if (protectedHit is not null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.PathEscapeRejected,
                $"datasetPath '{target}' is inside a protected root ('{protectedHit}'); repair refused (no change made).");
        }

        // 2) confirm 显式确认（缺省 / false → 拒绝）
        var confirm = ToolArgs.GetBool(context, "confirm", false);
        if (!confirm)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "confirm must be explicitly true to repair geometry in place; the call was refused (no change made).");
        }

        // 3) 存在性预检（GP 对不存在目标静默成功 ⇒ 必须预检，消除假成功）
        var probe = await context.Host.Geoprocessing
            .CheckOutputExistsAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (!probe.Success)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidState,
                "dataset existence could not be determined; repair refused (fail-closed, no change made).");
        }

        if (probe.Data == OutputExistence.NotExists)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.DatasetNotFound,
                $"dataset '{target}' does not exist; repair refused (no change made).");
        }

        if (probe.Data == OutputExistence.Unknown)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidState,
                "dataset existence is unprovable; repair refused (fail-closed, no change made).");
        }

        // 4) 执行 GP：参数序 [in_features, {KEEP_NULL|DELETE_NULL}]
        var deleteNull = ToolArgs.GetBool(context, "deleteNull", false);
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest
            {
                ToolName = "RepairGeometry_management",
                Values = new List<string> { target, deleteNull ? "DELETE_NULL" : "KEEP_NULL" }
            },
            context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}
