# NOTICE · 版权与许可声明（ArcGIS Pro MCP 2.0）

> **代际同步说明（D-128 r26 重制批・2026-10-06 加入；D-131 r27・D-134 r28・D-143 r29 随代际更新）**：本文随包分发文本此前停留在更早代际（工具数・适用包・签名状态三处与现役相反），本轮按 2026-10-06 现场复算归位；现值口径以 `AGENTS.md` §4 与 `Docs/PROJECT_CLOSEOUT_20261006.md` 为准。★ 本包 ZIP 的完整性凭据＝**同名 `.sha256` 侧车**与 `Release/distribution-ledger-r29.json`（本件为包内成员，不写本包自身哈希以免自指）。

> 版本：r29（双代）· 2026-10-06（D-128 起随代际同步刷新・D-131 r27・D-134 r28・D-143 r29；上一代 r28 台账见 `Release/distribution-ledger-r28.json`）｜ 随分发物提供（本文件为**技术与事实层面**声明，不构成法律意见）

## 1 · 本项目

- **名称（技术）**：ArcGIS Pro MCP 2.0（MCP server for ArcGIS Pro）
- **产物（双代）**：`ArcGISProMCP.Compatibility.esriAddInX`（**239** 工具 / 错误码 **33** / GP 白名单 **53**——D-126 现算，权威口径见 `AGENTS.md` §4 与 `Docs/PROJECT_CLOSEOUT_20261006.md`）
  - **net6 变体**（`payload/net6/`）：`net6.0-windows`，SHA256 **`ADBF848C4ABC1BCB…`/1,107,215**（r29 包内逐字节实测＝自 r25 现件零改动搬运・r26／r27／r28／r29 逐字节沿用），服务 ArcGIS Pro **3.0–3.5**（3.5 已实测；3.0–3.4 编译期兼容·运行期未验证 NOT VERIFIED；3.6+ 未验证）；
  - **net8 变体**（`payload/net8/`）：`net8.0-windows`，SHA256 **`59F376555025052D…`/1,103,697**（**≡ 现役 239 装机位**——r25 bundle manifest `sourceArtifactSha256` 与安装位实测哈希逐字节同一、r26／r27／r28／r29 逐字节沿用），服务 ArcGIS Pro **3.3–3.6**（3.5 已实测 D-068/D-070/D-079 LIVE；3.3/3.4/3.6 编译期兼容·运行期未验证 NOT VERIFIED；3.0–3.2 不支持 UNSUPPORTED）；r21 代际的内容改动逐项清单（D-077／D-079 等）以三账与完结报告为史册，本行只声明载荷哈希身份；
  - 一键安装包 `ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip`（**本件为包内成员，不写本包自身哈希以免自指**；完整性凭据＝同名 `.sha256` 侧车与 `Release/distribution-ledger-r29.json`；双 payload 自动按宿主 Pro 版本选择）。上一代 r28＝2,277,454 字节／SHA256 `1D0F0F03CD87876D…`，逐字节封存零改动。
- **签名事实（r25 起、r26／r27／r28／r29 沿用）**：包内 `scripts/one-click-setup.ps1`（121,312 字节／`1312CF39C452…`；r25 的唯一签名对象，r26／r27／r28／r29 **均未重新签名、该件字节与签名块不变**）已由 `Set-AuthenticodeSignature` 施加**自签** Authenticode（`CN=ArcGIS-Pro-MCP SelfSigned`，签名者指纹 `4E4ED6CBD52F7D9F…`）；`Get-AuthenticodeSignature` 读回 **`UnknownError`**——「A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider」⇒ **操作系统不信任该证书链，状态**从未达到受信任**（建立受信需改动受信证书存储＝本项目未授权动作，G-299 R-3 维持）。ZIP 容器本身不带签名；包内其余件亦不施加签名。证据：`Release/release-record-r29-d143.json`＋`Release/distribution-ledger-r29.json`（上一代 r28 两件同目录封存、逐字节零改）。
- **版权**：© 2026 本项目所有者。**保留所有权利（All rights reserved）**——除另行书面授权外，不得再分发、转售或用于未授权环境。向授权对象（同事/内部）分发时，请连同本 NOTICE 与本包使用教程一并提供。

## 2 · 与同名外部项目的关系（重要事实声明 · 按 G-189 更正令修订）

GitHub 上的近名项目 `Knight60/ArcGIS-Pro-MCP` 是**独立项目**（**非本仓上游、亦非本仓 fork**；2026-09-27 经其仓库元数据复核：`fork=false`、无 `parent`/`source`），许可为 **AGPL-3.0**。本仓与其**不存在文件级代码继承**：

- 三个对照时点（其初始发布 / v1.0 前重构 / v1.0 tag）对本仓迁移清单 **327 件**逐文件 SHA256：路径重叠 ≤ 2，**哈希同一 = 0**；
- 其特有标识符在本仓源码/脚本/配置中 **0 命中**；
- 架构与安全姿态不同：其自述提供「每个 geoprocessing 工具 + 任意 arcpy」；本仓为 **多工程 net6/net8 双代 + 受控 GP 白名单（53，fail-closed）+ 禁任意 Python 旁路 + 破坏性操作 `confirm` 缺省拒 + 受保护输出根与只读模式**。

⇒ 其许可（AGPL-3.0）**义务对本仓不触发**（本仓自有代码独立撰写）；本声明仅在**同名混淆/商标**层面作事实区分，不代表任何授权或合作关系。**任何后续借鉴（含语义/命名对照）须先就 AGPL 边界取得书面授权。**

> 口径更正说明（G-189／O-D080-01·02）：本文此前使用「上游」措辞，且本仓其它文档曾出现「竞品零覆盖／fork 同源」类表述——均已更正为「独立项目／差异在**安全姿态与实现方式**」。其 112 命名工具集中**包含** `diagnose` 与 `run_batch`（2026-09-27 直连复核），故不得以「有无该能力」作为差异化依据。其测试件数量、Pro 版本门槛史、GP 工具总量（自述约 2,000）**本仓未复算**，一律不得作为结论依据。

## 3 · 第三方组件

| 组件 | 许可/条款 | 说明 |
|---|---|---|
| ArcGIS Pro SDK for Microsoft .NET | **Esri 许可条款（EULA）** | 本仓仅引用 SDK（`sdk-refs/`，见其 `EULA-Esri.txt` 与 `SOURCE.md`）；ArcGIS Pro 本体需自行合法授权 |
| Microsoft .NET 6 / .NET 8 运行时 | MIT / Microsoft 条款 | 宿主运行时（net6 变体 .NET 6+；net8 变体 .NET 8+，Pro 3.3+ 宿主自带） |
| 其他 NuGet/开源依赖 | 各自许可 | 见各包内声明 |

## 4 · 使用与风险

- 本产物为**本地自动化工具**（MCP 端点默认 `127.0.0.1:6520`，仅本机），**不得暴露公网**；
- 受控 GP 白名单、破坏性操作 `confirm` 缺省拒、只读模式与路径守卫等安全机制**不得绕过**；
- 对数据的最终影响由使用者负责；建议先在副本工程上验证。

---
*本文件随 r29（双代）分发物口径刷新（D-128 起・D-131 r27・D-134 r28・D-143 r29・D-145 包内归位・2026-10-07）。★ 代际同步闭合：r25 及更早包内封存的 `NOTICE.md` 副本停留在打包时（r21 文本）的 4,178 字节件（`9778DA1BCE071B6A`）与仓库现值不同文——该差异在 r21–r25 封存件中**逐字节保留不改**；自 r26 起包内副本与本仓现值口径**随代际同步**（R-G312-P1 根因闭合）・r28 起该同步由 D-134 回流仓库面达成（仓库 ≡ 包内逐字节）。★ **r29 包内 `NOTICE.md` 副本自 D-145（2026-10-07）起与本仓现值口径同代**（R-G312-P1 于 D-145 清偿）・包内与本仓在代际同步头段（L3）与本尾注（L44）两行按 G-322 option B 允许不同文。
