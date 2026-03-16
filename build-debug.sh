#!/usr/bin/env bash
set -euo pipefail

GODOT="${GODOT:-D:/Programs/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64.exe}"
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$PROJECT_DIR/build_debug"

echo "=== Building C# assemblies (Debug) ==="
dotnet build "$PROJECT_DIR/WildJam2603.sln" --configuration ExportDebug

echo "=== Preparing output directory ==="
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

echo "=== Exporting debug build ==="
"$GODOT" --headless --path "$PROJECT_DIR" --export-debug "Windows Desktop (Debug)" "$BUILD_DIR/WildJam2603.exe"

echo ""
echo "Debug build ready: build_debug/WildJam2603.exe"
echo "Run it from a terminal to see crash output:"
echo "  cd build_debug && ./WildJam2603.exe"
echo "  -- or for guaranteed console output: --"
echo "  cd build_debug && ./WildJam2603_console.exe"
