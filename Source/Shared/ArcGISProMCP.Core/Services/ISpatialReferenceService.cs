using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// 坐标系解析服务（D-037 F-D035-2；宿主侧实现，本接口位于 Shared，不引用 ArcGIS Pro SDK）。
/// 契约：WKID 数值形态 → 原样通过（已 LIVE 实证可用）；名称形态 → 解析为 WKID 后下传；
/// 无法解析 → <see cref="SpatialReferenceResolution.Wkid"/>=0 且 <see cref="SpatialReferenceResolution.Source"/>
/// = <c>unresolved-name</c>，由调用方**如实报错**（不静默、不猜坐标系）。
/// </summary>
public interface ISpatialReferenceService
{
    Task<OperationResult<SpatialReferenceResolution>> ResolveAsync(string? value, CancellationToken ct = default);
}
