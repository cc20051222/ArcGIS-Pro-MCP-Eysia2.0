# ArcGIS Pro MCP 一键部署与使用教程（1.0.2 · 239 工具）

> **代际同步说明（D-128 r26 重制批・2026-10-06 加入；D-131 r27 补代际批・D-134 r28・D-143 r29・D-145 包内归位随代际更新）**：本文随包分发文本此前停留在更早代际（工具数・适用包・签名状态三处与现役相反），本轮按 2026-10-06 现场复算归位；现值口径以 `AGENTS.md` §4 与 `Docs/PROJECT_CLOSEOUT_20261006.md` 为准。★ 本包 ZIP 的完整性凭据＝**同名 `.sha256` 侧车**与 `Release/distribution-ledger-r29.json`（本件为包内成员，不写本包自身哈希以免自指）。

> 适用包：`ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip`（完整性凭据＝同名 `.sha256` 侧车与 `Release/distribution-ledger-r29.json`）
> 版本 1.0.2 ｜ 生产工具 **239** ｜ 错误码 **33** ｜ 目标 ArcGIS Pro **3.0–3.5**（x64 / net6.0-windows）
>
> **版本口径（如实；G-160 三段口径）**：**支持 ArcGIS Pro 3.5（已实测）**；**3.0–3.4 编译期兼容（运行期未验证，NOT VERIFIED）**；**3.6 / 3.7 未验证**。禁止表述「支持 3.0–3.5」等无限定说法。
> 端点 `http://127.0.0.1:6520/mcp` ｜ 本包入口脚本 `scripts/one-click-setup.ps1` 携带**自签** Authenticode（签名者 `4E4ED6CBD52F7D9F73CEFC1462E78BEB380590C0`），`Get-AuthenticodeSignature` 读回 `UnknownError`＝自签根未被 Windows 信任；同事端若提示「未知发布者」，**先核对 ZIP 的 SHA-256 完全一致**，再「更多信息」→「仍要运行」；不一致则停止并回报 ｜ 干净机验收 **NOT VERIFIED**（如实披露，不包装）

---

## 1. 一句话说明

一键包 = 在已构建好的 ArcGIS Pro MCP 插件之上，加一层**图形化部署体验**：双击一个入口件，
完成「环境预检 → 包完整性校验 → 插件安装 → 客户端配置 → 连接诊断」，全程**不联网、不装依赖、不碰你的数据**。

它不改变生产工具清单、MCP 端点、Python Bridge 生命周期或任何 GIS 数据。

## 2. 前置条件（装之前确认）

| 项 | 要求 | 缺了会怎样 |
|---|---|---|
| ArcGIS Pro | **3.0–3.5（x64）** 已安装，且**当前未运行**（3.5 已实测；3.0–3.4 编译期兼容·未验证） | 预检停止并提示；安装器**不会**强制关闭 Pro |
| .NET 8 Runtime | 可用 | `COMPATIBILITY_NOT_PASS` |
| arcpy（Python） | 可导入（Pro 自带即可） | `COMPATIBILITY_NOT_PASS` |
| Windows PowerShell | 系统自带 5.1 即可 | 入口件提示缺少 PowerShell |
| 权限 | 当前用户可写自己的 Documents 目录 | 安装事务预检失败 |

> 一键包**不需要** Git、Visual Studio 或 .NET SDK，也不会自动登录任何账号、**不读取或生成** token / password / API key。

## 3. 第一步：核对完整性（强烈建议）

1. 打开命令提示符，在 ZIP 所在目录执行：

   ```bat
   certutil -hashfile ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip SHA256
   ```

2. 把输出的哈希与随包同名 `.sha256` 文件里的值逐字符比对。**不一致就不要用**。

3. **完整解压**（右键 → 全部解压缩）。**不要**在压缩包内直接双击运行。

解压后目录结构：

```
ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64\
├─ ONE-CLICK-SETUP.cmd        ← 双击这个（一键部署窗口）
├─ RECOVERY-MENU.cmd          ← 恢复窗口（回滚 / 恢复客户端 / 卸载）
├─ ROLLBACK-PLUGIN.cmd        ← 回滚最近一次安装（命令行）
├─ UNINSTALL-PLUGIN.cmd       ← 卸载插件（命令行）
├─ RESTORE-CLIENT.cmd         ← 只恢复客户端配置文件
├─ README-START-HERE.md
├─ bundle-manifest.json       ← 包内每个文件的长度与 SHA-256（不含自身）
├─ Docs\ONE_CLICK_DEPLOYMENT_USER_GUIDE.md   ← 本教程
├─ Config\  scripts\  Source\  payload\      ← 事务脚本 / 配置 / 插件载荷
```

## 4. 第二步：一键安装

1. 双击 **`ONE-CLICK-SETUP.cmd`**。
2. 在窗口里选择：

   | 选择 | 说明 |
   |---|---|
   | 客户端 | 默认 **Codex**；可选 **Cursor**、**DeepSeek Harness** |
   | Codex / Cursor | 需要指定**项目目录**（配置写到该项目的 `.codex\config.toml` / `.cursor\mcp.json`） |
   | DeepSeek Harness | 写到当前用户 `.dsh\profiles\web\cordis.patch.yml` |
   | 「仅安装插件」 | 只装插件，不动任何客户端配置 |

3. 窗口会显示最终写入目标，确认后**只需点一次**「开始一键部署」。随后自动执行：

   1. **只读环境预检**（不会改系统）
   2. **包完整性校验**（bundle-manifest + ZIP/解压内容 + 插件载荷）
   3. **一个明确的插件安装事务**（写前自动快照，失败可回滚）
   4. **每个选定客户端各自的单客户端事务**（互不牵连）
   5. **HTTP 连接诊断**（`tools/list` + 只读 `ping`）

> **没有「全部应用」按钮**：插件事务与每个客户端事务各自拥有独立的拒绝、账本、备份与恢复边界。
> 重复运行会复用已接受的安装状态（幂等）；若你手工改过配置文件，它会**先要求审查**而不是静默覆盖。

## 5. 第三步：启动 Pro 并连接

安装器**不会**替你启动 ArcGIS Pro，也不会替你点 Add-in 的 Start。

1. 打开 **ArcGIS Pro**（新建或打开一个工程）。
2. 在 **MCP 选项卡**点击 **Start**。
3. 回到一键窗口（或重新运行 `ONE-CLICK-SETUP.cmd` 的「诊断」）查看结果：

   | 层级 | 期望结果 | 含义 |
   |---|---|---|
   | Add-in | 已安装 | 插件事务成功、安装目标正确 |
   | HTTP | `tools/list = 239` | MCP 工具发现通过（按 canonical 239 去重比对） |
   | HTTP | `ping = pong` | 只读 MCP 调用通过 |
   | AI 客户端 | `NOT VERIFIED` | 尚未取得该客户端**自身新会话**的连接证据（需你手动验证，见 §6） |

> 若端口开着但响应不是有效 MCP，会显示 `PORT_OPEN_NOT_MCP` —— **不会**把「端口监听」当作连接成功。

**命令行复核（只读，不安装、不改配置）：**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-one-click-package.ps1 -RootPath . -Json
```

## 6. 客户端连接验证（每客户端独立）

对你在 §4 选过的每个客户端，**用它自己发起一次新会话**并确认：

1. 能在工具列表里看到 **239** 个工具；
2. 能成功调用至少 3 个代表性工具（建议 **1 个 Native + 1 个 GP + 1 个 Python**），例如只读的
   `get_project_info`、`get_feature_count`、`ping`；
3. 失败就**如实记录**（客户端版本 / 配置文件路径 / 报错原文），不要记成通过。

`claude-desktop` 当前为 **`APPLY_UNSUPPORTED`**（自动写入不支持）——请手工按其文档配置，或如实登记未配置。

## 7. 出错怎么办（错误码 → 动作）

| 错误码 / 现象 | 含义 | 你要做的 |
|---|---|---|
| `COMPATIBILITY_NOT_PASS` | 环境缺件或版本不符 | 按窗口提示装好 ArcGIS Pro 3.0–3.5 / .NET 6+ runtime / arcpy 后重试 |
| `ARCGIS_PRO_RUNNING` | Pro 正在运行 | **你先关闭 Pro**；安装器绝不强制关闭 |
| `PROJECT_ROOT_NOT_FOUND` | 项目目录不存在 | 重新选择一个真实存在的目录 |
| `CLIENT_RUNNING` | 目标客户端在运行 | 先关闭 Cursor 等客户端 |
| `WAITING_USER_START` | 插件没被 Start | 到 ArcGIS Pro 的 MCP 选项卡点 Start |
| `PORT_OPEN_NOT_MCP` | 6520 被别的程序占用 | 找出占用者并关闭，再重试 |
| `STALE_BACKUP_REFUSED` | 目标文件在上次事务后被手工编辑 | 先备份你当前的内容，人工审查后再决定是否恢复 |
| `BUNDLE_*` | 包缺件 / 被篡改 / 哈希不符 | 重新获取完整 ZIP 并重新核对哈希 |
| `EXISTING_DEPLOYMENT_REQUIRES_RECOVERY` | 有未收尾的失败/取消事务 | 先走恢复入口（`RECOVERY-MENU.cmd`） |

> 不要通过「改端点、删备份、删锁文件、手删安装目录」来绕错误 —— 那会破坏可回滚性。

## 8. 恢复、回滚与卸载

| 动作 | 入口 | 说明 |
|---|---|---|
| 恢复窗口 | `RECOVERY-MENU.cmd` | 图形化：回滚最近安装 / 恢复失败客户端 / 卸载 |
| 回滚最近安装 | `ROLLBACK-PLUGIN.cmd` | 读取 recovery index，用**同一账本**回滚 |
| 卸载插件 | `UNINSTALL-PLUGIN.cmd` | 用同一账本移除**精确 Add-in 目录**，保留事务证据 |
| 恢复客户端配置 | `RESTORE-CLIENT.cmd` | **只**用该客户端的备份恢复其配置文件，不重装插件 |

事务状态保存在 `%LOCALAPPDATA%\ArcGISProMCP\installer`。其中的 `recovery-index.json`、`latest-transaction.json`、
`transactions\`、`.arcgis-pro-mcp.bak(.json)` 以及未知 `.lock` **不要手工删除**。

## 9. 关于签名与分发（无公网场景）

**当前包为自签、且证书链未被系统信任**（读回 `UnknownError`）—— 安装时 Windows/ArcGIS Pro 可能提示「发布者未知」，属如实状态；**不要**把它当作已受信任的发布者凭证使用。

| 场景 | 可行性 | 说明 |
|---|---|---|
| 自用 / 同事内网分发（推荐） | ✅ 现在就能做 | U 盘或内网共享分发 ZIP；接收方按 §3 核对 SHA-256 后再解压 |
| 自签名证书签名（**本包当前姿态**） | 已执行・**链未受信** | 本包入口脚本已由自签证书签名；**本项目未授权把该证书导入任何受信任存储**（G-299 R-3 维持），因此本机或同事端提示「未知发布者」属预期状态，应对顺序＝先核对 SHA-256、再决定是否放行 |
| 商业 CA 签名（OV/EV 代码签名证书） | ⚠️ 需外网 + 实名 + 付费・**本项目未启动**（O-D123-01 维持「暂不采购」） | 证书签发与时间戳服务都需联网；且与 `AGENTS.md` §3「禁止公网暴露」口径冲突，**本路线在本项目内不可作为可行方案推荐** |
| 公网/市场公开发布 | ⚠️ 需外网 + 托管 + （平台）账号・**本项目未启动**（O-D123-02 维持「不公开」，与 `AGENTS.md` §3 逐字一致） | 需公开下载地址与发布渠道；本项目的分发路线是**内网 / U 盘 + SHA-256 校验** |

> 结论：**没有公网也能分发** —— 把 ZIP + `.sha256` 一起给接收方即可，完整性靠哈希保障（本包 `bundle-manifest.json` 还能逐文件自证）。

## 10. 证据边界（这个包能证明什么、不能证明什么）

**能证明**：包完整性与身份（bundle-manifest 逐文件哈希 + payload 与内嵌 release-manifest 自洽）、
编排步骤定义与只读预检/计划（`-Action Plan` / `-Action ValidateBundle`）、恢复与卸载的事务账本机制、
以及 HTTP 层 `tools/list=239` 与 `ping=pong`。

**不能自动证明**：所有全新电脑都能装（`cleanMachineAcceptance = NOT VERIFIED`）、
真实图形界面的每一次点击、以及 Codex / Cursor / DeepSeek 客户端**自身的连接**（必须由各客户端实测）。

## 11. 最小实机检查清单（建议打印勾选）

- [ ] ZIP SHA-256 与 `.sha256` 一致
- [ ] 完整解压（不是压缩包内直接运行）
- [ ] 双击 `ONE-CLICK-SETUP.cmd`，确认显示的插件目录与客户端目标
- [ ] 确认 Pro 版本在 3.0–3.5 内（3.5 已实测；3.0–3.4 编译期兼容·未验证）、项目目录存在
- [ ] 点一次确认，安装事务完成（无错误码）
- [ ] 打开 ArcGIS Pro → MCP 选项卡 → Start
- [ ] 窗口显示 `tools/list = 239` 且 `ping = pong`
- [ ] 各选定客户端**自身**连接并成功调用 ≥3 个代表性工具
- [ ] 需要时验证回滚 / 卸载（先关闭客户端）
- [ ] 保存脱敏日志、安装账本（ledger）、退出码与包哈希
- [ ] 不要提交或外发：`MyProject1.aprx`、测试 GDB、你的客户端配置或任何凭据
