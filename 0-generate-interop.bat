@echo off
setlocal

:: Define the name of the output DLL
set "OUTPUT_DLL_NAME=Interop.Empirija.dll"

echo.
echo =======================================================
echo ==    Generating COM Interop Assembly              ==
echo =======================================================
echo.

:: Get the current directory of the batch file
set "CURRENT_DIR=%~dp0"
cd /d "%CURRENT_DIR%"

:: Find the tlbimp.exe tool path
for /f "tokens=*" %%a in ('where tlbimp.exe') do set "TLBIMP_PATH=%%a"
if "%TLBIMP_PATH%"=="" (
    echo Error: tlbimp.exe not found. Please ensure it's in your system's PATH.
    goto :error
)

:: Get the path to the Empirija COM type library (assumes it's installed)
set "COM_DLL_PATH=C:\Altera\EmpiFisX\EmpiFisX.dll"
if not exist "%COM_DLL_PATH%" (
    echo Error: Empirija COM DLL not found at %COM_DLL_PATH%.
    echo Please update the COM_DLL_PATH variable in this script.
    goto :error
)

:: Run tlbimp.exe to generate the interop DLL
echo Generating "%OUTPUT_DLL_NAME%" from "%COM_DLL_PATH%"...
"%TLBIMP_PATH%" "%COM_DLL_PATH%" /out:"%OUTPUT_DLL_NAME%"
if %errorlevel% neq 0 (
    echo Error during interop assembly generation.
    goto :error
)
echo.

echo Interop assembly generated successfully.
goto :end

:error
echo.
echo An error occurred. Check the messages above for details.

:end
echo.
pause
endlocal