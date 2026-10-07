$ErrorActionPreference = 'Stop'
$workspace = 'D:\ArcGIS-Pro-MCP 2.0'
$runner = Join-Path $workspace 'scripts\arcgis-pro-mcp-executor-1h.ps1'
$autoRoot = Join-Path $workspace '.runtime\evolution\auto'
$statePath = Join-Path $autoRoot 'executor-loop-state.json'
New-Item -ItemType Directory -Path $autoRoot -Force | Out-Null

# The requested 00:30 first run has passed. Start on the next local :30 boundary,
# then continue hourly while this user session remains active.
$now = Get-Date
$nextRun = [datetime]::Today.AddHours($now.Hour).AddMinutes(30)
if ($nextRun -le $now) { $nextRun = $nextRun.AddHours(1) }

function Save-State($status, $next, $lastRun, $lastExit) {
    [ordered]@{
        status = $status
        processId = $PID
        workspace = $workspace
        runner = $runner
        startedAt = $startedAt.ToString('o')
        nextRunAt = $next.ToString('o')
        lastRunAt = if ($lastRun) { $lastRun.ToString('o') } else { $null }
        lastExitCode = $lastExit
        persistence = 'current interactive user session; stops at logoff or process termination'
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $statePath -Encoding utf8
}

$startedAt = Get-Date
Save-State 'RUNNING_SESSION_BOUND' $nextRun $null $null
while ($true) {
    $seconds = [Math]::Ceiling(($nextRun - (Get-Date)).TotalSeconds)
    if ($seconds -gt 0) {
        Start-Sleep -Seconds ([Math]::Min(30, $seconds))
        continue
    }

    $scheduledAt = $nextRun
    $args = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $runner + '"'
    $child = Start-Process -FilePath 'powershell.exe' -ArgumentList $args -WorkingDirectory $workspace -WindowStyle Hidden -Wait -PassThru
    $lastRun = Get-Date
    $lastExit = $child.ExitCode
    $nextRun = $scheduledAt.AddHours(1)
    while ($nextRun -le $lastRun) { $nextRun = $nextRun.AddHours(1) }
    Save-State 'RUNNING_SESSION_BOUND' $nextRun $lastRun $lastExit
}
