# ArcGIS Pro 版本兼容性评估与方案（支持 3.0 / 3.1 的可行性）

> 触发：用户要求「3.1、3.0 这些版本也要兼容」
> 结论摘要：**当前包（net8.0-windows / desktopVersion=3.5.0）在 Pro 3.0–3.2 上"根本不会加载"**，
> 不是"部分功能不好用"。要覆盖 3.0–3.2 必须改为 **.NET 6 构建**（并有配套工程与验证工作）。
> 本文属**产品面/契约面范围变更**，需 Keeper 派单 + 用户批准后方可动产品码。

---

## 1 · 权威事实（ESRI 官方文档，2026-09-18 核实）

| 事项 | 事实 | 来源 |
|---|---|---|
| Pro 3.0–3.2 | 需要 **.NET 6**（开发侧 .NET 6.0.5+） | ProGuide NET 10 Upgrade（表格）/ SDK 需求页 |
| Pro 3.3–3.6 | 需要 **.NET 8** | 同上 |
| Pro 3.7 | 需要 **.NET 10** | 同上 |
| 向前兼容规则 | **「Add-ins deployed on ArcGIS Pro 3.0–3.2 will run on 3.3 and newer 3.x without re-compilation」**；社区确认 **net8 可加载 net6 程序集，反之不行** | ProGuide NET 8 Upgrade；Esri Community 帖 |
| 反向限制 | 一旦改成 net8 重新编译，**就失去对 3.3 以前版本的兼容** | ProGuide NET 8 Upgrade（"once you rebuild under a new Target Framework you lose backward compatibility"） |
| 附带坑 | Pro 3.3 起 `RegisterAddIn.exe` 位置有变化（旧版打包/脚本可能找不到） | Esri Community 帖（用户实测） |

**推论（决定性）**：要**同时**支持 3.0–3.2 与 3.3–3.6，只有两条路 ——
① 用 **net6** 编译（一份包通吃 3.0–3.6），或 ② 出**两个包**（net6 包 + net8 包）。

## 2 · 当前工程现状（实测）

| 位置 | 现值 | 3.0/3.1 兼容性影响 |
|---|---|---|
| `Source/ArcGISProMCP.Compatibility/ArcGISProMCP.Compatibility.csproj` | `<TargetFramework>net8.0-windows</TargetFramework>` | ❌ 3.0–3.2 宿主只有 .NET 6 运行时 ⇒ **无法加载** |
| `Config.daml` → `AddInInfo desktopVersion` | `3.5.0` | ❌ Pro 3.0 不会加载"目标 3.5"的加载项 |
| SDK 程序集引用 | `$(ArcGISProBin)ArcGIS.Core.dll`（**13.5.x**，本机 3.5 SDK） | ⚠️ 编译期 API 以 13.5 为准；3.0 的 13.0.x 未必有同名同签名 API |
| `Config/compatibility-policy.json` | `supportedSeries:["3.5"]`、`requiredSdkAssemblyProductVersionPrefix:"13.5."`、`otherVersions` 仅列 3.3/3.4/3.6/3.7+ = NOT_TESTED | ❌ **3.0/3.1/3.2 连条目都没有**；检测脚本会把非 3.5 判为 UNSUPPORTED 并**中止安装** |
| `scripts/check-compatibility.ps1` | 硬判：系列 == 3.5；SDK 版本前缀 13.5.；**arcpy == 3.5**；**Python 3.11** | ❌ 每一条都会拦下 3.0/3.1（其 Python/arcpy 版本不同） |
| Python Bridge（`bridge_runner.py`） | 随包分发，用 Pro 自带 arcpy | ⚠️ 需确认语法/依赖能在 3.0–3.2 的 Python 上跑（老版本 Python 语法限制） |
| 一键包 / 部署清单 / release-manifest | 身份写死 `target 3.5.0` / net8.0-windows | ⚠️ 全部需要随目标版本同步 |

## 3 · 差距清单（要真正兼容 3.0/3.1 必须做的 7 件事）

1. **构建目标**：改 `net6.0-windows`（或做双包）；确认 SDK 8 工具链能编 net6。
2. **SDK 引用**：需要有 **Pro 3.0–3.2 的 SDK 程序集**（`ArcGIS.Core.dll` / `ArcGIS.Desktop.Framework.dll`，13.0.x–13.2.x）
   才能以"最低版本"为基准编译 —— **本机只有 3.5**，这是首要外部依赖。
3. **代码 API 面复核**：把 78 个工具用到的 Pro SDK API 逐个对照 3.0 API 面；3.3+ 新增的 API 必须移除或加版本分支。
4. **语言/运行时特性**：net6 下不可用的 C# 新特性（如 `required` 成员等需要 .NET 7+ 运行时类型的）要改写；
   禁用 net7/net8-only API。
5. **`desktopVersion` 与身份链**：`Config.daml` 目标改 `3.0`（最低支持版本），并同步
   部署清单 / release-manifest / 兼容性策略 / 检测脚本 / 一键包 bundle-manifest 的 target 字段。
6. **检测策略**：`compatibility-policy.json` + `check-compatibility.ps1` 改为**版本矩阵**（3.0–3.6 各自要求
   的 SDK 前缀 / arcpy / Python 版本），而不是"只认 3.5"。
7. **Python Bridge**：确认 `bridge_runner.py` 在 3.0–3.2 的 Python 版本上可运行（语法 + 标准库 + arcpy 差异）。

## 4 · 验证的现实约束（必须如实说）

* 本项目**所有 LIVE 证据（78 工具、GP、布局导出、AssemblyCache、一键包）都建立在 Pro 3.5 上**。
* 本机**没有** Pro 3.0/3.1/3.2，也**没有**它们的 SDK 程序集 ⇒ **无法在本机编译或验证 3.0/3.1 兼容性**。
* 因此 3.0/3.1 的兼容结论在拿到对应环境前只能是 **NOT VERIFIED**（不得写成 PASS）。

## 5 · 三个方案（含推荐）

| 方案 | 做法 | 覆盖范围 | 成本/风险 | 本机可做? |
|---|---|---|---|---|
| **A（推荐）单包通吃** | 改为 **net6** 编译 + `desktopVersion=3.0` + 检测策略改版本矩阵 | **Pro 3.0 – 3.6** | 中高：需 3.0–3.2 SDK、API 面复核、Python 兼容、全量回归 + 各版本真机验证 | ❌ 缺 SDK/机器 |
| **B 双包** | 出 **net6 包**（3.0–3.6）＋ **net8 包**（3.3–3.6，可用新 API） | Pro 3.0 – 3.6（功能可分档） | 高：两套构建与两套验证 | ❌ 缺 SDK/机器 |
| **C 仅扩展声明（过渡）** | 维持 net8/3.5 构建，只把可声明范围扩到 **3.3–3.5**（同为 .NET 8），并**明确标注 3.0–3.2 = NOT SUPPORTED（需 .NET 6 构建）** | Pro **3.3 – 3.6** | 低：主要是策略/文档/一键包 target 字段调整 + 3.3/3.4 真机抽查 | ✅ 部分可做（策略与文档），验证仍需机器 |

> **推荐路径**：若同事里有人在用 **3.0–3.2**，就必须走 **A**（先做 spike：拿到 3.0–3.2 的 SDK 程序集或
> 一台 3.1 机器 → 改 net6 → 编译 → 冒烟）；若同事都在 **3.3+**，则 **C** 即可马上满足，且 A 可作为后续批次。
> 无论哪条，**都不能在未验证的情况下把 3.0/3.1 写进"已支持"**。

## 6 · 建议的下一步（等待决策）

1. **请确认**：需要覆盖的 Pro 版本到底是哪些？（3.0/3.1/3.2 是否真有同事在用？）
2. 若确认需要 3.0–3.2：**需要外部资源** —— 一台装了 Pro 3.0/3.1/3.2 的机器（或提供其
   `bin\` 下 SDK 程序集与 `RegisterAddIn.exe` 路径信息），否则本机无法编译/验证。
3. 拿到资源后按 **A** 执行：改 net6 → API 面复核 → 检测策略版本矩阵 → 全量回归 → 各版本真机冒烟 →
   重打一键包（net6 版）→ 更新教程中的版本要求。
4. 本变更涉及**冻结契约**（目标版本、身份链、工具面基线），需 **Keeper 派单 + 用户批准** 后动产品码。

## 7 · 兼容性风险面扫描证据（2026-09-18 21:3x，只读扫描）

**① Python Bridge 老版本语法风险：无命中 ✓**

扫描 `Source/ArcGISProMCP.PythonBridge/bridge_runner.py`（含全仓 `Source/**/*.py`）的 7 类"新语法/新标准库"特征：
`match` 语句(3.10+)｜海象运算符(3.8+)｜字典并集 `|`(3.9+)｜内置泛型注解 `list[...]`(3.9+)｜`X | None` 注解(3.10+)｜
`removeprefix/suffix`(3.9+)｜`zoneinfo`(3.9+) —— **全部 0 命中** ⇒ 桥接脚本大概率可直接在老版本 Python 上运行
（仍须在真机 Python 上实测，此处仅为静态证据）。

**② C# 侧 SDK 使用面：仅核心命名空间，无 3.3+ 专有 API ✓**

| 命名空间（using 次数） | 自 Pro 3.0 起可用 |
|---|---|
| `ArcGIS.Desktop.Framework.Threading.Tasks` (11)、`ArcGIS.Desktop.Mapping` (9)、`ArcGIS.Desktop.Core` (8)、`ArcGIS.Desktop.Framework.Contracts` (6)、`ArcGIS.Desktop.Framework.Dialogs` (5) | ✅ |
| `ArcGIS.Core.Data` (4)、`ArcGIS.Desktop.Layouts` (2)、`ArcGIS.Core.CIM` / `Geometry` / `Licensing` / `Data.Raster` / `Data.Exceptions` (各 1) | ✅ |
| `ArcGIS.Desktop.Core.Geoprocessing` (1)、`ArcGIS.Desktop.Catalog` (1)、`ArcGIS.Desktop.Core.Events` (1) | ✅ |

**未出现**任何 3.3+ 才引入的能力域（Report / KnowledgeGraph / UtilityNetwork / TraceNetwork / Parcel 等）。

**③ 版本敏感 API 调用点（需逐签名核对 3.0 API 面）**

`FeatureLayer`(23) ｜ `MapView`(8) ｜ `CreateLayout`(8) ｜ `Geoprocessing.ExecuteToolAsync`(2) ｜
`GeometryEngine.Instance`(1) / `GeometryEngine.GetPredefinedCoordinateSystemList`(1) ｜ `CreateMapFrame`(1)

> 这些 API **在 3.0 就存在**，风险不在"有没有"而在**重载签名/参数**是否有差异 —— 只能靠"用 3.0–3.2 SDK 实际编译"暴露。
> 结论：**方案 A（net6 单包）看起来是"工程化改造"而非"架构重写"**，但最终判定必须来自真机编译 + 运行。

---

## 8 · 现状对使用者的直接影响（重要，别踩坑）

**现在发给 Pro 3.0/3.1/3.2 的同事，安装器会在"依赖检测"阶段直接拦住并提示 `COMPATIBILITY_NOT_PASS`。**
这是当前设计的**安全行为**（宁可拦住也不装坏），但要清楚：

* 拦住的原因不是"缺件"，而是**本插件编译目标为 .NET 8 + 加载项目标版本 3.5**；
* Pro 3.0–3.2 只有 **.NET 6** 运行时 ⇒ 加载项即使装上也不会被加载；
* 所以——**在完成 net6 改造并验证之前，不要把本包发给用 3.0–3.2 的同事**（会白跑一趟）。
  **3.3/3.4 亦未验证** —— 本包 AddInInfo 的 desktopVersion 为 3.5.0，按 ESRI 加载项版本规则，目标版本高于当前 Pro 版本时不会被加载（3.3/3.4 能否加载需实测确认，本轮未在真机验证，不得写成支持）。**已确证的可用范围 = Pro 3.5（本机 LIVE 全量验证）**；3.6/3.7 亦未实测。

---

## 9 · net6 编译 spike 实证（2026-09-18 21:4x，**在 .runtime 副本中进行，未改产品码**）

**方法**：把 `Source/` 复制到 `.runtime/.../spike-net6/`（191 文件，排除 bin/obj），在**副本内**把所有 csproj 改为
`net6.0-windows`，用本机 .NET SDK 8.0.424 编译，逐层暴露错误。产品树**零改动**。

### 逐层结果（每层修掉后重编，观察下一层）

| 轮次 | 错误码 | 数量 | 含义 | 结论 |
|---|---|---|---|---|
| 1 | （NuGet 环境变量缺失） | 8 | `Value cannot be null. (Parameter 'path1')` | 构建环境问题，非代码问题（补 APPDATA/LOCALAPPDATA 等即可） |
| 2 | `CS8936` | 14 | 语言特性不可用（net6 默认 C# 10，代码用了 C# 11 语法） | **1 行配置**：显式 `LangVersion` |
| 3 | `CS0656` | 12（6 处） | 缺 `System.Runtime.CompilerServices.RequiredMemberAttribute` 等 | **1 个小 polyfill 文件**（C# 11 `required` 的运行时特性，.NET 7+ 才有） |
| 4 | `CS0656` | 4 | polyfill 命名空间放错（`SetsRequiredMembersAttribute` 属 `System.Diagnostics.CodeAnalysis`） | 修正即可 |
| 5 | `CS1501` | 4（2 处） | **真正的 net8-only API**：`Stream.FlushAsync(CancellationToken)` 在 .NET 6 不存在 | 需改写（保留取消语义的等价实现） |
| **6** | **`CS1705`** | 10（5 处） | **程序集 `ArcGIS.Desktop.Framework 13.5.0.0` 引用 `System.Runtime 8.0.0.0` > 本项目 `6.0.0.0`** | **★ 决定性阻塞**：必须用 Pro 3.0–3.2 的 SDK 程序集（13.0–13.2）编译 net6，**用 13.5 不可能成功** |

### 关键读数

* **共享库（Core / Protocol / Configuration / Logging / Security / Tools / Server）在 net6 下全部编译通过** ✓
  ⇒ net6 改造的阻力**不在业务逻辑**，只在少数 API 与语言特性上。
* **唯一硬阻塞**：加载项项目（`ArcGISProMCP.Compatibility`）必须引用与目标 .NET 同代的 Pro SDK 程序集；
  本机只有 13.5（.NET 8 系）⇒ CS1705 不可绕过。
* **方案 A 工程量（已量化）**：① 显式 LangVersion ② required polyfill（1 文件）③ 2 处 net8-only API 改写
  ④ **外部依赖：13.0–13.2 SDK 程序集（Pro 3.0–3.2 机器）** ⑤ 之后才是身份链 / 策略版本矩阵 / 真机验证。

### spike 证据落盘

`run-20260918-d058-stage2-cleanmachine/spike-net6/Source/spike-build{,2..6}.log`（六轮日志全量保留，可复算）。
> 纪律：spike **只在副本内**进行，产品树未改一个字节；`Source/` 主树 csproj 仍为 `net8.0-windows`。
