#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$PROJECT_DIR/build_web"

echo "=== Preparing output directory ==="
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

echo "=== Exporting Web build ==="
"$GODOT_PATH" --headless --path "$PROJECT_DIR" --export-debug "Web" "$BUILD_DIR/index.html"

echo ""
echo "Web build ready in build_web/"
echo ""
echo "Starting local server on http://localhost:8060 ..."
echo "Press Ctrl+C to stop."
echo ""
python3 -m http.server 8060 --directory "$BUILD_DIR"
