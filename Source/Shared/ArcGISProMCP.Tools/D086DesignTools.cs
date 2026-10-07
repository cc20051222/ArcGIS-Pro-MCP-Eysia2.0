using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Jobs;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>D-086 B3/B4：GIS 素材包、交付校验、工作流建议和作业账本统计。</summary>
internal static class D086Schemas
{
    private static Dictionary<string, object?> S(
        string type, string? description = null, object? defaultValue = null,
        object? minimum = null, object? maximum = null, string[]? values = null)
    {
        var schema = new Dictionary<string, object?> { ["type"] = type };
        if (description is not null) schema["description"] = description;
        if (defaultValue is not null) schema["default"] = defaultValue;
        if (minimum is not null) schema["minimum"] = minimum;
        if (maximum is not null) schema["maximum"] = maximum;
        if (values is not null) schema["enum"] = values;
        return schema;
    }

    private static IReadOnlyDictionary<string, object?> Obj(
        Dictionary<string, object?> properties, params string[] required)
        => new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };

    private static Dictionary<string, object?> P(params (string Name, object? Schema)[] fields)
        => fields.ToDictionary(x => x.Name, x => x.Schema, StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, object?> ExportDesignBundle = Obj(P(
        ("layout", S("string", "布局名。")),
        ("outputPath", S("string", "输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。")),
        ("dpi", S("integer", "导出 DPI（与布局页面尺寸分别设置，不虚构 CreateLayout 的 DPI 参数）。", 300)),
        ("transparentBackground", S("boolean", "透明语义分层开关。", true)),
        ("includeVectorFiles", S("boolean", "是否另存 GIS 原生矢量文件。", true)),
        ("designSpecRevision", S("string", "绑定的 DesignSpec revision（提供则写入 manifest）。")),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))), "layout", "outputPath");

    public static readonly IReadOnlyDictionary<string, object?> ValidateDesignBundle = Obj(P(
        ("bundlePath", S("string", "素材包目录。")),
        ("manifestPath", S("string", "ArtifactManifest 路径；省略则用包内约定位置。")),
        ("strict", S("boolean", "严格模式：任一必需件不符＝整体 fail。", true))), "bundlePath");

    public static readonly IReadOnlyDictionary<string, object?> RefreshDesignBundle = Obj(P(
        ("bundlePath", S("string", "既有素材包目录。")),
        ("dataUpdates", new Dictionary<string, object?>
        {
            ["type"] = "array",
            ["default"] = Array.Empty<object>(),
            ["description"] = "数据源更新声明（路径→新路径/新 hash）。",
        }),
        ("outputPath", S("string", "新版包目录（缺省＝原路径旁新 revision 目录，不覆盖原件）。")),
        ("rerenderAll", S("boolean", "首版必须全重渲染；局部更新仅在等价性检查通过后作为优化（FINAL 路线图 §7）。", true)),
        ("overwrite", S("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", false))), "bundlePath");

    public static readonly IReadOnlyDictionary<string, object?> ValidateDeliveryPackage = Obj(P(
        ("packagePath", S("string", "交付包目录或 zip。")),
        ("manifestPath", S("string", "DeliveryManifest 路径。")),
        ("requireAll", S("boolean", "必需项缺失 ⇒ 总体不完整（不允许仅因文件存在就 PASS）。", true))), "packagePath");

    public static readonly IReadOnlyDictionary<string, object?> SuggestWorkflow = Obj(P(
        ("goal", S("string", "用户目标（表单或自然语言；两者产出同一结构）。")),
        ("inputs", new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["default"] = new Dictionary<string, object?>(),
            ["description"] = "已知输入引用（数据路径/地图/布局）。",
        }),
        ("maxSuggestions", S("integer", "建议条数（缺省 3，上限 5）。", 3))), "goal");

    public static readonly IReadOnlyDictionary<string, object?> GetPerformanceStats = Obj(P(
        ("scope", S("string", "统计范围。", "all", values: new[] { "all", "tool", "job", "host" })),
        ("toolName", S("string", "scope=tool 时的工具名。")),
        ("sinceHours", S("integer", "回溯小时数（缺省 24）。", 24)),
        ("maxItems100", S("integer", "返回条目上限（缺省 100，上限 500）。超限 ⇒ 截断并如实披露 truncated/returnedCount/totalMatched。 统计条目返回上限。", 100, 1, 500))));
}

internal sealed class D086BundleManifest
{
    public string SchemaVersion { get; set; } = "artifact-manifest-v1";
    public string JobId { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string? ParentRevision { get; set; }
    public string Layout { get; set; } = string.Empty;
    public int Dpi { get; set; }
    public bool TransparentBackground { get; set; }
    public bool IncludeVectorFiles { get; set; }
    public string? DesignSpecRevision { get; set; }
    public string GeneratedUtc { get; set; } = string.Empty;
    public D086PageDimensions PageDimensions { get; set; } = new();
    public List<D086Anchor> Anchors { get; set; } = new();
    public string FontAuditStatus { get; set; } = "not_verified_by_layout_read_api";
    public string IccAuditStatus { get; set; } = "not_reported_by_layout_export_api";
    public List<string> Limitations { get; set; } = new();
    public List<D086BundleArtifact> Artifacts { get; set; } = new();
    public List<D086DataUpdate> DataUpdates { get; set; } = new();
    public bool? RerenderAll { get; set; }
}

internal sealed class D086PageDimensions
{
    public double? Width { get; set; }
    public double? Height { get; set; }
    public string? Units { get; set; }
    public int? PixelWidth { get; set; }
    public int? PixelHeight { get; set; }
}

internal sealed class D086Anchor
{
    public string Name { get; set; } = string.Empty;
    public string ElementType { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
}

internal sealed class D086BundleArtifact
{
    public string Path { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public D086PageDimensions Dimensions { get; set; } = new();
    public List<D086Anchor> Anchors { get; set; } = new();
    public string FontAuditStatus { get; set; } = "not_verified_by_layout_read_api";
    public string IccAuditStatus { get; set; } = "not_reported_by_layout_export_api";
}

internal sealed class D086DataUpdate
{
    public string SourcePath { get; set; } = string.Empty;
    public string? NewPath { get; set; }
    public string? NewHash { get; set; }
}

internal static class D086BundleFiles
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static OperationError? ValidateOutputRoot(string? value, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            return new OperationError(ErrorCodes.InvalidArgument, "outputPath must be an absolute directory path.");

        try
        {
            fullPath = Path.GetFullPath(value);
            if (!string.Equals(Path.GetPathRoot(fullPath), @"D:\", StringComparison.OrdinalIgnoreCase))
                return new OperationError(ErrorCodes.PathEscapeRejected, "G-197: outputPath must be on D:.");
            if (string.Equals(fullPath.TrimEnd('\\'), @"D:", StringComparison.OrdinalIgnoreCase))
                return new OperationError(ErrorCodes.InvalidArgument, "outputPath must name a job-owned directory below D:.");
            if (WorkflowJobStore.IsTempLikePath(fullPath))
                return new OperationError(ErrorCodes.PathEscapeRejected, "outputPath must not live under %TEMP%.");
            var protectedHit = ProtectedOutputPathGuard.Match(fullPath);
            if (protectedHit is not null)
                return new OperationError(ErrorCodes.PathEscapeRejected, $"outputPath was refused by the protected-path guard ('{protectedHit}').");
            var parent = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
                return new OperationError(ErrorCodes.InvalidArgument, "outputPath parent directory must already exist.");
            if (ProtectedOutputPathGuard.Match(parent) is { } parentHit)
                return new OperationError(ErrorCodes.PathEscapeRejected, $"output parent was refused by the protected-path guard ('{parentHit}').");
            if (File.Exists(fullPath))
                return new OperationError(ErrorCodes.InvalidArgument, "outputPath names a file; a bundle directory is required.");
            return null;
        }
        catch (Exception ex)
        {
            return new OperationError(ErrorCodes.InvalidArgument, "outputPath is invalid.", ex.Message);
        }
    }

    public static bool IsWithin(string root, string path)
    {
        var canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var canonicalPath = Path.GetFullPath(path);
        return canonicalPath.Equals(canonicalRoot, StringComparison.OrdinalIgnoreCase)
            || canonicalPath.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static OperationResult<D086BundleManifest> ReadManifest(string bundlePath, string? manifestPath)
    {
        if (!Path.IsPathFullyQualified(bundlePath))
            return OperationResult<D086BundleManifest>.Fail(ErrorCodes.InvalidArgument, "bundlePath must be absolute.");
        string root;
        try { root = Path.GetFullPath(bundlePath); }
        catch (Exception ex) { return OperationResult<D086BundleManifest>.Fail(ErrorCodes.InvalidArgument, "bundlePath is invalid.", ex.Message); }
        if (!Directory.Exists(root))
            return OperationResult<D086BundleManifest>.Fail(ErrorCodes.NotFound, $"Bundle directory was not found: {root}");

        string path;
        try { path = Path.GetFullPath(string.IsNullOrWhiteSpace(manifestPath) ? Path.Combine(root, "ArtifactManifest.json") : manifestPath!); }
        catch (Exception ex) { return OperationResult<D086BundleManifest>.Fail(ErrorCodes.InvalidArgument, "manifestPath is invalid.", ex.Message); }
        if (!File.Exists(path))
            return OperationResult<D086BundleManifest>.Fail(ErrorCodes.NotFound, $"ArtifactManifest was not found: {path}");
        try
        {
            var manifest = JsonSerializer.Deserialize<D086BundleManifest>(File.ReadAllText(path), Json);
            if (manifest is null || !string.Equals(manifest.SchemaVersion, "artifact-manifest-v1", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(manifest.Layout) || manifest.Artifacts is null)
                return OperationResult<D086BundleManifest>.Fail(ErrorCodes.InvalidState, "ArtifactManifest has an unsupported or incomplete shape.");
            return OperationResult<D086BundleManifest>.Ok(manifest);
        }
        catch (JsonException ex)
        {
            return OperationResult<D086BundleManifest>.Fail(ErrorCodes.InvalidState, "ArtifactManifest is not valid JSON.", ex.Message);
        }
        catch (IOException ex)
        {
            return OperationResult<D086BundleManifest>.Fail(ErrorCodes.ExecutionFailed, "ArtifactManifest could not be read.", ex.Message);
        }
    }

    public static OperationResult<string> ResolveBundleArtifact(string bundleRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)
            || relativePath.Contains(':') || relativePath.StartsWith('\\') || relativePath.StartsWith('/'))
            return OperationResult<string>.Fail(ErrorCodes.InvalidState, "Artifact path must be relative to the bundle.");
        var parts = relativePath.Replace('\\', '/').Split('/');
        if (parts.Any(p => p is "" or "." or ".."))
            return OperationResult<string>.Fail(ErrorCodes.InvalidState, "Artifact path contains an empty, dot, or parent segment.");
        try
        {
            var resolved = Path.GetFullPath(Path.Combine(bundleRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithin(bundleRoot, resolved))
                return OperationResult<string>.Fail(ErrorCodes.InvalidState, "Artifact path escapes the bundle directory.");
            return OperationResult<string>.Ok(resolved);
        }
        catch (Exception ex)
        {
            return OperationResult<string>.Fail(ErrorCodes.InvalidState, "Artifact path is invalid.", ex.Message);
        }
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    public static (int? Width, int? Height) ReadPngDimensions(string path)
    {
        Span<byte> header = stackalloc byte[24];
        using var stream = File.OpenRead(path);
        if (stream.Read(header) != header.Length
            || !header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            || !header[12..16].SequenceEqual("IHDR"u8))
            return (null, null);
        var width = BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
        var height = BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
        return width > 0 && height > 0 ? (width, height) : (null, null);
    }

    public static OperationError? Publish(string stagePath, string outputPath, bool overwrite, string jobId, out bool cleanupWarning)
    {
        cleanupWarning = false;
        var backupPath = Path.Combine(Path.GetDirectoryName(outputPath)!, $"run-{jobId}-previous");
        var hadTarget = Directory.Exists(outputPath);
        if ((File.Exists(outputPath) || hadTarget) && !overwrite)
            return new OperationError(ErrorCodes.OutputExists, $"Output already exists: {outputPath}");
        if (File.Exists(outputPath))
            return new OperationError(ErrorCodes.InvalidArgument, "outputPath names a file; no output was changed.");
        if (Directory.Exists(backupPath) || File.Exists(backupPath))
            return new OperationError(ErrorCodes.InvalidState, "The job-owned backup path already exists.");
        if (ProtectedOutputPathGuard.Match(stagePath) is not null || ProtectedOutputPathGuard.Match(backupPath) is not null)
            return new OperationError(ErrorCodes.PathEscapeRejected, "Staging or backup path was refused by the protected-path guard.");

        var movedOld = false;
        try
        {
            if (hadTarget)
            {
                Directory.Move(outputPath, backupPath);
                movedOld = true;
            }
            Directory.Move(stagePath, outputPath);
            if (movedOld)
            {
                try { Directory.Delete(backupPath, recursive: true); }
                catch { cleanupWarning = true; }
            }
            return null;
        }
        catch (IOException ex) when (Directory.Exists(outputPath) && !overwrite)
        {
            if (movedOld && !Directory.Exists(outputPath) && Directory.Exists(backupPath))
                Directory.Move(backupPath, outputPath);
            return new OperationError(ErrorCodes.OutputExists, $"Output already exists: {outputPath}", ex.Message);
        }
        catch (Exception ex)
        {
            if (movedOld && !Directory.Exists(outputPath) && Directory.Exists(backupPath))
            {
                try { Directory.Move(backupPath, outputPath); }
                catch (Exception restoreEx)
                {
                    return new OperationError(ErrorCodes.ExecutionFailed,
                        "Publishing the new bundle failed and the prior output could not be restored.",
                        $"{ex.Message}; restore: {restoreEx.Message}");
                }
            }
            return new OperationError(ErrorCodes.ExecutionFailed, "Publishing the bundle failed.", ex.Message);
        }
    }

    public static void DeleteOwnedStage(string stagePath)
    {
        try
        {
            if (Directory.Exists(stagePath) && ProtectedOutputPathGuard.Match(stagePath) is null)
                Directory.Delete(stagePath, recursive: true);
        }
        catch { }
    }

    public static D086PageDimensions ToPageDimensions(LayoutDetailInfo layout, int dpi)
    {
        var result = new D086PageDimensions
        {
            Width = layout.PageWidth,
            Height = layout.PageHeight,
            Units = layout.PageUnits,
        };
        var inches = layout.PageUnits?.ToLowerInvariant() switch
        {
            "inches" or "inch" => 1d,
            "points" or "point" => 1d / 72d,
            "millimeters" or "millimeter" or "mm" => 1d / 25.4d,
            "centimeters" or "centimeter" or "cm" => 1d / 2.54d,
            _ => (double?)null,
        };
        if (inches is not null && layout.PageWidth is > 0 && layout.PageHeight is > 0)
        {
            var width = layout.PageWidth.Value * inches.Value * dpi;
            var height = layout.PageHeight.Value * inches.Value * dpi;
            if (double.IsFinite(width) && double.IsFinite(height)
                && width <= int.MaxValue && height <= int.MaxValue)
            {
                result.PixelWidth = (int)Math.Round(width);
                result.PixelHeight = (int)Math.Round(height);
            }
        }
        return result;
    }

    public static List<D086Anchor> ToAnchors(IReadOnlyList<LayoutElementInfo> elements)
        => elements.Select(e => new D086Anchor
        {
            Name = e.Name,
            ElementType = e.ElementType,
            IsVisible = e.IsVisible,
            X = e.X,
            Y = e.Y,
        }).ToList();

    public static byte[] Serialize(D086BundleManifest manifest) => JsonSerializer.SerializeToUtf8Bytes(manifest, Json);
}

/// <summary>D-086 B3：将布局导出为 PNG 预览和可选 PDF/SVG 矢量件。</summary>
public sealed class ExportDesignBundleTool : McpToolBase
{
    public override string Name => "export_design_bundle";
    public override string Description => "在 D 盘 job-owned run 根内导出布局素材包，写入逐件 SHA-256、页面尺寸与元素锚点的 ArtifactManifest；默认拒绝覆盖。";
    protected override string CategoryName => ToolCategories.Layout;
    public override IReadOnlyDictionary<string, object?> InputSchema => D086Schemas.ExportDesignBundle;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layout = ToolArgs.GetString(context, "layout");
        var output = ToolArgs.GetString(context, "outputPath");
        var dpi = ToolArgs.GetInt(context, "dpi") ?? 300;
        var transparent = ToolArgs.GetBool(context, "transparentBackground", true);
        var includeVector = ToolArgs.GetBool(context, "includeVectorFiles", true);
        var designSpecRevision = ToolArgs.GetString(context, "designSpecRevision");
        var overwrite = ToolArgs.GetBool(context, "overwrite", false);
        if (string.IsNullOrWhiteSpace(layout) || string.IsNullOrWhiteSpace(output) || dpi < 1)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layout, outputPath and a positive integer dpi are required.");
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "Write refused while read-only mode is enabled; no output was written.");
        return await D086BundleExport.ExportAsync(context, layout!, output!, dpi, transparent, includeVector,
            designSpecRevision, overwrite, null, Array.Empty<D086DataUpdate>(), null).ConfigureAwait(false);
    }
}

/// <summary>D-086 B4：创建新 revision；源素材包只读保留。</summary>
public sealed class RefreshDesignBundleTool : McpToolBase
{
    public override string Name => "refresh_design_bundle";
    public override string Description => "从当前 ArcGIS 布局重新渲染至新的 revision 目录；dataUpdates 作为更新声明写入回执，源素材包保持不变。";
    protected override string CategoryName => ToolCategories.Layout;
    public override IReadOnlyDictionary<string, object?> InputSchema => D086Schemas.RefreshDesignBundle;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var sourcePath = ToolArgs.GetString(context, "bundlePath");
        if (string.IsNullOrWhiteSpace(sourcePath))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "bundlePath is required.");
        if (context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "Write refused while read-only mode is enabled; no output was written.");

        var rerenderAll = ToolArgs.GetBool(context, "rerenderAll", true);
        if (!rerenderAll)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "This initial implementation requires rerenderAll=true; no output was written.");

        var dataUpdatesResult = ParseDataUpdates(ToolArgs.GetValue(context, "dataUpdates"));
        if (!dataUpdatesResult.Success || dataUpdatesResult.Data is null)
            return OperationResult<object?>.Fail(dataUpdatesResult.Errors);
        var sourceManifestResult = D086BundleFiles.ReadManifest(sourcePath!, null);
        if (!sourceManifestResult.Success || sourceManifestResult.Data is null)
            return ToolResult.From(sourceManifestResult);
        var sourceManifest = sourceManifestResult.Data;

        var output = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(output))
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(sourcePath!));
            if (string.IsNullOrWhiteSpace(parent))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "bundlePath must have a parent directory.");
            var baseName = Path.GetFileName(sourcePath);
            if (baseName.Length > 48) baseName = baseName[..48];
            output = Path.Combine(parent, $"{baseName}-revision-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}");
        }

        string fullSource;
        string fullOutput;
        try
        {
            fullSource = Path.GetFullPath(sourcePath!);
            fullOutput = Path.GetFullPath(output!);
        }
        catch (Exception ex)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "bundlePath or outputPath is invalid.", ex.Message);
        }
        if (D086BundleFiles.IsWithin(fullSource, fullOutput) || D086BundleFiles.IsWithin(fullOutput, fullSource))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "outputPath must be separate from the source bundle; the source bundle will not be overwritten.");

        foreach (var artifact in sourceManifest.Artifacts)
        {
            var resolved = D086BundleFiles.ResolveBundleArtifact(fullSource, artifact.Path);
            if (!resolved.Success || resolved.Data is null || !File.Exists(resolved.Data)
                || !string.Equals(D086BundleFiles.HashFile(resolved.Data), artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidState, "Source bundle integrity check failed; no new revision was written.");
        }

        return await D086BundleExport.ExportAsync(context, sourceManifest.Layout, fullOutput,
            Math.Max(1, sourceManifest.Dpi), sourceManifest.TransparentBackground, sourceManifest.IncludeVectorFiles,
            sourceManifest.DesignSpecRevision, ToolArgs.GetBool(context, "overwrite", false),
            sourceManifest.Revision, dataUpdatesResult.Data, sourceManifest.RerenderAll ?? true).ConfigureAwait(false);
    }

    private static OperationResult<IReadOnlyList<D086DataUpdate>> ParseDataUpdates(object? raw)
    {
        if (raw is null) return OperationResult<IReadOnlyList<D086DataUpdate>>.Ok(Array.Empty<D086DataUpdate>());
        IReadOnlyList<object?> items = raw switch
        {
            IReadOnlyList<object?> list => list,
            System.Collections.IEnumerable enumerable when raw is not string => enumerable.Cast<object?>().ToList(),
            _ => Array.Empty<object?>(),
        };
        if (raw is not System.Collections.IEnumerable || raw is string)
            return OperationResult<IReadOnlyList<D086DataUpdate>>.Fail(ErrorCodes.InvalidArgument, "dataUpdates must be an array.");

        var updates = new List<D086DataUpdate>();
        foreach (var item in items)
        {
            IReadOnlyDictionary<string, object?>? record = item switch
            {
                IReadOnlyDictionary<string, object?> ro => ro,
                IDictionary<string, object?> d => new Dictionary<string, object?>(d),
                JsonElement element when element.ValueKind == JsonValueKind.Object => element.EnumerateObject()
                    .ToDictionary(p => p.Name, p => (object?)p.Value.Clone(), StringComparer.Ordinal),
                _ => null,
            };
            if (record is null || record.Keys.Any(k => k is not ("sourcePath" or "newPath" or "newHash")))
                return OperationResult<IReadOnlyList<D086DataUpdate>>.Fail(ErrorCodes.InvalidArgument, "Each dataUpdates item must contain only sourcePath, newPath and newHash.");
            var source = ValueString(record, "sourcePath");
            var newPath = ValueString(record, "newPath");
            var newHash = ValueString(record, "newHash");
            if (string.IsNullOrWhiteSpace(source)
                || (record.ContainsKey("newPath") && string.IsNullOrWhiteSpace(newPath))
                || (record.ContainsKey("newHash") && (newHash is null || newHash.Length != 64 || !newHash.All(Uri.IsHexDigit))))
                return OperationResult<IReadOnlyList<D086DataUpdate>>.Fail(ErrorCodes.InvalidArgument, "dataUpdates requires a non-empty sourcePath; newPath, when present, must be non-empty; newHash, when present, must be 64 hexadecimal characters.");
            updates.Add(new D086DataUpdate { SourcePath = source!, NewPath = newPath, NewHash = newHash });
        }
        return OperationResult<IReadOnlyList<D086DataUpdate>>.Ok(updates);
    }

    private static string? ValueString(IReadOnlyDictionary<string, object?> record, string key)
    {
        if (!record.TryGetValue(key, out var value)) return null;
        if (value is string s) return s;
        if (value is JsonElement json && json.ValueKind == JsonValueKind.String) return json.GetString();
        return null;
    }
}

internal static class D086BundleExport
{
    public static async Task<OperationResult<object?>> ExportAsync(
        ToolExecutionContext context, string layoutName, string outputPath, int dpi, bool transparentBackground,
        bool includeVectorFiles, string? designSpecRevision, bool overwrite, string? parentRevision,
        IReadOnlyList<D086DataUpdate> dataUpdates, bool? rerenderAll)
    {
        var pathError = D086BundleFiles.ValidateOutputRoot(outputPath, out var finalPath);
        if (pathError is not null) return OperationResult<object?>.Fail(pathError);
        if ((Directory.Exists(finalPath) || File.Exists(finalPath)) && !overwrite)
            return OperationResult<object?>.Fail(ErrorCodes.OutputExists, $"Output already exists: {finalPath}. Pass overwrite=true to replace this bundle.");
        if (context.Host is null)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidState, "ArcGIS host is unavailable.");

        var layoutResult = await context.Host.Layout.GetLayoutInfoAsync(layoutName, context.CancellationToken).ConfigureAwait(false);
        if (!layoutResult.Success || layoutResult.Data is null)
            return OperationResult<object?>.Fail(MapLayoutError(layoutResult.Errors.FirstOrDefault()?.Code), "Layout details are unavailable; no output was written.", layoutResult.Errors.FirstOrDefault()?.Message);
        var elementsResult = await context.Host.Layout.ListLayoutElementsAsync(layoutName, context.CancellationToken).ConfigureAwait(false);
        if (!elementsResult.Success || elementsResult.Data is null)
            return OperationResult<object?>.Fail(MapLayoutError(elementsResult.Errors.FirstOrDefault()?.Code), "Layout anchors are unavailable; no output was written.", elementsResult.Errors.FirstOrDefault()?.Message);

        var parent = Path.GetDirectoryName(finalPath)!;
        var jobId = Guid.NewGuid().ToString("N");
        var stagePath = Path.Combine(parent, $"run-{jobId}");
        if (Directory.Exists(stagePath) || File.Exists(stagePath))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidState, "The job-owned D-drive run root already exists.");
        if (ProtectedOutputPathGuard.Match(stagePath) is not null || WorkflowJobStore.IsTempLikePath(stagePath))
            return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, "The job-owned staging root was refused by the path guard.");

        var anchors = D086BundleFiles.ToAnchors(elementsResult.Data);
        var page = D086BundleFiles.ToPageDimensions(layoutResult.Data, dpi);
        var revision = $"rev-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{jobId[..8]}";
        var manifest = new D086BundleManifest
        {
            JobId = jobId,
            Revision = revision,
            ParentRevision = parentRevision,
            Layout = layoutResult.Data.Name,
            Dpi = dpi,
            TransparentBackground = transparentBackground,
            IncludeVectorFiles = includeVectorFiles,
            DesignSpecRevision = designSpecRevision,
            GeneratedUtc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            PageDimensions = page,
            Anchors = anchors,
            Limitations =
            {
                "Font availability and embedded-font status are not exposed by the current layout read API.",
                "ICC profile embedding is not reported by the current layout export API.",
                "dataUpdates are recorded as declarations; this tool does not mutate GIS data sources or the ArcGIS project.",
            },
            DataUpdates = dataUpdates.ToList(),
            RerenderAll = rerenderAll,
        };

        try
        {
            Directory.CreateDirectory(stagePath);
            if (includeVectorFiles)
            {
                var pdf = await ExportOneAsync(context, layoutName, Path.Combine(stagePath, "layout.pdf"), "PDF", dpi, null).ConfigureAwait(false);
                if (!pdf.Success) return FinishFailure(stagePath, pdf.Errors);
                manifest.Artifacts.Add(Artifact(stagePath, pdf.Data!, "vector-pdf", page, anchors));

                var svg = await ExportOneAsync(context, layoutName, Path.Combine(stagePath, "layout.svg"), "SVG", dpi, null).ConfigureAwait(false);
                if (!svg.Success) return FinishFailure(stagePath, svg.Errors);
                manifest.Artifacts.Add(Artifact(stagePath, svg.Data!, "vector-svg", page, anchors));
            }

            var png = await ExportOneAsync(context, layoutName, Path.Combine(stagePath, "preview.png"), "PNG", dpi, transparentBackground).ConfigureAwait(false);
            if (!png.Success) return FinishFailure(stagePath, png.Errors);
            var preview = Artifact(stagePath, png.Data!, "raster-preview-png", page, anchors);
            var pngDimensions = D086BundleFiles.ReadPngDimensions(Path.Combine(stagePath, preview.Path));
            preview.Dimensions.PixelWidth = pngDimensions.Width;
            preview.Dimensions.PixelHeight = pngDimensions.Height;
            manifest.Artifacts.Add(preview);

            var manifestBytes = D086BundleFiles.Serialize(manifest);
            var manifestPath = Path.Combine(stagePath, "ArtifactManifest.json");
            using (var stream = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(manifestBytes, context.CancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            var manifestHash = Convert.ToHexString(SHA256.HashData(manifestBytes));

            var publishError = D086BundleFiles.Publish(stagePath, finalPath, overwrite, jobId, out var cleanupWarning);
            if (publishError is not null) return FinishFailure(stagePath, new[] { publishError });

            var files = manifest.Artifacts.Select(a => new Dictionary<string, object?>
            {
                ["path"] = a.Path,
                ["kind"] = a.Kind,
                ["bytes"] = a.Bytes,
                ["sha256"] = a.Sha256,
                ["pageDimensions"] = a.Dimensions,
                ["anchors"] = a.Anchors,
                ["fontAuditStatus"] = a.FontAuditStatus,
                ["iccAuditStatus"] = a.IccAuditStatus,
            }).ToList();
            var data = new Dictionary<string, object?>
            {
                ["outputPath"] = finalPath,
                ["jobId"] = jobId,
                ["revision"] = revision,
                ["layout"] = manifest.Layout,
                ["artifactManifestPath"] = Path.Combine(finalPath, "ArtifactManifest.json"),
                ["artifactManifestSha256"] = manifestHash,
                ["artifactManifest"] = manifest,
                ["artifacts"] = files,
                ["artifactCount"] = files.Count,
                ["transparentBackgroundAppliedToPng"] = transparentBackground,
                ["dataUpdatesApplied"] = false,
                ["rerenderAll"] = rerenderAll ?? true,
                ["sourceBundleUntouched"] = true,
                ["backupCleanupWarning"] = cleanupWarning,
            };
            return OperationResult<object?>.Ok(data, "Bundle exported to a job-owned D-drive output root; ArtifactManifest records observed file hashes, page dimensions and layout anchors.");
        }
        catch (OperationCanceledException)
        {
            D086BundleFiles.DeleteOwnedStage(stagePath);
            return OperationResult<object?>.Fail(ErrorCodes.ExecutionFailed, "Bundle export was cancelled; the source bundle and final output were not changed.");
        }
        catch (Exception ex)
        {
            D086BundleFiles.DeleteOwnedStage(stagePath);
            return OperationResult<object?>.Fail(ErrorCodes.ExecutionFailed, "Bundle export failed; only the job-owned staging root was removed.", ex.Message);
        }
    }

    private static async Task<OperationResult<LayoutExportInfo>> ExportOneAsync(
        ToolExecutionContext context, string layout, string path, string format, int dpi, bool? transparent)
    {
        OperationResult<LayoutExportInfo> result = transparent is null
            ? await context.Host!.Layout.ExportLayoutAsync(layout, path, format, dpi, overwrite: false, context.CancellationToken).ConfigureAwait(false)
            : await context.Host!.Layout.ExportLayoutWithOptionsAsync(layout, path, format, dpi, overwrite: false, transparent, context.CancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Data is null)
            return OperationResult<LayoutExportInfo>.Fail(MapLayoutError(result.Errors.FirstOrDefault()?.Code),
                result.Errors.FirstOrDefault()?.Message ?? "Layout export failed.", result.Errors.FirstOrDefault()?.Details);
        if (!File.Exists(path))
            return OperationResult<LayoutExportInfo>.Fail(ErrorCodes.InternalError, "Layout exporter returned success without creating the requested artifact.");
        return result;
    }

    private static D086BundleArtifact Artifact(
        string stageRoot, LayoutExportInfo export, string kind, D086PageDimensions page, List<D086Anchor> anchors)
    {
        var relative = Path.GetRelativePath(stageRoot, export.OutputPath);
        var info = new FileInfo(export.OutputPath);
        return new D086BundleArtifact
        {
            Path = relative.Replace('\\', '/'),
            Kind = kind,
            Bytes = info.Length,
            Sha256 = D086BundleFiles.HashFile(export.OutputPath),
            Dimensions = new D086PageDimensions
            {
                Width = page.Width,
                Height = page.Height,
                Units = page.Units,
                PixelWidth = page.PixelWidth,
                PixelHeight = page.PixelHeight,
            },
            Anchors = anchors.ToList(),
        };
    }

    private static OperationResult<object?> FinishFailure(string stagePath, IReadOnlyList<OperationError> errors)
    {
        D086BundleFiles.DeleteOwnedStage(stagePath);
        return OperationResult<object?>.Fail(errors);
    }

    private static string MapLayoutError(string? code)
        => code switch
        {
            ErrorCodes.InvalidArgument => ErrorCodes.InvalidArgument,
            ErrorCodes.OutputExists => ErrorCodes.OutputExists,
            ErrorCodes.PathEscapeRejected => ErrorCodes.PathEscapeRejected,
            ErrorCodes.ArcGISError => ErrorCodes.ArcGISError,
            ErrorCodes.InvalidState => ErrorCodes.InvalidState,
            ErrorCodes.InternalError => ErrorCodes.InternalError,
            ErrorCodes.NotFound or ErrorCodes.LayerNotFound or ErrorCodes.NotImplemented => ErrorCodes.InvalidState,
            _ => ErrorCodes.ExecutionFailed,
        };
}

/// <summary>D-086 B3：只读验证 ArtifactManifest、逐件文件指纹和可观察的 PNG 尺寸。</summary>
public sealed class ValidateDesignBundleTool : McpToolBase
{
    public override string Name => "validate_design_bundle";
    public override string Description => "只读核验素材包文件、SHA-256、字节数、PNG 尺寸和布局锚点；字体与 ICC 无宿主事实时明确标为 NOT VERIFIED。";
    protected override string CategoryName => ToolCategories.Quality;
    public override IReadOnlyDictionary<string, object?> InputSchema => D086Schemas.ValidateDesignBundle;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var bundlePath = ToolArgs.GetString(context, "bundlePath");
        var manifestPath = ToolArgs.GetString(context, "manifestPath");
        var strict = ToolArgs.GetBool(context, "strict", true);
        if (string.IsNullOrWhiteSpace(bundlePath))
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "bundlePath is required."));

        var read = D086BundleFiles.ReadManifest(bundlePath!, manifestPath);
        if (!read.Success || read.Data is null)
            return Task.FromResult(ToolResult.From(read));

        var root = Path.GetFullPath(bundlePath!);
        var manifest = read.Data;
        var errors = new List<Dictionary<string, object?>>();
        var warnings = new List<Dictionary<string, object?>>();
        var checkedArtifacts = new List<Dictionary<string, object?>>();
        var passedFiles = 0;
        foreach (var artifact in manifest.Artifacts)
        {
            var file = D086BundleFiles.ResolveBundleArtifact(root, artifact.Path);
            if (!file.Success || file.Data is null)
            {
                errors.Add(Issue(artifact.Path, "path", "invalid", file.Errors.FirstOrDefault()?.Message ?? "Artifact path is invalid."));
                checkedArtifacts.Add(new Dictionary<string, object?> { ["path"] = artifact.Path, ["valid"] = false, ["status"] = "invalid_path" });
                continue;
            }
            if (!File.Exists(file.Data))
            {
                errors.Add(Issue(artifact.Path, "exists", "missing", "Artifact file is missing."));
                checkedArtifacts.Add(new Dictionary<string, object?> { ["path"] = artifact.Path, ["valid"] = false, ["exists"] = false });
                continue;
            }
            if ((File.GetAttributes(file.Data) & FileAttributes.ReparsePoint) != 0)
            {
                errors.Add(Issue(artifact.Path, "path", "reparse_point", "Artifact file is a reparse point."));
                checkedArtifacts.Add(new Dictionary<string, object?> { ["path"] = artifact.Path, ["valid"] = false, ["exists"] = true });
                continue;
            }

            var size = new FileInfo(file.Data).Length;
            var hash = D086BundleFiles.HashFile(file.Data);
            var sizeMatches = size == artifact.Bytes;
            var hashMatches = string.Equals(hash, artifact.Sha256, StringComparison.OrdinalIgnoreCase);
            var dimensionStatus = "not_checked_for_vector";
            bool? dimensionsMatch = null;
            if (string.Equals(Path.GetExtension(file.Data), ".png", StringComparison.OrdinalIgnoreCase))
            {
                var dimensions = D086BundleFiles.ReadPngDimensions(file.Data);
                dimensionsMatch = dimensions.Width is not null && dimensions.Height is not null
                    && dimensions.Width == artifact.Dimensions.PixelWidth
                    && dimensions.Height == artifact.Dimensions.PixelHeight;
                dimensionStatus = dimensionsMatch.Value ? "match" : "mismatch_or_unavailable";
                if (!dimensionsMatch.Value)
                    errors.Add(Issue(artifact.Path, "dimensions", "mismatch", "PNG IHDR dimensions do not match the manifest or could not be read."));
            }
            var anchorsMatch = artifact.Anchors is not null
                && artifact.Anchors.Count == manifest.Anchors.Count
                && artifact.Anchors.Select(a => a.Name).SequenceEqual(manifest.Anchors.Select(a => a.Name), StringComparer.Ordinal);
            if (!anchorsMatch)
                errors.Add(Issue(artifact.Path, "anchors", "mismatch", "Artifact anchor list does not match the bundle manifest."));
            if (!sizeMatches)
                errors.Add(Issue(artifact.Path, "bytes", "mismatch", $"Expected {artifact.Bytes} bytes, observed {size}."));
            if (!hashMatches)
                errors.Add(Issue(artifact.Path, "sha256", "mismatch", "SHA-256 differs from ArtifactManifest."));

            var valid = sizeMatches && hashMatches && (dimensionsMatch ?? true) && anchorsMatch;
            if (valid) passedFiles++;
            checkedArtifacts.Add(new Dictionary<string, object?>
            {
                ["path"] = artifact.Path,
                ["kind"] = artifact.Kind,
                ["exists"] = true,
                ["valid"] = valid,
                ["expectedBytes"] = artifact.Bytes,
                ["observedBytes"] = size,
                ["expectedSha256"] = artifact.Sha256,
                ["observedSha256"] = hash,
                ["dimensionsStatus"] = dimensionStatus,
                ["anchorsStatus"] = anchorsMatch ? "manifest_shape_match" : "mismatch",
            });
        }
        if (manifest.Artifacts.Count == 0)
            errors.Add(Issue("ArtifactManifest.json", "artifacts", "empty", "ArtifactManifest contains no artifacts."));

        var fontVerified = manifest.FontAuditStatus is not ("not_verified_by_layout_read_api" or "not_verified");
        var iccVerified = manifest.IccAuditStatus is not ("not_reported_by_layout_export_api" or "not_verified");
        if (!fontVerified)
            warnings.Add(Issue("bundle", "fonts", "not_verified", "The current layout read API does not expose font availability or embedding."));
        if (!iccVerified)
            warnings.Add(Issue("bundle", "icc", "not_verified", "The current layout export API does not report ICC profile embedding."));

        var validOverall = errors.Count == 0;
        var checks = new Dictionary<string, object?>
        {
            ["files"] = new Dictionary<string, object?> { ["status"] = validOverall ? "pass" : "fail", ["passed"] = passedFiles, ["total"] = manifest.Artifacts.Count },
            ["hashes"] = new Dictionary<string, object?> { ["status"] = validOverall ? "pass" : "fail" },
            ["dimensions"] = new Dictionary<string, object?> { ["status"] = checkedArtifacts.Any(a => Equals(a.GetValueOrDefault("dimensionsStatus"), "mismatch_or_unavailable")) ? "fail" : "partial" },
            ["anchors"] = new Dictionary<string, object?> { ["status"] = errors.Any(e => Equals(e.GetValueOrDefault("check"), "anchors")) ? "fail" : "manifest_shape_match" },
            ["fonts"] = new Dictionary<string, object?> { ["status"] = fontVerified ? "verified_in_manifest" : "not_verified" },
            ["icc"] = new Dictionary<string, object?> { ["status"] = iccVerified ? "verified_in_manifest" : "not_verified" },
        };
        var report = new Dictionary<string, object?>
        {
            ["valid"] = validOverall,
            ["strict"] = strict,
            ["status"] = validOverall ? "valid_with_limitations" : "invalid",
            ["bundlePath"] = root,
            ["revision"] = manifest.Revision,
            ["checks"] = checks,
            ["artifacts"] = checkedArtifacts,
            ["artifactCount"] = manifest.Artifacts.Count,
            ["passedCount"] = passedFiles,
            ["errors"] = errors,
            ["warnings"] = warnings,
            ["sideEffects"] = false,
        };
        return Task.FromResult(OperationResult<object?>.Ok(report, "Bundle validation completed; unobservable font and ICC facts remain NOT VERIFIED."));
    }

    private static Dictionary<string, object?> Issue(string path, string check, string status, string message)
        => new() { ["path"] = path, ["check"] = check, ["status"] = status, ["message"] = message };
}

/// <summary>D-086 B4：只读完整性校验目录或 zip 内的 DeliveryManifest 条目。</summary>
public sealed class ValidateDeliveryPackageTool : McpToolBase
{
    public override string Name => "validate_delivery_package";
    public override string Description => "只读核验交付目录或 ZIP 的 DeliveryManifest、必需文件、尺寸与 SHA-256；不解压、不写入。";
    protected override string CategoryName => ToolCategories.Quality;
    public override IReadOnlyDictionary<string, object?> InputSchema => D086Schemas.ValidateDeliveryPackage;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var packagePath = ToolArgs.GetString(context, "packagePath");
        var manifestPath = ToolArgs.GetString(context, "manifestPath");
        var requireAll = ToolArgs.GetBool(context, "requireAll", true);
        if (string.IsNullOrWhiteSpace(packagePath))
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "packagePath is required."));
        if (!Path.IsPathFullyQualified(packagePath))
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "packagePath must be absolute."));

        string fullPackage;
        try { fullPackage = Path.GetFullPath(packagePath!); }
        catch (Exception ex) { return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "packagePath is invalid.", ex.Message)); }
        var isDirectory = Directory.Exists(fullPackage);
        var isZip = File.Exists(fullPackage) && string.Equals(Path.GetExtension(fullPackage), ".zip", StringComparison.OrdinalIgnoreCase);
        if (!isDirectory && !isZip)
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.NotFound, "packagePath must name an existing directory or ZIP package."));

        try
        {
            using var archive = isZip ? ZipFile.OpenRead(fullPackage) : null;
            if (archive is not null)
            {
                var unsafeEntry = archive.Entries.FirstOrDefault(e => !e.FullName.EndsWith("/", StringComparison.Ordinal) && !SafePackageRelativePath(e.FullName));
                if (unsafeEntry is not null)
                    return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, $"ZIP contains a path outside the package root: {unsafeEntry.FullName}"));
            }

            var manifestName = string.IsNullOrWhiteSpace(manifestPath) ? "DeliveryManifest.json" : manifestPath!;
            string manifestText;
            if (isZip)
            {
                if (!SafePackageRelativePath(manifestName))
                    return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, "manifestPath must be a relative ZIP entry path."));
                var entry = archive!.GetEntry(manifestName.Replace('\\', '/'));
                if (entry is null)
                    return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.NotFound, $"DeliveryManifest was not found in ZIP: {manifestName}"));
                using var stream = entry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                manifestText = reader.ReadToEnd();
            }
            else
            {
                var root = fullPackage.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string manifestFull;
                if (Path.IsPathFullyQualified(manifestName))
                {
                    manifestFull = Path.GetFullPath(manifestName);
                    if (!D086BundleFiles.IsWithin(root, manifestFull))
                        return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, "manifestPath must remain inside packagePath."));
                }
                else
                {
                    if (!SafePackageRelativePath(manifestName))
                        return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, "manifestPath must remain inside packagePath."));
                    manifestFull = Path.GetFullPath(Path.Combine(root, manifestName));
                    if (!D086BundleFiles.IsWithin(root, manifestFull))
                        return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, "manifestPath must remain inside packagePath."));
                }
                if (!File.Exists(manifestFull))
                    return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.NotFound, $"DeliveryManifest was not found: {manifestFull}"));
                manifestText = File.ReadAllText(manifestFull);
            }

            using var document = JsonDocument.Parse(manifestText);
            if (!TryGetArtifactArray(document.RootElement, out var artifacts))
                return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidState, "DeliveryManifest must contain an artifacts or files array."));
            var rows = new List<Dictionary<string, object?>>();
            var errors = new List<Dictionary<string, object?>>();
            var warnings = new List<Dictionary<string, object?>>();
            var requiredMissing = 0;
            foreach (var item in artifacts.EnumerateArray())
            {
                var relative = ArtifactPath(item);
                if (string.IsNullOrWhiteSpace(relative))
                {
                    errors.Add(new Dictionary<string, object?> { ["check"] = "path", ["status"] = "invalid", ["message"] = "Manifest item is missing a relative path." });
                    continue;
                }
                if (!SafePackageRelativePath(relative!))
                    return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, $"Manifest artifact path escapes the package: {relative}"));
                var required = !TryGetBool(item, "required", out var requiredValue) || requiredValue;
                var expectedBytes = TryGetLong(item, "bytes", out var bytes) ? bytes
                    : TryGetLong(item, "sizeBytes", out bytes) ? bytes
                    : (long?)null;
                var expectedHash = TryGetString(item, "sha256");
                long? observedBytes = null;
                string? observedHash = null;
                var exists = false;
                if (isZip)
                {
                    var entry = archive!.GetEntry(relative!.Replace('\\', '/'));
                    if (entry is not null && !entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    {
                        exists = true;
                        observedBytes = entry.Length;
                        using var stream = entry.Open();
                        using var sha = SHA256.Create();
                        observedHash = Convert.ToHexString(sha.ComputeHash(stream));
                    }
                }
                else
                {
                    var resolved = Path.GetFullPath(Path.Combine(fullPackage, relative!.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
                    if (!D086BundleFiles.IsWithin(fullPackage, resolved))
                        return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, $"Manifest artifact path escapes the package: {relative}"));
                    if (File.Exists(resolved))
                    {
                        if ((File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0)
                            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, $"Manifest artifact is a reparse point: {relative}"));
                        exists = true;
                        observedBytes = new FileInfo(resolved).Length;
                        observedHash = D086BundleFiles.HashFile(resolved);
                    }
                }
                var bytesMatch = !expectedBytes.HasValue || expectedBytes.Value == observedBytes;
                var hashMatch = string.IsNullOrWhiteSpace(expectedHash) || string.Equals(expectedHash, observedHash, StringComparison.OrdinalIgnoreCase);
                var valid = exists && bytesMatch && hashMatch;
                if (!exists && required) requiredMissing++;
                if (!exists)
                    (required ? errors : warnings).Add(new Dictionary<string, object?> { ["path"] = relative, ["check"] = "exists", ["status"] = "missing", ["required"] = required });
                else if (!bytesMatch || !hashMatch)
                    errors.Add(new Dictionary<string, object?> { ["path"] = relative, ["check"] = !bytesMatch ? "bytes" : "sha256", ["status"] = "mismatch", ["required"] = required });
                rows.Add(new Dictionary<string, object?>
                {
                    ["path"] = relative,
                    ["required"] = required,
                    ["exists"] = exists,
                    ["valid"] = valid,
                    ["expectedBytes"] = expectedBytes,
                    ["observedBytes"] = observedBytes,
                    ["expectedSha256"] = expectedHash,
                    ["observedSha256"] = observedHash,
                });
            }

            var complete = requiredMissing == 0;
            var integrityErrors = errors.Any(e =>
                Equals(e.GetValueOrDefault("check"), "bytes")
                || Equals(e.GetValueOrDefault("check"), "sha256")
                || Equals(e.GetValueOrDefault("check"), "path"));
            var validPackage = !integrityErrors && (!requireAll || complete);
            var report = new Dictionary<string, object?>
            {
                ["valid"] = validPackage,
                ["complete"] = complete,
                ["requireAll"] = requireAll,
                ["packagePath"] = fullPackage,
                ["manifestEntryCount"] = rows.Count,
                ["returnedCount"] = rows.Count,
                ["totalMatched"] = rows.Count,
                ["truncated"] = false,
                ["artifacts"] = rows,
                ["errors"] = errors,
                ["warnings"] = warnings,
                ["sideEffects"] = false,
            };
            return Task.FromResult(OperationResult<object?>.Ok(report, "Delivery package validation completed without extracting or writing files."));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidState, "DeliveryManifest is not valid JSON.", ex.Message));
        }
        catch (InvalidDataException ex)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidState, "ZIP package or manifest is invalid.", ex.Message));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.ExecutionFailed, "Delivery package validation failed.", ex.Message));
        }
    }

    private static bool TryGetArtifactArray(JsonElement root, out JsonElement array)
    {
        foreach (var name in new[] { "artifacts", "files", "deliverables" })
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array)
                return true;
        }
        array = default;
        return false;
    }

    private static string? ArtifactPath(JsonElement item)
        => item.ValueKind switch
        {
            JsonValueKind.String => item.GetString(),
            JsonValueKind.Object => TryGetString(item, "path") ?? TryGetString(item, "relativePath"),
            _ => null,
        };

    private static string? TryGetString(JsonElement item, string name)
        => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static bool TryGetBool(JsonElement item, string name, out bool value)
    {
        value = false;
        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var token)
            && token.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = token.GetBoolean();
            return true;
        }
        return false;
    }

    private static bool TryGetLong(JsonElement item, string name, out long value)
    {
        value = 0;
        return item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var token)
            && token.ValueKind == JsonValueKind.Number && token.TryGetInt64(out value);
    }

    private static bool SafePackageRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains(':') || path.StartsWith('/') || path.StartsWith('\\'))
            return false;
        return !path.Replace('\\', '/').Split('/').Any(part => part is "" or "." or "..");
    }
}

/// <summary>D-086 B4：按冻结 M1 六场景做确定性词项匹配；不调用模型、不编造数据事实。</summary>
public sealed class SuggestWorkflowTool : McpToolBase
{
    private sealed record Recipe(
        string ScenarioId, string Title, string TemplateId, string Template,
        string[] Terms, string[] RequiredInputs, string[] Tools);

    private static readonly Recipe[] Recipes =
    [
        new("S19", "规划区位现状图", "TP01", "规划位置与研究区布局",
            ["规划区位", "现状图", "study area", "location", "规划", "研究区", "区位"],
            ["studyAreaGeometry", "locationFeatures", "CRS", "source"], ["get_project_info", "list_maps", "get_map_info", "get_layer_info", "get_map_extent", "create_layout", "export_layout_pdf"]),
        new("S20", "用地/约束专题图", "TP02", "用地与约束专题布局",
            ["用地", "约束专题", "land use", "constraint", "分类设色", "用地专题"],
            ["categoryField", "categoryToSymbolMap", "units", "source", "studyAreaGeometry"], ["get_field_values", "get_field_statistics", "get_layer_symbology", "set_layer_renderer", "create_layout", "export_layout_pdf"]),
        new("S07", "设施直线覆盖", "TP03", "公共服务直线覆盖布局",
            ["设施覆盖", "直线覆盖", "coverage", "service area", "服务覆盖", "覆盖分析"],
            ["facilityPoints", "studyAreaGeometry", "distanceValue", "distanceUnit", "CRS"], ["get_dataset_info", "get_geometry_info", "get_layer_features", "buffer", "create_layout", "export_layout_pdf"]),
        new("S23", "研究区域系列图册", "TS01", "研究区域与采样点系列图",
            ["系列图册", "map series", "atlas", "采样点", "分页地图", "区域系列"],
            ["studyAreaGeometry", "samplePoints", "pointIdentityOrMeasure", "CRS", "source"], ["get_layout_info", "configure_map_series", "export_map_series", "export_layout_pdf", "list_layouts"]),
        new("S22", "科研分级设色图", "TS02", "科研变量分级设色布局",
            ["分级设色", "choropleth", "科学变量", "class breaks", "分布图", "专题设色"],
            ["numericVariable", "units", "classificationMethod", "classBreaks", "source"], ["get_field_statistics", "get_field_values", "get_layer_symbology", "set_layer_renderer", "create_layout", "export_layout_png"]),
        new("S18", "多波段专题合成", "TS08", "遥感多波段对照布局",
            ["多波段", "遥感", "multiband", "raster bands", "波段对照", "影像合成"],
            ["bandIdentityAndSpectralRole", "commonGrid", "noDataPolicy", "compositeOrder", "sourceAndAcquisition"], ["list_rasters", "get_raster_info", "create_layout", "export_layout_tif", "export_layout_png"]),
    ];

    public override string Name => "suggest_workflow";
    public override string Description => "依据冻结的 M1 场景和当前工具目录确定性匹配工作流，列出缺失输入、所需工具与仅按工具数估算的成本。";
    protected override string CategoryName => ToolCategories.General;
    protected override bool? RequiresArcGISOverride => false;
    public override IReadOnlyDictionary<string, object?> InputSchema => D086Schemas.SuggestWorkflow;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var goal = ToolArgs.GetString(context, "goal");
        var max = ToolArgs.GetInt(context, "maxSuggestions") ?? 3;
        if (string.IsNullOrWhiteSpace(goal) || max is < 1 or > 5)
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "goal is required and maxSuggestions must be between 1 and 5."));
        var rawInputs = ToolArgs.GetValue(context, "inputs");
        var inputs = ToolArgs.GetObject(context, "inputs");
        if (rawInputs is not null && inputs is null)
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "inputs must be an object."));
        var provided = new HashSet<string>(inputs?.Keys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var matched = Recipes.Select(recipe => new
            {
                Recipe = recipe,
                Score = recipe.Terms.Count(term => goal.Contains(term, StringComparison.OrdinalIgnoreCase)),
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Recipe.ScenarioId, StringComparer.Ordinal)
            .ToList();

        var ambiguous = matched.Count > 1 && matched[0].Score == matched[1].Score
            ? matched.TakeWhile(item => item.Score == matched[0].Score)
                .Select(item => new Dictionary<string, object?> { ["scenarioId"] = item.Recipe.ScenarioId, ["templateId"] = item.Recipe.TemplateId, ["score"] = item.Score })
                .ToList()
            : new List<Dictionary<string, object?>>();
        var suggestions = matched.Take(max).Select(item =>
        {
            var unavailable = context.Registry is null
                ? Array.Empty<string>()
                : item.Recipe.Tools.Where(tool => context.Registry.Get(tool) is null).ToArray();
            return new Dictionary<string, object?>
            {
                ["scenarioId"] = item.Recipe.ScenarioId,
                ["scenarioTitle"] = item.Recipe.Title,
                ["templateId"] = item.Recipe.TemplateId,
                ["template"] = item.Recipe.Template,
                ["matchScore"] = item.Score,
                ["requiredInputs"] = item.Recipe.RequiredInputs,
                ["missingPrerequisites"] = item.Recipe.RequiredInputs.Where(key => !provided.Contains(key)).ToArray(),
                ["toolSurface"] = item.Recipe.Tools,
                ["unavailableTools"] = unavailable,
                ["costEstimate"] = new Dictionary<string, object?>
                {
                    ["toolCalls"] = item.Recipe.Tools.Length,
                    ["elapsedMs"] = null,
                    ["basis"] = "frozen M1 scenario toolSurface count; runtime duration is not estimated or verified.",
                },
                ["evidenceStatus"] = "DEFINITION_ONLY_NOT_RUNTIME_VERIFIED",
            };
        }).ToList();
        var result = new Dictionary<string, object?>
        {
            ["goal"] = goal,
            ["suggestions"] = suggestions,
            ["ambiguities"] = ambiguous,
            ["returnedCount"] = suggestions.Count,
            ["totalMatched"] = matched.Count,
            ["truncated"] = matched.Count > suggestions.Count,
            ["matching"] = "deterministic keyword overlap against the frozen M1 six-scenario set; no model call",
            ["sideEffects"] = false,
        };
        return Task.FromResult(OperationResult<object?>.Ok(result));
    }
}

/// <summary>D-086 B4：从内存与 D-073 作业账本汇总已记录时长；不访问宿主或写入新存储。</summary>
public sealed class GetPerformanceStatsTool : McpToolBase
{
    private sealed record PerformanceSample(FolderJobState Job, FolderJobItem Item);

    public override string Name => "get_performance_stats";
    public override string Description => "读取 D-073 内存/作业账本中的工具项耗时和计数；不读取日志正文、不记录参数、不新建指标存储。";
    protected override string CategoryName => ToolCategories.System;
    protected override bool? RequiresArcGISOverride => false;
    public override IReadOnlyDictionary<string, object?> InputSchema => D086Schemas.GetPerformanceStats;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var scope = ToolArgs.GetString(context, "scope") ?? "all";
        var toolName = ToolArgs.GetString(context, "toolName");
        var sinceHours = ToolArgs.GetInt(context, "sinceHours") ?? 24;
        var max = ToolArgs.GetInt(context, "maxItems100") ?? 100;
        if (scope is not ("all" or "tool" or "job" or "host") || sinceHours < 1 || max is < 1 or > 500)
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "scope must be all/tool/job/host, sinceHours must be positive, and maxItems100 must be 1..500."));
        if (scope == "tool" && string.IsNullOrWhiteSpace(toolName))
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "toolName is required when scope=tool."));
        if (scope != "tool" && !string.IsNullOrWhiteSpace(toolName))
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "toolName is only valid when scope=tool."));
        if (scope == "tool" && context.Registry?.Get(toolName!) is null)
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.NotFound, $"Tool is not registered: {toolName}"));

        DateTimeOffset cutoff;
        try { cutoff = DateTimeOffset.UtcNow.AddHours(-sinceHours); }
        catch (ArgumentOutOfRangeException) { cutoff = DateTimeOffset.MinValue; }
        var jobs = WorkflowJobStore.ListAll()
            .Select(state => new { State = state, Updated = ParseUtc(state.UpdatedUtc) })
            .Where(item => item.Updated is null || item.Updated >= cutoff)
            .ToList();
        var records = jobs.SelectMany(job => job.State.Items
            .Where(item => item.State is "ok" or "failed" or "skipped")
            .Select(item => new PerformanceSample(job.State, item)))
            .ToList();

        List<Dictionary<string, object?>> rows;
        int totalMatched;
        switch (scope)
        {
            case "tool":
                var toolSamples = records.Where(row => string.Equals(row.Item.Tool, toolName, StringComparison.Ordinal)).ToList();
                rows = new List<Dictionary<string, object?>> { ToolSummary(toolName!, toolSamples) };
                totalMatched = 1;
                break;
            case "job":
                var groupedJobs = jobs.OrderByDescending(job => job.Updated).ThenBy(job => job.State.JobId, StringComparer.Ordinal).ToList();
                totalMatched = groupedJobs.Count;
                rows = groupedJobs.Take(max).Select(job =>
                {
                    var jobItems = records.Where(row => ReferenceEquals(row.Job, job.State)).ToList();
                    return new Dictionary<string, object?>
                    {
                        ["jobIdHash"] = ShortHash(job.State.JobId),
                        ["kind"] = job.State.Kind,
                        ["state"] = ListJobsTool.JobStateName(job.State),
                        ["sampleCount"] = jobItems.Count,
                        ["successCount"] = jobItems.Count(row => row.Item.State == "ok"),
                        ["failureCount"] = jobItems.Count(row => row.Item.State == "failed"),
                        ["skippedCount"] = jobItems.Count(row => row.Item.State == "skipped"),
                        ["totalDurationMs"] = jobItems.Sum(row => row.Item.DurationMs),
                        ["averageDurationMs"] = Average(jobItems.Select(row => row.Item.DurationMs)),
                        ["updatedUtc"] = job.State.UpdatedUtc,
                    };
                }).ToList();
                break;
            case "host":
                rows = new List<Dictionary<string, object?>> { Summary("host", records) };
                totalMatched = 1;
                break;
            default:
                rows = records.GroupBy(row => string.IsNullOrWhiteSpace(row.Item.Tool) ? "(unknown)" : row.Item.Tool, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Take(max)
                    .Select(group => ToolSummary(group.Key, group.ToList()))
                    .ToList();
                totalMatched = records.Select(row => row.Item.Tool).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.Ordinal).Count();
                break;
        }

        var payload = new Dictionary<string, object?>
        {
            ["scope"] = scope,
            ["toolName"] = scope == "tool" ? toolName : null,
            ["sinceHours"] = sinceHours,
            ["windowStartUtc"] = cutoff.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["source"] = "D-073 in-memory/job ledger item records only",
            ["coverage"] = "partial: calls outside recorded folder-workflow job items are not represented",
            ["records"] = rows,
            ["sampleCount"] = scope == "tool" ? records.Count(row => string.Equals(row.Item.Tool, toolName, StringComparison.Ordinal)) : records.Count,
            ["returnedCount"] = rows.Count,
            ["totalMatched"] = totalMatched,
            ["truncated"] = totalMatched > rows.Count,
            ["sensitiveArgumentsIncluded"] = false,
            ["newPersistenceCreated"] = false,
        };
        return Task.FromResult(OperationResult<object?>.Ok(payload));
    }

    private static Dictionary<string, object?> ToolSummary(
        string tool, IReadOnlyList<PerformanceSample> samples)
    {
        var durations = samples.Select(sample => (long)sample.Item.DurationMs).ToArray();
        return new Dictionary<string, object?>
        {
            ["toolName"] = tool,
            ["sampleCount"] = samples.Count,
            ["successCount"] = samples.Count(sample => sample.Item.State == "ok"),
            ["failureCount"] = samples.Count(sample => sample.Item.State == "failed"),
            ["skippedCount"] = samples.Count(sample => sample.Item.State == "skipped"),
            ["totalDurationMs"] = durations.Sum(),
            ["averageDurationMs"] = Average(durations),
            ["minimumDurationMs"] = durations.Length == 0 ? null : durations.Min(),
            ["maximumDurationMs"] = durations.Length == 0 ? null : durations.Max(),
        };
    }

    private static Dictionary<string, object?> Summary(string name, IReadOnlyList<PerformanceSample> samples)
        => new()
        {
            ["name"] = name,
            ["sampleCount"] = samples.Count,
            ["successCount"] = samples.Count(sample => sample.Item.State == "ok"),
            ["failureCount"] = samples.Count(sample => sample.Item.State == "failed"),
            ["skippedCount"] = samples.Count(sample => sample.Item.State == "skipped"),
            ["totalDurationMs"] = samples.Sum(sample => (long)sample.Item.DurationMs),
            ["averageDurationMs"] = Average(samples.Select(sample => (long)sample.Item.DurationMs)),
        };

    private static double? Average(IEnumerable<long> values)
    {
        var samples = values.ToArray();
        return samples.Length == 0 ? null : Math.Round(samples.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private static DateTimeOffset? ParseUtc(string value)
        => DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed : null;

    private static string ShortHash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];
}
