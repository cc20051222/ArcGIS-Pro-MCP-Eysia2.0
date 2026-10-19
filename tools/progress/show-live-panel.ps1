[CmdletBinding()]
param([Parameter(Mandatory=$true)][int]$ProcessId,[switch]$InspectOnly,[string]$Screenshot='', [int]$CaptureFrames=1, [int]$CaptureIntervalMs=250, [switch]$ShowGpHistory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
$process=Get-Process -Id $ProcessId
$window=[Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
if($InspectOnly -or $ShowGpHistory){
    . (Join-Path $PSScriptRoot 'find-panel-window.ps1')
    $window=Get-WorkflowPanelWindow $ProcessId
}
if (-not $window) { throw 'ArcGIS Pro window not found' }
if($Screenshot -or $ShowGpHistory){
    $windowPattern=$null
    if($window.TryGetCurrentPattern([Windows.Automation.WindowPattern]::Pattern,[ref]$windowPattern)){
        $windowPattern.SetWindowVisualState([Windows.Automation.WindowVisualState]::Normal)
    }
    $window.SetFocus()
    Start-Sleep -Milliseconds 500
}
if($ShowGpHistory){
    $all=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
    $target=$all | Where-Object { $_.Current.Name -like 'Buffer_analysis*7%*' } | Select-Object -First 1
    $scrolls=@()
    foreach($element in $all){
        $pattern=$null
        if($element.TryGetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern,[ref]$pattern) -and $pattern.Current.VerticallyScrollable){$scrolls+=@(@{element=$element;pattern=$pattern})}
    }
    if(-not $target -or -not $scrolls.Count){
        Write-Output ('UI root='+$window.Current.Name+'; scrolls='+$scrolls.Count+'; target='+[bool]$target)
        $all | Where-Object { $_.Current.Name -match 'GP|buffer|Buffer' } | Select-Object -First 15 | ForEach-Object { Write-Output $_.Current.Name }
        throw 'Real GP history UI element not found'
    }
    $scroll=$scrolls[-1]
    $viewport=$scroll.element.Current.BoundingRectangle
    $contentHeight=$viewport.Height*100/$scroll.pattern.Current.VerticalViewSize
    $range=$contentHeight-$viewport.Height
    $delta=$target.Current.BoundingRectangle.Top-$viewport.Top-50
    $percent=[Math]::Max(0,[Math]::Min(100,$scroll.pattern.Current.VerticalScrollPercent+$delta/$range*100))
    $scroll.pattern.SetScrollPercent(-1,$percent)
    Start-Sleep -Milliseconds 600
    Write-Output 'Scrolled to actual retained GP 7-percent event; this is history, not a running-task capture'
}
if ($Screenshot) {
    if ($CaptureFrames -lt 1 -or $CaptureFrames -gt 24 -or $CaptureIntervalMs -lt 100) { throw 'Invalid capture bounds' }
    if ([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($Screenshot)) -ne 'D:\') { throw 'Screenshot must be on D' }
    Add-Type -AssemblyName System.Drawing
    $rect=$window.Current.BoundingRectangle
    for($frame=0;$frame -lt $CaptureFrames;$frame++) {
        $path=if($CaptureFrames -eq 1){$Screenshot}else{[IO.Path]::Combine([IO.Path]::GetDirectoryName($Screenshot),[IO.Path]::GetFileNameWithoutExtension($Screenshot)+'-'+$frame.ToString('00')+'.png')}
        if(Test-Path -LiteralPath $path){throw 'Capture exists; refusing overwrite'}
        $bitmap=New-Object Drawing.Bitmap ([int]$rect.Width),([int]$rect.Height)
        $graphics=[Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen([int]$rect.X,[int]$rect.Y,0,0,$bitmap.Size); $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png) } finally {$graphics.Dispose();$bitmap.Dispose()}
        if($CaptureFrames -gt 1){
            $texts=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text))
            $observation=@{at=[DateTimeOffset]::Now.ToString('o');frame=$frame;screenshot=$path;text=@($texts|Select-Object -First 12|ForEach-Object{$_.Current.Name})}|ConvertTo-Json -Depth 5 -Compress
            [IO.File]::AppendAllText($Screenshot+'.frames.jsonl',$observation+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
        }
        Write-Output ('Captured '+$path)
        if($CaptureFrames -gt 1){Start-Sleep -Milliseconds $CaptureIntervalMs}
    }
}
if ($InspectOnly) {
    $all=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
    $all | Where-Object { $_.Current.AutomationId -like 'ArcGISProMCP*' -or $_.Current.Name -match 'MCP|任务|Start|Stop|进度' } | ForEach-Object { Write-Output ($_.Current.ControlType.ProgrammaticName+' '+$_.Current.Name+' '+$_.Current.AutomationId) }
    return
}
$name=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'MCP')
$tabs=$window.FindAll([Windows.Automation.TreeScope]::Descendants,$name)
$selected=$false
foreach($tab in $tabs) {
    $pattern=$null
    if ($tab.TryGetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern,[ref]$pattern)) {
        $pattern.Select(); $selected=$true; break
    }
}
if (-not $selected) { throw 'MCP tab selection pattern not found' }
Start-Sleep -Milliseconds 500
$panelCondition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'ArcGISProMCP_ProgressPanel')
$button=$window.FindFirst([Windows.Automation.TreeScope]::Descendants,$panelCondition)
if (-not $button) { throw 'Actual progress panel button not found' }
$invoke=$button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
$invoke.Invoke()
Write-Output 'Actual ArcGIS Pro MCP tab selected and task-panel button invoked'
