using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArcGISProMCP.Logging;

/// <summary>Evidence for one atomic, owner-scoped JSONL snapshot-and-tail compaction.</summary>
public sealed record JsonlCompactionReceipt(
    string SourceFileName,
    string OriginalSha256,
    string CompactedSha256,
    int OriginalRecordCount,
    int TailRecordCount,
    DateTimeOffset CompactedAtUtc);

/// <summary>
/// Atomically replaces one explicitly owned JSONL file with a snapshot record and a verified tail.
/// The caller owns naming and record semantics; this helper never enumerates or deletes sibling files.
/// </summary>
public sealed class OwnedJsonlCompactor
{
    public JsonlCompactionReceipt Compact(
        string ownedJournalPath,
        string snapshotRecord,
        IReadOnlyList<string> tailRecords,
        string evidencePath)
    {
        if (string.IsNullOrWhiteSpace(ownedJournalPath))
        {
            throw new ArgumentException("An owned journal path is required.", nameof(ownedJournalPath));
        }

        if (string.IsNullOrWhiteSpace(snapshotRecord))
        {
            throw new ArgumentException("A snapshot record is required.", nameof(snapshotRecord));
        }

        if (string.IsNullOrWhiteSpace(evidencePath))
        {
            throw new ArgumentException("An evidence path is required.", nameof(evidencePath));
        }
        ArgumentNullException.ThrowIfNull(tailRecords);

        var journalPath = Path.GetFullPath(ownedJournalPath);
        var receiptPath = Path.GetFullPath(evidencePath);
        if (!Path.IsPathRooted(ownedJournalPath)
            || !Path.IsPathRooted(evidencePath)
            || !string.Equals(Path.GetDirectoryName(journalPath), Path.GetDirectoryName(receiptPath), StringComparison.OrdinalIgnoreCase)
            || !journalPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(receiptPath).Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Compaction requires sibling, explicitly owned journal and evidence files.");
        }

        if (!File.Exists(journalPath)
            || IsReparsePoint(journalPath)
            || HasReparseAncestor(journalPath)
            || (File.Exists(receiptPath) && IsReparsePoint(receiptPath))
            || HasReparseAncestor(receiptPath))
        {
            throw new InvalidOperationException("The owned journal is missing or traverses a reparse point.");
        }

        ValidateJsonLine(snapshotRecord);
        foreach (var line in tailRecords)
        {
            ValidateJsonLine(line);
        }

        var originalBytes = File.ReadAllBytes(journalPath);
        var originalHash = Convert.ToHexString(SHA256.HashData(originalBytes));
        var originalLines = Encoding.UTF8.GetString(originalBytes)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in originalLines)
        {
            ValidateJsonLine(line);
        }

        var compactedText = string.Join("\n", new[] { snapshotRecord }.Concat(tailRecords)) + "\n";
        var compactedBytes = new UTF8Encoding(false).GetBytes(compactedText);
        var compactedHash = Convert.ToHexString(SHA256.HashData(compactedBytes));
        var temporaryPath = journalPath + ".compact.tmp";
        var pendingEvidencePath = receiptPath + ".pending";
        var receipt = new JsonlCompactionReceipt(
            Path.GetFileName(journalPath),
            originalHash,
            compactedHash,
            originalLines.Length,
            tailRecords.Count,
            DateTimeOffset.UtcNow);

        if (File.Exists(temporaryPath) || File.Exists(pendingEvidencePath))
        {
            throw new InvalidOperationException("A compaction temporary sibling already exists; refusing to overwrite unverified state.");
        }

        var temporaryCreated = false;
        var pendingEvidenceCreated = false;
        var replacementCompleted = false;
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                temporaryCreated = true;
                stream.Write(compactedBytes, 0, compactedBytes.Length);
                stream.Flush(flushToDisk: true);
            }

            using (var stream = new FileStream(pendingEvidencePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                pendingEvidenceCreated = true;
                writer.Write(JsonSerializer.Serialize(receipt));
                writer.Write(Environment.NewLine);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, journalPath, overwrite: true);
            replacementCompleted = true;
            File.Move(pendingEvidencePath, receiptPath, overwrite: true);
            pendingEvidenceCreated = false;
            return receipt;
        }
        finally
        {
            if (temporaryCreated && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            // If replacement committed but promoting the receipt failed, keep the pending evidence
            // beside the journal so an interrupted compaction remains independently inspectable.
            if (!replacementCompleted && pendingEvidenceCreated && File.Exists(pendingEvidencePath))
            {
                File.Delete(pendingEvidencePath);
            }
        }
    }

    private static void ValidateJsonLine(string line)
    {
        using var _ = JsonDocument.Parse(line);
    }

    private static bool HasReparseAncestor(string path)
    {
        var current = new FileInfo(path).Directory;
        while (current is not null)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static bool IsReparsePoint(string path)
        => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}
