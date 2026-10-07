# ArcGIS Pro MCP - end-user launcher for the shareable binary bundle.
# Installation and AI-client configuration remain separate explicit actions.
[CmdletBinding()]
param(
    [ValidateSet('Menu', 'Check', 'Install', 'Uninstall', 'Rollback', 'ConfigureClient', 'RestoreClient')]
    [string]$Action = 'Menu',
    [ValidateSet('', 'codex', 'cursor', 'deepseek-harness')]
    [string]$Client = '',
    [string]$ProjectRoot = '',
    [switch]$DryRun,
    [switch]$Yes,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$bundleRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packagePath = Join-Path $bundleRoot 'payload\ArcGISProMCP.Compatibility.esriAddInX'
$manifestPath = Join-Path $bundleRoot 'payload\ArcGISProMCP.Compatibility.release-manifest.json'
$workflowPath = Join-Path $PSScriptRoot 'user-workflow.ps1'
$compatibilityPath = Join-Path $PSScriptRoot 'check-compatibility.ps1'
$catalogPath = Join-Path $bundleRoot 'Config\client-catalog.json'
$policyPath = Join-Path $bundleRoot 'Config\compatibility-policy.json'
# D-113 (R-G244-P1, mirrors the accepted D-111 fix): the Config.daml fed to the compatibility gate is no longer
# pinned to the outer Source\...\Config.daml (net6 metadata only, desktopVersion 3.0). Get-PayloadConfigDaml below
# reads it from the payload that will actually be installed, so the gate cannot reject its own package (3.0 != 3.5.0).
$installRoot = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'ArcGIS\AddIns\ArcGISPro'))
$addInId = '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'
$installTarget = Join-Path $installRoot $addInId
$stateRoot = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ArcGISProMCP\installer'))
$statePath = Join-Path $stateRoot 'latest-transaction.json'

function Write-Result([System.Collections.IDictionary]$result) {
    if ($Json) { Write-Output ($result | ConvertTo-Json -Depth 12); return }
    Write-Host ''
    $color = if ($result.status -eq 'PASS') { 'Green' } else { 'Yellow' }
    Write-Host ('Result: ' + [string]$result.status) -ForegroundColor $color
    if ($result.Contains('message')) { Write-Host ([string]$result.message) }
    if ($result.Contains('nextStep')) { Write-Host ('Next: ' + [string]$result.nextStep) -ForegroundColor Cyan }
}

function Get-FileSha256([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }

function Get-PayloadConfigDaml {
    # Same-source Config.daml, extracted from the payload that Read-BundleIdentity just verified byte-for-byte.
    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) { throw 'COMPATIBILITY_PAYLOAD_NOT_FOUND' }
    $scratchRoot = Join-Path ([IO.Path]::GetTempPath()) ('arcgis-pro-mcp-share-compat-config-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratchRoot -Force | Out-Null
    $targetPath = Join-Path $scratchRoot 'Config.daml'
    $zip = $null
    try {
        $zip = [IO.Compression.ZipFile]::OpenRead($packagePath)
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
    $versionMatch = [regex]::Match([IO.File]::ReadAllText($targetPath, [Text.Encoding]::UTF8), 'desktopVersion="([^"]+)"')
    if ($versionMatch.Success) { $desktopVersion = $versionMatch.Groups[1].Value }
    return [pscustomobject]@{ Path = $targetPath; ScratchRoot = $scratchRoot; Source = [ordered]@{
        payload = $packagePath; configDamlOrigin = 'payload entry Config.daml (same-source)'; desktopVersion = $desktopVersion } }
}

function Read-BundleIdentity {
    foreach ($required in @($packagePath, $manifestPath, $workflowPath, $compatibilityPath, $catalogPath, $policyPath)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw ('BUNDLE_FILE_MISSING: ' + [IO.Path]::GetFileName($required)) }
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$manifest.schema -cne 'arcgis-pro-mcp-release-manifest-v1' -or
        [string]$manifest.product.addInId -cne $addInId -or
        [string]$manifest.artifact.fileName -cne [IO.Path]::GetFileName($packagePath)) { throw 'BUNDLE_IDENTITY_INVALID' }
    if ([int64]$manifest.artifact.sizeBytes -ne (Get-Item -LiteralPath $packagePath).Length -or
        [string]$manifest.artifact.sha256 -cne (Get-FileSha256 $packagePath)) { throw 'BUNDLE_INTEGRITY_FAILED' }
    return $manifest
}

function Confirm-Mutation([string]$description) {
    if ($Yes) { return $true }
    Write-Host ''; Write-Host $description -ForegroundColor Yellow
    return (Read-Host 'Enter Y to continue; any other key cancels') -match '^(?i)y$'
}

function Invoke-PowerShellJson([string]$scriptPath, [string[]]$arguments) {
    $shell = Get-Command powershell.exe -ErrorAction SilentlyContinue
    if ($null -eq $shell) { throw 'WINDOWS_POWERSHELL_NOT_FOUND' }
    $lines = @(& $shell.Source -NoProfile -ExecutionPolicy Bypass -File $scriptPath @arguments 2>&1 | ForEach-Object { [string]$_ })
    $exitCode = [int]$LASTEXITCODE
    $text = $lines -join [Environment]::NewLine
    $parsed = $null
    try { $parsed = $text | ConvertFrom-Json } catch { }
    return [pscustomobject]@{ ExitCode = $exitCode; Text = $text; Json = $parsed }
}

function Write-State([string]$transactionRoot, [string]$ledgerPath, [string]$lastAction) {
    if (-not (Test-Path -LiteralPath $stateRoot -PathType Container)) { New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null }
    $value = [ordered]@{
        schema = 'arcgis-pro-mcp-share-installer-state-v1'; addInId = $addInId; installRoot = $installRoot
        transactionRoot = $transactionRoot; ledgerPath = $ledgerPath; lastAction = $lastAction
        updatedAtUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json -Depth 6
    $temporary = Join-Path $stateRoot ('.tmp-' + [guid]::NewGuid().ToString('N'))
    try {
        [IO.File]::WriteAllText($temporary, $value, [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporary -Destination $statePath -Force
    } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue } }
}

function Read-State {
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) { throw 'SAFE_TRANSACTION_STATE_NOT_FOUND' }
    $state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$state.schema -cne 'arcgis-pro-mcp-share-installer-state-v1' -or
        [string]$state.addInId -cne $addInId -or
        [IO.Path]::GetFullPath([string]$state.installRoot) -cne $installRoot) { throw 'SAFE_TRANSACTION_STATE_INVALID' }
    $transactionRoot = [IO.Path]::GetFullPath([string]$state.transactionRoot)
    $ledgerPath = [IO.Path]::GetFullPath([string]$state.ledgerPath)
    $ownedTransactions = [IO.Path]::GetFullPath((Join-Path $stateRoot 'transactions')).TrimEnd('\') + '\'
    if (-not $transactionRoot.StartsWith($ownedTransactions, [StringComparison]::OrdinalIgnoreCase) -or
        -not $ledgerPath.StartsWith($transactionRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'SAFE_TRANSACTION_PATH_REFUSED' }
    return [pscustomobject]@{ TransactionRoot = $transactionRoot; LedgerPath = $ledgerPath }
}

function Get-ClientTarget([string]$clientId, [string]$projectPath) {
    switch ($clientId) {
        'codex' { return Join-Path $projectPath '.codex\config.toml' }
        'cursor' { return Join-Path $projectPath '.cursor\mcp.json' }
        'deepseek-harness' { return Join-Path ([Environment]::GetFolderPath('UserProfile')) '.dsh\profiles\web\cordis.patch.yml' }
        default { throw 'CLIENT_REQUIRED' }
    }
}

function Resolve-ProjectRoot([string]$clientId) {
    if ($clientId -eq 'deepseek-harness') { return '' }
    if (-not [string]::IsNullOrWhiteSpace($ProjectRoot)) { return [IO.Path]::GetFullPath($ProjectRoot) }
    if ($Yes) { return $bundleRoot }
    Write-Host ''; Write-Host 'Codex/Cursor configuration is project-scoped. Leave blank to use this extracted bundle folder.' -ForegroundColor Cyan
    $entered = Read-Host ('Project folder [' + $bundleRoot + ']')
    if ([string]::IsNullOrWhiteSpace($entered)) { return $bundleRoot }
    return [IO.Path]::GetFullPath($entered.Trim([char]34))
}

function Invoke-Check {
    $manifest = Read-BundleIdentity
    # D-113 (R-G244-P1): -ConfigPath comes from the payload, never from the outer metadata file; the throwaway
    # copy is removed as soon as the checker answers.
    $extracted = Get-PayloadConfigDaml
    try {
        $compatibility = Invoke-PowerShellJson $compatibilityPath @('-PolicyPath', $policyPath, '-ManifestPath', $manifestPath, '-ConfigPath', $extracted.Path, '-Json')
    } finally {
        Remove-Item -LiteralPath $extracted.ScratchRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    $compatibilityStatus = if ($null -ne $compatibility.Json) { [string]$compatibility.Json.overallStatus } else { 'ERROR' }
    $listener = $false; $tcp = [Net.Sockets.TcpClient]::new()
    try {
        $pending = $tcp.BeginConnect('127.0.0.1', 6520, $null, $null)
        if ($pending.AsyncWaitHandle.WaitOne(750)) { try { $tcp.EndConnect($pending); $listener = $tcp.Connected } catch { $listener = $false } }
    } finally { $tcp.Dispose() }
    Write-Result ([ordered]@{
        schema = 'arcgis-pro-mcp-share-check-v1'; status = $compatibilityStatus; version = [string]$manifest.product.releaseVersion
        bundleIntegrity = 'PASS'; compatibility = $compatibilityStatus; configSource = $extracted.Source
        addInInstalled = (Test-Path -LiteralPath $installTarget -PathType Container); endpointListener = $listener
        endpoint = 'http://127.0.0.1:6520/mcp'
        message = if ($listener) { 'Bundle integrity passed and the MCP port is listening.' } else { 'Bundle integrity passed. If installed, click Start on the ArcGIS Pro MCP tab.' }
        nextStep = 'Install the add-in, start MCP in ArcGIS Pro, then configure one AI client.'
    })
}

function Invoke-Install {
    $manifest = Read-BundleIdentity
    $transactionRoot = Join-Path $stateRoot ('transactions\install-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
    $ledgerPath = Join-Path $transactionRoot 'ledger.json'
    Write-Host ('Add-in target: ' + $installTarget); Write-Host ('Transaction ledger: ' + $ledgerPath)
    if (-not (Confirm-Mutation ('Install ArcGIS Pro MCP ' + [string]$manifest.product.releaseVersion + '. Close ArcGIS Pro first.'))) {
        Write-Result ([ordered]@{ status = 'CANCELLED'; message = 'Installation was not started.' }); return
    }
    $arguments = @('-Action', 'Install', '-PackagePath', $packagePath, '-ManifestPath', $manifestPath,
        '-InstallRoot', $installRoot, '-TransactionRoot', $transactionRoot, '-LedgerPath', $ledgerPath)
    if ($DryRun) { $arguments += '-DryRun' }
    $result = Invoke-PowerShellJson $workflowPath $arguments
    if ($result.ExitCode -ne 0 -or $null -eq $result.Json -or [string]$result.Json.status -ne 'PASS') {
        $code = if ($null -ne $result.Json -and @($result.Json.failed).Count -gt 0) { [string]$result.Json.failed[0].errorCode } else { 'INSTALL_FAILED' }; throw $code
    }
    if (-not $DryRun) { Write-State $transactionRoot $ledgerPath 'Install' }
    Write-Result ([ordered]@{
        status = 'PASS'; message = if ($DryRun) { 'Install dry-run passed; the add-in target was not changed.' } else { 'Add-in installation completed.' }
        nextStep = if ($DryRun) { 'Run Install again without -DryRun.' } else { 'Open ArcGIS Pro, click Start on the MCP tab, then run CONFIGURE-CODEX.cmd.' }
    })
}

function Invoke-TransactionAction([string]$transactionAction) {
    $null = Read-BundleIdentity; $state = Read-State
    Write-Host ('Add-in target: ' + $installTarget); Write-Host ('Transaction ledger: ' + $state.LedgerPath)
    $description = if ($transactionAction -eq 'Uninstall') { 'Uninstall the add-in managed by this transaction.' } else { 'Restore the add-in state from before this install transaction.' }
    if (-not (Confirm-Mutation $description)) { Write-Result ([ordered]@{ status = 'CANCELLED'; message = 'No change was made.' }); return }
    $arguments = @('-Action', $transactionAction, '-PackagePath', $packagePath, '-ManifestPath', $manifestPath,
        '-InstallRoot', $installRoot, '-TransactionRoot', $state.TransactionRoot, '-LedgerPath', $state.LedgerPath)
    if ($DryRun) { $arguments += '-DryRun' }
    $result = Invoke-PowerShellJson $workflowPath $arguments
    if ($result.ExitCode -ne 0 -or $null -eq $result.Json -or [string]$result.Json.status -ne 'PASS') {
        $code = if ($null -ne $result.Json -and @($result.Json.failed).Count -gt 0) { [string]$result.Json.failed[0].errorCode } else { ($transactionAction.ToUpperInvariant() + '_FAILED') }; throw $code
    }
    if (-not $DryRun) { Write-State $state.TransactionRoot $state.LedgerPath $transactionAction }
    Write-Result ([ordered]@{ status = 'PASS'; message = ($transactionAction + ' completed.'); nextStep = 'Keep the installer state and ledger so safe recovery remains available.' })
}

function Invoke-ClientAction([string]$clientAction) {
    $null = Read-BundleIdentity
    if ([string]::IsNullOrWhiteSpace($Client)) { throw 'CLIENT_REQUIRED' }
    $projectPath = Resolve-ProjectRoot $Client; $targetPath = Get-ClientTarget $Client $projectPath
    Write-Host ('Client: ' + $Client); Write-Host ('Config target: ' + $targetPath)
    $verb = if ($clientAction -eq 'ClientApply') { 'Apply managed MCP configuration' } else { 'Restore the pre-apply configuration backup' }
    if (-not $DryRun -and -not (Confirm-Mutation ($verb + '. Only this client will be changed. Close the client first.'))) {
        Write-Result ([ordered]@{ status = 'CANCELLED'; message = 'Client configuration was not changed.' }); return
    }
    $delegatedAction = if ($DryRun) { 'Validate' } else { $clientAction }
    $arguments = @('-Action', $delegatedAction, '-Client', $Client, '-CatalogPath', $catalogPath, '-TemplateRoot', $bundleRoot)
    if (-not [string]::IsNullOrWhiteSpace($projectPath)) { $arguments += @('-ConfigRoot', $projectPath) }
    $result = Invoke-PowerShellJson $workflowPath $arguments
    if ($result.ExitCode -ne 0 -or $null -eq $result.Json -or [string]$result.Json.status -ne 'PASS') {
        $code = if ($null -ne $result.Json -and @($result.Json.failed).Count -gt 0) { [string]$result.Json.failed[0].errorCode } else { 'CLIENT_CONFIGURATION_FAILED' }; throw $code
    }
    if ($DryRun) {
        Write-Result ([ordered]@{ status = 'PASS'; message = ($Client + ' validation completed; no client configuration was changed.'); nextStep = 'Run again without -DryRun to apply or restore.' })
        return
    }
    Write-Result ([ordered]@{
        status = 'PASS'; message = if ($clientAction -eq 'ClientApply') { ($Client + ' configuration completed.') } else { ($Client + ' configuration restored.') }
        nextStep = if ($clientAction -eq 'ClientApply') { 'Restart the client, then run tools/list and ping after MCP starts in ArcGIS Pro.' } else { 'The client can now be reopened.' }
    })
}

function Show-Menu {
    while ($true) {
        Clear-Host; Write-Host 'ArcGIS Pro MCP 1.0.2 - Simple Setup' -ForegroundColor Cyan
        Write-Host '1. Check computer and bundle'; Write-Host '2. Install ArcGIS Pro add-in'; Write-Host '3. Configure Codex (recommended)'
        Write-Host '4. Configure Cursor'; Write-Host '5. Configure DeepSeek Harness'; Write-Host '6. Restore one client configuration'
        Write-Host '7. Uninstall add-in'; Write-Host '8. Roll back latest install transaction'; Write-Host '0. Exit'
        $choice = Read-Host 'Select'
        try {
            switch ($choice) {
                '1' { & $PSCommandPath -Action Check }; '2' { & $PSCommandPath -Action Install }
                '3' { & $PSCommandPath -Action ConfigureClient -Client codex }; '4' { & $PSCommandPath -Action ConfigureClient -Client cursor }
                '5' { & $PSCommandPath -Action ConfigureClient -Client deepseek-harness }
                '6' { $selected = Read-Host 'Enter codex, cursor, or deepseek-harness'; & $PSCommandPath -Action RestoreClient -Client $selected }
                '7' { & $PSCommandPath -Action Uninstall }; '8' { & $PSCommandPath -Action Rollback }; '0' { return }
                default { Write-Host 'Invalid selection.' -ForegroundColor Yellow }
            }
        } catch { Write-Host ('Failed: ' + $_.Exception.Message) -ForegroundColor Red }
        Write-Host ''; Read-Host 'Press Enter to return to the menu' | Out-Null
    }
}

try {
    switch ($Action) {
        'Menu' { Show-Menu }; 'Check' { Invoke-Check }; 'Install' { Invoke-Install }
        'Uninstall' { Invoke-TransactionAction 'Uninstall' }; 'Rollback' { Invoke-TransactionAction 'Rollback' }
        'ConfigureClient' { Invoke-ClientAction 'ClientApply' }; 'RestoreClient' { Invoke-ClientAction 'ClientRestore' }
    }
} catch {
    Write-Result ([ordered]@{ status = 'FAILED'; errorCode = ([string]$_.Exception.Message -split ':')[0]
        message = 'The operation did not complete. Keep the ledger/backups and do not manually delete unknown directories.'
        nextStep = 'Run Check first. For client errors, review the current config before RestoreClient.' })
    exit 1
}
