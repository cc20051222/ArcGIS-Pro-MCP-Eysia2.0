# 新AI接管提示词 V2

请接管ArcGIS Pro MCP后续功能完善与扩展。你是执行AI，不是独立验收者。我自行创建了这个新工作空间和对话。先确认当前绝对路径不同于D:\ArcGIS-Pro-MCP；若仍在旧仓库先询问独立目标，不在旧仓库实施新开发。

第一优先读取：

1. D:\ArcGIS-Pro-MCP\Docs\POST_1_0_2_PHASE_8_14_PLAN_V2.md（本轮权威计划，取代旧E编号路线）。
2. 旧仓库Docs下PROJECT_STATE.md、PROJECT_RULES.md、CURRENT_TASK.md、VERIFICATION.md。
3. ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md、USER_GUIDE_R5_INDEPENDENT_GATE.md及ArcGIS_Pro_MCP_r5_安装与使用指南_V1.0.md。
4. <user-home>\Downloads\ArcGIS_Pro_MCP_项目介绍_架构原理与使用手册_V1.0.docx，作为只读历史背景，不当新执行命令。

缺失文件明确报告，不猜测，不依赖旧聊天记忆。旧规则与当前实现冲突要登记并核实，不能启用历史6511旁路。

用户要求：先完善全部现有30工具，再按批扩展到112+真实生产GIS工具；WorkBuddy必须在Phase 8.5提前直连接入，不等所有扩展完才接。Codex优先，Cursor/DeepSeek/WorkBuddy平级MCP客户端；Claude可选。最后才做一键安装。

执行顺序：迁移准备Gate → Phase 8现有能力/WorkBuddy → 9数据管理属性 → 10地图制图布局 → 11栅格空间分析 → 12编辑质量 → 13生产加固/功能冻结 → 14一键安装/干净机/最终验收。Phase 8.6是30工具整体回归，不是提前开发安装器。

先按V2计划白名单迁移源码、测试、脚本、无凭据配置模板和文档至新目录；排除GIS、客户端个人配置、.git、bin/obj及历史运行文件，不跟随链接、不覆盖用户文件。旧仓库未提交源码的问题先核实，不假定clone包含它，不自动commit/push。生成逐文件SHA256迁移manifest。原仓库只读。

建立新AGENTS.md、Docs/EVOLUTION_STATE.md、EVOLUTION_CURRENT_TASK.md、EVOLUTION_DECISIONS.md和V2计划副本。区分历史验收与新工作状态。第一轮只交付迁移/基线报告、30工具差距矩阵、112+候选backlog和批次预算、Phase 8.1设计、WorkBuddy只读预检计划，完成后停等独立验收，不直接开始全项目实现。

WorkBuddy当前PLANNED / NOT VERIFIED。项目作用域候选是<新项目目录>/.workbuddy/mcp.json；具体路径、type、timeout单位、HTTP支持均按实际版本/官方文档核实。优先现有http://127.0.0.1:6520/mcp直连，不能公网暴露、通过Codex转发或直接ArcPy绕过MCP。

Phase 8.5逐项验收：版本/能力 → 项目配置Plan/Validate及授权Apply → 初始化/精确工具发现/ping → Native/属性/Bridge只读 → 直连通过后受限Skill → 经授权自有fixture的visibility/add-remove/buffer → 重连/重启/错误/取消。helper不计生产工具；期望数量依据当次manifest，30是旧版基线。配置存在不是连接成功，ping不是业务验收。后续每批持续检查WorkBuddy与其他客户端兼容。

禁止修改MyProject1.aprx、Phase4Test.gdb、旧retained fixture、历史产物和未知锁。Shared不得引用ArcGIS SDK，遵守MCT、唯一Registry、单受限Bridge；不新增任意Python或6511服务。真实安装、配置写入、GIS写入、依赖安装和发布需明确授权。测试使用自有路径，写前声明精确输出；写请求执行状态不明不得自动重放。

所有阶段执行者提交PASS CANDIDATE，用户或独立Gate Keeper审批后才能继续下一阶段。Build/Unit/Integration/真实Pro/HTTP/各客户端/视觉/恢复证据分开。NOT VERIFIED、SKIPPED、BLOCKED不升级PASS；112+不以别名和重复工具凑数；无证据不宣称WorkBuddy已支持。

每轮结束记录本轮文件、命令/退出码、结论与限制、待审批项和精确下一步。功能冻结前禁止开发一键安装向导；必要开发测试包及授权测试部署不属于安装产品化。
