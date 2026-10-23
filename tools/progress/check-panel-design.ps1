[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$run=[IO.Path]::GetFullPath($RunDirectory)
if(-not $run.StartsWith($projectRoot+'\.runtime\',[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $run)){throw 'New project D-drive evidence directory required'}
New-Item -ItemType Directory -Path $run|Out-Null
$env:TEMP=Join-Path $projectRoot '.runtime\progress-panel\temp';$env:TMP=$env:TEMP
$env:PYTHONDONTWRITEBYTECODE='1'
$python='C:\Python314\python.exe'
$fixture=Join-Path $run 'fixtures'
& $python -B (Join-Path $PSScriptRoot 'check-workflow-panel.py') demo --run $fixture
if($LASTEXITCODE){throw 'Fixture generation failed'}
$record=Join-Path $run 'task.json';$events=Join-Path $run 'events'
New-Item -ItemType Directory -Path $events|Out-Null
$publisher=Join-Path $projectRoot 'Skills\arcgis-pro-2-workflows\scripts\publish_workflow.py'
function Publish([string]$source){
    $publishArgs=@($source,'--target',$record,'--activate')
    if(Test-Path -LiteralPath $record) {
        # Only this run's test record may switch tasks, with the normal publisher's exact prior ID guard.
        if(-not $record.StartsWith($run+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Test record outside owned run'}
        $previous=Get-Content -LiteralPath $record -Raw -Encoding UTF8|ConvertFrom-Json
        $next=Get-Content -LiteralPath $source -Raw -Encoding UTF8|ConvertFrom-Json
        if($previous.taskId -ne $next.taskId){$publishArgs+=@('--expected-task',$previous.taskId)}
    }
    & $python -B $publisher @publishArgs
    if($LASTEXITCODE){throw 'Fixture publishing failed'}
}
$repair=Join-Path $fixture 'hydrology-repair-demo.json'
$r=Get-Content -LiteralPath $repair -Raw -Encoding UTF8|ConvertFrom-Json
($r.plan.steps|Where-Object id -eq 'H13').requestIds=@('design-repair')
$oldObservation=[DateTimeOffset]::Now.AddMinutes(-5).ToString('o')
foreach($observation in $r.observations){$observation.observedAt=$oldObservation}
$r.presentation.lastObservedAt=$oldObservation
$r|ConvertTo-Json -Depth 50|Set-Content -LiteralPath $repair -Encoding UTF8
# Seed the old-observation fixture consistently before its first publication.
# Later synthetic states append observations; they must not rewrite the shared earlier evidence.
foreach($name in @('hydrology-rechecking-demo.json','hydrology-closed-demo.json')) {
    $path=Join-Path $fixture $name;$next=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json
    foreach($observation in $next.observations){if($observation.id -in @($r.observations.id)){$observation.observedAt=$oldObservation}}
    $next|ConvertTo-Json -Depth 50|Set-Content -LiteralPath $path -Encoding UTF8
}
Publish $repair
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
if(-not ('PanelKeyboardNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;
public static class PanelKeyboardNative {
 [StructLayout(LayoutKind.Sequential)]public struct Point { public int X;public int Y; }
 [DllImport("user32.dll")]public static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]public static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]public static extern bool BringWindowToTop(IntPtr h);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
}
'@
}
. (Join-Path $PSScriptRoot 'find-panel-window.ps1')
$checks=[Collections.Generic.List[object]]::new()
$script:sequence=0
function Find([string]$name){
    $script:window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$name))
}
function ById([string]$id){
    $script:window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id))
}
function Texts {
    $deadline=[DateTime]::UtcNow.AddSeconds(3)
    do {
        try {return (@($script:window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text))|ForEach-Object{$_.Current.Name}) -join '\n')}
        catch {
            $cause=$_.Exception.GetBaseException()
            if(($cause -isnot [Runtime.InteropServices.COMException] -and $cause -isnot [Windows.Automation.ElementNotAvailableException]) -or [DateTime]::UtcNow -ge $deadline){throw}
            Start-Sleep -Milliseconds 150
        }
    }while($true)
}
function Wait-Text([string]$text){
    $until=[DateTime]::UtcNow.AddSeconds(8);$lastProviderError=''
    do {
        try {if((Texts).Contains($text)){return}}
        catch {
            $cause=$_.Exception.GetBaseException()
            if($cause -isnot [Runtime.InteropServices.COMException] -and $cause -isnot [Windows.Automation.ElementNotAvailableException]){throw}
            $lastProviderError=$cause.Message
        }
        Start-Sleep -Milliseconds 200
    }while([DateTime]::UtcNow -lt $until)
    throw ('UI text not observed: '+$text+'; last UI provider error: '+$lastProviderError)
}
function Check([string]$name,[scriptblock]$action){
    try {& $action;$checks.Add([pscustomobject]@{name=$name;result='PASS'});Write-Output ('PASS '+$name)}
    catch {
        if($name -eq 'keyboard search and escape clear filters within the owned panel' -and $_.Exception.Message -in @('Owned title bar is covered; no input sent','Owned panel did not acquire keyboard focus; no keys sent')) {
            $checks.Add([pscustomobject]@{name=$name;result='BLOCKED_BY_ENVIRONMENT';error=$_.Exception.Message});Write-Warning $_.Exception.Message;return
        }
        $checks.Add([pscustomobject]@{name=$name;result='FAIL';error=$_.Exception.Message});throw
    }
}
function Select-Tab([string]$name){(Find $name).GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()}
function Event([double]$completed,[double]$total,[string]$message,[string]$request='design-repair'){
    $script:sequence++
    $row=[ordered]@{Sequence=$script:sequence;InvocationId='design-fixture';RequestId=$request;Tool='UI_FIXTURE_NOT_LIVE';Stage='test';State='RUNNING';Completed=$completed;Total=$total;Message=$message;Source='SYNTHETIC_NOT_LIVE';ObservedAt=[DateTimeOffset]::UtcNow.ToString('o')}
    [IO.File]::AppendAllText((Join-Path $events 'progress-design.jsonl'),(($row|ConvertTo-Json -Compress)+[Environment]::NewLine))
}
function Capture([string]$name,[int]$tab=0){
    & (Join-Path $PSScriptRoot 'inspect-workflow-panel.ps1') -ProcessId $script:owned.Id -Tab $tab -Screenshot (Join-Path $run ($name+'.png'))
}
$exe=Join-Path $projectRoot '.runtime\progress-panel\bin\ArcGISProMCP.ProgressMonitor\Debug\net8.0-windows\ArcGISProMCP.ProgressMonitor.exe'
# The user requested an interactive panel; this owned test window is intentionally visible.
$script:owned=Start-Process -FilePath $exe -ArgumentList @('--directory',('"'+$events+'"'),'--task-record',('"'+$record+'"')) -PassThru
try {
    $script:window=Get-WorkflowPanelWindow $script:owned.Id
    Wait-Text 'H13  问题修复与下游重算'
    Check 'all 16 workflow steps remain available' {
        $list=ById 'WorkflowSteps';$scroll=$list.GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern)
        $seen=[Collections.Generic.HashSet[string]]::new()
        foreach($percent in @(0,50,100)) {
            $scroll.SetScrollPercent(-1,$percent);Start-Sleep -Milliseconds 150
            $items=$list.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem))
            foreach($item in $items){$null=$seen.Add($item.Current.AutomationId)}
        }
        $scroll.SetScrollPercent(-1,0)
        if($seen.Count -ne 16){throw ('Expected 16 across virtualized list, saw '+$seen.Count)}
    }
    Check 'synthetic workflow clearly labelled' {Wait-Text '演示记录 · 非真实 GIS 执行'}
    Check 'unreported stage has no invented percentage' {
        Wait-Text '等待工具回传阶段进度'
        $bars=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ProgressBar))
        if(@($bars|Where-Object{-not $_.Current.IsOffscreen}).Count){throw 'Unexpected visible progress bar'}
    }
    Check 'summary counts stay separate from quality and execution progress' {
        if((ById 'StatTotal').Current.Name -ne '16' -or (ById 'StatRunning').Current.Name -ne '1' -or (ById 'StatVerified').Current.Name -ne '5'){throw 'Unexpected task counts'}
    }
    Check 'search matches ID, title and translated state without changing global counts' {
        foreach($case in @(@('H03','step-H03'),@('填洼','step-H03'),@('检查失败','step-H06'))) {
            (ById 'StepSearch').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($case[0]);Wait-Text '显示 1 / 16 步'
            $deadline=[DateTime]::UtcNow.AddSeconds(5)
            do {Start-Sleep -Milliseconds 100;$match=ById $case[1]}while(-not $match -and [DateTime]::UtcNow -lt $deadline)
            if(-not $match){throw 'Expected search result missing'}
            if((ById 'StatTotal').Current.Name -ne '16' -or (ById 'StatRunning').Current.Name -ne '1'){throw 'Filter changed global counts'}
            Wait-Text 'H13  问题修复与下游重算'
        }
    }
    Check 'no-result search is explicit and leaves execution visible' {
        (ById 'StepSearch').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('no-match-ui-fixture');Wait-Text '没有匹配的步骤';Wait-Text '显示 0 / 16 步'
        if((ById 'StatTotal').Current.Name -ne '16'){throw 'Zero matches hid task size'}
    }
    Check 'attention filter keeps failed steps and explains filtered-out execution' {
        (ById 'ClearStepFilters').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
        (ById 'AttentionOnly').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Wait-Text '当前执行在筛选外'
        if(-not(ById 'step-H06')){throw 'Failure omitted from attention filter'}
        if((ById 'StatRunning').Current.Name -ne '1'){throw 'Filtered-out execution lost'}
    }
    Check 'keyboard search and escape clear filters within the owned panel' {
        $script:window.SetFocus();(ById 'LocateCurrentStep').SetFocus();Start-Sleep -Milliseconds 200
        if([Windows.Automation.AutomationElement]::FocusedElement.Current.ProcessId -ne $script:owned.Id) {
            # Windows foreground lock can reject programmatic focus. Click only an ownership-checked title bar.
            $null=[PanelKeyboardNative]::BringWindowToTop([IntPtr]$script:window.Current.NativeWindowHandle)
            $rect=$script:window.Current.BoundingRectangle;$point=[PanelKeyboardNative+Point]::new();$point.X=[int]$rect.X+100;$point.Y=[int]$rect.Y+20
            $ownerPid=[uint32]0;$null=[PanelKeyboardNative]::GetWindowThreadProcessId([PanelKeyboardNative]::WindowFromPoint($point),[ref]$ownerPid)
            if($ownerPid -ne $script:owned.Id){throw 'Owned title bar is covered; no input sent'}
            $savedCursor=[PanelKeyboardNative+Point]::new();$null=[PanelKeyboardNative]::GetCursorPos([ref]$savedCursor)
            try {$null=[PanelKeyboardNative]::SetCursorPos($point.X,$point.Y);[PanelKeyboardNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero);[PanelKeyboardNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 250}
            finally {$null=[PanelKeyboardNative]::SetCursorPos($savedCursor.X,$savedCursor.Y)}
            (ById 'LocateCurrentStep').SetFocus();Start-Sleep -Milliseconds 150
        }
        if([Windows.Automation.AutomationElement]::FocusedElement.Current.ProcessId -ne $script:owned.Id){throw 'Owned panel did not acquire keyboard focus; no keys sent'}
        [Windows.Forms.SendKeys]::SendWait('^f');$deadline=[DateTime]::UtcNow.AddSeconds(3)
        do{Start-Sleep -Milliseconds 100;$focused=[Windows.Automation.AutomationElement]::FocusedElement.Current}while($focused.AutomationId -ne 'StepSearch' -and [DateTime]::UtcNow -lt $deadline)
        if($focused.AutomationId -ne 'StepSearch'){throw ('Ctrl+F did not focus search; focused process='+$focused.ProcessId+'; element='+$focused.AutomationId)}
        (ById 'StepSearch').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('keyboard-no-match');Wait-Text '显示 0 / 16 步'
        [Windows.Forms.SendKeys]::SendWait('{ESC}');Wait-Text '显示 16 / 16 步'
        if((ById 'StepSearch').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value -ne ''){throw 'Escape did not clear search'}
    }
    Check 'locate current clears filters and restores following' {
        (ById 'LocateCurrentStep').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Wait-Text '显示 16 / 16 步'
        if((ById 'StepSearch').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value -ne ''){throw 'Search not cleared'}
        if((ById 'AttentionOnly').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [Windows.Automation.ToggleState]::Off){throw 'Attention filter not cleared'}
        if((ById 'FollowCurrentStep').Current.Name -ne '跟随当前 · 开'){throw 'Follow not restored'}
        $selected=(ById 'WorkflowSteps').GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
        if($selected[0].Current.AutomationId -ne 'step-H13'){throw 'Current step not selected'}
    }
    Check 'quality shortcut opens real checks and repair details' {
        (ById 'OpenQuality').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Wait-Text '质量检查';Wait-Text '修复中';Select-Tab '步骤详情'
    }
    Check 'manual inspection does not change execution step' {
        (ById 'FollowCurrentStep').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
        (ById 'WorkflowSteps').GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern).SetScrollPercent(-1,0);Start-Sleep -Milliseconds 200
        (ById 'step-H01').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Wait-Text '资料、需求与规范预检'
        Wait-Text 'H13  问题修复与下游重算'
        if((ById 'StatRunning').Current.Name -ne '1'){throw 'Executing step changed'}
    }
    Check 'expanded technical details survive event refresh' {
        (Find '方法、输入输出与证据').GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        Event 3 10 'UI fixture phase counter'
        Wait-Text '本阶段 3 / 10 · 非整单完成率'
        $expanded=(Find '方法、输入输出与证据').GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Current.ExpandCollapseState
        if($expanded -ne [Windows.Automation.ExpandCollapseState]::Expanded){throw 'Fold collapsed on refresh'}
    }
    Check 'known counter is rendered as a stage value' {
        $bars=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ProgressBar))
        $bar=@($bars|Where-Object{-not $_.Current.IsOffscreen})[0]
        $range=$bar.GetCurrentPattern([Windows.Automation.RangeValuePattern]::Pattern).Current
        if($range.Value -ne 3 -or $range.Maximum -ne 10){throw 'Incorrect stage counter'}
    }
    Check 'fresh bound SDK observations are distinguished from an old task record' {
        Wait-Text '内部进度已更新'
        if((ById 'ObservationState').Current.Name -ne '内部进度已更新'){throw 'Fresh bound SDK progress hidden behind stale-record badge'}
    }
    Check 'same sequence after a log restart still updates the execution view' {
        $script:sequence--
        Event 5 10 'same-sequence new observation'
        Wait-Text '本阶段 5 / 10 · 非整单完成率'
    }
    Check 'invalid counter falls back to unknown' {
        Event 99 0 '阶段总量未知 · UI fixture'
        Wait-Text '阶段总量未知 · UI fixture'
        $bars=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ProgressBar))
        if(@($bars|Where-Object{-not $_.Current.IsOffscreen}).Count){throw 'Invalid percentage displayed'}
    }
    Check 'invalid positive totals are rejected consistently in details and internal progress' {
        Event 99 10 'invalid positive counter fixture'
        (ById 'LocateCurrentStep').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
        Wait-Text '阶段计数无效'
        Select-Tab '内部进度';Wait-Text '阶段计数无效'
        if((Texts).Contains('当前阶段 99/10')){throw 'Invalid ratio still presented as progress'}
        Select-Tab '步骤详情'
        (ById 'FollowCurrentStep').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
        (ById 'WorkflowSteps').GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern).SetScrollPercent(-1,0);Start-Sleep -Milliseconds 200
        (ById 'step-H01').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    }
    Check 'locked journal reports event failure without discarding the business workflow' {
        $journal=Join-Path $events 'progress-design.jsonl';$locked=$null;$until=[DateTime]::UtcNow.AddSeconds(3)
        do {
            try {$locked=[IO.FileStream]::new($journal,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}
            catch [IO.IOException] {if([DateTime]::UtcNow -ge $until){throw};Start-Sleep -Milliseconds 100}
        }while(-not $locked)
        try {
            Wait-Text '事件读取异常'
            if((ById 'StatTotal').Current.Name -ne '16' -or (ById 'TaskGoal').Current.Name -eq '任务记录暂不可用'){throw 'Journal fault discarded valid business record'}
            Select-Tab '内部进度';Wait-Text '事件读取提示';Capture 'journal-locked' 2
        } finally {$locked.Dispose();Select-Tab '步骤详情'}
        $until=[DateTime]::UtcNow.AddSeconds(5)
        do{Start-Sleep -Milliseconds 100;$health=(ById 'ObservationState').Current.Name}while($health -notin @('内部进度已更新','暂无新记录') -and [DateTime]::UtcNow -lt $until)
        if($health -notin @('内部进度已更新','暂无新记录')){throw ('Journal health did not recover: '+$health)}
    }
    Check 'corrupt complete events are disclosed while incomplete live writes remain pending' {
        $journal=Join-Path $events 'progress-design.jsonl';$saved=[IO.File]::ReadAllBytes($journal)
        try {
            [IO.File]::AppendAllText($journal,"{"+[Environment]::NewLine);Wait-Text '事件读取异常'
            Select-Tab '内部进度';Wait-Text '损坏事件'
            if((ById 'StatTotal').Current.Name -ne '16'){throw 'One damaged event discarded workflow'}
            [IO.File]::WriteAllBytes($journal,$saved);[IO.File]::AppendAllText($journal,"{");Start-Sleep -Milliseconds 1200
            if((ById 'ObservationState').Current.Name -eq '事件读取异常'){throw 'Incomplete final write was reported as corruption'}
        } finally {[IO.File]::WriteAllBytes($journal,$saved);Select-Tab '步骤详情'}
    }
    Check 'unrelated process events do not take over task progress or task observation' {
        Event 1 4 'unrelated process-only event' 'unrelated-request'
        Select-Tab '内部进度';Wait-Text '本任务最新内部观测';Wait-Text '其他请求'
        if((ById 'StatRunning').Current.Name -ne '1'){throw 'Unrelated event changed execution count'}
        if((ById 'InternalSummary').Current.Name.Contains('unrelated process-only event')){throw 'Other request shown as latest task progress'}
        Select-Tab '步骤详情'
    }
    Check 'bound SDK progress preserves the selected noncurrent step row' {
        Select-Tab '步骤详情'
        $list=ById 'WorkflowSteps';$selected=$list.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()[0]
        $id=$selected.Current.AutomationId;$before=$selected.GetRuntimeId() -join ','
        if($id -eq 'step-H13'){throw 'Fixture did not select a noncurrent step'}
        Event 1 7 'selected row continuity fixture';Wait-Text '本阶段 1 / 7 · 非整单完成率'
        $after=(ById 'WorkflowSteps').GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()[0]
        if($after.Current.AutomationId -ne $id -or ($after.GetRuntimeId() -join ',') -ne $before){throw 'Bound progress replaced the selected noncurrent row'}
        $null=$selected.Current.Name
    }
    Check 'unrelated SDK updates preserve the manually opened step detail controls' {
        Select-Tab '步骤详情';$detail=Find '方法、输入输出与证据'
        $detail.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();$before=$detail.GetRuntimeId() -join ','
        Event 1 2 'unrelated detail continuity fixture' 'another-task-detail'
        $latest=Get-Content -LiteralPath (Join-Path $events 'progress-design.jsonl') -Tail 1|ConvertFrom-Json
        Select-Tab '内部进度';Wait-Text (([DateTimeOffset]$latest.ObservedAt).ToLocalTime().ToString('HH:mm:ss.fff'));Select-Tab '步骤详情'
        if($before -ne ((Find '方法、输入输出与证据').GetRuntimeId() -join ',')){throw 'Unrelated event replaced the active detail controls'}
        if($detail.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Current.ExpandCollapseState -ne [Windows.Automation.ExpandCollapseState]::Expanded){throw 'Manually opened detail became unavailable'}
    }
    Check 'bound progress preserves unchanged quality detail controls' {
        Select-Tab '检查修复';$quality=Find '依据与适用限制'
        $quality.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();$before=$quality.GetRuntimeId() -join ','
        Event 9 10 'quality reading continuity fixture';Wait-Text '本阶段 9 / 10 · 非整单完成率'
        if($before -ne ((Find '依据与适用限制').GetRuntimeId() -join ',')){throw 'SDK progress replaced unchanged quality controls'}
        if($quality.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Current.ExpandCollapseState -ne [Windows.Automation.ExpandCollapseState]::Expanded){throw 'Quality reading interrupted'}
        Select-Tab '步骤详情'
    }
    Check 'long SDK messages stay compact while full event detail remains accessible' {
        $message='长消息界面夹具 '+("多行工具消息，仅用于界面测试。`n"*400)+' FULL-EVENT-END'
        Event 8 10 $message
        Select-Tab '内部进度';Wait-Text '本任务最新内部观测'
        $deadline=[DateTime]::UtcNow.AddSeconds(5)
        do{Start-Sleep -Milliseconds 150;$summary=(ById 'InternalSummary').Current.Name}while(-not $summary.Contains('长消息界面夹具') -and [DateTime]::UtcNow -lt $deadline)
        if($summary.Length -gt 500 -or ($summary -split "`n").Count -gt 5){throw 'Long SDK message overwhelms the latest progress summary'}
        $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsExpandCollapsePatternAvailableProperty,$true)
        $fold=@($window.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)|Where-Object{$_.Current.Name.Contains('UI_FIXTURE_NOT_LIVE')})[0]
        $fold.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Wait-Text 'FULL-EVENT-END'
        $fold.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
        Capture 'compact-long-sdk' 2;Select-Tab '步骤详情'
    }
    Check 'reused sequence does not inherit a different event expansion' {
        Select-Tab '内部进度';Event 6 10 'old event fold fixture';Wait-Text 'old event fold fixture'
        $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsExpandCollapsePatternAvailableProperty,$true)
        $fold=@($window.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)|Where-Object{$_.Current.Name.Contains('UI_FIXTURE_NOT_LIVE')})[0]
        $oldName=$fold.Current.Name;$fold.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        Start-Sleep -Milliseconds 100;$script:sequence--;Event 7 10 'new event fold fixture';Wait-Text 'new event fold fixture'
        $newFold=@($window.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)|Where-Object{$_.Current.Name.Contains('UI_FIXTURE_NOT_LIVE')})[0]
        if($newFold.Current.Name -eq $oldName){throw 'Replacement event not visible'}
        if($newFold.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Current.ExpandCollapseState -ne [Windows.Automation.ExpandCollapseState]::Collapsed){throw 'New event inherited old fold expansion'}
        if((Find $oldName).GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Current.ExpandCollapseState -ne [Windows.Automation.ExpandCollapseState]::Expanded){throw 'Original event lost its fold state'}
        Select-Tab '步骤详情'
    }
    Check 'event bursts and rotated snapshots reconcile without inventing task completion' {
        $batch=[Text.StringBuilder]::new()
        foreach($n in 1..100) {
            $script:sequence++
            $row=[ordered]@{Sequence=$script:sequence;InvocationId='burst-fixture';RequestId='design-repair';Tool='UI_FIXTURE_NOT_LIVE';Stage='test';State='RUNNING';Completed=$n;Total=100;Message=('burst fixture '+$n);Source='SYNTHETIC_NOT_LIVE';ObservedAt=[DateTimeOffset]::UtcNow.ToString('o')}
            $null=$batch.AppendLine(($row|ConvertTo-Json -Compress))
        }
        [IO.File]::AppendAllText((Join-Path $events 'progress-design.jsonl'),$batch.ToString())
        Wait-Text '本阶段 100 / 100 · 非整单完成率'
        if((ById 'StatRunning').Current.Name -ne '1' -or (ById 'StatVerified').Current.Name -ne '5'){throw 'Stage completion promoted task state'}
        $row.Sequence=1;$row.Completed=2;$row.Total=12;$row.Message='rotated snapshot fixture';$row.ObservedAt=[DateTimeOffset]::UtcNow.ToString('o')
        [IO.File]::WriteAllText((Join-Path $events 'progress-design.jsonl'),(($row|ConvertTo-Json -Compress)+[Environment]::NewLine))
        Wait-Text '本阶段 2 / 12 · 非整单完成率'
        Select-Tab '内部进度';Wait-Text 'rotated snapshot fixture'
        if((Texts).Contains('burst fixture 100')){throw 'Removed journal snapshot still displayed'}
        Select-Tab '步骤详情'
    }
    Check 'manual list scroll and selection survive a new bound SDK event' {
        $list=ById 'WorkflowSteps';$scroll=$list.GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern);$scroll.SetScrollPercent(-1,50);Start-Sleep -Milliseconds 250
        $beforePercent=$scroll.Current.VerticalScrollPercent;$beforeId=$list.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()[0].Current.AutomationId
        Event 4 10 'manual scroll preservation fixture';Wait-Text '本阶段 4 / 10 · 非整单完成率';Start-Sleep -Milliseconds 250
        $afterPercent=$list.GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern).Current.VerticalScrollPercent
        $afterId=$list.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()[0].Current.AutomationId
        if([Math]::Abs($beforePercent-$afterPercent) -gt 5 -or $beforeId -ne $afterId){throw ('Manual view moved: '+$beforePercent+' -> '+$afterPercent+'; '+$beforeId+' -> '+$afterId)}
    }
    Check 'quality and live repair details are reachable' {Select-Tab '检查修复';Wait-Text '修复中';Wait-Text '检查失败';Capture 'quality-repair' 1}
    Check 'internal event details remain available' {Select-Tab '内部进度';Wait-Text 'UI_FIXTURE_NOT_LIVE';Capture 'internal-events' 2}
    Check 'intake and delivery tab is reachable' {Select-Tab '资料交付';Wait-Text '实际成果';Capture 'intake' 3}
    Select-Tab '步骤详情'
    Check 'pause freezes observation without claiming task cancellation' {
        (ById 'PauseObservation').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Wait-Text '面板已暂停';if((ById 'ObservationState').Current.Name -ne '已暂停观测'){throw 'Pause badge misleading'}
        Publish (Join-Path $fixture 'hydrology-rechecking-demo.json')
        Start-Sleep -Milliseconds 1000
        if(-not (Texts).Contains('H13  问题修复与下游重算')){throw 'Paused view updated'}
        (ById 'PauseObservation').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Wait-Text 'H14  新证据全量复检'
    }
    Check 'window remains usable at reduced size' {
        $dpi=[PanelKeyboardNative]::GetDpiForWindow([IntPtr]$window.Current.NativeWindowHandle)
        if($dpi -lt 96){throw 'Window DPI unavailable'}
        $window.GetCurrentPattern([Windows.Automation.TransformPattern]::Pattern).Resize(960*$dpi/96,640*$dpi/96)
        Start-Sleep -Milliseconds 300
        $bounds=$window.Current.BoundingRectangle
        foreach($id in @('WorkflowSteps','WorkflowDetails')){
            $element=ById $id;$rect=$element.Current.BoundingRectangle
            if($element.Current.IsOffscreen -or $rect.Width -lt 100 -or $rect.Height -lt 100 -or $rect.Right -gt $bounds.Right -or $rect.Left -lt $bounds.Left){throw 'Required panel clipped at minimum size'}
        }
        Capture 'narrow-recheck'
    }
    Check 'minimum window preserves reading space and restores comfortable density when enlarged' {
        $dpi=[PanelKeyboardNative]::GetDpiForWindow([IntPtr]$window.Current.NativeWindowHandle)
        $window.GetCurrentPattern([Windows.Automation.TransformPattern]::Pattern).Resize(960*$dpi/96,640*$dpi/96)
        $until=[DateTime]::UtcNow.AddSeconds(4)
        do{Start-Sleep -Milliseconds 100}while($window.Current.ItemStatus -ne 'density:compact' -and [DateTime]::UtcNow -lt $until)
        $height=(ById 'WorkflowSteps').Current.BoundingRectangle.Height/($dpi/96)
        if($height -lt 160){throw ('Minimum workflow viewport too short: '+$height+' DIP')}
        if($window.Current.ItemStatus -ne 'density:compact'){throw 'Compact density not disclosed to accessibility'}
        Capture 'compact-density'
        $window.GetCurrentPattern([Windows.Automation.TransformPattern]::Pattern).Resize(1400*$dpi/96,900*$dpi/96);Start-Sleep -Milliseconds 400
        if($window.Current.ItemStatus -ne 'density:comfortable'){throw 'Comfortable density did not restore'}
    }
    Check 'resizing back to minimum keeps step status inside the visible list' {
        $dpi=[PanelKeyboardNative]::GetDpiForWindow([IntPtr]$window.Current.NativeWindowHandle)
        $window.GetCurrentPattern([Windows.Automation.TransformPattern]::Pattern).Resize(960*$dpi/96,640*$dpi/96)
        $until=[DateTime]::UtcNow.AddSeconds(4)
        do{Start-Sleep -Milliseconds 100}while($window.Current.ItemStatus -ne 'density:compact' -and [DateTime]::UtcNow -lt $until)
        $list=ById 'WorkflowSteps';$list.GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern).SetScrollPercent(-1,0);Start-Sleep -Milliseconds 200
        $bounds=$list.Current.BoundingRectangle
        $first=$list.FindFirst([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem))
        $labels=$first.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text))
        if($first.Current.IsOffscreen -or $labels.Count -lt 4){throw 'First realized row and its status are not observable'}
        foreach($label in $labels){$rect=$label.Current.BoundingRectangle;if($rect.Right -gt $bounds.Right+1){throw ('Step text extends beyond visible list: '+$label.Current.Name)}}
        Capture 'minimum-status-visible'
    }
    Check 'corrupt record clears old workflow state' {
        $script:validRecordBytes=[IO.File]::ReadAllBytes($record)
        [IO.File]::WriteAllText($record,'{')
        Wait-Text '任务记录暂不可用'
        if((ById 'WorkflowSteps').FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem)).Count){throw 'Stale steps retained'}
    }
    Check 'corrupt-record tooltips do not retain the old task or stage' {
        if((ById 'TaskGoal').Current.HelpText -ne '任务记录暂不可用'){throw 'Stale task tooltip retained'}
        if((ById 'ActiveStep').Current.HelpText -ne '当前步骤未知'){throw 'Stale execution tooltip retained'}
        if((ById 'StageDescription').Current.HelpText.Contains('4 / 10')){throw 'Stale stage tooltip retained'}
    }
    Check 'valid record recovers after corruption' {
        # Restore only this test's known-good snapshot. The publisher correctly rejects corrupt targets.
        [IO.File]::WriteAllBytes($record,$script:validRecordBytes)
        Wait-Text 'H14  新证据全量复检';Capture 'recovered-recheck'
    }
    $prepare=@'
import copy,datetime as dt,json,pathlib,sys,uuid
folder=pathlib.Path(sys.argv[1]);run=pathlib.Path(sys.argv[2]);base=json.loads((folder/'hydrology-plan.json').read_text(encoding='utf-8-sig'))
long=copy.deepcopy(base);long['taskId']='ui-long-'+uuid.uuid4().hex;long['goal']='300步长流程界面演示（未执行GIS）'
for n in range(17,301):
 row=copy.deepcopy(base['plan']['steps'][1]);row.update(id=f'L{n:03}',title=f'长流程演示步骤 {n}',dependsOn=['H16' if n==17 else f'L{n-1:03}'],requirementIds=[]);long['plan']['steps'].append(row)
artifact=str(run/'只读界面夹具_不是GIS成果'/('长路径测试_'*22)/'界面演示结果.tif')
long['presentation']['artifacts']=[{'label':'长路径界面演示（未核验成果）','path':artifact,'state':'PLANNED','stepId':'L300'}]
(folder/'long-demo.json').write_text(json.dumps(long,ensure_ascii=False),encoding='utf-8')
(folder/'long-path.txt').write_text(artifact,encoding='utf-8')
future=json.loads((folder/'hydrology-rechecking-demo.json').read_text(encoding='utf-8-sig'));future['taskId']='ui-future-'+uuid.uuid4().hex
at=(dt.datetime.now().astimezone()+dt.timedelta(hours=1)).isoformat()
for observation in future['observations']:observation['observedAt']=at
future['presentation']['lastObservedAt']=at
(folder/'future-clock-demo.json').write_text(json.dumps(future,ensure_ascii=False),encoding='utf-8')
stale=json.loads((folder/'hydrology-rechecking-demo.json').read_text(encoding='utf-8-sig'));stale['taskId']='ui-artifact-'+uuid.uuid4().hex
owner=next(s for s in stale['plan']['steps'] if s['id']=='H13');owner['requestIds']=['artifact-reexecution']
artifact_file=run/'ui-only-not-GIS.txt';artifact_file.write_text('Synthetic UI fixture; not a GIS result.',encoding='utf-8')
stale['presentation']['artifacts']=[{'label':'已核验路径界面夹具（非 GIS 成果）','path':str(artifact_file),'state':'VERIFIED','stepId':'H13','observationRef':owner['evidenceRefs'][-1]}]
(folder/'artifact-reexecution-demo.json').write_text(json.dumps(stale,ensure_ascii=False),encoding='utf-8')
'@
    & $python -B -c $prepare $fixture $run
    if($LASTEXITCODE){throw 'Extended fixture generation failed'}
    Check '300-step plan stays complete and last step remains searchable' {
        Publish (Join-Path $fixture 'long-demo.json');Wait-Text '300步长流程界面演示'
        if((ById 'StatTotal').Current.Name -ne '300'){throw 'Long workflow truncated'}
        (ById 'StepSearch').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('L300');Wait-Text '显示 1 / 300 步'
        if(-not(ById 'step-L300')){throw 'Last long-workflow step inaccessible'}
        Capture 'long-workflow-search'
    }
    Check 'long artifact path is complete, selectable and has a copy action' {
        Select-Tab '资料交付';Wait-Text '实际成果'
        Wait-Text '实际成果 · 0 项'
        (Find '计划与待核对路径 · 1 项').GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Wait-Text '计划输出，尚未生成'
        $path=Find '成果路径 · 长路径界面演示（未核验成果）'
        $value=$path.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current
        if(-not $value.IsReadOnly -or $value.Value -ne [IO.File]::ReadAllText((Join-Path $fixture 'long-path.txt'))){throw 'Artifact path truncated or writable'}
        $copy=Find '复制路径 · 长路径界面演示（未核验成果）';$null=$copy.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
        Capture 'long-artifact-path' 3
        # The user's clipboard is preserved: availability is tested without invoking the action.
        Select-Tab '步骤详情'
    }
    Check 'new bound execution invalidates old artifact verification in the delivery view' {
        Publish (Join-Path $fixture 'artifact-reexecution-demo.json');Select-Tab '资料交付';Wait-Text '已核验路径界面夹具（非 GIS 成果） · 已核验'
        Event 1 5 'Synthetic artifact reexecution' 'artifact-reexecution';Wait-Text '旧成果核验待复核';Wait-Text '已核验路径界面夹具（非 GIS 成果） · 已失效 · 需复检'
        Capture 'artifact-reexecution' 3;Select-Tab '步骤详情'
    }
    Check 'journal rotation cannot resurrect old artifact verification' {
        $journal=Join-Path $events 'progress-design.jsonl';$saved=[IO.File]::ReadAllBytes($journal)
        try {
            [IO.File]::WriteAllText($journal,'');Start-Sleep -Milliseconds 1400
            Select-Tab '资料交付';Wait-Text '旧成果核验待复核'
            if((Texts).Contains('已核验路径界面夹具（非 GIS 成果） · 已核验')){throw 'Old verification resurrected after journal rotation'}
            Capture 'rotation-keeps-recheck' 3
        } finally {[IO.File]::WriteAllBytes($journal,$saved);Select-Tab '步骤详情'}
    }
    Check 'temporary record failure cannot erase observed reexecution after recovery' {
        $journal=Join-Path $events 'progress-design.jsonl';$savedJournal=[IO.File]::ReadAllBytes($journal);$savedRecord=[IO.File]::ReadAllBytes($record)
        try {
            [IO.File]::WriteAllText($journal,'');[IO.File]::WriteAllText($record,'{');Wait-Text '任务记录暂不可用'
            [IO.File]::WriteAllBytes($record,$savedRecord);Select-Tab '资料交付';Wait-Text '旧成果核验待复核'
            if((Texts).Contains('已核验路径界面夹具（非 GIS 成果） · 已核验')){throw 'Recovery resurrected old verification without new evidence'}
        } finally {[IO.File]::WriteAllBytes($record,$savedRecord);[IO.File]::WriteAllBytes($journal,$savedJournal);Select-Tab '步骤详情'}
    }
    Check 'future observation time is flagged instead of looking fresh' {
        Publish (Join-Path $fixture 'future-clock-demo.json');Wait-Text '记录时钟待核对'
        if((ById 'ObservationState').Current.Name -ne '记录时钟待核对'){throw 'Future record treated as fresh'}
    }
    Check 'missing record shows intake guidance and recovers without stale counts' {
        $missing=Join-Path $run 'record-missing-snapshot.json';[IO.File]::Move($record,$missing)
        try {Wait-Text '先确认任务，再开始处理';Wait-Text '等待任务流程';if((ById 'StatTotal').Current.Name -ne '—'){throw 'Old counts retained without a record'};Capture 'empty-task'}
        finally {[IO.File]::Move($missing,$record)}
        Wait-Text 'H14  新证据全量复检'
    }
    foreach($defect in @('task-identity','duplicate-request','empty-step-id')) {
        Check ('reject ambiguous workflow snapshot: '+$defect) {
            $saved=[IO.File]::ReadAllBytes($record)
            try {
                $bad=Get-Content -LiteralPath $record -Raw -Encoding UTF8|ConvertFrom-Json
                switch($defect) {
                    'task-identity' {$bad.workflowView.taskId='wrong-task-ui-negative-fixture'}
                    'duplicate-request' {$bad.workflowView.steps[0].requestIds=@('duplicate-ui-request');$bad.workflowView.steps[1].requestIds=@('duplicate-ui-request')}
                    'empty-step-id' {$bad.workflowView.steps[0].id=''}
                }
                $bad|ConvertTo-Json -Depth 60|Set-Content -LiteralPath $record -Encoding UTF8
                Wait-Text '任务记录暂不可用'
                if((ById 'StatTotal').Current.Name -ne '—'){throw 'Ambiguous snapshot left valid-looking counts'}
            } finally {[IO.File]::WriteAllBytes($record,$saved)}
            Wait-Text 'H14  新证据全量复检'
        }
    }
    $crossTask=@'
import copy,json,pathlib,sys,uuid
folder=pathlib.Path(sys.argv[1]);base=json.loads((folder/'hydrology-plan.json').read_text(encoding='utf-8-sig'))
for n in (1,2):
 record=copy.deepcopy(base);record['taskId']='ui-cross-'+uuid.uuid4().hex;record['goal']=f'跨任务详情切换 {n}（界面演示，未执行GIS）'
 record['plan']['steps'][0]['summary']=f'新任务 {n} 的独立详情。'+('界面长文本检验，不是GIS结果。'*260)
 (folder/f'cross-task-{n}.json').write_text(json.dumps(record,ensure_ascii=False),encoding='utf-8')
'@
    & $python -B -c $crossTask $fixture
    if($LASTEXITCODE){throw 'Cross-task fixture creation failed'}
    Check 'switching tasks resets detail scroll even when both selected step IDs are H01' {
        Publish (Join-Path $fixture 'cross-task-1.json');Select-Tab '步骤详情';Wait-Text '跨任务详情切换 1'
        $detail=ById 'StepDetailScroll';$scroll=$detail.GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern)
        $scroll.SetScrollPercent(-1,70);Start-Sleep -Milliseconds 250
        if($scroll.Current.VerticalScrollPercent -lt 40){throw 'Detail did not scroll for first task'}
        Publish (Join-Path $fixture 'cross-task-2.json');Wait-Text '跨任务详情切换 2';Start-Sleep -Milliseconds 300
        if((ById 'StepDetailScroll').GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern).Current.VerticalScrollPercent -gt 1){throw 'New task inherited old detail scroll'}
        if((ById 'StepSearch').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value -ne ''){throw 'Old query retained in new task'}
        Capture 'cross-task-reset'
    }
} finally {
    $checks|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $run 'ui-results.json') -Encoding UTF8
    $script:owned.Refresh()
    if(-not $script:owned.HasExited){$null=$script:owned.CloseMainWindow();$null=$script:owned.WaitForExit(5000)}
    if(-not $script:owned.HasExited){Write-Warning 'Owned panel did not close; no forced termination attempted'}
}
Write-Output ('UI CHECKS PASS='+@($checks|Where-Object result -eq 'PASS').Count+'; BLOCKED='+@($checks|Where-Object result -eq 'BLOCKED_BY_ENVIRONMENT').Count+'; TOTAL='+$checks.Count+'; evidence='+$run)
