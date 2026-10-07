using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace ArcGISProMCP.Compatibility;

/// <summary>
/// Ribbon "Start" 按钮：启动 MCP Server（重复点击不会启动第二个）。
/// </summary>
internal class StartButton : ArcGIS.Desktop.Framework.Contracts.Button
{
    protected override async void OnClick()
    {
        try
        {
            var server = Composition.Server;
            if (server.IsRunning)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("MCP Server 已在运行：" + Composition.Endpoint);
                return;
            }

            await server.StartAsync();
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("MCP Server 已启动：" + Composition.Endpoint);
        }
        catch (Exception ex)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("MCP Server 启动失败：" + ex.Message);
        }
    }
}
