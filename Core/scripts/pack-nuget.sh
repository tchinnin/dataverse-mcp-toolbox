#!/bin/bash

# Script to package the .NET MCP server as a NuGet package
# This script builds all platform binaries and creates a .nupkg file

set -e

echo "🚀 Building and packaging DataverseMCPToolBox.Server..."

# Get the directory where this script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$SCRIPT_DIR/.."

cd "$PROJECT_DIR"

echo ""
echo "📦 Step 1: Building platform-specific binaries..."
./scripts/build-publish.sh

echo "📦 Step 2: Creating NuGet package..."
echo ""

# Create nupkg output directory
mkdir -p nupkg

# Pack the project (includes binaries from publish folders)
# The .csproj file maps publish/* folders to runtimes/<platform>/native/ structure
dotnet pack DataverseMCPToolBox.csproj \
    -c Release \
    -o nupkg

echo ""
echo "✅ NuGet package created successfully!"
echo ""
echo "📋 Package details:"
echo "   Location: $PROJECT_DIR/nupkg/"
ls -lh "$PROJECT_DIR/nupkg/"*.nupkg
echo ""

# Verify package structure
if command -v unzip &> /dev/null; then
    latest_pkg=$(ls -t "$PROJECT_DIR/nupkg/"*.nupkg | head -n 1)
    echo "📦 Package structure verification:"
    unzip -l "$latest_pkg" | grep -E "runtimes/.*/native/DataverseMCPToolBox" || echo "⚠️  Warning: No binaries found in expected runtimes/<platform>/native/ structure"
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
