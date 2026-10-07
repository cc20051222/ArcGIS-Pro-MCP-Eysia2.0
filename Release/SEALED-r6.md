# SEALED · ArcGIS Pro MCP 一键部署包 r6（封存记录）

- **封存时间**：2026-09-19T16:37:51
- **封存依据**：D-060 工单 C 项（G-157，用户指令「封存现在的一键包，继续完善功能」）
- **状态**：**SEALED（只读，不再改动）** —— 此后任何改进一律新版本（r7 ／ 版本号 1.0.3 待批准）

## 1 · 身份四元组（实测复核）

| 项 | 值 |
|---|---|
| 包哈希 | `E30F68680BFF3D3230A834ADD493A6F49128588061FA2060CB1277C8FB24B6D6` |
| 包字节 | 564,416 B |
| 包内条目 | 23（验证器计数） |
| payload（内嵌 .esriAddInX） | `AD8C58D9F535EBF5BA2C1E87F78074B09024F6788E058B5B56953968E37FC524` ／ 499,943 B |

- 声明值文件：`Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r6-Windows-x64.zip.sha256` ⇒ 与实测包哈希**一致**（✓）。
- 官方验证器（`scripts/verify-one-click-package.ps1`）结论：**PASS**（复跑于 09-18 21:5x / 09-19 二次复跑；证据 `oneclick-verify.json`）。

## 2 · 适用范围（如实登记 · 三段口径）

| 层级 | 版本 | 检测行为 | 对外表述（唯一允许用语） |
|---|---|---|---|
| **verified** | **3.5** | PASS | 「已实测支持」 |
| **not-verified** | — | — | r6 为 **net8 单包**，不含 not-verified 层 |
| unsupported | < 3.0 ／ ≥ 3.3 未实测 | UNSUPPORTED（3.3/3.4）／未验证 | 「不支持／未验证」 |

> **r6 的准确口径 = 「支持 ArcGIS Pro 3.5（已实测）；3.3/3.4 未实测；3.0–3.2 不支持（宿主仅 .NET 6）」**
> 禁止表述：「支持 3.0–3.5」「兼容 3.0–3.5」（无实测依据）。

## 3 · 已知限制清单（随封存一并固定）

1. **Pro 3.0 / 3.1 / 3.2 不支持** —— r6 为 `net8.0-windows`，而 3.0–3.2 宿主仅 .NET 6 ⇒ 加载项不会被加载；安装器依赖检测会主动拦住（`COMPATIBILITY_NOT_PASS`，安全行为）。
2. **Pro 3.3 / 3.4 / 3.6 / 3.7 未实测** —— 3.3/3.4 与 3.5 同代（.NET 8）但未在真机验证；3.6/3.7 未实测。**一律 NOT VERIFIED。**
3. **干净机实装未验证** —— D-058 阶段二（clean-machine）为 BLOCKED-PENDING-USER，r6 上线时未执行。
4. **Codex CLI / DeepSeek Harness 连接级未验证** —— 本机无可执行 CLI（D-059 如实登记，不包装）。
5. **包未签名** —— Windows 可能提示「未知发布者」；完整性依靠 SHA-256 ＋ 包内 `bundle-manifest.json` 逐文件自证。
6. **r6 不含本批（D-060）改进** —— net6 版本兼容改造、聚合类工具（78→80）均在 r7 及以后版本，r6 工具数固定 = 78。

## 4 · 只读处置（三处）

| # | 路径 | 属性 |
|---|---|---|
| 1 | `Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r6-Windows-x64.zip` | `+R` |
| 2 | `Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r6-Windows-x64.zip.sha256` | `+R` |
| 3 | `Release/发送给同事/`（7 个文件） | `+R`（文件夹 + 逐文件） |

> 说明：`attrib +R` 对文件夹不递归 ⇒ 已对 `发送给同事/` 内**逐个文件**显式设置，确保封存件真正只可读。
> r7 及后续版本**另建新目录/新文件名**，不复用 r6 路径。

---
*本记录由执行会话依 D-060 C 项生成；证据 `.runtime/evolution/phase15/run-20260919-d060/seal-r6.json`。*

## 5 · 只读校验实测（判据修正后）

- 方法：**写入探测**（`open(...,'ab')` 尝试追加）+ `attrib` 属性解析（初版用 `os.access(W_OK)` 判目录只读 ⇒ 判据缺陷，已修正并留档）。
- 结果：`ArcGIS-Pro-MCP-OneClick-1.0.2-r6-Windows-x64.zip` 与 `.sha256` ⇒ **PermissionError 拒写** ✓；`发送给同事/` 内 **7 个文件全部 `R`** 且拒写 ✓。
- 目录级：`+R` 已设；Windows 目录属性**不阻止**新增文件（语义如此，非缺陷）—— 保护以「逐文件 R」达成，r7 另建新目录不受影响。
- 结论：**C 项 PASS**（三处置只读确证）。
