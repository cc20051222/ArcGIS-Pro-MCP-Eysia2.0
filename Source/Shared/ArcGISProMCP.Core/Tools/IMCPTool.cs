using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Core.Tools;

/// <summary>
/// MCP 工具抽象（统一 Contract）。每个工具包含 Name / Description / InputSchema / ExecuteAsync 及元数据。
/// 本阶段不实现网络通信，只建立工具抽象与执行契约。
/// </summary>
public interface IMCPTool
{
    string Name { get; }

    string Description { get; }

    IReadOnlyDictionary<string, object?> InputSchema { get; }

    Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context);

    /// <summary>
    /// 工具元数据（默认来自 Name/Description；可覆写以提供 Category/ExecutionType/DisplayName 等）。
    /// 默认实现不破坏既有工具。
    /// </summary>
    ToolMetadata Metadata =>
        new()
        {
            Name = Name,
            DisplayName = Name,
            Description = Description,
            Category = ArcGISProMCP.Core.Models.ToolCategories.General,
            Version = "1.0.0",
            ExecutionType = ArcGISProMCP.Core.Models.ExecutionTypes.Native,
            RequiresArcGIS = false,
            SupportsCancellation = true
        };
}
