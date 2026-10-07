using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace ArcGISProMCP.Compatibility;

/// <summary>
/// Ribbon "Run Tests" 按钮：通过 MCPToolRouter 执行内部集成自测并写入
/// managed structured sink；不显示路径、异常文本或工具结果数据。
/// </summary>
internal class RunTestsButton : ArcGIS.Desktop.Framework.Contracts.Button
{
    protected override async void OnClick()
    {
        try
        {
            var summary = await SelfTestRunner.RunAllAsync();
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(summary);
        }
        catch
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Self-test could not be completed.");
        }
    }
}
