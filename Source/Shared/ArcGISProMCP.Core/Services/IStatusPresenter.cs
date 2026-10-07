using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Services;

/// <summary>纯、无副作用的健康快照展示器。</summary>
public interface IStatusPresenter
{
    StatusPresentation Present(HealthSnapshot snapshot);
}
