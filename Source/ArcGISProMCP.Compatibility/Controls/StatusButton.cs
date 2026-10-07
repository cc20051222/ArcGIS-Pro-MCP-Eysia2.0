using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility;

/// <summary>
/// Ribbon "Status" 按钮：显示 MCP Server 状态与 ArcGIS Pro Host 状态。
/// </summary>
internal class StatusButton : ArcGIS.Desktop.Framework.Contracts.Button
{
    protected override void OnClick()
    {
        try
        {
            var provider = Composition.Container.Resolve<IHealthFactsProvider>();
            var healthService = Composition.Container.Resolve<IHealthService>();
            var presenter = Composition.Container.Resolve<IStatusPresenter>();
            var snapshot = healthService.CreateSnapshot(provider.GetFacts(), DateTimeOffset.UtcNow);
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(presenter.Present(snapshot).ToDisplayText());
        }
        catch
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(StatusPresenter.UnavailableFallback().ToDisplayText());
        }
    }
}
