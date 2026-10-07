# ArcGIS Pro MCP Server 用户指南

> **历史时点声明（D-126 项目完结批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **2026-09-07 代际（文内无 ISO 日期；时点由正文「Phase 7／7.8 正式接受」表述推定）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。

本指南描述当前仓库的用户入口和证据边界。Phase 7 overall、Phase 7.8 Final Handoff 和 1.0.2 canonical 30-tool scope 已 `FORMALLY ACCEPTED / PASS / COMPLETE`；新增 one-click deployment package 是独立的 `PASS CANDIDATE / AWAITING INDEPENDENT GATE KEEPER`，不等同于 clean-machine 或真实客户端 acceptance。

## 1. 支持范围与前置条件

正式支持目标是 Windows x64 + ArcGIS Pro 3.5 当前 host。需要：

- ArcGIS Pro 3.5 及其 `arcgispro-py3` 环境；
- 二进制分享包用户：.NET 8 runtime 和系统自带 Windows PowerShell 5.1，不需要 .NET SDK；
- 源码构建者：.NET 8 SDK；PowerShell 7 可选；
- server / namespace：`arcgis-pro-mcp`；
- 本机回环端点可用：`http://127.0.0.1:6520/mcp`。

其他 ArcGIS Pro 版本、远程公网部署和 clean-machine 安装仍为 `NOT VERIFIED`，不要从版本号推断支持。已接受的 installed runtime/UI 和三类 required-client evidence 仅限 Phase 7.7 记录的当前主机范围。

## 2. 推荐入口

### 普通用户：二进制分享包

接收者不需要源码或开发环境。完整解压发布者提供的 ZIP 后，关闭 ArcGIS Pro，双击 `INSTALL-PLUGIN.cmd`；安装完成后打开 ArcGIS Pro，在 MCP 选项卡点击 `Start`，再双击 `CONFIGURE-CODEX.cmd`。Cursor 和 DeepSeek Harness 可从 `START-HERE.cmd` 菜单单独配置。

安装器自动确定当前用户 Add-ins 目录和 owned transaction ledger，但在写入前仍显示精确目标并要求确认。卸载/回滚复用最近一次成功安装的 ledger；客户端配置仍一次只处理一个。完整分发说明见 [分享与简易安装方案](SHARING_AND_SIMPLE_INSTALL.md)。clean-machine support 仍为 `NOT VERIFIED`。

### 一键部署分享版（candidate）

完整解压 `ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip` 后双击 `ONE-CLICK-SETUP.cmd`。它先执行只读预检，再以独立事务处理插件和所选客户端，等待用户启动 ArcGIS Pro 的 MCP `Start` 后诊断 loopback `tools/list=30` 与 `ping`。不会自动安装依赖、启动/强制关闭程序或使用 `ApplyAll`。r1 的 Gate Keeper rejection 与 r2/r3/r4 不同 revision/hash 均保留作历史记录；r5 的 Windows PowerShell 5.1 入口、自动 GUI callback、real cancel callback、child-completion hard-interruption recovery 与 recovery-index evidence 已通过，但 real install、真实 GUI click-through、clean-machine 和客户端自身连接仍是 `NOT VERIFIED`。详细步骤见 [一键部署用户指南](ONE_CLICK_DEPLOYMENT_USER_GUIDE.md)。

### 开发者：仓库工作流

所有命令从用户实际克隆或解压的仓库根目录执行。先将当前目录切换到该根目录，再使用只读默认入口：

```powershell
Set-Location '<your-clone-or-extract-root>'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Plan
```

它返回安全 ledger，列出支持动作、客户端优先级和边界；不会写入文件、创建备份、生成安装目标或启动进程。

只读检查当前客户端配置：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Validate
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Validate -Client codex
```

## 3. Build 与发布包

```powershell
. .\scripts\dev-env.ps1
dotnet build .\ArcGIS-Pro-MCP.sln --no-restore --nologo
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Package -Configuration Debug
```

`Package` 只生成 `.esriAddInX` 和 release manifest，并强制向 `package-addin.ps1` 传递 `-SkipRegistration`。它不安装、不注册、不启动 ArcGIS Pro。包内容必须由 manifest 和独立 ZIP 检查共同确认。

## 4. 安装、卸载和回滚

实际安装动作只应在明确授权、已完成 Build/Package/兼容性检查并选择 owned 安装目标后执行。先做事务预演：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 `
  -Action Install -DryRun `
  -PackagePath <owned-package.esriAddInX> `
  -ManifestPath <owned-package.release-manifest.json> `
  -InstallRoot <owned-install-root> `
  -TransactionRoot <owned-transaction-root> `
  -LedgerPath <owned-transaction-root>\ledger.json
```

正式 `Install`、`Uninstall`、`Rollback` 都只委托给 `scripts/release-transaction.ps1`，并由它写出事务 ledger。对应入口分别是：每个 mutation 都必须提供同一个显式 `-LedgerPath`，wrapper 会读取并脱敏汇总该 ledger。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Install ...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Uninstall ...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action Rollback ...
```

省略号代表同一组已审查的 `PackagePath`、`ManifestPath`、`InstallRoot`、`TransactionRoot` 和 `LedgerPath` 参数；不要改用宽泛删除或手工删除目录。测试注入和 compatibility facts 属于底层测试/内部控制，不属于用户入口。Phase 7.7 的真实安装、runtime/UI 和 client gates 已由独立 Gate Keeper 接受；本 Phase 7.8 只整理交接文档，不重新执行这些动作。

## 5. 客户端配置

客户端目录来自 [`Config/client-catalog.json`](../Config/client-catalog.json)：

| 优先级 | 客户端 | 范围 | 位置/模板 |
|---|---|---|---|
| P0 | Codex | required、project TOML | `.codex/config.toml` |
| P1 | Cursor | required、project JSON | `.cursor/mcp.json` |
| P1 | DeepSeek Harness | required、external user profile YAML patch | `.dsh\profiles\web\cordis.patch.yml` |
| P2 | Claude Desktop | optional、template-only | platform-specific user JSON |

端点固定为 `http://127.0.0.1:6520/mcp`，canonical production tools 为 `30`；`mcp_auth` 是客户端 helper，不计入生产工具数。

### Apply / Restore 安全规则

Apply 或 Restore 必须显式且只指定一个客户端。示例：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action ClientApply -Client codex
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\user-workflow.ps1 -Action ClientRestore -Client codex
```

不要使用未定义的 `ApplyAll`，不要把安装和客户端配置合并为一个命令。configurator 负责受管块、备份、原子替换、恢复和凭据字段拒绝；wrapper 不复制这些写入逻辑。Phase 7.7 已分别正式接受 Codex P0、Cursor P1 和 DeepSeek Harness P1 fresh client evidence；Claude Desktop 仍为 template-only。Apply/Restore 仍需显式授权且一次只处理一个客户端。

## 6. ArcGIS Pro 状态、健康和 Diagnostics

在 ArcGIS Pro 内，`MCP` 选项卡提供 Start、Stop、Status、Run Tests 和 Diagnostics 按钮。默认服务契约仍是 loopback `127.0.0.1:6520/mcp`。

- `Status` 显示 Server、endpoint、ArcGIS Pro Host、configuration、compatibility、Bridge、logging 和客户端事实的安全摘要。
- `Run Tests` 通过现有 Tool Router 运行内置自测；它不是客户端连接证明。
- `Diagnostics` 导出允许列表内的 `health-snapshot.json`、`managed-log.jsonl` 和 `manifest.json`，不应包含凭据、GIS 数据或自由格式异常文本。
- managed logs 的用户目录由运行时的 LocalApplicationData 策略决定；不要手工清理未知日志或锁文件。

上述 installed UI/runtime、managed-log/export 和 required-client fresh observations 已在 Phase 7.7 的专项报告中记录并正式接受；clean-machine support、HTTP cancellation 和未列入 client gate 的能力仍保持 `NOT VERIFIED` 或相应限制分类。

## 7. 离线、网络和凭据

MCP 默认只访问本机回环地址，不自动开放公网。客户端配置模板不得包含 token、password、secret、apiKey、authorization、header、login、provider 或 model。离线情况下可以做源代码 Build、package audit、Plan 和 Validate；不能把离线校验当作 ArcGIS Pro runtime 或客户端连接 PASS。

如果 6520 没有 listener，先在 ArcGIS Pro 中确认 Add-in 和 Start 状态，再使用 Status/Diagnostics；不要修改 endpoint 到公网地址，也不要在命令行打印凭据或完整异常文本。

## 8. 故障与恢复

1. 先运行 `Plan`，确认动作和目标边界。
2. 再运行 `Validate`，确认 catalog、endpoint 和配置语法。
3. 若是安装事务失败，保留 ledger，先运行 release transaction 的只读 `Preflight`，再按 ledger 指示使用 `Rollback`；不要手工删除未知目录。
4. 若是客户端配置失败，停止客户端后使用同一个 client 的 `Validate` 和 `ClientRestore`；如果检测到 stale backup，先审查用户编辑，不要使用强制恢复绕过审查。
5. 若是 runtime/Bridge failure，保留安全 Diagnostics 导出并检查 Status；不要把历史 `.runtime` 日志改写成当前 PASS。

失败 ledger 会区分已完成步骤、失败步骤和未启动步骤。若 transaction ledger 已验证自动 rollback 成功，recovery action 为 `NONE`，但仍应先审查原始失败；若 rollback 失败或状态未验证，则 recovery action 会分别要求 `Rollback` 或只读 `Preflight`。ledger 只报告固定 `errorCode`、allowlisted 状态和恢复提示，不应显示 token、密码、绝对路径、hash、原始异常或 child script 的原始输出。

## 9. 当前限制

- `list_maps`/`get_map_info` 为 `PARTIAL`；`get_dataset_info`/`get_raster_info` 为 `LIMITED IMPLEMENTATION`。
- ArcGIS Pro 3.5 的 `ArcGISProject.isDirty` 不可用，但属于 non-blocking limitation。
- HTTP client-disconnect cancellation 为 `NOT VERIFIED`；7 个历史 HTTP transport tests 仍为 `BLOCKED_BY_HARNESS`。
- 当前 `select_layer` schema 只有 `mapName` 和 `layerName`，没有确定性 OID 参数；不宣称 selection mutation PASS。
- 当前主机 directory reparse 行为相关用例为 capability-aware `SKIPPED`，不冒充 PASS。
- accepted installed release is `1.0.2` exact match；historical `1.0.1` remains only the failed-run rollback baseline and is not a normal downgrade target。

## 10. 发布检查清单

- [ ] 读取当前权威 Docs，确认允许的 Phase 和安全边界。
- [ ] Build 通过，记录 warning 与 error 分类。
- [ ] Package 使用 `-SkipRegistration`，manifest 与 ZIP 内容一致。
- [ ] Plan/Validate 通过，生产工具数仍为 `30`，端点仍为 loopback。
- [ ] 如需 mutation，使用 owned target、显式动作、ledger 和可恢复路径。
- [ ] 不修改受保护 APRX/GDB，不删除 historical outputs 或未知 locks。
- [ ] Fresh evidence、历史 evidence、`NOT VERIFIED` 和 `BLOCKED_BY_HARNESS` 分开记录。
- [ ] 保持 `NOT VERIFIED`、`BLOCKED_BY_HARNESS`、PARTIAL/LIMITED 和 client-surface limitations 的原始分类。
- [ ] Phase 7 overall 与项目最终接受已为 `FORMALLY ACCEPTED / PASS / COMPLETE`；one-click deployment candidate 在 Independent Gate Keeper 复核前保持 `PASS CANDIDATE`，不把本地包证据写成该 candidate 的 formal acceptance。
