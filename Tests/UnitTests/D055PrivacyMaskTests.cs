using ArcGISProMCP.Logging;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-055（C2 · 13.2 日志脱敏）单元测试：用户主目录段掩码。
/// 结构化日志通道维持既有 drop-first 行为（绝对路径一律丢弃）；本掩码为**能力补位**，
/// 供需要保留诊断价值的回显面调用（接线与否待 Keeper 裁定，见 <see cref="LogRedactionPolicy.MaskUserProfilePaths"/> 备注）。
/// </summary>
public sealed class D055PrivacyMaskTests
{
    [Fact]
    public void MaskUserProfilePaths_ReplacesAccountSegmentOnly()
    {
        var windows = @"C:\Users\alice\Documents\proj\out.gdb";
        Assert.Equal(@"C:\Users\%USERPROFILE%\Documents\proj\out.gdb", LogRedactionPolicy.MaskUserProfilePaths(windows));

        var posix = "/Users/bob/work/data.gdb";
        Assert.Equal("/Users/%USERPROFILE%/work/data.gdb", LogRedactionPolicy.MaskUserProfilePaths(posix));

        // 多账户段：全部替换
        var twice = @"C:\Users\a\C:\Users\b\x";
        Assert.Equal(@"C:\Users\%USERPROFILE%\C:\Users\%USERPROFILE%\x", LogRedactionPolicy.MaskUserProfilePaths(twice));
    }

    [Fact]
    public void MaskUserProfilePaths_LeavesOtherTextUntouched()
    {
        // 非用户目录路径、无账户段的 Users\、空值 → 原样
        Assert.Equal(@"D:\ArcGIS-Pro-MCP 2.0\.runtime\evolution", LogRedactionPolicy.MaskUserProfilePaths(@"D:\ArcGIS-Pro-MCP 2.0\.runtime\evolution"));
        Assert.Equal(@"C:\Users\", LogRedactionPolicy.MaskUserProfilePaths(@"C:\Users\"));
        Assert.Equal("plain text", LogRedactionPolicy.MaskUserProfilePaths("plain text"));
        Assert.Null(LogRedactionPolicy.MaskUserProfilePaths(null));
        Assert.Equal("  ", LogRedactionPolicy.MaskUserProfilePaths("  "));

        // 不受大小写影响（users / USERS）
        Assert.Equal(@"C:\USERS\%USERPROFILE%\x", LogRedactionPolicy.MaskUserProfilePaths(@"C:\USERS\carol\x"));
    }
}
