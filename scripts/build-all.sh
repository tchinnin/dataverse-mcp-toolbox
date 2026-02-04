#!/bin/bash

# Build script for Dataverse MCP ToolBox - Sidecar Architecture
# Builds both the main server and the bridge for all platforms

set -e

echo "========================================"
echo "Dataverse MCP ToolBox - Build All"
echo "Sidecar Architecture (Server + Bridge)"
echo "========================================"

# Définir les répertoires
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
CORE_DIR="$ROOT_DIR/Core"
BRIDGE_DIR="$ROOT_DIR/Bridge"

# Plateformes supportées
PLATFORMS=("osx-arm64" "osx-x64" "win-x64" "linux-x64")

echo ""
echo "📦 Building Main Server (DataverseMCPToolBox)..."
echo ""

# Build Core/Server
for PLATFORM in "${PLATFORMS[@]}"; do
    echo "Building Core for $PLATFORM..."
    
    OUTPUT_DIR="$CORE_DIR/publish/$PLATFORM"
    rm -rf "$OUTPUT_DIR"
    mkdir -p "$OUTPUT_DIR"
    
    dotnet publish "$CORE_DIR/DataverseMCPToolBox.csproj" \
        -c Release \
        -r "$PLATFORM" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -o "$OUTPUT_DIR"
    
    if [ $? -eq 0 ]; then
        echo "✓ Core built for $PLATFORM"
    else
        echo "✗ Failed to build Core for $PLATFORM"
        exit 1
    fi
done

echo ""
echo "🌉 Building Bridge (DataverseMCPToolBox.Bridge)..."
echo ""

# Build Bridge
for PLATFORM in "${PLATFORMS[@]}"; do
    echo "Building Bridge for $PLATFORM..."
    
    OUTPUT_DIR="$BRIDGE_DIR/publish/$PLATFORM"
    rm -rf "$OUTPUT_DIR"
    mkdir -p "$OUTPUT_DIR"
    
    dotnet publish "$BRIDGE_DIR/DataverseMCPToolBox.Bridge.csproj" \
        -c Release \
        -r "$PLATFORM" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -o "$OUTPUT_DIR"
    
    if [ $? -eq 0 ]; then
        echo "✓ Bridge built for $PLATFORM"
    else
        echo "✗ Failed to build Bridge for $PLATFORM"
        exit 1
    fi
done

echo ""
echo "========================================"
echo "✓ Build completed successfully!"
echo "========================================"
echo ""
echo "Outputs:"
echo "  Server: $CORE_DIR/publish/<platform>/"
echo "  Bridge: $BRIDGE_DIR/publish/<platform>/"
echo ""
