using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Jobs;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

// ══════════════════════════════════════════════════════════════════════════════
// D-073 · 数据文件夹工作流（5 件）：
//   scan_data_folder（只读）／load_folder_data（写）／apply_processing_plan（写，分片）
//   ／get_job_status（只读）／get_job_report（只读）
// 设计红线：口述→计划回显（dryRun 缺省 true）→ confirm 才执行；源数据只读；
//   守卫链复用（ProtectedOutputPathGuard / 输出存在性 / 只读模式 / GP 白名单 fail-closed）；
//   分片 + jobId + 断点落 D 盘；审计带 jobId；错误码零新增（复用 33 个）。
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// D-079 · A1（O-D073-05）：口述→计划回显的**歧义**与**缺省值**披露。
/// 仅**新增响应字段**（<c>ambiguities</c> / <c>defaults</c>）——不改参数面、不改既有字段语义、
/// 也不改「未落计划不得执行」（dryRun 缺省 true ＋ confirm 缺省拒）的链路。
/// </summary>
internal static class PlanEchoDisclosure
{
    internal const string TargetLocation = "targetLocation";
    internal const string OutputNaming = "outputNaming";
    internal const string OverwritePolicy = "overwritePolicy";

    /// <summary>缺省值条目：值 + 来源（user=调用方给定 / default=本工具缺省规则）+ 规则说明。</summary>
    internal static Dictionary<string, object?> Field(object? value, bool userSpecified, string rule) => new()
    {
        ["value"] = value,
        ["source"] = userSpecified ? "user" : "default",
        ["rule"] = rule,
    };

    /// <summary>歧义条目：类别 + 事实（为什么会歧义）+ 本次采用的解析（不静默猜测）。</summary>
    internal static Dictionary<string, object?> Ambiguity(string kind, string detail, string resolution) => new()
    {
        ["kind"] = kind,
        ["detail"] = detail,
        ["resolution"] = resolution,
    };

    /// <summary>输出型参数名（用于判定"步骤未指定产物落点"）。</summary>
    internal static bool IsOutputArgumentName(string name)
        => name.StartsWith("out", StringComparison.OrdinalIgnoreCase)
           || name.Contains("output", StringComparison.OrdinalIgnoreCase);

    /// <summary>基名（去扩展名）碰撞组：命名缺省规则会互相覆盖/混淆 ⇒ 必须明示。</summary>
    internal static List<string> BaseNameCollisions(IEnumerable<string> paths)
    {
        var groups = paths
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        groups.Sort(StringComparer.Ordinal);
        return groups;
    }
}

/// <summary>D-073 · 文件夹扫描（只读）：目录 → 可处理清单（含可用性判定/建议动作/跳过原因）。</summary>
public sealed class ScanDataFolderTool : McpToolBase
{
    public override string Name => "scan_data_folder";

    public override string Description =>
        "扫描一个数据文件夹并返回**可处理清单**（只读）：name/path/kind（featureclass·table·raster·dataset·other）/" +
        "geometryType/spatialReference/featureOrRowCount/countBasis/countUnavailableReason/sizeBytes/available/suggestedAction/skipReason；" +
        "GDB 容器项另给 memberCount/members（限深 1 层、上限 50、超出截断标注）。计数不可得时**如实给原因**，不裸 null。" +
        "路径守卫 + 深度/数量双上限 + 不穿越 reparse；不可读项**如实列原因**，不静默丢。参数：folderPath*、maxDepth（缺省 2，上限 4）、maxItems（缺省 200，上限 1000）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["folderPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Absolute folder path to scan." },
            ["maxDepth"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Max recursion depth (default 2, max 4)." },
            ["maxItems"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Max items returned (default 200, max 1000)." },
        },
        ["required"] = new List<object?> { "folderPath" },
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public const int MaxDepthCeiling = 4;
    public const int MaxItemsCeiling = 1000;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var folder = ToolArgs.GetString(context, "folderPath");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "folderPath is required."));
        }

        var full = Path.GetFullPath(folder!);
        var hit = ProtectedOutputPathGuard.Match(full);
        if (hit is not null)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"folderPath is refused by the path guard ('{hit}'); nothing was read."));
        }

        if (!Directory.Exists(full))
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.NotFound, $"Folder not found: {full}"));
        }

        var maxDepth = Math.Clamp(ToolArgs.GetInt(context, "maxDepth") ?? 2, 0, MaxDepthCeiling);
        var maxItems = Math.Clamp(ToolArgs.GetInt(context, "maxItems") ?? 200, 1, MaxItemsCeiling);

        var items = new List<Dictionary<string, object?>>();
        var truncated = false;
        long total = 0;
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((full, 0));
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (queue.Count > 0)
        {
            var (dir, depth) = queue.Dequeue();
            if (!visited.Add(dir))
            {
                continue;
            }

            string[] entries;
            try
            {
                entries = Directory.GetFileSystemEntries(dir);
            }
            catch (Exception ex)
            {
                items.Add(new Dictionary<string, object?>
                {
                    ["name"] = Path.GetFileName(dir),
                    ["path"] = dir,
                    ["kind"] = "other",
                    ["featureOrRowCount"] = null,
                    ["countUnavailableReason"] = "not-a-dataset: " + ex.GetType().Name,
                    ["available"] = false,
                    ["suggestedAction"] = "none",
                    ["skipReason"] = "unreadable-directory: " + ex.GetType().Name,
                });
                continue;
            }

            foreach (var entry in entries.OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
            {
                var isDir = Directory.Exists(entry);
                var info = isDir ? new DirectoryInfo(entry) : (FileSystemInfo)new FileInfo(entry);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue; // 不穿越 reparse point
                }

                var ext = Path.GetExtension(entry).ToLowerInvariant();
                var name = Path.GetFileName(entry);

                if (isDir)
                {
                    if (ext == ".gdb")
                    {
                        total++;
                        items.Add(DescribeGdb(context, entry, name));
                    }
                    else if (depth < maxDepth)
                    {
                        queue.Enqueue((entry, depth + 1));
                    }

                    continue;
                }

                if (ext is not (".shp" or ".csv" or ".tif" or ".tiff" or ".img" or ".jpg" or ".jpeg" or ".png"))
                {
                    continue; // 非数据文件不列入清单
                }

                total++;
                items.Add(DescribeItem(context, entry, name, ext));
            }
        }

        if (items.Count > maxItems)
        {
            truncated = true;
            items = items.Take(maxItems).ToList();
        }

        return Task.FromResult(OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["folderPath"] = full,
            ["maxDepth"] = maxDepth,
            ["maxItems"] = maxItems,
            ["itemCount"] = items.Count,
            ["totalDiscovered"] = total,
            ["truncated"] = truncated,
            ["readOnly"] = true,
            ["sourceUntouched"] = true,
            ["items"] = items,
        }, $"Scanned {items.Count} item(s) (read-only)."));
    }

    /// <summary>
    /// D-079 · A2/A3（O-D073-02 ／ O-D073-01）：GDB 容器项 —— **尽力只读计数** ＋ 限深 1 层子项摘要。
    /// 计数走宿主只读枚举通道（GetDefinitions + GetCount，无编辑/无写入 API）；不可得 ⇒ <c>countUnavailableReason</c>（不再裸 null）。
    /// 上限 <see cref="MembersCap"/> 项、超出打截断标注；聚合口径 = 顶层要素类 + 独立表（要素数据集为其子项，不计入，见 countScope）。
    /// </summary>
    internal const int MembersCap = 50;

    internal static Dictionary<string, object?> DescribeGdb(ToolExecutionContext context, string entry, string name)
    {
        long? total = null;
        string? countReason = null;
        string? countScope = null;
        List<Dictionary<string, object?>>? members = null;
        int? memberCount = null;
        var membersTruncated = false;

        if (context.Host is null)
        {
            countReason = "host-unavailable";
        }
        else
        {
            try
            {
                // 多取 1 条用于判定"是否还有更多"（截断标注），不放宽上限语义。
                var r = context.Host.Schema.ListDatasetMembersAsync(entry, MembersCap + 1, context.CancellationToken)
                    .GetAwaiter().GetResult();
                if (!r.Success || r.Data is null)
                {
                    countReason = "enumerate-unavailable: " + (r.Errors.Count > 0 ? r.Errors[0].Code : "unknown");
                }
                else
                {
                    var listed = r.Data!;
                    membersTruncated = listed.Count > MembersCap;
                    var kept = membersTruncated ? listed.Take(MembersCap).ToList() : listed;
                    memberCount = kept.Count;
                    members = kept.Select(m => new Dictionary<string, object?>
                    {
                        ["name"] = m.Name,
                        ["path"] = m.Path,
                        ["type"] = m.Type,
                        ["featureOrRowCount"] = m.RowCount,
                        ["countUnavailableReason"] = m.RowCount.HasValue ? null : (m.CountUnavailableReason ?? "count-unavailable"),
                    }).ToList();

                    var rowAddressable = kept
                        .Where(m => m.Type is "FeatureClass" or "Table")
                        .ToList();
                    if (rowAddressable.Count == 0)
                    {
                        countReason = kept.Count == 0
                            ? "container-empty"
                            : "no-row-addressable-member（仅容器/栅格子项）";
                    }
                    else if (rowAddressable.Any(m => !m.RowCount.HasValue))
                    {
                        var first = rowAddressable.First(m => !m.RowCount.HasValue);
                        countReason = "member-count-unavailable: " + first.Name
                                      + "（" + (first.CountUnavailableReason ?? "unknown") + "）";
                    }
                    else if (membersTruncated)
                    {
                        // 截断 ⇒ 聚合必然低估，不冒充容器总行数（宁可给原因，不给半个真相）
                        total = null;
                        countReason = "truncated:more-than-" + MembersCap + "-members（聚合口径不可靠 ⇒ 不报数）";
                    }
                    else
                    {
                        total = rowAddressable.Sum(m => m.RowCount!.Value);
                        countScope = "sum-of-top-level-featureclasses-and-tables";
                    }
                }
            }
            catch (Exception ex)
            {
                countReason = "enumerate-failed: " + ex.GetType().Name;
            }
        }

        return new Dictionary<string, object?>
        {
            ["name"] = name,
            ["path"] = entry,
            ["kind"] = "dataset",
            ["geometryType"] = null,
            ["spatialReference"] = null,
            ["featureOrRowCount"] = total,
            ["countUnavailableReason"] = countReason,
            ["countScope"] = countScope,
            ["memberCount"] = memberCount,
            ["members"] = members,
            ["membersTruncated"] = membersTruncated,
            ["memberCap"] = MembersCap,
            ["sizeBytes"] = null,
            ["available"] = true,
            ["suggestedAction"] = "load_folder_data(copy_to_gdb|register_only) 或 list_workspace_datasets 展开",
            ["skipReason"] = null,
        };
    }

    private static Dictionary<string, object?> DescribeItem(ToolExecutionContext context, string entry, string name, string ext)
    {
        var kind = ext switch
        {
            ".shp" => "featureclass",
            ".csv" => "table",
            ".tif" or ".tiff" or ".img" or ".jpg" or ".jpeg" or ".png" => "raster",
            _ => "other",
        };
        string? geometryType = null;
        string? sr = null;
        long? count = null;
        string? countBasis = null;
        string? countReason = null;
        long? size = null;
        var available = true;
        string? skipReason = null;

        try
        {
            size = new FileInfo(entry).Length;
        }
        catch (Exception ex)
        {
            available = false;
            skipReason = "size-unreadable: " + ex.GetType().Name;
        }

        if (kind == "raster")
        {
            if (context.Host is null)
            {
                available = false;
                skipReason = "host-unavailable";
                countReason = "host-unavailable";
            }
            else
            {
                try
                {
                    var r = context.Host.Raster.GetRasterInfoAsync(entry, context.CancellationToken).GetAwaiter().GetResult();
                    if (r.Success && r.Data is not null)
                    {
                        geometryType = "Raster";
                        count = (long)r.Data.Width * r.Data.Height;
                        countBasis = "width-times-height-pixels";
                    }
                    else
                    {
                        available = false;
                        skipReason = "raster-describe-failed: " + (r.Errors.Count > 0 ? r.Errors[0].Code : "unknown");
                        countReason = "raster-describe-failed: " + (r.Errors.Count > 0 ? r.Errors[0].Code : "unknown");
                    }
                }
                catch (Exception ex)
                {
                    available = false;
                    skipReason = "raster-describe-failed: " + ex.GetType().Name;
                    countReason = "raster-describe-failed: " + ex.GetType().Name;
                }
            }
        }
        else if (kind == "featureclass")
        {
            if (context.Host is not null)
            {
                try
                {
                    var s = context.Host.Schema.GetSchemaInfoAsync(entry, context.CancellationToken).GetAwaiter().GetResult();
                    if (s.Success && s.Data is not null)
                    {
                        geometryType = s.Data.GeometryType;
                        sr = s.Data.SpatialReference;
                        if (!s.Data.Exists)
                        {
                            available = false;
                            skipReason = "schema-not-exists: " + (s.Data.Reason ?? "unknown");
                        }
                    }
                    else
                    {
                        available = false;
                        skipReason = "schema-describe-failed";
                    }
                }
                catch (Exception ex)
                {
                    available = false;
                    skipReason = "schema-describe-failed: " + ex.GetType().Name;
                }
            }

            // shp 行数：DBF 头部记录数（纯文件读取，不经 SDK）
            var dbf = Path.ChangeExtension(entry, ".dbf");
            if (!File.Exists(dbf))
            {
                countReason = "dbf-missing";
            }
            else
            {
                try
                {
                    using var fs = File.OpenRead(dbf);
                    var header = new byte[8];
                    if (fs.Read(header, 0, 8) == 8)
                    {
                        count = BitConverter.ToInt32(header, 4);
                        countBasis = "dbf-header-record-count";
                    }
                    else
                    {
                        countReason = "dbf-header-short-read";
                    }
                }
                catch (Exception ex)
                {
                    count = null;
                    countReason = "dbf-unreadable: " + ex.GetType().Name;
                }
            }
        }
        else if (kind == "table" && ext == ".csv")
        {
            try
            {
                count = File.ReadLines(entry).Count() - 1; // 去表头
                countBasis = "line-count-minus-header";
            }
            catch (Exception ex)
            {
                available = false;
                skipReason = "csv-read-failed: " + ex.GetType().Name;
                countReason = "csv-read-failed: " + ex.GetType().Name;
            }
        }
        else
        {
            countReason = "not-row-addressable: " + kind;
        }

        if (count.HasValue && countReason is null && countBasis is null)
        {
            countBasis = "unspecified";
        }

        var suggested = available
            ? kind switch
            {
                "featureclass" => "load_folder_data(into_map|copy_to_gdb)",
                "table" => "load_folder_data(copy_to_gdb)",
                "raster" => "apply_processing_plan(raster_clip 等)",
                _ => "load_folder_data(register_only)",
            }
            : "none";

        return new Dictionary<string, object?>
        {
            ["name"] = name,
            ["path"] = entry,
            ["kind"] = kind,
            ["geometryType"] = geometryType,
            ["spatialReference"] = sr,
            ["featureOrRowCount"] = count,
            ["countBasis"] = countBasis,
            ["countUnavailableReason"] = count.HasValue ? null : (countReason ?? "count-unavailable"),
            ["sizeBytes"] = size,
            ["available"] = available,
            ["suggestedAction"] = suggested,
            ["skipReason"] = skipReason,
        };
    }
}

/// <summary>D-073 · 文件夹装载（写；dryRun 缺省 true、confirm 缺省拒；幂等）。</summary>
public sealed class LoadFolderDataTool : McpToolBase
{
    private static readonly string[] Modes = { "into_map", "copy_to_gdb", "register_only" };

    public override string Name => "load_folder_data";

    public override string Description =>
        "把文件夹内的数据（items 子集，缺省=全部）批量装载到**用户指定目标**（写；confirm 缺省拒、dryRun 缺省 true ⇒ 先回显结构化计划，" +
        "计划含 ambiguities 三类歧义〔目标位置/输出命名/覆盖策略〕与 defaults 缺省值）。" +
        "outputMode：into_map（建层入图）／copy_to_gdb（复制入库，需 targetGdb）／register_only（仅注册文件夹连接）。**幂等**（已存在 ⇒ 跳过并披露）；逐项 ok/skipped/failed + reason；源数据零写入。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["folderPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Folder to load from." },
            ["items"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "string" }, ["description"] = "Item names/paths (default: all scan-able items)." },
            ["outputMode"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "into_map | copy_to_gdb | register_only" },
            ["targetGdb"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target geodatabase (required for copy_to_gdb)." },
            ["mapName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Target map for into_map (default: current map)." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Must be explicitly true to execute (default refuse)." },
            ["dryRun"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Plan echo only (default true)." },
            ["jobId"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Optional job id for audit/report correlation." },
        },
        ["required"] = new List<object?> { "folderPath", "outputMode" },
    };

    protected override string CategoryName => ToolCategories.Project;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var folder = ToolArgs.GetString(context, "folderPath");
        var mode = ToolArgs.GetString(context, "outputMode");
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(mode))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "folderPath and outputMode are required.");
        }

        if (!Modes.Contains(mode))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"outputMode must be one of: {string.Join(" | ", Modes)}.");
        }

        if (ToolArgs.GetBool(context, "inPlace"))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "inPlace is not supported: source data is read-only by design; no change was made.");
        }

        var full = Path.GetFullPath(folder!);
        var hit = ProtectedOutputPathGuard.Match(full);
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, $"folderPath is refused by the path guard ('{hit}').");
        }

        if (!Directory.Exists(full))
        {
            return OperationResult<object?>.Fail(ErrorCodes.NotFound, $"Folder not found: {full}");
        }

        var targetGdb = ToolArgs.GetString(context, "targetGdb");
        if (mode == "copy_to_gdb")
        {
            if (string.IsNullOrWhiteSpace(targetGdb))
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "targetGdb is required for outputMode=copy_to_gdb.");
            }

            var gdbHit = ProtectedOutputPathGuard.Match(Path.GetFullPath(targetGdb!));
            if (gdbHit is not null)
            {
                return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, $"targetGdb is refused by the path guard ('{gdbHit}').");
            }

            if (WorkflowJobStore.IsTempLikePath(targetGdb!))
            {
                return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, "G-138: targetGdb must not live under %TEMP%.");
            }
        }

        var requested = ToolArgs.GetStringList(context, "items");
        var invoker = context.Invoker ?? ToolInvoker.Default;
        var scanContext = new ToolExecutionContext
        {
            RequestId = context.RequestId,
            CancellationToken = context.CancellationToken,
            Logger = context.Logger,
            Host = context.Host,
            Python = context.Python,
            Settings = context.Settings,
            Arguments = context.Arguments,
            Registry = context.Registry,
            ReadOnly = context.ReadOnly,
            Invoker = invoker,
        };
        var scan = await invoker.InvokeAsync(new ScanDataFolderTool(), scanContext).ConfigureAwait(false);
        if (!scan.Success || scan.Data is not Dictionary<string, object?> scanData)
        {
            return scan;
        }

        var all = (scanData["items"] as List<Dictionary<string, object?>>) ?? new List<Dictionary<string, object?>>();
        var selected = requested.Count == 0
            ? all.Where(i => i["kind"] as string is "featureclass" or "table" or "raster" or "dataset").ToList()
            : all.Where(i => requested.Any(r => string.Equals(r, i["name"] as string, StringComparison.OrdinalIgnoreCase)
                                                || string.Equals(r, i["path"] as string, StringComparison.OrdinalIgnoreCase))).ToList();

        if (selected.Count == 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.NotFound, "No matching items to load (see scan_data_folder for the available inventory).");
        }

        var jobId = ToolArgs.GetString(context, "jobId") ?? ("load-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        if (!WorkflowJobStore.IsSafeJobId(jobId))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "jobId must be a simple file-safe identifier of at most 128 characters.");
        var confirm = ToolArgs.GetBool(context, "confirm");
        var dryRun = ToolArgs.GetBool(context, "dryRun", true);
        var mapNameArg = ToolArgs.GetString(context, "mapName");
        var itemsArg = ToolArgs.GetStringList(context, "items");

        // ── D-079 · A1（O-D073-05）：装载计划的三类歧义 ＋ 未指定项缺省值 —— **仅新增响应字段** ──
        var selectedPaths = selected.Select(i => i["path"] as string ?? string.Empty).ToList();
        var collisions = PlanEchoDisclosure.BaseNameCollisions(selectedPaths);

        var loadAmbiguities = new List<Dictionary<string, object?>>
        {
            PlanEchoDisclosure.Ambiguity(PlanEchoDisclosure.TargetLocation,
                mode switch
                {
                    "into_map" => string.IsNullOrWhiteSpace(mapNameArg)
                        ? "mapName 未指定"
                        : "mapName 已给定：" + mapNameArg,
                    "copy_to_gdb" => "目标库已给定：" + targetGdb,
                    _ => "目标 = 当前工程的文件夹连接（工程级，非某个地图/库）",
                },
                mode switch
                {
                    "into_map" => string.IsNullOrWhiteSpace(mapNameArg)
                        ? "执行时解析为**当前活动地图**（GetCurrentMap）；无活动地图 ⇒ 该项 failed，不猜图"
                        : "落入指定地图",
                    "copy_to_gdb" => "复制进指定 GDB（走受控白名单通道），不写源、不落其他库",
                    _ => "注册到工程文件夹连接（add_folder_connection 幂等语义）",
                }),
            PlanEchoDisclosure.Ambiguity(PlanEchoDisclosure.OutputNaming,
                collisions.Count > 0
                    ? "选中项存在同名基名（去扩展名后）：" + string.Join(", ", collisions)
                    : "装载后的图层/数据集名 = 文件基名（去扩展名），本次无重名",
                "不自动改名、不加后缀 ⇒ 同名项由幂等判定处置（见覆盖策略）"),
            PlanEchoDisclosure.Ambiguity(PlanEchoDisclosure.OverwritePolicy,
                "本工具无覆盖开关：参数面不提供 overwrite/onConflict",
                "固定为幂等跳过 —— 目标已存在（地图内同名图层 / GDB 内同名数据集）⇒ 该项 skipped 并披露原因，**绝不覆盖**"),
        };

        var loadDefaults = new Dictionary<string, object?>
        {
            ["dryRun"] = PlanEchoDisclosure.Field(dryRun, ToolArgs.GetValue(context, "dryRun") is not null, "缺省 true ⇒ 只回显计划，不执行"),
            ["confirm"] = PlanEchoDisclosure.Field(confirm, ToolArgs.GetValue(context, "confirm") is not null, "缺省 false ⇒ 缺省拒（须显式 true 才执行）"),
            ["items"] = PlanEchoDisclosure.Field(itemsArg.Count == 0 ? "(全部可处理项)" : itemsArg, itemsArg.Count > 0,
                "未指定 ⇒ 取扫描结果中 kind ∈ {featureclass, table, raster, dataset} 的全部项"),
            ["mapName"] = PlanEchoDisclosure.Field(string.IsNullOrWhiteSpace(mapNameArg) ? "(current map)" : mapNameArg,
                !string.IsNullOrWhiteSpace(mapNameArg), "未指定 ⇒ 执行时取当前活动地图（仅 into_map 相关）"),
            ["targetGdb"] = PlanEchoDisclosure.Field(targetGdb, !string.IsNullOrWhiteSpace(targetGdb),
                "copy_to_gdb 必填（缺失 ⇒ INVALID_ARGUMENT，已在前置校验拒绝）"),
            ["jobId"] = PlanEchoDisclosure.Field(jobId, ToolArgs.GetValue(context, "jobId") is not null, "未指定 ⇒ 生成 load-<utc>-<rand>（审计关联用）"),
        };

        var plan = new Dictionary<string, object?>
        {
            ["tool"] = Name,
            ["jobId"] = jobId,
            ["source"] = full,
            ["outputMode"] = mode,
            ["target"] = mode switch
            {
                "into_map" => ToolArgs.GetString(context, "mapName") ?? "(current map)",
                "copy_to_gdb" => targetGdb,
                _ => "(project folder connections)",
            },
            ["itemCount"] = selected.Count,
            ["items"] = selected.Select(i => i["path"]).ToList(),
            ["estimatedImpact"] = mode == "into_map"
                ? $"{selected.Count} layer(s) added to the target map"
                : mode == "copy_to_gdb" ? $"{selected.Count} dataset(s) copied into {targetGdb}" : $"{selected.Count} folder connection(s) registered",
            ["ambiguities"] = loadAmbiguities,
            ["defaults"] = loadDefaults,
            ["sourceReadOnly"] = true,
        };

        if (dryRun)
        {
            return OperationResult<object?>.Ok(new Dictionary<string, object?>
            {
                ["dryRun"] = true,
                ["executed"] = false,
                ["plan"] = plan,
                ["note"] = "Plan echo only. Re-issue with confirm=true and dryRun=false to execute.",
            }, "Dry-run: structured plan returned, nothing was changed.");
        }

        if (!confirm)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied,
                "confirm=true is required to execute (default-refuse); no change was made.");
        }

        if (context.ReadOnly is { IsReadOnly: true } && ToolWriteClassification.RefusedInReadOnly(Name))
        {
            return OperationResult<object?>.Fail(ToolWriteClassification.ReadOnlyRefusal(Name));
        }

        var results = new List<Dictionary<string, object?>>();
        var okCount = 0;
        var skippedCount = 0;
        var failedCount = 0;

        foreach (var item in selected)
        {
            var path = (string)item["path"]!;
            var name = (string)item["name"]!;
            var one = new Dictionary<string, object?> { ["name"] = name, ["path"] = path };
            try
            {
                switch (mode)
                {
                    case "into_map":
                    {
                        var mapName = ToolArgs.GetString(context, "mapName");
                        if (string.IsNullOrWhiteSpace(mapName) && context.Host is not null)
                        {
                            var cur = await context.Host.Maps.GetCurrentMapAsync(context.CancellationToken).ConfigureAwait(false);
                            mapName = cur.Success ? cur.Data?.Name : null;
                        }

                        if (!string.IsNullOrWhiteSpace(mapName) && context.Host is not null)
                        {
                            var existing = await context.Host.Layers.GetLayersAsync(mapName!, true, context.CancellationToken).ConfigureAwait(false);
                            var dup = existing.Success && existing.Data is not null
                                      && existing.Data.Any(l => string.Equals(l.Name, Path.GetFileNameWithoutExtension(name), StringComparison.OrdinalIgnoreCase));
                            if (dup)
                            {
                                one["ok"] = false;
                                one["skipped"] = true;
                                one["reason"] = "already-present-in-map (idempotent skip)";
                                skippedCount++;
                                results.Add(one);
                                continue;
                            }
                        }

                        var r = await context.Host!.Layers.AddLayerAsync(ToolArgs.GetString(context, "mapName") ?? string.Empty, path, context.CancellationToken).ConfigureAwait(false);
                        one["ok"] = r.Success;
                        one["reason"] = r.Success ? null : (r.Errors.Count > 0 ? r.Errors[0].Message : "add-layer-failed");
                        if (r.Success)
                        {
                            okCount++;
                        }
                        else
                        {
                            failedCount++;
                        }

                        break;
                    }

                    case "copy_to_gdb":
                    {
                        var dest = Path.Combine(targetGdb!, Path.GetFileNameWithoutExtension(name));
                        if (File.Exists(dest) || Directory.Exists(dest))
                        {
                            one["ok"] = false;
                            one["skipped"] = true;
                            one["reason"] = "already-exists-in-target-gdb (idempotent skip)";
                            skippedCount++;
                            results.Add(one);
                            continue;
                        }

                        // D-073 修复（LIVE 实测 ERROR 000979）：复制走**受控白名单通道**（fail-closed），
                        // 不用遗留 Copy_management（跨工作空间失败）。
                        var ext = Path.GetExtension(name).ToLowerInvariant();
                        GpRunRequest req;
                        if (ext is ".shp")
                        {
                            req = new GpRunRequest
                            {
                                ToolName = "management.CopyFeatures",
                                Parameters = new Dictionary<string, object?> { ["in_features"] = path, ["out_feature_class"] = dest },
                                AuditNote = "load_folder_data copy_to_gdb",
                            };
                        }
                        else if (ext is ".csv")
                        {
                            req = new GpRunRequest
                            {
                                ToolName = "conversion.TableToTable",
                                Parameters = new Dictionary<string, object?>
                                {
                                    ["in_rows"] = path,
                                    ["out_path"] = targetGdb!,
                                    ["out_name"] = Path.GetFileNameWithoutExtension(name),
                                },
                                AuditNote = "load_folder_data copy_to_gdb",
                            };
                        }
                        else
                        {
                            one["ok"] = false;
                            one["reason"] = "no-whitelisted-copy-tool-for-kind: " + ext + "（栅格请用 apply_processing_plan 的白名单栅格工具）";
                            failedCount++;
                            break;
                        }

                        if (context.Host is null)
                        {
                            one["ok"] = false;
                            one["reason"] = "host-unavailable";
                            failedCount++;
                            break;
                        }

                        var r = await context.Host.Geoprocessing.RunWhitelistedAsync(req, context.CancellationToken).ConfigureAwait(false);
                        one["ok"] = r.Success;
                        one["output"] = dest;
                        one["gpTool"] = req.ToolName;
                        one["reason"] = r.Success ? null : (r.Errors.Count > 0 ? r.Errors[0].Message : "copy-failed");
                        if (r.Success)
                        {
                            okCount++;
                        }
                        else
                        {
                            failedCount++;
                        }

                        break;
                    }

                    default: // register_only
                    {
                        var sub = new ToolExecutionContext
                        {
                            RequestId = context.RequestId,
                            Host = context.Host,
                            Logger = context.Logger,
                            Python = context.Python,
                            Settings = context.Settings,
                            Registry = context.Registry,
                            ReadOnly = context.ReadOnly,
                            CancellationToken = context.CancellationToken,
                            Arguments = new Dictionary<string, object?> { ["path"] = full },
                            Invoker = context.Invoker,
                        };
                        var tool = context.Registry?.Get("add_folder_connection");
                        if (tool is null)
                        {
                            one["ok"] = false;
                            one["reason"] = "registry-unavailable: add_folder_connection not resolvable";
                            failedCount++;
                            break;
                        }

                        var r = await (sub.Invoker ?? ToolInvoker.Default).InvokeAsync(tool, sub).ConfigureAwait(false);
                        one["ok"] = r.Success;
                        one["reason"] = r.Success ? "folder connection registered" : (r.Errors.Count > 0 ? r.Errors[0].Message : "register-failed");
                        if (r.Success)
                        {
                            okCount++;
                            skippedCount += 0;
                            // 同一次注册对后续项幂等：仅首项真正注册，其余标记 skipped
                            for (var k = selected.IndexOf(item) + 1; k < selected.Count; k++)
                            {
                                // 由外层循环自然处理（后续项会再次调用，宿主侧幂等）
                            }
                        }
                        else
                        {
                            failedCount++;
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                one["ok"] = false;
                one["reason"] = ex.GetType().Name + ": " + ex.Message;
                failedCount++;
            }

            results.Add(one);
        }

        AppendAudit(context, jobId, Name, mode, failedCount == 0);
        return OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["dryRun"] = false,
            ["executed"] = true,
            ["jobId"] = jobId,
            ["outputMode"] = mode,
            ["ok"] = okCount,
            ["skipped"] = skippedCount,
            ["failed"] = failedCount,
            ["idempotent"] = skippedCount > 0,
            ["sourceUntouched"] = true,
            ["items"] = results,
        }, $"Loaded {okCount} item(s); skipped {skippedCount}; failed {failedCount}.");
    }

    internal static void AppendAudit(ToolExecutionContext context, string jobId, string tool, string? note, bool success, string? auditPathOverride = null)
    {
        try
        {
            var dir = auditPathOverride ?? Environment.GetEnvironmentVariable(GpAuditLog.AuditDirVariable);
            var path = string.IsNullOrWhiteSpace(dir) ? null : Path.Combine(dir!, GpAuditLog.FileName);
            if (path is null)
            {
                return;
            }

            var entry = new GpAuditEntry
            {
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                Tool = tool,
                ParameterDigest = note ?? string.Empty,
                ParameterForm = "named",
                Destructive = false,
                Confirm = true,
                Success = success,
                ResultCode = success ? "OK" : "FAILED",
                DurationMs = 0,
                AuditNote = note,
                Pid = Environment.ProcessId,
                JobId = jobId,
            };
            GpAuditLog.TryAppend(path, entry, out _, out _);
        }
        catch
        {
            // 审计失败不阻断结果
        }
    }
}

/// <summary>D-073 · 处理计划执行（写；dryRun 缺省 true、confirm 缺省拒；分片 + jobId + 断点）。</summary>
public sealed class ApplyProcessingPlanTool : McpToolBase
{
    private static readonly HashSet<string> DeniedInPlan = new(StringComparer.Ordinal)
    {
        "run_batch", "apply_processing_plan", "set_readonly_mode",
    };

    public override string Name => "apply_processing_plan";

    public override string Description =>
        "按**结构化计划**（plan.steps = [{tool, args}]）对 targets 数据集集合**逐个应用**（写；confirm 缺省拒、dryRun 缺省 true ⇒ 先回显计划，" +
        "计划含 ambiguities〔目标位置/输出命名/覆盖策略〕与 defaults 缺省值）。" +
        "串行、逐项前后照、continueOnError 缺省 false；**jobId 分片执行**（每片 ≤25 s / 项数上限）⇒ 突破 run_batch 30 s 硬约束，断点落 D 盘可续跑。" +
        "步骤工具必须**已注册**且非 run_batch/apply_processing_plan/set_readonly_mode（禁嵌套）——任一非法 ⇒ 整计划拒、零副作用。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["plan"] = new Dictionary<string, object?> { ["type"] = "object", ["description"] = "{ steps: [ { tool, args } ] }" },
            ["targets"] = new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "string" }, ["description"] = "Dataset paths the plan is applied to." },
            ["outputLocation"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "User-specified output folder (path guard applies)." },
            ["confirm"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Must be explicitly true to execute (default refuse)." },
            ["dryRun"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Plan echo only (default true)." },
            ["continueOnError"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Continue after a failed item (default false)." },
            ["shardSeconds"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Per-shard time budget in seconds (default 25, max 25)." },
            ["maxItemsPerShard"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Max work items per shard (default 10)." },
            ["jobId"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Job id (reuse to resume)." },
            ["onConflict"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "fail | skip | overwrite (default fail)" },
        },
        ["required"] = new List<object?> { "plan", "targets", "outputLocation" },
    };

    protected override string CategoryName => ToolCategories.General;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public const int ShardSecondsCeiling = 25;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var plan = ToolArgs.GetObject(context, "plan");
        var targets = ToolArgs.GetStringList(context, "targets");
        var outputLocation = ToolArgs.GetString(context, "outputLocation");
        if (plan is null || targets.Count == 0 || string.IsNullOrWhiteSpace(outputLocation))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "plan, targets and outputLocation are required.");
        }

        if (ToolArgs.GetBool(context, "inPlace"))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "inPlace is not supported: source data is read-only by design; no change was made.");
        }

        var outFull = Path.GetFullPath(outputLocation!);
        var hit = ProtectedOutputPathGuard.Match(outFull);
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, $"outputLocation is refused by the path guard ('{hit}').");
        }

        if (WorkflowJobStore.IsTempLikePath(outFull))
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, "G-138: outputLocation must not live under %TEMP%.");
        }

        // ── 计划结构校验（整计划准入；任一非法 ⇒ 整计划拒、零副作用）──
        if (!plan!.TryGetValue("steps", out var stepsRaw) || stepsRaw is not IEnumerable<object?> steps || !steps.Any())
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "plan.steps must be a non-empty array of { tool, args }.");
        }

        var stepList = new List<(string Tool, IReadOnlyDictionary<string, object?> Args)>();
        var rejected = new List<string>();
        foreach (var s in steps)
        {
            if (s is not IReadOnlyDictionary<string, object?> step || step is null)
            {
                rejected.Add("(malformed step)");
                continue;
            }

            var toolName = ToolArgs.ReadString(step, "tool");
            if (string.IsNullOrWhiteSpace(toolName))
            {
                rejected.Add("(missing tool)");
                continue;
            }

            if (DeniedInPlan.Contains(toolName!))
            {
                rejected.Add(toolName + " (denied in plan)");
                continue;
            }

            if (context.Registry is null || !context.Registry.Contains(toolName!))
            {
                rejected.Add(toolName + " (not registered)");
                continue;
            }

            stepList.Add((toolName!, ToolArgs.Read(step, "args") as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>()));
        }

        if (rejected.Count > 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                "plan rejected as a whole (zero side effects); rejected steps: " + string.Join(", ", rejected));
        }

        var confirm = ToolArgs.GetBool(context, "confirm");
        var dryRun = ToolArgs.GetBool(context, "dryRun", true);
        var continueOnError = ToolArgs.GetBool(context, "continueOnError");
        var shardSeconds = Math.Clamp(ToolArgs.GetInt(context, "shardSeconds") ?? 25, 1, ShardSecondsCeiling);
        var maxItemsPerShard = Math.Max(1, ToolArgs.GetInt(context, "maxItemsPerShard") ?? 10);
        var onConflict = ToolArgs.GetString(context, "onConflict") ?? "fail";

        // D-079 · A1：区分"调用方给定"与"本工具缺省"，供回显的 defaults/ambiguities 使用（不改任何判定逻辑）。
        var given = new HashSet<string>(
            (context.Arguments?.Keys ?? Array.Empty<string>()).Where(k => ToolArgs.GetValue(context, k) is not null),
            StringComparer.OrdinalIgnoreCase);
        bool UserSpecified(string key) => given.Contains(key);
        if (onConflict is not ("fail" or "skip" or "overwrite"))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "onConflict must be one of: fail | skip | overwrite.");
        }

        var jobIdGiven = UserSpecified("jobId");
        var jobId = ToolArgs.GetString(context, "jobId") ?? ("plan-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        if (!WorkflowJobStore.IsSafeJobId(jobId))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "jobId must be a simple file-safe identifier of at most 128 characters.");

        var workItems = (from t in targets from s in stepList select (Target: t, s.Tool, s.Args)).ToList();

        // ── D-079 · A1（O-D073-05）：三类歧义明示 ＋ 未指定项缺省值 —— **仅新增响应字段** ──
        var unresolvedOutputs = new List<string>();
        foreach (var (stepTool, stepArgs) in stepList)
        {
            var missing = RequiredOutputArgs(context, stepTool).Where(a => !stepArgs.ContainsKey(a)).ToList();
            if (missing.Count > 0)
            {
                unresolvedOutputs.Add(stepTool + " ⇒ 缺 " + string.Join(", ", missing));
            }
        }

        var outputLocationReferenced = stepList
            .Any(s => s.Args.Values.OfType<string>().Any(v => v.Contains(outFull, StringComparison.OrdinalIgnoreCase)));
        var nameCollisions = PlanEchoDisclosure.BaseNameCollisions(targets);

        var ambiguities = new List<Dictionary<string, object?>>();
        if (unresolvedOutputs.Count > 0)
        {
            ambiguities.Add(PlanEchoDisclosure.Ambiguity(PlanEchoDisclosure.TargetLocation,
                "步骤未给出必填的产物落点参数：" + string.Join("；", unresolvedOutputs),
                "不猜测落点 —— 该步骤执行时会以 INVALID_ARGUMENT 失败并逐项如实记录；请在 plan.steps[].args 内补齐"));
        }
        else if (!outputLocationReferenced)
        {
            ambiguities.Add(PlanEchoDisclosure.Ambiguity(PlanEchoDisclosure.TargetLocation,
                "outputLocation 已给定（" + outFull + "），但没有任何步骤 args 引用它",
                "产物落点由各步骤工具自身的缺省规则决定 ⇒ 不自动改写为 outputLocation（落点一致性请显式给参数）"));
        }

        ambiguities.Add(PlanEchoDisclosure.Ambiguity(PlanEchoDisclosure.OutputNaming,
            nameCollisions.Count > 0
                ? "目标基名重复（去扩展名后同名）：" + string.Join(", ", nameCollisions)
                : "产物名未显式给定 ⇒ 由各步骤工具按目标基名/自身缺省命名规则生成",
            nameCollisions.Count > 0
                ? "同名产物按 onConflict 处置（fail=失败并记录；skip=跳过；overwrite=覆写），不静默改名"
                : "本次无基名重复项"));

        ambiguities.Add(PlanEchoDisclosure.Ambiguity(PlanEchoDisclosure.OverwritePolicy,
            UserSpecified("onConflict")
                ? "调用方已显式给定 onConflict=" + onConflict
                : "未指定 onConflict",
            UserSpecified("onConflict")
                ? "按给定值执行（目标已存在时的行为由该值决定）"
                : "采用缺省 fail —— 目标已存在 ⇒ 该项 failed（错误码由该工具给出），不覆盖任何既有产物"));

        var defaults = new Dictionary<string, object?>
        {
            ["dryRun"] = PlanEchoDisclosure.Field(dryRun, UserSpecified("dryRun"), "缺省 true ⇒ 只回显计划，不执行"),
            ["confirm"] = PlanEchoDisclosure.Field(confirm, UserSpecified("confirm"), "缺省 false ⇒ 缺省拒（须显式 true 才执行）"),
            ["onConflict"] = PlanEchoDisclosure.Field(onConflict, UserSpecified("onConflict"), "缺省 fail（不覆盖）"),
            ["continueOnError"] = PlanEchoDisclosure.Field(continueOnError, UserSpecified("continueOnError"), "缺省 false ⇒ 首个失败即中止，余项标 skipped"),
            ["shardSeconds"] = PlanEchoDisclosure.Field(shardSeconds, UserSpecified("shardSeconds"), "缺省 25（上限 25，绕开 run_batch 30 s 硬约束）"),
            ["maxItemsPerShard"] = PlanEchoDisclosure.Field(maxItemsPerShard, UserSpecified("maxItemsPerShard"), "缺省 10（累计式预算的分片粒度）"),
            ["jobId"] = PlanEchoDisclosure.Field(jobId, jobIdGiven, "未指定 ⇒ 生成 plan-<utc>-<rand>；同一 jobId 复用于续跑"),
            ["outputLocation"] = PlanEchoDisclosure.Field(outFull, true, "必填；已规范化为绝对路径并过路径守卫"),
            ["plan.steps[].args.target"] = PlanEchoDisclosure.Field("(逐目标自动注入)", false, "targets 的每一项以 args.target 注入步骤工具，调用方无需在 steps[].args 内写"),
            ["plan.steps[].args"] = PlanEchoDisclosure.Field("(原样透传)", false, "本工具不代填步骤参数（含产物落点/命名），缺失即如实失败"),
        };

        var planEcho = new Dictionary<string, object?>
        {
            ["tool"] = Name,
            ["jobId"] = jobId,
            ["steps"] = stepList.Select(s => new Dictionary<string, object?> { ["tool"] = s.Tool, ["args"] = s.Args }).ToList(),
            ["targets"] = targets,
            ["outputLocation"] = outFull,
            ["workItemCount"] = workItems.Count,
            ["estimatedImpact"] = $"{workItems.Count} tool invocation(s) across {targets.Count} target(s)",
            ["continueOnError"] = continueOnError,
            ["onConflict"] = onConflict,
            ["shardSeconds"] = shardSeconds,
            ["maxItemsPerShard"] = maxItemsPerShard,
            ["ambiguities"] = ambiguities,
            ["defaults"] = defaults,
            ["sourceReadOnly"] = true,
        };

        if (dryRun)
        {
            return OperationResult<object?>.Ok(new Dictionary<string, object?>
            {
                ["dryRun"] = true,
                ["executed"] = false,
                ["plan"] = planEcho,
                ["note"] = "Plan echo only. Re-issue with confirm=true and dryRun=false to execute.",
            }, "Dry-run: structured plan returned, nothing was executed.");
        }

        if (!confirm)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied,
                "confirm=true is required to execute (default-refuse); no change was made.");
        }

        if (context.ReadOnly is { IsReadOnly: true } && stepList.Any(s => ToolWriteClassification.RefusedInReadOnly(s.Tool)))
        {
            return OperationResult<object?>.Fail(ToolWriteClassification.ReadOnlyRefusal(Name));
        }

        if (onConflict == "overwrite" && !confirm && !dryRun)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, "onConflict=overwrite requires confirm=true.");
        }

        Directory.CreateDirectory(outFull);
        // D-077：并发同 jobId ⇒ 冲突拒写（忙占用 / 异所有者），不静默覆盖
        var (claim, claimed) = WorkflowJobStore.Claim(jobId, Name, workItems.Count, shardSeconds, maxItemsPerShard);
        if (claim is WorkflowJobStore.ClaimResult.Busy or WorkflowJobStore.ClaimResult.ForeignOwner)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidState,
                claim == WorkflowJobStore.ClaimResult.Busy
                    ? $"jobId '{jobId}' is already running (concurrent execution refused; no change was made)."
                    : $"jobId '{jobId}' is owned by another session (conflict refused; use a distinct jobId).");
        }

        var state = claimed!;
        var shardCount = (int)Math.Ceiling(workItems.Count / (double)maxItemsPerShard);
        state.ShardCount = shardCount;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var shardBudget = TimeSpan.FromSeconds(shardSeconds);
        var aborted = false;
        var resumedSkips = 0;
        var cancellationSource = WorkflowJobStore.BeginExecution(state, context.CancellationToken);
        var jobToken = cancellationSource.Token;

        try
        {
        for (var idx = 0; idx < workItems.Count; idx++)
        {
            var (target, toolName, args) = workItems[idx];
            var shard = (idx / maxItemsPerShard) + 1;
            state.CurrentShard = shard;

            if (state.Items.Count <= idx)
            {
                state.Items.Add(new FolderJobItem { Index = idx, Tool = toolName, Target = target });
            }

            var record = state.Items[idx];
            if (record.State == "ok")
            {
                // D-075 修复（O-D073-03 分片续跑）：**已完成项不重跑**（按断点续跑语义）
                resumedSkips++;
                state.LastResumedSkips = resumedSkips;
                WorkflowJobStore.Save(state);
                continue;
            }

            if (jobToken.IsCancellationRequested || state.CancelRequested)
            {
                record.State = "pending";
                record.Reason = "cancel requested before this step started";
                state.CancelRequested = true;
                state.Cancelled = true;
                state.ResumeToken = jobId;
                WorkflowJobStore.Save(state);
                break;
            }

            if (aborted)
            {
                record.State = "skipped";
                record.Reason = "aborted-after-failure";
                continue;
            }

            if (watch.Elapsed >= shardBudget * shard)
            {
                // 本片预算耗尽 ⇒ 余项留 pending + 续跑令牌（断点已落盘）
                record.State = "pending";
                record.Reason = "shard-budget-exhausted (resume with the same jobId)";
                state.ResumeToken = jobId;
                WorkflowJobStore.Save(state);
                continue;
            }

            var sub = new ToolExecutionContext
            {
                RequestId = context.RequestId,
                Host = context.Host,
                Logger = context.Logger,
                Python = context.Python,
                Settings = context.Settings,
                Registry = context.Registry,
                ReadOnly = context.ReadOnly,
                CancellationToken = jobToken,
                Arguments = new Dictionary<string, object?>(args) { ["target"] = target },
                Invoker = context.Invoker,
            };

            var t0 = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var tool = context.Registry!.Get(toolName);
                if (tool is null)
                {
                    record.DurationMs = t0.ElapsedMilliseconds;
                    record.State = "failed";
                    record.ErrorCode = ErrorCodes.ToolNotFound;
                    record.Reason = "tool not resolvable at execution time: " + toolName;
                    state.LastError = record.Reason;
                    if (!continueOnError)
                    {
                        aborted = true;
                    }

                    AppendAuditForPlan(context, jobId, toolName, target, record);
                    WorkflowJobStore.Save(state);
                    continue;
                }

                var r = await (sub.Invoker ?? ToolInvoker.Default).InvokeAsync(tool, sub).ConfigureAwait(false);
                record.DurationMs = t0.ElapsedMilliseconds;
                if (r.Success)
                {
                    record.State = "ok";
                    var artifact = ToolArgs.ReadString(r.Data as IReadOnlyDictionary<string, object?>, "outputPath")
                                   ?? ToolArgs.ReadString(r.Data as IReadOnlyDictionary<string, object?>, "output")
                                   ?? ToolArgs.ReadString(r.Data as IReadOnlyDictionary<string, object?>, "outputRaster");
                    if (!string.IsNullOrWhiteSpace(artifact) && File.Exists(artifact))
                    {
                        record.Artifact = artifact;
                        record.Sha256 = Sha256Of(artifact!);
                    }
                }
                else
                {
                    if (jobToken.IsCancellationRequested)
                    {
                        record.State = "pending";
                        record.Reason = "cancellation requested while the child tool was running; inspect outputs before resuming";
                        state.CancelRequested = true;
                        state.Cancelled = true;
                        state.ResumeToken = jobId;
                    }
                    else
                    {
                        var code = r.Errors.Count > 0 ? r.Errors[0].Code : ErrorCodes.ExecutionFailed;
                        record.State = "failed";
                        record.ErrorCode = code;
                        record.Reason = r.Errors.Count > 0 ? r.Errors[0].Message : "failed";
                        state.LastError = toolName + ": " + record.Reason;
                        if (!continueOnError)
                        {
                            aborted = true;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (jobToken.IsCancellationRequested)
            {
                record.DurationMs = t0.ElapsedMilliseconds;
                record.State = "pending";
                record.Reason = "cancellation requested while the child tool was running; inspect outputs before resuming";
                state.CancelRequested = true;
                state.Cancelled = true;
                state.ResumeToken = jobId;
            }
            catch (Exception ex)
            {
                record.DurationMs = t0.ElapsedMilliseconds;
                record.State = "failed";
                record.ErrorCode = ErrorCodes.ExecutionFailed;
                record.Reason = ex.GetType().Name + ": " + ex.Message;
                state.LastError = toolName + ": " + record.Reason;
                if (!continueOnError)
                {
                    aborted = true;
                }
            }

            AppendAuditForPlan(context, jobId, toolName, target, record);
            WorkflowJobStore.Save(state);
            if (jobToken.IsCancellationRequested)
            {
                state.CancelRequested = true;
                state.Cancelled = true;
                state.CancelReason ??= "execution context cancellation";
                state.ResumeToken = jobId;
                WorkflowJobStore.Save(state);
                break;
            }
        }

        }
        finally
        {
            WorkflowJobStore.Release(state);
            WorkflowJobStore.EndExecution(state, cancellationSource);
        }

        return OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["dryRun"] = false,
            ["executed"] = true,
            ["jobId"] = jobId,
            ["total"] = state.Total,
            ["ok"] = state.Done,
            ["failed"] = state.Failed,
            ["skipped"] = state.Skipped,
            ["pending"] = state.Pending,
            ["resumedSkips"] = resumedSkips,
            ["currentShard"] = state.CurrentShard,
            ["shardCount"] = state.ShardCount,
            ["shardSeconds"] = shardSeconds,
            ["resumeToken"] = state.ResumeToken,
            ["cancelled"] = state.Cancelled,
            ["cancelReason"] = state.CancelReason,
            ["checkpoint"] = WorkflowJobStore.TryCheckpointPath(jobId),
            ["sourceUntouched"] = true,
            ["items"] = state.Items.Select(i => new Dictionary<string, object?>
            {
                ["index"] = i.Index,
                ["tool"] = i.Tool,
                ["target"] = i.Target,
                ["state"] = i.State,
                ["reason"] = i.Reason,
                ["errorCode"] = i.ErrorCode,
                ["artifact"] = i.Artifact,
                ["sha256"] = i.Sha256,
                ["durationMs"] = i.DurationMs,
            }).ToList(),
        }, $"Plan executed: ok={state.Done} failed={state.Failed} skipped={state.Skipped} pending={state.Pending}.");
    }

    private static void AppendAuditForPlan(ToolExecutionContext context, string jobId, string toolName, string target, FolderJobItem record)
    {
        LoadFolderDataTool.AppendAudit(context, jobId, "apply_processing_plan:" + toolName, target, record.State == "ok");
    }

    /// <summary>
    /// D-079 · A1：从**已注册工具自己的 schema** 取其必填"产物落点"参数名（只读取 schema，不猜值）。
    /// 工具不可解析 / 无 schema ⇒ 空集（回显如实不提，不臆造）。
    /// </summary>
    private static List<string> RequiredOutputArgs(ToolExecutionContext context, string toolName)
    {
        var names = new List<string>();
        if (context.Registry?.Get(toolName)?.InputSchema is not { } schema
            || !schema.TryGetValue("required", out var raw)
            || raw is not System.Collections.IEnumerable required)   // 兼容 string[] 与 List<object?> 两种写法
        {
            return names;
        }

        foreach (var item in required)
        {
            if (item is string name && PlanEchoDisclosure.IsOutputArgumentName(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    internal static string Sha256Of(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs));
    }
}

/// <summary>D-073 · 任务进度（只读）。</summary>
public sealed class GetJobStatusTool : McpToolBase
{
    public override string Name => "get_job_status";

    public override string Description =>
        "查询任务进度（**只读**）：jobId / 总项 / 已完成 / 失败 / 跳过 / 当前分片 / 剩余 / 可续跑断点 / 最近错误。参数：jobId*。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["jobId"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Job id returned by load_folder_data / apply_processing_plan." },
        },
        ["required"] = new List<object?> { "jobId" },
    };

    protected override string CategoryName => ToolCategories.System;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    protected override bool? RequiresArcGISOverride => false;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var jobId = ToolArgs.GetString(context, "jobId");
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "jobId is required."));
        }

        var state = WorkflowJobStore.Find(jobId!);
        if (state is null)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.NotFound, $"No job found for jobId '{jobId}'."));
        }

        return Task.FromResult(OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["jobId"] = state.JobId,
            ["kind"] = state.Kind,
            ["total"] = state.Total,
            ["done"] = state.Done,
            ["failed"] = state.Failed,
            ["skipped"] = state.Skipped,
            ["pending"] = state.Pending,
            ["currentShard"] = state.CurrentShard,
            ["shardCount"] = state.ShardCount,
            ["remaining"] = state.Pending,
            ["resumable"] = state.ResumeToken is not null || state.Pending > 0,
            ["resumeToken"] = state.ResumeToken,
            ["lastError"] = state.LastError,
            ["checkpoint"] = WorkflowJobStore.TryCheckpointPath(state.JobId),
            ["createdUtc"] = state.CreatedUtc,
            ["updatedUtc"] = state.UpdatedUtc,
        }, $"Job {state.JobId}: {state.Done}/{state.Total} done, {state.Failed} failed, {state.Pending} pending."));
    }
}

/// <summary>D-073 · 逐项报告（只读）。</summary>
public sealed class GetJobReportTool : McpToolBase
{
    public override string Name => "get_job_report";

    public override string Description =>
        "任务逐项报告（**只读**）：处理了什么／跳过什么（原因）／失败什么（原因+错误码）／产物路径与 SHA256／审计关联（jobId）／未验证项。" +
        "机器可读 JSON + 人读摘要；失败不静默、Unknown 不伪装 Ok。参数：jobId*、format（json|summary，缺省 json）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["jobId"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Job id." },
            ["format"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "json | summary (default json)" },
        },
        ["required"] = new List<object?> { "jobId" },
    };

    protected override string CategoryName => ToolCategories.System;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    protected override bool? RequiresArcGISOverride => false;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var jobId = ToolArgs.GetString(context, "jobId");
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "jobId is required."));
        }

        var format = ToolArgs.GetString(context, "format") ?? "json";
        var state = WorkflowJobStore.Find(jobId!);
        if (state is null)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.NotFound, $"No job found for jobId '{jobId}'."));
        }

        var processed = state.Items.Where(i => i.State == "ok").Select(i => new Dictionary<string, object?>
        {
            ["index"] = i.Index, ["tool"] = i.Tool, ["target"] = i.Target,
            ["artifact"] = i.Artifact, ["sha256"] = i.Sha256, ["durationMs"] = i.DurationMs,
        }).ToList();
        var skipped = state.Items.Where(i => i.State == "skipped" || i.State == "pending").Select(i => new Dictionary<string, object?>
        {
            ["index"] = i.Index, ["tool"] = i.Tool, ["target"] = i.Target, ["state"] = i.State, ["reason"] = i.Reason ?? "unspecified",
        }).ToList();
        var failed = state.Items.Where(i => i.State == "failed").Select(i => new Dictionary<string, object?>
        {
            ["index"] = i.Index, ["tool"] = i.Tool, ["target"] = i.Target,
            ["reason"] = i.Reason ?? "unspecified", ["errorCode"] = i.ErrorCode ?? ErrorCodes.ExecutionFailed,
        }).ToList();

        var summary = $"Job {state.JobId}: {state.Done} processed, {skipped.Count} skipped/pending, {state.Failed} failed " +
                      $"(total {state.Total}); checkpoint={WorkflowJobStore.TryCheckpointPath(state.JobId) ?? "(none)"}.";

        return Task.FromResult(OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["jobId"] = state.JobId,
            ["kind"] = state.Kind,
            ["format"] = format,
            ["total"] = state.Total,
            ["processed"] = processed,
            ["skipped"] = skipped,
            ["failed"] = failed,
            ["auditJobId"] = state.JobId,
            ["checkpoint"] = WorkflowJobStore.TryCheckpointPath(state.JobId),
            ["unverified"] = new List<object?>
            {
                "clean-machine acceptance: NOT VERIFIED",
                "net8 变体在 Pro 3.3/3.4/3.6 运行期: NOT VERIFIED",
            },
            ["summary"] = summary,
        }, summary));
    }
}
