namespace ArcGISProMCP.Core.Models;

// ══════════════════════════════════════════════════════════════════════════════
// D-064 · 功能完善第四批（21 件）纯数据模型（Shared；不得引用 ArcGIS SDK）
//   A Schema 创建（6） / B Project 增强（5） / C 数据发现（4） / D 批处理（2）
//   ＋ G-166 差异化（4：diagnose / snapshot_project / restore_snapshot / set_readonly_mode）
// 错误码零新增（33）—— 全部复用既有码；本文件为纯数据，无契约面变更风险。
// ══════════════════════════════════════════════════════════════════════════════

// ─────────────────────────── A · Schema 创建 ───────────────────────────

/// <summary>字段规格（create_feature_class / add_fields 共用）。</summary>
public sealed class SchemaFieldSpec
{
    public string Name { get; set; } = string.Empty;

    /// <summary>字段类型（Text/Short/Long/Float/Double/Date/GUID/Blob/Raster…，大小写不敏感）。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>文本字段长度（仅 Text；≤0 或缺省 ⇒ 由宿主取默认）。</summary>
    public int? Length { get; set; }

    /// <summary>数值精度（仅 Float/Double），缺省 ⇒ 宿主默认。</summary>
    public int? Precision { get; set; }

    /// <summary>小数位（仅 Float/Double），缺省 ⇒ 宿主默认。</summary>
    public int? Scale { get; set; }

    /// <summary>是否可空；缺省（null）视为 true（与既有 add_field 的 fieldIsNullable 默认一致）。</summary>
    public bool? Nullable { get; set; }

    public string? Alias { get; set; }

    /// <summary>字段默认值（字符串形态，可空）。</summary>
    public string? DefaultValue { get; set; }
}

/// <summary>create_feature_class 结果（写后读回，含 stateProof）。</summary>
public sealed class CreateFeatureClassResult
{
    public string Path { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string GeometryType { get; set; } = string.Empty;

    /// <summary>空间参考（WKID 或名称；未指定 ⇒ 宿主默认，如实回报）。</summary>
    public string? SpatialReference { get; set; }

    /// <summary>字段清单（写后经 schema 读回）。</summary>
    public IReadOnlyList<string> Fields { get; set; } = Array.Empty<string>();

    public int FieldCount { get; set; }

    public bool AddedToMap { get; set; }

    public string? LayerName { get; set; }

    /// <summary>加入地图后读回的要素数（未加图 ⇒ 0）。</summary>
    public int FeatureCount { get; set; }

    public string? StateProof { get; set; }

    public string? OverwriteNote { get; set; }
}

/// <summary>create_table 结果（写后读回，含 stateProof）。</summary>
public sealed class CreateTableResult
{
    public string Path { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public IReadOnlyList<string> Fields { get; set; } = Array.Empty<string>();

    public int FieldCount { get; set; }

    public int RowCount { get; set; }

    public bool AddedToMap { get; set; }

    public string? TableName { get; set; }

    public string? StateProof { get; set; }

    public string? OverwriteNote { get; set; }
}

/// <summary>add_fields 结果（批量；就地 ⇒ 输入守卫 + 前照对照）。</summary>
public sealed class AddFieldsResult
{
    public string Path { get; set; } = string.Empty;

    /// <summary>请求添加的字段名（原序）。</summary>
    public IReadOnlyList<string> Requested { get; set; } = Array.Empty<string>();

    /// <summary>实际新增成功者。</summary>
    public IReadOnlyList<string> Added { get; set; } = Array.Empty<string>();

    /// <summary>因**已存在**而跳过者（缺省拒已存在 ⇒ 不报错，如实跳过并披露）。</summary>
    public IReadOnlyList<string> SkippedExisting { get; set; } = Array.Empty<string>();

    /// <summary>因规格非法（空名/类型不在允许清单）而**未提交**者。</summary>
    public IReadOnlyList<string> Rejected { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> FieldsBefore { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> FieldsAfter { get; set; } = Array.Empty<string>();

    public string? StateProof { get; set; }
}

/// <summary>delete_field 结果（破坏性 ⇒ confirm 缺省拒）。</summary>
public sealed class DeleteFieldResult
{
    public string Path { get; set; } = string.Empty;

    public string FieldName { get; set; } = string.Empty;

    public bool Confirm { get; set; }

    public bool Deleted { get; set; }

    public IReadOnlyList<string> FieldsBefore { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> FieldsAfter { get; set; } = Array.Empty<string>();

    public string? StateProof { get; set; }
}

/// <summary>truncate_table 结果（破坏性 ⇒ confirm 缺省拒 + 行数前后照）。</summary>
public sealed class TruncateTableResult
{
    public string Path { get; set; } = string.Empty;

    public bool Confirm { get; set; }

    /// <summary>清空前行数（读回）。</summary>
    public long RowsBefore { get; set; }

    /// <summary>清空后行数（读回；成功应为 0）。</summary>
    public long RowsAfter { get; set; }

    /// <summary>schema（字段清单）是否保持——清空仅删行不删字段。</summary>
    public bool SchemaPreserved { get; set; }

    public IReadOnlyList<string> Fields { get; set; } = Array.Empty<string>();

    public string? StateProof { get; set; }
}

/// <summary>export_features 结果（输出守卫；尊重当前选择与定义查询）。</summary>
public sealed class ExportFeaturesResult
{
    public string Input { get; set; } = string.Empty;

    public string Output { get; set; } = string.Empty;

    /// <summary>是否**尊重当前选择**（true 时仅导出选中要素；无选择则全量并披露）。</summary>
    public bool RespectSelection { get; set; }

    /// <summary>是否**尊重定义查询**（定义查询在全链由 GP 图层语义自动生效）。</summary>
    public bool RespectDefinitionQuery { get; set; }

    /// <summary>生效的定义查询（无 ⇒ null）。</summary>
    public string? DefinitionQuery { get; set; }

    /// <summary>追加的 where（与定义查询叠加；未提供 ⇒ null）。</summary>
    public string? WhereClause { get; set; }

    /// <summary>源要素总数（读回）。</summary>
    public long SourceCount { get; set; }

    /// <summary>源选中数（无选择 ⇒ -1，如实披露"无选择"而非 0）。</summary>
    public long SelectedCount { get; set; }

    /// <summary>导出要素数（写后对输出读回）。</summary>
    public long ExportedCount { get; set; }

    /// <summary>选择是否被应用（无选择 ⇒ false + Note 披露）。</summary>
    public bool SelectionApplied { get; set; }

    public string? Note { get; set; }

    public string? StateProof { get; set; }

    public string? OverwriteNote { get; set; }
}

// ─────────────────────────── B · Project 增强 ───────────────────────────

/// <summary>remove_map 结果（破坏性 ⇒ confirm 缺省拒）。</summary>
public sealed class RemoveMapResult
{
    public string MapName { get; set; } = string.Empty;

    public bool Confirm { get; set; }

    public bool Removed { get; set; }

    public IReadOnlyList<string> MapsBefore { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> MapsAfter { get; set; } = Array.Empty<string>();
}

/// <summary>activate_map 结果（UI 联动：打开/激活地图视图）。</summary>
public sealed class ActivateMapResult
{
    public string MapName { get; set; } = string.Empty;

    /// <summary>激活后读回的活动地图名（未激活 ⇒ null）。</summary>
    public string? ActiveMap { get; set; }

    public bool Activated { get; set; }

    /// <summary>是否新打开了地图视图窗格（false = 已存在视图仅激活）。</summary>
    public bool ViewOpened { get; set; }

    /// <summary>无 UI 线程/无视图能力时如实披露（不等于失败原因伪造）。</summary>
    public string? Note { get; set; }
}

/// <summary>set_map_properties 结果（地图改名 / 换空间参考；写后读回）。</summary>
public sealed class SetMapPropertiesResult
{
    public string MapName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string NameBefore { get; set; } = string.Empty;

    public bool Renamed { get; set; }

    public string? SpatialReference { get; set; }

    public string? SpatialReferenceBefore { get; set; }

    public bool SpatialReferenceChanged { get; set; }

    public IReadOnlyList<string> MapNames { get; set; } = Array.Empty<string>();
}

/// <summary>GP 环境设置快照（get_environment / set_environment）。</summary>
public sealed class GpEnvironmentInfo
{
    public string? Workspace { get; set; }

    public string? ScratchWorkspace { get; set; }

    public string? OutputCoordinateSystem { get; set; }

    /// <summary>处理范围（"xmin ymin xmax ymax"；未设 ⇒ null）。</summary>
    public string? Extent { get; set; }

    public string? Mask { get; set; }

    public string? CellSize { get; set; }

    public bool? OverwriteOutput { get; set; }

    public bool? ParallelProcessing { get; set; }

    public int? ParallelProcessingFactor { get; set; }

    public string? OutputMFlag { get; set; }

    public string? OutputZFlag { get; set; }

    /// <summary>未设置项（如实披露缺省而非伪造值）。</summary>
    public IReadOnlyList<string> Unset { get; set; } = Array.Empty<string>();
}

/// <summary>set_environment 结果（会话级 + 审计附注 + reset 还原 + 前后照）。</summary>
public sealed class SetEnvironmentResult
{
    /// <summary>本次实际写入的键（规范化后的键名）。</summary>
    public IReadOnlyList<string> Applied { get; set; } = Array.Empty<string>();

    /// <summary>因未提供而**未改动**的键。</summary>
    public IReadOnlyList<string> Unchanged { get; set; } = Array.Empty<string>();

    public GpEnvironmentInfo? Before { get; set; }

    public GpEnvironmentInfo? After { get; set; }

    /// <summary>reset=true 还原请求（还原为会话初始快照）。</summary>
    public bool Reset { get; set; }

    /// <summary>还原后与**会话初始快照**是否一致（reset=false ⇒ null）。</summary>
    public bool? ResetRestoredBaseline { get; set; }

    /// <summary>会话级语义披露（不落盘、进程结束失效、影响后续 GP/受控调用）。</summary>
    public string? Scope { get; set; }

    /// <summary>审计附注（写入 GP 审计目录，供事后追溯）。</summary>
    public string? AuditNote { get; set; }

    /// <summary>审计落盘条号（0 = 未落盘，如实披露）。</summary>
    public long AuditEntryIndex { get; set; }

    public string? AuditPath { get; set; }
}

// ─────────────────────────── C · 数据发现 ───────────────────────────

/// <summary>数据发现条目（search_data / get_project_items 共用）。</summary>
public sealed class DataItemInfo
{
    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    /// <summary>数据集类型（FeatureClass / Table / Raster / Folder / Toolbox / Unknown…）。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>所属容器（GDB 路径 / 文件夹 / "home" / "project"）。</summary>
    public string? Container { get; set; }
}

/// <summary>search_data 结果（上限 + 截断如实披露）。</summary>
public sealed class SearchDataResult
{
    public string? Pattern { get; set; }

    public string? TypeFilter { get; set; }

    /// <summary>实际检索的根（home / 默认 GDB / 文件夹连接）。</summary>
    public IReadOnlyList<string> SearchedRoots { get; set; } = Array.Empty<string>();

    public int MatchCount { get; set; }

    public int TopN { get; set; }

    public bool Truncated { get; set; }

    public IReadOnlyList<DataItemInfo> Items { get; set; } = Array.Empty<DataItemInfo>();
}

/// <summary>list_folder 条目（防遍历：深度/数量双上限）。</summary>
public sealed class FolderEntryInfo
{
    public string Name { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public bool IsDirectory { get; set; }

    public long? Size { get; set; }

    public string? Extension { get; set; }

    public string? LastWriteUtc { get; set; }
}

/// <summary>list_folder 结果（路径守卫 + 深度/数量上限）。</summary>
public sealed class ListFolderResult
{
    public string Path { get; set; } = string.Empty;

    public int RequestedDepth { get; set; }

    /// <summary>实际遍历深度上限（&lt;= MaxDepth）。</summary>
    public int EffectiveDepth { get; set; }

    public int MaxEntries { get; set; }

    public int EntryCount { get; set; }

    public bool Truncated { get; set; }

    /// <summary>跳过的目录数（权限/不可读；如实披露不掩盖）。</summary>
    public int SkippedDirectories { get; set; }

    public IReadOnlyList<FolderEntryInfo> Entries { get; set; } = Array.Empty<FolderEntryInfo>();
}

/// <summary>add_folder_connection 结果（路径规范化）。</summary>
public sealed class AddFolderConnectionResult
{
    public string Path { get; set; } = string.Empty;

    public string NormalizedPath { get; set; } = string.Empty;

    public bool Added { get; set; }

    public bool AlreadyPresent { get; set; }

    public int ConnectionsCount { get; set; }
}

/// <summary>get_project_items 结果（已注册连接 / 工具箱）。</summary>
public sealed class ProjectItemsResult
{
    public IReadOnlyList<DataItemInfo> Connections { get; set; } = Array.Empty<DataItemInfo>();

    public IReadOnlyList<DataItemInfo> Toolboxes { get; set; } = Array.Empty<DataItemInfo>();

    public int Count { get; set; }
}

// ─────────────────────────── D · 批处理与系列输出 ───────────────────────────

/// <summary>run_batch 单项**请求**（工具名 + 参数对象；可选逐项 continueOnError 覆盖）。</summary>
public sealed class BatchItemRequest
{
    public string Tool { get; set; } = string.Empty;

    /// <summary>该工具的参数对象（键值对；原样透传，**不豁免** required 校验）。</summary>
    public Dictionary<string, object?> Arguments { get; set; } = new();

    /// <summary>逐项覆盖 <c>continueOnError</c>（null ⇒ 用批级设置）。</summary>
    public bool? ContinueOnError { get; set; }
}

/// <summary>run_batch 单项结果（逐项经同一守卫/confirm/审计）。</summary>
public sealed class BatchItemResult
{
    public int Index { get; set; }

    public string Tool { get; set; } = string.Empty;

    public bool Executed { get; set; }

    public bool Success { get; set; }

    /// <summary>"OK" 或错误码。</summary>
    public string ResultCode { get; set; } = string.Empty;

    public string? Message { get; set; }

    public long DurationMs { get; set; }

    /// <summary>未执行原因（白名单外 / 只读模式拒绝 / 预算截断 / continueOnError=false 后续项）。</summary>
    public string? SkippedReason { get; set; }

    /// <summary>工具返回载荷（成功时；可能为 null）。</summary>
    public object? Data { get; set; }

    /// <summary>该工具是否被判定为写类（只读模式下据此拒绝）。</summary>
    public bool WriteOperation { get; set; }
}

/// <summary>run_batch 结果（30s 硬约束：超限截断并报告已完成项）。</summary>
public sealed class RunBatchResult
{
    public int TotalRequested { get; set; }

    public int Executed { get; set; }

    public int Succeeded { get; set; }

    public int Failed { get; set; }

    public int Skipped { get; set; }

    /// <summary>是否因**总耗时预算**截断（true 时后续项 SkippedReason = budget-exceeded）。</summary>
    public bool Truncated { get; set; }

    /// <summary>硬约束预算（毫秒；默认 30000）。</summary>
    public int BudgetMs { get; set; }

    public long ElapsedMs { get; set; }

    /// <summary>是否在只读模式下运行（此时全部写类项被拒）。</summary>
    public bool ReadOnlyMode { get; set; }

    public IReadOnlyList<BatchItemResult> Items { get; set; } = Array.Empty<BatchItemResult>();

    /// <summary>批级审计附注（逐项审计 + 批级汇总）。</summary>
    public string? AuditNote { get; set; }

    public long AuditEntryIndex { get; set; }

    public string? AuditPath { get; set; }

    /// <summary>是否因**未注册/白名单外工具**而在**执行前**整体拒绝（零副作用）。</summary>
    public IReadOnlyList<string> RejectedTools { get; set; } = Array.Empty<string>();
}

/// <summary>export_map_series 结果（布局 map series 多页 PDF）。</summary>
public sealed class ExportMapSeriesResult
{
    public string LayoutName { get; set; } = string.Empty;

    public string OutputPath { get; set; } = string.Empty;

    /// <summary>布局是否启用 map series（未启用 ⇒ 明确拒绝而非导出单页）。</summary>
    public bool MapSeriesEnabled { get; set; }

    public string? MapSeriesKind { get; set; }

    /// <summary>总页数（读回）。</summary>
    public int PageCount { get; set; }

    /// <summary>页数上限（超过 ⇒ 明确拒绝，不静默截断文件）。</summary>
    public int MaxPages { get; set; }

    public long Bytes { get; set; }

    /// <summary>PDF magic（"%PDF-"）校验。</summary>
    public bool PdfMagic { get; set; }

    public string? RequestedPages { get; set; }

    public string? StateProof { get; set; }

    public string? OverwriteNote { get; set; }
}

// ─────────────────── G-166 · diagnose / snapshot / readonly ───────────────────

/// <summary>单项体检结果。</summary>
public sealed class DiagnosticCheck
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Ok / Warn / Fail / Unknown（Unknown 不得伪装为 Ok）。</summary>
    public string Status { get; set; } = "Unknown";

    public string Detail { get; set; } = string.Empty;

    public string? RecommendedAction { get; set; }

    /// <summary>证据键值（脱敏：不含凭据）。</summary>
    public IReadOnlyDictionary<string, object?> Evidence { get; set; } = new Dictionary<string, object?>();
}

/// <summary>diagnose 结构化体检报告（只读）。</summary>
public sealed class DiagnosticsReport
{
    public string GeneratedAtUtc { get; set; } = string.Empty;

    /// <summary>整体状态 = 各项最差态（Ok &lt; Warn &lt; Fail；Unknown 单独标注但不等同 Fail）。</summary>
    public string OverallStatus { get; set; } = "Unknown";

    public string? ProductVersion { get; set; }

    public string? ArcGISProVersion { get; set; }

    public string? ProjectPath { get; set; }

    public bool ReadOnlyMode { get; set; }

    public IReadOnlyList<DiagnosticCheck> Checks { get; set; } = Array.Empty<DiagnosticCheck>();
}

/// <summary>快照文件条目（哈希清单，用于完整性校验）。</summary>
public sealed class SnapshotFileInfo
{
    /// <summary>快照目录内相对路径。</summary>
    public string RelativePath { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long Bytes { get; set; }
}

/// <summary>快照清单（snapshot.manifest.json）。</summary>
public sealed class SnapshotManifest
{
    public string FormatVersion { get; set; } = "1.0";

    public string SnapshotId { get; set; } = string.Empty;

    public string CreatedAtUtc { get; set; } = string.Empty;

    public string AprxRelativePath { get; set; } = string.Empty;

    public string AprxSha256 { get; set; } = string.Empty;

    public long AprxBytes { get; set; }

    /// <summary>图层引用清单（图层名 → 数据源，含断源项）。</summary>
    public IReadOnlyList<SnapshotLayerRef> LayerReferences { get; set; } = Array.Empty<SnapshotLayerRef>();

    /// <summary>断点记录（快照时仍断源的图层）。</summary>
    public IReadOnlyList<string> BreakPoints { get; set; } = Array.Empty<string>();

    public IReadOnlyList<SnapshotFileInfo> Files { get; set; } = Array.Empty<SnapshotFileInfo>();

    public long TotalBytes { get; set; }
}

/// <summary>快照内的图层引用。</summary>
public sealed class SnapshotLayerRef
{
    public string MapName { get; set; } = string.Empty;

    public string LayerName { get; set; } = string.Empty;

    public string? DataSource { get; set; }

    public bool Broken { get; set; }
}

/// <summary>snapshot_project 结果。</summary>
public sealed class SnapshotResult
{
    public string SnapshotId { get; set; } = string.Empty;

    public string Directory { get; set; } = string.Empty;

    public string ManifestPath { get; set; } = string.Empty;

    public string AprxPath { get; set; } = string.Empty;

    public string AprxSha256 { get; set; } = string.Empty;

    public long AprxBytes { get; set; }

    public int LayerRefCount { get; set; }

    public int BreakPointCount { get; set; }

    public long TotalBytes { get; set; }

    public int FileCount { get; set; }

    public string CreatedAtUtc { get; set; } = string.Empty;

    /// <summary>快照目录是否落在 D 盘受控根（G-138）。</summary>
    public bool TransientRootOnDDrive { get; set; }

    public string? Note { get; set; }
}

/// <summary>restore_snapshot 结果（破坏性 ⇒ confirm 缺省拒 + 完整性校验）。</summary>
public sealed class RestoreSnapshotResult
{
    public string Directory { get; set; } = string.Empty;

    public bool Confirm { get; set; }

    /// <summary>完整性校验（清单哈希逐项比对；损坏/篡改 ⇒ false 并拒绝恢复）。</summary>
    public bool IntegrityVerified { get; set; }

    public string? IntegrityDetail { get; set; }

    public bool Restored { get; set; }

    public int RestoredFiles { get; set; }

    public string? AprxPath { get; set; }

    public string? AprxSha256Snapshot { get; set; }

    public string? AprxSha256Restored { get; set; }

    /// <summary>恢复后与快照内 APRX 是否**逐字节**一致（LIVE 判据）。</summary>
    public bool? ByteIdentical { get; set; }

    public string? BackupOfPreviousAprx { get; set; }

    /// <summary>恢复前"让路"（关闭工程 / 释放 APRX 独占句柄）的**实测**说明（dirty/保存/关闭/句柄已释放）。</summary>
    public string? PrepareNote { get; set; }

    /// <summary>恢复后重载工程的**实测**说明（未重载时如实说明）。</summary>
    public string? ReloadNote { get; set; }
}

/// <summary>set_readonly_mode 结果（会话级；错误码零新增 ⇒ 复用 PERMISSION_DENIED + "(read-only)"）。</summary>
public sealed class ReadOnlyModeResult
{
    public bool ReadOnlyMode { get; set; }

    public bool Previous { get; set; }

    public bool Changed { get; set; }

    /// <summary>被判定为写类/破坏类的工具数（拒绝清单规模，供审计与测试断言）。</summary>
    public int WriteToolCount { get; set; }

    /// <summary>只读类工具数。</summary>
    public int ReadToolCount { get; set; }

    /// <summary>会话级语义披露（不落盘；重启失效）。</summary>
    public string? Scope { get; set; }

    public string? Note { get; set; }
}
