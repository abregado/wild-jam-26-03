@echo off
setlocal

set GODOT=D:\Programs\Godot_v4.61-stable_win64\Godot_v4.6.1-stable_win64.exe
set PROJECT_DIR=%~dp0
set PROJECT_DIR_NOSLASH=%PROJECT_DIR:~0,-1%
set BUILD_DIR=%PROJECT_DIR%build_web

echo === Preparing output directory ===
if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
mkdir "%BUILD_DIR%"

echo === Exporting Web build ===
"%GODOT%" --headless --path "%PROJECT_DIR_NOSLASH%" --export-debug "Web" "%BUILD_DIR%\index.html"
if errorlevel 1 (
    echo ERROR: Web export failed
    exit /b 1
)

echo.
echo Web build ready in build_web\
echo.
echo Starting local server on http://localhost:8060 ...
echo Press Ctrl+C to stop.
echo.
python -m http.server 8060 --directory "%BUILD_DIR%"
endlocal
