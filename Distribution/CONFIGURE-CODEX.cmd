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
set "CLIENT=%~1"
if "%CLIENT%"=="" set "CLIENT=codex"
echo.
echo  目标客户端：%CLIENT%
echo.
echo  [1/2] 计划预览 Plan（只读，不写配置）
"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\client-config.ps1" -Action Plan -Client "%CLIENT%" -Json
if errorlevel 1 (
  echo  计划生成失败（exit %errorlevel%）。配置未修改。
  endlocal ^& exit /b 1
)

echo.
echo  [2/2] 应用配置 Apply
"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\client-config.ps1" -Action Apply -Client "%CLIENT%" -Json
set "RC=%errorlevel%"
if not "%RC%"=="0" (
  echo  配置应用未成功（exit %RC%）。可用 -Action Restore 还原。
  endlocal ^& exit /b %RC%
)
echo.
echo  配置完成。可用 -Action Validate -Client %CLIENT% 复核。
echo.
endlocal
pause
