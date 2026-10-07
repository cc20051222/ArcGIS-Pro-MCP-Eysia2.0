using ArcGIS.Core.Geometry;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-062 · B 段：WKT → SDK 几何解析（仅 Compatibility 可触 SDK —— Rule 5）。
/// 走 <c>GeometryEngine.Instance.ImportFromWkt</c>（统一入口，点/线/面/多几何皆可）；
/// 解析失败 → 失败信息（调用方转 INVALID_ARGUMENT）。
/// </summary>
public static class GeometryWkt
{
    public static (bool Success, Geometry? Geometry, string? Error) Parse(string wkt, SpatialReference? sr)
    {
        if (string.IsNullOrWhiteSpace(wkt))
        {
            return (false, null, "WKT is empty.");
        }

        try
        {
            var g = GeometryEngine.Instance.ImportFromWKT(WktImportFlags.WktImportDefaults, wkt, sr);
            return g is null
                ? (false, null, "WKT parse returned null geometry.")
                : (true, g, null);
        }
        catch (Exception ex)
        {
            return (false, null, "WKT parse failed: " + ex.Message);
        }
    }
}
