# DECISION-002-architecture-layering-and-packaging

## Context
需同时满足：ArcGIS Pro SDK 仅能进入 Host 实现层；MCP Server 不在 `dotnet build` 下可用官方 CodeTaskFactory 打包；工具名需符合 MCP 规范。

## Decision
1. **严格分层**：`AI → MCP Transport → MCP Protocol → MCP Server → Tool Router → Tool Registry → GIS Tool → IArcGISHost → ArcGIS Services → SDK`；Shared 层零 ArcGIS SDK；MCP Server 不直接操作 ArcGIS SDK，Tool 不绕过 Host。
2. **打包**：官方 `Esri.ProApp.SDK.Desktop.targets` 的 CodeTaskFactory 在 `dotnet build`（.NET Core MSBuild）下不支持（MSB4801），且本机 VS MSBuild 缺 .NET SDK 解析器；故用 `scripts/package-addin.ps1` 复刻官方布局生成 `.esriAddInX`，并用官方 `RegisterAddIn.exe` 注册。
3. **工具命名**：Tool 名使用 snake_case（ping / get_current_map / …），InputSchema 使用 JSON Schema 结构（type/properties/required）。
4. **配置集中**：端口/超时/路径统一进入 `ArcGISProMCP.Configuration`（MCPSettings），不散落代码。

## Reason
遵守架构分层与 Shared 隔离（Rule 5/6）；在既有环境限制下保证打包可复现；符合 MCP 规范；便于未来多客户端接入。

## Alternatives
- 机器级安装全部构建链（否决：需提权/安装，违背环境规则）。
- 用包名保持 PascalCase（否决：不符合 MCP 工具命名）。

## Consequences
- 新增 GIS Tool 必须按 snake_case + JSON Schema 注册到唯一 Registry。
- 打包脚本为打包唯一边界；改动需验证。
- 需新立项并推翻本决策的部分必须新建 Decision。

## Date
2026-08-31

## Phase
Phase 1–3
