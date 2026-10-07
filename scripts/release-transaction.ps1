# ArcGIS Pro MCP - controlled install / uninstall / rollback transaction
#
# Default action is read-only Preflight. Actual mutation requires an explicit
# -Action and is fail-closed to the exact ArcGIS Pro Add-in ID directory.
# The official RegisterAddIn.exe mechanism is used only for real installation.

[CmdletBinding()]
param(
    [ValidateSet('Preflight', 'Snapshot', 'Install', 'Uninstall', 'Rollback', 'Acceptance')]
    [string]$Action = 'Preflight',
    [string]$PackagePath = '',
    [string]$ManifestPath = '',
    [string]$InstallRoot = '',
    [string]$TransactionRoot = '',
    [string]$LedgerPath = '',
    [string]$RegisterAddInPath = 'C:\Program Files\ArcGIS\Pro\bin\RegisterAddIn.exe',
    [string]$ArcGISProProcessName = 'ArcGISPro',
    [string]$CompatibilityCheckerPath = '',
    [string]$CompatibilityPolicyPath = '',
    [string]$CompatibilityFactsPath = '',
    [switch]$AllowCreateInstallRoot,
    [switch]$DryRun,
    [switch]$TestMode,
    [switch]$InjectFailureAfterInstall
)

$ErrorActionPreference = 'Stop'
$addInId = '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'
$assemblyName = 'ArcGISProMCP.Compatibility'
$packageName = "$assemblyName.esriAddInX"
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$defaultPackage = Join-Path $repoRoot 'Source\ArcGISProMCP.Compatibility\bin\x64\Debug\net6.0-windows\ArcGISProMCP.Compatibility.esriAddInX'
$packagePathResolved = if ([string]::IsNullOrWhiteSpace($PackagePath)) { $defaultPackage } else { [IO.Path]::GetFullPath($PackagePath) }
$manifestPathResolved = if ([string]::IsNullOrWhiteSpace($ManifestPath)) { [IO.Path]::ChangeExtension($packagePathResolved, '.release-manifest.json') } else { [IO.Path]::GetFullPath($ManifestPath) }
$defaultInstallRoot = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'ArcGIS\AddIns\ArcGISPro'
$installRootResolved = if ([string]::IsNullOrWhiteSpace($InstallRoot)) { [IO.Path]::GetFullPath($defaultInstallRoot) } else { [IO.Path]::GetFullPath($InstallRoot) }
$targetDir = Join-Path $installRootResolved $addInId
$transactionRootResolved = if ([string]::IsNullOrWhiteSpace($TransactionRoot)) {
    Join-Path $repoRoot ('.runtime\release-transactions\phase72_' + [guid]::NewGuid().ToString('N'))
} else { [IO.Path]::GetFullPath($TransactionRoot) }
$ledgerPathResolved = if ([string]::IsNullOrWhiteSpace($LedgerPath)) { Join-Path $transactionRootResolved 'ledger.json' } else { [IO.Path]::GetFullPath($LedgerPath) }
$compatibilityCheckerResolved = if ($CompatibilityCheckerPath) { [IO.Path]::GetFullPath($CompatibilityCheckerPath) } else { Join-Path $repoRoot 'scripts\check-compatibility.ps1' }
$compatibilityPolicyResolved = if ($CompatibilityPolicyPath) { [IO.Path]::GetFullPath($CompatibilityPolicyPath) } else { Join-Path $repoRoot 'Config\compatibility-policy.json' }

function Get-FileSha256([string]$path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($path)
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose() }
    } finally { $sha.Dispose() }
}

function Get-ZipEntryHash($entry) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = $entry.Open()
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose() }
    } finally { $sha.Dispose() }
}

function Get-CanonicalExistingPath([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { throw "PATH_NOT_FOUND: '$path'." }
    return [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $path).Path)
}

function Assert-OutsideInstallTarget([string]$path) {
    $candidate = [IO.Path]::GetFullPath($path).TrimEnd('\')
    $root = $installRootResolved.TrimEnd('\')
    if ($candidate.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or
        $candidate.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "TRANSACTION_PATH_INVALID: transaction path '$path' is inside install root '$installRootResolved'."
    }
}

function Get-AclSddl([string]$path) {
    try { return (Get-Acl -LiteralPath $path).Sddl }
    catch {
        $icacls = Join-Path $env:SystemRoot 'System32\icacls.exe'
        if (-not (Test-Path -LiteralPath $icacls)) { throw 'ACL_READ_FAILED: Get-Acl unavailable and icacls.exe was not found.' }
        $lines = @(& $icacls $path 2>&1 | ForEach-Object { [string]$_ } |
            Where-Object { $_ -notmatch '(?i)successfully processed|Successfully processed|processed file' })
        if ($LASTEXITCODE -ne 0 -or $lines.Count -eq 0) { throw "ACL_READ_FAILED: unable to read ACL for '$path'." }
        if ($lines[0].StartsWith($path, [StringComparison]::OrdinalIgnoreCase)) { $lines[0] = $lines[0].Substring($path.Length).TrimStart() }
        return 'ICACLS:' + ($lines -join "`n")
    }
}

function Set-AclSddl([string]$path, [string]$sddl, [bool]$isDirectory) {
    if ([string]::IsNullOrWhiteSpace($sddl)) { return }
    if ($sddl.StartsWith('ICACLS:', [StringComparison]::Ordinal)) { return }
    if ($isDirectory) {
        $security = [Security.AccessControl.DirectorySecurity]::new()
    } else {
        $security = [Security.AccessControl.FileSecurity]::new()
    }
    $security.SetSecurityDescriptorSddlForm($sddl)
    Set-Acl -LiteralPath $path -AclObject $security
}

function Get-UtcDateTime([object]$value) {
    if ($value -is [DateTime]) { return $value.ToUniversalTime() }
    if ($value -is [DateTimeOffset]) { return $value.UtcDateTime }
    return [DateTimeOffset]::Parse([string]$value).UtcDateTime
}

function Get-ZipConfig([string]$path) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -ceq 'Config.daml' } | Select-Object -First 1
        if ($null -eq $entry) { throw "PACKAGE_CONTENT_INVALID: '$path' has no root Config.daml." }
        $stream = $entry.Open()
        try {
            $reader = [IO.StreamReader]::new($stream, [Text.UTF8Encoding]::new($false))
            try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        } finally { $stream.Dispose() }
        $node = $xml.SelectSingleNode("/*[local-name()='ArcGIS']/*[local-name()='AddInInfo']")
        if ($null -eq $node) { throw 'AddInInfo node not found.' }
        return [pscustomobject]@{ Id = [string]$node.id; Version = [string]$node.version; DesktopVersion = [string]$node.desktopVersion }
    } finally { $zip.Dispose() }
}

function Get-PackageIdentity {
    if (-not (Test-Path -LiteralPath $packagePathResolved -PathType Leaf)) { throw "PACKAGE_NOT_FOUND: '$packagePathResolved'." }
    if (-not $packagePathResolved.EndsWith('.esriAddInX', [StringComparison]::OrdinalIgnoreCase)) { throw 'PACKAGE_CONTENT_INVALID: package extension must be .esriAddInX.' }
    if (-not (Test-Path -LiteralPath $manifestPathResolved -PathType Leaf)) { throw "MANIFEST_NOT_FOUND: '$manifestPathResolved'." }
    $config = Get-ZipConfig $packagePathResolved
    if ($config.Id -cne $addInId) { throw "PACKAGE_ID_MISMATCH: expected '$addInId' but found '$($config.Id)'." }
    $zip = [IO.Compression.ZipFile]::OpenRead($packagePathResolved)
    try {
        $names = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        foreach ($required in @('Config.daml', 'Images/AddInIcon.png', "Install/$assemblyName.dll")) {
            if ($names -notcontains $required) { throw "PACKAGE_CONTENT_INVALID: missing '$required'." }
        }
        $nested = @($names | Where-Object { $_ -match '(?i)\.esriAddInX$' })
        if ($nested.Count -gt 0) { throw "PACKAGE_CONTENT_INVALID: nested add-in entries: $($nested -join ', ' )" }
        $bridgeLogicalPath = 'Install/PythonBridge/bridge_runner.py'
        $bridgeEntries = @($zip.Entries | Where-Object {
            $_.FullName.Replace('\', '/') -ceq $bridgeLogicalPath
        })
        $bridgeNamedEntries = @($zip.Entries | Where-Object {
            [IO.Path]::GetFileName($_.FullName.Replace('\', '/')) -ceq 'bridge_runner.py'
        })
        if ($bridgeEntries.Count -ne 1 -or $bridgeNamedEntries.Count -ne 1) {
            throw 'PACKAGE_RUNTIME_ARTIFACT_INVALID: bridge_runner.py must appear exactly once at the owned logical path.'
        }
    } finally { $zip.Dispose() }
    $packageHash = Get-FileSha256 $packagePathResolved
    try { $manifest = Get-Content -LiteralPath $manifestPathResolved -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { throw "MANIFEST_INVALID: cannot parse '$manifestPathResolved': $($_.Exception.Message)" }
    if ($manifest.schema -ne 'arcgis-pro-mcp-release-manifest-v1') { throw 'MANIFEST_INVALID: unsupported manifest schema.' }
    if ($manifest.product.addInId -cne $addInId) { throw 'MANIFEST_ID_MISMATCH: manifest AddIn ID differs from target.' }
    if ($manifest.artifact.fileName -cne [IO.Path]::GetFileName($packagePathResolved)) { throw 'MANIFEST_ARTIFACT_MISMATCH: manifest filename differs from package.' }
    if ($manifest.artifact.sha256 -cne $packageHash) { throw 'MANIFEST_ARTIFACT_MISMATCH: manifest package hash differs from package.' }
    $runtimeArtifacts = @($manifest.runtimeArtifacts | Where-Object {
        [string]$_.logicalPath -ceq 'Install/PythonBridge/bridge_runner.py'
    })
    if ($runtimeArtifacts.Count -ne 1) {
        throw 'MANIFEST_RUNTIME_ARTIFACT_INVALID: bridge_runner.py manifest entry is missing or duplicated.'
    }
    $zip = [IO.Compression.ZipFile]::OpenRead($packagePathResolved)
    try {
        $bridgeEntry = @($zip.Entries | Where-Object {
            $_.FullName.Replace('\', '/') -ceq 'Install/PythonBridge/bridge_runner.py'
        })[0]
        if ([int64]$runtimeArtifacts[0].sizeBytes -ne [int64]$bridgeEntry.Length) {
            throw 'MANIFEST_RUNTIME_ARTIFACT_MISMATCH: bridge_runner.py byte length differs from package.'
        }
        if ([string]$runtimeArtifacts[0].sha256 -cne (Get-ZipEntryHash $bridgeEntry)) {
            throw 'MANIFEST_RUNTIME_ARTIFACT_MISMATCH: bridge_runner.py SHA-256 differs from package.'
        }
    } finally { $zip.Dispose() }
    foreach ($assembly in @($manifest.firstPartyAssemblies)) {
        if ($assembly.assemblyVersion -ne '1.0.0.0') { throw "MANIFEST_IDENTITY_MISMATCH: $($assembly.path) AssemblyVersion is not 1.0.0.0." }
    }
    return [pscustomobject]@{ Config = $config; Manifest = $manifest; PackageHash = $packageHash; PackageSize = (Get-Item -LiteralPath $packagePathResolved).Length }
}

function Assert-NoReparse([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return }
    $items = @(Get-Item -LiteralPath $path -Force) + @(Get-ChildItem -LiteralPath $path -Force -Recurse -ErrorAction Stop)
    foreach ($item in $items) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "REPARSE_POINT_REFUSED: '$($item.FullName)'." }
    }
}

function Get-InstallInventory {
    $root = Get-CanonicalExistingPath $installRootResolved
    $targetCanonical = [IO.Path]::GetFullPath($targetDir)
    if (-not (Test-Path -LiteralPath $targetDir)) {
        return [pscustomobject]@{ Exists = $false; Directory = $targetCanonical; Files = @(); DirectoryAcl = $null }
    }
    $targetItem = Get-Item -LiteralPath $targetDir -Force
    if (-not $targetItem.PSIsContainer) { throw 'INSTALL_TARGET_INVALID: target AddIn ID path is not a directory.' }
    if ($targetItem.Name -cne $addInId) { throw 'INSTALL_TARGET_INVALID: target leaf does not equal AddIn ID.' }
    Assert-NoReparse $targetDir
    $children = @(Get-ChildItem -LiteralPath $targetDir -Force)
    if (@($children | Where-Object { $_.PSIsContainer }).Count -gt 0) { throw 'OWNERSHIP_REFUSED: nested directories in target AddIn directory.' }
    $records = @()
    foreach ($file in @($children | Where-Object { -not $_.PSIsContainer })) {
        if ($file.Name -notmatch ('^' + [regex]::Escape($assemblyName) + '\.esriAddInX(?:\..+)?$')) {
            throw "OWNERSHIP_REFUSED: unexpected file '$($file.Name)' in exact AddIn ID directory."
        }
        $zipConfig = Get-ZipConfig $file.FullName
        if ($zipConfig.Id -cne $addInId) { throw "OWNERSHIP_REFUSED: '$($file.Name)' does not belong to target AddIn ID." }
        $records += [pscustomobject]@{
            RelativePath = $file.Name
            Kind = 'file'
            Length = [int64]$file.Length
            Sha256 = Get-FileSha256 $file.FullName
            LastWriteTimeUtc = $file.LastWriteTimeUtc.ToString('o')
            Attributes = [string]$file.Attributes
            AclSddl = Get-AclSddl $file.FullName
        }
    }
    return [pscustomobject]@{
        Exists = $true
        Directory = $targetCanonical
        DirectoryAcl = Get-AclSddl $targetDir
        DirectoryLastWriteTimeUtc = $targetItem.LastWriteTimeUtc.ToString('o')
        Files = @($records | Sort-Object RelativePath)
    }
}

function Test-ArcGISProStopped {
    $processes = @(Get-Process -Name $ArcGISProProcessName -ErrorAction SilentlyContinue)
    if ($processes.Count -gt 0) { throw "ARCGIS_PRO_RUNNING: process name '$ArcGISProProcessName' is running; mutation refused." }
}

function Invoke-CompatibilityCheck {
    if (-not (Test-Path -LiteralPath $compatibilityCheckerResolved -PathType Leaf)) { throw "COMPATIBILITY_CHECKER_NOT_FOUND: '$compatibilityCheckerResolved'." }
    # D-054 A2：宿主解析不再依赖 PATH 上的裸名 'powershell.exe'（PATH 被裁剪/非标准形态时无法解析，
    # 会静默得到空报告从而把「校验失败」误报为「兼容门拒绝」）⇒ 优先用 System32 内置绝对路径。
    $hostExe = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) 'WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $hostExe -PathType Leaf)) { $hostExe = 'powershell.exe' }
    # D-072 (C-015)：兼容门核对**待装包内** Config.daml（双代：net8 包 desktopVersion=3.5.0 / net6=3.0）——
    # 从包 zip 提取 Config.daml 到事务根并传 checker -ConfigPath（源码 Config.daml 仅 net6 默认，net8 会误判）。
    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
    New-Item -ItemType Directory -Force -Path $transactionRootResolved | Out-Null
    $pkgConfigPath = Join-Path $transactionRootResolved 'config.daml'
    $pkgZip = [IO.Compression.ZipFile]::OpenRead($packagePathResolved)
    try {
        $cfgEntry = $pkgZip.Entries | Where-Object { $_.FullName.Replace('\','/') -ceq 'Config.daml' } | Select-Object -First 1
        if ($null -eq $cfgEntry) { throw "PACKAGE_CONFIG_DAML_MISSING: '$packagePathResolved'" }
        $cfgStream = $cfgEntry.Open(); $mem = New-Object IO.MemoryStream
        try { $cfgStream.CopyTo($mem); [IO.File]::WriteAllBytes($pkgConfigPath, $mem.ToArray()) }
        finally { $mem.Dispose(); $cfgStream.Dispose() }
    } finally { $pkgZip.Dispose() }
    $args = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$compatibilityCheckerResolved,'-PolicyPath',$compatibilityPolicyResolved,'-ManifestPath',$manifestPathResolved,'-ConfigPath',$pkgConfigPath,'-Json')
    if ($CompatibilityFactsPath) { $args += @('-FactsPath',[IO.Path]::GetFullPath($CompatibilityFactsPath)) }
    # D-054 A2：文件握手 —— 子进程 stdout 在部分宿主不可捕获，改用报告文件为**权威通道**（stdout 保留兼容）。
    $reportPath = Join-Path $transactionRootResolved 'compatibility-report.json'
    $args += @('-JsonPath', $reportPath)
    $lines = @(& $hostExe @args 2>&1)
    $exitCode = $LASTEXITCODE
    $text = if (Test-Path -LiteralPath $reportPath -PathType Leaf) { (Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8) } else { ($lines -join [Environment]::NewLine) }
    if ([string]::IsNullOrWhiteSpace($text)) { throw "COMPATIBILITY_CHECK_FAILED: checker produced no report (host '$hostExe', exit '$exitCode', report path '$reportPath')." }
    try { $report = $text | ConvertFrom-Json } catch { throw "COMPATIBILITY_CHECK_FAILED: checker did not return JSON. $text" }
    if ($null -eq $report -or -not $report.PSObject.Properties['overallStatus']) { throw "COMPATIBILITY_CHECK_FAILED: checker report has no overallStatus. $text" }
    if ([string]$report.overallStatus -ne 'PASS' -and $Action -ne 'Preflight' -and -not $DryRun) {
        throw "COMPATIBILITY_GATE_REFUSED: status '$($report.overallStatus)', exit code '$($report.exitCode)'."
    }
    Add-Event 'compatibility-preflight' ([string]$report.overallStatus) @{ checkerExitCode=$exitCode; reportExitCode=$report.exitCode; policyVersion=$report.policyVersion; currentHostEvidence=$report.currentHostEvidence }
    return $report
}

function Add-Event([string]$name, [string]$state, [hashtable]$data = @{}) {
    $ledger.events += [ordered]@{ atUtc = [DateTime]::UtcNow.ToString('o'); name = $name; state = $state; data = $data }
}

function Save-Ledger {
    if ($Action -eq 'Preflight' -and $DryRun) { return }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ledgerPathResolved) | Out-Null
    [IO.File]::WriteAllText($ledgerPathResolved, ($ledger | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
}

function Copy-SnapshotDirectory([string]$source, [string]$destination) {
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $destination -Recurse -Force
}

function Set-InventoryMetadata([string]$basePath, [pscustomobject]$inventory) {
    if (-not $inventory.Exists) { return }
    foreach ($record in @($inventory.Files)) {
        $path = Join-Path $basePath $record.RelativePath
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "ROLLBACK_VERIFY_FAILED: missing '$($record.RelativePath)'." }
        (Get-Item -LiteralPath $path).LastWriteTimeUtc = Get-UtcDateTime $record.LastWriteTimeUtc
        Set-AclSddl $path $record.AclSddl $false
    }
    (Get-Item -LiteralPath $basePath).LastWriteTimeUtc = Get-UtcDateTime $inventory.DirectoryLastWriteTimeUtc
    Set-AclSddl $basePath $inventory.DirectoryAcl $true
}

function Assert-InventoryEqual([pscustomobject]$expected, [pscustomobject]$actual, [string]$label) {
    if ($expected.Exists -ne $actual.Exists) { throw "${label}_VERIFY_FAILED: Exists mismatch." }
    if (-not $expected.Exists) { return }
    if ($expected.Files.Count -ne $actual.Files.Count) { throw "${label}_VERIFY_FAILED: file count mismatch." }
    for ($i = 0; $i -lt $expected.Files.Count; $i++) {
        $left = $expected.Files[$i]; $right = $actual.Files[$i]
        foreach ($field in @('RelativePath', 'Length', 'Sha256', 'LastWriteTimeUtc', 'Attributes', 'AclSddl')) {
            $leftValue = [string]$left.$field
            $rightValue = [string]$right.$field
            if ($field -eq 'LastWriteTimeUtc') {
                $leftValue = (Get-UtcDateTime $left.$field).ToString('o')
                $rightValue = (Get-UtcDateTime $right.$field).ToString('o')
            }
            if ($leftValue -cne $rightValue) { throw "${label}_VERIFY_FAILED: $field mismatch for '$($left.RelativePath)'." }
        }
    }
}

function New-Snapshot {
    Assert-OutsideInstallTarget $transactionRootResolved
    if (-not (Test-Path -LiteralPath $installRootResolved -PathType Container)) {
        if ($Action -ne 'Install' -or -not $AllowCreateInstallRoot) { throw "INSTALL_ROOT_NOT_FOUND: '$installRootResolved'." }
        if (Test-Path -LiteralPath $installRootResolved) { throw "INSTALL_ROOT_INVALID: '$installRootResolved' is not a directory." }
        New-Item -ItemType Directory -Force -Path $installRootResolved | Out-Null
    }
    Assert-NoReparse $installRootResolved
    $inventory = Get-InstallInventory
    $baselineDir = Join-Path $transactionRootResolved 'baseline'
    New-Item -ItemType Directory -Force -Path $transactionRootResolved | Out-Null
    if ($inventory.Exists) { Copy-SnapshotDirectory $targetDir $baselineDir }
    $ledger.baseline = [ordered]@{ Exists = $inventory.Exists; SnapshotDirectory = $baselineDir; Inventory = $inventory }
    Add-Event 'snapshot' 'PASS' @{ exists = $inventory.Exists; fileCount = $inventory.Files.Count }
    Save-Ledger
    return $inventory
}

function Read-Baseline {
    if ($null -eq $ledger.baseline) { throw 'TRANSACTION_STATE_INVALID: baseline snapshot is missing.' }
    return [pscustomobject]$ledger.baseline.Inventory
}

function Move-TargetTo([string]$destination) {
    if (-not (Test-Path -LiteralPath $targetDir)) { return $false }
    $null = Get-InstallInventory
    if (Test-Path -LiteralPath $destination) { throw "RECOVERY_TARGET_EXISTS: '$destination'." }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Move-Item -LiteralPath $targetDir -Destination $destination
    return $true
}

function Restore-Baseline {
    $baseline = Read-Baseline
    $baselineDir = [string]$ledger.baseline.SnapshotDirectory
    if ($baseline.Exists -and -not (Test-Path -LiteralPath $baselineDir)) { throw 'SNAPSHOT_NOT_FOUND: baseline directory missing.' }
    $quarantine = Join-Path $transactionRootResolved ('quarantine-' + [guid]::NewGuid().ToString('N'))
    if (Test-Path -LiteralPath $targetDir) { Move-TargetTo $quarantine | Out-Null }
    if ($baseline.Exists) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
        Copy-SnapshotDirectory $baselineDir $targetDir
        Set-InventoryMetadata $targetDir $baseline
    }
    $actual = Get-InstallInventory
    Assert-InventoryEqual $baseline $actual 'BASELINE'
    Add-Event 'rollback' 'PASS' @{ restored = $baseline.Exists; quarantine = $quarantine }
    Save-Ledger
}

function Invoke-OfficialInstall([pscustomobject]$package) {
    if ($TestMode) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
        Copy-Item -LiteralPath $packagePathResolved -Destination (Join-Path $targetDir $packageName) -Force
        Add-Event 'install' 'PASS' @{ mode = 'TEST_COPY'; packageHash = $package.PackageHash }
        return 0
    }
    if (-not (Test-Path -LiteralPath $RegisterAddInPath -PathType Leaf)) { throw "REGISTER_TOOL_NOT_FOUND: '$RegisterAddInPath'." }
    # D-060 修复（G-149-B scripts 类，含反证）：官方 RegisterAddIn.exe 以**源包路径**调用时，对「同 AddIn ID 且版本号相同」
    # 的既存安装不执行替换（实测 exit 0 但安装位文件字节未变）⇒ 先显式落位到安装位，再注册**安装位路径**。
    # 反证：修复前 —— Install 以 INSTALL_VERIFY_FAILED 中止（目标哈希仍为旧值，事务自动 rollback）；修复后 —— install-verify PASS。
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    $installedPackagePath = Join-Path $targetDir $packageName
    Copy-Item -LiteralPath $packagePathResolved -Destination $installedPackagePath -Force
    Add-Event 'install-copy' 'PASS' @{ destination = $installedPackagePath; packageHash = $package.PackageHash }
    $start = Get-Date
    $process = Start-Process -FilePath $RegisterAddInPath -ArgumentList @($installedPackagePath, '/s') -Wait -PassThru
    $elapsedMs = [int]((Get-Date) - $start).TotalMilliseconds
    Add-Event 'register-addin' $(if ($process.ExitCode -eq 0) { 'PASS' } else { 'FAIL' }) @{ exitCode = $process.ExitCode; elapsedMs = $elapsedMs; mechanism = 'Esri RegisterAddIn.exe /s' }
    if ($process.ExitCode -ne 0) { throw "REGISTER_FAILED: RegisterAddIn.exe exit code $($process.ExitCode)." }
    return $process.ExitCode
}

function Verify-InstalledPackage([pscustomobject]$package) {
    $inventory = Get-InstallInventory
    if (-not $inventory.Exists) { throw 'INSTALL_VERIFY_FAILED: target directory absent.' }
    $installed = Join-Path $targetDir $packageName
    if (-not (Test-Path -LiteralPath $installed -PathType Leaf)) { throw 'INSTALL_VERIFY_FAILED: expected package file absent.' }
    $hash = Get-FileSha256 $installed
    if ($hash -cne $package.PackageHash) { throw "INSTALL_VERIFY_FAILED: expected package hash '$($package.PackageHash)' but found '$hash'." }
    Add-Event 'install-verify' 'PASS' @{ packageHash = $hash; fileCount = $inventory.Files.Count }
    return $inventory
}

function Invoke-Preflight {
    $installRootExists = Test-Path -LiteralPath $installRootResolved -PathType Container
    if (-not $installRootExists) {
        if (Test-Path -LiteralPath $installRootResolved) { throw "INSTALL_ROOT_INVALID: '$installRootResolved' is not a directory." }
        if ($Action -ne 'Install' -or -not $AllowCreateInstallRoot) { throw "INSTALL_ROOT_NOT_FOUND: '$installRootResolved'." }
    }
    Assert-OutsideInstallTarget $transactionRootResolved
    Assert-NoReparse $installRootResolved
    Test-ArcGISProStopped
    $package = Get-PackageIdentity
    $compatibility = Invoke-CompatibilityCheck
    $inventory = if ($installRootExists) { Get-InstallInventory } else { [pscustomobject]@{ Exists = $false; Directory = [IO.Path]::GetFullPath($targetDir); Files = @(); DirectoryAcl = $null } }
    $result = [ordered]@{
        schema = 'arcgis-pro-mcp-release-transaction-v1'
        action = $Action
        dryRun = [bool]$DryRun
        testMode = [bool]$TestMode
        allowCreateInstallRoot = [bool]$AllowCreateInstallRoot
        addInId = $addInId
        packagePath = $packagePathResolved
        packageHash = $package.PackageHash
        releaseVersion = $package.Manifest.product.releaseVersion
        installRoot = $installRootResolved
        targetDirectory = $targetDir
        arcgisProProcessName = $ArcGISProProcessName
        arcgisProRunning = $false
        registeredTargetExists = $inventory.Exists
        registeredFileCount = $inventory.Files.Count
        registerTool = $RegisterAddInPath
        officialInstallMechanism = 'RegisterAddIn.exe /s'
        uninstallMechanism = 'project-owned recoverable move of exact owned AddIn ID directory; not an official CLI uninstall'
        compatibilityStatus = $compatibility.overallStatus
        compatibilityExitCode = $compatibility.exitCode
    }
    Add-Event 'preflight' 'PASS' @{ targetExists = $inventory.Exists; targetFileCount = $inventory.Files.Count; packageHash = $package.PackageHash }
    Save-Ledger
    Write-Host ($result | ConvertTo-Json -Depth 8)
    return $package
}

$ledger = [ordered]@{
    schema = 'arcgis-pro-mcp-release-transaction-v1'
    runId = [IO.Path]::GetFileName($transactionRootResolved)
    startedAtUtc = [DateTime]::UtcNow.ToString('o')
    action = $Action
    dryRun = [bool]$DryRun
    testMode = [bool]$TestMode
    allowCreateInstallRoot = [bool]$AllowCreateInstallRoot
    addInId = $addInId
    packagePath = $packagePathResolved
    manifestPath = $manifestPathResolved
    installRoot = $installRootResolved
    targetDirectory = $targetDir
    events = @()
    status = 'STARTED'
}

if ($Action -in @('Install', 'Uninstall', 'Rollback') -and (Test-Path -LiteralPath $ledgerPathResolved -PathType Leaf)) {
    $existingLedger = Get-Content -LiteralPath $ledgerPathResolved -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($existingLedger.addInId -cne $addInId) { throw 'TRANSACTION_STATE_INVALID: existing ledger belongs to a different AddIn ID.' }
    foreach ($property in $existingLedger.PSObject.Properties) { $ledger[$property.Name] = $property.Value }
    $ledger.action = $Action
    $ledger.dryRun = [bool]$DryRun
    $ledger.testMode = [bool]$TestMode
    $ledger.allowCreateInstallRoot = [bool]$AllowCreateInstallRoot
    $ledger.events = @($ledger.events)
}

try {
    if ($InjectFailureAfterInstall -and -not $TestMode) { throw 'TEST_INJECTION_REFUSED: failure injection requires -TestMode.' }
    $package = Invoke-Preflight
    if ($Action -eq 'Preflight') { $ledger.status = 'PASS'; Save-Ledger; exit 0 }
    if ($DryRun) {
        Add-Event 'dry-run' 'PASS' @{ noMutation = $true }
        $ledger.status = 'PASS'
        Save-Ledger
        Write-Host 'DryRun: PASS; no install-root mutation performed.' -ForegroundColor Yellow
        exit 0
    }

    switch ($Action) {
        'Snapshot' {
            New-Snapshot | Out-Null
        }
        'Install' {
            if ($null -eq $ledger.baseline) { New-Snapshot | Out-Null }
            Invoke-OfficialInstall $package | Out-Null
            Test-ArcGISProStopped
            $null = Verify-InstalledPackage $package
            if ($InjectFailureAfterInstall) { throw 'TEST_INJECTED_FAILURE_AFTER_INSTALL' }
        }
        'Uninstall' {
            if ($null -eq $ledger.baseline) { throw 'TRANSACTION_STATE_INVALID: Uninstall requires the same transaction ledger as Snapshot/Install.' }
            $current = Get-InstallInventory
            if (-not $current.Exists) { Add-Event 'uninstall' 'PASS' @{ alreadyAbsent = $true }; break }
            $removed = Join-Path $transactionRootResolved 'removed-target'
            Move-TargetTo $removed | Out-Null
            if (Test-Path -LiteralPath $targetDir) { throw 'UNINSTALL_VERIFY_FAILED: target directory still exists.' }
            Add-Event 'uninstall' 'PASS' @{ recoverableMove = $removed }
        }
        'Rollback' {
            Restore-Baseline
        }
        'Acceptance' {
            New-Snapshot | Out-Null
            try {
                Invoke-OfficialInstall $package | Out-Null
                Test-ArcGISProStopped
                $null = Verify-InstalledPackage $package
                if ($InjectFailureAfterInstall) { throw 'TEST_INJECTED_FAILURE_AFTER_INSTALL' }
                $removed = Join-Path $transactionRootResolved 'removed-target'
                Move-TargetTo $removed | Out-Null
                if (Test-Path -LiteralPath $targetDir) { throw 'UNINSTALL_VERIFY_FAILED: target directory still exists.' }
                Add-Event 'uninstall' 'PASS' @{ recoverableMove = $removed }
                Restore-Baseline
                Add-Event 'acceptance' 'PASS' @{ finalState = 'exact-baseline-restored' }
            } catch {
                Add-Event 'acceptance' 'FAIL' @{ error = $_.Exception.Message }
                $ledger.recoveryAttempted = $true
                try { Restore-Baseline } catch { Add-Event 'automatic-rollback' 'FAIL' @{ error = $_.Exception.Message } }
                throw
            }
        }
    }
    $ledger.status = 'PASS'
    $ledger.completedAtUtc = [DateTime]::UtcNow.ToString('o')
    Save-Ledger
    Write-Host ("$Action : PASS") -ForegroundColor Green
    if ($Action -ne 'Preflight') { Write-Host ("Ledger : " + $ledgerPathResolved) }
    exit 0
} catch {
    if ($Action -in @('Install', 'Uninstall') -and $null -ne $ledger.baseline -and $ledger.recoveryAttempted -ne $true) {
        $ledger.recoveryAttempted = $true
        try { Restore-Baseline } catch { Add-Event 'automatic-rollback' 'FAIL' @{ error = $_.Exception.Message } }
    }
    $ledger.status = 'FAIL'
    $ledger.error = $_.Exception.Message
    $ledger.completedAtUtc = [DateTime]::UtcNow.ToString('o')
    try { Save-Ledger } catch { }
    Write-Error $_.Exception.Message
    exit 1
}
