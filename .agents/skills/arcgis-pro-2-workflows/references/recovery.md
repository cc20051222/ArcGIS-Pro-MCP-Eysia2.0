# 恢复、错误解释与交付证据

## 响应分层

先区分传输/JSON-RPC 错误、MCP `isError`、工具内部业务 `success/errors/status`、作业逐项状态和文件核验。请求成功送达不代表 GP 成功；schema 存在不代表实现可执行；包内自述“已验”也不替代当前证据。

## 按错误处理

| 错误族 | 下一步 | 不允许的捷径 |
|---|---|---|
| `INVALID_ARGUMENT` / `TOOL_NOT_FOUND` | 重取当前 schema、核字段/工具名称与版本；有效授权内纠正明确参数错误 | 猜参数、调用未注册工具、绕过白名单 |
| `AMBIGUOUS_MAP_NAME` / `AMBIGUOUS_LAYER_NAME` / `NO_ACTIVE_VIEW` | 只读枚举实际对象，补充地图/路径身份 | 静默选第一个或自动切用户工程 |
| `NOT_FOUND` / `LAYER_NOT_FOUND` / `MAP_NOT_FOUND` / `DATASET_NOT_FOUND` / `LAYER_DATA_SOURCE_UNAVAILABLE` | 区分不存在与断源；检查真实路径/数据源和访问条件 | 自动删除重建、改用户连接 |
| `OUTPUT_EXISTS` | 默认保留旧成果；按既有授权给出新版本路径或询问覆盖范围 | 自动删旧文件、默认 overwrite=true |
| `PATH_ESCAPE_REJECTED` / `PERMISSION_DENIED` / `INVALID_STATE` | 解释具体范围/状态；只检查合法拥有目录和当前前置条件 | 改根/改只读模式/换旁路逃过守卫 |
| `LICENSE_REQUIRED` | 说明缺的许可/扩展、暂停依赖步骤 | 把另一种算法冒称同结果 |
| `NOT_IMPLEMENTED` | 记录该成员受阻，提供已验部分与准确后续条件 | 假文件、循环重试、以注册替代完成 |
| `TIMEOUT` / `REQUEST_TIMEOUT` / `PYTHON_TIMEOUT` / `CANCELLED` | 先查作业/审计/实际效果，辨明已完成、未执行、部分、未知 | 对可能写操作盲目自动重试 |
| `SELECTION_LIMIT_EXCEEDED` / `SELECTION_BASELINE_MISMATCH` / `SELECTION_SNAPSHOT_NOT_FOUND` / `SELECTION_SNAPSHOT_EXPIRED` / `LAYER_NOT_SELECTABLE` | 重核选择范围与快照身份，披露上限；重新选择可能是会话写 | 把截断结果称全量、静默扩大到全层 |
| `PYTHON_BRIDGE_UNAVAILABLE` / `PYTHON_OUTPUT_LIMIT_EXCEEDED` / `PYTHON_PROTOCOL_ERROR` | 读取 `python_bridge_ping` / `python_runtime_info` 的当前事实，缩小读取量或报告缺口 | 自动安装环境、执行任意 Python 或直连 6511 |
| `GEOPROCESSING_ERROR` / `ARCGIS_ERROR` / `EXECUTION_FAILED` / `THREADING_ERROR` / `INTERNAL_ERROR` | 保留原始代码与经过脱敏的诊断，读 `get_messages` / `get_audit_log` / `diagnose` | 清进程/清未知锁/重启宿主或修改数据来隐藏错误 |

完整 33 错误码见离线工具快照；错误族不全都意味着可重试。只有明确证明尚未执行、原授权仍有效、没有停止意图且仍满足前置条件时，才准备新的写调用。不存在一律自动重试次数。

## 最小证据与交付

任务需要时保留请求目标/输入身份、所选工具与 schema 版本、关键参数/环境、许可/授权范围、输出位置、响应/作业 ID、原始数量口径与检查结果。凭据/文件令牌不进交付正文，数据不因需要诊断而自动上传第三方模型或公开服务。

用户结果简洁列“已完成 / 部分完成 / 受阻 / 未验证”，分别对应实际成员。地图说明视觉是否已检查；数据说明完整还是采样；PS 说明是否真实执行；错误说明具体未完成的影响，不泛泛显示成功。工程维护报告按当前派工提交 `PASS CANDIDATE`，不代指挥席关闭 Gate。

只读重开验证需要确保宿主/查看器本身不会写回原文件；无法保证时用已拥有产物或明确给出未验证项。GDB/PSD/工程存在锁时先停止相关变化，不能 taskkill 或删未知锁。历史结果保留不证明现时许可和未来复算可用。
