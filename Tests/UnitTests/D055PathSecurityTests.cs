using System.Diagnostics;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-055（Phase 13 第二批 · 13.1 路径安全加固）行为测试。
/// 全部**黑盒经工具**（守卫类为 internal，不经 InternalsVisibleTo 暴露）：
/// <list type="number">
/// <item><b>reparse point（junction/symlink）→ 拒绝</b>：工作区内建 junction 指向普通目录，
/// 输出经由 junction 写出 ⇒ 守卫命中 <c>reparse-point</c> 且**零 GP 调用**；对照组（同结构真实目录）放行。</item>
/// <item><b>规范化后二次判界 / 允许范围白名单</b>：<c>ARCGIS_PRO_MCP_ALLOWED_ROOTS</c> 配置后，
/// 白名单外拒绝、白名单内放行（该判定在 full-path resolve 之后执行）。</item>
/// <item><b>混合分隔符 + `..`</b>：以正斜杠与 <c>..</c> 书写的路径仍被受保护根判定命中。</item>
/// <item><b>export 写类接入守卫</b>：受保护根 → <c>PATH_ESCAPE_REJECTED</c> 且**宿主未被调用**（零文件写入）；正常路径 → 宿主被调用。</item>
/// </list>
/// **并行隔离**：本类用例会改写进程级环境变量，故归入 <see cref="EnvSensitiveCollection"/>
/// （<c>DisableParallelization = true</c>），避免与其它集合并行时互相干扰。
/// </summary>
[Collection(EnvSensitiveCollection.Name)]
public sealed class D055PathSecurityTests
{
    private const string ProtectedRootsVariable = "ARCGIS_PRO_MCP_PROTECTED_ROOTS";
    private const string AllowedRootsVariable = "ARCGIS_PRO_MCP_ALLOWED_ROOTS";

    private static async Task<OperationResult<object?>> CallAsync(
        FakeArcGISHost host, IMCPTool tool, IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    private static async Task<OperationResult<object?>> CallBufferAsync(RecordingGeoprocessingService service, string output)
        => await CallAsync(new FakeArcGISHost(geoprocessing: service), new BufferTool(),
            new Dictionary<string, object?> { ["input"] = "in_fc", ["output"] = output, ["distance"] = 10.0 });

    /// <summary>用 cmd 的 <c>mklink /J</c> 建目录 junction（无需特权；.NET 无内置 junction API）。</summary>
    private static bool TryCreateJunction(string linkPath, string targetPath)
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };
        p.Start();
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0 && Directory.Exists(linkPath);
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "d055-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ---------------- 1) reparse point ----------------

    [Fact]
    public async Task ReparsePointAncestor_IsRejectedWithZeroGp()
    {
        var root = TempDir();
        var realTarget = Path.Combine(root, "target");
        Directory.CreateDirectory(realTarget);
        var junction = Path.Combine(root, "link");
        try
        {
            Assert.True(TryCreateJunction(junction, realTarget), "junction 构造失败（测试环境不支持 mklink /J）");
            var service = new RecordingGeoprocessingService();
            var result = await CallBufferAsync(service, Path.Combine(junction, "out.gdb"));

            Assert.False(result.Success);
            Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
            Assert.Empty(service.Requests);   // 守卫先于 GP：零调用
        }
        finally
        {
            try { Directory.Delete(junction, false); } catch (IOException) { }
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task PlainSubdirectory_IsNotRejected()
    {
        // 对照组：与 reparse 用例同结构，但为**真实目录** ⇒ 必须放行到 GP（证明判别点是 reparse，而非路径形状）
        var root = TempDir();
        var plain = Path.Combine(root, "plain");
        Directory.CreateDirectory(plain);
        try
        {
            var service = new RecordingGeoprocessingService();
            var result = await CallBufferAsync(service, Path.Combine(plain, "out.gdb"));

            Assert.True(result.Success, result.Errors.Count > 0 ? Assert.Single(result.Errors).Message : "expected success");
            Assert.Single(service.Requests);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    // ---------------- 2) 允许范围白名单（full-path resolve 后判定） ----------------

    [Fact]
    public async Task AllowedRoots_OutsideConfiguredRoot_IsRejected()
    {
        var allowed = TempDir();
        var previous = Environment.GetEnvironmentVariable(AllowedRootsVariable);
        try
        {
            Environment.SetEnvironmentVariable(AllowedRootsVariable, allowed);
            var service = new RecordingGeoprocessingService();
            // D 盘项目内路径：不在白名单（临时目录）内 ⇒ 拒绝
            var result = await CallBufferAsync(service,
                @"D:\ArcGIS-Pro-MCP 2.0\.runtime\evolution\phase13\run-20260917-d055-stage1\allowedroots-out.gdb");

            Assert.False(result.Success);
            Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
            Assert.Empty(service.Requests);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AllowedRootsVariable, previous);
            try { Directory.Delete(allowed, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task AllowedRoots_InsideConfiguredRoot_IsAllowed()
    {
        var allowed = TempDir();
        var previous = Environment.GetEnvironmentVariable(AllowedRootsVariable);
        try
        {
            Environment.SetEnvironmentVariable(AllowedRootsVariable, allowed);
            var service = new RecordingGeoprocessingService();
            var result = await CallBufferAsync(service, Path.Combine(allowed, "sub", "out.gdb"));

            Assert.True(result.Success, result.Errors.Count > 0 ? Assert.Single(result.Errors).Message : "expected success");
            Assert.Single(service.Requests);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AllowedRootsVariable, previous);
            try { Directory.Delete(allowed, true); } catch (IOException) { }
        }
    }

    // ---------------- 3) 混合分隔符 + .. 仍命中受保护根 ----------------

    [Fact]
    public async Task MixedSeparatorsAndDotDot_IntoProtectedRoot_IsRejected()
    {
        var protectedRoot = TempDir();
        var previous = Environment.GetEnvironmentVariable(ProtectedRootsVariable);
        try
        {
            Environment.SetEnvironmentVariable(ProtectedRootsVariable, protectedRoot);
            var service = new RecordingGeoprocessingService();
            // 正斜杠 + ".." 绕行写法；归一化后落回受保护根内
            var sneaky = protectedRoot.Replace('\\', '/') + "/sub/../out.gdb";
            var result = await CallBufferAsync(service, sneaky);

            Assert.False(result.Success);
            Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
            Assert.Empty(service.Requests);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProtectedRootsVariable, previous);
            try { Directory.Delete(protectedRoot, true); } catch (IOException) { }
        }
    }

    // ---------------- 4) export 写类接入守卫 ----------------

    [Fact]
    public async Task ExportLayout_ProtectedOutput_IsRejectedWithoutHostCall()
    {
        var called = false;
        var layout = new NotImplementedService
        {
            ExportLayoutHook = (_, _, _, _, _) =>
            {
                called = true;
                return OperationResult<LayoutExportInfo>.Fail(ErrorCodes.InternalError, "hook-should-not-run");
            },
        };
        var host = new FakeArcGISHost(layout: layout);
        var result = await CallAsync(host, new ExportLayoutPdfTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L1",
            ["outputPath"] = @"D:\ArcGIS-Pro-MCP 2.0\TestFixtures\Phase8_5_6\should-not-write.pdf",
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PathEscapeRejected, Assert.Single(result.Errors).Code);
        Assert.False(called);   // 守卫先于导出：宿主未被调用 ⇒ 零文件写入
    }

    [Fact]
    public async Task ExportLayout_NormalOutput_ReachesHost()
    {
        var called = false;
        var layout = new NotImplementedService
        {
            ExportLayoutHook = (_, _, _, _, _) =>
            {
                called = true;
                return OperationResult<LayoutExportInfo>.Fail(ErrorCodes.InternalError, "hook-reached");
            },
        };
        var host = new FakeArcGISHost(layout: layout);
        var normal = Path.Combine(TempDir(), "layout.pdf");
        var result = await CallAsync(host, new ExportLayoutPdfTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L1",
            ["outputPath"] = normal,
        });

        Assert.True(called);
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InternalError, Assert.Single(result.Errors).Code);
    }
}
