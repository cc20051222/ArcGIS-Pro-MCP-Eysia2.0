---
name: Feature request / 功能请求
about: 提出新功能或工具改进（Suggest an idea for this project）
title: "[feat] "
labels: enhancement
---

## 想解决的问题 / Problem

<!-- 请说明在 ArcGIS Pro 内的具体工作流痛点，以及当前绕行方式。 -->

## 期望的方案 / Proposed solution

## 期望的接口形态 / Tool surface

- 建议工具名（snake_case）:
- 通道: **Native** ／ **GP** ／ **Python** ／ **Bridge(PS/UXP)**
- 读／写归类: **Read** ／ **Session** ／ **Write**
- 是否需要 `QueuedTask.Run`（Rule 4：访问 SDK 对象必须经主线程调度）: **是／否**

## 契约影响 / Contract impact

- [ ] 会改变生产工具数（当前 **239**・注册 ≡ 契约快照 ⇒ 需同步契约与台账）
- [ ] 会新增错误码（当前 **33**）
- [ ] 会改动 GP 白名单（当前 **53** 件／30,634 B・扩项须单独评审）
- [ ] 不影响以上任何一项

## 其它

- [ ] 已阅读 [`CONTRIBUTING.md`](../../CONTRIBUTING.md) 与 [`AGENTS.md`](../../AGENTS.md) 的分层与安全硬规则
- [ ] 未要求把 Esri SDK、安装包或 `Release/*.zip` 加入仓库
