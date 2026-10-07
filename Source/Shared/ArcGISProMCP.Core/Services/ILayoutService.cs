using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>布局服务。</summary>
public interface ILayoutService
{
    Task<OperationResult<IReadOnlyList<LayoutInfo>>> GetLayoutsAsync(CancellationToken ct = default);

    /// <summary>D-084 Session：更新已有布局元素的白名单属性。</summary>
    Task<OperationResult<object?>> SetElementPropertiesAsync(
        string layoutName, string elementId, IReadOnlyDictionary<string, object?> properties, string units, CancellationToken ct = default)
        => Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "Layout element properties are not implemented by this host."));

    /// <summary>D-084 Session：配置现有 spatial map series，不产生文件。</summary>
    Task<OperationResult<object?>> ConfigureMapSeriesAsync(
        string mapName, string indexField, string? sortField, string extentSource, string? nameField, CancellationToken ct = default)
        => Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "Map series configuration is not implemented by this host."));

    /// <summary>D-042：单个布局详情（页面尺寸/单位/元素计数）。同名多布局 → 多布局歧义语义由实现判（列全或歧义码，报告披露）。</summary>
    Task<OperationResult<LayoutDetailInfo>> GetLayoutInfoAsync(string layoutName, CancellationToken ct = default);

    /// <summary>内部实现用：读取布局所有 MapFrame 当前地图中的 raster 数据源，供导出前资格复核。</summary>
    Task<OperationResult<IReadOnlyList<string>>> GetRasterSourcePathsAsync(
        string layoutName,
        CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<string>>.Ok(Array.Empty<string>()));

    /// <summary>D-042：布局元素清单（展平；Element.Name/IsVisible/GetX/GetY 派生自 SDK）。同名多布局 → 同上。</summary>
    Task<OperationResult<IReadOnlyList<LayoutElementInfo>>> ListLayoutElementsAsync(string layoutName, CancellationToken ct = default);

    /// <summary>
    /// D-045：**新建自有布局**（写操作；plan §7「默认新建自有布局，不改用户原布局」）。
    /// 同名已存在 → <c>INVALID_ARGUMENT</c>（拒绝创建，不静默改名）；给了 <paramref name="mapName"/> 则
    /// 同时创建并绑定地图框（<paramref name="mapFrameName"/> 可缺省）；mapName 不存在 → <c>MAP_NOT_FOUND</c>。
    /// <paramref name="pageUnits"/> ∈ Inches/Centimeters/Millimeters/Points（大小写不敏感）。
    /// **DPI 不在参数面**（SDK 无可达成员，见 sdk-spike.md）。
    /// </summary>
    Task<OperationResult<LayoutCreateInfo>> CreateLayoutAsync(
        string name,
        double pageWidth,
        double pageHeight,
        string pageUnits,
        string? mapName,
        string? mapFrameName,
        double? frameXMin,
        double? frameYMin,
        double? frameXMax,
        double? frameYMax,
        CancellationToken ct = default);

    /// <summary>D-045：向既有布局**就地**添加文本元素（页面坐标 = 页面单位）。</summary>
    Task<OperationResult<LayoutElementAddInfo>> AddLayoutTextAsync(
        string layoutName,
        string text,
        double x,
        double y,
        double? fontSize,
        string? fontFamily,
        string? elementName,
        CancellationToken ct = default);

    /// <summary>D-045：向既有布局**就地**添加图例（锚定地图框）。</summary>
    Task<OperationResult<LayoutElementAddInfo>> AddLegendAsync(
        string layoutName, string mapFrameName, double x, double y,
        double? width, double? height, string? elementName, CancellationToken ct = default);

    /// <summary>D-045：向既有布局**就地**添加指北针（锚定地图框；默认样式，样式项自定义留待后续批次）。</summary>
    Task<OperationResult<LayoutElementAddInfo>> AddNorthArrowAsync(
        string layoutName, string mapFrameName, double x, double y,
        double? width, double? height, string? elementName, CancellationToken ct = default);

    /// <summary>D-045：向既有布局**就地**添加比例尺（锚定地图框；<c>units</c> 不在参数面 —— SDK 无可达单位面）。</summary>
    Task<OperationResult<LayoutElementAddInfo>> AddScaleBarAsync(
        string layoutName, string mapFrameName, double x, double y,
        double? width, double? height, string? elementName, CancellationToken ct = default);

    /// <summary>
    /// D-047：**导出布局到文件**（写操作 = 新建文件；可复原 = 删除产物）。
    /// <paramref name="format"/> ∈ PDF / PNG（大小写不敏感）；<paramref name="resolution"/> 为 DPI（&gt;0；缺省用 SDK 默认 96）。
    /// **覆盖策略**：目标文件已存在且 <paramref name="overwrite"/>=false → <c>OUTPUT_EXISTS</c>（不写、不覆盖）；
    /// 显式 overwrite=true 才覆盖。扩展名不匹配（pdf↔.pdf / png↔.png）→ <c>INVALID_ARGUMENT</c>。
    /// 布局不存在 → <c>LAYER_NOT_FOUND</c>（消息明示 Layout）。返回产物事实（字节数 + 文件头）。
    /// **范围披露**：width/height **不在参数面**（SDK 文档：仅适用地图视图导出，布局导出时被忽略）；
    /// 地图级导出不在本批（依赖活动 MapView，工程态不可达）。
    /// </summary>
    Task<OperationResult<LayoutExportInfo>> ExportLayoutAsync(
        string layoutName,
        string outputPath,
        string format,
        double? resolution,
        bool overwrite,
        CancellationToken ct = default);

    /// <summary>D-086：导出布局素材时可显式控制 PNG 透明背景；非 PNG 格式传 null。</summary>
    Task<OperationResult<LayoutExportInfo>> ExportLayoutWithOptionsAsync(
        string layoutName,
        string outputPath,
        string format,
        double? resolution,
        bool overwrite,
        bool? transparentBackground,
        CancellationToken ct = default)
        => transparentBackground is null
            ? ExportLayoutAsync(layoutName, outputPath, format, resolution, overwrite, ct)
            : Task.FromResult(OperationResult<LayoutExportInfo>.Fail(
                ErrorCodes.NotImplemented,
                "This host does not expose the requested PNG transparency option."));

    // ── D-064 · D 段：布局 map series（系列）多页导出 ──

    /// <summary>
    /// D-064：把布局的 **map series**（系列）导出为多页 PDF。
    /// 契约（工具层强制）：输出路径守卫 + 覆写闸门；<paramref name="maxPages"/> 为**页数上限**
    /// （超出 ⇒ 明确拒绝 `<c>INVALID_ARGUMENT</c>`，**不静默截断文件**）；布局未启用 map series ⇒
    /// 明确拒绝（不静默退化为单页导出）。失败时须**清理半成品产物**（不留残缺文件）。
    /// </summary>
    /// <remarks>默认实现返回 NOT_IMPLEMENTED（保既有宿主实现零改动编译）。</remarks>
    Task<OperationResult<ExportMapSeriesResult>> ExportMapSeriesAsync(
        string layoutName, string outputPath, int maxPages, double? resolution, bool overwrite,
        CancellationToken ct = default)
        => Task.FromResult(OperationResult<ExportMapSeriesResult>.Fail(
            ErrorCodes.NotImplemented, "Map series export is not implemented by this host."));
}
