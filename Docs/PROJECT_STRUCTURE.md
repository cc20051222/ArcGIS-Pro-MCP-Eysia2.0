# 项目结构（PROJECT_STRUCTURE）

> Phase 1 建立的最小可运行 ArcGIS Pro 3.5 Add-in（Compatibility Host）。

---

## 1. Solution 与项目

| 解决方案 | `ArcGIS-Pro-MCP.sln`（仓库根目录） |
|----------|------------------------------------|

```
D:\ArcGIS-Pro-MCP\
├── ArcGIS-Pro-MCP.sln
├── Directory.Build.props        # 集中管理 ArcGIS Pro SDK 路径（$(ArcGISProFolder) / $(ArcGISProBin)）
├── scripts\
│   ├── dev-env.ps1              # 会话级 PATH 前置到用户级 .NET 8 SDK
│   └── package-addin.ps1        # 打包 .esriAddinX 并注册（复刻官方 targets 布局）
├── Source\
│   ├── Shared\
│   │   ├── ArcGISProMCP.Core/           net8.0
│   │   ├── ArcGISProMCP.Protocol/       net8.0
│   │   ├── ArcGISProMCP.Configuration/  net8.0
│   │   ├── ArcGISProMCP.Logging/        net8.0
│   │   └── ArcGISProMCP.Security/       net8.0
│   ├── ArcGISProMCP.Compatibility/      net8.0-windows   （被 ArcGIS Pro 3.5 加载的 Add-in）
│   └── ArcGISProMCP.Current/            （占位说明，未编译）
└── Docs\
    ├── ENVIRONMENT.md
    ├── ENVIRONMENT_SETUP.md
    ├── PROJECT_STRUCTURE.md
    └── ARCHITECTURE.md
```

### 各项目 TFM

| 项目 | TargetFramework | Platform | 说明 |
|------|-----------------|----------|------|
| Shared/* | `net8.0` | AnyCPU | 纯托管，**不引用 ArcGIS Pro SDK** |
| ArcGISProMCP.Compatibility | `net8.0-windows` | x64 (`PlatformTarget=x64`, `UseWPF`) | Add-in 宿主，引用 ArcGIS Pro 3.5 SDK |
| ArcGISProMCP.Current | — | — | 占位（Pro 3.7+ / .NET 10，暂未编译） |

---

## 2. ArcGIS Pro SDK 引用（真实路径，集中管理）

路径集中在 `Directory.Build.props`：
- `$(ArcGISProFolder) = C:\Program Files\ArcGIS\Pro\`
- `$(ArcGISProBin) = $(ArcGISProFolder)bin\`

Compatibility 项目实际引用的程序集（`<Reference>` 均为 `Private=false`，运行时由 Pro 提供）：

| 程序集 | 实际路径 |
|--------|----------|
| ArcGIS.Desktop.Core | `$(ArcGISProBin)Extensions\Core\ArcGIS.Desktop.Core.dll` |
| ArcGIS.Desktop.Mapping | `$(ArcGISProBin)Extensions\Mapping\ArcGIS.Desktop.Mapping.dll` |
| ArcGIS.Desktop.Catalog | `$(ArcGISProBin)Extensions\Catalog\ArcGIS.Desktop.Catalog.dll` |
| ArcGIS.Desktop.Framework | `$(ArcGISProBin)ArcGIS.Desktop.Framework.dll` |

（Catalog 引用是 `Project.GetItems<T>()` 的传递依赖所需。）

---

## 3. 依赖关系

```
ArcGISProMCP.Compatibility
   ├─> ArcGISProMCP.Core
   ├─> ArcGISProMCP.Protocol
   ├─> ArcGISProMCP.Configuration
   ├─> ArcGISProMCP.Logging
   ├─> ArcGISProMCP.Security
   └─> ArcGIS Pro 3.5 SDK 程序集（Core / Mapping / Catalog / Framework）
```
Shared 各项目之间暂不互相引用；Shared 不引用 ArcGIS Pro SDK（架构约束）。

---

## 4. Build 方式

```powershell
# 1) 先加载用户级 .NET 8 SDK 到 PATH（保证 dotnet 为 8.0.424）
. .\scripts\dev-env.ps1
dotnet --version            # 必须为 8.0.424

# 2) 编译（0 warning / 0 error）
dotnet build ArcGIS-Pro-MCP.sln -c Debug
```

- 构建使用用户级 .NET SDK `8.0.424`（`%LOCALAPPDATA%\Microsoft\dotnet`）。
- 编译通过后，Compatibility 输出到
  `Source\ArcGISProMCP.Compatibility\bin\x64\Debug\net8.0-windows\`。

> 注意：本机 `dotnet build`（.NET Core MSBuild）**不支持** `CodeTaskFactory`，
> 因此官方 `Esri.ProApp.SDK.Desktop.targets` 无法在 `dotnet build` 中完成打包
> （会报 MSB4801）。同时本机 VS 2026 的 MSBuild.exe 未安装 .NET 桌面工作负载 /
> .NET SDK 解析器，无法编译 SDK 风格项目。故打包在编译后用脚本完成（见下）。

---

## 5. Add-in 打包方式

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\package-addin.ps1
```

`scripts/package-addin.ps1` 复刻官方 targets 的打包布局：
```
*.esriAddinX（zip）
├── Config.daml              （包根）
├── Images\AddInIcon.png     （包根）
└── Install\
    ├── ArcGISProMCP.Compatibility.dll
    ├── ArcGISProMCP.Compatibility.pdb
    ├── ArcGISProMCP.Compatibility.deps.json
    └── <Shared 各 dll> ...
```
然后调用官方 `C:\Program Files\ArcGIS\Pro\bin\RegisterAddIn.exe <pkg> /s` 注册。

产物：`Source\ArcGISProMCP.Compatibility\bin\x64\Debug\net8.0-windows\ArcGISProMCP.Compatibility.esriAddInX`

---

## 6. 安装方式

- 由 `package-addin.ps1` 自动调用 `RegisterAddIn.exe` 注册到当前用户。
- 已注册位置：`<user-home>\Documents\ArcGIS\AddIns\ArcGISPro\{BAA5628C-3C08-4AD5-A6C0-915ADA475709}\ArcGISProMCP.Compatibility.esriAddInX`
- 卸载：`RegisterAddIn.exe <pkg> /u`（未在本阶段执行）。

---

## 7. Key 内容物

| 文件 | 作用 |
|------|------|
| `Config.daml` | Add-in 清单：AddInInfo、模块、MCP 选项卡/MCP 组、Start/Status 按钮 |
| `Module1.cs` | Add-in 模块入口（`ArcGIS.Desktop.Framework.Contracts.Module`） |
| `Controls/StartButton.cs` | Start 按钮（点击显示占位消息） |
| `Controls/StatusButton.cs` | Status 按钮（点击调 `ArcGISHost.GetMapsAsync()` 后显示 Host 状态） |
| `Hosting/IArcGISHost.cs` | Host 接口：`GetCurrentMapAsync()` / `GetMapsAsync()` |
| `Hosting/ArcGISHost.cs` | Host 实现（`QueuedTask.Run` + `MapView.Active` / `Project.GetItems<Item>` / `MapFactory`） |
| `Images/AddInIcon.png` | 32x32 图标 |
