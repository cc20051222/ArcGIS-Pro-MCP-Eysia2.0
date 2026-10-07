# Verify an ArcGIS Pro MCP one-click bundle from its ZIP or extracted root.
# This is intentionally dependency-free: Windows PowerShell 5.1 and .NET only.
[CmdletBinding(DefaultParameterSetName='Root')]
param(
    [Parameter(Mandatory=$true, ParameterSetName='Zip')][string]$ZipPath,
    [Parameter(Mandatory=$true, ParameterSetName='Root')][string]$RootPath,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$acceptedAddInId = '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'
$acceptedRelease = '1.0.2'
$acceptedDeploymentVersions = @('one-click-1.0.2-r1', 'one-click-1.0.2-r2', 'one-click-1.0.2-r3', 'one-click-1.0.2-r4', 'one-click-1.0.2-r5', 'one-click-1.0.2-r6', 'one-click-1.0.2-r7', 'one-click-1.0.2-r8', 'one-click-1.0.2-r9', 'one-click-1.0.2-r10', 'one-click-1.0.2-r11', 'one-click-1.0.2-r12', 'one-click-1.0.2-r13', 'one-click-1.0.2-r14', 'one-click-1.0.2-r15', 'one-click-1.0.2-r16', 'one-click-1.0.2-r17', 'one-click-1.0.2-r18', 'one-click-1.0.2-r19', 'one-click-1.0.2-r20', 'one-click-1.0.2-r21', 'one-click-1.0.2-r22', 'one-click-1.0.2-r23', 'one-click-1.0.2-r24', 'one-click-1.0.2-r25',
    'one-click-1.0.2-r26',
    'one-click-1.0.2-r27',
    'one-click-1.0.2-r28',
    'one-click-1.0.2-r29')
$canonicalEndpoint = 'http://127.0.0.1:6520/mcp'
$requiredFiles = @(
    'ONE-CLICK-SETUP.cmd',
    'README-START-HERE.md',
    'scripts/one-click-setup.ps1',
    'scripts/verify-one-click-package.ps1',
    'scripts/user-workflow.ps1',
    'scripts/release-transaction.ps1',
    'scripts/client-config.ps1',
    'scripts/check-compatibility.ps1',
    'Config/client-catalog.json',
    'Config/compatibility-policy.json',
    'Source/ArcGISProMCP.Compatibility/Config.daml'
)

function Fail([string]$code, [string]$message) { throw ($code + '|' + $message) }
function Normalize-Logical([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { Fail 'BUNDLE_PATH_INVALID' 'empty logical path' }
    $normalized = $value.Replace('\', '/')
    if ($normalized.StartsWith('/') -or $normalized -match '^[A-Za-z]:') { Fail 'BUNDLE_PATH_INVALID' 'absolute logical path' }
    foreach ($part in $normalized.Split('/')) {
        if ($part -eq '..' -or $part -eq '.' -or $part -eq '') {
            Fail 'BUNDLE_PATH_INVALID' 'path traversal or empty segment'
        }
    }
    return $normalized
}
function Get-BytesHash([byte[]]$bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '') }
    finally { $sha.Dispose() }
}
function Read-ZipEntryBytes($entry) {
    $stream = $entry.Open()
    $memory = New-Object IO.MemoryStream
    try { $stream.CopyTo($memory); return ,$memory.ToArray() }
    finally { $memory.Dispose(); $stream.Dispose() }
}
function Read-JsonBytes([byte[]]$bytes, [string]$label) {
    try { return (([Text.Encoding]::UTF8.GetString($bytes)).TrimStart([char]0xFEFF) | ConvertFrom-Json) }   # 去 UTF-8 BOM（byte 级读取会残留 U+FEFF）
    catch { Fail 'BUNDLE_MANIFEST_INVALID' ($label + ' is not valid JSON') }
}
function Get-RootFileMap([string]$root) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { Fail 'BUNDLE_ROOT_NOT_FOUND' 'extracted root is missing' }
    $resolved = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $root).Path).TrimEnd('\')
    $rootItem = Get-Item -LiteralPath $resolved -Force
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail 'BUNDLE_REPARSE_REFUSED' 'extracted root is a reparse point' }
    $map = @{}
    foreach ($directory in @(Get-ChildItem -LiteralPath $resolved -Recurse -Force -Directory)) {
        if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail 'BUNDLE_REPARSE_REFUSED' 'reparse-point directory in extracted bundle' }
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $resolved -Recurse -Force -File)) {
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail 'BUNDLE_REPARSE_REFUSED' 'reparse-point file in extracted bundle' }
        $relative = Normalize-Logical $file.FullName.Substring($resolved.Length).TrimStart('\')
        if ($map.ContainsKey($relative)) { Fail 'BUNDLE_DUPLICATE_FILE' 'duplicate extracted logical path' }
        $map[$relative] = $file
    }
    return [pscustomobject]@{ Root = $resolved; Files = $map }
}
function Test-ForbiddenBundlePath([string]$logical) {
    if ($logical -match '(?i)(^|/)(MyProject1\.aprx|Phase4Test\.gdb|\.runtime|\.codex|\.cursor|\.dsh|[^/]+\.lock|[^/]+\.sr\.lock)(/|$)') {
        Fail 'BUNDLE_PROTECTED_CONTENT' 'user project, GIS data, runtime or lock content is present'
    }
    if ($logical -match '(?i)(^|/)(\.bak|backup|historical)') { Fail 'BUNDLE_UNAPPROVED_CONTENT' 'backup or historical content is present' }
}
function Verify-Common([hashtable]$files, [scriptblock]$ReadBytes) {
    if (-not $files.ContainsKey('bundle-manifest.json')) { Fail 'BUNDLE_MANIFEST_MISSING' 'bundle-manifest.json is missing' }
    $bundleManifest = Read-JsonBytes (& $ReadBytes $files['bundle-manifest.json']) 'bundle-manifest.json'
    if ([string]$bundleManifest.schema -cne 'arcgis-pro-mcp-one-click-bundle-v1') { Fail 'BUNDLE_MANIFEST_INVALID' 'unsupported bundle manifest schema' }
    # D-138 (G-329): dual-face acceptance-list guard, fail-closed. This script is copied into every bundle by
    # the packaging builder's take array, so the repository face is the single source of truth and the packaged
    # copy is refreshed from it whenever the generation moves. If an incoming bundle declares a deployment
    # version outside the list above, verification aborts here and reports both sides. This block deliberately
    # carries no generation literal and no tool-count literal, so the same-source policy tests keep their
    # single-match invariants; the take-array membership itself is proven in the batch run root (f2 G3).
    $repoAcceptedCount = @($acceptedDeploymentVersions).Count
    $repoAcceptedLast = [string](@($acceptedDeploymentVersions)[-1])
    $packageDeployment = [string]$bundleManifest.deploymentVersion
    if ($acceptedDeploymentVersions -notcontains $packageDeployment) {
        Fail 'DUALFACE_ACCEPTANCE_LIST_MISMATCH' ('repo=count:' + $repoAcceptedCount + ';last:' + $repoAcceptedLast +
            '|package=' + $packageDeployment)
    }
    if ([string]$bundleManifest.version -cne $acceptedRelease -or $acceptedDeploymentVersions -notcontains [string]$bundleManifest.deploymentVersion) { Fail 'BUNDLE_IDENTITY_INVALID' 'deployment identity mismatch' }
    if ([string]$bundleManifest.platform -cne 'Windows x64' -or [string]$bundleManifest.endpoint -cne $canonicalEndpoint -or [int]$bundleManifest.canonicalProductionToolCount -ne 239) { Fail 'BUNDLE_IDENTITY_INVALID' 'platform, endpoint or tool-count contract mismatch' }
    if ([string]$bundleManifest.cleanMachineAcceptance -cne 'NOT VERIFIED') { Fail 'BUNDLE_EVIDENCE_INVALID' 'clean-machine status must remain NOT VERIFIED' }
    if ($bundleManifest.manifestSelfExcluded -ne $true) { Fail 'BUNDLE_MANIFEST_INVALID' 'manifestSelfExcluded must be true' }

    $listed = @($bundleManifest.files)
    if ($listed.Count -eq 0) { Fail 'BUNDLE_MANIFEST_INVALID' 'file list is empty' }
    $listedNames = @{}
    foreach ($record in $listed) {
        $logical = Normalize-Logical ([string]$record.path)
        if ($logical -eq 'bundle-manifest.json' -or $listedNames.ContainsKey($logical)) { Fail 'BUNDLE_MANIFEST_INVALID' 'bundle manifest lists itself or duplicates a file' }
        Test-ForbiddenBundlePath $logical
        $listedNames[$logical] = $true
        if (-not $files.ContainsKey($logical)) { Fail 'BUNDLE_FILE_MISSING' 'manifest-listed file is absent' }
        $bytes = & $ReadBytes $files[$logical]
        if ([int64]$record.sizeBytes -ne $bytes.Length -or [string]$record.sha256 -cne (Get-BytesHash $bytes)) { Fail 'BUNDLE_FILE_HASH_MISMATCH' 'manifest-listed file hash or length mismatch' }
    }
    foreach ($name in @($files.Keys)) {
        $logical = Normalize-Logical ([string]$name)
        Test-ForbiddenBundlePath $logical
        if ($logical -ne 'bundle-manifest.json' -and -not $listedNames.ContainsKey($logical)) { Fail 'BUNDLE_UNLISTED_FILE' 'ZIP/extracted file is not listed in bundle manifest' }
    }
    foreach ($required in $requiredFiles) {
        if (-not $files.ContainsKey($required) -or -not $listedNames.ContainsKey($required)) { Fail 'BUNDLE_REQUIRED_FILE_MISSING' ('required file missing: ' + $required) }
    }
    $packageBytes = $null; $packageHash = ''
    # D-071 (C-013)：双代模式 —— bundle-manifest.payloads[] 存在则逐 payload 校验（net6/net8 各含 esriAddInX＋release-manifest）；否则单包模式（r15 及更早）
    $dualPayloads = @($bundleManifest.payloads)
    $sdkRefEntries = @()
    if ($null -ne $zip) { $sdkRefEntries = @($zip.Entries | Where-Object { $_.FullName -match '(?i)(^|/)sdk-refs/' }) }
    if ($sdkRefEntries.Count -gt 0) { Fail 'BUNDLE_CONTAINS_SDK_REFS' 'bundle must not contain the Esri SDK reference set' }
    if ($dualPayloads.Count -gt 0) {
        foreach ($pe in $dualPayloads) {
            $pkgPath = Normalize-Logical ([string]$pe.path)
            $manPath = Normalize-Logical ([string]$pe.manifestPath)
            if (-not $files.ContainsKey($pkgPath) -or -not $listedNames.ContainsKey($pkgPath)) { Fail 'BUNDLE_REQUIRED_FILE_MISSING' ('payload file missing: ' + $pkgPath) }
            if (-not $files.ContainsKey($manPath) -or -not $listedNames.ContainsKey($manPath)) { Fail 'BUNDLE_REQUIRED_FILE_MISSING' ('payload manifest missing: ' + $manPath) }
            $pkgBytes = & $ReadBytes $files[$pkgPath]
            $pkgHash = Get-BytesHash $pkgBytes
            if ([int64]$pe.sizeBytes -ne $pkgBytes.Length -or [string]$pe.sha256 -cne $pkgHash) { Fail 'BUNDLE_PAYLOAD_HASH_MISMATCH' ('payload mismatch: ' + $pkgPath) }
            $rm = Read-JsonBytes (& $ReadBytes $files[$manPath]) ('release manifest ' + $manPath)
            if ([string]$rm.schema -cne 'arcgis-pro-mcp-release-manifest-v1' -or [string]$rm.product.releaseVersion -cne $acceptedRelease -or [string]$rm.product.addInId -cne $acceptedAddInId) { Fail 'BUNDLE_RELEASE_IDENTITY_INVALID' 'embedded release manifest identity mismatch' }
            if ([int64]$rm.artifact.sizeBytes -ne $pkgBytes.Length -or [string]$rm.artifact.sha256 -cne $pkgHash) { Fail 'BUNDLE_PAYLOAD_HASH_MISMATCH' 'payload does not match embedded release manifest' }
            $packageBytes = $pkgBytes; $packageHash = $pkgHash
        }
    } else {
        foreach ($req in @('payload/ArcGISProMCP.Compatibility.esriAddInX', 'payload/ArcGISProMCP.Compatibility.release-manifest.json')) {
            if (-not $files.ContainsKey($req) -or -not $listedNames.ContainsKey($req)) { Fail 'BUNDLE_REQUIRED_FILE_MISSING' ('required file missing: ' + $req) }
        }
        $packageBytes = & $ReadBytes $files['payload/ArcGISProMCP.Compatibility.esriAddInX']
        $packageHash = Get-BytesHash $packageBytes
        $releaseManifest = Read-JsonBytes (& $ReadBytes $files['payload/ArcGISProMCP.Compatibility.release-manifest.json']) 'release manifest'
        if ([string]$releaseManifest.schema -cne 'arcgis-pro-mcp-release-manifest-v1' -or [string]$releaseManifest.product.releaseVersion -cne $acceptedRelease -or [string]$releaseManifest.product.addInId -cne $acceptedAddInId) { Fail 'BUNDLE_RELEASE_IDENTITY_INVALID' 'embedded release manifest identity mismatch' }
        if ([int64]$releaseManifest.artifact.sizeBytes -ne [int64]$packageBytes.Length -or [string]$releaseManifest.artifact.sha256 -cne [string]$packageHash) { Fail 'BUNDLE_PAYLOAD_HASH_MISMATCH' 'payload package does not match embedded release manifest' }
        if ([string]$bundleManifest.sourceArtifactSha256 -cne [string]$packageHash) { Fail 'BUNDLE_MANIFEST_INVALID' 'bundle manifest source artifact hash does not match payload' }
    }
    return [ordered]@{ schema = 'arcgis-pro-mcp-one-click-verification-v1'; status = 'PASS'; deploymentVersion = [string]$bundleManifest.deploymentVersion; releaseVersion = $acceptedRelease; fileCount = $listed.Count + 1; payloadSizeBytes = $packageBytes.Length; payloadSha256 = $packageHash; dualPayloadCount = $dualPayloads.Count; canonicalProductionToolCount = 239; cleanMachineAcceptance = 'NOT VERIFIED' }
}

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if ($PSCmdlet.ParameterSetName -eq 'Zip') {
        $zipFull = [IO.Path]::GetFullPath($ZipPath)
        if (-not (Test-Path -LiteralPath $zipFull -PathType Leaf)) { Fail 'BUNDLE_ZIP_NOT_FOUND' 'ZIP is missing' }
        $zip = [IO.Compression.ZipFile]::OpenRead($zipFull)
        try {
            $files = @{}
            foreach ($entry in @($zip.Entries | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Name) })) {
                $logical = Normalize-Logical ([string]$entry.FullName)
                if ($files.ContainsKey($logical)) { Fail 'BUNDLE_DUPLICATE_FILE' 'duplicate ZIP logical path' }
                $files[$logical] = $entry
            }
            $report = Verify-Common $files { param($item) Read-ZipEntryBytes $item }
            $report.source = 'ZIP'
        } finally { $zip.Dispose() }
    } else {
        $rootInfo = Get-RootFileMap $RootPath
        $report = Verify-Common $rootInfo.Files { param($item) [IO.File]::ReadAllBytes($item.FullName) }
        $report.source = 'EXTRACTED_ROOT'
    }
    if ($Json) { $report | ConvertTo-Json -Depth 8 } else { Write-Host ('One-click bundle verification: PASS (' + $report.source + ')') }
    exit 0
} catch {
    $parts = ([string]$_.Exception.Message).Split('|', 2)
    $code = if ($parts.Count -gt 1) { $parts[0] } else { 'BUNDLE_VERIFICATION_FAILED' }
    $message = if ($parts.Count -gt 1) { $parts[1] } else { 'bundle verification failed' }
    if ($Json) { [ordered]@{ schema = 'arcgis-pro-mcp-one-click-verification-v1'; status = 'FAIL'; errorCode = $code; message = $message } | ConvertTo-Json -Depth 6 }
    else { Write-Error ($code + ': ' + $message) }
    exit 1
}
