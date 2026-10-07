@echo off
REM ============================================================================
REM  ArcGIS Pro MCP 一键部署入口：恢复菜单（图形窗口）
REM  作用：只调用 scripts\one-click-setup.ps1（窗口式恢复入口（回滚 / 恢复客户端 / 卸载））；本入口自身不做写入。
REM  纪律：入口先做**依赖存在性断言**并 fail-fast（REFERENCE §X-4：路径断链会伪装成"卡死"）。
REM ============================================================================
setlocal
set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
set "HERE=%~dp0"
set "ENTRY=%HERE%scripts\one-click-setup.ps1"
if not exist "%ENTRY%" goto :no_entry
if not exist "%PS%" goto :no_ps
echo.
echo  ArcGIS Pro MCP - 恢复菜单（图形窗口）
echo.
"%PS%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%ENTRY%" -Action GUI
set "RC=%ERRORLEVEL%"
echo.
echo  退出码：%RC%
pause
endlocal & exit /b %RC%

:no_entry
echo [FAIL] 入口脚本缺失：%ENTRY%
echo        请确认 ZIP 已完整解压（本入口应与 scripts\ 目录同级）。
pause
endlocal & exit /b 3

:no_ps
echo [FAIL] 未找到 Windows PowerShell：%PS%
pause
endlocal & exit /b 4
