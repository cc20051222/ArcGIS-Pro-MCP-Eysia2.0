@echo off
REM ============================================================================
REM  ArcGIS Pro MCP  ArcGIS Pro MCP
REM  AddIn ID        : {BAA5628C-3C08-4AD5-A6C0-915ADA475709}
REM  Release version : 1.0.2     Tools: 149     Error codes: 33
REM  Target          : ArcGIS Pro 3.0+ (x64, net6.0-windows)
REM  Package lineage : ArcGISProMCP.Compatibility.esriAddInX  (identity per release-manifest.json)  —— 现役包身份以 release-manifest.json 为准
REM  ---------------------------------------------------------------------------
REM  菜单式客户端配置向导：**逐客户端独立事务**（Plan 预览 -> 确认 -> Apply -> Validate）。
REM  本向导不提供批量应用：每个选项只处理一个客户端（逐客户端独立事务）。
REM  用法：CONFIGURE-CLIENT.cmd [client] [configRoot]
REM     client     可选；传入则跳过菜单直达（codex / cursor / deepseek-harness / claude-desktop）
REM     configRoot 可选；透传 -ConfigRoot（高级 / 测试用；默认使用真实用户目录）
REM  自动化调用注意：本仓库路径含空格，以绝对路径直接调用会被命令解释器在空格处截断
REM    => 请以本目录为工作目录、用相对脚本名调用（见 D-058 单测 D058ClientWizardTests 的调用方式）
REM ============================================================================
setlocal
set "REPO=%~dp0.."
set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
set "DIRECT=%~1"
set "CFGROOT=%~2"
REM 直达（传参）即自动化场景 => 跳过所有 pause，避免无交互环境下阻塞
set "NOPAUSE="
if not "%DIRECT%"=="" set "NOPAUSE=1"
set "INVALID=0"

if not "%DIRECT%"=="" goto DIRECT_ENTRY

:DIRECT_ENTRY
set "CLIENT=%DIRECT%"
goto RUN

:MENU
cls
echo.
echo  ===========================================================================
echo   ArcGIS Pro MCP - 客户端配置向导（逐客户端独立事务）
echo   身份：80 工具 / 错误码 33 / Release 1.0.2 / 支持 ArcGIS Pro 3.5（已实测；3.0-3.4 编译期兼容·未验证）
echo  ===========================================================================
echo   每个选项只处理一个客户端：Plan 预览 -^> 确认 -^> Apply -^> Validate
echo   本向导不提供批量应用（不含任何批量应用形态）
echo.
echo     [1] codex
echo     [2] cursor
echo     [3] deepseek-harness
echo     [4] claude-desktop  （模板校验；Apply 不受支持，仅 Plan + Validate）
echo     [0] 退出
echo.
set "CHOICE="
set /p "CHOICE=请选择 [0-4]: "

if "%CHOICE%"=="1" goto PICK_CODEX
if "%CHOICE%"=="2" goto PICK_CURSOR
if "%CHOICE%"=="3" goto PICK_DEEPSEEK
if "%CHOICE%"=="4" goto PICK_CLAUDE
if "%CHOICE%"=="0" goto END
echo   无效选择。
set /a INVALID+=1
if %INVALID% GEQ 3 goto END
ping -n 2 127.0.0.1 >nul
goto MENU

:PICK_CODEX
set "CLIENT=codex"
goto CONFIRM
:PICK_CURSOR
set "CLIENT=cursor"
goto CONFIRM
:PICK_DEEPSEEK
set "CLIENT=deepseek-harness"
goto CONFIRM
:PICK_CLAUDE
set "CLIENT=claude-desktop"
goto CONFIRM

:CONFIRM
set "OK="
set /p "OK=确认对 [%CLIENT%] 执行独立事务? [Y/N]: "
if /i not "%OK%"=="Y" goto MENU

:RUN
echo.
echo  ---- 客户端: %CLIENT% ----
if not "%CFGROOT%"=="" echo  ---- ConfigRoot(测试/高级): %CFGROOT% ----
echo.
echo  [1/3] Plan 预览（只读，不修改任何配置）
if "%CFGROOT%"=="" (
  "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\client-config.ps1" -Action Plan -Client "%CLIENT%" -Json
) else (
  "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\client-config.ps1" -Action Plan -Client "%CLIENT%" -ConfigRoot "%CFGROOT%" -Json
)
if errorlevel 1 (
  echo   Plan 失败（exit %errorlevel%）。配置未修改，返回菜单。
  if "%NOPAUSE%"=="" pause
  goto MENU
)

if /i "%CLIENT%"=="claude-desktop" goto CLAUDE_PATH

echo.
echo  [2/3] Apply 应用配置（独立事务）
if "%CFGROOT%"=="" (
  "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\client-config.ps1" -Action Apply -Client "%CLIENT%" -Json
) else (
  "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\client-config.ps1" -Action Apply -Client "%CLIENT%" -ConfigRoot "%CFGROOT%" -Json
)
if errorlevel 1 (
  echo   Apply 未成功（exit %errorlevel%）。可用 -Action Restore 还原。
  if "%NOPAUSE%"=="" pause
  goto MENU
)

echo.
echo  [3/3] Validate 复核
if "%CFGROOT%"=="" (
  "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\client-config.ps1" -Action Validate -Client "%CLIENT%" -Json
) else (
  "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\client-config.ps1" -Action Validate -Client "%CLIENT%" -ConfigRoot "%CFGROOT%" -Json
)
echo.
echo   完成。如需还原：client-config.ps1 -Action Restore -Client %CLIENT% -Json
if "%NOPAUSE%"=="" pause
goto MENU

:CLAUDE_PATH
echo.
echo  [2/3] Apply 跳过：claude-desktop 在本版本不支持 Apply（APPLY_UNSUPPORTED）
echo         仅提供模板校验（TEMPLATE_VALID）。
echo.
echo  [3/3] Validate 复核
if "%CFGROOT%"=="" (
  "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\client-config.ps1" -Action Validate -Client "%CLIENT%" -Json
) else (
  "%PS%" -NoProfile -ExecutionPolicy Bypass -File "%REPO%\scripts\client-config.ps1" -Action Validate -Client "%CLIENT%" -ConfigRoot "%CFGROOT%" -Json
)
echo.
echo   完成（claude-desktop：Plan + Validate；未写入任何配置）。
if "%NOPAUSE%"=="" pause
goto MENU

:END
echo 退出向导。未做任何变更（除你已确认的客户端外）。
endlocal
