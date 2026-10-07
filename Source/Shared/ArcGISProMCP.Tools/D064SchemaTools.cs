using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

// ══════════════════════════════════════════════════════════════════════════════
// D-064 · A 段：Schema 创建（6 件）
//   create_feature_class / create_table / add_fields / delete_field / truncate_table / export_features
// 守卫链（与既有专用 GP 家族同源，不新增错误码）：
//   输出类（create_*/export_features）＝ ProtectedOutputPathGuard → GpOverwriteGuard → 宿主执行；
//   就地类（add_fields/delete_field/truncate_table）＝ 以**输入数据集**过同一守卫；
//   破坏性类（delete_field/truncate_table）＝ confirm 缺省拒（缺省/false → INVALID_ARGUMENT，零变更）。
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>D-064 · 在 GDB 内建要素类（GP：management.CreateFeatureclass）。</summary>
public sealed class CreateFeatureClassTool : McpToolBase
{
    public override string Name => "create_feature_class";

    public override string Description =>
        "在文件地理数据库（.gdb）内新建要素类；可选指定空间参考与字段集，可选项加入当前地图。参数：" +
        "outputPath（**必须在 .gdb 内**，如 D:\\data\\x.gdb\\roads）、geometryType（POINT/MULTIPOINT/POLYLINE/POLYGON；" +
        "缺省 POINT）、spatialReference（WKID 或名称/WKT，可省略 ⇒ 宿主默认）、fields（数组，元素 = {name,type,length?,precision?,scale?,nullable?,alias?,defaultValue?}）、" +
        "addToMap（缺省 false）、overwrite（缺省 false）。" +
        "契约：输出已存在且未显式 overwrite=true → OUTPUT_EXISTS（不执行 GP、零状态变更）；空白/非 .gdb 路径/非法几何类型 → INVALID_ARGUMENT；" +
        "受保护输出根（TestFixtures／旧仓库／ARCGIS_PRO_MCP_PROTECTED_ROOTS）→ PATH_ESCAPE_REJECTED（不执行 GP、零产物）；" +
        "成功返回 stateProof（文件 executed / GDB 容器 unprovable）与**写后读回**的字段清单。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Feature class path inside a .gdb, e.g. D:\\data\\x.gdb\\roads." },
            ["geometryType"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "POINT | MULTIPOINT | POLYLINE | POLYGON (default POINT)." },
            ["spatialReference"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "WKID number or SR name/WKT; omit for host default." },
            ["fields"] = new Dictionary<string, object?>
            {
                ["type"] = "array",
                ["description"] = "Optional field specs: {name,type,length?,precision?,scale?,nullable?,alias?,defaultValue?}.",
                ["items"] = new Dictionary<string, object?> { ["type"] = "object" },
            },
            ["addToMap"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Add the new feature class to the active map. Default false." },
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

        if (!D064SchemaValidation.IsInsideGdb(output!))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, "outputPath must be a dataset path inside a .gdb (e.g. D:\\data\\x.gdb\\roads).");
        }

        var geometry = (ToolArgs.GetString(context, "geometryType") ?? "POINT").Trim().ToUpperInvariant();
        if (!D064SchemaValidation.GeometryTypes.Contains(geometry))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                $"unsupported geometryType '{geometry}'; allowed: {string.Join(", ", D064SchemaValidation.GeometryTypes)}.");
        }

        var specs = D064SchemaValidation.ParseFieldSpecs(context, "fields");
        if (specs.Error is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, specs.Error);
        }

        var hit = ProtectedOutputPathGuard.Match(output);
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"output '{output}' is inside a protected root ('{hit}'); refused before geoprocessing (no artefacts).");
        }

        var gate = await GpOverwriteGuard.EvaluateAsync(context, output!, context.CancellationToken).ConfigureAwait(false);
        if (gate.Refusal is not null)
        {
            return gate.Refusal;
        }

        var r = await context.Host.Schema.CreateFeatureClassAsync(
            output!, geometry, ToolArgs.GetString(context, "spatialReference"), specs.Fields,
            ToolArgs.GetBool(context, "addToMap"), context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>D-064 · 在 GDB 内建独立表（GP：management.CreateTable）。</summary>
public sealed class CreateTableTool : McpToolBase
{
    public override string Name => "create_table";

    public override string Description =>
        "在文件地理数据库内新建独立表（非要素类）；可选字段集与加入地图。参数：outputPath（**必须在 .gdb 内**）、" +
        "fields（数组，元素 = {name,type,length?,precision?,scale?,nullable?,alias?,defaultValue?}）、addToMap（缺省 false）、overwrite（缺省 false）。" +
        "契约：输出已存在且未显式 overwrite=true → OUTPUT_EXISTS（不执行 GP）；非 .gdb 路径/空字段规格 → INVALID_ARGUMENT；" +
        "受保护输出根 → PATH_ESCAPE_REJECTED（零产物）；成功返回 stateProof 与写后读回字段清单。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Table path inside a .gdb, e.g. D:\\data\\x.gdb\\survey." },
            ["fields"] = new Dictionary<string, object?>
            {
                ["type"] = "array",
                ["description"] = "Optional field specs: {name,type,length?,precision?,scale?,nullable?,alias?,defaultValue?}.",
                ["items"] = new Dictionary<string, object?> { ["type"] = "object" },
            },
            ["addToMap"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Add the new table to the active map. Default false." },
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

        if (!D064SchemaValidation.IsInsideGdb(output!))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, "outputPath must be a dataset path inside a .gdb (e.g. D:\\data\\x.gdb\\survey).");
        }

        var specs = D064SchemaValidation.ParseFieldSpecs(context, "fields");
        if (specs.Error is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, specs.Error);
        }

        var hit = ProtectedOutputPathGuard.Match(output);
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"output '{output}' is inside a protected root ('{hit}'); refused before geoprocessing (no artefacts).");
        }

        var gate = await GpOverwriteGuard.EvaluateAsync(context, output!, context.CancellationToken).ConfigureAwait(false);
        if (gate.Refusal is not null)
        {
            return gate.Refusal;
        }

        var r = await context.Host.Schema.CreateTableAsync(
            output!, specs.Fields, ToolArgs.GetBool(context, "addToMap"), context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>D-064 · 就地批量加字段（GP：management.AddField ×N；缺省拒已存在）。</summary>
public sealed class AddFieldsTool : McpToolBase
{
    public override string Name => "add_fields";

    public override string Description =>
        "**就地**为要素类/表批量新增字段（单次可多字段，逐字段调用 AddField）。参数：path（GDB 内数据集路径）、" +
        "fields（数组，元素 = {name,type,length?,precision?,scale?,nullable?,alias?,defaultValue?}）。" +
        "契约：**缺省拒绝已存在字段**（跳过并如实列入 skippedExisting，不报错、不改动它）；规格非法（空名/类型不在允许清单/字段名重复）→ INVALID_ARGUMENT（**整个请求不提交**，零变更）；" +
        "**就地修改 ⇒ 以输入数据集过受保护根守卫**（命中 → PATH_ESCAPE_REJECTED，零变更）；返回 fieldBefore/fieldAfter 前后照。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["path"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Dataset path inside a .gdb (feature class or table)." },
            ["fields"] = new Dictionary<string, object?>
            {
                ["type"] = "array",
                ["description"] = "Field specs: {name,type,length?,precision?,scale?,nullable?,alias?,defaultValue?}.",
                ["items"] = new Dictionary<string, object?> { ["type"] = "object" },
            },
        },
        ["required"] = new[] { "path", "fields" }
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var path = ToolArgs.GetString(context, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "path is required.");
        }

        var specs = D064SchemaValidation.ParseFieldSpecs(context, "fields");
        if (specs.Error is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, specs.Error);
        }

        if (specs.Fields is null || specs.Fields.Count == 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "fields must contain at least one field spec.");
        }

        var hit = ProtectedOutputPathGuard.Match(path);
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"input '{path}' is inside a protected root ('{hit}'); in-place modification refused (no change made).");
        }

        var r = await context.Host.Schema.AddFieldsAsync(path!, specs.Fields, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · 就地删字段（GP：management.DeleteField；破坏性 ⇒ confirm 缺省拒）。</summary>
public sealed class DeleteFieldTool : McpToolBase
{
    public override string Name => "delete_field";

    public override string Description =>
        "**就地删除字段**（破坏性：字段及其全部取值不可恢复）。参数：path（GDB 内数据集路径）、fieldName、confirm。" +
        "契约：**confirm 必须显式 true**（缺省或 false → INVALID_ARGUMENT 拒绝执行，零变更）；" +
        "以输入数据集过受保护根守卫（命中 → PATH_ESCAPE_REJECTED）；字段不存在 → NOT_FOUND；返回 fieldBefore/fieldAfter 对照。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["path"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Dataset path inside a .gdb." },
            ["fieldName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Field to delete." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Must be explicitly true; otherwise refused (INVALID_ARGUMENT)." },
        },
        ["required"] = new[] { "path", "fieldName", "confirm" }
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var path = ToolArgs.GetString(context, "path");
        var field = ToolArgs.GetString(context, "fieldName");
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(field))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "path and fieldName are required.");
        }

        if (!ToolArgs.GetBool(context, "confirm"))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "delete_field is destructive: confirm=true is required (default-refuse); no change was made.");
        }

        var hit = ProtectedOutputPathGuard.Match(path);
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"input '{path}' is inside a protected root ('{hit}'); destructive in-place change refused (no change made).");
        }

        var r = await context.Host.Schema.DeleteFieldAsync(path!, field!, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · 就地清空行（GP：management.TruncateTable；破坏性 ⇒ confirm 缺省拒 + 行数前后照）。</summary>
public sealed class TruncateTableTool : McpToolBase
{
    public override string Name => "truncate_table";

    public override string Description =>
        "**就地清空要素类/表的全部行**（保 schema；破坏性：行数据不可恢复）。参数：path（GDB 内数据集路径）、confirm。" +
        "契约：**confirm 必须显式 true**（缺省或 false → INVALID_ARGUMENT，零变更）；以输入数据集过受保护根守卫（命中 → PATH_ESCAPE_REJECTED）；" +
        "返回 rowsBefore/rowsAfter（成功应为 0）+ schemaPreserved（字段清单未变）+ stateProof。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["path"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Dataset path inside a .gdb." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Must be explicitly true; otherwise refused (INVALID_ARGUMENT)." },
        },
        ["required"] = new[] { "path", "confirm" }
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var path = ToolArgs.GetString(context, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "path is required.");
        }

        if (!ToolArgs.GetBool(context, "confirm"))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "truncate_table is destructive: confirm=true is required (default-refuse); no change was made.");
        }

        var hit = ProtectedOutputPathGuard.Match(path);
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"input '{path}' is inside a protected root ('{hit}'); destructive in-place change refused (no change made).");
        }

        var r = await context.Host.Schema.TruncateTableAsync(path!, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · 导出图层/表到新数据集（GP：conversion.ExportFeatures；尊重当前选择与定义查询）。</summary>
public sealed class ExportFeaturesTool : McpToolBase
{
    public override string Name => "export_features";

    public override string Description =>
        "把**活动地图中的图层**（或 GDB 内数据集）导出为新数据集。参数：inputLayerName（图层名或数据集路径）、" +
        "outputPath（目标数据集全路径，如 D:\\data\\x.gdb\\roads_sel）、mapName（可省略 = 活动地图）、" +
        "whereClause（可选，与定义查询**叠加**）、useSelection（缺省 true = **尊重当前选择**，仅导出选中要素；无选择时全量并如实披露）、overwrite（缺省 false）。" +
        "契约：**定义查询始终生效**（GP 图层语义）；**图层名按 GP 语义在\"活动地图\"中解析 ⇒ 无活动视图时请直接给数据集路径**"
        + "（或先 `activate_map` 打开视图）；输出已存在且未显式 overwrite=true → OUTPUT_EXISTS（不执行 GP）；" +
        "受保护输出根 → PATH_ESCAPE_REJECTED（零产物）；返回 selectionApplied/sourceCount/selectedCount/exportedCount 与 stateProof。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["inputLayerName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layer name in the active map, or a dataset path." },
            ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Output dataset path (e.g. D:\\data\\x.gdb\\roads_sel)." },
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Map name; omit for the active map." },
            ["whereClause"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional SQL where, combined with the layer definition query." },
            ["useSelection"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Respect the current selection (default true); ignored when there is no selection (disclosed)." },
            ["overwrite"] = OverwritePolicy.SchemaProperty
        },
        ["required"] = new[] { "inputLayerName", "outputPath" }
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var input = ToolArgs.GetString(context, "inputLayerName");
        var output = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputLayerName and outputPath are required.");
        }

        var hit = ProtectedOutputPathGuard.Match(output);
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"output '{output}' is inside a protected root ('{hit}'); refused before geoprocessing (no artefacts).");
        }

        var gate = await GpOverwriteGuard.EvaluateAsync(context, output!, context.CancellationToken).ConfigureAwait(false);
        if (gate.Refusal is not null)
        {
            return gate.Refusal;
        }

        var r = await context.Host.Schema.ExportFeaturesAsync(
            ToolArgs.GetString(context, "mapName"), input!, output!, ToolArgs.GetString(context, "whereClause"),
            ToolArgs.GetBool(context, "useSelection", true), context.CancellationToken).ConfigureAwait(false);
        return gate.Annotate(ToolResult.From(r));
    }
}

/// <summary>D-064 · A 段共享校验（字段规格解析 / .gdb 路径判定 / 几何类型清单）。纯函数。</summary>
internal static class D064SchemaValidation
{
    /// <summary>允许的几何类型（GP CreateFeatureclass 的 geometry_type 全集）。</summary>
    public static readonly IReadOnlyCollection<string> GeometryTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "POINT", "MULTIPOINT", "POLYLINE", "POLYGON",
    };

    /// <summary>允许的字段类型（GP Field 类型的工具层准入清单，大小写不敏感）。</summary>
    public static readonly IReadOnlyCollection<string> FieldTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "TEXT", "SHORT", "LONG", "FLOAT", "DOUBLE", "DATE", "BLOB", "GUID", "RASTER",
    };

    /// <summary>路径是否位于 <c>.gdb</c> 容器**内**（末段非 .gdb，但其祖先含 .gdb）。</summary>
    public static bool IsInsideGdb(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var segments = path!.Split('\\', '/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        // 末段不得以 .gdb 结尾（那是容器本体，不是数据集）
        if (segments[^1].EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return segments.Any(s => s.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>字段规格解析结果。</summary>
    public sealed class FieldSpecParse
    {
        public IReadOnlyList<SchemaFieldSpec>? Fields { get; init; }

        public string? Error { get; init; }
    }

    /// <summary>
    /// 解析字段规格数组（严格：空名/未知类型/重复名/负长度 ⇒ 整体拒绝，不做部分提交）。
    /// </summary>
    public static FieldSpecParse ParseFieldSpecs(ToolExecutionContext context, string key)
    {
        var raw = ToolArgs.GetObjectList(context, key);
        if (raw.Count == 0)
        {
            return new FieldSpecParse { Fields = Array.Empty<SchemaFieldSpec>() };
        }

        var list = new List<SchemaFieldSpec>(raw.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < raw.Count; i++)
        {
            var item = raw[i];
            if (item is null)
            {
                return new FieldSpecParse { Error = $"fields[{i}] must be an object." };
            }

            var name = ToolArgs.ReadString(item, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                return new FieldSpecParse { Error = $"fields[{i}].name is required." };
            }

            if (!seen.Add(name!))
            {
                return new FieldSpecParse { Error = $"duplicate field name '{name}' in fields[]." };
            }

            var type = ToolArgs.ReadString(item, "type");
            if (string.IsNullOrWhiteSpace(type))
            {
                return new FieldSpecParse { Error = $"fields[{i}].type is required (one of {string.Join(", ", FieldTypes)})." };
            }

            if (!FieldTypes.Contains(type!))
            {
                return new FieldSpecParse { Error = $"fields[{i}].type '{type}' is not supported; allowed: {string.Join(", ", FieldTypes)}." };
            }

            var length = ToolArgs.ReadInt(item, "length");
            if (length is < 0)
            {
                return new FieldSpecParse { Error = $"fields[{i}].length must be >= 0." };
            }

            var precision = ToolArgs.ReadInt(item, "precision");
            var scale = ToolArgs.ReadInt(item, "scale");
            if (precision is < 0 || scale is < 0)
            {
                return new FieldSpecParse { Error = $"fields[{i}].precision/scale must be >= 0." };
            }

            // nullable：显式 false 才算不可空；缺省 = true（与既有 add_field 默认一致）。
            bool? nullable = item.ContainsKey("nullable") ? ToolArgs.ReadBool(item, "nullable", true) : null;

            list.Add(new SchemaFieldSpec
            {
                Name = name!,
                Type = type!.ToUpperInvariant(),
                Length = length,
                Precision = precision,
                Scale = scale,
                Nullable = nullable,
                Alias = ToolArgs.ReadString(item, "alias"),
                DefaultValue = ToolArgs.ReadString(item, "defaultValue"),
            });
        }

        return new FieldSpecParse { Fields = list };
    }
}
