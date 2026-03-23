#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$PROJECT_DIR/build_debug"

echo "=== Preparing output directory ==="
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

echo "=== Exporting debug build ==="
"$GODOT_PATH" --headless --path "$PROJECT_DIR" --export-debug "Windows Desktop (Debug)" "$BUILD_DIR/WildJam2603.exe"

echo ""
echo "Debug build ready: build_debug/WildJam2603.exe"
