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
echo  [1/3] 依赖检测：scripts\check-compatibility.ps1
echo        检测项：ArcGIS Pro / Pro SDK / .NET 8 runtime / arcpy(Python)
"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\check-compatibility.ps1" -Json
if errorlevel 1 (
  echo.
  echo  依赖检测未通过（exit %errorlevel%）。为避免静默修改系统，安装已中止。
  echo  请补齐环境后重试；单独重跑检测：
  echo    "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\check-compatibility.ps1" -Json
  endlocal ^& exit /b 1
)

echo.
echo  [2/3] 预检 Preflight（DryRun，不改动系统）
"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\release-transaction.ps1" -Action Preflight -DryRun
if errorlevel 1 (
  echo  预检未通过（exit %errorlevel%）。安装已中止。
  endlocal ^& exit /b 1
)

echo.
echo  [3/3] 事务式安装（写入前自动快照，失败可 Rollback）
"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\release-transaction.ps1" -Action Install
set "RC=%errorlevel%"
if not "%RC%"=="0" (
  echo  安装未成功（exit %RC%）。可用 -Action Rollback 回滚，或查看事务账本。
  endlocal ^& exit /b %RC%
)
echo.
echo  安装完成。下一步：CONFIGURE-CODEX.cmd
echo.
endlocal
pause
