# ArcGIS Pro MCP - safe user workflow orchestrator
#
# This is intentionally a thin coordinator. It does not implement package,
# installation, rollback, or client-file mutation logic. It delegates to the
# validated repository scripts and emits only a safe machine-readable ledger.
# Default action is read-only Plan.
[CmdletBinding()]
param(
    [string]$Action = 'Plan',
    [string]$Client = '',
    [string]$Configuration = 'Debug',
    [string]$CatalogPath = '',
    [string]$ConfigRoot = '',
    [string]$TemplateRoot = '',
    [string]$ProjectDir = '',
    [string]$PackagePath = '',
    [string]$ManifestPath = '',
    [string]$InstallRoot = '',
    [string]$TransactionRoot = '',
    [string]$LedgerPath = '',
    [switch]$AllowCreateInstallRoot,
    [switch]$TestMode,
    [switch]$TestInjectChildFailure,
    [switch]$DryRun,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$catalogFile = if ([string]::IsNullOrWhiteSpace($CatalogPath)) {
    Join-Path $repoRoot 'Config\client-catalog.json'
} else {
    [IO.Path]::GetFullPath($CatalogPath)
}
$clientScript = Join-Path $PSScriptRoot 'client-config.ps1'
$packageScript = Join-Path $PSScriptRoot 'package-addin.ps1'
$releaseScript = Join-Path $PSScriptRoot 'release-transaction.ps1'
$allowedActions = @('Plan', 'Validate', 'Package', 'Install', 'Uninstall', 'Rollback', 'ClientApply', 'ClientRestore')
$mutationActions = @('Package', 'Install', 'Uninstall', 'Rollback', 'ClientApply', 'ClientRestore')

function Add-Argument([System.Collections.Generic.List[string]]$arguments, [string]$name, [string]$value) {
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        [void]$arguments.Add($name)
        [void]$arguments.Add($value)
    }
}

function Read-Catalog {
    if (-not (Test-Path -LiteralPath $catalogFile -PathType Leaf)) {
        throw 'WORKFLOW_CATALOG_NOT_FOUND'
    }

    $catalog = Get-Content -LiteralPath $catalogFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if (($catalog.schema -ne 'arcgis-pro-mcp-client-catalog-v1') -or
        ($catalog.server.canonicalProductionToolCount -ne 239) -or
        ($catalog.server.endpoint -ne 'http://127.0.0.1:6520/mcp')) {
        throw 'WORKFLOW_CATALOG_INVALID'
    }

    return $catalog
}

function Get-SafeChildErrorCode([string]$capturedOutput) {
    $knownCodes = @(
        'APPLY_UNSUPPORTED',
        'ARCGIS_PRO_RUNNING',
        'BACKUP_METADATA_MISSING',
        'BACKUP_NOT_FOUND',
        'CATALOG_SCHEMA_INVALID',
        'CATALOG_TOOL_NORMALIZATION_INVALID',
        'CLIENT_NOT_IN_CATALOG',
        'COMPATIBILITY_GATE_REFUSED',
        'CONFIG_ENDPOINT_INVALID',
        'CONFIG_NOT_FOUND',
        'CONFIG_PATH_INVALID',
        'CONFIG_SYNTAX_INVALID',
        'INJECTED_POST_WRITE_FAILURE',
        'MANIFEST_ID_MISMATCH',
        'MANIFEST_NOT_FOUND',
        'MANIFEST_RUNTIME_ARTIFACT_MISMATCH',
        'MUTATION_REQUIRES_EXPLICIT_CLIENT',
        'PACKAGE_CONTENT_INVALID',
        'PACKAGE_ID_MISMATCH',
        'PACKAGE_NOT_FOUND',
        'RESTORE_VERIFY_FAILED',
        'SECRET_FIELD_REFUSED',
        'STALE_BACKUP_REFUSED',
        'TEST_INJECTED_FAILURE_AFTER_INSTALL',
        'TEST_INJECTION_REFUSED',
        'UNMANAGED_EXISTING_ENTRY'
    )

    foreach ($code in $knownCodes) {
        if ($capturedOutput -match ('(?<![A-Z0-9_])' + [regex]::Escape($code) + '(?![A-Z0-9_])')) {
            return $code
        }
    }

    return 'WORKFLOW_CHILD_FAILED'
}

function Invoke-ChildScript([string]$scriptPath, [System.Collections.Generic.List[string]]$arguments) {
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        return [pscustomobject]@{ ExitCode = 127; CapturedOutput = 'WORKFLOW_SCRIPT_NOT_FOUND' }
    }

    # Prefer Windows PowerShell for the existing ArcGIS/Compress-Archive scripts;
    # fall back to PowerShell 7 only when powershell.exe is unavailable.
    $shell = Get-Command powershell.exe -ErrorAction SilentlyContinue
    if ($null -eq $shell) {
        $shell = Get-Command pwsh.exe -ErrorAction SilentlyContinue
    }
    if ($null -eq $shell) {
        return [pscustomobject]@{ ExitCode = 127; CapturedOutput = 'WORKFLOW_SHELL_NOT_FOUND' }
    }

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # The delegated scripts intentionally emit controlled failure details with
        # Write-Error. Capture those records as data so the workflow can classify
        # the child exit code and preserve its ledger instead of entering the
        # workflow-level configuration catch.
        $ErrorActionPreference = 'Continue'
        $captured = @(
            & $shell.Source -NoProfile -ExecutionPolicy Bypass -File $scriptPath @arguments 2>&1 |
                ForEach-Object { [string]$_ }
        ) -join [Environment]::NewLine
        $childExitCode = [int]$LASTEXITCODE
        return [pscustomobject]@{ ExitCode = $childExitCode; CapturedOutput = $captured }
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function New-Ledger([string]$workflowAction, [bool]$readOnly, [bool]$mutationAttempted = $false) {
    return [ordered]@{
        schema = 'arcgis-pro-mcp-user-workflow-ledger-v1'
        action = $workflowAction
        readOnly = $readOnly
        mutationAttempted = $mutationAttempted
        status = 'PASS'
        completed = @()
        failed = @()
        notStarted = @()
        transaction = $null
        recovery = [ordered]@{ state = 'NOT_REQUIRED'; action = 'NONE'; instruction = 'No recovery action is required.' }
    }
}

function Write-Ledger([System.Collections.IDictionary]$ledger) {
    Write-Output ($ledger | ConvertTo-Json -Depth 10)
}

function Add-Success([System.Collections.IDictionary]$ledger, [string]$step, [string]$status = 'PASS') {
    $ledger.completed += ,([ordered]@{ step = $step; status = $status })
}

function Add-Failure([System.Collections.IDictionary]$ledger, [string]$step, [string]$errorCode, [int]$exitCode) {
    $ledger.status = 'FAILED'
    $ledger.failed += ,([ordered]@{ step = $step; status = 'FAILED'; errorCode = $errorCode; exitCode = $exitCode })
}

function Add-NotStarted([System.Collections.IDictionary]$ledger, [string]$step) {
    $ledger.notStarted += ,([ordered]@{ step = $step; status = 'NOT_STARTED' })
}

function Get-RecoveryInstruction([string]$workflowAction, [string]$clientId) {
    if ($workflowAction -in @('ClientApply', 'ClientRestore')) {
        return [ordered]@{
            state = 'CLIENT_ACTION_NOT_COMPLETED'
            action = 'Validate'
            instruction = 'Run read-only Validate for the selected client, review the fixed errorCode, and do not retry until the client boundary is understood.'
        }
    }

    if ($workflowAction -in @('Install', 'Uninstall', 'Rollback')) {
        return [ordered]@{
            state = 'TRANSACTION_STATE_NOT_VERIFIED'
            action = 'Preflight'
            instruction = 'Review the delegated transaction ledger with the underlying read-only Preflight before any retry; do not delete files manually.'
        }
    }

    if ($workflowAction -eq 'Package') {
        return [ordered]@{
            state = 'PACKAGE_NOT_COMPLETED'
            action = 'Package'
            instruction = 'No installation or registration was started. Review the fixed errorCode before retrying package-only.'
        }
    }

    return [ordered]@{
        state = 'NO_MUTATION_STARTED'
        action = 'Plan'
        instruction = 'No mutation was started. Review the fixed errorCode and rerun the read-only Plan or Validate action.'
    }
}

function Get-ExpectedReleaseSteps([string]$workflowAction, [bool]$dryRun) {
    if ($dryRun -and $workflowAction -in @('Install', 'Uninstall', 'Rollback')) {
        return @('preflight', 'compatibility-preflight', 'dry-run')
    }
    switch ($workflowAction) {
        'Install' { return @('preflight', 'compatibility-preflight', 'snapshot', 'install', 'install-verify') }
        'Uninstall' { return @('preflight', 'compatibility-preflight', 'uninstall') }
        'Rollback' { return @('preflight', 'compatibility-preflight', 'rollback') }
        default { return @() }
    }
}

function Get-SafeReleaseStep([string]$eventName) {
    switch ($eventName) {
        'preflight' { return 'preflight' }
        'compatibility-preflight' { return 'compatibility-preflight' }
        'snapshot' { return 'snapshot' }
        'install' { return 'install' }
        'register-addin' { return 'install' }
        'install-verify' { return 'install-verify' }
        'uninstall' { return 'uninstall' }
        'rollback' { return 'rollback' }
        'automatic-rollback' { return 'automatic-rollback' }
        'dry-run' { return 'dry-run' }
        default { return $null }
    }
}

function Read-SafeReleaseLedger([string]$path, [string]$workflowAction, [bool]$dryRun) {
    $unavailable = [ordered]@{
        availability = 'NOT_VERIFIED'
        status = 'UNKNOWN'
        errorState = if ([string]::IsNullOrWhiteSpace($path)) { 'LEDGER_PATH_NOT_SUPPLIED' } else { 'LEDGER_NOT_READABLE' }
        events = @()
        completed = @()
        failed = @()
        notStarted = @()
        recoveryAttempted = $false
    }
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return [pscustomobject]$unavailable
    }

    try {
        $source = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        $unavailable.errorState = 'LEDGER_SYNTAX_INVALID'
        return [pscustomobject]$unavailable
    }
    if ([string]$source.schema -cne 'arcgis-pro-mcp-release-transaction-v1') {
        $unavailable.errorState = 'LEDGER_SCHEMA_INVALID'
        return [pscustomobject]$unavailable
    }

    $actionProperty = $source.PSObject.Properties['action']
    if ($null -eq $actionProperty -or [string]$source.action -cne $workflowAction) {
        $unavailable.errorState = 'LEDGER_ACTION_MISMATCH'
        return [pscustomobject]$unavailable
    }

    $dryRunProperty = $source.PSObject.Properties['dryRun']
    if ($null -eq $dryRunProperty -or
        -not ($source.dryRun -is [bool]) -or
        [bool]$source.dryRun -ne $dryRun) {
        $unavailable.errorState = 'LEDGER_DRYRUN_MISMATCH'
        return [pscustomobject]$unavailable
    }

    $expectedSteps = @(Get-ExpectedReleaseSteps $workflowAction $dryRun)
    $allowedSteps = @($expectedSteps)
    if (-not $dryRun) { $allowedSteps += @('rollback', 'automatic-rollback') }
    $safeEvents = @()
    foreach ($candidate in @($source.events)) {
        if ($null -eq $candidate) { continue }
        $sourceName = [string]$candidate.name
        $safeStep = Get-SafeReleaseStep $sourceName
        $state = ([string]$candidate.state).ToUpperInvariant()
        if ($null -eq $safeStep -or $state -notin @('PASS', 'FAIL') -or $allowedSteps -notcontains $safeStep) { continue }
        $safeEvents += ,([ordered]@{
            step = $safeStep
            source = $sourceName
            status = $state
        })
    }

    $completed = @()
    $failed = @()
    $seen = @{}
    foreach ($event in $safeEvents) {
        $step = [string]$event.step
        $seen[$step] = $true
        $entry = [ordered]@{ step = $step; status = [string]$event.status; source = [string]$event.source }
        if ($event.status -eq 'PASS') {
            if (-not @($completed | Where-Object { $_.step -eq $step }).Count) { $completed += ,$entry }
        } else {
            if (-not @($failed | Where-Object { $_.step -eq $step }).Count) { $failed += ,$entry }
        }
    }

    $notStarted = @()
    foreach ($step in $expectedSteps) {
        if (-not $seen.ContainsKey($step)) {
            $notStarted += ,([ordered]@{ step = $step; status = 'NOT_STARTED' })
        }
    }

    $status = ([string]$source.status).ToUpperInvariant()
    if ($status -notin @('PASS', 'FAIL')) { $status = 'UNKNOWN' }
    $recoveryAttempted = $false
    if ($source.PSObject.Properties['recoveryAttempted']) { $recoveryAttempted = [bool]$source.recoveryAttempted }
    return [pscustomobject]@{
        availability = 'VERIFIED'
        status = $status
        errorState = ''
        events = @($safeEvents)
        completed = @($completed)
        failed = @($failed)
        notStarted = @($notStarted)
        recoveryAttempted = $recoveryAttempted
    }
}

function Get-ReleaseRecovery($safeLedger, [bool]$childFailed) {
    if ($safeLedger.availability -ne 'VERIFIED') {
        return [ordered]@{
            state = 'TRANSACTION_LEDGER_NOT_VERIFIED'
            action = 'Preflight'
            instruction = 'Run the underlying read-only Preflight for the same owned transaction and review the ledger before any retry.'
        }
    }
    $rollbackPass = @($safeLedger.events | Where-Object { $_.step -eq 'rollback' -and $_.status -eq 'PASS' }).Count -gt 0
    $rollbackFail = @($safeLedger.events | Where-Object { $_.step -in @('rollback', 'automatic-rollback') -and $_.status -eq 'FAIL' }).Count -gt 0
    if (-not $childFailed -and $safeLedger.status -eq 'PASS' -and @($safeLedger.notStarted).Count -eq 0) {
        return [ordered]@{ state = 'COMPLETED'; action = 'NONE'; instruction = 'Verified delegated transaction completed; no recovery action is required.' }
    }
    if (-not $childFailed -and $safeLedger.status -eq 'PASS') {
        return [ordered]@{ state = 'TRANSACTION_LEDGER_INCOMPLETE'; action = 'Preflight'; instruction = 'The delegated ledger is incomplete; run the underlying read-only Preflight before any retry.' }
    }
    if ($rollbackPass) {
        return [ordered]@{ state = 'AUTOMATIC_ROLLBACK_VERIFIED'; action = 'NONE'; instruction = 'Automatic rollback was verified; review the original failure before retrying the mutation.' }
    }
    if ($rollbackFail -or $safeLedger.status -eq 'FAIL') {
        return [ordered]@{ state = 'ROLLBACK_REQUIRED'; action = 'Rollback'; instruction = 'Review the owned transaction ledger, then run the delegated Rollback with the same explicit LedgerPath.' }
    }
    return [ordered]@{ state = 'TRANSACTION_OUTCOME_UNKNOWN'; action = 'Preflight'; instruction = 'Run the underlying read-only Preflight before any retry.' }
}

function Add-ReleaseSummary([System.Collections.IDictionary]$ledger, $safeLedger) {
    foreach ($entry in @($safeLedger.completed)) {
        $ledger.completed += ,([ordered]@{ step = 'release:' + [string]$entry.step; status = 'PASS'; source = [string]$entry.source })
    }
    foreach ($entry in @($safeLedger.failed)) {
        $ledger.failed += ,([ordered]@{ step = 'release:' + [string]$entry.step; status = 'FAILED'; source = [string]$entry.source; errorCode = 'DELEGATED_TRANSACTION_FAILED'; exitCode = 1 })
    }
    foreach ($entry in @($safeLedger.notStarted)) {
        $ledger.notStarted += ,([ordered]@{ step = 'release:' + [string]$entry.step; status = 'NOT_STARTED' })
    }
    $ledger.transaction = [ordered]@{
        availability = [string]$safeLedger.availability
        status = [string]$safeLedger.status
        completed = @($safeLedger.completed)
        failed = @($safeLedger.failed)
        notStarted = @($safeLedger.notStarted)
        recoveryAttempted = [bool]$safeLedger.recoveryAttempted
    }
}

function Get-ActionStep([string]$workflowAction) {
    switch ($workflowAction) {
        'Package' { return 'package-only' }
        'Install' { return 'release:install' }
        'Uninstall' { return 'release:uninstall' }
        'Rollback' { return 'release:rollback' }
        'ClientApply' { return 'client-configuration' }
        'ClientRestore' { return 'client-configuration' }
        default { return 'workflow' }
    }
}

try {
    if ($allowedActions -notcontains $Action) {
        throw 'WORKFLOW_ACTION_INVALID'
    }

    if ($Configuration -notin @('Debug', 'Release')) {
        throw 'WORKFLOW_CONFIGURATION_INVALID'
    }

    $catalog = Read-Catalog
    $catalogClients = @($catalog.clients)
    if ($Action -in @('Validate', 'ClientApply', 'ClientRestore')) {
        if ($Action -in @('ClientApply', 'ClientRestore') -and [string]::IsNullOrWhiteSpace($Client)) {
            throw 'WORKFLOW_CLIENT_REQUIRED'
        }

        if (-not [string]::IsNullOrWhiteSpace($Client) -and
            @($catalogClients | Where-Object { $_.id -ceq $Client }).Count -ne 1) {
            throw 'WORKFLOW_CLIENT_INVALID'
        }
    } elseif (-not [string]::IsNullOrWhiteSpace($Client)) {
        throw 'WORKFLOW_ACTION_SCOPE_INVALID'
    }

    if ($Action -in @('Install', 'Uninstall', 'Rollback') -and [string]::IsNullOrWhiteSpace($LedgerPath)) {
        throw 'WORKFLOW_LEDGER_REQUIRED'
    }

    $isReadOnly = $Action -in @('Plan', 'Validate') -or
        ($Action -in @('Install', 'Uninstall', 'Rollback') -and $DryRun)
    $ledger = New-Ledger $Action $isReadOnly $false

    if ($Action -eq 'Plan') {
        $ledger.availableActions = @(
            [ordered]@{ action = 'Plan'; mutation = $false; default = $true }
            [ordered]@{ action = 'Validate'; mutation = $false; scope = 'supported client definitions' }
            [ordered]@{ action = 'Package'; mutation = $true; boundary = 'package artifact only; always SkipRegistration' }
            [ordered]@{ action = 'Install'; mutation = $true; boundary = 'delegated release transaction' }
            [ordered]@{ action = 'Uninstall'; mutation = $true; boundary = 'delegated release transaction' }
            [ordered]@{ action = 'Rollback'; mutation = $true; boundary = 'delegated release transaction' }
            [ordered]@{ action = 'ClientApply'; mutation = $true; boundary = 'one explicit Client only' }
            [ordered]@{ action = 'ClientRestore'; mutation = $true; boundary = 'one explicit Client only' }
        )
        $ledger.clients = @($catalogClients | ForEach-Object {
            [ordered]@{
                id = $_.id
                displayName = $_.displayName
                priority = $_.priority
                required = $_.required
                configScope = $_.configScope
                configPath = $_.configPath
            }
        })
        Add-Success $ledger 'plan' 'PLAN_ONLY'
        $ledger.recovery = [ordered]@{ state = 'READ_ONLY_COMPLETE'; action = 'NONE'; instruction = 'No files, package artifacts, installation targets, client configs, or processes were changed.' }
        Write-Ledger $ledger
        exit 0
    }

    if ($Action -eq 'Validate') {
        $clientArgs = [System.Collections.Generic.List[string]]::new()
        [void]$clientArgs.Add('-Action')
        [void]$clientArgs.Add('Validate')
        Add-Argument $clientArgs '-CatalogPath' $CatalogPath
        Add-Argument $clientArgs '-ConfigRoot' $ConfigRoot
        Add-Argument $clientArgs '-TemplateRoot' $TemplateRoot
        Add-Argument $clientArgs '-Client' $Client
        [void]$clientArgs.Add('-Json')
        $result = Invoke-ChildScript $clientScript $clientArgs
        if ($result.ExitCode -ne 0) {
            $code = Get-SafeChildErrorCode $result.CapturedOutput
            Add-Failure $ledger 'client-validation' $code $result.ExitCode
            $ledger.recovery = [ordered]@{ state = 'VALIDATION_FAILED'; action = 'Validate'; instruction = 'No client configuration was written. Correct the read-only validation condition before rerunning Validate.' }
            Write-Ledger $ledger
            exit ([Math]::Max(1, [Math]::Min(255, $result.ExitCode)))
        }

        Add-Success $ledger 'client-validation'
        $ledger.validatedScope = if ([string]::IsNullOrWhiteSpace($Client)) { 'all-supported-clients' } else { $Client }
        $ledger.recovery = [ordered]@{ state = 'READ_ONLY_COMPLETE'; action = 'NONE'; instruction = 'Read-only validation completed; no client configuration, package, installation target, or backup was changed.' }
        Write-Ledger $ledger
        exit 0
    }

    if ($Action -in @('ClientApply', 'ClientRestore')) {
        $clientArgs = [System.Collections.Generic.List[string]]::new()
        [void]$clientArgs.Add('-Action')
        [void]$clientArgs.Add($(if ($Action -eq 'ClientApply') { 'Apply' } else { 'Restore' }))
        Add-Argument $clientArgs '-CatalogPath' $CatalogPath
        Add-Argument $clientArgs '-ConfigRoot' $ConfigRoot
        Add-Argument $clientArgs '-TemplateRoot' $TemplateRoot
        Add-Argument $clientArgs '-Client' $Client
        [void]$clientArgs.Add('-Json')
        $ledger.mutationAttempted = $true
        $result = Invoke-ChildScript $clientScript $clientArgs
        if ($result.ExitCode -ne 0) {
            $code = Get-SafeChildErrorCode $result.CapturedOutput
            Add-Failure $ledger 'client-configuration' $code $result.ExitCode
            $ledger.delegated = [ordered]@{ boundary = 'client-config'; action = if ($Action -eq 'ClientApply') { 'Apply' } else { 'Restore' }; status = 'FAILED'; client = $Client }
            $ledger.recovery = Get-RecoveryInstruction $Action $Client
            Write-Ledger $ledger
            exit ([Math]::Max(1, [Math]::Min(255, $result.ExitCode)))
        }

        Add-Success $ledger 'client-configuration'
        $ledger.client = $Client
        $ledger.delegatedAction = if ($Action -eq 'ClientApply') { 'Apply' } else { 'Restore' }
        $ledger.delegated = [ordered]@{ boundary = 'client-config'; action = $ledger.delegatedAction; status = 'PASS'; client = $Client }
        $ledger.recovery = [ordered]@{ state = 'COMPLETED'; action = 'NONE'; instruction = 'Client configurator completed; no additional recovery action is required.' }
        Write-Ledger $ledger
        exit 0
    }

    if ($Action -eq 'Package') {
        $packageArgs = [System.Collections.Generic.List[string]]::new()
        Add-Argument $packageArgs '-Configuration' $Configuration
        Add-Argument $packageArgs '-ProjectDir' $ProjectDir
        Add-Argument $packageArgs '-ManifestPath' $ManifestPath
        [void]$packageArgs.Add('-SkipRegistration')
        $ledger.mutationAttempted = $true
        $result = Invoke-ChildScript $packageScript $packageArgs
        if ($result.ExitCode -ne 0) {
            $code = Get-SafeChildErrorCode $result.CapturedOutput
            Add-Failure $ledger 'package-only' $code $result.ExitCode
            $ledger.recovery = Get-RecoveryInstruction $Action $Client
            Write-Ledger $ledger
            exit ([Math]::Max(1, [Math]::Min(255, $result.ExitCode)))
        }

        Add-Success $ledger 'package-only'
        $ledger.registration = 'SKIPPED'
        $ledger.recovery = [ordered]@{ state = 'COMPLETED'; action = 'NONE'; instruction = 'Package artifact generation completed without registration or installation.' }
        Write-Ledger $ledger
        exit 0
    }

    $releaseArgs = [System.Collections.Generic.List[string]]::new()
    Add-Argument $releaseArgs '-Action' $Action
    Add-Argument $releaseArgs '-PackagePath' $PackagePath
    Add-Argument $releaseArgs '-ManifestPath' $ManifestPath
    Add-Argument $releaseArgs '-InstallRoot' $InstallRoot
    Add-Argument $releaseArgs '-TransactionRoot' $TransactionRoot
    Add-Argument $releaseArgs '-LedgerPath' $LedgerPath
    if ($AllowCreateInstallRoot) { [void]$releaseArgs.Add('-AllowCreateInstallRoot') }
    if ($TestMode) { [void]$releaseArgs.Add(('-' + 'TestMode')) }
    if ($TestInjectChildFailure -and -not $TestMode) { throw 'TEST_INJECTION_REFUSED' }
    if ($TestInjectChildFailure) { [void]$releaseArgs.Add(('-' + 'InjectFailureAfterInstall')) }
    if ($DryRun) { [void]$releaseArgs.Add('-DryRun') }
    $ledger.mutationAttempted = -not $DryRun
    $result = Invoke-ChildScript $releaseScript $releaseArgs
    $childFailed = $result.ExitCode -ne 0
    $childCode = if ($childFailed) { Get-SafeChildErrorCode $result.CapturedOutput } else { '' }
    $safeTransaction = Read-SafeReleaseLedger $LedgerPath $Action $DryRun
    Add-ReleaseSummary $ledger $safeTransaction
    $ledger.recovery = Get-ReleaseRecovery $safeTransaction $childFailed
    $ledger.delegated = [ordered]@{
        boundary = 'release-transaction'
        action = $Action
        status = if ($childFailed) { 'FAILED' } else { [string]$safeTransaction.status }
    }
    if ($childFailed) {
        Add-Failure $ledger 'release-transaction' $childCode $result.ExitCode
        Write-Ledger $ledger
        exit ([Math]::Max(1, [Math]::Min(255, $result.ExitCode)))
    }

    if ($safeTransaction.availability -ne 'VERIFIED') {
        Add-Failure $ledger 'transaction-ledger' 'WORKFLOW_LEDGER_NOT_VERIFIED' 1
        Write-Ledger $ledger
        exit 1
    }

    if ($safeTransaction.status -ne 'PASS') {
        Add-Failure $ledger 'release-transaction' 'DELEGATED_TRANSACTION_FAILED' 1
        Write-Ledger $ledger
        exit 1
    }

    if (@($safeTransaction.notStarted).Count -gt 0) {
        Add-Failure $ledger 'transaction-ledger' 'WORKFLOW_LEDGER_INCOMPLETE' 1
        Write-Ledger $ledger
        exit 1
    }

    Add-Success $ledger 'release-transaction'
    $ledger.registration = if ($Action -eq 'Install' -and -not $DryRun) { 'delegated-only' } else { 'not-performed-by-workflow' }
    Write-Ledger $ledger
    exit 0
} catch {
    $safeCode = switch -Regex ([string]$_.Exception.Message) {
        'WORKFLOW_ACTION_INVALID' { 'WORKFLOW_ACTION_INVALID'; break }
        'WORKFLOW_CATALOG_NOT_FOUND' { 'WORKFLOW_CATALOG_NOT_FOUND'; break }
        'WORKFLOW_CATALOG_INVALID' { 'WORKFLOW_CATALOG_INVALID'; break }
        'WORKFLOW_CONFIGURATION_INVALID' { 'WORKFLOW_CONFIGURATION_INVALID'; break }
        'WORKFLOW_CLIENT_REQUIRED' { 'WORKFLOW_CLIENT_REQUIRED'; break }
        'WORKFLOW_CLIENT_INVALID' { 'WORKFLOW_CLIENT_INVALID'; break }
        'WORKFLOW_ACTION_SCOPE_INVALID' { 'WORKFLOW_ACTION_SCOPE_INVALID'; break }
        'WORKFLOW_LEDGER_REQUIRED' { 'WORKFLOW_LEDGER_REQUIRED'; break }
        default { 'WORKFLOW_CONFIGURATION_ERROR' }
    }
    $failureLedger = New-Ledger $Action ($Action -in @('Plan', 'Validate')) $false
    Add-Failure $failureLedger 'workflow' $safeCode 1
    Add-NotStarted $failureLedger (Get-ActionStep $Action)
    $failureLedger.recovery = Get-RecoveryInstruction $Action $Client
    Write-Ledger $failureLedger
    exit 1
}
