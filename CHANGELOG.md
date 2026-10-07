# Changelog

本文件记录 **ArcGIS Pro MCP** 一键分发代际（r21 → r29）的变更要点。

- 格式参考 [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)，版本对应包内 `bundle-manifest.json` 的 `version`（当前 `1.0.2`），代际后缀 `rN` 为分发序号。
- ★**每条内容逐字取自仓库内既有台账与发布记录**（`Release/distribution-ledger-rN.json`・`Release/release-record-rN-dXXX.json`・`Release/release-acceptance-record-r23-d112.json`），不含回忆性叙述；缺项一律写「未见于台账」，不做推测补齐。
- 字节数与 SHA-256 前缀为 2026-10-08 对 `Release/` 现体复算值；发布件本身**不在 git 树内**（`.gitignore` 排除 `Release/*.zip`），现役 r29 经 **GitHub Releases** 分发。
- ★**未建 `.github/workflows` CI**：Esri ArcGIS Pro SDK（`sdk-refs/`）依其 EULA 不入库、且排除面不得放宽 ⇒ CI 内编译必然失败；该决策与合规边界另见 `README.md`「开源发布说明（AGPL-3.0）」与 Releases 页 notes。

## one-click-1.0.2-r29（2026-10-07・现役发布件）

- 发布件 `ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip`＝**2,278,077 B**・SHA-256 `099A4F3A575A65A1A8F9A7F424414465E676E605B12D8C1B9A7BA36E6F36B3CE`（全值・本席 2026-10-08 对 `Release/` 现体复算）
- 同名侧车 `ArcGIS-Pro-MCP-OneClick-1.0.2-r29-Windows-x64.zip.sha256`（116 B）・内部记录摘要与上值逐字符相符
- ★分发途径：GitHub Releases tag `v1.0.2`（两件资产＝本包与侧车）・**不入 git 树**・[Releases 页](https://github.com/cc20051222/ArcGIS-Pro-MCP-Eysia2.0/releases/tag/v1.0.2)
- 台账工单：**D-143**
- 替代上代原因（台账 `supersedes.reason` 原文）：r29 换代（种子路・D-142 三面一致性已验证后搬运发布）・非缺陷返工
- 签名事实：`Get-AuthenticodeSignature` 读回 **UnknownError**（自签・系统不信任・本代是否重签=False）
- 恒定面（台账记载）：生产工具 **239**・名册 未记载・错误码 未记载・GP 白名单 未记载

## one-click-1.0.2-r28（2026-10-06）

- 发布件 `ArcGIS-Pro-MCP-OneClick-1.0.2-r28-Windows-x64.zip`＝**2,277,454 B**・SHA-256 前 16 位 `1D0F0F03CD87876D`（本席 2026-10-08 对 `Release/` 现体复算・全值见本机同名 `.sha256` 侧车）
- 同名侧车 `ArcGIS-Pro-MCP-OneClick-1.0.2-r28-Windows-x64.zip.sha256`（116 B）・内部记录摘要与上值逐字符相符
- 台账工单：**D-134**
- 替代上代原因（台账 `supersedes.reason` 原文）：r28 换代（用户明文授权・G-323）：NOTICE.md 代际同步头段回流仓库面＋包内文档代际位归位，非缺陷返工
- 签名事实：`Get-AuthenticodeSignature` 读回 **UnknownError**（自签・系统不信任・本代是否重签=False）
- 恒定面（台账记载）：生产工具 **239**・名册 未记载・错误码 未记载・GP 白名单 未记载

## one-click-1.0.2-r27（2026-10-06）

- 发布件 `ArcGIS-Pro-MCP-OneClick-1.0.2-r27-Windows-x64.zip`＝**2,273,452 B**・SHA-256 前 16 位 `A83108120973EBB3`（本席 2026-10-08 对 `Release/` 现体复算・全值见本机同名 `.sha256` 侧车）
- 同名侧车 `ArcGIS-Pro-MCP-OneClick-1.0.2-r27-Windows-x64.zip.sha256`（116 B）・内部记录摘要与上值逐字符相符
- 台账工单：**D-131**
- 发布主题（台账 `releaseSubject` 原文）：ArcGIS Pro MCP 1.0.2 one-click r27 ＝ 239 代际分发件 ＋ **包内 GUIDE 门④返工完成**（工具数语境 80 五处归位 239・代际位四处归位 r27）；签名块与两轨 payload 与 r26／r25 逐字节同一
- 本代修复项（台账 `entryFix` 原文）：D-131（G-318 R-8 派・G-319 R-4 解锁）＝**r27 补代际批**，返工承接 D-128 门④未达成项：包内 `Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md` 的「工具数语境 80」五处（L6／L93×2／L109／L161／L174）逐处按语境归位 239，并把同件的包名・哈希核验命令・解压目录名・侧车台账名四处代际位由 r26 归位 r27；`bundle-manifest.json` 最小差分（5 行）＋`$acc…
- 替代上代（台账 `supersedes`）：{'revision': 'one-click-1.0.2-r26', 'r26ZipSha256': '4E7A82C8C769F0E9CA0A2151DB67B0BD9E4294C99A21A7D7555730DAE598EAA5', 'r26ZipBytes': 2273422, 'defect': 'r26 包内 `Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md` 仍有**五处「工具数语境 80」原文在位**（L6「生产工具 **80*…
- 逐外层成员差（台账 `deltaFromR26Measured`）：{'method': '逐外层成员 RAW-byte SHA-256 对比 r26 封存 zip 解包件（种子＝f1 逐件解包・Z3 全等）', 'changedMemberCount': 3, 'unchangedMemberCount': 23, 'contentMembers': ['Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md', 'scripts/verify-one-click-package.ps1'], 'plus': 'bundle-manifest.json（自身重刷・manifest 自件除外规则不变）', 'detail': [{'pa…
- 门④返工（台账 `gate4Rectification`）：{'ticket': 'D-128', 'gate': '完成门④', 'ruling': 'G-318 R-7 ／ G-319 R-4（D-128 改判 CLOSED-WITH-EXCEPTION）', 'spansFixed': 10, 'linePositions': [6, 93, 109, 161, 174], 'occurrences80': {'before': 6, 'after': 0}, 'occurrences239': {'before': 1, 'after': 7}}
- 恒定面（台账记载）：生产工具 **239**・名册 285・错误码 33・GP 白名单 53
- cleanMachineAcceptance（台账）：**NOT VERIFIED**
- fourScenarioAcceptance（台账）：**NOT EXECUTED IN D-131（工单硬边界：不安装不启宿主）**
- net6RuntimeAcceptance（台账）：**NOT VERIFIED**

## one-click-1.0.2-r26（2026-10-06）

- 发布件 `ArcGIS-Pro-MCP-OneClick-1.0.2-r26-Windows-x64.zip`＝**2,273,422 B**・SHA-256 前 16 位 `4E7A82C8C769F0E9`（本席 2026-10-08 对 `Release/` 现体复算・全值见本机同名 `.sha256` 侧车）
- 同名侧车 `ArcGIS-Pro-MCP-OneClick-1.0.2-r26-Windows-x64.zip.sha256`（116 B）・内部记录摘要与上值逐字符相符
- 台账工单：**D-128**
- 发布主题（台账 `releaseSubject` 原文）：ArcGIS Pro MCP 1.0.2 one-click r26 ＝ 239 代际分发件 ＋ **包内三件文档随代际归位**（工具数・适用包・签名事实三处失真已闭合）；签名块与 payload 与 r25 逐字节同一
- 本代修复项（台账 `entryFix` 原文）：D-128（G-309 R-4 预登记・G-312 R-1 用户明文「解锁全面修复」激活・G-316 续作）：r26 ＝ r25 同双 payload 基线（net8 ≡ 现役装机 239 逐字节・net6 沿 r25 现件），外层仅**包内文档随代际归位**：README-START-HERE／Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE／NOTICE ＋ 包外 bundle-manifest／`$acceptedDeploymentVersio…
- 替代上代（台账 `supersedes`）：{'revision': 'one-click-1.0.2-r25', 'r25ZipSha256': '7969C220886F4D085C6746AE9EE57FE27C1031698515C59718FDA68CA53FAD29', 'defect': 'r25 包内三件文档的**工具数・适用包・签名事实**三处陈述与现役相反（149／80 工具／154 工具・「适用包 r6」・「本包未签名」），且包内 `Docs/ONE_CLICK_DEPLOYMENT_USER_G…
- 逐外层成员差（台账 `deltaFromR25Measured`）：{'method': '逐外层成员 RAW-byte SHA-256 对比 r25 封存 zip 解包件（种子＝f1 逐件解包、Z3 全等）', 'changedMemberCount': 5, 'unchangedMemberCount': 21, 'contentMembers': ['README-START-HERE.md', 'Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md', 'NOTICE.md', 'scripts/verify-one-click-package.ps1'], 'plus': 'bundle-manifest.json（自身重刷…
- 恒定面（台账记载）：生产工具 **239**・名册 285・错误码 33・GP 白名单 53
- cleanMachineAcceptance（台账）：**NOT VERIFIED**
- fourScenarioAcceptance（台账）：**NOT EXECUTED IN D-128（工单硬边界：不安装不启宿主）**
- net6RuntimeAcceptance（台账）：**NOT VERIFIED**

## one-click-1.0.2-r25（2026-10-05）

- 发布件 `ArcGIS-Pro-MCP-OneClick-1.0.2-r25-Windows-x64.zip`＝**2,270,984 B**・SHA-256 前 16 位 `7969C220886F4D08`（本席 2026-10-08 对 `Release/` 现体复算・全值见本机同名 `.sha256` 侧车）
- 同名侧车 `ArcGIS-Pro-MCP-OneClick-1.0.2-r25-Windows-x64.zip.sha256`（117 B）・内部记录摘要与上值逐字符相符
- 台账工单：**D-124**
- 发布主题（台账 `releaseSubject` 原文）：ArcGIS Pro MCP 1.0.2 one-click r25 ＝ 239 代际分发件 ＋ 外层部署入口脚本自签 Authenticode（签名已写入・系统不信任，逐字标注）
- 本代修复项（台账 `entryFix` 原文）：D-124 (G-299 R-3 采 A 路径): r25 = r24 同 payload 基线（net8 ≡ 现役装机 239 逐字节・net6 沿 r24 现件）＋外层部署入口脚本 scripts/one-click-setup.ps1 施加 Authenticode 签名（自签证书，D-123 建、本批复用不重生成）＋该件在 bundle-manifest 内哈希刷新＋$acceptedDeploymentVersions 补 r25
- 替代上代（台账 `supersedes`）：{'revision': 'one-click-1.0.2-r24', 'defect': 'r24 未含任何 Authenticode 签名（发布记录 digitalSignature=NOT EXECUTED (AGENTS §6)）；且 r24 包内 Config/client-catalog.json 携 D-120 修复前的代际字面量 normalization.productionToolCount=154（r25 随包改为 239）', 'defectScope…
- 逐外层成员差（台账 `deltaFromR24Measured`）：{'method': '逐外层成员 RAW-byte SHA-256(16) 对比 r24 封存 zip 解包件', 'changedMemberCount': 4, 'unchangedMemberCount': 22, 'detail': [{'path': 'Config/client-catalog.json', 'r24': {'bytes': 2246, 'sha256_16': '73106108942D0414'}, 'r25': {'bytes': 2246, 'sha256_16': '068772E356E3027E'}, 'reason': 'D-120 装机代际字面量…
- 恒定面（台账记载）：生产工具 **239**・名册 285・错误码 33・GP 白名单 53
- cleanMachineAcceptance（台账）：**NOT VERIFIED**
- fourScenarioAcceptance（台账）：**NOT EXECUTED IN D-124 (工单硬边界：不安装不启宿主)**
- net6RuntimeAcceptance（台账）：**NOT VERIFIED**

## one-click-1.0.2-r24（2026-10-05）

- 发布件 `ArcGIS-Pro-MCP-OneClick-1.0.2-r24-Windows-x64.zip`＝**2,269,257 B**・SHA-256 前 16 位 `539B732878B6DC75`（本席 2026-10-08 对 `Release/` 现体复算・全值见本机同名 `.sha256` 侧车）
- 同名侧车 `ArcGIS-Pro-MCP-OneClick-1.0.2-r24-Windows-x64.zip.sha256`（117 B）・内部记录摘要与上值逐字符相符
- 台账工单：**D-121**
- 发布主题（台账 `releaseSubject` 原文）：ArcGIS Pro MCP 1.0.2 one-click r24 (239-generation payload distribution artefact, O-D119-01 closure)
- 本代修复项（台账 `entryFix` 原文）：D-121 (G-294 R-3): 239-generation distribution payload after D-119 install; net6 layer rebuilt to the accepted 43-entry form (bin seed was stale r21-era 20-entry after rebuild, Deployment/* reused from sealed r21 net6 payload byte-verified)…
- 替代上代（台账 `supersedes`）：{'revision': 'one-click-1.0.2-r23', 'defect': 'r23 carries the 224-generation payload (net8 A2D6A0D0/1,036,008); installed carrier is now the 239 generation', 'defectScope': 'payload generation only; no package-form defect claimed against r…
- 恒定面（台账记载）：生产工具 **239**・名册 285・错误码 33・GP 白名单 53
- cleanMachineAcceptance（台账）：**NOT VERIFIED**
- fourScenarioAcceptance（台账）：**NOT EXECUTED IN D-121 (ticket 硬边界：不安装)**
- net6RuntimeAcceptance（台账）：**NOT VERIFIED**

## one-click-1.0.2-r23（2026-10-01）

- 发布件 `ArcGIS-Pro-MCP-OneClick-1.0.2-r23-Windows-x64.zip`＝**2,114,330 B**・SHA-256 前 16 位 `796D1956B06BBE04`（本席 2026-10-08 对 `Release/` 现体复算・全值见本机同名 `.sha256` 侧车）
- 同名侧车 `ArcGIS-Pro-MCP-OneClick-1.0.2-r23-Windows-x64.zip.sha256`（117 B）・内部记录摘要与上值逐字符相符
- 台账工单：**D-112**
- 发布主题（台账 `releaseSubject` 原文）：ArcGIS Pro MCP 1.0.2 one-click r23 (FINAL release suite, G-RELEASE terminal ring)
- 本代修复项（台账 `entryFix` 原文）：D-111 (G-243 R-1): Config.daml is extracted from the payload selected by host version (net6/net8)
- 替代上代（台账 `supersedes`）：{'revision': 'r22', 'zipSha256': '2F63B5BAB6BFF8EF20C2A80ECFFF9B4AC08C16DF7113D787563862EACB9ED2CC', 'originalsRetainedReadOnly': True, 'defect': 'G-243 R-1: one-click entry pinned -ConfigPath to the outer net6-era Config.daml, so the compa…
- 逐外层成员差（台账 `changedFromR22`）：['Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md', 'NOTICE.md', 'README-START-HERE.md', 'bundle-manifest.json', 'scripts/one-click-setup.ps1', 'scripts/verify-one-click-package.ps1']
- 恒定面（台账记载）：生产工具 **224**・名册 285・错误码 33・GP 白名单 53
- cleanMachineAcceptance（台账）：**NOT VERIFIED**
- fourScenarioAcceptance（台账）：**NOT VERIFIED**
- net6RuntimeAcceptance（台账）：**NOT VERIFIED**

## one-click-1.0.2-r22（2026-10-01）

- 发布件 `ArcGIS-Pro-MCP-OneClick-1.0.2-r22-Windows-x64.zip`＝**2,112,591 B**・SHA-256 前 16 位 `2F63B5BAB6BFF8EF`（本席 2026-10-08 对 `Release/` 现体复算・全值见本机同名 `.sha256` 侧车）
- 同名侧车 `ArcGIS-Pro-MCP-OneClick-1.0.2-r22-Windows-x64.zip.sha256`（117 B）・内部记录摘要与上值逐字符相符
- 恒定面（台账记载）：生产工具 **224**・名册 未记载・错误码 未记载・GP 白名单 未记载
- cleanMachineAcceptance（台账）：**NOT VERIFIED**
- fourScenarioAcceptance（台账）：**NOT VERIFIED**

## one-click-1.0.2-r21（日期未见于台账）

- 发布件 `ArcGIS-Pro-MCP-OneClick-1.0.2-r21-Windows-x64.zip`＝**1,615,423 B**・SHA-256 前 16 位 `C74A790C1E8629AA`（本席 2026-10-08 对 `Release/` 现体复算・全值见本机同名 `.sha256` 侧车）
- 同名侧车 `ArcGIS-Pro-MCP-OneClick-1.0.2-r21-Windows-x64.zip.sha256`（116 B）・内部记录摘要与上值逐字符相符
- ★r21 无独立 `distribution-ledger`／`release-record` 在库：字节与摘要取自 `release-acceptance-record-r23-d112.json` 的 `versionChain`（1,615,423 B／`C74A790C…`），日期取自本机同名 `.sha256` 侧车文件时间 **2026-09-27 21:04**，本条不补写主题叙述
