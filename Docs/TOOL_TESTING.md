# 工具测试（TOOL_TESTING）

> **历史时点声明（D-126 项目完结批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **2026-09-04（文内最晚 ISO 日期实测；正文跨度 2026-08-31…2026-09-04）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。

## 测试层次
1. **Unit**（参数/元数据/Registry/Router/错误/取消）
2. **Integration**（FakeArcGISHost + 真实 MCPToolRouter：Tool→Router→Registry→Host→Service→Result）
3. **Real ArcGIS Pro 3.5**（在 Pro 内加载 Add-in，经 MCP HTTP 真实调用）

## 当前状态（Phase 5.8.1，2026-09-03）

- Phase 5.7.5 **PASS**；Phase 5.7 **PASS**；Phase 5.8 preflight **COMPLETE / ACCEPTED**；Phase 5.8.1 **PASS**（independent Gate Keeper formally accepted）；Phase 5.8.2 **NOT STARTED / READY TO START**；Phase 5.9 **NOT STARTED**；Phase 5 整体 **NOT COMPLETE**。
- Unit **68/68 PASS**；Integration **20/20 PASS**；`MCPProtocolTests` **9/9 PASS**；`MCPServerTests` **25/25 PASS**；7 项 HTTP transport 用例为 `BLOCKED_BY_HARNESS / Case H`。
- Final discovered inventory 为 129 Facts；PASS 的非 HTTP tests 为 122，HTTP 7 项保持 blocker；coverage 仅诊断用途、无阈值。
- Test-owned workspace、safe naming、cleanup、placeholder/Python temp fixture ownership 已落实；runtime initialize/tools/list=30/python_bridge_ping=pong 只读复核 PASS。
- 完整 30-tool historical matrix 位于 [`PHASE_05_7_5_TEST_ACCEPTANCE.md`](phases/PHASE_05_7_5_TEST_ACCEPTANCE.md)；Phase 5.8 preflight 已完成并接受；外部 native host 已通过同一 ArcPy probe，下一步继续执行 5.8.1 fixture preparation。

## Phase 5.8 Real Verification Preflight（2026-09-03）

- 完成 30-tool real inventory、历史 real-evidence matrix、P0/P1/P2 gap、shared GDB read-only policy、per-run `TestWorkspace` FileGDB、controlled project、selection/visibility/add/remove、GP/ArcPy、cleanup、serialization 和 failure-isolation preflight。
- 当前 runtime 只读复核：initialize 200、tools/list=30 distinct、python_bridge_ping=pong；`TestPolygons` 为 FeatureClass/Polygon/WKID 3857/count 4/6 fields。
- 本轮 build/tests：0 errors/3×NU1900；Unit 68/68、Integration 20/20、Protocol 9/9、MCPServer 25/25 PASS；HTTP 7 项 Case H 不重开。
- Phase 5.8 不要求 30 个工具全部直接真实执行；优先 shared execution path representative、high-risk mutation/GP、历史 accepted evidence 和 PARTIAL/NOT VERIFIED gaps。
- 完整策略与最终 gate：[`PHASE_05_8_REAL_VERIFICATION_PREFLIGHT.md`](phases/PHASE_05_8_REAL_VERIFICATION_PREFLIGHT.md)。本轮未执行 real GP、mutation、完整 MCP business chain、AI/provider 或 Phase 5.9。

**READY FOR PHASE 5.8 REAL VERIFICATION IMPLEMENTATION**；停止并等待 implementation 指令。

## Phase 5.8.1 Initial Fixture Attempt（2026-09-03）

- Source health read-only PASS；Type values `Type1`、`Type2`、`Type3`、`Type4`。
- Test-only runner 复用 `TestWorkspace`；file artifact cleanup PASS。初次 Codex harness 中 ArcPy GDB probe 复现 `-1073741819`；随后外部 native host 对同一 probe 返回 `ARCPY_OK=true`/exit 0，故初次失败分类为 `BLOCKED_BY_HARNESS`。
- 没有创建 owned FileGDB、owned source、second polygon、controlled project；当前 user `MyProject1.aprx` dirty，project mutation subset `BLOCKED_BY_FIXTURE`。
- MCP 只读入口最终仍为 initialize 200、tools/list=30、python_bridge_ping=pong；shared GDB TestDate top-level 仍只有 `Phase4Test.gdb`、physical count 115、observed locks 11；没有 real GP/mutation/tool acceptance。
- Build 0 errors/3×NU1900；Unit 68/68；Integration 20/20；完整记录见 [`PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md`](phases/PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md)。

**PHASE 5.8.1 INITIAL FIXTURE ATTEMPT BLOCKED_BY_ENVIRONMENT**；Recovery Gate 已通过；继续 5.8.1 fixture preparation，不进入 Phase 5.8.2。

## Phase 5.8.1 ArcGIS Python Execution-Context Recovery（2026-09-03）

- 当前 Bridge PID 16064 的真实 ArcPy discovery action PASS；`python_runtime_info` 的 `arcpyImportOk=true`。
- direct/propy standalone 的 core/stdlib/NumPy 1.26.4 PASS；`arcgisscripting`、`arcpy`、`ARCPY_NO_IMPORTS` 均 `-1073741819`。
- 无目标 shadowing；环境白名单没有发现 `PYTHONHOME/PYTHONPATH` 污染；Windows event log 未记录匹配 crash module。外部 native host 同一 probe 返回 `ARCPY_OK=true`、ArcGIS Pro 3.5/Build 57366、Advanced、exit 0；current harness failure 分类为 `BLOCKED_BY_HARNESS`。
- Build 0 errors/3×NU1900；Unit 68/68；Integration 20/20；最终 MCP 30-tool/ping/discovery baseline PASS；无 probe orphan。
- 完整报告：[`PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md`](phases/PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md)。

**PHASE 5.8.1 RECOVERY GATE PASS**；**FIXTURE PREPARATION IN PROGRESS / NOT YET ACCEPTED**；继续 5.8.1，停止，不进入 Phase 5.8.2。

## Phase 5.8.1 Native Host Fixture Resume Implementation（2026-09-04）

- test-only runner 的真实模式为 `create-retained`、`cleanup-probe`、`project-probe`；本轮不在当前 Codex harness 执行 ArcPy modes。
- `create-retained` 生成 `Phase58_<RunId>.gdb`、owned source、preparation-only ClipMask、controlled `.aprx` 和 manifest；成功结果含 `READY`、owned paths、manifest path、Python payload 和 `retained=true`。
- marker 在 ArcPy 写入前创建；source/copy/ClipMask/project health、datasource/reopen、selection/visibility 和 cleanup evidence 均通过 machine-readable JSON 返回。
- cleanup 发现 `.lock`/`.sr.lock` 时分类 `BLOCKED_BY_RUNTIME_LOCK`，不手工删除锁文件。
- 成功路径保留 workspace，失败路径 cleanup；fixture helper 0 warnings/0 errors；solution 0 errors/3×NU1900；Unit 68/68；Integration 20/20。
- 失败 marker JSON 记录 stage、exception/message、created paths、cleanup status、retained artifacts 和 exit code；用于 external native host 回传。

## 当前状态（历史 Phase 4 基线）
- 测试合计 **57/57 PASS**：
  - UnitTests 11，IntegrationTests 20（含 Phase 4 新增 12），ServerTests 26。
- Phase 4 新增工具覆盖：list_maps / get_map_info / get_layer_info / set_layer_visibility / add_layer / remove_layer / list_layouts / list_databases / query_attributes / get_field_info / get_feature_count / clear_selection / select_layer / buffer / clip / intersect / dissolve（Integration，Fake）；GP 工具=路由+参数组装（Fake 返回 NOT_IMPLEMENTED 的经验证行为）。

## 真实 ArcGIS Pro 3.5 验证（2026-08-31，经 MCP HTTP tools/call）
- tools/list：**23 个工具可发现**，category/executionType 正确透出。
- tools/call 核心工具（真实数据）：get_current_map、get_layers、get_layer_info、set_layer_visibility(修复 FindLayer 回退后 isError=False→true，且 get_layer_info 可见 isVisible=false)、get_project_info、list_databases、clear_selection → 全部成功。
- tools/call：get_feature_count / query_attributes → 因测试工程无 FeatureLayer 返回真实 `LAYER_NOT_FOUND`（验证类型/路径校验与不崩溃）。
- list_maps：经 Project.GetItems<Item>+MapFactory 路径返回空（已知 Phase 2 限制，非崩溃）。
- 服务器全程不崩溃；Phase 3 回归（initialize/ping）保持正常。

## 复现
```
. .\scripts\dev-env.ps1
dotnet build ArcGIS-Pro-MCP.sln -m:1 -c Debug   # 0 warning 0 error（-m:1 规避 SDK 8.0.424 多节点 MSB4276）
dotnet test ArcGIS-Pro-MCP.sln -m:1 -c Debug    # 需在可运行 testhost 的会话（本 Harness 会因 EnableRaisingEvents 限制 BLOCKED）
```
 真实 Pro：启动 Pro → MCP→Start → POST http://127.0.0.1:6520/mcp（initialize / tools/list / tools/call）。

## 2026-09-02 最终 PASS（干净重启 Pro + MCP HTTP 6520，25 工具最终包）
- tools/list=25；MCP/基础 6/Layer/GP(buffer/clip/intersect/dissolve)/Error/FeatureLayer属性/Selection 全 **PASS**。
- **query_attributes 无 fieldNames = PASS**（真实全字段 oid1-4，不再 Internal error）；带 fieldNames 亦 PASS。
- **2 处 bug 已修复并部署**：#1 LAYER_NOT_FOUND 回退、#2 query_attributes 几何序列化(跳过 Shape 字段)。
- get_dataset_info/get_raster_info：filesystem-only 占位（FileGDB FC → DATASET_NOT_FOUND，real 解析 NOT VERIFIED）。
- Automated dotnet test：**BLOCKED_BY_HARNESS**（本 Harness 进程访问限制，与项目无关；历史 57/57 为历史基线）。
- 复现：真实 Pro HTTP `POST http://127.0.0.1:6520/mcp`（initialize / tools/list / tools/call buffer/clip/intersect/dissolve/query_attributes/…）。

## Phase 5.7 Test Architecture & Coverage Preflight（2026-09-03）

- 源码 inventory：73 个 xUnit Fact；Unit 27、Integration 20、Server 26；无 Theory/Trait/Collection。
- 本轮执行：Build 0 errors/3×NU1900；Unit 27/27 PASS；Integration 20/20 PASS；Server 19/26 PASS，7 项 HttpListenerException: 句柄无效 为当前 HTTP harness blocker。
- 当前自动化 tool call：19/21 Native tool 有 Fake/Host call，buffer 是 4 个 GP 中唯一实际 call，5 个 Python tool 没有 facade 自动化 call；clip/intersect/dissolve 仅注册未单独执行；get_dataset_info/get_raster_info 无自动化 call。
- 静态 30 tool class、Composition registration 和真实 tools/list 名称集合一致；本轮 MCP 只读 initialize/tools/list/python_bridge_ping 均 PASS。
- 本轮只完成架构/coverage audit 和 planning，没有新增测试实现、没有重跑完整 Phase 4/5.6 或昂贵 Pro E2E。详细矩阵、P0/P1/P2 gaps 与推荐 subphases 见 Docs/phases/PHASE_05_7_TEST_ARCHITECTURE_COVERAGE_PREFLIGHT.md。

## Post-Phase-5.6 Transition Baseline（2026-09-03）

- 当前静态 Registry 与真实 Runtime 均为 **30 tools**；本轮只读确认 `initialize`、`tools/list`、`python_bridge_ping`，均 PASS。
- 本轮 build 为 `dotnet build -m:1 -c Debug --no-restore`：0 errors，3×NU1900 环境告警。
- 本轮没有重跑完整 Phase 5.6 lifecycle/guardrail suite，也没有连接 AI client/provider；Phase 5.7 Tests 是 `PHASE_05.md` roadmap 定义的下一阶段。

## Phase 5.7.1 Production Tool Contract Snapshot & Test Foundation（2026-09-03）

- 新增 UnitTests/ProductionToolContractSnapshot.cs 与 ProductionToolContractTests.cs。
- 测试通过反射调用真实 Composition.BuildRegistry()，没有在测试中复制 30 个 production registration。
- 30-tool contract 覆盖：count、unique names、exact name set、class↔registration、metadata、category、execution type、RequiresArcGIS、lower snake_case、schema object/type/properties/required/serialization、Python 5、GP 4 和 forbidden tool regression。
- Unit 从 27 增至 **38/38 PASS**；Integration **20/20 PASS**。
- Server **19/26 PASS**；7 项 HTTP transport 仍因当前宿主 HttpListener 句柄无效而 BLOCKED_BY_HARNESS。
- Build **0 errors / 3×NU1900**。
- 真实 MCP 只读 baseline：initialize HTTP 200；tools/list HTTP 200/count 30；runtime name set 与 production snapshot 一致。
- 本阶段未实现 Python facade 行为测试、GP 独立 routing/argument 测试、完整 error matrix、HTTP workaround、真实业务 E2E 或 Phase 5.7.2。

详细验收报告：Docs/phases/PHASE_05_7_1_TOOL_CONTRACT_TESTS.md。

## Phase 5.7.2 Tool Behavior Tests（2026-09-03）

- 新增 `PythonToolBehaviorTests`：5 个 production Python facade 的调用一次性、JSON/payload、路径转发、service unavailable/failure、error matrix 和 cancellation token。
- 新增 `GeoprocessingToolBehaviorTests`：`buffer`、`clip`、`intersect`、`dissolve` 独立经 `MCPToolRouter` 调用，锁定 GP tool name、values 顺序、optional/default 参数、错误和取消传播。
- 新增 `PlaceholderToolBehaviorTests`：通过当前构建的生产 `DataManagementService`/`RasterService`，使用 test-owned temp path 验证 existing/missing/invalid 的 filesystem-only 行为。
- 新增无 HTTP Server boundary test：生产 `BufferTool` service failure 经 `McpServer` 渲染为 `isError=true`，文本保留 `GEOPROCESSING_ERROR` 和消息。
- 新增 25 个 Unit Fact、1 个 Server Fact；本轮不创建测试项目，不修改 production Source/配置/package。

### 2026-09-03 automated result

- Build：0 errors，3×NU1900 环境告警。
- Unit：**63/63 PASS**；Integration：**20/20 PASS**。
- Server：**20/27 PASS**；7 项继续为 `HttpListenerException: 句柄无效` / `BLOCKED_BY_HARNESS`。
- Production snapshot：30 class / 30 registration / 30 runtime tool names 保持一致。
- Runtime 只读：initialize HTTP 200、tools/list=30、python_bridge_ping=pong；未运行 GP 或其他 business action。

详细报告：[`PHASE_05_7_2_TOOL_BEHAVIOR_TESTS.md`](phases/PHASE_05_7_2_TOOL_BEHAVIOR_TESTS.md)。

## Phase 5.7.3 Server Protocol & HTTP Harness Resolution（2026-09-03）

- 新增 14 个 HTTP-independent Server Fact：JSON-RPC ID、params/name/arguments shape、notification/batch contract、server timeout 和 caller cancellation。
- `MCPProtocolTests` **9/9 PASS**；`MCPServerTests` **25/25 PASS**；Server 完整结果 **34/41 PASS**。
- 7 个 HTTP transport tests 均在 `HttpListener.Start()` 报 `HttpListenerException (6): 句柄无效`；raw BCL fixed `16521`、dynamic `54450` 和独立 child process 复现同一 `SetupV2Config()` failure，分类 **Case H / BLOCKED_BY_HARNESS**。
- 没有修改 `HttpMcpTransport`、没有 skip/伪造 HTTP PASS、没有修改 URLACL/firewall/权限。真实 Pro 6520 listener 仍可用。
- Unit **63/63 PASS**；Integration **20/20 PASS**；Build **0 errors / 3×NU1900**。
- 真实 MCP 只读 baseline：initialize HTTP 200、tools/list=30 distinct、python_bridge_ping=pong；未执行其他 business action、AI/provider 或 Phase 5.8/6。

详细报告：[`PHASE_05_7_3_SERVER_PROTOCOL_HARNESS.md`](phases/PHASE_05_7_3_SERVER_PROTOCOL_HARNESS.md)。Phase 5.7 仍 IN PROGRESS，下一阶段为 Phase 5.7.4。

## Phase 5.7.4 Test Data Ownership & Mutation Safety（2026-09-03）

- 新增 `Tests/TestSupport/TestWorkspace.cs`，统一管理 `%TEMP%` test-owned root、ownership marker、run-scoped short dataset names、separator-aware path containment、child-process artifact registration、reparse refusal 和 non-throwing cleanup result。
- `PlaceholderToolBehaviorTests` 的 filesystem-only data 与 `PythonBridgeLifecycleTests` 的 script/log 均不依赖 `TestDate/Phase4Test.gdb`；manager 停止后才登记 child-created `requests.ndjson`。
- Pure fixture tests 覆盖 root/marker、unique naming、owned data、external sibling、escape、unowned-content block、late registration 和 idempotent cleanup；Unit **68/68 PASS**，Integration **20/20 PASS**。
- Real GP fixture policy：默认复制 immutable source 到 per-run owned workspace；输出以 `P57_<run>_<purpose>_<seq>` 命名；只清理 current-run ownership，锁未释放则保留并报告 `BLOCKED_BY_RUNTIME_LOCK`。
- Map/layer/selection mutation policy：snapshot → mutate → verify → finally restore；真实 Pro mutation tests serialized。当前 SelectionService 不提供 selected object IDs，selection restoration 保持 `NOT VERIFIED`，作为后续 dedicated project/API prerequisite。
- `TestDate/Phase4Test.gdb` 只读 inventory，历史 output/lock 不删除、不覆盖；HTTP Case H 不重开。详细计划见 [`PHASE_05_7_4_TEST_DATA_MUTATION_FIXTURES.md`](phases/PHASE_05_7_4_TEST_DATA_MUTATION_FIXTURES.md)。

## Phase 5.7.5 Test Consolidation & Acceptance（2026-09-03）

- Phase 5.7.5 = PASS；Phase 5.7 = PASS；Phase 5.8 = NOT STARTED。
- 三个测试项目发现 129 Facts：Unit 68、Integration 20、Server 41；非 HTTP 122 PASS，HTTP 7 项为 Case H / BLOCKED_BY_HARNESS。
- 30-tool final matrix、Python 5 facade、GP 4 routing、placeholder、error/protocol rendering、TestWorkspace、mutation policy 和 coverage diagnostic 已汇总。
- `TestDate/Phase4Test.gdb` remains shared historical data；real GP 使用 per-run owned workspace 的规则、锁保留规则和 mutation snapshot/restore 规则已固定。
- 完整矩阵和最终 gate：[`PHASE_05_7_5_TEST_ACCEPTANCE.md`](phases/PHASE_05_7_5_TEST_ACCEPTANCE.md)。

## Phase 5.8.1 Unit Regression / Process-Lock Recovery（2026-09-04）

- 修复前完整 Unit 67/68 的失败点是测试 helper `File.ReadAllLines(requests.ndjson)` 与 fake Python child 写日志的 Windows sharing race；根因分类为 `TEST DEFECT`。
- 仅在 `Tests/UnitTests/PythonBridgeLifecycleTests.cs` 捕获 transient `IOException`，不改变 production bridge、取消、quarantine、kill、generation 或 no-replay contract。
- 修复后目标测试 10/10，完整 Unit 5/5 次 68/68，Integration 20/20；solution Build 0 errors/3×NU1900。
- retained fixture health、external cleanup-probe 和 project-probe 均 PASS；生产 business tool/GP acceptance 仍未执行。
- 详细报告：[`PHASE_05_8_1_UNIT_REGRESSION_RECOVERY.md`](phases/PHASE_05_8_1_UNIT_REGRESSION_RECOVERY.md)。

**Regression Gate = PASS；Phase 5.8.1 Final Gate = PASS CANDIDATE，等待独立 Gate Keeper；不进入 Phase 5.8.2。**

## Phase 5.8.1 Formal Acceptance — Repository State Reconciliation（2026-09-04）

- Independent Gate Keeper: **FORMALLY ACCEPT — PHASE 5.8.1 PASS**；**READY FOR PHASE 5.8.2**。
- Accepted fixture/evidence: source health、external native ArcPy、owned FileGDB、`P58_TestPolygons`、`P58_ClipMask`、controlled APRX、explicit reopen、owned datasource、selection/visibility baseline、cleanup/project cleanup probes、manifest、no orphan process 均 PASS。
- Automated baseline: Build 0 errors/3 `NU1900`；Unit 68/68；Integration 20/20；regression root cause **TEST DEFECT**；production Python Bridge lifecycle semantics 未修改。
- Retained identity: `P57_B8C6FE8E`；fixture health unchanged，未重建/重命名/移动/修改；shared GDB、`MyProject1.aprx`、历史 outputs 和未知 locks untouched。
- 历史 `PASS CANDIDATE`、67/68、Recovery evidence 保留。`list_maps`/`get_map_info` partial、dataset/raster limited implementation、`isDirty` unavailable、HTTP disconnect NOT VERIFIED、7 HTTP tests BLOCKED_BY_HARNESS 和 `select_layer` 无 OID 参数继续保留。

```text
Phase 5.8.2 — Native Real Verification
NOT STARTED / READY TO START
```

本轮未执行 Native mutation。
