@echo off

REM Set working directory to script location
cd /d "%~dp0"

REM Project root directory
set PROJECT_ROOT=%~dp0
set SOLUTION_FILE=src\DMM_Hide_Launcher.sln
set OUTPUT_DIR=publish

REM Title
title DMM_Hide_Launcher Build Script

:menu
cls
echo =========================================
echo DMM_Hide_Launcher Build Script
echo =========================================
echo.
echo 1. Build Normal Version
echo 2. Build WebView2 Version
echo 3. Exit
echo.
set /p CHOICE=Enter choice [1-3]: 

if "%CHOICE%"=="1" goto build_normal
if "%CHOICE%"=="2" goto build_webview2
if "%CHOICE%"=="3" goto exit_script

echo Invalid choice, try again.
pause
goto menu

:build_normal
set PROJECT_FILE=src\DMM_Hide_Launcher\DMM_Hide_Launcher.csproj
goto set_build_args

:build_webview2
set PROJECT_FILE=src\DMM_Hide_Launcher_Webview2\DMM_Hide_Launcher.csproj
goto set_build_args

:set_build_args
if "%PROJECT_FILE%"=="src\DMM_Hide_Launcher\DMM_Hide_Launcher.csproj" (
    set VERSION_TYPE=normal
    set NATIVE_LIB=false
) else (
    set VERSION_TYPE=webview2
    set NATIVE_LIB=true
)

:build
cls
echo =========================================
echo Building %VERSION_TYPE% version
echo =========================================
echo.
echo 1. x86 (32-bit)
echo 2. x64 (64-bit)
echo 3. Both x86 and x64
echo.
set /p ARCH=Enter architecture [1-3]: 

REM Build x86
if "%ARCH%"=="1" goto build_x86

REM Build x64
if "%ARCH%"=="2" goto build_x64

REM Build both
if "%ARCH%"=="3" goto build_both

echo Invalid architecture, try again.
pause
goto build

:build_x86
if %ERRORLEVEL% NEQ 0 (
    echo Restore failed!
    pause
    goto menu
)

echo Building x86 version...
dotnet publish %PROJECT_FILE% -c Release -r win-x86 --no-self-contained /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=%NATIVE_LIB% /p:DebugType=None /p:TrimUnusedDependencies=true /p:IncludeAllContentForSelfExtract=false -o %OUTPUT_DIR%\%VERSION_TYPE%\x86
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    pause
    goto menu
)

echo Build completed!
echo Output: %OUTPUT_DIR%\%VERSION_TYPE%\x86
echo.
pause
goto menu

:build_x64
if %ERRORLEVEL% NEQ 0 (
    echo Restore failed!
    pause
    goto menu
)

echo Building x64 version...
dotnet publish %PROJECT_FILE% -c Release -r win-x64 --no-self-contained /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=%NATIVE_LIB% /p:DebugType=None /p:TrimUnusedDependencies=true /p:IncludeAllContentForSelfExtract=false -o %OUTPUT_DIR%\%VERSION_TYPE%\x64
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    pause
    goto menu
)

echo Build completed!
echo Output: %OUTPUT_DIR%\%VERSION_TYPE%\x64
echo.
pause
goto menu

:build_both

echo Building x86 version...
dotnet publish %PROJECT_FILE% -c Release -r win-x86 --no-self-contained /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=%NATIVE_LIB% /p:DebugType=None /p:TrimUnusedDependencies=true /p:IncludeAllContentForSelfExtract=false -o %OUTPUT_DIR%\%VERSION_TYPE%\x86
if %ERRORLEVEL% NEQ 0 (
    echo Build x86 failed!
    pause
    goto menu
)

echo Building x64 version...
dotnet publish %PROJECT_FILE% -c Release -r win-x64 --no-self-contained /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=%NATIVE_LIB% /p:DebugType=None /p:TrimUnusedDependencies=true /p:IncludeAllContentForSelfExtract=false -o %OUTPUT_DIR%\%VERSION_TYPE%\x64
if %ERRORLEVEL% NEQ 0 (
    echo Build x64 failed!
    pause
    goto menu
)

echo Build completed!
echo Output: %OUTPUT_DIR%\%VERSION_TYPE%
echo.
pause
goto menu

:exit_script
echo.
echo Thank you for using DMM_Hide_Launcher Build Script!
echo Press any key to exit...
pause >nul
