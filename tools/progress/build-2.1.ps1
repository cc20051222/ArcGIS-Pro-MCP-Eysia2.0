[CmdletBinding()]
param([switch]$Verify, [switch]$BuildNet8)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if ([IO.Path]::GetPathRoot($projectRoot) -ne 'D:\') { throw '构建根必须在 D 盘。' }
$runtime = Join-Path $projectRoot '.runtime\releases\v2.1\build'
$cache = Join-Path $env:USERPROFILE '.nuget\packages'
if (-not (Test-Path -LiteralPath $cache)) { throw '未找到已有只读 NuGet 缓存；本脚本不会联网安装依赖。' }
$env:APPDATA = Join-Path $env:USERPROFILE 'AppData\Roaming'
$env:LOCALAPPDATA = Join-Path $env:USERPROFILE 'AppData\Local'
$env:ProgramData = 'C:\ProgramData'; $env:ALLUSERSPROFILE = 'C:\ProgramData'
$env:ProgramFiles = 'C:\Program Files'; $env:CommonProgramFiles = 'C:\Program Files\Common Files'
$env:SystemRoot = 'C:\Windows'; $env:windir = 'C:\Windows'; $env:PUBLIC = 'C:\Users\Public'
$env:TEMP = Join-Path $runtime 'temp'; $env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = Join-Path $runtime 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $runtime 'nuget-packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'; $env:DOTNET_NOLOGO = '1'
$env:PROGRESS_CHECK_ROOT = $runtime
$env:ARCGIS_PRO_MCP_LIVE_TEST_ROOT = Join-Path $runtime ('server-live-tests\run-' + [guid]::NewGuid().ToString('N'))
$offline = Join-Path $runtime 'offline-source'
@($runtime,$env:TEMP,$env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES,$offline) | ForEach-Object { New-Item -ItemType Directory -Path $_ -Force | Out-Null }
$props = Join-Path $runtime 'build-paths.props'
$nuget = Join-Path $runtime 'NuGet.Config'
$escapedRoot = [Security.SecurityElement]::Escape($runtime + '\')
$propsBody = @'
<Project><PropertyGroup>
<ProgressBuildRoot>__ROOT__</ProgressBuildRoot>
<ProgressBuildRoot Condition="'$(ProgressBuildVariant)' != ''">$(ProgressBuildRoot)$(ProgressBuildVariant)\</ProgressBuildRoot>
<BaseIntermediateOutputPath>$(ProgressBuildRoot)obj\$(MSBuildProjectName)\</BaseIntermediateOutputPath>
<BaseOutputPath>$(ProgressBuildRoot)bin\$(MSBuildProjectName)\</BaseOutputPath>
<DefaultItemExcludes>$(DefaultItemExcludes);$(MSBuildProjectDirectory)\obj\**;$(MSBuildProjectDirectory)\bin\**</DefaultItemExcludes>
</PropertyGroup></Project>
'@
[IO.File]::WriteAllText($props,$propsBody.Replace('__ROOT__',$escapedRoot))
[IO.File]::WriteAllText($nuget,('<configuration><packageSources><clear/><add key="offline" value="' + [Security.SecurityElement]::Escape($offline) + '"/></packageSources><fallbackPackageFolders><clear/><add key="read-existing-cache" value="' + [Security.SecurityElement]::Escape($cache) + '"/></fallbackPackageFolders></configuration>'))
$flags = @('-p:CustomBeforeDirectoryBuildProps=' + $props)
function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw ('dotnet failed with exit ' + $LASTEXITCODE) }
}
function Restore-Local([string]$Project,[string[]]$Extra=@()) {
    Invoke-Dotnet -Arguments (@('restore',(Join-Path $projectRoot $Project),'--configfile',$nuget) + $flags + $Extra)
}
function Build-Local([string]$Project,[string[]]$Extra=@()) {
    Invoke-Dotnet -Arguments (@('build',(Join-Path $projectRoot $Project),'--no-restore') + $flags + $Extra)
}
Invoke-Dotnet -Arguments @('build-server','shutdown')
$monitor='Source\ArcGISProMCP.ProgressMonitor\ArcGISProMCP.ProgressMonitor.csproj'
$checks='Tests\ProgressPanelChecks\ArcGISProMCP.ProgressPanelChecks.csproj'
$compat='Source\ArcGISProMCP.Compatibility\ArcGISProMCP.Compatibility.csproj'
Restore-Local $monitor; Restore-Local $checks; Restore-Local $compat
Build-Local $monitor; Build-Local $checks; Build-Local $compat
if ($Verify) {
    Invoke-Dotnet -Arguments @((Join-Path $runtime 'bin\ArcGISProMCP.ProgressPanelChecks\Debug\net8.0\ArcGISProMCP.ProgressPanelChecks.dll'))
    $serverTests='Tests\ArcGISProMCP.ServerTests\ArcGISProMCP.ServerTests.csproj'
    Restore-Local $serverTests
    Invoke-Dotnet -Arguments (@('test',(Join-Path $projectRoot $serverTests),'--no-restore','--logger','trx;LogFileName=server-final.trx','--results-directory',(Join-Path $runtime 'test-results')) + $flags)
}
if ($BuildNet8) {
    $variant=@('-p:ProgressBuildVariant=net8')
    $rootFlags=$variant + @('-p:TargetFramework=net8.0-windows',('-p:ArcGISProSdkRefs=' + (Join-Path $projectRoot 'sdk-refs\pro-13.5-net8-compile\')),'-p:ArcGISProSdkExpectedVersionPrefix=13.5.')
    Restore-Local $compat $rootFlags
    # SDK project-reference negotiation retains Shared at net6. Restore those assets explicitly.
    foreach ($name in @('Tools','Server','Security')) {
        Restore-Local ('Source\Shared\ArcGISProMCP.'+$name+'\ArcGISProMCP.'+$name+'.csproj') $variant
    }
    Build-Local $compat $rootFlags
}
Write-Output ('构建/测试完成；未打包、未安装、未修改运行中的 Pro。证据：' + $runtime)
