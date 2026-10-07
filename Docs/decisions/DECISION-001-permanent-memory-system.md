# DECISION-001-permanent-memory-system

## Context
项目规模跨多 Phase，聊天上下文不可持续依赖；需要让新会话脱离聊天历史独立恢复项目状态。

## Decision
建立长期记忆系统：
- `Docs/PROJECT_STATE.md` = 项目第一入口（简洁，≤约 300 行）。
- `Docs/PROJECT_RULES.md` = 永久规则。
- `Docs/CURRENT_TASK.md` = 当前任务（永远反映现在做什么）。
- `Docs/VERIFICATION.md` = 最终真实验证结果。
- `Docs/phases/PHASE_00..07.md` = 每 Phase 最终状态。
- `Docs/decisions/DECISION-XXX-*.md` = 重要架构决策。
- 读取优先级：PROJECT_STATE > PROJECT_RULES > CURRENT_TASK > 当前 Phase > 当前任务相关代码。

## Reason
满足 Rule：仓库 > 文档 > 当前代码 > 当前聊天历史；实现上下文预算控制；防止因上下文丢失而推倒既有架构。

## Alternatives
- 仅靠聊天历史记忆（否决：不可持续）。
- 单一超大状态文件（否决：难以阅读）。

## Consequences
- 每个 Phase 结束必须更新记忆文件。
- 需推翻既有 Decision 时必须新建 Decision，不得删除旧 Decision。

## Date
2026-08-31

## Phase
全程（来自初始化指令）
