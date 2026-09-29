@echo off
setlocal

:: Define service name
set SERVICE_NAME="empifisJsonAPI2Service"

echo.
echo =======================================================
echo ==           Starting Windows Service            ==
echo =======================================================
echo.

echo Starting service %SERVICE_NAME%...
sc start %SERVICE_NAME%
if %errorlevel% equ 0 (
    echo Service started successfully.
) else (
    echo Error: Failed to start service. Check if the service is installed and if you have permissions.
    echo        Details: C:\Altera\Log\json2.log and the Windows Event Viewer ^(System log^).
)

pause
endlocal
