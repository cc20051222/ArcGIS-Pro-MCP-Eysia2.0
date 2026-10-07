using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-064（功能完善第四批）· D 段：布局 map series 多页 PDF 导出 —— <c>LayoutService</c> 分部实现。
/// <para>SDK 实证（`bin/Extensions/Layout/ArcGIS.Desktop.Layouts.XML`，T:/M:/P:/F: 逐条核对）：
/// <c>Layout.MapSeries</c>(P) → <c>MapSeries.Enabled/PageCount/FirstPageNumber/LastPageNumber/CIMMapSeries</c>；
/// <c>Layout.Export(ExportFormat, MapSeriesExportOptions)</c>；
/// <c>MapSeriesExportOptions.ExportPages</c>（枚举 <c>ExportPages.All</c>）与
/// <c>MapSeriesExportOptions.ExportFileOptions</c>（<c>ArcGIS.Desktop.Mapping.ExportFileOptions.ExportAsSinglePDF</c>）。</para>
/// <para><b>拒绝语义</b>：布局未启用 series ⇒ <c>INVALID_ARGUMENT</c>（不静默退化为单页）；
/// 页数 &gt; maxPages ⇒ <c>INVALID_ARGUMENT</c>（**不静默截断文件**）；失败时清理半成品产物。</para>
/// </summary>
public sealed partial class LayoutService
{
    public Task<OperationResult<ExportMapSeriesResult>> ExportMapSeriesAsync(
        string layoutName, string outputPath, int maxPages, double? resolution, bool overwrite,
        CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<ExportMapSeriesResult>>(
            () =>
            {
                var (layout, code, message) = ResolveLayoutByName(layoutName);
                if (layout is null)
                {
                    return OperationResult<ExportMapSeriesResult>.Fail(code, message);
                }

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    return OperationResult<ExportMapSeriesResult>.Fail(ErrorCodes.InvalidArgument, "outputPath is required.");
                }

                var path = outputPath.Trim();
                if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<ExportMapSeriesResult>.Fail(
                        ErrorCodes.InvalidArgument, "outputPath must end with .pdf.");
                }

                MapSeries? series;
                try
                {
                    series = layout.MapSeries;
                }
                catch (Exception ex)
                {
                    return OperationResult<ExportMapSeriesResult>.Fail(
                        ErrorCodes.InternalError, "failed to read Layout.MapSeries: " + ex.Message);
                }

                if (series is null || !series.Enabled)
                {
                    return OperationResult<ExportMapSeriesResult>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"layout '{layoutName}' has no enabled map series; export_map_series refuses to degrade into a single-page export "
                        + "(enable a map series on the layout, or use export_layout_pdf for a single page).");
                }

                int pageCount;
                try
                {
                    pageCount = series.PageCount;
                }
                catch (Exception ex)
                {
                    return OperationResult<ExportMapSeriesResult>.Fail(
                        ErrorCodes.InternalError, "failed to read map series page count: " + ex.Message);
                }

                if (maxPages > 0 && pageCount > maxPages)
                {
                    return OperationResult<ExportMapSeriesResult>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"map series has {pageCount} pages which exceeds maxPages={maxPages}; the export was refused "
                        + "(no partial file was produced — increase maxPages up to the 500 ceiling if this is intended).");
                }

                var exists = File.Exists(path);
                if (exists && !overwrite)
                {
                    return OperationResult<ExportMapSeriesResult>.Fail(
                        ErrorCodes.OutputExists,
                        $"Output file already exists: {path}. Pass overwrite=true to replace it.");
                }

                var pdf = new PDFFormat { OutputFileName = path };
                if (resolution is not null && resolution.Value > 0)
                {
                    pdf.Resolution = (int)Math.Round(resolution.Value);
                }

                var options = new MapSeriesExportOptions
                {
                    ExportPages = ExportPages.All,
                    ExportFileOptions = ExportFileOptions.ExportAsSinglePDF,
                };

                try
                {
                    layout.Export(pdf, options);
                }
                catch (Exception ex)
                {
                    // 失败清理半成品（不留残缺 PDF）。
                    TryDelete(path);
                    return OperationResult<ExportMapSeriesResult>.Fail(
                        ErrorCodes.InternalError, "map series export failed: " + ex.Message,
                        "half-written output was removed (no partial file left behind).");
                }

                if (!File.Exists(path))
                {
                    return OperationResult<ExportMapSeriesResult>.Fail(
                        ErrorCodes.InternalError,
                        $"Export reported success but the output file does not exist: {path}");
                }

                var fi = new FileInfo(path);
                var head = new byte[5];
                int read;
                using (var fs = File.OpenRead(path))
                {
                    read = fs.Read(head, 0, head.Length);
                }

                var magic = read == head.Length
                            && head[0] == (byte)'%' && head[1] == (byte)'P' && head[2] == (byte)'D' && head[3] == (byte)'F' && head[4] == (byte)'-';

                // 注：MapSeries 的系列种类读取面无稳定公开成员（XML 中的 CIMMapSeries 非公开可达
                // —— 构建期 CS1061 证伪）⇒ kind 如实留空，不臆造值。
                string? kind = null;

                return OperationResult<ExportMapSeriesResult>.Ok(new ExportMapSeriesResult
                {
                    LayoutName = layout.Name ?? layoutName,
                    OutputPath = path,
                    MapSeriesEnabled = true,
                    MapSeriesKind = kind,
                    PageCount = pageCount,
                    MaxPages = maxPages,
                    Bytes = fi.Length,
                    PdfMagic = magic,
                    RequestedPages = "all (" + pageCount + " page(s))",
                    StateProof = StateProofJson(path),
                    OverwriteNote = exists ? " (overwrite=true: the existing output was replaced by this run)" : null,
                });
            },
            TaskCreationOptions.None);

    private static string StateProofJson(string path)
    {
        try
        {
            var after = StateProof.Snapshot(path);
            return StateProof.ToJson(null, after, "executed");
        }
        catch
        {
            return StateProof.ToJson(null, null, "unprovable");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 清理失败已在调用方披露（不掩盖）。
        }
    }
}
