#!/bin/bash

# Script to copy local server binaries to extension development storage
# Usage: ./copy-local-server.sh [version]

VERSION=${1:-"0.1.0-alpha"}
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
EXTENSION_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
CORE_DIR="$(cd "$EXTENSION_DIR/../Core" && pwd)"

# VS Code extension development storage path
DEV_STORAGE="$HOME/Library/Application Support/Code/User/globalStorage/tchinnin.dataverse-mcp-toolbox"

echo "📦 Copying local server binaries to extension dev storage..."
echo "   Version: $VERSION"
echo "   From: $CORE_DIR/publish/"
echo "   To: $DEV_STORAGE/server/$VERSION/"
echo ""

# Create target directories
mkdir -p "$DEV_STORAGE/server/$VERSION/osx-arm64"
mkdir -p "$DEV_STORAGE/server/$VERSION/osx-x64"
mkdir -p "$DEV_STORAGE/server/$VERSION/win-x64"
mkdir -p "$DEV_STORAGE/server/$VERSION/linux-x64"

# Copy binaries
if [ -f "$CORE_DIR/publish/osx-arm64/DataverseMCPToolBox" ]; then
    echo "   ✓ Copying osx-arm64..."
    cp "$CORE_DIR/publish/osx-arm64/DataverseMCPToolBox" "$DEV_STORAGE/server/$VERSION/osx-arm64/"
    chmod +x "$DEV_STORAGE/server/$VERSION/osx-arm64/DataverseMCPToolBox"
else
    echo "   ✗ osx-arm64 binary not found"
fi

if [ -f "$CORE_DIR/publish/osx-x64/DataverseMCPToolBox" ]; then
    echo "   ✓ Copying osx-x64..."
    cp "$CORE_DIR/publish/osx-x64/DataverseMCPToolBox" "$DEV_STORAGE/server/$VERSION/osx-x64/"
    chmod +x "$DEV_STORAGE/server/$VERSION/osx-x64/DataverseMCPToolBox"
else
    echo "   ✗ osx-x64 binary not found"
fi

if [ -f "$CORE_DIR/publish/win-x64/DataverseMCPToolBox.exe" ]; then
    echo "   ✓ Copying win-x64..."
    cp "$CORE_DIR/publish/win-x64/DataverseMCPToolBox.exe" "$DEV_STORAGE/server/$VERSION/win-x64/"
else
    echo "   ✗ win-x64 binary not found"
fi

if [ -f "$CORE_DIR/publish/linux-x64/DataverseMCPToolBox" ]; then
    echo "   ✓ Copying linux-x64..."
    cp "$CORE_DIR/publish/linux-x64/DataverseMCPToolBox" "$DEV_STORAGE/server/$VERSION/linux-x64/"
    chmod +x "$DEV_STORAGE/server/$VERSION/linux-x64/DataverseMCPToolBox"
else
    echo "   ✗ linux-x64 binary not found"
fi

echo ""
echo "✅ Local server binaries copied successfully!"
echo "   The extension will now use your local build when running in debug mode."
echo ""
echo "💡 Tip: Set 'dataverse.server.enforcedVersion' to '$VERSION' in settings to ensure this version is used."
echo "⚠️  Remember to reload the Extension Development Host after changes to the server."
