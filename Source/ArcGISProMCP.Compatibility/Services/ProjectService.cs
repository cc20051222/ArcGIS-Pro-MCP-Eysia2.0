using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>工程服务实现。</summary>
public sealed partial class ProjectService : IProjectService
{
    public Task<OperationResult<ProjectInfo>> GetProjectInfoAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<ProjectInfo>>(
            () =>
            {
                var prj = Project.Current;
                // ★ D-064 阶段二真缺陷 ⑫：工程未打开时**如实拒绝**（旧实现直接解引用 ⇒ NRE ⇒
                //   整条工具链级联 INTERNAL_ERROR，掩盖真实原因"工程已关闭"）。
                if (prj is null)
                {
                    return OperationResult<ProjectInfo>.Fail(
                        ErrorCodes.InvalidState,
                        "no project is open in ArcGIS Pro; open a project (or restore_snapshot) before querying project facts.");
                }

                var path = prj.URI?.ToString() ?? string.Empty;
                var projectInfo = new ProjectInfo
                {
                    Path = path,
                    Name = System.IO.Path.GetFileNameWithoutExtension(path) ?? string.Empty,
                    IsDirty = prj.IsDirty,
                    DefaultGeodatabase = prj.DefaultGeodatabasePath ?? string.Empty,
                    HomeFolder = prj.HomeFolderPath ?? string.Empty
                };
                return OperationResult<ProjectInfo>.Ok(projectInfo);
            },
            TaskCreationOptions.None);

    public Task<OperationResult<IReadOnlyList<LayoutInfo>>> ListLayoutsAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<LayoutInfo>>>(
            () =>
            {
                var layouts = Project.Current.GetItems<LayoutProjectItem>()
                    .Select(item => new LayoutInfo
                    {
                        Name = item.Name ?? string.Empty,
                        Uri = item.Path ?? string.Empty,
                        IsVisible = true
                    })
                    .ToList();
                return OperationResult<IReadOnlyList<LayoutInfo>>.Ok(layouts);
            },
            TaskCreationOptions.None);

    public Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListDatabasesAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<DatasetInfo>>>(
            () =>
            {
                var dbs = Project.Current.GetItems<GDBProjectItem>()
                    .Select(item => new DatasetInfo
                    {
                        Name = item.Name ?? string.Empty,
                        Path = item.Path ?? string.Empty,
                        Type = IsFileGeodatabase(item.Path) ? "FileGeodatabase" : "Database"
                    })
                    .ToList();
                return OperationResult<IReadOnlyList<DatasetInfo>>.Ok(dbs);
            },
            TaskCreationOptions.None);

    /// <summary>
    /// D-061：判定地理数据库工程项是否为「文件地理数据库」。
    /// 说明：13.0 SDK 不含 <c>GDBProjectItem.IsGeodatabase</c>（该属性为 13.5 新增；以 13.0 引用集编译时
    /// 由编译期护栏捕获 CS1061）。此处改用工程项路径后缀做等价判定：
    /// <c>*.gdb</c> ⇒ 文件地理数据库；其余（企业库连接 / 移动库等）⇒ 数据库。
    /// </summary>
    // ------------------------------------------------------------------ D-061

    /// <summary>
    /// D-061：保存工程（<paramref name="saveAsPath"/> 为空 ⇒ 原位保存；非空 ⇒ 另存）。
    /// 返回值为**保存后实测**（文件存在性 / 字节数 / 写入时间 / 脏标记），不回填请求值。
    /// </summary>
    /// <remarks>
    /// 已知 SDK 行为差异（阶段一未 LIVE，待阶段二验证）：<c>Project.SaveAs</c> 成功后 <c>Project.Current</c>
    /// 指向另存副本；若目标不可写，SDK 可能返回 false 而不抛异常 —— 此处一律按**失败**处理并报 INTERNAL_ERROR，
    /// 避免"返回成功但文件未更新"的假成功。
    /// </remarks>
    public Task<OperationResult<ProjectSaveInfo>> SaveProjectAsync(string? saveAsPath, CancellationToken ct = default)
        // Rule 4 MCT：Project 操作一律在 QueuedTask 内进行（SDK 异步委托形态，D-061 编译期求证可用）。
        => QueuedTask.Run<OperationResult<ProjectSaveInfo>>(
            async () =>
            {
                var prj = Project.Current;
                if (prj is null)
                {
                    return OperationResult<ProjectSaveInfo>.Fail(
                        ErrorCodes.InvalidState, "No current project is available.");
                }

                if (string.IsNullOrEmpty(prj.URI?.ToString()))
                {
                    // 未保存过的工程没有 URI：原位保存无意义，必须先另存。
                    if (string.IsNullOrWhiteSpace(saveAsPath))
                    {
                        return OperationResult<ProjectSaveInfo>.Fail(
                            ErrorCodes.InvalidArgument,
                            "The current project has never been saved (no path); saveAsPath is required.");
                    }
                }

                string targetPath;
                bool savedAs;
                try
                {
                    if (string.IsNullOrWhiteSpace(saveAsPath))
                    {
                        // Project.URI 即文件系统路径（字符串形态，非 file:// Uri）。
                        targetPath = prj.URI!;
                        savedAs = false;
                    }
                    else
                    {
                        targetPath = saveAsPath!.Trim();
                        savedAs = true;
                    }

                    // ★ D-061 阶段二 LIVE 修复（真实缺陷，反证见报告 §）：
                    // Project.SaveAsync / SaveAsAsync 内部访问 WPF DispatcherObject；在 QueuedTask 背景线程
                    // 直接 await 会抛「调用线程无法访问此对象，因为另一个线程拥有该对象」(INTERNAL_ERROR)。
                    // 修复：**保存动作派发到 UI 线程**（本就在 UI 线程则直接执行）；其余 SDK 只读仍走 QueuedTask（Rule 4）。
                    async Task<bool> SaveCore()
                        => string.IsNullOrWhiteSpace(saveAsPath)
                            ? await prj.SaveAsync()
                            : await prj.SaveAsAsync(targetPath);

                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    var ok = (dispatcher is null || dispatcher.CheckAccess())
                        ? await SaveCore().ConfigureAwait(false)
                        : await dispatcher.InvokeAsync(SaveCore).Task.Unwrap().ConfigureAwait(false);

                    if (!ok)
                    {
                        return OperationResult<ProjectSaveInfo>.Fail(
                            ErrorCodes.InternalError,
                            savedAs
                                ? $"Project.SaveAsAsync('{targetPath}') reported failure; the file was not written."
                                : "Project.SaveAsync() reported failure; no verified file state is available.");
                    }
                }
                catch (Exception ex)
                {
                    return OperationResult<ProjectSaveInfo>.Fail(
                        ErrorCodes.InternalError, "Saving the project failed: " + ex.Message);
                }

                // 保存后实测（N1：产物事实必须读回，不臆测）。
                var fi = new System.IO.FileInfo(targetPath);
                var info = new ProjectSaveInfo
                {
                    Path = targetPath,
                    Name = System.IO.Path.GetFileNameWithoutExtension(targetPath) ?? string.Empty,
                    SavedAs = savedAs,
                    IsDirty = prj.IsDirty,
                    FileExists = fi.Exists,
                    FileSizeBytes = fi.Exists ? fi.Length : 0,
                    LastWriteTimeUtc = fi.Exists
                        ? fi.LastWriteTimeUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
                        : null
                };

                if (!fi.Exists)
                {
                    return OperationResult<ProjectSaveInfo>.Fail(
                        ErrorCodes.InternalError,
                        $"Saving reported success but the project file does not exist: {targetPath}");
                }

                return OperationResult<ProjectSaveInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    private static bool IsFileGeodatabase(string? path)
        => !string.IsNullOrEmpty(path)
           && path.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase);
}
