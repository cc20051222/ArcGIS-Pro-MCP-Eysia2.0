using System;
using System.Collections.Generic;
using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// F10（D-028）选择快照恢复的可解析性判定（纯逻辑，不依赖 ArcGIS SDK，便于单测覆盖）。
/// 背景：恢复曾以**非展平**的 <c>map.Layers</c> 建 URI 索引，组内子层未命中即被静默 continue，
/// 导致"能选（LayerResolver flatten）/能拍快照（ReadContent）/不能恢复（restoredCount=0 且无错误码）"。
/// 本类只回答"快照里的图层在当前地图是否都可解析"；索引本身由调用方用展平集合构建。
/// </summary>
public static class SelectionRestoreResolver
{
    /// <summary>
    /// 返回快照中在当前地图**不可解析**的图层显示名（空 = 全部可解析，可安全恢复）。
    /// 调用方须以**展平**的图层/成员 URI 集合作为 <paramref name="availableUris"/>（含组内子层）。
    /// </summary>
    public static IReadOnlyList<string> UnresolvableLayerNames(
        IReadOnlyCollection<string>? availableUris,
        IEnumerable<SelectionLayerContent>? snapshotLayers)
    {
        var unresolvable = new List<string>();
        if (snapshotLayers is null)
        {
            return unresolvable;
        }

        var known = new HashSet<string>(availableUris ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (var layer in snapshotLayers)
        {
            var uri = layer?.LayerUri ?? string.Empty;
            if (known.Contains(uri))
            {
                continue;
            }

            var name = layer?.LayerName;
            unresolvable.Add(string.IsNullOrWhiteSpace(name) ? uri : name!);
        }

        return unresolvable;
    }
}
