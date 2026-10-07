using System.IO;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGISProMCP.Core.Services;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using FormsFolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace ArcGISProMCP.Compatibility;

/// <summary>
/// Thin explicit export action. The user chooses a parent folder; the service
/// creates one new child directory and publishes only its fixed artifact set.
/// </summary>
internal sealed class DiagnosticsButton : ArcGIS.Desktop.Framework.Contracts.Button
{
    protected override void OnClick()
    {
        try
        {
            using var dialog = new FormsFolderBrowserDialog
            {
                Description = "Select a parent folder for a new diagnostic export.",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };
            if (dialog.ShowDialog() != FormsDialogResult.OK)
            {
                return;
            }

            var destination = Path.Combine(
                dialog.SelectedPath,
                "ArcGISProMCP-Diagnostics-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
            var healthService = Composition.Container.Resolve<IHealthService>();
            var provider = Composition.Container.Resolve<IHealthFactsProvider>();
            var snapshot = healthService.CreateSnapshot(provider.GetFacts(), DateTimeOffset.UtcNow);
            var result = Composition.Container.Resolve<IDiagnosticExportService>().Export(destination, snapshot);
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(result.UserMessage);
        }
        catch
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Diagnostic export could not be created.");
        }
    }
}
