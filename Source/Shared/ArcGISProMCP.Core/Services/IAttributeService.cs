using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>属性服务。</summary>
public interface IAttributeService
{
    Task<OperationResult<IReadOnlyList<FeatureInfo>>> QueryFeaturesAsync(AttributeQueryRequest request, CancellationToken ct = default);

    Task<OperationResult<IReadOnlyList<FieldInfo>>> GetFieldInfoAsync(string mapName, string layerName, CancellationToken ct = default);

    Task<OperationResult<long>> GetFeatureCountAsync(string mapName, string layerName, string? whereClause = null, CancellationToken ct = default);

    /// <summary>Phase 9 第四批（D-038）：字段值域画像（只读）。maxDistinct 默认 200、上限 2000（超出截断）。</summary>
    Task<OperationResult<FieldValuesInfo>> GetFieldValuesAsync(
        string mapName, string layerName, string fieldName, int maxDistinct = 200, CancellationToken ct = default);

    // ── D-062 · C 段统计聚合（只读；新成员一律默认实现）──

    /// <summary>
    /// D-062 · C1：数值字段统计（min/max/mean/median/sum/stddev/count）。空值/非数值跳过并披露计数；
    /// 样本标准差（n-1，n&lt;2 → null）；扫描上限由实现披露。不落 GP。
    /// </summary>
    Task<OperationResult<FieldStatisticsInfo>> GetFieldStatisticsAsync(
        string? mapName, string layerName, string fieldName, string? whereClause = null, CancellationToken ct = default)
        => Task.FromResult(OperationResult<FieldStatisticsInfo>.Fail(
            ErrorCodes.NotImplemented, "GetFieldStatisticsAsync is not implemented by this host."));

    /// <summary>
    /// D-062 · C2：分组聚合（group-by 一/多字段 + count/sum/min/max/mean/first/last + topN）。
    /// 回答「每类多少」不落 GP；组数上限由实现披露。
    /// </summary>
    Task<OperationResult<GroupSummaryInfo>> SummarizeFeaturesAsync(
        string? mapName, string layerName,
        IReadOnlyList<string> groupByFields, string? aggField, IReadOnlyList<string> aggregations,
        string? whereClause = null, int topN = 0, CancellationToken ct = default)
        => Task.FromResult(OperationResult<GroupSummaryInfo>.Fail(
            ErrorCodes.NotImplemented, "SummarizeFeaturesAsync is not implemented by this host."));
}
