using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>D-047（Phase 10 第五批 · 收官）：布局导出公共基类（工具层业务校验，**非 schema 层**）。</summary>
public abstract class LayoutExportToolBase : McpToolBase
{
    /// <summary>导出格式（PDF / PNG）。</summary>
    protected abstract string ExportFormat { get; }

    /// <summary>要求的扩展名（主扩展名；副名见 <see cref="AcceptedExtensions"/>）。</summary>
    protected abstract string RequiredExtension { get; }

    /// <summary>
    /// D-061：可接受的输出扩展名列表（默认仅主扩展名）。
    /// 多后缀格式（如 TIFF 的 <c>.tif</c>/<c>.tiff</c>、JPEG 的 <c>.jpg</c>/<c>.jpeg</c>）在此追加别名。
    /// </summary>
    protected virtual IReadOnlyList<string> AcceptedExtensions => new[] { RequiredExtension };

    /// <summary>D-061：可接受扩展名的展示串（<c>.jpg / .jpeg</c>）。</summary>
    private string JoinedExtensions => string.Join(" / ", AcceptedExtensions);

    public override IReadOnlyDictionary<string, object?> InputSchema => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>
        {
            ["layoutName"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Layout name." },
            ["outputPath"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "Absolute output file path (" + JoinedExtensions + ")." },
            ["resolution"] = new Dictionary<string, object?> { ["type"] = "number", ["description"] = "Optional DPI (> 0); SDK default 96." },
            ["overwrite"] = new Dictionary<string, object?> { ["type"] = "boolean", ["description"] = "Required (true) to replace an existing file; default false." },
        },
        ["required"] = new[] { "layoutName", "outputPath" }
    };

    protected override string CategoryName => ToolCategories.Layout;
    protected override string ExecutionTypeName => ExecutionTypes.Native;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var layoutName = ToolArgs.GetString(context, "layoutName");
        if (string.IsNullOrWhiteSpace(layoutName))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layoutName is required.");
        }

        var outputPath = ToolArgs.GetString(context, "outputPath");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "outputPath is required.");
        }

        // 工具层业务校验（非 schema 层）：扩展名与声明格式一致
        var path = outputPath.Trim();

        // D-055 A2（13.1 路径安全加固）：写类调用点统一前置守卫。
        // 导出会**真实落盘文件**，故与 GP 写类同规格：受保护根（fixture / 旧仓库 / 环境变量追加根）→ 拒绝；
        // 并叠加 D-055 加固项：reparse point（junction/symlink）拒绝 + 规范化后二次判界 + 可选允许范围白名单。
        var protectedHit = ProtectedOutputPathGuard.Match(path);
        if (protectedHit is not null)
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.PathEscapeRejected,
                $"outputPath '{path}' was refused by the protected-path guard ('{protectedHit}'); no file was written.");
        }

        if (!AcceptedExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                $"outputPath must end with '{JoinedExtensions}' for format {ExportFormat}.");
        }

        var resolution = ToolArgs.GetDouble(context, "resolution");
        if (resolution is not null && (double.IsNaN(resolution.Value) || resolution.Value <= 0))
        {
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument, "resolution must be a positive number (DPI).");
        }

        var overwrite = ToolArgs.GetBool(context, "overwrite");

        var rasterSources = await context.Host!.Layout
            .GetRasterSourcePathsAsync(layoutName, context.CancellationToken).ConfigureAwait(false);
        if (!rasterSources.Success || rasterSources.Data is null)
        {
            return OperationResult<object?>.Fail(rasterSources.Errors!);
        }

        var qualification = await RasterQualificationGate.ValidatePathsAsync(
            context.Python, rasterSources.Data, context.CancellationToken).ConfigureAwait(false);
        if (!qualification.Success)
        {
            return OperationResult<object?>.Fail(qualification.Errors!);
        }

        var r = await context.Host!.Layout
            .ExportLayoutAsync(layoutName, path, ExportFormat, resolution, overwrite, context.CancellationToken)
            .ConfigureAwait(false);
        return r.Success ? OperationResult<object?>.Ok(r.Data) : OperationResult<object?>.Fail(r.Errors);
    }
}

/// <summary>D-047：导出布局到 PDF（写 · Native · 零 GP）。</summary>
public sealed class ExportLayoutPdfTool : LayoutExportToolBase
{
    public override string Name => "export_layout_pdf";
    protected override string ExportFormat => "PDF";
    protected override string RequiredExtension => ".pdf";
    public override string Description =>
        "导出布局为 **PDF 文件**（写操作 = **新建文件**；可复原 = 删除产物）。" +
        "参数：layoutName、outputPath（须以 `.pdf` 结尾）、可选 resolution（DPI，> 0；缺省用 SDK 默认 96）、" +
        "overwrite（**默认 false = 不覆盖既有文件**；已存在且未授权 → `OUTPUT_EXISTS`，不写入任何内容）。" +
        "**参数面收窄披露（spike）**：`width`/`height` **不在参数面** —— SDK 文档明确其「仅适用地图视图导出，布局导出时被忽略」。**地图级导出不在本批**" +
        "（依赖活动地图视图，工程态不可达，见 O-D047-01）。" +
        "**路径守卫（D-055 A2 / 13.1）**：输出路径落在受保护根（`TestFixtures` / 旧仓库 / 环境变量追加根）→ `PATH_ESCAPE_REJECTED`，" +
        "**不写任何文件**；并叠加 reparse point（junction/symlink）拒绝、规范化后二次判界、可选允许范围白名单。" +
        "布局不存在 → `LAYER_NOT_FOUND`（消息明示 Layout）；扩展名不匹配 / 非法 resolution / 空白参数 → `INVALID_ARGUMENT`。" +
        "成功返回产物事实：**字节数 + 文件头（magic bytes 十六进制）**，供调用方校验 `%PDF-`。零 GP。";
}

/// <summary>D-047：导出布局到 PNG（写 · Native · 零 GP）。</summary>
public sealed class ExportLayoutPngTool : LayoutExportToolBase
{
    public override string Name => "export_layout_png";
    protected override string ExportFormat => "PNG";
    protected override string RequiredExtension => ".png";
    public override string Description =>
        "导出布局为 **PNG 图片**（写操作 = **新建文件**；可复原 = 删除产物）。" +
        "参数：layoutName、outputPath（须以 `.png` 结尾）、可选 resolution（DPI，> 0；缺省用 SDK 默认 96）、" +
        "overwrite（**默认 false = 不覆盖既有文件**；已存在且未授权 → `OUTPUT_EXISTS`，不写入任何内容）。" +
        "**参数面收窄披露（spike）**：`width`/`height` **不在参数面** —— SDK 文档明确其「仅适用地图视图导出，布局导出时被忽略」（PNG 像素尺寸由页面尺寸 × DPI 决定）。" +
        "**尺寸判据（D-047 实测 / D-055 LIVE 复核）**：像素尺寸 = 页面尺寸 × resolution；本仓受控载体 L_Map 为 11 × 8.5 in，" +
        "缺省 96 DPI ⇒ **1056 × 816 px**（D-055 LIVE A6 实测一致），可作为 DPI 是否生效的判据。" +
        "**路径守卫（D-055 A2 / 13.1）**：输出路径落在受保护根（`TestFixtures` / 旧仓库 / 环境变量追加根）→ `PATH_ESCAPE_REJECTED`，" +
        "**不写任何文件**；并叠加 reparse point（junction/symlink）拒绝、规范化后二次判界、可选允许范围白名单。" +
        "**地图级导出不在本批**（依赖活动地图视图，工程态不可达，见 O-D047-01）。" +
        "布局不存在 → `LAYER_NOT_FOUND`；扩展名不匹配 / 非法 resolution / 空白参数 → `INVALID_ARGUMENT`。" +
        "成功返回产物事实：**字节数 + 文件头（十六进制）**，供调用方校验 `\\x89PNG`。零 GP。";
}

// ---------------------------------------------------------------------------
// D-061（功能完善第一批）：布局导出四新格式 —— 复用同一基类与同一套路径守卫 + 覆写闸门。
// ---------------------------------------------------------------------------

/// <summary>D-061：导出布局到 JPEG（写 · Native · 零 GP）。</summary>
public sealed class ExportLayoutJpgTool : LayoutExportToolBase
{
    public override string Name => "export_layout_jpg";
    protected override string ExportFormat => "JPEG";
    protected override string RequiredExtension => ".jpg";
    protected override IReadOnlyList<string> AcceptedExtensions => new[] { ".jpg", ".jpeg" };

    public override string Description =>
        "导出布局为 **JPEG 图片**（写操作 = **新建文件**；可复原 = 删除产物）。" +
        "参数：layoutName、outputPath（须以 `.jpg` 或 `.jpeg` 结尾）、可选 resolution（DPI，> 0；缺省用 SDK 默认 96）、" +
        "overwrite（**默认 false = 不覆盖既有文件**；已存在且未授权 → `OUTPUT_EXISTS`，不写入任何内容）。" +
        "**路径守卫（D-055 A2 / 13.1）**：输出路径落在受保护根（`TestFixtures` / 旧仓库 / 环境变量追加根）→ `PATH_ESCAPE_REJECTED`，" +
        "**不写任何文件**；并叠加 reparse point（junction/symlink）拒绝、规范化后二次判界、可选允许范围白名单。" +
        "布局不存在 → `LAYER_NOT_FOUND`；扩展名不匹配 / 非法 resolution / 空白参数 → `INVALID_ARGUMENT`。" +
        "成功返回产物事实：**字节数 + 文件头（十六进制）**，供调用方校验 JPEG 魔数 `\\xFF\\xD8`。零 GP。";
}

/// <summary>D-061：导出布局到 TIFF（写 · Native · 零 GP）。</summary>
public sealed class ExportLayoutTifTool : LayoutExportToolBase
{
    public override string Name => "export_layout_tif";
    protected override string ExportFormat => "TIFF";
    protected override string RequiredExtension => ".tif";
    protected override IReadOnlyList<string> AcceptedExtensions => new[] { ".tif", ".tiff" };

    public override string Description =>
        "导出布局为 **TIFF 图片**（写操作 = **新建文件**；可复原 = 删除产物）。" +
        "参数：layoutName、outputPath（须以 `.tif` 或 `.tiff` 结尾）、可选 resolution（DPI，> 0；缺省用 SDK 默认 96）、" +
        "overwrite（**默认 false = 不覆盖既有文件**；已存在且未授权 → `OUTPUT_EXISTS`，不写入任何内容）。" +
        "**路径守卫（D-055 A2 / 13.1）**：输出路径落在受保护根 → `PATH_ESCAPE_REJECTED`，**不写任何文件**。" +
        "布局不存在 → `LAYER_NOT_FOUND`；扩展名不匹配 / 非法 resolution / 空白参数 → `INVALID_ARGUMENT`。" +
        "成功返回产物事实：**字节数 + 文件头（十六进制）**，供调用方校验 TIFF 魔数（小端 `II\\x2A\\x00` 或大端 `MM\\x00\\x2A`）。零 GP。";
}

/// <summary>D-061：导出布局到 SVG（写 · Native · 零 GP）。</summary>
public sealed class ExportLayoutSvgTool : LayoutExportToolBase
{
    public override string Name => "export_layout_svg";
    protected override string ExportFormat => "SVG";
    protected override string RequiredExtension => ".svg";

    public override string Description =>
        "导出布局为 **SVG 矢量文件**（写操作 = **新建文件**；可复原 = 删除产物）。" +
        "参数：layoutName、outputPath（须以 `.svg` 结尾）、可选 resolution（DPI，> 0；缺省用 SDK 默认 96）、" +
        "overwrite（**默认 false = 不覆盖既有文件**；已存在且未授权 → `OUTPUT_EXISTS`，不写入任何内容）。" +
        "**如实披露（尚未 LIVE 验证）**：矢量导出为 SDK `SVGFormat` 的原生产物；" +
        "本宿主不对其做内容级校验（仅凭魔数判别），文本/字形是否内嵌由 SDK 决定。**DPI 对矢量图的适用性亦未在本批 LIVE 验证**" +
        "（阶段一尚未实装）；做强化判据请以产物字节数与文件头为准。" +
        "**路径守卫（D-055 A2 / 13.1）**：输出路径落在受保护根 → `PATH_ESCAPE_REJECTED`，**不写任何文件**。" +
        "布局不存在 → `LAYER_NOT_FOUND`；扩展名不匹配 / 非法 resolution / 空白参数 → `INVALID_ARGUMENT`。" +
        "成功返回产物事实：**字节数 + 文件头（十六进制）**，供调用方校验 `<svg`。零 GP。";
}

/// <summary>D-061：导出布局到 EPS（写 · Native · 零 GP）。</summary>
public sealed class ExportLayoutEpsTool : LayoutExportToolBase
{
    public override string Name => "export_layout_eps";
    protected override string ExportFormat => "EPS";
    protected override string RequiredExtension => ".eps";

    public override string Description =>
        "导出布局为 **EPS（Encapsulated PostScript）文件**（写操作 = **新建文件**；可复原 = 删除产物）。" +
        "参数：layoutName、outputPath（须以 `.eps` 结尾）、可选 resolution（DPI，> 0；缺省用 SDK 默认 96）、" +
        "overwrite（**默认 false = 不覆盖既有文件**；已存在且未授权 → `OUTPUT_EXISTS`，不写入任何内容）。" +
        "**如实披露（尚未 LIVE 验证）**：EPS 为 PostScript 描述格式，与位图导出不同；" +
        "本宿主不对其做内容级校验（仅凭魔数判别），**DPI 在 EPS 上的具体语义未在本批 LIVE 验证**（阶段一尚未实装）。" +
        "**路径守卫（D-055 A2 / 13.1）**：输出路径落在受保护根 → `PATH_ESCAPE_REJECTED`，**不写任何文件**。" +
        "布局不存在 → `LAYER_NOT_FOUND`；扩展名不匹配 / 非法 resolution / 空白参数 → `INVALID_ARGUMENT`。" +
        "成功返回产物事实：**字节数 + 文件头（十六进制）**，供调用方校验 `%!PS`。零 GP。";
}
