using System.Diagnostics;
using System.Text.Json;
using ArcGISProMCP.TestSupport;

namespace ArcGISProMCP.UnitTests;

public sealed class ClientConfigurationPolicyTests
{
    private static string Repo => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static string Script => Path.Combine(Repo, "scripts", "client-config.ps1");

    [Fact]
    public void PlanListsAllFourClientsWithoutWriting()
    {
        var r = Run("-Action", "Plan", "-Json");
        Assert.Equal(0, r.Code);
        Assert.Equal(4, JsonDocument.Parse(r.Out).RootElement.GetProperty("clients").GetArrayLength());
    }

    /// <summary>
    /// 当前宿主上的 codex / cursor / deepseek-harness 配置**只读**校验。
    /// **D-054 A2 修复（环境假设）**：原实现无条件断言 status=PASS，隐含「本机已配置全部客户端」这一
    /// 未经声明的前提；未配置的客户端会让 Validate 如实返回 <c>NOT_INSTALLED</c>（脚本第 205 行语义），
    /// 于是整例恒失败。现改为**随现实自适应但仍严格**：配置文件在场 ⇒ 必须 PASS；
    /// 不在场 ⇒ 必须恰为 NOT_INSTALLED（拒绝第三种状态，避免把真实缺口伪装成「未安装」）；
    /// 并强制 Validate 为只读（readOnly=true）。
    /// </summary>
    [Fact]
    public void CurrentCodexCursorAndDeepSeekConfigsValidateReadOnly()
    {
        var repo = Repo;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var targets = new[]
        {
            ("codex", Path.Combine(repo, ".codex", "config.toml")),
            ("cursor", Path.Combine(repo, ".cursor", "mcp.json")),
            ("deepseek-harness", Path.Combine(userProfile, ".dsh", "profiles", "web", "cordis.patch.yml")),
        };
        foreach (var (id, expectedPath) in targets)
        {
            var r = Run("-Action", "Validate", "-Client", id, "-Json");
            Assert.Equal(0, r.Code);
            var root = JsonDocument.Parse(r.Out).RootElement;
            var client = root.GetProperty("clients")[0];
            Assert.Equal(expectedPath, client.GetProperty("path").GetString());
            Assert.True(root.GetProperty("readOnly").GetBoolean(), $"{id}: Validate must be read-only");
            var expected = File.Exists(expectedPath) ? "PASS" : "NOT_INSTALLED";
            Assert.Equal(expected, client.GetProperty("status").GetString());
        }
    }

    [Fact]
    public void CursorApplyRestoreIsAtomicIdempotentAndPreservesUnrelatedEntry()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var dir = Directory.CreateDirectory(Path.Combine(root.FullName, ".cursor"));
        var file = Path.Combine(dir.FullName, "mcp.json");
        File.WriteAllText(file, """{"unrelated":{"keep":true,"token":"opaque"},"mcpServers":{}}""");
        try
        {
            var firstApply = Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json");
            Assert.True(firstApply.Code == 0, firstApply.Out + firstApply.Err);
            var applied = JsonDocument.Parse(File.ReadAllText(file));
            Assert.True(applied.RootElement.GetProperty("unrelated").GetProperty("keep").GetBoolean());
            Assert.Equal("http://127.0.0.1:6520/mcp", applied.RootElement.GetProperty("mcpServers").GetProperty("arcgis-pro-mcp").GetProperty("url").GetString());
            var afterFirst = File.ReadAllText(file);
            Assert.Equal(0, Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json").Code);
            Assert.Contains("arcgis-pro-mcp", File.ReadAllText(file));
            Assert.NotEmpty(File.ReadAllText(file));
            Assert.Equal(0, Run("-Action", "Restore", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json").Code);
            var restored = JsonDocument.Parse(File.ReadAllText(file));
            Assert.True(restored.RootElement.GetProperty("unrelated").GetProperty("keep").GetBoolean());
            Assert.Empty(restored.RootElement.GetProperty("mcpServers").EnumerateObject());
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void WrongEndpointIsRejected()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var dir = Directory.CreateDirectory(Path.Combine(root.FullName, ".cursor"));
        File.WriteAllText(Path.Combine(dir.FullName, "mcp.json"), """{"mcpServers":{"arcgis-pro-mcp":{"url":"http://127.0.0.1:9999/mcp"}}}""");
        try
        {
            var r = Run("-Action", "Validate", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, r.Code);
            Assert.Contains("CONFIG_ENDPOINT_INVALID", r.Out + r.Err);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void UnmanagedFragmentEntryIsRejected()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var dir = Directory.CreateDirectory(Path.Combine(root.FullName, ".codex"));
        File.WriteAllText(Path.Combine(dir.FullName, "config.toml"), "[mcp_servers.arcgis-pro-mcp]\nurl = \"http://127.0.0.1:6520/mcp\"\n");
        try
        {
            var r = Run("-Action", "Apply", "-Client", "codex", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, r.Code);
            Assert.Contains("UNMANAGED_EXISTING_ENTRY", r.Out + r.Err);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void SecretFieldIsRefused()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var dir = Directory.CreateDirectory(Path.Combine(root.FullName, ".cursor"));
        File.WriteAllText(Path.Combine(dir.FullName, "mcp.json"), """{"mcpServers":{"arcgis-pro-mcp":{"url":"http://127.0.0.1:6520/mcp","token":"forbidden"}}}""");
        try
        {
            var r = Run("-Action", "Validate", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, r.Code);
            Assert.Contains("SECRET_FIELD_REFUSED", r.Out + r.Err);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void CursorApplyRefusesTargetSecretAndPreservesExactBytes()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var dir = Directory.CreateDirectory(Path.Combine(root.FullName, ".cursor"));
        var file = Path.Combine(dir.FullName, "mcp.json");
        var original = "{\r\n  \"mcpServers\": {\r\n    \"arcgis-pro-mcp\": {\r\n      \"url\": \"http://127.0.0.1:6520/mcp\",\r\n      \"token\": \"opaque\"\r\n    }\r\n  }\r\n}";
        File.WriteAllText(file, original);
        try
        {
            var r = Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, r.Code);
            Assert.Contains("SECRET_FIELD_REFUSED", r.Out + r.Err);
            Assert.Equal(original, File.ReadAllText(file));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void CursorNewFileApplyRestoreDeletesOnlyCreatedFile()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var file = Path.Combine(root.FullName, ".cursor", "mcp.json");
        try
        {
            Assert.Equal(0, Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json").Code);
            Assert.True(File.Exists(file));
            Assert.True(File.Exists(file + ".arcgis-pro-mcp.bak.json"));
            Assert.Equal(0, Run("-Action", "Restore", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json").Code);
            Assert.False(File.Exists(file));
            Assert.False(Directory.EnumerateFiles(root.FullName, ".tmp-*", SearchOption.AllDirectories).Any());
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void InjectedFailureRestoresExactExistingBytesAndDeletesNewFile()
    {
        var existingRoot = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var existingFile = Path.Combine(Directory.CreateDirectory(Path.Combine(existingRoot.FullName, ".cursor")).FullName, "mcp.json");
        var existingBytes = new byte[] { 0x7B, 0x0D, 0x0A, 0x20, 0x22, 0x6D, 0x63, 0x70, 0x53, 0x65, 0x72, 0x76, 0x65, 0x72, 0x73, 0x22, 0x3A, 0x20, 0x7B, 0x7D, 0x0A, 0x7D };
        File.WriteAllBytes(existingFile, existingBytes);
        var newRoot = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        try
        {
            var existingResult = Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", existingRoot.FullName, "-InjectFailureAfterWrite", "-Json");
            Assert.NotEqual(0, existingResult.Code);
            Assert.Contains("INJECTED_POST_WRITE_FAILURE", existingResult.Out + existingResult.Err);
            Assert.Equal(existingBytes, File.ReadAllBytes(existingFile));

            var newResult = Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", newRoot.FullName, "-InjectFailureAfterWrite", "-Json");
            Assert.NotEqual(0, newResult.Code);
            Assert.False(File.Exists(Path.Combine(newRoot.FullName, ".cursor", "mcp.json")));
        }
        finally { existingRoot.Delete(true); newRoot.Delete(true); }
    }

    [Fact]
    public void StaleUserEditBlocksApplyAndRestore()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var dir = Directory.CreateDirectory(Path.Combine(root.FullName, ".cursor"));
        var file = Path.Combine(dir.FullName, "mcp.json");
        File.WriteAllText(file, "{\"unrelated\":{\"keep\":true},\"mcpServers\":{}}");
        try
        {
            Assert.Equal(0, Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json").Code);
            File.AppendAllText(file, "\n");
            var apply = Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, apply.Code);
            Assert.Contains("STALE_BACKUP_REFUSED", apply.Out + apply.Err);
            var restore = Run("-Action", "Restore", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, restore.Code);
            Assert.Contains("STALE_BACKUP_REFUSED", restore.Out + restore.Err);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void RepeatedApplyInjectedFailureRecoversAppliedStateForFutureApplyAndRestore()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var dir = Directory.CreateDirectory(Path.Combine(root.FullName, ".cursor"));
        var file = Path.Combine(dir.FullName, "mcp.json");
        File.WriteAllText(file, "{\"unrelated\":{\"keep\":true},\"mcpServers\":{}}");
        try
        {
            Assert.Equal(0, Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json").Code);
            var appliedBytes = File.ReadAllBytes(file);
            var failed = Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-InjectFailureAfterWrite", "-Json");
            Assert.NotEqual(0, failed.Code);
            Assert.Contains("INJECTED_POST_WRITE_FAILURE", failed.Out + failed.Err);
            Assert.Equal(appliedBytes, File.ReadAllBytes(file));
            Assert.Equal(0, Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json").Code);
            Assert.Equal(0, Run("-Action", "Restore", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json").Code);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void IncompleteFirstApplyBaselineRejectsLaterExistingUserEdits()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var file = Path.Combine(Directory.CreateDirectory(Path.Combine(root.FullName, ".cursor")).FullName, "mcp.json");
        File.WriteAllText(file, "{\"mcpServers\":{\"arcgis-pro-mcp\":{\"url\":\"http://127.0.0.1:6520/mcp\",\"token\":\"opaque\"}}}");
        try
        {
            var first = Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, first.Code);
            Assert.Contains("SECRET_FIELD_REFUSED", first.Out + first.Err);
            File.WriteAllText(file, "{\"mcpServers\":{}}");
            var retry = Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, retry.Code);
            Assert.Contains("STALE_BACKUP_REFUSED", retry.Out + retry.Err);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void IncompleteAbsentBaselineRejectsLaterUserCreatedFile()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var templateRoot = Directory.CreateTempSubdirectory("arcgispro-mcp-template-");
        var file = Path.Combine(root.FullName, ".cursor", "mcp.json");
        try
        {
            var first = Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-TemplateRoot", templateRoot.FullName, "-Json");
            Assert.NotEqual(0, first.Code);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "{\"mcpServers\":{}}");
            var retry = Run("-Action", "Apply", "-Client", "cursor", "-ConfigRoot", root.FullName, "-TemplateRoot", templateRoot.FullName, "-Json");
            Assert.NotEqual(0, retry.Code);
            Assert.Contains("STALE_BACKUP_REFUSED", retry.Out + retry.Err);
        }
        finally { root.Delete(true); templateRoot.Delete(true); }
    }

    [Fact]
    public void ExactEndpointAndDuplicateManagedBlocksAreRejected()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var cursor = Path.Combine(Directory.CreateDirectory(Path.Combine(root.FullName, ".cursor")).FullName, "mcp.json");
        var codex = Path.Combine(Directory.CreateDirectory(Path.Combine(root.FullName, ".codex")).FullName, "config.toml");
        File.WriteAllText(cursor, "{\"mcpServers\":{\"arcgis-pro-mcp\":{\"url\":\"http://127.0.0.1:6520/mcp-extra\"}}}");
        var fragment = File.ReadAllText(Path.Combine(Repo, "Config", "client-templates", "codex.toml.fragment"));
        File.WriteAllText(codex, fragment + Environment.NewLine + fragment);
        try
        {
            var json = Run("-Action", "Validate", "-Client", "cursor", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, json.Code);
            Assert.Contains("CONFIG_ENDPOINT_INVALID", json.Out + json.Err);
            var toml = Run("-Action", "Validate", "-Client", "codex", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, toml.Code);
            Assert.Contains("MANAGED_BLOCK_DUPLICATE", toml.Out + toml.Err);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void MutationWithoutExplicitClientFailsBeforeAnyOwnedTempWrite()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var codex = Path.Combine(Directory.CreateDirectory(Path.Combine(root.FullName, ".codex")).FullName, "config.toml");
        var cursor = Path.Combine(Directory.CreateDirectory(Path.Combine(root.FullName, ".cursor")).FullName, "mcp.json");
        File.WriteAllText(codex, "# unrelated codex settings\n");
        File.WriteAllText(cursor, "{\"unrelated\":{\"keep\":true},\"mcpServers\":{}}");
        var beforeCodex = File.ReadAllBytes(codex);
        var beforeCursor = File.ReadAllBytes(cursor);
        try
        {
            var apply = Run("-Action", "Apply", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, apply.Code);
            Assert.Contains("MUTATION_REQUIRES_EXPLICIT_CLIENT", apply.Out + apply.Err);
            Assert.Equal(beforeCodex, File.ReadAllBytes(codex));
            Assert.Equal(beforeCursor, File.ReadAllBytes(cursor));
            Assert.Empty(Directory.EnumerateFiles(root.FullName, "*.arcgis-pro-mcp.bak*", SearchOption.AllDirectories));
            Assert.Empty(Directory.EnumerateFiles(root.FullName, ".tmp-*", SearchOption.AllDirectories));

            var restore = Run("-Action", "Restore", "-ConfigRoot", root.FullName, "-Json");
            Assert.NotEqual(0, restore.Code);
            Assert.Contains("MUTATION_REQUIRES_EXPLICIT_CLIENT", restore.Out + restore.Err);
            Assert.Equal(beforeCodex, File.ReadAllBytes(codex));
            Assert.Equal(beforeCursor, File.ReadAllBytes(cursor));
            Assert.Empty(Directory.EnumerateFiles(root.FullName, "*.arcgis-pro-mcp.bak*", SearchOption.AllDirectories));
            Assert.Empty(Directory.EnumerateFiles(root.FullName, ".tmp-*", SearchOption.AllDirectories));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void CodexManagedApplyRestoreAndDeepSeekUserProfileRelativeLifecyclePass()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        try
        {
            Assert.Equal(0, Run("-Action", "Apply", "-Client", "codex", "-ConfigRoot", root.FullName, "-Json").Code);
            Assert.Equal(0, Run("-Action", "Apply", "-Client", "codex", "-ConfigRoot", root.FullName, "-Json").Code);
            Assert.Equal(0, Run("-Action", "Restore", "-Client", "codex", "-ConfigRoot", root.FullName, "-Json").Code);
            var deepSeek = Path.Combine(root.FullName, ".dsh", "profiles", "web", "cordis.patch.yml");
            Assert.Equal(0, Run("-Action", "Apply", "-Client", "deepseek-harness", "-ConfigRoot", root.FullName, "-Json").Code);
            Assert.True(File.Exists(deepSeek));
            Assert.Equal(0, Run("-Action", "Apply", "-Client", "deepseek-harness", "-ConfigRoot", root.FullName, "-Json").Code);
            Assert.Equal(0, Run("-Action", "Restore", "-Client", "deepseek-harness", "-ConfigRoot", root.FullName, "-Json").Code);
            Assert.False(File.Exists(deepSeek));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void ClaudeTemplateValidatesAndApplyIsUnsupported()
    {
        var validate = Run("-Action", "Validate", "-Client", "claude-desktop", "-Json");
        Assert.Equal(0, validate.Code);
        Assert.Contains("TEMPLATE_VALID", validate.Out);
        var apply = Run("-Action", "Apply", "-Client", "claude-desktop", "-Json");
        Assert.NotEqual(0, apply.Code);
        Assert.Contains("APPLY_UNSUPPORTED", apply.Out + apply.Err);
    }

    [Fact]
    public void UnrelatedTomlAndYamlCredentialWordsDoNotFalsePositive()
    {
        var root = Directory.CreateTempSubdirectory("arcgispro-mcp-client-");
        var codex = Path.Combine(Directory.CreateDirectory(Path.Combine(root.FullName, ".codex")).FullName, "config.toml");
        File.WriteAllText(codex, "model = \"unrelated\"\nprovider = \"unrelated\"\n" + File.ReadAllText(Path.Combine(Repo, "Config", "client-templates", "codex.toml.fragment")));
        var deepSeek = Path.Combine(Directory.CreateDirectory(Path.Combine(root.FullName, ".dsh", "profiles", "web")).FullName, "cordis.patch.yml");
        File.WriteAllText(deepSeek, "- insert:\n    - id: unrelated\n      model: opaque\n" + File.ReadAllText(Path.Combine(Repo, "Config", "client-templates", "deepseek.cordis.patch.yml")));
        try
        {
            Assert.Equal(0, Run("-Action", "Validate", "-Client", "codex", "-ConfigRoot", root.FullName, "-Json").Code);
            Assert.Equal(0, Run("-Action", "Validate", "-Client", "deepseek-harness", "-ConfigRoot", root.FullName, "-Json").Code);
        }
        finally { root.Delete(true); }
    }

    /// <summary>
    /// 调用 <c>scripts/client-config.ps1</c>。
    /// **D-054 A2 修复**：宿主由硬编码 <c>pwsh.exe</c> 改为 <see cref="PowerShellHost"/> 解析
    /// （PATH 上的 PS7 → 标准安装位 → System32 内置 5.1 兜底）。原实现在未安装 PS7 的机器上
    /// 以 Win32Exception 整族失败（环境不可达，非产品缺口）；断言强度不变。
    /// </summary>
    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo { FileName = PowerShellHost.Executable, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        p.StartInfo.ArgumentList.Add("-NoProfile"); p.StartInfo.ArgumentList.Add("-ExecutionPolicy"); p.StartInfo.ArgumentList.Add("Bypass"); p.StartInfo.ArgumentList.Add("-File"); p.StartInfo.ArgumentList.Add(Script);
        foreach (var arg in args) p.StartInfo.ArgumentList.Add(arg);
        p.Start(); var o = p.StandardOutput.ReadToEnd(); var e = p.StandardError.ReadToEnd(); p.WaitForExit(); return (p.ExitCode, o, e);
    }
}
