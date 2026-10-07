using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGISProMCP.Configuration;

namespace ArcGISProMCP.Compatibility;

/// <summary>
/// Add-in 模块入口（由 Config.daml 的 insertModule 声明）。
/// 工程打开后自动运行集成自测；模块卸载/Pro 退出时自动停止 MCP Server。
/// </summary>
internal class Module1 : Module
{
    protected override bool Initialize()
    {
        ProjectOpenedAsyncEvent.Subscribe(OnProjectOpenedAsync);
        return true;
    }

    private static async Task OnProjectOpenedAsync(ProjectEventArgs args)
    {
        _ = SelfTestRunner.RunAllAsync();

        // The production path remains the existing Start ribbon button. This
        // opt-in branch exists only to make a headless, controlled runtime
        // acceptance probe deterministic; the environment variable is never
        // set by the normal Add-in configuration.
        if (!string.Equals(
                Environment.GetEnvironmentVariable(
                    MCPSettings.VerificationAutoStartServerEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await Composition.Server.StartAsync().ConfigureAwait(false);
            Composition.ServerLogger.Info(
                "MCP Server auto-started for verification.",
                category: "verification");
        }
        catch (Exception ex)
        {
            Composition.ServerLogger.Error(
                "MCP Server verification auto-start failed.",
                category: "verification",
                exception: ex);
        }
    }

    protected override bool CanUnload()
    {
        _ = Composition.Server.StopAsync();
        _ = Composition.DisposePythonBridgeAsync();
        return true;
    }
}
