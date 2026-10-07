# ArcGIS Pro MCP - Add-in 打包与注册脚本
#
# 用途：把 Compatibility 项目的编译产物打包为 .esriAddinX 并注册到 ArcGIS Pro 3.5。
#
# 背景：官方 Esri.ProApp.SDK.Desktop.targets 使用 CodeTaskFactory 打包，
#       该任务工厂在 .NET Core MSBuild（dotnet build）中不受支持（MSB4801）；
#       本机 VS 2026 MSBuild.exe 又缺少 .NET SDK 解析器，无法构建 SDK 风格项目。
#       因此本脚本复刻官方 targets 的打包产物布局，并使用官方 RegisterAddIn.exe 注册。
#
# 产物布局（与官方 targets 一致）：
#   Config.daml            （包根）
#   Images\*               （包根）
#   Install\<编译输出全部>  （程序集、依赖、copy-local 程序集等）
#
# 用法（先 dotnet build）：
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-addin.ps1

[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [string]$ProjectDir = '',
    [switch]$SkipRegistration,
    [string]$ManifestPath = '',
    # D-020 / F7：安装后核对并（必要时）同步 AssemblyCache 运行时 bridge 脚本。
    # 缓存不会随重装可靠刷新，故此处显式核对 + 带时间戳备份后同步。
    [switch]$SkipCacheSync,
    [string]$CacheBackupRoot = ''
)

$ErrorActionPreference = 'Stop'

$assemblyName = 'ArcGISProMCP.Compatibility'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectDirResolved = if ([string]::IsNullOrWhiteSpace($ProjectDir)) {
    Join-Path $repoRoot 'Source\ArcGISProMCP.Compatibility'
} else {
    [System.IO.Path]::GetFullPath($ProjectDir)
}
$outDir = Join-Path $projectDirResolved "bin\x64\$Configuration\net6.0-windows"
$packageName = "$assemblyName.esriAddInX"
$packagePath = Join-Path $outDir $packageName
$versionPropsPath = Join-Path $repoRoot 'Directory.Build.props'
$manifestPathResolved = if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    Join-Path $outDir "$assemblyName.release-manifest.json"
} else {
    [System.IO.Path]::GetFullPath($ManifestPath)
}
$bridgeSourcePath = Join-Path $repoRoot 'Source\ArcGISProMCP.PythonBridge\bridge_runner.py'
$bridgeLogicalPath = 'Install/PythonBridge/bridge_runner.py'
$staging = Join-Path ([System.IO.Path]::GetDirectoryName($outDir)) ("arcgispro-mcp-pkg-" + [guid]::NewGuid().ToString('N'))   # D-060/G-138：暂存落输出盘，禁用系统 TEMP

function Get-ConfigInfo([string]$path) {
    try {
        [xml]$xml = Get-Content -LiteralPath $path -Raw -Encoding UTF8
        $node = $xml.SelectSingleNode("/*[local-name()='ArcGIS']/*[local-name()='AddInInfo']")
        if ($null -eq $node) { throw 'Config.daml AddInInfo node not found.' }
        $nameNode = $node.SelectSingleNode("*[local-name()='Name']")
        return [pscustomobject]@{
            Id = [string]$node.id
            Version = [string]$node.version
            DesktopVersion = [string]$node.desktopVersion
            Name = if ($null -eq $nameNode) { 'ArcGIS Pro MCP' } else { [string]$nameNode.InnerText }
        }
    } catch {
        throw "RELEASE_IDENTITY_INVALID: cannot parse Config.daml '$path': $($_.Exception.Message)"
    }
}

function Get-VersionPolicy([string]$path) {
    try {
        [xml]$xml = Get-Content -LiteralPath $path -Raw -Encoding UTF8
        $propertyGroup = $xml.SelectSingleNode("/*[local-name()='Project']/*[local-name()='PropertyGroup'][*[local-name()='Version']]")
        if ($null -eq $propertyGroup) { throw 'Version property group not found.' }
        $version = [string]$propertyGroup.Version
        $assemblyVersion = [string]$propertyGroup.AssemblyVersion
        $fileVersion = [string]$propertyGroup.FileVersion
        $informationalVersion = [string]$propertyGroup.InformationalVersion
        if ([string]::IsNullOrWhiteSpace($version) -or
            [string]::IsNullOrWhiteSpace($assemblyVersion) -or
            [string]::IsNullOrWhiteSpace($fileVersion) -or
            [string]::IsNullOrWhiteSpace($informationalVersion)) {
            throw 'Version, AssemblyVersion, FileVersion, and InformationalVersion are all required.'
        }
        return [pscustomobject]@{
            Version = $version
            AssemblyVersion = $assemblyVersion
            FileVersion = $fileVersion
            InformationalVersion = $informationalVersion
        }
    } catch {
        throw "RELEASE_IDENTITY_INVALID: cannot parse '$path': $($_.Exception.Message)"
    }
}

function Assert-Equal([string]$label, [string]$expected, [string]$actual) {
    if ($expected -cne $actual) {
        throw "RELEASE_IDENTITY_MISMATCH: $label expected '$expected' but found '$actual'."
    }
}

function Get-ZipEntryHash($entry) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = $entry.Open()
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose() }
    } finally { $sha.Dispose() }
}

function Get-ZipEntryText($entry) {
    $stream = $entry.Open()
    try {
        $reader = [IO.StreamReader]::new($stream, [Text.UTF8Encoding]::new($false))
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
}

function Get-FileSha256([string]$path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($path)
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose() }
    } finally { $sha.Dispose() }
}

function Assert-RegularFile([string]$path, [string]$label) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "PACKAGE_RUNTIME_ARTIFACT_MISSING: $label was not found."
    }

    $item = Get-Item -LiteralPath $path -Force
    if ($item.PSIsContainer -or
        (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        throw "PACKAGE_RUNTIME_ARTIFACT_INVALID: $label is not an owned regular file."
    }

    $parent = $item.Directory
    while ($null -ne $parent) {
        if (($parent.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "PACKAGE_RUNTIME_ARTIFACT_INVALID: $label has a reparse-point ancestor."
        }

        $next = $parent.Parent
        if ($null -eq $next -or $next.FullName -eq $parent.FullName) { break }
        $parent = $next
    }
}

function Assert-PackageIdentity(
    [string]$path,
    [pscustomobject]$configInfo,
    [pscustomobject]$versionPolicy,
    [string]$expectedPackageName) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entries = @($zip.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.FullName) })
        $normalized = @($entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        $nested = @($normalized | Where-Object { $_ -match '(?i)\.esriAddInX$' })
        if ($nested.Count -gt 0) { throw "PACKAGE_CONTENT_INVALID: nested esriAddInX entries: $($nested -join ', ')" }

        $required = @('Config.daml', 'Images/AddInIcon.png', "Install/$expectedPackageName.dll", $bridgeLogicalPath)
        foreach ($name in $required) {
            if ($normalized -notcontains $name) { throw "PACKAGE_CONTENT_INVALID: missing core package entry '$name'." }
        }

        Assert-RegularFile $bridgeSourcePath 'bridge_runner.py source'
        $bridgeEntries = @($zip.Entries | Where-Object {
            $_.FullName.Replace('\', '/') -ceq $bridgeLogicalPath
        })
        $bridgeNamedEntries = @($zip.Entries | Where-Object {
            [IO.Path]::GetFileName($_.FullName.Replace('\', '/')) -ceq 'bridge_runner.py'
        })
        if ($bridgeEntries.Count -ne 1 -or $bridgeNamedEntries.Count -ne 1) {
            throw 'PACKAGE_RUNTIME_ARTIFACT_INVALID: bridge_runner.py must appear exactly once at the owned logical path.'
        }
        $bridgeEntry = $bridgeEntries[0]
        $bridgeSourceItem = Get-Item -LiteralPath $bridgeSourcePath -Force
        if ([int64]$bridgeEntry.Length -ne [int64]$bridgeSourceItem.Length) {
            throw 'PACKAGE_RUNTIME_ARTIFACT_MISMATCH: bridge_runner.py byte length differs from source.'
        }
        $bridgeHash = Get-ZipEntryHash $bridgeEntry
        Assert-Equal $bridgeLogicalPath (Get-FileSha256 $bridgeSourcePath) $bridgeHash
        $bridgeArtifact = [pscustomobject]@{
            logicalPath = $bridgeLogicalPath
            kind = 'python-bridge-script'
            sizeBytes = [int64]$bridgeEntry.Length
            sha256 = $bridgeHash
        }

        $rootConfig = $zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -ceq 'Config.daml' } | Select-Object -First 1
        $packageConfig = Get-ConfigInfoFromText (Get-ZipEntryText $rootConfig)
        Assert-Equal 'package root Config.daml version' $configInfo.Version $packageConfig.Version
        Assert-Equal 'package root Config.daml id' $configInfo.Id $packageConfig.Id

        $assemblyEntries = @($zip.Entries | Where-Object {
            $_.FullName.Replace('\', '/') -match '^Install/ArcGISProMCP\..+\.dll$'
        })
        if ($assemblyEntries.Count -eq 0) { throw 'PACKAGE_CONTENT_INVALID: no first-party Install DLLs found.' }

        $inventory = @()
        foreach ($entry in $assemblyEntries) {
            $relative = $entry.FullName.Replace('\', '/')
            $sourcePath = Join-Path $outDir ([IO.Path]::GetFileName($relative))
            if (-not (Test-Path -LiteralPath $sourcePath)) {
                throw "PACKAGE_CONTENT_INVALID: source assembly missing for '$relative'."
            }
            $entryHash = Get-ZipEntryHash $entry
            $sourceHash = Get-FileSha256 $sourcePath
            Assert-Equal "$relative source/ZIP SHA256" $sourceHash $entryHash
            $vi = [Diagnostics.FileVersionInfo]::GetVersionInfo($sourcePath)
            $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($sourcePath).Version.ToString()
            Assert-Equal "$relative AssemblyVersion" $versionPolicy.AssemblyVersion $assemblyVersion
            Assert-Equal "$relative FileVersion" $versionPolicy.FileVersion $vi.FileVersion
            Assert-Equal "$relative ProductVersion" $versionPolicy.InformationalVersion $vi.ProductVersion
            $inventory += [pscustomobject]@{
                path = $relative
                fileName = [IO.Path]::GetFileName($relative)
                sizeBytes = [int64]$entry.Length
                sha256 = $entryHash
                assemblyVersion = $assemblyVersion
                fileVersion = $vi.FileVersion
                productVersion = $vi.ProductVersion
                informationalVersion = $versionPolicy.InformationalVersion
            }
        }

        return [pscustomobject]@{
            Entries = $entries
            NormalizedNames = $normalized
            FirstPartyAssemblies = $inventory
            RuntimeArtifacts = @($bridgeArtifact)
        }
    } finally { $zip.Dispose() }
}

function Get-ConfigInfoFromText([string]$text) {
    try {
        [xml]$xml = $text
        $node = $xml.SelectSingleNode("/*[local-name()='ArcGIS']/*[local-name()='AddInInfo']")
        if ($null -eq $node) { throw 'Config.daml AddInInfo node not found.' }
        return [pscustomobject]@{ Id = [string]$node.id; Version = [string]$node.version }
    } catch { throw "PACKAGE_CONTENT_INVALID: cannot parse package root Config.daml: $($_.Exception.Message)" }
}

Write-Host "=== ArcGIS Pro MCP Add-in packaging ==="
Write-Host ("ProjectDir : " + $projectDirResolved)
Write-Host ("OutDir     : " + $outDir)
Write-Host ("Package    : " + $packagePath)

try {
    if (-not (Test-Path -LiteralPath $versionPropsPath)) {
        throw "RELEASE_IDENTITY_INVALID: centralized version source not found at '$versionPropsPath'."
    }
    Assert-RegularFile $bridgeSourcePath 'bridge_runner.py source'
    $versionPolicy = Get-VersionPolicy $versionPropsPath
    $configPath = Join-Path $projectDirResolved 'Config.daml'
    $configInfo = Get-ConfigInfo $configPath
    Assert-Equal 'Config.daml version' $versionPolicy.Version $configInfo.Version

    if (-not (Test-Path (Join-Path $outDir "$assemblyName.dll"))) {
        throw "Add-in assembly not built. Run 'dotnet build' first."
    }
    $packagePathFull = [IO.Path]::GetFullPath($packagePath)
    $manifestPathFull = [IO.Path]::GetFullPath($manifestPathResolved)
    $sourcePackages = @(Get-ChildItem -LiteralPath $outDir -Filter '*.esriAddInX' -File -ErrorAction SilentlyContinue |
        Where-Object { [IO.Path]::GetFullPath($_.FullName) -ne $packagePathFull })
    if ($sourcePackages.Count -gt 0) {
        throw "PACKAGE_CONTENT_INVALID: nested/stale .esriAddInX source output detected: $($sourcePackages.Name -join ', ')"
    }

    $mainDll = Join-Path $outDir "$assemblyName.dll"
    $mainVersionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($mainDll)
    Assert-Equal 'main DLL FileVersion' $versionPolicy.FileVersion $mainVersionInfo.FileVersion
    Assert-Equal 'main DLL ProductVersion' $versionPolicy.InformationalVersion $mainVersionInfo.ProductVersion

    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'Install') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'Images') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'Install\PythonBridge') | Out-Null

    # 1. Config.daml -> 包根
    Copy-Item (Join-Path $projectDirResolved 'Config.daml') (Join-Path $staging 'Config.daml') -Force

    # 2. Images -> 包根
    if (Test-Path (Join-Path $projectDirResolved 'Images')) {
        Copy-Item (Join-Path $projectDirResolved 'Images\*') (Join-Path $staging 'Images') -Recurse -Force
    }

    # 3. 编译输出全部 -> Install\（先确保 OutDir 无遗留 .esriAddInX，避免嵌套）
    Remove-Item -LiteralPath $packagePathFull -Force -ErrorAction SilentlyContinue
    $copyItems = @(Get-ChildItem -LiteralPath $outDir -Force |
        Where-Object {
            $fullPath = [IO.Path]::GetFullPath($_.FullName)
            $fullPath -ne $packagePathFull -and $fullPath -ne $manifestPathFull
        })
    if ($copyItems.Count -gt 0) {
        Copy-Item -LiteralPath $copyItems.FullName -Destination (Join-Path $staging 'Install') -Recurse -Force
    }

    # 4. 压缩为 .zip 后改名为 .esriAddinX（Compress-Archive 只接受 .zip 扩展名）
    $zip = "$staging.zip"
    Copy-Item -LiteralPath $bridgeSourcePath -Destination (Join-Path $staging 'Install\PythonBridge\bridge_runner.py') -Force
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -Force
    Move-Item -Force $zip $packagePath

    $size = (Get-Item $packagePath).Length
    Write-Host ("Packaged: " + $packagePath) -ForegroundColor Green
    Write-Host ("Size    : " + $size + " bytes")

    $packageAudit = Assert-PackageIdentity $packagePath $configInfo $versionPolicy $assemblyName
    $artifactHash = Get-FileSha256 $packagePath
    $manifest = [ordered]@{
        schema = 'arcgis-pro-mcp-release-manifest-v1'
        product = [ordered]@{
            name = $configInfo.Name
            addInId = $configInfo.Id
            releaseVersion = $versionPolicy.Version
            assemblyVersion = $versionPolicy.AssemblyVersion
            assemblyVersionPolicy = 'Intentional binary compatibility identity; not a release-version mismatch.'
        }
        target = [ordered]@{
            arcgisProDesktopVersion = $configInfo.DesktopVersion
            targetFramework = 'net6.0-windows'
            platform = 'x64'
        }
        artifact = [ordered]@{
            fileName = $packageName
            sizeBytes = [int64]$size
            sha256 = $artifactHash
        }
        firstPartyAssemblies = @($packageAudit.FirstPartyAssemblies)
        runtimeArtifacts = @($packageAudit.RuntimeArtifacts)
        packageContentSummary = [ordered]@{
            totalEntries = $packageAudit.Entries.Count
            rootEntries = @($packageAudit.NormalizedNames | Where-Object { $_ -notmatch '/' }).Count
            installEntries = @($packageAudit.NormalizedNames | Where-Object { $_ -like 'Install/*' }).Count
            firstPartyAssemblyCount = $packageAudit.FirstPartyAssemblies.Count
            runtimeArtifactCount = $packageAudit.RuntimeArtifacts.Count
            runtimeArtifacts = @($packageAudit.RuntimeArtifacts | ForEach-Object { $_.logicalPath })
            nestedAddInCount = 0
            coreFiles = @('Config.daml', 'Images/AddInIcon.png', "Install/$assemblyName.dll", $bridgeLogicalPath)
        }
    }
    $json = $manifest | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText($manifestPathResolved, $json, [Text.UTF8Encoding]::new($false))
    Write-Host ("Manifest : " + $manifestPathResolved) -ForegroundColor Green

    # 5. 使用官方 RegisterAddIn.exe 注册
    if ($SkipRegistration) {
        Write-Host 'Registration: SKIPPED (-SkipRegistration)' -ForegroundColor Yellow
    } else {
        $register = 'C:\Program Files\ArcGIS\Pro\bin\RegisterAddIn.exe'
        if (Test-Path $register) {
            $p = Start-Process -FilePath $register -ArgumentList @("`"$packagePath`"", '/s') -Wait -PassThru
            Write-Host ("RegisterAddIn exit code: " + $p.ExitCode)
            if ($p.ExitCode -ne 0) {
                Write-Warning "RegisterAddIn returned non-zero exit code."
            }
        } else {
            Write-Warning "RegisterAddIn.exe not found; add-in NOT registered."
        }
    }

    # 6. D-020 / F7 —— AssemblyCache 运行时 bridge 脚本核对与同步
    #    三处哈希必须一致：缓存脚本 == 包内脚本 == 源码脚本。
    #    不同步的后果：python 侧修复"已打包"却在运行态不生效（D-018 实测教训）。
    if ($SkipCacheSync) {
        Write-Host 'AssemblyCache bridge sync: SKIPPED (-SkipCacheSync)' -ForegroundColor Yellow
    } else {
        $verifyScript = Join-Path $PSScriptRoot 'verify-assemblycache-bridge.ps1'
        if (-not (Test-Path -LiteralPath $verifyScript -PathType Leaf)) {
            throw "CACHE_SYNC_SCRIPT_MISSING: '$verifyScript' not found."
        }
        $backupRootResolved = if ([string]::IsNullOrWhiteSpace($CacheBackupRoot)) {
            Join-Path $repoRoot '.runtime\evolution\phase08\assemblycache-backup'
        } else { [System.IO.Path]::GetFullPath($CacheBackupRoot) }

        $syncArgs = @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $verifyScript,
            '-PackagePath', $packagePath,
            '-AddInId', $configInfo.Id,
            '-Sync',
            '-BackupRoot', $backupRootResolved
        )
        Write-Host '=== AssemblyCache bridge script check/sync (D-020) ===' -ForegroundColor Cyan
        & 'powershell' $syncArgs
        $syncExit = $LASTEXITCODE
        if ($syncExit -ne 0) {
            throw "CACHE_SYNC_FAILED: AssemblyCache bridge script check/sync exited $syncExit (see output above)."
        }
    }
}
finally {
    Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
    if (Test-Path "$staging.zip") { Remove-Item -Force "$staging.zip" -ErrorAction SilentlyContinue }
}
