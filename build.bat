@echo off
setlocal

set GODOT=D:\Programs\Godot_v4.6.1-stable_win64\Godot_v4.6.1-stable_win64.exe
set PROJECT_DIR=%~dp0
set PROJECT_DIR_NOSLASH=%PROJECT_DIR:~0,-1%
set DIST_DIR=%PROJECT_DIR%dist

if not exist "%PROJECT_DIR%WildJam2603.sln" (
    echo ERROR: WildJam2603.sln not found.
    echo In the Godot editor: Project ^> Tools ^> C# ^> Create C# Solution
    exit /b 1
)

echo === Building C# assemblies ===
dotnet build "%PROJECT_DIR%WildJam2603.sln" --configuration ExportRelease
if errorlevel 1 (
    echo ERROR: dotnet build failed
    exit /b 1
)

echo === Preparing output directories ===
if exist "%PROJECT_DIR%build"       rmdir /s /q "%PROJECT_DIR%build"
if exist "%PROJECT_DIR%build_linux" rmdir /s /q "%PROJECT_DIR%build_linux"
if exist "%PROJECT_DIR%build_mac"   rmdir /s /q "%PROJECT_DIR%build_mac"
if exist "%DIST_DIR%"               rmdir /s /q "%DIST_DIR%"
mkdir "%PROJECT_DIR%build"
mkdir "%PROJECT_DIR%build_linux"
mkdir "%PROJECT_DIR%build_mac"
mkdir "%DIST_DIR%"

echo === Exporting Windows build ===
"%GODOT%" --headless --path "%PROJECT_DIR_NOSLASH%" --export-release "Windows Desktop" "%PROJECT_DIR%build\WildJam2603.exe"
if errorlevel 1 (
    echo ERROR: Windows export failed
    exit /b 1
)

echo === Exporting Linux build ===
"%GODOT%" --headless --path "%PROJECT_DIR_NOSLASH%" --export-release "Linux" "%PROJECT_DIR%build_linux\WildJam2603.x86_64"
if errorlevel 1 (
    echo ERROR: Linux export failed
    exit /b 1
)

echo === Exporting macOS build ===
"%GODOT%" --headless --path "%PROJECT_DIR_NOSLASH%" --export-release "macOS" "%PROJECT_DIR%build_mac\WildJam2603.zip"
if errorlevel 1 (
    echo ERROR: macOS export failed
    exit /b 1
)

echo === Packaging distributables ===
powershell -NoProfile -Command "Compress-Archive -Path '%PROJECT_DIR%build\*'       -DestinationPath '%DIST_DIR%\WildJam2603-Windows.zip' -Force"
powershell -NoProfile -Command "Compress-Archive -Path '%PROJECT_DIR%build_linux\*' -DestinationPath '%DIST_DIR%\WildJam2603-Linux.zip'   -Force"
copy "%PROJECT_DIR%build_mac\WildJam2603.zip" "%DIST_DIR%\WildJam2603-Mac.zip"

echo.
echo Build complete:
echo   dist\WildJam2603-Windows.zip
echo   dist\WildJam2603-Linux.zip
echo   dist\WildJam2603-Mac.zip
endlocal