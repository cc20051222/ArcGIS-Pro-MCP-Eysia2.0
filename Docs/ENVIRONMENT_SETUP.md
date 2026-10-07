# ArcGIS Pro MCP — 开发环境准备记录（ENVIRONMENT_SETUP）

> 阶段：开发环境准备（Phase 0 → Phase 1 之间）
> 执行时间：2026-08-31
> 机器：`PC`（用户 `<machine>\<account>`）
> 原则：用户级安装、不改系统 PATH、不删/不改已有 .NET、不装 .NET 10、不改 Pro 安装与 Python 环境、不设 Git 全局身份。

---

## 1. 安装内容

| 项 | 值 |
|----|----|
| 安装软件 | .NET SDK 8.0（x64） |
| 安装版本 | **8.0.424** |
| 附带运行时 | Microsoft.NETCore.App / AspNetCore.App / WindowsDesktop.App **8.0.30** |
| 安装位置 | `<user-home>\AppData\Local\Microsoft\dotnet`（用户级） |
| 安装方式 | 官方 `dotnet-install.ps1`（`-NoPath`，未写 PATH） |
| 是否改动已有 .NET | 否（系统 `C:\Program Files\dotnet` 原样保留，仍无 SDK） |

安装命令（实际执行）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:TEMP\dotnet-install.ps1" `
  -Channel 8.0 -Architecture x64 `
  -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet" -NoPath
```

> 脚本来源：`https://dot.net/v1/dotnet-install.ps1`（Microsoft 官方，下载到 `%TEMP%\dotnet-install.ps1`）。

---

## 2. 安装后验证结果

### dotnet --version

```
8.0.424
```

### dotnet --list-sdks

```
8.0.424 [<user-home>\AppData\Local\Microsoft\dotnet\sdk]
```

### dotnet --list-runtimes

```
Microsoft.AspNetCore.App 8.0.30 [<user-home>\AppData\Local\Microsoft\dotnet\shared\Microsoft.AspNetCore.App]
Microsoft.NETCore.App 8.0.30 [<user-home>\AppData\Local\Microsoft\dotnet\shared\Microsoft.NETCore.App]
Microsoft.WindowsDesktop.App 8.0.30 [<user-home>\AppData\Local\Microsoft\dotnet\shared\Microsoft.WindowsDesktop.App]
```

### dotnet --info（要点）

```
.NET SDK: 8.0.424 (MSBuild 17.11.48)
Host: 8.0.30, win-x64
RID: win-x64
```

### 编译冒烟测试（net8.0-windows）

在 `%TEMP%\arcgis-mcp-smoke` 用用户级 SDK 构建 `net8.0-windows` 类库：

```
已成功生成。 0 个警告 0 个错误 (exit code 0)
输出: ...\bin\Debug\net8.0-windows\smoke.dll
```

> 结论：.NET 8 SDK + net8.0-windows 目标包 **真实可用**。冒烟项目已删除。

---

## 3. PATH 处理

- **未修改**系统级 / 用户级环境变量（安装使用 `-NoPath`）。
- 系统 PATH 上的 `dotnet` 仍指向 `C:\Program Files\dotnet\dotnet.exe`（运行时-only，无 SDK，原样保留）。
- 提供**会话级、可追踪**的辅助脚本：`scripts/dev-env.ps1`
  - 仅把用户级 SDK 目录前置到**当前 PowerShell 会话**的 PATH，关闭即失效。
  - 用法：`. .\scripts\dev-env.ps1`，执行后 `dotnet --version` → `8.0.424`（已验证）。
- 后续每个构建命令会先 dot-source 该脚本（或在命令内显式使用全路径 `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`）。

---

## 4. ArcGIS Pro SDK 状态

| 检查项 | 路径 | 结果 |
|--------|------|------|
| Core 程序集 | `bin\Extensions\Core\ArcGIS.Desktop.Core.dll` | ✅ 13.5.0.57366 |
| Mapping 程序集 | `bin\Extensions\Mapping\ArcGIS.Desktop.Mapping.dll` | ✅ 13.5.0.57366 |
| Catalog 程序集 | `bin\Extensions\Catalog\ArcGIS.Desktop.Catalog.dll` | ✅ 13.5.0.57366 |
| Editing 程序集 | `bin\Extensions\Editing\ArcGIS.Desktop.Editing.dll` | ✅ 13.5.0.57366 |
| Layout 程序集 | `bin\Extensions\Layout\ArcGIS.Desktop.Layouts.dll` | ✅ 13.5.0.57366 |
| Geoprocessing 程序集 | `bin\Extensions\Geoprocessing\ArcGIS.Desktop.GeoProcessing.dll` | ✅ 13.5.0.57366 |
| Framework 程序集 | `bin\ArcGIS.Desktop.Framework.dll` | ✅ 13.5.0.57366 |
| 打包 targets | `bin\Esri.ProApp.SDK.Desktop.targets` | ✅ 存在（18,159 字节） |
| Add-in 注册工具 | `bin\RegisterAddIn.exe` | ✅ 存在 |
| VSIX 项目模板 | — | 未安装（按计划暂不安装） |

**结论：可通过「手工 .csproj」创建并编译 ArcGIS Pro Add-in。**
依据：
1. SDK 运行时程序集齐全（`bin` 与 `bin\Extensions\*`）；
2. 打包 targets 齐全（`Esri.ProApp.SDK.Desktop.targets`，内含 `PackageAddIn` 任务，可产出 `.esriAddinX`）；
3. `RegisterAddIn.exe` 齐全；
4. .NET 8 SDK 已就绪（net8.0-windows 冒烟构建通过）。

> 完整 Add-in（含 Config.daml）的真实编译将在 Phase 1 进行并记录结果，不依赖未验证的 VS 2026 VSIX 模板。

---

## 5. Git 状态

- 仓库已初始化：`D:\ArcGIS-Pro-MCP`（分支 `master`，无提交）。
- 全局身份：`git config --global user.name` / `user.email` 均**未配置**（按要求：仅报告，不擅自设置）。
- 仓库本地身份：同样未配置。
- 因此**本阶段不提交 commit**。

---

## 6. 环境复验总表

| 项 | 结果 |
|----|------|
| Windows | ✅ Windows 11 专业版 64-bit (26200) |
| ArcGIS Pro | ✅ 3.5.0 (Build 57366) |
| Visual Studio | ✅ Community 2026 (18.1.11312.151) + BuildTools (18.9.12120.119) |
| .NET 8 SDK | ✅ 8.0.424（用户级） |
| ArcGIS Pro SDK | ✅ 程序集 + targets + RegisterAddIn 齐全 |
| Python | ✅ 3.11.11 |
| ArcPy | ✅ arcpy 3.5 / 57366 |
| Git | ✅ 2.53.0（仓库已初始化；身份未配置） |

---

## 7. Build 前置条件（已满足）

- ✅ .NET 8 SDK（8.0.424，用户级）
- ✅ net8.0-windows 目标包（冒烟构建 0 错误 0 警告）
- ✅ ArcGIS Pro 3.5 SDK 程序集 + 打包 targets
- ✅ MSBuild（随 SDK 17.11.48）
- ✅ 工作区可写（`D:\ArcGIS-Pro-MCP`）

**判定：ArcGIS Pro 3.5 + .NET 8 SDK + ArcGIS Pro SDK 已构成可编译 Add-in 的开发环境。**

---

## 8. 仍存在的问题

1. **Git 身份未配置**：提交前需先设置 `user.name` / `user.email`（等用户明确提供后配置，或用户自行设置）。
2. **VS 2026 VSIX 模板未安装**：官方 ArcGIS Pro SDK 项目模板未装（按计划采用手工 csproj 绕开，无阻断）。
3. **当前 shell 非提权**：机器级操作（如向 `C:\Program Files` 写入）需提权；本阶段全部用户级，无影响。
4. **系统 `dotnet`（PATH 上）仍无 SDK**：必须 dot-source `scripts/dev-env.ps1` 或使用用户级全路径调用 SDK。
5. **联网核对 Esri 官方文档不可用**：web 检索接口认证错误，`3.7+ → .NET 10`、SDK 模板下载地址等仍以官方文档为准（不影响当前 Pro 3.5 开发）。
