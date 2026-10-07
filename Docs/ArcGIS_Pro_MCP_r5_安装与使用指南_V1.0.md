# ArcGIS Pro MCP r5 安装与使用指南

面向普通 GIS 用户的 Codex 优先指南

文档版本：V1.0  
适用包：`one-click-1.0.2-r5`  
当前使用边界：按前置条件进行用户实机试用；真实用户电脑安装、真实 AI 客户端连接和 clean-machine 验收仍为 `NOT VERIFIED`。  
发布日期：2026-09-10

> 本指南是用户操作说明，不是新的 Phase 授权。包内插件仍是已接受的 ArcGIS Pro MCP 1.0.2；r5 是围绕该插件的 Windows x64 一键部署与恢复分享包。

## 目录导航

本文档按下面顺序组织。使用 Word 版时可点击目录标题跳转；安装前请先读“先看结论”和“前置条件”。

1. [先看结论](#先看结论)
2. [产品用途与架构](#产品用途与架构)
3. [包身份与前置条件](#包身份与前置条件)
4. [下载校验与完整解压](#下载校验与完整解压)
5. [运行一键部署](#运行一键部署)
6. [启动 ArcGIS Pro 并完成 Codex 首次验收](#启动-arcgis-pro-并完成-codex-首次验收)
7. [日常启动与关闭](#日常启动与关闭)
8. [30 个生产工具](#30-个生产工具)
9. [安全提问方式与典型 buffer clip 流程](#安全提问方式与典型-buffer-clip-流程)
10. [Cursor 与 DeepSeek Harness](#cursor-与-deepseek-harness)
11. [错误、取消、重试、恢复与卸载](#错误取消重试恢复与卸载)
12. [1.0.2 与 r5 的关系](#102-与-r5-的关系)
13. [新电脑验收清单](#新电脑验收清单)
14. [反馈模板](#反馈模板)
15. [实际行为核对表与来源](#实际行为核对表与来源)

## 先看结论

r5 的用途是把已经构建好的 ArcGIS Pro MCP 1.0.2 插件，以一个可校验、可恢复、可选择单个客户端的 Windows x64 分享包交给用户。典型使用链路是：

```text
校验 ZIP → 完整解压 → ONE-CLICK-SETUP.cmd
       → 选择一个客户端和项目目录 → 确认目标
       → 只读预检 → 插件事务 → 单客户端事务 → 连接诊断
       → 用户打开 ArcGIS Pro → MCP → Start
       → 在同一项目中的 Codex 先做 ping 和只读工具检查
```

必须区分四种结果：

| 结果 | 能证明什么 | 不能证明什么 |
|---|---|---|
| 已安装 | Add-in 安装事务及精确安装目标 | ArcGIS Pro 已启动、MCP 已监听或 AI 客户端已连接 |
| 已配置 | 选定客户端的配置事务完成 | 客户端新会话已实际发现并调用工具 |
| HTTP `tools/list=30` | `127.0.0.1:6520/mcp` 返回精确的 canonical 30 工具集 | Codex、Cursor 或 DeepSeek 自身连接 PASS |
| HTTP `ping=pong` | loopback MCP 的只读 `ping` 调用成功 | 其他工具、数据写入、真实项目验收全部成功 |

r5 当前仅按前置条件进行用户实机试用，不等于全新电脑、真实客户端或公开发布的整体 PASS。没有用户电脑上的新会话证据时，客户端连接必须写成 `NOT VERIFIED`。

## 产品用途与架构

### 解决什么问题

普通 GIS 用户不需要构建源代码或手工拼接多个客户端配置，就可以：

- 校验发布 ZIP 和内部插件载荷；
- 选择 Codex、Cursor、DeepSeek Harness 中的一个，或选择“仅安装插件”；
- 让部署器显示插件目录、Add-in GUID 和客户端配置目标；
- 使用独立事务、ledger、backup metadata 和 recovery index 处理失败、取消、回滚与卸载；
- 在 ArcGIS Pro 由用户点击 `MCP → Start` 后，对本机 loopback MCP 做工具发现和只读 `ping` 诊断。

### 组件关系

| 组件 | 位置或接口 | 作用 |
|---|---|---|
| 分享包 | `ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip` | 携带 launcher、脚本、manifest、客户端模板和 1.0.2 Add-in |
| 一键部署 UI | `ONE-CLICK-SETUP.cmd` → `scripts\\one-click-setup.ps1` | 选择范围、显示目标、执行预检和有边界的事务 |
| ArcGIS Pro Add-in | 当前用户 Documents 下 `ArcGIS\\AddIns\\ArcGISPro\\{BAA5628C-3C08-4AD5-A6C0-915ADA475709}` | 在 ArcGIS Pro 内承载 MCP 服务 |
| MCP 服务 | `http://127.0.0.1:6520/mcp` | 提供 streamable HTTP MCP endpoint |
| AI 客户端 | Codex / Cursor / DeepSeek Harness | 从各自配置读取 endpoint，发送工具发现和调用请求 |
| 状态与恢复 | `%LOCALAPPDATA%\\ArcGISProMCP\\installer` | 保存 one-click state、operation log、recovery index 和事务 ledger |

安装器不会启动或强制关闭 ArcGIS Pro、Codex、Cursor、DeepSeek 或其他用户程序；也不会替用户点击 ArcGIS Pro 的 `Start`。

### 为什么包不包含 ArcGIS Pro、.NET 和 AI 客户端

- ArcGIS Pro 是需用户自行安装和授权的桌面软件，且必须在支持的 3.5 系列环境中运行；把它打进分享包既不可行，也会绕过许可与系统安装管理。
- .NET 8 runtime 是系统运行时依赖，不是本包的安装内容；包只检查前置条件，不联网替用户安装依赖。
- Codex、Cursor、DeepSeek Harness 和 Claude Desktop 各自有安装、登录、版本和用户配置边界；包只写入明确选择的受支持配置目标，不携带账号、token、password、secret、provider 或 model。
- 分享包明确不含 APRX、GDB、runtime 历史目录、客户端实际配置、备份或锁文件。

## 包身份与前置条件

### 当前唯一试用包

| 项目 | 值 |
|---|---|
| 文件 | `Release\\ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip` |
| ZIP 大小 | `331679` bytes |
| ZIP SHA-256 | `3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652` |
| 部署版本 | `one-click-1.0.2-r5` |
| 内嵌插件 | `ArcGISProMCP.Compatibility.esriAddInX` |
| 内嵌插件大小 | `269548` bytes |
| 内嵌插件 SHA-256 | `361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A` |
| Add-in GUID | `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}` |
| endpoint | `http://127.0.0.1:6520/mcp` |
| canonical production tools | `30` |
| `mcp_auth` | 客户端辅助项，不计入生产工具 |

不要把 r4、r3、r2 或 r1 的历史 ZIP/hash 与本次 r5 混用。遇到同名文件，先看文件名、`.sha256`、`bundle-manifest.json` 和验证报告。

### 电脑条件

开始部署前准备并记录：

- Windows x64；
- ArcGIS Pro 3.5（策略要求 Desktop 3.5.0 系列）；
- Windows PowerShell 5.1（Windows 通常自带）；
- .NET 8 runtime；
- ArcGIS Pro 的 `arcgispro-py3` 环境和 ArcPy；兼容性检查按 Python 3.11 / ArcPy 3.5 识别；
- 若选择 Codex 或 Cursor：已经安装对应客户端，并知道要使用的项目目录；
- 部署时关闭 ArcGIS Pro；选择 Cursor 时也关闭 Cursor。安装器不会强制关闭这些程序。

不需要 Git、Visual Studio 或 .NET SDK。版本不符合时，不要用修改 endpoint、复制 DLL、删除配置或删除锁文件的方式绕过预检。

## 下载校验与完整解压

> **重要提示：包内旧指南与本指南的关系**：固定 r5 ZIP 内的 `Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md` 仍是 r4 遗留文件，含有过期的 r4 版本号、大小/hash 和待复核叙述。本文件是随仓库提供的独立 r5 配套指南，**当前没有包含在固定 r5 ZIP 内**；请以本指南、ZIP 的 `bundle-manifest.json`、验证脚本输出和仓库权威 r5 Gate 记录为准，不要使用包内旧指南中的 r4 hash 验证 r5。

### 1. 先校验 ZIP

在 PowerShell 中进入 ZIP 所在目录，运行：

```powershell
Get-FileHash .\\ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip -Algorithm SHA256
```

输出的 `Hash` 必须等于：

```text
3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652
```

也可以使用命令提示符：

```cmd
CertUtil -hashfile "ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip" SHA256
```

不一致就停止，不要运行其中的脚本。

### 2. 完整解压

用资源管理器或可信压缩工具把整个 ZIP 解压到一个普通目录，例如：

```text
D:\GIS\ArcGIS-Pro-MCP-OneClick-1.0.2-r5
```

不要直接在 ZIP 内双击 launcher。解压后根目录应能看到 `ONE-CLICK-SETUP.cmd`、`bundle-manifest.json`、`scripts`、`Config`、`payload` 和 `README-START-HERE.md`。

### 3. 运行包内只读验证

在解压根目录打开 PowerShell，运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\verify-one-click-package.ps1 -RootPath . -Json
```

期待看到：

- `status: PASS`；
- `deploymentVersion: one-click-1.0.2-r5`；
- `fileCount: 23`；
- `payloadSizeBytes: 269548`；
- 内嵌载荷 hash 为上表中的 64 位值；
- `canonicalProductionToolCount: 30`；
- `cleanMachineAcceptance: NOT VERIFIED`。

该验证不会启动 ArcGIS Pro，不安装插件，不写客户端配置，也不读取 GIS 数据。验证失败时保存窗口或 JSON 输出，联系项目维护者。

## 运行一键部署

### 1. 启动窗口

双击解压根目录的 `ONE-CLICK-SETUP.cmd`。Windows PowerShell 5.1 以 STA 模式打开部署窗口。

首次建议先点击“只做预检”，确认环境、包、目标和客户端配置均通过，再决定是否开始写入。

### 2. 选择一次范围

窗口的客户端选项是单选：

| 选项 | 选择后行为 | 需要的目录 |
|---|---|---|
| Codex（P0，推荐） | 安装插件，并写入 Codex 项目级配置 | 现有 Codex 项目目录 |
| Cursor（P1） | 安装插件，并写入 Cursor 项目级配置 | 现有 Cursor 项目目录 |
| DeepSeek Harness（P1） | 安装插件，并写入当前用户的 DeepSeek patch | 不需要项目目录，使用当前用户配置位置 |
| 仅安装插件 | 只安装 Add-in，不写 AI 客户端 | 不需要 |

一次一键部署只能明确选择一个客户端（或“仅安装插件”）。当前生产流程对已存在的可恢复/已安装事务拒绝再次部署，通常返回 `EXISTING_DEPLOYMENT_REQUIRES_RECOVERY`；因此，第一次部署完成后，不要为了增加第二个客户端再次运行一键部署器，也不要把卸载正常插件当作普通的“加客户端”步骤。一次部署内选择多个客户端也会被拒绝，错误码为 `MULTIPLE_CLIENTS_NOT_ALLOWED`。

### 已完成一次部署后增加第二个客户端

增加第二个客户端应使用 r5 包内的独立客户端配置工作流，不重新安装插件。以下命令均在**已完整解压的 r5 根目录**运行；先执行只读 `Validate`，核对目标路径、当前配置摘要和备份边界，再由人工明确确认后执行一次 `ClientApply`。`ClientApply` 命令本身不会弹出确认框，因此执行前必须确认目标确实是要配置的客户端，并保留命令输出中的 ledger JSON、目标配置及其 `.arcgis-pro-mcp.bak` / `.arcgis-pro-mcp.bak.json`（如已有配置）。不要手工覆盖、删除或绕过 stale-backup 检查。

Codex（将 `<Codex 项目目录>` 替换为实际项目根目录）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Validate -Client codex -CatalogPath .\Config\client-catalog.json -ConfigRoot "<Codex 项目目录>" -TemplateRoot . -Json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action ClientApply -Client codex -CatalogPath .\Config\client-catalog.json -ConfigRoot "<Codex 项目目录>" -TemplateRoot . -Json
```

Cursor（将 `<Cursor 项目目录>` 替换为实际项目根目录）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Validate -Client cursor -CatalogPath .\Config\client-catalog.json -ConfigRoot "<Cursor 项目目录>" -TemplateRoot . -Json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action ClientApply -Client cursor -CatalogPath .\Config\client-catalog.json -ConfigRoot "<Cursor 项目目录>" -TemplateRoot . -Json
```

DeepSeek Harness（使用当前用户配置位置；不要为此客户端传入 `-ConfigRoot`）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Validate -Client deepseek-harness -CatalogPath .\Config\client-catalog.json -TemplateRoot . -Json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action ClientApply -Client deepseek-harness -CatalogPath .\Config\client-catalog.json -TemplateRoot . -Json
```

这些命令只处理一个明确的客户端；它们不会证明新的 AI 客户端连接已经建立。Claude Desktop 在当前 r5 配置脚本中是 `validate-template-only`，`ClientApply` 不支持；如需该客户端，请先向维护者确认，不要自行改写模板或生产配置。

### 3. 选择 Codex 项目目录

对 Codex 或 Cursor，点击“选择目录…”并选择实际要使用的项目根目录。目录必须已经存在；不要选择桌面、整个磁盘、`MyProject1.aprx` 所在的保护项目目录，或 `Phase4Test.gdb` 所在的数据目录作为随意目标。

部署器显示的实际目标是：

```text
Codex   → <项目目录>\\.codex\\config.toml
Cursor  → <项目目录>\\.cursor\\mcp.json
DeepSeek → %USERPROFILE%\\.dsh\\profiles\\web\\cordis.patch.yml
```

Codex 的项目目录要与之后打开的 Codex project 完全一致。配置写到另一个目录，即使文件本身写入成功，之后也可能看不到 MCP 服务。

### 4. 核对目标

窗口的目标框至少应显示：

- 插件安装根目录；
- 精确插件目录和 Add-in GUID；
- 选定客户端的配置目标；
- “仅安装插件”时客户端显示为未选择。

如果路径不对，先取消或改选目录，不要确认部署。不要手工改写 GUID、endpoint 或配置模板。

### 5. 开始部署并只确认一次

点击“开始一键部署”。确认框会说明顺序：

```text
预检 → 包完整性校验 → 插件安装事务 → 单客户端配置事务 → 连接诊断
```

确认后，窗口可能出现以下状态：

- `PASS`：对应只读或恢复步骤通过；
- `WAITING_USER_START`：插件事务完成，但 ArcGIS Pro 尚未由用户启动 MCP 服务；
- `PASS_WITH_CLIENT_VERIFICATION_PENDING`：HTTP 诊断通过，但真实 AI 客户端自身证据仍未取得；
- `FAILED`：保留错误码、ledger 和恢复入口；
- `CANCELLED`：在安全边界停止，已完成事务不会被假装撤销。

### 6. 按提示启动 ArcGIS Pro

部署器不会自动打开 ArcGIS Pro。若窗口显示：

```text
等待用户启动：请打开 ArcGIS Pro，在 MCP 选项卡点击 Start。
```

请执行：

1. 打开 ArcGIS Pro；
2. 打开要工作的项目；
3. 在 `MCP` 选项卡点击 `Start`；
4. 回到部署窗口点击“连接重试（仅诊断）”。

“连接重试”只重复 loopback MCP diagnosis，不重新安装插件，不重新写客户端配置，不创建新 transaction。

## 启动 ArcGIS Pro 并完成 Codex 首次验收

下面是推荐的 Codex 首次验收顺序。它要求 Codex 打开的 project directory 与部署时选择的目录相同。

### 1. 先确认同一项目

在 Codex 中打开部署时选择的项目目录，检查项目级配置确实位于：

```text
<同一项目目录>\\.codex\\config.toml
```

如果 Codex 已经打开，关闭并重新打开该 project，让客户端重新读取项目配置。不要把“文件存在”当作“新会话已经连接”。

### 2. 先做工具发现和只读检查

可以向 Codex 发送类似下面的请求：

```text
请先只读检查 ArcGIS Pro MCP：调用 ping、get_arcgis_version、get_project_info。
先返回服务是否可达、ArcGIS Pro 版本和当前项目摘要；不要修改图层、选择集、地图、项目或任何数据。
```

最低首验建议记录：

| 检查 | 期望 | 证据 |
|---|---|---|
| 工具发现 | canonical production names 共 30 个，distinct 30，重复 0 | Codex 新会话工具列表或客户端可见工具记录 |
| `ping` | `pong` 且无 MCP error | Codex 工具调用结果 |
| `get_arcgis_version` | 返回当前 ArcGIS Pro 版本 | Codex 工具调用结果 |
| `get_project_info` | 成功返回当前项目摘要且调用无 MCP error | Codex 工具调用结果 |

如项目中没有可用 map，先不要把 map 相关错误当作 MCP 连接失败；保留原始错误并区分“服务连接”和“项目上下文”。

`get_project_info` 如果因当前项目上下文返回错误，可以作为上下文诊断记录，但不计为一次成功的只读工具验收。此时应改用另一个实际成功、无 MCP error 的非 `ping` 只读工具，并保留原始错误；不要把“返回了清晰错误”包装成工具 PASS。

### 3. 诊断状态如何判断

| 窗口/客户端结果 | 处理 |
|---|---|
| `tools/list=30` 且 `ping=pong` | loopback MCP 层通过；继续在同一 Codex 会话做只读工具检查 |
| 端口监听但 `tools/list` 无效 | 记录 `PORT_OPEN_NOT_MCP`，不要称为连接成功 |
| 工具数量不是 30 或名称不一致 | 记录 `TOOL_SET_MISMATCH`，不要按数量猜测 |
| 客户端显示未连接 | 记录 `NOT VERIFIED`，核对 project directory 后重启 Codex |
| 只显示插件已安装 | 仍需 ArcGIS Pro `Start` 和 MCP/客户端检查 |

GUI 截图中的长路径、中文路径和 `WAITING_USER_START` 只属于 r5 独立自动化的只读按钮回调证据，路径是测试环境示例，不是用户电脑安装结果。

## 日常启动与关闭

### 每日启动

1. 打开 ArcGIS Pro 和工作项目；
2. 在 MCP 选项卡点击 `Start`；
3. 打开同一项目目录中的 Codex；
4. 先调用 `ping`，再做只读检查；
5. 确认项目、图层和数据路径后，再考虑任何写入工具。

每天不需要重新运行 `ONE-CLICK-SETUP.cmd`。部署器是安装/恢复工具；ArcGIS Pro MCP 服务由 ArcGIS Pro 内 Add-in 的 `Start` 控制。

### 关闭

1. 先停止正在等待或运行的 AI 工具调用；
2. 按当前 Add-in UI 的可用按钮停止 MCP 服务；
3. 关闭 Codex/Cursor/DeepSeek 会话；
4. 保存并退出 ArcGIS Pro；
5. 保留需要审计的日志和 ledger，不要删除未知 `.lock` 或 `.sr.lock`。

## 30 个生产工具

下面按 `Composition.BuildRegistry()` 的实际注册顺序分组。表中的“执行类型”和“是否需要 ArcGIS”来自生产工具契约快照；这 30 个名称才是 canonical production tool set。

### 基础与地图

| 工具 | 执行类型 | 只读/写入 | 用途 |
|---|---|---|---|
| `ping` | Native | 只读 | 检查 MCP 服务并返回 `pong` |
| `get_current_map` | Native | 只读 | 读取当前地图上下文 |
| `list_maps` | Native | 只读 | 列出当前项目地图；当前实现为 PARTIAL |
| `get_map_info` | Native | 只读 | 读取指定地图信息；当前实现为 PARTIAL |

### 图层

| 工具 | 执行类型 | 只读/写入 | 用途 |
|---|---|---|---|
| `get_layers` | Native | 只读 | 列出地图中的图层 |
| `get_layer_info` | Native | 只读 | 读取指定图层信息 |
| `set_layer_visibility` | Native | 写入地图状态 | 设置图层可见性；执行前确认地图和图层名 |
| `add_layer` | Native | 写入地图状态 | 向地图添加图层 |
| `remove_layer` | Native | 写入地图状态 | 从地图移除图层；执行前确认目标 |

### 工程、布局与属性

| 工具 | 执行类型 | 只读/写入 | 用途 |
|---|---|---|---|
| `get_project_info` | Native | 只读 | 读取当前 ArcGIS Pro 项目摘要 |
| `list_layouts` | Native | 只读 | 列出项目布局 |
| `list_databases` | Native | 只读 | 列出项目关联数据库 |
| `query_attributes` | Native | 只读 | 按查询条件读取属性结果 |
| `get_field_info` | Native | 只读 | 读取字段定义 |
| `get_feature_count` | Native | 只读 | 读取要素数量 |

### 选择、版本与许可

| 工具 | 执行类型 | 只读/写入 | 用途 |
|---|---|---|---|
| `clear_selection` | Native | 写入选择状态 | 清除选择集 |
| `select_layer` | Native | 写入选择状态 | 对指定地图/图层执行选择；当前 schema 只有 `mapName`、`layerName`，不提供确定性 OID 参数 |
| `get_arcgis_version` | Native | 只读 | 读取 ArcGIS Pro 版本 |
| `get_license_info` | Native | 只读 | 读取许可信息 |

### 地理处理

| 工具 | 执行类型 | 只读/写入 | 用途 |
|---|---|---|---|
| `buffer` | Geoprocessing | 写入输出 | 生成缓冲区；先确认距离、单位和不覆盖的输出路径 |
| `clip` | Geoprocessing | 写入输出 | 用裁剪范围生成输出；先确认输入、裁剪图层和输出路径 |
| `intersect` | Geoprocessing | 写入输出 | 生成相交结果；先确认多个输入和输出路径 |
| `dissolve` | Geoprocessing | 写入输出 | 融合要素；先确认融合字段、是否多部件和输出路径 |

### 数据管理与栅格

| 工具 | 执行类型 | 只读/写入 | 用途 |
|---|---|---|---|
| `get_dataset_info` | Native | 只读 | 读取数据集信息；当前为 LIMITED IMPLEMENTATION |
| `get_raster_info` | Native | 只读 | 读取栅格信息；当前为 LIMITED IMPLEMENTATION |

### Python Bridge 与 ArcPy Discovery

| 工具 | 执行类型 | 只读/写入 | 用途 |
|---|---|---|---|
| `python_bridge_ping` | Python | 只读 | 检查受约束 Python Bridge |
| `python_runtime_info` | Python | 只读 | 读取 Python runtime 信息 |
| `dataset_summary` | Python | 只读 | 读取指定数据集摘要 |
| `list_fields` | Python | 只读 | 读取数据集字段 |
| `list_workspace_datasets` | Python | 只读 | 列出工作空间数据集 |

`mcp_auth` 是客户端作用域的辅助项，不在 ArcGIS Pro MCP production registry 中，不计入 30。生产环境也没有任意 `python_execute`、`sleep_test` 或 `raise_test_exception`；不要把这类名称写进用户验收表。

### 已知工具限制

- `list_maps`、`get_map_info`：当前为 PARTIAL，不把有限返回当作完整地图验收；
- `get_dataset_info`、`get_raster_info`：当前为 LIMITED IMPLEMENTATION；
- `select_layer`：当前只接受 `mapName` 和 `layerName`，不能通过现有 schema 指定确定性 OID；因此不要宣称确定性选择 mutation PASS；
- `ArcGISProject.isDirty` 在 Pro 3.5 不可用是非阻断限制；
- HTTP client-disconnect cancellation 尚未验证；
- 7 个历史 HTTP transport tests 为 `BLOCKED_BY_HARNESS`；
- Python 工具是受约束的 Bridge/Discovery 工具，不等于开放任意 Python MCP。

## 安全提问方式与典型 buffer clip 流程

### 通用提问模板

普通 GIS 用户可以直接描述目标，但要把“只读阶段”和“写入阶段”分开：

```text
第一步只读：请列出当前地图和图层，读取目标图层的字段、要素数和数据路径。
不要修改地图、图层可见性、选择集或任何数据。若名称不唯一，请列出候选让我确认。
```

任何写入前使用明确的输出协议：

```text
在执行写入前，请先复述：输入数据、操作参数、坐标系/单位，以及唯一输出路径。
本次输出路径是 <我拥有的输出目录>\\P58_Buffer_500m.gdb\\Buffer_500m。
如果该路径已存在、无法确认，或落在 MyProject1.aprx / Phase4Test.gdb 保护范围内，请停止，不要覆盖。
```

这不是装饰性提示：`buffer`、`clip`、`intersect`、`dissolve` 和地图/选择状态工具都会改变状态或产生结果。执行前应让 AI 明确说出输出路径，并确认不是用户已有数据。

### Buffer 典型流程

1. 只读定位：`list_workspace_datasets`、`dataset_summary`、`list_fields`、`get_feature_count`；
2. 确认输入数据集、几何类型、坐标系和距离单位；
3. 由用户指定一个拥有的、尚不存在的输出数据集路径；
4. AI 在执行前复述输入、距离、单位、输出路径和不覆盖要求；
5. 调用 `buffer`；
6. 使用 `get_dataset_info`、`get_feature_count` 或 `dataset_summary` 做只读结果检查；
7. 如果要统计面积，注明是逐要素未融合结果的总和，还是 dissolve 后的面积，不能混为一谈。

示例请求：

```text
先只读检查 <我拥有的输入要素类> 的字段、要素数和坐标系。
如果信息明确，再告诉我将使用的距离和单位；在执行 buffer 前先宣布输出路径 <我拥有的输出要素类>，
确认路径不存在且不覆盖用户数据。执行后只读验证输出要素数和数据集摘要。
```

### Clip 典型流程

1. 只读确认输入图层和裁剪 mask 的数据路径；
2. 确认二者的几何类型和坐标系可用性；
3. 明确用户拥有的输出路径；
4. 执行前宣布“输入、裁剪 mask、输出路径”，存在歧义就停；
5. 调用 `clip`；
6. 只读验证输出存在、要素数和基本摘要。

示例请求：

```text
请先只读确认输入图层 <input> 和裁剪 mask <mask> 的真实路径，不要修改数据。
确认后，在执行 clip 前先宣布输出路径 <owned-output>；输出已存在或路径不明确时停止。
完成后只读检查输出要素数和字段。
```

禁止把 `Phase4Test.gdb` 当作输出位置，禁止修改 `MyProject1.aprx`，禁止让 AI 通过未公开的任意 Python 命令绕过生产工具边界。

## Cursor 与 DeepSeek Harness

### Cursor

- 在窗口选择 Cursor（P1），并选择实际的 Cursor 项目目录；
- 目标是 `<项目目录>\\.cursor\\mcp.json`；
- 部署前关闭 Cursor，避免用户编辑与配置事务竞争；
- 部署后重新打开同一项目，检查客户端自身工具发现、`ping` 和只读工具调用；
- Cursor 的“配置已写入”只证明配置事务，不自动证明新会话连接。

### DeepSeek Harness

- 在窗口选择 DeepSeek Harness（P1）；
- 它使用当前用户配置位置 `%USERPROFILE%\\.dsh\\profiles\\web\\cordis.patch.yml`，不使用 Codex/Cursor 项目目录；
- 先关闭会编辑该配置的相关会话，部署后重新打开新会话；
- 必须保留客户端自身工具列表和一次 `ping` 的新鲜证据；没有证据就记为 `NOT VERIFIED`。

### Claude Desktop

Claude Desktop 在当前客户端目录中是可选的 `validate-template-only` 项，不属于一键窗口的三个直接选择项。不要把模板文件存在或历史连接证据写成当前 r5 新客户端 PASS。

## 错误、取消、重试、恢复与卸载

### 常见错误

| 错误码 | 含义 | 用户动作 |
|---|---|---|
| `BUNDLE_*` | 包缺件、hash、manifest、路径或载荷不合格 | 重新获取同一 r5 ZIP，先校验 hash；保留原输出 |
| `COMPATIBILITY_NOT_PASS` | Windows、ArcGIS Pro、SDK、.NET 8 或 ArcPy 前置条件不通过 | 按提示修复环境后再预检 |
| `ARCGIS_PRO_RUNNING` | ArcGIS Pro 尚未关闭 | 用户自行关闭 ArcGIS Pro 后重试 |
| `INSTALL_ROOT_NOT_FOUND` / `INSTALL_ROOT_INVALID` | Add-in 目标根目录缺失或不是目录 | 先确认路径；明确部署时只允许安全创建预定根目录 |
| `PROJECT_ROOT_NOT_FOUND` | Codex/Cursor 项目目录不存在 | 重新选择一个已存在的项目目录 |
| `CLIENT_RUNNING` | Cursor 正在运行 | 关闭 Cursor 后重试 |
| `MULTIPLE_CLIENTS_NOT_ALLOWED` | 一次选择了多个客户端 | 每次只选一个，或选仅安装插件 |
| `WAITING_USER_START` | ArcGIS Pro 尚未点击 MCP `Start` | 打开 Pro，点击 `Start`，再用“连接重试（仅诊断）” |
| `PORT_OPEN_NOT_MCP` | 6520 可达但没有有效 MCP 响应 | 保留诊断输出，不把端口当作 MCP PASS |
| `TOOL_SET_MISMATCH` / `TOOL_DUPLICATE` | 工具集与 canonical 30 不一致 | 记录 names/count，停止写入验收并联系维护者 |
| `STALE_BACKUP_REFUSED` | 目标文件已被用户在上次事务后编辑 | 保存现状，人工审查；不要删除备份后强行恢复 |
| `EXISTING_DEPLOYMENT_REQUIRES_RECOVERY` | 已有未完成或可恢复事务 | 先使用恢复/卸载入口，不要重复点击部署 |

### 取消与重试

- “取消/停止等待”只请求在安全步骤边界停止，不强杀子进程或用户程序；
- 取消后已完成的插件事务仍保留，客户端事务可能尚未开始；
- `Retry` 只重新做 loopback diagnosis，不重新安装插件、不写客户端、不创建新事务；
- 如果状态要求 recovery，先处理 recovery index 和同一事务 ledger，再考虑重试；
- 不要手工删除 `latest-transaction.json`、`recovery-index.json`、transaction 目录、`.arcgis-pro-mcp.bak`、`.arcgis-pro-mcp.bak.json` 或未知锁文件。

### 恢复、回滚与卸载

| 入口 | 作用 | 注意 |
|---|---|---|
| “恢复最近安装” / `ROLLBACK-PLUGIN.cmd` | 按最近可恢复的同一 ledger 回滚插件事务 | 优先 recovery index，保留证据 |
| “恢复失败客户端” / `RESTORE-CLIENT.cmd` | 只用失败客户端的 backup metadata 恢复客户端文件 | 不重新安装插件 |
| “卸载最近安装” / `UNINSTALL-PLUGIN.cmd` | 按同一 ledger 移除精确 Add-in GUID 目录 | 保留可恢复 transaction evidence |
| `RECOVERY-MENU.cmd` | 打开恢复 UI | 适合已有失败/取消状态时重新进入 |

恢复或卸载前应关闭 ArcGIS Pro、相关 AI 客户端和可能占用配置/插件文件的进程。不要删除未知 `.lock`、`.sr.lock` 或历史输出来“清理”。

### 日志在哪里

默认状态根目录：

```text
%LOCALAPPDATA%\\ArcGISProMCP\\installer
```

重点文件和目录：

```text
one-click-state.json
one-click-operation.jsonl
recovery-index.json
latest-transaction.json
transactions\\
```

客户端恢复还可能使用客户端旁边的：

```text
<配置文件>.arcgis-pro-mcp.bak
<配置文件>.arcgis-pro-mcp.bak.json
```

提交反馈时只提供脱敏后的状态、错误码、时间、版本、hash、工具名/count 和调用结果；删除 token、密码、路径中不必要的个人信息和 GIS 敏感内容，但不要删除能说明事务边界的 ledger 字段。

## 1.0.2 与 r5 的关系

### 不变的生产契约

- 内嵌生产插件版本仍为 `1.0.2`；
- Add-in GUID、MCP endpoint 和 canonical 30-tool registry 不变；
- 生产 Python Bridge lifecycle semantics 未修改；
- r5 不增加任意 Python MCP，不增加 production fixture action；
- r5 不携带 `MyProject1.aprx`、`Phase4Test.gdb` 或历史运行目录。

### r5 固定包的实际部署层行为

r5 是 one-click share/deployment revision，固定包实际提供的部署层能力包括：

- Windows PowerShell 5.1 入口与包验证器；
- 单客户端选择、目标路径预览、只读预检和一次明确确认；
- 插件安装事务、客户端配置事务以及安装后的连接诊断；
- recovery index、ledger、备份/恢复、失败事务恢复和卸载入口；
- 用户可见的目标路径、Add-in GUID、预检结果、连接诊断和恢复状态。

### 独立验证 harness 的后续修订

以下项目属于独立验证 harness 或测试证据侧的修订，不是固定 r5 产品包新增的生产功能：

- UTF-8 encoded bootstrap、父进程 stdout/stderr 解码、dot-source scope 保留和 U+FFFD 替换字符断言；
- 失败时保留完整 owned-temp artifact snapshot；
- preset GUI smoke 以及实际只读按钮回调证据。

这些测试侧改进不改变生产 Python Bridge lifecycle semantics，也不把隔离 TestMode 安装、模拟 GUI smoke 或 harness 的 `122/122` 通过变成用户实机安装 PASS。当前真实用户验收仍需提交新机器上的安装、Pro `Start`、客户端工具发现、至少两个成功的非 `ping` 只读调用、重启和恢复证据。

## 新电脑验收清单

请逐项勾选并保留原始证据：

### 环境和包

- [ ] Windows x64 已记录；
- [ ] ArcGIS Pro 3.5 的实际版本已记录；
- [ ] .NET 8 runtime 已记录；
- [ ] ArcGIS Pro `arcgispro-py3` / ArcPy 状态已记录；
- [ ] ZIP SHA-256 等于 `3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652`；
- [ ] `verify-one-click-package.ps1` 返回 r5 / 23 files / payload hash / tools 30 / clean-machine `NOT VERIFIED`。

### 部署和目标

- [ ] ZIP 已完整解压，没有直接在压缩包内运行；
- [ ] ArcGIS Pro 已关闭后启动部署；
- [ ] 只选择一个客户端，或选择仅安装插件；
- [ ] Codex/Cursor 项目目录是之后实际打开的同一目录；
- [ ] 窗口显示的插件路径和 Add-in GUID 正确；
- [ ] 只读预检 PASS；
- [ ] 用户确认一次后部署完成；
- [ ] 没有修改受保护的 APRX/GDB、历史输出或未知锁文件。

### ArcGIS Pro、MCP 与客户端

- [ ] 用户打开 ArcGIS Pro 并在 MCP 选项卡点击 `Start`；
- [ ] 窗口或客户端证据显示 `tools/list=30`，distinct 30，duplicate 0；
- [ ] `ping` 返回 `pong` 且 `isError=false`；
- [ ] 至少两个非 `ping` 只读工具新鲜调用成功，且每次调用均无 MCP error；`get_project_info` 的上下文错误只能作为诊断记录，不计入这两个成功调用；
- [ ] 关闭并重新打开 ArcGIS Pro/客户端后重复一次 `ping` 和只读检查；
- [ ] 如测试恢复：按授权关闭相关程序，再验证回滚、客户端恢复或卸载；
- [ ] 保存脱敏日志、ledger、时间、版本、hash、工具发现和调用结果；
- [ ] 真实 AI 客户端连接没有证据的部分明确标为 `NOT VERIFIED`。

### 通过标准

只有当上述实机证据完整、路径属于用户、工具集合与生产契约精确一致、恢复结果可解释且保护边界不变时，才能提交给独立 Gate Keeper 做下一次审查。r5 本地隔离自动化的 `122/122` 通过不能替代这份实机证据。

## 反馈模板

请复制下面模板填写；不要粘贴凭据或完整敏感 GIS 数据：

```text
ArcGIS Pro MCP r5 实机反馈

电脑：Windows x64 / 版本：
ArcGIS Pro：
.NET runtime：
ArcGIS Pro Python / ArcPy：

ZIP 文件名：ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip
ZIP SHA-256：
解压目录：<可脱敏>
包验证：PASS / FAIL（附 JSON 或错误码）

选择范围：Codex / Cursor / DeepSeek Harness / 仅安装插件
项目目录是否与之后客户端完全一致：是 / 否 / 不适用
窗口显示的插件目标和 GUID：
客户端配置目标：

ArcGIS Pro 是否点击 MCP → Start：是 / 否
HTTP tools/list：PASS / FAIL / NOT VERIFIED
工具数量、distinct、duplicate：
ping：pong / FAIL / NOT VERIFIED
只读工具 1 及结果：
只读工具 2 及结果：
重启后复测：PASS / FAIL / NOT VERIFIED

恢复/卸载测试：未执行 / PASS / FAIL
错误码（如有）：
日志/ledger 路径（脱敏）：
保护资产是否保持不变：是 / 否 / 未检查
补充说明：
```

## 实际行为核对表与来源

### 文档与实际 r5 行为核对

| 核对项 | 实际依据 | 本指南采用的结论 |
|---|---|---|
| r5 包名、hash、payload、30 工具 | `Release\\ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip`、`.sha256`、`bundle-manifest.json` | 以 r5 hash 和 manifest 为唯一包身份 |
| launcher 和 UI | ZIP 内 `ONE-CLICK-SETUP.cmd`、`scripts\\one-click-setup.ps1` | 单客户端、目标预览、预检、确认、诊断和恢复按钮按实际脚本描述 |
| MCP 发现与诊断 | `Invoke-ConnectionDiagnosis` | 端口监听不等于 MCP；必须区分 `tools/list=30`、`ping=pong` 和客户端连接 |
| 生产 registry | `Source\\ArcGISProMCP.Compatibility\\Composition.cs`、`Tests\\UnitTests\\ProductionToolContractSnapshot.cs` | 列出精确 30 个工具，`mcp_auth` 排除 |
| 恢复安全边界 | ZIP 内 `scripts\\one-click-setup.ps1`、`scripts\\user-workflow.ps1`、`scripts\\client-config.ps1`、`scripts\\release-transaction.ps1` | 保留 ledger/backup/recovery index；不手工删除或覆盖 |
| 独立审批范围 | `Docs\\ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md` | 允许用户实机试用；真实安装、真实客户端、clean-machine 仍 `NOT VERIFIED` |
| GUI 证据 | 独立证据根 `.runtime\\one-click-deployment-r5\\run-20260910-052628-6f6d5d15ae56481092d378c74dd8e856\\gui-button-smoke` | 截图仅为实际只读按钮回调 smoke，路径是测试环境示例 |
| 包内旧用户指南 | ZIP 内 `Docs\\ONE_CLICK_DEPLOYMENT_USER_GUIDE.md` | 仍含 r4 版本/hash/待复核叙述；属于遗留冲突，不用于确定 r5 包身份或审批状态 |
