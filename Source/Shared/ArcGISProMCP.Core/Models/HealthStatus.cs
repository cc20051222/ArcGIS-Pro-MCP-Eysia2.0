using System.Text.Json.Serialization;

namespace ArcGISProMCP.Core.Models;

/// <summary>
/// 用户可见的整体健康状态。状态名称是稳定契约，新增值需要单独阶段授权。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HealthStatus
{
    Running,
    Stopped,
    Unavailable,
    Incompatible,
    BridgeFailure,
    MalformedConfiguration
}

/// <summary>组件级状态。组件可以比整体状态提供更细的事实分类。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HealthComponentStatus
{
    Pass,
    DeclaredOnly,
    Running,
    Stopped,
    Failure,
    Unavailable,
    Incompatible,
    MalformedConfiguration,
    Unknown,
    NotChecked,
    NotTracked,
    Degraded
}

/// <summary>HTTP transport 的显式事实；Unknown 不得被推断成 Stopped。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransportHealthStatus
{
    Running,
    Stopped,
    Unknown
}
