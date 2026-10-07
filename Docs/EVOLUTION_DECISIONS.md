# EVOLUTION_DECISIONS.md — 决策与冲突登记（ADR / Conflict Register）

> **历史时点声明（D-126 项目完结批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **2026-09-11（原头部自述 2026-09-10／D-001；文内最晚日期 2026-09-11）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。

> 规则：冲突先**登记核实**，不挑选乐观结论；不得用历史 PASS 替代当前事实。
> 最后更新：2026-09-10，派工单 D-001。

## 已核实并解除的疑似冲突

### C-01 `ArcGISProMCP.Current` 目录为空（疑似源码缺失）
- **现象**：`Source/ArcGISProMCP.Current/` 仅 1 个 README.md，0 个 `.cs`。
- **核实**：README 明示"目标 ArcGIS Pro 3.7+（.NET 10）；**当前状态仅占位**，不创建 .csproj、不编译、不安装 .NET 10"。
- **结论**：**有意占位，非源码缺失**，不构成迁移阻断。占位目录随白名单迁移以保持结构完整。
- **影响**：Phase 8 不涉及 Current Host；若将来需 3.7+ 新 API，另立 ADR。

### C-02 旧仓库零 Git 提交
- **现象**：`git log` → `fatal: your current branch 'master' does not have any commits yet`；`git ls-files` → `0`。
- **结论**：与 V2 计划第 4 节预估一致。**禁止**用 `clone`/`worktree` 冒充迁移；必须白名单文件复制 + 逐文件 SHA256。
- **状态**：已按此执行。

## 待裁定冲突

### C-03 WorkBuddy 配置字段与现有模板不一致 【需核实，暂缓写入】
- **V2 计划候选**：
  `{"mcpServers":{"arcgis-pro-mcp":{"type":"streamableHttp","url":"http://127.0.0.1:6520/mcp","timeout":30000}}}`
- **现有 4 客户端模板实测**（`Config/client-templates/`）：
  - `claude-desktop.mcp.json` / `cursor.mcp.json`：仅 `{"mcpServers":{"arcgis-pro-mcp":{"url":"…"}}}`，**无 `type`、无 `timeout`**
  - `codex.toml.fragment`：仅 `url`
  - `deepseek.cordis.patch.yml`：显式 `transport: streamable-http` + `url`，**无 `timeout`**
- **冲突**：`type`/`timeout` 在本项目既有模板中从未出现，也未经验证；`.workbuddy/mcp.json` 在本机尚不存在。
- **决定**：**不得**直接写入真实配置。按 V2 §5.2 先 Plan/Validate，字段、timeout 单位、路径均按实际版本与官方资料核实后再申请 Apply。
- **状态（2026-09-11 08:41 更新，D-005/D-006 + R-W7/R-W8）**：**已结案**。字段存在性由官方文档证实
  （G-13）；**W2 定论通过**（R-W8 客户端层实证：WorkBuddy 接受 `http://127.0.0.1` loopback，非 HTTPS 照样通）；
  **W3 定论通过**（`/mcp` 后缀有效，协议层与新会话均实证）。Apply 双轨完成（项目级 + 用户级，均经用户授权）。
  逐项对照表：`Docs/_gatekeeper/TEMPLATES/8.5.2_FIELD_VALIDATION.md`。
  注意：连接对象 = 已安装 1.0.2（不含 8.1 修复），连接验收不代表 8.1 已验证。

### C-04 `select_layer` 语义与选择契约 V2 的鸿沟 【影响工具数量预期】
- **源码实证**（`SelectionTools.cs`）：描述为"在活动视图中**选择（高亮）指定图层**"，入参仅 `mapName`+`layerName`，
  调用 `Selection.SelectLayerAsync(mapName, layerName)` —— **不含 where 条件、不含 OID、不返回选中要素**。
- **冲突**：这是"TOC 图层选择"，不是"要素选择集"。当前 30 工具中**没有任何按属性/OID/位置选择要素的能力**。
- **决定（待批准）**：Phase 8.3 建议新增 `select_by_attribute` / `select_by_location` / `get_selected_features`；
  旧 `select_layer` 语义不得静默改变（按 V2 §8.3 采用兼容扩展或经批准的新工具名）。
- **连锁影响**：若 8.3 新增工具获批，**Phase 8.5.3 的工具数量预期不得硬编码 30**，应按当次 manifest 精确验证。

### C-05 `get_map_info` 与 `get_current_map` 语义混淆
- **源码实证**（`MapTools.cs`）：`get_map_info` 省略 `mapName` 时执行 `r.Data?.FirstOrDefault()`，
  即**取列表首项**，而非真正的活动地图；且按名称匹配用 `OrdinalIgnoreCase` + `FirstOrDefault`，**重名时静默选第一个**。
- **冲突**：直接违反 V2 §8.1"明确稳定标识与名称的关系；歧义不选择第一个"。
- **决定**：Phase 8.1 必须重新定义二者边界，详见 `Docs/phases/PHASE_08/04_PHASE_8_1_DESIGN.md`。

### C-06 `get_current_map` 用非空结果掩盖"无活动视图" 【掩盖失败】
- **源码实证**（`MapService.cs`）：`MapView.Active?.Map` 为 null 时回退到工程首个地图，
  源码注释为"**保证自测稳定返回非空**"。
- **冲突**：违反 V2 §8.1"覆盖…无活动视图"与"不能用空数组掩盖读取失败"。返回非空 ≠ 存在活动地图。
- **决定**：8.1 必须区分"有活动视图"与"无活动视图"，后者返回明确状态而非静默回退。

### C-07 候选工具的许可依赖 【范围风险】
- Phase 11 候选中 `erase` / `union_analysis` / `identity` / `symmetrical_difference` / `near` 等依赖 **Advanced** 许可；
  `zonal_statistics` / `reclassify_raster` / `raster_calculate` 等依赖 **Spatial Analyst** 扩展。
- **决定**：实施前必须核实实机许可；许可不足时按 V2 §8"记录阻断并请求范围决定"，**不得**以降级实现冒充完成。
- **状态**：NOT VERIFIED（本轮未启动 Pro，未读许可）。

### C-08 批次预算与计划估算显著冲突 【需用户范围决策】
- 87 个新增必做工具 ÷ 每批 3–5 个 ≈ **18–29 批**；按 V2 §13 每批 5–12 日 ≈ **90–348 工作日**。
- V2 §13 对 Phase 9–12 仅给"每批 3–5 工具、5–12 日粗估"，未给出总量级换算。
- **决定**：**不自行降级或凑数**。按 V2 §10"若需调整目标，必须由用户明确批准"，将此列为范围决策项。

### C-09 `buffer` 入参类型与实现不符
- Schema 声明 `distance: number`，实现用 `ToolArgs.GetInt(context, "distance")` → **小数距离存在截断/失败风险**。
- **状态**：已登记，建议 Phase 8.6 全量回归时验证并修正契约。

### C-10 `get_layer_info` schema 与实现契约不一致
- `InputSchema` **未声明** `required`，但实现在 `layerName` 为空时返回 `InvalidArgument`。
- 客户端按 schema 认为参数可选 → 实际必填，属契约不自洽。建议 8.1/8.6 统一。

### C-11 Release 白名单边界 ——【G-03 已批准；D-002 已执行补复制，条目保留备查】
- 状态变更：原"待裁定" → **Gate Keeper G-03 批准补复制（限一项，只复制不安装）**。
- 已于 D-002 执行：复制 `payload/ArcGISProMCP.Compatibility.esriAddInX`（269548 B）到新工作空间同路径；
  源端与目标端 SHA256 均为 `361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A`；
  源端复算未变；**未执行** RegisterAddIn / 安装 / 启动；未复制 `… - 副本.zip` 及 r5 目录其余内容。
- 独立证据：`migration-manifest-d002-addendum.json`（**不改动** D-001 的 327 条基线清单）。
- 已复制：`r5 ZIP` + `.zip.sha256` + `bundle-manifest.json` + `payload/release-manifest.json`（4 项）。
- 未复制（超出白名单字面范围）：`payload/ArcGISProMCP.Compatibility.esriAddInX`（269548 B，1.0.2 基线二进制）、
  r5 解压目录其余文件、`… - 副本.zip`（派工单明确排除）。
- 该 addin 的身份已**实测**记录于本报告（大小与 SHA256 均与派工单一致）。
- **待裁定**：是否在后续派工中补复制该二进制作为基线身份链证据。

### C-12 `MapService.ToInfo` 硬编码 `Kind`/`MapType` 【Phase 8.1】
- **实证**（`MapService.cs:45-51`）：`Kind` 恒为 `"Map"`、`MapType` 恒为 `""`。
- **影响**：无法区分 2D 地图与 3D 场景，违反 V2 §8.1。
- **补充约束**（S1 所得）：Pro 3.3+ 存在 `MapType.LinkChart`，故 `kind` **不得按二值设计**，须按枚举原值透出并保留未知值兜底。

### C-13 `MapService.GetMapsAsync` 只读路径副作用 【Phase 8.1】
- **实证**（`MapService.cs:35-38`）：`GetItems<Item>()` + `MapFactory.CreateMapFromItem` 枚举地图。
- **S1 核实结论**：`CreateMapFromItem` 的官方文档用例对象是 **portal/web 项用于建图**；
  对已存在于工程中的地图项，文档推荐路径是 `GetItems<MapProjectItem>()` + `mpi.GetMap()`。
  现有写法偏离推荐路径，**是否真的新增项目项需实机确认 → 运行时 NOT VERIFIED**。
- **建议实现**：改为 `GetItems<MapProjectItem>()` + 读 `mpi.MapType`/`Path`/`Name`，**既不创建也不加载**，从根上消除副作用。

### C-14 `list_workspace_datasets` 栅格未枚举 vs 栅格为空不可区分 【Phase 8.2】
- 源码注释已诚实声明 `rasterDatasets=[]` 表示未实现栅格枚举、不代表不存在栅格。
- 但**契约层**未提供区分手段，V2 §8.2 要求二者必须可区分 → 8.2 需在返回结构中显式表达"未支持"状态。

### C-15 【已更正】构建环境：沙箱剥离 Windows 系统环境变量（**原判定已撤回**）
- **原判定（已撤回）**：曾记为"NuGet restore 不可用 = 既定环境限制 → BLOCKED_BY_ENVIRONMENT"。
  **该结论错误**，Gate Keeper 已用 G-07 作废，执行会话于 D-003 rev2 正式撤回。
- **真实根因**：本 Harness 的 Bash shell **被剥离 Windows 系统环境变量**
  （`ProgramData`/`ProgramFiles`/`CommonProgramFiles`/`SystemRoot`/`windir`/`APPDATA`/`PUBLIC` 等为空）。
  NuGet 在 `_GetRestoreSettings` 解析机器级/用户级配置路径取到 null，抛
  `ArgumentNullException('path1')`（`NuGet.targets(745,5)`）。
  **与项目代码、路径空格、沙箱策略、SDK 安装均无关。**
- **规避方式**：执行任何 `dotnet` 命令前先补齐 `Docs/_gatekeeper/BUILD_ENV_FIX.md` 所列环境变量。
  实测补齐后：`restore` 12/12 成功；`build` **0 警告 / 0 错误**。
- **我的误判路径（教训）**：只单补 `APPDATA` 未补系统级变量 → 仍失败；又把"最小项目也失败"
  错误外推为"环境根本不可用"，未继续追问**具体缺失的输入**。
  教训：最小复现失败时应继续定位具体缺失项，而非停在"环境不行"。
- **状态**：**CLOSED（已更正并给出规避方式）**。

### C-16 47 个 Unit 失败的精确定性 + 白名单连带后果 【D-003 rev2】
- **背景**：D-003 实测 UnitTests `47 F / 199 P / 3 S / 249 T`。逐条按实际异常分类（非笼统归因）：
  - **A（29）** `Win32Exception: start process '<7.x 宿主>'` —— 可选脚本宿主缺失 → `BLOCKED_BY_ENVIRONMENT`。
  - **B（4）** `bin/x64/Debug/net8.0-windows/` 缺 `esriAddInX` / `release-manifest.json`（**打包产物**，
    非 `dotnet build` 产出）→ `BLOCKED_BY_ENVIRONMENT`。
  - **C（12）** 依赖 B 的脚本退出码不符（`Expected 0/23, Actual 1`）→ 根因同 B。
  - **D（2）** `DocumentationPolicyTests`：`Distribution/START-HERE.cmd` 缺失、
    `Docs/PROJECT_STATE.md` 链接 `../.runtime/phase78_final_handoff/…json` 断裂
    —— **由 D-001 白名单排除 `Distribution/`、`.runtime/` 直接导致** → `BLOCKED_BY_WHITELIST`（**非环境问题**）。
- **与改动相关性**：**0 项**与 D-003 的 8 个产品文件相关。
- **历史矛盾（233 P / 3 S / 0 F）**：**当前基线不可判定** —— 历史证据目录 `.runtime/phase78_final_handoff/`
  本身未迁移；且旧仓库存在 `bin/` 打包产物与 `Distribution/`，新工作空间按白名单不含这些。
  两个候选解释：① 历史环境具备 7.x 宿主；② 历史运行跳过/排除了该批用例
  （与 `PROJECT_STATE.md:160` "Automated dotnet test = BLOCKED_BY_HARNESS" 相符）。
  **建议不再追溯，以本次实测为新基线。**
- **待裁定**：① D 类 2 项如何处理（补迁移 vs 修订文档预期）；② 是否采用**能力感知跳过**（CR-4）——
  依据 `USER_GUIDE.md:11` 已声明 "PowerShell 7 **可选**"，而 29 个用例硬失败，二者自相矛盾。
- **状态**：**OPEN**，等待裁定。

### C-17 Phase 8.1 测试工程已建立 + arpy/SDK 行为实测发现 【D-004】
- **产物**：`TestFixtures/Phase8_1/Phase8_1_MapDiscovery.aprx`（36,430 B，`b3662671…f5e47b`）+
  `Phase8_1Test.gdb`（69 文件）；以 Pro 自带空白模板经 **ArcPy 无 GUI** 建立；受保护资产未触碰。
- **实测清单**：5 地图（`MD_Active`×2 重名 / `MD_Inactive` / `MD_Scene`(SCENE) / `MD_Empty`）；
  `MD_Active` 顶层 6 层（含重名 `L_Lines`×2、`L_Hidden` visible=false）+ `L_Group`(内 `L_Sub_A`/`L_Sub_B`)；
  布局 `LY_Discovery`。**flatten=true 期望 8 / false 期望 6**。
- **新证据（B-1…B-5）**：
  - **B-1** `createMap` 重名自动去重为 `xxx1`，但**显式赋 `map.name` 可强制重名成功** → 重名歧义用例**可构造**（ADR S2 有机会实证）。
  - **B-2** `Map.addLayerToGroup(group, <path>)` **不接受路径**，只接受 Layer 对象。
  - **B-3** `addLayerToGroup(group, layer)` 会**保留顶层原层** → 需显式 `removeLayer` 去顶层原层，否则组合图层子层重复计数。
  - **B-4** `Map.listLayers()`（arcpy）**包含组合图层子层**（实测 8 = 6 顶层 + 2 组内），可用于与 SDK `Map.Layers`/`GetLayersAsFlattenedList()` 交叉验证。
  - **B-5** `createMap` 自带默认底图 `Topographic`，且该 Layer 实例**不支持 `.name`/`.visible`** 读取 → 空地图须显式移除底图，否则 A3/A12 的"空→`[]`"失效。
- **状态**：§4 完成；§5（A1–A20）**BLOCKED** —— 需**用户启动 ArcGIS Pro**（MCP Server 随 Pro 进程运行，执行者不得代为启动）。
- 建工程脚本：`tools/fixtures/build_phase8_1_fixture.py`、`cleanup_phase8_1_fixture.py`（幂等、拒绝覆盖）。

## 随附裁定落地（Gate Keeper G-01…G-05，2026-09-10）

| 编号 | 对应本文件 | 裁定 | 落地状态 |
|---|---|---|---|
| G-01 | **C-08** | 维持 112+ / 必做 117，缓冲 5；批次 3–5；**禁止降级或凑数**；前 2 批后按实测吞吐重估 | 已记入 `03_BACKLOG_112.md` §9 |
| G-02 | **C-04** | **批准**新增 `select_by_attribute` / `select_by_location` / `get_selected_features`；`select_layer` 语义不得静默改变；**工具数预期不得再硬编码 30** | 已记入 `02_TOOL_GAP_MATRIX.md` §2 第 19 行与 `03_BACKLOG_112.md` §2 |
| G-03 | **C-11** | 批准补复制 `.esriAddInX`（只复制/不安装/不注册） | **已执行**（见上） |
| G-04 | — | 批准创建自有 `TestFixtures/Phase8_1/*.aprx|gdb`；受保护资产禁触 | **不在 D-002 行使**（spike 为只读），待 8.1 实现派工 |
| G-05 | — | `MyProject1.aprx` 已定位：`<user-home>\Documents\ArcGIS\Projects\MyProject1\MyProject1.aprx`，SHA256 `ECF25A87…CD14`，36150 B | 已记入受保护资产清单，后续 post-check 纳入 |

> G-01/G-02/G-03/G-05 的原文见 `Docs/_gatekeeper/DECISIONS_LOG.md`。

## S1–S5 SDK 能力核实 ADR（D-002 任务 ②）

完整 ADR 见 **`Docs/phases/PHASE_08/06_SDK_SPIKE_ADR.md`**。要点：

| # | 结论 | 选定方案 | 状态 |
|---|---|---|---|
| S1 | 不创建对象区分 2D/3D **可行** | `MapProjectItem.MapType`（`ArcGIS.Core.CIM.MapType`；含 `LinkChart`） | 文档+程序集 ✅ / 运行时 NOT VERIFIED |
| S2 | 稳定标识 | **主标识 `MapProjectItem.Path`**；`Item.ID` 备选；`Map.URI` 不作主标识（可能为空） | 重名唯一性 NOT VERIFIED |
| S3 | 活动地图不回退 | `MapView.Active?.Map`；null ⇒ `NO_ACTIVE_VIEW`；关联用 URI↔Path（兜底 `GetMap()` 相等） | 关联语义 NOT VERIFIED |
| S4 | 组合图层语义确认 | `Map.Layers` 保层级（仅顶层） vs `GetLayersAsFlattenedList()` 扁平递归；**建议默认 flatten** | ⚠ 属**既有契约变更**，需批准+四客户端回归 |
| S5 | 空工程 | 预期返回 `[]` 而非错误 | NOT VERIFIED（未启 Pro） |

## 已采取但未改变事实的动作
- 本轮未启动 ArcGIS Pro / Python Bridge / MCP Server；未写入任何真实客户端配置；未安装任何依赖；未 commit/push。
- D-002 另：未执行 RegisterAddIn / 安装；未创建或修改任何 `.aprx` / `.gdb`；未修改 `Source/`。

---

## 附录 A 编号变更对照表（D-002 统一，2026-09-10）

**权威编号 = 本文件（EVOLUTION_DECISIONS.md）**。一切跨文档引用以本表为准；**结论内容零改动**。

| 旧编号（冲突处） | 出现在 | 新编号（权威） | 说明 |
|---|---|---|---|
| `C-11`（= `GetMapsAsync` 只读路径副作用） | `04_PHASE_8_1_DESIGN.md` §1 | **`C-13`** | 与 DECISIONS 的 `C-11`（Release 白名单）冲突，故重编 |
| `C-12`（= `ToInfo` 硬编码 Kind/MapType） | `04_PHASE_8_1_DESIGN.md` §1 | **`C-12`（不变）** | DECISIONS 原无 C-12，无冲突，保留 |
| `C-05a` / `C-05b` | `04_PHASE_8_1_DESIGN.md` §1 | **`C-05` 的子项**（非独立编号） | DECISIONS 中合并为 `C-05`；DESIGN 为叙述清晰而拆分 |
| （未编号）`list_maps` 副作用 + Kind 硬编码 | `02_TOOL_GAP_MATRIX.md` §3 | **`C-12` + `C-13`** | 原为一行"—"，现拆为两条有编号缺陷 |
| （未编号）`list_workspace_datasets` 栅格不可区分 | `02_TOOL_GAP_MATRIX.md` §3 | **`C-14`** | 新增编号 |

**权威 C-ID 全表（截至 D-002）**

| ID | 主题 | 归属阶段 |
|---|---|---|
| C-01 | `ArcGISProMCP.Current` 空目录 = 有意占位（非缺失） | 已核实解除 |
| C-02 | 旧仓库零 Git 提交 → 禁用 clone/worktree 冒充迁移 | 迁移 Gate |
| C-03 | WorkBuddy 配置字段（`type`/`timeout`）与现有模板不一致 | 8.5 |
| C-04 | `select_layer` 语义鸿沟（无要素选择能力） | **8.3**（G-02 已批准新增 3 工具） |
| C-05 | `get_map_info` / `get_current_map` 语义混淆（a 取首项；b 重名静默取第一） | 8.1 |
| C-06 | `get_current_map` 用非空掩盖"无活动视图" | 8.1 |
| C-07 | 候选工具许可依赖（Advanced / Spatial Analyst） | 11（预检） |
| C-08 | 批次预算与计划估算冲突 | 范围决策（G-01 已裁定不降级） |
| C-09 | `buffer` schema `number` vs 实现 `GetInt` | 8.6 |
| C-10 | `get_layer_info` schema 无 `required` 但实现必填 | 8.1 / 8.6 |
| C-11 | Release 白名单边界 | **已完成**（G-03 + D-002） |
| C-12 | `ToInfo` 硬编码 `Kind`/`MapType` | 8.1 |
| C-13 | `GetMapsAsync` 只读路径副作用 | 8.1 |
| C-14 | `list_workspace_datasets` 栅格未枚举 vs 为空 | 8.2 |
| C-15 | 构建环境（沙箱剥离系统环境变量）——**已更正并 CLOSED** | 环境纪律（每次 `dotnet` 前补齐变量） |
| C-16 | 47 项 Unit 失败定性（环境 43 + 白名单 2）+ 历史矛盾 | 待裁定 |

> 命名空间约定：`C-xx` = 执行会话登记的冲突/缺陷；`G-xx` = Gate Keeper 裁定（见 `Docs/_gatekeeper/DECISIONS_LOG.md`）。两者**互不复用编号**。
