# 贡献方式

## 开发环境

- 平台：Windows x64（ArcGIS Pro 插件形态，无法在 Linux/macOS 直接运行宿主内代码）。
- .NET SDK：`net6.0-windows` 为主构建目标；`net8.0-windows` 代际需同时安装 .NET 8 SDK 与 ArcGIS Pro 3.5 SDK 引用集。
- ArcGIS Pro SDK 引用集**不随本仓库分发**（Esri EULA 限制）。本地需自行安装 ArcGIS Pro 及其 SDK，
  或将引用集放在仓库根的 `sdk-refs/` —— 该目录已被 `.gitignore` 完全排除，任何情况下不得提交。

## 构建与测试

按 `AGENTS.md` §8 的顺序执行（该节同时给出本机构建前必须补齐的环境变量清单）：

```bat
dotnet build-server shutdown
dotnet restore ArcGIS-Pro-MCP.sln
dotnet build   ArcGIS-Pro-MCP.sln -c Debug --no-restore
dotnet test    Tests/UnitTests/ArcGISProMCP.UnitTests.csproj --no-build --no-restore
dotnet test    Tests/IntegrationTests/ArcGISProMCP.IntegrationTests.csproj --no-build --no-restore
dotnet test    Tests/ArcGISProMCP.ServerTests/ArcGISProMCP.ServerTests.csproj --no-build --no-restore
```

期望结果为 0 警告 / 0 错误。`Compatibility` 项目固定 `Platforms=x64`；单元测试工程会从
`Source/ArcGISProMCP.Compatibility/bin` 加载真实程序集，因此必须先构建该插件工程。
禁止 `dotnet clean`。

## 打包

- `scripts/build-one-click-package.ps1`：生成一键部署包（双代 payload）。
- `scripts/package-addin.ps1`：生成 `.esriAddInX` 插件包并做身份校验。
- `scripts/verify-one-click-package.ps1`：对成品包做清单/哈希/身份复验。
- 制包批的完成门包含"真实兼容性路径验证"，细节见 `Docs/` 内演进文档。

## 架构硬规则（不接受例外）

1. 分层：`AI Client -> MCP Transport -> MCP Protocol -> MCP Server -> Tool Router -> Tool Registry -> GIS Tool -> IArcGISHost -> ArcGIS Services -> ArcGIS Pro SDK`。
2. ArcGIS SDK 对象的任何访问必须在 `QueuedTask.Run` 内；禁止从 HTTP/后台线程直连。
3. `Source/Shared` 不得引用 ArcGIS Pro SDK；SDK 引用只允许出现在 `ArcGISProMCP.Compatibility`。
4. 工具只在 `Composition.BuildRegistry()` 注册，且每件恰好一次；禁止第二处注册。
5. 端口/超时/路径/开关等配置集中在 `ArcGISProMCP.Configuration`。
6. 服务只监听 `127.0.0.1:6520/mcp`（Python Bridge `6511`）。禁止 `0.0.0.0`、禁止公网暴露、
   禁止新增任意 Python 执行旁路。

## Pull Request 约定

- 说明动机与影响面；涉及工具契约变更时同时更新契约快照与相关文档。
- 附测试证据：单元/集成/服务器测试的通过计数与失败名集合（环境敏感失败需逐名说明，不得为凑绿放宽断言）。
- 不得提交：`sdk-refs/`、`.runtime/`、`Release/*.zip`、`Archive/`、任何运行期产物、任何本机绝对路径或身份信息。
