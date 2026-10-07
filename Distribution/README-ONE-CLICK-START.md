# ArcGIS Pro MCP — 一键部署包 · 从这开始

> 版本 1.0.2 ｜ 工具数 **239** ｜ 错误码 **33** ｜ 目标 ArcGIS Pro（x64 / Windows，双轨自动选层）：**net8 轨 3.3–3.6**・**net6 轨 3.0–3.2**；**3.5 已实测，其余版本编译期兼容·未验证**
> 端点 `http://127.0.0.1:6520/mcp` ｜ 本包入口脚本携带**自签** Authenticode（签名者 `4E4ED6CBD52F7D9F73CEFC1462E78BEB380590C0`），`Get-AuthenticodeSignature` 读回 `Status=UnknownError`＝**链终止于不受信任根**・不得按受信处理 ｜ 干净机验收 **NOT VERIFIED**（如实披露，不包装）

## 1. 先核对完整性（必做）

1. 用随包 `.sha256` 核对 ZIP：`certutil -hashfile <zip 文件名> SHA256`
2. **完整解压**（右键 → 全部解压缩）。不要直接在压缩包里双击运行。

## 2. 三步装机

| 步骤 | 操作 | 说明 |
|---|---|---|
| 1 | 双击 **`ONE-CLICK-SETUP.cmd`** | 打开图形窗口；选择客户端（默认 Codex），或勾选「仅安装插件」 |
| 2 | 确认一次，等待完成 | 只读预检 → 包完整性校验 → 插件安装事务 → 各客户端配置事务 → 连接诊断 |
| 3 | 打开 ArcGIS Pro → **MCP 选项卡 → Start** | 端口 `6520` 才会监听；窗口随后显示 `tools/list=239` 与 `ping=pong` |

> 安装器**不会**启动或强杀 ArcGIS Pro，也不会替你点 Start。**端口监听本身不等于连接成功**。

## 3. 出错怎么办

| 现象 / 错误码 | 处理 |
|---|---|
| `COMPATIBILITY_NOT_PASS` | 缺 ArcGIS Pro 3.0–3.5 / .NET 6+ runtime / arcpy —— 按提示补齐后重试 |
| `ARCGIS_PRO_RUNNING` | 先由你关闭 ArcGIS Pro（安装器不强制关闭） |
| `PROJECT_ROOT_NOT_FOUND` | 重新选择已存在的项目目录 |
| `WAITING_USER_START` | 到 ArcGIS Pro 的 MCP 选项卡点 Start |
| `PORT_OPEN_NOT_MCP` | 6520 被其他程序占用；关闭占用者后重试（不会升级为连接 PASS） |
| `STALE_BACKUP_REFUSED` | 目标文件在上次事务后被手工编辑 —— 先备份当前内容，再人工审查 |
| `BUNDLE_*` | 包缺件 / 被篡改 —— 重新获取完整 ZIP |

**恢复入口**：`RECOVERY-MENU.cmd`（窗口）、`ROLLBACK-PLUGIN.cmd`（回滚）、`UNINSTALL-PLUGIN.cmd`（卸载）、`RESTORE-CLIENT.cmd`（只恢复客户端配置）。

## 4. 详细教程

包内 `Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md`：逐步骤截图位、判据、故障分类、证据边界与最小实机检查清单。
