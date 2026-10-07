@echo off
REM ============================================================================
REM  ArcGIS Pro MCP  ArcGIS Pro MCP
REM  AddIn ID        : {BAA5628C-3C08-4AD5-A6C0-915ADA475709}
REM  Release version : 1.0.2     Tools: 149     Error codes: 33
REM  Target          : ArcGIS Pro 3.0+ (x64, net6.0-windows)
REM  Package         : ArcGISProMCP.Compatibility.esriAddInX  (identity per release-manifest.json)
REM  Package SHA-256 : (not embedded; see release-manifest.json)
REM  ---------------------------------------------------------------------------
REM  本入口件只做指引/调用；一切写入动作由 scripts\ 下的事务脚本执行（可回滚）。
REM ============================================================================
setlocal
set "REPO=%~dp0.."
set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
echo.
echo  卸载前请先关闭 ArcGIS Pro（事务脚本检测到 Pro 运行时会拒绝变更）。
echo.
echo  [1/2] 预检 Preflight（DryRun，不改动系统）
"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\release-transaction.ps1" -Action Preflight -DryRun
if errorlevel 1 (
  echo  预检未通过（exit %errorlevel%）。卸载已中止。
  endlocal ^& exit /b 1
)

echo.
echo  [2/2] 事务式卸载（失败可 Rollback）
"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\release-transaction.ps1" -Action Uninstall
set "RC=%errorlevel%"
if not "%RC%"=="0" (
  echo  卸载未成功（exit %RC%）。
  endlocal ^& exit /b %RC%
)
echo.
echo  卸载完成。如需一并移除客户端配置：
echo    "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\client-config.ps1" -Action Restore -Client codex -Json
echo.
endlocal
pause
