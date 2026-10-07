using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Services;

/// <summary>工程服务。</summary>
public interface IProjectService
{
    Task<OperationResult<ProjectInfo>> GetProjectInfoAsync(CancellationToken ct = default);

    Task<OperationResult<IReadOnlyList<LayoutInfo>>> ListLayoutsAsync(CancellationToken ct = default);

    Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListDatabasesAsync(CancellationToken ct = default);

    /// <summary>
    /// D-061：保存当前工程。<paramref name="saveAsPath"/> 为空 ⇒ 原位保存；非空 ⇒ 另存到该路径（须为 <c>.aprx</c>）。
    /// 返回 <see cref="ProjectSaveInfo"/>（保存后实测：存在性 / 字节数 / 写入时间 / 脏标记）。
    /// </summary>
    /// <remarks>
    /// 以**默认接口实现**提供，既有宿主实现（含测试替身）无需改动即可编译；未覆写时返回 NOT_IMPLEMENTED。
    /// </remarks>
    Task<OperationResult<ProjectSaveInfo>> SaveProjectAsync(string? saveAsPath, CancellationToken ct = default)
        => Task.FromResult(OperationResult<ProjectSaveInfo>.Fail(
            ErrorCodes.NotImplemented, "Project save is not implemented by this host."));

    // ══════════════════════════ D-064 · B/C 段：环境与数据发现 ══════════════════════════

    /// <summary>
    /// D-064 · C：在工程内注册**文件夹连接**（路径规范化后注册）。重复注册 ⇒ Added=false + AlreadyPresent=true。
    /// </summary>
    /// <remarks>默认实现返回 NOT_IMPLEMENTED（保既有宿主实现零改动编译）。</remarks>
    Task<OperationResult<AddFolderConnectionResult>> AddFolderConnectionAsync(string path, CancellationToken ct = default)
        => Task.FromResult(OperationResult<AddFolderConnectionResult>.Fail(
            ErrorCodes.NotImplemented, "Folder connection registration is not implemented by this host."));

    /// <summary>D-064 · C：工程已注册连接 / 工具箱清单（只读）。</summary>
    /// <remarks>默认实现返回 NOT_IMPLEMENTED（保既有宿主实现零改动编译）。</remarks>
    Task<OperationResult<ProjectItemsResult>> GetProjectItemsAsync(CancellationToken ct = default)
        => Task.FromResult(OperationResult<ProjectItemsResult>.Fail(
            ErrorCodes.NotImplemented, "Project item listing is not implemented by this host."));

    /// <summary>
    /// D-064 · C：按名在工程 home / 默认 GDB / 文件夹连接中检索数据集（只读；<paramref name="topN"/> 上限 + 截断披露）。
    /// </summary>
    /// <remarks>默认实现返回 NOT_IMPLEMENTED（保既有宿主实现零改动编译）。</remarks>
    Task<OperationResult<SearchDataResult>> SearchDataAsync(
        string? pattern, string? typeFilter, int topN, CancellationToken ct = default)
        => Task.FromResult(OperationResult<SearchDataResult>.Fail(
            ErrorCodes.NotImplemented, "Data search is not implemented by this host."));
}
