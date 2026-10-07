# Geoprocessing（GEOPROCESSING）

## 统一 GP 执行器
Phase 4 建立统一 `GeoprocessingExecutor`（Compatibility/Services），封装 ArcGIS Pro Geoprocessing API：

```
Tool → IGeoprocessingService.RunToolAsync → GeoprocessingExecutor.ExecuteAsync → ArcGIS.Desktop.Core.Geoprocessing.Geoprocessing.ExecuteToolAsync → IGPResult → OperationResult
```

- 使用 API（Pro 3.5 真实验证）：`ArcGIS.Desktop.Core.Geoprocessing.Geoprocessing.ExecuteToolAsync(tool, IEnumerable<string> values, ..., CancellationToken, ..., GPExecuteToolFlags.Default)` → `IGPResult`。
- 保留：`IGPResult.Messages / ErrorMessages / IsFailed / IsCanceled / ReturnValue`。
- 结果映射：成功 → `OperationResult<GeoprocessingResult>`（tool、ReturnValue、messages）；失败 → `GEOPROCESSING_ERROR`（含错误码 + GP messages）；取消 → `CANCELLED`。
- 不把大量 GP 业务逻辑复制进各工具；GP 工具只负责把参数按工具顺序组装为 `values`。

## 已实现的 GP 工具（首批）
| 工具 | 工具名(API) | 参数（顺序 values） |
|------|------------|---------------------|
| buffer | `Buffer_analysis` | input, output, "{distance} {unit}", "FULL", "ROUND", "ALL"/"NONE" |
| clip | `Clip_analysis` | input, clipFeatures, output |
| intersect | `Intersect_analysis` | inputs("A;B"), output, "ALL", "" |
| dissolve | `Dissolve_management` | input, output, dissolveField, "" |

### 覆写策略（Phase 8.5.6 / D-021，F6）

4 个 GP 写工具（`buffer` / `clip` / `dissolve` / `intersect`）新增参数 **`overwrite`（boolean，默认 `false`）**，
语义为 **S1（默认拒绝）+ S2（显式开关）**：

| 场景 | 行为 |
|---|---|
| 输出不存在（无论是否传 `overwrite`） | 正常执行 |
| 输出已存在 + `overwrite` 缺省 / `false` | 返回 **`OUTPUT_EXISTS`**，**不执行 GP、零状态变更、响应不带 stateProof** |
| 输出已存在 + `overwrite: true` | 维持原行为执行（**含覆写风险**），成功响应消息附 `overwrite=true: existing output ... was replaced` 审计提示 |
| 存在性**不可判定**（探测失败/能力不足）+ 未授权 | **保守拒绝** `OUTPUT_EXISTS`（红线：绝不默认放行覆写） |

判定语义分叉（D-017 教训）：文件型输出走文件系统语义；**`.gdb` 容器内输出复用 D-017 已落地的 SDK
Geodatabase 定义枚举**（`IGeoprocessingService.CheckOutputExistsAsync`），文件语义在容器内恒为"不存在"，不可用。

> **破坏性变更**：此前"对同一输出反复重跑"的用法现在会报 `OUTPUT_EXISTS`。迁移：显式 `overwrite: true`，或先删除输出。

## 说明 / 限制
- GP 输入/输出当前使用**数据集路径字符串**；layer-name→path 自动解析为后续增强。
- GP 工具集成测试验证了「工具→Router→Host 调用链 + 参数组装」（Fake 占位返回 NOT_IMPLEMENTED）；**真实 GP 运行（buffer/clip 等）尚未在 Pro 3.5 端到端验证**（需含要素数据工程）。
- 错误不导致 Server/HTTP 崩溃；GP 失败保留 messages。

## 未来（Phase 5+）
- Python/ArcPy 执行器（单独安全子系统，不在 Phase 4 启动 6511）。
- layer-name 解析、环境设置（环境中提供环境变量）增强。

## Phase 5.7.4 Test Fixture Policy（2026-09-03）

- `TestDate/Phase4Test.gdb` 是 shared historical verification workspace，不是 pristine fixture；不得由测试 cleanup、overwrite 或按名称猜测删除其中的历史 output/lock。
- Real GP verification 默认使用 per-run copied FileGDB/owned workspace：immutable source 只读复制，output 使用 `P57_<run>_<purpose>_<seq>` 这类短、ASCII、ArcGIS-compatible 名称；Option C fresh FileGDB 仅在复制/创建能力经验证后采用。
- 每个 GP output 至少验证 operation success、returned path、exists、可 Describe、geometry/data type 和基本 feature-count sanity；没有固定 fixture 依据时不 assert exact count。
- 只清理当前 run 明确登记的 owned paths。所有 ArcGIS/ArcPy 引用释放、GP 完成、Bridge/service dispose 后才尝试 cleanup；仍有锁时保留路径并报告 `BLOCKED_BY_RUNTIME_LOCK`，不手工删除 `.lock`/`.sr.lock`。
- 本阶段只完成 fixture/policy 与 pure .NET tests，未执行完整 real GP regression；详细 matrix 见 `phases/PHASE_05_7_4_TEST_DATA_MUTATION_FIXTURES.md`。

## Phase 5.7.5 Test Consolidation & Acceptance（2026-09-03）

- 四个 GP tool 的独立 routing、ArcGIS GP tool name、values 顺序、optional/default、INVALID_ARGUMENT、GEOPROCESSING_ERROR、OUTPUT_EXISTS、CANCELLED 和 token forwarding 已由 GBT 覆盖。
- 本轮未执行 full real GP；real output/Describe/geometry/count/cleanup 属于 Phase 5.8/5.10。
- 真实 GP 测试继续使用 per-run copied source、owned outputs、短 ASCII P57 名称；不写入 shared TestDate/Phase4Test.gdb，不手工删除 lock。
- Phase 5.7.5 总报告：[`PHASE_05_7_5_TEST_ACCEPTANCE.md`](phases/PHASE_05_7_5_TEST_ACCEPTANCE.md)。

## Phase 5.8 Real Verification Preflight（2026-09-03）

- 本轮只完成真实 GP/ArcPy 验证架构、夹具、scope boundary 和 acceptance preflight；没有执行 real GP，也没有创建/覆盖 shared GDB output。
- 四个 GP 工具的后续矩阵固定为：owned input/output、success、returned path、exists、Describe、geometry/spatial reference、feature-count sanity、GP messages 和 cleanup classification。
- `buffer` 使用 owned `TestPolygons` copy，NONE/ALL 分开；`clip` 需要第二个健康 owned polygon；`intersect` 至少两个 owned inputs；`dissolve` 先 inspect `Type` 实际值，至少做 dissolve-all，再考虑 by-field。
- FileGDB copy 方案须在后续 5.8.1 验证 ArcPy/ArcGIS supported copy、schema/index/metadata、locks 和 active Pro references；直接 directory copy 不能作为活跃 GDB 的唯一安全方案。
- `TestDate/Phase4Test.gdb` 继续是 shared historical source，只读；所有 output 和 derived input 使用 `Tests/TestSupport/TestWorkspace` 的 current-run ownership，锁不手工删除。
- 完整报告：[`PHASE_05_8_REAL_VERIFICATION_PREFLIGHT.md`](phases/PHASE_05_8_REAL_VERIFICATION_PREFLIGHT.md)。完整 MCP→Router→Native/GP/Python→ArcGIS/ArcPy chain 留在 Phase 5.9。

## Phase 5.8.1 Initial Fixture Attempt（2026-09-03）

- 已决定后续采用 fresh owned FileGDB + `CopyFeatures`，只复制 `TestPolygons`；不复制 shared historical GDB，不用 production MCP `buffer` 制造 ClipMask。
- test-only ArcPy helper 已实现固定的 CreateFileGDB/CopyFeatures/ArcPy preparation recipe；初次 Codex harness 中 standalone import 以 `-1073741819` 退出，但外部 native host 同一 probe 已 `ARCPY_OK=true`/exit 0，因此 Recovery Gate 已 PASS。GDB、owned source、ClipMask 和 FileGDB cleanup lifecycle 仍 `NOT VERIFIED`。
- `buffer` preparation 与未来 production buffer acceptance 明确分离；本轮未执行任何 production GP tool。
- 当前 user project `MyProject1.aprx` dirty 且禁止 mutation；没有 controlled `.aprx`，project mutation subset `BLOCKED_BY_FIXTURE`。
- 详细报告：[`PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md`](phases/PHASE_05_8_1_REAL_FIXTURE_PREPARATION.md)。

## Phase 5.8.1 ArcGIS Python Execution-Context Recovery（2026-09-03）

- GP fixture route 的 ArcPy 前置条件已在外部 native host 恢复：同一 probe 输出 `ARCPY_OK=true`、ArcGIS Pro 3.5/Build 57366、LicenseLevel Advanced、exit code 0；当前 Codex harness 的 standalone crash 分类为 `BLOCKED_BY_HARNESS`。
- 因此没有执行 `CreateFileGDB`、`CopyFeatures`、`Describe`、`GetCount`、production GP tool 或 cleanup lifecycle；没有产生 owned GDB/ClipMask/output。
- 后续仍采用 fresh owned FileGDB + `CopyFeatures` 的原策略；下一步在同一 external native prompt 激活 `proenv.bat`，运行 test-only fixture runner 的 `create-retained`，仍留在 5.8.1 fixture gate。
- 详细 Recovery 证据：[`PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md`](phases/PHASE_05_8_1_ARCPY_EXECUTION_CONTEXT_RECOVERY.md)。

**GP/ArcPy fixture NOT READY**；**Phase 5.8.1 Recovery Gate PASS；fixture preparation IN PROGRESS / NOT YET ACCEPTED**；不进入 Phase 5.8.2。

## Phase 5.8.1 Native Host Fixture Resume Implementation（2026-09-04）

- `create-retained` 是 test-only fixture preparation，不是 production GP acceptance；它在 external native ArcPy host 中准备 `Phase58_<RunId>.gdb`、copied source、preparation-only `P58_ClipMask` 和 controlled project。
- ClipMask 由 owned source 上的 direct ArcPy `Buffer` 创建，不调用 production MCP `buffer`；因此 fixture setup PASS 不等于 business GP verification PASS。
- `cleanup-probe` 和 `project-probe` 使用独立 disposable root，在 ArcPy 进程退出后验证 current-run GDB/project cleanup；retained fixture 不参与 cleanup probe。
- 若 disposable root 仍有 `.lock`/`.sr.lock`，cleanup 分类为 `BLOCKED_BY_RUNTIME_LOCK`，不手工删除锁文件。
- 本轮未执行 ArcPy 创建；fixture helper 0 warnings/0 errors；solution 0 errors/3×NU1900；Unit 68/68；Integration 20/20。正式 GP acceptance 仍未开始。
- External runner 通过 marker JSON 返回 stage、ArcPy/异常消息、owned paths、cleanup status 和 retained artifacts；在收到外部结果前尚无新 GP fixture。

## Phase 5.8.1 Unit Regression / Process-Lock Recovery（2026-09-04）

- retained fixture 已通过 external native ArcPy preparation；其 `P58_ClipMask` 仍是 preparation-only direct ArcPy Buffer 产物，不等同于 production `buffer` acceptance。
- Unit 回归根因在 test-only `requests.ndjson` 读取 helper 的 Windows sharing race；最小修复未改变任何 GP executor/tool 或 Python Bridge production code。
- 修复后目标测试 10/10、Unit 68/68（连续 5 次）、Integration 20/20、Build 0 errors/3×NU1900；real production GP acceptance 仍 NOT PERFORMED。
- 详细回归报告：[`PHASE_05_8_1_UNIT_REGRESSION_RECOVERY.md`](phases/PHASE_05_8_1_UNIT_REGRESSION_RECOVERY.md)。

**Phase 5.8.1 Regression Gate = PASS；Final Gate = PASS CANDIDATE，等待独立审核；不进入 Phase 5.8.2。**

## Phase 5.8.1 Formal Acceptance — Repository State Reconciliation（2026-09-04）

- Independent Gate Keeper formally accepted Phase 5.8.1: **PASS**；Phase 5.8.2 is **NOT STARTED / READY TO START**。
- Fixture Preparation、Regression Gate、source/owned fixture/controlled APRX/manifest/cleanup evidence、Build、Unit 68/68 和 Integration 20/20 均按权威记录保持 PASS。
- Retained fixture `P57_B8C6FE8E` 未重建、未修改；`P58_ClipMask` 仍是 preparation-only direct ArcPy Buffer 产物，不等同于 production GP acceptance。
- Real production GP acceptance 仍未开始；不得将本次 Phase 5.8.1 fixture acceptance 解释为 Phase 5.8.3 或 Phase 5.9 acceptance。
