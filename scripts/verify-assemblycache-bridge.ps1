# ArcGIS Pro MCP — AssemblyCache 与插件包逐条目核对 / bridge 脚本同步 / 进程回收
#
# 来源：派工单 D-020（F7 发布流程修复），依据 GATE-D018_VERDICT.md §2（F7 裁定）。
# 加固：派工单 D-030（P1 相对路径对齐 / P2 参数别名 / P3 fail-closed / P4 entries[]）。
# 返工 r2：GATE-D030_VERDICT.md M1（收窄判定面，修复 F13 工具链阻断缺陷）。
#
# 背景（D-018 实测）：运行时 bridge 脚本与 dll 取自 Pro 的 AssemblyCache：
#   %LOCALAPPDATA%\ESRI\ArcGISPro\AssemblyCache\{<addin-id>}\PythonBridge\bridge_runner.py
# 该缓存曾长期停留在旧脚本，导致"修复已打包但复验仍失败"。
# 结论：**不能假设重装会刷新缓存** → 流程必须显式核对 + 必要时同步。
# 影响面：python 侧脚本 + dll/pdb/deps.json（C# 编译进 dll，随包加载，但缓存副本才是运行态来源）。
#
# 三处哈希必须一致：缓存 == 包内 == 源码（源码仅对 bridge 脚本成立，dll 以包内为准）。
#
# 用法：
#   # 只读核对（默认；不写任何文件）。**必须显式给 -PackagePath**，否则 fail-closed 拒绝出结论
#   .\scripts\verify-assemblycache-bridge.ps1 -PackagePath <path-to.esriAddInX>
#
#   # 核对并在漂移时同步 bridge 脚本（会先做带时间戳的备份）
#   .\scripts\verify-assemblycache-bridge.ps1 -PackagePath <pkg> -Sync -BackupRoot <dir>
#   # -BackupRoot 的同义别名 -CacheBackupRoot 亦可用（D-030 P2，向后兼容 package-addin.ps1）
#
#   # 同步后按 D-017 F5 归属门回收长驻 bridge 进程（祖先链须含 ArcGISPro.exe）
#   .\scripts\verify-assemblycache-bridge.ps1 -PackagePath <pkg> -Sync -RecycleBridgeProcess
#
#   # 沙盒演练：-CacheRoot 指向镜像目录，逻辑与真实路径完全一致
#   .\scripts\verify-assemblycache-bridge.ps1 -PackagePath <pkg> -CacheRoot <mirror-root> -Sync
#
#   # 审计面：把成功判据扩到 Install/ 全部条目（默认只判 bridge）
#   .\scripts\verify-assemblycache-bridge.ps1 -PackagePath <pkg> -Scope all
#
# ---- D-030 r2（F13 修复）：判定范围 -Scope ----
#   bridge（**默认**，= D-030 之前的既有语义）：退出码只由 bridge 脚本是否收敛决定。
#       「源码改动 → 重新打包」时缓存是旧构建、包是新构建，dll/pdb 必然不同；
#       若把成功判据扩到全部条目，则 package-addin.ps1（exit≠0 即 throw CACHE_SYNC_FAILED）
#       每次打包都会中止 —— 这是 F13 的根因，故默认必须回到 bridge 语义。
#   all（**显式审计面**）：Install/ 下 18 条全部相同才算 IN_SYNC；供人工/验收审计用。
#   两层共同不变式：① bridge 未收敛（DRIFT）恒可见且非 0；② 条目漂移在 entries[] 中逐条如实登记，
#       且以独立状态词 IN_SYNC_ENTRIES_DRIFT 呈现，**绝不裸报 IN_SYNC**；③ 异常路径不可能得 0。
#
# 退出码（D-030 P3，fail-closed：**0 只能来自成功族**）：
#   0 = IN_SYNC                  （-Scope all）18 条全同；或（bridge）全条目本就一致
#   0 = IN_SYNC_ENTRIES_DRIFT    bridge 已收敛，但存在非 bridge 条目漂移（仅 -Scope bridge 可达，
#                                不掩盖：entries[] 逐条登记 + entriesDriftCount + 控制台告警行）
#   2 = DRIFT                    命中但哈希不一致（未 -Sync 或未收敛）
#   3 = CACHE_MISSING            缓存目录不存在或为空（无法比较）
#   4 = UNVERIFIED_NO_PACKAGE    未提供 -PackagePath（**禁止输出 IN_SYNC**）
#   5 = INCOMPARABLE             包侧可比条目为 0 / 命中数不足 / bridge 条目缺失
#   6 = PACKAGE_MISSING          -PackagePath 指向的文件不存在
#   9 = ERROR                    未处理异常（顶层 try/catch 兜底）
#
# 状态词与退出码一一对应，任何异常路径都不可能得到 0。

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SourceScript = '',
    [string]$PackagePath = '',
    [string]$AddInId = '',
    [string]$CacheRoot = '',
    [Alias('CacheBackupRoot')]
    [string]$BackupRoot = '',
    [string]$ConfigDaml = '',
    # D-030 r2 / M1：判定范围。**默认 bridge** = 既有语义（保 package-addin.ps1 管线）；all = 审计面。
    [ValidateSet('bridge', 'all')]
    [string]$Scope = 'bridge',
    [switch]$Sync,
    [switch]$RecycleBridgeProcess,
    [string]$ResultPath = ''
)

$ErrorActionPreference = 'Stop'

$script:BridgeLogicalPath = 'Install/PythonBridge/bridge_runner.py'
$script:BridgeRelativePath = 'PythonBridge/bridge_runner.py'
$script:PackageInstallPrefix = 'Install/'
$script:HostProcessName = 'ArcGISPro.exe'

# D-030 P3：状态 -> 退出码。
# 0 只能来自「成功族」两位成员：IN_SYNC（全同）与 IN_SYNC_ENTRIES_DRIFT（bridge 收敛、非 bridge 漂移，
# 仅 -Scope bridge 可达，且漂移在 entries[]/entriesDriftCount 中如实登记）。其余状态一律非 0。
$script:ExitCodeByStatus = @{
    'IN_SYNC'               = 0
    'IN_SYNC_ENTRIES_DRIFT' = 0
    'DRIFT'                 = 2
    'CACHE_MISSING'         = 3
    'UNVERIFIED_NO_PACKAGE' = 4
    'INCOMPARABLE'          = 5
    'PACKAGE_MISSING'       = 6
    'ERROR'                 = 9
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceScriptResolved = if ([string]::IsNullOrWhiteSpace($SourceScript)) {
    Join-Path $repoRoot 'Source\ArcGISProMCP.PythonBridge\bridge_runner.py'
} else { [System.IO.Path]::GetFullPath($SourceScript) }

$configDamlResolved = if ([string]::IsNullOrWhiteSpace($ConfigDaml)) {
    Join-Path $repoRoot 'Source\ArcGISProMCP.Compatibility\Config.daml'
} else { [System.IO.Path]::GetFullPath($ConfigDaml) }

$cacheRootResolved = if ([string]::IsNullOrWhiteSpace($CacheRoot)) {
    Join-Path $env:LOCALAPPDATA 'ESRI\ArcGISPro\AssemblyCache'
} else { [System.IO.Path]::GetFullPath($CacheRoot) }

function Get-FileSha256([string]$path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($path)
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose() }
    } finally { $sha.Dispose() }
}

function Get-AddInIdFromConfig([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "CACHE_SYNC_CONFIG_MISSING: Config.daml not found at '$path'."
    }
    [xml]$xml = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $node = $xml.SelectSingleNode("/*[local-name()='ArcGIS']/*[local-name()='AddInInfo']")
    if ($null -eq $node -or [string]::IsNullOrWhiteSpace([string]$node.id)) {
        throw "CACHE_SYNC_CONFIG_INVALID: cannot resolve addIn id from '$path'."
    }
    return [string]$node.id
}

function Get-ZipEntrySha256([string]$zipPath, [string]$logicalPath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $sha = [Security.Cryptography.SHA256]::Create()
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entry = @($zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -ceq $logicalPath })
        if ($entry.Count -ne 1) {
            throw "CACHE_SYNC_PACKAGE_INVALID: '$logicalPath' must appear exactly once in '$zipPath'."
        }
        $stream = $entry[0].Open()
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose() }
    } finally { $zip.Dispose(); $sha.Dispose() }
}

# D-030 P1：一次打开包，返回「相对路径键 -> 哈希」的可比集合 + 被排除条目。
# 键规则：去掉 'Install/' 前缀，其余保持原样（分隔符统一 '/'）。
# 不在 'Install/' 下的条目（Config.daml / Images/*.png）**不会**出现在 AssemblyCache，故排除并登记原因。
function Get-PackageEntryMap([string]$zipPath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $sha = [Security.Cryptography.SHA256]::Create()
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    $comparable = [ordered]@{}
    $excluded = @()
    try {
        foreach ($e in $zip.Entries) {
            $full = $e.FullName.Replace('\', '/')
            if ([string]::IsNullOrEmpty($full) -or $full.EndsWith('/')) { continue }
            if (-not $full.StartsWith($script:PackageInstallPrefix)) {
                $excluded += [ordered]@{ logicalPath = $full; reason = 'not-under-Install (never mirrored into AssemblyCache)' }
                continue
            }
            $key = $full.Substring($script:PackageInstallPrefix.Length)
            $stream = $e.Open()
            try {
                $comparable[$key] = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '')
            } finally { $stream.Dispose() }
        }
    } finally { $zip.Dispose(); $sha.Dispose() }
    return [ordered]@{ comparable = $comparable; excluded = $excluded }
}

# D-030 P1：缓存侧 **-Recurse** 枚举（修复子目录条目被漏掉 → 误报 MISSING_IN_CACHE）。
# 键规则：相对 '{addin-id}\' 的路径，分隔符统一 '/'。
function Get-CacheEntryMap([string]$addinDir) {
    $map = @{}
    if (-not (Test-Path -LiteralPath $addinDir -PathType Container)) { return $map }
    $baseLen = $addinDir.TrimEnd('\', '/').Length
    foreach ($f in @(Get-ChildItem -LiteralPath $addinDir -Recurse -File -ErrorAction Stop)) {
        $rel = $f.FullName.Substring($baseLen).TrimStart('\', '/').Replace('\', '/')
        if ([string]::IsNullOrEmpty($rel)) { continue }
        $map[$rel] = $f
    }
    return $map
}

# D-030 r2 / M1：成功判据按 -Scope 分层（唯一判定入口，前后两次判定同源）。
#   bridge（默认）：只看 bridge 是否收敛；非 bridge 漂移 → IN_SYNC_ENTRIES_DRIFT（仍 exit 0，不裸报 IN_SYNC）。
#   all（审计面）：Install/ 全部条目必须全同。
# 不变式：DRIFT（bridge 未同步）恒非 0；包侧无可比条目 / 缓存不可用 / bridge 缺失 → 非 0。
function Resolve-Status(
    [int]$packageEntryCount, [bool]$cacheDirExists, [int]$comparableCount, [int]$diffCount,
    [int]$entriesDriftCount, [string]$bridgeVerdictValue, [bool]$bridgeInPackage, [string]$scopeValue
) {
    if ($packageEntryCount -eq 0) { return 'INCOMPARABLE' }
    if ((-not $cacheDirExists) -or $comparableCount -eq 0) { return 'CACHE_MISSING' }
    if ($scopeValue -eq 'all') {
        if ($comparableCount -lt $packageEntryCount) { return 'INCOMPARABLE' }
        if ($diffCount -gt 0) { return 'DRIFT' }
        return 'IN_SYNC'
    }
    if (-not $bridgeInPackage) { return 'INCOMPARABLE' }
    if ($bridgeVerdictValue -eq 'MISSING_IN_CACHE') { return 'INCOMPARABLE' }
    if ($bridgeVerdictValue -eq 'DIFF') { return 'DRIFT' }
    if ($entriesDriftCount -gt 0) { return 'IN_SYNC_ENTRIES_DRIFT' }
    return 'IN_SYNC'
}

# D-030 r2：**非 bridge** 条目漂移汇总（DIFF / MISSING_IN_CACHE；EXTRA_IN_CACHE 只报不计失败）。
# 与 entries[] 同源，避免"计数与明细不一致"。
function Get-EntryDriftSummary([array]$entries) {
    $d = @($entries | Where-Object {
        ($_.verdict -eq 'DIFF' -or $_.verdict -eq 'MISSING_IN_CACHE') -and
        ($_.relativePath -ne $script:BridgeRelativePath)
    })
    return [ordered]@{ count = $d.Count; paths = @($d | ForEach-Object { $_.relativePath }) }
}

# ---- D-017 F5 归属门：kill 之前必须验证祖先链含宿主进程 ----
function Get-AncestryChain([int]$processId, [int]$maxDepth = 10) {
    $chain = @()
    $cur = Get-CimInstance Win32_Process -Filter "ProcessId=$processId" -ErrorAction SilentlyContinue
    for ($i = 0; $i -lt $maxDepth -and $null -ne $cur; $i++) {
        $chain += ('{0}({1})' -f $cur.Name, $cur.ProcessId)
        $parentId = [int]$cur.ParentProcessId
        if ($parentId -le 0) { break }
        $cur = Get-CimInstance Win32_Process -Filter "ProcessId=$parentId" -ErrorAction SilentlyContinue
    }
    return $chain
}

function Assert-PidOwnedByHost([int]$processId, [string]$hostName) {
    $chain = Get-AncestryChain $processId
    $names = @($chain | ForEach-Object { ($_ -split '\(')[0].ToLowerInvariant() })
    if ($names -notcontains $hostName.ToLowerInvariant()) {
        throw "[ABORT] kill refused: PID $processId ancestry [$($chain -join ' <- ')] does not contain '$hostName'. F5 归属门未通过，禁止注入。"
    }
    Write-Host ("[GUARD] PID $processId ancestry verified: " + ($chain -join ' <- ')) -ForegroundColor DarkGray
}

function Get-BridgeProcesses([string]$scriptPath) {
    return @(Get-CimInstance Win32_Process -Filter "Name='python.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine -like "*$scriptPath*" })
}

# ---------------- 结果容器与统一出口（fail-closed） ----------------
$result = [ordered]@{
    schema                 = 'arcgis-pro-mcp-assemblycache-bridge-check-v2'
    dispatch               = 'D-020'
    hardenedBy             = 'D-030'
    timestampUtc           = [DateTime]::UtcNow.ToString('o')
    addInId                = $null
    cacheRoot              = $cacheRootResolved
    cacheAddinDir          = $null
    cacheScript            = $null
    sourceScript           = $sourceScriptResolved
    sourceSha256           = $null
    sourceBytes            = $null
    packagePath            = $null
    scope                  = $Scope         # D-030 r2：bridge（默认）| all（审计面）
    packageSha256          = $null          # bridge 脚本在包内的哈希（兼容 v1 字段）
    expectedHash           = $null
    expectedFrom           = $null
    cacheSha256            = $null          # bridge 脚本在缓存的哈希（兼容 v1 字段）
    cacheBytes             = $null
    packageEntryCount      = 0              # 包内可比条目数（Install/ 下）
    comparableEntryCount   = 0              # D-030 P3：实际参与比较的条目数
    sameCount              = 0
    diffCount              = 0
    missingInCacheCount    = 0
    extraInCacheCount      = 0
    bridgeVerdict          = $null          # D-030 r2：bridge 条目判定（SAME/DIFF/MISSING_IN_CACHE）
    entriesDriftCount      = 0              # D-030 r2：非 bridge 条目漂移数（DIFF + MISSING_IN_CACHE）
    entriesDrift           = @()            # D-030 r2：漂移条目清单（不静默）
    excludedFromPackage    = @()
    entries                = @()            # D-030 P4
    status                 = 'ERROR'
    exitCode               = 9
    action                 = 'none'
    backupPath             = $null
    recycledPids           = @()
    error                  = $null
}

function Write-ResultFile {
    if ([string]::IsNullOrWhiteSpace($ResultPath)) { return }
    $resultPathFull = [System.IO.Path]::GetFullPath($ResultPath)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resultPathFull) | Out-Null
    ($result | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $resultPathFull -Encoding UTF8
    Write-Host ("[RESULT] " + $resultPathFull)
}

function Complete-AndExit([string]$status, [string]$message) {
    $code = $script:ExitCodeByStatus[$status]
    if ($null -eq $code) { $code = 9 }
    $result.status = $status
    $result.exitCode = $code
    $result.timestampUtc = [DateTime]::UtcNow.ToString('o')
    $color = switch ($status) {
        'IN_SYNC' { 'Green' }
        'IN_SYNC_ENTRIES_DRIFT' { 'Yellow' }
        'DRIFT' { 'Red' }
        'ERROR' { 'Red' }
        'PACKAGE_MISSING' { 'Red' }
        default { 'Yellow' }
    }
    Write-Host ("STATUS      : " + $status + "  (exit " + $code + ")") -ForegroundColor $color
    if ($message) { Write-Host ("  " + $message) -ForegroundColor $color }
    Write-ResultFile
    exit $code
}

# ---------------- 主体（顶层 try/catch，异常一律 ERROR + exit 9） ----------------
try {
    Write-Host '=== AssemblyCache 逐条目核对 (D-020 / F7，D-030 加固) ===' -ForegroundColor Cyan

    $addInIdResolved = if ([string]::IsNullOrWhiteSpace($AddInId)) { Get-AddInIdFromConfig $configDamlResolved } else { $AddInId }
    # Config.daml 的 id 自带花括号，参数可能带也可能不带 —— 统一归一化为「无花括号」形式，
    # 由本脚本在拼接缓存路径时统一加花括号，避免出现 '{{guid}}' 双重包裹。
    $addInIdResolved = $addInIdResolved.Trim().Trim('{', '}')
    $result.addInId = $addInIdResolved

    # 只构造"本插件"的精确路径；绝不枚举/触碰其它 addin 目录
    $cacheAddinDir = Join-Path $cacheRootResolved ('{' + $addInIdResolved + '}')
    $cacheScript = Join-Path $cacheAddinDir 'PythonBridge\bridge_runner.py'
    $result.cacheAddinDir = $cacheAddinDir
    $result.cacheScript = $cacheScript

    if (-not (Test-Path -LiteralPath $sourceScriptResolved -PathType Leaf)) {
        throw "CACHE_SYNC_SOURCE_MISSING: source bridge script not found at '$sourceScriptResolved'."
    }
    $sourceHash = Get-FileSha256 $sourceScriptResolved
    $sourceBytes = (Get-Item -LiteralPath $sourceScriptResolved).Length
    $result.sourceSha256 = $sourceHash
    $result.sourceBytes = $sourceBytes

    Write-Host ("AddInId     : {" + $addInIdResolved + "}")
    Write-Host ("CacheRoot   : " + $cacheRootResolved)
    Write-Host ("CacheDir    : " + $cacheAddinDir)
    Write-Host ("Source      : " + $sourceScriptResolved)
    Write-Host ("  source sha256 = " + $sourceHash + " (" + $sourceBytes + " B)")

    # ---- D-030 P3 闸门 0：未提供 -PackagePath 一律 fail-closed，禁止任何 IN_SYNC ----
    if ([string]::IsNullOrWhiteSpace($PackagePath)) {
        $cacheEnumOnly = Get-CacheEntryMap $cacheAddinDir
        $result.packageEntryCount = 0
        $result.comparableEntryCount = 0
        $result.entries = @($cacheEnumOnly.Keys | Sort-Object | ForEach-Object {
            [ordered]@{
                relativePath = $_
                packageHash  = $null
                cacheHash    = $null
                verdict      = 'NOT_COMPARABLE_NO_PACKAGE'
            }
        })
        Write-Host '  -PackagePath 未提供 → 包侧校验不可用（旧版会静默回落源码并可能报『已同步』，属假绿）' -ForegroundColor Yellow
        Complete-AndExit 'UNVERIFIED_NO_PACKAGE' '未提供 -PackagePath：不产出任何一致性结论（fail-closed）。'
    }

    $packagePathFull = [System.IO.Path]::GetFullPath($PackagePath)
    $result.packagePath = $packagePathFull
    if (-not (Test-Path -LiteralPath $packagePathFull -PathType Leaf)) {
        $result.error = "CACHE_SYNC_PACKAGE_MISSING: package not found at '$packagePathFull'."
        Write-Host ("  package not found: " + $packagePathFull) -ForegroundColor Red
        Complete-AndExit 'PACKAGE_MISSING' $result.error
    }

    $packageHash = Get-ZipEntrySha256 $packagePathFull $script:BridgeLogicalPath
    $result.packageSha256 = $packageHash
    $result.expectedHash = $packageHash
    $result.expectedFrom = 'package'
    Write-Host ("  package sha256= " + $packageHash + "  (bridge, expected from package)")

    # ---- P1：包侧 / 缓存侧按「相对路径键」对齐 ----
    $pkg = Get-PackageEntryMap $packagePathFull
    $pkgComparable = $pkg.comparable
    $result.packageEntryCount = $pkgComparable.Count
    $result.excludedFromPackage = @($pkg.excluded)

    $cacheMap = Get-CacheEntryMap $cacheAddinDir
    $cacheDirExists = Test-Path -LiteralPath $cacheAddinDir -PathType Container

    $entries = @()
    $same = 0; $diff = 0; $missing = 0; $comparable = 0
    $bridgeVerdict = $null
    $bridgeCacheHash = $null
    $bridgeCacheBytes = $null

    foreach ($key in @($pkgComparable.Keys | Sort-Object)) {
        $ph = $pkgComparable[$key]
        if ($cacheMap.ContainsKey($key)) {
            $comparable++
            $ch = Get-FileSha256 $cacheMap[$key].FullName
            if ($ch -eq $ph) { $v = 'SAME'; $same++ } else { $v = 'DIFF'; $diff++ }
            if ($key -eq $script:BridgeRelativePath) {
                $bridgeVerdict = $v
                $bridgeCacheHash = $ch
                $bridgeCacheBytes = $cacheMap[$key].Length
            }
            $entries += [ordered]@{ relativePath = $key; packageHash = $ph; cacheHash = $ch; verdict = $v }
        } else {
            $missing++
            if ($key -eq $script:BridgeRelativePath) { $bridgeVerdict = 'MISSING_IN_CACHE' }
            $entries += [ordered]@{ relativePath = $key; packageHash = $ph; cacheHash = $null; verdict = 'MISSING_IN_CACHE' }
        }
    }

    $extra = 0
    foreach ($key in @($cacheMap.Keys | Sort-Object)) {
        if (-not $pkgComparable.Contains($key)) {
            $extra++
            $entries += [ordered]@{
                relativePath = $key
                packageHash  = $null
                cacheHash    = Get-FileSha256 $cacheMap[$key].FullName
                verdict      = 'EXTRA_IN_CACHE'
            }
        }
    }

    $drift = Get-EntryDriftSummary $entries
    $result.comparableEntryCount = $comparable
    $result.sameCount = $same
    $result.diffCount = $diff
    $result.missingInCacheCount = $missing
    $result.extraInCacheCount = $extra
    $result.bridgeVerdict = $bridgeVerdict
    $result.entriesDriftCount = $drift.count
    $result.entriesDrift = @($drift.paths)
    $result.entries = $entries
    $result.cacheSha256 = $bridgeCacheHash
    $result.cacheBytes = $bridgeCacheBytes

    if ($bridgeCacheHash) { Write-Host ("  cache   sha256= " + $bridgeCacheHash + " (" + $bridgeCacheBytes + " B)  [bridge]") }
    else { Write-Host '  cache   sha256= <missing>  [bridge]' }
    Write-Host ("  比较条目: 包侧 " + $pkgComparable.Count + " / 命中 " + $comparable +
                " / SAME " + $same + " / DIFF " + $diff + " / MISSING_IN_CACHE " + $missing +
                " / EXTRA_IN_CACHE " + $extra) -ForegroundColor $(if ($diff -eq 0 -and $missing -eq 0) { 'Green' } else { 'Red' })
    Write-Host ("  Scope       : " + $Scope + "  （默认 bridge = 既有语义；all = 全条目审计面）") -ForegroundColor Cyan
    Write-Host ("  bridge 判定 : " + $(if ($bridgeVerdict) { $bridgeVerdict } else { '<not-in-package>' })) -ForegroundColor $(
        if ($bridgeVerdict -eq 'SAME') { 'Green' } else { 'Red' })
    if ($result.entriesDriftCount -gt 0) {
        Write-Host ("  [WARN] 非 bridge 条目漂移 " + $result.entriesDriftCount + " 项（-Scope bridge 下不计入失败；" +
                    "审计请用 -Scope all）：" + ($result.entriesDrift -join ', ')) -ForegroundColor Yellow
    }
    foreach ($ex in $result.excludedFromPackage) {
        Write-Host ("  [excluded] " + $ex.logicalPath + " — " + $ex.reason) -ForegroundColor DarkGray
    }

    # ---- 状态判定（D-030 r2：唯一判定入口 Resolve-Status，按 -Scope 分层） ----
    $status = Resolve-Status -packageEntryCount $pkgComparable.Count -cacheDirExists $cacheDirExists `
        -comparableCount $comparable -diffCount $diff -entriesDriftCount $result.entriesDriftCount `
        -bridgeVerdictValue $bridgeVerdict -bridgeInPackage $pkgComparable.Contains($script:BridgeRelativePath) `
        -scopeValue $Scope

    # ---------------- 同步（可回退；只同步 bridge 脚本，语义与 v1 一致） ----------------
    # r2：同步与否**只取决于 bridge 是否漂移**（F13 修复）——非 bridge 漂移不触发同步，
    # 因 -Sync 契约只同步 bridge，强求其收敛会永久失败并阻断 package-addin.ps1。
    $bridgeNeedsSync = ($bridgeVerdict -eq 'DIFF')
    if ($Sync -and $status -eq 'CACHE_MISSING') {
        Write-Host '[SYNC] 缓存目录不存在或为空（Pro 未加载过该插件），不动作。' -ForegroundColor Yellow
        $result.action = 'noop-cache-missing'
    } elseif ($Sync -and -not $bridgeNeedsSync -and $status -eq 'INCOMPARABLE') {
        Write-Host '[SYNC] 可比条目不足（缓存侧缺 bridge 条目），bridge 同步不足以收敛 → 不动作，需先重装/重新加载插件。' -ForegroundColor Yellow
        $result.action = 'noop-incomparable'
    } elseif ($Sync -and -not $bridgeNeedsSync) {
        Write-Host ('[SYNC] bridge 已一致，不动作（状态 ' + $status + '）。') -ForegroundColor Green
        $result.action = 'noop-bridge-in-sync'
    } elseif ($Sync -and $bridgeNeedsSync) {
        if ([string]::IsNullOrWhiteSpace($BackupRoot)) {
            throw 'CACHE_SYNC_BACKUP_REQUIRED: -Sync 必须提供 -BackupRoot（可回退要求，D-020 §2.3）。'
        }
        if (-not (Test-Path -LiteralPath $cacheScript -PathType Leaf)) {
            Write-Host '[SYNC] 缓存 bridge 脚本不存在，无法增量同步 → 不动作。' -ForegroundColor Yellow
            $result.action = 'noop-bridge-missing'
        } else {
            $backupRootFull = [System.IO.Path]::GetFullPath($BackupRoot)
            New-Item -ItemType Directory -Force -Path $backupRootFull | Out-Null
            $stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
            $backupPath = Join-Path $backupRootFull ("bridge_runner.py.$stamp.bak")
            if ($PSCmdlet.ShouldProcess($cacheScript, "backup -> $backupPath then sync from package")) {
                Copy-Item -LiteralPath $cacheScript -Destination $backupPath -Force
                Write-Host ("[BACKUP] " + $backupPath) -ForegroundColor Yellow
                Copy-Item -LiteralPath $sourceScriptResolved -Destination $cacheScript -Force
                $newHash = Get-FileSha256 $cacheScript
                if ($newHash -ne $packageHash) {
                    throw "CACHE_SYNC_VERIFY_FAILED: sync did not converge (cache=$newHash expected=$packageHash). 备份仍在: $backupPath"
                }
                Write-Host ("[SYNCED] cache restored to " + $newHash) -ForegroundColor Green
                $result.action = 'synced'
                $result.backupPath = $backupPath
                $result.cacheSha256 = $newHash
                $result.cacheBytes = (Get-Item -LiteralPath $cacheScript).Length

                # 同步后重算 bridge 条目与整体状态（其余条目不变）
                foreach ($e in $result.entries) {
                    if ($e.relativePath -eq $script:BridgeRelativePath -and $e.verdict -ne 'SAME') {
                        $e.cacheHash = $newHash
                        $e.verdict = 'SAME'
                    }
                }
                $result.diffCount = @($result.entries | Where-Object { $_.verdict -eq 'DIFF' }).Count
                $result.sameCount = @($result.entries | Where-Object { $_.verdict -eq 'SAME' }).Count
                $result.missingInCacheCount = @($result.entries | Where-Object { $_.verdict -eq 'MISSING_IN_CACHE' }).Count
                $bridgeVerdict = 'SAME'
                $result.bridgeVerdict = $bridgeVerdict
                $post = Get-EntryDriftSummary $result.entries
                $result.entriesDriftCount = $post.count
                $result.entriesDrift = @($post.paths)

                # 与同步前同一判定入口、同一 -Scope 规则（不因同步而放宽）
                $status = Resolve-Status -packageEntryCount $result.packageEntryCount -cacheDirExists $true `
                    -comparableCount $result.comparableEntryCount -diffCount $result.diffCount `
                    -entriesDriftCount $result.entriesDriftCount -bridgeVerdictValue $bridgeVerdict `
                    -bridgeInPackage $true -scopeValue $Scope
            }
        }
    }

    # ---------------- 长驻 bridge 进程回收（F5 归属门） ----------------
    if ($RecycleBridgeProcess) {
        $procs = Get-BridgeProcesses $cacheScript
        Write-Host ("[RECYCLE] candidate bridge python.exe count = " + $procs.Count)
        foreach ($p in $procs) {
            Assert-PidOwnedByHost ([int]$p.ProcessId) $script:HostProcessName
            if ($PSCmdlet.ShouldProcess(("python.exe PID " + $p.ProcessId), 'stop (F5 guard passed)')) {
                Stop-Process -Id ([int]$p.ProcessId) -Force
                Write-Host ("[RECYCLED] PID " + $p.ProcessId) -ForegroundColor Magenta
                $result.recycledPids += ([int]$p.ProcessId)
                if ($result.action -eq 'synced') { $result.action = 'synced+recycled' }
                elseif (@('none', 'noop-in-sync', 'noop-cache-missing', 'noop-incomparable', 'noop-bridge-missing') -contains $result.action) { $result.action = 'recycled' }
            }
        }
        if ($procs.Count -eq 0) { Write-Host '[RECYCLE] 无长驻 bridge 进程（逐调用短暂存活），无需回收。' }
    }

    Complete-AndExit $status
}
catch {
    $result.error = $_.Exception.Message
    Write-Host ("[ERROR] " + $result.error) -ForegroundColor Red
    Write-Host ("  at " + $_.InvocationInfo.PositionMessage) -ForegroundColor DarkGray
    Complete-AndExit 'ERROR' $result.error
}
