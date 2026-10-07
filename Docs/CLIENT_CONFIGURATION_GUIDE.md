# ArcGIS Pro MCP 客户端配置指南

> 当前用户首选入口是 `scripts/user-workflow.ps1 -Action Plan` 或 `-Action Validate`。本文件中的 `client-config.ps1` 命令是底层 configurator 入口；Apply/Restore 仍必须显式指定且只指定一个 `-Client`，不存在 `ApplyAll`。

## 固定连接信息

Phase 7.8 Final Handoff is `FORMALLY ACCEPTED / PASS`; Phase 7 overall and
the 1.0.2 planned release scope remain `PASS CANDIDATE / AWAITING INDEPENDENT
GATE KEEPER`. This guide does not claim whole-project completion.

- Server name / namespace: arcgis-pro-mcp
- Streamable HTTP endpoint: http://127.0.0.1:6520/mcp
- Canonical production tools: 30
- mcp_auth is a client-scoped helper and is never counted as a production tool
- 不在这些模板中填写 provider、model、login、token、header、password 或其他凭据

## 使用顺序

1. 先启动受控的 ArcGIS Pro 工程和 Add-in Server。
2. 将对应模板导入或放入客户端配置位置。
3. 确认客户端识别 namespace arcgis-pro-mcp。
4. 确认工具集合为 canonical 30。
5. 仅使用安全只读 ping 做连接检查。

## 客户端

| Client | Priority | Scope / format | Template | Apply |
|---|---|---|---|---|
| Codex | P0 required | project TOML | Config/client-templates/codex.toml.fragment | managed fragment |
| Cursor | P1 required | project JSON | Config/client-templates/cursor.mcp.json | structured merge |
| DeepSeek Harness | P1 required | external YAML patch | Config/client-templates/deepseek.cordis.patch.yml | managed fragment |
| Claude Desktop | P2 optional | user JSON | Config/client-templates/claude-desktop.mcp.json | template-only validation |

Codex 和 DeepSeek 的 TOML/YAML 完整 merge 依赖客户端语法，仓库 configurator 只对受管块执行精确替换；没有受管块时只追加模板块，不进行脆弱字符串重写。Claude Desktop 本阶段只校验模板，不安装、不连接。

## Validate / Plan

只读校验全部客户端：

    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\client-config.ps1 -Action Plan -Json
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\client-config.ps1 -Action Validate -Json

真实配置的 Apply/Restore 仍必须显式指定单一客户端。Phase 7.7 已分别正式接受 Codex P0、Cursor P1 和 DeepSeek Harness P1 fresh client evidence；Claude Desktop 仍为 template-only，未获得 fresh connection/tools-call acceptance。不要把任何 client helper `mcp_auth` 计入 canonical production tool count `30`。

## Apply / Restore

Apply 和 Restore 必须显式指定 `-Client`，且一次只能处理一个明确客户端；未指定客户端时会在读取任何客户端前失败，不会发生跨客户端部分提交。Apply 会在首次写入前创建旁车备份和元数据，使用同目录临时文件后原子替换；重复 Apply 不覆盖原始备份，并拒绝偏离最近一次受管写入的用户编辑。Restore 从备份恢复，或只删除本事务新建的目标文件，并校验 SHA-256。JSON 采用结构化解析并保留无关项；受管 fragment 只触碰 begin/end 标记范围。

任何检测到凭据字段、错误 endpoint、错误 server name 或非法 JSON 都拒绝操作。失败不应留下半写文件。当前任务没有对真实用户路径执行 Apply/Restore，尤其没有改写 DeepSeek profile。

## 故障与恢复

确认 Pro first、6520 loopback listener、server namespace 和 endpoint。若配置被误改，在停止客户端后使用同一 configurator 的 Restore；不要用服务器证据代替客户端连接证据，也不要把叙述性工具数或 helper surface 当作实际 production set。当前 accepted release identity 是 installed `1.0.2`，但 Apply/Restore 仍属于显式授权的用户配置操作。
