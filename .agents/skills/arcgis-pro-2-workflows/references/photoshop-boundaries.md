# GIS 素材交接与 PS 边界

## 当前源码结论

本包快照包含 20 个 `ps_*` 注册名；`ps_get_capabilities` 只报告本地路由/握手信息并明确 `NOT_VERIFIED`，不证明 Adobe 对端就绪。其余 PS 业务方法在当前 D091/D102 实现中按 `NOT_IMPLEMENTED` 拒绝执行。这些是源码观察；制作本 Skill 没有启动 Photoshop、跑配方或执行 UXP 验收。

请求“自动 PS 美化、图层修改、PS 导出”时先说明当前阻断。即使 `ps_get_capabilities` 的外层操作成功，里面 `status=NOT_VERIFIED`、深探禁用或 peer 未验证仍不能继续写。**不让不会 PS 的用户手工操作充当自动化成功证据**，也不把 GIS 导出改名 PSD 或生成空母版。

## 可以准备的交接

用户授权的 GIS 制图和素材准备，可按当前 schema 使用 `export_design_bundle`、`refresh_design_bundle`、`validate_design_bundle`、`validate_delivery_package`。必须检查这些工具真正支持的成员、检查范围和实际结果；素材包合格不证明 PS 已导入、已设计或已导出。

交接保留数据/地图/事实来源、CRS、比例尺/地图框、图例和量化色义、必要文字、素材格式、目标 DPI、设计区域和不得修改的科学区域。业务目标要求完整 PS 成果时，GIS 成果仅作为用户可领取的部分成员，整体标受阻。不能自动换成手工 PS、另装 Adobe 插件或执行任意 ExtendScript/actionJSON。

## 未来后端完成后如何接入

要解除限制，至少取得：当前插件/Bridge 与 Adobe/UXP 版本、真实对端能力、文档拥有关系、宿主批准链、受控配方、输出 grant/路径、取消/断线行为、保存后重开及图层/像素/事实核验的真实证据。现役 schema 某些写方法包含 `specRevision` 和 `apsReference`；`confirm=true` 不能替代它们，Skill 不能自造有效批准记录。

Adobe 的 `executeAsModal` 与文件令牌规则属于后端实现约束，Skill 只通过已验证受控 MCP 工具使用它们，不直接调用 Adobe 内部 API 绕过批准。用户授权当前具体文档和输出后可自动走已验配方，不能把“自动化”做成每一步重复问同一许可。当前发布候选仍不宣称上述后端已实现。
