namespace ArcGISProMCP.Core.Models;

/// <summary>字段 schema 信息（纯数据）。</summary>
public sealed class SchemaFieldInfo
{
    public string Name { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
    public int Length { get; set; }
    public int? Precision { get; set; }
    public string? DomainName { get; set; }
}

/// <summary>数据集 schema 信息（get_schema_info 输出）。</summary>
public sealed class SchemaInfo
{
    public string Path { get; set; } = string.Empty;
    public bool Exists { get; set; } = true;
    public string? Reason { get; set; }
    public string? DataType { get; set; }
    public string? GeometryType { get; set; }
    public string? SpatialReference { get; set; }
    public IReadOnlyList<SchemaFieldInfo> Fields { get; set; } = Array.Empty<SchemaFieldInfo>();
    public bool Truncated { get; set; }
}

/// <summary>
/// D-079 · A2/A3：容器（文件地理数据库）内**直系子项**的只读摘要（名称/类型/路径/行数）。
/// 行数不可得时 <see cref="CountUnavailableReason"/> 必须给出原因（不裸 null）。
/// </summary>
public sealed class DatasetMemberInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    /// <summary>FeatureClass | Table | FeatureDataset | RasterDataset | Other。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>行数／要素数；不可得为 null（并给 <see cref="CountUnavailableReason"/>）。</summary>
    public long? RowCount { get; set; }

    public string? CountUnavailableReason { get; set; }
}

/// <summary>域信息（get_domains 输出）。</summary>
public sealed class DomainInfo{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // CodedValue / Range
    public IReadOnlyDictionary<string, string>? CodedValues { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
}

/// <summary>子类型信息（get_subtypes 输出）。</summary>
public sealed class SubtypeInfo
{
    public string Path { get; set; } = string.Empty;
    public bool Exists { get; set; } = true;
    public string? Reason { get; set; }
    public string? SubtypeField { get; set; }   // 空 = 无子类型字段（确实为空）
    public string? SubtypeFieldType { get; set; }
    public bool SubtypeFieldSupported { get; set; } = true; // false = 未支持/不可判定
    public IReadOnlyDictionary<string, string> Subtypes { get; set; } = new Dictionary<string, string>();
}

/// <summary>索引信息（get_indexes 输出）。</summary>
public sealed class IndexInfo
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<string> Fields { get; set; } = Array.Empty<string>();
    public bool IsUnique { get; set; }
    public bool IsSpatial { get; set; }
}
