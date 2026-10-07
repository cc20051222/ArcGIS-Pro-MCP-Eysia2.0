# D-060 · API 跨版本兼容报告（A 项硬判据）

- **生成**：执行会话 ｜ 2026-09-19 ｜ **依据**：D-060 工单 A-修订 2 §5.3 ＋ **G-160 裁定 2**（API 差集升为硬判据）
- **结论**：**PASS** —— 本仓产物的 SDK 引用面在 **13.0 / 13.1 / 13.2 / 13.5 及本机 Pro 3.5 运行期程序集**上**全部存在（0 缺失）**，且在改造过程中**实际检出并消除 2 处真实的跨代阻断**。

---

## 1 · 方法与工具（两套，一主一辅）

| 套 | 工具 | 范围 | 局限 |
|---|---|---|---|
| **主（有效判据）** | 自建 `apicheck`（`.runtime/tools/apicheck`，基于 `System.Reflection.Metadata`） | **本仓编译产物实际引用到的 SDK 成员** → 逐个在目标引用集判定类型/成员存在性 | 成员重载不区分签名（按名称判定） |
| 辅（形式判据） | `Microsoft.DotNet.ApiCompat` 10.0.401（dotnet tool，项目内 `.runtime/tools/apicompat`） | 两个引用集之间的全量 API 集合差 | 含**跨代接口解析产物**（CP0008 等），且无法限定「我们真正用到的 API」⇒ 原始计数不直接可用 |

> **判据选择说明（重要）**：ApiCompat 的**原始差集**报 `removed = 2,142` 项，但其中绝大多数是 Esri 两代之间的内部/未使用 API 变动（例如 Core 的 772 条 CP0008 全部是 `ArcGIS.Core.Internal.CIM.*` 的接口实现形态变化）。
> 这类差异**对本站交付物无影响**（我们既不实现也不引用）。G-160 要求的「removed = 0」在本项交付中的**有效形态** = **「本仓引用面 removed = 0」**，
> 这正是 `apicheck` 所测。两份结果**都**在 §4 / §5 完整披露，供 Keeper 裁定。

---

## 2 · 主判据结果：引用面 × 版本可用性矩阵（`api-availability-matrix.json`）

被测产物：`Source/ArcGISProMCP.Compatibility/bin/x64/Debug/net6.0-windows/ArcGISProMCP.Compatibility.dll`（net6，针对 13.0 引用集编译）

| 目标引用集 | 引用类型数 | 引用成员数 | **缺失成员** | **缺失类型** | 结果 |
|---|---|---|---|---|---|
| **13.0**（编译基线，Pro 3.0） | 80 | 183 | **0** | **0** | PASS |
| **13.1**（Pro 3.1） | 80 | 183 | **0** | **0** | PASS |
| **13.2**（Pro 3.2） | 80 | 183 | **0** | **0** | PASS |
| **13.5**（Pro 3.5，引用集） | 80 | 183 | **0** | **0** | PASS |
| **本机 Pro 3.5 `bin`（运行期真实程序集）** | 80 | 183 | **0** | **0** | PASS |

⇒ **跨版本安全证据成立**：单包在 3.0–3.5 上不存在「编译通过、运行期缺成员」的风险（就引用面而言）。

---

## 3 · ★ 改造过程中实际检出并消除的 2 处跨代阻断（差集的实战价值）

### 3.1 `GDBProjectItem.IsGeodatabase`（13.5 新增，13.0 无）

- **捕获方式**：**编译期护栏**（以 13.0 引用集编译 ⇒ `CS1061`），符合方案书 §4 预期。
- **实情**：该属性在 13.5 存在、13.0 不存在。
- **处置**：改按工程项路径后缀等价判定 —— `*.gdb` ⇒ `FileGeodatabase`，其余 ⇒ `Database`
  （`ProjectService.IsFileGeodatabase`，附 XML 注释说明差异与依据）。

### 3.2 ★★ `Map.GetSelection()` 返回类型跨代不同（**运行期阻断级**，仅元数据判据可检出）

- **实情（元数据实测）**：

| 版本 | `Map.GetSelection()` 返回 | `SelectionSet` 成员数 | `ToDictionary` 绑定目标 |
|---|---|---|---|
| **13.0** | `SelectionSet` | 14（含 `ToDictionary`/`Count`/`Item`/`Contains`） | `SelectionSet::ToDictionary` |
| **13.5** | `MapMemberIDSet`（**13.0 无此类型**） | 6（仅 `FromDictionary`/`FromMapMemberIDSet`/`FromSelection`/`InternalSelectionSet`） | `MapMemberIDSet::ToDictionary` |

- **反查证据**：直接读取两代产物中 `ToDictionary` 的 MemberRef 父类型 ——
  - 现役安装位（13.5 编译，`24CA2B4B`） ⇒ 父 = `ArcGIS.Desktop.Mapping.MapMemberIDSet`
  - 新版 net6（13.0 编译） ⇒ 父 = `ArcGIS.Desktop.Mapping.SelectionSet`
  ⇒ **同一份源码在两代绑定到不同类型**。
- **危害**：net6 单包若保留原写法，**在 3.5 上必然运行期失败**（调用不存在的 `SelectionSet::ToDictionary`）。
  这是**编译期护栏抓不到**的反向风险 —— 正如方案书 §4 所警示、G-160 把差集升为硬判据的理由所在。
- **处置（改为「两代公共 API 面」）**：读取路径不再使用 `Map.GetSelection()`，改为

  ```
  Map.GetLayersAsFlattenedList()          ┐
  Map.GetStandaloneTablesAsFlattenedList() ├─► 两代均存在（已逐版本核验）
  BasicFeatureLayer.GetSelection()        │
  StandaloneTable.GetSelection()          │   → ArcGIS.Core.Data.Selection
  ArcGIS.Core.Data.Selection.GetObjectIDs()┘   → 两代同形
  ```
- **语义等价性**：原实现按「地图级选择集字典」逐成员输出；新实现按「图层/独立表选择对象」逐成员输出；
  均为「有选择集的成员各一条」，空选择集不产出条目 ⇒ 快照/账本语义不变。
- **保留**：`SelectionSet.FromDictionary` + `Map.SetSelection`（两代同名同形）用于恢复路径，**未改动**。

---

## 4 · 辅判据原始结果（全量披露，`api-diff-fwd-*.log`）

ApiCompat：`left = 13.0`（契约基线）→ `right = 13.5`（对照），**原始**规则码分布：

| 规则 | 含义 | 件数 |
|---|---|---|
| CP0008 | 类型在某侧未实现另一侧的接口 | 784 |
| CP0002 | 成员在 left 存在、right 不存在 | 646 |
| CP0006 | 无法添加接口成员 | 625 |
| CP0001 | 类型在 left 存在、right 不存在 | 87 |
| CP0005 / CP0007 / CP0019 / CP0009 / CP0012 | 枚举/常量等其他差异 | 16 |
| **removed 合计（不含 CP0003 程序集版本）** | | **2,142** |

**性质判定**：这些差异集中在 `ArcGIS.Core.Internal.*`、`ArcGIS.Desktop.Internal.*`、`ServiceContracts`、内部 CIM 接口等
**非公开契约面**；本站交付物**未引用**其中任何一项（该结论由主判据的 183 个引用成员逐条核验支撑）。

> **Keeper 裁定请求**：G-160 的「removed API 必须 = 0」是否接受按「**引用面 = 0**」（§2 结果）执行；
> 若要求「全量集合 = 0」，则 Esri 两代间客观存在 2,142 项差异，无法通过本站改造达成（且与本站交付物无关）。

---

## 5 · 与运行期验证的关系（G-160 §5.2 分层）

- **编译期（本报告）**：100% 自解决，已达成。
- **运行期**：`3.5` 由本机实测（D-060 LIVE 阶段）；`3.0–3.4` **NOT VERIFIED**（用户裁定 (c) 不验证）。
- 本报告的 §2 矩阵**不能替代**运行期验证，但它是放弃运行期验证后**唯一的跨版本安全证据**（G-160 原话），
  其中 §3.2 已实证该证据的**有效性**（抓到了一处编译期抓不到的真实阻断）。

---

*证据文件（`.runtime/evolution/phase15/run-20260919-d060/`）：
`api-availability-matrix.json`（主判据）｜`api-diff.json` ＋ `api-diff-fwd-*.log`（辅判据）｜
`r6-extract/installed-24CA2B4B-Compatibility.dll`（绑定反查样本）。*
