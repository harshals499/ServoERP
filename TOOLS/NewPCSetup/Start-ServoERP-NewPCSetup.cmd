@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0ServoERP-NewPCSetup.ps1"
set "EXIT_CODE=%ERRORLEVEL%"
echo.
if not "%EXIT_CODE%"=="0" echo Setup did not complete. Read the message above, correct the issue, and run this file again.
pause
exit /b %EXIT_CODE%
