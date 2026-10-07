using System.Collections;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>D-091 PS/UXP candidate tools. The frozen schema is public; Adobe-side behavior remains unverified.</summary>
internal static class D091PsSchemas
{
    private const string SchemaUri = "https://json-schema.org/draft/2020-12/schema";
    private const string IdPrefix = "arcgis-pro-mcp/candidate/";

    public static IReadOnlyDictionary<string, object?> GetCapabilities { get; } = Build(
        "ps_get_capabilities",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["probeDeep"] = Property("boolean", "是否执行深度探测（可能触达宿主；缺省 false 只读握手缓存）。", hasDefault: true, defaultValue: false),
        });

    public static IReadOnlyDictionary<string, object?> ApplyDesignRecipe { get; } = Build(
        "ps_apply_design_recipe",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["documentId"] = Property("string", "Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。"),
            ["specRevision"] = Property("string", "DesignSpec 不可变 revision 标识。写类 PS 工具必填；旧 revision ⇒ INVALID_STATE（旧 revision 不覆盖新版，FINAL 路线图 §2 DesignPatch 行）。"),
            ["apsReference"] = Property("object", "ApprovalRecord 引用（approvalId + planDigest + expiry + scopeDigest）。PS 写类工具必填；服务端校验 digest 与 scope，不匹配 ⇒ PERMISSION_DENIED。客户端 confirm=true 不构成批准（FINAL 路线图 §2 ApprovalRecord 行）。"),
            ["recipeId"] = Property("string", "已登记配方 ID（固定步骤 handler；不接受任意 JS/actionJSON）。"),
            ["typedArgs"] = Property("object", "配方参数（按配方 schema 校验）。", hasDefault: true, defaultValue: new Dictionary<string, object?>()),
            ["dryRun"] = Property("boolean", "仅求解不落写。", hasDefault: true, defaultValue: false),
        }, "documentId", "apsReference", "recipeId");

    public static IReadOnlyDictionary<string, object?> ImportDesignBundle { get; } = Build(
        "ps_import_design_bundle",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["bundlePath"] = Property("string", "素材包目录。"),
            ["documentId"] = Property("string", "Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。"),
            ["specRevision"] = Property("string", "DesignSpec 不可变 revision 标识。写类 PS 工具必填；旧 revision ⇒ INVALID_STATE（旧 revision 不覆盖新版，FINAL 路线图 §2 DesignPatch 行）。"),
            ["apsReference"] = Property("object", "ApprovalRecord 引用（approvalId + planDigest + expiry + scopeDigest）。PS 写类工具必填；服务端校验 digest 与 scope，不匹配 ⇒ PERMISSION_DENIED。客户端 confirm=true 不构成批准（FINAL 路线图 §2 ApprovalRecord 行）。"),
            ["placementMode"] = Property("string", "放置基准。", hasDefault: true, defaultValue: "mapFrame", enumValues: new[] { "mapFrame", "artboard", "absolute" }),
        }, "bundlePath", "documentId", "apsReference");

    public static IReadOnlyDictionary<string, object?> RefreshDesignBundle { get; } = Build(
        "ps_refresh_design_bundle",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["documentId"] = Property("string", "Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。"),
            ["bundlePath"] = Property("string", "新版素材包目录。"),
            ["specRevision"] = Property("string", "DesignSpec 不可变 revision 标识。写类 PS 工具必填；旧 revision ⇒ INVALID_STATE（旧 revision 不覆盖新版，FINAL 路线图 §2 DesignPatch 行）。"),
            ["apsReference"] = Property("object", "ApprovalRecord 引用（approvalId + planDigest + expiry + scopeDigest）。PS 写类工具必填；服务端校验 digest 与 scope，不匹配 ⇒ PERMISSION_DENIED。客户端 confirm=true 不构成批准（FINAL 路线图 §2 ApprovalRecord 行）。"),
            ["fencingGeneration"] = Property("integer", "fencing generation（旧实例迟到结果不得覆盖新状态）。"),
        }, "documentId", "bundlePath", "apsReference");

    public static IReadOnlyDictionary<string, object?> ExportDeliverables { get; } = Build(
        "ps_export_deliverables",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["documentId"] = Property("string", "Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。"),
            ["outputDir"] = Property("string", "交付输出目录（绝对路径，D 盘工作根内）。"),
            ["formats"] = Property("array", "输出格式清单。", enumItems: new[] { "psb", "psd", "tif", "png", "jpg", "pdf" }),
            ["dpi"] = Property("integer", "输出 DPI。", hasDefault: true, defaultValue: 300),
            ["colorProfile"] = Property("string", "ICC 目标（缺省＝文档 ICC，须回显 resolvedIcc）。"),
            ["overwrite"] = Property("boolean", "允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。", hasDefault: true, defaultValue: false,
                note: "现役复用点 OverwritePolicy.SchemaProperty（Source/Shared/ArcGISProMCP.Core/Results/OverwritePolicy.cs:40）；其 description 目前把默认值写在**文本**里而非 schema 的 default 键 ⇒ O-D080-04 待归并项。"),
        }, "documentId", "outputDir", "formats");

    private static IReadOnlyDictionary<string, object?> Build(
        string name,
        Dictionary<string, object?> properties,
        params string[] required)
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$schema"] = SchemaUri,
            ["$id"] = IdPrefix + name + ".schema.json",
            ["title"] = name,
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };

    private static Dictionary<string, object?> Property(
        string type,
        string description,
        bool hasDefault = false,
        object? defaultValue = null,
        string[]? enumValues = null,
        string[]? enumItems = null,
        string? note = null)
    {
        var property = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = type,
            ["description"] = description,
        };
        if (hasDefault)
            property["default"] = defaultValue;
        if (enumValues is not null)
            property["enum"] = enumValues;
        if (enumItems is not null)
            property["enum_items"] = enumItems;
        if (note is not null)
            property["note"] = note;
        return property;
    }
}

/// <summary>Shared NOT VERIFIED behavior and strict input checking for the D-091 PS batch.</summary>
public abstract class D091PsToolBase : McpToolBase
{
    protected override string CategoryName => ToolCategories.Photoshop;
    protected override string ExecutionTypeName => ExecutionTypes.Bridge;
    protected override bool? RequiresArcGISOverride => false;

    protected static OperationError? ValidateInput(IMCPTool tool, ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true } && ToolWriteClassification.RefusedInReadOnly(tool.Name))
            return ToolWriteClassification.ReadOnlyRefusal(tool.Name);

        var schema = tool.InputSchema;
        if (schema.TryGetValue("properties", out var rawProperties)
            && rawProperties is IReadOnlyDictionary<string, object?> properties)
        {
            var args = context.Arguments ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var key in args.Keys)
            {
                if (!properties.ContainsKey(key))
                    return Invalid($"Unknown argument '{key}' for tool '{tool.Name}'.", key);
            }

            if (schema.TryGetValue("required", out var rawRequired) && rawRequired is IEnumerable<string> required)
            {
                foreach (var name in required)
                {
                    if (!args.TryGetValue(name, out var value) || value is null)
                        return Invalid($"Missing required argument '{name}' for tool '{tool.Name}'.", name);
                }
            }

            foreach (var pair in args)
            {
                if (!properties.TryGetValue(pair.Key, out var rawProperty)
                    || rawProperty is not IReadOnlyDictionary<string, object?> property)
                    return Invalid($"Invalid schema property '{pair.Key}' for tool '{tool.Name}'.", pair.Key);

                if (property.TryGetValue("type", out var rawType)
                    && rawType is string expectedType
                    && !MatchesType(expectedType, pair.Value))
                    return Invalid($"Argument '{pair.Key}' must be {expectedType}.", pair.Key);

                if (property.TryGetValue("enum", out var rawEnum)
                    && rawEnum is IEnumerable<string> allowed
                    && TryString(pair.Value, out var enumValue)
                    && !allowed.Contains(enumValue, StringComparer.Ordinal))
                    return Invalid($"Argument '{pair.Key}' has an unsupported value.", pair.Key);

                if (property.TryGetValue("enum_items", out var rawItems)
                    && rawItems is IEnumerable<string> allowedItems
                    && TryArray(pair.Value, out var items))
                {
                    foreach (var item in items)
                    {
                        if (!TryString(item, out var itemValue)
                            || !allowedItems.Contains(itemValue, StringComparer.Ordinal))
                            return Invalid($"Argument '{pair.Key}' contains an unsupported item.", pair.Key);
                    }
                }
            }
        }

        return null;
    }

    protected static OperationResult<object?> NotVerifiedStub(string toolName, string operation)
        => OperationResult<object?>.Fail(
            ErrorCodes.NotImplemented,
            $"{toolName} is registered, but PS/UXP execution is NOT_VERIFIED and was not attempted.",
            JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = "NOT_VERIFIED",
                ["operation"] = operation,
                ["psRequestSent"] = false,
                ["sideEffects"] = false,
                ["reason"] = "UXP peer and P-05 deployment workflow are outside D-091 execution authority.",
            }));

    private static OperationError Invalid(string message, string field)
        => new(ErrorCodes.InvalidArgument, message, field);

    private static bool MatchesType(string expectedType, object? value)
        => expectedType switch
        {
            "string" => TryString(value, out _),
            "boolean" => value is bool || value is JsonElement { ValueKind: JsonValueKind.True or JsonValueKind.False },
            "integer" => IsInteger(value),
            "object" => value is IReadOnlyDictionary<string, object?>
                || value is IDictionary<string, object?>
                || value is JsonElement { ValueKind: JsonValueKind.Object },
            "array" => TryArray(value, out _),
            _ => false,
        };

    private static bool IsInteger(object? value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong
            || value is JsonElement element
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt64(out _);

    private static bool TryString(object? value, out string result)
    {
        if (value is string text)
        {
            result = text;
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.String } element)
        {
            result = element.GetString() ?? string.Empty;
            return true;
        }

        result = string.Empty;
        return false;
    }

    private static bool TryArray(object? value, out IReadOnlyList<object?> result)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            result = jsonArray.EnumerateArray().Select(item => (object?)item).ToArray();
            return true;
        }

        if (value is IEnumerable enumerable && value is not string && value is not IDictionary<string, object?>)
        {
            result = enumerable.Cast<object?>().ToArray();
            return true;
        }

        result = Array.Empty<object?>();
        return false;
    }
}

/// <summary>Reports local PS route health separately from unverified Adobe peer capabilities.</summary>
public sealed class PsGetCapabilitiesTool : D091PsToolBase
{
    private const string HealthPath = "/internal/ps/v1/health";
    private const string ExpectedService = "arcgis-pro-mcp-internal-ps-channel";

    public override string Name => "ps_get_capabilities";
    public override string Description => "报告本机 PS 通道路由健康状态；这不证明 Photoshop/UXP 对端能力。深度探测在 D-091 中始终禁用，并如实返回 NOT_VERIFIED。";
    public override IReadOnlyDictionary<string, object?> InputSchema => D091PsSchemas.GetCapabilities;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (ValidateInput(this, context) is { } error)
            return OperationResult<object?>.Fail(error);

        var probeDeepRequested = context.Arguments is not null
            && context.Arguments.TryGetValue("probeDeep", out var rawProbeDeep)
            && (rawProbeDeep is bool requested && requested
                || rawProbeDeep is JsonElement { ValueKind: JsonValueKind.True });
        var routeHealth = await ProbeLocalRouteAsync(context.Settings, context.CancellationToken).ConfigureAwait(false);
        return OperationResult<object?>.Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = "NOT_VERIFIED",
            ["capabilities"] = Array.Empty<string>(),
            ["peer"] = "Photoshop/UXP",
            ["probeDeepRequested"] = probeDeepRequested,
            ["deepProbePerformed"] = false,
            ["psRequestSent"] = false,
            ["routeHealth"] = routeHealth,
        }, "The local route check does not verify a Photoshop/UXP peer.");
    }

    private static async Task<IReadOnlyDictionary<string, object?>> ProbeLocalRouteAsync(
        MCPSettings settings,
        CancellationToken cancellationToken)
    {
        var address = ParseLoopback(settings.Host);
        if (address is null || settings.Port is < 1 or > 65535)
            return RouteNotProbed("configured_host_not_loopback_or_port_invalid");

        var uri = new UriBuilder(Uri.UriSchemeHttp, address.ToString(), settings.Port, HealthPath).Uri;
        using var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
        using var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
            MaxResponseContentBufferSize = 16 * 1024,
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var budgetMs = settings.RequestTimeoutMs > 0 ? Math.Clamp(settings.RequestTimeoutMs, 100, 1500) : 750;
        timeout.CancelAfter(budgetMs);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "UNAVAILABLE",
                    ["requestAttempted"] = true,
                    ["httpStatusCode"] = (int)response.StatusCode,
                    ["serviceIdentityMatched"] = false,
                };
            }

            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var identityMatched = root.ValueKind == JsonValueKind.Object
                && JsonString(root, "service") == ExpectedService
                && JsonString(root, "route") == HealthPath
                && JsonBoolean(root, "newListener") == false
                && JsonBoolean(root, "boundToExistingListener") == true;
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = identityMatched ? "HEALTHY" : "UNVERIFIED_RESPONSE",
                ["requestAttempted"] = true,
                ["httpStatusCode"] = (int)response.StatusCode,
                ["serviceIdentityMatched"] = identityMatched,
                ["schemaVersion"] = root.ValueKind == JsonValueKind.Object ? JsonString(root, "schemaVersion") : null,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RouteUnavailable("timeout");
        }
        catch (HttpRequestException)
        {
            return RouteUnavailable("connection_failed");
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = "UNVERIFIED_RESPONSE",
                ["requestAttempted"] = true,
                ["serviceIdentityMatched"] = false,
                ["reason"] = "invalid_health_json",
            };
        }
        catch (IOException)
        {
            return RouteUnavailable("response_read_failed");
        }
    }

    private static IPAddress? ParseLoopback(string? host)
    {
        if (string.Equals(host?.Trim(), "localhost", StringComparison.OrdinalIgnoreCase))
            return IPAddress.Loopback;
        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address) ? address : null;
    }

    private static string? JsonString(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? JsonBoolean(JsonElement root, string property)
        => root.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    private static IReadOnlyDictionary<string, object?> RouteNotProbed(string reason)
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = "NOT_PROBED",
            ["requestAttempted"] = false,
            ["serviceIdentityMatched"] = false,
            ["reason"] = reason,
        };

    private static IReadOnlyDictionary<string, object?> RouteUnavailable(string reason)
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = "UNAVAILABLE",
            ["requestAttempted"] = true,
            ["serviceIdentityMatched"] = false,
            ["reason"] = reason,
        };
}

/// <summary>NOT VERIFIED D-091 stub for the fixed design recipe operation.</summary>
public sealed class PsApplyDesignRecipeTool : D091PsToolBase
{
    public override string Name => "ps_apply_design_recipe";
    public override string Description => "按固定 recipeId 应用设计配方；当前 UXP 对端与批准链未验证，本实现 fail-closed，不发送 PS 请求、不执行配方。";
    public override IReadOnlyDictionary<string, object?> InputSchema => D091PsSchemas.ApplyDesignRecipe;
    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        => Task.FromResult(ValidateInput(this, context) is { } error
            ? OperationResult<object?>.Fail(error)
            : NotVerifiedStub(Name, "apply_design_recipe"));
}

/// <summary>NOT VERIFIED D-091 stub for importing a design bundle.</summary>
public sealed class PsImportDesignBundleTool : D091PsToolBase
{
    public override string Name => "ps_import_design_bundle";
    public override string Description => "导入拥有根内的设计包；当前 UXP 对端与路径拥有关系未验证，本实现 fail-closed，不读文件、不发送 PS 请求。";
    public override IReadOnlyDictionary<string, object?> InputSchema => D091PsSchemas.ImportDesignBundle;
    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        => Task.FromResult(ValidateInput(this, context) is { } error
            ? OperationResult<object?>.Fail(error)
            : NotVerifiedStub(Name, "import_design_bundle"));
}

/// <summary>NOT VERIFIED D-091 stub for refreshing a design bundle.</summary>
public sealed class PsRefreshDesignBundleTool : D091PsToolBase
{
    public override string Name => "ps_refresh_design_bundle";
    public override string Description => "刷新已拥有文档的设计包；当前 UXP 对端、fencing 与批准链未验证，本实现 fail-closed，不发送 PS 请求。";
    public override IReadOnlyDictionary<string, object?> InputSchema => D091PsSchemas.RefreshDesignBundle;
    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        => Task.FromResult(ValidateInput(this, context) is { } error
            ? OperationResult<object?>.Fail(error)
            : NotVerifiedStub(Name, "refresh_design_bundle"));
}

/// <summary>NOT VERIFIED D-091 stub for exporting Photoshop deliverables.</summary>
public sealed class PsExportDeliverablesTool : D091PsToolBase
{
    public override string Name => "ps_export_deliverables";
    public override string Description => "从已拥有的 Photoshop 文档导出交付物；当前 UXP 与路径权限未验证，本实现 fail-closed，不创建或覆盖文件。";
    public override IReadOnlyDictionary<string, object?> InputSchema => D091PsSchemas.ExportDeliverables;
    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
        => Task.FromResult(ValidateInput(this, context) is { } error
            ? OperationResult<object?>.Fail(error)
            : NotVerifiedStub(Name, "export_deliverables"));
}
