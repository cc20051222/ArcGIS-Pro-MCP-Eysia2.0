using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// D-053（Phase 12 第二批）：**编辑类新工具** delete_dataset / rename_dataset / append_features（75 → 78）。
/// 全部为**破坏性或就地修改类** —— 统一：输入（或就地修改目标）守卫 + 预检 fail-closed + G-78-B 全披露。
/// spike 实测依据（`run-20260917-d053-stage1/spike-edit-tools.json`）：
/// ① Delete_management 对**要素数据集为级联删除**（删 FD 会连带删其内部 FC）；② **目标不存在时 GP 静默成功**（假成功 ⇒ 必须预检）；
/// ③ Rename_management **跨工作区会"成功"但源与目的都不存在**（危险 ⇒ 工具层显式拒绝）；
/// ④ Append_management **NO_TEST 下字段不匹配仍成功并静默丢字段**（⇒ 默认取 TEST）。
/// 错误码：**零新增**（复用 InvalidArgument / NotFound / DatasetNotFound / PathEscapeRejected / OutputExists / InvalidState / GeoprocessingError）。
/// </summary>

/// <summary>删除数据集（Delete_management；**破坏性、不可回退**；`confirm` 缺省/非 true → 拒绝执行）。</summary>
public sealed class DeleteDatasetTool : McpToolBase
{
    public override string Name => "delete_dataset";

    public override string Description =>
        "**删除数据集（破坏性：不可回退，调用方须自行备份）**。参数：datasetPath（目标 FC/表/栅格/要素数据集全路径）、" +
        "confirm（**必须显式传 true**，缺省或 false → INVALID_ARGUMENT 拒绝执行，防误删）。" +
        "预检：① **输入守卫** —— datasetPath 命中 TestFixtures／旧仓库／ARCGIS_PRO_MCP_PROTECTED_ROOTS → " +
        "PATH_ESCAPE_REJECTED（零变更）② **存在性预检** —— 目标确定不存在 → DATASET_NOT_FOUND 提前拒绝；" +
        "存在性不可判定 → INVALID_STATE 保守拒绝（**失败关闭**：不因探测失败而放行删除）③ 目标存在 → 执行 GP。" +
        "**★ 级联披露（D-053 spike 实测）**：目标为**要素数据集时，其内部的要素类会被一并删除**（级联），破坏范围大于目标本身。" +
        "（另：GP 对不存在的目标会静默返回成功，本工具以预检消除该假成功。）" +
        "语义区分：本工具删**磁盘数据集**；remove_layer 只移除**地图图层引用**（不删数据）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["datasetPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Full path of the dataset to delete (feature class / table / raster / feature dataset)." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Must be explicitly true; otherwise the call is refused (INVALID_ARGUMENT)." }
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

        // 1) 输入守卫（受保护根 → 拒绝，零变更）
        var protectedHit = ProtectedOutputPathGuard.Match(target);
        if (protectedHit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"datasetPath '{target}' is inside a protected root ('{protectedHit}'); destructive operation refused (no change made).");
        }

        // 2) confirm 显式确认（缺省/false → 拒绝）
        var confirm = ToolArgs.GetBool(context, "confirm", false);
        if (!confirm)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "confirm must be explicitly true to delete a dataset; the call was refused (no change made).");
        }

        // 3) 存在性预检（GP 对不存在目标静默成功 ⇒ 必须预检，消除假成功）
        var probe = await context.Host.Geoprocessing.CheckOutputExistsAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (!probe.Success)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidState,
                "dataset existence could not be determined; delete refused (fail-closed, no change made).");
        }

        if (probe.Data == OutputExistence.NotExists)
        {
            return OperationResult<object?>.Fail(ErrorCodes.DatasetNotFound,
                $"dataset '{target}' does not exist; delete refused (no change made).");
        }

        if (probe.Data == OutputExistence.Unknown)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidState,
                "dataset existence is unprovable; delete refused (fail-closed, no change made).");
        }

        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "Delete_management", Values = new List<string> { target } },
            context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>同工作区内重命名数据集（Rename_management；**跨工作区显式拒绝**）。</summary>
public sealed class RenameDatasetTool : McpToolBase
{
    /// <summary>G-78-B：数据集名非法字符（与 GP 命名限制一致）。</summary>
    private static readonly char[] InvalidNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    public override string Name => "rename_dataset";

    public override string Description =>
        "**在同一工作区内重命名数据集**（Rename_management；**破坏性引用变更：不可回退**，原名称的所有引用（图层/脚本/地图）将失效，调用方须自行备份与更新引用）。" +
        "参数：datasetPath（现有数据集全路径）、newName（**仅新数据集名，不含路径**）。" +
        "预检：① **输入守卫** —— datasetPath 命中受保护根 → PATH_ESCAPE_REJECTED（零变更）② **新名合法性** —— 空白或含 " +
        "\\ / : * ? \" < > | 等非法字符 → INVALID_ARGUMENT ③ **跨工作区显式拒绝** —— newName 带路径分隔符或解析后容器与源不同 → " +
        "INVALID_ARGUMENT（**D-053 spike 实测：跨工作区 rename 会「成功」但源与目的都不存在，故必须拒绝**）" +
        "④ **目标名已存在 → OUTPUT_EXISTS 提前拒绝**（与 GP ERROR 160326 同义，不进 GP）。" +
        "成功后旧名不在、新名在场（LIVE 判据）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["datasetPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Full path of the existing dataset." },
            ["newName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "New dataset name only (no path); must stay in the same workspace." }
        },
        ["required"] = new[] { "datasetPath", "newName" }
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var source = ToolArgs.GetString(context, "datasetPath");
        var newName = ToolArgs.GetString(context, "newName");
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(newName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "datasetPath and newName are required.");
        }

        // 1) 输入守卫
        var protectedHit = ProtectedOutputPathGuard.Match(source);
        if (protectedHit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"datasetPath '{source}' is inside a protected root ('{protectedHit}'); rename refused (no change made).");
        }

        // 2) 新名合法性（不得为路径形态）
        var trimmed = newName.Trim();
        if (trimmed.Length == 0 || trimmed.IndexOfAny(InvalidNameChars) >= 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "newName must be a bare dataset name (no path separators and none of \\ / : * ? \" < > |).");
        }

        // 3) 跨工作区拒绝：新名所在容器必须与源容器相同
        var container = System.IO.Path.GetDirectoryName(source!.Replace('/', '\\'));
        if (string.IsNullOrEmpty(container))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "datasetPath must be a dataset inside a workspace (container could not be determined).");
        }

        var destination = System.IO.Path.Combine(container!, trimmed);

        // 4) 目标名已存在 → 提前拒绝（复用 OutputExists，零新增）
        var probe = await context.Host.Geoprocessing.CheckOutputExistsAsync(destination, context.CancellationToken).ConfigureAwait(false);
        if (probe.Success && probe.Data == OutputExistence.Exists)
        {
            return OperationResult<object?>.Fail(ErrorCodes.OutputExists,
                $"target name '{trimmed}' already exists in the workspace; rename refused (no change made).");
        }

        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest { ToolName = "Rename_management", Values = new List<string> { source, destination, string.Empty } },
            context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>向既有要素类追加要素（Append_management；**就地修改目标**，schemaType 默认 TEST）。</summary>
public sealed class AppendFeaturesTool : McpToolBase
{
    /// <summary>schema 校验模式白名单（**默认 TEST**：NO_TEST 会静默丢弃不匹配字段 —— D-053 spike 实测）。</summary>
    private static readonly HashSet<string> AllowedSchemaTypes = new(StringComparer.Ordinal) { "TEST", "NO_TEST" };

    public override string Name => "append_features";

    public override string Description =>
        "向**既有要素类追加要素**（Append_management；**就地修改目标数据集、不可回退**，调用方须自行备份）。" +
        "参数：targetPath（目标要素类，**就地修改**）、sourcePaths（分号分隔，每项为数据集路径或活动地图图层名）、" +
        "schemaType（可选，默认 **TEST**；可选 NO_TEST）。" +
        "预检：① **目标守卫** —— 就地修改类守**目标数据集**：targetPath 命中受保护根 → PATH_ESCAPE_REJECTED（零变更）" +
        "② **源路径防穿越** —— 任一源命中受保护根 → PATH_ESCAPE_REJECTED ③ schemaType 白名单校验（非法 → INVALID_ARGUMENT）。" +
        "**★ schema 披露（D-053 spike 实测）**：`NO_TEST` 下**字段不匹配仍会成功并静默丢弃不匹配的字段**（数据静默丢失）⇒ " +
        "本工具默认取 **TEST**（严格校验，不匹配即失败），选用 NO_TEST 须自行承担丢字段风险。" +
        "**字段映射不暴露**（沿用 O-5 教训：不开放自由串映射，避免畸形产物）。" +
        "成功后目标要素数增加（LIVE 判据）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["targetPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target feature class (modified in place)." },
            ["sourcePaths"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Semicolon-separated sources; each item may be a dataset path or a layer name in the active map." },
            ["schemaType"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "TEST (default, strict) or NO_TEST (silently drops mismatched fields)." }
        },
        ["required"] = new[] { "targetPath", "sourcePaths" }
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var target = ToolArgs.GetString(context, "targetPath");
        var sources = ToolArgs.GetString(context, "sourcePaths");
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(sources))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "targetPath and sourcePaths are required.");
        }

        // 1) 就地修改类 → 守**目标数据集**
        var protectedTarget = ProtectedOutputPathGuard.Match(target);
        if (protectedTarget is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"targetPath '{target}' is inside a protected root ('{protectedTarget}'); in-place modification refused (no change made).");
        }

        // 2) 源逐项防穿越（受保护根 / 路径形态）
        foreach (var item in sources!.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var s = item.Trim();
            if (s.Length == 0)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "sourcePaths contains an empty item.");
            }

            var hit = ProtectedOutputPathGuard.Match(s);
            if (hit is not null)
            {
                return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                    $"source '{s}' is inside a protected root ('{hit}'); append refused (no change made).");
            }
        }

        // 3) schemaType 白名单（默认 TEST）
        var schemaType = (ToolArgs.GetString(context, "schemaType") ?? "TEST").Trim().ToUpperInvariant();
        if (!AllowedSchemaTypes.Contains(schemaType))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "schemaType must be one of: TEST, NO_TEST.");
        }

        var gpInputs = GpMultiValueBuilder.FromSemicolonList(sources);
        if (gpInputs is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "sourcePaths must be a semicolon-separated list of dataset paths or layer names (no empty items).");
        }

        // Append_management(inputs, target, {schema_type}, {field_mapping}, {subtype})
        var r = await context.Host.Geoprocessing.RunToolAsync(
            new GeoprocessingRequest
            {
                ToolName = "Append_management",
                Values = new List<string> { gpInputs, target, schemaType, string.Empty, string.Empty }
            },
            context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}
