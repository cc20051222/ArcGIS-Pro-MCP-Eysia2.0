using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// GIS 工具基类：提供默认 Metadata（使用 Name/Description/Category），减少重复。
/// </summary>
public abstract class McpToolBase : IMCPTool
{
    public abstract string Name { get; }

    public abstract string Description { get; }

    public abstract IReadOnlyDictionary<string, object?> InputSchema { get; }

    public abstract Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context);

    /// <summary>工具分类，见 ToolCategories。子类覆写。</summary>
    protected abstract string CategoryName { get; }

    /// <summary>执行类型，默认 Native；GP 工具覆写为 ExecutionTypes.Geoprocessing。</summary>
    protected virtual string ExecutionTypeName => ExecutionTypes.Native;

    /// <summary>是否需要 ArcGIS Host；默认 true（既有 GIS 工具不变）。非 GIS 工具可覆写 false。</summary>
    protected virtual bool? RequiresArcGISOverride => null;

    public virtual ToolMetadata Metadata => new()
    {
        Name = Name,
        DisplayName = DisplayNameOverride ?? Name,
        Description = Description,
        Category = CategoryName,
        Version = "1.0.0",
        ExecutionType = ExecutionTypeName,
        RequiresArcGIS = RequiresArcGISOverride ?? true,
        SupportsCancellation = true
    };

    /// <summary>可选中文/显示名覆写。</summary>
    protected virtual string? DisplayNameOverride => null;
}
