# 技术详稿导航

本目录完整保留上一轮 V11 的 15 份 Markdown 技术文件，按主方案 6 份、模块实现 5 份、支持体验 4 份分类。配套两份 JSON 是原资料的逐字节存档。本轮没有删除任何历史功能、验收门或科学合同，也没有修改原规划目录。

新读者先读 [完整最终总方案](../01_完整最终方案/01_项目最终总方案.md)，再按需要查阅本目录。技术文件保留历史深化章节，以便追踪规则形成过程；其中的“本轮”“V5 至 V11”“已完成”均须结合所在章节和原证据理解，不代表产品已经实现或通过最终验收。

初次整理时，本目录 Markdown 仅迁移本地链接：能在本目录找到的链接指向复制件，外部源码、历史快照、研究和核验脚本指向原工作空间的绝对路径。JSON 中的旧相对路径仍以原资料目录为基准，它们属于证据存档，不是本目录的一键执行命令。迁移记录在 [来源与链接迁移台账](../99_来源与核验/技术详稿来源与链接迁移.json)。

2026-10-03按用户指示直接修改本目录相关技术合同，保留原范围和门。新增资产物化细则在领域合同§26、格式支持§27；当前合同、实施和验收稿分别补充子里程碑及可输入实测容量。台账保留初次迁移哈希，另列当前修订哈希；两份原JSON仍逐字节存档。当前主文和Word同步更新，历史章节的证据日期不随修订日期刷新。

## 主方案 6 份

1. [总入口与最终方案导航](01_主方案（6份）/README_FINAL.md)
2. [功能覆盖与支持矩阵](01_主方案（6份）/CAPABILITY_AND_SUPPORT_MATRIX.md)
3. [智能决策与任务编排](01_主方案（6份）/INTELLIGENCE_AND_ORCHESTRATION.md)
4. [技术合同与执行流程](01_主方案（6份）/CONTRACTS_AND_EXECUTION_RECIPES.md)
5. [完整开发计划与安装发布安排](01_主方案（6份）/IMPLEMENTATION_AND_RELEASE_PLAN.md)
6. [验收标准 竞争对比与证据追踪](01_主方案（6份）/VERIFICATION_AND_TRACEABILITY.md)

## 模块实现详稿 5 份

7. [实现详稿总入口](02_模块实现详稿（5份）/README_IMPLEMENTATION_DETAILS.md)
8. [模块接口 开发任务与依赖](02_模块实现详稿（5份）/MODULE_INTERFACES_AND_BACKLOG.md)
9. [领域数据合同与任务状态机](02_模块实现详稿（5份）/DOMAIN_CONTRACTS_AND_STATE_MACHINES.md)
10. [三条标准业务链的实现](02_模块实现详稿（5份）/THREE_CHAIN_IMPLEMENTATION.md)
11. [GP扩展 PS自动化与创新功能实现](02_模块实现详稿（5份）/GP_PS_AND_INNOVATION_RECIPES.md)

## 支持与用户体验详稿 4 份

12. [支持详稿总入口](03_支持与用户体验详稿（4份）/README_SUPPORT_DETAILS.md)
13. [模型接入 切换与故障降级](03_支持与用户体验详稿（4份）/MODEL_PROVIDER_AND_FALLBACK_CONTRACTS.md)
14. [跨版本兼容 环境能力与用户工作台](03_支持与用户体验详稿（4份）/ENVIRONMENT_AND_WORKBENCH_CONTRACTS.md)
15. [格式支持 转换损失与行业资源包](03_支持与用户体验详稿（4份）/FORMAT_AND_RESOURCE_PACK_CONTRACTS.md)
