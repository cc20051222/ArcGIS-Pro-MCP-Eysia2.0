# 一键部署 r5 独立审批记录

> **历史时点声明（D-129 README 链接陈旧面补声明批 · 2026-10-06 加入；仅加本头部，正文逐字未改）**
> 本文基线时点 ＝ **2026-09-10（文内唯一 ISO 日期实测；与正文证据目录名 `.runtime/one-click-deployment-r5/` 同日互证）**；现行基线见 `AGENTS.md` §4 ＋ `Docs/PROJECT_CLOSEOUT_20261006.md`；
> 本文历史内容按 `AGENTS.md` §2 文档优先级第 5 条**仅作历史来源，不构成当前授权**。
> 时点口径为机械复算：取文内 ISO 日期（2026-mm-dd）极值；无日期者按正文阶段表述推定。
> ★ 本头部点名的当时点位（由本批扫描件机械实测，非人工摘录；现值见上列两件）：`-r5`・`3924F9B3`

审批日期：2026-09-10。审批角色：Independent Gate Keeper。

## 决定与范围

批准 r5 进入用户实机试用验收。实现与本机隔离自动化验证范围：PASS。此决定不等于全新电脑部署、真实 AI 客户端连接或公开发布整体通过；这些仍为 NOT VERIFIED。既有 Phase 7 与生产 1.0.2 接受范围不变。

唯一审批包：`Release/ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip`。

SHA256：`3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652`。

## 独立证据

Gate Keeper 实际运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tests/OneClickDeployment/one-click-deployment.tests.ps1`，退出码 0，122 项断言通过。

本轮证据目录：`.runtime/one-click-deployment-r5/run-20260910-052628-6f6d5d15ae56481092d378c74dd8e856`。

覆盖最终 ZIP 校验、单客户端范围、隔离配置/恢复、取消、子安装完成后硬中断、恢复索引写失败、旧事务并存、重复执行保护。隔离安装使用 TestMode，不冒充真实 RegisterAddIn 安装。预置 GUI 状态测试为模拟；真实只读 Preflight/Diagnose 按钮回调另行执行。

独立逐图查看本轮 gui-button-smoke 的 01-initial、02-preflight-button、03-diagnose-button：插件目标及 GUID、中文路径与日志可读，控件恢复，预检 PASS，诊断 WAITING_USER_START，无异常弹窗。等待服务不记为连接 PASS。

前轮独立运行在第108项后因中文截图路径解码失败而失败，其证据 `.runtime/one-click-deployment-r5/run-20260910-042911-27dcb7b0bbdc4dad9d88693aab5f2afc` 保留。测试编码修订后重新完整运行通过；r5 ZIP 哈希未变化。r1-r4 不在本审批范围。

## 用户下一步

在满足 Windows x64、ArcGIS Pro 3.5 与 .NET 8 条件的测试电脑上，校验并解压 r5，运行 ONE-CLICK-SETUP.cmd。选择 Codex 与项目目录，检查目标后执行部署。按提示启动 ArcGIS Pro 的 MCP 服务，再在该项目的 Codex 中验证 tools/list、ping 以及两个只读工具；重启复测，并在测试环境验证卸载/恢复。

保留安装日志、环境版本、包哈希、客户端工具发现和调用结果。未执行的实际安装、真实客户端及 clean-machine 项目继续标 NOT VERIFIED。用户提交证据后另行审批实机范围。
