# 分享与简易安装方案

> **历史时点声明（D-129 README 链接陈旧面补声明批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **r5 代际（文内无 ISO 日期；时点由正文「当前另有一个独立的 `ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip` candidate」与「身份四元组」条（含 80 工具／33 错误码・G-153 裁定）两处表述推定）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。
> ★ 本头部点名的当时点位（由本批扫描件机械实测，非人工摘录；现值见上列两件）：`-r5`・`80 工具`

## 目标用户体验

接收者不需要克隆仓库、安装 Git/Visual Studio/.NET SDK，也不需要手写安装路径或 transaction ledger；仍需 Windows x64、ArcGIS Pro **3.0–3.5**（**3.5 已实测；3.0–3.4 编译期兼容·运行期未验证**）、**.NET 运行时 ≥ 6** 和系统自带 Windows PowerShell 5.1。发布者只分享 `Release/ArcGIS-Pro-MCP-<version>-Windows-x64.zip` 及其 `.sha256` 文件。

当前另有一个独立的 `ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip` candidate，提供 GUI/CLI 一键编排、包清单审计和 recovery-index 恢复入口。它复用已接受的 1.0.2 payload，不改变下方既有两步安装方案；r1 的 Gate Keeper rejection 与 r2/r3/r4 不同 revision/hash 保留为历史证据，r5 的真实安装、clean-machine、真实用户 GUI click-through 和真实客户端连接仍为 `NOT VERIFIED`。详见 [ONE_CLICK_DEPLOYMENT_USER_GUIDE.md](ONE_CLICK_DEPLOYMENT_USER_GUIDE.md) 与 [ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md](ONE_CLICK_DEPLOYMENT_CANDIDATE_REPORT.md)。

推荐路径保持为两个明确动作：

1. 双击 `INSTALL-PLUGIN.cmd` 安装 ArcGIS Pro Add-in；
2. 打开 ArcGIS Pro 并启动 MCP 后，双击 `CONFIGURE-CODEX.cmd` 配置首选客户端。

Cursor 和 DeepSeek Harness 从 `START-HERE.cmd` 菜单选择，或使用菜单式向导 `CONFIGURE-CLIENT.cmd`（codex / cursor / deepseek-harness / claude-desktop；**逐客户端独立事务**：Plan 预览 → 确认 → Apply → Validate）。安装与客户端配置不静默合并；客户端仍一次只处理一个，不存在 `ApplyAll`。

## 现役身份（如实登记）

- 包：`ArcGISProMCP.Compatibility.esriAddInX`，**491,452 B**，SHA-256 `24CA2B4B…`（完整值见 release manifest）。
- 身份四元组：`1.0.2` + **80 工具** + **33 错误码** + 包哈希（G-153 裁定）；版本号升级（1.0.3）为待批准项。
- 回滚链：`.runtime/evolution/phase08/backup-pre-*` 逐代保留（最近一级 `backup-pre-d057` = `9bbd8847…`）。

## 发布者生成分享包

当前 accepted 1.0.2 artifact 位于 Debug 产物目录，因此保持其既有 hash 构建分发 ZIP：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-share-package.ps1 -Configuration Debug
```

若输出已存在，审查后显式添加 `-Force`。构建器只复制和校验现有 `.esriAddInX`、release manifest、必要安全脚本、配置模板与说明，不编译、不注册插件、不修改客户端配置。输出为：

- `Release/ArcGIS-Pro-MCP-1.0.2-Windows-x64.zip`
- `Release/ArcGIS-Pro-MCP-1.0.2-Windows-x64.zip.sha256`

## 接收者安装

1. 校验 ZIP SHA-256（可选但推荐）。
2. 完整解压 ZIP。
3. 关闭 ArcGIS Pro。
4. 运行 `INSTALL-PLUGIN.cmd`，审查安装目标和 ledger，输入 `Y`。
5. 打开 ArcGIS Pro，在 MCP 选项卡点击 `Start`。
6. 运行 `CONFIGURE-CODEX.cmd`，选择 Codex 项目目录。
7. 重启 Codex，在目标项目中检查 `tools/list` 和 `ping`。

生产连接契约固定为 `arcgis-pro-mcp`、`http://127.0.0.1:6520/mcp`、**80 个生产工具**（冻结清单口径，见 `_gatekeeper/FUNCTION_SCOPE_FREEZE_D056.md`）；`mcp_auth` 是客户端 helper，不计入 78。错误码数 **33**（冻结期零新增）。

## 安全与恢复

- 插件安装目标固定为当前用户 Documents 下的 ArcGIS Pro Add-ins GUID 目录。
- 安装器把可恢复事务保存在 `%LOCALAPPDATA%\ArcGISProMCP\installer\transactions`。
- 卸载和回滚必须读取最近一次成功安装的同一 ledger；缺失时拒绝宽泛删除。
- 客户端写入继续使用原子替换、基线备份和 stale-edit 拒绝；检测到用户后续编辑时返回 `STALE_BACKUP_REFUSED`，不会强制覆盖。
- 不打开或修改 `MyProject1.aprx`、`TestDate/Phase4Test.gdb` 或任何用户 GIS 数据。
- 打包、安装、客户端配置和 runtime/`ping` 是不同证据层；不能用 ZIP 构建 PASS 代替新电脑 runtime PASS。

## 分发前检查清单

- [ ] `.esriAddInX` size/hash 与 release manifest 完全一致。
- [ ] ZIP 及 `.sha256` 已重新生成并一起发布。
- [ ] 在隔离的普通用户账户或全新电脑完成安装、启动、`tools/list`、`ping`、卸载与回滚验收。
- [ ] 保持未执行的 clean-machine 验收为 `NOT VERIFIED`。
- [ ] 确认软件许可证、第三方组件声明和公开分发权限。
- [ ] 若面向更广泛用户，完成发布包/脚本签名并提供可信下载渠道。
- [ ] 不在分享包中放入 token、密码、个人配置备份、APRX/GDB 或历史 runtime 目录。
