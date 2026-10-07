using System.Globalization;
using System.IO;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;   // D-079 · A3：RasterDatasetDefinition（容器子项枚举，只读）
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// Schema 只读服务（Phase 9 第一批，D-034）。
/// 全部 SDK 访问经 QueuedTask.Run（Rule 4 MCT）；错误码复用既有 32 码，不新增。
/// 路径语义与既有工具一致：GDB 容器内用 SDK 定义枚举；文件路径（shp）用 FileSystemDatastore。
/// exists=false + reason 不掩盖；空态区分"确实为空"与"未支持/不可判定"。
/// </summary>
public sealed partial class SchemaService : ISchemaService
{
    private const int DefaultMaxItems = 500;
    private const int MaxMaxItems = 2000;

    // ---------------------------------------------------------------- get_schema_info

    public Task<OperationResult<SchemaInfo>> GetSchemaInfoAsync(string path, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<SchemaInfo>>(() =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return OperationResult<SchemaInfo>.Fail(ErrorCodes.InvalidArgument, "path is required.");
            }

            try
            {
                var opened = OpenTable(path, ct);
                if (opened.NotFound)
                {
                    return OperationResult<SchemaInfo>.Ok(new SchemaInfo { Path = path, Exists = false, Reason = "NOT_FOUND" });
                }

                if (opened.Table is null)
                {
                    return OperationResult<SchemaInfo>.Fail(ErrorCodes.InternalError, "get_schema_info failed to open dataset.", opened.Error);
                }

                using (opened.Table)
                {
                    var td = opened.Table.GetDefinition();
                    var fields = new List<SchemaFieldInfo>();
                    foreach (var f in td.GetFields())
                    {
                        fields.Add(new SchemaFieldInfo
                        {
                            Name = f.Name ?? string.Empty,
                            Alias = f.AliasName ?? string.Empty,
                            Type = f.FieldType.ToString(),
                            IsNullable = f.IsNullable,
                            DefaultValue = f.GetDefaultValue(null)?.ToString(),
                            Length = f.Length,
                            Precision = f.Precision,
                            DomainName = f.GetDomain(null)?.GetName(),
                        });
                    }

                    var info = new SchemaInfo
                    {
                        Path = path,
                        Exists = true,
                        DataType = opened.IsFeatureClass ? "FeatureClass" : "Table",
                        Fields = fields,
                    };

                    if (opened.Table is FeatureClass fc && td is FeatureClassDefinition fcd)
                    {
                        info.GeometryType = fcd.GetShapeType().ToString();
                        var sr = fcd.GetSpatialReference();
                        info.SpatialReference = sr?.Name ?? (sr != null ? sr.Wkid.ToString(CultureInfo.InvariantCulture) : null);
                    }

                    return OperationResult<SchemaInfo>.Ok(info);
                }
            }
            catch (Exception ex)
            {
                return OperationResult<SchemaInfo>.Fail(ErrorCodes.InternalError, "get_schema_info failed.", ex.Message);
            }
        },
        TaskCreationOptions.None);

    // ---------------------------------------------------------------- get_domains

    public Task<OperationResult<IReadOnlyList<DomainInfo>>> GetDomainsAsync(string workspace, int maxItems, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<DomainInfo>>>(() =>
        {
            if (string.IsNullOrWhiteSpace(workspace))
            {
                return OperationResult<IReadOnlyList<DomainInfo>>.Fail(ErrorCodes.InvalidArgument, "workspace is required.");
            }

            var cap = Math.Clamp(maxItems <= 0 ? DefaultMaxItems : maxItems, 1, MaxMaxItems);

            try
            {
                if (!StateProof.IsGdbContainerPath(workspace))
                {
                    // 非 GDB 工作空间无域（"确实为空"，显式说明）。
                    return OperationResult<IReadOnlyList<DomainInfo>>.Ok(
                        Array.Empty<DomainInfo>(),
                        "workspace is not a FileGDB; no domains are defined (expected for non-GDB workspaces).");
                }

                using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace.TrimEnd('\\', '/'))));
                var domains = gdb.GetDomains();
                var outList = new List<DomainInfo>();
                foreach (var d in domains)
                {
                    ct.ThrowIfCancellationRequested();
                    if (outList.Count >= cap)
                    {
                        break;
                    }

                    var info = new DomainInfo { Name = d.GetName() ?? string.Empty };
                    if (d is CodedValueDomain cvd)
                    {
                        info.Type = "CodedValue";
                        var cv = new Dictionary<string, string>();
                        foreach (var kv in cvd.GetCodedValuePairs())
                        {
                            cv[kv.Key?.ToString() ?? string.Empty] = kv.Value?.ToString() ?? string.Empty;
                        }

                        info.CodedValues = cv;
                    }
                    else if (d is RangeDomain rd)
                    {
                        info.Type = "Range";
                        info.MinValue = rd.GetMinValue();
                        info.MaxValue = rd.GetMaxValue();
                    }
                    else
                    {
                        info.Type = d.GetType().Name;
                    }

                    outList.Add(info);
                }

                return OperationResult<IReadOnlyList<DomainInfo>>.Ok(outList);
            }
            catch (Exception ex)
            {
                return OperationResult<IReadOnlyList<DomainInfo>>.Fail(ErrorCodes.InternalError, "get_domains failed.", ex.Message);
            }
        },
        TaskCreationOptions.None);

    // ---------------------------------------------------------------- get_subtypes

    public Task<OperationResult<SubtypeInfo>> GetSubtypesAsync(string path, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<SubtypeInfo>>(() =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return OperationResult<SubtypeInfo>.Fail(ErrorCodes.InvalidArgument, "path is required.");
            }

            try
            {
                var opened = OpenTable(path, ct);
                if (opened.NotFound)
                {
                    return OperationResult<SubtypeInfo>.Ok(new SubtypeInfo { Path = path, Exists = false, Reason = "NOT_FOUND" });
                }

                if (opened.Table is null)
                {
                    return OperationResult<SubtypeInfo>.Fail(ErrorCodes.InternalError, "get_subtypes failed to open dataset.", opened.Error);
                }

                using (opened.Table)
                {
                    var td = opened.Table.GetDefinition();
                    var field = td.GetSubtypeField();
                    var info = new SubtypeInfo { Path = path, Exists = true, SubtypeField = string.IsNullOrEmpty(field) ? null : field };

                    if (string.IsNullOrEmpty(field))
                    {
                        // 无子类型字段 = "确实为空"（与"未支持"区分）。
                        return OperationResult<SubtypeInfo>.Ok(info);
                    }

                    info.SubtypeFieldType = td.GetFields().FirstOrDefault(f => string.Equals(f.Name, field, StringComparison.OrdinalIgnoreCase))?.FieldType.ToString();

                    var map = new Dictionary<string, string>();
                    foreach (var s in td.GetSubtypes())
                    {
                        map[s.GetCode().ToString(CultureInfo.InvariantCulture)] = s.GetName() ?? string.Empty;
                    }

                    info.Subtypes = map;
                    return OperationResult<SubtypeInfo>.Ok(info);
                }
            }
            catch (Exception ex)
            {
                return OperationResult<SubtypeInfo>.Fail(ErrorCodes.InternalError, "get_subtypes failed.", ex.Message);
            }
        },
        TaskCreationOptions.None);

    // ---------------------------------------------------------------- get_indexes

    public Task<OperationResult<IReadOnlyList<IndexInfo>>> GetIndexesAsync(string path, int maxItems, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<IndexInfo>>>(() =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return OperationResult<IReadOnlyList<IndexInfo>>.Fail(ErrorCodes.InvalidArgument, "path is required.");
            }

            var cap = Math.Clamp(maxItems <= 0 ? DefaultMaxItems : maxItems, 1, MaxMaxItems);

            try
            {
                var opened = OpenTable(path, ct);
                if (opened.NotFound)
                {
                    return OperationResult<IReadOnlyList<IndexInfo>>.Ok(Array.Empty<IndexInfo>(), "NOT_FOUND: dataset not found: " + path);
                }

                if (opened.Table is null)
                {
                    return OperationResult<IReadOnlyList<IndexInfo>>.Fail(ErrorCodes.InternalError, "get_indexes failed to open dataset.", opened.Error);
                }

                using (opened.Table)
                {
                    var td = opened.Table.GetDefinition();
                    var shapeField = opened.Table is FeatureClass fc2
                        ? (td as FeatureClassDefinition)?.GetShapeField() ?? string.Empty
                        : string.Empty;
                    var indexes = td.GetIndexes();
                    var outList = new List<IndexInfo>();
                    foreach (var idx in indexes)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (outList.Count >= cap)
                        {
                            break;
                        }

                        var fields = idx.GetFields()?.Select(f => f.Name ?? string.Empty).ToList() ?? new List<string>();
                        outList.Add(new IndexInfo
                        {
                            Name = idx.GetName() ?? string.Empty,
                            Fields = fields,
                            IsUnique = idx.IsUnique(),
                            IsSpatial = !string.IsNullOrEmpty(shapeField)
                                && fields.Any(f => string.Equals(f, shapeField, StringComparison.OrdinalIgnoreCase)),
                        });
                    }

                    return OperationResult<IReadOnlyList<IndexInfo>>.Ok(outList);
                }
            }
            catch (Exception ex)
            {
                return OperationResult<IReadOnlyList<IndexInfo>>.Fail(ErrorCodes.InternalError, "get_indexes failed.", ex.Message);
            }
        },
        TaskCreationOptions.None);

    // ---------------------------------------------------------------- D-079 · A2/A3 容器子项只读摘要

    /// <summary>
    /// D-079 · A2/A3：**只读**枚举文件地理数据库内的直系子项（要素类/表尽力计数）。
    /// Rule 4 MCT：全部 SDK 访问在 QueuedTask.Run 内；仅 GetDefinitions / OpenDataset / GetCount（无编辑、无写入 API）。
    /// 限深 1 层：要素数据集作为**容器项**列出，不递归其子要素类（其行数不计入聚合，聚合口径由调用方披露）。
    /// 错误码复用既有 33 码（INVALID_ARGUMENT / DATASET_NOT_FOUND / INTERNAL_ERROR / NOT_IMPLEMENTED），零新增。
    /// </summary>
    public Task<OperationResult<IReadOnlyList<DatasetMemberInfo>>> ListDatasetMembersAsync(
        string containerPath, int maxItems, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<DatasetMemberInfo>>>(() =>
        {
            if (string.IsNullOrWhiteSpace(containerPath))
            {
                return OperationResult<IReadOnlyList<DatasetMemberInfo>>.Fail(ErrorCodes.InvalidArgument, "containerPath is required.");
            }

            var ws = containerPath!.Trim();
            if (!ws.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<IReadOnlyList<DatasetMemberInfo>>.Fail(ErrorCodes.InvalidArgument,
                    "only file geodatabase ('*.gdb') containers are enumerated in this batch; provided: " + ws);
            }

            if (!Directory.Exists(ws))
            {
                return OperationResult<IReadOnlyList<DatasetMemberInfo>>.Fail(ErrorCodes.DatasetNotFound, "Workspace does not exist: " + ws);
            }

            var cap = Math.Clamp(maxItems, 1, MaxMaxItems);
            var members = new List<DatasetMemberInfo>();
            try
            {
                using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(ws)));
                AddCountedMembers(gdb, ws, members, cap, FeatureClassDefinitionKind.FeatureClass, ct);
                AddCountedMembers(gdb, ws, members, cap, FeatureClassDefinitionKind.Table, ct);
                AddContainerMembers(gdb, ws, members, cap, ct);
            }
            catch (Exception ex)
            {
                return OperationResult<IReadOnlyList<DatasetMemberInfo>>.Fail(
                    ErrorCodes.InternalError, "Enumerating container members failed.", ex.Message);
            }

            members.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return OperationResult<IReadOnlyList<DatasetMemberInfo>>.Ok(members);
        },
        TaskCreationOptions.None);

    private enum FeatureClassDefinitionKind { FeatureClass, Table }

    /// <summary>要素类 / 独立表：打开后取 GetCount()（元数据级只读计数）。</summary>
    private static void AddCountedMembers(
        Geodatabase gdb, string ws, List<DatasetMemberInfo> members, int cap, FeatureClassDefinitionKind kind, CancellationToken ct)
    {
        foreach (var def in kind == FeatureClassDefinitionKind.FeatureClass
                     ? gdb.GetDefinitions<FeatureClassDefinition>()
                     : gdb.GetDefinitions<TableDefinition>())
        {
            ct.ThrowIfCancellationRequested();
            if (members.Count >= cap)
            {
                return;
            }

            using (def)
            {
                var name = def.GetName() ?? string.Empty;
                var member = NewMember(ws, name, kind == FeatureClassDefinitionKind.FeatureClass ? "FeatureClass" : "Table");
                try
                {
                    if (kind == FeatureClassDefinitionKind.FeatureClass)
                    {
                        using var fc = gdb.OpenDataset<FeatureClass>(name);
                        member.RowCount = fc.GetCount();
                    }
                    else
                    {
                        using var table = gdb.OpenDataset<Table>(name);
                        member.RowCount = table.GetCount();
                    }
                }
                catch (Exception ex)
                {
                    member.RowCount = null;
                    member.CountUnavailableReason = "count-failed: " + ex.GetType().Name;
                }

                members.Add(member);
            }
        }
    }

    /// <summary>要素数据集 / 栅格数据集：非行式可计数项 ⇒ 如实给原因（不硬造 0）。</summary>
    private static void AddContainerMembers(Geodatabase gdb, string ws, List<DatasetMemberInfo> members, int cap, CancellationToken ct)
    {
        foreach (var def in gdb.GetDefinitions<FeatureDatasetDefinition>())
        {
            ct.ThrowIfCancellationRequested();
            if (members.Count >= cap)
            {
                return;
            }

            using (def)
            {
                var member = NewMember(ws, def.GetName() ?? string.Empty, "FeatureDataset");
                member.CountUnavailableReason = "container:featureDataset (限深 1 层，不递归子要素类)";
                members.Add(member);
            }
        }

        foreach (var def in gdb.GetDefinitions<RasterDatasetDefinition>())
        {
            ct.ThrowIfCancellationRequested();
            if (members.Count >= cap)
            {
                return;
            }

            using (def)
            {
                var member = NewMember(ws, def.GetName() ?? string.Empty, "RasterDataset");
                member.CountUnavailableReason = "not-row-addressable:rasterDataset";
                members.Add(member);
            }
        }
    }

    private static DatasetMemberInfo NewMember(string ws, string name, string type) => new()
    {
        Name = name,
        Path = ws.TrimEnd('\\', '/') + "\\" + name,
        Type = type,
    };

    // ---------------------------------------------------------------- helpers

    private sealed record OpenedResult(Table? Table, bool IsFeatureClass, bool NotFound, string Error);
    /// <summary>按路径打开数据集（GDB 容器内 SDK 定义枚举；文件路径 FileSystemDatastore）。必须在 MCT 内调用。</summary>
    private static OpenedResult OpenTable(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (StateProof.IsGdbContainerPath(path))
        {
            var (workspace, name) = SplitGdbPath(path);
            if (string.IsNullOrEmpty(workspace) || string.IsNullOrEmpty(name))
            {
                return new OpenedResult(null, false, false, "invalid GDB container path: " + path);
            }

            if (!Directory.Exists(workspace))
            {
                return new OpenedResult(null, false, true, "gdb workspace missing");
            }

            using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace)));
            foreach (var def in gdb.GetDefinitions<FeatureClassDefinition>())
            {
                ct.ThrowIfCancellationRequested();
                if (string.Equals(def.GetName(), name, StringComparison.OrdinalIgnoreCase))
                {
                    var fc = gdb.OpenDataset<FeatureClass>(def.GetName());
                    return new OpenedResult(fc, true, false, string.Empty);
                }
            }

            foreach (var def in gdb.GetDefinitions<TableDefinition>())
            {
                ct.ThrowIfCancellationRequested();
                if (string.Equals(def.GetName(), name, StringComparison.OrdinalIgnoreCase))
                {
                    var t = gdb.OpenDataset<Table>(def.GetName());
                    return new OpenedResult(t, false, false, string.Empty);
                }
            }

            return new OpenedResult(null, false, true, "dataset not found in gdb");
        }

        // 文件路径（shp）：FileSystemDatastore（连接=所在目录，数据集名=文件基名）。
        if (!File.Exists(path))
        {
            return new OpenedResult(null, false, true, "file not found");
        }

        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(full);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(baseName))
        {
            return new OpenedResult(null, false, false, "invalid file path: " + path);
        }

        using var fsds = new FileSystemDatastore(new FileSystemConnectionPath(new Uri(dir), FileSystemDatastoreType.Shapefile));
        var table = fsds.OpenDataset<Table>(baseName);
        return new OpenedResult(table, table is FeatureClass, false, string.Empty);
    }

    private static (string Workspace, string Name) SplitGdbPath(string path)
    {
        var segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var gdbIdx = Array.FindLastIndex(segments, s => s.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase));
        if (gdbIdx < 0)
        {
            return (string.Empty, string.Empty);
        }

        var workspace = string.Join("\\", segments.Take(gdbIdx + 1));
        var name = gdbIdx + 1 < segments.Length ? segments[^1] : string.Empty;
        return (workspace, name);
    }
}
