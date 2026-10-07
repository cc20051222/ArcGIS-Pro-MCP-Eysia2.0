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
echo  本文件只做指引：不安装、不写配置、不改系统。
echo.
echo  按顺序执行（均在本 Distribution 目录内）：
echo    1) INSTALL-PLUGIN.cmd     安装（先做依赖检测，不通过则拒绝安装）
echo    2) CONFIGURE-CODEX.cmd    配置 MCP 客户端（默认 codex，参数可指定其它客户端）
echo    3) UNINSTALL-PLUGIN.cmd   卸载（请先关闭 ArcGIS Pro）
echo.
echo  只想先做依赖检测（不安装）：
echo    "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\check-compatibility.ps1" -Json
echo.
echo  完整说明（含未验证项如实披露）见同目录 README-START-HERE.md
echo.
endlocal
pause
