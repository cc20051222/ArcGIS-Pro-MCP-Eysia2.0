namespace ArcGISProMCP.Core.Models;

/// <summary>要素记录（纯数据）。</summary>
public sealed class FeatureInfo
{
    public long Oid { get; set; }

    public IReadOnlyDictionary<string, object?> Attributes { get; set; } = new Dictionary<string, object?>();
}
