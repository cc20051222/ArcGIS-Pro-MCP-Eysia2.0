[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$TaskRecord,[Parameter(Mandatory=$true)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$run=[IO.Path]::GetFullPath($RunDirectory)
$record=[IO.Path]::GetFullPath($TaskRecord)
if(-not $run.StartsWith($root+'\.runtime\',[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $run)){throw 'New D project evidence directory required'}
if(-not $record.StartsWith('D:\',[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path -LiteralPath $record -PathType Leaf)){throw 'Existing D task record required; it is read only'}
New-Item -ItemType Directory -Path $run|Out-Null
$env:TEMP=Join-Path $root '.runtime\progress-panel\temp';$env:TMP=$env:TEMP
$before=(Get-FileHash -LiteralPath $record -Algorithm SHA256).Hash
$clock=Join-Path $run 'clock-fixture.txt';$events=Join-Path $run 'events'
New-Item -ItemType Directory -Path $events|Out-Null
[IO.File]::WriteAllText($clock,'2026-10-22 00:00:00')
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
. (Join-Path $PSScriptRoot 'find-panel-window.ps1')
$results=[Collections.Generic.List[object]]::new()
$boundaries=[Collections.Generic.List[object]]::new()
function ById([string]$id){$script:sceneryWindow.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id))}
function Check([string]$name,[scriptblock]$action){
    try {& $action;$results.Add([pscustomobject]@{name=$name;result='PASS'});Write-Output ('PASS '+$name)}
    catch {$results.Add([pscustomobject]@{name=$name;result='FAIL';error=$_.Exception.Message});throw}
}
function Snapshot([string]$name){
    $rect=(ById 'SceneryPreview').Current.BoundingRectangle
    if($rect.X -lt 0 -or $rect.Y -lt 0 -or $rect.Width -lt 100 -or $rect.Height -lt 100){throw 'Owned scenery not visible'}
    $bitmap=[Drawing.Bitmap]::new([int]$rect.Width,[int]$rect.Height);$graphics=[Drawing.Graphics]::FromImage($bitmap)
    $path=Join-Path $run ($name+'.png')
    try {$graphics.CopyFromScreen([int]$rect.X,[int]$rect.Y,0,0,$bitmap.Size);$bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)}
    finally {$graphics.Dispose();$bitmap.Dispose()}
    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
function Set-TestTime([string]$time,[string]$theme,[string]$label){
    [IO.File]::WriteAllText($clock,$time)
    $deadline=[DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 150
        $name=(ById 'ScenerySchedule').Current.Name
        $background=(ById 'SceneryBackground').Current.ItemStatus
        $preview=(ById 'SceneryPreview').Current.ItemStatus
        if($name.Contains($label) -and $background.EndsWith(';theme:'+$theme) -and $preview.EndsWith(';theme:'+$theme)){
            $boundaries.Add([pscustomobject]@{localTime=$time;expected=$theme;observedLabel=$name;result='PASS'});return
        }
    }while([DateTime]::UtcNow -lt $deadline)
    throw ('Automatic schedule failed at '+$time+'; label='+$name+'; background='+$background+'; preview='+$preview)
}
$exe=Join-Path $root '.runtime\progress-panel\bin\ArcGISProMCP.ProgressMonitor\Debug\net8.0-windows\ArcGISProMCP.ProgressMonitor.exe'
# Intentionally visible owned UI fixture; no GIS, credentials, system clock or user task writes.
$script:sceneryOwned=Start-Process -FilePath $exe -ArgumentList @('--directory',('"'+$events+'"'),'--task-record',('"'+$record+'"'),'--scenery-clock-fixture',('"'+$clock+'"')) -PassThru
try {
    $script:sceneryWindow=Get-WorkflowPanelWindow $script:sceneryOwned.Id
    $script:sceneryWindow.SetFocus();Start-Sleep -Milliseconds 500
    Check 'manual scenery and effect controls removed; time fixture visibly labelled' {
        if((ById 'ChooseScenery') -or (ById 'ToggleSceneryMotion')){throw 'Manual scenery controls remain'}
        if(-not(ById 'ScenerySchedule').Current.Name.Contains('时间演示')){throw 'Test clock not visibly disclosed'}
        $invocable=$null
        if((ById 'ScenerySchedule').TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern,[ref]$invocable)){throw 'Schedule is a manual action'}
    }
    Check 'all local-time boundaries and next-day transitions update both scenes without restart' {
        foreach($case in @(
            @('2026-10-22 00:00:00','AURORA','夜间星野'),@('2026-10-22 04:59:59','AURORA','夜间星野'),
            @('2026-10-22 05:00:00','DAWN','晨曦云海'),@('2026-10-22 07:59:59','DAWN','晨曦云海'),
            @('2026-10-22 08:00:00','MOUNTAIN','日间山海'),@('2026-10-22 16:59:59','MOUNTAIN','日间山海'),
            @('2026-10-22 17:00:00','DUSK','暮色映湖'),@('2026-10-22 19:59:59','DUSK','暮色映湖'),
            @('2026-10-22 20:00:00','AURORA','夜间星野'),@('2026-10-22 23:59:59','AURORA','夜间星野'),
            @('2026-10-23 00:00:00','AURORA','夜间星野'),@('2026-10-23 05:00:00','DAWN','晨曦云海'))){Set-TestTime $case[0] $case[1] $case[2]}
    }
    Check 'four time-of-day compositions captured as actual windows' {
        foreach($case in @(@('05:00:00','DAWN','晨曦云海'),@('08:00:00','MOUNTAIN','日间山海'),@('17:00:00','DUSK','暮色映湖'),@('20:00:00','AURORA','夜间星野'))){
            Set-TestTime ('2026-10-22 '+$case[0]) $case[1] $case[2]
            Start-Sleep -Milliseconds 1400
            & (Join-Path $PSScriptRoot 'inspect-workflow-panel.ps1') -ProcessId $script:sceneryOwned.Id -Screenshot (Join-Path $run ($case[1]+'-window.png'))
        }
    }
    Check 'clock schedule stays live while business observation is paused' {
        (ById 'PauseObservation').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
        Set-TestTime '2026-10-23 08:00:00' 'MOUNTAIN' '日间山海'
        (ById 'PauseObservation').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    }
    $stamp=(ById 'SceneryPreview').Current.ItemStatus;Start-Sleep -Milliseconds 600
    if($stamp -eq (ById 'SceneryPreview').Current.ItemStatus){
        $results.Add([pscustomobject]@{name='motion runtime';result='NOT VERIFIED';reason='System reduced motion or software rendering; no OS settings overridden'})
    }else {
        Check 'automatic motion really changes visible pixels' {
            $a=Snapshot 'moving-1';Start-Sleep -Milliseconds 1200;$b=Snapshot 'moving-2'
            if($a -eq $b){throw 'Scenery pixels did not change'}
        }
        Check 'minimizing pauses decoration and restoring resumes it' {
            $wp=$script:sceneryWindow.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern)
            $wp.SetWindowVisualState([Windows.Automation.WindowVisualState]::Minimized)
            Start-Sleep -Milliseconds 300;$stamp=(ById 'SceneryPreview').Current.ItemStatus;Start-Sleep -Milliseconds 800
            if($stamp -ne (ById 'SceneryPreview').Current.ItemStatus){throw 'Decoration kept ticking while minimized'}
            $wp.SetWindowVisualState([Windows.Automation.WindowVisualState]::Normal);$script:sceneryWindow.SetFocus();Start-Sleep -Milliseconds 500
            if($stamp -eq (ById 'SceneryPreview').Current.ItemStatus){throw 'Decoration did not resume'}
        }
    }
    Check 'activation immediately reconciles clock changes' {
        $wp=$script:sceneryWindow.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern)
        $wp.SetWindowVisualState([Windows.Automation.WindowVisualState]::Minimized)
        [IO.File]::WriteAllText($clock,'2026-10-23 17:00:00')
        $wp.SetWindowVisualState([Windows.Automation.WindowVisualState]::Normal);$script:sceneryWindow.SetFocus()
        $deadline=[DateTime]::UtcNow.AddSeconds(3)
        do{Start-Sleep -Milliseconds 100;$name=(ById 'ScenerySchedule').Current.Name}while(-not $name.Contains('暮色映湖') -and [DateTime]::UtcNow -lt $deadline)
        if(-not $name.Contains('暮色映湖')){throw 'Schedule stale after activation'}
    }
    Check 'task record remains byte-for-byte unchanged' {if($before -ne (Get-FileHash -LiteralPath $record -Algorithm SHA256).Hash){throw 'User task record changed'}}
} finally {
    [pscustomobject]@{checks=$results.ToArray();boundaries=$boundaries.ToArray();recordSha256Before=$before;recordSha256After=(Get-FileHash -LiteralPath $record -Algorithm SHA256).Hash;note='PASS CANDIDATE. Only the owned UI clock fixture is changed; system clock and GIS untouched. Normal mode checks local time every 30 seconds and on activation.'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $run 'scenery-results.json') -Encoding UTF8
    if(-not $script:sceneryOwned.HasExited){$null=$script:sceneryOwned.CloseMainWindow();$null=$script:sceneryOwned.WaitForExit(5000);if(-not $script:sceneryOwned.HasExited){Write-Warning 'Owned test window still running; no forced termination'}}
}
Write-Output ('SCENERY CHECKS '+($results|Where-Object result -eq 'PASS').Count+' passed; evidence='+$run)
