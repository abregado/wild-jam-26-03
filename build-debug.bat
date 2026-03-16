@echo off
setlocal

set GODOT=D:\Programs\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe
set PROJECT_DIR=%~dp0
set PROJECT_DIR_NOSLASH=%PROJECT_DIR:~0,-1%
set BUILD_DIR=%PROJECT_DIR%build_debug

if not exist "%PROJECT_DIR%WildJam2603.sln" (
    echo ERROR: WildJam2603.sln not found.
    echo In the Godot editor: Project ^> Tools ^> C# ^> Create C# Solution
    exit /b 1
)

echo === Building C# assemblies (Debug) ===
dotnet build "%PROJECT_DIR%WildJam2603.sln" --configuration Debug
if errorlevel 1 (
    echo ERROR: dotnet build failed
    exit /b 1
)

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
echo Run it from a terminal to see crash output:
echo   cd build_debug
echo   .\WildJam2603.exe
echo   -- or for guaranteed console output: --
echo   .\WildJam2603_console.exe
endlocal
