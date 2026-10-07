using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>数据管理服务。</summary>
public interface IDataManagementService
{
    Task<OperationResult<DatasetInfo>> GetDatasetInfoAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// D-061：枚举工作区内的**栅格数据集**（工作区 = GDB 容器 / 文件夹）。
    /// <paramref name="workspace"/> 为 null 或空白 ⇒ 由调用方（工具层）以工作区上下文补齐，宿主不擅自臆测路径。
    /// 返回 <see cref="DatasetInfo"/> 列表：名称 / 类型 / 空间参考 / 像元大小 / 波段数（不可判定项为 null）。
    /// </summary>
    /// <remarks>以**默认接口实现**提供，既有宿主实现（含测试替身）无需改动即可编译；未覆写时返回 NOT_IMPLEMENTED。</remarks>
    Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListRastersAsync(string? workspace, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<DatasetInfo>>.Fail(
            ErrorCodes.NotImplemented, "Raster listing is not implemented by this host."));

    /// <summary>
    /// D-061：枚举工作区内的**独立表**（不含要素类；GDB 内 <c>DatasetType.Table</c> / 文件夹工作区的表文件）。
    /// </summary>
    /// <remarks>以**默认接口实现**提供，既有宿主实现（含测试替身）无需改动即可编译；未覆写时返回 NOT_IMPLEMENTED。</remarks>
    Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListTablesAsync(string? workspace, CancellationToken ct = default)
        => Task.FromResult(OperationResult<IReadOnlyList<DatasetInfo>>.Fail(
            ErrorCodes.NotImplemented, "Table listing is not implemented by this host."));
}
