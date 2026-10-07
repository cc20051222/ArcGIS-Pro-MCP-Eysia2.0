using System.Security.Cryptography;
using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Jobs;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>D-084 B1：只读任务目录。</summary>
public sealed class ListJobsTool : McpToolBase
{
    public override string Name => "list_jobs";
    public override string Description => "枚举本地工作流作业，支持状态/类别筛选、恢复任务筛选、分页上限和 summary/full 响应。";
    protected override string CategoryName => ToolCategories.System;
    protected override bool? RequiresArcGISOverride => false;
    public override IReadOnlyDictionary<string, object?> InputSchema => D084Schemas.ListJobs;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var stateFilter = ToolArgs.GetString(context, "state");
        var kindFilter = ToolArgs.GetString(context, "kind");
        var maximum = ToolArgs.GetInt(context, "maxItems50") ?? 50;
        if (maximum is < 1 or > 500)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxItems50 must be between 1 and 500."));
        }
        if (stateFilter is not null && !new[] { "queued", "running", "awaiting", "partial", "cancelled", "failed", "done" }
                .Contains(stateFilter, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "state is not a supported job state."));

        var includeResumed = ToolArgs.GetBool(context, "includeResumed", true);
        var responseFormat = ToolArgs.GetString(context, "responseFormat") ?? "summary";
        if (responseFormat is not ("summary" or "full"))
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "responseFormat must be summary or full."));
        }

        var jobs = WorkflowJobStore.ListAll()
            .Select(s => new { State = s, Name = JobStateName(s) })
            .Where(x => stateFilter is null || string.Equals(x.Name, stateFilter, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(kindFilter) || string.Equals(x.State.Kind, kindFilter, StringComparison.OrdinalIgnoreCase))
            .Where(x => includeResumed || !WorkflowJobStore.WasResumed(x.State))
            .OrderByDescending(x => x.State.UpdatedUtc, StringComparer.Ordinal)
            .ThenBy(x => x.State.JobId, StringComparer.Ordinal)
            .ToList();

        var returned = jobs.Take(maximum).Select(x => JobSummary(x.State, x.Name, responseFormat == "full")).ToList();
        return Task.FromResult(OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["jobs"] = returned,
            ["returnedCount"] = returned.Count,
            ["totalMatched"] = jobs.Count,
            ["truncated"] = jobs.Count > returned.Count,
            ["responseFormat"] = responseFormat,
            ["includeResumed"] = includeResumed,
        }));
    }

    internal static string JobStateName(FolderJobState s)
        => WorkflowJobStore.JobStateName(s);

    private static Dictionary<string, object?> JobSummary(FolderJobState s, string state, bool full)
    {
        var item = new Dictionary<string, object?>
        {
            ["jobId"] = s.JobId,
            ["kind"] = s.Kind,
            ["state"] = state,
            ["total"] = s.Total,
            ["done"] = s.Done,
            ["failed"] = s.Failed,
            ["skipped"] = s.Skipped,
            ["pending"] = s.Pending,
            ["currentShard"] = s.CurrentShard,
            ["shardCount"] = s.ShardCount,
            ["resumeToken"] = s.ResumeToken,
            ["resumeCount"] = s.ResumeCount,
            ["resumedSkips"] = s.LastResumedSkips,
            ["cancelRequested"] = s.CancelRequested,
            ["cancelled"] = s.Cancelled,
            ["cancelReason"] = s.CancelReason,
            ["createdUtc"] = s.CreatedUtc,
            ["updatedUtc"] = s.UpdatedUtc,
            ["lastError"] = s.LastError,
        };
        if (full)
        {
            item["items"] = s.Items.Select(i => new Dictionary<string, object?>
            {
                ["index"] = i.Index, ["tool"] = i.Tool, ["target"] = i.Target, ["state"] = i.State,
                ["reason"] = i.Reason, ["errorCode"] = i.ErrorCode, ["artifact"] = i.Artifact,
                ["sha256"] = i.Sha256, ["durationMs"] = i.DurationMs,
            }).ToList();
        }
        return item;
    }
}

/// <summary>D-084 B1：向数据文件夹作业发出合作式取消请求。</summary>
public sealed class CancelJobTool : McpToolBase
{
    public override string Name => "cancel_job";
    public override string Description => "向现有工作流作业发送合作式取消请求；返回 accepted、awaiting 或已确认的 cancelled 状态。";
    protected override string CategoryName => ToolCategories.System;
    public override IReadOnlyDictionary<string, object?> InputSchema => D084Schemas.CancelJob;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var jobId = ToolArgs.GetString(context, "jobId");
        var reason = ToolArgs.GetString(context, "reason");
        var waitMs = ToolArgs.GetInt(context, "waitMs") ?? 0;
        if (string.IsNullOrWhiteSpace(jobId) || waitMs < 0 || waitMs > 60_000)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "jobId is required and waitMs must be between 0 and 60000.");
        }

        var result = await WorkflowJobStore.RequestCancelAsync(jobId!, reason, waitMs, context.CancellationToken).ConfigureAwait(false);
        if (result.Success)
        {
            var state = result.Data is not null && result.Data.TryGetValue("state", out var stateValue)
                ? stateValue?.ToString() : "awaiting";
            LoadFolderDataTool.AppendAudit(context, jobId!, Name,
                $"reason={reason ?? "cancel requested"}; state={state}; waitMs={waitMs}", true);
        }
        return ToolResult.From(result);
    }
}

/// <summary>D-084 B1：无副作用的结构化计划校验。</summary>
public sealed class ValidatePlanTool : McpToolBase
{
    public override string Name => "validate_plan";
    public override string Description => "只读校验结构化计划：步骤非空、禁止嵌套、工具已注册且参数满足必填项。";
    protected override string CategoryName => ToolCategories.General;
    public override IReadOnlyDictionary<string, object?> InputSchema => D084Schemas.ValidatePlan;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var plan = ToolArgs.GetObject(context, "plan");
        if (plan is null) return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "plan must be an object."));
        var strict = ToolArgs.GetBool(context, "strict", true);
        if (!plan.TryGetValue("steps", out var raw) || raw is not System.Collections.IEnumerable enumerable || raw is string)
        {
            return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "plan.steps must be a non-empty array of { tool, args }."));
        }

        var steps = enumerable.Cast<object?>().ToList();
        if (steps.Count == 0) return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "plan.steps must not be empty."));

        var errors = new List<Dictionary<string, object?>>();
        var warnings = new List<Dictionary<string, object?>>();
        var checkedSteps = new List<Dictionary<string, object?>>();
        var denied = new HashSet<string>(StringComparer.Ordinal) { "run_batch", "apply_processing_plan", "set_readonly_mode" };
        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index] is not IReadOnlyDictionary<string, object?> step)
            {
                errors.Add(PlanIssue(index, "(malformed step)", "step must be an object."));
                continue;
            }

            var name = ToolArgs.ReadString(step, "tool");
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(PlanIssue(index, "(missing tool)", "tool is required."));
                continue;
            }
            if (denied.Contains(name!))
            {
                errors.Add(PlanIssue(index, name!, "nested workflow/control tools are not allowed."));
                continue;
            }

            var tool = context.Registry?.Get(name!);
            if (tool is null)
            {
                var issue = PlanIssue(index, name!, "tool is not registered.");
                (strict ? errors : warnings).Add(issue);
                continue;
            }

            var args = ToolArgs.Read(step, "args") as IReadOnlyDictionary<string, object?>
                       ?? new Dictionary<string, object?>();
            var requiredError = ToolArgumentValidator.Validate(tool, args);
            if (requiredError is not null)
            {
                errors.Add(PlanIssue(index, name!, requiredError.Message));
                continue;
            }

            checkedSteps.Add(new Dictionary<string, object?> { ["index"] = index, ["tool"] = name, ["valid"] = true });
        }

        var valid = errors.Count == 0;
        return Task.FromResult(OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["valid"] = valid,
            ["strict"] = strict,
            ["stepCount"] = steps.Count,
            ["validatedSteps"] = checkedSteps,
            ["errors"] = errors,
            ["warnings"] = warnings,
            ["sideEffects"] = false,
        }));
    }

    private static Dictionary<string, object?> PlanIssue(int index, string tool, string message)
        => new() { ["stepIndex"] = index, ["tool"] = tool, ["code"] = ErrorCodes.InvalidArgument, ["message"] = message };
}

/// <summary>D-084 B1：基于真实注册表的工具目录检索。</summary>
public sealed class DescribeToolCatalogTool : McpToolBase
{
    public override string Name => "describe_tool_catalog";
    public override string Description => "检索本 MCP 实际注册的工具、分类、说明和输入 schema；结果来自当前注册表。";
    protected override string CategoryName => ToolCategories.General;
    protected override bool? RequiresArcGISOverride => false;
    public override IReadOnlyDictionary<string, object?> InputSchema => D084Schemas.DescribeToolCatalog;

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Registry is null) return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidState, "tool registry is unavailable."));
        var query = ToolArgs.GetString(context, "query");
        var category = ToolArgs.GetString(context, "category");
        var scenarioId = ToolArgs.GetString(context, "scenarioId");
        var includeSchema = ToolArgs.GetBool(context, "includeSchema", true);
        var maximum = ToolArgs.GetInt(context, "maxItems50") ?? 50;
        if (maximum is < 1 or > 500) return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxItems50 must be between 1 and 500."));

        IReadOnlySet<string>? scenarioTools = null;
        var scenarioMappingStatus = "not_requested";
        if (scenarioId is not null)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(scenarioId, @"^S(?:0[1-9]|[1-5][0-9]|60)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                return Task.FromResult(OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "scenarioId must be S01–S60."));
            if (D084ScenarioMap.TryGetValue(scenarioId, out var names))
            {
                scenarioTools = names;
                scenarioMappingStatus = "mapped";
            }
            else
            {
                scenarioTools = new HashSet<string>(StringComparer.Ordinal);
                scenarioMappingStatus = "unmapped";
            }
        }

        var tools = context.Registry.List()
            .Where(t => category is null || string.Equals(t.Metadata.Category, category, StringComparison.OrdinalIgnoreCase))
            .Where(t => scenarioTools is null || scenarioTools.Contains(t.Name))
            .Where(t => string.IsNullOrWhiteSpace(query)
                        || Contains(t.Name, query!) || Contains(t.Metadata.DisplayName, query!)
                        || Contains(t.Description, query!) || Contains(t.Metadata.Category, query!))
            .ToList();
        var returned = tools.Take(maximum).Select(t =>
        {
            var item = new Dictionary<string, object?>
            {
                ["name"] = t.Name,
                ["displayName"] = t.Metadata.DisplayName,
                ["description"] = t.Description,
                ["category"] = t.Metadata.Category,
                ["executionType"] = t.Metadata.ExecutionType,
                ["requiresArcGIS"] = t.Metadata.RequiresArcGIS,
                ["supportsCancellation"] = t.Metadata.SupportsCancellation,
            };
            if (includeSchema) item["inputSchema"] = t.InputSchema;
            return item;
        }).ToList();

        return Task.FromResult(OperationResult<object?>.Ok(new Dictionary<string, object?>
        {
            ["tools"] = returned,
            ["returnedCount"] = returned.Count,
            ["totalMatched"] = tools.Count,
            ["truncated"] = tools.Count > returned.Count,
            ["registryCount"] = context.Registry.Count,
            ["includeSchema"] = includeSchema,
            ["scenarioId"] = scenarioId,
            ["scenarioMappingStatus"] = scenarioMappingStatus,
        }));
    }

    private static bool Contains(string? value, string query) => value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> D084ScenarioMap =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["S01"] = Set("scan_data_folder", "load_folder_data", "apply_processing_plan", "list_jobs", "get_job_status", "get_job_report"),
            ["S03"] = Set("get_geometry_info", "find_identical", "generate_quality_report"),
            ["S04"] = Set("get_domains", "get_subtypes", "find_identical", "generate_quality_report"),
            ["S19"] = Set("list_layouts", "list_layout_elements", "set_layout_element_properties", "set_label_properties"),
            ["S23"] = Set("configure_map_series", "export_map_series", "set_layout_element_properties"),
            ["S26"] = Set("generate_quality_report", "describe_tool_catalog", "set_layout_element_properties"),
        };

    private static IReadOnlySet<string> Set(params string[] names) => new HashSet<string>(names, StringComparer.Ordinal);
}

/// <summary>D-084 B2：只读几何摘要。</summary>
public sealed class GetGeometryInfoTool : McpToolBase
{
    public override string Name => "get_geometry_info";
    public override string Description => "读取几何类型、空间参考、计数、包络、长度/面积摘要；超出 maxFeatures 时披露采样口径。";
    protected override string CategoryName => ToolCategories.Attribute;
    public override IReadOnlyDictionary<string, object?> InputSchema => D084Schemas.GetGeometryInfo;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D084Analysis is not { } analysis) return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "geometry analysis service is unavailable.");
        var layer = ToolArgs.GetString(context, "layer");
        var unit = ToolArgs.GetString(context, "geometryUnit") ?? "layer";
        var max = ToolArgs.GetInt(context, "maxFeatures") ?? 1000;
        if (string.IsNullOrWhiteSpace(layer) || max is < 1 or > 1_000_000)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layer is required and maxFeatures must be between 1 and 1000000.");
        return ToolResult.From(await analysis.GetGeometryInfoAsync(layer!, unit, max, context.CancellationToken).ConfigureAwait(false));
    }
}

/// <summary>D-084 B2：属性/几何相同项分组。</summary>
public sealed class FindIdenticalTool : McpToolBase
{
    public override string Name => "find_identical";
    public override string Description => "以显式 attributes、geometry 或 attributes+geometry 判据查找重复要素，并披露解析字段与截断口径。";
    protected override string CategoryName => ToolCategories.Quality;
    public override IReadOnlyDictionary<string, object?> InputSchema => D084Schemas.FindIdentical;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D084Analysis is not { } analysis) return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "identical analysis service is unavailable.");
        var layer = ToolArgs.GetString(context, "layer");
        var mode = ToolArgs.GetString(context, "mode");
        var fields = ToolArgs.GetStringList(context, "fields");
        var tolerance = ToolArgs.GetDouble(context, "tolerance") ?? 0d;
        var max = ToolArgs.GetInt(context, "maxFeatures") ?? 1000;
        if (string.IsNullOrWhiteSpace(layer) || mode is not ("attributes" or "geometry" or "attributes+geometry")
            || !double.IsFinite(tolerance) || tolerance < 0 || max is < 1 or > 1_000_000)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layer, supported mode, non-negative finite tolerance and maxFeatures 1..1000000 are required.");
        return ToolResult.From(await analysis.FindIdenticalAsync(layer!, mode!, fields, tolerance, max, context.CancellationToken).ConfigureAwait(false));
    }
}

/// <summary>D-084 B2：内存 QC 报告，可选安全落盘。</summary>
public sealed class GenerateQualityReportTool : McpToolBase
{
    private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };
    public override string Name => "generate_quality_report";
    public override string Description => "生成结构化数据质量报告；不带 reportPath 时仅返回内存结果，带路径时创建唯一新文件并回传 ArtifactManifest。";
    protected override string CategoryName => ToolCategories.Quality;
    public override IReadOnlyDictionary<string, object?> InputSchema => D084Schemas.GenerateQualityReport;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host?.D084Analysis is not { } analysis) return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, "quality analysis service is unavailable.");
        var dataset = ToolArgs.GetString(context, "dataset");
        var reportPath = ToolArgs.GetString(context, "reportPath");
        var severityFloor = ToolArgs.GetString(context, "severityFloor") ?? "info";
        var max = ToolArgs.GetInt(context, "maxFeatures") ?? 1000;
        var rules = ToolArgs.GetStringList(context, "rules");
        if (string.IsNullOrWhiteSpace(dataset) || severityFloor is not ("info" or "warning" or "error")
            || max is < 1 or > 1_000_000)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "dataset, supported severityFloor and maxFeatures 1..1000000 are required.");
        if (rules.Count == 0) rules = new[] { "nulls", "duplicates", "domain", "geometry" };
        var allowed = new HashSet<string>(new[] { "nulls", "duplicates", "domain", "subtype", "geometry", "topology", "units", "outliers", "crs" }, StringComparer.Ordinal);
        if (rules.Any(r => !allowed.Contains(r))) return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "rules contains an unsupported quality rule.");
        if (reportPath is not null && context.ReadOnly is { IsReadOnly: true })
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "reportPath is refused while read-only mode is enabled; no file was written.");

        var reportResult = await analysis.GenerateQualityReportAsync(dataset!, rules, severityFloor, max, context.CancellationToken).ConfigureAwait(false);
        if (!reportResult.Success || reportResult.Data is null) return ToolResult.From(reportResult);
        var report = new Dictionary<string, object?>(reportResult.Data, StringComparer.Ordinal)
        {
            ["reportVersion"] = "1.0",
            ["generatedUtc"] = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["severityFloor"] = severityFloor,
            ["requestedRules"] = rules,
        };

        if (reportPath is null) return OperationResult<object?>.Ok(report, "Report generated in memory; no file was written.");
        if (!Path.IsPathFullyQualified(reportPath)) return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "reportPath must be an absolute path.");

        string fullPath;
        try { fullPath = Path.GetFullPath(reportPath); }
        catch (Exception ex) { return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "reportPath is invalid.", ex.Message); }
        if (!string.Equals(Path.GetPathRoot(fullPath), @"D:\", StringComparison.OrdinalIgnoreCase))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "G-197: reportPath must be on D:; no file was written.");
        var protectedHit = ProtectedOutputPathGuard.Match(fullPath);
        if (protectedHit is not null) return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, $"reportPath is refused by the path guard ('{protectedHit}').");
        if (WorkflowJobStore.IsTempLikePath(fullPath)) return OperationResult<object?>.Fail(ErrorCodes.PathEscapeRejected, "reportPath must not live under %TEMP%.");

        var bytes = JsonSerializer.SerializeToUtf8Bytes(report, ReportJson);
        try
        {
            if (Directory.Exists(fullPath)) return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "reportPath names an existing directory.");
            using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
        }
        catch (IOException ex) when (File.Exists(fullPath))
        {
            return OperationResult<object?>.Fail(ErrorCodes.OutputExists, $"Report file already exists: {fullPath}.", ex.Message);
        }
        catch (Exception ex)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Writing quality report failed.", ex.Message);
        }

        var manifest = new Dictionary<string, object?>
        {
            ["artifacts"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["path"] = fullPath,
                    ["kind"] = "quality-report",
                    ["bytes"] = bytes.LongLength,
                    ["sha256"] = Convert.ToHexString(SHA256.HashData(bytes)),
                }
            }
        };
        report["reportPath"] = fullPath;
        report["artifactManifest"] = manifest;
        return OperationResult<object?>.Ok(report, "Report file created with OUTPUT_EXISTS protection; ArtifactManifest included.");
    }
}

/// <summary>D-084 B2：版面元素白名单属性变更（Session）。</summary>
public sealed class SetLayoutElementPropertiesTool : McpToolBase
{
    public override string Name => "set_layout_element_properties";
    public override string Description => "仅修改布局元素白名单属性 x/y/width/height/rotation/visible/locked/name；白名单外属性整体拒绝。";
    protected override string CategoryName => ToolCategories.Layout;
    public override IReadOnlyDictionary<string, object?> InputSchema => D084Schemas.SetLayoutElementProperties;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null) return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        var layout = ToolArgs.GetString(context, "layout");
        var elementId = ToolArgs.GetString(context, "elementId");
        var properties = ToolArgs.GetObject(context, "properties");
        var units = ToolArgs.GetString(context, "units") ?? "page";
        if (string.IsNullOrWhiteSpace(layout) || string.IsNullOrWhiteSpace(elementId) || properties is null || properties.Count == 0)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layout, elementId and a non-empty properties object are required.");
        var valid = D084WriteArguments.ValidateElementProperties(properties, units);
        if (valid is not null) return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, valid);
        return ToolResult.From(await context.Host.Layout.SetElementPropertiesAsync(layout!, elementId!, properties, units, context.CancellationToken).ConfigureAwait(false));
    }
}

/// <summary>D-084 B2：受限的标注属性修改（Session）。</summary>
public sealed class SetLabelPropertiesTool : McpToolBase
{
    public override string Name => "set_label_properties";
    public override string Description => "更新图层标注表达式、字体、字号、放置规则或可见性；表达式限单一字段引用，不接受脚本。";
    protected override string CategoryName => ToolCategories.Layer;
    public override IReadOnlyDictionary<string, object?> InputSchema => D084Schemas.SetLabelProperties;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null) return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        var layer = ToolArgs.GetString(context, "layer");
        var expression = ToolArgs.GetString(context, "expression");
        var fontFamily = ToolArgs.GetString(context, "fontFamily");
        var fontSize = ToolArgs.GetDouble(context, "fontSize");
        var placement = ToolArgs.GetString(context, "placement");
        var visibleRaw = ToolArgs.GetValue(context, "visible");
        bool? visible = visibleRaw is bool b ? b : null;
        if (string.IsNullOrWhiteSpace(layer)) return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "layer is required.");
        if (fontSize.HasValue && (!double.IsFinite(fontSize.Value) || fontSize.Value < 1)) return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "fontSize must be a finite value of at least 1 pt.");
        if (placement is not null && !D084WriteArguments.Placements.Contains(placement)) return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "placement is not supported.");
        if (expression is not null && !D084WriteArguments.IsSafeLabelExpression(expression)) return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "expression must be one direct field reference; arbitrary scripts are refused.");
        if (expression is null && fontFamily is null && fontSize is null && placement is null && visible is null)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "at least one label property must be supplied.");
        return ToolResult.From(await context.Host.Layers.SetLabelPropertiesAsync(null, layer!, expression, fontFamily, fontSize, placement, visible, context.CancellationToken).ConfigureAwait(false));
    }
}

/// <summary>D-084 B2：map series 配置（Session）。</summary>
public sealed class ConfigureMapSeriesTool : McpToolBase
{
    public override string Name => "configure_map_series";
    public override string Description => "配置布局地图系列的索引/排序/命名字段和范围模式；不导出文件。";
    protected override string CategoryName => ToolCategories.Layout;
    public override IReadOnlyDictionary<string, object?> InputSchema => D084Schemas.ConfigureMapSeries;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.Host is null) return OperationResult<object?>.Fail(ErrorCodes.InternalError, "Host is unavailable.");
        var map = ToolArgs.GetString(context, "map");
        var indexField = ToolArgs.GetString(context, "indexField");
        var sortField = ToolArgs.GetString(context, "sortField");
        var nameField = ToolArgs.GetString(context, "nameField");
        var extentSource = ToolArgs.GetString(context, "extentSource") ?? "bookmarks";
        if (string.IsNullOrWhiteSpace(map) || string.IsNullOrWhiteSpace(indexField)
            || extentSource is not ("bookmarks" or "layer" or "fixed"))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "map and indexField are required; extentSource must be bookmarks, layer or fixed.");
        return ToolResult.From(await context.Host.Layout.ConfigureMapSeriesAsync(map!, indexField!, sortField, extentSource, nameField, context.CancellationToken).ConfigureAwait(false));
    }
}

internal static class D084WriteArguments
{
    internal static readonly HashSet<string> Placements = new(StringComparer.Ordinal)
    { "above", "center", "below", "above_left", "above_right", "below_left", "below_right" };

    private static readonly HashSet<string> ElementKeys = new(StringComparer.Ordinal)
    { "x", "y", "width", "height", "rotation", "visible", "locked", "name" };
    private static readonly HashSet<string> Units = new(StringComparer.Ordinal)
    { "page", "points", "inches", "mm", "cm" };

    internal static string? ValidateElementProperties(IReadOnlyDictionary<string, object?> values, string units)
    {
        if (!Units.Contains(units)) return "units must be page, points, inches, mm or cm.";
        foreach (var pair in values)
        {
            if (!ElementKeys.Contains(pair.Key)) return $"property '{pair.Key}' is outside the allowlist; no property was changed.";
            if (pair.Key is "visible" or "locked")
            {
                if (pair.Value is not bool) return $"property '{pair.Key}' must be boolean; no property was changed.";
            }
            else if (pair.Key == "name")
            {
                if (pair.Value is not string s || string.IsNullOrWhiteSpace(s)) return "property 'name' must be a non-empty string; no property was changed.";
            }
            else
            {
                var d = pair.Value switch { double n => n, float n => n, int n => n, long n => n, _ => double.NaN };
                if (!double.IsFinite(d) || ((pair.Key is "width" or "height") && d <= 0))
                    return $"property '{pair.Key}' must be a finite number" + ((pair.Key is "width" or "height") ? " greater than zero" : string.Empty) + "; no property was changed.";
            }
        }
        return null;
    }

    internal static bool IsSafeLabelExpression(string value)
    {
        var s = value.Trim();
        return System.Text.RegularExpressions.Regex.IsMatch(s, @"^\$feature\.[A-Za-z_][A-Za-z0-9_]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
               || System.Text.RegularExpressions.Regex.IsMatch(s, @"^\[[A-Za-z_][A-Za-z0-9_]*\]$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
}

/// <summary>冻结契约在 D-082 f03b-5-schemas 中；此处只将其映射为 MCP InputSchema。</summary>
internal static class D084Schemas
{
    private static Dictionary<string, object?> S(string type, string? description = null, object? defaultValue = null,
        object? minimum = null, object? maximum = null, string[]? values = null)
    {
        var d = new Dictionary<string, object?> { ["type"] = type };
        if (description is not null) d["description"] = description;
        if (defaultValue is not null) d["default"] = defaultValue;
        if (minimum is not null) d["minimum"] = minimum;
        if (maximum is not null) d["maximum"] = maximum;
        if (values is not null) d["enum"] = values;
        return d;
    }
    private static IReadOnlyDictionary<string, object?> Obj(Dictionary<string, object?> props, params string[] required)
        => new Dictionary<string, object?>
        {
            ["type"] = "object", ["properties"] = props, ["required"] = required,
            ["additionalProperties"] = false,
        };
    private static Dictionary<string, object?> P(params (string Name, object? Schema)[] fields)
        => fields.ToDictionary(x => x.Name, x => x.Schema, StringComparer.Ordinal);

    internal static readonly IReadOnlyDictionary<string, object?> ListJobs = Obj(P(
        ("state", S("string", "状态过滤。省略＝全部。", values: new[] { "queued", "running", "awaiting", "partial", "cancelled", "failed", "done" })),
        ("kind", S("string", "作业类别过滤（如 folder-workflow / ps-render）。")),
        ("maxItems50", S("integer", "返回条目上限（缺省 50，上限 500）。超限 ⇒ 截断并如实披露 truncated/returnedCount/totalMatched。 作业列举返回上限。", 50, 1, 500)),
        ("includeResumed", S("boolean", "是否包含已 resume 的作业（resumedSkips>0）。", true)),
        ("responseFormat", S("string", "summary|full；full 含逐作业 disclosures。", "summary", values: new[] { "summary", "full" }))));

    internal static readonly IReadOnlyDictionary<string, object?> CancelJob = Obj(P(
        ("jobId", S("string", "目标作业 ID（与现役 get_job_status 同名字段）。")),
        ("reason", S("string", "取消原因（进审计 details，不新增公开错误码）。")),
        ("waitMs", S("integer", "等待实际停止的毫秒数（缺省 0＝立即返回 accepted）。实际停止需写入核对完成后才判 cancelled；未停止 ⇒ 成功受理 + state=awaiting（FINAL 路线图 §7）。", 0))), "jobId");

    internal static readonly IReadOnlyDictionary<string, object?> ValidatePlan = Obj(P(
        ("plan", S("object", "计划对象（与 apply_processing_plan 的 plan 同构）：steps 非空/禁嵌套/工具须已注册/参数须过 required 校验。")),
        ("strict", S("boolean", "严格模式：未注册工具＝整体 fail（与现役行为一致）；false ⇒ 降为警告。", true))), "plan");

    internal static readonly IReadOnlyDictionary<string, object?> DescribeToolCatalog = Obj(P(
        ("query", S("string", "关键词/能力检索词。省略 ⇒ 返回全部工具名（分页）。")),
        ("category", S("string", "按现役 ToolCategories 过滤。")),
        ("scenarioId", S("string", "按 60 场景 ID（S01–S60，能力目录 §6）过滤。")),
        ("includeSchema", S("boolean", "是否内联完整 InputSchema（缺省 true）。", true)),
        ("maxItems50", S("integer", "目录检索返回上限。缺省 50，上限 500。", 50, 1, 500))));

    internal static readonly IReadOnlyDictionary<string, object?> GetGeometryInfo = Obj(P(
        ("layer", S("string", "图层名或数据源路径。")),
        ("geometryUnit", S("string", "长度/面积单位（layer＝图层 CRS 单位）。不允许静默换算。", "layer", values: new[] { "layer", "meters", "kilometers", "feet", "miles", "hectares", "acres", "square_meters" })),
        ("maxFeatures", S("integer", "参与计算/采样/返回的要素上限（缺省 1000）。超限不静默：必须给 countBasis + truncated。", 1000, 1, 1_000_000))), "layer");

    internal static readonly IReadOnlyDictionary<string, object?> FindIdentical = Obj(P(
        ("layer", S("string", "目标图层/表路径。")),
        ("mode", S("string", "重复判据；两种模式可组合但必须显式。", values: new[] { "attributes", "geometry", "attributes+geometry" })),
        ("fields", new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "string" }, ["default"] = Array.Empty<string>(), ["description"] = "参与属性比较的字段子集。空 ⇒ 全部可比字段（须回显 resolvedFields）。" }),
        ("tolerance", S("number", "几何比较容差（CRS 单位；缺省 0.0＝精确相等）。", 0d)),
        ("maxFeatures", S("integer", "参与计算/采样/返回的要素上限（缺省 1000）。超限不静默：必须给 countBasis + truncated。", 1000, 1, 1_000_000))), "layer", "mode");

    internal static readonly IReadOnlyDictionary<string, object?> GenerateQualityReport = Obj(P(
        ("dataset", S("string", "目标数据集/图层。")),
        ("rules", new Dictionary<string, object?> { ["type"] = "array", ["items"] = new Dictionary<string, object?> { ["type"] = "string", ["enum"] = new[] { "nulls", "duplicates", "domain", "subtype", "geometry", "topology", "units", "outliers", "crs" } }, ["default"] = new[] { "nulls", "duplicates", "domain", "geometry" }, ["description"] = "启用的质量规则目录（固定枚举，不接任意表达式）。" }),
        ("severityFloor", S("string", "最低报告严重度。", "info", values: new[] { "info", "warning", "error" })),
        ("reportPath", S("string", "可选：结构化报告落盘绝对路径。一旦提供，该工具按 W（写）检查：路径守卫 + OUTPUT_EXISTS 闸门 + 产物进 ArtifactManifest。未提供则只返回内存结果。")),
        ("maxFeatures", S("integer", "参与计算/采样/返回的要素上限（缺省 1000）。超限不静默：必须给 countBasis + truncated。", 1000, 1, 1_000_000))), "dataset");

    internal static readonly IReadOnlyDictionary<string, object?> SetLayoutElementProperties = Obj(P(
        ("layout", S("string", "布局名。")),
        ("elementId", S("string", "元素语义 ID（来自 list_layout_elements）。")),
        ("properties", S("object", "允许修改的属性白名单（x/y/width/height/rotation/visible/locked/name）。白名单外键 ⇒ INVALID_ARGUMENT 且整体不生效。")),
        ("units", S("string", "位置/尺寸单位。", "page", values: new[] { "page", "points", "inches", "mm", "cm" }))), "layout", "elementId", "properties");

    internal static readonly IReadOnlyDictionary<string, object?> SetLabelProperties = Obj(P(
        ("layer", S("string", "图层名或路径。")),
        ("expression", S("string", "标注表达式（仅受限子集；不做任意脚本通道）。")),
        ("fontFamily", S("string", "字体族（须在本机字体清单内，否则 INVALID_ARGUMENT）。")),
        ("fontSize", S("number", "字号（pt）；下限受模板最小字号保护（FINAL 路线图 §6 第 5 条）。", minimum: 1)),
        ("placement", S("string", "放置规则。", values: new[] { "above", "center", "below", "above_left", "above_right", "below_left", "below_right" })),
        ("visible", S("boolean", "可见性（与现役 set_label_visibility 重叠部分以整流器收敛）。"))), "layer");

    internal static readonly IReadOnlyDictionary<string, object?> ConfigureMapSeries = Obj(P(
        ("map", S("string", "地图名。")),
        ("indexField", S("string", "页码/索引字段。")),
        ("sortField", S("string", "排序字段。")),
        ("extentSource", S("string", "范围来源。", "bookmarks", values: new[] { "bookmarks", "layer", "fixed" })),
        ("nameField", S("string", "页名字段。"))), "map", "indexField");
}
