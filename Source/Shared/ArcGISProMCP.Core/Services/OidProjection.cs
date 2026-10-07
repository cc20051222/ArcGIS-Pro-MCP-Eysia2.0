namespace ArcGISProMCP.Core.Services;

/// <summary>
/// OID 投影构造结果（D-022 / F2）。纯数据，便于单测断言。
/// </summary>
/// <param name="SubFields">最终投递给 <c>QueryFilter.SubFields</c> 的字段列表（已按需前置 OID）。</param>
/// <param name="OidField">该表的实际 OID 字段名；数据源无 OID 时为 <c>null</c>。</param>
/// <param name="OidAdded">本次是否把 OID 补进了投影（原本请求未含）。</param>
/// <param name="OidAvailable">该表是否存在 OID 字段（false → 调用方必须显式披露 <c>oid:-1</c>）。</param>
public sealed record OidProjectionResult(
    IReadOnlyList<string> SubFields,
    string? OidField,
    bool OidAdded,
    bool OidAvailable);

/// <summary>
/// F2（D-022）：<c>query_attributes</c> 的字段投影构造——**OID 必须始终在投影内**。
/// 纯逻辑，无 SDK 依赖（Rule 5）。
/// </summary>
/// <remarks>
/// 背景：历史实现为 <c>qf.SubFields = string.Join(",", request.FieldNames)</c>，
/// 当客户端 <c>fieldNames</c> 未含 OID 时，游标取不到 OID →
/// <c>row.GetObjectID()</c> 退化为 <c>-1</c> 且属性里 <c>OBJECTID</c> 为 <c>null</c>（D-019 F2）。
/// Keeper 裁定：**始终携带 OID**；仅当数据源客观无 OID 时才允许 <c>-1</c>，且必须显式披露。
///
/// 语义（不得回退）：
/// ① 不新增任何**业务字段**——只补 OID 本身，客户端未请求的业务字段一律不返回；
/// ② 客户端已显式包含 OID（大小写不敏感）时不重复追加；
/// ③ 空白/空条目被过滤，不进入投影。
/// </remarks>
public static class OidProjection
{
    /// <summary>构造含 OID 的字段投影。</summary>
    /// <param name="requestedFields">客户端请求的字段名（可空 / 可含空白条目）。</param>
    /// <param name="oidField">该表的实际 OID 字段名（可空 = 数据源无 OID）。</param>
    public static OidProjectionResult Build(IReadOnlyList<string>? requestedFields, string? oidField)
    {
        var names = new List<string>();
        if (requestedFields is not null)
        {
            foreach (var field in requestedFields)
            {
                if (!string.IsNullOrWhiteSpace(field))
                {
                    names.Add(field);
                }
            }
        }

        var oidAvailable = !string.IsNullOrWhiteSpace(oidField);
        if (!oidAvailable)
        {
            // 数据源无 OID：投影保持客户端原样，由调用方在响应中显式披露 oid 不可得。
            return new OidProjectionResult(names, null, false, false);
        }

        var alreadyRequested = names.Any(
            n => string.Equals(n, oidField, StringComparison.OrdinalIgnoreCase));
        if (alreadyRequested)
        {
            return new OidProjectionResult(names, oidField, false, true);
        }

        names.Insert(0, oidField!);
        return new OidProjectionResult(names, oidField, true, true);
    }

    /// <summary>
    /// OID 客观不可得时的披露文案（D-022 F2：不得静默降级为 <c>-1</c>）。
    /// </summary>
    public static string UnavailableMessage()
        => "The data source has no ObjectID field; feature 'oid' is reported as -1 and no OBJECTID value is available.";
}
