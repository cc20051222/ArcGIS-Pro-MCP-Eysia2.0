using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// Pure validator for in-memory MCP server settings only.
/// It never reads client configuration or performs a listener probe.
/// </summary>
public static class RuntimeConfigurationValidator
{
    public static HealthComponent Validate(MCPSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!IsLoopback(settings.Host))
        {
            return Invalid("CONFIG_HOST_NOT_LOOPBACK");
        }

        if (settings.Port != MCPSettings.DefaultPort)
        {
            return Invalid("CONFIG_PORT_NOT_CANONICAL");
        }

        if (!string.Equals(settings.Endpoint, MCPSettings.DefaultEndpoint, StringComparison.Ordinal))
        {
            return Invalid("CONFIG_ENDPOINT_NOT_CANONICAL");
        }

        return new HealthComponent(
            "configuration",
            HealthComponentStatus.Pass,
            summary: "Managed server settings match the loopback, port 6520 and /mcp contract.");
    }

    public static bool IsLoopback(string? host)
        => string.Equals(host, MCPSettings.DefaultHost, StringComparison.Ordinal)
           || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);

    private static HealthComponent Invalid(string errorCode)
        => new(
            "configuration",
            HealthComponentStatus.MalformedConfiguration,
            summary: "Managed server settings do not match the canonical MCP contract.",
            errorCode: errorCode);
}
