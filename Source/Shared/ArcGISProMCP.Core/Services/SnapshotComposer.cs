using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// D-064 · 项目快照 / 恢复（G-166 差异化支柱二）的**纯文件层**：清单读写 + 哈希 + 完整性校验。
/// 无 SDK、无 IO 副作用以外逻辑 —— 便于单测穷举（损坏快照拒绝 / 篡改字节拒绝 / 哈希不一致拒绝）。
/// <para><b>完整性契约</b>：清单内每个文件都必须**存在 + 字节数一致 + SHA256 一致**；
/// 任一不符 ⇒ <c>IntegrityVerified=false</c> 且恢复被拒绝（**不得"尽力恢复"**）。
/// 另做**清单外文件检测**（快照目录内出现未登记文件 ⇒ 视为污染，同样拒绝）。</para>
/// </summary>
public static class SnapshotComposer
{
    /// <summary>清单文件名（固定，位于快照目录根）。</summary>
    public const string ManifestFileName = "snapshot.manifest.json";

    /// <summary>APRX 在快照目录内的固定相对路径。</summary>
    public const string AprxRelativePath = "project.aprx";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>计算文件 SHA256（十六进制大写）。文件不存在/不可读 ⇒ null。</summary>
    public static string? HashFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var fs = File.OpenRead(path!);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(fs));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>生成文件条目（含哈希与字节数）；不可读 ⇒ null。</summary>
    public static SnapshotFileInfo? Describe(string root, string fullPath)
    {
        var hash = HashFile(fullPath);
        if (hash is null)
        {
            return null;
        }

        long bytes;
        try
        {
            bytes = new FileInfo(fullPath).Length;
        }
        catch
        {
            return null;
        }

        return new SnapshotFileInfo
        {
            RelativePath = Relative(root, fullPath),
            Sha256 = hash,
            Bytes = bytes,
        };
    }

    /// <summary>相对路径（统一 <c>/</c> 分隔，稳定可读）。</summary>
    public static string Relative(string root, string fullPath)
        => Path.GetRelativePath(root, fullPath).Replace('\\', '/');

    /// <summary>写入清单（UTF-8 no BOM，缩进，确定性）。</summary>
    public static void WriteManifest(string snapshotDir, SnapshotManifest manifest)
    {
        Directory.CreateDirectory(snapshotDir);
        var path = Path.Combine(snapshotDir, ManifestFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions), new UTF8Encoding(false));
    }

    /// <summary>读取清单；缺失/损坏 ⇒ null + 原因。</summary>
    public static SnapshotManifest? ReadManifest(string snapshotDir, out string? error)
    {
        error = null;
        var path = Path.Combine(snapshotDir, ManifestFileName);
        if (!File.Exists(path))
        {
            error = "manifest-missing: " + path;
            return null;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var manifest = JsonSerializer.Deserialize<SnapshotManifest>(json, JsonOptions);
            if (manifest is null)
            {
                error = "manifest-empty-or-null";
                return null;
            }

            if (string.IsNullOrWhiteSpace(manifest.AprxSha256) || manifest.Files.Count == 0)
            {
                error = "manifest-incomplete: aprxSha256/files missing";
                return null;
            }

            return manifest;
        }
        catch (Exception ex)
        {
            error = "manifest-corrupt: " + ex.Message;
            return null;
        }
    }

    /// <summary>完整性校验结论。</summary>
    public sealed class IntegrityReport
    {
        public bool Verified { get; init; }

        public string Detail { get; init; } = string.Empty;

        /// <summary>不一致项（相对路径 + 原因）。</summary>
        public IReadOnlyList<string> Mismatches { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// 完整性校验：逐项比对「存在 / 字节数 / SHA256」，并检测**未登记文件**（污染）。
    /// 任一不符 ⇒ <c>Verified=false</c>（恢复必须拒绝）。
    /// </summary>
    public static IntegrityReport VerifyIntegrity(string snapshotDir, SnapshotManifest? manifest)
    {
        if (manifest is null)
        {
            return new IntegrityReport { Verified = false, Detail = "manifest-missing-or-corrupt" };
        }

        var mismatches = new List<string>();
        foreach (var f in manifest.Files)
        {
            var full = Path.Combine(snapshotDir, f.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                mismatches.Add(f.RelativePath + ": missing");
                continue;
            }

            long bytes;
            try
            {
                bytes = new FileInfo(full).Length;
            }
            catch (Exception ex)
            {
                mismatches.Add(f.RelativePath + ": unreadable (" + ex.GetType().Name + ")");
                continue;
            }

            if (bytes != f.Bytes)
            {
                mismatches.Add(f.RelativePath + $": size {bytes} != {f.Bytes}");
                continue;
            }

            var hash = HashFile(full);
            if (!string.Equals(hash, f.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                mismatches.Add(f.RelativePath + ": sha256 mismatch (tampered?)");
            }
        }

        // 清单外文件（污染）检测 —— 不阻断同名已登记项，但污染快照不得用于恢复。
        var listed = new HashSet<string>(manifest.Files.Select(f => f.RelativePath), StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var f in Directory.EnumerateFiles(snapshotDir, "*", SearchOption.AllDirectories))
            {
                var rel = Relative(snapshotDir, f);
                if (string.Equals(rel, ManifestFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!listed.Contains(rel))
                {
                    mismatches.Add(rel + ": unlisted-file (polluted snapshot)");
                }
            }
        }
        catch (Exception ex)
        {
            mismatches.Add("enumeration-failed: " + ex.GetType().Name);
        }

        return mismatches.Count == 0
            ? new IntegrityReport { Verified = true, Detail = $"{manifest.Files.Count} file(s) verified (existence/size/sha256)" }
            : new IntegrityReport { Verified = false, Detail = $"{mismatches.Count} integrity problem(s)", Mismatches = mismatches };
    }
}
