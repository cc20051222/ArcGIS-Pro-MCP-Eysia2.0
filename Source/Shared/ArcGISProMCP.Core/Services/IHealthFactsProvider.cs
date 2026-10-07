using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// Read-only runtime facts boundary. Implementations may read already-owned
/// in-memory state, but must not probe files, processes, ports or clients.
/// </summary>
public interface IHealthFactsProvider
{
    HealthFacts GetFacts();
}
