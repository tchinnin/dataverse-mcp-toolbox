#!/bin/bash

# Local development workflow script
# Builds Core + Bridge and copies binaries to Extension for immediate testing
# Use this when developing to test changes without publishing to NuGet

set -e

echo "========================================"
echo "Local Development Install"
echo "Build & Deploy to Extension (Debug)"
echo "========================================"

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$SCRIPT_DIR/.."
EXTENSION_DIR="$ROOT_DIR/Extension"
SERVER_DIR="$EXTENSION_DIR/server/binaries"

echo ""
echo "📦 Step 1: Building Core + Bridge for all platforms..."
echo ""
"$SCRIPT_DIR/build-all.sh"

echo ""
echo "📂 Step 2: Preparing Extension server directory..."
rm -rf "$SERVER_DIR"
mkdir -p "$SERVER_DIR"

echo ""
echo "📥 Step 3: Copying binaries to Extension..."
echo ""

# Copy Core binaries
echo "Copying Core Server..."
for PLATFORM in osx-arm64 osx-x64 win-x64 linux-x64; do
    SRC_DIR="$ROOT_DIR/Core/publish/$PLATFORM"
    DEST_DIR="$SERVER_DIR/runtimes/$PLATFORM/native"
    
    if [ -d "$SRC_DIR" ]; then
        mkdir -p "$DEST_DIR"
        cp -r "$SRC_DIR"/* "$DEST_DIR/"
        echo "  ✓ $PLATFORM"
    else
        echo "  ⚠️  $PLATFORM not found (skip)"
    fi
done

echo ""
echo "Copying Bridge..."
for PLATFORM in osx-arm64 osx-x64 win-x64 linux-x64; do
    SRC_DIR="$ROOT_DIR/Bridge/publish/$PLATFORM"
    DEST_DIR="$SERVER_DIR/runtimes/$PLATFORM/native"
    
    if [ -d "$SRC_DIR" ]; then
        mkdir -p "$DEST_DIR"
        cp -r "$SRC_DIR"/* "$DEST_DIR/"
        echo "  ✓ $PLATFORM"
    else
        echo "  ⚠️  $PLATFORM not found (skip)"
    fi
done

echo ""
echo "🔐 Step 4: Making binaries executable (Unix)..."
chmod +x "$SERVER_DIR"/runtimes/osx-*/native/* 2>/dev/null || true
chmod +x "$SERVER_DIR"/runtimes/linux-*/native/* 2>/dev/null || true

echo ""
echo "========================================"
echo "✅ Local install completed!"
echo "========================================"
echo ""
echo "📂 Binaries installed to:"
echo "   $SERVER_DIR"
echo ""
echo "🚀 Next steps:"
echo "   1. Open VS Code"
echo "   2. Press F5 to debug Extension Development Host"
echo "   3. The Extension will use local binaries"
echo ""
echo "📝 Note: This setup is for development only."
echo "   For production, the Extension downloads binaries from NuGet."
echo ""

