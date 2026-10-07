namespace ArcGISProMCP.Core.Services;

/// <summary>
/// select 类工具的 where 子句构造（D-026 F9，纯逻辑无 SDK 依赖——Rule 5）。
/// 与历史 GP 路径行为对齐：oidList 优先（空列表 → 匹配空集 <c>1=0</c>）；
/// 否则透传 whereClause（空白 → 匹配全集，等价 GP replace 无 where）。
/// </summary>
/// <remarks>
/// D-024/D-026：IN 子句使用**图层真实 OID 字段名**（由调用方经表定义取得后传入），
/// 替代历史硬编码 <c>OBJECTID</c>——等价或更优（非 OBJECTID 命名数据源不再必然失败）。
/// </remarks>
public static class SelectWhereClause
{
    public static string Build(IReadOnlyList<long>? oidList, string? where, string oidFieldName)
    {
        if (oidList is not null)
        {
            return oidList.Count == 0 ? "1=0" : $"{oidFieldName} IN ({string.Join(",", oidList)})";
        }

        return where ?? string.Empty;
    }
}
