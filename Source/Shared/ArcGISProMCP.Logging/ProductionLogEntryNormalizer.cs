using System.Globalization;

namespace ArcGISProMCP.Logging;

/// <summary>
/// Converts legacy production logging calls into a fixed structured contract.
/// Free text, exception text and result payloads are deliberately discarded
/// before the managed sink sees the entry.
/// </summary>
public static class ProductionLogEntryNormalizer
{
    private const string FallbackCategory = "production";
    private const string FallbackComponent = "production";
    private const string FallbackOperation = "event";
    private const string FallbackCorrelationId = "production";

    private static readonly IReadOnlyDictionary<string, string> CategoryAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mcp"] = "mcp",
            ["server"] = "mcp",
            ["mcp-server"] = "mcp",
            ["router"] = "router",
            ["mcp-router"] = "router",
            ["pythonbridge"] = "pythonbridge",
            ["python-bridge"] = "pythonbridge",
            ["selftest"] = "selftest",
            ["self-test"] = "selftest",
            ["diagnostic-export"] = "diagnostic-export",
            ["production"] = "production",
            ["General"] = "General",
            ["Map"] = "Map",
            ["Layer"] = "Layer",
            ["Attribute"] = "Attribute",
            ["Selection"] = "Selection",
            ["Analysis"] = "Analysis",
            ["Geoprocessing"] = "Geoprocessing",
            ["DataManagement"] = "DataManagement",
            ["Raster"] = "Raster",
            ["Layout"] = "Layout",
            ["Project"] = "Project",
            ["System"] = "System",
            ["Python"] = "Python"
        };

    private static readonly IReadOnlyDictionary<string, string> ComponentAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mcp"] = "mcp-server",
            ["server"] = "mcp-server",
            ["mcp-server"] = "mcp-server",
            ["router"] = "mcp-router",
            ["mcp-router"] = "mcp-router",
            ["pythonbridge"] = "python-bridge",
            ["python-bridge"] = "python-bridge",
            ["selftest"] = "self-test",
            ["self-test"] = "self-test",
            ["diagnostic-export"] = "diagnostic-export",
            ["verification"] = "verification",
            ["compatibility"] = "compatibility",
            ["core"] = "core",
            ["production"] = "production"
        };

    private static readonly IReadOnlyDictionary<string, string> OperationAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["event"] = "event",
            ["request"] = "request",
            ["handle-request"] = "handle-request",
            ["execute-tool"] = "execute-tool",
            ["tool-check"] = "tool-check",
            ["run"] = "run",
            ["lifecycle"] = "lifecycle",
            ["verification"] = "verification",
            ["export"] = "export",
            ["start"] = "start",
            ["stop"] = "stop",
            ["error"] = "error"
        };

    private static readonly HashSet<string> AllowedClients = new(StringComparer.Ordinal)
    {
        "codex",
        "cursor",
        "deepseek-harness",
        "claude-desktop"
    };

    private static readonly HashSet<string> AllowedToolsAndMethods = new(StringComparer.Ordinal)
    {
        "initialize",
        "notifications/initialized",
        "tools/list",
        "tools/call",
        "ping",
        "get_current_map",
        "list_maps",
        "get_map_info",
        "get_layers",
        "get_layer_info",
        "set_layer_visibility",
        "add_layer",
        "remove_layer",
        "get_project_info",
        "list_layouts",
        "list_databases",
        "query_attributes",
        "get_field_info",
        "get_feature_count",
        "clear_selection",
        "select_layer",
        "get_arcgis_version",
        "get_license_info",
        "buffer",
        "clip",
        "intersect",
        "dissolve",
        "get_dataset_info",
        "get_raster_info",
        "python_bridge_ping",
        "python_runtime_info",
        "dataset_summary",
        "list_fields",
        "list_workspace_datasets"
    };

    private static readonly HashSet<string> AllowedErrorCodes = new(StringComparer.Ordinal)
    {
        "NONE",
        "INVALID_ARGUMENT",
        "NOT_FOUND",
        "LICENSE_REQUIRED",
        "PERMISSION_DENIED",
        "EXECUTION_FAILED",
        "TIMEOUT",
        "CANCELLED",
        "THREADING_ERROR",
        "INTERNAL_ERROR",
        "TOOL_NOT_FOUND",
        "NOT_IMPLEMENTED",
        "LAYER_NOT_FOUND",
        "MAP_NOT_FOUND",
        "DATASET_NOT_FOUND",
        "INVALID_STATE",
        "ARCGIS_ERROR",
        "GEOPROCESSING_ERROR",
        "OUTPUT_EXISTS",
        "PYTHON_BRIDGE_UNAVAILABLE",
        "PYTHON_TIMEOUT",
        "PYTHON_OUTPUT_LIMIT_EXCEEDED",
        "PYTHON_PROTOCOL_ERROR",
        "SELFTEST_TOOL_FAILED",
        "SELFTEST_RUN_FAILED",
        "SERVER_STATE_NOT_CHECKED",
        "COMPATIBILITY_NOT_CHECKED",
        "COMPATIBILITY_MISMATCH",
        "ARCGIS_HOST_CONTEXT_UNAVAILABLE",
        "ARCGIS_HOST_CONTEXT_NOT_CHECKED",
        "BRIDGE_STATE_UNKNOWN",
        "LOGGING_DEGRADED",
        "MANAGED_LOG_UNAVAILABLE",
        "MANAGED_LOG_RECORD_INVALID",
        "MANAGED_LOG_BOUNDS_EXCEEDED",
        "LOG_INIT_FAILED",
        "LOG_DISPOSED",
        "LOG_WRITE_FAILED",
        "LOG_RETENTION_REPARSE_POINT",
        "LOG_RETENTION_FAILED"
    };

    public static LogEntry Normalize(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var category = CanonicalCategory(entry.Category) ?? FallbackCategory;
        var component = CanonicalComponent(entry.Component)
            ?? ComponentForCategory(category)
            ?? FallbackComponent;
        var operation = CanonicalOperation(entry.Operation) ?? FallbackOperation;
        var level = entry.Level;

        return new LogEntry
        {
            Timestamp = entry.Timestamp == default ? DateTimeOffset.UtcNow : entry.Timestamp.ToUniversalTime(),
            Level = level,
            Category = category,
            RequestId = GeneratedIdentifier(entry.RequestId),
            CorrelationId = GeneratedIdentifier(entry.CorrelationId)
                ?? GeneratedIdentifier(entry.RequestId)
                ?? FallbackCorrelationId,
            Component = component,
            Operation = operation,
            ErrorCode = CanonicalErrorCode(entry.ErrorCode)
                ?? (level is LogLevel.Error or LogLevel.Critical ? "INTERNAL_ERROR" : "NONE"),
            Outcome = LogRedactionPolicy.IsAllowedOutcome(entry.Outcome)
                ? entry.Outcome!.Trim()
                : DefaultOutcome(level),
            Client = ExactAllowed(entry.Client, AllowedClients),
            Tool = ExactAllowed(entry.Tool, AllowedToolsAndMethods),
            MessageCode = LogRedactionPolicy.IsAllowedMessageCode(entry.MessageCode)
                ? entry.MessageCode!.Trim()
                : "PRODUCTION_EVENT",
            ExecutionTime = entry.ExecutionTime,
            DurationMs = entry.DurationMs,
            // These fields are never passed through the production sink.
            Message = null,
            Result = null,
            Error = null
        };
    }

    private static string DefaultOutcome(LogLevel level)
        => level switch
        {
            LogLevel.Warning => "DEGRADED",
            LogLevel.Error or LogLevel.Critical => "FAILURE",
            _ => "OK"
        };

    private static string? CanonicalCategory(string? value)
        => TryCanonical(value, CategoryAliases);

    private static string? CanonicalComponent(string? value)
        => TryCanonical(value, ComponentAliases);

    private static string? CanonicalOperation(string? value)
        => TryCanonical(value, OperationAliases);

    private static string? CanonicalErrorCode(string? value)
        => ExactAllowed(value, AllowedErrorCodes);

    private static string? ComponentForCategory(string category)
        => category switch
        {
            "mcp" => "mcp-server",
            "router" => "mcp-router",
            "pythonbridge" => "python-bridge",
            "selftest" => "self-test",
            "diagnostic-export" => "diagnostic-export",
            _ => null
        };

    private static string? TryCanonical(
        string? value,
        IReadOnlyDictionary<string, string> aliases)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return aliases.TryGetValue(value.Trim(), out var canonical) ? canonical : null;
    }

    private static string? ExactAllowed(string? value, IReadOnlySet<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return allowed.Contains(normalized) ? normalized : null;
    }

    private static string? GeneratedIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (Guid.TryParseExact(normalized, "D", out var dashedGuid)
            || Guid.TryParseExact(normalized, "N", out dashedGuid))
        {
            return dashedGuid.ToString("D");
        }

        if (normalized.Length <= 20
            && normalized.All(character => character is >= '0' and <= '9')
            && ulong.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric.ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }
}
