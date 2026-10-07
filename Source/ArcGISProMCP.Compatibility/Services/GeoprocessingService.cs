using System.IO;
using System.Text.Json;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;   // D-051：RasterDatasetDefinition（栅格数据集定义枚举）
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>地理处理服务实现（通过统一 GeoprocessingExecutor 调用 ArcGIS Pro GP）。</summary>
public sealed partial class GeoprocessingService : IGeoprocessingService
{
    public async Task<OperationResult<GeoprocessingResult>> RunToolAsync(GeoprocessingRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ToolName))
        {
            return OperationResult<GeoprocessingResult>.Fail(ErrorCodes.InvalidArgument, "toolName is required.");
        }

        // Phase 8.4（D-016，G-37）：GP 写工具的输出状态证明（stateProof 三态，设计 §3/§4）。
        // output 参数位置映射（循 GeoprocessingTools.Values 构造）。
        var outputIndex = request.ToolName switch
        {
            "Buffer_analysis" => 1,
            "Clip_analysis" => 2,
            "Intersect_analysis" => 1,
            "Dissolve_management" => 1,
            // Phase 9 第二批（D-035）：Copy / ExportTable / Project 的输出参数位。
            "Copy_management" => 1,
            "ExportTable_conversion" => 1,
            "Project_management" => 1,
            // Phase 9 第三批（D-036）：Merge 输出在位置 1；AddField 无输出数据集，
            // 以"被修改的输入数据集"（位置 0）作为状态证明目标（GDB 容器内 → 恒 unprovable，如实）。
            "Merge_management" => 1,
            "AddField_management" => 0,
        // Phase 9 第四批（D-038）：alter_field / calculate_field 均为就地修改，
        // 以"被修改的输入数据集"（位置 0）作为状态证明目标（GDB 容器内 → 恒 unprovable）。
        "AlterField_management" => 0,
        "CalculateField_management" => 0,
        // Phase 11 第三批（D-050）：RasterCalculator_sa 输出在位置 1；
        // MosaicToNewRaster 的输出由 output_location(1) + raster_name(2) 组合（见下方复合分支）。
        "RasterCalculator_sa" => 1,
        // Phase 15 功能批（D-060 · B 项）：聚合统计输出在位置 1 —— (in_raster[s], out_raster, ...)
        "CellStatistics_sa" => 1,
        "FocalStatistics_sa" => 1,
        // D-064 · A 段（schema 创建）：Create* 为 (out_path, out_name, ...) 复合 → 见下方复合分支；
        // in-place 三件以"被修改的输入数据集"（位置 0）为状态证明目标（GDB 容器内 → 恒 unprovable，如实）。
        // 注：批量加字段复用既有 "AddField_management"（已映射为 0）。
        "DeleteField_management" => 0,
        "TruncateTable_management" => 0,
        "ExportFeatures_conversion" => 1,
        _ => -1,
    };
    var outputPath = request.ToolName switch
    {
        "MosaicToNewRaster_management" when request.Values is { Count: > 2 }
            => Path.Combine(request.Values[1], request.Values[2]),
        // D-064：CreateFeatureclass_management / CreateTable_management 的 out_path + out_name 复合形态。
        "CreateFeatureclass_management" or "CreateTable_management" when request.Values is { Count: > 1 }
            => Path.Combine(request.Values[0], request.Values[1]),
        _ => outputIndex >= 0 && request.Values is { Count: > 0 } && request.Values.Count > outputIndex
            ? request.Values[outputIndex]
            : null,
    };
        var before = StateProof.Snapshot(outputPath);

        // D-017 F2（复验修正 2，红线 §8-3）：GDB 容器输出在文件语义下不可见 → before 快照恒 exists:false，
        // 无法保护既有输出。management.Exists 经 ExecuteToolAsync 静默失败（success=False, 无消息）不可用，
        // 改用 SDK Geodatabase 定义枚举（MCT 内）判 pre-existence；判定失败 → null → 清理保守跳过。
        bool? gdbOutputPreExists = null;
        string precheckDetail = "not-run";
        GeoprocessingExecutor.GpExecution execution;
        bool cancelledByCt = false;
        try
        {
            if (StateProof.IsGdbContainerPath(outputPath))
            {
                (gdbOutputPreExists, precheckDetail) = await CheckGdbOutputExistsViaSdkAsync(outputPath!, ct).ConfigureAwait(false);
            }

            execution = await GeoprocessingExecutor.ExecuteAsync(request.ToolName, request.Values ?? Array.Empty<string>(), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 取消 ≠ 已撤销：GP 可能已部分/全部完成 → 必须以 after 快照给出真实 verdict（设计 §2-⑥）。
            cancelledByCt = true;
            execution = new GeoprocessingExecutor.GpExecution { Success = false, Cancelled = true };
        }

        var after = StateProof.Snapshot(outputPath);
        // D-017 F1：带路径上下文判定——GDB 容器内输出 → 恒 unprovable（文件语义盲区），严禁 not_executed。
        var verdict = StateProof.Verdict(outputPath, before, after);
        var stateProofJson = StateProof.ToJson(before, after, verdict);

        // D-017 F2：取消/超时收尾——先判后清（verdict 已定，不因清理改变判定依据）。
        string? cleanupNote = null;
        if (cancelledByCt || execution.Cancelled)
        {
            cleanupNote = await CleanupResidualOutputAsync(outputPath, before, after, gdbOutputPreExists, precheckDetail).ConfigureAwait(false);
        }

        if (cancelledByCt)
        {
            return OperationResult<GeoprocessingResult>.Fail(
                ErrorCodes.Cancelled,
                $"Tool '{request.ToolName}' was cancelled (state: {verdict}).{AppendCleanupNote(cleanupNote)}",
                stateProofJson);
        }

        if (execution.Cancelled)
        {
            return OperationResult<GeoprocessingResult>.Fail(
                ErrorCodes.Cancelled,
                $"Tool '{request.ToolName}' was cancelled (state: {verdict}).{AppendCleanupNote(cleanupNote)}",
                stateProofJson);
        }

        var result = new GeoprocessingResult
            {
                ToolName = request.ToolName,
                Result = execution.ReturnValue ?? string.Empty,
                Messages = execution.Messages,
                StateProof = stateProofJson
            };

            if (!execution.Success)
            {
                var detail = string.Join(Environment.NewLine, execution.ErrorMessages.Concat(execution.Messages).DefaultIfEmpty("unknown GP error"));
                return OperationResult<GeoprocessingResult>.Fail(
                    ErrorCodes.GeoprocessingError,
                    $"Geoprocessing tool '{request.ToolName}' failed.",
                    detail);
            }

        return OperationResult<GeoprocessingResult>.Ok(result);
    }

    // ---------------------------------------------------------------- Phase 8.5.6（D-021 / F6 覆写前置判定）

    /// <inheritdoc />
    /// <remarks>
    /// 存在性判定的**语义分叉**（D-017 教训）：
    /// <list type="bullet">
    /// <item><c>.gdb</c> 容器内输出 → 文件系统不可见，**复用 D-017 的 SDK Geodatabase 定义枚举**（不重复实现）；</item>
    /// <item>文件型输出（shp/tif/csv…）→ 文件系统语义；</item>
    /// <item>任一路径不可判定（SDK 报错 / IO 异常）→ <see cref="OutputExistence.Unknown"/>，由策略层保守拒绝。</item>
    /// </list>
    /// 另：容器本身（<c>.gdb</c> 目录）不存在时可直接判 NotExists——目录可见性属文件系统语义，不受容器内盲区影响。
    /// <para>
    /// **D-037 F-D036-1(a) 修正**：上述"容器本身"分支只覆盖了"容器不存在"这一种情形；
    /// 容器**已存在**时仍会落到"容器内 SDK 枚举"，而枚举目标名取的是路径末段（即 <c>out.gdb</c> 自身）
    /// → 恒 <c>NotExists</c> → 覆写守卫对 <c>.gdb</c> 目录本体**完全失效**（LIVE：已存在仍放行，
    /// 进而出现"有时 GP 000258、有时成功"的行为不确定）。
    /// 现改为：**路径末段以 .gdb 结尾 ⇒ 路径本身即容器 ⇒ 一律走文件系统语义**
    /// （<see cref="StateProof.IsGdbContainerRoot"/>），行为确定且与"位于容器内"路径严格分道。
    /// </para>
    /// </remarks>
    public async Task<OperationResult<OutputExistence>> CheckOutputExistsAsync(string outputPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return OperationResult<OutputExistence>.Fail(ErrorCodes.InvalidArgument, "outputPath is required.");
        }

        // D-037 F-D036-1(a)(d)：路径本身即 .gdb 容器 → 文件系统语义（确定性，同一输入两次调用结果一致）。
        if (StateProof.IsGdbContainerRoot(outputPath))
        {
            try
            {
                var rootExists = Directory.Exists(outputPath);
                return OperationResult<OutputExistence>.Ok(
                    rootExists ? OutputExistence.Exists : OutputExistence.NotExists,
                    "file-system:gdb-container-root");
            }
            catch (Exception ex)
            {
                return OperationResult<OutputExistence>.Ok(OutputExistence.Unknown, "file-probe-failed: " + ex.Message);
            }
        }

        if (StateProof.IsGdbContainerPath(outputPath))
        {
            // 容器目录本身不存在 → 其内部输出必然不存在（文件系统语义，非容器内盲区）。
            var wsPath = GdbWorkspacePath(outputPath);
            if (wsPath is not null && !Directory.Exists(wsPath))
            {
                return OperationResult<OutputExistence>.Ok(OutputExistence.NotExists, "gdb-workspace-missing");
            }

            var (exists, detail) = await CheckGdbOutputExistsViaSdkAsync(outputPath, ct).ConfigureAwait(false);
            return exists is null
                ? OperationResult<OutputExistence>.Ok(OutputExistence.Unknown, "gdb-existence-unknown: " + detail)
                : OperationResult<OutputExistence>.Ok(
                    exists.Value ? OutputExistence.Exists : OutputExistence.NotExists,
                    "sdk-geodatabase");
        }

        try
        {
            var exists = File.Exists(outputPath) || Directory.Exists(outputPath);
            return OperationResult<OutputExistence>.Ok(
                exists ? OutputExistence.Exists : OutputExistence.NotExists,
                "file-system");
        }
        catch (Exception ex)
        {
            return OperationResult<OutputExistence>.Ok(OutputExistence.Unknown, "file-probe-failed: " + ex.Message);
        }
    }

    /// <summary>取容器内路径对应的 <c>.gdb</c> 工作空间目录（取最后一段 .gdb 及其之前部分）；非容器路径 → null。</summary>
    private static string? GdbWorkspacePath(string outputPath)
    {
        var segments = outputPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var gdbIdx = Array.FindLastIndex(segments, s => s.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase));
        if (gdbIdx < 0)
        {
            return null;
        }

        return string.Join("\\", segments.Take(gdbIdx + 1));
    }

    // ---------------------------------------------------------------- Phase 8.4 收尾（D-017 F2）

    /// <summary>
    /// 取消/超时后尽力清理"本次调用创建且非既有"的半成品输出。
    /// 红线：既有输出一律不动；清理失败仅记提示，不升级为工具错误。
    /// pre-existence 判据（复验修正）：文件型 → before 快照；GDB 容器 → 主 GP 前 management.Exists 的
    /// GP 语义判定（<paramref name="gdbOutputPreExists"/>）；判定不可得 → 保守跳过清理。
    /// </summary>
    /// <returns>提示文本（无动作或静默成功为 null；删除成功/失败均返回说明）。</returns>
    private static async Task<string?> CleanupResidualOutputAsync(
        string? outputPath, StateProof.FileSnapshot? before, StateProof.FileSnapshot? after,
        bool? gdbOutputPreExists, string precheckDetail)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return null;
        }

        var isGdbContainer = StateProof.IsGdbContainerPath(outputPath);
        var afterExists = after is { Exists: true };
        bool? preExists = isGdbContainer ? gdbOutputPreExists : (before is { Exists: true });

        // ★ D-051（O-D050-08）B 加固：决策抽到纯策略类 —— 只有"**可靠**判定先验不存在"才允许清理；
        // 预检不可得（null）或结论不可靠（detail 非 sdk-geodatabase）时一律保守跳过（宁留残留不误删）。
        var decision = ResidualCleanupPolicy.Decide(isGdbContainer, preExists, precheckDetail, afterExists);
        var skipNote = ResidualCleanupPolicy.Describe(decision, precheckDetail);
        if (skipNote is not null)
        {
            return skipNote;
        }

        if (decision != ResidualCleanupDecision.DeleteResidual)
        {
            // KeepPreExisting（既有输出一律不动）/ NothingToDelete（文件型且 after 不存在）。
            return null;
        }

        // GDB 容器输出：文件语义不可见，无法预判 → 尽力尝试 GP Delete。
        try
        {
            var cleanup = await GeoprocessingExecutor.ExecuteAsync(
                "management.Delete",
                new object[] { outputPath },
                CancellationToken.None).ConfigureAwait(false); // 清理独立于已取消的主令牌
            if (cleanup.Success)
            {
                return $" (cleanup: deleted residual output '{outputPath}')";
            }

            var detail = string.Join("; ", cleanup.ErrorMessages.Concat(cleanup.Messages).Where(m => !string.IsNullOrWhiteSpace(m)));
            // 输出根本未创建 → 非"清理失败"，如实记录。
            if (detail.Contains("000732") || detail.Contains("不存在") || detail.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                return $" (cleanup: nothing to delete at '{outputPath}' — output was not created)";
            }

            return $" (cleanupFailed: residual output '{outputPath}' not deleted — {detail})";
        }
        catch (Exception ex)
        {
            return $" (cleanupFailed: residual output '{outputPath}' not deleted — {ex.Message})";
        }
    }

    private static string AppendCleanupNote(string? cleanupNote)
        => string.IsNullOrEmpty(cleanupNote) ? string.Empty : cleanupNote;

    /// <summary>
    /// GDB 容器输出的 pre-existence 判定（SDK Geodatabase 定义枚举，MCT 内）。
    /// 返回 (exists, detail)：判定失败 → (null, 原因) → 调用方保守跳过清理。
    /// </summary>
    private static async Task<(bool? Exists, string Detail)> CheckGdbOutputExistsViaSdkAsync(string outputPath, CancellationToken ct)
    {
        try
        {
            var segments = outputPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var gdbIdx = Array.FindLastIndex(segments, s => s.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase));
            if (gdbIdx < 0)
            {
                return (null, "no-gdb-segment");
            }

            // D-037 F-D036-1 纵深防御：路径本身即 .gdb 容器（末段）时，"容器内数据集名"取到的是
            // 容器目录名本身 → SDK 枚举必不命中 → 会谎报 NotExists（正是覆写守卫失效的根因）。
            // 此处一律判"不可判定"，禁止此类路径经本函数得出 NotExists。
            if (gdbIdx == segments.Length - 1)
            {
                return (null, "path-is-gdb-root");
            }

            var wsPath = string.Join("\\", segments.Take(gdbIdx + 1));
            var name = segments[^1];
            // ★ D-051（O-D050-08，P1）：三条定义**全枚举** —— 原先缺 RasterDatasetDefinition，
            // 导致 GDB 容器内栅格数据集恒判 NotExists（假阴性）⇒ 覆写守卫失效 + 取消清理误删先验产物。
            var exists = await QueuedTask.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(wsPath)));

                var featureClasses = new List<string>();
                foreach (var def in gdb.GetDefinitions<FeatureClassDefinition>())
                using (def)
                {
                    featureClasses.Add(def.GetName());
                }

                var tables = new List<string>();
                foreach (var def in gdb.GetDefinitions<TableDefinition>())
                using (def)
                {
                    tables.Add(def.GetName());
                }

                var rasterDatasets = new List<string>();
                foreach (var def in gdb.GetDefinitions<RasterDatasetDefinition>())
                using (def)
                {
                    rasterDatasets.Add(def.GetName());
                }

                return GdbDefinitionMatcher.AnyMatches(name, featureClasses, tables, rasterDatasets);
            }).ConfigureAwait(false);
            return (exists, GdbDefinitionMatcher.SdkDetail);
        }
        catch (Exception ex)
        {
            return (null, "sdk-error: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- Phase 8.3（D-014 方案 A）
    private static readonly IReadOnlyDictionary<string, string> SelectionModeMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["replace"] = "NEW_SELECTION",
        ["add"] = "ADD_TO_SELECTION",
        ["remove"] = "REMOVE_FROM_SELECTION",
        ["switch"] = "SWITCH_SELECTION",
    };

    /// <summary>地图内精确（大小写不敏感）解析图层；重名 → AMBIGUOUS_LAYER_NAME+候选 URI（8.1 循例）。须在 MCT 内。</summary>
    /// <remarks>
    /// D-022（F1 全仓收敛，第 5 站点）：本实现**已有**正确的三分支与 AMBIGUOUS_LAYER_NAME 映射，
    /// 但为独立的第二份实现 → 改为委托共享 <see cref="LayerResolver"/>，消除双真相源。
    /// D-024（F4 组子层可寻址性统一）：枚举语义改为 <c>flatten: true</c> ——
    /// **凡 <c>get_layers(flatten=true)</c> 可见的图层（含组内子层与嵌套下钻）均按名可寻址**；
    /// 同名命中顶层与子层（或多个子层）→ 仍走 <c>AMBIGUOUS_LAYER_NAME</c>+候选，不得静默取第一。
    /// GP 执行按图层名寻址，组子层在 arcpy 侧同样按名可达（重名已在本解析层拦截）。
    /// </remarks>
    private static ArcGIS.Desktop.Mapping.Layer? ResolveLayerExact(ArcGIS.Desktop.Mapping.Map map, string layerName, out string? error)
        => LayerResolver.ResolveExact(map, layerName, out error, flatten: true);

    private static long? ReadSelectionCount(ArcGIS.Desktop.Mapping.Layer layer)
    {
        try
        {
            if (layer is BasicFeatureLayer bfl)
            {
                return bfl.GetSelection().GetCount();
            }
        }
        catch
        {
            // 计数不可得 → null（工具层 after-read 为权威）。
        }

        return null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// D-014：目标集计算与落选择（Map.SetSelection，官方 API）在 C# 端完成。
    /// **D-026 F9：匹配集查询改走 SDK 直路径**（QueryFilter 于解析所得图层上查询），
    /// 绕开 GP 引擎按名查找——组内子层按名传 GP 报 000732（R-D024 R5 / R-D025 R5），
    /// 恢复 D-024 契约"flatten 可见 = 按名可寻址"。语义等价性评估见 R-D026 §B1；
    /// select_by_location 因 17 种 overlapType 映射等价性未确认，**维持 GP 路径并上报等裁定**。
    /// </remarks>
    public async Task<OperationResult<JsonElement?>> SelectLayerByAttributeAsync(
        string? mapName, string layerName, string mode,
        IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "layerName is required");
        }

        if (!SelectionModeMap.ContainsKey(mode))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "mode must be one of: replace, add, remove, switch");
        }

        return await QueuedTask.Run<OperationResult<JsonElement?>>(async () =>
        {
            // D-034：既有 CS1998 警告清理（async lambda 无 await；此行使编译 0 警告，行为不变）。
            await Task.CompletedTask;
            var resolved = MapResolver.Resolve(mapName);
            if (MapResolver.FailIfNotOk<JsonElement?>(resolved, mapName) is { } resolveFail)
            {
                return resolveFail;
            }

            var map = resolved.Map!;
            var guard = RequireActiveMap(map);
            if (guard is not null)
            {
                return guard;
            }

            var layer = ResolveLayerExact(map, layerName, out var layerError);
            if (layer is null)
            {
                return OperationResult<JsonElement?>.Fail(
                    layerError is null ? ErrorCodes.LayerNotFound : ErrorCodes.AmbiguousLayerName,
                    layerError ?? $"Layer '{layerName}' not found in map '{map.Name}'.");
            }

            // GP 前先捕获当前选择（GP NEW_SELECTION 会清掉它）。
            var currentContent = SelectionReadService.ReadContent(map);
            var current = currentContent.ToUriSetMap();
            current.TryGetValue(layer.URI ?? string.Empty, out var currentSet);
            currentSet ??= new HashSet<long>();

            // switch：目标集 = 全集 − 当前（GP 前计算，不依赖查询）。
            if (mode == "switch")
            {
                var universe = ReadAllOids(layer);
                if (universe is null)
                {
                    return OperationResult<JsonElement?>.Fail(ErrorCodes.GeoprocessingError, "unable to enumerate feature OIDs for switch.");
                }

                if (universe.Count > 100_000)
                {
                    return OperationResult<JsonElement?>.Fail(
                        ErrorCodes.SelectionLimitExceeded,
                        $"switch requires enumerating {universe.Count} OIDs which exceeds the 100000 limit.");
                }

                var targetSwitch = new HashSet<long>(universe.Except(currentSet));
                return ApplyTargetAsync(map, layer, targetSwitch, mode);
            }

            // F9（D-026）：匹配集改走 **SDK 直路径**——在解析所得图层上以 QueryFilter 查询匹配 OID，
            // 绕开 GP 引擎按名查找（组内子层按名传 GP 报 000732）。语义等价性评估见 R-D026 §B1：
            // ① 选择集归属：GP 与 Map.SetSelection 最终都落在活动地图该图层的选择集（RequireActiveMap 守卫保持）；
            // ② where 方言：QueryFilter 与 arcpy 共用同一 geodatabase SQL 方言（空白 = 全集，等价 replace 无 where）；
            // ③ clear_selection / get_selected_features：读同一地图选择状态，交叉行为不变；
            // ④ 图层级 Search 与 GP 图层视图一致地作用于定义查询。
            // select_by_location 的 17 种 overlapType 映射存在不等价风险 → 维持 GP 路径，已上报等裁定。
            if (layer is not ArcGIS.Desktop.Mapping.FeatureLayer featureLayer)
            {
                return OperationResult<JsonElement?>.Fail(
                    ErrorCodes.GeoprocessingError,
                    $"Layer '{layerName}' does not support attribute selection (not a feature layer).");
            }

            // D-040 B 组（F-D039-N1）：数据源不可用**前置守卫**——broken 图层的 Search() 会抛 NRE，
            // 框架文本曾直达调用方误导排查（G-039）。与 AttributeService 同源信号：GetTable() 为 null 即不可用；
            // 命中 → LAYER_DATA_SOURCE_UNAVAILABLE（与 AttributeService 族收敛到同一码，G-82-C）。
            // 下方 catch(Exception) 保留为兜底（前置守卫生效后应不可达）。
            if (featureLayer.GetTable() is null)
            {
                return OperationResult<JsonElement?>.Fail(
                    ErrorCodes.LayerDataSourceUnavailable,
                    $"Layer '{layerName}' exists but its data source is unavailable " +
                    "(the layer is broken or its connection cannot be opened). Verify the layer's data source path.");
            }

            string? oidFieldName = null;
            try
            {
                oidFieldName = featureLayer.GetTable().GetDefinition()?.GetObjectIDField();
            }
            catch
            {
                // OID 字段名不可得 → 回退历史硬编码 OBJECTID（与 GP 路径行为一致）。
            }

            var whereClause = SelectWhereClause.Build(oidList, where, oidFieldName ?? "OBJECTID");

            HashSet<long> matchSet;
            try
            {
                matchSet = new HashSet<long>();
                using (var cursor = featureLayer.Search(new QueryFilter { WhereClause = whereClause }))
                {
                    while (cursor.MoveNext())
                    {
                        matchSet.Add(cursor.Current.GetObjectID());
                    }
                }
            }
            catch (Exception ex)
            {
                // SDK 失败沿用既有错误码（不新增）。
                return OperationResult<JsonElement?>.Fail(
                    ErrorCodes.GeoprocessingError,
                    "SelectLayerByAttribute match-set query failed.",
                    ex.Message);
            }

            var target = mode switch
            {
                "replace" => matchSet,
                "add" => new HashSet<long>(currentSet.Union(matchSet)),
                "remove" => new HashSet<long>(currentSet.Except(matchSet)),
                _ => new HashSet<long>(matchSet),
            };

            return ApplyTargetAsync(map, layer, target, mode);
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// **D-026 F9 评估结论**：17 种 overlapType 向 SDK SpatialQueryFilter 的映射存在不等价风险
    /// （距离/单位语义、几何谓词细节），**维持 GP 路径**并上报等裁定——组子层对本工具暂不可达（GP 000732），
    /// 见 R-D026 §B1；不得自行取舍。
    /// </remarks>
    public async Task<OperationResult<JsonElement?>> SelectLayerByLocationAsync(
        string? mapName, string layerName, string? selectingLayerName,
        string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "layerName is required");
        }

        if (!SelectionModeMap.TryGetValue(mode, out _) || mode == "switch")
        {
            return OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "mode must be one of: replace, add, remove");
        }

        return await QueuedTask.Run<OperationResult<JsonElement?>>(async () =>
        {
            var resolved = MapResolver.Resolve(mapName);
            if (MapResolver.FailIfNotOk<JsonElement?>(resolved, mapName) is { } resolveFail)
            {
                return resolveFail;
            }

            var map = resolved.Map!;
            var guard = RequireActiveMap(map);
            if (guard is not null)
            {
                return guard;
            }

            var layer = ResolveLayerExact(map, layerName, out var layerError);
            if (layer is null)
            {
                return OperationResult<JsonElement?>.Fail(
                    layerError is null ? ErrorCodes.LayerNotFound : ErrorCodes.AmbiguousLayerName,
                    layerError ?? $"Layer '{layerName}' not found in map '{map.Name}'.");
            }

            ArcGIS.Desktop.Mapping.Layer? selectingLayer = layer;
            if (!string.IsNullOrWhiteSpace(selectingLayerName))
            {
                selectingLayer = ResolveLayerExact(map, selectingLayerName, out var selectingError);
                if (selectingLayer is null)
                {
                    return OperationResult<JsonElement?>.Fail(
                        selectingError is null ? ErrorCodes.LayerNotFound : ErrorCodes.AmbiguousLayerName,
                        selectingError ?? $"Selecting layer '{selectingLayerName}' not found in map '{map.Name}'.");
                }
            }

            var currentContent = SelectionReadService.ReadContent(map);
            var current = currentContent.ToUriSetMap();
            current.TryGetValue(layer.URI ?? string.Empty, out var currentSet);
            currentSet ??= new HashSet<long>();

            // GP 只求匹配集（缺省 selection_type=NEW_SELECTION，参数省略以避开本地化域）；
            // 距离参数仅给数值（缺省单位），单位本地化域问题在披露项登记。
            var args = new List<object> { layer, overlapType, selectingLayer };
            if (searchDistance is { } distance)
            {
                args.Add(distance);
            }

            var execution = await GeoprocessingExecutor.ExecuteAsync(
                "management.SelectLayerByLocation",
                args,
                ct).ConfigureAwait(false);

            if (execution.Cancelled)
            {
                return OperationResult<JsonElement?>.Fail(ErrorCodes.Cancelled, "SelectLayerByLocation was cancelled.");
            }

            if (!execution.Success)
            {
                var detail = string.Join(Environment.NewLine, execution.ErrorMessages.Concat(execution.Messages).DefaultIfEmpty("unknown GP error"));
                return OperationResult<JsonElement?>.Fail(ErrorCodes.GeoprocessingError, "SelectLayerByLocation failed.", detail);
            }

            var matchContent = SelectionReadService.ReadContent(map);
            matchContent.ToUriSetMap().TryGetValue(layer.URI ?? string.Empty, out var matchSet);
            matchSet ??= new HashSet<long>();

            var target = mode switch
            {
                "replace" => matchSet,
                "add" => new HashSet<long>(currentSet.Union(matchSet)),
                _ => new HashSet<long>(currentSet.Except(matchSet)),
            };

            return ApplyTargetAsync(map, layer, target, mode);
        });
    }

    /// <summary>GP 图层名按活动地图解析（SDK 字符串限制）——目标地图必须是活动地图。</summary>
    private static OperationResult<JsonElement?>? RequireActiveMap(ArcGIS.Desktop.Mapping.Map map)
    {
        var activeMap = MapView.Active?.Map;
        if (activeMap is null || !ReferenceEquals(activeMap, map))
        {
            return OperationResult<JsonElement?>.Fail(
                ErrorCodes.InvalidState,
                "Selection GP requires the target map to be the active map view (GP resolves layer names against the active map).");
        }

        return null;
    }

    /// <summary>把目标 OID 集经 Map.SetSelection（官方 API）落到地图，返回载荷。</summary>
    private static OperationResult<JsonElement?> ApplyTargetAsync(
        ArcGIS.Desktop.Mapping.Map map, ArcGIS.Desktop.Mapping.Layer layer, HashSet<long> target, string mode, CancellationToken ct = default)
    {
        var dict = new Dictionary<MapMember, List<long>>();
        if (target.Count > 0)
        {
            dict[layer] = target.OrderBy(o => o).ToList();
        }

        SelectionSet selectionSet = dict.Count == 0 ? null! : SelectionSet.FromDictionary(dict);
        map.SetSelection(selectionSet); // null → 清空该图（官方语义）

        var payload = JsonSerializer.SerializeToElement(new
        {
            map_name = map.Name ?? string.Empty,
            layer_name = layer.Name ?? string.Empty,
            layer_uri = layer.URI ?? string.Empty,
            mode,
            nonIdempotent = mode == "switch",
            selected_count = ReadSelectionCount(layer),
        });
        return OperationResult<JsonElement?>.Ok(payload);
    }

    /// <summary>枚举图层全部 OID（switch 取补集用）；超 100_000 返回超长列表由调用方拒绝。</summary>
    private static List<long>? ReadAllOids(ArcGIS.Desktop.Mapping.Layer layer)
    {
        try
        {
            if (layer is BasicFeatureLayer bfl)
            {
                var oids = new List<long>();
                using (var table = bfl.GetTable())
                using (var cursor = table.Search())
                {
                    while (cursor.MoveNext())
                    {
                        oids.Add(cursor.Current.GetObjectID());
                        if (oids.Count > 100_000)
                        {
                            break;
                        }
                    }
                }

                return oids;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    // D-062 · 受控 GP 通用调用（旗舰 A 段）
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>最近一次 GP 执行的消息快照（A4 数据源；进程内静态，跨调用保持）。</summary>
    private static readonly object LastGpGate = new();
    private static GpMessagesInfo? _lastGp;

    private static void RecordLastMessages(string toolName, bool success,
        IReadOnlyList<string> messages, IReadOnlyList<string> errorMessages)
    {
        lock (LastGpGate)
        {
            _lastGp = new GpMessagesInfo
            {
                LastCallAtUtc = DateTime.UtcNow.ToString("o"),
                ToolName = toolName,
                Success = success,
                Messages = messages,
                ErrorMessages = errorMessages,
            };
        }
    }

    /// <summary>白名单嵌入资源名（csproj 以链接方式嵌入 Config/gp-whitelist.json）。</summary>
    private const string WhitelistResourceName = "ArcGISProMCP.Compatibility.Config.gp-whitelist.json";

    /// <summary>白名单文件环境变量名。</summary>
    public const string WhitelistEnvVariable = "ARCGIS_PRO_MCP_GP_WHITELIST_PATH";

    public Task<OperationResult<GpWhitelist>> GetWhitelistAsync(CancellationToken ct = default)
    {
        // ① 环境变量（LIVE 期指向 run 目录内副本，也支持部署自定义）。
        var envPath = Environment.GetEnvironmentVariable(WhitelistEnvVariable);
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
        {
            try
            {
                return Task.FromResult(OperationResult<GpWhitelist>.Ok(
                    GpWhitelistParser.Parse(File.ReadAllText(envPath), "env:" + envPath)));
            }
            catch (Exception ex)
            {
                return Task.FromResult(OperationResult<GpWhitelist>.Fail(
                    ErrorCodes.InvalidState, "Whitelist from " + WhitelistEnvVariable + " failed to parse: " + ex.Message));
            }
        }

        // ② 程序集旁文件（部署形态）。
        var sidePath = Path.Combine(AppContext.BaseDirectory, "gp-whitelist.json");
        if (File.Exists(sidePath))
        {
            try
            {
                return Task.FromResult(OperationResult<GpWhitelist>.Ok(
                    GpWhitelistParser.Parse(File.ReadAllText(sidePath), "file:" + sidePath)));
            }
            catch (Exception ex)
            {
                return Task.FromResult(OperationResult<GpWhitelist>.Fail(
                    ErrorCodes.InvalidState, "Whitelist file next to assembly failed to parse: " + ex.Message));
            }
        }

        // ③ 嵌入资源（随程序集分发；本批 LIVE 兜底）。
        var asm = typeof(GeoprocessingService).Assembly;
        using (var stream = asm.GetManifestResourceStream(WhitelistResourceName))
        {
            if (stream is null)
            {
                return Task.FromResult(OperationResult<GpWhitelist>.Fail(
                    ErrorCodes.NotFound,
                    "GP whitelist not found (env / side file / embedded resource all missing); run_geoprocessing refuses to run without it (fail-closed)."));
            }

            using var reader = new StreamReader(stream);
            try
            {
                return Task.FromResult(OperationResult<GpWhitelist>.Ok(
                    GpWhitelistParser.Parse(reader.ReadToEnd(), "embedded:" + WhitelistResourceName)));
            }
            catch (Exception ex)
            {
                return Task.FromResult(OperationResult<GpWhitelist>.Fail(
                    ErrorCodes.InvalidState, "Embedded whitelist failed to parse: " + ex.Message));
            }
        }
    }

    public async Task<OperationResult<GpRunResult>> RunWhitelistedAsync(GpRunRequest request, CancellationToken ct = default)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();

        if (request is null || string.IsNullOrWhiteSpace(request.ToolName))
        {
            return OperationResult<GpRunResult>.Fail(ErrorCodes.InvalidArgument, "tool (GP tool name, e.g. analysis.Buffer) is required.");
        }

        // ① 白名单校验（fail-closed：白名单不可得 → 拒绝执行）。
        var wl = await GetWhitelistAsync(ct).ConfigureAwait(false);
        if (!wl.Success || wl.Data is null)
        {
            return OperationResult<GpRunResult>.Fail(
                ErrorCodes.InvalidState,
                "GP whitelist is unavailable (" + (wl.Message ?? "no details") + "); refusing to run (fail-closed).");
        }

        var entry = wl.Data.Find(request.ToolName);
        if (entry is null)
        {
            return OperationResult<GpRunResult>.Fail(
                ErrorCodes.InvalidArgument,
                $"'{request.ToolName}' is not in the controlled GP whitelist (受控白名单外，禁止执行). " +
                "Use list_geoprocessing_tools to enumerate allowed tools; whitelist changes require Keeper approval.");
        }

        // ② 破坏性 confirm（缺省拒）。
        if (entry.Destructive && !request.Confirm)
        {
            return OperationResult<GpRunResult>.Fail(
                ErrorCodes.InvalidArgument,
                $"'{entry.Tool}' is destructive (in-place/overwrite semantics): confirm=true is required (default-refuse).");
        }

        // ③ 审计路径先行解析（G-138：禁 %TEMP%；解析失败/违规 → 拒绝执行，零变更）。
        string auditPath;
        try
        {
            auditPath = GpAuditLog.ResolvePath(request.AuditPath);
        }
        catch (Exception ex)
        {
            return OperationResult<GpRunResult>.Fail(ErrorCodes.InvalidArgument, ex.Message);
        }

        // ④ 组参（named / positional 双形态）+ 守卫。
        var (values, form, buildError) = BuildParameterValues(entry, request);
        if (buildError is not null)
        {
            return OperationResult<GpRunResult>.Fail(ErrorCodes.InvalidArgument, buildError);
        }

        var guardError = GuardWhitelistedValues(entry, values, out var outputValues);
        if (guardError is not null)
        {
            return OperationResult<GpRunResult>.Fail(ErrorCodes.PathEscapeRejected, guardError);
        }

        // ⑤ 输出存在性闸门（OUTPUT_EXISTS；A1 v1 不提供覆写语义，如实披露）。
        foreach (var output in outputValues)
        {
            var exists = await CheckOutputExistsAsync(output, ct).ConfigureAwait(false);
            if (exists.Success && exists.Data == OutputExistence.Exists)
            {
                return OperationResult<GpRunResult>.Fail(
                    ErrorCodes.OutputExists,
                    $"Output '{output}' already exists; run_geoprocessing v1 does not offer overwrite (disclosed). " +
                    "Choose a fresh output path.");
            }
        }

        // ⑥ 执行（白名单工具名按点串原样交 SDK；参数序 A/B 实测在阶段二 LIVE 复核）。
        GeoprocessingExecutor.GpExecution execution;
        try
        {
            execution = await GeoprocessingExecutor.ExecuteAsync(entry.Tool, values, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            RecordLastMessages(entry.Tool, false, Array.Empty<string>(), new[] { "cancelled" });
            return OperationResult<GpRunResult>.Fail(ErrorCodes.Cancelled, $"'{entry.Tool}' was cancelled.");
        }
        catch (Exception ex)
        {
            RecordLastMessages(entry.Tool, false, Array.Empty<string>(), new[] { ex.Message });
            return OperationResult<GpRunResult>.Fail(ErrorCodes.GeoprocessingError, "GP execution failed: " + ex.Message);
        }

        started.Stop();
        RecordLastMessages(entry.Tool, execution.Success, execution.Messages, execution.ErrorMessages);

        // ⑦ 输出产物证明（执行后仍不存在 → 明示，防 GP 静默假成功）。
        var messages = execution.Messages.ToList();
        foreach (var output in outputValues)
        {
            var after = await CheckOutputExistsAsync(output, ct).ConfigureAwait(false);
            if (after.Success && after.Data != OutputExistence.Exists)
            {
                messages.Add("[mcp] WARNING: output '" + output + "' still not found after execution (verified via SDK probe).");
            }
        }

        // ⑧ 审计落盘（append-only jsonl；写失败不阻断结果，但以 AuditEntryIndex=0 明示）。
        var auditEntry = new GpAuditEntry
        {
            TimestampUtc = DateTime.UtcNow.ToString("o"),
            Tool = entry.Tool,
            ParameterDigest = DigestValues(entry, values),
            ParameterForm = form,
            Destructive = entry.Destructive,
            Confirm = request.Confirm,
            Success = execution.Success,
            ResultCode = execution.Success ? "OK" : ErrorCodes.GeoprocessingError,
            DurationMs = started.ElapsedMilliseconds,
            AuditNote = string.IsNullOrWhiteSpace(request.AuditNote) ? null : request.AuditNote!.Trim(),
            Pid = Environment.ProcessId,
        };
        var appended = GpAuditLog.TryAppend(auditPath, auditEntry, out var auditIndex, out var auditError);

        if (!execution.Success)
        {
            var detail = string.Join(Environment.NewLine,
                execution.ErrorMessages.Concat(execution.Messages).DefaultIfEmpty("unknown GP error"));
            return OperationResult<GpRunResult>.Fail(ErrorCodes.GeoprocessingError, detail);
        }

        return OperationResult<GpRunResult>.Ok(new GpRunResult
        {
            ToolName = entry.Tool,
            Result = execution.ReturnValue ?? string.Empty,
            Messages = messages,
            DurationMs = started.ElapsedMilliseconds,
            AuditPath = auditPath,
            AuditEntryIndex = appended ? auditIndex : 0,
            Destructive = entry.Destructive,
            ConfirmRequired = entry.Destructive,
            ParameterForm = form,
        }, appended ? null : "AUDIT WRITE FAILED: " + auditError + " (AuditEntryIndex=0; disclosed)");
    }

    public Task<OperationResult<GpMessagesInfo>> GetLastMessagesAsync(CancellationToken ct = default)
    {
        lock (LastGpGate)
        {
            return Task.FromResult(OperationResult<GpMessagesInfo>.Ok(_lastGp ?? new GpMessagesInfo()));
        }
    }

    /// <summary>组参：named（按白名单参数名映射）/ positional（按参数序）双形态。</summary>
    private static (List<string> Values, string Form, string? Error) BuildParameterValues(
        GpWhitelistEntry entry, GpRunRequest request)
    {
        var values = new List<string>();

        if (request.PositionalValues is { Count: > 0 })
        {
            if (request.Parameters is { Count: > 0 })
            {
                return (values, string.Empty, "Provide either parameters (named) or positionalValues — not both.");
            }

            if (request.PositionalValues.Count > entry.Parameters.Count)
            {
                return (values, string.Empty,
                    $"positionalValues has {request.PositionalValues.Count} items but '{entry.Tool}' takes at most {entry.Parameters.Count} parameters.");
            }

            // 位置形态：按序取值（尾部可缺省；必需参数不得留空 —— 不支持跳位）。
            for (var i = 0; i < entry.Parameters.Count; i++)
            {
                var raw = i < request.PositionalValues.Count ? request.PositionalValues[i] : null;
                values.Add(raw ?? string.Empty);
            }

            for (var i = 0; i < entry.Parameters.Count; i++)
            {
                var p = entry.Parameters[i];
                var provided = !string.IsNullOrWhiteSpace(values[i]);
                if (!provided && p.Required && p.Default is null)
                {
                    return (values, string.Empty,
                        $"Missing required parameter '{p.Name}' for '{entry.Tool}' (positional form leaves it empty).");
                }
            }

            while (values.Count > 0 && string.IsNullOrWhiteSpace(values[^1]) && !entry.Parameters[values.Count - 1].Required)
            {
                values.RemoveAt(values.Count - 1);
            }

            return (values, "positional", null);
        }

        if (request.Parameters is not { Count: > 0 })
        {
            return (values, string.Empty, "parameters (named) or positionalValues is required.");
        }

        // 命名形态：未知参数名 → 拒绝（防拼写静默失效）。
        foreach (var key in request.Parameters.Keys)
        {
            if (!entry.Parameters.Any(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)))
            {
                return (values, string.Empty,
                    $"Unknown parameter '{key}' for '{entry.Tool}' (not in the whitelist signature).");
            }
        }

        foreach (var p in entry.Parameters)
        {
            var kv = request.Parameters.FirstOrDefault(x => string.Equals(x.Key, p.Name, StringComparison.OrdinalIgnoreCase));
            if (kv.Key is not null && kv.Value is not null)
            {
                values.Add(ToStringValue(kv.Value));
                continue;
            }

            if (p.Default is not null)
            {
                values.Add(p.Default);
                continue;
            }

            if (p.Required)
            {
                return (values, string.Empty, $"Missing required parameter '{p.Name}' for '{entry.Tool}'.");
            }

            // 可选未提供：留空占位（GP 形参序对齐）。
            values.Add(string.Empty);
        }

        // 修剪尾部全空可选项（GP 接受截断参数表）。
        while (values.Count > 0 && string.IsNullOrWhiteSpace(values[^1]) && !entry.Parameters[values.Count - 1].Required)
        {
            values.RemoveAt(values.Count - 1);
        }

        return (values, "named", null);
    }

    private static string ToStringValue(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>守卫：input 方向 → 输入判界；output 方向 → 输出判界（多值按分隔符逐项）。</summary>
    private static string? GuardWhitelistedValues(
        GpWhitelistEntry entry, List<string> values, out List<string> outputValues)
    {
        outputValues = new List<string>();
        for (var i = 0; i < entry.Parameters.Count && i < values.Count; i++)
        {
            var param = entry.Parameters[i];
            var raw = values[i];
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            // 多值输入按 ; , 分隔逐项判界（披露：GP multivalue 惯例分隔符）。
            var pieces = param.Type == "multivalue"
                ? raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : new[] { raw };

            foreach (var piece in pieces)
            {
                if (string.IsNullOrWhiteSpace(piece))
                {
                    continue;
                }

                var hit = ProtectedOutputPathGuard.Match(piece);
                if (hit is null)
                {
                    continue;
                }

                return $"{param.Direction} parameter '{param.Name}' hits a protected root ('{piece}' → {hit}); refused (zero changes).";
            }

            if (param.Direction == "output")
            {
                outputValues.Add(raw);
            }
        }

        return null;
    }

    /// <summary>审计参数摘要（值截断，控制行体积；完整值以请求侧为准）。</summary>
    private static string DigestValues(GpWhitelistEntry entry, List<string> values)
    {
        const int maxPerValue = 60;
        var parts = new List<string>();
        for (var i = 0; i < entry.Parameters.Count && i < values.Count; i++)
        {
            var v = values[i];
            if (string.IsNullOrWhiteSpace(v))
            {
                continue;
            }

            var shown = v.Length <= maxPerValue ? v : v[..maxPerValue] + "…";
            parts.Add(entry.Parameters[i].Name + "=" + shown);
        }

        var joined = string.Join("; ", parts);
        return joined.Length <= 900 ? joined : joined[..900] + "…";
    }

}
