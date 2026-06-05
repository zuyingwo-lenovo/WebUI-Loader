@echo off
setlocal enabledelayedexpansion

echo ===================================================
echo  WebUI WPF App Build Script
echo ===================================================
echo.

:: 1. Locate Visual Studio vswhere tool
set VSWHERE_PATH="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist %VSWHERE_PATH% (
    echo [ERROR] Visual Studio Installer not found. Please ensure Visual Studio is installed.
    goto :end
)

:: 2. Find MSBuild path
echo Locating MSBuild...
for /f "usebackq tokens=*" %%i in (`%VSWHERE_PATH% -latest -requires Microsoft.Component.MSBuild -property installationPath`) do (
    set VS_INSTALL_DIR=%%i
)

if "!VS_INSTALL_DIR!"=="" (
    echo [ERROR] Visual Studio MSBuild component not found.
    goto :end
)

set MSBUILD_PATH="!VS_INSTALL_DIR!\MSBuild\Current\Bin\MSBuild.exe"
if not exist %MSBUILD_PATH% (
    set MSBUILD_PATH="!VS_INSTALL_DIR!\MSBuild\15.0\Bin\MSBuild.exe"
)

if not exist %MSBUILD_PATH% (
    echo [ERROR] MSBuild.exe could not be located at: %MSBUILD_PATH%
    goto :end
)
echo Found MSBuild: %MSBUILD_PATH%
echo.

:: 3. Restore NuGet packages
echo Restoring NuGet packages...
set NUGET_URL=https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
set NUGET_PATH=%TEMP%\nuget.exe

if not exist "%NUGET_PATH%" (
    echo Downloading nuget.exe to %TEMP%...
    powershell -Command "[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri '%NUGET_URL%' -OutFile '%NUGET_PATH%'"
    if !errorlevel! neq 0 (
        echo [ERROR] Failed to download nuget.exe.
        goto :end
    )
)

"%NUGET_PATH%" restore WebUI.sln
if !errorlevel! neq 0 (
    echo [ERROR] NuGet package restore failed.
    goto :end
)
echo NuGet package restore completed.
echo.

:: 4. Build the solution in Release mode
echo Building project in Release mode...
%MSBUILD_PATH% WebUI.sln /p:Configuration=Release /p:Platform="Any CPU" /t:Rebuild
if !errorlevel! neq 0 (
    echo.
    echo [ERROR] Build failed. Please check the errors above.
    goto :end
)
echo.
echo Build succeeded!
echo.

:: 5. Create clean dist folder
echo Packaging output files to 'dist' folder...
if exist dist rd /s /q dist
mkdir dist

:: Copy core files
xcopy /Y bin\Release\*.exe dist\ >nul
xcopy /Y bin\Release\*.config dist\ >nul
xcopy /Y bin\Release\*.dll dist\ >nul
xcopy /Y bin\Release\*.manifest dist\ >nul
xcopy /Y bin\Release\*.application dist\ >nul
xcopy /Y bin\Release\*.html dist\ >nul

:: Copy WebView2 native runtimes (essential for runtime load of WebView2Loader.dll)
if exist bin\Release\runtimes (
    xcopy /E /I /Y bin\Release\runtimes dist\runtimes\ >nul
)

echo.
echo ===================================================
echo  Build Completed Successfully!
echo  Output directory: %~dp0dist
echo  Executable: dist\WebUI.exe
echo ===================================================
echo.

:end
:: Pause if the script was launched by double-clicking in Explorer
echo %cmdcmdline% | findstr /i /c:"cmd /c" >nul
if %errorlevel% equ 0 pause
