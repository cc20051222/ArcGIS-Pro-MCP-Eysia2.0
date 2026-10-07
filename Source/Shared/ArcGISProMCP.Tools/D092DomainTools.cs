using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

// ═══════════════════════════════════════════════════════════════════════════════
// D-092 · M2 批二：P3/P4 域治理与数据管理工具（9W＋1R；179→189）
// 冻结契约：.runtime/evolution/v5-f/run-20260928-d082/f03b-5-schemas/
// 实现先例：D084Tools.cs＋D086DesignTools.cs＋D088QualityTools.cs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>D-092 P3：创建域（W）。</summary>
public sealed class CreateDomainTool : McpToolBase
{
    public override string Name => "create_domain";
    public override string Description => "在 GDB 工作空间中创建编码值域或范围域；CodedValue 须附 codedValues，Range 须附 range。";
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D092Schemas.CreateDomain;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D092Domain is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-092 domain service is unavailable.");

        var workspace = ToolArgs.GetString(context, "workspace");
        var domainName = ToolArgs.GetString(context, "domainName");
        var domainType = ToolArgs.GetString(context, "domainType");
        if (string.IsNullOrWhiteSpace(workspace) || string.IsNullOrWhiteSpace(domainName))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "workspace and domainName are required.");
        if (domainType is not ("CodedValue" or "Range"))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "domainType must be CodedValue or Range.");

        var codedValues = ToolArgs.GetList(context, "codedValues");
        var range = ToolArgs.GetObject(context, "range");
        if (domainType == "CodedValue" && codedValues.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "codedValues is required when domainType is CodedValue.");
        if (domainType == "Range" && range is null)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "range is required when domainType is Range.");

        var result = await service.CreateDomainAsync(workspace!, domainName!, domainType!, codedValues, range, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-092 P3：更新域（W）。</summary>
public sealed class UpdateDomainTool : McpToolBase
{
    public override string Name => "update_domain";
    public override string Description => "更新既有域的编码值清单或范围；至少提供 codedValues 或 range 之一。";
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D092Schemas.UpdateDomain;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D092Domain is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-092 domain service is unavailable.");

        var workspace = ToolArgs.GetString(context, "workspace");
        var domainName = ToolArgs.GetString(context, "domainName");
        if (string.IsNullOrWhiteSpace(workspace) || string.IsNullOrWhiteSpace(domainName))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "workspace and domainName are required.");

        var codedValues = ToolArgs.GetList(context, "codedValues");
        var range = ToolArgs.GetObject(context, "range");
        if (codedValues.Count == 0 && range is null)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "at least one of codedValues or range must be provided.");

        var result = await service.UpdateDomainAsync(workspace!, domainName!, codedValues.Count > 0 ? codedValues : null, range, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-092 P3：删除域（W）。</summary>
public sealed class DeleteDomainTool : McpToolBase
{
    public override string Name => "delete_domain";
    public override string Description => "删除 GDB 域；域仍被字段引用时缺省拒绝（INVALID_STATE），force=true 方可强制。";
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D092Schemas.DeleteDomain;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D092Domain is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-092 domain service is unavailable.");

        var workspace = ToolArgs.GetString(context, "workspace");
        var domainName = ToolArgs.GetString(context, "domainName");
        if (string.IsNullOrWhiteSpace(workspace) || string.IsNullOrWhiteSpace(domainName))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "workspace and domainName are required.");

        var force = ToolArgs.GetBool(context, "force", false);
        var result = await service.DeleteDomainAsync(workspace!, domainName!, force, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-092 P3：将域指派给字段（W）。</summary>
public sealed class AssignDomainToFieldTool : McpToolBase
{
    public override string Name => "assign_domain_to_field";
    public override string Description => "将域绑定到数据集字段；字段已有域且未显式 overwrite=true ⇒ OUTPUT_EXISTS。";
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D092Schemas.AssignDomainToField;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D092Domain is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-092 domain service is unavailable.");

        var workspace = ToolArgs.GetString(context, "workspace");
        var dataset = ToolArgs.GetString(context, "dataset");
        var field = ToolArgs.GetString(context, "field");
        var domainName = ToolArgs.GetString(context, "domainName");
        if (string.IsNullOrWhiteSpace(workspace) || string.IsNullOrWhiteSpace(dataset)
            || string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(domainName))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "workspace, dataset, field and domainName are all required.");

        var overwrite = ToolArgs.GetBool(context, "overwrite", false);
        var result = await service.AssignDomainToFieldAsync(workspace!, dataset!, field!, domainName!, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-092 P3：从字段移除域（W）。</summary>
public sealed class RemoveDomainFromFieldTool : McpToolBase
{
    public override string Name => "remove_domain_from_field";
    public override string Description => "解除字段与域的绑定关系。";
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D092Schemas.RemoveDomainFromField;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D092Domain is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-092 domain service is unavailable.");

        var workspace = ToolArgs.GetString(context, "workspace");
        var dataset = ToolArgs.GetString(context, "dataset");
        var field = ToolArgs.GetString(context, "field");
        if (string.IsNullOrWhiteSpace(workspace) || string.IsNullOrWhiteSpace(dataset) || string.IsNullOrWhiteSpace(field))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "workspace, dataset and field are all required.");

        var result = await service.RemoveDomainFromFieldAsync(workspace!, dataset!, field!, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-092 P4：配置子类型（W）。</summary>
public sealed class ConfigureSubtypesTool : McpToolBase
{
    public override string Name => "configure_subtypes";
    public override string Description => "为要素类配置子类型字段与子类型清单；clearExisting=true 先清空既有子类型。";
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D092Schemas.ConfigureSubtypes;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D092Domain is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-092 domain service is unavailable.");

        var dataset = ToolArgs.GetString(context, "dataset");
        var subtypeField = ToolArgs.GetString(context, "subtypeField");
        if (string.IsNullOrWhiteSpace(dataset) || string.IsNullOrWhiteSpace(subtypeField))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "dataset and subtypeField are required.");

        var subtypes = ToolArgs.GetList(context, "subtypes");
        if (subtypes.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "subtypes must be a non-empty array.");

        var clearExisting = ToolArgs.GetBool(context, "clearExisting", false);
        var result = await service.ConfigureSubtypesAsync(dataset!, subtypeField!, subtypes, clearExisting, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-092 P4：创建关系类（W）。</summary>
public sealed class CreateRelationshipClassTool : McpToolBase
{
    public override string Name => "create_relationship_class";
    public override string Description => "在两个表/要素类之间创建库级关系类（非图层连接；与 add_join/remove_join 划界）。";
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D092Schemas.CreateRelationshipClass;

    private static readonly HashSet<string> Cardinalities = new(StringComparer.Ordinal)
    { "one_to_one", "one_to_many", "many_to_many" };

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D092Domain is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-092 domain service is unavailable.");

        var origin = ToolArgs.GetString(context, "originTable");
        var dest = ToolArgs.GetString(context, "destinationTable");
        var relName = ToolArgs.GetString(context, "relationshipName");
        var cardinality = ToolArgs.GetString(context, "cardinality");
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(dest) || string.IsNullOrWhiteSpace(relName))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "originTable, destinationTable and relationshipName are required.");
        if (cardinality is null || !Cardinalities.Contains(cardinality))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "cardinality must be one_to_one, one_to_many or many_to_many.");

        var forwardLabel = ToolArgs.GetString(context, "forwardLabel");
        var backwardLabel = ToolArgs.GetString(context, "backwardLabel");
        var attributed = ToolArgs.GetBool(context, "attributed", false);

        var result = await service.CreateRelationshipClassAsync(origin!, dest!, relName!, cardinality!, forwardLabel, backwardLabel, attributed, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-092 P4：计算几何属性（W；Native 执行）。</summary>
public sealed class CalculateGeometryAttributesTool : McpToolBase
{
    public override string Name => "calculate_geometry_attributes";
    public override string Description => "为要素类计算几何量测（length/area/perimeter/x/y/z 等）并写回字段；不得静默换算单位，须回显 resolvedUnits。";
    protected override string CategoryName => ToolCategories.DataManagement;
    public override IReadOnlyDictionary<string, object?> InputSchema => D092Schemas.CalculateGeometryAttributes;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D092Domain is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-092 domain service is unavailable.");

        var dataset = ToolArgs.GetString(context, "dataset");
        var units = ToolArgs.GetString(context, "units");
        if (string.IsNullOrWhiteSpace(dataset) || string.IsNullOrWhiteSpace(units))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "dataset and units are required.");

        var fields = ToolArgs.GetList(context, "fields");
        if (fields.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "fields must be a non-empty array of field/measurement mappings.");

        var overwrite = ToolArgs.GetBool(context, "overwrite", false);
        var result = await service.CalculateGeometryAttributesAsync(dataset!, fields, units!, overwrite, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-092 P4：定义投影（W）。</summary>
public sealed class DefineProjectionTool : McpToolBase
{
    public override string Name => "define_projection";
    public override string Description => "为数据集定义 CRS（定义 ≠ 变换；与 project 工具划界）。confirmNoTransform 必须显式为 true。";
    protected override string CategoryName => ToolCategories.DataManagement;
    protected override string ExecutionTypeName => ExecutionTypes.Geoprocessing;
    public override IReadOnlyDictionary<string, object?> InputSchema => D092Schemas.DefineProjection;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, ToolWriteClassification.ReadOnlyRefusal(Name).Message);
        if (context.Host?.D092Domain is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-092 domain service is unavailable.");

        var dataset = ToolArgs.GetString(context, "dataset");
        var crs = ToolArgs.GetString(context, "crs");
        if (string.IsNullOrWhiteSpace(dataset) || string.IsNullOrWhiteSpace(crs))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "dataset and crs are required.");

        var confirmRaw = ToolArgs.GetValue(context, "confirmNoTransform");
        if (confirmRaw is not true)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "confirmNoTransform must be explicitly true; this tool defines a CRS without coordinate transformation.");

        var result = await service.DefineProjectionAsync(dataset!, crs!, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

/// <summary>D-092 P4：校验关系类（R）。</summary>
public sealed class ValidateRelationshipClassTool : McpToolBase
{
    public override string Name => "validate_relationship_class";
    public override string Description => "校验关系类完整性：源/目标表存在性、基数一致性、孤立记录检测。";
    protected override string CategoryName => ToolCategories.DataManagement;
    public override IReadOnlyDictionary<string, object?> InputSchema => D092Schemas.ValidateRelationshipClass;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D092Domain is not { } service)
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "D-092 domain service is unavailable.");

        var relName = ToolArgs.GetString(context, "relationshipName");
        if (string.IsNullOrWhiteSpace(relName))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "relationshipName is required.");

        var workspace = ToolArgs.GetString(context, "workspace");
        var maxItems = ToolArgs.GetInt(context, "maxItems") ?? 200;
        if (maxItems is < 1 or > 1000)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxItems must be between 1 and 1000.");

        var result = await service.ValidateRelationshipClassAsync(relName!, workspace, maxItems, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(result);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// D092Schemas：冻结契约在 D-082 f03b-5-schemas 中；此处只将其映射为 MCP InputSchema。
// ═══════════════════════════════════════════════════════════════════════════════

internal static class D092Schemas
{
    private static Dictionary<string, object?> S(string type, string? description = null, object? defaultValue = null,
        object? minimum = null, object? maximum = null, string[]? values = null)
    {
        var d = new Dictionary<string, object?> { ["type"] = type };
        if (description is not null) d["description"] = description;
        if (defaultValue is not null) d["default"] = defaultValue;
        if (minimum is not null) d["minimum"] = minimum;
        if (maximum is not null) d["maximum"] = maximum;
        if (values is not null) d["enum"] = values;
        return d;
    }

    private static IReadOnlyDictionary<string, object?> Obj(Dictionary<string, object?> props, params string[] required)
        => new Dictionary<string, object?>
        {
            ["type"] = "object", ["properties"] = props, ["required"] = required,
            ["additionalProperties"] = false,
        };

    private static Dictionary<string, object?> P(params (string Name, object? Schema)[] fields)
        => fields.ToDictionary(x => x.Name, x => x.Schema, StringComparer.Ordinal);

    // ── P3 域治理 ──

    internal static readonly IReadOnlyDictionary<string, object?> CreateDomain = Obj(P(
        ("workspace", S("string", "GDB 工作空间路径。")),
        ("domainName", S("string", "域名。")),
        ("domainType", S("string", "域类型。", values: new[] { "CodedValue", "Range" })),
        ("codedValues", new Dictionary<string, object?> { ["type"] = "array", ["default"] = Array.Empty<object>(), ["description"] = "编码值清单（domainType=CodedValue 时必填）。" }),
        ("range", S("object", "范围（domainType=Range 时必填）。"))),
        "workspace", "domainName", "domainType");

    internal static readonly IReadOnlyDictionary<string, object?> UpdateDomain = Obj(P(
        ("workspace", S("string", "GDB 工作空间路径。")),
        ("domainName", S("string", "域名。")),
        ("codedValues", new Dictionary<string, object?> { ["type"] = "array", ["description"] = "替换后的编码值清单。" }),
        ("range", S("object", "替换后的范围。"))),
        "workspace", "domainName");

    internal static readonly IReadOnlyDictionary<string, object?> DeleteDomain = Obj(P(
        ("workspace", S("string", "GDB 工作空间路径。")),
        ("domainName", S("string", "域名。")),
        ("force", S("boolean", "域仍被字段引用时是否强制（缺省 false ⇒ INVALID_STATE，不静默破坏引用）。", false))),
        "workspace", "domainName");

    internal static readonly IReadOnlyDictionary<string, object?> AssignDomainToField = Obj(P(
        ("workspace", S("string", "GDB 工作空间路径。")),
        ("dataset", S("string", "要素类/表名（工作空间内）。")),
        ("field", S("string", "目标字段名。")),
        ("domainName", S("string", "域名。")),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "workspace", "dataset", "field", "domainName");

    internal static readonly IReadOnlyDictionary<string, object?> RemoveDomainFromField = Obj(P(
        ("workspace", S("string", "GDB 工作空间路径。")),
        ("dataset", S("string", "要素类/表名。")),
        ("field", S("string", "目标字段名。"))),
        "workspace", "dataset", "field");

    // ── P4 ──

    internal static readonly IReadOnlyDictionary<string, object?> ConfigureSubtypes = Obj(P(
        ("dataset", S("string", "要素类路径。")),
        ("subtypeField", S("string", "子类型字段名。")),
        ("subtypes", new Dictionary<string, object?> { ["type"] = "array", ["description"] = "子类型清单（code/name/default）。" }),
        ("clearExisting", S("boolean", "是否先清空既有子类型。", false))),
        "dataset", "subtypeField", "subtypes");

    internal static readonly IReadOnlyDictionary<string, object?> CreateRelationshipClass = Obj(P(
        ("originTable", S("string", "源表/要素类。")),
        ("destinationTable", S("string", "目标表/要素类。")),
        ("relationshipName", S("string", "关系类名。")),
        ("cardinality", S("string", "基数。", values: new[] { "one_to_one", "one_to_many", "many_to_many" })),
        ("forwardLabel", S("string", "正向标签。")),
        ("backwardLabel", S("string", "反向标签。")),
        ("attributed", S("boolean", "是否为属性关系类。", false))),
        "originTable", "destinationTable", "relationshipName", "cardinality");

    internal static readonly IReadOnlyDictionary<string, object?> CalculateGeometryAttributes = Obj(P(
        ("dataset", S("string", "要素类路径（计数写回原数据 ⇒ 须显式 overwrite/写授权）。")),
        ("fields", new Dictionary<string, object?> { ["type"] = "array", ["description"] = "目标字段与量测类型映射（length/area/perimeter/x/y/z/minx…）。" }),
        ("units", S("string", "量测单位（不得静默换算；须回显 resolvedUnits）。")),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))),
        "dataset", "fields", "units");

    internal static readonly IReadOnlyDictionary<string, object?> DefineProjection = Obj(P(
        ("dataset", S("string", "数据集路径。")),
        ("crs", S("string", "要定义的 CRS（WKID/WKT）。")),
        ("confirmNoTransform", S("boolean", "必须显式为 true：本工具不做坐标变换（定义 ≠ 变换）。缺失 ⇒ INVALID_ARGUMENT。"))),
        "dataset", "crs", "confirmNoTransform");

    internal static readonly IReadOnlyDictionary<string, object?> ValidateRelationshipClass = Obj(P(
        ("relationshipName", S("string", "关系类名。")),
        ("workspace", S("string", "工作空间路径（省略 ⇒ 当前工程默认 GDB）。")),
        ("maxItems", S("integer", "返回条目上限（缺省 200，上限 1000）。超限 ⇒ 截断并在响应中如实披露 truncated/returnedCount/totalMatched。", 200, 1, 1000))),
        "relationshipName");
}
