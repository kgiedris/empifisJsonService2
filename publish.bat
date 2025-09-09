@echo off
setlocal

:: Define project and output paths
set "PROJECT_DIR=C:\empifisJsonService2"
set "PUBLISH_DIR=C:\Altera\EmpifisJsonAPI"
set "EXECUTABLE_NAME=empifisJsonService2.exe"

echo.
echo =======================================================
echo ==    Publishing empifisJsonService2 as win-x86      ==
echo =======================================================
echo.

:: Change to the project directory
cd /d "%PROJECT_DIR%"

:: Step 1: Generate the interop assembly before publishing
echo Generating COM interop assembly...
call ".\0-generate-interop.bat"
if %errorlevel% neq 0 (
    echo Error: Failed to generate interop assembly. Exiting.
    goto :end
)
echo.

:: Clean the previous build and publish output
echo Cleaning previous build...
dotnet clean --configuration Release
if %errorlevel% neq 0 (
    echo Error during dotnet clean. Exiting.
    goto :end
)
echo.

:: Publish the application as framework-dependent to resolve COM hosting issue
echo Publishing the application to "%PUBLISH_DIR%"...
dotnet publish --runtime win-x86 --configuration Release -p:PublishSingleFile=true --no-self-contained -o "%PUBLISH_DIR%"
if %errorlevel% neq 0 (
    echo Error during dotnet publish. Exiting.
    goto :end
)
echo.

echo Publishing complete.
echo Executable is located at: "%PUBLISH_DIR%\%EXECUTABLE_NAME%"

:end
pause
endlocal

pause
endlocal

pause
endlocal