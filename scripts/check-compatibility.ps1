# ArcGIS Pro MCP read-only version and compatibility checker.
# This script inspects host identity only; it never opens a project or GIS data.
[CmdletBinding()]
param(
    [string]$PolicyPath = '',
    [string]$ManifestPath = '',
    [string]$ConfigPath = '',
    [string]$ArcGISProPath = '',
    [string]$PythonPath = '',
    [string]$FactsPath = '',
    [string]$JsonPath = '',
    [switch]$Json
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$policyFile = if ($PolicyPath) { [IO.Path]::GetFullPath($PolicyPath) } else { Join-Path $repoRoot 'Config\compatibility-policy.json' }
$manifestFile = if ($ManifestPath) { [IO.Path]::GetFullPath($ManifestPath) } else { Join-Path $repoRoot 'Source\ArcGISProMCP.Compatibility\bin\x64\Debug\net6.0-windows\ArcGISProMCP.Compatibility.release-manifest.json' }
# D-114 (G-247 R-3): the outer Source config is net6-only metadata (desktopVersion 3.0); defaulting to it made the
# gate able to reject a net8 payload it was checking (D-111 class). Callers must pass the payload's own Config.daml.
$configFile = if ($ConfigPath) { [IO.Path]::GetFullPath($ConfigPath) } else { throw 'CONFIG_DAML_NOT_SPECIFIED: pass -ConfigPath with the Config.daml extracted from the payload under test (no outer net6 fallback).' }
$proFile = if ($ArcGISProPath) { [IO.Path]::GetFullPath($ArcGISProPath) } else { 'C:\Program Files\ArcGIS\Pro\bin\ArcGISPro.exe' }
$pyFile = if ($PythonPath) { [IO.Path]::GetFullPath($PythonPath) } else { 'C:\Program Files\ArcGIS\Pro\bin\Python\envs\arcgispro-py3\python.exe' }
function New-Check($name, $status, $expected, $actual, $evidence) { [pscustomobject]@{ name=$name; status=$status; expected=$expected; actual=$actual; evidence=$evidence } }
function Read-JsonFile($path) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "missing file '$path'" }; Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json }
function Get-PeMachine($path) {
    try {
        $stream=[IO.File]::OpenRead($path);$reader=[IO.BinaryReader]::new($stream)
        $stream.Seek(0x3c,[IO.SeekOrigin]::Begin)|Out-Null;$peOffset=$reader.ReadInt32()
        $stream.Seek($peOffset+4,[IO.SeekOrigin]::Begin)|Out-Null;$machine=$reader.ReadUInt16()
        $reader.Dispose();$stream.Dispose()
        if($machine -eq 0x8664){return 'AMD64'};if($machine -eq 0x014c){return 'I386'};return ('0x{0:X4}' -f $machine)
    } catch { return $null }
}
function Get-DotNet8FromSharedFolder {
    # D-054 A2：CLI 子进程输出在某些宿主中不可捕获（例如被包装/沙箱化的 PowerShell 会话），
    # 此时 `dotnet --list-runtimes` 会返回空 ⇒ 退化为**文件系统事实**：直接查 shared\Microsoft.NETCore.App\8.x。
    # 该路径是 .NET 官方运行时布局，只读、无副作用；命中即视为 .NET 8 运行时在场（证据标注来源）。
    $roots = [System.Collections.Generic.List[string]]::new()
    $pf = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    if (-not [string]::IsNullOrWhiteSpace($pf)) { $roots.Add((Join-Path $pf 'dotnet\shared\Microsoft.NETCore.App')) }
    foreach ($envRoot in @($env:DOTNET_ROOT, $env:ProgramRoot)) {
        if (-not [string]::IsNullOrWhiteSpace($envRoot)) { $roots.Add((Join-Path $envRoot 'shared\Microsoft.NETCore.App')) }
    }
    $roots.Add('C:\Program Files\dotnet\shared\Microsoft.NETCore.App')
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        $majors = @(Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
                    Where-Object { $_.Name -match '^8\.' } | ForEach-Object { $_.Name })
        if ($majors.Count -gt 0) { return ($root + ' -> ' + ($majors -join ', ')) }
    }
    return $null
}
function Get-DotNetSharedRuntime {
    # D-060：net6 目标框架 ⇒ 通用探测「shared\Microsoft.NETCore.App 下 >= 指定主版本 的运行时目录」。
    # 与 Get-DotNet8FromSharedFolder 同源（文件系统事实，只读），用于 CLI 输出不可捕获的宿主。
    param([int]$MinimumMajor = 6)
    $roots = [System.Collections.Generic.List[string]]::new()
    $pf = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    if (-not [string]::IsNullOrWhiteSpace($pf)) { $roots.Add((Join-Path $pf 'dotnet\shared\Microsoft.NETCore.App')) }
    foreach ($envRoot in @($env:DOTNET_ROOT, $env:ProgramRoot)) {
        if (-not [string]::IsNullOrWhiteSpace($envRoot)) { $roots.Add((Join-Path $envRoot 'shared\Microsoft.NETCore.App')) }
    }
    $roots.Add('C:\Program Files\dotnet\shared\Microsoft.NETCore.App')
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        $hits = @(Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
                  Where-Object { $_.Name -match ('^(\d+)\.') -and [int]$Matches[1] -ge $MinimumMajor } |
                  ForEach-Object { $_.Name })
        if ($hits.Count -gt 0) { return ($root + ' -> ' + ($hits -join ', ')) }
    }
    return $null
}
function Resolve-DotNetCommand {
    # D-054 A2：宿主探测健壮性 —— 仅靠 PATH 解析 dotnet 会漏检（例如 PATH 条目为 MSYS/Git-Bash 形态，
    # Windows PowerShell 的 Get-Command 无法解析）⇒ 依次回退到标准安装位与 DOTNET_ROOT。
    # 注意：环境变量可能缺失（进程环境被裁剪），故**不直接 Join-Path $env:ProgramFiles**（空值绑定会抛错），
    # 改用 [Environment]::GetFolderPath 取系统目录。
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source -and (Test-Path -LiteralPath $cmd.Source -PathType Leaf)) { return $cmd.Source }
    $candidates = [System.Collections.Generic.List[string]]::new()
    $pf = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    if (-not [string]::IsNullOrWhiteSpace($pf)) { $candidates.Add((Join-Path $pf 'dotnet\dotnet.exe')) }
    $pf86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
    if (-not [string]::IsNullOrWhiteSpace($pf86)) { $candidates.Add((Join-Path $pf86 'dotnet\dotnet.exe')) }
    foreach ($envRoot in @($env:DOTNET_ROOT, $env:ProgramW6432)) {
        if (-not [string]::IsNullOrWhiteSpace($envRoot)) { $candidates.Add((Join-Path $envRoot 'dotnet.exe')) }
    }
    $candidates.Add('C:\Program Files\dotnet\dotnet.exe')
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    return $null
}
function Invoke-ArcPyProbe($path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return [pscustomobject]@{ error='missing'; path=$path } }
    $runPath=$path
    if([string]$path -eq 'C:\Program Files\ArcGIS\Pro\bin\Python\envs\arcgispro-py3\python.exe'){$runPath='C:\Program Files\ArcGIS\Pro\bin\Python\envs\arcgispro-py3\python.exe'}
    $code = 'import sys,arcpy; i=arcpy.GetInstallInfo(); print(sys.version.split()[0]+"|"+str(sys.maxsize)+"|"+str(i.get("Version"))+"|"+str(i.get("ProductName")))'
    try {
        $pythonRoot=Split-Path (Split-Path (Split-Path $path))
        $envUtils=Join-Path (Split-Path $pythonRoot) 'PythonEnvUtils.exe'
        if(Test-Path -LiteralPath $envUtils -PathType Leaf) {
            # D-054 A2：PythonEnvUtils 无输出或失败时不得对 $null 调方法（原实现在此抛
            # "不能对 Null 值表达式调用方法"，把可诊断的探测降级为 NOT_VERIFIED）。
            $condaPrefix = $null
            try { $condaPrefix = (& $envUtils 2>$null | Select-Object -First 1) } catch { $condaPrefix = $null }
            if (-not [string]::IsNullOrWhiteSpace([string]$condaPrefix)) {
                $env:CONDA_PREFIX=([string]$condaPrefix).Trim()
                $env:PATH=$env:CONDA_PREFIX+';'+$env:CONDA_PREFIX+'\Scripts;'+$env:CONDA_PREFIX+'\Library\bin;'+$env:PATH
            }
        }
        $env:CONDA_DEFAULT_ENV='arcgispro-py3'
        $env:CONDA_PREFIX=(Split-Path $runPath)
        $env:PATH=$env:CONDA_PREFIX+';'+$env:CONDA_PREFIX+'\Scripts;'+$env:CONDA_PREFIX+'\Library\bin;'+$env:PATH
        $lines=@(& $runPath -c "import sys,arcpy; i=arcpy.GetInstallInfo(); print(sys.version.split()[0]+'|'+str(sys.maxsize)+'|'+str(i.get('Version'))+'|'+str(i.get('ProductName')))" 2>&1)
        if($LASTEXITCODE -ne 0){return [pscustomobject]@{error=(($lines | ForEach-Object { $_.ToString() }) -join ' ');path=$path}}
        $line=($lines | ForEach-Object { $_.ToString() } | Where-Object { $_ -match '^\d+\.\d+\.\d+\|' } | Select-Object -Last 1)
        if(-not $line){return [pscustomobject]@{error='ArcPy probe returned no parseable fact';path=$path}}
        $parts=$line -split '\|'
        return [pscustomobject]@{path=$path;version=$parts[0];architecture=if([int64]$parts[1] -gt 4294967296){'64bit'}else{'32bit'};arcpyImported=$true;arcpyVersion=$parts[2];arcpyProduct=$parts[3]}
    }
    catch { return [pscustomobject]@{error=$_.Exception.Message;path=$path} }
}
function Add-StatusCheck($list,$name,$condition,$expected,$actual,$evidence,$falseStatus='UNSUPPORTED') { $s=$falseStatus; if($condition){$s='PASS'}; $list.Add((New-Check $name $s $expected $actual $evidence)) }
try {
    $policy=Read-JsonFile $policyFile; $checks=[System.Collections.Generic.List[object]]::new()
    $facts=if($FactsPath){Read-JsonFile ([IO.Path]::GetFullPath($FactsPath))}else{$null}
    if($facts){$os=$facts.os;$pro=$facts.arcgisPro;$sdk=$facts.sdk;$dn=$facts.dotnet;$py=$facts.python}
    else {
        $os=[pscustomobject]@{platform='Windows';architecture=([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString());version=[Environment]::OSVersion.Version.ToString()}
        if(Test-Path -LiteralPath $proFile -PathType Leaf){$v=[Diagnostics.FileVersionInfo]::GetVersionInfo($proFile);$pro=[pscustomobject]@{installed=$true;path=$proFile;fileVersion=$v.FileVersion;productVersion=$v.ProductVersion;architecture=(Get-PeMachine $proFile)}}else{$pro=[pscustomobject]@{installed=$false;path=$proFile}}
        $sdkItems=@('ArcGIS.Core.dll','ArcGIS.Desktop.Framework.dll')|ForEach-Object{Join-Path (Split-Path $proFile) $_};$assemblies=@($sdkItems|Where-Object{Test-Path -LiteralPath $_}|ForEach-Object{$v=[Diagnostics.FileVersionInfo]::GetVersionInfo($_);[pscustomobject]@{path=$_;fileVersion=$v.FileVersion;productVersion=$v.ProductVersion}})
        # D-055 S6 修复（工具链缺陷）：本分支此前只计算 $assemblies 却**从未组装 $sdk** ⇒ 主机探针路径下
        # $sdk 为 $null，'arcgis-pro-sdk' 恒判 NOT_INSTALLED/'missing'（仅在注入 FactsPath 时正常），
        # 进而使兼容门在真实主机上被误拒（exit 11）—— 与注入 facts 路径语义对齐。
        $sdk=[pscustomobject]@{installed=($assemblies.Count -eq 2);assemblies=$assemblies}
        $dotnetPath=Resolve-DotNetCommand;$runtimeLines=@();if($dotnetPath){$runtimeLines=@(& $dotnetPath --list-runtimes 2>&1)};$majors=@($runtimeLines|ForEach-Object{if($_ -match 'Microsoft\.NETCore\.App\s+(\d+)\.'){[int]$Matches[1]}});$runtimeMajor=0;if($majors.Count -gt 0){$runtimeMajor=($majors|Measure-Object -Maximum).Maximum};$dotnetEvidence=if($runtimeLines.Count -gt 0){($runtimeLines -join '; ')}elseif($dotnetPath){'host probe: dotnet found at '+$dotnetPath+' but no runtime list was returned'}else{'host probe: dotnet CLI not located on PATH, %ProgramFiles%\dotnet or DOTNET_ROOT'};if($runtimeMajor -lt 6){$shared=Get-DotNetSharedRuntime -MinimumMajor 6;if($shared){$runtimeMajor=[int]([regex]::Match($shared,'(\d+)\.').Groups[1].Value);$dotnetEvidence='host probe (filesystem): '+$shared}};$hasRuntime=($runtimeMajor -ge 6);$dn=[pscustomobject]@{runtimePresent=$hasRuntime;runtimeMajor=$runtimeMajor;evidence=$dotnetEvidence};$py=Invoke-ArcPyProbe $pyFile
    }
    # D-069：双代 TFM —— manifest 提前读取并解析变体（supportedTargetFrameworks 集合 + tfmVariants，缺省回退 net6 规范）
    $manifest=Read-JsonFile $manifestFile;[xml]$cfg=Get-Content -LiteralPath $configFile -Raw -Encoding UTF8;$node=$cfg.SelectSingleNode("/*[local-name()='ArcGIS']/*[local-name()='AddInInfo']");$tfm=[string]$manifest.target.targetFramework;$tfmSet=@($policy.product.supportedTargetFrameworks|ForEach-Object{[string]$_});$tfmOk=$tfmSet -contains $tfm;$variant=$null;if($tfmOk){$vp=$policy.support.tfmVariants.PSObject.Properties[$tfm];if($vp){$variant=$vp.Value}};if($null -eq $variant){$variant=[pscustomobject]@{dotnet=$policy.support.dotnet;requiredDesktopVersion=[string]$policy.support.arcgisPro.requiredDesktopVersion;sdkPrefixes=@($policy.support.arcgisPro.supportedSdkAssemblyProductVersionPrefixes);supportedSeries=@($policy.support.arcgisPro.supportedSeries);verifiedSeries=@($policy.support.arcgisPro.verifiedSeries);notVerifiedSeries=@($policy.support.arcgisPro.notVerifiedSeries);otherVersions=$policy.support.arcgisPro.otherVersions;supportStatement=[string]$policy.support.arcgisPro.supportStatement}}
    Add-StatusCheck $checks 'windows-platform' ($os.platform -eq 'Windows') 'Windows' $os.platform 'OS family'
    Add-StatusCheck $checks 'windows-architecture' ([string]$os.architecture -match '(?i)x64|amd64|64bit') 'x64' $os.architecture 'Host architecture'
    if(-not $pro.installed){$checks.Add((New-Check 'arcgis-pro-installed' 'NOT_INSTALLED' 'ArcGIS Pro 3.0+（verified 3.5；not-verified 3.0-3.4）' ('missing: '+$pro.path) 'Required component was not found.'))}
    else{$series=(([string]$pro.productVersion -split '\.')[0..1]-join '.');$sup=@($variant.supportedSeries);$ver=@($variant.verifiedSeries);$nv=@($variant.notVerifiedSeries);$vSt='UNSUPPORTED';$vNote='';if(-not ($series -match '^\d+\.\d+$')){$vSt='NOT_VERIFIED';$vNote='宿主版本无法解析（未知）'}elseif($sup -contains $series){if($ver -contains $series){$vSt='PASS';$vNote='已实测支持'}else{$vSt='PASS';$vNote='编译期兼容；运行期未验证（NOT VERIFIED）'}}else{$ovKeys=@($variant.otherVersions.PSObject.Properties.Name);$isOth=$false;foreach($k in $ovKeys){if(([string]$k).TrimEnd('+') -eq $series){$isOth=$true}};if($isOth){$vSt='NOT_VERIFIED';$vNote='不在支持范围且未实测（NOT VERIFIED）'}else{$vSt='UNSUPPORTED';$vNote='超出支持范围'}};$checks.Add((New-Check 'arcgis-pro-version' $vSt ([string]$variant.supportStatement+' [verified='+(($ver)-join '/')+';not-verified='+(($nv)-join '/')+']') $pro.productVersion ('Series='+$series+'; '+$vNote+'; File='+$pro.fileVersion+'; Product='+$pro.productVersion)));$proArchStatus='NOT_VERIFIED';if($pro.architecture){$proArchStatus='UNSUPPORTED';if([string]$pro.architecture -eq [string]$policy.support.arcgisPro.requiredExecutableMachine){$proArchStatus='PASS'}};$checks.Add((New-Check 'arcgis-pro-architecture' $proArchStatus 'AMD64 PE machine' $pro.architecture 'ArcGISPro.exe PE header machine'))}
    if(-not $sdk.installed){$checks.Add((New-Check 'arcgis-pro-sdk' 'NOT_INSTALLED' 'ArcGIS.Core.dll and ArcGIS.Desktop.Framework.dll' 'missing' 'Required SDK assemblies were not found.'))}else{$sdkVersions=@($sdk.assemblies|ForEach-Object{[string]$_.productVersion});$sdkPrefixes=@($variant.sdkPrefixes);$sdkOk=0;foreach($sv in $sdkVersions){foreach($pfx in $sdkPrefixes){if($sv -like ($pfx+'*')){$sdkOk++;break}}};$sdkPass=($sdkVersions.Count -eq 2 -and $sdkOk -eq 2);$sdkStatus='UNSUPPORTED';if($sdkPass){$sdkStatus='PASS'};$checks.Add((New-Check 'arcgis-pro-sdk' $sdkStatus ('SDK assemblies 属 13.0-13.5（支持范围 '+(($sdkPrefixes|ForEach-Object{$_.TrimEnd('.')})-join '/')+'）') ($sdkVersions-join ', ') 'Read-only assembly file metadata'))}
    $minMajor=[int]$variant.dotnet.minimumRuntimeMajor;$dotnetPass=([bool]$dn.runtimePresent -and [int]$dn.runtimeMajor -ge $minMajor);$dotnetStatus='NOT_INSTALLED';if($dotnetPass){$dotnetStatus='PASS'};$dotnetEvidence=if($facts){'injected facts'}else{'host probe'};if($dn.evidence){$dotnetEvidence=$dn.evidence};$checks.Add((New-Check 'dotnet-runtime' $dotnetStatus ('.NET '+$minMajor+'+ runtime（'+$tfm+' 目标框架）') $dn.runtimeMajor $dotnetEvidence))
    if($py.error){$pyStatus='NOT_VERIFIED';if($py.error -eq 'missing'){$pyStatus='NOT_INSTALLED'};$checks.Add((New-Check 'arcgis-python-arcpy' $pyStatus 'Python 3.11 + ArcPy import' $py.error 'Read-only ArcGIS Python probe'))}else{$pyPass=([string]$py.version-like '3.11*' -and [string]$py.arcpyVersion -eq '3.5');$pyStatus='UNSUPPORTED';if($pyPass){$pyStatus='PASS'};$checks.Add((New-Check 'arcgis-python-arcpy' $pyStatus 'Python 3.11 + ArcPy 3.5' (([string]$py.version)+' / '+[string]$py.arcpyVersion) 'ArcPy GetInstallInfo read-only probe'))}
    # (manifest/Config/node 已在变体解析处提前读取；此处不再重复读取)
    $identityOk=([string]$manifest.product.releaseVersion -eq [string]$policy.product.releaseVersion -and [string]$manifest.product.addInId -eq [string]$policy.product.addInId -and $tfmOk -and [string]$manifest.target.platform -eq [string]$policy.product.platform -and [string]$node.version -eq [string]$policy.product.releaseVersion -and $null -ne $variant -and ([string]$manifest.target.arcgisProDesktopVersion -eq [string]$variant.requiredDesktopVersion) -and ([string]$node.desktopVersion -eq [string]$variant.requiredDesktopVersion))
    $identityStatus='ERROR';if($identityOk){$identityStatus='PASS'};$checks.Add((New-Check 'release-identity-consistency' $identityStatus 'manifest/Config/policy consistent（双代 TFM 变体判据）' ('manifest='+$manifest.product.releaseVersion+'; TFM='+$tfm+'; Config='+$node.version+'; desktop='+$node.desktopVersion) 'Release manifest, Config.daml and policy only'))
    $rank=@{ERROR=1;UNSUPPORTED=2;NOT_INSTALLED=3;NOT_VERIFIED=4;PASS=5};$overall=($checks|Sort-Object{$rank[$_.status]}|Select-Object -First 1).status;$exit=[int]$policy.exitCodes.$overall
    $out=[ordered]@{schema='arcgis-pro-mcp-compatibility-report-v1';policySchema=$policy.schema;policyVersion=$policy.policyVersion;checkedAtUtc=[DateTime]::UtcNow.ToString('o');overallStatus=$overall;exitCode=$exit;currentHostEvidence=(-not [bool]$facts);checks=@($checks)}
    # D-054 A2：可选**文件握手** —— 某些宿主（被包装/沙箱化的 PowerShell）无法捕获子进程 stdout，
    # 调用方（如 release-transaction.ps1）改用本文件读取报告，避免把「读不到报告」误判为「兼容门拒绝」。
    if ($JsonPath) { $jsonFullPath=[IO.Path]::GetFullPath($JsonPath);$jsonParent=Split-Path -Parent $jsonFullPath;if($jsonParent -and -not (Test-Path -LiteralPath $jsonParent)){New-Item -ItemType Directory -Force -Path $jsonParent|Out-Null};$out | ConvertTo-Json -Depth 10 | Out-File -LiteralPath $jsonFullPath -Encoding UTF8 }
    if($Json){$out|ConvertTo-Json -Depth 10}else{Write-Host ('Compatibility: '+$overall+' (exit '+$exit+')');$checks|ForEach-Object{Write-Host ('{0}: {1} - {2}'-f $_.name,$_.status,$_.actual)};Write-Host ('JSON schema: '+$out.schema)};exit $exit
}catch{$err=[ordered]@{schema='arcgis-pro-mcp-compatibility-report-v1';overallStatus='ERROR';exitCode=1;error=$_.Exception.Message};if($JsonPath){$jsonFullPath=[IO.Path]::GetFullPath($JsonPath);$jsonParent=Split-Path -Parent $jsonFullPath;if($jsonParent -and -not (Test-Path -LiteralPath $jsonParent)){New-Item -ItemType Directory -Force -Path $jsonParent|Out-Null};$err|ConvertTo-Json -Depth 6|Out-File -LiteralPath $jsonFullPath -Encoding UTF8};if($Json){$err|ConvertTo-Json}else{Write-Error $err.error};exit 1}
