using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// 坐标系解析服务（D-037 F-D035-2）：把 <c>project.outSR</c> 的名称形态解析为 WKID。
/// <para>
/// 依据：**LIVE 实证** <c>Project_management</c> 的 <c>out_coor_system</c> 仅 WKID 数值形态可解析，
/// 名称形态（含空格与下划线两种写法）一律 <c>ERROR 000735</c>（R-D035-phase2 PR2）。
/// 因此本服务把名称 → WKID 解析放在 **宿主（SDK）侧**：
/// <c>GeometryEngine.GetPredefinedCoordinateSystemList</c> 枚举 Pro 预置坐标系（WKID + Name），
/// 再用 Shared 的纯函数 <see cref="SpatialReferenceName.TryResolve"/> 做确定性归一化匹配。
/// </para>
/// 规则：WKID 字面量 → 原样通过（不查表，行为不变）；名称为**未知** → Resolved=false（调用方如实报错，
/// 绝不静默回落为任意坐标系）；SDK 抛错 → <see cref="ErrorCodes.ArcGISError"/>。
/// </summary>
public sealed class SpatialReferenceService : ISpatialReferenceService
{
    /// <summary>预置坐标系清单（进程内缓存一次；Enumeration 需 MCT，故在 QueuedTask 内首次构建）。</summary>
    private static readonly object Sync = new();
    private static List<SpatialReferenceEntry>? _cache;

    public async Task<OperationResult<SpatialReferenceResolution>> ResolveAsync(string? value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OperationResult<SpatialReferenceResolution>.Fail(ErrorCodes.InvalidArgument, "outSR is required.");
        }

        // WKID 数值形态：原样通过（LIVE 已证可用），不进 SDK。
        if (SpatialReferenceName.IsWkidLiteral(value, out var literal))
        {
            return OperationResult<SpatialReferenceResolution>.Ok(
                new SpatialReferenceResolution(literal, null, "wkid-literal"), "wkid-literal");
        }

        try
        {
            return await QueuedTask.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var entries = Entries();
                if (!SpatialReferenceName.TryResolve(value, entries, out var match) || match is null)
                {
                    // 未解析 ≠ 失败：交回调用方**如实报错**（保留 GP 真实错误码语义）。
                    return OperationResult<SpatialReferenceResolution>.Ok(
                        new SpatialReferenceResolution(0, null, "unresolved-name"),
                        "unresolved-name");
                }

                return OperationResult<SpatialReferenceResolution>.Ok(
                    new SpatialReferenceResolution(match.Wkid, match.Name, "sdk-predefined-list"),
                    "sdk-predefined-list");
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<SpatialReferenceResolution>.Fail(ErrorCodes.Cancelled, "Spatial reference resolution was cancelled.");
        }
        catch (Exception ex)
        {
            return OperationResult<SpatialReferenceResolution>.Fail(
                ErrorCodes.ArcGISError,
                $"Spatial reference '{value}' could not be resolved by the host.",
                ex.Message);
        }
    }

    private static List<SpatialReferenceEntry> Entries()
    {
        lock (Sync)
        {
            if (_cache is not null)
            {
                return _cache;
            }

            const CoordinateSystemFilter filter =
                CoordinateSystemFilter.GeographicCoordinateSystem | CoordinateSystemFilter.ProjectedCoordinateSystem;

            var list = GeometryEngine.Instance.GetPredefinedCoordinateSystemList(filter);
            _cache = list is null
                ? new List<SpatialReferenceEntry>()
                : list.Select(e => new SpatialReferenceEntry(e.Wkid, e.Name ?? string.Empty)).ToList();

            return _cache;
        }
    }

    /// <summary>测试/诊断：当前缓存条目数（0 = 尚未构建）。</summary>
    public static int CachedEntryCount
    {
        get { lock (Sync) { return _cache?.Count ?? 0; } }
    }
}
