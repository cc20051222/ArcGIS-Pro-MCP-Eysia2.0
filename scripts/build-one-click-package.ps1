# Build a separate one-click deployment bundle from the accepted 1.0.2 artifact.
# This does not build, register, install, launch, or configure any real client.
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$OutputDirectory = '',
    [ValidatePattern('^r[1-9][0-9]*$')][string]$CandidateRevision = 'r6',
    [switch]$Force,
    [string]$ReproducibilityManifestPath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
# D-114 (G-247 R-3): dual-payload layout, mirroring the accepted r22/r23 form. Host selection in
# one-click-setup.ps1 picks payload\net8 for Pro 3.3+ and payload\net6 for 3.0-3.2, so a release bundle has to
# carry both layers; the previous single net6-only layer could not reproduce the current r23 package.
$layerDefinitions = @(
    [pscustomobject]@{ Tf = 'net6'; Directory = 'net6.0-windows'; DesktopVersion = '3.0' },
    [pscustomobject]@{ Tf = 'net8'; Directory = 'net8.0-windows'; DesktopVersion = '3.5.0' }
)
$packageFileName = 'ArcGISProMCP.Compatibility.esriAddInX'
$manifestFileName = 'ArcGISProMCP.Compatibility.release-manifest.json'
$addInId = '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'
$verifierPath = Join-Path $repoRoot 'scripts\verify-one-click-package.ps1'
$outputRoot = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path $repoRoot 'Release' } else { [IO.Path]::GetFullPath($OutputDirectory) }
$deploymentVersion = 'one-click-1.0.2-' + $CandidateRevision
$bundleName = 'ArcGIS-Pro-MCP-OneClick-1.0.2-' + $CandidateRevision + '-Windows-x64'
$zipPath = Join-Path $outputRoot ($bundleName + '.zip')
$hashPath = $zipPath + '.sha256'

function Get-Hash([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function Copy-BundleFile([string]$relative, [string]$stagingRoot) {
    $source = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw ('BUNDLE_SOURCE_MISSING:' + $relative) }
    $destination = Join-Path $stagingRoot $relative
    $directory = [IO.Path]::GetDirectoryName($destination)
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    Copy-Item -LiteralPath $source -Destination $destination -Force
}
function Get-Logical([string]$path, [string]$root) { return $path.Substring($root.Length + 1).Replace('\', '/') }

$layers = @()
foreach ($layer in $layerDefinitions) {
    $layerRoot = Join-Path $repoRoot ("Source\ArcGISProMCP.Compatibility\bin\x64\$Configuration\" + $layer.Directory)
    $layerPackage = Join-Path $layerRoot $packageFileName
    $layerManifest = Join-Path $layerRoot $manifestFileName
    if (-not (Test-Path -LiteralPath $layerPackage -PathType Leaf)) { throw ('PACKAGE_NOT_FOUND:' + $layer.Tf) }
    if (-not (Test-Path -LiteralPath $layerManifest -PathType Leaf)) { throw ('RELEASE_MANIFEST_NOT_FOUND:' + $layer.Tf) }
    $layerSize = (Get-Item -LiteralPath $layerPackage).Length
    $layerHash = Get-Hash $layerPackage
    # Each layer's own release manifest is the identity anchor for that layer (artifact size + sha256).
    $peek = Get-Content -LiteralPath $layerManifest -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($layerSize -ne [int64]$peek.artifact.sizeBytes -or $layerHash -cne [string]$peek.artifact.sha256) { throw ('ACCEPTED_ARTIFACT_MISMATCH:' + $layer.Tf) }
    if ([string]$peek.schema -cne 'arcgis-pro-mcp-release-manifest-v1' -or
        [string]$peek.product.releaseVersion -cne '1.0.2' -or
        [string]$peek.product.addInId -cne $addInId -or
        [string]$peek.target.targetFramework -cne $layer.Directory -or
        [string]$peek.target.arcgisProDesktopVersion -cne $layer.DesktopVersion) { throw ('RELEASE_MANIFEST_IDENTITY_INVALID:' + $layer.Tf) }
    $layers += [pscustomobject]@{ Tf = $layer.Tf; TargetFramework = $layer.Directory; DesktopVersion = $layer.DesktopVersion
                                  PackagePath = $layerPackage; ManifestPath = $layerManifest
                                  SizeBytes = $layerSize; Sha256 = $layerHash }
}
# sourceArtifactSha256 keeps naming the net8 payload -- the layer the current Pro 3.5 host installs (r23 precedent).
$net8Layer = @($layers | Where-Object { $_.Tf -eq 'net8' })[0]
$acceptedHash = [string]$net8Layer.Sha256
if (-not (Test-Path -LiteralPath $verifierPath -PathType Leaf)) { throw 'BUNDLE_VERIFIER_MISSING' }
if ((Test-Path -LiteralPath $zipPath -PathType Leaf) -and -not $Force) { throw 'BUNDLE_ALREADY_EXISTS_USE_FORCE' }

$temporaryRoot = Join-Path $outputRoot ('.staging-' + 'arcgis-pro-mcp-one-click-' + '-' + [guid]::NewGuid().ToString('N'))
$stagingRoot = Join-Path $temporaryRoot $bundleName
try {
    New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
    $bundleTakeFiles = @(
        'NOTICE.md',
        'Distribution\ONE-CLICK-SETUP.cmd', 'Distribution\RECOVERY-MENU.cmd',
        'Distribution\ONE-CLICK-UNINSTALL-PLUGIN.cmd', 'Distribution\ROLLBACK-PLUGIN.cmd',
        'Distribution\RESTORE-CLIENT.cmd',
        'Distribution\README-ONE-CLICK-START.md',
        'scripts\one-click-setup.ps1', 'scripts\verify-one-click-package.ps1',
        'scripts\user-workflow.ps1', 'scripts\release-transaction.ps1',
        'scripts\client-config.ps1', 'scripts\check-compatibility.ps1',
        'Config\client-catalog.json', 'Config\compatibility-policy.json',
        'Config\client-templates\codex.toml.fragment', 'Config\client-templates\cursor.mcp.json',
        'Config\client-templates\deepseek.cordis.patch.yml', 'Config\client-templates\claude-desktop.mcp.json',
        'Source\ArcGISProMCP.Compatibility\Config.daml',
        'Docs\ONE_CLICK_DEPLOYMENT_USER_GUIDE.md'
    )
    foreach ($relative in $bundleTakeFiles) { Copy-BundleFile $relative $stagingRoot }
    Move-Item -LiteralPath (Join-Path $stagingRoot 'Distribution\ONE-CLICK-SETUP.cmd') -Destination (Join-Path $stagingRoot 'ONE-CLICK-SETUP.cmd')
    Move-Item -LiteralPath (Join-Path $stagingRoot 'Distribution\RECOVERY-MENU.cmd') -Destination (Join-Path $stagingRoot 'RECOVERY-MENU.cmd')
    Move-Item -LiteralPath (Join-Path $stagingRoot 'Distribution\ONE-CLICK-UNINSTALL-PLUGIN.cmd') -Destination (Join-Path $stagingRoot 'UNINSTALL-PLUGIN.cmd')
    Move-Item -LiteralPath (Join-Path $stagingRoot 'Distribution\ROLLBACK-PLUGIN.cmd') -Destination (Join-Path $stagingRoot 'ROLLBACK-PLUGIN.cmd')
    Move-Item -LiteralPath (Join-Path $stagingRoot 'Distribution\RESTORE-CLIENT.cmd') -Destination (Join-Path $stagingRoot 'RESTORE-CLIENT.cmd')
    Move-Item -LiteralPath (Join-Path $stagingRoot 'Distribution\README-ONE-CLICK-START.md') -Destination (Join-Path $stagingRoot 'README-START-HERE.md')
    Remove-Item -LiteralPath (Join-Path $stagingRoot 'Distribution') -Force
    New-Item -ItemType Directory -Path (Join-Path $stagingRoot 'payload') -Force | Out-Null
    foreach ($layer in $layers) {
        $layerDirectory = Join-Path $stagingRoot ('payload\' + $layer.Tf)
        New-Item -ItemType Directory -Path $layerDirectory -Force | Out-Null
        Copy-Item -LiteralPath $layer.PackagePath -Destination (Join-Path $layerDirectory $packageFileName)
        Copy-Item -LiteralPath $layer.ManifestPath -Destination (Join-Path $layerDirectory $manifestFileName)
    }

    # D-133 (G-322 ruling, option B): fail-closed reproducibility guard, runs BEFORE any bundle output exists.
    # (i) take integrity - repo source face vs the staged member copied from it. Never whitelisted.
    # (ii) reproducibility - staged member vs the reference published face. Only the three ruled
    #       release-stage differences may differ, each with its own reason (no bare suppression).
    $reproTakeMap = [ordered]@{
        'NOTICE.md' = 'NOTICE.md'
        'Distribution\ONE-CLICK-SETUP.cmd' = 'ONE-CLICK-SETUP.cmd'
        'Distribution\RECOVERY-MENU.cmd' = 'RECOVERY-MENU.cmd'
        'Distribution\ONE-CLICK-UNINSTALL-PLUGIN.cmd' = 'UNINSTALL-PLUGIN.cmd'
        'Distribution\ROLLBACK-PLUGIN.cmd' = 'ROLLBACK-PLUGIN.cmd'
        'Distribution\RESTORE-CLIENT.cmd' = 'RESTORE-CLIENT.cmd'
        'Distribution\README-ONE-CLICK-START.md' = 'README-START-HERE.md'
        'scripts\one-click-setup.ps1' = 'scripts/one-click-setup.ps1'
        'scripts\verify-one-click-package.ps1' = 'scripts/verify-one-click-package.ps1'
        'scripts\user-workflow.ps1' = 'scripts/user-workflow.ps1'
        'scripts\release-transaction.ps1' = 'scripts/release-transaction.ps1'
        'scripts\client-config.ps1' = 'scripts/client-config.ps1'
        'scripts\check-compatibility.ps1' = 'scripts/check-compatibility.ps1'
        'Config\client-catalog.json' = 'Config/client-catalog.json'
        'Config\compatibility-policy.json' = 'Config/compatibility-policy.json'
        'Config\client-templates\codex.toml.fragment' = 'Config/client-templates/codex.toml.fragment'
        'Config\client-templates\cursor.mcp.json' = 'Config/client-templates/cursor.mcp.json'
        'Config\client-templates\deepseek.cordis.patch.yml' = 'Config/client-templates/deepseek.cordis.patch.yml'
        'Config\client-templates\claude-desktop.mcp.json' = 'Config/client-templates/claude-desktop.mcp.json'
        'Source\ArcGISProMCP.Compatibility\Config.daml' = 'Source/ArcGISProMCP.Compatibility/Config.daml'
        'Docs\ONE_CLICK_DEPLOYMENT_USER_GUIDE.md' = 'Docs/ONE_CLICK_DEPLOYMENT_USER_GUIDE.md'
    }
    if ($reproTakeMap.Count -ne $bundleTakeFiles.Count) { throw ('BUNDLE_TAKE_MAP_STALE|map=' + $reproTakeMap.Count + '|takeArray=' + $bundleTakeFiles.Count) }
    # Whitelist: exactly the three differences the commander ruled as release-stage products (G-322).
    $reproWhitelist = @(
        [pscustomobject]@{ member = 'NOTICE.md'; reason = 'generation-sync header exists only in the published copy (D-128); the repo face carries the AGENTS-grounded release note, so a rebuild legitimately differs here (G-322 option B)' },
        [pscustomobject]@{ member = 'scripts/one-click-setup.ps1'; reason = 'the 33-line Authenticode signature block is a release-stage artefact; backflowing it into the repo source would embed a one-time signature in source (G-322 rejected option A)' },
        [pscustomobject]@{ member = 'README-START-HERE.md'; reason = 'release-stage generation-sync header (665 B) is added during packaging; the repo source is semantically restored but shorter (D-132 T-132-3)' }
    )
    if ($reproWhitelist.Count -ne 3) { throw ('REPRO_WHITELIST_SIZE_INVALID|' + $reproWhitelist.Count) }
    $reproWhitelistNames = @($reproWhitelist | ForEach-Object { [string]$_.member })
    $reproTakeCompared = 0
    foreach ($reproSource in @($reproTakeMap.Keys)) {
        $reproMember = [string]$reproTakeMap[$reproSource]
        $reproSourcePath = Join-Path $repoRoot $reproSource
        $reproMemberPath = Join-Path $stagingRoot ([string]$reproMember.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $reproSourcePath -PathType Leaf)) { throw ('BUNDLE_SOURCE_MISSING|' + $reproSource + '|member=' + $reproMember) }
        if (-not (Test-Path -LiteralPath $reproMemberPath -PathType Leaf)) { throw ('BUNDLE_STAGED_MEMBER_MISSING|' + $reproMember + '|source=' + (Get-Hash $reproSourcePath) + '|staged=ABSENT') }
        $reproSourceHash = Get-Hash $reproSourcePath
        $reproStagedHash = Get-Hash $reproMemberPath
        if ($reproSourceHash -cne $reproStagedHash) { throw ('BUNDLE_TAKE_DRIFT|' + $reproMember + '|source=' + $reproSourceHash + '|staged=' + $reproStagedHash) }
        $reproTakeCompared = $reproTakeCompared + 1
    }
    if ([string]::IsNullOrWhiteSpace($ReproducibilityManifestPath)) {
        $reproRefDir = Join-Path $repoRoot 'Release'
        if (-not (Test-Path -LiteralPath $reproRefDir -PathType Container)) { throw 'REPRO_REFERENCE_MISSING|no Release directory; pass -ReproducibilityManifestPath' }
        $reproRefFound = @(Get-ChildItem -LiteralPath $reproRefDir -Filter '*.zip.manifest.json' -File)
        if ($reproRefFound.Count -eq 0) { throw 'REPRO_REFERENCE_MISSING|no *.zip.manifest.json under Release' }
        $reproReferencePath = [string](@($reproRefFound | Sort-Object { [int]([regex]::Match([string]$_.Name, '-r(\d+).*\.zip\.manifest\.json').Groups[1].Value) } | Select-Object -Last 1)[0]).FullName
    } else {
        $reproReferencePath = [IO.Path]::GetFullPath($ReproducibilityManifestPath)
    }
    if (-not (Test-Path -LiteralPath $reproReferencePath -PathType Leaf)) { throw ('REPRO_REFERENCE_MISSING|' + $reproReferencePath) }
    $reproReference = Get-Content -LiteralPath $reproReferencePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $reproReferenceFiles = @{}
    foreach ($reproRec in @($reproReference.files)) { $reproReferenceFiles[[string]$reproRec.path] = ([string]$reproRec.sha256).ToUpper() }
    $reproMatched = 0
    $reproWhitelistedDifferences = @()
    $reproWhitelistUsed = @{}
    $reproUnexplained = @()
    foreach ($reproMember in @($reproReferenceFiles.Keys | Sort-Object)) {
        $reproMemberPath = Join-Path $stagingRoot ([string]$reproMember.Replace('/', '\'))
        $reproRefHash = [string]$reproReferenceFiles[$reproMember]
        if (-not (Test-Path -LiteralPath $reproMemberPath -PathType Leaf)) { $reproUnexplained += ('MISSING_MEMBER|' + $reproMember + '|staged=ABSENT|reference=' + $reproRefHash); continue }
        $reproStagedHash = Get-Hash $reproMemberPath
        if ($reproStagedHash -ceq $reproRefHash) { $reproMatched = $reproMatched + 1; continue }
        if ($reproWhitelistNames -contains $reproMember) { $reproWhitelistUsed[$reproMember] = $true; $reproWhitelistedDifferences += ('WHITELISTED|' + $reproMember + '|staged=' + $reproStagedHash + '|reference=' + $reproRefHash); continue }
        $reproUnexplained += ('MISMATCH|' + $reproMember + '|staged=' + $reproStagedHash + '|reference=' + $reproRefHash)
    }
    $reproWhitelistUnneeded = @($reproWhitelistNames | Where-Object { -not $reproWhitelistUsed.ContainsKey([string]$_) })
    if ($reproUnexplained.Count -gt 0) { throw ('BUNDLE_REPRO_MISMATCH|' + ($reproUnexplained -join ';')) }
    $reproSummary = [ordered]@{
        reference = $reproReferencePath; referenceDeploymentVersion = [string]$reproReference.deploymentVersion
        referenceFiles = $reproReferenceFiles.Count; takeCompared = $reproTakeCompared; matched = $reproMatched
        whitelistedDifferences = $reproWhitelistedDifferences; whitelistUnneeded = @($reproWhitelistUnneeded)
    }
    $records = @(Get-ChildItem -LiteralPath $stagingRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = Get-Logical $_.FullName $stagingRoot; sizeBytes = $_.Length; sha256 = Get-Hash $_.FullName }
    })
    $payloadRecords = @($layers | ForEach-Object {
        [ordered]@{ tf = $_.Tf; targetFramework = $_.TargetFramework; arcgisProDesktopVersion = $_.DesktopVersion
                    path = ('payload/' + $_.Tf + '/' + $packageFileName)
                    manifestPath = ('payload/' + $_.Tf + '/' + $manifestFileName)
                    sizeBytes = $_.SizeBytes; sha256 = $_.Sha256 }
    })
    $bundleManifest = [ordered]@{
        schema = 'arcgis-pro-mcp-one-click-bundle-v1'; deploymentVersion = $deploymentVersion; product = 'ArcGIS Pro MCP'
        version = '1.0.2'; platform = 'Windows x64'; arcGISPro = '3.5'; endpoint = 'http://127.0.0.1:6520/mcp'
        canonicalProductionToolCount = 239; cleanMachineAcceptance = 'NOT VERIFIED'; manifestSelfExcluded = $true
        sourceArtifactSha256 = $acceptedHash; payloads = $payloadRecords; files = $records
    }
    [IO.File]::WriteAllText((Join-Path $stagingRoot 'bundle-manifest.json'), ($bundleManifest | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))

    if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) { New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null }
    $temporaryZip = Join-Path $temporaryRoot ($bundleName + '.zip')
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($stagingRoot, $temporaryZip, [IO.Compression.CompressionLevel]::Optimal, $false)
    if (Test-Path -LiteralPath $zipPath -PathType Leaf) { Remove-Item -LiteralPath $zipPath -Force }
    Move-Item -LiteralPath $temporaryZip -Destination $zipPath
    $verificationLines = @(& powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $verifierPath -ZipPath $zipPath -Json 2>&1 | ForEach-Object { [string]$_ })
    $verificationExit = [int]$LASTEXITCODE
    if ($verificationExit -ne 0) { throw 'BUNDLE_ARCHIVE_AUDIT_FAILED' }
    try { $verification = ($verificationLines -join [Environment]::NewLine) | ConvertFrom-Json } catch { throw 'BUNDLE_ARCHIVE_AUDIT_OUTPUT_INVALID' }
    $zipHash = Get-Hash $zipPath
    [IO.File]::WriteAllText($hashPath, ($zipHash + '  ' + [IO.Path]::GetFileName($zipPath) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
    $externalHash = ((Get-Content -LiteralPath $hashPath -Raw -Encoding UTF8) -split '\s+')[0]
    if ([string]$externalHash -cne $zipHash) { throw 'BUNDLE_SHA256_FILE_INVALID' }
    [ordered]@{ schema = 'arcgis-pro-mcp-one-click-build-v1'; status = 'PASS'; deploymentVersion = $deploymentVersion; releaseVersion = '1.0.2'; output = $zipPath; sha256File = $hashPath; sha256 = $zipHash; archiveVerification = [string]$verification.status; reproGuard = $reproSummary; registration = 'SKIPPED'; installation = 'SKIPPED'; clientConfiguration = 'SKIPPED'; cleanMachineAcceptance = 'NOT VERIFIED' } | ConvertTo-Json -Depth 8
} finally {
    if (Test-Path -LiteralPath $temporaryRoot -PathType Container) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
