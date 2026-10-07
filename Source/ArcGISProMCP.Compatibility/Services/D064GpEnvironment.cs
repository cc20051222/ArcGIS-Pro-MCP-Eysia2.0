using System;
using System.Collections.Generic;
using System.Linq;
using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-064 · GP 环境设置的**会话级存储与下发**。
/// <para><b>可达性如实披露（重要）</b>：Pro SDK 的 GP 执行面无"环境查询"API ——
/// <c>Geoprocessing.ExecuteToolAsync(..., environment, ...)</c> 只接受**每次运行的**环境数组
/// （<c>MakeEnvironmentArray</c> 产物形态：<c>"&lt;name&gt; &lt;value&gt;"</c>），
/// 既无 <c>arcpy.env</c> 式常驻环境，也无环境读回接口。因此本实现：
/// ① 在**进程内**维护会话环境（首见态为基线快照）；
/// ② 每次 GP 调用**下发**为环境数组（<see cref="BuildArray"/>），从而真正影响后续 GP/受控调用；
/// ③ <c>get_environment</c> 返回会话存储 + 未设置项列入 <c>unset</c>（**不臆造值**）。
/// 语义等价于"会话级、不落盘、进程结束失效"，与工单口径一致。</para>
/// </summary>
public static class D064GpEnvironment
{
    /// <summary>会话环境键 → GP 环境数组名。</summary>
    public static readonly IReadOnlyDictionary<string, string> GpNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["workspace"] = "workspace",
        ["scratchWorkspace"] = "scratchWorkspace",
        ["outputCoordinateSystem"] = "outputCoordinateSystem",
        ["extent"] = "extent",
        ["mask"] = "mask",
        ["cellSize"] = "cellSize",
        ["overwriteOutput"] = "overwriteoutput",
        ["parallelProcessing"] = "parallelProcessing",
        ["parallelProcessingFactor"] = "parallelProcessingFactor",
    };

    private static readonly object Gate = new();
    private static readonly Dictionary<string, object?> Current = new(StringComparer.Ordinal);
    private static Dictionary<string, object?>? _baseline;

    /// <summary>当前会话环境快照（未设置项缺省）。</summary>
    public static GpEnvironmentInfo Snapshot()
    {
        lock (Gate)
        {
            return ToInfo(Current);
        }
    }

    /// <summary>
    /// 写入会话环境（未提供的键保持不动）。
    /// <paramref name="reset"/>=true ⇒ 还原到**会话初始基线**（首见快照；无基线 ⇒ 清空）。
    /// </summary>
    public static SetEnvironmentResult Apply(GpEnvironmentInfo? desired, bool reset)
    {
        lock (Gate)
        {
            _baseline ??= new Dictionary<string, object?>(Current, StringComparer.Ordinal);
            var before = ToInfo(Current);

            var applied = new List<string>();
            var unchanged = new List<string>();

            if (reset)
            {
                Current.Clear();
                foreach (var kv in _baseline)
                {
                    Current[kv.Key] = kv.Value;
                }

                applied.AddRange(GpNames.Keys.Where(k => Current.ContainsKey(k)));
            }
            else
            {
                foreach (var (key, value) in Flatten(desired))
                {
                    if (value is null)
                    {
                        unchanged.Add(key);
                        continue;
                    }

                    Current[key] = value;
                    applied.Add(key);
                }
            }

            var after = ToInfo(Current);
            var baselineMatches = reset && BaselineMatches();

            return new SetEnvironmentResult
            {
                Applied = applied,
                Unchanged = unchanged,
                Before = before,
                After = after,
                Reset = reset,
                ResetRestoredBaseline = reset ? baselineMatches : null,
                Scope = "session-scoped in-memory GP environment (not persisted; applied to every subsequent GP/controlled run; "
                        + "resets when the process restarts). Pro SDK exposes no ambient GP environment read-back, "
                        + "so get_environment reports this session store and lists never-set keys under 'unset'.",
            };
        }
    }

    /// <summary>
    /// 构造 GP 环境（每次运行下发；无设置 ⇒ null，保持既有行为不变）。
    /// 形态 = IEnumerable&lt;KeyValuePair&lt;string,string&gt;&gt;（Pro 3.5 的 ExecuteToolAsync
    /// environment 参数**实际类型**；构建期 CS1503 证伪了 "string[]" 的文档印象 —— 如实记账）。
    /// </summary>
    public static List<KeyValuePair<string, string>>? BuildEnvironment()
    {
        lock (Gate)
        {
            if (Current.Count == 0)
            {
                return null;
            }

            var entries = new List<KeyValuePair<string, string>>();
            foreach (var kv in Current)
            {
                var text = Render(kv.Value);
                if (text is null)
                {
                    continue;
                }

                entries.Add(new KeyValuePair<string, string>(GpNames[kv.Key], text));
            }

            return entries.Count == 0 ? null : entries;
        }
    }

    private static bool BaselineMatches()
    {
        if (_baseline is null)
        {
            return Current.Count == 0;
        }

        if (_baseline.Count != Current.Count)
        {
            return false;
        }

        foreach (var kv in _baseline)
        {
            if (!Current.TryGetValue(kv.Key, out var v) || !Equals(v, kv.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<KeyValuePair<string, object?>> Flatten(GpEnvironmentInfo? info)
    {
        if (info is null)
        {
            yield break;
        }

        yield return new KeyValuePair<string, object?>("workspace", info.Workspace);
        yield return new KeyValuePair<string, object?>("scratchWorkspace", info.ScratchWorkspace);
        yield return new KeyValuePair<string, object?>("outputCoordinateSystem", info.OutputCoordinateSystem);
        yield return new KeyValuePair<string, object?>("extent", info.Extent);
        yield return new KeyValuePair<string, object?>("mask", info.Mask);
        yield return new KeyValuePair<string, object?>("cellSize", info.CellSize);
        yield return new KeyValuePair<string, object?>("overwriteOutput", info.OverwriteOutput);
        yield return new KeyValuePair<string, object?>("parallelProcessing", info.ParallelProcessing);
        yield return new KeyValuePair<string, object?>("parallelProcessingFactor", info.ParallelProcessingFactor);
    }

    private static GpEnvironmentInfo ToInfo(Dictionary<string, object?> map)
    {
        string? S(string key) => map.TryGetValue(key, out var v) ? v as string : null;
        bool? B(string key) => map.TryGetValue(key, out var v) && v is bool b ? b : null;
        int? I(string key) => map.TryGetValue(key, out var v) && v is int i ? i : null;

        var unset = GpNames.Keys.Where(k => !map.ContainsKey(k) || map[k] is null).ToList();

        return new GpEnvironmentInfo
        {
            Workspace = S("workspace"),
            ScratchWorkspace = S("scratchWorkspace"),
            OutputCoordinateSystem = S("outputCoordinateSystem"),
            Extent = S("extent"),
            Mask = S("mask"),
            CellSize = S("cellSize"),
            OverwriteOutput = B("overwriteOutput"),
            ParallelProcessing = B("parallelProcessing"),
            ParallelProcessingFactor = I("parallelProcessingFactor"),
            Unset = unset,
        };
    }

    private static string? Render(object? value)
        => value switch
        {
            null => null,
            bool b => b ? "true" : "false",
            string s when string.IsNullOrWhiteSpace(s) => null,
            _ => value.ToString(),
        };
}
