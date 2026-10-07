using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-047（Phase 10 第五批 · 收官）：布局导出 2 工具的 Fake 层单测。
/// 覆盖：① export_layout_pdf —— 参数透传（layoutName/outputPath/resolution/overwrite）+ 产物事实透出 /
/// 空白 outputPath 校验 / **扩展名不匹配校验（工具层业务校验，非 schema 层 → 反证回退点）**；
/// ② export_layout_png —— 透传 / resolution 非正校验 / 服务 OUTPUT_EXISTS 透出；
/// ③ 缺 layoutName 校验。
/// </summary>
public class LayoutExportToolTests
{
    private static async Task<OperationResult<object?>> CallAsync(
        FakeArcGISHost host, IMCPTool tool, IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    private static FakeArcGISHost HostWithLayout(NotImplementedService layout) => new(layout: layout);

    // ---------- ① export_layout_pdf ----------

    [Fact]
    public async Task ExportPdf_PassesAllParams_AndReturnsArtifactFacts()
    {
        string? seenLayout = null;
        string? seenPath = null;
        string? seenFormat = null;
        double? seenRes = null;
        bool? seenOverwrite = null;
        var layout = new NotImplementedService
        {
            ExportLayoutHook = (name, path, fmt, res, ow) =>
            {
                seenLayout = name; seenPath = path; seenFormat = fmt; seenRes = res; seenOverwrite = ow;
                return OperationResult<LayoutExportInfo>.Ok(new LayoutExportInfo
                {
                    LayoutName = name, OutputPath = path, Format = fmt, Resolution = res,
                    FileSizeBytes = 12345, Overwritten = ow, MagicBytesHex = "255044462D312E37",
                });
            },
        };
        var host = HostWithLayout(layout);

        var r = await CallAsync(host, new ExportLayoutPdfTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L_Map",
            ["outputPath"] = @"D:\out\report.pdf",
            ["resolution"] = 300.0,
            ["overwrite"] = true,
        });

        Assert.True(r.Success);
        Assert.Equal("L_Map", seenLayout);
        Assert.Equal(@"D:\out\report.pdf", seenPath);
        Assert.Equal("PDF", seenFormat);
        Assert.Equal(300.0, seenRes);
        Assert.True(seenOverwrite);
        var info = Assert.IsType<LayoutExportInfo>(r.Data);
        Assert.Equal(12345, info.FileSizeBytes);
        Assert.Equal("255044462D312E37", info.MagicBytesHex);
        Assert.True(info.Overwritten);
    }

    [Fact]
    public async Task ExportPdf_BlankOutputPath_InvalidArgument()
    {
        var host = HostWithLayout(new NotImplementedService());
        var r = await CallAsync(host, new ExportLayoutPdfTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L_Map",
            ["outputPath"] = "   ",
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    /// <summary>★ 反证回退点：工具层业务校验（非 schema required）—— 扩展名与声明格式一致。</summary>
    [Fact]
    public async Task ExportPdf_WrongExtension_InvalidArgument()
    {
        var host = HostWithLayout(new NotImplementedService());
        var r = await CallAsync(host, new ExportLayoutPdfTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L_Map",
            ["outputPath"] = @"D:\out\report.png",
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
        Assert.Contains(".pdf", r.Errors![0].Message);
    }

    [Fact]
    public async Task ExportPdf_InvalidResolution_InvalidArgument()
    {
        var host = HostWithLayout(new NotImplementedService());
        var r = await CallAsync(host, new ExportLayoutPdfTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L_Map",
            ["outputPath"] = @"D:\out\report.pdf",
            ["resolution"] = 0.0,
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    // ---------- ② export_layout_png ----------

    [Fact]
    public async Task ExportPng_PassesParams_AndReturnsArtifactFacts()
    {
        string? seenFormat = null;
        double? seenRes = null;
        bool? seenOverwrite = null;
        var layout = new NotImplementedService
        {
            ExportLayoutHook = (name, path, fmt, res, ow) =>
            {
                seenFormat = fmt; seenRes = res; seenOverwrite = ow;
                return OperationResult<LayoutExportInfo>.Ok(new LayoutExportInfo
                {
                    LayoutName = name, OutputPath = path, Format = fmt, Resolution = res,
                    FileSizeBytes = 4096, Overwritten = ow, MagicBytesHex = "89504E470D0A1A0A",
                });
            },
        };
        var host = HostWithLayout(layout);

        var r = await CallAsync(host, new ExportLayoutPngTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L_Map",
            ["outputPath"] = @"D:\out\map.png",
        });

        Assert.True(r.Success);
        Assert.Equal("PNG", seenFormat);
        Assert.Null(seenRes);
        Assert.False(seenOverwrite);   // 缺省 false（不覆盖）
        var info = Assert.IsType<LayoutExportInfo>(r.Data);
        Assert.Equal("89504E470D0A1A0A", info.MagicBytesHex);
        Assert.Equal(4096, info.FileSizeBytes);
    }

    [Fact]
    public async Task ExportPng_WrongExtension_InvalidArgument()
    {
        var host = HostWithLayout(new NotImplementedService());
        var r = await CallAsync(host, new ExportLayoutPngTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L_Map",
            ["outputPath"] = @"D:\out\map.pdf",
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
        Assert.Contains(".png", r.Errors![0].Message);
    }

    [Fact]
    public async Task ExportPng_NegativeResolution_InvalidArgument()
    {
        var host = HostWithLayout(new NotImplementedService());
        var r = await CallAsync(host, new ExportLayoutPngTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L_Map",
            ["outputPath"] = @"D:\out\map.png",
            ["resolution"] = -5.0,
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    [Fact]
    public async Task ExportPng_OutputExists_RejectionSurfaces()
    {
        var layout = new NotImplementedService
        {
            ExportLayoutHook = (_, path, _, _, _) => OperationResult<LayoutExportInfo>.Fail(
                ErrorCodes.OutputExists,
                $"Output file already exists: {path}. Pass overwrite=true to replace it."),
        };
        var host = HostWithLayout(layout);

        var r = await CallAsync(host, new ExportLayoutPngTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "L_Map",
            ["outputPath"] = @"D:\out\exists.png",
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.OutputExists, r.Errors![0].Code);
        Assert.Contains("overwrite=true", r.Errors![0].Message);
    }

    // ---------- ③ 公共校验 ----------

    [Fact]
    public async Task Export_BlankLayoutName_InvalidArgument()
    {
        var host = HostWithLayout(new NotImplementedService());
        var r = await CallAsync(host, new ExportLayoutPdfTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "  ",
            ["outputPath"] = @"D:\out\x.pdf",
        });
        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.InvalidArgument, r.Errors![0].Code);
    }

    [Fact]
    public async Task Export_LayoutNotFound_SurfacesLayerNotFound()
    {
        var layout = new NotImplementedService
        {
            ExportLayoutHook = (name, _, _, _, _) => OperationResult<LayoutExportInfo>.Fail(
                ErrorCodes.LayerNotFound, $"Layout '{name}' not found."),
        };
        var host = HostWithLayout(layout);

        var r = await CallAsync(host, new ExportLayoutPdfTool(), new Dictionary<string, object?>
        {
            ["layoutName"] = "NoSuchLayout",
            ["outputPath"] = @"D:\out\x.pdf",
        });

        Assert.False(r.Success);
        Assert.Equal(ErrorCodes.LayerNotFound, r.Errors![0].Code);
    }
}
