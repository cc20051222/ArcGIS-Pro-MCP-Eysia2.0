# ArcGIS Pro MCP — 环境检测报告（Phase 0）

> 检测时间：2026-08-31 12:28 (+08:00)
> 检测机器：`PC`（用户 `<machine>\<account>`）
> 阶段性质：仅环境检测，不编写业务代码、不安装任何软件。

---

## 0. 结论摘要

| # | 检测项 | 结果 | 状态 |
|---|--------|------|------|
| 1 | Windows 版本 | Windows 11 专业版 64-bit (Build 26200) | ✅ |
| 2 | ArcGIS Pro 版本 | **3.5.0** (Build 57366, 简体中文) | ✅ 已安装 |
| 3 | Visual Studio | Community 2026 (18.1.11312.151) + BuildTools 2026 (18.9.12120.119) | ✅ 已安装 |
| 4 | .NET 8 SDK | **未安装**（仅有 .NET 8 运行时） | ❌ 缺失 |
| 5 | .NET 10 SDK | **未安装**（无 .NET 10 运行时/SDK） | ❌ 缺失（暂不需要） |
| 6 | ArcGIS Pro SDK | 运行时程序集 + 打包 targets 存在；**VSIX 项目模板未安装** | ⚠️ 部分 |
| 7 | Python 版本 | Python 3.11.11（arcgispro-py3 conda 环境） | ✅ |
| 8 | ArcPy 环境 | `import arcpy` 成功，LicenseLevel=Advanced | ✅ |
| 9 | Git | git 2.53.0.windows.1（**user.name/user.email 未配置**） | ⚠️ 部分 |
| 10 | 当前工作区 | `D:\ArcGIS-Pro-MCP`（存在、为空、可写） | ✅ |
| 11 | 当前项目文件 | 无（空工作区） | ➖ |
| 12 | 可否创建 Pro Add-in | **暂不能编译**：缺 .NET 8 SDK；其余条件已具备 | ⚠️ 待补 |
| 13 | 开发权限 | 账户属 Administrators，但当前 shell **未提权**（Medium 完整性） | ⚠️ 部分 |
| 14 | Pro 能否正常启动 | **可启动**：进程存活 30s、响应、主窗口 "ArcGIS Pro"、可优雅退出 | ✅ |
| 15 | Pro Python 环境可用 | python.exe + arcpy 均可用 | ✅ |

**一句话结论**：本机具备运行 ArcGIS Pro 3.5、ArcPy、Visual Studio 2026 的条件，ArcGIS Pro 可正常启动；**唯一阻断性缺失是「.NET 8 SDK」**，安装后即可开始创建并编译 ArcGIS Pro 3.5 Add-in。

---

## 1. Windows 版本

| 项 | 值 |
|----|----|
| 产品 | Microsoft Windows 11 专业版 |
| 架构 | 64-bit |
| 版本/内部版本 | 10.0.26200 / Build 26200 |
| 计算机名 | PC |
| 当前用户 | <machine>\<account> |

---

## 2. ArcGIS Pro 版本

| 项 | 值 |
|----|----|
| 产品 | ArcGIS Pro |
| 版本 | **3.5.0** |
| BuildNumber | 57366 |
| 安装目录 | `C:\Program Files\ArcGIS\Pro\` |
| 语言 | 2052（简体中文），zh-CN 语言包 |
| PythonCondaRoot | `C:\Program Files\ArcGIS\Pro\bin\Python` |
| PythonCondaEnv | `arcgispro-py3` |

> 本机仅检测到 **Pro 3.5** 一个版本；未安装 Pro 3.3 / 3.4 / 3.6 / 3.7。
> 另检测到遗留的 **ArcGIS Desktop 10.8**（`C:\Program Files (x86)\ArcGIS\Desktop10.8`），与本项目无关。

---

## 3. Visual Studio 版本

| 实例 | 版本 | 路径 |
|------|------|------|
| Visual Studio Community 2026 | 18.1.11312.151 | `C:\Program Files\Microsoft Visual Studio\18\Community` |
| Visual Studio Build Tools 2026（生成工具） | 18.9.12120.119 | `C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools` |

- 两个实例均存在 `MSBuild.exe`。
- **未检测到** "ArcGIS Pro SDK for .NET" 的 VS 扩展/项目模板（用户扩展目录中无 ArcGIS/Esri 相关内容）。
- VS 2026 实例的工作负载清单未能直接读取（VS 2026 安装状态存储位置与旧版不同），但 .NET SDK 缺失这一结论不受影响。

---

## 4. .NET 8 SDK

**状态：未安装。** 已安装的仅是运行时，不是 SDK。

检测证据：
- `dotnet --list-sdks`（x64）：`No SDKs were found`
- `C:\Program Files\dotnet\sdk`：目录为空
- x86 dotnet（`C:\Program Files (x86)\dotnet`）：无 `sdk` 目录
- VS 2026 自带 dotnet 仅为 `net8.0` **运行时**（含 host/shared，无 `sdk` 目录）

已存在的 .NET 8 运行时：8.0.0 / 8.0.11 / 8.0.13（x64），以及 VS 自带 net8.0 运行时 8.0.21。

---

## 5. .NET 10 SDK

**状态：未安装。** 未发现任何 .NET 10 运行时或 SDK（本机运行时最高为 9.0.2）。

> 按项目规则，.NET 10 仅用于 **Pro 3.7+ Current Host**；当前本机为 Pro 3.5，暂不需要。

---

## 6. ArcGIS Pro SDK

**运行时程序集：存在 ✅**

- `C:\Program Files\ArcGIS\Pro\bin\ArcGIS.Desktop.Framework.dll`（及 17 个 `ArcGIS.Desktop.*.dll`，多为 WPF）
- 核心 SDK 程序集位于 `bin\Extensions\<模块>\`，例如：
  - `bin\Extensions\Core\ArcGIS.Desktop.Core.dll`
  - `bin\Extensions\Mapping\ArcGIS.Desktop.Mapping.dll`
  - `bin\Extensions\Catalog\ArcGIS.Desktop.Catalog.dll`
  - `bin\Extensions\Editing\ArcGIS.Desktop.Editing.dll`
  - `bin\Extensions\Layout\ArcGIS.Desktop.Layouts.dll`
  - `bin\Extensions\Geoprocessing\ArcGIS.Desktop.GeoProcessing.dll`
  - …（Analyst3D / DataReviewer / Metadata / Sharing / Workflow 等扩展齐全）

**打包/构建 targets：存在 ✅**

- `C:\Program Files\ArcGIS\Pro\bin\Esri.ProApp.SDK.Desktop.targets`
  - 内含 `PackageAddIn` / `CleanAddIn` / `ConvertToRelativePath` 任务，可把含 `Config.daml` 的项目打包为 `.esriAddinX`。

**SDK 开发工具链（VSIX 项目模板 / NuGet 包）：未安装 ⚠️**

- 用户 `%USERPROFILE%\.nuget\packages` 中无任何 Esri/ArcGIS 包。
- VS 扩展目录中无 ArcGIS Pro SDK 模板。

> 说明：缺少 VSIX 模板不阻断开发——可手工编写 `.csproj`（引用 bin 下的 SDK 程序集 + 导入 `Esri.ProApp.SDK.Desktop.targets`）完成 Add-in 构建。官方模板仅为便利项。

---

## 7. Python 版本

- 版本：**Python 3.11.11** (64-bit)
- 可执行文件：`C:\Program Files\ArcGIS\Pro\bin\Python\envs\arcgispro-py3\python.exe`
- conda 环境：`base`（`C:\Program Files\ArcGIS\Pro\bin\Python`）与 `arcgispro-py3`（当前激活）

---

## 8. ArcPy 环境

`import arcpy` 成功，`arcpy.GetInstallInfo()` 返回：

```json
{
  "LicenseLevel": "Advanced",
  "InstallDir": "c:\\program files\\arcgis\\pro\\",
  "ProductName": "ArcGISPro",
  "Version": "3.5",
  "BuildNumber": "57366",
  ...
}
```

结论：**ArcPy 环境可用 ✅**（无需启动 Pro 即可导入，Level 为 Advanced）。

---

## 9. Git

- 版本：**git 2.53.0.windows.1** ✅
- **未配置** `user.name` 与 `user.email`（提交前需配置）。
- 工作区尚未初始化 git 仓库。

---

## 10. 当前工作区

- 路径：`D:\ArcGIS-Pro-MCP`
- 状态：存在、为空、**可写**（写入/删除测试通过）。

## 11. 当前项目文件

- 无（空工作区，尚未创建任何项目文件）。

---

## 12. 是否可以创建 ArcGIS Pro Add-in

| 条件 | 状态 |
|------|------|
| ArcGIS Pro 3.5 已安装 | ✅ |
| SDK 运行时程序集（bin\Extensions\*） | ✅ |
| SDK 打包 targets（Esri.ProApp.SDK.Desktop.targets） | ✅ |
| VS 2026 + MSBuild | ✅ |
| 用户 Add-in 目录（`Documents\ArcGIS\AddIns\ArcGISPro`）可用 | ✅（已有 1 个已注册的 `.esriAddinX`） |
| `RegisterAddIn.exe` | ✅ `C:\Program Files\ArcGIS\Pro\bin\RegisterAddIn.exe` |
| **.NET 8 SDK（编译 net8.0-windows 必需）** | ❌ **缺失** |

**结论：目前「不能」直接创建并编译 Add-in——唯一阻断项是 .NET 8 SDK。** 安装 .NET 8 SDK 后即可手工编写 csproj 并编译；官方 VSIX 模板为可选便利项。

---

## 13. 开发权限

- 账户 `<machine>\<account>` 属于 `BUILTIN\Administrators` 组。
- **但当前 shell 令牌未提权**：完整性级别为 `Medium Mandatory Level`，`IsInRole(Administrator)` = False（UAC 过滤令牌）。
- 影响：
  - `D:\ArcGIS-Pro-MCP`、`<user-home>\Documents` 可写（构建、注册 Add-in 无需提权）。
  - 向 `C:\Program Files` 写入（例如机器级安装 .NET SDK）需要**提权**；或改用**用户级安装**（见下）。

---

## 14. ArcGIS Pro 能否正常启动

实测（2026-08-31）：

- 启动 `ArcGISPro.exe` 后 **30 秒进程仍存活**；
- `Responding = True`；
- `MainWindowTitle = "ArcGIS Pro"`（已创建主窗口）；
- 工作集约 329 MB；
- `CloseMainWindow()` 优雅关闭成功，进程退出。

**结论：ArcGIS Pro 3.5 可正常启动 ✅。**

> 说明：本次为进程级验证；GUI 内的**登录/授权状态未人工目视确认**（标记 NOT VERIFIED）。结合 FlexNet Licensing Service（32/64 位均在运行）与 ArcPy LicenseLevel=Advanced，推断已有可用授权，但建议后续在 GUI 中确认一次。

---

## 15. ArcGIS Pro Python 环境是否可用

实测：

- `python.exe --version` → `Python 3.11.11`
- `import arcpy` → 成功，`arcpy.GetInstallInfo()` 正常返回 3.5 / 57366 / Advanced

**结论：可用 ✅。**

---

## 版本矩阵（Pro ↔ .NET）

| ArcGIS Pro | 目标 .NET | 本机验证情况 |
|------------|-----------|--------------|
| 3.3 | .NET 8 | 未安装该版本（NOT VERIFIED） |
| 3.4 | .NET 8 | 未安装该版本（NOT VERIFIED） |
| **3.5** | **.NET 8** | **✅ 已本地验证**：`ArcGISPro.runtimeconfig.json` → `"tfm": "net8.0"` |
| 3.6 | .NET 8 | 未安装该版本（NOT VERIFIED） |
| 3.7+ | .NET 10 | 未安装该版本；依据项目规则（NOT VERIFIED） |

> 3.3–3.6 → .NET 8 的映射由本机 Pro 3.5 的 `net8.0` TFM 直接佐证；3.7+ → .NET 10 为项目规则约定。本次会话联网核对 Esri 官方文档失败（web 检索接口认证错误），未能在线二次确认，已如实标注。

---

## 缺失环境与安装建议

| 缺失项 | 为什么需要 | 版本 | 如何安装（未执行，等待批准） |
|--------|-----------|------|------------------------------|
| **.NET 8 SDK** | 编译 net8.0-windows 的 Pro 3.5 Add-in（Compatibility Host） | 8.0.x（建议 8.0.4xx） | ① 官方安装器：<https://dotnet.microsoft.com/download/dotnet/8.0>（机器级，需提权）；② `winget install Microsoft.DotNet.SDK.8`（需提权）；③ 用户级免提权：`dotnet-install.ps1 -Channel 8.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"` |
| .NET 10 SDK | 仅 Pro 3.7+ Current Host 需要 | 10.0.x | 暂不安装；待迁移 Pro 3.7+ 时再装 |
| ArcGIS Pro SDK for .NET 项目模板（VSIX） | 官方 Add-in 项目模板（便利项，非必需） | 对应 Pro 3.5 | Esri 官方渠道（Visual Studio Marketplace / My Esri）下载对应版本 VSIX；或手工编写 csproj（已具备打包 targets） |
| Git 身份 | 每阶段提交 commit 需要 | — | `git config --global user.name "..."` / `user.email "..."` |

> 按约定：**未获明确批准前不安装任何软件。**

---

## 已知限制 / NOT VERIFIED 清单

1. Pro 3.3 / 3.4 / 3.6 / 3.7 的版本矩阵：本机未安装这些版本，未做本地验证。
2. Pro GUI 内的登录/授权状态：未人工目视确认。
3. Esri 官方文档联网核对：本次会话 web 检索接口认证失败，未能在线确认（3.7+ → .NET 10、SDK 模板下载地址等以官方为准）。
4. VS 2026 对 ArcGIS Pro SDK 3.5 官方模板的兼容性：未验证（官方模板历史上针对 VS 2022）；不影响手工 csproj 构建路径。
5. 当前 shell 未提权：机器级安装 SDK 需提权或用用户级安装绕开。

---

## 结论

- ✅ 可运行、可启动、可写：Pro 3.5、ArcPy(Python 3.11)、Git、工作区、VS 2026。
- ⚠️ 需补充：**（1）.NET 8 SDK**（阻断项，必需）；Git 身份配置；官方 SDK 模板（可选）。
- ➡️ 下一步（Phase 1 前）建议先安装 .NET 8 SDK，再进入项目脚手架与 Shared Core 搭建。
