# ArcGIS Host 服务（HOST_SERVICES）

> Phase 2 建立。Shared 定义接口与数据模型（不含 ArcGIS Pro SDK）；Compatibility 实现。

## 1. IArcGISHost（Shared.Hosting）

聚合 11 个服务，全部为纯数据模型：

```
IArcGISHost
├── Maps        (IMapService)
├── Layers      (ILayerService)
├── Attributes  (IAttributeService)      —— 占位(NOT_IMPLEMENTED)
├── Selection   (ISelectionService)      —— 占位(NOT_IMPLEMENTED)
├── Geoprocessing (IGeoprocessingService) —— 占位(NOT_IMPLEMENTED)
├── Raster      (IRasterService)          —— 占位(NOT_IMPLEMENTED)
├── Data        (IDataManagementService)  —— 占位(NOT_IMPLEMENTED)
├── Layout      (ILayoutService)          —— 占位(NOT_IMPLEMENTED)
├── Project     (IProjectService)         ✅ 真实
├── License     (ILicenseService)         ✅ 真实
└── Version     (IArcGISVersionService)   ✅ 真实
```

## 2. 已真实实现的服务（Compatibility/Services）

| 服务 | 方法 | 数据模型 | 底层 API |
|------|------|----------|----------|
| MapService | GetCurrentMapAsync / GetMapsAsync | MapInfo | MapView.Active?.Map / Project.GetItems<Item> + MapFactory |
| LayerService | GetLayersAsync / FindLayerAsync | LayerInfo | Map.Layers / Map.FindLayer |
| ProjectService | GetProjectInfoAsync | ProjectInfo | Project.Current (URI/IsDirty/DefaultGeodatabasePath/HomeFolderPath) |
| LicenseService | GetLicenseInfoAsync / CheckExtensionAsync / RequireExtensionAsync | LicensingInfo | ArcGIS.Core.Licensing.LicenseInformation |
| VersionService | GetVersionAsync | ArcGISVersionInfo | 注册表 SOFTWARE\ESRI\ArcGISPro |

## 3. 线程模型

所有 SDK 对象访问（Map/MapView/Layer/Project/Licensing）均通过 `QueuedTask.Run`（MCT）。
详见 Docs/THREADING.md。

## 4. 返回模型

所有服务方法返回 `OperationResult<T>`（Success/Data/Message/Warnings/Errors/ExecutionTime/RequestId）。
错误为 `OperationError(Code, Message, Details)`，Code 见 Docs/ERROR_CODES.md。

## 5. NOT_IMPLEMENTED 服务

Attributes / Selection / Geoprocessing / Raster / DataManagement / Layout 仅定义接口，
Compatibility 提供占位实现返回错误码 `NOT_IMPLEMENTED`（明确不伪造数据）。后续阶段实现真实能力。
