#!/bin/bash
# Script pour publier le serveur .NET pour toutes les plateformes

# Se placer dans le répertoire du projet (parent du dossier scripts)
cd "$(dirname "$0")/.."

echo "🚀 Building self-contained executables for all platforms..."

# Nettoyer les anciennes publications
rm -rf ./publish

# macOS ARM64 (Apple Silicon)
echo "📦 Building for macOS ARM64..."
dotnet publish -c Release -r osx-arm64 -o ./publish/osx-arm64 --self-contained

# macOS x64 (Intel)
echo "📦 Building for macOS x64..."
dotnet publish -c Release -r osx-x64 -o ./publish/osx-x64 --self-contained

# Windows x64
echo "📦 Building for Windows x64..."
dotnet publish -c Release -r win-x64 -o ./publish/win-x64 --self-contained

# Linux x64
echo "📦 Building for Linux x64..."
dotnet publish -c Release -r linux-x64 -o ./publish/linux-x64 --self-contained

echo "✅ Build complete! Executables are in ./publish/"
echo ""
echo "Sizes:"
du -sh ./publish/*/DataverseMCPToolBox* 2>/dev/null | grep -v "\.pdb"
