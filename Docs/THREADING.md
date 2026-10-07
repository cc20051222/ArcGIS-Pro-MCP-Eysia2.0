# 线程模型（THREADING）

> 严格遵守 ArcGIS Pro 线程模型，本文件为纪律性说明。

## 1. 核心规则
- 访问 ArcGIS Pro 对象（Map / MapView / Layer / Project / Geodatabase / CIM / Layout / Licensing / Geoprocessing）
  必须通过 **MCT（Main CIM Thread）**，使用 `QueuedTask.Run`。
- UI 相关操作使用 `Dispatcher`（按钮 OnClick 等运行在 UI 线程）。
- 禁止在任意线程直接访问 Pro 对象。

## 2. 实现模式（Compatibility/Services）
每个 Service 方法把 SDK 访问包进：
```csharp
return await QueuedTask.Run(() => { ... }, TaskCreationOptions.None);
```
例如 MapService / LayerService / ProjectService / LicenseService。

## 3. 异步
- 所有 Host Service 采用 `Task<T>`（如 `GetCurrentMapAsync`, `GetLayersAsync`）。
- GIS 服务层禁止 `.Result` / `.Wait()` / `Thread.Sleep`，避免死锁。
- Tool 支持 `CancellationToken`（Router 传入，取消 → CANCELLED）。

## 4. 本阶段已确认的 SDK 多线程要点
- `MapView.Active?.Map`、`Project.Current`、`MapFactory.CreateMapFromItem` 等在 QueuedTask 内访问。
- 版本服务读注册表（无 Pro 对象），不依赖 MCT。
- 后续 Phase 实现属性/选择/栅格/地理处理时，必须沿用 QueuedTask 模式。
