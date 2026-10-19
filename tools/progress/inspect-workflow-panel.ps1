[CmdletBinding()]
param([Parameter(Mandatory=$true)][int]$ProcessId,[ValidateRange(0,3)][int]$Tab=0,
 [Parameter(Mandatory=$true)][string]$Screenshot,[string]$ExpectText)
$ErrorActionPreference='Stop'
if([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($Screenshot)) -ne 'D:\'){throw 'D screenshot required'}
if(Test-Path -LiteralPath $Screenshot){throw 'Existing evidence is never overwritten'}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
. (Join-Path $PSScriptRoot 'find-panel-window.ps1')
$window=Get-WorkflowPanelWindow $ProcessId
$window.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([Windows.Automation.WindowVisualState]::Normal)
$window.SetFocus()
$tabs=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::TabItem))
if($tabs.Count -ne 4){throw 'Expected workflow/detail/diagnostic/intake tabs'}
$tabs[$Tab].GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
$deadline=[DateTime]::UtcNow.AddSeconds(8)
do {
 Start-Sleep -Milliseconds 200
 $texts=$window.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text))
 $names=@($texts|ForEach-Object{$_.Current.Name})
 $found= -not $ExpectText -or (($names -join ' ') -like ('*'+$ExpectText+'*'))
}while(-not $found -and [DateTime]::UtcNow -lt $deadline)
if(-not $found){throw 'Expected actual UI text not observed'}
$rect=$window.Current.BoundingRectangle
if($rect.X -lt 0 -or $rect.Y -lt 0 -or $rect.Width -lt 900 -or $rect.Height -lt 600){throw 'Window not visible/offscreen'}
Add-Type -AssemblyName System.Drawing
$bitmap=New-Object Drawing.Bitmap ([int]$rect.Width),([int]$rect.Height)
$graphics=[Drawing.Graphics]::FromImage($bitmap)
try{$graphics.CopyFromScreen([int]$rect.X,[int]$rect.Y,0,0,$bitmap.Size);$bitmap.Save($Screenshot,[Drawing.Imaging.ImageFormat]::Png)}finally{$graphics.Dispose();$bitmap.Dispose()}
[ordered]@{observedAt=[DateTimeOffset]::Now.ToString('o');processId=$ProcessId;tab=$Tab;screenshot=$Screenshot;text=$names;expectedTextObserved=$found}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath ($Screenshot+'.observation.json') -Encoding utf8
Write-Output ('Actual UI captured: '+$Screenshot)
