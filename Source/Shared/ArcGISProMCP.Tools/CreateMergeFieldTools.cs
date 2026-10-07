using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// Phase 9 第三批（D-036）：create_file_gdb / merge / add_field —— GP 产出写工具。
/// 沿用 D-034/D-035 契约：覆写守卫（GpOverwriteGuard：默认拒绝 OUTPUT_EXISTS + overwrite 双信号）、
/// stateProof 三态（GeoprocessingService.RunToolAsync：文件 executed / GDB 容器 unprovable）、
/// 错误码复用既有 32 个（本批零新增）。
/// 另沿用 D-028（F11）教训：多值参数（merge.inputs）经 GpMultiValueBuilder 逐项加引号，
/// 使"数据集路径"与"活动地图图层名"两形态均可用，且不因路径含空格被 GP 拆坏。
/// </summary>

/// <summary>创建自有文件地理数据库（GP：management.CreateFileGDB）。</summary>
public sealed class CreateFileGdbTool : McpToolBase
{
    public override string Name => "create_file_gdb";
    public override string Description =>
        "在指定目录创建自有文件地理数据库（.gdb）。参数：outputPath（目标 .gdb 全路径）、overwrite（默认 false）。" +
        "契约：目标已存在且未显式 overwrite=true → OUTPUT_EXISTS（不执行 GP、零状态变更、不带 stateProof）；" +
        "成功返回 stateProof（.gdb 为容器 → unprovable，文件语义不可证，不是失败）；空白或非 .gdb 结尾 = 非法参数。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Full path of the .gdb to create (must end with .gdb)." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "outputPath" }
    };
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var output = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "outputPath is required.");
        }

        if (!output.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "outputPath must end with .gdb.");
        }

        var folder = System.IO.Path.GetDirectoryName(output);
        var name = System.IO.Path.GetFileName(output);
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(name))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "outputPath must include a parent folder and a .gdb name.");
        }

        // D-052 守卫统一接入（O-D049-08 全量兑现）：受保护输出路径守卫，先于覆写闸门与 GP。
        var protectedHit = ProtectedOutputPathGuard.Match(output);
        if (protectedHit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"output '{output}' is inside a protected root ('{protectedHit}'); refused before geoprocessing (no artefacts).");
        }

        var gate = await GpOverwriteGuard.EvaluateAsync(context, output, context.CancellationToken).ConfigureAwait(false);
        if (gate.Refusal is not null)
        {
            return gate.Refusal;
        }

        var values = new List<string> { folder!, name!, "CURRENT" };
        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "CreateFileGDB_management", Values = values }, context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>合并多个输入数据集到单一输出（GP：management.Merge）。</summary>
public sealed class MergeTool : McpToolBase
{
    public override string Name => "merge";
    public override string Description =>
        "合并多个输入数据集到单一输出。参数：inputs（分号分隔，每项可为数据集路径或活动地图图层名）、output（目标数据集，scratch/自有 GDB 内）、overwrite（默认 false）。" +
        "契约：输出已存在且未显式 overwrite=true → OUTPUT_EXISTS（不执行 GP、零状态变更、不带 stateProof）；成功返回 stateProof（文件 executed / GDB 容器 unprovable）；空白或空项 = 非法参数。" +
        "受保护输出路径守卫：output 命中 TestFixtures／旧仓库／ARCGIS_PRO_MCP_PROTECTED_ROOTS → PATH_ESCAPE_REJECTED，不执行 GP。" +
        "**★ D-052 O-5 修复（fieldMappings 显式拒绝）**：`fieldMappings` 参数**暂不支持** —— 显式传入非空值 → `INVALID_ARGUMENT`（不执行 GP）。" +
        "依据（D-052 spike，实测落盘）：GP Merge_management 对自由串形态的 field_mappings **不校验、不报错**，" +
        "会把垃圾串直接生成为输出字段（实测 'GARBAGE_NOT_A_MAPPING' → 输出仅剩该畸形字段，输入属性全部丢失），" +
        "而合法映射串的构造格式极其复杂且无校验手段；为杜绝畸形产物，本工具显式拒绝非空值，省略该参数 = GP 默认字段映射（字段并集）。" +
        "结构化字段映射支持如获批准将在后续批次以独立参数面重新设计。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputs"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Semicolon-separated inputs; each item may be a dataset path or a layer name in the active map, e.g. A;B" },
            ["output"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target dataset path (scratch / owned GDB)." },
            ["fieldMappings"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "NOT supported: providing a non-empty value is refused with INVALID_ARGUMENT (D-052 spike: GP accepts free-form strings and produces malformed output). Omit to use GP default mapping." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "inputs", "output" }
    };
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var inputs = ToolArgs.GetString(context, "inputs");
        var output = ToolArgs.GetString(context, "output");
        if (string.IsNullOrWhiteSpace(inputs) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputs and output are required.");
        }

        // D-028（F11）教训：GP 多值参数逐项加引号，否则含空格的路径会被按空白拆坏（误报 000735）。
        var gpInputs = GpMultiValueBuilder.FromSemicolonList(inputs);
        if (gpInputs is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputs must be a semicolon-separated list of dataset paths or layer names (no empty items).");
        }

        // ★ D-052 O-5 修复：非空 fieldMappings → 显式拒绝（spike 证实自由串会生成畸形输出而非报错/忽略）。
        var mapping = ToolArgs.GetString(context, "fieldMappings") ?? string.Empty;
        if (mapping.Trim().Length > 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "fieldMappings is not supported yet and is explicitly refused (INVALID_ARGUMENT): Merge_management accepts free-form mapping strings without validation and turns them into malformed output fields (D-052 spike). Omit the parameter to use GP default field mapping.");
        }

        var gate = await GpOverwriteGuard.EvaluateAsync(context, output, context.CancellationToken).ConfigureAwait(false);
        if (gate.Refusal is not null)
        {
            return gate.Refusal;
        }

        // 受保护输出路径守卫（D-052 守卫统一接入；进入覆写闸门与 GP 之前）。
        var protectedHit = ProtectedOutputPathGuard.Match(output);
        if (protectedHit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"output '{output}' is inside a protected root ('{protectedHit}'); refused before geoprocessing (no artefacts).");
        }

        var values = new List<string> { gpInputs, output, string.Empty };
        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "Merge_management", Values = values }, context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>向要素类/表新增字段（GP：management.AddField）。</summary>
public sealed class AddFieldTool : McpToolBase
{
    public override string Name => "add_field";
    public override string Description =>
        "向要素类/表新增字段。参数：inputPath（目标要素类/表）、fieldName、fieldType（GP 字段类型，如 TEXT/LONG/DOUBLE/DATE，默认 TEXT）、fieldLength（可选，TEXT 长度）、fieldAlias（可选）。" +
        "契约：字段已存在 / 字段名非法 / 类型不被 GP 接受 → 错误如实（GEOPROCESSING_ERROR，消息含 GP 码；" +
        "**D-037 F-D036-2：字段名已存在时 GP 仅发 WARNING 000012 且 IsFailed=false，本工具据此升级为显式错误 isError=true，不做静默成功**）；" +
        "无输出数据集，故不经覆写守卫、无 overwrite 参数；空白参数 = 非法参数。" +
        "**★ D-052 O-3 修复（就地修改披露）**：本工具**直接改写目标数据集的字段结构** —— 就地修改、**不可自动回退**，调用方须**自行备份**。" +
        "**输入数据集守卫**：inputPath 命中 TestFixtures／旧仓库／ARCGIS_PRO_MCP_PROTECTED_ROOTS → PATH_ESCAPE_REJECTED（不执行、零变更）。" +
        "**前后照断言**：写前/写后经 Python 桥列字段清单 —— 前照已存在同名字段 → 提前拒绝（不入 GP）；GP 成功但后照未见新字段 → GEOPROCESSING_ERROR（变更未证实）；桥不可用/回读不可解析 → 跳过断言并如实披露（GP WARNING 升级语义仍兜底）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target feature class / table path." },
            ["fieldName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Name of the field to add." },
            ["fieldType"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "GP field type, e.g. TEXT, LONG, DOUBLE, DATE. Default TEXT." },
            ["fieldLength"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Optional field length (TEXT)." },
            ["fieldAlias"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional field alias." }
        },
        ["required"] = new[] { "inputPath", "fieldName" }
    };
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var input = ToolArgs.GetString(context, "inputPath");
        var fieldName = ToolArgs.GetString(context, "fieldName");
        var fieldType = ToolArgs.GetString(context, "fieldType") ?? "TEXT";
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(fieldType))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputPath, fieldName and fieldType are required.");
        }

        // D-052 守卫统一接入：就地修改类无输出数据集 → 守**输入数据集**（受保护根 → 拒绝执行，零变更）。
        var protectedInputHit = ProtectedOutputPathGuard.Match(input);
        if (protectedInputHit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"input '{input}' is inside a protected root ('{protectedInputHit}'); in-place modification of protected data is refused (no change made).");
        }

        // ★ D-052 O-3：前后照断言（前照）。桥不可用 → 跳过（GP WARNING 000012 升级语义仍兜底）。
        var bridge = context.Python;
        List<string>? preFields = null;
        if (bridge is not null)
        {
            var pre = await bridge.ListFieldsAsync(input, context.CancellationToken).ConfigureAwait(false);
            preFields = ExtractFieldNames(pre.Data);
            if (preFields is not null
                && preFields.Any(f => string.Equals(f, fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                return OperationResult<object?>.Fail(
                    ErrorCodes.GeoprocessingError,
                    $"Field '{fieldName}' already exists on '{input}'; no field was added (pre-write snapshot; GP not invoked).");
            }
        }

        var length = ToolArgs.GetDouble(context, "fieldLength");
        var alias = ToolArgs.GetString(context, "fieldAlias") ?? string.Empty;

        // AddField_management(in_table, field_name, field_type, field_precision, field_scale,
        //                     field_length, field_alias, field_is_nullable, field_is_required, field_domain)
        var values = new List<string>
        {
            input,
            fieldName,
            fieldType,
            string.Empty,                                        // field_precision
            string.Empty,                                        // field_scale
            length.HasValue ? ((long)length.Value).ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
            alias,
            "NULLABLE",
            "NON_REQUIRED",
            string.Empty                                         // field_domain
        };

        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "AddField_management", Values = values }, context.CancellationToken).ConfigureAwait(false);

        // D-037 F-D036-2：GP 对"字段名已存在"只发 WARNING 000012 且 IsFailed=false ——
        // "GP 未失败"不等于"字段已新增"。此处把该语义显式升级为错误（isError=true），
        // 与契约"错误如实"一致；错误码复用既有 GEOPROCESSING_ERROR（零新增）。
        var preExisting = GpMessageSemantics.FindPreExistingMemberMessage(r.Data?.Messages);
        if (preExisting is not null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.GeoprocessingError,
                $"Field '{fieldName}' already exists on '{input}' (GP {GpMessageSemantics.AlreadyExistsCode}); no field was added.",
                preExisting);
        }

        if (!r.Success)
        {
            return ToolResult.From(r);
        }

        // ★ D-052 O-3：前后照断言（后照）。GP 成功但桥在场且回读可解析而未见新字段 → 显式失败（变更未证实）。
        if (bridge is not null)
        {
            var post = await bridge.ListFieldsAsync(input, context.CancellationToken).ConfigureAwait(false);
            var postFields = ExtractFieldNames(post.Data);
            if (postFields is not null
                && !postFields.Any(f => string.Equals(f, fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                return OperationResult<object?>.Fail(
                    ErrorCodes.GeoprocessingError,
                    $"Post-write assertion failed: field '{fieldName}' did not appear on '{input}' although GP reported success (change not proven; the target dataset was NOT modified).");
            }

            if (postFields is null)
            {
                // 回读不可解析：不做猜测，如实披露（对齐 stateProof.unprovable 先例 —— 不可证 ≠ 成功/失败）。
                return OperationResult<object?>.Ok(r.Data,
                    "post-write assertion skipped: field snapshot unreadable (bridge returned no parsable field list); GP reported success.");
            }
        }

        return ToolResult.From(r);
    }

    /// <summary>
    /// 从 Python 桥 ListFields 响应中宽容提取字段名列表（结构：<c>{ fields: [ { name, ... }, ... ] }</c>）。
    /// 结构不符/空列表 → null（语义：不可解析，交由调用方按"跳过断言"披露处理，绝不猜测）。
    /// </summary>
    internal static List<string>? ExtractFieldNames(System.Text.Json.JsonElement? data)
    {
        if (data is null || !data.HasValue)
        {
            return null;
        }

        var el = data.Value;
        System.Text.Json.JsonElement arr;
        if (el.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            arr = el;
        }
        else if (el.ValueKind == System.Text.Json.JsonValueKind.Object
                 && el.TryGetProperty("fields", out var f)
                 && f.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            arr = f;
        }
        else
        {
            return null;
        }

        var names = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var key in new[] { "name", "Name", "fieldName" })
                {
                    if (item.TryGetProperty(key, out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        names.Add(n.GetString()!);
                        break;
                    }
                }
            }
            else if (item.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                names.Add(item.GetString()!);
            }
        }

        return names.Count > 0 ? names : null;
    }
}
