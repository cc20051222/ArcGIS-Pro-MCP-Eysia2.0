# 文件夹自动处理、作业和批处理

## 文件夹到成果

1. 用 `scan_data_folder` 建清单；原件只读，跳过项有原因，同名基名、复合格式、GDB 成员与扫描上限明确列出。
2. 使用当前 `suggest_workflow`、`validate_plan` 或文件夹工具的计划回显准备实际可达步骤。`validate_plan` 只证明它实现的结构/必填项检查，不证明 CRS、方法、许可或输出质量。
3. `load_folder_data` / `apply_processing_plan` 按当前 schema 先走默认 `dryRun`，检查 `ambiguities`、`defaults`、目标位置、输出命名、覆盖和新增地图/图层等副作用。计划有歧义不直接确认为可执行。
4. 用户已明确授权这份具体范围后，按后端要求提交 `confirm` 和实际执行参数；不伪造批准摘要，不把 `dryRun=true` 当产物已生成。工程里已有同名项或用户未保存编辑需先处理身份与范围。
5. 保存返回的 `jobId` / checkpoint / 步骤结果；用 `get_job_status`、`get_job_report`、`list_jobs` 查询。不从概念图发明 resume API，也不把恢复 checkpoint 当成重新授权。
6. 对每个已完成成员核真实输出，failed/skipped/cancelled/unknown 与成功使用原任务分母，给出整批和逐项状态。

需要让用户观看流程与中间成果时，按 [场景适配与进度展示](adaptive-flow-and-progress.md) 给出计划与真实计数。`get_job_status` 是读取，不能用轮询推进分片；`apply_processing_plan` 给子调用补入 `target`，不自动替换各工具的 `input` / `output`。异构目标、跨步依赖和输出命名必须逐项确认绑定可达，不能把它当通用 DAG 引擎。

## run_batch 的实际限制

当前 `run_batch` 最多 50 项、总预算上限 30000 ms，始终顺序执行。禁止嵌套 `run_batch` 和在批内调用 `set_readonly_mode`；`allowedTools` 只能收窄。每项仍经必填、只读、守卫和 confirm；批处理不增加权限。依赖分析/写入默认失败即停，不为吞错随意打开 `continueOnError`。

51 项任务可在准备阶段拆成合规小批或用实际支持的持久作业机制；30 秒预算不适合长耗时 GP，不把超时批整批再发。批处理不保证事务原子性，不声称失败会撤销先前成功写入。

地图/选择/环境/编辑/同一 GDB 的共享状态不交给多个 AI 同时写。即使外部有多执行端，也要按照对象和依赖隔离并核现有调度能力；Skill 不创建后台并行代理或占用新宿主。

## 取消与恢复

只对用户指定作业发 `cancel_job`；`accepted` / `awaiting` 不是 `cancelled`。继续只读查询最终状态及已落结果；确认停止前不释放为可重跑，不清理未知输出/锁。用户已取消或暂停时，恢复执行须有新的明确意图，并重新核输入、输出、许可与范围。旧 jobId 找不到时展示事实与可核结果，不虚构成功或自动从头重跑。
