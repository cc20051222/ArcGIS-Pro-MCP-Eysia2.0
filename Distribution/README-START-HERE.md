# ArcGIS Pro MCP — 从这里开始

本目录提供 **10 个入口件**（Windows 批处理；含一键式与恢复菜单），用于在本机安装 / 配置 / 卸载 ArcGIS Pro MCP 插件。
入口件本身**不做任何写入**：所有变更都由仓库 `scripts\` 下的事务脚本执行（事务式安装，失败可回滚）。

## 版本身份（如实登记；来源 = release-manifest / Config.daml / 工具注册表）

| 项 | 值 |
|---|---|
| 产品名 | ArcGIS Pro MCP |
| AddIn ID | `{BAA5628C-3C08-4AD5-A6C0-915ADA475709}` |
| Release version | 1.0.2（程序集版本 1.0.0.0，属**有意的二进制兼容标识**，非版本错配） |
| 目标平台 | ArcGIS Pro 3.0+ / x64 / net6.0-windows（**支持 3.5 已实测；3.0–3.4 编译期兼容·未验证**） |
| **工具数** | **149** |
| **错误码数** | **33** |
| 包文件 | `ArcGISProMCP.Compatibility.esriAddInX`（字节数见包外 `release-manifest.json`） |
| 包 SHA-256（现役） | 以**包外** `release-manifest.json` 为准（包哈希不可自包含）；lineage：`9BBD8847…` → `24CA2B4B…` → 本批 net6 候选（D-060） |
| 包条目 | 43（逐条见包外 `release-manifest.json`） |
| 功能冻结 | `Docs/_gatekeeper/FUNCTION_SCOPE_FREEZE_D056.md`（SIGNED/FROZEN；**D-060 增补至 80 工具；D-061 增补至 93；D-062 增补至 107；D-063 增补至 128；D-064 增补至 149 工具**，增补段见该文件 §A1） |

> 早期文档出现过「30 工具」的表述，属**陈旧口径**；当前**以 149 工具为准**。
> **现役基线（rollback 锚点）**：`C8A71CFA…`/517,797 B（80 工具 / 错误码 33；D-060 LIVE 后现役）。D-064 阶段二 LIVE 后现役将切换为 **D-064 候选（149 工具 / 错误码 33）**；阶段性现役基线：`C4A6B143…`/633,815 B（128 工具，D-063 LIVE 后现役）。包身份一律以**包外** `release-manifest.json` 为准（本 README 不内嵌包哈希）。

## 入口件

| 文件 | 作用 | 是否写入系统 |
|---|---|---|
| `START-HERE.cmd` | 指引（列出后续命令 + 依赖检测命令） | 否 |
| `INSTALL-PLUGIN.cmd` | 依赖检测 → 预检 → 事务式安装 | 是（经事务脚本） |
| `CONFIGURE-CLIENT.cmd` | **菜单式客户端配置向导**（codex / cursor / deepseek-harness / claude-desktop）——**逐客户端独立事务**（Plan 预览 → 确认 → Apply → Validate），**不提供批量应用**；支持 `[client] [configRoot]` 直达参数 | 是（经事务脚本） |
| `CONFIGURE-CODEX.cmd` | 单客户端快捷入口（等价于向导的 codex 分支，保留向后兼容） | 是（经事务脚本） |
| `UNINSTALL-PLUGIN.cmd` | 预检 → 事务式卸载 | 是（经事务脚本） |
| 一键式 / 恢复类 | `ONE-CLICK-SETUP.cmd`、`RECOVERY-MENU.cmd`、`ROLLBACK-PLUGIN.cmd`、`RESTORE-CLIENT.cmd`、`ONE-CLICK-UNINSTALL-PLUGIN.cmd` —— 详见 `README-ONE-CLICK-START.md` | 是（经事务脚本） |
| 一键式 / 恢复类 | `ONE-CLICK-SETUP.cmd`、`RECOVERY-MENU.cmd`、`ROLLBACK-PLUGIN.cmd`、`RESTORE-CLIENT.cmd`、`ONE-CLICK-UNINSTALL-PLUGIN.cmd` —— 详见 `README-ONE-CLICK-START.md` | 是（经事务脚本） |
| 一键式 / 恢复类 | `ONE-CLICK-SETUP.cmd`、`RECOVERY-MENU.cmd`、`ROLLBACK-PLUGIN.cmd`、`RESTORE-CLIENT.cmd`、`ONE-CLICK-UNINSTALL-PLUGIN.cmd` —— 详见 `README-ONE-CLICK-START.md` | 是（经事务脚本） |

## 前置条件

1. **ArcGIS Pro 3.0–3.5**（x64）已安装且**未在运行**（事务脚本会检测 `ARCGIS_PRO_RUNNING` 并拒绝变更）。
2. **ArcGIS Pro SDK** 程序集（`ArcGIS.Core.dll` / `ArcGIS.Desktop.Framework.dll`）位于 Pro 安装目录。
3. **.NET 运行时 ≥ 6** 可用（net6 目标框架；3.0–3.2 宿主为 .NET 6，3.3+ 宿主 .NET 8 可加载 net6 程序集）。
4. **arcpy（Python）** 可导入 —— 由 `scripts\check-compatibility.ps1` 检测。
5. 需要 Windows PowerShell（5.1 即可；PowerShell 7 非必需）。

单独执行依赖检测（不安装）：

```bat
%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\check-compatibility.ps1" -Json
```

**缺依赖时的行为**：入口件**只提示并中止**，**不静默修改系统**（不自动安装 .NET、不改环境变量、不动 Pro 配置）。

## 回滚

- 安装/卸载均为事务式：写入前自动快照，失败可 `-Action Rollback`。
- 仓库内另有 `.runtime/evolution/phase08/backup-pre-*` 备份链（逐代保留，供人工回退）。
- 陈旧/不匹配的备份会被拒绝冒充现役（陈旧备份拒绝），详见 `Docs\SHARING_AND_SIMPLE_INSTALL.md`。

## 未验证项（如实披露，不包装为「已支持」）

- **干净机实装**：**NOT VERIFIED** —— 尚未在未装过本插件的机器上执行完整安装验证，需用户协同安排。
- **安装向导（GUI）**：**NOT VERIFIED / 未提供** —— 当前仅提供命令行入口件。
- **代码签名与公网发布**：**NOT VERIFIED / 未执行** —— 需另行授权；当前包**未签名**。
- **连接级客户端验证**：仅 WorkBuddy 侧完成连接级验证；Codex / Cursor / DeepSeek-Harness 目前为**配置面**验证，**连接级 NOT VERIFIED**。

## 更多信息

- 分享与简化安装：`Docs\SHARING_AND_SIMPLE_INSTALL.md`
- 功能冻结清单（149 工具权威口径）：`Docs/_gatekeeper/FUNCTION_SCOPE_FREEZE_D056.md`
