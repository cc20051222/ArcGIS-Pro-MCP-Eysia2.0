using ArcGIS.Desktop.Framework.Contracts;
using ArcGISProMCP.Core.Progress;

namespace ArcGISProMCP.Compatibility;

internal sealed class ProgressPanelButton : ArcGIS.Desktop.Framework.Contracts.Button
{
    private static Controls.ProgressPanelWindow? _window;
    protected override void OnClick()
    {
        if (_window is not null) { _window.Activate(); return; }
        var hub = ToolProgress.DefaultHub;
        _window = new Controls.ProgressPanelWindow(hub.Snapshot, () => hub.JournalError, Composition.Settings.ProgressTaskRecord);
        // Native panel follows the Pro window lifetime instead of becoming an unowned app window.
        if (System.Windows.Application.Current?.MainWindow is { } owner)
            _window.Owner = owner;
        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }
}
