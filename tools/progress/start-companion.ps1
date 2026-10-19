[CmdletBinding()]
param([ValidateSet('Panel','Pro')][string]$Mode='Panel',[string]$WorkRoot='D:\ArcGIS-Pro-MCP-Companion',
 [string]$Project,[string]$TaskRecord)
$ErrorActionPreference='Stop'
$WorkRoot=[IO.Path]::GetFullPath($WorkRoot)
if([IO.Path]::GetPathRoot($WorkRoot) -ne 'D:\'){throw 'WorkRoot must be on D'}
if(-not $TaskRecord){$TaskRecord=Join-Path $WorkRoot 'task-record.json'}
$TaskRecord=[IO.Path]::GetFullPath($TaskRecord)
foreach($p in @($WorkRoot,$TaskRecord)){
 if([IO.Path]::GetPathRoot($p) -ne 'D:\'){throw 'D paths required'}
 for($a=$p;$a;$a=[IO.Path]::GetDirectoryName($a)){if((Test-Path -LiteralPath $a) -and ((Get-Item -LiteralPath $a -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Reparse path refused'}}
}
foreach($d in @('temp','events','managed-logs','jobs','gp-audit','transient','map-images','raster-temp')){
 $dir=Join-Path $WorkRoot $d
 [IO.Directory]::CreateDirectory($dir)|Out-Null
 $probe=Join-Path $dir ('.write-probe-'+[guid]::NewGuid().ToString('N'))
 [IO.File]::WriteAllText($probe,'D')
 if([IO.File]::ReadAllText($probe) -cne 'D'){throw ('D write probe failed: '+$dir)}
 Remove-Item -LiteralPath $probe
}
$env:TEMP=Join-Path $WorkRoot 'temp';$env:TMP=$env:TEMP;$env:ARCTEMPDIR=$env:TEMP
$env:ARCGIS_PRO_MCP_LOG_DIRECTORY=Join-Path $WorkRoot 'managed-logs'
$env:ARCGIS_PRO_MCP_PROGRESS_DIRECTORY=Join-Path $WorkRoot 'events'
$env:ARCGIS_PRO_MCP_PROGRESS_TASK_RECORD=$TaskRecord
$env:ARCGIS_PRO_MCP_GP_AUDIT_DIR=Join-Path $WorkRoot 'gp-audit'
$env:ARCGIS_PRO_MCP_JOBS_DIR=Join-Path $WorkRoot 'jobs'
$env:ARCGIS_PRO_MCP_TRANSIENT_ROOT=Join-Path $WorkRoot 'transient'
$env:ARCGIS_PRO_MCP_MAP_IMAGE_DIR=Join-Path $WorkRoot 'map-images'
$env:TempFolders=Join-Path $WorkRoot 'raster-temp'
if($Mode -eq 'Panel'){
 $exe=Join-Path $WorkRoot 'panel/ArcGISProMCP.ProgressMonitor.exe'
 if(-not(Test-Path -LiteralPath $exe)){throw 'InstallPanel first. .NET 8 Windows Desktop Runtime is required, no dependency is auto-installed.'}
 Start-Process -FilePath $exe -ArgumentList @('--directory',('"'+$env:ARCGIS_PRO_MCP_PROGRESS_DIRECTORY+'"'),'--task-record',('"'+$TaskRecord+'"')) -WorkingDirectory $WorkRoot
}else{
 if(@(Get-Process ArcGISPro -ErrorAction SilentlyContinue).Count){throw 'Pro is already open; existing processes cannot inherit new path settings. Save and normally close first.'}
 if(-not $Project){throw 'Specify the existing user-selected D project with -Project'}
 $Project=[IO.Path]::GetFullPath($Project)
 if([IO.Path]::GetPathRoot($Project) -ne 'D:\' -or -not(Test-Path -LiteralPath $Project -PathType Leaf) -or [IO.Path]::GetExtension($Project) -ne '.aprx'){throw 'Existing D .aprx required'}
 $start=[Diagnostics.ProcessStartInfo]::new('C:\Program Files\ArcGIS\Pro\bin\ArcGISPro.exe');$start.UseShellExecute=$true
 $start.Arguments='"'+$Project+'"';$start.WorkingDirectory=[IO.Path]::GetDirectoryName($Project)
 $proc=[Diagnostics.Process]::Start($start);Write-Output ('Pro started '+$proc.Id+'; click MCP Start Server, then Task Panel. This launcher does not edit client configuration.')
}
Write-Output ('Publish the Agent task to '+$TaskRecord+'; events '+$env:ARCGIS_PRO_MCP_PROGRESS_DIRECTORY)
