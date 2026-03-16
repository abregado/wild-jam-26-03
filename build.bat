@echo off
setlocal

set GODOT=D:\Programs\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe
set PROJECT_DIR=%~dp0
set DIST_DIR=%PROJECT_DIR%dist

echo === Building C# assemblies ===
dotnet build "%PROJECT_DIR%WildJam2603.csproj" --configuration Release
if errorlevel 1 (
    echo ERROR: dotnet build failed
    exit /b 1
)

echo === Preparing output directories ===
if exist "%PROJECT_DIR%build"       rmdir /s /q "%PROJECT_DIR%build"
if exist "%PROJECT_DIR%build_linux" rmdir /s /q "%PROJECT_DIR%build_linux"
if exist "%DIST_DIR%"               rmdir /s /q "%DIST_DIR%"
mkdir "%PROJECT_DIR%build"
mkdir "%PROJECT_DIR%build_linux"
mkdir "%DIST_DIR%"

echo === Exporting Windows build ===
"%GODOT%" --headless --path "%PROJECT_DIR%" --export-release "Windows Desktop" "%PROJECT_DIR%build\WildJam2603.exe"
if errorlevel 1 (
    echo ERROR: Windows export failed
    exit /b 1
)

echo === Exporting Linux build ===
"%GODOT%" --headless --path "%PROJECT_DIR%" --export-release "Linux" "%PROJECT_DIR%build_linux\WildJam2603.x86_64"
if errorlevel 1 (
    echo ERROR: Linux export failed
    exit /b 1
)

echo === Packaging distributables ===
powershell -NoProfile -Command "Compress-Archive -Path '%PROJECT_DIR%build\*'       -DestinationPath '%DIST_DIR%\WildJam2603-Windows.zip' -Force"
powershell -NoProfile -Command "Compress-Archive -Path '%PROJECT_DIR%build_linux\*' -DestinationPath '%DIST_DIR%\WildJam2603-Linux.zip'   -Force"

echo.
echo Build complete:
echo   dist\WildJam2603-Windows.zip
echo   dist\WildJam2603-Linux.zip
endlocal
