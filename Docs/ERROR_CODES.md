# 统一错误码（ERROR_CODES）

定义于 `ArcGISProMCP.Core.Results.ErrorCodes`。各 Service / Tool 必须复用，不得各自造格式。

| Code | 含义 |
|------|------|
| `INVALID_ARGUMENT` | 参数缺失或非法 |
| `NOT_FOUND` | 资源/工具/对象不存在 |
| `LICENSE_REQUIRED` | 缺少必要 License / Extension |
| `PERMISSION_DENIED` | 权限不足 |
| `EXECUTION_FAILED` | GIS 执行失败 |
| `TIMEOUT` | 超时 |
| `CANCELLED` | 已取消 |
| `THREADING_ERROR` | 线程模型违规（如 CalledOnWrongThread） |
| `INTERNAL_ERROR` | 内部异常 |
| `TOOL_NOT_FOUND` | Tool 不存在（Router 返回） |
| `NOT_IMPLEMENTED` | 尚未实现（Phase 2 占位服务） |
| `PYTHON_BRIDGE_UNAVAILABLE` | Python Bridge 进程、管道或启动/恢复不可用 |
| `PYTHON_TIMEOUT` | Python Bridge action deadline 超时；已派发请求会先 quarantine 进程 |
| `PYTHON_OUTPUT_LIMIT_EXCEEDED` | 单条 Python stdout response 超过集中配置的 UTF-8 字节上限 |
| `PYTHON_PROTOCOL_ERROR` | Python stdout 非法 JSON、response schema 或 correlation ID 无法可靠解析 |

## 错误对象

`OperationError(Code, Message, Details?)`，ToString 输出 `Code: Message` 或其 + `(Details)`。

## 统一结果模型

`OperationResult<T>`：`Success`, `Data`, `Message`, `Warnings`, `Errors`, `ExecutionTime`, `RequestId`。

## Phase 5.6.4 verification

The Phase 5.6.2/5.6.3 Python Bridge mappings were verified against the deployed real runtime and internal guardrail fixtures: `PYTHON_TIMEOUT` for the bounded action deadline, `CANCELLED` for caller-token cancellation, `PYTHON_BRIDGE_UNAVAILABLE` for crash/EOF, `PYTHON_EXECUTION_ERROR` for an isolated structured action error, `PYTHON_OUTPUT_LIMIT_EXCEEDED` for an oversized response, and `PYTHON_PROTOCOL_ERROR` for malformed/invalid protocol responses. No new error code was added in Phase 5.6.4.
