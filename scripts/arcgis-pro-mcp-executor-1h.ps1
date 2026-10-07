param([switch]$DryRun)
$ErrorActionPreference = 'Stop'
$workspace = 'D:\ArcGIS-Pro-MCP 2.0'
$autoRoot = Join-Path $workspace '.runtime\evolution\auto'
New-Item -ItemType Directory -Path $autoRoot -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runDir = Join-Path $autoRoot ("run-executor-" + $stamp)
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$resultPath = Join-Path $runDir 'run-result.json'
if ($DryRun) {
    @{status='DRY_RUN_OK'; at=(Get-Date).ToString('o'); workspace=$workspace; runDir=$runDir} | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding utf8
    exit 0
}
$holdPath = Join-Path $autoRoot 'manual-executor-hold.json'
if (Test-Path -LiteralPath $holdPath) {
    try {
        $hold = Get-Content -Raw -LiteralPath $holdPath | ConvertFrom-Json
        $holdExpiry = [DateTimeOffset]::Parse([string]$hold.expiresAt)
        if ($hold.status -eq 'HOLD' -and $holdExpiry -gt [DateTimeOffset]::Now) {
            @{status='SKIPPED_MANUAL_HOLD'; at=(Get-Date).ToString('o'); reason=[string]$hold.reason; expiresAt=$holdExpiry.ToString('o'); runDir=$runDir} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resultPath -Encoding utf8
            exit 0
        }
    } catch {
        @{status='SKIPPED_INVALID_MANUAL_HOLD'; at=(Get-Date).ToString('o'); reason='manual hold file could not be parsed; fail closed'; runDir=$runDir} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resultPath -Encoding utf8
        exit 0
    }
}
$mutex = [System.Threading.Mutex]::new($false, 'Local\ArcGISProMcpExecutorHourly')
$mutexAcquired = $false
try {
    $mutexAcquired = $mutex.WaitOne(0)
} catch [System.Threading.AbandonedMutexException] {
    $mutexAcquired = $true
}
if (-not $mutexAcquired) {
    @{status='SKIPPED_BUSY'; at=(Get-Date).ToString('o'); reason='another executor run owns the user-session mutex'; runDir=$runDir} | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding utf8
    $mutex.Dispose()
    exit 0
}
$exitCode = 99
try {
    $tempRoot = Join-Path $runDir 'temp'
    $testTemp = Join-Path $runDir 'test-workspaces'
    New-Item -ItemType Directory -Path $tempRoot,$testTemp -Force | Out-Null
    $env:TEMP = $tempRoot
    $env:TMP = $tempRoot
    $env:ARCGIS_PRO_MCP_TEST_TEMP_ROOT = $testTemp
    Set-Location -LiteralPath $workspace
    $codex = Get-ChildItem -LiteralPath (Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\bin') -Filter 'codex.exe' -File -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $codex) { throw 'Codex CLI executable was not found under LOCALAPPDATA\OpenAI\Codex\bin' }
    $prompt = @"
You are the scheduled Executor for D:\ArcGIS-Pro-MCP 2.0. This is an hourly continuation run, not an independent approval role.

At the start, read AGENTS.md sections 3 and 5, Docs/_gatekeeper/COMMANDER_CHARTER.md, the last three entries of Docs/_gatekeeper/HANDOVER.md, then determine the one current ACTIVE D-*.md in Docs/_gatekeeper/inbox and read its matching HANDOFF_*.md in full. Honor the user's latest executor protocol in the conversation and the active ticket. Ignore historical ACTIVE text when a later CLOSED/DELIVERED status supersedes it. If there is not exactly one current ACTIVE ticket, or its current status is BLOCKED, do no product work; write only a concise D-drive run result and stop.

Executor role: implementation, build/regression/LIVE as authorized, and evidence. Never self-approve PASS, never close a ticket, and never edit HANDOVER.md, DISPATCH_LOG.md, DECISIONS_LOG.md, OPEN_ISSUES_LEDGER.md, or commander verdicts. Deliver only outbox/R-D*.md marked PASS CANDIDATE plus the required h-receipt and a complete evidence-index with no placeholders. Stop and register the blocker in the active ticket status row if scope expands, a new error code is needed, a P-05 gate is reached, ambiguity needs a ruling, or user cooperation is required.

Current user boundary: keep project changes, evidence, temporary files, logs, and test workspaces on D:. A minimal C: record is allowed only when essential for the requested automation/application; avoid other C: writes. No installation; no dotnet clean; error-code count remains 33; never execute ps_* or F04-A; keep P-05 ②/③ gated. Use ARCGIS_PRO_MCP_TEST_TEMP_ROOT for test temp output and TEMP/TMP already point to this run's D: folder. Do not touch protected assets, fixture data, installation contents, distributions, Release, or rollback anchors.

For D-084, resume from existing evidence, verify exactly one current ACTIVE ticket and do not repeat or overwrite existing evidence. Complete the 10-tool three-state gap audit before Source edits. Follow the ticket's seven-step HANDOFF sequence; use the frozen F03 schemas and preregistered read/session/write tiers. Implement only clearly authorized gaps; if equivalence or tier is ambiguous, stop. Required tool count is 154 to 164, registry/catalog/snapshot must agree, error codes stay 33, whitelist stays unchanged, and SDK-dependent Pro-host checks are NOT VERIFIED if installation is unavailable. Do not claim a live check without raw evidence.

If there is no actionable change (for example, awaiting commander review or user gate), record a quiet no-op in this run folder and stop. Keep all claims evidence-backed and disclose any failures with denominators.
"@
    $lastMessage = Join-Path $runDir 'codex-last-message.md'
    $events = Join-Path $runDir 'codex-events.jsonl'
    # --approve-for-me already selects the workspace-write sandbox; the CLI rejects combining it with --sandbox.
    $codexArgs = @('exec','--ephemeral','--skip-git-repo-check','--approve-for-me','--cd',$workspace,'--json','-o',$lastMessage,'-')
    $prompt | & $codex.FullName @codexArgs *>&1 | Set-Content -LiteralPath $events -Encoding utf8
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) { $status = 'COMPLETED' } else { $status = 'CODEX_EXIT_NONZERO' }
    @{status=$status; exitCode=$exitCode; at=(Get-Date).ToString('o'); runDir=$runDir; codex=$codex.FullName; lastMessage=$lastMessage; events=$events} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath -Encoding utf8
} catch {
    @{status='ERROR'; at=(Get-Date).ToString('o'); runDir=$runDir; message=$_.Exception.Message} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $exitCode = 9
} finally {
    if ($mutexAcquired) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
exit $exitCode
