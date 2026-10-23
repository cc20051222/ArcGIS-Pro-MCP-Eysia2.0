[CmdletBinding()]
param([switch]$PlanOnly,[string]$WorkRoot,[string]$AgentWorkspace)
$ErrorActionPreference='Stop'
if(-not $WorkRoot){$WorkRoot=Read-Host '运行记录目录（D盘，回车采用 D:\ArcGIS-Pro-MCP-2.1）';if(-not $WorkRoot){$WorkRoot='D:\ArcGIS-Pro-MCP-2.1'}}
if(-not $AgentWorkspace){$AgentWorkspace=Read-Host 'AI客户端打开的项目工作区（D盘，回车采用运行目录）';if(-not $AgentWorkspace){$AgentWorkspace=$WorkRoot}}
$installer=Join-Path $PSScriptRoot '安装配套.ps1'
& $installer -Action Plan -WorkRoot $WorkRoot -AgentWorkspace $AgentWorkspace
if($PlanOnly){return}
Write-Host 'ArcGIS Pro MCP 2.1：先保存并正常关闭 ArcGIS Pro。安装会备份旧文件，仅更新本插件GUID安装/缓存目录及选定D盘目录。'
Write-Host '不会安装依赖或修改客户端连接配置。Skill需客户端重新加载。'
$answer=Read-Host '确认后输入 INSTALL，其他输入退出'
if($answer -cne 'INSTALL'){Write-Host '已退出，未安装。';return}
& $installer -Action InstallAll -WorkRoot $WorkRoot -AgentWorkspace $AgentWorkspace -UpdateExisting -AllowOnlyThisAddinOnC
Write-Host ('安装完成。客户端连接地址：http://127.0.0.1:6520/mcp；运行目录：'+$WorkRoot)
Write-Host '按05_说明的安装教程，从启动配套.ps1启动已有工程，然后在Pro的MCP页签启动服务。'
