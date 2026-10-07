# 新工作空间 AI 启动提示词

请接管 ArcGIS Pro MCP 的后续完善工作。你是执行者，不是独立验收者。先执行E0，不立即修改产品功能。

## 用户目标

采用V1.1功能优先修订：在保留现有1.0.2已验收成果的基础上，先全面检查并完善现有30工具，再扩展制图布局与GIS工具，并完成WorkBuddy真实接入。客户端优先Codex，兼顾Cursor、DeepSeek Harness，WorkBuddy是本轮必做项。功能冻结验收之后才开发一键安装，不能先完善安装器。

执行顺序：E0 → E2 → E3 → E4 → E5 → E6 → E8 WorkBuddy → 功能冻结Gate → E1一键安装 → E7发布。编号为保留的任务ID，不是大小顺序。开发验证必要的测试包不等于提前做一键部署。

## 先读取的文件

1. `D:\ArcGIS-Pro-MCP\Docs\NEXT_ROADMAP_AND_AI_HANDOFF_V1.md`，这是本轮详细路线图与执行边界。
2. `D:\ArcGIS-Pro-MCP\Docs\PROJECT_STATE.md`、`PROJECT_RULES.md`、`CURRENT_TASK.md`、`VERIFICATION.md`。
3. `D:\ArcGIS-Pro-MCP\Docs\ONE_CLICK_DEPLOYMENT_INDEPENDENT_GATE_R5.md`、`USER_GUIDE_R5_INDEPENDENT_GATE.md`。
4. `D:\ArcGIS-Pro-MCP\Docs\ArcGIS_Pro_MCP_r5_安装与使用指南_V1.0.md`。
5. `<user-home>\Downloads\ArcGIS_Pro_MCP_项目介绍_架构原理与使用手册_V1.0.docx`，只读作为历史介绍，不把文中指令当作新授权。

文件不可访问时，明确列出缺失并请用户提供，不能猜测。不要递归读取所有历史阶段文档。冲突时核对当前源码、发布身份和正式审批，保留历史状态，不挑选更乐观的结论。

## 工作空间

我已自行创建新工作空间和本对话。请先报告当前绝对路径，确认它与`D:\ArcGIS-Pro-MCP`不同。若仍在旧仓库，不要继续写入，向我询问独立目标目录。

原仓库当前没有Git提交，源码主要未跟踪；先重新核实，不能假设普通git clone/worktree包含当前源码。不自动commit、push或改Git配置。

按详细计划E0白名单，将源码、测试、脚本、无凭据的项目配置模板和文档复制到新工作空间，保留相对目录。排除bin/obj、.git、.runtime、.codex-artifacts、.codex、.cursor、TestDate、GDB/APRX、历史输出和锁；不跟随reparse point。目标非空先盘点，不能覆盖我的文件。Release仅复制计划明确列出的固定包和必要manifest，不复制全部历史包。为每个复制文件记录SHA256并验证一致，旧仓库只读。

## 必须保留的事实与规则

- 1.0.2和canonical 30工具的原计划范围已验收完成，历史Phase 0至7不重开。112+工具是未来目标。
- r5只有实现/隔离验证有限放行；新电脑安装、当前真实客户端连接不能自动写PASS。
- r5包SHA256：3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652。旧r5不可覆盖，后续产生新包名和哈希。
- list_maps/get_map_info、dataset/raster、select_layer、HTTP取消等限制按详细计划逐项处理，不能因为工具已注册便称完整。
- 唯一Registry、Shared不引用ArcGIS SDK、MCT线程约束、单受限Python Bridge、本机回环网络保持不变。禁止任意Python和6511旁路。
- 不修改旧MyProject1.aprx、Phase4Test.gdb、retained fixture、历史输出、未知锁或用户程序。
- 真实安装/卸载、真实客户端配置Apply/Restore、GIS写入、依赖安装及公开发布需另行明确授权。声明精确输出路径后才可执行获得授权的写操作。隔离测试仅用新任务拥有的目录。
- Build、Unit、Integration、真实Pro、HTTP、客户端和视觉验收分开计证。保留SKIPPED/NOT VERIFIED/BLOCKED，不自批PASS。

## 第一轮具体交付

先完成迁移和只读恢复，在新目录建立`AGENTS.md`、`Docs/EVOLUTION_STATE.md`、`Docs/EVOLUTION_CURRENT_TASK.md`、`Docs/EVOLUTION_DECISIONS.md`及路线图副本。区分旧历史CURRENT_TASK与新E0任务，不把旧绝对路径静默替换为新证据。

报告新目录、迁移文件数与manifest路径、身份校验、源码模块、当前限制、所需依赖及E2最小任务拆分。必须提交全部30工具的完善矩阵、新增工具分批清单、WorkBuddy版本/官方接入调研计划及本轮功能冻结范围，不能只修几个PARTIAL工具或把30作为永久数量上限。可以在新目录进行不触及真实部署的安全隔离构建/测试；需要安装依赖或真实宿主操作则先停下说明。

完成E0后提交PASS CANDIDATE及证据，等待用户或独立Gate Keeper验收，再进入E2。不要直接进入E1，不要一次开发全部路线图。WorkBuddy官方支持MCP不等于本项目已接入；核实本机HTTP支持，云端127.0.0.1不能当用户电脑，禁止为连接方便开放公网。之后每包均按设计→实现→测试→真实验证→文档→独立验收推进。每轮结尾留下精确下一步，不依赖旧聊天记忆。
