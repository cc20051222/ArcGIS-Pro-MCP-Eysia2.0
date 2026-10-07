# Pro 3.0–3.5 兼容方案书 **v3**（Keeper 制定）

- **制定**：Keeper ｜ **版本**：v1 = G-158（09-19 00:0x）→ **v2 = G-159（09-19 00:2x，SDK 自获取实证）** → **v3 = G-160（09-19 08:2x，用户裁定「批准，选 c 方案」）**
- **v2 依据**：用户指令「SDK 程序集（13.0–13.2）需要兼容 3.0 到 3.5，程序集自己搞定，你只需要制定方案」＋「**都由你制定方案和验收，然后执行者执行，你们要自己解决 SDK 的问题**」
- **v3 依据**：用户指令「**批准，选 c 方案**」⇒ ① 聚合类（B 项）批准，工具数 78→80；② **运行期机器路径 = (c) 不验证** ⇒ 3.0–3.4 一律 NOT VERIFIED，支持范围按 §7 **三段口径**如实表述，**API 差集扫描升为硬判据**（补偿措施）
- **定位**：**方案 = Keeper 产物；执行 = 执行者；验收 = Keeper**；本件为 D-060 A 项执行依据
- **目标**：单包覆盖 ArcGIS Pro **3.0–3.5**（运行期实测 3.5；3.0–3.4 编译期兼容·未验证）

---

## 0 · v2 修订摘要（相对 v1）

| # | 项 | v1（G-158） | **v2（G-159，已实证）** |
|---|---|---|---|
| 1 | **SDK 来源** | 「从 Pro 3.0–3.2 机器复制」＋**需用户提供机器** | **nuget.org 官方包 `Esri.ArcGISPro.Extensions30` —— Keeper 已实测下载并解剖成功**（3.0 + 3.5 两包）⇒ **零外部依赖、零用户提供** |
| 2 | **硬阻塞 CS1705** | 缺 13.0–13.2 引用集 ⇒ 无法编译 | **已解除** —— 引用集可自获取（渠道见 §2） |
| 3 | **职责** | SDK 获取 = 执行侧/用户 | **SDK 获取 = 执行侧自解决**（渠道、命令、校验、合规均已固化于 §2） |
| 4 | **验证分层** | 六版本真机（混在一起） | **编译期 / 运行期显式分层**（§6）：编译期 100% 自解决；运行期边界如实单列 |
| 5 | **新增技术手段** | — | **API 差集扫描**（§6.2）：以 13.0 为契约基线、13.5 为对照，自动检出「3.0 有而 3.5 无 / 3.5 新增」的 API 差异 —— 无需 Pro 3.0 即可得静态兼容性证据 |
| 6 | **v3（G-160）运行期裁定** | 待用户裁定 (a)/(b)/(c) | **= (c) 不验证**：运行期实测仅 3.5；3.0–3.4 标 NOT VERIFIED；**差集升为硬判据**；**支持范围三段口径**（§7） |
| 7 | **v3（G-160）B 项** | 聚合类待批准 | **已批准**：`CellStatistics`/`FocalStatistics` 实现，工具数 **78 → 80** |

---

## 1 · 技术事实（实测基线，2026-09-19 Keeper 亲测）

| 事实 | 内容 | 证据 |
|---|---|---|
| **.NET 代际（实测）** | 3.0–3.2 SDK 包 TFM = **`net6.0-windows7.0`**；3.5 SDK 包 TFM = **`net8.0-windows7.0`** | nupkg 内 `ref/` 目录实测 |
| **加载规则** | net8 宿主可加载 net6 程序集，反之不行；且「Add-in 目标版本 > 宿主版本 ⇒ 不加载」⇒ **最小基准 = net6 + desktopVersion = 3.0** | 官方文档 + spike |
| **程序集版本映射（实测）** | 本机 Pro 3.5.0.57366 → `ArcGIS.Core.dll`/`Framework`/`Desktop.Core` 的 FileVersion = **13.5.0.57366** ⇒ **SDK 程序集版本 = `13.<Pro 次版本>`**（3.0→13.0 / 3.1→13.1 / 3.2→13.2 / 3.5→13.5） | `Get-Item .VersionInfo` 实测 |
| **引用方式** | 加载项项目对 6 个程序集用 `<Reference>` + `HintPath=$(ArcGISProBin)…`（**非 NuGet**）⇒ 替换引用集 = 换路径/供副本，**引用机制不变** | csproj + `Directory.Build.props` |
| **本机现状** | 仅 Pro **3.5.0.57366**（net8 世代）；无 Pro 3.0–3.4；无虚拟化平台；RAM 15.6 GB（空闲 4.4）/ D 盘剩余 80.4 GB | host-probe |
| **CS1705 成因** | 3.5 的 `ArcGIS.Desktop.Framework 13.5.0.0` 引用 `System.Runtime 8.0.0.0` > net6 的 6.0.0.0 ⇒ 必须用 net6 代（13.0–13.2）引用集编译 | spike |

---

## 2 · ★ SDK 自获取路径（v2 核心；渠道已实证）

### 2.1 渠道结论：nuget.org 官方包（唯一必要渠道）

- **包 ID**：`Esri.ArcGISPro.Extensions30`（作者 Esri Inc.）
- **实测版本清单（10 个，完整覆盖目标）**：
  `3.0.0.36056` ／ `3.1.0.41833` ／ `3.2.0.49743` ／ `3.3.0.52636` ／ `3.3.3.52636` ／ `3.4.0.55405` ／ `3.4.1.55405` ／ `3.5.0.57366` ／ `3.6.0.59527` ／ `3.7.0.1901`
- **nuspec 原文（决定性证据）**：
  > description: “ArcGIS Pro Extensions30 NuGet contains the ArcGIS Pro extension assemblies needed to **compile your Add-ins and Configurations for ArcGIS Pro versions 3.0 and higher**.”
  > `<group targetFramework="net6.0-windows7.0" />`（3.0.0.36056）
- **直链模板（无需 NuGet 客户端、无需 Esri 账号）**：
  - 包：`https://api.nuget.org/v3-flatcontainer/esri.arcgispro.extensions30/<版本>/esri.arcgispro.extensions30.<版本>.nupkg`
  - 元数据：同路径 `esri.arcgispro.extensions30.nuspec`
- **已排除的渠道（实测，防止走冤枉路）**：
  - ❌ `proapp-sdk-templates.vsix`（3.0）：实测 3,899 条目 / 37 dll **全为 VS 向导与模板依赖，无任何 `ArcGIS.Desktop.*` 程序集**
  - ❌ GitHub Releases 的 `nupkg` 资产：**仅 3.3+ 才有**（3.0/3.1/3.2 无）
  - ❌ 旧世代包 `Esri.ArcGISPro.Extensions`（2.3–2.9）：仅适用 2.x，与本目标无关

### 2.2 落地流程（执行侧实施，5 步）

| 步 | 动作 | 判据 |
|---|---|---|
| S1 | **下载**：3.0.0.36056（**必需，编译基准**）＋ 3.5.0.57366（**推荐，API 对照**）；如做 3.1/3.2 独有验证再补对应包 | nupkg 落 `sdk-refs/_pkg/`（**项目内 D 盘**，G-138；**不得使用 C 盘 NuGet 全局缓存**——如用 `nuget install` 必须设 `NUGET_PACKAGES` 指向项目内） |
| S2 | **解包**：nupkg 即 zip；取 **`ref/net6.0-windows7.0/`（3.0–3.2）** 或 `ref/net8.0-windows7.0/`（3.3+）下程序集 | 落 `sdk-refs/pro-13.0-net6/`（3.0 世代）、`sdk-refs/pro-13.5-net8/`（3.5 世代，**仅供 API 差集对照，不参与编译**） |
| S3 | **校验**：三层（见 2.3） | 任一不符 ⇒ 构建/流程立即失败 |
| S4 | **接线**：`Directory.Build.props` 增 `ArcGISProSdkRefs`（TFM 条件切换）＋ csproj 6 处 `HintPath` 改指变量 | 编译通过（0 警 0 错），引用来源可打印 |
| S5 | **记录**：`sdk-refs/SOURCE.md` | 包 ID/版本/nupkg SHA256/下载 URL/时间/EULA 归属/程序集清单及 SHA256 |

### 2.3 校验规则（三层，缺一不可）

1. **包级**：nupkg SHA256 与 SOURCE.md 记录一致（实测参考：3.0.0.36056 = `2A36F5DB…`；3.5.0.57366 = `204D6760…`）
2. **文件级**：每个程序集 SHA256 记录在案；**版本断言**——3.0 引用集内程序集 AssemblyVersion/FileVersion 必须落在 **`13.0.*`**（3.1/3.2 类比），越界即失败
3. **身份级**：`AssemblyName.GetPublicKeyToken()` 为 Esri 签名（与 r6 现用 13.5 程序集公钥一致）；不一致 ⇒ 停止并上报

### 2.4 合规（强制，随包 EULA）

- 包内 `EULA.txt` 且 nuspec `requireLicenseAcceptance="true"` ⇒ **接受 Esri EULA 后方可使用**；
- **程序集用途限定 = 编译期引用**：`sdk-refs/` **不得进入一键包 / r7 发布物 / 任何对外分发物**（`.gitignore` 或发布脚本排除清单明确）；对外仅分发自己的 Add-in 产物（既有规则不变）
- 许可归属在 `SOURCE.md` 与 `Notice`（如发布需）中保留 Esri 版权声明

### 2.5 已完成的实证资产（执行侧可直接复用）

Keeper 已实测下载并解剖，缓存在 **`.runtime/evolution/phase15/sdk-probe/`**（D 盘项目内）：

| 文件 | 大小 | SHA256（前 16） | 用途 |
|---|---|---|---|
| `ext30-3.0.0.36056.nupkg` | 21,447,686 B | `2A36F5DBDD62857E` | **编译基准引用集来源** |
| `ext30-3.5.0.57366.nupkg` | 54,328,619 B | `204D67601334CF11` | API 差集对照 |
| `nupkg-probe-report.json` | 解剖报告 | — | 程序集清单/大小/许可文件 |
| `sdk-availability-probe.json` | release 资产实测 | — | 渠道排除证据 |
| `proapp-sdk-templates-3.0.0.36056.vsix` | 9,280,974 B | `2A28FBEA69CA61F6` | 排除证据（不含 ArcGIS.Desktop 程序集） |

> 执行侧 S1 可「校验哈希后直接复用」，无需重复下载；如需 3.1/3.2 补充包按 §2.1 模板自取。

---

## 3 · 主方案 A1：单包 net6 覆盖 3.0–3.5

> 备选 B（net6+net8 双包）、C（仅扩 3.3–3.5）**均不采为主线**：B 收益有限、C 不满足 3.0 需求。**主线 = A1**。

### 3.1 引用集与构建接线

1. **引用集（项目内 vendored，离线可复现）**：`sdk-refs/pro-13.0-net6/` —— 由 §2 自获取，目录结构**保留 `Extensions\<子系统>\` 层次**（与 csproj HintPath 形态一致）。
2. **构建期切换（改 2 处）**：
   ```xml
   <ArcGISProSdkRefs Condition="'$(TargetFramework)'=='net6.0-windows'">$(MSBuildThisFileDirectory)sdk-refs\pro-13.0-net6\</ArcGISProSdkRefs>
   <ArcGISProSdkRefs Condition="'$(TargetFramework)'=='net8.0-windows'">$(ArcGISProBin)</ArcGISProSdkRefs>
   ```
   csproj 的 6 个 `HintPath` → `$(ArcGISProSdkRefs)…`
3. **前置校验（防静默降级）**：引用集缺失 / 版本越界 / SHA 不符 ⇒ **构建立即失败**（exit ≠ 0 + 缺失清单）。
4. **体积**：仅 4 个 Extensions 子目录 + 必要 `ArcGIS.*.dll`；不复制整 bin。

### 3.2 代码侧改造（五项链，spike 已量化）

| # | 项 | 动作 |
|---|---|---|
| 1 | 目标框架 | 加载项项目 `net8.0-windows → net6.0-windows`（主推单目标；共享库按需 `net6;net8` 双目标） |
| 2 | 语言版本 | 显式 `<LangVersion>`（14 例 CS8936） |
| 3 | 运行时特性 | `required` polyfill（1 文件，CS0656） |
| 4 | API 改写 | `Stream.FlushAsync(CancellationToken)` **2 处**（net8-only，CS1501） |
| 5 | 引用集 | §3.1 |

### 3.3 身份链与检测策略（6 处）

| 对象 | 现值 | 改为 |
|---|---|---|
| `Config.daml` | `desktopVersion="3.5.0"` | **`3.0`** |
| `Config/compatibility-policy.json` | `net8.0-windows` / `supportedSeries:["3.5"]` / `requiredDesktopVersion:"3.5.0"` | `net6.0-windows` / **`["3.0"…"3.5"]`** / `"3.0"` |
| **检测判定（版本矩阵）** | 「只认 3.5」 | ① 宿主 3.0–3.5 且 Add-in 目标 ≤ 宿主 ⇒ PASS；② 宿主未知 ⇒ NOT_VERIFIED；③ 宿主 <3.0 或 >3.6 ⇒ UNSUPPORTED；④ net6 包在任意宿主可加载（不另设框架判据，net8 判据保留给旧包） |
| release-manifest / 部署清单 | net8 / 3.5.0 | 同步 net6 / 3.0 |
| 一键包构建脚本 | 固定 3.5 校验 | 按新身份取值（仍校验 payload 哈希） |
| 教程 / README | 「适用 3.5」 | 「适用 **3.0–3.5**（3.6/3.7 未实测）」+ §9 如实化规则 |

---

## 4 · API 面差异处置（3.0 vs 3.5）

- 版本敏感调用点清单（spike 已备）：`FeatureLayer`(23)、`MapView`(8)、`CreateLayout`(8)、`ExecuteToolAsync`(2)、`GeometryEngine`、`CreateMapFrame`。
- **编译期天然护栏**：以 13.0 引用集编译 ⇒ 使用 3.1+ 新增 API 会**编译失败**（定位明确），这是首选防线。
- **反向风险（必须查）**：3.5 若**移除了** 3.0 尚存的 API ⇒ 编译通过但运行期抛错 ⇒ 由 **API 差集扫描（§5.1 / §8，硬判据）** 覆盖。
- 处置规则：若存在版本敏感 API ⇒ 该工具改**条件可用**（运行时探测宿主版本，低版本返回「该工具需 Pro ≥ x.y」显式拒绝，**复用现有错误码，零新增**）；若 API 面一致 ⇒ 无此路径。

---

## 5 · 验证矩阵（**编译期 / 运行期分层**）

### 5.1 编译期（**100% 自解决，无外部依赖**）

| 项 | 判据 |
|---|---|
| 引用集齐备 | `sdk-refs/pro-13.0-net6/`（+ 3.1/3.2 如做）程序集在位 + 三层校验通过 |
| net6 编译 | 0 警 0 错；产出 net6 单包（新哈希） |
| **API 差集扫描** | `13.0 → 13.5` 差集报告：**removed API = 0**（或逐项给出条件可用处置） |
| 身份链 | 6 处字段级核对 + 检测矩阵单测 **≥5**（3.5 verified PASS ／ 3.0–3.4 not-verified PASS+警告 ／ 2.9 UNSUPPORTED ／ 未知 NOT_VERIFIED） |
| 打包 | net6 候选包 deterministic（双打同哈希）+ 一键包 r7 链 PASS（官方验证器） |

### 5.2 运行期（需各版本 Pro 本体 —— **唯一剩余外部边界，如实分层**）

| Pro 版本 | 本机可得性 | 验证内容（每版本独立记录，State A/B 口径） |
|---|---|---|
| **3.5** | ✅ **本机已有** | 加载 + `tools/list`=78 + 代表调用（Native/GP/Python）+ 守卫 + 回归 |
| 3.0 / 3.1 / 3.2 / 3.3 / 3.4 | ❌ 需另装 | 同上 |

**★ 运行期路径已裁定 = (c) 不验证（G-160，用户指令「批准，选 c 方案」，09-19 08:2x）**：

- **运行期实测仅 3.5（本机）**；**3.0–3.4 一律 NOT VERIFIED**；**不再投入**虚机 / Pro 官方试用版 / 用户侧机器（原 (a)/(b) 路径**关闭**）。
- **★ 补偿措施（强制）**：**API 差集扫描升为硬判据** —— `13.0 → 13.5` 的 **removed API 必须 = 0**（有则逐项「条件可用」处置）。理由：放弃运行期验证后，差集是**唯一的跨版本安全证据**。
- **支持范围表述按 §7 三层口径**（3.5 已实测支持／3.0–3.4 编译期兼容·运行期未验证／范围外不支持）。
- 判据口径随之更新：**不再以「六版本全绿」为目标**；改为「**编译期判据全绿 + 3.5 运行期实测 + 三段口径如实表述**」。

---

## 6 · 发布与回退

| 项 | 规则 |
|---|---|
| r6 | **保持封存（SEALED）**，3.3–3.5 使用者可继续用；不因本改造改动 |
| 新包 | **r7**（net6 单包；覆盖 3.5 已实测 ＋ 3.0–3.4 编译期兼容·如实标注）；**版本号 1.0.2 → 1.0.3 待用户批准**（未批则维持 1.0.2 + r7 区分） |
| 发布前置 | 编译期判据全绿 ＋ **运行期 3.5 实测** ＋ **三段口径如实表述（G-160）**；一键包链重跑；教程/README 更新 |
| **回退** | 任一步失败 ⇒ **保持 r6 为交付主线**；net6 分支留档（结论 + 失败点）；**不半途出包** |
| 风险提示 | **改造 + 验证完成前，不得把 net6 包发给任何使用者（含 3.5 用户）** |

---

## 7 · 支持范围表述规则（如实化，强制）

**★ 三层口径（G-160 落定；策略文件 + 文档 + 检测输出三处一致）**：

| 层级 | 版本 | 检测行为 | 唯一允许的对外表述 |
|---|---|---|---|
| **verified** | **3.5** | PASS | 「**已实测支持**」 |
| **not-verified** | **3.0 / 3.1 / 3.2 / 3.3 / 3.4** | PASS ＋ **警告附注** | 「**编译期兼容；运行期未验证（NOT VERIFIED）**」 |
| unsupported | < 3.0 ／ > 3.6 | UNSUPPORTED（3.6/3.7 亦未实测 ⇒ NOT VERIFIED） | 「不支持／未验证」 |

1. 策略文件字段：`supportedSeries: ["3.0"…"3.5"]` ＋ **`verifiedSeries: ["3.5"]`** ＋ **`notVerifiedSeries: ["3.0","3.1","3.2","3.3","3.4"]`**；检测矩阵单测须覆盖 **verified PASS／not-verified PASS+警告／范围外 UNSUPPORTED／未知 NOT_VERIFIED**（**≥5 例**）。
2. **禁止表述（表述审计红线）**：**不得**写「支持 3.0–3.5」「兼容 3.0–3.5」而不加限定；**正确写法 = 「支持 3.5（已实测）；3.0–3.4 编译期兼容（运行期未验证）」**。
3. 3.6 / 3.7：net6 包**预期**可被 .NET 8/10 宿主加载 —— **未实测 ⇒ NOT VERIFIED**（禁「理论兼容／应该可以」类表述）。
4. 一键包/教程/README 中的版本范围**必须与验证证据逐条对齐**（Keeper 表述审计项）。

---

## 8 · 交付物与判据（并入 D-060 A 项）

| 交付物 | 判据 |
|---|---|
| `sdk-refs/pro-13.0-net6/` + `SOURCE.md` | 三层校验通过（包/文件/身份）；版本区间 13.0.* |
| 代码侧改造（五项链） | net6 编译 0 警 0 错；spike 复现（共享库 net6 全通） |
| **★ API 差集报告（硬判据）** | `13.0 → 13.5` **removed API = 0**（有则逐项「条件可用」处置）—— 放弃运行期验证后的**唯一跨版本安全证据**（G-160） |
| 身份链/策略（6 处） | 字段级核对 + 检测矩阵单测 **≥5**（verified ／ not-verified+警告 ／ UNSUPPORTED ／ 未知） |
| 验证矩阵 | **编译期全绿 ＋ 运行期 3.5 实测** ＋ 三段口径如实表述（G-160） |
| 打包 | net6 候选包（新哈希）+ r7 一键包链 PASS |
| **B 项（已批准，G-160）** | 聚合类 78→80：单测 ≥8/工具 + 守卫接入 + 错误码 33 零新增 + 快照变更披露 + LIVE 代表判据 |

**职责划分（按用户口径）**
- **执行者**：SDK 自获取与落地（§2）、改造实施、编译期证据、运行期验证组织（按 §5.2 裁定路径）
- **Keeper**：本方案维护、**阶段验收**、支持范围表述审计、封存/发布裁定、判定器与判据维护

---

*配套：`Docs/PRO_VERSION_COMPATIBILITY_ASSESSMENT.md`（执行侧评估）／`outbox/CR-D058-S2_scope-pro-version-compat.md`（范围变更申请）／`inbox/D-060_phase15-features.md`（A-修订 2 ＋ **G-160 裁定**）／实证缓存 `.runtime/evolution/phase15/sdk-probe/`。*
