# Owned-temp verification for the r5 one-click deployment bundle.
# This harness never installs the Add-in or writes a real client profile.
# The only mutation sequence uses TestMode paths under the Windows temp root.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Import-Module Microsoft.PowerShell.Utility -ErrorAction Stop
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$oneClick = Join-Path $repoRoot 'scripts\one-click-setup.ps1'
$verifier = Join-Path $repoRoot 'scripts\verify-one-click-package.ps1'
$zip = Join-Path $repoRoot 'Release\ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip'
$oldZip = Join-Path $repoRoot 'Release\ArcGIS-Pro-MCP-OneClick-1.0.2-r1-Windows-x64.zip'
$evidenceRoot = Join-Path $repoRoot '.runtime\one-click-deployment-r5'
$runId = 'run-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N')
$runEvidence = Join-Path $evidenceRoot $runId
$root = Join-Path ([IO.Path]::GetTempPath()) ('arcgis-pro-mcp-one-click-r5-test-' + [guid]::NewGuid().ToString('N'))
$passed = 0
$commandRecords = @()
$asyncProcesses = @()
$reportIndex = 0
$script:testFailed = $false
$script:failureCode = ''
$script:failureMessage = ''

function Assert-That([bool]$condition, [string]$message) {
    if (-not $condition) { throw ('ASSERT_FAILED|' + $message) }
    $script:passed++
}

function Save-JsonReport([string]$name, [object]$value) {
    $script:reportIndex++
    $path = Join-Path $runEvidence ('{0:D2}-{1}.json' -f $script:reportIndex, $name)
    [IO.File]::WriteAllText($path, ($value | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
    return $path
}

function Invoke-Json([string]$name, [string]$path, [string[]]$arguments, [switch]$Sta) {
    $script:commandRecords += ,([ordered]@{ name = $name; path = $path; arguments = @($arguments); sta = [bool]$Sta; launch = 'UTF8_ENCODED_COMMAND_BOOTSTRAP'; stdoutEncoding = 'UTF-8'; stderrEncoding = 'UTF-8' })
    $rawOutputRoot = Join-Path $runEvidence 'raw-command-output'
    New-Item -ItemType Directory -Path $rawOutputRoot -Force | Out-Null
    $stdoutPath = Join-Path $rawOutputRoot ('command-output-' + [guid]::NewGuid().ToString('N') + '.stdout.log')
    $stderrPath = Join-Path $rawOutputRoot ('command-output-' + [guid]::NewGuid().ToString('N') + '.stderr.log')
    $process = $null
    try {
        $shell = (Get-Command powershell.exe -ErrorAction Stop).Source
        $argumentParts = @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass')
        if ($Sta) { $argumentParts += '-STA' }
        $invocation = '. ' + (ConvertTo-PowerShellLiteral $path)
        foreach ($argument in @($arguments)) {
            $token = [string]$argument
            if ($token.StartsWith('-')) { $invocation += ' ' + $token }
            else { $invocation += ' ' + (ConvertTo-PowerShellLiteral $token) }
        }
        $bootstrap = @(
            '$ErrorActionPreference = ''Stop''',
            '$utf8 = [System.Text.UTF8Encoding]::new($false)',
            '[Console]::OutputEncoding = $utf8',
            '$OutputEncoding = $utf8',
            '$ProgressPreference = ''SilentlyContinue''',
            '$exitCode = 0',
            'try { ' + $invocation + '; if ($null -ne $LASTEXITCODE) { $exitCode = [int]$LASTEXITCODE } } catch { Write-Error $_; $exitCode = 1 }',
            'exit $exitCode'
        ) -join '; '
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($bootstrap))
        $argumentParts += '-EncodedCommand'
        $argumentParts += $encoded
        $psi = New-Object Diagnostics.ProcessStartInfo
        $psi.FileName = $shell
        $psi.Arguments = $argumentParts -join ' '
        $psi.WorkingDirectory = (Get-Location).Path
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
        $psi.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
        $process = [Diagnostics.Process]::Start($psi)
        $stdoutText = $process.StandardOutput.ReadToEnd()
        $stderrText = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        $code = [int]$process.ExitCode
        [IO.File]::WriteAllText($stdoutPath, $stdoutText, [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText($stderrPath, $stderrText, [Text.UTF8Encoding]::new($false))
        $text = $stdoutText
        if (-not [string]::IsNullOrWhiteSpace($stderrText)) { $text += [Environment]::NewLine + $stderrText }
        $json = $null
        try { $json = $stdoutText | ConvertFrom-Json } catch { }
        return [pscustomobject]@{ Name = $name; Code = $code; Text = $text; Stdout = $stdoutText; Stderr = $stderrText; StdoutPath = $stdoutPath; StderrPath = $stderrPath; Json = $json }
    } finally {
        if ($null -ne $process) { $process.Dispose() }
    }
}

function ConvertTo-PowerShellLiteral([string]$value) {
    if ($null -eq $value) { return "''" }
    return "'" + ([string]$value).Replace("'", "''") + "'"
}

function ConvertTo-ProcessArgument([string]$value) {
    if ($null -eq $value) { return '""' }
    $text = [string]$value
    if ($text -notmatch '[\s"]') { return $text }
    $builder = New-Object Text.StringBuilder
    [void]$builder.Append('"')
    $backslashes = 0
    foreach ($character in $text.ToCharArray()) {
        if ($character -eq '\') {
            $backslashes++
            continue
        }
        if ($character -eq '"') {
            for ($i = 0; $i -lt ((2 * $backslashes) + 1); $i++) { [void]$builder.Append('\') }
            [void]$builder.Append('"')
            $backslashes = 0
            continue
        }
        for ($i = 0; $i -lt $backslashes; $i++) { [void]$builder.Append('\') }
        $backslashes = 0
        [void]$builder.Append($character)
    }
    for ($i = 0; $i -lt (2 * $backslashes); $i++) { [void]$builder.Append('\') }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Start-AsyncOneClick([string]$name, [string]$scriptPath, [string[]]$arguments, [string]$stdoutPath, [string]$stderrPath) {
    $commandParts = @('& ' + (ConvertTo-PowerShellLiteral $scriptPath))
    foreach ($argument in @($arguments)) {
        $token = [string]$argument
        if ($token.StartsWith('-')) { $commandParts += $token }
        else { $commandParts += ConvertTo-PowerShellLiteral $token }
    }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes(($commandParts -join ' ')))
    $shell = (Get-Command powershell.exe -ErrorAction Stop).Source
    $process = Start-Process -FilePath $shell -ArgumentList @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encoded) -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
    $script:asyncProcesses += ,$process
    $script:commandRecords += ,([ordered]@{ name = $name; path = $scriptPath; arguments = @($arguments); asynchronous = $true; pid = $process.Id })
    return [pscustomobject]@{ Name = $name; Process = $process; StdoutPath = $stdoutPath; StderrPath = $stderrPath }
}

function Complete-AsyncOneClick([pscustomobject]$started) {
    $started.Process.WaitForExit()
    $stdoutText = ''
    $stderrText = ''
    if (Test-Path -LiteralPath $started.StdoutPath -PathType Leaf) { $stdoutText = Get-Content -LiteralPath $started.StdoutPath -Raw -Encoding UTF8 }
    if (Test-Path -LiteralPath $started.StderrPath -PathType Leaf) {
        $stderrText = Get-Content -LiteralPath $started.StderrPath -Raw -Encoding UTF8
    }
    $text = $stdoutText
    if (-not [string]::IsNullOrWhiteSpace($stderrText)) { $text += [Environment]::NewLine + $stderrText }
    $json = $null
    try { $json = $stdoutText | ConvertFrom-Json } catch { }
    return [pscustomobject]@{ Name = $started.Name; Code = [int]$started.Process.ExitCode; Text = $text; Stdout = $stdoutText; Stderr = $stderrText; StdoutPath = $started.StdoutPath; StderrPath = $started.StderrPath; Json = $json }
}

function Wait-AsyncMarker([pscustomobject]$started, [string]$markerPath, [int]$timeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    do {
        if (Test-Path -LiteralPath $markerPath -PathType Leaf) { return $true }
        $started.Process.Refresh()
        if ($started.Process.HasExited) { return $false }
        Start-Sleep -Milliseconds 50
    } while ((Get-Date) -lt $deadline)
    return (Test-Path -LiteralPath $markerPath -PathType Leaf)
}

function Save-CommandReport([object]$result) {
    $safe = [ordered]@{ name = $result.Name; exitCode = $result.Code; report = $result.Json; capturedOutput = $result.Text }
    $reportPath = Save-JsonReport ('command-' + $result.Name.ToLowerInvariant().Replace('_', '-')) $safe
    $reportStem = [IO.Path]::GetFileNameWithoutExtension($reportPath)
    foreach ($stream in @(
        [ordered]@{ suffix = 'stdout.log'; path = [string]$result.StdoutPath },
        [ordered]@{ suffix = 'stderr.log'; path = [string]$result.StderrPath }
    )) {
        if (-not [string]::IsNullOrWhiteSpace($stream.path) -and (Test-Path -LiteralPath $stream.path -PathType Leaf)) {
            Copy-Item -LiteralPath $stream.path -Destination (Join-Path $runEvidence ($reportStem + '.' + $stream.suffix)) -Force
        }
    }
}

function Preserve-FailureArtifacts {
    if (-not $script:testFailed -or -not (Test-Path -LiteralPath $runEvidence -PathType Container)) { return }
    $failureRoot = Join-Path $runEvidence 'failure-artifacts'
    $ownedRootCopy = Join-Path $failureRoot 'owned-temp-root'
    $copyStatus = 'NOT_COPIED'
    $copyError = $null
    New-Item -ItemType Directory -Path $failureRoot -Force | Out-Null
    try {
        if (Test-Path -LiteralPath $root -PathType Container) {
            Copy-Item -LiteralPath $root -Destination $ownedRootCopy -Recurse -Force -ErrorAction Stop
            $copyStatus = 'COPIED'
        } else {
            $copyStatus = 'SOURCE_ROOT_ABSENT'
        }
    } catch {
        $copyStatus = 'COPY_FAILED'
        $copyError = [string]$_.Exception
    }
    [IO.File]::WriteAllText((Join-Path $failureRoot 'README.md'), @"
This directory preserves the complete owned temporary root before test cleanup.
It includes GUI smoke screenshots, GUI smoke reports/logs, child stdout/stderr logs,
and any owned transaction evidence available at the time of failure.
status=$copyStatus
sourceRoot=$root
"@, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $runEvidence 'failure-artifacts.json'), ([ordered]@{
        schema = 'arcgis-pro-mcp-one-click-failure-artifacts-v1'
        status = $copyStatus
        sourceRoot = $root
        destination = $ownedRootCopy
        errorCode = $script:failureCode
        message = $script:failureMessage
        assertions = $passed
        copyError = $copyError
    } | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
}

function Get-FileDigest([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    $item = Get-Item -LiteralPath $path
    return [ordered]@{ path = $path; sizeBytes = [int64]$item.Length; sha256 = (Get-Sha256Hex $path) }
}

function Get-Sha256Hex([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($hasher.ComputeHash($stream)) -replace '-', '') }
    finally {
        $hasher.Dispose()
        $stream.Dispose()
    }
}

function Get-ExactPackageTarget([string]$installRoot) {
    return Join-Path $installRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}\ArcGISProMCP.Compatibility.esriAddInX'
}

function Get-RecoveryIndexEntry([string]$stateRoot, [bool]$recoverableOnly = $false) {
    $indexPath = Join-Path $stateRoot 'recovery-index.json'
    if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) { return $null }
    $index = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $entries = @($index.entries)
    if ($recoverableOnly) { $entries = @($entries | Where-Object { [bool]$_.recoverable }) }
    if ($entries.Count -eq 0) { return $null }
    return $entries[$entries.Count - 1]
}

try {
    New-Item -ItemType Directory -Path $runEvidence -Force | Out-Null
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    Assert-That (Test-Path -LiteralPath $zip -PathType Leaf) 'r5 one-click ZIP is present'
    Assert-That (Test-Path -LiteralPath $verifier -PathType Leaf) 'bundle verifier is present'

    $newHash = Get-FileDigest $zip
    $oldHash = Get-FileDigest $oldZip
    Assert-That ([int64]$newHash.sizeBytes -eq 331679) 'r5 ZIP size matches the built candidate'
    Assert-That ($newHash.sha256 -ceq '3924F9B3FD122A171A3C15CCFD3A0B4AA8636E6F8DFA6DBF34169A33229AB652') 'r5 ZIP hash matches the built candidate'
    Assert-That ($oldHash.sha256 -ceq '3303E02CFAC50EB02B74F4C0B4BDA77C3BE2788607920EE1402B43B19102D7B1') 'r1 ZIP hash remains unchanged'
    Assert-That ([int64]$oldHash.sizeBytes -eq 316207) 'r1 ZIP size remains unchanged'
    Save-JsonReport 'package-hash' ([ordered]@{
        schema = 'arcgis-pro-mcp-one-click-package-hash-v1'
        candidate = $newHash
        candidateDeploymentVersion = 'one-click-1.0.2-r5'
        acceptedPayloadSha256 = '361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A'
        acceptedPayloadSizeBytes = 269548
        retainedR1 = $oldHash
        retainedR1ExpectedSha256 = '3303E02CFAC50EB02B74F4C0B4BDA77C3BE2788607920EE1402B43B19102D7B1'
    }) | Out-Null

    $zipReport = Invoke-Json 'verify-zip' $verifier @('-ZipPath', $zip, '-Json')
    Save-CommandReport $zipReport
    Assert-That ($zipReport.Code -eq 0 -and $zipReport.Json.status -eq 'PASS' -and $zipReport.Json.source -eq 'ZIP' -and $zipReport.Json.deploymentVersion -eq 'one-click-1.0.2-r5') 'r5 ZIP full-file audit passes'

    $extract = Join-Path $root '中文 空格 解压目录'
    Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
    $rootReport = Invoke-Json 'verify-extracted-root' $verifier @('-RootPath', $extract, '-Json')
    Save-CommandReport $rootReport
    Assert-That ($rootReport.Code -eq 0 -and $rootReport.Json.status -eq 'PASS' -and $rootReport.Json.source -eq 'EXTRACTED_ROOT') 'r5 extracted bundle full-file audit passes'

    $plan1 = Invoke-Json 'plan-1' $oneClick @('-Action', 'Plan', '-BundleRoot', $extract, '-Json')
    $plan2 = Invoke-Json 'plan-2' $oneClick @('-Action', 'Plan', '-BundleRoot', $extract, '-Json')
    Save-CommandReport $plan1; Save-CommandReport $plan2
    Assert-That ($plan1.Code -eq 0 -and $plan1.Json.status -eq 'PASS' -and $plan1.Json.noApplyAll -eq $true) 'Plan is read-only and excludes ApplyAll'
    Assert-That ($plan2.Code -eq 0 -and $plan2.Json.status -eq 'PASS') 'repeated Plan is idempotent'

    $bundleAction = Invoke-Json 'validate-bundle' $oneClick @('-Action', 'ValidateBundle', '-BundleRoot', $extract, '-Json')
    Save-CommandReport $bundleAction
    Assert-That ($bundleAction.Code -eq 0 -and $bundleAction.Json.status -eq 'PASS' -and $bundleAction.Json.report.source -eq 'EXTRACTED_ROOT') 'CLI bundle validation passes on extracted r5 root'

    $ownedInstall = Join-Path $root 'owned install root'
    $ownedState = Join-Path $root 'owned installer state'
    $preflightBefore = @(Get-ChildItem -LiteralPath $root -Force -Recurse -File -ErrorAction SilentlyContinue).Count
    $preflight = Invoke-Json 'owned-readonly-preflight' $oneClick @('-Action', 'Preflight', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $ownedInstall, '-InstallerStateRoot', $ownedState, '-TestMode', '-Json')
    Save-CommandReport $preflight
    Assert-That ($preflight.Code -eq 0 -and $preflight.Json.status -eq 'PASS') 'owned-temp read-only preflight passes'
    Assert-That (-not (Test-Path -LiteralPath $ownedState)) 'read-only preflight does not create installer state'
    Assert-That (-not (Test-Path -LiteralPath $ownedInstall)) 'read-only preflight does not create a missing install root'
    $preflightAfter = @(Get-ChildItem -LiteralPath $root -Force -Recurse -File -ErrorAction SilentlyContinue).Count
    Assert-That ($preflightAfter -eq $preflightBefore) 'read-only preflight file inventory is unchanged'

    $cancelState = Join-Path $root 'cancel-state'
    $cancelInstall = Join-Path $root 'cancel-install'
    $cancel = Invoke-Json 'cancel-before-mutation' $oneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $cancelInstall, '-InstallerStateRoot', $cancelState, '-DryRun', '-NoWait', '-TestMode', '-TestCancelAfterStep', 'bundle-integrity', '-Json')
    Save-CommandReport $cancel
    Assert-That ($cancel.Code -eq 1 -and $cancel.Json.status -eq 'CANCELLED') 'test-only cancellation stops before mutation'
    Assert-That (-not (Test-Path -LiteralPath $cancelState) -and -not (Test-Path -LiteralPath $cancelInstall)) 'cancelled pre-mutation flow leaves no owned targets'

    $missingProject = Join-Path $root 'missing project'
    $blockedState = Join-Path $root 'blocked-state'
    $blockedDeploy = Invoke-Json 'missing-project-block' $oneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-SelectedClients', 'codex', '-ProjectRoot', $missingProject, '-InstallRoot', (Join-Path $root 'blocked-install'), '-InstallerStateRoot', $blockedState, '-DryRun', '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $blockedDeploy
    Assert-That ($blockedDeploy.Code -eq 1 -and $blockedDeploy.Json.status -eq 'FAILED' -and $blockedDeploy.Json.errorCode -eq 'PROJECT_ROOT_NOT_FOUND') 'client preflight blocks mutation when project target is missing'
    Assert-That (-not (Test-Path -LiteralPath $blockedState)) 'blocked client preflight creates no installer state'

    $multiState = Join-Path $root 'multi-state'
    $multi = Invoke-Json 'multi-client-rejection' $oneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-SelectedClients', 'codex,cursor', '-InstallRoot', (Join-Path $root 'multi-install'), '-InstallerStateRoot', $multiState, '-DryRun', '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $multi
    Assert-That ($multi.Code -eq 1 -and $multi.Json.status -eq 'FAIL' -and $multi.Json.errorCode -eq 'MULTIPLE_CLIENTS_NOT_ALLOWED') 'multi-client mutation is rejected before work'
    Assert-That (-not (Test-Path -LiteralPath $multiState)) 'multi-client rejection creates no state'

    $unknownState = Join-Path $root 'unknown-state'
    $unknown = Invoke-Json 'unknown-client-rejection' $oneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-SelectedClients', 'not-a-client', '-InstallRoot', (Join-Path $root 'unknown-install'), '-InstallerStateRoot', $unknownState, '-DryRun', '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $unknown
    Assert-That ($unknown.Code -eq 1 -and $unknown.Json.status -eq 'FAIL' -and $unknown.Json.errorCode -eq 'CLIENT_NOT_SUPPORTED') 'unknown client is rejected before work'
    Assert-That (-not (Test-Path -LiteralPath $unknownState)) 'unknown-client rejection creates no state'

    $deepInstall = Join-Path $root 'deepseek-install'
    $deepState = Join-Path $root 'deepseek-state'
    $deepPreflight = Invoke-Json 'deepseek-no-project-preflight' $oneClick @('-Action', 'Preflight', '-BundleRoot', $extract, '-SelectedClients', 'deepseek-harness', '-InstallRoot', $deepInstall, '-InstallerStateRoot', $deepState, '-TestMode', '-Json')
    Save-CommandReport $deepPreflight
    Assert-That ($deepPreflight.Code -eq 0 -and $deepPreflight.Json.status -eq 'PASS' -and $deepPreflight.Json.errorCode -ne 'PROJECT_ROOT_NOT_FOUND') 'DeepSeek preflight does not require a project directory'
    $deepDryRun = Invoke-Json 'deepseek-no-project-dry-run' $oneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-SelectedClients', 'deepseek-harness', '-InstallRoot', $deepInstall, '-InstallerStateRoot', $deepState, '-DryRun', '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $deepDryRun
    Assert-That ($deepDryRun.Code -eq 0 -and $deepDryRun.Json.status -eq 'DRY_RUN_PASS' -and $deepDryRun.Json.errorCode -ne 'PROJECT_ROOT_NOT_FOUND') 'DeepSeek dry-run without a project directory passes'
    Assert-That (-not (Test-Path -LiteralPath $deepInstall)) 'DeepSeek dry-run does not create an install root'

    $projectRoot = Join-Path $root 'owned project'
    New-Item -ItemType Directory -Path $projectRoot -Force | Out-Null
    $codexConfig = Join-Path $projectRoot '.codex\config.toml'
    $codexBackup = $codexConfig + '.arcgis-pro-mcp.bak'
    $codexMetadata = $codexBackup + '.json'
    Assert-That (-not (Test-Path -LiteralPath $codexConfig)) 'owned Codex baseline config is absent'

    $deploy = Invoke-Json 'owned-codex-deploy' $oneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-SelectedClients', 'codex', '-ProjectRoot', $projectRoot, '-InstallRoot', $ownedInstall, '-InstallerStateRoot', $ownedState, '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $deploy
    Assert-That ($deploy.Code -eq 0 -and $deploy.Json.status -eq 'WAITING_USER_START') 'owned TestMode deployment stops at explicit user-start gate'
    Assert-That ($deploy.Json.pluginInstall -eq 'PASS' -and $deploy.Json.clientConfiguration -eq 'PASS') 'owned plugin and Codex transactions pass'
    Assert-That (Test-Path -LiteralPath (Join-Path $ownedState 'one-click-state.json') -PathType Leaf) 'one-click state is recorded after deployment'
    Assert-That (Test-Path -LiteralPath (Join-Path $ownedState 'one-click-operation.jsonl') -PathType Leaf) 'sanitized one-click operation log is recorded'
    Assert-That (Test-Path -LiteralPath (Get-ExactPackageTarget $ownedInstall) -PathType Leaf) 'owned install target contains the accepted add-in package'
    Assert-That ((Get-Sha256Hex (Get-ExactPackageTarget $ownedInstall)) -ceq '361FC84FB43973831D3BDE03BF468257C03BD022C6C64D1200E24B129E436A2A') 'owned installed package preserves accepted payload hash'
    Assert-That (Test-Path -LiteralPath $codexConfig -PathType Leaf) 'owned Codex config was written only inside the temp project'
    Assert-That (Test-Path -LiteralPath $codexMetadata -PathType Leaf) 'owned Codex backup metadata was written'
    Assert-That (-not (Test-Path -LiteralPath $codexBackup -PathType Leaf)) 'absent Codex baseline has no content backup file'
    $configText = Get-Content -LiteralPath $codexConfig -Raw -Encoding UTF8
    Assert-That ($configText -match 'http://127\.0\.0\.1:6520/mcp' -and $configText -notmatch '(?i)token|password|secret|apikey|authorization') 'owned Codex config has the canonical endpoint without credentials'
    $oneClickStatePath = Join-Path $ownedState 'one-click-state.json'
    $stateAfterDeploy = Get-Content -LiteralPath $oneClickStatePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $firstTransactionRoot = [string]$stateAfterDeploy.plugin.transactionRoot
    $firstLedgerPath = [string]$stateAfterDeploy.plugin.ledgerPath
    Assert-That ($stateAfterDeploy.plugin.status -eq 'PASS' -and $stateAfterDeploy.client.status -eq 'PASS' -and $stateAfterDeploy.diagnosis.status -eq 'WAITING_USER_START') 'owned state records plugin/client pass and waiting diagnosis'
    Assert-That (Test-Path -LiteralPath $firstLedgerPath -PathType Leaf) 'owned release ledger is retained'
    $packageBeforeRetry = Get-FileDigest (Get-ExactPackageTarget $ownedInstall)
    $configBeforeRetry = Get-FileDigest $codexConfig

    $retry = Invoke-Json 'retry-diagnosis-only' $oneClick @('-Action', 'Retry', '-BundleRoot', $extract, '-InstallerStateRoot', $ownedState, '-TestMode', '-Json')
    Save-CommandReport $retry
    Assert-That ($retry.Code -eq 0 -and $retry.Json.status -eq 'WAITING_USER_START' -and $retry.Json.retryMode -eq 'DIAGNOSE_ONLY') 'Retry only repeats loopback diagnosis'
    Assert-That ([IO.Path]::GetFullPath([string]$retry.Json.transactionRoot) -eq [IO.Path]::GetFullPath($firstTransactionRoot) -and [IO.Path]::GetFullPath([string]$retry.Json.ledgerPath) -eq [IO.Path]::GetFullPath($firstLedgerPath)) 'Retry keeps the same transaction and ledger'
    Assert-That ((Get-FileDigest (Get-ExactPackageTarget $ownedInstall)).sha256 -ceq $packageBeforeRetry.sha256 -and (Get-FileDigest $codexConfig).sha256 -ceq $configBeforeRetry.sha256) 'Retry does not change install or client inventory'

    $restore = Invoke-Json 'codex-client-restore' $oneClick @('-Action', 'Recover', '-RecoveryAction', 'ClientRestore', '-BundleRoot', $extract, '-InstallRoot', $ownedInstall, '-InstallerStateRoot', $ownedState, '-TestMode', '-Json')
    Save-CommandReport $restore
    Assert-That ($restore.Code -eq 0 -and $restore.Json.status -eq 'PASS' -and $restore.Json.client -eq 'codex') 'owned Codex ClientRestore passes'
    Assert-That (-not (Test-Path -LiteralPath $codexConfig) -and (Test-Path -LiteralPath $codexMetadata)) 'ClientRestore removes the owned config and retains restore metadata'
    Assert-That (Test-Path -LiteralPath (Get-ExactPackageTarget $ownedInstall) -PathType Leaf) 'ClientRestore does not touch the plugin install target'

    $rollback = Invoke-Json 'plugin-rollback' $oneClick @('-Action', 'Recover', '-RecoveryAction', 'Rollback', '-BundleRoot', $extract, '-InstallRoot', $ownedInstall, '-InstallerStateRoot', $ownedState, '-TestMode', '-Json')
    Save-CommandReport $rollback
    Assert-That ($rollback.Code -eq 0 -and $rollback.Json.status -eq 'PASS' -and $rollback.Json.action -eq 'Rollback') 'owned plugin rollback passes'
    Assert-That (-not (Test-Path -LiteralPath (Get-ExactPackageTarget $ownedInstall))) 'rollback removes the owned plugin target to baseline'
    Assert-That ((Test-Path -LiteralPath $firstTransactionRoot -PathType Container) -and (Test-Path -LiteralPath $firstLedgerPath -PathType Leaf)) 'rollback retains the transaction directory and ledger'

    $deployAgain = Invoke-Json 'owned-codex-deploy-after-recovery' $oneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-SelectedClients', 'codex', '-ProjectRoot', $projectRoot, '-InstallRoot', $ownedInstall, '-InstallerStateRoot', $ownedState, '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $deployAgain
    Assert-That ($deployAgain.Code -eq 0 -and $deployAgain.Json.status -eq 'WAITING_USER_START' -and $deployAgain.Json.pluginInstall -eq 'PASS' -and $deployAgain.Json.clientConfiguration -eq 'PASS') 'explicit deployment after recovery starts a fresh owned transaction'
    $stateAfterSecondDeploy = Get-Content -LiteralPath $oneClickStatePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $secondTransactionRoot = [string]$stateAfterSecondDeploy.plugin.transactionRoot
    Assert-That ([IO.Path]::GetFullPath($secondTransactionRoot) -ne [IO.Path]::GetFullPath($firstTransactionRoot)) 'post-recovery deployment uses a new transaction root'
    Assert-That ((Test-Path -LiteralPath (Get-ExactPackageTarget $ownedInstall) -PathType Leaf) -and (Test-Path -LiteralPath $codexConfig -PathType Leaf)) 'post-recovery deployment restores both owned targets'
    Assert-That ([string]$stateAfterSecondDeploy.plugin.status -eq 'PASS' -and [string]$stateAfterSecondDeploy.client.status -eq 'PASS') 'post-recovery state is healthy'

    $extractedOneClick = Join-Path $extract 'scripts\one-click-setup.ps1'
    $installFailureRoot = Join-Path $root 'install-failure install'
    $installFailureState = Join-Path $root 'install-failure state'
    $installFailure = Invoke-Json 'recovery-install-child-failure' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $installFailureRoot, '-InstallerStateRoot', $installFailureState, '-NoWait', '-TestMode', '-TestInjectChildFailure', '-Json')
    Save-CommandReport $installFailure
    Assert-That ($installFailure.Code -eq 1 -and $installFailure.Json.status -eq 'FAILED' -and $installFailure.Json.errorCode -eq 'TEST_INJECTED_FAILURE_AFTER_INSTALL') 'isolated install transaction child failure is surfaced after mutation'
    $installFailureEntry = Get-RecoveryIndexEntry $installFailureState $true
    Assert-That ($null -ne $installFailureEntry -and $installFailureEntry.status -eq 'FAIL' -and [bool]$installFailureEntry.recoverable -and (Test-Path -LiteralPath ([string]$installFailureEntry.ledgerPath) -PathType Leaf)) 'install failure keeps a recoverable index and ledger'
    $installFailureLedger = Get-Content -LiteralPath ([string]$installFailureEntry.ledgerPath) -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $installFailureRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}')) -and $installFailureLedger.recoveryAttempted -eq $true) 'install failure automatic rollback reaches baseline while retaining a recoverable ledger'
    $installFailureRepeat = Invoke-Json 'recovery-install-child-failure-repeat-block' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $installFailureRoot, '-InstallerStateRoot', $installFailureState, '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $installFailureRepeat
    Assert-That ($installFailureRepeat.Code -eq 1 -and $installFailureRepeat.Json.errorCode -eq 'EXISTING_DEPLOYMENT_REQUIRES_RECOVERY' -and [IO.Path]::GetFullPath([string]$installFailureRepeat.Json.transactionRoot) -eq [IO.Path]::GetFullPath([string]$installFailureEntry.transactionRoot)) 'failed install cannot overwrite its recoverable transaction'
    $installFailureRecovery = Invoke-Json 'recovery-install-child-failure-rollback' $extractedOneClick @('-Action', 'Recover', '-RecoveryAction', 'Rollback', '-BundleRoot', $extract, '-InstallRoot', $installFailureRoot, '-InstallerStateRoot', $installFailureState, '-TestMode', '-Json')
    Save-CommandReport $installFailureRecovery
    Assert-That ($installFailureRecovery.Code -eq 0 -and $installFailureRecovery.Json.status -eq 'PASS' -and $installFailureRecovery.Json.recoverySource -eq 'recovery-index' -and [IO.Path]::GetFullPath([string]$installFailureRecovery.Json.transactionRoot) -eq [IO.Path]::GetFullPath([string]$installFailureEntry.transactionRoot)) 'install failure rollback uses the indexed failed transaction'
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $installFailureRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'))) 'install failure rollback returns the owned target to baseline'

    $installFailureRetry = Invoke-Json 'recovery-install-child-failure-fresh-deploy' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $installFailureRoot, '-InstallerStateRoot', $installFailureState, '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $installFailureRetry
    Assert-That ($installFailureRetry.Code -eq 0 -and $installFailureRetry.Json.status -eq 'WAITING_USER_START' -and [IO.Path]::GetFullPath([string]$installFailureRetry.Json.transactionRoot) -ne [IO.Path]::GetFullPath([string]$installFailureEntry.transactionRoot)) 'deployment after recovery creates a fresh transaction'
    $installFailureCleanup = Invoke-Json 'recovery-install-child-failure-fresh-rollback' $extractedOneClick @('-Action', 'Recover', '-RecoveryAction', 'Rollback', '-BundleRoot', $extract, '-InstallRoot', $installFailureRoot, '-InstallerStateRoot', $installFailureState, '-TestMode', '-Json')
    Save-CommandReport $installFailureCleanup
    Assert-That ($installFailureCleanup.Code -eq 0 -and $installFailureCleanup.Json.status -eq 'PASS' -and -not (Test-Path -LiteralPath (Join-Path $installFailureRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'))) 'fresh recovered transaction can be safely cleaned up'

    $stateWriteRoot = Join-Path $root 'state-write-failure install'
    $stateWriteState = Join-Path $root 'state-write-failure state'
    $stateWrite = Invoke-Json 'recovery-state-write-failure' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $stateWriteRoot, '-InstallerStateRoot', $stateWriteState, '-NoWait', '-TestMode', '-TestInjectStateWriteFailureAt', 'plugin-install', '-Json')
    Save-CommandReport $stateWrite
    Assert-That ($stateWrite.Code -eq 1 -and $stateWrite.Json.status -eq 'FAIL' -and $stateWrite.Json.errorCode -eq 'TEST_INJECTED_ONE_CLICK_STATE_WRITE_FAILURE') 'state-write failure is surfaced as a controlled failure'
    $stateWriteEntry = Get-RecoveryIndexEntry $stateWriteState $true
    Assert-That ($null -ne $stateWriteEntry -and $stateWriteEntry.status -eq 'PASS' -and [bool]$stateWriteEntry.recoverable -and (Test-Path -LiteralPath ([string]$stateWriteEntry.ledgerPath) -PathType Leaf)) 'state-write failure preserves the recovery index independently of one-click state'
    $stateWriteRepeat = Invoke-Json 'recovery-state-write-failure-repeat-block' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $stateWriteRoot, '-InstallerStateRoot', $stateWriteState, '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $stateWriteRepeat
    Assert-That ($stateWriteRepeat.Code -eq 1 -and $stateWriteRepeat.Json.errorCode -eq 'EXISTING_DEPLOYMENT_REQUIRES_RECOVERY' -and [IO.Path]::GetFullPath([string]$stateWriteRepeat.Json.transactionRoot) -eq [IO.Path]::GetFullPath([string]$stateWriteEntry.transactionRoot)) 'state-write failure cannot be overwritten by a new deployment'
    $stateWriteRecovery = Invoke-Json 'recovery-state-write-failure-rollback' $extractedOneClick @('-Action', 'Recover', '-RecoveryAction', 'Rollback', '-BundleRoot', $extract, '-InstallRoot', $stateWriteRoot, '-InstallerStateRoot', $stateWriteState, '-TestMode', '-Json')
    Save-CommandReport $stateWriteRecovery
    Assert-That ($stateWriteRecovery.Code -eq 0 -and $stateWriteRecovery.Json.status -eq 'PASS' -and $stateWriteRecovery.Json.recoverySource -eq 'recovery-index' -and [IO.Path]::GetFullPath([string]$stateWriteRecovery.Json.transactionRoot) -eq [IO.Path]::GetFullPath([string]$stateWriteEntry.transactionRoot)) 'state-write failure rollback uses the preserved indexed transaction'
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $stateWriteRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'))) 'state-write failure rollback returns the owned target to baseline'

    $cancelAfterInstallRoot = Join-Path $root 'cancel-after-install root'
    $cancelAfterInstallState = Join-Path $root 'cancel-after-install state'
    $cancelAfterInstall = Invoke-Json 'recovery-cancel-after-plugin-install' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $cancelAfterInstallRoot, '-InstallerStateRoot', $cancelAfterInstallState, '-NoWait', '-TestMode', '-TestCancelAfterStep', 'plugin-install', '-Json')
    Save-CommandReport $cancelAfterInstall
    Assert-That ($cancelAfterInstall.Code -eq 1 -and $cancelAfterInstall.Json.status -eq 'CANCELLED' -and $cancelAfterInstall.Json.pluginInstall -eq 'PASS') 'cancellation after plugin mutation is returned at the safe boundary'
    $cancelEntry = Get-RecoveryIndexEntry $cancelAfterInstallState $true
    Assert-That ($null -ne $cancelEntry -and $cancelEntry.status -eq 'CANCELLED' -and [bool]$cancelEntry.recoverable -and (Test-Path -LiteralPath (Join-Path $cancelAfterInstallRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}') -PathType Container)) 'cancelled post-install flow keeps a recoverable target and index entry'
    $cancelRepeat = Invoke-Json 'recovery-cancel-after-plugin-install-repeat-block' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $cancelAfterInstallRoot, '-InstallerStateRoot', $cancelAfterInstallState, '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $cancelRepeat
    Assert-That ($cancelRepeat.Code -eq 1 -and $cancelRepeat.Json.errorCode -eq 'EXISTING_DEPLOYMENT_REQUIRES_RECOVERY' -and [IO.Path]::GetFullPath([string]$cancelRepeat.Json.transactionRoot) -eq [IO.Path]::GetFullPath([string]$cancelEntry.transactionRoot)) 'cancelled post-install flow cannot be duplicated before recovery'
    $cancelRecovery = Invoke-Json 'recovery-cancel-after-plugin-install-rollback' $extractedOneClick @('-Action', 'Recover', '-RecoveryAction', 'Rollback', '-BundleRoot', $extract, '-InstallRoot', $cancelAfterInstallRoot, '-InstallerStateRoot', $cancelAfterInstallState, '-TestMode', '-Json')
    Save-CommandReport $cancelRecovery
    Assert-That ($cancelRecovery.Code -eq 0 -and $cancelRecovery.Json.status -eq 'PASS' -and $cancelRecovery.Json.recoverySource -eq 'recovery-index' -and [IO.Path]::GetFullPath([string]$cancelRecovery.Json.transactionRoot) -eq [IO.Path]::GetFullPath([string]$cancelEntry.transactionRoot)) 'cancelled post-install rollback uses the indexed transaction'
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $cancelAfterInstallRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'))) 'cancelled post-install rollback returns the owned target to baseline'

    $callbackInstallRoot = Join-Path $root 'cancel-callback install'
    $callbackStateRoot = Join-Path $root 'cancel-callback state'
    $callbackReadyPath = Join-Path $root 'cancel-callback ready.flag'
    $callbackTokenPath = Join-Path $root 'cancel-callback token.flag'
    $callbackStdoutPath = Join-Path $root 'cancel-callback stdout.txt'
    $callbackStderrPath = Join-Path $root 'cancel-callback stderr.txt'
    $callbackStarted = Start-AsyncOneClick 'recovery-cancel-real-callback-start' $extractedOneClick @('-Action', 'GuiWorker', '-WorkerMode', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $callbackInstallRoot, '-InstallerStateRoot', $callbackStateRoot, '-NoWait', '-TestMode', '-CancelTokenPath', $callbackTokenPath, '-TestCancelCallbackReadyPath', $callbackReadyPath, '-Json') $callbackStdoutPath $callbackStderrPath
    Assert-That (Wait-AsyncMarker $callbackStarted $callbackReadyPath) 'real cancellation callback reaches the post-plugin callback gate'
    $callbackStarted.Process.Refresh()
    Assert-That (-not $callbackStarted.Process.HasExited) 'real cancellation callback test remains owned and waiting at the callback gate'
    [IO.File]::WriteAllText($callbackTokenPath, 'cancel', [Text.UTF8Encoding]::new($false))
    $callbackResult = Complete-AsyncOneClick $callbackStarted
    Save-CommandReport $callbackResult
    Assert-That ($callbackResult.Code -eq 0 -and $callbackResult.Json.status -eq 'CANCELLED' -and $callbackResult.Json.pluginInstall -eq 'PASS') 'real cancel callback cancels after PluginOnly installation completes'
    $callbackEntry = Get-RecoveryIndexEntry $callbackStateRoot $true
    Assert-That ($null -ne $callbackEntry -and $callbackEntry.status -eq 'CANCELLED' -and $callbackEntry.recoveryState -eq 'AVAILABLE' -and [bool]$callbackEntry.recoverable -and (Test-Path -LiteralPath (Join-Path $callbackInstallRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}') -PathType Container)) 'real cancel callback preserves the installed target and recoverable index entry'
    $callbackRecovery = Invoke-Json 'recovery-cancel-real-callback-rollback' $extractedOneClick @('-Action', 'Recover', '-RecoveryAction', 'Rollback', '-BundleRoot', $extract, '-InstallRoot', $callbackInstallRoot, '-InstallerStateRoot', $callbackStateRoot, '-TestMode', '-Json')
    Save-CommandReport $callbackRecovery
    Assert-That ($callbackRecovery.Code -eq 0 -and $callbackRecovery.Json.status -eq 'PASS' -and $callbackRecovery.Json.recoverySource -eq 'recovery-index') 'real cancel callback transaction is recoverable after process completion'
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $callbackInstallRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'))) 'real cancel callback rollback returns the owned target to baseline'

    $crashInstallRoot = Join-Path $root 'hard-interrupt install'
    $crashStateRoot = Join-Path $root 'hard-interrupt state'
    $seed = Invoke-Json 'recovery-hard-interrupt-seed-deploy' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $crashInstallRoot, '-InstallerStateRoot', $crashStateRoot, '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $seed
    Assert-That ($seed.Code -eq 0 -and $seed.Json.status -eq 'WAITING_USER_START') 'hard-interrupt recovery scenario seeds an owned completed transaction'
    $seedState = Get-Content -LiteralPath (Join-Path $crashStateRoot 'one-click-state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $seedTransactionRoot = [string]$seedState.plugin.transactionRoot
    $seedRollback = Invoke-Json 'recovery-hard-interrupt-seed-rollback' $extractedOneClick @('-Action', 'Recover', '-RecoveryAction', 'Rollback', '-BundleRoot', $extract, '-InstallRoot', $crashInstallRoot, '-InstallerStateRoot', $crashStateRoot, '-TestMode', '-Json')
    Save-CommandReport $seedRollback
    Assert-That ($seedRollback.Code -eq 0 -and $seedRollback.Json.status -eq 'PASS' -and $seedRollback.Json.recoverySource -eq 'recovery-index') 'hard-interrupt scenario rolls back the seed transaction through the index'
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $crashInstallRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'))) 'hard-interrupt seed rollback returns to baseline'
    $legacyBeforeCrash = Get-Content -LiteralPath (Join-Path $crashStateRoot 'latest-transaction.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-That ([IO.Path]::GetFullPath([string]$legacyBeforeCrash.transactionRoot) -eq [IO.Path]::GetFullPath($seedTransactionRoot)) 'hard-interrupt scenario retains an older latest-transaction reference'

    $crashReadyPath = Join-Path $root 'hard-interrupt ready.flag'
    $crashStdoutPath = Join-Path $root 'hard-interrupt stdout.txt'
    $crashStderrPath = Join-Path $root 'hard-interrupt stderr.txt'
    $crashStarted = Start-AsyncOneClick 'recovery-hard-interrupt-start' $extractedOneClick @('-Action', 'GuiWorker', '-WorkerMode', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $crashInstallRoot, '-InstallerStateRoot', $crashStateRoot, '-NoWait', '-TestMode', '-TestInterruptAfterPluginChildReadyPath', $crashReadyPath, '-Json') $crashStdoutPath $crashStderrPath
    Assert-That (Wait-AsyncMarker $crashStarted $crashReadyPath) 'hard-interrupt test reaches the boundary after child installation and ledger completion'
    $crashReadyText = Get-Content -LiteralPath $crashReadyPath -Raw -Encoding UTF8
    Assert-That ($crashReadyText -match 'READY\|plugin-install-child-completed\|pid=') 'hard-interrupt boundary identifies the completed plugin child step'
    $crashStarted.Process.Refresh()
    Assert-That (-not $crashStarted.Process.HasExited) 'hard-interrupt parent process is alive at the controlled boundary'
    $ownedCrashProcess = Get-Process -Id $crashStarted.Process.Id -ErrorAction Stop
    Assert-That ($ownedCrashProcess.ProcessName -ieq 'powershell') 'hard-interrupt target is the exact self-owned PowerShell test process'
    Stop-Process -Id $crashStarted.Process.Id -Force -ErrorAction Stop
    $crashResult = Complete-AsyncOneClick $crashStarted
    Save-CommandReport $crashResult
    Assert-That ($null -eq $crashResult.Json -and [string]::IsNullOrWhiteSpace([string]$crashResult.Stdout)) 'hard-interrupt terminates only the self-owned parent before its JSON completion'
    $crashEntry = Get-RecoveryIndexEntry $crashStateRoot $true
    Assert-That ($null -ne $crashEntry -and $crashEntry.status -eq 'STARTED' -and [bool]$crashEntry.recoverable -and $crashEntry.recoveryState -eq 'PENDING' -and (Test-Path -LiteralPath ([string]$crashEntry.ledgerPath) -PathType Leaf) -and (Test-Path -LiteralPath (Join-Path $crashInstallRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}') -PathType Container)) 'hard-interrupt preserves a pending recoverable index entry, ledger, and installed target'
    $crashLedger = Get-Content -LiteralPath ([string]$crashEntry.ledgerPath) -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-That ($crashLedger.status -eq 'PASS' -and @($crashLedger.events | Where-Object { $_.name -eq 'install' }).Count -gt 0 -and @($crashLedger.events | Where-Object { $_.name -eq 'install-verify' }).Count -gt 0) 'hard-interrupt ledger proves the child install completed before the parent stopped'
    $crashOneClickState = Get-Content -LiteralPath (Join-Path $crashStateRoot 'one-click-state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-That ($crashOneClickState.plugin.status -eq 'INSTALLING' -and [IO.Path]::GetFullPath([string]$crashOneClickState.plugin.transactionRoot) -eq [IO.Path]::GetFullPath([string]$crashEntry.transactionRoot)) 'one-click state remains a reliable in-progress guard after parent interruption'
    $legacyAfterCrash = Get-Content -LiteralPath (Join-Path $crashStateRoot 'latest-transaction.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-That ([IO.Path]::GetFullPath([string]$legacyAfterCrash.transactionRoot) -eq [IO.Path]::GetFullPath($seedTransactionRoot) -and [IO.Path]::GetFullPath([string]$legacyAfterCrash.transactionRoot) -ne [IO.Path]::GetFullPath([string]$crashEntry.transactionRoot)) 'hard-interrupt leaves the older latest-transaction reference distinct from the pending index entry'
    $crashRepeat = Invoke-Json 'recovery-hard-interrupt-restart-block' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $crashInstallRoot, '-InstallerStateRoot', $crashStateRoot, '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $crashRepeat
    Assert-That ($crashRepeat.Code -eq 1 -and $crashRepeat.Json.errorCode -eq 'EXISTING_DEPLOYMENT_REQUIRES_RECOVERY' -and [IO.Path]::GetFullPath([string]$crashRepeat.Json.transactionRoot) -eq [IO.Path]::GetFullPath([string]$crashEntry.transactionRoot)) 'restart blocks on the interrupted pending index entry instead of falling back to older latest-transaction'
    $crashRecovery = Invoke-Json 'recovery-hard-interrupt-index-rollback' $extractedOneClick @('-Action', 'Recover', '-RecoveryAction', 'Rollback', '-BundleRoot', $extract, '-InstallRoot', $crashInstallRoot, '-InstallerStateRoot', $crashStateRoot, '-TestMode', '-Json')
    Save-CommandReport $crashRecovery
    Assert-That ($crashRecovery.Code -eq 0 -and $crashRecovery.Json.status -eq 'PASS' -and $crashRecovery.Json.recoverySource -eq 'recovery-index' -and [IO.Path]::GetFullPath([string]$crashRecovery.Json.transactionRoot) -eq [IO.Path]::GetFullPath([string]$crashEntry.transactionRoot)) 'restart recovery uses the pending index entry and its actual ledger'
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $crashInstallRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'))) 'hard-interrupt recovery returns the owned target to baseline'
    $crashIndexAfter = Get-Content -LiteralPath (Join-Path $crashStateRoot 'recovery-index.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $crashEntryAfter = @($crashIndexAfter.entries | Where-Object { [IO.Path]::GetFullPath([string]$_.transactionRoot) -eq [IO.Path]::GetFullPath([string]$crashEntry.transactionRoot) }) | Select-Object -First 1
    Assert-That ($null -ne $crashEntryAfter -and $crashEntryAfter.recoveryState -eq 'COMPLETED' -and -not [bool]$crashEntryAfter.recoverable) 'hard-interrupt index entry closes only after recovery completes'
    $crashFresh = Invoke-Json 'recovery-hard-interrupt-fresh-deploy' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $crashInstallRoot, '-InstallerStateRoot', $crashStateRoot, '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $crashFresh
    $crashFreshState = Get-Content -LiteralPath (Join-Path $crashStateRoot 'one-click-state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-That ($crashFresh.Code -eq 0 -and $crashFresh.Json.status -eq 'WAITING_USER_START' -and [IO.Path]::GetFullPath([string]$crashFreshState.plugin.transactionRoot) -ne [IO.Path]::GetFullPath([string]$crashEntry.transactionRoot) -and [IO.Path]::GetFullPath([string]$crashFreshState.plugin.transactionRoot) -ne [IO.Path]::GetFullPath($seedTransactionRoot)) 'fresh deployment after hard-interrupt recovery allocates a new transaction'
    $crashFreshRollback = Invoke-Json 'recovery-hard-interrupt-fresh-rollback' $extractedOneClick @('-Action', 'Recover', '-RecoveryAction', 'Rollback', '-BundleRoot', $extract, '-InstallRoot', $crashInstallRoot, '-InstallerStateRoot', $crashStateRoot, '-TestMode', '-Json')
    Save-CommandReport $crashFreshRollback
    Assert-That ($crashFreshRollback.Code -eq 0 -and $crashFreshRollback.Json.status -eq 'PASS' -and -not (Test-Path -LiteralPath (Join-Path $crashInstallRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'))) 'fresh hard-interrupt recovery transaction rolls back cleanly'

    $indexFailureInstallRoot = Join-Path $root 'index-write-failure install'
    $indexFailureStateRoot = Join-Path $root 'index-write-failure state'
    $indexFailureSeed = Invoke-Json 'recovery-index-write-failure-seed-deploy' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $indexFailureInstallRoot, '-InstallerStateRoot', $indexFailureStateRoot, '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $indexFailureSeed
    Assert-That ($indexFailureSeed.Code -eq 0 -and $indexFailureSeed.Json.status -eq 'WAITING_USER_START') 'recovery-index write failure scenario seeds an older completed transaction'
    $indexFailureSeedState = Get-Content -LiteralPath (Join-Path $indexFailureStateRoot 'one-click-state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $indexFailureSeedTransactionRoot = [string]$indexFailureSeedState.plugin.transactionRoot
    $indexFailureSeedRollback = Invoke-Json 'recovery-index-write-failure-seed-rollback' $extractedOneClick @('-Action', 'Recover', '-RecoveryAction', 'Rollback', '-BundleRoot', $extract, '-InstallRoot', $indexFailureInstallRoot, '-InstallerStateRoot', $indexFailureStateRoot, '-TestMode', '-Json')
    Save-CommandReport $indexFailureSeedRollback
    Assert-That ($indexFailureSeedRollback.Code -eq 0 -and $indexFailureSeedRollback.Json.status -eq 'PASS' -and $indexFailureSeedRollback.Json.recoverySource -eq 'recovery-index') 'recovery-index write failure scenario rolls back the older seed through the index'
    $indexFailureLegacyBefore = Get-Content -LiteralPath (Join-Path $indexFailureStateRoot 'latest-transaction.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-That ([IO.Path]::GetFullPath([string]$indexFailureLegacyBefore.transactionRoot) -eq [IO.Path]::GetFullPath($indexFailureSeedTransactionRoot)) 'recovery-index write failure scenario retains an older latest-transaction reference'
    $indexFailure = Invoke-Json 'recovery-index-write-failure' $extractedOneClick @('-Action', 'GuiWorker', '-WorkerMode', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $indexFailureInstallRoot, '-InstallerStateRoot', $indexFailureStateRoot, '-NoWait', '-TestMode', '-TestInjectRecoveryIndexWriteFailureAt', 'update-pass', '-Json')
    Save-CommandReport $indexFailure
    Assert-That ($indexFailure.Code -eq 1 -and $indexFailure.Json.status -eq 'FAIL' -and $indexFailure.Json.errorCode -eq 'TEST_INJECTED_RECOVERY_INDEX_WRITE_FAILURE') 'recovery-index write failure is surfaced after the child install boundary'
    $indexFailureEntry = Get-RecoveryIndexEntry $indexFailureStateRoot $true
    Assert-That ($null -ne $indexFailureEntry -and $indexFailureEntry.status -eq 'STARTED' -and [bool]$indexFailureEntry.recoverable -and $indexFailureEntry.recoveryState -eq 'PENDING' -and (Test-Path -LiteralPath ([string]$indexFailureEntry.ledgerPath) -PathType Leaf) -and (Test-Path -LiteralPath (Join-Path $indexFailureInstallRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}') -PathType Container)) 'recovery-index write failure leaves the pending entry, actual ledger, and target intact'
    $indexFailureLegacyAfterFailure = Get-Content -LiteralPath (Join-Path $indexFailureStateRoot 'latest-transaction.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-That ([IO.Path]::GetFullPath([string]$indexFailureLegacyAfterFailure.transactionRoot) -eq [IO.Path]::GetFullPath($indexFailureSeedTransactionRoot) -and [IO.Path]::GetFullPath([string]$indexFailureLegacyAfterFailure.transactionRoot) -ne [IO.Path]::GetFullPath([string]$indexFailureEntry.transactionRoot)) 'recovery-index write failure preserves the older latest reference beside the pending entry'
    $indexFailureRepeat = Invoke-Json 'recovery-index-write-failure-restart-block' $extractedOneClick @('-Action', 'Deploy', '-BundleRoot', $extract, '-PluginOnly', '-InstallRoot', $indexFailureInstallRoot, '-InstallerStateRoot', $indexFailureStateRoot, '-NoWait', '-TestMode', '-Json')
    Save-CommandReport $indexFailureRepeat
    Assert-That ($indexFailureRepeat.Code -eq 1 -and $indexFailureRepeat.Json.errorCode -eq 'EXISTING_DEPLOYMENT_REQUIRES_RECOVERY' -and [IO.Path]::GetFullPath([string]$indexFailureRepeat.Json.transactionRoot) -eq [IO.Path]::GetFullPath([string]$indexFailureEntry.transactionRoot)) 'recovery-index write failure blocks overwrite on restart'
    $indexFailureRecovery = Invoke-Json 'recovery-index-write-failure-rollback' $extractedOneClick @('-Action', 'Recover', '-RecoveryAction', 'Rollback', '-BundleRoot', $extract, '-InstallRoot', $indexFailureInstallRoot, '-InstallerStateRoot', $indexFailureStateRoot, '-TestMode', '-Json')
    Save-CommandReport $indexFailureRecovery
    Assert-That ($indexFailureRecovery.Code -eq 0 -and $indexFailureRecovery.Json.status -eq 'PASS' -and $indexFailureRecovery.Json.recoverySource -eq 'recovery-index' -and [IO.Path]::GetFullPath([string]$indexFailureRecovery.Json.transactionRoot) -eq [IO.Path]::GetFullPath([string]$indexFailureEntry.transactionRoot)) 'recovery-index write failure is recoverable through the preserved ledger'
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $indexFailureInstallRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'))) 'recovery-index write failure rollback returns the target to baseline'

    $guiOutput = Join-Path $root 'GUI smoke output with 中文 空格'
    $gui = Invoke-Json 'gui-smoke-simulated' (Join-Path $extract 'scripts\one-click-setup.ps1') @('-Action', 'GUI', '-GuiTestMode', '-GuiSmokeOutput', $guiOutput, '-PluginOnly', '-Json') -Sta
    Save-CommandReport $gui
    Assert-That ($gui.Code -eq 0 -and $gui.Json.status -eq 'PASS' -and $gui.Json.simulated -eq $true -and $gui.Json.businessCallbacks -eq 'NOT_VERIFIED' -and $gui.Json.workerException -eq $false -and $gui.Json.serial -eq $true -and $gui.Json.realMutation -eq 'NOT_PERFORMED') 'preset GUI worker smoke is explicitly simulated and does not claim business callbacks'
    Assert-That (@($gui.Json.screenshots).Count -eq 6 -and @($gui.Json.results).Count -eq 5) 'GUI smoke returns six screenshots and five ordered scenarios'
    Assert-That ($gui.Stdout.IndexOf([char]0xFFFD) -lt 0 -and @($gui.Json.screenshots | Where-Object { ([string]$_).IndexOf([char]0xFFFD) -ge 0 }).Count -eq 0) 'GUI top-level stdout preserves Unicode screenshot paths without replacement characters'
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $guiOutput 'gui-smoke-error.log'))) 'successful GUI smoke has no error log'
    Assert-That (Test-Path -LiteralPath (Join-Path $guiOutput 'gui-smoke-sanitized.log') -PathType Leaf) 'successful GUI smoke writes a sanitized lifecycle log'
    foreach ($screenshot in @($gui.Json.screenshots)) { Assert-That (Test-Path -LiteralPath ([string]$screenshot) -PathType Leaf) ('GUI smoke screenshot exists: ' + [IO.Path]::GetFileName([string]$screenshot)) }
    $guiEvidence = Join-Path $runEvidence 'gui-smoke-simulated'
    New-Item -ItemType Directory -Path $guiEvidence -Force | Out-Null
    foreach ($name in @('01-initial.png', '02-preflight.png', '03-waiting.png', '04-cancelled.png', '05-failure.png', '06-completed.png', 'gui-smoke-report.json', 'gui-smoke-sanitized.log')) {
        Copy-Item -LiteralPath (Join-Path $guiOutput $name) -Destination (Join-Path $guiEvidence $name) -Force
    }
    Save-JsonReport 'gui-smoke-simulated-summary' ([ordered]@{ schema = 'arcgis-pro-mcp-r5-gui-smoke-simulated-evidence-v1'; status = 'PASS'; simulated = $true; sourceOutput = $guiOutput; evidenceDirectory = $guiEvidence; report = $gui.Json }) | Out-Null

    $guiButtonOutput = Join-Path $root 'GUI button smoke output with 中文 空格'
    $guiProjectRoot = Join-Path $root 'owned project 中文 空格'
    $guiInstallRoot = Join-Path $root 'GUI install root 中文 空格'
    $guiStateRoot = Join-Path $root 'GUI installer state 中文 空格'
    New-Item -ItemType Directory -Path $guiProjectRoot -Force | Out-Null
    $guiButton = Invoke-Json 'gui-button-smoke-real-callbacks' (Join-Path $extract 'scripts\one-click-setup.ps1') @('-Action', 'GUI', '-GuiButtonSmokeMode', '-GuiSmokeOutput', $guiButtonOutput, '-ProjectRoot', $guiProjectRoot, '-InstallRoot', $guiInstallRoot, '-InstallerStateRoot', $guiStateRoot, '-TestMode', '-Json') -Sta
    Save-CommandReport $guiButton
    Assert-That ($guiButton.Code -eq 0 -and $guiButton.Json.status -eq 'PASS' -and $guiButton.Json.simulated -eq $false -and $guiButton.Json.automation -eq 'REAL_BUTTON_CALLBACKS' -and $guiButton.Json.workerException -eq $false -and $guiButton.Json.controlsRecovered -eq $true -and $guiButton.Json.correctTarget -eq $true -and $guiButton.Json.noUnexpectedDialog -eq $true -and $guiButton.Json.realMutation -eq 'NOT_PERFORMED') 'extracted-package GUI real button smoke executes only read-only callbacks'
    Assert-That (@($guiButton.Json.actualButtonCallbacks).Count -eq 2 -and [string]$guiButton.Json.actualButtonCallbacks[0] -eq 'Preflight.PerformClick' -and [string]$guiButton.Json.actualButtonCallbacks[1] -eq 'Diagnose.PerformClick' -and @($guiButton.Json.callbackSequence).Count -eq 2) 'GUI evidence records the exact Preflight and Diagnose button callback sequence'
    Assert-That (@($guiButton.Json.results).Count -eq 2 -and $guiButton.Json.results[0].status -eq 'PASS' -and $guiButton.Json.results[0].actualButtonCallback -eq $true -and $guiButton.Json.results[1].status -eq 'WAITING_USER_START' -and $guiButton.Json.results[1].actualButtonCallback -eq $true -and @($guiButton.Json.workerExitCodes | Where-Object { [int]$_ -ne 0 }).Count -eq 0) 'GUI callback workers return expected read-only statuses with no worker failure'
    $guiExpectedInstallRoot = [IO.Path]::GetFullPath($guiInstallRoot)
    $guiExpectedPluginTarget = Join-Path $guiExpectedInstallRoot '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'
    $guiExpectedCodexTarget = [IO.Path]::GetFullPath((Join-Path $guiProjectRoot '.codex\config.toml'))
    $guiTargetText = [string]$guiButton.Json.targetText
    $guiLogText = [string]$guiButton.Json.uiLogText
    $expectedPreflightMessage = '预检完成，未写入插件、客户端配置或事务状态。'
    $expectedDiagnoseMessage = '端口未监听；请由用户在 ArcGIS Pro 中点击 Start。'
    Assert-That ([string]$guiButton.Json.encoding -eq 'UTF-8' -and [IO.Path]::GetFullPath([string]$guiButton.Json.smokeOutput) -eq [IO.Path]::GetFullPath($guiButtonOutput) -and [string]$guiButton.Json.pluginTarget -ceq $guiExpectedPluginTarget -and [string]$guiButton.Json.pluginInstallRoot -ceq $guiExpectedInstallRoot -and [string]$guiButton.Json.addInId -ceq '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}' -and $guiTargetText.Contains('插件目标：' + $guiExpectedPluginTarget) -and $guiTargetText.Contains('codex → ' + $guiExpectedCodexTarget)) 'GUI displays the exact plugin InstallRoot plus Add-in GUID and the Chinese project target'
    Assert-That ($guiButton.Json.results[0].message -ceq $expectedPreflightMessage -and $guiButton.Json.results[1].message -ceq $expectedDiagnoseMessage -and $guiLogText.Contains($expectedPreflightMessage) -and $guiLogText.Contains($expectedDiagnoseMessage) -and $guiLogText.IndexOf([char]0xFFFD) -lt 0) 'GUI UTF-8 stdout round-trip preserves exact Chinese callback messages without replacement characters'
    Assert-That (@($guiButton.Json.screenshots).Count -eq 3 -and (Test-Path -LiteralPath (Join-Path $guiButtonOutput '01-initial.png') -PathType Leaf) -and (Test-Path -LiteralPath (Join-Path $guiButtonOutput '02-preflight-button.png') -PathType Leaf) -and (Test-Path -LiteralPath (Join-Path $guiButtonOutput '03-diagnose-button.png') -PathType Leaf)) 'real GUI callback smoke captures initial, preflight, and diagnose screenshots'
    Assert-That (-not (Test-Path -LiteralPath (Join-Path $guiButtonOutput 'gui-smoke-error.log')) -and (Test-Path -LiteralPath (Join-Path $guiButtonOutput 'gui-smoke-sanitized.log') -PathType Leaf)) 'real GUI callback smoke has no error log and writes a sanitized lifecycle log'
    $guiButtonEvidence = Join-Path $runEvidence 'gui-button-smoke'
    New-Item -ItemType Directory -Path $guiButtonEvidence -Force | Out-Null
    foreach ($name in @('01-initial.png', '02-preflight-button.png', '03-diagnose-button.png', 'gui-button-smoke-report.json', 'gui-smoke-sanitized.log')) {
        Copy-Item -LiteralPath (Join-Path $guiButtonOutput $name) -Destination (Join-Path $guiButtonEvidence $name) -Force
    }
    Save-JsonReport 'gui-button-smoke-summary' ([ordered]@{ schema = 'arcgis-pro-mcp-r5-gui-button-smoke-evidence-v1'; status = 'PASS'; simulated = $false; sourceOutput = $guiButtonOutput; evidenceDirectory = $guiButtonEvidence; report = $guiButton.Json }) | Out-Null

    $priorEvidence = Join-Path $runEvidence 'prior-failure-evidence'
    New-Item -ItemType Directory -Path $priorEvidence -Force | Out-Null
    $tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    foreach ($leaf in @('arcgis-pro-mcp-gui-smoke-check2', 'arcgis-pro-mcp-gui-smoke-check3', 'arcgis-pro-mcp-gui-smoke-debug4', 'arcgis-pro-mcp-gui-smoke-debug5', 'arcgis-pro-mcp-gui-smoke-debug6', 'arcgis-pro-mcp-gui-smoke-debug7', 'arcgis-pro-mcp-gui-smoke-debug8', 'arcgis-pro-mcp-gui-smoke-debug9', 'arcgis-pro-mcp-gui-smoke-r2-ninth')) {
        $candidate = Join-Path $tempBase $leaf
        if (Test-Path -LiteralPath $candidate -PathType Container) { Copy-Item -LiteralPath $candidate -Destination (Join-Path $priorEvidence $leaf) -Recurse -Force }
    }
    $gateScreenshot = Join-Path $tempBase 'codex-clipboard-c6c364f8-1bc7-4596-a554-905af9df2b54.png'
    if (Test-Path -LiteralPath $gateScreenshot -PathType Leaf) { Copy-Item -LiteralPath $gateScreenshot -Destination (Join-Path $priorEvidence 'gate-keeper-rejection.png') -Force }
    [IO.File]::WriteAllText((Join-Path $priorEvidence 'README.md'), @'
This directory preserves prior rejected GUI smoke evidence and references.
The r1 candidate was not accepted by the independent Gate Keeper. Its failure evidence is historical and is retained for audit; it is not used as a current PASS.
The preset r5 GUI smoke evidence is under ../gui-smoke-simulated/.
The extracted-package real read-only button callback evidence is under ../gui-button-smoke/.
'@, [Text.UTF8Encoding]::new($false))

    $final = [ordered]@{
        schema = 'arcgis-pro-mcp-one-click-test-report-v2'
        status = 'PASS'
        assertions = $passed
        candidate = 'one-click-1.0.2-r5'
        candidateZip = $zip
        candidateSha256 = $newHash.sha256
        evidenceDirectory = $runEvidence
        ownedTestRoot = $root
        realInstallation = 'NOT_PERFORMED'
        ownedTempInstallation = 'PERFORMED_AND_ROLLED_FORWARD_FOR_TEST'
        realClientMutation = 'NOT_PERFORMED'
        ownedTempClientMutation = 'PERFORMED_AND_ROLLED_FORWARD_FOR_TEST'
        realArcGISRuntime = 'NOT_PERFORMED'
        guiSmoke = 'PASS_SIMULATED_PRESET_ONLY'
        guiButtonSmoke = 'PASS_REAL_READ_ONLY_CALLBACKS'
        recoveryIndex = 'PASS_OWNED_FAILURE_AND_CANCEL_INJECTIONS'
        cancellation = 'PASS_OWNED_POST_MUTATION_BOUNDARY'
        cleanMachineAcceptance = 'NOT_VERIFIED'
    }
    Save-JsonReport 'final' $final | Out-Null
    $final | ConvertTo-Json -Depth 12
    exit 0
} catch {
    $parts = ([string]$_.Exception.Message).Split('|', 2)
    $code = if ($parts.Count -gt 1) { $parts[0] } else { 'ONE_CLICK_TEST_FAILED' }
    $message = if ($parts.Count -gt 1) { $parts[1] } else { [string]$_.Exception.Message }
    $script:testFailed = $true
    $script:failureCode = $code
    $script:failureMessage = $message
    if (Test-Path -LiteralPath $runEvidence -PathType Container) {
        Save-JsonReport 'failure' ([ordered]@{ schema = 'arcgis-pro-mcp-one-click-test-report-v2'; status = 'FAIL'; assertions = $passed; errorCode = $code; message = $message; evidenceDirectory = $runEvidence }) | Out-Null
    }
    [ordered]@{ schema = 'arcgis-pro-mcp-one-click-test-report-v2'; status = 'FAIL'; assertions = $passed; errorCode = $code; message = $message; evidenceDirectory = $runEvidence } | ConvertTo-Json -Depth 12
    exit 1
} finally {
    foreach ($ownedAsync in @($asyncProcesses)) {
        try {
            $ownedAsync.Refresh()
            if (-not $ownedAsync.HasExited) { Stop-Process -Id $ownedAsync.Id -Force -ErrorAction SilentlyContinue }
        } catch { }
    }
    Preserve-FailureArtifacts
    if (Test-Path -LiteralPath $runEvidence -PathType Container) {
        [IO.File]::WriteAllText((Join-Path $runEvidence '00-commands.json'), ($commandRecords | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    }
    if (Test-Path -LiteralPath $root -PathType Container) { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
}
