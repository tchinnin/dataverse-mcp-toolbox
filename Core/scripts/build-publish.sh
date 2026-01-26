#!/bin/bash
# Script to publish the .NET server for all platforms
# Builds self-contained executables and prepares them for NuGet packaging

set -e

# Navigate to project directory (parent of scripts folder)
cd "$(dirname "$0")/.."

echo "🚀 Building self-contained executables for all platforms..."
echo ""

# Clean old publish directories
rm -rf ./publish

# Array of target platforms
platforms=("osx-arm64" "osx-x64" "win-x64" "linux-x64")

# Build for each platform
for platform in "${platforms[@]}"; do
    echo "📦 Building for $platform..."
    
    # Publish to temporary directory
    temp_dir="./publish/temp-$platform"
    dotnet publish -c Release -r "$platform" -o "$temp_dir" --self-contained true /p:PublishSingleFile=true
    
    # Create final directory and copy only the executable
    final_dir="./publish/$platform"
    mkdir -p "$final_dir"
    
    # Copy only the main executable (not .pdb or other files)
    if [[ "$platform" == win-* ]]; then
        cp "$temp_dir/DataverseMCPToolBox.exe" "$final_dir/"
        echo "   ✓ Copied DataverseMCPToolBox.exe"
    else
        cp "$temp_dir/DataverseMCPToolBox" "$final_dir/"
        chmod +x "$final_dir/DataverseMCPToolBox"
        echo "   ✓ Copied DataverseMCPToolBox (executable)"
    fi
    
    # Clean up temporary directory
    rm -rf "$temp_dir"
    echo ""
done

echo "✅ Build complete! Executables are in ./publish/"
echo ""
echo "📊 Binary sizes:"
du -sh ./publish/*/DataverseMCPToolBox* 2>/dev/null
echo ""
echo "📁 Directory structure for NuGet packaging:"
tree -L 2 ./publish 2>/dev/null || find ./publish -type f -print | sed 's|[^/]*/| |g'
