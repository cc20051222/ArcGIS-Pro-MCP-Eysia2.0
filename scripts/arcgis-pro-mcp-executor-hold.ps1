$ErrorActionPreference = 'Stop'
$workspace = 'D:\ArcGIS-Pro-MCP 2.0'
$autoRoot = Join-Path $workspace '.runtime\evolution\auto'
$holdPath = Join-Path $autoRoot 'manual-executor-hold.json'
$statePath = Join-Path $autoRoot 'manual-executor-hold-state.json'
$mutex = [System.Threading.Mutex]::new($false, 'Local\ArcGISProMcpExecutorHourly')
$acquired = $false
try {
    try { $acquired = $mutex.WaitOne(0) }
    catch [System.Threading.AbandonedMutexException] { $acquired = $true }
    if (-not $acquired) {
        @{status='NOT_ACQUIRED'; at=(Get-Date).ToString('o'); processId=$PID} | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
        exit 0
    }
    @{status='HELD'; at=(Get-Date).ToString('o'); processId=$PID; holdFile=$holdPath; mutex='Local\ArcGISProMcpExecutorHourly'} | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
    while ($true) {
        $hold = Get-Content -Raw -LiteralPath $holdPath | ConvertFrom-Json
        if ($hold.status -eq 'RELEASE' -or [DateTimeOffset]::Parse($hold.expiresAt) -le [DateTimeOffset]::Now) { break }
        Start-Sleep -Seconds 10
    }
    @{status='RELEASED'; at=(Get-Date).ToString('o'); processId=$PID; releaseReason=$hold.status} | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
} finally {
    if ($acquired) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
