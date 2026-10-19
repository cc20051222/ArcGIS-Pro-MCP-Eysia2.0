[CmdletBinding()]
param(
    [string]$Project='D:\shiyanwenjaijia\工程\SkillExam_自测.aprx',
    [string]$DataRoot='D:\shiyanwenjaijia',
    [switch]$StartMcp
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runtime=Join-Path $root '.runtime\progress-panel'
$projectPath=[IO.Path]::GetFullPath($Project)
$dataPath=[IO.Path]::GetFullPath($DataRoot)
foreach($path in @($root,$projectPath,$dataPath)){
    if([IO.Path]::GetPathRoot($path) -ne 'D:\'){throw 'Project/runtime/data must be on D'}
    $ancestor=$path
    while($ancestor){
        if(Test-Path -LiteralPath $ancestor){if((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse path refused'}}
        $parent=[IO.Path]::GetDirectoryName($ancestor);if($parent -eq $ancestor){break};$ancestor=$parent
    }
}
if(-not(Test-Path -LiteralPath $projectPath -PathType Leaf)){throw 'Existing project required'}
if(@(Get-Process ArcGISPro -ErrorAction SilentlyContinue).Count){throw 'Pro is already running; no restart or retrofit of its environment performed'}
$session=Join-Path $dataPath ('运行记录\进度面板\'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
foreach($dir in @($runtime,$session,(Join-Path $session 'scratch'),(Join-Path $runtime 'events'),(Join-Path $runtime 'managed-logs'))){[IO.Directory]::CreateDirectory($dir)|Out-Null}
$probe=Join-Path $session '.write-probe';[IO.File]::WriteAllText($probe,'D');if([IO.File]::ReadAllText($probe) -cne 'D'){throw 'D write probe failed'};Remove-Item -LiteralPath $probe
$env:TEMP=Join-Path $session 'scratch';$env:TMP=$env:TEMP;$env:ARCTEMPDIR=$env:TEMP
$env:ARCGIS_PRO_MCP_LOG_DIRECTORY=Join-Path $runtime 'managed-logs'
$env:ARCGIS_PRO_MCP_PROGRESS_DIRECTORY=Join-Path $runtime 'events'
$env:ARCGIS_PRO_MCP_PROGRESS_TASK_RECORD=Join-Path $runtime 'task-record.json'
$env:ARCGIS_PRO_MCP_GP_AUDIT_DIR=Join-Path $session 'gp-audit'
$env:ARCGIS_PRO_MCP_JOBS_DIR=Join-Path $session 'jobs'
$env:ARCGIS_PRO_MCP_AUTOSTART_SERVER_FOR_VERIFICATION=if($StartMcp){'1'}else{'0'}
$start=[Diagnostics.ProcessStartInfo]::new('C:\Program Files\ArcGIS\Pro\bin\ArcGISPro.exe')
$start.UseShellExecute=$true;$start.Arguments='"'+$projectPath+'"';$start.WorkingDirectory=[IO.Path]::GetDirectoryName($projectPath)
$process=[Diagnostics.Process]::Start($start)
@{at=[DateTimeOffset]::Now.ToString('o');processId=$process.Id;project=$projectPath;runtime=$runtime;session=$session;startMcp=[bool]$StartMcp;installationChanged=$false;clientConfigWritten=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $session 'launch.json') -Encoding utf8
Write-Output ('Pro started: '+$process.Id+'; events: '+$env:ARCGIS_PRO_MCP_PROGRESS_DIRECTORY)
