using System.IO;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;   // D-061：RasterDatasetDefinition（栅格数据集定义枚举）
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// 数据管理服务实现。提供数据集路径存在性/基础信息（真实文件系统检查）。
/// 说明：完整的数据集元数据读取依赖后续（GP/ArcGIS.Core.Data），此处不伪造。
/// </summary>
/// <remarks>Compatibility host path for bounded raster/table enumeration; raster metadata routing remains on Bridge.</remarks>
public sealed class DataManagementService : IDataManagementService
{
    public Task<OperationResult<DatasetInfo>> GetDatasetInfoAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(OperationResult<DatasetInfo>.Fail(ErrorCodes.InvalidArgument, "path is required."));
        }

        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return Task.FromResult(OperationResult<DatasetInfo>.Fail(ErrorCodes.DatasetNotFound, $"Dataset path not found: {path}"));
        }

        var name = Path.GetFileName(path.TrimEnd('\\', '/'));
        var type = Directory.Exists(path) ? "FolderOrGdb" : Path.GetExtension(path).TrimStart('.');

        return Task.FromResult(OperationResult<DatasetInfo>.Ok(new DatasetInfo
        {
            Path = path,
            Name = string.IsNullOrWhiteSpace(name) ? path : name,
            Type = string.IsNullOrWhiteSpace(type) ? "Dataset" : type
        }));
    }

    // ------------------------------------------------------------------ D-061

    /// <summary>
    /// D-061/D-105：枚举文件地理数据库（<c>*.gdb</c>）或顶层文件夹中的**栅格数据集**。
    /// FileGDB 类型按定义类别保证；文件夹仅尝试打开顶层 GeoTIFF 候选为真实 RasterDataset。
    /// </summary>
    /// <remarks>
    /// 文件夹仅支持非递归顶层 <c>.tif</c>/<c>.tiff</c>；SDE 与其他文件格式仍不在枚举范围内。
    /// </remarks>
    public Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListRastersAsync(string? workspace, CancellationToken ct = default)
        // Rule 4 MCT：SDK（ArcGIS.Core.Data）访问一律经 QueuedTask.Run。
        => QueuedTask.Run(() => Enumerate(workspace, rasters: true), TaskCreationOptions.None);

    /// <summary>
    /// D-061：枚举文件地理数据库工作区内的**独立表**（不含要素类 —— 由定义类别区分，见实现）。
    /// </summary>
    public Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListTablesAsync(string? workspace, CancellationToken ct = default)
        => QueuedTask.Run(() => Enumerate(workspace, rasters: false), TaskCreationOptions.None);

    private static OperationResult<IReadOnlyList<DatasetInfo>> Enumerate(string? workspace, bool rasters)
    {
        if (string.IsNullOrWhiteSpace(workspace))
        {
            return OperationResult<IReadOnlyList<DatasetInfo>>.Fail(ErrorCodes.InvalidArgument, "workspace is required.");
        }

        var ws = workspace!.Trim();

        if (!ws.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
        {
            if (rasters && Directory.Exists(ws))
            {
                return EnumerateFolderRasters(ws);
            }

            return OperationResult<IReadOnlyList<DatasetInfo>>.Fail(
                ErrorCodes.InvalidArgument,
                "workspace must be a file geodatabase ('*.gdb') in this batch; folder and enterprise workspaces are not supported yet. Provided: "
                + ws);
        }

        if (!Directory.Exists(ws))
        {
            return OperationResult<IReadOnlyList<DatasetInfo>>.Fail(
                ErrorCodes.DatasetNotFound, $"Workspace does not exist: {ws}");
        }

        var items = new List<DatasetInfo>();
        try
        {
            using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(ws)));

            if (rasters)
            {
                foreach (var def in gdb.GetDefinitions<RasterDatasetDefinition>())
                using (def)
                {
                    items.Add(ToInfo(ws, def.GetName(), "Raster"));
                }
            }
            else
            {
                // TableDefinition 与 FeatureClassDefinition 为 Definition 的兄弟类型 ⇒ 本枚举只命中独立表。
                foreach (var def in gdb.GetDefinitions<TableDefinition>())
                using (def)
                {
                    items.Add(ToInfo(ws, def.GetName(), "Table"));
                }
            }
        }
        catch (Exception ex)
        {
            return OperationResult<IReadOnlyList<DatasetInfo>>.Fail(
                ErrorCodes.InternalError, "Enumerating the workspace failed: " + ex.Message);
        }

        items.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return OperationResult<IReadOnlyList<DatasetInfo>>.Ok(items);
    }

    /// <summary>
    /// D-105: enumerate only top-level GeoTIFF files in a folder. ArcGIS opens each
    /// candidate as a RasterDataset before it is returned; extension alone is not proof.
    /// This method is called from <c>QueuedTask.Run</c> via Enumerate.
    /// </summary>
    private static OperationResult<IReadOnlyList<DatasetInfo>> EnumerateFolderRasters(string workspace)
    {
        string[] candidates;
        try
        {
            candidates = Directory.EnumerateFiles(workspace, "*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetExtension(path), ".tif", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetExtension(path), ".tiff", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex)
        {
            return OperationResult<IReadOnlyList<DatasetInfo>>.Fail(
                ErrorCodes.InternalError, "Enumerating the raster folder failed: " + ex.Message);
        }

        if (candidates.Length == 0)
        {
            return OperationResult<IReadOnlyList<DatasetInfo>>.Ok(Array.Empty<DatasetInfo>());
        }

        var items = new List<DatasetInfo>();
        try
        {
            var connection = new FileSystemConnectionPath(new Uri(workspace), FileSystemDatastoreType.Raster);
            using var datastore = new FileSystemDatastore(connection);
            foreach (var candidate in candidates)
            {
                var name = Path.GetFileName(candidate);
                try
                {
                    using var dataset = datastore.OpenDataset<RasterDataset>(name);
                    items.Add(ToInfo(workspace, name, "Raster"));
                }
                catch
                {
                    // Ignore a same-extension file that ArcGIS cannot open as a raster dataset.
                }
            }
        }
        catch (Exception ex)
        {
            return OperationResult<IReadOnlyList<DatasetInfo>>.Fail(
                ErrorCodes.InternalError, "Opening the raster folder failed: " + ex.Message);
        }

        items.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return OperationResult<IReadOnlyList<DatasetInfo>>.Ok(items);
    }

    private static DatasetInfo ToInfo(string workspace, string name, string type) => new()
    {
        Name = name,
        Path = workspace.TrimEnd('\\', '/') + "\\" + name,
        Type = type,
        // 空间参考本批不采集（未 LIVE 验证 ⇒ 一律 null，不臆测）。
        SpatialReference = null,
        CellSizeX = null,
        CellSizeY = null,
        BandCount = null
    };
}
