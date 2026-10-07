using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

// ══════════════════════════════════════════════════════════════════════════════
// D-064 · C 段：数据发现（4 件）
//   search_data（只读） / list_folder（只读；路径守卫 + 深度/数量上限，防遍历）
//   add_folder_connection（写：工程注册变更） / get_project_items（只读）
// 错误码零新增。
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>D-064 · 按名检索数据集（工程 home / 默认 GDB / 文件夹连接；只读）。</summary>
public sealed class SearchDataTool : McpToolBase
{
    public override string Name => "search_data";

    public override string Description =>
        "在工程 home 目录、默认地理数据库与已注册的文件夹连接中按名检索数据集（**只读**）。参数：" +
        "pattern（子串，大小写不敏感；省略 = 全部）、typeFilter（可选：FeatureClass/Table/Raster/Folder/Toolbox）、topN（缺省 50，上限 500）。" +
        "契约：结果超出 topN → truncated=true 且 matchCount 为**真实命中数**（不谎报）；返回实际检索的根清单（searchedRoots）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["pattern"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Name substring (case-insensitive); omit for all." },
            ["typeFilter"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "FeatureClass | Table | Raster | Folder | Toolbox (optional)." },
            ["topN"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Max items returned (default 50, max 500)." },
        },
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    /// <summary>topN 上限（工具层钳制，防"一次拉全库"）。</summary>
    public const int MaxTopN = 500;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var topN = ToolArgs.GetInt(context, "topN") ?? 50;
        if (topN <= 0)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "topN must be > 0.");
        }

        if (topN > MaxTopN)
        {
            topN = MaxTopN;
        }

        var r = await context.Host.Project.SearchDataAsync(
            ToolArgs.GetString(context, "pattern"), ToolArgs.GetString(context, "typeFilter"), topN,
            context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · 磁盘目录枚举（只读；路径守卫 + 深度/数量双上限，防遍历）。</summary>
public sealed class ListFolderTool : McpToolBase
{
    public override string Name => "list_folder";

    public override string Description =>
        "枚举磁盘目录下的 GIS 文件与子目录（**只读**）。参数：path（**绝对路径**）、depth（缺省 1，上限 5）、" +
        "maxEntries（缺省 200，上限 2000）、includeFiles（缺省 true）。" +
        "契约：**受保护根**（TestFixtures／旧仓库／ARCGIS_PRO_MCP_PROTECTED_ROOTS；配置 ARCGIS_PRO_MCP_ALLOWED_ROOTS 后为白名单制）→ PATH_ESCAPE_REJECTED；" +
        "非绝对路径 → INVALID_ARGUMENT；**reparse point/symlink/junction 不穿越**；深度与条数**双上限**，超限 → truncated=true（不静默截断）；" +
        "不可读子目录计入 skippedDirectories（如实披露不掩盖）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["path"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Absolute directory path on disk." },
            ["depth"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Recursion depth (default 1, max 5)." },
            ["maxEntries"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "Max entries returned (default 200, max 2000)." },
            ["includeFiles"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Include files (default true)." },
        },
        ["required"] = new[] { "path" }
    };

    protected override string CategoryName => ToolCategories.DataManagement;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    /// <summary>深度上限（防遍历）。</summary>
    public const int MaxDepth = 5;

    /// <summary>条数上限（防遍历）。</summary>
    public const int MaxEntries = 2000;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var path = ToolArgs.GetString(context, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "path is required."));
        }

        // 绝对路径判定必须**先于** GetFullPath（相对路径会被 CWD 解析成绝对路径，判定形同虚设）。
        if (!Path.IsPathFullyQualified(path!))
        {
            return Task.FromResult(OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, "path must be an absolute path."));
        }

        string full;
        try
        {
            full = Path.GetFullPath(path!);
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, $"path is not a valid path: {ex.Message}"));
        }

        // 路径守卫（复用 D-050/D-055 既有判定：受保护根 + 规范化二次判界 + reparse 拒绝 + 可选白名单）。
        var hit = ProtectedOutputPathGuard.Match(full);
        if (hit is not null)
        {
            return Task.FromResult(OperationResult<object?>.Fail(
                ErrorCodes.PathEscapeRejected,
                $"path '{full}' was rejected by the path guard ('{hit}'); directory listing refused (no traversal performed)."));
        }

        var requestedDepth = ToolArgs.GetInt(context, "depth") ?? 1;
        if (requestedDepth <= 0)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "depth must be > 0."));
        }

        var effectiveDepth = Math.Min(requestedDepth, MaxDepth);

        var requestedMax = ToolArgs.GetInt(context, "maxEntries") ?? 200;
        if (requestedMax <= 0)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxEntries must be > 0."));
        }

        var maxEntries = Math.Min(requestedMax, MaxEntries);
        var includeFiles = ToolArgs.GetBool(context, "includeFiles", true);

        if (!Directory.Exists(full))
        {
            return Task.FromResult(OperationResult<object?>.Fail(
                ErrorCodes.NotFound, $"directory not found: {full}"));
        }

        var entries = new List<FolderEntryInfo>();
        var truncated = false;
        var skipped = 0;

        void Walk(string dir, int level)
        {
            if (truncated || level > effectiveDepth)
            {
                return;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateFileSystemEntries(dir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                skipped++;
                return;
            }

            foreach (var child in children)
            {
                if (truncated)
                {
                    return;
                }

                FileAttributes attrs;
                try
                {
                    attrs = File.GetAttributes(child);
                }
                catch
                {
                    skipped++;
                    continue;
                }

                var isDir = attrs.HasFlag(FileAttributes.Directory);

                // reparse point（symlink/junction）不穿越：目录不递归、仅列出。
                var isReparse = attrs.HasFlag(FileAttributes.ReparsePoint);

                if (!isDir && !includeFiles)
                {
                    continue;
                }

                if (entries.Count >= maxEntries)
                {
                    truncated = true;
                    return;
                }

                entries.Add(new FolderEntryInfo
                {
                    Name = Path.GetFileName(child),
                    FullPath = child,
                    IsDirectory = isDir,
                    Size = isDir ? null : SafeLength(child),
                    Extension = isDir ? null : SafeExtension(child),
                    LastWriteUtc = SafeLastWrite(child),
                });

                if (isDir && !isReparse && level + 1 <= effectiveDepth)
                {
                    Walk(child, level + 1);
                }
            }
        }

        Walk(full, 1);

        var result = new ListFolderResult
        {
            Path = full,
            RequestedDepth = requestedDepth,
            EffectiveDepth = effectiveDepth,
            MaxEntries = maxEntries,
            EntryCount = entries.Count,
            Truncated = truncated,
            SkippedDirectories = skipped,
            Entries = entries,
        };

        return Task.FromResult(OperationResult<object?>.Ok(result));
    }

    private static long? SafeLength(string file)
    {
        try
        {
            return new FileInfo(file).Length;
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeExtension(string file)
    {
        try
        {
            return Path.GetExtension(file);
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeLastWrite(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path).ToString("o");
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>D-064 · 注册文件夹连接（写：工程注册变更）。</summary>
public sealed class AddFolderConnectionTool : McpToolBase
{
    public override string Name => "add_folder_connection";

    public override string Description =>
        "把磁盘文件夹注册为工程文件夹连接（**写：工程注册变更**，可经移除连接复原）。参数：path（**绝对路径**）。" +
        "契约：非绝对路径 → INVALID_ARGUMENT；目录不存在 → NOT_FOUND；**受保护根 → PATH_ESCAPE_REJECTED**（零变更）；" +
        "重复注册 → added=false + alreadyPresent=true（幂等，不算失败）；返回 normalizedPath 与连接总数。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["path"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Absolute folder path to register as a project folder connection." },
        },
        ["required"] = new[] { "path" }
    };

    protected override string CategoryName => ToolCategories.Project;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var path = ToolArgs.GetString(context, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "path is required.");
        }

        if (!Path.IsPathFullyQualified(path!))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "path must be an absolute path.");
        }

        string full;
        try
        {
            full = Path.GetFullPath(path!);
        }
        catch (Exception ex)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"path is not a valid path: {ex.Message}");
        }

        var hit = ProtectedOutputPathGuard.Match(full);
        if (hit is not null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected,
                $"path '{full}' is inside a protected root ('{hit}'); folder connection refused (no change made).");
        }

        if (!Directory.Exists(full))
        {
            return OperationResult<object?>.Fail(ErrorCodes.NotFound, $"directory not found: {full}");
        }

        var r = await context.Host.Project.AddFolderConnectionAsync(full, context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}

/// <summary>D-064 · 工程已注册连接 / 工具箱清单（只读）。</summary>
public sealed class GetProjectItemsTool : McpToolBase
{
    public override string Name => "get_project_items";

    public override string Description =>
        "列出工程已注册的**文件夹连接 / 地理数据库**与**工具箱**（**只读**）。无参数。" +
        "契约：返回 connections / toolboxes 两清单与合计 count；空态如实返回空数组（不伪报）。";

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>(),
    };

    protected override string CategoryName => ToolCategories.Project;

    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        }

        var r = await context.Host.Project.GetProjectItemsAsync(context.CancellationToken).ConfigureAwait(false);
        return ToolResult.From(r);
    }
}
