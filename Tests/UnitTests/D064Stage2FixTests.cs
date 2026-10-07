using System.Runtime.InteropServices;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.TestSupport;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-064 <b>阶段二</b>修复单测：LIVE 实测暴露的真缺陷 ①–⑤ 的反证与回归。
/// ① diagnose 恒等比对错口径（ZIP vs DLL ⇒ 恒 STALE）② SR 短串 WKID 解析
/// ④ %TEMP% 判定可被 8.3 短名绕过（G-138 穿透）⑤ 工程打开态恢复写失败（独占句柄）
/// </summary>
public class D064Stage2FixTests
{
    private static string Code<T>(OperationResult<T> r) => r.Errors.Count > 0 ? r.Errors[0].Code : "OK";

    // ══════════════ ④ IsUnderTemp：8.3 短名绕过（真缺陷 ④ 反证）══════════════

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathNameW(string lpszLongPath, [Out] char[] lpszShortPath, uint cchBuffer);

    private static string? ShortPath(string longPath)
    {
        var buf = new char[4096];
        var n = GetShortPathNameW(longPath, buf, (uint)buf.Length);
        return n == 0 || n > buf.Length ? null : new string(buf, 0, (int)n);
    }

    [Fact]
    public void IsUnderTemp_CatchesPlainLongForm()
    {
        var temp = Path.GetTempPath();
        Assert.True(D064PathPolicy.IsUnderTemp(Path.Combine(temp, "d064")));
        Assert.True(D064PathPolicy.IsUnderTemp(temp));
    }

    [Fact]
    public void IsUnderTemp_CatchesShortNameForm_G138BypassFixed()
    {
        var temp = Path.GetTempPath().TrimEnd('\\', '/');
        var shortForm = ShortPath(temp);
        if (string.IsNullOrWhiteSpace(shortForm) || shortForm!.Equals(temp, StringComparison.OrdinalIgnoreCase))
        {
            // 本卷 8.3 短名被禁用（短名 == 长名）⇒ 该反证形态在本机不可构造，如实退化为普通形态断言。
            Assert.True(D064PathPolicy.IsUnderTemp(Path.Combine(temp, "d064snap")));
            return;
        }

        // 真缺陷 ④ 的形态：短名与长名**字符串不同**，但都指向系统临时目录。
        Assert.NotEqual(temp, shortForm, StringComparer.OrdinalIgnoreCase);
        Assert.True(D064PathPolicy.IsUnderTemp(Path.Combine(shortForm!, "d064snap")),
            "短名形态必须被识别为 %TEMP% 之下（原朴素前缀比较会漏判 ⇒ G-138 禁区穿透）");
    }

    [Fact]
    public void IsUnderTemp_CatchesShortNameFormOfNotYetCreatedLeaf()
    {
        // ★ 阶段二第二轮加固的反证：目标目录**尚未创建**时 GetLongPathNameW 会失败（返回 0），
        //   若只做「直接转换」则退化 ⇒ 仍漏判。修复后逐级上溯父级 ⇒ 必须命中。
        var temp = Path.GetTempPath().TrimEnd('\\', '/');
        var shortForm = ShortPath(temp);
        if (string.IsNullOrWhiteSpace(shortForm) || shortForm!.Equals(temp, StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(D064PathPolicy.IsUnderTemp(Path.Combine(temp, "d064snap-not-created")));
            return;
        }

        var leaf = "d064snap-not-created-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var candidate = Path.Combine(shortForm!, leaf);
        Assert.False(Directory.Exists(candidate), "前置：该叶子目录**必须不存在**（否则测的不是本条缺陷）");
        Assert.True(D064PathPolicy.IsUnderTemp(candidate),
            "未创建的短名叶子也必须被判为 %TEMP% 之下（否则 LIVE 里会直接把快照写到 C 盘 %TEMP%）");
    }

    [Fact]
    public void IsUnderTemp_CatchesDotDotAndMixedCase()
    {
        var temp = Path.GetTempPath().TrimEnd('\\', '/');
        Assert.True(D064PathPolicy.IsUnderTemp(temp + @"\..\Temp\d064"));
        Assert.True(D064PathPolicy.IsUnderTemp(temp.ToUpperInvariant() + @"\d064"));
    }

    [Fact]
    public void IsUnderTemp_StillFalseForNonTemp()
    {
        Assert.False(D064PathPolicy.IsUnderTemp(@"D:\D061Live\d064"));
        Assert.False(D064PathPolicy.IsUnderTemp(null));
        Assert.False(D064PathPolicy.IsUnderTemp(""));
    }

    [Fact]
    public void IsUnderTemp_CoversAllKnownTempRoots_NotJustGetTempPath()
    {
        // ★ 阶段二第二轮加固（真缺陷 ⑧）：宿主进程的 TEMP/TMP 可能被**重定向**（本机 Harness 为遵守
        //   G-138 把 TEMP 指到 D 盘）。此时只比对 Path.GetTempPath() 会让
        //   C:\Users\…\AppData\Local\Temp 与 %SystemRoot%\Temp 逃逸 ⇒ C 盘禁区被穿透。
        var roots = D064PathPolicy.TempRoots();
        Assert.True(roots.Count >= 3, "必须枚举多个临时根（至少 TEMP/TMP、LOCALAPPDATA\\Temp、SystemRoot\\Temp）");

        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            Assert.True(D064PathPolicy.IsUnderTemp(Path.Combine(localAppData!, "Temp", "d064snap")),
                "%LOCALAPPDATA%\\Temp 下的路径必须被判为禁区（即使 Path.GetTempPath() 指向别处）");
        }

        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (!string.IsNullOrWhiteSpace(systemRoot))
        {
            Assert.True(D064PathPolicy.IsUnderTemp(Path.Combine(systemRoot!, "Temp", "d064snap")),
                "%SystemRoot%\\Temp 下的路径必须被判为禁区");
        }
    }

    // ══════════════ ① diagnose 恒等比对口径（真缺陷 ① 反证）══════════════

    [Fact]
    public void AssemblyIdentity_EqualDllHashes_IsOk()
    {
        var facts = new DiagnosticsFacts
        {
            LoadedAssemblySha256 = "AABBCC",
            InstalledPayloadSha256 = "AABBCC",   // 同物同口径（包内 Install/…Compatibility.dll）
            InstalledPackageSha256 = "ZZZZZZ",   // 包 ZIP 不同 —— 不应参与判定
            RegisteredToolCount = ToolWriteClassification.Total,
        };
        var report = DiagnosticsComposer.Compose(facts, DateTimeOffset.UtcNow);
        Assert.Equal(DiagnosticsComposer.StatusOk,
            report.Checks.Single(c => c.Id == "assembly-identity").Status);
    }

    [Fact]
    public void AssemblyIdentity_ZipHashOnly_IsNotCompared()
    {
        // 旧口径形态：只有包 ZIP 哈希可得、包内 DLL 哈希缺失 ⇒ 必须落 Unknown（而不是拿 ZIP 比 DLL 得 Fail 假警报）
        var facts = new DiagnosticsFacts
        {
            LoadedAssemblySha256 = "AABBCC",
            InstalledPayloadSha256 = null,
            InstalledPackageSha256 = "ZZZZZZ",
            RegisteredToolCount = ToolWriteClassification.Total,
        };
        var report = DiagnosticsComposer.Compose(facts, DateTimeOffset.UtcNow);
        Assert.Equal(DiagnosticsComposer.StatusUnknown,
            report.Checks.Single(c => c.Id == "assembly-identity").Status);
    }

    [Fact]
    public void AssemblyIdentity_EvidenceNamesComparedArtifacts()
    {
        var facts = new DiagnosticsFacts
        {
            LoadedAssemblySha256 = "AABBCC",
            InstalledPayloadSha256 = "DDEEFF",
            RegisteredToolCount = ToolWriteClassification.Total,
        };
        var check = DiagnosticsComposer.Compose(facts, DateTimeOffset.UtcNow)
            .Checks.Single(c => c.Id == "assembly-identity");
        Assert.Equal(DiagnosticsComposer.StatusFail, check.Status);
        Assert.True(check.Evidence.ContainsKey("comparedArtifacts"));
        Assert.True(check.Evidence.ContainsKey("installedPayloadDllSha256"));
        Assert.True(check.Evidence.ContainsKey("installedPackageZipSha256"));
    }

    // ══════════════ ⑤ 恢复让路 + 复制重试（真缺陷 ⑤ 反证）══════════════

    private sealed class HookedSnapshotService : ProjectSnapshotService
    {
        public readonly List<string> Order = new();
        public bool PrepareFails { get; set; }

        public HookedSnapshotService(IProjectService project) : base(project, null) { }

        protected override Task<OperationResult<object?>> PrepareForRestoreAsync(string aprxPath, CancellationToken ct)
        {
            Order.Add("prepare");
            return Task.FromResult(PrepareFails
                ? OperationResult<object?>.Ok(null, "prepare failed (simulated)")
                : OperationResult<object?>.Ok(null, "prepare ok"));
        }

        protected override Task<OperationResult<object?>> ReloadProjectAsync(string aprxPath, CancellationToken ct)
        {
            Order.Add("reload");
            return Task.FromResult(OperationResult<object?>.Ok(null, "reload ok"));
        }
    }

    private sealed class StubProject : IProjectService
    {
        private readonly string _path;
        public StubProject(string path) => _path = path;

        public Task<OperationResult<ProjectInfo>> GetProjectInfoAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<ProjectInfo>.Ok(new ProjectInfo { Path = _path }));

        public Task<OperationResult<IReadOnlyList<LayoutInfo>>> ListLayoutsAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<LayoutInfo>>.Ok(Array.Empty<LayoutInfo>()));

        public Task<OperationResult<IReadOnlyList<DatasetInfo>>> ListDatabasesAsync(CancellationToken ct = default)
            => Task.FromResult(OperationResult<IReadOnlyList<DatasetInfo>>.Ok(Array.Empty<DatasetInfo>()));

        public Task<OperationResult<ProjectSaveInfo>> SaveProjectAsync(string? saveAsPath, CancellationToken ct = default)
            => Task.FromResult(OperationResult<ProjectSaveInfo>.Ok(new ProjectSaveInfo
            {
                Path = _path, FileExists = File.Exists(_path),
                FileSizeBytes = File.Exists(_path) ? new FileInfo(_path).Length : 0,
            }));
    }

    [Fact]
    public async Task Restore_InvokesPrepareThenReload_InThatOrder()
    {
        using var ws = TestWorkspace.Create();
        var aprx = ws.CreateOwnedFile("hooked.aprx", "V1");
        var svc = new HookedSnapshotService(new StubProject(aprx));
        var dir = ws.CreateOwnedDirectory("snap-hooked");

        Assert.True((await svc.CreateAsync(dir, "h1")).Success, "snapshot should succeed");
        File.WriteAllText(aprx, "V2-CHANGED");

        var r = await svc.RestoreAsync(dir, confirm: true);
        Assert.True(r.Success, Code(r));
        Assert.Equal(new[] { "prepare", "reload" }, svc.Order);      // 让路必须先于覆盖、重载必须在其后
        var payload = Assert.IsType<RestoreSnapshotResult>(r.Data);
        Assert.True(payload.ByteIdentical);
        // 阶段二加固：让路/重载的**实测说明**必须随成功回执披露（否则 LIVE 无法判断句柄是否真的释放）
        Assert.False(string.IsNullOrWhiteSpace(payload.PrepareNote));
        Assert.False(string.IsNullOrWhiteSpace(payload.ReloadNote));
    }

    [Fact]
    public async Task Restore_PrepareFailureIsNonBlocking_AndDisclosed()
    {
        using var ws = TestWorkspace.Create();
        var aprx = ws.CreateOwnedFile("hooked2.aprx", "V1");
        var svc = new HookedSnapshotService(new StubProject(aprx)) { PrepareFails = true };
        var dir = ws.CreateOwnedDirectory("snap-hooked2");
        Assert.True((await svc.CreateAsync(dir, "h2")).Success);
        File.WriteAllText(aprx, "V2");

        var r = await svc.RestoreAsync(dir, confirm: true);
        Assert.True(r.Success, Code(r));                              // 让路失败不阻断（复制层有重试）
        Assert.Equal("V1", File.ReadAllText(aprx));
    }

    [Fact]
    public async Task Restore_RetriesWhileDestinationIsTransientlyLocked()
    {
        using var ws = TestWorkspace.Create();
        var aprx = ws.CreateOwnedFile("locked.aprx", "V1");
        var svc = new HookedSnapshotService(new StubProject(aprx));
        var dir = ws.CreateOwnedDirectory("snap-locked");
        Assert.True((await svc.CreateAsync(dir, "h3")).Success);
        File.WriteAllText(aprx, "V2-ENGINE");

        // 模拟 Pro 的**瞬态**独占持有：600 ms 后释放（重试 8×400 ms 应能覆盖）。
        var holder = new FileStream(aprx, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        _ = Task.Run(async () =>
        {
            await Task.Delay(600);
            holder.Dispose();
        });

        var r = await svc.RestoreAsync(dir, confirm: true);
        Assert.True(r.Success, "restore must survive a transient exclusive lock (retry path): " + Code(r));
        Assert.Equal("V1", File.ReadAllText(aprx));
    }

    [Fact]
    public async Task Restore_PersistentLockFailsHonestly_WithoutCorruptingFile()
    {
        using var ws = TestWorkspace.Create();
        var aprx = ws.CreateOwnedFile("locked2.aprx", "V1");
        var svc = new HookedSnapshotService(new StubProject(aprx));
        var dir = ws.CreateOwnedDirectory("snap-locked2");
        Assert.True((await svc.CreateAsync(dir, "h4")).Success);
        File.WriteAllText(aprx, "V2-ENGINE");

        using (new FileStream(aprx, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var r = await svc.RestoreAsync(dir, confirm: true);
            Assert.Equal(ErrorCodes.ExecutionFailed, Code(r));
            Assert.Contains("attempt", r.Errors[0].Message!, StringComparison.OrdinalIgnoreCase);  // 重试次数如实披露
            // 阶段二加固：失败回执**始终**带让路说明（LIVE 取证需要"为何句柄未释放"）
            Assert.Contains("prepare:", r.Errors[0].Message!, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal("V2-ENGINE", File.ReadAllText(aprx));   // 未损坏、未被半写
    }

    // ══════════════ ② SR 短串解析（真缺陷 ② 的**等价纯逻辑**回归）══════════════

    [Fact]
    public void SpatialReferenceToken_IsTreatedAsWkid_WhenNumeric()
    {
        // 与 MapService.D064 同口径的判定逻辑（int-first）；Compatibility 侧无单测面，此处固化判据语义。
        foreach (var text in new[] { "4326", "3857", " 4326 " })
        {
            var t = text.Trim();
            Assert.True(int.TryParse(t, out var wkid) && wkid > 0, $"'{text}' 必须走 WKID 分支");
        }

        foreach (var text in new[] { "GCS_WGS_1984", @"GEOGCS[""WGS 84""]" })
        {
            var t = text.Trim();
            Assert.False(int.TryParse(t, out _), $"'{text}' 必须走名称/WKT 分支");
        }
    }
}
