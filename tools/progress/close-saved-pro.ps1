[CmdletBinding()]
param([Parameter(Mandatory=$true)][int]$ProcessId,[Parameter(Mandatory=$true)][string]$ProjectPath,
 [Parameter(Mandatory=$true)][string]$ReceiptPath)
$ErrorActionPreference='Stop'
if([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($ReceiptPath)) -ne 'D:\'){throw 'D evidence required'}
if(Test-Path -LiteralPath $ReceiptPath){throw 'Do not overwrite close evidence'}
$process=Get-Process -Id $ProcessId -ErrorAction Stop
if($process.Path -cne 'C:\Program Files\ArcGIS\Pro\bin\ArcGISPro.exe'){throw 'Unexpected process'}
$q=@{jsonrpc='2.0';id='workflow-close-saved-check';method='tools/call';params=@{name='get_project_info';arguments=@{}}}
$r=Invoke-RestMethod 'http://127.0.0.1:6520/mcp' -Method Post -ContentType 'application/json' -Body ($q|ConvertTo-Json -Depth 6)
if($r.error -or $r.result.isError){throw 'Project state unreadable'}
$project=$r.result.content[0].text|ConvertFrom-Json
if($project.isDirty -ne $false -or [IO.Path]::GetFullPath($project.path) -cne [IO.Path]::GetFullPath($ProjectPath)){throw 'Saved project identity/state changed'}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$c=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
$name=[IO.Path]::GetFileNameWithoutExtension($ProjectPath)
$windows=@([Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,$c)|Where-Object{$_.Current.Name -ceq $name})
if($windows.Count -ne 1){throw 'Expected one identified saved Pro project window'}
$window=$windows[0];$window.SetFocus();$rect=$window.Current.BoundingRectangle
$buttons=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Button))
$close=@($buttons|Where-Object{$_.Current.Name -eq 'Close' -and $_.Current.BoundingRectangle.Top -lt $rect.Top+70 -and $_.Current.BoundingRectangle.Left -gt $rect.Right-100})
if($close.Count -ne 1){throw 'Expected one actual outer Pro titlebar Close button'}
$close[0].GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
$deadline=[DateTime]::UtcNow.AddSeconds(45)
while(@(Get-Process ArcGISPro -ErrorAction SilentlyContinue).Count -gt 0 -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 500}
$remaining=@(Get-Process ArcGISPro -ErrorAction SilentlyContinue).Count
@{at=[DateTimeOffset]::Now.ToString('o');method='Invoke identified outer Pro titlebar Close';processId=$ProcessId;project=$ProjectPath;remaining=$remaining;forceKill=$false;saveRepeated=$false}|ConvertTo-Json|Set-Content -LiteralPath $ReceiptPath -Encoding utf8
if($remaining -ne 0){throw 'Normal close incomplete; no force fallback'}
