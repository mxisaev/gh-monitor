@echo off
setlocal
set "GH_MONITOR_MODE="
if "%~1"=="--check" goto check
if "%~1"=="--console" goto console
if not "%~1"=="" goto usage
"%SystemRoot%\System32\wscript.exe" "%~dp0Start.vbs"
exit /b %errorlevel%

:check
set "GH_MONITOR_MODE=check"
goto foreground

:console
set "GH_MONITOR_MODE=console"

:foreground
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Start.ps1"
exit /b %errorlevel%

:usage
echo Usage: gh-monitor.cmd [--check ^| --console]
exit /b 2
