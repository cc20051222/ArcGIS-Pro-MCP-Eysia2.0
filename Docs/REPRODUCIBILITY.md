# 发布包可重现性声明（Reproducibility of the one-click release bundle）

> 编制：L1 实现线执行席・派单 **D-133**（G-322 裁定采（乙））・生成 `2026-10-06 22:25:35`・全部数字为现场复算值
> 本件为**事实与机制声明**，不构成任何放行：真实安装／干净机验收／真实客户端连接＝**NOT VERIFIED**（AGENTS §7）。

## 1. 结论

**现役发布包不是仓库源面的逐字节函数。** 包内两类字节属**发布环节产物**，仓库源面不承载：

1. `scripts/one-click-setup.ps1` 尾部 **33 行 Authenticode 签名块**（`# SIG # Begin signature block` 起）——发布件已自签、仓库源件未签；
2. **文档代际同步头段**：包内 `NOTICE.md` 与包内 `README-START-HERE.md` 各带一段代际同步说明（分别在仓库面短 911 B／665 B）。

因此：**不得**把「从仓库源面重建 ≡ 现役包」当作既定事实；重建一致性判定改由构建器的 **fail-closed 可重现性守卫**承担（§3）。
**禁止的处置方式**：把签名块回流仓库面（会造成「仓库源件携带一次性签名块」反模式）・以裸抑制跳过比对・把包内文档当作仓库面自动派生。

## 2. 现役代际事实（现场复算）

| 项 | 现值 |
|---|---|
| 一键包 | `ArcGIS-Pro-MCP-OneClick-1.0.2-r27-Windows-x64.zip`・**2,273,452** 字节／SHA256 `A83108120973EBB34B9349F82F72C523…`（全串 `A83108120973EBB34B9349F82F72C523E4EC0BB3CD58E7D4EB45B37509F5F38D`） |
| 外层条目 | **26**（含双 payload）・包内 `bundle-manifest.json` 列 **25** 条并自件排除 |
| 参照台账件 | `Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r27-Windows-x64.zip.manifest.json`・8,127 字节／`A6FA626EC45846B9…`・deploymentVersion `one-click-1.0.2-r27` |
| net8 payload | 1,103,697 字节／`59F376555025052D…` ≡ 现役装机位（逐字节同一）・装机工具数 **239** |
| net6 payload | 1,107,215 字节／`ADBF848C4ABC1BCB…` |
| 恒定面 | 注册 239 ≡ 契约 239・名册 94/33/158＝285・错误码 33・白名单 53 件／30,634 B |

## 3. 构建器 fail-closed 守卫（本单加入）

`scripts/build-one-click-package.ps1` 在**任何包输出落盘之前**执行两道比对，任一未解释差异即 `throw` 中止（不产出 zip、不写台账、不留 staging）：

| 道 | 比对 | 中止码 | 豁免 |
|---|---|---|---|
| (i) 取件完整性 | 仓库源件 ↔ 暂存成员（按取件数组∪改名映射逐名配对） | `BUNDLE_TAKE_DRIFT\|成员名\|source=…\|staged=…`（缺文件＝`BUNDLE_STAGED_MEMBER_MISSING`） | **永不豁免** |
| (ii) 可重现性 | 暂存成员 ↔ **参照包面**（缺省取 `Release/` 内最高代 `*.zip.manifest.json`；显式 `-ReproducibilityManifestPath` 可覆盖） | `BUNDLE_REPRO_MISMATCH\|成员名\|staged=…\|reference=…` | **恰 3 条白名单**・逐条附理由 |

白名单（与实测差异逐名相符・禁裸抑制・无跳过开关）：

* `NOTICE.md` — generation-sync header exists only in the published copy (D-128); the repo face carries the AGENTS-grounded release note, so a rebuild legitimately differs here (G-322 option B)
* `scripts/one-click-setup.ps1` — the 33-line Authenticode signature block is a release-stage artefact; backflowing it into the repo source would embed a one-time signature in source (G-322 rejected option A)
* `README-START-HERE.md` — release-stage generation-sync header (665 B) is added during packaging; the repo source is semantically restored but shorter (D-132 T-132-3)

守卫还会把**未用到的白名单条目**回报进产出 JSON 的 `reproGuard.whitelistUnneeded`（防豁免随时间失效成为静默抑制）。

## 4. 重算配方（从 r27 zip 起）

```powershell
# 0) 只读取种子：从现役 zip 取 26 外层条目与包内成员哈希（禁止改写发布面）
$zip = 'Release\ArcGIS-Pro-MCP-OneClick-1.0.2-r27-Windows-x64.zip'
$ref = 'Release\ArcGIS-Pro-MCP-OneClick-1.0.2-r27-Windows-x64.zip.manifest.json'
# 1) 前置：双载荷已构建于 Source\ArcGISProMCP.Compatibility\bin\x64\Debug\{net6.0-windows,net8.0-windows}
#    （未构建则构建器 PACKAGE_NOT_FOUND 中止——属正确 fail-closed，禁以旧 zip 冒充）
# 2) 重建到暂存目录（禁指向 Release/；本声明件的举证一律用沙箱副本）
& scripts\build-one-click-package.ps1 -Configuration Debug -OutputDirectory <run 根>/f4-sandbox-proof/out -CandidateRevision r27
# 3) 期望：守卫通过・zip 产出・包内 verifier PASS；若报 BUNDLE_REPRO_MISMATCH 则为**未解释差异**，须停等报指挥席
# 4) 三项预期差异（非缺陷）：NOTICE.md／scripts/one-click-setup.ps1／README-START-HERE.md 的字节差见下表
```

| 差异成员 | 仓库源件（字节／SHA256 前 16） | 包内成员（字节／SHA256 前 16） |
|---|---|---|
| `NOTICE.md` | 5,546／`263AD22EB2B31C3F…` | 6,457／`7092CCF33FE88F55…` |
| `README-START-HERE.md` | 2,481／`015000BC4767444E…` | 3,146／`F22D3776BF604CB8…` |
| `scripts/one-click-setup.ps1` | 119,230／`87C89C595554A920…` | 121,312／`1312CF39C452E954…` |

## 5. 边界

* 本件**不提出任何信任链操作建议**（承 G-299 R-3）：不新增证书（O-D123-01 维持）・不对外发布（O-D123-02 维持）・不改动信任根配置。
* 签名读回事实：`Get-AuthenticodeSignature` 的 `Status` 为 **`UnknownError`**・指纹 `4E4ED6CBD52F7D9F73CEFC1462E78BEB380590C0`（链终止于不受信根）。本件不声称该状态等同受信，亦**不得**被改写为受信结论。
* 真机四场景（首装／升级／回滚／卸载）・干净机部署・真实客户端连接＝**NOT EXECUTED／NOT VERIFIED**。

## 6. 已知残留（本单写权面之外・登记待裁）

* `README.md` 除本单归位的现役代际行外，另有历史行仍带旧代际引用（L10／L57／L60／L63 类）⇒ 属 D-133 写权之外，登记 **T-133-1**；
* 仓库 `NOTICE.md` 的代际头段与包内副本相差 911 B（三差异之一）⇒ 登记 **T-133-2**；
* 两者一律**不静默修**，等指挥席派单或裁定。

