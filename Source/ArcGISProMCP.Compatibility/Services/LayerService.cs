using System.IO;
using System.Globalization;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>图层服务实现。所有 SDK 访问通过 QueuedTask.Run。</summary>
/// <remarks>D-009：地图解析收敛至 <see cref="MapResolver"/>（去 CreateMapFromItem 依赖）；
/// 重名 → AMBIGUOUS_MAP_NAME + 候选 id；空白 mapName → 活动地图（G-22）。</remarks>
public sealed partial class LayerService : ILayerService
{
    /// <summary>
    /// 获取图层列表。
    /// </summary>
    /// <param name="flatten">
    /// 默认 <c>true</c>：使用 <c>Map.GetLayersAsFlattenedList()</c>，嵌套在组合图层内的子图层可见；
    /// <c>false</c>：使用 <c>Map.Layers</c>，仅返回顶层并保留层级结构。
    /// 默认展开由 Gate Keeper 决定 G-06 批准（属既有契约变更）。
    /// 语义依据：官方 ProConcepts Map Authoring。
    /// </param>
    public Task<OperationResult<IReadOnlyList<LayerInfo>>> GetLayersAsync(
        string? mapName = null,
        bool flatten = true,
        CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<LayerInfo>>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<IReadOnlyList<LayerInfo>>(resolved, mapName) is { } resolveFail) return resolveFail;
                var map = resolved.Map!;

                var layers = LayerResolver.EnumerateLayers(map, flatten);
                var infos = layers.Select(layer => ToInfo(layer, map.Name ?? string.Empty)).ToList();
                return OperationResult<IReadOnlyList<LayerInfo>>.Ok(infos);
            },
            TaskCreationOptions.None);

    public Task<OperationResult<IReadOnlyList<string>>> GetRasterSourcePathsAsync(
        string? mapName = null,
        CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyList<string>>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<IReadOnlyList<string>>(resolved, mapName) is { } resolveFail)
                {
                    return resolveFail;
                }

                var paths = new List<string>();
                foreach (var raster in LayerResolver.EnumerateLayers(resolved.Map!, flatten: true).OfType<RasterLayer>())
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
                            $"Cannot resolve the raster source for layer '{raster.Name}': {ex.Message}");
                    }

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return OperationResult<IReadOnlyList<string>>.Fail(
                            ErrorCodes.InvalidArgument,
                            $"Cannot resolve the raster source for layer '{raster.Name}'.");
                    }

                    paths.Add(path);
                }

                return OperationResult<IReadOnlyList<string>>.Ok(
                    paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            },
            TaskCreationOptions.None);

    public Task<OperationResult<LayerInfo?>> FindLayerAsync(string mapName, string layerName, CancellationToken ct = default)
        => GetLayerInfoAsync(mapName, layerName, ct);

    public Task<OperationResult<LayerInfo?>> GetLayerInfoAsync(string mapName, string layerName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LayerInfo?>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<LayerInfo?>(resolved, mapName) is { } resolveFail) return resolveFail;
                var map = resolved.Map!;

                // D-022（F1）：与其余站点同源收敛到 LayerResolver（三分支 0/1/>1）。
                var resolvedLayer = LayerResolver.Resolve(map, layerName, flatten: true);
                if (LayerResolver.FailIfNotOk<LayerInfo?>(resolvedLayer, layerName) is { } layerFail)
                {
                    return layerFail;
                }

                return OperationResult<LayerInfo?>.Ok(ToInfo(resolvedLayer.Layer!, map.Name ?? string.Empty));
            },
            TaskCreationOptions.None);

    public Task<OperationResult<bool>> SetLayerVisibilityAsync(string mapName, string layerName, bool visible, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<bool>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<bool>(resolved, mapName) is { } resolveFail) return resolveFail;
                var map = resolved.Map!;

                // F1（D-022 站点 2）：重名 → AMBIGUOUS_LAYER_NAME，禁止静默作用于第一层。
                var resolvedLayer = LayerResolver.Resolve(map, layerName, flatten: true);
                if (LayerResolver.FailIfNotOk<bool>(resolvedLayer, layerName) is { } layerFail)
                {
                    return layerFail;
                }

                resolvedLayer.Layer!.SetVisibility(visible);
                return OperationResult<bool>.Ok(true);
            },
            TaskCreationOptions.None);

    public Task<OperationResult<LayerInfo?>> AddLayerAsync(string mapName, string layerPathOrUri, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LayerInfo?>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<LayerInfo?>(resolved, mapName) is { } resolveFail) return resolveFail;
                var map = resolved.Map!;

                if (string.IsNullOrWhiteSpace(layerPathOrUri))
                {
                    return OperationResult<LayerInfo?>.Fail(ErrorCodes.InvalidArgument, "layerPathOrUri is required.");
                }

                // ══ D-062 · P-20 spike 修复 ══
                // 旧实现：单一 CreateLayer(Uri) —— 对 .gdb/.sde 容器内数据集在本环境实测失败（P-20），
                // 且异常细节被吞（只剩 "Failed to create layer."）。修复 = 多策略尝试 + 错误细节透传：
                //   ① CreateLayer（原路径，CIMPATH/layer 文件等仍走此路）
                //   ② 容器内数据集 → CreateFeatureLayer / CreateRasterLayer（显式工厂）
                //   ③ 全失败 → 报告各策略异常细节（不臆测根因，LIVE 取证）
                var pathText = layerPathOrUri.Trim();
                var isCimPath = pathText.StartsWith("CIMPATH", StringComparison.OrdinalIgnoreCase);

                // ★ D-064 阶段二真缺陷 ⑦ 终版：**幂等加层** —— 同一地图内已存在**同名**图层时直接复用
                //   （不重复建层）。LIVE 实测：冷启动窗口内 CreateLayer「建层成功但随后抛 COM 异常」，
                //   重试会在地图里堆积同名图层（6 个 roads_r13）⇒ 之后按名解析一律 AMBIGUOUS_LAYER_NAME。
                //   幂等语义对自动化调用方更安全；复用事实在回执 note 中如实披露。
                if (!isCimPath)
                {
                    try
                    {
                        var leaf = Path.GetFileName(pathText.TrimEnd('\\', '/'));
                        if (!string.IsNullOrWhiteSpace(leaf))
                        {
                            var existing = LayerResolver.EnumerateLayers(map, flatten: true)
                                .LastOrDefault(l => string.Equals(l.Name, leaf, StringComparison.OrdinalIgnoreCase));
                            if (existing is not null)
                            {
                                return OperationResult<LayerInfo?>.Ok(
                                    ToInfo(existing, map.Name ?? string.Empty),
                                    "layer already present in the map; reused (idempotent add by dataset name '"
                                    + leaf + "').");
                            }
                        }
                    }
                    catch
                    {
                        // 枚举不可得（冷启动 COM 竞态）⇒ 跳过幂等检查，继续正常建层路径。
                    }
                }
                var uri = isCimPath
                    ? new Uri(pathText, UriKind.RelativeOrAbsolute)
                    : new Uri(pathText, UriKind.Absolute);

                // ★ D-064 阶段二真缺陷 ⑩：对**刚由 GP 建好**的数据集，CreateLayer 偶发抛
                //   「添加数据失败，数据类型不受支持 / 没有注册类」（COM 注册竞态）——
                //   LIVE 实测同一调用数秒后稳定成功（r8 轮 try1 即成功；r9 轮三次皆败）。
                //   与 D-063「句柄延迟释放 ⇒ 删除重试」同源 ⇒ 整条策略链**带退避重试**，
                //   各轮诊断如实透传（不吞错、不臆测根因）。
                // ★ 真缺陷 ⑩ 修正（第三轮）：**不做盲重试** —— LIVE 实测冷启动窗口内 CreateLayer
                //   「建层成功但随后抛 COM 异常」且此时枚举不可见，盲重试会**成倍制造同名图层**
                //   （6 个 roads_r13 ⇒ AMBIGUOUS_LAYER_NAME 连锁）。重试职责上移到调用方
                //   （驱动在重试间用 get_layers 验证图层是否其实已建好）。
                Layer? layer = null;
                var attempts = new List<string>();
                {
                    var urisBefore = LayerUrisBefore();

                // ★ D-064 阶段二真缺陷 ⑦ 修复：多策略回退前先记录**已有图层 URI 集合**。
                //   LIVE 实测：策略①可能"图层已建但随后抛异常/返回 null" ⇒ 旧实现继续走策略②
                //   ⇒ **同名图层重复建立**（roads_r4.json + roads_r42.json）⇒ 之后按名解析一律
                //   AMBIGUOUS_LAYER_NAME，整条链路失效。回退前用 URI 差集确认"是否其实已经建好"。
                // 枚举走 LayerResolver.EnumerateLayers（= get_layers 的既有实现，LIVE 已验证可用）；
                // ★ 第二轮加固：LIVE 实测裸用 map.GetLayersAsFlattenedList() 在本环境会抛
                //   COM「没有注册类」⇒ 差集恒为空 ⇒ 检测失效（A5 误报失败但图层其实已建）。
                IReadOnlyList<string> LayerUris()
                {
                    try
                    {
                        return LayerResolver.EnumerateLayers(map, flatten: true)
                            .Select(l => l.URI ?? string.Empty)
                            .ToList();
                    }
                    catch
                    {
                        return Array.Empty<string>();
                    }
                }

                HashSet<string> LayerUrisBefore() => new(LayerUris(), StringComparer.OrdinalIgnoreCase);

                Layer? FindNewlyAdded(HashSet<string> before)
                {
                    try
                    {
                        var current = LayerUris();
                        var added = current.Where(u => !before.Contains(u)).ToList();
                        if (added.Count > 0)
                        {
                            // 以 URI 反查刚建好的图层（最后一个是本轮新增）。
                            var uri = added[^1];
                            var found = LayerResolver.EnumerateLayers(map, flatten: true)
                                .LastOrDefault(l => string.Equals(l.URI, uri, StringComparison.OrdinalIgnoreCase));
                            if (found is not null)
                            {
                                attempts.Add($"layer actually created despite the error: {uri}");
                                return found;
                            }
                        }

                        // ★ 第二轮加固（URI 差集不可得时）：按**数据集叶子名**匹配 —— LIVE 实测本环境的
                        //   CreateLayer 可能「建层成功但随后抛 COM 异常」，且枚举偶发不可得；
                        //   此时若地图里已存在同**名**图层，即认定建层已生效（以产物事实为准，不臆测）。
                        var leaf = Path.GetFileName(pathText.TrimEnd('\\', '/'));
                        if (!string.IsNullOrWhiteSpace(leaf))
                        {
                            var byName = LayerResolver.EnumerateLayers(map, flatten: true)
                                .LastOrDefault(l => string.Equals(l.Name, leaf, StringComparison.OrdinalIgnoreCase));
                            if (byName is not null)
                            {
                                attempts.Add($"layer with dataset name '{leaf}' already present in the map");
                                return byName;
                            }
                        }

                        return null;
                    }
                    catch
                    {
                        return null;
                    }
                }

                try
                {
                    layer = LayerFactory.Instance.CreateLayer(uri, (ILayerContainerEdit)map, -1, string.Empty);
                    if (layer is null)
                    {
                        attempts.Add("CreateLayer returned null");
                        layer = FindNewlyAdded(urisBefore);          // ★ ⑦：可能"已建但返回 null"
                    }
                }
                catch (Exception ex)
                {
                    attempts.Add("CreateLayer: " + ex.Message);
                    layer = FindNewlyAdded(urisBefore);              // ★ ⑦：可能"已建但随后抛异常"
                }

                if (layer is null && IsContainerDatasetPath(pathText))
                {
                    try
                    {
                        layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(new LayerCreationParams(uri), (ILayerContainerEdit)map);
                        if (layer is null)
                        {
                            attempts.Add("CreateFeatureLayer returned null");
                            layer = FindNewlyAdded(urisBefore);
                        }
                    }
                    catch (Exception ex)
                    {
                        attempts.Add("CreateFeatureLayer: " + ex.Message);
                        layer = FindNewlyAdded(urisBefore);
                    }
                }

                if (layer is null && IsRasterPath(pathText))
                {
                    try
                    {
                        layer = LayerFactory.Instance.CreateLayer<RasterLayer>(new LayerCreationParams(uri), (ILayerContainerEdit)map);
                        if (layer is null)
                        {
                            attempts.Add("CreateRasterLayer returned null");
                            layer = FindNewlyAdded(urisBefore);
                        }
                    }
                    catch (Exception ex)
                    {
                        attempts.Add("CreateRasterLayer: " + ex.Message);
                        layer = FindNewlyAdded(urisBefore);
                    }
                }

                }   // ★ ⑩ 重试轮结束

                return layer is null
                    ? OperationResult<LayerInfo?>.Fail(
                        ErrorCodes.ArcGISError,
                        "Failed to create layer (P-20 spike diagnostics): " + string.Join(" | ", attempts) + "; uri=" + uri.ToString())
                    : OperationResult<LayerInfo?>.Ok(ToInfo(layer, map.Name ?? string.Empty));
            },
            TaskCreationOptions.None);

    /// <summary>路径是否为地理数据库容器内数据集（.gdb / .sde / .ngd，分隔符两种形态都认）。</summary>
    private static bool IsContainerDatasetPath(string path)
    {
        foreach (var ext in new[] { ".gdb", ".sde", ".ngd" })
        {
            var idx = path.IndexOf(ext + "\\", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                idx = path.IndexOf(ext + "/", StringComparison.OrdinalIgnoreCase);
            }

            if (idx >= 0 && idx + ext.Length + 1 < path.Length)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>路径是否疑似栅格（常见栅格扩展名 / 栅格容器目录）。</summary>
    private static bool IsRasterPath(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.ToLowerInvariant() is ".tif" or ".tiff" or ".img" or ".ecw" or ".jpg" or ".png" or ".bil" or ".crf"
            or ".asc" or ".grd";
    }

    public Task<OperationResult<bool>> RemoveLayerAsync(string mapName, string layerName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<bool>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<bool>(resolved, mapName) is { } resolveFail) return resolveFail;
                var map = resolved.Map!;

                // F1（D-022 站点 3）：重名 → AMBIGUOUS_LAYER_NAME，禁止静默删除第一层。
                var resolvedLayer = LayerResolver.Resolve(map, layerName, flatten: true);
                if (LayerResolver.FailIfNotOk<bool>(resolvedLayer, layerName) is { } layerFail)
                {
                    return layerFail;
                }

                map.RemoveLayer(resolvedLayer.Layer!);
                return OperationResult<bool>.Ok(true);
            },
            TaskCreationOptions.None);


    private static LayerInfo ToInfo(Layer layer, string mapName) => new()
    {
        Name = layer.Name ?? string.Empty,
        Uri = layer.URI?.ToString() ?? string.Empty,
        LayerType = layer.GetType().Name,
        IsVisible = layer.IsVisible,
        MapName = mapName
    };
    /// <summary>D-042：定义查询读取。supports=false → DefinitionQuery=null（GroupLayer 等不支持）；
    /// 空串 = 支持但未设置；非空 = 查询原文（G-82-C 可判别）。</summary>
    public Task<OperationResult<DefinitionQueryInfo>> GetDefinitionQueryAsync(
        string? mapName, string layerName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<DefinitionQueryInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<DefinitionQueryInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }
                var map = resolved.Map!;
                var layerResult = LayerResolver.Resolve(map, layerName, flatten: true);
                if (layerResult.Status == LayerResolveStatus.Ambiguous)
                {
                    var hint = layerResult.CandidateIds.Count > 0
                        ? " Candidate layer ids: " + string.Join(" | ", layerResult.CandidateIds)
                        : string.Empty;
                    return OperationResult<DefinitionQueryInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName,
                        $"Layer name '{layerName}' is ambiguous ({layerResult.CandidateIds.Count} matches).{hint}");
                }

                var info = new DefinitionQueryInfo
                {
                    LayerName = layerName,
                    SupportsDefinitionQuery = false,
                    DefinitionQuery = null,
                };
                if (layerResult.Layer is ArcGIS.Desktop.Mapping.BasicFeatureLayer featureLayer)
                {
                    info.SupportsDefinitionQuery = true;
                    info.DefinitionQuery = featureLayer.DefinitionQuery ?? string.Empty;
                }
                return OperationResult<DefinitionQueryInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    public Task<OperationResult<DefinitionQuerySetInfo>> SetDefinitionQueryAsync(
        string? mapName, string layerName, string? definitionQuery, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<DefinitionQuerySetInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<DefinitionQuerySetInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var map = resolved.Map!;
                var layerResult = LayerResolver.Resolve(map, layerName, flatten: true);
                if (LayerResolver.FailIfNotOk<DefinitionQuerySetInfo>(layerResult, layerName) is { } layerFail)
                {
                    return layerFail;
                }

                var layer = layerResult.Layer!;
                if (layer is not ArcGIS.Desktop.Mapping.BasicFeatureLayer featureLayer)
                {
                    // 与 get_definition_query 的 supportsDefinitionQuery=false 同义（G-82-C 对齐）。
                    return OperationResult<DefinitionQuerySetInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"Layer '{layerName}' does not support a definition query (supportsDefinitionQuery=false).");
                }

                // 空 = 清除（SDK 无 ClearDefinitionQuery 成员，见 sdk-spike.md）。
                var whereClause = definitionQuery ?? string.Empty;
                featureLayer.SetDefinitionQuery(whereClause);
                var readBack = featureLayer.DefinitionQuery ?? string.Empty;
                return OperationResult<DefinitionQuerySetInfo>.Ok(new DefinitionQuerySetInfo
                {
                    LayerName = layerName,
                    Applied = true,
                    SupportsDefinitionQuery = true,
                    DefinitionQuery = readBack,
                });
            },
            TaskCreationOptions.None);

    public Task<OperationResult<LayerOrderInfo>> MoveLayerAsync(
        string? mapName, string layerName, string? referenceLayer, string position, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LayerOrderInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<LayerOrderInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var map = resolved.Map!;
                var pos = MoveTargetIndex.Normalize(position) ?? string.Empty;
                if (!MoveTargetIndex.IsSupported(pos))
                {
                    return OperationResult<LayerOrderInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        "position must be one of: TOP, BOTTOM, BEFORE, AFTER.");
                }

                if (pos is "BEFORE" or "AFTER" && string.IsNullOrWhiteSpace(referenceLayer))
                {
                    return OperationResult<LayerOrderInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"referenceLayer is required when position is {pos}.");
                }

                var layerResult = LayerResolver.Resolve(map, layerName, flatten: true);
                if (LayerResolver.FailIfNotOk<LayerOrderInfo>(layerResult, layerName) is { } layerFail)
                {
                    return layerFail;
                }

                var target = layerResult.Layer!;
                var root = map.Layers.ToList();
                var targetAtRoot = root.FindIndex(
                    l => ReferenceEquals(l, target)
                         || (l.URI is not null && target.URI is not null && l.URI == target.URI));
                if (targetAtRoot < 0)
                {
                    // 组内/跨层级移动不在本批语义（spike 披露：GroupLayer.MoveLayer 存在但语义另立）。
                    return OperationResult<LayerOrderInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"Layer '{layerName}' is not at the root of the map TOC; moving nested/grouped layers is out of scope for this batch.");
                }

                int index;
                var noOp = false;
                if (pos == MoveTargetIndex.PositionTop)
                {
                    index = 0;
                }
                else if (pos == MoveTargetIndex.PositionBottom)
                {
                    index = root.Count - 1;
                }
                else
                {
                    var refIndex = ResolveReferenceIndex(map, root, referenceLayer!, out var err);
                    if (refIndex < 0)
                    {
                        return OperationResult<LayerOrderInfo>.Fail(
                            ErrorCodes.InvalidArgument,
                            "referenceLayer could not be resolved at the map root"
                            + (err is null ? "." : ": " + err));
                    }

                    // ★ O-D043-04 修正：target 是"移除源层之后"列表的索引 ⇒ s<r 时参考下标下移 1 位。
                    // 索引计算已抽为纯函数 MoveTargetIndex（Shared/Core，无 SDK 依赖，可单测）。
                    if (!MoveTargetIndex.TryCompute(targetAtRoot, refIndex, pos, root.Count, out var plan))
                    {
                        return OperationResult<LayerOrderInfo>.Fail(
                            ErrorCodes.InvalidArgument,
                            "referenceLayer could not be resolved at the map root.");
                    }

                    index = plan.TargetIndex;
                    noOp = plan.IsNoOp;
                }

                if (!noOp)
                {
                    map.MoveLayer(target, index);
                }

                var order = map.Layers.Select(l => l.Name ?? string.Empty).ToList();
                return OperationResult<LayerOrderInfo>.Ok(new LayerOrderInfo
                {
                    MapName = map.Name ?? string.Empty,
                    LayerName = layerName,
                    Position = pos,
                    TargetIndex = index,
                    RootLayerOrder = order,
                });
            },
            TaskCreationOptions.None);

    /// <summary>
    /// 解析参考层在**根容器**中的「移动前」原始索引（<c>out error</c> 非 null 表示失败）。
    /// ★ D-044：本方法**只负责定位**，不做任何 BEFORE/AFTER 位移补偿 —— 补偿统一由纯函数
    /// <see cref="MoveTargetIndex.TryCompute"/> 完成（避免双重补偿，O-D043-04 收口）。
    /// </summary>
    private static int ResolveReferenceIndex(
        ArcGIS.Desktop.Mapping.Map map,
        List<ArcGIS.Desktop.Mapping.Layer> root,
        string referenceLayer,
        out string? error)
    {
        error = null;
        var refResult = LayerResolver.Resolve(map, referenceLayer, flatten: true);
        if (refResult.Status == LayerResolveStatus.Ambiguous)
        {
            error = "referenceLayer is ambiguous";
            return -1;
        }

        if (refResult.Status != LayerResolveStatus.Ok || refResult.Layer is null)
        {
            error = "referenceLayer not found";
            return -1;
        }

        var idx = root.FindIndex(
            l => ReferenceEquals(l, refResult.Layer)
                 || (l.URI is not null && refResult.Layer!.URI is not null && l.URI == refResult.Layer.URI));
        if (idx < 0)
        {
            error = "referenceLayer is not at the map root";
            return -1;
        }

        return idx;
    }

    // ================= D-046：简单符号与标注（2 读 + 2 写） =================

    private static string AmbiguousLayerMessage(string layerName, LayerResolveResult result)
        => result.CandidateIds.Count > 0
            ? $"Layer name '{layerName}' is ambiguous ({result.CandidateIds.Count} matches). Candidate layer ids: {string.Join(" | ", result.CandidateIds)}"
            : $"Layer name '{layerName}' is ambiguous ({result.CandidateIds.Count} matches).";

    /// <summary>D-046：符号层展平（含字符标记内层递归；深度上限 4 防环）。</summary>
    private static IEnumerable<CIMSymbolLayer> FlattenSymbolLayers(CIMSymbol? symbol, int depth = 0)
    {
        if (symbol is not CIMMultiLayerSymbol multi || multi.SymbolLayers is null || depth > 4)
        {
            yield break;
        }

        foreach (var layer in multi.SymbolLayers)
        {
            if (layer is null)
            {
                continue;
            }

            yield return layer;
            if (layer is CIMCharacterMarker marker && marker.Symbol is not null)
            {
                foreach (var inner in FlattenSymbolLayers(marker.Symbol, depth + 1))
                {
                    yield return inner;
                }
            }
        }
    }

    private static string? ToHexColor(CIMColor? color)
        => color is CIMRGBColor rgb
            ? $"#{(int)Math.Round(rgb.R):X2}{(int)Math.Round(rgb.G):X2}{(int)Math.Round(rgb.B):X2}"
            : null;

    private static bool TryParseHexColor(string? text, out CIMColor? color, out string error)
    {
        color = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var v = text.Trim().TrimStart('#');
        if (v.Length != 6 || !int.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            error = $"color '{text}' is invalid; expected #RRGGBB or RRGGBB.";
            return false;
        }

        color = CIMColor.CreateRGBColor((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }

    private static void PopulateSymbolFields(CIMSymbol? symbol, LayerSymbologyInfo info)
    {
        if (symbol is CIMPointSymbol)
        {
            info.SymbolKind = "Point";
        }
        else if (symbol is CIMLineSymbol)
        {
            info.SymbolKind = "Line";
        }
        else if (symbol is CIMPolygonSymbol)
        {
            info.SymbolKind = "Polygon";
        }

        foreach (var layer in FlattenSymbolLayers(symbol))
        {
            if (info.FillColor is null && layer is CIMSolidFill fill && fill.Color is not null)
            {
                info.FillColor = ToHexColor(fill.Color);
            }

            if (info.OutlineColor is null && layer is CIMSolidStroke stroke && stroke.Color is not null)
            {
                info.OutlineColor = ToHexColor(stroke.Color);
            }

            if (info.LineWidth is null && layer is CIMSolidStroke widthStroke && widthStroke.Width > 0)
            {
                info.LineWidth = widthStroke.Width;
            }

            if (info.PointSize is null && layer is CIMMarker marker && marker.Size > 0)
            {
                info.PointSize = marker.Size;
            }
        }
    }

    /// <summary>D-046：符号读取（非要素图层 → SupportsSymbology=false；复杂渲染器 → IsSimpleRenderer=false）。</summary>
    public Task<OperationResult<LayerSymbologyInfo>> GetLayerSymbologyAsync(
        string? mapName, string layerName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LayerSymbologyInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<LayerSymbologyInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var layerResult = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (layerResult.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<LayerSymbologyInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, layerResult));
                }

                if (layerResult.Status != LayerResolveStatus.Ok || layerResult.Layer is null)
                {
                    return OperationResult<LayerSymbologyInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                var info = new LayerSymbologyInfo { LayerName = layerName, SupportsSymbology = false };
                if (layerResult.Layer is FeatureLayer featureLayer)
                {
                    info.SupportsSymbology = true;
                    var renderer = featureLayer.GetRenderer();
                    info.RendererType = renderer?.GetType().Name;
                    if (renderer is CIMSimpleRenderer simpleRenderer)
                    {
                        info.IsSimpleRenderer = true;
                        PopulateSymbolFields(simpleRenderer.Symbol?.Symbol, info);
                    }

                    // D-063 增强（工单 B2 语义）：复杂渲染器不再仅返回 null —— 读出结构化摘要。
                    // 不可读出的项仍保持 null（不静默降级，G-82-C 三态判定不变）。
                    EnrichComplexRenderer(renderer, info);
                    EnrichLabelDigest(featureLayer, info);
                }

                return OperationResult<LayerSymbologyInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    /// <summary>D-046：简单符号写入（仅 CIMSimpleRenderer；写后读回）。</summary>
    public Task<OperationResult<SymbologySetInfo>> SetSimpleSymbologyAsync(
        string? mapName, string layerName, string? fillColor, string? outlineColor,
        double? pointSize, double? lineWidth, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<SymbologySetInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<SymbologySetInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var layerResult = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (layerResult.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<SymbologySetInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, layerResult));
                }

                if (layerResult.Status != LayerResolveStatus.Ok || layerResult.Layer is null)
                {
                    return OperationResult<SymbologySetInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                if (layerResult.Layer is not FeatureLayer featureLayer)
                {
                    return OperationResult<SymbologySetInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"Layer '{layerName}' does not support symbology editing (not a feature layer).");
                }

                if (fillColor is null && outlineColor is null && pointSize is null && lineWidth is null)
                {
                    return OperationResult<SymbologySetInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        "at least one of fillColor / outlineColor / pointSize / lineWidth is required.");
                }

                if (!TryParseHexColor(fillColor, out var fill, out var fillError))
                {
                    return OperationResult<SymbologySetInfo>.Fail(ErrorCodes.InvalidArgument, fillError);
                }

                if (!TryParseHexColor(outlineColor, out var outline, out var outlineError))
                {
                    return OperationResult<SymbologySetInfo>.Fail(ErrorCodes.InvalidArgument, outlineError);
                }

                if ((pointSize is not null && (double.IsNaN(pointSize.Value) || pointSize.Value <= 0))
                    || (lineWidth is not null && (double.IsNaN(lineWidth.Value) || lineWidth.Value <= 0)))
                {
                    return OperationResult<SymbologySetInfo>.Fail(
                        ErrorCodes.InvalidArgument, "pointSize and lineWidth must be positive numbers.");
                }

                var renderer = featureLayer.GetRenderer();
                if (renderer is not CIMSimpleRenderer simpleRenderer)
                {
                    return OperationResult<SymbologySetInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"Renderer of layer '{layerName}' is {renderer?.GetType().Name ?? "null"}; " +
                        "only SimpleRenderer is supported (no silent downgrade).");
                }

                foreach (var layer in FlattenSymbolLayers(simpleRenderer.Symbol?.Symbol))
                {
                    if (layer is CIMSolidFill solidFill && fill is not null)
                    {
                        solidFill.Color = fill;
                    }

                    if (layer is CIMSolidStroke solidStroke)
                    {
                        if (outline is not null)
                        {
                            solidStroke.Color = outline;
                        }

                        if (lineWidth is not null)
                        {
                            solidStroke.Width = lineWidth.Value;
                        }
                    }

                    if (layer is CIMMarker marker && pointSize is not null)
                    {
                        marker.Size = pointSize.Value;
                    }
                }

                featureLayer.SetRenderer(simpleRenderer);

                var probe = new LayerSymbologyInfo();
                PopulateSymbolFields((featureLayer.GetRenderer() as CIMSimpleRenderer)?.Symbol?.Symbol, probe);
                var info = new SymbologySetInfo
                {
                    LayerName = layerName,
                    FillColor = probe.FillColor,
                    OutlineColor = probe.OutlineColor,
                    PointSize = probe.PointSize,
                    LineWidth = probe.LineWidth,
                };
                return OperationResult<SymbologySetInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    /// <summary>D-046：标注读取（未启用非错误；非要素图层 → SupportsLabels=false）。</summary>
    public Task<OperationResult<LabelInfo>> GetLabelInfoAsync(
        string? mapName, string layerName, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LabelInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<LabelInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var layerResult = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (layerResult.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<LabelInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, layerResult));
                }

                if (layerResult.Status != LayerResolveStatus.Ok || layerResult.Layer is null)
                {
                    return OperationResult<LabelInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                var info = new LabelInfo { LayerName = layerName, SupportsLabels = false };
                if (layerResult.Layer is FeatureLayer featureLayer)
                {
                    info.SupportsLabels = true;
                    info.Enabled = featureLayer.IsLabelVisible;
                    var classes = featureLayer.LabelClasses;
                    info.LabelClassCount = classes is null ? 0 : classes.Count;
                    var first = classes is { Count: > 0 } ? classes[0] : null;
                    if (first is not null)
                    {
                        info.Expression = first.Expression;
                        info.ExpressionEngine = first.ExpressionEngine.ToString();
                        try
                        {
                            var textSymbol = first.GetTextSymbol();
                            info.FontFamily = textSymbol?.FontFamilyName;
                            info.FontSize = textSymbol?.Height;
                        }
                        catch (Exception)
                        {
                            // 字体读取不可达 → 如实置 null（Notes 已披露），不伪造
                        }
                    }
                }

                return OperationResult<LabelInfo>.Ok(info);
            },
            TaskCreationOptions.None);

    /// <summary>D-046：标注开关（无 labelClass 且请求开启 → INVALID_ARGUMENT，不代建）。</summary>
    public Task<OperationResult<LabelVisibilityInfo>> SetLabelVisibilityAsync(
        string? mapName, string layerName, bool enabled, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<LabelVisibilityInfo>>(
            () =>
            {
                var resolved = MapResolver.Resolve(mapName);
                if (MapResolver.FailIfNotOk<LabelVisibilityInfo>(resolved, mapName) is { } fail)
                {
                    return fail;
                }

                var layerResult = LayerResolver.Resolve(resolved.Map!, layerName, flatten: true);
                if (layerResult.Status == LayerResolveStatus.Ambiguous)
                {
                    return OperationResult<LabelVisibilityInfo>.Fail(
                        ErrorCodes.AmbiguousLayerName, AmbiguousLayerMessage(layerName, layerResult));
                }

                if (layerResult.Status != LayerResolveStatus.Ok || layerResult.Layer is null)
                {
                    return OperationResult<LabelVisibilityInfo>.Fail(
                        ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
                }

                if (layerResult.Layer is not FeatureLayer featureLayer)
                {
                    return OperationResult<LabelVisibilityInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"Layer '{layerName}' does not support labels (not a feature layer).");
                }

                var classes = featureLayer.LabelClasses;
                var count = classes is null ? 0 : classes.Count;
                if (enabled && count == 0)
                {
                    return OperationResult<LabelVisibilityInfo>.Fail(
                        ErrorCodes.InvalidArgument,
                        $"Layer '{layerName}' has no label classes; enabling labels requires an existing " +
                        "label class (this batch does not create one).");
                }

                featureLayer.SetLabelVisibility(enabled);

                var info = new LabelVisibilityInfo
                {
                    LayerName = layerName,
                    Enabled = featureLayer.IsLabelVisible,
                    LabelClassCount = count,
                };
                return OperationResult<LabelVisibilityInfo>.Ok(info);
            },
            TaskCreationOptions.None);
}
