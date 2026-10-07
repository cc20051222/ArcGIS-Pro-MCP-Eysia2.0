namespace ArcGISProMCP.Core.Models;

/// <summary>地图（纯数据，不暴露 ArcGIS SDK 类型）。</summary>
/// <remarks>
/// Phase 8.1 扩展：新增 <see cref="Id"/>（稳定标识，来源 <c>MapProjectItem.Path</c>）
/// 与 <see cref="IsActive"/>。既有字段名与含义保持不变，客户端按字段新增兼容。
/// Shared 层不得引用 ArcGIS Pro SDK（Rule 5）：<see cref="Kind"/> / <see cref="MapType"/>
/// 一律以字符串承载，由宿主层完成 SDK 枚举到字符串的映射。
/// </remarks>
public sealed class MapInfo
{
    public string Name { get; set; } = string.Empty;

    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// 地图种类：<c>Map</c>（2D）/ <c>Scene</c>（3D）/ <c>LinkChart</c>（Pro 3.3+）/ <c>Unknown</c>。
    /// 禁止硬编码为 <c>Map</c>（缺陷 C-12）。
    /// </summary>
    public string Kind { get; set; } = "Map";

    /// <summary>SDK 原始 <c>MapType</c> 文本；与 <see cref="Kind"/> 同源，保留便于排障。</summary>
    public string MapType { get; set; } = string.Empty;

    /// <summary>稳定标识（<c>MapProjectItem.Path</c>）。重名地图据此唯一定位；解析不到时为空。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>是否为当前活动地图视图。</summary>
    public bool IsActive { get; set; }
}
