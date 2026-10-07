using System;
using System.IO;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>布局服务实现（真实枚举工程布局）。</summary>
public sealed partial class LayoutService : ILayoutService
{
    public Task<OperationResult<IReadOnlyList<LayoutInfo>>> GetLayoutsAsync(CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<LayoutInfo>>>(
            () =>
            {
                var layouts = Project.Current.GetItems<LayoutProjectItem>()
                    .Select(item => new LayoutInfo
                    {
                        Name = item.Name ?? string.Empty,
                        Uri = item.Path ?? string.Empty,
                        IsVisible = true
                    })
                    .ToList();
                return OperationResult<IReadOnlyList<LayoutInfo>>.Ok(layouts);
            },
            TaskCreationOptions.None);

    /// <summary>D-042：按名取布局详情。歧义/未找到语义见实现内注释（错误码复用，报告披露）。</summary>
    public Task<OperationResult<LayoutDetailInfo>> GetLayoutInfoAsync(string layoutName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LayoutDetailInfo>>(
            () =>
            {
                var (layout, code, message) = ResolveLayoutByName(layoutName);
                if (layout is null)
                {
                    return OperationResult<LayoutDetailInfo>.Fail(code!, message!);
                }

                var page = layout.GetPage();
                var elements = layout.GetElementsAsFlattenedList().ToList();
                var info = new LayoutDetailInfo
                {
                    Name = layout.Name ?? string.Empty,
                    Uri = layout.URI ?? string.Empty,
                    ElementCount = elements.Count,
                };
                if (page is not null)
                {
                    info.PageWidth = page.Width;
                    info.PageHeight = page.Height;
                    info.PageUnits = page.Units.ToString();
                }
                return OperationResult<LayoutDetailInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    public Task<OperationResult<IReadOnlyList<string>>> GetRasterSourcePathsAsync(
        string layoutName,
        CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<string>>>(
            () =>
            {
                var (layout, code, message) = ResolveLayoutByName(layoutName);
                if (layout is null)
                {
                    return OperationResult<IReadOnlyList<string>>.Fail(code!, message!);
                }

                var paths = new List<string>();
                foreach (var mapFrame in layout.Elements.OfType<MapFrame>())
                {
                    var map = mapFrame.Map;
                    if (map is null)
                    {
                        return OperationResult<IReadOnlyList<string>>.Fail(
                            ErrorCodes.InvalidArgument,
                            $"Cannot resolve the map bound to map frame '{mapFrame.Name}' in layout '{layoutName}'.");
                    }

                    foreach (var raster in LayerResolver.EnumerateLayers(map, flatten: true).OfType<RasterLayer>())
                    {
                        string? path;
                        try
                        {
                            path = raster.GetPath()?.ToString();
                        }
                        catch (Exception ex)
                        {
                            return OperationResult<IReadOnlyList<string>>.Fail(
                                ErrorCodes.InvalidArgument,
                                $"Cannot resolve the raster source for layer '{raster.Name}' in layout '{layoutName}': {ex.Message}");
                        }

                        if (string.IsNullOrWhiteSpace(path))
                        {
                            return OperationResult<IReadOnlyList<string>>.Fail(
                                ErrorCodes.InvalidArgument,
                                $"Cannot resolve the raster source for layer '{raster.Name}' in layout '{layoutName}'.");
                        }

                        paths.Add(path);
                    }
                }

                return OperationResult<IReadOnlyList<string>>.Ok(
                    paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            },
            TaskCreationOptions.None);

    /// <summary>D-042：布局元素清单（展平）。</summary>
    public Task<OperationResult<IReadOnlyList<LayoutElementInfo>>> ListLayoutElementsAsync(string layoutName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<LayoutElementInfo>>>(
            () =>
            {
                var (layout, code, message) = ResolveLayoutByName(layoutName);
                if (layout is null)
                {
                    return OperationResult<IReadOnlyList<LayoutElementInfo>>.Fail(code!, message!);
                }

                var infos = layout.GetElementsAsFlattenedList()
                    .Select(el => new LayoutElementInfo
                    {
                        Name = el.Name ?? string.Empty,
                        ElementType = el.GetType().Name,
                        IsVisible = el.IsVisible,
                        X = SafeGetX(el),
                        Y = SafeGetY(el),
                    })
                    .ToList();
                return OperationResult<IReadOnlyList<LayoutElementInfo>>.Ok(infos);
            },
            TaskCreationOptions.None);

    private static double? SafeGetX(ArcGIS.Desktop.Layouts.Element el)
    {
        try { return el.GetX(); } catch { return null; }
    }

    private static double? SafeGetY(ArcGIS.Desktop.Layouts.Element el)
    {
        try { return el.GetY(); } catch { return null; }
    }

    /// <summary>布局按名解析。0 命中 → LAYER_NOT_FOUND（消息明示 Layout）；同名多布局 → AMBIGUOUS_LAYER_NAME + 候选。
    /// 错误码复用（零新增，D-042 报告披露：布局属工程项而非地图图层，Keeper 如认为需专用码再裁定）。</summary>
    private static (ArcGIS.Desktop.Layouts.Layout? Layout, string Code, string Message) ResolveLayoutByName(string? layoutName)
    {
        if (string.IsNullOrWhiteSpace(layoutName))
        {
            return (null, ErrorCodes.InvalidArgument, "layoutName is required.");
        }

        var items = Project.Current.GetItems<ArcGIS.Desktop.Layouts.LayoutProjectItem>().ToList();
        var matches = new List<ArcGIS.Desktop.Layouts.Layout>();
        var candidates = new List<string>();
        foreach (var item in items)
        {
            if (string.Equals(item.Name, layoutName, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var ly = item.GetLayout();
                    if (ly is not null)
                    {
                        matches.Add(ly);
                        candidates.Add(ly.URI ?? item.Path ?? item.Name ?? string.Empty);
                    }
                }
                catch
                {
                    // GetLayout 失败的项跳过（不计入命中也不视为致命）。
                }
            }
        }

        if (matches.Count == 0)
        {
            return (null, ErrorCodes.LayerNotFound, $"Layout '{layoutName}' not found.");
        }
        if (matches.Count > 1)
        {
            return (null, ErrorCodes.AmbiguousLayerName,
                $"Layout name '{layoutName}' is ambiguous ({matches.Count} matches). Candidates: " +
                string.Join(" | ", candidates));
        }
        return (matches[0], ErrorCodes.LayerNotFound, string.Empty);
    }

    // ================= D-045：布局构建（写类首批，5 工具） =================

    /// <summary>页面单位解析（spike 确证：LinearUnit.Inches/Centimeters/Millimeters/Points）。</summary>
    private static bool TryResolvePageUnits(string? units, out ArcGIS.Core.Geometry.LinearUnit? unit, out string error)
    {
        unit = null;
        error = string.Empty;
        var u = (units ?? string.Empty).Trim().ToUpperInvariant();
        switch (u)
        {
            case "":
            case "INCH":
            case "INCHES":
                unit = ArcGIS.Core.Geometry.LinearUnit.Inches;
                return true;
            case "CENTIMETER":
            case "CENTIMETERS":
            case "CM":
                unit = ArcGIS.Core.Geometry.LinearUnit.Centimeters;
                return true;
            case "MILLIMETER":
            case "MILLIMETERS":
            case "MM":
                unit = ArcGIS.Core.Geometry.LinearUnit.Millimeters;
                return true;
            case "POINT":
            case "POINTS":
            case "PT":
                unit = ArcGIS.Core.Geometry.LinearUnit.Points;
                return true;
            default:
                error = "pageUnits must be one of: Inches, Centimeters, Millimeters, Points.";
                return false;
        }
    }

    private static string PageUnitsName(ArcGIS.Core.Geometry.LinearUnit unit)
    {
        if (unit is null)
        {
            return string.Empty;
        }

        var n = unit.Name ?? string.Empty;
        if (n.StartsWith("Inch", StringComparison.OrdinalIgnoreCase))
        {
            return "Inches";
        }

        if (n.StartsWith("Centimeter", StringComparison.OrdinalIgnoreCase))
        {
            return "Centimeters";
        }

        if (n.StartsWith("Millimeter", StringComparison.OrdinalIgnoreCase))
        {
            return "Millimeters";
        }

        if (n.StartsWith("Point", StringComparison.OrdinalIgnoreCase))
        {
            return "Points";
        }

        return n;
    }

    /// <summary>元素默认命名：&lt;prefix&gt;_&lt;n&gt;，n 从 1 起取未占用的最小序号。</summary>
    private static string DefaultElementName(ArcGIS.Desktop.Layouts.Layout layout, string prefix)
    {
        for (var i = 1; i < 1000; i++)
        {
            var candidate = prefix + "_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (layout.FindElement(candidate) is null)
            {
                return candidate;
            }
        }

        return prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    private static bool TryPageBounds(ArcGIS.Desktop.Layouts.Layout layout, double x, double y, out string error)
    {
        error = string.Empty;
        var page = layout.GetPage();
        if (page is null)
        {
            return true;
        }

        if (x < 0 || y < 0 || x > page.Width || y > page.Height)
        {
            error = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "element position ({0}, {1}) is outside the page bounds (0..{2} x 0..{3}).",
                x, y, page.Width, page.Height);
            return false;
        }

        return true;
    }

    /// <summary>解析地图框元素（必须存在且为 MapFrame）。</summary>
    private static bool TryResolveMapFrame(
        ArcGIS.Desktop.Layouts.Layout layout,
        string mapFrameName,
        out ArcGIS.Desktop.Layouts.MapFrame? mapFrame,
        out string error)
    {
        mapFrame = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(mapFrameName))
        {
            error = "mapFrameName is required.";
            return false;
        }

        var element = layout.FindElement(mapFrameName);
        if (element is null)
        {
            error = $"Map frame '{mapFrameName}' not found in layout '{layout.Name}'.";
            return false;
        }

        if (element is not ArcGIS.Desktop.Layouts.MapFrame mf)
        {
            error = $"Element '{mapFrameName}' is not a map frame ({element.GetType().Name}).";
            return false;
        }

        mapFrame = mf;
        return true;
    }

    private static ArcGIS.Core.Geometry.Envelope BuildPageEnvelope(double x, double y, double width, double height)
    {
        var b = new ArcGIS.Core.Geometry.EnvelopeBuilderEx(x, y, x + width, y + height, null);
        return b.ToGeometry();
    }

    public Task<OperationResult<LayoutCreateInfo>> CreateLayoutAsync(
        string name,
        double pageWidth,
        double pageHeight,
        string pageUnits,
        string? mapName,
        string? mapFrameName,
        double? frameXMin,
        double? frameYMin,
        double? frameXMax,
        double? frameYMax,
        CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LayoutCreateInfo>>(
            () =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return OperationResult<LayoutCreateInfo>.Fail(ErrorCodes.InvalidArgument, "name is required.");
                }

                if (double.IsNaN(pageWidth) || double.IsNaN(pageHeight) || pageWidth <= 0 || pageHeight <= 0)
                {
                    return OperationResult<LayoutCreateInfo>.Fail(
                        ErrorCodes.InvalidArgument, "pageWidth and pageHeight must be positive numbers.");
                }

                if (!TryResolvePageUnits(pageUnits, out var unit, out var unitsError))
                {
                    return OperationResult<LayoutCreateInfo>.Fail(ErrorCodes.InvalidArgument, unitsError);
                }

                // plan §7：默认新建自有布局，不改用户原布局 ⇒ 同名拒绝（不静默改名）。
                var existing = Project.Current.GetItems<ArcGIS.Desktop.Layouts.LayoutProjectItem>()
                    .Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
                if (existing)
                {
                    return OperationResult<LayoutCreateInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"Layout '{name}' already exists; refusing to create (no silent rename).");
                }

                ArcGIS.Desktop.Mapping.Map? map = null;
                if (!string.IsNullOrWhiteSpace(mapName))
                {
                    var resolved = MapResolver.Resolve(mapName);
                    if (MapResolver.FailIfNotOk<LayoutCreateInfo>(resolved, mapName) is { } mapFail)
                    {
                        return mapFail;
                    }

                    map = resolved.Map!;
                }

                var layout = LayoutFactory.Instance.CreateLayout(pageWidth, pageHeight, unit!, false, 0);
                if (layout is null)
                {
                    return OperationResult<LayoutCreateInfo>.Fail(
                        ErrorCodes.InternalError, "LayoutFactory.CreateLayout returned null.");
                }

                layout.SetName(name);

                string? frameName = null;
                if (map is not null)
                {
                    ArcGIS.Core.Geometry.Envelope frameEnv;
                    if (frameXMin is not null && frameYMin is not null && frameXMax is not null && frameYMax is not null)
                    {
                        if (!(frameXMin.Value < frameXMax.Value) || !(frameYMin.Value < frameYMax.Value))
                        {
                            return OperationResult<LayoutCreateInfo>.Fail(
                                ErrorCodes.InvalidArgument,
                                "map frame extent is invalid: require frameXMin < frameXMax and frameYMin < frameYMax.");
                        }

                        frameEnv = BuildPageEnvelope(frameXMin.Value, frameYMin.Value,
                            frameXMax.Value - frameXMin.Value, frameYMax.Value - frameYMin.Value);
                    }
                    else
                    {
                        // 缺省地图框：页面内缩 1 个页面单位（spike 披露的默认形态）。
                        frameEnv = BuildPageEnvelope(1, 1, Math.Max(0.1, pageWidth - 2), Math.Max(0.1, pageHeight - 2));
                    }

                    frameName = string.IsNullOrWhiteSpace(mapFrameName) ? "Map Frame" : mapFrameName!.Trim();
                    ElementFactory.Instance.CreateMapFrameElement(layout, frameEnv, map, frameName, false, null);
                }

                var page = layout.GetPage();
                var info = new LayoutCreateInfo
                {
                    Name = layout.Name ?? name,
                    PageWidth = page?.Width ?? pageWidth,
                    PageHeight = page?.Height ?? pageHeight,
                    PageUnits = PageUnitsName(page?.Units ?? unit!),
                    ElementCount = layout.Elements?.Count() ?? 0,
                    MapFrameName = frameName,
                };
                return OperationResult<LayoutCreateInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    public Task<OperationResult<LayoutElementAddInfo>> AddLayoutTextAsync(
        string layoutName,
        string text,
        double x,
        double y,
        double? fontSize,
        string? fontFamily,
        string? elementName,
        CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LayoutElementAddInfo>>(
            () =>
            {
                var (layout, code, message) = ResolveLayoutByName(layoutName);
                if (layout is null)
                {
                    return OperationResult<LayoutElementAddInfo>.Fail(code, message);
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    return OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.InvalidArgument, "text is required.");
                }

                if (double.IsNaN(x) || double.IsNaN(y))
                {
                    return OperationResult<LayoutElementAddInfo>.Fail(
                        ErrorCodes.InvalidArgument, "x and y must be numbers.");
                }

                if (!TryPageBounds(layout, x, y, out var boundsError))
                {
                    return OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.InvalidArgument, boundsError);
                }

                var size = fontSize ?? 12.0;
                if (size <= 0 || double.IsNaN(size))
                {
                    return OperationResult<LayoutElementAddInfo>.Fail(
                        ErrorCodes.InvalidArgument, "fontSize must be a positive number (points).");
                }

                var color = ColorFactory.Instance.BlackRGB;
                var symbol = string.IsNullOrWhiteSpace(fontFamily)
                    ? SymbolFactory.Instance.ConstructTextSymbol(color, size)
                    : SymbolFactory.Instance.ConstructTextSymbol(color, size, fontFamily!.Trim());

                var point = ArcGIS.Core.Geometry.MapPointBuilderEx.CreateMapPoint(x, y);
                var name = string.IsNullOrWhiteSpace(elementName) ? DefaultElementName(layout, "Text") : elementName!.Trim();
                var element = ElementFactory.Instance.CreateTextGraphicElement(
                    layout, TextType.PointText, point, symbol, text, name, false, null);

                var info = new LayoutElementAddInfo
                {
                    LayoutName = layout.Name ?? layoutName ?? string.Empty,
                    ElementName = element?.Name ?? name,
                    ElementType = element?.GetType().Name ?? nameof(TextElement),
                    X = x,
                    Y = y,
                    AnchorMapFrame = null,
                    ElementCount = layout.Elements?.Count() ?? 0,
                };
                return OperationResult<LayoutElementAddInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    private Task<OperationResult<LayoutElementAddInfo>> AddMapSurroundAsync(
        string layoutName,
        string mapFrameName,
        double x,
        double y,
        double? width,
        double? height,
        string? elementName,
        ArcGIS.Desktop.Layouts.MapSurroundType surroundType,
        string defaultPrefix,
        double defaultWidth,
        double defaultHeight)
        => QueuedTask.Run<OperationResult<LayoutElementAddInfo>>(
            () =>
            {
                var (layout, code, message) = ResolveLayoutByName(layoutName);
                if (layout is null)
                {
                    return OperationResult<LayoutElementAddInfo>.Fail(code, message);
                }

                if (!TryResolveMapFrame(layout, mapFrameName, out var mapFrame, out var frameError))
                {
                    return OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.InvalidArgument, frameError);
                }

                if (double.IsNaN(x) || double.IsNaN(y))
                {
                    return OperationResult<LayoutElementAddInfo>.Fail(
                        ErrorCodes.InvalidArgument, "x and y must be numbers.");
                }

                if (!TryPageBounds(layout, x, y, out var boundsError))
                {
                    return OperationResult<LayoutElementAddInfo>.Fail(ErrorCodes.InvalidArgument, boundsError);
                }

                var w = width ?? defaultWidth;
                var h = height ?? defaultHeight;
                if (w <= 0 || h <= 0 || double.IsNaN(w) || double.IsNaN(h))
                {
                    return OperationResult<LayoutElementAddInfo>.Fail(
                        ErrorCodes.InvalidArgument, "width and height must be positive numbers (page units).");
                }

                ArcGIS.Desktop.Layouts.MapSurroundInfo info = surroundType switch
                {
                    ArcGIS.Desktop.Layouts.MapSurroundType.Legend => new ArcGIS.Desktop.Layouts.LegendInfo(),
                    ArcGIS.Desktop.Layouts.MapSurroundType.NorthArrow => new ArcGIS.Desktop.Layouts.NorthArrowInfo(),
                    _ => new ArcGIS.Desktop.Layouts.ScaleBarInfo(),
                };
                info.MapFrameName = mapFrame!.Name;

                var env = BuildPageEnvelope(x, y, w, h);
                var name = string.IsNullOrWhiteSpace(elementName) ? DefaultElementName(layout, defaultPrefix) : elementName!.Trim();
                var element = ElementFactory.Instance.CreateMapSurroundElement(layout, env, info, name, false, null);

                var result = new LayoutElementAddInfo
                {
                    LayoutName = layout.Name ?? layoutName ?? string.Empty,
                    ElementName = element?.Name ?? name,
                    ElementType = element?.GetType().Name ?? surroundType.ToString(),
                    X = x,
                    Y = y,
                    AnchorMapFrame = mapFrame.Name,
                    ElementCount = layout.Elements?.Count() ?? 0,
                };
                return OperationResult<LayoutElementAddInfo>.Ok(result);
            },
            TaskCreationOptions.None);

    public Task<OperationResult<LayoutElementAddInfo>> AddLegendAsync(
        string layoutName, string mapFrameName, double x, double y,
        double? width, double? height, string? elementName, CancellationToken ct = default)
        => AddMapSurroundAsync(layoutName, mapFrameName, x, y, width, height, elementName,
            ArcGIS.Desktop.Layouts.MapSurroundType.Legend, "Legend", 3.0, 2.0);

    public Task<OperationResult<LayoutElementAddInfo>> AddNorthArrowAsync(
        string layoutName, string mapFrameName, double x, double y,
        double? width, double? height, string? elementName, CancellationToken ct = default)
        => AddMapSurroundAsync(layoutName, mapFrameName, x, y, width, height, elementName,
            ArcGIS.Desktop.Layouts.MapSurroundType.NorthArrow, "NorthArrow", 1.0, 1.0);

    public Task<OperationResult<LayoutElementAddInfo>> AddScaleBarAsync(
        string layoutName, string mapFrameName, double x, double y,
        double? width, double? height, string? elementName, CancellationToken ct = default)
        => AddMapSurroundAsync(layoutName, mapFrameName, x, y, width, height, elementName,
            ArcGIS.Desktop.Layouts.MapSurroundType.ScaleBar, "ScaleBar", 2.0, 0.5);

    /// <summary>D-047：导出布局到 PDF/PNG（MCT 内；写后核产物 magic bytes + 字节数）。</summary>
    public Task<OperationResult<LayoutExportInfo>> ExportLayoutAsync(
        string layoutName,
        string outputPath,
        string format,
        double? resolution,
        bool overwrite,
        CancellationToken ct = default)
        => ExportLayoutCoreAsync(layoutName, outputPath, format, resolution, overwrite, null, ct);

    /// <summary>D-086：显式导出选项；透明背景仅对 PNGFormat 生效。</summary>
    public Task<OperationResult<LayoutExportInfo>> ExportLayoutWithOptionsAsync(
        string layoutName,
        string outputPath,
        string format,
        double? resolution,
        bool overwrite,
        bool? transparentBackground,
        CancellationToken ct = default)
        => ExportLayoutCoreAsync(layoutName, outputPath, format, resolution, overwrite, transparentBackground, ct);

    private Task<OperationResult<LayoutExportInfo>> ExportLayoutCoreAsync(
        string layoutName,
        string outputPath,
        string format,
        double? resolution,
        bool overwrite,
        bool? transparentBackground,
        CancellationToken ct)
        => QueuedTask.Run<OperationResult<LayoutExportInfo>>(
            () =>
            {
                var (layout, code, message) = ResolveLayoutByName(layoutName);
                if (layout is null)
                {
                    return OperationResult<LayoutExportInfo>.Fail(code, message);
                }

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    return OperationResult<LayoutExportInfo>.Fail(
                        ErrorCodes.InvalidArgument, "outputPath is required.");
                }

                if (resolution is not null && (double.IsNaN(resolution.Value) || resolution.Value <= 0))
                {
                    return OperationResult<LayoutExportInfo>.Fail(
                        ErrorCodes.InvalidArgument, "resolution must be a positive number (DPI).");
                }

                // D-061：格式族扩展为 PDF / PNG / JPEG / TIFF / SVG / EPS（多后缀格式给别名列表）。
                var fmt = (format ?? string.Empty).Trim().ToUpperInvariant();
                string[] allowedExts;
                ExportFormat exportFormat;
                switch (fmt)
                {
                    case "PDF":
                        allowedExts = new[] { ".pdf" };
                        exportFormat = new PDFFormat();
                        break;
                    case "PNG":
                        allowedExts = new[] { ".png" };
                        exportFormat = new PNGFormat();
                        break;
                    case "JPEG":
                        allowedExts = new[] { ".jpg", ".jpeg" };
                        exportFormat = new JPEGFormat();
                        break;
                    case "TIFF":
                        allowedExts = new[] { ".tif", ".tiff" };
                        exportFormat = new TIFFFormat();
                        break;
                    case "SVG":
                        allowedExts = new[] { ".svg" };
                        exportFormat = new SVGFormat();
                        break;
                    case "EPS":
                        allowedExts = new[] { ".eps" };
                        exportFormat = new EPSFormat();
                        break;
                    default:
                        return OperationResult<LayoutExportInfo>.Fail(
                            ErrorCodes.InvalidArgument, "format must be one of: PDF, PNG, JPEG, TIFF, SVG, EPS.");
                }

                var path = outputPath.Trim();
                if (!allowedExts.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                {
                    return OperationResult<LayoutExportInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"outputPath must end with '{string.Join(" / ", allowedExts)}' for format {fmt}.");
                }

                var exists = File.Exists(path);
                if (exists && !overwrite)
                {
                    return OperationResult<LayoutExportInfo>.Fail(
                        ErrorCodes.OutputExists,
                        $"Output file already exists: {path}. Pass overwrite=true to replace it.");
                }

                exportFormat.OutputFileName = path;
                if (resolution is not null)
                {
                    exportFormat.Resolution = (int)Math.Round(resolution.Value);
                }

                if (exportFormat is PNGFormat pngFormat && transparentBackground is not null)
                {
                    pngFormat.HasTransparentBackground = transparentBackground.Value;
                }

                layout.Export(exportFormat);

                if (!File.Exists(path))
                {
                    return OperationResult<LayoutExportInfo>.Fail(
                        ErrorCodes.InternalError,
                        $"Export reported success but the output file does not exist: {path}");
                }

                var fi = new FileInfo(path);
                var head = new byte[8];
                int read;
                using (var fs = File.OpenRead(path))
                {
                    read = fs.Read(head, 0, head.Length);
                }

                if (read < head.Length)
                {
                    Array.Resize(ref head, Math.Max(read, 0));
                }

                var info = new LayoutExportInfo
                {
                    LayoutName = layout.Name ?? layoutName,
                    OutputPath = path,
                    Format = fmt,
                    Resolution = resolution,
                    FileSizeBytes = fi.Length,
                    Overwritten = exists,
                    MagicBytesHex = BitConverter.ToString(head).Replace("-", string.Empty),
                };
                return OperationResult<LayoutExportInfo>.Ok(info);
            },
            TaskCreationOptions.None);
}
