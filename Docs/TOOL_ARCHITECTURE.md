# 工具架构（TOOL_ARCHITECTURE）

> Phase 2 建立 Tool 抽象、Registry、Router。尚未实现网络通信（Phase 3）。

## 1. 调用链
```
MCP Server (Phase 3+) → MCPToolRouter → MCPToolRegistry → IMCPTool
                                                    ↓
                                               ToolExecutionContext (Host/Settings/Logger/Cancellation/Args)
                                                    ↓
                                               IArcGISHost → Services → ArcGIS Pro SDK
```

## 2. IMCPTool（Shared/Tools）
```csharp
public interface IMCPTool {
    string Name { get; }
    string Description { get; }
    IReadOnlyDictionary<string, object?> InputSchema { get; }
    Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context);
}
```

## 3. ToolExecutionContext
`RequestId`, `CancellationToken`, `Logger`, `Host (IArcGISHost?)`, `Settings (MCPSettings)`, `Arguments`。

## 4. MCPToolRegistry
- `Register` / `Unregister` / `Get` / `List` / `Contains` / `Count`
- 名称唯一；重复注册抛 `InvalidOperationException`；列表按名称稳定排序（`SortedDictionary`）；线程安全。

## 5. MCPToolRouter（只调度，不实现 GIS 业务）
- 接收 `MCPToolCall` → 查 Registry（不存在 → TOOL_NOT_FOUND）→ 校验必需参数（→ INVALID_ARGUMENT）
- → 构造 ToolExecutionContext → 调用 Tool → try/catch → 统一 `OperationResult<object?>`
- 记录 ExecutionTime / RequestId；异常与取消分别映射为 INTERNAL_ERROR / CANCELLED。

## 6. 内置测试工具（ArcGISProMCP.Tools）
`ping`, `GetCurrentMap`, `GetLayers`, `GetProjectInfo`, `GetArcGISVersion`, `GetLicenseInfo`
（不是 112 工具的一部分，用于验证 Router+Registry+Host 调用链）。

## 7. 依赖注入（轻量 ServiceContainer）
`ArcGISProMCP.Core.Container.ServiceContainer`：`Register<T>` / `RegisterInstance<T>` / `Resolve<T>` / `TryResolve<T>`。
Compatibility 的 `Composition` 为组合根：装配 Services → IArcGISHost → Tools → Registry/Router。
不引入 Microsoft.Extensions.DependencyInjection（避免额外依赖）。

## 8. 配置
`MCPSettings`（Shared/Configuration）：Host=127.0.0.1、Port=6520、Endpoint=/mcp、PythonBridgePort=6511、
LogLevel、RequestTimeoutMs、MaxConcurrentRequests。本阶段不启动服务器。
