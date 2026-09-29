@echo off
setlocal

:: Installs (or re-installs) the empifisJsonService2 Windows service from the folder this script is in.
:: Checks the prerequisites first, so it is clear why the service would not run.

set "SERVICE_NAME=empifisJsonAPI2Service"
set "DISPLAY_NAME=EmpiFis JSON API 2"
set "INSTALL_DIR=%~dp0"
set "INSTALL_DIR=%INSTALL_DIR:~0,-1%"
set "EXECUTABLE_NAME=empifisJsonService2.exe"
set "BIN_PATH=%INSTALL_DIR%\%EXECUTABLE_NAME%"
:: demand = start manually (Services, run-service.bat); auto = start with Windows
set "START_TYPE=demand"
set "CONFIG_FILE=C:\Altera\EmpifisJsonAPI\config.json"
set "EMPIFISX_CLSID={AB882ADA-330A-4656-B401-76DDD7F68D08}"

:: Check if the script is running with administrative privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo =======================================================
    echo ==    ERROR: This script must be run as an         ==
    echo ==    administrator. Right-click the file and      ==
    echo ==    select "Run as administrator".               ==
    echo =======================================================
    echo.
    goto :end
)

echo.
echo =======================================================
echo ==     Checking prerequisites                        ==
echo =======================================================
echo.

if not exist "%BIN_PATH%" (
    echo ERROR: %BIN_PATH% not found. Run this script from the install folder.
    goto :end
)
echo [OK] %EXECUTABLE_NAME% found in %INSTALL_DIR%

:: .NET: a self-contained package carries its own runtime (coreclr.dll next to the exe). Otherwise
:: the 32-bit .NET 10 ASP.NET Core and Desktop runtimes must be installed.
if exist "%INSTALL_DIR%\coreclr.dll" (
    echo [OK] .NET runtime included in the package ^(self-contained^)
) else (
    call :check_runtime "Microsoft.AspNetCore.App" || goto :end
    call :check_runtime "Microsoft.WindowsDesktop.App" || goto :end
)

:: EmpiFisX is a 32-bit COM server; it must be registered for 32-bit programs and its DLL must exist.
set "EMPIFISX_DLL="
for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "(Get-ItemProperty 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Classes\CLSID\%EMPIFISX_CLSID%\InprocServer32' -ErrorAction SilentlyContinue).'(default)'"`) do set "EMPIFISX_DLL=%%P"
if not defined EMPIFISX_DLL (
    echo ERROR: EmpiFisX ^(Empirija fiscal COM component^) is not registered for 32-bit programs.
    echo        Install EmpiFis, or register it with:
    echo        %%windir%%\SysWOW64\regsvr32.exe "C:\Altera\VersionX\EmpiFisX.dll"
    goto :end
)
if not exist "%EMPIFISX_DLL%" (
    echo ERROR: EmpiFisX is registered as "%EMPIFISX_DLL%", but that file does not exist.
    echo        Reinstall EmpiFis or register the DLL again with %%windir%%\SysWOW64\regsvr32.exe.
    goto :end
)
echo [OK] EmpiFisX registered: %EMPIFISX_DLL%

if exist "%CONFIG_FILE%" (
    echo [OK] Configuration: %CONFIG_FILE%
) else (
    echo [WARNING] %CONFIG_FILE% not found - the built-in defaults will be used ^(port 5006, file mode on^).
)

echo.
echo =======================================================
echo ==     Installing/Updating Windows Service         ==
echo =======================================================
echo.

:: Check for existing service and stop/delete it
echo Checking for existing service "%SERVICE_NAME%"...
sc query "%SERVICE_NAME%" >nul
if %errorlevel% equ 0 (
    rem Keep an existing automatic start (a till needs it); only new installs get START_TYPE.
    sc qc "%SERVICE_NAME%" | find "AUTO_START" >nul && set "START_TYPE=auto"
    echo Service found. Stopping and deleting it...
    sc stop "%SERVICE_NAME%" >nul
    sc delete "%SERVICE_NAME%"
    ping 127.0.0.1 -n 3 >nul
    echo Service deleted successfully.
) else (
    echo Service not found. Proceeding with installation.
)
echo.

:: Create the new service - The binPath must have a space after the equal sign
echo Creating new service "%SERVICE_NAME%"...
sc create "%SERVICE_NAME%" binPath= "%BIN_PATH%" start= %START_TYPE% DisplayName= "%DISPLAY_NAME%"
if %errorlevel% neq 0 (
    echo Error: Failed to create service. Check the messages above for details.
    goto :end
)
:: If EmpiFis gets stuck and reloading it doesn't help, the service ends its own process; Windows
:: then restarts it: after 5 s, after 30 s, then every 5 minutes (the count resets after a day).
sc failure "%SERVICE_NAME%" reset= 86400 actions= restart/5000/restart/30000/restart/300000 >nul
sc failureflag "%SERVICE_NAME%" 1 >nul
echo Service created successfully, with automatic restart if it fails.
echo Start it with run-service.bat or from Services. Log: C:\Altera\Log\json2.log
echo.
goto :end

:check_runtime
dir /b /ad "%ProgramFiles(x86)%\dotnet\shared\%~1\10.*" >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] 32-bit .NET 10 %~1 installed
    exit /b 0
)
echo ERROR: The 32-bit ^(x86^) .NET 10 %~1 runtime is not installed.
echo        Download "ASP.NET Core Runtime" and "Desktop Runtime" 10.0 for x86 from
echo        https://dotnet.microsoft.com/download/dotnet/10.0
echo        or use the self-contained install package, which needs no runtime.
exit /b 1

:end
pause
endlocal
