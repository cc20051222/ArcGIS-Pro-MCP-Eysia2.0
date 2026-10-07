param(
    [Parameter(Mandatory=$true)][string]$DocxPath,
    [Parameter(Mandatory=$true)][string]$PdfPath,
    [Parameter(Mandatory=$true)][string]$WorkingPath
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath('D:\ArcGIS-Pro-MCP 2.0\3.0最终方案')
foreach ($candidatePath in @($DocxPath,$PdfPath,$WorkingPath)) {
    $resolvedTaskPath = [IO.Path]::GetFullPath($candidatePath)
    if (-not $resolvedTaskPath.StartsWith($taskRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must remain in final document directory' }
}
New-Item -ItemType Directory -Path $WorkingPath -Force | Out-Null
$taskProfile = Join-Path $WorkingPath 'office-profile'
$taskTemp = Join-Path $WorkingPath 'office-temp'
foreach ($taskFolder in @($taskProfile,$taskTemp,(Join-Path $taskProfile 'Roaming'),(Join-Path $taskProfile 'Local'))) {
    New-Item -ItemType Directory -Path $taskFolder -Force | Out-Null
}
$env:TEMP = $taskTemp
$env:TMP = $taskTemp
$env:TMPDIR = $taskTemp
$env:APPDATA = Join-Path $taskProfile 'Roaming'
$env:LOCALAPPDATA = Join-Path $taskProfile 'Local'
$priorPids = @(Get-Process -Name WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
if ($priorPids.Count -gt 0) { throw 'Existing user Word sessions present; isolated native conversion deferred' }
$wordApp = $null
$ownedDocument = $null
$startedAt = [DateTime]::UtcNow
$result = [ordered]@{ renderer='Microsoft Word native PDF export'; input=$DocxPath; output=$PdfPath; projectTemporaryRoot=$WorkingPath; openedReadOnly=$true; addedToRecentFiles=$false; existingSessionsTouched=$false; success=$false }
try {
    $wordApp = New-Object -ComObject Word.Application
    $wordApp.Visible = $false
    $wordApp.DisplayAlerts = 0
    $wordApp.AutomationSecurity = 3
    $ownedDocument = $wordApp.Documents.Open($DocxPath, $false, $true, $false)
    $ownedDocument.ExportAsFixedFormat($PdfPath,17)
    $result['pageCount'] = $ownedDocument.ComputeStatistics(2)
    $result['wordVersion'] = $wordApp.Version
    $result['success'] = (Test-Path -LiteralPath $PdfPath)
} catch {
    $result['error'] = $_.Exception.Message
    throw
} finally {
    if ($null -ne $ownedDocument) {
        $ownedDocument.Close(0)
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($ownedDocument)
    }
    if ($null -ne $wordApp) {
        $wordApp.Quit(0)
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($wordApp)
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    $remaining = @(Get-Process -Name WINWORD -ErrorAction SilentlyContinue | Where-Object { $_.StartTime.ToUniversalTime() -ge $startedAt })
    $result['remainingOwnedWordProcesses'] = @($remaining | ForEach-Object { $_.Id })
    $result['elapsedSeconds'] = ([DateTime]::UtcNow - $startedAt).TotalSeconds
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $WorkingPath 'native-word-render.json') -Encoding utf8
}
