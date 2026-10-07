namespace ArcGISProMCP.Core.Models;

/// <summary>
/// 坐标系解析结果（D-037 F-D035-2）。
/// <para><see cref="Wkid"/> &gt; 0 ⇒ 解析成功，该 WKID 为可下传 GP 的权威值；
/// <see cref="Wkid"/> = 0（<see cref="Source"/> = <c>unresolved-name</c>）⇒ 未能解析
/// （宿主侧无此预置坐标系），调用方须**如实报错**，不得静默回落为任意坐标系。</para>
/// </summary>
public sealed record SpatialReferenceResolution(int Wkid, string? Name, string Source);
