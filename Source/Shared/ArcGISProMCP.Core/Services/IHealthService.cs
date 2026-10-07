using ArcGISProMCP.Core.Models;

namespace ArcGISProMCP.Core.Services;

/// <summary>
/// 纯健康快照组合接口。实现只能消费注入事实，不能读取 ArcGIS、注册表、文件或进程。
/// </summary>
public interface IHealthService
{
    HealthSnapshot CreateSnapshot(HealthFacts facts, DateTimeOffset generatedAtUtc);
}
