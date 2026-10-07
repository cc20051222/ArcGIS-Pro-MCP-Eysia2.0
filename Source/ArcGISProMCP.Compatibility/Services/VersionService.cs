using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Versioning;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>ArcGIS Pro 版本服务实现（读取安装注册表）。</summary>
public sealed class VersionService : IArcGISVersionService
{
    public Task<OperationResult<ArcGISVersionInfo>> GetVersionAsync(CancellationToken ct = default)
    {
        // D-070（P-23）：TFM 运行态派生 —— 从运行程序集 TargetFrameworkAttribute 读取并映射
        // （net6.0-windows→"net6.0"；net8.0-windows→"net8.0"；映射表见 Core.Versioning.VersionIdentity）。
        var runtimeTfm = typeof(VersionService).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        var info = new ArcGISVersionInfo { TargetFramework = VersionIdentity.MapTargetFramework(runtimeTfm) };

        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\ESRI\ArcGISPro");
        if (key is not null)
        {
            var realVersion = key.GetValue("RealVersion")?.ToString();
            var buildNumber = key.GetValue("BuildNumber");

            info.ProductVersion = realVersion ?? string.Empty;
            var parts = (realVersion ?? string.Empty).Split('.');
            if (parts.Length > 0 && int.TryParse(parts[0], out var major))
            {
                info.Major = major;
            }

            if (parts.Length > 1 && int.TryParse(parts[1], out var minor))
            {
                info.Minor = minor;
            }

            if (buildNumber is not null && int.TryParse(buildNumber.ToString(), out var build))
            {
                info.Build = build;
            }
        }

        return Task.FromResult(OperationResult<ArcGISVersionInfo>.Ok(info));
    }
}
