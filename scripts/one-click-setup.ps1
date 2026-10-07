# ArcGIS Pro MCP - one-click deployment UI and safe orchestration.
# Windows PowerShell 5.1 + .NET only; no SDK, Git or Visual Studio is required.
# This script never starts or kills ArcGIS Pro or an AI client. It delegates
# installation and client-file mutation to the accepted transaction scripts.
[CmdletBinding()]
param(
    [ValidateSet('GUI', 'Plan', 'Preflight', 'Deploy', 'Retry', 'ValidateBundle', 'Diagnose', 'Recover', 'GuiWorker')]
    [string]$Action = 'GUI',
    [string]$BundleRoot = '',
    [string]$ProjectRoot = '',
    [string]$InstallRoot = '',
    [string]$InstallerStateRoot = '',
    [ValidateSet('Uninstall', 'Rollback', 'ClientRestore')]
    [string]$RecoveryAction = 'Rollback',
    [string[]]$SelectedClients = @('codex'),
    [switch]$PluginOnly,
    [switch]$DryRun,
    [switch]$NoWait,
    [switch]$TestMode,
    [switch]$GuiTestMode,
    [switch]$GuiButtonSmokeMode,
    [string]$GuiSmokeOutput = '',
    [string]$WorkerScenario = '',
    [string]$WorkerMode = '',
    [string]$CancelTokenPath = '',
    [string]$TestCancelAfterStep = '',
    [string]$TestCancelCallbackReadyPath = '',
    [string]$TestInterruptAfterPluginChildReadyPath = '',
    [switch]$TestInjectChildFailure,
    [string]$TestInjectStateWriteFailureAt = '',
    [string]$TestInjectRecoveryIndexWriteFailureAt = '',
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
if ($Action -eq 'GuiWorker') {
    $guiUtf8Encoding = [Text.UTF8Encoding]::new($false)
    [Console]::OutputEncoding = $guiUtf8Encoding
    $OutputEncoding = $guiUtf8Encoding
}
$script:bundleRootResolved = if ([string]::IsNullOrWhiteSpace($BundleRoot)) {
    [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
} else { [IO.Path]::GetFullPath($BundleRoot) }
$script:entryScriptPath = [IO.Path]::GetFullPath($PSCommandPath)
# D-071 (C-013)：双代 payload 选择 —— 宿主 Pro 3.3+ → payload\net8；3.0-3.2 → payload\net6；单包（r15 及更早）→ payload\ 根级。
# 披露：最小改动（约 15 行），不改安装事务/契约语义；宿主版本读注册表（与 VersionService 同源）。
$hostMajor = 0; $hostMinor = 0
try {
    $rk = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('SOFTWARE\ESRI\ArcGISPro')
    if ($rk -ne $null) { $real = [string]$rk.GetValue('RealVersion'); $rk.Dispose(); $parts = $real.Split('.'); if ($parts.Length -ge 2) { [int]::TryParse($parts[0], [ref]$hostMajor) | Out-Null; [int]::TryParse($parts[1], [ref]$hostMinor) | Out-Null } }
} catch { }
$dualNet8 = Join-Path $script:bundleRootResolved 'payload\net8\ArcGISProMCP.Compatibility.esriAddInX'
$dualNet6 = Join-Path $script:bundleRootResolved 'payload\net6\ArcGISProMCP.Compatibility.esriAddInX'
$singlePkg = Join-Path $script:bundleRootResolved 'payload\ArcGISProMCP.Compatibility.esriAddInX'
if ((Test-Path -LiteralPath $dualNet8) -and (Test-Path -LiteralPath $dualNet6)) {
    $useNet8 = ($hostMajor -gt 3) -or ($hostMajor -eq 3 -and $hostMinor -ge 3)
    $script:packagePath = if ($useNet8) { $dualNet8 } else { $dualNet6 }
} else {
    $script:packagePath = $singlePkg
}
$script:manifestPath = [IO.Path]::ChangeExtension($script:packagePath, '.release-manifest.json')
$script:verifyPath = Join-Path $script:bundleRootResolved 'scripts\verify-one-click-package.ps1'
$script:workflowPath = Join-Path $script:bundleRootResolved 'scripts\user-workflow.ps1'
$script:checkerPath = Join-Path $script:bundleRootResolved 'scripts\check-compatibility.ps1'
$script:clientConfigPath = Join-Path $script:bundleRootResolved 'scripts\client-config.ps1'
$script:catalogPath = Join-Path $script:bundleRootResolved 'Config\client-catalog.json'
$script:policyPath = Join-Path $script:bundleRootResolved 'Config\compatibility-policy.json'
# D-111 (G-243 R-1): the compatibility gate's Config.daml is NOT pinned here any more -- the outer
# Source/ArcGISProMCP.Compatibility/Config.daml carries net6 metadata only (desktopVersion 3.0), and using it
# for a net8 payload made the gate reject its own package (3.0 != 3.5.0). Get-PayloadConfigDaml below reads
# Config.daml from the payload that host selection actually picked.
$script:installRootResolved = if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'ArcGIS\AddIns\ArcGISPro'))
} else { [IO.Path]::GetFullPath($InstallRoot) }
$script:addInId = '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'
$script:installTarget = Join-Path $script:installRootResolved $script:addInId
$script:stateRootResolved = if ([string]::IsNullOrWhiteSpace($InstallerStateRoot)) {
    [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ArcGISProMCP\installer'))
} else { [IO.Path]::GetFullPath($InstallerStateRoot) }
$script:statePath = Join-Path $script:stateRootResolved 'latest-transaction.json'
$script:oneClickStatePath = Join-Path $script:stateRootResolved 'one-click-state.json'
$script:oneClickLogPath = Join-Path $script:stateRootResolved 'one-click-operation.jsonl'
$script:recoveryIndexPath = Join-Path $script:stateRootResolved 'recovery-index.json'
$script:stateWriteInjectionConsumed = $false
$script:recoveryIndexWriteInjectionConsumed = $false
$script:endpoint = 'http://127.0.0.1:6520/mcp'
$script:canonicalToolCount = 239
$script:canonicalToolNames = @(
    'activate_map'
    'add_field'
    'add_fields'
    'add_folder_connection'
    'add_join'
    'add_layer'
    'add_layout_text'
    'add_legend'
    'add_north_arrow'
    'add_scale_bar'
    'alter_field'
    'analyze_scenario_sensitivity'
    'append_features'
    'apply_bookmark'
    'apply_processing_plan'
    'apply_symbology_from_layer'
    'assess_classification_accuracy'
    'assess_remote_sensing_quality'
    'assign_domain_to_field'
    'buffer'
    'build_raster_pyramids'
    'calculate_field'
    'calculate_geometry_attributes'
    'calculate_service_areas'
    'cancel_job'
    'cell_statistics'
    'check_extension'
    'check_topology_rules'
    'clear_selection'
    'clip'
    'compare_datasets'
    'compare_multi_criteria_scenarios'
    'compare_schemas'
    'compare_temporal_trajectories'
    'compose_raster_bands'
    'configure_map_series'
    'configure_subtypes'
    'copy_dataset'
    'create_bookmark'
    'create_domain'
    'create_elevation_profile'
    'create_feature_class'
    'create_file_gdb'
    'create_group_layer'
    'create_layout'
    'create_map'
    'create_relationship_class'
    'create_selection_snapshot'
    'create_table'
    'cross_validate_spatial_model'
    'dataset_summary'
    'define_projection'
    'delete_bookmark'
    'delete_dataset'
    'delete_domain'
    'delete_features'
    'delete_field'
    'describe_geoprocessing_tool'
    'describe_tool_catalog'
    'design_spatial_sample'
    'detect_temporal_change_points'
    'diagnose'
    'discard_edits'
    'dissolve'
    'duplicate_layer'
    'erase'
    'evaluate_scenario_ensemble'
    'export_design_bundle'
    'export_features'
    'export_layout_eps'
    'export_layout_jpg'
    'export_layout_pdf'
    'export_layout_png'
    'export_layout_svg'
    'export_layout_tif'
    'export_map_series'
    'export_map_view'
    'export_scene_package'
    'export_table'
    'export_time_animation'
    'extrude_scene_features'
    'find_identical'
    'focal_statistics'
    'generate_quality_report'
    'generate_tessellation'
    'get_arcgis_version'
    'get_audit_log'
    'get_broken_layers'
    'get_current_map'
    'get_dataset_info'
    'get_dataset_lineage'
    'get_definition_query'
    'get_domains'
    'get_edit_session'
    'get_environment'
    'get_feature_count'
    'get_field_info'
    'get_field_statistics'
    'get_field_values'
    'get_geometry_info'
    'get_indexes'
    'get_job_report'
    'get_job_status'
    'get_label_info'
    'get_layer_features'
    'get_layer_info'
    'get_layer_symbology'
    'get_layers'
    'get_layout_info'
    'get_license_info'
    'get_map_extent'
    'get_map_info'
    'get_map_view'
    'get_messages'
    'get_performance_stats'
    'get_project_info'
    'get_project_items'
    'get_raster_info'
    'get_schema_info'
    'get_selected_features'
    'get_subtypes'
    'get_unique_values'
    'get_workspace'
    'hotspot_analysis'
    'insert_features'
    'inspect_point_cloud'
    'inspect_raster_alignment'
    'intersect'
    'list_bookmarks'
    'list_color_ramps'
    'list_databases'
    'list_fields'
    'list_folder'
    'list_geographic_transformations'
    'list_geoprocessing_tools'
    'list_jobs'
    'list_layout_elements'
    'list_layouts'
    'list_maps'
    'list_rasters'
    'list_tables'
    'list_workspace_datasets'
    'load_folder_data'
    'merge'
    'move_layer'
    'near'
    'ping'
    'polygon_neighbors'
    'project'
    'ps_apply_design_recipe'
    'ps_compare_document_versions'
    'ps_create_adjustment_layer'
    'ps_create_document_snapshot'
    'ps_export_deliverables'
    'ps_get_capabilities'
    'ps_get_document_info'
    'ps_get_layer_info'
    'ps_import_design_bundle'
    'ps_list_documents'
    'ps_list_layers'
    'ps_manage_artboards'
    'ps_place_design_asset'
    'ps_preview_document'
    'ps_refresh_design_bundle'
    'ps_restore_document_snapshot'
    'ps_set_layer_mask'
    'ps_set_layer_properties'
    'ps_set_text_properties'
    'ps_validate_document'
    'python_bridge_ping'
    'python_runtime_info'
    'query_attributes'
    'raster_calc'
    'raster_change_detection'
    'raster_clip'
    'raster_mosaic'
    'raster_pixel_inspect'
    'raster_reproject'
    'raster_resample'
    'raster_statistics'
    'refresh_design_bundle'
    'remove_domain_from_field'
    'remove_join'
    'remove_layer'
    'remove_map'
    'rename_dataset'
    'rename_layer'
    'repair_geometry'
    'repair_layer_source'
    'restore_selection_snapshot'
    'restore_snapshot'
    'run_batch'
    'run_geoprocessing'
    'save_edits'
    'save_layer_file'
    'save_project'
    'scan_data_folder'
    'search_data'
    'select_by_attribute'
    'select_by_location'
    'select_layer'
    'set_basemap'
    'set_definition_query'
    'set_environment'
    'set_label_properties'
    'set_label_visibility'
    'set_layer_renderer'
    'set_layer_scale_range'
    'set_layer_transparency'
    'set_layer_visibility'
    'set_layout_element_properties'
    'set_map_extent'
    'set_map_properties'
    'set_map_view'
    'set_readonly_mode'
    'set_simple_symbology'
    'set_workspace'
    'simplify_features'
    'smooth_features'
    'snapshot_project'
    'solve_routes'
    'spatial_autocorrelation'
    'spatial_join'
    'suggest_workflow'
    'summarize_features'
    'trace_dataset_dependencies'
    'truncate_table'
    'union'
    'update_domain'
    'update_features'
    'validate_delivery_package'
    'validate_design_bundle'
    'validate_field_constraints'
    'validate_geometries'
    'validate_interchange_conformance'
    'validate_plan'
    'validate_relationship_class'
    'validate_statistical_assumptions'
    'zonal_histogram'
)
$script:supportedClientIds = @('codex', 'cursor', 'deepseek-harness')
$script:activeWorker = $null
$script:lastSelection = $null
$script:guiSmokeReport = $null

function Get-ProcessExists([string]$name) { return @((Get-Process -Name $name -ErrorAction SilentlyContinue)).Count -gt 0 }
function Send-Progress([scriptblock]$callback, [string]$message) { if ($null -ne $callback) { & $callback $message } }
function Test-Cancelled([scriptblock]$callback) { if ($null -eq $callback) { return $false }; return [bool](& $callback) }
function Test-TestCancellation([string]$step) { return (-not [string]::IsNullOrWhiteSpace($TestCancelAfterStep) -and [string]$TestCancelAfterStep -eq $step) }
function Get-SafeErrorCode([object]$result, [string]$fallback) {
    if ($null -ne $result -and $null -ne $result.Json) {
        if ($result.Json.errorCode) { return [string]$result.Json.errorCode }
        if (@($result.Json.failed).Count -gt 0 -and $result.Json.failed[0].errorCode) { return [string]$result.Json.failed[0].errorCode }
        if ($result.Json.overallStatus -and [string]$result.Json.overallStatus -ne 'PASS') { return [string]$result.Json.overallStatus }
    }
    return $fallback
}
function Test-PathWithin([string]$candidate, [string]$parent) {
    $child = [IO.Path]::GetFullPath($candidate).TrimEnd('\')
    $root = [IO.Path]::GetFullPath($parent).TrimEnd('\')
    return $child.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or $child.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)
}
function Wait-TestCancellationCallback([scriptblock]$cancel) {
    if (-not $TestMode -or [string]::IsNullOrWhiteSpace($TestCancelCallbackReadyPath)) { return $false }
    $readyPath = [IO.Path]::GetFullPath($TestCancelCallbackReadyPath)
    $parent = Split-Path -Parent $readyPath
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($readyPath, ('READY|plugin-install-callback|pid=' + $PID + '|at=' + [DateTime]::UtcNow.ToString('o')), [Text.UTF8Encoding]::new($false))
    $deadline = (Get-Date).AddSeconds(30)
    do {
        if (Test-Cancelled $cancel) { return $true }
        Start-Sleep -Milliseconds 25
    } while ((Get-Date) -lt $deadline)
    throw 'TEST_CANCEL_CALLBACK_TIMEOUT'
}
function Wait-TestInterruptBoundary([string]$stage) {
    if (-not $TestMode -or [string]::IsNullOrWhiteSpace($TestInterruptAfterPluginChildReadyPath)) { return }
    $readyPath = [IO.Path]::GetFullPath($TestInterruptAfterPluginChildReadyPath)
    $parent = Split-Path -Parent $readyPath
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($readyPath, ('READY|' + $stage + '|pid=' + $PID + '|at=' + [DateTime]::UtcNow.ToString('o')), [Text.UTF8Encoding]::new($false))
    $deadline = (Get-Date).AddSeconds(30)
    do {
        if (Test-Path -LiteralPath $readyPath -PathType Leaf) {
            $signal = Get-Content -LiteralPath $readyPath -Raw -Encoding UTF8
            if ([string]$signal -match '(?m)^RELEASE') { return }
        }
        Start-Sleep -Milliseconds 25
    } while ((Get-Date) -lt $deadline)
    throw 'TEST_INTERRUPT_BOUNDARY_TIMEOUT'
}
function Write-OneClickLog([string]$stage, [string]$status, [string]$errorCode = '') {
    if (-not (Test-Path -LiteralPath $script:stateRootResolved -PathType Container)) {
        New-Item -ItemType Directory -Path $script:stateRootResolved -Force | Out-Null
    }
    $safeCode = if ([string]::IsNullOrWhiteSpace($errorCode)) { '' } else { ([string]$errorCode -split '\|')[0] }
    if ($safeCode -notmatch '^[A-Za-z0-9_\-]*$') { $safeCode = 'UNSAFE_ERROR_CODE' }
    $entry = [ordered]@{
        schema = 'arcgis-pro-mcp-one-click-log-v1'
        atUtc = [DateTime]::UtcNow.ToString('o')
        stage = $stage
        status = $status
        errorCode = $safeCode
    }
    [IO.File]::AppendAllText($script:oneClickLogPath, (($entry | ConvertTo-Json -Compress -Depth 6) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
}
function Write-OneClickState([object]$state) {
    if (-not (Test-Path -LiteralPath $script:stateRootResolved -PathType Container)) {
        New-Item -ItemType Directory -Path $script:stateRootResolved -Force | Out-Null
    }
    if ($TestMode -and -not $script:stateWriteInjectionConsumed -and
        -not [string]::IsNullOrWhiteSpace($TestInjectStateWriteFailureAt) -and
        [string]$state.lastStage -ceq $TestInjectStateWriteFailureAt) {
        $script:stateWriteInjectionConsumed = $true
        throw 'TEST_INJECTED_ONE_CLICK_STATE_WRITE_FAILURE'
    }
    $state.schema = 'arcgis-pro-mcp-one-click-state-v1'
    $state.logPath = $script:oneClickLogPath
    $state.updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    $temporary = Join-Path $script:stateRootResolved ('.tmp-one-click-' + [guid]::NewGuid().ToString('N') + '.json')
    try {
        [IO.File]::WriteAllText($temporary, ($state | ConvertTo-Json -Depth 14), [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporary -Destination $script:oneClickStatePath -Force
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue }
    }
}
function New-RecoveryIndex {
    return [ordered]@{
        schema = 'arcgis-pro-mcp-one-click-recovery-index-v1'
        version = 1
        updatedAtUtc = [DateTime]::UtcNow.ToString('o')
        entries = @()
    }
}
function Get-RecoveryTransactionsRoot {
    return [IO.Path]::GetFullPath((Join-Path $script:stateRootResolved 'transactions'))
}
function Read-RecoveryIndex {
    if (-not (Test-Path -LiteralPath $script:recoveryIndexPath -PathType Leaf)) { return New-RecoveryIndex }
    try { $index = Get-Content -LiteralPath $script:recoveryIndexPath -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { throw 'RECOVERY_INDEX_INVALID' }
    if ([string]$index.schema -cne 'arcgis-pro-mcp-one-click-recovery-index-v1' -or [int]$index.version -ne 1 -or $null -eq $index.entries) {
        throw 'RECOVERY_INDEX_INVALID'
    }
    $transactionsRoot = Get-RecoveryTransactionsRoot
    foreach ($entry in @($index.entries)) {
        if ([string]::IsNullOrWhiteSpace([string]$entry.transactionRoot) -or
            [string]::IsNullOrWhiteSpace([string]$entry.ledgerPath) -or
            -not (Test-PathWithin ([string]$entry.transactionRoot) $transactionsRoot) -or
            -not (Test-PathWithin ([string]$entry.ledgerPath) ([string]$entry.transactionRoot))) {
            throw 'SAFE_TRANSACTION_PATH_REFUSED'
        }
    }
    return $index
}
function Read-RecoveryLedgerSummary([object]$entry) {
    if ($null -eq $entry -or [string]::IsNullOrWhiteSpace([string]$entry.ledgerPath) -or -not (Test-Path -LiteralPath ([string]$entry.ledgerPath) -PathType Leaf)) { return $null }
    try { $ledger = Get-Content -LiteralPath ([string]$entry.ledgerPath) -Raw -Encoding UTF8 | ConvertFrom-Json } catch { return $null }
    if ([string]$ledger.schema -cne 'arcgis-pro-mcp-release-transaction-v1') { return $null }
    return [pscustomobject]@{ Status = [string]$ledger.status; HasBaseline = ($null -ne $ledger.baseline); Path = [IO.Path]::GetFullPath([string]$entry.ledgerPath) }
}
function Write-RecoveryIndex([object]$index, [string]$operation = '') {
    if (-not (Test-Path -LiteralPath $script:stateRootResolved -PathType Container)) {
        New-Item -ItemType Directory -Path $script:stateRootResolved -Force | Out-Null
    }
    if ($TestMode -and -not $script:recoveryIndexWriteInjectionConsumed -and
        -not [string]::IsNullOrWhiteSpace($TestInjectRecoveryIndexWriteFailureAt) -and
        [string]$operation -ceq $TestInjectRecoveryIndexWriteFailureAt) {
        $script:recoveryIndexWriteInjectionConsumed = $true
        throw 'TEST_INJECTED_RECOVERY_INDEX_WRITE_FAILURE'
    }
    $index.schema = 'arcgis-pro-mcp-one-click-recovery-index-v1'
    $index.version = 1
    if ($null -eq $index.entries) { $index.entries = @() }
    $index.updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    $temporary = Join-Path $script:stateRootResolved ('.tmp-recovery-index-' + [guid]::NewGuid().ToString('N') + '.json')
    try {
        [IO.File]::WriteAllText($temporary, ($index | ConvertTo-Json -Depth 14), [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporary -Destination $script:recoveryIndexPath -Force
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue }
    }
}
function Register-RecoveryTransaction([pscustomobject]$transaction, [pscustomobject]$selection) {
    $index = Read-RecoveryIndex
    $transactionRoot = [IO.Path]::GetFullPath([string]$transaction.Root)
    $ledgerPath = [IO.Path]::GetFullPath([string]$transaction.Ledger)
    $transactionsRoot = Get-RecoveryTransactionsRoot
    if (-not (Test-PathWithin $transactionRoot $transactionsRoot) -or -not (Test-PathWithin $ledgerPath $transactionRoot)) { throw 'SAFE_TRANSACTION_PATH_REFUSED' }
    $existing = @($index.entries | Where-Object { [string]$_.transactionRoot -ieq $transactionRoot })
    if ($existing.Count -gt 0) { return $existing[0] }
    $clientId = if (@($selection.clients).Count -eq 1) { [string](@($selection.clients)[0]) } else { '' }
    $entry = [ordered]@{
        id = [guid]::NewGuid().ToString('N')
        registeredAtUtc = [DateTime]::UtcNow.ToString('o')
        updatedAtUtc = [DateTime]::UtcNow.ToString('o')
        transactionRoot = $transactionRoot
        ledgerPath = $ledgerPath
        clientId = $clientId
        projectRoot = [string]$selection.projectRoot
        status = 'STARTED'
        recoverable = $true
        recoveryState = 'PENDING'
        errorCode = $null
    }
    $index.entries += ,$entry
    Write-RecoveryIndex $index 'register'
    return $entry
}
function Update-RecoveryTransaction([string]$transactionRoot, [string]$status, [bool]$recoverable, [string]$errorCode = '', [string]$recoveryState = 'AVAILABLE') {
    $index = Read-RecoveryIndex
    $root = [IO.Path]::GetFullPath($transactionRoot)
    $entry = @($index.entries | Where-Object { [string]$_.transactionRoot -ieq $root }) | Select-Object -First 1
    if ($null -eq $entry) { throw 'RECOVERY_INDEX_ENTRY_NOT_FOUND' }
    $entry.status = $status
    $entry.recoverable = [bool]$recoverable
    $entry.recoveryState = $recoveryState
    $entry.errorCode = if ([string]::IsNullOrWhiteSpace($errorCode)) { $null } else { $errorCode }
    $entry.updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    Write-RecoveryIndex $index ('update-' + $status.ToLowerInvariant())
    return $entry
}
function Get-LatestRecoverableTransaction {
    $index = Read-RecoveryIndex
    $candidates = @($index.entries | Where-Object {
        $active = [bool]$_.recoverable -and [string]$_.recoveryState -cne 'COMPLETED'
        $legacyInterrupted = [string]$_.status -eq 'STARTED' -and [string]$_.recoveryState -in @('NOT_READY', 'PENDING') -and $null -ne (Read-RecoveryLedgerSummary $_)
        $active -or $legacyInterrupted
    } | Sort-Object -Property updatedAtUtc)
    if ($candidates.Count -eq 0) { return $null }
    return $candidates[$candidates.Count - 1]
}
function Resolve-RecoveryTransaction {
    $candidate = Get-LatestRecoverableTransaction
    if ($null -ne $candidate) {
        $ledgerSummary = Read-RecoveryLedgerSummary $candidate
        return [pscustomobject]@{ TransactionRoot = [IO.Path]::GetFullPath([string]$candidate.transactionRoot); LedgerPath = [IO.Path]::GetFullPath([string]$candidate.ledgerPath); Source = 'recovery-index'; Entry = $candidate; LedgerStatus = if ($null -eq $ledgerSummary) { 'NOT_AVAILABLE' } else { $ledgerSummary.Status } }
    }
    $oneClickState = $null
    try { $oneClickState = Read-OneClickState } catch { $oneClickState = $null }
    if ($null -ne $oneClickState -and $oneClickState.plugin -and $oneClickState.plugin.transactionRoot) {
        return [pscustomobject]@{ TransactionRoot = [IO.Path]::GetFullPath([string]$oneClickState.plugin.transactionRoot); LedgerPath = [IO.Path]::GetFullPath([string]$oneClickState.plugin.ledgerPath); Source = 'one-click-state'; Entry = $null }
    }
    $legacy = Read-InstallerState
    return [pscustomobject]@{ TransactionRoot = $legacy.TransactionRoot; LedgerPath = $legacy.LedgerPath; Source = 'legacy-installer-state'; Entry = $null }
}
function Read-OneClickState {
    if (-not (Test-Path -LiteralPath $script:oneClickStatePath -PathType Leaf)) { return $null }
    try { $state = Get-Content -LiteralPath $script:oneClickStatePath -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { throw 'ONE_CLICK_STATE_INVALID' }
    if ([string]$state.schema -cne 'arcgis-pro-mcp-one-click-state-v1' -or [string]::IsNullOrWhiteSpace([string]$state.deploymentId)) {
        throw 'ONE_CLICK_STATE_INVALID'
    }
    if ($null -eq $state.plugin -or [string]::IsNullOrWhiteSpace([string]$state.plugin.transactionRoot) -or [string]::IsNullOrWhiteSpace([string]$state.plugin.ledgerPath)) {
        throw 'ONE_CLICK_STATE_INVALID'
    }
    $transactionsRoot = [IO.Path]::GetFullPath((Join-Path $script:stateRootResolved 'transactions'))
    if (-not (Test-PathWithin ([string]$state.plugin.transactionRoot) $transactionsRoot) -or
        -not (Test-PathWithin ([string]$state.plugin.ledgerPath) ([string]$state.plugin.transactionRoot))) {
        throw 'SAFE_TRANSACTION_PATH_REFUSED'
    }
    if ($state.client -and -not [string]::IsNullOrWhiteSpace([string]$state.client.id) -and $script:supportedClientIds -notcontains [string]$state.client.id) {
        throw 'CLIENT_NOT_SUPPORTED'
    }
    return $state
}
function New-OneClickState([pscustomobject]$transaction, [pscustomobject]$selection) {
    $clientId = if (@($selection.clients).Count -eq 1) { [string](@($selection.clients)[0]) } else { '' }
    return [ordered]@{
        schema = 'arcgis-pro-mcp-one-click-state-v1'
        deploymentId = [guid]::NewGuid().ToString('N')
        status = 'STARTED'
        lastStage = 'started'
        plugin = [ordered]@{ status = 'NOT_STARTED'; transactionRoot = $transaction.Root; ledgerPath = $transaction.Ledger }
        client = [ordered]@{ id = $clientId; status = if ($clientId) { 'NOT_STARTED' } else { 'NOT_SELECTED' }; projectRoot = [string]$selection.projectRoot }
        diagnosis = [ordered]@{ status = 'NOT_RUN'; httpToolDiscovery = 'NOT_RUN'; httpPing = 'NOT_RUN'; clientVerification = 'NOT_VERIFIED' }
        failedClient = $null
        steps = @()
        logPath = $script:oneClickLogPath
        updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
}
function Set-OneClickStep([object]$state, [string]$stage, [string]$status, [string]$errorCode = '') {
    if ($null -eq $state.steps) { $state.steps = @() }
    $state.lastStage = $stage
    $state.steps += ,([ordered]@{ name = $stage; status = $status; errorCode = if ($errorCode) { $errorCode } else { $null } })
    Write-OneClickLog $stage $status $errorCode
    Write-OneClickState $state
}
function Assert-TestModeScope([pscustomobject]$selection = $null) {
    if (-not $TestMode) { return }
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    foreach ($path in @($script:installRootResolved, $script:stateRootResolved)) {
        if (-not (Test-PathWithin $path $tempRoot)) { throw 'TEST_MODE_PATH_REFUSED' }
    }
    if ($null -ne $selection) {
        if (@($selection.clients | Where-Object { $_ -in @('codex', 'cursor') }).Count -gt 0 -and
            ([string]::IsNullOrWhiteSpace($selection.projectRoot) -or -not (Test-PathWithin $selection.projectRoot $tempRoot))) {
            throw 'TEST_MODE_PROJECT_PATH_REFUSED'
        }
        if (@($selection.clients | Where-Object { $_ -eq 'deepseek-harness' }).Count -gt 0 -and -not $DryRun) {
            throw 'TEST_MODE_EXTERNAL_CLIENT_REFUSED'
        }
    }
}
function Assert-TestInjectionScope {
    $hasInjection = [bool]$TestInjectChildFailure -or -not [string]::IsNullOrWhiteSpace($TestInjectStateWriteFailureAt) -or -not [string]::IsNullOrWhiteSpace($TestInjectRecoveryIndexWriteFailureAt) -or -not [string]::IsNullOrWhiteSpace($TestCancelCallbackReadyPath) -or -not [string]::IsNullOrWhiteSpace($TestInterruptAfterPluginChildReadyPath)
    if ($hasInjection -and -not $TestMode) {
        throw 'TEST_INJECTION_REFUSED'
    }
    if (-not [string]::IsNullOrWhiteSpace($TestInjectRecoveryIndexWriteFailureAt) -and @('register', 'update-pass') -notcontains $TestInjectRecoveryIndexWriteFailureAt) { throw 'TEST_INJECTION_PARAMETER_INVALID' }
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    foreach ($path in @($TestCancelCallbackReadyPath, $TestInterruptAfterPluginChildReadyPath)) {
        if (-not [string]::IsNullOrWhiteSpace($path) -and -not (Test-PathWithin $path $tempRoot)) { throw 'TEST_MODE_CONTROL_PATH_REFUSED' }
    }
}
function Invoke-ChildJson([string]$path, [string[]]$arguments) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return [pscustomobject]@{ ExitCode = 127; Text = ''; Json = $null } }
    $shell = Get-Command powershell.exe -ErrorAction SilentlyContinue
    if ($null -eq $shell) { return [pscustomobject]@{ ExitCode = 127; Text = ''; Json = $null } }
    $lines = @(& $shell.Source -NoLogo -NoProfile -ExecutionPolicy Bypass -File $path @arguments 2>&1 | ForEach-Object { [string]$_ })
    $exitCode = [int]$LASTEXITCODE
    $text = $lines -join [Environment]::NewLine
    $parsed = $null
    try { $parsed = $text | ConvertFrom-Json } catch { }
    return [pscustomobject]@{ ExitCode = $exitCode; Text = $text; Json = $parsed }
}
function Get-ClientTarget([string]$clientId, [string]$projectPath) {
    switch ($clientId) {
        'codex' { if ([string]::IsNullOrWhiteSpace($projectPath)) { return '[需先选择项目目录]'; }; return Join-Path $projectPath '.codex\config.toml' }
        'cursor' { if ([string]::IsNullOrWhiteSpace($projectPath)) { return '[需先选择项目目录]'; }; return Join-Path $projectPath '.cursor\mcp.json' }
        'deepseek-harness' { return Join-Path ([Environment]::GetFolderPath('UserProfile')) '.dsh\profiles\web\cordis.patch.yml' }
        default { return '[unsupported client]' }
    }
}
function Get-ClientTargets([string[]]$clients, [string]$projectPath) {
    $items = @()
    foreach ($client in @($clients)) { $items += [ordered]@{ client = $client; target = Get-ClientTarget $client $projectPath } }
    return @($items)
}
function Write-InstallerState([string]$transactionRoot, [string]$ledgerPath, [string]$lastAction) {
    if (-not (Test-Path -LiteralPath $script:stateRootResolved -PathType Container)) { New-Item -ItemType Directory -Path $script:stateRootResolved -Force | Out-Null }
    $value = [ordered]@{
        schema = 'arcgis-pro-mcp-share-installer-state-v1'; addInId = $script:addInId
        installRoot = $script:installRootResolved; transactionRoot = $transactionRoot; ledgerPath = $ledgerPath
        lastAction = $lastAction; updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json -Depth 8
    $temporary = Join-Path $script:stateRootResolved ('.tmp-' + [guid]::NewGuid().ToString('N'))
    try { [IO.File]::WriteAllText($temporary, $value, [Text.UTF8Encoding]::new($false)); Move-Item -LiteralPath $temporary -Destination $script:statePath -Force }
    finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue } }
}
function Read-InstallerState {
    if (-not (Test-Path -LiteralPath $script:statePath -PathType Leaf)) { throw 'SAFE_TRANSACTION_STATE_NOT_FOUND' }
    $state = Get-Content -LiteralPath $script:statePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$state.schema -cne 'arcgis-pro-mcp-share-installer-state-v1' -or [string]$state.addInId -cne $script:addInId -or [IO.Path]::GetFullPath([string]$state.installRoot) -cne $script:installRootResolved) { throw 'SAFE_TRANSACTION_STATE_INVALID' }
    $transactionRoot = [IO.Path]::GetFullPath([string]$state.transactionRoot)
    $ledgerPath = [IO.Path]::GetFullPath([string]$state.ledgerPath)
    $transactionsRoot = [IO.Path]::GetFullPath((Join-Path $script:stateRootResolved 'transactions')).TrimEnd('\') + '\'
    if (-not $transactionRoot.StartsWith($transactionsRoot, [StringComparison]::OrdinalIgnoreCase) -or -not $ledgerPath.StartsWith($transactionRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'SAFE_TRANSACTION_PATH_REFUSED' }
    return [pscustomobject]@{ TransactionRoot = $transactionRoot; LedgerPath = $ledgerPath }
}
function Get-NewTransactionPaths {
    $root = Join-Path $script:stateRootResolved ('transactions\one-click-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
    return [pscustomobject]@{ Root = $root; Ledger = Join-Path $root 'ledger.json' }
}
function Test-Bundle([scriptblock]$progress) {
    Send-Progress $progress '正在校验分享包清单、ZIP/解压文件哈希和 1.0.2 插件载荷……'
    $result = Invoke-ChildJson $script:verifyPath @('-RootPath', $script:bundleRootResolved, '-Json')
    if ($result.ExitCode -ne 0 -or $null -eq $result.Json -or [string]$result.Json.status -ne 'PASS') { return [pscustomobject]@{ status = 'FAIL'; errorCode = Get-SafeErrorCode $result 'BUNDLE_VERIFICATION_FAILED' } }
    return [pscustomobject]@{ status = 'PASS'; report = $result.Json }
}
function Get-PayloadConfigDaml {
    # D-111 (G-243 R-1): the checked Config.daml must come from the payload that will actually be installed
    # (dual generation: net8 package = desktopVersion 3.5.0 / net6 package = 3.0). Extraction follows the
    # accepted D-072 (C-015) precedent in release-transaction.ps1; the copy lives in a throwaway temporary
    # directory and is removed by the caller as soon as the checker has answered.
    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
    if (-not (Test-Path -LiteralPath $script:packagePath -PathType Leaf)) { throw 'COMPATIBILITY_PAYLOAD_NOT_FOUND' }
    $scratchRoot = Join-Path ([IO.Path]::GetTempPath()) ('arcgis-pro-mcp-compat-config-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratchRoot -Force | Out-Null
    $targetPath = Join-Path $scratchRoot 'Config.daml'
    $zip = $null
    try {
        $zip = [IO.Compression.ZipFile]::OpenRead($script:packagePath)
        $entry = $zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -ceq 'Config.daml' } | Select-Object -First 1
        if ($null -eq $entry) { throw 'COMPATIBILITY_PAYLOAD_CONFIG_DAML_MISSING' }
        $entryStream = $entry.Open(); $buffer = New-Object IO.MemoryStream
        try { $entryStream.CopyTo($buffer); [IO.File]::WriteAllBytes($targetPath, $buffer.ToArray()) }
        finally { $buffer.Dispose(); $entryStream.Dispose() }
    } catch {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force -ErrorAction SilentlyContinue
        throw
    } finally { if ($null -ne $zip) { $zip.Dispose() } }
    $desktopVersion = ''
    $damlText = [IO.File]::ReadAllText($targetPath, [Text.Encoding]::UTF8)
    $versionMatch = [regex]::Match($damlText, 'desktopVersion="([^"]+)"')
    if ($versionMatch.Success) { $desktopVersion = $versionMatch.Groups[1].Value }
    return [pscustomobject]@{ Path = $targetPath; ScratchRoot = $scratchRoot; Source = [ordered]@{ payload = $script:packagePath; configDamlOrigin = 'payload entry Config.daml (same-source)'; desktopVersion = $desktopVersion } }
}
function Invoke-Compatibility([scriptblock]$progress) {
    Send-Progress $progress '正在检查 Windows、ArcGIS Pro 3.5、.NET 8、SDK 与 ArcPy 前置条件……'
    # D-111 (G-243 R-1): -ConfigPath now comes from the selected payload, never from the outer metadata file.
    $extracted = Get-PayloadConfigDaml
    try {
        $result = Invoke-ChildJson $script:checkerPath @('-PolicyPath', $script:policyPath, '-ManifestPath', $script:manifestPath, '-ConfigPath', $extracted.Path, '-Json')
    } finally {
        Remove-Item -LiteralPath $extracted.ScratchRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($null -eq $result.Json) { return [pscustomobject]@{ status = 'ERROR'; errorCode = 'COMPATIBILITY_CHECK_FAILED' } }
    return [pscustomobject]@{ status = [string]$result.Json.overallStatus; exitCode = $result.ExitCode; configSource = $extracted.Source; report = $result.Json }
}
function Test-InstallPreflight([bool]$allowCreate = $false) {
    if (Get-ProcessExists 'ArcGISPro') { return [pscustomobject]@{ status = 'FAIL'; errorCode = 'ARCGIS_PRO_RUNNING'; message = '请先关闭 ArcGIS Pro，安装器不会强制关闭它。' } }
    if (Test-Path -LiteralPath $script:installRootResolved -PathType Leaf) { return [pscustomobject]@{ status = 'FAIL'; errorCode = 'INSTALL_ROOT_INVALID'; message = 'ArcGIS Pro Add-ins 目标路径是文件，不是目录。' } }
    if (-not (Test-Path -LiteralPath $script:installRootResolved -PathType Container)) {
        if ($allowCreate) { return [pscustomobject]@{ status = 'PASS'; installRootWillBeCreated = $true; message = '安装目标目录尚不存在；明确部署写入时将安全创建该目录。' } }
        return [pscustomobject]@{ status = 'FAIL'; errorCode = 'INSTALL_ROOT_NOT_FOUND'; message = 'ArcGIS Pro Add-ins 目录不存在。明确部署时安装器会安全创建它。' }
    }
    return [pscustomobject]@{ status = 'PASS' }
}
function Test-ClientPreflight([string]$client, [string]$projectPath) {
    if ($client -in @('codex', 'cursor') -and -not (Test-Path -LiteralPath $projectPath -PathType Container)) { return [pscustomobject]@{ status = 'FAIL'; errorCode = 'PROJECT_ROOT_NOT_FOUND'; message = '请选择存在的 Codex/Cursor 项目目录。' } }
    if ($client -eq 'cursor' -and (Get-ProcessExists 'Cursor')) { return [pscustomobject]@{ status = 'FAIL'; errorCode = 'CLIENT_RUNNING'; message = '请先关闭 Cursor；安装器不会强制关闭客户端。' } }
    return [pscustomobject]@{ status = 'PASS' }
}
function Invoke-ClientValidate([string]$client, [string]$projectPath, [scriptblock]$progress) {
    Send-Progress $progress ('正在只读校验 ' + $client + ' 配置……')
    $args = @('-Action', 'Validate', '-Client', $client, '-CatalogPath', $script:catalogPath, '-TemplateRoot', $script:bundleRootResolved, '-Json')
    if ($client -in @('codex', 'cursor')) { $args += @('-ConfigRoot', $projectPath) }
    $result = Invoke-ChildJson $script:clientConfigPath $args
    if ($result.ExitCode -ne 0 -or $null -eq $result.Json) { return [pscustomobject]@{ status = 'FAIL'; client = $client; errorCode = Get-SafeErrorCode $result 'CLIENT_VALIDATE_FAILED' } }
    return [pscustomobject]@{ status = 'PASS'; client = $client; detail = 'read-only validation passed' }
}
function Invoke-ClientApply([string]$client, [string]$projectPath, [scriptblock]$progress, [bool]$dryRun) {
    Send-Progress $progress (($client + $(if ($dryRun) { ' 只读预演……' } else { ' 配置事务……' })))
    $args = @('-Action', $(if ($dryRun) { 'Validate' } else { 'ClientApply' }), '-Client', $client, '-CatalogPath', $script:catalogPath, '-TemplateRoot', $script:bundleRootResolved, '-Json')
    if ($client -in @('codex', 'cursor')) { $args += @('-ConfigRoot', $projectPath) }
    $result = Invoke-ChildJson $script:workflowPath $args
    if ($result.ExitCode -ne 0 -or $null -eq $result.Json -or [string]$result.Json.status -ne 'PASS') { return [pscustomobject]@{ status = 'FAIL'; client = $client; errorCode = Get-SafeErrorCode $result 'CLIENT_CONFIGURATION_FAILED' } }
    return [pscustomobject]@{ status = 'PASS'; client = $client; detail = if ($dryRun) { 'read-only validation passed' } else { 'single-client transaction passed' } }
}
function Test-TcpPort([string]$hostName, [int]$port) {
    $client = New-Object Net.Sockets.TcpClient
    try { $pending = $client.BeginConnect($hostName, $port, $null, $null); if (-not $pending.AsyncWaitHandle.WaitOne(1000)) { return $false }; $client.EndConnect($pending); return $client.Connected }
    catch { return $false }
    finally { $client.Dispose() }
}
function Read-HttpJsonRpc([string]$method, [hashtable]$params) {
    $requestId = [guid]::NewGuid().ToString('N')
    $body = [ordered]@{ jsonrpc = '2.0'; id = $requestId; method = $method; params = $params } | ConvertTo-Json -Depth 8
    $request = [Net.HttpWebRequest]::Create($script:endpoint); $request.Method = 'POST'; $request.ContentType = 'application/json'; $request.Accept = 'application/json, text/event-stream'; $request.Timeout = 2500; $request.ReadWriteTimeout = 2500
    $bytes = [Text.Encoding]::UTF8.GetBytes($body); $request.ContentLength = $bytes.Length
    try {
        $stream = $request.GetRequestStream(); try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
        $response = $request.GetResponse(); try { $reader = New-Object IO.StreamReader($response.GetResponseStream()); try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() } } finally { $response.Dispose() }
        return [pscustomobject]@{ RequestId = $requestId; Text = $text }
    } catch { return $null }
}
function Convert-McpResponse([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    try { return $text | ConvertFrom-Json } catch { }
    foreach ($line in ($text -split "`r?`n")) {
        if ($line -match '^data:\s*(\{.*\})\s*$') { try { return ($Matches[1] | ConvertFrom-Json) } catch { } }
    }
    return $null
}
function Invoke-McpJsonRpc([string]$method, [hashtable]$params) {
    $raw = Read-HttpJsonRpc $method $params
    if ($null -eq $raw) { return [pscustomobject]@{ Valid = $false; Response = $null; ErrorCode = 'HTTP_RESPONSE_NOT_RECEIVED' } }
    $response = Convert-McpResponse ([string]$raw.Text)
    if ($null -eq $response) { return [pscustomobject]@{ Valid = $false; Response = $null; ErrorCode = 'MCP_RESPONSE_INVALID' } }
    if ($null -eq $response.PSObject.Properties['id'] -or [string]$response.id -cne [string]$raw.RequestId) {
        return [pscustomobject]@{ Valid = $false; Response = $null; ErrorCode = 'MCP_RESPONSE_ID_MISMATCH' }
    }
    if ($null -ne $response.PSObject.Properties['error'] -and $null -ne $response.error) {
        return [pscustomobject]@{ Valid = $false; Response = $response; ErrorCode = 'MCP_JSONRPC_ERROR' }
    }
    if ($null -eq $response.PSObject.Properties['result'] -or $null -eq $response.result) {
        return [pscustomobject]@{ Valid = $false; Response = $response; ErrorCode = 'MCP_RESULT_MISSING' }
    }
    return [pscustomobject]@{ Valid = $true; Response = $response; ErrorCode = '' }
}
function Invoke-ConnectionDiagnosis([scriptblock]$progress) {
    if (-not (Test-TcpPort '127.0.0.1' 6520)) {
        Send-Progress $progress '等待用户启动：请打开 ArcGIS Pro，在 MCP 选项卡点击 Start。当前 6520 尚未监听。'
        return [ordered]@{ status = 'WAITING_USER_START'; portListening = $false; httpToolDiscovery = 'NOT_VERIFIED'; httpPing = 'NOT_VERIFIED'; clientVerification = 'NOT_VERIFIED'; message = '端口未监听；请由用户在 ArcGIS Pro 中点击 Start。' }
    }
    Send-Progress $progress '6520 已监听，正在区分端口可达与 MCP 工具发现……'
    $listCall = Invoke-McpJsonRpc 'tools/list' @{}
    if (-not $listCall.Valid -or $null -eq $listCall.Response.result.tools -or [bool]$listCall.Response.result.isError) {
        Send-Progress $progress '端口可达但未确认 MCP tools/list；不会把端口监听当作 MCP 已连接。'
        return [ordered]@{ status = 'PORT_OPEN_NOT_MCP'; portListening = $true; httpToolDiscovery = 'NOT_VERIFIED'; httpPing = 'NOT_VERIFIED'; clientVerification = 'NOT_VERIFIED'; errorCode = $listCall.ErrorCode; message = '6520 可达，但未取得有效 MCP tools/list。' }
    }
    $tools = @($listCall.Response.result.tools)
    $names = @($tools | ForEach-Object { [string]$_.name })
    $duplicateGroups = @($names | Group-Object | Where-Object { $_.Count -gt 1 })
    if ($duplicateGroups.Count -gt 0) {
        return [ordered]@{ status = 'TOOL_DUPLICATE'; portListening = $true; httpToolDiscovery = 'FAIL'; httpToolCount = $names.Count; uniqueToolCount = @($names | Select-Object -Unique).Count; httpPing = 'NOT_VERIFIED'; clientVerification = 'NOT_VERIFIED'; message = 'MCP tools/list 含有重复工具名，不能按数量冒充 canonical 239。' }
    }
    $missing = @($script:canonicalToolNames | Where-Object { $names -notcontains $_ })
    $unexpected = @($names | Where-Object { $script:canonicalToolNames -notcontains $_ })
    if ($names.Count -ne $script:canonicalToolCount -or $missing.Count -gt 0 -or $unexpected.Count -gt 0) {
        return [ordered]@{ status = 'TOOL_SET_MISMATCH'; portListening = $true; httpToolDiscovery = 'FAIL'; httpToolCount = $names.Count; uniqueToolCount = @($names | Select-Object -Unique).Count; missingToolCount = $missing.Count; unexpectedToolCount = $unexpected.Count; httpPing = 'NOT_VERIFIED'; clientVerification = 'NOT_VERIFIED'; message = 'MCP tools/list 未精确匹配 canonical production tool set。' }
    }
    $pingCall = Invoke-McpJsonRpc 'tools/call' @{ name = 'ping'; arguments = @{} }
    $pong = $false
    if ($pingCall.Valid -and $null -ne $pingCall.Response.result -and -not [bool]$pingCall.Response.result.isError) {
        $pong = @($pingCall.Response.result.content | Where-Object { [string]$_.type -ceq 'text' -and (([string]$_.text).Trim() -ceq 'pong') }).Count -gt 0
    }
    if (-not $pong) { return [ordered]@{ status = 'HTTP_PING_NOT_CONFIRMED'; portListening = $true; httpToolDiscovery = 'PASS'; httpToolCount = $names.Count; httpPing = 'NOT_VERIFIED'; clientVerification = 'NOT_VERIFIED'; errorCode = if ($pingCall.Valid) { 'HTTP_PING_NOT_CONFIRMED' } else { $pingCall.ErrorCode }; message = 'tools/list canonical 239 已确认，但 ping 未确认返回精确文本 pong。' } }
    Send-Progress $progress 'HTTP tools/list=239 与只读 ping=pong 已确认；真实 AI 客户端验证仍未执行。'
    return [ordered]@{ status = 'PASS'; portListening = $true; httpToolDiscovery = 'PASS'; httpToolCount = $names.Count; httpPing = 'PASS'; clientVerification = 'NOT_VERIFIED'; message = 'HTTP 工具发现和 ping 通过；Codex/Cursor/DeepSeek 客户端自身连接仍为 NOT VERIFIED。' }
}
function Invoke-ReadOnlyPreflight([pscustomobject]$selection, [scriptblock]$progress, [scriptblock]$cancel) {
    $steps = @(); $clients = @($selection.clients)
    if (Test-Cancelled $cancel) { return [ordered]@{ status = 'CANCELLED'; steps = @(); next = '可重新点击预检或开始。' } }
    $bundle = Test-Bundle $progress; $steps += [ordered]@{ name = 'bundle-integrity'; status = $bundle.status; errorCode = $bundle.errorCode }
    if ($bundle.status -ne 'PASS') { return [ordered]@{ status = 'FAILED'; steps = $steps; errorCode = $bundle.errorCode } }
    if (Test-TestCancellation 'bundle-integrity') { return [ordered]@{ status = 'CANCELLED'; steps = $steps; next = '测试取消点：未执行安装或客户端配置。' } }
    $compat = Invoke-Compatibility $progress; $steps += [ordered]@{ name = 'compatibility'; status = $compat.status; errorCode = if ($compat.status -eq 'PASS') { $null } else { 'COMPATIBILITY_NOT_PASS' } }
    if ($compat.status -ne 'PASS') { return [ordered]@{ status = 'FAILED'; steps = $steps; errorCode = 'COMPATIBILITY_NOT_PASS'; compatibility = $compat.report } }
    $install = Test-InstallPreflight $true; $steps += [ordered]@{ name = 'install-preflight'; status = $install.status; errorCode = $install.errorCode; installRootWillBeCreated = [bool]$install.installRootWillBeCreated }
    if ($install.status -ne 'PASS') { return [ordered]@{ status = 'FAILED'; steps = $steps; errorCode = $install.errorCode; message = $install.message } }
    foreach ($client in $clients) {
        if (Test-Cancelled $cancel) { return [ordered]@{ status = 'CANCELLED'; steps = $steps; next = '已停止在未执行客户端 mutation 的边界。' } }
        $check = Test-ClientPreflight $client $selection.projectRoot; $steps += [ordered]@{ name = 'client-preflight:' + $client; status = $check.status; errorCode = $check.errorCode }
        if ($check.status -ne 'PASS') { return [ordered]@{ status = 'FAILED'; steps = $steps; errorCode = $check.errorCode; message = $check.message } }
        $valid = Invoke-ClientValidate $client $selection.projectRoot $progress; $steps += [ordered]@{ name = 'client-validate:' + $client; status = $valid.status; errorCode = $valid.errorCode }
        if ($valid.status -ne 'PASS') { return [ordered]@{ status = 'FAILED'; steps = $steps; errorCode = $valid.errorCode } }
    }
    return [ordered]@{ status = 'PASS'; steps = $steps; pluginInstall = 'NOT_RUN'; clientConfiguration = if ($clients.Count -eq 0) { 'NOT_SELECTED' } else { 'NOT_RUN' }; httpToolDiscovery = 'NOT_RUN'; httpPing = 'NOT_RUN'; clientVerification = 'NOT_VERIFIED'; message = '预检完成，未写入插件、客户端配置或事务状态。' }
}
function Invoke-DeploymentFlow([pscustomobject]$selection, [scriptblock]$progress, [scriptblock]$cancel) {
    Assert-TestInjectionScope
    Assert-TestModeScope $selection
    $clients = @($selection.clients); $steps = @()
    $recoverable = Get-LatestRecoverableTransaction
    if ($null -ne $recoverable) {
        return [ordered]@{ status = 'FAILED'; errorCode = 'EXISTING_DEPLOYMENT_REQUIRES_RECOVERY'; pluginInstall = [string]$recoverable.status; clientConfiguration = 'NOT_VERIFIED'; clientVerification = 'NOT_VERIFIED'; recoveryIndex = 'AVAILABLE'; transactionRoot = [string]$recoverable.transactionRoot; ledgerPath = [string]$recoverable.ledgerPath; next = '请先使用恢复/卸载插件；恢复索引优先于任何旧 latest-transaction。' }
    }
    $existing = Read-OneClickState
    if ($null -ne $existing -and [string]$existing.plugin.status -in @('PASS', 'ACTIVE', 'INSTALLING', 'FAIL', 'CANCELLED')) {
        return [ordered]@{ status = 'FAILED'; errorCode = 'EXISTING_DEPLOYMENT_REQUIRES_RECOVERY'; pluginInstall = [string]$existing.plugin.status; clientConfiguration = [string]$existing.client.status; clientVerification = 'NOT_VERIFIED'; next = '请先使用恢复/卸载插件；连接重试只做诊断，不会重新安装。' }
    }
    Send-Progress $progress '开始一键部署：预检 → 完整性校验 → 插件事务 → 单客户端事务 → 连接诊断。'
    $bundle = Test-Bundle $progress; $steps += [ordered]@{ name = 'bundle-integrity'; status = $bundle.status; errorCode = $bundle.errorCode }
    if ($bundle.status -ne 'PASS') { return [ordered]@{ status = 'FAILED'; steps = $steps; errorCode = $bundle.errorCode; clientVerification = 'NOT_VERIFIED' } }
    if (Test-TestCancellation 'bundle-integrity') { return [ordered]@{ status = 'CANCELLED'; steps = $steps; pluginInstall = 'NOT_STARTED'; clientConfiguration = 'NOT_STARTED'; clientVerification = 'NOT_VERIFIED' } }
    $compat = Invoke-Compatibility $progress; $steps += [ordered]@{ name = 'compatibility'; status = $compat.status; errorCode = if ($compat.status -eq 'PASS') { $null } else { 'COMPATIBILITY_NOT_PASS' } }
    if ($compat.status -ne 'PASS') { return [ordered]@{ status = 'FAILED'; steps = $steps; errorCode = 'COMPATIBILITY_NOT_PASS'; compatibility = $compat.report; clientVerification = 'NOT_VERIFIED' } }
    if (Test-Cancelled $cancel) { return [ordered]@{ status = 'CANCELLED'; steps = $steps; pluginInstall = 'NOT_STARTED'; clientConfiguration = 'NOT_STARTED'; clientVerification = 'NOT_VERIFIED' } }
    $installCheck = Test-InstallPreflight $true; $steps += [ordered]@{ name = 'install-preflight'; status = $installCheck.status; errorCode = $installCheck.errorCode; installRootWillBeCreated = [bool]$installCheck.installRootWillBeCreated }
    if ($installCheck.status -ne 'PASS') { Send-Progress $progress $installCheck.message; return [ordered]@{ status = 'FAILED'; steps = $steps; errorCode = $installCheck.errorCode; message = $installCheck.message; clientVerification = 'NOT_VERIFIED' } }
    foreach ($client in $clients) {
        if (Test-Cancelled $cancel) { return [ordered]@{ status = 'CANCELLED'; steps = $steps; pluginInstall = 'NOT_STARTED'; clientConfiguration = 'NOT_STARTED'; clientVerification = 'NOT_VERIFIED' } }
        $check = Test-ClientPreflight $client $selection.projectRoot; $steps += [ordered]@{ name = 'client-preflight:' + $client; status = $check.status; errorCode = $check.errorCode }
        if ($check.status -ne 'PASS') { Send-Progress $progress $check.message; return [ordered]@{ status = 'FAILED'; steps = $steps; errorCode = $check.errorCode; message = $check.message; pluginInstall = 'NOT_STARTED'; clientConfiguration = 'NOT_STARTED'; clientVerification = 'NOT_VERIFIED' } }
        $valid = Invoke-ClientValidate $client $selection.projectRoot $progress; $steps += [ordered]@{ name = 'client-validate:' + $client; status = $valid.status; errorCode = $valid.errorCode }
        if ($valid.status -ne 'PASS') { return [ordered]@{ status = 'FAILED'; steps = $steps; errorCode = $valid.errorCode; pluginInstall = 'NOT_STARTED'; clientConfiguration = 'NOT_STARTED'; clientVerification = 'NOT_VERIFIED' } }
    }

    $transaction = Get-NewTransactionPaths
    $state = New-OneClickState $transaction $selection
    Register-RecoveryTransaction $transaction $selection | Out-Null
    Write-OneClickState $state; Write-OneClickLog 'deployment-start' 'PASS'; $state.status = 'INSTALLING'; Set-OneClickStep $state 'preflight' 'PASS'
    $installArgs = @('-Action', 'Install', '-PackagePath', $script:packagePath, '-ManifestPath', $script:manifestPath, '-InstallRoot', $script:installRootResolved, '-TransactionRoot', $transaction.Root, '-LedgerPath', $transaction.Ledger, '-AllowCreateInstallRoot', '-Json')
    if ($DryRun) { $installArgs += '-DryRun' }; if ($TestMode) { $installArgs += '-TestMode' }
    if ($TestInjectChildFailure) { $installArgs += '-TestInjectChildFailure' }
    $state.plugin.status = 'INSTALLING'; $state.status = 'INSTALLING'; Set-OneClickStep $state 'plugin-install-start' 'STARTED'
    $installResult = Invoke-ChildJson $script:workflowPath $installArgs
    if ($installResult.ExitCode -ne 0 -or $null -eq $installResult.Json -or [string]$installResult.Json.status -ne 'PASS') {
        $code = Get-SafeErrorCode $installResult 'INSTALL_FAILED'; Send-Progress $progress ('插件安装未完成：' + $code); Update-RecoveryTransaction $transaction.Root 'FAIL' (Test-Path -LiteralPath $transaction.Ledger -PathType Leaf) $code 'AVAILABLE' | Out-Null; $state.status = 'FAILED'; $state.plugin.status = 'FAIL'; Set-OneClickStep $state 'plugin-install' 'FAIL' $code
        return [ordered]@{ status = 'FAILED'; steps = $steps + [ordered]@{ name = 'plugin-install'; status = 'FAIL'; errorCode = $code }; errorCode = $code; clientConfiguration = 'NOT_STARTED'; clientVerification = 'NOT_VERIFIED' }
    }
    Wait-TestInterruptBoundary 'plugin-install-child-completed'
    $pluginStatus = if ($DryRun) { 'DRY_RUN_PASS' } else { 'PASS' }; $recoveryStage = if ($DryRun) { 'NOT_REQUIRED' } else { 'AVAILABLE' }; Update-RecoveryTransaction $transaction.Root 'PASS' (-not $DryRun) '' $recoveryStage | Out-Null; $state.plugin.status = $pluginStatus; $state.status = $pluginStatus; Set-OneClickStep $state 'plugin-install' $pluginStatus
    if (-not $DryRun) { Write-InstallerState $transaction.Root $transaction.Ledger 'Install' }
    $steps += [ordered]@{ name = 'plugin-install'; status = $pluginStatus }
    if ($DryRun) { return [ordered]@{ status = 'DRY_RUN_PASS'; steps = $steps; pluginInstall = 'DRY_RUN_PASS'; clientConfiguration = 'NOT_RUN'; httpToolDiscovery = 'NOT_RUN'; httpPing = 'NOT_RUN'; clientVerification = 'NOT_VERIFIED'; message = '插件事务预演通过，未修改安装目标或客户端配置。' } }
    $callbackCancelled = Wait-TestCancellationCallback $cancel
    if ((Test-TestCancellation 'plugin-install') -or $callbackCancelled -or (Test-Cancelled $cancel)) {
        Update-RecoveryTransaction $transaction.Root 'CANCELLED' $true '' 'AVAILABLE' | Out-Null
        $state.status = 'CANCELLED'; $state.client.status = 'NOT_STARTED'; Set-OneClickStep $state 'client-configuration' 'NOT_STARTED'
        return [ordered]@{ status = 'CANCELLED'; steps = $steps; pluginInstall = 'PASS'; clientConfiguration = 'NOT_STARTED'; httpToolDiscovery = 'NOT_RUN'; httpPing = 'NOT_RUN'; clientVerification = 'NOT_VERIFIED'; transactionRoot = $transaction.Root; ledgerPath = $transaction.Ledger; message = '已取消；插件事务保留，客户端事务尚未开始。' }
    }
    foreach ($client in $clients) {
        if (Test-Cancelled $cancel) { Update-RecoveryTransaction $transaction.Root 'CANCELLED' $true '' 'AVAILABLE' | Out-Null; $state.status = 'CANCELLED'; $state.client.status = 'NOT_STARTED'; Set-OneClickStep $state 'client-configuration' 'NOT_STARTED'; return [ordered]@{ status = 'CANCELLED'; steps = $steps; pluginInstall = 'PASS'; clientConfiguration = 'NOT_STARTED'; httpToolDiscovery = 'NOT_RUN'; httpPing = 'NOT_VERIFIED'; clientVerification = 'NOT_VERIFIED'; transactionRoot = $transaction.Root; ledgerPath = $transaction.Ledger; message = '已取消；插件事务保留，客户端事务尚未开始。' } }
        $check = Test-ClientPreflight $client $selection.projectRoot
        if ($check.status -ne 'PASS') { Update-RecoveryTransaction $transaction.Root 'FAIL' $true $check.errorCode 'AVAILABLE' | Out-Null; $state.status = 'FAILED'; $state.client.status = 'FAIL'; $state.failedClient = $client; Set-OneClickStep $state 'client-preflight' 'FAIL' $check.errorCode; return [ordered]@{ status = 'FAILED'; steps = $steps + [ordered]@{ name = 'client-preflight:' + $client; status = 'FAIL'; errorCode = $check.errorCode }; errorCode = $check.errorCode; pluginInstall = 'PASS'; clientConfiguration = 'NOT_STARTED'; clientVerification = 'NOT_VERIFIED' } }
        $apply = Invoke-ClientApply $client $selection.projectRoot $progress $false; $steps += [ordered]@{ name = 'client-configuration:' + $client; status = $apply.status; errorCode = $apply.errorCode }
        if ($apply.status -ne 'PASS') { Update-RecoveryTransaction $transaction.Root 'FAIL' $true $apply.errorCode 'AVAILABLE' | Out-Null; $state.status = 'FAILED'; $state.client.status = 'FAIL'; $state.failedClient = $client; Set-OneClickStep $state 'client-configuration' 'FAIL' $apply.errorCode; return [ordered]@{ status = 'FAILED'; steps = $steps; errorCode = $apply.errorCode; pluginInstall = 'PASS'; clientConfiguration = 'FAIL'; failedClient = $client; clientVerification = 'NOT_VERIFIED'; next = '请使用“恢复失败客户端”，不要用部署按钮重复写入。' } }
        $state.client.status = 'PASS'; Set-OneClickStep $state 'client-configuration' 'PASS'
    }
    if ($clients.Count -eq 0) { $state.client.status = 'NOT_SELECTED'; Set-OneClickStep $state 'client-configuration' 'NOT_SELECTED'; Send-Progress $progress '未选择 AI 客户端：按“仅安装插件”模式继续。' }
    $diagnosis = if ($NoWait) { Invoke-ConnectionDiagnosis $progress } else {
        $last = $null; $deadline = (Get-Date).AddMinutes(5)
        do {
            if (Test-Cancelled $cancel) { Update-RecoveryTransaction $transaction.Root 'CANCELLED' $true '' 'AVAILABLE' | Out-Null; $state.status = 'CANCELLED'; $state.diagnosis = [ordered]@{ status = 'CANCELLED'; httpToolDiscovery = 'NOT_VERIFIED'; httpPing = 'NOT_VERIFIED'; clientVerification = 'NOT_VERIFIED' }; Set-OneClickStep $state 'connection-diagnosis' 'CANCELLED'; return [ordered]@{ status = 'CANCELLED'; steps = $steps; pluginInstall = 'PASS'; clientConfiguration = if ($clients.Count -eq 0) { 'NOT_SELECTED' } else { 'PASS' }; httpToolDiscovery = 'NOT_VERIFIED'; httpPing = 'NOT_VERIFIED'; clientVerification = 'NOT_VERIFIED'; transactionRoot = $transaction.Root; ledgerPath = $transaction.Ledger; message = '已取消等待；没有强制结束任何用户程序。' } }
            $last = Invoke-ConnectionDiagnosis $progress; if ([string]$last.status -eq 'PASS') { break }; if ((Get-Date) -ge $deadline) { break }; Start-Sleep -Seconds 2
        } while ($true)
        $last
    }
    $state.diagnosis = $diagnosis
    $finalStatus = if ($diagnosis.status -eq 'PASS') { 'PASS_WITH_CLIENT_VERIFICATION_PENDING' } elseif ($diagnosis.status -eq 'WAITING_USER_START') { 'WAITING_USER_START' } else { [string]$diagnosis.status }
    Update-RecoveryTransaction $transaction.Root $finalStatus $true '' 'AVAILABLE' | Out-Null
    $state.status = $finalStatus; Set-OneClickStep $state 'connection-diagnosis' ([string]$diagnosis.status); $steps += [ordered]@{ name = 'connection-diagnosis'; status = $diagnosis.status }
    return [ordered]@{ status = $finalStatus; steps = $steps; pluginInstall = 'PASS'; clientConfiguration = if ($clients.Count -eq 0) { 'NOT_SELECTED' } else { 'PASS' }; httpToolDiscovery = $diagnosis.httpToolDiscovery; httpToolCount = $diagnosis.httpToolCount; httpPing = $diagnosis.httpPing; clientVerification = 'NOT_VERIFIED'; diagnosis = $diagnosis; transactionRoot = $transaction.Root; ledgerPath = $transaction.Ledger; next = '真实 Codex/Cursor/DeepSeek 客户端连接仍需用户分别验证。' }
}
function Invoke-ClientRestoreFlow([scriptblock]$progress) {
    $state = Read-OneClickState
    if ($null -eq $state -or $null -eq $state.client -or [string]::IsNullOrWhiteSpace([string]$state.client.id)) { return [ordered]@{ status = 'FAILED'; errorCode = 'CLIENT_RECOVERY_STATE_MISSING' } }
    $client = [string]$state.client.id
    if ($script:supportedClientIds -notcontains $client) { return [ordered]@{ status = 'FAILED'; errorCode = 'CLIENT_NOT_SUPPORTED' } }
    $selection = New-Selection @($client) ([string]$state.client.projectRoot) $false; Assert-TestModeScope $selection
    Send-Progress $progress ('正在恢复失败客户端：' + $client + '；只使用该客户端的备份元数据。')
    $args = @('-Action', 'ClientRestore', '-Client', $client, '-CatalogPath', $script:catalogPath, '-TemplateRoot', $script:bundleRootResolved, '-Json')
    if ($client -in @('codex', 'cursor')) { $args += @('-ConfigRoot', [string]$state.client.projectRoot) }
    $result = Invoke-ChildJson $script:workflowPath $args
    if ($result.ExitCode -ne 0 -or $null -eq $result.Json -or [string]$result.Json.status -ne 'PASS') {
        $code = Get-SafeErrorCode $result 'CLIENT_RESTORE_FAILED'; $state.status = 'CLIENT_RESTORE_FAILED'; Set-OneClickStep $state 'client-restore' 'FAIL' $code
        return [ordered]@{ status = 'FAILED'; errorCode = $code; client = $client; next = '请保留客户端备份和 sanitized operation log，人工检查后再决定是否恢复。' }
    }
    $state.client.status = 'RESTORED'; $state.failedClient = $null; if ([string]$state.plugin.status -eq 'PASS') { $state.status = 'CLIENT_RESTORED' }; Set-OneClickStep $state 'client-restore' 'PASS'
    return [ordered]@{ status = 'PASS'; action = 'ClientRestore'; client = $client; transactionRoot = [string]$state.plugin.transactionRoot; message = '失败客户端已按其 own backup metadata 恢复；插件 ledger 保留。' }
}
function Invoke-RecoveryFlow([string]$recovery, [scriptblock]$progress) {
    Assert-TestModeScope $null
    $oneClickState = $null
    try { $oneClickState = Read-OneClickState } catch { $oneClickState = $null }
    $state = Resolve-RecoveryTransaction
    Send-Progress $progress ('正在执行受同一 ledger 约束的 ' + $recovery + '……')
    $args = @('-Action', $recovery, '-PackagePath', $script:packagePath, '-ManifestPath', $script:manifestPath, '-InstallRoot', $script:installRootResolved, '-TransactionRoot', $state.TransactionRoot, '-LedgerPath', $state.LedgerPath, '-Json')
    if ($TestMode) { $args += '-TestMode' }
    $result = Invoke-ChildJson $script:workflowPath $args
    if ($result.ExitCode -ne 0 -or $null -eq $result.Json -or [string]$result.Json.status -ne 'PASS') { return [ordered]@{ status = 'FAILED'; errorCode = Get-SafeErrorCode $result ($recovery.ToUpperInvariant() + '_FAILED') } }
    Write-InstallerState $state.TransactionRoot $state.LedgerPath $recovery
    $indexState = 'COMPLETED'; $indexStatus = if ($recovery -eq 'Uninstall') { 'UNINSTALLED' } else { 'RECOVERED' }
    Update-RecoveryTransaction $state.TransactionRoot $indexStatus $false '' $indexState | Out-Null
    $stateUpdate = 'NOT_REQUIRED'
    if ($null -ne $oneClickState) {
        try {
            $oneClickState.plugin.status = if ($recovery -eq 'Uninstall') { 'UNINSTALLED' } else { 'RECOVERED' }
            $oneClickState.status = 'RECOVERED'; Set-OneClickStep $oneClickState ('plugin-' + $recovery.ToLowerInvariant()) 'PASS'; $stateUpdate = 'PASS'
        } catch { $stateUpdate = 'NOT_VERIFIED' }
    }
    return [ordered]@{ status = 'PASS'; action = $recovery; transactionRoot = $state.TransactionRoot; ledgerPath = $state.LedgerPath; recoverySource = $state.Source; recoveryIndexState = $indexState; oneClickStateUpdate = $stateUpdate; message = ($recovery + ' 已完成；ledger 和 recovery index 条目保留用于审计。') }
}
function Invoke-RetryFlow([scriptblock]$progress) {
    $state = Read-OneClickState
    if ($null -eq $state) { return [ordered]@{ status = 'FAILED'; errorCode = 'RETRY_STATE_NOT_FOUND' } }
    if ([string]$state.client.status -eq 'FAIL') { return [ordered]@{ status = 'FAILED'; errorCode = 'CLIENT_RECOVERY_REQUIRED'; next = '请先点击“恢复失败客户端”；Retry 不会重新写客户端。' } }
    if ([string]$state.plugin.status -ne 'PASS') { return [ordered]@{ status = 'FAILED'; errorCode = 'RETRY_REQUIRES_ROLLBACK'; next = '当前插件事务不是可诊断的已完成状态；请先恢复/卸载插件。' } }
    $diagnosisStatus = if ($state.diagnosis) { [string]$state.diagnosis.status } else { [string]$state.status }
    $diagnoseOnly = @('WAITING_USER_START', 'PORT_OPEN_NOT_MCP', 'TOOL_SET_MISMATCH', 'TOOL_DUPLICATE', 'TOOL_COUNT_MISMATCH', 'HTTP_PING_NOT_CONFIRMED', 'PASS_WITH_CLIENT_VERIFICATION_PENDING')
    if ($diagnoseOnly -notcontains $diagnosisStatus) { return [ordered]@{ status = 'FAILED'; errorCode = 'RETRY_NOT_AUTHORIZED'; next = 'Retry 只允许对等待/连接诊断状态执行；不重复安装或配置。' } }
    Send-Progress $progress 'Retry 模式：只重新执行 loopback MCP diagnosis，不创建新 transaction、不安装插件、不写客户端。'
    $diagnosis = Invoke-ConnectionDiagnosis $progress; $state.diagnosis = $diagnosis
    $state.status = if ($diagnosis.status -eq 'PASS') { 'PASS_WITH_CLIENT_VERIFICATION_PENDING' } else { [string]$diagnosis.status }
    Set-OneClickStep $state 'retry-diagnosis' ([string]$diagnosis.status)
    return [ordered]@{ status = $state.status; retryMode = 'DIAGNOSE_ONLY'; transactionRoot = [string]$state.plugin.transactionRoot; ledgerPath = [string]$state.plugin.ledgerPath; diagnosis = $diagnosis; httpToolDiscovery = $diagnosis.httpToolDiscovery; httpToolCount = $diagnosis.httpToolCount; httpPing = $diagnosis.httpPing; clientVerification = 'NOT_VERIFIED'; next = 'Retry 未改变安装事务或客户端配置。' }
}
function Invoke-GuiSmokeWorker([string]$scenario, [scriptblock]$progress, [scriptblock]$cancel) {
    Send-Progress $progress ('GUI smoke：' + $scenario)
    switch ($scenario) {
        'GuiSmokePreflight' { Start-Sleep -Milliseconds 80; Send-Progress $progress '预检层：PASS（仅 smoke test，未访问真实安装/客户端目标）。'; return [ordered]@{ status = 'PASS'; pluginInstall = 'NOT_RUN'; clientConfiguration = 'NOT_RUN'; httpToolDiscovery = 'NOT_RUN'; httpPing = 'NOT_RUN'; clientVerification = 'NOT_VERIFIED'; smokeScenario = 'preflight' } }
        'GuiSmokeWaiting' { Start-Sleep -Milliseconds 80; Send-Progress $progress '等待层：等待用户启动 ArcGIS Pro MCP Start。'; return [ordered]@{ status = 'WAITING_USER_START'; pluginInstall = 'PASS'; clientConfiguration = 'PASS'; httpToolDiscovery = 'NOT_VERIFIED'; httpPing = 'NOT_VERIFIED'; clientVerification = 'NOT_VERIFIED'; smokeScenario = 'waiting' } }
        'GuiSmokeCancel' {
            for ($i = 0; $i -lt 20; $i++) { if (Test-Cancelled $cancel) { return [ordered]@{ status = 'CANCELLED'; pluginInstall = 'PASS'; clientConfiguration = 'NOT_STARTED'; httpToolDiscovery = 'NOT_VERIFIED'; httpPing = 'NOT_VERIFIED'; clientVerification = 'NOT_VERIFIED'; smokeScenario = 'cancel' } }; Start-Sleep -Milliseconds 30 }
            return [ordered]@{ status = 'FAILED'; errorCode = 'GUI_SMOKE_CANCEL_NOT_RECEIVED'; smokeScenario = 'cancel' }
        }
        'GuiSmokeFailure' { Start-Sleep -Milliseconds 80; return [ordered]@{ status = 'FAILED'; errorCode = 'GUI_SMOKE_EXPECTED_FAILURE'; pluginInstall = 'PASS'; clientConfiguration = 'FAIL'; httpToolDiscovery = 'NOT_RUN'; httpPing = 'NOT_RUN'; clientVerification = 'NOT_VERIFIED'; smokeScenario = 'failure' } }
        'GuiSmokeCompletion' { Start-Sleep -Milliseconds 80; Send-Progress $progress '完成层：插件 PASS；客户端 PASS；HTTP 连接待用户确认。'; return [ordered]@{ status = 'PASS_WITH_CLIENT_VERIFICATION_PENDING'; pluginInstall = 'PASS'; clientConfiguration = 'PASS'; httpToolDiscovery = 'PASS'; httpPing = 'PASS'; clientVerification = 'NOT_VERIFIED'; smokeScenario = 'completion' } }
        default { return [ordered]@{ status = 'FAILED'; errorCode = 'GUI_SMOKE_SCENARIO_INVALID' } }
    }
}
function New-Selection([string[]]$clients, [string]$projectPath, [bool]$pluginOnly) {
    $requested = @($clients | ForEach-Object { ([string]$_ -split ',') } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if (-not $pluginOnly) {
        $unknown = @($requested | Where-Object { $script:supportedClientIds -notcontains $_ })
        if ($unknown.Count -gt 0) { throw 'CLIENT_NOT_SUPPORTED' }
        if ($requested.Count -gt 1) { throw 'MULTIPLE_CLIENTS_NOT_ALLOWED' }
    }
    return [pscustomobject]@{ clients = if ($pluginOnly) { @() } else { @($requested) }; projectRoot = [string]$projectPath; pluginOnly = $pluginOnly }
}
function Invoke-Plan {
    $clients = @('codex', 'cursor', 'deepseek-harness')
    $out = [ordered]@{ schema = 'arcgis-pro-mcp-one-click-plan-v1'; status = 'PASS'; defaultClient = 'codex'; supportedClients = $clients; pluginOnly = $true; endpoint = $script:endpoint; canonicalProductionToolCount = 239; steps = @('read-only preflight', 'bundle manifest and payload verification', 'one explicit add-in transaction', 'zero or more independent single-client transactions', 'HTTP tools/list and read-only ping diagnosis'); cleanMachineAcceptance = 'NOT VERIFIED'; clientVerification = 'NOT VERIFIED'; mutation = 'explicit user confirmation required'; noApplyAll = $true }
    if ($Json) { $out | ConvertTo-Json -Depth 10 } else { Write-Host 'One-click plan: PASS'; $out.steps | ForEach-Object { Write-Host ('- ' + $_) } }
    exit 0
}
function Invoke-ValidateBundle {
    $result = Test-Bundle $null
    $out = [ordered]@{ schema = 'arcgis-pro-mcp-one-click-cli-report-v1'; action = 'ValidateBundle'; status = $result.status; errorCode = $result.errorCode; report = $result.report }
    if ($Json) { $out | ConvertTo-Json -Depth 12 } else { Write-Host ('Bundle validation: ' + $result.status) }
    exit $(if ($result.status -eq 'PASS') { 0 } else { 1 })
}
function Invoke-DiagnoseCli {
    $result = Invoke-ConnectionDiagnosis $null
    $out = [ordered]@{ schema = 'arcgis-pro-mcp-one-click-cli-report-v1'; action = 'Diagnose'; status = $result.status; endpoint = $script:endpoint; portListening = $result.portListening; httpToolDiscovery = $result.httpToolDiscovery; httpToolCount = $result.httpToolCount; httpPing = $result.httpPing; clientVerification = 'NOT_VERIFIED'; message = $result.message }
    if ($Json) { $out | ConvertTo-Json -Depth 10 } else { Write-Host ('Diagnosis: ' + $result.status); Write-Host $result.message }
    exit 0
}
function Initialize-GuiWorkerBridge {
    if ($null -ne ('ArcGISProMcp.OneClickGuiWorker.Bridge' -as [type])) { return }
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ArcGISProMcp.OneClickGuiWorker
{
    public sealed class Payload
    {
        public string Arguments { get; set; }
        public string OutputPath { get; set; }
        public string ErrorPath { get; set; }
        public string MetaPath { get; set; }
    }

    public sealed class Result
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool Cancelled { get; set; }
    }

    public static class Bridge
    {
        public static void Attach(BackgroundWorker worker)
        {
            if (worker == null) { throw new ArgumentNullException("worker"); }
            worker.DoWork += Run;
        }

        private static void WriteArtifacts(Payload payload, string output, string error, int exitCode, bool cancelled)
        {
            if (payload == null) { return; }
            if (!String.IsNullOrWhiteSpace(payload.OutputPath)) { File.WriteAllText(payload.OutputPath, output ?? "", new UTF8Encoding(false)); }
            if (!String.IsNullOrWhiteSpace(payload.ErrorPath)) { File.WriteAllText(payload.ErrorPath, error ?? "", new UTF8Encoding(false)); }
            if (!String.IsNullOrWhiteSpace(payload.MetaPath)) { File.WriteAllText(payload.MetaPath, exitCode.ToString() + "|" + cancelled.ToString()); }
        }

        private static void Run(object sender, DoWorkEventArgs e)
        {
            var payload = e.Argument as Payload;
            if (payload == null || String.IsNullOrWhiteSpace(payload.Arguments))
            {
                WriteArtifacts(payload, "", "GUI_WORKER_ARGUMENTS_INVALID", 127, false);
                e.Result = new Result { ExitCode = 127, Output = "", Error = "GUI_WORKER_ARGUMENTS_INVALID", Cancelled = false };
                return;
            }

            var backgroundWorker = sender as BackgroundWorker;
            if (backgroundWorker != null && backgroundWorker.CancellationPending)
            {
                WriteArtifacts(payload, "", "GUI_WORKER_CANCELLED_BEFORE_START", 130, true);
                e.Result = new Result { ExitCode = 130, Output = "", Error = "GUI_WORKER_CANCELLED_BEFORE_START", Cancelled = true };
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = payload.Arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = new UTF8Encoding(false),
                    StandardErrorEncoding = new UTF8Encoding(false)
                };

                using (var process = Process.Start(startInfo))
                {
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    Task.WaitAll(outputTask, errorTask);
                    var cancelled = backgroundWorker != null && backgroundWorker.CancellationPending;
                    WriteArtifacts(payload, outputTask.Result ?? "", errorTask.Result ?? "", process.ExitCode, cancelled);
                    e.Result = new Result
                    {
                        ExitCode = process.ExitCode,
                        Output = outputTask.Result ?? "",
                        Error = errorTask.Result ?? "",
                        Cancelled = cancelled
                    };
                }
            }
            catch (Exception exception)
            {
                try { WriteArtifacts(payload, "", exception.ToString(), 127, false); } catch { }
                e.Result = new Result { ExitCode = 127, Output = "", Error = exception.ToString(), Cancelled = false };
            }
        }
    }
}
'@ -Language CSharp -ErrorAction Stop
}
function ConvertTo-PowerShellLiteral([string]$value) {
    if ($null -eq $value) { return "''" }
    return "'" + ([string]$value).Replace("'", "''") + "'"
}
function New-GuiWorkerArguments([string]$mode, [pscustomobject]$selection, [string]$scenario, [string]$cancelToken) {
    $command = @('& ' + (ConvertTo-PowerShellLiteral $script:entryScriptPath), '-Action GuiWorker', '-WorkerMode ' + (ConvertTo-PowerShellLiteral $mode), '-BundleRoot ' + (ConvertTo-PowerShellLiteral $script:bundleRootResolved), '-InstallRoot ' + (ConvertTo-PowerShellLiteral $script:installRootResolved), '-InstallerStateRoot ' + (ConvertTo-PowerShellLiteral $script:stateRootResolved), '-Json')
    if ($selection.pluginOnly) { $command += '-PluginOnly' }
    else {
        if (@($selection.clients).Count -eq 1) { $command += '-SelectedClients ' + (ConvertTo-PowerShellLiteral ([string](@($selection.clients)[0]))) }
        if (-not [string]::IsNullOrWhiteSpace([string]$selection.projectRoot)) { $command += '-ProjectRoot ' + (ConvertTo-PowerShellLiteral ([string]$selection.projectRoot)) }
    }
    if ($DryRun) { $command += '-DryRun' }
    if ($NoWait) { $command += '-NoWait' }
    if ($TestMode) { $command += '-TestMode' }
    if (-not [string]::IsNullOrWhiteSpace($scenario)) { $command += '-WorkerScenario ' + (ConvertTo-PowerShellLiteral $scenario) }
    if (-not [string]::IsNullOrWhiteSpace($cancelToken)) { $command += '-CancelTokenPath ' + (ConvertTo-PowerShellLiteral $cancelToken) }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes(($command -join ' ')))
    return '-NoLogo -NoProfile -ExecutionPolicy Bypass -STA -EncodedCommand ' + $encoded
}
function Write-GuiSmokeError([string]$outputPath, [string]$stage, [string]$errorCode, [string]$detail) {
    if ([string]::IsNullOrWhiteSpace($outputPath)) { return }
    $fullPath = Join-Path $outputPath 'gui-smoke-error.log'
    $safePath = Join-Path $outputPath 'gui-smoke-sanitized.log'
    [IO.File]::WriteAllText($fullPath, ('stage=' + $stage + [Environment]::NewLine + 'errorCode=' + $errorCode + [Environment]::NewLine + [string]$detail), [Text.UTF8Encoding]::new($false))
    $safeCode = if ([string]::IsNullOrWhiteSpace($errorCode)) { 'GUI_SMOKE_FAILURE' } else { ($errorCode -split '[\r\n|]')[0] }
    if ($safeCode -notmatch '^[A-Za-z0-9_\-]+$') { $safeCode = 'UNSAFE_ERROR_CODE' }
    $safeEntry = [ordered]@{ schema = 'arcgis-pro-mcp-gui-smoke-log-v1'; atUtc = [DateTime]::UtcNow.ToString('o'); stage = $stage; status = 'FAIL'; errorCode = $safeCode }
    [IO.File]::AppendAllText($safePath, (($safeEntry | ConvertTo-Json -Compress -Depth 5) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
}
function Read-GuiWorkerText([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) { return '' }
    return [IO.File]::ReadAllText($path, [Text.UTF8Encoding]::new($false))
}
function Write-GuiSmokeEvent([string]$outputPath, [string]$stage, [string]$status, [string]$errorCode = '') {
    if ([string]::IsNullOrWhiteSpace($outputPath)) { return }
    $safeStage = if ([string]::IsNullOrWhiteSpace($stage)) { 'unknown' } else { ($stage -split '[\r\n|]')[0] }
    if ($safeStage -notmatch '^[A-Za-z0-9_\-]+$') { $safeStage = 'unknown' }
    $safeStatus = if ([string]::IsNullOrWhiteSpace($status)) { 'UNKNOWN' } else { ($status -split '[\r\n|]')[0] }
    if ($safeStatus -notmatch '^[A-Za-z0-9_\-]+$') { $safeStatus = 'UNKNOWN' }
    $safeCode = if ([string]::IsNullOrWhiteSpace($errorCode)) { '' } else { ($errorCode -split '[\r\n|]')[0] }
    if ($safeCode -and $safeCode -notmatch '^[A-Za-z0-9_\-]+$') { $safeCode = 'UNSAFE_ERROR_CODE' }
    $entry = [ordered]@{ schema = 'arcgis-pro-mcp-gui-smoke-log-v1'; atUtc = [DateTime]::UtcNow.ToString('o'); stage = $safeStage; status = $safeStatus; errorCode = if ($safeCode) { $safeCode } else { $null } }
    [IO.File]::AppendAllText((Join-Path $outputPath 'gui-smoke-sanitized.log'), (($entry | ConvertTo-Json -Compress -Depth 5) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
}
function Show-SetupGui {
    $initialPluginOnly = [bool]$PluginOnly
    if ([string]::IsNullOrWhiteSpace([string]$script:installTarget)) { throw 'GUI_PLUGIN_TARGET_UNAVAILABLE' }
    $pluginTargetForUi = [IO.Path]::GetFullPath([string]$script:installTarget)
    $pluginInstallRootForUi = [IO.Path]::GetFullPath([string]$script:installRootResolved)
    $pluginAddInIdForUi = [string]$script:addInId
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [Windows.Forms.Application]::EnableVisualStyles()
    $form = New-Object Windows.Forms.Form; $form.Text = 'ArcGIS Pro MCP 一键部署'; $form.StartPosition = 'CenterScreen'; $form.ClientSize = New-Object Drawing.Size(940, 820); $form.MinimumSize = New-Object Drawing.Size(940, 820)
    $title = New-Object Windows.Forms.Label; $title.Text = 'ArcGIS Pro MCP 一键部署分享版'; $title.Font = New-Object Drawing.Font('Microsoft YaHei UI', 16, [Drawing.FontStyle]::Bold); $title.Location = New-Object Drawing.Point(22, 18); $title.AutoSize = $true; $form.Controls.Add($title)
    $sub = New-Object Windows.Forms.Label; $sub.Text = '固定服务：arcgis-pro-mcp  |  http://127.0.0.1:6520/mcp  |  canonical tools=239'; $sub.Location = New-Object Drawing.Point(24, 52); $sub.AutoSize = $true; $form.Controls.Add($sub)
    $scope = New-Object Windows.Forms.GroupBox; $scope.Text = '1. 选择本次操作范围（客户端只能单选）'; $scope.Location = New-Object Drawing.Point(20, 82); $scope.Size = New-Object Drawing.Size(900, 122); $form.Controls.Add($scope)
    $codex = New-Object Windows.Forms.RadioButton; $codex.Text = 'Codex（P0，推荐）'; $codex.Checked = $true; $codex.Location = New-Object Drawing.Point(20, 28); $codex.AutoSize = $true; $scope.Controls.Add($codex)
    $cursor = New-Object Windows.Forms.RadioButton; $cursor.Text = 'Cursor（P1）'; $cursor.Location = New-Object Drawing.Point(220, 28); $cursor.AutoSize = $true; $scope.Controls.Add($cursor)
    $deepseek = New-Object Windows.Forms.RadioButton; $deepseek.Text = 'DeepSeek Harness（P1）'; $deepseek.Location = New-Object Drawing.Point(380, 28); $deepseek.AutoSize = $true; $scope.Controls.Add($deepseek)
    $pluginOnly = New-Object Windows.Forms.CheckBox; $pluginOnly.Text = '仅安装插件（不写 AI 客户端配置）'; $pluginOnly.Location = New-Object Drawing.Point(620, 28); $pluginOnly.AutoSize = $true; $scope.Controls.Add($pluginOnly)
    $scopeNote = New-Object Windows.Forms.Label; $scopeNote.Text = '安装和每个客户端配置是独立事务；不会使用 ApplyAll。'; $scopeNote.Location = New-Object Drawing.Point(20, 65); $scopeNote.AutoSize = $true; $scope.Controls.Add($scopeNote)
    $projectLabel = New-Object Windows.Forms.Label; $projectLabel.Text = 'Codex/Cursor 项目目录：'; $projectLabel.Location = New-Object Drawing.Point(20, 225); $projectLabel.AutoSize = $true; $form.Controls.Add($projectLabel)
    $projectBox = New-Object Windows.Forms.TextBox; $projectBox.Location = New-Object Drawing.Point(175, 222); $projectBox.Size = New-Object Drawing.Size(610, 25); $projectBox.Text = $ProjectRoot; $form.Controls.Add($projectBox)
    $browse = New-Object Windows.Forms.Button; $browse.Text = '选择目录…'; $browse.Location = New-Object Drawing.Point(795, 220); $browse.Size = New-Object Drawing.Size(105, 28); $form.Controls.Add($browse)
    $targetLabel = New-Object Windows.Forms.Label; $targetLabel.BorderStyle = [Windows.Forms.BorderStyle]::FixedSingle; $targetLabel.Location = New-Object Drawing.Point(20, 260); $targetLabel.Size = New-Object Drawing.Size(880, 90); $targetLabel.AutoEllipsis = $false; $targetLabel.AutoSize = $false; $targetLabel.Font = New-Object Drawing.Font('Consolas', 8.25); $form.Controls.Add($targetLabel)
    $status = New-Object Windows.Forms.Label; $status.Text = '状态：尚未开始'; $status.Location = New-Object Drawing.Point(20, 360); $status.AutoSize = $true; $status.ForeColor = [Drawing.Color]::DarkBlue; $form.Controls.Add($status)
    $log = New-Object Windows.Forms.TextBox; $log.Multiline = $true; $log.ScrollBars = 'Vertical'; $log.ReadOnly = $true; $log.Location = New-Object Drawing.Point(20, 390); $log.Size = New-Object Drawing.Size(900, 185); $form.Controls.Add($log)
    $layers = New-Object Windows.Forms.TextBox; $layers.Multiline = $true; $layers.ReadOnly = $true; $layers.BackColor = [Drawing.Color]::WhiteSmoke; $layers.Location = New-Object Drawing.Point(20, 585); $layers.Size = New-Object Drawing.Size(900, 80); $form.Controls.Add($layers)
    $start = New-Object Windows.Forms.Button; $start.Text = '开始一键部署'; $start.Location = New-Object Drawing.Point(20, 680); $start.Size = New-Object Drawing.Size(125, 34); $form.Controls.Add($start)
    $preflight = New-Object Windows.Forms.Button; $preflight.Text = '只做预检'; $preflight.Location = New-Object Drawing.Point(155, 680); $preflight.Size = New-Object Drawing.Size(105, 34); $form.Controls.Add($preflight)
    $diagnose = New-Object Windows.Forms.Button; $diagnose.Text = '连接诊断'; $diagnose.Location = New-Object Drawing.Point(270, 680); $diagnose.Size = New-Object Drawing.Size(105, 34); $form.Controls.Add($diagnose)
    $retry = New-Object Windows.Forms.Button; $retry.Text = '连接重试（仅诊断）'; $retry.Location = New-Object Drawing.Point(385, 680); $retry.Size = New-Object Drawing.Size(145, 34); $retry.Enabled = $false; $form.Controls.Add($retry)
    $cancel = New-Object Windows.Forms.Button; $cancel.Text = '取消/停止等待'; $cancel.Location = New-Object Drawing.Point(540, 680); $cancel.Size = New-Object Drawing.Size(125, 34); $cancel.Enabled = $false; $form.Controls.Add($cancel)
    $rollback = New-Object Windows.Forms.Button; $rollback.Text = '恢复最近安装'; $rollback.Location = New-Object Drawing.Point(675, 680); $rollback.Size = New-Object Drawing.Size(115, 34); $form.Controls.Add($rollback)
    $uninstall = New-Object Windows.Forms.Button; $uninstall.Text = '卸载最近安装'; $uninstall.Location = New-Object Drawing.Point(795, 680); $uninstall.Size = New-Object Drawing.Size(120, 34); $form.Controls.Add($uninstall)
    $clientRestore = New-Object Windows.Forms.Button; $clientRestore.Text = '恢复失败客户端'; $clientRestore.Location = New-Object Drawing.Point(20, 725); $clientRestore.Size = New-Object Drawing.Size(135, 34); $form.Controls.Add($clientRestore)
    $updateSummary = {
        $selected = @(); if ($codex.Checked) { $selected += 'codex' }; if ($cursor.Checked) { $selected += 'cursor' }; if ($deepseek.Checked) { $selected += 'deepseek-harness' }
        $lines = @('插件目标：' + $pluginTargetForUi)
        if ($pluginOnly.Checked -or $selected.Count -eq 0) { $lines += '客户端：未选择（仅安装插件）' } else { foreach ($item in Get-ClientTargets $selected $projectBox.Text) { $lines += ($item.client + ' → ' + $item.target) } }
        $targetLabel.Text = ($lines -join [Environment]::NewLine)
    }.GetNewClosure()
    & $updateSummary
    $browse.Add_Click({ $dialog = New-Object Windows.Forms.FolderBrowserDialog; $dialog.Description = '选择 Codex/Cursor 项目目录'; if ($dialog.ShowDialog() -eq [Windows.Forms.DialogResult]::OK) { $projectBox.Text = $dialog.SelectedPath; & $updateSummary } }.GetNewClosure())
    foreach ($box in @($codex, $cursor, $deepseek)) { $box.Add_CheckedChanged({ if ($this.Checked) { $pluginOnly.Checked = $false }; & $updateSummary }.GetNewClosure()) }
    $pluginOnly.Add_CheckedChanged({ if ($pluginOnly.Checked) { $codex.Checked = $false; $cursor.Checked = $false; $deepseek.Checked = $false }; & $updateSummary }.GetNewClosure())
    $pluginOnly.Checked = $initialPluginOnly
    $getUiSelection = { $selected = @(); if ($codex.Checked) { $selected += 'codex' }; if ($cursor.Checked) { $selected += 'cursor' }; if ($deepseek.Checked) { $selected += 'deepseek-harness' }; return New-Selection $selected $projectBox.Text $pluginOnly.Checked }.GetNewClosure()
    $setUiBusy = { param([bool]$busy) foreach ($control in @($start, $preflight, $diagnose, $retry, $rollback, $uninstall, $clientRestore, $browse, $codex, $cursor, $deepseek, $pluginOnly, $projectBox)) { $control.Enabled = -not $busy }; $cancel.Enabled = $busy }.GetNewClosure()
    $appendUiLog = { param([string]$text) $log.AppendText(([DateTime]::Now.ToString('HH:mm:ss') + '  ' + $text + [Environment]::NewLine)) }.GetNewClosure()
    $updateUiLayers = { param($result)
        $pluginValue = if ($null -ne $result.pluginInstall) { [string]$result.pluginInstall } else { 'NOT_RUN' }
        $clientValue = if ($null -ne $result.clientConfiguration) { [string]$result.clientConfiguration } else { 'NOT_RUN' }
        $discoveryValue = if ($null -ne $result.httpToolDiscovery) { [string]$result.httpToolDiscovery } else { 'NOT_RUN' }
        $pingValue = if ($null -ne $result.httpPing) { [string]$result.httpPing } else { 'NOT_RUN' }
        $verifyValue = if ($null -ne $result.clientVerification) { [string]$result.clientVerification } else { 'NOT_VERIFIED' }
        $layers.Text = "插件事务：$pluginValue`r`n客户端事务：$clientValue`r`nHTTP tools/list：$discoveryValue    HTTP ping：$pingValue    客户端连接：$verifyValue"
    }.GetNewClosure()
    $smokeOutput = $null
    $form.Tag = @{ smoke = @{ nextIndex = 0; scenarios = @('GuiSmokePreflight', 'GuiSmokeWaiting', 'GuiSmokeCancel', 'GuiSmokeFailure', 'GuiSmokeCompletion'); active = ''; cancelIssued = $false; screenshots = @(); results = @(); started = $false; finished = $false; failure = '' }; buttonSmoke = @{ phase = 'initial'; screenshots = @(); results = @(); callbackSequence = @(); finished = $false; failure = ''; controlsRecovered = $false; correctTarget = $false }; ui = @{ worker = $null; start = $null; cancelToken = ''; mode = ''; outputPath = ''; errorPath = ''; metaPath = '' } }
    if ($GuiTestMode -or $GuiButtonSmokeMode) {
        if (-not $GuiSmokeOutput) { $GuiSmokeOutput = Join-Path ([IO.Path]::GetTempPath()) ('arcgis-pro-mcp-gui-smoke-' + [guid]::NewGuid().ToString('N')) }
        $smokeOutput = [IO.Path]::GetFullPath($GuiSmokeOutput)
        $tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        if (-not (Test-PathWithin $smokeOutput $tempBase) -or $smokeOutput.Equals($tempBase, [StringComparison]::OrdinalIgnoreCase)) { throw 'GUI_SMOKE_OUTPUT_PATH_REFUSED' }
        New-Item -ItemType Directory -Path $smokeOutput -Force | Out-Null
    }
    $captureSmoke = {
        param([string]$name)
        if (-not ($GuiTestMode -or $GuiButtonSmokeMode)) { return $null }
        [Windows.Forms.Application]::DoEvents()
        $path = Join-Path $smokeOutput ($name + '.png')
        $bitmap = New-Object Drawing.Bitmap($form.Width, $form.Height)
        try {
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try { $form.DrawToBitmap($bitmap, (New-Object Drawing.Rectangle(0, 0, $form.Width, $form.Height))) } finally { $graphics.Dispose() }
            $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
        } finally { $bitmap.Dispose() }
        if ($GuiButtonSmokeMode) { ($form.Tag)['buttonSmoke']['screenshots'] += $path } else { ($form.Tag)['smoke']['screenshots'] += $path }
        return $path
    }.GetNewClosure()
    $finishButtonSmoke = {
        param([string]$failure = '')
        $button = ($form.Tag)['buttonSmoke']
        if (-not [string]::IsNullOrWhiteSpace($failure) -and [string]::IsNullOrWhiteSpace([string]$button['failure'])) { $button['failure'] = $failure }
        $button['finished'] = $true
        $errorLogPath = Join-Path $smokeOutput 'gui-smoke-error.log'
        $controlsPass = (@($button['results']).Count -eq 2 -and @($button['results'] | Where-Object { -not [bool]$_.controlsRecovered }).Count -eq 0)
        $targetPass = (@($button['results']).Count -eq 2 -and @($button['results'] | Where-Object { -not [bool]$_.correctTarget }).Count -eq 0)
        $callbacksPass = (@($button['results']).Count -eq 2 -and @($button['results'] | Where-Object { -not [bool]$_.actualButtonCallback }).Count -eq 0)
        $exitPass = (@($button['results']).Count -eq 2 -and @($button['results'] | Where-Object { [int]$_.workerExitCode -ne 0 }).Count -eq 0)
        $reportPass = ([string]::IsNullOrWhiteSpace([string]$button['failure']) -and $controlsPass -and $targetPass -and $callbacksPass -and $exitPass -and -not (Test-Path -LiteralPath $errorLogPath -PathType Leaf))
        $button['controlsRecovered'] = $controlsPass
        $button['correctTarget'] = $targetPass
        $report = [ordered]@{
            schema = 'arcgis-pro-mcp-gui-button-smoke-v1'
            status = if ($reportPass) { 'PASS' } else { 'FAIL' }
            simulated = $false
            automation = 'REAL_BUTTON_CALLBACKS'
            readOnlyOperations = @('Preflight', 'Diagnose')
            actualButtonCallbacks = @('Preflight.PerformClick', 'Diagnose.PerformClick')
            callbackSequence = @($button['callbackSequence'])
            screenshots = @($button['screenshots'])
            results = @($button['results'])
            controlsRecovered = $controlsPass
            correctTarget = $targetPass
            noUnexpectedDialog = -not (Test-Path -LiteralPath $errorLogPath -PathType Leaf)
            workerException = -not [string]::IsNullOrWhiteSpace([string]$button['failure'])
            workerExitCodes = @($button['results'] | ForEach-Object { [int]$_.workerExitCode })
            processCompleted = $true
            realMutation = 'NOT_PERFORMED'
            encoding = 'UTF-8'
            smokeOutput = [IO.Path]::GetFullPath($smokeOutput)
            targetText = [string]$targetLabel.Text
            pluginTarget = $pluginTargetForUi
            pluginInstallRoot = $pluginInstallRootForUi
            addInId = $pluginAddInIdForUi
            uiLogText = [string]$log.Text
            failure = if ([string]::IsNullOrWhiteSpace([string]$button['failure'])) { $null } else { [string]$button['failure'] }
        }
        [IO.File]::WriteAllText((Join-Path $smokeOutput 'gui-button-smoke-report.json'), ($report | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
        $sequenceErrorCode = if ($report.status -eq 'PASS') { '' } else { 'GUI_BUTTON_SMOKE_SEQUENCE_FAIL' }; Write-GuiSmokeEvent $smokeOutput 'button-sequence' $report.status $sequenceErrorCode
        $form.Close()
    }.GetNewClosure()
    $completeUiWorker = {
        param([string]$completedMode)
        $context = $form.Tag
        $ui = $context['ui']; $smoke = $context['smoke']; $buttonSmoke = $context['buttonSmoke']
        try {
            $worker = $ui['worker']
            if ($null -eq $worker) { return }
            $outputPath = [string]$ui['outputPath']; $errorPath = [string]$ui['errorPath']; $metaPath = [string]$ui['metaPath']; $completedToken = [string]$ui['cancelToken']
            $childOutput = Read-GuiWorkerText $outputPath
            $childError = Read-GuiWorkerText $errorPath
            $exitCode = if (Test-Path -LiteralPath $metaPath -PathType Leaf) { [int]((Get-Content -LiteralPath $metaPath -Raw -Encoding UTF8) -split '\|')[0] } else { 127 }
            foreach ($artifact in @($outputPath, $errorPath, $metaPath)) { if (-not [string]::IsNullOrWhiteSpace($artifact) -and (Test-Path -LiteralPath $artifact -PathType Leaf)) { Remove-Item -LiteralPath $artifact -Force -ErrorAction SilentlyContinue } }
            if (-not [string]::IsNullOrWhiteSpace($completedToken) -and (Test-Path -LiteralPath $completedToken -PathType Leaf)) { Remove-Item -LiteralPath $completedToken -Force -ErrorAction SilentlyContinue }
            $ui['worker'] = $null; $ui['cancelToken'] = ''; $ui['mode'] = ''; $ui['outputPath'] = ''; $ui['errorPath'] = ''; $ui['metaPath'] = ''; $script:activeWorker = $null
            $null = $setUiBusy.Invoke($false)
            $result = $null; $workerFailure = $false
            if (-not [string]::IsNullOrWhiteSpace($childOutput)) {
                try { $result = $childOutput | ConvertFrom-Json } catch {
                    $workerFailure = $true; Write-GuiSmokeError $smokeOutput 'child-result-parse' 'GUI_WORKER_RESULT_INVALID' ('exitCode=' + [string]$exitCode + [Environment]::NewLine + 'stdout=' + $childOutput + [Environment]::NewLine + 'stderr=' + $childError + [Environment]::NewLine + 'parse=' + $_.Exception.ToString())
                }
            } else {
                $workerFailure = $true; Write-GuiSmokeError $smokeOutput 'child-result-empty' 'GUI_WORKER_RESULT_EMPTY' ('exitCode=' + [string]$exitCode + [Environment]::NewLine + 'stderr=' + $childError)
            }
            if ($null -eq $result -and $workerFailure) { $result = [ordered]@{ status = 'FAILED'; errorCode = 'GUI_WORKER_RESULT_INVALID'; message = '子进程没有返回可解析的 JSON；已保留错误堆栈。' } }
            if ($workerFailure -and $GuiTestMode) { $smoke['failure'] = 'GUI_WORKER_FAILURE' }
            if ($workerFailure -and $GuiButtonSmokeMode) { $buttonSmoke['failure'] = 'GUI_BUTTON_WORKER_FAILURE' }
            if ($GuiTestMode -and $completedMode -like 'GuiSmoke*') {
                $smokeEventErrorCode = ''
                if ($result.errorCode) { $smokeEventErrorCode = [string]$result.errorCode }
                Write-GuiSmokeEvent $smokeOutput ('complete-' + $completedMode) ([string]$result.status) $smokeEventErrorCode
            }
            if ($null -eq $result) { $status.Text = '状态：FAILED'; $status.ForeColor = [Drawing.Color]::DarkRed; $null = $appendUiLog.Invoke('FAILED：没有返回安全结果。'); $retry.Enabled = $true; return }
            $status.Text = '状态：' + [string]$result.status
            $status.ForeColor = if ([string]$result.status -match 'PASS') { [Drawing.Color]::DarkGreen } elseif ([string]$result.status -eq 'WAITING_USER_START') { [Drawing.Color]::DarkOrange } else { [Drawing.Color]::DarkRed }
            $null = $updateUiLayers.Invoke($result)
            if ($result.message) { $null = $appendUiLog.Invoke([string]$result.message) }
            if ($result.next) { $null = $appendUiLog.Invoke([string]$result.next) }
            if ($result.errorCode) { $null = $appendUiLog.Invoke('安全错误码：' + [string]$result.errorCode) }
            if ([string]$result.status -ne 'PASS' -and [string]$result.status -ne 'DRY_RUN_PASS') { $retry.Enabled = $true }
            if ([string]$result.status -eq 'WAITING_USER_START') { $null = $appendUiLog.Invoke('可点击“连接重试（仅诊断）”；不会重新安装或写客户端。'); $retry.Enabled = $true }
            if ($GuiButtonSmokeMode -and $completedMode -in @('Preflight', 'Diagnose')) {
                $callbackName = if ($completedMode -eq 'Preflight') { 'Preflight.PerformClick' } else { 'Diagnose.PerformClick' }
                $controlsRecovered = ($start.Enabled -and $preflight.Enabled -and $diagnose.Enabled -and $rollback.Enabled -and $uninstall.Enabled -and $clientRestore.Enabled -and $browse.Enabled -and $codex.Enabled -and $cursor.Enabled -and $deepseek.Enabled -and $pluginOnly.Enabled -and $projectBox.Enabled -and -not $cancel.Enabled)
                $expectedTarget = Get-ClientTarget 'codex' $projectBox.Text
                $targetText = [string]$targetLabel.Text
                $targetIsCorrect = $targetText.Contains($pluginTargetForUi) -and $targetText.Contains($expectedTarget)
                $diagnoseAllowed = @('WAITING_USER_START', 'PASS', 'PORT_OPEN_NOT_MCP', 'TOOL_SET_MISMATCH', 'TOOL_DUPLICATE', 'TOOL_COUNT_MISMATCH', 'HTTP_PING_NOT_CONFIRMED') -contains [string]$result.status
                $callbackPass = ($completedMode -eq 'Preflight' -and [string]$result.status -eq 'PASS') -or ($completedMode -eq 'Diagnose' -and $diagnoseAllowed)
                $null = $captureSmoke.Invoke($(if ($completedMode -eq 'Preflight') { '02-preflight-button' } else { '03-diagnose-button' }))
                $buttonSmoke['results'] += ,([ordered]@{ callback = $callbackName; actualButtonCallback = $true; mode = $completedMode; status = [string]$result.status; message = if ($result.message) { [string]$result.message } else { $null }; next = if ($result.next) { [string]$result.next } else { $null }; errorCode = if ($result.errorCode) { [string]$result.errorCode } else { $null }; workerExitCode = [int]$exitCode; callbackPass = $callbackPass; controlsRecovered = $controlsRecovered; correctTarget = $targetIsCorrect })
                $callbackErrorCode = if ($callbackPass) { '' } else { 'GUI_BUTTON_CALLBACK_RESULT_FAIL' }; Write-GuiSmokeEvent $smokeOutput ('complete-button-' + $completedMode.ToLowerInvariant()) ([string]$result.status) $callbackErrorCode
                if (-not $callbackPass -or -not $controlsRecovered -or -not $targetIsCorrect -or $workerFailure) {
                    $failureCode = if ($workerFailure) { 'GUI_BUTTON_WORKER_FAILURE' } elseif (-not $controlsRecovered) { 'GUI_BUTTON_CONTROLS_NOT_RECOVERED' } elseif (-not $targetIsCorrect) { 'GUI_BUTTON_TARGET_MISMATCH' } else { 'GUI_BUTTON_CALLBACK_RESULT_FAIL' }
                    $null = $finishButtonSmoke.Invoke($failureCode)
                } elseif ($completedMode -eq 'Preflight') {
                    $buttonSmoke['phase'] = 'diagnose-ready'
                } else {
                    $buttonSmoke['phase'] = 'finished'
                    $null = $finishButtonSmoke.Invoke('')
                }
                return
            }
            if ($GuiTestMode -and $completedMode -like 'GuiSmoke*') {
                $smokeName = switch ($completedMode) { 'GuiSmokePreflight' { '02-preflight' }; 'GuiSmokeWaiting' { '03-waiting' }; 'GuiSmokeCancel' { '04-cancelled' }; 'GuiSmokeFailure' { '05-failure' }; 'GuiSmokeCompletion' { '06-completed' } }
                if ($smokeName) { $null = $captureSmoke.Invoke($smokeName) }
                $smoke['results'] += ,([ordered]@{ scenario = $completedMode; status = [string]$result.status; errorCode = if ($result.errorCode) { [string]$result.errorCode } else { $null } })
                if ($completedMode -eq 'GuiSmokeCompletion') {
                    $expected = @('PASS', 'WAITING_USER_START', 'CANCELLED', 'FAILED', 'PASS_WITH_CLIENT_VERIFICATION_PENDING')
                    $actual = @($smoke['results'] | ForEach-Object { [string]$_.status }); $diff = @(Compare-Object -ReferenceObject $expected -DifferenceObject $actual -SyncWindow 0)
                    $sequencePass = ($smoke['results'].Count -eq 5 -and $smoke['screenshots'].Count -eq 6 -and $smoke['failure'] -eq '' -and $diff.Count -eq 0)
                    $smoke['finished'] = $true
                    $report = [ordered]@{ schema = 'arcgis-pro-mcp-gui-smoke-v1'; status = if ($sequencePass) { 'PASS' } else { 'FAIL' }; simulated = $true; automation = 'PRESET_WORKER_SCENARIOS'; actualButtonCallbacks = @(); businessCallbacks = 'NOT_VERIFIED'; screenshots = @($smoke['screenshots']); results = @($smoke['results']); workerException = ($smoke['failure'] -ne ''); serial = $true; realMutation = 'NOT_PERFORMED'; failure = if ($smoke['failure']) { $smoke['failure'] } else { $null } }
                    [IO.File]::WriteAllText((Join-Path $smokeOutput 'gui-smoke-report.json'), ($report | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
                    $sequenceStatus = 'FAIL'; $sequenceErrorCode = 'GUI_SMOKE_SEQUENCE_FAIL'
                    if ($sequencePass) { $sequenceStatus = 'PASS'; $sequenceErrorCode = '' }
                    Write-GuiSmokeEvent $smokeOutput 'sequence' $sequenceStatus $sequenceErrorCode
                    if (-not $sequencePass -and -not (Test-Path -LiteralPath (Join-Path $smokeOutput 'gui-smoke-error.log') -PathType Leaf)) { Write-GuiSmokeError $smokeOutput 'smoke-sequence' 'GUI_SMOKE_SEQUENCE_FAIL' ('results=' + ($actual -join ',') + ';screenshots=' + [string]$smoke['screenshots'].Count) }
                    $form.Close()
                }
            }
        } catch {
            $ui['worker'] = $null; $ui['cancelToken'] = ''; $script:activeWorker = $null; $null = $setUiBusy.Invoke($false); $smoke['failure'] = 'GUI_WORKER_POLLER_EXCEPTION'
            Write-GuiSmokeError $smokeOutput 'worker-poller' $smoke['failure'] $_.Exception.ToString(); $status.Text = '状态：FAILED'; $status.ForeColor = [Drawing.Color]::DarkRed; $null = $appendUiLog.Invoke('FAILED：worker 结果轮询异常，已保留完整错误堆栈。')
            if ($GuiTestMode) { $smoke['finished'] = $true; $failureReport = [ordered]@{ schema = 'arcgis-pro-mcp-gui-smoke-v1'; status = 'FAIL'; simulated = $true; screenshots = @($smoke['screenshots']); results = @($smoke['results']); workerException = $true; serial = $true; realMutation = 'NOT_PERFORMED'; failure = $smoke['failure'] }; [IO.File]::WriteAllText((Join-Path $smokeOutput 'gui-smoke-report.json'), ($failureReport | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false)); $form.Close() }
            if ($GuiButtonSmokeMode) { $null = $finishButtonSmoke.Invoke('GUI_BUTTON_POLLER_EXCEPTION') }
        }
    }.GetNewClosure()
    $startUiWorker = {
        param([string]$mode, [pscustomobject]$selection)
        if ($null -ne ($form.Tag)['ui']['worker'] -and ($form.Tag)['ui']['worker'].IsBusy) { return }
        if ($mode -in @('Deploy', 'Preflight') -and @($selection.clients).Count -gt 0 -and @($selection.clients)[0] -in @('codex', 'cursor') -and [string]::IsNullOrWhiteSpace($selection.projectRoot)) { [Windows.Forms.MessageBox]::Show($form, '请选择 Codex/Cursor 项目目录。', '需要目录', 'OK', 'Warning'); return }
        if ($mode -eq 'Deploy' -and -not ($GuiTestMode -or $GuiButtonSmokeMode)) {
            $confirmText = "将按顺序执行：预检、包完整性校验、插件安装、所选客户端独立配置、连接诊断。`r`n`r`n" + ($targetLabel.Text) + "`r`n`r`n不会强制关闭程序，不会修改 GIS 数据；真实 AI 客户端验证仍需另外执行。`r`n是否继续？"
            if ([Windows.Forms.MessageBox]::Show($form, $confirmText, '确认本次部署', 'YesNo', 'Warning') -ne [Windows.Forms.DialogResult]::Yes) { $null = $appendUiLog.Invoke('用户取消，本次没有开始写入。'); return }
        }
        $cancelToken = Join-Path ([IO.Path]::GetTempPath()) ('arcgis-pro-mcp-gui-cancel-' + [guid]::NewGuid().ToString('N') + '.flag')
        $outputPath = Join-Path ([IO.Path]::GetTempPath()) ('arcgis-pro-mcp-gui-worker-' + [guid]::NewGuid().ToString('N') + '.stdout')
        $errorPath = Join-Path ([IO.Path]::GetTempPath()) ('arcgis-pro-mcp-gui-worker-' + [guid]::NewGuid().ToString('N') + '.stderr')
        $metaPath = Join-Path ([IO.Path]::GetTempPath()) ('arcgis-pro-mcp-gui-worker-' + [guid]::NewGuid().ToString('N') + '.meta')
        ($form.Tag)['ui']['cancelToken'] = $cancelToken; ($form.Tag)['ui']['outputPath'] = $outputPath; ($form.Tag)['ui']['errorPath'] = $errorPath; ($form.Tag)['ui']['metaPath'] = $metaPath
        $worker = $null
        try {
            $null = $setUiBusy.Invoke($true); $status.Text = '状态：执行中'; $status.ForeColor = [Drawing.Color]::DarkBlue; $null = $appendUiLog.Invoke(('开始 ' + $mode + '。'))
            if ($GuiTestMode -and $mode -like 'GuiSmoke*') { Write-GuiSmokeEvent $smokeOutput ('start-' + $mode) 'STARTED' }
            if ($GuiButtonSmokeMode -and $mode -in @('Preflight', 'Diagnose')) { Write-GuiSmokeEvent $smokeOutput ('button-start-' + $mode.ToLowerInvariant()) 'STARTED' }
            Initialize-GuiWorkerBridge
            $scenario = if ($mode -like 'GuiSmoke*') { $mode } else { '' }
            $workerArgs = New-GuiWorkerArguments $mode $selection $scenario $cancelToken
            $payload = New-Object ArcGISProMcp.OneClickGuiWorker.Payload
            $payload.Arguments = $workerArgs; $payload.OutputPath = $outputPath; $payload.ErrorPath = $errorPath; $payload.MetaPath = $metaPath
            $worker = New-Object ComponentModel.BackgroundWorker
            $worker.WorkerSupportsCancellation = $true
            $worker.WorkerReportsProgress = $false
            [ArcGISProMcp.OneClickGuiWorker.Bridge]::Attach($worker)
            ($form.Tag)['ui']['worker'] = $worker; ($form.Tag)['ui']['mode'] = $mode; $script:activeWorker = $worker; $null = $worker.RunWorkerAsync($payload)
        } catch {
            ($form.Tag)['ui']['worker'] = $null; $script:activeWorker = $null; $null = $setUiBusy.Invoke($false); $status.Text = '状态：FAILED'; $status.ForeColor = [Drawing.Color]::DarkRed
            $code = 'GUI_WORKER_START_FAILED'; Write-GuiSmokeError $smokeOutput 'worker-start' $code $_.Exception.ToString(); $null = $appendUiLog.Invoke('FAILED：GUI worker 启动失败，已保留完整错误堆栈。')
            if ($GuiTestMode) { ($form.Tag)['smoke']['failure'] = $code }
            if ($GuiButtonSmokeMode) { ($form.Tag)['buttonSmoke']['failure'] = 'GUI_BUTTON_WORKER_START_FAILED'; $null = $finishButtonSmoke.Invoke('GUI_BUTTON_WORKER_START_FAILED') }
        }
    }.GetNewClosure()
    $form.Tag['ui']['start'] = $startUiWorker
    $start.Add_Click({ $script:lastSelection = $getUiSelection.Invoke(); $null = $form.Tag['ui']['start'].Invoke('Deploy', $script:lastSelection) }.GetNewClosure())
    $preflight.Add_Click({ $script:lastSelection = $getUiSelection.Invoke(); $null = $form.Tag['ui']['start'].Invoke('Preflight', $script:lastSelection) }.GetNewClosure())
    $diagnose.Add_Click({ $null = $form.Tag['ui']['start'].Invoke('Diagnose', (New-Selection @() $projectBox.Text $true)) }.GetNewClosure())
    $retry.Add_Click({ $null = $form.Tag['ui']['start'].Invoke('Retry', (New-Selection @() $projectBox.Text $true)) }.GetNewClosure())
    $cancel.Add_Click({ if ($null -ne $form.Tag['ui']['worker'] -and $form.Tag['ui']['worker'].IsBusy) { $null = $appendUiLog.Invoke('正在请求停止；不会强制终止子进程或用户程序。'); if (-not [string]::IsNullOrWhiteSpace($form.Tag['ui']['cancelToken'])) { [IO.File]::WriteAllText($form.Tag['ui']['cancelToken'], 'cancel', [Text.UTF8Encoding]::new($false)) }; $form.Tag['ui']['worker'].CancelAsync() } }.GetNewClosure())
    $rollback.Add_Click({ if ([Windows.Forms.MessageBox]::Show($form, '将按最近一次成功安装 ledger 执行回滚。是否继续？', '确认恢复', 'YesNo', 'Warning') -eq [Windows.Forms.DialogResult]::Yes) { $null = $form.Tag['ui']['start'].Invoke('Rollback', (New-Selection @() $projectBox.Text $true)) } }.GetNewClosure())
    $uninstall.Add_Click({ if ([Windows.Forms.MessageBox]::Show($form, '将按最近一次成功安装 ledger 卸载插件。是否继续？', '确认卸载', 'YesNo', 'Warning') -eq [Windows.Forms.DialogResult]::Yes) { $null = $form.Tag['ui']['start'].Invoke('Uninstall', (New-Selection @() $projectBox.Text $true)) } }.GetNewClosure())
    $clientRestore.Add_Click({ if ([Windows.Forms.MessageBox]::Show($form, '将只恢复最近一次失败客户端的备份；不会重新安装插件。是否继续？', '确认恢复客户端', 'YesNo', 'Warning') -eq [Windows.Forms.DialogResult]::Yes) { $null = $form.Tag['ui']['start'].Invoke('ClientRestore', (New-Selection @() $projectBox.Text $true)) } }.GetNewClosure())
    $form.Add_FormClosing({
        param($sender, $eventArgs)
        $context = $sender.Tag
        if ($null -eq $context -or -not $context.ContainsKey('ui')) { return }
        $ui = $context['ui']
        if ($null -eq $ui) { return }
        $runningWorker = $ui['worker']
        if ($null -ne $runningWorker -and $runningWorker.IsBusy) {
            $token = [string]$ui['cancelToken']
            if (-not [string]::IsNullOrWhiteSpace($token)) { [IO.File]::WriteAllText($token, 'cancel', [Text.UTF8Encoding]::new($false)) }
            $runningWorker.CancelAsync(); $eventArgs.Cancel = $true; $null = $appendUiLog.Invoke('请等待当前步骤安全停止后再关闭窗口。')
        }
    }.GetNewClosure())
    if ($GuiTestMode -or $GuiButtonSmokeMode) {
        $smokeTimer = New-Object Windows.Forms.Timer; $smokeTimer.Interval = 100
        $form.Add_Shown({ try { $null = $captureSmoke.Invoke('01-initial'); if ($GuiButtonSmokeMode) { Write-GuiSmokeEvent $smokeOutput 'button-initial' 'CAPTURED' }; $smokeTimer.Start() } catch { $form.Tag['smoke']['failure'] = 'GUI_SMOKE_INITIAL_CAPTURE_FAILED'; Write-GuiSmokeError $smokeOutput 'initial-capture' $form.Tag['smoke']['failure'] $_.Exception.ToString(); if ($GuiButtonSmokeMode) { $null = $finishButtonSmoke.Invoke('GUI_BUTTON_INITIAL_CAPTURE_FAILED') } else { $form.Tag['smoke']['finished'] = $true; $status.Text = '状态：FAILED'; $status.ForeColor = [Drawing.Color]::DarkRed; $form.Close() } } }.GetNewClosure())
        $smokeTimer.add_Tick({
            try {
                $context = $form.Tag; $ui = $context['ui']; $smoke = $context['smoke']; $runningWorker = $ui['worker']
                if ($null -ne $runningWorker) {
                    if ($runningWorker.IsBusy) {
                        if ($smoke['active'] -eq 'GuiSmokeCancel' -and -not $smoke['cancelIssued']) { $smoke['cancelIssued'] = $true; if (-not [string]::IsNullOrWhiteSpace($ui['cancelToken'])) { [IO.File]::WriteAllText($ui['cancelToken'], 'cancel', [Text.UTF8Encoding]::new($false)) }; $runningWorker.CancelAsync() }
                        return
                    }
                    $completedMode = [string]$ui['mode']; $null = $completeUiWorker.Invoke($completedMode); return
                }
                if ($smoke['finished']) { $smokeTimer.Stop(); return }
                if ($GuiButtonSmokeMode) {
                    $buttonSmoke = $context['buttonSmoke']
                    if ($buttonSmoke['finished']) { $smokeTimer.Stop(); return }
                    if ($buttonSmoke['phase'] -eq 'initial') {
                        $buttonSmoke['phase'] = 'preflight-started'; $buttonSmoke['callbackSequence'] += ,'Preflight.PerformClick'; Write-GuiSmokeEvent $smokeOutput 'button-preflight-click' 'INVOKED'; $null = $preflight.PerformClick(); return
                    }
                    if ($buttonSmoke['phase'] -eq 'diagnose-ready') {
                        $buttonSmoke['phase'] = 'diagnose-started'; $buttonSmoke['callbackSequence'] += ,'Diagnose.PerformClick'; Write-GuiSmokeEvent $smokeOutput 'button-diagnose-click' 'INVOKED'; $null = $diagnose.PerformClick(); return
                    }
                    return
                }
                if ($smoke['nextIndex'] -ge $smoke['scenarios'].Count) { return }
                $scenario = [string]$smoke['scenarios'][$smoke['nextIndex']]; $smoke['nextIndex']++; $smoke['active'] = $scenario; $smoke['cancelIssued'] = $false
                $null = $ui['start'].Invoke($scenario, (New-Selection @() '' $true))
                if (-not [string]::IsNullOrWhiteSpace([string]$smoke['failure'])) {
                    $smokeTimer.Stop(); $smoke['finished'] = $true
                    $failureReport = [ordered]@{ schema = 'arcgis-pro-mcp-gui-smoke-v1'; status = 'FAIL'; screenshots = @($smoke['screenshots']); results = @($smoke['results']); workerException = $true; serial = $true; realMutation = 'NOT_PERFORMED'; failure = [string]$smoke['failure'] }
                    [IO.File]::WriteAllText((Join-Path $smokeOutput 'gui-smoke-report.json'), ($failureReport | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
                    $form.Close()
                }
            } catch {
                $form.Tag['smoke']['failure'] = 'GUI_SMOKE_TIMER_EXCEPTION'; Write-GuiSmokeError $smokeOutput 'timer' $form.Tag['smoke']['failure'] $_.Exception.ToString(); $smokeTimer.Stop(); $status.Text = '状态：FAILED'; $status.ForeColor = [Drawing.Color]::DarkRed; $null = $appendUiLog.Invoke('FAILED：GUI smoke 调度异常，已保留完整错误堆栈。'); if ($GuiButtonSmokeMode) { $null = $finishButtonSmoke.Invoke('GUI_BUTTON_TIMER_EXCEPTION') } else { $form.Tag['smoke']['finished'] = $true; $form.Close() }
            }
        }.GetNewClosure())
    }
    [void]$form.ShowDialog()
    if (($GuiTestMode -or $GuiButtonSmokeMode) -and (($form.Tag['smoke']['finished']) -or ($form.Tag['buttonSmoke']['finished']))) {
        $reportName = if ($GuiButtonSmokeMode) { 'gui-button-smoke-report.json' } else { 'gui-smoke-report.json' }
        $script:guiSmokeReport = Get-Content -LiteralPath (Join-Path $smokeOutput $reportName) -Raw -Encoding UTF8 | ConvertFrom-Json
        return $script:guiSmokeReport
    }
}

try {
    switch ($Action) {
        'GUI' { $guiResult = Show-SetupGui; if ($Json -and $null -ne $guiResult) { $guiResult | ConvertTo-Json -Depth 12 } }
        'GuiWorker' {
            $selection = New-Selection $SelectedClients $ProjectRoot $PluginOnly
            $cancelChecker = {
                if ([string]::IsNullOrWhiteSpace($CancelTokenPath)) { return $false }
                return Test-Path -LiteralPath $CancelTokenPath -PathType Leaf
            }
            if (-not [string]::IsNullOrWhiteSpace($WorkerScenario)) {
                $result = Invoke-GuiSmokeWorker $WorkerScenario $null $cancelChecker
            } else {
                switch ($WorkerMode) {
                    'Deploy' { $result = Invoke-DeploymentFlow $selection $null $cancelChecker }
                    'Preflight' { $result = Invoke-ReadOnlyPreflight $selection $null $cancelChecker }
                    'Diagnose' { $result = Invoke-ConnectionDiagnosis $null }
                    'Retry' { $result = Invoke-RetryFlow $null }
                    'ClientRestore' { $result = Invoke-ClientRestoreFlow $null }
                    'Rollback' { $result = Invoke-RecoveryFlow 'Rollback' $null }
                    'Uninstall' { $result = Invoke-RecoveryFlow 'Uninstall' $null }
                    default { throw 'GUI_WORKER_MODE_INVALID' }
                }
            }
            if ($Json) { $result | ConvertTo-Json -Depth 14 }
            $workerOk = @('PASS', 'PASS_WITH_CLIENT_VERIFICATION_PENDING', 'WAITING_USER_START', 'DRY_RUN_PASS', 'CANCELLED') -contains [string]$result.status
            exit $(if ($workerOk) { 0 } else { 1 })
        }
        'Plan' { Invoke-Plan }
        'Preflight' {
            $selection = New-Selection $SelectedClients $ProjectRoot $PluginOnly
            $result = Invoke-ReadOnlyPreflight $selection $null $null
            if ($Json) { $result | ConvertTo-Json -Depth 12 } else { Write-Host ('Preflight: ' + $result.status) }
            exit $(if ($result.status -eq 'PASS') { 0 } else { 1 })
        }
        'Deploy' {
            $selection = New-Selection $SelectedClients $ProjectRoot $PluginOnly
            $result = Invoke-DeploymentFlow $selection $null $null
            if ($Json) { $result | ConvertTo-Json -Depth 12 } else { Write-Host ('Deploy: ' + $result.status) }
            exit $(if ([string]$result.status -match 'PASS|WAITING_USER_START') { 0 } else { 1 })
        }
        'Retry' {
            $result = Invoke-RetryFlow $null
            if ($Json) { $result | ConvertTo-Json -Depth 12 } else { Write-Host ('Retry: ' + $result.status) }
            exit $(if ([string]$result.status -match 'PASS|WAITING_USER_START') { 0 } else { 1 })
        }
        'ValidateBundle' { Invoke-ValidateBundle }
        'Diagnose' { Invoke-DiagnoseCli }
        'Recover' {
            if ($RecoveryAction -eq 'ClientRestore') { $result = Invoke-ClientRestoreFlow $null }
            else { $result = Invoke-RecoveryFlow $RecoveryAction $null }
            if ($Json) { $result | ConvertTo-Json -Depth 10 } else { Write-Host ('Recovery: ' + $result.status) }
            exit $(if ($result.status -eq 'PASS') { 0 } else { 1 })
        }
    }
} catch {
    $code = ([string]$_.Exception.Message -split '\|')[0]
    if ($Json) { [ordered]@{ schema = 'arcgis-pro-mcp-one-click-cli-report-v1'; status = 'FAIL'; errorCode = $code } | ConvertTo-Json -Depth 6 } else { Write-Error ($code + ': operation failed') }
    exit 1
}
