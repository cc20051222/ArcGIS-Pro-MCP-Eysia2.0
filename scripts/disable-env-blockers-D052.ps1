#Requires -RunAsAdministrator
<#
.SYNOPSIS
  D-052 环境门阻断源处置：停用阿里 PC 安全服务（AlibabaProtect / AliPaladin）与 gameflt，并尝试立即卸载过滤驱动。
.DESCRIPTION
  背景：D 盘小文件操作被 minifilter 放大约 15~20 倍（delete 43~150ms，同盘 C 分区 <1ms），
  导致 arcpy GP 内部建删临时文件被拖到 12~50s，撞 MCP server 30s 请求超时。
  根因定位（多轮排除）：钉钉捆绑的「阿里 PC 安全服务」AlibabaProtect + AliPaladin(minifilter)。
  本脚本把服务启动类型改为 DISABLED，并尝试立即停止/卸载过滤驱动；
  本机服务受自我保护，通常仍需**重启**后才彻底生效。

  用法：右键本文件 →「使用 PowerShell 运行」（会弹出 UAC，点【是】）。
  执行后请**重启电脑**，然后回到 WorkBuddy 让我复测环境门。

  仅动服务/驱动启动状态，不删除任何文件、不卸载钉钉。
#>
$ErrorActionPreference = 'Continue'
Write-Host "=== ArcGIS-Pro-MCP：环境门阻断源处置 ===" -ForegroundColor Cyan
Write-Host ("当前时间: " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Write-Host ""

$targets = @(
    @{ Name = 'AlibabaProtect'; Desc = '阿里 PC 安全服务（用户态，钉钉捆绑）' },
    @{ Name = 'AliPaladin';     Desc = '阿里保护过滤驱动（minifilter）' },
    @{ Name = 'gameflt';        Desc = 'Windows 游戏输入过滤驱动（可选）' }
)

foreach ($t in $targets) {
    $n = $t.Name
    Write-Host ("--- " + $n + " (" + $t.Desc + ") ---") -ForegroundColor Yellow
    $svc = Get-Service -Name $n -ErrorAction SilentlyContinue
    if (-not $svc) {
        Write-Host "  未安装/不存在，跳过。"
        continue
    }
    Write-Host ("  处置前: Status=" + $svc.Status)

    # 1) 停止服务（自我保护可能拒绝 —— 属预期）
    try {
        Stop-Service -Name $n -Force -ErrorAction Stop
        Write-Host "  Stop-Service: OK" -ForegroundColor Green
    } catch {
        Write-Host ("  Stop-Service: 被拒（自我保护，属预期）: " + $_.Exception.Message.Split("`n")[0]) -ForegroundColor DarkYellow
    }

    # 2) 启动类型改为 DISABLED（重启后不再拉起）
    $r = & sc.exe config $n start= disabled 2>&1
    Write-Host ("  sc config start=disabled: " + ($r -join ' '))
    $reg = Get-ItemProperty ("HKLM:\SYSTEM\CurrentControlSet\Services\" + $n) -ErrorAction SilentlyContinue
    if ($reg) { Write-Host ("  注册表 Start 现为: " + $reg.Start + " (4=DISABLED)") }
}

Write-Host ""
Write-Host "--- 尝试立即卸载 minifilter（成功则无需等待重启） ---" -ForegroundColor Yellow
foreach ($drv in @('AliPaladin', 'AlibabaProtect')) {
    $r = & fltmc unload $drv 2>&1
    Write-Host ("  fltmc unload " + $drv + ": " + ($r -join ' '))
}

Write-Host ""
Write-Host "--- 处置后状态 ---" -ForegroundColor Yellow
foreach ($t in $targets) {
    $svc = Get-Service -Name $t.Name -ErrorAction SilentlyContinue
    if ($svc) { Write-Host ("  " + $t.Name + ": " + $svc.Status) }
}

Write-Host ""
Write-Host "★ 请立即重启电脑，使 DISABLED 启动类型生效（阿里保护在运行中会把设置改回 AUTO）。" -ForegroundColor Magenta
Write-Host "重启后回到 WorkBuddy 说一声，我会复测环境门（判据：D 盘中位 <= 5s）。" -ForegroundColor Magenta
Read-Host "按回车键关闭本窗口"
