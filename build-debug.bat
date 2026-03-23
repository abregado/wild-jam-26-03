@echo off
setlocal

set GODOT=D:\Programs\Godot_v4.61-stable_win64\Godot_v4.6.1-stable_win64.exe
set PROJECT_DIR=%~dp0
set PROJECT_DIR_NOSLASH=%PROJECT_DIR:~0,-1%
set BUILD_DIR=%PROJECT_DIR%build_debug

echo === Preparing output directory ===
if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
mkdir "%BUILD_DIR%"

echo === Exporting debug build ===
"%GODOT%" --headless --path "%PROJECT_DIR_NOSLASH%" --export-debug "Windows Desktop (Debug)" "%BUILD_DIR%\WildJam2603.exe"
if errorlevel 1 (
    echo ERROR: Godot export failed
    exit /b 1
)

echo.
echo Debug build ready: build_debug\WildJam2603.exe
endlocal
