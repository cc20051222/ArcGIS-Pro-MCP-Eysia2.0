param(
    [string]$EventDirectory,
    [string]$TaskRecord
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runtime = Join-Path $projectRoot '.runtime\progress-panel'
if (-not $EventDirectory) { $EventDirectory = Join-Path $runtime 'events' }
if (-not $TaskRecord -and (Test-Path -LiteralPath (Join-Path $runtime 'task-record.json'))) {
    $TaskRecord = Join-Path $runtime 'task-record.json'
}
$EventDirectory = [IO.Path]::GetFullPath($EventDirectory)
if ([IO.Path]::GetPathRoot($EventDirectory) -ne 'D:\') { throw '进度目录必须在 D 盘。' }
if ($TaskRecord) {
    $TaskRecord = [IO.Path]::GetFullPath($TaskRecord)
    if ([IO.Path]::GetPathRoot($TaskRecord) -ne 'D:\') { throw '任务记录必须在 D 盘。' }
}
$exe = Join-Path $runtime 'bin\ArcGISProMCP.ProgressMonitor\Debug\net8.0-windows\ArcGISProMCP.ProgressMonitor.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw '面板尚未构建；见 Docs\PROGRESS_PANEL.md。' }
New-Item -ItemType Directory -Path $EventDirectory -Force | Out-Null
$env:TEMP = Join-Path $runtime 'temp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
$argList = @('--directory', ('"' + $EventDirectory + '"'))
if ($TaskRecord) { $argList += @('--task-record', ('"' + $TaskRecord + '"')) }
# This is the interactive panel the user asked to see, so its window is visible.
Start-Process -FilePath $exe -ArgumentList $argList -WorkingDirectory $projectRoot
