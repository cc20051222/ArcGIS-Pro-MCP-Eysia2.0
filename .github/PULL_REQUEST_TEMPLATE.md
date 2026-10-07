# Pull Request

## 变更内容 / What changed

-

## 为什么 / Why

<!-- 关联议题或工单号；说明它解决的具体问题。 -->

## 授权与许可 / Licence acknowledgement

- [ ] 已阅读 [`CONTRIBUTING.md`](../CONTRIBUTING.md)
- [ ] **已阅读并同意以 AGPL-3.0 授权本贡献**（见 [`LICENSE`](../LICENSE)・本项目为上游 `ArcGIS-Pro-MCP` 的衍生作品，上游自 v1.1 起 AGPL-3.0／商业双许可，v1.0 及更早的 MIT 授予永久有效）
- [ ] 未引入 Esri ArcGIS Pro SDK 二进制、`EULA-Esri.txt` 或任何违反 Esri EULA 的分发物（`sdk-refs/` 永久排除・不入库）
- [ ] 未把一键安装包或任何 `Release/*.zip` 加入 git 树（分发经 GitHub Releases）

## 架构硬规则自查 / Hard rules

- [ ] Rule 4 MCT：所有 ArcGIS Pro SDK 对象访问经 `QueuedTask.Run`
- [ ] Rule 5 Shared 隔离：`Source/Shared` 零 SDK 引用
- [ ] Rule 7 唯一注册中心：新工具只在 `Composition.BuildRegistry()` 注册恰一次
- [ ] Rule 8 配置集中：端口／超时／路径／开关只在 `ArcGISProMCP.Configuration`
- [ ] 安全基线：默认端点仍为 `127.0.0.1:6520/mcp`，无 `0.0.0.0`、无公网暴露、无新增 Python 或 `6511` 旁路

## 验证 / Verification

- [ ] `dotnet build` **0 警告 0 错误**（需 .NET 8 SDK 与本地 `sdk-refs/` 引用・CI 不代跑，原因见 `CHANGELOG.md` 说明）
- [ ] Unit / Integration / Server 三套测试通过，且**失败与 NE 名单相对基线的差集为空**
- [ ] 若改动契约：`ProductionToolContractSnapshot.cs` 与注册表现算一致（当前基线 239）
- [ ] 若改动包：包内 `scripts/verify-one-click-package.ps1` 复验通过，payload 逐字节不变式已举证

## 未验证声明 / Not verified

<!-- 凡未实测的兼容性或行为，明确写 NOT VERIFIED，不得升格（例：clean-machine、真实客户端连接、Pro 3.6／3.7 实机）。 -->

## 回滚 / Rollback

-
