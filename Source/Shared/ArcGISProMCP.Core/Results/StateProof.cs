using System.Security.Cryptography;
using System.Text.Json;

namespace ArcGISProMCP.Core.Results;

/// <summary>
/// GP 写操作的前后状态证明（Phase 8.4，D-016；纯 .NET，无 SDK）。
/// verdict 三态（设计 §4）：executed / not_executed / unprovable —— unprovable 禁止自动重放。
/// </summary>
public static class StateProof
{
    public sealed record FileSnapshot(bool? Exists, long? Size, string? Sha256);

    /// <summary>对路径做存在性+长度+SHA256 快照；路径无效 → null（verdict 将为 unprovable）。</summary>
    public static FileSnapshot? Snapshot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (!File.Exists(path))
            {
                return new FileSnapshot(false, null, null);
            }

            using var fs = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(fs);
            return new FileSnapshot(true, fs.Length, Convert.ToHexString(hash));
        }
        catch (Exception)
        {
            return null; // 不可读（权限/IO）→ unprovable
        }
    }

    /// <summary>判定 verdict。before/after 任一不可得 → unprovable；不一致 → executed；一致 → not_executed。</summary>
    public static string Verdict(FileSnapshot? before, FileSnapshot? after)
    {
        if (before is null || after is null)
        {
            return "unprovable";
        }

        return Nullable.Equals(before, after) || (before.Exists == after.Exists
            && before.Size == after.Size
            && string.Equals(before.Sha256, after.Sha256, StringComparison.Ordinal))
            ? "not_executed"
            : "executed";
    }

    /// <summary>
    /// 路径是否位于 FileGDB 容器内（D-017 F1，GATE-D016 §2 裁定）。
    /// 纯字符串判定（无 IO、无 SDK）：任一路径段以 .gdb 结尾（大小写不敏感）即视为容器内。
    /// GDB 内要素类/表/要素数据集在文件系统语义下不可见（isfile=false ∧ isdir=false），
    /// 快照恒为 FileSnapshot(false,null,null) → 不得以文件语义判 not_executed，必须落 unprovable。
    /// </summary>
    public static bool IsGdbContainerPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 路径**本身即** FileGDB 容器（**末段**以 .gdb 结尾，如 <c>…\out.gdb</c>）—— 与"位于容器内"
    /// （如 <c>…\out.gdb\FC</c>）严格区分（D-037 F-D036-1 根因）。
    /// 语义：容器**目录本体**在文件系统下可见（Directory.Exists 有效），不属"容器内盲区"；
    /// 旧实现只判"任一段以 .gdb 结尾"→ 容器本体被误当作容器内数据集 → SDK 在库内枚举名为
    /// <c>out.gdb</c> 的数据集 → 恒 NotExists → 覆写守卫失效（已存在仍放行，行为不确定）。
    /// 纯字符串判定（无 IO、无 SDK）。
    /// </summary>
    public static bool IsGdbContainerRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0
            && segments[^1].EndsWith(".gdb", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 带路径上下文的判定（D-017 F1）：容器内路径 → 恒 unprovable（文件语义盲区，禁止落 not_executed）；
    /// 其余路径维持原有三态判定不变。
    /// </summary>
    public static string Verdict(string? outputPath, FileSnapshot? before, FileSnapshot? after)
        => IsGdbContainerPath(outputPath) ? "unprovable" : Verdict(before, after);

    /// <summary>序列化为响应载荷（before/after/verdict）。</summary>
    public static string ToJson(FileSnapshot? before, FileSnapshot? after, string verdict)
        => JsonSerializer.Serialize(new { before, after, verdict }, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

    /// <summary>
    /// 在既有 stateProof JSON 内增补 <c>"overwrite": true</c> 标记（D-026 F8，方案 b：加字段，不改 verdict 三态）。
    /// 场景：确定性覆写（overwrite=true 重跑同参数）产物字节等同 → 文件级证据只能判 <c>not_executed</c>，
    /// 该标记供消费方区分"未执行"与"覆写执行但产物字节等同"。
    /// 纯函数：解析失败 / 已含标记 / 空输入 → 原样返回，绝不抛出。
    /// </summary>
    public static string? AddOverwriteMarker(string? stateProofJson)
    {
        if (string.IsNullOrWhiteSpace(stateProofJson))
        {
            return stateProofJson;
        }

        try
        {
            if (System.Text.Json.Nodes.JsonNode.Parse(stateProofJson) is not System.Text.Json.Nodes.JsonObject obj
                || obj.ContainsKey("overwrite"))
            {
                return stateProofJson;
            }

            obj["overwrite"] = true;
            return obj.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception)
        {
            return stateProofJson;
        }
    }
}
