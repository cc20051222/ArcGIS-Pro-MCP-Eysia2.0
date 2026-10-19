[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Preflight,
    [switch]$Apply,
    [switch]$AllowOnlyThisAddinOnC,
    [switch]$SaveCurrentProject,
    [switch]$ResumeAfterSavedProject
)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runtime=Join-Path $projectRoot '.runtime\progress-panel'
. (Join-Path $runtime 'environment.ps1')
$guid='{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'
$install=Join-Path $env:USERPROFILE ('Documents\ArcGIS\AddIns\ArcGISPro\'+$guid+'\ArcGISProMCP.Compatibility.esriAddInX')
$cache=Join-Path $env:USERPROFILE ('AppData\Local\ESRI\ArcGISPro\AssemblyCache\'+$guid)
$pro='C:\Program Files\ArcGIS\Pro\bin\ArcGISPro.exe'
$pre=Get-Content -LiteralPath $Preflight -Raw | ConvertFrom-Json
$run=[IO.Path]::GetFullPath((Split-Path -Parent $Preflight))
if (-not $run.StartsWith($runtime+'\activation\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected evidence root' }
function Assert-PlainPath([string]$Path) {
    $p=[IO.Path]::GetFullPath($Path)
    while ($p) {
        if (Test-Path -LiteralPath $p) {
            $item=Get-Item -LiteralPath $p -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse path refused: $p" }
        }
        $parent=[IO.Path]::GetDirectoryName($p)
        if ($parent -eq $p) { break }; $p=$parent
    }
}
foreach($path in @($run,$install,$cache,$pre.candidate.path,$pre.rollback.path)) { Assert-PlainPath $path }
if ($pre.rollback.originalPath -cne $install) { throw 'Installation scope mismatch' }
if ((Get-FileHash -LiteralPath $pre.candidate.path).Hash -cne $pre.candidate.sha256) { throw 'Candidate hash mismatch' }
if ((Get-FileHash -LiteralPath $install).Hash -cne $pre.rollback.sha256) { throw 'Installation changed since preflight' }
if ((Get-FileHash -LiteralPath $pre.rollback.path).Hash -cne $pre.rollback.sha256) { throw 'Rollback package hash mismatch' }
if (-not $Apply) { Write-Output 'VALIDATED ONLY: no installation or process changes'; return }
if (-not $AllowOnlyThisAddinOnC -or -not $SaveCurrentProject) { throw 'Explicit C-scope exception and project-save authorization required' }
$receiptPath=Join-Path $run 'activation-report.json'
$receipt=[ordered]@{observedAt=[DateTimeOffset]::Now.ToString('o');status='STARTED';authorization='User explicitly allowed only this Add-in installation/cache on C and saving/restarting current project';install=$install;cache=$cache;candidateSha256=$pre.candidate.sha256;steps=@()}
if (Test-Path -LiteralPath $receiptPath) {
    if (-not $ResumeAfterSavedProject) { throw 'This activation run already has a report; reconcile it instead of retrying' }
    $prior=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json -AsHashtable
    if ($prior.status -ne 'STOPPED_REQUIRES_RECONCILIATION' -or @($prior.steps | Where-Object stage -eq 'PRO_EXIT_VERIFIED').Count -or -not @($prior.steps | Where-Object stage -eq 'PROJECT_BACKUP').Count) { throw 'Resume allowed only before Pro exit, after inspecting saved project state' }
    $receipt=$prior
}
function Get-SharedHash([string]$Path) {
    $input=[IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    $algorithm=[Security.Cryptography.SHA256]::Create()
    try { return [Convert]::ToHexString($algorithm.ComputeHash($input)) } finally { $algorithm.Dispose(); $input.Dispose() }
}
function Record([string]$Stage,$Details) {
    $receipt.steps+=@([ordered]@{at=[DateTimeOffset]::Now.ToString('o');stage=$Stage;details=$Details})
    $receipt | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $receiptPath -Encoding utf8
    Write-Output $Stage
}
function Call-Mcp([string]$Name,$Arguments) {
    $request=@{jsonrpc='2.0';id=('activation-'+$Name+'-'+[guid]::NewGuid().ToString('N'));method='tools/call';params=@{name=$Name;arguments=$Arguments}}
    $response=Invoke-RestMethod -Uri 'http://127.0.0.1:6520/mcp' -Method Post -ContentType 'application/json' -Body ($request | ConvertTo-Json -Depth 12) -TimeoutSec 60
    $response | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath (Join-Path $run ($Name+'-'+[DateTime]::Now.ToString('HHmmssfff')+'.json')) -Encoding utf8
    if ($response.error -or $response.result.isError) { throw ('MCP tool failed: '+$Name) }
    $text=@($response.result.content | Where-Object type -eq 'text')[0].text
    return ($text | ConvertFrom-Json)
}
try {
    $hosts=@(Get-Process ArcGISPro -ErrorAction SilentlyContinue)
    $alreadyClosed=$false
    if ($hosts.Count -eq 0 -and $ResumeAfterSavedProject) {
        $verifiedSave=@($receipt.steps | Where-Object stage -eq 'PROJECT_SAVED_VERIFIED')[-1].details
        if (-not $verifiedSave -or (Get-SharedHash $verifiedSave.path) -cne $verifiedSave.sha256) { throw 'Closed project has no matching verified saved state' }
        $project=@{path=$verifiedSave.path;isDirty=$false}
        $alreadyClosed=$true
        Record 'NORMAL_EXIT_RECONCILED' @{remaining=0;projectSha256=$verifiedSave.sha256}
    } else {
        if ($hosts.Count -ne 1 -or $hosts[0].Path -cne $pro) { throw 'Expected exactly the existing ArcGIS Pro process' }
        $oldId=$hosts[0].Id
        $project=Call-Mcp 'get_project_info' @{}
    }
    $aprx=[IO.Path]::GetFullPath($project.path)
    if (-not $aprx.StartsWith('D:\shiyanwenjaijia\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected user project; stop before save' }
    Assert-PlainPath $aprx
    $projectEvidence=Join-Path 'D:\shiyanwenjaijia\自测记录\进度面板实测' (Split-Path -Leaf $run)
    Assert-PlainPath $projectEvidence
    if (-not (Test-Path -LiteralPath $projectEvidence)) { New-Item -ItemType Directory -Path $projectEvidence | Out-Null }
    $probe=Join-Path $projectEvidence '.write-probe'; [IO.File]::WriteAllText($probe,'D probe'); if ([IO.File]::ReadAllText($probe) -cne 'D probe') { throw 'Write probe failed' }; Remove-Item -LiteralPath $probe
    if ($ResumeAfterSavedProject) {
        if ($project.isDirty -ne $false -or -not (Test-Path -LiteralPath (Join-Path $projectEvidence 'project-saved.aprx'))) { throw 'Saved state changed; no automatic save retry' }
        Record 'SAVED_PROJECT_RECONCILED' @{path=$aprx;isDirty=$project.isDirty;noSaveRepeated=$true}
    } else {
        Copy-Item -LiteralPath $aprx -Destination (Join-Path $projectEvidence 'project-before-save.aprx')
        Record 'PROJECT_BACKUP' @{path=$aprx;backupDirectory=$projectEvidence;wasDirty=$project.isDirty;oldProcessId=$oldId}
        if ($project.isDirty) { $saved=Call-Mcp 'save_project' @{} }
    }
    $verified=if($alreadyClosed){$project}else{Call-Mcp 'get_project_info' @{}}
    if ($verified.isDirty -ne $false -or [IO.Path]::GetFullPath($verified.path) -cne $aprx) { throw 'Project save not verified; do not close Pro' }
    if (-not (Test-Path -LiteralPath (Join-Path $projectEvidence 'project-saved.aprx'))) { Copy-Item -LiteralPath $aprx -Destination (Join-Path $projectEvidence 'project-saved.aprx') }
    $savedHash=(Get-FileHash -LiteralPath (Join-Path $projectEvidence 'project-saved.aprx')).Hash
    if ((Get-SharedHash $aprx) -cne $savedHash) { throw 'Live project and saved backup differ; preserve open state' }
    Record 'PROJECT_SAVED_VERIFIED' @{path=$aprx;isDirty=$verified.isDirty;sha256=$savedHash}
    if (-not $alreadyClosed) {
        $hosts[0].Refresh()
        if (-not $hosts[0].CloseMainWindow()) {
            # Some Pro windows reject Process.CloseMainWindow even with a valid HWND.
            # Recheck saved state, then use the normal UI provider close; never force-kill.
            Record 'PROCESS_CLOSE_NOT_ACCEPTED' @{processId=$oldId;alternate='Saved-state-checked normal UIAutomation close'}
            $helper=Join-Path $PSScriptRoot 'close-saved-pro.ps1'
            $windowsPs=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
            & $windowsPs -NoProfile -ExecutionPolicy Bypass -File $helper -ProcessId $oldId -ProjectPath $aprx -ReceiptPath (Join-Path $run 'normal-ui-close.json')
            if($LASTEXITCODE -ne 0){throw 'Normal UI close incomplete; no force fallback'}
        }
    }
    $deadline=[DateTime]::UtcNow.AddSeconds(45)
    while (@(Get-Process ArcGISPro -ErrorAction SilentlyContinue).Count -gt 0 -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 500 }
    $remaining=@(Get-Process ArcGISPro -ErrorAction SilentlyContinue).Count
    if ($remaining -ne 0) { throw "Normal close incomplete; remaining=$remaining. Do not replace loaded DLLs" }
    Record 'PRO_EXIT_VERIFIED' @{remaining=$remaining}
    $listeners=@(Get-NetTCPConnection -LocalPort 6520,6511 -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count) { throw 'MCP or Bridge listener remains after normal close; reconcile before installation' }
    # Cache backup is read only; never delete or move unknown/historical files.
    $cacheBackup=Join-Path $run 'cache-before'
    New-Item -ItemType Directory -Path $cacheBackup | Out-Null
    $cacheFiles=@(Get-ChildItem -LiteralPath $cache -Recurse -Force)
    if (@($cacheFiles | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Cache contains reparse point; stop' }
    $before=@()
    foreach($file in @($cacheFiles | Where-Object { -not $_.PSIsContainer })) {
        $relative=$file.FullName.Substring($cache.Length+1)
        $dest=Join-Path $cacheBackup $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dest)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $dest
        $hash=(Get-FileHash -LiteralPath $file.FullName).Hash
        if ((Get-FileHash -LiteralPath $dest).Hash -cne $hash) { throw 'Cache rollback copy mismatch' }
        $before+=@{path=$relative;sha256=$hash;bytes=$file.Length}
    }
    $before | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'cache-before-manifest.json') -Encoding utf8
    Record 'CACHE_BACKUP_VERIFIED' @{directory=$cacheBackup;files=$before.Count}
    Copy-Item -LiteralPath $pre.candidate.path -Destination $install -Force
    if ((Get-FileHash -LiteralPath $install).Hash -cne $pre.candidate.sha256) { throw 'Installed package hash mismatch' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead($pre.candidate.path)
    $updated=@()
    try {
        foreach($entry in $zip.Entries) {
            $logical=$entry.FullName.Replace('\','/')
            if (-not $logical.StartsWith('Install/')) { continue }
            $relative=$logical.Substring(8)
            if (-not $relative -or $relative.Split('/') -contains '..') { throw 'Unsafe ZIP entry' }
            $target=[IO.Path]::GetFullPath((Join-Path $cache $relative))
            if (-not $target.StartsWith($cache+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Cache output scope escape' }
            Assert-PlainPath $target
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            $input=$entry.Open(); $output=[IO.File]::Open($target,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
            try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
            $expected=@($pre.inventory | Where-Object path -ceq $logical)[0]
            $actual=(Get-FileHash -LiteralPath $target).Hash
            if ($actual -cne $expected.sha256) { throw ('Cache payload mismatch: '+$relative) }
            $updated+=@{path=$relative;sha256=$actual}
        }
    } finally { $zip.Dispose() }
    [IO.Directory]::SetLastWriteTimeUtc($cache,(Get-Item -LiteralPath $install).LastWriteTimeUtc)
    Record 'INSTALL_AND_CACHE_VERIFIED' @{packageSha256=(Get-FileHash -LiteralPath $install).Hash;updatedCacheFiles=$updated}
    $env:ARCGIS_PRO_MCP_LOG_DIRECTORY=Join-Path $runtime 'managed-logs'
    $env:ARCGIS_PRO_MCP_PROGRESS_DIRECTORY=Join-Path $runtime 'events'
    $env:ARCGIS_PRO_MCP_PROGRESS_TASK_RECORD=Join-Path $runtime 'task-record.json'
    $env:ARCGIS_PRO_MCP_AUTOSTART_SERVER_FOR_VERIFICATION='1'
    $env:ARCGIS_PRO_MCP_GP_AUDIT_DIR=Join-Path $projectEvidence 'gp-audit'
    $env:ARCGIS_PRO_MCP_JOBS_DIR=Join-Path $projectEvidence 'jobs'
    $env:ARCTEMPDIR=Join-Path $projectEvidence 'scratch'
    $env:TEMP=$env:ARCTEMPDIR; $env:TMP=$env:TEMP
    foreach($dir in @($env:ARCGIS_PRO_MCP_LOG_DIRECTORY,$env:ARCGIS_PRO_MCP_PROGRESS_DIRECTORY,$env:ARCTEMPDIR)) { [IO.Directory]::CreateDirectory($dir) | Out-Null }
    $start=[Diagnostics.ProcessStartInfo]::new($pro)
    # Interactive GUI launch avoids inherited redirected console handles;
    # direct launch produced a native Fortran CONOUT$ error on this host.
    $start.UseShellExecute=$true
    $start.WorkingDirectory=[IO.Path]::GetDirectoryName($aprx)
    $start.Arguments='"'+$aprx+'"'
    $newProcess=[Diagnostics.Process]::Start($start)
    $receipt.status='STARTED_AWAITING_RUNTIME_VERIFICATION'
    Record 'PRO_STARTED' @{processId=$newProcess.Id;project=$aprx;dataEvidenceDirectory=$projectEvidence;progress=$env:ARCGIS_PRO_MCP_PROGRESS_DIRECTORY;log=$env:ARCGIS_PRO_MCP_LOG_DIRECTORY;temp=$env:TEMP;clientConfigWritten=$false}
    Write-Output ('Receipt: '+$receiptPath)
} catch {
    $receipt.status='STOPPED_REQUIRES_RECONCILIATION'
    Record 'STOP' @{reason=$_.Exception.Message;noAutomaticRetry=$true}
    throw
}
