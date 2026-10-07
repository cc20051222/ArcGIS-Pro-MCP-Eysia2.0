using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// Phase 8.4 (D-016)：stateProof 三态判定与活动请求注册表生命周期单测（纯 Core）。
/// </summary>
public sealed class StateProofAndCancellationTests
{
    private static string TempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "d016-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Verdict_Executed_WhenOutputAppearsOrChanges()
    {
        var path = TempFile("v1");
        try
        {
            var before = StateProof.Snapshot(path);      // exists v1
            File.WriteAllText(path, "v2-longer");
            var after = StateProof.Snapshot(path);
            Assert.Equal("executed", StateProof.Verdict(before, after));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Verdict_Executed_WhenFileCreatedFromAbsent()
    {
        var path = Path.Combine(Path.GetTempPath(), "d016-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var before = StateProof.Snapshot(path);      // absent
            File.WriteAllText(path, "created");
            var after = StateProof.Snapshot(path);
            Assert.Equal("executed", StateProof.Verdict(before, after));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Verdict_NotExecuted_WhenUnchanged()
    {
        var path = TempFile("same");
        try
        {
            var before = StateProof.Snapshot(path);
            var after = StateProof.Snapshot(path);
            Assert.Equal("not_executed", StateProof.Verdict(before, after));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Verdict_NotExecuted_WhenAbsentBeforeAndAfter()
    {
        var missing = Path.Combine(Path.GetTempPath(), "d016-nope-" + Guid.NewGuid().ToString("N") + ".txt");
        var before = StateProof.Snapshot(missing);
        var after = StateProof.Snapshot(missing);
        Assert.Equal("not_executed", StateProof.Verdict(before, after));
    }

    [Fact]
    public void Verdict_Unprovable_WhenPathInvalid()
    {
        // 空路径 → Snapshot null → unprovable（禁止自动重放）。
        var before = StateProof.Snapshot("");
        Assert.Null(before);
        var after = StateProof.Snapshot(null);
        Assert.Null(after);
        Assert.Equal("unprovable", StateProof.Verdict(before, after));
    }

    [Fact]
    public void ToJson_CarriesVerdict()
    {
        var json = StateProof.ToJson(null, null, "unprovable");
        Assert.Contains("\"verdict\":\"unprovable\"", json);
    }

    // ---------------- D-017 F1：容器语义判据 ----------------

    [Theory]
    [InlineData(@"D:\ws.gdb\FC_Name", true)]
    [InlineData(@"D:\ws.GDB\fd\fc", true)]
    [InlineData(@"D:/data/ws.gdb/table", true)]
    [InlineData(@"D:\a.b.gdb\fc", true)]
    [InlineData(@"D:\data\out.shp", false)]
    [InlineData(@"D:\data\out.tif", false)]
    [InlineData(@"D:\gdbdir\out.csv", false)]   // 段名含 gdb 但不以 .gdb 结尾
    [InlineData(@"D:\my.gdb.backup\out.shp", false)] // 段以 .backup 结尾
    [InlineData("", false)]
    public void IsGdbContainerPath_SegmentDetection(string path, bool expected)
    {
        Assert.Equal(expected, StateProof.IsGdbContainerPath(path));
    }

    [Fact]
    public void Verdict_WithPath_GdbContainerOutput_IsAlwaysUnprovable()
    {
        // 回归（D-016 实测 F1）：GDB FC 输出在文件语义下两测皆 (false,null,null)，
        // 旧 Verdict 判 not_executed（假阴性）；带路径重载必须恒判 unprovable。
        var gdbFc = @"D:\ws.gdb\BUF_T1";
        var absent = new StateProof.FileSnapshot(false, null, null);
        Assert.Equal("unprovable", StateProof.Verdict(gdbFc, absent, absent));

        // 即便快照不一致（理论上不可能），容器路径仍须 unprovable。
        var present = new StateProof.FileSnapshot(true, 10, "AA");
        Assert.Equal("unprovable", StateProof.Verdict(gdbFc, absent, present));
        Assert.Equal("unprovable", StateProof.Verdict(gdbFc, null, null));
    }

    [Fact]
    public void Verdict_WithPath_FileTypeOutput_SemanticsUnchanged()
    {
        // 文件型输出（shp/csv/txt…）行为不变：created → executed；unchanged → not_executed。
        var path = Path.Combine(Path.GetTempPath(), "d017-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var before = StateProof.Snapshot(path);          // absent
            Assert.Equal("not_executed", StateProof.Verdict(path, before, StateProof.Snapshot(path)));
            File.WriteAllText(path, "created");
            Assert.Equal("executed", StateProof.Verdict(path, before, StateProof.Snapshot(path)));
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public void Registry_Cancel_ActiveRequest_ReturnsTrue_AndCancels()
    {
        var registry = new ActiveRequestRegistry();
        using var cts = new CancellationTokenSource();
        registry.Register("req-1", cts);

        Assert.True(registry.Cancel("req-1"));
        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void Registry_Cancel_UnknownOrRepeated_IsIdempotentFalse()
    {
        var registry = new ActiveRequestRegistry();
        Assert.False(registry.Cancel("nope"));
        Assert.False(registry.Cancel(null));

        using var cts = new CancellationTokenSource();
        registry.Register("req-1", cts);
        Assert.True(registry.Cancel("req-1"));
        Assert.False(registry.Cancel("req-1")); // 重复取消幂等
    }

    [Fact]
    public void Registry_Remove_CompletesRequest_LateCancelIgnored()
    {
        var registry = new ActiveRequestRegistry();
        using var cts = new CancellationTokenSource();
        registry.Register("req-1", cts);

        registry.Remove("req-1"); // 请求完成
        Assert.False(registry.Cancel("req-1")); // 迟到取消幂等忽略
        Assert.False(cts.IsCancellationRequested);
    }

    [Fact]
    public void Registry_Register_NullOrEmptyRequestId_Ignored()
    {
        var registry = new ActiveRequestRegistry();
        using var cts = new CancellationTokenSource();
        registry.Register(null, cts);
        registry.Register(string.Empty, cts);
        Assert.Equal(0, registry.Count);
    }
}
