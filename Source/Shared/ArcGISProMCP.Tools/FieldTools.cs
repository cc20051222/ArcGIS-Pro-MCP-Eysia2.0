using System.Text.RegularExpressions;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// Phase 9 第四批（D-038）：alter_field / calculate_field / get_field_values。
/// 本批三工具均为**就地修改/只读**语义，**无输出数据集 → 不适用覆写门**；
/// stateProof 以"被修改的输入数据集"为目标（GDB 容器内 → 恒 unprovable，如实）。
/// **安全核心**：calculate_field 仅 SQL 表达式（expression_type 硬编码 "SQL"），
/// 表达式经 <see cref="FieldExpressionPolicy"/> 白名单**先校验后下传**，
/// 绝不允许 PYTHON3/PYTHON/ARCADE、code_block、多语句、注释、子查询与任意函数。
/// </summary>

/// <summary>calculate_field 受限表达式白名单（**纯逻辑层，独立成类便于审计与单测**；D-038 安全核心）。</summary>
public static class FieldExpressionPolicy
{
    /// <summary>表达式最大长度（防超长注入面）。</summary>
    public const int MaxLength = 8192;

    /// <summary>禁词表（词边界、不区分大小写）：任意 Python/子查询/DDL/DML 痕迹一律拒绝。</summary>
    public static readonly IReadOnlySet<string> ForbiddenWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "import", "select", "exec", "execute", "insert", "update", "delete", "drop",
        "create", "alter", "grant", "revoke", "union", "truncate", "merge", "code_block",
        "python", "python3", "arcade",
    };

    /// <summary>禁前缀（词首）：xp_ / sp_。</summary>
    public static readonly IReadOnlyList<string> ForbiddenPrefixes = new[] { "xp_", "sp_" };

    /// <summary>本批**不开放任何函数调用**（plan §6："若无法在 SQL 通道保证可用则直接不纳入白名单"）。</summary>
    public static readonly IReadOnlySet<string> AllowedFunctions = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>校验表达式。合法 → true；否则 false + reason（→ INVALID_ARGUMENT，不进 GP）。</summary>
    public static bool IsSafe(string? expression, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(expression))
        {
            reason = "expression is required.";
            return false;
        }

        if (expression.Length > MaxLength)
        {
            reason = $"expression exceeds the maximum length ({MaxLength}).";
            return false;
        }

        if (expression.Any(char.IsControl))
        {
            reason = "control characters are not allowed.";
            return false;
        }

        if (expression.Contains('\0'))
        {
            reason = "NUL is not allowed.";
            return false;
        }

        if (expression.Contains(';'))
        {
            reason = "multiple statements are not allowed (';' found).";
            return false;
        }

        if (expression.Contains("--"))
        {
            reason = "comments are not allowed ('--' found).";
            return false;
        }

        if (expression.Contains("/*") || expression.Contains("*/"))
        {
            reason = "comments are not allowed (block comment found).";
            return false;
        }

        if (expression.Contains('`'))
        {
            reason = "backquotes are not allowed.";
            return false;
        }

        if (expression.Contains("__"))
        {
            reason = "'__' is not allowed.";
            return false;
        }

        // 词边界禁词（含 xp_/sp_ 前缀形态）。
        foreach (var token in Regex.Matches(expression, "[A-Za-z_][A-Za-z0-9_]*").Select(m => m.Value))
        {
            var lower = token.ToLowerInvariant();
            if (ForbiddenWords.Contains(lower))
            {
                reason = $"forbidden keyword '{token}'.";
                return false;
            }

            if (ForbiddenPrefixes.Any(p => lower.StartsWith(p, StringComparison.Ordinal)))
            {
                reason = $"forbidden prefix '{token}'.";
                return false;
            }
        }

        // 函数调用：本批白名单为空 → 任何 `name(` 形态一律拒绝（SQL 关键字后随括号不算函数调用）。
        var sqlKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "AND", "OR", "NOT", "IS", "NULL", "CASE", "WHEN", "THEN", "ELSE", "END" };
        foreach (Match call in Regex.Matches(expression, @"([A-Za-z_][A-Za-z0-9_]*)\s*\("))
        {
            var name = call.Groups[1].Value;
            if (sqlKeywords.Contains(name))
            {
                continue;
            }

            if (!AllowedFunctions.Contains(name.ToUpperInvariant()))
            {
                reason = $"function calls are not allowed ('{name}('); this batch opens no functions.";
                return false;
            }
        }

        return true;
    }
}

/// <summary>修改既有字段元数据（别名/可空/长度(TEXT)）；**改名与删除不在参数面**（GP 改名位传原字段名 = no-op）。</summary>
public sealed class AlterFieldTool : McpToolBase
{
    public override string Name => "alter_field";
    public override string Description =>
        "修改既有字段的元数据：别名 / 可空性 / 长度（仅 TEXT 字段）。参数：inputPath、fieldName、fieldAlias（可选）、fieldIsNullable（可选）、fieldLength（可选，仅 TEXT）。" +
        "边界：**不改类型、不改名、不删除**——GP 改名位传回原字段名（no-op）；改名/删除转 Phase 12。" +
        "本工具为**就地修改、不可回退**（无输出数据集、无覆写门），调用方须自行备份；字段不存在 → 错误如实（含 GP 码）。" +
        "fieldAlias 语义：**省略该参数 = 保持别名不变**；**显式传入空白值将被拒绝**（INVALID_ARGUMENT，不入 GP）；" +
        "clear_field_alias 位固定下传 DO_NOT_CLEAR（**空串会被 GP 视为\"已指定清除\"** → 与别名互斥 → ERROR 001656，见 F-D038-2）。" +
        "注意：默认值修改不在参数面（其回溯填充副作用转 Phase 12 一并评估）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target feature class / table path." },
            ["fieldName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Existing field to alter." },
            ["fieldAlias"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional new alias." },
            ["fieldIsNullable"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Optional nullability." },
            ["fieldLength"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Optional length (TEXT fields only)." }
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
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(fieldName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputPath and fieldName are required.");
        }

        // D-052 守卫统一接入：就地修改类无输出数据集 → 守**输入数据集**（受保护根 → 拒绝执行，零变更）。
        var protectedInputHit = ProtectedOutputPathGuard.Match(input);
        if (protectedInputHit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"input '{input}' is inside a protected root ('{protectedInputHit}'); in-place modification of protected data is refused (no change made).");
        }

        // AlterField_management(in_table, field, {new_field_name}, {new_field_alias},
        //                       {field_type}, {field_length}, {field_is_nullable}, {clear_field_alias})
        // 改名位传回原字段名（no-op）；类型位空（不改类型——契约明示）。
        // fieldAlias：**省略 = 保持不变**；**显式传空白 = policy 层拒绝**（不入 GP，G-78-A 口径）。
        if (context.Arguments is not null && context.Arguments.ContainsKey("fieldAlias"))
        {
            var aliasRaw = ToolArgs.GetString(context, "fieldAlias");
            if (aliasRaw is not null && aliasRaw.Trim().Length == 0)
            {
                return OperationResult<object?>.Fail(
                    ErrorCodes.InvalidArgument,
                    "fieldAlias must not be blank when provided; omit it to leave the alias unchanged.");
            }
        }
        var alias = ToolArgs.GetString(context, "fieldAlias") ?? string.Empty;
        var length = ToolArgs.GetDouble(context, "fieldLength");
        var lengthText = length.HasValue
            ? ((long)length.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
        var nullableText = ToolArgs.GetBool(context, "fieldIsNullable", true)
            ? (context.Arguments is not null && context.Arguments.ContainsKey("fieldIsNullable") ? "NULLABLE" : string.Empty)
            : "NON_NULLABLE";

        var values = new List<string>
        {
            // F-D038-2：第 8 位 {clear_field_alias} **不得传空串** —— 空串被 GP 视为"已指定清除"，
            // 与同传的 new_field_alias 互斥 → ERROR 001656（别名路径曾因此完全不可用）。
            // 固定 "DO_NOT_CLEAR" = 永不通过本工具清除别名（别名只能被设置为非空值）。
            input, fieldName, fieldName, alias, string.Empty, lengthText, nullableText, "DO_NOT_CLEAR",
        };

        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "AlterField_management", Values = values }, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>按受限 SQL 表达式批量计算字段值（GP CalculateField，expression_type 硬编码 SQL；禁 Python/代码块）。</summary>
public sealed class CalculateFieldTool : McpToolBase
{
    public override string Name => "calculate_field";
    public override string Description =>
        "按**受限 SQL 表达式**批量计算字段值（GP CalculateField，expression_type 硬编码 \"SQL\"）。参数：inputPath、fieldName、expression（SQL-92：字段名/字面量/算术/比较/AND OR NOT/IS NULL/CASE WHEN）、onlyWhenNull（默认 true = 仅填充空值，false 才全量重算）。" +
        "安全：禁 PYTHON3/PYTHON/ARCADE 与 code_block（参数面不暴露）；表达式经白名单先校验后下传——分号/注释(--、/*)/反引号/__/函数调用/保留字(import、select、exec、drop 等) → INVALID_ARGUMENT 且不进 GP；不得用字符串拼接构造表达式。" +
        "本工具**就地改写字段值、不可回退**（无输出数据集、无覆写门），调用方须自行备份；GP 错误（如 000800/000728）原样透出。" +
        "onlyWhenNull=true 通过 SQL CASE 包裹实现（CASE WHEN <字段> IS NULL THEN (<表达式>) ELSE <字段> END）。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target feature class / table path." },
            ["fieldName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Existing field to update." },
            ["expression"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Restricted SQL expression (SQL-92 subset; no functions, no subqueries, no multi-statement)." },
            ["onlyWhenNull"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Default true: only fill NULL rows; false = recompute all rows." }
        },
        ["required"] = new[] { "inputPath", "fieldName", "expression" }
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
        var expression = ToolArgs.GetString(context, "expression");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(expression))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputPath, fieldName and expression are required.");
        }

        // D-052 守卫统一接入：就地修改类无输出数据集 → 守**输入数据集**（受保护根 → 拒绝执行，零变更）。
        var protectedInputHit = ProtectedOutputPathGuard.Match(input);
        if (protectedInputHit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"input '{input}' is inside a protected root ('{protectedInputHit}'); in-place modification of protected data is refused (no change made).");
        }

        if (!FieldExpressionPolicy.IsSafe(expression, out var reason))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"expression rejected: {reason}");
        }

        var onlyWhenNull = ToolArgs.GetBool(context, "onlyWhenNull", true);   // 默认 true：仅填充空值（契约须披露）
        var finalExpression = onlyWhenNull
            ? $"CASE WHEN {fieldName} IS NULL THEN ({expression}) ELSE {fieldName} END"
            : expression;

        // CalculateField_management(in_table, field, expression, {expression_type}, {code_block}, {field_type}, {enforce_domains})
        // expression_type 硬编码 "SQL"；code_block 恒空（参数面不暴露）。
        var values = new List<string> { input, fieldName, finalExpression, "SQL", string.Empty, string.Empty, string.Empty };

        var r = await context.Host.Geoprocessing.RunToolAsync(new GeoprocessingRequest { ToolName = "CalculateField_management", Values = values }, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>字段值域画像（只读：唯一值枚举/最小最大/空值计数；maxDistinct 默认 200、上限 2000，超出截断）。</summary>
public sealed class GetFieldValuesTool : McpToolBase
{
    public override string Name => "get_field_values";
    public override string Description =>
        "字段值域画像（**只读，零写入**）：唯一值枚举（带上限与截断标注）/ 最小最大值 / 空值计数，用于计算前画像与结果验证。" +
        "参数：mapName（可选，缺省活动地图）、layerName、fieldName、maxDistinct（可选，默认 200，上限 2000，超出 → truncated=true + 精确计数）。" +
        "字段不存在 / 表超扫描上限（200,000 行）→ 显式错误（不返回无界数据）；空表与全空字段可区分。";
    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional map name (default: active map)." },
            ["layerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target layer / table name." },
            ["fieldName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Field to profile." },
            ["maxDistinct"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Optional distinct-value cap; default 200, max 2000." }
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
        var fieldName = ToolArgs.GetString(context, "fieldName");
        if (string.IsNullOrWhiteSpace(layerName) || string.IsNullOrWhiteSpace(fieldName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layerName and fieldName are required.");
        }

        var maxDistinctRaw = ToolArgs.GetDouble(context, "maxDistinct");
        var maxDistinct = maxDistinctRaw.HasValue ? (int)maxDistinctRaw.Value : 200;
        if (maxDistinct > 2000)
        {
            maxDistinct = 2000;   // 上限钳制（契约：默认 200 / 上限 2000）
        }

        var r = await context.Host.Attributes.GetFieldValuesAsync(
            ToolArgs.GetString(context, "mapName") ?? string.Empty,
            layerName, fieldName, maxDistinct, context.CancellationToken).ConfigureAwait(false);
        return r.Success
            ? OperationResult<object?>.Ok(r.Data)
            : OperationResult<object?>.Fail(r.Errors);
    }
}
