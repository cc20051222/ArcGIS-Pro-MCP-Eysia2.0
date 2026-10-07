using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-064（功能完善第四批）· C 段：数据发现 —— <c>ProjectService</c> 分部实现。
/// <para>SDK 实证：<c>Project.AddItem(IProjectItem)</c> / <c>ItemFactory.Instance.Create(path)</c> /
/// <c>Project.Current.GetItems&lt;T&gt;()</c> / <c>Project.HomeFolderPath</c> / <c>Project.DefaultGeodatabasePath</c>。</para>
/// <para>检索面（<c>search_data</c>）= 工程 home ＋ 默认 GDB ＋ 已注册文件夹连接；GDB 内用
/// <c>Geodatabase.GetDefinitions&lt;T&gt;()</c> 枚举（与 D-034 同通路），文件夹用文件系统枚举（按 GIS 扩展名）。</para>
/// </summary>
public sealed partial class ProjectService
{
    private const int SearchHardCap = 20_000;

    public async Task<OperationResult<AddFolderConnectionResult>> AddFolderConnectionAsync(
        string path, CancellationToken ct = default)
    {
        var normalized = NormalizeFolder(path);
        if (normalized is null)
        {
            return OperationResult<AddFolderConnectionResult>.Fail(
                ErrorCodes.InvalidArgument, $"path '{path}' is not a usable absolute folder path.");
        }

        var existing = await QueuedTask.Run(
            () => Project.Current?.GetItems<FolderConnectionProjectItem>()
                .Select(i => NormalizeFolder(i.Path ?? string.Empty))
                .Where(p => p is not null)
                .ToList() ?? new List<string?>(),
            TaskCreationOptions.None).ConfigureAwait(false);

        var already = existing.Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase));

        if (!already)
        {
            var added = await QueuedTask.Run(() =>
            {
                try
                {
                    var item = ItemFactory.Instance.Create(normalized) as IProjectItem;
                    if (item is null)
                    {
                        return "item-factory did not yield an IProjectItem";
                    }

                    Project.Current!.AddItem(item);
                    return (string?)null;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }, TaskCreationOptions.None).ConfigureAwait(false);

            if (added is not null)
            {
                return OperationResult<AddFolderConnectionResult>.Fail(
                    ErrorCodes.ExecutionFailed, $"failed to register folder connection '{normalized}': {added}");
            }
        }

        var after = await QueuedTask.Run(
            () => Project.Current?.GetItems<FolderConnectionProjectItem>().Count() ?? 0,
            TaskCreationOptions.None).ConfigureAwait(false);

        return OperationResult<AddFolderConnectionResult>.Ok(new AddFolderConnectionResult
        {
            Path = path,
            NormalizedPath = normalized!,
            Added = !already,
            AlreadyPresent = already,
            ConnectionsCount = after,
        });
    }

    public Task<OperationResult<ProjectItemsResult>> GetProjectItemsAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<ProjectItemsResult>>(
            () =>
            {
                var project = Project.Current;
                if (project is null)
                {
                    return OperationResult<ProjectItemsResult>.Fail(
                        ErrorCodes.InvalidState, "no project is open.");
                }

                var connections = new List<DataItemInfo>();
                foreach (var item in project.GetItems<FolderConnectionProjectItem>())
                {
                    connections.Add(new DataItemInfo
                    {
                        Name = item.Name ?? string.Empty,
                        Path = item.Path ?? string.Empty,
                        Type = "FolderConnection",
                        Container = "project",
                    });
                }

                foreach (var item in project.GetItems<GDBProjectItem>())
                {
                    connections.Add(new DataItemInfo
                    {
                        Name = item.Name ?? string.Empty,
                        Path = item.Path ?? string.Empty,
                        Type = "Geodatabase",
                        Container = "project",
                    });
                }

                // 工具箱：Pro 的 toolbox 工程项类型不在本程序集引用面内（构建期 CS0246 证伪）⇒
                // 以工程 home 目录下的 *.tbx / *.pyt 事实枚举，如实标注来源为文件系统。
                var toolboxes = new List<DataItemInfo>();
                try
                {
                    var home = project.HomeFolderPath;
                    if (!string.IsNullOrWhiteSpace(home) && Directory.Exists(home))
                    {
                        foreach (var pattern in new[] { "*.tbx", "*.pyt" })
                        {
                            foreach (var f in Directory.EnumerateFiles(home, pattern))
                            {
                                toolboxes.Add(new DataItemInfo
                                {
                                    Name = Path.GetFileNameWithoutExtension(f),
                                    Path = f,
                                    Type = "Toolbox",
                                    Container = home,
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // 不可读 ⇒ 如实空清单。
                }

                return OperationResult<ProjectItemsResult>.Ok(new ProjectItemsResult
                {
                    Connections = connections,
                    Toolboxes = toolboxes,
                    Count = connections.Count + toolboxes.Count,
                });
            },
            TaskCreationOptions.None);

    public Task<OperationResult<SearchDataResult>> SearchDataAsync(
        string? pattern, string? typeFilter, int topN, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<SearchDataResult>>(
            () =>
            {
                var project = Project.Current;
                if (project is null)
                {
                    return OperationResult<SearchDataResult>.Fail(ErrorCodes.InvalidState, "no project is open.");
                }

                var roots = new List<string>();
                void AddRoot(string? p)
                {
                    if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p) && !roots.Contains(p!, StringComparer.OrdinalIgnoreCase))
                    {
                        roots.Add(p!);
                    }
                }

                AddRoot(project.HomeFolderPath);
                AddRoot(project.DefaultGeodatabasePath);
                foreach (var fc in project.GetItems<FolderConnectionProjectItem>())
                {
                    AddRoot(fc.Path);
                }

                var matches = new List<DataItemInfo>();
                var truncated = false;
                var typeWanted = string.IsNullOrWhiteSpace(typeFilter) ? null : typeFilter!.Trim();

                foreach (var root in roots)
                {
                    if (truncated)
                    {
                        break;
                    }

                    if (root.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                    {
                        matches.AddRange(EnumerateGdb(root, typeWanted));
                    }
                    else
                    {
                        matches.AddRange(EnumerateFolder(root, typeWanted));
                    }

                    if (matches.Count > SearchHardCap)
                    {
                        truncated = true;
                    }
                }

                var filtered = string.IsNullOrWhiteSpace(pattern)
                    ? matches
                    : matches.Where(m => m.Name.IndexOf(pattern!.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                var realMatchCount = filtered.Count;
                var page = filtered
                    .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(m => m.Path, StringComparer.OrdinalIgnoreCase)
                    .Take(topN)
                    .ToList();

                return OperationResult<SearchDataResult>.Ok(new SearchDataResult
                {
                    Pattern = pattern,
                    TypeFilter = typeFilter,
                    SearchedRoots = roots,
                    MatchCount = realMatchCount,
                    TopN = topN,
                    Truncated = realMatchCount > page.Count,
                    Items = page,
                });
            },
            TaskCreationOptions.None);

    private static List<DataItemInfo> EnumerateGdb(string gdb, string? typeWanted)
    {
        var list = new List<DataItemInfo>();
        try
        {
            using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdb)));
            foreach (var def in geodatabase.GetDefinitions<FeatureClassDefinition>())
            {
                Add(list, def.GetName(), gdb, "FeatureClass", typeWanted);
            }

            foreach (var def in geodatabase.GetDefinitions<TableDefinition>())
            {
                Add(list, def.GetName(), gdb, "Table", typeWanted);
            }

            foreach (var def in geodatabase.GetDefinitions<FeatureDatasetDefinition>())
            {
                Add(list, def.GetName(), gdb, "FeatureDataset", typeWanted);
            }
        }
        catch
        {
            // 不可读的 GDB：如实跳过（不伪造条目）。
        }

        return list;
    }

    private static List<DataItemInfo> EnumerateFolder(string folder, string? typeWanted)
    {
        var list = new List<DataItemInfo>();
        var gisExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".shp", ".shx", ".dbf", ".tif", ".tiff", ".img", ".lyr", ".lyrx", ".aprx", ".csv", ".gpx", ".geojson", ".json",
        };

        void AddItem(string name, string path, string type)
        {
            if (typeWanted is not null && !string.Equals(typeWanted, type, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            list.Add(new DataItemInfo { Name = name, Path = path, Type = type, Container = folder });
        }

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(folder))
            {
                var name = Path.GetFileName(dir);
                if (dir.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var inner in EnumerateGdb(dir, typeWanted))
                    {
                        inner.Name = name + "\\" + inner.Name;
                        list.Add(inner);
                    }
                }
                else
                {
                    AddItem(name, dir, "Folder");
                }
            }

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var ext = Path.GetExtension(file);
                if (gisExt.Contains(ext) && !ext.Equals(".dbf", StringComparison.OrdinalIgnoreCase))
                {
                    AddItem(Path.GetFileNameWithoutExtension(file), file, "File");
                }
            }

            foreach (var tbx in Directory.EnumerateFiles(folder, "*.tbx"))
            {
                AddItem(Path.GetFileNameWithoutExtension(tbx), tbx, "Toolbox");
            }

            foreach (var pyt in Directory.EnumerateFiles(folder, "*.pyt"))
            {
                AddItem(Path.GetFileNameWithoutExtension(pyt), pyt, "Toolbox");
            }
        }
        catch
        {
            // 不可读目录：如实跳过。
        }

        return list;
    }

    private static void Add(List<DataItemInfo> list, string? name, string container, string type, string? typeWanted)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (typeWanted is not null
            && !string.Equals(typeWanted, type, StringComparison.OrdinalIgnoreCase)
            && !(string.Equals(typeWanted, "Raster", StringComparison.OrdinalIgnoreCase) && type == "RasterDataset"))
        {
            return;
        }

        list.Add(new DataItemInfo { Name = name!, Path = container + "\\" + name, Type = type, Container = container });
    }

    private static string? NormalizeFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(path);
            return Path.IsPathFullyQualified(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }
}
