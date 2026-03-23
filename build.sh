#!/usr/bin/env bash
set -euo pipefail

GODOT="${GODOT:-D:/Programs/Godot_v4.6.1-stable_win64/Godot_v4.6.1-stable_win64.exe}"
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DIST_DIR="$PROJECT_DIR/dist"

echo "=== Building C# assemblies ==="
dotnet build "$PROJECT_DIR/WildJam2603.sln" --configuration ExportRelease

echo "=== Preparing output directories ==="
rm -rf "$PROJECT_DIR/build" "$PROJECT_DIR/build_linux" "$PROJECT_DIR/build_mac" "$DIST_DIR"
mkdir -p "$PROJECT_DIR/build" "$PROJECT_DIR/build_linux" "$PROJECT_DIR/build_mac" "$DIST_DIR"

echo "=== Exporting Windows build ==="
"$GODOT" --headless --path "$PROJECT_DIR" --export-release "Windows Desktop" "$PROJECT_DIR/build/WildJam2603.exe"

echo "=== Exporting Linux build ==="
"$GODOT" --headless --path "$PROJECT_DIR" --export-release "Linux" "$PROJECT_DIR/build_linux/WildJam2603.x86_64"

echo "=== Exporting macOS build ==="
"$GODOT" --headless --path "$PROJECT_DIR" --export-release "macOS" "$PROJECT_DIR/build_mac/WildJam2603.zip"

echo "=== Packaging distributables ==="
_zip() {
    local src="$1" dest="$2"
    if command -v zip &>/dev/null; then
        (cd "$src" && zip -r "$dest" .)
    else
        powershell -NoProfile -Command "Compress-Archive -Path '$(cygpath -w "$src")\\*' -DestinationPath '$(cygpath -w "$dest")' -Force"
    fi
}

_zip "$PROJECT_DIR/build"       "$DIST_DIR/WildJam2603-Windows.zip"
_zip "$PROJECT_DIR/build_linux" "$DIST_DIR/WildJam2603-Linux.zip"
cp "$PROJECT_DIR/build_mac/WildJam2603.zip" "$DIST_DIR/WildJam2603-Mac.zip"

echo ""
echo "Build complete:"
echo "  dist/WildJam2603-Windows.zip"
echo "  dist/WildJam2603-Linux.zip"
echo "  dist/WildJam2603-Mac.zip"