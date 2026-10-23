[CmdletBinding()]
param([ValidateSet('Plan','InstallSkill','InstallPanel','InstallPlugin','InstallAll','Restore')][string]$Action='Plan',
 [string]$WorkRoot='D:\ArcGIS-Pro-MCP-Companion', [string]$AgentWorkspace,
 [switch]$AllowOnlyThisAddinOnC,[switch]$UpdateExisting,[string]$Receipt)
$ErrorActionPreference='Stop'
function Plain([string]$path,[switch]$AllowC) {
 $full=[IO.Path]::GetFullPath($path)
 if(-not $AllowC -and [IO.Path]::GetPathRoot($full) -ne 'D:\'){throw 'Project outputs must be on D'}
 for($p=$full;$p;$p=[IO.Path]::GetDirectoryName($p)){
  if((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Reparse path refused'}
 }
 return $full
}
$WorkRoot=Plain $WorkRoot
$bundle=Plain $PSScriptRoot
$manifest=Get-Content -LiteralPath (Join-Path $bundle 'companion-manifest.json') -Raw -Encoding utf8 | ConvertFrom-Json
$supported=@{'COMPANION_070_CANDIDATE'='0.7.0';'COMPANION_071_CANDIDATE'='0.7.1';'COMPANION_077_CANDIDATE'='0.7.7';'ARCGIS_PRO_MCP_2_1_RELEASE'='0.7.7'}
if(-not $supported.ContainsKey($manifest.kind)){throw 'Unexpected companion manifest'}
if($supported[$manifest.kind] -cne $manifest.skillVersion){throw 'Companion kind/version mismatch'}
if($manifest.kind -eq 'ARCGIS_PRO_MCP_2_1_RELEASE' -and $manifest.releaseVersion -cne '2.1.0'){throw 'Release version mismatch'}
$paths=@{}
foreach($f in $manifest.files){
 $full=Plain (Join-Path $bundle $f.path)
 if(-not $full.StartsWith($bundle+'\',[StringComparison]::OrdinalIgnoreCase) -or $paths.ContainsKey($full)){throw 'Unsafe/duplicate bundle path'}
 $paths[$full]=$true
 if(-not(Test-Path -LiteralPath $full -PathType Leaf) -or (Get-Item -LiteralPath $full).Length -ne $f.bytes -or (Get-FileHash -LiteralPath $full).Hash -cne $f.sha256){throw ('Bundle hash mismatch: '+$f.path)}
}
$guid='{BAA5628C-3C08-4AD5-A6C0-915ADA475709}'
$installRoot=Join-Path ([Environment]::GetFolderPath('MyDocuments')) ('ArcGIS\AddIns\ArcGISPro\'+$guid)
$cacheRoot=Join-Path $env:LOCALAPPDATA ('ESRI\ArcGISPro\AssemblyCache\'+$guid)
$pro='C:\Program Files\ArcGIS\Pro\bin\ArcGISPro.exe'
$restorePreview=$null
if($Action -eq 'Restore'){
 if(-not $Receipt){throw 'Specify a completed/failed install transaction receipt'}
 $Receipt=Plain $Receipt
 if(-not $Receipt.StartsWith($WorkRoot+'\transactions\',[StringComparison]::OrdinalIgnoreCase)){throw 'Receipt outside this work root'}
 $restorePreview=Get-Content -LiteralPath $Receipt -Raw -Encoding utf8 | ConvertFrom-Json
 if($restorePreview.agentWorkspace){$restorePreview.agentWorkspace=Plain $restorePreview.agentWorkspace}
}
$pluginActions=($Action -in @('InstallPlugin','InstallAll')) -or ($Action -eq 'Restore' -and @($restorePreview.changes|Where-Object {[IO.Path]::GetPathRoot($_.target) -eq 'C:\'}).Count -gt 0)
if($Action -in @('InstallSkill','InstallAll')){
 if(-not $AgentWorkspace){throw 'Specify the D AgentWorkspace for project-local Skill discovery'}
 $AgentWorkspace=Plain $AgentWorkspace
}
$plan=[ordered]@{action=$Action;status='PLAN_ONLY';skillVersion=$manifest.skillVersion;workRoot=$WorkRoot;agentWorkspace=$AgentWorkspace;
 pluginTarget=(Join-Path $installRoot 'ArcGISProMCP.Compatibility.esriAddInX');cacheTarget=$cacheRoot;pluginSupport='Pro 3.5 / net8 only for this candidate';
 clientConfigChanged=$false;originalR29Changed=$false;acceptance='PASS CANDIDATE, independent acceptance pending'}
if($manifest.kind -eq 'ARCGIS_PRO_MCP_2_1_RELEASE'){
 $plan['releaseVersion']=$manifest.releaseVersion
 $plan['pluginSupport']='ArcGIS Pro 3.5 / net8'
 $plan['acceptance']='User-authorized local acceptance; see 05_说明/RELEASE_2_1_VALIDATION.json; not third-party independent review'
}
if($Action -eq 'Plan'){$plan|ConvertTo-Json -Depth 6;return}
if($pluginActions){
 if(-not $AllowOnlyThisAddinOnC){throw 'Plugin update/restore needs -AllowOnlyThisAddinOnC for this GUID only'}
 if(@(Get-Process ArcGISPro -ErrorAction SilentlyContinue).Count){throw 'Save and normally close ArcGIS Pro before plugin update/restore; remaining must be 0'}
 if(@(Get-NetTCPConnection -LocalPort 6520,6511 -State Listen -ErrorAction SilentlyContinue).Count){throw 'Existing MCP/Bridge listener must be reconciled first'}
 $null=Plain $installRoot -AllowC;$null=Plain $cacheRoot -AllowC
 if(-not(Test-Path -LiteralPath $pro) -or (Get-Item -LiteralPath $pro).VersionInfo.FileVersion -notmatch '^3\.5\.') {throw 'This companion plugin requires actual ArcGIS Pro 3.5; original r29 retains its own compatibility matrix'}
}
[IO.Directory]::CreateDirectory($WorkRoot)|Out-Null
$temp=Join-Path $WorkRoot 'temp';[IO.Directory]::CreateDirectory($temp)|Out-Null;$env:TEMP=$temp;$env:TMP=$temp
$probe=Join-Path $WorkRoot ('.probe-'+[guid]::NewGuid().ToString('N'));[IO.File]::WriteAllText($probe,'D');if([IO.File]::ReadAllText($probe)-cne 'D'){throw 'D write probe failed'};Remove-Item -LiteralPath $probe
if($Action -eq 'Restore'){
 if(-not $Receipt){throw 'Specify a completed/failed install transaction receipt'}
 $Receipt=Plain $Receipt
 if(-not $Receipt.StartsWith($WorkRoot+'\transactions\',[StringComparison]::OrdinalIgnoreCase)){throw 'Receipt outside this work root'}
 $saved=$restorePreview
 if($saved.status -eq 'RESTORED'){throw 'Already restored'}
 foreach($f in $saved.changes){
  $allowed=$f.target.StartsWith($WorkRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or $f.target.StartsWith($installRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or $f.target.StartsWith($cacheRoot+'\',[StringComparison]::OrdinalIgnoreCase)
  if($saved.agentWorkspace){$allowed=$allowed -or $f.target.StartsWith((Join-Path $saved.agentWorkspace '.agents\skills\arcgis-pro-2-workflows')+'\',[StringComparison]::OrdinalIgnoreCase)}
  if(-not $allowed){throw 'Restore target outside transaction scope'}
  $null=Plain $f.target -AllowC
  if(-not(Test-Path -LiteralPath $f.target) -or (Get-FileHash -LiteralPath $f.target).Hash -cne $f.after){throw 'Target changed after installation; do not overwrite user edits'}
  if($f.before -and ((Get-FileHash -LiteralPath (Plain $f.backup)).Hash -cne $f.before)){throw 'Rollback backup mismatch'}
 }
 $reverse=@($saved.changes);[array]::Reverse($reverse)
 foreach($f in $reverse){
  if($f.before){Copy-Item -LiteralPath $f.backup -Destination $f.target -Force}else{Remove-Item -LiteralPath $f.target}
 }
 $saved.status='RESTORED';$saved|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $Receipt -Encoding utf8
 Write-Output ('Restored exact known files; receipt: '+$Receipt);return
}
$transaction=Join-Path $WorkRoot ('transactions\'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($transaction)|Out-Null
$receiptPath=Join-Path $transaction 'receipt.json'
$state=[ordered]@{status='IN_PROGRESS';action=$Action;at=[DateTimeOffset]::Now.ToString('o');agentWorkspace=$AgentWorkspace;changes=@();runtimeVerified=$false;independentAcceptance='PENDING'}
function Save-Receipt {$state|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $receiptPath -Encoding utf8}
function Put([string]$source,[string]$target,[switch]$Plugin){
 $target=Plain $target -AllowC:$Plugin
 $next=(Get-FileHash -LiteralPath $source).Hash
 if((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target).Hash -ceq $next){return}
 $before=$null;$backup=$null
 if(Test-Path -LiteralPath $target){
  if(-not $UpdateExisting){throw ('Existing different file; use -UpdateExisting to back up and update: '+$target)}
  $before=(Get-FileHash -LiteralPath $target).Hash;$backup=Join-Path $transaction ($state.changes.Count.ToString('D4')+'.bak')
  Copy-Item -LiteralPath $target -Destination $backup
  if((Get-FileHash -LiteralPath $backup).Hash -cne $before){throw 'Backup mismatch'}
 }
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
 $state.changes+=@{target=$target;before=$before;backup=$backup;after=$next};Save-Receipt
 Copy-Item -LiteralPath $source -Destination $target -Force
 if((Get-FileHash -LiteralPath $target).Hash -cne $next){throw 'Installed hash mismatch'}
}
Save-Receipt
try {
 if($Action -in @('InstallSkill','InstallAll')){
  foreach($f in $manifest.files|Where-Object {$_.path.StartsWith('02_Skill/')}){
   Put (Join-Path $bundle $f.path) (Join-Path (Join-Path $AgentWorkspace '.agents\skills\arcgis-pro-2-workflows') $f.path.Substring(9))
  }
 }
 if($Action -in @('InstallPanel','InstallAll')){
  foreach($f in $manifest.files|Where-Object {$_.path.StartsWith('03_Panel/')}){Put (Join-Path $bundle $f.path) (Join-Path (Join-Path $WorkRoot 'panel') $f.path.Substring(9))}
  foreach($d in @('events','managed-logs','jobs','gp-audit')){[IO.Directory]::CreateDirectory((Join-Path $WorkRoot $d))|Out-Null}
 }
 if($Action -in @('InstallPlugin','InstallAll')){
  $package=Join-Path $bundle '04_Plugin/ArcGISProMCP.Compatibility.esriAddInX'
  Put $package (Join-Path $installRoot 'ArcGISProMCP.Compatibility.esriAddInX') -Plugin
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $zip=[IO.Compression.ZipFile]::OpenRead($package)
  try {
   foreach($entry in $zip.Entries){
    $logical=$entry.FullName.Replace('\','/');if(-not $logical.StartsWith('Install/')){continue}
    $relative=$logical.Substring(8);if(-not $relative -or $relative.Split('/') -contains '..' -or $relative.Contains(':')){throw 'Unsafe add-in member'}
    $dest=[IO.Path]::GetFullPath((Join-Path $cacheRoot $relative));if(-not $dest.StartsWith($cacheRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Cache scope escape'}
    $stage=Join-Path $transaction ('payload/'+$relative);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($stage))|Out-Null
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$stage,$false);Put $stage $dest -Plugin
   }
  }finally{$zip.Dispose()}
  [IO.Directory]::SetLastWriteTimeUtc($cacheRoot,(Get-Item -LiteralPath (Join-Path $installRoot 'ArcGISProMCP.Compatibility.esriAddInX')).LastWriteTimeUtc)
 }
 $state.status='FILES_INSTALLED_RUNTIME_NOT_VERIFIED';Save-Receipt
 Write-Output ('Installed exact files. Restart client for Skill discovery; use launchers for D progress paths. Receipt: '+$receiptPath)
} catch {
 $state.status='FAILED_RECONCILE_BEFORE_RETRY';$state.error=$_.Exception.Message;Save-Receipt
 throw ('Installation stopped; keep backups and reconcile receipt before retry: '+$receiptPath+'; '+$_.Exception.Message)
}
