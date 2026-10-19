[CmdletBinding()]
param([Parameter(Mandatory=$true)][int]$ProcessId,[Parameter(Mandatory=$true)][string]$DataRun)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$run=[IO.Path]::GetFullPath($DataRun)
if([IO.Path]::GetPathRoot($run) -ne 'D:\'){throw 'D output required'}
$env:TEMP=Join-Path $run 'scratch';$env:TMP=$env:TEMP
[IO.Directory]::CreateDirectory($env:TEMP)|Out-Null
$env:PYTHONIOENCODING='utf-8'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
$condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
. (Join-Path $PSScriptRoot 'find-panel-window.ps1')
$window=Get-WorkflowPanelWindow $ProcessId
$window.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([Windows.Automation.WindowVisualState]::Normal)
$window.SetFocus()
Start-Sleep -Milliseconds 500
$observations=Join-Path $run 'running-panel-observations.jsonl'
if(Test-Path -LiteralPath $observations){throw 'Capture evidence exists; do not rerun GP'}
$writer=[IO.StreamWriter]::new($observations,$false,[Text.UTF8Encoding]::new($false));$writer.AutoFlush=$true
$python=Get-Command python.exe -ErrorAction Stop
$script=Join-Path $PSScriptRoot 'run-progress-probe.py'
$arguments=@('-B',('"'+$script+'"'),('"'+$run+'"'))
$child=Start-Process -FilePath $python.Source -ArgumentList $arguments -WindowStyle Hidden -RedirectStandardOutput (Join-Path $run 'progress-client-stdout.log') -RedirectStandardError (Join-Path $run 'progress-client-stderr.log') -PassThru
$deadline=[DateTime]::UtcNow.AddSeconds(90)
$frames=0;$observed=0;$observerError=$null
try{
    while([DateTime]::UtcNow -lt $deadline){
        $texts=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text))
        $names=@($texts|Select-Object -First 8|ForEach-Object{$_.Current.Name})
        $record=[ordered]@{at=[DateTimeOffset]::Now.ToString('o');windowProcessId=$ProcessId;text=$names;screenshot=$null;actualRunningGp=$false}
        $runningLabel=([string][char]0x6267)+([string][char]0x884c)+([string][char]0x4e2d)
        $gp= -not $child.HasExited -and ($names -join ' ') -match $runningLabel -and ($names -join ' ') -match 'ArcGIS\.GP\.' -and ($names -join ' ') -notmatch 'OnEndExecute' -and ($names -join ' ') -match 'quality-fix-live-buffer'
        if($gp -and $frames -lt 10){
            $rect=$window.Current.BoundingRectangle
            if($rect.Width -lt 900 -or $rect.Height -lt 600 -or $rect.X -lt 0 -or $rect.Y -lt 0){throw 'Native panel minimized/offscreen; no fake frame accepted'}
            $window.SetFocus()
            $shot=Join-Path $run ('running-gp-'+$frames.ToString('00')+'.png')
            if(Test-Path -LiteralPath $shot){throw 'Frame already exists'}
            $bitmap=New-Object Drawing.Bitmap ([int]$rect.Width),([int]$rect.Height)
            $graphics=[Drawing.Graphics]::FromImage($bitmap)
            $record.captureStartedAt=[DateTimeOffset]::Now.ToString('o')
            try{$graphics.CopyFromScreen([int]$rect.X,[int]$rect.Y,0,0,$bitmap.Size);$bitmap.Save($shot,[Drawing.Imaging.ImageFormat]::Png)}finally{$graphics.Dispose();$bitmap.Dispose()}
            $record.captureEndedAt=[DateTimeOffset]::Now.ToString('o')
            $record.screenshot=$shot;$record.actualRunningGp=$false;$record.candidateRunningGp=$true;$frames++
            Write-Output ('Captured candidate GP running frame; SDK interval recheck required: '+$shot)
        }
        $writer.WriteLine(($record|ConvertTo-Json -Depth 6 -Compress));$observed++
        if($child.HasExited){break}
        Start-Sleep -Milliseconds 100
    }
}catch{$observerError=$_.Exception.Message;Write-Warning ('UI observer stopped; GP was not retried or cancelled: '+$observerError)}finally{$writer.Dispose()}
# A failed UI provider must not lose already captured candidates or replay the GIS write.
if(-not $child.HasExited){$null=$child.WaitForExit(5000)}
$summary=[ordered]@{at=[DateTimeOffset]::Now.ToString('o');observations=$observed;runningFrames=$frames;clientProcessId=$child.Id;clientRemaining=@(Get-Process -Id $child.Id -ErrorAction SilentlyContinue).Count;observerError=$observerError;status=if($frames -gt 0 -and $child.HasExited){'CAPTURED_REQUIRES_RECHECK'}else{'NOT_VERIFIED'}}
$summary|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'capture-summary.json') -Encoding utf8
$summary|ConvertTo-Json
if($frames -eq 0){throw 'No actual running GP frame captured; do not substitute history screenshots'}
