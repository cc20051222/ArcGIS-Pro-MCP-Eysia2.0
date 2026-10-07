# Build an integrity-checked binary distribution from an existing accepted
# .esriAddInX and release manifest. No build, registration, installation, or
# client configuration is performed here.
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$OutputDirectory = '',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path $repoRoot 'Release' } else { [IO.Path]::GetFullPath($OutputDirectory) }
$artifactRoot = Join-Path $repoRoot ("Source\ArcGISProMCP.Compatibility\bin\x64\$Configuration\net6.0-windows")
$packagePath = Join-Path $artifactRoot 'ArcGISProMCP.Compatibility.esriAddInX'
$manifestPath = Join-Path $artifactRoot 'ArcGISProMCP.Compatibility.release-manifest.json'

function Get-FileSha256([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function Copy-Relative([string]$relativePath, [string]$stagingRoot) {
    $source = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw ('BUNDLE_SOURCE_MISSING: ' + $relativePath) }
    $destination = Join-Path $stagingRoot $relativePath
    $directory = [IO.Path]::GetDirectoryName($destination)
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) { throw 'PACKAGE_NOT_FOUND' }
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'MANIFEST_NOT_FOUND' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string]$manifest.schema -cne 'arcgis-pro-mcp-release-manifest-v1' -or
    [string]$manifest.product.addInId -cne '{BAA5628C-3C08-4AD5-A6C0-915ADA475709}' -or
    [string]$manifest.artifact.fileName -cne [IO.Path]::GetFileName($packagePath)) { throw 'BUNDLE_IDENTITY_INVALID' }
if ([int64]$manifest.artifact.sizeBytes -ne (Get-Item -LiteralPath $packagePath).Length -or
    [string]$manifest.artifact.sha256 -cne (Get-FileSha256 $packagePath)) { throw 'BUNDLE_INTEGRITY_FAILED' }

$version = [string]$manifest.product.releaseVersion
$bundleName = "ArcGIS-Pro-MCP-$version-Windows-x64"
$zipPath = Join-Path $outputRoot ($bundleName + '.zip')
$hashPath = $zipPath + '.sha256'
if ((Test-Path -LiteralPath $zipPath -PathType Leaf) -and -not $Force) { throw 'BUNDLE_ALREADY_EXISTS_USE_FORCE' }

$temporaryRoot = Join-Path $outputRoot ('.staging-' + 'arcgis-pro-mcp-share-' + '-' + [guid]::NewGuid().ToString('N'))
$stagingRoot = Join-Path $temporaryRoot $bundleName
try {
    New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
    foreach ($relative in @(
        'Distribution\START-HERE.cmd', 'Distribution\INSTALL-PLUGIN.cmd',
        'Distribution\CONFIGURE-CODEX.cmd', 'Distribution\UNINSTALL-PLUGIN.cmd',
        'Distribution\README-START-HERE.md', 'scripts\share-setup.ps1',
        'scripts\user-workflow.ps1', 'scripts\release-transaction.ps1',
        'scripts\check-compatibility.ps1', 'scripts\client-config.ps1',
        'Config\client-catalog.json', 'Config\compatibility-policy.json',
        'Config\client-templates\codex.toml.fragment', 'Config\client-templates\cursor.mcp.json',
        'Config\client-templates\deepseek.cordis.patch.yml', 'Config\client-templates\claude-desktop.mcp.json',
        'Source\ArcGISProMCP.Compatibility\Config.daml',
        'Docs\USER_GUIDE.md', 'Docs\CLIENT_CONFIGURATION_GUIDE.md', 'Docs\SHARING_AND_SIMPLE_INSTALL.md'
    )) { Copy-Relative $relative $stagingRoot }
    # Construct the Unicode title without relying on PS 5.1 source encoding.
    $tutorialTitle = -join ([char[]]@(20998,20139,29256,35814,32454,23433,35013,19982,39564,35777,25945,31243))
    Copy-Relative ('Docs\ArcGIS_Pro_MCP_' + $tutorialTitle + '_V1.0.docx') $stagingRoot

    foreach ($name in @('START-HERE.cmd', 'INSTALL-PLUGIN.cmd', 'CONFIGURE-CODEX.cmd', 'UNINSTALL-PLUGIN.cmd', 'README-START-HERE.md')) {
        Move-Item -LiteralPath (Join-Path $stagingRoot ('Distribution\' + $name)) -Destination (Join-Path $stagingRoot $name)
    }
    Remove-Item -LiteralPath (Join-Path $stagingRoot 'Distribution') -Force

    $payloadRoot = Join-Path $stagingRoot 'payload'
    New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
    Copy-Item -LiteralPath $packagePath -Destination (Join-Path $payloadRoot ([IO.Path]::GetFileName($packagePath)))
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $payloadRoot ([IO.Path]::GetFileName($manifestPath)))

    $files = @(Get-ChildItem -LiteralPath $stagingRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = $_.FullName.Substring($stagingRoot.Length + 1).Replace('\', '/'); sizeBytes = $_.Length; sha256 = Get-FileSha256 $_.FullName }
    })
    $bundleManifest = [ordered]@{
        schema = 'arcgis-pro-mcp-share-bundle-v1'; product = 'ArcGIS Pro MCP'; version = $version
        platform = 'Windows x64'; arcGISPro = '3.5'; endpoint = 'http://127.0.0.1:6520/mcp'
        canonicalProductionToolCount = 239; cleanMachineAcceptance = 'NOT VERIFIED'
        sourceArtifactSha256 = [string]$manifest.artifact.sha256; files = $files
    }
    [IO.File]::WriteAllText((Join-Path $stagingRoot 'bundle-manifest.json'), ($bundleManifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))

    if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) { New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null }
    $temporaryZip = Join-Path $temporaryRoot ($bundleName + '.zip')
    Compress-Archive -LiteralPath $stagingRoot -DestinationPath $temporaryZip -CompressionLevel Optimal
    if (Test-Path -LiteralPath $zipPath -PathType Leaf) { Remove-Item -LiteralPath $zipPath -Force }
    Move-Item -LiteralPath $temporaryZip -Destination $zipPath
    $zipHash = Get-FileSha256 $zipPath
    [IO.File]::WriteAllText($hashPath, ($zipHash + '  ' + [IO.Path]::GetFileName($zipPath) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))

    Write-Output ([ordered]@{
        schema = 'arcgis-pro-mcp-share-build-v1'; status = 'PASS'; version = $version
        sourceConfiguration = $Configuration; registration = 'SKIPPED'; clientConfiguration = 'SKIPPED'
        cleanMachineAcceptance = 'NOT VERIFIED'; output = $zipPath; sha256File = $hashPath; sha256 = $zipHash
    } | ConvertTo-Json -Depth 6)
} finally {
    if (Test-Path -LiteralPath $temporaryRoot -PathType Container) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
