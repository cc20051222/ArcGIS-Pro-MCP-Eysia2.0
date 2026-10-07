using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace ArcGISProMCP.Compatibility;

/// <summary>
/// Ribbon "Stop" 按钮：停止 MCP Server。
/// </summary>
internal class StopButton : ArcGIS.Desktop.Framework.Contracts.Button
{
    protected override async void OnClick()
    {
        try
        {
            var server = Composition.Server;
            if (!server.IsRunning)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("MCP Server 已停止。");
                return;
            }

            await server.StopAsync();
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("MCP Server 已停止。");
        }
        catch (Exception ex)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("MCP Server 停止失败：" + ex.Message);
        }
    }
}
