#!/bin/bash

# Script to package the Dataverse MCP Toolbox Runtime (Core + Bridge) as a unified NuGet package
# This script builds all platform binaries for both Core and Bridge, then creates a .nupkg file

set -e

echo "🚀 Building and packaging DataverseMCPToolBox.Runtime (Core + Bridge)..."

# Get the directory where this script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$SCRIPT_DIR/.."
ROOT_DIR="$PROJECT_DIR/.."

cd "$ROOT_DIR"

echo ""
echo "📦 Step 1: Building platform-specific binaries (Core + Bridge)..."
echo "   Running: ./scripts/build-all.sh"
./scripts/build-all.sh

echo ""
echo "📦 Step 2: Creating unified NuGet package..."
echo ""

cd "$PROJECT_DIR"

# Create nupkg output directory
mkdir -p nupkg

# Pack the project (includes binaries from Core/publish/* and Bridge/publish/* folders)
# The .csproj file maps publish/* folders to runtimes/<platform>/native/ structure
dotnet pack DataverseMCPToolBox.csproj \
    -c Release \
    -o nupkg

echo ""
echo "✅ NuGet package created successfully!"
echo ""
echo "📋 Package details:"
echo "   Package ID: DataverseMCPToolBox.Runtime"
echo "   Location: $PROJECT_DIR/nupkg/"
ls -lh "$PROJECT_DIR/nupkg/"*.nupkg
echo ""

# Verify package structure
if command -v unzip &> /dev/null; then
    latest_pkg=$(ls -t "$PROJECT_DIR/nupkg/"*.nupkg | head -n 1)
    echo "📦 Package structure verification:"
    echo ""
    echo "Core Server binaries:"
    unzip -l "$latest_pkg" | grep -E "runtimes/.*/native/DataverseMCPToolBox[^.]" || echo "⚠️  Warning: No Core binaries found"
    echo ""
    echo "Bridge binaries:"
    unzip -l "$latest_pkg" | grep -E "runtimes/.*/native/DataverseMCPToolBox.Bridge" || echo "⚠️  Warning: No Bridge binaries found"
    echo ""
fi

echo "📤 To publish to NuGet.org:"
echo "   dotnet nuget push nupkg/*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json"
echo ""
echo "🧪 To test locally:"
echo "   1. Create a local NuGet source:"
echo "      dotnet nuget add source $PROJECT_DIR/nupkg --name LocalDataverseMCP"
echo "   2. Install in VS Code extension (it will download from your local source)"
echo "   3. Check downloaded structure in:"
echo "      ~/Library/Application Support/Code/User/globalStorage/<extension-id>/server/"
